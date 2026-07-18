# WORK ORDER 746 — RESULT (Build-Mode felt-fix pass BM-1/2/3)

**Status:** IMPLEMENTED + gate-green + code-verified. **Awaiting owner felt-verify to CLOSE** (each BM ticket).
**Commit:** `9b0f27e0` (WO-746 + UICaptureMode), on `wip/village2-and-f8-tickets` (pushed).
**Gate:** `COMPILE_GATE_OK`; `DataRegression` — WO-746 added **no new red** (the 8-red baseline was unaffected;
a separate pass then cleared HUDUI + fountan L2/L3, 8->6). Brace-balanced, zero NUL.

---

## BM-1 — After PLACE, return to the carousel (was: intent bar stayed armed)
- `BuildModeController.Place()` tail now runs the single return-to-carousel point on the success path only
  (after charge + BaseLayout append + `StructurePlaced` signal): `CancelArmed(afterPlacement:true)`
  (disarms + `_palette.Expand()`) then `_hud?.SetState(BuildHudState.Browse)`. The redundant `CancelArmed`
  in `UpdateDroppedPlaceLoop` was removed (Place() owns it); the hover-PLACE path now returns too.
- The CoC "stay-armed" default was reversed per owner (2026-07-18).
- **Trace wired (emits on any placement):** `[Flow:BuildHud] state -> Browse (placement committed; intent bar
  hidden, carousel restored)` paired with `[Flow:BuildHud] placed -> returned to carousel`.

## BM-2 — Placed singleton renders non-armable "Built" (was: still buyable)
- Hoisted the WO-707 singleton check to `internal static BuildModeController.IsSingletonBuilt(CatalogEntry)`
  (quiet, poll-safe). `BuildPaletteUI.BuildCard` renders a built singleton as desaturated art + a **"Built"
  chip** (word + shape, never color-alone) replacing the cost label; `armed` forced false; tap shows the
  existing Singleton toast instead of arming. Re-renders on placement via BM-1's `Expand()`.
- Enforcement semantics (WO-707 arm/commit) unchanged. `pet-house` (Echo Hollow) is `singleton:true`;
  lumberyard/foundry/silo are containers -> unaffected.
- **Trace wired:** `[Flow:Build] palette: tapped BUILT singleton card '<id>' — arm refused, Singleton toast.`

## BM-3 — Tutorial spotlight anchors the right card + follows liveness (was: on Forge / orphaned)
- Every `BuildPaletteUI.BuildCard` Render() registers the card under a stable id `build.card.<entryId>`
  (`TutorialHighlightRegistry`), trace `[Flow:Build] card-register id=build.card.<entryId>`.
- `UiSpotlight.TryTargetScreenRect` now returns false when the resolved RectTransform is
  `!activeInHierarchy` (collapsed tray / rebuilt card) -> the glow no longer strands at a stale rect
  (Update sets alpha 0); it re-acquires when the card re-registers active.
- The `founding_stores` step highlight moved `hud.build_button` -> `build.card.lumberyard` (both
  tutorial-steps.json copies); `build.card.lumberyard` added to `TutorialHighlightRegistry.KnownIds`.
- **Entry-id SETTLED by code (no capture needed to decide):** it is `lumberyard`, confirmed three ways —
  the wired completion signal `build.structure_placed:lumberyard`, `structures-catalog.json:1039`
  `"id":"lumberyard"` (Town tab), and `lumbermill` is retired/locked (`BuildCategoryRegistry.cs:200`).
- **Traces wired:** `[Flow:Spotlight] show highlightId=<id>` + on first resolve
  `[Flow:Spotlight] show highlightId=<id> target=<name> rect=(x,y,w,h)`.

---

## §12 capture status
The proving instrumentation is wired (the `[Flow:BuildHud] state -> Browse`, the Singleton-toast line, and
`[Flow:Spotlight] ... target=Card_lumberyard rect=...`). BM-3's only ambiguity (wrong-target vs stale-rect)
was resolved by code, not by the capture, so no decision hangs on the live trace. The literal captured lines
emit during any Build-Mode play; they will be **auto-harvested by the F8 watcher on the owner's felt-verify**
(or on request via a background Windows build + `run-autopilot-fleet.ps1` `AssertFoundingArc` run — the driver
is player-only, no editor-batchmode entry).

## Owner (PO) to close
Felt-verify on device: (BM-1) place any building -> intent bar hidden, palette expanded, no armed card;
(BM-2) place Echo Hollow -> its card shows "Built", not armable, survives reopen/reload; (BM-3) during
"Place your Lumberyard" the spotlight sits on the Lumberyard card only, hides on palette collapse, gone after
placement, no floating glow. Then close the three BM tickets + set Notion WO-746 -> Done.
