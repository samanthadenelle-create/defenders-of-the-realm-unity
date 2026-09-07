# MORNING HANDBACK - 2026-09-07 (CLI seat, overnight run from 01:20)

**Status:** LIVE for one day; supersedes nothing. The pass/fail on every Manage screen is YOURS
(ruling 29: a device frame beside its mockup panel, full screen, >=95% on size / font / style /
context / images). Everything below is what was built, gated and measured; none of it is a pass.

## 1. What you asked for last night, and where it stands

| Ask (your words) | State this morning |
|---|---|
| "fix the board so those tickets dont say done" | 15 Manage tickets sit in a new board bucket **Verify** ("AWAITING OWNER MATCH"), which only your match can close. `BOARD.html` regenerated. |
| "update the goal to be screenshots proving these match" + "95% ... size font style context images" + "fill the screen, not 60%" | Written as ruling 29 (`WorkOrders/ManageRedesign/OWNER_RULINGS_LOCKED.md`), the goal block at the top of `CAPTURE_LOOP_GOAL.md` with the nine-row six-axis scorecard, WO-1566 s2.0 / WO-1567 s6.0. |
| offline harvest: "results reflect what the screen states, aesthetically fixed", "no idea why raid is listed here" | Both harvest surfaces read one merged number per resource; the away grant's overflow reaches a screen for the first time; RAID and the army line are gone from the welcome-back; screen == banked pinned on a fresh-game fixture. **Test it on the NEW GAME you planned:** the first welcome-back must claim nothing. |
| "build the APK to seeker, exe to windows, AAB for upload and Web UI deployed to vercel" | See section 4 (filled as each marker lands). |

## 2. Commits (all gated; markers on fresh logs named in each message)

- `c0c30f715` wave four: Manage passes one and two, harvest end to end, BUILD door + collections, dungeon doors, in-raid tray, breach probe, orc binding, four ticketed reds closed, board Verify bucket.
- `94808e2e2` Manage passes three and four: the CLOSE band reclaimed (the missing grid row and three queue rows), hub cards, tree rows, tiles with whole buildings, queue overlay, capture fidelity proven.
- Gate at HEAD: `COMPILE_GATE_OK` (cg-wave5c), `REGRESSION 440/441` (reg-wave5c; the one red is WO-1539, an ogre model with no mesh = art ask), `MANAGE_FLOW_MAP_OK 16 frames, geometry 0, touch 0` (cap-manage-wave5c), `DUNGEON_DOOR_CAPTURE_OK`, `ENEMY_PROVING_OK 19/19`.

## 3. The nine Manage screens - my read of the headless frames (NOT a pass)

Frames: `Builds/ui-capture/ManageFlow_*_2670x1200.png` (hub, BUILD/ARMY/RESEARCH x grid top/bottom,
queue, locked, max, research school). Mockup: `docs/mockups/manage/MANAGE_MOCKUP_8_SCREENS.png`.

| # | Screen | Fills screen | My read against the panel | Known gap |
|---|---|---|---|---|
| 1 | Hub | yes | three cards at the sheet's ratio, full copy, HEART chip in the header | the three card paintings DO NOT EXIST (art ask: hub-build / hub-army / hub-research); cards fill ~0.55 of the well |
| 2 | BUILD grid | yes | 5x2 fills the band, whole buildings, one-word state + icon | none I can see; your call on the state words vs the mockup's bare tiles |
| 3 | Building detail | yes | square art, level, description, Production current->next, cost words, UPGRADE | art is square, mockup is a taller painting |
| 4 | ARMY grid | yes | 3x3 fills, locked dimmed | portraits carry a baked gilt ring (WO-1574 art ask) |
| 5 | Troop detail | yes | stats, train time band, TRAIN 1 <NAME> | ring in the art; training is FREE by your WO-1387 ruling, so no cost band |
| 6 | Locked troop | yes | requirement row + VIEW BARRACKS door | device's door kept over the mockup's inert plate (your WO-1518 ruling) |
| 7 | Research picker | yes | one row of square school tiles, centred | mockup draws 4 schools, this save has 5 |
| 8 | Research tree | yes | painting 40% + rows 60%, two-line benefit, padlock rows | cost on RESEARCH only when a row is researchable |
| 9 | Queue | yes | overlay, active tab, 4 rows with portraits, CANCEL / MOVE UP / SPEED UP + cost | mockup shows 5 rows; five need ~870 px of well, we have 758 at MinTouchPx - stated in the code and the suite, not faked |

## 4. Builds (filled as markers land)

- APK: `APK_OK 03:41:58` (Builds/overnight-apk-status.txt), 469 MB, `R2_PARITY_OK objects=271` 03:42:20, `Builds/Android/DefendersOfTheRealm.apk`
- Seeker install: `Success` 03:43:43, device reads `versionName=2026.09.07.359076`
- Windows exe: `[build] SUCCESS` 03:47:29, `Builds/Windows/DefendersOfTheRealm.exe` (Builds/build.log)
- Google Play AAB: `AAB_OK` + `AAB_SIZE_OK 477,211,728 (22.8 MB under the 500 MB cap)` + `AAB_DONE 04:04:49` (Builds/aab-status.txt), `Builds/Android/EchoesOfElarion-GooglePlay.aab` 458 MiB, release keystore
- WebGL -> Vercel: __WEB__

## 5. Decisions only you can make (each one word)

1. Orc casters: the stand-in Mage sheet now actually reaches the Necromancer / Shaman / Berserker bodies (`Builds/EnemyCaps/orc-necromancer.png`, `ogre.png`, `orc-berserker.png`). It reads as a textured orc with the wrong UV layout. Keep until real sheets exist, or revert to white?
2. Art asks now open: hub card paintings x3 (WO-1567 s5), rectangular troop portraits x9 (WO-1574), Sky Ballista tier sheets x2 (WO-1567 s5), OgreMage mesh (WO-1539).
3. Queue drawer: accept four rows at the touch floor, or rule a smaller row.
4. From before: migrations (`node tools/run-migrations.mjs --baseline ...`), the store update media, WO-1527 / 1475 / 1504 / 1529 / 1486 rulings, the proof/ deletions.

## 6. Things found overnight that were not on any ticket

- Every Manage screen reserved a CLOSE band that only the hub draws (~150 px) - the cause of the grid's missing row and the queue's missing rows.
- Tower and wall queue rows never asked for a portrait (two catalogs, one key producer now).
- The Build Collections root hid Realm and Trade because a baked twin counted as "built" (WO-1572).
- A Manage BUILD button for a CRAFT/ECONOMY structure dead-ended at the collections root (WO-1571).
- The four "pre-existing" regression reds were: a ruling working (repair), a lost trace line (arena), a false premise (flag bleed), and a real F8 dedupe defect (WO-1531, fixed and both producers restarted).
- Verify bucket + ManageRedesign rows on the board; the era sweep knows Verify.
