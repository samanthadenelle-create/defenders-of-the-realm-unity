# WORK ORDER 1259 — Resource-panel expand buried under CombatTextLayer

**Status:** FIXED — CODE + FULL REGRESSION PASS; DEVICE UI_CAPTURE OWED
**Minted:** 2026-08-28 (CLI, F8 device triage seq 3623)
**Silo:** HUD
**Evidence (captured, not theorized):** device `SM02G4061955851`, scene `Main_Castle_Overworld`,
2026-08-27T18:12Z — `UiSurfaceProbe` line:
`[Flow:HudKit] resource panel expand: SURFACE_BEHIND — fully covered by 'CombatTextLayer' at
sortingOrder 30500 > 4000 with opacity 1.00. The panel draws, then something opaque draws over it.
(kind=UGui rect=522x242px @(2135,115) viewport=2670x1200 opacity=1.00 sortingOrder=4000)`
Stack: `HudKitController.TickResourceExpandVerify()`.
Capture: `logs/f8-inbox/capture-device-20260828-131837-seq3623.md`.

## RCA (from the data)
The expanded resource panel renders at sortingOrder **4000**; `CombatTextLayer` sits at **30500**
with opacity **1.00** and fully covers the panel's rect (top-right, 522x242 px). This fired in the
TOWN scene — the combat text layer is present and opaque outside combat. Two candidate roots, to be
settled by instrumentation not preference: (a) CombatTextLayer's canvas/background is opaque when it
should be transparent-when-idle, or (b) the resource panel's expand surface is authored at a sort
order far below the established HUD overlay band.

## Acceptance
1. In town, expanding the resource panel shows it fully (UiSurfaceProbe reports no SURFACE_BEHIND).
2. Combat floating text still renders above world/HUD during battle (do not just sink the layer).
3. Regression: extend the existing UI-surface probe coverage so this exact overlay pair is asserted.
4. `UI_CAPTURE_OK` with the expanded panel visible in the PNG.

## Do not touch
Sorting orders of unrelated panels; the probe itself (it did its job).

## Implementation + validation — 2026-08-28

**Settled RCA:** candidate (a). `CombatTextLayer` is a full-screen overlay canvas at sorting order
30500, but it has no background graphic: only six pooled TMP stamp children. `UiSurfaceProbe`
correctly consumed the root canvas's explicit opacity, which remained 1.00 even when every stamp was
inactive. The empty decorative layer therefore falsely presented itself as an opaque coverer over
the resource panel.

The layer now owns a non-interactive `CanvasGroup`: alpha 0 while the pool is idle, alpha 1 whenever
a stamp is pushed/refreshed, and back to 0 after the final live stamp expires. Its sorting order stays
30500, so real combat text remains above the HUD; no unrelated panel or probe logic changed.

Regression added to `UiSurfaceProbeRegression`: construct the actual layer, assert the idle alpha is
0, raycasts are disabled, its established sorting order is intact, and pushing a real stamp changes
alpha to 1.

- `COMPILE_GATE_OK` — `Builds/wo1259-compile.log`
- `REGRESSION_OK` — 315/315 registered suites green, `Builds/wo1259-regression-retry.log`
- First regression invocation was not counted: it exposed the edit-mode harness not invoking the
  component's private `Awake`; the harness was corrected to drive the real lifecycle construction.
- Remaining acceptance item: played device capture with the resource panel expanded. No
  `UI_CAPTURE_OK` or felt-device claim is made by this batch regression.
