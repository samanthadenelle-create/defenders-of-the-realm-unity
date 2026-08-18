// =============================================================================
// StructureImportOptimizer — shrink what Assets/StructureContent costs in the
// APK, without changing what the player sees.
// -----------------------------------------------------------------------------
// WHY (owner, 2026-08-17): the APK went 570.9 -> 603.6 MB (+32.7 MB) after the
// owner-purchased art landed. Her diagnosis, exactly right: "its the new
// buildings" / "from 100k to 3mb". Forge 64 KB -> 3.07 MB, armorer 96 KB ->
// 3.05 MB, jeweler 160 KB -> 2.97 MB — roughly 30x each, plus six new models.
//
// ⛔ AND THE BYTES ARE NOT WHERE I FIRST SAID THEY WERE. I told the owner the
// 3 MB was texture data. Measured: .fbx = 42.81 MB, ALL textures = 19.43 MB.
// The MESHES dominate — Tripo exports dense, unoptimised geometry. A texture-only
// pass would have trimmed the smaller half and reported victory. Measure first,
// every time; "obviously it's the textures" is the kind of confident guess §12
// exists to stop.
//
// TWO LEVERS, both import-only — no asset is modified, nothing is re-authored,
// and every setting is reversible by flipping it back and reimporting:
//
//   MESHES (the big half)
//     meshCompression = High     quantises vertex/normal/UV precision. This is a
//                                BUILD-SIZE lever; at building scale on a phone
//                                the quantisation is not visible.
//     isReadable = false         no second CPU-side copy in the build. These are
//                                static props — nothing reads their mesh at runtime.
//     optimizeMeshPolygons/Vertices  reorders for GPU cache; also drops unused verts.
//     weldVertices = true        merges duplicates Tripo leaves behind.
//     importBlendShapes/Cameras/Lights = false, importVisibility/animation off
//                                Tripo FBXs carry none of these; importing them
//                                costs bytes for nothing.
//
//   TEXTURES (the smaller half)
//     maxTextureSize 1024        a building read at phone distance never resolves
//                                2048. This is the single biggest texture lever.
//     compression High quality   ASTC/ETC via the platform default.
//     mipmaps on                 distant buildings are the common case; without
//                                mips they shimmer AND sample the full-res level.
//     *_normal -> NormalMap type so Unity packs it properly instead of as colour.
//
// ⚠ NOT DONE HERE, ON PURPOSE: deleting the redundant metallic/roughness maps
// that several models ship alongside a combined _rm. Deleting an asset a material
// references breaks the material; that is an asset edit, not an import setting,
// and it belongs in its own reversible change.
//
// Run: -executeMethod DeNelle.Editor.StructureImportOptimizer.Run
// =============================================================================

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor
{
    /// <summary>Import-settings optimiser for Assets/StructureContent.</summary>
    public static class StructureImportOptimizer
    {
        private const string Root = DeNelle.Core.AssetRoots.StructureContent;
        private const string OkMarker = "STRUCTURE_IMPORT_OK";

        /// <summary>Buildings are read at phone distance; 2048 never resolves.</summary>
        private const int MaxTextureSize = 1024;

        [MenuItem("Defenders/Art/Optimize Structures import settings")]
        public static void RunMenu() => Run();

        public static void Run()
        {
            if (!AssetDatabase.IsValidFolder(Root))
            {
                Debug.LogError($"[StructureImport] {Root} not found — nothing done.");
                return;
            }

            long before = FolderBytes();
            int models = 0, textures = 0, skipped = 0;

            // ---- MESHES ------------------------------------------------------
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { Root }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var mi = AssetImporter.GetAtPath(path) as ModelImporter;
                if (mi == null) { skipped++; continue; }

                bool changed = false;

                if (mi.meshCompression != ModelImporterMeshCompression.High)
                { mi.meshCompression = ModelImporterMeshCompression.High; changed = true; }

                if (mi.isReadable) { mi.isReadable = false; changed = true; }
                if (!mi.optimizeMeshPolygons) { mi.optimizeMeshPolygons = true; changed = true; }
                if (!mi.optimizeMeshVertices) { mi.optimizeMeshVertices = true; changed = true; }
                if (!mi.weldVertices) { mi.weldVertices = true; changed = true; }
                if (mi.importBlendShapes) { mi.importBlendShapes = false; changed = true; }
                if (mi.importCameras) { mi.importCameras = false; changed = true; }
                if (mi.importLights) { mi.importLights = false; changed = true; }
                if (mi.importVisibility) { mi.importVisibility = false; changed = true; }

                // ⚠ ANIMATION IS LEFT ALONE. These are static props today, but the NPC and pet
                // pipelines share this importer shape, and stripping animation from something that
                // turns out to be rigged is a silent, hard-to-trace break. Import type is not this
                // pass's business.

                if (changed)
                {
                    mi.SaveAndReimport();
                    models++;
                    Debug.Log($"[StructureImport] mesh optimised: {System.IO.Path.GetFileName(path)}");
                }
            }

            // ---- TEXTURES ----------------------------------------------------
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Root }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var ti = AssetImporter.GetAtPath(path) as TextureImporter;
                if (ti == null) { skipped++; continue; }

                bool changed = false;

                if (ti.maxTextureSize > MaxTextureSize)
                { ti.maxTextureSize = MaxTextureSize; changed = true; }

                if (ti.textureCompression != TextureImporterCompression.CompressedHQ)
                { ti.textureCompression = TextureImporterCompression.CompressedHQ; changed = true; }

                if (!ti.mipmapEnabled) { ti.mipmapEnabled = true; changed = true; }

                // A normal map imported as a colour texture is both wrong and larger. Tripo names
                // them consistently, so the name is a reliable signal here.
                string lower = System.IO.Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
                if (lower.EndsWith("_normal") && ti.textureType != TextureImporterType.NormalMap)
                { ti.textureType = TextureImporterType.NormalMap; changed = true; }

                // Nothing in this folder is sampled by script.
                if (ti.isReadable) { ti.isReadable = false; changed = true; }

                if (changed)
                {
                    ti.SaveAndReimport();
                    textures++;
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            long after = FolderBytes();
            Debug.Log($"[StructureImport] {models} model(s), {textures} texture(s) re-imported, {skipped} skipped.");
            // ⚠ SOURCE BYTES BARELY MOVE — import settings change what the BUILD serialises, not
            // what the .fbx weighs on disk. The real number is the APK; this is reported only so
            // nobody reads an unchanged folder size as "it did nothing".
            Debug.Log($"[StructureImport] source folder {before / 1048576.0:F1} MB -> {after / 1048576.0:F1} MB " +
                      "(source is EXPECTED to be ~unchanged — the win lands in the APK, measure there).");
            Debug.Log($"{OkMarker} {models} models, {textures} textures");
        }

        private static long FolderBytes()
        {
            long total = 0;
            foreach (var f in System.IO.Directory.GetFiles(Root, "*", System.IO.SearchOption.AllDirectories))
            {
                if (f.EndsWith(".meta")) continue;
                total += new System.IO.FileInfo(f).Length;
            }
            return total;
        }
    }
}
