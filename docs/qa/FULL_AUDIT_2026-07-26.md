# FULL AUDIT — 2026-07-26 (Sunday-scale ground-truth sweep)

> **Purpose:** the master truth doc for the build decision. Every claim is sourced from HEAD /
> working tree (CLAUDE.md §12: cite evidence, never guess). Read-only audit — this file is the
> only thing written. Method: 5 parallel read-only SME agents + direct config reads, cross-checked.
>
> **Repo state at audit:** branch `wip/village2-and-f8-tickets`, **HEAD `8e1ec74a`** (2 commits
> AHEAD of `origin/wip` — push held on the newest doc commits), **AHEAD of the 07-26 canon anchor
> (`7dec0e07`)**. Save schema **v35** (the anchor still says v34). WO next-free = **775** (774 minted).

---

## 0. HEADLINE — the tree has advanced PAST its own canon

The dominant finding, exactly as the 07-22 sweep concluded: **the code is HEALTHY; the debt is
DOCUMENTATION DRIFT** — and this time the drift is *worse* because a large build wave landed AFTER
the 07-26 doc-only housekeeping anchor was written. The anchor (`CANON_GROUND_TRUTH_2026-07-26.md`)
says save=v34 and WO-771/772/773 are BACKLOG/SPEC/BLOCKED. **The tree contradicts all of that:**

| System | Canon (07-26 anchor) says | Tree at HEAD `8e1ec74a` actually has |
|---|---|---|
| Save schema | v34 | **v35** (`SaveSchema.cs:34`) — obsidianQueue + barracksLevel/troopLevels |
| WO-773 Obsidian queue | BACKLOG / not built | **DONE** — `ObsidianQueueEngine.cs` + `BuildTimerService.cs`, wired + tested |
| WO-772 shared enemy system | BLOCKED on owner ratification | **DONE (Phase 1)** — `EnemyResolver.cs` (258 lines, Hollow Ones ratified) |
| WO-771 raid V1 spine | "nothing built yet — all SPEC" | **WIRED end-to-end** — `RaidVictoryController.cs` closes victory→claim→loot→return |
| WO-771.9 barracks/troops | BACKLOG | **DONE / wired** — `BarracksService.cs`, `TroopTrainingPanel.cs`, `ArmyStorage.cs` |
| All 4 core loops | mixed | **all WIRED** (wave / dungeon / raid / build-queue) |

The build decision should treat the 07-26 anchor's status ledger as **superseded by this doc**.

---

## 1. BUILD / COMPILE STATE — GREEN (with 2 caveats)

**Compile-look:** GREEN on the spot-checked hot files.
- Brace balance verified on `SaveSchema.cs` (73/73), `SaveMigrator.cs` (99/99), `FeatureFlags.cs`
  (28/28).
- A repo-wide naive char-count scan of `Assets/_Modules` + `Assets/Editor` flagged exactly ONE file,
  `Assets/Editor/RegressionSuite.cs` (208 `{` vs 203 `}`) — **investigated and confirmed a FALSE
  POSITIVE**: the 5-char delta is entirely brace characters inside string / regex / char literals
  (`"[{(r.Pass…"`, `"{r.Name}"`, `'{'`, `'}'`, regex `"…\{"`). File is git-clean/unmodified. Not a
  compile break. (The scan also timed out mid-tree, so it is not a full pass — the authoritative
  compile signal is the `CompileGate` marker, last certified 07-22.)
- **NUL-byte scan (CLAUDE.md §1 WO-434 guard):** none found in the scanned modules before timeout.

**Save schema + migrator:** GREEN.
- `SaveSchema.CurrentVersion = 35` (`Assets/_Modules/Core/State/SaveSchema.cs:34`). FileFormat = 1.
- Migrator chain COMPLETE: `SaveMigrator.cs` registers steps `{2..35}` and iterates
  `fromVersion+1..CurrentVersion` ascending (`:40-76, :113`). `MigrateToV35` present (`:523`) — folds
  legacy `buildJobs`/`pendingBuilds`/`buildingCooldowns` into the Builder channel. The skipped
  numbers (11/12/13/15/16/19/20/32) are documented additive-default-on-read no-migrator bumps.
- v35 = WO-773 obsidianQueue; WO-771.9 barracksLevel/troopLevels ride v35 additively (no bump).

**Feature-flag defaults of record** (`Assets/_Modules/Core/FeatureFlags.cs`, verified line-by-line):

| Flag | Default | Line | Note |
|---|---|---|---|
| `ff.raidwalk` (RaidContinuousWalk) | **OFF** | :88 | WO-771 LOCKED Teleport/Deploy — deploy loop is default. (XML doc at :84 still says "Default ON" — **comment lie**, code is OFF.) |
| `ff.buildtimers` (BuildTimers) | **ON** | :75 | CoC construction timers. **No separate `ff.buildqueue`/`ff.queue` flag exists** — the WO-773 multi-channel queue rides `ff.buildtimers` + the v35 save field. |
| `ff.barracks` (Barracks) | **ON** | :597 | **Flipped from OFF → ON 2026-07-26** for WO-771 V1 (raid pulls troops from barracks roster). |
| `ff.dungeonfpv` (DungeonFpv) | **ON** | :642 | First-person dungeon traversal (architect chose FPV over raising the ceiling). |
| `ff.overworldencounter` (OverworldEncounter) | **OFF** | :154 | Reverted to OFF 2026-07-26 so the leftover wandering-encounter loop no longer shadows the Teleport/Deploy raid loop. |

Adjacent defaults confirmed: `ff.dungeonrealtime` ON (:265), `ff.mergedworld` ON (:350),
`ff.gatetraversal` OFF (:371, felt-test 07-26), `ff.enemyweapons` OFF (:218).

**Caveats (see §3):** (1) the last certified full `DataRegression REGRESSION_OK` was **07-22, not
re-run since** the build wave; (2) WO-545's **8 pre-existing EditMode failures remain open** — the
test gate is not clean.

---

## 2. CORE LOOPS — all WIRED

| Loop | Verdict | Key evidence |
|---|---|---|
| **Village wave** | **WIRED** | `WaveManager.cs` — auto-arm `:533` (`ff.waveautostart` ON), spawn `BeginLoop :622`/`EnterCountdown :948`, win `CompleteWave :1957`, loss `HandleHeartDestroyed :2187`. Latent op-risk only: a scene with no `WaveSpawnPoint` self-clears instantly (loudly guarded :931). |
| **Dungeon** | **WIRED** | `DungeonController.cs` — enter (`DungeonEntrance.EnterDungeon :147`), move/camera (`:248`,`:614`, sole-mover `:525`), lore (`HydrateLoreStones :882` → `LoreReadingModal`), craft (`ConfigureCrafting :734`), fight (`HydrateEncounters :972` → BattleArena, `ff.dungeonrealtime` ON), settle (single authority `SettleEncounter :1072`, realtime `:1141`/ATB `:1039`), leave (`DungeonExit.ExitToVillage :366`). All WO-770.1–.9 present with real callers. |
| **Raid V1 spine** | **WIRED** (canon STALE) | `RaidVictoryController.cs:154` closes victory→claim→loot→return. Train (`BarracksService.EnqueueTraining :188/:212`) → army (`GameState.Army`) → select (`RaidEntryBridge :130` → `RaidSelectionScreen.Open :152`, raidwalk OFF so Teleport path active) → deploy screen (`RaidDeployScreen.Open`) → `SceneRouter.GoRaid :456` (RaidBase_* scenes on disk) → deploy (`RaidDeployController :267` → `TroopDeployer.SpawnFromArmy :290`) → auto-fight (`RaidScoring` + garrison) → stars/loot (`RaidScoring.Finalize`+`GrantLoot :179-182`) → claim (`RaidClaimService.MarkClaimed :55`). |
| **Build/upgrade multi-channel queue (WO-773)** | **WIRED** | `ObsidianQueueEngine.cs` (pure: `Enqueue :43`, `Resolve :65` w/ offline cascade) + `BuildTimerService.cs` (MonoBehaviour wrapper owning Builder/Train/Research channels, `:263`,`:312`,`:575`). Placement enqueues via `BuildModeController :1797`; training via Train channel `BarracksService :212`. **Built in code, not just the v35 save field.** |

**Raid weak/stub spots (not on the critical path):** `RaidDeployScreen.OnAutoRecommend()` is a cosmetic
STUB (`:358-367`); claim persistence is PlayerPrefs (`dotr-raid-owner-<id>`), not SaveSchema/cloud
(`RaidClaimService.cs:14-20`). WO-774 (deploy-ring spatial rule, loadout handoff, Army/Deploy naming)
is genuine SPEC — the only remaining raid polish.

---

## 3. REGRESSION COVERAGE

**Entry point:** `DataRegression.RunAll()` (`Assets/Editor/Regression/DataRegression.cs:36`) → prints
`REGRESSION_OK` or `REGRESSION_FAIL: <n>`. ~95 checks: ~22 inline `Check*` oracles + ~70 external
`Regression.Run()` suites. Most HARD-FAIL. Nuances:
- **FAIL-BY-DESIGN reds (open):** `crystal-production`, `modal-registration`, `ftue-honesty`
  (`:289-292`) — flip green when their fix lands.
- **RATCHETS (allow-list debt):** `ui-mvvm` (`:275`), `ui-obsidian` (`:274`) — fail only on NEW debt.
- **SOFT/WARN:** item-capability prefabPath coverage (`:161`), crafting orphan-material, icon coverage
  (`:326`, hard only for Knight starter weapon).

**Coverage table — every requested major system has ≥1 oracle AND ≥1 test (all GREEN for presence):**

| System | Oracle | Test |
|---|---|---|
| Village wave | `[wave-scaling]`,`[waves-schema]`,`[enemy-rewards]`, CheckWaveScaling/Enemies | WaveDataTest, VillageSmokeTests |
| Dungeon loop | ~13 `dungeon-*` suites (exit/return/defeat/toast/fpv/state-reset/lore), `[room-forge]` | DungeonSettleTests, DungeonRuntimeStateResetTests, EnemyResolverSpawnTests |
| Raid | `[raid-scoring]`, TroopRosterRegression | RaidScoringTests, RaidSelectionVMTests, RaidDeployVMTests, RaidOutpostCardinalsTest |
| Build/upgrade queue | `[obsidian-queue]`,`[build-upgrade]`,`[upgrade-authority]`,`[build-econ]`,`[strategic-placement]` | ObsidianQueueTests, BuildMenuVMTests, TowerUpgradeVMTests |
| Save/migrator | `[core-save]`,`[core-save-sme]` | SaveLoadRoundTripTest, SaveMigratorTest, SaveSchemaValidateTest, GameStateRoundtripTests |
| Echo workforce | `[echo-spec]`,`[offline-harvest]`,`[echo-card-copy]`, CheckPopulationMilestones | EchoWorkforceVMTests, EchoRosterVMTests |
| Hero progression | `[hero-prog]`,`[talent-strategy]`,`[hero-loco-clips]`, CheckArmedHeroInvariant | LevelUpVMTests, AnimParamsTests, MotionCastingsTests |
| Combat/BattleArena/ATB | `[atb-engine]`,`[combat-atb]`,`[arena-cat]`,`[arena-prefab]`, CheckBattleClosing | CombatTest, TurnTest, AiTest, TargetingTest, RngGoldenVectorTest, ArenaVMTests |
| Shop/economy | CheckVendorStock/Accessories, `[glimmer]`,`[econ-meta]`,`[covenant]`,`[pack-grant]` | ShopVMTests, EconomyServiceTests, PackStoreVMTests, WalletServiceTest |
| Tutorial/FTUE | CheckTutorialSteps, `[ftue-honesty]`(FBD), `[founding-reach]` | TutorialDirectorHubGateTest, HubScenesTest |
| Dialogue | CheckDialogueSpeakers, `[dialogue]`,`[townsfolk]` | DialogueRunnerTests, DialogueViewModelTests |
| Audio/SFX | `[sfx-webgl]`,`[sfx-resolve]`, CheckBattleClosing | JukeboxVMTests only (thin — no core audio-routing test) |
| Shared enemy (WO-772) | `[enemy-resolver]` | EnemyResolverSpawnTests (PlayMode) |
| Barracks/troops (WO-771.9) | `[troop-roster]` (BarracksProgression, TroopStatResolver) | BarracksProgressionTests, TroopTrainingVMTests, TroopStatResolverTests |

**RED — genuine data-verify gaps (per `docs/reference/REGRESSION_COVERAGE_MATRIX.md`):** the table
measures *presence*, not defect-specific coverage. Unguarded classes worth flagging:
- **NavMesh reachability / wall-carve** — `CastleNavTopologyDiag` exists but is **opt-in, NOT
  registered in `RunAll`**; no PlayMode reachability suite in the automated gate.
- **Feature-flag defaults** — no `[feature-flag-defaults]` oracle; a silent flag flip ships green.
- **Authored-portal ↔ enabled-scene join** — no oracle.
- Echo non-Harvest lane consumers + exhaustive save round-trip — write-side/subset only.

**EditMode debt state:** the "~17 failures" framing conflates two markers. Live known-failing baseline
is **8 (WO-545, still OPEN** — `WorkOrders/WORK_ORDER_545_editmode_pre_existing_test_failures.md:3`):
BuildingCatalog ×3 (9→10 building data drift), Wallet log-assert ×3, `ModalPanelDiscipline`,
`UnityObjectNullCoalesceLint`. The other ~16 (Core `_state` null) were **already fixed** (43→59/59).
There is no red-marker file enumerating a current 17.

---

## 4. WO BACKLOG vs REALITY

`CLI_LANES_WO_NUMBERS.md` has two conflicting authorities: the FROZEN-HISTORY body (`:35` "Next free
WO = 430", flagged stale at `:20`) and the live top banner (`:3` "next-free = **775**, 761–774
CONSUMED"). The 430 is dead history — the **live authority is 775**.

| WO | Doc/anchor claims | TRUE tree status | Evidence |
|---|---|---|---|
| **770** dungeon loop | 7/11 sub-orders DONE | **IN-FLIGHT** (7 shipped, 4 backlog) | `docs/qa/SUNDAY_STATUS_2026-07-26.md:16-27` (commit hashes); `docs/qa/dungeon-raid-validation-2026-07-26.md`. No RESULT files on disk. 770.5/.6/.8/.10/.11 remain backlog. |
| **771** raid (Teleport/Deploy) | "nothing built" | **WIRED / largely built** | `RaidSelectionScreen.cs`, `RaidDeployScreen/Controller/VM.cs`, `RaidScoring.cs`, `RaidClaimService.cs`, `RaidVictoryController.cs`; Raid ON, raidwalk OFF |
| **771.9** barracks/troops | BACKLOG | **DONE / wired** | `GameState.cs:480-497`; `BarracksService.cs`(311), `BarracksProgression.cs`(237), `TroopTrainingPanel.cs`(602), `ArmyStorage.cs`(272); barracks.json + troops.json; ff.barracks ON |
| **772** shared enemy system | BLOCKED (owner gate) | **DONE (Phase 1)** | `Assets/_Modules/Core/Enemies/EnemyResolver.cs`(258, ratified Hollow Ones); `EnemyResolverRegression.cs`; `EnemyResolverSpawnTests.cs`; enemies.json |
| **773** Obsidian queue | BACKLOG | **DONE** (save v35) | `ObsidianQueueEngine.cs`, `BuildTimerService.cs`(672); `SaveSchema.cs:35` + MigrateToV35; `ObsidianQueueTests.cs`, `ObsidianQueueRegression.cs` |
| **774** raid loadout/deploy-ring/naming | SPEC READY | **SPEC-ONLY** | `WorkOrders/WORK_ORDER_774_raid_loadout_deployring_naming.md` (Status: READY TO IMPLEMENT; deploy-ring not built) |

WO spec files present: `docs/qa/WORK_ORDER_770..773_*.md` + `WorkOrders/WORK_ORDER_774_*.md`. No
RESULT files for 770-773 (evidence is commit hashes in the status ledger).

---

## 5. ART / ASSET GAPS

Policy: `tools/art/REQUIRED_PACKS.md` (tracked runtime fallbacks in `Resources/` vs gitignored source
packs that travel by zip). Ground truth on THIS machine:

- **Tracked runtime fallback cast — present:** `Resources/Enemies/*` ALL present
  (Skeleton_Warrior/Rogue/Mage/Healer/Golem/Minion, Orc_*, Necromancer, Boss_Dragon.prefab);
  `Resources/NPCs/*` all 4 present; `Resources/Heroes/*` present EXCEPT the raw `Mage.fbx`/`Cleric.fbx`
  meshes (their `.controller` + `.tripo-extracted/` folders ARE present; Knight/KnightV3/knightV2 fbx
  present). **Minor flag:** if Mage/Cleric are meant to render from a raw fbx (not a prefab) they'd be
  bodyless — worth confirming, but Knight (the V1 hero) is fully present.
- **Gitignored source packs on this machine:** KayKit (61k+ files incl. Dungeon Remastered 1.1,
  Skeletons 1.1, Adventurers 2.0), People (LFS), polyperfect, Supercyan all PRESENT. **Quaternius
  ABSENT** — gitignored/travels by zip; only RED if a boot path needs it (fallback cast covers boot).
- **People/textures gap — CONFIRMED as documented:** shared `Assets/Models/People/textures/` ABSENT
  (gitignored); per-model `People/<NPC>/Textures/*.png` PRESENT with real PNGs — so committed
  Blacksmith/Merchant/Peasant NPCs texture correctly; only bodies referencing the shared folder bite.
- **KayKit Phase-2 (armed models) — UNBUILT / gated OFF:** `ff.enemyweapons` default **OFF**
  (`FeatureFlags.cs:218`) — enemies spawn weaponless. Attach path (`EnemyFactory.AttachEnemyWeapon`,
  `:170`) intact but exercised only for Orc_Berserker+axe_A behind the flag. "One perfect armed type
  first" (Hollow Warrior) not yet productionized.
- **`tools/art/verify-runtime-art.ps1` EXISTS** (canon "implement if missing" already satisfied).

---

## 6. STALE DOCS — one-line flags (canon-sync + doc-team lanes own the fixes)

The load-bearing set is behind the tree. `docs/HANDOVER.md` top block is the closest to current
(branch/HEAD/raid-model/next-free correct; only save version wrong). The worst is `MASTER_CATALOG.md`.

- `STALE: SESSION_CANON_LOADER.md:15,114` — says save v34; tree v35.
- `STALE: SESSION_CANON_LOADER.md:117` — says `ff.atbdungeon` OFF; that flag does not exist (real gate `ff.dungeonrealtime`).
- `STALE: SESSION_CANON_LOADER.md:128` — home hub `MainCastle_Hall`; tree = `Main_Castle_Overworld` (merged world).
- `STALE: SESSION_CANON_LOADER.md:8-11` — "771 + 773 all BACKLOG"; both built at v35.
- `STALE: PIPELINE_STATE.md:24-26` — "WO-771 nothing built / WO-773 BACKLOG"; both built.
- `STALE: PIPELINE_STATE.md:34` — "ff.barracks default unchanged"; now ON.
- `STALE: PIPELINE_STATE.md:81,85` — `ff.atbdungeon` OFF + hub `MainCastle_Hall`; both wrong.
- `STALE: docs/HANDOVER.md:22` — save v34; tree v35.
- `STALE: docs/HANDOVER.md:47-50` — "Raid V1 nothing built / WO-773 open"; both built.
- `STALE: docs/MASTER_CATALOG.md:52,281` — next-free WO 412; tree 775.
- `STALE: docs/MASTER_CATALOG.md:188,411` — SaveSchema v30/v20; tree v35.
- `STALE: docs/MASTER_CATALOG.md:131,116,138-140` — hub `MainCastle_Hall`; tree merged world (no inline correction here, unlike ARCHITECTURE.md).
- `STALE: docs/MASTER_CATALOG.md:204-214` — raid described as "two mechanisms"; loop now LOCKED Teleport/Deploy.
- `STALE: docs/MASTER_CATALOG/<area>.md` — all area sections dated 2026-06-12 / labelled `feat/tower-core-loop`; weeks stale.
- `STALE: PROJECT_INDEX.md:55` — WO numbering "runs through 602"; tree 775.
- `STALE: PROJECT_INDEX.md:11-28` — names 07-08 anchor as live; current is 07-26 (top banner corrects branch/HEAD).
- `STALE: docs/ARCHITECTURE.md:174,167` — SaveSchema v20 (inline correction says v34, itself now stale); tree v35.
- `STALE: docs/ARCHITECTURE.md:108,87` — hub `MainCastle_Hall` (correction banner flags it); tree merged world. (ARCHITECTURE §3 is the ONE doc correct on `ff.atbdungeon` not existing.)
- `STALE (comment lie): FeatureFlags.cs:84` — XML doc "Default ON" for raidwalk; code `:88` is OFF.
- `STALE (comment lie): RaidVictoryController.cs:32-34` — header says stars/loot "OUT"; code `:175-182` builds them.

---

## 7. PRIORITIZED LEDGER — GREEN / YELLOW / RED

### 🟩 GREEN (in sync / healthy)
- Compile-look clean on save/flag/migrator layer; RegressionSuite brace flag = false positive; no NUL bytes. — *lane: CLI/gate*
- Save v35 + migrator chain complete v2→v35. — *Core/Save*
- Feature-flag defaults match the raid-lock canon (raidwalk OFF, overworldencounter OFF, barracks ON, dungeonfpv ON, buildtimers ON). — *Core*
- All 4 core loops WIRED (wave / dungeon / raid / build-queue). — *Combat-AI / Dungeons / Raid / Economy*
- WO-772 Phase 1, WO-773, WO-771.9 all BUILT + tested + regression-covered. — *Enemies / Economy / Raid*
- Every major system has ≥1 oracle + ≥1 test (presence coverage). — *QA*
- Tracked runtime art fallback cast present; `verify-runtime-art.ps1` exists. — *Art*

### 🟨 YELLOW (drifted — fix queued, not blocking)
- **Whole load-bearing doc set is behind the tree** (save version, hub scene, raid model, WO status, next-free number) — §6 flags. Fix in the same breath per CLAUDE.md §15. — *lane: canon-sync + doc-team*
- Two in-code comment lies: `FeatureFlags.cs:84` (raidwalk "ON") + `RaidVictoryController.cs:32` (stars/loot "OUT"). — *lane: owning code silo*
- `CLI_LANES_WO_NUMBERS.md` frozen-history "430" reads as authoritative next to the live "775" banner. — *doc-team*
- `MASTER_CATALOG/<area>.md` sections dated 06-12 / `feat/tower-core-loop` label — big catalog re-verify still queued as a housekeeping WO. — *doc-team*
- Raid claim persistence is PlayerPrefs, not SaveSchema/cloud (`RaidClaimService.cs:14`); `OnAutoRecommend` stub; WO-774 deploy-ring SPEC. — *Raid lane*
- KayKit Phase-2 armed models unbuilt (`ff.enemyweapons` OFF) — intentional "perfect one first". — *Art / Enemies*
- People/textures shared folder + Quaternius absent on this machine (gitignored, travel-by-zip) — only bites if a boot path needs them. — *Art*
- Audio coverage thin on the test side (JukeboxVMTests only; no core audio-routing test). — *QA*

### 🟥 RED (broken / missing / unverified — resolve before a build SHIP)
- **Full `DataRegression.RunAll` NOT re-run since 2026-07-22** — last certified `REGRESSION_OK` predates the WO-771/772/773 + v35 build wave. **Re-run before shipping** (`docs/qa/SUNDAY_STATUS_2026-07-26.md:114`). — *lane: CLI/gate*
- **WO-545: 8 pre-existing EditMode failures still OPEN** — test gate not clean (BuildingCatalog ×3 data drift, Wallet log-assert ×3, ModalPanelDiscipline, NullCoalesceLint). — *lane: CLI*
- **CS-1 real bug (still open):** equipped ring/amulet (`equippedRingId`/`equippedAmuletId`, migrator-seeded v26) has NO `GameState` field / no Snapshot-Apply → **resets on reload**. Needs a ticket. — *Core/Save*
- **NavMesh reachability has no gate oracle** — `CastleNavTopologyDiag` is opt-in, not in `RunAll`; scene-reachability regressions ship green. — *QA*
- **No feature-flag-defaults oracle** — a silent flip (like the raidwalk/barracks/overworldencounter changes this wave) ships green with no data-verify. Given how many defaults moved this wave, this is the highest-leverage new oracle to add. — *QA*
- Mage/Cleric raw `.fbx` meshes absent from `Resources/Heroes/` (controllers + extracted folders present) — confirm they render from a prefab, else non-Knight heroes are bodyless. Knight (V1 hero) is fine. — *Art*

---
*Read-only audit. Sourced from HEAD `8e1ec74a` + working tree, cross-checked by 5 parallel SME agents.
No files edited except this report. Supersedes the status ledger in `CANON_GROUND_TRUTH_2026-07-26.md`
§2-6 where they conflict (the anchor was a doc-only pass written before the build wave landed).*
