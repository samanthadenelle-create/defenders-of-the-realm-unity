// =============================================================================
// MoatMaterialGenerator — one-shot editor step that BAKES the committed moat
// materials (mirrors HedgePrefabGenerator / BridgePrefabGenerator).
// -----------------------------------------------------------------------------
// WHY (WO-605): CastleMoatBuilder runs at runtime ([RuntimeInitializeOnLoadMethod])
// and built the moat WATER + bridge STONE materials via runtime Shader.Find(
// "Universal Render Pipeline/Lit"). In a WebGL build no committed ASSET references
// that shader, so the build STRIPS it -> Shader.Find returns null at runtime ->
// water renders invisible and the bridge ships Unity-default WHITE (the owner's
// WO-605 symptom). Committing .mat assets that reference URP/Lit PULLS the shader
// INTO the build; CastleMoatBuilder.ResolveMaterial then Resources.Loads them and
// uses runtime Shader.Find only as a last-resort fallback.
//
// Run once: menu 'Defenders > Seam > Generate Moat Materials'. Idempotent (overwrites
// the two assets in place). Editor-only (DeNelle.Editor). No drag-drop authoring.
//
// Produces (loaded by CastleMoatBuilder.WaterMaterialResource / StoneMaterialResource):
//   Assets/Resources/Materials/Moat/MoatWater.mat   — URP/Lit OPAQUE solid blue (owner pivot: a
//       filled contained-basin channel, always visible — no transparent edge-on-invisible plane)
//   Assets/Resources/Materials/Moat/BridgeStone.mat — URP/Lit OPAQUE stone grey (also tinted for
//       the basin lip rim + the natural bank berms via ResolveMaterial instancing)
// =============================================================================
using UnityEngine;
using UnityEditor;

namespace DeNelle.Editor
{
    public static class MoatMaterialGenerator
    {
        private const string OutFolder = "Assets/Resources/Materials/Moat";
        private const string WaterPath = "Assets/Resources/Materials/Moat/MoatWater.mat";
        private const string StonePath = "Assets/Resources/Materials/Moat/BridgeStone.mat";

        // Base shades (CastleMoatBuilder stamps the per-band shade on an INSTANCE at runtime;
        // these committed base colours are the sensible defaults / the shader-carrier).
        private static readonly Color WaterColor = new Color(0.10f, 0.30f, 0.62f, 1f);   // solid opaque blue
        private static readonly Color StoneColor = new Color(0.55f, 0.55f, 0.57f);        // stone grey

        [MenuItem("Defenders/Seam/Generate Moat Materials")]
        public static void Generate()
        {
            var sh = Shader.Find("Universal Render Pipeline/Lit");
            if (sh == null) sh = Shader.Find("Standard");
            if (sh == null)
            {
                Debug.LogWarning("[MoatMaterialGenerator] neither URP/Lit nor Standard shader found — skipped.");
                return;
            }

            EnsureFolder();

            // --- WATER (OPAQUE solid blue — owner pivot: a filled basin channel, always visible) ---
            var water = new Material(sh) { name = "MoatWater" };
            if (water.HasProperty("_BaseColor")) water.SetColor("_BaseColor", WaterColor);
            if (water.HasProperty("_Color"))     water.SetColor("_Color", WaterColor);
            if (water.HasProperty("_Surface"))   water.SetFloat("_Surface", 0f); // Opaque
            water.SetOverrideTag("RenderType", "Opaque");
            if (water.HasProperty("_ZWrite")) water.SetFloat("_ZWrite", 1f);
            water.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry;
            if (water.HasProperty("_Smoothness")) water.SetFloat("_Smoothness", 0.25f); // de-glossed sheen
            WriteMaterial(water, WaterPath);

            // --- BRIDGE STONE (opaque grey) ---
            var stone = new Material(sh) { name = "BridgeStone" };
            if (stone.HasProperty("_BaseColor")) stone.SetColor("_BaseColor", StoneColor);
            if (stone.HasProperty("_Color"))     stone.SetColor("_Color", StoneColor);
            if (stone.HasProperty("_Smoothness")) stone.SetFloat("_Smoothness", 0.15f);
            WriteMaterial(stone, StonePath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[MoatMaterialGenerator] baked committed moat materials:\n  " + WaterPath + "\n  " + StonePath +
                "\n(WebGL-safe: URP/Lit shader is now pulled into the build; CastleMoatBuilder.ResolveMaterial loads these).");
        }

        private static void EnsureFolder()
        {
            if (AssetDatabase.IsValidFolder(OutFolder)) return;
            if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder("Assets/Resources/Materials")) AssetDatabase.CreateFolder("Assets/Resources", "Materials");
            if (!AssetDatabase.IsValidFolder(OutFolder)) AssetDatabase.CreateFolder("Assets/Resources/Materials", "Moat");
        }

        private static void WriteMaterial(Material mat, string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                existing.CopyPropertiesFromMaterial(mat);
                EditorUtility.SetDirty(existing);
            }
            else
            {
                AssetDatabase.CreateAsset(mat, path);
            }
        }
    }
}
