using System.Collections.Generic;
using UnityEngine;
using Statecraft.Core;
using Statecraft.Data;
using Statecraft.Entities;
using Statecraft.Factions;

namespace Statecraft.Economy
{
    /// <summary>
    /// ── THE ECONOMY TICK ────────────────────────────────────────────────
    /// Runs once per SimClock tick, in a fixed order, for every faction:
    ///
    ///   1. Capacity pass   — housing, security, trade slots, labour DEMAND from buildings
    ///   2. Labour pass     — population and labour SUPPLY from units, then efficiency
    ///   3. Conversion pass — recipes consume inputs and emit outputs at that efficiency
    ///   4. Production pass — training queues advance, finished units spawn
    ///   5. Finance pass    — interest on debt
    ///   6. Progress pass   — age advancement and research
    ///
    /// The order matters and is the reason the sim is predictable: nothing in step 3
    /// can be affected by something that happens in step 4 of the same tick.
    ///
    /// The two pressures that make this game an economy rather than a resource counter:
    ///   • LABOUR   — build more factories than you can staff and everything slows down.
    ///   • ENERGY   — energy is an input like any other, so a power shortfall throttles
    ///                every downstream recipe at once. Brownouts cascade.
    /// </summary>
    public class EconomySystem : MonoBehaviour
    {
        public static EconomySystem Instance { get; private set; }

        [Header("Finance")]
        [Tooltip("Interest charged per second on negative Capital, as a fraction of the debt.")]
        public float debtInterestPerSecond = 0.005f;

        [Header("Sabotage")]
        public float sabotageProductionSeconds = 60f;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        void OnEnable()
        {
            if (SimClock.Instance != null) SimClock.Instance.OnTick += Tick;
        }

        void Start()
        {
            // SimClock may have been created after us.
            if (SimClock.Instance != null)
            {
                SimClock.Instance.OnTick -= Tick;
                SimClock.Instance.OnTick += Tick;
            }
        }

        void OnDisable()
        {
            if (SimClock.Instance != null) SimClock.Instance.OnTick -= Tick;
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        // ──────────────────────────────────────────────────────────────────

        void Tick(float dt)
        {
            var reg = FactionRegistry.Instance;
            if (reg == null) return;

            for (int i = 0; i < reg.Factions.Count; i++)
            {
                var f = reg.Factions[i];
                f.netPerSecond = ResourceBundle.Zero;

                CapacityPass(f, reg, dt);
                LabourPass(f, reg);
                ConversionPass(f, reg, dt);
                ProductionPass(f, reg, dt);
                FinancePass(f, dt);
                ProgressPass(f, dt);
                AgentPass(f, reg, dt);
            }
        }

        // ── 1. Capacity ───────────────────────────────────────────────────

        void CapacityPass(Faction f, FactionRegistry reg, float dt)
        {
            f.housingCapacity  = 0;
            f.buildingSecurity = 0f;
            f.labourDemand     = 0;

            var buildings = reg.BuildingsOf(f.id);
            for (int i = buildings.Count - 1; i >= 0; i--)
            {
                var b = buildings[i];
                if (b == null) { buildings.RemoveAt(i); continue; }

                b.TickSabotage(dt);

                if (b.underConstruction)
                {
                    if (b.AdvanceConstruction(dt, f.BuildSpeedMultiplier))
                        EventLog.Post(LogChannel.Economy, $"{b.def.displayName} completed.", f.id);
                    continue;
                }

                f.housingCapacity  += b.def.housingProvided;
                f.buildingSecurity += b.def.securityBonus;

                if (b.HasRecipe && b.IsOperational)
                    f.labourDemand += b.def.recipe.labourSlots;
            }

            // Counter-intelligence boost decays.
            if (f.securityBoostSeconds > 0f)
            {
                f.securityBoostSeconds -= dt;
                if (f.securityBoostSeconds <= 0f) { f.securityBoostSeconds = 0f; f.securityBoost = 0f; }
            }
        }

        // ── 2. Labour ─────────────────────────────────────────────────────

        void LabourPass(Faction f, FactionRegistry reg)
        {
            int pop = 0, workers = 0;
            var units = reg.UnitsOf(f.id);
            for (int i = units.Count - 1; i >= 0; i--)
            {
                var u = units[i];
                if (u == null || !u.IsAlive) { units.RemoveAt(i); continue; }
                pop += u.def != null ? u.def.housingCost : 1;
                if (u is WorkerUnit) workers++;
            }
            f.population   = pop;
            f.labourSupply = workers;

            // Gathering income, for the HUD's rate display.
            var gatherers = reg.WorkersOf(f.id);
            for (int i = 0; i < gatherers.Count; i++)
            {
                var w = gatherers[i];
                if (w.state != WorkerUnit.WorkState.Gathering) continue;
                f.netPerSecond.Add(w.carriedKind, w.gatherPerSecond * f.ExtractionMultiplier);
            }
        }

        // ── 3. Conversion ─────────────────────────────────────────────────

        void ConversionPass(Faction f, FactionRegistry reg, float dt)
        {
            float labourEff = f.LabourEfficiency;
            var buildings = reg.BuildingsOf(f.id);

            for (int i = 0; i < buildings.Count; i++)
            {
                var b = buildings[i];
                if (b == null || !b.HasRecipe || !b.IsOperational) { if (b != null && !b.IsOperational) b.lastEfficiency = 0f; continue; }

                var recipe = b.def.recipe;

                // Input required this tick, with the energy discount applied.
                var need = recipe.inputPerSecond * dt;
                need.energy *= f.EnergyInputMultiplier;

                // How much of that we can actually pay for.
                float supplyRatio = 1f;
                for (int k = 0; k < ResourceBundle.AllKinds.Length; k++)
                {
                    var kind = ResourceBundle.AllKinds[k];
                    float required = need.Get(kind);
                    if (required <= 0f) continue;
                    float have = f.stock.Get(kind);
                    supplyRatio = Mathf.Min(supplyRatio, have <= 0f ? 0f : Mathf.Clamp01(have / required));
                }

                float eff = Mathf.Clamp01(Mathf.Min(labourEff, supplyRatio));
                b.lastEfficiency = eff;
                if (eff <= 0.001f) continue;

                // Consume.
                var consumed = need * eff;
                for (int k = 0; k < ResourceBundle.AllKinds.Length; k++)
                {
                    var kind = ResourceBundle.AllKinds[k];
                    float amount = consumed.Get(kind);
                    if (amount > 0f) f.Drain(kind, amount);
                }

                // Produce, with the per-resource civ/tech/age multipliers.
                var gross = recipe.outputPerSecond * dt * eff;
                var gained = ResourceBundle.Zero;
                gained.materials  = gross.materials  * f.ExtractionMultiplier;
                gained.components = gross.components * f.ManufacturingMultiplier;
                gained.data       = gross.data       * f.DataMultiplier;
                gained.energy     = gross.energy     * f.Age.outputMultiplier;
                gained.capital    = gross.capital;

                f.Grant(gained);
                EconomyStats.RecordProduced(f.id, gained);

                // Rate display (per second, not per tick).
                var perSecondIn  = consumed * (1f / dt);
                var perSecondOut = gained   * (1f / dt);
                f.netPerSecond = f.netPerSecond + perSecondOut - perSecondIn;
            }
        }

        // ── 4. Production queues ──────────────────────────────────────────

        void ProductionPass(Faction f, FactionRegistry reg, float dt)
        {
            var buildings = reg.BuildingsOf(f.id);
            for (int i = 0; i < buildings.Count; i++)
            {
                var b = buildings[i];
                if (b == null || b.queue.Count == 0) continue;

                string finished = b.AdvanceProduction(dt, f.BuildSpeedMultiplier);
                if (finished == null) continue;

                Vector3 spawnAt = b.rallyPoint + new Vector3(Random.Range(-2f, 2f), 0f, Random.Range(-2f, 2f));
                var unit = Spawner.SpawnUnit(f.id, finished, spawnAt);

                // A fresh worker goes straight to the nearest deposit — no idle villagers.
                if (unit is WorkerUnit w)
                {
                    var node = reg.NearestFreeNode(spawnAt, ResourceKind.Materials) ?? reg.NearestFreeNode(spawnAt, null);
                    if (node != null) w.AssignTo(node);
                }
            }
        }

        // ── 5. Finance ────────────────────────────────────────────────────

        void FinancePass(Faction f, float dt)
        {
            if (f.stock.capital >= 0f) return;

            float interest = -f.stock.capital * debtInterestPerSecond * dt;
            f.stock.capital -= interest;
            f.interestPaidTotal += interest;
            f.netPerSecond.capital -= interest / dt;
        }

        // ── 6. Age & research ─────────────────────────────────────────────

        void ProgressPass(Faction f, float dt)
        {
            if (f.advancingAge)
            {
                var age = Content.Age(f.ageIndex + 1);
                f.ageProgress += dt / Mathf.Max(1f, age.advanceSeconds);
                if (f.ageProgress >= 1f)
                {
                    f.ageProgress  = 0f;
                    f.advancingAge = false;
                    f.ageIndex     = Mathf.Min(Content.MaxAge, f.ageIndex + 1);
                    EventLog.Post(LogChannel.Economy, $"{f.displayName} advanced to {f.Age.displayName}.", -1);
                }
            }

            if (!string.IsNullOrEmpty(f.researchingTechId))
            {
                var tech = Content.Tech(f.researchingTechId);
                if (tech == null) { f.researchingTechId = null; return; }

                f.researchProgress += (dt / Mathf.Max(1f, tech.researchSeconds)) * f.ResearchSpeedMultiplier;
                if (f.researchProgress >= 1f)
                {
                    f.techs.Add(tech.id);
                    EventLog.Post(LogChannel.Economy, $"Researched {tech.displayName}.", f.id);
                    f.researchingTechId = null;
                    f.researchProgress  = 0f;
                }
            }
        }

        // ── Agents ────────────────────────────────────────────────────────

        void AgentPass(Faction f, FactionRegistry reg, float dt)
        {
            var agents = reg.AgentsOf(f.id);
            for (int i = 0; i < agents.Count; i++) agents[i].TickCover(dt);
        }

        // ── Public commands (HUD and AI both go through these) ────────────

        public bool CanAdvanceAge(Faction f, out string reason)
        {
            reason = null;
            if (f.ageIndex >= Content.MaxAge) { reason = "Already at the final age."; return false; }
            if (f.advancingAge)               { reason = "Already advancing.";        return false; }

            var next = Content.Age(f.ageIndex + 1);
            if (!f.CanAfford(next.advanceCost))
            {
                var missing = f.stock.FirstShortfall(next.advanceCost);
                reason = $"Short on {(missing.HasValue ? ResourceBundle.Label(missing.Value) : "resources")}.";
                return false;
            }
            if (FactionRegistry.Instance.FirstBuildingOfRole(f.id, BuildingRole.Capitol) == null)
            {
                reason = "Requires an operational Capitol.";
                return false;
            }
            return true;
        }

        public bool TryAdvanceAge(Faction f)
        {
            if (!CanAdvanceAge(f, out var reason))
            {
                if (f.isHuman) EventLog.Post(LogChannel.Economy, $"Cannot advance age — {reason}", f.id);
                return false;
            }
            var next = Content.Age(f.ageIndex + 1);
            f.TrySpend(next.advanceCost);
            f.advancingAge = true;
            f.ageProgress  = 0f;
            EventLog.Post(LogChannel.Economy, $"Advancing to {next.displayName}…", f.id);
            return true;
        }

        public bool CanResearch(Faction f, TechDef tech, out string reason)
        {
            reason = null;
            if (tech == null)                       { reason = "No such technology.";      return false; }
            if (f.HasTech(tech.id))                 { reason = "Already researched.";      return false; }
            if (!string.IsNullOrEmpty(f.researchingTechId)) { reason = "Already researching."; return false; }
            if (f.ageIndex < tech.minAge)           { reason = $"Requires {Content.Age(tech.minAge).displayName}."; return false; }
            if (FactionRegistry.Instance.FirstBuildingOfRole(f.id, BuildingRole.Academy) == null)
                                                    { reason = "Requires an Academy.";     return false; }

            for (int i = 0; i < tech.requires.Count; i++)
                if (!f.HasTech(tech.requires[i]))
                {
                    reason = $"Requires {Content.Tech(tech.requires[i])?.displayName ?? tech.requires[i]}.";
                    return false;
                }

            if (!f.CanAfford(tech.cost))
            {
                var missing = f.stock.FirstShortfall(tech.cost);
                reason = $"Short on {(missing.HasValue ? ResourceBundle.Label(missing.Value) : "resources")}.";
                return false;
            }
            return true;
        }

        public bool TryResearch(Faction f, string techId)
        {
            var tech = Content.Tech(techId);
            if (!CanResearch(f, tech, out var reason))
            {
                if (f.isHuman) EventLog.Post(LogChannel.Economy, $"Cannot research — {reason}", f.id);
                return false;
            }
            f.TrySpend(tech.cost);
            f.researchingTechId = tech.id;
            f.researchProgress  = 0f;
            return true;
        }

        /// <summary>Places a building if the faction can pay for it. Returns null on failure.</summary>
        public BuildingEntity TryPlaceBuilding(Faction f, string buildingId, Vector3 position)
        {
            var def = Content.Building(buildingId);
            if (def == null) return null;

            if (f.ageIndex < def.minAge)
            {
                if (f.isHuman) EventLog.Post(LogChannel.Economy,
                    $"{def.displayName} requires {Content.Age(def.minAge).displayName}.", f.id);
                return null;
            }
            if (!f.TrySpend(def.cost))
            {
                var missing = f.stock.FirstShortfall(def.cost);
                if (f.isHuman) EventLog.Post(LogChannel.Economy,
                    $"Cannot afford {def.displayName} — short on {(missing.HasValue ? ResourceBundle.Label(missing.Value) : "resources")}.", f.id);
                return null;
            }
            return Spawner.SpawnBuilding(f.id, buildingId, position, complete: false);
        }
    }
}
