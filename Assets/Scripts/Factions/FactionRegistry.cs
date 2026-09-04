using System.Collections.Generic;
using UnityEngine;
using Statecraft.Data;
using Statecraft.Entities;

namespace Statecraft.Factions
{
    /// <summary>
    /// The single index of who exists and what they own.
    ///
    /// Every system asks this instead of calling FindObjectsByType. An RTS runs the
    /// same queries ("my buildings", "nearest free deposit") tens of times per tick,
    /// and a scan-the-scene approach is the classic reason these games start fine and
    /// die at 300 units.
    /// </summary>
    public class FactionRegistry : MonoBehaviour
    {
        public static FactionRegistry Instance { get; private set; }

        readonly List<Faction> _factions = new List<Faction>();
        public IReadOnlyList<Faction> Factions => _factions;

        readonly Dictionary<int, List<BuildingEntity>> _buildings = new Dictionary<int, List<BuildingEntity>>();
        readonly Dictionary<int, List<UnitEntity>>     _units     = new Dictionary<int, List<UnitEntity>>();
        readonly List<ResourceNode> _nodes = new List<ResourceNode>();

        /// <summary>The faction the human is playing.</summary>
        public int localFactionId;

        public Faction Local => Get(localFactionId);

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        // ── Factions ──────────────────────────────────────────────────────

        public Faction AddFaction(Faction f)
        {
            _factions.Add(f);
            if (!_buildings.ContainsKey(f.id)) _buildings[f.id] = new List<BuildingEntity>();
            if (!_units.ContainsKey(f.id))     _units[f.id]     = new List<UnitEntity>();
            return f;
        }

        public Faction Get(int id)
        {
            for (int i = 0; i < _factions.Count; i++)
                if (_factions[i].id == id) return _factions[i];
            return null;
        }

        public IEnumerable<Faction> Others(int id)
        {
            for (int i = 0; i < _factions.Count; i++)
                if (_factions[i].id != id) yield return _factions[i];
        }

        // ── Entity registration ───────────────────────────────────────────

        public void Register(Entity e)
        {
            if (e is BuildingEntity b)
            {
                var list = ListFor(_buildings, b.factionId);
                if (!list.Contains(b)) list.Add(b);
            }
            else if (e is UnitEntity u)
            {
                var list = ListFor(_units, u.factionId);
                if (!list.Contains(u)) list.Add(u);
            }
        }

        public void Unregister(Entity e)
        {
            if (e is BuildingEntity b) ListFor(_buildings, b.factionId).Remove(b);
            else if (e is UnitEntity u) ListFor(_units, u.factionId).Remove(u);
        }

        public void RegisterNode(ResourceNode n)   { if (!_nodes.Contains(n)) _nodes.Add(n); }
        public void UnregisterNode(ResourceNode n) { _nodes.Remove(n); }

        static List<T> ListFor<T>(Dictionary<int, List<T>> dict, int key)
        {
            if (!dict.TryGetValue(key, out var list)) { list = new List<T>(); dict[key] = list; }
            return list;
        }

        // ── Queries ───────────────────────────────────────────────────────

        public List<BuildingEntity> BuildingsOf(int factionId) => ListFor(_buildings, factionId);
        public List<UnitEntity>     UnitsOf(int factionId)     => ListFor(_units, factionId);
        public IReadOnlyList<ResourceNode> Nodes => _nodes;

        public List<AgentUnit> AgentsOf(int factionId)
        {
            var result = new List<AgentUnit>();
            var units = UnitsOf(factionId);
            for (int i = 0; i < units.Count; i++)
                if (units[i] is AgentUnit a && a.IsAlive) result.Add(a);
            return result;
        }

        public List<WorkerUnit> WorkersOf(int factionId)
        {
            var result = new List<WorkerUnit>();
            var units = UnitsOf(factionId);
            for (int i = 0; i < units.Count; i++)
                if (units[i] is WorkerUnit w && w.IsAlive) result.Add(w);
            return result;
        }

        public BuildingEntity FirstBuildingOfRole(int factionId, BuildingRole role, bool operationalOnly = true)
        {
            var list = BuildingsOf(factionId);
            for (int i = 0; i < list.Count; i++)
            {
                var b = list[i];
                if (b == null || b.def == null || b.def.role != role) continue;
                if (operationalOnly && !b.IsOperational) continue;
                return b;
            }
            return null;
        }

        public bool HasBuilding(int factionId, BuildingRole role) => FirstBuildingOfRole(factionId, role) != null;

        public BuildingEntity NearestDropOff(int factionId, Vector3 from)
        {
            BuildingEntity best = null;
            float bestSqr = float.MaxValue;
            var list = BuildingsOf(factionId);
            for (int i = 0; i < list.Count; i++)
            {
                var b = list[i];
                if (b == null || !b.IsDropOff || b.underConstruction || !b.IsAlive) continue;
                float d = (b.transform.position - from).sqrMagnitude;
                if (d < bestSqr) { bestSqr = d; best = b; }
            }
            return best;
        }

        public ResourceNode NearestFreeNode(Vector3 from, ResourceKind? kind)
        {
            ResourceNode best = null;
            float bestSqr = float.MaxValue;
            for (int i = 0; i < _nodes.Count; i++)
            {
                var n = _nodes[i];
                if (n == null || n.Depleted || !n.HasFreeSlot) continue;
                if (kind.HasValue && n.kind != kind.Value) continue;
                float d = (n.transform.position - from).sqrMagnitude;
                if (d < bestSqr) { bestSqr = d; best = n; }
            }
            return best;
        }

        /// <summary>Any live production building of a faction, for sabotage targeting.</summary>
        public BuildingEntity RandomProductionBuilding(int factionId)
        {
            var candidates = new List<BuildingEntity>();
            var list = BuildingsOf(factionId);
            for (int i = 0; i < list.Count; i++)
                if (list[i] != null && list[i].HasRecipe && list[i].IsOperational) candidates.Add(list[i]);
            if (candidates.Count == 0) return null;
            return candidates[Random.Range(0, candidates.Count)];
        }
    }
}
