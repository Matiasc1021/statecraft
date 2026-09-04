using System.Collections.Generic;
using UnityEngine;
using Statecraft.Core;
using Statecraft.Data;
using Statecraft.Factions;

namespace Statecraft.Entities
{
    public class ProductionOrder
    {
        public string unitId;
        public float  progress;      // 0..1
        public float  totalSeconds;
    }

    /// <summary>
    /// A building. Three jobs, depending on its def:
    ///   1. Convert resources (recipe) — this is where the economy actually happens.
    ///   2. Train units (trains list).
    ///   3. Provide passive capacity — housing, security, trade route slots.
    ///
    /// A building only runs at full rate when its owner has the labour to staff it
    /// and the inputs to feed it. Sabotage stops it outright — which is the whole
    /// point of the espionage layer.
    /// </summary>
    public class BuildingEntity : Entity
    {
        public BuildingDef def;

        [Header("Construction")]
        public bool  underConstruction = true;
        public float buildProgress;               // 0..1

        [Header("Runtime")]
        public float lastEfficiency;              // 0..1, what the recipe actually achieved last tick
        public float sabotagedSeconds;            // > 0 means offline
        public Vector3 rallyPoint;

        public readonly List<ProductionOrder> queue = new List<ProductionOrder>();

        public bool IsOperational => !underConstruction && IsAlive && sabotagedSeconds <= 0f;
        public bool IsSabotaged   => sabotagedSeconds > 0f;
        public bool IsDropOff     => def != null && (def.role == BuildingRole.Capitol || def.role == BuildingRole.Extractor);
        public bool HasRecipe     => def != null && def.recipe != null;

        public override string DisplayName => def != null ? def.displayName : "Building";

        public override string StatusLine
        {
            get
            {
                if (underConstruction) return $"Under construction {buildProgress * 100f:0}%";
                if (IsSabotaged)       return $"SABOTAGED — offline {sabotagedSeconds:0}s";
                if (queue.Count > 0)   return $"Producing {Content.Unit(queue[0].unitId)?.displayName} {queue[0].progress * 100f:0}%";
                if (HasRecipe)         return $"Running at {lastEfficiency * 100f:0}%";
                return "Idle";
            }
        }

        public void Configure(BuildingDef d, int owningFactionId, bool startComplete)
        {
            def       = d;
            factionId = owningFactionId;
            maxHp     = d.maxHp;
            hp        = startComplete ? d.maxHp : Mathf.Max(1f, d.maxHp * 0.15f);
            underConstruction = !startComplete;
            buildProgress     = startComplete ? 1f : 0f;
            rallyPoint        = transform.position + transform.forward * (d.footprint.y * 0.5f + 4f);
        }

        // ── Construction ──────────────────────────────────────────────────

        /// <summary>Called by the economy tick. Returns true on the tick it completes.</summary>
        public bool AdvanceConstruction(float dt, float speedMultiplier)
        {
            if (!underConstruction || def == null) return false;

            buildProgress += (dt / Mathf.Max(0.1f, def.buildSeconds)) * speedMultiplier;
            hp = Mathf.Lerp(def.maxHp * 0.15f, def.maxHp, buildProgress);

            if (buildProgress >= 1f)
            {
                buildProgress     = 1f;
                underConstruction = false;
                hp                = def.maxHp;
                return true;
            }
            return false;
        }

        // ── Production queue ──────────────────────────────────────────────

        public bool CanQueue(string unitId)
        {
            if (def == null || !def.CanTrain(unitId)) return false;
            var u = Content.Unit(unitId);
            var f = Owner;
            if (u == null || f == null) return false;
            return f.ageIndex >= u.minAge && queue.Count < 8;
        }

        public bool TryQueue(string unitId)
        {
            if (!CanQueue(unitId)) return false;
            var u = Content.Unit(unitId);
            var f = Owner;

            if (f.population + u.housingCost > f.housingCapacity)
            {
                EventLog.Post(LogChannel.Economy, "No housing capacity — build a Habitat Block.", factionId);
                return false;
            }
            if (!f.TrySpend(u.cost))
            {
                var missing = f.stock.FirstShortfall(u.cost);
                EventLog.Post(LogChannel.Economy,
                    $"Cannot afford {u.displayName} — short on {(missing.HasValue ? ResourceBundle.Label(missing.Value) : "resources")}.",
                    factionId);
                return false;
            }

            queue.Add(new ProductionOrder { unitId = unitId, progress = 0f, totalSeconds = u.buildSeconds });
            return true;
        }

        public void CancelFront()
        {
            if (queue.Count == 0) return;
            var u = Content.Unit(queue[0].unitId);
            var f = Owner;
            if (u != null && f != null) f.Grant(u.cost);       // full refund, this is a prototype
            queue.RemoveAt(0);
        }

        /// <summary>Advances the front order. Returns the finished unit id, or null.</summary>
        public string AdvanceProduction(float dt, float speedMultiplier)
        {
            if (queue.Count == 0 || !IsOperational) return null;

            var order = queue[0];
            order.progress += (dt / Mathf.Max(0.1f, order.totalSeconds)) * speedMultiplier;
            if (order.progress < 1f) return null;

            queue.RemoveAt(0);
            return order.unitId;
        }

        // ── Sabotage ──────────────────────────────────────────────────────

        public void ApplySabotage(float seconds)
        {
            sabotagedSeconds = Mathf.Max(sabotagedSeconds, seconds);
            lastEfficiency   = 0f;
        }

        public void TickSabotage(float dt)
        {
            if (sabotagedSeconds > 0f) sabotagedSeconds = Mathf.Max(0f, sabotagedSeconds - dt);
        }
    }
}
