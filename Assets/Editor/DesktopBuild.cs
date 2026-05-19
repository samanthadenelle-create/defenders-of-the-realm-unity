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

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = exePath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
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
