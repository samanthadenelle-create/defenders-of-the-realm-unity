# Overnight Queue — 2026-06-06 (hero refresh: animations + routines + addressables)

Owner signed off. One unattended chain: **WO-283 → WO-284 → WO-282**, gated between
each. Run when Unity is free (editor closed — all steps are batchmode). One session
carries all three. Full specs live in the work orders; this file is the run order.

## Hard order: WO-283 → WO-284 → WO-282. Do NOT reorder.
- **283** imports the clip library + builds per-class controllers in `Resources/Heroes/`.
- **284** standardizes the animator params + adds the routine layer that drives the clips
  (idle/walk/hit/death/etc. for all actors); finalizes the controller param set.
- **282** relocates the hero models + finalized controllers into the Addressables
  `Heroes` group and repoints load sites.
Running these out of order means redoing controller params (284 before 283) or moving
controllers twice (282 before 284).

---

## STEP 1 — WO-283: Canonical animation library
Spec: `WORK_ORDER_283_canonical_animation_library.md`. Source: owner upload
`Animations.zip` (162 FBX).
1. Import preserving subfolders → `Assets/Action/{Shared,Knight,Ranger,Wizard,Enemies}/`.
2. Extend `ActionClipImporter.cs` to enforce **Optimal** compression (Humanoid +
   in-place XZ root + loop-on-idle/walk/run already enforced).
3. `Defenders/Animation/Reimport Action Clips (force Humanoid)` →
   `ActionClipImporter.ReimportActionClips`
4. `Defenders/Animation/Fix Action Clip Root Motion (stop slide)` →
   `ActionClipImporter.FixActionClipRootMotion`
5. Update `HeroAnimatorFactory` — add **Cleric** spec (sources `Shared/`+`Wizard/`,
   same as Mage); widen clip lookup to the new subfolders; wire enemy factory to
   `Shared/`+`Enemies/`. Run `HeroAnimatorFactory.BuildAll` (+ enemy equivalent).

## STEP 2 — GATE: `run-unity-method.ps1 -Method DeNelle.Editor.CompileGate.Run`
Marker `COMPILE_GATE_OK`. Brace-check every `.cs` edited (CLAUDE.md §1). Unity closed.
**If the gate fails, STOP — do not start WO-282.** Animation half can still land alone.

## STEP 3 — WO-284: Unified animation routines (all actors)
Spec: `WORK_ORDER_284_unified_animation_routines.md`.
1. Add `AnimParams.cs` + `IActorAnimator.cs` + `ActorAnimator.cs` + enums in
   `DeNelle.Core` (`Assets/_Modules/Core/Combat/`).
2. Migrate the scattered animator-param callers (Enemy, HeroLocomotion, HeroAbilities,
   Pet, PetAnimatorController, DragonBoss, DungeonHero, etc.) to the driver + constants;
   kill the per-class `StringToHash`; fix the `Dead`/`Death` split → canonical `Dead`.
3. Route events → routines: on idle/walk/run, attack/cast, block, hit, death, victory,
   turn, emote — for heroes, enemies, dragon, pets.
4. Finalize the animator-factory controllers so they expose the full `AnimParams` set
   (284 is authoritative over 283 on params).

## STEP 3b — GATE: `CompileGate.Run` → `COMPILE_GATE_OK`. Brace-check every `.cs`.
**If the gate fails, STOP before WO-282.** 283+284 can land together without 282.

## STEP 4 — WO-282: Heroes → Addressables `Heroes` group
Spec: `WORK_ORDER_282_heroes_resources_to_addressables.md`. Ref:
`docs/addressables-implementation-plan.md` (Heroes slice).
1. Create `Heroes` group (On Demand / Remote / LZ4 / Pack Separately).
2. Move hero FBX + the controllers built in STEP 1 out of `Resources/Heroes/` →
   `Assets/Art/Characters/Heroes/`; mark Addressable, address `Heroes/<slug>`.
3. Convert the `Resources.Load("Heroes/...")` call sites (HeroBodySwapper,
   AtbCombatantSwapper, StoryCompanionInjector, PatriciaLight) to async Addressables
   loads + handle release. No hardcoded address strings (use AddressablesGroupConfig).
4. Reconcile `HeroAnimatorFactory` controller output path (no longer Resources).

## STEP 4b — GATE again: `CompileGate.Run` → `COMPILE_GATE_OK`. Brace-check.

## STEP 5 — BUILD: Addressables content build (Heroes group) + player build-verify.
Then `ship-webgl.ps1 -NoBrotli` if shipping to itch. Verify the 4 heroes load + animate
(idle/walk/run + primary attack/cast + hit + death + victory), no T-pose, no slide;
enemy plays injured set + hit/death; pet plays its set.

## STEP 6 — Commit (explicit LFS paths, NOT -A), push.
Write `WORK_ORDER_283_*.RESULT.md`, `WORK_ORDER_284_*.RESULT.md`, and
`WORK_ORDER_282_*.RESULT.md`. UI closes the matching Linear issues on push.

---

## Notes
- Owner chose CLI-side import of the 162 FBX (no mount-sync risk on bulk binaries).
- Cleric + Mage share the Wizard animation set (caster decision).
- Pre-swap hero mesh backups: `Backups/hero_fbx_20260606_005717/`.
- Knight set is large (99 clips) — wire essentials this pass; full combo trees = later WO.
- If WO-282 is genuinely the first Addressables group built, flag in RESULT (owner may
  want towers/pets slices sequenced next to amortize profile/CDN setup).
