// =============================================================================
// BridgePrefabGenerator — one-shot editor step that makes the polyperfect stone
// bridge FBX loadable at RUNTIME.
// -----------------------------------------------------------------------------
// WHY: CastleMoatBuilder runs at runtime ([RuntimeInitializeOnLoadMethod]) and the
// runtime API can only Resources.Load a prefab under a Resources/ folder — it cannot
// AssetDatabase-load the polyperfect FBX (editor-only) which lives outside Resources.
// This bakes a prefab of SM_Bridge_Medieval_Stone into Assets/Resources/Bridges/ so
// the moat builder can Resources.Load<GameObject>("Bridges/Bridge_Medieval_Stone").
//
// Run once: menu 'Defenders > Seam > Generate Bridge Resources Prefab'.
// No drag-drop authoring; sourced by the FBX GUID so it survives renames.
// =============================================================================
using UnityEngine;
using UnityEditor;

namespace DeNelle.Editor
{
    public static class BridgePrefabGenerator
    {
        // GUID of Assets/polyperfect/.../Meshes_M/Medieval_M/SM_Bridge_Medieval_Stone.fbx
        private const string FbxGuid   = "9acab0fc6f34b4030b853712945a7b05";
        private const string OutFolder = "Assets/Resources/Bridges";
        private const string OutPath   = "Assets/Resources/Bridges/Bridge_Medieval_Stone.prefab";

        [MenuItem("Defenders/Seam/Generate Bridge Resources Prefab")]
        public static void Generate()
        {
            string fbxPath = AssetDatabase.GUIDToAssetPath(FbxGuid);
            if (string.IsNullOrEmpty(fbxPath))
            {
                Debug.LogError("[BridgePrefabGenerator] FBX GUID " + FbxGuid +
                    " not found — is the polyperfect pack imported? (Assets/polyperfect/...)");
                return;
            }

            var src = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (src == null)
            {
                Debug.LogError("[BridgePrefabGenerator] could not load FBX at " + fbxPath);
                return;
            }

            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder(OutFolder))
                AssetDatabase.CreateFolder("Assets/Resources", "Bridges");

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(src);
            if (instance == null)
            {
                Debug.LogError("[BridgePrefabGenerator] InstantiatePrefab returned null for " + fbxPath);
                return;
            }

            var prefab = PrefabUtility.SaveAsPrefabAsset(instance, OutPath);
            Object.DestroyImmediate(instance);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (prefab != null)
                Debug.Log("[BridgePrefabGenerator] Created " + OutPath + " from " + fbxPath +
                    " — CastleMoatBuilder can now Resources.Load(\"Bridges/Bridge_Medieval_Stone\").");
            else
                Debug.LogError("[BridgePrefabGenerator] SaveAsPrefabAsset failed for " + OutPath);
        }
    }
}
