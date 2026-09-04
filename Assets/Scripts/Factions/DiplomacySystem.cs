using System.Collections.Generic;
using UnityEngine;
using Statecraft.Core;

namespace Statecraft.Factions
{
    public enum Stance { Correct, Strained, Hostile, Embargo }

    /// <summary>
    /// Tension between powers, driven almost entirely by espionage that got caught.
    ///
    /// There is no diplomacy screen and no treaties. Getting caught raises tension;
    /// tension decays if you behave. Past a threshold the injured party embargoes you,
    /// which widens your market spread against them and makes the AI prioritise
    /// retaliation. Cheating is profitable — cheating VISIBLY is expensive.
    /// </summary>
    public class DiplomacySystem : MonoBehaviour
    {
        public static DiplomacySystem Instance { get; private set; }

        [Header("Tension")]
        public float decayPerSecond      = 0.06f;
        public float strainedThreshold   = 3f;
        public float hostileThreshold    = 7f;
        public float embargoThreshold    = 12f;
        [Tooltip("Extra market spread suffered per active embargo against you.")]
        public float embargoSpreadPenalty = 0.03f;

        readonly Dictionary<long, float> _tension = new Dictionary<long, float>();

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

        static long Key(int a, int b) => ((long)Mathf.Min(a, b) << 32) | (uint)Mathf.Max(a, b);

        void Tick(float dt)
        {
            var keys = new List<long>(_tension.Keys);
            for (int i = 0; i < keys.Count; i++)
            {
                float v = _tension[keys[i]] - decayPerSecond * dt;
                if (v <= 0f) _tension.Remove(keys[i]);
                else         _tension[keys[i]] = v;
            }
        }

        /// <summary>Called when an operation is DETECTED. Undetected operations cost nothing diplomatically.</summary>
        public void RegisterHostileAct(int perpetrator, int victim, float weight)
        {
            long k = Key(perpetrator, victim);
            _tension.TryGetValue(k, out float v);
            v += weight;
            _tension[k] = v;

            var stance = StanceBetween(perpetrator, victim);
            if (stance == Stance.Embargo)
                EventLog.Post(LogChannel.Diplomacy,
                    "Relations have collapsed. Trade sanctions are in force.", victim, new Color(1f, 0.5f, 0.4f));
        }

        public float TensionBetween(int a, int b)
            => _tension.TryGetValue(Key(a, b), out float v) ? v : 0f;

        public Stance StanceBetween(int a, int b)
        {
            float t = TensionBetween(a, b);
            if (t >= embargoThreshold) return Stance.Embargo;
            if (t >= hostileThreshold) return Stance.Hostile;
            if (t >= strainedThreshold) return Stance.Strained;
            return Stance.Correct;
        }

        /// <summary>Extra market spread a faction pays because of everyone currently embargoing it.</summary>
        public float EmbargoPenaltyFor(int factionId)
        {
            var reg = FactionRegistry.Instance;
            if (reg == null) return 0f;

            float penalty = 0f;
            foreach (var other in reg.Others(factionId))
                if (StanceBetween(factionId, other.id) == Stance.Embargo) penalty += embargoSpreadPenalty;
            return penalty;
        }

        public static string Label(Stance s)
        {
            switch (s)
            {
                case Stance.Strained: return "Strained";
                case Stance.Hostile:  return "Hostile";
                case Stance.Embargo:  return "EMBARGO";
                default:              return "Correct";
            }
        }
    }
}
