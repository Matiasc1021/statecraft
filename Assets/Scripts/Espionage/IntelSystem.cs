using System.Collections.Generic;
using UnityEngine;
using Statecraft.Core;
using Statecraft.Data;
using Statecraft.Factions;

namespace Statecraft.Espionage
{
    /// <summary>
    /// What you know about your rivals, and how wrong it is.
    ///
    /// The design bet of this game: the interesting fog is not fog of WAR, it is fog
    /// of INFORMATION. You can see their buildings on the map. What you cannot see is
    /// their Capital position, their stockpiles, their tech, and therefore whether the
    /// price move you are about to make will ruin them or cost you nothing.
    ///
    /// So intel is modelled as a decaying snapshot:
    ///   • A successful operation refreshes it to the truth.
    ///   • Confidence decays exponentially from that moment.
    ///   • Reported numbers are the truth plus noise scaled by (1 - confidence).
    ///   • Disinformation adds a deliberate BIAS on top, so a confident-looking
    ///     estimate can be confidently wrong. That is the point.
    ///
    /// The noise is deterministic per five-second bucket, so a stale estimate wobbles
    /// slowly instead of flickering every frame — readable, and still untrustworthy.
    /// </summary>
    public class IntelSystem : MonoBehaviour
    {
        public static IntelSystem Instance { get; private set; }

        [Header("Decay")]
        [Tooltip("Seconds for confidence to fall to ~37%.")]
        public float confidenceHalfLifeSeconds = 75f;

        [Header("Noise")]
        [Tooltip("Maximum relative error at zero confidence.")]
        public float maxRelativeError = 0.85f;

        public class Record
        {
            public float          capturedAt = -999f;
            public ResourceBundle stock;
            public int            ageIndex;
            public int            unitCount;
            public int            buildingCount;
            public float          security;
            public readonly HashSet<string> techs = new HashSet<string>();

            public float corruptedUntil;
            public float corruptionBias;     // multiplicative distortion, e.g. 1.8 or 0.4
        }

        public struct Estimate
        {
            public bool           hasAny;
            public float          confidence;      // 0..1
            public float          ageSeconds;
            public bool           corrupted;       // true only if WE could tell, which we cannot — always false to the UI
            public ResourceBundle stock;
            public int            ageIndex;
            public int            unitCount;
            public int            buildingCount;
            public float          security;
            public HashSet<string> techs;
        }

        readonly Dictionary<long, Record> _records = new Dictionary<long, Record>();

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        static long Key(int observer, int target) => ((long)observer << 32) | (uint)target;

        Record RecordFor(int observer, int target)
        {
            long k = Key(observer, target);
            if (!_records.TryGetValue(k, out var r)) { r = new Record(); _records[k] = r; }
            return r;
        }

        float Now => SimClock.Instance != null ? SimClock.Instance.Elapsed : Time.time;

        // ── Writing ───────────────────────────────────────────────────────

        /// <summary>A successful operation gives the observer a true snapshot of the target.</summary>
        public void Refresh(int observerFactionId, Faction target)
        {
            var reg = FactionRegistry.Instance;
            var r = RecordFor(observerFactionId, target.id);

            r.capturedAt    = Now;
            r.stock         = target.stock;
            r.ageIndex      = target.ageIndex;
            r.security      = target.Security;
            r.unitCount     = reg != null ? reg.UnitsOf(target.id).Count     : 0;
            r.buildingCount = reg != null ? reg.BuildingsOf(target.id).Count : 0;
            r.techs.Clear();
            foreach (var t in target.techs) r.techs.Add(t);
        }

        /// <summary>Plant disinformation: the observer's picture of the target goes badly wrong.</summary>
        public void Corrupt(int observerFactionId, int targetFactionId, float seconds)
        {
            var r = RecordFor(observerFactionId, targetFactionId);
            r.corruptedUntil = Now + seconds;
            r.corruptionBias = Random.value < 0.5f ? Random.Range(0.30f, 0.55f) : Random.Range(1.7f, 2.6f);
        }

        // ── Reading ───────────────────────────────────────────────────────

        public Estimate Get(int observerFactionId, Faction target)
        {
            var e = new Estimate { techs = new HashSet<string>() };
            if (target == null) return e;

            // You always know your own numbers exactly.
            if (observerFactionId == target.id)
            {
                var reg0 = FactionRegistry.Instance;
                e.hasAny        = true;
                e.confidence    = 1f;
                e.stock         = target.stock;
                e.ageIndex      = target.ageIndex;
                e.security      = target.Security;
                e.unitCount     = reg0 != null ? reg0.UnitsOf(target.id).Count     : 0;
                e.buildingCount = reg0 != null ? reg0.BuildingsOf(target.id).Count : 0;
                foreach (var t in target.techs) e.techs.Add(t);
                return e;
            }

            var r = RecordFor(observerFactionId, target.id);
            if (r.capturedAt < 0f) return e;                     // never had eyes on them

            float elapsed = Now - r.capturedAt;
            float decay   = confidenceHalfLifeSeconds / Mathf.Max(0.1f, target.IntelDecayMultiplier);
            float conf    = Mathf.Clamp01(Mathf.Exp(-elapsed / Mathf.Max(1f, decay)));

            bool corrupted = Now < r.corruptedUntil;
            float bias     = corrupted ? r.corruptionBias : 1f;

            e.hasAny        = true;
            e.confidence    = conf;
            e.ageSeconds    = elapsed;
            e.corrupted     = false;                             // deliberately never surfaced
            e.ageIndex      = r.ageIndex;
            e.buildingCount = Mathf.RoundToInt(Fuzz(r.buildingCount, conf, 3) * bias);
            e.unitCount     = Mathf.RoundToInt(Fuzz(r.unitCount, conf, 5) * bias);
            e.security      = Fuzz(r.security, conf, 7) * bias;

            for (int i = 0; i < ResourceBundle.AllKinds.Length; i++)
            {
                var k = ResourceBundle.AllKinds[i];
                e.stock.Set(k, Mathf.Max(0f, Fuzz(r.stock.Get(k), conf, 11 + i) * bias));
            }

            // Tech knowledge does not degrade gracefully — you either saw it or you did not.
            foreach (var t in r.techs) e.techs.Add(t);
            return e;
        }

        /// <summary>Truth plus deterministic noise that grows as confidence falls.</summary>
        float Fuzz(float trueValue, float confidence, int salt)
        {
            if (confidence >= 0.999f) return trueValue;

            int bucket = Mathf.FloorToInt(Now / 5f);
            float n = Hash01(bucket * 73856093 ^ salt * 19349663) * 2f - 1f;
            float error = maxRelativeError * (1f - confidence);
            return trueValue * (1f + n * error);
        }

        static float Hash01(int x)
        {
            unchecked
            {
                x = (x ^ 61) ^ (x >> 16);
                x = x + (x << 3);
                x = x ^ (x >> 4);
                x = x * unchecked((int)0x27d4eb2d);
                x = x ^ (x >> 15);
                return (x & 0x7fffffff) / (float)0x7fffffff;
            }
        }

        public static string ConfidenceLabel(float confidence)
        {
            if (confidence >= 0.85f) return "CONFIRMED";
            if (confidence >= 0.55f) return "RELIABLE";
            if (confidence >= 0.30f) return "STALE";
            if (confidence >= 0.10f) return "UNRELIABLE";
            return "NO CURRENT INTEL";
        }
    }
}
