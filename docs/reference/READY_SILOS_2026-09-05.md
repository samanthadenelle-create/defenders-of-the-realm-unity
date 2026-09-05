# READY silos - dispatch table, 2026-09-05 (07:20 CDT snapshot, morning after the overnight run)

Dispatcher output per `docs/TICKET_PIPELINE.md` "Standing three-role loop". READ-ONLY pass: every fact
below was read from the WO file, the tree at HEAD `e94027216` (working tree dirty only in
`ProjectSettings/ProjectSettings.asset` + `docs/HANDOVER_2026-09-05_overnight.md`), a log, or one
public HTTP GET this session. No Status line was changed; the CLI validates each cited line at source
before a lane starts (CLAUDE.md s11B), gates once per wave, commits by explicit path, and is the ONLY
seat that holds the Unity lock.

## 0. Snapshot facts that change the table

- **Selection rule:** first `**Status:**` line contains `READY TO IMPLEMENT` and is not SUPERSEDED /
  CLOSED / DONE / FIXED / BLOCKED / IMPLEMENTED / SPEC; `.RESULT`/`.PLAN`/`.VERIFICATION` excluded.
  Result: **16 tickets.** `BOARD.html` (generated 07:06) shows **Ready 18** because its Ready bucket
  also folds in the two IN PROGRESS tickets **WO-1367** (RpgUi Android texture pass, CLI-owned) and
  **WO-1291** (Synty building retheme, 30/33). Both are CLI lanes already running; not dispatched here.
- **Gate state at snapshot:** `Builds/regression.log` 07:09 `REGRESSION_OK 383/383 suites`; the fifth
  APK chain of the night started 07:11 on the WO-1397 tree (`Builds/overnight-apk-status.txt`
  `APK_START 07:11:16` -> `SCHEMA_PARITY_OK`). **The Seeker is still off USB** (`Builds/wait-device-install.log`
  `no-device 07:16:45`); the last three builds reached the owner via Firebase App Distribution only
  (356329 / 356357 / 356386). Any "device proof" pin below therefore waits on the phone.
- **Two READY tickets are DISPROVED / already landed at source (the WO-1395 pattern):**
  - **WO-1382** (Manage Troops redesign) - the rebuild is IN THE TREE: `65d5a7eae` (2026-09-04 23:5x)
    landed `TroopWorkspacePx = 260f`, the `TRAINING NOW` band (`ManageScreenPanel.cs:159-175,215,269-274`)
    and deleted `_troopMode` (`git log -S"_troopMode"` last hit is that commit). Oracles pin it:
    `ManageTroopsTrainDoorRegression.cs:311` CASE 6 "TWO VERBS and NO MODE SWITCH",
    `ManageQueueDrawerRegression.cs:216` `[drawer-clear-of-card]`. Proven on her Seeker:
    `docs/qa/UI_REVIEW_2026-09-05/INDEX.md` rows 07 (`troops browse: 9 def(s) -> 2 Train, 2 Upgrade`, PASS)
    and 08 (`Train CTA 'troop-footman'` -> `train job enqueued (45s)` -> `TRAINING NOW rows=1`, PASS).
    **Status still reads READY; no RESULT file.** Verdict: SUPERSEDED - flip to FIXED (build 355952 or
    later), write the RESULT, owner felt-test closes. Not a lane.
  - **WO-1376 gate 1 is PROVEN OPEN, not unknown:** `GET https://defenders-of-the-realm-v2.vercel.app/api/dungeon-status`
    this session -> `HTTP 200`, 258 bytes: `dg_starter_loop / dg_sunken_vault / dg_bonecrypt /
    dg_ember_deep / dg_folks_granary = open`, `dg_healers_cottage = sealed`. The WO's "NOT PROVEN" clause
    and yesterday's ledger pin (1) are answered; the Dungeons card can be built against a live open set.
- **WO-1379's remaining half is CONFIRMED and is worse than the board note says.** `RaidSelectionScreen.cs:550`
  still refuses the card tap on `RaidCooldownService.IsOnCooldown(id)` (line moved from :527 by
  `e99d2f290`); `grep -i heartfire RaidSelectionScreen.cs` = **0 hits** - the Heartfire gate is NOT at the
  door at all. `RaidDeployController.cs:163-171` spends the charge INSIDE the raid scene and logs
  `FlowTrace.Fail("Heartfire", "... ENTERED with an EMPTY Heartfire pool ... Wire HeartfireService.HasCharge /
  BlockedMessage into RaidSelectionScreen.OnCardTapped ...")`. And `scene-configs.json:108,173,230` carry
  `_raidCooldownSecondsSuperseded: "... not consulted at runtime"` - **that sentence is FALSE at source**
  while :550 consults it; `:287` (`iron_bastion`, 43200) has no superseded note. Resources and
  StreamingAssets copies are byte-identical (`cmp` clean).
- **Shared merge files - CLI-owned, excluded from the disjointness test** (same convention as 09-04):
  `Assets/Editor/Regression/DataRegression.cs` (suite registration) and the tunables rail
  (`Core/Ops/RemoteTunables.cs`, `RemoteTunablesService.cs`, `api/_lib/tunables.js`,
  `RemoteTunablesDefaultsRegression.cs`). Agents hand back the suite file / key + default; the CLI adds
  the registration lines at batch-gate. Lanes marked `[rail]` / `[reg]`.
- **Owner lens (KEY_FACTS "THE NORTH STAR MAP", 2026-09-04):** `Collect -> Train -> Raid -> Get richer ->
  Upgrade -> Unlock harder raid -> Get stronger -> Repeat`; *"A ticket that does not serve that loop is not
  on the critical path."* Ranking below puts "reason to raid / desire to play more" first, then
  progress-loss, then session pain, then polish/tooling. PARK list from `OVERNIGHT_ORDERS_2026-09-05.md`
  is honoured verbatim: 1373 / 1377 / 1371 / 1370 / 1368 / 1244 / 1184 / 1292 / 1314 are never dispatched
  without her word (1371 / 1370 / 1368 are no longer READY and appear only in s4).

## 1. The table - every READY ticket

Class: NEW = new feature (route as spec, s13), DEFECT = existing behaviour wrong, INSTR = instrument-only.
Size: S < 1 file-day, M = 1-3 files + oracle, L = multi-seam. Verdict: DISPATCH (morning wave) / AFTER
(sequenced behind a lane) / PARKED (owner word, quoted) / STALE (banner or flip recommended).

| WO | Title | Class | Silo | Files the lane touches | Size | Verdict | Evidence (read this session) |
|---|---|---|---|---|---|---|---|
| **1379** | Heartfire replaces Raid Orders - retire the per-camp wall at the ONE door | DEFECT (half landed) | Combat/AI - raid door | `Assets/_Modules/Village/Hero/RaidSelectionScreen.cs:540-560` (replace the `IsOnCooldown` refusal with `HeartfireService.HasCharge` / `BlockedMessage`), `Assets/_Modules/Village/World/Camps/RaidCooldownService.cs:259,280,296` (+ `BeginAfterClear` callers `RaidVictoryController.cs:249`, `Village2RaidController.cs:232` - keep the stamp, drop the WHEN gate), `Assets/_Modules/Village/Troops/RaidDeployController.cs:163-171` (the Fail line becomes unreachable; keep it), `Assets/Resources/Data/Canonical/scene-configs.json:287` + StreamingAssets twin (add the superseded note to iron_bastion; fix the false "not consulted" wording on :108/:173/:230), `Assets/Editor/Regression/HeartfireRegression.cs` (add the "second WHEN gate reappears" pin from acceptance) `[reg]` | S-M | **DISPATCH - lane A, P0 on the owner lens** | `RaidSelectionScreen.cs:550`; 0 `heartfire` hits in that file; `RaidDeployController.cs:170` names this exact wiring as missing; twins `cmp` identical; `[heartfire]` suite exists |
| **1376** | P2 retention around the loop - Journey five cards + dungeon gate + ladder (troops-in-defence SPLIT OUT) | NEW | HUD - Journey deck (ONE lane with 1394 + 1396) | `Assets/_Modules/HUD/PlayerDeckWorkspace.cs:669-720` (case `PlayerDeckKind.Journey`: add Dungeons / Realm Map / Season via the existing `Route(...)`), `Assets/_Modules/Core/World/DungeonStatusCatalog.cs` (read-only unless the sealed copy needs a card lock reason), `Assets/Editor/Regression/PublicNavigationRetirementRegression.cs:14-21` (re-point, never delete), `Assets/Editor/UICaptureLaunch.cs` (Journey capture gains cards), weekly ladder = separate spec slice `[rail]` `[reg]` | L (split: nav = M; ladder = M; dungeon rewards = M; troops-in-defence = its own WO) | **DISPATCH the navigation slice - lane B** (with 1394 + 1396); ladder + dungeon rewards AFTER lane B; troops-in-defence = mint a new WO (owner: "Not P0, though", PROGRAM s9) | `PlayerDeckWorkspace.cs:669-719` still two Journey cards (Quests, Raids); `/api/dungeon-status` HTTP 200 with 5 `open`; WO-1375 FIXED (gate released); `PublicNavigationRetirementRegression.cs:7,14-21` still asserts absent and is green in 383/383 |
| **1394** | Season Track registered, never opened - Journey "Season" card | NEW (door slice of 1376) | HUD - Journey deck (lane B) | `PlayerDeckWorkspace.cs:669+` (one `Route("Season", ..., PanelId.BattlePass, "season")`), `PublicNavigationRetirementRegression.cs:14-17` (re-point), `Assets/_Modules/Wallet/BattleMonthlyPanelsBootstrap.cs:104` (`OpenSeasonTrack` trace line), `UICaptureLaunch.cs` (new `SeasonTrack` case), canon-strings for the purpose line `[reg]` | S | **DISPATCH inside lane B** | `BattleMonthlyPanelsBootstrap.cs:64` registers, `:93` `RegisterRaidHandler(BattlePassService.OnRaidResult)` (XP feed wired); `grep PanelId.BattlePass Assets/_Modules` = registration only; WO-1375 FIXED so "track that never moves" clause is met pending her felt-test |
| **1396** | Realm Map has no release door - Journey "Realm Map" card, read-only map | NEW (door slice of 1376) | HUD - Journey deck (lane B) + `Village/Hero` | `PlayerDeckWorkspace.cs:669+` (one Route to `PanelId.RealmMap`), `PublicNavigationRetirementRegression.cs:15,18-21` (re-point), `Assets/_Modules/Village/Hero/RealmMapPanel.cs` (worded travel-stub CTA + `FlowTrace.Step("RealmMap", ...)` in `Open`), `Assets/_Modules/Village/Hero/InventoryUIBuilder.cs:658-668` + `Assets/_Modules/Core/FeatureFlags.cs:842` (delete the dormant `MapTab` door so there is ONE), `Assets/_Modules/HUD/Kit/HudKitController.cs:854-858` (stale comment), `Assets/Editor/Regression/RealmMapRegression.cs` (worded-CTA pin) `[reg]` | M | **DISPATCH inside lane B on the WO's proposed default (ship read-only)** - the WO's own owner question is listed in s3; default is stated in the WO, so the lane runs and the RESULT flags it | `InventoryUIBuilder.cs:661-667` still the flag-only opener; `FeatureFlags.cs:842 MapTab => Get("maptab", defaultOn: false)`; `HudKitController.cs:857` stale comment present; `RealmMapPanel.cs:203` registers |
| **1361** | Player structures vanish from the save - RE-SCOPED to the census instrument | INSTR (the WO's own re-scope: "none required for data loss") | Core/State - build-mode census | `Assets/_Modules/Village/BuildMode/BuildModeController.cs:511-524` (replace the census block with the replayable-vs-bake-owned instrument written out in the WO's diagnosis section; `StrategicPlacementMigration.ShouldReplayRecord` exists at `StrategicPlacementMigration.cs:239`) | S | **DISPATCH - lane C** (instrument only; no fix; the (3) `ResetToNewGame` lead stays an owner question, s3) | `BuildModeController.cs:515-523` unchanged since `486cd7b17` - still prints "live << persisted = structures already gone"; `BaseLayoutRoundTripRegression` green inside 383/383; no ticket exists for the 09-04 09:33 `ResetToNewGame` lead (grep over WO-138x..140x = 0) |
| **1366** | Arena wager currency per channel (SKR on dApp, Crystals on Play) | DEFECT (channel abstraction) | Monetization/Backend - Arena | `Assets/_Modules/Village/Arena/ArenaWalletService.cs:2,19,38,41` (+ `ArenaMode.cs:161,379-384,431-436,455`, `ArenaVM.cs:208,212` read-only callers), `Assets/_Modules/Core/Platform/CurrencySkinResolver.cs:96,140,248,276-279` (EXTEND), `Assets/Editor/Regression/ArenaCatalogRegression.cs:51-54` (keep green when tiers move to the rail), tier tunables `[rail]`; NOT `arenaDefense` / `ArenaProgress` | M | **DISPATCH - lane D** (edit-only; the `global-metadata.dat` scan is a CLI AAB step behind WO-1362 GO) | `git log` Arena files: last `387dc2bf1` (July) - untouched; `grep -c GOOGLE_PLAY Assets/_Modules/Village/Arena/*.cs` = 0 in every file; the ledger's line-number drift (`:239->:248`, `:267->:276/:279`) re-confirmed. LEAD pin: the "existing Crystals spend seam" is not a method (`GameStateService.AddCrystals` exists; debits are inline elsewhere) - lane uses `AddCrystals(-n)` unless the CLI says otherwise |
| **1348** | VFX picks tunable from the Command Center (`realm.vfx.<key>`) | NEW | VFX/Audio + Tooling (rail + console) | `Assets/_Modules/Core/Addressables/VfxAssetLoader.cs:21-46` or `Village/Vfx/HovlVfxCatalog.cs` (the runtime override seam - the json is editor-time only), `api/admin/console.js` / `api/admin/ops.js` (picker), `api/_lib/tunable-manifest.js` + generated json, new suite; rail `[rail]`; `Assets/Editor/VfxManualPicks.json` stays the default | L | **AFTER lane D** (shares the rail merge files; P3 - owner iteration speed, not player-felt). Hold is released (1343/1344 CLOSED, 1345-1347 FIXED) - the WO's Status text is stale, banner it | ledger evidence unchanged: `grep realm\.vfx` = 0 hits; 39 files reference `VfxManualPicks`; `git log` VFX files: nothing since `f27e95724` (09-02) |
| **1382** | Manage Troops screen redesign | DEFECT - **LANDED** | HUD - Manage | none (Status flip + RESULT only) | - | **STALE - flip to FIXED** (build 2026.09.05.355952+, proven on her Seeker 23:40-23:52) and write the RESULT; owner felt-test closes. Its three review defaults were RULED 22:50 (one per tap YES; BACK KEEP; rail NAMES) | `ManageScreenPanel.cs:159-175,215,269-274` (`TroopWorkspacePx = 260f`, `TrainingNowBandPx`), `_troopMode` gone (`git log -S`, last touch `65d5a7eae`), `ManageTroopsTrainDoorRegression.cs:311` CASE 6, `docs/qa/UI_REVIEW_2026-09-05/INDEX.md` rows 07-09 PASS |
| **1327** | Fireball bounces back to caster (bounced 09-04: "red glowing orb stayed at me") | DEFECT (UNPROVABLE without capture) | VFX/Audio | (after capture) `Assets/_Modules/Village/Vfx/VFXManager.Hovl.cs:426`, `VFXManager.cs:1495 EnforceOneshotEmission`, `MarqueeSpellVfx.cs:66-84`, `HeroAbilities.cs` | M | **PARKED on a capture, not a ruling** - one fire cast on the current Firebase build with logcat filtered to `[Flow:VFX]` (`collision-clamp`, `light-budget`, `marquee:firespell_Cast`, `live systems=`) + a 5 s screenrecord. The phone is off USB; the CLI captures it the moment it is back. No code edit is earned before that line (CLAUDE.md s12) | RESULT `:34-36` + `VFXManager.cs:1571-1580`: the bounce was never the proven root of "stays at me"; no VFX trace exists for build 354315; no VFX commit since 09-02 |
| **1215** | Shield seats at identity through the body (bounced 09-03, no note) | DEFECT (UNPROVABLE) | World/Environment - gear seating | (after capture) `Core/Geometry/WeaponOrientHelper.cs:514,627/647,727`, `Village/Hero/GearSeat.cs:138,180` | M | **PARKED on a capture** - screenshot + logcat `AttachOffHandProp MEASURED` / `ShieldHandleSide` for the exact shield id + hero | ledger unchanged; PROD-019 s0b shows the knight row applying byte-exact on device; `offsets.json` row re-dialled `74d9e6546` |
| **1184** | Lookout horde warnings (bounced 09-03: "red dot middle of screen ... vibration and pulsing") | DEFECT (UNPROVABLE) + design | HUD - notifications | (after capture + ruling) `Village/Waves/LookoutNoticeChip.cs:115-121`, `AlertIntelSystem.cs`, `LookoutAlertRegression.cs` | M | **PARKED** (OVERNIGHT_ORDERS PARK list: "lookout warning treatment") - needs (1) screenshot of the red dot + build id, (2) her word on vibration/pulsing as the style | `LookoutNoticeChip.cs:115` draws a parchment `ToastCard` top-centre - the surface she describes is not this system's |
| **1244** | Command Center console (bounced 09-03, no note) | DEFECT (UNPROVABLE) + scope | Backend (`api/admin/*`) | `api/admin/console.js`, `ops.js`, `stats.js` | M-L | **PARKED** (PARK list: "command center scope") - her phone screenshot with the response code + the missing-surface list; CLI confirms `ADMIN_OPS_KEY` on prod differs from `ADMIN_DASH_KEY` | WO banner says the key is unset; `docs/ACCESS_AND_SECRETS.md:145` says set 08-28 - unproven either way from this seat |
| **1373** | Raid rewards + rough stone chain (P1, core loop) | NEW + BALANCE | Economy - raid loot | (after ruling) `Village/Troops/RaidScoring.cs`, `RaidLootTunables.cs`, `Core/Catalog/DungeonExclusiveItems.cs:42-54`, `DungeonGemExclusivityRegression.cs` (re-point), `accessories.json` / `jeweler-recipes.json` | M-L | **PARKED** (PARK list: "1373 reward shape (A/B/C)") - the highest-leverage parked item on the owner lens; one letter unblocks it | `DungeonExclusiveItems.cs:49 ing_rough_stone` exclusivity + oracle unchanged; `RaidLootTunables.cs` exists (`1ef5f6ad4`) as the rail precedent |
| **1377** | Crypto identifiers in IL2CPP metadata (Play) | DEFECT (Play artifact) | Core assembly boundaries | (after ruling) `Core/Web3/*` -> `Assets/_Modules/Web3/`, `Core/CoreServices.cs:154-172`, `Core/Payments/IPaymentProvider.cs:10`, `Core/Platform/CurrencySkin.cs:27`, asmdefs, save read-migration if persisted | L | **PARKED** (PARK list: "1377 type names") - also behind WO-1362 GO; if released it runs AFTER lane D (shares `PaymentChannelResolver`) | ledger unchanged; `DeNelle.Web3.asmdef` `!GOOGLE_PLAY`; no name-persistence found by grep (not the round-trip proof the WO demands) |
| **1292** | Synty environment dressing - EXECUTION | NEW (art) | World/Environment (Unity lock) | `Assets/Editor/MainCastleEnvironmentDressing.cs` (Addressables route first), `Main_Castle_Overworld.unity` via builder + navmesh bake + content build + `tools\r2-ship.ps1` | M (CLI only) | **PARKED** (PARK list: "1292 (blocked on 1291)"; WO-1291 IN PROGRESS 30/33) | builder committed `33ba9c966` + `3f49e93d5`, never run; scene still 140 `Rock_*` refs |
| **1314** | WebGL payload vs 512 MB heap (Pi) | DEFECT - **done by `5163f425c`** | Web/Content | none | - | **PARKED / STALE** (PARK list: "1314 (Pi parked)"; KEY_FACTS 09-02 "Pi is PARKED") - recommend she closes it as DONE-by-`5163f425c` or unparks Pi; either is one word | ledger: both levers applied and measured; PROD-022 DONE awaiting felt-test |

Not READY, listed so nobody re-dispatches them: **WO-1367** IN PROGRESS (CLI, texture pass) and
**WO-1291** IN PROGRESS (CLI, 30/33 Synty addresses) - both sit in the board's Ready bucket by its
bucketing rule; **WO-1395** FIXED (re-scoped; premise disproved; ruling in s3); **WO-1362** `RECON
COMPLETE - AWAITING OWNER GO / NO-GO` (the pin over every Play-artifact proof).

## 2. Dispatch order for the morning - file-disjoint lanes that run in parallel

Order = player-felt impact, owner lens first. Every lane is edit-only, in its own worktree off HEAD,
never gates / commits / builds; the CLI batch-gates the combined tree once and commits by explicit path.

| Lane | WO(s) | Why this rank | Files (disjoint across lanes; merge files CLI-owned) | Pins before the lane can CLOSE (not before it can START) |
|---|---|---|---|---|
| **A** | **WO-1379** remaining half | The raid door is the "reason to raid" surface; today a player holding Heartfire is refused by a timer the canon says is retired, and `scene-configs.json` lies about it. Two lockouts "reads as a bug" (the WO's own words). Smallest change, largest felt effect on the loop | `RaidSelectionScreen.cs`, `RaidCooldownService.cs`, `RaidDeployController.cs` (comment only), `scene-configs.json` x2, `HeartfireRegression.cs` | RED-first pin for "a second WHEN gate reappears"; device proof = tap a camp cleared < 4 h ago with a charge -> RaidDeploy opens (needs the phone) |
| **B** | **WO-1376 nav slice + WO-1394 + WO-1396** as ONE agent | Journey goes 2 -> 5 cards; the Season Track and the Realm Map become visible for the first time; Dungeons opens against a proven-open endpoint. This is the PROGRAM s8 "open the content you already built" and the largest "desire to play more" delta available without a ruling | `PlayerDeckWorkspace.cs`, `PublicNavigationRetirementRegression.cs`, `RealmMapPanel.cs`, `InventoryUIBuilder.cs`, `FeatureFlags.cs` (MapTab), `HudKitController.cs:854-858` (comment), `BattleMonthlyPanelsBootstrap.cs:104` (trace), `RealmMapRegression.cs`, `UICaptureLaunch.cs` (Journey + SeasonTrack cases), canon-strings (purpose lines) | WO-1396 owner question (read-only map now vs hold for WO-827) - lane runs on the WO's stated default and flags it; `JourneyWorkspace_*.png` with five cards opened by the CLI; `UI_CAPTURE_OK` with zero `[UICap-GEO]` lines (five cards in the deck grid must clear MinTouchPx - WO-1397 hit 108.7 px on a 5-card Hero deck and fixed the grid, so re-read that trace) |
| **C** | **WO-1361** census instrument | P0 by label, but the WO's own diagnosis says no data is lost; the instrument is what lets the NEXT capture name a real loss by id. One file, no behaviour change | `BuildModeController.cs:511-524` | none for the edit; closure = a founding-load capture printing `= N replayable + 8 bake-owned` (needs the phone) |
| **D** | **WO-1366** Arena wager per channel | Real-money adjacent and a Play blocker, but invisible on the Seeker build she plays; ranks below the loop lanes | `ArenaWalletService.cs`, `ArenaMode.cs` (trace strings), `CurrencySkinResolver.cs`, `ArenaCatalogRegression.cs`; rail key + default handed back `[rail]` | LEAD pin on the Crystals spend shape (see table); artifact scan is a CLI AAB step behind WO-1362 GO |
| **E** (after D) | **WO-1348** | Owner iteration speed on VFX picks; shares the rail merge files with D, so it follows D at gate time | `VfxAssetLoader.cs` / `HovlVfxCatalog.cs`, `api/admin/console.js`, `ops.js`, `tunable-manifest.js` + json, new suite | design pin: how key CREATION (boss-death) reaches a shipped prefab list |
| **CLI-only, no agent** | **WO-1382** flip + RESULT; **WO-1348** Status banner ("hold released"); **WO-1379** twin JSON note wording; **WO-1314** close-or-unpark question; **WO-1361** (3) question | board hygiene the derived board needs (CLAUDE.md s2: the flip belongs in the same commit as the work - 1382's was missed) | `WorkOrders/WORK_ORDER_1382_*.md` + new `.RESULT.md`, `WORK_ORDER_1348_*.md:3` | - |
| **Capture lanes (CLI, the moment the Seeker is back on USB)** | **WO-1327**, **WO-1215**, **WO-1184**, **WO-1244** | Four owner bounces with no capture; each needs one screenshot/logcat before any code is earned | none | the phone |

**Disjointness check:** A / B / C / D touch pairwise-disjoint files. B and D both hand back suite files
but never edit `DataRegression.cs` (CLI registers). A and B do not share a file (`RaidSelectionScreen.cs`
vs `PlayerDeckWorkspace.cs`); B's `PublicNavigationRetirementRegression.cs` is touched by no other lane.
E follows D only because of the rail merge files. `Assets/Editor/VillageSceneBuilder.cs` (s9 bottleneck):
no READY WO touches it. Two lanes (B, C) plus the WO-1397 tree are all HUD/BuildMode - no overlap with the
APK chain running at 07:11, which builds from the committed tree only.

## 3. Rulings needed - one word each, collected for the morning (merged with the overnight handover's list)

| # | WO / source | The question, quoted | One-word answer form |
|---|---|---|---|
| 1 | **WO-1373** | "Which shape do you want? (A) raids drop rough stone directly, (B) raids drop it but dungeons keep a GRADE advantage, (C) raids drop a different tier-up material" (WO recommends B); also "can a player wear more than one at once, and do they stack?" | "A" / "B" / "C"; "stack yes/no" |
| 2 | **WO-1395** (FIXED, re-scoped overnight) | "keep two artifact-exclusive storefronts (what is pinned now) OR collapse Play into a PackStore skin, which means reversing WO-1282's Wallet exclusion for Play builds" | "two" / "collapse" |
| 3 | **WO-1396** | "Ship the map READ-ONLY now (explore + rewards named, travel worded as coming), or hold the card until WO-827 lands travel? Default proposed: ship read-only" | "read-only" / "hold" |
| 4 | **WO-1376** | split troops-in-wave-defence into its own WO? (PROGRAM s9 "Explicitly NOT P0"; the WO itself says "consider splitting it out") | "split" / "keep" |
| 5 | **WO-1394** | card copy defaults: title "Season", purpose "Raid to climb this month's track" (WO: "None - ruled in PROGRAM s8") | "ok" or her two lines |
| 6 | **WO-1377** | "is a Play artifact that contains the type name `IJupiterService` - with no reachable code, no string copy and no UI - actually a policy problem?" | "type-name OK" / "move it" |
| 7 | **WO-1362** | GO / NO-GO on the Google Play AAB programme (pins 1366's artifact proof, 1377, and every Play scan) | "GO" / "NO-GO" |
| 8 | **WO-1371** (FIXED, awaiting felt-test) | "Decide the correct new-game state and say so: zero fill, or fill seeded" - the commit chose zero, never asked on record | "zero" / "seeded" |
| 9 | **WO-1370** (FIXED) | final wording of the harvest-result lines ("FINAL WORDING IS THE OWNER'S") | "use it" or her lines |
| 10 | **WO-1368** (FIXED) | drawer placement of the queue verbs ("That is a UI design question and it is the owner's") - the fix put them in the drawer; confirm | "in the drawer" / where |
| 11 | **WO-1244** | (a) is `ADMIN_OPS_KEY` set on prod and different from `ADMIN_DASH_KEY`? (b) the missing-surface list from her 09-03 bounce | "key set" + the list |
| 12 | **WO-1184** | "vibration and pulsing warning" as the notice style - a design pick | "pulse+vibrate" / other |
| 13 | **WO-1292** | run the Synty dressing before WO-1291 finishes, or wait? | "dress now" / "wait" |
| 14 | **WO-1314** | close as DONE-by-`5163f425c`, or unpark the Pi lane? | "close" / "unpark" |
| 15 | **WO-1361** item (3) | were BOTH `ResetToNewGame` fires on 09-04 (07:42 and 09:33, the second with a level-3 hero) her own START NEW taps? If not, that is the real progress loss and a new ticket | "mine" / "not mine" |
| 16 | HANDOVER overnight #1 | `arcane-tower` tier-1 authors `costCrystal: 1280` but `BuildingUpgradeService.TierCost` charges WOOD by tier number; the page shows the CHARGED lane. Which is right - the data or the service? | "data" / "service" |
| 17 | HANDOVER overnight #2 | add a retained-vs-lost field to `BankOverflowStatus` (schema-free, in-memory) so the silo dump row stops using the modal's generic sentence? | "yes" / "no" |
| 18 | **WO-1389** Q1-Q3 (FIXED) | the post-first-raid beat: Q1 voice (HUD default), Q2-Q3 as written in the WO | per WO |
| 19 | **WO-1388** (FIXED) | pack name / basket / badge copy for the $1.99 Builder's Hour pack | per WO |
| 20 | `collector_farm` | farm vs quarry name (OVERNIGHT PARK list) | "farm" / "quarry" |
| 21 | **WO-1382** (landed) | none new - her 22:50 rulings answered the three review questions; this row exists so the flip is not held for a ruling | - |

Lead (CLI) decisions, not owner rulings: **WO-1366** Crystals spend shape (`AddCrystals(-n)` vs a new
`SpendCrystals` seam); **WO-1348** key-creation path; **WO-1292** direct-instance vs Addressables route.

## 4. Ticket defects found this pass (for the RCA-clean agent / CLI, not fixed here)

- **WO-1382** Status never flipped in the commit that landed it (`65d5a7eae`) - CLAUDE.md s2 violation;
  no RESULT file. Same failure the 09-04 ledger found on nine tickets.
- **WO-1379** the board note dates `RaidSelectionScreen.cs:527`; the gate is now at `:550`. The JSON
  superseded notes on `:108/:173/:230` assert "not consulted at runtime" while `:550` consults it -
  false at source until lane A lands; iron_bastion `:287` has no note.
- **WO-1376** "Whether that endpoint currently serves `open` rows is NOT PROVEN" - proven this session
  (HTTP 200, 5 open / 1 sealed). Update the clause when lane B lands.
- **WO-1348** Status text still reads "DISPATCH HELD until the WO-1343..1347 agents land" - all five
  landed 09-03/09-04; the hold is stale.
- **WO-1361** Status carries a third open lead (the 09:33 `ResetToNewGame`) with no ticket of its own
  - either mint one on her answer to s3 #15 or record "mine" in the WO.
- **WO-1244** banner "ADMIN_OPS_KEY IS NOT SET" contradicted by `docs/ACCESS_AND_SECRETS.md:145` -
  one of the two documents is wrong; neither is provable from a read-only seat.
- **WO-1314** first Status line still READY while its actionable scope is done and the ticket is
  PARKED by ruling - a banner or a close, her call (s3 #14).
- Board bucketing: `BOARD.html` folds IN PROGRESS (1367, 1291) into "Ready 18" - `tools/board_build.py`
  counts them as Ready; not a ticket defect, but a reader who trusts the count will over-dispatch by two.
