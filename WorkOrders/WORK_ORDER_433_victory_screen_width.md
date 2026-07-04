# WO-433 — P2 UI: Victory screen too wide — narrow panel + row style cleanup

**Status:** READY TO IMPLEMENT  
**Priority:** P2  
**Lane:** 4 UI/HUD  
**Minted:** 2026-07-03

---

## What

The Victory/EndState panel spans 84% of screen width (`anchorMin.x=0.08, anchorMax.x=0.92`).
It should be ~56% wide, centered — matching the compact Obsidian modal style used elsewhere.

## Current implementation

**File:** `Assets/_Modules/Village/UI/EndState/EndStateView.cs`

```csharp
// line 89–93
float half = PanelHalfHeight(vm);
var modal = ElarionUiKit.BuildObsidianModal("EndState", vm.Title,
    new Vector2(0.08f, 0.53f - half), new Vector2(0.92f, 0.53f + half),
    onClose: null, frameName: RpgUiCatalog.FrameCore, medallionIcon: "crest");
```

Width = 0.92 - 0.08 = **84% of screen**.

## Requested changes

### 1 — Narrow the panel
Change anchors to center-56%:

```csharp
new Vector2(0.22f, 0.53f - half), new Vector2(0.78f, 0.53f + half),
```

Width = 0.78 - 0.22 = **56%**. Vertically unchanged.

### 2 — Reward row height cap
`PanelHalfHeight` currently clamps to max 0.33 (66% screen height).
With a narrower panel the rows may feel tight — increase max slightly:

```csharp
return Mathf.Clamp(0.055f + units * 0.021f, 0.12f, 0.36f);
```

This gives a bit more breathing room at max content (5+ spoil rows).

## Files to touch
- `Assets/_Modules/Village/UI/EndState/EndStateView.cs` — anchor X values (line ~91) + clamp max (line ~121)

## Do NOT touch
- `EndStateVM.cs`, `ElarionUiKit.cs`, any scene files

## Acceptance criteria
- [ ] Victory panel visually occupies ~56% of screen width, centered
- [ ] All spoil rows (Experience, Wisdom, resources, gear) still visible and not clipped
- [ ] Continue button still centered within the footer zone
- [ ] Headless AutoPilot smoke run passes (no null refs)
