using System.Collections.Generic;
using UnityEngine;
using Statecraft.Core;
using Statecraft.Data;
using Statecraft.Economy;
using Statecraft.Entities;
using Statecraft.Espionage;
using Statecraft.Factions;

namespace Statecraft.AI
{
    /// <summary>
    /// ── OPPONENT ────────────────────────────────────────────────────────
    /// A priority list, re-evaluated every couple of seconds. Not a behaviour tree,
    /// not a planner — those are worth building once the game is fun, and worthless
    /// before that.
    ///
    /// The one thing this AI does that matters: it plays the ECONOMY, not the map.
    /// It fixes its own binding constraint first (housing, labour, energy, capacity),
    /// and runs operations against whoever looks strongest — which is what makes the
    /// espionage layer feel like a game rather than a toy.
    ///
    /// Deliberately NOT cheating: it pays the same costs and sees rivals only through
    /// the same IntelSystem the player uses. If it beats you, it beat you.
    /// </summary>
    public class AIController : MonoBehaviour
    {
        public int factionId;

        [Header("Personality")]
        [Range(0f, 1f)] public float aggression = 0.5f;      // how eagerly it spends on operations
        [Range(0f, 1f)] public float greed      = 0.5f;      // how eagerly it trades
        public float decisionIntervalSeconds = 2.0f;

        float _cooldown;
        readonly List<Vector3> _usedPlots = new List<Vector3>();
        Vector3 _basePosition;
        int _plotIndex;

        Faction F => FactionRegistry.Instance != null ? FactionRegistry.Instance.Get(factionId) : null;

        void Start()
        {
            var hq = FactionRegistry.Instance != null
                ? FactionRegistry.Instance.FirstBuildingOfRole(factionId, BuildingRole.Capitol, false)
                : null;
            _basePosition = hq != null ? hq.transform.position : transform.position;

            if (SimClock.Instance != null)
            {
                SimClock.Instance.OnTick -= Tick;
                SimClock.Instance.OnTick += Tick;
            }
        }

        void OnDisable() { if (SimClock.Instance != null) SimClock.Instance.OnTick -= Tick; }

        void Tick(float dt)
        {
            _cooldown -= dt;
            if (_cooldown > 0f) return;
            _cooldown = decisionIntervalSeconds;

            var f = F;
            if (f == null) return;

            // Fix the binding constraint first. The order IS the strategy.
            if (FixHousing(f))        return;
            if (FixLabour(f))         return;
            if (FixEnergy(f))         return;
            if (ExpandEconomy(f))     return;
            if (AdvanceOrResearch(f)) return;
            if (RunTrade(f))          return;
            if (RunEspionage(f))      return;
        }

        // ── 1. Housing ────────────────────────────────────────────────────

        bool FixHousing(Faction f)
        {
            if (f.PopulationHeadroom > 3) return false;
            return Build(f, "habitat");
        }

        // ── 2. Labour ─────────────────────────────────────────────────────

        bool FixLabour(Faction f)
        {
            int target = 6 + f.ageIndex * 5;
            var reg = FactionRegistry.Instance;
            int workers = reg.WorkersOf(f.id).Count;

            // Understaffed factories are worse than no factories.
            bool starved = f.LabourEfficiency < 0.85f;
            if (workers >= target && !starved) return false;

            var hq = reg.FirstBuildingOfRole(f.id, BuildingRole.Capitol);
            if (hq == null || hq.queue.Count >= 2) return false;
            return hq.TryQueue("worker");
        }

        // ── 3. Energy ─────────────────────────────────────────────────────

        bool FixEnergy(Faction f)
        {
            bool draining = f.netPerSecond.energy < 0.2f;
            bool thin     = f.stock.energy < 60f;
            if (!draining && !thin) return false;
            return Build(f, "power");
        }

        // ── 4. Economic build-out ─────────────────────────────────────────

        bool ExpandEconomy(Faction f)
        {
            var reg = FactionRegistry.Instance;

            int extractors = CountOfRole(f.id, BuildingRole.Extractor);
            if (extractors < 1 + f.ageIndex && Build(f, "extractor")) return true;

            if (f.ageIndex >= 1)
            {
                if (!reg.HasBuilding(f.id, BuildingRole.Fabricator)  && Build(f, "fabricator"))  return true;
                if (!reg.HasBuilding(f.id, BuildingRole.Exchange)    && Build(f, "exchange"))    return true;
                if (!reg.HasBuilding(f.id, BuildingRole.Directorate) && Build(f, "directorate")) return true;
                if (CountOfRole(f.id, BuildingRole.Fabricator) < f.ageIndex && Build(f, "fabricator")) return true;
            }

            if (!reg.HasBuilding(f.id, BuildingRole.Academy) && f.stock.capital > 500f && Build(f, "academy")) return true;
            if (f.ageIndex >= 2 && !reg.HasBuilding(f.id, BuildingRole.DataCenter) && Build(f, "datacenter")) return true;

            return false;
        }

        int CountOfRole(int fid, BuildingRole role)
        {
            int n = 0;
            var list = FactionRegistry.Instance.BuildingsOf(fid);
            for (int i = 0; i < list.Count; i++)
                if (list[i] != null && list[i].def != null && list[i].def.role == role) n++;
            return n;
        }

        // ── 5. Age & research ─────────────────────────────────────────────

        bool AdvanceOrResearch(Faction f)
        {
            var eco = EconomySystem.Instance;
            if (eco == null) return false;

            string why;
            if (eco.CanAdvanceAge(f, out why) && f.LabourEfficiency > 0.75f)
                return eco.TryAdvanceAge(f);

            if (!string.IsNullOrEmpty(f.researchingTechId)) return false;

            foreach (var kv in Content.Techs)
                if (eco.CanResearch(f, kv.Value, out why)) return eco.TryResearch(f, kv.Key);

            return false;
        }

        // ── 6. Trade ──────────────────────────────────────────────────────

        bool RunTrade(Faction f)
        {
            var market = MarketSystem.Instance;
            var routes = TradeRouteSystem.Instance;
            if (market == null || routes == null) return false;
            if (!FactionRegistry.Instance.HasBuilding(f.id, BuildingRole.Exchange)) return false;

            // Standing exports for whatever we have a surplus of.
            if (routes.UsedSlots(f.id) < routes.SlotsFor(f.id))
            {
                if (f.stock.components > 250f && routes.TryOpenRoute(f, ResourceKind.Components, 1.5f)) return true;
                if (f.stock.materials  > 700f && routes.TryOpenRoute(f, ResourceKind.Materials,  2.5f)) return true;
            }

            // Opportunistic: sell into strength, buy into weakness.
            for (int i = 0; i < ResourceBundle.AllKinds.Length; i++)
            {
                var k = ResourceBundle.AllKinds[i];
                if (!market.IsTradable(k)) continue;

                float dev  = market.DeviationOf(k);
                float have = f.stock.Get(k);

                if (dev > 0.25f && have > 300f && Random.value < greed)
                    return market.Sell(f, k, Mathf.Min(120f, have * 0.2f));

                if (dev < -0.25f && f.stock.capital > 900f && Random.value < greed * 0.6f)
                    return market.Buy(f, k, 80f);
            }
            return false;
        }

        // ── 7. Espionage ──────────────────────────────────────────────────

        bool RunEspionage(Faction f)
        {
            var reg = FactionRegistry.Instance;
            var esp = EspionageSystem.Instance;
            if (esp == null || f.ageIndex < 1) return false;
            if (!reg.HasBuilding(f.id, BuildingRole.Directorate)) return false;

            // Keep a small stable of agents.
            var agents = reg.AgentsOf(f.id);
            int wantAgents = 1 + Mathf.RoundToInt(aggression * 2f) + f.ageIndex / 2;
            if (agents.Count < wantAgents)
            {
                var dir = reg.FirstBuildingOfRole(f.id, BuildingRole.Directorate);
                if (dir != null && dir.queue.Count == 0 && dir.TryQueue("agent")) return true;
            }

            AgentUnit free = null;
            for (int i = 0; i < agents.Count; i++)
                if (agents[i].IsAvailable && agents[i].cover > 45f) { free = agents[i]; break; }
            if (free == null) return false;

            // Defensive sweep if we are being worked over.
            if (f.opsCaught >= 2 && f.securityBoostSeconds <= 0f && Random.value < 0.5f)
                return esp.TryLaunch(f, free, "counter_sweep", f);

            var target = PickTarget(f);
            if (target == null) return false;

            string opId = PickOperation(f, target);
            if (opId == null) return false;
            if (Random.value > 0.35f + aggression * 0.5f) return false;

            return esp.TryLaunch(f, free, opId, target);
        }

        /// <summary>Whoever looks strongest through our own (fallible) intel.</summary>
        Faction PickTarget(Faction f)
        {
            var intel = IntelSystem.Instance;
            Faction best = null;
            float bestScore = -1f;

            foreach (var other in FactionRegistry.Instance.Others(f.id))
            {
                var e = intel != null ? intel.Get(f.id, other) : default(IntelSystem.Estimate);
                float score = e.hasAny
                    ? e.stock.capital + e.stock.components * 5f + e.stock.data * 14f + e.ageIndex * 500f
                    : 400f;                                     // unknown rivals are worth scouting
                if (DiplomacySystem.Instance != null)
                    score += DiplomacySystem.Instance.TensionBetween(f.id, other.id) * 120f;
                if (score > bestScore) { bestScore = score; best = other; }
            }
            return best;
        }

        string PickOperation(Faction f, Faction target)
        {
            var intel = IntelSystem.Instance;
            var e = intel != null ? intel.Get(f.id, target) : default(IntelSystem.Estimate);

            // No current picture? The cheapest way to get one is a data lift.
            if (!e.hasAny || e.confidence < 0.25f) return "steal_data";

            var options = new List<string>();
            if (e.stock.data > 120f)                                     options.Add("steal_data");
            if (e.techs != null && e.techs.Count > 0 && f.ageIndex >= 2) options.Add("steal_tech");
            if (e.buildingCount > 4)                                     options.Add("sabotage_production");
            options.Add("sabotage_trade");
            if (f.ageIndex >= 2)                                         options.Add("market_manipulation");
            if (e.stock.capital > 800f)                                  options.Add("insider_trading");
            options.Add("disinformation");

            return options[Random.Range(0, options.Count)];
        }

        // ── Placement ─────────────────────────────────────────────────────

        bool Build(Faction f, string buildingId)
        {
            var def = Content.Building(buildingId);
            if (def == null || f.ageIndex < def.minAge || !f.CanAfford(def.cost)) return false;

            var spot   = NextPlot(def);
            var placed = EconomySystem.Instance != null
                ? EconomySystem.Instance.TryPlaceBuilding(f, buildingId, spot)
                : null;
            if (placed == null) return false;

            _usedPlots.Add(spot);
            return true;
        }

        /// <summary>A widening spiral of plots around the Capitol. Ugly, but it never overlaps.</summary>
        Vector3 NextPlot(BuildingDef def)
        {
            for (int attempt = 0; attempt < 40; attempt++)
            {
                _plotIndex++;
                float ring  = 14f + (_plotIndex / 8) * 11f;
                float angle = (_plotIndex % 8) * (Mathf.PI * 2f / 8f) + (_plotIndex / 8) * 0.4f;
                var spot = _basePosition + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * ring;

                bool clear = true;
                for (int i = 0; i < _usedPlots.Count; i++)
                    if ((_usedPlots[i] - spot).sqrMagnitude < 90f) { clear = false; break; }
                if (clear) return spot;
            }
            return _basePosition + new Vector3(Random.Range(-40f, 40f), 0f, Random.Range(-40f, 40f));
        }
    }
}
