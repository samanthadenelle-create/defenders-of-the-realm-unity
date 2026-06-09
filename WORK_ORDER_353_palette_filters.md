# WO-353: Palette Filters & Category Tabs

**Status:** READY TO IMPLEMENT  
**Estimated Effort:** P1 (1–2 days)  
**Priority:** High (QoL, reduces scroll fatigue)  
**Lane:** HUD/UI (parallel to WO-352, WO-354)

---

## Overview

Add filterable category tabs (All, Defenses, Resources, Utility) above the structure palette card strip. Clicking a tab narrows the visible cards to that category. Maintains armed state across filter changes. Improves discoverability when 10+ structures exist.

**Why:** Village building with many buildables becomes tedious if players must scroll through all cards to find a specific type. Tabs provide instant narrowing while preserving full palette on "All" view.

---

## Acceptance Criteria

- [ ] Filter tab row displays above card strip (All / Defenses / Resources / Utility)
- [ ] Clicking a tab filters cards to that CatalogType; updates palette immediately
- [ ] "All" tab shows unfiltered list (existing behavior)
- [ ] Armed entry remains armed when switching filters
- [ ] Armed entry's tab auto-highlights when tab is switched (visual feedback)
- [ ] Unaffordable cards still grey out (cost logic unchanged)
- [ ] Tabs styled consistently (stone/gilt theme from ElarionUi)
- [ ] Active tab highlighted (blue bg), inactive tabs grey
- [ ] Works on landscape + portrait (horizontal scroll on narrow screens if needed)
- [ ] Zero allocations during filter changes (reuse card objects)

---

## Files to Modify

### Existing Files
- `Assets/_Modules/Village/BuildMode/BuildPaletteUI.cs` — Add filter tab row, implement filter logic

### No Changes Required
- `Assets/_Modules/Village/BuildMode/BuildModeController.cs` (palette interface unchanged)
- `DeNelle.Core.Catalog.CatalogRegistry` (filtering handled on client side)

---

## Design Spec

### Tab Row Layout
- **Position:** Above card strip, full width of palette
- **Height:** 36px
- **Background:** PanelStoneDark (recessed tray)
- **Tabs:** 4 buttons, equal width or flex-space-between
- **Styling:**
  - **Active tab:** Blue background (ElarionUi.AetherDim), white text, bold
  - **Inactive tab:** Stone background, parchment text, normal weight
  - **Hover:** Slight opacity increase
  - **Padding:** 6px horizontal, 4px vertical per tab

### Tab Labels
```
All  |  Defenses  |  Resources  |  Utility
```

**Categories (by CatalogType enum):**
- **Defenses:** Tower, Wall, Gate
- **Resources:** Resource (trees, farms, mines)
- **Utility:** Watchtower, Armory, other support structures (defined by repo.catalogType)

---

## Implementation Notes

### BuildPaletteUI.cs Structure
```csharp
private VisualElement _tabRow;           // NEW: tab button container
private Button[] _tabButtons;            // NEW: refs to tab buttons
private CatalogType[] _filterTypes;      // NEW: types for each tab
private CatalogType _activeFilter;       // NEW: current filter

public void EnsureBuilt()
{
    // ... existing setup ...
    
    // NEW: Build tab row above card strip
    BuildTabRow();
}

private void BuildTabRow()
{
    _tabRow = new VisualElement { name = "build-palette-tab-row" };
    _tabRow.style.height = 36;
    _tabRow.style.flexDirection = FlexDirection.Row;
    _tabRow.style.backgroundColor = ElarionUi.PanelStoneDark;
    _tabRow.style.borderBottomWidth = 1;
    _tabRow.style.borderBottomColor = ElarionUi.StoneTrim;
    _root.Add(_tabRow);
    
    var tabs = new[] { ("All", (CatalogType?)null), ("Defenses", CatalogType.Tower), ... };
    _tabButtons = new Button[tabs.Length];
    
    for (int i = 0; i < tabs.Length; i++)
    {
        var btn = new Button(() => SetFilter(tabs[i].type)) { text = tabs[i].label };
        ElarionUi.StyleButton(btn, i == 0 ? ElarionUi.ButtonKind.Gold : ElarionUi.ButtonKind.Neutral);
        btn.style.flex = 1;  // equal width
        _tabButtons[i] = btn;
        _tabRow.Add(btn);
    }
}

public void SetFilter(CatalogType? type)
{
    _activeFilter = type ?? (CatalogType)(-1);  // -1 = "All"
    Render();  // Re-render card strip with filter applied
    UpdateTabButtonStates();
}

private void UpdateTabButtonStates()
{
    foreach (var btn in _tabButtons)
    {
        // Highlight active tab
    }
}

public void Render()
{
    EnsureBuilt();
    if (_strip == null) return;
    
    _strip.Clear();
    UpdateBalance();
    UpdateOrientButton();
    
    // Apply filter before rendering cards
    var entriesToShow = FilterEntries();
    
    foreach (var e in entriesToShow)
    {
        if (e == null) continue;
        _strip.Add(BuildCard(e));
    }
}

private List<CatalogEntry> FilterEntries()
{
    var result = new List<CatalogEntry>();
    
    if (_activeFilter == (CatalogType)(-1))  // "All"
    {
        foreach (var type in _types)
            AddEntriesOfType(type, result);
    }
    else
    {
        AddEntriesOfType(_activeFilter, result);
    }
    
    return result;
}

private void AddEntriesOfType(CatalogType type, List<CatalogEntry> result)
{
    var entries = CatalogRegistry.OfType(type);
    if (entries == null) return;
    foreach (var e in entries) result.Add(e);
}
```

### Armed State Persistence
When armed entry's type is filtered out:
```csharp
public void Render()
{
    // ... filter logic ...
    
    // If armed entry is not in current filter, auto-switch to its tab
    if (!string.IsNullOrEmpty(_armedId) && !entriesToShow.Any(e => e.id == _armedId))
    {
        var armedEntry = CatalogRegistry.Get(_armedId);
        if (armedEntry != null && armedEntry.repo?.catalogType != null)
        {
            SetFilter(armedEntry.repo.catalogType);
            return;  // Re-render with correct filter
        }
    }
}
```

---

## Testing Checklist

- [ ] "All" tab shows all registered structures
- [ ] "Defenses" tab shows only Tower, Wall, Gate types
- [ ] "Resources" tab shows Resource type only
- [ ] "Utility" tab shows support structures
- [ ] Clicking a tab re-renders palette immediately
- [ ] Armed entry stays armed when switching tabs
- [ ] If armed entry's tab is filtered out, auto-switch to its tab
- [ ] Tab highlighting updates correctly
- [ ] Unaffordable cards still grey in any filter
- [ ] Works on mobile (tabs wrap or scroll if needed)
- [ ] No allocations during filter changes (profile)

---

## What NOT to Touch

- Card building logic (BuildCard)
- Cost resolution (CostFor, CanAfford)
- Arm/disarm behavior (OnEntrySelected)
- CatalogRegistry (query as-is)

---

## Dependencies

- **Depends on:** WO-108 (BuildPaletteUI, CatalogRegistry)
- **Unblocks:** None (independent feature)
- **Parallel:** WO-352 (preview panel), WO-354 (synergy display)

---

## Acceptance Sign-Off

- [ ] Brace balance check passed
- [ ] No allocations in Render loop
- [ ] All UI elements responsive (44px+ buttons on mobile)
- [ ] Works in WebGL build
