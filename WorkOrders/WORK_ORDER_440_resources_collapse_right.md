> ⚠ **UNRESOLVED NUMBER COLLISION — WO-440 is claimed by more than one file and OWNERSHIP IS NOT DECIDED.**
> Co-claimants: `WORK_ORDER_440_atb_wiring.md` (06-17, first-on-disk), `WORK_ORDER_440_resources_collapse_right.md` (07-04)
> **This is one of a four-number group (WO-437 / 438 / 439 / 440) that collided the same way.** The June
> files are **first-on-disk**; the 2026-07-04 files are the ones **git history says shipped** — commit
> `0b0e0915c` reads *"UI-100% wave 1 — shared-kit parchment fix, WO-437/438/439/440, per-screen match"*,
> which names the 07-04 UI batch, and `aa931577b` separately records *"WO-437/438 landed"*. First-on-disk
> and referenced-by-commit point at DIFFERENT files, so the project rule resolves to neither.
> Flagged (not resolved) by the 2026-08-16 Sunday board-grooming pass — needs an **owner ruling**, ideally
> one ruling for all four at once. Nothing renumbered or deleted. Cite by FILENAME, never by bare number.

# WO-440 — P2 UI: Resources panel — collapsed to right edge by default, tap to expand

**Status:** READY TO IMPLEMENT  
**Priority:** P2  
**Lane:** 4 UI/HUD  
**Minted:** 2026-07-03

---

## What

Per owner direction: the Resources panel (currently always-visible, top-right) should
be **collapsed to the right edge** by default. Tapping it expands the full panel;
tapping again collapses it. "Not needed to be seen unless using something that
specifically calls out for them."

## Current implementation

**File:** `Assets/_Modules/HUD/Kit/HudKitController.cs` — `BuildResourceChips()`
Currently builds a always-visible `GameObject("ResourceChips")` with an Obsidian frame,
"Resources" header, and 5 `CurrencyChip` rows pinned to the top-right of the screen.

## Design spec

**Collapsed state:**
- A narrow tab (~40×100px) pinned to the right edge (anchorMin.x = 1, anchorMax.x = 1),
  showing a small gold/coin icon (use `Gold_Currency.png` from Icons_Obsidian) or a
  "›" indicator.
- The full Resources panel is off-screen to the right.

**Expanded state:**
- Tab shows "‹" (pointing left = close).
- Panel animates in from the right (~0.2s ease-out lerp on anchorMin.x).
- Full panel shows exactly as WO-431 specced: Obsidian frame, "Resources" header,
  5 rows (Gold, Wood, Iron, Food, Crystal) with icons and dynamic column width.
- A second tap on the tab collapses the panel back off-screen.

**Auto-expand trigger (future, stub now):**
When a system awards resources (wave victory, pickup), auto-expand the panel for 3s
then auto-collapse. Wire via `ResourcePanelHandle.ShowBriefly(float seconds)` — stub
the method now, logic in a future WO.

## Implementation notes

- Build in code, no scene hand-edits.
- Use `CollapseButton.prefab` / `ExpandButton.prefab` sprites for tab icon.
- Animate: `StartCoroutine` lerp on `anchorMin.x` from 1.0 → 0.78 over 0.2s.
- Return `ResourcePanelHandle { Image[] FillBars, TMP_Text[] ValueLabels, Action ShowBriefly }`
  so `HudKitController.SetResources()` can update values whether panel is open or closed.

## Files to touch
- `Assets/_Modules/HUD/Kit/HudKitController.cs` — replace current `BuildResourceChips()`
  with new collapsed-by-default builder; wire `SetResources()` to the handle

## Do NOT touch
- `ElarionUiKit.CurrencyChip()` (reuse as-is), `VillageHudController.cs`, any scene files

## Acceptance criteria
- [ ] Resources panel collapsed to right edge on HUD load (not visible by default)
- [ ] Tap tab → panel slides in from right (~0.2s), shows all 5 resource rows
- [ ] Tap again → panel slides out, tab returns to collapsed state
- [ ] `SetResources()` updates values correctly whether panel is open or closed
- [ ] `ShowBriefly(float)` stub present (no-op is fine for now)
- [ ] Headless smoke run passes
