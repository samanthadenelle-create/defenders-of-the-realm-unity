// =============================================================================
// LanaUrpMaterialFix — F8-49 root fix (SME dossier docs/SME/VFX_PACKS_SME.md §2).
//
// RCA (data): all 19 Lana Studio "Casual RPG VFX" particle materials sit on the
// BUILT-IN legacy shaders Particles/Additive (fileID 10720) / Particles/Alpha
// Blended-ish variants (10721) — under URP every raw prefab renders
// Hidden/InternalErrorShader magenta, and the game only looked right because
// VFXManager.ProofUrpParticleShaders re-materials EVERY spawned instance at
// runtime (VFXManager.cs:596). The vendor's URP upgrade package was never
// imported and is gitignored — the canonical "half-upgraded pack" state.
//
// Fix: upgrade the SOURCE materials in place, once, with the SAME blend logic
// the runtime proof uses (additive vs alpha from the legacy shader name,
// texture → _BaseMap, tint → _BaseColor, transparent surface, no ZWrite,
// transparent queue). In-place shader swap keeps material GUIDs, so every
// prefab stays wired. Idempotent: URP materials are skipped.
//
// Deliberately NOT the generic MagentaMaterialFixer sweep — that converts to
// opaque URP/Lit and would deaden the additive glows (dossier fix-rank note).
//
// Batchmode: -executeMethod DeNelle.Editor.LanaUrpMaterialFix.Run
// Success marker: LANA_URP_FIX_OK <n> upgraded / <m> skipped
// =============================================================================

using System;
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class LanaUrpMaterialFix
    {
        private const string Root = "Assets/Lana Studio";
        private const string UrpParticlesUnlit = "Universal Render Pipeline/Particles/Unlit";
        private const string Log = "[LanaUrpFix] ";

        // UnityEngine.Rendering.BlendMode values (mirrors VFXManager.cs:584-586).
        private const int BLEND_ONE                 = 1;
        private const int BLEND_SRC_ALPHA           = 5;
        private const int BLEND_ONE_MINUS_SRC_ALPHA = 10;

        [MenuItem("Defenders/VFX/Fix Lana Studio URP Materials (F8-49)")]
        public static void Run()
        {
            var urp = Shader.Find(UrpParticlesUnlit);
            if (urp == null)
            {
                Debug.LogError(Log + $"'{UrpParticlesUnlit}' shader not found — URP not active? Aborting, nothing touched.");
                return;
            }

            if (!AssetDatabase.IsValidFolder(Root))
            {
                Debug.LogError(Log + $"'{Root}' not found — pack missing. Aborting.");
                return;
            }

            int upgraded = 0, skipped = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { Root }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m == null) continue;

                if (!IsLegacyParticleShader(m.shader))
                {
                    skipped++;
                    continue;
                }

                // Capture the authored look BEFORE the shader swap loses property slots.
                string legacyName = m.shader != null ? m.shader.name : "<null>";
                bool additive = legacyName.IndexOf("Additive", StringComparison.OrdinalIgnoreCase) >= 0
                    || (legacyName.IndexOf("Alpha", StringComparison.OrdinalIgnoreCase) < 0);   // default additive (glows)
                Texture mainTex = m.HasProperty("_MainTex") ? m.GetTexture("_MainTex")
                                 : m.HasProperty("_BaseMap") ? m.GetTexture("_BaseMap") : null;
                Color tint = m.HasProperty("_TintColor") ? m.GetColor("_TintColor")
                           : m.HasProperty("_Color") ? m.GetColor("_Color") : Color.white;

                m.shader = urp;

                // Same blend recipe as VFXManager.ConfigureUrpParticleBlend (VFXManager.cs:738).
                if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);           // transparent
                if (m.HasProperty("_Blend"))   m.SetFloat("_Blend", additive ? 2f : 0f);
                if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", BLEND_SRC_ALPHA);
                if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", additive ? BLEND_ONE : BLEND_ONE_MINUS_SRC_ALPHA);
                if (m.HasProperty("_ZWrite"))   m.SetFloat("_ZWrite", 0f);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                m.DisableKeyword("_ALPHAMODULATE_ON");
                m.SetOverrideTag("RenderType", "Transparent");
                m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

                if (mainTex != null && m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", mainTex);
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", tint);

                EditorUtility.SetDirty(m);
                upgraded++;
                Debug.Log(Log + $"upgraded '{path}' ({legacyName} -> URP Particles/Unlit, " +
                    $"{(additive ? "ADDITIVE" : "ALPHA")}, tex={(mainTex != null ? mainTex.name : "<none>")}).");
            }

            AssetDatabase.SaveAssets();
            Debug.Log(Log + $"LANA_URP_FIX_OK {upgraded} upgraded / {skipped} skipped (already URP or non-particle).");
        }

        /// <summary>Mirror of VFXManager.IsLegacyParticleShader (VFXManager.cs:701).</summary>
        private static bool IsLegacyParticleShader(Shader sh)
        {
            if (sh == null) return true;
            string n = sh.name ?? string.Empty;
            if (n.IndexOf("Universal Render Pipeline", StringComparison.Ordinal) >= 0) return false;
            if (n == "Hidden/InternalErrorShader") return true;
            if (n.StartsWith("Legacy Shaders/", StringComparison.Ordinal)) return true;
            if (n.IndexOf("Particles/", StringComparison.Ordinal) >= 0) return true;
            return false;
        }
    }
}
