using UnityEngine;
using Statecraft.Data;
using Statecraft.Economy;
using Statecraft.Factions;

namespace Statecraft.Entities
{
    /// <summary>
    /// The Age of Empires loop, kept intact: walk to a deposit, gather, carry it back
    /// to a drop-off, repeat. Everything downstream of the stockpile is where this
    /// game diverges — but the moment-to-moment economy has to feel like an RTS, and
    /// this is the loop that makes it feel like one.
    ///
    /// Workers ALSO count as labour supply (see Faction.LabourEfficiency): building a
    /// factory you cannot staff is a real mistake, not a free win.
    /// </summary>
    public class WorkerUnit : UnitEntity
    {
        public enum WorkState { Idle, ToNode, Gathering, ToDropOff }

        [Header("Gathering")]
        public float carryCapacity  = 12f;
        public float gatherPerSecond = 1.4f;

        public WorkState state = WorkState.Idle;
        public ResourceNode assignedNode;
        public float carried;
        public ResourceKind carriedKind = ResourceKind.Materials;

        BuildingEntity _dropOff;
        float _gatherAccum;

        public override string DisplayName => "Worker";
        public override string StatusLine
        {
            get
            {
                switch (state)
                {
                    case WorkState.Gathering: return $"Gathering {ResourceBundle.Label(carriedKind)} ({carried:0}/{carryCapacity:0})";
                    case WorkState.ToNode:    return "Walking to deposit";
                    case WorkState.ToDropOff: return $"Hauling {carried:0} {ResourceBundle.Label(carriedKind)}";
                    default:                  return "Idle";
                }
            }
        }

        public override void Configure(UnitDef d, int owningFactionId)
        {
            base.Configure(d, owningFactionId);
            gatherPerSecond = d.gatherPerSecond;
        }

        // ── Orders ────────────────────────────────────────────────────────

        public void AssignTo(ResourceNode node)
        {
            if (node == null || node.Depleted) return;
            ReleaseNode();
            assignedNode = node;
            node.Claim();
            carriedKind = node.kind;
            state = carried >= carryCapacity ? WorkState.ToDropOff : WorkState.ToNode;
            if (state == WorkState.ToNode) MoveTo(node.transform.position);
            else HeadToDropOff();
        }

        public override void MoveTo(Vector3 worldPos)
        {
            base.MoveTo(worldPos);
        }

        /// <summary>A plain move order cancels the gathering job.</summary>
        public void ManualMove(Vector3 worldPos)
        {
            ReleaseNode();
            state = WorkState.Idle;
            MoveTo(worldPos);
        }

        void ReleaseNode()
        {
            if (assignedNode != null) assignedNode.Release();
            assignedNode = null;
        }

        protected override void Die(int killerFactionId)
        {
            ReleaseNode();
            base.Die(killerFactionId);
        }

        // ── Loop ──────────────────────────────────────────────────────────

        protected override void Update()
        {
            base.Update();
            float dt = Time.deltaTime;

            switch (state)
            {
                case WorkState.Gathering:
                    TickGather(dt);
                    break;

                case WorkState.ToNode:
                case WorkState.ToDropOff:
                case WorkState.Idle:
                    break;
            }
        }

        protected override void OnArrived()
        {
            if (state == WorkState.ToNode)
            {
                if (assignedNode == null || assignedNode.Depleted) { FindNewNode(); return; }
                state = WorkState.Gathering;
            }
            else if (state == WorkState.ToDropOff)
            {
                Deliver();
            }
        }

        void TickGather(float dt)
        {
            if (assignedNode == null || assignedNode.Depleted) { FindNewNode(); return; }

            var owner = Owner;
            float rate = gatherPerSecond * (owner != null ? owner.ExtractionMultiplier : 1f);

            _gatherAccum += rate * dt;
            if (_gatherAccum >= 0.25f)
            {
                float want  = Mathf.Min(_gatherAccum, carryCapacity - carried);
                float taken = assignedNode.Extract(want);
                carried     += taken;
                _gatherAccum -= want;
                if (_gatherAccum < 0f) _gatherAccum = 0f;
            }

            if (carried >= carryCapacity - 0.01f) HeadToDropOff();
        }

        void HeadToDropOff()
        {
            _dropOff = FactionRegistry.Instance?.NearestDropOff(factionId, transform.position);
            if (_dropOff == null) { state = WorkState.Idle; return; }
            state = WorkState.ToDropOff;
            MoveTo(_dropOff.transform.position);
        }

        void Deliver()
        {
            var owner = Owner;
            if (owner != null && carried > 0f)
            {
                owner.Grant(carriedKind, carried);
                EconomyStats.RecordGathered(factionId, carriedKind, carried);
            }
            carried = 0f;

            if (assignedNode != null && !assignedNode.Depleted)
            {
                state = WorkState.ToNode;
                MoveTo(assignedNode.transform.position);
            }
            else
            {
                FindNewNode();
            }
        }

        void FindNewNode()
        {
            var next = FactionRegistry.Instance?.NearestFreeNode(transform.position, carriedKind);
            if (next == null) next = FactionRegistry.Instance?.NearestFreeNode(transform.position, null);

            if (next == null)
            {
                ReleaseNode();
                state = carried > 0f ? WorkState.ToDropOff : WorkState.Idle;
                if (state == WorkState.ToDropOff) HeadToDropOff();
                return;
            }
            AssignTo(next);
        }
    }
}
