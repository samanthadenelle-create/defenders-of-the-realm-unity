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
            string piController = Read("Assets/_Modules/Core/Platform/PiSignInController.cs", failures);
            string loginBridge = Read("Assets/_Modules/Core/Platform/LoginSurfacePlatform.cs", failures);
            string loginVm = Read("Assets/_Modules/Onboarding/LoginViewModel.cs", failures);

            Require(build, "BuildGooglePlayAab", "Play AAB entry point missing", failures);
            Require(build, "GooglePlayPackagingGate.AssertSourceIsolation()", "Play build no longer runs Gate 0", failures);
            Require(build, "BuildAndroidArtifact(isGooglePlay: true)", "Play entry point does not select the Play artifact", failures);
            Require(build, "EditorUserBuildSettings.buildAppBundle = isGooglePlay", "AAB/APK mode is not asserted per artifact", failures);
            Require(build, "? \"GOOGLE_PLAY\" : \"DAPP_STORE\"", "immutable channel stamps missing", failures);
            Require(build, "string forbidden = isGooglePlay ? \"DAPP_STORE\" : \"GOOGLE_PLAY\"",
                    "artifact define composition no longer removes the opposite channel", failures);
            Require(build, "!isGooglePlay || !string.Equals(value, \"SOLANA_SDK\"",
                    "Play artifact define composition no longer strips SOLANA_SDK", failures);
            Require(build, ".Append(wanted)", "artifact define composition no longer supplies its channel stamp", failures);
            Require(build, "GooglePlayPackagingGate.AssertBuiltArtifact(artifactPath)", "successful AAB bypasses post-build inspection", failures);

            int gateAt = build.IndexOf("GooglePlayPackagingGate.AssertSourceIsolation()", StringComparison.Ordinal);
            int buildAt = build.IndexOf("BuildAndroidArtifact(isGooglePlay: true)", StringComparison.Ordinal);
            if (gateAt < 0 || buildAt < 0 || gateAt > buildAt)
                failures.Add("Play source gate does not execute before the player build");

            Require(gate, "DeNelle.Village directly references DeNelle.Wallet", "known assembly-graph blocker is no longer diagnosed", failures);
            Require(gate, "MobileWalletAdapter.androidlib is an unconditional Android plugin", "MWA plugin blocker is no longer diagnosed", failures);
            Require(gate, "Solana SDK is not embedded", "embedded-package source gate is missing", failures);
            Require(gate, "Embedded Solana SDK runtime assembly has no !GOOGLE_PLAY constraint", "SDK runtime constraint gate is missing", failures);
            Require(gate, "Embedded Solana managed plugin is unconditional", "managed-plugin constraint gate is missing", failures);
            Require(gate, "Vendored UniTask must remain available", "UniTask availability gate is missing", failures);
            Require(gate, "Android PlayerSettings persist artifact symbol",
                    "persistent Android artifact-symbol rejection is missing", failures);
            Require(gate, "PLAY_ARTIFACT_DIRTY", "artifact rejection marker missing", failures);
            Require(gate, "ScanStream(stream, entry.FullName, tokens, hits)", "in-build audit no longer scans every AAB payload", failures);
            Require(gate, "SKRbvo6Gf7GondiT3BbTfuRDPqLWei4j2Qy2NPGZhW3", "live SKR mint is absent from forbidden material", failures);
            Require(gate, "stake.solanamobile", "staking marketing is absent from forbidden material", failures);
            Require(gate, "\"crypto\"", "generic crypto token is absent from forbidden material", failures);
            Require(gate, "\"sign in with pi\"", "Pi authentication CTA is absent from forbidden material", failures);
            Require(gate, "\"api.minepi.com\"", "Pi authentication backend is absent from forbidden material", failures);
            Require(gate, "defenders/mwa/", "in-build scanner does not target the actual MWA package path", failures);
            Require(gate, "phantom wallet", "in-build scanner does not target Phantom wallet branding", failures);
            Require(gate, "app.phantom", "in-build scanner does not target Phantom app identifiers", failures);
            Reject(gate, "\"mwa/\"", "in-build scanner still uses ambiguous mwa/ token", failures);
            Reject(gate, "\"phantom\"", "in-build scanner still uses ambiguous phantom token", failures);
            Require(scanner, "PLAY_ARTIFACT_CLEAN_OK", "standalone artifact scanner success marker missing", failures);
            Require(scanner, "Test-StreamToken", "standalone scanner no longer inspects binary payloads", failures);
            Require(scanner, "stake.solanamobile", "standalone scanner does not cover staking marketing", failures);
            Require(scanner, "'crypto'", "standalone scanner does not cover generic crypto material", failures);
            Require(scanner, "'sign in with pi'", "standalone scanner does not cover Pi authentication CTA", failures);
            Require(scanner, "'api.minepi.com'", "standalone scanner does not cover Pi authentication backend", failures);
            Require(scanner, "'defenders/mwa/'", "standalone scanner does not target the actual MWA package path", failures);
            Require(scanner, "'phantom wallet'", "standalone scanner does not target Phantom wallet branding", failures);
            Require(scanner, "'app.phantom'", "standalone scanner does not target Phantom app identifiers", failures);
            Require(scanner, "$userFacingTokens", "standalone scanner lost readable-content semantic tier", failures);
            Require(scanner, "$opaqueTokens", "standalone scanner lost opaque executable semantic tier", failures);
            Require(scanner, "$tokens = if ($isUserFacing)", "standalone scanner no longer selects tokens by entry semantics", failures);
            Require(scanner, "BUNDLE-METADATA/com.unity/dependencies.pb", "standalone scanner no longer classifies dependency provenance separately", failures);
            Reject(scanner, "'mwa/'", "standalone scanner still uses ambiguous mwa/ token", failures);
            Reject(scanner, "'phantom'", "standalone scanner still uses ambiguous phantom token", failures);

            Require(piController, "#if GOOGLE_PLAY", "Pi runtime has no Play compile-time exclusion", failures);
            Require(piController, "public static string SignedInUid => null;",
                    "Play Pi stub no longer preserves the shared read-only identity seam", failures);
            int piGuard = piController.IndexOf("#if GOOGLE_PLAY", StringComparison.Ordinal);
            int piElse = piController.IndexOf("#else", piGuard, StringComparison.Ordinal);
            int piRuntime = piController.IndexOf("public sealed class PiSignInController : MonoBehaviour", StringComparison.Ordinal);
            if (piGuard < 0 || piElse < 0 || piRuntime < piElse)
                failures.Add("Pi runtime controller is not confined to the non-Play branch");

            Require(loginBridge, "GooglePlayIdentityBridge.EnsureSignedInAsync()",
                    "Play login still does not call the real Google identity bridge", failures);
            Require(loginBridge, "GameStateService.IsGooglePlayIdentity(playerId)",
                    "Play login no longer verifies the bound play-* identity", failures);
            Require(loginVm, "#if !GOOGLE_PLAY", "Play VM can re-bind Google identity through the wallet API", failures);

            // Semantic-tier mutation pins: readable authoring content keeps broad policy
            // vocabulary, while opaque runtime metadata rejects only executable SDK evidence.
            string[] readableMutation = GooglePlayPackagingGate.TokensForEntry(
                "base/assets/Data/Canonical/mutation.json");
            string[] opaqueMutation = GooglePlayPackagingGate.TokensForEntry(
                "base/assets/bin/Data/Managed/Metadata/global-metadata.dat");
            if (!Array.Exists(readableMutation, token => token == "crypto") ||
                !Array.Exists(readableMutation, token => token == "web3"))
                failures.Add("readable-content mutation no longer rejects standalone crypto/web3");
            if (Array.Exists(opaqueMutation, token => token == "crypto") ||
                Array.Exists(opaqueMutation, token => token == "web3"))
                failures.Add("opaque mutation still rejects generic runtime crypto/web3 substrings");
            if (Array.Exists(opaqueMutation, token => token == "com.solana.unity_sdk"))
                failures.Add("opaque mutation still rejects package provenance identity");
            if (!Array.Exists(opaqueMutation, token => token == "Solana.Unity.") ||
                !Array.Exists(opaqueMutation, token => token == "connect wallet"))
                failures.Add("opaque mutation no longer rejects executable SDK/wallet evidence");
            if (!GooglePlayPackagingGate.ShouldSkipProvenanceEntry(
                    "BUNDLE-METADATA/com.unity/dependencies.pb"))
                failures.Add("dependency provenance receipt is no longer classified separately");
            if (GooglePlayPackagingGate.MatchesTokenForAudit(
                    "Disconnect Wallet pressed", "connect wallet"))
                failures.Add("word-boundary mutation falsely rejects Disconnect Wallet");
            if (!GooglePlayPackagingGate.MatchesTokenForAudit(
                    "Tap Connect Wallet now", "connect wallet"))
                failures.Add("word-boundary mutation no longer rejects standalone Connect Wallet");
            Require(scanner, "Test-TokenInText", "standalone scanner lost boundary-aware token matching", failures);

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
            Require(content, "Assets/Resources/Data/Canonical/stake-rewards.json",
                    "Play content exclusion no longer quarantines generated stake-reward source data", failures);
            Require(content, "root[\"_nightMarketNote\"] = \"Google Play store presentation.\"",
                    "Play-neutral canon-strings rewrite lost its exact note allowlist", failures);
            Require(content, "root[\"storeBuyWalletRequiredCta\"] = \"Continue\"",
                    "Play-neutral wallet CTA rewrite lost its exact field pin", failures);
            Require(content, "root[\"swap.poweredBy\"] = \"Store service\"",
                    "Play-neutral localization rewrite lost its exact field pin", failures);
            Require(content, "pricing?.Property(\"usdc\")?.Remove()",
                    "Play-neutral pack rewrite no longer removes only wallet price rails", failures);
            Require(content, "pricing?.Property(\"sol\")?.Remove()",
                    "Play-neutral pack rewrite no longer removes SOL pricing", failures);
            Require(content, "pricing?.Property(\"skr\")?.Remove()",
                    "Play-neutral pack rewrite no longer removes SKR pricing", failures);
            Require(content, "RewriteLedgerPath", "Play-neutral rewrite has no crash-safe ledger", failures);
            Require(content, "File.WriteAllBytes(backup, File.ReadAllBytes(path))",
                    "Play-neutral rewrite no longer backs up original bytes", failures);
            Require(content, "ValidateNeutralMirrorEquality(\"rewrite\")",
                    "Play-neutral rewrite no longer validates mirror equality after mutation", failures);
            Require(content, "ValidateNeutralMirrorEquality(\"restore\")",
                    "Play-neutral rewrite no longer validates mirror equality after restoration", failures);
            Require(content, "Assets/_Modules/Onboarding/UI/TitleScreen.uxml",
                    "Play-neutral rewrite no longer covers TitleScreen.uxml", failures);
            Require(content, "Assets/_Modules/Onboarding/UI/HeroSelectScreen.uxml",
                    "Play-neutral rewrite no longer covers HeroSelectScreen.uxml", failures);
            Require(content, "uxml.Replace(\"Connect Wallet\", \"Continue with Google\")",
                    "Play-neutral UXML rewrite lost its exact text mutation", failures);
            Require(content, "PLAY_NEUTRAL_BYTE_RESTORE_MISMATCH",
                    "Play-neutral UXML/catalog transaction no longer verifies byte restore", failures);

            string currencySkin = Read("Assets/_Modules/Core/Platform/CurrencySkin.cs", failures);
            string showcase = Read("Assets/_Modules/Core/UI/SkrShowcasePanel.cs", failures);
            string stakePanel = Read("Assets/_Modules/Core/UI/StakeRewardsPanel.cs", failures);
            Require(currencySkin, "#if GOOGLE_PLAY", "SKR currency fallback is not Play-neutral at compile time", failures);
            Require(currencySkin, "storeCtaVerb: \"Continue\"", "Play currency fallback exposes a crypto spend CTA", failures);
            Require(showcase, "ConnectActionLabel = \"Continue with Google\"",
                    "Play showcase action is not compile-time neutral", failures);
            Require(stakePanel, "StakeNativeLine = \"Store rewards are unavailable in this edition.\"",
                    "Play staking surface still compiles its native-staking URL", failures);
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

        private static void Reject(string text, string token, string message, List<string> failures)
        {
            if (text.Contains(token)) failures.Add(message);
        }
    }
}
