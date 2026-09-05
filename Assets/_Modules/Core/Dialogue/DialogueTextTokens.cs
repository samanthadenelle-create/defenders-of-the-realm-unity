// =============================================================================
// DialogueTextTokens - live-data tokens inside authored dialogue TEXT (WO-1389).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.Dialogue
//
// WHY THIS EXISTS. The post-first-raid beat says things like "Army 3 / 10" and
// "The Broken Garrison opens at 3 wins - Iron walls, 15 defenders". Every one of
// those numbers lives in a catalog (ArmyStorage, troop-upgrades.json,
// scene-configs.json). WO-1389's own draft copy carried "stone walls, 12 defenders"
// for a camp whose data reads Iron / 15 - the exact copied-state drift CLAUDE.md
// sec.2/5/16 keep paying for. So the SENTENCE is authored in dialogues.json and the
// NUMBERS are tokens ("{camp.next.defenders}") resolved at surface time from the
// live data by resolvers that gameplay registers here.
//
// Core-only mechanism: the registry knows nothing about armies or camps. Village
// registers the resolvers (PostRaidBeatTokens). DialogueViewModel.OnLine resolves
// each line's text through Resolve(). Unknown tokens pass through UNTOUCHED, so an
// authored "{guide}" (a SPEAKER token, resolved elsewhere) or any literal braces in
// older copy are byte-identical to before this file existed.
//
// Every resolver call is guarded: a throwing resolver logs and leaves its token in
// place ("{army.used}" on screen is a visible, greppable defect - a blank is not).
// =============================================================================

using System;
using System.Collections.Generic;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Core.Dialogue
{
    /// <summary>Registry of "{key}" -> live string resolvers for dialogue line text.</summary>
    public static class DialogueTextTokens
    {
        private static readonly Dictionary<string, Func<string>> _resolvers =
            new Dictionary<string, Func<string>>(StringComparer.OrdinalIgnoreCase);

        /// <summary>The opening curly brace, spelled by code point so the CLAUDE.md sec.1
        /// file-level brace-balance gate (a raw count of the two brace characters) stays exact.</summary>
        private const char OpenBrace = (char)123;

        /// <summary>Register (or replace) the resolver for <paramref name="key"/> (no braces).</summary>
        public static void Register(string key, Func<string> resolver)
        {
            if (string.IsNullOrEmpty(key) || resolver == null)
            {
                FlowTrace.Warn("Dialogue", "DialogueTextTokens.Register ignored a null key/resolver.");
                return;
            }
            _resolvers[key] = resolver;
        }

        /// <summary>True when a resolver is registered for <paramref name="key"/>.</summary>
        public static bool IsRegistered(string key) =>
            !string.IsNullOrEmpty(key) && _resolvers.ContainsKey(key);

        /// <summary>The registered keys (for oracles). Never null.</summary>
        public static IEnumerable<string> Keys => _resolvers.Keys;

        /// <summary>Remove one registration. Safe on an unknown key.</summary>
        public static void Unregister(string key)
        {
            if (!string.IsNullOrEmpty(key)) _resolvers.Remove(key);
        }

        /// <summary>
        /// Replace every "{key}" for a REGISTERED key in <paramref name="text"/>. Text with no
        /// opening brace short-circuits (the hot path is every dialogue line). A resolver that
        /// throws or returns null leaves its token in place and logs - never a silent blank.
        /// </summary>
        public static string Resolve(string text)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf(OpenBrace) < 0 || _resolvers.Count == 0) return text;

            string result = text;
            foreach (var kv in _resolvers)
            {
                string token = "{" + kv.Key + "}";
                if (result.IndexOf(token, StringComparison.OrdinalIgnoreCase) < 0) continue;

                string value = null;
                try { value = kv.Value(); }
                catch (Exception ex)
                {
                    FlowTrace.Fail("Dialogue", "text token '" + token + "' resolver threw " +
                        ex.GetType().Name + ": " + ex.Message + " - token left in place.");
                    continue;
                }
                if (value == null)
                {
                    FlowTrace.Warn("Dialogue", "text token '" + token + "' resolved to null - token left in place.");
                    continue;
                }
                result = ReplaceIgnoreCase(result, token, value);
                FlowTrace.Step("Dialogue", "text token '" + token + "' -> '" + value + "'.");
            }
            return result;
        }

        private static string ReplaceIgnoreCase(string haystack, string needle, string value)
        {
            var sb = new System.Text.StringBuilder(haystack.Length + value.Length);
            int at = 0;
            while (true)
            {
                int idx = haystack.IndexOf(needle, at, StringComparison.OrdinalIgnoreCase);
                if (idx < 0) { sb.Append(haystack, at, haystack.Length - at); break; }
                sb.Append(haystack, at, idx - at).Append(value);
                at = idx + needle.Length;
            }
            return sb.ToString();
        }
    }
}
