// =============================================================================
// SupercyanUrpMaterialFix — converts the Supercyan pack materials from their
// Built-in shaders (which render MAGENTA in our URP project) to URP/Lit, matte,
// preserving each material's albedo texture + tint color.
//
// WHY (from the Supercyan URP readme + a live check): the pack ships materials on
// Built-in shaders (m_Shader fileID 46) and the custom "SupercyanShader" is a
// surface shader (Built-in-only) — neither renders in URP, so the SC_Footman/
// SC_Archer troop bodies show magenta. The readme's fix is the Render Pipeline
// Converter (Material Upgrade); this does the equivalent deterministically in
// batchmode (the converter window is unreliable headless): swap to URP/Lit and
// remap _MainTex->_BaseMap, _Color->_BaseColor, Smoothness/Metallic=0 (matte, so
// the flat low-poly look reads close to the original cel style — refine at polish).
//
// Idempotent: materials already on URP/Lit are skipped. Touches ONLY
// Assets/Supercyan/Materials.
//
// Batchmode: DeNelle.Editor.SupercyanUrpMaterialFix.Run
// Menu:      Defenders/Art/Fix Supercyan URP Materials
// =============================================================================
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class SupercyanUrpMaterialFix
    {
        private const string MaterialRoot = "Assets/Supercyan/Materials";

        [MenuItem("Defenders/Art/Fix Supercyan URP Materials")]
        public static void Run()
        {
            var urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null)
            {
                Debug.LogError("[SupercyanUrpMaterialFix] 'Universal Render Pipeline/Lit' shader not found — " +
                               "is the URP package present? Aborting.");
                return;
            }

            var guids = AssetDatabase.FindAssets("t:Material", new[] { MaterialRoot });
            int converted = 0, already = 0, noAlbedo = 0;
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null) continue;
                if (mat.shader == urpLit) { already++; continue; }

                // Capture the original albedo + tint BEFORE swapping (shader change drops
                // properties whose names don't carry over).
                Texture albedo = null;
                if (mat.HasProperty("_MainTex")) albedo = mat.GetTexture("_MainTex");
                if (albedo == null && mat.HasProperty("_BaseMap")) albedo = mat.GetTexture("_BaseMap");

                Color tint = Color.white;
                if (mat.HasProperty("_Color")) tint = mat.GetColor("_Color");
                else if (mat.HasProperty("_BaseColor")) tint = mat.GetColor("_BaseColor");

                mat.shader = urpLit;

                if (albedo != null)
                {
                    if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", albedo);
                    if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", albedo);
                }
                else noAlbedo++;

                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", tint);
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0f); // matte (no PBR shine)
                if (mat.HasProperty("_Metallic"))   mat.SetFloat("_Metallic", 0f);

                EditorUtility.SetDirty(mat);
                converted++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[SupercyanUrpMaterialFix] converted {converted} material(s) to URP/Lit " +
                      $"({already} already URP, {noAlbedo} had no albedo to carry) under {MaterialRoot}.");
        }
    }
}
