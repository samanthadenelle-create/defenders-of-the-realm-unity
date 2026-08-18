// =============================================================================
// WebGLSizePass408 — WO-408 build-size reduction, scoped from the BuildReport
// ground truth (NOT disk size). The 2026-06-13 WebGL BuildReport showed the
// 200 MB build is dominated by:
//   • Meshes  115 MB (33%) — Tripo/AccuRIG FBXs ship UNCOMPRESSED
//     (OgreMage 24.9, Troll 24.8, Knight 22.5, Mage/Cleric/Ranger 5-ish MB).
//   • Quaternius Medieval Village MegaKit material textures ~100 MB of the
//     169 MB Textures total — 8 normal maps @ 5.3 MB + ~20 basecolor/rough/ORM
//     @ 2.7 MB, all 2048².
// The hero/enemy base-color textures were a RED HERRING — TripoTextureImportCap
// already caps them to ~170 KB in-build; the 8192² Mirza Beig VFX sheets do NOT
// ship at all. So this pass touches ONLY the two real levers.
//
// SAFE PASS (owner-chosen 2026-06-13):
//   Meshes  → MeshCompression.Medium + meshOptimizationFlags.Everything +
//             isReadable=false (removes the CPU-side duplicate from the build).
//             Medium quantization is visually safe on stylized characters;
//             owner verifies the skinned heroes/enemies after rebuild.
//   Quaternius textures → default maxTextureSize 2048 → 1024 (4× fewer pixels;
//             a tiling world material at 1024 is invisible on mobile/WebGL).
//             Capping the DEFAULT applies to every platform and sidesteps the
//             "WebGL" vs Unity-6 "Web" platform-name trap that regressed before.
//   ItemIcons/ProjectileIcons → 2.6 MB sheets capped to 1024 (zero risk).
//
// Idempotent + reversible (re-import restores from source). Run headless:
//   Unity.exe -batchmode -quit -projectPath <proj> \
//             -executeMethod DeNelle.Editor.WebGLSizePass408.Run
//
// ASSEMBLY: DeNelle.Editor (Editor-only).
// =============================================================================

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor
{
    /// <summary>WO-408 build-size pass: compress shipped meshes + cap Quaternius
    /// world textures, scoped from the BuildReport. Mesh-safe (Medium), reversible.</summary>
    public static class WebGLSizePass408
    {
        // Folders whose FBX meshes ship and are heavy (per BuildReport).
        private static readonly string[] MeshFolders =
        {
            DeNelle.Core.AssetRoots.EnemyContent,
            "Assets/Resources/Heroes",
            DeNelle.Core.AssetRoots.StructureContent,
            "Assets/Resources/Walls",
        };

        // Folders whose textures are oversized world/material/icon art.
        private static readonly string[] TextureCapFolders =
        {
            "Assets/Quaternius/Medieval Village MegaKit/Materials",
            "Assets/Resources/ItemIcons",
            "Assets/Resources/ProjectileIcons",
        };

        private const int TextureCap = 1024;

        [MenuItem("Defenders/Build/WO-408 WebGL Size Pass (mesh+texture)")]
        public static void Run()
        {
            int meshDone = 0, meshFail = 0, texDone = 0, texSkip = 0, texFail = 0;

            // ── 1) Mesh compression on shipped FBXs ──────────────────────────
            string[] modelGuids = AssetDatabase.FindAssets("t:Model", MeshFolders);
            var modelPaths = new HashSet<string>();
            foreach (var g in modelGuids) modelPaths.Add(AssetDatabase.GUIDToAssetPath(g));

            foreach (var path in modelPaths)
            {
                if (!path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)) continue;
                try
                {
                    var mi = AssetImporter.GetAtPath(path) as ModelImporter;
                    if (mi == null) continue;
                    mi.meshCompression = ModelImporterMeshCompression.Medium;
                    mi.meshOptimizationFlags = MeshOptimizationFlags.Everything;
                    mi.isReadable = false;
                    mi.SaveAndReimport();
                    meshDone++;
                    Debug.Log($"[WO-408] mesh compressed (Medium): {path}");
                }
                catch (Exception e)
                {
                    meshFail++;
                    Debug.LogWarning($"[WO-408] mesh pass FAILED on {path}: {e.Message}");
                }
            }

            // ── 2) Texture cap on Quaternius + icon sheets ──────────────────
            string[] texGuids = AssetDatabase.FindAssets("t:Texture2D", TextureCapFolders);
            var texPaths = new HashSet<string>();
            foreach (var g in texGuids) texPaths.Add(AssetDatabase.GUIDToAssetPath(g));

            foreach (var path in texPaths)
            {
                try
                {
                    var ti = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (ti == null) continue;
                    if (ti.maxTextureSize <= TextureCap) { texSkip++; continue; }
                    ti.maxTextureSize = TextureCap;
                    ti.textureCompression = TextureImporterCompression.Compressed;
                    ti.SaveAndReimport();
                    texDone++;
                    Debug.Log($"[WO-408] texture capped → {TextureCap}px: {path}");
                }
                catch (Exception e)
                {
                    texFail++;
                    Debug.LogWarning($"[WO-408] texture pass FAILED on {path}: {e.Message}");
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[WO-408] DONE. Meshes: {meshDone} compressed, {meshFail} failed. " +
                      $"Textures: {texDone} capped, {texSkip} already ≤{TextureCap}, {texFail} failed.");
        }
    }
}
