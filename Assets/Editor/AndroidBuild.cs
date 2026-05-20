// =============================================================================
// AndroidBuild — produces an ARM64 APK for the Solana Seeker phone. Run headless:
//
//   Unity.exe -batchmode -quit -buildTarget Android -projectPath <proj> \
//             -executeMethod DeNelle.Editor.AndroidBuild.BuildSeekerApk
//
// Output: <proj>/Builds/Android/DefendersOfTheRealm.apk
//
// Prerequisites:
//   1. Unity Hub -> 6000.4.7f1 -> Add Modules -> "Android Build Support" (with
//      OpenJDK + Android SDK + NDK sub-modules). Without these the build aborts
//      immediately with a Gradle error.
//   2. The Seeker is ARM64-only — this script forces ARM64, IL2CPP, and the
//      minimum SDK level the Seeker ships with.
//
// After the APK lands, push to the connected Seeker over USB:
//
//   adb install -r Builds\Android\DefendersOfTheRealm.apk
//
// (adb ships with the Android module under
//  C:\Program Files\Unity\Hub\Editor\6000.4.7f1\Editor\Data\PlaybackEngines\
//      AndroidPlayer\SDK\platform-tools\adb.exe.)
// =============================================================================

using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace DeNelle.Editor
{
    /// <summary>
    /// One-shot Android APK build entry point. Configures Seeker-correct player
    /// settings (ARM64-only, IL2CPP, package id), then builds whatever scenes
    /// are enabled in Build Settings to <c>Builds/Android/</c>.
    /// </summary>
    public static class AndroidBuild
    {
        private const string OutputDir = "Builds/Android";
        private const string ApkName = "DefendersOfTheRealm.apk";
        private const string PackageId = "com.denelle.defenders";
        private const string ProductName = "Defenders of the Realm";
        private const string CompanyName = "DeNelle";

        [MenuItem("Defenders/Build/Android APK (Seeker)")]
        public static void BuildSeekerApk()
        {
            string[] scenes = EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => s.path)
                .ToArray();

            if (scenes.Length == 0)
            {
                Debug.LogError("[AndroidBuild] No enabled scenes in Build Settings — aborting.");
                EditorApplication.Exit(1);
                return;
            }

            ApplyAndroidPlayerSettings();

            string dir = Path.GetFullPath(OutputDir);
            Directory.CreateDirectory(dir);
            string apkPath = Path.Combine(dir, ApkName);

            Debug.Log($"[AndroidBuild] Building {scenes.Length} scene(s) -> {apkPath}");
            foreach (string s in scenes)
                Debug.Log($"[AndroidBuild]   scene: {s}");

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = apkPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None,
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[AndroidBuild] SUCCEEDED — {summary.totalSize / (1024 * 1024)} MB in {summary.totalTime}. APK: {apkPath}");
            }
            else
            {
                Debug.LogError($"[AndroidBuild] FAILED — result={summary.result}, " +
                               $"errors={summary.totalErrors}. See log for Gradle output.");
                EditorApplication.Exit(1);
            }
        }

        /// <summary>
        /// Applies the Seeker-correct PlayerSettings via the supported APIs.
        /// Idempotent — re-running re-asserts every value.
        /// </summary>
        private static void ApplyAndroidPlayerSettings()
        {
            PlayerSettings.companyName = CompanyName;
            PlayerSettings.productName = ProductName;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, PackageId);

            // IL2CPP is required for ARM64; the Mono backend cannot target ARM64
            // on Android, which the Seeker requires.
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);

            // ARM64-only. The Seeker is ARM64; building ARMv7 alongside doubles
            // the APK size for no benefit.
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;

            // Min SDK 26 (Android 8) — the Seeker ships with Android 13 (API 33),
            // and 26 is the modern floor for IL2CPP / 64-bit Play Store policy.
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;

            // Internal-test signing — the dev build is installed via adb, not
            // through the Play Store. The Seeker accepts unsigned APKs in
            // developer mode, so no keystore is wired here. Add one if/when
            // we ship through Saga's dApp Store or Play.
            PlayerSettings.Android.useCustomKeystore = false;

            Debug.Log($"[AndroidBuild] PlayerSettings: id={PackageId}, IL2CPP, ARM64, minSdk=26.");
        }
    }
}
