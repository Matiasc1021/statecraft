using System.Collections.Generic;
using UnityEngine;

namespace Statecraft.Core
{
    public enum LogChannel { Economy, Espionage, Military, Diplomacy, System }

    public struct LogEntry
    {
        public float      time;
        public LogChannel channel;
        public string     text;
        public Color      tint;
        /// <summary>Faction this entry is addressed to. -1 means everyone sees it.</summary>
        public int        audienceFactionId;
    }

    /// <summary>
    /// The player's feed. Espionage is only interesting if you can SEE the
    /// consequences, so every system that changes someone's economy behind their
    /// back reports here — to the perpetrator always, to the victim only if they
    /// detected it.
    /// </summary>
    public class EventLog : MonoBehaviour
    {
        public static EventLog Instance { get; private set; }

        public const int MaxEntries = 200;
        readonly List<LogEntry> _entries = new List<LogEntry>();
        public IReadOnlyList<LogEntry> Entries => _entries;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        public static void Post(LogChannel channel, string text, int audienceFactionId = -1, Color? tint = null)
        {
            if (Instance == null) { Debug.Log($"[{channel}] {text}"); return; }

            Instance._entries.Add(new LogEntry {
                time              = SimClock.Instance ? SimClock.Instance.Elapsed : Time.time,
                channel           = channel,
                text              = text,
                tint              = tint ?? DefaultTint(channel),
                audienceFactionId = audienceFactionId,
            });

            if (Instance._entries.Count > MaxEntries)
                Instance._entries.RemoveRange(0, Instance._entries.Count - MaxEntries);
        }

        public static Color DefaultTint(LogChannel c)
        {
            switch (c)
            {
                case LogChannel.Economy:   return new Color(0.95f, 0.85f, 0.45f);
                case LogChannel.Espionage: return new Color(0.72f, 0.55f, 0.95f);
                case LogChannel.Military:  return new Color(0.95f, 0.45f, 0.40f);
                case LogChannel.Diplomacy: return new Color(0.50f, 0.85f, 0.95f);
                default:                   return Color.white;
            }
        }

        /// <summary>Entries visible to a given faction, newest last.</summary>
        public List<LogEntry> For(int factionId, int max = 12)
        {
            var result = new List<LogEntry>();
            for (int i = _entries.Count - 1; i >= 0 && result.Count < max; i--)
            {
                var e = _entries[i];
                if (e.audienceFactionId == -1 || e.audienceFactionId == factionId)
                    result.Add(e);
            }
            result.Reverse();
            return result;
        }
    }
}
