using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor
{
    /// <summary>Read-only ActorCore/AccuRIG and combat-event audit. Never reimports assets.</summary>
    public static class CombatAnimationPipelineAudit
    {
        private const string Marker = "COMBAT_ANIMATION_AUDIT_OK";

        [MenuItem("Defenders/Animation/Audit Combat Animation Pipeline (read only)")]
        public static void Audit()
        {
            var failures = new List<string>();
            var warnings = new List<string>();
            int models = 0, clips = 0, attackClips = 0, hitFrameClips = 0;
            int liveAttackClips = 0, liveHitFrameClips = 0;
            var liveQueue = new List<string[]>();
            var liveModelDependencies = LiveEnemyModelDependencies();

            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[]
                     { "Assets/Action", DeNelle.Core.AssetRoots.EnemyContent }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(path) is not ModelImporter importer) continue;
                models++;

                bool motionLibrary = path.Replace('\\', '/').StartsWith("Assets/Action/", StringComparison.OrdinalIgnoreCase);
                if (motionLibrary && importer.animationType != ModelImporterAnimationType.Human)
                    failures.Add($"{path}: motion source is not Humanoid");
                if (motionLibrary && importer.materialImportMode != ModelImporterMaterialImportMode.None)
                    warnings.Add($"{path}: motion-only source imports materials");

                bool validHumanAvatar = false;
                foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
                    if (asset is Avatar avatar && avatar.isValid && avatar.isHuman) validHumanAvatar = true;
                if (motionLibrary && !validHumanAvatar)
                    failures.Add($"{path}: no valid Humanoid source Avatar");

                ModelImporterClipAnimation[] authored = importer.clipAnimations;
                if (authored == null || authored.Length == 0) authored = importer.defaultClipAnimations;
                if (authored == null) continue;
                foreach (var clip in authored)
                {
                    if (ActionClipImporter.IsBindOrTPoseClipName(clip.name)) continue;
                    clips++;
                    string key = ((clip.name ?? string.Empty) + " " + path).ToLowerInvariant();
                    bool attack = key.Contains("attack") || key.Contains("slash") || key.Contains("stab")
                                  || key.Contains("swing") || key.Contains("strike");
                    bool projectileOrCast = key.Contains("magic") || key.Contains("spell")
                                            || key.Contains("cast") || key.Contains("wizard");
                    attack = attack && !projectileOrCast;
                    if (!attack) continue;
                    attackClips++;
                    bool live = liveModelDependencies.Contains(path);
                    if (live)
                    {
                        liveAttackClips++;
                        liveQueue.Add(new[] { path, clip.name ?? string.Empty });
                    }
                    int hitFrames = 0;
                    if (clip.events != null)
                        foreach (var evt in clip.events)
                            if (evt != null && evt.functionName == "HitFrame") hitFrames++;
                    if (hitFrames == 1)
                    {
                        hitFrameClips++;
                        if (live) liveHitFrameClips++;
                    }
                    else warnings.Add($"{path}::{clip.name}: expected exactly one reviewed HitFrame, found {hitFrames}");
                }
            }

            string root = Directory.GetParent(Application.dataPath)?.FullName ?? ".";
            string reportPath = Path.Combine(root, "Builds", "combat-animation-audit.json");
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
            var json = new StringBuilder();
            json.AppendLine("{");
            json.AppendLine($"  \"generatedUtc\": \"{DateTime.UtcNow:O}\",");
            json.AppendLine($"  \"models\": {models}, \"clips\": {clips},");
            json.AppendLine($"  \"attackClips\": {attackClips}, \"attackClipsWithOneHitFrame\": {hitFrameClips},");
            json.AppendLine($"  \"liveAttackClips\": {liveAttackClips}, \"liveAttackClipsWithOneHitFrame\": {liveHitFrameClips},");
            json.AppendLine($"  \"failures\": {failures.Count}, \"warnings\": {warnings.Count},");
            json.AppendLine("  \"details\": [");
            var details = new List<string>(); details.AddRange(failures); details.AddRange(warnings);
            for (int i = 0; i < details.Count; i++)
                json.Append("    \"").Append(Escape(details[i])).Append('"').AppendLine(i + 1 < details.Count ? "," : "");
            json.AppendLine("  ]");
            json.AppendLine("}");
            File.WriteAllText(reportPath, json.ToString());

            string queuePath = Path.Combine(root, "Builds", "combat-animation-hitframe-review.json");
            var queueJson = new StringBuilder();
            queueJson.AppendLine("{\n  \"schemaVersion\": 1,\n  \"entries\": [");
            for (int i = 0; i < liveQueue.Count; i++)
            {
                queueJson.Append("    { \"assetPath\": \"").Append(Escape(liveQueue[i][0]))
                    .Append("\", \"clipName\": \"").Append(Escape(liveQueue[i][1]))
                    .Append("\", \"hitFrameNormalized\": null, \"status\": \"review-required\" }")
                    .AppendLine(i + 1 < liveQueue.Count ? "," : "");
            }
            queueJson.AppendLine("  ]\n}");
            File.WriteAllText(queuePath, queueJson.ToString());

            foreach (string failure in failures) Debug.LogError("[CombatAnimationAudit] " + failure);
            Debug.Log($"[{Marker}] models={models} clips={clips} attacks={attackClips} " +
                      $"reviewedHitFrames={hitFrameClips} liveAttacks={liveAttackClips} " +
                      $"liveReviewedHitFrames={liveHitFrameClips} failures={failures.Count} warnings={warnings.Count} report={reportPath}");
        }

        private static HashSet<string> LiveEnemyModelDependencies()
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string guid in AssetDatabase.FindAssets("t:AnimatorController", new[]
                     { DeNelle.Core.AssetRoots.EnemyContent, "Assets/Resources" }))
            {
                string controller = AssetDatabase.GUIDToAssetPath(guid);
                foreach (string dependency in AssetDatabase.GetDependencies(controller, true))
                    if (dependency.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
                        result.Add(dependency);
            }
            return result;
        }

        private static string Escape(string value) => (value ?? string.Empty)
            .Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n");
    }
}
