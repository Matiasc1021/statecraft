using System.Collections.Generic;
using UnityEngine;
using Statecraft.Data;

namespace Statecraft.Factions
{
    /// <summary>
    /// Everything a player (human or AI) owns that is not a GameObject.
    ///
    /// Design rule: this class holds STATE and derived multipliers. It never
    /// mutates the world. Systems (EconomySystem, EspionageSystem, ...) read and
    /// write it on tick. That keeps the sim inspectable — you can dump a Faction
    /// and know exactly why the numbers are what they are.
    /// </summary>
    public class Faction
    {
        public int    id;
        public string displayName;
        public bool   isHuman;
        public CivilizationDef civ;
        public Color  colour = Color.white;

        // ── Stockpile & flow ──────────────────────────────────────────────
        public ResourceBundle stock;
        /// <summary>Net change per second, recomputed every tick. This is what the HUD shows as +/-.</summary>
        public ResourceBundle netPerSecond;
        /// <summary>Cumulative interest paid on negative Capital. A civ that lives on debt pays for it.</summary>
        public float interestPaidTotal;

        // ── Age & research ────────────────────────────────────────────────
        public int   ageIndex;
        public bool  advancingAge;
        public float ageProgress;               // 0..1
        public readonly HashSet<string> techs = new HashSet<string>();
        public string researchingTechId;
        public float  researchProgress;         // 0..1

        // ── Population & labour ───────────────────────────────────────────
        public int housingCapacity;             // recomputed each tick from buildings
        public int population;                  // units alive
        public int labourSupply;                // workers alive
        public int labourDemand;                // sum of running buildings' labourSlots

        // ── Espionage ─────────────────────────────────────────────────────
        public float buildingSecurity;          // recomputed each tick from buildings
        public float securityBoost;             // from CounterSweep
        public float securityBoostSeconds;
        public int   opsRunAgainstMe;
        public int   opsCaught;

        // ── Derived: labour efficiency ────────────────────────────────────
        /// <summary>
        /// If you have fewer workers than your factories need, EVERYTHING runs slower.
        /// This is the pressure that makes housing and worker count a real decision
        /// instead of an afterthought.
        /// </summary>
        public float LabourEfficiency
        {
            get
            {
                float demand = labourDemand * LabourRequirementMultiplier;
                if (demand <= 0.01f) return 1f;
                return Mathf.Clamp01(labourSupply / demand);
            }
        }

        // ── Derived multipliers ───────────────────────────────────────────
        // Every one of these is civ bonus x tech bonus x age bonus. No exceptions,
        // no special cases — so a player can reason about their own economy.

        public AgeDef Age => Content.Age(ageIndex);

        public bool HasTech(string id) => techs.Contains(id);

        public float ExtractionMultiplier =>
            (civ?.extractionMultiplier ?? 1f)
            * (HasTech("logistics") ? 1.15f : 1f)
            * (HasTech("autonomy")  ? 1.25f : 1f)
            * Age.outputMultiplier;

        public float ManufacturingMultiplier =>
            (civ?.manufacturingMultiplier ?? 1f)
            * (HasTech("assembly_line") ? 1.20f : 1f)
            * (HasTech("autonomy")      ? 1.25f : 1f)
            * Age.outputMultiplier;

        public float DataMultiplier =>
            (civ?.dataMultiplier ?? 1f)
            * (HasTech("autonomy") ? 1.25f : 1f)
            * Age.outputMultiplier;

        /// <summary>Applied to the Energy component of every recipe INPUT.</summary>
        public float EnergyInputMultiplier => HasTech("grid_efficiency") ? 0.85f : 1f;

        public float LabourRequirementMultiplier => HasTech("autonomy") ? 0.70f : 1f;

        public float BuildSpeedMultiplier => (civ?.buildSpeedMultiplier ?? 1f);

        public float ResearchSpeedMultiplier => (civ?.researchSpeedMultiplier ?? 1f);

        /// <summary>Fraction taken off both sides of every market trade. Lower is better.</summary>
        public float MarketSpread =>
            0.06f * (civ?.marketSpreadMultiplier ?? 1f) * (HasTech("market_desk") ? 0.75f : 1f)
            + (DiplomacySystem.Instance != null ? DiplomacySystem.Instance.EmbargoPenaltyFor(id) : 0f);

        public float TradeRouteMultiplier => (civ?.tradeRouteMultiplier ?? 1f);

        /// <summary>0-100ish. Raises the chance a hostile op fails and is detected.</summary>
        public float Security =>
            20f
            + buildingSecurity
            + (civ?.securityBonus ?? 0f)
            + (HasTech("encryption") ? 20f : 0f)
            + securityBoost;

        public float AgentSkillBonus =>
            (civ?.agentSkillBonus ?? 0f) + (HasTech("field_craft") ? 10f : 0f);

        /// <summary>Multiplier on how fast agents burn cover. Lower is better.</summary>
        public float CoverDecayMultiplier =>
            (civ?.coverDecayMultiplier ?? 1f)
            * (HasTech("field_craft") ? 0.85f : 1f)
            * (HasTech("deep_cover")  ? 0.65f : 1f);

        public float OperationCostMultiplier => (civ?.operationCostMultiplier ?? 1f);

        /// <summary>Multiplier on the chance an op of ours is detected. Lower is better.</summary>
        public float DetectionMultiplier => HasTech("deep_cover") ? 0.80f : 1f;

        /// <summary>How fast an enemy's intel about US goes stale. Higher is better for us.</summary>
        public float IntelDecayMultiplier => HasTech("encryption") ? 2.0f : 1f;

        // ── Spending ──────────────────────────────────────────────────────

        public bool CanAfford(ResourceBundle cost) => stock.Covers(cost);

        public bool TrySpend(ResourceBundle cost)
        {
            if (!stock.Covers(cost)) return false;
            stock = stock - cost;
            return true;
        }

        /// <summary>Spend Capital even into the red. Only the market and interest use this.</summary>
        public void SpendCapitalOnCredit(float amount) => stock.capital -= amount;

        public void Grant(ResourceBundle gain) => stock = stock + gain;

        public void Grant(ResourceKind kind, float amount) => stock.Add(kind, amount);

        /// <summary>Takes up to <paramref name="amount"/>, returns what was actually available.</summary>
        public float Drain(ResourceKind kind, float amount)
        {
            float have  = stock.Get(kind);
            float taken = Mathf.Min(have, amount);
            stock.Set(kind, have - taken);
            return taken;
        }

        public int PopulationHeadroom => housingCapacity - population;

        public override string ToString() => $"{displayName} ({civ?.displayName ?? "no civ"})";
    }
}
