# WORK ORDER 75 — Full Shop UI with SOL / SKR / USDC Tabs + SKR Bonus Highlight

**Status:** READY TO IMPLEMENT
**Date:** 2026-05-28
**Priority:** High
**Scope:** Medium — full ShopUI.cs replacement + prefab guide
**Depends on:** WO-73 (ShopUI base), WO-74 (CryptoPaymentManager)

---

## Goal

Replace the base `ShopUI.cs` with a tabbed version that has Aether, Crypto, and
Battle Pass panels. The Crypto panel shows three currency cards (SOL, SKR, USDC)
with a prominent "+25% BONUS" badge on the SKR card.

---

## 1. Shop UI Layout

```
[ Aether Tab ] [ Crypto Tab ] [ Battle Pass Tab ]
────────────────────────────────────────────────
  ┌──────────┐  ┌──────────────┐  ┌──────────┐
  │  SOL     │  │  SKR ★BONUS  │  │  USDC    │
  │ 1200 ☽   │  │  1500 ☽ +300 │  │ 1200 ☽   │
  │ [Buy]    │  │  [Buy]       │  │ [Buy]    │
  └──────────┘  └──────────────┘  └──────────┘
         "Pay with SKR → +25% extra Aether Shards!"
```

---

## 2. Shop Item Prefab (`ShopItem.prefab`)

Create a UI prefab with:

| Child name | Component |
|---|---|
| `CurrencyIcon` | `Image` |
| `Title` | `TMP_Text` |
| `AetherAmount` | `TMP_Text` |
| `BonusBadge` | `GameObject` (contains `TMP_Text`) |
| Root | `Button` |

The `BonusBadge` child is active only when `bonusAether > 0`.

---

## 3. Full `ShopUI.cs` (Tabbed + Crypto)

**Path:** `Assets/_Modules/Monetization/ShopUI.cs`

Replaces the simpler version from WO-73.

```csharp
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class ShopUI : MonoBehaviour
{
    // ── Tabs ──────────────────────────────────────────────────────────────────

    [Header("Tabs")]
    public Button aetherTab;
    public Button cryptoTab;
    public Button battlePassTab;

    [Header("Panels")]
    public GameObject aetherPanel;
    public GameObject cryptoPanel;
    public GameObject battlePassPanel;

    // ── Aether panel ─────────────────────────────────────────────────────────

    [Header("Aether Grid")]
    public Transform  itemContainer;
    public GameObject shopItemPrefab;   // Image + TMP_Text (name) + TMP_Text (price) + Button
    public TMP_Text   shardBalanceText;

    // ── Crypto panel ─────────────────────────────────────────────────────────

    [Header("Crypto Options")]
    public Transform  cryptoContainer;
    public GameObject cryptoOptionPrefab;   // CurrencyIcon, Title, AetherAmount, BonusBadge, Button

    // ── Staking banner ────────────────────────────────────────────────────────

    [Header("Staking Banner")]
    public GameObject stakingBanner;
    public TMP_Text   stakingAmountText;
    public TMP_Text   bonusMultiplierText;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        aetherTab.onClick.AddListener(    () => ShowPanel(aetherPanel));
        cryptoTab.onClick.AddListener(    () => ShowPanel(cryptoPanel));
        battlePassTab.onClick.AddListener(() => ShowPanel(battlePassPanel));

        PopulateCryptoOptions();
    }

    private void OnEnable()
    {
        RefreshShardDisplay();
        RefreshStakingDisplay();
    }

    // ── Panel control ─────────────────────────────────────────────────────────

    private void ShowPanel(GameObject panel)
    {
        aetherPanel.SetActive(false);
        cryptoPanel.SetActive(false);
        battlePassPanel.SetActive(false);
        panel.SetActive(true);
    }

    /// <summary>Opens the shop and defaults to the Crypto tab for Solana players.</summary>
    public void OpenShop()
    {
        gameObject.SetActive(true);
        CryptoPaymentManager.Instance?.ConnectWallet();
        ShowPanel(cryptoPanel);
    }

    /// <summary>Opens filtered to a specific cosmetic type (used by village building buttons).</summary>
    public void OpenShop(CosmeticType filterType)
    {
        gameObject.SetActive(true);
        ShowPanel(aetherPanel);

        var all = Resources.LoadAll<CosmeticData>("Cosmetics");
        var filtered = new List<CosmeticData>();
        foreach (var c in all)
            if (c.cosmeticType == filterType)
                filtered.Add(c);

        PopulateAetherGrid(filtered);
        RefreshShardDisplay();
    }

    // ── Aether grid ───────────────────────────────────────────────────────────

    private void PopulateAetherGrid(List<CosmeticData> cosmetics)
    {
        foreach (Transform child in itemContainer)
            Destroy(child.gameObject);

        foreach (var cosmetic in cosmetics)
        {
            var item = Instantiate(shopItemPrefab, itemContainer);
            item.transform.Find("Name").GetComponent<TMP_Text>().text  = cosmetic.cosmeticName;
            item.transform.Find("Price").GetComponent<TMP_Text>().text =
                cosmetic.isFreeByDefault ? "FREE" : $"{cosmetic.aetherShardPrice} Shards";

            var btn = item.GetComponentInChildren<Button>();
            btn.interactable = !MonetizationManager.Instance.OwnsCosmetic(cosmetic.cosmeticID);
            btn.onClick.AddListener(() => TryBuy(cosmetic));
        }
    }

    private void TryBuy(CosmeticData cosmetic)
    {
        if (MonetizationManager.Instance.OwnsCosmetic(cosmetic.cosmeticID)) return;

        if (cosmetic.isFreeByDefault ||
            MonetizationManager.Instance.SpendShards(cosmetic.aetherShardPrice))
        {
            MonetizationManager.Instance.UnlockCosmetic(cosmetic.cosmeticID);
            RefreshShardDisplay();
        }
    }

    // ── Crypto panel ──────────────────────────────────────────────────────────

    private void PopulateCryptoOptions()
    {
        foreach (Transform child in cryptoContainer)
            Destroy(child.gameObject);

        // baseAether, bonusAether, totalAether shown in UI
        CreateCryptoOption("SOL",  "Pay with SOL",  1200,   0, 1200);
        CreateCryptoOption("SKR",  "Pay with SKR",  1200, 300, 1500);   // +25% bonus
        CreateCryptoOption("USDC", "Pay with USDC", 1200,   0, 1200);
    }

    private void CreateCryptoOption(string currency, string title,
                                    int baseAether, int bonusAether, int totalAether)
    {
        var option = Instantiate(cryptoOptionPrefab, cryptoContainer);

        var iconImg = option.transform.Find("CurrencyIcon")?.GetComponent<Image>();
        if (iconImg != null) iconImg.sprite = GetCurrencyIcon(currency);

        option.transform.Find("Title")?.GetComponent<TMP_Text>()
            .SetText(title);

        option.transform.Find("AetherAmount")?.GetComponent<TMP_Text>()
            .SetText($"{totalAether} Aether");

        var badge = option.transform.Find("BonusBadge");
        if (badge != null)
        {
            badge.gameObject.SetActive(bonusAether > 0);
            if (bonusAether > 0)
                badge.GetComponentInChildren<TMP_Text>().text = $"+{bonusAether} BONUS";
        }

        option.GetComponent<Button>().onClick.AddListener(() =>
        {
            switch (currency)
            {
                case "SOL":  CryptoPaymentManager.Instance.PayWithSOL(baseAether);  break;
                case "SKR":  CryptoPaymentManager.Instance.PayWithSKR(baseAether);  break;
                case "USDC": CryptoPaymentManager.Instance.PayWithUSDC(baseAether); break;
            }
        });
    }

    private Sprite GetCurrencyIcon(string currency)
    {
        // TODO: Load SOL / SKR / USDC sprites from Resources or assign in Inspector
        return null;
    }

    // ── Staking banner ────────────────────────────────────────────────────────

    public async void RefreshStakingDisplay()
    {
        if (StakingBonusManager.Instance == null) return;

        await StakingBonusManager.Instance.RefreshStakedAmount();

        float staked     = StakingBonusManager.Instance.lastCheckedStakedAmount;
        float multiplier = StakingBonusManager.Instance.currentMultiplier;

        if (stakingAmountText    != null) stakingAmountText.text    = $"Staked: {staked:F0} SKR";
        if (bonusMultiplierText  != null) bonusMultiplierText.text  = $"Bonus: +{(multiplier - 1f) * 100:F0}%";
        if (stakingBanner        != null) stakingBanner.SetActive(multiplier > 1.05f);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void RefreshShardDisplay()
    {
        if (shardBalanceText != null)
            shardBalanceText.text = $"{MonetizationManager.Instance.aetherShards} Shards";
    }
}
```

---

## 4. Quick Setup Steps

1. Create three panel GameObjects under the Shop Canvas:
   `AetherPanel`, `CryptoPanel`, `BattlePassPanel`.
2. Assign the tab buttons and panels in the Inspector.
3. Create `ShopItem.prefab` with children: `Name`, `Price`, `Button`.
4. Create `CryptoOptionPrefab` with children: `CurrencyIcon`, `Title`,
   `AetherAmount`, `BonusBadge` (with its own `TMP_Text`), root `Button`.
5. Add a staking banner at the top of the Shop Canvas with
   `stakingAmountText` and `bonusMultiplierText` fields wired.
6. Call `ShopUI.OpenShop()` from the Village menu button.

---

## 5. Bonus UI Polish (Recommended)

- Make the SKR card the largest/most prominent with a glowing border and
  subtitle: **"Best Value — +25% Aether"**
- Add a tooltip on hover: *"Paying with SKR supports the project and gives
  you extra Shards!"*
- Default tab is `cryptoPanel` for Solana players; change to `aetherPanel`
  if targeting a broader audience.

---

## Files to Create / Edit

| File | Action |
|---|---|
| `Assets/_Modules/Monetization/ShopUI.cs` | **Replace** (supersedes WO-73 version) |
| `ShopItem.prefab` | **Create** (if not already present) |
| `CryptoOptionPrefab` | **Create** |
| Shop Canvas in Village scene | **Edit** — wire new fields |

---

## Acceptance Criteria

- [ ] Three tabs switch panels without errors
- [ ] Crypto panel shows SOL, SKR, USDC cards
- [ ] SKR BonusBadge is visible and shows "+300 BONUS"
- [ ] Tapping SKR "Buy" calls `CryptoPaymentManager.Instance.PayWithSKR(1200)`
- [ ] Staking banner appears only when `currentMultiplier > 1.05`
- [ ] `OpenShop(CosmeticType.HeroSkin)` still works for the cosmetic grid
- [ ] Shard balance updates after every purchase
