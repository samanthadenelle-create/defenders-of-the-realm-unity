// =============================================================================
// MeshCompressionPass (APK size pass) -- shrink the vertex/mesh payload that
// rides in the APK behind the heavy model packs (KayKit, polyperfect, the
// Enemies/Heroes rigs, and everything else under Assets/Models).
// -----------------------------------------------------------------------------
// A companion to TextureShrinkAudit: that tool crushes the TEXTURE payload
// (Android ASTC), this one crushes the MESH payload by turning on Unity's
// per-importer mesh compression. Vertex data (positions, normals, tangents,
// UVs) is quantised into a compact packed form at import; the runtime unpacks
// transparently. This is a size lever, not a runtime-cost lever.
//
// SAFETY -- why LOW only:
//   ModelImporterMeshCompression has Off / Low / Medium / High. Low is the
//   near-lossless tier (a wide quantisation window) -- visually indistinguishable
//   on game-scale meshes. Medium/High trade visible vertex wobble / seam gaps
//   for a few more KB and are NOT used here. This pass sets Low, and ONLY Low.
//
//   It touches ONLY ModelImporter.meshCompression. It NEVER changes isReadable,
//   the rig/avatar setup, animation import, materials, normals/tangents import
//   mode, scale, or anything else -- so the hero + enemy rigs animate exactly as
//   before. Non-Model assets are never touched (we resolve the importer and
//   skip anything that is not a ModelImporter).
//
// TWO batchmode-callable entry points (mirrors TextureShrinkAudit):
//   DeNelle.Editor.MeshCompressionPass.Report  -- DRY RUN, changes NOTHING.
//       Scans every ModelImporter under the heavy roots, buckets by current
//       meshCompression, prints counts, writes Builds/mesh-compression-report.txt,
//       ends with the marker MESH_REPORT_OK.
//   DeNelle.Editor.MeshCompressionPass.Apply   -- sets meshCompression = Low on
//       every model currently at Off, idempotent (already Low/Medium/High are
//       skipped -- never downgraded), SaveAndReimport each changed, progress
//       every 50, ends with MESH_APPLY_OK changed=<n> skipped=<n>.
//
// Reimports are batched inside StartAssetEditing / StopAssetEditing. Every
// per-asset op is wrapped in try/catch: a bad asset is logged and skipped, it
// never aborts the run.
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class MeshCompressionPass
    {
        // =====================================================================
        //  TUNABLES
        // =====================================================================

        // The SAFE, near-lossless compression tier. Do NOT raise this to
        // Medium/High -- those introduce visible vertex quantisation error.
        private const ModelImporterMeshCompression SafeLevel = ModelImporterMeshCompression.Low;

        // Report output (relative to the project root).
        private const string ReportRelPath = "Builds/mesh-compression-report.txt";

        // Progress log cadence for Apply().
        private const int ProgressEvery = 50;

        // The scan roots. Missing roots are skipped with a warning (packs like
        // polyperfect / Quaternius are gitignored and absent on some clones).
        // Assets/Models covers KayKit + KayKit Adventurers + the Complete KayKit
        // Collection + Cathedral + CastleGate + People + Pet in one root.
        private static readonly string[] RootFolders =
        {
            "Assets/Models",             // KayKit + all env/prop/character model packs
            "Assets/Resources/Enemies",  // enemy rigs (skinned meshes)
            "Assets/Resources/Heroes",   // hero rigs (skinned meshes) -- meshCompression only
            "Assets/polyperfect",        // low-poly shared model pack
            "Assets/Quaternius",         // low-poly model pack (gitignored -- may be absent)
        };

        // Non-shipping / editor-only art -- never touched.
        private static readonly string[] SkipFragments =
        {
            "/Demo/", "/Demos/", "/Example/", "/Examples/", "/Editor/",
        };

        // =====================================================================
        //  (1) REPORT -- dry run, changes nothing
        // =====================================================================

        [MenuItem("Defenders/Build/Mesh Compression -- Report (dry run)")]
        public static void Report()
        {
            var folders = ResolveScanFolders();
            if (folders.Count == 0)
            {
                Debug.LogWarning("[MeshCompressionPass] No scan folders found -- nothing to report.");
                Debug.Log("MESH_REPORT_OK");
                return;
            }

            string projRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var seen = new HashSet<string>();

            // Bucket counts by current compression level.
            var byLevel = new Dictionary<ModelImporterMeshCompression, int>();
            foreach (ModelImporterMeshCompression lv in
                     Enum.GetValues(typeof(ModelImporterMeshCompression)))
                byLevel[lv] = 0;

            int totalScanned = 0, totalSkipped = 0, wouldChange = 0;

            foreach (var folder in folders)
            {
                foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { folder }))
                {
                    if (!seen.Add(guid)) continue;
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (string.IsNullOrEmpty(path)) continue;
                    if (IsSkippedPath(path)) { totalSkipped++; continue; }

                    try
                    {
                        var mi = AssetImporter.GetAtPath(path) as ModelImporter;
                        if (mi == null) { totalSkipped++; continue; }

                        byLevel[mi.meshCompression]++;
                        if (mi.meshCompression == ModelImporterMeshCompression.Off) wouldChange++;
                        totalScanned++;
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"[MeshCompressionPass] Report: skipped '{path}' -- {e.Message}");
                        totalSkipped++;
                    }
                }
            }

            var sb = new StringBuilder();
            sb.AppendLine("=== Mesh Compression Pass -- APK size pass (DRY RUN) ===");
            sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Safe target level: {SafeLevel} (near-lossless; Medium/High deliberately NOT used)");
            sb.AppendLine($"Scan folders: {folders.Count}   Models scanned: {totalScanned}   Skipped (demo/editor/non-model): {totalSkipped}");
            sb.AppendLine();
            sb.AppendLine("Current meshCompression distribution:");
            foreach (ModelImporterMeshCompression lv in
                     Enum.GetValues(typeof(ModelImporterMeshCompression)))
                sb.AppendLine($"    {lv,-8} : {byLevel[lv]}");
            sb.AppendLine();
            sb.AppendLine($"Models currently Off that Apply() WOULD set to {SafeLevel}: {wouldChange}");
            sb.AppendLine();
            sb.AppendLine("NOTES:");
            sb.AppendLine("  * Apply() ONLY changes ModelImporter.meshCompression (Off -> Low). It NEVER");
            sb.AppendLine("    touches isReadable, rig/avatar, animation, normals/tangents, scale, or materials.");
            sb.AppendLine("  * Models already Low/Medium/High are skipped -- never downgraded.");
            sb.AppendLine("  * All changes are to git-tracked .meta files -- fully revertable.");

            string outPath = WriteReport(projRoot, sb.ToString());
            Debug.Log(sb.ToString());
            Debug.Log($"[MeshCompressionPass] Report written -> {outPath}");
            Debug.Log("MESH_REPORT_OK");
        }

        // =====================================================================
        //  (2) APPLY -- sets meshCompression = Low, idempotent
        // =====================================================================

        [MenuItem("Defenders/Build/Mesh Compression -- Apply (Low)")]
        public static void Apply()
        {
            var folders = ResolveScanFolders();
            if (folders.Count == 0)
            {
                Debug.LogWarning("[MeshCompressionPass] No scan folders found -- nothing to apply.");
                Debug.Log("MESH_APPLY_OK changed=0 skipped=0");
                return;
            }

            var seen = new HashSet<string>();
            int changed = 0, skipped = 0, processed = 0;

            try
            {
                AssetDatabase.StartAssetEditing();

                foreach (var folder in folders)
                {
                    foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { folder }))
                    {
                        if (!seen.Add(guid)) continue;
                        string path = AssetDatabase.GUIDToAssetPath(guid);
                        if (string.IsNullOrEmpty(path)) continue;
                        if (IsSkippedPath(path)) { skipped++; continue; }

                        try
                        {
                            var mi = AssetImporter.GetAtPath(path) as ModelImporter;
                            if (mi == null) { skipped++; continue; }

                            // Idempotent + non-destructive: only lift Off -> Low.
                            // Anything already Low/Medium/High is left exactly as-is
                            // (never downgraded).
                            if (mi.meshCompression != ModelImporterMeshCompression.Off)
                            {
                                skipped++;
                                processed++;
                                continue;
                            }

                            // ONLY meshCompression -- every other importer setting
                            // (rig, animation, isReadable, normals, scale) is left
                            // untouched so rigs animate identically.
                            mi.meshCompression = SafeLevel;
                            mi.SaveAndReimport();

                            changed++;
                        }
                        catch (Exception e)
                        {
                            Debug.LogWarning($"[MeshCompressionPass] Apply: skipped '{path}' -- {e.Message}");
                            skipped++;
                        }

                        processed++;
                        if (processed % ProgressEvery == 0)
                            Debug.Log($"[MeshCompressionPass] ...processed {processed}  (changed {changed}, skipped {skipped})");
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.Refresh();
            }

            Debug.Log($"[MeshCompressionPass] Apply done. changed={changed} skipped={skipped} (level={SafeLevel})");
            Debug.Log($"MESH_APPLY_OK changed={changed} skipped={skipped}");
        }

        // =====================================================================
        //  Helpers
        // =====================================================================

        /// <summary>Static roots that actually exist as valid asset folders.</summary>
        private static List<string> ResolveScanFolders()
        {
            var folders = new List<string>();
            foreach (var root in RootFolders)
            {
                if (AssetDatabase.IsValidFolder(root)) folders.Add(root);
                else Debug.LogWarning($"[MeshCompressionPass] Skipping missing root: {root}");
            }
            return folders;
        }

        /// <summary>True if the path is non-shipping demo/example/editor art.</summary>
        private static bool IsSkippedPath(string assetPath)
        {
            string p = assetPath.Replace('\\', '/');
            foreach (var frag in SkipFragments)
                if (p.IndexOf(frag, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        private static string WriteReport(string projRoot, string body)
        {
            string outPath = Path.Combine(projRoot, ReportRelPath);
            try
            {
                string dir = Path.GetDirectoryName(outPath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(outPath, body);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[MeshCompressionPass] Could not write report to {outPath}: {e.Message}");
            }
            return outPath;
        }
    }
}
