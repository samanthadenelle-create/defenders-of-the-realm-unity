// =============================================================================
// TripoEnemyMaterialExtractor — extract the EMBEDDED textures/materials from a Tripo
// enemy FBX so URP can render it (fixes the "wight" = Demon/OgreMage magenta/untextured).
// -----------------------------------------------------------------------------
// ROOT CAUSE (owner playtest 2026-06-13, confirmed from the .meta): Demon.fbx and
// OgreMage.fbx import with `externalObjects: {}` — their embedded textures were never
// extracted, so the auto-generated material has no _BaseMap and the runtime
// TripoMaterialFixer has nothing to apply → magenta/untextured. The WORKING Tripo
// enemies (Orc_*.fbx) have a POPULATED externalObjects (extracted textures + remapped
// material). This tool brings Demon/OgreMage to the same state: pull the embedded
// textures into a sibling .fbm folder + reimport so the material links _BaseMap, then
// the existing FixTripoMaterials runtime path (EnemyFactory) renders it in URP.
//
// Reusable: ExtractFor(path) works for ANY Tripo FBX that imported with empty
// externalObjects. Run headless: DeNelle.Editor.TripoEnemyMaterialExtractor.ExtractWights.
// =============================================================================

using System.IO;
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class TripoEnemyMaterialExtractor
    {
        private static readonly string[] WightFbx =
        {
            "Assets/Resources/Enemies/Demon.fbx",
            "Assets/Resources/Enemies/OgreMage.fbx",
            "Assets/Resources/Enemies/Troll.fbx",   // 2026-06-13: untextured (externalObjects:{}) — featured in the brute wave
        };

        [MenuItem("Defenders/Art/Extract Wight (Demon+OgreMage) Tripo Textures")]
        public static void ExtractWights()
        {
            int ok = 0;
            foreach (var p in WightFbx)
                if (ExtractFor(p)) ok++;
            // PERSIST: ExtractTextures + the reimport only modify the in-memory AssetDatabase;
            // without SaveAssets the externalObjects remap is dropped to the .meta on disk so the
            // NEXT Unity session (the player build) reimports the FBX raw → magenta again (Troll
            // regressed exactly this way 2026-06-13). SaveAssets flushes the .meta now.
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[TripoExtract] DONE — extracted {ok}/{WightFbx.Length} (saved). TRIPO_EXTRACT_OK");
        }

        /// <summary>Extract embedded textures for one FBX into its sibling .fbm folder and
        /// reimport so the material links _BaseMap. Returns true if extraction ran.</summary>
        public static bool ExtractFor(string fbxPath)
        {
            var imp = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
            if (imp == null)
            {
                Debug.LogWarning($"[TripoExtract] no ModelImporter at '{fbxPath}' — skipped.");
                return false;
            }

            string dir = Path.GetDirectoryName(fbxPath).Replace('\\', '/');
            string baseName = Path.GetFileNameWithoutExtension(fbxPath);
            string fbm = $"{dir}/{baseName}.fbm";   // Unity's conventional embedded-texture folder

            // 1) pull the embedded textures out as real assets (idempotent — re-extract is fine).
            bool any = imp.ExtractTextures(fbm);
            Debug.Log($"[TripoExtract] {baseName}: ExtractTextures -> '{fbm}' extracted={any}");

            // 2) force a reimport so the auto material remaps its _BaseMap to the extracted texture.
            AssetDatabase.ImportAsset(fbxPath, ImportAssetOptions.ForceUpdate);

            // 3) report what the material now references so the headless run is verifiable.
            var mat = AssetDatabase.LoadAssetAtPath<Material>(fbxPath); // first sub-material if any
            var obj = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
            foreach (var o in obj)
            {
                if (o is Material m)
                {
                    bool hasBase = m.HasProperty("_BaseMap") && m.GetTexture("_BaseMap") != null;
                    bool hasMain = m.HasProperty("_MainTex") && m.GetTexture("_MainTex") != null;
                    Debug.Log($"[TripoExtract] {baseName}: material '{m.name}' shader='{m.shader.name}' _BaseMap={hasBase} _MainTex={hasMain}");
                }
            }
            return any;
        }
    }
}
