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

## Do NOT touch
- No `.unity` edits. Don't fork BuildPreviewModal/BuildModeController — fix in place. WO-282 (per-prefab yaw
  persistence) builds on this; keep the existing `PlacedStructureData.yawOffset` path intact.
