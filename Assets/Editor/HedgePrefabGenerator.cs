// =============================================================================
// HedgePrefabGenerator — one-shot editor step that makes the polyperfect hedge
// (Fence_Shrub) loadable at RUNTIME (mirrors BridgePrefabGenerator).
// -----------------------------------------------------------------------------
// WHY: CastleMoatBuilder runs at runtime ([RuntimeInitializeOnLoadMethod]) and the
// runtime API can only Resources.Load a prefab under a Resources/ folder — it cannot
// AssetDatabase-load the polyperfect Fence_Shrub prefab (editor-only, and the whole
// pack is GITIGNORED so it is absent on a fresh clone / web build). This bakes a
// committed prefab of Fence_Shrub into Assets/Resources/Hedges/ so the moat builder
// can Resources.Load<GameObject>("Hedges/Fence_Shrub") for the moat's hedge lip ring.
//
// Run once: menu 'Defenders > Seam > Generate Hedge Resources Prefab'.
// No drag-drop authoring; sourced by the prefab GUID so it survives renames.
// On a missing source (pack unimported): Debug.LogWarning + skip — never a hard error.
// =============================================================================
using UnityEngine;
using UnityEditor;

namespace DeNelle.Editor
{
    public static class HedgePrefabGenerator
    {
        // GUID of Assets/polyperfect/.../Building Fences_M/Fence_Shrub.prefab
        private const string SrcGuid   = "4938eefdf61ec4b9097a1398980b421c";
        private const string OutFolder = "Assets/Resources/Hedges";
        private const string OutPath   = "Assets/Resources/Hedges/Fence_Shrub.prefab";

        [MenuItem("Defenders/Seam/Generate Hedge Resources Prefab")]
        public static void Generate()
        {
            string srcPath = AssetDatabase.GUIDToAssetPath(SrcGuid);
            if (string.IsNullOrEmpty(srcPath))
            {
                Debug.LogWarning("[HedgePrefabGenerator] Fence_Shrub GUID " + SrcGuid +
                    " not found — is the polyperfect pack imported? (Assets/polyperfect/...) — skipped.");
                return;
            }

            var src = AssetDatabase.LoadAssetAtPath<GameObject>(srcPath);
            if (src == null)
            {
                Debug.LogWarning("[HedgePrefabGenerator] could not load Fence_Shrub at " + srcPath + " — skipped.");
                return;
            }

            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder(OutFolder))
                AssetDatabase.CreateFolder("Assets/Resources", "Hedges");

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(src);
            if (instance == null)
            {
                Debug.LogWarning("[HedgePrefabGenerator] InstantiatePrefab returned null for " + srcPath + " — skipped.");
                return;
            }

            var prefab = PrefabUtility.SaveAsPrefabAsset(instance, OutPath);
            Object.DestroyImmediate(instance);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (prefab != null)
                Debug.Log("[HedgePrefabGenerator] Created " + OutPath + " from " + srcPath +
                    " — CastleMoatBuilder can now Resources.Load(\"Hedges/Fence_Shrub\").");
            else
                Debug.LogWarning("[HedgePrefabGenerator] SaveAsPrefabAsset failed for " + OutPath + " — skipped.");
        }
    }
}
