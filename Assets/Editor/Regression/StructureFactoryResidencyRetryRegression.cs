// WO-1142: pins the one StructureAssetLoader caller that used to drop paid structures on a
// first-frame residency miss. Registered by DataRegression beside structure-load-bounded.

using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor.Regression
{
    public static class StructureFactoryResidencyRetryRegression
    {
        private const string Rel = "/_Modules/Village/Catalog/StructureFactory.cs";

        [MenuItem("Tools/Regression/World/Structure Factory Residency Retry")]
        public static void RunMenu()
        {
            bool ok = Run(out string reason);
            if (ok) Debug.Log(reason); else Debug.LogError(reason);
        }

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            string path = Application.dataPath + Rel;
            string source = File.Exists(path) ? File.ReadAllText(path) : string.Empty;
            if (source.Length == 0) failures.Add("StructureFactory.cs missing/unreadable");

            Require(source, "BuildPendingArtProxy(root, entry)",
                "Create has no visible fallback for a first-frame residency miss", failures);
            Require(source, "StructureContentWarmer.WhenSettled",
                "Create never retries after the structure warm/request pass settles", failures);
            Require(source, "TryReplacePendingArt(root, entry, capturedProxy)",
                "settle callback does not target the original gameplay root", failures);
            Require(source, "retaining visible proxy; building not lost",
                "failed late resolution is not explicitly fail-loud while preserving the building", failures);
            Require(source, "CosmeticApplier.RefreshOn(root)",
                "late visual replacement does not refresh the existing root's cosmetic seam", failures);

            int miss = source.IndexOf("if (visual == null)", StringComparison.Ordinal);
            int nextStage = source.IndexOf("if (entry.orientation != null", miss, StringComparison.Ordinal);
            if (miss < 0 || nextStage < 0 || miss >= nextStage)
                failures.Add("could not isolate the Create visual-miss branch");
            else
            {
                string createTail = source.Substring(miss, nextStage - miss);
                if (createTail.Contains("DestroyRoot(root)") || createTail.Contains("return null"))
                    failures.Add("Create still destroys/returns-null from the visual residency miss branch");
            }

            if (failures.Count > 0)
            {
                reason = "STRUCTURE_FACTORY_RESIDENCY_RETRY_FAIL\n - " + string.Join("\n - ", failures);
                return false;
            }
            reason = "STRUCTURE_FACTORY_RESIDENCY_RETRY_OK — visible root survives miss; one settle retry replaces art in place";
            return true;
        }

        private static void Require(string source, string marker, string failure, List<string> failures)
        {
            if (source.IndexOf(marker, StringComparison.Ordinal) < 0) failures.Add(failure);
        }
    }
}
