<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-07-04
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-07-04) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

> ⚠ **NUMBER COLLISION — this document does not own WO-433; `WORK_ORDER_433_shop_blink_cohesion.md` does.**
> Referred to hereafter as **WO-433-C (victory screen width)**.
> Flagged by the 2026-08-16 Sunday board-grooming pass (`python tools/board_build.py` → `DUPLICATE_WO_NUMBERS`);
> ownership decided by **first-on-disk** (`git log --follow --diff-filter=A`): the winner's file was created first.
> Banner only — nothing was renumbered or deleted.
> ⚠ **Work HAS shipped under this number** — commit messages and/or a `.RESULT.md` cite WO-433 for THIS document. It is deliberately **not renumbered**; a renumber would orphan those references. Use the alias above when you need to name it unambiguously.

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
