using System;
using System.Collections.Generic;
using UnityEngine;

namespace Statecraft.Data
{
    public enum UnitRole    { Worker, Agent, Security }
    public enum BuildingRole { Capitol, Habitat, Power, Extractor, Fabricator, DataCenter, Exchange, Directorate, Academy }

    /// <summary>What a building turns into what, per simulation tick, while it has labour and power.</summary>
    [Serializable]
    public class ConversionRecipe
    {
        public ResourceBundle inputPerSecond;
        public ResourceBundle outputPerSecond;
        /// <summary>Workers that must be assigned for the building to run at full rate.</summary>
        public int labourSlots;
    }

    [Serializable]
    public class UnitDef
    {
        public string     id;
        public string     displayName;
        public UnitRole   role;
        public ResourceBundle cost;
        public float      buildSeconds = 8f;
        public float      maxHp        = 60f;
        public float      moveSpeed    = 6f;
        public int        housingCost  = 1;
        public int        minAge;

        // Worker
        public float gatherPerSecond = 1.2f;

        // Agent
        public float agentSkill     = 50f;
        public float agentCoverBase = 70f;

        // Security
        public float attack      = 6f;
        public float attackRange = 8f;
        public float sightRange  = 18f;
    }

    [Serializable]
    public class BuildingDef
    {
        public string       id;
        public string       displayName;
        public BuildingRole role;
        public ResourceBundle cost;
        public float        buildSeconds = 20f;
        public float        maxHp        = 400f;
        public Vector2      footprint    = new Vector2(4f, 4f);
        public int          minAge;

        /// <summary>Population capacity this building adds.</summary>
        public int housingProvided;
        /// <summary>Flat security contribution for the owning faction.</summary>
        public float securityBonus;
        /// <summary>Extra trade route slots.</summary>
        public int tradeRouteSlots;

        public ConversionRecipe recipe;
        public List<string> trains = new List<string>();

        public bool CanTrain(string unitId) => trains != null && trains.Contains(unitId);
    }

    [Serializable]
    public class TechDef
    {
        public string id;
        public string displayName;
        public string description;
        public ResourceBundle cost;
        public float researchSeconds = 30f;
        public int   minAge;
        public List<string> requires = new List<string>();
    }

    [Serializable]
    public class AgeDef
    {
        public int    index;
        public string displayName;
        public string flavour;
        public ResourceBundle advanceCost;
        public float advanceSeconds = 45f;
        /// <summary>Multiplier applied to every conversion recipe's output in this age.</summary>
        public float outputMultiplier = 1f;
    }

    /// <summary>What a civilisation is good at. Every bonus is a multiplier or a flat delta — no hidden rules.</summary>
    [Serializable]
    public class CivilizationDef
    {
        public string id;
        public string displayName;
        public string doctrine;
        public Color  colour = Color.white;

        [Header("Economy")]
        public float extractionMultiplier   = 1f;
        public float manufacturingMultiplier = 1f;
        public float dataMultiplier         = 1f;
        public float marketSpreadMultiplier = 1f;   // < 1 means cheaper trading
        public float tradeRouteMultiplier   = 1f;
        public int   bonusTradeRouteSlots;

        [Header("Espionage")]
        public float agentSkillBonus     = 0f;
        public float operationCostMultiplier = 1f;
        public float securityBonus       = 0f;      // defensive
        public float coverDecayMultiplier = 1f;     // < 1 means agents stay hidden longer

        [Header("Tempo")]
        public float researchSpeedMultiplier = 1f;
        public float buildSpeedMultiplier    = 1f;
    }

    public enum OperationKind
    {
        StealData,
        StealTech,
        SabotageProduction,
        SabotageTrade,
        MarketManipulation,
        InsiderTrading,
        Disinformation,
        CounterSweep,
    }

    [Serializable]
    public class OperationDef
    {
        public string        id;
        public string        displayName;
        public string        description;
        public OperationKind kind;
        public ResourceBundle cost;
        public float  durationSeconds = 20f;
        public float  baseSuccess     = 0.55f;   // before skill / security
        public float  baseDetection   = 0.25f;   // before cover / security
        public float  coverCost       = 18f;     // cover burned by running it
        public int    minAge;
        /// <summary>Defensive ops target your own faction.</summary>
        public bool   selfTargeted;
    }
}
