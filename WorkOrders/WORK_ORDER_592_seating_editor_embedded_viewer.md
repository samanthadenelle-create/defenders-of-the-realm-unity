<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-07-03
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-07-03) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK ORDER 592 — Seating Editor: embedded rotatable hero+weapon viewer

**Status:** CLOSED — owner range sweep 2026-08-21 (WO 0-800): completed or immaterial.
**Origin:** owner felt-test 2026-06-29 grading sword/harvest offsets. The in-game Seating Editor
(WO-577, `SeatingEditorOverlay`) is unusable for actually dialing offsets:
- It edited the gear panel's THROWAWAY preview clone (`LaunchFor(_heroPreview.Equip)`), which is
  **destroyed when the panel closes** → owner: "i have to close the panel to get out, and in doing
  so lose pointer reference to the object i wanted to redo offsets against."
- When it falls back to the world hero, the **follow-camera pans back behind the hero** as she
  adjusts → owner: "can i force camera stationary ... the camera pans back behind me."
- Owner's design call (the fix): **"it would be much easier with the player and weapon in that
  viewer so i could rotate everything and see clearly."**

## Goal
Give `SeatingEditorOverlay` its **OWN self-contained, orbit-rotatable 3D viewport** showing the
hero + equipped weapon — so offsets are dialed against a clear, rotatable, WYSIWYG view that is
**independent of the gear panel lifecycle AND the world follow-camera**. Parity with the editor-only
Offset Forge window (PreviewRenderUtility orbit), built from runtime pieces we already have.

## Reuse, don't greenfield
- `Assets/_Modules/Village/Hero/HeroPreviewViewer.cs` — already renders a LIVE hero + weapon (+ off-hand
  + armor tier) to a RenderTexture on a dedicated `HeroPreview` layer with its OWN preview camera and
  its OWN `EquipmentController` (`.Equip`). This is the viewer to embed. The gear panel already uses it.
- `Assets/_Modules/Village/BuildMode/BuildPreviewModal.cs` — has drag-to-rotate (orbit) on a
  RenderTexture; mirror that input handling for the viewport.
- `SeatingEditorOverlay` already drives an injected `EquipmentController` live via
  `BeginSeatingEdit`/`ApplySeatingPreview`/`SaveSeating` (no change to that core).

## Build
1. **Overlay owns a `HeroPreviewViewer`.** On `Open()`, the seating overlay spins up its OWN
   HeroPreviewViewer seeded with the current loadout (main weapon + off-hand + armor tier, sourced
   from the world hero's GearLoadout / EquipmentController). The overlay injects **that viewer's
   `.Equip`** as `_injected` (so the sliders drive the model shown in the viewport — what-you-see-is
   -what-you-save). On `Close()`, dispose the viewer (HeroPreviewViewer.Dispose) and free the RT.
2. **Show the viewport in the panel.** Put the viewer's RenderTexture into the overlay UI (a large
   `Image`/`VisualElement` background-image on the LEFT/CENTER; keep the existing slider column on the
   RIGHT). The overlay already builds a transparent full-screen root — add the RT image to it.
3. **Drag-to-rotate (orbit).** Pointer-drag on the viewport orbits the preview (yaw, + optional
   pitch) — mirror BuildPreviewModal's drag math. Optional pinch/scroll zoom. This is the
   "rotate everything and see clearly" ask. Rotation is VIEW-only (orbit the preview camera or the
   model root) — it must NOT change the saved offset.
4. **Decouple from the gear panel + world camera.** Because the viewer is owned by the overlay and
   has its own camera, closing the gear panel no longer destroys the edit target, and the world
   follow-cam (SmartMobileCamera) is irrelevant to the viewport (no pan). The Orient buttons no
   longer need to keep the gear panel open or hand over a preview clone.
5. **Rewire the Orient buttons** (`InventoryUIBuilder.BuildOrientButton`, `EquipmentPanel.BuildOrientButton`):
   call `SeatingEditorOverlay.Launch()` (self-contained — it builds its own viewer from the current
   loadout). Drop the `LaunchFor(_heroPreview?.Equip)` panel-clone dependency. Dev-only guard stays.
6. **Save unchanged:** still writes to `AttachmentOffsetRegistry` (offsets.json dev file + JSON
   snippet) keyed by weapon id. The whole point: dial in the viewer → Save → correct everywhere.

## Acceptance
- Open the Seating Editor (Orient button, dev build): a rotatable hero+weapon viewport appears with
  the sliders; dragging orbits the view; the world camera never pans.
- Adjusting Rot/Pos/Scale moves the weapon in the viewport live; Save writes the offset; re-open
  shows the saved pose. Closing the gear panel does NOT break the editor (no lost reference).
- FlowTrace.Step("Seating", ...) on open/viewer-spin/save proves the flow.

## Out of scope
- No change to the offset MATH or the registry schema. No new player-facing UI. Dev-only tool.

> **AUDIT 2026-08-21 (agent fleet, read-only):** OPEN — STILL VALID. Evidence: `SeatingEditorOverlay.cs no HeroPreviewViewer` — orbit viewport unbuilt. Status left at READY deliberately: this work is real and unbuilt. Verified against HEAD 2f0b97bb5, not against the ticket's own claims.

> **OWNER RULING 2026-08-21 (verbal, this session):** CLOSED by an explicit owner sweep of the WHOLE 0-800 RANGE: "I've eyeballed them many times, and all of them are already completed. or immaterial." This is a RANGE close on the owner's direct review, not a per-ticket verdict. ⚠ NOTE FOR ANYONE REOPENING: the 2026-08-21 read-only audit had classified this one OPEN - STILL VALID, with the evidence cited above. The owner's review supersedes that call (owner statements are ground truth). The audit line is left in place deliberately, so if this work turns out to be needed, the evidence for it is still here rather than erased.
