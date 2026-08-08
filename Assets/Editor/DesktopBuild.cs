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
        private const string WebOutputDir = "Builds/WebGL";

        // WebGL build for the Vercel "Dreams" deploy (owner WO-35). Run headless:
        //   run-unity-method.ps1 -Method DeNelle.Editor.DesktopBuild.BuildWebGL -LogName webgl.log
        // Output: Builds/WebGL/ (index.html + Build/). Deploy that folder to Vercel.
        [MenuItem("Defenders/Build/WebGL Player")]
        public static void BuildWebGL()
        {
            string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0)
            {
                Debug.LogError("[DesktopBuild] No enabled scenes in Build Settings — aborting WebGL.");
                EditorApplication.Exit(1);
                return;
            }
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, BuildTarget.WebGL))
            {
                Debug.LogError("[DesktopBuild] WebGL build support is NOT installed — add the WebGL module in Unity Hub, then re-run.");
                EditorApplication.Exit(2);
                return;
            }

            string dir = Path.GetFullPath(WebOutputDir);
            Directory.CreateDirectory(dir);

            // The reflection bridges (audio / pets / dungeon) need their reflected
            // types preserved under IL2CPP stripping (link.xml was removed); keep
            // stripping minimal so WebGL doesn't strip them. Gzip = Vercel-friendly.
            try { PlayerSettings.SetManagedStrippingLevel(UnityEditor.Build.NamedBuildTarget.WebGL, ManagedStrippingLevel.Minimal); } catch (System.Exception e) { Debug.LogWarning("[DesktopBuild] stripping-level set failed: " + e.Message); }
            try { PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip; } catch (System.Exception e) { Debug.LogWarning("[DesktopBuild] WebGL compression set failed: " + e.Message); }
            // RCA 2026-08-01: this write PERSISTS into ProjectSettings.asset and kept showing up
            // as an uncommitted 1->0 flip after every WebGL build (owner keeps the committed
            // value). Capture the prior value and restore it after the build — the setting only
            // matters AT build time.
            var priorExceptionSupport = PlayerSettings.WebGL.exceptionSupport;
            try { PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.None; } catch { }

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = dir,
                target = BuildTarget.WebGL,
                targetGroup = BuildTargetGroup.WebGL,
                options = BuildOptions.None,
            };

            Debug.Log($"[DesktopBuild] WebGL build -> {dir} ({scenes.Length} scenes). This can take many minutes.");
            BuildReport webReport = BuildPipeline.BuildPlayer(options);
            BuildSummary webSummary = webReport.summary;

            // Restore the committed exception-support value (see RCA note above) BEFORE exiting,
            // so a WebGL build never leaves ProjectSettings.asset dirty.
            try { PlayerSettings.WebGL.exceptionSupport = priorExceptionSupport; } catch { }

            if (webSummary.result == BuildResult.Succeeded)
                Debug.Log($"[DesktopBuild] WebGL SUCCEEDED — {webSummary.totalSize / (1024 * 1024)} MB in {webSummary.totalTime}. Deploy Builds/WebGL/ to Vercel.");
            else
            {
                Debug.LogError($"[DesktopBuild] WebGL FAILED — result={webSummary.result}, errors={webSummary.totalErrors}.");
                EditorApplication.Exit(1);
            }
        }

        /// <summary>
        /// RELEASE desktop player — no Development flag, so no watermark and no DevTools.
        /// </summary>
        /// <remarks>
        /// Owner ask 2026-08-08, mid store-capture: "flag is still on ... cant record with that ...
        /// can you do a prod build."
        ///
        /// WHY A FLAG FLIP WAS NOT ENOUGH. ff.devresourcetool now defaults OFF, but FeatureFlags.Get
        /// (:796-802) reads PlayerPrefs FIRST and only falls back to the default when nothing is
        /// stored - and this machine carries ff.devresourcetool=1 from the 08-07 "enable the dev tab"
        /// pass. A stored 1 beats any default, on her machine and on anyone else's who ever set it.
        /// A release build settles it structurally instead: DeNelle.DevTools is gated
        /// #if UNITY_EDITOR || DEVELOPMENT_BUILD, so with Development OFF the chips and the panel
        /// cannot exist whatever PlayerPrefs says.
        ///
        /// AND IT REMOVES THE WATERMARK. A Development player paints "Development Build" in the
        /// corner of every frame - which was about to appear in the store screenshots. KEY_FACTS has
        /// carried "desktop release still ships Development (open item)" for weeks; this is that item.
        ///
        /// The QA build is UNCHANGED and still the default: BuildWindows keeps
        /// BuildOptions.Development so the F1 panel, force-wave and grant-materials survive for
        /// felt-testing. Use this one for capture and release, that one to play.
        /// </remarks>
        [MenuItem("Defenders/Build/Windows x64 Player (RELEASE - no dev tools)")]
        public static void BuildWindowsRelease() => BuildWindows(development: false);

        [MenuItem("Defenders/Build/Windows x64 Player")]
        public static void BuildWindows() => BuildWindows(development: true);

        private static void BuildWindows(bool development)
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

            // Say WHICH build this is, loudly. The two differ in whether dev tools and the
            // watermark exist, and "which exe am I looking at" is exactly the question a capture
            // session needs answered from the log rather than from the corner of a screenshot.
            Debug.Log($"[DesktopBuild] Building {scenes.Length} scene(s) -> {exePath} " +
                      $"[{(development ? "DEVELOPMENT - dev tools + watermark" : "RELEASE - no dev tools, no watermark")}]");
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
                    // RCA instrumentation (owner recurrence 2026-08-01: dynamic batching keeps
                    // reverting to 0 in ProjectSettings despite the 1 above — this readback
                    // captures what the internal setter ACTUALLY wrote, so the next diff is proven
                    // not guessed). GetBatchingForPlatform: (BuildTarget, out int, out int).
                    var getBatching = typeof(PlayerSettings).GetMethod("GetBatchingForPlatform",
                        System.Reflection.BindingFlags.Static
                        | System.Reflection.BindingFlags.Public
                        | System.Reflection.BindingFlags.NonPublic);
                    if (getBatching != null)
                    {
                        var args = new object[] { BuildTarget.StandaloneWindows64, 0, 0 };
                        getBatching.Invoke(null, args);
                        Debug.Log("[DesktopBuild] batching readback: static=" + args[1] + " dynamic=" + args[2]
                                  + " (expected 0/1 — if dynamic reads 0 the internal setter is the reverter).");
                    }
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

            // Windowed by default (owner 2026-05-27): the player opened in exclusive
            // fullscreen, which is awkward to alt-tab out of mid-playtest. Ship a
            // resizable 1600x900 window instead. Players can still toggle fullscreen
            // at runtime with Alt+Enter.
            try
            {
                PlayerSettings.fullScreenMode      = FullScreenMode.Windowed;
                PlayerSettings.defaultScreenWidth  = 1600;
                PlayerSettings.defaultScreenHeight = 900;
                PlayerSettings.resizableWindow     = true;
                Debug.Log("[DesktopBuild] Default display mode set to Windowed 1600x900 (resizable; Alt+Enter toggles fullscreen).");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[DesktopBuild] Could not set windowed mode: {e.Message}");
            }

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = exePath,
                target = BuildTarget.StandaloneWindows64,
                // Development build so the DevTools QA panel (gear / F1: force-wave,
                // grant-materials) compiles in — it is gated #if DEVELOPMENT_BUILD.
                // RELEASE (development:false) strips that assembly entirely AND drops the
                // "Development Build" watermark — see BuildWindowsRelease for why a feature-flag
                // flip could not achieve either.
                options = development ? BuildOptions.Development : BuildOptions.None,
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            // RCA CLOSE (owner recurrence, twice-captured 2026-08-01): the pre-build
            // SetBatchingForPlatform(0,1) readback proves memory holds static=0/dynamic=1,
            // yet the session's exit serialization writes the Standalone entry with
            // dynamic=0 — the reverter runs INSIDE BuildPlayer, after our set. Mirror the
            // WebGL exceptionSupport restore above: re-assert the owner's 0/1 AFTER the
            // build so the final ProjectSettings.asset save carries dynamic=1 and the
            // build never leaves the tree dirty.
            try
            {
                var setBatching = typeof(PlayerSettings).GetMethod("SetBatchingForPlatform",
                    System.Reflection.BindingFlags.Static
                    | System.Reflection.BindingFlags.Public
                    | System.Reflection.BindingFlags.NonPublic);
                if (setBatching != null)
                {
                    setBatching.Invoke(null, new object[] { BuildTarget.StandaloneWindows64, 0, 1 });
                    Debug.Log("[DesktopBuild] post-build batching re-assert: static=0 dynamic=1 (exit-serialization guard).");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[DesktopBuild] post-build batching re-assert failed: {e.Message}");
            }

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
