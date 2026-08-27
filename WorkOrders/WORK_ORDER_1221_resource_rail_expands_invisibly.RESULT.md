# WO-1221 RESULT — resource rail expands invisibly

**Status:** IMPLEMENTED — not closed (owner felt-verifies). Did not commit. Did not run Unity batchmode.

## Proving cause (captured, then the bounce)

Device capture `tmp/resources-expanded-105803.png` (2670x1200, inside the expand window) vs `tmp/resources-tap-105648.png`: both show only the gold chip `1034`. Log:

```
[Flow:HudKit] resource chips tap-expanded (6s window)
[Flow:HudKit] resource panel expanded (opener live=True)
```

§12 class: **built-but-invisible**. Not data-empty (`opener live=True`), not threw-and-skipped (no Guard/Fail).

**Root cause that survived the 2026-08-26 "SetActive" pass:** the Wood/Iron/Stone/Crystals pixels lived on a **second occupancy widget** (`resourceChips` wrapping `_resDock`) that `hud-areas.json` **never occupies**. `Register()` deactivates every widget; occupancy is the only thing that turns one on (`docs/MASTER_CATALOG/hud.md`). LateTick SetActive'd that WRAPPER (a full-ActionRail empty dock) and even `_resExpandedRow.SetActive(true)` could not make those pixels children of the gold chip the player actually sees.

Two follow-on failures on the same seam:

1. **ApplyPosture kills unoccupied widgets.** `HudKitController` is `AddComponent`'d *before* `PostureEvaluator` on the same GameObject, so `Update` (and the probe) runs first; `PostureEvaluator.Update` can then deactivate `resourceChips` before render. Probe reports painted; screen stays gold-only.
2. **`UiSurfaceProbe.MeasureRect` treats `activeInHierarchy=false` as a named skip** (same bucket as batchmode). The 08-26 poll could not Fail the captured class; INACTIVE looked like "could not measure".

The 08-26 source-lint (`_resExpandedRow.SetActive` appears inside SetResourcePanelOpen + `UiSurfaceProbe` string exists) is the hollow "handler ran" token the WO forbids: it was green on a tree that still painted nothing.

## Fix

- Four chips are **children of the gold chip** (`resourceChipsCollapsed`, occupancy-live). They hang BELOW it (owner mockup). Same width as gold. Silhouette identity via CurrencyChip icon; word tag only when the icon is missing (never colour alone).
- Tap **calls `SetResourcePanelOpen` directly** (toggle; no 6s timer). That method is the one owner of `_resExpandedRow.SetActive`.
- LateTick only enforces the opener-live gate. It does **not** SetActive a second occupancy widget.
- `TickResourceExpandVerify` still polls `UiSurfaceProbe.MeasureRect` after layout settles (max 8 frames) and now **FAILS on INACTIVE** instead of skipping it. Verdict is `VERIFIED PAINTED — N/N rows … rect=… opacity=… coveredBy=…`, never `opener live=True`.
- Gold chip stays the tap target; `ClampMinTouch` keeps it >= MinTouchPx 112. Expanded rows are display-only (4×112 cannot fit under ActionRail top 0.42 on 2670x1200).

## Regression (would FAIL today's pre-fix tree)

`HudUiRegression` check 7 now fails if:

- `SetResourcePanelOpen` does not `_resExpandedRow.SetActive`
- verify is missing `UiSurfaceProbe` / `VERIFIED PAINTED`
- hollow `resource panel expanded (opener live=` returns
- `Register("resourceChips")` is back (split-widget dock)
- expanded stack is not parented to `tapGo` (the gold chip)
- tap does not call `SetResourcePanelOpen(!_resChipsExpanded)`
- INACTIVE is not a Fail
- occupancy json drops `resourceChipsCollapsed`
- hanging-below-gold stack at 2670x1200 is zero-size or offscreen

## Files

- `Assets/_Modules/HUD/Kit/HudKitController.cs`
- `Assets/Editor/Regression/HudUiRegression.cs`
- `docs/reference/HOLLOW_ASSERTIONS_REGISTRY.md`
- `WorkOrders/WORK_ORDER_1221_resource_rail_expands_invisibly.RESULT.md`

WO **Status** left as READY TO IMPLEMENT (do not change). Owner felt-verifies + CLOSES.

## Trace the owner should see after tap

```
[Flow:HudKit] resource panel expand REQUESTED (toggle=ON, child of gold chip, ...) — NOT yet a claim that anything painted
[Flow:HudKit] resource panel expand VERIFIED PAINTED — 4/4 rows measured on screen, panel kind=UGui rect=WxHpx @(x,y) viewport=... opacity=... coveredBy=<none>
```

A gold-only screen after tap must now emit `resource panel expand INACTIVE` or `SURFACE_ZERO_SIZE` / `ROWS_MISSING`, not a success line.
