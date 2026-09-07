# WO-1517 RESULT - the train/army screens now say queue full, army full, and upgradeable

**Status:** IMPLEMENTED - 2026-09-06, uncommitted, awaiting the Unity gate.
**Lane:** edit-only. Files: `ManageScreenVM.cs`, `ManageTroopsTrainDoorRegression.cs`. The renderer
needed no change - every face used here already exists in `ManageScreenPanel`.

## WHAT LANDED (verified at source this session). Line numbers are `ManageScreenVM.cs`.

1. **ARMY FULL is a fact, not a footnote.** `FillTrainFacts` (`:2173`) reads
   `ArmyReadiness.Compute(state)` (`:2204`) + `TroopDialogueCommands.SlotOf` (`:2205`) - the SAME
   formula and slot reader `BarracksService.EnqueueTraining` seeds its own refusal from, and tests the
   ARMY CAP **before** the line depth (`:2213` vs `:2219`), matching the service's own order
   (reasoning in-code at `:2196-2199`). New `TroopChoiceVM` fields: `ArmyFull`, `ArmyFullText`,
   `QueueFullText`, `ArmyUsedSlots`, `ArmyCapSlots`.
2. **QUEUE FULL** at `:2222`: `"Training line full . <depth>/<cap> queued"`, from
   `BuildTimerService.QueueDepth` / `QueueDepthLimit` - the queue's own numbers, never recomputed.
3. **The upgrade word.** `FillUpgradeFacts` (`:2245`) composes `UpgradeWord` as exactly one of
   `UPGRADE AVAILABLE` (`:2299`) / `MAX` (`:2254`) / `UPGRADING` / `NEEDS <blocker>` (`:2290`), ASKED
   of `BarracksService.CanUpgradeTroop` (`:2288`), never re-derived. The Research LINE being full is
   excluded (`:2284`): that is the queue's state, not the troop's.
4. **Tile precedence** (`:4342-4356`): UPGRADING -> ARMY FULL (`:4347`) -> QUEUE FULL (`:4348`) ->
   the upgrade word -> TRAINABLE -> MAX -> IDLE, matching the service's refusal order.
5. **TRAIN is refused with its reason** as `QueueBlocked` + `Route.None`, never a silent no-op.
6. **The detail card (section 1B).** `TroopStatRows` (`:4778`, called `:4594`) emits Health / Damage /
   Range / Speed with a `DeltaText` at level+1 from `TroopStatResolver.Effective` - the resolver
   `TroopDeployer` applies to the live unit - plus train time. `TwoFacts` now takes its LABELS from the
   caller, retiring the hardcoded `Next` / `Time` on the Research and Build cards too. `ComposeDetail`
   seats the Upgrade action in the card's SECONDARY face slot: always composed, never painted, because
   `ProjectSelection` fills that slot from `ActionOf(Cancel)` and a troop has none.
7. **The green arrow** leaves any refused tile - `ProjectAffordanceTile` (`:3967`) at `:4217`.

## MEASURED CASES (`Assets/Editor/Regression/ManageTroopsTrainDoorRegression.cs`)
- **case 8** (`:318`, called `:252`): every unlocked troop's detail card - row count >= 5, every row
  labelled, the retired `Next`/`Time` labels banned by name (`:382`), a Health row, a level+1 delta
  wherever a next level exists, the UPGRADE face with its time on it whenever `UpgradeReady`.
- **case 9** (`:440`, called `:297`): fills the army through `BarracksProgression.GrantTrainedTroop` -
  the grant path a completed job uses - then asserts `ArmyFull`, `TrainReady=false`, a `TrainStateText`
  naming the army, an `ARMY FULL` tile word, and a TRAIN tap that enqueues nothing while leaving the
  reason as the notice.
- **case 10 - ADDED BY THIS LANE** (`:568`, called `:293`). Acceptance line 1 wanted a case per string
  and only ARMY FULL was covered. Half A: the upgrade-word vocabulary is CLOSED to the four words,
  never empty for an unlocked troop, a `NEEDS ` word actually names a blocker, and the word reaches the
  TILE. Half B: fills the Train line through `BarracksService.EnqueueTraining` until `IsLineFull`, then
  asserts `TrainReady=false`, a `QueueFullText` carrying digits, and a tile reading exactly
  `QUEUE FULL`. Runs BEFORE case 9 (case 9 caps the roster, and ArmyFull leads QueueFull). Every arm
  FAILS rather than skips.

## GATE HYGIENE / REGISTRATION
`ManageScreenVM.cs` braces 418/418 NUL 0. `ManageTroopsTrainDoorRegression.cs` braces 174/174 NUL 0;
the region this lane added is pure ASCII. No `.cs` written through a shell redirect.
No `DataRegression.cs` edit needed - the suite is ALREADY registered in HEAD at `DataRegression.cs:438`
(`[manage-train-door]`), and case 10 rides that existing entry point.

## OWED
- `COMPILE_GATE_OK` + `REGRESSION_OK n/n` on a fresh log. **Case 10 is UNRUN** - it is the only code
  this lane authored and no Unity run has exercised it.
- Headless `ManageFlow_ARMY_*` PNGs, opened (acceptance line 6). Owner device felt-verify + close.

## CONTRADICTION
Acceptance line 3 asks for stats "for EVERY troop id"; case 8 walks every **unlocked** troop. A locked
troop composes no detail card at all, so "every troop id" is unmeasurable as written. Needs either the
reading case 8 takes, or a ruling that locked troops carry a stat preview too.
