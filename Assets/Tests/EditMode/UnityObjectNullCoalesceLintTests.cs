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
    // UnityObjectNullCoalesceLintTests (EditMode) — SYSTEMATIC guard for the
    // "?? / ?. on a UnityEngine.Object" crash class.
    // -----------------------------------------------------------------------------
    // WHY THIS EXISTS — this project shipped a PARTIAL HUD this session because of
    // exactly this trap:
    //
    //     var cg = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
    //     cg.alpha = 0f;   // <-- MissingComponentException
    //
    // UnityEngine.Object overloads `==` so a destroyed/absent object compares EQUAL
    // to null — BUT the C# `??` and `?.` operators check for a REAL CLR null, which a
    // Unity "fake-null" object is NOT. So when GetComponent finds nothing it returns a
    // fake-null wrapper, `??` sees a non-null reference, the AddComponent fallback is
    // SKIPPED, `cg` holds the fake-null, and the very next member access throws.
    //
    // The whole codebase was just swept clean of this shape. This lint keeps it from
    // coming back: a re-introduced `GetComponent<T>() ??` goes RED.
    //
    // WHAT THIS LINT FLAGS (string/regex over comment-stripped source — deterministic,
    // fast, no scene load): a generic GetComponent / GetComponentInChildren /
    // GetComponentInParent call with an empty arg list, immediately followed by `??`.
    // That is the precise dangerous shape: the result of a UnityEngine.Object lookup
    // fed straight into the C# null-coalesce operator.
    //
    // OPT-OUT (a reviewed exception PASSES). A flagged line is ACCEPTED if it carries
    // an inline escape-hatch comment  // unity-null-ok  on the SAME source line.
    //
    // THE FIX (what the message tells the dev): use TryGetComponent(out var c) or an
    // explicit `== null` check (Unity's overloaded equality DOES treat fake-null as
    // null) — never `??` / `?.` on a UnityEngine.Object.
    //
    // KNOWN LIMITS (documented honestly):
    //   * Heuristic, not a type analysis. It keys on the GetComponent family (the
    //     overwhelming source of the trap), not on every method that returns a
    //     UnityEngine.Object. A `FindObjectOfType<T>() ?? fallback` would slip through;
    //     widen the regex if a real instance of that shape ever ships.
    //   * Comment-stripped scan, so a doc-comment mentioning `GetComponent<X>() ??`
    //     (e.g. the ActorAnimator header example) can never trip the lint.
    // =============================================================================
    public class UnityObjectNullCoalesceLintTests
    {
        // Roots scanned. Gameplay modules + the editor tooling we author. Library /
        // PackageCache copies are NOT scanned (read-only, not ours).
        private static readonly string[] ScanRootsRelativeToProject =
        {
            "Assets/_Modules",
            "Assets/Editor",
        };

        // The dangerous shape: GetComponent[InChildren|InParent]<T>()  immediately
        // followed by  ?? . Empty arg list (the generic GetComponent overload).
        private static readonly Regex DangerousNullCoalesce = new Regex(
            @"GetComponent(InChildren|InParent)?\s*<[^>]+>\s*\(\s*\)\s*\?\?",
            RegexOptions.Compiled);

        // ── tests ────────────────────────────────────────────────────────────────

        [Test]
        public void NoNullCoalesceOnGetComponent()
        {
            var offenders = ScanForOffenders();

            Assert.IsEmpty(offenders,
                "`??` / `?.` on a UnityEngine.Object lookup found (the partial-HUD crash class). " +
                "GetComponent<T>() returns a Unity FAKE-NULL when the component is absent; the C# " +
                "`??` operator does NOT treat fake-null as null, so the fallback never runs and the " +
                "next member access throws MissingComponentException. Use `TryGetComponent(out var c)` " +
                "or an explicit `== null` check (Unity overloads equality) — NEVER `??`/`?.` on a " +
                "UnityEngine.Object. Mark a reviewed exception with `// unity-null-ok` on the line.\n  " +
                string.Join("\n  ", offenders));
        }

        // SELF-TEST: the detector itself must FIRE on the exact shipped shape.
        // Proves the lint would have caught the partial-HUD bug (synthetic source, no I/O).
        [Test]
        public void Detector_FiresOnTheGetComponentNullCoalesceShape()
        {
            const string bad =
                "void Awake() { var c = go.GetComponent<CanvasGroup>() ?? go.AddComponent<CanvasGroup>(); c.alpha = 0f; }";

            var hits = FindOffendersInSource("BadHud.cs", bad);
            Assert.IsNotEmpty(hits,
                "Detector FAILED to flag `GetComponent<CanvasGroup>() ??` — it would not have caught " +
                "the partial-HUD fake-null crash that shipped this session.");
        }

        // SELF-TEST: the detector must NOT fire on safe / unrelated `??` uses.
        [Test]
        public void Detector_DoesNotFireOnSafePatterns()
        {
            // (1) GameObject.Find returns a real CLR null on miss -> `??` is correct here.
            const string safeFind =
                "void M() { var go = GameObject.Find(\"x\") ?? GameObject.Find(\"y\"); }";
            Assert.IsEmpty(FindOffendersInSource("SafeFind.cs", safeFind),
                "Detector wrongly flagged GameObject.Find(...) ?? — that is a true-null, safe `??`.");

            // (2) a nullable struct (Color?) — `??` is the canonical idiom, not a Unity Object.
            const string safeStruct =
                "void M(Color? maybe) { Color c = maybe ?? Color.white; }";
            Assert.IsEmpty(FindOffendersInSource("SafeStruct.cs", safeStruct),
                "Detector wrongly flagged a nullable-struct `??` (Color? ?? Color.white).");

            // (3) a plain string `??` — ordinary reference-type coalesce, nothing to do with Unity.
            const string safeString =
                "void M(string a) { string s = a ?? \"\"; }";
            Assert.IsEmpty(FindOffendersInSource("SafeString.cs", safeString),
                "Detector wrongly flagged a string `??` (a ?? \"\").");
        }

        // ── scan engine ────────────────────────────────────────────────────────────

        private static List<string> ScanForOffenders()
        {
            var offenders = new List<string>();
            string projectRoot = ProjectRoot();

            foreach (var rel in ScanRootsRelativeToProject)
            {
                string root = Path.Combine(projectRoot, rel.Replace('/', Path.DirectorySeparatorChar));
                if (!Directory.Exists(root)) continue;   // root may be absent on a lean clone
                foreach (var file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
                {
                    string fileName = Path.GetFileName(file);
                    string raw = File.ReadAllText(file);
                    string code = StripComments(raw);
                    offenders.AddRange(FindOffenders(fileName, code, raw));
                }
            }
            return offenders;
        }

        // Convenience for the synthetic self-tests: scan one in-memory source string.
        private static List<string> FindOffendersInSource(string fileName, string source)
        {
            string code = StripComments(source);
            return FindOffenders(fileName, code, source);
        }

        // Core rule application over ONE file's (comment-stripped) code. `rawForComments`
        // is the original text, used both for line numbers and to detect the inline
        // `// unity-null-ok` escape hatch on the offending line.
        private static List<string> FindOffenders(string fileName, string code, string rawForComments)
        {
            var offenders = new List<string>();
            string[] rawLines = rawForComments.Replace("\r\n", "\n").Split('\n');

            foreach (Match m in DangerousNullCoalesce.Matches(code))
            {
                // 1-based line number of the match within the comment-stripped code.
                int line = LineOf(code, m.Index);

                // Per-match escape hatch: a reviewed exception carries `// unity-null-ok`
                // on the SAME line in the RAW source.
                string rawLine = (line >= 1 && line <= rawLines.Length) ? rawLines[line - 1] : "";
                if (rawLine.Contains("// unity-null-ok") || rawLine.Contains("//unity-null-ok"))
                    continue;

                offenders.Add($"{fileName}:{line} — GetComponent<...>() ?? (Unity fake-null fed to " +
                              "C# null-coalesce; fallback never runs -> MissingComponentException). " +
                              "Use TryGetComponent / explicit `== null`.");
            }
            return offenders;
        }

        // 1-based line number of a character index within `s`.
        private static int LineOf(string s, int index)
        {
            int line = 1;
            int cap = Math.Min(index, s.Length);
            for (int i = 0; i < cap; i++)
                if (s[i] == '\n') line++;
            return line;
        }

        // Strips // line comments and /* */ block comments (string-literal-aware) so the
        // token scan sees live CODE only — mirrors PerFrameReentrancyLintTests.StripComments
        // so a doc-comment mentioning GetComponent<X>() ?? can never trip the lint.
        // CRUCIAL: replaces stripped spans with spaces / preserves newlines so character
        // offsets (and therefore reported line numbers) stay aligned with the raw source.
        private static string StripComments(string src)
        {
            var sb = new StringBuilder(src.Length);
            bool inStr = false, inChar = false, inLine = false, inBlock = false, esc = false;
            for (int i = 0; i < src.Length; i++)
            {
                char c = src[i];
                char n = i + 1 < src.Length ? src[i + 1] : '\0';
                if (inLine) { if (c == '\n') { inLine = false; sb.Append(c); } continue; }
                if (inBlock)
                {
                    if (c == '*' && n == '/') { inBlock = false; i++; }
                    else if (c == '\n') sb.Append(c); // keep newlines so line numbers hold
                    continue;
                }
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
