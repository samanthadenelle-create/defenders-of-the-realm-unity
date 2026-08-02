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

        /// <summary>Drop one enemy's pursuit pulse (death / despawn). Other live pursuers
        /// keep <see cref="PursuitActive"/> true so combat HUD stays up until the last
        /// threat is gone.</summary>
        public static void RevokePursuit(int key)
        {
            for (int i = _pursuitCount - 1; i >= 0; i--)
            {
                if (_pursuitKeys[i] != key) continue;
                _pursuitCount--;
                _pursuitKeys[i] = _pursuitKeys[_pursuitCount];
                _pursuitAt[i] = _pursuitAt[_pursuitCount];
                FlowTrace.Step("HudKit", $"pursuit revoked (key={key}, live={_pursuitCount})");
                return;
            }
        }

        /// <summary>Clears all pursuit pulses (hub return / combat end — peaceful HUD).</summary>
        public static void ClearPursuits()
        {
            if (_pursuitCount == 0) return;
            _pursuitCount = 0;
            FlowTrace.Step("HudKit", "pursuit cleared (posture -> peaceful)");
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

        // ── Raid capability (WO-835 — the SetTalkAvailable mirror pattern) ───
        // Village-side RaidCapabilityHudBridge publishes "the player CAN raid":
        // FeatureFlags.Raid AND barracks built AND >=1 deployable troop. The
        // HudActionBarModel reads it to pack the Raids face in/out (hide, not
        // dim — WO-835 §3d owner default). Distinct from RaidEntryGate.
        // ArmyStatus.Ready (the WO-820 full-army DIM gate, which still applies
        // to a VISIBLE Raids face).

        /// <summary>True while the player can raid at all (Village-published).
        /// Defaults TRUE so headless / pre-publish scenes never hide the raid
        /// door (the RaidEntryGate.ArmyStatus never-false-block precedent).</summary>
        public static bool RaidCapable { get; private set; } = true;

        /// <summary>Raised when <see cref="RaidCapable"/> changes value.</summary>
        public static event Action RaidCapableChanged;

        /// <summary>Producer-only (Village RaidCapabilityHudBridge).</summary>
        public static void SetRaidCapable(bool capable)
        {
            if (RaidCapable == capable) return;
            RaidCapable = capable;
            FlowTrace.Step("HudKit", "raid capable -> " + capable);
            RaidCapableChanged?.Invoke();
        }
    }
}
