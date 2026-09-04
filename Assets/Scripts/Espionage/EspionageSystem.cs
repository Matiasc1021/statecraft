using System.Collections.Generic;
using UnityEngine;
using Statecraft.Core;
using Statecraft.Data;
using Statecraft.Economy;
using Statecraft.Entities;
using Statecraft.Factions;

namespace Statecraft.Espionage
{
    /// <summary>
    /// ── OPERATIONS ──────────────────────────────────────────────────────
    /// Espionage is the second economy. You spend Capital and Data to buy a CHANCE
    /// to move someone else's economy, and the price of trying is cover.
    ///
    /// Every operation resolves as two independent rolls:
    ///
    ///   SUCCESS  = base + (skill - 50) x 0.006 + (cover/100) x 0.15 - targetSecurity x 0.004
    ///   DETECTED = base x attackerDetectionMult + targetSecurity x 0.003 - (cover/100) x 0.25
    ///
    /// They are independent on purpose, which gives four outcomes rather than two:
    ///   • Clean success   — the good one.
    ///   • Loud success    — it worked and they know it was you. Expect retaliation.
    ///   • Quiet failure   — you wasted the money. Nobody knows.
    ///   • Blown operation — it failed AND they caught you. The agent may be burned.
    ///
    /// A defender does not buy immunity, they buy a better distribution. That is why
    /// security is worth building even though it never blocks anything outright.
    /// </summary>
    public class EspionageSystem : MonoBehaviour
    {
        public static EspionageSystem Instance { get; private set; }

        [Header("Resolution")]
        public float skillWeight      = 0.006f;
        public float coverSuccessWeight = 0.15f;
        public float securityWeight   = 0.004f;
        public float securityDetectWeight = 0.003f;
        public float coverStealthWeight = 0.25f;

        [Header("Consequences")]
        public float burnChanceOnBlown = 0.45f;
        public float counterSweepSecurityBoost = 25f;
        public float counterSweepSeconds       = 180f;

        class RunningOp
        {
            public AgentUnit     agent;
            public OperationDef  def;
            public int           attackerId;
            public int           targetId;
            public float         secondsLeft;
            public float         coverAtLaunch;
        }

        readonly List<RunningOp> _running = new List<RunningOp>();

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        void Start()
        {
            if (SimClock.Instance != null)
            {
                SimClock.Instance.OnTick -= Tick;
                SimClock.Instance.OnTick += Tick;
            }
        }

        void OnDisable() { if (SimClock.Instance != null) SimClock.Instance.OnTick -= Tick; }
        void OnDestroy() { if (Instance == this) Instance = null; }

        // ── Launching ─────────────────────────────────────────────────────

        public bool CanLaunch(Faction attacker, AgentUnit agent, OperationDef op, Faction target, out string reason)
        {
            reason = null;
            if (op == null)                      { reason = "No operation selected."; return false; }
            if (agent == null || !agent.IsAlive) { reason = "No agent available.";    return false; }
            if (!agent.IsAvailable)              { reason = "Agent is busy.";         return false; }
            if (attacker.ageIndex < op.minAge)   { reason = $"Requires {Content.Age(op.minAge).displayName}."; return false; }

            if (!FactionRegistry.Instance.HasBuilding(attacker.id, BuildingRole.Directorate))
            { reason = "Requires an operational Directorate."; return false; }

            if (op.selfTargeted) target = attacker;
            if (target == null)                  { reason = "No target selected."; return false; }
            if (!op.selfTargeted && target.id == attacker.id) { reason = "That operation needs a foreign target."; return false; }

            var cost = op.cost * attacker.OperationCostMultiplier;
            if (!attacker.CanAfford(cost))
            {
                var missing = attacker.stock.FirstShortfall(cost);
                reason = $"Short on {(missing.HasValue ? ResourceBundle.Label(missing.Value) : "resources")}.";
                return false;
            }
            return true;
        }

        public bool TryLaunch(Faction attacker, AgentUnit agent, string operationId, Faction target)
        {
            var op = Content.Operation(operationId);
            if (!CanLaunch(attacker, agent, op, target, out var reason))
            {
                if (attacker.isHuman) EventLog.Post(LogChannel.Espionage, $"Operation refused — {reason}", attacker.id);
                return false;
            }

            if (op.selfTargeted) target = attacker;
            attacker.TrySpend(op.cost * attacker.OperationCostMultiplier);

            agent.state                = AgentUnit.AgentState.Running;
            agent.runningOperationId   = op.id;
            agent.operationSecondsLeft = op.durationSeconds;
            agent.deployedAgainst      = target.id;

            _running.Add(new RunningOp {
                agent         = agent,
                def           = op,
                attackerId    = attacker.id,
                targetId      = target.id,
                secondsLeft   = op.durationSeconds,
                coverAtLaunch = agent.cover,
            });

            EconomyStats.For(attacker.id).opsAttempted++;
            EventLog.Post(LogChannel.Espionage,
                $"{op.displayName} under way against {target.displayName}…", attacker.id);
            return true;
        }

        // ── Tick ──────────────────────────────────────────────────────────

        void Tick(float dt)
        {
            for (int i = _running.Count - 1; i >= 0; i--)
            {
                var run = _running[i];
                if (run.agent == null || !run.agent.IsAlive) { _running.RemoveAt(i); continue; }

                run.secondsLeft -= dt;
                run.agent.operationSecondsLeft = Mathf.Max(0f, run.secondsLeft);
                if (run.secondsLeft > 0f) continue;

                _running.RemoveAt(i);
                Resolve(run);
            }
        }

        // ── Resolution ────────────────────────────────────────────────────

        void Resolve(RunningOp run)
        {
            var reg      = FactionRegistry.Instance;
            var attacker = reg.Get(run.attackerId);
            var target   = reg.Get(run.targetId);
            var agent    = run.agent;
            var op       = run.def;

            if (attacker == null || target == null) return;

            float cover = run.coverAtLaunch;
            agent.BurnCover(op.coverCost);
            agent.state              = AgentUnit.AgentState.Idle;
            agent.runningOperationId = null;
            agent.deployedAgainst    = -1;

            float successChance = op.baseSuccess
                                + (agent.skill - 50f) * skillWeight
                                + (cover / 100f) * coverSuccessWeight
                                - (op.selfTargeted ? 0f : target.Security * securityWeight);

            float detectChance  = op.selfTargeted ? 0f
                                : op.baseDetection * attacker.DetectionMultiplier
                                + target.Security * securityDetectWeight
                                - (cover / 100f) * coverStealthWeight;

            successChance = Mathf.Clamp(successChance, 0.05f, 0.95f);
            detectChance  = Mathf.Clamp(detectChance,  0.00f, 0.95f);

            bool success  = Random.value < successChance;
            bool detected = Random.value < detectChance;

            if (!op.selfTargeted) target.opsRunAgainstMe++;

            if (success)
            {
                EconomyStats.For(attacker.id).opsSucceeded++;
                ApplyEffect(op, attacker, target, agent);
                IntelSystem.Instance?.Refresh(attacker.id, target);

                EventLog.Post(LogChannel.Espionage,
                    $"{op.displayName} SUCCEEDED against {target.displayName}." + (detected ? " Cover blown." : ""),
                    attacker.id);
            }
            else
            {
                EventLog.Post(LogChannel.Espionage,
                    $"{op.displayName} failed against {target.displayName}." + (detected ? " The agent was spotted." : " No trace left."),
                    attacker.id);
            }

            if (detected)
            {
                EconomyStats.For(attacker.id).opsDetected++;
                target.opsCaught++;
                EventLog.Post(LogChannel.Espionage,
                    $"COUNTER-INTELLIGENCE: {attacker.displayName} ran {op.displayName} against us" +
                    (success ? " and it worked." : " and we stopped it."),
                    target.id, new Color(1f, 0.55f, 0.35f));

                DiplomacySystem.Instance?.RegisterHostileAct(attacker.id, target.id, success ? 2f : 1f);

                bool burn = !success && Random.value < burnChanceOnBlown;
                if (burn || agent.cover <= 1f)
                {
                    EventLog.Post(LogChannel.Espionage, "Agent burned. We have lost them.", attacker.id,
                        new Color(1f, 0.4f, 0.4f));
                    agent.state = AgentUnit.AgentState.Burned;
                    agent.Damage(agent.maxHp * 10f, target.id);
                }
            }
        }

        // ── Effects ───────────────────────────────────────────────────────

        void ApplyEffect(OperationDef op, Faction attacker, Faction target, AgentUnit agent)
        {
            var reg    = FactionRegistry.Instance;
            var market = MarketSystem.Instance;

            switch (op.kind)
            {
                case OperationKind.StealData:
                {
                    float amount = Mathf.Min(target.stock.data * 0.30f, 250f);
                    amount = target.Drain(ResourceKind.Data, amount);
                    attacker.Grant(ResourceKind.Data, amount);
                    EventLog.Post(LogChannel.Espionage, $"Exfiltrated {amount:0} Data.", attacker.id);
                    break;
                }

                case OperationKind.StealTech:
                {
                    string stolen = null;
                    foreach (var t in target.techs) if (!attacker.HasTech(t)) { stolen = t; break; }
                    if (stolen == null)
                    {
                        EventLog.Post(LogChannel.Espionage, "They had nothing we do not already have.", attacker.id);
                        break;
                    }
                    attacker.techs.Add(stolen);
                    EventLog.Post(LogChannel.Espionage,
                        $"Acquired {Content.Tech(stolen)?.displayName ?? stolen}.", attacker.id);
                    break;
                }

                case OperationKind.SabotageProduction:
                {
                    var b = reg.RandomProductionBuilding(target.id);
                    if (b == null) { EventLog.Post(LogChannel.Espionage, "No plant worth hitting.", attacker.id); break; }
                    float seconds = EconomySystem.Instance != null ? EconomySystem.Instance.sabotageProductionSeconds : 60f;
                    b.ApplySabotage(seconds);
                    EventLog.Post(LogChannel.Espionage, $"{b.def.displayName} offline for {seconds:0}s.", attacker.id);
                    EventLog.Post(LogChannel.Economy, $"{b.def.displayName} has gone offline. Sabotage suspected.", target.id);
                    break;
                }

                case OperationKind.SabotageTrade:
                {
                    bool hit = TradeRouteSystem.Instance != null && TradeRouteSystem.Instance.DisruptRandomRoute(target.id);
                    EventLog.Post(LogChannel.Espionage,
                        hit ? "Trade route throttled." : "They run no trade routes to cut.", attacker.id);
                    if (hit) EventLog.Post(LogChannel.Economy, "A trade route has collapsed to a trickle.", target.id);
                    break;
                }

                case OperationKind.MarketManipulation:
                {
                    if (market == null) break;
                    var kind = market.LargestHolding(target);
                    market.ForcePriceShift(kind, -0.25f);
                    EventLog.Post(LogChannel.Espionage,
                        $"{ResourceBundle.Label(kind)} driven down 25% — they are long that book.", attacker.id);
                    EventLog.Post(LogChannel.Economy,
                        $"{ResourceBundle.Label(kind)} has crashed. Someone moved against us.", target.id);
                    break;
                }

                case OperationKind.InsiderTrading:
                {
                    float gain = Mathf.Clamp(Mathf.Abs(target.stock.capital) * 0.10f, 60f, 700f);
                    attacker.stock.capital += gain;
                    target.stock.capital   -= gain * 0.5f;
                    EconomyStats.For(attacker.id).capitalStolenByOps += gain;
                    EconomyStats.For(target.id).capitalLostToOps     += gain * 0.5f;
                    EventLog.Post(LogChannel.Espionage, $"Position closed: +{gain:0} Capital.", attacker.id);
                    break;
                }

                case OperationKind.Disinformation:
                {
                    IntelSystem.Instance?.Corrupt(target.id, attacker.id, 120f);
                    EventLog.Post(LogChannel.Espionage,
                        "Their picture of us is now wrong, and they do not know it.", attacker.id);
                    break;
                }

                case OperationKind.CounterSweep:
                {
                    attacker.securityBoost        = counterSweepSecurityBoost;
                    attacker.securityBoostSeconds = counterSweepSeconds;
                    EventLog.Post(LogChannel.Espionage,
                        $"Sweep complete. Security +{counterSweepSecurityBoost:0} for {counterSweepSeconds:0}s.", attacker.id);

                    // Anyone currently running an operation against us may be caught in the net.
                    for (int i = 0; i < _running.Count; i++)
                    {
                        var other = _running[i];
                        if (other.targetId != attacker.id) continue;
                        if (Random.value > 0.35f) continue;
                        EventLog.Post(LogChannel.Espionage, "The sweep caught a hostile agent in place.", attacker.id);
                        EventLog.Post(LogChannel.Espionage, "Our agent was rolled up mid-operation.", other.attackerId,
                            new Color(1f, 0.4f, 0.4f));
                        if (other.agent != null) other.agent.Damage(other.agent.maxHp * 10f, attacker.id);
                    }
                    break;
                }
            }
        }

        // ── Queries for the HUD and the AI ────────────────────────────────

        public float PreviewSuccess(Faction attacker, AgentUnit agent, OperationDef op, Faction target)
        {
            if (op == null || agent == null) return 0f;
            float sec = (op.selfTargeted || target == null) ? 0f : target.Security;
            return Mathf.Clamp(op.baseSuccess
                 + (agent.skill - 50f) * skillWeight
                 + (agent.cover / 100f) * coverSuccessWeight
                 - sec * securityWeight, 0.05f, 0.95f);
        }

        public float PreviewDetection(Faction attacker, AgentUnit agent, OperationDef op, Faction target)
        {
            if (op == null || agent == null || op.selfTargeted || target == null) return 0f;
            return Mathf.Clamp(op.baseDetection * attacker.DetectionMultiplier
                 + target.Security * securityDetectWeight
                 - (agent.cover / 100f) * coverStealthWeight, 0f, 0.95f);
        }

        public int RunningCount(int factionId)
        {
            int n = 0;
            for (int i = 0; i < _running.Count; i++) if (_running[i].attackerId == factionId) n++;
            return n;
        }
    }
}
