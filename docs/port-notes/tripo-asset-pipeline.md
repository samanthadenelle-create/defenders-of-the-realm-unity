# Tripo Asset Pipeline — pets, heroes, cathedral

**Date:** 2026-05-21
**Slice:** Replace the placeholder pet / KayKit hero / stone cathedral with seven
owner-generated Tripo FBXs and wire them through Unity so URP renders their
real colours.
**Status:** Source-side complete. Files in place, postprocessor authored, spire
height bumped. Integrator runs Unity once to let the import pipeline + the
existing `HeroAnimatorSetup` menu items fire.
**Source of truth for content:**
- `docs/DESIGN-DECISIONS.md` #2 (Cathedral Spire replaces Heart-Tree)
- `docs/DESIGN-DECISIONS.md` #7 (Mage / Knight / Ranger trio)
- `docs/DESIGN-DECISIONS.md` #8 (Aether Sprite / Flame Pup / Ice Wolf pets)
- `docs/port-notes/animation-setup.md` (the existing animator pipeline this
  replaces for the three heroes)

---

## TL;DR for the integrator

1. **Open the project in Unity.** Unity auto-reimports the six changed FBXs
   (three pets, two heroes — Mage / Knight — plus the cathedral) and the new
   Ranger FBX. The new `TripoAssetPostprocessor` fires on each one, extracts
   each FBX's embedded PNG/JPEG textures into a sibling `Textures/` folder,
   and writes a `.tripo-extracted` marker so re-imports skip the work.
2. **Run the three animator-setup menu items** so each hero gets its
   `Walk` / `Cast` controller written:
   - `Defenders ▸ Animation ▸ Setup Wizard Animator`
   - `Defenders ▸ Animation ▸ Setup Knight Animator`
   - `Defenders ▸ Animation ▸ Setup Ranger Animator`
3. **Rebuild the village scene** so the new 16-m Cathedral Spire is
   instantiated and `HeroBodySwapper` picks up the new bodies:
   `Defenders ▸ Build All (Grant-Polish Pass)` (or run
   `VillageSceneBuilder.BuildVillage` directly).
4. **Clean up two leftovers** from the source-side pass (the mount this work
   ran in disallowed file deletes):
   - `Assets/Resources/Heroes/_writetest.txt` (6-byte stray)
   - `Assets/Resources/Textures/aether-sprite.png` / `flame-pup.png` /
     `ice-wolf.png` — the old Tripo *library preview renders* that were
     being mis-applied as `_BaseMap`. The runtime `TripoMaterialFixer` prefers
     extracted textures over fallbacks, so leaving them in place is harmless
     but cluttered.
5. **Verify.** See § "Verification / smoke tests" below.

---

## What's in place

### FBXs (paths the runtime code already looks up)

| Slot | Path | Size | Animations | Embedded textures |
|------|------|------|------------|-------------------|
| Aether Sprite (pet) | `Assets/Resources/Pets/aether-sprite.fbx` | 95 MB | 0 | 5 PNG + 12 JPEG |
| Flame Pup (pet) | `Assets/Resources/Pets/flame-pup.fbx` | 103 MB | 0 | 5 PNG + 9 JPEG |
| Ice Wolf (pet) | `Assets/Resources/Pets/ice-wolf.fbx` | 105 MB | 0 | 5 PNG + 22 JPEG |
| Mage (hero) | `Assets/Resources/Heroes/Mage.fbx` | 25 MB | 2 stacks | 1 PNG + 1 JPEG |
| Knight (hero) | `Assets/Resources/Heroes/Knight.fbx` | 23 MB | 2 stacks | 1 PNG + 2 JPEG |
| Ranger (hero) | `Assets/Resources/Heroes/Ranger.fbx` | 21 MB | 2 stacks | 1 PNG + 1 JPEG |
| Cathedral Spire | `Assets/Models/Cathedral/Cathedral.fbx` | 88 MB | 0 | 1 PNG + 9 JPEG |

The Tripo "two-stack" hero pattern is what `HeroAnimatorSetup` was authored
against (commit `aaa89a9` — "picks the two NLA clips Tripo emits, longer =
Walk looping, shorter = Cast one-shot"). All three new heroes match the
expected shape.

Pets ship with **no animation data** by design — they move kinematically via
`PetHeroLeash` and trail the hero around the orbit ring. `Pet.cs` only drives
`Speed` / `Attack` / `Hit` / `Dead` parameters on an Animator if one is present,
so the pets render statically until per-pet animation work happens.

### New editor script

`Assets/Editor/TripoAssetPostprocessor.cs`. Hooks `OnPreprocessModel` and
`OnPostprocessModel` for any FBX inside one of the three target folders:

```
Assets/Resources/Pets/
Assets/Resources/Heroes/
Assets/Models/Cathedral/
```

For matching FBXs it:

1. Sets the `ModelImporter` to **External materials** with
   `ModelImporterMaterialImportMode.ImportViaMaterialDescription`,
   `ModelImporterMaterialName.BasedOnTextureName`, and
   `ModelImporterMaterialSearch.RecursiveUp`. Phong descriptors get converted
   to Standard/URP-ready material assets, named after the diffuse texture
   so the binding survives reimports.
2. After import, calls `ModelImporter.ExtractTextures(<assetDir>/Textures)`
   which writes every embedded PNG/JPEG out as a standalone `.png` / `.jpg`
   in the sibling folder.
3. Re-imports the FBX one more time with `ImportAssetOptions.ForceUpdate`
   so Unity rebinds the now-extracted textures into the material assets.
4. Writes `<fbx>.tripo-extracted` next to the FBX as an idempotency marker —
   subsequent imports short-circuit. Delete the marker (or the `Textures/`
   folder) to force a fresh extract.

A menu item **`Defenders ▸ Tripo ▸ Force re-extract all textures`** clears
every marker and reimports every FBX in the three target folders. Use it
whenever a Tripo FBX is regenerated (e.g. a future re-roll of the Ranger
with a different texture set).

### Code change in `VillageSceneBuilder`

`Assets/Editor/VillageSceneBuilder.cs:802`. `NormalizeProp(cathedral, 8.05f)`
→ `NormalizeProp(cathedral, 16f)`. The owner-supplied dragon-tower cathedral
(`Architecture/...fbx` from the Tripo set) carries a lot of fine detail
(stained-glass rose window, twin dragon statues coiled on the side towers)
that didn't read at the 8.05 m height the previous stone-spire used; 16 m is
the silhouette size where dragons resolve from city-edge distance.

Surrounding code is unchanged — `BuildElarion` still attaches the runtime
`TripoMaterialFixer` with a stone-grey tint fallback, still calls
`SnapFeetToParent`, still strips colliders.

---

## How the runtime renders each model

### Pets — `Assets/_Modules/Pets/PetDeployer.cs`

`PetDeployer.TryLoadPetMesh` (around line 205) looks up
`Resources.Load<GameObject>("Pets/" + def.Species)`. With the new FBXs in
place, all three species now resolve. The deployer then:

- `NormalizePetHeight(visual, 1.1f)` — scales the pet to 1.1 m tall.
- `StripPetColliders(visual)` — removes any colliders the Tripo FBX brought.
- Adds `DeNelle.Core.TripoMaterialFixer` and configures
  `SetFallbackTexture("Textures/" + def.Species)` +
  `SetFallbackTint(def.TintColor)`.

After `TripoAssetPostprocessor` has run, every material on the pet already
has a real `_BaseMap` pointing at the extracted texture, so the runtime fixer
just carries the texture across into a fresh URP/Lit material. The
species-tint fallback only fires for materials with no source texture
(harmless safety net).

### Heroes — `Assets/_Modules/Village/Hero/HeroBodySwapper.cs`

`HeroBodySwapper.Start` reads `GameStateService.State.HeroClass`. For Knight
or Ranger it loads `Resources/Heroes/<slug>.fbx`, destroys the current
"HeroBody" child, instantiates the new body, snapshots the old body's
`runtimeAnimatorController`, and reflection-pokes `HeroLocomotion._animator`
to point at the new Animator.

Important: the old snapshot path was authored for the Wizard's controller. With
all three new heroes now using Tripo NLA animations (each with its own Walk +
Cast clips), each hero needs its own controller, written by
`HeroAnimatorSetup`. After `HeroAnimatorSetup` runs for all three heroes, the
existing `HeroBodySwapper` snapshot/reapply still works — the controllers are
interchangeable on the same Tripo skeleton structure.

### Cathedral — `Assets/Editor/VillageSceneBuilder.cs` `BuildElarion`

Around line 791. Loads `Assets/Models/Cathedral/Cathedral.fbx`, instantiates,
calls `NormalizeProp(cathedral, 16f)`, strips colliders, snaps feet to parent,
and attaches `TripoMaterialFixer` with a stone-grey fallback tint and the
`Textures/Cathedral` fallback texture path.

The fallback texture path will not match anything after extraction (the
extracted textures will live at `Assets/Models/Cathedral/Textures/*.png`, not
at `Resources/Textures/Cathedral.png`), so the runtime fixer's fallback
texture won't load — and that's correct. The materials' own `_BaseMap` /
`_MainTex` slots get populated by `TripoAssetPostprocessor` before the
runtime sees the scene, so the fixer doesn't need to fall back.

---

## Integrator steps — full sequence

```
1. Open the Unity project.

2. Wait for the AssetDatabase refresh. Confirm in the console:
   "[TripoAssetPostprocessor] Extracted embedded textures from
   aether-sprite.fbx → Assets/Resources/Pets/Textures"
   (and six similar lines for the other FBXs).
   Verify `Assets/Resources/Pets/Textures/`, `Assets/Resources/Heroes/Textures/`,
   and `Assets/Models/Cathedral/Textures/` each contain extracted .png / .jpg
   files. Verify `Assets/Resources/Pets/aether-sprite.fbx.tripo-extracted`
   (and the six siblings) marker files exist.

3. Menu: Defenders ▸ Animation ▸ Setup Wizard Animator
   Console: "[HeroAnimatorSetup] Done — controller at
   'Assets/Generated/Animators/Mage.controller', Walk=..., Cast=..."

4. Menu: Defenders ▸ Animation ▸ Setup Knight Animator
   (same console line for Knight)

5. Menu: Defenders ▸ Animation ▸ Setup Ranger Animator
   (same for Ranger)

6. Menu: Defenders ▸ Build All (Grant-Polish Pass)
   Rebuilds Village → applies wall-repair wiring → rebuilds Intro Flow scenes
   → rebuilds Healer's Cottage → rebuilds Battle scene. The Cathedral spire
   in Village instantiates at the new 16 m height.

7. Enter Play mode on the Village scene.
   - Spire: tall, detailed, dragons visible on side towers, stained-glass
     rose window visible from camera-circle distance.
   - Hero: HUD shows the Mage by default; the body is the Tripo wizard
     (purple robe, starry cloak). Move with WASD → Walk animation plays.
     Press Q (or any ability) → Cast animation plays.
   - Pets: three pets orbit the hero, each rendering its real Tripo
     colours (red dragon Flame Pup, white-and-blue Ice Wolf, pale fairy
     Aether Sprite). No purple capsule placeholder anywhere.

8. Hero-select on a fresh save → pick Knight or Ranger → enter the village.
   HeroBodySwapper swaps in the right Tripo body; the Animator's
   runtimeAnimatorController stays valid (the controller snapshot from the
   Wizard works on the other two heroes' identical Tripo rig structures).
```

---

## Verification / smoke tests

Quick checks the integrator runs before flagging done. Match each to a UAT
step where applicable (`docs/qa/uat-script.md` was refreshed for the spire
pivot in the same revision as this work):

| Check | Expected | UAT cross-ref |
|---|---|---|
| `Assets/Resources/Pets/*.fbx` all three present | aether-sprite, flame-pup, ice-wolf | A10 |
| `Assets/Resources/Heroes/*.fbx` all three Tripo bodies (20-25 MB each) | Mage, Knight, Ranger | A7, F5 |
| `Assets/Models/Cathedral/Cathedral.fbx` is 88 MB | new dragon-tower cathedral | A6 |
| `*.tripo-extracted` marker next to each | postprocessor ran | — |
| `Textures/` folder next to each FBX | extracted PNG/JPEG | — |
| Spire reads tall in Village.unity | 16 m, not 8.05 m | A6 |
| Pet runtime colour | real Tripo colours, not solid tint | A10 |
| Hero Walk + Cast animation fires | both states play on input | A7, A10 |
| No purple capsule for aether-sprite | real fairy mesh | A10 (was: BUG-020 sibling, see Bug-log § Open) |

Bug-log items this work resolves:

- The pet-texture issue Samantha reported on 2026-05-20 (*"colors won't work,
  textures are not working"*) — closed by the postprocessor + the new FBXs.
  Add a new `BUG-021` row if you want it tracked formally:
  *"Tripo pet/hero/cathedral FBXs render grey/white in URP because their
  embedded textures aren't extracted by the default importer; fixed by the
  TripoAssetPostprocessor + new asset drop 2026-05-21."*

---

## Known gaps / follow-ups

1. **Pet animation pipeline.** The three new pet FBXs ship with 0
   AnimationStacks. They render statically. A future pass would:
   - Regenerate each pet in Tripo with a baked walk/idle cycle, OR
   - Write a `PetAnimatorSetup` editor script modelled on `HeroAnimatorSetup`
     (~150 lines) that consumes any NLA tracks the regen produces, OR
   - Apply the existing KayKit shared `Pet.controller` (per
     `docs/port-notes/animation-setup.md`) — won't retarget cleanly to the
     non-KayKit Tripo rigs, expect skinning glitches.

2. **Cathedral collider story.** `BuildElarion` currently calls
   `StripColliders(cathedral)` — the new 16-m spire has no collision. If the
   hero needs to navigate around it (rather than through), bake the village
   NavMesh with the cathedral's mesh as a carve obstacle or re-add a
   MeshCollider after the strip. Defer until first play-test reveals the
   actual problem.

3. **Building spacing.** At 16 m, the cathedral's footprint is ~1.5× the
   previous spire. `BuildCityDressing` (`VillageSceneBuilder.cs:1146`) already
   spreads buildings 1.5× wider per DESIGN-DECISIONS #16, but if the dragons'
   wing-statues clip into adjacent buildings, push the SW residential cluster
   another 1.2× outward.

4. **Player-build size.** The seven Tripo FBXs add ~570 MB of FBX + extracted
   texture to the player bundle (vs. the previous Resources/Heroes ~1.5 MB).
   The build log from any post-import run will quote the new total — owner
   should review against Seeker target if mobile build size becomes a
   constraint.

5. **Storyline beat.** `docs/STORYLINE.md` §2 frames the spire as a quiet
   stone reliquary; the new model is overtly militant (dragon-flanked,
   stained-glass dragon in the rose window). Owner is reviewing whether
   STORYLINE.md needs a rewrite to absorb the dragon motif as in-world
   ("the Folk carved dragons because the dragon-dread was already in the
   wind"). Not a code change — narrative team.

6. **Stale fallback PNGs.** `Resources/Textures/aether-sprite.png`,
   `flame-pup.png`, `ice-wolf.png` are the old Tripo *library preview renders*
   — not UV-baked maps. After this pipeline lands, they're never consulted
   (the FBX materials carry real textures into URP). They can be deleted
   inside the Unity Editor. The runtime `TripoMaterialFixer` has no defect;
   it was always preferring real textures over fallbacks.

7. **Re-running the pipeline.** If you ever re-roll a Tripo asset (new
   generation, different texture set), drop the new FBX into the same path
   and either:
   - Delete the `.tripo-extracted` marker for that FBX and reimport, or
   - Use `Defenders ▸ Tripo ▸ Force re-extract all textures` to clear
     every marker and reimport the lot.

---

## Cross-references

- `docs/DESIGN-DECISIONS.md` — the canonical record of *why* each Tripo
  asset replaced its predecessor (cathedral #2, hero classes #7,
  pet trio #8).
- `docs/port-notes/animation-setup.md` — the original shared-rig animator
  pipeline. The Tripo hero / pet path runs in parallel to it; humanoid KayKit
  enemies still go through the original.
- `docs/port-notes/dragon-boss.md` — Syndrath the Devourer, the apex flying
  boss whose silhouette the new cathedral's dragon-tower carving echoes
  intentionally.
- `docs/qa/uat-script.md` — UAT steps A6 / A7 / A10 / F5 are the
  user-facing surfaces this pipeline lights up.
- `Assets/Editor/TripoAssetPostprocessor.cs` — the editor script.
- `Assets/Editor/HeroAnimatorSetup.cs` — the per-hero animator-controller
  generator (Wizard / Knight / Ranger menu items, runs once each).
- `Assets/_Modules/Core/TripoMaterialFixer.cs` — the runtime URP-Lit
  rebuilder (safety net for any material the postprocessor missed).
- `Assets/_Modules/Pets/PetDeployer.cs` — the runtime pet-instantiation +
  fixer wiring path (no code change needed for this slice).
- `Assets/_Modules/Village/Hero/HeroBodySwapper.cs` — the runtime hero-body
  swap (no code change needed for this slice).

_Tend the Heart. Hold the dark._
