# WO-380: Gear Icon — Move Out of Minimap Overlap

**Status:** DONE

> **DONE - verified in HEAD 2026-08-14 (phantom sweep).** The work is present at VillageHudController.cs:191-192 (minimap cut).
> Status had read READY because the landing commit did not flip this line in the same commit
> (CLAUDE.md §2), so the DERIVED board (BOARD.html) kept re-serving finished work.
> _Prior status line, preserved: Status: READY TO IMPLEMENT_

**Estimated Effort:** P0 (0.25 days — positioning only)  
**Priority:** HIGH (blocks access to settings)  
**Lane:** 4 UI/HUD

---

## Issue

**Gear icon (settings) is covered by minimap** — player cannot click it.

---

## Fix Options

### Option A: Move Gear Icon
- Relocate gear icon to different corner (top-left, bottom-left, etc.)
- Keep minimap in current position
- Simple repositioning

### Option B: Raise Gear Icon
- Keep gear icon position
- Increase sort order/z-position above minimap
- Minimap stays behind

### Option C: Minimize Minimap
- Shrink minimap slightly
- Reposition to avoid overlap
- Both stay visible

---

## Recommended: Option B (Raise Gear Icon)

**In Canvas/Hierarchy:**
```
Canvas (HUD)
├── Minimap (Canvas) 
│   └── Sort Order: 50
└── GearIcon (Button)
    └── Sort Order: 100  ← Higher than minimap
```

**Or in code:**
```csharp
var gearCanvas = gearIcon.GetComponent<Canvas>();
gearCanvas.sortingOrder = 100;  // Above minimap
```

---

## Testing

- [ ] Gear icon visible and clickable
- [ ] Not covered by minimap
- [ ] Minimap still visible behind icon
- [ ] Both functional

---

## Acceptance

- [ ] Gear icon accessible
- [ ] No overlap with minimap
- [ ] Settings menu opens on click
