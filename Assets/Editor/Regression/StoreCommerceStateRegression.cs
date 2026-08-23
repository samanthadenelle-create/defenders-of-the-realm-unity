using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace DeNelle.Editor.Regression
{
    /// <summary>UI-002: truthful, visibly distinct store commerce lifecycle presentation.</summary>
    public static class StoreCommerceStateRegression
    {
        private static readonly string[] Keys =
        {
            "storeCommerceReady", "storeCommerceOpeningWallet", "storeCommerceAwaitingApproval",
            "storeCommerceSubmitted", "storeCommerceVerifying", "storeCommerceDelivering",
            "storeCommerceFulfilled", "storeCommerceCancelled", "storeCommerceFailed",
            "storeCommerceDelayed"
        };

        public static void RunAll()
        {
            if (Run(out var reason)) Debug.Log("STORE_COMMERCE_STATE_OK - " + reason);
            else Debug.LogError("STORE_COMMERCE_STATE_FAIL: " + reason);
        }

        public static bool Run(out string reason)
        {
            var fail = new List<string>();
            try
            {
                string root = Directory.GetParent(Application.dataPath).FullName;
                string code = File.ReadAllText(Path.Combine(root, "Assets/_Modules/Wallet/PackStore.cs"));
                string aPath = Path.Combine(root, "Assets/Resources/Data/Canonical/canon-strings.json");
                string bPath = Path.Combine(root, "Assets/StreamingAssets/Data/Canonical/canon-strings.json");
                string a = File.ReadAllText(aPath);
                string b = File.ReadAllText(bPath);
                if (!string.Equals(a, b, StringComparison.Ordinal)) fail.Add("canonical string copies differ");
                var json = JObject.Parse(a);
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (string key in Keys)
                {
                    string value = (string)json[key];
                    if (string.IsNullOrWhiteSpace(value)) fail.Add("missing commerce key " + key);
                    else if (!seen.Add(value)) fail.Add("two lifecycle states share copy: " + value);
                    else foreach (char c in value) if (c > 127) { fail.Add(key + " is not ASCII-clean"); break; }
                }

                Require(code, "Buy - {pack.AmountLabel(rail)}", "CTA lacks verb + exact price", fail);
                Require(code, "DEVNET - TEST TOKEN", "Devnet marker missing", fail);
                Require(code, "PurchaseEntitlementVerifier.HasPending(pack.Sku)", "pending payment does not suppress Buy", fail);
                Require(code, "Reconcile - no new payment", "pending receipt has no explicit recovery CTA", fail);
                Require(code, "var reconcile = ElarionUiKit.BuildObsidianButton", "pending recovery is inert text instead of a button", fail);
                Require(code, "() => Purchase(pack, SelectedCurrency(pack.Sku)).Forget()", "pending recovery does not re-enter the guarded purchase flow", fail);
                if (code.Contains("MakeText(_spotlightHost, \"[PENDING] Reconcile purchase"))
                    fail.Add("pending recovery regressed to an inert text label");
                Require(code, "do not pay again", "delayed state lacks duplicate-charge warning", fail);
                Require(code, "Human approval has no countdown", "approval timing is dishonest/absent", fail);
                Require(code, "elapsed >= 60f", "confirmation delay escalation missing", fail);
                Require(code, "Wallet did not respond", "wallet timeout recovery missing", fail);
                Require(code, "Reopen the store to reconcile before trying another payment", "indeterminate failure invites unsafe retry", fail);
                Require(code, "$\"{pack.Name} received\\n{DescribeGrantedContents(pack)}\"",
                    "successful delivery has no pack-named exact-content receipt", fail);
                Require(code, "ElarionUiKit.ShowToast(receipt, ElarionUiKit.ToastTone.Confirm",
                    "successful delivery does not use the shared HUD feedback surface", fail);

                string complete = Slice(code, "private async UniTask<bool> CompleteVerifiedPurchaseAsync",
                    "private void ShowFulfillmentReceipt", fail);
                int applyAt = complete.IndexOf("_vm.ApplyPackContents(pack)", StringComparison.Ordinal);
                int ownedAt = complete.IndexOf("_vm.IsOwned(pack.Sku)", applyAt + 1, StringComparison.Ordinal);
                int durableAt = complete.IndexOf("durableFulfillmentSucceeded = await PurchaseEntitlementVerifier.MarkFulfilledAsync", StringComparison.Ordinal);
                int receiptAt = complete.IndexOf("ShowFulfillmentReceipt(pack, payment)", durableAt + 1, StringComparison.Ordinal);
                if (applyAt < 0 || ownedAt < applyAt || durableAt < ownedAt || receiptAt < durableAt)
                    fail.Add("receipt ordering is not apply -> owned proof -> durable fulfilled -> receipt");

                string restore = Slice(code, "private async UniTask<bool> RestoreFulfilledOwnershipAsync",
                    "private async UniTask<bool> CompleteVerifiedPurchaseAsync", fail);
                if (restore.Contains("ShowFulfillmentReceipt") || restore.Contains("ShowToast"))
                    fail.Add("fulfilled reinstall ownership restore replays the purchase receipt/reward feedback");
                // NOT "private async ..." (2026-08-22): the real method at PackStore.cs:1457 is
                // PUBLIC, so this slice found nothing and the suite failed on correct code. The
                // accessibility modifier is incidental to what this oracle asserts -- slice on the
                // return type + name, which is the part that actually identifies the charge path.
                // Third stale oracle ADDRESS found today (see [suite-count] and contract.lamports):
                // when a suite fails on code you believe is right, check what it is pointed at.
                string preFulfilled = Slice(code, "UniTask<PaymentResult> Purchase",
                    "private static PaymentResult Indeterminate", fail);
                if (preFulfilled.Contains("ShowFulfillmentReceipt"))
                    fail.Add("submitted/pending/failed purchase path can show the fulfillment receipt");
                if (code.Contains("SetStatus($\"Purchase failed - {ex.Message}\")"))
                    fail.Add("raw exception still reaches the money screen");
            }
            catch (Exception ex) { fail.Add(ex.GetType().Name + ": " + ex.Message); }

            reason = fail.Count == 0
                ? "10 distinct ASCII lifecycle states, honest timing, pending suppression, safe recovery and Devnet marker pinned"
                : string.Join("; ", fail);
            return fail.Count == 0;
        }

        private static void Require(string haystack, string needle, string message, List<string> fail)
        {
            if (haystack.IndexOf(needle, StringComparison.Ordinal) < 0) fail.Add(message);
        }

        private static string Slice(string source, string start, string end, List<string> fail)
        {
            int a = source.IndexOf(start, StringComparison.Ordinal);
            int b = a >= 0 ? source.IndexOf(end, a + start.Length, StringComparison.Ordinal) : -1;
            if (a >= 0 && b > a) return source.Substring(a, b - a);
            fail.Add("could not isolate commerce source block " + start);
            return string.Empty;
        }
    }
}
