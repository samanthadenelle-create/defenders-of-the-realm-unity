# WO-1403: Raid Deploy offers BEGIN ASSAULT at zero troops and tells the player to "Visit the Barracks" in a sentence, not a door

**Status:** FIXED 2026-09-05 21:45 - TRAIN TROOPS primary at zero, one Manage door, readiness through the ONE ArmyReadiness snapshot, RaidDeployZeroArmyRegression green, COMPILE_GATE_OK + REGRESSION_OK 385/385; deviations in the RESULT file; device build tonight, owner felt-test closes. *(was: READY TO IMPLEMENT - minted 2026-09-05 from the merged UI review)*

Sprint framing, one line: the owner said "creating reason to raid is big" - a new player's first deploy screen must send them to train, not let them lose.

## Evidence
- `Builds/ui-capture/RaidDeploy_2670x1200.png` (09-05 07:02, post WO-1385/1389) - SEEN (`REVIEW_MERGED.md` row 2).
  Words on screen: `Army: -`, `No troops trained yet. Visit the Barracks.`, a button `ARMY READY?`, a full-size
  live `BEGIN ASSAULT`, header `Est. ~2:30 | Troops 0 | Pow...` (truncated), the Echo quote cut mid-line,
  scout copy `Assault to recon`. The compare line `Garrison: 9 defenders - you field 0` IS present (WO-1389 proven).
  No spoils line anywhere.
- Both reviewers: `REVIEW_A_independent.md` B-2 / B-6, `REVIEW_B_independent.md` B2 / B4.
- CODE: `Assets/_Modules/Village/Hero/RaidDeployScreen.cs:335` and `:797` carry the literal
  `No troops trained yet. Visit the Barracks.`; `:485` `Assault to recon - deploy troops on the field`;
  `:701-717` seat the full-width BEGIN ASSAULT beside ARMY READY?. The Troops door already exists:
  `PanelRouter.Open(PanelId.Manage, "Troops")` is called at `Village/Tutorial/DialogueCommandSink.cs:199`.

## What the player experiences
With no army the loudest button says attack, the sentence says go somewhere else, and neither is a door to
the Barracks. Tapping BEGIN ASSAULT with zero troops is a loss the screen invited.

## Fix shape (one mechanism)
`RaidDeployVM` exposes `Fielded` (already the compare line's source); the screen binds the footer to it:
- `Fielded == 0` -> the primary reads `TRAIN TROOPS` and routes `PanelRouter.Open(PanelId.Manage, "Troops")`;
  BEGIN ASSAULT is not drawn. `Fielded > 0` -> primary is BEGIN ASSAULT as today.
- `ARMY READY?` -> `EDIT ARMY` (a verb, not a question).
- SCOUT REPORT gets line 4 `Spoils if you win: ~600 wood, ~250 iron` from the same producer as WO-1402.
- Header stats one per line (`Recon 2:30` / `Troops 0` / `Power ?`); `Assault to recon` -> `Scout the camp`;
  the Echo quote authored to two lines.

```
SCOUT REPORT                         |  Army 0 / 10
Garrison: 9 defenders - you field 0  |  [ TRAIN TROOPS ]      (fielded == 0)
Spoils if you win: ~600 wood ...     |  [ EDIT ARMY ] [ BEGIN ASSAULT ]   (fielded > 0)
```
Trace: `FlowTrace.Step("Raid", "deploy footer fielded=<n> primary=<TRAIN TROOPS|BEGIN ASSAULT>")`.

## Acceptance
- [ ] RED first: `RaidDeployZeroArmyRegression` - fixture army 0: primary label is `TRAIN TROOPS`, no button
      labelled BEGIN ASSAULT exists, tap routes to Manage with tab `Troops` (trace line); fixture army 3: BEGIN
      ASSAULT present. Fails on the current tree.
- [ ] Headless: `RaidDeploy_2670x1200.png` regenerated at both fixtures, opened; no `...` in header or quote
      (`HudLabelFitRegression`); spoils line present.
- [ ] Device: with no troops, tap Journey > Raids > a camp; TRAIN TROOPS lands on the Troops tab; screencap read.

## Not in scope
Raid balance; the selection rows (WO-1402); the settle screen; the Troops tab content (WO-1405/1406).

## Owner ruling
- Section 2 #2 Zero-army-assault? - written to the default NO (primary becomes TRAIN TROOPS).
- Section 2 #1 Spoils-shown? - default YES (the scout report's line 4).
