# READY silos - dispatch table, 2026-09-04 (22:32 CDT snapshot)

Dispatcher output per `docs/TICKET_PIPELINE.md` "Standing three-role loop" (owner directive
2026-09-04). Read-only: every fact below was read from the WO file, the tree or a log THIS
session; nothing here is a status flip. The CLI validates each lane's cited evidence at source
before it starts (CLAUDE.md s11B), gates once per wave, commits by explicit path, and is the
ONLY seat that holds the Unity lock. Agents are edit-only or read-only and never gate/commit/build.

## 0. Snapshot facts that change the table

- **Selection rule:** first `**Status:**` line contains `READY TO IMPLEMENT`, not SUPERSEDED /
  CLOSED / DONE / FIXED / BLOCKED; `.RESULT`/`.PLAN`/`.VERIFICATION` excluded. First pass (22:12)
  returned 24; re-read at 22:32 returned **22** - WO-1378 and WO-1380 flipped to FIXED in the
  working tree at ~22:22 (uncommitted edit: `git diff WorkOrders/WORK_ORDER_1378_*.md` shows
  `-READY TO IMPLEMENT / +FIXED - in build 2026.09.05.355872`). They are listed in s6 as
  "left the queue while this was written", not dispatched.
- **The working tree is DIRTY with the CLI's in-flight commit** (`git status --short`, 22:20):
  `ObsidianQueueHud.cs`, `ManageScreenVM.cs`, `BuildTimerService.cs`, `BuildTimerConfig.cs`,
  `OfflineHarvestService.cs`, `OfflineHarvestResult.cs`, `WelcomeBackPopup.cs`,
  `ArenaOutcomeRelay.cs`, `BattlePassService.cs`, `ElarionUiKitObsidian.cs`,
  `MainCastleEnvironmentDressing.cs`, `DataRegression.cs`, `RemoteTunablesDefaultsRegression.cs`,
  `scene-configs.json`, `canon-strings.json`, `guide-content.json`, `structures-catalog.json`,
  `waves.json`, `buildings.json` (both Resources and StreamingAssets copies) + untracked
  `BaseLayoutRoundTripRegression.cs`. **Any lane that touches one of those files starts only
  after the CLI commits that tree** (marked `after-commit` below). Two READY lanes are affected:
  WO-1368 (`ManageScreenVM.cs`) and WO-1379 (`scene-configs.json`).
- **Shared merge files - CLI-owned, excluded from the disjointness test.** Four files are
  touched by almost every lane as a one-line registration and would otherwise serialise the
  whole board: `Assets/Editor/Regression/DataRegression.cs` (suite registration),
  `Assets/_Modules/Core/Ops/RemoteTunables.cs` + `Assets/_Modules/Core/Ops/RemoteTunablesService.cs`
  + `api/_lib/tunables.js` + `Assets/Editor/Regression/RemoteTunablesDefaultsRegression.cs` (the
  tunables rail: KEY_FACTS "all four sources change in the SAME commit"). Convention proposed here:
  **agents author the suite file / name the tunable key + default in their handback; the CLI adds
  the registration lines at batch-gate time.** If the CLI prefers agents to edit these directly,
  the lanes marked `[rail]` / `[reg]` below collide and must run one at a time (s5 shows the pairs).
- **Owner ruling 2026-09-02 (KEY_FACTS):** "the Android APK is the priority. Pi is PARKED." Every
  Pi/WebGL ticket is parked regardless of its own status line. Google Play is Android but is a
  SEPARATE lane behind WO-1362 (`RECON COMPLETE - AWAITING OWNER GO / NO-GO`); its tickets are
  edit-only-now / build-later, ranked below Seeker-felt defects.
- **Ranks:** P0 loses progress/money or blocks play; P1 session pain the owner felt on device;
  P2 polish; P3 tooling/docs.

## 1. WAVE 1 - dispatch now (no owner ruling needed for the WORK; pins that only gate closure are noted)

| # | Silo (s9 lane) | WO | Files the lane touches (as cited by the WO; `[D]` = resolved by the dispatcher at source because the WO named none) | P | Agent brief (one line) |
|---|---|---|---|---|---|
| 1a | Save-State (Combat/AI lane is not it; this is Core/State - own silo) | **WO-1361** player structures vanish from the save | READ-ONLY RCA. Seams from `REGRESSION_COVERAGE_AUDIT s4`: `Assets/_Modules/Village/BuildMode/BaseLayoutLoader.cs:140-170,258-301`, `Assets/_Modules/Core/State/SaveMigrator.cs:308,643-645`, `Assets/_Modules/Core/State/GameStateService.cs:441,582,706,1250`, `Assets/_Modules/Village/BuildMode/BuildModeController.cs:513-523`; logs `logs/device/*.log` (`grep "Enter build mode CENSUS"`). WO text: "NO fix before that line exists" - **ticket defect: names no file to edit, by design.** | **P0** | Read the 08-19/08-20 (0/0/8) and 09-03 (9/9/17) census captures + the two `return null` drop points in `BaseLayoutLoader.Spawn`; prove from data whether one defect or two and WHICH seam drops the 8; hand back the proving line + the bounded fix (no edit). |
| 1b | Combat/AI + HUD (world clock) | **WO-1369** game-over WorldHold never released, hard freeze | `Assets/_Modules/Core/UI/WorldHold.cs` (liveness probe REQUIRED on `AcquirePlayerOwned`; `ReleaseAllForSceneLoad` wired or doc sentence deleted, `:369-372`), `Assets/_Modules/Village/Heart/GameOverScreen.cs` (`:79,89,110,126,131-132,246,264,276,283`), `Assets/_Modules/HUD/Kit/HudKitController.cs` (`combat-item-picker` hold; re-verify - `:4070` already carries a comment about that hold surviving deactivation), `Assets/_Modules/Village/UI/EndState/EndStateView.cs:201,2124-2140` (duplicate HeroDeath raise), `Assets/_Modules/HUD/BugReportView.cs` + `Assets/_Modules/Core/Diagnostics/BreakCaptureHarness.cs` `[D]` (the `bug-report-form` / `f8-note-capture` holders the WO names by hold-id only); NEW oracle `Assets/Editor/Regression/<PlayerOwnedHoldLifetime>Regression.cs` `[reg]`. DO NOT touch the wallet hold (180 s ceiling stays, WO-1360 s4). | **P0** | Prove RED from `logs/device/freeze-20260904-095249.log` (`effective timeScale 0.00` for 2m07s), then: probe-required acquire, GameOverScreen owns its own release + Unhook, audit the six WO-1360 holds, oracle that fails if any PlayerOwned hold can outlive its owner. |
| 1c | Save-State / Economy (read-only half) | **WO-1371** new game inherits collector fill (14,089) | DIAGNOSIS FIRST: `Assets/_Modules/Core/State/GameStateService.cs` `ResetToNewGame` (`:1117,1156,1223,1249,1259`), `Assets/_Modules/Village/Buildings/Progression/CollectorStackView.cs`, `Assets/_Modules/Village/Harvest/OfflineHarvestService.cs` `[D]` (dirty in tree - read at HEAD, edit after-commit). **WO says "establish the real storage before writing the reset" - the fix file is not yet known.** | **P1** | Reproduce the inheritance headless (new game after a filled save), name the field/timestamp that carries fill across `ResetToNewGame`, hand back the proving line; the RESET itself waits on the s3 ruling (zero vs seeded). |
| 1d | VFX/Audio | **WO-1327** fireball bounces back to caster (bounced AGAIN 09-04: "red glowing orb stayed at me") | `Assets/_Modules/Village/Vfx/VFXManager.cs` + `Assets/_Modules/Village/Vfx/VFXManager.Hovl.cs` `[D]` (the ONE spawn owner - clamp `collision.bounce/dampen/minKillSpeed/collidesWith` at spawn since the prefab is gitignored); tunable key `[rail]`. WO names the seam by symbol only - **ticket defect on paths.** Owner pin: change BEHAVIOUR only, never restyle/recolour/swap the prefab. | **P1** | The 09-03 fix clamped LIGHTS (25 -> 4) and the owner still sees the orb return: clamp the CollisionModule at spawn (bounce 0, dampen 1, minKillSpeed > 0, collidesWith = enemy/world only) in the one spawn owner; expose as `vfx.fireball.*` keys; capture inside the walls proves it dies. |
| 1e | Combat/AI (raid door) | **WO-1379** Heartfire replaces Raid Orders - REMAINING HALF ONLY | The charge pool + HUD flames LANDED (`1ef5f6ad4`: `HeartfireService.cs`, `HeartfireCharges.cs`, `HudKitController.cs:137-142`). Remaining per the tree's board note: `Assets/_Modules/Village/Hero/RaidSelectionScreen.cs:527` (`RaidCooldownService.IsOnCooldown(id)` still walls the card tap), `Assets/_Modules/Village/World/Camps/RaidCooldownService.cs:259,275` + callers, `Assets/Resources/Data/Canonical/scene-configs.json:107,170,225` + `Assets/StreamingAssets/Data/Canonical/scene-configs.json` (mark `raidCooldownSeconds` superseded, never shorten) - **after-commit** (scene-configs is dirty). | **P1** | Retire the per-camp wall at the ONE door so Heartfire is the only gate ("two lockouts reads as a bug"); mark the field superseded in both JSON copies; `[heartfire]` suite stays green. |
| 1f | World/Environment (gear seating) | **WO-1215** shield seats at identity through the body (bounced 09-03) | `[D]` deriver candidates: `Assets/_Modules/Core/Geometry/WeaponOrientHelper.cs`, `Assets/_Modules/Village/Hero/AttachmentOffsetRegistry.cs`, `Assets/_Modules/Village/Hero/RigAttachmentRegistry.cs`, `Assets/_Modules/Village/Hero/GearVisualApplier.cs`; data `Assets/OffsetForge/offsets.json` (gap-fill rows only; `manual = true` rows are CANON). Mandatory pre-read `docs/WEAPON_ARMOR_ORIENT_LOGIC.md`. **WO names no code file - ticket defect.** | **P2** | Extend the existing seat derivation so the 18 shields with no offset row get a seat from `mesh.bounds` (Read/Write may be OFF); never flip `_sheatheLongAxisSign`; closure needs a device screenshot (CLI build). |
| 1g | Monetization/Backend (Arena) | **WO-1366** arena wager currency per channel | `Assets/_Modules/Village/Arena/ArenaWalletService.cs` (`:2,19,38,41`), `Assets/_Modules/Village/Arena/ArenaCatalog.cs:48,87,101,114`, `Assets/_Modules/Core/Platform/CurrencySkinResolver.cs:96,239,267,271-313`, `Assets/_Modules/Core/Platform/CurrencySkin.cs:130`, `Assets/_Modules/Core/Payments/PaymentChannelResolver.cs:18,21,27` (EXTEND, never a parallel resolver); tier tunables `[rail]`. Do NOT touch `arenaDefense` (v19) / `ArenaProgress` (v34). | **P2** | Make the wallet currency-agnostic (SKR on dApp, Crystals on Play), never carry the 500 stub into Play, tiers on the rail with today's values as defaults; artifact scan for `SKR` literals is a CLI build step. |
| 1h | Monetization/Backend (Play gate) - ONE agent, two WOs in sequence (same file) | **WO-1364** then **WO-1363** | 1364: `Assets/Editor/Regression/GooglePlayPackagingGate.cs:20-45,167-179` + `tools/android/assert-google-play-aab-clean.ps1:16-35` (byte-identical pair, ONE edit), `Assets/Editor/Regression/GooglePlayPackagingRegression.cs:105-129`. 1363: `Assets/_Modules/Core/UI/SkrShowcasePanel.cs:77,154,172`, `Assets/_Modules/Core/UI/StakeRewardsPanel.cs:193`, `Assets/_Modules/Onboarding/TitleController.cs:348,353`, `Assets/Editor/GooglePlayContentExclusion.cs:128-156,203-273` (SHARED with 1364 - why one agent), `canon-strings.json:197,202-207,231`, `en.json:141,248`, `siege-stakes.json:2`, `ad-placements.json:14,76` (both Canonical copies; canon-strings is dirty -> **after-commit**). Do NOT touch `Assets/_Modules/Village/Arena/*` (1366's lane). | **P3** (behind WO-1362 GO) | Widen the gate first so the dirty AAB on disk emits `PLAY_ARTIFACT_DIRTY`, then compile the literals out under `#if !GOOGLE_PLAY` and re-point the exclusion catalogs; the proving AAB build is a CLI step (no throwaway AAB). |
| 1i | Monetization/Backend (ship chain, .ps1) | **WO-1365** AAB has no ship chain | NEW `tools/<aab-wrapper>.ps1` calling `tools\r2-ship.ps1` (never re-inline), `Assets/Editor/AndroidBuild.cs` (`:10` stale `6000.4.7f1` comment; version stamp `:350-365` is parameterless), `docs/CLI_OPERATIONS_RUNBOOK.md` build table (same commit). | **P3** | Author the blocking wrapper + `-MeasureOnly` size guard (bundletool `get-size`, ceiling as a parameter, never settle MB-vs-MiB); the real AAB build is a CLI/Unity-lock step. |

**Wave-1 parallelism check:** 1a and 1c are read-only. 1b/1d/1e/1f/1g/1h/1i touch pairwise-disjoint
files once the four merge files are CLI-owned (s5). 1b and 1e both touch `HudKitController.cs`?
NO - 1e's HUD half already landed; 1e is confined to `RaidSelectionScreen.cs` / `RaidCooldownService.cs` /
`scene-configs.json`. 1c reads `GameStateService.cs` that 1a also reads - both read-only in this wave.

## 2. WAVE 2 - after wave 1, or needs the Unity lock (CLI-triggered, serialised)

| # | Silo | WO | Files | P | Why wave 2 |
|---|---|---|---|---|---|
| 2a | Save-State | WO-1361 FIX | whichever seam 1a proves (`BaseLayoutLoader.cs` / `SaveMigrator.cs` / `GameStateService.cs`) + the PlayMode oracle for audit seams 1-2 | P0 | Fix is forbidden until 1a's proving line exists; PlayMode oracle needs a scene = Unity lock. Owner felt-verifies across a relaunch. |
| 2b | Save-State / Economy | WO-1371 FIX | the storage 1c names + `GameStateService.ResetToNewGame` + oracle `[reg]` | P1 | Needs the s3 ruling (zero vs seeded) AND 1c's finding; shares `GameStateService.cs` with 2a -> one agent or serialised. |
| 2c | HUD | WO-1368 Manage builds zero queue rows (money path) | `Assets/_Modules/Village/UI/Manage/ManageScreenPanel.cs:1737,1863-1866`, `Assets/_Modules/Village/UI/Manage/ManageScreenVM.cs:489-540` (**dirty in tree - after-commit**), `Assets/Editor/Regression/ManageQueueDrawerRegression.cs:27-29` (reconcile, never delete), NOT `Assets/_Modules/Core/UI/QueueRailView.cs` | P1 | `ManageScreenVM.cs` is in the CLI's uncommitted diff AND the WO's own s0 says the original diagnosis is refuted - the CLI must confirm the in-flight `ObsidianQueueHud`/`ManageScreenVM` edit did not already move the verbs before an agent is briefed. Owner design pin in s3. |
| 2d | HUD (EndState) | WO-952 gear drop tips the arena victory panel (93.3%) | `Assets/_Modules/Village/UI/EndState/EndStateView.cs` (`MaxPanelHalf :236/:391`, `CompressFailBelow :~1379`, `NarrativeStripAt`, `RequiredBodyPxAt`, `BuildBody`, `ProbeFit`), `Assets/Editor/UICaptureLaunch.cs:526,548`, `Assets/Editor/Regression/EndStateBodyFitRegression.cs` + `COMPRESSED`-absence oracle `[reg]` | P1 | Shares `EndStateView.cs` with 1b (WO-1369) -> after 1b lands. Closure = `RunCaptureHeadless` at 2670x1200 / 2340x1080 / 1920x1080 with PNGs OPENED = Unity lock. |
| 2e | VFX/Audio + Tooling | WO-1348 VFX picks tunable from the Command Center | `Assets/Editor/VfxManualPicks.json` (stays the default/record), consumers `[D]` from the tree: `Assets/_Modules/Village/Vfx/VFXManager.Hovl.cs`, `AmbientAuraPolicy.cs`, `NightStoreAuraSelector.cs`, `FtueWorldPointer.cs`, `Assets/_Modules/Village/Hero/HeroAbilities.cs`, `WeaponVfxMap.cs`, `ArcaneTower.cs`, `Enemy.cs` (agent enumerates from the tree, WO refuses to); rail `[rail]` + Command Center editor + `client_tunables` schema. Never `api/_lib/purchase-catalog.js`. | P3 (owner iteration speed; not player-felt) | Its hold ("until WO-1343..1347 land and the tree is gated") is SATISFIED - 1343/1344 CLOSED 09-04, 1345/46/47 FIXED and shipped `2026.09.03.353999`, tree gated 09-03 20:13 - but it shares `VFXManager.Hovl.cs` with 1d (WO-1327), so it follows 1d. |
| 2f | World/Environment (Unity lock) | WO-1292 Synty environment dressing - EXECUTION | code LANDED (`33ba9c966` + a 4-line uncommitted tweak to `Assets/Editor/MainCastleEnvironmentDressing.cs`). Remaining = run `DeNelle.Editor.MainCastleEnvironmentDressing.Run` on `Main_Castle_Overworld` (builder, never hand-edit), NavMesh re-bake (`CastleGateNavVerify`, `TROOP_WALL_NAV_OK`), Addressables content build + `tools\r2-ship.ps1` (`R2_PUSH_OK` + `R2_PARITY_OK` on a FRESH log), `RunCaptureHeadless`. Never touch `Assets/Generated/Terrain/**`, the perimeter (1290), `structures-catalog.json` (1291). | P2 | Status still says BLOCKED ON WO-1291 (IN PROGRESS, 30/33 addresses). Pure CLI/Unity-lock work; no agent. |
| 2g | Monetization/Backend (Unity lock) | WO-1363 + 1364 + 1365 + 1366 PROOF | one Play AAB via the 1365 wrapper; unzip; scan `base/assets/bin/Data/Managed/Metadata/global-metadata.dat` (shared acceptance of 1363/1364/1366); `AAB_SIZE_*` marker | P3 | One build proves four tickets ("no throwaway AAB"); behind WO-1362 GO. |
| 2h | Tooling (Unity lock) | PROD-021 R2 catalog never pushed (Windows) - CANDIDATE CLOSE | `tools/r2-ship.ps1:177-182` (fix verified in the WO's 09-02 section: enumerates every `ServerData/*` target), `.githooks/pre-push`. Remaining = falsification run + fresh exe/device run. | P0-class when live; today: verify-and-close | Editor must be CLOSED for the content push. The 09-03 evening `R2_PARITY_OK targets=Android,StandaloneWindows64,WebGL objects=266` (KEY_FACTS) is the evidence the CLI should quote; then PO closes. Doc typo to fix in the same pass: the WO writes the sanctioned path as `tools2-ship.ps1` twice. |
| 2i | Retention (Backend + HUD) | WO-1376 P2 retention around the loop | `Assets/_Modules/Core/World/DungeonStatusCatalog.cs:20-48` (fail-closed gate - VERIFY `/api/dungeon-status` serves `open` rows first), `Assets/_Modules/HUD/PlayerDeckWorkspace.cs:588-624` (Journey deck 2 -> 5 cards), `Assets/Editor/Regression/PublicNavigationRetirementRegression.cs` (re-point), rail `[rail]`, suites `[reg]`. Spec = `docs/PROGRAM_RAID_ECONOMY_2026-09-04.md` (deliberately not restated). **Thin on paths for its size - split before dispatch:** (i) Journey nav cards, (ii) weekly ladder, (iii) dungeon rewards, (iv) troops-in-wave-defence (owner: "Not P0, though" - split out). | P2 | "sequenced AFTER WO-1375" - 1375 flipped to FIXED tonight (build `2026.09.05.355872`, on the Seeker 22:22) and awaits the owner's felt-test; 1376 starts when she passes it. |

## 3. PARKED / NEEDS RULING - one word from the owner unblocks each

| WO | What is needed (quoted from the WO) | Suggested one-word answer form |
|---|---|---|
| **WO-1373** raid rewards + rough stone chain (P1, core loop) | "**Which shape do you want?** ... (A) raids drop rough stone directly, (B) raids drop it but dungeons keep a GRADE advantage, (C) raids drop a different tier-up material ... this is a design call and it is hers. Do not implement until she has picked." Also "can a player wear more than one at once, and do they stack? ... **ASK.**" | "A" / "B" / "C", then "stack: yes/no" |
| **WO-1377** crypto identifiers in IL2CPP metadata (Play) | "if the identifiers cannot be moved safely, is a Play artifact that contains the *type name* `IJupiterService` - with no reachable code, no string copy and no UI - actually a policy problem? **That is a judgement call ... and it is hers.**" Blocked on s4 (renaming `PaymentChannel.SolanaDappStore` / `SkinAuthMode.SolanaWallet` "silently breaks every existing player's save on read" if stored by name). | "type-name OK" or "move it" (then the save-format proof runs first) |
| **WO-1371** new-game collector fill (fix half) | "**Decide the correct new-game state and say so**: zero fill, or fill seeded to the same starting point a founding town gets. ... it is the OWNER'S - ask, do not pick." | "zero" or "seeded" |
| **WO-1370** harvest result modal unreadable (`Assets/_Modules/Core/UI/HarvestOverflowModal.cs:55-60`, copy + layout only, 1 file, P2) | "SUGGESTED SHAPE - FINAL WORDING IS THE OWNER'S (SAMANTHA.md rule 8) ... Offered as a starting point to react to, NOT to implement unasked." | "use it" (the suggested copy) or her own two lines - then it is a 1-file wave-1 lane |
| **WO-1368** zero queue rows (fix half) | "The verbs must return somewhere that does not re-create the overflow - most likely INSIDE the drawer, next to the rail ... **That is a UI design question and it is the owner's.**" | "in the drawer" (default) or where |
| **WO-1244** command center console (bounced 09-03 "i want a seperate ticket as a complete command center as i envisioned in the docs") | Two owner actions: (1) "`ADMIN_OPS_KEY` IS NOT SET ON THE DEPLOYMENT ... It must be a DIFFERENT value from `ADMIN_DASH_KEY`" - set it on Vercel; (2) the bounce is a SCOPE statement, not a defect - which WO-1169 surfaces are missing needs her list before anyone codes (s11: ambiguous tickets bounce for detail). Backend lane (`api/admin/ops.js`, `api/admin/stats.js?view=ops`, static page under `api/`), file-disjoint from every Unity lane. | "key set" + the missing-surface list |
| **WO-1184** lookout horde warnings (bounced 09-03) | Owner: "right now its a red dot middle of screen, Have UI refine to maybe vibration and pulsing warning" - routed to the UI seat for a refined spec. Code seams `[D]`: `Assets/_Modules/Village/Waves/AlertIntelSystem.cs`, `Assets/_Modules/Village/Siege/RoamingHordeNotifications.cs`, `Assets/Editor/Regression/LookoutAlertRegression.cs`. **WO names no file - ticket defect.** Capture pin: prove whether the LOOKOUT REPORT renders in a player build before rebuilding it. | UI-seat spec lands -> wave-2 HUD lane (P2) |
| **WO-1292** Synty dressing | "BLOCKED ON WO-1291 - dress last, once the buildings set the language" (1291 IN PROGRESS 30/33). Code is landed; only execution remains (2f). | "dress now" or wait for 1291 |
| **WO-1376** retention | "sequenced AFTER WO-1375" - 1375 is FIXED on her Seeker tonight; her felt-pass releases 1376. | 1375 pass/fail |
| **WO-1314** WebGL payload vs 512 MB heap | "**PARKED 2026-09-02 by owner ruling - the Android APK is the priority. Pi work resumes on her word.**" Also needs her word on `meshCompression: 1` for `Ranger.fbx.meta` / `Mage.fbx.meta` ("touches Android too"). Files otherwise: `Packages/manifest.json` (`com.unity.ai.inference`), WebGL rebuild + `r2-ship`. | stays parked |
| **PROD-021** | Not a ruling - a CLOSE: "close it only after the falsification run and a fresh device/exe run" (2h). | PO closes |
| **WO-1348** | No ruling; its hold is satisfied (2e). Listed here only so nobody re-holds it. | - |
| **WO-1365** | No ruling for the work. Only the MB-vs-MiB empirical settle "is an outward-facing action and needs the owner's word" - not needed to ship on the conservative reading. | - |
| **WO-1366** | Only "if you believe the Play side should grant something for a stub balance ... ask, do not decide" - the lane's default (grant nothing) needs no ruling. | - |

## 4. Backlog candidates from tonight's audits with NO ticket (mint from the banner; do not number here)

Checked against `WorkOrders/*.md` by token grep (`intermediate texture`, `RGBA32`, `FlowTrace.Enabled`,
`Sustained|thermal`, `AdConsent`, `Core/Payments`, `KnownHollowSites|hollow`, `maxConcurrentRequests`,
`WaitForCompletion`, `VfxPerfGate|ambient ring`, `PerfReporter`, `renderScale`, `PlayMode`). Hits that
already cover an item are named; the rest have no ticket.

| Proposed WO title | Source | Files | P |
|---|---|---|---|
| Town frame baseline 21 ms: split CPU-main / render / GPU with one USB Profiler capture at 0 enemies | PERF D1 | none (measurement WO; `adb shell dumpsys gfxinfo ... framestats`, Development Build) | P1 |
| Ambient VFX loops never shed in town: 8-11 live loops = ~14 ms; make the ambient ring shed on the existing shed path + `vfx.*` tunables | PERF D2 (A10 `Shed level None (ambient ring 8/8)`) | `Assets/_Modules/Village/Vfx/VfxPerformanceGate.cs`, `Assets/_Modules/Village/Vfx/VFXManager.cs:184,187`, rail | P1 |
| Release build logs 257 lines/s with stack traces: remote flag for `FlowTrace.Enabled` (calls STAY, s12) + `m_StackTraceTypes` to None for Log in release | PERF D3 | `Assets/_Modules/Core/Diagnostics/FlowTrace.cs:46,365-369`, `ProjectSettings/ProjectSettings.asset:59`, `Assets/Editor/MobileSettings.cs`, rail key | P1 |
| Town fps regressed 40.7 -> 35.8 -> 29.3 at 0 enemies across three builds: bisect with the APKs on disk | PERF D4 | none (`install-apk-to-seeker.ps1 -Build:$false`, `[Flow:Perf]` 5 min each) | P1 |
| Renderer intermediate texture forced ALWAYS; `MobileSettings.cs:537-538` sets it on the wrong asset | PERF D6 | `Assets/Settings/DeNelle-UniversalRenderer.asset:56`, `Assets/Editor/MobileSettings.cs:537-538` | P2 |
| Thermal status 3 with no in-game response: Sustained Performance Mode trial + an Adaptive Performance provider (URP flag is INERT today) | PERF D5 | `ProjectSettings/ProjectSettings.asset:10`, `Packages/manifest.json`, `Assets/Settings/DeNelle-URP.asset:79` | P2 |
| 22 x 6 MiB RGBA32 NPOT UI sprites (`Resources/UI/ElarionMedieval/*`) + two 4096 particle sheets: Android overrides | PERF D7 (A31/A32) | `.meta` files under `Assets/Resources/UI/ElarionMedieval/`, `Mirza Beig/.../*.png.meta:104-109` - **partly covered by WO-1367 (IN PROGRESS, RpgUi texture pass); confirm its scope includes ElarionMedieval before minting** | P2 |
| 4.5 s first frame every launch + 7.2 s `StructureAssets` warm: Profiler on boot; `m_DisableCatalogUpdateOnStart` | PERF D8 | `EnemyContentWarmer.cs`, `StructureContentWarmer.cs`, `AddressableAssetSettings.asset:96` | P2 |
| Sync `WaitForCompletion` on uncached remote content (5 sites) + unbounded `assets.maxConcurrentRequests` off-Pi | PERF D9/D10 | `Core/Addressables/HeroAssetLoader.cs:95`, `HeroTextureLoader.cs:79`, `AudioAssetLoader.cs:158`, `EnemyEditorSyncResolver.cs:73,88`, `RemoteTunables.cs:596-611` (PROD-010 closed; this is the residual) | P2 |
| PlayMode oracle for WO-1361 seams 1-2 (`BaseLayoutLoader.LoadFromState` count + `Spawn` null drops over REAL ids) | REG s4 | new `Assets/Tests/PlayMode/BaseLayoutReplayTests.cs` + `BaseLayoutLoader.cs:140-170,258-301` | P0 (fold into WO-1361 rather than a new number) |
| `Core/Payments` (10 of 14 files) + `_Modules/GooglePlay/` have no oracle on the live money path (receipt settlement + grant applier) | REG s6 #3 | `Assets/_Modules/Core/Payments/*`, `Assets/_Modules/GooglePlay/*`, new suite | P1 (real money is live) |
| 27 ledgered hollow-pass sites owed a resolution (`KnownHollowSites`) | REG s3 / s6 #4 | `Assets/Editor/Regression/RegressionMarkerRegression.cs:248-307` + each named suite | P3 |
| Shape-B "OK, 0 checked" is advisory only - contract change so an empty discovered collection can red a suite | REG s6 #5 | `RegressionMarkerRegression.cs:1107-1136` + ~150 suites | P3 |
| `AdConsentService` has no test (consent gating = legal exposure) | REG s6 #6 | `Assets/_Modules/Core/Monetization/AdConsentService.cs`, new suite | P2 |
| Camera / cinematic / night-light controllers: zero oracles; Audio 4 suites for 23 files; `Village/Hero` one-third unnamed | REG s6 #8-10 | `CinemachineCameraController`, `DragonCinematicFlyby`, `NightTorchLightSystem`, `BattleMusicManager`, `TowerAudioController` | P3 |
| Three un-ledgered hollow candidates | REG s3 | `QuestCompletabilityRegression.cs:1521-1525`, `DataWebRegression.cs:626,629`, `BuildEconomyRegression.cs:1577-1581` | P3 |
| `Builds/test-results-EditMode.xml` is 4 days stale (1033 passed, 08-31): EditMode run in the check-in gate | REG s5 | `tools/regression/checkin_gate.ps1` | P3 |

## 5. Conflict matrix - READY WO pairs that share a file

Computed from the file lists above (WO-cited + `[D]`-resolved). Merge files listed separately.

| File | WOs | Resolution in this table |
|---|---|---|
| `Assets/_Modules/Village/UI/EndState/EndStateView.cs` | 1369, 952 | 1369 wave 1; 952 wave 2 |
| `Assets/_Modules/Core/State/GameStateService.cs` | 1361, 1371 | both READ-ONLY in wave 1; fixes 2a/2b serialised (one agent) |
| `Assets/_Modules/Village/Vfx/VFXManager.Hovl.cs` (+ `VFXManager.cs`) | 1327, 1348 | 1327 wave 1; 1348 wave 2 |
| `Assets/Editor/GooglePlayContentExclusion.cs:203-273` | 1363, 1364 | ONE agent, 1364 then 1363 (WO's own order) |
| `Assets/_Modules/HUD/Kit/HudKitController.cs` | 1369, (1379 HUD half - LANDED) | no live conflict; 1379 lane is confined to the raid door |
| `Assets/Resources/Data/Canonical/scene-configs.json` (+ StreamingAssets copy) | 1379, (1378 - now FIXED, in the dirty tree) | after-commit |
| `Assets/Resources/Data/Canonical/canon-strings.json` (+ copy) | 1363, (1378 - now FIXED, dirty) | after-commit |
| `Assets/_Modules/Core/UI/SkrShowcasePanel.cs` | 1363 only (1366 is fenced out of `DeNelle.Core` UI) | none |
| `Assets/_Modules/Core/Payments/PaymentChannelResolver.cs` | 1366, 1377 (rename `PaymentChannel.SolanaDappStore`) | 1377 parked; if released, after 1366 |
| `Assets/_Modules/Core/Web3/*`, `CoreServices.cs` | 1377 only | parked |
| `api/admin/stats.js` | 1244 only (additive) | parked |
| **Merge files (CLI-owned):** `Assets/_Modules/Core/Ops/RemoteTunables.cs`, `RemoteTunablesService.cs`, `api/_lib/tunables.js`, `Assets/Editor/Regression/RemoteTunablesDefaultsRegression.cs` | 1327, 1348, 1366, 1373, 1376 `[rail]` | agents hand back key + default; CLI registers at gate. If agents edit directly: 1327 -> 1366 -> 1348 -> 1376 -> 1373 one at a time |
| **Merge file:** `Assets/Editor/Regression/DataRegression.cs` | 952, 1369, 1371, 1376, 1379, 1378, 1380 `[reg]` | same convention; the file is also dirty in the tree |
| `Assets/Editor/VillageSceneBuilder.cs` (s9 serialisation bottleneck) | **no READY WO touches it** | - |

## 6. Left the queue while this was written / not dispatched

- **WO-1378** fiction + naming pass and **WO-1380** Echo Guides: `READY` at 22:12, `FIXED - in build
  2026.09.05.355872 ... Awaiting owner felt-test` at 22:32 (uncommitted). Owner felt-tests close them.
- **WO-1208**, **WO-978**: first status line is IMPLEMENTED / BLOCKED - not READY, excluded by the rule.
- **WO-1362** (Play AAB programme, untracked file tonight): `RECON COMPLETE - AWAITING OWNER GO / NO-GO` -
  the pin over 1h/1i/2g.

## 7. Ticket defects found (for the RCA-clean agent, not fixed here)

- **No file to edit named:** WO-1361 (by design - fix forbidden before data), WO-1184, WO-1215,
  WO-1327 (symbols only). Dispatcher-resolved candidates are marked `[D]` and must be re-verified at
  source by the lane agent before the first edit.
- **Partial:** WO-1348 (4 of 6 tunable seams by role only), WO-1371 (storage of collector fill
  unknown), WO-1376 (seams only, for a four-part deliverable), WO-1379 (HUD half unnamed - now moot,
  it landed).
- **Stale text:** PROD-021 writes the sanctioned script as `tools2-ship.ps1`; WO-1348's hold condition
  is already met; WO-1365's original RED criterion was invalidated by WO-1367 (the WO says so).
