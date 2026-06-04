# WORK ORDER 133 — First-run tutorial / onboarding wiring (FTUE)

**Status:** READY TO IMPLEMENT
**Priority:** P0 — no FTUE at all; cold-open cinematic replays every launch
**Date:** 2026-05-30
**Source:** docs/QA_player_sanity_pass_2026-05-30.md (P0-A; also resolves P2-K)
**Lane:** World/Environment (VillageSceneBuilder) + Onboarding (code)

---

## Symptom

A brand-new player drops straight into the village with **no teaching**. Worse,
because onboarding never completes, `GameState.Onboarded` never flips, so the
**cold-open cinematic replays on every launch** (it is gated on that flag).

---

## Root cause (verified file:line)

`OnboardingFlow` is fully built but **referenced nowhere**. It is a passive,
integrator-driven component that by design cannot reference the Village/HUD
assemblies; the Village scene must wire it.

- `OnboardingFlow.cs` exists at
  `Assets/_Modules/Onboarding/OnboardingFlow.cs`. Its `Finish(bool)` already calls
  `GameStateService.FinishOnboarding()` (sets `Onboarded = true` + Save) and raises
  `BeginWaveRequested` on a completed run (`OnboardingFlow.cs:452-484`).
- Its own **integrator notes** spell out the five seams the Village scene must wire:
  `OpenBuildMenuRequested`, `BeginWaveRequested`, `TutorialClosed`,
  `NotifyTowerBuilt`, `NotifyPetPlaced` (`OnboardingFlow.cs:534-577`).
- **Zero references** exist outside OnboardingFlow itself: a repo search for
  `OnboardingFlow` / `BeginWaveRequested` / `NotifyTowerBuilt` across `Assets`
  returns only `OnboardingFlow.cs` and its own `TutorialOverlay.uxml`/`.uss`
  (`Assets/_Modules/Onboarding/UI/`). Nothing instantiates it.
- `VillageController.Start()` wires gate openers, the Heart HUD bridge and dungeon
  entrances — but **not the tutorial** (`Assets/_Modules/Village/VillageController.cs:141-146`).
- A grep of `VillageSceneBuilder.cs` for `OnboardingFlow` / `TutorialOverlay` returns
  **0** — it is never placed in the scene.
- Because `Finish()` never runs, `Onboarded` stays false forever and the cold open
  re-plays (StoryIntroController gates on `!Onboarded`, per OnboardingFlow's own note
  `OnboardingFlow.cs:571-575`).

---

## Fix (precise)

Place `OnboardingFlow` in the Village scene and wire its five seams per its own
integrator notes (`OnboardingFlow.cs:534-577`).

1. **Place the tutorial object (scene rebake — CLI's job).**
   In `VillageSceneBuilder`, add a tutorial GameObject with a `UIDocument` whose
   sort order is ABOVE the VillageHud document, plus the `OnboardingFlow` component.
   Leave `_runOnStart = true` — `OnboardingFlow.Start()` checks `GameState.Onboarded`
   itself and shows the tutorial only on a first run.
   - **PIPELINE_STATE §8: UXML does NOT render in builds.** `OnboardingFlow` drives
     `TutorialOverlay.uxml`. This will likely come up **empty in the player build** —
     so the overlay must be **code-built** (matching the project's other code-built
     HUDs), not the UXML, before relying on it. If a code-built overlay is needed,
     spec that as part of this WO (build the coach-mark overlay in code; keep the
     UXML only as an editor reference).

2. **Wire requests TO gameplay** (per `OnboardingFlow.cs:549-554`):
   - `flow.OpenBuildMenuRequested.AddListener(buildMenu.Open);`
   - `flow.BeginWaveRequested.AddListener(() => waveManager.BeginLoop().Forget());`
   - `flow.TutorialClosed.AddListener(villageController.OnOnboardingClosed);`
   Use `?.`-guarded resolution for cross-module references (CLAUDE.md §10).

3. **Wire gameplay events TO the tutorial** (per `OnboardingFlow.cs:556-562`):
   - `buildMenu.BuildingPlaced += (_, _) => flow.NotifyTowerBuilt();`
     (`BuildMenu.BuildingPlaced` already exists, `BuildMenu.cs:139`.)
   - `petDeployer.PetPlaced += () => flow.NotifyPetPlaced();`
     (If `PetDeployer` lacks a placed-event, call `NotifyPetPlaced()` from its
     placement path. `NotifyTowerBuilt`/`NotifyPetPlaced` are safe no-ops off-beat —
     wire unconditionally.)

4. **Hold Wave 1 until the tutorial closes** (per `OnboardingFlow.cs:564-569`):
   On a FIRST run, do NOT let the village auto-start the wave loop — let
   `BeginWaveRequested` be the sole kickoff so the player is taught first.
   `WaveManager._autoStart` currently auto-begins the loop in `Start()`
   (`Assets/_Modules/Village/Waves/WaveManager.cs:140-141` per QA P2-K). Gate the
   auto-start on `GameState.Onboarded`: returning players (Onboarded already true)
   get `TutorialClosed` raised immediately by the flow's TryRun and start the loop
   from that listener; first-run players wait for `BeginWaveRequested`. This also
   resolves the "long silent first-wave countdown" (QA P2-K).

5. **Confirm the cold-open fix is automatic.**
   No `StoryIntroController` change is needed (`OnboardingFlow.cs:571-575`):
   `Finish()` persists `Onboarded = true`, after which the cold open is correctly
   skipped. Just verify on a second launch.

---

## Acceptance criteria

- [ ] On a fresh save (`Onboarded == false`), the FTUE overlay appears on entering
      the Village and teaches build → wave, rendering correctly in the **player build**
      (code-built overlay if UXML stays empty).
- [ ] Completing OR skipping the tutorial flips `GameState.Onboarded` to true and
      Save()s it (via `OnboardingFlow.Finish` → `GameStateService.FinishOnboarding`).
- [ ] On the SECOND launch the cold-open cinematic does **not** replay and the
      tutorial does **not** re-show.
- [ ] First-run Wave 1 does NOT auto-start; it begins only when the tutorial raises
      `BeginWaveRequested`. Returning players start the loop normally with no FTUE.
- [ ] `NotifyTowerBuilt` / `NotifyPetPlaced` advance the relevant tutorial beats.
- [ ] `?.` on cross-module calls; brace balance check passes on every `.cs` edited.

## Files to edit

- `Assets/Editor/VillageSceneBuilder.cs` — place OnboardingFlow + tutorial UIDocument;
  gate WaveManager auto-start on Onboarded. **CLI only — requires a scene rebake.**
- `Assets/_Modules/Village/VillageController.cs` — wire the five seams (or a dedicated
  small integrator component the builder attaches), incl. `OnOnboardingClosed`.
- (If needed) a code-built tutorial overlay class under `Assets/_Modules/Onboarding/`
  if `TutorialOverlay.uxml` renders empty in the build (PIPELINE_STATE §8).

## Do NOT touch

- `OnboardingFlow.Finish` / `FinishOnboarding` persistence logic — it is correct;
  only CALL it (CLAUDE.md cross-assembly rule: Onboarding cannot see Village/HUD).
- `StoryIntroController` — the cold-open fix is automatic once `Onboarded` flips.
- Any `.unity` scene file by hand. Placement goes through VillageSceneBuilder + a
  CLI bake (CLAUDE.md §3 — UI does not fire batchmode).

## Cross-dependencies

- **VillageSceneBuilder serialization bottleneck (CLAUDE.md §9)** — coordinate with
  WO-132 (also a builder edit) and WO-125 if it touches the scene: one branch on
  `VillageSceneBuilder.cs` at a time. Consider batching the WO-132 + WO-133 builder
  changes into a single CLI bake.
- **WO-131** edits `BuildMenu.cs` (`OnConfirmBuild`); this WO only listens to
  `BuildMenu.BuildingPlaced` — no conflict, but land WO-131's BuildMenu change first
  if both are in flight to avoid rebasing the listener wiring.
