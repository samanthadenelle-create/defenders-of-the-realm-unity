# WORK ORDER 31 — Build Menu: Tower Build/Upgrade Dialog

**Status:** CLOSED — SUPERSEDED (owner-approved sweep 2026-08-09: build/upgrade dialogs owned by the Build HUD line — BuildHudController + BUILD_HUD_RECONCILED_SPEC / WO-1010/1012)
**Date:** 2026-05-26
**Author:** Owner design spec — playtest screenshot + description
**Priority:** High — Build button currently shows a bare card with no element choice,
              material costs, or timing information

---

## Problem / Owner Direction

> "Clicking build button should bring some dialog or option to select tower or build
> tower and show current supply of items needed to build as well as wait times on
> building each/upgrading each. Thinking radio button for element type if build build
> tower — maybe upgrade tower popup for which tower and cost and timing."

The current `BuildMenu` shows a single "Build Tower" card and a "Repair Wall" card.
There is no element selection, no material cost display, and no upgrade path.

---

## Desired Flow

```
[Build] button →
    ┌──────────────────────────────────┐
    │  BUILD                    [✕]   │
    │  ◆ 233 (current crystals)        │
    │                                  │
    │  [🏗  Build Tower]               │
    │  [⬆  Upgrade Tower]             │
    │  [🔧  Repair Wall]              │
    └──────────────────────────────────┘

"Build Tower" →
    ┌──────────────────────────────────┐
    │  BUILD TOWER              [✕]   │
    │  ← Back                          │
    │                                  │
    │  Element:                        │
    │  ◉ Flame   ○ Ice   ○ Aether     │
    │  ○ Physical                      │
    │                                  │
    │  Cost:  ◆ 150 crystals           │
    │         Wood: 20 / 20  ✓         │
    │         Stone: 10 / 5  ✗         │
    │                                  │
    │  Build time: 2m 30s              │
    │                                  │
    │  [Build]  (greyed if unaffordable)│
    └──────────────────────────────────┘

"Upgrade Tower" →
    ┌──────────────────────────────────┐
    │  UPGRADE TOWER            [✕]   │
    │  ← Back                          │
    │                                  │
    │  Select tower to upgrade:        │
    │  ◉ Arcane Tower  (Aether, Lvl 1) │
    │  ○ North Tower   (Flame,  Lvl 1) │
    │                                  │
    │  Upgrade cost: ◆ 200 + Stone 15  │
    │  Upgrade time: 5m 0s             │
    │  Result: Aether Tower Lvl 2      │
    │  (adds: +25 DPS, +50 HP)         │
    │                                  │
    │  [Upgrade]                       │
    └──────────────────────────────────┘
```

---

## Data Model

### Tower Element Variants (new stub data — Week 4+)

Add a new JSON: `Assets/StreamingAssets/Data/Canonical/tower-variants.json`

```json
{
  "version": 1,
  "variants": [
    {
      "id": "tower-flame",
      "element": "Flame",
      "displayName": "Flame Tower",
      "crystalCost": 150,
      "materials": [
        { "id": "wood",  "amount": 20 },
        { "id": "stone", "amount": 5  }
      ],
      "buildTimeSec": 150,
      "upgradeTimeSec": 300,
      "upgradeCrystalCost": 200,
      "upgradeMaterials": [
        { "id": "stone", "amount": 15 }
      ],
      "dps": 30,
      "hp": 200
    },
    { "id": "tower-ice",    "element": "Ice",      "crystalCost": 150, "buildTimeSec": 150, ... },
    { "id": "tower-aether", "element": "Aether",   "crystalCost": 180, "buildTimeSec": 180, ... },
    { "id": "tower-physical","element": "Physical", "crystalCost": 120, "buildTimeSec": 120, ... }
  ]
}
```

For **Week 4 stub**: hardcode these values directly in `BuildMenu.cs` as a
`TowerVariantDef` list (no JSON yet) to unblock the UI. Wire the JSON load in Week 6.

### Material inventory stub

For now read `GameState.Resources.Crystals` for crystal check (already done).
Add stub material counts as static constants (Wood=20, Stone=5) until the Week 6
resource system tracks materials. Clearly label as `// STUB — Week 6`.

---

## Implementation

### 1. New menu state enum in `BuildMenu.cs`

```csharp
private enum MenuScreen { Root, BuildTower, UpgradeTower }
private MenuScreen _screen = MenuScreen.Root;
private ElementType _selectedElement = ElementType.Flame;
```

### 2. `Render()` — dispatch to sub-screens

```csharp
public void Render()
{
    if (_list == null) return;
    _list.Clear();
    UpdateBalanceLabel();
    switch (_screen)
    {
        case MenuScreen.Root:        RenderRoot();         break;
        case MenuScreen.BuildTower:  RenderBuildTower();   break;
        case MenuScreen.UpgradeTower:RenderUpgradeTower(); break;
    }
}
```

### 3. `RenderRoot()` — two big option tiles + Repair Wall

Three large tappable tiles:
- **Build Tower** → `_screen = MenuScreen.BuildTower; Render()`
- **Upgrade Tower** → `_screen = MenuScreen.UpgradeTower; Render()`
- **Repair Wall** → existing `InvokeRepairNearestWall()` (keep as-is)

### 4. `RenderBuildTower()` — element radio + costs + timing

```csharp
private void RenderBuildTower()
{
    // ← Back button sets _screen = MenuScreen.Root
    _list.Add(BuildBackButton());

    // Element radio group: Flame / Ice / Aether / Physical
    var radioGroup = new VisualElement();
    radioGroup.AddToClassList("element-radio-group");
    foreach (ElementType el in new[] { ElementType.Flame, ElementType.Ice,
                                        ElementType.Aether, ElementType.Physical })
    {
        var row = BuildElementRadioRow(el);
        radioGroup.Add(row);
    }
    _list.Add(radioGroup);

    // Cost + timing block for the selected element variant
    var variant = VariantFor(_selectedElement);
    _list.Add(BuildCostBlock(variant));     // crystals + material rows with ✓/✗
    _list.Add(BuildTimingLabel(variant));   // "Build time: 2m 30s"

    // Confirm button — greyed if any cost unmet
    bool canBuild = CanAfford(variant);
    var btn = new Button(() => OnConfirmBuild(variant)) { text = "Build" };
    btn.SetEnabled(canBuild);
    _list.Add(btn);
}
```

**Element radio row**: a `RadioButton`-style toggle row. Since UI Toolkit's
`RadioButton` in Unity 6 requires a `RadioButtonGroup`, use a simple
`VisualElement` row with a toggle-style button that sets `_selectedElement` and
calls `Render()`:

```csharp
private VisualElement BuildElementRadioRow(ElementType el)
{
    bool selected = el == _selectedElement;
    var row = new Button(() => { _selectedElement = el; Render(); });
    row.AddToClassList("element-radio-row");
    row.EnableInClassList("element-radio-row--selected", selected);
    row.text = (selected ? "◉ " : "○ ") + el.ToString();
    return row;
}
```

**Cost block**: shows crystal row + one row per material. Each material row:
- Label: `"Wood: 20"` with a ✓ (green) if inventory ≥ required, ✗ (red) if not.
- Use stub inventory: `GetMaterialCount(id)` returns hardcoded values until Week 6.

**Timing label**: format `buildTimeSec` as `"Build time: Xm Ys"`.

### 5. `RenderUpgradeTower()` — tower list + upgrade info

```csharp
private void RenderUpgradeTower()
{
    _list.Add(BuildBackButton());

    // Find all placed Building components of type ArcaneTower in the scene
    var towers = UnityEngine.Object.FindObjectsByType<Building>(
        FindObjectsInactive.Exclude, FindObjectsSortMode.None);

    var towerList = new VisualElement();
    towerList.AddToClassList("tower-select-list");
    bool any = false;
    foreach (var b in towers)
    {
        if (b.Type != BuildingType.ArcaneTower) continue;
        var row = BuildTowerSelectRow(b);
        towerList.Add(row);
        any = true;
    }
    if (!any)
        towerList.Add(new Label("No towers placed yet."));
    _list.Add(towerList);

    if (_selectedTowerForUpgrade != null)
        _list.Add(BuildUpgradeInfoBlock(_selectedTowerForUpgrade));
}
```

Add `private Building _selectedTowerForUpgrade;` field.
`BuildTowerSelectRow(b)`: shows tower name + level + element (from `Building.Def`).
Clicking selects it and calls `Render()`.

`BuildUpgradeInfoBlock(b)`: shows upgrade crystal cost, materials, time, and result
description. Includes an "Upgrade" button that stubs the upgrade action
(`Debug.Log("[BuildMenu] Upgrade stub — Week 6")`) for now.

---

## UXML Changes (`BuildMenu.uxml`)

No structural UXML changes required — all sub-screens build into the existing
`build-menu-list` container at runtime. The `build-menu-panel` chrome (header,
balance, close) stays. Do add:

```xml
<!-- Separate back-nav container (shown on sub-screens, hidden on root) -->
<ui:VisualElement name="build-menu-back-row" class="build-menu-back-row" />
```

### USS additions (`BuildMenu.uss`)

Add classes:
- `.element-radio-group` — flex-column, gap 6px
- `.element-radio-row` — pill-style button, amber border
- `.element-radio-row--selected` — filled amber background
- `.cost-row` — flex-row, space-between
- `.cost-check` — green ✓ text
- `.cost-fail` — red ✗ text
- `.build-menu-back-btn` — small grey text button, left-aligned
- `.tower-select-list` — flex-column, gap 4px
- `.tower-row` — pill button, slate style
- `.tower-row--selected` — filled

---

## Files to Create / Edit

| File | Change |
|---|---|
| `Assets/_Modules/Village/Buildings/UI/BuildMenu.cs` | Add `MenuScreen` enum, `_selectedElement`, `_selectedTowerForUpgrade`; new `RenderRoot/RenderBuildTower/RenderUpgradeTower` methods; stub `TowerVariantDef` list; `CanAfford`, `GetMaterialCount` stubs |
| `Assets/_Modules/Village/Buildings/UI/BuildMenu.uxml` | Add `build-menu-back-row` element |
| `Assets/_Modules/Village/Buildings/UI/BuildMenu.uss` | Add element-radio, cost-row, tower-select USS classes |

Do **NOT** add a dependency on `DeNelle.BattleATB` from `DeNelle.Village`. Copy the
element names as a local `TowerElement` string enum or use the existing
`DamageElement` enum from `DeNelle.Core.Combat.IDamageable` (already referenced by
Core asmdef):

```csharp
// Local alias — avoids cross-asmdef dep on BattleATB
private enum TowerElement { Flame, Ice, Aether, Physical }
```

---

## Acceptance Criteria

- [ ] Clicking "Build" opens the panel with three tiles: Build Tower, Upgrade Tower, Repair Wall
- [ ] Tapping "Build Tower" shows element radio buttons (Flame / Ice / Aether / Physical), crystal cost, material counts with ✓/✗, and build time
- [ ] Selecting a different element radio updates the cost and timing immediately
- [ ] "Build" button is greyed when crystals or materials are insufficient
- [ ] Tapping "Upgrade Tower" shows a list of placed towers; selecting one shows upgrade cost + time
- [ ] "← Back" returns to the root screen
- [ ] "Repair Wall" still works as before
- [ ] No regressions: HUD ability bar and START WAVE button still click correctly
- [ ] Label values are clearly marked `// STUB — Week 6` where hardcoded
