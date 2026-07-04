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
