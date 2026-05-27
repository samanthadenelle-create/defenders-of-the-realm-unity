// =============================================================================
// VikingTowerMaterialFixer — editor one-shot for the viking watch tower FBX
// (Assets/Art/Towers/VikingWatchTower/Tower.fbx).
//
// The FBX imported grey: its two material slots ("tower", "decorations") were
// extracted to MatDir/tower.mat + decorations.mat (Unity 6 no longer supports
// External material *remap*, so those extracted mats are what the renderers
// actually use). So we populate THOSE two materials directly — URP/Lit + the
// PBR BaseColor & Normal maps — rather than remapping.
//
// Also (idempotent): strips the Blender lamp/camera from the import and reduces
// the model 75% (globalScale x0.25), per owner feedback.
//
// Run: Defenders -> Fix Viking Tower Materials.
// =============================================================================

using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class VikingTowerMaterialFixer
    {
        private const string Dir    = "Assets/Art/Towers/VikingWatchTower";
        private const string Fbx    = Dir + "/Tower.fbx";
        private const string MatDir = Dir + "/Materials";
        private const string Tex    = Dir + "/textures";

        [MenuItem("Defenders/Fix Viking Tower Materials")]
        public static void Fix()
        {
            // Texture the two extracted materials the renderers actually use.
            ConfigMat($"{MatDir}/tower.mat",
                $"{Tex}/tower_tower_BaseColor.png", $"{Tex}/tower_tower_Normal.png");
            ConfigMat($"{MatDir}/decorations.mat",
                $"{Tex}/tower_decorations_BaseColor.png", $"{Tex}/tower_decorations_Normal.png");

            // Importer: strip lights/cameras + reduce 75% (idempotent via userData).
            var importer = AssetImporter.GetAtPath(Fbx) as ModelImporter;
            if (importer != null)
            {
                importer.importLights  = false;
                importer.importCameras = false;
                // Owner-tuned ABSOLUTE scale (the original FBX globalScale was 1.0).
                // 0.5 = 2x the earlier 0.25-size tower. Bump TargetScale + the marker
                // together to re-tune; the marker keeps re-runs idempotent.
                const float TargetScale = 0.5f;
                const string Mark = "dotr-scale-0.5";
                if (importer.userData != Mark)
                {
                    float before = importer.globalScale;
                    importer.globalScale = TargetScale;
                    importer.userData = Mark;
                    Debug.Log($"[VikingTowerMat] Scale {before} -> {importer.globalScale} (absolute target, 2x the 0.25 size).");
                }
                importer.SaveAndReimport();
            }

            // Drop the redundant materials the first (remap) attempt created.
            DeleteIfExists($"{MatDir}/VikingTower_Body.mat");
            DeleteIfExists($"{MatDir}/VikingTower_Decorations.mat");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[VikingTowerMat] Done — tower.mat + decorations.mat textured (URP/Lit, BaseColor+Normal).");
        }

        /// <summary>URP/Lit + BaseColor + Normal on an existing material. Wood/stone
        /// tower is non-metallic, so metallic=0 + a mid smoothness (skip the metallic
        /// map to avoid its alpha-smoothness quirk).</summary>
        private static void ConfigMat(string matPath, string baseColor, string normal)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null) { Debug.LogWarning($"[VikingTowerMat] Material not found: {matPath}"); return; }

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader != null) mat.shader = shader;

            var bc = LoadTex(baseColor, asNormal: false);
            if (bc != null) { mat.SetTexture("_BaseMap", bc); mat.SetColor("_BaseColor", Color.white); }
            else Debug.LogWarning($"[VikingTowerMat] BaseColor missing: {baseColor}");

            var nm = LoadTex(normal, asNormal: true);
            if (nm != null) { mat.SetTexture("_BumpMap", nm); mat.EnableKeyword("_NORMALMAP"); mat.SetFloat("_BumpScale", 1f); }

            mat.SetFloat("_Metallic", 0f);
            mat.SetFloat("_Smoothness", 0.4f);
            EditorUtility.SetDirty(mat);
            Debug.Log($"[VikingTowerMat] Textured {matPath} (base={bc != null}, normal={nm != null}).");
        }

        private static Texture2D LoadTex(string path, bool asNormal)
        {
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti != null)
            {
                bool changed = false;
                if (ti.maxTextureSize > 2048) { ti.maxTextureSize = 2048; changed = true; }   // tower needs no 4K
                if (asNormal && ti.textureType != TextureImporterType.NormalMap)
                    { ti.textureType = TextureImporterType.NormalMap; changed = true; }
                if (changed) ti.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static void DeleteIfExists(string path)
        {
            if (AssetDatabase.LoadAssetAtPath<Object>(path) != null) AssetDatabase.DeleteAsset(path);
        }
    }
}
