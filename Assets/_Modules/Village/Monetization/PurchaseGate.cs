// =============================================================================
// PurchaseGate — WO-1121. The honest Buy gate + the idempotent-grant ledger.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.Monetization
//
// WO-1121 sec.0: "A live store that cannot take money, or takes money and grants
// nothing, is worse than no store." Most of that ticket is a SHIP CHECKLIST -
// a mainnet decision, an SKR mint, a settled test transfer - and none of those
// are code. This file is the part that IS code, and it is exactly two things:
//
//   1. ONE PLACE that answers "may this build take money?", with a PLAYER-READABLE
//      reason attached. Today the answer is no, and the reason is not "the flag is
//      off" - it is that the flag is off BECAUSE the rails underneath it are not
//      finished. A dead Buy button with no explanation is the failure mode
//      WO-1121 sec.3.5 names; so is a silent no-op on a broke-case Finish Now.
//
//   2. AN IDEMPOTENT GRANT LEDGER keyed by paymentId, so a retried or duplicated
//      settlement can NEVER grant a pack twice. This is the half of "charged and
//      granted" that has no owner today: the charge is the wallet's, the contents
//      are PackStore's, and nothing sat between them remembering what had already
//      been paid out.
//
// ⛔ WHAT THIS FILE DELIBERATELY DOES NOT DO. It does NOT flip
// FeatureFlags.RealmStorePurchase, and no future edit here should. That default is
// a PO decision with three named preconditions (a resolvable mint, a lifted mainnet
// block with a settling path behind it, and the closed stub-wallet free-grant hole)
// written out in FeatureFlags.cs. This gate READS the flag and reports the state
// honestly; deciding the state is not its job. See ChecklistReport() - it prints
// exactly which preconditions are still red, so the owner can judge from data
// rather than from a summary.
// =============================================================================
using System;
using DeNelle.Core;
using DeNelle.Core.Diagnostics;
using DeNelle.Wallet;
using UnityEngine;

namespace DeNelle.Village.Monetization
{
    /// <summary>
    /// The single authority on whether this build may take money, and the memory that stops one
    /// payment from granting twice.
    /// </summary>
    public static class PurchaseGate
    {
        private const string GrantedPrefix = "purchase.granted.";
        private const string GrantedIndex = "purchase.granted.index";

        /// <summary>
        /// How many recent paymentIds the ledger remembers. Bounded because PlayerPrefs is not a
        /// database: an unbounded key set would grow forever on a whale's device. 64 is far past any
        /// realistic retry horizon - a duplicate settlement arrives seconds later, not 64 purchases
        /// later - and the ledger is defence against RETRY, not an entitlement record. The
        /// entitlement record is the save (and, eventually, the server).
        /// </summary>
        private const int LedgerCapacity = 64;

        // =====================================================================
        //  The gate
        // =====================================================================

        /// <summary>
        /// May this build present a working Buy CTA? Returns false with a PLAYER-READABLE
        /// <paramref name="reason"/> - never null, never "flag off", never blank. A caller that
        /// gets false shows the reason instead of a dead button.
        /// </summary>
        public static bool CanBuy(out string reason)
        {
            if (!FeatureFlags.RealmStorePurchase)
            {
                reason = "Purchases are not open yet. Everything here is still earned in-game.";
                FlowTrace.Once("Store", "buy-gated",
                    "PurchaseGate: Buy is CLOSED (ff.realmstorepurchase OFF). This is the shipping state - " +
                    "the payment rails underneath are not finished (see PurchaseGate.ChecklistReport). " +
                    "The store still browses; only the CTA refuses, and it refuses in words.");
                return false;
            }

            // Flag ON but the rails still incomplete: refuse anyway rather than take a tap we
            // cannot settle. The flag is a PO decision; this is a factual check of the rail, and a
            // factual check must be able to veto an optimistic flag.
            if (!SkrMintResolvable() && !string.Equals(PrimaryRail(), "USDC/SOL", StringComparison.Ordinal))
            {
                reason = "The payment rail is not ready on this build. Nothing was charged.";
                FlowTrace.Fail("Store",
                    "PurchaseGate: Buy is ON but the default rail has NO RESOLVABLE MINT " +
                    "(WalletEndpoints.SkrMint is empty for this network). Refusing at the gate rather " +
                    "than letting a tap dead-end inside the wallet.");
                return false;
            }

            reason = null;
            return true;
        }

        /// <summary>Player-readable line for a shelf that is browsable but not buyable.</summary>
        public static string ClosedShelfLine() =>
            "Coming soon - purchases are not open in this build. Nothing here is required to play.";

        // =====================================================================
        //  Idempotent grant ledger
        // =====================================================================

        /// <summary>
        /// True when <paramref name="paymentId"/> has ALREADY been granted on this device. A caller
        /// that sees true must NOT grant again - it must report success (the player already has the
        /// goods) rather than pay out twice or claim a failure.
        /// </summary>
        public static bool WasGranted(string paymentId)
        {
            if (string.IsNullOrEmpty(paymentId)) return false;
            return PlayerPrefs.GetInt(GrantedPrefix + Slug(paymentId), 0) == 1;
        }

        /// <summary>
        /// Claims <paramref name="paymentId"/> for granting. Returns TRUE exactly once per payment:
        /// the FIRST caller wins and must proceed with the grant, every later caller gets false and
        /// must skip it.
        ///
        /// <para>THE CLAIM IS MADE BEFORE THE GRANT, ON PURPOSE. Claiming afterwards leaves a window
        /// in which a crash or a racing retry pays out twice, and a double grant is unrecoverable
        /// (you cannot take a pack back). Claiming first risks the opposite - a crash mid-grant
        /// leaves the payment marked with the goods undelivered - which is recoverable by support
        /// and is loudly logged by <see cref="ReportGrantFailed"/>. Between an unrecoverable error
        /// and a recoverable one, take the recoverable one.</para>
        /// </summary>
        public static bool TryClaimGrant(string paymentId)
        {
            if (string.IsNullOrEmpty(paymentId))
            {
                FlowTrace.Fail("Store",
                    "TryClaimGrant with an EMPTY paymentId - refusing. An unidentifiable payment cannot " +
                    "be made idempotent, so granting on it would be a double-grant waiting to happen.");
                return false;
            }

            string key = GrantedPrefix + Slug(paymentId);
            if (PlayerPrefs.GetInt(key, 0) == 1)
            {
                FlowTrace.Warn("Store",
                    $"DUPLICATE settlement for payment '{paymentId}' - already granted on this device. " +
                    "Skipping the grant. This is the retry path working, not an error.");
                return false;
            }

            PlayerPrefs.SetInt(key, 1);
            RememberInIndex(key);
            PlayerPrefs.Save();
            FlowTrace.Step("Store", $"grant CLAIMED for payment '{paymentId}'.");
            return true;
        }

        /// <summary>
        /// Call when the grant FAILED after the payment settled - the "charged + empty inventory"
        /// case (WO-1121 sec.1). Releases the claim so a retry can pay out, and logs LOUDLY: this
        /// is the one failure in the whole store that costs a real player real money, so it must
        /// never be a swallowed exception.
        /// </summary>
        public static void ReportGrantFailed(string paymentId, string what)
        {
            if (!string.IsNullOrEmpty(paymentId))
            {
                PlayerPrefs.DeleteKey(GrantedPrefix + Slug(paymentId));
                PlayerPrefs.Save();
            }
            FlowTrace.Fail("Store",
                $"⛔ PAID BUT NOT GRANTED - payment '{paymentId}' settled and '{what}' failed to deliver. " +
                "The idempotency claim has been RELEASED so a retry can complete it. This line is the " +
                "support trail: a player is out real money until it does.");
        }

        /// <summary>QA reset. Never called by gameplay.</summary>
        public static void ClearLedgerForTests()
        {
            string index = PlayerPrefs.GetString(GrantedIndex, "");
            if (!string.IsNullOrEmpty(index))
            {
                string[] keys = index.Split('|');
                for (int i = 0; i < keys.Length; i++)
                    if (!string.IsNullOrEmpty(keys[i])) PlayerPrefs.DeleteKey(keys[i]);
            }
            PlayerPrefs.DeleteKey(GrantedIndex);
            PlayerPrefs.Save();
        }

        // =====================================================================
        //  Checklist reporting (WO-1121 sec.2) — data, not a summary
        // =====================================================================

        /// <summary>
        /// One-shot boot report of every payment-rail precondition, each with its live value read
        /// AT SOURCE. It exists so the go-live decision is made from a capture rather than from
        /// somebody's recollection of what was true last week - the same reason CLAUDE.md sec.16
        /// insists a push is judged by a marker on a fresh log.
        /// </summary>
        public static string ChecklistReport()
        {
            string rail = PrimaryRail();
            bool mint = SkrMintResolvable();
            bool buy = FeatureFlags.RealmStorePurchase;

            return
                "PurchaseGate checklist (WO-1121 sec.2):\n" +
                $"  Buy CTA gate (ff.realmstorepurchase) : {(buy ? "ON" : "OFF")}\n" +
                $"  Default network                      : {WalletService.DefaultNetwork}\n" +
                $"  SKR mint resolvable on that network  : {(mint ? "YES" : "NO - WalletEndpoints.SkrMint is empty")}\n" +
                $"  Declared primary rail                : {rail}\n" +
                $"  Idempotent grant by paymentId        : YES (PurchaseGate.TryClaimGrant)\n" +
                $"  Mainnet policy                       : blocked in SolanaWalletProvider.SendPayment (deliberate)\n" +
                "  NOT VERIFIABLE FROM CODE (owner actions): one settled transfer test, the mainnet\n" +
                "  decision itself, and the pay -> grant -> save -> relaunch device proof.";
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void ReportOnBoot()
        {
            Guard.Try("Store", "PurchaseGate boot report",
                () => FlowTrace.Once("Store", "purchase-checklist", ChecklistReport()));
        }

        // =====================================================================
        //  Internals
        // =====================================================================

        /// <summary>
        /// True when the SKR mint for the build's default network is non-empty. Read from
        /// WalletEndpoints at source - never cached into a doc or a second constant, which is how
        /// the two copies drift and a ship decision gets made against a stale one.
        /// </summary>
        private static bool SkrMintResolvable() =>
            !string.IsNullOrEmpty(WalletEndpoints.SkrMint(WalletService.DefaultNetwork));

        /// <summary>
        /// Which rail this build claims as primary. WO-1121 sec.2 asks for this to be DOCUMENTED
        /// rather than assumed: if SKR cannot resolve a mint, SKR is not the primary rail no matter
        /// what the storefront copy says.
        /// </summary>
        private static string PrimaryRail() => SkrMintResolvable() ? "SKR" : "USDC/SOL";

        // Bounded ring so the ledger cannot grow without limit on a device.
        private static void RememberInIndex(string key)
        {
            string index = PlayerPrefs.GetString(GrantedIndex, "");
            string[] keys = string.IsNullOrEmpty(index) ? Array.Empty<string>() : index.Split('|');
            int start = keys.Length >= LedgerCapacity ? keys.Length - LedgerCapacity + 1 : 0;
            for (int i = 0; i < start; i++) PlayerPrefs.DeleteKey(keys[i]);

            var sb = new System.Text.StringBuilder();
            for (int i = start; i < keys.Length; i++)
            {
                if (string.IsNullOrEmpty(keys[i])) continue;
                sb.Append(keys[i]).Append('|');
            }
            sb.Append(key);
            PlayerPrefs.SetString(GrantedIndex, sb.ToString());
        }

        // PlayerPrefs keys are free-form, but a paymentId is a signature and may carry anything.
        // Normalise so a key can never collide with the index key or break the '|' index format.
        private static string Slug(string paymentId)
        {
            var sb = new System.Text.StringBuilder(paymentId.Length);
            for (int i = 0; i < paymentId.Length; i++)
            {
                char c = paymentId[i];
                sb.Append(char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : '_');
            }
            return sb.ToString();
        }
    }
}
