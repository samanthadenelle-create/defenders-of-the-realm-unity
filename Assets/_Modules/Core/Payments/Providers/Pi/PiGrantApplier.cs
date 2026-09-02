// =============================================================================
// PiGrantApplier - exactly-once local delivery of a Pi-paid pack.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.PaymentProviders.Pi   Namespace: DeNelle.Core.Payments.Providers
//
// WO-1318. Shape copied DELIBERATELY from GooglePlayGrantApplier (same write-ahead
// journal, same fail-closed rule, same recovery-by-evidence), keyed on the Pi
// paymentId instead of a Play purchase token. It is a copy of a PATTERN, not a second
// grant path: the actual delivery is still PackGrantBridge.TryApply(sku), the one
// rail-neutral entry point (PackGrantBridge.cs), so no contents are hand-rolled here.
//
// WHY IT IS NEEDED AT ALL. Pi WILL re-present a payment: onIncompletePaymentFound
// hands us a payment from a previous session that we may or may not have already
// settled and granted. Without a journal, a resumed payment double-grants; with a
// naive marker written after the mutation, a crash between them looks like a fresh
// purchase and also double-grants.
//
//   1. read the pack's CURRENT ownership -> preOwned
//   2. journal "pending|<sku>|<preOwned>" + PlayerPrefs.Save()   (WRITE-AHEAD)
//   3. PackGrantBridge.TryApply(sku)
//   4. verify it took; if not, leave the journal PENDING so a retry re-grants
//   5. journal "applied|<sku>"
//
// ⛔ FAIL CLOSED, ALWAYS. Every unknown returns false. A false means "not granted" and
//    leaves the payment recoverable; never return true to unstick something.
//
// ⛔ RESIDUAL AMBIGUITY, DECLARED NOT HIDDEN (same as the Play rail): if the player
//    ALREADY owned the SKU and we crash between 3 and 5, ownership cannot say whether
//    this payment's contents landed. We RE-APPLY, because "paid and got nothing" is
//    unrecoverable and a duplicated top-up is not. Every time that branch is taken it
//    is FlowTrace.Warn-ed so it shows up in a device capture.
//
// The Pi paymentId is an identifier, not a bearer credential (the key that authorises
// anything against api.minepi.com is PI_NETWORK_API_KEY and lives only on the server),
// so it is safe to journal directly - but it is still hashed, because PlayerPrefs is
// backup-readable and a payment id correlates a device to a purchase.
// =============================================================================

using System;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using DeNelle.Commerce;
using DeNelle.Core.Diagnostics;
// PackCatalog lives in the DeNelle.Commerce ASSEMBLY but kept the DeNelle.Wallet NAMESPACE
// deliberately (WO-1282 Lane A) - PromoCodeService resolves "DeNelle.Wallet.PackStoreVM" as a
// reflection string literal, so the namespace is a live runtime contract. This using is the
// NAMESPACE, not a reference to the DeNelle.Wallet assembly (which this one does not reference).
using DeNelle.Wallet;

namespace DeNelle.Core.Payments.Providers
{
    internal static class PiGrantApplier
    {
        private const string TraceSystem = PiPaymentEndpoints.TraceSystem;
        private const string JournalPrefix = "pi.grant.";
        private const string StatePending = "pending";
        private const string StateApplied = "applied";

        /// <summary>
        /// Applies the pack for this Pi payment exactly once. True only when the entitlement is proven
        /// present in the save afterwards.
        /// </summary>
        internal static bool ApplyExactlyOnce(string sku, string piPaymentId)
        {
            using var _ = FlowTrace.Enter(TraceSystem, $"grant ApplyExactlyOnce '{sku}'");

            if (string.IsNullOrEmpty(sku) || !TryJournalKey(piPaymentId, out string key))
            {
                FlowTrace.Fail(TraceSystem, "grant refused: empty SKU or unusable Pi payment id.");
                return false;
            }

            if (PackCatalog.Find(sku) == null)
            {
                FlowTrace.Fail(TraceSystem,
                    $"grant refused: '{sku}' is not in the pack catalog. A settled payment for an unknown " +
                    "SKU must never invent contents.");
                return false;
            }

            if (IsApplied(key, sku)) return true;

            if (!PackGrantBridge.HasApplier)
            {
                FlowTrace.Fail(TraceSystem,
                    $"grant refused for '{sku}': no local pack grant applier is registered. On a WebGL/Pi " +
                    "artifact this is a DEFECT - PackStoreBootstrap did not run, and a paid pack is being " +
                    "refused. The payment stays incomplete so Pi re-presents it next launch.");
                return false;
            }

            if (!PackGrantBridge.TryIsOwned(sku, out bool preOwned))
            {
                FlowTrace.Fail(TraceSystem,
                    $"grant refused for '{sku}': the ownership probe did not resolve, so an interrupted " +
                    "retry could not be told apart from a first apply. Failing closed.");
                return false;
            }

            // WRITE-AHEAD. Persist the intent BEFORE mutating; marking after would make a crash in
            // between look like a fresh purchase and grant twice.
            WriteJournal(key, StatePending + "|" + sku + "|" + (preOwned ? "1" : "0"));

            if (!PackGrantBridge.TryApply(sku))
            {
                FlowTrace.Fail(TraceSystem,
                    $"grant for '{sku}' did NOT take. The journal entry stays PENDING; the payment is left " +
                    "recoverable so onIncompletePaymentFound retries it. If this repeats, the entitlement " +
                    "writer is broken.");
                return false;
            }

            WriteJournal(key, StateApplied + "|" + sku);
            FlowTrace.Step(TraceSystem, $"grant for '{sku}' applied and journalled as settled.");
            return true;
        }

        /// <summary>
        /// Has this payment already delivered? Recovers the crash-between-apply-and-mark case by
        /// EVIDENCE: a pending entry whose pack was NOT owned before and IS owned now means the grant
        /// landed, so it is promoted rather than re-applied.
        /// </summary>
        private static bool IsApplied(string key, string sku)
        {
            string entry = PlayerPrefs.GetString(JournalPrefix + key, string.Empty);
            if (string.IsNullOrEmpty(entry)) return false;

            if (entry.StartsWith(StateApplied, StringComparison.Ordinal))
            {
                FlowTrace.Step(TraceSystem, $"grant for '{sku}' already journalled as applied - not re-granting.");
                return true;
            }

            var parts = entry.Split('|');
            bool preOwned = parts.Length >= 3 && parts[2] == "1";
            if (!PackGrantBridge.TryIsOwned(sku, out bool ownedNow)) return false;

            if (preOwned)
            {
                // The declared ambiguity. Say it out loud, every time.
                FlowTrace.Warn(TraceSystem,
                    $"interrupted settlement for '{sku}': the player ALREADY owned it before this payment, " +
                    "so ownership cannot prove whether these contents landed. RE-APPLYING by ruling - a " +
                    "duplicated top-up is recoverable, a silent charge-for-nothing is not.");
                return false;
            }

            if (!ownedNow) return false;

            FlowTrace.Step(TraceSystem,
                $"interrupted settlement for '{sku}' recovered: the SKU is owned and was not owned before " +
                "this payment, so the grant landed. Promoting the journal entry to applied.");
            WriteJournal(key, StateApplied + "|" + sku);
            return true;
        }

        private static void WriteJournal(string key, string value)
        {
            PlayerPrefs.SetString(JournalPrefix + key, value);
            PlayerPrefs.Save();
        }

        private static bool TryJournalKey(string piPaymentId, out string key)
        {
            key = null;
            if (string.IsNullOrEmpty(piPaymentId)) return false;
            try
            {
                using var sha = SHA256.Create();
                var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(piPaymentId));
                var sb = new StringBuilder(hash.Length * 2);
                foreach (var b in hash) sb.Append(b.ToString("x2"));
                key = sb.ToString();
                return true;
            }
            catch (Exception e)
            {
                FlowTrace.Fail(TraceSystem, $"journal key could not be derived: {e.GetType().Name}: {e.Message}");
                return false;
            }
        }
    }
}
