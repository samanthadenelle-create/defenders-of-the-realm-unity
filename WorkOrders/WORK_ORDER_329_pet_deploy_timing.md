**Status:** CLOSED — owner range sweep 2026-08-21 (WO 0-800): completed or immaterial.

# WORK ORDER 329 — Pet deploy timing (pick the canonical trigger)

**Status: SPEC / revisit** — stopgap shipped, root design call pending. **Lane:** 12 (Onboarding) / 6 (Pets).
**Origin:** 2026-06-07 roundtable — pet stopped appearing after the intro/onboarding was changed to a
Yarn click-through. Root cause: `<<spawn_starting_pet>>` lived only in `CompanionMeeting.yarn`'s
`PetIntroduction` node, which the new click-through doesn't reliably reach; and the Yarn command
`DialogueCommandBridge.spawn_starting_pet` no-ops (with a log) if no `PetDeployer` is in the scene.

## Stopgap shipped (this session — TO BE CONFIRMED by playtest)
- `CompanionMeeting.yarn`: `<<spawn_starting_pet>>` added at **tutorial START** (entry node) **and**
  **tutorial END** (`TutorialComplete`, before `<<save_game>>`). `DeployStarterPets()` clears+redeploys
  (idempotent), so multiple calls = no double pets. `DeployStarterPets` already falls back to the
  ice-wolf when `GameState.StarterPetId` is unset, so a pet appears even with no pet-select step.
- **Confirm:** reset onboarding flag → load UI → start New Game → run the click-through → a pet should
  be present + roaming (PetHeroLeash idle-trail).

## Revisit (the real decision)
1. **One canonical trigger** — having it at start + end + PetIntroduction is belt-and-suspenders. Decide the
   single source of truth and remove the redundant calls once verified.
2. **PetDeployer presence/scene + load order** — `spawn_starting_pet` requires a `PetDeployer` in the
   ACTIVE scene. Confirm which scene the tutorial runs in vs. where the village `PetDeployer` (built with
   `_autoDeployOnStart=true` by VillageSceneBuilder) lives. If the tutorial runs before the village scene
   is active, the command finds no deployer → no pet. Options: ensure the deployer exists at tutorial time
   (the bridge could create one, like `PatriciaLightController` does), or gate the spawn to after the
   village loads.
3. **Verify the village scene isn't stale** — if `_autoDeployOnStart` isn't actually set on the in-scene
   deployer, a `BuildVillage` rebake restores it (Lane 1, batchmode).

## Notes
- Idle roam is driven by `PetHeroLeash` (sets the Pet's HomePost carrot); `Pet.Update` only self-moves in
  Defend mode, so a missing/!running leash also reads as "pet not moving." Verify the leash is attached on deploy.
- Local WO; numbering per `MASTER_PIPELINES_BACKLOG` (next free 330 after this).

> **OWNER RULING 2026-08-21 (verbal, this session):** CLOSED by an explicit owner sweep of the WHOLE 0-800 RANGE: "I've eyeballed them many times, and all of them are already completed. or immaterial." This is a RANGE close on the owner's direct review, not a per-ticket verdict.
