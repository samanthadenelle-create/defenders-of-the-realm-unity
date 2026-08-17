// =============================================================================
// CoreDataHubRegression — the canonical-data-hub sync + read contract.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression (references DeNelle.Core). Headless, no-scene,
// no-PlayMode. Proves the CanonicalJson dual-copy invariant the WHOLE catalog layer
// rests on (see CanonicalJson.cs header):
//   • Every canonical catalog JSON READS NON-EMPTY through the REAL game path
//     (DeNelle.Core.CanonicalJson.Read — Resources dual-copy first, StreamingAssets
//     fallback), so a WebGL build can't come up with an empty catalog.
//   • Every StreamingAssets canonical source has its Resources DUAL-COPY present
//     (the WebGL-safe mirror). A StreamingAssets catalog with NO Resources copy is
//     exactly the "loads but combat won't play" bug the dual-copy guards against.
//
// SCOPE: the runtime catalog set = top-level *.json under StreamingAssets/Data/Canonical,
// EXCLUDING the monetization/editor-only files that are read on a DIFFERENT path
// (skr_*, battle_* — PackCatalog / covenant gate read those straight from
// StreamingAssets, not via the Resources dual-copy) and the *.sample.json fixtures.
// This keeps the oracle TRUTHFUL: it only asserts the dual-copy invariant for files
// that actually depend on it.
//
// Wire into the suite from DataRegression.RunAll (one line):
//   if (!CoreDataHubRegression.Run(out var coreDataReason)) failures.Add(coreDataReason); else log.AppendLine("[core-datahub] " + coreDataReason);
// =============================================================================
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using DeNelle.Core;

namespace DeNelle.Editor
{
    public static class CoreDataHubRegression
    {
        // Files read on a NON-dual-copy path (StreamingAssets-direct) — excluded from the
        // "must have a Resources copy" assertion so this oracle stays free of false fails.
        private static bool IsExcluded(string fileName)
        {
            var n = fileName.ToLowerInvariant();
            if (n.EndsWith(".sample.json")) return true;
            if (n.StartsWith("skr_")) return true;
            if (n.StartsWith("battle_")) return true;
            return false;
        }

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var log = new StringBuilder();
            log.AppendLine("--- CORE DATA HUB (CanonicalJson dual-copy + non-empty read) ---");

            // The canonical source-of-truth dir (StreamingAssets) + its WebGL-safe mirror (Resources).
            string streamingDir = Path.Combine(Application.streamingAssetsPath, "Data/Canonical");
            string resourcesDir = Path.Combine(Application.dataPath, "Resources/Data/Canonical");

            if (!Directory.Exists(streamingDir))
            {
                reason = "core-datahub: StreamingAssets/Data/Canonical does not exist (" + streamingDir + ")";
                Debug.LogError(log.ToString() + "CORE_DATAHUB_FAIL: " + reason);
                return false;
            }

            string[] files = Directory.GetFiles(streamingDir, "*.json", SearchOption.TopDirectoryOnly);
            int checkedCount = 0, excludedCount = 0;

            foreach (var path in files)
            {
                string fileName = Path.GetFileName(path);
                if (IsExcluded(fileName)) { excludedCount++; continue; }
                checkedCount++;

                string relative = "Data/Canonical/" + fileName;

                // (1) Reads NON-EMPTY through the REAL game path (Resources-first).
                string text = null;
                try { text = CanonicalJson.Read(relative); }
                catch (System.Exception ex)
                {
                    failures.Add($"CanonicalJson.Read('{relative}') THREW {ex.GetType().Name}: {ex.Message}");
                    continue;
                }
                if (string.IsNullOrEmpty(text))
                    failures.Add($"'{relative}' read EMPTY through CanonicalJson (WebGL would see an empty catalog)");

                // (2) The Resources dual-copy must exist (WebGL-safe mirror of the StreamingAssets source).
                string resCopy = Path.Combine(resourcesDir, fileName);
                if (!File.Exists(resCopy))
                    failures.Add($"'{fileName}' has a StreamingAssets source but NO Resources dual-copy " +
                                 $"({resCopy}) — it will load EMPTY in WebGL (Resources.Load miss)");
            }

            log.AppendLine($"checked {checkedCount} canonical file(s), excluded {excludedCount} (skr_*/battle_*/*.sample.json).");

            // ZERO-GUARD (Shape B, 2026-08-16 coverage audit). Every assertion in this
            // suite lives inside the loop above, so an EMPTY `files` array produced zero
            // failures and an unqualified "CORE DATA HUB OK - 0 canonical file(s)". A
            // canonical data directory that exists but holds nothing to check is not a
            // pass, it is the catalog being gone - which is the exact WebGL-empty defect
            // this oracle was written to catch. An iteration that checked nothing must
            // never be reported as a verification.
            if (checkedCount == 0)
                failures.Add($"canonical data hub checked ZERO files ({files.Length} .json present, {excludedCount} excluded) " +
                             $"in {streamingDir} - this suite asserted nothing; an empty catalog loads EMPTY in WebGL");

            if (failures.Count == 0)
            {
                Debug.Log(log.ToString() + "CORE_DATAHUB_OK");
                reason = $"CORE DATA HUB OK — {checkedCount} canonical file(s) read non-empty + Resources dual-copy present";
                return true;
            }
            reason = "core-datahub: " + string.Join("; ", failures);
            Debug.LogError(log.ToString() + "CORE_DATAHUB_FAIL: " + reason);
            return false;
        }
    }
}
