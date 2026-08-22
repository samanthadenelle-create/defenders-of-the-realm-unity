// =============================================================================
// HollowPassFixtures  --  KNOWN INPUTS that pin the hollow-pass detector
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression.  Namespace: DeNelle.Editor.Regression.
// No markers, no Run(out string): a fixture bank, not a suite. Driven by
// RegressionMarkerRegression RULE 4, which calls SelfTest() BEFORE it sweeps the
// tree - because if the detector is broken, the sweep's silence means nothing.
//
// ── WHY A DETECTOR NEEDS ITS OWN REGRESSION ──────────────────────────────────
// WO-1138's finding was not "there was a bug in the ratchet". It was that the
// ratchet's COVERAGE was a function of CODE FORMATTING (a ~4-line window around the
// `return`), and nothing anywhere measured that coverage. So it reported clean while
// five real hollow passes sat in the file it had just scanned. A detector with no
// known-input test is exactly the thing it hunts: it answers OK having proven
// nothing.
//
// This file therefore holds the ACTUAL 2026-08-21 EVIDENCE as source text:
//
//   * CosmeticSixSites - the six hollow passes that were in CosmeticApplyRegression
//     on the morning of 2026-08-21, reconstructed in their pre-fix form. The
//     scanner must flag ALL SIX.
//   * RaidCooldownCase5 - the vacuous-against-an-absent-fixture shape that passed
//     while asserting nothing, found only because a NEIGHBOURING case failed loudly
//     for an unrelated reason and a human read the fixture.
//   * CleanSuite - five shapes that are CORRECT and must NEVER be flagged: a
//     declared stand-down, a producer that already reported, a sibling branch that
//     already went red, a per-item guard on an incoming value, and a per-site
//     opt-out. A detector is only as useful as its false-positive rate; 400
//     unactionable findings is the same crime pointed the other way.
//   * Unanalysable - deliberately unbalanced source. The scanner must REFUSE it
//     (analysisError) rather than report it clean.
//
// ── THE CONTROL: THE RETIRED 4-LINE WINDOW, KEPT AND RUN ─────────────────────
// NarrowWindowFind is the OLD detector, ported verbatim from
// RegressionMarkerRegression.FindHollowPassLines as it stood on 2026-08-21. It is
// kept and EXECUTED, not described, so the claim at the heart of this work order is
// a measurement rather than a story:
//
//     on CosmeticSixSites   the retired window finds ONE      (the [folders] site)
//                           the control-flow scanner finds SIX
//     on RaidCooldownCase5  the retired window finds ZERO
//                           the control-flow scanner finds ONE
//
// If a future edit ever makes the new scanner agree with the old window, SelfTest
// goes red and says so. That is the ratchet on the ratchet.
// =============================================================================

using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace DeNelle.Editor.Regression
{
    public static class HollowPassFixtures
    {
        // Declared as a balanced PAIR on one line on purpose: the Unanalysable fixture is
        // deliberately brace-UNBALANCED, so it is built from these constants rather than
        // written as a literal - a literal would unbalance THIS file's own brace count and
        // trip the CLAUDE.md rule-1 gate.
        private const char OpenBrace = '{', CloseBrace = '}';

        // =====================================================================
        //  FIXTURE 1 - the six CosmeticApplyRegression sites, pre-fix (2026-08-21)
        // =====================================================================
        // Sites 1-5 place the guarding `if` FIVE OR MORE LINES above the `return`, which
        // is the ONLY reason the retired window never saw them. Site 6 - [folders] - keeps
        // its guard three lines from the return, which is the only reason it WAS seen.
        // Nothing else about the six differs in kind. That is the whole finding.
        public const string CosmeticSixSites = @"
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace Fixture
{
    public static class CosmeticApplyRegressionAsItWas
    {
        private const string PetDeployerPath = ""Assets/_Modules/Pets/PetDeployer.cs"";

        // SITE 1 - [reaches], harness-capability-absent.
        private static void CheckReachesRenderer(List<string> failures, List<string> notes)
        {
            Shader shader = Shader.Find(""Universal Render Pipeline/Lit"")
                            ?? Shader.Find(""Standard"")
                            ?? Shader.Find(""Sprites/Default"");
            if (shader == null)
            {
                notes.Add(""[reaches] SKIPPED - no URP/Lit, Standard or Sprites/Default shader "" +
                          ""resolved in this editor session, so no renderer could be built to "" +
                          ""prove the apply path against. Nothing below this line ran, and the "" +
                          ""suite still reports GREEN, because the caller's only channel is "" +
                          ""the bool."");
                return;
            }

            var host = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var renderer = host.GetComponent<MeshRenderer>();
            var mat = new Material(shader);
            renderer.sharedMaterial = mat;

            // SITE 2 - [reaches], the colour property. Same shape, nested one deeper.
            string prop = mat.HasProperty(""_BaseColor"") ? ""_BaseColor""
                        : mat.HasProperty(""_Color"") ? ""_Color""
                        : null;
            if (prop == null)
            {
                notes.Add(""[reaches] SKIPPED - the resolved shader '"" + shader.name + ""' exposes "" +
                          ""neither _BaseColor nor _Color, so a tint cannot be read back off a "" +
                          ""renderer at all. The live half of rule 1 did not run; the note rides "" +
                          ""along in a GREEN reason string where nobody subtracts it."");
                return;
            }

            notes.Add(""[reaches] tint landed on a real renderer"");
        }

        // SITE 3 - [meshPath], fixture-absent (the field is gone) -> must FAIL, not skip.
        private static void CheckMeshPathParsed(List<string> failures, List<string> notes)
        {
            var field = typeof(CosmeticDef).GetField(""MeshPath"", BindingFlags.Public | BindingFlags.Instance);
            if (field == null)
            {
                notes.Add(""[meshPath] SKIPPED - CosmeticDef has no MeshPath field in this build, "" +
                          ""so the round-trip could not be measured. cosmetics.json AUTHORS the "" +
                          ""key on the pet-aether-twilight row, so if the field is gone the value "" +
                          ""is being parsed and thrown away - which is the exact defect this rule "" +
                          ""was written to catch."");
                return;
            }

            // SITE 4 - [meshPath], the catalog row. Fixture-absent again.
            CosmeticDef def = CosmeticCatalog.Find(""pet-aether-twilight"");
            if (def == null)
            {
                notes.Add(""[meshPath] SKIPPED - 'pet-aether-twilight' is not in the loaded "" +
                          ""catalog. The row ships in BOTH authored copies of cosmetics.json, so "" +
                          ""its absence is drift in the load path, not an optional dependency; "" +
                          ""standing down here hides exactly the regression the rule exists for."");
                return;
            }

            // SITE 5 - [meshPath], the parsed value.
            if (string.IsNullOrEmpty(def.MeshPath))
            {
                notes.Add(""[meshPath] SKIPPED - the row loaded but its MeshPath came back empty, "" +
                          ""so the authored value is not surviving the parse. Check the "" +
                          ""JsonProperty name against the key in cosmetics.json, in both the "" +
                          ""StreamingAssets and the Resources copy."");
                return;
            }

            notes.Add(""[meshPath] round-trips as '"" + def.MeshPath + ""'"");
        }

        // SITE 6 - [folders]. THE ONE THE 4-LINE WINDOW CAUGHT: its guard and its return
        // are three lines apart, which is the entire difference between it and the five.
        private static void CheckFolderAgreement(List<string> failures, List<string> notes)
        {
            string folder = CosmeticApplier.ResourceFolderFor(""pet"");
            string pet = CodeText(PetDeployerPath);
            if (pet == null)
            {
                notes.Add(""[folders] SKIPPED - PetDeployer.cs not found"");
                return;
            }

            if (pet.IndexOf(folder, StringComparison.Ordinal) < 0)
                failures.Add(""[folders] PetDeployer does not load from the folder ResourceFolderFor names"");
        }

        private static string CodeText(string p) { return File.Exists(p) ? File.ReadAllText(p) : null; }
    }
}
";

        // =====================================================================
        //  FIXTURE 2 - RaidCooldownRegression case 5, pre-fix (2026-08-21)
        // =====================================================================
        // Case 4's teardown destroyed the installed GameState while it was still installed,
        // so Unity's fake-null made the save read back as null. Every assertion below hung
        // off `rec != null`, nothing asserted the fixture was alive, and the method checked
        // ZERO things while reporting green. There is no bad line in it - the state under
        // it had been demolished. That is why the arm is METHOD-scoped.
        public const string RaidCooldownCase5 = @"
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Fixture
{
    public static class RaidCooldownRegressionAsItWas
    {
        private static void StateMachineCases(List<string> f)
        {
            var gss = GameStateService.Instance;
            var rec = FindRecord(gss.State, ScratchId);
            if (rec != null)
            {
                if (!RaidCooldownService.IsOnCooldown(ScratchId))
                    f.Add(""case5 ClearCooldown left the camp on cooldown"");
                if (rec.DurationSeconds <= 0d)
                    f.Add(""case5 the window length did not survive the restart"");
            }
        }
    }
}
";

        // =====================================================================
        //  FIXTURE 3 - five CORRECT shapes that must NEVER be flagged
        // =====================================================================
        public const string CleanSuite = @"
using System;
using System.Collections.Generic;
using System.IO;

namespace Fixture
{
    public static class CleanSuite
    {
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var notes = new List<string>();
            CheckDeclaredStandDown(failures, notes);
            CheckProducerReported(failures, notes);
            CheckSiblingReported(failures, notes);
            CheckPerItemGuard(failures, notes, new List<string>());
            CheckOptOut(failures, notes);
            if (failures.Count == 0)
            {
                reason = ""CLEAN OK - "" + string.Join(""; "", notes.ToArray());
                return true;
            }
            reason = failures.Count + "" failure(s)"";
            return false;
        }

        // CLEAN 1 - a DECLARED stand-down: the honest answer, and the whole point of the
        // three-way rule. It lands in the THIRD column, never the green one.
        private static void CheckDeclaredStandDown(List<string> failures, List<string> notes)
        {
            Shader shader = Shader.Find(""Universal Render Pipeline/Lit"");
            if (shader == null)
            {
                notes.Add(RegressionOutcome.PartialSkip(""[reaches] live renderer proof"",
                          ""no shader resolved in this editor session, so no renderer could be "" +
                          ""built to prove against""));
                return;
            }
            notes.Add(""[reaches] proven live"");
        }

        // CLEAN 2 - the PRODUCER already recorded the miss, one statement earlier.
        private static void CheckProducerReported(List<string> failures, List<string> notes)
        {
            string src = SourceLint.ReadCode(""_Modules/Pets/PetDeployer.cs"", failures);
            if (src == null)
            {
                return;
            }
            notes.Add(""[producer] read "" + src.Length + "" chars"");
        }

        // CLEAN 3 - a SIBLING branch on the same identifier already went red.
        private static void CheckSiblingReported(List<string> failures, List<string> notes)
        {
            var sockets = Discover();
            if (sockets.Count != 4)
                failures.Add(""[sockets] expected 4, found "" + sockets.Count);
            if (sockets.Count == 0)
            {
                return;
            }
            notes.Add(""[sockets] "" + sockets.Count);
        }

        // CLEAN 4 - a guard on an INCOMING value skips ONE ITEM, not a section.
        private static void CheckPerItemGuard(List<string> failures, List<string> notes, List<string> rows)
        {
            foreach (var row in rows)
            {
                if (string.IsNullOrEmpty(row)) continue;
                notes.Add(row);
            }
            AssertRenderable(failures, ""title"", null);
        }

        private static void AssertRenderable(List<string> failures, string what, string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }
            if (text.Length > 400) failures.Add(what + "" is too long"");
        }

        // CLEAN 5 - a PER-SITE opt-out, naming this site and its reason.
        private static void CheckOptOut(List<string> failures, List<string> notes)
        {
            string optional = FindOptionalCurationFile();
            if (optional == null)
            {
                // hollow-pass-ok: the curation picks file is opt-in EDITOR tooling that no
                // shipped build reads; its absence is the normal state, not a missing fixture.
                notes.Add(""[curation] no picks file staged"");
                return;
            }
            notes.Add(""[curation] "" + optional);
        }
    }
}
";

        // =====================================================================
        //  FIXTURE 4 - source the scanner MUST REFUSE
        // =====================================================================
        /// <summary>
        /// Deliberately brace-unbalanced. Built from the brace CONSTANTS rather than
        /// written as a literal, because a literal would unbalance this file's own count.
        /// The scanner must return an analysisError for it - a detector that reports
        /// "no findings" on source it could not parse is itself a hollow pass.
        /// </summary>
        public static string Unanalysable()
        {
            return "public static class Broken\n" + OpenBrace + "\n" +
                   "    private static void Check(List<string> failures)\n" + OpenBrace + "\n" +
                   "        if (thing == null)\n" + OpenBrace + "\n" +
                   "            return;\n";
        }

        // =====================================================================
        //  THE CONTROL - the RETIRED 4-line-window detector, ported verbatim
        // =====================================================================
        // This is RegressionMarkerRegression.FindHollowPassLines as it stood on
        // 2026-08-21, kept EXECUTABLE so "the window missed five of six" stays a
        // measurement instead of a story. Do not "improve" it - its value is that it is
        // frozen at the moment of the finding.
        public static List<int> NarrowWindowFind(string code)
        {
            var hits = new List<int>();
            if (string.IsNullOrEmpty(code)) return hits;
            string[] lines = code.Replace("\r\n", "\n").Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                bool returnsTrue = line.IndexOf("return true", StringComparison.Ordinal) >= 0;
                bool bareReturn = Regex.IsMatch(line, @"^\s*return\s*;\s*$");
                if (!returnsTrue && !bareReturn) continue;

                int from = Math.Max(0, i - 3);
                var window = new StringBuilder();
                for (int j = from; j <= i; j++) window.Append(lines[j]).Append('\n');
                string w = window.ToString();

                if (w.IndexOf("RegressionOutcome.Skip", StringComparison.Ordinal) >= 0 ||
                    w.IndexOf("RegressionOutcome.PartialSkip", StringComparison.Ordinal) >= 0 ||
                    w.IndexOf(RegressionOutcome.SkipToken, StringComparison.Ordinal) >= 0 ||
                    w.IndexOf(RegressionOutcome.PartialSkipToken, StringComparison.Ordinal) >= 0) continue;
                if (w.Contains("return false") || w.Contains("failures.Add")) continue;

                bool guardedByToken = w.Contains("== null")
                                   || w.Contains("IsNullOrEmpty")
                                   || w.Contains("!File.Exists")
                                   || w.Contains("!Directory.Exists");
                if (returnsTrue && guardedByToken && w.Contains("reason")) { hits.Add(i + 1); continue; }

                bool hasGuardIf = Regex.IsMatch(w, @"\bif\s*\(") &&
                                  (w.Contains("!") || w.Contains("== null") || w.Contains("== 0"));
                bool saysSkip = w.IndexOf("skip", StringComparison.OrdinalIgnoreCase) >= 0
                             || w.IndexOf("stand down", StringComparison.OrdinalIgnoreCase) >= 0
                             || w.IndexOf("stand-down", StringComparison.OrdinalIgnoreCase) >= 0;
                if (hasGuardIf && saysSkip) hits.Add(i + 1);
            }
            return hits;
        }

        // =====================================================================
        //  THE SELF-TEST  --  RULE 4 runs this BEFORE it sweeps the tree
        // =====================================================================
        /// <summary>
        /// Drives every fixture through HollowPassScanner and asserts the exact expected
        /// verdict. Returns false with a named detail on ANY deviation. RULE 4 fails the
        /// whole marker suite when this does: a sweep run by an unproven detector is a
        /// hollow pass with the whole tree inside it.
        /// </summary>
        public static bool SelfTest(out string detail)
        {
            var problems = new List<string>();

            // --- the six known sites, all of them ---------------------------
            string err;
            var six = HollowPassScanner.Scan(CosmeticSixSites, "CosmeticSixSites", out err);
            if (!string.IsNullOrEmpty(err))
                problems.Add("the six-site fixture would not analyse (" + err + ")");
            else if (six.Count != 6)
                problems.Add("the six KNOWN CosmeticApplyRegression hollow passes of 2026-08-21 must ALL be " +
                             "caught; the scanner found " + six.Count + " (" + Describe(six) + "). Five of " +
                             "the six escaped the retired 4-line window; catching all six is THE acceptance " +
                             "test for WO-1138.");

            int narrowSix = NarrowWindowFind(CosmeticSixSites).Count;
            if (narrowSix != 1)
                problems.Add("the RETIRED 4-line window is the CONTROL and must still find exactly 1 of the " +
                             "six (the [folders] site) - it found " + narrowSix + ". If this moved, the " +
                             "fixture was reformatted and it is no longer the 2026-08-21 evidence.");
            if (string.IsNullOrEmpty(err) && six.Count <= narrowSix)
                problems.Add("the control-flow scanner must find STRICTLY MORE than the retired window on " +
                             "the six-site fixture (found " + six.Count + " vs " + narrowSix + "). Equal " +
                             "counts mean the widening has been undone.");

            // --- the vacuous-against-an-absent-fixture shape ----------------
            var raid = HollowPassScanner.Scan(RaidCooldownCase5, "RaidCooldownCase5", out err);
            if (!string.IsNullOrEmpty(err))
                problems.Add("the raid-cooldown fixture would not analyse (" + err + ")");
            else if (raid.Count != 1 || raid[0].Arm != HollowPassScanner.ArmVacuous)
                problems.Add("RaidCooldownRegression case 5's vacuous-against-an-absent-fixture shape must be " +
                             "caught by arm " + HollowPassScanner.ArmVacuous + "; got " + Describe(raid) +
                             ". No token scan catches this one at ANY window length - it needs the " +
                             "method-scoped arm.");
            if (NarrowWindowFind(RaidCooldownCase5).Count != 0)
                problems.Add("the retired window is not supposed to see the vacuous shape at all - if it " +
                             "does, the fixture has drifted away from the 2026-08-21 evidence.");

            // --- and no false positives on the five correct shapes ----------
            var clean = HollowPassScanner.Scan(CleanSuite, "CleanSuite", out err);
            if (!string.IsNullOrEmpty(err))
                problems.Add("the clean fixture would not analyse (" + err + ")");
            else if (clean.Count != 0)
                problems.Add("FALSE POSITIVES on shapes that are CORRECT: " + Describe(clean) + ". A detector " +
                             "that fires on a declared stand-down, a producer that already reported, a " +
                             "sibling that already went red, a per-item guard or a named per-site opt-out " +
                             "produces a number nobody can act on - which is the same crime as a hollow " +
                             "pass, pointed the other way.");

            // --- and it REFUSES what it cannot parse ------------------------
            var broken = HollowPassScanner.Scan(Unanalysable(), "Unanalysable", out err);
            if (string.IsNullOrEmpty(err))
                problems.Add("the scanner reported CLEAN on deliberately unparseable source. A detector that " +
                             "answers 'no findings' when it could not read its input IS the thing it hunts; " +
                             "it must set analysisError and let the caller go red.");
            if (broken.Count != 0)
                problems.Add("the scanner returned findings alongside an analysis error - the result list " +
                             "must be empty when the analysis is untrustworthy.");

            if (problems.Count > 0)
            {
                detail = "HOLLOW-PASS DETECTOR SELF-TEST FAILED (" + problems.Count + "): " +
                         string.Join(" | ", problems.ToArray());
                return false;
            }

            detail = "detector self-test OK -- all 6 known CosmeticApplyRegression sites of 2026-08-21 are " +
                     "caught (the retired 4-line window finds 1 of them, which is the WO-1138 finding, " +
                     "measured rather than asserted); RaidCooldownRegression case 5's vacuous shape is " +
                     "caught by " + HollowPassScanner.ArmVacuous + "; 0 false positives across 5 correct " +
                     "shapes; unparseable source is REFUSED rather than reported clean";
            return true;
        }

        private static string Describe(List<HollowPassFinding> findings)
        {
            if (findings == null || findings.Count == 0) return "none";
            var parts = new List<string>();
            foreach (var f in findings) parts.Add(f.Line + ":" + f.Arm + ":" + f.Guard);
            return string.Join(", ", parts.ToArray());
        }
    }
}
