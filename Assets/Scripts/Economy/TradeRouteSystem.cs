using System.Collections.Generic;
using UnityEngine;
using Statecraft.Core;
using Statecraft.Data;
using Statecraft.Entities;
using Statecraft.Factions;

namespace Statecraft.Economy
{
    /// <summary>
    /// A standing export contract: convert a resource into Capital at the market price,
    /// continuously, without the player clicking anything.
    ///
    /// Routes are the passive half of the economy and the reason espionage has teeth.
    /// Manual market trades are visible and instantaneous; routes are the thing a rival
    /// can quietly strangle with Cut Trade Route and you may not notice for a minute.
    /// </summary>
    public class TradeRoute
    {
        public int          ownerFactionId;
        public ResourceKind export;
        public float        throughputPerSecond = 1f;
        /// <summary>1.0 healthy. Sabotage drops this.</summary>
        public float        efficiency = 1f;
        public float        disruptedSeconds;
        public float        capitalEarnedTotal;

        public bool IsDisrupted => disruptedSeconds > 0f;
    }

    public class TradeRouteSystem : MonoBehaviour
    {
        public static TradeRouteSystem Instance { get; private set; }

        [Header("Sabotage")]
        public float sabotageSeconds    = 90f;
        public float sabotageEfficiency = 0.25f;

        readonly List<TradeRoute> _routes = new List<TradeRoute>();
        public IReadOnlyList<TradeRoute> Routes => _routes;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        void Start()
        {
            if (SimClock.Instance != null)
            {
                SimClock.Instance.OnTick -= Tick;
                SimClock.Instance.OnTick += Tick;
            }
        }

        void OnDisable() { if (SimClock.Instance != null) SimClock.Instance.OnTick -= Tick; }
        void OnDestroy() { if (Instance == this) Instance = null; }

        // ── Capacity ──────────────────────────────────────────────────────

        public int SlotsFor(int factionId)
        {
            var reg = FactionRegistry.Instance;
            var f   = reg?.Get(factionId);
            if (f == null) return 0;

            int slots = f.civ != null ? f.civ.bonusTradeRouteSlots : 0;
            var buildings = reg.BuildingsOf(factionId);
            for (int i = 0; i < buildings.Count; i++)
            {
                var b = buildings[i];
                if (b != null && b.IsOperational && b.def != null) slots += b.def.tradeRouteSlots;
            }
            return slots;
        }

        public int UsedSlots(int factionId)
        {
            int n = 0;
            for (int i = 0; i < _routes.Count; i++) if (_routes[i].ownerFactionId == factionId) n++;
            return n;
        }

        public List<TradeRoute> RoutesOf(int factionId)
        {
            var result = new List<TradeRoute>();
            for (int i = 0; i < _routes.Count; i++) if (_routes[i].ownerFactionId == factionId) result.Add(_routes[i]);
            return result;
        }

        // ── Commands ──────────────────────────────────────────────────────

        public bool TryOpenRoute(Faction f, ResourceKind export, float throughputPerSecond)
        {
            if (export == ResourceKind.Capital) return false;

            if (UsedSlots(f.id) >= SlotsFor(f.id))
            {
                if (f.isHuman) EventLog.Post(LogChannel.Economy,
                    "No free trade route slots — build an Exchange.", f.id);
                return false;
            }

            _routes.Add(new TradeRoute {
                ownerFactionId      = f.id,
                export              = export,
                throughputPerSecond = Mathf.Clamp(throughputPerSecond, 0.2f, 6f),
            });
            EventLog.Post(LogChannel.Economy,
                $"Trade route opened: exporting {ResourceBundle.Label(export)}.", f.id);
            return true;
        }

        public void CloseRoute(TradeRoute route) => _routes.Remove(route);

        /// <summary>Called by the Cut Trade Route operation.</summary>
        public bool DisruptRandomRoute(int victimFactionId)
        {
            var owned = RoutesOf(victimFactionId);
            if (owned.Count == 0) return false;

            var route = owned[Random.Range(0, owned.Count)];
            route.disruptedSeconds = sabotageSeconds;
            route.efficiency       = sabotageEfficiency;
            return true;
        }

        // ── Tick ──────────────────────────────────────────────────────────

        void Tick(float dt)
        {
            var reg    = FactionRegistry.Instance;
            var market = MarketSystem.Instance;
            if (reg == null || market == null) return;

            for (int i = _routes.Count - 1; i >= 0; i--)
            {
                var r = reg.Get(_routes[i].ownerFactionId);
                var route = _routes[i];
                if (r == null) { _routes.RemoveAt(i); continue; }

                if (route.disruptedSeconds > 0f)
                {
                    route.disruptedSeconds -= dt;
                    if (route.disruptedSeconds <= 0f)
                    {
                        route.disruptedSeconds = 0f;
                        route.efficiency = 1f;
                        EventLog.Post(LogChannel.Economy,
                            $"{ResourceBundle.Label(route.export)} route restored.", r.id);
                    }
                }

                float rate = route.throughputPerSecond * route.efficiency * r.TradeRouteMultiplier;
                float want = rate * dt;
                float have = r.stock.Get(route.export);
                if (have < want) continue;                       // nothing to ship this tick

                r.Drain(route.export, want);
                float revenue = want * market.PriceOf(route.export) * (1f - r.MarketSpread);
                r.stock.capital += revenue;
                route.capitalEarnedTotal += revenue;

                EconomyStats.For(r.id).capitalFromTrade += revenue;

                r.netPerSecond.capital += revenue / dt;
                r.netPerSecond.Add(route.export, -rate);
            }
        }
    }
}
