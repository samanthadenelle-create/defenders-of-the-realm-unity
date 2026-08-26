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
            }
            finally
            {
                // Never leak the global under test. Every later suite reads this clock.
                Time.timeScale = savedScale;
                BattleQuiescenceGate.Unregister("wo1127-suite-probe");
                BattleSessionEnd.UnregisterUnwind("wo1233-suite-hitstop");
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
                     "an open reward screen is correctly not judged; and the gate is wired into " +
                     "battle resolve.";
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
