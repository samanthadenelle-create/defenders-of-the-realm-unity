// =============================================================================
// DevClock - the DEV-ONLY wall-clock skip that lets QA jump the BUILD QUEUES
// forward without touching combat (owner ask 2026-08-04: "a speed timer for
// testing building queues ... but NOT impact the battle timer").
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.Diagnostics
//
// WHY IT IS HERE AND NOT ON TimeSource DIRECTLY
// ---------------------------------------------
// The clock seam itself is DeNelle.Village.TimeSource (WO-115 sec.3) and that is
// still the ONE place NowUnixMs() is assembled. But the live dev panel the owner
// actually reaches (Settings -> Dev tools = DeNelle.HUD.AdminOverlay) sits in the
// DeNelle.HUD assembly, which by the cross-assembly law (CLAUDE.md sec.5) may
// reference DeNelle.Core ONLY - never DeNelle.Village. AdminOverlay's existing
// workaround for that is System.Reflection, which is banned for new code here.
// So the STORAGE lives in Core (reachable by HUD, DevTools and Village alike) and
// TimeSource exposes it as TimeSource.DevSkipMs. There is exactly one backing
// field; it is NOT ServerOffsetMs.
//
// WHY NOT ServerOffsetMs
// ----------------------
// ServerOffsetMs belongs to the WO-120 backend time-sync lane (serverNow -
// deviceNow at the last handshake). Folding a dev skip into it would make a QA
// action indistinguishable from a server correction, and clearing one would clear
// the other. They are separate fields and compose additively:
//     NowUnixMs() = deviceUtcMs + ServerOffsetMs + DevSkipMs
//
// ############################################################################
// # WHAT A SKIP MOVES - READ THIS BEFORE YOU TRUST A WALLET OR A TIMER       #
// ############################################################################
// TimeSource.NowUnixMs() is read by EIGHT live consumers. A +1h skip moves ALL
// of them, not just the build queue. This is DESIRABLE for testing but must be
// deliberate - a jumped wallet is not a bug report:
//
//  1. BuildTimerService / ObsidianQueueEngine  (Builder / Train / Research)
//     -> THE TARGET. Every running job whose FinishMs is now <= the skewed now
//        completes on the next sweep; pending jobs cascade into the freed slots.
//        +1h clears the 30s/1.5m/4.5m/13.5m/40m tiers and the 2h tier's first hour.
//  2. ObsidianQueueHud / TroopTrainingPanel    (queue readouts)
//     -> Remaining-time strings shorten to match. Display only.
//  3. OfflineClaimCoordinator.Claim            (WO-1147: the ONE offline clock read)
//     -> Computes +1h of elapsed window ONCE and fans it out to every consumer:
//        OfflineHarvestService (node/settlement/pet haul, 10h cap), EchoService
//        (the silo, 4h cap) and EchoRepairService (repair fractions, 4h cap). Each
//        clamps the SAME window with its own cap; the clock advances exactly once.
//  4. (was EchoService.ClaimOffline -- now EchoService.ApplyOfflineWindow, driven by 3)
//     -> Fills the silo by +1h of harvest rate, clamped to the silo HOUR cap (4h).
//  5. EchoService.DumpSilos                    (banking the silo)
//     -> Re-stamps GameState.LastHarvestClaimMs to the skewed now, via
//        OfflineClaimCoordinator.StampClock (the single-owner write path).
//  6. ResourceCollector.CatchUpAway            (WO-859 collector offline accrual)
//     -> PAYS +1h into each collector's pending pool, clamped by its capacity cap.
//  7. TroopRecoveryService -> ArmyStorage.AdvanceRecovery
//     -> HEALS WOUNDED TROOPS by +1h. This is out-of-battle army recovery between
//        raids (Wounded is only ever SET at raid exit, and RecoveryRemaining is read
//        only by GetDeployable) - it changes roster AVAILABILITY, never the pacing
//        of a battle in progress.
//  8. This file's own FlowTrace line, so a capture always shows the skew.
//
// COMBAT IS NOT ON THIS LIST, AND THAT IS THE WHOLE POINT.
// Verified at source 2026-08-04: WaveManager (_countdownRemaining -= Time.deltaTime),
// RaidScoring (_elapsed += Time.deltaTime), ATBCombatManager (_currentTime +=
// Time.deltaTime), BattleController, EnemyBrain (Time.time cooldowns) and HeroHealth
// (Time.time invuln / Time.deltaTime contact ticks) contain ZERO references to
// TimeSource. They run on engine time. DevTimeSkipRegression pins that with a source
// assertion so a future refactor cannot silently make this dev tool warp battles.
// Time.timeScale is deliberately NOT touched - that WOULD speed up combat.
//
// ############################################################################
// # SAVE SAFETY - can a skip corrupt a save?  ASSESSED: NO, BUT SEE (b).     #
// ############################################################################
// (a) PERSISTED-TIMESTAMP REWIND. Four consumers persist a stamp taken from this
//     clock: GameState.LastHarvestClaimMs (OfflineHarvestService + EchoService),
//     ResourceCollector._lastAccrualMs (PlayerPrefs) and ArmyStorage
//     .LastRecoveryTickMs. Skipping +1h and then Reset()ing leaves those stamps in
//     the FUTURE relative to the restored clock. Every one of them already carries
//     an explicit anti-tamper monotonic guard - a negative delta clamps to ZERO
//     (never a negative haul, never a re-claim) and the stamp is then UNCONDITIONALLY
//     re-anchored to the current now. So the state SELF-HEALS on the very next
//     claim/tick; the only cost is that the rewound window is forfeited. Proven at
//     source: OfflineHarvestService.cs:152-154 + :176-181, EchoService.cs:271,
//     ResourceCollector.cs:~205 + :343, ArmyStorage.cs:299-303.
// (b) THE ONE REAL HAZARD - jobs ENQUEUED WHILE SKIPPED. BuildJobData.StartMs /
//     FinishMs are stamped from this clock at enqueue and persisted in the save. A
//     job started during a +1h skip gets a FinishMs an hour beyond real time; a
//     subsequent Reset() strands it for that extra real hour. It is not corruption
//     (the schema is intact and the job still completes) but it WILL look like a
//     hung queue. GUARD: Reset() FlowTrace.Warn()s when any skip is being cleared,
//     naming this risk, and the dev buttons say so. The safe order is: enqueue at
//     real time -> skip -> let it finish -> reset.
// (c) Add() refuses NEGATIVE deltas. The skip is FORWARD-ONLY, so the only backward
//     move a dev can make is the single, logged, self-healing Reset().
//
// ############################################################################
// # RELEASE SAFETY - it is IMPOSSIBLE to ship this enabled.                  #
// ############################################################################
// The mutable backing field only EXISTS under `#if UNITY_EDITOR ||
// DEVELOPMENT_BUILD`. In a shipped (non-development) player build the compiler
// emits `SkipMs => 0d` - a constant zero with no storage to write - and Add()/
// Reset() are no-op stubs. There is nothing to toggle, nothing to leave on and no
// PlayerPrefs key to flip: a release build behaves exactly as if DevSkipMs is 0.
// Belt-and-braces on top of that: the AdminOverlay buttons live inside its existing
// `#if DEVELOPMENT_BUILD || UNITY_EDITOR` block, and the DevPanelController buttons
// are in DeNelle.DevTools, whose asmdef carries the defineConstraint
// "UNITY_EDITOR || DEVELOPMENT_BUILD" (whole assembly skipped in release).
// =============================================================================
using System;

namespace DeNelle.Core.Diagnostics
{
    /// <summary>
    /// DEV-ONLY additive skew (in unix-ms) applied to <c>DeNelle.Village.TimeSource
    /// .NowUnixMs()</c> so QA can jump the Obsidian build/train/research queues
    /// forward without touching combat (combat runs on <c>Time.deltaTime</c>).
    /// <para>
    /// Compiled to a constant <c>0</c> in release player builds - the backing field
    /// does not exist there, so a shipped build cannot have a skip. See the file
    /// header for the full consumer list and the save-safety assessment.
    /// </para>
    /// </summary>
    public static class DevClock
    {
        /// <summary>FlowTrace system tag every skip line is filed under.</summary>
        public const string Tag = "DevClock";

#if UNITY_EDITOR || DEVELOPMENT_BUILD

        // The ONE mutable store. Deliberately separate from TimeSource.ServerOffsetMs
        // (WO-120 backend lane) so a dev skip and a server correction never conflate.
        private static double _skipMs;

        /// <summary>True when a dev skip can exist at all (editor / development build).</summary>
        public static bool Available => true;

        /// <summary>Accumulated dev skip in milliseconds. Always &gt;= 0 (forward-only).</summary>
        public static double SkipMs => _skipMs;

        /// <summary>
        /// Adds <paramref name="deltaMs"/> to the accumulated skip and returns the new
        /// total. Additive, so repeated taps stack. NEGATIVE deltas are refused (the
        /// skip is forward-only; <see cref="Reset"/> is the only way back) - a rewind
        /// would write past-skewed stamps into the save for no testing benefit.
        /// </summary>
        public static double Add(double deltaMs)
        {
            if (double.IsNaN(deltaMs) || double.IsInfinity(deltaMs))
            {
                FlowTrace.Warn(Tag, $"skip REFUSED - non-finite delta ({deltaMs}); skip stays {Describe(_skipMs)}.");
                return _skipMs;
            }
            if (deltaMs < 0d)
            {
                FlowTrace.Warn(Tag,
                    $"skip REFUSED - negative delta {deltaMs:0}ms; the dev skip is FORWARD-ONLY " +
                    $"(use Reset to clear). Skip stays {Describe(_skipMs)}.");
                return _skipMs;
            }

            double before = _skipMs;
            _skipMs = before + deltaMs;
            FlowTrace.Warn(Tag,
                $"WALL CLOCK SKEWED +{Describe(deltaMs)} -> total dev skip {Describe(_skipMs)} " +
                $"(was {Describe(before)}). TimeSource.NowUnixMs is now AHEAD of the device clock. " +
                "This advances the Obsidian build/train/research queues AND pays offline income " +
                "(OfflineHarvestService, EchoService silo, ResourceCollector) AND heals wounded " +
                "troops (TroopRecoveryService). Combat/waves/battle timers are UNAFFECTED - they " +
                "run on Time.deltaTime and never read TimeSource.");
            return _skipMs;
        }

        /// <summary>
        /// Clears the accumulated skip (back to a pure device clock) and returns the
        /// amount that was cleared. Logged as a Warn because it rewinds the wall clock:
        /// persisted accrual stamps self-heal (they clamp negative deltas to zero and
        /// re-anchor), but any queue job ENQUEUED while skipped keeps its skewed
        /// FinishMs and will look stalled for that much real time.
        /// </summary>
        public static double Reset()
        {
            double cleared = _skipMs;
            _skipMs = 0d;
            if (cleared > 0d)
            {
                FlowTrace.Warn(Tag,
                    $"dev skip CLEARED (-{Describe(cleared)}) - clock is the raw device clock again. " +
                    "Accrual stamps (LastHarvestClaimMs / collector / army recovery) self-heal on the " +
                    "next tick via their monotonic guards. WARNING: any queue job enqueued DURING the " +
                    $"skip kept a FinishMs up to {Describe(cleared)} in the future and will appear " +
                    "stalled for that long - re-skip or let it run.");
            }
            else
            {
                FlowTrace.Step(Tag, "dev skip reset requested - already 0, nothing to clear.");
            }
            return cleared;
        }

#else   // ---- SHIPPED (non-development) PLAYER BUILD ----------------------------
        // No backing field exists. SkipMs is a compile-time constant zero, so
        // TimeSource.NowUnixMs() is exactly (device + ServerOffsetMs) and the JIT
        // folds the term away. Add/Reset are no-ops that cannot be made to do
        // anything - there is no state for them to touch.

        /// <summary>Always false in a shipped build - a dev skip cannot exist.</summary>
        public static bool Available => false;

        /// <summary>Always 0 in a shipped build (no backing storage is compiled).</summary>
        public static double SkipMs => 0d;

        /// <summary>No-op in a shipped build. Always returns 0.</summary>
        public static double Add(double deltaMs) => 0d;

        /// <summary>No-op in a shipped build. Always returns 0.</summary>
        public static double Reset() => 0d;
#endif

        /// <summary>
        /// Human-readable rendering of a millisecond span for dev labels / traces
        /// ("none", "45s", "10m", "1h 30m"). Safe in every build configuration.
        /// </summary>
        public static string Describe(double ms)
        {
            if (ms <= 0d) return "none";
            var span = TimeSpan.FromMilliseconds(ms);
            int h = (int)span.TotalHours;
            int m = span.Minutes;
            int s = span.Seconds;
            if (h > 0) return m > 0 ? $"{h}h {m}m" : $"{h}h";
            if (m > 0) return s > 0 ? $"{m}m {s}s" : $"{m}m";
            return $"{s}s";
        }

        /// <summary>Human-readable rendering of the CURRENT accumulated skip.</summary>
        public static string DescribeCurrent() => Describe(SkipMs);
    }
}
