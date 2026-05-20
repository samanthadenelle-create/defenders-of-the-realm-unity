// =============================================================================
// RoundedBubbleShaderRegistrar — ensures DeNelle/UI/RoundedChatBubble is
// INCLUDED in the player build.
// -----------------------------------------------------------------------------
// Bug 2026-05-20 (owner screenshot): TownsfolkBubble's panel rendered as a
// square box in the Windows player. Root cause: every reference to the
// rounded shader is via Shader.Find at runtime, so Unity stripped it from the
// build. This editor utility:
//   • Adds "DeNelle/UI/RoundedChatBubble" to GraphicsSettings.alwaysIncludedShaders
//     (so the player build always packs it).
//   • Creates Assets/Resources/Materials/RoundedChatBubble.mat referencing
//     the shader (a Resources/ material is the belt-and-suspenders path — even
//     if AlwaysIncluded gets edited away, a Resources material drags the
//     shader along).
// Idempotent — re-runs cleanly. Entry point: Defenders → Build → Register
// Rounded Bubble Shader.
// =============================================================================

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace DeNelle.Editor
{
    public static class RoundedBubbleShaderRegistrar
    {
        private const string ShaderName = "DeNelle/UI/RoundedChatBubble";
        private const string MaterialPath = "Assets/Resources/Materials/RoundedChatBubble.mat";

        [MenuItem("Defenders/Build/Register Rounded Bubble Shader")]
        public static void RegisterFromMenu() => Register();

        public static void Register()
        {
            var shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                Debug.LogError($"[RoundedBubbleShaderRegistrar] Shader '{ShaderName}' not found. " +
                               "Make sure Assets/Shaders/RoundedChatBubble.shader exists and compiles.");
                return;
            }

            EnsureInAlwaysIncluded(shader);
            EnsureSentinelMaterial(shader);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[RoundedBubbleShaderRegistrar] Shader registered. Player build will now include it.");
        }

        private static void EnsureInAlwaysIncluded(Shader shader)
        {
            var settings = GraphicsSettings.GetGraphicsSettings();
            var so = new SerializedObject(settings);
            var arr = so.FindProperty("m_AlwaysIncludedShaders");
            if (arr == null)
            {
                Debug.LogError("[RoundedBubbleShaderRegistrar] m_AlwaysIncludedShaders not found on GraphicsSettings.");
                return;
            }

            for (int i = 0; i < arr.arraySize; i++)
            {
                var el = arr.GetArrayElementAtIndex(i);
                if (el != null && el.objectReferenceValue == shader) return; // already present
            }

            int idx = arr.arraySize;
            arr.InsertArrayElementAtIndex(idx);
            arr.GetArrayElementAtIndex(idx).objectReferenceValue = shader;
            so.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log("[RoundedBubbleShaderRegistrar] Added shader to AlwaysIncludedShaders.");
        }

        private static void EnsureSentinelMaterial(Shader shader)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(MaterialPath)!);
            // If a stub file exists (e.g. the prior empty .mat), wipe it so
            // CreateAsset doesn't refuse.
            if (File.Exists(MaterialPath))
            {
                var existing = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
                if (existing != null)
                {
                    if (existing.shader != shader)
                    {
                        existing.shader = shader;
                        EditorUtility.SetDirty(existing);
                    }
                    return;
                }
                // not a valid material — delete and recreate
                AssetDatabase.DeleteAsset(MaterialPath);
            }

            var mat = new Material(shader) { name = "RoundedChatBubble" };
            mat.SetColor("_BaseColor", new Color(1f, 0.99f, 0.96f, 0.94f));
            mat.SetFloat("_Radius", 0.22f);
            mat.SetFloat("_Aspect", 2.6f);
            AssetDatabase.CreateAsset(mat, MaterialPath);
            Debug.Log($"[RoundedBubbleShaderRegistrar] Created sentinel material at {MaterialPath}.");
        }
    }
}
