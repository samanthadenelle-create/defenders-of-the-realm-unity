# RESULT - WORK ORDER 1374 - P0: close the raid economy loop

**Filed:** 2026-09-04 (board agent, from the lead's measured facts)
**WO status:** FIXED - in build 2026.09.05.355872, awaiting owner felt-test. PO closes.

## What shipped

The map's section 10 P0 list, across two landings:

**Earlier today, commit `1ef5f6ad4` (another seat), Lane A + the edit-only lane recorded in the WO
body:** raids pay wood + iron + gold on the five-rung performance ladder (`RaidScoring` /
`RaidLootTunables`), crystals rebalanced DOWN (55 -> 26 at a perfect clear), free starter army on
Barracks completion (`StarterArmyGrant`, 3 Footmen, latched on the acquired ledger), raid dailies
`requiresFeature: "raids"` resolved from `PostureSignals.RaidCapable`, the Arena Herald bypass
closed at `RaidSelectionScreen.Open()`, the refusal names the actual blocker, and the six-event
funnel on the existing `EventTracker` rail (`RaidFunnel`). Eight `raid.*` tunables on the existing
rail.

**Tonight, IN this build:**
- `guide-content.json` raids copy now sends the player to **Journey** (not "the HUD").
- `ArenaOutcomeRelay` now carries `win` to `BattlePassService.OnRaidResult` - **a LOST raid used to
  credit season XP as a win** - and `BattleMonthlyPanelsBootstrap` registers the raid handler.
- Tunable pins for `raid.heartfireMaxCharges` / `raid.heartfireRegenSeconds`; regenerated TowerPerk
  + Catalog fallbacks; twelve canonical JSONs de-BOMed.

## The registration finding - say it plainly

**Ten of this programme's suites were ON DISK but UNREGISTERED in `DataRegression.RunAll` until
tonight** - exactly the WO-973 failure this ticket's own "REGRESSION COVERAGE IS NOT OPTIONAL"
section warns about. They are registered now (`DataRegression.cs:1329-1338`) and all ten ran GREEN
on pass 2: `raid-loot-currency`, `raid-gold-arrow`, `raid-payout-visibility`, `raid-escalation`,
`raid-season-xp`, `raid-funnel`, `starter-army-grant`, `raid-discoverability-copy`,
`hire-reinforcements`, `away-summary-report`.

## Which suites prove THIS ticket

- `[raid-loot-currency]` - wood + iron on the ladder (fail 18% / 1* 50% / 2* 75% / 3* 100% / perfect
  110%) off 1800w/1100i, a loss still pays, all 8 knob defaults are the owner's numbers.
- `[raid-gold-arrow]` - gold on the ladder off a PER-CAMP base (2200/3100/4500/6500), gold and
  crystals off the camp multiplier, crystals at a perfect clear inside the 20-30 band.
- `[starter-army-grant]` - first Barracks grants 3 free Footmen once per save, spends nothing, tells
  the player Journey -> Raids.
- `[raid-funnel]` - six `raid_funnel_*` steps in order, the 24h window refuses a missing stamp /
  backwards clock, one telemetry rail.
- `[raid-discoverability-copy]` - Guide sends to Journey, both `combat.raid.*` dailies require the
  raids feature, the capability gate sits at the top of `RaidSelectionScreen.Open`.
- `[raid-season-xp]` - the raid outcome door is wired end to end (ArenaOutcomeRelay raid publish +
  a Wallet subscriber on `BattlePassService.OnRaidResult`); a loss pays nothing.
- `[tunable-defaults]` - 34 knobs incl. the seven WO-1374 raid-reward knobs + starter-squad size.

## Build + install evidence (lead's measurements, 2026-09-04)

- Build `2026.09.05.355872` (versionCode 355872) installed on the owner's Seeker 2026-09-04 22:22:13
  via `install-apk-to-seeker.ps1` (`Success`; `adb shell dumpsys package` versionName=2026.09.05.355872).
- Chain markers: `SCHEMA_PARITY_OK`, `APK_OK` (461 MB), `R2_PUSH_OK` (catalog_2026.09.05.355872.bin/.hash
  uploaded), `R2_PARITY_OK targets=Android,StandaloneWindows64,WebGL objects=271`, `APK_DONE`.
- Regression on the same tree: pass 2 = 375/377 with all ten suites above GREEN; the two reds were
  in TEST code (a new oracle's fixture and a hollow-pass lint), fixed; **final pass pending; see
  `Builds/regression.log` marker.**

## What the owner should felt-test

1. New game -> build the Barracks. On completion: 3 free Footmen appear and a message points to
   Journey -> Raids.
2. Open the Game Guide's Raids entry - it says open **Journey**, then Raids (not "the HUD").
3. Journey -> Raids -> The Forsaken Camp -> deploy and win. The result screen lists wood, iron,
   gold AND crystals (crystals noticeably fewer than before).
4. Lose (or retreat from) a raid on purpose - it still pays a small wood/iron/gold haul, and the
   Season Pass does NOT tick a win for it.
5. Talk to the Arena Herald before owning a Barracks - it refuses with the actual blocker named,
   and does not open the training panel.
