# WO-1520 RESULT - a computed staging marker and an engagement-gated raid clock; the device retest is the whole remaining proof

**Status:** SOURCE COMPLETE - uncommitted in the working tree as of 2026-09-06 21:45, awaiting the wave-two gate.
**Tree contradicts the ticket:** its Status line still reads `READY TO IMPLEMENT - P0` while the work sits in the
tree. (Status line not edited here - RESULT-only lane.)
**Commit:** none. Edit-only lane.
**Files:** `Assets/Editor/WallTools/RaidBaseGenerator.cs:121-161,375-391`,
`Village/Troops/RaidScoring.cs:139-162,214-219,760-824`, `Village/Troops/RaidDeployController.cs`,
`Village/World/Camps/RaidGarrisonSpawner.cs`, `Assets/Editor/Regression/RaidStagingMarkerRegression.cs` (NEW,
untracked), registered at `Assets/Editor/Regression/DataRegression.cs:684` as the `raid-staging suite`.
**Gates:** none. `Builds/cg-quiet.log` `COMPILE_GATE_OK` is 20:04 and the owner's ruling arrived 20:26, so the gate
predates the lane entirely. `Builds/cg-aab.log` (20:54) is RED - 42x `CS0103`, the Manage lane's half-written
suites (`ManageTroopsTrainDoorRegression.cs(247,17)`, `ManageProgressiveDisclosureRegression.cs(228,41)`).

## 1. The staging marker is COMPUTED, never a literal

`RaidBaseGenerator.PlaceStagingMarker` (called at `:375`) seats the point at
`max(towerReach, defenderReach) + StagingMargin` (`:140`), with the defender perception radius mirrored from the
sensor and a plane-edge inset so it lands on the RaidGround. The builder logs its own measurement at `:391`:
`STAGING @ <pos> (<d>m out, <c>m of clear air, ...)`. **The RED-today proof is stated in the suite's own header**
(`RaidStagingMarkerRegression.cs:15-28`): the legacy seat was `HeroStartPoint_PlayerSpawn` at `-(radius + 8)` =
**-39.00** on `raider_camp_small` (baseRadius 31), while turret fire reaches ~45.5 m from arena centre - the hero
sat **6.5 m inside range on frame one**. Case 2 asserts that exact legacy number is unsafe so it can never return
as "close enough"; cases 1/4/5 pin the three halves of the fix.

## 2. The clock starts on first engagement

`RaidScoring` gates `_elapsed += Time.deltaTime` (`:783`) behind the engagement flag. `IsStaging` is documented at
`:214-215` as "true while the player is still in the staging area ... The HUD reads STAGING - deploy your troops",
and `_engagedReason` (`:219`) is empty while staging. The one permanent trace the ticket demanded is at `:815`:
`FlowTrace.Step("Raid", $"clock started reason={_engagedReason} ...")`, with the staging-end line at `:816` naming
the remaining raid seconds. Spawn accounting still runs while staging (`:760`) because the spawn fills the
destruction denominator - the player is simply not billed time for it (`:771`).

## 3. Acceptance

- [x] A regression measuring staging distance against every defender sensor radius and tower range, with the
      RED-today proof stated in-file - `RaidStagingMarkerRegression.cs:15-28`, cases 1/2/4/5.
- [x] A source case that `_elapsed` cannot advance before the engagement flag is set - `RaidScoring.cs:783`.
- [ ] A captured device raid where `clock started reason=` appears AFTER the first deploy, hero alive at t=0 -
      **OPEN, and load-bearing.** Nothing in the tree proves the seat is outside range in a running scene; the
      generator's assert proves the AUTHORING, not the play.
- [ ] `REGRESSION_OK n/n` on a fresh log - owed.
- [ ] The whole **Easy-camp retest gate** (sec.4, seven clauses incl. median clear 90-140s) - untouched by this
      lane; it is a felt-test the owner runs, and it gates WO-1526 / 1461 / 1527 / 1528.

## 4. Owed

The wave-two gate; then a fresh raid base bake (the marker is authored at generate time - an existing baked scene
still carries the old seat); then one device raid capture read for `clock started reason=` and the hero's t=0 HP.
