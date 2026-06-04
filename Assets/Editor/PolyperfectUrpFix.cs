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

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[PolyperfectUrpFix] scanned {scanned} polyperfect material(s); converted {converted} built-in → URP/Lit.");
        }
    }
}
