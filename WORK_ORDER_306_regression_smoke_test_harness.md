# WORK ORDER 306 — Regression Smoke-Test Harness (run before push / end of day)

**Status: SPEC — capture now, build when ramping** (owner: "designing ≠ implementing now;
something to think on"). **Lane:** 10 — Build/Deploy/Perf (slot in `MASTER_PIPELINES_BACKLOG`).
**Why:** as the backlog empties and dev velocity climbs toward a full, well-tested **closed
loop**, a regression net is what lets us move fast WITHOUT silently re-breaking it. Today's
landmines were all silent regressions a scripted check would have caught instantly:
- the animation-kit broke the hero rig (T-pose) → a "hero has a valid avatar+controller" test catches it
- the gutted `VillageSceneBuilder` → a "scene boots with zero errors" test catches it
- the `IVillageHud` signature drift (HEAD didn't compile from clean) → a build/interface test catches it

## Tiering
- **Tier 1 (exists):** `CompileGate.Run` → `COMPILE_GATE_OK` (does it compile).
- **Tier 2 (THIS WO):** does it RUN — headless PlayMode + EditMode tests asserting the closed loop.
- Run **both before every push + at end of day.**

## Existing infra to build on (NOT greenfield)
- Unity Test Framework is set up: `Assets/Data/Tests/` (`DeNelle.Data.Tests.asmdef` — catalog/JSON
  integrity tests) + `Assets/_Modules/BattleATB/Tests/` (ATB logic tests). EditMode tests already pass.
- Harness pattern to mirror: `run-unity-method.ps1` (fork-aware batchmode; judge by the result, not
  the wrapper exit code) + `CompileGate.cs`.

## Deliverables
1. **`run-tests.ps1`** — headless `-runTests -testPlatform PlayMode` (and EditMode), writes a results
   XML; success = all-passed in the XML (NOT the exit code — Unity forks, same quirk as the gate).
   Editor must be CLOSED (single-Unity lock, like every batchmode op).
2. **PlayMode "closed-loop" smoke suite** (new test asmdef referencing the runtime asmdefs). First
   high-value tests (each guards a real failure mode):
   - **Boot:** load the village/combat scene → 0 errors/exceptions in the first N frames (catches
     broken/gutted scene builders, missing scripts).
   - **Hero valid:** hero spawns; has a SkinnedMeshRenderer + a Humanoid `Animator` with a controller
     declaring the canonical params (Speed/Attack/Cast/Hit/Dead…) → catches the kit-broke-rig T-pose.
   - **Combat damages:** drive a hero attack near an enemy → the enemy's `IDamageable` HP drops;
     on lethal → enemy removed + `CombatFeedbackManager.Kill` fired (guards the combat hook).
   - **Economy banks:** a `MineNode` extract → `EconomyService` resource total increases (guards the
     harvest→economy routing we just reconciled).
   - **Wave advances:** `WaveManager` countdown → spawn → clear → next wave.
   - **Interface intact:** `CoreServices.Hud` resolves an `IVillageHud`; `SetHeartHp(cur,max)` /
     `SetResources(...)` callable (guards signature drift / HEAD-compile breaks).
3. **Cadence wiring:** document/automate "run `run-tests.ps1` before push + EOD" (optional git
   pre-push hook later; manual to start).

## Phasing (don't boil the ocean)
- **v1:** `run-tests.ps1` + the 3 highest-value smokes (Boot, Hero-valid, Combat-damages).
- **v2:** Economy + Wave + Interface.
- Grow coverage as each loop closes — add a regression test whenever a bug is fixed (the bug becomes a test).

## Caveats
- PlayMode tests are timing/scene-setup sensitive — keep them deterministic + fast (a 2-min suite, not 20).
- Single-Unity constraint: can't run tests while the editor is open (same as the gate/builds).
- Don't over-assert visuals/feel (those stay human playtest) — assert STATE + no-errors, not "looks good."

## Notes
- This pairs with the sole-committer gate discipline: Tier-1 (compile) + Tier-2 (run) = a real
  pre-push quality bar. Local WO (Linear maxed); numbering per `MASTER_PIPELINES_BACKLOG` (next free 327).
