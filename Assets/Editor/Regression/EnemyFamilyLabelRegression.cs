// =============================================================================
// EnemyFamilyLabelRegression [enemy-family-label]
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression.
// Markers: ENEMY_FAMILY_LABEL_OK / ENEMY_FAMILY_LABEL_FAIL.
//
// WO-1303. THE DEFECT IT GUARDS: EnemyAnimatorLateBinder.Arm prewarmed the enemy
// family by the CONTROLLER name instead of the MODEL name. EnemyContentWarmer.
// FamilyOf cuts at the first underscore, and a controller name has none, so the
// whole controller became the family and the code asked Addressables for
// 'enemyfam-skeletonhumanoid' / 'enemyfam-orchumanoid' / 'enemyfam-largehumanoid'.
// Those labels do not exist, DownloadDependenciesAsync threw InvalidKeyException,
// Guard.Try turned it into a red error, and the family pre-fetch that the 2026-08-20
// per-family ruling exists for silently never happened. Four F8 captures on
// 2026-09-02 (seq 4359 / 4369 / 4377 / 4639) - the last one two minutes AFTER the
// R2 push, which is what proved it was a bad KEY, not missing content.
//
//   CASE 1 [call-site]  EnemyAnimatorLateBinder must prewarm by the MODEL. Source
//     lint with comments and string literals blanked, so no prose in either file
//     can satisfy it: PrewarmFamily(ctrlName) must be ABSENT and
//     PrewarmFamily(modelName) PRESENT. This is the exact known-bad state, so the
//     case demonstrably can go red.
//
//   CASE 2 [warmer-guard]  EnemyContentWarmer.WarmFamily must REFUSE an undeclared
//     label rather than let the engine throw: IsDeclaredFamilyLabel must exist and
//     be consulted, and the label must be built from the FamilyLabelPrefix const
//     rather than a second hand-typed literal (one grammar, one place - the same
//     duplicated-state rule as CLAUDE.md sec.2 / sec.5).
//
//   CASE 3 [data]  Every family authored in enemies.json maps to a label that is
//     DECLARED in AddressableAssetSettings.asset. This is the content oracle: if
//     someone adds an enemy family to the catalog without the matching label, the
//     whole family loses its pre-fetch and reads as "enemies pop in late" with no
//     error anywhere. Deliberately asserted over the AUTHORED family field, never
//     over FamilyOf(model) - the two DISAGREE by design (model 'Skeleton_Warrior'
//     belongs to family 'hollow'), and pinning the derived token here would pin
//     the bug instead of the rule.
//
// Standalone: run-unity-method
//   -Method DeNelle.Editor.Regression.EnemyFamilyLabelRegression.RunAll
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace DeNelle.Editor.Regression
{
    public static class EnemyFamilyLabelRegression
    {
        private const string BinderRel =
            "Assets/_Modules/Village/Enemies/EnemyAnimatorLateBinder.cs";
        private const string WarmerRel =
            "Assets/_Modules/Core/Addressables/EnemyContentWarmer.cs";
        private const string SettingsRel =
            "Assets/AddressableAssetsData/AddressableAssetSettings.asset";
        private const string EnemiesJsonRel =
            "Assets/Resources/Data/Canonical/enemies.json";

        private const string LabelPrefix = "enemyfam-";

        public static void RunAll()
        {
            bool ok = Run(out string reason);
            if (ok) Debug.Log("ENEMY_FAMILY_LABEL_OK\n" + reason);
            else    Debug.LogError("ENEMY_FAMILY_LABEL_FAIL\n" + reason);
        }

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var notes    = new List<string>();

            DeNelle.Core.Diagnostics.Guard.Try("Regression", "enemy-family-label case 1",
                () => Case1_LateBinderPrewarmsByModel(failures, notes));
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "enemy-family-label case 2",
                () => Case2_WarmFamilyRefusesUndeclaredLabel(failures, notes));
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "enemy-family-label case 3",
                () => Case3_AuthoredFamiliesHaveDeclaredLabels(failures, notes));

            if (failures.Count == 0)
            {
                reason = string.Join("; ", notes);
                return true;
            }

            var sb = new StringBuilder();
            sb.Append(failures.Count).Append(" failure(s):");
            foreach (string f in failures) sb.Append("\n  - ").Append(f);
            if (notes.Count > 0) sb.Append("\n  (context: ").Append(string.Join("; ", notes)).Append(')');
            reason = sb.ToString();
            return false;
        }

        // =====================================================================
        //  CASE 1 — the late binder prewarms by MODEL, never by CONTROLLER
        // =====================================================================
        private static void Case1_LateBinderPrewarmsByModel(List<string> failures, List<string> notes)
        {
            string src = ReadStripped(BinderRel, failures);
            if (src == null) return;

            int before = failures.Count;

            if (src.Contains("PrewarmFamily(ctrlName)"))
            {
                failures.Add(BinderRel + " calls PrewarmFamily(ctrlName) - the WO-1303 defect. " +
                             "PrewarmFamily consumes a MODEL slug or a full Enemies/ address; a controller " +
                             "name has no underscore for FamilyOf to cut at, so the whole controller becomes " +
                             "the family and Addressables is asked for a label that does not exist.");
            }

            if (!src.Contains("PrewarmFamily(modelName)"))
            {
                failures.Add(BinderRel + " no longer calls PrewarmFamily(modelName) - the family pre-fetch " +
                             "is gone, so a late-bound enemy trickles its bundle in per address instead of " +
                             "one family fetch (owner ruling 2026-08-20).");
            }

            // The header records that WAITING on this seam deadlocked the game on 2026-08-20.
            if (src.Contains("WaitForCompletion"))
            {
                failures.Add(BinderRel + " contains a blocking WaitForCompletion. This seam is " +
                             "fire-and-forget by ruling: waiting here is what deadlocked the game on " +
                             "2026-08-20.");
            }

            if (failures.Count == before)
                notes.Add("[case1] late binder prewarms by model, not controller, and never blocks");
        }

        // =====================================================================
        //  CASE 2 — an undeclared label is a named refusal, not an exception
        // =====================================================================
        private static void Case2_WarmFamilyRefusesUndeclaredLabel(List<string> failures, List<string> notes)
        {
            string src = ReadStripped(WarmerRel, failures);
            if (src == null) return;

            int before = failures.Count;

            if (!src.Contains("public static bool IsDeclaredFamilyLabel"))
                failures.Add(WarmerRel + " has no IsDeclaredFamilyLabel - nothing can tell a caller's bad " +
                             "key apart from missing content before Addressables throws.");

            if (!src.Contains("if (!IsDeclaredFamilyLabel(family))"))
                failures.Add(WarmerRel + " WarmFamily no longer refuses an undeclared family label. Without " +
                             "that check DownloadDependenciesAsync throws InvalidKeyException into Guard.Try " +
                             "and a caller bug reads as an engine error, once per spawn.");

            if (!src.Contains("public const string FamilyLabelPrefix"))
                failures.Add(WarmerRel + " has no FamilyLabelPrefix const - the label grammar must be " +
                             "written down exactly once, or a second hand-typed copy drifts from it.");

            if (!src.Contains("LabelFor(family)"))
                failures.Add(WarmerRel + " WarmFamily no longer builds its label through LabelFor - a " +
                             "re-typed prefix is the duplicated state this const exists to remove.");

            if (failures.Count == before)
                notes.Add("[case2] WarmFamily refuses an undeclared label with a named warn and one label grammar");
        }

        // =====================================================================
        //  CASE 3 — every AUTHORED family has a declared label
        // =====================================================================
        private static void Case3_AuthoredFamiliesHaveDeclaredLabels(List<string> failures, List<string> notes)
        {
            string settings = ReadRaw(SettingsRel, failures);
            string enemies  = ReadRaw(EnemiesJsonRel, failures);
            if (settings == null || enemies == null) return;

            HashSet<string> declared = CollectDeclaredLabels(settings);
            if (declared.Count == 0)
            {
                failures.Add(SettingsRel + " declares NO '" + LabelPrefix + "*' labels at all. Every enemy " +
                             "family loses its pre-fetch and the whole per-family seam is inert.");
                return;
            }

            var rejected = new List<string>();
            HashSet<string> families = CollectAuthoredFamilies(enemies, rejected);
            if (rejected.Count > 0)
            {
                // Surfaced, never swallowed: a value shaped unlike a family token is either
                // documentation (fine, and named here) or malformed data (a real defect).
                notes.Add("[case3] " + rejected.Count + " non-token \"family\" value(s) skipped: " +
                          string.Join(" | ", rejected.ToArray()));
            }
            if (families.Count == 0)
            {
                failures.Add(EnemiesJsonRel + " yielded NO family values - the parse found nothing to " +
                             "assert, which is a hollow pass, not a clean one.");
                return;
            }

            var missing = new List<string>();
            foreach (string fam in families)
                if (!declared.Contains(LabelPrefix + fam)) missing.Add(fam);

            if (missing.Count > 0)
            {
                failures.Add("enemies.json authors famil(ies) [" + string.Join(", ", missing) + "] with no " +
                             "declared label in " + SettingsRel + " (declared: " +
                             string.Join(", ", ToSorted(declared)) + "). Those families get no bundle " +
                             "pre-fetch and their bodies pop in late with no error on screen. Fix by " +
                             "labelling the assets in the enemy groups - and note that ANY change under " +
                             "Assets/AddressableAssetsData re-hashes every bundle and mandates a fresh " +
                             "tools/r2-ship.ps1 push (CLAUDE.md sec.16).");
                return;
            }

            notes.Add("[case3] " + families.Count + " authored enemy famil(ies) all map to declared labels (" +
                      declared.Count + " declared)");
        }

        private static HashSet<string> CollectDeclaredLabels(string settingsYaml)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string[] lines = settingsYaml.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (!line.StartsWith("- ", StringComparison.Ordinal)) continue;
                string val = line.Substring(2).Trim();
                if (val.StartsWith(LabelPrefix, StringComparison.OrdinalIgnoreCase)) set.Add(val);
            }
            return set;
        }

        /// <summary>Pull every "family": "value" pair out of enemies.json. A token scan rather
        /// than a typed load, so this oracle does not depend on the runtime catalog being
        /// loadable in batchmode (that dependency is how a suite ends up passing hollow).</summary>
        // WO-1307 follow-up, 2026-09-02: this is a RAW TEXT scan, and on its first real run it
        // matched "family" inside the "_schemaNotes" DOCUMENTATION block at the top of
        // enemies.json - whose value is an English sentence describing what a family is. The suite
        // then reported that prose as an "authored family with no declared label" and went red
        // against perfectly good data. The catalog files in this repo carry underscore-prefixed
        // authoring metadata by convention (_schemaNotes, _authoringNotes, _smartComposition), so
        // a scanner that does not skip them is reading documentation as content.
        //
        // Two guards, deliberately BOTH kept:
        //   1. skip any "_"-prefixed metadata object outright - the convention-level fix;
        //   2. reject any value that cannot be a family TOKEN (whitespace / punctuation / absurd
        //      length) and RECORD the rejection in 'rejected' - never drop it silently, or a
        //      genuinely malformed family value would vanish into a clean pass.
        private static HashSet<string> CollectAuthoredFamilies(string json, List<string> rejected)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            const string Needle = "\"family\"";
            int at = 0;
            while (true)
            {
                int k = json.IndexOf(Needle, at, StringComparison.OrdinalIgnoreCase);
                if (k < 0) break;
                at = k + Needle.Length;

                int colon = json.IndexOf(':', at);
                if (colon < 0) break;
                int q1 = json.IndexOf('"', colon);
                if (q1 < 0) break;
                int q2 = json.IndexOf('"', q1 + 1);
                if (q2 < 0) break;

                string val = json.Substring(q1 + 1, q2 - q1 - 1).Trim();
                at = q2 + 1;
                if (val.Length == 0) continue;

                if (InUnderscoreMetadataBlock(json, k)) continue;

                if (!IsFamilyToken(val))
                {
                    rejected.Add(val.Length > 48 ? val.Substring(0, 48) + "..." : val);
                    continue;
                }
                set.Add(val.ToLowerInvariant());
            }
            return set;
        }

        // True when the "family" key at 'keyIndex' sits inside an object whose own key begins with
        // '_' (the repo's authoring-metadata convention). Walks back to the nearest enclosing '{'
        // and reads the key that opens it.
        private static bool InUnderscoreMetadataBlock(string json, int keyIndex)
        {
            int depth = 0;
            for (int i = keyIndex - 1; i >= 0; i--)
            {
                char c = json[i];
                if (c == '}') depth++;
                else if (c == '{')
                {
                    if (depth > 0) { depth--; continue; }
                    int q2 = json.LastIndexOf('"', i);
                    if (q2 <= 0) return false;
                    int q1 = json.LastIndexOf('"', q2 - 1);
                    if (q1 < 0) return false;
                    string owner = json.Substring(q1 + 1, q2 - q1 - 1);
                    return owner.StartsWith("_", StringComparison.Ordinal);
                }
            }
            return false;
        }

        // A family is a short bare token ('hollow', 'orc', 'troll'). Prose is not.
        private static bool IsFamilyToken(string val)
        {
            if (val.Length > 32) return false;
            foreach (char c in val)
                if (!char.IsLetterOrDigit(c) && c != '_' && c != '-') return false;
            return true;
        }

        private static List<string> ToSorted(HashSet<string> set)
        {
            var list = new List<string>(set);
            list.Sort(StringComparer.OrdinalIgnoreCase);
            return list;
        }

        // =====================================================================
        //  Source helpers
        // =====================================================================

        private static string ReadRaw(string rel, List<string> failures)
        {
            string full = Path.Combine(Directory.GetCurrentDirectory(), rel.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(full))
            {
                failures.Add("missing file: " + rel);
                return null;
            }
            return File.ReadAllText(full);
        }

        private static string ReadStripped(string rel, List<string> failures)
        {
            string raw = ReadRaw(rel, failures);
            return raw == null ? null : StripCommentsAndStrings(raw);
        }

        /// <summary>Blank comments and string literal CONTENT so no prose - in the file under
        /// test or in this one - can satisfy a lint. Structure (quotes, newlines) is kept so
        /// the result still reads as code.</summary>
        private static string StripCommentsAndStrings(string raw)
        {
            var sb = new StringBuilder(raw.Length);
            bool inLine = false, inBlock = false, inStr = false, inChar = false, inVerbatim = false;
            for (int i = 0; i < raw.Length; i++)
            {
                char c = raw[i];
                char n = i + 1 < raw.Length ? raw[i + 1] : '\0';

                if (inLine) { if (c == '\n') { inLine = false; sb.Append(c); } continue; }
                if (inBlock) { if (c == '*' && n == '/') { inBlock = false; i++; } else if (c == '\n') sb.Append(c); continue; }
                if (inVerbatim)
                {
                    if (c == '"' && n == '"') { i++; continue; }
                    if (c == '"') { inVerbatim = false; sb.Append('"'); }
                    else if (c == '\n') sb.Append(c);
                    continue;
                }
                if (inStr)
                {
                    if (c == '\\' && n != '\0') { i++; continue; }
                    if (c == '"') { inStr = false; sb.Append('"'); }
                    continue;
                }
                if (inChar)
                {
                    if (c == '\\' && n != '\0') { i++; continue; }
                    if (c == '\'') { inChar = false; sb.Append('\''); }
                    continue;
                }

                if (c == '/' && n == '/') { inLine = true; i++; continue; }
                if (c == '/' && n == '*') { inBlock = true; i++; continue; }
                if (c == '@' && n == '"') { inVerbatim = true; sb.Append('"'); i++; continue; }
                if (c == '$' && n == '"') { inStr = true; sb.Append('"'); i++; continue; }
                if (c == '"') { inStr = true; sb.Append('"'); continue; }
                if (c == '\'') { inChar = true; sb.Append('\''); continue; }
                sb.Append(c);
            }
            return sb.ToString();
        }
    }
}
