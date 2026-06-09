# Build Mode UI/UX Improvements — Implementation Roadmap

**Created:** 2026-06-08  
**Status:** READY FOR CLI  
**Total WOs:** 6 (WO-352 through WO-357)  
**Estimated Total Effort:** 10–14 days  
**Priority:** High (core gameplay loop improvement)

---

## Overview

Six-part overhaul of the Build Mode UI to implement 2026 best practices:

1. **Structure Info Previews** (WO-352) — Show stats before placing
2. **Palette Filters** (WO-353) — Organize structures by category
3. **Upgrade & Synergy System** (WO-354) — Tier display, bonus cascades
4. **Portrait Responsiveness** (WO-355) — Mobile-first single-column layout
5. **Validation & Grid Tools** (WO-356) — Clear feedback, grid visualization, hints
6. **Touch & Accessibility** (WO-357) — Gesture controls, WCAG compliance

All work is parallelizable (see lane assignments below). No blocking dependencies between most WOs.

---

## Key Improvements

### Before
- Tap card → immediate placement ghost (no preview)
- Silent red/green feedback ("Why can't I place here?")
- 10+ cards in one horizontal scroll (tedious search)
- No tier info displayed
- Synergies invisible (players don't know structures buff each other)
- No mobile portrait support
- Keyboard-only input
- No accessibility settings

### After
- Tap card → preview panel (stats, cost, next tier, nearby bonuses)
- Explicit validation messages ("Gate clearance violation", "Overlaps tower")
- Filter tabs: Defenses, Resources, Utility (instant narrowing)
- Tier badge (Lv X/Max) on every card + full upgrade tree
- Synergy preview: "Valid • +8% DPS (Lumbermill) • +15% Range (Watchtower)"
- Portrait layout: 360px viewport + 2×2 emoji palette grid
- Touch gestures: tap-drag pan, two-finger pinch zoom
- Accessibility settings on first launch; WCAG AA compliance

---

## Implementation Order

### Phase 1: Core UX (Days 1–3)
**Lane:** HUD/UI (sequential or parallel with team)

1. **WO-352: Structure Info Preview Panel** (2–3 days)
   - Implements left-side preview or modal
   - Shows stats, cost, footprint, next tier benefits
   - "Place" button arms structure
   - Depends on: WO-108 (BuildModeController, CatalogRegistry)
   - Unblocks: WO-354 (synergy preview integration)

2. **WO-353: Palette Filters** (1–2 days, **parallel**)
   - Add filter tabs above card strip
   - Narrow by: All, Defenses, Resources, Utility
   - Auto-switch to armed entry's tab if filtered out
   - No blocking dependencies

### Phase 2: Gameplay Depth (Days 3–5)
**Lane:** Gameplay/Systems

3. **WO-354: Upgrade & Synergy System** (3–5 days)
   - Define tier costs & max levels in catalog
   - Implement SynergyCalculator for real-time bonus detection
   - Display tier badges, next-tier benefits, active synergies
   - Examples: Lumbermill +8% DPS, Watchtower +15% Range
   - Depends on: WO-352 (preview panel for synergy display)
   - Unblocks: WO-356 (placement feedback messages)

### Phase 3: Platform Support (Days 4–7)
**Lane:** HUD/UI (parallel to Gameplay lane)

4. **WO-355: Portrait Responsiveness** (2–3 days, **parallel** with Phase 2)
   - Reflow UI for portrait (<600px width)
   - Game viewport becomes primary (360px+)
   - Palette shrinks to 2×2 emoji grid + compact armed card
   - Safe area handling (notches, rounded corners)
   - Depends on: WO-352, WO-353 (elements to reflow)
   - Test on: 380px mobile, 600px tablet, 1920px desktop

5. **WO-356: Validation Messages & Grid** (1–2 days, **parallel**)
   - Replace silent red/green with text feedback
   - Grid toggle (G key), auto-hide after 2s
   - Rotation indicator (0°/90°/180°/270°)
   - Camera pan hints (fade after 4s)
   - Depends on: WO-108 (placement grid, GhostPreview)
   - Unblocks: None (independent polish)

### Phase 4: Mobile & Accessibility (Days 7–10)
**Lane:** Gameplay/Input (parallel to Validation work)

6. **WO-357: Touch Gestures & Accessibility** (2–4 days, **parallel**)
   - Tap-drag camera pan, two-finger pinch zoom
   - Accessibility settings prompt (first launch)
   - WCAG AA compliance: focus order, keyboard fallbacks, screen reader
   - Color blind modes, high contrast option
   - Depends on: WO-108 (camera controls), WO-355 (safe area handling)

---

## Parallel Lane Breakdown

**Avoid conflicts by isolating file ownership:**

| Lane | WOs | Files | Team |
|------|-----|-------|------|
| **HUD/UI** | WO-352, WO-353, WO-355 | BuildPaletteUI.cs, BuildStructureInfoPanel.cs, BuildModeController UI | UI Agent |
| **Gameplay/Systems** | WO-354 | SynergyCalculator.cs, CatalogEntry.cs, BuildModeController logic | Gameplay Agent |
| **Input/Accessibility** | WO-356, WO-357 | BuildModeTouchInput.cs, AccessibilitySettings.cs, BuildModeController camera | Input Agent |

**Coordination points:**
- **WO-352 ↔ WO-354:** Info panel needs synergy integration (one pass needed mid-WO-352)
- **WO-355 ↔ WO-357:** Safe area + gesture handling (parallel, no conflicts)
- **All ↔ WO-108:** No changes to BuildModeController; just subscribe to new events

---

## Dependencies & Blocking Graph

```
WO-352 (Info Panel)
├─ WO-354 (Synergy) depends on
│  └─ WO-356 (Validation Messages)
│
WO-353 (Filters) — no dependencies
│
WO-355 (Portrait) depends on
├─ WO-352 (elements to reflow)
├─ WO-353 (filters to reflow)
└─ WO-357 (safe area, gestures)
│
WO-356 (Validation) — no blocking dependencies
│
WO-357 (Touch) depends on
├─ WO-108 (camera control abstraction)
└─ WO-355 (safe area respect)
```

**Critical path:** WO-352 → WO-354 → WO-356 (7–10 days)  
**Parallel path:** WO-353, WO-355, WO-357 (2–4 days each)

---

## Testing Strategy

### Unit Tests
- SynergyCalculator bonus detection (correctness, edge cases)
- BuildModeController orientation detection (reflow on rotate)
- AccessibilitySettings persistence (save/load cycle)

### Integration Tests
- Preview panel → Place → Ghost armed (data flow)
- Filter tab → Card list updates → Armed entry highlights (state consistency)
- Touch pan → Camera moves → Synergies update (real-time feedback)

### QA Checklist
- **Desktop landscape (≥800px):** 3-column layout, all panels visible
- **Tablet landscape (600–800px):** Compact panels, responsive
- **Mobile portrait (<600px):** Single column, viewport large, buttons 44×44px
- **Gesture testing:** Pan, pinch, rotate (iOS/Android physical devices or emulators)
- **Accessibility:** Tab navigation, screen reader, high contrast, color blind modes
- **WebGL build:** No Resources.Load, no scene mesh refs, no flickering
- **Performance:** Zero GC allocation per frame, synergy recalc only on ghost move

### Regression Testing
- Existing placement logic unchanged
- Ghost red/green still validates correctly
- Palette cost gating unaffected
- Tiers/upgrades don't break persistence (WO-131)
- BuildSelectionUI (move/sell/upgrade) still works

---

## Success Criteria (Cross-WO)

- [ ] All 6 WOs implemented and verified in WebGL build
- [ ] Desktop + tablet + portrait layouts all functional
- [ ] Accessibility settings integrated and persistent
- [ ] Synergy bonuses calculated correctly + displayed in real-time
- [ ] Touch gestures work on iOS/Android devices
- [ ] No regressions in existing build mode features
- [ ] Performance: ≥60 FPS on target mobile device
- [ ] Zero GC allocations during placement phase
- [ ] WCAG 2.1 Level AA compliance verified

---

## Migration Notes

### CatalogEntry Changes (Backward Compatible)
```csharp
public int maxLevel = 1;  // Default: not upgradeable (existing behavior)
public ResourceCost[] tierCosts = { };  // Empty: no upgrade costs
public SynergyEffect[] auras = { };  // Empty: no bonuses
```

Existing structures without tier data will have maxLevel = 1 (locked), displaying correctly as "Lv 1/1".

### BuildModeController Interface (No Breaking Changes)
- New events: `OnCardTapped`, `OnPlaceRequested` (replaces immediate `OnEntrySelected`)
- Existing callers (palette buttons) emit new events; controller subscribes
- Ghost/placement logic identical

### Persistence (WO-131 Compatible)
- Tier state stored as `PlacedStructure.currentTier`
- Upgrade costs charged to `GameStateService.State.Resources`
- Synergies recalculated on load (no state needed)

---

## Notion Sync

Create 6 issues in Notion "Work Orders" database:

| WO | Title | Status | Effort | Lane |
|----|-------|--------|--------|------|
| 352 | Structure Info Preview Panel | READY | 2–3d | HUD/UI |
| 353 | Palette Filters & Categories | READY | 1–2d | HUD/UI |
| 354 | Upgrade Tier & Synergy System | READY | 3–5d | Gameplay |
| 355 | Portrait/Vertical Responsiveness | READY | 2–3d | HUD/UI |
| 356 | Placement Validation Messages & Grid | READY | 1–2d | HUD/UI |
| 357 | Mobile Touch Gestures & Accessibility | READY | 2–4d | Input |

---

## Sign-Off

**Design approved by:** Samantha (user)  
**2026 best practices research:** Completed (see conversation)  
**Mockups:** Desktop landscape + portrait, provided  
**Ready for CLI implementation:** YES

---

## Questions for CLI Before Start

1. **Team capacity:** Can we run 2–3 parallel agents (HUD, Gameplay, Input lanes)?
2. **Testing environment:** Mobile device for gesture testing, or emulator sufficient?
3. **Notion sync:** Should WOs be created there before CLI starts, or after?
4. **Persistence (WO-131):** Is tier/cost state shape already defined, or should CLI design it?

---

## Future Enhancements (Post-MVP)

- [ ] Structure rotation preview (3D model rotates as player drags R button)
- [ ] "Undo" stack (place structure, undo placement, restore resources)
- [ ] Placement templates (save favorite layouts, load presets)
- [ ] Multi-select + batch operations (select 3 walls, move together)
- [ ] Build cost simulator (before committing, show total cost of planned structures)
- [ ] Video tutorials in-game (learn gestures + synergies)
