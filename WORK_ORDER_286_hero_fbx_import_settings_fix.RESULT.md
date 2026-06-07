# WORK ORDER 286 — RESULT (RESOLVED — heroes re-rigged Humanoid)

**Status:** DONE (build-verified) — `148c42f` (Read/Write + Ranger green) → `0c86454`
(re-rigged FBX, all 4 Humanoid). Pushed `feat/tower-core-loop`.
**Date:** 2026-06-06 · **By:** CLI (importer + batchmode reimport, editor closed).

## RESOLUTION (2026-06-06)
Owner re-rigged the 4 meshes via AccuRIG (CC_Base) and re-exported. After clearing the
stale `humanDescription` in `HeroFbxImporter` (the old meta's bone hierarchy rejected
the new rig — Cleric's root parent is 'Cleric', not 'Armature'), all four import with a
**valid Humanoid avatar (human=True), zero avatar failures, Read/Write ON**. The WO-285
controllers retarget the library onto them. Windows build-verify SUCCESS. Heroes should
now stand + animate + texture; final upright/scale/green verified by owner playtest.


## Landed (deterministic, committed)
- **Read/Write Enabled** on all 4 hero FBX → `isReadable: 1` (was 0). Kills the
  per-frame `isReadable is false / ReadWrite must be enabled` console spam **and** the
  `HeroBodySwapper` foot-grounding vertex-read exception. Verified in the metas.
- **`HeroFbxImporter`** (new `AssetPostprocessor`, keyed to `Assets/Resources/Heroes/`):
  forces Read/Write + Humanoid + Create-From-This-Model on every hero FBX dropped here,
  so the next mesh swap auto-imports correct (no recurrence). Mirrors `ActionClipImporter`.
  (Note: adding it triggered a one-time full-library model reimport — expected, drained clean.)
- **Ranger green fix:** `HeroBodySwapper` diffuse repointed off the removed
  `Heroes/Ranger_tex/remesh_12_combined_Bake_Diffuse` → existing loadable
  `Heroes/Textures/Archer_basecolor`. (Mage/Knight/Cleric texpaths already resolve.)

## BLOCKED → resolved by owner choice: AccuRIG re-rig
The Humanoid **avatar cannot build** on the swapped meshes. Unity, all 4:
```
Rig Error: Avatar creation failed:
  Transform 'CC_Base_Hip' for human bone 'Hips' not found
  Required human bone 'Hips' not found
```
The new Tripo FBX have **no CC_Base humanoid skeleton**, so Humanoid clips can't
retarget → T-pose persists. This is a rig/asset issue, not import settings.
**Generic was ruled out** (evidence-based): the heroes (CC_Base) and the Mixamo/Action
library (`mixamorig:`) are different skeletons — they only ever animated via Humanoid
retargeting; Generic drives bones by name and would not cross-retarget (and would
reverse the WO-283 "one Humanoid clip → every model" foundation).

**Owner decision (2026-06-06):** re-rig the 4 new meshes through **AccuRIG** (CC_Base
auto-rig) and re-export, then import Humanoid.

## Hand-back recipe (when the AccuRIG'd FBX are dropped into Resources/Heroes/)
1. Replace `Cleric/Knight/Mage/Ranger.fbx` with the AccuRIG'd exports (T-pose,
   no weapon). `HeroFbxImporter` auto-applies Read/Write + Humanoid + avatar on import.
2. CLI runs (editor closed):
   - `DeNelle.Editor.HeroFbxImporter.FixHeroFbx` — reimports + logs `humanoidAvatar(valid,human=True)`
     per hero (must read **human=True** now); also logs mesh Y for scale sanity.
   - `DeNelle.Editor.HeroAnimatorFactory.BuildAll` — controllers already retarget the library.
   - CompileGate + Windows build-verify.
3. Then playtest: heroes stand upright, animate (idle/walk/run + attack/cast/hit/death
   from WO-285), real textures, scaled to the in-scene NPCs.

Pre-swap rigged originals (known-good fallback if AccuRIG stalls):
`Backups/hero_fbx_20260606_005717/`.

## Acceptance status
- [x] No `isReadable` errors / grounding exception (Read/Write ON).
- [x] Ranger green fallback fixed (diffuse repointed).
- [ ] Upright + animate (not T-pose) — **pending AccuRIG re-rig** (owner).
- [ ] Scale matched to NPCs — verify after the rig lands (NormalizeHeight needs upright).
- [x] AssetPostprocessor added + brace-checked; CompileGate OK.
