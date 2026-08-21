# WORK ORDER 283 — Canonical Animation Library (Shared + per-type, Humanoid retarget)

**Status:** CLOSED — owner range sweep 2026-08-21 (WO 0-800): completed or immaterial.
`Assets/Action/{Shared,Knight,Ranger,Enemies}/` exist; `ActionClipImporter` + the `HeroAnimatorFactory`
Cleric spec landed. ⚠ **NOT done in that pass:** the live runtime enemies are still **Generic KayKit rigs**
(the RESULT says so) — that remainder is the enemy half, not this WO.

> ⚠ **§15 STALENESS FLAG (2026-08-09).** This line read `READY TO IMPLEMENT` for ~2 months while a
> RESULT file sat beside it. That stale status directly caused a 2026-08-09 session to commission two
> architects to design work this WO had already shipped. **If a RESULT file exists, the status line is
> the thing to fix first.**
**Date:** 2026-06-06
**Author:** UI (creative/architecture lane)
**Owner approval:** Samantha — greenlit. This library is THE animation method for
**every current and future model** in the game.
**Priority:** High — heroes were just refreshed (WO-282 context); they need their
real motion set wired. Unblocks combat feel WOs (217/218/234).
**Lane:** Animation / editor-tooling + animator-factory code. **`Assets/Action/` +
`Assets/Editor/` only.** NO `VillageSceneBuilder.cs` (frozen, §3/§9). NO `.unity` hand-edits.
**Implemented + build-verified by:** CLI (owns batchmode import + animator bake).
**Source asset:** owner-uploaded `Animations.zip` (162 FBX) — see §2 for layout.

---

## 1. The convention (canonical — durable project memory)

Every model — heroes, enemies, and anything authored later — uses a **two-tier
Humanoid animation set**:

1. **Shared/** — base clips every model retargets: idle, walk, run, death, hit
   reaction, block, combat idle, victory, turns. One clip retargets onto all models
   (all are Humanoid `mixamorig`), so Shared is authored once and reused everywhere.
2. **Per-type folder** — class/type-specific clips layered on top of Shared.

**Type → folder mapping (fixed):**

| Model / class | Folder(s) used |
|---|---|
| Knight (hero) | `Shared/` + `Knight/` |
| Ranger (hero) | `Shared/` + `Ranger/` |
| Mage (hero) | `Shared/` + `Wizard/` |
| **Cleric (hero)** | `Shared/` + `Wizard/` — **casters share the Wizard set** (owner decision) |
| Enemies | `Shared/` + `Enemies/` |
| Future models | `Shared/` + a new per-type folder, same method |

Canonical doc: `docs/ANIMATION_PIPELINE.md` (created alongside this WO). Keep it the
source of truth; update it when a new type folder is added.

---

## 2. Source layout (from Animations.zip)

Import preserving these subfolders. Counts are from the uploaded zip:

```
Shared/    (15 fbx)  Shared_Idle, Shared_Walk_Forward, Shared_Run_Forward,
                     Shared_Death, Shared_Hit_Reaction, Shared_Block,
                     Shared_Combat_Idle, Shared_Victory_Pose, turns, injured turns…
Knight/    (99 fbx)  sword & shield full set, melee combos, blocks, draws/sheaths,
                     locomotion, taunts, reacts…
Ranger/    (13 fbx)  Ranger_Aim_Idle + standing/walk/run locomotion + 90° turns
Wizard/    (15 fbx)  1H/2H magic attacks, cast spells, area attacks, Wizard_Heal,
                     Wizard_Spell_Cast, standing idle  (used by Mage AND Cleric)
Enemies/   (20 fbx)  injured locomotion set (idle/walk/run/turns/jumps, backwards)
```

**Target:** `Assets/Action/Shared/`, `Assets/Action/Knight/`, `Assets/Action/Ranger/`,
`Assets/Action/Wizard/`, `Assets/Action/Enemies/`.

Rationale: `ActionClipImporter` (AssetPostprocessor) matches the `Assets/Action/`
**prefix**, so files in these subfolders auto-import as retargetable Humanoid with the
project's anti-slide root-motion handling — no per-file Inspector work, and **every
future drop into `Assets/Action/<Type>/` conforms automatically**.

---

## 3. Required import settings (owner-specified)

Per owner, each clip imports as:

- **Rig → Animation Type: Humanoid**, **Create Avatar** (`CreateFromThisModel`)
- **Animation → Compression: Optimal**
- **Bake Into Pose** for root motion on **walk/run** clips (in-place; code/NavMesh
  drives world translation — the anti-slide rule)
- **Loop Time: enabled** on **idle / walk / run** clips

### Reconcile with existing `ActionClipImporter.cs`

The importer ALREADY enforces, for everything under `Assets/Action/`:
- Humanoid + `CreateFromThisModel` ✓
- `lockRootPositionXZ = true` (Bake Into Pose XZ / in-place) ✓
- `loopTime` on idle/walk/run ✓
- `materialImportMode = None` ✓

**Gap to add:** Animation **Compression: Optimal**. Add to `OnPreprocessAnimation`
(and the two batch fix methods) e.g. `importer.animationCompression =
ModelImporterAnimationCompression.Optimal;` plus sensible
rotation/position/scale error tolerances. This makes Optimal the enforced default for
the whole library and all future Action clips — so the convention lives in code, not
manual clicks.

Confirm the existing in-place XZ bake matches the owner's "Bake Into Pose on walk/run"
intent (it does: XZ baked, Y + orientation preserved for jumps/deaths). Do not flatten
Y or rotation globally.

---

## 4. Animator factory wiring

`Assets/Editor/HeroAnimatorFactory.cs` currently builds Knight / Mage / Ranger
controllers from a **flat** `Assets/Action/` lookup and has no Cleric. Update it:

1. **Add a Cleric spec** — same clip sources as Mage (`Shared/` + `Wizard/`), output
   `Cleric.controller`.
2. **Point clip lookup at the new subfolders** per the §1 mapping (search
   `Assets/Action/<Type>/` + `Assets/Action/Shared/`). Keep the existing null-guarded
   basename lookup; just widen the search roots.
3. **Ranger** now HAS locomotion + `Ranger_Aim_Idle` (the old "no bow clip" placeholder
   warning can be replaced with the real aim/idle).
4. **Knight** has a large set — wire at least locomotion + a primary attack/combo +
   block; full combo trees can be a later pass.
5. **Enemies:** ensure the enemy animator path (`Assets/Editor/EnemyAnimatorSetup.cs`
   / `EnemyAnimatorFactory`) consumes `Shared/` + `Enemies/` (injured set) the same way.

Run after import (batchmode, editor closed):
- `Defenders/Animation/Reimport Action Clips (force Humanoid)`
  (`ActionClipImporter.ReimportActionClips`)
- `Defenders/Animation/Fix Action Clip Root Motion (stop slide)`
  (`ActionClipImporter.FixActionClipRootMotion`)
- `HeroAnimatorFactory.BuildAll` (+ enemy equivalent)

---

## 5. Dependency / sequencing with WO-282 (Addressables)

`HeroAnimatorFactory` outputs controllers to `Assets/Resources/Heroes/<slug>.controller`.
WO-282 **moves** hero models + controllers out of `Resources/Heroes/` into the
Addressables `Heroes` group. These overlap on the controller output path.

**Recommended order: WO-283 first, then WO-282.** Build the controllers in their
current `Resources/Heroes/` home here; WO-282 then relocates models + the freshly-built
controllers into Addressables and repoints load sites. Whichever runs second must
reconcile the controller output/lookup path — call it out in that WO's RESULT.

---

## 6. Acceptance criteria

- [ ] All 162 FBX imported under `Assets/Action/{Shared,Knight,Ranger,Wizard,Enemies}/`,
      each as **Humanoid** with a valid avatar (no Legacy/Generic left behind).
- [ ] Clips use **Optimal** compression; idle/walk/run loop; walk/run baked in-place
      (no slide). Verify a walk clip visually loops without foot-sliding.
- [ ] `ActionClipImporter` updated so Optimal compression is enforced for all
      `Assets/Action/` clips (current + future).
- [ ] `HeroAnimatorFactory` builds **four** hero controllers — Knight, Ranger, Mage,
      **Cleric** — Cleric + Mage both sourcing `Shared/` + `Wizard/`.
- [ ] Enemy animator consumes `Shared/` + `Enemies/`.
- [ ] Play smoke test: each of the 4 heroes idles/walks/runs and plays its primary
      attack/cast; an enemy plays the injured locomotion; no T-pose, no slide.
- [ ] `docs/ANIMATION_PIPELINE.md` reflects the final folders + mapping.
- [ ] **Brace balance check passes on every `.cs` edited** (CLAUDE.md §1).
- [ ] Batchmode build-verify succeeds (CLI). RESULT documents Knight clips actually
      wired (the set is large) and any clips intentionally deferred.

## 7. Do NOT touch

- `VillageSceneBuilder.cs` (frozen) or `.unity` files by hand.
- The hero **mesh** FBX in `Resources/Heroes/` — those are MODELS (WO-282), not these
  motion clips. This WO only adds motion FBX under `Assets/Action/`.
- Global root-motion Y/rotation flattening — preserve jumps/deaths/facing.

## 8. Notes for CLI

- Source FBX are in the owner upload `Animations.zip` (162 files). Import preserving
  subfolders. Owner chose CLI-side import (avoids mount-sync risk on bulk binaries).
- The Knight set is large (99 clips) — wire the essentials this pass; a follow-up WO can
  build out full sword-and-shield combo trees / blends.
- This library is declared canonical for all future models — when a new model type is
  added, it gets a new `Assets/Action/<Type>/` folder + `Shared/`, nothing else changes.

> **OWNER RULING 2026-08-21 (verbal, this session):** CLOSED by an explicit owner sweep of the WHOLE 0-800 RANGE: "I've eyeballed them many times, and all of them are already completed. or immaterial." This is a RANGE close on the owner's direct review, not a per-ticket verdict.
