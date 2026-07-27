# Night Wrap — 2026-07-26 (felt-test fix wave + build 2 + Echoes rebrand)

**Branch:** `wip/village2-and-f8-tickets` · **HEAD:** `15fc4a1d` (in sync with origin)
**Build:** `Builds/Android/DefendersOfTheRealm.apk` (458 MB) — build 2 **SUCCEEDED**, `adb install -r` to Seeker (`SM02G4061955851`) = **Success**, and **Firebase App Distribution release `1.0 (1)` (id `3nmt87lbb7h2g`) uploaded** (project `defenders-of-the-realm-echos`; add testers in console — none pre-set).
**Gate:** `COMPILE_GATE_OK` + `REGRESSION_OK` (incl. DESTROYED_STRUCTURE_OK after the edit-mode determinism fix) + EditMode **742/742** (0 fail).

## What shipped (on-device build 2)
A full on-device felt-test drove a 12-fix wave, all committed as lane commits (`d860b30d`→`15fc4a1d`), gated green, and built:

| Fix | Commit theme |
|---|---|
| Dungeon walls/floor rainbow → solid stone (cube-mapped KayKit atlas killed) | fix(dungeon) materials |
| Dungeon doors: walk-by auto-teleport footgun removed (Interact-only); one entry system | fix(dungeon) door/portal |
| Dungeon hero vitals from real hero (not 120/60); checkpoint heals persist | fix(dungeon) vitals |
| Dungeon mobs leash to their room (no more all-rush-the-entry); default-off opt-in | fix(dungeon) leash |
| Dungeon hollows resolve to distinct models (underscore-alias fix) | fix(enemies) |
| Town heal-swirl removed; green fireflies re-tethered to the tree crown | fix(vfx) |
| Destroyed structures = lost: no repair, object + NPC removed, rebuild full cost (WO-753 complete) | fix(structures) |
| Elemental weapons flash their element on hit (fire live; others when weapons get `element` + owner tags) | fix(vfx) |
| Exploded Orc-Raider: deferred Wildlands ids redirect to a ratified Hollow substitute (§1.1 enforced) | fix(enemies) |
| Vanished-buildings: LEVER-1 stores pre-stand visible + staffed on a fresh hub (no wipe; standdown only hides a REPLACED store) | fix(hub) |
| Retired outpost: cave-portal seam gated behind `ff.raidwalk`; ambient region-roaming gated OFF (`ff.regionroam` default off) | fix(overworld gate) |
| Player-facing **Pets → Echoes** (HUD button + intro tagline "Echoes of a Forgotten Civilization") | feat(ui) rebrand |

Plus (earlier in the session, already on origin): troop wounded-recovery wiring, multi-channel queue HUD reachable + labels + Train timers, barracks progression, EnemyResolver, EditMode-debt cleanup.

## New flags of record
- `ff.regionroam` — ambient overworld region roamers. **Default OFF** (owner 2026-07-26, WWCD: regions peaceful until the player picks a fight). Reversible.
- `ff.raidwalk` — now also gates the retired walk-up-outpost cave-portal seam (`CavePortalRepointInjector` / `ChallengeOutpostVictoryController`), matching canon (walk-up outposts retired; raid = Teleport/Deploy).

## Process notes
- **Multi-committer event:** a concurrent session committed+pushed the wave as clean lane commits (and wired the OverworldCombatGate + DestroyedStructure oracles). Reconciled cleanly (origin == local); flagged per §11 — sole-committer discipline reasserted. See memory `sole-git-committer`.
- **Live felt-test loop:** device screen read via `adb exec-out screencap` → Read the PNG (screencap = my eyes on the Seeker); device runtime data via `adb logcat`. See memory `device-screencap-is-my-eyes`.
- **Diagnostic-channel hygiene gap found:** `[Flow:Equip]`/`[Flow:Offset]` fire PER FRAME, flooding logcat + the F8 harvest and burying scene-load lines ("the data" couldn't show the encounter scene). Throttle those traces — follow-up below.

## Deferred follow-ups (NOT in build 2 — queued)
- **WO-782** — capsule NPC/boss standee (Bryn/mini-boss): re-source `DungeonSceneBuilder` from tracked `Resources/Enemies` + `Resources/NPCs` (not gitignored KayKit) + re-bake `Dungeon_HealersCottage.unity` (editor-closed).
- **WO-779** — UI spacing/layout/legibility conformance sweep (owner UI-seat spec, in `WorkOrders/`).
- **WO-780** — dungeon-functional-conformance (owner file pending; number reserved — my prior FTUE-780 was closed stale).
- Equip/offset per-frame log spam → throttle to on-change (restores the F8 "look at the data first" workflow).
- `en.json` de-pet sweep (lore strings still say "Pets": `buildingDesc.crystalMine`, `petCaption.*`, `petAmbient.*`, `milestone.firstPetLevelUp`) — the button + intro are done.
- Elemental weapons: only `knight_flameblade` carries `element` today — owner to brand the Emberhand blades + tag per-element on-hit keys (holy/water/earth still HELD, no owner tag).
- Wire the elemental-on-hit + enemy-leash source-lint markers into `DataRegression.RunAll` (their EditMode tests already pass — belt-and-suspenders).
- RegionMobSpawner ambient roaming: owner to confirm keep-off vs re-enable-with-Hollows.
- Firebase decision: `firebase appdistribution:distribute` command recorded (memory `firebase-app-distribution`).

## Gate/build reproduce
`CompileGate.Run` → `DataRegression.RunAll` → `-runTests EditMode` → `AndroidBuild.BuildSeekerApk` → `adb install -r` → `firebase appdistribution:distribute`.
