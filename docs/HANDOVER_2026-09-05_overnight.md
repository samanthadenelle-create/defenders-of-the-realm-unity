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
