# RESULT - WORK ORDER 1372 - Troops cost TIME, gold BUYS TIME (hire mercenaries)

**Filed:** 2026-09-04 (board agent, from the lead's measured facts)
**WO status:** FIXED - in build 2026.09.05.355872, awaiting owner felt-test. PO closes.

## What shipped

The ruling as resolved by the banner on the WO (the map wins; troops COST gold, ALSO take time,
and a SECOND gold spend hires mercenaries to skip the remaining clock). **Lane D (gold hires
mercenaries)** landed tonight and is IN this build:

- `BuildTimerConfig` - `SkipPrice` is now ONE curve (no per-channel fork) plus
  `HireReinforcementsPrice`.
- `BuildTimerService` - `FinishPaysGold` / `InsufficientGoldPrefix` / `HireReinforcementsVerb` and
  the wallet branch: a `TrainTroop` job finishes on the ONE instant-finish mechanism, priced in
  gold; a Builder job still pays crystals.
- `ObsidianQueueHud` + `ManageScreenVM` - the Train channel's speed-up CTA reads the mercenary verb
  (`HIRE REINFORCEMENTS - {0} Gold`, `canon-strings.json:396-397`), never "Skip Training".
- The mercenary runtime itself (`TryHireMercenaries`, second gold spend skips the clock) was
  committed earlier today in `1ef5f6ad4` Lane D by another seat; tonight's lane finished the price
  curve + wallet branch + HUD wiring and proved it.

## What is NOT claimed here

- **Part C - sell surplus resources for gold** - not in the lead's fact list for this build and not
  verified by this RESULT. Treat it as still open on this ticket.
- The section 5 owner rulings (do hired troops differ, supply limit, does `Hire` extend to Builder /
  Research) remain hers; nothing in this build answers them in code.
- The WO's "enqueue at zero gold" acceptance line was SUPERSEDED by the banner (gold stays the
  price); it is not a failure.

## Which suite proves it

- `[hire-reinforcements]` (`HireReinforcementsRegression`, registered `DataRegression.cs:1337`) -
  pass-2 line: *"gold (and only gold) finishes a TrainTroop job on the ONE instant-finish mechanism:
  Coins falls by exactly the quoted price, Crystals is untouched, the troop lands in the roster, a
  Builder job still pays crystals, and the two shortfalls carry distinct prefixes"*.
- `[tunable-defaults]` (`RemoteTunablesDefaultsRegression`, `DataRegression.cs:1475`) GREEN on the
  same pass - all 34 knobs resolve to shipping defaults on every failure path.

## Build + install evidence (lead's measurements, 2026-09-04)

- Build `2026.09.05.355872` (versionCode 355872) installed on the owner's Seeker 2026-09-04 22:22:13
  via `install-apk-to-seeker.ps1` (`Success`; `adb shell dumpsys package` versionName=2026.09.05.355872).
- Chain markers: `SCHEMA_PARITY_OK`, `APK_OK` (461 MB), `R2_PUSH_OK` (catalog_2026.09.05.355872.bin/.hash
  uploaded), `R2_PARITY_OK targets=Android,StandaloneWindows64,WebGL objects=271`, `APK_DONE`.
- Regression on the same tree: pass 2 = 375/377, `[hire-reinforcements]` GREEN; the two reds were
  in TEST code and are fixed; **final pass pending; see `Builds/regression.log` marker.**

## What the owner should felt-test

1. Barracks -> queue one Footman. The job should take gold AND start a training clock.
2. Open Manage (the `Upgrade` bar face) - the running Train job shows a **HIRE REINFORCEMENTS -
   N Gold** button, not "Finish Now" / "Skip Training".
3. Tap it with enough gold: the clock ends at once, gold drops by exactly the quoted N, crystals do
   not move, the Footman is in the roster.
4. Tap it with too little gold: a refusal that names gold (not crystals) and the shortfall.
5. Queue a Builder job (any structure) and check its speed-up still prices in crystals.
