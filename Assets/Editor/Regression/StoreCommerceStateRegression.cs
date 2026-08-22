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
                Require(code, "do not pay again", "delayed state lacks duplicate-charge warning", fail);
                Require(code, "Human approval has no countdown", "approval timing is dishonest/absent", fail);
                Require(code, "elapsed >= 60f", "confirmation delay escalation missing", fail);
                Require(code, "Wallet did not respond", "wallet timeout recovery missing", fail);
                Require(code, "Reopen the store to reconcile before trying another payment", "indeterminate failure invites unsafe retry", fail);
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
    }
}
