// =============================================================================
// GlossaryRegression [glossary]
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression (references DeNelle.Core + DeNelle.Village).
//
// Pins the owner request "make sure the full glossary and help guide is in
// settings if needed" (2026-08-02). The help GUIDE already existed and is
// covered by DataRegression.CheckGuideContent; the GLOSSARY was net-new, and it
// is content in a dual-copy canonical file surfaced through a projection in
// GuideVM. Three things can rot silently there, so all three are asserted:
//
//   ROT 1 (dual copy)  - glossary.json lives twice (Resources for WebGL,
//                        StreamingAssets for everything else). Edit one and the
//                        editor reads the fixed text while the device reads the
//                        stale one. Byte-identity is the only honest assertion.
//   ROT 2 (content)    - a term with an empty definition renders as a naked word
//                        on the parchment, and a term defined twice means two
//                        different answers to the same question.
//   ROT 3 (orphaning)  - the file can load perfectly and reach NO PLAYER, because
//                        nothing but GuideVM's projection puts it on screen. A
//                        source lint on that projection is what makes the data
//                        provably reachable.
//
// Cases:
//   1 [glossary-dual]     Both copies exist and are BYTE-IDENTICAL (asserted on
//                         raw bytes, so a BOM or a CRLF flip is caught too).
//   2 [glossary-schema]   Groups carry id/tab/title, group ids are unique, every
//                         term files under a declared group, and every group has
//                         at least one term (an empty group is an empty tab).
//   3 [glossary-terms]    Every term has a non-empty term AND a non-empty, real
//                         definition; nothing is ASCII-illegal (the build TMP
//                         font renders non-ASCII as tofu) and nothing carries NUL.
//   4 [glossary-unique]   No term is defined twice (case-insensitive).
//   5 [glossary-surface]  GlossaryCatalog really hydrates the authored rows, and
//                         GuideVM really projects them onto the guide rail - i.e.
//                         the glossary is reachable from Settings -> Game Guide.
//   6 [guide-dual]        guide-content.json's two copies are byte-identical, and
//                         no RETIRED string ("Avalon", "Garran", the old tagline)
//                         or INTERNAL code name ("Obsidian") appears in anything
//                         the player can read, in either the guide or the
//                         glossary. Scanned on the PARSED, rendered fields only,
//                         so the authoring _comment may still name the rule.
//
// Markers: GLOSSARY_OK / GLOSSARY_FAIL.
// Standalone: run-unity-method DeNelle.Editor.Regression.GlossaryRegression.RunAll
// Covenant contract Run(out reason) is DataRegression-shaped; wiring into
// DataRegression.RunAll is left to the committer (that file is lane-fenced).
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using DeNelle.Village;

namespace DeNelle.Editor.Regression
{
    public static class GlossaryRegression
    {
        private const string GlossaryRes = "Assets/Resources/Data/Canonical/glossary.json";
        private const string GlossarySA = "Assets/StreamingAssets/Data/Canonical/glossary.json";
        private const string GuideRes = "Assets/Resources/Data/Canonical/guide-content.json";
        private const string GuideSA = "Assets/StreamingAssets/Data/Canonical/guide-content.json";

        private const string GuideVmSrc = "Assets/_Modules/Village/UI/Guide/GuideVM.cs";

        /// <summary>A definition shorter than this is a placeholder, not an answer.</summary>
        private const int MinDefinitionChars = 20;

        /// <summary>Strings that must never reach the player. Retired canon + the
        /// internal code name for the Work Queue.</summary>
        private static readonly string[] BannedInPlayerCopy =
        {
            "Avalon",              // retired village name (DESIGN-DECISIONS #1)
            "Garran",              // retired Knight name - he is Grom (WO-861 A0)
            "Hold the last light", // retired tagline (2026-07-24)
            "Obsidian",            // internal code name; player-facing is "Work Queue"
        };

        /// <summary>Standalone batch entry - prints the marker.</summary>
        public static void RunAll()
        {
            if (Run(out string reason)) Debug.Log("GLOSSARY_OK - " + reason);
            else Debug.LogError("GLOSSARY_FAIL: " + reason);
        }

        /// <summary>Covenant contract (DataRegression-shaped). Never throws.</summary>
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var notes = new List<string>();
            try
            {
                Case(failures, "glossary-dual", () => Case1_DualCopy(failures, notes));
                Case(failures, "glossary-schema", () => Case2_Schema(failures, notes));
                Case(failures, "glossary-terms", () => Case3_Terms(failures, notes));
                Case(failures, "glossary-unique", () => Case4_Unique(failures));
                Case(failures, "glossary-surface", () => Case5_Surface(failures, notes));
                Case(failures, "guide-dual", () => Case6_GuideDualAndBannedCopy(failures, notes));
            }
            catch (Exception ex)
            {
                failures.Add("[suite] THREW " + ex.GetType().Name + ": " + ex.Message);
            }

            string noteStr = notes.Count > 0 ? " [notes: " + string.Join("; ", notes) + "]" : "";
            if (failures.Count == 0)
            {
                reason = "GLOSSARY OK - both glossary.json copies are byte-identical, every term carries a real " +
                         "definition under a declared group, no term is defined twice, GuideVM projects the groups " +
                         "onto the Game Guide rail so Settings reaches them, and neither the guide nor the glossary " +
                         "leaks a retired name or the internal 'Obsidian' code name to the player" + noteStr;
                return true;
            }
            reason = "glossary FAIL x" + failures.Count + ": " + string.Join(" | ", failures) + noteStr;
            return false;
        }

        private static void Case(List<string> failures, string name, Action body)
        {
            try { body(); }
            catch (Exception ex) { failures.Add("[" + name + "] THREW " + ex.GetType().Name + ": " + ex.Message); }
        }

        // =====================================================================
        //  CASE 1 - the dual copy is byte-identical
        // =====================================================================
        private static void Case1_DualCopy(List<string> failures, List<string> notes)
        {
            AssertByteIdentical(failures, notes, GlossaryRes, GlossarySA, "glossary-dual", "glossary.json");
        }

        private static void AssertByteIdentical(List<string> failures, List<string> notes,
            string resPath, string saPath, string caseName, string label)
        {
            if (!File.Exists(resPath))
            {
                failures.Add("[" + caseName + "] " + resPath + " is missing - Resources is the copy the SHIPPED " +
                             "player loads first, so " + label + " would be absent on device");
                return;
            }
            if (!File.Exists(saPath))
            {
                failures.Add("[" + caseName + "] " + saPath + " is missing - the StreamingAssets copy is the " +
                             "fallback every non-WebGL build reads; " + label + " must exist twice");
                return;
            }

            byte[] a = File.ReadAllBytes(resPath);
            byte[] b = File.ReadAllBytes(saPath);

            if (a.Length != b.Length)
            {
                failures.Add("[" + caseName + "] " + label + " copies differ in SIZE (Resources " + a.Length +
                             " bytes vs StreamingAssets " + b.Length + ") - one side was edited alone, so the " +
                             "editor and the device are reading different content");
                return;
            }

            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] == b[i]) continue;
                failures.Add("[" + caseName + "] " + label + " copies diverge at byte " + i + " (Resources 0x" +
                             a[i].ToString("X2") + " vs StreamingAssets 0x" + b[i].ToString("X2") + ") - same size, " +
                             "different bytes, which is how a BOM or a CRLF flip hides a real content drift");
                return;
            }

            notes.Add(label + " dual-copy identical (" + a.Length + " bytes)");
        }

        // =====================================================================
        //  CASE 2 - groups resolve, ids are unique, no empty tab
        // =====================================================================
        private static void Case2_Schema(List<string> failures, List<string> notes)
        {
            GlossaryCatalog.Reload();
            var groups = new List<GlossaryGroup>(GlossaryCatalog.Groups);
            var terms = new List<GlossaryTerm>(GlossaryCatalog.Terms);

            if (groups.Count == 0)
            {
                failures.Add("[glossary-schema] glossary.json hydrated ZERO groups - the guide rail would gain no " +
                             "glossary tabs at all, which is the exact 'the glossary does not exist' state this " +
                             "work was raised to fix");
                return;
            }
            if (terms.Count == 0)
            {
                failures.Add("[glossary-schema] glossary.json hydrated ZERO terms (mapping break or empty 'terms')");
                return;
            }

            var groupIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var g in groups)
            {
                if (g == null) { failures.Add("[glossary-schema] a null group row"); continue; }
                if (string.IsNullOrEmpty(g.Id))
                    failures.Add("[glossary-schema] group with tab '" + (g.Tab ?? "<null>") + "' has no id - terms " +
                                 "reference groups BY ID, so nothing could ever file under it");
                else if (!groupIds.Add(g.Id.Trim()))
                    failures.Add("[glossary-schema] duplicate group id '" + g.Id + "' - two rail tabs would claim " +
                                 "the same terms and the second would silently shadow the first");

                if (string.IsNullOrEmpty(g.Tab))
                    failures.Add("[glossary-schema] group '" + (g.Id ?? "<null>") + "' has no tab label (blank rail button)");
                if (string.IsNullOrEmpty(g.Title))
                    failures.Add("[glossary-schema] group '" + (g.Id ?? "<null>") + "' has no title (blank body header)");

                if (!string.IsNullOrEmpty(g.Id) && GlossaryCatalog.TermsIn(g.Id).Count == 0)
                    failures.Add("[glossary-schema] group '" + g.Id + "' has NO terms - it would render as an empty " +
                                 "glossary tab, which reads to the player as a broken screen");
            }

            foreach (var t in terms)
            {
                if (t == null) continue;
                if (string.IsNullOrEmpty(t.Group))
                    failures.Add("[glossary-schema] term '" + (t.Term ?? "<null>") + "' declares no group - it is " +
                                 "authored but unreachable, filed under no tab");
                else if (!groupIds.Contains(t.Group.Trim()))
                    failures.Add("[glossary-schema] term '" + t.Term + "' files under group '" + t.Group +
                                 "', which is not declared in 'groups' - the term is dropped on the floor");
            }

            notes.Add(groups.Count + " groups / " + terms.Count + " terms");
        }

        // =====================================================================
        //  CASE 3 - every term has a real, renderable definition
        // =====================================================================
        private static void Case3_Terms(List<string> failures, List<string> notes)
        {
            GlossaryCatalog.Reload();
            int shortest = int.MaxValue;
            string shortestTerm = "<none>";

            foreach (var t in GlossaryCatalog.Terms)
            {
                if (t == null) { failures.Add("[glossary-terms] a null term row"); continue; }

                if (string.IsNullOrEmpty(t.Term))
                {
                    failures.Add("[glossary-terms] a term row has no 'term' - it would render as a bare definition " +
                                 "with nothing to look up");
                    continue;
                }

                if (string.IsNullOrEmpty(t.Definition) || t.Definition.Trim().Length == 0)
                {
                    failures.Add("[glossary-terms] '" + t.Term + "' has an EMPTY definition - the player opens the " +
                                 "glossary, finds the word, and learns nothing");
                    continue;
                }

                int len = t.Definition.Trim().Length;
                if (len < shortest) { shortest = len; shortestTerm = t.Term; }
                if (len < MinDefinitionChars)
                    failures.Add("[glossary-terms] '" + t.Term + "' has a " + len + "-character definition (\"" +
                                 t.Definition.Trim() + "\") - under " + MinDefinitionChars + " characters is a " +
                                 "placeholder, not an explanation");

                AssertRenderable(failures, "term '" + t.Term + "'", t.Term);
                AssertRenderable(failures, "definition of '" + t.Term + "'", t.Definition);
            }

            foreach (var g in GlossaryCatalog.Groups)
            {
                if (g == null) continue;
                AssertRenderable(failures, "tab of group '" + g.Id + "'", g.Tab);
                AssertRenderable(failures, "title of group '" + g.Id + "'", g.Title);
                if (!string.IsNullOrEmpty(g.Intro)) AssertRenderable(failures, "intro of group '" + g.Id + "'", g.Intro);
            }

            if (shortest != int.MaxValue)
                notes.Add("shortest definition = " + shortest + " chars ('" + shortestTerm + "')");
        }

        /// <summary>ASCII + NUL guard. The shipped TMP font tofus non-ASCII glyphs, so an
        /// em dash or a curly quote is a visible box on the parchment, not a style choice.</summary>
        private static void AssertRenderable(List<string> failures, string what, string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '\0')
                {
                    failures.Add("[glossary-terms] the " + what + " contains a NUL byte at index " + i +
                                 " - that is the mount-garble signature and it poisons the file");
                    return;
                }
                if (c > 127)
                {
                    failures.Add("[glossary-terms] the " + what + " contains non-ASCII '" + c + "' (U+" +
                                 ((int)c).ToString("X4") + ") at index " + i + " - the build TMP font renders it as " +
                                 "a tofu box; use ASCII (\"--\" for a dash, straight quotes)");
                    return;
                }
            }
        }

        // =====================================================================
        //  CASE 4 - no term is defined twice
        // =====================================================================
        private static void Case4_Unique(List<string> failures)
        {
            GlossaryCatalog.Reload();
            var seen = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var t in GlossaryCatalog.Terms)
            {
                if (t == null || string.IsNullOrEmpty(t.Term)) continue;
                string key = t.Term.Trim();
                string firstGroup;
                if (seen.TryGetValue(key, out firstGroup))
                    failures.Add("[glossary-unique] '" + t.Term + "' is defined twice (groups '" + firstGroup +
                                 "' and '" + (t.Group ?? "<null>") + "') - two answers to one question, and the " +
                                 "player has no way to know which is current");
                else
                    seen[key] = t.Group ?? "<null>";
            }
        }

        // =====================================================================
        //  CASE 5 - the data actually reaches a screen
        // =====================================================================
        private static void Case5_Surface(List<string> failures, List<string> notes)
        {
            // 5a. The catalog hydrates the authored rows (not just an empty fallback).
            GlossaryCatalog.Reload();
            int hydrated = GlossaryCatalog.TermCount;
            if (hydrated == 0)
            {
                failures.Add("[glossary-surface] GlossaryCatalog hydrated 0 terms - the loader's failure path " +
                             "returns an empty list, so the guide would show no glossary tabs and the only trace " +
                             "would be a FlowTrace.Fail line nobody reads");
                return;
            }

            // 5b. GuideVM must still PROJECT them. Everything above can be green while the
            //     glossary reaches no player, because nothing else puts it on screen.
            string src = ReadSource(GuideVmSrc, failures);
            if (src == null) return;
            string code = StripComments(src);

            if (code.IndexOf("GlossaryCatalog", StringComparison.Ordinal) < 0)
                failures.Add("[glossary-surface] GuideVM.cs no longer references GlossaryCatalog - glossary.json is " +
                             "authored, loadable, and ORPHANED: nothing appends it to the guide rail, so Settings -> " +
                             "Game Guide shows the old tabs only");

            if (!Regex.IsMatch(code, @"BuildGlossarySections\s*\(\s*\)"))
                failures.Add("[glossary-surface] GuideVM.cs no longer calls BuildGlossarySections() - the projection " +
                             "that turns glossary groups into rail tabs is gone; re-point this lint deliberately if " +
                             "the projection was renamed");

            if (!Regex.IsMatch(code, @"_sections\s*\.\s*Add") || !Regex.IsMatch(code, @"_tabs\s*\.\s*Add"))
                failures.Add("[glossary-surface] GuideVM.cs no longer adds to both _sections and _tabs - the rail " +
                             "label list and the content list must grow together or the tabs and bodies desync");

            notes.Add("surfaced via GuideVM projection (" + hydrated + " terms across " +
                      GlossaryCatalog.Groups.Count + " rail tabs)");
        }

        // =====================================================================
        //  CASE 6 - the guide's own dual copy + no retired/internal words on screen
        // =====================================================================
        private static void Case6_GuideDualAndBannedCopy(List<string> failures, List<string> notes)
        {
            AssertByteIdentical(failures, notes, GuideRes, GuideSA, "guide-dual", "guide-content.json");

            // Scan the PARSED, rendered fields only - the authoring _comment in each file
            // is allowed to name the banned words as a rule ("never 'Obsidian'").
            GuideContentCatalog.Reload();
            foreach (var s in GuideContentCatalog.Sections)
            {
                if (s == null) continue;
                ScanBanned(failures, "guide section '" + s.Id + "' tab", s.Tab);
                ScanBanned(failures, "guide section '" + s.Id + "' title", s.Title);
                if (s.Body != null)
                    for (int i = 0; i < s.Body.Count; i++)
                        ScanBanned(failures, "guide section '" + s.Id + "' body[" + i + "]", s.Body[i]);
                if (s.Tips != null)
                    for (int i = 0; i < s.Tips.Count; i++)
                        ScanBanned(failures, "guide section '" + s.Id + "' tip[" + i + "]", s.Tips[i]);
            }

            GlossaryCatalog.Reload();
            foreach (var g in GlossaryCatalog.Groups)
            {
                if (g == null) continue;
                ScanBanned(failures, "glossary group '" + g.Id + "' tab", g.Tab);
                ScanBanned(failures, "glossary group '" + g.Id + "' title", g.Title);
                ScanBanned(failures, "glossary group '" + g.Id + "' intro", g.Intro);
            }
            foreach (var t in GlossaryCatalog.Terms)
            {
                if (t == null) continue;
                ScanBanned(failures, "glossary term '" + t.Term + "'", t.Term);
                ScanBanned(failures, "definition of '" + t.Term + "'", t.Definition);
            }
        }

        private static void ScanBanned(List<string> failures, string where, string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            foreach (var banned in BannedInPlayerCopy)
            {
                if (text.IndexOf(banned, StringComparison.OrdinalIgnoreCase) < 0) continue;
                failures.Add("[guide-dual] the " + where + " contains '" + banned + "', which must never reach the " +
                             "player (retired canon or an internal code name - the timed-work system is the " +
                             "\"Work Queue\" with Builders / Training / Research channels)");
            }
        }

        // =====================================================================
        //  HELPERS
        // =====================================================================

        private static string ReadSource(string path, List<string> failures)
        {
            if (!File.Exists(path))
            {
                failures.Add("[glossary-surface] " + path + " not found - the file moved without updating this oracle");
                return null;
            }
            try { return File.ReadAllText(path); }
            catch (Exception ex)
            {
                failures.Add("[glossary-surface] could not read " + path + ": " + ex.GetType().Name + ": " + ex.Message);
                return null;
            }
        }

        /// <summary>Strips // and /* */ comments so a lint can never be satisfied by prose.</summary>
        private static string StripComments(string src)
        {
            if (string.IsNullOrEmpty(src)) return string.Empty;
            string noBlock = Regex.Replace(src, @"/\*.*?\*/", " ", RegexOptions.Singleline);
            return Regex.Replace(noBlock, @"//[^\r\n]*", " ");
        }
    }
}
