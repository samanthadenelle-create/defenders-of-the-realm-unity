# WORK ORDER 76 — Staked SKR Bonus System (Solana Staking Rewards)

**Status:** READY TO IMPLEMENT
**Date:** 2026-05-28
**Priority:** High
**Scope:** Medium — one new script + integration hooks
**Depends on:** WO-74 (CryptoPaymentManager, Solana Unity SDK)

---

## Goal

Automatically detect how much SKR a player has currently staked, then give them
meaningful but fair bonuses: extra Aether on purchases, faster lumbermill
production, and a Battle Pass XP multiplier. Rewards loyal holders and increases
token utility without being pay-to-win.

---

## 1. `StakingBonusManager.cs`

**Path:** `Assets/_Modules/Monetization/StakingBonusManager.cs`

```csharp
using UnityEngine;
using Solana.Unity.SDK;
using Solana.Unity.Rpc;
using Solana.Unity.Wallet;
using System.Threading.Tasks;

public class StakingBonusManager : MonoBehaviour
{
    public static StakingBonusManager Instance { get; private set; }

    [Header("Staking Settings")]
    public string SKR_MINT             = "YOUR_SKR_TOKEN_MINT_ADDRESS_HERE";
    public string STAKING_PROGRAM_ID   = "YOUR_STAKING_PROGRAM_ID_HERE";    // optional custom staking program
    public float  bonusPer1000Staked   = 0.15f;   // 15% bonus per 1 000 SKR staked

    [Header("Active Bonuses — read-only in Inspector")]
    public float currentMultiplier = 1f;   // 1.0 = no bonus, 1.25 = +25%

    // Exposed so ShopUI / StakingDashboardUI can display the raw value.
    [HideInInspector] public float lastCheckedStakedAmount = 0f;

    private WalletBase _wallet;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // ── Wallet + on-chain read ─────────────────────────────────────────────────

    public async Task ConnectAndCheckStaking()
    {
        _wallet = await Web3.Instance.WalletManager.ConnectWallet(WalletType.Phantom);
        if (_wallet == null) return;

        await RefreshStakedAmount();
    }

    public async Task RefreshStakedAmount()
    {
        if (_wallet == null) return;

        try
        {
            // Get all token accounts owned by this wallet that hold SKR.
            var tokenAccounts = await Web3.Instance.RpcClient.GetTokenAccountsByOwnerAsync(
                _wallet.Account.PublicKey,
                new TokenAccountsFilter { Mint = new PublicKey(SKR_MINT) }
            );

            float totalStaked = 0f;

            foreach (var account in tokenAccounts.Result.Value)
            {
                var balance = await Web3.Instance.RpcClient
                    .GetTokenAccountBalanceAsync(account.Pubkey);

                if (balance.Result != null)
                {
                    // Adjust divisor to match your token's decimal places.
                    totalStaked += (float)balance.Result.Value.Amount / 1_000_000_000f;
                }
            }

            lastCheckedStakedAmount = totalStaked;
            currentMultiplier = 1f + (totalStaked / 1000f) * bonusPer1000Staked;

            Debug.Log($"[Staking] {totalStaked:F0} SKR staked → multiplier: {currentMultiplier:F2}x");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[Staking] Check failed: {ex.Message}");
        }
    }

    // ── Bonus application ─────────────────────────────────────────────────────

    /// <summary>Apply the staking multiplier to any Aether grant.</summary>
    public int ApplyBonusToAether(int baseAmount)
        => Mathf.RoundToInt(baseAmount * currentMultiplier);

    /// <summary>Used by Lumbermill / Workshop to speed up resource production.</summary>
    public float GetLumbermillMultiplier() => currentMultiplier;

    /// <summary>Used by BattlePassSystem.AddXP() to scale XP gain.</summary>
    public int ApplyBattlePassXPBonus(int baseXP)
        => Mathf.RoundToInt(baseXP * currentMultiplier);
}
```

---

## 2. Integration Points

### `CryptoPaymentManager.cs` — `PayWithSKR` (WO-74)

The WO-74 version already checks for `StakingBonusManager.Instance`:

```csharp
int finalAether = StakingBonusManager.Instance != null
    ? StakingBonusManager.Instance.ApplyBonusToAether(
          Mathf.RoundToInt(aetherAmount * skrBonusMultiplier))
    : Mathf.RoundToInt(aetherAmount * skrBonusMultiplier);
```

No additional edits needed there.

### Lumbermill / Resource scripts

```csharp
// In LumbermillController.cs or ResourceManager.cs
public float GetProductionRate()
{
    float baseRate = 1f;
    return baseRate * (StakingBonusManager.Instance?.GetLumbermillMultiplier() ?? 1f);
}
```

Apply the same pattern to Workshop build speed and Store stock refresh.

### `BattlePassSystem.AddXP()` (WO-73)

```csharp
public void AddXP(int amount)
{
    int bonusAmount = StakingBonusManager.Instance != null
        ? StakingBonusManager.Instance.ApplyBattlePassXPBonus(amount)
        : amount;

    currentXP += bonusAmount;
    // ... rest of level-up logic unchanged
}
```

### On game start / Shop open

```csharp
await StakingBonusManager.Instance.ConnectAndCheckStaking();
```

---

## 3. Shop UI Banner

In `ShopUI.cs` (WO-75) the staking banner is already implemented. It shows:

```
Staked: 2 400 SKR  →  Bonus: +36%
```

and is hidden when `currentMultiplier ≤ 1.05` (i.e. less than 350 SKR staked).

---

## Files to Create / Edit

| File | Action |
|---|---|
| `Assets/_Modules/Monetization/StakingBonusManager.cs` | **Create** |
| `Assets/_Modules/Monetization/CryptoPaymentManager.cs` | Already handled in WO-74 |
| `LumbermillController.cs` (or equivalent) | **Edit** — multiply production rate |
| `Assets/_Modules/Monetization/BattlePassSystem.cs` | **Edit** — wrap `AddXP` with bonus |
| Persistent manager GO | **Edit** — add `StakingBonusManager` component |

---

## Acceptance Criteria

- [ ] `ConnectAndCheckStaking()` reads SKR token balance from the connected wallet
- [ ] `currentMultiplier` updates correctly: 0 SKR → 1.0×, 1 000 SKR → 1.15×,
      5 000 SKR → 1.75×
- [ ] `ApplyBonusToAether(1200)` with 2 000 SKR staked returns 1 560
      (1 200 × 1.30)
- [ ] Lumbermill production rate visibly increases when multiplier > 1
- [ ] `AddXP` in BattlePassSystem grants staking-scaled XP
- [ ] `RefreshStakedAmount()` can be called again without crashing if wallet
      is not connected (graceful no-op)
- [ ] No crash if `StakingBonusManager` is absent from the scene
      (all callers use `?.` null-safe)
