// =============================================================================
// DialogueEventBus — a tiny, pure-C# signal bus so GAMEPLAY can raise named
// events that a scripted (Yarn) dialogue waits on via <<wait_for_event NAME>>.
// -----------------------------------------------------------------------------
// The FTUE dialogue (CompanionMeeting.yarn) blocks on real player actions:
//   <<wait_for_event tower_placed>>   — player placed the free watchtower
//   <<wait_for_event wave_cleared>>   — the scripted first wave is dead
// Gameplay systems (TowerPlacementSystem.OnTowerPlaced, WaveManager.OnWaveCleared,
// etc.) raise the matching event here; the Yarn command bridge's wait coroutine
// polls HasFired(name) and resumes the dialogue.
//
// Lives in DeNelle.Core (no Yarn / no Village dependency) so ANY assembly can
// raise or observe without a cross-module reference. Case-insensitive names.
// Raised events latch (stay "fired") until Clear()'d — the wait coroutine clears
// the name when it STARTS waiting, so it only ever resolves on a FRESH raise.
// =============================================================================

using System;
using System.Collections.Generic;

namespace DeNelle.Core
{
    /// <summary>
    /// Process-wide signal bus bridging gameplay events to scripted dialogue
    /// waits. Pure static; survives scene loads. Thread-affine to the main thread
    /// (Unity events only) so no locking is needed.
    /// </summary>
    public static class DialogueEventBus
    {
        private static readonly HashSet<string> _fired =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Raised whenever an event fires, with the event name.</summary>
        public static event Action<string> Fired;

        /// <summary>Raise a named event (e.g. "tower_placed"). No-op on null/empty.</summary>
        public static void Raise(string eventName)
        {
            if (string.IsNullOrEmpty(eventName)) return;
            _fired.Add(eventName);
            Fired?.Invoke(eventName);
        }

        /// <summary>True if <paramref name="eventName"/> has fired since the last Clear.</summary>
        public static bool HasFired(string eventName) =>
            !string.IsNullOrEmpty(eventName) && _fired.Contains(eventName);

        /// <summary>Clear one event's fired-latch (a waiter calls this when it begins waiting).</summary>
        public static void Clear(string eventName)
        {
            if (!string.IsNullOrEmpty(eventName)) _fired.Remove(eventName);
        }

        /// <summary>Clear every latched event (e.g. on a fresh FTUE run).</summary>
        public static void ClearAll() => _fired.Clear();
    }
}
