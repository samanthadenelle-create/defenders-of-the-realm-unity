# RESULT - WORK ORDER 1380 - Echo Guides, and the memory lines

**Filed:** 2026-09-04 (board agent, from the lead's measured facts + a same-session read of the JSON)
**WO status:** FIXED - in build 2026.09.05.355872, awaiting owner felt-test. PO closes.

## Provenance - who built what

The runtime and the 24 lines were **committed earlier today by another seat in `1ef5f6ad4`**
(`EchoGuideCatalog`, `EchoGuideService`, `EchoAutoDeployTrigger` voice hook, `RaidDeployScreen`
Guide picker, `echo-guide-memories.json` in both twins, `EchoGuideMemoryRegression`). Tonight did
NOT author that. Tonight **PROVED it** - `[echo-guide-memories]` ran GREEN, the JSON gained its
`version` field, and the tree was cut to the build on her device.

## What shipped

- **Guide selection before a raid**, defaulting to **Corvin** when the player has made no choice.
- **24 memory lines** - the full 6-Echo x 4-target grid (Aldwin, Elowen, Corvin, Bran, Doran, Maren
  x The Forsaken Camp / The Broken Garrison / The Veiled Enclave / The Iron Bastion), no gap, no
  duplicate; every `echoId` is a real roster Echo from `EchoRosterCatalog`; the canon-quoted lines
  are unreworded (Doran at the Iron Bastion: *"I know this stone." / "I laid it."*).
- **Narrative only** - the Guide grants NO stat, yield or combat effect; the suite fails if one
  appears.
- **The Echo keeps ONE appearance owner** - `EchoWorldPresence` is voiced, not duplicated; no second
  spawner.
- **The section 5 player-name question is answered:** no string uses a player name (asserted by the suite),
  so Aldwin's *"...there's someone here."* ships without one and still works.
- **Tonight, IN this build:** `echo-guide-memories.json` `version` field (both twins).

## Which suite proves it

- `[echo-guide-memories]` (`EchoGuideMemoryRegression`, registered `DataRegression.cs:1258`) -
  pass-2 line: *"WO-1380 holds: all 24 Echo Guide memory lines ship (the full 6-Echo x 4-target
  grid, no gap, no duplicate); every echoId is a real roster Echo; the canon-quoted lines are
  unreworded and no string uses a player name; copy is ASCII; the Resources/StreamingAssets twins
  are byte-identical; the picker defaults to Corvin; the Guide grants NO stat/yield/combat effect
  ..."* - the count-below-24 and the scope-fence assertions the WO section 6 demands are both in it.

## Build + install evidence (lead's measurements, 2026-09-04)

- Build `2026.09.05.355872` (versionCode 355872) installed on the owner's Seeker 2026-09-04 22:22:13
  via `install-apk-to-seeker.ps1` (`Success`; `adb shell dumpsys package` versionName=2026.09.05.355872).
- Chain markers: `SCHEMA_PARITY_OK`, `APK_OK` (461 MB), `R2_PUSH_OK` (catalog_2026.09.05.355872.bin/.hash
  uploaded), `R2_PARITY_OK targets=Android,StandaloneWindows64,WebGL objects=271`, `APK_DONE`.
- Regression on the same tree: pass 2 = 375/377; the two reds were in TEST code, fixed; **final pass
  pending; see `Builds/regression.log` marker.**

## What the owner should felt-test

1. Journey -> Raids -> The Forsaken Camp -> the deploy screen shows an Echo Guide picker with
   **Corvin** pre-selected.
2. Deploy with Corvin - on the way in, his memory line for the Forsaken Camp plays (*"Wait. I have
   walked this road."*); nothing about loot or bonuses.
3. Pick Doran and raid a camp (any unlocked one) - a different line, his; the loot and the fight are
   identical to Corvin's run (no buff).
4. Reach The Iron Bastion later: Doran's *"I know this stone." / "I laid it."* - two beats, nothing
   explained.
5. Cycle every Echo once on one camp - six different lines, none silent, none a lore dump.
