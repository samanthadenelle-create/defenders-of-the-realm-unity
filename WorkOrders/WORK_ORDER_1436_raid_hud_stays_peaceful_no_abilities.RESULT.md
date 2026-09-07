# WO-1436 RESULT - a raid scene declares combat, so the HUD takes the battle posture

**Status:** FIXED - ON THE SEEKER `2026.09.07.358574` (installed 2026-09-06 19:20). Awaiting the owner's
felt-verify (ability faces tappable in a raid) and a headless raid capture for AC3.
**Commit:** `5bc5025f5` (WO-1437, "the raid can be left, declares combat, and stops wearing the town HUD"). The
work closed this ticket's AC1/AC2/AC4 and the Status was never flipped; this RESULT closes the gap after a
read-only re-verification at source on 2026-09-06.
**Gates on fresh logs postdating the commit:** `COMPILE_GATE_OK` (`Builds/cg-final.log` 18:48),
`REGRESSION_OK 414/414` (`Builds/reg-final2.log` 18:50).

## Acceptance, verified at source

| AC | State | Proof |
|---|---|---|
| 1 raid resolves to COMBAT posture, pinned | CLOSED | `Assets/_Modules/Core/HubScenes.cs:193` `SceneDeclaresCombat(s) => IsRaid(s)`; consumed as the `combat` input at `Core/HudModel/HudContextResolver.cs:62-73`; `HUD/Kit/PostureEvaluator.cs:130` maps `HudContext.Battle -> HostileActiveBattle`; asserted by `Editor/Regression/ScenePostureSeamRegression.cs` (`SceneKind.Raid`). |
| 2 seam oracle over the whole build list | CLOSED | `ScenePostureSeamRegression.cs`, registered `DataRegression.cs:505`; enumerates `EditorBuildSettings.scenes`; `SceneKind.Unknown` and an empty list are hard FAILs. Hand-run over all 28 enabled scenes: none falls through. |
| 3 ability faces tappable, PNG opened | OPEN | needs a headless raid run (`--scene=RaidBase_raider_camp_small`) on the post-fix build. |
| 4 the modal freeze is not billed | CLOSED | `Village/Troops/RaidScoring.cs:715` `_elapsed += Time.deltaTime` (scaled), so `timeScale=0` does not accrue. |
| 5 `REGRESSION_OK` | CLOSED | 414/414 on `reg-final2.log`. |

The proving FlowTrace already carries the new input: `Village/HUD/HudContextEvaluator.cs:128` emits
`context inputs: sceneCombat=... wave=... battleLock=... pursuit=...`, so the next device capture is unambiguous.

## Findings carried forward (no edit made)
- Ticket line citations moved: `RaidDeployController.cs:868/:865` are now `:1028`/`:1063`; `HudAreasHost.cs:135`
  is now `:142-143`. Bar and status bands both derive from `HudLayoutBands.StackAboveThumbBand`; nothing re-enters
  the thumb band.
- `ScenePostureSeamRegression`'s `SceneKind.Overworld` case is unreachable today: `Main_Castle_Overworld` is in
  `HubScenes.Names` (`HubScenes.cs:25`) and `Classify` tests `IsHub` first (`:176-177`), so it asserts as Hub and
  still passes. Dead by ordering, not a defect; do not "fix" it without reading that.
- `Garrison_village2_stronghold` classifies only because `Contains` is ordinal case-sensitive (`HubScenes.cs:42`);
  fragile, not broken.
- §8.3 items (ToastZone inside the deploy band; the move stick under the moved bar) remain raised, not taken,
  pending an owner ruling.
