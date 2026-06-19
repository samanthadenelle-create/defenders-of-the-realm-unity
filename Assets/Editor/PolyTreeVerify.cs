// =============================================================================
// PolyTreeVerify — WO-323 diagnostic. Loads sample polyperfect tree FBXs and
// reports their ACTUAL imported renderer materials (null = renders white,
// atlas = fixed). Confirms whether RemapTreeFbxToAtlas actually bound materials
// under Unity 6000.4 (where MaterialLocation.External is obsolete/unsupported).
// Throwaway diagnostic — not shipped logic.
// =============================================================================
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class PolyTreeVerify
    {
        public static void Run()
        {
            string[] samples =
            {
                "Assets/polyperfect/Low Poly Ultimate Pack/_T/Meshes_T/Nature_T/Trees_T/SM_Tree_Forest_White.fbx",
                "Assets/polyperfect/Low Poly Ultimate Pack/_T/Meshes_T/Nature_T/Trees_T/SM_Tree_Birch_White.fbx",
                "Assets/polyperfect/Low Poly Ultimate Pack/_M/Meshes_M/Nature_M/Trees_M/SM_Tree_Forest_White.fbx",
            };

            foreach (var p in samples)
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                if (go == null)
                {
                    Debug.LogWarning($"[PolyTreeVerify] no GameObject at {p}");
                    continue;
                }

                int renderers = 0, nullMat = 0, atlas = 0, other = 0;
                foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                {
                    renderers++;
                    foreach (var m in r.sharedMaterials)
                    {
                        if (m == null) nullMat++;
                        else if (m.name.Contains("Atlas_LPUP")) atlas++;
                        else other++;
                    }
                }

                string verdict = (nullMat == 0 && atlas > 0) ? "FIXED (atlas bound)"
                               : (nullMat > 0) ? "STILL WHITE (null material slots)"
                               : "OTHER material (not atlas, not null)";
                Debug.Log($"[PolyTreeVerify] {System.IO.Path.GetFileName(p)}: renderers={renderers} " +
                          $"nullMat={nullMat} atlas={atlas} other={other} -> {verdict}");
            }
        }
    }
}
