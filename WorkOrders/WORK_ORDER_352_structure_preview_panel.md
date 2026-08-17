<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WO-352: Structure Info Preview Panel

**Status:** READY TO IMPLEMENT

> **CHECKED 2026-08-14 (phantom sweep) - STAYS READY.** The panel exists, but the subscription is
> DELIBERATELY disabled at BuildModeController.cs:3828. This is real outstanding work, not a phantom.

**Estimated Effort:** P1 (2–3 days)  
**Priority:** High (UX clarity blocker)  
**Lane:** HUD/UI (parallel to WO-353, WO-354)

---

## Overview

Add a structure information preview panel that appears when the player taps a palette card **before** placing it. Shows full stats, cost, footprint, current upgrade tier, next tier benefits, and active synergy bonuses from nearby structures. Allows one-tap confirmation to arm the structure, or dismiss to continue browsing.

**Why:** Players currently tap a card → immediately place a ghost → discover they placed wrong structure or didn't understand the cost. This preview prevents regretted placements and clarifies upgrade benefits.

---

## Acceptance Criteria

- [ ] Panel appears on palette card tap (not on armed structure)
- [ ] Shows: name, description, current tier (Lv X/Max), footprint, cost (all resources), stats (DPS/Range/HP or structure-type defaults)
- [ ] Shows next tier preview: stat increases (bold), new auras/bonuses, upgrade cost
- [ ] Shows active bonuses from nearby structures (detected in real-time)
- [ ] "Place Structure" button arms the entry (replaces old tap behavior)
- [ ] "Cancel" or click-outside dismisses panel
- [ ] Panel responsive: left-side on landscape (≥600px), modal/bottom-sheet on portrait (<600px)
- [ ] All text within safe area (WCAG compliant)
- [ ] Code-built UI (no UXML); adopts PanelSettings from sibling UIDocument
- [ ] Zero GC allocation during preview (cached labels, reused containers)

---

## Files to Modify

### New Files
- `Assets/_Modules/Village/BuildMode/BuildStructureInfoPanel.cs` — Panel controller (show/hide, render content)

### Existing Files
- `Assets/_Modules/Village/BuildMode/BuildPaletteUI.cs` — Emit `OnCardTapped` event (instead of immediate OnEntrySelected)
- `Assets/_Modules/Village/BuildMode/BuildModeController.cs` — Subscribe to OnCardTapped, show preview panel, defer armed state until "Place" tap

### No Changes Required
- `Assets/_Modules/Village/BuildMode/GhostPreview.cs` (placement logic unchanged)
- `Assets/_Modules/Village/BuildMode/BuildSelectionUI.cs` (edit panel unchanged)
- `DeNelle.Core.Catalog.CatalogEntry` (stat fields already exist)

---

## Design Spec

### Panel Layout (Landscape ≥600px)
- **Position:** Left side, anchored to top-left of game viewport
- **Size:** 280px wide, 380px tall, vertically scrollable if content overflows
- **Background:** Stone (ElarionUi.PanelStone) with gilt trim on top
- **Sections (top to bottom):**
  1. **Header** (60px) — Icon + name + tier badge (Lv X/Max)
  2. **Cost** (50px) — Icon + multi-resource breakdown (◆, W, I, F)
  3. **Footprint** (40px) — "2×2 cells" + visual grid preview
  4. **Current Stats** (80px) — DPS, Range, HP (or structure-type defaults)
  5. **Active Bonuses** (60px) — List of nearby structure buffs (green badges)
  6. **Next Tier Preview** (100px) — "Upgrade to Lv 2" box with stat deltas + cost + "Upgrade" button
  7. **Action Buttons** (40px) — "Place Structure" (blue) + "Cancel" (grey)

### Panel Layout (Portrait <600px)
- **Position:** Bottom sheet, full width, 60% of viewport height
- **Behavior:** Swipe-up to expand, swipe-down to dismiss
- **Content:** Vertical stack, same sections, compact spacing

### Content Rules

**Stats Display:**
```
DPS       12  →  18 (Lv 2)
Range     8m  →  9m
HP        45  →  60
```
Current stat on left, next-tier delta on right (green if increase).

**Cost Label:**
```
◆ 75  W 20
```
Skip zero-cost resources. Use icons + compact notation.

**Active Bonuses Example:**
```
+ 8% DPS
  from Lumbermill (Lv 2)

+ 15% Range
  from Watchtower (within 12m)
```
Green text on transparent green background. Muted if not currently active (e.g., "Unlocks at Lv 2").

**Next Tier Box (blue info panel):**
```
Upgrade to Lv 2
New benefits:
  • DPS: 12 → 18
  • Range: 8m → 9m
  • +5% defense aura (adjacent structures)

Cost: ◆ 45  W 30
[Upgrade Now button]
```

---

## Implementation Notes

### BuildStructureInfoPanel.cs
```csharp
public sealed class BuildStructureInfoPanel : MonoBehaviour
{
    public event Action<CatalogEntry> OnPlaceRequested;  // "Place" tap
    public event Action OnCancelRequested;               // "Cancel" tap

    public void Show(CatalogEntry entry)
    {
        // Render layout
        // Populate stats from entry + CatalogRegistry
        // Query nearby structures for synergies (PlacementGrid bounds check)
        // Animate in
    }

    public void Hide()
    {
        // Animate out, clear content
    }

    private void RenderCostSection(CatalogEntry entry) { }
    private void RenderCurrentStats(CatalogEntry entry) { }
    private void RenderNextTierPreview(CatalogEntry entry) { }
    private void RenderActiveBonuses(CatalogEntry entry, Vector3 ghostPosition) { }
}
```

### BuildPaletteUI.cs Changes
**Before:**
```csharp
card.clicked += () => {
    _armedId = e.id;
    OnEntrySelected?.Invoke(e);  // Immediate arm
};
```

**After:**
```csharp
public event Action<CatalogEntry> OnCardTapped;  // NEW

card.clicked += () => {
    OnCardTapped?.Invoke(e);  // Defer to controller
};
```

### BuildModeController.cs Changes
```csharp
private BuildStructureInfoPanel _infoPanel;

private void OnEnable()
{
    _palette.OnCardTapped += (entry) =>
    {
        _infoPanel.Show(entry);
        // Ghost is NOT armed yet
    };

    _infoPanel.OnPlaceRequested += (entry) =>
    {
        _armed = entry;
        _infoPanel.Hide();
        _ghost.SetEntry(entry);  // Show ghost
    };

    _infoPanel.OnCancelRequested += () =>
    {
        _infoPanel.Hide();
    };
}
```

---

## Synergy Data Source

Query PlacementGrid for nearby structures within specific radii:
- **Lumbermill:** Adjacent cells (2m range) grant +8% DPS (Lv 2+)
- **Watchtower:** 12m radius grant +15% Range (all levels)
- **Armory:** Adjacent cells grant +10% HP to walls (Lv 1+)

Store synergy rules in a ScriptableObject or static data (see WO-354 for full synergy catalog).

---

## Testing Checklist

- [ ] Tap a card → panel opens with correct entry
- [ ] Tap another card → panel updates to new entry
- [ ] "Place" button arms entry + hides panel
- [ ] "Cancel" dismisses panel, no armed state change
- [ ] Panel scrolls if content overflows (landscape)
- [ ] Safe area respected on notched devices
- [ ] Zero allocations during Show/Hide cycles (profile with IL2CPP)
- [ ] Works in WebGL build (no Resources.Load, no scene mesh refs)
- [ ] Nearby structure detection updates in real-time (if ghost moves)
- [ ] All text visible at minimum screen size (360px mobile)

---

## What NOT to Touch

- PlacementGrid cell size or placement logic
- GhostPreview color tinting (red/green validation)
- BuildSelectionUI (tap-to-edit, move/sell/upgrade — handled by WO-108 P2)
- CatalogEntry stat fields (use as-is)

---

## Dependencies

- **Depends on:** WO-108 (BuildModeController, CatalogRegistry, PlacementGrid, GhostPreview)
- **Unblocks:** WO-354 (synergy system), WO-356 (placement validation)
- **Parallel:** WO-353 (palette filters), WO-355 (portrait layout)

---

## Reference Mockups

See conversation mockups for landscape and portrait layouts. Panel is left-side on landscape, bottom-sheet modal on portrait.

---

## Acceptance Sign-Off

- [ ] Brace balance check passed (CLAUDE.md §1)
- [ ] No new Reflection usage introduced
- [ ] All cross-module calls use `?.` (null-conditional)
- [ ] BuildModeChanged event respected (info panel hidden during exit)
- [ ] CLI verified in WebGL build
