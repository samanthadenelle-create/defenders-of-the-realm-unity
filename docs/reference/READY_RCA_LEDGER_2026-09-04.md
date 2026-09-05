# READY RCA LEDGER - 2026-09-04 (QA read-only pass)

**Scope:** every `WorkOrders/WORK_ORDER_*.md` whose FIRST `**Status:**` line reads READY TO IMPLEMENT
at HEAD `3f49e93d5` (2026-09-04 22:34, `feat/synty-art-retheme`), working tree as truth. Excluded by
instruction (board agent editing tonight): 1333, 1372, 1374, 1375, 1378, 1379, 1380. `.RESULT` /
`.PLAN` / `.VERIFICATION` files excluded. **21 tickets.**

**Method:** each WO's cited file:line/symbol re-opened this session; `git log` per cited file;
`Assets/Editor/Regression` grepped for the symbol; RESULT / superseding WO checked. The full evidence
block is appended to the END of each WO under `## RCA re-verified 2026-09-04 (QA read-only pass)`.
No Status line was changed (PO closes, CLAUDE.md s13).

## Count per verdict

| Verdict | n | tickets |
|---|---|---|
| VALID | 2 | 1292, 1366 |
| STALE | 1 | 1361 |
| SUPERSEDED | 10 | 952, 1314, 1363, 1364, 1365, 1368, 1369, 1370, 1371, PROD-021 |
| NEW-FEATURE | 2 | 1348, 1376 |
| NEEDS-RULING | 2 | 1373, 1377 |
| UNPROVABLE | 4 | 1184, 1215, 1244, 1327 |

## THE HEADLINE FINDING

**Nine of the ten SUPERSEDED tickets were implemented in commits that are ancestors of HEAD on
2026-09-04 (`f6540db88`, `6979fb961`, `61d19a23b`, `da9694c86`) but their `**Status:**` lines were
never flipped** - CLAUDE.md s2 requires the flip in the same commit as the work. The derived board
(`python tools/board_build.py`) therefore shows them READY. That is the one-pass fix for the owner:
flip nine lines, write the missing RESULT files, regenerate the board.

## Full table

| WO | Verdict | One-line evidence | Files a lane would touch | Pins / rulings needed |
|---|---|---|---|---|
| 952 endstate panel compression (REOPENED) | SUPERSEDED | `f6540db88` landed EndStateView +390 (`NarrativeStripAt` at `:1035`) + `EndStateBodyFitRegression.cs` registered `DataRegression.cs:679`; status never flipped | this WO (Status); UI-capture case list (arena 5-row gear case) | owner felt call on the side-by-side strip; an arena EndState PNG opened (only WaveClear PNGs exist in `Builds/ui-capture/`) |
| 1184 lookout horde warnings (BOUNCED) | UNPROVABLE | chip is parchment top-centre uGUI (`LookoutNoticeChip.cs:115-121`), not a red dot; bounce has no capture; suite `[lookout-alert]` registered | `LookoutNoticeChip.cs`, `AlertIntelSystem.cs` (only after capture) | device screenshot of the "red dot" + build id; owner ruling on vibration/pulsing style (design) |
| 1215 shield seats (BOUNCED) | UNPROVABLE | substantiation gate live `WeaponOrientHelper.cs:319-331`, pinned by `AttachmentOffsetRegression` Case6; bounce is "Fail" with no note; PROD-019 s0b shows the seat applying on device | `WeaponOrientHelper.cs` (`:514`, `:627/647`, `:727`), `GearSeat.cs` | screenshot + logcat `AttachOffHandProp MEASURED` / `ShieldHandleSide` for the exact shield id + hero |
| 1244 command center console (BOUNCED) | UNPROVABLE | console + split write gate present and tested (`ops.js:101-128`, `test/command-center.test.js:190-346`); bounce has no note; WO banner "key unset" contradicted by `ACCESS_AND_SECRETS.md:145` | `api/admin/console.js`, `ops.js`, `stats.js` | her phone screenshot with the response code; CLI confirms `ADMIN_OPS_KEY` present on prod and differs from `ADMIN_DASH_KEY` |
| 1292 synty environment dressing | VALID | scene still 140 `Rock_*` refs / 0 dressing root; builder landed `33ba9c966`, compile fix in `3f49e93d5`, NEVER RUN; builder uses direct gitignored instances against WO `:82-84` | `Editor/MainCastleEnvironmentDressing.cs`, Addressables groups, `Main_Castle_Overworld.unity` via builder + bake | blocked on WO-1291 (IN PROGRESS); lead decision: Addressables route before execution |
| 1314 webgl payload vs 512 MB heap (PARKED) | SUPERSEDED | both named levers applied `5163f425c` (-29.2 MB measured); heroes to R2 `d706b430b`; PROD-022 DONE awaiting felt-test; ticket parked by owner | none | owner: close as DONE-by-`5163f425c` or unpark Pi; s6 audio would be a NEW WO |
| 1327 fireball bounces (BOUNCED) | UNPROVABLE | clamp live `VFXManager.cs:1592/1657/1719`, `Hovl.cs:426`; RESULT `:34` says bounce was never the proven root; "orb stayed at me" has no VFX trace on build 354315 | `VFXManager.Hovl.cs`, `VFXManager.cs:1495`, `MarqueeSpellVfx.cs`, `HeroAbilities.cs` | one-cast logcat filtered to `[Flow:VFX]` + screen recording; then a NEW RCA for the "stayed" symptom |
| 1348 vfx picks tunable | NEW-FEATURE | hold released (1343/1344 CLOSED, 1345-1347 FIXED); zero `realm.vfx` code; runtime reads `HovlVfxCatalog.asset` via `VfxAssetLoader.cs:21-46`, not the json | `RemoteTunables.cs`, `RemoteTunablesService.cs`, `api/_lib/tunables.js` + manifest, `console.js`/`ops.js`, `VfxAssetLoader.cs`, new suite | owner confirms `realm.vfx.<key>`; design: how key CREATION reaches a shipped prefab list |
| 1361 structures vanish from save | STALE | device log 09-04: migration writer wrote 8 baked-twin records; census gap 18-10 = 8 by design; oracle `BaseLayoutRoundTripRegression.cs` committed `3f49e93d5`; schema is v41 not v38 | `BuildModeController.cs:516-523` (split migrated/standdown-pending), `BaseLayoutLoader.cs` | owner confirms whether any PLACED structure is missing after relaunch; a post-standdown relaunch capture |
| 1363 play crypto purge | SUPERSEDED | landed `6979fb961`; `SkrShowcasePanel.cs:46/277`, `TitleController.cs:337` guarded; gate now RED `wo1367-aab.log:37493` on `canon-strings.json` "Solana"; status still READY | this WO; then 1366/1377 lanes | WO-1377 ruling; canon-strings residue |
| 1364 play gate sees dirty artifact | SUPERSEDED | landed `6979fb961` + `61d19a23b`; single `ForbiddenTokens` with `skr`/USDC mint (`GooglePlayPackagingGate.cs:54,62`); ps1 derived from C# (`:71,73`); RED proven on a real AAB | this WO only | none; green-artifact proof waits on 1366 + 1377 |
| 1365 AAB has no ship chain | SUPERSEDED | `da9694c86` landed `google-play-aab-build.ps1` at REPO ROOT (r2-ship call `:316`, `ExpectMarker :270`, `AAB_SIZE` exit 6); `Builds/aab-status.txt` AAB_SIZE_OK; status never flipped | this WO; `KEY_FACTS.md:158` (stale "NO SHIP CHAIN") | none for the flip; no captured RED for "no fresh R2 push FAILS"; 09-01 catalog push unknown |
| 1366 arena wager per channel | VALID | `ArenaWalletService.cs:38/41` still the SKR PlayerPrefs stub; 0 `GOOGLE_PLAY` in Arena; 0 arena tunables; seam lines moved to `CurrencySkinResolver.cs:248/276/279` | `ArenaWalletService.cs`, `ArenaMode.cs`, `CurrencySkinResolver.cs`, `RemoteTunables.cs`, `RemoteTunablesService.cs`, `api/_lib/tunables.js`, `ArenaCatalogRegression.cs`, Balance tab | LEAD: the "existing Crystals spend seam" is not a method (`AddCrystals` exists, inline debits elsewhere) - pick the shape; wager amounts stay defaults |
| 1368 manage builds zero queue rows | SUPERSEDED | `ManageScreenPanel.cs:1262` `AddQueueRow(_vm.QueueRows[i])` inside `RenderQueueDrawer`; oracle re-pointed `ManageQueueDrawerRegression.cs:106-121`; landed `f6540db88`; status not flipped | this WO (Status) | owner felt-verify on a build newer than 354315 (no post-fix device log exists) |
| 1369 game-over hold hard freeze | SUPERSEDED | `WorldHold.cs:446/463` probe REQUIRED, `:944` sceneLoaded wired; `GameOverScreen.cs:166` liveness poll, `:191` Unhook; suites `DataRegression.cs:675/677`; landed `f6540db88`; status not flipped | this WO (Status) | owner felt-verify of a dungeon death on a build newer than 354315 |
| 1370 harvest modal unreadable | SUPERSEDED | `HarvestOverflowModal.cs:100-110` name+figure one line, says "lost"; `HarvestResultCopyRegression` registered `:669`; landed `f6540db88`; status not flipped | this WO (Status) | owner approval of the wording (WO said final wording is hers; not recorded) |
| 1371 new game inherits collector fill | SUPERSEDED | `GameStateService.cs:1296 ClearHarvestPrefs()` + `:1452-1454` prefixes, `ResourceCollector.cs:22-26` aliases; `NewGamePrefStoreSweepRegression` registered `:667`; landed `f6540db88`; status not flipped | this WO (Status) | post-fix capture quoting `pending=0` after reset; owner confirms zero-fill vs seeded (commit chose zero, never asked on record) |
| 1373 raid rewards + rough stone | NEEDS-RULING | `DungeonExclusiveItems.cs:42-49` invariant + `DungeonGemExclusivityRegression` unchanged; no raid path drops stone; `PROGRAM_RAID_ECONOMY:330` still "blocked"; `RaidLootTunables.cs` now exists (`1ef5f6ad4`) as the rail precedent | `RaidScoring.cs`, `RaidLootTunables.cs`, `DungeonExclusiveItems.cs`, `DungeonGemExclusivityRegression.cs` (re-point), `accessories.json`, `jeweler-recipes.json` | owner picks s4 A/B/C (WO recommends B); s5 open items |
| 1376 P2 retention around the loop | NEW-FEATURE | WO-1375 FIXED in build 355872 so the sequence gate is released; Journey deck has exactly 2 cards (`PlayerDeckWorkspace.cs:591,610`); `DungeonStatusCatalog.cs:2` fail-closed; retirement oracle exists | `PlayerDeckWorkspace.cs`, `DungeonStatusCatalog.cs`, `PublicNavigationRetirementRegression.cs`, tunables rail (4 files), new oracles | prove `/api/dungeon-status` serves open rows (unproven); owner: split troops-in-wave-defence out? |
| 1377 crypto identifiers in IL2CPP | NEEDS-RULING | identifiers at `CoreServices.cs:154-172`, `IPaymentProvider.cs:10`, `CurrencySkin.cs:27`; `DeNelle.Web3.asmdef:17 !GOOGLE_PLAY`; no name-persistence found in `Core/State` or via Parse/ToString/PlayerPrefs greps (evidence, not the demanded round-trip proof) | `Core/Web3/*` -> Web3 assembly, `CoreServices.cs`, `FeatureFlags.cs`, both enums, asmdefs, save migration if persisted | owner rules s4 (move+rename vs accept dead type names); CLI proves persistence with a real save round-trip |
| PROD-021 R2 catalog never pushed | SUPERSEDED | gate fixed since `486cd7b17` (`r2-ship.ps1:177/182/161/223`); fresh `Builds/r2-parity.log` 22:21:57 `R2_PARITY_OK targets=Android,StandaloneWindows64,WebGL objects=271`; nothing under `ServerData/` is newer than the proof | this WO (Status) | CLI attaches the falsification log (claimed only in `33ba9c966`'s message; no `R2_PARITY_FAIL` log exists); fresh exe run; PO closes |

## SUPERSEDED - close or flip in one pass (10)

| WO | Implemented by | What remains before PO closes |
|---|---|---|
| 952 | `f6540db88` (2026-09-04 12:47) | fresh `REGRESSION_OK` marker (22:42 log is a run in progress); arena EndState capture PNG; felt call on the strip |
| 1314 | `5163f425c` + `d706b430b` (2026-09-03) | owner ruling: close, or unpark Pi lane |
| 1363 | `6979fb961` | Status flip + RESULT; artifact-clean box waits on 1366 + 1377 |
| 1364 | `6979fb961` + `61d19a23b` | Status flip + RESULT |
| 1365 | `da9694c86` | Status flip; `KEY_FACTS.md:158` canon fix |
| 1368 | `f6540db88` | owner felt-verify on a post-354315 build |
| 1369 | `f6540db88` | owner felt-verify (dungeon death) on a post-354315 build |
| 1370 | `f6540db88` | owner approves wording; post-fix screenshot |
| 1371 | `f6540db88` | post-fix `pending=0` capture; owner confirms zero-fill choice |
| PROD-021 | `486cd7b17` (gate), `33ba9c966` (claimed falsification) | falsification log artefact; fresh exe run; PO closes |

## NEW-FEATURE - route back to the owner as spec, never RCA-fix (2)

| WO | Why it is scope, not a defect | Gate to dispatch |
|---|---|---|
| 1348 | zero `realm.vfx` code exists; the six-source tunable shape is a build | hold is released (1343-1347 landed); confirm key shape; decide how new keys reach a shipped prefab list |
| 1376 | five Journey cards, dungeon gate, Season/Realm nav, troops-in-defence - none built | WO-1375 landed (sequence gate open); prove `/api/dungeon-status`; owner splits troops-in-defence |

## NEEDS-RULING - one owner word each (2)

| WO | The ruling |
|---|---|
| 1373 | s4: (A) raids drop rough stone / (B) raids drop stone, dungeons keep a GRADE edge / (C) a new material. WO recommends B. |
| 1377 | s4: move `Core/Web3` + rename two enum members (data-loss risk if persisted by name) vs accept dead type names in `global-metadata.dat`. |

## UNPROVABLE - the capture that does not exist (4)

All four are owner bounces (felt-test 09-03/09-04) on fixes that ARE in the tree, with no capture,
screenshot or note that names the failing case. Per s12/s14 no code edit is earned until one exists.

| WO | The missing capture |
|---|---|
| 1184 | screenshot of the "red dot middle of screen" + build id; plus a design ruling on vibration/pulsing |
| 1215 | screenshot + logcat `AttachOffHandProp MEASURED` / `ShieldHandleSide` for the exact shield + hero |
| 1244 | phone screenshot with the response code; prod env check for `ADMIN_OPS_KEY` |
| 1327 | one-cast logcat filtered to `[Flow:VFX]` on build 354315 + screen recording |

## VALID - ready for a lane (2)

- **1366** - RCA holds byte-for-byte on the Arena side; only `CurrencySkinResolver` line numbers moved. One LEAD decision first: the Crystals spend seam the WO forbids bypassing does not exist as a method.
- **1292** - RCA holds (scene untouched, builder never run) but it is BLOCKED on WO-1291 and the builder must be re-routed through Addressables per the WO's own constraint before it is executed.

## Cross-cutting notes for the lead

1. `KEY_FACTS.md:158` still asserts "THE AAB LANE HAS NO SHIP CHAIN" - contradicted by `google-play-aab-build.ps1` on disk (s15 canon fix owed).
2. No post-fix device capture exists for ANY of the `f6540db88` fixes; every log under `logs/device/` is the 09-04 morning pre-fix pull. Their felt-verify rows are genuinely open, not just unticked.
3. `Builds/regression.log` was read at its 22:31 state during this pass: `:113612 [baselayout-roundtrip] BASELAYOUT ROUND-TRIP OK - 17 records survive save->reload (v41)` and `:113715 REGRESSION_OK 377/377 suites -- 377 green, 0 red, 0 skipped`. A rewrite began at 22:42 (74,746 lines, no marker yet at the time of writing), so a later reader must re-read the marker on the new run.
4. `WORK_ORDER_1361` and `WORK_ORDER_1369` were appended to by another seat during this pass (a loader-side diagnosis on 1361 reaching the same "nothing is lost" conclusion). Both QA blocks are intact and terminal.
