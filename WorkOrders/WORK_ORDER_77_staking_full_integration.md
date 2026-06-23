# WORK ORDER 77 — Staked SKR Full Integration (Shop + Lumbermill + Daily Login)

**Status:** READY TO IMPLEMENT
**Date:** 2026-05-28
**Priority:** High
**Scope:** Small — edits to existing scripts + one new DailyLoginBonus script
**Depends on:** WO-76 (StakingBonusManager)

---

## Goal

Wire `StakingBonusManager` into every place a player earns or spends resources,
so staked SKR holders feel immediately rewarded across the whole game — shop
purchases, resource production, daily login bonus, and battle pass XP.

---

## 1. `ShopUI.cs` — staking status banner (WO-75 already includes this)

The full ShopUI from WO-75 already implements the banner. For reference, the
key addition to the `partial`/`OnEnable` flow is:

```csharp
[Header("Staking Banner")]
public GameObject stakingBanner;
public TMP_Text   stakingAmountText;
public TMP_Text   bonusMultiplierText;

private void OnEnable() => RefreshStakingDisplay();

public async void RefreshStakingDisplay()
{
    if (StakingBonusManager.Instance == null) return;
    await StakingBonusManager.Instance.RefreshStakedAmount();

    float staked     = StakingBonusManager.Instance.lastCheckedStakedAmount;
    float multiplier = StakingBonusManager.Instance.currentMultiplier;

    stakingAmountText.text   = $"Staked: {staked:F0} SKR";
    bonusMultiplierText.text = $"Bonus: +{(multiplier - 1f) * 100:F0}%";
    stakingBanner.SetActive(multiplier > 1.05f);
}
```

Add a **"Refresh"** button in the banner that calls `RefreshStakingDisplay()`.

---

## 2. `CryptoPaymentManager.cs` — staking-aware SKR path (already in WO-74)

Confirm `PayWithSKR` applies the staking multiplier on top of the 25% base
bonus. The WO-74 version is correct — no additional changes needed.

---

## 3. Lumbermill / Workshop / Store — resource multiplier

Add this to any building that produces resources over time:

```csharp
// In LumbermillController.cs, WorkshopController.cs, StoreController.cs
public float GetResourceProductionRate()
{
    float baseRate = 1f;   // your normal production rate constant
    return baseRate * (StakingBonusManager.Instance?.GetLumbermillMultiplier() ?? 1f);
}
```

Call `GetResourceProductionRate()` wherever you accumulate lumber, workshop
progress, or store stock so the multiplier flows into every tick.

---

## 4. `DailyLoginBonus.cs`

**Path:** `Assets/_Modules/Monetization/DailyLoginBonus.cs`

```csharp
using UnityEngine;

public class DailyLoginBonus : MonoBehaviour
{
    public static DailyLoginBonus Instance { get; private set; }

    [Header("Reward Settings")]
    public int baseDailyAether = 150;
    public int maxStakedBonus  = 300;   // cap on extra Aether from staking

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// Call this when the player enters the Village scene each session.
    /// Grants daily Aether once per UTC day, scaled by staking bonus.
    /// </summary>
    public async void CheckDailyLogin()
    {
        string lastLogin = PlayerPrefs.GetString("LastDailyLogin", "");
        string today     = System.DateTime.UtcNow.ToString("yyyy-MM-dd");

        if (lastLogin == today) return;   // Already claimed today

        // Refresh staking to get the most recent multiplier.
        if (StakingBonusManager.Instance != null)
            await StakingBonusManager.Instance.RefreshStakedAmount();

        float multiplier   = StakingBonusManager.Instance?.currentMultiplier ?? 1f;
        int   totalReward  = Mathf.Min(
            Mathf.RoundToInt(baseDailyAether * multiplier),
            baseDailyAether + maxStakedBonus);

        MonetizationManager.Instance.AddShards(totalReward);
        BattlePassSystem.Instance?.AddXP(200);   // small BP XP on login

        Debug.Log($"[Daily] Login bonus: +{totalReward} Aether (multiplier {multiplier:F2}x)");

        PlayerPrefs.SetString("LastDailyLogin", today);
        PlayerPrefs.Save();
    }
}
```

**Where to call it:** In your Village scene entry point — e.g.
`VillageManager.Start()` or a `SceneLoader.OnVillageLoaded` event:

```csharp
DailyLoginBonus.Instance?.CheckDailyLogin();
```

---

## 5. Shop UI "Staking Incentive" Copy

Show this text prominently on the Crypto panel when the player has no staking:

> *"Stake SKR to unlock production speed bonuses, more daily Aether, and
> bigger rewards on every purchase!"*

When they have staking active, replace with the live banner (see §1).

---

## Files to Create / Edit

| File | Action |
|---|---|
| `Assets/_Modules/Monetization/DailyLoginBonus.cs` | **Create** |
| `LumbermillController.cs` (or ResourceManager) | **Edit** — use `GetLumbermillMultiplier()` |
| `WorkshopController.cs` | **Edit** — same pattern |
| `StoreController.cs` | **Edit** — same pattern |
| `VillageManager.cs` (or entry point) | **Edit** — call `CheckDailyLogin()` |
| Persistent manager GO | **Edit** — add `DailyLoginBonus` component |

---

## Acceptance Criteria

- [ ] Shop banner shows live staked amount and bonus% when staking > 0
- [ ] Shop banner is hidden when multiplier ≤ 1.05 (no meaningful staking)
- [ ] First login of the day grants `baseDailyAether × currentMultiplier` Aether (capped)
- [ ] Second login same day does nothing (PlayerPrefs guard)
- [ ] Lumbermill tick rate scales correctly with staking multiplier
- [ ] Daily login also awards 200 BP XP
- [ ] No crash if `StakingBonusManager` is null (all callers null-safe)
