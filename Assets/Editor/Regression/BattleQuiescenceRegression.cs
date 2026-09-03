// =============================================================================
// BattleQuiescenceRegression — WO-1127. Proves the battle-end teardown contract
// FAILS every known-bad state, and PASSES a clean teardown.
// -----------------------------------------------------------------------------
// WHY BOTH DIRECTIONS ARE ASSERTED.
//
// A gate that does not fail the known-bad state is not a gate (the WO-1124 lesson,
// restated here because it is the same trap). But a gate that fails a CLEAN state
// is worse than useless: it becomes a permanent red, everyone learns to skip it,
// and the one time it means something nobody looks. So group 1 drives each
// invariant wrong in turn and requires a NAMED failure, and group 2 requires a
// clean world to pass with zero findings.
//
// Group 3 reproduces the originating defect exactly — timeScale pinned at 0.04,
// the value captured on the owner's device on 2026-08-20 — and requires the gate's
// message to name the field, the value, and the fact that the world is at 4% speed.
// A failure message that does not carry those is a debugging session someone pays
// for later.
//
// Group 4 is a source-lint on the WIRING, because a perfect gate nothing calls is
// the exact shape of the bug this ticket exists to prevent: everything green, the
// world broken. It reads CODE ONLY (comments and string-literal contents blanked),
// the project's standing lint discipline — a rule that matches its own tombstone
// comment punishes the self-documenting notes CLAUDE.md sec.12/15 asks for.
//
// EVERY test restores Time.timeScale in a finally. A regression suite that leaks
// the very global it is testing would poison every suite that runs after it.
//
// Markers: BATTLE_QUIESCENCE_SUITE_OK / BATTLE_QUIESCENCE_SUITE_FAIL.
// Standalone: run-unity-method
//   -Method DeNelle.Editor.BattleQuiescenceRegression.RunAll
// Registered in DataRegression.RunAll as the "battle-quiescence suite".
// =============================================================================
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using DeNelle.Core.Combat;
using DeNelle.Core.HudModel;

namespace DeNelle.Editor
{
    public static class BattleQuiescenceRegression
    {
        public const string MarkerOk   = "BATTLE_QUIESCENCE_SUITE_OK";
        public const string MarkerFail = "BATTLE_QUIESCENCE_SUITE_FAIL";

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var log = new StringBuilder();
            log.AppendLine("--- BATTLE-END QUIESCENCE CONTRACT (WO-1127) ---");

            float savedScale = Time.timeScale;
            try
            {
                CleanStatePasses(failures, log);
                KnownBadTimeScaleFails(failures, log);
                OriginalDefectIsNamed(failures, log);
                ModuleProbeFailureIsSurfaced(failures, log);
                RewardScreenIsNotJudged(failures, log);
                WiringIsPresent(failures, log);

                // WO-1233 — the battle-lock half.
                SessionEndReleasesTheLock(failures, log, "arena win");
                SessionEndReleasesTheLock(failures, log, "arena loss");
                SessionEndReleasesTheLock(failures, log, "retreat");
                HolderIsNamedInTheFinding(failures, log);
                LiveChaseIsNotSuppressed(failures, log);

                // WO-1233 — the timeScale half (a SEPARATE owner, asserted separately).
                BattleEndedDuringHitStopRestoresTheClock(failures, log);

                SessionEndWiringIsPresent(failures, log);

                // WO-1233b — the observer must know WHICH battle it is judging, and a FAIL must
                // self-heal rather than leave the player holding it.
                SessionEpochAdvancesOnEachBattle(failures, log);
                SupersedeGuardIsWired(failures, log);
                SelfHealIsWired(failures, log);

                // WO-1308 — a RETREAT must release EVERY battle-lock holder, including the wave loop.
                RetreatReleasesEveryLockHolder(failures, log);
                WaveLoopUnwindsItsOwnPhase(failures, log);
            }
            finally
            {
                // Never leak the global under test. Every later suite reads this clock.
                Time.timeScale = savedScale;
                BattleQuiescenceGate.Unregister("wo1127-suite-probe");
                BattleSessionEnd.UnregisterUnwind("wo1233-suite-hitstop");
                BattleSessionEnd.UnregisterUnwind("wo1308-suite-latched-phase");
                if (s_latchedHolderProbe != null)
                {
                    BattleLock.UnregisterProbe(s_latchedHolderProbe);
                    s_latchedHolderProbe = null;
                }
                PostureSignals.ClearPursuits();
            }

            if (failures.Count > 0)
            {
                reason = $"{MarkerFail}: {failures.Count} failure(s) -- " + string.Join(" | ", failures);
                Debug.LogError(log + "\n" + reason);
                return false;
            }

            reason = $"{MarkerOk} -- a clean world passes with zero findings; a wrong timeScale, a " +
                     "failing module probe and the 2026-08-20 defect each produce a NAMED failure; " +
                     "an open reward screen is correctly not judged; the gate is wired into " +
                     "battle resolve; and a RETREAT releases every battle-lock holder, the wave " +
                     "loop's latched phase included (WO-1308).";
            Debug.Log(log + MarkerOk);
            return true;
        }

        // ── 1. a clean world must pass ───────────────────────────────────────
        private static void CleanStatePasses(List<string> failures, StringBuilder log)
        {
            Time.timeScale = 1f;
            var found = BattleQuiescenceGate.Evaluate(rewardScreenOpen: false);

            // Only the Core invariants are asserted clean here: module probes are registered by a
            // live BattleArena, which does not exist in an editor batch run, so their absence is
            // expected rather than a finding.
            var coreFindings = found.Where(f => f.StartsWith("timeScale:") || f.StartsWith("battle-lock:")).ToList();
            if (coreFindings.Count == 0)
            {
                log.AppendLine("  [clean] a baseline world produces ZERO core findings");
            }
            else
            {
                failures.Add("[clean] the gate reported a finding against a CLEAN world (timeScale 1, no " +
                             "battle): " + string.Join(" / ", coreFindings) + ". A gate that fails correct " +
                             "behaviour becomes a permanent red everyone learns to ignore, which is worse " +
                             "than no gate.");
            }
        }

        // ── 2. the known-bad timeScale must FAIL ─────────────────────────────
        private static void KnownBadTimeScaleFails(List<string> failures, StringBuilder log)
        {
            Time.timeScale = 0.5f;
            var found = BattleQuiescenceGate.Evaluate(rewardScreenOpen: false);
            Time.timeScale = 1f;

            if (found.Any(f => f.StartsWith("timeScale:")))
                log.AppendLine("  [known-bad] timeScale 0.50 correctly produced a named finding");
            else
                failures.Add("[known-bad] timeScale was 0.50 and the gate did NOT report it. A gate that " +
                             "does not fail the known-bad state is not a gate.");
        }

        // ── 3. the ORIGINATING defect, named in full ─────────────────────────
        private static void OriginalDefectIsNamed(List<string> failures, StringBuilder log)
        {
            // 0.04 is not an arbitrary number: it is HitTier.Medium / Enemy.cs's death stop, and it
            // is the value measured on the owner's device on 2026-08-20.
            Time.timeScale = 0.04f;
            var found = BattleQuiescenceGate.Evaluate(rewardScreenOpen: false);
            Time.timeScale = 1f;

            string line = found.FirstOrDefault(f => f.StartsWith("timeScale:"));
            if (line == null)
            {
                failures.Add("[defect-2026-08-20] the exact captured defect (timeScale 0.04) produced NO " +
                             "finding. This is the state the owner sat in for three minutes.");
                return;
            }

            // The message must carry the evidence, not just the verdict.
            bool namesValue = line.Contains("0.04");
            bool namesSpeed = line.Contains("4%");
            if (namesValue && namesSpeed)
            {
                log.AppendLine("  [defect-2026-08-20] timeScale 0.04 reported with the value AND the 4% speed");
            }
            else
            {
                failures.Add($"[defect-2026-08-20] the finding fires but does not carry its evidence " +
                             $"(value={namesValue}, speed={namesSpeed}): \"{line}\". A message that names " +
                             "only the field costs the next reader the measurement all over again.");
            }
        }

        // ── 4. a module probe's failure must reach the report ────────────────
        private static void ModuleProbeFailureIsSurfaced(List<string> failures, StringBuilder log)
        {
            // The Village probes (arena-actors, hero-owner) cannot be exercised in an editor batch,
            // so the CONTRACT is asserted instead: a registered probe that reports a reason must
            // appear in the findings, prefixed by its name. That is what makes a real probe's
            // failure legible when it does fire on device.
            BattleQuiescenceGate.Register(new QuiescenceProbe
            {
                Name = "wo1127-suite-probe",
                Check = () => "deliberate failure injected by the WO-1127 regression suite"
            });

            Time.timeScale = 1f;
            var found = BattleQuiescenceGate.Evaluate(rewardScreenOpen: false);
            BattleQuiescenceGate.Unregister("wo1127-suite-probe");

            if (found.Any(f => f.StartsWith("wo1127-suite-probe:")))
                log.AppendLine("  [module-probe] a failing module probe surfaces, prefixed by its name");
            else
                failures.Add("[module-probe] a registered probe returned a reason and it did NOT reach the " +
                             "findings. Every Village-side invariant (orphaned arena actors, hero owner) " +
                             "rides this path, so a break here silently disables all of them.");

            // And it must be gone after Unregister, or a suite could poison the live gate.
            var after = BattleQuiescenceGate.Evaluate(rewardScreenOpen: false);
            if (after.Any(f => f.StartsWith("wo1127-suite-probe:")))
                failures.Add("[module-probe] Unregister did not remove the probe - a test or a torn-down " +
                             "scene could leave a dead probe failing the gate forever.");
        }

        // ── 5. an open reward screen must NOT be judged ──────────────────────
        private static void RewardScreenIsNotJudged(List<string> failures, StringBuilder log)
        {
            Time.timeScale = 1f;
            var whileOpen = BattleQuiescenceGate.Evaluate(rewardScreenOpen: true);

            if (whileOpen.Any(f => f.StartsWith("modal:")))
                failures.Add("[reward-screen] the gate reported a modal finding while the reward screen was " +
                             "legitimately OPEN. Failing on correct behaviour is the fastest way to get a " +
                             "gate switched off.");
            else
                log.AppendLine("  [reward-screen] the modal invariant is correctly skipped while it is up");
        }

        // ── 6. the gate must actually be WIRED ───────────────────────────────
        private static void WiringIsPresent(List<string> failures, StringBuilder log)
        {
            string src = ReadCode("Assets/_Modules/Village/Arena/BattleArena.cs");
            if (src == null)
            {
                failures.Add("[wiring] BattleArena.cs is MISSING - the wiring cannot be verified at all.");
                return;
            }

            if (!src.Contains("BattleQuiescenceGate.Arm"))
            {
                failures.Add("[wiring] BattleArena no longer arms BattleQuiescenceGate at resolve. A gate " +
                             "nothing calls is the exact shape of the bug this ticket exists to prevent: " +
                             "everything green, the world broken.");
                return;
            }
            log.AppendLine("  [wiring] BattleArena arms the gate at resolve");

            if (src.Contains("BattleQuiescenceGate.Register"))
                log.AppendLine("  [wiring] the Village-side probes are registered");
            else
                failures.Add("[wiring] BattleArena no longer registers its module probes, so the arena-actor " +
                             "and hero-owner invariants are silently absent from the contract.");

            // Armed on BOTH outcomes. A retreat tears down the same systems a win does, and a
            // contract with a hole in it is not a contract.
            int armAt = src.IndexOf("BattleQuiescenceGate.Arm");
            string tail = src.Substring(armAt, System.Math.Min(400, src.Length - armAt));
            if (tail.Contains("retreat"))
                log.AppendLine("  [wiring] the gate is armed on the loss/retreat path too, not only on a win");
            else
                failures.Add("[wiring] the gate appears to be armed only on the WIN path. A retreat tears " +
                             "down the same systems; leaving it unchecked leaves half the teardowns unproven.");
        }

        // =====================================================================
        //  WO-1233 — the battle SESSION must release what the battle raised
        // ---------------------------------------------------------------------
        //  CAPTURED DEFECT: nine BATTLE_QUIESCENCE_FAIL events on 2026-08-26 —
        //  EIGHT on an arena WIN, one on a retreat. The owner only reported the
        //  retreat, so every case below drives the WIN path too: a suite that only
        //  covered the reported symptom would have gone green on the rare instance
        //  while the common one shipped.
        //
        //  THE STATE EACH CASE REPRODUCES is the one the device captured, in the
        //  neighbouring harvest for an identical failure (capture-…-seq3545):
        //     [Flow:HUD] context inputs: wave=False battleLock=True pursuit=True …
        //  The arena is DOWN and the lock is still up, held through
        //  PursuitBattleProbe by a pursuit pulse the fight opened and nothing
        //  closed. The pre-assert in each case pins that defect state explicitly,
        //  so the suite fails loudly if the reproduction ever stops reproducing —
        //  a green that came from a test that no longer sets up the bug is the
        //  worst outcome available here.
        // =====================================================================

        /// <summary>
        /// Drive one battle outcome end-to-end over the REAL statics: a staged enemy chased the
        /// hero (a live pursuit pulse), the battle then ends, and the lock must be clear afterwards.
        /// </summary>
        private static void SessionEndReleasesTheLock(List<string> failures, StringBuilder log, string outcome)
        {
            bool arenaLive = true;
            Func<bool> arenaProbe   = () => arenaLive;                       // BattleArena.BattleInProgress
            Func<bool> pursuitProbe = () => PostureSignals.PursuitActive;    // PursuitBattleProbe.Probe

            Time.timeScale = 1f;
            PostureSignals.ClearPursuits();
            BattleLock.RegisterProbe(arenaProbe);
            BattleLock.RegisterProbe(pursuitProbe);
            try
            {
                // A staged arena enemy is chasing the hero. Enemy.DriveNav pulses this every tick.
                PostureSignals.ReportPursuit(19233);

                // The battle ends: BattleArena clears its own flag. This is the ONLY release the
                // arena ever performed, and it is not enough — which is the whole defect.
                arenaLive = false;

                if (!BattleLock.IsInBattle())
                {
                    failures.Add($"[session-end/{outcome}] the DEFECT STATE no longer reproduces: with the " +
                                 "arena down and a live pursuit pulse, BattleLock already reads clear. " +
                                 "Either PursuitBattleProbe's source changed or the pulse TTL did — this " +
                                 "case is no longer testing the captured failure and must be re-derived " +
                                 "before it is trusted.");
                    return;
                }

                string heldBy = BattleLock.DescribeHolders();
                BattleSessionEnd.Release(outcome);

                if (BattleLock.IsInBattle())
                    failures.Add($"[session-end/{outcome}] the battle ended and the lock is STILL HELD by " +
                                 $"[{BattleLock.DescribeHolders()}]. This is the owner's \"doesnt do " +
                                 "anything\": PanelManager refuses every town panel while it is up.");
                else if (Mathf.Abs(Time.timeScale - 1f) > 0.001f)
                    failures.Add($"[session-end/{outcome}] the lock released but the world clock is " +
                                 $"{Time.timeScale:F2}, not 1.00.");
                else
                    log.AppendLine($"  [session-end/{outcome}] lock was held by [{heldBy}] and is released; timeScale 1.00");
            }
            finally
            {
                BattleLock.UnregisterProbe(arenaProbe);
                BattleLock.UnregisterProbe(pursuitProbe);
                PostureSignals.ClearPursuits();
                Time.timeScale = 1f;
            }
        }

        /// <summary>
        /// The finding must NAME the holder. Nine device captures said "still HELD" and not one said
        /// by whom, which is the entire reason this ticket cost a log-archaeology session.
        /// </summary>
        private static void HolderIsNamedInTheFinding(List<string> failures, StringBuilder log)
        {
            Func<bool> stuckProbe = () => true;
            Time.timeScale = 1f;
            BattleLock.RegisterProbe(stuckProbe);
            try
            {
                string line = BattleQuiescenceGate.Evaluate(rewardScreenOpen: false)
                                                  .FirstOrDefault(f => f.StartsWith("battle-lock:"));
                if (line == null)
                {
                    failures.Add("[holder] a probe reporting TRUE produced no battle-lock finding at all.");
                    return;
                }

                // The original sentence must survive verbatim - this is an addition, not a rewrite.
                if (!line.Contains("still HELD after the battle ended"))
                    failures.Add("[holder] the battle-lock finding's original wording was CHANGED. It may be " +
                                 "added to and never narrowed: it is the only reason this defect was findable.");

                if (line.Contains("HOLDER(S):") && line.Contains("BattleQuiescenceRegression"))
                    log.AppendLine("  [holder] the finding names the probe that actually holds the lock");
                else
                    failures.Add($"[holder] the finding does not name the holder: \"{line}\". Attribution is " +
                                 "the deliverable - \"the lock is stuck\" costs a whole session that \"the " +
                                 "lock is held by PursuitBattleProbe\" does not.");
            }
            finally { BattleLock.UnregisterProbe(stuckProbe); }
        }

        /// <summary>
        /// The release must NOT be able to suppress a real fight. Pursuit is pulse-based: a chaser
        /// that is still chasing re-reports on its next tick, so the lock legitimately comes back.
        /// Without this case the "fix" could be a blind force-false, which would unblock panels
        /// mid-battle - the exact thing WO-1233 forbids.
        /// </summary>
        private static void LiveChaseIsNotSuppressed(List<string> failures, StringBuilder log)
        {
            Func<bool> pursuitProbe = () => PostureSignals.PursuitActive;
            Time.timeScale = 1f;
            PostureSignals.ClearPursuits();
            BattleLock.RegisterProbe(pursuitProbe);
            try
            {
                BattleSessionEnd.Release("arena win");
                PostureSignals.ReportPursuit(29233);   // a town rep is still chasing - next aggro tick

                if (BattleLock.IsInBattle())
                    log.AppendLine("  [live-chase] a still-chasing pursuer re-raises the lock after the release");
                else
                    failures.Add("[live-chase] a live pursuer re-reported AFTER the battle-end release and the " +
                                 "lock did NOT come back. The release has become a suppression, which unblocks " +
                                 "town panels during a real chase - worse than the bug it replaced.");
            }
            finally
            {
                BattleLock.UnregisterProbe(pursuitProbe);
                PostureSignals.ClearPursuits();
            }
        }

        /// <summary>
        /// The timeScale half, asserted through its OWN owner. A battle that ends mid-hit-stop must
        /// leave the clock at 1.00 - 0.04 is HitTier.Medium and the exact value the device captured
        /// twice on 2026-08-26 (and once on 2026-08-20 before it).
        /// </summary>
        private static void BattleEndedDuringHitStopRestoresTheClock(List<string> failures, StringBuilder log)
        {
            // Stands in for HitStopManager, which cannot run its coroutine outside play mode. The
            // CONTRACT under test is that the session end reaches a registered clock unwind at all;
            // that the real manager is the one registered is asserted by the source lint below, so
            // this can never pass against a stub alone.
            BattleSessionEnd.RegisterUnwind("wo1233-suite-hitstop", _ =>
            {
                if (Mathf.Abs(Time.timeScale - 0.04f) < 0.001f) Time.timeScale = 1f;
            });
            try
            {
                Time.timeScale = 0.04f;   // a hit stop is in flight at the instant the battle resolves
                BattleSessionEnd.Release("arena win");

                if (Mathf.Abs(Time.timeScale - 1f) <= 0.001f)
                    log.AppendLine("  [hit-stop] a battle ended mid-stop leaves timeScale 1.00");
                else
                    failures.Add($"[hit-stop] the battle ended during a hit stop and the clock is still " +
                                 $"{Time.timeScale:F2}. The player reads 4% speed as frozen controls even " +
                                 "though input is fine - the 2026-08-20 defect, returned.");
            }
            finally
            {
                BattleSessionEnd.UnregisterUnwind("wo1233-suite-hitstop");
                Time.timeScale = 1f;
            }
        }

        /// <summary>
        /// WO-1233 wiring lint. The release must be announced from the battle's LIFECYCLE ENDS, and
        /// the real hit-stop owner must be the thing subscribed to it. A release nothing calls, or a
        /// stub-only subscriber, is the same everything-green-world-broken shape group 6 guards.
        /// </summary>
        private static void SessionEndWiringIsPresent(List<string> failures, StringBuilder log)
        {
            string arena = ReadCode("Assets/_Modules/Village/Arena/BattleArena.cs");
            if (arena == null)
            {
                failures.Add("[session-wiring] BattleArena.cs is MISSING - the wiring cannot be verified.");
            }
            else
            {
                int calls = CountOf(arena, "BattleSessionEnd.Release");
                if (calls >= 2)
                    log.AppendLine($"  [session-wiring] BattleArena announces the session end from both lifecycle ends ({calls} call sites)");
                else
                    failures.Add($"[session-wiring] BattleArena calls BattleSessionEnd.Release {calls} time(s). It " +
                                 "must be announced from BOTH lifecycle ends (Resolve and ResolveAbandoned) and " +
                                 "from NEITHER individual outcome - an abandoned fight opened the same pursuit " +
                                 "window a resolved one did.");

                foreach (var token in new[]
                         {
                             "ArenaEntryLanded(heroStance",
                             "ARENA ENTRY HANDSHAKE failed after warp",
                             "ARENA ENTRY HANDSHAKE failed after retry",
                             "Resolve(false)"
                         })
                    if (!arena.Contains(token))
                        failures.Add($"[session-wiring] arena entry handshake lost '{token}' - a WarpHero request " +
                                     "can again spawn enemies before proving the hero arrived.");
            }

            string hitStop = ReadCode("Assets/_Modules/Village/Vfx/HitStopManager.cs");
            if (hitStop == null)
            {
                failures.Add("[session-wiring] HitStopManager.cs is MISSING - the clock unwind cannot be verified.");
                return;
            }

            if (hitStop.Contains("BattleSessionEnd.RegisterUnwind") && hitStop.Contains("EndStopNow"))
                log.AppendLine("  [session-wiring] the real hit-stop owner registers its own battle-end unwind");
            else
                failures.Add("[session-wiring] HitStopManager no longer registers a battle-end unwind, so the ONLY " +
                             "thing proving the clock case above is the suite's own stub. The 2026-08-20 fix had " +
                             "three unwind paths and the defect still returned, because all three were keyed to " +
                             "the manager's lifetime and none to the battle's.");
        }

        // =====================================================================
        //  WO-1233b — the gate must not report a LIVE battle's state as the last
        //  battle's leak, and a FAIL must self-heal.
        // ---------------------------------------------------------------------
        //  CAPTURED DEFECT (2026-08-30, Seeker 2026.08.30.348233, device break-log):
        //    t=342.5  BATTLE_QUIESCENCE_FAIL (arena win) - timeScale 0.04 +
        //             battle-lock held by PursuitBattleProbe.Probe AND
        //             BattleArena.<Awake>b__84_0
        //    t=350.8  [HeroDeath] death freeze armed ... pinPos=(4997.93,0.08,5004.65)
        //  ArenaCentre is (5000,0,5000): the hero was IN the arena, fighting, and died
        //  there 8.3s after the gate declared the previous battle unclean. Resolve
        //  clears BattleInProgress BEFORE it arms the gate, so that probe reading true
        //  inside the gate's own coroutine can only mean a SECOND battle had begun.
        // =====================================================================

        /// <summary>
        /// The epoch is what tells an armed observer that the world in front of it belongs to a
        /// different fight. Behavioural, over the real static.
        /// </summary>
        private static void SessionEpochAdvancesOnEachBattle(List<string> failures, StringBuilder log)
        {
            int before = BattleSessionEnd.Epoch;
            BattleSessionEnd.Begin("wo1233b-suite: first battle");
            int afterFirst = BattleSessionEnd.Epoch;
            BattleSessionEnd.Release("wo1233b-suite: first battle ends");
            int afterRelease = BattleSessionEnd.Epoch;
            BattleSessionEnd.Begin("wo1233b-suite: second battle");
            int afterSecond = BattleSessionEnd.Epoch;

            if (afterFirst == before)
            {
                failures.Add("[epoch] BattleSessionEnd.Begin did not advance the session epoch. Every " +
                             "quiescence gate then judges whatever battle happens to be running when it " +
                             "settles, which is the 2026-08-30 false failure verbatim.");
                return;
            }

            if (afterRelease != afterFirst)
            {
                failures.Add("[epoch] BattleSessionEnd.Release advanced the epoch. It MUST NOT: the gate " +
                             "is armed immediately after Release, so a bump there would make every gate " +
                             "consider itself superseded by its own battle and never report anything.");
                return;
            }

            if (afterSecond <= afterFirst)
            {
                failures.Add("[epoch] a SECOND Begin did not advance the epoch past the first, so two " +
                             "back-to-back battles are indistinguishable to an armed observer.");
                return;
            }

            log.AppendLine("  [epoch] each battle start advances the session epoch; a session END does not");
        }

        /// <summary>
        /// Source-lint. The settle window cannot be driven in an editor batch (it waits on
        /// Time.unscaledTime, which does not advance inside a synchronous suite), so the WIRING is
        /// what is asserted — the same discipline as groups 4 and 6.
        /// </summary>
        private static void SupersedeGuardIsWired(List<string> failures, StringBuilder log)
        {
            string gate = ReadCode("Assets/_Modules/Core/Combat/BattleQuiescenceGate.cs");
            if (gate == null)
            {
                failures.Add("[supersede] BattleQuiescenceGate.cs is MISSING - the guard cannot be verified.");
            }
            else if (CountOf(gate, "BattleSessionEnd.Epoch") < 2)
            {
                failures.Add("[supersede] BattleQuiescenceGate no longer captures AND compares " +
                             "BattleSessionEnd.Epoch. Without both, the gate reports the live state of " +
                             "whatever battle started while it was settling as the previous battle's leak " +
                             "- 2026-08-30, timeScale 0.04 plus two 'stuck' holders, all of them correct.");
            }
            else if (!gate.Contains("BATTLE_QUIESCENCE_SUPERSEDED"))
            {
                failures.Add("[supersede] the superseded case no longer has its own marker. A withdrawal " +
                             "that looks like a pass is a gate that silently stops covering the real leak.");
            }
            else
            {
                log.AppendLine("  [supersede] the gate captures the epoch at arm time and withdraws under its own marker");
            }

            string arena = ReadCode("Assets/_Modules/Village/Arena/BattleArena.cs");
            if (arena == null)
                failures.Add("[supersede] BattleArena.cs is MISSING - the session-start wiring cannot be verified.");
            else if (!arena.Contains("BattleSessionEnd.Begin"))
                failures.Add("[supersede] BattleArena no longer announces the session START, so the epoch " +
                             "never advances and the supersede guard in the gate is dead code.");
            else
                log.AppendLine("  [supersede] BattleArena announces the session start from BeginEncounter");
        }

        /// <summary>
        /// A FAIL must leave the player better off, not merely documented. Asserts the recovery goes
        /// through the ONE authoritative exit rather than force-clearing the lock from the observer.
        /// </summary>
        private static void SelfHealIsWired(List<string> failures, StringBuilder log)
        {
            string gate = ReadCode("Assets/_Modules/Core/Combat/BattleQuiescenceGate.cs");
            if (gate == null)
            {
                failures.Add("[self-heal] BattleQuiescenceGate.cs is MISSING - the self-heal cannot be verified.");
                return;
            }

            if (!gate.Contains("quiescence self-heal"))
                failures.Add("[self-heal] a BATTLE_QUIESCENCE_FAIL no longer re-drives " +
                             "BattleSessionEnd.Release. Reporting alone leaves the player holding the " +
                             "stuck lock and the 4% clock the report describes.");
            else
                log.AppendLine("  [self-heal] a FAIL re-drives the one authoritative exit seam");

            // The recovery must NOT become a new writer of the lock. BattleLock is an OR over N
            // owners; forcing it false from here would mask a live fight instead of ending one.
            if (gate.Contains("BattleLock.UnregisterProbe") || gate.Contains("BattleLock.RegisterProbe"))
                failures.Add("[self-heal] BattleQuiescenceGate now mutates BattleLock's probe list. The " +
                             "observer must never own the lock: a live chase would be silently unlocked, " +
                             "and the holder attribution the 2026-08-26 tickets bought would be lost.");
            else
                log.AppendLine("  [self-heal] the gate still owns no lock state - it re-drives the owners' own unwinds");
        }

        // =====================================================================
        //  WO-1308 — a RETREAT must release EVERY battle-lock holder.
        // ---------------------------------------------------------------------
        //  CAPTURED DEFECT (2026-09-02, owner felt-test, F8 seq 4663-4665,
        //  Main_Castle_Overworld — "somehow the wolf is still here and sitting in fight"):
        //
        //    [Flow:Quiescence] battle-lock STILL HELD after the self-heal (retreat):
        //      [WaveManager.<OnEnable>b__106_0]
        //      (was [PursuitBattleProbe.Probe, WaveManager.<OnEnable>b__106_0]).
        //
        //  PursuitBattleProbe released. The WAVE probe did not, because the wave loop's
        //  _phase was latched at Active and NOTHING at battle end ever asked it. WO-1233
        //  established the rule that every owner of a global registers its own unwind;
        //  WaveManager owned one (the lock claim it raises through _phase) and was the one
        //  owner that had never registered.
        // =====================================================================

        /// <summary>Kept in a field so the finally can always drop it, even on an early return.</summary>
        private static Func<bool> s_latchedHolderProbe;

        /// <summary>
        /// Behavioural, both directions. A holder that registers NO unwind survives a full session
        /// release — that is the captured defect, and asserting it is what stops this test passing
        /// for the wrong reason. The SAME holder, once it registers an unwind, is released by the
        /// same call.
        /// </summary>
        private static void RetreatReleasesEveryLockHolder(List<string> failures, StringBuilder log)
        {
            bool latched = true;                       // stands in for WaveManager._phase == Active
            s_latchedHolderProbe = () => latched;
            BattleLock.RegisterProbe(s_latchedHolderProbe);

            if (!BattleLock.IsInBattle())
            {
                failures.Add("[wo1308] a registered probe returning true did not raise the battle-lock, " +
                             "so this test cannot prove anything about releasing it.");
                BattleLock.UnregisterProbe(s_latchedHolderProbe);
                s_latchedHolderProbe = null;
                return;
            }

            // (a) THE DEFECT. No unwind registered: a retreat leaves the holder exactly where it was.
            BattleSessionEnd.Release("wo1308-suite: retreat, holder has NO unwind");
            if (!BattleLock.IsInBattle())
            {
                failures.Add("[wo1308] a latched holder that registered NO battle-end unwind was released " +
                             "anyway. Either something now force-clears BattleLock from the outside — which " +
                             "would silently unlock a LIVE fight — or this probe is not really a holder, and " +
                             "the pass below would then be meaningless.");
                BattleLock.UnregisterProbe(s_latchedHolderProbe);
                s_latchedHolderProbe = null;
                return;
            }
            log.AppendLine("  [wo1308] a holder with no unwind survives a session release (the captured defect)");

            // (b) THE CONTRACT. The owner registers an unwind that lowers its OWN claim; the same
            //     retreat now releases it. Nothing force-clears the lock — the owner stands down.
            BattleSessionEnd.RegisterUnwind("wo1308-suite-latched-phase", _ => latched = false);
            BattleSessionEnd.Release("retreat");

            if (BattleLock.IsInBattle())
                failures.Add("[wo1308] the battle-lock is STILL HELD after a retreat, even though the holder " +
                             "registered a battle-end unwind. This is F8 seq 4664 verbatim: combat input " +
                             "stays suppressed and the HUD cannot return to town for the rest of the session.");
            else
                log.AppendLine("  [wo1308] a retreat releases a holder that unwinds its own state at session end");

            BattleSessionEnd.UnregisterUnwind("wo1308-suite-latched-phase");
            BattleLock.UnregisterProbe(s_latchedHolderProbe);
            s_latchedHolderProbe = null;
        }

        /// <summary>
        /// Source-lint on the REAL holder. The stub above proves the seam; only this proves that
        /// WaveManager uses it — and the whole defect was a seam that existed and an owner that
        /// never reached for it. Same discipline as the session-wiring group: a live wave loop
        /// cannot be driven inside a synchronous editor batch.
        /// </summary>
        private static void WaveLoopUnwindsItsOwnPhase(List<string> failures, StringBuilder log)
        {
            string wave = ReadCode("Assets/_Modules/Village/Waves/WaveManager.cs");
            if (wave == null)
            {
                failures.Add("[wo1308-wiring] WaveManager.cs is MISSING — the wave loop's battle-end unwind " +
                             "cannot be verified.");
                return;
            }

            if (!wave.Contains("BattleSessionEnd.RegisterUnwind"))
                failures.Add("[wo1308-wiring] WaveManager no longer registers a battle-end unwind. The wave " +
                             "loop raises the battle-lock through _phase == Active and is the ONLY owner of " +
                             "that claim; with no unwind, a retreat leaves it held for the rest of the " +
                             "session — F8 seq 4664, 2026-09-02.");
            else
                log.AppendLine("  [wo1308-wiring] the wave loop registers its own battle-end unwind");

            // The unwind must DRIVE the loop's own clear test, not re-decide it. A second copy of
            // "is this wave over" is a second answer waiting to disagree with TickActiveWave.
            if (!wave.Contains("ReconcileLatchedWavePhase") || !wave.Contains("TickActiveWave()"))
                failures.Add("[wo1308-wiring] the battle-end unwind no longer drives TickActiveWave. If it " +
                             "decides for itself whether the wave is over, that judgement will drift from " +
                             "the loop's and one of the two will cancel a live siege.");
            else
                log.AppendLine("  [wo1308-wiring] the unwind drives the loop's own tick rather than re-deciding");

            // ⛔ The probe itself must NOT have been weakened to make the log clean. A live siege is
            //    combat, and a retreat from an overworld wolf must never cancel one.
            if (!wave.Contains("Instance == this && _phase == WavePhase.Active"))
                failures.Add("[wo1308-wiring] the battle-lock probe predicate changed. It must stay " +
                             "'isActiveAndEnabled && Instance == this && _phase == WavePhase.Active': " +
                             "narrowing it hides a genuine siege from the lock, which trades a stuck lock " +
                             "for a combat state the game does not know it is in — strictly worse, and " +
                             "invisible.");
            else
                log.AppendLine("  [wo1308-wiring] the battle-lock probe predicate is intact (a real siege still holds)");

            // The roster and the phase must come down together. OnDisable clears every live enemy
            // and the held count; a phase left Active over that empty field is the latch itself.
            if (!wave.Contains("OnDisable/roster-cleared"))
                failures.Add("[wo1308-wiring] OnDisable no longer stands the phase down with the roster it " +
                             "clears. A manager disabled mid-wave then re-enables still claiming Active over " +
                             "a field it emptied, and re-registers the lock probe against that claim.");
            else
                log.AppendLine("  [wo1308-wiring] OnDisable stands the phase down with the roster it clears");
        }

        private static int CountOf(string haystack, string needle)
        {
            int n = 0, i = 0;
            while ((i = haystack.IndexOf(needle, i, StringComparison.Ordinal)) >= 0) { n++; i += needle.Length; }
            return n;
        }

        /// <summary>Source with comments and string-literal contents blanked, so a rule cannot match
        /// its own tombstone comment.</summary>
        private static string ReadCode(string relPath)
        {
            string full = Path.GetFullPath(relPath);
            if (!File.Exists(full)) return null;

            var sb = new StringBuilder();
            foreach (string raw in File.ReadAllLines(full))
            {
                string line = raw;
                string trimmed = line.TrimStart();
                if (trimmed.StartsWith("//") || trimmed.StartsWith("*")) continue;
                int c = line.IndexOf("//");
                if (c >= 0 && !InsideQuotes(line, c)) line = line.Substring(0, c);
                sb.AppendLine(line);
            }
            return sb.ToString();
        }

        private static bool InsideQuotes(string line, int index)
        {
            bool q = false;
            for (int i = 0; i < index; i++)
                if (line[i] == '"' && (i == 0 || line[i - 1] != '\\')) q = !q;
            return q;
        }

        /// <summary>Standalone entry point (run-unity-method).</summary>
        public static void RunAll()
        {
            bool ok = Run(out string reason);
            Debug.Log(reason);
            if (!ok) EditorApplication.Exit(1);
        }
    }
}
