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
        // FeatureFlags.Raid AND barracks built. ⚠ WO-1008 (2026-08-16) DELETED the
        // old third clause ">=1 deployable troop": an empty army used to HIDE the
        // face, and the owner reported "I do not see a way to start a raid" with a
        // Barracks standing. Troop count now picks a DIM REASON on a VISIBLE face
        // (HudActionBarModel.RaidDimReason), alongside RaidEntryGate.ArmyStatus.Ready
        // (the WO-820 full-army DIM gate). The only hide reasons left are "no
        // barracks" and "flag off".

        /// <summary>True while the player can raid at all (Village-published).
        /// Defaults TRUE so headless / pre-publish scenes never hide the raid
        /// door (the RaidEntryGate.ArmyStatus never-false-block precedent).</summary>
        public static bool RaidCapable { get; private set; } = true;

        // ── WO-1357: WHY the raid door is shut ───────────────────────────────
        // Owner ruling 2026-09-03, verbatim: "Raid button under journey should fail
        // gracefully, it works great if there is a barracks but should show locked if
        // doesnt have one yet or its destroyed".
        //
        // ⛔ THIS ADDS A REASON, NOT A SECOND RULE. RaidCapable is still THE one
        // predicate and its boundary is UNCHANGED by WO-1357 — the bar face and the
        // Journey card now read the SAME bool, and the reason below only supplies the
        // words. Never write a second barracks check on a surface: two checks drift,
        // and the drift IS the defect this ticket closes (the Journey card carried
        // `Available = () => true` while the bar honoured RaidCapable).

        /// <summary>WHY <see cref="RaidCapable"/> is false. <see cref="RaidLockReason.None"/> when open.</summary>
        public enum RaidLockReason
        {
            /// <summary>Not locked — the raid door is open.</summary>
            None = 0,
            /// <summary>FeatureFlags.Raid is off in this build.</summary>
            FlagOff = 1,
            /// <summary>No Barracks has ever stood on this save — build the first one.</summary>
            NoBarracks = 2,
            /// <summary>A Barracks stood on this save and is gone (destroyed = lost, WO-753) — rebuild it.</summary>
            BarracksLost = 3,
        }

        /// <summary>Latest published lock reason (Village-published, WO-1357).</summary>
        public static RaidLockReason RaidLock { get; private set; } = RaidLockReason.None;

        /// <summary>
        /// The ONE owner of the player-facing lock copy, so the Journey card, any future
        /// surface and the regression oracle all read identical words.
        /// ASCII-only (mobile font-atlas law) and it always says WHAT TO DO, never just
        /// "Locked" — the owner is red/green colourblind, so the tell has to be words and
        /// the words have to be actionable. "No Barracks" and "lost your Barracks" are
        /// DIFFERENT player situations with different remedies; never collapse them.
        /// </summary>
        public static string RaidLockCopy(RaidLockReason reason)
        {
            switch (reason)
            {
                case RaidLockReason.FlagOff: return "Raids are turned off in this build";
                case RaidLockReason.NoBarracks: return "Build a Barracks to raid";
                case RaidLockReason.BarracksLost: return "Rebuild your lost Barracks to raid";
                default: return null;
            }
        }

        /// <summary>Raised when <see cref="RaidCapable"/> OR <see cref="RaidLock"/> changes.</summary>
        public static event Action RaidCapableChanged;

        /// <summary>
        /// Producer-only (Village RaidCapabilityHudBridge). The event fires on a
        /// REASON-only change too (NoBarracks -> BarracksLost never flips the bool, but the
        /// card copy must repaint) — an early-return on the bool alone would strand the
        /// wrong sentence on screen.
        /// </summary>
        public static void SetRaidCapable(bool capable, RaidLockReason reason = RaidLockReason.None)
        {
            if (capable) reason = RaidLockReason.None;   // an open door has no reason to give
            if (RaidCapable == capable && RaidLock == reason) return;
            RaidCapable = capable;
            RaidLock = reason;
            FlowTrace.Step("HudKit", "raid capable -> " + capable +
                           (capable ? "" : " (locked: " + reason + " -> \"" + RaidLockCopy(reason) + "\")"));
            RaidCapableChanged?.Invoke();
        }
    }
}
