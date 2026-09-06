# HANDOVER 2026-09-05 evening - the Manage re-layout night (CLI lead seat, Fable director + Opus lanes + Codex dev lane)

Frozen point-in-time ledger. Written 23:2x before the owner's reboot, so the resume steps survive the session.

## 0. Owner rulings tonight (verbatim where it matters)
- "i want tonight to be a focus on the UI layout ... too much text on screen ... Clash of clans and warcraft ... simple
  and intuitive" / "manage is the big offender" / "we can reuse the building cards from build". Her two mockups (Manage -
  Buildings, Manage - Troops: portrait rail, one selected card, BUILDING/TRAINING NOW strip) are the approved target.
- "can we do most work in Opus and leave the CLI seat only in Fable as the smart director?" -> every dispatched lane is a
  non-fork Agent with `model: "opus"`; this seat keeps gate / verify / commit / rulings (memory
  `lanes-run-on-opus-cli-seat-fable-director`).
- "i can have CLI Codex do a lot of work if you give me detailed work orders" / "feel free to hand as much to Codex as
  you want" / "codex has full credits" -> BATCH_STATE.md PART 8 is the Codex batch; the owner couriers.
- "yeah i hate those [*] items when we should use some icon, we have over 4000" -> WO-1419 (ember-medallion flame icons).
- Order of work: close the tree first, then Manage; wait for Codex, then reboot, then build (her answers).

## 1. What landed (all local, NEVER pushed - 46 commits ahead of origin/feat/synty-art-retheme)
| commit | what |
|---|---|
| `9a9e65c8a` | WO-1416 the Quarry pays Stone (code + guide + canon-strings; oracles moved with the ruling) |
| `e3b082a1a` | WO-1417 build palette item cards on the kit |
| `003b64ce2` | WO-1407 Heart plate objective line, minutes, idle-builders copy (model-published army snapshot) |
| `4ebbaccf7` | board: WO-1418 + WO-1419 minted, PART 8, 1415 status corrected, 1405/1406/1412 absorbed |
| `87393bfeb` | WO-1402 raid cards say what a raid PAYS (+ the culled-row fix: CardHeightPx 142 -> 178) |
| `9d1e7fb2a` | WO-1403 Raid Deploy at zero troops: TRAIN TROOPS, one door, readiness via the ONE snapshot |
| `7d97cb892` | courier files (PART 8 s8.6-8.9 + hand-backs) |
| `11dfea3c1` | WO-1419 ember-medallion flame icons (Codex; + sprite cache + missing-sprite FlowTrace.Once) |
| `3c677027e` | WO-1418 Manage - Buildings re-layout (Codex lanes A-D + rework; launcher toast pin re-pointed) |
| `ecf647b53` | WO-1410 one source for BAG/SKILLS/LOADOUT (Codex; + popup word, Wisdom plate, OPEN SKILLS at the floor) |
| `5661d71e4` | WO-1404 Journey deck subtitles carry state (Codex; Core VM, change-only publisher) |
| `458baf57f` | WO-1413 part 1 copy hygiene (Codex; Pause reverted per ruling) |
| `f4c6e27dc` | board + courier files (s8.10 polish rework, s8.11 new base) |

Every commit was gated on a fresh log before it landed: the last full run is `COMPILE_GATE_OK` (`Builds/c11` 23:13),
`REGRESSION_OK 389/389` (`Builds/r12` 23:15), `UI_CAPTURE_OK 91` (`Builds/cap6` 23:16),
`REGISTERED_SECONDARY_CAPTURE_OK 36/36 touch=clean` (`cap5s` 23:11), `MANAGE_OPERATIONAL_CAPTURE_OK 12/12`
(`capman` 22:45), `ADAPTIVE_HUD_CAPTURE_OK 9/9` (`caphud` 22:27). Frames opened by the CLI: BuildCollections,
RaidSelection, RaidDeploy (both aspects), AdaptiveHudPeaceful, ManageBuildings (both aspects, sent to the owner),
HeroSkillTree, HeroLoadout (both aspects), JourneyWorkspace, HelpMenu.

## 2. NOT on the device yet
The phone (SM02G4061955851, on USB) still runs **2026.09.05.356620** (pre-WO-1415). The 11:02 chain died on an APK
file lock (`apk-build.log:25795`), so nothing since 356620 has been installed. `ProjectSettings.asset` carries the
phantom stamp 356642 - deliberately NOT committed; the next chain re-stamps.
Commit charge at 23:2x: **105.2 / 115.8 GB** - the leak that OOM-kills player builds. **Reboot before the chain.**

## 3. RESUME AFTER THE REBOOT (the exact steps)
1. `git status` - expect only `ProjectSettings/ProjectSettings.asset` dirty; HEAD `f4c6e27dc` or later.
2. If Codex's s8.10 polish hand-back is in `batch_results_state.md`: measure (stat, brace/NUL, `git apply --check --3way`),
   apply, **`git reset` immediately** (memory `git-apply-3way-stages-reset-before-commit`), compile + regression +
   `RunManageOperationalCaptureHeadless`, OPEN `ManageBuildings_2670x1200.png` and `_1920x1080.png`, commit by explicit
   path with the WO-1418 RESULT updated.
3. APK: `Start-Process powershell -ArgumentList '-File .\overnight-apk-build.ps1 -Tester'` (detached); watch
   `Builds\overnight-apk-status.txt` for `SCHEMA_PARITY_OK` -> `APK_OK` -> `R2_PARITY_OK` -> `APK_DONE`. Retry once on a
   kill. Then `& .\install-apk-to-seeker.ps1 -Build:$false -Install:$true` (direct call, never `-File`);
   `.\distribute-android.ps1 -Notes "..."`.
4. Screencap the phone (`adb shell screencap -p /sdcard/x.png; adb pull`) on Manage - Buildings, Manage - Troops, the
   Heart plate, Raids, Raid Deploy; send them to the owner. Flip 1415 / 1416 / 1417 / 1402 / 1403 / 1407 / 1419 / 1418 /
   1410 / 1404 / 1413p1 Status lines with the build number; `python tools/board_build.py`; commit.
5. Never push. The production push is the owner's, via `publishing/SUBMIT_CHECKLIST.md` as written.

## 4. Codex (dev lane) state - BATCH_STATE.md PART 8
- In flight: **s8.10 Manage polish** (six measured defects on the first frames; rebases onto `ecf647b53`+).
- Next for the lane: **WO-1413 part 2** (UICaptureLaunch fixtures, HudKitController faces, dialogue twins,
  CopyHygieneRegression) at `458baf57f`; **WO-1409** landed inside 1418 lane D (its own oracle still owed).
- Lead-owned follow-ups: WO-1348 rail glue (RemoteTunables spine + `VfxAssetLoader.SetRuntimePickResolver`; the lane's
  code sits in `D:\eoa-codex-1348`, base `44d46128d`); `StoreReturnToManageRegression` + `NightMarketNoWalletRegression`
  (RED-first, in-house); a headless fixture that opens one Build collection's item cards (WO-1417 visual);
  `invHeaderTalents` retirement with its pin; the `Phase 2` Troop*/Building* unification WO after her felt-test.
- Worktrees to remove when their lanes are fully landed: `D:\eoa-codex-1418-{a,b,c,d,integration}`, `-1404`, `-1410-ready`
  (+ the interrupted `-1410`), `-1413`, `-1419`, `-1348`; in-house `D:\eoa-lane-1403`, `D:\eoa-lane-1407`.

## 5. Rulings queue for the owner (one word each)
1. WO-1414 B (sub-minute re-fire) and D.
2. Overnight #1 (arcane-tower tier-1 authors crystal cost but the service charges wood - data or service?) and #2.
3. REVIEW_MERGED 1-16 defaults (the lanes shipped on the stated defaults; confirm or re-rule).
4. WO-1416 art: the Quarry still wears the farm visual.
5. WO-1407: "Builders chip tap opens Manage" contradicts the ONE-Queues-door rule - want a second door?
6. WO-1403: header dropped the redundant `Troops N` line; Echo quote two-line authoring lives in EchoGuideService.
7. WO-1419: icon size (26 px slots) on the device.
8. WO-1418: footer hint text ("Recommended next upgrades" in the mockup vs the shipped "Need another town structure?").
9. WO-1410: the Loadout door reads OPEN SKILLS (diagram) not the bare key value (line 38).
10. WO-1413: "Pause: RESUME only" - the primary Resume face is owner-approved skin and the ornate CLOSE is the kit shell.
11. Portraits: Forge / Lumber Mill have no building portrait (NPC faces stand in) - art drop.
12. 1373 / 1377 / 1292 / 1314 pins; drop `stash@{0}` (1388/1389 partial, both FIXED)?

## 6. Process findings tonight (already in memory)
- `git apply --3way` STAGES; an explicit-path commit then sweeps the leftovers in - happened twice, both redone
  (`git-apply-3way-stages-reset-before-commit`).
- A read-only reviewer per Codex hand-back caught: an unreachable Wallet API from Village (compile), a HUD import in the
  regression assembly (compile), a chip that bypassed the Barracks lock, an OWNED word painted on locked nodes, a
  1 Hz VM construction with a trace per second, a Pause "Resume" that never rendered. Every one was a claim until proven.
- Rows built but culled by TMP (RaidSelection): a sub-29-unit text band is invisible in every headless capture - a
  kit/harness ticket to mint (`UiKitTextFitGuard` never fires in capture).
