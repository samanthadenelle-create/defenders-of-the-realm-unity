// =============================================================================
// UniversalAssetOptimizer (WO-93) - project-wide IMPORT-SETTINGS optimizer for
// FBX + textures: smaller builds, lower runtime memory, WebGL-friendly formats.
// -----------------------------------------------------------------------------
// NOTE ON SCOPE: this changes ASSET IMPORT SETTINGS, which shrink the BUILD /
// Library / runtime footprint. It does NOT shrink the source .fbx / .tga bytes
// on disk, so it does NOT reduce the git/repo size of the source assets.
//
// Three deliberate guardrails vs the original work-order script (owner-approved):
//   1. OptimizeAllAssets is public static + settings are static, so it runs both
//      from the window button AND headless (batchmode -executeMethod).
//   2. importTangents left at its default (NOT set to None) - stripping tangents
//      breaks normal-mapping on every URP/Lit material; the size win is trivial.
//   3. The sweep is scoped to "Assets/" (not Packages) so it never recompresses
//      read-only package textures (TMP / URP) and corrupts them.
//
// Run: Defenders -> Optimize All Assets (FBX + Textures), or headless via
//      run-unity-method.ps1 -Method UniversalAssetOptimizer.OptimizeAllAssets.
// =============================================================================

using UnityEngine;
using UnityEditor;

public class UniversalAssetOptimizer : EditorWindow
{
    // Static so the optimization can run headless (batchmode) as well as from the
    // window. Defaults match the work order.
    private static bool optimizeTextures = true;
    private static bool optimizeFBX = true;
    private static int maxTextureSize = 2048;
    private static int compressionQuality = 70;

    [MenuItem("Defenders/Optimize All Assets (FBX + Textures)")]
    public static void ShowWindow()
    {
        GetWindow<UniversalAssetOptimizer>("Universal Optimizer");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Universal Asset Optimizer", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Optimizes FBX and Textures across Assets/ (import settings only).\n" +
                                "Shrinks the BUILD / runtime / WebGL footprint, not the source files.\n" +
                                "Safe to run multiple times.", MessageType.Info);

        optimizeTextures = EditorGUILayout.Toggle("Optimize Textures", optimizeTextures);
        optimizeFBX = EditorGUILayout.Toggle("Optimize FBX Models", optimizeFBX);
        maxTextureSize = EditorGUILayout.IntSlider("Max Texture Size", maxTextureSize, 512, 4096);
        compressionQuality = EditorGUILayout.IntSlider("Compression Quality", compressionQuality, 0, 100);

        if (GUILayout.Button("RUN OPTIMIZATION ON ALL ASSETS", GUILayout.Height(50)))
        {
            if (EditorUtility.DisplayDialog("Confirm Optimization",
                "This will reimport and compress ALL FBX and Textures in Assets/.\n\nThis may take 5-15 minutes.\n\nContinue?",
                "Yes, Optimize", "Cancel"))
            {
                OptimizeAllAssets();
            }
        }
    }

    public static void OptimizeAllAssets()
    {
        Debug.Log("=== Starting Universal Asset Optimization ===");

        int texCount = 0;
        int fbxCount = 0;

        // ── Textures ─────────────────────────────────────────────────────────
        if (optimizeTextures)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.Contains("/Editor/") || path.Contains("/Resources/Fonts/")) continue;

                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;

                importer.maxTextureSize = maxTextureSize;
                importer.textureCompression = TextureImporterCompression.Compressed;
                importer.crunchedCompression = true;
                importer.compressionQuality = compressionQuality;
                importer.isReadable = false;

                // WebGL-friendly platform override (ASTC). overridden=true so it sticks.
                importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings
                {
                    name = "WebGL",
                    overridden = true,
                    maxTextureSize = maxTextureSize,
                    format = TextureImporterFormat.ASTC_6x6,
                    compressionQuality = compressionQuality
                });

                importer.SaveAndReimport();
                texCount++;
            }
        }

        // ── FBX models ───────────────────────────────────────────────────────
        if (optimizeFBX)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { "Assets" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null) continue;

                importer.meshCompression = ModelImporterMeshCompression.High;
                // optimizeMesh=true equates to both of these true (per Unity's deprecation note).
                importer.optimizeMeshPolygons = true;
                importer.optimizeMeshVertices = true;
                importer.importBlendShapes = false;
                importer.importCameras = false;
                importer.importLights = false;
                importer.importVisibility = false;
                // importTangents intentionally NOT set to None (preserves normal maps).

                importer.SaveAndReimport();
                fbxCount++;
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // Machine-greppable marker for the batchmode log, plus the human summary.
        Debug.Log($"OPTIMIZE_DONE textures={texCount} fbx={fbxCount}");
        Debug.Log($"✅ Optimization Complete!\n" +
                  $"Textures processed: {texCount}\n" +
                  $"FBX models processed: {fbxCount}\n" +
                  $"Build/runtime footprint reduced (source files on disk unchanged).");
    }
}
