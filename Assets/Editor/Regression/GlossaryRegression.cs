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
//   7 [ui-source-copy]    SOURCE LINT (declared honestly as one, per WO-1494): the
//                         same BannedInPlayerCopy list, applied to STRING LITERALS in
//                         UI source under Assets/_Modules/**/UI/** and
//                         Assets/_Modules/HUD/**. Case 6 only ever saw canonical JSON,
//                         so hardcoded copy - lore arrays, button faces, toasts built
//                         in C# - was never covered at all.
//
// ROT 4 (hardcoded copy) - not every player-visible string lives in a canonical file.
//                        VillageLoadOverlay's rotating lore array is a plain C#
//                        string[], and it shipped the RETIRED tagline "Hold the last
//                        light." for months after the 2026-07-24 rebrand while cases
//                        1-6 stayed green, because no oracle had ever looked at C#.
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
                Case(failures, "ui-source-copy", () => Case7_BannedInUiSource(failures, notes));
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
                         "onto the Game Guide rail so Settings reaches them, and neither the guide, the glossary, " +
                         "nor a hardcoded string literal in UI/HUD source leaks a retired name or the internal " +
                         "'Obsidian' code name to the player" + noteStr;
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
        //  CASE 7 - the same banned words, in HARDCODED UI SOURCE COPY
        // ---------------------------------------------------------------------
        //  This is a SOURCE-TEXT LINT and says so out loud (WO-1494 is about six
        //  suites that claim to MEASURE and are really linting text). It cannot
        //  prove a string reached a screen; it proves the retired word is not in
        //  the file. That is still the only oracle that would have caught the
        //  2026-09-06 defect, because the copy never passed through JSON.
        //
        //  ⛔ THE DISCRIMINATORS ARE NOT DECORATION - READ WHY THEY EXIST.
        //  A naive "banned word appears in a string literal" scan over these two
        //  globs returns THIRTEEN hits against HEAD 815c628e9, and TWELVE of them
        //  are correct code: FlowTrace/Debug diagnostic prose, an exception
        //  message, a `case "avalon":` wire key, a realm-key HashSet, a const
        //  slug. A lint that cries twelve times to catch one is a lint nobody
        //  runs. Three filters, in order, and each one is a PRINCIPLE:
        //
        //   (a) COMMENTS ARE STRIPPED FIRST (StripComments, shared with case 5) -
        //       a doc-comment is allowed to NAME the retired word as a rule, the
        //       same allowance case 6 gives the canonical files' _comment field.
        //   (b) DIAGNOSTIC + FROZEN-SYNTAX STATEMENTS ARE SKIPPED. Precedent, not
        //       invention: RetiredVocabularyRegression.cs:63-65 already excludes
        //       exactly this shape (JsonProperty(, const string, case ", FlowTrace.,
        //       Debug., PlayerPrefs, Regex.) under its header line "Frozen
        //       persistence/wire identifiers are excluded". `throw new` is added
        //       here - an exception message is engineer copy, never player copy.
        //   (c) BARE SLUG LITERALS ARE SKIPPED. A literal whose whole content is
        //       one lowercase token (^[a-z0-9_.:-]+$) is an identifier, a wire key
        //       or a scene name - never player prose. Same principle as (b),
        //       applied syntactically instead of by call-site.
        //
        //  ⚠ KNOWN GAP, stated rather than hidden: verbatim strings (@"...") are
        //  matched by the ordinary literal regex and their escape rules differ, so
        //  a banned word inside one MAY be missed. No UI/HUD file carries such a
        //  case today (measured, see RED PROOF). Interpolated strings ($"...") ARE
        //  covered - the literal body is scanned like any other.
        //
        //  ★ RED PROOF (§11B - this is a replicated measurement, NOT a Unity run)
        //  Measured 2026-09-06 against HEAD 815c628e98c248776272d4b3160ffe481aa66395
        //  by replicating the exact regexes below in Python over the same two globs
        //  (156 files scanned). The filter ladder, in hits:
        //      13  raw literal scan (comments stripped)
        //       3  after (b) diagnostic/frozen-syntax statement skip
        //       2  after (c) bare-slug skip
        //       1  after the dated exemption below
        //  The ONE survivor is the defect this case was written for:
        //      Assets/_Modules/Core/UI/VillageLoadOverlay.cs:65
        //      "Hold the last light.",      <- retired tagline (2026-07-24 rebrand)
        //  i.e. this case is RED against HEAD's overlay and GREEN once that line is
        //  removed (0 hits on the edited tree, same replication). The lane that
        //  wrote this could not hold the Unity lock; the committer confirms on the
        //  real suite via DataRegression.RunAll (registered at DataRegression.cs:663).
        // =====================================================================

        /// <summary>UI-source roots. A path qualifies when it is under a "/UI/" folder
        /// anywhere in _Modules, or anywhere under the HUD module.</summary>
        private const string ModulesRoot = "Assets/_Modules";
        private const string HudScopePrefix = "Assets/_Modules/HUD/";

        // ── DATED EXEMPTION LEDGER (WO-1495 shape: WO + date + remove-by) ──────
        // ⛔ NOT A PARDON. The value is an EXACT COUNT, so the (count+1)th hit in an
        // exempted file still FAILS, a smaller count fails as DRIFT (the copy was
        // fixed - lower it), and a zero match fails as STALE (delete the row). Same
        // ratchet as RetiredVocabularyRegression.KnownCopyDebt2026_08_25.
        //
        // { path, count } - one row, minted 2026-09-06 with its reason PROVEN, not
        // assumed (WO-1495 §3 forbids dating an unexplained entry to buy green):
        //   ElarionUiKitDemo.cs is a DEV-ONLY surface. Its sole caller is
        //   DevPanelController.cs:863 ("Toggle Obsidian kit demo"); its own header
        //   calls it "a dev surface, not a pooled HUD"; and
        //   UiObsidianConformanceRegression.cs:110 ALREADY exempts the same file as
        //   "the kit's own screenshot-compare demo harness". Its one hit is the
        //   demo's screenshot title, which no player can reach.
        //   WO: this lane (retired-tagline sweep, 2026-09-06).
        //   REMOVE-BY: 2026-12-06 - by then either the demo title drops the code
        //   name, or the dev-surface exclusion is made structural (a marker the
        //   lint can read) instead of a per-file row.
        private static readonly Dictionary<string, int> DevSurfaceDebt2026_09_06 =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            { "Assets/_Modules/Core/UI/ElarionUiKitDemo.cs", 1 },
        };

        /// <summary>Any C# string literal, ordinary or interpolated. See the KNOWN GAP note.</summary>
        private static readonly Regex LiteralRx =
            new Regex(@"""(?:[^""\\\r\n]|\\.)*""", RegexOptions.Compiled);

        /// <summary>Statements whose strings are engineer-facing, or frozen wire syntax.
        /// Shape lifted from RetiredVocabularyRegression.cs:63-65.</summary>
        private static readonly Regex FrozenOrDiagnosticStatement = new Regex(
            @"(FlowTrace\.|Debug\.|throw\s+new|\bconst\s+string\b|\bcase\s+""|PlayerPrefs|Regex\.|nameof\s*\()",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>A literal that is one bare lowercase token: an id, not prose.</summary>
        private static readonly Regex SlugLiteralRx =
            new Regex(@"^[a-z0-9_.:\-]+$", RegexOptions.Compiled);

        /// <summary>
        /// Mask token used to lift string literals out of the source before the text is
        /// split into statements. It is ASCII SOH (U+0001) BY DESIGN: the one class of
        /// character that cannot legally appear in C# source outside a literal, so a
        /// masked slot can never collide with real code.
        /// ⚠ NOTE FOR THE NEXT READER / THE NUL GUARD: the two inline patterns that
        /// consume this token (in <see cref="ScanUiSource"/> and <see cref="Unmask"/>)
        /// carry the U+0001 byte LITERALLY. The lone control character here -> &lt;- IS that byte, quoted on
        /// purpose. It is NOT the §0 mount-garble signature, which is
        /// U+0000 (NUL) and is what CompileGate actually rejects.
        /// </summary>
        private const string Sentinel = "";

        private static void Case7_BannedInUiSource(List<string> failures, List<string> notes)
        {
            if (!Directory.Exists(ModulesRoot))
            {
                failures.Add("[ui-source-copy] " + ModulesRoot + " not found - the module root moved " +
                             "without updating this oracle, so the lint scanned NOTHING and would have " +
                             "reported green");
                return;
            }

            string[] all;
            try { all = Directory.GetFiles(ModulesRoot, "*.cs", SearchOption.AllDirectories); }
            catch (Exception ex)
            {
                failures.Add("[ui-source-copy] could not enumerate " + ModulesRoot + ": " +
                             ex.GetType().Name + ": " + ex.Message);
                return;
            }

            int scanned = 0;
            var perFile = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var reported = new List<string>();

            foreach (var raw in all)
            {
                // ⚠ Directory.GetFiles returns BACKSLASHES on Windows. Without this
                // normalisation the "/UI/" test matches nothing, the lint scans zero
                // files, and the case passes GREEN while proving nothing.
                string path = raw.Replace('\\', '/');
                bool inScope = path.IndexOf("/UI/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                               path.StartsWith(HudScopePrefix, StringComparison.OrdinalIgnoreCase);
                if (!inScope) continue;

                string src;
                try { src = File.ReadAllText(path); }
                catch (Exception ex)
                {
                    failures.Add("[ui-source-copy] could not read " + path + ": " +
                                 ex.GetType().Name + ": " + ex.Message);
                    continue;
                }
                scanned++;

                foreach (var hit in ScanUiSource(path, src))
                {
                    perFile[path] = perFile.TryGetValue(path, out int had) ? had + 1 : 1;
                    reported.Add(hit);
                }
            }

            if (scanned == 0)
            {
                failures.Add("[ui-source-copy] scanned 0 UI/HUD source files under " + ModulesRoot +
                             " - the scope predicate matched nothing, which is a broken lint, not a pass");
                return;
            }

            // Apply the ratchet: forgive up to the exact authored count, and fail on
            // a row that is now stale or has drifted DOWN (the copy was fixed).
            int forgiven = 0;
            foreach (var row in DevSurfaceDebt2026_09_06)
            {
                perFile.TryGetValue(row.Key, out int actual);
                if (actual == row.Value) { forgiven += actual; continue; }
                if (actual == 0)
                {
                    failures.Add("[ui-source-copy] STALE exemption: " + row.Key + " is allowed " + row.Value +
                                 " banned-word literal(s) and now has NONE - the copy was fixed, so delete " +
                                 "the DevSurfaceDebt2026_09_06 row instead of letting it forgive a future leak");
                    continue;
                }
                if (actual < row.Value)
                {
                    failures.Add("[ui-source-copy] DRIFTED exemption: " + row.Key + " is allowed " + row.Value +
                                 " but now has " + actual + " - lower the DevSurfaceDebt2026_09_06 count to " +
                                 actual + "; the debt shrank");
                }
                forgiven += Math.Min(actual, row.Value);
            }

            int leaks = reported.Count - forgiven;
            if (leaks > 0)
            {
                foreach (var hit in reported) failures.Add(hit);
                failures.Add("[ui-source-copy] " + leaks + " banned word(s) reach the player from HARDCODED UI " +
                             "source (not from a canonical file, which is why cases 1-6 stayed green). Fix the " +
                             "copy at source - do NOT add an exemption row unless you can PROVE, this session, " +
                             "that the surface is unreachable by a player (WO-1495 §3)");
            }

            notes.Add("ui-source lint: " + scanned + " UI/HUD .cs scanned, " + reported.Count +
                      " raw hit(s), " + forgiven + " forgiven by the dated dev-surface row (remove-by 2026-12-06)");
        }

        /// <summary>Returns one failure line per banned word found in a real player-copy
        /// string literal. Comments are stripped, diagnostic/frozen statements skipped,
        /// bare slug literals skipped - see the case header for why each filter exists.</summary>
        private static List<string> ScanUiSource(string path, string src)
        {
            var found = new List<string>();
            string stripped = StripComments(src);

            // Mask every literal to a token BEFORE splitting into statements, so a
            // semicolon or a brace INSIDE a string cannot split the literal in half
            // and hide the banned word across the seam.
            var literals = new List<string>();
            string masked = LiteralRx.Replace(stripped, m =>
            {
                literals.Add(m.Value);
                return Sentinel + (literals.Count - 1) + Sentinel;
            });

            foreach (var statement in masked.Split(';', '{', '}'))
            {
                if (string.IsNullOrEmpty(statement)) continue;

                // Unmask before the skip test so patterns like `case "` - which only
                // exist in the ORIGINAL text - are live rather than silently inert.
                string unmasked = Unmask(statement, literals);
                if (FrozenOrDiagnosticStatement.IsMatch(unmasked)) continue;

                foreach (Match tok in Regex.Matches(statement, "([0-9]+)"))
                {
                    int idx = int.Parse(tok.Groups[1].Value);
                    if (idx < 0 || idx >= literals.Count) continue;

                    string lit = literals[idx];
                    string body = lit.Length >= 2 ? lit.Substring(1, lit.Length - 2) : lit;
                    if (SlugLiteralRx.IsMatch(body)) continue;

                    foreach (var banned in BannedInPlayerCopy)
                    {
                        if (!HasWord(body, banned)) continue;
                        found.Add("[ui-source-copy] " + path + " hardcodes '" + banned + "' in the player-facing " +
                                  "literal \"" + Trim(body) + "\" - retired canon or an internal code name must " +
                                  "never be authored into UI source (the canonical tagline is " +
                                  "\"Echoes of a Forgotten Civilization\"; the timed-work system is the " +
                                  "\"Work Queue\")");
                    }
                }
            }
            return found;
        }

        private static string Unmask(string statement, List<string> literals)
        {
            return Regex.Replace(statement, "([0-9]+)", m =>
            {
                int i = int.Parse(m.Groups[1].Value);
                return i >= 0 && i < literals.Count ? literals[i] : m.Value;
            });
        }

        /// <summary>Word-boundary, case-insensitive. This is what keeps "ObsidianModal"
        /// and "BuildObsidianPanel" out of the results while "the Obsidian queue" stays in.
        /// Same matcher shape as RetiredVocabularyRegression.cs:391.</summary>
        private static bool HasWord(string text, string word)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(word)) return false;
            return Regex.IsMatch(text, "(?<![A-Za-z0-9_])" + Regex.Escape(word) + "(?![A-Za-z0-9_])",
                                 RegexOptions.IgnoreCase);
        }

        private static string Trim(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            return s.Length <= 70 ? s : s.Substring(0, 70) + "...";
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
