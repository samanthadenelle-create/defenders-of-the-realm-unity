# WORK ORDER 78 — Backend Transaction Verification + Staking Dashboard UI

**Status:** READY TO IMPLEMENT
**Date:** 2026-05-28
**Priority:** High
**Scope:** Small — two new scripts
**Depends on:** WO-74 (CryptoPaymentManager), WO-76 (StakingBonusManager)

---

## Goal

1. Verify every Solana transaction on-chain before granting Aether, then save
   a receipt to the backend (Supabase or Firebase — WO-80 covers the Neon/Vercel
   backend in full).
2. Give players a clean Staking Dashboard UI so they can see their staked
   amount, current bonus, and estimated daily rewards at a glance.

---

## 1. `TransactionVerifier.cs`

**Path:** `Assets/_Modules/Monetization/TransactionVerifier.cs`

```csharp
using UnityEngine;
using Solana.Unity.Rpc;
using Solana.Unity.Wallet;
using System.Threading.Tasks;

public class TransactionVerifier : MonoBehaviour
{
    public static TransactionVerifier Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// Verify a Solana transaction on-chain and save a receipt.
    /// Call this after every successful crypto payment in CryptoPaymentManager.
    /// </summary>
    public async Task<bool> VerifyAndSaveReceipt(string signature, int aetherGranted, string paymentType)
    {
        try
        {
            var txResult = await Web3.Instance.RpcClient
                .GetTransactionAsync(signature, commitment: Commitment.Confirmed);

            if (txResult.Result == null || txResult.Result.Meta.Err != null)
            {
                Debug.LogError($"[Verifier] Transaction not found or failed: {signature}");
                return false;
            }

            await SaveReceiptToBackend(signature, aetherGranted, paymentType);

            Debug.Log($"[Verifier] ✅ Verified & saved: {signature} → {aetherGranted} Aether via {paymentType}");
            return true;
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[Verifier] Verification exception: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Send receipt to your backend (Vercel/Neon — see WO-80).
    /// Replace the BackendAPI call with your actual endpoint once WO-80 is deployed.
    /// </summary>
    private async Task SaveReceiptToBackend(string signature, int aether, string paymentType)
    {
        // WO-80 wires this properly. For now, log and save locally.
        string walletKey = Web3.Instance.WalletManager?.CurrentWallet?.Account?.PublicKey ?? "unknown";

        BackendAPI.VerifyTransaction(signature, walletKey, paymentType, aether);

        // Local PlayerPrefs fallback so receipts aren't completely lost before backend is live.
        string receipts = PlayerPrefs.GetString("TxReceipts", "");
        receipts += $"{signature}:{aether}:{paymentType};";
        PlayerPrefs.SetString("TxReceipts", receipts);
        PlayerPrefs.Save();
    }
}
```

### Wire into `CryptoPaymentManager.SendPayment()`

After the `result.IsSuccessful` block in WO-74, add:

```csharp
if (result.IsSuccessful)
{
    MonetizationManager.Instance.AddShards(aetherReward);

    // Verify on-chain and save receipt.
    _ = TransactionVerifier.Instance?.VerifyAndSaveReceipt(
        result.Result,   // transaction signature string
        aetherReward,
        tokenMint == null ? "SOL" : (tokenMint == SKR_MINT ? "SKR" : "USDC"));

    Debug.Log($"[Crypto] Payment successful → {aetherReward} Aether granted.");
    return true;
}
```

---

## 2. `StakingDashboardUI.cs`

**Path:** `Assets/_Modules/Monetization/StakingDashboardUI.cs`

```csharp
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Threading.Tasks;

public class StakingDashboardUI : MonoBehaviour
{
    public static StakingDashboardUI Instance { get; private set; }

    [Header("Panel")]
    public GameObject dashboardPanel;

    [Header("Data Fields")]
    public TMP_Text stakedAmountText;
    public TMP_Text bonusMultiplierText;
    public TMP_Text dailyBonusEstimateText;
    public TMP_Text lumbermillBonusText;

    [Header("Buttons")]
    public Button refreshButton;
    public Button closeButton;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        refreshButton.onClick.AddListener(() => _ = RefreshDashboard());
        closeButton.onClick.AddListener(()   => dashboardPanel.SetActive(false));
    }

    /// <summary>Show the dashboard and refresh data immediately.</summary>
    public async void ShowDashboard()
    {
        dashboardPanel.SetActive(true);
        await RefreshDashboard();
    }

    private async Task RefreshDashboard()
    {
        if (StakingBonusManager.Instance == null) return;

        await StakingBonusManager.Instance.RefreshStakedAmount();

        float staked     = StakingBonusManager.Instance.lastCheckedStakedAmount;
        float multiplier = StakingBonusManager.Instance.currentMultiplier;

        stakedAmountText.text        = $"{staked:F0} SKR staked";
        bonusMultiplierText.text     = $"+{(multiplier - 1f) * 100:F0}% bonus on everything";
        dailyBonusEstimateText.text  = $"+{Mathf.RoundToInt(150 * multiplier)} Aether daily";
        lumbermillBonusText.text     = $"{multiplier:F2}x faster resource production";
    }
}
```

### Scene wiring

Add a **"Staking Dashboard"** button in the Village HUD that calls:

```csharp
StakingDashboardUI.Instance?.ShowDashboard();
```

---

## Files to Create / Edit

| File | Action |
|---|---|
| `Assets/_Modules/Monetization/TransactionVerifier.cs` | **Create** |
| `Assets/_Modules/Monetization/StakingDashboardUI.cs` | **Create** |
| `Assets/_Modules/Monetization/CryptoPaymentManager.cs` | **Edit** — call `VerifyAndSaveReceipt` after success |
| Persistent manager GO | **Edit** — add `TransactionVerifier` component |
| Village HUD Canvas | **Edit** — add Staking Dashboard button + panel |

---

## Acceptance Criteria

- [ ] Successful SOL/SKR/USDC payment triggers `VerifyAndSaveReceipt()` automatically
- [ ] `VerifyAndSaveReceipt` returns `false` if the signature is not found on-chain
- [ ] Receipt is saved to PlayerPrefs as a local fallback before backend is live
- [ ] Staking Dashboard panel shows correct staked amount, multiplier, daily
      estimate, and lumbermill speed
- [ ] Refresh button re-queries the chain and updates all four fields
- [ ] Close button hides the panel
- [ ] No crash if `TransactionVerifier` or `StakingBonusManager` is null
