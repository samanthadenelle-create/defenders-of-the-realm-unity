<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WO-355: Portrait/Vertical Layout Responsiveness

**Status:** CLOSED — DEPRECATED, audit-verified obsolete (2026-08-21 backlog audit).
**Estimated Effort:** P1 (2–3 days)  
**Priority:** High (mobile core UX)  
**Lane:** HUD/UI (parallel to WO-352–354)

---

## Overview

Reflow Build Mode UI for mobile portrait orientation. When `Screen.height > Screen.width`, switch from 3-column landscape (info | game | palette) to single-column vertical stacking: status bar → large game viewport → minimal palette (emoji grid). Ensures 44×44px touch targets and full viewport height for placement editing.

**Why:** Mobile devices are held in portrait 90% of the time. Current UI optimizes for landscape (computer monitor). Portrait requires entire rethink of space allocation.

---

## Acceptance Criteria

- [ ] Landscape mode (≥600px width): 3-column grid (info | game | palette)
- [ ] Portrait mode (<600px width): Single-column vertical stack
- [ ] Game viewport becomes primary (360px+ height on portrait)
- [ ] Info panel becomes modal/bottom-sheet or "Info" button in portrait
- [ ] Palette cards compact to 2×2 grid of emoji cards (44×44px touch targets)
- [ ] All text within safe area (WCAG safe-area-inset-*)
- [ ] Filter tabs remain horizontal, can scroll if needed
- [ ] Armed card shows one-liner summary, not full panel
- [ ] Responsive without layout shift (debounce Screen.orientation checks)
- [ ] Works on 380px mobile, 600px tablet, 1920px desktop

---

## Files to Modify

### Existing Files
- `Assets/_Modules/Village/BuildMode/BuildModeController.cs` — Orientation detection, layout switching
- `Assets/_Modules/Village/BuildMode/BuildPaletteUI.cs` — Conditional styling (landscape grid vs portrait grid)
- `Assets/_Modules/Village/BuildMode/BuildStructureInfoPanel.cs` (WO-352) — Modal vs side-panel layout
- `Assets/_Modules/Village/BuildMode/GhostPreview.cs` — Update feedback positioning for portrait
- `Assets/_Modules/Village/BuildMode/BuildSelectionUI.cs` — Modal instead of top-center on portrait

### No Changes Required
- `PlacementGrid`, `StructureFactory`, gameplay logic

---

## Design Spec

### Landscape (≥600px)
```
┌─────────────────────────────────────────┐
│ [Info Panel]  [Game Viewport]  [Palette]│
│ (280px)       (400px)          (220px)  │
│               + Controls       + Buttons│
└─────────────────────────────────────────┐
```
All panels visible simultaneously.

### Portrait (<600px)
```
┌──────────────────────┐
│ Status Bar (40px)    │ ← Crystal balance + filter tabs
├──────────────────────┤
│                      │
│  Game Viewport       │ ← 360px+ (flexible)
│  (large, tappable)   │
│                      │
├──────────────────────┤
│ Feedback (24px)      │ ← Validation + synergies (one line)
│ Rotate | Cancel      │ ← 44px buttons
├──────────────────────┤
│ Armed Card (60px)    │ ← Name + cost + [Info] button
├──────────────────────┤
│ Palette Grid         │ ← 2×2 emoji cards
│ 🏭 Watchtower        │   (4 quick-access structures)
│ 👁️ Lumbermill  🧱 Wall│
└──────────────────────┘
```

### Safe Area (Mobile)
Use `env(safe-area-inset-*)` CSS to prevent overlap with notches:
```css
.build-mode-root {
    padding-top: env(safe-area-inset-top);
    padding-left: env(safe-area-inset-left);
    padding-right: env(safe-area-inset-right);
    padding-bottom: env(safe-area-inset-bottom);
}
```

All interactive elements (buttons, cards) within inner 90% of screen. No text in top/bottom 20px on notched devices.

### Responsive Breakpoints

| Breakpoint | Layout | Card Size | Columns |
|-----------|--------|-----------|---------|
| <440px | Portrait | Compact | 2×2 grid |
| 440–600px | Portrait | Compact | 2×2 grid |
| 600–800px | Landscape (tablet) | Medium | 1×3 list or 3 column grid |
| >800px | Landscape (desktop) | Large | 3-column layout |

---

## Implementation Notes

### Orientation Detection (BuildModeController.cs)

```csharp
private float _lastScreenWidth = -1;
private LayoutMode _currentLayout;

private enum LayoutMode { Landscape, Portrait }

private void Update()
{
    // Debounce screen orientation checks (avoid thrashing)
    if (Mathf.Abs(Screen.width - _lastScreenWidth) > 20)
    {
        _lastScreenWidth = Screen.width;
        UpdateLayout();
    }
}

private void UpdateLayout()
{
    bool isPortrait = Screen.height > Screen.width;
    LayoutMode newLayout = isPortrait ? LayoutMode.Portrait : LayoutMode.Landscape;
    
    if (newLayout == _currentLayout) return;
    
    _currentLayout = newLayout;
    
    if (isPortrait)
    {
        _palette.SetLayout(BuildPaletteUI.LayoutMode.Portrait);
        _infoPanel?.SetLayout(BuildStructureInfoPanel.LayoutMode.Modal);
        _selectionUi?.SetLayout(BuildSelectionUI.LayoutMode.Modal);
    }
    else
    {
        _palette.SetLayout(BuildPaletteUI.LayoutMode.Landscape);
        _infoPanel?.SetLayout(BuildStructureInfoPanel.LayoutMode.SidePanel);
        _selectionUi?.SetLayout(BuildSelectionUI.LayoutMode.TopBar);
    }
}
```

### BuildPaletteUI Portrait Layout

```csharp
public void SetLayout(LayoutMode mode)
{
    if (mode == LayoutMode.Portrait)
    {
        // Compact palette: 2×2 grid of emoji cards
        _strip.style.flexDirection = FlexDirection.Row;
        _strip.style.flexWrap = Wrap.Wrap;
        
        // Adjust card size
        var cards = _strip.Children().OfType<Button>();
        foreach (var card in cards)
        {
            card.style.width = 80;  // Compact width
            card.style.height = 60;
            card.style.fontSize = 10;  // Smaller text
        }
    }
    else
    {
        // Landscape palette: horizontal scrollable list
        _strip.style.flexDirection = FlexDirection.Row;
        _strip.style.flexWrap = Wrap.NoWrap;
        
        foreach (var card in cards)
        {
            card.style.width = 116;
            card.style.height = 108;
            card.style.fontSize = 12;
        }
    }
}
```

### BuildStructureInfoPanel Portrait Modal

```csharp
public void SetLayout(LayoutMode mode)
{
    if (mode == LayoutMode.Modal)
    {
        // Bottom-sheet modal: position: absolute, bottom: 0, width: 100%
        _root.style.position = Position.Absolute;
        _root.style.bottom = 0;
        _root.style.left = 0;
        _root.style.right = 0;
        _root.style.width = Length.Percent(100);
        _root.style.maxHeight = Length.Percent(60);
        _root.style.overflow = Overflow.Auto;
    }
    else
    {
        // Side panel: left: 0, top: 0
        _root.style.position = Position.Absolute;
        _root.style.left = 0;
        _root.style.top = 0;
        _root.style.width = 280;
        _root.style.maxHeight = Length.Percent(80);
    }
}
```

### Safe Area Handling

```csharp
private void ApplySafeArea()
{
    var safeArea = Screen.safeArea;
    float safeLeft = safeArea.xMin / Screen.width;
    float safeRight = 1 - (safeArea.xMax / Screen.width);
    float safeTop = (Screen.height - safeArea.yMax) / Screen.height;
    float safeBottom = safeArea.yMin / Screen.height;
    
    // Apply padding to root container
    _root.style.paddingLeft = new StyleLength(new Length(safeLeft * 100, LengthUnit.Percent));
    _root.style.paddingRight = new StyleLength(new Length(safeRight * 100, LengthUnit.Percent));
    _root.style.paddingTop = new StyleLength(new Length(safeTop * 100, LengthUnit.Percent));
    _root.style.paddingBottom = new StyleLength(new Length(safeBottom * 100, LengthUnit.Percent));
}
```

---

## Testing Checklist

- [ ] Rotate device → layout reflows correctly (no glitches)
- [ ] Landscape: 3 columns visible (info | game | palette)
- [ ] Portrait: Single column, game viewport large (360px+)
- [ ] Portrait: Palette shows 2×2 emoji grid
- [ ] Portrait: Filter tabs scroll horizontally if needed
- [ ] Portrait: Info panel is modal/bottom-sheet, swipeable
- [ ] All buttons 44×44px minimum on mobile
- [ ] Text never overlaps notch (safe area respected)
- [ ] No text cut off at screen edges
- [ ] Debounce prevents layout thrashing during rotation
- [ ] Responsive at 380px, 600px, 1920px widths
- [ ] Works on iPhone notch + Android rounded corners

---

## What NOT to Touch

- Game viewport scaling (let it flex)
- Ghost preview (same behavior on any layout)
- Placement grid (unchanged)

---

## Dependencies

- **Depends on:** WO-352 (info panel), WO-353 (filters), WO-354 (tier display)
- **Unblocks:** WO-357 (touch gestures)
- **Parallel:** All HUD work

---

## Acceptance Sign-Off

- [ ] Brace balance check passed
- [ ] No layout shift on rotation
- [ ] Safe area respected on notched devices
- [ ] All touch targets ≥44×44px
- [ ] Works in WebGL on mobile (test with Android device or simulator)

> **AUDIT 2026-08-21 (agent fleet, read-only):** DEPRECATED. Evidence: `ProjectSettings.asset:63-66 landscape-locked` — portrait reflow moot. Status was flipped from READY by the AUDIT, not by an implementation pass. The body below is left intact; if this call is wrong, the evidence cited here is what to challenge.
