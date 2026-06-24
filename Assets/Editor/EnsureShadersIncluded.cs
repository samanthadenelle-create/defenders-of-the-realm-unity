// =============================================================================
// EnsureShadersIncluded — WebGL shader-stripping durable fix.
// -----------------------------------------------------------------------------
// ROOT CAUSE: ProjectSettings/GraphicsSettings.asset's m_AlwaysIncludedShaders
// list does NOT include the URP particle shaders (-> stripped from the WebGL
// build -> Shader.Find returns null -> particles render MAGENTA, the WO-420 bug)
// nor the Unity video-decode shaders (-> build-time "Could not find material
// Hidden/VideoDecode / Hidden/VideoComposite / video decode shader pass
// YCbCr..." -> the opener VideoPlayer won't render in WebGL).
//
// Adding the wanted shaders to AlwaysIncludedShaders is the durable fix for
// BOTH symptoms. Idempotent: re-running adds nothing new. Editor-only.
// =============================================================================
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace DeNelle.Editor
{
    public static class EnsureShadersIncluded
    {
        // Shaders that MUST survive WebGL stripping. Verified by Shader.Find at
        // run time; any name that does not resolve is warned and skipped.
        static readonly string[] WantedShaderNames =
        {
            // URP core shaders loaded at runtime via Shader.Find — fixes the BUILD-only
            // crash in BattleArena.BuildBackdrop (`new Material(null)` when the unlit shader
            // is stripped) AND the magenta RuntimeSeam gate beacon (BuildGateBeacon now assigns
            // URP/Lit explicitly via Shader.Find — so this Always-Included entry is
            // belt-and-suspenders, NOT the actual fix). Both share the same strip root.
            "Universal Render Pipeline/Unlit",
            "Universal Render Pipeline/Lit",

            // URP particle shaders — fixes magenta particles (WO-420).
            "Universal Render Pipeline/Particles/Unlit",
            "Universal Render Pipeline/Particles/Lit",
            "Universal Render Pipeline/Particles/Simple Lit",

            // Unity video-decode shaders — fixes the opener VideoPlayer in WebGL.
            "Hidden/VideoDecode",
            "Hidden/VideoComposite",
            "Hidden/VideoDecodeOSX",
            "Hidden/VideoDecodeAndroid",
        };

        [MenuItem("Defenders/Build/Ensure Shaders Included")]
        public static void Run()
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset");
            if (assets == null || assets.Length == 0 || assets[0] == null)
            {
                Debug.LogError("[EnsureShadersIncluded] Could not load ProjectSettings/GraphicsSettings.asset.");
                return;
            }

            var gsObj = assets[0];
            var so = new SerializedObject(gsObj);
            var arr = so.FindProperty("m_AlwaysIncludedShaders");
            if (arr == null)
            {
                Debug.LogError("[EnsureShadersIncluded] m_AlwaysIncludedShaders property not found on GraphicsSettings.");
                return;
            }

            // Collect the shaders already in the list.
            var present = new HashSet<Object>();
            for (int i = 0; i < arr.arraySize; i++)
            {
                var element = arr.GetArrayElementAtIndex(i);
                var existing = element.objectReferenceValue;
                if (existing != null)
                    present.Add(existing);
            }

            int alreadyPresent = 0;
            var added = new List<string>();

            foreach (var name in WantedShaderNames)
            {
                var shader = Shader.Find(name);
                if (shader == null)
                {
                    Debug.LogWarning($"[EnsureShadersIncluded] Shader.Find returned null for \"{name}\" — skipping (may not exist in this project).");
                    continue;
                }

                if (present.Contains(shader))
                {
                    alreadyPresent++;
                    continue;
                }

                int index = arr.arraySize;
                arr.arraySize = index + 1;
                arr.GetArrayElementAtIndex(index).objectReferenceValue = shader;
                present.Add(shader);
                added.Add(name);
            }

            if (added.Count > 0)
            {
                so.ApplyModifiedProperties();
                AssetDatabase.SaveAssets();
            }

            var sb = new StringBuilder();
            for (int i = 0; i < added.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(added[i]);
            }
            var addedText = added.Count > 0 ? sb.ToString() : "(none)";
            Debug.Log($"[EnsureShadersIncluded] added: {addedText}; already-present: {alreadyPresent}");

            // Batchmode marker (grepped by run-unity-method.ps1 callers).
            var names = new StringBuilder();
            for (int i = 0; i < WantedShaderNames.Length; i++)
            {
                if (i > 0) names.Append(", ");
                names.Append(WantedShaderNames[i]);
            }
            Debug.Log($"ALWAYS_INCLUDED_OK :: {names}");
        }
    }
}
