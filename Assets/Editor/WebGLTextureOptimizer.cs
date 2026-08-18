// =============================================================================
// WebGLTextureOptimizer (WO-211 Phase 2) — shrink the uncompressed-texture bloat
// that dominates the WebGL .data payload.
// -----------------------------------------------------------------------------
// The WebGL build's WebGL.data is texture-bound (Resources/Textures ~85 MB +
// Resources/Heroes/Textures ~71 MB of 4K base-color PNGs). This pass caps the
// big character/building/enemy/pet textures to 2048 and forces COMPRESSED import
// (vs RGBA32 uncompressed), which is the real size win — far more than deleting a
// source FBX (a source file isn't shipped; its imported texture is).
//
// SAFE + REVERSIBLE: only changes TextureImporter settings (persisted in .meta,
// tracked by git). No texture pixels are edited; reverting the .meta restores the
// originals. Scoped to model/character texture folders under Resources so UI
// sprites/icons elsewhere are left alone. Run headless:
//
//   Unity.exe -batchmode -quit -projectPath <proj> \
//     -executeMethod DeNelle.Editor.WebGLTextureOptimizer.Optimize
//
// Owner/Tricia must eyeball the rebuilt WebGL on a 1440p+ display before this is
// committed (downsampling can show on hi-DPI). Tune MaxSize / folders below.
// =============================================================================

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class WebGLTextureOptimizer
    {
        private const int MaxSize = 2048;

        // Only these Resources subtrees — the heavy model/character/pet/enemy bakes.
        // UI sprites, icons, and TMP atlases live elsewhere and are left untouched.
        private static readonly string[] Folders =
        {
            "Assets/Resources/Textures",
            "Assets/Resources/Heroes",
            "Assets/HeroContent",   // WO-545: heroes migrated out of Resources into per-hero bundles
            "Assets/Resources/Pets",
            DeNelle.Core.AssetRoots.EnemyContent,
            "Assets/Resources/Cosmetics",
        };

        [MenuItem("Defenders/Art/Optimize WebGL Textures (2K + compressed)")]
        public static void Optimize()
        {
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", Folders);
            int changed = 0, scanned = 0;
            var notes = new List<string>();

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path)) continue;
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;
                scanned++;

                bool dirty = false;

                // 1. Cap resolution to 2K (default platform).
                if (importer.maxTextureSize > MaxSize)
                {
                    importer.maxTextureSize = MaxSize;
                    dirty = true;
                }

                // 2. Force compressed (the big win vs RGBA32 uncompressed).
                if (importer.textureCompression == TextureImporterCompression.Uncompressed)
                {
                    importer.textureCompression = TextureImporterCompression.Compressed;
                    dirty = true;
                }

                // 3. Pin a WebGL platform override so the player build honours 2K + compressed
                //    even if the default platform differs.
                var web = importer.GetPlatformTextureSettings("WebGL");
                if (!web.overridden || web.maxTextureSize > MaxSize ||
                    web.format == TextureImporterFormat.RGBA32 || web.format == TextureImporterFormat.RGB24)
                {
                    web.overridden = true;
                    web.maxTextureSize = Mathf.Min(web.maxTextureSize <= 0 ? MaxSize : web.maxTextureSize, MaxSize);
                    web.format = TextureImporterFormat.Automatic;
                    web.textureCompression = TextureImporterCompression.Compressed;
                    importer.SetPlatformTextureSettings(web);
                    dirty = true;
                }

                if (dirty)
                {
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                    changed++;
                    if (notes.Count < 40) notes.Add($"  {path}");
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[WebGLTextureOptimizer] scanned {scanned} textures, changed {changed} (cap {MaxSize}, compressed).");
            foreach (string n in notes) Debug.Log(n);
            Debug.Log($"WEBGL_TEX_OPT_OK :: changed={changed}");
        }
    }
}
