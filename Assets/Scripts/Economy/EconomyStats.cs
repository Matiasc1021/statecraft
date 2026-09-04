using System.Collections.Generic;
using Statecraft.Data;

namespace Statecraft.Economy
{
    /// <summary>
    /// Cumulative per-faction totals, for the post-match screen and for the AI to
    /// reason about its own trajectory. Cheap, allocation-free, and separate from the
    /// sim so it can be ripped out without touching gameplay.
    /// </summary>
    public static class EconomyStats
    {
        public class Totals
        {
            public ResourceBundle gathered;
            public ResourceBundle produced;
            public ResourceBundle boughtVolume;
            public ResourceBundle soldVolume;
            public float capitalFromTrade;
            public float capitalFromMarket;
            public float capitalStolenByOps;
            public float capitalLostToOps;
            public int   opsAttempted;
            public int   opsSucceeded;
            public int   opsDetected;
        }

        static readonly Dictionary<int, Totals> _byFaction = new Dictionary<int, Totals>();

        public static Totals For(int factionId)
        {
            if (!_byFaction.TryGetValue(factionId, out var t)) { t = new Totals(); _byFaction[factionId] = t; }
            return t;
        }

        public static void Reset() => _byFaction.Clear();

        public static void RecordGathered(int factionId, ResourceKind kind, float amount)
            => For(factionId).gathered.Add(kind, amount);

        public static void RecordProduced(int factionId, ResourceBundle amount)
        {
            var t = For(factionId);
            t.produced = t.produced + amount;
        }
    }
}
