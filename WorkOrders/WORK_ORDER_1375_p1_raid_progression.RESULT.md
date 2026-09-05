# RESULT - WORK ORDER 1375 - P1: give raids progression

**Filed:** 2026-09-04 (board agent, from the lead's measured facts)
**WO status:** FIXED - in build 2026.09.05.355872, awaiting owner felt-test. PO closes.

## Provenance - who built what

The runtime for this ticket was **committed earlier today by another seat in `1ef5f6ad4`**
(Lanes B, C and E: victory settle + `GameState.RaidVictories`, Season Pass contract
`BattlePassService.OnRaidResult` / `RaidXpFor`, the 0/3/10/20 unlock ladder and the four named
targets). Tonight did NOT author that runtime. Tonight **PROVED it**: its suites were registered
and ran GREEN, one real defect in it was fixed, and the tree was cut to the build on her device.

## What shipped

- **Clear-count unlock ladder 0 / 3 / 10 / 20 victories** - `scene-configs.json` `unlockVictories`
  at lines 116/181/238/295 (Forsaken Camp / Broken Garrison / Veiled Enclave / Iron Bastion);
  `RaidSelectionVM` locks and unlocks by `GameState.RaidVictories`, and the card tap refuses a locked
  camp with the same sentence the card prints (`RaidSelectionScreen.cs:506-519`).
- **Increasing loot** - gold base rises per camp 2200 / 3100 / 4500 / 6500; wood/iron/food still
  scale on the camp `rewardMultiplier`.
- **Raid XP feeds the Season Pass** - +50 completed, +25 three-star, +25 hundred-percent, +100
  first clear (once per target); a loss pays nothing. **Tonight's fix, IN this build:**
  `ArenaOutcomeRelay` now carries `win` to `BattlePassService.OnRaidResult` (a LOST raid used to
  credit season XP as a win) and `BattleMonthlyPanelsBootstrap` registers the raid handler.
- **Victory counter + payout visible** - `RaidVictories` round-trips the save wire and is backfilled
  from claimed camps; `EndStateVM` shows one spoil row per non-zero currency.

## NOT delivered on this ticket - stated so the FIXED line is honest

- **Iron Bastion is NOT enabled.** The target is named, described (*"The Heart remembers no fortress
  here."*) and sits at rung 20, but `[raid-escalation]` asserts *"no seatless raid scene enabled"* -
  `RaidBase_IronBastion` has no HeroStartPoint yet (commit `1ef5f6ad4` body). A player reaching 20
  victories will find the card, not the scene. That remains open.
- **Raid charges stacking to 3** is Heartfire = **WO-1379**, which stays READY: `[heartfire]` is
  green, but `RaidSelectionScreen.cs:527` still gates on the per-camp cooldown. Not claimed here.
- The "first-win daily bonus" is not separately evidenced by a suite line read this session; the
  `+100 first clear` is a Season XP term, once per target, not a daily.

## Which suites prove it

- `[raid-escalation]` (`RaidEscalationRegression`, `DataRegression.cs:1332`) - *"four targets
  authored 0/3/10/20 with the canon names + card lines, twins byte-identical, no superseded name
  survives, every raid sceneName registered, no seatless raid scene enabled, and the VM
  locks/unlocks by victory count"*.
- `[raid-season-xp]` (`DataRegression.cs:1333`) - the section-6 table, first-clear bonus takeable
  once per target, the raid outcome door wired end to end.
- `[raid-payout-visibility]` (`DataRegression.cs:1331`) - one spoil row per credited currency, the
  monotonic `RaidVictories` counter round-trips the save.
- `[raid-gold-arrow]` (`DataRegression.cs:1330`) - the per-camp gold base ladder.
- All four were among the ten suites registered tonight and GREEN on pass 2.

## Build + install evidence (lead's measurements, 2026-09-04)

- Build `2026.09.05.355872` (versionCode 355872) installed on the owner's Seeker 2026-09-04 22:22:13
  via `install-apk-to-seeker.ps1` (`Success`; `adb shell dumpsys package` versionName=2026.09.05.355872).
- Chain markers: `SCHEMA_PARITY_OK`, `APK_OK` (461 MB), `R2_PUSH_OK` (catalog_2026.09.05.355872.bin/.hash
  uploaded), `R2_PARITY_OK targets=Android,StandaloneWindows64,WebGL objects=271`, `APK_DONE`.
- Regression on the same tree: pass 2 = 375/377; the two reds were in TEST code, fixed; **final pass
  pending; see `Builds/regression.log` marker.**

## What the owner should felt-test

1. Journey -> Raids: four cards - The Forsaken Camp open, The Broken Garrison / The Veiled Enclave /
   The Iron Bastion locked with a "needs N victories" line on the card.
2. Tap a locked card - a toast repeats the card's exact sentence; nothing opens.
3. Win the Forsaken Camp three times - the Broken Garrison unlocks; its clear pays visibly more gold.
4. After a win, open the Season Pass - XP moved (+50 base, more for stars / full destruction /
   first clear of that target). Lose one - XP does NOT move.
5. Close the app fully and relaunch - the victory count and the unlocked camps survive.
