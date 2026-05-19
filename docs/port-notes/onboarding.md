# Onboarding — first-run tutorial (audit P0-11 / §2.5)

The missing-components audit (`docs/audit/missing-components.md` §2.5, gap
P0-11) flagged the Onboarding module as "built only as far as the cinematics":
the cold open plays, but there was **no first-run teaching**, and
`GameState.Onboarded` was "never set to true in normal play" — so the cold-open
intro cinematic re-played on **every launch**. This note records the first-run
tutorial that closes that gap.

## Files

- `Assets/_Modules/Onboarding/UI/TutorialOverlay.uxml` — the coach-mark overlay
  layout: a dimming scrim + one bottom-pinned card (caption, narrated copy,
  progress readout, Skip + Next controls).
- `Assets/_Modules/Onboarding/UI/TutorialOverlay.uss` — styling, matching the
  project UI Toolkit visual language (`VillageHud.uss` / `BuildMenu.uss`: dark
  Heart-Forest palette, violet Heart accent, amber CTA).
- `Assets/_Modules/Onboarding/OnboardingFlow.cs` — the `MonoBehaviour`
  controller, namespace `DeNelle.Onboarding`. Also the `TutorialController`
  asked for in the deliverable (named `OnboardingFlow` to match the module).

No `.asmdef` change was needed — `DeNelle.Onboarding` already references
`DeNelle.Core` + `UniTask`, and `UnityEngine.UIElements` / `UnityEngine.Events`
are engine modules. The tutorial adds **no new module reference** (see Module
isolation below).

No `.meta` files were hand-authored for the `.cs` / `.uxml` / `.uss` — Unity
generates those on import.

## The tutorial flow

A short, **skippable** five-beat guided sequence, shown the first time the
player reaches the village:

| Beat | Caption | Teaches | Advance |
| --- | --- | --- | --- |
| 1 | WELCOME, KEEPER | You are the Keeper; the realm is yours to hold | Next |
| 2 | THE HEART | Elarion — the Heart — is what you defend | Next |
| 3 | RAISE A TOWER | Open the Build menu, raise a tower | Next opens the Build menu; auto-advances when a tower is built |
| 4 | YOUR WARDENS | Station a starter pet at a slot | Next; auto-advances when a pet is placed |
| 5 | HOLD THE LINE | Wave 1 begins | "Begin Wave 1" (amber CTA) |

Every beat's narrated body copy is a **canon string** resolved at runtime from
`StreamingAssets/Data/Canonical/en.json` (keys `tutorial.steps.1`, `.2`, `.3`,
`.6`, `.5` — all already present in that file, verbatim from the React
project's story content) — never typed inline (v2 port-spec Part 4). Only the
short UI kicker captions (not narrative copy) live in C#.

`Skip tutorial` is available on **every** beat. The tutorial is concise by
design: five beats, one card, no modal-stacking.

## The `Onboarded` flag — read and set

**Read (the gate).** `OnboardingFlow.ShouldRun` reads
`GameStateService.Instance.State.Onboarded`:

- `Onboarded == false` (a brand-new save) → the tutorial runs.
- `Onboarded == true` (a returning player) → the tutorial does **not** run;
  `OnboardingFlow.TryRun()` raises `TutorialClosed` immediately so the
  integrator's "tutorial done" continuation still fires.
- No `GameStateService` yet → treated as a first launch (a new player is never
  silently denied the tutorial).

This mirrors the gate `StoryIntroController.ShouldAutoPlay` already uses for the
cold open, so the two first-run surfaces stay in lock-step.

**Set (the persist).** `OnboardingFlow.Finish()` runs on the **last beat's
advance OR on Skip at any beat** and calls `GameStateService.FinishOnboarding()`
— the existing Core mutator that sets `Onboarded = true`, raises `PlayerChanged`
and `Save()`s to `PlayerPrefs['dotr-save']`. After that the tutorial never
replays.

## The cold-open replay fix (the explicit audit bug)

`StoryIntroController.ShouldAutoPlay` already returned `!Onboarded`. The cold
open re-played every launch **only because nothing ever flipped the flag** —
the audit's exact diagnosis. `OnboardingFlow.Finish()` is the missing flip:
it calls `GameStateService.FinishOnboarding()` on completion **or** skip, which
persists `Onboarded = true`. After the first run, `ShouldAutoPlay` correctly
returns false and the cold open is skipped.

**No change to `StoryIntroController` was required** — the gate logic was
already correct; it just had nothing upstream setting the flag. The fix is the
new flow, not an edit to the cinematic.

## Module isolation (v2 port-spec Part 2) — the defensible call

`DeNelle.Onboarding` references `DeNelle.Core` only. It does **not** reference
`DeNelle.Village` or `DeNelle.HUD`, so `OnboardingFlow` **cannot** call
`BuildMenu.Open()`, read `WaveManager`, or place a pet directly.

The call: the tutorial is a **passive coach-mark display**, modelled exactly on
`VillageHudController` (which the HUD port-note records as the precedent — the
HUD never reaches into `DeNelle.Village` either). Gameplay is reached through
**Core types + UnityEvents**, never a gameplay-module reference:

- **Tutorial → gameplay** — `UnityEvent`s the integrator hooks:
  `OpenBuildMenuRequested`, `BeginWaveRequested`, `TutorialClosed`.
- **Gameplay → tutorial** — `Notify*` methods the integrator calls from
  gameplay events: `NotifyTowerBuilt()`, `NotifyPetPlaced()`. These auto-advance
  the action-beats when the player actually does the thing (not just on Next),
  and are harmless no-ops when off-beat, so they can be wired unconditionally.

Adding `DeNelle.Village` to the Onboarding asmdef would have let the flow call
`BuildMenu.Open()` directly, but it would couple the onboarding module to a
gameplay module and invert the established dependency direction (gameplay
depends on Core; Onboarding depends on Core; they do not depend on each other).
The UnityEvent seam keeps the module graph clean and is the same call the HUD
module already made and recorded — so this is consistent, not novel.

## Integrator wiring (the tutorial does NOT do this itself)

The village scene builder / `VillageController` owns every connection — see the
INTEGRATOR NOTES block at the foot of `OnboardingFlow.cs` for the copy-paste
snippets. In short:

1. Add a `UIDocument` to a tutorial GameObject in the **Village** scene; source
   `TutorialOverlay.uxml`, panel settings = the Onboarding panel settings, sort
   order **above** the `VillageHud` document so coach-marks paint over the HUD.
   Add `OnboardingFlow` beside it.
2. Leave `_runOnStart = true` — `OnboardingFlow.Start()` does the `Onboarded`
   check itself; the tutorial shows only on a first run.
3. Wire tutorial → gameplay:
   - `flow.OpenBuildMenuRequested.AddListener(buildMenu.Open);`
   - `flow.BeginWaveRequested.AddListener(() => waveManager.BeginLoop().Forget());`
   - `flow.TutorialClosed.AddListener(villageController.OnOnboardingClosed);`
4. Wire gameplay → tutorial:
   - `buildMenu.BuildingPlaced += (_, _) => flow.NotifyTowerBuilt();`
     (`BuildMenu.BuildingPlaced` already exists.)
   - `flow.NotifyPetPlaced()` from the pet-placement path — `PetDeployer` has
     `DeployStarterPets()` / `ClearDeployed()` but **no per-pet placed event
     yet**; the integrator should add one, or call `NotifyPetPlaced()` from
     wherever the player stations a starter Warden.
5. **Hold Wave 1 for a first run** — do not auto-call `WaveManager.BeginLoop()`
   in the village `Start()`; let `BeginWaveRequested` be the sole kickoff so a
   first-run player is taught before the dark arrives. A returning player gets
   `TutorialClosed` raised immediately, so start the loop from that listener
   instead.

## Known follow-ups / out of scope

- **Pet-creation flow** — audit §2.5 also mentions a missing pet-creation /
  selection step (the React `pet creation`). The five-beat tutorial *teaches*
  pet placement but does not add a pet-creation UI; the three starter pets are
  still deployed by `PetDeployer.DeployStarterPets()`. Pet creation is a
  separate piece of scope and is left as a follow-up.
- **`PetDeployer` placed-event** — needs a small public hook (event or callback)
  so beat 4 auto-advances; noted above and in the in-file integrator block.
- **Replay-tutorial option** — `OnboardingFlow.Run()` is public and bypasses the
  `Onboarded` gate, so a future settings "Replay tutorial" item can re-show the
  sequence on demand. No settings menu exists yet (audit P0-8).
