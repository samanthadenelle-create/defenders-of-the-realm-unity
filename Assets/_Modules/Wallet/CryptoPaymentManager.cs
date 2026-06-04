// =============================================================================
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
            if (_wallet == null) return;

            var account = await _wallet.Connect();
            if (account.IsValid)
                Debug.Log($"[CryptoPaymentManager] Wallet connected: {account.Address}");
            else
                Debug.LogWarning("[CryptoPaymentManager] Wallet connection cancelled or failed.");
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
                Debug.Log($"[CryptoPaymentManager] SKR payment → {finalAether} Glimmer granted (with bonus).");

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
            if (_wallet == null) return false;

            // Ensure wallet is connected before attempting payment.
            if (!_wallet.IsConnected)
            {
                await ConnectWallet();
                if (!_wallet.IsConnected) return false;
            }

            PaymentResult result;
            try
            {
                result = await _wallet.PayFlat(txId, currency, amount);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CryptoPaymentManager] Payment exception ({currency}): {ex.Message}");
                return false;
            }

            if (!result.Ok)
            {
                Debug.LogWarning($"[CryptoPaymentManager] Payment failed ({currency}): {result.Error}");
                return false;
            }

            // Grant Glimmer (the branch's soft currency — analogous to Aether Shards).
            GrantGlimmer(glimmerReward, currency);

            Debug.Log($"[CryptoPaymentManager] Payment confirmed → {glimmerReward} Glimmer granted " +
                $"({currency}, tx: {result.TxSignature}).");
            return true;
        }

        // ── Glimmer grant ─────────────────────────────────────────────────────

        private static void GrantGlimmer(int amount, CurrencyKind fromCurrency)
        {
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
            var svc = t?.GetProperty("Instance",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public)?.GetValue(null);
            if (svc == null)
            {
                Debug.LogWarning("[CryptoPaymentManager] GlimmerCurrencyService unavailable — Glimmer not granted.");
                return;
            }
            t.GetMethod("TryAddGlimmer", new[] { typeof(int) })?.Invoke(svc, new object[] { amount });
            Debug.Log($"[CryptoPaymentManager] +{amount} Glimmer via {fromCurrency}.");
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
                Debug.LogWarning("[CryptoPaymentManager] StakingBonusManager invoke failed: " + ex.Message);
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
