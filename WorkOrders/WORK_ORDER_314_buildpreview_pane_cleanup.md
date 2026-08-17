<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK_ORDER_314 — BuildPreviewModal preview pane cleanup (isolate + make functional)

**Status: READY TO IMPLEMENT**
**Branch:** feat/tower-core-loop · **Lane:** 11 (Build Mode / Player Base) · **Origin:** owner playtest 2026-06-06 (screenshot)
**Reconcile with:** `BuildPreviewModal.cs`, `BuildModeController`, `VisualFactory`; precursor to **WO-282** (premium rotation)

## Problem
The build preview pane renders **into the world** instead of inside the modal: a large grey panel with a
white inner square floats over the village (the neutral preview plane + RenderTexture quad/orthographic cam
are not isolated). The logic is present but **non-functional** — preview isolation is broken.

**Additional reported symptoms (owner playtest 2026-06-06 — same system, same file):**
- ⚠ **NullReferenceException spam** when the Build tab/preview opens (a wall of "Object reference not set to
  an instance of an object") — a null deref in the preview build path. Crash-level — prioritize.
- **Modal stays open after the Build tab is closed** — the preview panel/Canvas + plane persist in the world
  after the player closes the Build tab; teardown only fires on Confirm/Cancel, not on tab close.

## Likely cause / where to look (`Assets/_Modules/Village/BuildMode/BuildPreviewModal.cs`)
- Preview root (neutral plane + 2–3 lights + preview instance + ortho cam) is on the **default layer at/near
  world origin**, visible to the main camera → it shows in-scene. Needs a **dedicated preview layer**, with
  the **preview camera culling mask = that layer only** and the **main camera mask excluding it**.
- Park the preview root far off-screen (e.g. far -Y/offset) so it can't overlap the playfield.
- Ensure the RawImage in the modal Canvas is the thing showing the RenderTexture (256px), and the Canvas is
  Screen-Space Overlay (not rendering the world).
- Confirm teardown: `RenderTexture` + previewRoot + preview cam `DestroyImmediate` on close (no leak/persist).

## Goal
The preview shows **only inside the modal** as a clean isolated 3D thumbnail on a neutral plane with good
lighting; nothing leaks into the world; open/close is clean with no leftover objects.

## Acceptance criteria
- [ ] Arming/placing a structure opens the modal with the model shown **inside the pane only** — no grey/white panel in the world.
- [ ] Preview is isolated (dedicated layer; preview cam sees only the preview; main cam never renders it).
- [ ] Lighting/neutral plane read cleanly; model is centered and framed.
- [ ] Closing the modal destroys the RT + preview root + cam (no leak, no residual objects next open).
- [ ] +/−90 buttons + drag-rotate still update the live preview (functional), feeding `onConfirm` yaw.
- [ ] **No NullReferenceException** when opening the Build tab / preview (null-guard the preview build path; identify the null member).
- [ ] **Closing the Build tab** (not just Confirm/Cancel) tears down the modal + preview root immediately — nothing persists in the world.
- [ ] Brace check; CompileGate `COMPILE_GATE_OK`; Windows build SUCCESS; verify in a play session.

## Root cause (triage 2026-06-06)
**Confidence: Confirmed (with a correction).** The "renders into the world" symptom is **already fixed in
current source** — do not re-do it:
- `BuildPreviewModal.SetupPreview3D` parks the whole preview rig at `y = -5000` (`Assets/_Modules/Village/BuildMode/BuildPreviewModal.cs:189`),
  puts it all on `PREVIEW_LAYER = 31` (`:48`, `:262`), sets the preview cam `cullingMask = 1<<31` (`:263`) and
  masks both lights to layer 31 (`:264-265`), with a Screen-Space-Overlay Canvas (`:73`) showing the RT via
  RawImage. So the grey panel/white square in the world should NOT recur in this build; verify in play first.

**Remaining valid issues:**
1. **Teardown gap (Confirmed).** Cleanup only runs on Confirm/Cancel/OnDestroy (`:376-398`). There is no
   "close on Build-tab-close" path → if the player closes the Build tab without Confirm/Cancel, the modal GO +
   RT persist. Fix: have `BuildModeController` destroy the live modal when the tab/build mode closes.
2. **NRE-on-open (Hypothesis).** The modal's own `Update` is guarded (`:278`). The most plausible open-path
   throw is inside `VisualFactory.Skin(...)` called from `SetupPreview3D` (`:231`) when `_entry.visualPrefabPath`
   resolves to a missing/!-loadable prefab. This is the ONE build-path NRE worth re-checking under WO-328 —
   capture the stack on Build-tab open. Null-guard the `_entry`/`visualPrefabPath` path.

## Do NOT touch
- No `.unity` edits. Don't fork BuildPreviewModal/BuildModeController — fix in place. WO-282 (per-prefab yaw
  persistence) builds on this; keep the existing `PlacedStructureData.yawOffset` path intact.
