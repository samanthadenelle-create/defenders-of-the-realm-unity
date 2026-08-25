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
                Require(code, "ElarionUiKit.ShowToast(receipt, ElarionUiKit.ToastTone.Confirm",
                    "successful delivery does not use the shared HUD feedback surface", fail);

                // =============================================================================
                //  WO-1188 - THE RECEIPT NAMES WHAT ARRIVED, NOT WHAT WAS ADVERTISED
                // -----------------------------------------------------------------------------
                // ⭐ RE-POINTED, NOT SOFTENED (2026-08-25). This block used to be ONE Require, on
                //     $"{pack.Name} received\n{DescribeGrantedContents(pack)}"
                // - the pack's AUTHORED inventory, read straight off the pack card. The owner ruled
                // that the confirmation screen must report the MEASURED credited amounts, so the
                // CONTRACT moved and the oracle moves WITH it, in the same edit. (WO-978 is the
                // ticket where four economy callers logged the amount REQUESTED as though it were
                // the amount CREDITED; the one screen where the player is checking whether they got
                // what they paid for is the worst possible place to repeat that.)
                //
                // ⛔ THE REPLACEMENT IS STRICTER THAN WHAT IT REPLACES, and that is the whole point.
                // The old pin proved ONE STRING EXISTED. These prove the whole chain, each link of
                // which a revert would break:
                //   1. the receipt is still PACK-NAMED (the half of the old contract that survives)
                //   2. its body comes from the MEASURED describer, not the advertised one
                //   3. the number is a BEFORE/AFTER DELTA, not a pack field
                //   4. that delta is what gets PRINTED
                //   5. the delta is measured against the ONE authoritative wallet total
                //   6. the advertised list can never be printed (positional + anti-revert)
                // A revert of the receipt line to DescribeGrantedContents(pack) fails 2, 6 and the
                // anti-revert literal - three ways, not one.
                // =============================================================================
                Require(code, "string receipt = $\"{pack.Name} received\\n\" +",
                    "successful delivery receipt is no longer pack-named", fail);
                Require(code, "DescribeMeasuredDelivery(pack, before, payment.TxSignature)",
                    "successful delivery receipt is not built from the MEASURED delivery", fail);
                Require(code, "int credited = Mathf.Max(0, after.Of(r) - before.Of(r));",
                    "the receipt amount is not a measured before/after delta", fail);
                Require(code, "deposited.Append(credited.ToString(\"N0\"))",
                    "the receipt prints something other than the CREDITED amount", fail);
                Require(code, "Bank.CurrentOf(BankRes.Wood)",
                    "the delta is not measured against the authoritative wallet total", fail);
                // ANTI-REVERT. The exact construction this ticket retired, pinned as a NEGATIVE so
                // restoring it can never pass by also satisfying something else.
                if (code.Contains("received\\n{DescribeGrantedContents(pack)}"))
                    fail.Add("the receipt reverted to the ADVERTISED pack contents");

                string measured = Slice(code, "private string DescribeMeasuredDelivery",
                    "private async UniTask<bool> RestoreFulfilledOwnershipAsync", fail);
                if (measured.Contains("deposited.Append(advertised"))
                    fail.Add("the deposited line prints the ADVERTISED amount instead of the credited one");
                if (measured.Contains("DescribeGrantedContents"))
                    fail.Add("the measured describer falls back to the advertised pack contents");
                // The two disclosures the owner ruled must exist IN WORDS - never a tick, never a
                // colour (she is red/green colourblind, so the greyscale reading is the contract).
                Require(measured, "is above storage (",
                    "a purchase that lands above the storage cap is not disclosed in words", fail);
                // ⚠ TWO needles, not one sentence: the copy is built from concatenated fragments, so
                // a needle spanning the join would never match and this pin would fail on correct
                // code - the stale-ADDRESS failure this file already carries a note about at :79.
                Require(measured, "arrived so far. ",
                    "a short delivery does not name what arrived", fail);
                Require(measured, "The rest stays recorded against this payment",
                    "a short delivery does not say the remainder is still recorded", fail);

                // The advertised describer may still EXIST - it is the comparison half of the
                // diagnostic that prints measured and advertised side by side, which is how a
                // divergence becomes visible in the trace instead of on the player's screen. It may
                // live NOWHERE ELSE on the delivery path. Sliced from the SIGNATURE, so the method's
                // own XML doc (which names it in prose) is outside the window.
                string receiptFn = Slice(code, "private void ShowFulfillmentReceipt",
                    "private async UniTaskVoid ConnectForWalletGate", fail);
                int advertisedAt = receiptFn.IndexOf("DescribeGrantedContents(", StringComparison.Ordinal);
                int comparisonAt = receiptFn.IndexOf("| pack card would have said: ", StringComparison.Ordinal);
                if (advertisedAt < 0 || comparisonAt < 0 || advertisedAt < comparisonAt)
                    fail.Add("advertised pack contents reach the receipt body - they may live ONLY in the side-by-side diagnostic");
                if (Occurrences(receiptFn, "DescribeGrantedContents(") != 1)
                    fail.Add("advertised pack contents are referenced more than once on the delivery path");

                string complete = Slice(code, "private async UniTask<bool> CompleteVerifiedPurchaseAsync",
                    "private void ShowFulfillmentReceipt", fail);
                // ⛔ THE ORDERING ASSERTION IS PRESERVED, NOT REPLACED. The receipt must still come
                // AFTER the durable proof or the screen prints a delivery nobody confirmed. It broke
                // only because the call gained the snapshot argument - an ADDRESS change, not a
                // contract change. It gains one link at the FRONT: the baseline must be captured
                // BEFORE the grant, because that is the only moment at which "what this purchase
                // credited" is measurable at all. Read it any later and the delta is zero and the
                // confirmation quietly says nothing arrived.
                int snapshotAt = complete.IndexOf("var beforeGrant = EconomySnapshot.Capture()", StringComparison.Ordinal);
                int applyAt = complete.IndexOf("_vm.ApplyPackContents(pack)", StringComparison.Ordinal);
                int ownedAt = complete.IndexOf("_vm.IsOwned(pack.Sku)", applyAt + 1, StringComparison.Ordinal);
                int durableAt = complete.IndexOf("durableFulfillmentSucceeded = await PurchaseEntitlementVerifier.MarkFulfilledAsync", StringComparison.Ordinal);
                int receiptAt = complete.IndexOf("ShowFulfillmentReceipt(pack, payment, beforeGrant)", durableAt + 1, StringComparison.Ordinal);
                if (snapshotAt < 0 || applyAt < 0 || snapshotAt > applyAt ||
                    ownedAt < applyAt || durableAt < ownedAt || receiptAt < durableAt)
                    fail.Add("receipt ordering is not baseline snapshot -> apply -> owned proof -> durable fulfilled -> measured receipt");
                // The already-granted replay branch has no before/after to measure (the grant landed
                // in an earlier run), so it must hand the receipt an UNMEASURED snapshot rather than
                // a stale one - an invalid snapshot is what makes the copy say so instead of guessing.
                int replayAt = complete.IndexOf("ShowFulfillmentReceipt(pack, payment, default(EconomySnapshot))", StringComparison.Ordinal);
                if (replayAt < 0 || (snapshotAt >= 0 && replayAt > snapshotAt))
                    fail.Add("the already-granted replay branch does not hand an UNMEASURED snapshot to the receipt");

                // WO-1188 - the processing screen STAYS, and re-asking is never re-paying.
                string poll = Slice(code, "private async UniTask<bool> AwaitGrantConfirmationAsync",
                    "private static string BuildProcessingDetail", fail);
                if (poll.Contains("_wallet.Pay(") || poll.Contains("RequestQuoteAsync"))
                    fail.Add("the confirmation poll re-prompts the wallet or re-quotes - one payment, one transfer");
                Require(code, "GrantPollAttempts",
                    "the post-payment confirmation is not bounded by a poll ceiling", fail);
                Require(code, "Your payment is recorded and nothing further will be charged.",
                    "the bounded give-up does not state that nothing further will be charged", fail);
                if (code.Contains("Payment submitted; verification is pending. Reopen the store to resume"))
                    fail.Add("the retired 'reopen the store to resume' terminal sentence is back on the post-payment path");

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
                ? "10 distinct ASCII lifecycle states, honest timing, pending suppression, safe recovery, Devnet marker, "
                  + "and the WO-1188 measured-delivery contract (pack-named receipt, before/after delta off the "
                  + "authoritative wallet, worded over-cap and short-delivery disclosure, advertised list confined to "
                  + "the side-by-side diagnostic, snapshot->apply->owned->durable->receipt ordering, non-repaying poll) pinned"
                : string.Join("; ", fail);
            return fail.Count == 0;
        }

        private static void Require(string haystack, string needle, string message, List<string> fail)
        {
            if (haystack.IndexOf(needle, StringComparison.Ordinal) < 0) fail.Add(message);
        }

        /// <summary>Non-overlapping occurrence count. Used for the "exactly one caller" pins, where
        /// "it exists somewhere" is not the assertion - "it exists in exactly one place" is.</summary>
        private static int Occurrences(string haystack, string needle)
        {
            if (string.IsNullOrEmpty(haystack) || string.IsNullOrEmpty(needle)) return 0;
            int n = 0;
            for (int i = haystack.IndexOf(needle, StringComparison.Ordinal); i >= 0;
                 i = haystack.IndexOf(needle, i + needle.Length, StringComparison.Ordinal)) n++;
            return n;
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
