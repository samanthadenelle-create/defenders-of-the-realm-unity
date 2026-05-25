// =============================================================================
// DesktopBuild — produces a Windows x64 standalone player for play-testing and
// the Solana grant demo. Run headless:
//
//   Unity.exe -batchmode -quit -buildTarget Win64 -projectPath <proj> \
//             -executeMethod DeNelle.Editor.DesktopBuild.BuildWindows
//
// Output: <proj>/Builds/Windows/DefendersOfTheRealm.exe
// The Builds/ folder is gitignored — build output is never committed.
// =============================================================================

using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace DeNelle.Editor
{
    /// <summary>
    /// One-shot Windows-standalone build entry point. Builds whatever scenes
    /// are enabled in Build Settings, in order, to <c>Builds/Windows/</c>.
    /// </summary>
    public static class DesktopBuild
    {
        private const string OutputDir = "Builds/Windows";
        private const string ExeName = "DefendersOfTheRealm.exe";

        [MenuItem("Defenders/Build/Windows x64 Player")]
        public static void BuildWindows()
        {
            string[] scenes = EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => s.path)
                .ToArray();

            if (scenes.Length == 0)
            {
                Debug.LogError("[DesktopBuild] No enabled scenes in Build Settings — aborting.");
                EditorApplication.Exit(1);
                return;
            }

            string dir = Path.GetFullPath(OutputDir);
            Directory.CreateDirectory(dir);
            string exePath = Path.Combine(dir, ExeName);

            Debug.Log($"[DesktopBuild] Building {scenes.Length} scene(s) -> {exePath}");
            foreach (string s in scenes)
                Debug.Log($"[DesktopBuild]   scene: {s}");

            // Crash mitigation (2026-05-25): the WO-26 village carries ~5000+
            // BatchingStatic tiles. Static Batching combines them at build time, and
            // that combined-mesh serialization produced a CORRUPT level3 — the player
            // hard-crashes on Village load with "The file 'level3' is corrupted! …
            // [Position out of bounds!]". Disable Static Batching for the build so the
            // scene serializes per-renderer (GPU instancing on the materials still
            // collapses draw calls). SetBatchingForPlatform is historically internal,
            // so reach it by reflection.
            try
            {
                var setBatching = typeof(PlayerSettings).GetMethod("SetBatchingForPlatform",
                    System.Reflection.BindingFlags.Static
                    | System.Reflection.BindingFlags.Public
                    | System.Reflection.BindingFlags.NonPublic);
                if (setBatching != null)
                {
                    // Overload takes BuildTarget (not BuildTargetGroup): (BuildTarget, staticBatching, dynamicBatching)
                    setBatching.Invoke(null, new object[] { BuildTarget.StandaloneWindows64, 0, 1 }); // static OFF, dynamic ON
                    Debug.Log("[DesktopBuild] Static Batching DISABLED for StandaloneWindows64 (level3-corruption mitigation).");
                }
                else
                {
                    Debug.LogWarning("[DesktopBuild] PlayerSettings.SetBatchingForPlatform not found — static batching left unchanged.");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[DesktopBuild] Could not toggle static batching: {e.Message}");
            }

            // D3D12 upload-buffer crash mitigation (2026-05-25): Village instantiates
            // raw un-decimated Tripo structure meshes (Cathedral 84MB, PetHome 54MB,
            // LumberMill 52MB, Forge/Farm 29MB). A single >35MB mesh overflows the
            // D3D12 staging upload heap ("d3d12: upload buffer was too small for the
            // requested resource!"), which cascades into a corrupt level3 / "Position
            // out of bounds!" hard-crash on Village load. Force Direct3D11 for the
            // standalone player — its mesh upload path has no equivalent fixed staging
            // limit. (Real long-term fix is to decimate those meshes.)
            try
            {
                PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
                PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64,
                    new[] { UnityEngine.Rendering.GraphicsDeviceType.Direct3D11 });
                Debug.Log("[DesktopBuild] Graphics API forced to Direct3D11 for StandaloneWindows64 (D3D12 upload-buffer crash mitigation).");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[DesktopBuild] Could not force Direct3D11 graphics API: {e.Message}");
            }

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = exePath,
                target = BuildTarget.StandaloneWindows64,
                // Development build so the DevTools QA panel (gear / F1: force-wave,
                // grant-materials) compiles in — it is gated #if DEVELOPMENT_BUILD.
                options = BuildOptions.Development,
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[DesktopBuild] SUCCEEDED — {summary.totalSize / (1024 * 1024)} MB in {summary.totalTime}.");
            }
            else
            {
                Debug.LogError($"[DesktopBuild] FAILED — result={summary.result}, " +
                               $"errors={summary.totalErrors}.");
                EditorApplication.Exit(1);
            }
        }
    }
}
