using System.Collections.Generic;
using UnityEngine;
using Statecraft.Core;
using Statecraft.Data;
using Statecraft.Factions;

namespace Statecraft.Economy
{
    /// <summary>
    /// ── THE COMMODITY MARKET ────────────────────────────────────────────
    /// One global price per resource, quoted in Capital. Capital itself is the
    /// numeraire and always worth 1.
    ///
    /// Prices are not scripted. They come from PRESSURE:
    ///
    ///     price = base x clamp(1 + pressure, 0.25, 4.0)
    ///
    /// Buying pushes pressure up by (quantity / depth); selling pushes it down.
    /// Pressure mean-reverts toward zero every tick. "Depth" is per-resource
    /// liquidity: Data is thin, Materials are deep, so the same 500-unit order
    /// barely moves Materials and badly moves Data.
    ///
    /// This is the mechanic the whole game hangs off:
    ///   • Dumping  — sell hard into a commodity your rival depends on selling.
    ///   • Cornering — buy up a thin market before your rival needs to buy.
    ///   • Front-running — know their position before they trade (that is what
    ///     the espionage layer is FOR).
    ///
    /// A faction's spread (Faction.MarketSpread) is the cut it loses on every
    /// trade. Meridian trades at roughly a third less friction than Novaya, which
    /// is the entire difference between those two civilisations' game plans.
    /// </summary>
    public class MarketSystem : MonoBehaviour
    {
        public static MarketSystem Instance { get; private set; }

        public const int HistoryLength = 180;

        [Header("Dynamics")]
        [Tooltip("Fraction of accumulated pressure that decays back toward fair value each second.")]
        public float meanReversionPerSecond = 0.12f;
        [Tooltip("Random noise added to pressure per second. Keeps prices alive between trades.")]
        public float noisePerSecond = 0.004f;

        class Book
        {
            public float basePrice;
            public float depth;
            public float pressure;
            public readonly Queue<float> history = new Queue<float>();
        }

        readonly Dictionary<ResourceKind, Book> _books = new Dictionary<ResourceKind, Book>();

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            //                          base price   depth (units that move price by 1.0)
            _books[ResourceKind.Energy]     = new Book { basePrice =  2.0f, depth =  900f };
            _books[ResourceKind.Materials]  = new Book { basePrice =  1.0f, depth = 1400f };
            _books[ResourceKind.Components] = new Book { basePrice =  5.0f, depth =  500f };
            _books[ResourceKind.Data]       = new Book { basePrice = 14.0f, depth =  180f };
        }

        void Start()
        {
            if (SimClock.Instance != null)
            {
                SimClock.Instance.OnTick -= Tick;
                SimClock.Instance.OnTick += Tick;
            }
        }

        void OnDisable()
        {
            if (SimClock.Instance != null) SimClock.Instance.OnTick -= Tick;
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        // ── Tick ──────────────────────────────────────────────────────────

        void Tick(float dt)
        {
            foreach (var kv in _books)
            {
                var b = kv.Value;
                b.pressure *= Mathf.Max(0f, 1f - meanReversionPerSecond * dt);
                b.pressure += Random.Range(-noisePerSecond, noisePerSecond) * dt * 10f;

                b.history.Enqueue(PriceOf(kv.Key));
                while (b.history.Count > HistoryLength) b.history.Dequeue();
            }
        }

        // ── Quotes ────────────────────────────────────────────────────────

        public bool IsTradable(ResourceKind k) => k != ResourceKind.Capital;

        public float PriceOf(ResourceKind k)
        {
            if (k == ResourceKind.Capital) return 1f;
            if (!_books.TryGetValue(k, out var b)) return 1f;
            return b.basePrice * Mathf.Clamp(1f + b.pressure, 0.25f, 4.0f);
        }

        public float BasePriceOf(ResourceKind k)
            => _books.TryGetValue(k, out var b) ? b.basePrice : 1f;

        /// <summary>-1..+1ish. How far the price has moved from fair value. The HUD colours on this.</summary>
        public float DeviationOf(ResourceKind k)
        {
            float bp = BasePriceOf(k);
            return bp <= 0f ? 0f : (PriceOf(k) - bp) / bp;
        }

        public float[] HistoryOf(ResourceKind k)
            => _books.TryGetValue(k, out var b) ? b.history.ToArray() : new float[0];

        /// <summary>What a buy would cost right now, including the faction's spread.</summary>
        public float QuoteBuy(Faction f, ResourceKind k, float qty)
            => qty * PriceOf(k) * (1f + f.MarketSpread) * (1f + Impact(k, qty) * 0.5f);

        /// <summary>What a sell would pay right now, after the faction's spread.</summary>
        public float QuoteSell(Faction f, ResourceKind k, float qty)
            => qty * PriceOf(k) * (1f - f.MarketSpread) * (1f - Impact(k, qty) * 0.5f);

        float Impact(ResourceKind k, float qty)
            => _books.TryGetValue(k, out var b) ? Mathf.Clamp(qty / Mathf.Max(1f, b.depth), 0f, 0.6f) : 0f;

        // ── Orders ────────────────────────────────────────────────────────

        /// <summary>Buy <paramref name="qty"/> of a resource with Capital. Moves the price up.</summary>
        public bool Buy(Faction f, ResourceKind k, float qty)
        {
            if (!IsTradable(k) || qty <= 0f) return false;
            if (!RequiresExchange(f)) return false;

            float cost = QuoteBuy(f, k, qty);
            if (f.stock.capital < cost)
            {
                if (f.isHuman) EventLog.Post(LogChannel.Economy,
                    $"Not enough Capital: buying {qty:0} {ResourceBundle.Label(k)} costs {cost:0}.", f.id);
                return false;
            }

            f.stock.capital -= cost;
            f.Grant(k, qty);
            ApplyPressure(k, +qty);

            var t = EconomyStats.For(f.id);
            t.boughtVolume.Add(k, qty);
            t.capitalFromMarket -= cost;
            return true;
        }

        /// <summary>Sell <paramref name="qty"/> of a resource for Capital. Moves the price down.</summary>
        public bool Sell(Faction f, ResourceKind k, float qty)
        {
            if (!IsTradable(k) || qty <= 0f) return false;
            if (!RequiresExchange(f)) return false;
            if (f.stock.Get(k) < qty)
            {
                if (f.isHuman) EventLog.Post(LogChannel.Economy,
                    $"Not enough {ResourceBundle.Label(k)} to sell.", f.id);
                return false;
            }

            float revenue = QuoteSell(f, k, qty);
            f.Drain(k, qty);
            f.stock.capital += revenue;
            ApplyPressure(k, -qty);

            var t = EconomyStats.For(f.id);
            t.soldVolume.Add(k, qty);
            t.capitalFromMarket += revenue;
            return true;
        }

        bool RequiresExchange(Faction f)
        {
            if (FactionRegistry.Instance.HasBuilding(f.id, BuildingRole.Exchange)) return true;
            if (f.isHuman) EventLog.Post(LogChannel.Economy, "Trading requires an operational Exchange.", f.id);
            return false;
        }

        void ApplyPressure(ResourceKind k, float signedQty)
        {
            if (!_books.TryGetValue(k, out var b)) return;
            b.pressure += signedQty / Mathf.Max(1f, b.depth);
            b.pressure = Mathf.Clamp(b.pressure, -0.75f, 3.0f);
        }

        /// <summary>Used by the Move the Market operation. A deliberate, attributable price shock.</summary>
        public void ForcePriceShift(ResourceKind k, float fraction)
        {
            if (!_books.TryGetValue(k, out var b)) return;
            b.pressure = Mathf.Clamp(b.pressure + fraction, -0.75f, 3.0f);
        }

        /// <summary>The resource a faction is most exposed to — what a market attack should target.</summary>
        public ResourceKind LargestHolding(Faction f)
        {
            ResourceKind best = ResourceKind.Materials;
            float bestValue = -1f;
            for (int i = 0; i < ResourceBundle.AllKinds.Length; i++)
            {
                var k = ResourceBundle.AllKinds[i];
                if (!IsTradable(k)) continue;
                float v = f.stock.Get(k) * PriceOf(k);
                if (v > bestValue) { bestValue = v; best = k; }
            }
            return best;
        }
    }
}
