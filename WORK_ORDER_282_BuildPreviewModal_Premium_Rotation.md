# WORK_ORDER_282_BuildPreviewModal_Premium_Rotation.md

**Status: READY TO IMPLEMENT**

**Created:** (current session)
**Branch context:** feat/tower-core-loop

## Goal
Turn the existing Build Preview Modal into a premium, intuitive 3D model viewer that permanently solves rotation/axes pain for player-placed structures (walls, bridges, buildings, towers, gates, etc.) coming from third-party low-poly packs (Quaternius, KayKit via catalog, Polyperfect _M, etc.).

When a player arms any structure and confirms a world placement:
- A clean dedicated preview modal opens.
- The object is shown in isolation on a neutral flat plane with good lighting (already partially present).
- Controls: +/- 90° snap buttons + free drag-to-rotate (yaw) directly on the 3D preview area.
- Player rotates until the model "sits naturally" (correct facing / flat on ground / no weird tilt from import axes).

On confirm:
- The final yaw offset is saved **permanently for that prefab type** (keyed by stable CatalogEntry.id / itemId).
- Future placements of the same type open the preview already pre-rotated to the saved "natural" orientation.
- The chosen value flows through the existing PlacedStructureData.yawOffset path so the placed instance in the world (via StructureFactory + BaseLayoutLoader) automatically appears correctly oriented.

This is a major quality-of-life / "wow" feature for the build UX.

Also: honest evaluation of whether a polished, dep-minimal version of (preview modal + per-prefab yaw correction registry + apply hook) could be extracted and sold as a Unity Asset Store package.

## Background / Review (completed before implementation)
- Reviewed (mandatory nav + targeted reads): PROJECT_INDEX.md, Assets/README.md, Assets/_Modules/README.md, docs/README.md, Claude.md, Assets/_Modules/Village/README.md.
- Core files reviewed in detail:
  - BuildPreviewModal.cs: self-contained code-built Canvas + RT (256px) + orthographic cam + neutral Quad plane + 2-3 lights, title + instr, +/-90 buttons, Confirm/Cancel, drag approximated in Update() via screen rect + PreviewDragHandler helper. Always starts _currentYaw=0. Uses VisualFactory.Skin + CatalogEntry. Passes final yaw to onConfirm.
  - PlacedStructureData.cs (DeNelle.Core.State): yawSteps (0-3) + yawOffset (float additive degrees). Ctor + JSON-friendly.
  - BuildModeController.cs: Arm resets offset=0; on valid place tap creates modal + Show; onConfirm lambda quantizes to steps + stores full yaw as _armedYawOffset, then DoPendingPlace -> Place creates PlacedStructureData(..., yawOffset) -> loader.Spawn + state.BaseLayout append. Ghost uses discrete steps only during aim.
  - BaseLayoutLoader.cs: Spawn computes `rot = Quaternion.Euler(0f, data.yawSteps * 90f + data.yawOffset, 0f)` then StructureFactory.Create(entry, new Pose(pos, rot), ...). Loaded structures get PlacedStructure marker.
  - GhostPreview.cs: discrete yawSteps only for live ghost (consistent with aim loop).
  - Supporting: VisualFactory, CatalogEntry (id + visualPrefabPath + displayName + repo), PlacementGrid, StructureFactory (the single create path).
- Gap identified: no per-type persistence of the "natural yaw" the player discovers in the modal. Every new wall/bridge/etc requires manual re-correction. Imported packs have inconsistent root rotations/pivots/ "forward".
- Existing yawOffset persistence + apply sites already exist — we extend the UX to seed + remember the correction.

## Requirements (verbatim from query + clarifications)
1. When player arms/places any structure (bridge, wall piece, building, etc.):
   - Open a clean modal with a dedicated 3D preview area.
   - Show the object alone on a neutral flat plane with good lighting.
   - Give user easy controls: +/- 90° buttons + free drag rotation on the preview.
   - Allow them to rotate until the object sits naturally (correct yaw/axes).
2. When user confirms:
   - Save the final rotation offset (yaw) permanently for that prefab type.
   - When the object is placed in the world, automatically apply this saved offset so it always appears correctly oriented.
3. Make it feel premium and intuitive (this is a major quality-of-life feature).

Constraints (Claude.md non-negotiable):
- Code-built UI only (no UXML at runtime).
- Village → Core only; use ?. on any CoreServices.
- After **every** .cs edit: run the exact python3 brace-balance check before continuing/reporting.
- NEVER hand-edit .unity (Village etc). Builders only.
- Update module README + living docs when files added/moved.
- One agent at a time on VillageSceneBuilder (not touching it here).
- No new heavy per-frame costs; keep mobile-friendly (RT size reasonable, pooling patterns respected).
- Brace balance 100% on ship; no mismatched braces ever.

## Files
**Create:**
- `Assets/_Modules/Village/BuildMode/RotationCorrectionRegistry.cs` — static registry. Keys by CatalogEntry.id (itemId). Load/Save via PlayerPrefs + JsonUtility (mobile-safe, no extra assemblies, survives restarts). GetYawOffset / SetAndSave. Internal to DeNelle.Village.

**Edit:**
- `Assets/_Modules/Village/BuildMode/BuildPreviewModal.cs` — seed `_currentYaw` from registry on Show (for the type); enlarge RT (e.g. 384); premium UI polish (title with displayName + id, live "Yaw: XX°" readout that updates, "Reset" button, improved touch-friendly buttons + earthy theme, richer instructions that mention "saved for future placements of this type", confirm path calls registry save before invoking callback).
- `Assets/_Modules/Village/BuildMode/BuildModeController.cs` — light touch: ensure onConfirm flow remains clean; optional one-line to seed initial _armedYawOffset from registry for documentation/consistency (ghost still discrete-steps); add comments referencing the registry as the source of "remembered natural orientation".
- `Assets/_Modules/Village/README.md` — update the BuildMode paragraph to mention the new per-type orientation memory + premium viewer.

**Also produce:**
- `WORK_ORDER_282_BuildPreviewModal_Premium_Rotation.RESULT.md` (on completion, with verification steps, brace outputs, test notes, Asset Store evaluation).

**What NOT to touch / change:**
- Any *.unity scene files.
- VillageSceneBuilder* (or any Editor builder partials).
- StructureFactory.cs (reuse the existing Create path; do not fork).
- Assembly definitions or cross-module wiring beyond allowed (registry stays inside Village).
- EconomyService, GameState schema (yawOffset field already exists and is used), PlacedStructureData (no schema change).
- GhostPreview MoveTo signature or core logic (discrete steps for live aim is fine; fine yaw is a "commit-time" concern in the modal).
- Add no UXML, no System.Reflection, no new heavy managers.
- Do not alter existing save/load of BaseLayout (the offset value just becomes better/more consistent).

## Acceptance Criteria (must be demonstrable)
- [ ] Arming a structure type that has never had a correction saved opens the modal with yaw = 0 (or the model's authored rotation).
- [ ] Horizontal drag over the preview RawImage and the +/-90 buttons rotate the 3D model live inside the isolated RT (smooth, no judder, works on mouse + basic touch).
- [ ] Confirming a placement for a type causes RotationCorrectionRegistry to persist the chosen yaw for that itemId (verifiable in PlayerPrefs or via a debug print of the map).
- [ ] Re-arming the same type (same play session or after restart) causes the modal's 3D preview to open with the previously-saved yaw already applied — the object "sits naturally" with no extra player work.
- [ ] Player can still fine-tune further in the modal and re-confirm; the new value becomes the updated default for all future instances of that type.
- [ ] The placed structure in the live scene (and after BaseLayoutLoader round-trip on reload) uses the effective yaw and orients correctly on the grid (no more "sideways wall" or "bridge floating wrong").
- [ ] UI feels premium: panel has breathing room, title shows friendly name, a numeric yaw readout is visible and live, Reset button returns preview to the saved default (or 0), instruction text explicitly says the orientation will be remembered, buttons are large (thumb-friendly), colors match Village earthy/medieval theme.
- [ ] RT size increased (≥320-384) for better viewer experience while remaining mobile-perf friendly.
- [ ] Every .cs file edited passes the exact brace check (python3 -c ... opens==closes) immediately after the edit and before any further work or "done" claim. Output captured in RESULT.
- [ ] No .unity touched, no builder serialization, cross-calls use ?. where applicable, code-built UI.
- [ ] Village README updated.
- [ ] Honest Asset Store package evaluation written (market size, competition, pricing realism, extraction effort, realistic sales estimate).

## Implementation Sketch (for the implementer)
- Registry: simple static class. Serializable wrapper `{ public List<CorrectionEntry> entries; }` + JsonUtility.ToJson / FromJson. PlayerPrefs.SetString/GetString under a namespaced key. Normalize key = itemId.Trim().ToLowerInvariant() or keep exact id. Default return 0f. Call Save after every Set.
- Modal.Show: after storing entry, `_currentYaw = RotationCorrectionRegistry.GetYawOffset(_entry.id);` then setup + initial apply.
- In Confirm (before or after Cleanup): `if (_entry != null) RotationCorrectionRegistry.SetAndSave(_entry.id, yaw);` then `_onConfirm?.Invoke(yaw);`
- Add Reset button that does `_currentYaw = RotationCorrectionRegistry.GetYawOffset(_entry?.id) ?? 0f;` + force update.
- Add a small live Text for current yaw (update in Update or on rotate methods).
- Enlarge panel + RT area; bump RT_SIZE const to 384; improve drag sensitivity or switch the handler to a proper IDragHandler implementation on PreviewDragHandler for rect-accurate hit (optional polish).
- Keep all destroy/cleanup behavior (mobile leak safety).
- In controller Place path the existing data + loader already do the right thing once the modal returns a good yaw.

## Notes / Risks
- The controller currently does `_armedYawSteps = Mathf.RoundToInt(yaw / 90f) & 3; _armedYawOffset = yaw;` on modal return. This folds the free yaw partly into the discrete steps. For structures placed after this WO the effective visual yaw will still be correct (offset carries the remainder or the full value). If exact reconstruction matters for server replay etc., a future P2 could store full continuous yaw or change the quantization strategy — out of scope here.
- Ghost remains discrete-steps (intentional for aim UX). Fine orientation is chosen once at commit in the nice viewer.
- Persistence is per-device (PlayerPrefs). If cloud save / cross-device is added later, the small correction map can be folded into the existing save blob.
- Asset Store angle: see the .RESULT.md for the final honest assessment.

This WO is narrowly scoped to the preview + per-type yaw memory + premium feel. It directly builds on the already-wired modal/offset/ghost/loader system from prior build UX work.

---
Next agent step: implement exactly per acceptance, run brace gate after every .cs, produce RESULT.md, update README. Do not expand scope.