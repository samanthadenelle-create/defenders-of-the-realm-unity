// =============================================================================
// PostureSignals — Core-visible battle-arc / interaction signals for the HUD kit.
// (HUD_OBSIDIAN_ARCHITECTURE_2026-07-03 amendments A4.4-A4.7 — P23 HUDKIT.)
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.HudModel
//
// THE PROBLEM THIS SOLVES: the posture arc (calm -> hostile(prebattle |
// activebattle | postbattle)) needs Village-side facts (enemy pursuit/aggro,
// the end-state screen, NPC-in-range talk availability) but DeNelle.HUD may
// reference DeNelle.Core ONLY (CLAUDE.md §5). Village pushes the facts here;
// the HUD-side PostureEvaluator reads them. Pure data — no UnityEngine.UI.
//
// PURSUIT is PULSE-BASED (self-decaying): Village re-reports an active pursuit
// every poll tick (RegionMobSpawner aggro loop); a pursuit that stops being
// reported expires after PursuitTtl seconds. This makes the engagement window
// robust against missed "clear" paths (death/despawn/scene-unload) — the same
// reasoning as the leash system's own decay.
//
// TALK availability ALSO lives here (root cause of the "talk button never
// appears" §0 felt failure): TalkHudBridge used a ONE-SHOT reflection hook onto
// a PER-SCENE VillageHudController — after the first scene swap the cached
// MethodInfo targeted a destroyed instance and the bridge never re-hooked
// (MaxResolveAttempts exhausted), so availability was never pushed again.
// A Core static cannot go stale.
// =============================================================================

using System;
using UnityEngine;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Core.HudModel
{
    /// <summary>Village-pushed facts the HUD posture arc derives from (A4.4-A4.7).</summary>
    public static class PostureSignals
    {
        /// <summary>Seconds a reported pursuit stays live without being re-reported.</summary>
        public const float PursuitTtl = 1.5f;

        // ── Pursuit / engagement window (A4.5) ───────────────────────────────

        // Small fixed ring of (key, lastReport) pairs — no allocation per pulse.
        private const int MaxTracked = 12;
        private static readonly int[] _pursuitKeys = new int[MaxTracked];
        private static readonly float[] _pursuitAt = new float[MaxTracked];
        private static int _pursuitCount;

        /// <summary>
        /// Report that the enemy identified by <paramref name="key"/> (instance id) is
        /// actively pursuing the hero RIGHT NOW. Call every aggro tick — the report
        /// self-expires after <see cref="PursuitTtl"/> so no explicit clear is needed.
        /// </summary>
        public static void ReportPursuit(int key)
        {
            float now = Time.unscaledTime;
            for (int i = 0; i < _pursuitCount; i++)
            {
                if (_pursuitKeys[i] != key) continue;
                _pursuitAt[i] = now;
                return;
            }
            Prune(now);
            if (_pursuitCount >= MaxTracked) return;   // ring full — window is already open
            _pursuitKeys[_pursuitCount] = key;
            _pursuitAt[_pursuitCount] = now;
            _pursuitCount++;
            FlowTrace.Step("HudKit", $"pursuit reported (key={key}, live={_pursuitCount})");
        }

        /// <summary>True while at least one un-expired pursuit report is live —
        /// the enemy half of the A4.5 engagement window.</summary>
        public static bool PursuitActive
        {
            get
            {
                float now = Time.unscaledTime;
                Prune(now);
                return _pursuitCount > 0;
            }
        }

        private static void Prune(float now)
        {
            for (int i = _pursuitCount - 1; i >= 0; i--)
            {
                if (now - _pursuitAt[i] <= PursuitTtl) continue;
                _pursuitCount--;
                _pursuitKeys[i] = _pursuitKeys[_pursuitCount];
                _pursuitAt[i] = _pursuitAt[_pursuitCount];
            }
        }

        // ── Postbattle / end-state (A4.6 — the decision node owns the screen) ─

        /// <summary>True while the shared end-state template (Victory/Defeat) is on
        /// screen — hostile(postbattle): the HUD kit stands down.</summary>
        public static bool EndStateVisible { get; private set; }

        /// <summary>Producer-only (EndStateView Show/teardown).</summary>
        public static void SetEndState(bool visible)
        {
            if (EndStateVisible == visible) return;
            EndStateVisible = visible;
            FlowTrace.Step("HudKit", "end-state " + (visible ? "SHOWN (posture -> hostile(postbattle))" : "dismissed"));
        }

        // ── Talk availability (§0 root-cause fix — see file header) ──────────

        /// <summary>True while a talkable NPC is in range of the hero (TalkHudBridge pushes).</summary>
        public static bool TalkAvailable { get; private set; }

        /// <summary>Raised when <see cref="TalkAvailable"/> changes value.</summary>
        public static event Action TalkChanged;

        /// <summary>Producer-only (Village TalkHudBridge).</summary>
        public static void SetTalkAvailable(bool available)
        {
            if (TalkAvailable == available) return;
            TalkAvailable = available;
            FlowTrace.Step("HudKit", "talk available -> " + available);
            TalkChanged?.Invoke();
        }
    }
}
