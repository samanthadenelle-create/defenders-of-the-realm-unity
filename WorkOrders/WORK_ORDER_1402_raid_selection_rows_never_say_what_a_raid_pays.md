# WO-1402: Raid Selection rows say how hard a camp is and never what a raid PAYS

**Status:** CLOSED 2026-09-07 - owner felt-test PASS (validated 2026-09-07T14:03:06, build 2026.09.07.359076). PRIOR STATUS: FIXED - ON THE SEEKER in build 2026.09.06.357453 (chain 00:31-00:38: APK_OK 463MB, R2_PARITY_OK objects=271; installed 00:41, versionCode 357453 read off dumpsys; Firebase App Distribution release 0kka4h6t9u400); owner felt-test closes 2026-09-05 21:45 - spoils line landed (RaidScoring.EstimateSpoils, ONE producer shared with WO-1403), RaidSelectionSpoilsRegression green, COMPILE_GATE_OK + REGRESSION_OK 385/385; device build tonight, owner felt-test closes. *(was: READY TO IMPLEMENT - minted 2026-09-05 from the merged UI review)*

Sprint framing, one line: the owner said "creating reason to raid is big" - this screen is where that reason must first appear.

## Evidence
- `Builds/ui-capture/RaidSelection_2670x1200.png` (09-05 07:02) - SEEN by the CLI in the merged review
  (`docs/qa/UI_REVIEW_2026-09-05/REVIEW_MERGED.md` row 1). Rows read `Wood walls . 9 defenders` plus a
  difficulty word; no row carries a resource word or number. Three identical gold pips sit on every row and
  vary on none. The left-edge difficulty bars are green / yellow / red with the tier word tinted the same.
- Both reviewers, independently: `REVIEW_A_independent.md` B-1 / B-4, `REVIEW_B_independent.md` B1 / B5.
- CODE: `Assets/_Modules/Village/Hero/RaidSelectionVM.cs:133` `RewardMultiplierFor(id)` reads the camp def's
  `rewardMultiplier` - the camp authors a MULTIPLIER, not a loot list. The real spoils are computed once, at
  settle: `Assets/_Modules/Village/UI/EndState/EndStateVM.cs:238-246` fills `vm.Spoils` (SpoilRowVM). That is
  the one producer the preview must reuse; there is no second loot table to author.

## What the player experiences
Four camps, each described only by what will hurt (walls, defenders, difficulty). Nothing on the screen says
what a win is worth, and nothing compares the camp to the army the player has. The rational tap is BACK.

## Fix shape (one mechanism)
Right column, line 2 of every row: `Spoils: ~600 wood, ~250 iron` - a RANGE/estimate, never exact, produced
by one pure function shared with `EndStateVM` (base spoils x `rewardMultiplier`) and exposed on
`RaidSelectionVM` as a string per id; the screen (`RaidSelectionScreen.cs`) only renders it. The pips are
hidden until star ratings actually vary (no data today -> not drawn). When the camp's garrison exceeds the
player's deployable army (`PostureSignals.SetArmyFill` used/cap, producer `BuildTimerService.PublishArmyStatus`),
the row carries the WORD `LOCKED - needs Army N`; the bar stays but never carries state alone.

```
[ The Forsaken Camp        Easy   ]  Wood walls . 9 defenders
[                                 ]  Spoils: ~600 wood, ~250 iron
[ Ashen Hold                Hard  ]  Stone walls . 14 defenders
[                                 ]  Spoils: ~1400 wood, ~600 iron   LOCKED - needs Army 6
```
Kit primitives only (`ElarionUiKit.Label`), MVVM (VM owns the strings), words never hue.
Trace once per built row: `FlowTrace.Step("Raid", "selection row id=<id> spoils='<text>' locked=<bool>")`.

## Acceptance
- [ ] RED first: `RaidSelectionSpoilsRegression` - every row VM exposes a non-empty spoils string containing
      a resource word; the same function feeds `EndStateVM` (one producer); a fixture with army 0 yields the
      `LOCKED - needs Army N` word on any camp with garrison > 0. Fails on the current tree.
- [ ] Headless: `RaidSelection_2670x1200.png` regenerated (`UI_CAPTURE_OK`), opened: spoils line on every row,
      no pips, lock word present on the fixture's over-army camps; `HudLabelFitRegression` green (no `...`).
- [ ] Device: open Journey > Raids; the four rows read spoils and one reads the lock word; screencap read.

## Not in scope
Balance of the spoils numbers (tunables, retune on the rail); the deploy screen (WO-1403); the settle screen
capture gap; the Journey card subtitle (WO-1404).

## Owner ruling
- Section 2 #1 Spoils-shown? - written to the default YES (a range, never exact).
