# Dungeon integration — Healer's Cottage playable + breach-return fix

**Date:** 2026-05-19
**Slice:** Wire the Week 5/6 dungeon systems into `Dungeon_HealersCottage.unity`
so the dungeon plays end-to-end, and fix BUG-008 (the ATB battle hard-coded its
post-battle return to the Village even for a dungeon encounter).
**Status:** Source written. Cannot build / run Unity here — the integrator runs
`Defenders ▸ Dungeons ▸ Build Healer's Cottage (D1)` (or `-executeMethod
DeNelle.Editor.DungeonSceneBuilder.BuildHealersCottage`), then plays the scene
to verify (checklist at the end).

## Files changed

| File | Change |
| ---- | ------ |
| `Assets/Editor/DungeonSceneBuilder.cs` | **extended** (not rewritten) — wires the Week 5/6 systems into the scene: `DungeonHero` on the Keeper, a real Cinemachine follow rig (`CinemachineCamera` + `CinemachineFollow` + `DungeonCameraRig`), a `CinemachineBrain` on the Main Camera, the mini-boss `EncounterTrigger`, the `WandererBubble` on Bryn, the BUG-007 wall-collider hardening pass, and a fuller `DungeonController` wiring. |
| `Assets/_Modules/Dungeons/DungeonController.cs` | **extended** — run-time hydration: loads `lore-fragments.json`; hydrates the lore stones / checkpoints / encounter triggers from the layout JSON; hands Bryn the hero + lore set; resolves the in-flight ATB encounter on dungeon re-entry; seeds placeholder hero vitals; `OnDestroy` no longer ends the run while an encounter battle is pending (the BUG-008 round-trip). |
| `Assets/_Modules/Dungeons/Wanderer/WandererBubble.cs` | **new** — the concrete `IWandererBubble`: a self-building, billboarded world-space speech bubble for Bryn (TextMesh + a tinted quad panel). |
| `Assets/_Modules/Core/SceneRouter.cs` | **changed** — `BattleParams` gains a `ReturnScene` field (defaults to `SceneRouter.Village`). |
| `Assets/_Modules/BattleATB/BattleController.cs` | **changed** — `ReturnAfterResult` returns to `BattleParams.ReturnScene` instead of the hard-coded `SceneRouter.Village` (BUG-008). |
| `Assets/_Modules/Dungeons/EncounterTrigger.cs` | **changed** — `LaunchBattle` sets `BattleParams.ReturnScene = SceneRouter.DungeonHealersCottage` so a dungeon encounter routes back into the dungeon. |

No `.asmdef` changed. No `.meta` hand-created. `docs/unity-decisions.md` not edited.

## BUG-008 — the dungeon ATB return (the breach-return fix)

`BattleController.ReturnAfterResult` hard-coded `SceneRouter.LoadSceneWithFade
(SceneRouter.Village)`, so a dungeon encounter battle always dumped the player
back in the Village instead of the dungeon. The fix is a single new field plus
a resolver — minimal, and it keeps the Village as the default:

1. **`BattleParams.ReturnScene`** (`SceneRouter.cs`) — a `string`, initialised to
   `SceneRouter.Village`. A village breach (`WaveManager`) builds `BattleParams`
   without touching it, so it stays `Village` — village behaviour is unchanged.
2. **`EncounterTrigger.LaunchBattle`** sets `ReturnScene =
   SceneRouter.DungeonHealersCottage` on the dungeon's `BattleParams`.
3. **`BattleController.ReturnAfterResult`** calls a new `ResolveReturnScene()` —
   `PendingBattle.ReturnScene` when set, else `SceneRouter.Village` (a null
   handoff from dev / direct play still resolves to the Village).

The dungeon **side** of the round-trip was already built (the Week-6
`DungeonRuntimeState` encounter handoff). Two things were needed to actually
close the loop, both in `DungeonController`:

- **`OnDestroy` must not end the run mid-round-trip.** When the dungeon routes
  to `ATBBattle` the `DungeonController` is destroyed; the old `OnDestroy`
  unconditionally called `EndRun()`, which wipes the encounter handoff + hero
  vitals the `DungeonRuntimeState` SO carries across the scene round-trip. It
  now early-returns when `RuntimeState.HasPendingEncounter` is true (the scene
  is being torn down for the battle, not a genuine exit). A real exit (no
  pending encounter) still ends the run.
- **`EnterDungeon` resolves the pending encounter on re-entry.** When the
  dungeon scene reloads after the battle, `HasPendingEncounter` is still true
  (a ScriptableObject survives `SceneManager.LoadScene`). The controller then
  spawns the hero at `EncounterResumePosition`, hydrates the encounter triggers,
  and calls `ResolvePendingEncounter()` → the matching
  `EncounterTrigger.ResumePendingEncounter(victory)` (which clears the combat
  lock and, on a boss victory, flags the boss defeated).

**Known limitation — the victory flag.** `ResolvePendingEncounter()` currently
assumes `victory == true`. The ATB outcome lands on the *battle* runtime state
(`ATBRuntimeState.Result`), and the dungeon module references `DeNelle.Core`
only — not `DeNelle.BattleATB`. v1's ATB engine fully restores party HP/MP after
every fight, so a clean resume is a reasonable victory assumption, and the
cleared encounter never re-fires either way. A Core-level battle-result carrier
(e.g. an outcome field on a runtime SO both modules can see) is the proper fix
and is flagged for the Week-2 battle owner.

## What the scene builder now wires (Week 5/6 checklists)

### Week 5 — hero, camera, walls, audio

- **`DungeonHero`** on the `Keeper` rig (reflection — `DeNelle.Dungeons`). The
  `CharacterController` is sized to the KayKit mage mesh (radius 0.35, height
  1.9, centred at y 0.95, slopeLimit 50, stepOffset 0.45 so it climbs the KayKit
  stair pieces). The visual capsule has its collider stripped — the
  `CharacterController` IS the collision body.
- **Cinemachine follow rig** — `FollowCameraRig` carries a `CinemachineCamera`
  + `CinemachineFollow` (Body, no Aim — the fixed isometric tilt) +
  `DungeonCameraRig`. The `Main Camera` carries a `CinemachineBrain`. All
  Cinemachine + Dungeons components are added by reflection (`FindType` resolves
  them — the `DeNelle.Editor` asmdef references neither package; both
  assemblies are loaded in the editor — the same pattern `BattleSceneBuilder`
  uses for `InputSystemUIInputModule`).
- **`DungeonController`** wired to `_hero`, `_heroController`, `_followCamera`,
  `_cameraRig`, `_lantern`, `_bryn`, the interactable roots and `_ambientBgm`.
- **Wall colliders (BUG-007)** — every KayKit wall / doorway / structure mesh
  already gets a fitted collider at build time (`EnsureCollider`). A new
  `VerifyWallColliders` hardening pass walks every mesh under a `Walls` group or
  the `VerticalConnectors` root and guarantees a collider, **skipping the
  `[ILLUSORY]`-prefixed walls** (the crypt↔hidden-vault hidden passages, which
  are collider-free by design). It logs the count hardened + the illusory count
  skipped. Idempotent.
- **Ambient BGM** — an `AudioSource` (`loop` on, `playOnAwake` off,
  `spatialBlend` 0, volume 0.25 per audio-mix-spec §2). The clip is left
  **unassigned** — `echoes-beneath-elarion.mp3` is not in the project.
  `DungeonController.StartAmbientAudio()` guards a null clip (logs a warning,
  plays silently). Import the MP3 to `Assets/Audio/dungeons/` and assign it to
  `DungeonController._ambientBgmClip` when it lands — no code change.

### Week 6 — interactables, NPC, encounters, ATB round-trip

- **Lantern** — a hero-childed point `Light` + `Lantern` component (already in
  the builder). `_flickerAudio` left null — no `lantern-flicker.mp3` exists; the
  `Lantern` guards a null clip.
- **Bryn** — placed at the Garden Approach with a `WandererBubble` (the concrete
  `IWandererBubble`) on a `SpeechBubbleAnchor` child, wired to her
  `_bubbleBehaviour`. `DungeonController.ConfigureBryn` now also calls
  `SetHero` and `SetLoreFragments` so the bubble shows on proximity.
- **Lore stones** — 5 placed; `DungeonController.HydrateLoreStones` calls
  `Configure(def, runtimeState, hero, total=5)` + `SetLoreFragments(set)` on
  each, pairing scene child `[i]` with `Layout.loreStones[i]` (the builder
  places them in layout order). **The UI Toolkit reading modal
  (`LoreStoneModal.uxml`) + its HUD-side controller are NOT built here** — that
  is a HUD-module deliverable. Each stone raises the typed `LoreReadRequest`
  via its `ReadRequested` event; the HUD subscribes. Until that modal exists,
  reading a stone records the read + advances the questline but shows no panel.
- **Checkpoints** — 2 placed; `HydrateCheckpoints` calls `Configure(def,
  runtimeState, hero)`. The heal (`HealHeroToFull`) fires on first proximity. A
  HUD toast on `ToastRequested` is a HUD-module deliverable.
- **Encounter triggers** — the builder now places 4 scripted triggers **plus**
  the mini-boss trigger (`Encounter_apprentice-of-the-apothecary`, built last).
  `HydrateEncounters` configures scripted child `[i]` with
  `Layout.scriptedEncounters[i]` and the trailing child with `Layout.miniBoss`
  via `ConfigureBoss`.
- **Hero vitals** — the dungeon module owns no hero-stat type, so
  `DungeonController` seeds **placeholder** vitals
  (`_heroBaselineHp` 120 / `_heroBaselineMana` 60) onto the run state at run
  start so the checkpoint heal + the ATB round-trip have numbers. When a real
  dungeon hero-stat component lands it should drive
  `DungeonRuntimeState.SetHeroVitals` each frame instead.
- **Build Settings** — `Dungeon_HealersCottage` is registered by the builder
  (`EnsureBuildSettings`). **`ATBBattle` must also be in Build Settings** for the
  round-trip — the integrator should confirm it (the `BattleSceneBuilder`
  registers it).

## Design calls

- **`WandererBubble` uses `TextMesh`, not a UGUI Canvas.** The v2 port renders
  UI through UI Toolkit (`UnityEngine.UIElements`, a core module). A UGUI
  world-space Canvas would pull the separate `UnityEngine.UI` assembly into the
  `DeNelle.Dungeons` asmdef. A speech bubble is a tiny piece of 3D text, so the
  bubble is built from `TextMesh` + a tinted quad — both core `UnityEngine`, no
  new assembly reference. It billboards to `Camera.main` each `LateUpdate` so it
  faces the player under the isometric tilt, and self-builds its panel + text in
  `Awake()` (no prefab, no `.meta` to hand-author). **Worth a unity-decisions.md
  row** — flagged, not written, per the task brief.
- **Cinemachine + Dungeons types are added by pure reflection** from the editor
  builder. The `DeNelle.Editor` asmdef stays referencing only Core / Data /
  Localization / URP — no `Unity.Cinemachine`, no gameplay asmdef. `FindType`
  resolves both because their assemblies are loaded in the editor.
- **Scene-child ↔ layout-array pairing is by index.** The builder places lore
  stones / checkpoints / encounter triggers as flat siblings in layout-array
  order; the controller pairs them by `GetComponentsInChildren` order (hierarchy
  order = sibling creation order). A count mismatch logs a warning and hydrates
  what aligns rather than throwing.

## Integrator verification checklist (Unity build run)

1. **Build the scene** — run `Defenders ▸ Dungeons ▸ Build Healer's Cottage
   (D1)`. Confirm 0 compile errors and the summary log (rooms, model count, the
   BUG-007 pass line: "added a collider to N wall/structure mesh(es)" and "M
   illusory wall(s) left collider-free").
2. **No walk-through-walls (BUG-007) — CRITICAL.** Enter play mode. Walk the
   Keeper (WASD) into every room's walls — the hero must **slide**, never pass
   through. Specifically test: the Garden Approach perimeter, every doorway
   frame, the Workshop walls. Then walk into the **two illusory walls**
   (crypt-sublevel ↔ hidden-vault, the `[ILLUSORY]`-named pieces near
   x≈12, z≈15–21 underground) — the Keeper **must** pass through those into the
   Hidden Vault. If a solid wall lets the hero through, that wall's KayKit FBX
   imported without a collider and the `VerifyWallColliders` pass missed it —
   check it sits under a `Walls` group.
3. **Hero + camera** — the Keeper spawns in the Garden Approach facing east;
   the Cinemachine follow camera holds the isometric tilt and chases smoothly;
   WASD is screen-relative; a mouse/touch tap walks the hero to the tapped point.
4. **Bryn** — walk toward Bryn at the entrance; her `WandererBubble` fades in
   with the canon Tier-1 line, fades out when the Keeper leaves.
5. **Lore stones / checkpoints** — confirm the 5 lore stones and 2 checkpoint
   shrines are placed; the checkpoint crystal goes violet→gold on first
   proximity. (The lore reading modal is a HUD deliverable — reading records the
   read but shows no panel yet.)
6. **Encounters + the ATB round-trip (BUG-008) — CRITICAL.** Walk into a
   scripted encounter zone (e.g. the Garden Approach trigger). The scene should
   fade into `ATBBattle`. Win/lose the fight; on result, the scene must fade
   back into **`Dungeon_HealersCottage`** (NOT the Village), with the Keeper
   placed at the encounter resume position and able to move again. Re-entering
   the same trigger must NOT re-fire the fight. Confirm `ATBBattle` is in Build
   Settings.
7. **Mini-boss** — reach the Workshop trigger; the boss fight launches, and on
   victory the run state's `BossDefeated` flips true on return.
8. **Village regression** — run a Village wave breach into `ATBBattle`; confirm
   it still returns to the **Village** (BUG-008 fix must not regress this — the
   village `BattleParams.ReturnScene` defaults to `Village`).
9. **Ambient audio** — the dungeon plays silently with a single
   `[DungeonController]` warning about the missing `echoes-beneath-elarion`
   clip. Expected until the MP3 is imported.

## Follow-ups / not in this slice

- `LoreStoneMode​l.uxml` + the HUD reading-modal controller, and the HUD toasts
  for checkpoints — HUD-module deliverables.
- A Core-level battle-result carrier so `ResolvePendingEncounter` reads the real
  victory/defeat instead of assuming victory.
- A real dungeon hero-stat component to drive `SetHeroVitals` (placeholder
  baseline used for now).
- `echoes-beneath-elarion.mp3` import.
- A dedicated `Floor` layer for `DungeonHero._walkableMask` (left at
  `Everything` — works, but a tap can land on a prop).
