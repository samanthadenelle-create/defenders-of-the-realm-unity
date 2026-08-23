using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace DeNelle.Editor.Regression
{
    /// <summary>Independent source oracle for MON002's real-value safety envelope.</summary>
    public static class MainnetCanaryRegression
    {
        private const string Sku = "mainnet-wood-canary";
        private const string Owner = "CHKKFkPGz8VZfjpsZjJTqfAUW7vMpdNkkqCVuCcZsfkC";
        private const string Mint = "SKRbvo6Gf7GondiT3BbTfuRDPqLWei4j2Qy2NPGZhW3";

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            string root = Application.dataPath.Replace('\\', '/');
            string repo = Directory.GetParent(Application.dataPath).FullName.Replace('\\', '/');
            string product = Read(root + "/_Modules/Wallet/MainnetCanaryCatalog.cs", failures);
            string endpoints = Read(root + "/_Modules/Wallet/WalletEndpoints.cs", failures);
            string registry = Read(root + "/_Modules/Wallet/WalletRegistry.cs", failures);
            string gate = Read(root + "/_Modules/Wallet/PurchaseGate.cs", failures);
            string provider = Read(root + "/_Modules/Wallet/SolanaWalletProvider.cs", failures);
            string store = Read(root + "/_Modules/Wallet/PackStore.cs", failures);
            string verifier = Read(root + "/_Modules/Wallet/PurchaseEntitlementVerifier.cs", failures);
            string flags = Read(root + "/_Modules/Core/FeatureFlags.cs", failures);
            string server = Read(repo + "/api/_lib/purchase-catalog.js", failures);
            string verifyApi = Read(repo + "/api/purchases/verify.js", failures);

            Need(product, $"Sku = \"{Sku}\"", "client SKU drifted", failures);
            Need(product, $"OwnerWallet = \"{Owner}\"", "client owner allowlist drifted", failures);
            Need(product, "SkrPrice = 1d", "client price is not exactly 1 SKR", failures);
            Need(product, "WoodReward = 1", "client reward is not exactly 1 wood", failures);
            Need(product, "#if MAINNET_CANARY_TEST", "product is not compile-time isolated", failures);
            Need(endpoints, $"SkrMintMainnet = \"{Mint}\"", "official Mainnet SKR mint drifted", failures);
            Need(flags, "STORE_RAIL_LOCAL_TEST || MAINNET_CANARY_TEST",
                "canary build cannot open the otherwise fail-closed purchase gate", failures);
            Need(gate, "WalletRegistry.MainnetPurchaseRecipientAddress",
                "purchase gate does not refuse a missing approved recipient", failures);
            Need(registry, "return e != null && e.IsValid ? e.Address : string.Empty",
                "Mainnet recipient gained an unsafe fallback", failures);
            Need(provider, "PurchaseGate.MainnetCanarySku", "provider does not pin the canary SKU", failures);
            Need(provider, "currency != CurrencyKind.Skr", "provider does not pin the SKR rail", failures);
            Need(provider, "MainnetCanaryCatalog.OwnerWallet", "provider does not pin the owner signer", failures);
            Need(store, "_wallet.SetNetwork(WalletNetwork.Mainnet)", "canary never selects Mainnet", failures);
            Need(store, "await _wallet.Disconnect()", "canary can reuse a chain-scoped Devnet association", failures);
            Need(verifier, "? \"mainnet-beta\" : \"devnet\"", "client/backend Mainnet spelling can drift", failures);

            Need(server, $"MAINNET_CANARY_SKU = '{Sku}'", "server SKU drifted", failures);
            Need(server, $"MAINNET_SKR_MINT = '{Mint}'", "server mint drifted", failures);
            Need(server, $"MAINNET_CANARY_OWNER = '{Owner}'", "server owner allowlist drifted", failures);
            Need(server, "amountBaseUnits: 1_000_000, decimals: 6", "server price is not exactly 1 SKR", failures);
            Need(endpoints, "SkrDecimalsMainnet = 6", "client Mainnet SKR decimals drifted", failures);
            Need(provider, "DecimalsFor(currency, network)", "transfer builder ignores network-specific decimals", failures);
            Need(server, "MAINNET_CANARY_ENABLED", "server has no independent kill switch", failures);
            Need(verifyApi, "SOLANA_MAINNET_RPC_URL", "verifier has no explicit Mainnet RPC", failures);
            Need(verifyApi, "walletAllowed(network, sku, playerId)", "verifier does not enforce owner access", failures);
            Need(verifyApi, "commitment: 'finalized'", "Mainnet verification does not require finality", failures);
            Need(verifyApi, "parsed.type === 'transferChecked'", "Mainnet verifier accepts unchecked transfer", failures);
            Need(verifyApi, "=== String(contract.amountBaseUnits)", "verifier does not require exact base units", failures);

            reason = failures.Count == 0
                ? "MON002 owner-only 1 SKR -> 1 wood contract is isolated, fail-closed, and independently verified"
                : string.Join(" | ", failures);
            return failures.Count == 0;
        }

        private static string Read(string path, List<string> failures)
        {
            if (File.Exists(path)) return File.ReadAllText(path);
            failures.Add("missing " + path);
            return string.Empty;
        }

        private static void Need(string source, string token, string message, List<string> failures)
        {
            if (source.IndexOf(token, StringComparison.Ordinal) < 0) failures.Add(message);
        }
    }
}
