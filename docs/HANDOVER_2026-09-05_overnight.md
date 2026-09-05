# HANDOVER 2026-09-05 - overnight run (CLI, owner asleep from ~00:00)

Orders: `OVERNIGHT_ORDERS_2026-09-05.md`. Baseline at bedtime: commit `65d5a7eae` (pushed, owner-authorised,
23:5x), installed on the Seeker as build 2026.09.05.355952. **Nothing pushed since** (standing order).

Every line below names the log/PNG/commit it was read from. Anything without evidence is marked as such.

## Timeline (fill as it happens)
| time | what | evidence |
|---|---|---|
| 00:12 | first combined gate on lanes 1391+1393+1392+silo: RED, CS1657 `out _` shadowed by a `using var _` | `Builds/compile-gate.log` 00:13; fix at `ResourceCollectorService.cs:157` |
| 00:14 | `COMPILE_GATE_OK` | `Builds/compile-gate.log` mtime 00:14:42 |
| 00:17 | `REGRESSION_FAIL 377/378` - `echo-spec` pinned the OLD burn ("silo reset") | `Builds/regression.log` 00:17 |
| 00:19 | re-pinned `EchoSpecializationRegression.CheckDumpCredit` to conservation (pool == banked + retained); `COMPILE_GATE_OK` | `Builds/compile-gate.log` 00:19:19 |
| 00:21 | **`REGRESSION_OK 378/378 suites`** | `Builds/regression.log:116320`, mtime 00:21:27 |
| 00:22 | `UI_CAPTURE_OK` (91 PNGs regenerated) | `Builds/ui-capture.log` 00:22:33 |
| 00:23 | `RunRegisteredSecondaryCaptureHeadless`: **RED** `geometry=2 touch=2` - the WO-1391 card's LockBtn covers the Bonuses label (was green at baseline, `ui-reskin-registered-secondary-v9.log`) | `Builds/ui-capture-secondary.log`; `Builds/ui-capture/BuildingUpgrade_2670x1200.png` |
| 00:24 | WO-1391 lane resumed with the exact `[UICap-GEO]` lines; WO-1389 + WO-1388 lanes dispatched (file-disjoint) | this doc |
| 00:26 | `WELCOME_BACK_CAPTURE_OK 3/3` - per-resource rows, "9h 30m (STORAGE FULL)" | `Builds/ui-capture-welcomeback.png` / `Builds/ui-capture/WelcomeBack_2670x1200.png` |

## Builds installed on the Seeker
- 2026.09.05.355952 (bedtime baseline, 65d5a7eae) - still the installed build at 00:24 (adb devices: SM02G4061955851, battery 23%).

| 00:29 | 1391 layout fixed (bonus zone budgeted from the post-scale body; pills+tabs moved into the Close band): **`REGISTERED_SECONDARY_CAPTURE_OK 33/33 touch=clean`** | `Builds/ui-capture-secondary.log` 00:29:26; PNG regenerated |
| 00:31 | committed the gated set by explicit path | `da90ddc0f` |

| 00:33 | board: 1383/1384/1385/1386/1387/1390 -> FIXED (all in 65d5a7eae = build 355952); 1369/1371 wording; `BOARD_CHECK_OK` Fixed 32 / Ready 21 | `45ae654da` |
| ~00:45 | **API session limit hit** (HTTP 429, "resets 3:30am") - both lanes (1389, 1388) died mid-edit. Their partial diffs saved: `git stash@{0}` + `scratchpad/lanes-1388-1389-partial.patch` (1859 lines). Tree returned to HEAD. | stash list |
| 04:48 | resumed. `REGRESSION_OK 378/378` on HEAD 45ae654da (the tree the APK is built from) | `Builds/regression.log` mtime 04:51:07 |
| 04:52 | `overnight-apk-build.ps1 -Tester` started (background). Lanes 1389 + 1388 relaunched in plain worktrees `D:\eoa-lane-1389` / `D:\eoa-lane-1388` (branches `lane/wo-1389`, `lane/wo-1388`) so nothing touches the build tree; the Agent tool's own worktree isolation was refused (a `D:/EoA` vs `D:/eoa` path-case redirect - `git worktree list` shows the repo as `D:/EoA`). | `Builds/overnight-apk-status.txt` |

| 04:59 | **APK 2026.09.05.356272 built from 45ae654da** (version read off `Builds/apk-build.log`): `SCHEMA_PARITY_OK` 04:51:47 -> `APK_OK` 04:58:38 (461 MB) -> `R2_PARITY_OK targets=Android,StandaloneWindows64,WebGL objects=271` 04:59:04 -> `APK_DONE` | `Builds/overnight-apk-status.txt`; `Builds/Android/DefendersOfTheRealm.apk` |
| 05:00 | **install BLOCKED: the Seeker is gone from USB.** Windows PnP: Seeker `LastRemovalDate 2026-09-05 04:47:32` (arrival was 09-04 22:21). My first command after the limit reset was the regression at 04:48:01 - nothing I ran touched USB before the removal. adb kill/start-server: no devices. Cable/phone event; cannot be fixed from here. | `Get-PnpDeviceProperty ... DEVPKEY_Device_LastRemovalDate`; `install-apk-to-seeker.ps1`: "No Android device in 'device' state" |
| 05:02 | armed `scratchpad/wait-device-then-install.ps1` (polls adb every 30 s for 5 h; on sight runs the SANCTIONED install script once). Log: `Builds/wait-device-install.log`. **If the phone is back on USB in the morning and this log shows INSTALL_DONE, the build on the device is 45ae654da's.** Otherwise plug it in and run `.\install-apk-to-seeker.ps1 -Build:$false -Install:$true`. | that log |

| 05:07 | WO-1388 lane finished in its worktree (21 files, +932/-16); diff applied into `D:\eoa` by explicit path; `cmp` packs.json twins IDENTICAL; `builders-hour` present. Pack name / basket / badge copy still the owner's call. | `scratchpad/lane-1388.patch` |
| 05:10 | WO-1384b lane finished (HudKitController rounded Mask + `NightMarketCardRing` + 3 comets from the ONE `Update()`; HudLabelFitRegression Case 12 `[night-market-aurora]`); rail-wiring lane dispatched for its 3 knobs (36-38) | agent report |
| 05:12 | `COMPILE_GATE_OK` on 1388 + 1384b | `Builds/compile-gate.log` 05:12:07 |
| 05:13 | `UI_CAPTURE_OK` (91 canvases) but `UI_GEOMETRY_FAIL x9` / `UI_TOUCH_FAIL x9` - ALL on `HeroSkillTree` QuickSwapRail ObsBtn_1..3 over text, at all three resolutions. **Pre-existing, not tonight's:** `HeroSkillTreePanelMvvm.cs` last commit 3b3f28354 (09-03 14:46), untouched by any lane. Ticket to mint in the morning pile. | `Builds/ui-capture.log` `[UICap-GEO]` lines |
| 05:14 | `ADAPTIVE_HUD_CAPTURE_OK 9/9`; trace `[Flow:Store] HUD Night Market card (WO-1384b): rounded r=18px, ring 6px, comets=3, lap=5s alpha=35% paletteMask=7`. PNG shows the rounded ring + a comet, full "NIGHT MARKET" label. Motion + the `aurora cost` line need the device. | `Builds/ui-capture/AdaptiveHudPeaceful_2670x1200.png` |

| 05:18-05:23 | rail lane landed knobs 36-38; `COMPILE_GATE_OK` 05:18; regression RED once on an ENVIRONMENTAL red (the encoding suite scanned two refused Agent-tool worktrees under `.claude/worktrees/` carrying an old non-ASCII `install-apk-to-seeker.ps1`) -> removed those two stale worktrees -> **`REGRESSION_OK 378/378`** 05:23 | `Builds/regression.log` 05:23:18 |
| 05:24 | committed 1388 + 1384b + rail by explicit path | `9b47c9ad9` (27 files) |
| 05:25 | WO-1389 lane finished in its worktree (30 files); diff applied into `D:\eoa`; twins `tutorial-steps.json` / `dialogues.json` / `guide-content.json` cmp IDENTICAL; seven graph dead-end WOs 1394..1400 minted (banner -> 1401), committed | `01ceeb346` |
| 05:29 | `COMPILE_GATE_OK` after one gate-step fix (a ternary used as a statement, `PlayerDeckWorkspace.cs:549`) | `Builds/compile-gate.log` 05:29:26 |
| 05:32 | `REGRESSION_FAIL 376/378`: (a) RaidDeployUi pinned the OLD scout line verbatim - re-pointed to the WO-1389 compare form (oracle moves with the ruling); (b) `RaidsDiscoverability D5`: the 1389 lane read `ArmyReadiness` inside `RaidCapabilityHudBridge` (the visibility bridge must never) - sent back to the lane to move the producer | `Builds/regression.log` 05:32 |
| 05:30 | two more lanes loaded in worktrees: WO-1398 (one Night Market string source) at `D:\eoa-lane-1398`; a new WO-1401 (HeroSkillTree quick-swap rail over text, from the 05:13 geometry audit) at `D:\eoa-lane-skilltree` | agents |
| 05:2x | **Owner (mid-turn): "When done and built push to firebase."** Firebase CLI 15.24.0 logged in; app `1:264518851517:android:8e193b012cba6986d050d4` confirmed via `firebase apps:list`. The next green APK goes to App Distribution (group testers) as well as the device watcher. | `firebase apps:list ANDROID --project defenders-of-the-realm-echos` |

| 05:36-05:37 | 1389 bridge fix landed (producer moved to `BuildTimerService.PublishArmyStatus`; bridge byte-identical to HEAD): `COMPILE_GATE_OK` 05:36, **`REGRESSION_OK 378/378`** 05:37 | `Builds/regression.log` 05:37:47 |
| 05:38 | committed WO-1389 by explicit path (32 files); APK chain started on that tree | `e99d2f290` |
| 05:4x | lanes 1398 (one Night Market string source, 12 files) + 1401 (skill-tree rail, 3 files) finished in their worktrees; patches exported, held until the build's `APK_OK` so the build tree stays byte-stable; banner -> 1402 | `scratchpad/lane-1398.patch`, `lane-1401.patch` |
| ~05:45 | **APK build KILLED by the harness for low system memory** mid-Unity. Measured: physical 31.8 GB / free 9.8 GB, **committed 98.1 GB of a 115.8 GB limit** with the top 12 processes summing to ~10 GB - the known commit-charge leak from long batchmode nights (memory `commit-charge-leak-blocks-builds`: no process owns it; a reboot is the only fix). Stopped the orphaned Unity Gradle `java.exe` (2.3 GB); retrying the chain ONCE. If it dies again: **the morning needs a reboot before any build**, and the tree at `e99d2f290` is gated green and ready to build. | `Get-Counter '\Memory\Committed Bytes'`; `overnight-apk-status.txt` stopped at `SCHEMA_PARITY_OK` |

| 05:45 | retry killed the same way. Launched the SAME sanctioned script DETACHED from the harness (`Start-Process ... overnight-apk-build.ps1 -Tester`, log `Builds/overnight-apk-detached.log`) with a status-file monitor. | |
| 05:54 | **APK 2026.09.05.356329 built from e99d2f290**: `SCHEMA_PARITY_OK` 05:48:43 -> `APK_OK` 05:53:41 (461 MB) -> `R2_PARITY_OK objects=271` 05:54:14 -> `APK_DONE`. The harness's memory kill is about the harness's own watchdog; Unity itself finished under the leak both times it was allowed to. | `Builds/overnight-apk-status.txt` |
| 05:55 | **Pushed to Firebase App Distribution** (owner order 05:2x "push to firebase"): `uploaded new release 2026.09.05.356329 (356329) successfully`, release notes added, `distributed to testers/groups successfully`. Console: https://console.firebase.google.com/project/defenders-of-the-realm-echos/appdistribution/app/android:com.denellestudios.echoesofelarion/releases/4vs24o6e7l3k8 . Push notification sent to the owner. Device watcher still polling (no phone). | firebase CLI output; `Builds/firebase-release-notes-2026-09-05.txt` |
| 05:56 | WO-1391/1392/1393/1388/1389 -> FIXED (on a distributed build). 1398 + 1401 patches applied to main (1398's `PlayerDeckWorkspace` hunk reconciled by hand against 1389's Route signature). Gate next. | this commit |

| 06:07-06:14 | 1398 + 1401 gated: `COMPILE_GATE_OK` 06:12; regression RED once (`NightMarketUiRegression` pins exactly ONE wordmark read in PackStore - the 1398 lane had added a second on the diagnostic Register handle; handle re-pointed to "PackStoreUI") -> **`REGRESSION_OK 379/379`** 06:14 (the new StoreNameSingleSource suite is #379) | `Builds/regression.log` 06:14:40 |
| 06:15 | `UI_CAPTURE_OK` with **ZERO `[UICap-GEO]` lines** (HeroSkillTree was x9 at 05:13); traces `quick-swap rail built: slots y 0..112, hint y 120..160 (gap 8)` and `store face label='The Night Market' source=canon-strings` | `Builds/ui-capture.log` 06:15:44 |
| 06:16 | committed 1398 + 1401 by explicit path (18 files); the four lane worktrees + branches removed | `eb118ac14` |
| 06:17 | third APK chain started DETACHED on eb118ac14 (1398 + 1401 on top of the Firebase build); new lanes 1395 / 1399 / 1400 loaded in fresh worktrees off eb118ac14 | `Builds/overnight-apk-status.txt` |

| 06:24 | **APK 2026.09.05.356357 built from eb118ac14** (detached): `APK_OK` 06:24:25 -> `R2_PARITY_OK objects=271` -> `APK_DONE` 06:24:51 | `Builds/overnight-apk-status.txt` |
| 06:3x | **Pushed 356357 to Firebase App Distribution** (release `66dqqtmeb`, notes `Builds/firebase-release-notes-2026-09-05b.txt`: supersedes 356329; adds 1398 + 1401). Version bump committed `03991f38a`. | firebase CLI: `distributed to testers/groups successfully` |
| 06:35 | lanes 1395 / 1399 / 1400 finished in worktrees; merged into `D:\eoa` (3-way; the only conflict was the three suite registrations above DataRegression's END fence - all three kept) | `scratchpad/lane-1395.patch` etc. |
| 06:39-06:42 | `COMPILE_GATE_OK` 06:39; **`REGRESSION_OK 382/382`** 06:42 | `Builds/regression.log` 06:42:04 |
| 06:42 | **WO-1395 finding for the morning:** the graph's "registered twice under GOOGLE_PLAY" is FALSE at source - Wallet is `!GOOGLE_PLAY`, GooglePlay is `GOOGLE_PLAY` (asmdef defineConstraints, WO-1282). Fixed what was real (door-context opener + funnel line under Play, registration traced, detector). **Ruling needed:** two artifact-exclusive storefronts (pinned) vs Play as a PackStore skin (reverses WO-1282). | WO-1395 status line |

| 06:43-06:44 | `UI_CAPTURE_OK` zero geometry lines; `SETTINGS_CAPTURE_OK 3/3` with `ladder built: content=2066 px, unused=24 px` (the Help row fits; the PNG shows only the top of the scroll, so the row is proven by the trace, not the picture) | `Builds/ui-capture-settings.log` 06:44 |
| 06:44 | committed 1395 + 1399 + 1400 by explicit path (22 files) + board | `0f716ffaa`, `89887a3c1` |
| 06:45 | fourth APK chain started DETACHED on 89887a3c1; WO-1397 lane loaded in `D:\eoa-lane-1397` | `Builds/overnight-apk-status.txt` |

| 06:52 | **APK 2026.09.05.356386 built from 89887a3c1** (detached): `APK_OK` 06:51:50 -> `R2_PARITY_OK objects=271` -> `APK_DONE` 06:52:16; version bump committed `0e942b953`; Firebase upload started (notes `firebase-release-notes-2026-09-05c.txt`) | `Builds/overnight-apk-status.txt` |
| 06:55 | WO-1397 lane finished (Hero-deck "Wardrobe" card -> PanelId.CosmeticShop; deck grid rows from card count); merged into `D:\eoa`; suite registered by the committer above the fence | `scratchpad/lane-1397.patch` |
| 06:58-07:01 | `COMPILE_GATE_OK` 06:57; **`REGRESSION_OK 383/383`** 07:01 | `Builds/regression.log` 07:01:00 |

| 07:02 | `UI_CAPTURE_OK` but **`UI_GEOMETRY_FAIL x10`** from WO-1397: the third Hero-deck row resolves cards to 108.7 px (3.3 under MinTouchPx 112) at 2670x1200 and 110.9 at 2340x1080; and the Wardrobe card renders LOCKED in the headless capture (nothing registered the shop there). Sent back to the lane (editing in `D:\eoa`). 1397 is NOT committed yet. | `Builds/ui-capture.log` 07:02; `Builds/ui-capture/HeroWorkspace_2670x1200.png` |
| 07:05 | **Pushed 356386 to Firebase App Distribution** (`uploaded new release 2026.09.05.356386 ... distributed to testers/groups successfully`; notes `firebase-release-notes-2026-09-05c.txt`). WO-1398/1401/1399/1400/1395 -> FIXED. | firebase CLI output |

| 07:08-07:10 | 1397 fix landed (cell height floored at MinTouchPx+2, grid band extends toward Close; capture fixture registers the shop door): `COMPILE_GATE_OK` 07:08, **`REGRESSION_OK 383/383`** 07:09, **`UI_CAPTURE_OK` with ZERO geometry lines**; trace `deck 'Hero' grid 2x3 for 5 card(s), cell 788x114 (band 0.000..0.820 ..., extended toward Close for the touch floor)` | `Builds/ui-capture.log` 07:10:40 |
| 07:11 | committed WO-1397; fifth APK chain started DETACHED; two independent UI reviewers (read-only) running for MUST item 7 | this commit |

| 07:15 | independent UI review A delivered (read-only agent) - saved verbatim at `docs/qa/UI_REVIEW_2026-09-05/REVIEW_A_independent.md`; review B and the READY-silo dispatcher still running. Its top three: (1) every Manage row prices the tap but never says what it buys; (2) nothing in the raid chain says what a raid PAYS; (3) the HUD never tells a non-raid-capable player how to become one. Two of its "unproven" items are for the CLI: the BuildingUpgrade capture renders the card body dimmed (fixture or code?), and INDEX rows 15/16 are mislabelled. | that file |
| 07:17 | **APK 2026.09.05.356411 built from e94027216** (detached): `APK_OK` 07:17:28 -> `R2_PARITY_OK objects=271` -> `APK_DONE` 07:17:52; Firebase upload started (notes `firebase-release-notes-2026-09-05d.txt`) | `Builds/overnight-apk-status.txt` |

| 07:19-07:25 | independent UI review B delivered; CLI re-opened RaidSelection / RaidDeploy / JourneyWorkspace / NightMarket and confirmed the quoted words; **merged verdict written**: `docs/qa/UI_REVIEW_2026-09-05/REVIEW_MERGED.md` - 12 tickets (WO-1402..1413, one per failing screen), **15 rulings**, and a list of CLI checks. Both reviewers' top three agree: (1) nothing in the raid chain says what a raid PAYS; (2) BEGIN ASSAULT is live at 0 troops and "Visit the Barracks" is a sentence, not a door; (3) the HUD/welcome-back have no "something is ready" surface. | that file + `79d8c5b5e` |
| 07:20 | dispatcher pass written: `docs/reference/READY_SILOS_2026-09-05.md` - 16 READY tickets classified; **WO-1382 was already LANDED at bedtime (65d5a7eae) and proven on the device but never flipped** -> flipped to FIXED now; WO-1376 gate proven OPEN (`/api/dungeon-status` HTTP 200, 5 open / 1 sealed); WO-1379's other half confirmed (`RaidSelectionScreen.cs:550` still refuses on cooldown, 0 heartfire mentions; `scene-configs.json` notes say "not consulted at runtime" - false at source); four disjoint morning lanes named. | that file |
| 07:30 | four lanes dispatched in worktrees off fcae8d591 per the dispatcher table: **A** WO-1379 (Heartfire at the ONE raid door), **B** WO-1376 nav slice + 1394 + 1396 (Journey deck to five cards; 1396 on its read-only default, flagged), **C** WO-1361 census instrument, **D** WO-1366 arena wager per channel (rail key handed back). Plus a docs lane minting WO-1402..1413 (banner -> 1414). Firebase upload of 356411 still in flight. | agents |

| 07:3x | **Pushed 356411 to Firebase** (`uploaded new release 2026.09.05.356411 ... distributed to testers/groups successfully`). WO-1397 -> FIXED. WO-1402..1413 minted from the merged review (`bd106b24b`; board Ready 29 / Fixed 43). | firebase CLI |
| 07:36-07:42 | lanes C (1361 census instrument), D (1366 arena wager per channel), A (1379 Heartfire at the ONE raid door) finished; merged into `D:\eoa`. Lane C's first regression RED was the WallAdjacency wiring suite's comment stripper: a comment containing `logs/device/*.log` opened a `/*` block that swallowed the rest of the file - one comment word changed. **Lane D finding for the rulings list:** with "Unknown channel -> refuse" executed as written, the Editor and the desktop exe (both resolve `PaymentChannel.Unknown`) now REFUSE arena wagers; Pi is refused too. **Lane A question:** the Raids grid has no on-screen Heartfire state before a tap (refusal is the toast; flames live on the HUD) - does she want a Heartfire header on the grid? Lane D's four tunable keys (`arena.wagerTier1..3`, `arena.winPursePct`) await the rail wiring (one change, CLI). | agent reports; `scene-configs.json` twins cmp IDENTICAL |

| 07:43 | committed lanes A + C + D (24 files) | `da1773f1e`; board `eb472aa98` |
| 07:47-07:51 | lane B (Journey deck five cards: 1376 nav slice + 1394 + 1396) + the arena rail wiring (knobs 39-42) merged: `COMPILE_GATE_OK` 07:48, **`REGRESSION_OK 383/383`** 07:50, `UI_CAPTURE_OK` zero geometry lines, trace `deck 'Journey' grid 2x3 for 5 card(s), cell 788x114` | `Builds/regression.log` 07:50:20 |
| 07:52 | secondary capture RED on the NEW SeasonTrack case: the Season Track panel's own header (CloseButton over "Season Track - Emberwake" / "Tier 0 of 30" / "0 of 100 XP...") - a pre-existing defect on a screen nobody could reach until tonight. Fix lane dispatched (BattlePass panel header only). Lane B is NOT committed until it is green. | `Builds/ui-capture-secondary.log` 07:52 |

| 08:02-08:07 | Season Track header fixed (TextBand excludes a CloseBand; header 200/1200): `COMPILE_GATE_OK` 08:03, **`REGRESSION_OK 383/383`** 08:06, **`REGISTERED_SECONDARY_CAPTURE_OK 36/36 routes=12 touch=clean`** (trace `header bands: close y307.6..439.6 x628.1..988.1, text ... gap 12 px` at 2670x1200); `UI_CAPTURE_OK` zero geometry lines at 07:51 (Journey five cards, 114 px) | `Builds/ui-capture-secondary.log` 08:07:28 |
| 08:08 | sixth APK chain started DETACHED on this tree (lane B + rail + Season Track); commit follows by explicit path | `Builds/overnight-apk-status.txt` |

## Commits (local only)
- `da90ddc0f` feat(ui,harvest): WO-1391 upgrade page, WO-1393 close-frame grace + queue drawer, WO-1392 harvest never burns (49 files). Gate evidence: COMPILE_GATE_OK 00:19, REGRESSION_OK 378/378 00:21 (before the 1391 layout fix), COMPILE OK + REGISTERED_SECONDARY_CAPTURE_OK 00:29 (after it). The full regression re-runs at the next combined gate before any APK.

## What the headless proofs already show (before any device run)
- WO-1391: preview = catalog icon, never noise (`[Flow:UpgradeUI] preview model NOT resolved for 'arcane-tower@1'` -> `model band built ... as ICON`); the sentence names the shortfall (`Short 1280 Wood, 800 Gold`); kit CLOSE. Open: the card bands overlap (above).
- WO-1392: welcome-back rows are per resource from `PendingByResource()`; storage-full warning in words.
- WO-1393 / 1392 / silo: pins green inside 378/378.

## Rulings needed in the morning (one word each)
1. `arcane-tower` tier-1 authors `costCrystal: 1280` but `BuildingUpgradeService.TierCost` charges WOOD by tier number; the page shows the CHARGED lane. Which is right - the data or the service? (trace: `[Flow:Upgrade] arcane-tower tier-1 authors costCrystal=1280 (costWood=0) ...`, `ui-capture-secondary.log`.)
2. `BankOverflowStatus` has no retained-vs-lost field, so the silo dump row still uses the modal's generic sentence even though nothing is burned any more. Add the field (schema-free, in-memory) - yes/no?
3. WO-1389 Q1-Q3, WO-1388 pack name / basket / badge (unchanged from the WOs).

## Not done (and why)
- (fill)
