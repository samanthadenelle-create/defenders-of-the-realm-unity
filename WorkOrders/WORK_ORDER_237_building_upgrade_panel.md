<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK ORDER 237 — Building Upgrade Panel (Lumbermill + data-driven system)

**Status: READY TO IMPLEMENT**
**Author:** UI (creative lane)
**WO Number:** 237
**Date:** 2026-06-02
**Closes:** upgrade interaction stub in `BuildingInteractable.cs` (currently shows a toast only)

---

## Assembly map

| File | Assembly | Namespace |
|---|---|---|
| `UpgradeData.cs` | `DeNelle.Core` | `DeNelle.Core` |
| `UpgradeLoader.cs` | `DeNelle.Village` | `DeNelle.Village` |
| `Building.cs` (extend) | `DeNelle.Village` | `DeNelle.Village` |
| `BuildingUpgradePanel.cs` | `DeNelle.Village` | `DeNelle.Village` |
| `BuildingInteractable.cs` (extend) | `DeNelle.Village` | `DeNelle.Village` |
| `Resources/Data/Upgrades/*.json` | n/a | n/a |

**Cross-assembly rule:** Village → Core only. Panel lives in Village (not HUD) because it talks to `EconomyService` and `Building`, both in Village. No `using DeNelle.HUD` anywhere in these files.

---

## Step 1 — `UpgradeData.cs` (new, `DeNelle.Core`)

```csharp
// Assets/_Modules/Core/Upgrades/UpgradeData.cs
using System;
using System.Collections.Generic;

namespace DeNelle.Core
{
    [Serializable]
    public class UpgradeData
    {
        public string id;
        public string title;
        public string description;
        public int woodCost;
        public int stoneCost;
        public int ironCost;
        public int crystalCost;
        public List<StatBoost> boosts = new List<StatBoost>();
    }

    [Serializable]
    public class StatBoost
    {
        public string stat;
        public float value;
        public string type; // "add" or "multiply"
    }
}
```

---

## Step 2 — `UpgradeLoader.cs` (new, `DeNelle.Village`)

```csharp
// Assets/_Modules/Village/Buildings/UpgradeLoader.cs
using System.Collections.Generic;
using DeNelle.Core;
using UnityEngine;

namespace DeNelle.Village
{
    public static class UpgradeLoader
    {
        public static List<UpgradeData> LoadUpgrades(string buildingName)
        {
            var asset = Resources.Load<TextAsset>($"Data/Upgrades/{buildingName}Upgrades");
            if (asset == null)
            {
                Debug.LogWarning($"[UpgradeLoader] No upgrade JSON found for '{buildingName}'.");
                return new List<UpgradeData>();
            }
            var wrapper = JsonUtility.FromJson<UpgradeWrapper>(asset.text);
            return wrapper?.upgrades ?? new List<UpgradeData>();
        }

        [System.Serializable]
        private class UpgradeWrapper { public List<UpgradeData> upgrades; }
    }
}
```

---

## Step 3 — Extend `Building.cs`

Add to the existing `Building` class in `Assets/_Modules/Village/Buildings/Building.cs`. Do NOT rewrite the file — append these fields to the class body:

```csharp
// ── Upgrade fields (WO-237) ──────────────────────────────────────────────────
[Header("Upgrades")]
public bool isUpgradable = false;   // set true in Inspector for upgradable buildings
public int  currentLevel = 1;
public int  maxLevel     = 5;

[HideInInspector]
public List<DeNelle.Core.UpgradeData> availableUpgrades = new List<DeNelle.Core.UpgradeData>();

private void LoadUpgradesIfNeeded()
{
    if (isUpgradable && availableUpgrades.Count == 0)
        availableUpgrades = UpgradeLoader.LoadUpgrades(buildingName);
}

public bool HasUpgradesAvailable() =>
    isUpgradable && currentLevel < maxLevel && availableUpgrades.Count > 0;
// ─────────────────────────────────────────────────────────────────────────────
```

Also add `LoadUpgradesIfNeeded()` to the existing `Start()` or `Configure()` method — whichever runs on scene load.

**Note:** `Lumbermill` is not yet in the `BuildingType` enum. Add it:
```csharp
/// <summary>Resource building — yields Wood.</summary>
Lumbermill = 5,
```

---

## Step 4 — `BuildingUpgradePanel.cs` (new, `DeNelle.Village`)

Code-built. No UXML. No UIDocument. Plain Canvas.

```csharp
// Assets/_Modules/Village/Buildings/BuildingUpgradePanel.cs
using System;
using System.Collections.Generic;
using DeNelle.Core;
using UnityEngine;
using UnityEngine.UI;

namespace DeNelle.Village
{
    /// <summary>
    /// Code-built upgrade panel. Spawned once by BuildingInteractable; shown/hidden per interaction.
    /// No UXML. No UIDocument.
    /// </summary>
    public sealed class BuildingUpgradePanel : MonoBehaviour
    {
        // ── singleton within the village scene ──────────────────────────────
        public static BuildingUpgradePanel Instance { get; private set; }

        private Canvas        _canvas;
        private GameObject    _panel;
        private Text          _nameLabel;
        private Text          _levelLabel;
        private Transform     _btnContainer;
        private Button        _closeBtn;

        private Building      _currentBuilding;
        private EconomyService _economy;

        // ── lifecycle ───────────────────────────────────────────────────────
        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;

            _economy = FindObjectOfType<EconomyService>();
            BuildCanvas();
            Hide();
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        // ── public API ──────────────────────────────────────────────────────
        public void Show(Building building)
        {
            _currentBuilding = building;
            Refresh();
            _panel.SetActive(true);
        }

        public void Hide()
        {
            _panel?.SetActive(false);
            _currentBuilding = null;
        }

        // ── internal ────────────────────────────────────────────────────────
        private void Refresh()
        {
            if (_currentBuilding == null) return;

            _nameLabel.text  = _currentBuilding.buildingName;
            _levelLabel.text = $"Level {_currentBuilding.currentLevel} / {_currentBuilding.maxLevel}";

            foreach (Transform child in _btnContainer)
                Destroy(child.gameObject);

            if (_currentBuilding.currentLevel >= _currentBuilding.maxLevel)
            {
                AddLabel(_btnContainer, "Max level reached.", 14);
                return;
            }

            foreach (var upgrade in _currentBuilding.availableUpgrades)
                AddUpgradeButton(upgrade);
        }

        private void AddUpgradeButton(UpgradeData upgrade)
        {
            var go  = new GameObject(upgrade.id, typeof(RectTransform));
            go.transform.SetParent(_btnContainer, false);

            var bg = go.AddComponent<Image>();
            bg.color = new Color(0.15f, 0.12f, 0.10f, 0.95f);
            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(320f, 72f);

            // Title
            var title = AddLabel(go.transform, upgrade.title, 15);
            title.GetComponent<RectTransform>().anchoredPosition = new Vector2(10f, 20f);
            title.GetComponent<RectTransform>().anchorMin        = new Vector2(0f, 0.5f);
            title.GetComponent<RectTransform>().anchorMax        = new Vector2(0f, 0.5f);

            // Description
            var desc = AddLabel(go.transform, upgrade.description, 11);
            var descRect = desc.GetComponent<RectTransform>();
            descRect.anchoredPosition = new Vector2(10f, -10f);
            descRect.anchorMin = descRect.anchorMax = new Vector2(0f, 0.5f);

            // Cost line
            string costStr = FormatCost(upgrade);
            var cost = AddLabel(go.transform, costStr, 11);
            var costRect = cost.GetComponent<RectTransform>();
            costRect.anchoredPosition = new Vector2(10f, -26f);
            costRect.anchorMin = costRect.anchorMax = new Vector2(0f, 0.5f);

            // Buy button
            var btnGo  = new GameObject("BuyBtn", typeof(RectTransform));
            btnGo.transform.SetParent(go.transform, false);
            var btnRect = btnGo.GetComponent<RectTransform>();
            btnRect.sizeDelta        = new Vector2(72f, 36f);
            btnRect.anchoredPosition = new Vector2(-10f, 0f);
            btnRect.anchorMin        = new Vector2(1f, 0.5f);
            btnRect.anchorMax        = new Vector2(1f, 0.5f);

            var btnImg = btnGo.AddComponent<Image>();
            var btn    = btnGo.AddComponent<Button>();

            var cost2 = new ResourceCost(upgrade.woodCost, upgrade.stoneCost, upgrade.ironCost, upgrade.crystalCost);
            bool canAfford = _economy != null && _economy.CanAfford(cost2);

            btnImg.color = canAfford ? new Color(0.2f, 0.6f, 0.2f) : new Color(0.4f, 0.15f, 0.15f);
            btn.interactable = canAfford;

            var btnLabel = AddLabel(btnGo.transform, canAfford ? "Upgrade" : "Need more", 12);
            btnLabel.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;

            var upgRef   = upgrade;
            btn.onClick.AddListener(() => PurchaseUpgrade(upgRef));
        }

        private void PurchaseUpgrade(UpgradeData upgrade)
        {
            if (_currentBuilding == null || _economy == null) return;

            var cost = new ResourceCost(upgrade.woodCost, upgrade.stoneCost, upgrade.ironCost, upgrade.crystalCost);
            if (!_economy.TrySpend(cost))
            {
                Debug.Log("[BuildingUpgradePanel] Cannot afford upgrade.");
                return;
            }

            _currentBuilding.currentLevel = Mathf.Min(_currentBuilding.currentLevel + 1, _currentBuilding.maxLevel);
            Debug.Log($"[BuildingUpgradePanel] Applied '{upgrade.title}' to {_currentBuilding.buildingName} → Level {_currentBuilding.currentLevel}");

            Refresh(); // redraw panel with new level/affordability
        }

        // ── canvas builder ──────────────────────────────────────────────────
        private void BuildCanvas()
        {
            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 20;
            gameObject.AddComponent<CanvasScaler>();
            gameObject.AddComponent<GraphicRaycaster>();

            // Dim backing
            _panel = new GameObject("Panel", typeof(RectTransform));
            _panel.transform.SetParent(transform, false);
            var backing = _panel.AddComponent<Image>();
            backing.color = new Color(0f, 0f, 0f, 0.6f);
            var pr = _panel.GetComponent<RectTransform>();
            pr.anchorMin = Vector2.zero; pr.anchorMax = Vector2.one;
            pr.offsetMin = pr.offsetMax = Vector2.zero;

            // Inner card
            var card = new GameObject("Card", typeof(RectTransform));
            card.transform.SetParent(_panel.transform, false);
            var cardImg  = card.AddComponent<Image>();
            cardImg.color = new Color(0.08f, 0.06f, 0.05f, 0.97f);
            var cr = card.GetComponent<RectTransform>();
            cr.sizeDelta        = new Vector2(360f, 460f);
            cr.anchoredPosition = Vector2.zero;
            cr.anchorMin        = new Vector2(0.5f, 0.5f);
            cr.anchorMax        = new Vector2(0.5f, 0.5f);

            // Name label
            _nameLabel  = AddLabel(card.transform, "Building", 18);
            _nameLabel.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 190f);

            // Level label
            _levelLabel = AddLabel(card.transform, "Level 1 / 5", 13);
            _levelLabel.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 168f);

            // Upgrade button container
            var containerGo = new GameObject("UpgradeContainer", typeof(RectTransform));
            containerGo.transform.SetParent(card.transform, false);
            _btnContainer = containerGo.transform;
            var vl = containerGo.AddComponent<VerticalLayoutGroup>();
            vl.spacing     = 8f;
            vl.padding     = new RectOffset(12, 12, 0, 0);
            vl.childAlignment = TextAnchor.UpperCenter;
            var contRect = containerGo.GetComponent<RectTransform>();
            contRect.sizeDelta        = new Vector2(340f, 340f);
            contRect.anchoredPosition = new Vector2(0f, 0f);
            contRect.anchorMin        = new Vector2(0.5f, 0.5f);
            contRect.anchorMax        = new Vector2(0.5f, 0.5f);

            // Close button
            var closeGo = new GameObject("CloseBtn", typeof(RectTransform));
            closeGo.transform.SetParent(card.transform, false);
            closeGo.AddComponent<Image>().color = new Color(0.5f, 0.1f, 0.1f);
            _closeBtn = closeGo.AddComponent<Button>();
            _closeBtn.onClick.AddListener(Hide);
            var closeRect = closeGo.GetComponent<RectTransform>();
            closeRect.sizeDelta        = new Vector2(80f, 32f);
            closeRect.anchoredPosition = new Vector2(0f, -210f);
            closeRect.anchorMin        = new Vector2(0.5f, 0.5f);
            closeRect.anchorMax        = new Vector2(0.5f, 0.5f);
            AddLabel(closeGo.transform, "Close", 13);
        }

        private static Text AddLabel(Transform parent, string text, int fontSize)
        {
            var go  = new GameObject("Label", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>();
            t.text      = text;
            t.fontSize  = fontSize;
            t.color     = Color.white;
            t.alignment = TextAnchor.MiddleCenter;
            t.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var r = go.GetComponent<RectTransform>();
            r.sizeDelta = new Vector2(320f, 24f);
            return t;
        }

        private static string FormatCost(UpgradeData u)
        {
            var parts = new List<string>();
            if (u.woodCost    > 0) parts.Add($"{u.woodCost} Wood");
            if (u.stoneCost   > 0) parts.Add($"{u.stoneCost} Stone");
            if (u.ironCost    > 0) parts.Add($"{u.ironCost} Iron");
            if (u.crystalCost > 0) parts.Add($"{u.crystalCost} Crystals");
            return parts.Count > 0 ? "Cost: " + string.Join(" · ", parts) : "Free";
        }
    }
}
```

---

## Step 5 — Extend `BuildingInteractable.cs`

Replace the stub toast actions with real upgrade panel calls:

```csharp
// In BuildingInteractable — replace the F-key action block:
private void OnInteract()
{
    if (_building.HasUpgradesAvailable())
    {
        if (BuildingUpgradePanel.Instance == null)
        {
            var go = new GameObject("BuildingUpgradePanel");
            go.AddComponent<BuildingUpgradePanel>();
        }
        BuildingUpgradePanel.Instance?.Show(_building);
    }
    else
    {
        // existing toast fallback for non-upgradable buildings
        Debug.Log($"[BuildingInteractable] {_building.buildingName} has no upgrades.");
    }
}
```

---

## Step 6 — JSON data files

**`Assets/Resources/Data/Upgrades/LumbermillUpgrades.json`**

```json
{
  "upgrades": [
    {
      "id": "lumber_efficiency",
      "title": "Improved Saws",
      "description": "Increases wood gathering speed by 25%",
      "woodCost": 80,
      "stoneCost": 0,
      "ironCost": 30,
      "crystalCost": 0,
      "boosts": [
        { "stat": "WoodGatherSpeed", "value": 1.25, "type": "multiply" }
      ]
    },
    {
      "id": "stronger_axes",
      "title": "Reinforced Axes",
      "description": "Hero deals +15% damage near the Lumbermill",
      "woodCost": 60,
      "stoneCost": 0,
      "ironCost": 80,
      "crystalCost": 20,
      "boosts": [
        { "stat": "Damage", "value": 1.15, "type": "multiply" }
      ]
    },
    {
      "id": "lumber_stockpile",
      "title": "Expanded Stockpile",
      "description": "Wood cap increased by 200",
      "woodCost": 120,
      "stoneCost": 40,
      "ironCost": 0,
      "crystalCost": 0,
      "boosts": [
        { "stat": "WoodCap", "value": 200, "type": "add" }
      ]
    }
  ]
}
```

Create the same pattern for any other upgradable building — `FarmUpgrades.json`, `WorkshopUpgrades.json` etc.

---

## Inspector setup

| Building GameObject | `isUpgradable` | `buildingName` (must match JSON filename prefix) |
|---|---|---|
| Lumbermill | `true` | `"Lumbermill"` |
| Farm | `true` | `"Farm"` |
| Workshop | `true` | `"Workshop"` |
| Crystal Mine | `true` | `"CrystalMine"` |
| Gates / Walls | `false` | — |
| Decorative props | `false` | — |

---

## Acceptance criteria

- [ ] Approaching a Lumbermill (F within range) opens the upgrade panel
- [ ] Panel shows building name, current level, and available upgrades with real resource costs
- [ ] "Upgrade" button is greyed out if player cannot afford
- [ ] Confirming an upgrade calls `EconomyService.TrySpend(ResourceCost)` — resources actually deducted
- [ ] `currentLevel` increments; panel refreshes; at max level shows "Max level reached"
- [ ] Non-upgradable buildings (`isUpgradable = false`) show no panel
- [ ] No UXML / UIDocument used
- [ ] Brace balance check passed on every `.cs` file edited or created

---

## What NOT to touch

- `Village.unity` — do not hand-edit
- `TowerPlacementSystem`, `WaveManager`, ATB scripts
- `EconomyService` internals — call only the public API (`CanAfford`, `TrySpend`)
