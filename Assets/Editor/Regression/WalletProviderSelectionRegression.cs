// =============================================================================
// WalletProviderSelectionRegression - WO-766 source-level oracle: real Solana
// wallet provider wiring (Seeker/Android identity+save connect).
// -----------------------------------------------------------------------------
// Pins, from the FILES (no live scene, no package resolve needed):
//   1. SOLANA_SDK is set for the ANDROID scripting-define group and NOT for
//      Standalone. NOTE (2026-08-06): this check is HYGIENE ONLY and is NOT what
//      keeps the real provider off desktop. ProjectSettings is not the only
//      source of that define - DeNelle.Wallet.asmdef carries a
//      PLATFORM-INDEPENDENT versionDefine ("com.solana.unity_sdk" ->
//      "SOLANA_SDK", includePlatforms empty), so SOLANA_SDK is in fact defined on
//      EVERY target. This suite was GREEN while the Windows exe was selecting the
//      real provider and throwing NotSupportedException out of Connect (F8
//      capture 2026-08-06 10:41:22) precisely because it measured this instead of
//      the selector. Check 3 now measures the selector.
//   2. Packages/manifest.json carries the pinned magicblock-labs SDK
//      (com.solana.unity_sdk @ a tagged git URL - never a floating branch).
//   3. WalletService auto-select keys off SolanaWalletProvider
//      .IsSupportedOnThisPlatform - compiled from the same
//      "#if SOLANA_SDK && UNITY_ANDROID && !UNITY_EDITOR" that guards the working
//      body of Connect - and keeps the StubWalletProvider fallback. The old
//      define-based selector must NOT come back.
//   7. Dapp identity is populated and spec-shaped (the wallet approval sheet
//      renders name/uri/icon straight from our authorize request), and the icon
//      is actually SERVED - api/icon.js plus the /icon.png rewrite. A 404 icon
//      makes the wallet draw its own branding instead of ours.
//   4. Safety invariant (spec 766 s3): transfer construction exists ONLY in
//      SolanaWalletProvider.cs (inside its #if SOLANA_SDK block) - no other
//      module file builds a SystemProgram/TokenProgram transfer - and
//      PackStore.Purchase stays gated by FeatureFlags.RealmStorePurchase.
//   5. MWA Android wiring: the .androidlib manifest with the solana-wallet
//      <queries> block exists (package visibility - without it, connect finds
//      no wallet apps on API 30+).
//   6. Identity chain: WalletSkinBootstrap still routes a real connect into
//      GameStateService.BindWallet (the address becomes the cloud-save key).
// Wire into DataRegression.RunAll as [wallet-provider].
// =============================================================================

using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class WalletProviderSelectionRegression
    {
        public static bool Run(out string reason)
        {
            var failures = new List<string>();

            string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string projectSettings = Path.Combine(root, "ProjectSettings/ProjectSettings.asset");
            string manifest = Path.Combine(root, "Packages/manifest.json");
            string walletDir = Path.Combine(Application.dataPath, "_Modules/Wallet");
            string providerPath = Path.Combine(walletDir, "SolanaWalletProvider.cs");
            string servicePath = Path.Combine(walletDir, "WalletService.cs");
            string packStorePath = Path.Combine(walletDir, "PackStore.cs");
            string bootstrapPath = Path.Combine(walletDir, "WalletSkinBootstrap.cs");
            string mwaManifest = Path.Combine(Application.dataPath,
                "Plugins/Android/MobileWalletAdapter.androidlib/AndroidManifest.xml");

            // -- 1. Scripting defines: Android ON, Standalone OFF -------------
            if (!File.Exists(projectSettings))
                failures.Add("ProjectSettings.asset missing");
            else
            {
                var defines = ReadDefineBlock(File.ReadAllLines(projectSettings));
                if (!defines.TryGetValue("Android", out var androidDefines) ||
                    !androidDefines.Contains("SOLANA_SDK"))
                    failures.Add("SOLANA_SDK not in the Android scriptingDefineSymbols group");
                if (defines.TryGetValue("Standalone", out var standaloneDefines) &&
                    standaloneDefines.Contains("SOLANA_SDK"))
                    failures.Add("SOLANA_SDK leaked into the Standalone define group (desktop must keep the stub)");
            }

            // -- 2. Pinned SDK package ----------------------------------------
            if (!File.Exists(manifest))
                failures.Add("Packages/manifest.json missing");
            else
            {
                string manifestText = File.ReadAllText(manifest);
                if (!manifestText.Contains("\"com.solana.unity_sdk\""))
                    failures.Add("com.solana.unity_sdk missing from Packages/manifest.json");
                else if (!manifestText.Contains("Solana.Unity-SDK.git#v"))
                    failures.Add("com.solana.unity_sdk is not pinned to a tagged git version (floating ref)");
            }

            // -- 3. WalletService selection -----------------------------------
            if (!File.Exists(servicePath))
                failures.Add("WalletService.cs missing");
            else
            {
                string svc = File.ReadAllText(servicePath);

                // The selector must be the PLATFORM predicate, not the SDK define.
                // SOLANA_SDK is defined on every target by the asmdef versionDefine,
                // so selecting on it hands a Windows/WebGL player a provider whose
                // Connect can only throw (F8 2026-08-06).
                if (!svc.Contains("if (SolanaWalletProvider.IsSupportedOnThisPlatform)"))
                    failures.Add("WalletService no longer auto-selects on SolanaWalletProvider.IsSupportedOnThisPlatform " +
                                 "(the SDK define is set on EVERY target - selecting on it breaks desktop/WebGL)");

                // And the exact old predicate must not return. Matched as source
                // text because that is what a careless revert would reintroduce.
                if (svc.Contains("if (SolanaWalletProvider.IsSdkAvailable && !Application.isEditor)"))
                    failures.Add("WalletService reverted to the define-based selector " +
                                 "(IsSdkAvailable && !Application.isEditor) - desktop will throw at Connect again");

                if (!svc.Contains("new SolanaWalletProvider()"))
                    failures.Add("WalletService never constructs SolanaWalletProvider");
                if (!svc.Contains("new StubWalletProvider()"))
                    failures.Add("WalletService lost the StubWalletProvider fallback");

                // The cloud-sync attestation gate. Desktop falling back to the stub
                // is only SAFE while the stub cannot attest - otherwise every
                // SDK-less build keys the same cloud player_data row.
                if (!svc.Contains("!(_provider is StubWalletProvider)"))
                    failures.Add("WalletService.IsRealSigningWallet lost the StubWalletProvider exclusion " +
                                 "(a devnet stub could key a shared cloud save row)");
            }

            // The platform predicate must exist and must be compiled from the SAME
            // condition that guards the working Connect body, or the two drift and
            // the desktop defect returns in a new shape.
            if (File.Exists(providerPath))
            {
                string prov0 = File.ReadAllText(providerPath);
                if (!prov0.Contains("IsSupportedOnThisPlatform"))
                    failures.Add("SolanaWalletProvider lost IsSupportedOnThisPlatform (the platform selector)");
                if (!prov0.Contains("#if SOLANA_SDK && UNITY_ANDROID && !UNITY_EDITOR"))
                    failures.Add("SolanaWalletProvider.IsSupportedOnThisPlatform is no longer compiled from " +
                                 "'#if SOLANA_SDK && UNITY_ANDROID && !UNITY_EDITOR' (selection can drift from capability)");
            }

            // -- 4. Safety: transfer construction confined + purchase gate ----
            if (!File.Exists(providerPath))
                failures.Add("SolanaWalletProvider.cs missing");
            else
            {
                string prov = File.ReadAllText(providerPath);
                if (!prov.Contains("#if SOLANA_SDK"))
                    failures.Add("SolanaWalletProvider lost its #if SOLANA_SDK isolation");
                if (!prov.Contains("SystemProgram.Transfer("))
                    failures.Add("SolanaWalletProvider.SendPayment lost its (gated) transfer builder - file drifted, re-verify WO-766 assumptions");
            }
            string modulesRoot = Path.Combine(Application.dataPath, "_Modules");
            foreach (var file in Directory.GetFiles(modulesRoot, "*.cs", SearchOption.AllDirectories))
            {
                if (Path.GetFileName(file) == "SolanaWalletProvider.cs") continue;
                string text = File.ReadAllText(file);
                if (text.Contains("SystemProgram.Transfer(") || text.Contains("TokenProgram.Transfer("))
                    failures.Add("transfer construction OUTSIDE SolanaWalletProvider.cs: " +
                                 file.Substring(modulesRoot.Length + 1).Replace('\\', '/'));
            }
            if (!File.Exists(packStorePath))
                failures.Add("PackStore.cs missing");
            else if (!File.ReadAllText(packStorePath).Contains("FeatureFlags.RealmStorePurchase"))
                failures.Add("PackStore lost the FeatureFlags.RealmStorePurchase gate (purchase rail unguarded)");

            // -- 5. MWA manifest wiring ---------------------------------------
            if (!File.Exists(mwaManifest))
                failures.Add("MobileWalletAdapter.androidlib/AndroidManifest.xml missing (MWA wallet discovery breaks on API 30+)");
            else
            {
                string mwa = File.ReadAllText(mwaManifest);
                if (!mwa.Contains("<queries>") || !mwa.Contains("solana-wallet"))
                    failures.Add("MWA manifest lost the solana-wallet <queries> package-visibility block");
            }

            // -- 6. Identity chain --------------------------------------------
            if (!File.Exists(bootstrapPath))
                failures.Add("WalletSkinBootstrap.cs missing");
            else if (!File.ReadAllText(bootstrapPath).Contains("BindWallet("))
                failures.Add("WalletSkinBootstrap no longer routes connect into GameStateService.BindWallet");

            // -- 7. Dapp identity + the icon that must actually resolve --------
            // An MWA wallet renders its approval sheet from the identity object in
            // our authorize request. Every field is NullValueHandling.Ignore, so a
            // blank one is dropped from the JSON and the wallet silently substitutes
            // its OWN branding - the 2026-08-06 defect. Pin the shape here; the
            // absolute values are asserted in WalletIdentityAndPlatformTest.
            if (File.Exists(providerPath))
            {
                string prov2 = File.ReadAllText(providerPath);
                if (!prov2.Contains("DappIdentityName = \"Echoes of Elarion\""))
                    failures.Add("dapp identity NAME is not \"Echoes of Elarion\" - the approval sheet stops naming the game");
                if (prov2.Contains("DappIconUri = \"/"))
                    failures.Add("dapp icon path starts with '/' - MWA resolves the icon RELATIVE to the identity uri, " +
                                 "and append-style wallet resolvers turn a leading slash into a doubled slash + 404");
                if (!prov2.Contains("DappIdentityUri = \"https://"))
                    failures.Add("dapp identity URI is not an absolute https URL (the SDK client throws on anything else)");
            }

            // The icon has to be SERVED, or the wallet draws its placeholder. The
            // static root is a gitignored Unity WebGL output that every build wipes,
            // so the icon lives in a function + rewrite, like the assetlinks
            // statement. If either half goes missing the branding silently reverts.
            string iconFn = Path.Combine(root, "api/icon.js");
            string vercelJson = Path.Combine(root, "vercel.json");
            if (!File.Exists(iconFn))
                failures.Add("api/icon.js missing - the dapp icon 404s and the wallet shows its own branding");
            if (!File.Exists(vercelJson))
                failures.Add("vercel.json missing");
            else
            {
                string vj = File.ReadAllText(vercelJson);
                if (!vj.Contains("\"/icon.png\""))
                    failures.Add("vercel.json lost the /icon.png rewrite (the dapp icon stops resolving)");
                if (!vj.Contains("/.well-known/assetlinks.json"))
                    failures.Add("vercel.json lost the assetlinks rewrite (wallets can no longer verify us -> " +
                                 "ERROR_AUTHORIZATION_FAILED on every connect)");
            }

            if (failures.Count > 0)
            {
                reason = "WALLET PROVIDER FAIL - " + string.Join("; ", failures);
                return false;
            }
            reason = "WALLET PROVIDER OK - SDK pinned (v-tag), WalletService auto-selects on " +
                     "IsSupportedOnThisPlatform (Android device only) with the stub fallback and the " +
                     "StubWalletProvider attestation exclusion intact, transfer code confined to " +
                     "SolanaWalletProvider behind the RealmStorePurchase gate, MWA queries manifest present, " +
                     "dapp identity named + icon relative and served (api/icon.js + rewrite), " +
                     "BindWallet identity chain wired";
            return true;
        }

        /// <summary>
        /// Extracts the scriptingDefineSymbols platform-to-defines map from the
        /// ProjectSettings.asset YAML (4-space-indented "Platform: A;B;C" lines
        /// directly under the scriptingDefineSymbols key).
        /// </summary>
        private static Dictionary<string, string> ReadDefineBlock(string[] lines)
        {
            var map = new Dictionary<string, string>();
            bool inBlock = false;
            foreach (var line in lines)
            {
                if (!inBlock)
                {
                    if (line.TrimEnd() == "  scriptingDefineSymbols:") inBlock = true;
                    continue;
                }
                if (!line.StartsWith("    ")) break; // block ended
                int colon = line.IndexOf(':');
                if (colon <= 0) continue;
                string key = line.Substring(0, colon).Trim();
                string value = line.Substring(colon + 1).Trim();
                map[key] = value;
            }
            return map;
        }
    }
}
