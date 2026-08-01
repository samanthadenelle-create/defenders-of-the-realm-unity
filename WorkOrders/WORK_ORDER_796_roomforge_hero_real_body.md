# WORK ORDER 796 — Room-Forge composed dungeons bake a REAL hero body (not a pill)

**Status: SHIPPED 2026-08-01 (fb358585 — Room-Forge hero real body, in the audit ship-now trio).**
**Origin:** owner F8 seq 461 (dg_starter_loop): "I am a pill". Classification: NEW FEATURE —
no body-swap was ever wired on this scene path (QA triage 2026-07-30, read-only).
**Proof from capture:** `[Flow:HeroDrift] baseNt=NaN clips=[<none>]` — NaN is only possible
with NO Animator anywhere on the hero (HeroLocomotion.cs:997-1003) = bare primitive.
**NOT WO-782** (that is Bryn/mini-boss standees in Dungeon_HealersCottage via
DungeonSceneBuilder — different scene, builder, objects).

## Root

`Assets/Editor/RoomForge/DungeonBaker.cs:411-412` — `PopulateForPlay()` creates
`GameObject.CreatePrimitive(PrimitiveType.Capsule)` named "Hero (Blaise)" with ONLY
HeroLocomotion attached. Its comment claims it mirrors HeroControlEnsurer.SpawnEmergencyHero;
it does not — the ensurer builds an empty root + a child named `HeroBody` + HeroBodySwapper
(HeroControlEnsurer.cs:408-452). Runtime never repairs it: the found-existing-hero branch
(HeroControlEnsurer.cs:227-273) never adds HeroBodySwapper, and dg_* scenes fail the
IsVillageScene gate anyway.

## Change (DungeonBaker.PopulateForPlay, ~:400-427)

1. Hero root = EMPTY GameObject("Hero (Blaise)"), tagged Player, top-level (DedupeHeroes
   reasoning at :409-410 still holds).
2. Child capsule renamed `HeroBody`, collider stripped (keep :415-416), material via
   MagentaGuard.BuildUrpLitMaterial (never Shader.Find("Standard")).
3. Attach DeNelle.Village.HeroBodySwapper by the same FindType reflection idiom as
   HeroLocomotion at :417-420; FlowTrace.Warn on unresolved type.
4. Re-bake dg_starter_loop.unity — **editor-closed batchmode, in an ISOLATED WORKTREE**
   (memory: DungeonCompose .unity NUL-corrupts when re-baked in the shared tree).

## Acceptance

- [ ] Headless dg_starter_loop run logs `[Flow:HeroBody]` Blink base-load lines (absent today).
- [ ] `[Flow:HeroDrift]` shows numeric baseNt + real clips (not NaN/<none>).
- [ ] Screenshot: rigged body, no capsule renderer on the root.
- [ ] COMPILE_GATE_OK + REGRESSION_OK; scene diff reviewed before commit (NUL guard).
