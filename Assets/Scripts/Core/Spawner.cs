using UnityEngine;
using Statecraft.Data;
using Statecraft.Entities;
using Statecraft.Factions;

namespace Statecraft.Core
{
    /// <summary>
    /// Builds map objects from primitives.
    ///
    /// There is deliberately no prefab dependency anywhere in the prototype: the whole
    /// match is constructed from code, so the repo clones and runs with no asset
    /// wiring, no missing references, and no "works on my machine" scene file. When
    /// real art arrives, give each def a prefab field and swap the bodies out here —
    /// nothing else in the codebase knows what a unit looks like.
    /// </summary>
    public static class Spawner
    {
        public static Transform Root
        {
            get
            {
                var go = GameObject.Find("== Statecraft World ==");
                if (go == null) go = new GameObject("== Statecraft World ==");
                return go.transform;
            }
        }

        static Transform Group(string name)
        {
            var root = Root;
            var t = root.Find(name);
            if (t == null)
            {
                var go = new GameObject(name);
                go.transform.SetParent(root, false);
                t = go.transform;
            }
            return t;
        }

        // ── Units ─────────────────────────────────────────────────────────

        public static UnitEntity SpawnUnit(int factionId, string unitId, Vector3 position)
        {
            var def = Content.Unit(unitId);
            if (def == null) { Debug.LogError($"[Spawner] Unknown unit '{unitId}'"); return null; }

            var faction = FactionRegistry.Instance?.Get(factionId);
            var colour  = faction?.colour ?? Color.white;

            GameObject body;
            UnitEntity unit;

            switch (def.role)
            {
                case UnitRole.Worker:
                    body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    body.transform.localScale = new Vector3(0.7f, 0.75f, 0.7f);
                    unit = body.AddComponent<WorkerUnit>();
                    break;

                case UnitRole.Agent:
                    body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    body.transform.localScale = new Vector3(0.6f, 0.8f, 0.6f);
                    unit = body.AddComponent<AgentUnit>();
                    break;

                default:
                    body = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    body.transform.localScale = new Vector3(0.9f, 1.6f, 0.9f);
                    unit = body.AddComponent<UnitEntity>();
                    break;
            }

            body.name = $"{def.displayName} [F{factionId}]";
            body.transform.SetParent(Group("Units"), false);
            body.transform.position = new Vector3(position.x, 0.8f, position.z);

            var col = body.GetComponent<Collider>();
            if (col != null) col.isTrigger = true;

            unit.Configure(def, factionId);
            unit.ApplyFactionColour(def.role == UnitRole.Agent ? Color.Lerp(colour, Color.black, 0.45f) : colour);

            // A small marker on top so agents read differently at a glance.
            if (def.role == UnitRole.Agent)
            {
                var pip = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Object.Destroy(pip.GetComponent<Collider>());
                pip.name = "AgentPip";
                pip.transform.SetParent(body.transform, false);
                pip.transform.localPosition = new Vector3(0f, 0.85f, 0f);
                pip.transform.localScale    = Vector3.one * 0.45f;
                MaterialFactory.Paint(pip, Color.Lerp(colour, Color.white, 0.6f), unlit: true);
            }

            return unit;
        }

        // ── Buildings ─────────────────────────────────────────────────────

        public static BuildingEntity SpawnBuilding(int factionId, string buildingId, Vector3 position, bool complete)
        {
            var def = Content.Building(buildingId);
            if (def == null) { Debug.LogError($"[Spawner] Unknown building '{buildingId}'"); return null; }

            var faction = FactionRegistry.Instance?.Get(factionId);
            var colour  = faction?.colour ?? Color.white;

            float height = HeightFor(def.role);
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = $"{def.displayName} [F{factionId}]";
            body.transform.SetParent(Group("Buildings"), false);
            body.transform.localScale = new Vector3(def.footprint.x, height, def.footprint.y);
            body.transform.position   = new Vector3(position.x, height * 0.5f, position.z);

            var b = body.AddComponent<BuildingEntity>();
            b.Configure(def, factionId, complete);
            b.ApplyFactionColour(Color.Lerp(colour, RoleTint(def.role), 0.55f));

            // Role badge — a coloured slab on the roof, so you can read a base at a glance.
            var badge = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.Destroy(badge.GetComponent<Collider>());
            badge.name = "RoleBadge";
            badge.transform.SetParent(body.transform, false);
            badge.transform.localScale    = new Vector3(0.5f, 0.12f, 0.5f);
            badge.transform.localPosition = new Vector3(0f, 0.56f, 0f);
            MaterialFactory.Paint(badge, RoleTint(def.role), unlit: true);

            return b;
        }

        static float HeightFor(BuildingRole role)
        {
            switch (role)
            {
                case BuildingRole.Capitol:     return 7f;
                case BuildingRole.Power:       return 6f;
                case BuildingRole.Fabricator:  return 5f;
                case BuildingRole.DataCenter:  return 4.5f;
                case BuildingRole.Directorate: return 5.5f;
                case BuildingRole.Exchange:    return 5f;
                case BuildingRole.Academy:     return 4.5f;
                case BuildingRole.Habitat:     return 4f;
                default:                       return 3.5f;
            }
        }

        public static Color RoleTint(BuildingRole role)
        {
            switch (role)
            {
                case BuildingRole.Capitol:     return new Color(1.00f, 0.95f, 0.80f);
                case BuildingRole.Habitat:     return new Color(0.80f, 0.80f, 0.85f);
                case BuildingRole.Power:       return new Color(0.40f, 0.80f, 1.00f);
                case BuildingRole.Extractor:   return new Color(0.78f, 0.55f, 0.38f);
                case BuildingRole.Fabricator:  return new Color(0.70f, 0.75f, 0.82f);
                case BuildingRole.DataCenter:  return new Color(0.55f, 0.95f, 0.60f);
                case BuildingRole.Exchange:    return new Color(0.95f, 0.82f, 0.35f);
                case BuildingRole.Directorate: return new Color(0.72f, 0.55f, 0.95f);
                case BuildingRole.Academy:     return new Color(0.95f, 0.60f, 0.75f);
                default:                       return Color.white;
            }
        }

        // ── Resource nodes ────────────────────────────────────────────────

        public static ResourceNode SpawnNode(ResourceKind kind, Vector3 position, float amount)
        {
            var body = GameObject.CreatePrimitive(kind == ResourceKind.Data ? PrimitiveType.Cylinder : PrimitiveType.Cube);
            body.name = $"{ResourceBundle.Label(kind)} Deposit";
            body.transform.SetParent(Group("Deposits"), false);
            body.transform.localScale = kind == ResourceKind.Data
                ? new Vector3(2.2f, 1.2f, 2.2f)
                : new Vector3(3.2f, 1.6f, 3.2f);
            body.transform.position = new Vector3(position.x, 0.8f, position.z);
            MaterialFactory.Paint(body, ResourceBundle.TintOf(kind));

            var n = body.AddComponent<ResourceNode>();
            n.kind = kind;
            n.amountRemaining = amount;
            n.initialAmount   = amount;
            n.gatherSlots     = kind == ResourceKind.Data ? 3 : 5;
            FactionRegistry.Instance?.RegisterNode(n);
            return n;
        }

        // ── Terrain ───────────────────────────────────────────────────────

        public static GameObject SpawnGround(float size)
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.SetParent(Root, false);
            ground.transform.localScale = new Vector3(size / 10f, 1f, size / 10f);
            ground.transform.position   = Vector3.zero;
            MaterialFactory.Paint(ground, new Color(0.19f, 0.21f, 0.20f));
            return ground;
        }
    }
}
