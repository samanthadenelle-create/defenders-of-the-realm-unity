using System;
using System.Collections.Generic;
using System.IO;

namespace DeNelle.Editor
{
    /// <summary>WO-1255 source oracle for the fail-closed Play AAB packaging chain.</summary>
    public static class GooglePlayPackagingRegression
    {
        /// <summary>Focused batchmode entry point; does not build an AAB.</summary>
        public static void RunFocused()
        {
            if (Run(out string focusedReason))
            {
                UnityEngine.Debug.Log(focusedReason);
                UnityEngine.Debug.Log("PLAY_PACKAGING_REGRESSION_OK");
                return;
            }

            UnityEngine.Debug.LogError(focusedReason);
            UnityEngine.Debug.LogError("PLAY_PACKAGING_REGRESSION_FAIL");
            UnityEditor.EditorApplication.Exit(1);
        }

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            string build = Read("Assets/Editor/AndroidBuild.cs", failures);
            string gate = Read("Assets/Editor/Regression/GooglePlayPackagingGate.cs", failures);
            string scanner = Read("tools/android/assert-google-play-aab-clean.ps1", failures);

            Require(build, "BuildGooglePlayAab", "Play AAB entry point missing", failures);
            Require(build, "GooglePlayPackagingGate.AssertSourceIsolation()", "Play build no longer runs Gate 0", failures);
            Require(build, "BuildAndroidArtifact(isGooglePlay: true)", "Play entry point does not select the Play artifact", failures);
            Require(build, "EditorUserBuildSettings.buildAppBundle = isGooglePlay", "AAB/APK mode is not asserted per artifact", failures);
            Require(build, "? \"GOOGLE_PLAY\" : \"DAPP_STORE\"", "immutable channel stamps missing", failures);
            Require(build, "GooglePlayPackagingGate.AssertBuiltArtifact(artifactPath)", "successful AAB bypasses post-build inspection", failures);

            int gateAt = build.IndexOf("GooglePlayPackagingGate.AssertSourceIsolation()", StringComparison.Ordinal);
            int buildAt = build.IndexOf("BuildAndroidArtifact(isGooglePlay: true)", StringComparison.Ordinal);
            if (gateAt < 0 || buildAt < 0 || gateAt > buildAt)
                failures.Add("Play source gate does not execute before the player build");

            Require(gate, "DeNelle.Village directly references DeNelle.Wallet", "known assembly-graph blocker is no longer diagnosed", failures);
            Require(gate, "MobileWalletAdapter.androidlib is an unconditional Android plugin", "MWA plugin blocker is no longer diagnosed", failures);
            Require(gate, "PLAY_ARTIFACT_DIRTY", "artifact rejection marker missing", failures);
            Require(gate, "ScanStream(stream, entry.FullName, hits)", "in-build audit no longer scans every AAB payload", failures);
            Require(gate, "SKRbvo6Gf7GondiT3BbTfuRDPqLWei4j2Qy2NPGZhW3", "live SKR mint is absent from forbidden material", failures);
            Require(scanner, "PLAY_ARTIFACT_CLEAN_OK", "standalone artifact scanner success marker missing", failures);
            Require(scanner, "Test-StreamToken", "standalone scanner no longer inspects binary payloads", failures);

            // -- WO-1282 Lane D: the force-include door -------------------------------------
            // Source isolation being green proved NOTHING about the artifact on 2026-08-30:
            // Resources/ and StreamingAssets/ are packed by construction, so no .asmdef
            // constraint reaches them. These assertions pin the mechanism that does, INCLUDING
            // its restore discipline — a quarantine with no restore silently strips the Solana
            // rail from the next Seeker APK, which is a WORSE regression than the one it fixes.
            string content = Read("Assets/Editor/GooglePlayContentExclusion.cs", failures);
            Require(content, "Assets/Resources/SolanaUnitySDK",
                    "Play content exclusion no longer quarantines the force-included Solana SDK Resources", failures);
            Require(content, "Assets/StreamingAssets/Data/Canonical/wallets.json",
                    "Play content exclusion no longer quarantines the StreamingAssets wallet registry", failures);
            Require(content, "Assets/Resources/Data/Canonical/wallets.json",
                    "Play content exclusion no longer quarantines the Resources wallet registry mirror", failures);
            Require(content, "IPostprocessBuildWithReport",
                    "Play content exclusion has no post-build restore", failures);
            Require(content, "InitializeOnLoadMethod",
                    "Play content exclusion has no domain-load repair for an interrupted build", failures);
            Require(content, "PLAY_CONTENT_REPAIRED",
                    "interrupted-build repair no longer announces itself", failures);
            Require(build, "GooglePlayContentExclusion.EnsureTreeIsWhole()",
                    "AndroidBuild no longer sweeps a leftover quarantine before the content build", failures);

            int sweepAt = build.IndexOf("GooglePlayContentExclusion.EnsureTreeIsWhole()", StringComparison.Ordinal);
            int contentAt = build.IndexOf("AddressablesContentBuild.EnsureBuilt", StringComparison.Ordinal);
            if (sweepAt < 0 || contentAt < 0 || sweepAt > contentAt)
                failures.Add("quarantine sweep does not run before the Addressables content build; a Seeker " +
                             "APK could be baked from a wallet-less tree");

            string stamp = Read("Assets/_Modules/Core/Payments/ArtifactVariantStamp.cs", failures);
            Require(stamp, "RuntimeInitializeOnLoadMethod",
                    "artifact variant is no longer stamped into the device log at startup", failures);
            Require(stamp, "SEEKER BUILD IS MISSING THE WALLET PAYLOAD",
                    "a wallet-less Seeker build no longer announces itself on first launch", failures);

            // This is an audit observation, not a red regression: today Gate 0 is expected to
            // block. Once the assembly split/plugin exclusion lands, the same regression stays
            // green and the build may proceed to physical artifact proof.
            int blockerCount = GooglePlayPackagingGate.InspectSourceIsolation().Count;
            if (blockerCount == 0)
                reason = "PLAY_PACKAGING_GATE_OK - source isolation ready; AAB still requires physical scanner proof";
            else
                reason = $"PLAY_PACKAGING_GATE_OK - fail-closed with {blockerCount} named Gate-0 blocker(s); no non-compliant AAB can be built";

            if (failures.Count == 0) return true;
            reason = "PLAY_PACKAGING_GATE_FAIL: " + string.Join(" | ", failures);
            return false;
        }

        private static string Read(string path, List<string> failures)
        {
            if (File.Exists(path)) return File.ReadAllText(path);
            failures.Add("missing " + path);
            return string.Empty;
        }

        private static void Require(string text, string token, string message, List<string> failures)
        {
            if (!text.Contains(token)) failures.Add(message);
        }
    }
}
