// =============================================================================
// VfxLoopFlagAudit -- rewrites every VFX catalog row's IsLoop flag from the ONE
// thing that actually knows the answer: the prefab's emission module.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Editor   Namespace: DeNelle.Editor   (editor-only)
//
// THE DEFECT THIS REPAIRS:
//   IsLoop was never read off a prefab. It was a sticky manual checkbox in
//   VfxCasterWindow, force-set TRUE for the Projectile and Aura roles, and
//   whatever it happened to hold got written into the catalog row. 95 of the 135
//   HovlVfxCatalog rows ended up IsLoop:1 -- including PP_BigExplosion,
//   PP_SmallExplosion, PP_TinyExplosion, PP_EnergyExplosion, PP_DustExplosion,
//   PP_MuzzleFlash, PP_MetalImpacts, PP_FleshImpacts and PP_EarthShatter, every
//   one of them a rate-0 + burst-at-t0 prefab that self-terminates in under a
//   second. (PP_StoneImpacts and PP_ElectricalSparks were named alongside them but
//   the prefabs disagree: their root systems emit continuously at 5/sec and 50/sec
//   with looping on, so the derivation KEEPS them as loops. The prefab wins -- that
//   is the entire point of this tool.)
//
// WHY THAT IS EXPENSIVE:
//   VFXManager.Hovl.cs ~283-288 -- a loop row bumps _activeLoops, returns a
//   VFXHandle and registers NO reclaim deadline; only the oneshot branch
//   (~290-297) does. The one loop reclaim, PruneDestroyedFromSet
//   (VFXManager.cs ~973), frees loops whose HOST was DESTROYED, and pooled hosts
//   are never destroyed. So a fire-and-forget play of a loop-flagged burst row
//   burns one of the 20 slots (_maxActiveLoops, VFXManager.cs:142) for the whole
//   session. DefenseTower.CastKeyFor (~1099-1108) hands PP_MuzzleFlash to the
//   archer and ballista -- the two most common towers in any town -- and discards
//   the handle (~1065, ~1069). Six F8 captures caught the cap saturated:
//     capture-20260730-175552.md:55  PlayKey('ArcherTower_Projectile') SKIPPED -
//                                    active loops 20/20 (cap hit)
//     capture-20260730-175447.md:21  ARcaneTower_Projectile
//     capture-20260730-175729.md:54  ArcaneTower-Baselevel_Projectile
//     capture-20260716-205819.md:99  Poi_NodeAura
//     capture-20260716-210343.md:97  Poi_Landmark
//   Enemy.cs ~1680-1685 already spells this failure mode out in a comment. The
//   lesson was applied to one call site and never to the catalog.
//
// WHAT IT DOES:
//   1. Derives loop-vs-burst for every row of BOTH catalogs from the prefab
//      (VfxLoopFlagRegression.TryDerive -- the shared, documented rule).
//   2. Writes the corrected IsLoop back through SerializedObject.
//   3. Corrects Assets/Editor/VfxManualPicks.json to match, so a later
//      Defenders/VFX/Generate Hovl VFX Catalog cannot resurrect the wrong flag.
//      The JSON is patched IN PLACE, one token per line -- it is a tracked,
//      human-edited file and the diff must show only the flags that changed.
//   4. Reports (never edits) any generator Map entry whose isLoop: argument
//      disagrees, because VFXCatalogGenerator.Build() does
//      `entries.arraySize = rows.Count` and rebuilds VFXCatalog.asset wholesale
//      from its Map -- a correction to that asset is lost on the next Generate
//      unless the Map argument is right too. The Map files are the owner's.
//
//   A row whose prefab is MISSING (the art packs are gitignored -- a fresh clone
//   has none of them) or that carries no ParticleSystem is SKIPPED AND NAMED. A
//   missing prefab must never silently flip a flag.
//
//   IDEMPOTENT: a second run changes nothing and writes nothing.
//
// RUN:
//   Editor menu : Defenders/VFX/Audit Loop Flags
//   Batchmode   : DeNelle.Editor.VfxLoopFlagAudit.Run
//   Markers     : VFX_LOOPFLAG_OK on success, VFX_LOOPFLAG_FAIL on failure
//                 (the ok marker is withheld entirely on failure).
//
// The gate that keeps this fixed once fixed is VfxLoopFlagRegression
// [vfx-loop-flag], registered in DataRegression.RunAll.
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using DeNelle.Village;
using DeNelle.Editor.Regression;

namespace DeNelle.Editor
{
    public static class VfxLoopFlagAudit
    {
        private const string OkMarker   = "VFX_LOOPFLAG_OK";
        private const string FailMarker = "VFX_LOOPFLAG_FAIL";
        private const string Log        = "[VfxLoopFlagAudit] ";

        private const string ManualPicksPath   = "Assets/Editor/VfxManualPicks.json";
        private const string HovlGeneratorPath = "Assets/Editor/HovlVfxCatalogGenerator.cs";
        private const string TypedGeneratorPath = "Assets/Editor/VFXCatalogGenerator.cs";

        // A generator Map entry -- an initialiser line of the shape
        //     <brace> "Key", new Pick( ... ) <brace>
        // one per line in both generator files. The opening brace is deliberately NOT in
        // the pattern: a lone brace char inside a string literal unbalances the CLAUDE.md
        // rule-1 naive brace counter and the CompileGate scan (RegressionMarkerRegression
        // dodges the same trap with its OpenBrace/CloseBrace pair). `"Key", new Pick(` is
        // specific enough on its own. Parsed for REPORTING only; those files are never
        // edited here.
        private static readonly Regex MapEntry = new Regex(
            "\"(?<key>[^\"]+)\"\\s*,\\s*new\\s+Pick\\s*\\(", RegexOptions.Compiled);

        // `"isLoop": true` / `"isLoop": false` in the manual-picks JSON, with the
        // surrounding whitespace and the trailing comma left untouched by the rewrite.
        private static readonly Regex JsonIsLoop = new Regex(
            "(\"isLoop\"\\s*:\\s*)(true|false)", RegexOptions.Compiled);

        private static readonly Regex JsonKey = new Regex(
            "\"key\"\\s*:\\s*\"(?<key>[^\"]*)\"", RegexOptions.Compiled);

        [MenuItem("Defenders/VFX/Audit Loop Flags")]
        public static void Run()
        {
            string report;
            bool ok;
            try
            {
                ok = Execute(out report);
            }
            catch (Exception e)
            {
                Debug.LogError(Log + FailMarker + " threw " + e.GetType().Name + ": " + e.Message +
                               "\n" + e.StackTrace);
                return;
            }

            if (ok)
            {
                // Report first, marker last and exactly once -- the gate greps the token,
                // so it must never appear on a run that did not finish clean.
                Debug.Log(report);
                Debug.Log(OkMarker);
            }
            else
            {
                Debug.LogError(report + "\n" + FailMarker);
            }
        }

        // =====================================================================
        //  Body
        // =====================================================================
        private static bool Execute(out string report)
        {
            var sb = new StringBuilder();
            var problems = new List<string>();
            sb.AppendLine("--- VFX loop-flag audit: prefab emission is the authority, not the checkbox ---");

            int flips = 0, unchanged = 0, skipped = 0, distanceOnly = 0;
            // Rows going the OTHER way (false -> true) are the ones to read twice: a row
            // that BECOMES a loop starts consuming a slot that is only ever released by an
            // explicit VFXHandle.Stop(). Truthful per the prefab, but any caller that plays
            // it fire-and-forget now leaks exactly the way this audit is repairing.
            var newLoops = new List<string>();

            // key/type name -> derived truth, for the generator-Map cross-check below.
            var derivedByKey = new Dictionary<string, bool>(StringComparer.Ordinal);
            var derivedByType = new Dictionary<string, bool>(StringComparer.Ordinal);

            // ---------------------------------------------------------------
            //  (1) HovlVfxCatalog.asset -- the 135 string-keyed rows
            // ---------------------------------------------------------------
            var hovl = AssetDatabase.LoadAssetAtPath<HovlVfxCatalog>(VfxLoopFlagRegression.HovlCatalogPath);
            if (hovl == null)
            {
                problems.Add("HovlVfxCatalog.asset did not load from " + VfxLoopFlagRegression.HovlCatalogPath);
            }
            else
            {
                var rows = hovl.Rows ?? new HovlVfxCatalog.Row[0];
                var so = new SerializedObject(hovl);
                var arr = so.FindProperty("Rows");
                if (arr == null)
                {
                    problems.Add("HovlVfxCatalog has no serialized 'Rows' array property -- cannot write.");
                }
                else
                {
                    sb.AppendLine("HovlVfxCatalog: " + rows.Length + " row(s)");
                    for (int i = 0; i < rows.Length && i < arr.arraySize; i++)
                    {
                        var row = rows[i];
                        string key = string.IsNullOrEmpty(row.Key) ? ("<row " + i + ">") : row.Key;
                        bool derived;
                        string detail;
                        if (!VfxLoopFlagRegression.TryResolveExpected(key, row.Prefab, out derived, out detail))
                        {
                            skipped++;
                            sb.AppendLine("  SKIP " + key + ": " + detail + " -- flag left as stored (IsLoop " +
                                          (row.IsLoop ? "1" : "0") + ")");
                            continue;
                        }
                        derivedByKey[key] = derived;
                        if (derived && VfxLoopFlagRegression.QualifiesByDistanceOnly(row.Prefab)) distanceOnly++;

                        if (row.IsLoop == derived) { unchanged++; continue; }

                        var prop = arr.GetArrayElementAtIndex(i).FindPropertyRelative("IsLoop");
                        if (prop == null)
                        {
                            problems.Add("HovlVfxCatalog row " + i + " ('" + key + "') has no 'IsLoop' property.");
                            continue;
                        }
                        prop.boolValue = derived;
                        flips++;
                        if (derived) newLoops.Add("HovlVfxCatalog '" + key + "'");
                        sb.AppendLine("  " + key + ": IsLoop " + (row.IsLoop ? "1" : "0") + " -> " +
                                      (derived ? "1" : "0") + " (" + detail + ")");
                    }
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(hovl);
                }
            }

            // ---------------------------------------------------------------
            //  (2) VFXCatalog.asset -- the VFXType-keyed rows
            // ---------------------------------------------------------------
            // Types are named via ToString() only. Hardcoding an enum MEMBER here would
            // fail-compile the entire editor assembly the day it is renamed, taking the
            // compile gate down for every parallel lane.
            var typed = AssetDatabase.LoadAssetAtPath<VFXCatalog>(VfxLoopFlagRegression.TypedCatalogPath);
            if (typed == null)
            {
                problems.Add("VFXCatalog.asset did not load from " + VfxLoopFlagRegression.TypedCatalogPath);
            }
            else
            {
                var entries = typed.Entries ?? new VFXCatalog.Entry[0];
                var so = new SerializedObject(typed);
                var arr = so.FindProperty("Entries");
                if (arr == null)
                {
                    problems.Add("VFXCatalog has no serialized 'Entries' array property -- cannot write.");
                }
                else
                {
                    sb.AppendLine("VFXCatalog: " + entries.Length + " entry(ies)");
                    for (int i = 0; i < entries.Length && i < arr.arraySize; i++)
                    {
                        var e = entries[i];
                        string name = e.Type.ToString();
                        bool derived;
                        string detail;
                        if (!VfxLoopFlagRegression.TryResolveExpected(name, e.Prefab, out derived, out detail))
                        {
                            skipped++;
                            sb.AppendLine("  SKIP " + name + ": " + detail + " -- flag left as stored (IsLoop " +
                                          (e.IsLoop ? "1" : "0") + ")");
                            continue;
                        }
                        derivedByType[name] = derived;
                        if (derived && VfxLoopFlagRegression.QualifiesByDistanceOnly(e.Prefab)) distanceOnly++;

                        if (e.IsLoop == derived) { unchanged++; continue; }

                        var prop = arr.GetArrayElementAtIndex(i).FindPropertyRelative("IsLoop");
                        if (prop == null)
                        {
                            problems.Add("VFXCatalog entry " + i + " ('" + name + "') has no 'IsLoop' property.");
                            continue;
                        }
                        prop.boolValue = derived;
                        flips++;
                        if (derived) newLoops.Add("VFXCatalog '" + name + "'");
                        sb.AppendLine("  " + name + ": IsLoop " + (e.IsLoop ? "1" : "0") + " -> " +
                                      (derived ? "1" : "0") + " (" + detail + ")");
                    }
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(typed);
                }
            }

            if (flips > 0) AssetDatabase.SaveAssets();

            // ---------------------------------------------------------------
            //  (3) VfxManualPicks.json -- the overlay that would undo the fix
            // ---------------------------------------------------------------
            int jsonFlips = PatchManualPicks(sb, problems, ref skipped);

            // ---------------------------------------------------------------
            //  (4) Generator Map cross-check -- REPORT ONLY, never edit
            // ---------------------------------------------------------------
            var mapMismatches = new List<string>();
            ReportMapMismatches(HovlGeneratorPath, "HovlVfxCatalogGenerator.Map", derivedByKey, sb, mapMismatches);
            ReportMapMismatches(TypedGeneratorPath, "VFXCatalogGenerator.Map", derivedByType, sb, mapMismatches);

            // ---------------------------------------------------------------
            //  Verdict
            // ---------------------------------------------------------------
            sb.AppendLine("--- summary ---");
            sb.AppendLine("catalog flips: " + flips + "   already correct: " + unchanged +
                          "   skipped (no prefab / no ParticleSystem): " + skipped);
            sb.AppendLine("VfxManualPicks.json isLoop flips: " + jsonFlips);
            sb.AppendLine("rows that derive LOOP only via rateOverDistance (widened clause, see " +
                          "VfxLoopFlagRegression's combining rule): " + distanceOnly);
            sb.AppendLine("rows that BECAME loops (" + newLoops.Count + ") -- each one now holds a slot until " +
                          "someone calls VFXHandle.Stop(); check the call site before shipping:");
            foreach (var n in newLoops) sb.AppendLine("  NEW LOOP " + n);
            sb.AppendLine("generator Map entries whose isLoop argument disagrees with the prefab: " +
                          mapMismatches.Count + " (REPORTED ONLY -- the Map files are owner-owned; a " +
                          "VFXCatalog.asset correction is lost on the next Generate until the Map matches)");
            foreach (var m in mapMismatches) sb.AppendLine("  MAP " + m);

            if (problems.Count > 0)
            {
                sb.AppendLine("FAILURES (" + problems.Count + "):");
                foreach (var p in problems) sb.AppendLine("  " + p);
                report = Log + sb;
                return false;
            }

            report = Log + sb;
            return true;
        }

        // =====================================================================
        //  Manual-picks JSON: in-place, minimal-diff token rewrite
        // =====================================================================
        // Deliberately NOT a JsonUtility round trip. This file is tracked and
        // hand-edited; a round trip would rewrite every line's formatting and bury
        // the flag changes in noise. One regex substitution on the isLoop line of a
        // row that needs it, everything else byte-identical.
        private static int PatchManualPicks(StringBuilder sb, List<string> problems, ref int skipped)
        {
            string full = Path.Combine(Directory.GetParent(Application.dataPath).FullName,
                                       ManualPicksPath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(full))
            {
                sb.AppendLine("VfxManualPicks.json: absent -- nothing to correct.");
                return 0;
            }

            string original;
            try { original = File.ReadAllText(full); }
            catch (IOException e)
            {
                problems.Add("could not read " + ManualPicksPath + ": " + e.Message);
                return 0;
            }

            // Preserve the file's own line endings by splitting on '\n' and keeping any '\r'.
            string[] lines = original.Split('\n');
            string currentKey = null;
            string currentPath = null;
            int flips = 0;

            // Two passes per row are impossible in one forward scan because "prefabPath"
            // follows "key" and precedes "isLoop" in this schema -- which is exactly the
            // order this scan relies on. Assert that shape rather than assume it.
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];

                var km = JsonKey.Match(line);
                if (km.Success) { currentKey = km.Groups["key"].Value; currentPath = null; continue; }

                int pp = line.IndexOf("\"prefabPath\"", StringComparison.Ordinal);
                if (pp >= 0)
                {
                    int q1 = line.IndexOf('"', line.IndexOf(':', pp) + 1);
                    int q2 = q1 >= 0 ? line.IndexOf('"', q1 + 1) : -1;
                    if (q1 >= 0 && q2 > q1) currentPath = line.Substring(q1 + 1, q2 - q1 - 1);
                    continue;
                }

                var lm = JsonIsLoop.Match(line);
                if (!lm.Success) continue;
                if (string.IsNullOrEmpty(currentPath))
                {
                    problems.Add("VfxManualPicks.json row '" + (currentKey ?? "<unknown>") +
                                 "' has an isLoop with no prefabPath ahead of it -- schema changed, " +
                                 "the in-place patcher cannot be trusted; fix the parser before re-running.");
                    continue;
                }

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(currentPath);
                bool derived;
                string detail;
                if (!VfxLoopFlagRegression.TryResolveExpected(currentKey, prefab, out derived, out detail))
                {
                    skipped++;
                    sb.AppendLine("  SKIP json '" + (currentKey ?? "<unknown>") + "': " + detail +
                                  " (" + currentPath + ") -- left as authored");
                    continue;
                }

                bool stored = lm.Groups[2].Value == "true";
                if (stored == derived) continue;

                lines[i] = JsonIsLoop.Replace(line, "${1}" + (derived ? "true" : "false"), 1);
                flips++;
                sb.AppendLine("  json '" + (currentKey ?? "<unknown>") + "': isLoop " +
                              (stored ? "true" : "false") + " -> " + (derived ? "true" : "false") +
                              " (" + detail + ")");
            }

            if (flips == 0)
            {
                sb.AppendLine("VfxManualPicks.json: already correct (no write).");
                return 0;
            }

            string rewritten = string.Join("\n", lines);
            if (rewritten == original)
            {
                sb.AppendLine("VfxManualPicks.json: no byte change (no write).");
                return 0;
            }

            try
            {
                File.WriteAllText(full, rewritten, new UTF8Encoding(false));
                AssetDatabase.ImportAsset(ManualPicksPath);
                sb.AppendLine("VfxManualPicks.json: " + flips + " isLoop token(s) corrected in place.");
            }
            catch (IOException e)
            {
                problems.Add("could not write " + ManualPicksPath + ": " + e.Message);
                return 0;
            }
            return flips;
        }

        // =====================================================================
        //  Generator Map cross-check (report only)
        // =====================================================================
        // Both generators declare one Map entry per SOURCE LINE, so a line scan is
        // enough and cannot be confused by parentheses inside a prefab path. The
        // parsed-entry count is printed so a reformat of those files shows up as a
        // suspiciously low number rather than as silence.
        private static void ReportMapMismatches(
            string generatorAssetPath, string label,
            Dictionary<string, bool> derivedByKey, StringBuilder sb, List<string> mismatches)
        {
            string full = Path.Combine(Directory.GetParent(Application.dataPath).FullName,
                                       generatorAssetPath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(full))
            {
                sb.AppendLine(label + ": generator source not found at " + generatorAssetPath + " -- not cross-checked.");
                return;
            }

            string[] lines;
            try { lines = File.ReadAllText(full).Replace("\r\n", "\n").Split('\n'); }
            catch (IOException e)
            {
                sb.AppendLine(label + ": could not read generator source (" + e.Message + ") -- not cross-checked.");
                return;
            }

            int parsed = 0, compared = 0;
            foreach (var line in lines)
            {
                string trimmed = line.TrimStart();
                if (trimmed.StartsWith("//", StringComparison.Ordinal)) continue;
                var m = MapEntry.Match(line);
                if (!m.Success) continue;
                parsed++;

                string key = m.Groups["key"].Value;
                bool mapLoop = line.IndexOf("isLoop: true", StringComparison.Ordinal) >= 0;
                bool derived;
                if (!derivedByKey.TryGetValue(key, out derived)) continue;   // prefab absent -> nothing proven
                compared++;
                if (mapLoop == derived) continue;

                mismatches.Add(label + " '" + key + "' declares isLoop: " + (mapLoop ? "true" : "false") +
                               " but the prefab derives " + (derived ? "true" : "false") +
                               " -- the next Generate would restore the wrong flag.");
            }
            sb.AppendLine(label + ": " + parsed + " Map entry line(s) parsed, " + compared + " comparable against a live prefab.");
        }
    }
}
