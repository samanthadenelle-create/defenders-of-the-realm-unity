# WO-1403 RESULT - Raid Deploy at zero troops: TRAIN TROOPS, one door, no live assault

**Status:** FIXED 2026-09-05 (gated; device build follows tonight)

## What landed
- `Assets/_Modules/Village/Hero/RaidDeployVM.cs` - `Readiness` = the ONE `ArmyReadiness.Compute` snapshot (injected by the View from the live save, null-army arm for fixtures); `Fielded => Readiness.DeployableSlots`; `ShowAssault`, `PrimaryCtaLabel` (`TRAIN TROOPS` at zero, `BEGIN ASSAULT` otherwise); spoils line through WO-1402's `RaidSelectionVM.EstimateSpoils`/`FormatSpoils` (no second formula); deliberately NOT bound to `Snapshot.Ready` so the first-raid soft gate stays at the ONE door (RaidEntryGate / RaidSelectionScreen).
- `Assets/_Modules/Village/Hero/RaidDeployScreen.cs` - `OpenInternal` takes the snapshot once (`ArmyReadiness.Compute(st)`, traced); footer: BEGIN ASSAULT not drawn at zero, `TRAIN TROOPS` primary, `EDIT ARMY` secondary (`Army Ready?` gone), one `PanelRouter.Open(PanelId.Manage, "Troops")` door; `Deploy()` refuses at `Fielded <= 0` with a `FlowTrace.Warn`; "Assault to recon" -> "Scout the camp"; header stats one per line; trace `deploy footer fielded=<n> primary=<label> required=<n> ready=<bool>`.
- New `Assets/Editor/Regression/RaidDeployZeroArmyRegression.cs` (5 cases, RED recipes in the header, comment-stripped scan), registered in `DataRegression.cs`.

## Evidence
- `COMPILE_GATE_OK` (`Builds/c3`, 21:43); `REGRESSION_OK 385/385` (`Builds/r3`, 21:45): `[raid-deploy-zero-army] OK`, `FirstRaidSoftGateRegression` green (its `ArmyReadiness.Compute` pin is satisfied by the real call at `RaidDeployScreen.cs:118`, not a mention).
- The first gate run (`Builds/r2`) was RED on exactly that pin - the lane had removed the call; fixed by routing readiness through the snapshot.

## Capture finding and fix
First capture (`Builds/cap2`): SCOUT REPORT line 4 clipped (`...~1100 iron,` at 1920, `...~22` at 2670). Fix:
`SpoilsPrefix` "Spoils if you win: " -> "Spoils: " (aliased to `RaidSelectionVM.SpoilsPrefix`, one producer) and the
report block widened 0.08-0.92 -> 0.05-0.96 of the well; longest live line 42 chars vs a measured ~45 capacity;
`RaidDeployZeroArmyRegression` gains a `WellCharBudget = 45` pin over all four shipped camps (RED at the old prefix).
Recapture (`Builds/cap3`, 22:18): `RaidDeploy_1920x1080.png` opened - line 4 ends in "gold", no fifth line.

## Deviations (owner's call)
- Header dropped the `Troops N` line rather than relaying it (it is already said by `Army: N / M slots` and `you field N`); the band math did not fit four scout lines + enemy well + guide.
- The Echo guide quote's two-line authoring lives in `EchoGuideService`, outside this ticket's files - not done.
- `Fielded` is slot-weighted (DeployableSlots) while the WO-1389 compare line keeps a headcount; they agree for 1-slot troops and at zero.
- Headless/AutoPilot deploy with a null GameState now shows TRAIN TROOPS (0 slots) - any fleet flow that tapped BEGIN ASSAULT with no army needs an army fixture.
- Owner felt-test on the tester build closes it.
