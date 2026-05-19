// =============================================================================
// UrpActivator — makes URP the ACTIVE render pipeline.
// -----------------------------------------------------------------------------
// The project was created with the Built-in pipeline (Unity CLI -createProject)
// and the URP package was added to manifest.json afterwards — but no
// UniversalRenderPipelineAsset was ever created or assigned. Result: the project
// kept running Built-in, so every URP shader (the KayKit models, the URP Terrain,
// the village/exterior materials) rendered magenta.
//
// This creates a UniversalRenderPipelineAsset + Universal Renderer under
// Assets/Settings/ and assigns it to GraphicsSettings + every Quality level, so
// URP is genuinely active. Run once:
//   -executeMethod DeNelle.Editor.UrpActivator.ActivateUrp
// Idempotent — reuses the assets if they already exist.
// =============================================================================

using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DeNelle.Editor
{
    /// <summary>Creates and assigns the URP pipeline asset so URP is active.</summary>
    public static class UrpActivator
    {
        private const string SettingsDir = "Assets/Settings";
        private const string RendererPath = SettingsDir + "/DeNelle-UniversalRenderer.asset";
        private const string PipelinePath = SettingsDir + "/DeNelle-URP.asset";

        [MenuItem("Defenders/Setup/Activate URP")]
        public static void ActivateUrp()
        {
            if (!AssetDatabase.IsValidFolder(SettingsDir))
                AssetDatabase.CreateFolder("Assets", "Settings");

            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (pipeline == null)
            {
                var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
                if (rendererData == null)
                {
                    rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
                    AssetDatabase.CreateAsset(rendererData, RendererPath);
                }

                pipeline = UniversalRenderPipelineAsset.Create(rendererData);
                pipeline.name = "DeNelle-URP";
                AssetDatabase.CreateAsset(pipeline, PipelinePath);
                AssetDatabase.SaveAssets();
            }

            GraphicsSettings.defaultRenderPipeline = pipeline;

            int original = QualitySettings.GetQualityLevel();
            int levels = QualitySettings.names.Length;
            for (int i = 0; i < levels; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = pipeline;
            }
            QualitySettings.SetQualityLevel(original, false);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[UrpActivator] URP is now the ACTIVE render pipeline — pipeline asset " +
                      $"assigned to GraphicsSettings.defaultRenderPipeline + {levels} quality levels.");
        }
    }
}
