using System;
using System.Collections.Generic;
using System.IO;
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
                string api = Read(Directory.GetParent(Application.dataPath).FullName.Replace('\\', '/') +
                                  "/api/purchases/verify.js", failures);
                string catalog = Read(Directory.GetParent(Application.dataPath).FullName.Replace('\\', '/') +
                                      "/api/_lib/purchase-catalog.js", failures);

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
                Forbid(solana, "var wallet = Web3.Wallet", "payment revived the dead/implicit Web3 wallet path", failures);
                Require(store, "PurchaseEntitlementVerifier.VerifyPendingAsync", "charge path has no backend entitlement verification", failures);
                Require(store, "CompleteVerifiedPurchaseAsync", "verified exactly-once fulfilment seam is absent", failures);
                Require(store, "MarkFulfilledAsync", "local ownership does not acknowledge server fulfilment", failures);
                Require(store, "await PurchaseEntitlementVerifier.MarkFulfilledAsync", "fulfilment acknowledgement is not awaited", failures);

                string verifier = Read(root + "/_Modules/Wallet/PurchaseEntitlementVerifier.cs", failures);
                Require(verifier, "/api/purchases/fulfill", "fulfilment endpoint is not wired", failures);
                Require(verifier, "req.responseCode != 200", "pending purchase can clear without HTTP 200", failures);
                Require(verifier, "response.State != \"fulfilled\"", "pending purchase can clear without fulfilled state", failures);
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
                Require(catalog, "'hearth-spark'", "server canary SKU is absent", failures);
                Require(catalog, "currency: 'SKR'", "server canary is not the ruled SKR rail", failures);
                Require(catalog, "amountBaseUnits: 20_000_000_000", "server canary drifted from 20 SKR at 9 decimals", failures);
                Require(catalog, "SOLANA_DEVNET_SKR_MINT", "server has no independent Devnet SKR mint authority", failures);
                Require(api, "parsed.type === 'transferChecked'", "backend accepts an unchecked token transfer", failures);
                Require(api, "contract.recipientAta", "backend does not pin the ruled recipient ATA", failures);
                Require(api, "contract.mint", "backend does not pin the Devnet SKR mint", failures);
                Require(api, "tokenAmount.decimals", "backend does not pin mint decimals", failures);

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
