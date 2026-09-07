// =============================================================================
// PlacedStructureDoorRegression [placed-door] -- WO-2006 / OWNER_RULINGS_LOCKED §25.
// -----------------------------------------------------------------------------
// A PLACED STRUCTURE'S MOVE / UPGRADE / SELL CONTROLS MUST BE REACHABLE WITHOUT
// THE PLAYER ALREADY KNOWING ABOUT BUILD MODE.
//
// WHY THIS SUITE EXISTS (the class of bug, not one instance): on 2026-09-06 a
// friend playtesting the build put a palisade down by accident and could not
// remove it. Owner, verbatim: "he accidentally put a palisade down and he didn't
// mean to and now he has no way to move the Palisade... I think right now we lost
// that option when we simplified the UI."
//
// The controls were NEVER missing. BuildSelectionUI raises OnMoveRequested /
// OnUpgradeRequested / OnSellRequested from real buttons and BuildModeController
// subscribes all three; BeginMoveSelected, UpgradeSelected and SellSelected are
// all live. What was missing was a DOOR: the ONLY route in was "enter build mode,
// then tap the exact placed piece", a gesture nothing on screen ever named. Every
// oracle in the tree asked "do move/sell/upgrade work"; none asked "can a player
// who mis-tapped a wall FIND them". This is the same finding PanelDoorRegression
// records for panels, one layer down -- a WIRED VERB with no signpost is as dead
// to a player as a panel with no spawner.
//
// -----------------------------------------------------------------------------
// WHAT THIS ORACLE PROVES -- and, more importantly, WHAT IT DOES NOT
// -----------------------------------------------------------------------------
// It walks the door chain as SOURCE TEXT, link by link:
//
//   C1  BuildSelectionUI declares the three edit verbs as events.
//   C2  BuildModeController subscribes all three (the controls are live).
//   C3  BuildPaletteUI declares OnManagePlacedRequested and HANDS A MANAGE-PLACED
//       CALLBACK to the collection browser's Show -- an event nobody supplies is
//       not a door.
//   C4  BuildCollectionBrowser builds the card (BuildManagePlacedCard) from the
//       category grid renderer and invokes that callback.
//   C5  BuildModeController subscribes OnManagePlacedRequested, and the handler
//       BeginManagePlaced exists and lands on the EXISTING selection seam
//       (CancelArmed + ClearSelection) rather than minting a second one.
//   C6  THE DOOR IS NOT STOMPED BY ITS OWN DISARM (added WO-1581, 2026-09-07).
//       C6a: BuildPaletteUI.Collapse closes the collection browser, before its
//       '_canvas == null' early return. C6b: BeginManagePlaced calls Collapse
//       AFTER CancelArmed.
//
// !! WHY C6 HAD TO BE ADDED, AND WHAT IT SAYS ABOUT C1..C5. On 2026-09-07 the owner
// reported "manage buildings doesnt do anything" against build 2026.09.07.359076,
// and the tester had asked several times where MOVE went. C1..C5 were ALL GREEN.
// They were also all true: every link in the chain existed and fired. The device
// log shows the card's own trace line landing exactly as designed --
//   08:28:00.191  Manage Placed card TAPPED - closing the browser ...
//   08:28:00.193  Navigation: closed workspace 'Build Collections' to world
//   08:28:00.194  PanelManager: 'Build Collections' opened and verified visible
//   08:28:00.210  Build: ManagePlaced ENTERED ... 28 bodies are selectable
// -- and then the catalog stood back up over it, because CancelArmed ends in
// _palette.Expand() and WO-1273 had redefined Expand() to Show() a MODAL workspace.
// The same one-frame stomp buried the verb row on the ordinary select path too
// (08:28:02.424 SELECTS 'lumberyard' -> 08:28:02.429 'Build Collections' opened).
// So a door can be perfectly wired and still be dead, because something ELSE
// re-covers it a frame later. A chain oracle proves the chain; it cannot prove the
// panel at the end of the chain is the thing on screen. C6 pins the one seam that
// decides that, and the lesson generalises: when a suite is green and the player
// says the feature does nothing, suspect what runs AFTER the last link.
//
// ⛔ IT DOES NOT PROVE THE TAP LANDS AT RUNTIME. It is source-text analysis, like
// PanelDoorRegression: it proves the chain is wired end to end and that no link
// was quietly deleted. It cannot prove a raycast reaches a palisade's collider,
// that a card is on screen, or that a toast rendered. AutoPilotDriver.cs already
// carries the matching runtime failure string ("tap on structure never showed
// BuildSelectionUI"), which is the seam that would prove the other half; wiring a
// palisade case into that fleet is the follow-up, NOT something a green here
// stands in for. Say so out loud rather than letting a green read wider than it is.
//
// ⛔ C5 DELIBERATELY ASSERTS THE HANDLER REACHES ClearSelection/CancelArmed. That
// is the anti-duplication clause, and it is the whole reason the case is written
// this way: the cheap way to "fix" the tester's report would have been a second
// placed-structure screen with its own idea of what is selected. This repo's
// dominant failure mode is a second answer to a question that already had one
// (CLAUDE.md records it for spawn points, for the board, for the WO numbers, for
// the dependency table). If a future change makes BeginManagePlaced stop landing
// on the shared seam, this case must fail rather than shrug.
//
// Marker: PLACED_DOOR_OK / PLACED_DOOR_FAIL <case>.
// EXPECTED ON ARRIVAL (2026-09-06): RED on C3, C4 and C5 against the pre-WO-2006
// tree -- the palette had no OnManagePlacedRequested, the browser had no card, and
// the controller had no handler. C1 and C2 were green before the change and are
// asserted anyway, because they are what makes the door worth opening: if the
// controls themselves ever regress, a door onto them is worse than no door.
//
// THERE IS NO ALLOWLIST, on purpose. There is exactly one door chain here and a
// missing link is a player who cannot undo a mis-tap. Nothing about that is
// legitimately excusable, so no field exists to excuse it in.
//
// Wire (DataRegression.RunAll):
//   DeNelle.Core.Diagnostics.Guard.Try("Regression", "placed-door suite", () => { if (!DeNelle.Editor.PlacedStructureDoorRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[placed-door] " + r); });
// =============================================================================
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace DeNelle.Editor
{
    /// <summary>
    /// Source oracle: the move/upgrade/sell controls for an ALREADY-PLACED structure must
    /// be reachable through a signposted door, not only through the undocumented
    /// tap-a-piece-inside-build-mode gesture.
    /// </summary>
    public static class PlacedStructureDoorRegression
    {
        private const string Tag = "[placed-door]";

        // The brace characters as NAMED CONSTANTS, declared as one balanced pair.
        // CLAUDE.md sec.1's C# quality gate is a RAW brace count over the file, so an UNPAIRED
        // brace character inside a literal (ExtractMethodBody's walk needs three opening ones)
        // would fail the gate on a file that is structurally perfect. Naming them once, as a
        // pair, keeps the count honest -- do not inline these back as character literals, and
        // do not write an unpaired brace into a comment here either.
        private const char OpenBrace = '{';
        private const char CloseBrace = '}';

        private const string SelectionUiRel = "Assets/_Modules/Village/BuildMode/BuildSelectionUI.cs";
        private const string ControllerRel  = "Assets/_Modules/Village/BuildMode/BuildModeController.cs";
        private const string PaletteRel     = "Assets/_Modules/Village/BuildMode/BuildPaletteUI.cs";
        private const string BrowserRel     = "Assets/_Modules/Village/BuildMode/BuildCollectionBrowser.cs";

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var log = new StringBuilder("=== PlacedStructureDoorRegression (WO-2006 / ruling 25) ===\n");
            try
            {
                CheckDoorChain(failures, log);
            }
            catch (Exception ex)
            {
                failures.Add(Tag + " suite threw " + ex.GetType().Name + ": " + ex.Message);
            }

            if (failures.Count == 0)
            {
                reason = "PLACED_DOOR_OK a placed structure's Move/Upgrade/Sell controls are wired AND " +
                         "reachable through the Manage Placed door (BuildCollectionBrowser card -> " +
                         "BuildPaletteUI.OnManagePlacedRequested -> BuildModeController.BeginManagePlaced -> " +
                         "the existing SelectStructure/BuildSelectionUI seam)";
                Debug.Log(reason + "\n" + log);
                return true;
            }

            reason = "PLACED_DOOR_FAIL " + string.Join(" | ", failures);
            Debug.LogError(reason + "\n" + log);
            return false;
        }

        // =====================================================================
        private static void CheckDoorChain(List<string> failures, StringBuilder log)
        {
            string root = Directory.GetParent(Application.dataPath.Replace('\\', '/')).FullName.Replace('\\', '/');

            string selection  = ReadStripped(root, SelectionUiRel, failures, log);
            string controller = ReadStripped(root, ControllerRel, failures, log);
            string palette    = ReadStripped(root, PaletteRel, failures, log);
            string browser    = ReadStripped(root, BrowserRel, failures, log);
            if (selection == null || controller == null || palette == null || browser == null) return;

            // ── C1: the three edit verbs exist as events on the selection panel ──
            foreach (string verb in new[] { "OnMoveRequested", "OnUpgradeRequested", "OnSellRequested" })
            {
                if (Has(selection, @"event\s+Action\s+" + verb))
                    log.AppendLine("[C1] " + SelectionUiRel + " declares event " + verb + " -- OK");
                else
                    failures.Add(Tag + " C1: " + SelectionUiRel + " no longer declares 'event Action " + verb +
                                 "'. The placed-structure edit control itself is gone; a door onto it would " +
                                 "lead nowhere.");
            }

            // ── C2: the controller subscribes all three (the verbs are LIVE) ──
            foreach (string verb in new[] { "OnMoveRequested", "OnUpgradeRequested", "OnSellRequested" })
            {
                if (Has(controller, verb + @"\s*\+="))
                    log.AppendLine("[C2] " + ControllerRel + " subscribes " + verb + " -- OK");
                else
                    failures.Add(Tag + " C2: " + ControllerRel + " does not subscribe '" + verb +
                                 "'. The button would raise an event nothing handles -- the exact silent " +
                                 "failure CLAUDE.md sec.12 forbids.");
            }

            // ── C3: the palette declares the door event AND supplies it to the browser ──
            if (Has(palette, @"event\s+Action\s+OnManagePlacedRequested"))
                log.AppendLine("[C3a] " + PaletteRel + " declares event OnManagePlacedRequested -- OK");
            else
                failures.Add(Tag + " C3a: " + PaletteRel + " declares no 'event Action OnManagePlacedRequested'. " +
                             "Ruling 25 requires a MANAGE PLACED entry; without this event the card has no way " +
                             "to reach the controller.");

            if (Has(palette, @"OnManagePlacedRequested\s*\?\.\s*Invoke"))
                log.AppendLine("[C3b] " + PaletteRel + " raises OnManagePlacedRequested into the browser's Show -- OK");
            else
                failures.Add(Tag + " C3b: " + PaletteRel + " never raises OnManagePlacedRequested. An event that " +
                             "nothing invokes is a door that never opens -- the PanelDoorRegression finding " +
                             "one layer down.");

            // ── C4: the browser actually builds the card and invokes the callback ──
            if (Has(browser, @"private\s+void\s+BuildManagePlacedCard"))
                log.AppendLine("[C4a] " + BrowserRel + " declares BuildManagePlacedCard -- OK");
            else
                failures.Add(Tag + " C4a: " + BrowserRel + " has no BuildManagePlacedCard. The owner asked for " +
                             "\"one more card, which is just move or manage\" (ruling 25); the category grid is " +
                             "where it lives.");

            if (Has(browser, @"BuildManagePlacedCard\s*\(\s*grid\s*\)"))
                log.AppendLine("[C4b] " + BrowserRel + " calls BuildManagePlacedCard(grid) from the category grid -- OK");
            else
                failures.Add(Tag + " C4b: " + BrowserRel + " declares the card builder but never calls it from the " +
                             "category grid. A card nobody renders is not a door.");

            if (Has(browser, @"_managePlaced\s*\?\.\s*Invoke"))
                log.AppendLine("[C4c] " + BrowserRel + " invokes the managePlaced callback -- OK");
            else
                failures.Add(Tag + " C4c: " + BrowserRel + " never invokes its managePlaced callback. The card would " +
                             "close the browser onto nothing, which is a WORSE defect than the missing door.");

            // ── C5: the controller answers the door, on the EXISTING selection seam ──
            if (Has(controller, @"OnManagePlacedRequested\s*\+="))
                log.AppendLine("[C5a] " + ControllerRel + " subscribes OnManagePlacedRequested -- OK");
            else
                failures.Add(Tag + " C5a: " + ControllerRel + " does not subscribe OnManagePlacedRequested -- the " +
                             "door's far side is unwired.");

            string handler = ExtractMethodBody(controller, "BeginManagePlaced");
            if (handler == null)
            {
                failures.Add(Tag + " C5b: " + ControllerRel + " declares no BeginManagePlaced handler.");
            }
            else
            {
                log.AppendLine("[C5b] " + ControllerRel + " declares BeginManagePlaced (" + handler.Length + " chars) -- OK");

                // THE ANTI-DUPLICATION CLAUSE. See the header: the handler must land on the
                // shared selection seam, never mint a second one.
                bool clears = handler.Contains("ClearSelection") && handler.Contains("CancelArmed");
                if (clears)
                    log.AppendLine("[C5c] BeginManagePlaced lands on the shared seam (CancelArmed + ClearSelection) -- OK");
                else
                    failures.Add(Tag + " C5c: BeginManagePlaced does not call BOTH CancelArmed and ClearSelection. " +
                                 "The door must hand the session to the EXISTING selection state; a handler that " +
                                 "skips them is either leaking a half-armed CREATE entry into EDIT, or has grown a " +
                                 "second answer to \"what is selected\" -- this repo's dominant failure mode.");

                // -- C6b: ...AND IS NOT STOMPED BY THAT SEAM. --
                // CancelArmed ends in _palette.Expand(). Since WO-1273 Expand() means "Show()
                // the modal collection browser", so the seam C5c mandates ALSO re-opens the
                // catalog. The door therefore has to collapse it back, AFTER CancelArmed has
                // had its say -- ordering is the whole assertion, so this compares indices
                // rather than merely looking for the call.
                int iCancel   = handler.IndexOf("CancelArmed", StringComparison.Ordinal);
                int iCollapse = handler.IndexOf("Collapse", StringComparison.Ordinal);
                if (iCollapse > iCancel && iCancel >= 0)
                    log.AppendLine("[C6b] BeginManagePlaced collapses the shop AFTER CancelArmed re-expanded it -- OK");
                else
                    failures.Add(Tag + " C6b: BeginManagePlaced does not Collapse the palette after CancelArmed. " +
                                 "CancelArmed ends in _palette.Expand(), which since WO-1273 RE-OPENS the modal " +
                                 "Build Collections browser -- so the card's own Close() is undone in the same " +
                                 "frame and the player sees the catalog again. That is exactly the 2026-09-07 " +
                                 "report \"manage buildings doesnt do anything\" (device log 08:28:00.191 TAPPED " +
                                 "-> 08:28:00.194 'Build Collections' opened). Order matters: a Collapse placed " +
                                 "BEFORE CancelArmed is overwritten by it and is not a fix.");
            }

            // -- C6a: Collapse is the INVERSE of Expand, or the verbs are unreachable --
            // This is the seam, and it guards MORE than the door: BuildModeController.
            // SelectStructure relies on the same Collapse to get the catalog off the
            // Move/Upgrade/Sell panel it just raised. When it stopped doing that, tapping a
            // placed structure rendered the verb row and buried it under the catalog one
            // frame later (device log 08:28:02.424 SELECTS 'lumberyard' -> 08:28:02.429
            // 'Build Collections' opened and verified visible), which is the tester's
            // repeated "I cannot find MOVE". A green C1..C5 chain proves nothing if the
            // panel at the end of it is covered.
            string collapse = ExtractMethodBody(palette, "Collapse");
            if (collapse == null)
            {
                failures.Add(Tag + " C6a: " + PaletteRel + " declares no Collapse method.");
            }
            else
            {
                int iBrowser = collapse.IndexOf("_collectionBrowser", StringComparison.Ordinal);
                int iGuard   = collapse.IndexOf("_canvas == null", StringComparison.Ordinal);
                if (iBrowser < 0)
                    failures.Add(Tag + " C6a: BuildPaletteUI.Collapse never touches _collectionBrowser. Expand() " +
                                 "OPENS that modal workspace (WO-1273); a Collapse that only hides legacy _canvas " +
                                 "chrome leaves the catalog standing over BuildSelectionUI, so MOVE / UPGRADE / " +
                                 "SELL are rendered and unreachable.");
                else if (iGuard >= 0 && iBrowser > iGuard)
                    failures.Add(Tag + " C6a: BuildPaletteUI.Collapse closes the browser only AFTER its " +
                                 "'_canvas == null' early return. The legacy dock canvas is built lazily and " +
                                 "Show() deactivates it, so that guard can skip the one line that still matters. " +
                                 "Close the browser BEFORE the guard.");
                else
                    log.AppendLine("[C6a] " + PaletteRel + " Collapse closes the collection browser before the " +
                                   "_canvas guard -- Expand/Collapse are symmetric again -- OK");
            }
        }

        // =====================================================================
        //  Helpers
        // =====================================================================

        /// <summary>
        /// Read a file and strip comments + string literals BEFORE matching. This repo leaves
        /// long explanatory prose naming the very identifiers an oracle looks for -- including
        /// the comments written beside this door -- so a naive grep would read a tombstone as
        /// a door. Same rule PanelDoorRegression states for the same reason.
        /// </summary>
        private static string ReadStripped(string root, string rel, List<string> failures, StringBuilder log)
        {
            string abs = root + "/" + rel;
            if (!File.Exists(abs))
            {
                failures.Add(Tag + " missing file: " + rel + " (the door chain cannot be evaluated)");
                return null;
            }
            string src = File.ReadAllText(abs);
            src = Regex.Replace(src, @"/\*.*?\*/", " ", RegexOptions.Singleline);
            src = Regex.Replace(src, @"//[^\n]*", " ");
            src = Regex.Replace(src, "@\"(?:[^\"]|\"\")*\"", " \"\" ");
            src = Regex.Replace(src, "\"(?:\\\\.|[^\"\\\\\\n])*\"", " \"\" ");
            log.AppendLine("read " + rel + " (" + src.Length + " chars after comment/string strip)");
            return src;
        }

        private static bool Has(string src, string pattern) => Regex.IsMatch(src, pattern);

        /// <summary>
        /// Return the brace-matched body of <paramref name="method"/>, or null. Brace counting
        /// is safe here because comments and string literals are already stripped, so no brace
        /// inside prose or a literal can unbalance the walk.
        /// </summary>
        private static string ExtractMethodBody(string src, string method)
        {
            var m = Regex.Match(src, @"\b" + Regex.Escape(method) + @"\s*\([^)]*\)\s*"
                                     + Regex.Escape(OpenBrace.ToString()));
            if (!m.Success) return null;
            int i = src.IndexOf(OpenBrace, m.Index);
            if (i < 0) return null;
            int depth = 0;
            for (int j = i; j < src.Length; j++)
            {
                if (src[j] == OpenBrace) depth++;
                else if (src[j] == CloseBrace)
                {
                    depth--;
                    if (depth == 0) return src.Substring(i, j - i + 1);
                }
            }
            return null;
        }
    }
}
