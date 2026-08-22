// =============================================================================
// HollowPassScanner  --  the hollow-pass detector, keyed on CONTROL FLOW
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression.  Namespace: DeNelle.Editor.Regression.
// No markers and no Run(out string) entry point: this is a HELPER, not a suite
// (so RULE 1's emitter scan and RULE 2's registration rule do not apply to it).
// It is driven by RegressionMarkerRegression RULE 4, and pinned by the known-input
// fixtures in HollowPassFixtures.cs.
//
// ── WHAT A HOLLOW PASS IS ────────────────────────────────────────────────────
// A regression case that returns GREEN while asserting NOTHING:
//
//     if (dependencyMissing)
//     {
//         notes.Add("SKIPPED - no PetDeployer.cs");
//         return;
//     }
//
// The caller's only channel is the bool, so A SKIP IS A PASS. A gate that reports
// success without proving it does not merely fail to catch a bug: it ACTIVELY
// ASSERTS THE BUG IS ABSENT, and work proceeds on the strength of that. That is
// strictly worse than having no gate at all (memory:
// `gates-report-success-without-proving-it`; CLAUDE.md section 8's marker law).
//
// ── WHY THIS FILE EXISTS (WO-1138) ───────────────────────────────────────────
// The previous detector lived inside RegressionMarkerRegression and inspected a
// ~4-LINE WINDOW ending at the `return`. On 2026-08-21 it caught ONE hollow pass in
// CosmeticApplyRegression.cs. A human then read the same file and found FIVE MORE.
// All six were real. The five escaped for exactly one reason: THEIR GUARDING `if`
// SAT MORE THAN FOUR LINES ABOVE THE `return`.
//
// That is not a tuning miss. It made the detector's coverage a function of CODE
// FORMATTING - the least reliable signal available. Reflow a guard body to add an
// explanatory comment and a real hollow pass becomes invisible; nothing warns.
//
// ── HOW THIS ONE CANNOT ROT THE SAME WAY ─────────────────────────────────────
// There is NO LINE WINDOW anywhere in this file. Detection walks the actual
// CONTROL-FLOW relationship:
//
//   1. A brace-depth walk BACKWARD from each `return` finds the INNERMOST ENCLOSING
//      BLOCK, whatever its length - 4 lines or 400.
//   2. The block's HEADER (the text between the previous STATEMENT BOUNDARY and the
//      OPENING BRACE) says whether that block is an `if` guard, and carries its condition.
//   3. The GUARD PATH - the statements between the OPENING BRACE and the `return` AT THE
//      BLOCK'S OWN NESTING DEPTH - is what definitely executes before returning, so
//      it is what carries the exonerating evidence.
//
// Every one of those is derived from BRACES and STATEMENT BOUNDARIES, which the
// compiler already agrees with. Reindenting, wrapping a string, or inserting twenty
// lines of comment moves nothing that this scanner reads.
//
// ── THE FOUR ARMS ────────────────────────────────────────────────────────────
//   A  missing-dependency   the guard tests null / empty / an absent file or dir,
//                           and the block returns having asserted nothing.
//   B  says-skip            the block TELLS THE READER it is standing down, in
//                           words, but tells the CALLER nothing. Kept from the old
//                           detector because it is the one arm a NOVEL guard form
//                           cannot evade.
//   C  negated-guard        `if (!InstallState(...))`, `if (!DevClock.Available)` -
//                           the 2026-08-16 evasion. Identical damage to arm A with
//                           no null token anywhere in the condition.
//   D  vacuous-against-an-absent-fixture   EVERY assertion in the method is nested
//                           inside a positive-existence guard and nothing asserts
//                           the fixture exists. RaidCooldownRegression case 5.
//
// ── AND THE EXONERATIONS, WHICH ARE THE HARDER HALF ──────────────────────────
// A detector that cries wolf 400 times is as useless as one that never fires - a
// number nobody can act on is the same crime as a hollow pass pointed the other way
// (RULE 5 records that lesson). Each exoneration below is a CONTROL-FLOW fact, not
// a taste:
//
//   * VERDICT BLOCK        `if (failures.Count == 0) { reason = "OK"; return true; }`
//                          is the CORRECT terminal. Recognised generally: the guard
//                          tests `.Count == 0` on a collection THIS METHOD ADDS TO.
//   * ASSERTED ON THE PATH `failures.Add(...)` at the block's own depth - including
//                          through an `Action<string> Fail` delegate parameter.
//   * DECLARED STAND-DOWN  RegressionOutcome.Skip / PartialSkip, directly or
//                          propagated through an `out bool skipped` the file feeds
//                          into RegressionOutcome.Skip.
//   * PRODUCER REPORTED IT `var src = ReadCode(path, failures); if (src == null)
//                          return;` - the failure was recorded ONE STATEMENT EARLIER
//                          by a callee that was handed the failure list.
//   * SIBLING REPORTED IT  an earlier branch on the SAME identifiers already
//                          recorded the miss; this guard only stops a second one.
//   * INCOMING VALUES      a guard on a PARAMETER or a foreach variable skips ONE
//                          ITEM, not a section - the dependency was produced
//                          elsewhere and belongs to whoever produced it. Only guards
//                          on a value THIS METHOD PRODUCED are judged.
//   * A REASON AT THE SITE a comment at the site saying the miss is already reported
//                          elsewhere. Per-SITE and self-documenting.
//
// ⛔ THERE IS NO FILE-LEVEL OPT-OUT. `hollow-pass-ok` must sit INSIDE the guard
// block it excuses. A file-level token is a broad opt-out, and a broad opt-out is
// how the previous baseline (KnownHollowPassFiles) made every hollow pass in six
// whole files invisible, including new ones.
//
// ── THIS SCANNER MUST NOT BECOME THE THING IT HUNTS ──────────────────────────
// A detector that silently returns "no findings" when it cannot parse its input is
// itself a hollow pass, pointed at the whole tree. So Scan() reports an
// `analysisError` for unbalanced braces or an unexpected throw, and THE CALLER IS
// CONTRACTUALLY REQUIRED TO FAIL ON IT. An empty finding list means something only
// when analysisError is empty. See RegressionMarkerRegression RULE 4.
//
// ── SCOPE: VERDICT METHODS ONLY ──────────────────────────────────────────────
// Only code that can contribute to a verdict can hollow-pass it, so only VERDICT
// METHODS are scanned: `bool Run(out string reason)` and the `void CheckX(
// List<string> failures, ...)` sections it calls. A private helper returning a value
// cannot green a suite by returning early. This is a control-flow narrowing, not a
// vocabulary one - it does not depend on any method being NAMED anything.
// =============================================================================

using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace DeNelle.Editor.Regression
{
    /// <summary>One detected hollow pass. Line is 1-based.</summary>
    public sealed class HollowPassFinding
    {
        public int Line;
        public string File;
        public string Arm;
        public string Guard;
        public string Detail;

        public override string ToString()
        {
            return (string.IsNullOrEmpty(File) ? string.Empty : File + ":") + Line +
                   " [" + Arm + "] guard '" + Guard + "' - " + Detail;
        }
    }

    /// <summary>
    /// Scope-aware hollow-pass detection. Reads C# source TEXT (never a path), so it is
    /// equally drivable from a real file and from the known-input fixtures that pin it.
    /// </summary>
    public static class HollowPassScanner
    {
        // Declared as a balanced PAIR on one line on purpose (RegressionMarkerRegression's
        // precedent): a lone brace char literal trips the CLAUDE.md rule-1 brace counter
        // and the CompileGate scan.
        private const char OpenBrace = '{', CloseBrace = '}';

        /// <summary>The per-SITE opt-out. Must appear INSIDE the guard block it excuses.</summary>
        public const string OptOutToken = "hollow-pass-ok";

        public const string ArmMissingDependency = "A-missing-dependency";
        public const string ArmSaysSkip = "B-says-skip";
        public const string ArmNegatedGuard = "C-negated-guard";
        public const string ArmVacuous = "D-vacuous-against-absent-fixture";

        // ---------------------------------------------------------------------
        //  Vocabularies. NOTE the asymmetry, and it is deliberate: the EXONERATING
        //  side may be generous, because it only ever clears a site that the
        //  CONTROL-FLOW shape already selected. No site is judged by words alone.
        // ---------------------------------------------------------------------

        private static readonly Regex AssertionCall = new Regex(
            @"\b(?:failures|fails|failure|errs|errors|problems|issues|f)\s*\.\s*Add\s*\(",
            RegexOptions.Compiled);

        // The verdict block by NAME (the common case); AccumulatorVerdict generalises it.
        private static readonly Regex VerdictCondition = new Regex(
            @"\b(?:failures|fails|failure|errs|errors|problems|issues|f)\s*\.\s*Count\s*(?:==|<=)\s*0",
            RegexOptions.Compiled);

        // `X.Count == 0` where X is a collection this method ADDS to = the verdict block,
        // whatever X is called.
        private static readonly Regex AccumulatorCondition = new Regex(
            @"\b([A-Za-z_]\w*)\s*\.\s*(?:Count|Length)\s*(?:==|<=)\s*0", RegexOptions.Compiled);

        private static readonly string[] DeclaredStandDownTokens =
        {
            "RegressionOutcome.Skip",
            "RegressionOutcome.PartialSkip",
            RegressionOutcome.SkipToken,
            RegressionOutcome.PartialSkipToken,
        };

        private static readonly Regex MissingDependencyGuard = new Regex(
            @"==\s*null|\bIsNullOrEmpty\b|\bIsNullOrWhiteSpace\b|!\s*File\.Exists|!\s*Directory\.Exists|" +
            @"==\s*default|\bLength\s*==\s*0|\bCount\s*==\s*0",
            RegexOptions.Compiled);

        private static readonly Regex NegatedGuard = new Regex(
            @"(^|[\s(&|])!\s*[A-Za-z_]", RegexOptions.Compiled);

        // ARM D's positive side is deliberately NARROW - existence of a FIXTURE, never
        // `.Count > 0` on a list the method just built (that is the ordinary
        // "report what I found" shape and it asserts perfectly well).
        private static readonly Regex PositiveExistenceGuard = new Regex(
            @"!=\s*null|\bFile\.Exists\s*\(|\bDirectory\.Exists\s*\(", RegexOptions.Compiled);

        private static readonly string[] SkipWords =
        {
            "skip", "skipped", "stand down", "stand-down", "standing down",
            "not available", "unavailable", "not present", "no fixture",
        };

        // A comment at the SITE saying the miss is reported elsewhere. Per-site and
        // self-documenting: it names a reason a reader can check, which is exactly what
        // a blanket opt-out never does.
        private static readonly Regex AlreadyReportedNote = new Regex(
            @"(?:already|elsewhere|the caller|above|case\s*\d)[^\n]" + Q(0, 70) + @"?(?:report|fail|flag|catch)" +
            @"|(?:report|fail|flag|catch)[^\n]" + Q(0, 50) + @"?(?:already|elsewhere|above)" +
            @"|double[- ]fail",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex MethodHeader = new Regex(
            @"(?:public|private|internal|protected)[A-Za-z0-9_<>\[\],\.\s]*?\b(\w+)\s*\(([^()]*)\)\s*",
            RegexOptions.Compiled);

        // `IReadOnlyList<string>` must NOT read as the failure list - that mistake pulled
        // ordinary bool helpers into scope and produced pure noise.
        private static readonly Regex FailureListParam = new Regex(
            @"(?<![\w\.])List<string>", RegexOptions.Compiled);

        private static readonly Regex ParamName = new Regex(@"([A-Za-z_]\w*)\s*(?:,|$)", RegexOptions.Compiled);
        private static readonly Regex LocalDecl = new Regex(
            @"\b(?:var|[A-Za-z_][\w<>\[\],\.\?]*)\s+([A-Za-z_]\w*)\s*=[^=]", RegexOptions.Compiled);
        private static readonly Regex ForeachVar = new Regex(
            @"\bforeach\s*\(\s*[\w<>\[\],\.\?]+\s+([A-Za-z_]\w*)\s+in\b", RegexOptions.Compiled);
        private static readonly Regex FailDelegateParam = new Regex(
            @"Action<string>\s+([A-Za-z_]\w*)", RegexOptions.Compiled);
        private static readonly Regex OutParam = new Regex(
            @"\bout\s+[A-Za-z_][\w<>\[\],\.\?]*\s+([A-Za-z_]\w*)", RegexOptions.Compiled);

        private static readonly Regex ReturnStatement = new Regex(
            @"\breturn\s*(?:true\s*)?;", RegexOptions.Compiled);
        private static readonly Regex Identifier = new Regex(@"\b[A-Za-z_]\w*\b", RegexOptions.Compiled);
        private static readonly Regex ReasonAssign = new Regex(@"\breason\s*=", RegexOptions.Compiled);

        /// <summary>A brace-quantifier built at runtime - a literal one would trip the
        /// CLAUDE.md rule-1 naive brace counter on this very file.</summary>
        private static string Q(int lo, int hi)
        {
            return OpenBrace.ToString() + lo + "," + hi + CloseBrace.ToString();
        }

        // =====================================================================
        //  Entry point
        // =====================================================================

        /// <summary>
        /// Findings for one C# source text. <paramref name="analysisError"/> is EMPTY on a
        /// clean analysis; when it is non-empty THE RESULT LIST IS MEANINGLESS and the
        /// caller MUST fail - a detector that cannot read its input has proven nothing.
        /// </summary>
        public static List<HollowPassFinding> Scan(string source, out string analysisError)
        {
            return Scan(source, null, out analysisError);
        }

        /// <summary>As Scan(source, out error), stamping each finding with a file name.</summary>
        public static List<HollowPassFinding> Scan(string source, string fileName, out string analysisError)
        {
            analysisError = string.Empty;
            var findings = new List<HollowPassFinding>();
            if (source == null) { analysisError = "source was null"; return findings; }
            if (source.Length == 0) return findings;   // an empty file has no control flow

            try
            {
                string src = source.Replace("\r\n", "\n").Replace('\r', '\n');
                string words, skel;
                BuildMasks(src, out words, out skel);

                if (words.Length != src.Length || skel.Length != src.Length)
                {
                    analysisError = "mask length mismatch (src " + src.Length + ", words " + words.Length +
                                    ", skel " + skel.Length + ") - index correspondence is the whole basis " +
                                    "of the control-flow walk, so the scan REFUSES rather than reporting clean";
                    return findings;
                }

                string balanceError;
                if (!BracesBalance(skel, out balanceError)) { analysisError = balanceError; return findings; }

                int[] lineStarts = LineStarts(src);
                bool declaresStandDown = ContainsAny(words, DeclaredStandDownTokens);

                foreach (var method in VerdictMethods(skel))
                {
                    ScanMethod(src, words, skel, lineStarts, method, declaresStandDown, fileName, findings);
                }
            }
            catch (Exception ex)
            {
                analysisError = "hollow-pass scanner threw " + ex.GetType().Name + ": " + ex.Message;
                findings.Clear();
            }
            return findings;
        }

        // =====================================================================
        //  One verdict method
        // =====================================================================

        private static void ScanMethod(string src, string words, string skel, int[] lineStarts,
                                       MethodSpan method, bool declaresStandDown, string fileName,
                                       List<HollowPassFinding> findings)
        {
            string seg = skel.Substring(method.Start, method.Length);

            var incoming = new HashSet<string>(StringComparer.Ordinal);
            foreach (Match m in ParamName.Matches(method.Parameters)) incoming.Add(m.Groups[1].Value);
            foreach (Match m in ForeachVar.Matches(seg)) incoming.Add(m.Groups[1].Value);

            var produced = new HashSet<string>(StringComparer.Ordinal);
            foreach (Match m in LocalDecl.Matches(seg))
            {
                string id = m.Groups[1].Value;
                if (!incoming.Contains(id)) produced.Add(id);
            }

            var delegates = new List<string>();
            foreach (Match m in FailDelegateParam.Matches(method.Parameters)) delegates.Add(m.Groups[1].Value);

            var outParams = new List<string>();
            foreach (Match m in OutParam.Matches(method.Parameters)) outParams.Add(m.Groups[1].Value);

            foreach (Match ret in ReturnStatement.Matches(seg))
            {
                int r = method.Start + ret.Index;

                int openIdx, closeIdx;
                string condition;
                bool braced;
                if (!GuardBlockFor(skel, r, method.Start, out openIdx, out closeIdx, out condition, out braced))
                    continue;

                // THE GUARD PATH: what definitely runs before this return, at the block's
                // own nesting depth. Not a window - a scope.
                string path = braced ? words.Substring(openIdx, r - openIdx) : string.Empty;
                string topLevel = braced ? TopLevelOnly(skel, words, openIdx, r) : string.Empty;

                if (ContainsAny(path, DeclaredStandDownTokens)) continue;
                if (topLevel.IndexOf("return false", StringComparison.Ordinal) >= 0) continue;
                if (Asserted(topLevel, delegates)) continue;
                if (declaresStandDown && AssignsAnyOf(topLevel, outParams)) continue;
                if (VerdictCondition.IsMatch(condition)) continue;
                if (AccumulatorVerdict(seg, condition)) continue;
                if (ReportedByProducer(words.Substring(method.Start, r - method.Start), condition)) continue;

                if (braced && RawSlice(src, openIdx, closeIdx)
                        .IndexOf(OptOutToken, StringComparison.OrdinalIgnoreCase) >= 0) continue;

                int eol = src.IndexOf('\n', r);
                if (eol < 0) eol = src.Length;
                if (AlreadyReportedNote.IsMatch(RawSlice(src, Math.Max(0, openIdx - 300), eol))) continue;

                // A guard on an INCOMING value skips ONE ITEM, not a section, and belongs
                // to whoever produced the value. Only a dependency THIS METHOD FETCHED can
                // hollow-pass this method's own verdict.
                if (!SharesIdentifier(condition, produced)) continue;
                if (SiblingReported(skel, seg, method.Start, r, condition)) continue;

                int line = LineOf(lineStarts, r);
                bool returnsTrue = ret.Value.IndexOf("true", StringComparison.Ordinal) >= 0;

                if (MissingDependencyGuard.IsMatch(condition))
                {
                    findings.Add(Make(line, fileName, ArmMissingDependency, condition,
                        (returnsTrue ? "returns TRUE" : "returns") +
                        " out of a null/empty/missing-dependency guard having asserted nothing. The" +
                        " caller's only channel is the bool, so this reads as a PASS. Resolve it under" +
                        " the three-way rule: fixture-absent -> FAIL naming the missing path;" +
                        " harness-capability-absent -> RegressionOutcome.PartialSkip;" +
                        " content/art-absent -> assert THROUGH the proven fallback."));
                    continue;
                }
                if (SaysSkip(path) || SaysSkip(condition))
                {
                    findings.Add(Make(line, fileName, ArmSaysSkip, condition,
                        "a guarded block that TELLS THE READER it is standing down, in words, and tells" +
                        " the CALLER nothing - it returns without asserting and without a" +
                        " RegressionOutcome.Skip / PartialSkip token, so it lands in the GREEN column."));
                    continue;
                }
                if (NegatedGuard.IsMatch(condition) && (returnsTrue || AssignsReasonOrNote(path)))
                {
                    findings.Add(Make(line, fileName, ArmNegatedGuard, condition,
                        "a NEGATED call/flag guard answering OK with nothing asserted. This is the form" +
                        " that evaded the token-matching detector on 2026-08-16: identical damage to a" +
                        " null guard, with no null token anywhere in the condition."));
                }
            }

            ScanVacuous(skel, lineStarts, method, seg, declaresStandDown, fileName, findings);
        }

        // =====================================================================
        //  ARM D  --  vacuous against an absent fixture
        // =====================================================================
        // RaidCooldownRegression case 5, 2026-08-21: a teardown left GameStateService
        // holding a DESTROYED state, so the save read back as null and every later case
        // asserted against nothing while still reporting green. Nothing in the case's own
        // text was wrong - the state under it had been demolished.
        //
        // The decidable shape of that family: a verdict method in which EVERY assertion is
        // nested inside a POSITIVE-EXISTENCE guard, with nothing asserting the fixture
        // exists. If the fixture is absent the method checks ZERO things and reports green.
        //
        // Deliberately METHOD-scoped and deliberately requiring ALL assertions to be
        // guarded. One `if (rec != null) { ... }` alongside unconditional assertions is
        // normal and fine; a method whose ENTIRE assertion set hangs off a single existence
        // test has no floor at all.
        private static void ScanVacuous(string skel, int[] lineStarts, MethodSpan method,
                                        string seg, bool declaresStandDown,
                                        string fileName, List<HollowPassFinding> findings)
        {
            if (declaresStandDown) return;

            var asserts = new List<int>();
            foreach (Match a in AssertionCall.Matches(seg)) asserts.Add(method.Start + a.Index);
            if (asserts.Count == 0) return;

            string firstGuard = null;
            foreach (int a in asserts)
            {
                string condition = EnclosingPositiveGuard(skel, a, method.Start);
                if (condition == null) return;                 // it has a floor - not vacuous
                if (firstGuard == null) firstGuard = condition;
            }

            findings.Add(Make(LineOf(lineStarts, asserts[0]), fileName, ArmVacuous, firstGuard,
                "EVERY assertion in this verdict method is nested inside a positive-existence guard," +
                " and nothing asserts that the fixture exists. If it is absent the method checks ZERO" +
                " things and still reports green - RaidCooldownRegression case 5, 2026-08-21, where a" +
                " teardown left a DESTROYED state installed and the cases under it measured nothing." +
                " Assert the fixture's health, or declare the stand-down."));
        }

        private static string EnclosingPositiveGuard(string skel, int idx, int floor)
        {
            int cursor = idx;
            while (true)
            {
                int openIdx = EnclosingOpener(skel, cursor, floor);
                if (openIdx < 0) return null;
                string condition = ConditionOf(skel, BlockHeader(skel, openIdx), openIdx);
                if (condition != null && PositiveExistenceGuard.IsMatch(condition) &&
                    !MissingDependencyGuard.IsMatch(condition)) return condition;
                if (openIdx <= floor) return null;
                cursor = openIdx;
            }
        }

        // =====================================================================
        //  Exonerations
        // =====================================================================

        private static bool Asserted(string text, List<string> delegates)
        {
            if (string.IsNullOrEmpty(text)) return false;
            if (AssertionCall.IsMatch(text)) return true;
            foreach (var d in delegates)
                if (Regex.IsMatch(text, @"\b" + Regex.Escape(d) + @"\s*\(")) return true;
            return false;
        }

        private static bool AssignsAnyOf(string text, List<string> names)
        {
            if (string.IsNullOrEmpty(text)) return false;
            foreach (var n in names)
                if (Regex.IsMatch(text, @"\b" + Regex.Escape(n) + @"\s*=[^=]")) return true;
            return false;
        }

        /// <summary>
        /// `if (X.Count == 0) return true;` where THIS METHOD adds to X is the VERDICT
        /// block - "nothing was recorded, therefore pass". Keyed on the method actually
        /// accumulating into X, not on X being named `failures`.
        /// </summary>
        private static bool AccumulatorVerdict(string methodSkeleton, string condition)
        {
            foreach (Match m in AccumulatorCondition.Matches(condition))
            {
                string id = m.Groups[1].Value;
                if (Regex.IsMatch(methodSkeleton, @"\b" + Regex.Escape(id) + @"\s*\.\s*Add\s*\(")) return true;
            }
            return false;
        }

        /// <summary>
        /// The guarded value came from a call that was HANDED the failure list, so the
        /// absence was already RECORDED one statement earlier, by the callee. The assertion
        /// happened - it just happened across a call boundary.
        /// </summary>
        private static bool ReportedByProducer(string methodWordsUpToReturn, string condition)
        {
            foreach (string name in IdentifiersOf(condition))
            {
                foreach (Match m in Regex.Matches(methodWordsUpToReturn,
                             @"\b" + Regex.Escape(name) + @"\s*=\s*[A-Za-z_][\w\.<>]*\s*\("))
                {
                    string args = ArgListAt(methodWordsUpToReturn, m.Index + m.Length - 1);
                    foreach (var fn in new[] { "failures", "fails", "failure", "errs", "errors", "problems", "issues", "f" })
                        if (Regex.IsMatch(args, @"\b" + fn + @"\b")) return true;
                }
            }
            return false;
        }

        /// <summary>
        /// An earlier branch on the SAME identifiers already recorded the miss, so this
        /// guard is only stopping a second, redundant report. Handles the braced sibling
        /// (walk its enclosing `if` headers) AND the brace-less one, whose condition lives
        /// in the PRECEDING STATEMENT rather than in a block header.
        /// </summary>
        private static bool SiblingReported(string skel, string seg, int methodStart,
                                            int returnIdx, string condition)
        {
            var names = IdentifiersOf(condition);
            if (names.Count == 0) return false;
            string upto = seg.Substring(0, returnIdx - methodStart);

            foreach (Match a in AssertionCall.Matches(upto))
            {
                int back = upto.LastIndexOf(';', Math.Max(0, a.Index - 1));
                back = Math.Max(back, upto.LastIndexOf(OpenBrace, Math.Max(0, a.Index - 1)));
                back = Math.Max(back, upto.LastIndexOf(CloseBrace, Math.Max(0, a.Index - 1)));
                if (back >= 0 && SharesIdentifier(upto.Substring(back, a.Index - back), names)) return true;

                int o = EnclosingOpener(skel, methodStart + a.Index, methodStart);
                while (o > 0)
                {
                    string c = ConditionOf(skel, BlockHeader(skel, o), o);
                    if (c != null && SharesIdentifier(c, names)) return true;
                    if (o <= methodStart) break;
                    o = EnclosingOpener(skel, o, methodStart);
                }
            }
            return false;
        }

        // =====================================================================
        //  Control-flow primitives  --  braces and statement boundaries ONLY
        // =====================================================================

        private static bool GuardBlockFor(string skel, int returnIdx, int floor,
                                          out int openIdx, out int closeIdx,
                                          out string condition, out bool braced)
        {
            openIdx = closeIdx = -1; condition = null; braced = false;

            // Brace-less guard: `if (cond) return;` / `if (cond)\n    return;`
            int p = returnIdx - 1;
            while (p >= floor && char.IsWhiteSpace(skel[p])) p--;
            if (p >= floor && skel[p] == ')')
            {
                int lp = MatchingOpenParen(skel, p);
                if (lp > floor)
                {
                    int q = lp - 1;
                    while (q >= floor && char.IsWhiteSpace(skel[q])) q--;
                    if (q >= floor + 1 && skel[q] == 'f' && skel[q - 1] == 'i' &&
                        (q - 2 < 0 || !char.IsLetterOrDigit(skel[q - 2])))
                    {
                        condition = skel.Substring(lp + 1, p - lp - 1);
                        openIdx = lp; closeIdx = returnIdx; braced = false;
                        return true;
                    }
                }
            }

            int open = EnclosingOpener(skel, returnIdx, floor);
            if (open < 0) return false;
            string cond = ConditionOf(skel, BlockHeader(skel, open), open);
            if (cond == null) return false;
            int close = MatchingClose(skel, open);
            if (close < 0) return false;

            openIdx = open; closeIdx = close; condition = cond; braced = true;
            return true;
        }

        /// <summary>Index of the OPENING BRACE of the innermost block containing idx, or -1.</summary>
        private static int EnclosingOpener(string skel, int idx, int floor)
        {
            int pending = 0;
            for (int i = idx - 1; i >= floor; i--)
            {
                char c = skel[i];
                if (c == CloseBrace) pending++;
                else if (c == OpenBrace)
                {
                    if (pending == 0) return i;
                    pending--;
                }
            }
            return -1;
        }

        private static int MatchingClose(string skel, int openIdx)
        {
            int depth = 0;
            for (int i = openIdx; i < skel.Length; i++)
            {
                if (skel[i] == OpenBrace) depth++;
                else if (skel[i] == CloseBrace)
                {
                    depth--;
                    if (depth == 0) return i;
                }
            }
            return -1;
        }

        private static int MatchingOpenParen(string skel, int closeIdx)
        {
            int depth = 0;
            for (int i = closeIdx; i >= 0; i--)
            {
                if (skel[i] == ')') depth++;
                else if (skel[i] == '(')
                {
                    depth--;
                    if (depth == 0) return i;
                }
            }
            return -1;
        }

        private static string ArgListAt(string text, int fromIdx)
        {
            int lp = text.IndexOf('(', fromIdx);
            if (lp < 0) return string.Empty;
            int depth = 0;
            for (int i = lp; i < text.Length; i++)
            {
                if (text[i] == '(') depth++;
                else if (text[i] == ')')
                {
                    depth--;
                    if (depth == 0) return text.Substring(lp + 1, i - lp - 1);
                }
            }
            return string.Empty;
        }

        /// <summary>
        /// The text between the previous STATEMENT BOUNDARY and this OPENING BRACE. That is the
        /// block's header - `if (x == null)`, `foreach (var p in list)`, `else`, a method
        /// signature. Statement boundaries, not line counts, are what make this immune to
        /// reflowing.
        /// </summary>
        private static string BlockHeader(string skel, int openIdx)
        {
            int i = openIdx - 1;
            while (i >= 0)
            {
                char c = skel[i];
                if (c == ';' || c == OpenBrace || c == CloseBrace) break;
                i--;
            }
            return skel.Substring(i + 1, openIdx - i - 1);
        }

        /// <summary>The condition of an `if` header, or null when the header is not one.</summary>
        private static string ConditionOf(string skel, string header, int openIdx)
        {
            if (!header.TrimEnd().EndsWith(")", StringComparison.Ordinal)) return null;

            int close = openIdx - 1;
            while (close >= 0 && skel[close] != ')') close--;
            if (close < 0) return null;
            int lp = MatchingOpenParen(skel, close);
            if (lp < 0) return null;

            int q = lp - 1;
            while (q >= 0 && char.IsWhiteSpace(skel[q])) q--;
            if (q < 1) return null;
            if (skel[q] != 'f' || skel[q - 1] != 'i') return null;
            if (q - 2 >= 0 && char.IsLetterOrDigit(skel[q - 2])) return null;
            return skel.Substring(lp + 1, close - lp - 1);
        }

        /// <summary>
        /// The slice from openIdx to endIdx with every NESTED block blanked out, so what
        /// remains is exactly the statements that run at this block's own depth - the
        /// "definitely executed before the return" set. A scope, never a window.
        /// </summary>
        private static string TopLevelOnly(string skel, string words, int openIdx, int endIdx)
        {
            var sb = new StringBuilder(Math.Max(0, endIdx - openIdx));
            int depth = 0;
            for (int i = openIdx; i < endIdx; i++)
            {
                char c = skel[i];
                if (c == OpenBrace) { depth++; if (depth == 1) continue; }
                if (c == CloseBrace) { depth--; if (depth == 0) continue; }
                sb.Append(depth == 1 ? words[i] : (c == '\n' ? '\n' : ' '));
            }
            return sb.ToString();
        }

        // =====================================================================
        //  Masking  --  comments out, literals kept (words) / blanked (skel)
        // =====================================================================
        // TWO masks of IDENTICAL LENGTH so an index means the same thing in both.
        //   words - comments blanked, string CONTENTS KEPT. The word "SKIPPED" lives in a
        //           literal, so ARM B needs it.
        //   skel  - comments AND string contents blanked. A brace or paren inside a literal
        //           must never move the control-flow walk.
        private static void BuildMasks(string src, out string words, out string skel)
        {
            var w = new StringBuilder(src.Length);
            var s = new StringBuilder(src.Length);
            int i = 0;
            while (i < src.Length)
            {
                char c = src[i];
                char n = i + 1 < src.Length ? src[i + 1] : '\0';

                if (c == '/' && n == '/')
                {
                    while (i < src.Length && src[i] != '\n') { w.Append(' '); s.Append(' '); i++; }
                    continue;
                }
                if (c == '/' && n == '*')
                {
                    while (i < src.Length && !(src[i] == '*' && i + 1 < src.Length && src[i + 1] == '/'))
                    {
                        char k = src[i] == '\n' ? '\n' : ' ';
                        w.Append(k); s.Append(k); i++;
                    }
                    if (i < src.Length) { w.Append(' '); s.Append(' '); i++; }
                    if (i < src.Length) { w.Append(' '); s.Append(' '); i++; }
                    continue;
                }
                if (c == '\'')
                {
                    w.Append(c); s.Append(' '); i++;
                    while (i < src.Length && src[i] != '\'')
                    {
                        if (src[i] == '\\' && i + 1 < src.Length) { w.Append(src[i]); s.Append(' '); i++; }
                        w.Append(src[i]); s.Append(' '); i++;
                    }
                    if (i < src.Length) { w.Append(src[i]); s.Append(' '); i++; }
                    continue;
                }
                bool verbatim = (c == '@' && n == '"')
                             || (c == '$' && n == '@' && i + 2 < src.Length && src[i + 2] == '"');
                if (verbatim)
                {
                    int quote = src.IndexOf('"', i);
                    for (int k = i; k <= quote; k++) { w.Append(src[k]); s.Append(' '); }
                    i = quote + 1;
                    while (i < src.Length)
                    {
                        if (src[i] == '"' && i + 1 < src.Length && src[i + 1] == '"')
                        { w.Append(src[i]); s.Append(' '); w.Append(src[i + 1]); s.Append(' '); i += 2; continue; }
                        if (src[i] == '"') { w.Append(src[i]); s.Append(' '); i++; break; }
                        w.Append(src[i]); s.Append(src[i] == '\n' ? '\n' : ' '); i++;
                    }
                    continue;
                }
                if (c == '$' && n == '"') { w.Append(c); s.Append(' '); i++; c = src[i]; }
                if (c == '"')
                {
                    w.Append(c); s.Append(' '); i++;
                    while (i < src.Length && src[i] != '"')
                    {
                        if (src[i] == '\\' && i + 1 < src.Length)
                        {
                            w.Append(src[i]); s.Append(' '); i++;
                            if (i < src.Length) { w.Append(src[i]); s.Append(' '); i++; }
                            continue;
                        }
                        w.Append(src[i]); s.Append(src[i] == '\n' ? '\n' : ' '); i++;
                    }
                    if (i < src.Length) { w.Append(src[i]); s.Append(' '); i++; }
                    continue;
                }
                w.Append(c); s.Append(c); i++;
            }
            words = w.ToString();
            skel = s.ToString();
        }

        private static bool BracesBalance(string skel, out string error)
        {
            error = string.Empty;
            int depth = 0;
            for (int i = 0; i < skel.Length; i++)
            {
                if (skel[i] == OpenBrace) depth++;
                else if (skel[i] == CloseBrace)
                {
                    depth--;
                    if (depth < 0)
                    {
                        error = "unbalanced braces (a close with no open) - the control-flow walk cannot be " +
                                "trusted on this file, so the scan REFUSES rather than reporting clean";
                        return false;
                    }
                }
            }
            if (depth != 0)
            {
                error = "unbalanced braces (" + depth + " unclosed) - the control-flow walk cannot be " +
                        "trusted on this file, so the scan REFUSES rather than reporting clean";
                return false;
            }
            return true;
        }

        // =====================================================================
        //  Method discovery
        // =====================================================================

        private struct MethodSpan
        {
            public int Start, Length;
            public string Parameters;
            public MethodSpan(int start, int length, string parameters)
            {
                Start = start; Length = length; Parameters = parameters ?? string.Empty;
            }
        }

        private static List<MethodSpan> VerdictMethods(string skel)
        {
            var result = new List<MethodSpan>();
            foreach (Match m in MethodHeader.Matches(skel))
            {
                int after = m.Index + m.Length;
                while (after < skel.Length && char.IsWhiteSpace(skel[after])) after++;
                if (after >= skel.Length || skel[after] != OpenBrace) continue;

                string name = m.Groups[1].Value;
                string parameters = m.Groups[2].Value;
                bool verdict = FailureListParam.IsMatch(parameters)
                            || (parameters.IndexOf("out string", StringComparison.Ordinal) >= 0 &&
                                (name == "Run" || name == "RunAll" || name == "RunCore"));
                if (!verdict) continue;

                int close = MatchingClose(skel, after);
                if (close < 0) continue;
                result.Add(new MethodSpan(after, close - after + 1, parameters));
            }
            return result;
        }

        // =====================================================================
        //  Small helpers
        // =====================================================================

        private static HollowPassFinding Make(int line, string fileName, string arm, string guard, string detail)
        {
            return new HollowPassFinding
            {
                Line = line,
                File = fileName,
                Arm = arm,
                Guard = Condense(guard),
                Detail = detail,
            };
        }

        private static HashSet<string> IdentifiersOf(string text)
        {
            var set = new HashSet<string>(StringComparer.Ordinal);
            if (string.IsNullOrEmpty(text)) return set;
            foreach (Match m in Identifier.Matches(text)) set.Add(m.Value);
            return set;
        }

        private static bool SharesIdentifier(string text, ICollection<string> names)
        {
            if (names == null || names.Count == 0 || string.IsNullOrEmpty(text)) return false;
            foreach (Match m in Identifier.Matches(text))
                if (names.Contains(m.Value)) return true;
            return false;
        }

        private static int[] LineStarts(string src)
        {
            var starts = new List<int>();
            starts.Add(0);
            for (int i = 0; i < src.Length; i++) if (src[i] == '\n') starts.Add(i + 1);
            return starts.ToArray();
        }

        private static int LineOf(int[] lineStarts, int idx)
        {
            int lo = 0, hi = lineStarts.Length - 1;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) / 2;
                if (lineStarts[mid] <= idx) lo = mid; else hi = mid - 1;
            }
            return lo + 1;
        }

        private static string RawSlice(string src, int from, int to)
        {
            if (from < 0 || to <= from || to > src.Length) return string.Empty;
            return src.Substring(from, to - from);
        }

        private static bool ContainsAny(string haystack, string[] needles)
        {
            if (string.IsNullOrEmpty(haystack)) return false;
            foreach (var n in needles)
                if (haystack.IndexOf(n, StringComparison.Ordinal) >= 0) return true;
            return false;
        }

        private static bool SaysSkip(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            foreach (var wd in SkipWords)
                if (text.IndexOf(wd, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        private static bool AssignsReasonOrNote(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            return ReasonAssign.IsMatch(path) || path.IndexOf("notes.Add", StringComparison.Ordinal) >= 0;
        }

        /// <summary>Whitespace-collapsed, capped at 90 chars. This exact form is the LEDGER
        /// KEY in RegressionMarkerRegression, so it must stay stable.</summary>
        public static string Condense(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            string one = Regex.Replace(s, @"\s+", " ").Trim();
            return one.Length <= 90 ? one : one.Substring(0, 87) + "...";
        }
    }
}
