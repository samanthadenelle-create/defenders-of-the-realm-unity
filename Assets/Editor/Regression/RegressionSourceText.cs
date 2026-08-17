// =============================================================================
// RegressionSourceText  --  the ONE comment/string stripper for source-reading oracles
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression.  No markers, no Run(out string) entry point:
// this is a pure text helper, not a suite (so RegressionMarkerRegression's RULE 2
// registration scan and its RULE 4 hollow-pass ratchet correctly ignore it).
//
// WHY THIS EXISTS (2026-08-16 coverage audit).
// ~21 suites under Assets/Editor read C# SOURCE and then regex/IndexOf over the RAW
// text. Raw text includes // comments, /* */ comments and string literals -- so an
// oracle looking for a banned token matches its OWN prose, and a header paragraph
// that NAMES the thing it forbids reads as a violation of it. Four separate false
// alarms in one night came from exactly that. The two suites that DID strip each
// rolled their own, so behaviour differed per suite and no one place could be fixed.
//
// LENGTH-PRESERVING, AND THAT IS LOAD-BEARING.
// Every removed character is replaced by a SPACE; every newline survives in place.
// So `stripped[i]` addresses the same character as `src[i]`, and a match index or a
// line number computed on either text is valid on the other. That is what lets a
// suite match a regex against the ORIGINAL text and then confirm the hit is live
// code by testing the same span in the stripped copy (BuildMenuRealEconomy's
// IsLiveCode does exactly this) -- a stripper that DELETED characters would silently
// slide every index and turn that check into nonsense.
//
// DIRECTION MATTERS, and it is why this is offered rather than force-fitted:
//   * A BANNED-pattern check (fails when it FINDS something) can only get SAFER when
//     comments and strings are blanked -- fewer matches, and every removed match was
//     prose, not code.
//   * A REQUIRED-pattern check (fails when something is ABSENT) can go RED if the
//     thing it requires legitimately lives inside a string literal (a scene name, a
//     resource path, a PlayerPrefs key). For those use StripComments, which leaves
//     string bodies intact.
// Never re-point a REQUIRED check at the full stripper without first checking that
// its needle is not a quoted literal.
// =============================================================================

namespace DeNelle.Editor.Regression
{
    /// <summary>Shared source-text normalisation for oracles that scan C# by text.</summary>
    public static class RegressionSourceText
    {
        /// <summary>
        /// Blanks // and /* */ comment bodies AND the contents of string/char literals
        /// (quotes included), preserving length and newlines. Handles verbatim (@""),
        /// interpolated ($"") and $@""/@$"" forms plus backslash escapes.
        /// Use for BANNED-pattern scans.
        /// </summary>
        public static string StripCommentsAndStrings(string src)
        {
            return Strip(src, blankStringBodies: true);
        }

        /// <summary>
        /// Blanks comment bodies only; string literals are left intact. Preserves length
        /// and newlines. Use for REQUIRED-pattern scans whose needle may be quoted.
        /// </summary>
        public static string StripComments(string src)
        {
            return Strip(src, blankStringBodies: false);
        }

        // -------------------------------------------------------------------------
        //  One scanner, two modes. An explicit state walk rather than a regex on
        //  purpose: a regex over C# text cannot tell a // inside a string literal from
        //  a real comment, which is the whole bug class this helper exists to end.
        // -------------------------------------------------------------------------
        private static string Strip(string src, bool blankStringBodies)
        {
            if (string.IsNullOrEmpty(src)) return string.Empty;

            char[] outp = src.ToCharArray();
            int i = 0, n = src.Length;

            while (i < n)
            {
                char c = src[i];
                char next = i + 1 < n ? src[i + 1] : '\0';

                // ---- line comment -------------------------------------------------
                if (c == '/' && next == '/')
                {
                    while (i < n && src[i] != '\n') { outp[i] = ' '; i++; }
                    continue;
                }

                // ---- block comment ------------------------------------------------
                if (c == '/' && next == '*')
                {
                    outp[i] = ' '; outp[i + 1] = ' '; i += 2;
                    while (i + 1 < n && !(src[i] == '*' && src[i + 1] == '/'))
                    {
                        if (src[i] != '\n') outp[i] = ' ';
                        i++;
                    }
                    if (i + 1 < n) { outp[i] = ' '; outp[i + 1] = ' '; i += 2; }
                    else i = n;
                    continue;
                }

                // ---- verbatim string  @"..."  /  $@"..."  /  @$"..." ---------------
                int quote = -1;
                if (c == '@' && next == '"') quote = i + 1;
                else if (((c == '$' && next == '@') || (c == '@' && next == '$')) && i + 2 < n && src[i + 2] == '"') quote = i + 2;
                if (quote >= 0)
                {
                    for (int k = i; k <= quote; k++) if (blankStringBodies) outp[k] = ' ';
                    i = quote + 1;
                    // Inside a verbatim string "" is an escaped quote; a lone " ends it.
                    while (i < n)
                    {
                        if (src[i] == '"')
                        {
                            if (i + 1 < n && src[i + 1] == '"')
                            {
                                if (blankStringBodies) { outp[i] = ' '; outp[i + 1] = ' '; }
                                i += 2;
                                continue;
                            }
                            if (blankStringBodies) outp[i] = ' ';
                            i++;
                            break;
                        }
                        if (src[i] != '\n' && blankStringBodies) outp[i] = ' ';
                        i++;
                    }
                    continue;
                }

                // ---- regular string  "..."  /  $"..." ------------------------------
                if (c == '"' || (c == '$' && next == '"'))
                {
                    if (c == '$') { if (blankStringBodies) outp[i] = ' '; i++; }
                    if (blankStringBodies) outp[i] = ' ';
                    i++;
                    while (i < n)
                    {
                        if (src[i] == '\\' && i + 1 < n)
                        {
                            if (blankStringBodies) { outp[i] = ' '; if (src[i + 1] != '\n') outp[i + 1] = ' '; }
                            i += 2;
                            continue;
                        }
                        if (src[i] == '"') { if (blankStringBodies) outp[i] = ' '; i++; break; }
                        if (src[i] == '\n') { i++; break; }        // unterminated literal guard
                        if (blankStringBodies) outp[i] = ' ';
                        i++;
                    }
                    continue;
                }

                // ---- char literal  '.' ---------------------------------------------
                if (c == '\'')
                {
                    if (blankStringBodies) outp[i] = ' ';
                    i++;
                    while (i < n)
                    {
                        if (src[i] == '\\' && i + 1 < n)
                        {
                            if (blankStringBodies) { outp[i] = ' '; outp[i + 1] = ' '; }
                            i += 2;
                            continue;
                        }
                        if (src[i] == '\'') { if (blankStringBodies) outp[i] = ' '; i++; break; }
                        if (src[i] == '\n') { i++; break; }
                        if (blankStringBodies) outp[i] = ' ';
                        i++;
                    }
                    continue;
                }

                i++;
            }

            return new string(outp);
        }
    }
}
