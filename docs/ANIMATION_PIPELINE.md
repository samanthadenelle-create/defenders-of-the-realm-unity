# Animation Pipeline — Canonical Method (all current & future models)

**Status:** CANON — every model in the game follows this. Owner-established 2026-06-06.
**Implemented by:** WO-283 (library import + factory wiring).

---

## The method

Every model — heroes, enemies, anything authored later — is **Humanoid** and uses a
**two-tier animation set**: a shared base every model retargets, plus a per-type folder
of clips layered on top. Because all clips carry the `mixamorig` skeleton, one clip
retargets onto every model with no per-character re-authoring.

### Folder structure — `Assets/Action/`

```
Assets/Action/
  Shared/    base clips ALL models use: idle, walk, run, death, hit reaction,
             block, combat idle, victory, turns
  Knight/    sword-and-shield melee set, combos, blocks, draws/sheaths, locomotion
  Ranger/    aim idle + locomotion + turns
  Wizard/    1H/2H magic attacks, casts, area attacks, heal  (casters)
  Enemies/   injured locomotion set
  <NewType>/ future models get a new folder here + Shared — nothing else changes
```

### Type → folder mapping

| Model / class | Folders used |
|---|---|
| Knight | `Shared/` + `Knight/` |
| Ranger | `Shared/` + `Ranger/` |
| Mage | `Shared/` + `Wizard/` |
| Cleric | `Shared/` + `Wizard/` (casters share the Wizard set) |
| Enemies | `Shared/` + `Enemies/` |
| Any future model | `Shared/` + its own type folder |

---

## Import settings (enforced by `ActionClipImporter.cs`)

Anything under `Assets/Action/` (including subfolders) auto-imports with:

- **Rig → Humanoid**, avatar `CreateFromThisModel`
- **Animation Compression → Optimal**
- **Root motion baked in-place (XZ)** on walk/run — code / NavMesh drives world
  movement (this is the anti-slide rule; do NOT flatten Y or rotation — jumps, deaths,
  and facing need them)
- **Loop Time** on idle / walk / run
- Materials not imported (these are motion clips, not art)

The convention lives in the importer, not in manual Inspector clicks — so **every future
FBX dropped into `Assets/Action/<Type>/` conforms automatically.**

### Batchmode helpers (CLI, editor closed)

- `Defenders/Animation/Reimport Action Clips (force Humanoid)` —
  `ActionClipImporter.ReimportActionClips`
- `Defenders/Animation/Fix Action Clip Root Motion (stop slide)` —
  `ActionClipImporter.FixActionClipRootMotion`
- `HeroAnimatorFactory.BuildAll` — builds per-class controllers from the clip sets
  (+ enemy animator equivalent)

---

## Adding a new model later

1. Drop its model FBX in (heroes → `Resources/Heroes/` until the Addressables `Heroes`
   group exists, per WO-282; enemies per their convention).
2. If it needs unique motion, add an `Assets/Action/<NewType>/` folder of clips; it
   inherits `Shared/` automatically.
3. Add a spec in `HeroAnimatorFactory` (or the enemy factory) mapping the type to its
   folders; run the reimport + BuildAll batch methods.

No other changes. That's the whole pipeline.

> Related: WO-283 (this library), WO-282 (heroes → Addressables), WO-140 (original
> Mixamo→controller factory), WO-217/218/234 (animation feel/layering/sweep).
