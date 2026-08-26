// =============================================================================
// WO1225RewardFlyRegression -- the oracle for "a toast under a modal is still a
// silent grant".
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression   Namespace: DeNelle.Editor.Regression
// Contract: public static bool Run(out string reason). Registered in DataRegression.RunAll
// as [reward-fly]. The suite rolls into REGRESSION_OK <n>/<n> suites.
//
// THE DEFECT (owner felt-test, Seeker 2026.08.26.342290, 12:14):
//   12:14:16.401 [Flow:DailyChest] claimed +1000 Gold path=rewarded_double
//   12:14:16.404 [Flow:UI]         kit toast -> 'Claimed - 1,000 Gold added to your realm.'
//   12:14:16.405 [Flow:UI]         ... current=EchoUnlockDialogue
//   Owner: "it didt show or if it did it was under the echo introdution window that popped."
//
//   WO-1213 was CORRECT and still fired. The acknowledgement lived at sortingOrder 720 and
//   a modal opened at 31020 behind an alpha-0.85 full-screen scrim. The log said success and
//   the player saw nothing -- which is worse than no trace, because it steers the reader away.
//
// ⚠ WHAT THIS SUITE DELIBERATELY DOES *NOT* ASSERT
//   It does NOT assert "the toast is visible under a modal". The owner's ruling moved the
//   acknowledgement OFF the toast layer rather than winning a z-order race, so a suite pinned
//   to the toast's visibility would fail forever on the shipped fix and would be "repaired" by
//   deleting it. The general toast-ordering question stays OPEN and is a separate ticket.
//   What is pinned here is the NEW surface: it exists, it outranks the modal band, it is
//   pooled, and -- the load-bearing one -- IT CANNOT SHOW A NUMBER NOBODY BANKED.
//
// WHAT EACH CASE PINS, AND WHAT BROKEN STATE MAKES IT FAIL
//   [above-modals]    RewardFlightLayer.SortingOrder outranks every modal sortingOrder literal
//                     authored under Assets/_Modules. FAILS IF: the layer is lowered, or a new
//                     modal is authored above it -- i.e. the exact regression re-arriving.
//   [count-measured]  The acknowledgement's number comes from the MEASURED economy-model delta
//                     and the count runs to the MEASURED balance; the requested amount is
//                     compared for a shortfall and never rendered. FAILS IF: anyone re-points
//                     the headline or the Fly() balances at the requested amount.
//   [pooled]          Every body is built once in Awake and cycled; the per-claim path
//                     allocates no GameObject. FAILS IF: a burst starts Instantiating.
//   [chest-raises]    DailyChestController still calls ShowToast (WO-1213 intact) AND raises
//                     the celebration. FAILS IF: either half is dropped.
//   [hud-listens]     HudKitController subscribes AND unsubscribes the Core seam. FAILS IF: the
//                     raise goes to nobody -- a silent grant with a green log, again.
//   [words-not-colour] Both surfaces carry a word and a numeral. FAILS IF: the readout becomes
//                     a naked number or the tell becomes a tint (owner is red/green colourblind).
//   [surface-measured] MEASURED visibility of the layer's canvas via UiSurfaceProbe (WO-976),
//                     which separates SURFACE_ZERO_SIZE / _TRANSPARENT / _OFFSCREEN / _BEHIND --
//                     and SURFACE_BEHIND is precisely this ticket. Batchmode runs no layout
//                     pass, so this emits a NAMED SKIP there, never a silent pass.
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.UI;

namespace DeNelle.Editor.Regression
{
    /// <summary>WO-1225: the reward acknowledgement outranks the modal band and never lies.</summary>
    public static class WO1225RewardFlyRegression
    {
        private const string LayerPath = "_Modules/Core/UI/RewardFlightLayer.cs";
        private const string HudPath   = "_Modules/HUD/Kit/HudKitController.cs";
        private const string ChestPath = "_Modules/Village/Monetization/DailyChestController.cs";

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var notes = new List<string>();

            CheckAboveModals(failures, notes);
            CheckCountMeasured(failures, notes);
            CheckPooled(failures, notes);
            CheckChestRaises(failures, notes);
            CheckHudListens(failures, notes);
            CheckWordsNotColour(failures, notes);
            CheckSurfaceMeasured(failures, notes);

            if (failures.Count > 0)
            {
                reason = "WO-1225 reward-fly: " + string.Join(" | ", failures);
                return false;
            }

            reason = "reward acknowledgement outranks the modal band (sortingOrder " +
                     RewardFlightLayer.SortingOrder + "), is pooled, and counts to the MEASURED " +
                     "balance" + (notes.Count > 0 ? " | " + string.Join(" | ", notes) : "");
            return true;
        }

        private static bool TryRead(string relative, string caseTag, List<string> failures, out string src)
        {
            string full = Path.Combine(Application.dataPath, relative);
            if (!File.Exists(full))
            {
                failures.Add("[" + caseTag + "] MISSING file " + relative);
                src = null;
                return false;
            }
            src = File.ReadAllText(full);
            return true;
        }

        // =====================================================================
        //  [above-modals]
        // =====================================================================
        private static void CheckAboveModals(List<string> failures, List<string> notes)
        {
            // The toast the owner could not see. Pinned as a floor so nobody "fixes" this by
            // dragging the new layer back down toward it.
            if (RewardFlightLayer.SortingOrder <= 720)
            {
                failures.Add("[above-modals] RewardFlightLayer.SortingOrder is " + RewardFlightLayer.SortingOrder +
                             " -- at or below the kit toast's 720, which is the layer WO-1225 exists to leave.");
                return;
            }

            string modulesRoot = Path.Combine(Application.dataPath, "_Modules");
            if (!Directory.Exists(modulesRoot))
            {
                // FIXTURE-ABSENT, NOT A CAPABILITY GAP: the whole load-bearing claim of this case is
                // "outranks every modal sortingOrder authored under Assets/_Modules". With that tree
                // gone the comparison never happens, and a constant-vs-720 floor is not the assertion
                // the case is named for -- so this is red, and it names the path it looked at.
                failures.Add("[above-modals] MISSING directory " + modulesRoot + " -- the modal-band scan " +
                             "could not run, so 'outranks every authored modal sortingOrder' was never " +
                             "checked; only the constant-vs-720 floor was.");
                return;
            }

            // Both authored forms: `sortingOrder: 31020` (named argument) and `= 31020`.
            var rx = new Regex(@"sortingOrder\s*[:=]\s*(\d{3,6})", RegexOptions.IgnoreCase);
            int highest = 0;
            string highestWhere = "<none>";

            foreach (string file in Directory.GetFiles(modulesRoot, "*.cs", SearchOption.AllDirectories))
            {
                // The layer itself declares the winning value -- excluding it is what makes this
                // a comparison against the MODAL band and not against itself.
                if (file.Replace('\\', '/').EndsWith("RewardFlightLayer.cs", StringComparison.OrdinalIgnoreCase))
                    continue;

                string src;
                try { src = File.ReadAllText(file); }
                catch (Exception ex) { notes.Add("[above-modals] unreadable " + Path.GetFileName(file) + ": " + ex.Message); continue; }

                foreach (Match m in rx.Matches(src))
                {
                    int v;
                    if (!int.TryParse(m.Groups[1].Value, out v)) continue;
                    if (v > highest) { highest = v; highestWhere = Path.GetFileName(file); }
                }
            }

            if (highest <= 0)
            {
                notes.Add("[above-modals] no sortingOrder literal was found anywhere under _Modules -- " +
                          "the scan pattern may have gone stale; only the 720 floor was proven");
                return;
            }

            if (RewardFlightLayer.SortingOrder <= highest)
            {
                failures.Add("[above-modals] RewardFlightLayer.SortingOrder=" + RewardFlightLayer.SortingOrder +
                             " does NOT outrank the highest authored surface (" + highest + " in " + highestWhere +
                             "). The acknowledgement can be covered again -- that IS the WO-1225 defect.");
                return;
            }

            if (RewardFlightLayer.ModalBandCeiling < highest)
                notes.Add("[above-modals] ModalBandCeiling (" + RewardFlightLayer.ModalBandCeiling +
                          ") now understates the real ceiling (" + highest + " in " + highestWhere +
                          ") -- the layer still wins, but the documented constant is drifting");

            notes.Add("[above-modals] layer=" + RewardFlightLayer.SortingOrder + " vs highest authored " +
                      highest + " (" + highestWhere + ")");
        }

        // =====================================================================
        //  ⭐ [count-measured] -- the load-bearing case
        // =====================================================================
        private static void CheckCountMeasured(List<string> failures, List<string> notes)
        {
            string src;
            if (!TryRead(HudPath, "count-measured", failures, out src)) return;

            if (src.IndexOf("long measuredGold = e.Gold;", StringComparison.Ordinal) < 0)
                failures.Add("[count-measured] HudKitController no longer reads the post-grant balance " +
                             "off the economy model push (`long measuredGold = e.Gold;`) -- the count-up's " +
                             "source of truth is gone.");

            if (src.IndexOf("long measuredDelta = measuredGold - previous;", StringComparison.Ordinal) < 0)
                failures.Add("[count-measured] the shown amount is no longer the MEASURED delta between two " +
                             "economy pushes. WO-1225: never animate to an amount somebody asked for.");

            if (src.IndexOf("layer.Fly(headline, \"Gold\", _goldCelebrateOrigin, targetRect, previous, measuredGold)",
                            StringComparison.Ordinal) < 0)
                failures.Add("[count-measured] the flight is no longer handed (previous -> measuredGold), the two " +
                             "MEASURED balances. A count-up to anything else is a hollow assertion.");

            // The requested amount must survive ONLY as the shortfall oracle.
            if (src.IndexOf("reward SHORTFALL", StringComparison.Ordinal) < 0)
                failures.Add("[count-measured] the rolled-vs-credited SHORTFALL warn is gone -- a clamped or " +
                             "refused grant would now animate silently, with no line saying it was short.");

            int headlineAt = src.IndexOf("string headline = ", StringComparison.Ordinal);
            if (headlineAt < 0)
            {
                failures.Add("[count-measured] the headline string is no longer built in HudKitController -- " +
                             "the assertion below cannot see what number reaches the screen.");
            }
            else
            {
                int eol = src.IndexOf('\n', headlineAt);
                string line = eol > headlineAt ? src.Substring(headlineAt, eol - headlineAt) : src.Substring(headlineAt);
                if (line.IndexOf("measuredDelta", StringComparison.Ordinal) < 0)
                    failures.Add("[count-measured] the headline is not built from measuredDelta: \"" + line.Trim() + "\"");
                if (line.IndexOf("_goldCelebrateRequested", StringComparison.Ordinal) >= 0)
                    failures.Add("[count-measured] the headline renders the REQUESTED amount. That is exactly the " +
                                 "hollow assertion WO-1225 forbids -- show what was banked.");
            }

            // The layer must stay a dumb renderer with no wallet of its own.
            string layerSrc;
            if (TryRead(LayerPath, "count-measured", failures, out layerSrc))
            {
                foreach (string forbidden in new[] { "EconomyService", "GameStateService", "CoreServices.Economy" })
                {
                    if (layerSrc.IndexOf(forbidden, StringComparison.Ordinal) >= 0)
                        failures.Add("[count-measured] RewardFlightLayer reached for " + forbidden + ". It must stay a " +
                                     "renderer that CANNOT invent a number -- that impossibility is the guarantee.");
                }
            }

            notes.Add("[count-measured] headline=measuredDelta, count=(previous -> measuredGold), requested=oracle only");
        }

        // =====================================================================
        //  [pooled]
        // =====================================================================
        private static void CheckPooled(List<string> failures, List<string> notes)
        {
            string src;
            if (!TryRead(LayerPath, "pooled", failures, out src)) return;

            if (src.IndexOf("Instantiate(", StringComparison.Ordinal) >= 0)
                failures.Add("[pooled] RewardFlightLayer calls Instantiate -- pooling is project law " +
                             "(ARCHITECTURE_PRINCIPLES 2b.1/2b.2, the two-VFX-stack scar).");

            // The per-claim path (Fly + Burst) must allocate no body.
            int flyAt = src.IndexOf("public void Fly(", StringComparison.Ordinal);
            int endAt = src.IndexOf("private void Update()", StringComparison.Ordinal);
            if (flyAt < 0 || endAt <= flyAt)
            {
                failures.Add("[pooled] could not locate the per-claim path (Fly .. Update) in RewardFlightLayer");
            }
            else
            {
                string perClaim = src.Substring(flyAt, endAt - flyAt);
                if (perClaim.IndexOf("new GameObject", StringComparison.Ordinal) >= 0 ||
                    perClaim.IndexOf("AddComponent", StringComparison.Ordinal) >= 0)
                    failures.Add("[pooled] the per-claim path builds a GameObject/component. Bodies are built once " +
                                 "in Awake and cycled with SetActive; a burst that allocates per claim is the sprawl " +
                                 "the pooling rule exists to prevent.");
            }

            if (src.IndexOf("s.go.SetActive(true)", StringComparison.Ordinal) < 0)
                failures.Add("[pooled] the streamer pool no longer cycles with SetActive -- the pool may have been " +
                             "replaced by per-burst construction.");

            notes.Add("[pooled] headline + streamer + readout bodies all built in Awake, cycled with SetActive");
        }

        // =====================================================================
        //  [chest-raises] -- WO-1213 intact AND the new acknowledgement raised
        // =====================================================================
        private static void CheckChestRaises(List<string> failures, List<string> notes)
        {
            string src;
            if (!TryRead(ChestPath, "chest-raises", failures, out src)) return;

            if (src.IndexOf("ElarionUiKit.ShowToast(msg", StringComparison.Ordinal) < 0)
                failures.Add("[chest-raises] DailyChestController no longer raises the WO-1213 toast. WO-1225 is " +
                             "ADDITIVE -- the toast is committed, green and correct and must not be re-opened.");

            if (src.IndexOf("RewardCelebration.Raise(\"Gold\", gold,", StringComparison.Ordinal) < 0)
                failures.Add("[chest-raises] the claim no longer raises the chip-anchored acknowledgement. The toast " +
                             "alone is occludable -- that is the whole ticket.");

            if (src.IndexOf("EconomyService.Instance.AddCoins(gold);", StringComparison.Ordinal) < 0)
                failures.Add("[chest-raises] the grant path changed -- WO-1225 must not touch it.");

            notes.Add("[chest-raises] toast (WO-1213) + celebration raise (WO-1225) both present; grant untouched");
        }

        // =====================================================================
        //  [hud-listens]
        // =====================================================================
        private static void CheckHudListens(List<string> failures, List<string> notes)
        {
            string src;
            if (!TryRead(HudPath, "hud-listens", failures, out src)) return;

            if (src.IndexOf("RewardCelebration.Requested += OnRewardCelebrationRequested", StringComparison.Ordinal) < 0)
                failures.Add("[hud-listens] HudKitController does not subscribe RewardCelebration.Requested -- every " +
                             "raise would land on nobody and the grant would be silent again.");

            if (src.IndexOf("RewardCelebration.Requested -= OnRewardCelebrationRequested", StringComparison.Ordinal) < 0)
                failures.Add("[hud-listens] the subscription is never released -- a scene swap would leak a dead " +
                             "listener holding the old chip.");

            // The ordering half. DailyChestController credits the wallet BEFORE it raises, and
            // EconomyService.OnChanged pushes EconomyModel synchronously, so the measured move is
            // already behind us when the raise lands. Without the look-back the armed window would
            // simply EXPIRE and the owner would see nothing -- the original defect, reproduced by
            // a fix that only ever waits forward.
            if (src.IndexOf("_goldLastGainConsumed", StringComparison.Ordinal) < 0 ||
                src.IndexOf("GoldCelebrateLookbackSeconds", StringComparison.Ordinal) < 0)
                failures.Add("[hud-listens] the look-back on the last MEASURED gain is gone. The grant credits the " +
                             "wallet before it raises, so a wait-forward-only arm would expire unseen.");

            if (src.IndexOf("reward celebration EXPIRED", StringComparison.Ordinal) < 0)
                failures.Add("[hud-listens] the armed-window timeout is gone. An acknowledgement whose wallet push " +
                             "never arrives must FAIL loudly and show nothing, never animate on faith.");

            notes.Add("[hud-listens] subscribe + unsubscribe + armed-window timeout all present");
        }

        // =====================================================================
        //  [words-not-colour]
        // =====================================================================
        private static void CheckWordsNotColour(List<string> failures, List<string> notes)
        {
            string src;
            if (!TryRead(LayerPath, "words-not-colour", failures, out src)) return;

            if (src.IndexOf("_readoutWord + \" \"", StringComparison.Ordinal) < 0)
                failures.Add("[words-not-colour] the readout no longer prefixes the resource WORD -- it would read as " +
                             "a naked number. The owner is red/green colourblind (CLAUDE.md section 7).");

            string hud;
            if (TryRead(HudPath, "words-not-colour", failures, out hud))
            {
                if (hud.IndexOf("+ \" Gold\"", StringComparison.Ordinal) < 0)
                    failures.Add("[words-not-colour] the headline no longer names the resource in words " +
                                 "(expected a \"+N Gold\" build) -- a bare \"+N\" says nothing about what was won.");
            }

            if (src.IndexOf("raycastTarget = false", StringComparison.Ordinal) < 0)
                failures.Add("[words-not-colour] the layer's graphics are raycast-enabled -- decoration drawn above " +
                             "every modal must never eat the tap meant for the modal underneath it.");

            if (src.IndexOf("GraphicRaycaster", StringComparison.Ordinal) >= 0 &&
                src.IndexOf("NO GraphicRaycaster", StringComparison.Ordinal) < 0)
                failures.Add("[words-not-colour] the layer added a GraphicRaycaster -- at sortingOrder " +
                             RewardFlightLayer.SortingOrder + " it would swallow input for the whole screen.");

            notes.Add("[words-not-colour] word + numeral on both surfaces; layer eats no input");
        }

        // =====================================================================
        //  [surface-measured] -- MEASURED, or a NAMED SKIP. Never a silent pass.
        // =====================================================================
        private static void CheckSurfaceMeasured(List<string> failures, List<string> notes)
        {
            string skipReason;
            if (UiSurfaceProbe.IsUnmeasurableEnvironment(out skipReason))
            {
                // MANDATORY named skip (UiSurfaceProbe header): batchmode runs no layout pass, so
                // every rect would measure 0 and this would emit a spurious failure -- which the
                // next reader "fixes" by weakening the check, straight back to a hollow line.
                notes.Add(RegressionOutcome.PartialSkip("[surface-measured] measured visibility of the layer",
                          skipReason + " -- the measured half of acceptance 3 must be proven by the device " +
                          "screenshot and by an in-play capture, not by this suite"));
                FlowTrace.Step("Reward",
                    "WO-1225 [surface-measured] MEASURE_SKIPPED(" + skipReason + ")");
                return;
            }

            var layer = RewardFlightLayer.Instance;
            if (layer == null)
            {
                // HARNESS-CAPABILITY-ABSENT: the layer self-builds on the first acknowledgement and this
                // process raises none. Declared through RegressionOutcome.PartialSkip so the stand-down
                // carries a token the CALLER can see, instead of prose that reads green.
                notes.Add(RegressionOutcome.PartialSkip("[surface-measured] measured visibility of the layer",
                          "no RewardFlightLayer instance in this process -- the layer self-builds on the first " +
                          "acknowledgement and none has been raised here"));
                FlowTrace.Step("Reward", "WO-1225 [surface-measured] MEASURE_SKIPPED(no layer instance)");
                return;
            }

            var m = UiSurfaceProbe.Measure(layer.gameObject);
            if (!m.Measurable)
            {
                // HARNESS-CAPABILITY-ABSENT: the probe itself reports it cannot measure here. Same rule --
                // it stands down in a machine-readable token, not only in words.
                notes.Add(RegressionOutcome.PartialSkip("[surface-measured] measured visibility of the layer",
                          "UiSurfaceProbe reports unmeasurable: " + m.SkipReason));
                FlowTrace.Step("Reward", "WO-1225 [surface-measured] MEASURE_SKIPPED(" + m.SkipReason + ")");
                return;
            }

            // SURFACE_BEHIND is precisely this ticket: something higher-sorted covering the
            // acknowledgement. UiSurfaceProbe.Report names which of the four classes it is.
            if (!UiSurfaceProbe.Report("Reward", "reward-fly-layer", in m))
                failures.Add("[surface-measured] the acknowledgement layer did not measure visible: " + m.Describe());
        }
    }
}
