// =============================================================================
// GooglePlayGrantApplier — the durable IGooglePlayGrantApplier WO-1255 said did not
// exist (WO-1282 settlement lane).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.PaymentProviders.GooglePlay   Namespace: DeNelle.Core.Payments.Providers
//
// WHAT IT IS FOR. GooglePlayReceiptSettlement.SettleAsync runs
//   parse token -> server verify -> LOCAL APPLY EXACTLY ONCE -> server fulfill
// and GooglePlayBillingProvider confirms the Unity order only after that returns true.
// This class is the middle step. Google WILL re-deliver a purchase (app restart,
// restore, network retry, a crash before ConfirmPurchase), so the same purchase token
// can arrive many times and must grant exactly once.
//
// ── HOW EXACTLY-ONCE IS ACTUALLY ACHIEVED, AND WHERE IT IS NOT PERFECT ────────
// There is no single durable store holding BOTH the token marker and the pack
// mutation, so a literal one-write commit is not available: the grant lands in the
// GameState save (GameStateService.Save) and the marker lands in PlayerPrefs. What is
// built instead is a WRITE-AHEAD JOURNAL with recovery by evidence:
//
//   1. read the pack's CURRENT ownership  -> preOwned
//   2. journal "pending|<sku>|<preOwned>" + PlayerPrefs.Save()      (write-ahead)
//   3. PackGrantBridge.TryApply(sku) -> PackStoreVM.ApplyPackContents -> save
//   4. verify the SKU is now owned; if not, return false and leave the journal
//      PENDING so the Play order stays pending and Google re-delivers
//   5. journal "applied|<sku>" + PlayerPrefs.Save()
//
// A crash between 2 and 3 leaves "pending" with the grant NOT landed -> IsApplied
// reports false -> the redelivery grants. Correct.
// A crash between 3 and 5 leaves "pending" with the grant LANDED. IsApplied recovers
// that case by asking the save: preOwned==false and the SKU is owned NOW means the
// grant took, so the entry is promoted to "applied" and never re-granted.
//
// ⛔ THE ONE RESIDUAL AMBIGUITY, DECLARED RATHER THAN HIDDEN. If the player already
//    owned that SKU before this purchase (a repeat consumable buy) AND we crash
//    between 3 and 5, ownership cannot tell us whether this token's contents landed.
//    Both answers are wrong in one direction. We choose to RE-APPLY, because the
//    failure WO-1282 exists to prevent is "Google took the money and nothing granted";
//    a duplicated top-up is recoverable by support, a silent charge-for-nothing is not.
//    The choice is logged as a FlowTrace.Warn every time it is taken, so it is visible
//    in a device capture instead of being inferred later.
//
// ⛔ FAIL CLOSED, ALWAYS. Every unknown returns false. A false here means "not
//    granted", the Unity order is NOT confirmed, and Google re-delivers — which is the
//    safe direction. Never return true to "unstick" a purchase.
//
// ⛔ THE RAW PURCHASE TOKEN IS NEVER WRITTEN DOWN. PlayerPrefs on Android is readable
//    by a backup (the reason BackendRequestSigner keeps its session in memory only and
//    MwaSessionStore seals its token), and a purchase token is a credential the server
//    verifies against Google. The journal is keyed by SHA-256 of the token, so the
//    marker is durable while the token itself is not persisted anywhere on device.
// =============================================================================

using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using DeNelle.Commerce;
using DeNelle.Core.Diagnostics;
// PackCatalog lives in the DeNelle.Commerce ASSEMBLY but kept the DeNelle.Wallet
// NAMESPACE deliberately (WO-1282 Lane A): PromoCodeService.cs:334-335 resolves
// "DeNelle.Wallet.PackContents"/"DeNelle.Wallet.PackStoreVM" as reflection string
// literals, so the namespace is a live runtime contract. This using is the namespace,
// not a reference to the excluded DeNelle.Wallet assembly.
using DeNelle.Wallet;

namespace DeNelle.Core.Payments.Providers
{
    /// <summary>
    /// Durable, token-idempotent local grant for Google Play settlement. Records the purchase-token
    /// marker around the pack mutation as a write-ahead journal with recovery by ownership evidence.
    /// </summary>
    public sealed class GooglePlayGrantApplier : IGooglePlayGrantApplier
    {
        /// <summary>FlowTrace system tag. Distinct from "Store" so a settlement trace is greppable.</summary>
        private const string TraceSystem = "PlayBilling";

        /// <summary>PlayerPrefs key prefix. The suffix is SHA-256(token), never the token.</summary>
        private const string JournalPrefix = "gp.settle.";

        private const string StateApplied = "applied";
        private const string StatePending = "pending";

        /// <summary>
        /// True when this token's grant is already durably recorded as applied — or can be PROVEN
        /// applied from a write-ahead entry plus the live save. Anything else is false, because a
        /// false only ever causes a re-apply, while a wrong true silently eats a paid purchase.
        /// </summary>
        public bool IsApplied(string purchaseToken)
        {
            if (!TryJournalKey(purchaseToken, out var key)) return false;

            string entry = ReadJournal(key);
            if (string.IsNullOrEmpty(entry)) return false;

            if (!TryParseEntry(entry, out var state, out var sku, out bool preOwned))
            {
                FlowTrace.Warn(TraceSystem,
                    "settlement journal entry is unreadable; treating as NOT applied so the grant is " +
                    "retried rather than lost.");
                return false;
            }

            if (string.Equals(state, StateApplied, StringComparison.Ordinal))
            {
                FlowTrace.Step(TraceSystem, $"token already settled for '{sku}' — no second grant.");
                return true;
            }

            // state == pending: the write-ahead entry survived a crash. Ask the save whether the
            // grant actually landed. Only a pack that was NOT owned before this purchase can be
            // answered from ownership alone.
            if (preOwned)
            {
                FlowTrace.Warn(TraceSystem,
                    $"interrupted settlement for '{sku}' cannot be resolved from ownership (the SKU was " +
                    "ALREADY owned before this purchase), so the grant is being RE-APPLIED. Chosen " +
                    "deliberately: a duplicate top-up is recoverable, a charge with no grant is not.");
                return false;
            }

            if (!PackGrantBridge.TryIsOwned(sku, out bool ownedNow))
            {
                FlowTrace.Warn(TraceSystem,
                    $"interrupted settlement for '{sku}': ownership is UNKNOWN (no local grant applier " +
                    "in this build). Reporting not-applied; ApplyExactlyOnceAsync will fail closed.");
                return false;
            }

            if (!ownedNow) return false;

            FlowTrace.Step(TraceSystem,
                $"interrupted settlement for '{sku}' recovered: the SKU is owned and was not owned " +
                "before this purchase, so the grant landed. Promoting the journal entry to applied.");
            WriteJournal(key, StateApplied + "|" + sku);
            return true;
        }

        /// <summary>
        /// Applies the pack for this token exactly once. Returns true only when the entitlement is
        /// proven present in the save afterwards; any other outcome returns false and leaves the
        /// Play order pending so Google re-delivers.
        /// </summary>
        public Task<bool> ApplyExactlyOnceAsync(string sku, string purchaseToken)
        {
            using var _ = FlowTrace.Enter(TraceSystem, $"ApplyExactlyOnce '{sku}'");

            if (string.IsNullOrWhiteSpace(sku) || !TryJournalKey(purchaseToken, out var key))
            {
                FlowTrace.Fail(TraceSystem, "grant refused: empty SKU or unusable purchase token.");
                return Task.FromResult(false);
            }

            if (PackCatalog.Find(sku) == null)
            {
                FlowTrace.Fail(TraceSystem,
                    $"grant refused: '{sku}' is not in the pack catalog. A verified payment for an " +
                    "unknown SKU must never invent contents.");
                return Task.FromResult(false);
            }

            if (IsApplied(purchaseToken)) return Task.FromResult(true);

            if (!PackGrantBridge.HasApplier)
            {
                // Named both ways so a capture is never ambiguous (CLAUDE.md §12).
                FlowTrace.Fail(TraceSystem,
                    $"grant refused for '{sku}': no local pack grant applier. EXPECTED on a Google Play " +
                    "artifact, which compiles out DeNelle.Wallet and therefore carries no entitlement " +
                    "writer yet — settlement correctly fails closed and the order stays pending. On a " +
                    "build where DeNelle.Wallet IS present this is a DEFECT: PackStoreBootstrap did not " +
                    "run and a paid pack is being refused.");
                return Task.FromResult(false);
            }

            if (!PackGrantBridge.TryIsOwned(sku, out bool preOwned))
            {
                FlowTrace.Fail(TraceSystem,
                    $"grant refused for '{sku}': the ownership probe did not resolve, so an interrupted " +
                    "retry could not be told apart from a first apply. Failing closed.");
                return Task.FromResult(false);
            }

            // WRITE-AHEAD. Persist the intent BEFORE mutating, so a crash mid-grant is recoverable
            // rather than invisible. Ordering is load-bearing: marking after the mutation would make
            // a crash between them look like a fresh purchase and grant twice.
            WriteJournal(key, StatePending + "|" + sku + "|" + (preOwned ? "1" : "0"));

            bool granted = PackGrantBridge.TryApply(sku);
            if (!granted)
            {
                FlowTrace.Fail(TraceSystem,
                    $"grant for '{sku}' did NOT take. The journal entry stays PENDING and the Play order " +
                    "is NOT confirmed, so Google re-delivers and this retries. The player is not charged " +
                    "for nothing — but if this repeats, the entitlement writer is broken.");
                return Task.FromResult(false);
            }

            WriteJournal(key, StateApplied + "|" + sku);
            FlowTrace.Step(TraceSystem, $"grant for '{sku}' applied and journalled as settled.");
            return Task.FromResult(true);
        }

        // ── Journal (PlayerPrefs; keyed by SHA-256 of the token, never the token) ──

        private static bool TryJournalKey(string purchaseToken, out string key)
        {
            key = null;
            if (string.IsNullOrWhiteSpace(purchaseToken)) return false;
            try
            {
                using var sha = SHA256.Create();
                var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(purchaseToken));
                var sb = new StringBuilder(hash.Length * 2);
                foreach (var b in hash) sb.Append(b.ToString("x2"));
                key = JournalPrefix + sb.ToString();
                return true;
            }
            catch (Exception ex)
            {
                FlowTrace.Fail(TraceSystem, "settlement journal key could not be derived: " + ex.GetType().Name);
                return false;
            }
        }

        private static string ReadJournal(string key)
        {
            string value = null;
            Guard.Try(TraceSystem, "read settlement journal", () => { value = PlayerPrefs.GetString(key, string.Empty); });
            return value;
        }

        /// <summary>
        /// Writes the journal entry and FLUSHES it. PlayerPrefs.Save() is not optional here: an
        /// unflushed marker is lost on a crash, which is precisely the window this journal exists
        /// to survive.
        /// </summary>
        private static void WriteJournal(string key, string entry)
        {
            Guard.Try(TraceSystem, "write settlement journal", () =>
            {
                PlayerPrefs.SetString(key, entry);
                PlayerPrefs.Save();
            });
        }

        private static bool TryParseEntry(string entry, out string state, out string sku, out bool preOwned)
        {
            state = null;
            sku = null;
            preOwned = false;
            if (string.IsNullOrEmpty(entry)) return false;
            var parts = entry.Split('|');
            if (parts.Length < 2) return false;
            state = parts[0];
            sku = parts[1];
            if (string.IsNullOrWhiteSpace(sku)) return false;
            if (string.Equals(state, StateApplied, StringComparison.Ordinal)) return true;
            if (!string.Equals(state, StatePending, StringComparison.Ordinal)) return false;
            // A pending entry MUST carry its preOwned flag; without it we cannot resolve a crash,
            // and guessing "not owned" could grant a second time.
            if (parts.Length < 3) return false;
            preOwned = string.Equals(parts[2], "1", StringComparison.Ordinal);
            return true;
        }
    }
}
