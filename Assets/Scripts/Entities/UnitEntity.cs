using UnityEngine;
using Statecraft.Data;

namespace Statecraft.Entities
{
    /// <summary>
    /// A moving unit.
    ///
    /// Deliberate choice: movement is direct steering on a flat plane, NOT NavMesh.
    /// The previous project needed a manual NavMesh bake before anything would move,
    /// which meant "press Play" did not work on a fresh clone. Flat-plane steering
    /// gets the economic loop testable today; swap in NavMeshAgent (or a flow field,
    /// which is what an RTS with hundreds of units actually wants) at the point where
    /// terrain stops being flat. See docs/ROADMAP.md.
    /// </summary>
    public class UnitEntity : Entity
    {
        public UnitDef def;

        [Header("Movement")]
        public float moveSpeed    = 6f;
        public float arriveRadius = 0.6f;
        public float turnSpeed    = 12f;

        protected Vector3 _destination;
        protected bool    _hasDestination;

        public bool IsMoving => _hasDestination;
        public Vector3 Destination => _destination;

        public override string DisplayName => def != null ? def.displayName : "Unit";

        protected override void Awake()
        {
            base.Awake();
            if (def != null)
            {
                maxHp     = def.maxHp;
                if (hp <= 0f || hp > maxHp) hp = def.maxHp;
                moveSpeed = def.moveSpeed;
            }
        }

        /// <summary>Applies a UnitDef after instantiation (the bootstrap uses this).</summary>
        public virtual void Configure(UnitDef d, int owningFactionId)
        {
            def       = d;
            factionId = owningFactionId;
            maxHp     = d.maxHp;
            hp        = d.maxHp;
            moveSpeed = d.moveSpeed;
        }

        public virtual void MoveTo(Vector3 worldPos)
        {
            worldPos.y      = transform.position.y;
            _destination    = worldPos;
            _hasDestination = true;
        }

        public virtual void StopMoving() => _hasDestination = false;

        protected virtual void Update()
        {
            StepMovement(Time.deltaTime);
        }

        protected void StepMovement(float dt)
        {
            if (!_hasDestination) return;

            Vector3 flat = _destination;
            flat.y = transform.position.y;

            Vector3 to = flat - transform.position;
            float dist = to.magnitude;

            if (dist <= arriveRadius)
            {
                _hasDestination = false;
                OnArrived();
                return;
            }

            Vector3 dir = to / Mathf.Max(dist, 0.0001f);
            transform.position += dir * moveSpeed * dt;

            var look = Quaternion.LookRotation(new Vector3(dir.x, 0f, dir.z), Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, look, turnSpeed * dt);
        }

        protected virtual void OnArrived() { }

        /// <summary>Distance on the ground plane, ignoring height.</summary>
        public float FlatDistanceTo(Vector3 p)
        {
            Vector3 a = transform.position; a.y = 0f;
            p.y = 0f;
            return Vector3.Distance(a, p);
        }
    }
}
