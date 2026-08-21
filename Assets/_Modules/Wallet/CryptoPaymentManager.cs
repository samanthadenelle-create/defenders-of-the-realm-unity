// =============================================================================
// ⛔⛔ DORMANT AND UNTOUCHABLE — 2026-08-21 (WO-992). DO NOT DELETE. DO NOT WIRE.
//     THIS IS AN OWNER DECISION AND ONLY AN OWNER DECISION.
// -----------------------------------------------------------------------------
// THREE FACTS THAT TOGETHER FORBID AN AGENT ACTING ON THIS FILE:
//   1. THE GAME IS LIVE ON THE SOLANA dAPP STORE. The next submission is an
//      UPDATE to a shipped app, not a first listing.
//   2. THIS IS THE PAYMENT PATH. PayWithSOL/SKR/USDC -> SendFlatPayment ->
//      GrantGlimmer. Deleting or re-wiring a payment-adjacent class is never an
//      agent's call, at any confidence level.
//   3. A GLIMMER PURGE IS ALREADY PENDING AN OWNER MIGRATION RULING (WO-1126),
//      and GrantGlimmer (:235, called from :209) is squarely inside its blast
//      radius. Acting here would pre-empt a decision that has not been made.
//
// MEASURED STATE (2026-08-21, GUID-verified, not name-grepped):
//   • GUID d675552ee54bfb2438c2c53102c0eaec: ZERO hits in .unity / .prefab /
//     .asset, including a raw-byte scan of the 12 binary scenes.
//   • Zero AddComponent / new / GetComponent / Find*. `Instance` is read
//     nowhere; BuyWithSOL/SKR/USDC (:371-377) are wired to no Button. The only
//     two mentions outside this file are COMMENTS — GlimmerCurrencyService.cs:200
//     (inside a log STRING, not a type reference) and PackStoreVM.cs:190.
//
// ⚠ THE OWNER'S READ NEEDS CORRECTING, AND THAT IS THE FINDING.
//   She recorded these WO-73/74 classes as "ideas not implementations yet"
//   (2026-08-14). THAT IS NOT TRUE OF THIS ONE. 379 lines, heavily instrumented
//   (22 FlowTrace sites), and the most carefully-written failure handling in the
//   batch: a connect-retry (:172-181), an explicit THROW=INDETERMINATE branch
//   (:188-196), the entitlement-gap block at :205-221 that logs "PLAYER CHARGED,
//   ENTITLEMENT LOST. Needs reconciliation." and still returns true so the caller
//   cannot double-charge, and a GrantGlimmer bridge that reads the balance BEFORE
//   AND AFTER and asserts the delta (:267-271, :304-318) because "a true-returning
//   invoke whose balance didn't change is still a loss". Six distinct Fail paths.
//   This is a finished implementation that was never seated — which changes the
//   disposition entirely: a complete implementation is worth WIRING; a scaffold
//   is worth deleting. It is not a scaffold.
//
// ⚠ AND NOTE WHY IT IS ORPHANED: the shipped store path (PackStoreVM) went live
//   and REIMPLEMENTED the same reflection bridge rather than calling this class
//   — see its own comment at PackStoreVM.cs:190. So the live purchase path and
//   this one are duplicate implementations of the same grant. Deduplicating them
//   is real work with real money attached; it is a ticket, not a cleanup.
//
// ⚠ One dependency is genuinely absent: TryApplyStakingBonus (:335) reflects for
//   WO-76 StakingBonusManager, which DOES NOT EXIST in the tree (referenced only
//   by string at :342). It degrades to identity by design.
// -----------------------------------------------------------------------------
// CryptoPaymentManager — WO-74 Solana crypto payment bridge (reconciled).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Wallet  Namespace: DeNelle.Wallet
//
// Reconciliation vs WO-74 spec:
//   • WO-74 wrote this as a standalone MonoBehaviour using raw Solana Unity SDK
//     types (Web3, WalletBase, PublicKey, etc.) directly. This branch already
//     has a complete, tested, #if SOLANA_SDK-guarded seam:
//       WalletService → IWalletProvider → SolanaWalletProvider (real SDK)
//                                       → StubWalletProvider  (devnet mock)
//     Duplicating SDK calls outside that seam would bypass the devnet-only guard,
//     the treasury address config, and the #if SOLANA_SDK isolation that keeps
//     the project compiling without the SDK package.
//   • This class is therefore a THIN BRIDGE: it delegates to the shared
//     WalletService singleton (or creates one from the stub) for all wallet ops.
//     The ShopUI / BattlePassSystem can call this for Aether/Glimmer top-ups;
//     under the hood it flows through the same provider path.
//   • The WO-74 PayWithSOL/SKR/USDC methods are async UniTask<bool>, matching
//     the existing codebase (never async Task, never async void — UniTask mandate).
//   • SKR 25% bonus is applied before the WalletService.PayFlat call.
//   • StakingBonusManager (WO-76) may not exist yet; guarded with null-check.
//   • aetherReward → Glimmer grant (GlimmerCurrencyService) because "Aether
//     Shards" don't exist on this branch; Glimmer is the actual soft currency.
//   • ConnectWallet() delegates to WalletService.Connect().
//   • All #if SOLANA_SDK guards live in SolanaWalletProvider; this file compiles
//     unconditionally (no SDK types imported here).
// =============================================================================

using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Wallet
{
    /// <summary>
    /// Singleton MonoBehaviour that exposes simple crypto payment entry points
    /// to the Shop UI and Battle Pass. Internally delegates to
    /// <see cref="WalletService"/> (which routes to <see cref="SolanaWalletProvider"/>
    /// when the Solana Unity SDK is installed, or <see cref="StubWalletProvider"/>
    /// on devnet/editor).
    ///
    /// Add this to your persistent manager GameObject alongside
    /// <see cref="PackStore"/> and the wallet connect dialog.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CryptoPaymentManager : MonoBehaviour
    {
        public static CryptoPaymentManager Instance { get; private set; }

        // ── Inspector ─────────────────────────────────────────────────────────
        [Header("SKR Bonus")]
        [Range(1.0f, 2.0f)]
        [Tooltip("Aether bonus multiplier when the player pays in SKR. Default 1.25 (25% extra).")]
        public float skrBonusMultiplier = 1.25f;

        [Header("Conversion: Aether → SOL/USDC")]
        [Tooltip("SOL per 1 Aether Shard. Tune per economy design.")]
        public double aetherToSol  = 0.001;

        [Tooltip("USDC per 1 Aether Shard. Tune per economy design.")]
        public double aetherToUsdc = 0.05;

        [Tooltip("SKR per 1 Aether Shard (before bonus). Tune per economy design.")]
        public double aetherToSkr  = 0.1;

        // ── Runtime ───────────────────────────────────────────────────────────
        private WalletService _wallet;

        // ── Unity lifecycle ───────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Auto-selects SolanaWalletProvider when SOLANA_SDK define is set;
            // falls back to StubWalletProvider on devnet / no-SDK builds.
            _wallet = new WalletService();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ── Wallet connect ────────────────────────────────────────────────────

        /// <summary>
        /// Open the wallet-connect flow. Called by ShopUI.OnEnable / an [F]-key
        /// handler before the first payment. Safe to call when already connected
        /// (returns immediately).
        /// </summary>
        public async UniTask ConnectWallet()
        {
            using var _ = FlowTrace.Enter("CryptoPay", "ConnectWallet");
            if (_wallet == null)
            {
                FlowTrace.Fail("CryptoPay", "ConnectWallet: no WalletService — cannot connect.");
                return;
            }

            var account = await _wallet.Connect();
            if (account.IsValid)
                FlowTrace.Step("CryptoPay", $"Wallet connected: {account.Address}");
            else
                FlowTrace.Warn("CryptoPay", "Wallet connection cancelled or failed.");
        }

        // ── Payment entry points ──────────────────────────────────────────────

        /// <summary>
        /// Pay in SOL, then grant <paramref name="aetherAmount"/> Glimmer on success.
        /// Returns true on a confirmed transaction.
        /// </summary>
        public async UniTask<bool> PayWithSOL(int aetherAmount)
        {
            double solAmount = aetherAmount * aetherToSol;
            return await SendFlatPayment(CurrencyKind.Sol, solAmount, aetherAmount, "sol-aether");
        }

        /// <summary>
        /// Pay in SKR, applying the <see cref="skrBonusMultiplier"/> bonus (default +25%),
        /// then grant the boosted Glimmer amount on success.
        /// </summary>
        public async UniTask<bool> PayWithSKR(int aetherAmount)
        {
            // Optional staking bonus — WO-76 StakingBonusManager (may not exist yet).
            int baseBoosted = Mathf.RoundToInt(aetherAmount * skrBonusMultiplier);
            int finalAether = TryApplyStakingBonus(baseBoosted);

            double skrAmount = aetherAmount * aetherToSkr;
            bool ok = await SendFlatPayment(CurrencyKind.Skr, skrAmount, finalAether, "skr-aether");

            if (ok)
                FlowTrace.Step("CryptoPay", $"SKR payment -> {finalAether} Glimmer granted (with bonus).");

            return ok;
        }

        /// <summary>
        /// Pay in USDC, then grant <paramref name="aetherAmount"/> Glimmer on success.
        /// Returns true on a confirmed transaction.
        /// </summary>
        public async UniTask<bool> PayWithUSDC(int aetherAmount)
        {
            double usdcAmount = aetherAmount * aetherToUsdc;
            return await SendFlatPayment(CurrencyKind.Usdc, usdcAmount, aetherAmount, "usdc-aether");
        }

        // ── Core transaction ──────────────────────────────────────────────────

        private async UniTask<bool> SendFlatPayment(
            CurrencyKind currency, double amount, int glimmerReward, string txId)
        {
            using var _ = FlowTrace.Enter("CryptoPay",
                $"SendFlatPayment {currency} amount={amount} reward={glimmerReward} tx='{txId}'");

            if (_wallet == null)
            {
                FlowTrace.Fail("CryptoPay", $"SendFlatPayment: no WalletService — payment aborted ({currency}).");
                return false;
            }

            // Ensure wallet is connected before attempting payment.
            if (!_wallet.IsConnected)
            {
                await ConnectWallet();
                if (!_wallet.IsConnected)
                {
                    FlowTrace.Warn("CryptoPay",
                        $"SendFlatPayment: wallet still not connected after connect attempt — payment aborted ({currency}). Player NOT charged.");
                    return false;
                }
            }

            PaymentResult result;
            try
            {
                result = await _wallet.PayFlat(txId, currency, amount);
            }
            catch (Exception ex)
            {
                // Threw mid-transfer: we do NOT know if the chain debited. Surface loudly so the
                // break-log carries the exception — a possible charge with no grant must self-report.
                FlowTrace.Fail("CryptoPay",
                    $"SendFlatPayment: PayFlat THREW ({currency}, tx='{txId}'): {ex.GetType().Name}: {ex.Message} — " +
                    "payment indeterminate, no Glimmer granted.");
                return false;
            }

            if (!result.Ok)
            {
                FlowTrace.Warn("CryptoPay",
                    $"SendFlatPayment: payment failed ({currency}, tx='{txId}'): {result.Error} — no Glimmer granted (player NOT charged on a clean failure).");
                return false;
            }

            // CRITICAL ENTITLEMENT GAP: the payment CONFIRMED — the player IS charged from here on.
            // The grant MUST take, or the player paid for nothing. GrantGlimmer self-verifies the
            // balance moved and Fails loudly (-> break-log) if the reflected grant didn't land, so a
            // lost entitlement is never silent.
            bool granted = GrantGlimmer(glimmerReward, currency, txId);
            if (!granted)
            {
                // Payment took but entitlement did NOT — the worst case. Already Fail-logged inside
                // GrantGlimmer with the tx signature; re-state it here at the flow level so the
                // capture clearly pairs the confirmed tx with the lost grant.
                FlowTrace.Fail("CryptoPay",
                    $"SendFlatPayment: payment CONFIRMED (tx: {result.TxSignature}, {currency}) but the " +
                    $"{glimmerReward}-Glimmer grant did NOT take — PLAYER CHARGED, ENTITLEMENT LOST. Needs reconciliation.");
                // Still return true: the transaction settled on-chain; the caller must not retry the
                // charge. The Fail above is the signal for support/reconciliation, not a re-charge.
                return true;
            }

            FlowTrace.Step("CryptoPay",
                $"Payment confirmed -> {glimmerReward} Glimmer granted ({currency}, tx: {result.TxSignature}).");
            return true;
        }

        // ── Glimmer grant ─────────────────────────────────────────────────────

        // Grant the purchased Glimmer and VERIFY it actually landed. Returns true ONLY when the
        // reflected TryAddGlimmer ran AND the player's balance moved by the expected amount. Any
        // failure (service missing, method missing, invoke threw, returned false, or balance did NOT
        // change) Fails loudly via FlowTrace -> break-log, because by the time this is called the
        // player has ALREADY been charged: a silent failure here = a lost, paid-for entitlement.
        private static bool GrantGlimmer(int amount, CurrencyKind fromCurrency, string txId)
        {
            using var _ = FlowTrace.Enter("CryptoPay", $"GrantGlimmer +{amount} via {fromCurrency} (tx='{txId}')");

            // DeNelle.Wallet cannot reference DeNelle.Cosmetics (Cosmetics already
            // references Wallet → circular asmdef), so resolve GlimmerCurrencyService
            // and call TryAddGlimmer(int) by reflection — same cross-assembly bridge
            // pattern as TryApplyStakingBonus below.
            System.Type t = null;
            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                t = asm.GetType("DeNelle.Cosmetics.GlimmerCurrencyService");
                if (t != null) break;
            }
            if (t == null)
            {
                FlowTrace.Fail("CryptoPay",
                    $"GrantGlimmer: GlimmerCurrencyService TYPE not found — {amount} paid-for Glimmer LOST " +
                    $"(charged via {fromCurrency}, tx='{txId}').");
                return false;
            }

            var svc = t.GetProperty("Instance",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public)?.GetValue(null);
            if (svc == null)
            {
                FlowTrace.Fail("CryptoPay",
                    $"GrantGlimmer: GlimmerCurrencyService.Instance is null (service not in scene) — {amount} paid-for " +
                    $"Glimmer LOST (charged via {fromCurrency}, tx='{txId}').");
                return false;
            }

            // Read the balance BEFORE so we can PROVE the grant moved it. The public 'Glimmer' int
            // property is the verifiable balance.
            var balanceProp = t.GetProperty("Glimmer",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
            int before = (balanceProp != null && balanceProp.GetValue(svc) is int b) ? b : int.MinValue;

            var addMethod = t.GetMethod("TryAddGlimmer", new[] { typeof(int) });
            if (addMethod == null)
            {
                FlowTrace.Fail("CryptoPay",
                    $"GrantGlimmer: TryAddGlimmer(int) method not found on GlimmerCurrencyService — {amount} paid-for " +
                    $"Glimmer LOST (charged via {fromCurrency}, tx='{txId}').");
                return false;
            }

            object invokeResult;
            try
            {
                invokeResult = addMethod.Invoke(svc, new object[] { amount });
            }
            catch (Exception ex)
            {
                FlowTrace.Fail("CryptoPay",
                    $"GrantGlimmer: TryAddGlimmer THREW for +{amount} ({fromCurrency}, tx='{txId}'): " +
                    $"{ex.GetType().Name}: {ex.Message} — paid-for Glimmer LOST.");
                return false;
            }

            // TryAddGlimmer returns bool — false means the service itself rejected the grant.
            if (invokeResult is bool ok && !ok)
            {
                FlowTrace.Fail("CryptoPay",
                    $"GrantGlimmer: TryAddGlimmer RETURNED FALSE for +{amount} ({fromCurrency}, tx='{txId}') — " +
                    "grant rejected, paid-for Glimmer LOST.");
                return false;
            }

            // VERIFY the balance actually moved by the expected amount. This is the real proof the
            // entitlement took — a true-returning invoke whose balance didn't change is still a loss.
            if (before != int.MinValue && balanceProp != null)
            {
                int after = (balanceProp.GetValue(svc) is int a) ? a : int.MinValue;
                if (after == int.MinValue || after - before != amount)
                {
                    FlowTrace.Fail("CryptoPay",
                        $"GrantGlimmer: balance did NOT move by +{amount} ({fromCurrency}, tx='{txId}'): " +
                        $"before={before} after={after} (delta={after - before}) — entitlement NOT applied, paid-for Glimmer LOST.");
                    return false;
                }
                FlowTrace.Step("CryptoPay",
                    $"GrantGlimmer verified: +{amount} via {fromCurrency} (balance {before} -> {after}, tx='{txId}').");
            }
            else
            {
                // Could not read the balance to verify the delta — the grant invoke succeeded but we
                // cannot PROVE it landed. Warn (not Fail): the bool said ok, but flag the blind spot.
                FlowTrace.Warn("CryptoPay",
                    $"GrantGlimmer: +{amount} via {fromCurrency} invoked OK but balance was unreadable for verify (tx='{txId}') — grant assumed applied.");
            }
            return true;
        }

        // ── StakingBonusManager (WO-76) optional hook ─────────────────────────

        /// <summary>
        /// Applies staking bonus if StakingBonusManager exists (WO-76, not yet built).
        /// Returns the input amount unchanged when the manager is absent.
        /// </summary>
        private static int TryApplyStakingBonus(int amount)
        {
            // Resolved via reflection so this file compiles whether or not WO-76 exists.
            try
            {
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    var t = asm.GetType("StakingBonusManager", false);
                    if (t == null) continue;

                    var instProp = t.GetProperty("Instance",
                        System.Reflection.BindingFlags.Public |
                        System.Reflection.BindingFlags.Static);
                    var inst = instProp?.GetValue(null);
                    if (inst == null) return amount;

                    var method = t.GetMethod("ApplyBonusToAether", new[] { typeof(int) });
                    if (method == null) return amount;

                    var result = method.Invoke(inst, new object[] { amount });
                    return result is int r ? r : amount;
                }
            }
            catch (Exception ex)
            {
                FlowTrace.Warn("CryptoPay", "StakingBonusManager invoke failed (bonus skipped): " + ex.Message);
            }
            return amount;
        }

        // ── Null-safe sync wrappers (for Button.onClick and legacy callers) ───

        /// <summary>
        /// Fire-and-forget wrapper for Button.onClick compatibility.
        /// Logs errors; does not return a result.
        /// </summary>
        public void BuyWithSOL(int aetherAmount)  => PayWithSOL(aetherAmount).Forget();

        /// <summary>Fire-and-forget SKR payment.</summary>
        public void BuyWithSKR(int aetherAmount)  => PayWithSKR(aetherAmount).Forget();

        /// <summary>Fire-and-forget USDC payment.</summary>
        public void BuyWithUSDC(int aetherAmount) => PayWithUSDC(aetherAmount).Forget();
    }
}
