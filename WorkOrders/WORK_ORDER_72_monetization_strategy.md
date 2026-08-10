# WORK ORDER 72 — Best Non-P2W Monetization (Warcraft / Starcraft Style)

**Status:** CLOSED — SUPERSEDED (owner-approved sweep 2026-08-09: live model = player-built town + PackStore, canon §8)
**Date:** 2026-05-28
**Priority:** High
**Scope:** Large — MonetizationManager, CosmeticData, Shop UI, Battle Pass, Building Prestige
**Depends on:** WO-50 (VFXManager for skin-swap effects), WO-51 (PerformanceManager)

---

## Core Philosophy — Absolute Law

> **Zero premium items may affect damage, health, tower stats, wave difficulty,
> or any gameplay number.** Aether Shards, Battle Pass, DLC, and the Cosmetic
> Shop buy looks, sounds, and content — never power.

This is enforced architecturally: `MonetizationManager` only controls
`CosmeticData` unlocks and never touches `TowerData`, `HeroStats`, or
`WaveManager`.

---

## 1. Create `CosmeticData.cs`

**Path:** `Assets/_Modules/Monetization/CosmeticData.cs`

```csharp
using UnityEngine;

[CreateAssetMenu(menuName = "Defenders/Cosmetic Data", fileName = "New Cosmetic")]
public class CosmeticData : ScriptableObject
{
    [Header("Basic Info")]
    public string cosmeticID;           // Unique ID, e.g. "wizard_royal_robe"
    public string displayName;
    [TextArea(3, 6)]
    public string description;

    [Header("Category")]
    public CosmeticType type;

    [Header("Cost & Unlock")]
    public int aetherShardPrice = 800;
    public bool isFreeByDefault = false;   // e.g. starter skins

    [Header("Visuals")]
    public Sprite icon;
    public Material materialOverride;      // For towers, buildings, hero robes
    public GameObject prefabOverride;      // For pets, hero models, VFX attachments
    public GameObject vfxPrefab;           // For special spell/death effects

    [Header("Preview")]
    public bool hasPreviewAnimation = true;
}

public enum CosmeticType
{
    HeroSkin,
    PetSkin,
    TowerSkin,
    BuildingSkin,      // Lumbermill, Workshop, Store
    VFXPack,
    Emote,
    TitleFrame
}
```

---

## 2. Create `MonetizationManager.cs`

**Path:** `Assets/_Modules/Monetization/MonetizationManager.cs`

```csharp
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class MonetizationManager : MonoBehaviour
{
    public static MonetizationManager Instance { get; private set; }

    [Header("Player Data")]
    public int aetherShards = 1200;

    // All owned cosmetics (saved)
    private HashSet<string> ownedCosmetics = new HashSet<string>();

    // Battle Pass progress
    public int battlePassLevel = 1;
    public int battlePassXP    = 0;

    private const string SHARDS_KEY   = "AetherShards";
    private const string COSMETICS_KEY = "OwnedCosmetics";

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        LoadProgress();
    }

    // ── Aether Shards ─────────────────────────────────────────────────────────

    public bool SpendShards(int amount)
    {
        if (aetherShards < amount) return false;
        aetherShards -= amount;
        SaveProgress();
        return true;
    }

    public void AddShards(int amount)
    {
        aetherShards += amount;
        SaveProgress();
    }

    // ── Gameplay earn hooks ───────────────────────────────────────────────────

    /// <summary>Call from WaveManager.CompleteWave().</summary>
    public void OnWaveCleared(int waveNumber)
        => AddShards(waveNumber % 10 == 0 ? 5 : 1);

    /// <summary>Call from daily quest / achievement systems.</summary>
    public void OnDailyQuestCompleted() => AddShards(10);

    // ── Cosmetics ─────────────────────────────────────────────────────────────

    public bool OwnsCosmetic(string cosmeticID)
        => ownedCosmetics.Contains(cosmeticID);

    public bool UnlockCosmetic(CosmeticData cosmetic)
    {
        if (OwnsCosmetic(cosmetic.cosmeticID)) return true;

        if (cosmetic.isFreeByDefault || SpendShards(cosmetic.aetherShardPrice))
        {
            ownedCosmetics.Add(cosmetic.cosmeticID);
            SaveProgress();
            Debug.Log($"Unlocked cosmetic: {cosmetic.displayName}");
            return true;
        }
        return false;
    }

    // ── Battle Pass ───────────────────────────────────────────────────────────

    public void AddBattlePassXP(int xp)
    {
        battlePassXP += xp;
        // Level-up logic: expand here
    }

    // ── Save / Load ───────────────────────────────────────────────────────────

    private void SaveProgress()
    {
        PlayerPrefs.SetInt(SHARDS_KEY, aetherShards);
        PlayerPrefs.SetString(COSMETICS_KEY, string.Join(",", ownedCosmetics));
        PlayerPrefs.Save();
    }

    private void LoadProgress()
    {
        aetherShards = PlayerPrefs.GetInt(SHARDS_KEY, 1200);
        string saved = PlayerPrefs.GetString(COSMETICS_KEY, "");
        if (!string.IsNullOrEmpty(saved))
            ownedCosmetics = new HashSet<string>(saved.Split(','));
    }
}
```

---

## 3. Create `CosmeticApplicator.cs` — skin-swap runtime component

**Path:** `Assets/_Modules/Monetization/CosmeticApplicator.cs`

Attach to any hero, pet, tower, or building that supports skins.

```csharp
using UnityEngine;

/// <summary>
/// Swaps materials and activates visual variants when a cosmetic skin is applied.
/// Attach to the root of any prefab that supports cosmetics.
/// </summary>
public class CosmeticApplicator : MonoBehaviour
{
    [Tooltip("The primary renderer whose materials will be swapped.")]
    public Renderer targetRenderer;

    [Tooltip("The parent transform where visual variant GameObjects are spawned.")]
    public Transform variantParent;

    private Material[]  _defaultMaterials;
    private GameObject  _activeVariant;
    private CosmeticData _activeSkin;

    private void Awake()
    {
        if (targetRenderer != null)
            _defaultMaterials = targetRenderer.sharedMaterials;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void ApplySkin(CosmeticData skin)
    {
        if (skin == null || !skin.IsUnlocked) return;
        _activeSkin = skin;

        // Swap materials.
        if (targetRenderer != null && skin.materials != null && skin.materials.Length > 0)
            targetRenderer.sharedMaterials = skin.materials;

        // Swap visual variant.
        if (_activeVariant != null) Destroy(_activeVariant);
        if (skin.visualVariantPrefab != null)
        {
            _activeVariant = Instantiate(skin.visualVariantPrefab,
                variantParent != null ? variantParent : transform);
        }

        // Apply VFX.
        if (skin.applyVfxType != VFXType.None)
            VFXManager.Instance?.Play(skin.applyVfxType, transform.position);
    }

    public void ResetToDefault()
    {
        if (targetRenderer != null && _defaultMaterials != null)
            targetRenderer.sharedMaterials = _defaultMaterials;

        if (_activeVariant != null) { Destroy(_activeVariant); _activeVariant = null; }
        _activeSkin = null;
    }

    public CosmeticData ActiveSkin => _activeSkin;
}
```

---

## 4. Create `MonetizationManager.cs` — singleton coordinator

**Path:** `Assets/_Modules/Monetization/MonetizationManager.cs`

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

public class MonetizationManager : MonoBehaviour
{
    public static MonetizationManager Instance { get; private set; }

    [Header("All Cosmetics (drag ScriptableObjects here)")]
    public List<CosmeticData> allCosmetics = new List<CosmeticData>();

    [Header("Battle Pass")]
    public BattlePassData currentSeason;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // ── Cosmetic unlock ───────────────────────────────────────────────────────

    public bool TryPurchaseCosmetic(string cosmeticId)
    {
        var item = GetCosmetic(cosmeticId);
        if (item == null) return false;
        bool ok = item.TryPurchaseWithShards();
        if (ok) EventTracker.Instance?.Track("cosmetic_purchased",
            new System.Collections.Generic.Dictionary<string, object>
                { {"id", cosmeticId}, {"cost", item.shardCost} });
        return ok;
    }

    public void ApplyCosmeticToTarget(string cosmeticId, CosmeticApplicator target)
    {
        var item = GetCosmetic(cosmeticId);
        if (item == null || !item.IsUnlocked) return;
        target.ApplySkin(item);

        // Persist the selection.
        PlayerPrefs.SetString($"dotr-active-skin-{target.gameObject.name}", cosmeticId);
    }

    public CosmeticData GetCosmetic(string id) =>
        allCosmetics.Find(c => c.cosmeticId == id);

    public List<CosmeticData> GetByCategory(CosmeticCategory cat) =>
        allCosmetics.FindAll(c => c.category == cat);

    public List<CosmeticData> GetUnlocked() =>
        allCosmetics.FindAll(c => c.IsUnlocked);

    // ── Battle Pass ───────────────────────────────────────────────────────────

    public void ClaimBattlePassReward(int week)
    {
        if (currentSeason == null) return;
        currentSeason.ClaimWeekReward(week);
    }

    // ── Supporter Pack (one-time IAP) ─────────────────────────────────────────

    public void GrantSupporterPack()
    {
        const string key = "dotr-supporter-pack";
        if (PlayerPrefs.GetInt(key, 0) == 1) return;   // already granted

        foreach (var c in allCosmetics)
            if (c.source == CosmeticSource.SupporterPack) c.Unlock();

        AetherShards.Award(500);   // bonus shards
        PlayerPrefs.SetInt(key, 1);
        Debug.Log("[MonetizationManager] Supporter Pack granted.");
    }
}
```

---

## 5. Create `BattlePassData.cs`

**Path:** `Assets/_Modules/Monetization/BattlePassData.cs`

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Defenders/Monetization/Battle Pass Season",
                 fileName = "BattlePassSeason")]
public class BattlePassData : ScriptableObject
{
    [Serializable]
    public struct WeekReward
    {
        public int          week;
        public CosmeticData freeReward;      // always available
        public CosmeticData premiumReward;   // requires premium track
        [TextArea] public string displayLabel;
    }

    [Header("Season Info")]
    public string seasonName;        // e.g. "Season 1 – Ember Throne"
    public int    shardCostPremium = 800;  // ~$5–10 at typical shard prices

    [Header("Rewards (12 weeks)")]
    public List<WeekReward> weekRewards = new List<WeekReward>();

    // ── Runtime ───────────────────────────────────────────────────────────────

    private const string PremiumKey = "dotr-bp-premium-";
    private const string ClaimedKey = "dotr-bp-claimed-";

    public bool HasPremium =>
        PlayerPrefs.GetInt(PremiumKey + seasonName, 0) == 1;

    public bool UnlockPremiumTrack()
    {
        if (!AetherShards.TrySpend(shardCostPremium)) return false;
        PlayerPrefs.SetInt(PremiumKey + seasonName, 1);
        return true;
    }

    public bool IsWeekClaimed(int week) =>
        PlayerPrefs.GetInt(ClaimedKey + seasonName + week, 0) == 1;

    public void ClaimWeekReward(int week)
    {
        if (IsWeekClaimed(week)) return;
        var entry = weekRewards.Find(r => r.week == week);

        entry.freeReward?.Unlock();
        if (HasPremium) entry.premiumReward?.Unlock();

        PlayerPrefs.SetInt(ClaimedKey + seasonName + week, 1);
        Debug.Log($"[BattlePass] Week {week} rewards claimed.");
    }
}
```

---

## 6. Building prestige system

Each building (Lumbermill, Workshop, Store) gets a **Prestige** tab in its UI.
Prestige is purely cosmetic — free levels give real gameplay bonuses, prestige
skins are visual only.

```csharp
// In LumbermillController.cs (or BuildingUIBase):
public void OpenPrestigeTab()
{
    var skins = MonetizationManager.Instance.GetByCategory(CosmeticCategory.BuildingSkin);
    // Build a UIElements list of skins filtered by building type.
    // Show locked items with their shard cost; unlocked show "Apply" button.
}

public void ApplyLumbermillSkin(CosmeticData skin)
{
    MonetizationManager.Instance.ApplyCosmeticToTarget(skin.cosmeticId, GetComponent<CosmeticApplicator>());
}
```

**Prestige skin examples to create as assets:**

| Building | Skin name | Visual change |
|---|---|---|
| Lumbermill | Golden Lumbermill | Gold material + floating coin particles |
| Workshop | Arcane Forge | Blue glowing metal + rune overlay |
| Store | Ancient Bazaar | Rich purple cloth + hanging lanterns |

---

## 7. Expansion DLC framework

DLC packs are unlocked via IAP and grant a batch of `CosmeticData` items plus
flag a set of content scenes/levels as accessible.

```csharp
// DLCManager.cs (simple)
public static class DLCManager
{
    public static bool IsExpansionOwned(string expansionId) =>
        PlayerPrefs.GetInt("dotr-dlc-" + expansionId, 0) == 1;

    public static void GrantExpansion(string expansionId, List<CosmeticData> bundledCosmetics)
    {
        PlayerPrefs.SetInt("dotr-dlc-" + expansionId, 1);
        foreach (var c in bundledCosmetics) c.Unlock();
        Debug.Log($"[DLCManager] Expansion unlocked: {expansionId}");
    }
}

// Usage after IAP confirmation:
DLCManager.GrantExpansion("shadow_realms",
    MonetizationManager.Instance.GetByCategory(CosmeticCategory.HeroSkin)
        .FindAll(c => c.cosmeticId.StartsWith("dlc1_")));
```

---

## 8. Rewarded ads (optional)

```csharp
// RewardedAdManager.cs (thin wrapper — implement with Unity Ads or IronSource):
public class RewardedAdManager : MonoBehaviour
{
    public void ShowResourceBoostAd(Action onSuccess)
    {
        // Show ad → on complete:
        // ResourceBoostService.StartBoost(multiplier: 1.5f, durationSeconds: 1800);
        onSuccess?.Invoke();
    }

    public void ShowReviveAd(Action onSuccess)
    {
        // On complete: restore hero to 50% HP, continue wave
        onSuccess?.Invoke();
    }
}
```

---

## 9. Cosmetic ScriptableObject assets to create

Create these in `Assets/Resources/Cosmetics/` :

| cosmeticId | displayName | Category | Source | Cost |
|---|---|---|---|---|
| `hero_wizard_archmage` | Archmage Robes | HeroSkin | Shop | 400 |
| `hero_knight_royal` | Royal Knight | HeroSkin | Shop | 400 |
| `pet_flamepup_phoenix` | Phoenix Pup | PetSkin | BattlePass | — |
| `tower_blast_crystal` | Crystal Cannon | TowerSkin | Shop | 300 |
| `building_lumbermill_golden` | Golden Lumbermill | BuildingSkin | Shop | 500 |
| `building_workshop_arcane` | Arcane Forge | BuildingSkin | Shop | 500 |
| `building_store_bazaar` | Ancient Bazaar | BuildingSkin | Shop | 500 |
| `vfx_spellfx_enhanced` | Enhanced Spell FX | VFXPack | Shop | 600 |
| `title_founder` | Founder | TitleFrame | SupporterPack | — |

---

## Files to Create / Edit

| File | Action |
|---|---|
| `Assets/_Modules/Monetization/AetherShards.cs` | **Create** |
| `Assets/_Modules/Monetization/CosmeticData.cs` | **Create** |
| `Assets/_Modules/Monetization/CosmeticApplicator.cs` | **Create** |
| `Assets/_Modules/Monetization/MonetizationManager.cs` | **Create** |
| `Assets/_Modules/Monetization/BattlePassData.cs` | **Create** |
| `Assets/_Modules/Monetization/DLCManager.cs` | **Create** |
| `Assets/_Modules/Monetization/RewardedAdManager.cs` | **Create** |
| `Assets/Resources/Cosmetics/*.asset` | **Create** — one per cosmetic item |
| Lumbermill / Workshop / Store UI | **Edit** — add Prestige tab |
| `Assets/_Modules/Village/Waves/WaveManager.cs` | **Edit** — call `AetherShards.OnWaveCleared()` |

---

## Acceptance Criteria

- [ ] `AetherShards.TrySpend()` correctly deducts and persists balance
- [ ] `AetherShards.OnWaveCleared()` awards 1 shard per wave (5 on milestone)
- [ ] `CosmeticApplicator.ApplySkin()` visibly swaps materials on hero/tower/building
- [ ] `CosmeticApplicator.ResetToDefault()` fully restores original materials
- [ ] Battle Pass week rewards unlock on `ClaimWeekReward()` — free and premium tracks separate
- [ ] Supporter Pack grants all `CosmeticSource.SupporterPack` items in one call
- [ ] DLC expansion flag persists across restarts
- [ ] **Zero** `CosmeticData` assets set any stats, damage, health, or tower range values
- [ ] Lumbermill / Workshop / Store each have a functional Prestige tab
- [ ] `MonetizationManager` never references `TowerData.damage`, `HeroStats`, or `WaveManager.difficulty`
