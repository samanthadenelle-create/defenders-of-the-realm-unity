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
