using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;

namespace DeNelle.Editor.Regression
{
    /// <summary>Shared WO-1170 freshness gate. Site 2 owns its single fleet registration.</summary>
    public static class GeneratedFallbackParityRegression
    {
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            CheckBuildCategories(failures);
            CheckStakeRewards(failures);
            reason = failures.Count == 0
                ? "[generated-fallback-parity] build categories and stake rewards are byte-fresh against both canonical copies"
                : string.Join("\n", failures);
            return failures.Count == 0;
        }

        private static void CheckBuildCategories(List<string> failures)
        {
            const string resources = "Assets/Resources/Data/Canonical/build-categories.json";
            const string streaming = "Assets/StreamingAssets/Data/Canonical/build-categories.json";
            CheckPair("build-categories", resources, streaming,
                DeNelle.Village.BuildCategoryFallbackData.SourceSha256, failures);
            try
            {
                JObject parsed = JObject.Parse(DeNelle.Village.BuildCategoryFallbackData.Json);
                int rows = (parsed["categories"] as JArray)?.Count ?? 0;
                if (rows != DeNelle.Village.BuildCategoryFallbackData.SourceCategoryCount)
                    failures.Add($"[generated-fallback-parity] build category embedded row count {rows} != declared {DeNelle.Village.BuildCategoryFallbackData.SourceCategoryCount}");
            }
            catch (Exception ex)
            {
                failures.Add("[generated-fallback-parity] embedded build categories do not parse: " + ex.Message);
            }
        }

        private static void CheckStakeRewards(List<string> failures)
        {
            // Site 3 lands in a separate worktree; reflection keeps this shared owner compilable
            // before merge while still making the fleet RED if its generated artifact is absent.
            Type type = Type.GetType("DeNelle.Core.Platform.StakeRewardsFallbackData, DeNelle.Core");
            if (type == null)
            {
                failures.Add("[generated-fallback-parity] StakeRewardsFallbackData is missing; merge/regenerate WO-1170 site 3 before running the fleet");
                return;
            }
            FieldInfo hashField = type.GetField("SourceSha256", BindingFlags.Public | BindingFlags.Static);
            if (hashField == null)
            {
                failures.Add("[generated-fallback-parity] StakeRewardsFallbackData.SourceSha256 is missing");
                return;
            }
            CheckPair("stake-rewards", "Assets/Resources/Data/Canonical/stake-rewards.json",
                "Assets/StreamingAssets/Data/Canonical/stake-rewards.json",
                hashField.GetValue(null) as string, failures);
        }

        private static void CheckPair(string label, string resources, string streaming,
            string generatedHash, List<string> failures)
        {
            if (!File.Exists(resources) || !File.Exists(streaming))
            {
                failures.Add($"[generated-fallback-parity] {label} canonical copy missing");
                return;
            }
            string resourcesHash = Sha256(File.ReadAllBytes(resources));
            string streamingHash = Sha256(File.ReadAllBytes(streaming));
            if (!string.Equals(resourcesHash, streamingHash, StringComparison.Ordinal))
                failures.Add($"[generated-fallback-parity] {label} canonical copies differ: {resourcesHash} vs {streamingHash}");
            if (!string.Equals(resourcesHash, generatedHash, StringComparison.Ordinal))
                failures.Add($"[generated-fallback-parity] {label} generated fallback is STALE: source={resourcesHash} generated={generatedHash}");
        }

        private static string Sha256(byte[] bytes)
        {
            using (SHA256 sha = SHA256.Create())
            {
                var text = new StringBuilder(64);
                foreach (byte value in sha.ComputeHash(bytes)) text.Append(value.ToString("x2"));
                return text.ToString();
            }
        }
    }
}
