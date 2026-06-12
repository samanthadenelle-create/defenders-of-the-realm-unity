using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace DeNelle.Tests.EditMode
{
    // =============================================================================
    // PerFrameReentrancyLintTests (EditMode) — SYSTEMATIC guard for the
    // "per-frame Update self-multiplies / re-enters" failure class.
    // -----------------------------------------------------------------------------
    // WHY THIS EXISTS — this project has shipped the SAME bug shape TWICE:
    //
    //   1. THE FORK-BOMB LEAK (commit 69138fe). HeroLocomotion.HookDialogueGate /
    //      HeroBodySwapper.HookDialogueIdle re-called their own retry coroutine every
    //      frame with no single-flight guard. Finding no DialogueRunner, each frame
    //      started ANOTHER retry coroutine -> coroutines DOUBLED per frame -> ~3MB/s
    //      retained-heap leak -> GC-thrash "frame by frame" freeze in OuterWorld.
    //
    //   2. THE DIALOGUE-OPTION CRASH (2026-06-12). OptionItem.Update calls Submit()
    //      every frame; Submit()'s YarnTask continuation resumes SYNCHRONOUSLY INLINE,
    //      re-presents a fresh options view, whose auto-selected first option submits
    //      AGAIN on the same still-held keypress -> unbounded recursion -> freeze.
    //
    // Both are the SAME shape: an Update()/LateUpdate() (or a per-frame coroutine)
    // triggers something that RE-ENTERS or SPAWNS AGAIN, with no reentrancy guard.
    //
    // WHAT THIS LINT FLAGS (string/regex over source — deterministic, fast, no scene
    // load). It scans two kinds of per-frame-reachable method bodies:
    //   * void Update() / void LateUpdate()      (the per-frame entry points)
    //   * IEnumerator <coroutine>()              (a per-frame `yield return null` loop)
    //
    //   (A) FORK-BOMB pattern (HIGH signal). EITHER:
    //       (A1) an Update/LateUpdate body directly contains  StartCoroutine(  (a
    //            literal per-frame coroutine spawn), OR
    //       (A2) the SELF-SPAWN CYCLE — the EXACT 69138fe shape: an IEnumerator
    //            coroutine body calls a method X, and X's body calls StartCoroutine(
    //            (so the coroutine re-invokes its own spawner -> doubles per frame).
    //            This is the real fork-bomb: HeroLocomotion's StartCoroutine was NOT
    //            in Update() — it was in HookDialogueGate(), which the retry coroutine
    //            re-called. Rule (A1) alone would have MISSED it; (A2) catches it.
    //
    //   (B) RE-ENTRANT SUBMIT pattern (scoped TIGHT to kill noise): an Update/
    //       LateUpdate body calls  Submit( / OnSubmit(  — the exact verb whose inline
    //       YarnTask continuation re-presented options in the OptionItem crash. Broader
    //       verbs (Fire/Dispatch/Continue) were trialled and only produced false
    //       positives here (DefenseTower.Update->Fire(target) shoots a projectile), so
    //       they are intentionally OUT of scope.
    //
    // OPT-OUT (a properly guarded Update PASSES). A flagged Update is ACCEPTED if the
    // class shows a single-flight / reentrancy guard, ANY of:
    //   * a known guard field name referenced in the class:
    //       _retryingHook / _submitting / s_submitting / s_submitInFlight /
    //       _submitted / _wired / _hooked / _started / _running / _inFlight /
    //       s_lastSubmitFrame  (covers BOTH historical fixes verbatim);
    //   * a generic single-flight bool field (  bool _<name>  /  static bool s_<name> )
    //     that is BOTH assigned true and gated by an  if (...) return;  / `?` early-out
    //     in the class (the universal "set a flag, bail if already set" idiom);
    //   * an explicit inline escape hatch comment:  // perf-lint-ok: <reason>  in the
    //     method body (for a deliberate, reviewed exception).
    //
    // CALIBRATION (mentally run against the CURRENT tree — see the test asserts):
    //   * HeroLocomotion / HeroBodySwapper — FIXED: their Update path is guarded by
    //     `_retryingHook` (single-flight). PASS. The UNGUARDED original (no
    //     `_retryingHook`, retry re-called the spawner) would FAIL (A).
    //   * OptionItem (vendored Yarn ClassicRPG addon) — FIXED: `s_submitInFlight` /
    //     `_submitted` / `s_lastSubmitFrame` guards. PASS. The UNGUARDED original
    //     (Update -> Submit -> inline re-present, no global guard) would FAIL (B).
    //
    // KNOWN LIMITS (documented honestly):
    //   * Heuristic, not a data-flow analysis. It proves a GUARD EXISTS in the class,
    //     not that the guard actually covers the risky call. That trade keeps it fast
    //     + low-false-positive; the goal is to make a re-introduced UNGUARDED fork-bomb
    //     / reentrant-submit go RED, while a guarded one (the fixed shape) stays GREEN.
    //   * It scans Update/LateUpdate bodies. A coroutine that re-spawns itself with no
    //     Update entry point is out of scope (neither historical bug had that shape —
    //     both were reached from a per-frame entry). If a third instance ever shows
    //     that shape, widen Rule (A) to IEnumerator bodies that StartCoroutine a method
    //     matching their own spawner.
    //   * If a guarded Update is mis-flagged, add a `// perf-lint-ok: <reason>` line in
    //     the method body rather than weakening the rule.
    // =============================================================================
    public class PerFrameReentrancyLintTests
    {
        // Roots scanned. Gameplay modules + the vendored Yarn addons we edit (OptionItem
        // lives here). Library/PackageCache copies are NOT scanned (read-only, not ours).
        private static readonly string[] ScanRootsRelativeToProject =
        {
            "Assets/_Modules",
            "Packages/dev.yarnspinner.unity.addons.classicrpg/Runtime",
        };

        // Re-entrant continuation verbs (Rule B). DELIBERATELY narrow: only Submit /
        // OnSubmit — the EXACT verb whose inline YarnTask continuation re-entered in the
        // OptionItem crash. Broader verbs (Fire / Dispatch / Continue) were trialled and
        // produced pure NOISE in this codebase — DefenseTower.Update -> Fire(target) shoots
        // a projectile, DungeonStubEncounter.Update -> Fire() raises an effect; neither
        // re-presents/re-enters. Tower "Fire" is not a continuation, so it is NOT in scope.
        // If a real re-entrant dispatch verb shows up later, add it here with a calibration
        // note rather than widening blindly.
        private static readonly string[] ReentrantCallVerbs =
        {
            "Submit", "OnSubmit",
        };

        // Explicit guard field names that immediately satisfy the opt-out — these cover
        // both historical fixes verbatim plus the common single-flight idioms.
        private static readonly string[] KnownGuardFieldTokens =
        {
            "_retryingHook", "_submitting", "s_submitting", "s_submitInFlight",
            "_submitted", "_wired", "_hooked", "_started", "_running", "_inFlight",
            "s_lastSubmitFrame", "_dispatching", "_firing",
        };

        // ── tests ────────────────────────────────────────────────────────────────

        [Test]
        public void NoUnguardedPerFrameForkBombOrReentrantSubmit()
        {
            var offenders = ScanForOffenders();

            Assert.IsEmpty(offenders,
                "Per-frame self-multiply / re-entrancy smell found (the fork-bomb / OptionItem " +
                "failure class). An Update()/LateUpdate() spawns a coroutine or calls a " +
                "re-entrant submit/dispatch WITHOUT a single-flight guard. Add a guard field " +
                "(set true, `if (flag) return;` before the risky call) or a " +
                "`// perf-lint-ok: <reason>` marker.\n  " +
                string.Join("\n  ", offenders));
        }

        // CALIBRATION pin 1: the FIXED fork-bomb files must PASS (their guard is intact).
        // If someone strips `_retryingHook`, the main test goes RED and so does this.
        [Test]
        public void FixedForkBombFiles_AreNotFlagged()
        {
            var offenders = ScanForOffenders();
            foreach (var f in new[] { "HeroLocomotion.cs", "HeroBodySwapper.cs" })
                Assert.IsFalse(offenders.Any(o => o.Contains(f)),
                    $"{f} should PASS the per-frame lint — its retry spawn is single-flight " +
                    "guarded by `_retryingHook`. If this fails the guard was removed (the " +
                    "fork-bomb is back). Offenders: " + string.Join(", ", offenders));
        }

        // CALIBRATION pin 2: the FIXED OptionItem must PASS (s_submitInFlight guard).
        [Test]
        public void FixedOptionItem_IsNotFlagged()
        {
            var offenders = ScanForOffenders();
            Assert.IsFalse(offenders.Any(o => o.Contains("OptionItem.cs")),
                "OptionItem.cs should PASS — Update->Submit is guarded by s_submitInFlight / " +
                "_submitted / s_lastSubmitFrame. If this fails the reentrancy guard was removed " +
                "(the dialogue-option crash is back). Offenders: " + string.Join(", ", offenders));
        }

        // SELF-TEST: the detector itself must FIRE on the two ORIGINAL unguarded shapes.
        // Proves the lint would have caught both historical bugs (synthetic source, no I/O).
        [Test]
        public void Detector_FiresOnTheUnguardedHistoricalShapes()
        {
            // (1) the original unguarded fork-bomb: Update -> HookDialogueGate -> the retry
            // coroutine re-calls the spawner; NO _retryingHook guard anywhere.
            const string unguardedForkBomb = @"
                class HeroLocomotionOld {
                    void Update() { HookDialogueGate(); }
                    void HookDialogueGate() {
                        var runner = Find();
                        if (runner == null) { StartCoroutine(RetryHook()); return; }
                    }
                    System.Collections.IEnumerator RetryHook() {
                        yield return null;
                        HookDialogueGate();
                    }
                }";
            var hits1 = FindOffendersInSource("HeroLocomotionOld.cs", unguardedForkBomb);
            Assert.IsNotEmpty(hits1,
                "Detector FAILED to flag the original unguarded fork-bomb (Update -> StartCoroutine, " +
                "no single-flight guard) — it would not have caught commit 69138fe.");

            // (2) the original unguarded OptionItem: Update -> Submit -> onSubmit re-enters,
            // ONLY a per-instance flag that resets each new instance (no GLOBAL guard token).
            const string unguardedSubmit = @"
                class OptionItemOld {
                    void Update() {
                        if (Selected) Submit();
                    }
                    void Submit() { onSubmit(); }
                }";
            var hits2 = FindOffendersInSource("OptionItemOld.cs", unguardedSubmit);
            Assert.IsNotEmpty(hits2,
                "Detector FAILED to flag the original unguarded re-entrant Update->Submit — " +
                "it would not have caught the 2026-06-12 dialogue-option crash.");
        }

        // ── scan engine ────────────────────────────────────────────────────────────

        private static List<string> ScanForOffenders()
        {
            var offenders = new List<string>();
            string projectRoot = ProjectRoot();

            foreach (var rel in ScanRootsRelativeToProject)
            {
                string root = Path.Combine(projectRoot, rel.Replace('/', Path.DirectorySeparatorChar));
                if (!Directory.Exists(root)) continue;   // vendored pkg may be absent on a lean clone
                foreach (var file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
                {
                    string fileName = Path.GetFileName(file);
                    string code = StripComments(File.ReadAllText(file));
                    // Re-read raw text only to detect the inline escape-hatch comment.
                    string raw = File.ReadAllText(file);
                    offenders.AddRange(FindOffenders(fileName, code, raw));
                }
            }
            return offenders;
        }

        // Convenience for the synthetic self-test: scan one in-memory source string.
        private static List<string> FindOffendersInSource(string fileName, string source)
        {
            string code = StripComments(source);
            return FindOffenders(fileName, code, source);
        }

        // Core rule application over ONE file's (comment-stripped) code.
        private static List<string> FindOffenders(string fileName, string code, string rawForComments)
        {
            var offenders = new List<string>();

            bool classHasGuard = ClassHasGuard(code);

            // Extract EVERY method (name -> body) so the self-spawn cycle (A2) can resolve
            // whether a coroutine's callee itself calls StartCoroutine.
            var methods = ExtractMethods(code);

            // Names of methods whose body calls StartCoroutine( — the "spawners".
            var spawnerNames = new HashSet<string>(
                methods.Where(m => m.body.Contains("StartCoroutine("))
                       .Select(m => m.name));

            foreach (var m in methods)
            {
                bool isUpdate = m.name == "Update" || m.name == "LateUpdate";
                bool isCoroutine = m.isEnumerator;
                if (!isUpdate && !isCoroutine) continue;

                // OPT-OUT: explicit reviewed escape hatch in the method's raw text.
                string rawSpan = ExtractRawNeighbourhood(rawForComments, m.name);
                if (rawSpan.Contains("// perf-lint-ok") || rawSpan.Contains("//perf-lint-ok"))
                    continue;

                string why = null;

                // Rule (A1): Update/LateUpdate directly spawns a coroutine.
                if (isUpdate && m.body.Contains("StartCoroutine("))
                    why = "Update/LateUpdate directly spawns StartCoroutine (fork-bomb shape)";

                // Rule (A2): a coroutine body calls a method that is itself a spawner —
                // the self-spawn cycle (coroutine re-invokes its own StartCoroutine path).
                if (why == null && isCoroutine)
                {
                    foreach (var callee in spawnerNames)
                    {
                        if (callee == m.name) continue; // a coroutine calling itself directly is rarer; cycle is via the spawner
                        if (Regex.IsMatch(m.body, @"\b" + Regex.Escape(callee) + @"\s*\("))
                        {
                            why = $"coroutine re-invokes its own spawner '{callee}' " +
                                  "(StartCoroutine self-spawn cycle — the 69138fe fork-bomb)";
                            break;
                        }
                    }
                }

                // Rule (B): Update/LateUpdate calls a re-entrant submit/dispatch verb.
                if (why == null && isUpdate)
                {
                    string verb = ReentrantCallVerbs.FirstOrDefault(v =>
                        Regex.IsMatch(m.body, @"\b" + Regex.Escape(v) + @"\s*\("));
                    if (verb != null)
                        why = $"Update/LateUpdate calls re-entrant '{verb}(' (sync re-entry shape)";
                }

                if (why == null) continue;

                // GUARD opt-out: a single-flight guard present in the class clears it.
                if (classHasGuard) continue;

                offenders.Add($"{fileName}:{m.name} — {why}; NO single-flight guard in class");
            }
            return offenders;
        }

        // True if the class shows ANY recognised single-flight / reentrancy guard.
        private static bool ClassHasGuard(string code)
        {
            // (1) a known guard field name appears.
            if (KnownGuardFieldTokens.Any(t => code.Contains(t))) return true;

            // (2) a generic single-flight bool: a bool field that is BOTH set true and
            //     gated by an early-out. We approximate: there is a `bool <name>` (or
            //     `static bool <name>`) field, that <name> is assigned `= true`, AND an
            //     `if (<name>) return;` / `if (!<name>) ... ` style early-out references it.
            foreach (Match m in Regex.Matches(code, @"\bbool\s+(?<n>[A-Za-z_]\w*)\b"))
            {
                string n = m.Groups["n"].Value;
                bool assignedTrue = Regex.IsMatch(code, @"\b" + Regex.Escape(n) + @"\s*=\s*true\b");
                bool gatedReturn = Regex.IsMatch(code, @"if\s*\(\s*!?\s*" + Regex.Escape(n) + @"\b");
                if (assignedTrue && gatedReturn) return true;
            }
            return false;
        }

        // Extracts every method as (name, brace-matched body, isEnumerator). A method is
        // recognised as  <returnType> Name( ... ) {  on (roughly) one signature line; the
        // body is captured by brace-matching from the opening '{'. isEnumerator is true
        // when the return type is IEnumerator (a coroutine). This is a heuristic parser
        // (good enough for a lint): it tolerates modifiers/attributes before the return
        // type because it anchors on  <word> Name(<args>) {  and reads the token before
        // the name as the return type.
        private static List<(string name, string body, bool isEnumerator)> ExtractMethods(string code)
        {
            var list = new List<(string, string, bool)>();
            // returnType (last word incl. generic/qualified) + name + parens + '{'
            // e.g.  "System.Collections.IEnumerator RetryHook()  {"
            //       "private void Update ()  {"
            foreach (Match sig in Regex.Matches(
                         code,
                         @"(?<ret>[A-Za-z_][\w\.<>,\s]*?)\s+(?<name>[A-Za-z_]\w*)\s*\([^;{}]*\)\s*(?:where[^\{]*)?\{"))
            {
                string ret = sig.Groups["ret"].Value.Trim();
                string name = sig.Groups["name"].Value;

                // Skip obvious non-methods (control keywords that look like calls).
                if (name == "if" || name == "for" || name == "foreach" || name == "while" ||
                    name == "switch" || name == "catch" || name == "using" || name == "lock" ||
                    name == "fixed") continue;

                int open = code.IndexOf('{', sig.Index + sig.Length - 1);
                if (open < 0) continue;
                int end = MatchBrace(code, open);
                if (end < 0) continue;
                string body = code.Substring(open, end - open + 1);

                bool isEnumerator =
                    ret.EndsWith("IEnumerator") || ret.EndsWith("IEnumerator>");

                list.Add((name, body, isEnumerator));
            }
            return list;
        }

        // Returns a slice of the raw (comment-bearing) source around an Update/LateUpdate
        // signature so the // perf-lint-ok escape hatch can be detected (the stripper
        // removed it from `code`). If the signature is not found verbatim, returns "".
        private static string ExtractRawNeighbourhood(string raw, string methodName)
        {
            var m = Regex.Match(raw, @"\b" + Regex.Escape(methodName) + @"\s*\(\s*\)\s*\{");
            if (!m.Success) return "";
            int start = m.Index;
            int len = Math.Min(2000, raw.Length - start);
            return raw.Substring(start, len);
        }

        // Returns the index of the '}' matching the '{' at openIndex, or -1.
        private static int MatchBrace(string s, int openIndex)
        {
            int depth = 0;
            for (int i = openIndex; i < s.Length; i++)
            {
                if (s[i] == '{') depth++;
                else if (s[i] == '}') { depth--; if (depth == 0) return i; }
            }
            return -1;
        }

        // Strips // line comments and /* */ block comments (string-literal-aware) so the
        // token scan sees live CODE only — mirrors RegressionSuite.StripComments so a
        // doc-comment mentioning StartCoroutine / Submit can never trip the lint.
        private static string StripComments(string src)
        {
            var sb = new StringBuilder(src.Length);
            bool inStr = false, inChar = false, inLine = false, inBlock = false, esc = false;
            for (int i = 0; i < src.Length; i++)
            {
                char c = src[i];
                char n = i + 1 < src.Length ? src[i + 1] : '\0';
                if (inLine) { if (c == '\n') { inLine = false; sb.Append(c); } continue; }
                if (inBlock) { if (c == '*' && n == '/') { inBlock = false; i++; } continue; }
                if (inStr)
                {
                    sb.Append(c);
                    if (esc) esc = false;
                    else if (c == '\\') esc = true;
                    else if (c == '"') inStr = false;
                    continue;
                }
                if (inChar)
                {
                    sb.Append(c);
                    if (esc) esc = false;
                    else if (c == '\\') esc = true;
                    else if (c == '\'') inChar = false;
                    continue;
                }
                if (c == '/' && n == '/') { inLine = true; i++; continue; }
                if (c == '/' && n == '*') { inBlock = true; i++; continue; }
                if (c == '"') { inStr = true; sb.Append(c); continue; }
                if (c == '\'') { inChar = true; sb.Append(c); continue; }
                sb.Append(c);
            }
            return sb.ToString();
        }

        // Resolves the project root from Application.dataPath (== <project>/Assets), the
        // robust pattern the sibling EditMode tests use.
        private static string ProjectRoot()
        {
            string assets = UnityEngine.Application.dataPath; // <project>/Assets
            return Directory.GetParent(assets)?.FullName ?? assets;
        }
    }
}
