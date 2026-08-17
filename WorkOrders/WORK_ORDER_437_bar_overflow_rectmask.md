> ⚠ **UNRESOLVED NUMBER COLLISION — WO-437 is claimed by more than one file and OWNERSHIP IS NOT DECIDED.**
> Co-claimants: `WORK_ORDER_437_combat_hud_tech_skin.md` (06-13, first-on-disk), `WORK_ORDER_437_input_state_gate.md` (06-17, marked DONE), `WORK_ORDER_437_bar_overflow_rectmask.md` (07-04)
> **This is one of a four-number group (WO-437 / 438 / 439 / 440) that collided the same way.** The June
> files are **first-on-disk**; the 2026-07-04 files are the ones **git history says shipped** — commit
> `0b0e0915c` reads *"UI-100% wave 1 — shared-kit parchment fix, WO-437/438/439/440, per-screen match"*,
> which names the 07-04 UI batch, and `aa931577b` separately records *"WO-437/438 landed"*. First-on-disk
> and referenced-by-commit point at DIFFERENT files, so the project rule resolves to neither.
> Flagged (not resolved) by the 2026-08-16 Sunday board-grooming pass — needs an **owner ruling**, ideally
> one ruling for all four at once. Nothing renumbered or deleted. Cite by FILENAME, never by bare number.

# WO-437 — P1 Bug: HP/MP bars overflow nameplate bounds — add RectMask2D

**Status:** READY TO IMPLEMENT  
**Priority:** P1  
**Lane:** 4 UI/HUD  
**Minted:** 2026-07-03

---

## Bug

In the live HUD (screenshot 2026-07-03): the HP and MP fill bars in both the Knight
nameplate and the ♥ Elarion bar extend visually past the right edge of their dark
background panel. The bar fill bleeds outside the StatBars container with a small
arrow indicator overhanging the edge.

## Root cause

`BuildPartyNameplate()` (WO-432) builds `HealthFill` and `ManaFill` as children of
`HealthBackground` / `ManaBackground` respectively, but no `RectMask2D` is applied
to the parent container — so fills can overflow their bounds when `fillAmount` drives
the image width near 1.0.

## Fix

In `ElarionUiKit.BuildPartyNameplate()` (or `HudKitController` call site), add
`RectMask2D` to the **StatBars container** so all fill children are clipped to the
panel bounds:

```csharp
var statBars = new GameObject("StatBars", typeof(RectTransform), typeof(GridLayoutGroup),
    typeof(RectMask2D));
```

Also apply `RectMask2D` to each `HealthBackground` and `ManaBackground` individually
so fills clip to their own row bounds:

```csharp
var healthBg = new GameObject("HealthBackground", typeof(RectTransform), typeof(Image),
    typeof(RectMask2D));
var manaBg   = new GameObject("ManaBackground",   typeof(RectTransform), typeof(Image),
    typeof(RectMask2D));
```

**Do NOT** add `RectMask2D` to the root nameplate panel — masking there would clip
the Obsidian frame sprite's border decoration.

## Files to touch
- `Assets/_Modules/Core/UI/ElarionUiKit.cs` (or `ElarionUiKitNameplate.cs` if WO-432
  added a sibling partial) — add `typeof(RectMask2D)` to StatBars + bar background
  GameObject constructors

## Do NOT touch
- `HudKitController.cs`, any scene files

## Acceptance criteria
- [ ] HP/MP bars fully contained within the nameplate panel bounds at all fill values
- [ ] No bar fill visible outside the dark background rect
- [ ] Obsidian nameplate frame art not clipped
- [ ] Headless smoke run passes
