// =============================================================================
// RoughStoneFanfareRegression (WO-1596) - the permission gate for the rough-stone
// fanfare: the beat exists, it fills the screen, it is readable, it is reachable,
// and the exit WAITS for it without ever becoming a dead exit.
// -----------------------------------------------------------------------------
// THE DEFECT IT PINS SHUT (owner device log 2026-09-07 09:44:07): the grant, the
// "Take it to the Jeweler" sentence and the scene route all landed inside ten
// milliseconds with nothing on screen. The first Rough Stone is guaranteed exactly
// once per player and it is the door to the Jeweler and the Rings of Power.
//
// WHAT THIS SUITE DELIBERATELY DOES **NOT** TEST, and why (CLAUDE.md sec.11B - a
// test that fakes a pass is worse than no test):
//   GrantRunPayout needs a live VillageInventory singleton and UnityEngine.Random,
//   so "the panel opens for a grant and not for a missed roll" cannot be exercised
//   honestly in EditMode. What IS provable is tested instead:
//     * ShouldAwardPostFirstStone - pure math, the miss/hit boundary itself.
//     * SOURCE ORDER in DungeonController.GrantRunPayout: the announce sits AFTER
//       inv.AddEarned, and every withheld/skipped/missed path RETURNS before it.
//       That is the real invariant ("a fanfare only ever follows a banked stone"),
//       and it is checkable without a scene.
//     * The VM's first-vs-repeat composition, which is where the copy rules live.
//
// Runs inside DataRegression.RunAll (marker REGRESSION_OK <n>/<n> suites).
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using DeNelle.Core.Catalog;
using DeNelle.Core.UI;
using DeNelle.Dungeons;

namespace DeNelle.Editor.Regression
{
    public static class RoughStoneFanfareRegression
    {
        private const string VmSrc = "Assets/_Modules/Dungeons/RoughStoneFanfareVM.cs";
        private const string PanelSrc = "Assets/_Modules/Dungeons/RoughStoneFanfarePanel.cs";
        private const string ControllerSrc = "Assets/_Modules/Dungeons/DungeonController.cs";
        private const string ExitSrc = "Assets/_Modules/Dungeons/DungeonExitInteractable.cs";

        /// <summary>Reference canvas height the CTA touch floor is proved against.</summary>
        private const float ReferenceCanvasH = 1920f;

        /// <summary>Standalone batch entry - prints its own marker.</summary>
        public static void RunAll()
        {
            if (Run(out string reason)) Debug.Log("ROUGH_STONE_FANFARE_OK - " + reason);
            else Debug.LogError("ROUGH_STONE_FANFARE_FAIL: " + reason);
        }

        /// <summary>Covenant contract (DataRegression-shaped). Never throws.</summary>
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var notes = new List<string>();
            try
            {
                Case(failures, "bands", () => Case1_BandsDisjoint(failures));
                Case(failures, "fullscreen", () => Case2_FillsTheScreen(failures, notes));
                Case(failures, "touch", () => Case3_CtaMeetsTouchFloor(failures, notes));
                Case(failures, "copy", () => Case4_CopyIsAsciiAndVersioned(failures));
                Case(failures, "stars", () => Case5_StarsReadWithoutColour(failures));
                Case(failures, "no-grant", () => Case6_PresentationNeverGrants(failures));
                Case(failures, "announce-order", () => Case7_AnnounceFollowsTheBank(failures));
                Case(failures, "exit-waits", () => Case8_ExitWaitsAndIsNeverDead(failures));
                Case(failures, "drop-rate", () => Case9_PostFirstRollBoundary(failures));
                Case(failures, "real-catalog", () => Case10_RealCatalogPath(failures, notes));
                Case(failures, "trace-order", () => Case11_ShownIsEmittedOnceAndOnlyWhenShown(failures));
            }
            catch (Exception ex)
            {
                failures.Add("[suite] THREW " + ex.GetType().Name + ": " + ex.Message);
            }

            string noteStr = notes.Count > 0 ? " [notes: " + string.Join("; ", notes.ToArray()) + "]" : "";
            if (failures.Count == 0)
            {
                reason = "ROUGH STONE FANFARE OK - six bands pairwise disjoint, panel fills the safe area " +
                         "on both axes, the one verb clears MinTouchPx by construction, copy is ASCII and " +
                         "differs first-vs-repeat, the grade reads without colour, the View/VM touch no " +
                         "inventory, the announce follows inv.AddEarned with every withheld path returning " +
                         "first, the composed exit holds for the dismiss AND still routes on refusal, and " +
                         "the post-first roll boundary is exact" + noteStr;
                return true;
            }
            reason = "rough-stone-fanfare FAIL x" + failures.Count + ": " +
                     string.Join(" | ", failures.ToArray()) + noteStr;
            return false;
        }

        private static void Case(List<string> failures, string name, Action body)
        {
            try { body(); }
            catch (Exception ex) { failures.Add("[" + name + "] THREW " + ex.GetType().Name + ": " + ex.Message); }
        }

        // =====================================================================
        //  CASE 1 - the six bands may never intersect
        // =====================================================================
        // The WO-1228 treasure-modal collisions (title over subtitle, CTA over the footer
        // sentence) all came from FLOWING elements down a rect. This screen BANDS instead,
        // and a band table is only a fix while the bands stay disjoint.
        private static void Case1_BandsDisjoint(List<string> failures)
        {
            var bands = RoughStoneFanfarePanel.Layout.Bands();
            var names = RoughStoneFanfarePanel.Layout.BandNames();
            if (bands.Length != names.Length)
            {
                failures.Add("[bands] Bands() has " + bands.Length + " entries but BandNames() has " +
                             names.Length + " - a failure message could name the wrong band");
                return;
            }
            for (int i = 0; i < bands.Length; i++)
            {
                if (bands[i].z <= bands[i].x || bands[i].w <= bands[i].y)
                    failures.Add("[bands] '" + names[i] + "' is degenerate (" + bands[i] + ") - it would " +
                                 "render as a zero-area rect and its content would vanish silently");
                for (int j = i + 1; j < bands.Length; j++)
                {
                    if (RoughStoneFanfarePanel.Layout.Intersect(bands[i], bands[j]))
                        failures.Add("[bands] '" + names[i] + "' " + bands[i] + " INTERSECTS '" + names[j] +
                                     "' " + bands[j] + " - two elements would paint over each other, which " +
                                     "is exactly the WO-1228 defect class");
                }
            }
        }

        // =====================================================================
        //  CASE 2 - "fill the screen, not 60% of it"
        // =====================================================================
        // Owner ruling 2026-09-07 01:14, verbatim: "i expect these images to fill the screen,
        // not 60% of it". The only inset allowed is the device safe area.
        private static void Case2_FillsTheScreen(List<string> failures, List<string> notes)
        {
            var min = RoughStoneFanfarePanel.Layout.PanelMin;
            var max = RoughStoneFanfarePanel.Layout.PanelMax;
            float w = max.x - min.x, h = max.y - min.y;

            const float Floor = 0.95f;   // the same floor the Manage fixture pins
            if (w < Floor)
                failures.Add("[fullscreen] the fanfare spans " + w.ToString("0.###") + " of the canvas WIDTH, " +
                             "under the " + Floor + " floor - this is the 60%-plate the owner rejected");
            if (h < Floor)
                failures.Add("[fullscreen] the fanfare spans " + h.ToString("0.###") + " of the canvas HEIGHT, " +
                             "under the " + Floor + " floor");
            if (min.x <= 0f || min.y <= 0f || max.x >= 1f || max.y >= 1f)
                failures.Add("[fullscreen] the panel reaches the raw canvas edge (" + min + ".." + max + ") - " +
                             "the safe-area inset is what keeps the obsidian border off a rounded corner " +
                             "and out of a notch; edge-to-edge is not the same as full-screen");
            notes.Add("panel spans " + w.ToString("0.###") + "x" + h.ToString("0.###") + " of the canvas " +
                      "(safe-area inset " + RoughStoneFanfarePanel.Layout.SafeAreaInsetF.ToString("0.###") + ")");
        }

        // =====================================================================
        //  CASE 3 - the one verb clears the touch floor BY CONSTRUCTION
        // =====================================================================
        // ClampMinTouch can rescue an undersized button, and that rescue is how a CTA walks
        // into the band above it (the hero-select failure). The band must not need it.
        private static void Case3_CtaMeetsTouchFloor(List<string> failures, List<string> notes)
        {
            float ctaH = RoughStoneFanfarePanel.Layout.CtaHeightPx(ReferenceCanvasH);
            if (ctaH < ElarionUiKit.MinTouchPx)
                failures.Add("[touch] the verb resolves " + ctaH.ToString("0.0") + "px tall at canvasH=" +
                             ReferenceCanvasH + ", under the " + ElarionUiKit.MinTouchPx.ToString("0") +
                             "px floor - ClampMinTouch would GROW it into the stars band");
            notes.Add("cta " + ctaH.ToString("0.0") + "px at canvasH=" + ReferenceCanvasH +
                      " (floor " + ElarionUiKit.MinTouchPx.ToString("0") + ")");
        }

        // =====================================================================
        //  CASE 4 - ASCII, and the two versions really differ
        // =====================================================================
        // Non-ASCII renders as tofu on device. And the WO asks for a BIG first-ever beat and a
        // shorter repeat: if the two compose identically, the "first stone" moment is not built.
        private static void Case4_CopyIsAsciiAndVersioned(List<string> failures)
        {
            var first = RoughStoneFanfareVM.Compose("ing_rough_stone", "Rough Stone", 2, true, "", "@");
            var repeat = RoughStoneFanfareVM.Compose("ing_rough_stone", "Rough Stone", 1, false, "", "@");

            AssertAscii(failures, "title", first.Title);
            AssertAscii(failures, "meaning-first", first.Meaning);
            AssertAscii(failures, "meaning-repeat", repeat.Meaning);
            AssertAscii(failures, "cta-first", first.CtaLabel);
            AssertAscii(failures, "cta-repeat", repeat.CtaLabel);
            AssertAscii(failures, "glyph", first.Glyph);
            AssertAscii(failures, "name", first.StoneName);

            if (first.Meaning == repeat.Meaning)
                failures.Add("[copy] the first-ever and repeat MEANING lines are identical - the guaranteed " +
                             "introduction reads exactly like a 15% re-drop, so nothing tells the player " +
                             "the Jeweler just opened");
            if (first.CtaLabel == repeat.CtaLabel)
                failures.Add("[copy] the first-ever and repeat VERBS are identical - the WO asks for " +
                             "'TAKE IT TO THE JEWELER' first and 'TAKE' afterwards");
            if (first.Meaning.IndexOf("Jeweler", StringComparison.Ordinal) < 0)
                failures.Add("[copy] the first-ever meaning line never names the JEWELER - the whole point " +
                             "of the beat is telling the player where the stone goes");
            if (repeat.Meaning.Length >= first.Meaning.Length)
                failures.Add("[copy] the repeat meaning (" + repeat.Meaning.Length + " chars) is not shorter " +
                             "than the first-ever one (" + first.Meaning.Length + ") - the WO asks for " +
                             "'the same panel, shorter copy'");

            // The art ask must survive: the canonical key is offered even when materials.json
            // authors nothing, so the day the PNG lands the panel picks it up with no code edit.
            bool hasPreferred = false;
            for (int i = 0; i < first.ArtKeys.Count; i++)
                if (first.ArtKeys[i] == RoughStoneFanfareVM.PreferredArtKey) hasPreferred = true;
            if (!hasPreferred)
                failures.Add("[copy] the VM no longer offers '" + RoughStoneFanfareVM.PreferredArtKey +
                             "' as an art candidate - filling the ART ASK would then require a code edit");
        }

        // =====================================================================
        //  CASE 5 - the grade reads without colour
        // =====================================================================
        // The owner is red/green colourblind (memory owner-colorblind-delegate-visual-creative).
        // The star row alone is a hue-and-shape read; the words are what always survives.
        private static void Case5_StarsReadWithoutColour(List<string> failures)
        {
            int max = DungeonRunGrade.MaxStars;
            for (int s = 0; s <= max; s++)
            {
                string row = RoughStoneFanfarePanel.StarRow(s, max);
                int filled = 0;
                for (int i = 0; i < row.Length; i++) if (row[i] == '*') filled++;
                if (filled != s)
                    failures.Add("[stars] StarRow(" + s + "," + max + ") = '" + row + "' shows " + filled +
                                 " filled star(s) - the row and the score disagree");
                AssertAscii(failures, "star-row", row);

                string words = RoughStoneFanfarePanel.StarWords(s, max);
                if (words.IndexOf(s.ToString(), StringComparison.Ordinal) < 0)
                    failures.Add("[stars] StarWords(" + s + "," + max + ") = '" + words + "' does not state " +
                                 "the score in digits - the colourblind-safe reading is gone");
                AssertAscii(failures, "star-words", words);
            }

            // Clamping, both ends: a score outside the rubric must not paint a fourth star or a
            // negative row (DungeonRunPayout already clamps, so a mismatch here means two clamps).
            if (RoughStoneFanfarePanel.StarRow(99, max).IndexOf('-') >= 0)
                failures.Add("[stars] an over-max score leaves an EMPTY star in the row - it should clamp full");
            if (RoughStoneFanfarePanel.StarRow(-4, max).IndexOf('*') >= 0)
                failures.Add("[stars] a negative score paints a FILLED star - it should clamp empty");
        }

        // =====================================================================
        //  CASE 6 - presentation never touches the objects (ARCHITECTURE_PRINCIPLES sec.2)
        // =====================================================================
        // The payout is the ONE producer. A View or VM that can add to the larder re-creates the
        // duplicate-authority bug WO-1112 spent a day undoing, and it would do it invisibly.
        private static void Case6_PresentationNeverGrants(List<string> failures)
        {
            string[] banned = { "VillageInventory", "AddEarned", "DungeonRunPayout.Push", "LastPolishScore" };
            foreach (string path in new[] { VmSrc, PanelSrc })
            {
                string src = ReadSource(path, failures);
                if (src == null) continue;
                foreach (string token in banned)
                {
                    if (src.IndexOf(token, StringComparison.Ordinal) >= 0)
                        failures.Add("[no-grant] " + Path.GetFileName(path) + " references '" + token +
                                     "' - presentation may render the payout, never produce or re-write it");
                }
            }
        }

        // =====================================================================
        //  CASE 7 - the announce can only follow a BANKED stone
        // =====================================================================
        // Source-order proof, because the runtime path needs a live inventory singleton and
        // UnityEngine.Random (see the header). What matters is structural and is readable:
        // the raise sits after inv.AddEarned, and every withheld/skipped/missed branch returns
        // before reaching it.
        private static void Case7_AnnounceFollowsTheBank(List<string> failures)
        {
            string src = ReadSource(ControllerSrc, failures);
            if (src == null) return;

            if (src.IndexOf("public static event System.Action<string, int, bool> RoughStoneGranted",
                            StringComparison.Ordinal) < 0)
                failures.Add("[announce-order] DungeonController no longer declares the RoughStoneGranted " +
                             "event - the fanfare has no way to learn a stone was paid");

            int add = src.IndexOf("inv.AddEarned(stoneId, 1);", StringComparison.Ordinal);
            int raise = src.IndexOf("RaiseRoughStoneGranted(stoneId, score, firstDungeonStone);",
                                    StringComparison.Ordinal);
            if (add < 0) { failures.Add("[announce-order] the AddEarned bank call is gone from GrantRunPayout"); return; }
            if (raise < 0) { failures.Add("[announce-order] GrantRunPayout no longer raises RoughStoneGranted"); return; }
            if (raise < add)
                failures.Add("[announce-order] the announce is raised BEFORE inv.AddEarned - the fanfare " +
                             "would celebrate a stone the player does not own yet");

            // The missed 15% roll returns before the announce. Its return sits between the roll
            // check and the bank, so proving the roll block precedes AddEarned proves the miss
            // path cannot fall through to the raise.
            int roll = src.IndexOf("ShouldAwardPostFirstStone(UnityEngine.Random.value)", StringComparison.Ordinal);
            if (roll < 0 || roll > add)
                failures.Add("[announce-order] the post-first drop-rate roll no longer guards the bank - a " +
                             "missed roll could reach AddEarned and the fanfare with it");

            // The listener call must carry its OWN Guard, so a throwing subscriber is not reported
            // as a failed payout after the stone is already banked.
            if (src.IndexOf("\"rough stone granted listeners\"", StringComparison.Ordinal) < 0)
                failures.Add("[announce-order] the RoughStoneGranted listeners are no longer wrapped in their " +
                             "own Guard - a throwing subscriber would be logged as 'grant dungeon run payout " +
                             "FAILED' when the grant in fact succeeded");
        }

        // =====================================================================
        //  CASE 8 - the exit WAITS, and is never a dead exit
        // =====================================================================
        // Two halves, and the second is the one that matters more: the beat may hold the route,
        // but a refusal or a throw must still send the player home.
        private static void Case8_ExitWaitsAndIsNeverDead(List<string> failures)
        {
            string src = ReadSource(ExitSrc, failures);
            if (src == null) return;

            if (src.IndexOf("DungeonController.RoughStoneGranted += onGranted", StringComparison.Ordinal) < 0 ||
                src.IndexOf("DungeonController.RoughStoneGranted -= onGranted", StringComparison.Ordinal) < 0)
                failures.Add("[exit-waits] the composed exit no longer subscribes/unsubscribes around " +
                             "GrantRunPayout - either the fanfare never fires, or handlers accumulate per run");

            if (src.IndexOf("RoughStoneFanfarePanel.Show(fanfare, RouteHomeNow)", StringComparison.Ordinal) < 0)
                failures.Add("[exit-waits] the composed exit no longer hands RouteHomeNow to the fanfare - " +
                             "the route would run underneath the beat again (the 10ms defect)");

            // The route must be REACHABLE without the panel. Guard.Try's false fallback plus the
            // unconditional RouteHomeNow() at the tail are what make a failed beat a missed
            // flourish instead of a trapped player.
            if (src.IndexOf("() => RoughStoneFanfarePanel.Show(fanfare, RouteHomeNow), false)",
                            StringComparison.Ordinal) < 0)
                failures.Add("[exit-waits] Show is no longer wrapped in a Guard.Try with a FALSE fallback - a " +
                             "throwing panel would leave the player in the dungeon with no way home");

            int guarded = src.IndexOf("bool owned = Guard.Try(Sys, \"show rough stone fanfare\"", StringComparison.Ordinal);
            int tail = guarded >= 0
                ? src.IndexOf("RouteHomeNow();", guarded, StringComparison.Ordinal)
                : -1;
            if (guarded < 0)
                failures.Add("[exit-waits] the guarded Show call is gone from ExecuteLeave");
            else if (tail < 0)
                failures.Add("[exit-waits] no unconditional RouteHomeNow() call follows the guarded Show - the " +
                             "fall-through that guarantees a way home when the beat refuses is gone");
            else if (src.IndexOf("private void RouteHomeNow()", StringComparison.Ordinal) < 0)
                failures.Add("[exit-waits] RouteHomeNow is no longer declared - the continuation the fanfare " +
                             "is handed does not exist");

            // The 12s materialise window is measured from the fade, so the stamp belongs on the
            // route, not at the top of ExecuteLeave where the fanfare can outlast it.
            if (Regex.Matches(src, Regex.Escape("PortalVFXController.NotifyReturnedThroughPortal()")).Count < 2)
                failures.Add("[exit-waits] the WO-893 materialise stamp is not on BOTH routes (rich + " +
                             "RouteHomeNow) - one of the two exits lost its arrival flourish");
        }

        // =====================================================================
        //  CASE 9 - the drop-rate boundary is exact
        // =====================================================================
        // Pure math, so it is honestly testable. A boundary that drifts changes how often the
        // fanfare fires after the guaranteed first stone.
        private static void Case9_PostFirstRollBoundary(List<string> failures)
        {
            float rate = DungeonController.PostFirstRoughStoneDropRate;
            if (rate <= 0f || rate >= 1f)
                failures.Add("[drop-rate] PostFirstRoughStoneDropRate is " + rate + " - outside (0,1) it is " +
                             "either a guaranteed farm or a dead economy");

            if (!DungeonController.ShouldAwardPostFirstStone(0f))
                failures.Add("[drop-rate] roll 0.0 does not award - the low end of the band is closed");
            if (DungeonController.ShouldAwardPostFirstStone(rate))
                failures.Add("[drop-rate] roll == the rate itself awards - the band must be half-open, or the " +
                             "real drop rate is fractionally above the authored one");
            if (DungeonController.ShouldAwardPostFirstStone(1f))
                failures.Add("[drop-rate] roll 1.0 awards - every run would pay");
            if (DungeonController.ShouldAwardPostFirstStone(-0.01f))
                failures.Add("[drop-rate] a negative roll awards - a bad caller would farm stones");
        }

        // =====================================================================
        //  CASE 10 - the SUCCESS path, through the real catalog
        // =====================================================================
        // Cases 4 and 5 prove the RULES using Compose with fixture strings. That proves the
        // refusal surface and nothing about what the player actually reads: the shipped copy
        // comes from For() -> MaterialCatalog -> materials.json. A suite that only ever tests
        // the fixture is the "failure-only acceptance" trap (memory
        // prove-the-success-path-not-just-the-refusal), so this case walks the real path.
        private static void Case10_RealCatalogPath(List<string> failures, List<string> notes)
        {
            var vm = RoughStoneFanfareVM.For(DungeonExclusiveItems.RoughStoneId, 2, true);
            if (vm == null) { failures.Add("[real-catalog] For() returned null"); return; }

            if (vm.StoneName == DungeonExclusiveItems.RoughStoneId)
                failures.Add("[real-catalog] the fanfare would title the stone with its RAW ID ('" +
                             vm.StoneName + "') - materials.json did not resolve, so the player reads " +
                             "a database key where a name belongs");
            if (string.IsNullOrEmpty(vm.Glyph))
                failures.Add("[real-catalog] no glyph resolved - with no art authored yet (the ART ASK), " +
                             "the fallback disc would be EMPTY");
            if (vm.Stars != 2)
                failures.Add("[real-catalog] score 2 composed as " + vm.Stars + " stars");
            if (!vm.FirstEver)
                failures.Add("[real-catalog] firstEver=true composed as false - the guaranteed " +
                             "introduction would render as an ordinary re-drop");
            AssertAscii(failures, "real-name", vm.StoneName);
            AssertAscii(failures, "real-glyph", vm.Glyph);
            notes.Add("real catalog: name='" + vm.StoneName + "' glyph='" + vm.Glyph + "' " +
                      "art candidates=" + vm.ArtKeys.Count);
        }

        // =====================================================================
        //  CASE 11 - "shown" means SHOWN
        // =====================================================================
        // The WO's acceptance is a device log reading FANFARE shown -> dismissed -> scene route,
        // in that order. Build() also runs from the headless capture and runs before the arbiter
        // can reject the open, so a "shown" line emitted there would forge the first token of the
        // proof. Build says "built"; only Show says "shown".
        private static void Case11_ShownIsEmittedOnceAndOnlyWhenShown(List<string> failures)
        {
            string src = ReadSource(PanelSrc, failures);
            if (src == null) return;

            int shown = Regex.Matches(src, Regex.Escape("\"ROUGH STONE FANFARE shown ")).Count;
            if (shown != 1)
                failures.Add("[trace-order] the 'ROUGH STONE FANFARE shown' line is emitted " + shown +
                             " time(s) - it must be emitted exactly once, and only after the arbiter " +
                             "accepts the open, or the acceptance log can be forged by a capture or " +
                             "by a rejected Show");

            int accept = src.IndexOf("s_onDismiss = onDismiss;\n\n            // THE FIRST TOKEN",
                                     StringComparison.Ordinal);
            if (accept < 0)
                accept = src.IndexOf("PanelManager.NotifyOpened(s_handle)", StringComparison.Ordinal);
            int shownAt = src.IndexOf("\"ROUGH STONE FANFARE shown ", StringComparison.Ordinal);
            if (accept >= 0 && shownAt >= 0 && shownAt < accept)
                failures.Add("[trace-order] 'shown' is emitted BEFORE the arbiter is asked - a " +
                             "battle-locked rejection would still log the beat as shown");

            if (src.IndexOf("\"ROUGH STONE FANFARE dismissed", StringComparison.Ordinal) < 0)
                failures.Add("[trace-order] the dismiss Step is gone - the middle token of the " +
                             "acceptance proof (shown -> dismissed -> route) cannot be read");
        }

        // ── helpers ──────────────────────────────────────────────────────────

        private static void AssertAscii(List<string> failures, string label, string text)
        {
            if (text == null) { failures.Add("[copy] " + label + " is NULL - it would render blank"); return; }
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] > 126 || (text[i] < 32 && text[i] != '\n'))
                {
                    failures.Add("[copy] " + label + " carries a non-ASCII character (U+" +
                                 ((int)text[i]).ToString("X4") + " at index " + i + ") - TMP renders it as " +
                                 "tofu on device");
                    return;
                }
            }
        }

        private static string ReadSource(string path, List<string> failures)
        {
            if (!File.Exists(path))
            {
                failures.Add("[source] " + path + " is MISSING - the WO-1596 beat cannot exist without it");
                return null;
            }
            return File.ReadAllText(path);
        }
    }
}
