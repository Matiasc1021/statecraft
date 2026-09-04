using System.Collections.Generic;
using UnityEngine;
using Statecraft.AI;
using Statecraft.Data;
using Statecraft.Economy;
using Statecraft.Entities;
using Statecraft.Espionage;
using Statecraft.Factions;
using Statecraft.Player;

namespace Statecraft.Core
{
    /// <summary>
    /// ── MATCH BOOTSTRAP ─────────────────────────────────────────────────
    /// Drop this on ONE empty GameObject in an empty scene and press Play.
    /// It builds the camera, the lighting, the map, the factions, every system
    /// singleton and the HUD. No prefabs, no scene wiring, no NavMesh bake.
    ///
    /// That constraint is deliberate. A prototype that only runs from one blessed
    /// .scene file rots the first time someone opens it on another machine; a
    /// prototype built from code runs from a fresh clone forever, and the setup is
    /// reviewable in a diff.
    /// </summary>
    public class MatchBootstrap : MonoBehaviour
    {
        [Header("Match")]
        public string playerCivId = "meridian";
        public List<string> opponentCivIds = new List<string> { "novaya", "sable" };

        [Header("Map")]
        public float mapSize      = 240f;
        public float baseRadius   = 78f;
        public int   materialDepositsPerBase = 3;
        public int   neutralDataSites = 4;

        [Header("Start")]
        public int startingWorkers = 5;

        [Header("Opponent")]
        [Range(0f, 1f)] public float aiAggression = 0.55f;
        [Range(0f, 1f)] public float aiGreed      = 0.55f;

        void Awake()
        {
            // If a match already exists (scene reloaded twice) do not build a second one.
            if (FactionRegistry.Instance != null && FactionRegistry.Instance.Factions.Count > 0) return;

            Content.Warm();
            EconomyStats.Reset();

            BuildSystems();
            BuildEnvironment();
            BuildFactions();

            Debug.Log("[Statecraft] Match built. Left panel builds, right panel is where the game is.");
        }

        // ── Systems ───────────────────────────────────────────────────────

        void BuildSystems()
        {
            var go = new GameObject("== Statecraft Systems ==");

            go.AddComponent<SimClock>();
            go.AddComponent<EventLog>();
            go.AddComponent<FactionRegistry>();
            go.AddComponent<DiplomacySystem>();
            go.AddComponent<MarketSystem>();
            go.AddComponent<TradeRouteSystem>();
            go.AddComponent<IntelSystem>();
            go.AddComponent<EspionageSystem>();
            go.AddComponent<EconomySystem>();
            go.AddComponent<SelectionController>();
            go.AddComponent<HUD>();
        }

        // ── Environment ───────────────────────────────────────────────────

        void BuildEnvironment()
        {
            if (Object.FindAnyObjectByType<Light>() == null)
            {
                var lightGo = new GameObject("Sun");
                var light = lightGo.AddComponent<Light>();
                light.type      = LightType.Directional;
                light.color     = new Color(1f, 0.96f, 0.9f);
                light.intensity = 1.15f;
                light.shadows   = LightShadows.Soft;
                lightGo.transform.rotation = Quaternion.Euler(52f, 138f, 0f);
            }

            Spawner.SpawnGround(mapSize);

            var cam = Camera.main;
            if (cam == null)
            {
                var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = camGo.AddComponent<Camera>();
                camGo.AddComponent<AudioListener>();
            }
            cam.backgroundColor = new Color(0.05f, 0.06f, 0.08f);
            cam.farClipPlane    = 900f;

            var rig = cam.GetComponent<RTSCameraController>() ?? cam.gameObject.AddComponent<RTSCameraController>();
            rig.mapExtent = mapSize * 0.5f;

            RenderSettings.ambientLight = new Color(0.30f, 0.32f, 0.36f);
        }

        // ── Factions ──────────────────────────────────────────────────────

        void BuildFactions()
        {
            var reg = FactionRegistry.Instance;
            var civIds = new List<string> { playerCivId };
            civIds.AddRange(opponentCivIds);

            int count = civIds.Count;
            var basePositions = new List<Vector3>();

            for (int i = 0; i < count; i++)
            {
                float angle = (i / (float)count) * Mathf.PI * 2f;
                basePositions.Add(new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * baseRadius);
            }

            for (int i = 0; i < count; i++)
            {
                var civ = Content.Civ(civIds[i]) ?? Content.Civ("meridian");

                var f = new Faction {
                    id          = i,
                    displayName = civ.displayName,
                    civ         = civ,
                    colour      = civ.colour,
                    isHuman     = i == 0,
                    stock       = Content.StartingStockpile,
                    ageIndex    = 0,
                };
                reg.AddFaction(f);
                if (i == 0) reg.localFactionId = f.id;

                BuildStartingBase(f, basePositions[i]);

                if (!f.isHuman)
                {
                    var aiGo = new GameObject($"AI [{f.displayName}]");
                    var ai   = aiGo.AddComponent<AIController>();
                    ai.factionId  = f.id;
                    ai.aggression = Mathf.Clamp01(aiAggression + Random.Range(-0.15f, 0.15f));
                    ai.greed      = Mathf.Clamp01(aiGreed + Random.Range(-0.15f, 0.15f));
                }
            }

            // Contested Data sites in the middle. The mid-game map fight is over these.
            for (int i = 0; i < neutralDataSites; i++)
            {
                float angle = (i / (float)neutralDataSites) * Mathf.PI * 2f + 0.4f;
                var pos = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * (baseRadius * 0.32f);
                Spawner.SpawnNode(ResourceKind.Data, pos, 900f);
            }

            // Point the camera at the human's base.
            var cam = Camera.main;
            var rig = cam != null ? cam.GetComponent<RTSCameraController>() : null;
            if (rig != null)
            {
                cam.transform.position = basePositions[0] + new Vector3(0f, 55f, -45f);
                rig.FocusOn(basePositions[0]);
            }

            EventLog.Post(LogChannel.System,
                "Match started. Build an Exchange to trade and a Directorate to run operations.", -1);
        }

        void BuildStartingBase(Faction f, Vector3 centre)
        {
            var hq = Spawner.SpawnBuilding(f.id, "capitol", centre, complete: true);
            if (hq != null) hq.rallyPoint = centre + new Vector3(0f, 0f, -8f);

            // Material deposits ringing the base.
            var nodes = new List<ResourceNode>();
            for (int i = 0; i < materialDepositsPerBase; i++)
            {
                float angle = (i / (float)materialDepositsPerBase) * Mathf.PI * 2f + Random.Range(-0.2f, 0.2f);
                var pos = centre + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * Random.Range(17f, 26f);
                nodes.Add(Spawner.SpawnNode(ResourceKind.Materials, pos, 2200f));
            }

            // One Data site close to home so espionage is reachable without a fight.
            var dataPos = centre + new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)).normalized * 30f;
            Spawner.SpawnNode(ResourceKind.Data, dataPos, 550f);

            for (int i = 0; i < startingWorkers; i++)
            {
                float angle = (i / (float)startingWorkers) * Mathf.PI * 2f;
                var pos = centre + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 7f;
                var w = Spawner.SpawnUnit(f.id, "worker", pos) as WorkerUnit;
                if (w != null && nodes.Count > 0) w.AssignTo(nodes[i % nodes.Count]);
            }
        }
    }
}
