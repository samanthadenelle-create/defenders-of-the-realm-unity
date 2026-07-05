// =============================================================================
// CombatStatusTracker — timed combat statuses (slow / freeze / burn + named buffs).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.Combat
//
// Shared timer store for hero + enemy combat status HUD rows (WO-609 Phase 2).
// Producers call CollectActive into Core snapshots; presentation never reads timers.
// =============================================================================

using System;
using System.Collections.Generic;

namespace DeNelle.Core.Combat
{
    /// <summary>One active status for the HUD buff/debuff row.</summary>
    public readonly struct ActiveStatusSnapshot
    {
        /// <summary>Stable id (e.g. "slow", "mana-draught").</summary>
        public readonly string Id;
        /// <summary>Short player-facing label.</summary>
        public readonly string Label;
        /// <summary>True for buffs; false for debuffs.</summary>
        public readonly bool IsBuff;
        /// <summary>Seconds remaining.</summary>
        public readonly float RemainingSeconds;
        /// <summary>Original applied duration (for HUD sweep).</summary>
        public readonly float TotalSeconds;

        /// <summary>Constructs an active-status snapshot.</summary>
        public ActiveStatusSnapshot(string id, string label, bool isBuff, float remainingSeconds, float totalSeconds = 0f)
        {
            Id = id ?? "";
            Label = label ?? "";
            IsBuff = isBuff;
            RemainingSeconds = remainingSeconds;
            TotalSeconds = totalSeconds > 0f ? totalSeconds : remainingSeconds;
        }
    }

    /// <summary>Lightweight timer bag for CC + named timed buffs/debuffs.</summary>
    public sealed class CombatStatusTracker
    {
        private float _slowUntil;
        private float _freezeUntil;
        private float _burnUntil;
        private float _slowDuration;
        private float _freezeDuration;
        private float _burnDuration;

        private readonly Dictionary<string, NamedEntry> _named =
            new Dictionary<string, NamedEntry>(StringComparer.OrdinalIgnoreCase);

        private struct NamedEntry
        {
            public float Until;
            public float Duration;
            public bool IsBuff;
            public string Label;
        }

        /// <summary>Removes a named timed status (e.g. when a buff expires early).</summary>
        public void ClearNamed(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            _named.Remove(id);
        }

        /// <summary>Apply a core CC status for <paramref name="seconds"/>.</summary>
        public void Apply(StatusEffect effect, float seconds)
        {
            if (seconds <= 0f) return;
            float until = UnityEngine.Time.time + seconds;
            switch (effect)
            {
                case StatusEffect.Slow:
                    _slowUntil = Math.Max(_slowUntil, until);
                    _slowDuration = seconds;
                    break;
                case StatusEffect.Freeze:
                    _freezeUntil = Math.Max(_freezeUntil, until);
                    _freezeDuration = seconds;
                    break;
                case StatusEffect.Burn:
                    _burnUntil = Math.Max(_burnUntil, until);
                    _burnDuration = seconds;
                    break;
            }
        }

        /// <summary>Apply or refresh a named timed buff/debuff (e.g. mana draught drip).</summary>
        public void ApplyNamed(string id, string label, float seconds, bool isBuff)
        {
            if (string.IsNullOrEmpty(id) || seconds <= 0f) return;
            float until = UnityEngine.Time.time + seconds;
            if (_named.TryGetValue(id, out var prev))
                until = Math.Max(until, prev.Until);
            _named[id] = new NamedEntry { Until = until, Duration = seconds, IsBuff = isBuff, Label = label ?? id };
        }

        /// <summary>True while freeze is active.</summary>
        public bool IsFrozen => UnityEngine.Time.time < _freezeUntil;

        /// <summary>True while slow is active.</summary>
        public bool IsSlowed => UnityEngine.Time.time < _slowUntil;

        /// <summary>True while burn is active.</summary>
        public bool IsBurning => UnityEngine.Time.time < _burnUntil;

        /// <summary>
        /// Fills <paramref name="dst"/> with active statuses (debuffs first, then buffs),
        /// capped at <paramref name="max"/>. Prunes expired named entries.
        /// </summary>
        public void CollectActive(List<ActiveStatusSnapshot> dst, int max = 6)
        {
            if (dst == null) return;
            float now = UnityEngine.Time.time;

            if (now < _freezeUntil && dst.Count < max)
                dst.Add(new ActiveStatusSnapshot("freeze", "Freeze", false, _freezeUntil - now, _freezeDuration));
            if (now < _slowUntil && dst.Count < max)
                dst.Add(new ActiveStatusSnapshot("slow", "Slow", false, _slowUntil - now, _slowDuration));
            if (now < _burnUntil && dst.Count < max)
                dst.Add(new ActiveStatusSnapshot("burn", "Burn", false, _burnUntil - now, _burnDuration));

            if (_named.Count > 0)
            {
                _scratchKeys.Clear();
                foreach (var kv in _named)
                {
                    if (now >= kv.Value.Until) _scratchKeys.Add(kv.Key);
                }
                for (int i = 0; i < _scratchKeys.Count; i++)
                    _named.Remove(_scratchKeys[i]);

                foreach (var kv in _named)
                {
                    if (dst.Count >= max) break;
                    float rem = kv.Value.Until - now;
                    if (rem <= 0f) continue;
                    dst.Add(new ActiveStatusSnapshot(kv.Key, kv.Value.Label, kv.Value.IsBuff, rem, kv.Value.Duration));
                }
            }
        }

        private readonly List<string> _scratchKeys = new List<string>(4);
    }
}