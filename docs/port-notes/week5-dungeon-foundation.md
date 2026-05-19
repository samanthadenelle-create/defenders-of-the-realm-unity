# Week 5 — Dungeon Foundation (hero walk + camera + ambient BGM)

**Date:** 2026-05-19
**Slice:** v2-unity-port-spec.md Part 5 Week 5 — the Healer's Cottage scene exists,
the Keeper walks it, the Cinemachine camera follows, wall mesh colliders stop
walk-through. Layered on top of the existing `Dungeon_HealersCottage.unity`
(12-room scene) and the `DungeonController` / `DungeonLayout` scaffold.
**Status:** Source written. Cannot build / run Unity here — scene wiring,
collider verification, and audio import are integrator tasks (checklist below).

## Files produced / changed

| File | State | Purpose |
| ---- | ----- | ------- |
| `Assets/_Modules/Dungeons/DungeonHero.cs` | **new** | CharacterController-based dungeon locomotion — WASD/arrows on desktop, smooth tap-to-move on touch/mouse, sliding wall collision, gravity-grounded on stairs. |
| `Assets/_Modules/Dungeons/DungeonCameraRig.cs` | **new** | Self-configuring top-down isometric Cinemachine follow rig — fixed pitch/yaw chase, no orbit. |
| `Assets/_Modules/Dungeons/DungeonController.cs` | **changed** | Now wires the `DungeonHero` (input held off across the spawn teleport), prefers the `DungeonCameraRig` for framing, and wires the dungeon ambient BGM with a guarded missing clip. |

No `.meta` files were hand-created (Unity generates them on import). No asmdef
change needed — `DeNelle.Dungeons` already references `Unity.Cinemachine`,
`Unity.InputSystem`, `UniTask`, `DeNelle.Core`. Both new files are in the
`DeNelle.Dungeons` namespace.

**Files NOT touched** (owned by the concurrent Week-6 interactables agent):
`Lantern.cs`, `Bryn.cs`, `LoreStone.cs`, `EncounterTrigger.cs`, `Checkpoint.cs`,
`RandomEncounterTable.cs`, `DungeonRuntimeState.cs`. `Assets/Editor/DungeonSceneBuilder.cs`
was not touched — scene wiring is the integrator's job (see checklist).

## Design calls

### Hero locomotion — `DungeonHero`
- **CharacterController is the collision body.** Per port-spec Week 5, the hero
  is a `CharacterController` that slides along the KayKit wall mesh colliders.
  `[RequireComponent(typeof(CharacterController))]` enforces this. A single
  `_controller.Move()` call per frame (planar slide + gravity together) lets the
  controller resolve wall collision and grounding once — this is the "no
  walk-through" guarantee, **conditional on the wall meshes carrying colliders**
  (see integrator checklist item 4).
- **Two input schemes, keyboard wins.** WASD/arrows give a continuous
  camera-relative move vector; a touch/mouse tap raycasts onto the floor and the
  Keeper walks a straight line to it. Any held WASD cancels an in-flight tap-move
  (desktop players expect the keyboard to override). This matches the port-spec
  line "smooth tap-to-move on touch; WASD on desktop".
- **Camera-relative WASD.** The raw input is projected onto the camera's
  floor-plane basis so "up" is always screen-up under the isometric tilt. The
  controller pushes the live Unity camera into the hero via `SetCamera()`.
- **Input System low-level polling, not an `.inputactions` asset.** The project
  ships no `.inputactions` asset. Dungeon movement is a small fixed scheme, so
  `DungeonHero` polls `Keyboard.current` / `Mouse.current` / `Touchscreen.current`
  directly. If a project-wide input asset lands later, `SampleDesktopMove()` and
  `TryGetTapScreenPosition()` are the two seams to swap. **Decision worth a
  unity-decisions.md row** — flagged for the owner/integrator (this note does not
  edit that log per the task brief).
- **Gravity every frame.** A constant downward accel keeps `isGrounded` latched
  on the 12-room scene's stairs and uneven KayKit floor tiles; without it the
  controller can float off a step edge.

### Camera — `DungeonCameraRig`
- **Fixed-angle isometric, not orbit.** The `CinemachineCamera` carries only a
  `CinemachineFollow` (Body) component and **deliberately no Aim component**.
  With no Aim stage Cinemachine leaves the camera's transform rotation alone, so
  the rig keeps its authored pitch (~52°) and yaw — the steady top-down
  isometric tilt the spec asks for. `CinemachineFollow` with `WorldSpace`
  binding slides the camera at a fixed `FollowOffset` from the Keeper.
- **`DungeonController` keeps an inline fallback.** If the scene wires a bare
  `CinemachineCamera` with no `DungeonCameraRig`, `ConfigureCamera()` still
  applies the offset/pitch from its own serialized fields. The rig component is
  the preferred, self-contained path; the integrator can use either.
- **Orthographic option.** `DungeonCameraRig` exposes an `_orthographic` toggle
  for a true parallel-projection isometric look; defaults to perspective FOV 40.

### Ambient BGM
- Dungeon BGM volume is **0.25**, fixed by `audio-mix-spec.md` §2 (the `dungeon`
  track — "very soft, ambient only", owner directive 2026-05-18). Exposed as a
  `[Range(0,1)]` serialized field defaulting to 0.25.
- `DungeonController.StartAmbientAudio()` sets `loop = true`, `playOnAwake =
  false`, applies the volume, assigns the clip, and plays it.
- **The full crossfade / nudge system (audio-mix-spec §3/§4) is NOT built here.**
  Week 5 only needs the looping dungeon ambient. The spec's `MusicDirector`
  MonoBehaviour does not exist yet; when it lands it should take ownership of
  this AudioSource and the lore-stone/boss volume nudges. For now the BGM is
  self-contained in `DungeonController`.

## FLAGGED — missing audio asset

`echoes-beneath-elarion.mp3` **does not exist in the project.** `Assets/Audio/`
is an empty directory; there are no `.mp3` / `.wav` / `.ogg` files anywhere
under `Assets/`. Per the task brief I did **not** invent an audio file.

- The code path is fully wired: `DungeonController` has an `_ambientBgmClip`
  `AudioClip` field + an `_ambientBgm` `AudioSource` field + the 0.25 volume.
- `StartAmbientAudio()` **guards the missing clip** — when no clip is assigned
  (and none is on the AudioSource) it logs a `Debug.LogWarning` and the dungeon
  plays silently instead of throwing.
- **Action for the owner/integrator:** when `echoes-beneath-elarion.mp3` is
  available, import it to `Assets/Audio/dungeons/echoes-beneath-elarion.mp3`
  (mirrors the React path `/audio/dungeons/...` from audio-mix-spec) and assign
  it to `DungeonController._ambientBgmClip` (or directly to the AudioSource's
  clip). No code change needed.

## Integrator wiring checklist (Dungeon_HealersCottage scene — no Unity access here)

1. **Hero rig** — add `DungeonHero` to the Keeper's hero GameObject. It
   `[RequireComponent]`s a `CharacterController`; size the capsule to the KayKit
   mage mesh (radius ~0.3–0.4, height ~1.8, centred). Assign the hero
   `Transform` to `DungeonController._hero` and (optionally — it auto-finds) the
   `DungeonHero` to `DungeonController._heroController`.
2. **Hero walkable mask** — set `DungeonHero._walkableMask` to the dungeon floor
   layer(s) so tap-to-move raycasts hit the floor, not props/walls. Leaving it
   as `Everything` works but a dedicated `Floor` layer is cleaner.
3. **Camera rig** — add `DungeonCameraRig` to the `CinemachineCamera`
   GameObject. Confirm the Unity Camera has a `CinemachineBrain`. Assign the
   `CinemachineCamera` to `DungeonController._followCamera` and the
   `DungeonCameraRig` to `DungeonController._cameraRig`. Tune pitch/offset in the
   inspector if the 12-room layout reads better at a different angle.
4. **Wall colliders — CRITICAL for the "no walk-through" acceptance.** Every
   KayKit wall mesh in `Dungeon_HealersCottage.unity` must carry a collider
   (`MeshCollider`, or box colliders for perf). `DungeonLayout` distinguishes
   `solid` / `doorway` / `illusory` wall kinds — `illusory` walls must have **no**
   collider (the Keeper walks through into the secret room). Verify by walking
   the Keeper into every wall: it should slide, never pass through. This is the
   port-spec Week 5 deliverable + the "T60 lesson" in the layout spec §14.13.
5. **Ambient audio** — add an `AudioSource` to the dungeon (loop on,
   playOnAwake off) and assign it to `DungeonController._ambientBgm`. Assign the
   `echoes-beneath-elarion` clip to `_ambientBgmClip` **once the MP3 is imported**
   (see FLAGGED section). Volume is pre-set to 0.25.
6. **Runtime state** — assign the existing `HealersCottageRuntimeState.asset` to
   `DungeonController._runtimeState` (it may already be wired).
7. **Layout JSON** — confirm `StreamingAssets/Data/Canonical/dungeons/healers-cottage.json`
   exists with a `spawn` block (room id + position + facingY at the SW Garden
   Approach entrance). `DungeonController` falls back to the entry-room centre if
   `spawn` is absent, but an explicit spawn is correct.

## Acceptance check (port-spec Week 5 deliverable)

> "hero spawns in the Cottage entrance, walks the rooms, can't clip through walls."

- Spawn at the entrance — handled: `DungeonController.ResolveSpawnPosition()` +
  `PlaceHero()` → `DungeonHero.Teleport()`.
- Walks the rooms — handled: `DungeonHero` WASD + tap-to-move; camera follows via
  `DungeonCameraRig`.
- Can't clip through walls — handled in code (CharacterController sliding
  collision); **gated on integrator checklist item 4** (wall meshes need
  colliders — cannot verify without a Unity build).

## Known limitations / later passes

- **No walk animation hook.** `DungeonHero` exposes `IsMoving` / `CurrentSpeed`;
  an `Animator` blend (idle ↔ walk) is a later pass once the KayKit mage rig is
  imported with its animation clips.
- **No NavMesh.** Tap-to-move walks a straight line to the tapped point — it does
  not path around obstacles (the CharacterController slides off walls in the
  way). A `NavMeshAgent`-driven path is a v1.1 polish item if the 12-room layout
  needs corner navigation; for the foundation slice straight-line + slide is
  sufficient and matches the "cozy, simple" register.
- **Audio crossfades / nudges** — the audio-mix-spec §3/§4 crossfade table and
  the lore-stone/boss volume nudges are not built; they belong to a future
  `MusicDirector`. Week 5 ships only the looping dungeon ambient.
- **`echoes-beneath-elarion.mp3` not imported** — see FLAGGED section.
- **A unity-decisions.md row** is warranted for the Input-System low-level-polling
  choice (no `.inputactions` asset) and the no-Aim-component isometric camera
  pattern. This note does not edit `docs/unity-decisions.md` per the task brief —
  flagged for the owner/integrator.
