// =============================================================================
// TransactionWorldHoldRegression [world-hold] -- WO-1149. The world must stop for a
// transaction, and it must ALWAYS start again.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression (editor-only). Contract mirrors the siblings:
//   public static bool Run(out string reason)   -- NEVER throws
//   markers: WORLD_HOLD_OK (Debug.Log) / WORLD_HOLD_FAIL (LogError)
//   registered ONCE inside DataRegression.RunAll's fenced registry region.
//
// THE DEFECT (owner, on device 2026-08-22): "we need to stop game during transactions
// got killed while making purchase test." Opening the store switched HUD posture and
// nothing else -- wave timers ticked, the ATB ran, enemies attacked -- for the many
// seconds a real settlement takes. The player could neither defend nor cancel without
// abandoning a transaction that may already have been signed.
//
// ⛔ WHAT THIS SUITE REFUSES TO DO, AND WHY IT MATTERS MORE THAN WHAT IT DOES.
// The lazy oracle here greps PackStore.cs for the word "Resume" (or "Dispose") and goes
// green. That assertion passes while three branches skip the release -- which is the
// ONLY failure mode that matters, because a hold that fails to release is WORSE than no
// hold: a frozen game after a completed purchase is a support ticket AND a refund. So
// every case below either MEASURES Time.timeScale after driving a real path, or proves a
// STRUCTURAL property from which the undrivable paths follow by construction.
//
// THE PROOF CHAIN, stated plainly so a reader can audit it rather than trust it:
//   (A) [scope]      the hold RELEASES on normal exit, on an EXCEPTION, and only when the
//                    LAST outstanding hold goes -- driven live, Time.timeScale measured.
//   (B) [captured]   a full release restores the CAPTURED pre-freeze scale, not 1.0 --
//                    driven from a non-1 scale, measured.
//   (C) [first-stmt] the acquisition is a `using` declaration and is the FIRST statement
//                    of PackStore.Purchase -- read from source.
//   (A) + (C) together are what covers the exits no editor process can drive (wallet
//   rejection, chain timeout, the four verification outcomes): C# scope semantics, not
//   somebody's diligence, carry the release out of every one of them. That is a proof,
//   not a restatement -- but the LIVE wallet paths are still declared as a PARTIAL-SKIP
//   below, because "proven by construction" and "observed on a device" are not the same
//   event and the log must not blur them.
//   (D) [drive]      PackStore.Purchase is CALLED for real on its two editor-reachable
//                    branches (null pack; PurchaseGate refusal) and the clock is measured
//                    unfrozen afterwards.
//   (E) [one-owner]  neither PauseController nor PackStore writes Time.timeScale. A second
//                    writer is the WO-1016 permanent-invisible-freeze shape, and it is the
//                    regression this file most expects to catch two months from now.
//
// The suite works with FeatureFlags.RealmStorePurchase OFF (the shipping default) and
// never turns it on: stopping the world during a transaction is a SAFETY behaviour and
// must not depend on the money rail being live.
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using DeNelle.Core.UI;
using DeNelle.Wallet;

namespace DeNelle.Editor.Regression
{
    /// <summary>Pins WO-1149: a transaction freezes the world, and every exit unfreezes it.</summary>
    public static class TransactionWorldHoldRegression
    {
        private const string PackStoreRel      = "/_Modules/Wallet/PackStore.cs";
        private const string PauseControllerRel = "/_Modules/Settings/PauseController.cs";

        /// <summary>Standalone batch entry - prints the marker.</summary>
        public static void RunAll()
        {
            if (Run(out string reason)) Debug.Log("WORLD_HOLD_OK - " + reason);
            else Debug.LogError("WORLD_HOLD_FAIL: " + reason);
        }

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var log = new StringBuilder();
            log.AppendLine("=== TransactionWorldHoldRegression [world-hold] (WO-1149: stop the world for a transaction) ===");

            float entryScale = Time.timeScale;
            try
            {
                WorldHold.ResetForTests();

                CaseScopeReleasesOnEveryExit(failures, log);
                CaseRestoresTheCapturedScale(failures, log);
                CaseRefCountedAcrossOverlappingHolds(failures, log);
                CaseForceReleaseAlwaysUnfreezes(failures, log);
                CaseAcquireIsTheFirstStatementOfPurchase(failures, log);
                CaseDrivePurchaseBranches(failures, log);
                CaseSingleTimeScaleOwner(failures, log);

                log.AppendLine("  [live-rails] " + RegressionOutcome.PartialSkip(
                    "wallet-rejection / chain-timeout / verification-pending / backgrounded exits",
                    "no editor process can drive a signing wallet or a chain confirmation. They are " +
                    "covered BY CONSTRUCTION -- [scope] proves the `using` releases on return AND on " +
                    "throw, [first-stmt] proves the acquisition is the first statement of Purchase, so " +
                    "C# scope semantics carry the release out of every branch. The backgrounded-mid-" +
                    "flight case (an await that never resumes) is covered by WorldHold's stuck-hold " +
                    "watchdog, which needs a play session to tick. OWNER FELT-VERIFY IS STILL REQUIRED " +
                    "on device: open the store mid-wave, complete a purchase, and confirm the world " +
                    "both stops and starts again."));
            }
            catch (Exception ex)
            {
                failures.Add("[world-hold] TransactionWorldHoldRegression THREW: " + ex.GetType().Name + ": " + ex.Message);
            }
            finally
            {
                // This suite deliberately freezes the engine clock. It must never leave the EDITOR
                // frozen either -- the same rule it exists to enforce on the player.
                WorldHold.ResetForTests();
                Time.timeScale = entryScale > 0f ? entryScale : 1f;
            }

            if (failures.Count == 0)
            {
                reason = "WORLD HOLD OK - a WorldHold freezes Time.timeScale and releases it on normal exit, " +
                         "on an exception and only when the LAST overlapping hold goes; a full release restores " +
                         "the CAPTURED pre-freeze scale rather than a hardcoded 1.0; the acquisition is a " +
                         "`using` declaration and is the FIRST statement of PackStore.Purchase, so every branch " +
                         "of the charge path releases by construction; both editor-reachable Purchase branches " +
                         "were driven for real and left the clock running; and Time.timeScale still has exactly " +
                         "ONE writer (WorldHold) - neither PauseController nor PackStore assigns it.";
                Debug.Log("WORLD_HOLD_OK\n" + log);
                return true;
            }

            reason = "world-hold: " + failures.Count + " failure(s): " + string.Join(" | ", failures);
            Debug.LogError("WORLD_HOLD_FAIL: " + failures.Count + " failure(s)\n" + log +
                           "\n - " + string.Join("\n - ", failures));
            return false;
        }

        // =====================================================================
        //  (A) [scope] -- BEHAVIOURAL. Normal exit AND thrown exit both release.
        // =====================================================================
        private static void CaseScopeReleasesOnEveryExit(List<string> failures, StringBuilder log)
        {
            WorldHold.ResetForTests();
            Time.timeScale = 1f;

            // -- normal exit --
            float insideScale;
            using (WorldHold.Acquire("test-normal-exit"))
            {
                insideScale = Time.timeScale;
            }
            if (!Mathf.Approximately(insideScale, 0f))
                failures.Add("[scope] inside a WorldHold the clock read " + insideScale.ToString("0.00") +
                             ", not 0. The world did not stop - wave timers, the ATB and enemy movement " +
                             "all keep running, which is the entire defect WO-1149 exists to close.");
            if (!Mathf.Approximately(Time.timeScale, 1f))
                failures.Add("[scope] after a normal exit from the hold the clock read " +
                             Time.timeScale.ToString("0.00") + ", not 1. A hold that fails to release leaves " +
                             "the player in a frozen game after a completed purchase - worse than no hold.");

            // -- thrown exit. This is the case that proves the `using` (and not diligence) is
            //    what releases: nothing in the block runs after the throw. --
            WorldHold.ResetForTests();
            Time.timeScale = 1f;
            bool threw = false;
            try
            {
                using (WorldHold.Acquire("test-throw-exit"))
                {
                    throw new InvalidOperationException("simulated mid-transaction failure");
                }
            }
            catch (InvalidOperationException) { threw = true; }

            if (!threw)
                failures.Add("[scope] the simulated throw did not propagate - the case asserted nothing.");
            if (WorldHold.IsHeld || !Mathf.Approximately(Time.timeScale, 1f))
                failures.Add("[scope] an EXCEPTION inside the hold left the world frozen (clock " +
                             Time.timeScale.ToString("0.00") + ", holds [" + WorldHold.Describe() + "]). " +
                             "PackStore.Purchase has a catch on the settle path; if a throw can strand the " +
                             "clock, a failed purchase softlocks the game.");

            // -- double dispose must not double-release (it would unfreeze under another hold) --
            WorldHold.ResetForTests();
            Time.timeScale = 1f;
            var outer = WorldHold.Acquire("test-outer");
            var inner = WorldHold.Acquire("test-double");
            inner.Dispose();
            inner.Dispose();
            if (!Mathf.Approximately(Time.timeScale, 0f) || WorldHold.Count != 1)
                failures.Add("[scope] a DOUBLE dispose released more than its own hold (clock " +
                             Time.timeScale.ToString("0.00") + ", count " + WorldHold.Count + "). That would " +
                             "unfreeze the world under a live transaction.");
            outer.Dispose();

            log.AppendLine("  [scope] hold freezes the clock; releases on normal exit, on a thrown exception, " +
                           "and a double dispose releases only its own hold.");
        }

        // =====================================================================
        //  (B) [captured] -- RESTORE THE CAPTURED SCALE, NEVER A HARDCODED 1.0.
        //  A purchase opened during a slow-motion beat or a dev time-skip must not
        //  silently resume at full speed.
        // =====================================================================
        private static void CaseRestoresTheCapturedScale(List<string> failures, StringBuilder log)
        {
            WorldHold.ResetForTests();
            const float odd = 0.35f;          // deliberately not 1, and not a value anyone would hardcode
            Time.timeScale = odd;

            using (WorldHold.Acquire("test-captured"))
            {
                if (!Mathf.Approximately(WorldHold.CapturedScale, odd))
                    failures.Add("[captured] WorldHold captured " + WorldHold.CapturedScale.ToString("0.00") +
                                 " instead of the observed " + odd.ToString("0.00") + ".");
            }

            if (!Mathf.Approximately(Time.timeScale, odd))
                failures.Add("[captured] release restored " + Time.timeScale.ToString("0.00") + " instead of the " +
                             "captured " + odd.ToString("0.00") + ". A hardcoded 1.0 restore would silently " +
                             "resume a slow-motion or time-skipped world at full speed (WO-1149 acceptance #3).");

            // The WO-1016 guard: capturing an ALREADY-FROZEN clock is never meaningful to restore.
            // Restoring 0 there is the permanent, invisible freeze the owner hit on 2026-08-10.
            WorldHold.ResetForTests();
            Time.timeScale = 0f;
            using (WorldHold.Acquire("test-already-frozen")) { }
            if (Time.timeScale <= 0f)
                failures.Add("[captured] acquiring while the clock was ALREADY 0 restored " +
                             Time.timeScale.ToString("0.00") + " - a permanent invisible freeze (the WO-1016 " +
                             "signature). A capture of <= 0 must degrade to 1.");

            Time.timeScale = 1f;
            log.AppendLine("  [captured] release restores the captured pre-freeze scale (0.35 round-tripped); " +
                           "a captured <= 0 degrades to 1 rather than re-arming a frozen world.");
        }

        // =====================================================================
        //  (C) [refcount] -- the pause menu and a transaction OVERLAP. Resuming the
        //  menu mid-purchase must not drop the player back into a live battle.
        // =====================================================================
        private static void CaseRefCountedAcrossOverlappingHolds(List<string> failures, StringBuilder log)
        {
            WorldHold.ResetForTests();
            Time.timeScale = 1f;

            var purchase = WorldHold.Acquire(WorldHold.ReasonPurchase);
            var menu     = WorldHold.Acquire(WorldHold.ReasonPauseMenu);

            menu.Dispose();               // player closes the pause menu, transaction still running
            if (!Mathf.Approximately(Time.timeScale, 0f))
                failures.Add("[refcount] closing the pause menu during a transaction unfroze the world (clock " +
                             Time.timeScale.ToString("0.00") + "). The player is back in a live battle with a " +
                             "signed transaction in flight - the exact situation WO-1149 forbids.");
            if (WorldHold.Count != 1)
                failures.Add("[refcount] expected 1 outstanding hold after the menu closed, found " +
                             WorldHold.Count + " [" + WorldHold.Describe() + "].");

            purchase.Dispose();
            if (!Mathf.Approximately(Time.timeScale, 1f) || WorldHold.IsHeld)
                failures.Add("[refcount] the LAST hold released but the world stayed frozen (clock " +
                             Time.timeScale.ToString("0.00") + ", holds [" + WorldHold.Describe() + "]).");

            log.AppendLine("  [refcount] overlapping menu + transaction holds: frozen until the LAST releases.");
        }

        // =====================================================================
        //  (D) [force] -- quit-to-title and the stuck-hold watchdog must always
        //  leave a running clock. The next scene must never load frozen.
        // =====================================================================
        private static void CaseForceReleaseAlwaysUnfreezes(List<string> failures, StringBuilder log)
        {
            WorldHold.ResetForTests();
            Time.timeScale = 1f;

            WorldHold.Acquire("test-abandoned-a");
            WorldHold.Acquire("test-abandoned-b");
            WorldHold.ForceReleaseAll("regression");

            if (WorldHold.IsHeld || !Mathf.Approximately(Time.timeScale, 1f))
                failures.Add("[force] ForceReleaseAll left the world frozen (clock " +
                             Time.timeScale.ToString("0.00") + ", holds [" + WorldHold.Describe() + "]). This is " +
                             "the path quit-to-title uses; a frozen title screen is unrecoverable without a " +
                             "process restart.");

            // Idempotent: calling it with nothing outstanding must still be safe.
            WorldHold.ForceReleaseAll("regression-idempotent");
            if (!Mathf.Approximately(Time.timeScale, 1f))
                failures.Add("[force] a second ForceReleaseAll with no holds outstanding changed the clock to " +
                             Time.timeScale.ToString("0.00") + ".");

            log.AppendLine("  [force] ForceReleaseAll drops every outstanding hold and restores the clock; idempotent.");
        }

        // =====================================================================
        //  (E) [first-stmt] -- STRUCTURAL, and the load-bearing case.
        //  The acquisition must be a `using` DECLARATION and must be the FIRST
        //  statement of PackStore.Purchase. That is what makes the release
        //  unskippable on every branch, including the ones not yet written.
        // =====================================================================
        private static void CaseAcquireIsTheFirstStatementOfPurchase(List<string> failures, StringBuilder log)
        {
            string path = Application.dataPath + PackStoreRel;
            if (!File.Exists(path))
            {
                failures.Add("[first-stmt] PackStore.cs not found at " + path + " - the charge path cannot be " +
                             "verified, so this is a FAIL, not an unknown.");
                return;
            }

            string src = File.ReadAllText(path);
            int at = src.IndexOf("UniTask<PaymentResult> Purchase(", StringComparison.Ordinal);
            if (at < 0)
            {
                failures.Add("[first-stmt] PackStore.Purchase(PackDef, CurrencyKind) was not found. If the charge " +
                             "path was renamed, this oracle must be repointed at the new one in the SAME change - " +
                             "an oracle aimed at a method that no longer exists asserts nothing.");
                return;
            }

            int brace = src.IndexOf('{', at);
            if (brace < 0)
            {
                failures.Add("[first-stmt] could not locate the body of PackStore.Purchase.");
                return;
            }

            string firstStatement = FirstStatementAfter(src, brace + 1);
            if (firstStatement == null)
            {
                failures.Add("[first-stmt] PackStore.Purchase has no statements at all - unparseable.");
                return;
            }

            bool isUsingDecl = firstStatement.StartsWith("using var", StringComparison.Ordinal) ||
                               firstStatement.StartsWith("using (", StringComparison.Ordinal);
            bool acquires    = firstStatement.IndexOf("WorldHold.Acquire(", StringComparison.Ordinal) >= 0;

            if (!isUsingDecl || !acquires)
                failures.Add("[first-stmt] the FIRST statement of PackStore.Purchase is \"" + firstStatement +
                             "\", not a `using` WorldHold.Acquire(...). Either the world is not stopped for the " +
                             "transaction at all, or it is stopped by a paired Acquire/Dispose - and a pair is " +
                             "exactly the shape that leaves an early return, a guard clause or a catch without a " +
                             "release. A hold that fails to release is worse than no hold.");
            else
                log.AppendLine("  [first-stmt] PackStore.Purchase opens with `" + firstStatement +
                               "` - every branch releases by C# scope semantics, not by diligence.");

            // The reason token must be the shared constant, so a stuck clock names itself in the log
            // instead of arriving as an anonymous freeze (CLAUDE.md §12: diagnosable in one read).
            if (acquires && firstStatement.IndexOf("ReasonPurchase", StringComparison.Ordinal) < 0)
                failures.Add("[first-stmt] the hold is acquired with an ad-hoc reason string rather than " +
                             "WorldHold.ReasonPurchase. The reason is what a FlowTrace stuck-hold line prints; " +
                             "an anonymous hold is an undiagnosable freeze.");
        }

        /// <summary>
        /// Returns the first real statement (comments and blank lines skipped) after
        /// <paramref name="from"/>, trimmed to a single line. Deliberately line-based: the
        /// property being asserted is "what a reader sees first", and that is a line.
        /// </summary>
        private static string FirstStatementAfter(string src, int from)
        {
            string[] lines = src.Substring(from).Split('\n');
            for (int i = 0; i < lines.Length && i < 200; i++)
            {
                string t = lines[i].Trim().TrimEnd('\r');
                if (t.Length == 0) continue;
                if (t.StartsWith("//", StringComparison.Ordinal)) continue;
                if (t.StartsWith("/*", StringComparison.Ordinal) || t.StartsWith("*", StringComparison.Ordinal)) continue;
                if (t.StartsWith("#", StringComparison.Ordinal)) continue;   // #if / #region
                return t;
            }
            return null;
        }

        // =====================================================================
        //  (F) [drive] -- BEHAVIOURAL on the real method. Two branches of
        //  PackStore.Purchase are reachable from an editor process without a
        //  wallet, a chain or a built UI. Both are DRIVEN, and the clock is
        //  measured afterwards.
        //
        //  The component is added to an INACTIVE GameObject on purpose: Awake()
        //  would construct a WalletService and OnEnable() would build the whole
        //  store modal. Neither branch under test touches those fields, and an
        //  oracle that needs a live store to run is an oracle that stops running.
        // =====================================================================
        private static void CaseDrivePurchaseBranches(List<string> failures, StringBuilder log)
        {
            WorldHold.ResetForTests();
            Time.timeScale = 1f;

            GameObject go = null;
            try
            {
                go = new GameObject("~wo1149-packstore-probe") { hideFlags = HideFlags.HideAndDontSave };
                go.SetActive(false);                       // suppress Awake/OnEnable
                var store = go.AddComponent<PackStore>();

                // -- branch 1: the null-pack guard, the very first early return --
                DriveAndAssertUnfrozen(store, null, "null-pack guard", failures, log);

                // -- branch 2: the PurchaseGate refusal. With RealmStorePurchase OFF (the shipping
                //    default) this is the branch every real tap takes today. --
                PackDef pack = PackCatalog.Find(PurchaseGate.DevnetCanarySku);
                if (pack == null)
                {
                    log.AppendLine("  [drive] " + RegressionOutcome.PartialSkip("gate-refusal branch",
                        "pack '" + PurchaseGate.DevnetCanarySku + "' is not in the catalog on this machine, so the " +
                        "refusal branch could not be driven. The null-pack branch above still ran."));
                }
                else
                {
                    DriveAndAssertUnfrozen(store, pack, "PurchaseGate refusal", failures, log);
                }
            }
            catch (Exception ex)
            {
                failures.Add("[drive] driving PackStore.Purchase THREW outside the call itself: " +
                             ex.GetType().Name + ": " + ex.Message);
            }
            finally
            {
                if (go != null) UnityEngine.Object.DestroyImmediate(go);
            }

            if (WorldHold.IsHeld || !Mathf.Approximately(Time.timeScale, 1f))
                failures.Add("[drive] after driving the purchase branches the world is STILL FROZEN (clock " +
                             Time.timeScale.ToString("0.00") + ", holds [" + WorldHold.Describe() + "]).");
        }

        private static void DriveAndAssertUnfrozen(PackStore store, PackDef pack, string what,
                                                   List<string> failures, StringBuilder log)
        {
            string outcome;
            try
            {
                // Every branch under test is synchronous, so the UniTask is already complete here.
                var result = store.Purchase(pack, CurrencyKind.Sol).GetAwaiter().GetResult();
                outcome = "returned Ok=" + result.Ok + " error=\"" + (result.Error ?? "") + "\"";
            }
            catch (Exception ex)
            {
                // A throw is a legitimate exit of Purchase and the clock must survive it identically.
                outcome = "threw " + ex.GetType().Name;
            }

            if (WorldHold.IsHeld || !Mathf.Approximately(Time.timeScale, 1f))
                failures.Add("[drive] PackStore.Purchase (" + what + ", " + outcome + ") left the world FROZEN " +
                             "(clock " + Time.timeScale.ToString("0.00") + ", holds [" + WorldHold.Describe() +
                             "]). This branch returns without releasing its hold.");
            else
                log.AppendLine("  [drive] PackStore.Purchase (" + what + ") " + outcome +
                               " and left the clock running.");
        }

        // =====================================================================
        //  (G) [one-owner] -- STRUCTURAL. Time.timeScale must have exactly ONE
        //  writer. Two owners racing the same clock is the WO-1016 permanent-
        //  invisible-freeze shape, and it is the regression most likely to be
        //  reintroduced by a well-meaning future edit ("just pause it here").
        // =====================================================================
        private static void CaseSingleTimeScaleOwner(List<string> failures, StringBuilder log)
        {
            AssertNoTimeScaleAssignment(Application.dataPath + PauseControllerRel, "PauseController", failures);
            AssertNoTimeScaleAssignment(Application.dataPath + PackStoreRel, "PackStore", failures);
            log.AppendLine("  [one-owner] neither PauseController nor PackStore assigns Time.timeScale - " +
                           "WorldHold is the single writer.");
        }

        private static void AssertNoTimeScaleAssignment(string path, string label, List<string> failures)
        {
            if (!File.Exists(path))
            {
                failures.Add("[one-owner] " + label + " source not found at " + path +
                             " - single-owner cannot be verified, so this is a FAIL, not an unknown.");
                return;
            }

            string[] lines = File.ReadAllLines(path);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                int comment = line.IndexOf("//", StringComparison.Ordinal);
                if (comment >= 0) line = line.Substring(0, comment);

                int at = line.IndexOf("Time.timeScale", StringComparison.Ordinal);
                if (at < 0) continue;

                int j = at + "Time.timeScale".Length;
                while (j < line.Length && line[j] == ' ') j++;
                if (j >= line.Length || line[j] != '=') continue;          // a read, not a write
                if (j + 1 < line.Length && line[j + 1] == '=') continue;   // a comparison

                failures.Add("[one-owner] " + label + ".cs:" + (i + 1) + " ASSIGNS Time.timeScale directly: \"" +
                             lines[i].Trim() + "\". WorldHold is the single owner of the freeze - a second writer " +
                             "races it for the same frame's clock and can restore a value the other captured, " +
                             "which is exactly the permanent invisible freeze of WO-1016. Take a WorldHold " +
                             "instead.");
            }
        }
    }
}
