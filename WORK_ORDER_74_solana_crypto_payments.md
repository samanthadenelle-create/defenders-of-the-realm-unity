# WORK ORDER 74 — Solana Crypto Payments (SOL + SKR + USDC)

**Status:** READY TO IMPLEMENT
**Date:** 2026-05-28
**Priority:** Critical
**Scope:** Medium — one new script + SDK install + ShopUI hooks
**Depends on:** WO-73 (ShopUI, MonetizationManager)

---

## Goal

Let players buy Aether Shards, Battle Pass, and cosmetics directly with SOL,
SKR (your token — with a 25% bonus to encourage usage), or USDC. Works
alongside the existing `MonetizationManager`, `BattlePassSystem`, and `ShopUI`.

---

## 1. Install the Solana Unity SDK

1. Open **Window → Package Manager**.
2. Click **+** → **Add package from git URL**:
   ```
   https://github.com/magicblock-labs/Solana.Unity-SDK.git
   ```
3. Import. The SDK includes:
   - Phantom deep links
   - Solana Mobile Stack (SMS) / Mobile Wallet Adapter
   - Full transaction building
   - SPL token transfers
   - WebGL + Android + iOS support

Full documentation: https://solana.unity-sdk.gg/

---

## 2. `CryptoPaymentManager.cs`

**Path:** `Assets/_Modules/Monetization/CryptoPaymentManager.cs`

```csharp
using UnityEngine;
using Solana.Unity.SDK;
using Solana.Unity.Rpc.Models;
using Solana.Unity.Wallet;
using System.Threading.Tasks;

public class CryptoPaymentManager : MonoBehaviour
{
    public static CryptoPaymentManager Instance { get; private set; }

    [Header("Treasury Wallet")]
    public string treasuryPublicKey = "YOUR_TREASURY_WALLET_HERE";      // ← Change this!

    [Header("Token Mint Addresses (Mainnet)")]
    public string SKR_MINT  = "YOUR_SKR_TOKEN_MINT_ADDRESS_HERE";       // Your custom token
    public string USDC_MINT = "EPjFWdd5AufqSSqeM2qN1xzybapC8G4wEGGkZwyTDt1v"; // Official USDC

    [Header("SKR Bonus")]
    [Range(1.1f, 2f)]
    public float skrBonusMultiplier = 1.25f;   // 25% extra Aether when paying with SKR

    private WalletBase _wallet;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // ── Wallet ────────────────────────────────────────────────────────────────

    public async Task ConnectWallet()
    {
        // SMS / Mobile Wallet Adapter on Android; Phantom deep-link on iOS/desktop.
        _wallet = await Web3.Instance.WalletManager.ConnectWallet(WalletType.Phantom);

        if (_wallet != null)
            Debug.Log("[Crypto] Wallet connected: " + _wallet.Account.PublicKey);
        else
            Debug.LogWarning("[Crypto] Wallet connection cancelled or failed.");
    }

    // ── Payment entry points ──────────────────────────────────────────────────

    public async Task<bool> PayWithSOL(int aetherAmount)
    {
        double solAmount = aetherAmount * 0.001;   // Tune conversion rate as needed
        return await SendPayment(solAmount, null, aetherAmount);
    }

    public async Task<bool> PayWithSKR(int aetherAmount)
    {
        // Apply staking bonus if StakingBonusManager is present (WO-76).
        int finalAether = StakingBonusManager.Instance != null
            ? StakingBonusManager.Instance.ApplyBonusToAether(
                Mathf.RoundToInt(aetherAmount * skrBonusMultiplier))
            : Mathf.RoundToInt(aetherAmount * skrBonusMultiplier);

        bool success = await SendPayment(0, SKR_MINT, finalAether);

        if (success)
            Debug.Log($"[Crypto] SKR payment → {finalAether} Aether granted (with bonus).");

        return success;
    }

    public async Task<bool> PayWithUSDC(int aetherAmount)
    {
        double usdcAmount = aetherAmount * 0.05;   // Tune conversion rate as needed
        return await SendPayment(0, USDC_MINT, aetherAmount);
    }

    // ── Core transaction ──────────────────────────────────────────────────────

    private async Task<bool> SendPayment(double solAmount, string tokenMint, int aetherReward)
    {
        if (_wallet == null)
        {
            await ConnectWallet();
            if (_wallet == null) return false;
        }

        try
        {
            var transaction = await Web3.Instance.BuildTransferTransaction(
                _wallet.Account.PublicKey,
                new PublicKey(treasuryPublicKey),
                solAmount,
                tokenMint != null ? new PublicKey(tokenMint) : null
            );

            var result = await _wallet.SignAndSendTransaction(transaction);

            if (result.IsSuccessful)
            {
                MonetizationManager.Instance.AddShards(aetherReward);
                Debug.Log($"[Crypto] Payment successful → {aetherReward} Aether granted.");
                return true;
            }

            Debug.LogError("[Crypto] Transaction failed: " + result.RawRpcResponse);
            return false;
        }
        catch (System.Exception ex)
        {
            Debug.LogError("[Crypto] Payment exception: " + ex.Message);
            return false;
        }
    }
}
```

---

## 3. Update `ShopUI.cs` — add crypto buy hooks

In `ShopUI.cs` (WO-73), the following methods are already stubbed. They wire
directly to `CryptoPaymentManager`:

```csharp
public void BuyWithSOL(int aetherAmount)  => CryptoPaymentManager.Instance?.PayWithSOL(aetherAmount);
public void BuyWithSKR(int aetherAmount)  => CryptoPaymentManager.Instance?.PayWithSKR(aetherAmount);
public void BuyWithUSDC(int aetherAmount) => CryptoPaymentManager.Instance?.PayWithUSDC(aetherAmount);
```

Connect the Shop's **Open** button to also call:

```csharp
CryptoPaymentManager.Instance?.ConnectWallet();
```

so the wallet prompt appears as soon as the player opens the shop.

---

## 4. Setup Instructions

1. Replace `YOUR_TREASURY_WALLET_HERE` with your actual treasury wallet public key.
2. Replace `YOUR_SKR_TOKEN_MINT_ADDRESS_HERE` with your SKR token mint address.
3. Add `CryptoPaymentManager` to your persistent manager GameObject
   (same one as `MonetizationManager` and `BattlePassSystem`).
4. Call `CryptoPaymentManager.Instance.ConnectWallet()` when the player
   opens the Shop UI.
5. Test on:
   - Desktop: Phantom browser extension via deep link
   - Android: Solana Mobile Stack / Mobile Wallet Adapter
   - iOS: Phantom app deep link

---

## Files to Create / Edit

| File | Action |
|---|---|
| `Assets/_Modules/Monetization/CryptoPaymentManager.cs` | **Create** |
| `Assets/_Modules/Monetization/ShopUI.cs` | **Edit** — crypto hooks already stubbed in WO-73 |
| Persistent manager GO in scene | **Edit** — add `CryptoPaymentManager` component |

---

## Acceptance Criteria

- [ ] `ConnectWallet()` opens Phantom (or SMS adapter) and logs the public key
- [ ] `PayWithSOL(1200)` builds and sends a SOL transfer, then grants 1 200 Aether
- [ ] `PayWithSKR(1200)` grants 1 500 Aether (1 200 × 1.25 bonus)
- [ ] `PayWithUSDC(1200)` transfers USDC and grants 1 200 Aether
- [ ] Failed or cancelled transactions return `false` and grant nothing
- [ ] `MonetizationManager.aetherShards` increases correctly after a successful payment
- [ ] No crash if `CryptoPaymentManager` is absent (all calls are null-safe via `?.`)
