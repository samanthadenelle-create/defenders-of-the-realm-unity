// =============================================================================
// DailyQuestEmptyStateRegression [daily-quest-empty] (WO-879) - the daily-quest
// empty state is ONE fact, owned by the ViewModel, rendered ONCE by the View.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression. Namespace: DeNelle.Editor.Regression.
//
// WHAT SHIPPED (capture Builds/ui-capture/DailyQuestHud_2340x1080.png, 2026-08-04):
// the same message rendered TWICE, in two mismatched chromes - an italic
// "No daily quests today." in the black list well AND a parchment card reading
// "No daily quests today / Fresh quests arrive with the new day." - with ~half the
// panel dead black. It was not a stray duplicate widget: DailyQuestHud.Repaint
// branched on vm.IsEmpty and then made TWO render calls, and it AUTHORED both
// strings itself. The View was deciding state and owning copy - the MVVM defect
// the owner named ("the VM owns the single empty-state; the View stops rendering
// it twice"). The View also carried a SECOND, self-derived empty state (a
// "Select a quest" prompt behind a View-computed `found` flag).
//
// THE LAW THIS PINS: one producer, one render.
//   1 [vm-owns]    DailyQuestVM exposes a single EmptyState fact (Active +
//                  Headline + Detail), assigned at exactly ONE site in Rebuild,
//                  and IsEmpty is a PROJECTION of it - never a second
//                  `_quests.Count == 0`. Two producers is the bug's shape.
//   2 [vm-live]    Asserted on a LIVE VM built by reflection (null source => an
//                  empty set, no scene / no DailyQuestService needed): Active is
//                  true, the copy is non-empty, IsEmpty agrees with Active, and
//                  TryGetSelected is false. The strings are the VM's, so they can
//                  be read here - which is itself the proof they are not the
//                  View's.
//   3 [view-once]  DailyQuestHud reads vm.EmptyState at exactly ONE site, calls
//                  its ONE empty-state builder from exactly ONE site, no longer
//                  calls BuildParchmentDetailEmpty at all, never tests emptiness
//                  itself (no vm.IsEmpty / .Count == 0), and holds NO empty-state
//                  copy of its own (the two shipped literals are banned by name).
//   4 [bands]      The empty-state bands are FIXED reference pixels at/above a TMP
//                  line box (WO-841/852: a fraction band collapses with the well
//                  and TMP culls the line whole), the quest row is at/above the kit
//                  touch floor, and the stack fits the measured well by a mile.
//   5 [hygiene]    No non-ASCII inside any string literal of either file (tofu on
//                  the shipped TMP font) and no embedded NUL (CLAUDE.md Sec.0).
//
// SOURCE-LINT + reflection only: no scene, no play mode, so it runs in the headless
// DataRegression batch. Never throws.
//
// Markers: DAILY_QUEST_EMPTY_STATE_OK / DAILY_QUEST_EMPTY_STATE_FAIL.
// Standalone: run-unity-method
//   -Method DeNelle.Editor.Regression.DailyQuestEmptyStateRegression.RunAll
// Registered in DataRegression.RunAll as the "daily-quest-empty suite".
// =============================================================================
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;

namespace DeNelle.Editor.Regression
{
    public static class DailyQuestEmptyStateRegression
    {
        private const string ViewSrc = "Assets/_Modules/HUD/DailyQuestHud.cs";
        private const string VmSrc = "Assets/_Modules/HUD/DailyQuestVM.cs";

        private const string ViewType = "DeNelle.HUD.DailyQuestHud";
        private const string VmType = "DeNelle.HUD.DailyQuestVM";
        private const string KitType = "DeNelle.Core.UI.ElarionUiKit";
        private const string UiType = "DeNelle.Core.UI.ElarionUi";

        /// <summary>The TMP line box multiplier the bands are budgeted from (~1.25em).</summary>
        private const float LineBoxMul = 1.25f;

        // The MEASURED FrameQuest DETAIL well at the capture aspect (same derivation as
        // RumorBoardLayoutRegression's header: 1080x1920 reference canvas, MatchWidthOrHeight
        // 0.5). This panel opens at (0.06,0.12)-(0.94,0.88), which is WIDER and SHORTER than
        // the rumor board's rect, so the number below is the conservative one: the shortest
        // well the empty stack ever has to fit into. The stack is ~126 px, so the assertion
        // has a large margin on purpose - it is a floor, not a fit.
        private const float DetailWellH_2340 = 300f;

        // The two literals the View used to own. If either reappears in the View, the copy
        // has been re-typed outside the VM and a second message is one edit away.
        private static readonly string[] BannedViewCopy =
        {
            "No daily quests",
            "Fresh quests arrive",
            "Select a quest",
        };

        public static void RunAll()
        {
            if (Run(out string reason)) Debug.Log("DAILY_QUEST_EMPTY_STATE_OK - " + reason);
            else Debug.LogError("DAILY_QUEST_EMPTY_STATE_FAIL: " + reason);
        }

        /// <summary>Covenant contract (DataRegression-shaped). Never throws.</summary>
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var notes = new List<string>();
            try
            {
                Case(failures, "vm-owns", () => Case1_VmOwnsOneEmptyState(failures, notes));
                Case(failures, "vm-live", () => Case2_LiveVmProducesIt(failures, notes));
                Case(failures, "view-once", () => Case3_ViewRendersItExactlyOnce(failures, notes));
                Case(failures, "bands", () => Case4_FixedPixelBands(failures, notes));
                Case(failures, "hygiene", () => Case5_AsciiAndNul(failures, notes));
            }
            catch (Exception ex)
            {
                failures.Add("[suite] THREW " + ex.GetType().Name + ": " + ex.Message);
            }

            string noteStr = notes.Count > 0 ? " [notes: " + string.Join("; ", notes) + "]" : "";
            if (failures.Count == 0)
            {
                reason = "DAILY QUEST EMPTY STATE OK - the empty state is ONE DailyQuestVM fact " +
                         "(EmptyState: Active + Headline + Detail) assigned at a single site, IsEmpty " +
                         "projects it rather than recomputing it, a live null-source VM produces it, and " +
                         "DailyQuestHud reads it once and renders it once in one chrome (no second " +
                         "column, no View-authored copy, no View-side emptiness test), on fixed-pixel " +
                         "bands at/above a TMP line box" + noteStr;
                return true;
            }
            reason = "daily-quest-empty FAIL x" + failures.Count + ": " + string.Join(" | ", failures) + noteStr;
            return false;
        }

        private static void Case(List<string> failures, string name, Action body)
        {
            try { body(); }
            catch (Exception ex) { failures.Add("[" + name + "] THREW " + ex.GetType().Name + ": " + ex.Message); }
        }

        // =====================================================================
        //  CASE 1 - the VM owns ONE empty-state, produced at ONE site
        // =====================================================================
        private static void Case1_VmOwnsOneEmptyState(List<string> failures, List<string> notes)
        {
            Type vm = FindType(VmType);
            if (vm == null)
            {
                failures.Add("[vm-owns] " + VmType + " not found - the daily-quest ViewModel was renamed " +
                             "or removed; re-point this oracle (it is the only guard on the single empty-state)");
                return;
            }

            var prop = vm.GetProperty("EmptyState", BindingFlags.Public | BindingFlags.Instance);
            if (prop == null)
            {
                failures.Add("[vm-owns] DailyQuestVM.EmptyState is gone - the empty-state fact must live on the " +
                             "VM. Without it the View has to decide emptiness and author the copy itself, which " +
                             "is exactly how WO-879 rendered the same message in two mismatched columns.");
                return;
            }

            Type info = prop.PropertyType;
            foreach (string member in new[] { "Active", "Headline", "Detail" })
                if (info.GetField(member, BindingFlags.Public | BindingFlags.Instance) == null)
                    failures.Add("[vm-owns] " + info.Name + "." + member + " is missing - the one fact must carry " +
                                 "BOTH the flag and the copy, or the copy drifts back into the View");

            if (vm.GetMethod("TryGetSelected", BindingFlags.Public | BindingFlags.Instance) == null)
                failures.Add("[vm-owns] DailyQuestVM.TryGetSelected is gone - the selection LOOKUP belongs to the " +
                             "VM. When the View re-derived it, the 'nothing selected' branch became a SECOND " +
                             "View-authored empty state.");

            // --- source law: exactly ONE producer ---
            string raw = ReadSource(VmSrc, failures, "[vm-owns]");
            if (raw == null) return;
            string src = StripComments(raw);

            int assigns = Regex.Matches(src, @"\b_empty\s*=(?!=)").Count;
            if (assigns != 1)
                failures.Add("[vm-owns] the empty-state is assigned at " + assigns + " site(s) in DailyQuestVM " +
                             "(expected exactly 1, in Rebuild) - two producers of one fact is the WO-879 defect " +
                             "in its general form");

            if (!Regex.IsMatch(src, @"public\s+bool\s+IsEmpty\s*=>\s*_empty\s*\.\s*Active"))
                failures.Add("[vm-owns] IsEmpty no longer PROJECTS _empty.Active - if it recomputes emptiness " +
                             "(e.g. _quests.Count == 0) there are two answers to one question and they can drift");

            int copyDecls = Regex.Matches(src, @"EmptyHeadlineText\s*=\s*""").Count +
                            Regex.Matches(src, @"EmptyDetailText\s*=\s*""").Count;
            if (copyDecls != 2)
                failures.Add("[vm-owns] expected exactly 2 authored empty-state string constants in DailyQuestVM " +
                             "(headline + detail), found " + copyDecls + " - the copy is the VM's, declared once");

            notes.Add("VM: 1 producer, IsEmpty projects it, 2 authored strings");
        }

        // =====================================================================
        //  CASE 2 - a LIVE VM actually produces it (no scene, no service)
        // =====================================================================
        private static void Case2_LiveVmProducesIt(List<string> failures, List<string> notes)
        {
            Type vm = FindType(VmType);
            if (vm == null) { failures.Add("[vm-live] " + VmType + " not found"); return; }

            ConstructorInfo ctor = null;
            foreach (var c in vm.GetConstructors(BindingFlags.Public | BindingFlags.Instance))
                if (c.GetParameters().Length == 2) { ctor = c; break; }
            if (ctor == null)
            {
                failures.Add("[vm-live] DailyQuestVM(ISource, Action) constructor not found - this oracle builds a " +
                             "VM with a NULL source (which is an empty set) to assert the empty-state for real; " +
                             "re-point it rather than dropping the live check");
                return;
            }

            object live = ctor.Invoke(new object[] { null, null });
            try
            {
                object info = vm.GetProperty("EmptyState", BindingFlags.Public | BindingFlags.Instance)
                                .GetValue(live);
                Type t = info.GetType();
                bool active = (bool)t.GetField("Active").GetValue(info);
                string headline = t.GetField("Headline").GetValue(info) as string;
                string detail = t.GetField("Detail").GetValue(info) as string;

                if (!active)
                    failures.Add("[vm-live] a VM over an EMPTY quest set reports EmptyState.Active=false - the " +
                                 "panel would render no empty state at all");
                if (string.IsNullOrEmpty(headline))
                    failures.Add("[vm-live] EmptyState.Headline is blank on an empty set - the View would have " +
                                 "nothing to render and someone would re-type the copy into the View");
                if (string.IsNullOrEmpty(detail))
                    failures.Add("[vm-live] EmptyState.Detail is blank on an empty set");

                object isEmpty = vm.GetProperty("IsEmpty", BindingFlags.Public | BindingFlags.Instance).GetValue(live);
                if (!(isEmpty is bool b) || b != active)
                    failures.Add("[vm-live] IsEmpty (" + isEmpty + ") disagrees with EmptyState.Active (" + active +
                                 ") - one fact, two answers");

                var tryGet = vm.GetMethod("TryGetSelected", BindingFlags.Public | BindingFlags.Instance);
                if (tryGet != null)
                {
                    object[] args = { null };
                    if (tryGet.Invoke(live, args) is bool sel && sel)
                        failures.Add("[vm-live] TryGetSelected returned TRUE on an empty set - the View would " +
                                     "build a detail card for a quest that does not exist");
                }

                if (headline != null && detail != null)
                    notes.Add("live empty-state: '" + headline + "' / '" + detail + "'");
            }
            finally
            {
                var dispose = vm.GetMethod("Dispose", BindingFlags.Public | BindingFlags.Instance,
                    null, Type.EmptyTypes, null);
                if (dispose != null) { try { dispose.Invoke(live, null); } catch { } }
            }
        }

        // =====================================================================
        //  CASE 3 - the View reads it ONCE and renders it ONCE
        // =====================================================================
        private static void Case3_ViewRendersItExactlyOnce(List<string> failures, List<string> notes)
        {
            string raw = ReadSource(ViewSrc, failures, "[view-once]");
            if (raw == null) return;
            string src = StripComments(raw);

            int reads = Regex.Matches(src, @"_vm\s*\.\s*EmptyState").Count;
            if (reads != 1)
                failures.Add("[view-once] DailyQuestHud reads vm.EmptyState at " + reads + " site(s) (expected " +
                             "exactly 1) - the View asks the VM once and renders what it says; a second read is " +
                             "a second render waiting to happen");

            int builderCalls = Regex.Matches(src, @"\bBuildEmptyState\s*\(").Count -
                               Regex.Matches(src, @"void\s+BuildEmptyState\s*\(").Count;
            if (builderCalls != 1)
                failures.Add("[view-once] the empty state is built from " + builderCalls + " call site(s) " +
                             "(expected exactly 1) - the WO-879 capture is what TWO call sites look like");

            if (Regex.IsMatch(src, @"BuildParchmentDetailEmpty\s*\("))
                failures.Add("[view-once] DailyQuestHud calls BuildParchmentDetailEmpty again - that shared " +
                             "detail-zone empty card was the SECOND rendering of the same message (the parchment " +
                             "half of the duplicated capture). The panel has one empty state, in one chrome.");

            if (Regex.IsMatch(src, @"_vm\s*\.\s*IsEmpty"))
                failures.Add("[view-once] DailyQuestHud tests vm.IsEmpty - the View must not branch on emptiness " +
                             "at all; it renders the VM's EmptyState fact, which carries Active AND the copy");
            if (Regex.IsMatch(src, @"Quests\s*\.\s*Count\s*==\s*0") || Regex.IsMatch(src, @"quests\s*\.\s*Count\s*==\s*0"))
                failures.Add("[view-once] DailyQuestHud computes emptiness itself (quests.Count == 0) - that is " +
                             "the View duplicating what the VM owns, which is the defect, not a shortcut");

            foreach (string banned in BannedViewCopy)
                if (src.IndexOf("\"" + banned, StringComparison.Ordinal) >= 0)
                    failures.Add("[view-once] DailyQuestHud carries the empty-state literal '" + banned + "' - the " +
                                 "copy belongs to DailyQuestVM. Two authors of one message is how the panel " +
                                 "shipped 'No daily quests today.' next to 'No daily quests today'.");

            // The selection lookup is the VM's, or the 'nothing selected' prompt comes back.
            if (!Regex.IsMatch(src, @"_vm\s*\.\s*TryGetSelected\s*\("))
                failures.Add("[view-once] DailyQuestHud no longer routes the selection through vm.TryGetSelected - " +
                             "when the View re-derives 'is anything selected' it grows a second empty state");
            if (Regex.IsMatch(src, @"item\s*\.\s*Id\s*==\s*_vm\s*\.\s*SelectedId"))
                failures.Add("[view-once] DailyQuestHud re-derives the selected quest by scanning vm.Quests - that " +
                             "lookup is the VM's (TryGetSelected)");

            // Strict MVVM: the View never reaches for the quest services (ratchet armed).
            foreach (string forbidden in new[] { "DailyQuestService", "DailyQuestCatalog" })
                if (Regex.IsMatch(src, @"\b" + forbidden + @"\s*\."))
                    failures.Add("[view-once] DailyQuestHud touches " + forbidden + " directly - the View is a " +
                                 "read-only consumer of DailyQuestVM ([ui-mvvm] ratchet armed)");

            // Style-everything-obsidian: the panel routes through the shared kit.
            if (src.IndexOf("ElarionUiKit", StringComparison.Ordinal) < 0)
                failures.Add("[view-once] DailyQuestHud does not go through ElarionUiKit - the " +
                             "UiObsidianConformanceRegression hand-rolled-uGUI law");

            // The dead-black half: while empty there is ONE well, not two.
            if (!Regex.IsMatch(src, @"CollapseWellsForEmpty\s*\("))
                failures.Add("[view-once] the empty path no longer collapses the two wells into one - the capture's " +
                             "'~half the panel dead black' comes from leaving the dark list well up with nothing " +
                             "in it beside a parchment message");

            notes.Add("View: 1 EmptyState read, 1 builder call, 0 banned literals");
        }

        // =====================================================================
        //  CASE 4 - fixed-pixel bands, at/above a line box, fitting the well
        // =====================================================================
        private static void Case4_FixedPixelBands(List<string> failures, List<string> notes)
        {
            Type view = FindType(ViewType);
            Type kit = FindType(KitType);
            Type ui = FindType(UiType);
            if (view == null || kit == null || ui == null)
            {
                failures.Add("[bands] DailyQuestHud / ElarionUiKit / ElarionUi type not found - cannot read the " +
                             "band budget this oracle pins");
                return;
            }

            float minTouch = ConstFloat(kit, "MinTouchPx", failures, "[bands]");
            float fontBody = ConstFloat(ui, "FontBody", failures, "[bands]");
            float fontLabel = ConstFloat(ui, "FontLabel", failures, "[bands]");
            float headPx = ConstFloat(view, "EmptyHeadlinePx", failures, "[bands]");
            float detailPx = ConstFloat(view, "EmptyDetailPx", failures, "[bands]");
            float gapPx = ConstFloat(view, "EmptyGapPx", failures, "[bands]");
            if (minTouch <= 0f || fontBody <= 0f || fontLabel <= 0f || headPx <= 0f || detailPx <= 0f) return;

            float headLine = fontBody * LineBoxMul;
            float detailLine = fontLabel * LineBoxMul;

            if (headPx < headLine)
                failures.Add("[bands] EmptyHeadlinePx=" + headPx + " is shorter than one FontBody line box (" +
                             headLine + ") - TMP culls the headline whole and the empty panel reads blank, " +
                             "which is worse than the duplicate it replaced");
            if (detailPx < detailLine)
                failures.Add("[bands] EmptyDetailPx=" + detailPx + " is shorter than one FontLabel line box (" +
                             detailLine + ")");

            float stack = headPx + gapPx + detailPx;
            if (stack > DetailWellH_2340)
                failures.Add("[bands] the empty stack (" + stack + " px) does not fit the measured detail well at " +
                             "2340x1080 (" + DetailWellH_2340 + " ref px)");

            float rowPx = ConstFloat(view, "RowPx", failures, "[bands]", required: false);
            if (rowPx > 0f && rowPx < minTouch)
                failures.Add("[bands] RowPx=" + rowPx + " is below the kit touch floor " + minTouch +
                             " - a quest row IS the select target");

            // Source law: the empty stack is sized in PIXELS, never as a fraction of the well.
            string raw = ReadSource(ViewSrc, failures, "[bands]");
            if (raw == null) return;
            string src = StripComments(raw);

            if (!Regex.IsMatch(src, @"sizeDelta\s*=\s*new\s+Vector2\s*\(\s*0f\s*,[^;]*EmptyHeadlinePx"))
                failures.Add("[bands] the empty-state block is no longer a FIXED-PIXEL stack " +
                             "(sizeDelta ... EmptyHeadlinePx) - a fraction band scales with the well and culls " +
                             "the moment the aspect changes (WO-841 / WO-852)");
            if (!Regex.IsMatch(src, @"minHeight\s*=\s*px"))
                failures.Add("[bands] the empty-state bands lost their fixed minHeight - a band that can be " +
                             "squeezed below its line box is a culled line waiting to happen");
            if (Regex.IsMatch(src, @"(EmptyHeadlinePx|EmptyDetailPx)\s*[/*]\s*(rect|well|parent|_detail)", RegexOptions.IgnoreCase))
                failures.Add("[bands] an empty-state band is being scaled against the well - bands are fixed " +
                             "reference pixels, full stop");

            notes.Add("bands " + headPx + "/" + detailPx + " px (line boxes " + headLine + "/" + detailLine +
                      "), stack " + stack + " px in a " + DetailWellH_2340 + " px well");
        }

        // =====================================================================
        //  CASE 5 - ASCII string literals + no NUL (mount-garble guard)
        // =====================================================================
        private static void Case5_AsciiAndNul(List<string> failures, List<string> notes)
        {
            foreach (string path in new[] { ViewSrc, VmSrc })
            {
                string raw = ReadSource(path, failures, "[hygiene]");
                if (raw == null) continue;

                if (raw.IndexOf('\0') >= 0)
                    failures.Add("[hygiene] " + path + " contains an embedded NUL byte (mount-garble, " +
                                 "CLAUDE.md Sec.0) - the compile gate rejects this");

                // Only STRING LITERALS have to be ASCII: those are what TMP renders (a non-ASCII
                // glyph is tofu on the shipped font). Comments in this codebase use em dashes.
                foreach (Match m in Regex.Matches(StripComments(raw), "\"([^\"\\\\\\n]|\\\\.)*\""))
                {
                    foreach (char c in m.Value)
                    {
                        if (c <= 127) continue;
                        failures.Add("[hygiene] " + path + " has a NON-ASCII character (U+" +
                                     ((int)c).ToString("X4") + ") inside the string literal " + m.Value +
                                     " - it renders as tofu on the shipped TMP font");
                        break;
                    }
                }
            }
            notes.Add("ASCII + NUL checked on both files");
        }

        // =====================================================================
        //  helpers
        // =====================================================================

        /// <summary>Read a public const/static numeric field by reflection (no asmdef reference).</summary>
        private static float ConstFloat(Type t, string name, List<string> failures, string tag, bool required = true)
        {
            var f = t.GetField(name, BindingFlags.Public | BindingFlags.NonPublic |
                                     BindingFlags.Static | BindingFlags.FlattenHierarchy);
            if (f == null)
            {
                if (required)
                    failures.Add(tag + " " + t.Name + "." + name + " does not exist - the layout constant this " +
                                 "oracle pins was renamed or removed; re-point it rather than deleting the guard");
                return 0f;
            }
            object v = f.GetValue(null);
            if (v is float fv) return fv;
            if (v is int iv) return iv;
            if (v is double dv) return (float)dv;
            failures.Add(tag + " " + t.Name + "." + name + " is not a numeric constant");
            return 0f;
        }

        private static string ReadSource(string path, List<string> failures, string tag)
        {
            try
            {
                if (File.Exists(path)) return File.ReadAllText(path);
                failures.Add(tag + " source not found: " + path);
                return null;
            }
            catch (Exception ex)
            {
                failures.Add(tag + " could not read " + path + ": " + ex.Message);
                return null;
            }
        }

        /// <summary>Blank out // and block comments so a lesson written in prose (which quotes the
        /// old duplicated copy verbatim) can never fail a source law.</summary>
        private static string StripComments(string src)
        {
            string noBlock = Regex.Replace(src, @"/\*.*?\*/", " ", RegexOptions.Singleline);
            return Regex.Replace(noBlock, @"//[^\n]*", " ");
        }

        private static Type FindType(string fullName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type t = null;
                try { t = asm.GetType(fullName, false); }
                catch { }
                if (t != null) return t;
            }
            return null;
        }
    }
}
