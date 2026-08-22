// =============================================================================
// SourceLint — read a runtime .cs file as CODE ONLY, for oracles that pin CALL
// SITES rather than behaviour.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression   Namespace: DeNelle.Editor.Regression
//
// WHY SOURCE-LINT AT ALL: some invariants are about WHERE a call happens (this
// read must precede that write; this gate must precede that door), and no runtime
// assertion can see call ORDER. Those pins read the source.
//
// ⛔ WHY COMMENTS AND STRING LITERALS ARE STRIPPED — this is the whole point.
//   These files DOCUMENT the very symbols the pins look for. RaidClaimService's own
//   summary names IsClaimed and ScaleLootForClear; a FlowTrace message names the
//   method that emitted it. Matching raw text would let a COMMENT satisfy a pin that
//   no call site actually meets — a hollow pass wearing a green marker, which is the
//   worst possible outcome for a gate. Strip first, always.
//
// ⚠ PROVENANCE: this logic is lifted VERBATIM from
//   RaidRepeatClearRegression.StripCommentsAndLiterals / Body / ReadCode, where it
//   was private. It is factored out here because WO-728 needed the same three
//   helpers in a second oracle, and a third copy-paste is how the copies drift apart
//   (the duplicated-state failure CLAUDE.md §2/§5/§16 each record).
//   RaidRepeatClearRegression is DELIBERATELY LEFT UNCHANGED for now: it is a live
//   pre-ship gate, and re-pointing it is a structural refactor that must not be
//   smuggled into player-facing work (ARCHITECTURE_PRINCIPLES). Pointing it here is
//   a clean one-line follow-up whenever that file is next opened for its own reasons.
//
// PURE + static. No scene, no save, no Unity lifecycle. NEVER throws — a missing or
// unreadable file records a NAMED failure through the caller's list and returns
// empty, so the caller fails loudly instead of silently linting nothing.
// =============================================================================

using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace DeNelle.Editor.Regression
{
    /// <summary>Comment/literal-stripped source reading for call-site pins.</summary>
    public static class SourceLint
    {
        // Declared as a balanced PAIR on one line on purpose (RegressionMarkerRegression's
        // precedent): a lone brace char literal trips the CLAUDE.md rule-1 brace counter.
        private const char OpenBrace = '{', CloseBrace = '}';

        /// <summary>
        /// Reads a file under <c>Application.dataPath</c> as CODE ONLY (comments and string
        /// literal CONTENTS removed). A missing/unreadable file appends a named failure to
        /// <paramref name="fails"/> and returns <see cref="string.Empty"/>.
        /// </summary>
        public static string ReadCode(string relativeToAssets, List<string> fails)
        {
            string path = Path.Combine(Application.dataPath, relativeToAssets);
            if (!File.Exists(path))
            {
                if (fails != null) fails.Add("source file missing: " + relativeToAssets);
                return string.Empty;
            }
            try { return StripCommentsAndLiterals(File.ReadAllText(path)); }
            catch (IOException ex)
            {
                if (fails != null) fails.Add("could not read " + relativeToAssets + ": " + ex.Message);
                return string.Empty;
            }
        }

        /// <summary>
        /// The brace-matched body of the first method whose signature matches
        /// <paramref name="signaturePattern"/>, from its opening brace to the balanced close.
        /// Brace-matched rather than indentation-matched so a nested block cannot end the
        /// extraction early. Empty when the signature is not found.
        /// </summary>
        public static string Body(string code, string signaturePattern)
        {
            if (string.IsNullOrEmpty(code)) return string.Empty;
            var m = Regex.Match(code, signaturePattern);
            if (!m.Success) return string.Empty;
            int open = code.IndexOf(OpenBrace, m.Index + m.Length);
            if (open < 0) return string.Empty;
            int depth = 0;
            for (int i = open; i < code.Length; i++)
            {
                if (code[i] == OpenBrace) depth++;
                else if (code[i] == CloseBrace)
                {
                    depth--;
                    if (depth == 0) return code.Substring(open, i - open + 1);
                }
            }
            return string.Empty;
        }

        /// <summary>
        /// Strips <c>//</c> line comments, block comments AND the CONTENTS of char/verbatim/
        /// regular/interpolated string literals, preserving line structure well enough for
        /// ordering comparisons. See the file header for why literals go too.
        /// </summary>
        public static string StripCommentsAndLiterals(string src)
        {
            if (string.IsNullOrEmpty(src)) return string.Empty;
            var sb = new StringBuilder(src.Length);
            for (int i = 0; i < src.Length; i++)
            {
                char c = src[i];
                char n = i + 1 < src.Length ? src[i + 1] : '\0';

                // Line comment -> keep the newline only.
                if (c == '/' && n == '/')
                {
                    while (i < src.Length && src[i] != '\n') i++;
                    if (i < src.Length) sb.Append('\n');
                    continue;
                }
                // Block comment -> keep any newlines inside it.
                if (c == '/' && n == '*')
                {
                    i += 2;
                    while (i < src.Length && !(src[i] == '*' && i + 1 < src.Length && src[i + 1] == '/'))
                    {
                        if (src[i] == '\n') sb.Append('\n');
                        i++;
                    }
                    i++;   // land on the '/' (the for-loop's i++ steps past it)
                    continue;
                }
                // Char literal -> emptied.
                if (c == '\'')
                {
                    i++;
                    while (i < src.Length && src[i] != '\'')
                    {
                        if (src[i] == '\\') i++;
                        i++;
                    }
                    sb.Append("''");
                    continue;
                }
                // Verbatim string -> emptied ("" is an escaped quote inside one).
                if (c == '@' && n == '"')
                {
                    i += 2;
                    while (i < src.Length)
                    {
                        if (src[i] == '"')
                        {
                            if (i + 1 < src.Length && src[i + 1] == '"') { i += 2; continue; }
                            break;
                        }
                        if (src[i] == '\n') sb.Append('\n');
                        i++;
                    }
                    sb.Append("\"\"");
                    continue;
                }
                // Regular (and interpolated) string -> emptied. An interpolated hole may hold
                // a real call, but a hole is not a call SITE for these pins, and dropping it
                // is the conservative direction: a pin can only go red.
                if (c == '"')
                {
                    i++;
                    while (i < src.Length && src[i] != '"')
                    {
                        if (src[i] == '\\') i++;
                        i++;
                    }
                    sb.Append("\"\"");
                    continue;
                }
                sb.Append(c);
            }
            return sb.ToString();
        }
    }
}
