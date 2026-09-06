# WO-1407: the town HUD never tells a non-raid-capable player how to become one; raw seconds, ASCII pips, no idle-builders surface

**Status:** FIXED - ON THE SEEKER in build 2026.09.06.357453 (chain 00:31-00:38: APK_OK 463MB, R2_PARITY_OK objects=271; installed 00:41, versionCode 357453 read off dumpsys; Firebase App Distribution release 0kka4h6t9u400); owner felt-test closes 2026-09-05 21:45 - objective line + minutes + idle-builders copy landed (HudStateCopy, model-published army snapshot), HudLabelFit cases 13-15 green, COMPILE_GATE_OK + REGRESSION_OK 385/385; `[*]` pips -> icons is WO-1419, chip-tap door needs a ruling (RESULT file); device build tonight. *(was: READY TO IMPLEMENT - minted 2026-09-05 from the merged UI review)*

## Evidence
- Device frames (build 355952) - SEEN (`REVIEW_MERGED.md` row 6): `docs/qa/UI_REVIEW_2026-09-05/02-hud-town.png`,
  `12-hud-after-store-close.png`, `16-journey-deck.png` (a HUD frame; INDEX mislabel). Heart plate line 2 reads the
  static `Prepare the realm for the next wave.`; `Next wave in 855s`; `[*] [*] [*]` ASCII pips; no Builders chip
  while builders are idle; `Heartfire is full` clipped by the plate bottom on every device frame (fits in
  `Builds/ui-capture/AdaptiveHudPeaceful_2670x1200.png` 05:14 - device re-check on 356411 is a queued CLI check).
- Both reviewers: `REVIEW_A_independent.md` E-1 / E-5, `REVIEW_B_independent.md` E2 / E4 / E5.
- CODE: `Assets/_Modules/HUD/Kit/HudKitController.cs:2082` and `:4196` author the static plate line; `:3338`
  builds `"Next wave in " + CeilToInt(...) + "s"` (also `Village/Waves/WaveCountdownUI.cs:139`). Raid capability
  is already a model flag (`RaidCapable`, gating the Raids face per CLAUDE.md s7); army fill is
  `PostureSignals.SetArmyFill` (`Core/HudModel/PostureSignals.cs:321`).

## What the player experiences
The home screen shows a wave timer in seconds and a sentence that never changes. A player who cannot raid is
told nothing about how to earn it; a player with idle builders is not told they are idle. The only loud
invitation on the screen is the store card (ruling #9 - not changed here).

## Fix shape (one mechanism)
The Heart plate's line 2 becomes a model-driven string in `HudActionBarModel`/posture (MVVM; the View renders):
- `!RaidCapable` and no Barracks -> `Raids unlock at a Barracks - Build > Realm`;
- `!RaidCapable` with a Barracks -> `Train 3 troops to unlock Raids` (count from the same ArmyReadiness rule);
- `RaidCapable` -> the existing wave line.
`Next wave in 855s` -> `Next wave in 14m 15s` via one shared duration formatter (both call sites). Pips become
words or a kit meter, never `[*]`. Builders chip is visible when idle, reading `Builders idle 2`, tap -> Manage.
Plate budgets four lines at device DPI so `Heartfire is full` is never clipped (kit label fit).

```
HEART OF ELARION   Heartfire is full
Train 3 troops to unlock Raids        Next wave in 14m 15s
```
Trace: `FlowTrace.Step("Hud", "heart plate line2='<text>' raidCapable=<bool>")` on each change-only publish.

## Acceptance
- [ ] RED first: `HudRaidHintRegression` - fixture without a Barracks: plate line 2 contains `Barracks`; with a
      Barracks and 0 troops: contains `Train`; raid-capable: contains `wave`; the countdown string at 855s reads
      `14m 15s`; no HUD string contains `[*]`. Fails on the current tree (`HudKitController.cs:2082,:3338`).
- [ ] Headless: `AdaptiveHudPeaceful_2670x1200.png` regenerated for both fixtures (`UI_CAPTURE_OK`), opened;
      `HudLabelFitRegression` green.
- [ ] Device: fresh save HUD reads the Barracks hint; owner's save reads the wave line in minutes; Heartfire line
      not clipped; screencaps read.

## Not in scope
The Night Market card size (ruling #9, NO CHANGE); the Heartfire "what a charge buys" clause (ruling #3, no
default - blocked until the owner's sentence); the bar face set (`MaxVisibleFaces` is the code's authority).

## Owner ruling
- Section 2 #3 Heartfire-does? - NOT written into this ticket (no default); the plate line 1 stays as-is.
- Section 2 #9 Card-size? - written to the default NO CHANGE.
