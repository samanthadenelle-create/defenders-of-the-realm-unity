using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace DeNelle.Editor.Regression
{
    /// <summary>Independent source contract for WO-1146/1147 activation-critical seams.</summary>
    public static class MonetizationActivationRegression
    {
        public static void RunAll()
        {
            if (Run(out string reason)) Debug.Log("MONETIZATION_ACTIVATION_OK - " + reason);
            else Debug.LogError("MONETIZATION_ACTIVATION_FAIL: " + reason);
        }

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            try
            {
                string root = Application.dataPath.Replace('\\', '/');
                string rewarded = Read(root + "/_Modules/Village/Monetization/RewardedAdManager.cs", failures);
                string timers = Read(root + "/_Modules/Village/Buildings/BuildTimerService.cs", failures);
                string gate = Read(root + "/_Modules/Village/Monetization/AdGateService.cs", failures);
                string provider = Read(root + "/_Modules/Village/Monetization/Providers/LevelPlayInitializer.cs", failures);
                string store = Read(root + "/_Modules/Wallet/PackStore.cs", failures);
                string solana = Read(root + "/_Modules/Wallet/SolanaWalletProvider.cs", failures);
                string scenario = Read(root + "/_Modules/Wallet/TargetedLocalAssociationScenario.cs", failures);
                string featureFlags = Read(root + "/_Modules/Core/FeatureFlags.cs", failures);
                string endpoints = Read(root + "/_Modules/Wallet/WalletEndpoints.cs", failures);
                string mainnetCanary = Read(root + "/_Modules/Wallet/MainnetCanaryCatalog.cs", failures);
                string api = Read(Directory.GetParent(Application.dataPath).FullName.Replace('\\', '/') +
                                  "/api/purchases/verify.js", failures);
                string catalog = Read(Directory.GetParent(Application.dataPath).FullName.Replace('\\', '/') +
                                      "/api/_lib/purchase-catalog.js", failures);
                string streamingPacks = Read(root + "/StreamingAssets/Data/Canonical/packs.json", failures);
                string resourcePacks = Read(root + "/Resources/Data/Canonical/packs.json", failures);

                Require(rewarded, "TryShowAd(sync) is permanently refused", "ads sync seam is not a permanent refusal", failures);
                Require(timers, "sync overload REFUSED (WO-1146)", "build timer sync overload is not pinned shut", failures);
                Require(gate, "public static bool Present", "placement gate is absent", failures);
                Require(gate, "rewarded_ad_completed", "reward outcome telemetry is absent", failures);
                Require(provider, "rewarded_ad_impression", "ILRD does not reach analytics", failures);
                Require(provider, "ConcurrentQueue<ImpressionEvent>", "ILRD is not marshalled before Unity-facing telemetry", failures);
                Require(provider, "if (_presentationSettled)", "provider has no per-presentation duplicate reward guard", failures);
                Require(provider, "IsCallbackForActivePresentation", "reward callbacks are not pinned to the active ad unit", failures);
                Require(provider, "if (!_presentationSettled) SettleRewarded(AdShowResult.Dismissed())",
                    "close can overwrite or repeat an already-earned settlement", failures);

                Require(scenario, "client.SignTransactions", "targeted MWA transaction signing is absent", failures);
                Require(solana, "scenario.SignTransaction", "payment still bypasses targeted MWA signing", failures);
                Require(solana, "tx.Sign(new Account(string.Empty, from))",
                    "MWA transaction omits the fee-payer signature placeholder and is malformed", failures);
                RequireOrder(solana, "tx.Sign(new Account(string.Empty, from))", "scenario.SignTransaction",
                    "MWA transaction is serialized before its required signature placeholder is added", failures);
                Require(solana, "method = \"sendTransaction\"",
                    "signed purchase does not use the transparent JSON-RPC submission seam", failures);
                Require(solana, "skipPreflight = false",
                    "signed purchase bypasses Solana preflight simulation", failures);
                Require(solana, "TryReadPrimarySignature(signedWire",
                    "signed receipt is not derived before ambiguous RPC transport", failures);
                RequireOrder(solana, "TryReadPrimarySignature(signedWire", "SubmitSignedTransaction(",
                    "signed receipt is derived only after transport, leaving a double-charge window", failures);
                Forbid(solana, "var confirmed = await ConfirmTransaction",
                    "submitted signature is delayed behind client confirmation before durable pending storage", failures);
                Require(store, "PurchaseEntitlementVerifier.Remember(pack, result, _wallet)",
                    "submitted signature is not persisted before entitlement handling", failures);
                RequireOrder(store, "if (!string.IsNullOrEmpty(result.TxSignature))", "if (result.Ok)",
                    "ambiguous signed receipt is persisted only on the success branch", failures);
                Forbid(solana, "var wallet = Web3.Wallet", "payment revived the dead/implicit Web3 wallet path", failures);
                Require(store, "PurchaseEntitlementVerifier.VerifyPendingAsync", "charge path has no backend entitlement verification", failures);
                Require(store, "CompleteVerifiedPurchaseAsync", "verified exactly-once fulfilment seam is absent", failures);
                Require(store, "MarkFulfilledAsync", "local ownership does not acknowledge server fulfilment", failures);
                Require(store, "await PurchaseEntitlementVerifier.MarkFulfilledAsync", "fulfilment acknowledgement is not awaited", failures);
                Require(store, "EntitlementVerificationState.Fulfilled",
                    "server fulfilled state is collapsed into verified and can replay consumables", failures);
                Require(store, "RestoreFulfilledOwnershipAsync",
                    "fulfilled recovery has no ownership-only delivery path", failures);

                string verifier = Read(root + "/_Modules/Wallet/PurchaseEntitlementVerifier.cs", failures);
                Require(verifier, "/api/purchases/fulfill", "fulfilment endpoint is not wired", failures);
                Require(verifier, "req.responseCode != 200", "pending purchase can clear without HTTP 200", failures);
                Require(verifier, "response.State != \"fulfilled\"", "pending purchase can clear without fulfilled state", failures);
                Require(verifier, "pending.sku, pack.Sku",
                    "pending receipt is not strictly bound to the requested SKU", failures);
                Require(verifier, "pending.network, expectedNetwork",
                    "pending receipt is not strictly bound to the active network", failures);
                Require(verifier, "pending.currency, expectedCurrency",
                    "pending receipt is not strictly bound to the selected currency", failures);
                Require(verifier, "response.Sku, pack.Sku",
                    "verified response SKU is not bound before local grant", failures);
                RequireOrder(verifier, "response.State != \"fulfilled\"", "PlayerPrefs.DeleteKey(PendingPrefix + sku)",
                    "pending purchase clears before server fulfilment acknowledgement", failures);
                Require(store, "PurchaseGate.TryClaimGrant(payment.TxSignature)", "fulfilment does not claim the tx idempotently", failures);
                Require(api, "commitment: 'finalized'", "backend does not require finalized chain data", failures);
                Require(api, "wrong_signer", "backend does not verify the signer", failures);
                Require(api, "contract.recipient", "backend does not verify the server-owned recipient", failures);
                // NOT "contract.lamports" (2026-08-22): the canary rail is SKR, an SPL token at 9
                // decimals -- there are no lamports on it. The DB column kept the legacy name
                // expected_lamports, but the server-owned contract field is amountBaseUnits. Pin the
                // COMPARISON rather than the field name, so this proves exact equality against the
                // server's own figure instead of merely proving a token appears somewhere in the file.
                Require(api, "=== String(contract.amountBaseUnits)", "backend does not verify the server-owned amount", failures);
                Require(catalog, "PRICE-PARITY LAW", "server catalog does not document the no-build/no-deploy parity law", failures);
                VerifyPriceParity(streamingPacks, resourcePacks, catalog, failures);
                Require(catalog, "SOLANA_DEVNET_SKR_MINT", "server has no independent Devnet SKR mint authority", failures);
                Require(api, "parsed.type === 'transferChecked'", "backend accepts an unchecked token transfer", failures);
                Require(api, "contract.recipientAta", "backend does not pin the ruled recipient ATA", failures);
                Require(api, "contract.mint", "backend does not pin the Devnet SKR mint", failures);
                Require(api, "tokenAmount.decimals", "backend does not pin mint decimals", failures);

                // MON002 independent hard pins. These expectations are deliberately not derived
                // from the server catalog they police.
                Require(catalog, "MAINNET_CANARY_SKU = 'mainnet-wood-canary'",
                    "Mainnet canary SKU drifted", failures);
                Require(catalog, "amountBaseUnits: 1_000_000, decimals: 6",
                    "Mainnet canary is not exactly 1 SKR at 6 decimals", failures);
                Require(catalog, "MAINNET_CANARY_ENABLED", "Mainnet server canary has no fail-closed switch", failures);
                Require(catalog, "MAINNET_CANARY_OWNER", "Mainnet server canary has no owner allowlist", failures);
                Require(api, "walletAllowed(network, sku, playerId)",
                    "Mainnet verifier does not enforce the owner allowlist", failures);
                Require(endpoints, "SKRbvo6Gf7GondiT3BbTfuRDPqLWei4j2Qy2NPGZhW3",
                    "client does not pin the official Mainnet SKR mint", failures);
                Require(mainnetCanary, "internal const double SkrPrice = 1d",
                    "client Mainnet canary price is not exactly 1 SKR", failures);
                Require(mainnetCanary, "internal const int WoodReward = 1",
                    "client Mainnet canary reward is not exactly 1 wood", failures);
                Require(mainnetCanary, "#if MAINNET_CANARY_TEST",
                    "Mainnet canary product is not compiled behind its isolated symbol", failures);

                Require(featureFlags, "RealmStorePurchase => Get(\"realmstorepurchase\", defaultOn: false)",
                    "public purchase flag no longer defaults OFF", failures);
                Require(featureFlags, "RewardedAdSkip => Get(\"rewardedadskip\", defaultOn: false)",
                    "public rewarded-ad flag no longer defaults OFF", failures);
            }
            catch (Exception ex)
            {
                failures.Add("oracle threw " + ex.GetType().Name + ": " + ex.Message);
            }

            reason = failures.Count == 0
                ? "ads have one async placement-gated reward path + main-thread ILRD telemetry; purchases use targeted MWA, finalized server verification, a pinned canary contract, and exactly-once fulfilment; both public flags remain OFF"
                : string.Join(" | ", failures);
            return failures.Count == 0;
        }

        private static string Read(string path, List<string> failures)
        {
            if (File.Exists(path)) return File.ReadAllText(path);
            failures.Add("missing " + path);
            return string.Empty;
        }

        private static void VerifyPriceParity(string streaming, string resources, string server,
            List<string> failures)
        {
            if (!string.Equals(streaming, resources, StringComparison.Ordinal))
                failures.Add("canonical packs.json mirrors differ");
            try
            {
                JObject canon = JObject.Parse(streaming);
                Match canary = Regex.Match(server, @"DEVNET_CANARY_SKU\s*=\s*'([^']+)'", RegexOptions.CultureInvariant);
                Match row = Regex.Match(server,
                    @"\[DEVNET_CANARY_SKU\].*?currency:\s*'([^']+)'.*?amountBaseUnits:\s*([0-9_]+).*?decimals:\s*([0-9]+)",
                    RegexOptions.Singleline | RegexOptions.CultureInvariant);
                if (!canary.Success || !row.Success)
                {
                    failures.Add("server canary catalog is not decidable by the parity oracle");
                    return;
                }

                int devnetStart = server.IndexOf("const DEVNET_PACKS", StringComparison.Ordinal);
                int mainnetStart = server.IndexOf("const MAINNET_PACKS", StringComparison.Ordinal);
                string devnetBlock = devnetStart >= 0 && mainnetStart > devnetStart
                    ? server.Substring(devnetStart, mainnetStart - devnetStart)
                    : string.Empty;
                MatchCollection serverRows = Regex.Matches(devnetBlock,
                    @"Object\.freeze\(\{\s*currency:", RegexOptions.CultureInvariant);
                if (string.IsNullOrEmpty(devnetBlock) || serverRows.Count != 1)
                    failures.Add("missing or extra Devnet server canary");

                string sku = canary.Groups[1].Value;
                if (!string.Equals(sku, "hearth-spark", StringComparison.Ordinal))
                    failures.Add("ruled hearth-spark canary is missing");
                if (!string.Equals(row.Groups[1].Value, "SKR", StringComparison.Ordinal))
                    failures.Add("server canary currency is not SKR");

                JToken pack = canon["packs"]?.FirstOrDefault(p =>
                    string.Equals((string)p["sku"], sku, StringComparison.Ordinal));
                if (pack == null)
                {
                    failures.Add("server canary is absent from canonical client packs");
                    return;
                }

                int decimals = int.Parse(row.Groups[3].Value, CultureInfo.InvariantCulture);
                decimal skr = pack["pricing"]?["skr"]?.Value<decimal>() ?? -1m;
                decimal scale = 1m;
                for (int i = 0; i < decimals; i++) scale *= 10m;
                decimal scaled = skr * scale;
                if (scaled != decimal.Truncate(scaled))
                    failures.Add("canonical SKR price requires forbidden base-unit rounding");
                decimal backend = decimal.Parse(row.Groups[2].Value.Replace("_", ""), CultureInfo.InvariantCulture);
                if (scaled != backend)
                    failures.Add($"client/backend SKR price mismatch for {sku}: client={scaled} backend={backend}");
            }
            catch (Exception ex)
            {
                failures.Add("price parity oracle threw " + ex.GetType().Name + ": " + ex.Message);
            }
        }

        private static void Require(string source, string token, string failure, List<string> failures)
        {
            if (source.IndexOf(token, StringComparison.Ordinal) < 0) failures.Add(failure);
        }

        private static void Forbid(string source, string token, string failure, List<string> failures)
        {
            if (source.IndexOf(token, StringComparison.Ordinal) >= 0) failures.Add(failure);
        }

        private static void RequireOrder(string source, string first, string second,
            string failure, List<string> failures)
        {
            int firstIndex = source.IndexOf(first, StringComparison.Ordinal);
            int secondIndex = source.IndexOf(second, StringComparison.Ordinal);
            if (firstIndex < 0 || secondIndex < 0 || firstIndex >= secondIndex) failures.Add(failure);
        }
    }
}
