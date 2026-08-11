// =============================================================================
// EndStateTransitionHandoffRegression [endstate-handoff]
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression (references DeNelle.Core + DeNelle.Village).
//
// WO-969 (owner F8 seq 2315, scene Dungeon_HealersCottage). PROVEN BY CAPTURE, from
// her live Player.log, in order:
//
//   [Flow:UI] PanelManager: 'Pause' opened and verified visible (IsOpen=true).
//             DeNelle.Core.UI.PanelManager:NotifyOpened     (PanelManager.cs:181)
//             DeNelle.Core.HudModel.PostureSignals:SetEndState(bool) (PostureSignals.cs:127)
//             DeNelle.Village.UI.EndStateView:OnDestroy ()  (EndStateView.cs:1665)
//   [BREAK] error: [Flow:BattleArena] STRANDING WATCHDOG FIRED after 45s - the victory
//           panel was destroyed without firing its Continue action, so the deferred home
//           return never ran. Returning the hero anyway. [...] the watchdog is a safety
//           net, NOT the fix.
//
// THE DEFECT: the arena's ONLY route home (doMaskedReturn) was owned by EndStateVM.Primary,
// i.e. by a GameObject any modal may destroy. Opening Pause ran
// PanelManager.NotifyOpened -> previous.Close() -> EndStateView.CloseFromArbiter ->
// Destroy(gameObject), and the transition died with the view. 45 seconds of stranding.
//
// THE FIX SHAPE (c): the transition is made INDEPENDENT of the panel's lifetime. The view
// still refuses to fire the player's CHOICE when it is displaced (correct - a displaced
// end-state must never silently continue/respawn); it now HANDS THE TRANSITION BACK to its
// owner instead of taking it to the grave.
//
// WHAT THIS SUITE PROVES HEADLESSLY, AND WHAT IT CANNOT:
//
//   (a) LIVE MODEL CONTRACT - a real EndStateVM built by the real
//       EndStateVM.FromBattleVictory factory: the hand-back runs exactly once, never
//       invokes Primary, is a permanent no-op once Primary has fired, and is a no-op when
//       nothing load-bearing was delegated.
//
//   (b) LIVE ARBITER MECHANISM - a real PanelManager sequence proves the exact captured
//       step: admitting a battle-allowed 'Pause' handle INVOKES the previously open
//       handle's Close delegate (that invocation is what destroyed the end-state), and
//       that a Close wired to the hand-back completes the transition anyway.
//
//   (c) SOURCE INVARIANTS (comment-stripped lint) - the view cannot be exercised headlessly
//       (EndStateView.Show builds a canvas and its abandon paths call Destroy, which errors
//       in edit mode), so the three wiring lines are pinned at source: both abandon
//       choke points call the hand-back, and BattleArena passes its masked return as
//       onAbandon. PLUS: the STRANDING WATCHDOG must still be there, at 45s, with its
//       message intact (owner directive: it did its job and is what made this diagnosable
//       at all - the fix must never be to lengthen or quieten it).
//
//   NOT provable here: that the hero visibly arrives home after a Pause over the victory
//   summary - that is the owner's felt-verify (PO closes, docs/TICKET_PIPELINE.md).
//
// Markers: ENDSTATE_HANDOFF_OK / ENDSTATE_HANDOFF_FAIL.
// Standalone: run-unity-method DeNelle.Editor.Regression.EndStateTransitionHandoffRegression.RunAll
// Covenant contract Run(out reason) is DataRegression-shaped; wiring into
// DataRegression.RunAll is left to the committer (that file is lane-fenced).
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using DeNelle.Core.UI;
using DeNelle.Village.UI;

namespace DeNelle.Editor.Regression
{
    public static class EndStateTransitionHandoffRegression
    {
        private const string ViewSrc  = "Assets/_Modules/Village/UI/EndState/EndStateView.cs";
        private const string VmSrc    = "Assets/_Modules/Village/UI/EndState/EndStateVM.cs";
        private const string ArenaSrc = "Assets/_Modules/Village/Arena/BattleArena.cs";

        /// <summary>Standalone batch entry - prints the marker.</summary>
        public static void RunAll()
        {
            if (Run(out string reason)) Debug.Log("ENDSTATE_HANDOFF_OK - " + reason);
            else Debug.LogError("ENDSTATE_HANDOFF_FAIL: " + reason);
        }

        /// <summary>Covenant contract (DataRegression-shaped). Never throws.</summary>
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            try
            {
                Case(failures, "handback-contract", () => Case1_HandBackContract(failures));
                Case(failures, "arbiter-displace",  () => Case2_ArbiterDisplacement(failures));
                Case(failures, "wiring-lint",       () => Case3_WiringAndWatchdogLint(failures));
            }
            catch (Exception ex)
            {
                failures.Add("[suite] THREW " + ex.GetType().Name + ": " + ex.Message);
            }

            if (failures.Count == 0)
            {
                reason = "ENDSTATE HANDOFF OK - a pending end-state transition completes exactly once " +
                         "when the screen is destroyed without a Continue (never firing the player's " +
                         "choice), a real PanelManager 'Pause' admission still closes the open handle " +
                         "but no longer eats the route home, both abandon choke points hand back at " +
                         "source, BattleArena passes its masked return as onAbandon, and the 45s " +
                         "stranding watchdog is still armed and still loud.";
                return true;
            }
            reason = "endstate-handoff FAIL x" + failures.Count + ": " + string.Join(" | ", failures);
            return false;
        }

        private static void Case(List<string> failures, string name, Action body)
        {
            try { body(); }
            catch (Exception ex) { failures.Add("[" + name + "] THREW " + ex.GetType().Name + ": " + ex.Message); }
        }

        // =====================================================================
        //  Case 1 - the LIVE hand-back contract on a real victory VM
        // =====================================================================

        private static void Case1_HandBackContract(List<string> failures)
        {
            // Built by the REAL factory the arena uses, so a change to FromBattleVictory that
            // stopped carrying the hand-back fails here rather than in the owner's dungeon.
            int primary = 0, handBack = 0;
            var vm = EndStateVM.FromBattleVictory(
                stars: 3, durationSeconds: 12f, xp: 10, wisdom: 0, wood: 0, iron: 0, gearName: null,
                onContinue: () => primary++, autoTimeoutSeconds: 20f, perfect: false,
                primaryRoute: "regression", onAbandon: () => handBack++);

            if (vm == null) { failures.Add("[handback-contract] FromBattleVictory returned null."); return; }
            if (vm.Primary == null)
                failures.Add("[handback-contract] the victory VM carries no Primary - the Continue action was dropped.");
            if (vm.Abandoned == null)
                failures.Add("[handback-contract] FromBattleVictory no longer carries the onAbandon hand-back - " +
                             "the WO-969 fix is not reaching the model, so any modal opening over the victory " +
                             "summary strands the hero again until the 45s watchdog.");

            // The captured event: the screen is destroyed without a Continue.
            bool ran = vm.HandBackPendingTransition("regression: displaced by another modal");
            if (!ran || handBack != 1)
                failures.Add("[handback-contract] the hand-back did not run on displacement (ran=" + ran +
                             ", handBack=" + handBack + ") - the pending transition is still owned by the " +
                             "view's lifetime, which IS the captured stranding defect.");
            if (primary != 0)
                failures.Add("[handback-contract] the hand-back invoked Primary (primary=" + primary + "). " +
                             "A displaced end-state must NEVER silently make the player's choice - it may " +
                             "only return the transition to its owner.");

            // Exactly once, no matter how many destroy paths call it (AbandonedPrimaryWarn AND OnDestroy
            // both do, by design - OnDestroy is the catch-all for paths nobody has written yet).
            vm.HandBackPendingTransition("regression: second destroy path");
            vm.HandBackPendingTransition("regression: third destroy path");
            if (handBack != 1)
                failures.Add("[handback-contract] the hand-back fired " + handBack + " times - it must latch " +
                             "exactly once or the arena schedules the masked return more than once.");
            if (!vm.HandedBack)
                failures.Add("[handback-contract] HandedBack stayed false after a hand-back ran.");

            // A NORMAL Continue must make the hand-back a permanent no-op. The view nulls Primary the
            // instant it fires; model that exactly.
            int primary2 = 0, handBack2 = 0;
            var vm2 = EndStateVM.FromBattleVictory(
                stars: 1, durationSeconds: 3f, xp: 1, wisdom: 0, wood: 0, iron: 0, gearName: null,
                onContinue: () => primary2++, autoTimeoutSeconds: 20f, perfect: false,
                primaryRoute: "regression", onAbandon: () => handBack2++);
            var act = vm2.Primary; vm2.Primary = null; act?.Invoke();      // == EndStateView.FirePrimary
            if (vm2.HandBackPendingTransition("regression: destroyed AFTER Continue") || handBack2 != 0)
                failures.Add("[handback-contract] the hand-back fired after Continue had already run " +
                             "(handBack=" + handBack2 + ") - the return would be scheduled twice.");
            if (primary2 != 1)
                failures.Add("[handback-contract] the modelled Continue did not fire exactly once.");

            // Nothing delegated -> nothing to hand back, and no throw.
            var vm3 = EndStateVM.FromBattleVictory(
                stars: 0, durationSeconds: 1f, xp: 0, wisdom: 0, wood: 0, iron: 0, gearName: null,
                onContinue: () => { }, autoTimeoutSeconds: 20f, perfect: false);
            if (vm3.Abandoned != null)
                failures.Add("[handback-contract] a caller that delegated no hand-back got one anyway.");
            if (vm3.HandBackPendingTransition("regression: no hand-back wired"))
                failures.Add("[handback-contract] HandBackPendingTransition reported a run with no Abandoned action.");
        }

        // =====================================================================
        //  Case 2 - the LIVE arbiter step the capture named
        // =====================================================================

        // PanelManager.NotifyOpened('Pause') -> previous.Close(). That invocation is what
        // destroyed the end-state. This case proves BOTH halves on the real arbiter:
        // the displacement still happens (Pause must always be allowed - the player must
        // always be able to pause), and a Close wired to the hand-back completes the
        // transition regardless.
        private static void Case2_ArbiterDisplacement(List<string> failures)
        {
            PanelManager.CloseAll();   // start from a known-empty arbiter

            int handBack = 0, primary = 0;
            var vm = EndStateVM.FromBattleVictory(
                stars: 2, durationSeconds: 8f, xp: 5, wisdom: 0, wood: 0, iron: 0, gearName: null,
                onContinue: () => primary++, autoTimeoutSeconds: 20f, perfect: false,
                primaryRoute: "regression", onAbandon: () => handBack++);

            // Stands in for the end-state's registration (EndStateView.cs: RegisterBattleAllowed
            // "EndState", view.CloseFromArbiter, ...). Close mirrors CloseFromArbiter's contract:
            // do NOT fire Primary, DO hand the transition back.
            bool endStateAlive = true;
            var endState = PanelManager.RegisterBattleAllowed("EndState",
                () => { endStateAlive = false; vm.HandBackPendingTransition("CloseFromArbiter (regression)"); },
                () => endStateAlive);
            if (!PanelManager.NotifyOpened(endState))
                failures.Add("[arbiter-displace] the battle-allowed end-state handle was REJECTED by the arbiter.");

            // The captured event: Pause is admitted over it (PauseController.cs registers Pause
            // battle-allowed, so no gate can refuse it - and none should).
            bool paused = false;
            var pause = PanelManager.RegisterBattleAllowed("Pause", () => paused = false, () => paused);
            paused = true;
            bool admitted = PanelManager.NotifyOpened(pause);

            if (!admitted)
                failures.Add("[arbiter-displace] Pause was refused over the end-state. Pause must ALWAYS be " +
                             "allowed - blocking it is the fix shape (a) this WO rejected.");
            if (endStateAlive)
                failures.Add("[arbiter-displace] the arbiter did not close the previously open handle - " +
                             "PanelManager's one-modal contract (DEF-212) has changed under this fix.");
            if (handBack != 1)
                failures.Add("[arbiter-displace] admitting Pause over the end-state completed the pending " +
                             "transition " + handBack + " time(s), expected 1. THIS IS THE CAPTURED BUG: the " +
                             "route home dies with the displaced screen and the hero is stranded until the " +
                             "45s stranding watchdog.");
            if (primary != 0)
                failures.Add("[arbiter-displace] displacement fired the player's Continue action (" + primary +
                             ") - a displaced end-state must never silently continue/respawn.");

            paused = false;
            PanelManager.NotifyClosed(pause);
            PanelManager.CloseAll();
        }

        // =====================================================================
        //  Case 3 - wiring + the watchdog, pinned at source (comment-stripped)
        // =====================================================================

        private static void Case3_WiringAndWatchdogLint(List<string> failures)
        {
            string view  = StripComments(File.ReadAllText(ViewSrc));
            string vm    = StripComments(File.ReadAllText(VmSrc));
            string arena = StripComments(File.ReadAllText(ArenaSrc));

            // (1) The model owns the latch (so it can outlive the view) and exposes the hand-back.
            if (!vm.Contains("HandBackPendingTransition"))
                failures.Add("[wiring-lint] " + VmSrc + " no longer declares HandBackPendingTransition - " +
                             "the pending transition is back inside the view's lifetime.");

            // (2) BOTH abandon choke points call it. AbandonedPrimaryWarn covers the three known
            //     destroy paths (replacing Show / OnSceneLoaded / CloseFromArbiter); OnDestroy is the
            //     catch-all that makes the fix hold against the NEXT modal, not just against Pause.
            string warnBody = MethodBody(view, "AbandonedPrimaryWarn(string reason)");
            if (warnBody == null)
                failures.Add("[wiring-lint] EndStateView.AbandonedPrimaryWarn(string reason) is gone - the " +
                             "single choke point the three known destroy paths funnel through.");
            else if (!warnBody.Contains("SignalAbandon("))
                failures.Add("[wiring-lint] EndStateView.AbandonedPrimaryWarn no longer hands the transition " +
                             "back - the three known destroy paths would warn about the abandonment and then " +
                             "still drop it, which is exactly the captured stranding.");

            string destroyBody = MethodBody(view, "OnDestroy()");
            if (destroyBody == null)
                failures.Add("[wiring-lint] EndStateView.OnDestroy() is gone.");
            else if (!destroyBody.Contains("SignalAbandon("))
                failures.Add("[wiring-lint] EndStateView.OnDestroy no longer hands the transition back - the " +
                             "catch-all is gone, so any destroy path not routed through AbandonedPrimaryWarn " +
                             "(including ones not yet written) re-opens this bug.");
            if (!view.Contains("HandBackPendingTransition"))
                failures.Add("[wiring-lint] EndStateView never calls EndStateVM.HandBackPendingTransition.");

            // (3) The arena actually delegates its masked return as the hand-back. Without this line
            //     every mechanism above is wired to nothing.
            if (!Regex.IsMatch(arena, @"onAbandon\s*:\s*doMaskedReturn"))
                failures.Add("[wiring-lint] BattleArena no longer passes doMaskedReturn as onAbandon - the " +
                             "victory summary is once again the SOLE owner of the only route home.");

            // (4) OWNER DIRECTIVE: the watchdog stays exactly as it was. It did its job and its message
            //     is what made this diagnosable at all. The fix must never be to lengthen or quieten it.
            if (!Regex.IsMatch(arena, @"StrandWatchdogSeconds\s*=\s*45f"))
                failures.Add("[wiring-lint] the stranding watchdog is no longer 45s. Owner directive: keep " +
                             "the watchdog and its FlowTrace exactly as they are - it is the last-resort net, " +
                             "never the fix, and lengthening it is not a fix either.");
            if (!arena.Contains("STRANDING WATCHDOG FIRED"))
                failures.Add("[wiring-lint] the STRANDING WATCHDOG FIRED FlowTrace.Fail is gone. Never strip " +
                             "instrumentation (CLAUDE.md 12): that line is the only reason this defect was " +
                             "ever diagnosable.");
            if (!Regex.IsMatch(arena, @"FlowTrace\.Fail\s*\(\s*""BattleArena"""))
                failures.Add("[wiring-lint] the watchdog's FlowTrace.Fail was downgraded or removed - a " +
                             "quieter watchdog is a silent failure.");
        }

        /// <summary>
        /// Return the brace-delimited body of the first method whose declaration contains
        /// <paramref name="signature"/>, or null if it is not there. Depth-scanned rather than
        /// regex-matched so a lint can say WHICH method is missing the call instead of failing on
        /// a length bound. Brace CHARACTERS are written as code points on purpose: CLAUDE.md 1's
        /// brace-balance gate counts literal braces even inside string literals, and a lint that
        /// trips the project's own quality gate is a lint nobody keeps.
        /// (Limitation, stated rather than hidden: an UNBALANCED brace inside a string literal in
        /// the scanned method would skew the scan. The two methods pinned here carry only balanced
        /// interpolation holes.)
        /// </summary>
        private static string MethodBody(string src, string signature)
        {
            const char Open  = (char)123;
            const char Close = (char)125;

            int at = src.IndexOf(signature, StringComparison.Ordinal);
            if (at < 0) return null;
            int start = src.IndexOf(Open, at);
            if (start < 0) return null;

            int depth = 0;
            for (int p = start; p < src.Length; p++)
            {
                if (src[p] == Open) depth++;
                else if (src[p] == Close)
                {
                    depth--;
                    if (depth == 0) return src.Substring(start, p - start + 1);
                }
            }
            return null;
        }

        /// <summary>Strip // line and /* */ block comments so a lint never matches doc text.</summary>
        private static string StripComments(string src)
        {
            src = Regex.Replace(src, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
            src = Regex.Replace(src, @"//[^\r\n]*", string.Empty);
            return src;
        }
    }
}
