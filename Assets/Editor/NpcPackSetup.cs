// =============================================================================
// NpcPackSetup — DEF-91 Phase 1A-1C. Asset pipeline for the CGTrader People pack
// (Assets/Models/People): FBX import (GENERIC rigs) + URP/Lit materials.
// -----------------------------------------------------------------------------
// Owner decision (2026-05-27): GENERIC rigs, not Humanoid — 3 of 4 CGTrader rigs
// fail Humanoid auto-mapping, and every character ships its OWN full AS_* clip set
// so cross-character retargeting isn't needed. Each character's animation clips are
// bound to that character's own SKM avatar (CopyFromOther), so they play on the
// matching rig without any manual avatar configuration.
//
//   Pass 1  SKM_*.fbx -> Generic + own avatar, no animation, no materials.
//   Pass 2  AS_*.fbx  -> Generic, avatar copied from the folder's SKM, import
//                        animation, Loop ON for Idle/Walk/Run/Forging/Talking.
//   1C      URP/Lit materials per character + Blacksmith props from the TGA maps.
// Skips the duplicate "CGTrader Tob/" folder (canonical = "Peasant Tob/").
// Run: Defenders -> NPC Pack - Phase 1 (import + materials).
// =============================================================================

using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class NpcPackSetup
    {
        private const string PackRoot = "Assets/Models/People";
        private const string MatDir   = "Assets/_Modules/Village/NPCs/Materials";

        private static readonly string[] LoopNames = { "Idle", "Walk", "Run", "Forging", "Talking" };

        [MenuItem("Defenders/NPC Pack - Phase 1 (import + materials)")]
        public static void SetupImportsAndMaterials()
        {
            EnsureFolder(MatDir);

            // ── Pass 1: SKM rigs -> Generic; map character folder -> SKM avatar ──
            var folderAvatar = new Dictionary<string, Avatar>();
            int skm = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { PackRoot }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.Contains("/CGTrader Tob/")) continue;
                var file = Path.GetFileNameWithoutExtension(path);
                if (!file.StartsWith("SKM_")) continue;

                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null) continue;
                importer.animationType = ModelImporterAnimationType.Generic;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.importAnimation = false;
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
                importer.SaveAndReimport();
                skm++;

                var avatar = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();
                if (avatar != null)
                {
                    var folder = CharFolderOf(path);
                    // Prefer the "_Unity" SKM avatar where a folder has two (Peasant Tob).
                    if (!folderAvatar.ContainsKey(folder) || file.EndsWith("_Unity"))
                        folderAvatar[folder] = avatar;
                }
            }

            // ── Pass 2: AS clips -> Generic, bound to the folder's SKM avatar ────
            int asn = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { PackRoot }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.Contains("/CGTrader Tob/")) continue;
                var file = Path.GetFileNameWithoutExtension(path);
                if (!file.StartsWith("AS_")) continue;

                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null) continue;
                importer.animationType = ModelImporterAnimationType.Generic;
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
                importer.importAnimation = true;

                if (folderAvatar.TryGetValue(CharFolderOf(path), out var av) && av != null)
                {
                    importer.sourceAvatar = av;
                    importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
                }
                else
                {
                    importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                    Debug.LogWarning($"[NpcPackSetup] {file}: no folder SKM avatar — using own avatar.");
                }

                bool loop = LoopNames.Any(n => file.IndexOf(n, System.StringComparison.OrdinalIgnoreCase) >= 0);
                var clips = importer.defaultClipAnimations;
                if (clips != null && clips.Length > 0)
                {
                    for (int i = 0; i < clips.Length; i++) clips[i].loopTime = loop;
                    importer.clipAnimations = clips;
                }
                importer.SaveAndReimport();
                asn++;
            }

            int mats = 0;
            mats += MakeMat("MAT_Blacksmith",        $"{PackRoot}/Blacksmith/Textures/T_Blacksmith_Base_color.tga",   $"{PackRoot}/Blacksmith/Textures/T_Blacksmith_Normal_OpenGL.tga");
            mats += MakeMat("MAT_Blacksmith_Anvil",  $"{PackRoot}/Blacksmith/Textures/T_Anvil_Base_color.tga",        $"{PackRoot}/Blacksmith/Textures/T_Anvil_Normal_OpenGL.tga");
            mats += MakeMat("MAT_Blacksmith_Hammer", $"{PackRoot}/Blacksmith/Textures/T_Hammer_Base_color.tga",       $"{PackRoot}/Blacksmith/Textures/T_Hammer_Normal_OpenGL.tga");
            mats += MakeMat("MAT_Merchant",          $"{PackRoot}/Merchant/Textures/T_Merchant_Base_color.tga",       $"{PackRoot}/Merchant/Textures/T_Merchant_Normal_OpenGL.tga");
            mats += MakeMat("MAT_Peasant_Mevina",    $"{PackRoot}/Peasant/Textures/T_Peasant_Mevina_Base_color.tga",  $"{PackRoot}/Peasant/Textures/T_Peasant_Mevina_Normal_OpenGL.tga");
            mats += MakeMat("MAT_Peasant_Tob",       $"{PackRoot}/Peasant Tob/Textures/T_Peasant_Tob_Base_color.tga", $"{PackRoot}/Peasant Tob/Textures/T_Peasant_Tob_Normal_OpenGL.tga");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[NpcPackSetup] Phase 1A-1C (Generic) done: {skm} SKM + {asn} AS clips bound, {mats} materials.");
        }

        // Assets/Models/People/<CharFolder>/...  ->  Assets/Models/People/<CharFolder>
        private static string CharFolderOf(string assetPath)
        {
            var rel = assetPath.Substring(PackRoot.Length + 1);
            int slash = rel.IndexOf('/');
            return slash > 0 ? $"{PackRoot}/{rel.Substring(0, slash)}" : PackRoot;
        }

        private static int MakeMat(string name, string baseColor, string normal)
        {
            string path = $"{MatDir}/{name}.mat";
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) { Debug.LogWarning("[NpcPackSetup] URP/Lit shader missing."); return 0; }

            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, path); }
            else mat.shader = shader;

            var bc = LoadTex(baseColor, asNormal: false);
            if (bc != null) { mat.SetTexture("_BaseMap", bc); mat.SetColor("_BaseColor", Color.white); }
            else Debug.LogWarning($"[NpcPackSetup] BaseColor missing: {baseColor}");

            var nm = LoadTex(normal, asNormal: true);
            if (nm != null) { mat.SetTexture("_BumpMap", nm); mat.EnableKeyword("_NORMALMAP"); mat.SetFloat("_BumpScale", 1f); }

            mat.SetFloat("_Smoothness", 0.3f);
            EditorUtility.SetDirty(mat);
            return 1;
        }

        private static Texture2D LoadTex(string path, bool asNormal)
        {
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti != null && asNormal && ti.textureType != TextureImporterType.NormalMap)
            { ti.textureType = TextureImporterType.NormalMap; ti.SaveAndReimport(); }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static void EnsureFolder(string dir)
        {
            if (AssetDatabase.IsValidFolder(dir)) return;
            var parent = Path.GetDirectoryName(dir).Replace("\\", "/");
            var leaf = Path.GetFileName(dir);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
