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
| `295c12bba` | this handover (first cut) |
| `97918f627` | `CLI_SESSION_PLAYBOOK.md` - the executed per-session script with a receipt per step (owner directive) |
| `73a31b67a` | WO-1418 POLISH - the six defects from the first frames (Codex s8.10; tier-sheet route deviation accepted) |
| `9ec35ed52` | WO-1413 part 2 - fixture verbs, live combat skill faces, CopyHygieneRegression (lead corrected its retired-pet scan) |
| `b9a9e3166` | board + courier files (s8.12) |
| `05cfd4d97` | this handover (second cut) |
| `85866703e` | WO-1418 ART DROP - 26 building portraits for all six Manage ladders (owner archive via Codex); resolver + coverage oracle |
| `27855e4b3` | board + courier files (s8.13: two duplicated-state nits for the dev lane) - **HEAD at the reboot call** |

Art-drop gate: `COMPILE_GATE_OK` (`c14` 00:19), `REGRESSION_OK 390/390` (`r15` 00:21), `MANAGE_OPERATIONAL_CAPTURE_OK
12/12` (`capman3` 00:22); Cathedral / Forge medallions paint their sheets. Resume step 2 in section 3 is now MOOT (no
s8.10 hand-back pending) - go straight to the APK chain.

Final gate on that HEAD: `COMPILE_GATE_OK` (`Builds/c13` 23:51), `REGRESSION_OK 390/390` (`Builds/r14` 23:54),
`MANAGE_OPERATIONAL_CAPTURE_OK 12/12` (`capman2` 23:49), `UI_CAPTURE_OK 91` (`cap7` 23:56),
`ADAPTIVE_HUD_CAPTURE_OK 9/9` (`caphud2` 23:57). Commit charge at the call: 106.2 / 115.8 GB. Codex's s8.10 and
1413 part 2 are LANDED - the dev lane has nothing in flight; its next items are in PART 8 s8.12 / section 4 below.

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

## 7. 2026-09-06 00:31-00:45 - the chain ran after the reboot
- Reboot confirmed 00:29 (uptime 2 min, commit charge 15 / 111.8 GB, no Unity process). F8 inbox NO_CAPTURE ack=4682.
- `overnight-apk-build.ps1 -Tester` detached 00:31: SCHEMA_PARITY_OK 00:31, APK_OK 00:38 (463MB), R2_PARITY_OK targets=Android,StandaloneWindows64,WebGL objects=271, APK_DONE 00:38 (`Builds/overnight-apk-status.txt`).
- Installed via `install-apk-to-seeker.ps1 -Build:$false -Install:$true`: dumpsys versionCode=357453 versionName=2026.09.06.357453. Firebase App Distribution release 0kka4h6t9u400, testers notified.
- Board: the 11 Status lines stamped with the build + BOARD.html regenerated, commit `b22cb98cd` (with the ProjectSettings stamp 357453).
- OPEN: section 3 step 4 screencaps NOT taken - the Seeker sat on its keyguard (`mDreamingLockscreen=true` after a wake + swipe); needs the owner's unlock. The four `proof/` deletions in the working tree were there before this session and are untouched.

### 7b. 00:53-00:57 - the five screencaps (owner unlocked the phone; adb taps drove the UI)
Saved under `logs/device/screens/seeker-357453-*.png` (untracked, sent to the owner in-session). Seen on 357453:
- Heart plate: `Heartfire 3/3 (raids)` with three ember-medallion flame icons (WO-1419 live). Wave 5 timer + Start Now; 'Prepare the realm' line (WO-1407).
- Manage hub -> Buildings: Forge row with its BUILDING portrait (WO-1418 art drop live); Forge reads `LEVEL 0 / Upgradable / 57s . Short`, upgrade 1060 wood + 680 iron. The ladder list box shows ~2.3 rows before scrolling.
- Manage -> Troops: Footman L2, `Train one: 45s . Ready`, TRAIN 1 FOOTMAN primary; the upgrade subtitle truncates: `4m 30s . Ready . L3 unlocks Sweepi...`.
- Journey: deck subtitles with state (WO-1404 live): `Quests 0 active . 0 ready to claim`, `Raids Army 8 / 10 . train to open a camp`.
- Raids: spoils line per row (WO-1402 live). The Forsaken Camp row reads `LOCKED - needs Army 9`, yet tapping it OPENS Deploy with BEGIN ASSAULT live - logcat `[Flow:Raid] deploy readiness snapshot: deployableSlots=8 queued=0 required=3 cap=10 ready=True firstRaidSoftGate=True` while the list VM logged `army=unknown`. The list lock and the deploy gate disagree - a ticket to mint after the owner's felt-test (which one is the ruling?).
- Raid Deploy: Grom / Sylas hero medallions are BLANK (no portrait); the Echo quote truncates at `where i...` (WO-1403 two-line authoring in EchoGuideService).
- F8 seq 4683 on this boot: the wallet's silent reauthorize was REFUSED by com.solanamobile.wallet in 0.1s and WalletService reported a 30s TIMEOUT -> WO-1420 (`defe8a569`), READY. Ask the owner to tap Connect Wallet once on this build (section 4 of the WO).

## 8. 2026-09-06 overnight - the Manage relayout wave (owner asleep; run to completion)
**Owner rulings tonight, verbatim:** *"for research for troops and for defense, I want them all to match the same structure of how buildings looks under manage"*; *"under journey, please remove dungeons season in realm map"*; on the second door, **"Keep one door, but name what's behind it"** (AskUserQuestion); on the seat model, *"somebody from Fable to oversee this and answer the harder deeper logic questions, and then hand off the assignments to Opus or even Haiku if they're just repeatable tasks"*. Codex is NOT in the loop tonight - the lanes are Opus agents held to the Codex standard.

**Two WOs minted (55aafc385), banner -> 1423:**
- **WO-1421** Journey deck drops Dungeons / Realm Map / Season. Code-only in `PlayerDeckWorkspace.cs`; `PublicNavigationRetirementRegression` re-points presence -> absence (it has now flipped twice and is marked NEVER DELETED); `HudStrings` keys + both canon-strings copies stay DORMANT (deleting one breaks HudLabelFit Case 1). Realm Map and Season panels become owner-accepted orphans; Dungeons keeps the world portal.
- **WO-1422** Defense, Research and Troops take the Buildings workspace; the paged list is DELETED. Lanes split by FILE (VM / Panel / suites / capture) because all three tabs live in the same two files.

**The rulings the lanes could not make (all in WO-1422 section 3):** Defense rail is per TYPE not per placed instance (per-instance is unbounded on wall segments, and the code already targets the first instance at the lowest level); Research rail is per PERK (17) with all four state words including the two the browse list hides; the pager dies because `AddBrowseRow` has exactly ONE call site and would otherwise be dead code under a green pin; the second door is renamed per tab and NULL when nothing is behind it (Farm authors 0 perks, so it ships with one CTA).

**Measured evidence, not assumed:** `capman4` (MANAGE_OPERATIONAL_CAPTURE_OK 12/12, 01:26) - Defense paints an empty state, Research is the paged list clipped mid-row by CLOSE. Device frames for Buildings/Troops/Journey are in `logs/device/screens/`.

**⚠ TWO OF MY OWN CLAIMS WERE WRONG AND WERE CORRECTED BY A LANE (section 11B):**
1. I wrote that the Defense frame was empty because `BuildDefenseBrowse` skips a ceiling-capped entry. FALSE. The bail is `ManageScreenVM.cs:821` - `CatalogBootstrap`'s `[RuntimeInitializeOnLoadMethod]` never fires under `-executeMethod`, so `CatalogRegistry.Get` returns null for every BaseLayout row. Seeding alone would have changed nothing; the fixture now hydrates the catalog.
2. The WO quoted the research job id `building-research:arcane-tower:warding`. `warding` is NOT a perk id - the authored id is `arcane-warding-runes`. `IsResearching` compares the whole job id, so **the Researching state has been unreachable in every capture ever taken.** Corrected in the fixture.

**Art:** `docs/ART_REQUEST_2026-09-06_manage_tab_portraits.md` (41976a7ce) - 62 files, 12 of them re-cuts, for the Codex art seat. It names the real trap: two portrait conventions exist and only `Portraits/Buildings/` is tier-aware, so `archer-tower-2.png` sits on disk unreachable by any Manage loader.

### 8b. 02:20-02:50 - landed, gated, on the device
**Commits:** `9ad5c7e3c` (both WOs, five lanes) -> `230dd6b9a` (board + RESULT files) -> `5920ea35c` (the fixture grammar fix).
**Build `2026.09.06.357569`** is on the Seeker and on Firebase App Distribution. ⚠ It PREDATES `5920ea35c`, so the Defense queue-band name/art fix is NOT in it - a final build follows.

**Gates, fresh logs, marker-judged:** COMPILE_GATE_OK (c19 02:43), REGRESSION_OK **393/393 -- 393 green, 0 red, 0 skipped** (r18 02:46), MANAGE_OPERATIONAL_CAPTURE_OK 12/12 touch=clean (capman7 02:44). Six oracles proven RED then GREEN (rRED1/2/3); the tree was committed first so every mutation was undone with `git checkout --`.

**⚠ THE FIXTURE WAS LYING, AND IT HID THREE REAL BEHAVIOURS.** `SeedManageCaptureQueue` enqueued `tower_ground_archer:7:0`. `PlacedUpgradeKey.Compose` is the ONLY composer in the tree and emits `<itemId>@<cellX>_<cellZ>`; `TryParse` requires that `@` and rejected the colon form outright. So: the BUILDING NOW band could not resolve a name or art; the Archer Tower card read `Upgradable` while its own job ran (`HasPlacedBuilderJob` matches the key exactly); and the rail never showed its Building state. **All three were correct code behind a fixture speaking a language the game does not.** Fixed by calling `PlacedUpgradeKey.Compose` in the seed. There is now an unconditional `[Flow:Manage] BUILDING NOW band:` trace naming tab / jobId / buildingId / what resolved / art / which label won, so the next capture proves this instead of a seat inferring it.

**Still open, measured on the device, NOT hidden:**
1. The locked Research card paints TWO half-width faces and both truncate - `UPGRADE THE BUILDING T...` / `UPGRADE CATHEDRAL OF M...`. A lock REASON is a sentence and does not belong on a button. A lane is on it.
2. Rail sub-lines truncate on the real town (`Cathedral of Magic ....`).
3. The BUILDINGS tab's own queue band still resolves `<none>` when its first Builder job is a placed structure - the new trace says so out loud.
4. The longest Research names still ellipsize at the 26px floor; Buildings shares this.

**Her Defense tab on the real save shows LUMBERYARD and FOUNDRY, not towers** - which is exactly open question 1 in the WO (storage containers carry an upgrade ladder, so they qualify). She rules on membership; nothing was changed.
