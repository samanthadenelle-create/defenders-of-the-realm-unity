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

            // RELEASE SIGNING (owner 2026-07-16 — testers must be able to UPDATE IN PLACE, which
            // needs a STABLE signature across builds). Read the keystore + passwords from a
            // GITIGNORED keystore.properties at the project root (never committed); if absent, fall
            // back to debug signing so a fresh clone / CI still builds. Passwords are set in-memory
            // for this batchmode session only — do NOT AssetDatabase.SaveAssets() them into
            // ProjectSettings.asset (that would leak the secret into git).
            ApplyReleaseSigning();

            Debug.Log($"[AndroidBuild] PlayerSettings: id={PackageId}, IL2CPP, ARM64, minSdk=26.");
        }

        private static void ApplyReleaseSigning()
        {
            string propsPath = Path.Combine(Directory.GetCurrentDirectory(), "keystore.properties");
            if (!File.Exists(propsPath))
            {
                PlayerSettings.Android.useCustomKeystore = false;
                Debug.LogWarning("[AndroidBuild] keystore.properties not found — DEBUG signing (testers can't update in place).");
                return;
            }

            var kv = new System.Collections.Generic.Dictionary<string, string>();
            foreach (var raw in File.ReadAllLines(propsPath))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                kv[line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim();
            }

            string ksPath, alias, storePass, keyPass;
            if (!kv.TryGetValue("keystore.path", out ksPath) || !File.Exists(ksPath) ||
                !kv.TryGetValue("keystore.alias", out alias) ||
                !kv.TryGetValue("keystore.storepass", out storePass) ||
                !kv.TryGetValue("keystore.keypass", out keyPass))
            {
                PlayerSettings.Android.useCustomKeystore = false;
                Debug.LogWarning("[AndroidBuild] keystore.properties incomplete/keystore missing — DEBUG signing.");
                return;
            }

            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystoreName = ksPath;
            PlayerSettings.Android.keystorePass = storePass;
            PlayerSettings.Android.keyaliasName = alias;
            PlayerSettings.Android.keyaliasPass = keyPass;
            Debug.Log($"[AndroidBuild] RELEASE signing: keystore='{Path.GetFileName(ksPath)}' alias='{alias}' (stable signature for tester updates).");
        }
    }
}
