// =============================================================================
// WalletProviderSelectionRegression - WO-766 source-level oracle: real Solana
// wallet provider wiring (Seeker/Android identity+save connect).
// -----------------------------------------------------------------------------
// Pins, from the FILES (no live scene, no package resolve needed):
//   1. SOLANA_SDK is set for the ANDROID scripting-define group and NOT for
//      Standalone (desktop deliberately keeps StubWalletProvider - WO-766).
//   2. Packages/manifest.json carries the pinned magicblock-labs SDK
//      (com.solana.unity_sdk @ a tagged git URL - never a floating branch).
//   3. WalletService auto-select: constructs SolanaWalletProvider under
//      IsSdkAvailable, keeps the StubWalletProvider fallback, and guards the
//      Editor out (MWA needs a device).
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
                if (!svc.Contains("SolanaWalletProvider.IsSdkAvailable"))
                    failures.Add("WalletService no longer auto-selects on SolanaWalletProvider.IsSdkAvailable");
                if (!svc.Contains("new SolanaWalletProvider()"))
                    failures.Add("WalletService never constructs SolanaWalletProvider");
                if (!svc.Contains("new StubWalletProvider()"))
                    failures.Add("WalletService lost the StubWalletProvider fallback");
                if (!svc.Contains("!Application.isEditor"))
                    failures.Add("WalletService lost the Editor guard (MWA needs a device; editor must keep the stub)");
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

            if (failures.Count > 0)
            {
                reason = "WALLET PROVIDER FAIL - " + string.Join("; ", failures);
                return false;
            }
            reason = "WALLET PROVIDER OK - SOLANA_SDK Android-only, SDK pinned (v-tag), WalletService " +
                     "auto-select + editor guard + stub fallback intact, transfer code confined to " +
                     "SolanaWalletProvider behind the RealmStorePurchase gate, MWA queries manifest present, " +
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
