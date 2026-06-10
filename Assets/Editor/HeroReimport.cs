// =============================================================================
// HeroReimport — force-reimport the Knight FBX and LOG its native material /
// texture bindings, so we can wire the CORRECT basecolor (the one matching the
// FBX's UVs) instead of the mismatched atlas that caused the speckled Knight.
// Editor-only, batchmode-runnable via run-unity-method.
// =============================================================================
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class HeroReimport
    {
        private const string KnightPath = "Assets/Resources/Heroes/Knight.fbx";

        [MenuItem("Defenders/Art/Reimport Knight FBX")]
        public static void Run()
        {
            AssetDatabase.ImportAsset(KnightPath,
                ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.Refresh();
            Debug.Log("[HeroReimport] Force-reimported " + KnightPath);

            var objs = AssetDatabase.LoadAllAssetsAtPath(KnightPath);
            int mats = 0;
            foreach (var o in objs)
            {
                var m = o as Material;
                if (m == null) continue;
                mats++;
                Texture baseTex =
                    m.HasProperty("_BaseMap")  ? m.GetTexture("_BaseMap")  :
                    m.HasProperty("_MainTex")  ? m.GetTexture("_MainTex")  : null;
                Texture normTex =
                    m.HasProperty("_BumpMap")  ? m.GetTexture("_BumpMap")  : null;
                Debug.Log("[HeroReimport] material #" + mats + " '" + m.name + "' shader='" +
                          (m.shader != null ? m.shader.name : "null") + "' baseTex='" +
                          (baseTex != null ? baseTex.name : "NULL") + "' normal='" +
                          (normTex != null ? normTex.name : "NULL") + "'");
            }
            if (mats == 0) Debug.Log("[HeroReimport] FBX exposed NO Material sub-assets (materials not extracted).");
            Debug.Log("[HeroReimport] DONE — " + mats + " material(s).");
        }
    }
}
