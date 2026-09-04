using System;
using UnityEngine;

namespace Statecraft.Core
{
    /// <summary>
    /// ── SIMULATION CLOCK ────────────────────────────────────────────────
    /// The economy does not run in Update(). It runs on a fixed tick so that
    /// every rate in the game can be expressed per SECOND and stay true whether
    /// the player is running at 30fps or 240fps — and so that a replay or a
    /// deterministic lockstep netcode layer can be bolted on later without
    /// rewriting every system.
    ///
    /// Everything economic subscribes to OnTick. Nothing economic reads Time.deltaTime.
    ///
    /// Public API
    /// ──────────
    ///   SimClock.Instance.OnTick   += (dt) => { ... }   // dt is always TickSeconds
    ///   SimClock.Instance.Tick                          // ticks elapsed
    ///   SimClock.Instance.Elapsed                       // simulated seconds
    ///   SimClock.Instance.SpeedMultiplier = 2f          // game speed
    /// </summary>
    public class SimClock : MonoBehaviour
    {
        public static SimClock Instance { get; private set; }

        public const int   TicksPerSecond = 10;
        public const float TickSeconds    = 1f / TicksPerSecond;

        [Header("Speed")]
        [Range(0.25f, 4f)] public float speedMultiplier = 1f;
        public bool paused;

        public int   Tick    { get; private set; }
        public float Elapsed => Tick * TickSeconds;

        /// <summary>Fired once per simulation tick. The float is always TickSeconds.</summary>
        public event Action<float> OnTick;

        float _accumulator;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            if (paused) return;

            _accumulator += Time.deltaTime * Mathf.Max(0.05f, speedMultiplier);

            // Cap catch-up so a long editor stall does not fast-forward the match.
            int guard = 0;
            while (_accumulator >= TickSeconds && guard < 8)
            {
                _accumulator -= TickSeconds;
                Tick++;
                guard++;
                try { OnTick?.Invoke(TickSeconds); }
                catch (Exception e) { Debug.LogException(e); }
            }
            if (guard >= 8) _accumulator = 0f;
        }

        /// <summary>Seconds -> ticks, for anything that wants to count down in ticks.</summary>
        public static int SecondsToTicks(float seconds) => Mathf.Max(1, Mathf.RoundToInt(seconds * TicksPerSecond));
    }
}
