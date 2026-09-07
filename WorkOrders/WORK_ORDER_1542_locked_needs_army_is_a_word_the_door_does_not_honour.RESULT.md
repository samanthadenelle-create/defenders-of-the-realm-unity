# WO-1542 RESULT - the army word is a warning, and BEGIN ASSAULT asks once

**Status:** IMPLEMENTED - 2026-09-06 uncommitted, awaiting gate. Edit-only lane; no Unity run, no commit.
Line numbers re-read at source after the last edit (CLAUDE.md 11B).

## WHAT LANDED

1. **The word.** `ArmyLockPrefix = "LOCKED - needs Army "` is RETIRED (retirement note
   `Assets/_Modules/Village/Hero/RaidSelectionVM.cs:150-177`). It is now `ArmyWarnPrefix` (`:179`) +
   `ArmyWarnSuffix` -> `Outmatched - Army N advised`, produced by `ArmyWarnWordFor` (`:354`) /
   `ArmyWarnWord` (`:362`). **Same fact, same producer, same predicate** (garrison BODIES >
   deployable BODIES); only the framing changed, so there is still one producer of the number.
   Rendered at `RaidSelectionScreen.cs:1023`.
2. **The door is UNCHANGED.** `OnCardTapped` still refuses on exactly the escalation lock and
   Heartfire. `RaidDeployVM.CanDeploy` (scene + Build Settings), `ShowAssault` (`Fielded > 0`) and
   `Deploy()` gained **no readiness branch**. WO-1403's decoupling stands; no second gate.
3. **The styling.** An outmatched card keeps **full brightness**, now correct rather than half the
   defect: `dimmed` stays bound to the escalation lock alone (`RaidSelectionScreen.cs:1034`), since
   dimming a camp the player may march on today would say "unavailable" about an open door. The
   warning carries its weight in bold full `Parchment`, not the dim tone (`:1037`; reasoning
   `RaidSelectionVM.cs:349`).
4. **The confirm toast** (appended owner ruling, 22:20). `RaidDeployVM.NeedsOutmatchConfirm` (`:396`),
   `OutmatchToast` and `AcknowledgeOutmatch` compose it (header `:368`); the View shows it in **eight
   lines** before `_vm.Deploy()` (`Assets/_Modules/Village/Hero/RaidDeployScreen.cs:1097`). It **asks
   once and never refuses** - the acknowledgement is latched, so the second tap always marches.

## THE PREDICATE TRAP, CAUGHT

`RaidDeployVM.Fielded` is **slot-weighted**; the grid word compares **headcounts**. Feeding the toast
`Fielded` would have let the grid say Outmatched while the deploy screen stayed silent - the exact
two-producer drift this ticket exists to close. The toast calls
`RaidSelectionVM.OutmatchConfirmToast(_def, DeployableCount)` (`RaidSelectionVM.cs:379`) - the raw
headcount, the same axis WO-1389's "you field N" line uses. Recorded at `RaidDeployVM.cs:382`.

## PIN F AND THE OTHER SUITES ON THIS SEAM (acceptance 3)

- `HeartfireRegression.DoorGateCases` (`:693`) source-lints **`RaidSelectionScreen.cs`** (`:91`, F1-F4)
  and **`RaidDeployController.cs`** (F5). Neither `OnCardTapped`'s gate order/copy/trace nor
  `TryInstall`'s spend and empty-pool `FlowTrace.Fail` was touched, and no `RaidCooldownService` /
  `IsOnCooldown` reference was introduced. **`RaidDeployScreen.cs` - where the confirm lives - is not
  a PIN F file at all.**
- `FirstRaidSoftGateRegression:426` requires `OnDeploy` to contain no `DeployableCount`. The added
  branch reads `_vm.NeedsOutmatchConfirm` / `_vm.OutmatchToast`; the headcount read lives in the VM.
- `RaidDeployLayoutRegression:505` requires `NoteExpeditionTarget` **anywhere in the file**, not
  before a `return`. Unaffected by the early-return confirm branch.

**Voice (the WO-1541 cross-check).** Grid: `Outmatched - Army 9 advised`. Confirm: `Outmatched: 9
defenders against your 8. Tap BEGIN ASSAULT again to march anyway.` One vocabulary, one fact, neither
claiming a refusal.

## ORACLE (acceptance 4) - no `DataRegression.cs` edit

`RaidSelectionSpoilsRegression.CheckArmyLockWord`
(`Assets/Editor/Regression/RaidSelectionSpoilsRegression.cs:277`), **re-pointed rather than deleted**,
holds acceptance 4 from the word's side: `LOCK` in either half of the string FAILS the case, so the
card either does not say LOCKED or the tap answers - and the tap is deliberately unchanged. It also
asserts the exact strings, four army fixtures (0 / 9 / 99 / unknown), **predicate parity between the
grid word and the confirm toast on every camp**, that the toast carries both numbers, and that it does
not read as a refusal. Mutations M1 / M1b / M1c named at `:61`.

## RE-POINTED CALLERS (the coordinator's sweep)

`grep -rn "ArmyLockPrefix|ArmyLockWordFor|ArmyLockWord\b" Assets/ --include=*.cs` now returns **zero
code callers**; survivors are the local method name `CheckArmyLockWord` and two retirement notes. The
66 CS0117/CS1061 errors in `Builds/cg-artpack2.log` (22:38) all came from
`RaidSelectionSpoilsRegression.cs` - the **only** file that ever referenced the removed symbols, and
it is re-pointed. No other suite, module or editor file referenced them.

## OWED

- `COMPILE_GATE_OK` + `REGRESSION_OK <n>/<n>` on fresh logs, judged by the marker.
- **RED-before-green owed, not claimed** (the case cannot compile against the pre-change tree).
- Fresh captures of the grid's new word and of the tap's response, **verified in greyscale**
  (acceptance 2): if the warning at full brightness disappears, give it contrast or weight, never hue.
- Owner felt-verify + close.
