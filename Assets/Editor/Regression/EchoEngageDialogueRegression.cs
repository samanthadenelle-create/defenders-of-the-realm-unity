// =============================================================================
// EchoEngageDialogueRegression [echo-engage-dialogue] (WO-1030) - the Echo
// engagement prompt's choices are NEVER clipped and its speaker resolves art.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression. Namespace: DeNelle.Editor.Regression.
//
// WHAT SHIPPED (owner screenshot 2026-08-16, Main_Castle_Overworld): the "Frost"
// Echo task prompt rendered with "Repair structures" sliced by the panel edge in
// landscape, and the medallion showed the generic silhouette instead of the Echo.
//   DEFECT A root: DialogueView.ResizeToContent clamped TEXT+OPTIONS as ONE sum
//     (Mathf.Clamp(contentPx, MinBodyPx, _maxBodyPx)); on a short landscape canvas
//     the ceiling cut the bottom of the OPTION LIST - an unreachable choice.
//   DEFECT B root: PetTaskController.SpeakerName returns DISPLAY names ("Frost"/
//     "Ember"/"Aether") but dialogues.json's speakers block had no records for
//     them, so the portrait resolver fell through to the silhouette even though
//     the Echoes' own rendered portraits exist (Resources/PetPortraits/pet-*).
//
// THE LAWS THIS PINS:
//   1 [def]       PetTaskController.BuildEngageDef (now static/public - the UI
//                 capture shoots the SAME builder) yields the 2-option prompt with
//                 both pet_task routings intact.
//   2 [speakers]  Every species' display speaker (and the "Your Echo" default) has
//                 a speakers-block record whose portrait path LOADS as a Sprite -
//                 the resolve is exercised end to end, not just the JSON's shape.
//   3 [reserve]   DialogueView reserves the OPTION band first (options are not
//                 optional): the reserve-first arithmetic + the kit option scroll
//                 zone + the MEASURED VerifyOptionsFit (shared UiSurfaceProbe
//                 arithmetic) are present, and the two banned old shapes - the
//                 48px sub-touch-floor row and the 0..0.60 fraction overlay - are
//                 gone by name.
//   4 [fit]       ARITHMETIC pin at the real landscape surfaces (1920x1080 and the
//                 Seeker's 2670x1200): a 2-OPTION node fits WITHOUT option
//                 scrolling, from the view's own constants regex-read out of the
//                 source (never a second hand-maintained copy).
//   5 [hygiene]   No embedded NUL in the touched sources (CLAUDE.md Sec. 0).
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
using System.Text.RegularExpressions;
using UnityEngine;

namespace DeNelle.Editor.Regression
{
    public static class EchoEngageDialogueRegression
    {
        private const string ViewSrc = "Assets/_Modules/HUD/DialogueView.cs";
        private const string CtrlSrc = "Assets/_Modules/Village/Pets/PetTaskController.cs";

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
                CheckEngageDef(failures);
                CheckSpeakerPortraits(failures);
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
            reason = "pet_engage def intact; Frost/Ember/Aether/'Your Echo' speaker portraits load as " +
                     "Sprites; DialogueView reserves the option band first (banned shapes absent, " +
                     "measured verify present); 2-option node fits scroll-free at 1920x1080 and " +
                     "2670x1200 from the source's own constants; no NULs.";
            return true;
        }

        // -- 1 [def] ----------------------------------------------------------
        private static void CheckEngageDef(List<string> failures)
        {
            var def = DeNelle.Village.PetTaskController.BuildEngageDef("ice-wolf");
            if (def == null) { failures.Add("[def] BuildEngageDef returned null"); return; }
            var root = def.EntryNode();
            if (root == null || root.Options == null || root.Options.Count != 2)
            {
                failures.Add("[def] entry node must carry exactly 2 options (got " +
                             (root == null || root.Options == null ? "none" : root.Options.Count.ToString()) + ")");
                return;
            }
            if (root.Lines == null || root.Lines.Count == 0 ||
                !string.Equals(root.Lines[0].Speaker, "Frost", StringComparison.Ordinal))
                failures.Add("[def] ice-wolf must speak as 'Frost' (got '" +
                             (root.Lines != null && root.Lines.Count > 0 ? root.Lines[0].Speaker : "<none>") + "')");
            foreach (var optionTarget in new[] { root.Options[0].Goto, root.Options[1].Goto })
            {
                var node = def.FindNode(optionTarget);
                bool routed = false;
                if (node != null && node.Commands != null)
                    foreach (var c in node.Commands)
                        if (c != null && string.Equals(c.Verb, "pet_task", StringComparison.Ordinal)) routed = true;
                if (!routed)
                    failures.Add("[def] option target '" + optionTarget +
                                 "' does not fire the pet_task verb - the choice would be a dead end");
            }
        }

        // -- 2 [speakers] -----------------------------------------------------
        private static void CheckSpeakerPortraits(List<string> failures)
        {
            // The display names come from the REAL builder, so a SpeakerName edit that
            // orphans a record fails here instead of shipping a silhouette.
            var species = new[] { "ice-wolf", "flame-pup", "aether-sprite", "unknown-species" };
            foreach (var s in species)
            {
                var def = DeNelle.Village.PetTaskController.BuildEngageDef(s);
                string speaker = def != null && def.EntryNode() != null && def.EntryNode().Lines.Count > 0
                    ? def.EntryNode().Lines[0].Speaker : null;
                if (string.IsNullOrEmpty(speaker))
                {
                    failures.Add("[speakers] no speaker for species '" + s + "'");
                    continue;
                }
                var rec = DeNelle.Core.Dialogue.DialogueCatalog.FindSpeaker(speaker);
                if (rec == null)
                {
                    failures.Add("[speakers] no speakers-block record for '" + speaker +
                                 "' (species '" + s + "') - the portrait falls to the silhouette (WO-1030 B)");
                    continue;
                }
                if (string.IsNullOrEmpty(rec.Portrait))
                {
                    failures.Add("[speakers] record '" + speaker + "' carries no portrait path");
                    continue;
                }
                var sprite = Resources.Load<Sprite>(rec.Portrait);
                if (sprite == null)
                    failures.Add("[speakers] portrait '" + rec.Portrait + "' for '" + speaker +
                                 "' does not load as a Sprite from Resources - the resolve chain " +
                                 "(Resources.Load<Sprite>) would fall to the silhouette");
            }
        }

        // -- 3 [reserve] ------------------------------------------------------
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

        // -- 4 [fit] ----------------------------------------------------------
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

        // -- 5 [hygiene] ------------------------------------------------------
        private static void CheckHygiene(List<string> failures)
        {
            foreach (var path in new[] { ViewSrc, CtrlSrc })
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
