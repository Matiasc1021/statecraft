using System.Collections.Generic;
using UnityEngine;

namespace Statecraft.Data
{
    /// <summary>
    /// ── THE CONTENT CATALOGUE ───────────────────────────────────────────
    /// Every balance number in the game lives here, in code, so that a change
    /// shows up as a reviewable diff instead of a binary asset.
    ///
    /// Roadmap note: once the numbers stop moving weekly, port this to
    /// ScriptableObjects so designers can tune without a recompile. Until then,
    /// code is faster to iterate on and infinitely easier to diff.
    ///
    /// All rates are PER SECOND of simulation time.
    /// </summary>
    public static class Content
    {
        public static readonly Dictionary<string, UnitDef>         Units      = new Dictionary<string, UnitDef>();
        public static readonly Dictionary<string, BuildingDef>     Buildings  = new Dictionary<string, BuildingDef>();
        public static readonly Dictionary<string, TechDef>         Techs      = new Dictionary<string, TechDef>();
        public static readonly Dictionary<string, OperationDef>    Operations = new Dictionary<string, OperationDef>();
        public static readonly Dictionary<string, CivilizationDef> Civs       = new Dictionary<string, CivilizationDef>();
        public static readonly List<AgeDef>                        Ages       = new List<AgeDef>();

        /// <summary>Starting stockpile for every faction in a skirmish.</summary>
        public static ResourceBundle StartingStockpile =>
            new ResourceBundle(capital: 600f, energy: 120f, materials: 350f, components: 80f, data: 40f);

        static Content()
        {
            BuildAges();
            BuildCivs();
            BuildUnits();
            BuildBuildings();
            BuildTechs();
            BuildOperations();
        }

        /// <summary>Touch this from a bootstrap to force the static constructor to run early.</summary>
        public static void Warm() { }

        public static UnitDef      Unit(string id)      => Units.TryGetValue(id, out var v) ? v : null;
        public static BuildingDef  Building(string id)  => Buildings.TryGetValue(id, out var v) ? v : null;
        public static TechDef      Tech(string id)      => Techs.TryGetValue(id, out var v) ? v : null;
        public static OperationDef Operation(string id) => Operations.TryGetValue(id, out var v) ? v : null;
        public static CivilizationDef Civ(string id)    => Civs.TryGetValue(id, out var v) ? v : null;
        public static AgeDef Age(int index)             => Ages[Mathf.Clamp(index, 0, Ages.Count - 1)];
        public static int MaxAge                        => Ages.Count - 1;

        // ── Ages ──────────────────────────────────────────────────────────
        // Four eras, all of them modern. No stone age, no chopping trees.

        static void BuildAges()
        {
            Ages.Add(new AgeDef {
                index = 0, displayName = "Reconstruction",
                flavour = "Rebuilding capacity. Everything is scarce and nothing is automated.",
                advanceCost = new ResourceBundle(400, 0, 300, 0, 0),
                advanceSeconds = 45f, outputMultiplier = 1.00f });

            Ages.Add(new AgeDef {
                index = 1, displayName = "Industrial Consolidation",
                flavour = "Heavy manufacturing comes online. Intelligence services are founded.",
                advanceCost = new ResourceBundle(900, 0, 600, 150, 40),
                advanceSeconds = 60f, outputMultiplier = 1.15f });

            Ages.Add(new AgeDef {
                index = 2, displayName = "Information",
                flavour = "Data becomes the strategic commodity. Markets move faster than armies.",
                advanceCost = new ResourceBundle(1800, 0, 900, 400, 200),
                advanceSeconds = 75f, outputMultiplier = 1.32f });

            Ages.Add(new AgeDef {
                index = 3, displayName = "Automation",
                flavour = "Output decouples from labour. Whoever owns the models owns the century.",
                advanceCost = new ResourceBundle(3500, 0, 1600, 900, 600),
                advanceSeconds = 90f, outputMultiplier = 1.55f });
        }

        // ── Civilisations ─────────────────────────────────────────────────
        // Four blocs, each with an ECONOMIC identity and an ESPIONAGE identity.
        // Deliberately fictional: this game is about economic pressure between
        // powers, and real flags would drag in politics the design doesn't want.

        static void BuildCivs()
        {
            Add(new CivilizationDef {
                id = "meridian", displayName = "Meridian Compact",
                doctrine = "Trade bloc. Wins by owning the price, not the ground.",
                colour = new Color(0.20f, 0.62f, 0.95f),
                marketSpreadMultiplier = 0.65f,   // trades far cheaper than anyone
                tradeRouteMultiplier   = 1.25f,
                bonusTradeRouteSlots   = 2,
                manufacturingMultiplier = 1.05f,
                operationCostMultiplier = 1.15f,  // pays a premium for spycraft
                agentSkillBonus = -5f,
            });

            Add(new CivilizationDef {
                id = "novaya", displayName = "Novaya Directorate",
                doctrine = "State industry. Out-produces everyone and researches late.",
                colour = new Color(0.88f, 0.30f, 0.26f),
                extractionMultiplier    = 1.25f,
                manufacturingMultiplier = 1.20f,
                buildSpeedMultiplier    = 1.15f,
                researchSpeedMultiplier = 0.85f,
                dataMultiplier          = 0.90f,
                securityBonus           = 10f,
                marketSpreadMultiplier  = 1.15f,
            });

            Add(new CivilizationDef {
                id = "sable", displayName = "Sable Union",
                doctrine = "Intelligence state. Runs on what it takes from other people's economies.",
                colour = new Color(0.55f, 0.42f, 0.85f),
                agentSkillBonus         = 15f,
                operationCostMultiplier = 0.70f,
                coverDecayMultiplier    = 0.65f,
                securityBonus           = 20f,
                extractionMultiplier    = 0.90f,
                manufacturingMultiplier = 0.92f,
            });

            Add(new CivilizationDef {
                id = "kestrel", displayName = "Kestrel Republic",
                doctrine = "Research economy. Thin on raw output, first to every unlock.",
                colour = new Color(0.30f, 0.82f, 0.55f),
                dataMultiplier          = 1.35f,
                researchSpeedMultiplier = 1.30f,
                securityBonus           = 12f,
                extractionMultiplier    = 0.85f,
                agentSkillBonus         = 5f,
                coverDecayMultiplier    = 0.85f,
            });
        }

        // ── Units ─────────────────────────────────────────────────────────

        static void BuildUnits()
        {
            Add(new UnitDef {
                id = "worker", displayName = "Worker", role = UnitRole.Worker,
                cost = new ResourceBundle(60, 0, 20, 0, 0),
                buildSeconds = 8f, maxHp = 60f, moveSpeed = 6.5f, housingCost = 1,
                gatherPerSecond = 1.4f,
            });

            Add(new UnitDef {
                id = "agent", displayName = "Field Agent", role = UnitRole.Agent,
                cost = new ResourceBundle(250, 0, 0, 20, 30),
                buildSeconds = 22f, maxHp = 45f, moveSpeed = 7.5f, housingCost = 1, minAge = 1,
                agentSkill = 50f, agentCoverBase = 75f,
            });

            Add(new UnitDef {
                id = "security", displayName = "Security Detail", role = UnitRole.Security,
                cost = new ResourceBundle(140, 0, 0, 25, 0),
                buildSeconds = 12f, maxHp = 130f, moveSpeed = 5.5f, housingCost = 1,
                attack = 9f, attackRange = 9f, sightRange = 20f,
            });
        }

        // ── Buildings ─────────────────────────────────────────────────────

        static void BuildBuildings()
        {
            Add(new BuildingDef {
                id = "capitol", displayName = "Capitol", role = BuildingRole.Capitol,
                cost = new ResourceBundle(0, 0, 0, 0, 0),
                buildSeconds = 40f, maxHp = 1600f, footprint = new Vector2(8, 8),
                housingProvided = 8, securityBonus = 10f, tradeRouteSlots = 1,
                trains = new List<string> { "worker" },
            });

            Add(new BuildingDef {
                id = "habitat", displayName = "Habitat Block", role = BuildingRole.Habitat,
                cost = new ResourceBundle(80, 0, 90, 0, 0),
                buildSeconds = 18f, maxHp = 500f, footprint = new Vector2(5, 5),
                housingProvided = 8,
            });

            Add(new BuildingDef {
                id = "power", displayName = "Power Plant", role = BuildingRole.Power,
                cost = new ResourceBundle(150, 0, 180, 0, 0),
                buildSeconds = 26f, maxHp = 700f, footprint = new Vector2(6, 6),
                recipe = new ConversionRecipe {
                    inputPerSecond  = new ResourceBundle(0, 0, 0.7f, 0, 0),
                    outputPerSecond = new ResourceBundle(0, 3.2f, 0, 0, 0),
                    labourSlots = 2,
                },
            });

            Add(new BuildingDef {
                id = "extractor", displayName = "Extraction Site", role = BuildingRole.Extractor,
                cost = new ResourceBundle(120, 0, 100, 0, 0),
                buildSeconds = 20f, maxHp = 550f, footprint = new Vector2(5, 5),
                recipe = new ConversionRecipe {
                    inputPerSecond  = new ResourceBundle(0, 0.5f, 0, 0, 0),
                    outputPerSecond = new ResourceBundle(0, 0, 1.6f, 0, 0),
                    labourSlots = 3,
                },
            });

            Add(new BuildingDef {
                id = "fabricator", displayName = "Fabricator", role = BuildingRole.Fabricator,
                cost = new ResourceBundle(220, 0, 200, 0, 0),
                buildSeconds = 30f, maxHp = 800f, footprint = new Vector2(7, 6), minAge = 1,
                recipe = new ConversionRecipe {
                    inputPerSecond  = new ResourceBundle(0, 1.1f, 2.0f, 0, 0),
                    outputPerSecond = new ResourceBundle(0, 0, 0, 1.0f, 0),
                    labourSlots = 4,
                },
            });

            Add(new BuildingDef {
                id = "datacenter", displayName = "Data Centre", role = BuildingRole.DataCenter,
                cost = new ResourceBundle(400, 0, 150, 120, 0),
                buildSeconds = 34f, maxHp = 650f, footprint = new Vector2(6, 6), minAge = 2,
                recipe = new ConversionRecipe {
                    inputPerSecond  = new ResourceBundle(0, 1.8f, 0, 0.25f, 0),
                    outputPerSecond = new ResourceBundle(0, 0, 0, 0, 0.85f),
                    labourSlots = 3,
                },
            });

            Add(new BuildingDef {
                id = "exchange", displayName = "Exchange", role = BuildingRole.Exchange,
                cost = new ResourceBundle(300, 0, 120, 60, 0),
                buildSeconds = 28f, maxHp = 600f, footprint = new Vector2(6, 6), minAge = 1,
                tradeRouteSlots = 2,
                recipe = new ConversionRecipe {
                    inputPerSecond  = new ResourceBundle(0, 0.4f, 0, 0, 0),
                    outputPerSecond = new ResourceBundle(0.6f, 0, 0, 0, 0),   // clearing fees
                    labourSlots = 2,
                },
            });

            Add(new BuildingDef {
                id = "directorate", displayName = "Directorate", role = BuildingRole.Directorate,
                cost = new ResourceBundle(350, 0, 140, 90, 20),
                buildSeconds = 32f, maxHp = 700f, footprint = new Vector2(6, 6), minAge = 1,
                securityBonus = 25f,
                trains = new List<string> { "agent", "security" },
                recipe = new ConversionRecipe {
                    inputPerSecond  = new ResourceBundle(0, 0.6f, 0, 0, 0),
                    outputPerSecond = new ResourceBundle(0, 0, 0, 0, 0.15f),  // signals intelligence
                    labourSlots = 2,
                },
            });

            Add(new BuildingDef {
                id = "academy", displayName = "Academy", role = BuildingRole.Academy,
                cost = new ResourceBundle(280, 0, 160, 40, 0),
                buildSeconds = 30f, maxHp = 600f, footprint = new Vector2(6, 6),
                recipe = new ConversionRecipe {
                    inputPerSecond  = new ResourceBundle(0, 0.7f, 0, 0, 0),
                    outputPerSecond = new ResourceBundle(0, 0, 0, 0, 0.30f),
                    labourSlots = 3,
                },
            });
        }

        // ── Technologies ──────────────────────────────────────────────────

        static void BuildTechs()
        {
            Add(new TechDef { id = "logistics", displayName = "Logistics Corps", minAge = 0,
                description = "+15% extraction and gathering.",
                cost = new ResourceBundle(200, 0, 120, 0, 10), researchSeconds = 30f });

            Add(new TechDef { id = "grid_efficiency", displayName = "Grid Efficiency", minAge = 0,
                description = "-15% energy input on every recipe.",
                cost = new ResourceBundle(250, 0, 100, 0, 15), researchSeconds = 35f });

            Add(new TechDef { id = "assembly_line", displayName = "Assembly Line", minAge = 1,
                description = "+20% component output.",
                cost = new ResourceBundle(400, 0, 250, 40, 30), researchSeconds = 45f,
                requires = new List<string> { "logistics" } });

            Add(new TechDef { id = "field_craft", displayName = "Field Craft", minAge = 1,
                description = "+10 agent skill, agents burn cover 15% slower.",
                cost = new ResourceBundle(350, 0, 0, 60, 60), researchSeconds = 40f });

            Add(new TechDef { id = "market_desk", displayName = "Market Desk", minAge = 2,
                description = "-25% market spread.",
                cost = new ResourceBundle(600, 0, 0, 120, 90), researchSeconds = 50f });

            Add(new TechDef { id = "encryption", displayName = "Encryption Standard", minAge = 2,
                description = "+20 security, enemy intel on you decays twice as fast.",
                cost = new ResourceBundle(550, 0, 0, 100, 120), researchSeconds = 50f,
                requires = new List<string> { "field_craft" } });

            Add(new TechDef { id = "autonomy", displayName = "Autonomous Plant", minAge = 3,
                description = "+25% output on every conversion, -30% labour requirement.",
                cost = new ResourceBundle(1200, 0, 400, 300, 300), researchSeconds = 70f,
                requires = new List<string> { "assembly_line" } });

            Add(new TechDef { id = "deep_cover", displayName = "Deep Cover Network", minAge = 3,
                description = "-35% cover decay, -20% detection chance.",
                cost = new ResourceBundle(1000, 0, 0, 200, 350), researchSeconds = 65f,
                requires = new List<string> { "encryption" } });
        }

        // ── Operations ────────────────────────────────────────────────────
        // The core loop of the game. Every op is a bet: a chance to move the
        // opponent's economy, against a chance to be caught doing it.

        static void BuildOperations()
        {
            Add(new OperationDef {
                id = "steal_data", displayName = "Exfiltrate Data", kind = OperationKind.StealData,
                description = "Transfer a slice of the target's Data stockpile into yours.",
                cost = new ResourceBundle(150, 0, 0, 0, 15),
                durationSeconds = 20f, baseSuccess = 0.62f, baseDetection = 0.28f, coverCost = 18f, minAge = 1 });

            Add(new OperationDef {
                id = "steal_tech", displayName = "Industrial Espionage", kind = OperationKind.StealTech,
                description = "Copy one technology the target has researched and you have not.",
                cost = new ResourceBundle(450, 0, 0, 40, 60),
                durationSeconds = 38f, baseSuccess = 0.42f, baseDetection = 0.46f, coverCost = 32f, minAge = 2 });

            Add(new OperationDef {
                id = "sabotage_production", displayName = "Sabotage Plant", kind = OperationKind.SabotageProduction,
                description = "Halt one of the target's production buildings for 60 seconds.",
                cost = new ResourceBundle(260, 0, 0, 30, 0),
                durationSeconds = 26f, baseSuccess = 0.56f, baseDetection = 0.42f, coverCost = 26f, minAge = 1 });

            Add(new OperationDef {
                id = "sabotage_trade", displayName = "Cut Trade Route", kind = OperationKind.SabotageTrade,
                description = "Drop one of the target's trade routes to 25% throughput for 90 seconds.",
                cost = new ResourceBundle(210, 0, 0, 0, 10),
                durationSeconds = 22f, baseSuccess = 0.60f, baseDetection = 0.36f, coverCost = 20f, minAge = 1 });

            Add(new OperationDef {
                id = "market_manipulation", displayName = "Move the Market", kind = OperationKind.MarketManipulation,
                description = "Force the price of one commodity 25% against the target's net position.",
                cost = new ResourceBundle(520, 0, 0, 0, 40),
                durationSeconds = 30f, baseSuccess = 0.50f, baseDetection = 0.30f, coverCost = 22f, minAge = 2 });

            Add(new OperationDef {
                id = "insider_trading", displayName = "Insider Position", kind = OperationKind.InsiderTrading,
                description = "Front-run the target's next large trade. Pays Capital scaled to their stockpile.",
                cost = new ResourceBundle(130, 0, 0, 0, 30),
                durationSeconds = 25f, baseSuccess = 0.66f, baseDetection = 0.20f, coverCost = 15f, minAge = 1 });

            Add(new OperationDef {
                id = "disinformation", displayName = "Plant Disinformation", kind = OperationKind.Disinformation,
                description = "Corrupt the target's intelligence on you. Their estimates go badly wrong for 2 minutes.",
                cost = new ResourceBundle(190, 0, 0, 0, 40),
                durationSeconds = 20f, baseSuccess = 0.70f, baseDetection = 0.24f, coverCost = 12f, minAge = 1 });

            Add(new OperationDef {
                id = "counter_sweep", displayName = "Counter-Intelligence Sweep", kind = OperationKind.CounterSweep,
                description = "Defensive. Raises your security for 3 minutes and may burn a hostile agent.",
                cost = new ResourceBundle(320, 0, 0, 0, 20),
                durationSeconds = 26f, baseSuccess = 0.78f, baseDetection = 0f, coverCost = 0f,
                minAge = 1, selfTargeted = true });
        }

        // ── Registration helpers ──────────────────────────────────────────

        static void Add(UnitDef d)         => Units[d.id]      = d;
        static void Add(BuildingDef d)     => Buildings[d.id]  = d;
        static void Add(TechDef d)         => Techs[d.id]      = d;
        static void Add(OperationDef d)    => Operations[d.id] = d;
        static void Add(CivilizationDef d) => Civs[d.id]       = d;
    }
}
