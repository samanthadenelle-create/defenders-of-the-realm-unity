// =============================================================================
// NpcPackSourceCompressor (WO-93 / Option 1) - shrinks the SOURCE bytes of the
// People pack textures on disk so the pack can be committed via Git LFS without
// bloating the repo. This is the complement to UniversalAssetOptimizer: that one
// changes IMPORT settings (build size); THIS one rewrites the source files.
// -----------------------------------------------------------------------------
// For every .tga under Assets/Models/People it:
//   1. Re-imports a clean readable copy capped at 2048 (CPU pixels - reliable in
//      batchmode, no GPU needed) and encodes it to PNG.
//   2. Backs up the ORIGINAL 4K .tga to <repo>/Backups/People_Originals/ (outside
//      Assets so Unity never re-imports it; the folder is gitignored).
//   3. Replaces T_x.tga with T_x.png, REUSING THE .tga's .meta (renamed) so the
//      GUID is preserved - every material that referenced the texture stays wired
//      (no broken / pink materials, no per-material re-pointing needed).
//   4. Cleanup pass sets good final import settings (crunch-compressed, 2048,
//      not-readable, NormalMap type restored where it applied).
//
// 528 MB of 4K TGA -> roughly 40-60 MB of 2K PNG. Run AFTER the optimizer (only
// one editor instance at a time). Originals are kept in Backups/ for safety.
// Run: Defenders -> NPC Pack - Compress Source Textures (TGA->PNG 2048), or
//      headless: run-unity-method.ps1 -Method DeNelle.Editor.NpcPackSourceCompressor.CompressSourceTextures
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class NpcPackSourceCompressor
    {
        private const string Root = "Assets/Models/People";

        [MenuItem("Defenders/NPC Pack - Compress Source Textures (TGA->PNG 2048)")]
        public static void CompressSourceTextures()
        {
            string backupDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Backups", "People_Originals"));
            Directory.CreateDirectory(backupDir);

            // Snapshot the TGA list up front (we mutate the asset db as we go).
            var tgaPaths = AssetDatabase.FindAssets("t:Texture2D", new[] { Root })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => p.EndsWith(".tga", StringComparison.OrdinalIgnoreCase))
                .Distinct()
                .ToList();

            var converted = new List<(string pngPath, bool isNormal)>();
            int failed = 0;

            foreach (var tga in tgaPaths)
            {
                try
                {
                    var imp = AssetImporter.GetAtPath(tga) as TextureImporter;
                    if (imp == null) { failed++; continue; }
                    bool isNormal = imp.textureType == TextureImporterType.NormalMap;

                    // (1) Clean readable 2048 RGBA on the CPU.
                    imp.textureType = TextureImporterType.Default;   // read raw bytes (don't normal-process)
                    imp.isReadable = true;
                    imp.textureCompression = TextureImporterCompression.Uncompressed;
                    imp.maxTextureSize = 2048;
                    imp.SaveAndReimport();

                    var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(tga);
                    byte[] png = tex != null ? tex.EncodeToPNG() : null;
                    if (png == null || png.Length == 0) { failed++; continue; }

                    string abs = Path.GetFullPath(tga);

                    // (2) Back up the original 4K source (still on disk - reimport
                    //     only changes settings, not the file).
                    string flat = tga.Substring(Root.Length + 1).Replace('/', '_');
                    File.Copy(abs, Path.Combine(backupDir, flat), overwrite: true);

                    // (3) Write PNG, move the .meta over (preserves GUID), drop the TGA.
                    string pngPath = Path.ChangeExtension(tga, ".png");
                    File.WriteAllBytes(Path.GetFullPath(pngPath), png);
                    string tgaMeta = abs + ".meta";
                    string pngMeta = Path.GetFullPath(pngPath) + ".meta";
                    if (File.Exists(pngMeta)) File.Delete(pngMeta);
                    if (File.Exists(tgaMeta)) File.Move(tgaMeta, pngMeta);
                    File.Delete(abs);

                    converted.Add((pngPath, isNormal));
                }
                catch (Exception e)
                {
                    failed++;
                    Debug.LogWarning($"[NpcPackSourceCompressor] {tga}: {e.Message}");
                }
            }

            AssetDatabase.Refresh();   // import the PNGs under their preserved GUIDs

            // (4) Final import settings on each new PNG.
            foreach (var (pngPath, isNormal) in converted)
            {
                var imp = AssetImporter.GetAtPath(pngPath) as TextureImporter;
                if (imp == null) continue;
                if (isNormal) imp.textureType = TextureImporterType.NormalMap;
                imp.isReadable = false;
                imp.maxTextureSize = 2048;
                imp.textureCompression = TextureImporterCompression.Compressed;
                imp.crunchedCompression = true;
                imp.compressionQuality = 70;
                imp.SaveAndReimport();
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"COMPRESS_DONE converted={converted.Count} failed={failed} backup={backupDir}");
        }
    }
}
