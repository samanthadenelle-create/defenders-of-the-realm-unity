<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-23
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-23) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK_ORDER_490 — OFFSET FORGE (model alignment / offset-authoring tool)

**Status:** DONE — audit-verified as shipped (2026-08-21 backlog audit).
**Form factor (owner decision, "whatever is best"):** **Unity EditorWindow, drop-in package** — primary.
Standalone .exe is a deferred Phase 2 (only if Asset-Store buyers ask). Same core either way.

## Why this exists (owner insight, verbatim)
"It's the one part AI cannot resolve." An AI can instrument/RCA/read code but **cannot eyeball a 3D
rotation** — every grip euler it emits (e.g. EquipmentController `gripEuler=(135,170,-45)`) is a blind
guess that costs a rebuild + human eyes to check. A tiny Blender-style viewer offloads that one
human-only step: a human sets the offset ONCE, data makes it correct FOREVER, no AI guesses again.
**Also a first Asset-Store product** — every Unity dev (and now every AI-assisted Unity workflow) hits
the misaligned-attachment wall. Keep it DEAD SIMPLE (owner: "it wasnt very complex").

## The tool (generic, ZERO game dependencies — so it's sellable + reusable)
A self-contained EditorWindow `Tools > Offset Forge`. NO `DeNelle.*` references (must drop into any project).
1. **Load model:** an Object field (accept GameObject prefab or model asset) + recent list. Instantiate into
   a temporary preview scene/`PreviewRenderUtility` (editor-standard offscreen render — the Asset-Store-clean
   way; our runtime `BuildPreviewModal` RT rig is the proven recipe to mirror, NOT to import).
2. **Viewport:** orbit (drag), zoom (scroll), pan — Blender-like. Grid/ground optional.
3. **Controls:** Rotation X/Y/Z + Position X/Y/Z numeric fields AND drag-sliders, live two-way with the model.
   Optional uniform scale. A "snap to 5°/15°" toggle.
4. **Live exact readout:** shows `eulerAngles (x,y,z)` + `localPosition (x,y,z)` to 2 decimals, with a
   **Copy** button (clipboard, as `new Vector3(x,y,z)` AND as plain `x,y,z`).
5. **Save/Export:** write `{ modelId, pos{x,y,z}, rot{x,y,z}, scale }` to a JSON file the user picks
   (default `Assets/OffsetForge/offsets.json`), keyed by model name. Append/update by id.
6. Package polish later: README, demo scene, icon — Phase 1 just nails the core loop.

## Slice 1 (BUILD NOW) — the core loop, generic
- `Assets/OffsetForge/Editor/OffsetForgeWindow.cs` (asmdef `OffsetForge.Editor`, editor-only, NO DeNelle refs)
  — the EditorWindow: object field, `PreviewRenderUtility` viewport with orbit, XYZ rot + pos fields,
  live readout + Copy, Save-to-JSON.
- `Assets/OffsetForge/Editor/OffsetForge.Editor.asmdef` — standalone, `includePlatforms: [Editor]`.
- `Assets/OffsetForge/offsets.json` — created on first export (not hand-authored).
- ASCII-only logs. Brace gate. No scene hand-edits.
**Acceptance (slice 1):** open `Tools > Offset Forge`, drop in ANY prefab (e.g. our Resources/Enemies/Orc_Warrior
or a shield mesh), orbit it, dial X/Y/Z + position, read the EXACT euler, Copy it, Save offsets.json. Compiles
clean (`COMPILE_GATE_OK`). Pure editor tool — no play mode, no game deps.

## Slice 2 — OUR game consumes the offsets (closes the shield loop, separate from the package)
- A thin `DeNelle.Village` adapter `AttachmentOffsetRegistry` (loads `Resources/Data/attachment-offsets.json`
  the Forge exported; `Get(domain,id) -> {pos,euler}` with PlayerPrefs live-override tier per the
  RotationCorrectionRegistry idiom). EquipmentController `AttachOffHandProp` reads `offhand/<shieldId>`,
  **falls back to the current preset if absent (zero regression)**, flag-gated `ff.attachmentoffsets` (default OFF).
  FlowTrace logs registry-hit vs preset.
- Then: open Forge → set the SHIELD offset → export → game reads it → shield correct, no more euler guessing.

## NOT in scope / guardrails
- Do NOT couple the package to DeNelle (kills resale + reuse). The game adapter is the ONLY DeNelle code.
- Keep it small — no animation, no material editing, no rig retarget. Offsets only.
- Reuse the KNOWN-GOOD preview-camera recipe from `BuildPreviewModal.cs`, but the package uses
  `PreviewRenderUtility` (editor-native), not the runtime RT.

## References
- `Assets/_Modules/Village/BuildMode/BuildPreviewModal.cs` — proven model-preview camera/light/frame recipe.
- `Assets/_Modules/Village/BuildMode/RotationCorrectionRegistry.cs` — the persist-the-offset idiom (PlayerPrefs JSON).
- `Assets/_Modules/Village/Hero/EquipmentController.cs` — the shield/weapon consumer (slice 2).
- Memory: [[model-alignment-offset-tool]].

> **AUDIT 2026-08-21 (agent fleet, read-only):** FIXED. Evidence: `OffsetForgeWindow.cs + asmdef` — standalone window exists. Status was flipped from READY by the AUDIT, not by an implementation pass. The body below is left intact; if this call is wrong, the evidence cited here is what to challenge.
