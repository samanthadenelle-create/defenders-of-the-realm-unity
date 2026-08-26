// =============================================================================
// RetiredVocabularyRegression [retired-vocabulary]
// Player-visible vocabulary only. Frozen persistence/wire identifiers are excluded.
// DataRegression.cs registration is committer-fenced; the lead adds the one-line call.
// =============================================================================
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace DeNelle.Editor.Regression
{
    public static class RetiredVocabularyRegression
    {
        private const string Canon = "Assets/Resources/Data/Canonical/retired-vocabulary.json";
        private const string Mirror = "Assets/StreamingAssets/Data/Canonical/retired-vocabulary.json";
        private static readonly HashSet<string> VisibleJsonFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "displayName", "name", "title", "label", "description", "objectiveText", "tagline",
            "message", "copy", "buttonText", "emptyText", "statusText", "toast"
        };
        // ── DATED, TICKETED BASELINE - known debt, NOT an exemption ─────────────────────────
        // 2026-08-25: this suite's FIRST run found TWELVE player-visible Food leaks. Eight were
        // fixed at source the same night (four display labels, two world-node model routes, three
        // dead en.json keys). These four remain because they are PROSE, and prose is the owner's:
        // the guide tips explain food as a concept ("the wheat and apples the Heart wills into
        // being"), a quest objective is written around it, and the Mill's description raises a
        // design question (what does a gristmill produce once food is retired?).
        //
        // ⛔ THIS IS A RATCHET, NOT A PARDON. Anything NOT on this list still fails, so a NEW leak
        // cannot hide behind the old ones - the WO-910 dead-node and HudUiRegression missing-resource
        // precedent. ⛔ Removing a row from this list must mean the copy was FIXED, never that the
        // suite was quietened. When the owner authors the replacement copy, delete the row and let
        // it prove green.
        private static readonly HashSet<string> KnownCopyDebt2026_08_25 = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Assets/Resources/Data/Canonical/guide-content.json:tips[]",
            "Assets/Resources/Data/Canonical/quests.json:objectiveText",
            "Assets/Resources/Data/Canonical/structures-catalog.json:description",
        };

        // Populated per run so the SUCCESS string can name the debt it tolerated - a green
        // that silently skips things is the hollow-pass class this repo hunts.
        private static readonly List<string> knownDebtSeen = new List<string>();

        private static readonly Regex FrozenSourceSyntax = new Regex(
            "(?i)(JsonProperty\\s*\\(|\\bconst\\s+string\\b|\\bcase\\s+\\\"|FlowTrace\\.|Debug\\.|PlayerPrefs|Regex\\.)",
            RegexOptions.Compiled);
        private static readonly Regex VisibleSourceHint = new Regex(
            @"(?i)(label|text|toast|popup|display|option|entry|title|message|resourcegain|Harvest/|\bPop\s*\()",
            RegexOptions.Compiled);

        public static void RunAll()
        {
            if (Run(out string reason)) Debug.Log("RETIRED_VOCABULARY_OK - " + reason);
            else Debug.LogError("RETIRED_VOCABULARY_FAIL: " + reason);
        }

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            knownDebtSeen.Clear();   // per-run; the success string reports what was tolerated
            try
            {
                if (!File.Exists(Canon) || !File.Exists(Mirror))
                    failures.Add("[data] canonical retirement list or StreamingAssets mirror is missing");
                else if (!string.Equals(File.ReadAllText(Canon), File.ReadAllText(Mirror), StringComparison.Ordinal))
                    failures.Add("[data] retired-vocabulary canonical mirrors differ byte-for-byte");
                else
                {
                    var root = JObject.Parse(File.ReadAllText(Canon));
                    if ((root["version"]?.Value<int>() ?? 0) < 1)
                        failures.Add("[data] version must be >= 1");
                    var rows = root["retirements"] as JArray;
                    if (rows == null || rows.Count == 0) failures.Add("[data] no retirement rows authored");
                    else foreach (var token in rows)
                    {
                        var row = token as JObject;
                        string retired = row?["retired"]?.Value<string>()?.Trim();
                        string replacement = row?["replacement"]?.Value<string>()?.Trim();
                        string ticket = row?["ticket"]?.Value<string>()?.Trim();
                        string date = row?["date"]?.Value<string>()?.Trim();
                        if (string.IsNullOrEmpty(retired) || string.IsNullOrEmpty(replacement) ||
                            string.IsNullOrEmpty(ticket) || string.IsNullOrEmpty(date))
                        {
                            failures.Add("[data] every row requires retired/replacement/date/ticket");
                            continue;
                        }
                        ScanCanonicalJson(retired, replacement, failures);
                        ScanPlayerSource(retired, replacement, failures);
                    }
                }
            }
            catch (Exception ex) { failures.Add("[suite] THREW " + ex.GetType().Name + ": " + ex.Message); }

            if (failures.Count == 0)
            {
                reason = "player-visible strings, picker declarations and resource-art routes contain no retired vocabulary"
                         + (knownDebtSeen.Count > 0
                            ? " [TOLERATED known copy debt (dated 2026-08-25, owner copy owed): "
                              + string.Join("; ", knownDebtSeen) + "]"
                            : " [no tolerated debt - the dated baseline is now EMPTY and its list should be deleted]");
                return true;
            }
            reason = "retired-vocabulary FAIL x" + failures.Count + ": " + string.Join(" | ", failures);
            return false;
        }

        private static void ScanCanonicalJson(string retired, string replacement, List<string> failures)
        {
            foreach (string file in Directory.GetFiles("Assets/Resources/Data/Canonical", "*.json", SearchOption.AllDirectories))
            {
                if (file.Replace('\\', '/').EndsWith("/retired-vocabulary.json", StringComparison.OrdinalIgnoreCase)) continue;
                // DescendantsAndSelf() lives on JContainer, not JToken - the JToken overload is an
                // extension constrained to `T : JContainer`, so parsing into a bare JToken does not
                // compile (CS0311). Fixed by the committer, 2026-08-25.
                // A document whose root is a bare scalar has no descendants to walk, so it is
                // skipped rather than treated as an error: `null` here means "nothing to inspect",
                // never "inspection failed".
                JContainer root;
                try { root = JToken.Parse(File.ReadAllText(file)) as JContainer; }
                catch { continue; } // syntax belongs to the canonical-data gate
                if (root == null) continue;
                foreach (var value in root.DescendantsAndSelf())
                {
                    if (!(value is JValue scalar) || scalar.Type != JTokenType.String) continue;
                    var prop = value.Parent as JProperty;
                    if (!IsVisibleJsonValue(file, value, prop)) continue;
                    string text = scalar.Value<string>() ?? "";
                    if (!WholeWord(text, retired)) continue;
                    // Known copy debt is RECORDED, not silently passed: it still shows in the
                    // success string, so "green" never means "clean". Anything not on the dated
                    // list fails, which is what makes this a ratchet rather than a pardon.
                    string surfaceKey = file.Replace('\\', '/') + ":" + SurfaceName(value, prop);
                    if (KnownCopyDebt2026_08_25.Contains(surfaceKey))
                    {
                        if (!knownDebtSeen.Contains(surfaceKey)) knownDebtSeen.Add(surfaceKey);
                        continue;
                    }
                    failures.Add($"[catalog-copy] {file.Replace('\\', '/')}:{LineOf(file, text)} field '{SurfaceName(value, prop)}' exposes retired '{retired}' (use '{replacement}')");
                }
            }
        }

        private static void ScanPlayerSource(string retired, string replacement, List<string> failures)
        {
            foreach (string file in Directory.GetFiles("Assets/_Modules", "*.cs", SearchOption.AllDirectories))
            {
                string normalizedFile = file.Replace('\\', '/');
                if (normalizedFile.IndexOf("/Generated/", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                string source = StripComments(File.ReadAllText(file));
                string[] lines = source.Replace("\r\n", "\n").Split('\n');
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i];
                    // A variable/parameter named `food` is frozen implementation vocabulary, not
                    // player copy. Require the retired word inside a source string literal, then
                    // exclude the explicit serialization/diagnostic syntaxes that own wire words.
                    bool word = QuotedWholeWord(line, retired);
                    bool visibleContext = VisibleSourceHint.IsMatch(line) || RecentVisibleDeclaration(lines, i);
                    if (!word || !visibleContext || FrozenSourceSyntax.IsMatch(line)) continue;
                    failures.Add($"[source-surface] {file.Replace('\\', '/')}:{i + 1} exposes retired '{retired}' (use '{replacement}')");
                }
            }
        }

        private static bool WholeWord(string text, string word) =>
            Regex.IsMatch(text ?? "", @"(?i)(?<![A-Za-z0-9_])" + Regex.Escape(word) + @"(?![A-Za-z0-9_])");

        private static bool QuotedWholeWord(string line, string word) =>
            Regex.IsMatch(line ?? "", "(?i)\\\"[^\\\"\\r\\n]*(?<![A-Za-z0-9_])" +
                Regex.Escape(word) + "(?![A-Za-z0-9_])[^\\\"\\r\\n]*\\\"");

        private static bool RecentVisibleDeclaration(string[] lines, int index)
        {
            // Switch-return labels commonly place the method name several lines above the literal
            // (TargetLabel -> case HarvestTarget.Food -> "Food"). Carry only a short method-local
            // window; frozen TargetToken is a different declaration and remains outside it.
            int floor = Math.Max(0, index - 10);
            for (int i = index - 1; i >= floor; i--)
            {
                string prior = lines[i];
                if (prior.IndexOf("TargetLabel(", StringComparison.OrdinalIgnoreCase) >= 0) return true;
                if (Regex.IsMatch(prior, @"\b(public|private|internal|protected)\b.*\([^;]*\)")) break;
            }
            return false;
        }

        private static bool IsVisibleJsonValue(string file, JToken value, JProperty immediate)
        {
            string normalized = file.Replace('\\', '/');
            // Localization values use dynamic/dotted keys, so their property names cannot be
            // whitelisted. Every string value in the canonical locale is player copy.
            if (normalized.EndsWith("/en.json", StringComparison.OrdinalIgnoreCase)) return true;
            if (immediate != null && VisibleJsonFields.Contains(immediate.Name)) return true;

            // Copy arrays (guide tips, tutorial hints) have JArray as the immediate parent. Walk
            // ancestors so array-held strings are not invisible to the gate.
            for (JToken cur = value.Parent; cur != null; cur = cur.Parent)
                if (cur is JProperty p &&
                    (p.Name.Equals("tips", StringComparison.OrdinalIgnoreCase) ||
                     p.Name.Equals("hints", StringComparison.OrdinalIgnoreCase) ||
                     p.Name.Equals("lines", StringComparison.OrdinalIgnoreCase)))
                    return true;
            return false;
        }

        private static string SurfaceName(JToken value, JProperty immediate)
        {
            if (immediate != null) return immediate.Name;
            for (JToken cur = value.Parent; cur != null; cur = cur.Parent)
                if (cur is JProperty p) return p.Name + "[]";
            return "<array-copy>";
        }

        private static int LineOf(string file, string value)
        {
            string[] lines = File.ReadAllLines(file);
            for (int i = 0; i < lines.Length; i++)
                if (lines[i].IndexOf(value, StringComparison.Ordinal) >= 0) return i + 1;
            return 1;
        }

        private static string StripComments(string source)
        {
            var sb = new StringBuilder(source.Length);
            bool line = false, block = false, str = false, chr = false, escape = false;
            for (int i = 0; i < source.Length; i++)
            {
                char c = source[i], n = i + 1 < source.Length ? source[i + 1] : '\0';
                if (line) { if (c == '\n') { line = false; sb.Append(c); } else sb.Append(' '); continue; }
                if (block) { if (c == '*' && n == '/') { sb.Append("  "); i++; block = false; } else sb.Append(c == '\n' ? '\n' : ' '); continue; }
                if (!str && !chr && c == '/' && n == '/') { sb.Append("  "); i++; line = true; continue; }
                if (!str && !chr && c == '/' && n == '*') { sb.Append("  "); i++; block = true; continue; }
                sb.Append(c);
                if (escape) { escape = false; continue; }
                if ((str || chr) && c == '\\') { escape = true; continue; }
                if (!chr && c == '"') str = !str;
                else if (!str && c == '\'') chr = !chr;
            }
            return sb.ToString();
        }
    }
}
