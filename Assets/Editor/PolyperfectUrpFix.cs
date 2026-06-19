// =============================================================================
// PolyperfectUrpFix — converts the Low Poly Ultimate Pack (polyperfect) materials
// from built-in shaders to URP/Lit so they stop rendering magenta in this URP
// project. Headless equivalent of importing URP_LowPolyUltimatePack.unitypackage.
// -----------------------------------------------------------------------------
// The pack ships with built-in (Standard) materials; in URP those render pink.
// This scans every material under Assets/polyperfect and, for any on a built-in /
// error shader, swaps it to URP/Lit, carrying over base colour + main texture +
// emission. Materials already on URP are left alone. In-place edit (same material
// GUIDs), so the baked Village buildings render correctly without a re-bake.
//
// WO-323 (WHITE TREES) — second pass, ReimportTreeFbxs():
//   The polyperfect tree FBX files import with NO usable material, so every tree
//   renderer slot is NULL, which URP draws as flat WHITE — the exact WO-323
//   symptom. The shared atlas material M_Atlas_LPUP.mat is ALREADY correct
//   (URP/Lit + _BaseMap wired to the pack atlas). The REAL fix is the supported
//   Unity-6000 import path in PolyperfectTreePostprocessor (OnPreprocessModel sets
//   ImportViaMaterialDescription; OnAssignMaterialModel returns M_Atlas_LPUP so
//   Unity binds the shared atlas in-place). The old External material-location +
//   AddRemap remap is DEAD in U6 ("MaterialLocation.External is obsolete ... no
//   longer supported") and did NOT bind the atlas — it has been removed. This
//   menu now simply FORCE-REIMPORTS the SM_Tree* FBXs so the postprocessor runs
//   on demand and rebinds the atlas at IMPORT level (no re-bake, no runtime
//   band-aid).
//
// polyperfect is gitignored → this is a LOCAL editor op (re-run after re-importing
// the pack on a fresh clone, same as importing the pack's own URP unitypackage).
//
// Run: -executeMethod DeNelle.Editor.PolyperfectUrpFix.Fix
// =============================================================================

using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class PolyperfectUrpFix
    {
        private const string Root = "Assets/polyperfect";

        [MenuItem("Defenders/Art/Fix Polyperfect URP Materials")]
        public static void Fix()
        {
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null)
            {
                Debug.LogError("[PolyperfectUrpFix] 'Universal Render Pipeline/Lit' shader not found — is URP installed?");
                return;
            }

            string[] guids = AssetDatabase.FindAssets("t:Material", new[] { Root });
            int scanned = 0, converted = 0;

            foreach (var g in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(g);
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null) continue;
                scanned++;

                string sn = mat.shader != null ? mat.shader.name : "";
                bool builtIn = mat.shader == null
                            || sn == "Standard"
                            || sn.StartsWith("Legacy Shaders/")
                            || sn.Contains("InternalErrorShader")
                            || sn == "Standard (Specular setup)";
                if (!builtIn) continue;   // already URP / a custom shader → leave it

                // Capture the legacy properties before the shader swap drops them.
                Color baseCol = mat.HasProperty("_Color") ? mat.GetColor("_Color") : Color.white;
                Texture mainTex = mat.HasProperty("_MainTex") ? mat.GetTexture("_MainTex") : null;
                Color emis = mat.HasProperty("_EmissionColor") ? mat.GetColor("_EmissionColor") : Color.black;

                mat.shader = lit;
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", baseCol);
                if (mainTex != null && mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", mainTex);
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.1f);
                if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
                if (emis.maxColorComponent > 0.01f)
                {
                    mat.EnableKeyword("_EMISSION");
                    if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", emis);
                    mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                }

                EditorUtility.SetDirty(mat);
                converted++;
            }

            // WO-323: force-reimport the white tree FBXs so the supported U6
            // postprocessor (PolyperfectTreePostprocessor) rebinds the atlas.
            int treesReimported = ReimportTreeFbxs();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[PolyperfectUrpFix] scanned {scanned} polyperfect material(s); converted {converted} built-in → URP/Lit; " +
                      $"force-reimported {treesReimported} tree FBX(s) → PolyperfectTreePostprocessor rebinds M_Atlas_LPUP (WO-323).");
        }

        // ── WO-323: force-reimport the white tree FBXs ───────────────────────────
        // The REAL fix is PolyperfectTreePostprocessor (supported U6 import path).
        // This just force-reimports every SM_Tree* FBX under the pack so that
        // postprocessor runs on demand and rebinds M_Atlas_LPUP at import level.
        // The old obsolete External material-location + AddRemap remap is removed.
        // Returns the number of tree FBXs reimported.
        private static int ReimportTreeFbxs()
        {
            // Refresh the postprocessor's atlas cache so it re-resolves the material
            // (e.g. after the pack was just (re)imported on a fresh clone).
            PolyperfectTreePostprocessor.InvalidateAtlasCache();

            string[] modelGuids = AssetDatabase.FindAssets("t:Model", new[] { Root });
            int reimported = 0;

            try
            {
                AssetDatabase.StartAssetEditing();
                foreach (var g in modelGuids)
                {
                    string fbxPath = AssetDatabase.GUIDToAssetPath(g);
                    if (string.IsNullOrEmpty(fbxPath)) continue;

                    string file = System.IO.Path.GetFileNameWithoutExtension(fbxPath);
                    if (file == null || !file.StartsWith("SM_Tree", System.StringComparison.OrdinalIgnoreCase))
                        continue;   // trees only

                    AssetDatabase.ImportAsset(fbxPath, ImportAssetOptions.ForceUpdate);
                    reimported++;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
            return reimported;
        }
    }
}
