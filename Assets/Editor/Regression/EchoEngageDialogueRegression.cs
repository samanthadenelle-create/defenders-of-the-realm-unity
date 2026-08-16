// =============================================================================
// EchoEngageDialogueRegression [echo-engage-dialogue] - INVERTED by WO-1031.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression. Namespace: DeNelle.Editor.Regression.
//
// WHAT CHANGED (owner rulings 2026-08-16: "remove this screen then", "it gets
// managed from the echo tab", "the wolf isnt frost or shouldnt be its the first
// Echo"; F8 seq 2432 + 2502): the Echo/pet world ENGAGEMENT PROMPT is DELETED.
// This suite was written 2026-08-15 under WO-1030 and asserted that prompt's
// shape - including that the ice-wolf speaks as "Frost". That is now false BY
// DESIGN, so the suite is INVERTED rather than deleted: a removal that nothing
// guards is a removal that quietly comes back.
//
// THE LAWS THIS PINS NOW:
//   1 [removed]   PetTaskController carries NO BuildEngageDef / SpeakerName /
//                 Engage / TickEngagement / ApplyEngagementChoice member and its
//                 source names neither "pet_engage" nor "Frost" - the prompt and
//                 the invented species->name table cannot return unnoticed.
//                 (Reflection, not just source-lint: a re-add in ANY form fails.)
//   2 [verb]      The "pet_task" dialogue verb is unregistered in
//                 DialogueCommandSink AND unproduced by the authored dialogue
//                 data - no orphan verb, no dead-end command.
//   3 [name]      No "Frost" speaker record survives in EITHER dialogues.json
//                 copy. The guide wolf is Echo #1, ALDWIN (EchoRosterCatalog) -
//                 "Frost" was never a character. Names are DERIVED from the
//                 roster catalog, never hand-authored (WO-1031 sec. 2b/2d).
//                 Aldwin/Alduin are DIFFERENT characters (DungeonLoreReadable
//                 Regression:74-91) - this suite never rewrites one into the other.
//   4 [reserve]   RETAINED FROM WO-1030 AND STILL LOAD-BEARING. DialogueView
//                 reserves the OPTION band first (options are not optional). This
//                 lives in the SHARED DialogueView (canon reference implementation,
//                 UI_BLINK_TEMPLATE_CANON.md sec. 8) - EVERY conversation in the
//                 game depends on it. Removing the pet prompt does not make the
//                 clipping bug go away anywhere else (WO-1031 sec. 6).
//   5 [fit]       RETAINED FROM WO-1030. ARITHMETIC pin at the real landscape
//                 surfaces (1920x1080 and the Seeker's 2670x1200): a 2-OPTION node
//                 fits WITHOUT option scrolling, derived from the view's own
//                 constants regex-read out of the source (never a second copy).
//   6 [hygiene]   No embedded NUL in the touched sources (CLAUDE.md Sec. 0).
//
// DO NOT "restore" laws 1-3 to their WO-1030 form. If a future ticket brings
// Echo tasking back to a world surface, it needs an owner ruling and a NEW pin -
// this one exists to make the removal falsifiable.
//
// SOURCE-LINT + data + arithmetic only: no scene, no play mode, so it runs in the
// headless DataRegression batch. Never throws.
//
// Markers: ECHO_ENGAGE_DIALOGUE_OK / ECHO_ENGAGE_DIALOGUE_FAIL.
// Standalone: run-unity-method
//   -Method DeNelle.Editor.Regression.EchoEngageDialogueRegression.RunAll
// Registered in DataRegression.RunAll as the "echo-engage-dialogue suite".
// =============================================================================
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;

namespace DeNelle.Editor.Regression
{
    public static class EchoEngageDialogueRegression
    {
        private const string ViewSrc = "Assets/_Modules/HUD/DialogueView.cs";
        private const string CtrlSrc = "Assets/_Modules/Village/Pets/PetTaskController.cs";
        private const string SinkSrc = "Assets/_Modules/Village/Tutorial/DialogueCommandSink.cs";

        private static readonly string[] DialogueJson =
        {
            "Assets/Resources/Data/Canonical/dialogue/dialogues.json",
            "Assets/StreamingAssets/Data/Canonical/dialogue/dialogues.json",
        };

        public static void RunAll()
        {
            if (Run(out string reason)) Debug.Log("ECHO_ENGAGE_DIALOGUE_OK - " + reason);
            else Debug.LogError("ECHO_ENGAGE_DIALOGUE_FAIL - " + reason);
        }

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            try
            {
                CheckPromptRemoved(failures);
                CheckVerbUnregistered(failures);
                CheckNoFrostSpeaker(failures);
                CheckReserveFirstSource(failures);
                CheckFitArithmetic(failures);
                CheckHygiene(failures);
            }
            catch (Exception ex)
            {
                failures.Add("[suite] threw: " + ex.GetType().Name + ": " + ex.Message);
            }

            if (failures.Count > 0)
            {
                reason = failures.Count + " failure(s): " + string.Join(" | ", failures.ToArray());
                return false;
            }
            reason = "WO-1031 removal holds: no engage-prompt members on PetTaskController, no " +
                     "invented species->name table, no 'pet_task' verb (sink or data), no 'Frost' " +
                     "speaker record in either dialogues.json; and the WO-1030 DialogueView laws " +
                     "still stand (reserve-first option band; 2-option node fits scroll-free at " +
                     "1920x1080 and 2670x1200 from the source's own constants); no NULs.";
            return true;
        }

        // -- 1 [removed] ------------------------------------------------------
        // The prompt is gone at the TYPE level (reflection - a re-add under any
        // signature fails) and at the SOURCE level (the tokens that carried the
        // invented naming scheme).
        private static void CheckPromptRemoved(List<string> failures)
        {
            const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic |
                                     BindingFlags.Static | BindingFlags.Instance;
            var t = typeof(DeNelle.Village.PetTaskController);
            foreach (var member in new[]
                     { "BuildEngageDef", "SpeakerName", "Engage", "TickEngagement", "ApplyEngagementChoice" })
            {
                if (t.GetMethod(member, All) != null)
                    failures.Add("[removed] PetTaskController." + member + " is back - WO-1031 deleted " +
                                 "the world engagement prompt; Echo tasking belongs to the Echo tab " +
                                 "(EchoCardView -> EchoAssignments), not a world-modal dialogue");
            }

            string src = ReadSrc(CtrlSrc, failures);
            if (src == null) return;
            foreach (var token in new[] { "pet_engage", "\"Frost\"", "\"Ember\"", "\"Aether\"" })
                if (src.IndexOf(token, StringComparison.Ordinal) >= 0)
                    failures.Add("[removed] PetTaskController.cs names " + token + " again - the " +
                                 "species->display-name table bypasses EchoRosterCatalog, the name " +
                                 "authority. The guide wolf is Echo #1, Aldwin (WO-1031 sec. 2b/2d)");
        }

        // -- 2 [verb] ---------------------------------------------------------
        private static void CheckVerbUnregistered(List<string> failures)
        {
            string sink = ReadSrc(SinkSrc, failures);
            if (sink != null && Regex.IsMatch(sink, "case\\s*\"pet_task\""))
                failures.Add("[verb] DialogueCommandSink registers the 'pet_task' verb again - it has " +
                             "no producer since WO-1031 removed BuildEngageDef; an unrouted verb is a " +
                             "dead-end choice");

            foreach (var path in DialogueJson)
            {
                string json = ReadSrc(path, failures);
                if (json == null) continue;
                if (json.IndexOf("pet_task", StringComparison.Ordinal) >= 0)
                    failures.Add("[verb] " + path + " emits the 'pet_task' verb - nothing consumes it " +
                                 "(WO-1031); route Echo tasking through the Echo tab instead");
            }
        }

        // -- 3 [name] ---------------------------------------------------------
        private static void CheckNoFrostSpeaker(List<string> failures)
        {
            foreach (var path in DialogueJson)
            {
                string json = ReadSrc(path, failures);
                if (json == null) continue;
                if (Regex.IsMatch(json, "\"name\"\\s*:\\s*\"Frost\""))
                    failures.Add("[name] " + path + " carries a \"Frost\" speaker record again. The " +
                                 "guide wolf is Echo #1, ALDWIN (EchoRosterCatalog.ByCount(1)) - the " +
                                 "tutorial already says 'Follow Aldwin to the gate'. 'Frost' was an " +
                                 "invented name and must not return (WO-1031 sec. 2d). Note Aldwin != " +
                                 "Alduin the Mournful - do not correct one into the other");
            }

            // Dual-copy law (CLAUDE.md): the Resources and StreamingAssets copies must match.
            try
            {
                if (File.Exists(DialogueJson[0]) && File.Exists(DialogueJson[1]))
                {
                    var a = File.ReadAllBytes(DialogueJson[0]);
                    var b = File.ReadAllBytes(DialogueJson[1]);
                    bool same = a.Length == b.Length;
                    for (int i = 0; same && i < a.Length; i++) if (a[i] != b[i]) same = false;
                    if (!same)
                        failures.Add("[name] the Resources and StreamingAssets dialogues.json copies " +
                                     "are NOT byte-identical - the dual-copy law was broken by the " +
                                     "speaker-block edit");
                }
            }
            catch (Exception ex) { failures.Add("[name] dual-copy compare: " + ex.Message); }
        }

        // -- 4 [reserve] ------------------------------------------------------
        private static void CheckReserveFirstSource(List<string> failures)
        {
            string src = ReadSrc(ViewSrc, failures);
            if (src == null) return;

            // Reserve-first machinery, by the names a refactor would have to keep or fail loudly.
            string[] required =
            {
                "optionsBandPx",                 // the reserved band height
                "optionsCapPx",                  // options-alone ceiling (scroll beyond it)
                "MakeScrollZone(optHost.transform", // options host is a kit scroll zone (visible affordance)
                "VerifyOptionsFit",              // measured post-settle outcome assert
                "UiSurfaceProbe.ScreenRectOf",   // SHARED rect arithmetic, not a re-derived copy
                "UiSurfaceProbe.IsUnmeasurableEnvironment", // named skip, never a silent pass
                "resize contentH=",              // the WO-1030 oracle trace stays (Sec. 12)
                "OPTIONS CLIPPED",               // the falsifiable Fail line
                "ElarionUiKit.MinTouchPx",       // rows seat at the kit touch floor
            };
            foreach (var token in required)
                if (src.IndexOf(token, StringComparison.Ordinal) < 0)
                    failures.Add("[reserve] DialogueView.cs lost '" + token + "' - the reserve-first " +
                                 "option layout (WO-1030 A) has been unwound or renamed silently");

            // The two shapes the defect wore, banned by name.
            if (src.IndexOf("minHeight = 48", StringComparison.Ordinal) >= 0)
                failures.Add("[reserve] the 48px option row is back - under the MinTouchPx touch floor");
            if (Regex.IsMatch(src, @"_optionsCol\s*\.\s*anchorMax\s*=\s*new\s+Vector2\s*\(\s*0\.95f\s*,\s*0\.60f\s*\)"))
                failures.Add("[reserve] the 0..0.60 fraction overlay options column is back - the " +
                             "content-fit ceiling can push its bottom rows off the panel again");
        }

        // -- 5 [fit] ----------------------------------------------------------
        // Replicates DialogueView's derivation FROM ITS OWN SOURCE CONSTANTS (regex-read,
        // never a second hand-maintained table) and asserts the WO-1030 acceptance line:
        // a 2-option node fits with ZERO option scrolling at the real landscape surfaces.
        private static void CheckFitArithmetic(List<string> failures)
        {
            string src = ReadSrc(ViewSrc, failures);
            if (src == null) return;

            float topPad = SrcConst(src, "TopPad", failures);
            float headerPx = SrcConst(src, "HeaderPx", failures);
            float gap = SrcConst(src, "Gap", failures);
            float bottomBandPx = SrcConst(src, "BottomBandPx", failures);
            float bottomMarginPx = SrcConst(src, "BottomMarginPx", failures);
            float minBodyPx = SrcConst(src, "MinBodyPx", failures);
            float optionRowGapPx = SrcConst(src, "OptionRowGapPx", failures);
            float optionsPadPx = SrcConst(src, "OptionsPadPx", failures);
            float optionsGapPx = SrcConst(src, "OptionsGapPx", failures);
            float minTouch = DeNelle.Core.UI.ElarionUiKit.MinTouchPx;
            if (minTouch < 112f)
                failures.Add("[fit] ElarionUiKit.MinTouchPx dropped below 112 (" + minTouch + ")");
            // Any missing const has already been reported by SrcConst (-1) - the arithmetic
            // below would be garbage, so stop here.
            if (topPad < 0f || headerPx < 0f || gap < 0f || bottomBandPx < 0f || bottomMarginPx < 0f ||
                minBodyPx < 0f || optionRowGapPx < 0f || optionsPadPx < 0f || optionsGapPx < 0f) return;

            // The authored panel band (BuildObsidianPanel y 0.20..0.62) and the HUD-safe rails
            // (under TargetInfo 0.655, above the action bar 0.155) as DialogueView derives them.
            const float anchorY0 = 0.20f, anchorY1 = 0.62f;
            const float safeTop = 0.655f, safeBottom = 0.155f;
            float cyFrac = (anchorY0 + anchorY1) * 0.5f;
            float halfSafe = Mathf.Min(safeTop - cyFrac, cyFrac - safeBottom);
            float maxFrac = Mathf.Max(anchorY1 - anchorY0, 2f * halfSafe);

            foreach (var surf in new[] { new Vector2(1920f, 1080f), new Vector2(2670f, 1200f) })
            {
                // CanvasScaler ScaleWithScreenSize, ref 1080x1920, match 0.5 (the view's canvas).
                float scale = Mathf.Pow(2f, Mathf.Lerp(
                    Mathf.Log(surf.x / 1080f, 2f), Mathf.Log(surf.y / 1920f, 2f), 0.5f));
                float canvasH = surf.y / scale;
                float maxPanelH = maxFrac * canvasH;
                float maxBodyPx = Mathf.Max(180f, maxPanelH - (topPad + headerPx + gap + bottomBandPx));
                // Options showing => Close hidden => band collapses; the ceiling reclaims it.
                float maxBodyNow = maxBodyPx + (bottomBandPx - bottomMarginPx);
                float optionsPx = 2f * minTouch + optionRowGapPx + 2f * optionsPadPx;
                float need = optionsPx + optionsGapPx + minBodyPx;
                if (need > maxBodyNow + 0.5f)
                    failures.Add("[fit] at " + surf.x + "x" + surf.y + " a 2-option node needs " +
                                 need.ToString("0.#") + "px (2 rows @" + minTouch + " + pads + min text) but " +
                                 "the options-paint ceiling is " + maxBodyNow.ToString("0.#") +
                                 "px - the default choice configuration would scroll or clip (WO-1030 " +
                                 "acceptance: zero scrolling for 2 options at every aspect)");
            }
        }

        // -- 6 [hygiene] ------------------------------------------------------
        private static void CheckHygiene(List<string> failures)
        {
            foreach (var path in new[] { ViewSrc, CtrlSrc, SinkSrc })
            {
                try
                {
                    if (!File.Exists(path)) { failures.Add("[hygiene] missing " + path); continue; }
                    var bytes = File.ReadAllBytes(path);
                    for (int i = 0; i < bytes.Length; i++)
                        if (bytes[i] == 0) { failures.Add("[hygiene] embedded NUL in " + path); break; }
                }
                catch (Exception ex) { failures.Add("[hygiene] " + path + ": " + ex.Message); }
            }
        }

        // -- helpers ----------------------------------------------------------
        private static string ReadSrc(string path, List<string> failures)
        {
            try
            {
                if (File.Exists(path)) return File.ReadAllText(path);
                failures.Add("[src] missing " + path);
            }
            catch (Exception ex) { failures.Add("[src] " + path + ": " + ex.Message); }
            return null;
        }

        /// <summary>Regex-read `const float NAME = <n>f` out of the view source so the pin
        /// follows the source instead of duplicating it (duplicated numbers go stale - the
        /// CLAUDE.md Sec. 2/5 drift lesson).</summary>
        private static float SrcConst(string src, string name, List<string> failures)
        {
            // NOTE: the view declares some of these in one multi-declarator statement
            // (`const float TopPad = 18f, HeaderPx = 108f, Gap = 10f;`), so the pattern is
            // `<name> = <n>f` with a word boundary - not `const float <name>`.
            var m = Regex.Match(src, @"\b" + Regex.Escape(name) + @"\s*=\s*([0-9]+(?:\.[0-9]+)?)f");
            if (m.Success && float.TryParse(m.Groups[1].Value,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float v)) return v;
            failures.Add("[fit] const float " + name + " not found in " + ViewSrc +
                         " - the fit pin cannot follow the source");
            return -1f;
        }
    }
}
