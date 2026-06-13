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

        /// <summary>
        /// ANIMATION FIX (owner priority 2026-06-13): the new Tripo enemies imported as
        /// animationType=Generic (2), so the SHARED humanoid controllers (LargeEnemy etc.)
        /// can't retarget onto them → they T-pose / slide. They DO have a skeleton
        /// (skeletonHasParents:1), so convert to Humanoid + build an avatar; if Unity can
        /// map the rig the walk/attack/death cycles retarget on. If an avatar does NOT build
        /// (isHuman=false), the skeleton isn't humanoid-mappable → owner runs it through AccuRIG.
        /// </summary>
        [MenuItem("Defenders/Art/Set Wight Enemies to Humanoid (animate via shared controller)")]
        public static void ConvertWightsToHumanoid()
        {
            foreach (var p in WightFbx)
            {
                var imp = AssetImporter.GetAtPath(p) as ModelImporter;
                if (imp == null) { Debug.LogWarning($"[Humanoid] no ModelImporter at '{p}'."); continue; }

                imp.animationType = ModelImporterAnimationType.Human;
                imp.avatarSetup   = ModelImporterAvatarSetup.CreateFromThisModel;
                imp.SaveAndReimport();

                string name = Path.GetFileNameWithoutExtension(p);
                bool foundAvatar = false;
                foreach (var o in AssetDatabase.LoadAllAssetsAtPath(p))
                {
                    if (o is Avatar av)
                    {
                        foundAvatar = true;
                        Debug.Log($"[Humanoid] {name}: avatar built isValid={av.isValid} isHuman={av.isHuman} " +
                                  $"{(av.isValid && av.isHuman ? "-> WILL ANIMATE via the shared humanoid controller" : "-> avatar invalid, needs AccuRIG")}");
                    }
                }
                if (!foundAvatar)
                    Debug.LogWarning($"[Humanoid] {name}: NO avatar built — skeleton not humanoid-mappable; run it through AccuRIG.");
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[Humanoid] DONE — HUMANOID_CONVERT_OK");
        }

        /// <summary>
        /// Revert the enemies whose skeleton is NOT humanoid-mappable (avatar isHuman=False)
        /// back to Generic, so they sit in a clean state (plain T-pose, no broken-humanoid
        /// retarget warnings) until the owner re-exports an AccuRIG'd (CC_Base) version that
        /// Unity CAN map to Humanoid. Troll mapped fine and is left Humanoid.
        /// </summary>
        [MenuItem("Defenders/Art/Revert Unmappable Wights (Demon+OgreMage) to Generic (await AccuRIG)")]
        public static void RevertUnmappableToGeneric()
        {
            string[] unmappable =
            {
                "Assets/Resources/Enemies/Demon.fbx",
                "Assets/Resources/Enemies/OgreMage.fbx",
            };
            foreach (var p in unmappable)
            {
                var imp = AssetImporter.GetAtPath(p) as ModelImporter;
                if (imp == null) continue;
                imp.animationType = ModelImporterAnimationType.Generic;
                imp.SaveAndReimport();
                Debug.Log($"[Humanoid] reverted {Path.GetFileNameWithoutExtension(p)} -> Generic (await AccuRIG).");
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[Humanoid] REVERT DONE — REVERT_GENERIC_OK");
        }

        /// <summary>
        /// Ground-truth test: import Assets/Resources/Enemies/_WightCheck.fbx as Humanoid and
        /// report whether Unity can build a HUMAN avatar (isHuman). True = the rig is
        /// humanoid-mappable (AccuRIG worked) → it will animate on the shared controller.
        /// False = still a non-humanoid (raw Tripo) skeleton → needs a real AccuRIG pass.
        /// </summary>
        [MenuItem("Defenders/Art/Check _WightCheck.fbx is Humanoid (AccuRIG verify)")]
        public static void CheckWightHumanoid()
        {
            const string p = "Assets/Resources/Enemies/_WightCheck.fbx";
            var imp = AssetImporter.GetAtPath(p) as ModelImporter;
            if (imp == null) { Debug.LogWarning($"[WightCheck] no ModelImporter at '{p}'."); return; }
            imp.animationType = ModelImporterAnimationType.Human;
            imp.avatarSetup   = ModelImporterAvatarSetup.CreateFromThisModel;
            imp.SaveAndReimport();
            bool found = false;
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(p))
                if (o is Avatar av)
                {
                    found = true;
                    Debug.Log($"[WightCheck] avatar isValid={av.isValid} isHuman={av.isHuman} " +
                              $"{(av.isValid && av.isHuman ? "-> ACCURIG OK, WILL ANIMATE" : "-> NOT humanoid-mappable, needs real AccuRIG")}");
                }
            if (!found) Debug.LogWarning("[WightCheck] no avatar built — definitely not humanoid.");
            Debug.Log("[WightCheck] DONE — WIGHT_CHECK_OK");
        }

        /// <summary>
        /// Finalize the AccuRIG'd wight at Demon.fbx: import Humanoid (so it animates on the
        /// shared LargeEnemy controller), verify the avatar, and cap its basecolor to 1024 for
        /// the mobile/Seeker target. Run after copying the AccuRIG FBX + .fbm over Demon.
        /// </summary>
        [MenuItem("Defenders/Art/Finalize Wight (Demon) — Humanoid + 1024 cap")]
        public static void FinalizeDemonWight()
        {
            const string p = "Assets/Resources/Enemies/Demon.fbx";
            var imp = AssetImporter.GetAtPath(p) as ModelImporter;
            if (imp == null) { Debug.LogWarning("[Finalize] no ModelImporter for Demon.fbx"); return; }
            imp.animationType = ModelImporterAnimationType.Human;
            imp.avatarSetup   = ModelImporterAvatarSetup.CreateFromThisModel;
            imp.SaveAndReimport();

            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(p))
                if (o is Avatar av)
                    Debug.Log($"[Finalize] Demon avatar isValid={av.isValid} isHuman={av.isHuman} " +
                              $"{(av.isValid && av.isHuman ? "-> ANIMATES" : "-> still not humanoid")}");

            // Cap the wight's textures to 1024 (mobile/Seeker memory).
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D",
                         new[] { "Assets/Resources/Enemies/Demon.fbm" }))
            {
                var tp = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(tp) is TextureImporter ti && ti.maxTextureSize > 1024)
                {
                    ti.maxTextureSize = 1024;
                    ti.SaveAndReimport();
                    Debug.Log($"[Finalize] capped {tp} -> 1024");
                }
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[Finalize] DONE — FINALIZE_DEMON_OK");
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
