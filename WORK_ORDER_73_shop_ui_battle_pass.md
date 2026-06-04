# WORK ORDER 73 — Shop UI + Battle Pass System

**Status:** READY TO IMPLEMENT
**Date:** 2026-05-28
**Priority:** High
**Scope:** Medium — three scripts + prefab wiring guide
**Depends on:** WO-72 (MonetizationManager, CosmeticData)

---

## Goal

Wire up the cosmetic shop so players can browse, preview, and buy cosmetics with
Aether Shards, and implement the Battle Pass seasonal reward track with free and
premium tiers.

> **Naming note:** `CosmeticApplier` (this WO) supersedes `CosmeticApplicator`
> referenced in WO-72 §3. Use `CosmeticApplier` exclusively going forward.

---

## 1. `CosmeticApplier.cs`

**Path:** `Assets/_Modules/Monetization/CosmeticApplier.cs`

Handles material swaps, prefab overrides, and VFX attachment on any character or
building that can wear a cosmetic.

```csharp
using UnityEngine;

[RequireComponent(typeof(MeshRenderer))]
public class CosmeticApplier : MonoBehaviour
{
    [Header("References")]
    public MeshRenderer  meshRenderer;
    public GameObject    defaultModel;
    public Transform     attachmentPoint;

    private Material     _originalMaterial;
    private GameObject   _currentOverrideModel;

    private void Awake()
    {
        if (meshRenderer == null)
            meshRenderer = GetComponent<MeshRenderer>();

        if (meshRenderer != null)
            _originalMaterial = meshRenderer.sharedMaterial;
    }

    /// <summary>Apply a cosmetic — material swap, prefab swap, or VFX attach.</summary>
    public void ApplyCosmetic(CosmeticData cosmetic)
    {
        // Material override
        if (cosmetic.materialOverride != null && meshRenderer != null)
            meshRenderer.material = cosmetic.materialOverride;

        // Prefab override (replace model)
        if (cosmetic.prefabOverride != null)
        {
            if (_currentOverrideModel != null)
                Destroy(_currentOverrideModel);

            if (defaultModel != null) defaultModel.SetActive(false);

            _currentOverrideModel = Instantiate(cosmetic.prefabOverride,
                attachmentPoint != null ? attachmentPoint : transform);
            _currentOverrideModel.transform.localPosition = Vector3.zero;
            _currentOverrideModel.transform.localRotation = Quaternion.identity;
        }

        // VFX attach
        if (cosmetic.vfxPrefab != null && attachmentPoint != null)
            Instantiate(cosmetic.vfxPrefab, attachmentPoint);
    }

    /// <summary>Restore the original material and default model.</summary>
    public void ResetToDefault()
    {
        if (_originalMaterial != null && meshRenderer != null)
            meshRenderer.material = _originalMaterial;

        if (_currentOverrideModel != null)
        {
            Destroy(_currentOverrideModel);
            _currentOverrideModel = null;
        }

        if (defaultModel != null) defaultModel.SetActive(true);
    }
}
```

---

## 2. `ShopUI.cs`

**Path:** `Assets/_Modules/Monetization/ShopUI.cs`

Displays a filtered grid of cosmetics. Each item shows name, price, and a Buy
button. Opening the shop triggers a wallet-connect if crypto payments are
enabled (WO-74).

```csharp
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class ShopUI : MonoBehaviour
{
    [Header("Grid")]
    public Transform    itemContainer;
    public GameObject   shopItemPrefab;   // Image + TMP_Text (name) + TMP_Text (price) + Button

    [Header("Shard Display")]
    public TMP_Text     shardBalanceText;

    private List<CosmeticData> _currentCosmetics = new();

    private void OnEnable()
    {
        RefreshShardDisplay();
    }

    /// <summary>
    /// Open the shop filtered by cosmetic type.
    /// Pass CosmeticType.HeroSkin, PetSkin, etc.
    /// </summary>
    public void OpenShop(CosmeticType filterType)
    {
        gameObject.SetActive(true);

        // TODO: Replace with Addressables or direct asset reference list.
        var all = Resources.LoadAll<CosmeticData>("Cosmetics");
        _currentCosmetics.Clear();

        foreach (var c in all)
            if (c.cosmeticType == filterType)
                _currentCosmetics.Add(c);

        PopulateGrid();
        RefreshShardDisplay();
    }

    private void PopulateGrid()
    {
        foreach (Transform child in itemContainer)
            Destroy(child.gameObject);

        foreach (var cosmetic in _currentCosmetics)
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
        if (MonetizationManager.Instance.OwnsCosmetic(cosmetic.cosmeticID))
            return;

        if (cosmetic.isFreeByDefault ||
            MonetizationManager.Instance.SpendShards(cosmetic.aetherShardPrice))
        {
            MonetizationManager.Instance.UnlockCosmetic(cosmetic.cosmeticID);
            ApplyCosmeticToPreview(cosmetic);
            RefreshShardDisplay();
            PopulateGrid();   // Refresh button states
        }
    }

    private void ApplyCosmeticToPreview(CosmeticData cosmetic)
    {
        // Wire up a preview character's CosmeticApplier in the Inspector if desired.
        Debug.Log($"[ShopUI] Cosmetic applied: {cosmetic.cosmeticName}");
    }

    // Crypto hooks — implemented fully in WO-74 / WO-75
    public void BuyWithSOL(int aetherAmount)  => CryptoPaymentManager.Instance?.PayWithSOL(aetherAmount);
    public void BuyWithSKR(int aetherAmount)  => CryptoPaymentManager.Instance?.PayWithSKR(aetherAmount);
    public void BuyWithUSDC(int aetherAmount) => CryptoPaymentManager.Instance?.PayWithUSDC(aetherAmount);

    private void RefreshShardDisplay()
    {
        if (shardBalanceText != null)
            shardBalanceText.text = $"{MonetizationManager.Instance.aetherShards} Shards";
    }
}
```

---

## 3. `BattlePassSystem.cs` + `BattlePassReward`

**Path:** `Assets/_Modules/Monetization/BattlePassSystem.cs`

DontDestroyOnLoad singleton. Persists XP and level in PlayerPrefs. Grants free
rewards to all players; premium rewards only if `hasPremium` is true.

```csharp
using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class BattlePassReward
{
    public string       rewardName;
    public CosmeticData cosmeticReward;
    public int          aetherShardBonus = 0;
}

public class BattlePassSystem : MonoBehaviour
{
    public static BattlePassSystem Instance { get; private set; }

    [Header("Season")]
    public string seasonName = "Season 1 - Shadow Realms";

    [Header("Progress")]
    public int currentLevel = 1;
    public int currentXP    = 0;
    public int xpPerLevel   = 800;

    [Header("Rewards")]
    public List<BattlePassReward> freeRewards;
    public List<BattlePassReward> premiumRewards;

    private bool _hasPremium;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        LoadProgress();
    }

    /// <summary>Award XP — levels up automatically when threshold is crossed.</summary>
    public void AddXP(int amount)
    {
        currentXP += amount;

        while (currentXP >= xpPerLevel)
        {
            currentXP -= xpPerLevel;
            currentLevel++;
            GrantReward(currentLevel);
        }

        SaveProgress();
    }

    private void GrantReward(int level)
    {
        // Free track
        int idx = level - 1;
        if (idx >= 0 && idx < freeRewards.Count)
        {
            var r = freeRewards[idx];
            if (r.cosmeticReward != null)
                MonetizationManager.Instance.UnlockCosmetic(r.cosmeticReward.cosmeticID);
            if (r.aetherShardBonus > 0)
                MonetizationManager.Instance.AddShards(r.aetherShardBonus);

            Debug.Log($"[BattlePass] Free reward level {level}: {r.rewardName}");
        }

        // Premium track
        if (_hasPremium && idx >= 0 && idx < premiumRewards.Count)
        {
            var r = premiumRewards[idx];
            if (r.cosmeticReward != null)
                MonetizationManager.Instance.UnlockCosmetic(r.cosmeticReward.cosmeticID);
            if (r.aetherShardBonus > 0)
                MonetizationManager.Instance.AddShards(r.aetherShardBonus);

            Debug.Log($"[BattlePass] Premium reward level {level}: {r.rewardName}");
        }

        LevelUpVFXController.Instance?.PlayLevelUp(transform, level, false);
    }

    /// <summary>Purchase the premium track for 2 400 Aether Shards.</summary>
    public bool PurchasePremiumPass()
    {
        const int cost = 2400;
        if (_hasPremium) return true;   // already owned

        if (MonetizationManager.Instance.SpendShards(cost))
        {
            _hasPremium = true;
            SaveProgress();
            Debug.Log("[BattlePass] Premium pass unlocked!");

            // Grant all back-dated premium rewards up to current level
            for (int i = 1; i <= currentLevel; i++)
                GrantReward(i);

            return true;
        }

        Debug.LogWarning("[BattlePass] Not enough Aether Shards for premium pass.");
        return false;
    }

    public bool HasPremium => _hasPremium;

    // ── Persistence ────────────────────────────────────────────────────────────

    private void SaveProgress()
    {
        PlayerPrefs.SetInt("BP_Level",      currentLevel);
        PlayerPrefs.SetInt("BP_XP",         currentXP);
        PlayerPrefs.SetInt("BP_HasPremium", _hasPremium ? 1 : 0);
        PlayerPrefs.Save();
    }

    private void LoadProgress()
    {
        currentLevel = PlayerPrefs.GetInt("BP_Level",      1);
        currentXP    = PlayerPrefs.GetInt("BP_XP",         0);
        _hasPremium  = PlayerPrefs.GetInt("BP_HasPremium", 0) == 1;
    }
}
```

---

## 4. Quick Setup Instructions

1. Create `Assets/Resources/Cosmetics/` folder and move all `CosmeticData` assets there
   (so `Resources.LoadAll<CosmeticData>("Cosmetics")` finds them).
2. Add `BattlePassSystem` to your persistent manager GameObject
   (same one as `MonetizationManager`).
3. In the Village scene:
   - Attach `ShopUI` to the Shop Canvas.
   - Wire `itemContainer`, `shopItemPrefab`, `shardBalanceText` in Inspector.
4. For each hero/pet/building prefab:
   - Add `CosmeticApplier` component.
   - Assign `meshRenderer`, `defaultModel`, and `attachmentPoint` in Inspector.
5. Populate `freeRewards` and `premiumRewards` lists in the `BattlePassSystem`
   Inspector with your `CosmeticData` assets and shard bonuses.

---

## Files to Create / Edit

| File | Action |
|---|---|
| `Assets/_Modules/Monetization/CosmeticApplier.cs` | **Create** |
| `Assets/_Modules/Monetization/ShopUI.cs` | **Create** |
| `Assets/_Modules/Monetization/BattlePassSystem.cs` | **Create** |
| Hero / pet / building prefabs | **Edit** — add `CosmeticApplier` |
| Persistent manager scene GO | **Edit** — add `BattlePassSystem` |

---

## Acceptance Criteria

- [ ] `ShopUI.OpenShop(CosmeticType.HeroSkin)` displays only hero skins
- [ ] Buying a cosmetic deducts the correct Aether Shard amount
- [ ] Owned cosmetics show their Buy button as non-interactable
- [ ] `CosmeticApplier.ApplyCosmetic()` swaps material and/or model correctly
- [ ] `CosmeticApplier.ResetToDefault()` restores original appearance
- [ ] `BattlePassSystem.AddXP(900)` advances level and grants the correct free reward
- [ ] `PurchasePremiumPass()` fails gracefully when shards are insufficient
- [ ] Premium track grants back-dated rewards immediately on purchase
- [ ] Battle pass level + XP persists across app restarts via PlayerPrefs
