// =============================================================================
// TextureAuditTool (WO-408) — inventory EVERY texture in the project to a CSV so
// the WebGL texture-bloat (Textures = 203 MB / 60% of the build) can be measured
// BEFORE and AFTER the optimizer runs.
// -----------------------------------------------------------------------------
// READ-ONLY. This tool changes nothing — it only scans TextureImporters and
// writes evidence CSVs under Builds/TextureAudit/. Run it twice:
//   1. BEFORE TextureBatchOptimizer  -> texture-audit-before.csv
//   2. AFTER  TextureBatchOptimizer  -> texture-audit-after.csv
// then diff the two to prove the WebGL override actually shrank the payload.
//
// CSV columns:
//   path,role,defaultMaxSize,webglMaxSize,webglOverridden,defaultFormat,
//   webglFormat,compression,onDiskBytes,onDiskMB,inResources,shipsInBuild
//
// "shipsInBuild" is a heuristic: anything under a Resources/ folder, or NOT
// under a Demo/Example/Editor folder, is assumed to ship (it may be referenced
// by a prefab/material/scene). It is intentionally conservative — false
// positives are safe (they just get audited), false negatives would hide bloat.
//
// Batchmode entry points:
//   DeNelle.Editor.TextureAuditTool.RunBefore
//   DeNelle.Editor.TextureAuditTool.RunAfter
//   DeNelle.Editor.TextureAuditTool.Run            (alias of RunBefore)
// =============================================================================

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class TextureAuditTool
    {
        private const string OutputDir = "Builds/TextureAudit";

        // Path fragments (case-insensitive) that mark NON-shipping art.
        private static readonly string[] NonShippingFragments =
        {
            "/Demo/", "/Demos/", "/Example/", "/Examples/", "/Editor/",
        };

        [MenuItem("Defenders/Build/Texture Audit (BEFORE)")]
        public static void RunBefore() => Audit("texture-audit-before.csv");

        [MenuItem("Defenders/Build/Texture Audit (AFTER)")]
        public static void RunAfter() => Audit("texture-audit-after.csv");

        /// <summary>Alias so a generic ".Run" batch call works (defaults to BEFORE).</summary>
        public static void Run() => RunBefore();

        private static void Audit(string fileName)
        {
            string projRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string outDirFull = Path.Combine(projRoot, OutputDir);
            Directory.CreateDirectory(outDirFull);
            string outPath = Path.Combine(outDirFull, fileName);

            string[] guids = AssetDatabase.FindAssets("t:Texture2D");

            var sb = new StringBuilder();
            sb.AppendLine("path,role,defaultMaxSize,webglMaxSize,webglOverridden," +
                          "defaultFormat,webglFormat,compression,onDiskBytes,onDiskMB," +
                          "inResources,shipsInBuild");

            int rows = 0;
            long totalBytes = 0;
            long shippingBytes = 0;
            long resourcesBytes = 0;

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path)) continue;

                var ti = AssetImporter.GetAtPath(path) as TextureImporter;
                if (ti == null) continue;

                var web = ti.GetPlatformTextureSettings("WebGL");

                long bytes = OnDiskBytes(path, projRoot);
                totalBytes += bytes;

                bool inResources = path.Replace('\\', '/')
                    .IndexOf("/Resources/", StringComparison.OrdinalIgnoreCase) >= 0
                    || path.Replace('\\', '/').StartsWith("Assets/Resources/", StringComparison.OrdinalIgnoreCase);
                if (inResources) resourcesBytes += bytes;

                bool ships = ShipsInBuild(path);
                if (ships) shippingBytes += bytes;

                string role = ClassifyRole(path, ti);

                sb.Append(Csv(path)).Append(',')
                  .Append(Csv(role)).Append(',')
                  .Append(ti.maxTextureSize).Append(',')
                  .Append(web.overridden ? web.maxTextureSize : ti.maxTextureSize).Append(',')
                  .Append(web.overridden ? "true" : "false").Append(',')
                  .Append(Csv(ti.textureType.ToString())).Append(',')
                  .Append(Csv(web.overridden ? web.format.ToString() : "(inherit)")).Append(',')
                  .Append(Csv(ti.textureCompression.ToString())).Append(',')
                  .Append(bytes).Append(',')
                  .Append((bytes / 1048576.0).ToString("F3", CultureInfo.InvariantCulture)).Append(',')
                  .Append(inResources ? "true" : "false").Append(',')
                  .Append(ships ? "true" : "false")
                  .AppendLine();

                rows++;
            }

            // Footer summary row (commented so a CSV parser can skip it).
            sb.AppendLine();
            sb.AppendLine($"# textures,{rows}");
            sb.AppendLine($"# total_on_disk_MB,{(totalBytes / 1048576.0).ToString("F1", CultureInfo.InvariantCulture)}");
            sb.AppendLine($"# shipping_on_disk_MB,{(shippingBytes / 1048576.0).ToString("F1", CultureInfo.InvariantCulture)}");
            sb.AppendLine($"# resources_on_disk_MB,{(resourcesBytes / 1048576.0).ToString("F1", CultureInfo.InvariantCulture)}");

            File.WriteAllText(outPath, sb.ToString());

            Debug.Log($"[TextureAuditTool] Wrote {rows} rows -> {outPath}\n" +
                      $"  total on-disk: {(totalBytes / 1048576.0):F1} MB | " +
                      $"shipping: {(shippingBytes / 1048576.0):F1} MB | " +
                      $"Resources: {(resourcesBytes / 1048576.0):F1} MB");
            Debug.Log($"TEXTURE_AUDIT_OK :: file={fileName} rows={rows}");
        }

        /// <summary>Heuristic ship-in-build test: Resources always ships; non-Demo/Editor likely ships.</summary>
        private static bool ShipsInBuild(string assetPath)
        {
            string p = assetPath.Replace('\\', '/');
            if (p.IndexOf("/Resources/", StringComparison.OrdinalIgnoreCase) >= 0)
                return true; // Resources always bundles
            foreach (var frag in NonShippingFragments)
                if (p.IndexOf(frag, StringComparison.OrdinalIgnoreCase) >= 0)
                    return false;
            return true; // conservative: assume referenced art ships
        }

        /// <summary>Coarse role bucket used by the optimizer's max-size table.</summary>
        private static string ClassifyRole(string assetPath, TextureImporter ti)
        {
            string p = assetPath.Replace('\\', '/').ToLowerInvariant();

            if (ti.textureType == TextureImporterType.Sprite) return "ui-sprite";
            if (ti.textureType == TextureImporterType.NormalMap) return "normal-map";

            if (p.Contains("icon")) return "icon";
            if (p.Contains("portrait") || p.Contains("/dialogue/")) return "portrait";
            if (p.Contains("/title") || p.Contains("/intro") || p.Contains("/ui/") || p.Contains("/hud/"))
                return "ui-background";
            if (p.Contains("/heroes") || p.Contains("/people") || p.Contains("/enemies")
                || p.Contains("/pets") || p.Contains("character"))
                return "character";
            if (p.Contains("/structures") || p.Contains("/buildings") || p.Contains("/props")
                || p.Contains("polyperfect") || p.Contains("/models"))
                return "environment";
            return "default";
        }

        private static long OnDiskBytes(string assetPath, string projRoot)
        {
            try
            {
                string full = Path.Combine(projRoot, assetPath);
                if (File.Exists(full)) return new FileInfo(full).Length;
            }
            catch { /* size accounting only — non-fatal */ }
            return 0L;
        }

        /// <summary>CSV-escapes a field (quotes if it contains a comma/quote/newline).</summary>
        private static string Csv(string field)
        {
            if (string.IsNullOrEmpty(field)) return "";
            if (field.IndexOf(',') >= 0 || field.IndexOf('"') >= 0 || field.IndexOf('\n') >= 0)
                return "\"" + field.Replace("\"", "\"\"") + "\"";
            return field;
        }
    }
}
