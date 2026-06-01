// =============================================================================
// WebGLBuild - produces a WebGL (browser) build for playtest delivery. Run
// headless:
//
//   Unity.exe -batchmode -quit -buildTarget WebGL -projectPath <proj> \
//             -executeMethod DeNelle.Editor.WebGLBuild.BuildWebGL
//
// Output: <proj>/Builds/WebGL/ (index.html + Build/ + StreamingAssets/).
// Mirrors DesktopBuild.cs (WO-09 section 2.1). Applies the WebGL Player Settings
// the WO calls for: IL2CPP (mandatory for WebGL), Brotli compression, 512 MB
// memory, no exception support (release), data caching, minimal managed
// stripping.
//
// NOTE: this script COMPILES without the "WebGL Build Support" editor module
// (the PlayerSettings.WebGL / BuildTarget.WebGL APIs live in UnityEditor.dll),
// but BuildPipeline.BuildPlayer(WebGL) will FAIL at runtime until that module is
// installed via Unity Hub. So this is ready-to-build infrastructure; the actual
// build is gated on the module install (see WORK_ORDER_09_webgl_build.RESULT.md).
// =============================================================================

using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace DeNelle.Editor
{
    /// <summary>One-shot WebGL build entry point. Builds the enabled Build-Settings
    /// scenes, in order, to <c>Builds/WebGL/</c>.</summary>
    public static class WebGLBuild
    {
        private const string OutputDir = "Builds/WebGL";

        [MenuItem("Defenders/Build/WebGL Player")]
        public static void BuildWebGL()
        {
            string[] scenes = EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => s.path)
                .ToArray();

            if (scenes.Length == 0)
            {
                Debug.LogError("[WebGLBuild] No enabled scenes in Build Settings - aborting.");
                EditorApplication.Exit(1);
                return;
            }

            // --- WebGL Player Settings (WO-09 section 2.1) ---
            // WO-126: itch.io is a static host and does NOT send the
            // `Content-Encoding: br` header, so Brotli-compressed payloads
            // (.wasm.br/.js.br/.data.br) fail to load ("undefined at ...js.br").
            // Pass `-noBrotli` on the batchmode command line to ship uncompressed
            // files instead. Vercel keeps Brotli (default).
            bool noBrotli = System.Environment.GetCommandLineArgs()
                .Any(a => a.Equals("-noBrotli", System.StringComparison.OrdinalIgnoreCase));

            PlayerSettings.SetScriptingBackend(NamedBuildTarget.WebGL, ScriptingImplementation.IL2CPP);
            PlayerSettings.WebGL.compressionFormat = noBrotli
                ? WebGLCompressionFormat.Disabled
                : WebGLCompressionFormat.Brotli;
            Debug.Log($"[WebGLBuild] compressionFormat = {PlayerSettings.WebGL.compressionFormat}" +
                      (noBrotli ? " (-noBrotli: uncompressed for itch.io)" : " (Brotli for Vercel)"));
            PlayerSettings.WebGL.memorySize = 512;
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.None;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.WebGL, ManagedStrippingLevel.Minimal);
            PlayerSettings.runInBackground = false;

            string dir = Path.GetFullPath(OutputDir);
            Directory.CreateDirectory(dir);

            Debug.Log($"[WebGLBuild] Building {scenes.Length} scene(s) -> {dir}");
            foreach (string s in scenes)
                Debug.Log($"[WebGLBuild]   scene: {s}");

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = dir,   // WebGL outputs a DIRECTORY, not a single file
                target = BuildTarget.WebGL,
                targetGroup = BuildTargetGroup.WebGL,
                options = BuildOptions.None,
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[WebGLBuild] SUCCEEDED - {summary.totalSize / (1024 * 1024)} MB in {summary.totalTime}.");
            }
            else
            {
                Debug.LogError($"[WebGLBuild] FAILED - result={summary.result}, errors={summary.totalErrors}. " +
                               "If result=Failed with no errors, the 'WebGL Build Support' module is likely not installed " +
                               "(install via Unity Hub -> Installs -> 6000.4.8f1 -> Add Modules).");
                EditorApplication.Exit(1);
            }
        }
    }
}
