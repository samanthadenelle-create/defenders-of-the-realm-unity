// =============================================================================
// ManageOneHeadingRegression [manage-one-heading] -- WO-1443.
// -----------------------------------------------------------------------------
// ONE HEADING PER MANAGE SCREEN, AND ONE PLACE THAT PAINTS IT.
//
// Owner felt-test 2026-09-06 on build 2026.09.06.358245, verbatim:
//   "first UI screenahot that is off" ... "remove the manage army and sub line
//    replace the manage top"
// and, on the same screen in the same session:
//   "dont need the bottom line, close button is enough"
//
// WHAT SHE SAW. The Manage/ARMY screen stacked THREE headings down its top:
//   MANAGE                        <- the host chrome's panel title
//   MANAGE / ARMY                 <- ManageWorkspacePanel's header band title
//   Every troop, unlocked or not. <- ManageWorkspacePanel's header band subtitle
// and then gave roughly 40% of the remaining screen to a bordered SELECTION band
// holding one hint sentence, because that band was reserved whether or not anything
// was selected.
//
// WHY A SUITE AND NOT A NOTE. The defect is not "ARMY has a spare line". It is that
// the breadcrumb had TWO renderers - a host title and a body band - and the second
// one is shared by BUILD, ARMY and RESEARCH alike. The general form is what this
// suite pins: the MODEL owns the breadcrumb string, the HOST binds it once, and the
// renderer paints no copy of it on any tab. A note in a work order would have been
// true on the day it was written and stale by the next lane (CLAUDE.md 2 / 5 / 16).
//
// ⚠ WHAT A GREEN HERE DOES NOT PROVE. This is a SOURCE oracle. It proves no second
// heading is AUTHORED; it cannot prove what a device renders, and it cannot prove
// the longest breadcrumb ("MANAGE / RESEARCH / SCHOOL", 26 chars against the old
// "MANAGE" at 6) actually seats in the title zone. WO-1443 acceptance asks for a
// headless capture with the PNG opened; until that exists the FIT is UNVERIFIED and
// is recorded as such in ManageScreenPanel.ApplyWorkspaceTitle's summary.
//
// SELF-TESTED FIRST. Every pattern below is driven against a fixture it MUST match
// and a fixture it MUST NOT, before any of them is trusted against the real files.
// A regex nobody has seen match is not an oracle.
//
// Marker: MANAGE_ONE_HEADING_OK / MANAGE_ONE_HEADING_FAIL <case>.
// EXPECTED ON ARRIVAL: GREEN.
//
// Wire (DataRegression.RunAll):
//   DeNelle.Core.Diagnostics.Guard.Try("Regression", "manage-one-heading suite", () => { if (!DeNelle.Editor.Regression.ManageOneHeadingRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[manage-one-heading] " + r); });
// =============================================================================
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace DeNelle.Editor.Regression
{
    /// <summary>Source oracle: a Manage screen renders its heading exactly once.</summary>
    public static class ManageOneHeadingRegression
    {
        private const string Tag = "[manage-one-heading]";

        // PINNED PATHS. If one is renamed the suite goes RED rather than quietly
        // scanning nothing - the hollow pass RegressionMarkerRegression RULE 4 stops.
        private const string RendererRel = "Assets/_Modules/Core/Manage/ManageWorkspacePanel.cs";
        private const string ContractRel = "Assets/_Modules/Core/Manage/ManageViewContract.cs";
        private const string HostRel = "Assets/_Modules/Village/UI/Manage/ManageScreenPanel.cs";
        private const string VmRel = "Assets/_Modules/Village/UI/Manage/ManageScreenVM.cs";

        // Floors that make "the file is there" mean something.
        private const int RendererMinLines = 300;
        private const int ContractMinLines = 150;
        private const int HostMinLines = 2000;
        private const int VmMinLines = 2000;

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var log = new StringBuilder("=== ManageOneHeadingRegression (WO-1443) ===\n");
            try
            {
                SelfTest(failures, log);

                string renderer = ReadPinned(RendererRel, RendererMinLines, failures, log);
                string contract = ReadPinned(ContractRel, ContractMinLines, failures, log);
                string host = ReadPinned(HostRel, HostMinLines, failures, log);
                string vm = ReadPinned(VmRel, VmMinLines, failures, log);

                CaseRendererPaintsNoHeading(renderer, failures, log);
                CaseContractHasNoSubtitle(contract, failures, log);
                CaseHostBindsTheBreadcrumb(host, failures, log);
                CaseModelOwnsTheBreadcrumb(vm, failures, log);
                CaseSelectionBandCollapses(renderer, vm, failures, log);
            }
            catch (Exception ex)
            {
                failures.Add(Tag + " suite threw " + ex.GetType().Name + ": " + ex.Message);
            }

            if (failures.Count > 0)
            {
                reason = "MANAGE_ONE_HEADING_FAIL " + string.Join(" | ", failures.ToArray());
                Debug.LogError(log.ToString() + "\n" + reason);
                return false;
            }
            reason = "MANAGE_ONE_HEADING_OK one heading per Manage screen; host binds the model's " +
                     "breadcrumb, the renderer paints no copy, the selection band collapses when empty";
            Debug.Log(log.ToString() + "\n" + reason);
            return true;
        }

        // ── CASE [one-heading-renderer] ───────────────────────────────────────
        // The shared renderer paints NO breadcrumb and NO sub line, on any tab.
        //
        // REVERT RECIPE (RED): put `ElarionUiKit.Label(band, vm.HeaderTitle, ...)` back
        // into ManageWorkspacePanel.BuildHeader.
        private static void CaseRendererPaintsNoHeading(string src, List<string> failures, StringBuilder log)
        {
            if (string.IsNullOrEmpty(src)) return;
            string code = DeNelle.Editor.Regression.RegressionSourceText.StripComments(src);

            if (Regex.IsMatch(code, HeaderTitleUse))
                failures.Add(Tag + "[one-heading-renderer] " + RendererRel + " reads HeaderTitle again. " +
                             "The breadcrumb belongs to the HOST's panel title (ManageScreenPanel." +
                             "ApplyWorkspaceTitle); a body copy under it is the three-stacked-headings " +
                             "defect the owner reported on 2026-09-06, and it returns on BUILD and " +
                             "RESEARCH at the same time because this renderer is shared");

            if (Regex.IsMatch(code, HeaderSubtitleUse))
                failures.Add(Tag + "[one-heading-renderer] " + RendererRel + " reads HeaderSubtitle. " +
                             "That field is DELETED from the contract by owner ruling (WO-1443 section 1) " +
                             "- every line it carried restated something already on screen");

            log.AppendLine("[one-heading-renderer] renderer paints neither breadcrumb nor sub line");
        }

        // ── CASE [one-heading-contract] ───────────────────────────────────────
        // The contract keeps ONE heading field. HeaderSubtitle is gone, not merely
        // unread: a composed-but-unpainted value is the duplicated state that invites
        // the next seat to render it again.
        //
        // REVERT RECIPE (RED): add `public string HeaderSubtitle;` back to ManageWorkspaceVM.
        private static void CaseContractHasNoSubtitle(string src, List<string> failures, StringBuilder log)
        {
            if (string.IsNullOrEmpty(src)) return;
            string code = DeNelle.Editor.Regression.RegressionSourceText.StripComments(src);

            if (!Regex.IsMatch(code, HeaderTitleField))
                failures.Add(Tag + "[one-heading-contract] " + ContractRel + " no longer declares " +
                             "HeaderTitle. The model must still OWN the breadcrumb string - the host " +
                             "binds it and never composes one of its own");

            if (Regex.IsMatch(code, HeaderSubtitleField))
                failures.Add(Tag + "[one-heading-contract] " + ContractRel + " declares HeaderSubtitle " +
                             "again. WO-1443 section 1 deletes it; keeping a field nothing reads is how " +
                             "the sub line comes back");

            log.AppendLine("[one-heading-contract] HeaderTitle present, HeaderSubtitle absent");
        }

        // ── CASE [one-heading-host] ───────────────────────────────────────────
        // The host binds the MODEL's breadcrumb into the panel title, through one
        // method, and never types a breadcrumb of its own.
        //
        // REVERT RECIPE (RED): change RenderWorkspace back to `_workspace.Bind(_vm.ComposeWorkspace());`
        // with no ApplyWorkspaceTitle call.
        private static void CaseHostBindsTheBreadcrumb(string src, List<string> failures, StringBuilder log)
        {
            if (string.IsNullOrEmpty(src)) return;
            string code = DeNelle.Editor.Regression.RegressionSourceText.StripComments(src);

            if (!Regex.IsMatch(code, HostBindsTitle))
                failures.Add(Tag + "[one-heading-host] " + HostRel + " does not bind " +
                             "ManageWorkspaceVM.HeaderTitle into the panel title. The screen falls back " +
                             "to the bare word MANAGE and the player loses the only breadcrumb left");

            if (!Regex.IsMatch(code, HostHasApplyMethod))
                failures.Add(Tag + "[one-heading-host] " + HostRel + " has no ApplyWorkspaceTitle - the " +
                             "title is being set from more than one site again");

            // A literal breadcrumb typed into the host would be a SECOND authority on how to
            // spell "MANAGE / ARMY". String literals are deliberately left intact here.
            if (Regex.IsMatch(code, HostTypesABreadcrumb))
                failures.Add(Tag + "[one-heading-host] " + HostRel + " types a 'MANAGE / ...' breadcrumb " +
                             "literal. ManageScreenVM.HeaderTitle is the one authority on that string; a " +
                             "second copy is exactly the duplicated state that produced this defect");

            log.AppendLine("[one-heading-host] host binds the model breadcrumb through ApplyWorkspaceTitle");
        }

        // ── CASE [one-heading-model] ──────────────────────────────────────────
        // The model composes the breadcrumb and no longer composes a sub line.
        //
        // REVERT RECIPE (RED): restore `private string HeaderSubtitle(ManageNavEntry nav)` in
        // ManageScreenVM.
        private static void CaseModelOwnsTheBreadcrumb(string src, List<string> failures, StringBuilder log)
        {
            if (string.IsNullOrEmpty(src)) return;
            string code = DeNelle.Editor.Regression.RegressionSourceText.StripComments(src);

            if (!Regex.IsMatch(code, VmComposesTitle))
                failures.Add(Tag + "[one-heading-model] " + VmRel + " no longer composes HeaderTitle " +
                             "into the workspace VM");

            if (Regex.IsMatch(code, VmComposesSubtitle))
                failures.Add(Tag + "[one-heading-model] " + VmRel + " composes a HeaderSubtitle again. " +
                             "Owner ruling WO-1443 section 1 removed the sub line from every Manage tab, " +
                             "not only from ARMY");

            log.AppendLine("[one-heading-model] model composes the breadcrumb only");
        }

        // ── CASE [empty-band-collapses] ───────────────────────────────────────
        // WO-1443 section 3. With the hint sentence deleted the selection band has
        // nothing to hold, so it must COLLAPSE - not stand as an empty bordered box.
        //
        // REVERT RECIPE (RED): drop the `hasSelection` test from ManageWorkspacePanel.Build so the
        // detail card is reserved on a grid screen, or restore the EmptyText sentence in
        // ManageScreenVM.FillActiveTab.
        // ⚠ The old recipe named `SelectionFullPx`, the 392px reservation. That constant is DELETED:
        // WO-1443 gave the detail screen the whole body and a two-column layout, so there is no
        // fixed card height left to restore. The recipe names the live shape instead.
        private static void CaseSelectionBandCollapses(string renderer, string vm,
            List<string> failures, StringBuilder log)
        {
            if (!string.IsNullOrEmpty(renderer))
            {
                string code = DeNelle.Editor.Regression.RegressionSourceText.StripComments(renderer);
                if (!Regex.IsMatch(code, RendererCollapsesSelection))
                    failures.Add(Tag + "[empty-band-collapses] " + RendererRel + " no longer gates the " +
                                 "selection band on Selection.Visible. An unselected screen reserves the " +
                                 "band and shows a bordered box with nothing in it - roughly 40% of the " +
                                 "owner's capture, doing less than the tiles it displaced");
            }

            if (!string.IsNullOrEmpty(vm))
            {
                string code = DeNelle.Editor.Regression.RegressionSourceText.StripComments(vm);
                if (Regex.IsMatch(code, VmHintSentence))
                    failures.Add(Tag + "[empty-band-collapses] " + VmRel + " authors the selection hint " +
                                 "sentence again. Owner, 2026-09-06: \"dont need the bottom line, close " +
                                 "button is enough\" - the sentence explained what tapping a tile does, " +
                                 "which the tile already demonstrates");
            }

            log.AppendLine("[empty-band-collapses] selection band is gated on Visible; no hint sentence authored");
        }

        // ── PATTERNS ──────────────────────────────────────────────────────────
        // Every one is driven RED and GREEN against a fixture in SelfTest below.
        private const string HeaderTitleUse = @"\bvm\s*\.\s*HeaderTitle\b";
        private const string HeaderSubtitleUse = @"\bHeaderSubtitle\b";
        private const string HeaderTitleField = @"\bpublic\s+string\s+HeaderTitle\s*;";
        private const string HeaderSubtitleField = @"\bpublic\s+string\s+HeaderSubtitle\s*;";
        private const string HostBindsTitle = @"ApplyWorkspaceTitle\s*\(\s*\w+\s*\.\s*HeaderTitle\s*\)";
        private const string HostHasApplyMethod = @"\bvoid\s+ApplyWorkspaceTitle\s*\(";
        private const string HostTypesABreadcrumb = @"""MANAGE\s*/";
        // ⚠ WIDENED 2026-09-06. It required `HeaderTitle = HeaderTitle(` on ONE line - the model
        // calling its own composer straight into the VM initialiser. WO-1443's detail screens made
        // that a two-step: the composer's result goes into a local, a DETAIL screen overrides it
        // with the selected item's own name (mockup panels 3/5/9 are headed OUTRIDER, LUMBER MILL,
        // ARCHER - never a breadcrumb), and the local is then assigned. The MODEL still owns the
        // string end to end; only the statement shape changed.
        // The pattern now accepts either form, so it still fails if the View starts composing the
        // title - which is the thing this case exists to catch.
        private const string VmComposesTitle = @"HeaderTitle\s*=\s*(HeaderTitle\s*\(|headerTitle\b)";
        private const string VmComposesSubtitle = @"HeaderSubtitle\s*=\s*HeaderSubtitle\s*\(";
        private const string RendererCollapsesSelection =
            @"hasSelection\s*=\s*tab\s*!=\s*null\s*&&\s*tab\s*\.\s*Selection\s*!=\s*null\s*&&\s*tab\s*\.\s*Selection\s*\.\s*Visible";
        private const string VmHintSentence = @"Pick one to see what it does";

        // ── SELF-TEST ─────────────────────────────────────────────────────────
        // Each pattern must MATCH its positive fixture and MISS its negative one. A
        // pattern that cannot be shown to bite is not evidence of anything.
        private static void SelfTest(List<string> failures, StringBuilder log)
        {
            var cases = new[]
            {
                new[] { "HeaderTitleUse", HeaderTitleUse,
                        "var t = ElarionUiKit.Label(band, vm.HeaderTitle, 0.5f);",
                        "ApplyWorkspaceTitle(workspaceVm.HeaderTitle);" },
                new[] { "HeaderSubtitleUse", HeaderSubtitleUse,
                        "var s = vm.HeaderSubtitle;", "var s = vm.HeaderTitle;" },
                new[] { "HeaderTitleField", HeaderTitleField,
                        "public string HeaderTitle;", "public string Title;" },
                new[] { "HeaderSubtitleField", HeaderSubtitleField,
                        "public string HeaderSubtitle;", "public string HeaderTitle;" },
                new[] { "HostBindsTitle", HostBindsTitle,
                        "ApplyWorkspaceTitle(workspaceVm.HeaderTitle);", "ApplyWorkspaceTitle(\"MANAGE\");" },
                new[] { "HostHasApplyMethod", HostHasApplyMethod,
                        "private void ApplyWorkspaceTitle(string headerTitle)", "private void ShowWorkspace()" },
                new[] { "HostTypesABreadcrumb", HostTypesABreadcrumb,
                        "_workspaceTitle.text = \"MANAGE / ARMY\";", "_workspaceTitle.text = \"MANAGE\";" },
                new[] { "VmComposesTitle", VmComposesTitle,
                        "HeaderTitle = headerTitle,", "Tabs = tabs," },
                new[] { "VmComposesSubtitle", VmComposesSubtitle,
                        "HeaderSubtitle = HeaderSubtitle(nav),", "HeaderTitle = HeaderTitle(nav)," },
                new[] { "RendererCollapsesSelection", RendererCollapsesSelection,
                        "bool hasSelection = tab != null && tab.Selection != null && tab.Selection.Visible;",
                        "bool hasSelection = true;" },
                new[] { "VmHintSentence", VmHintSentence,
                        "EmptyText = \"Pick one to see what it does, what it costs and what you can do.\"",
                        "EmptyText = null" },
            };

            int proven = 0;
            for (int i = 0; i < cases.Length; i++)
            {
                string name = cases[i][0], pattern = cases[i][1], hit = cases[i][2], miss = cases[i][3];
                if (!Regex.IsMatch(hit, pattern))
                {
                    failures.Add(Tag + "[one-heading-self-test] pattern '" + name +
                                 "' does not match its own positive fixture - it cannot be evidence");
                    continue;
                }
                if (Regex.IsMatch(miss, pattern))
                {
                    failures.Add(Tag + "[one-heading-self-test] pattern '" + name +
                                 "' also matches its NEGATIVE fixture - it would fire on correct code");
                    continue;
                }
                proven++;
            }
            log.AppendLine("[one-heading-self-test] patterns proven " + proven + "/" + cases.Length);
        }

        // ── helpers ───────────────────────────────────────────────────────────

        private static string ReadPinned(string rel, int minLines, List<string> failures, StringBuilder log)
        {
            string full = Path.Combine(GetProjectRoot(), rel.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(full))
            {
                failures.Add(Tag + "[one-heading-files-exist] " + rel + " is missing - this suite would " +
                             "otherwise scan nothing and report green");
                return null;
            }
            string src = File.ReadAllText(full);
            int lines = CountLines(src);
            if (lines < minLines)
            {
                failures.Add(Tag + "[one-heading-files-exist] " + rel + " is only " + lines +
                             " lines (floor " + minLines + ") - a stub must not pass this suite");
                return null;
            }
            log.AppendLine("[one-heading-files-exist] " + rel + " " + lines + " lines");
            return src;
        }

        private static int CountLines(string text)
        {
            int n = 1;
            for (int i = 0; i < text.Length; i++) if (text[i] == '\n') n++;
            return n;
        }

        /// <summary>
        /// The repo root, derived from Application.dataPath rather than hardcoded. CLAUDE.md 0:
        /// the root is machine-dependent (C:\eoa on one seat, D:\eoa on another) and a doc that
        /// names one is how a seat follows canon to a path that does not exist.
        /// </summary>
        private static string GetProjectRoot() => Directory.GetParent(Application.dataPath).FullName;
    }
}
