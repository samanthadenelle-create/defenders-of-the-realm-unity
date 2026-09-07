# REGRESSION-ORACLE INVENTORY - known dictionary (verified 2026-09-06)

> Every `[tag]` registered between the START and END fences of
> `Assets/Editor/Regression/DataRegression.cs`, with its suite file, the exact
> registration line (the row's citation), and one line on what it locks.
> Refreshed by the Sunday sweep (`SUNDAY_HOUSEKEEPING.md` section 2, step 5).
> Companion dictionary: `docs/reference/REGRESSION_COVERAGE_MATRIX.md`
> (findings -> covering suite). This file is the INVENTORY (suite -> what it locks).

## How this file was built (so any row can be re-derived, not remembered)

1. Fence extraction: `DataRegression.cs` lines between `>>> REGISTERED ORACLE SUITES - START FENCE <<<`
   and `>>> REGISTERED ORACLE SUITES - END FENCE <<<`.
2. One row per `log.AppendLine("[tag] " + reason)` inside that span. The fence's own explanatory
   comment (which contains a literal `[tag]`) is excluded.
3. Suite class resolved to a file by scanning every `.cs` under `Assets/` for its `class` declaration.
   **All 417 resolved; zero missing.**
4. "What it locks" is derived MECHANICALLY from the suite's own header comment - the comment block
   immediately above the class declaration, else the file-top block, joined until ~165 chars.
   Where a suite carries no such comment the cell reads `no header` (8 suites - listed in the
   appendix). Non-ASCII characters in source comments are transliterated. **These cells are the
   suites' own words, not an audit of what they actually assert.**

## THE COUNT MOVED WHILE THIS WAS BEING WRITTEN - read this before quoting a number

Parsed from the **WORKING TREE at 2026-09-06 20:13**, because the working tree is what a gate run
executes. Three separate counts, all measured tonight:

| Tree / run | Registered oracles | Measured |
|---|---|---|
| `Builds/reg-final2.log` run (18:50) | **414** | the marker itself |
| `HEAD` = `b30e551ce` (20:13) | **416** | fence parse of `git show HEAD:...` |
| Working tree (20:13) | **417** | the table below |

`DataRegression.cs` is `M` in `git status`; the lead was committing lane work through the evening,
and an earlier parse of the same file at 20:09 counted 414. **So the 18:50 green baseline does NOT
cover every suite in this list** - at least three registrations landed after it. Re-run the gate and
re-derive this table after tonight's commits settle; do not quote 417 as "the" suite count tomorrow.

## COMPLETENESS PROOF

**417 tag lines parsed == the working tree's fence content at 20:13.** If a future parse disagrees
with a run's `REGRESSION_OK <n>/<n>` marker, one of the two is stale and neither may be assumed -
check the log's mtime against the file's.

## KNOWN-RED BASELINE (measured, not remembered)

| Fact | Value | Where measured |
|---|---|---|
| Marker | `REGRESSION_OK 414/414 suites -- 414 green, 0 red, 0 skipped` | `Builds/reg-final2.log:133487` |
| Log mtime | 2026-09-06 18:50 | filesystem stat |
| Log size | 13,034,993 bytes | filesystem stat |
| `error CS` lines in that log | **0** | `grep -c "error CS" Builds/reg-final2.log` (SUNDAY_HOUSEKEEPING section 3 rule 2 - the marker alone is not enough) |

**Known red: NONE.** The baseline is fully green as of 2026-09-06 18:50, over the 414 suites that
existed then. Any later claim of a red must cite its own fresh log, not this row.

## SUITES THAT EXIST BUT ARE NOT COUNTED BY THE MARKER

Found by the harness audit and re-verified against the tree on 2026-09-06
(`find Assets -name "<Name>.cs"` + `grep -n "<Name>" DataRegression.cs`).

| Suite file | In tree | Referenced in DataRegression.cs | Status |
|---|---|---|---|
| `Assets/Editor/Regression/ArenaCombatOracle.cs` | yes | 0 refs | UNREGISTERED - never runs from `DataRegression.RunAll` |
| `Assets/Editor/Regression/AssetMoveManifestRegression.cs` | yes | 1 ref, **line 170** | **RUNS BUT IS UNCOUNTED** - `.Verify(failures, log)` is called BEFORE the start fence, i.e. before the `suiteTagLinesBefore` snapshot, so nothing it appends reaches the denominator. Its failures DO still land in `failures`. The fence comment warns about lines added below the END fence; this is the same defect above the START fence. |
| `Assets/Editor/Regression/BlankStartCensusRegression.cs` | yes | 0 refs | UNREGISTERED |
| `Assets/Editor/Regression/CombatFoundationRegression.cs` | yes | 0 refs | UNREGISTERED |
| `Assets/Editor/Regression/EnemyArtCoverageRegression.cs` | yes | 0 refs | UNREGISTERED - note CLAUDE.md section 16: a missing R2 push fails SILENTLY, so an enemy-art oracle that never runs is a gap with a three-incident history behind it |
| `Assets/Editor/Regression/GearAddressableGroupRegression.cs` | yes | 0 refs | UNREGISTERED |
| `Assets/Editor/Regression/RepairProbeRegression.cs` | yes | 0 refs | UNREGISTERED |
| `Assets/Editor/Regression/BuildAffordabilityWordsRegression.cs` | yes | 1 ref, **line 530** | **NO LONGER UNREGISTERED** - inside the fence (Guard.Try-wrapped), emits `[build-affordability-words]`. The audit listed it as pending; it has landed. |
| `Assets/Editor/Regression/TroopTargetPreferenceRegression.cs` | yes | 1 ref, **line 1360** | **NO LONGER UNREGISTERED** - inside the fence, emits `[troop-target-preference]`. Landed. |

Net: **7 suites are not counted** (6 never run; `AssetMoveManifestRegression` runs uncounted), not 9.

Separately, tag-emitting groups exist OUTSIDE the fences (e.g. the tutorial-v2 registry, vendor-stock).
They run but are not part of the denominator. Out of scope here by design - the fence defines the
counted set.

## THE REGISTERED ORACLES (417, working tree 2026-09-06 20:13)

| # | `[tag]` | Suite class / file | Reg. line | What it locks (the suite's own header comment) |
|---|---|---|---|---|
| 1 | `[covenant]` | `Assets/Editor/Regression/MonetizationCovenantRegression.cs` | 309 | MonetizationCovenantRegression - the build-gate that ENFORCES the monetization covenant the canon has long *claimed* to enforce (skr_staking.json says a "SkrStakingReg... |
| 2 | `[battle-monthly]` | `Assets/Editor/Regression/BattleMonthlyRegression.cs` | 321 | Pins the battle-pass / monthly-card firewall, the vapor rule and the owner rulings. |
| 3 | `[tower-perks]` | `Assets/Editor/Regression/TowerPerkRegression.cs` | 322 | TowerPerkRegression - WO-432 (owner 2026-06-28). Headless gate for the DESIGNED, data-driven tower-upgrade tech (tower-perks.json + DeNelle.Village.TowerPerkTable). |
| 4 | `[tower-respawn]` | `Assets/Editor/Regression/TowerRespawnRegression.cs` | 324 | TowerRespawnRegression - F8-39 "towers vanish on death, ALL return on next placement." Headless, no-scene, no-PlayMode oracle that proves the DATA/LOGIC contradiction ... |
| 5 | `[hub-scene-literal]` | `Assets/Editor/Regression/HubSceneLiteralRegression.cs` | 325 | HubSceneLiteralRegression [hub-scene-literal]   Marker: HUB_SCENE_LITERAL_OK / _FAIL |
| 6 | `[def-target]` | `Assets/Editor/Regression/DefenseTargetableRegression.cs` | 326 | DefenseTargetableRegression - the DATA-decidable half of F8-41 "waves must ATTACK the city", proven headless in SECONDS (no scene drive, no play mode). |
| 7 | `[arena-prefab]` | `Assets/Editor/Regression/ArenaPrefabAuditRegression.cs` | 327 | ArenaPrefabAuditRegression (F8-37 "arena pole") -- EDITOR ASSET AUDIT oracle. |
| 8 | `[core-datahub]` | `Assets/Editor/Regression/CoreDataHubRegression.cs` | 329 | CoreDataHubRegression - the canonical-data-hub sync + read contract. |
| 9 | `[core-catalog]` | `Assets/Editor/Regression/CoreCatalogRegression.cs` | 330 | CoreCatalogRegression - the Core catalog/registry read + mapping contract. |
| 10 | `[core-world]` | `Assets/Editor/Regression/CoreWorldLogicRegression.cs` | 331 | CoreWorldLogicRegression - the shared world classifier + spawn-roster contract. |
| 11 | `[core-save]` | `Assets/Editor/Regression/CoreSaveContractRegression.cs` | 332 | CoreSaveContractRegression - the save version-triple + migrate/round-trip contract. |
| 12 | `[hero-prog]` | `Assets/Editor/Regression/HeroProgressionRegression.cs` | 333 | HeroProgressionRegression - headless "real object in, real response out" oracle for the hero XP / level / reward curves (village-hero silo). |
| 13 | `[aegis]` | `Assets/Editor/Regression/AegisSetReachabilityRegression.cs` | 334 | AegisSetReachabilityRegression - headless oracle proving the WO-295 "Oathweld" legendary set bonus is REACHABLE (a full Aegis set can actually be assembled). |
| 14 | `[build-upgrade]` | `Assets/Editor/Regression/BuildingUpgradeRegression.cs` | 335 | BuildingUpgradeRegression - headless oracle for the resource-building upgrade tables (Farm / Lumbermill / Forge) and the DEF-121 Magic-gated Arcane Forge tier. |
| 15 | `[offline-harvest]` | `Assets/Editor/Regression/OfflineHarvestRegression.cs` | 336 | OfflineHarvestRegression - headless oracle for the WO-115 offline-accrual clock: cap clamp, backwards-clock (anti-tamper) monotonic guard, and advance-even-on-zero (th... |
| 16 | `[offline-fanout]` | `Assets/Editor/Regression/OfflineClaimFanOutRegression.cs` | 337 | OfflineClaimFanOutRegression -- headless oracle for WO-1147: ONE offline clock, ONE read, ONE fan-out, ONE advance. |
| 17 | `[defense-report]` | `Assets/Editor/Regression/DefenseReportContractRegression.cs` | 343 | Contract oracle for the WO-1026 defence report + its layout fingerprint. |
| 18 | `[siege-cadence]` | `Assets/Editor/Regression/SiegeCadenceRegression.cs` | 344 | Cadence + offline-pressure oracle for the WO-1026 siege scheduler. |
| 19 | `[siege-spawn-authority]` | `Assets/Editor/Regression/SiegeSpawnAuthorityRegression.cs` | 345 | Duplicate-authority lint for the WO-1026 siege lane. |
| 20 | `[lookout-alert]` | `Assets/Editor/Regression/LookoutAlertRegression.cs` | 346 | WO-1184 lookout presentation oracle. |
| 21 | `[siege-loss-stakes]` | `Assets/Editor/Regression/SiegeLossStakesRegression.cs` | 347 | Oracle for the WO-1026 loss stakes: bounded bank theft, billed once. |
| 22 | `[accrual-trust]` | `Assets/Editor/Regression/OfflineAccrualTrustRegression.cs` | 352 | OfflineAccrualTrustRegression - headless oracle for WO-1128's CLIENT half: every offline window records WHICH CLOCK produced it, and records its own endpoints, so the ... |
| 23 | `[dev-time-skip]` | `Assets/Editor/Regression/DevTimeSkipRegression.cs` | 357 | DevTimeSkipRegression -- headless oracle for the DEV queue time-skip (owner ask 2026-08-04: "a speed timer for testing building queues ... but NOT impact the battle ti... |
| 24 | `[village-econ]` | `Assets/Editor/Regression/VillageEconomyRegression.cs` | 358 | VillageEconomyRegression - headless oracle for the village economy wallets: (A) CRYSTAL single-source-of-truth across the THREE stores that expose it, and (B) the Wood... |
| 25 | `[arena-cat]` | `Assets/Editor/Regression/ArenaCatalogRegression.cs` | 359 | ArenaCatalogRegression - headless oracle for the seeded Arena data spines: ArenaCatalog (3 opponents) + ArenaDefenseCatalog (6 defenders + point pool). |
| 26 | `[companion-roster]` | `Assets/Editor/Regression/CompanionRosterRegression.cs` | 360 | Data/logic regression for the companion roster: mapping (companion != hero, bijective), per-class dialogue names/intros, and the gear-up grant table. Real static game ... |
| 27 | `[troop-roster]` | `Assets/Editor/Regression/TroopRosterRegression.cs` | 362 | Data/logic regression for the Barracks troop roster: exact 9-id set, the unlock-tier ladder, cost/slot sanity, the WO-735 visual keys, the WO-734 barracks tier announc... |
| 28 | `[raid-scoring]` | `Assets/Editor/Regression/RaidScoringRegression.cs` | 364 | Data + source regression for the V1 raid scoring/loot/HUD slice. Real static game code in (RaidScoring.ComputeStars/ComputeLoot), asserted out; plus a source-lint that... |
| 29 | `[ad-seam]` | `Assets/Editor/Regression/AdServiceSeamRegression.cs` | 366 | Source-lint: the rewarded-ad provider is reachable only through <c>DeNelle.Core.Ads.IAdService</c>. Returns true (summary) / false (detail); never throws. |
| 30 | `[pi-ad-reward]` | `Assets/Editor/Regression/PiAdRewardVerificationRegression.cs` | 373 | Pins the Pi rewarded-ad grant rule. Returns true (summary) / false (detail); never throws. |
| 31 | `[android-content-target]` | `Assets/Editor/Regression/AndroidContentTargetRegression.cs` | 374 | AndroidContentTargetRegression - WO-1124. Proves the content-build target gate actually FAILS the known-bad state, and that the APK builder still switches the target b... |
| 32 | `[ps1-encoding]` | `Assets/Editor/Regression/PowerShellEncodingRegression.cs` | 377 | PowerShellEncodingRegression -- WO-1187. Makes "this .ps1 will silently never run" mechanically detectable, instead of discoverable only by accident. |
| 33 | `[softlock-classifier]` | `Assets/Editor/Regression/SoftlockClassifierRegression.cs` | 378 | WO-1237: proves the watchdog distinguishes an idle player from a stuck world. |
| 34 | `[battle-quiescence]` | `Assets/Editor/Regression/BattleQuiescenceRegression.cs` | 379 | BattleQuiescenceRegression - WO-1127. Proves the battle-end teardown contract FAILS every known-bad state, and PASSES a clean teardown. |
| 35 | `[knight-directional-death]` | `Assets/Editor/Regression/KnightDirectionalDeathRegression.cs` | 380 | WO-586: directional AnyState transitions must precede generic Death. |
| 36 | `[army-muster-layout]` | `Assets/Editor/Regression/ArmyMusterLayoutRegression.cs` | 381 | ArmyMusterLayoutRegression [army-muster-layout] - WO-1230. Markers: ARMY_MUSTER_LAYOUT_OK / ARMY_MUSTER_LAYOUT_FAIL |
| 37 | `[structure-seat]` | `Assets/Editor/Regression/StructureSeatRegression.cs` | 382 | Runtime oracle: <see cref="VisualFactory"/> seats a skinned body's world-bounds BOTTOM on the host's ground plane, regardless of where the model's pivot sits. Returns ... |
| 38 | `[structure-cadence]` | `Assets/Editor/Regression/StructureCadenceRegression.cs` | 383 | Footprint-cadence oracle over structures-catalog.json: no structure may render wildly wider than its family. Returns true (summary) / false (detail); never throws. |
| 39 | `[structure-load-bounded]` | `Assets/Editor/Regression/StructureLoadBoundedRegression.cs` | 384 | Source-lint: the structure asset load path is incapable of an unbounded main-thread block, degrades by SKIPPING (baked twin survives), and reports every skip loudly. R... |
| 40 | `[structure-factory-residency-retry]` | `Assets/Editor/Regression/StructureFactoryResidencyRetryRegression.cs` | 385 | WO-1142: pins the one StructureAssetLoader caller that used to drop paid structures on a first-frame residency miss. Registered by DataRegression beside structure-load... |
| 41 | `[sheathe-pose]` | `Assets/Editor/Regression/SheathePoseRegression.cs` | 386 | Pins the owner's 2026-08-20 sheathe ruling: hip anchor, one socket per slot, long axis vertical + inverted. Returns true (summary) / false (detail); never throws. |
| 42 | `[offline-pull]` | `Assets/Editor/Regression/OfflinePullRegression.cs` | 387 | OfflinePullRegression - PROD-010. Proves the opt-in offline pull cannot repeat the defect it shipped with on 2026-08-19. |
| 43 | `[enemy-load-bounded]` | `Assets/Editor/Regression/EnemyLoadBoundedRegression.cs` | 388 | Source-lint: the enemy asset load path is incapable of an unbounded main-thread block, degrades by SKIPPING to a placeholder that later RE-SKINS, and reports every ski... |
| 44 | `[content-packing]` | `Assets/Editor/Regression/ContentPackingRegression.cs` | 389 | Proves the per-family / per-asset content packing intent still holds. |
| 45 | `[dungeon-camera-feel]` | `Assets/Editor/Regression/DungeonCameraFeelRegression.cs` | 390 | DungeonCameraFeelRegression [dungeon-camera-feel] -- locks the boundary between the OUTDOOR world-aesthetics pass and an ENCLOSED interior camera. |
| 46 | `[dungeon-movement-owner]` | `Assets/Editor/Regression/DungeonMovementOwnerRegression.cs` | 391 | Pins the dungeon transform-ownership contract and the movement-basis rule. Returns true (summary) / false (detail); never throws. |
| 47 | `[enemy-tint]` | `Assets/Editor/Regression/EnemyTintRegression.cs` | 392 | Oracle for the enemy body-colour guard and the family tint authority. |
| 48 | `[enemy-body-texture]` | `Assets/Editor/Regression/EnemyBodyTextureRegression.cs` | 393 | EnemyBodyTextureRegression - the standing guard for the "enemies not having coloring" defect (owner report, proven by EnemyProvingHarness.RunBatch). |
| 49 | `[ad-covenant]` | `Assets/Editor/Regression/AdPlacementCovenantRegression.cs` | 395 | AdPlacementCovenantRegression - ad-placements.json obeys the covenant AND the ad networks' reward policy. Marker: AD_COVENANT_OK |
| 50 | `[ui-surface-probe]` | `Assets/Editor/Regression/UiSurfaceProbeRegression.cs` | 398 | Falsifies <see cref="UiSurfaceProbe"/>: each of the four failure classes must be able to fire on its own, and a healthy surface must still pass. Returns true (summary)... |
| 51 | `[combat-cast-caravan-mark]` | `Assets/Editor/Regression/CombatCastCaravanMarkRegression.cs` | 400 | CombatCastCaravanMarkRegression - WO-935 / WO-991 / WO-910 / WO-994 pins |
| 52 | `[hero-element-cast]` | `Assets/Editor/Regression/HeroElementCastVfxRegression.cs` | 401 | HeroElementCastVfxRegression - WO-875 focused standalone oracle. |
| 53 | `[townsfolk]` | `Assets/Editor/Regression/TownsfolkDialogueRegression.cs` | 402 | Data/logic regression for TownsfolkDialogue: stable archetype count, full name coverage, non-empty pools/lines, and the never-null LineFor contract. Returns true (summ... |
| 54 | `[atb-engine]` | `Assets/Editor/Regression/AtbEngineRegression.cs` | 403 | AtbEngineRegression - headless data/logic oracle for the isolated BattleATB engine (docs/MASTER_CATALOG/battle-atb.md). ZERO oracle coverage existed for this area befo... |
| 55 | `[econ-meta]` | `Assets/Editor/Regression/EconomyMetaCatalogRegression.cs` | 404 | EconomyMetaCatalogRegression - headless data-invariant gate for the economy-meta area (Pets / Cosmetics / Wallet / Web3 services + their canon JSON). |
| 56 | `[glimmer]` | `Assets/Editor/Regression/GlimmerEconomyRegression.cs` | 405 | no header |
| 57 | `[scene-route]` | `Assets/Editor/Regression/SceneRoutingRegression.cs` | 406 | SceneRoutingRegression - the DATA-decidable half of scene navigation, proven headless in SECONDS (no scene drive, no play mode). ZERO oracle coverage before this file ... |
| 58 | `[raid-hero-carry]` | `Assets/Editor/Regression/RaidHeroCarryRegression.cs` | 412 | RaidHeroCarryRegression (WO-1109) - the raid hero is the TOWN hero, carried, NOT the emergency fallback. Proven headless in milliseconds, no play mode. |
| 59 | `[composed-dungeon-run]` | `Assets/Editor/Regression/ComposedDungeonRunRegression.cs` | 413 | ComposedDungeonRunRegression (WO-1112) -- the composed (dg_*) dungeon is a REAL run: the hero has abilities, its keys and locks are visible, its lantern has a meter an... |
| 60 | `[hero-dedupe-survivor]` | `Assets/Editor/Regression/HeroDedupeSurvivorRegression.cs` | 420 | HeroDedupeSurvivorRegression (WO-1131) - the hero Ensure() operates on is the SURVIVOR of the dedupe, never an object the dedupe just destroyed. |
| 61 | `[art-resource]` | `Assets/Editor/Regression/ArtResourceRegression.cs` | 421 | ArtResourceRegression - headless "real object in -> assert -> one marker" oracle for the resources-art LOAD PATHS: dialogue portraits, the RPG-UI sprite atlas, the pro... |
| 62 | `[sfx-webgl]` | `Assets/Editor/Regression/SfxWebglAudioRegression.cs` | 423 | SfxWebglAudioRegression - headless oracle for the WO-682 defect class: a Resources/Sfx audio clip whose IMPORT diverges for WebGL fails FSB decode in the browser at er... |
| 63 | `[core-save-sme]` | `Assets/Editor/Regression/CoreSaveRegression.cs` | 425 | CoreSaveRegression - the full CORE/SAVE architecture-path suite (headless). |
| 64 | `[build-econ]` | `Assets/Editor/Regression/BuildEconomyRegression.cs` | 426 | BuildEconomyRegression - headless oracle for the BUILD MODE + BUILD ECONOMY data spine (structures-catalog.json -> CatalogRegistry -> StructureFactory / BuildModeControl... |
| 65 | `[obsidian-queue]` | `Assets/Editor/Regression/ObsidianQueueRegression.cs` | 427 | ObsidianQueueRegression - headless oracle for the common "Obsidian" multi-channel work queue (WO-773). Marker: OBSIDIAN_QUEUE_OK / OBSIDIAN_QUEUE_FAIL. |
| 66 | `[mercenary-hire]` | `Assets/Editor/Regression/BuildTimerMercenaryRegression.cs` | 429 | BuildTimerMercenaryRegression - LANE D mercenary hire system (gold skips time). Marker: MERCENARY_HIRE_OK / MERCENARY_HIRE_FAIL. |
| 67 | `[army-muster]` | `Assets/Editor/Regression/ArmyMusterRegression.cs` | 432 | ArmyMusterRegression - headless oracle for WO-897 "army composition auto-queues the build-outs". Marker: ARMY_MUSTER_OK / ARMY_MUSTER_FAIL. |
| 68 | `[manage-train-door]` | `Assets/Editor/Regression/ManageTroopsTrainDoorRegression.cs` | 438 | ManageTroopsTrainDoorRegression - headless oracle for PROD-013: the Manage screen's TROOPS tab is the ONE door to troop training, and it actually opens. Marker: MANAGE... |
| 69 | `[manage-progressive-disclosure]` | `Assets/Editor/Regression/ManageProgressiveDisclosureRegression.cs` | 439 | Source oracle for the phone Manage progressive-disclosure contract. |
| 70 | `[skill-tree-door]` | `Assets/Editor/Regression/HeroSkillTreeDoorRegression.cs` | 444 | HeroSkillTreeDoorRegression - headless oracle: the Bag's SKILLS tab is the one player-reachable door to the hero skill tree, and it is still wired. Marker: SKILL_TREE_... |
| 71 | `[hero-name-single-source]` | `Assets/Editor/Regression/HeroNameSingleSourceRegression.cs` | 446 | WO-1410: one canon noun per Hero destination, plus the Wisdom and Loadout doors. |
| 72 | `[manage-defense-door]` | `Assets/Editor/Regression/ManageDefenseUpgradeDoorRegression.cs` | 447 | ManageDefenseUpgradeDoorRegression - headless oracle: once a defensive structure is standing in this town, Manage's DEFENSE tab is the door to upgrading it, and it act... |
| 73 | `[ad-gate-arena]` | `Assets/Editor/Regression/AdGateAndArenaReturnRegression.cs` | 452 | AdGateAndArenaReturnRegression - headless oracle for two 2026-08-07 fixes that shipped WITHOUT a suite. Marker: AD_GATE_ARENA_OK / AD_GATE_ARENA_FAIL. |
| 74 | `[arena-return-music]` | `Assets/Editor/Regression/ArenaReturnMusicRegression.cs` | 453 | ArenaReturnMusicRegression [arena-return-music] -- WO-517 Pins the additive arena return to the position-aware WorldMusicDirector. |
| 75 | `[addressable-troop-visual]` | `Assets/Editor/Regression/AddressableTroopVisualRegression.cs` | 454 | WO-1143: pins remote troop recovery and a horizontal siege fallback silhouette. |
| 76 | `[troop-recovery]` | `Assets/Editor/Regression/ArmyRecoveryRegression.cs` | 456 | Headless oracle for WO-781 wounded-troop recovery: pure TickRecovery / AdvanceRecovery math + live-caller reachability. Returns true + summary / false + detail; never ... |
| 77 | `[data-web]` | `Assets/Editor/Regression/DataWebRegression.cs` | 457 | DataWebRegression - the data-catalog / web-platform sync gate. |
| 78 | `[hud-ui-sme]` | `Assets/Editor/Regression/HudUiRegression.cs` | 458 | HudUiRegression - headless HUD/UI defect-class gate (data + logic only). |
| 79 | `[raid-thumb-band]` | `Assets/Editor/Regression/RaidHudThumbBandRegression.cs` | 463 | Asserts the raid deploy bar and the hero ability row occupy EXCLUSIVE screen bands, measured from the authored anchors on both sides, and that neither side re-authors the |
| 80 | `[combat-atb]` | `Assets/Editor/Regression/CombatAtbRegression.cs` | 464 | CombatAtbRegression - the 5th SME headless suite: the COMBAT / ATB architecture path (docs/MASTER_CATALOG/battle-atb.md + docs/COMBAT_PIVOT_NORTHSTAR.md). Pure "real o... |
| 81 | `[dialogue]` | `Assets/Editor/Regression/DialogueRegression.cs` | 465 | DialogueRegression - headless oracle for the CUSTOM MVVM DIALOGUE spine (WO-455 rebuild, Yarn fully removed WO-557). The 6th SME regression path. |
| 82 | `[enemy-rig-color]` | `Assets/Editor/Regression/EnemyRigColorRegression.cs` | 466 | EnemyRigColorRegression -- "call up every enemy, prove each is RIGGED + COLORED". |
| 83 | `[enemy-resolver]` | `Assets/Editor/Regression/EnemyResolverRegression.cs` | 468 | EnemyResolverRegression - headless proof that the generic-skeleton bug is FIXED (WO-772 Phase 1 / A5, ruling PAIN_POINTS_2026-07-26 ?1.1). |
| 84 | `[enemy-addr-catalog]` | `Assets/Editor/Regression/EnemyAddressableCatalogRegression.cs` | 473 | EnemyAddressableCatalogRegression - the enemy ADDRESSES must actually be in the Addressables catalog once the art leaves Resources. |
| 85 | `[overworld-combat-gate]` | `Assets/Editor/Regression/OverworldCombatGateRegression.cs` | 475 | OverworldCombatGateRegression (WO-771) - proves the two gates that stop the unwanted / DEPRECATED overworld combat stay in place. |
| 86 | `[destroyed-structure]` | `Assets/Editor/Regression/DestroyedStructureRegression.cs` | 477 | DestroyedStructureRegression - WO-753 owner ruling oracle. |
| 87 | `[repair-hud-contract]` | `Assets/Editor/Regression/RepairHudContractRegression.cs` | 483 | RepairHudContractRegression - pins the Village->HUD REPAIR REFLECTION CONTRACT. |
| 88 | `[repair-prompt-readability]` | `Assets/Editor/Regression/RepairPromptReadabilityRegression.cs` | 484 | no header |
| 89 | `[orc-binding]` | `Assets/Editor/Regression/OrcRigBindingAudit.cs` | 485 | OrcRigBindingAudit - asset oracle for Tripo orc mesh ? skeleton binding. RCA 2026-07-11: OrcHumanoid FBXs report OK Humanoid avatar but visible body is rigid tripo_par... |
| 90 | `[hero-loco-clips]` | `Assets/Editor/Regression/HeroLocomotionClipRegression.cs` | 486 | Hero locomotion clip regression - knight walk/run must NOT resolve to 0_T-Pose takes. Self-contained (no MotionCastings/MotionClipPicker) - EditorRegression cannot ref... |
| 91 | `[ui-obsidian]` | `Assets/Editor/Regression/UiObsidianConformanceRegression.cs` | 488 | UiObsidianConformanceRegression - the build-gate that ENFORCES the "style everything through the Obsidian kit" law the owner has been policing BY EYE. |
| 92 | `[ui-mvvm]` | `Assets/Editor/Regression/UiMvvmConformanceRegression.cs` | 489 | UiMvvmConformanceRegression - the build-gate that ENFORCES strict MVVM: a View is a dumb skin that binds a ViewModel and NEVER reads/reconciles game state at runtime. ... |
| 93 | `[ui-capture-fidelity]` | `Assets/Editor/Regression/UiCaptureFidelityRegression.cs` | 499 | UiCaptureFidelityRegression [ui-capture-fidelity] |
| 94 | `[hud-posture]` | `Assets/Editor/Regression/HudPostureRegression.cs` | 500 | HudPostureRegression - headless pursuit-pulse lifecycle oracle (HUD flip contract). |
| 95 | `[scene-posture-seam]` | `Assets/Editor/Regression/ScenePostureSeamRegression.cs` | 505 | ScenePostureSeamRegression [scene-posture-seam] -- WO-1436. |
| 96 | `[strategic-placement]` | `Assets/Editor/Regression/StrategicPlacementRegression.cs` | 508 | StrategicPlacementRegression - WO-673 L6: the five ?5 permission-gate tests for strategic building placement (docs/WO673_ARCHITECTURE_REVIEW.md ?5 + L6). |
| 97 | `[talent-strategy]` | `Assets/Editor/Regression/TalentStrategyRegression.cs` | 511 | TalentStrategyRegression - WO-676 ?C gates G1-G3 (+ the G4 fleet-probe spec) for the strategic skill-tree redesign. |
| 98 | `[echo-spec]` | `Assets/Editor/Regression/EchoSpecializationRegression.cs` | 514 | EchoSpecializationRegression - the ?2c permission-gate oracle for WO-738/830 (Echo specialization + affinity/synergy). Headless, data-decidable, no play-mode. |
| 99 | `[room-forge]` | `Assets/Editor/Regression/RoomForgeRegression.cs` | 516 | RoomForgeRegression (WO-745) - the Room Forge pipeline permission gate. |
| 100 | `[wave-scaling]` | `Assets/Editor/Regression/WaveScalingRegression.cs` | 522 | WaveScalingRegression [wave-scaling] -- proves the most-played mode ESCALATES. |
| 101 | `[enemy-rewards]` | `Assets/Editor/Regression/EnemyRewardRegression.cs` | 523 | EnemyRewardRegression [enemy-rewards] -- proves the most-played mode PAYS. |
| 102 | `[wall-mitigation]` | `Assets/Editor/Regression/WallHeartMitigationRegression.cs` | 524 | WallHeartMitigationRegression [wall-mitigation] -- proves walls actually PROTECT. |
| 103 | `[pack-grant]` | `Assets/Editor/Regression/PackGrantRegression.cs` | 525 | PackGrantRegression [pack-grant] -- proves a purchased pack DELIVERS (ECON-01/02). |
| 104 | `[builder-sku]` | `Assets/Editor/Regression/BuilderSkuRegression.cs` | 526 | BuilderSkuRegression [builder-sku] -- WO-1253: permanent builder is CONCURRENCY. |
| 105 | `[temporary-builder]` | `Assets/Editor/Regression/TemporaryBuilderRegression.cs` | 527 | no header |
| 106 | `[card-collection-foundation]` | `Assets/Editor/Regression/CardCollectionFoundationRegression.cs` | 528 | no header |
| 107 | `[build-collection-player]` | `Assets/Editor/Regression/BuildCollectionPlayerRegression.cs` | 529 | no header |
| 108 | `[build-affordability-words]` | `Assets/Editor/Regression/BuildAffordabilityWordsRegression.cs` | 530 | WO-1411 - THE BUILD FLOW SAYS WHAT IT COSTS, WHAT IT TAKES, AND WHAT YOU CAN AFFORD. |
| 109 | `[post-wave-victory-modal]` | `Assets/Editor/Regression/PostWaveVictoryModalRegression.cs` | 531 | WO-1369: the hold must carry its REQUIRED liveness probe, and the probe must be the SAME expression the modal arbiter already holds - one liveness concept, not two. |
| 110 | `[night-market-shared-card]` | `Assets/Editor/Regression/NightMarketSharedCardRegression.cs` | 532 | no header |
| 111 | `[upgrade-authority]` | `Assets/Editor/Regression/BuildingUpgradeAuthorityRegression.cs` | 533 | BuildingUpgradeAuthorityRegression [upgrade-authority] -- proves a city upgrade writes the ONE authoritative store (GameState.BuildingTiers), not the legacy per-buildi... |
| 112 | `[queue-full-surface]` | `Assets/Editor/Regression/UpgradeQueueFullSurfaceRegression.cs` | 534 | UpgradeQueueFullSurfaceRegression [queue-full-surface] - WO-1045 + WO-1252. Marker: QUEUE_FULL_SURFACE_OK / QUEUE_FULL_SURFACE_FAIL. Expected: GREEN. |
| 113 | `[upgrade-family]` | `Assets/Editor/Regression/UpgradeFamilyPrecedenceRegression.cs` | 535 | UpgradeFamilyPrecedenceRegression [upgrade-family] -- pins THE INVARIANT that the START side and the COMPLETE side of a building upgrade resolve the SAME family ladder... |
| 114 | `[dualfamily-level-reset]` | `Assets/Editor/Regression/DualFamilyLevelResetRegression.cs` | 536 | DualFamilyLevelResetRegression [dualfamily-level-reset] -- pins the one-shot migration that resets the legacy RESOURCE-ladder level of every DUAL-FAMILY building to 1 ... |
| 115 | `[crystal-production]` | `Assets/Editor/Regression/CrystalProductionRegression.cs` | 537 | CrystalProductionRegression [crystal-production] -- the Crystal Mine oracle. |
| 116 | `[sfx-resolve]` | `Assets/Editor/Regression/SfxResolveRegression.cs` | 538 | SfxResolveRegression [sfx-resolve] -- proves the core one-shot SFX clips resolve. |
| 117 | `[dungeon-exit]` | `Assets/Editor/Regression/DungeonExitRegression.cs` | 539 | DungeonExitRegression [dungeon-exit] -- proves a composed dungeon can be LEFT. |
| 118 | `[dungeon-dressing]` | `Assets/Editor/Regression/DungeonDressingRegression.cs` | 540 | DungeonDressingRegression [dungeon-dressing] -- BEHAVIORAL proof of seating. |
| 119 | `[dungeon-return]` | `Assets/Editor/Regression/DungeonReturnSceneRegression.cs` | 541 | DungeonReturnSceneRegression [dungeon-return] - locks WO-770.2 (fixes D3): a dungeon encounter must round-trip back to the CURRENT dungeon scene, never a hardcoded one... |
| 120 | `[dungeon-lore]` | `Assets/Editor/Regression/DungeonLoreReadableRegression.cs` | 542 | DungeonLoreReadableRegression [dungeon-lore] - locks WO-770.4 (fixes D6): the lore-stone triple gap (no input caller for Read(), no subscriber for ReadRequested, no vi... |
| 121 | `[dungeon-state-reset]` | `Assets/Editor/Regression/DungeonStateResetRegression.cs` | 543 | DungeonStateResetRegression [dungeon-state-reset] - locks WO-770.9 (fixes D11): DungeonRuntimeState.OnEnable must reset the run IDENTITY (_dungeonId/_currentRoomId) an... |
| 122 | `[dungeon-defeat]` | `Assets/Editor/Regression/DungeonDefeatEndsRunRegression.cs` | 544 | DungeonDefeatEndsRunRegression [dungeon-defeat] - locks WO-770.3 (fixes D4): a LOST dungeon (ATB) fight must end the run and return to the Village, not be silently tre... |
| 123 | `[dungeon-exit-reachable]` | `Assets/Editor/Regression/DungeonExitReachableRegression.cs` | 550 | DungeonExitReachableRegression [dungeon-exit] - locks WO-770.1 (the roach-motel fix): the rich dungeon must have an ALWAYS-OPEN return exit (so a hero who can't or won... |
| 124 | `[dungeon-defeat-realtime]` | `Assets/Editor/Regression/DungeonRealtimeSettleRegression.cs` | 551 | DungeonRealtimeSettleRegression [dungeon-defeat-realtime] - locks WO-770.3b: the real-time BattleArena path (the DEFAULT, ff.dungeonrealtime ON) has NO scene round-tri... |
| 125 | `[dungeon-toast]` | `Assets/Editor/Regression/DungeonToastRegression.cs` | 552 | DungeonToastRegression [dungeon-toast] - locks WO-770.7 (fixes D13/D14): dungeon feedback that used to fire into the void must be surfaced. Asserts: 1. A code-built (E... |
| 126 | `[dungeon-fpv]` | `Assets/Editor/Regression/DungeonFpvRegression.cs` | 553 | DungeonFpvRegression [dungeon-fpv] - locks the DUNGEON CAMERA contract. |
| 127 | `[modal-registration]` | `Assets/Editor/Regression/ModalArbiterRegistrationRegression.cs` | 554 | ModalArbiterRegistrationRegression [modal-registration] -- proves top-band modals go through the PanelManager arbiter (back-button / battle-lock / one-modal). |
| 128 | `[founding-reach]` | `Assets/Editor/Regression/FoundingReachabilityRegression.cs` | 555 | FoundingReachabilityRegression [founding-reach] -- proves the founding choice is actually REACHABLE on a fresh save and correctly suppressed for a returning player. |
| 129 | `[ftue-honesty]` | `Assets/Editor/Regression/FtueHonestyRegression.cs` | 556 | FtueHonestyRegression [ftue-honesty] -- proves the founding tutorial teaches the truth (points at a real control; never teaches a fiction). |
| 130 | `[echo-card-copy]` | `Assets/Editor/Regression/EchoCardCopyRegression.cs` | 557 | EchoCardCopyRegression [echo-card-copy] -- proves the first-Echo card reads as an AWAKENING, not a nonsensical "Leveled Up to 1". |
| 131 | `[shader-pin]` | `Assets/Editor/Regression/ShaderPinRegression.cs` | 558 | ShaderPinRegression [shader-pin] -- proves the URP shaders survive a build (no pink/magenta materials in the player because a shader was stripped). |
| 132 | `[structure-burn]` | `Assets/Editor/Regression/StructureBurnRegression.cs` | 560 | StructureBurnRegression [structure-burn] - proves WO-761 fire lingers till repaired. |
| 133 | `[waves-schema]` | `Assets/Editor/Regression/WavesSchemaRegression.cs` | 562 | WavesSchemaRegression [waves-schema] -- closes audit P1 EW-3. |
| 134 | `[wave-authoring]` | `Assets/Editor/Regression/WaveAuthoringLiveRegression.cs` | 563 | WaveAuthoringLiveRegression [wave-authoring] -- are waves.json's authored enemies[] batches CONSUMED by the live spawn path, or silently discarded? |
| 135 | `[gear-levels]` | `Assets/Editor/Regression/GearLevelsRegression.cs` | 566 | GearLevelsRegression - WO-808 Option A data oracle: gear-levels.json integrity. |
| 136 | `[pack-cosmetic-integrity]` | `Assets/Editor/Regression/PackCosmeticIntegrityRegression.cs` | 567 | PackCosmeticIntegrityRegression [pack-cosmetic-integrity] -- the audit P1 ECON-1 integrity oracle: EVERY advertised pack cosmetic must be GRANTABLE (ECON-1). |
| 137 | `[cosmetic-apply]` | `Assets/Editor/Regression/CosmeticApplyRegression.cs` | 569 | CosmeticApplyRegression [cosmetic-apply] - an equipped cosmetic REACHES A RENDERER. |
| 138 | `[asset-roots]` | `Assets/Editor/Regression/AssetRootsRegression.cs` | 571 | AssetRootsRegression [asset-roots] - THE GATE ASSETROOTS.CS ALREADY CLAIMED IT HAD. |
| 139 | `[impulse-pack]` | `Assets/Editor/Regression/ImpulsePackRegression.cs` | 573 | ImpulsePackRegression [impulse-pack] -- WO-1037: single-resource impulse packs, legalised by the WO-947 section 12 amendment (the PURCHASE boundary is not the COST bou... |
| 140 | `[tower-wall-los]` | `Assets/Editor/Regression/TowerWallLosRegression.cs` | 574 | TowerWallLosRegression [tower-wall-los] - locks the "towers shoot through walls" fix (owner felt-test 2026-07). Two halves must both hold, or a tower fires straight th... |
| 141 | `[vfx-aura-diff]` | `Assets/Editor/Regression/VfxAuraDifferentiationRegression.cs` | 575 | VfxAuraDifferentiationRegression [vfx-aura-diff] - locks the owner's 2026-07-24 arcane-aura differentiation + the "Cathedral of Magic" rename + the archer perma-firewo... |
| 142 | `[tower-proj-map]` | `Assets/Editor/Regression/TowerProjectileMapRegression.cs` | 577 | TowerProjectileMapRegression - owner VfxManualPicks per-tier tower projectiles. |
| 143 | `[portal-rebuild]` | `Assets/Editor/Regression/PortalRebuildRegression.cs` | 579 | PortalRebuildRegression [portal-rebuild]  - WO-869 |
| 144 | `[realm-map]` | `Assets/Editor/Regression/RealmMapRegression.cs` | 581 | RealmMapRegression [realm-map] -- WO-826 dual-copy + loader oracle. |
| 145 | `[raid-deploy-ui]` | `Assets/Editor/Regression/RaidDeployUiRegression.cs` | 583 | RaidDeployUiRegression - WO-839 contract pins (Raid Deploy screen cleanup). |
| 146 | `[raid-deploy-zero-army]` | `Assets/Editor/Regression/RaidDeployZeroArmyRegression.cs` | 585 | RaidDeployZeroArmyRegression -- WO-1403 pins (Raid Deploy at zero troops). |
| 147 | `[wallet-provider]` | `Assets/Editor/Regression/WalletProviderSelectionRegression.cs` | 587 | WalletProviderSelectionRegression - WO-766 source-level oracle: real Solana wallet provider wiring (Seeker/Android identity+save connect). |
| 148 | `[play-packaging]` | `Assets/Editor/Regression/GooglePlayPackagingRegression.cs` | 590 | WO-1255 source oracle for the fail-closed Play AAB packaging chain. |
| 149 | `[audio-startup-bounded]` | `Assets/Editor/Regression/AudioStartupBoundedRegression.cs` | 591 | Release gate for the synchronous audio bootstrap black-screen class. |
| 150 | `[wallet-session]` | `Assets/Editor/Regression/WalletSessionPersistenceRegression.cs` | 593 | WalletSessionPersistenceRegression - the wallet SESSION survives a relaunch, and the capability grant that makes that possible is never leaked. |
| 151 | `[backend-save-auth]` | `Assets/Editor/Regression/BackendSaveAuthRegression.cs` | 595 | WO-1211: boot reads never sign; writes use the shared auth authority. |
| 152 | `[wallet-connect-attribution]` | `Assets/Editor/Regression/WalletConnectFailureAttributionRegression.cs` | 597 | WO-1420/WO-1441: a connect failure and a missing session must each name their real cause. |
| 153 | `[login-gate]` | `Assets/Editor/Regression/LoginGateRegression.cs` | 599 | LoginGateRegression [login-gate] -- the boot login surface must NEVER be shown to a player who is already in, and must ALWAYS be shown to one who is not. |
| 154 | `[pi-login-gate]` | `Assets/Editor/Regression/PiLoginGateRegression.cs` | 601 | PiLoginGateRegression [pi-login-gate] -- a player who is SIGNED IN WITH PI must never be shown the CHOOSE YOUR WALLET surface, and the SKR/Solana skin must be byte-for... |
| 155 | `[promo-redeem-entry]` | `Assets/Editor/Regression/PromoRedeemEntryRegression.cs` | 603 | PromoRedeemEntryRegression [promo-redeem-entry] - the promo-code DOOR. Marker: PROMO_REDEEM_ENTRY_OK / PROMO_REDEEM_ENTRY_FAIL. Expected: GREEN. |
| 156 | `[hud-actionbar]` | `Assets/Editor/Regression/HudActionBarRegression.cs` | 605 | HudActionBarRegression - headless oracle for the WO-835 action-bar applicability repack. Marker: HUD_ACTIONBAR_OK / HUD_ACTIONBAR_FAIL. |
| 157 | `[hud-label-fit]` | `Assets/Editor/Regression/HudLabelFitRegression.cs` | 611 | HudLabelFitRegression [hud-label-fit] (WO-1144) - a town HUD label can never again be CUT, ellipsised, or painted through the widget next to it. |
| 158 | `[hero-select-carousel]` | `Assets/Editor/Regression/HeroSelectCarouselRegression.cs` | 616 | HeroSelectCarouselRegression [hero-select-carousel] (WO-1248) Markers: HERO_SELECT_CAROUSEL_OK / HERO_SELECT_CAROUSEL_FAIL |
| 159 | `[raids-discoverability]` | `Assets/Editor/Regression/RaidsDiscoverabilityRegression.cs` | 618 | RaidsDiscoverabilityRegression - WO-1008 oracle: the Raids face is VISIBLE-and EXPLAINED the moment a Barracks exists, never ABSENT. Marker: RAIDS_DISCOVERABILITY_OK /... |
| 160 | `[echo-picker]` | `Assets/Editor/Regression/EchoResourcePickerRegression.cs` | 620 | EchoResourcePickerRegression - WO-830/831 oracle for the card-facing layer: the resource-picker VM projection, the picker verb, the disclosed synergy line (and its NON... |
| 161 | `[dungeon-room-ownership]` | `Assets/Editor/Regression/DungeonRoomOwnershipRegression.cs` | 622 | DungeonRoomOwnershipRegression [dungeon-rooms] (WO-797) - rooms OWN their enemies. |
| 162 | `[dungeon-kit]` | `Assets/Editor/Regression/DungeonKitRegression.cs` | 623 | DungeonKitRegression [dungeon-kit] -- WO-595 tracked 24-piece snap-kit contract. |
| 163 | `[quest-reach]` | `Assets/Editor/Regression/QuestCompletabilityRegression.cs` | 625 | QuestCompletabilityRegression [quest-reach]        WO-854 Phase 0, Silo R |
| 164 | `[dungeon-composed-pillars]` | `Assets/Editor/Regression/DungeonComposedPillarsRegression.cs` | 627 | DungeonComposedPillarsRegression - pins WO-1001 slices 1b through 8. |
| 165 | `[dungeon-multilevel]` | `Assets/Editor/Regression/DungeonMultiLevelRegression.cs` | 629 | DungeonMultiLevelRegression - pins the VERTICAL (multi-level) dungeon contract. |
| 166 | `[dungeon-egress]` | `Assets/Editor/Regression/DungeonEgressRegression.cs` | 631 | DungeonEgressRegression [dungeon-egress] - pins HOW MANY WAYS OUT a dungeon has, and WHERE the authored one is. |
| 167 | `[biome-roads]` | `Assets/Editor/Regression/BiomeRoadsRegression.cs` | 632 | BiomeRoadsRegression [biome-roads]   Marker: BIOME_ROADS_OK / BIOME_ROADS_FAIL |
| 168 | `[terrain-layer]` | `Assets/Editor/Regression/TerrainLayerRegression.cs` | 634 | TerrainLayerRegression [terrain-layer]   Marker: TERRAIN_LAYER_OK / TERRAIN_LAYER_FAIL |
| 169 | `[dungeon-treasure]` | `Assets/Editor/Regression/DungeonTreasureRegression.cs` | 636 | DungeonTreasureRegression [dungeon-treasure] (WO-850) - the deepest-room cache. |
| 170 | `[chest]` | `Assets/Editor/Regression/BreakableContainerChestRegression.cs` | 638 | BreakableContainerChestRegression [chest] (WO-1132) - the loot chest is OPENED, never attacked. |
| 171 | `[echo-card-layout]` | `Assets/Editor/Regression/EchoCardLayoutRegression.cs` | 640 | EchoCardLayoutRegression [echo-card-layout] (WO-852) - the Echo card's picker can never go back to fraction bands / sub-touch-floor chips. |
| 172 | `[rumor-board-layout]` | `Assets/Editor/Regression/RumorBoardLayoutRegression.cs` | 642 | RumorBoardLayoutRegression [rumor-board-layout] - Brom's rumor board (WO-1192 v3) can never re-grow a second region, shrink a band under its line box, or place two aut... |
| 173 | `[buildmenu-layout]` | `Assets/Editor/Regression/BuildMenuLayoutRegression.cs` | 644 | BuildMenuLayoutRegression [buildmenu-layout] (WO-878) - the build menu can never stack a control on top of another one again. |
| 174 | `[tower-manager]` | `Assets/Editor/Regression/TowerManagerRegression.cs` | 646 | TowerManagerRegression [tower-manager] (WO-880) - the Tower Manager can never again print a fabricated "rng 0, dmg 0", nor cut a row in half. |
| 175 | `[help-menu-entry]` | `Assets/Editor/Regression/HelpMenuEntryRegression.cs` | 648 | HelpMenuEntryRegression [help-menu-entry] (WO-882) - the Help menu can never ship a blank, label-less button again. |
| 176 | `[starter-loadout]` | `Assets/Editor/Regression/StarterLoadoutRegression.cs` | 650 | StarterLoadoutRegression [starter-loadout] (WO-860 + WO-861 Phase 0) |
| 177 | `[shield-defense]` | `Assets/Editor/Regression/ShieldDefenseRegression.cs` | 652 | ShieldDefenseRegression [shield-defense] |
| 178 | `[shield-load-restore]` | `Assets/Editor/Regression/ShieldLoadRestoreRegression.cs` | 653 | Pins the load-time seam that restores a persisted shield onto a bare Knight body. |
| 179 | `[jeweler-discovery-ftue]` | `Assets/Editor/Regression/JewelerDiscoveryFtueRegression.cs` | 654 | no header |
| 180 | `[tower-empower-reach]` | `Assets/Editor/Regression/TowerEmpowermentReachabilityRegression.cs` | 656 | Source-lint + asset-lint: resolves whether <c>Tower.TryEmpower()</c> is reachable from a shipping player surface. Returns true (summary) / false (detail); never throws. |
| 181 | `[modifier-key-coverage]` | `Assets/Editor/Regression/ModifierKeyCoverageRegression.cs` | 661 | ModifierKeyCoverageRegression [modifier-key-coverage] |
| 182 | `[hub-foliage]` | `Assets/Editor/Regression/HubFoliageRegression.cs` | 662 | HubFoliageRegression [hub-foliage] -- proves the runtime hub-foliage scatter is SAFE and DETERMINISTIC without opening a scene or running the game. |
| 183 | `[glossary]` | `Assets/Editor/Regression/GlossaryRegression.cs` | 663 | GlossaryRegression [glossary] |
| 184 | `[item-identity]` | `Assets/Editor/Regression/ItemIdentityRegression.cs` | 664 | ItemIdentityRegression [item-identity] |
| 185 | `[drop-mote]` | `Assets/Editor/Regression/ItemDropMoteIdentityRegression.cs` | 665 | ItemDropMoteIdentityRegression [drop-mote] |
| 186 | `[enemy-pool-reset]` | `Assets/Editor/Regression/EnemyPoolResetRegression.cs` | 670 | EnemyPoolResetRegression [enemy-pool-reset] |
| 187 | `[tutorial-reach]` | `Assets/Editor/Regression/TutorialStepReachabilityRegression.cs` | 671 | TutorialStepReachabilityRegression [tutorial-reach] |
| 188 | `[runtime-spawn-visual]` | `Assets/Editor/Regression/RuntimeSpawnVisualRegression.cs` | 672 | RuntimeSpawnVisualRegression [runtime-spawn-visual] |
| 189 | `[wallet-identity]` | `Assets/Editor/Regression/WalletIdentityRegression.cs` | 673 | WalletIdentityRegression [wallet-identity] |
| 190 | `[loot-class-gate]` | `Assets/Editor/Regression/LootClassGateRegression.cs` | 674 | LootClassGateRegression [loot-class-gate] |
| 191 | `[shader-predicate-authority]` | `Assets/Editor/Regression/ShaderPredicateSingleAuthorityRegression.cs` | 675 | ShaderPredicateSingleAuthorityRegression [shader-predicate-authority] |
| 192 | `[dynamic-difficulty]` | `Assets/Editor/Regression/DynamicDifficultyRegression.cs` | 677 | DynamicDifficultyRegression [dynamic-difficulty] |
| 193 | `[raid-arena-shape]` | `Assets/Editor/Regression/RaidArenaShapeRegression.cs` | 679 | RaidArenaShapeRegression [raid-arena-shape]   Marker: RAID_ARENA_SHAPE_OK / _FAIL |
| 194 | `[reset-full-clear]` | `Assets/Editor/Regression/ResetToNewGameFullClearRegression.cs` | 680 | ResetToNewGameFullClearRegression [reset-full-clear] |
| 195 | `[newgame-pref-sweep]` | `Assets/Editor/Regression/NewGamePrefStoreSweepRegression.cs` | 684 | NewGamePrefStoreSweepRegression [newgame-pref-sweep] |
| 196 | `[harvest-result-copy]` | `Assets/Editor/Regression/HarvestResultCopyRegression.cs` | 686 | HarvestResultCopyRegression [harvest-result-copy] |
| 197 | `[worldhold-liveness]` | `Assets/Editor/Regression/WorldHoldLivenessRegression.cs` | 692 | WO-1369: a PlayerOwned WorldHold must declare a liveness probe, and the watchdog must force-release the hold the moment that probe answers false - never because of age. |
| 198 | `[gameover-lifecycle]` | `Assets/Editor/Regression/GameOverScreenLifecycleRegression.cs` | 694 | WO-1369: the hub defeat screen unsubscribes, re-checks its scene, and binds its world hold to the object that can actually die. |
| 199 | `[endstate-body-fit]` | `Assets/Editor/Regression/EndStateBodyFitRegression.cs` | 696 | WO-952: no shipped end-state may compress its body bands below their own content size. The absence of the `COMPRESSED to fit` condition IS the acceptance signal. |
| 200 | `[cathedral-cumulative]` | `Assets/Editor/Regression/CathedralCumulativeRegression.cs` | 697 | CathedralCumulativeRegression [cathedral-cumulative] |
| 201 | `[hero-equip-hub]` | `Assets/Editor/Regression/HeroEquipHudHubRegression.cs` | 698 | HeroEquipHudHubRegression [hero-equip-hub] |
| 202 | `[armed-hero]` | `Assets/Editor/Regression/ArmedHeroInvariantRegression.cs` | 701 | ArmedHeroInvariantRegression [armed-hero] / [shield-improvement] / [defense-cap] |
| 203 | `[buildmenu-economy]` | `Assets/Editor/Regression/BuildMenuRealEconomyRegression.cs` | 702 | BuildMenuRealEconomyRegression [buildmenu-economy] |
| 204 | `[hero-death-pin]` | `Assets/Editor/Regression/HeroDeathPinRebaseRegression.cs` | 703 | HeroDeathPinRebaseRegression [hero-death-pin] - locks the F8 2026-08-10 fix (seq 2253/2254/2255, "when the player dies, he shakes then dies"): |
| 205 | `[wall-build-l1]` | `Assets/Editor/Regression/WallBuildL1Regression.cs` | 704 | WallBuildL1Regression [wall-build-l1] -- WO-948: walls BUILD at level 1 ONLY. |
| 206 | `[synty-perimeter-grounding]` | `Assets/Editor/Regression/SyntyPerimeterGroundingRegression.cs` | 705 | Prevents the merged-world perimeter from returning to the retired +3m island seat. |
| 207 | `[castle-plans]` | `Assets/Editor/Regression/CastlePlansUnlockRegression.cs` | 706 | CastlePlansUnlockRegression [castle-plans] -- WO-1013 guardrails for the Castle Defense Plans drop / Arcane Spire visible-lock. |
| 208 | `[castle-plans-seat]` | `Assets/Editor/Regression/CastlePlansSeatRegression.cs` | 710 | CastlePlansSeatRegression [castle-plans-seat] -- WO-1105 guardrails for WHERE the Castle Defense Plans drop is seated. |
| 209 | `[live-class-bow-afford]` | `Assets/Editor/Regression/LiveClassBowAndAffordSeverityRegression.cs` | 716 | LiveClassBowAndAffordSeverityRegression [live-class-bow-afford] |
| 210 | `[structure-targetable]` | `Assets/Editor/Regression/StructureTargetableRegression.cs` | 722 | StructureTargetableRegression [structure-targetable] |
| 211 | `[under-construction-gate]` | `Assets/Editor/Regression/UnderConstructionGateRegression.cs` | 732 | UnderConstructionGateRegression [under-construction-gate] -- a structure with an in-flight build job MUST NOT fight. |
| 212 | `[collector-income]` | `Assets/Editor/Regression/CollectorIncomeRegression.cs` | 740 | CollectorIncomeRegression [collector-income] |
| 213 | `[town-bank-cap]` | `Assets/Editor/Regression/TownBankCapRegression.cs` | 750 | TownBankCapRegression -- the permission gate for the TOWN BANK CAP (WO-857 / WO-901 Phase F). ARCHITECTURE_PRINCIPLES Sec.2c: this suite is what makes putting an upper... |
| 214 | `[retired-vocabulary]` | `Assets/Editor/Regression/RetiredVocabularyRegression.cs` | 761 | RetiredVocabularyRegression [retired-vocabulary] Player-visible vocabulary only. Frozen persistence/wire identifiers are excluded. DataRegression.cs registration is co... |
| 215 | `[clan-feature-gate]` | `Assets/Editor/Regression/ClanFeatureGateRegression.cs` | 766 | WO-1265: the shipped clan/chat implementation is a local PlayerPrefs prototype. Until a signed-wallet backend, moderation and two-wallet proof exist, neither player en... |
| 216 | `[harvest-trim-warn]` | `Assets/Editor/Regression/WO1207HarvestTrimWarnRegression.cs` | 774 | WO-1207: the harvest trim is TOLD, the battle reward is SILENT. |
| 217 | `[over-cap-income]` | `Assets/Editor/Regression/WO1191OverCapIncomeRegression.cs` | 785 | WO1191OverCapIncomeRegression -- the MEASURED oracle for income above the cap. |
| 218 | `[econ-sweep]` | `Assets/Editor/Regression/EconomySweepRegression.cs` | 796 | The ECON-SWEEP 2026-08-16 covenant suite. Never throws; returns a one-line reason. |
| 219 | `[ui-capture-coverage]` | `Assets/Editor/Regression/UiCaptureCoverageRegression.cs` | 807 | UiCaptureCoverageRegression [ui-capture-coverage] |
| 220 | `[skills-panel-layout]` | `Assets/Editor/Regression/SkillsPanelLayoutRegression.cs` | 821 | SkillsPanelLayoutRegression [skills-panel-layout] (WO-865) - the Grom (Knight) Skills panel can never go back to fraction bands, an unclipped grid, or a label that get... |
| 221 | `[dock-layout]` | `Assets/Editor/Regression/HudDockLayoutRegression.cs` | 830 | HudDockLayoutRegression [dock-layout] (WO-1319) - the bottom action dock can never again print its face captions as one overlapping run. |
| 222 | `[talent-focus]` | `Assets/Editor/Regression/TalentFocusSingletonRegression.cs` | 837 | TalentFocusSingletonRegression [talent-focus] (WO-1021 sec 2.1d) - the talent board can never again grow ONE OVERSIZED GOLD PLATE PER TRACK. |
| 223 | `[session-shape]` | `Assets/Editor/Regression/SessionShapeRegression.cs` | 848 | SessionShapeRegression - WO-1027: the ache is carried by SHAPE and NUMBER, and never by hue. |
| 224 | `[dungeon-status]` | `Assets/Editor/Regression/DungeonStatusRegression.cs` | 849 | DungeonStatusRegression [dungeon-status] |
| 225 | `[collector-tell]` | `Assets/Editor/Regression/CollectorTellRegression.cs` | 858 | CollectorTellRegression - WO-900: the collector "I am full" tell, both halves. |
| 226 | `[talent-tree-shape]` | `Assets/Editor/Regression/TalentTreeShapeRegression.cs` | 870 | TalentTreeShapeRegression [talent-tree-shape] - the owner's SHAPE LAW for every talent tree, common and specialty alike (owner ruling 2026-08-16). |
| 227 | `[numeral-legibility]` | `Assets/Editor/Regression/NumeralLegibilityRegression.cs` | 877 | NumeralLegibilityRegression [numeral-legibility] - no UI font may draw the numeral 1 as a bare vertical stroke. |
| 228 | `[daily-quest-empty]` | `Assets/Editor/Regression/DailyQuestEmptyStateRegression.cs` | 880 | DailyQuestEmptyStateRegression [daily-quest-empty] (WO-879) - the daily-quest empty state is ONE fact, owned by the ViewModel, rendered ONCE by the View. |
| 229 | `[vfx-loop-flag]` | `Assets/Editor/Regression/VfxLoopFlagRegression.cs` | 891 | VfxLoopFlagRegression [vfx-loop-flag] -- the oracle that stops a BURST prefab from being catalogued as a LOOP, and the single home of the loop-vs-burst derivation the ... |
| 230 | `[elite-vfx-wire]` | `Assets/Editor/Regression/EliteVfxWiringRegression.cs` | 901 | EliteVfxWiringRegression [elite-vfx-wire] - the oracle that stops WO-874's ruling from being routed around a SECOND time. |
| 231 | `[surface-impact-vfx]` | `Assets/Editor/Regression/SurfaceImpactVfxRegression.cs` | 911 | SurfaceImpactVfxRegression [surface-impact-vfx] - WO-887's surface half, pinned at the three places it can silently come undone. |
| 232 | `[ftue-pointer-vfx]` | `Assets/Editor/Regression/FtuePointerVfxRegression.cs` | 918 | FtuePointerVfxRegression - WO-1344: the FTUE "where to go" pointer is HER tag, and it cannot swallow a tap. |
| 233 | `[aoe-reticle-radius]` | `Assets/Editor/Regression/AoeReticleRadiusRegression.cs` | 927 | WO-1345: pins the AoE reticle's owner-tagged key -> prefab mapping and pins that its ring size derives from the ability's own radius data rather than a constant. |
| 234 | `[owner-aura-chest]` | `Assets/Editor/Regression/OwnerTaggedAuraChestWiringRegression.cs` | 935 | OwnerTaggedAuraChestWiringRegression [owner-aura-chest] - WO-1346 + WO-1347. |
| 235 | `[vfx-self-contained]` | `Assets/Editor/Regression/VfxResourceSelfContainmentRegression.cs` | 949 | VfxResourceSelfContainmentRegression [vfx-self-contained] |
| 236 | `[vfx-null-slot]` | `Assets/Editor/Regression/VfxParticleNullSlotRegression.cs` | 961 | VfxParticleNullSlotRegression [vfx-null-slot] -- the oracle that stops a catalogued VFX prefab with a NULL-material renderer from reaching the owner's F8 queue as a Ma... |
| 237 | `[enemy-rig-coherence]` | `Assets/Editor/Regression/EnemyRigControllerCoherenceRegression.cs` | 969 | EnemyRigControllerCoherenceRegression - every enemy mesh must be paired with a controller whose CLIP TYPE its rig can actually play. |
| 238 | `[build-card-art]` | `Assets/Editor/Regression/BuildCardArtRegression.cs` | 976 | BuildCardArtRegression - every build card the player can see resolves to REAL art, or is recorded as known debt. Nothing silently degrades to a letter. |
| 239 | `[dungeon-encounter-family]` | `Assets/Editor/Regression/DungeonEncounterFamilyRegression.cs` | 986 | DungeonEncounterFamilyRegression (WO-1001 Phase 1, slice 2) - the per-encounter ENEMY FAMILY contract for composed dungeons. |
| 240 | `[townsfolk-bodies]` | `Assets/Editor/Regression/TownsfolkBodyPoolRegression.cs` | 996 | TownsfolkBodyPoolRegression [townsfolk-bodies] - pins the castle-hub townsfolk BODY contract end to end: pool -> prefab -> renderer -> material -> texture. |
| 241 | `[collector-ladder]` | `Assets/Editor/Regression/CollectorLadderRegression.cs` | 1006 | CollectorLadderRegression - a placed COLLECTOR's upgrade ladder lives on a DIFFERENT row than the collector, and that row can be hidden from the palette. Deleting it m... |
| 242 | `[palette-groups]` | `Assets/Editor/Regression/BuildPaletteGroupsRegression.cs` | 1014 | BuildPaletteGroupsRegression - WO-1167: the build palette groups itself by ROLE, so a new building needs DATA and not code. This oracle pins the rule. |
| 243 | `[barracks-blanktown]` | `Assets/Editor/Regression/BarracksBlankTownRegression.cs` | 1021 | BarracksBlankTownRegression - WO-950 oracle: the drillmaster + once-teach + the phantom footprint on a BLANK-TOWN save (owner felt-report 2026-08-10). |
| 244 | `[echo-hollow-route]` | `Assets/Editor/Regression/EchoHollowRouteRegression.cs` | 1022 | EchoHollowRouteRegression [echo-hollow-route] -- WO-951: proves the Echo Hollow (pet-house) interact route opens the EXISTING Echo roster popup. |
| 245 | `[harvest-drip]` | `Assets/Editor/Regression/HarvestDripRegression.cs` | 1023 | HarvestDripRegression - WO-953 oracle for the harvest drip-feedback lane. |
| 246 | `[hostile-green]` | `Assets/Editor/Regression/HostileGreenCueRegression.cs` | 1024 | HostileGreenCueRegression (WO-956) - proves the faction colour law: ENEMY-side presentation never sits on the green axis (owner is red/green colourblind; green is the ... |
| 247 | `[aggro-leash]` | `Assets/Editor/Regression/AggroLeashRegression.cs` | 1025 | AggroLeashRegression - the BAIT ALLOWANCE oracle (owner live-play 2026-08-16: "i was trying to target and bait an enemy out and i think we need to allow aggro targets ... |
| 248 | `[dungeon-cam-958]` | `Assets/Editor/Regression/DungeonCameraTightRoomRegression.cs` | 1026 | DungeonCameraTightRoomRegression [dungeon-cam-958] - locks the WO-958 contract. |
| 249 | `[camera-wall-occlusion]` | `Assets/Editor/Regression/CameraWallOcclusionRegression.cs` | 1027 | WO-1289: walls stay visible and the camera always seats on their near side. |
| 250 | `[manage-navigation]` | `Assets/Editor/Regression/ManageNavigationRegression.cs` | 1033 | WO-2001 - the Manage screen graph, its back stack, and the retired launcher. |
| 251 | `[manage-queue-drawer]` | `Assets/Editor/Regression/ManageQueueDrawerRegression.cs` | 1034 | F8 2026-08-31: tower browsing leads; queue administration is opt-in. WO-1368: opt-in means BEHIND the QUEUE affordance, never NOWHERE. |
| 252 | `[manage-approved-launcher]` | `Assets/Editor/Regression/ManageApprovedLauncherRegression.cs` | 1035 | Source oracle for the approved 2026-08-31 four-card Manage launcher. |
| 253 | `[manage-buildings-card]` | `Assets/Editor/Regression/ManageBuildingsCardRegression.cs` | 1037 | Headless contract for WO-1418's compact Buildings destination. |
| 254 | `[manage-defense-card]` | `Assets/Editor/Regression/ManageDefenseCardRegression.cs` | 1039 | Headless contract for WO-1422's compact Defense destination. |
| 255 | `[manage-research-card]` | `Assets/Editor/Regression/ManageResearchCardRegression.cs` | 1040 | Headless contract for WO-1422's compact Research destination. |
| 256 | `[progression-reachability]` | `Assets/Editor/Regression/ProgressionReachabilityRegression.cs` | 1042 | ProgressionReachabilityRegression [progression-reachability] -- WO-1423. |
| 257 | `[heart-surface]` | `Assets/Editor/Regression/HeartSurfaceRegression.cs` | 1044 | HeartSurfaceRegression [heart-surface] -- WO-2003 / WO-2017. |
| 258 | `[troop-reachability]` | `Assets/Editor/Regression/TroopReachabilityRegression.cs` | 1046 | TroopReachabilityRegression [troop-reachability] -- WO-2011 / owner ruling 21. |
| 259 | `[manage-state-model]` | `Assets/Editor/Regression/ManageStateModelRegression.cs` | 1047 | ManageStateModelRegression [manage-state-model] -- WO-2011. |
| 260 | `[panel-door]` | `Assets/Editor/Regression/PanelDoorRegression.cs` | 1053 | Source oracle: no panel-like MonoBehaviour may exist without a door. |
| 261 | `[authored-field-reader]` | `Assets/Editor/Regression/AuthoredFieldReaderRegression.cs` | 1054 | Seam oracle: an authored field with no production reader is an unkept promise. |
| 262 | `[placed-door]` | `Assets/Editor/Regression/PlacedStructureDoorRegression.cs` | 1058 | Source oracle: the move/upgrade/sell controls for an ALREADY-PLACED structure must be reachable through a signposted door, not only through the undocumented tap-a-piec... |
| 263 | `[honest-feedback-grant]` | `Assets/Editor/Regression/HonestFeedbackGrantRegression.cs` | 1060 | Proves the WO-1432 thank-you lands in full against a near-cap bank. |
| 264 | `[honest-feedback-once]` | `Assets/Editor/Regression/HonestFeedbackClaimOnceRegression.cs` | 1061 | Proves a second thank-you claim moves nothing and says so out loud. |
| 265 | `[primary-fallback]` | `Assets/Editor/Regression/PrimaryFallbackRegression.cs` | 1063 | PrimaryFallbackRegression - WO-1429: THE HERO ALWAYS HAS A VERB Markers: PRIMARY_FALLBACK_OK / PRIMARY_FALLBACK_FAIL |
| 266 | `[manage-dumb-view]` | `Assets/Editor/Regression/ManageDumbViewRegression.cs` | 1065 | Source oracle: the common Manage renderer may hold no game rules. |
| 267 | `[manage-queue-panel8]` | `Assets/Editor/Regression/ManageQueuePanel8Regression.cs` | 1067 | Mockup panel 8 (QUEUE overlay), model side: channel tabs with live slot counts, numbered rows, and SPEED UP bound to the ONE existing paid-finish path. |
| 268 | `[manage-one-heading]` | `Assets/Editor/Regression/ManageOneHeadingRegression.cs` | 1069 | Source oracle: a Manage screen renders its heading exactly once. |
| 269 | `[collector-overflow]` | `Assets/Editor/Regression/CollectorOverflowRegression.cs` | 1071 | Ruling 26b: a full collector spills into its matching storage, and nothing burns. |
| 270 | `[public-navigation-retirement]` | `Assets/Editor/Regression/PublicNavigationRetirementRegression.cs` | 1072 | RE-POINTED 2026-09-06 (WO-1421) - STRICTER, NEVER DELETED. <para> CURRENT RULING (owner 2026-09-06, verbatim): "under journey, please remove dungeons season in realm m... |
| 271 | `[journey-deck-subtitle]` | `Assets/Editor/Regression/JourneyDeckSubtitleRegression.cs` | 1074 | WO-1404: Journey subtitles carry actionable state and fit one line. |
| 272 | `[journey-deck-two-card]` | `Assets/Editor/Regression/JourneyDeckTwoCardRegression.cs` | 1076 | WO-1421 (owner ruling 2026-09-06, verbatim): "under journey, please remove dungeons season in realm map as they should not be displayed there right now we don't have a... |
| 273 | `[copy-hygiene]` | `Assets/Editor/Regression/CopyHygieneRegression.cs` | 1078 | WO-1413: source-level guard for truthful player copy and capture fixtures. |
| 274 | `[pause-medieval-skin]` | `Assets/Editor/Regression/PauseMedievalSkinRegression.cs` | 1079 | Locks the approved compact Pause composition and shared skin seam. |
| 275 | `[combat-item-picker]` | `Assets/Editor/Regression/CombatItemPickerRegression.cs` | 1080 | Locks the adaptive combat HUD's single, paused, authoritative Item flow. |
| 276 | `[settings-medieval-skin]` | `Assets/Editor/Regression/SettingsMedievalSkinRegression.cs` | 1081 | Locks the approved medieval Settings shell without changing live persistence. |
| 277 | `[gear-aura-carry]` | `Assets/Editor/Regression/GearAuraCarryGateRegression.cs` | 1082 | GearAuraCarryGateRegression [gear-aura-carry] |
| 278 | `[armor-store-window]` | `Assets/Editor/Regression/ArmorStoreLockedWindowRegression.cs` | 1083 | ArmorStoreLockedWindowRegression [armor-store-window] (WO-960) |
| 279 | `[tutorial-anchor-latch]` | `Assets/Editor/Regression/TutorialAnchorLatchRegression.cs` | 1088 | TutorialAnchorLatchRegression [tutorial-anchor-latch] |
| 280 | `[tutorial-watchdog-bound]` | `Assets/Editor/Regression/TutorialWatchdogBoundRegression.cs` | 1089 | TutorialWatchdogBoundRegression [tutorial-watchdog-bound] |
| 281 | `[tutorial-completion-publisher]` | `Assets/Editor/Regression/TutorialCompletionPublisherRegression.cs` | 1096 | TutorialCompletionPublisherRegression [tutorial-completion-publisher] |
| 282 | `[build-carousel-order]` | `Assets/Editor/Regression/BuildCarouselTutorialOrderRegression.cs` | 1097 | BuildCarouselTutorialOrderRegression [build-carousel-order] |
| 283 | `[build-first-use-guide]` | `Assets/Editor/Regression/BuildFirstUseGuideRegression.cs` | 1098 | Confirmation out of order must not persist completion. |
| 284 | `[founding-guide-wolf]` | `Assets/Editor/Regression/FoundingGuideWolfBodyRegression.cs` | 1099 | FoundingGuideWolfBodyRegression [founding-guide-wolf] |
| 285 | `[hub-tree-aura]` | `Assets/Editor/Regression/HubTreeAuraWithholdRegression.cs` | 1100 | HubTreeAuraWithholdRegression [hub-tree-aura] |
| 286 | `[hud-class-fallback]` | `Assets/Editor/Regression/HudHeroClassFallbackRegression.cs` | 1101 | HudHeroClassFallbackRegression [hud-class-fallback] |
| 287 | `[tutorial-guide-identity]` | `Assets/Editor/Regression/TutorialGuideIdentityRegression.cs` | 1102 | TutorialGuideIdentityRegression [tutorial-guide-identity] |
| 288 | `[endstate-handoff]` | `Assets/Editor/Regression/EndStateTransitionHandoffRegression.cs` | 1103 | EndStateTransitionHandoffRegression [endstate-handoff] |
| 289 | `[wave-modal-safety]` | `Assets/Editor/Regression/WaveModalSafetyRegression.cs` | 1104 | WaveModalSafetyRegression [wave-modal-safety] Pins the device-proven contract: an active village siege cannot remain hidden behind an ordinary full-screen panel, while... |
| 290 | `[town-suspend-floor]` | `Assets/Editor/Regression/TownSuspendSceneFloorRegression.cs` | 1105 | TownSuspendSceneFloorRegression [town-suspend-floor] |
| 291 | `[equipment-screen-layout]` | `Assets/Editor/Regression/EquipmentScreenLayoutRegression.cs` | 1106 | EquipmentScreenLayoutRegression [equipment-screen-layout] (WO-1015) |
| 292 | `[inventory-armory-rail]` | `Assets/Editor/Regression/InventoryArmoryRailRegression.cs` | 1113 | InventoryArmoryRailRegression - WO-1133 "The Armory Rail" is what it says it is. |
| 293 | `[hero-preview-framing]` | `Assets/Editor/Regression/HeroPreviewFramingRegression.cs` | 1119 | HeroPreviewFramingRegression - WO-1059. The hero preview must frame the MODEL. |
| 294 | `[dungeon-mover-ownership]` | `Assets/Editor/Regression/DungeonMoverOwnershipRegression.cs` | 1120 | DungeonMoverOwnershipRegression [dungeon-mover-ownership] |
| 295 | `[hero-bar-rebind]` | `Assets/Editor/Regression/HeroBarClassRebindRegression.cs` | 1121 | HeroBarClassRebindRegression [hero-bar-rebind] |
| 296 | `[mage-spell-kit]` | `Assets/Editor/Regression/MageSpellKitAuthoringRegression.cs` | 1122 | MageSpellKitAuthoringRegression [mage-spell-kit] |
| 297 | `[guide-lead-move]` | `Assets/Editor/Regression/GuideLeadMovementRegression.cs` | 1123 | GuideLeadMovementRegression [guide-lead-move] |
| 298 | `[guide-lead-route]` | `Assets/Editor/Regression/GuideLeadRoutingRegression.cs` | 1131 | GuideLeadRoutingRegression [guide-lead-route] |
| 299 | `[town-movement-floor]` | `Assets/Editor/Regression/TownMovementFloorRegression.cs` | 1132 | TownMovementFloorRegression [town-movement-floor] |
| 300 | `[one-guide-body]` | `Assets/Editor/Regression/OneGuideBodyRegression.cs` | 1133 | OneGuideBodyRegression [one-guide-body] |
| 301 | `[wall-adjacency]` | `Assets/Editor/Regression/WallAdjacencyRegression.cs` | 1134 | WallAdjacencyRegression [wall-adjacency] |
| 302 | `[ranged-primary]` | `Assets/Editor/Regression/RangedPrimaryRegression.cs` | 1139 | RangedPrimaryRegression - pins WO-1105 (Sylas plays as an ARCHER). |
| 303 | `[ranged-facing]` | `Assets/Editor/Regression/RangedFacingLockRegression.cs` | 1140 | RangedFacingLockRegression - pins WO-1105 R3/R4 (owner rulings 2026-08-16). |
| 304 | `[mage-ability-icons]` | `Assets/Editor/Regression/MageAbilityIconRegression.cs` | 1144 | MageAbilityIconRegression - pins that NO mage ability medallion renders KNIGHT iconography, by RESOLVING each mage ability through the REAL resolution order. |
| 305 | `[knight-heal-icon]` | `Assets/Editor/Regression/KnightHealIconRegression.cs` | 1145 | Pins the phone action-bar contract for the Knight's authoritative heal. |
| 306 | `[hero-kit-mirror]` | `Assets/Editor/Regression/HeroKitMirrorRegression.cs` | 1150 | HeroKitMirrorRegression - pins the hero-select CARD KIT to the SHIPPED KIT. |
| 307 | `[spire-celebration]` | `Assets/Editor/Regression/SpirePlansCelebrationRegression.cs` | 1156 | SpirePlansCelebrationRegression [spire-celebration] -- WO-1104 SS3+SS4 guardrails for the Arcane Spire plans MOMENT (the celebration + call-to-arms screen). regression... |
| 308 | `[banned-vfx]` | `Assets/Editor/Regression/BannedVfxRegression.cs` | 1166 | BannedVfxRegression [banned-vfx] |
| 309 | `[class-resource]` | `Assets/Editor/Regression/ClassResourceRegression.cs` | 1170 | ClassResourceRegression - pins WO-997 (the class resource economy). |
| 310 | `[mana-spend]` | `Assets/Editor/Regression/ManaSpendRegression.cs` | 1176 | ManaSpendRegression - pins the SPEND half of the class resource economy. |
| 311 | `[wanderer-bubble]` | `Assets/Editor/Regression/WandererBubbleLegibilityRegression.cs` | 1180 | WandererBubbleLegibilityRegression - pins WO-973 (Bryn's speech bubble was a giant world-space card covering ~60 % of the frame with the line cut off). |
| 312 | `[cost-basket]` | `Assets/Editor/Regression/CostBasketSeparationRegression.cs` | 1189 | CostBasketSeparationRegression [cost-basket] -- WO-947: cost baskets separate by the structure's NATURE. |
| 313 | `[sink-cap]` | `Assets/Editor/Regression/EconomySinkCapRegression.cs` | 1201 | EconomySinkCapRegression [sink-cap] -- NO AUTHORED COST MAY EXCEED THE MAXIMUM BANKABLE AMOUNT OF THAT RESOURCE. |
| 314 | `[vfx-pool-shape]` | `Assets/Editor/Regression/VfxPoolShapeRegression.cs` | 1210 | VfxPoolShapeRegression [vfx-pool-shape] -- the oracle for WO-955: a VFX free list may never hand back a DESTROYED host, and may never accept one. |
| 315 | `[talent-icons]` | `Assets/Editor/Regression/TalentIconMapRegression.cs` | 1219 | TalentIconMapRegression [talent-icons] -- pins the talent icon map's integrity (WO-1023). Until 2026-08-15 NOTHING guarded talent-icon-map.json: every property (83/83 ... |
| 316 | `[concept-icons]` | `Assets/Editor/Regression/ConceptIconIdentityRegression.cs` | 1227 | ConceptIconIdentityRegression [concept-icons] -- pins the WO-1294 ONE-ICON IDENTITY CONTRACT: an assignable skill shows the SAME picture on its talent-tree node, in th... |
| 317 | `[shipped-surface-gate]` | `Assets/Editor/Regression/ShippedSurfaceGateRegression.cs` | 1241 | ShippedSurfaceGateRegression [shipped-surface-gate] |
| 318 | `[hero-death-severity]` | `Assets/Editor/Regression/HeroDeathSeverityRegression.cs` | 1253 | HeroDeathSeverityRegression (audit 2026-08-15) - a NORMAL hero death must not raise an F8 ERROR. Source-structural, headless, milliseconds, no play mode. |
| 319 | `[dev-grant-uncapped]` | `Assets/Editor/Regression/DevGrantUncappedRegression.cs` | 1260 | DevGrantUncappedRegression (audit 2026-08-15) - a DEV resource grant must land in FULL. Source-structural, headless, milliseconds, no play mode. |
| 320 | `[echo-engage-dialogue]` | `Assets/Editor/Regression/EchoEngageDialogueRegression.cs` | 1266 | EchoEngageDialogueRegression [echo-engage-dialogue] - INVERTED by WO-1031. |
| 321 | `[attachment-offset]` | `Assets/Editor/Regression/AttachmentOffsetRegression.cs` | 1273 | AttachmentOffsetRegression [attachment-offset] |
| 322 | `[store-mesh-readable]` | `Assets/Editor/Regression/StoreAssetMeshReadabilityRegression.cs` | 1280 | StoreAssetMeshReadabilityRegression [store-mesh-readable] |
| 323 | `[gear-prop-renders]` | `Assets/Editor/Regression/GearPropRendersRegression.cs` | 1291 | GearPropRendersRegression [gear-prop-renders] |
| 324 | `[hero-remote-content]` | `Assets/Editor/Regression/HeroRemoteContentRegression.cs` | 1303 | HeroRemoteContentRegression [hero-remote-content] - WO-1187 / hero art to R2. |
| 325 | `[echo-world-presence]` | `Assets/Editor/Regression/EchoWorldPresenceRegression.cs` | 1312 | EchoWorldPresenceRegression [echo-world-presence] - WO-1108 Lane B. |
| 326 | `[echo-guide-memories]` | `Assets/Editor/Regression/EchoGuideMemoryRegression.cs` | 1323 | EchoGuideMemoryRegression [echo-guide-memories] -- WO-1380. |
| 327 | `[equip-drawer-contents]` | `Assets/Editor/Regression/EquipDrawerContentsRegression.cs` | 1331 | EquipDrawerContentsRegression - WO-1061. The equip drawer listed NOTHING, and you could not change your weapon. |
| 328 | `[raid-exit-parity]` | `Assets/Editor/Regression/RaidExitParityRegression.cs` | 1337 | RaidExitParityRegression [raid-exit-parity]  --  markers RAID_EXIT_PARITY_OK / _FAIL |
| 329 | `[raid-terminal-state]` | `Assets/Editor/Regression/RaidTerminalStateRegression.cs` | 1348 | RaidTerminalStateRegression [raid-terminal-state] markers RAID_TERMINAL_STATE_OK / RAID_TERMINAL_STATE_FAIL |
| 330 | `[troop-target-preference]` | `Assets/Editor/Regression/TroopTargetPreferenceRegression.cs` | 1360 | TroopTargetPreferenceRegression [troop-target-preference] - WO-1438. |
| 331 | `[collector-props]` | `Assets/Editor/Regression/CollectorStackPropCatalogRegression.cs` | 1374 | CollectorStackPropCatalogRegression [collector-props] |
| 332 | `[placed-upgrade-page]` | `Assets/Editor/Regression/PlacedUpgradePageTruthRegression.cs` | 1376 | PlacedUpgradePageTruthRegression [placed-upgrade-page] -- pins THE INVARIANT that EVERY structure family with an upgrade ladder reaching the upgrade panel gets a TRUTH... |
| 333 | `[dungeon-gem-exclusivity]` | `Assets/Editor/Regression/DungeonGemExclusivityRegression.cs` | 1384 | DungeonGemExclusivityRegression - the oracle for WO-1041 / WO-1042. |
| 334 | `[combat-cue-authority]` | `Assets/Editor/Regression/CombatCueAuthorityRegression.cs` | 1392 | CombatCueAuthorityRegression [combat-cue-authority] |
| 335 | `[ranger-bow-fire]` | `Assets/Editor/Regression/RangerBowFireRegression.cs` | 1395 | RangerBowFireRegression [ranger-bow-fire] |
| 336 | `[class-primary-block]` | `Assets/Editor/Regression/ClassPrimaryAndKnightBlockRegression.cs` | 1396 | Locks class-authored primary attacks and the Knight's held shield pose. |
| 337 | `[raid-repeat-clear]` | `Assets/Editor/Regression/RaidRepeatClearRegression.cs` | 1398 | RaidRepeatClearRegression [raid-repeat-clear]  --  markers RAID_REPEAT_CLEAR_OK / _FAIL |
| 338 | `[raid-cooldown]` | `Assets/Editor/Regression/RaidCooldownRegression.cs` | 1407 | RaidCooldownRegression [raid-cooldown]  --  markers RAID_COOLDOWN_OK / _FAIL |
| 339 | `[heartfire]` | `Assets/Editor/Regression/HeartfireRegression.cs` | 1408 | HeartfireRegression [heartfire]  --  markers HEARTFIRE_OK / HEARTFIRE_FAIL |
| 340 | `[heartfire-pips]` | `Assets/Editor/Regression/HeartfirePipsRegression.cs` | 1410 | HeartfirePipsRegression [heartfire-pips] WO-1419: the player-facing Heart plate uses real flame Images, never ASCII pips. Markers: HEARTFIRE_PIPS_OK / HEARTFIRE_PIPS_F... |
| 341 | `[raid-loot-currency]` | `Assets/Editor/Regression/RaidLootCurrencyRegression.cs` | 1419 | Pins the WO-1374 raid payout: wood + iron on the map's ladder, crystals and food untouched, gold still zero. Returns true (summary) / false (detail). Never throws. |
| 342 | `[raid-gold-arrow]` | `Assets/Editor/Regression/RaidGoldArrowRegression.cs` | 1420 | Pins the raid GOLD payout (the map's missing arrow), its per-camp table, the crystal cut, and the two camp-multiplier exclusions. Returns true (summary) / false (detail). |
| 343 | `[raid-payout-visibility]` | `Assets/Editor/Regression/RaidPayoutVisibilityRegression.cs` | 1421 | WO-1374/1375: the raid payout is visible, and a victory counts. |
| 344 | `[raid-escalation]` | `Assets/Editor/Regression/RaidEscalationRegression.cs` | 1422 | RaidEscalationRegression - the raid ladder actually escalates, and the tier-4 target cannot dead-end the player. |
| 345 | `[raid-selection-spoils]` | `Assets/Editor/Regression/RaidSelectionSpoilsRegression.cs` | 1423 | RaidSelectionSpoilsRegression - the Raid Selection rows say what a raid PAYS, the pips carry data or nothing, and a camp above the army says so in words. |
| 346 | `[raid-selection-layout]` | `Assets/Editor/Regression/RaidSelectionLayoutRegression.cs` | 1427 | RaidSelectionLayoutRegression - the RAIDS camp list MEASURES, at four camps and at eight: no card overlaps another, every card sits wholly inside the scrolling content... |
| 347 | `[raid-season-xp]` | `Assets/Editor/Regression/RaidSeasonXpRegression.cs` | 1428 | Pins the raid -> Season Pass XP contract: the table, the once-ever bonus, the wiring. |
| 348 | `[raid-funnel]` | `Assets/Editor/Regression/RaidFunnelRegression.cs` | 1429 | Pins the WO-1374 six-event funnel: distinct names on one rail, correct 24h boundaries, and every step wired to a real call site. |
| 349 | `[starter-army-grant]` | `Assets/Editor/Regression/StarterArmyGrantRegression.cs` | 1430 | Pins the WO-1374 free starter squad: it fires, it fires exactly once per save, it grants the right unit, and it costs nothing. |
| 350 | `[raid-discoverability-copy]` | `Assets/Editor/Regression/RaidDiscoverabilityCopyRegression.cs` | 1431 | Pins the four WO-1374 discoverability fixes: the Guide's direction, the raid dailies' feature gate, the single gated raid door, and a refusal that names the actual blo... |
| 351 | `[hire-reinforcements]` | `Assets/Editor/Regression/HireReinforcementsRegression.cs` | 1432 | HireReinforcementsRegression - WO-1372 Lane D: GOLD HIRES MERCENARIES. Marker: HIRE_REINFORCEMENTS_OK / HIRE_REINFORCEMENTS_FAIL. |
| 352 | `[away-summary-report]` | `Assets/Editor/Regression/AwaySummaryReportRegression.cs` | 1433 | Headless oracle for the away summary's four-axis reveal gate + COLLECT wiring. |
| 353 | `[baselayout-roundtrip]` | `Assets/Editor/Regression/BaseLayoutRoundTripRegression.cs` | 1439 | BaseLayoutRoundTripRegression - a MULTI-RECORD BaseLayout survives the REAL save -> reload -> migrate cycle (WO-1361, headless DATA oracle). |
| 354 | `[training-costs-time-only]` | `Assets/Editor/Regression/TrainingCostsTimeOnlyRegression.cs` | 1444 | TrainingCostsTimeOnlyRegression [training-costs-time-only] - WO-1387. Marker: TRAINING_COSTS_TIME_ONLY_OK / TRAINING_COSTS_TIME_ONLY_FAIL. |
| 355 | `[spawn-budget-vfx-warm]` | `Assets/Editor/Regression/SpawnBudgetAndVfxWarmRegression.cs` | 1445 | SpawnBudgetAndVfxWarmRegression [spawn-budget-vfx-warm] |
| 356 | `[forge-shelf-kind]` | `Assets/Editor/Regression/ForgeShelfClassKindRegression.cs` | 1447 | ForgeShelfClassKindRegression [forge-shelf-kind] |
| 357 | `[realm-storefront]` | `Assets/Editor/Regression/RealmStorefrontRegression.cs` | 1456 | Artifact oracle for the PROD-003 baked storefront. See the file header. |
| 358 | `[welcome-back-doors]` | `Assets/Editor/Regression/WelcomeBackDoorsRegression.cs` | 1457 | Headless oracle for the away summary's optional door rows and ready door. |
| 359 | `[enemy-warm-order]` | `Assets/Editor/Regression/EnemyWarmOrderRegression.cs` | 1463 | EnemyWarmOrderRegression [enemy-warm-order] |
| 360 | `[enemy-family-label]` | `Assets/Editor/Regression/EnemyFamilyLabelRegression.cs` | 1467 | EnemyFamilyLabelRegression [enemy-family-label] |
| 361 | `[content-build-target]` | `Assets/Editor/Regression/ContentBuildTargetRegression.cs` | 1473 | ContentBuildTargetRegression [content-build-target] |
| 362 | `[npc-idle-controller]` | `Assets/Editor/Regression/NpcIdleControllerRegression.cs` | 1474 | NpcIdleControllerRegression [npc-idle-controller] - pins that a townsperson idles like a townsperson, not like a knight waiting to be attacked. |
| 363 | `[spawn-area-enemy-ids]` | `Assets/Editor/Regression/SpawnAreaEnemyIdRegression.cs` | 1475 | SpawnAreaEnemyIdRegression [spawn-area-enemy-ids] - the gate for the defect class that shipped on 2026-08-20: A CANONICAL DATA FILE NAMES AN ENEMY ID THAT NOTHING CAN ... |
| 364 | `[regression-marker]` | `Assets/Editor/Regression/RegressionMarkerRegression.cs` | 1476 | RegressionMarkerRegression [regression-marker] |
| 365 | `[raid-wall-material]` | `Assets/Editor/Regression/RaidWallMaterialRegression.cs` | 1481 | RaidWallMaterialRegression [raid-wall-material]  --  markers RAID_WALL_MATERIAL_OK / _FAIL |
| 366 | `[buy-gate]` | `Assets/Editor/Regression/BuyGateAndPriceLadderRegression.cs` | 1486 | Pins the WO-1121 price-ladder ruling and the per-channel wallet buy gate (WO-1386: every price on Solana, above $4.99 elsewhere). |
| 367 | `[store-sku-grant]` | `Assets/Editor/Regression/StoreSkuGrantRegression.cs` | 1490 | Pins that every browsable SKU has a working grant path (WO-1246). |
| 368 | `[monetization-activation]` | `Assets/Editor/Regression/MonetizationActivationRegression.cs` | 1491 | Independent source contract for WO-1146/1147 activation-critical seams. |
| 369 | `[mainnet-canary]` | `Assets/Editor/Regression/MainnetCanaryRegression.cs` | 1492 | Independent source oracle for MON002's real-value safety envelope. |
| 370 | `[store-commerce-state]` | `Assets/Editor/Regression/StoreCommerceStateRegression.cs` | 1493 | UI-002: truthful, visibly distinct store commerce lifecycle presentation. |
| 371 | `[store-pi-skin]` | `Assets/Editor/Regression/StorePiSkinCurrencyRegression.cs` | 1501 | WO-1323: the Pi skin's store never speaks SKR, and the SKR skin is untouched. |
| 372 | `[structure-orientation]` | `Assets/Editor/Regression/StructureOrientationOracle.cs` | 1502 | PROD-008 - orientation/height oracle over structures-catalog.json. |
| 373 | `[night-market-ui]` | `Assets/Editor/Regression/NightMarketUiRegression.cs` | 1509 | UI-001: independent source oracle for the landscape Night Market and its persistent HUD door. Presentation only. It deliberately does not inspect or alter PurchaseGate... |
| 374 | `[night-market-runtime-layout]` | `Assets/Editor/Regression/NightMarketRuntimeLayoutRegression.cs` | 1517 | NightMarketRuntimeLayoutRegression [night-market-runtime-layout] (WO-1162 ?1 FIX 3) - the Night Market's layout is proved on REAL RectTransforms, after a real layout p... |
| 375 | `[cost-format-source]` | `Assets/Editor/Regression/CostFormatSourceRegression.cs` | 1518 | Zero allowlist: match emission shapes so returns, assignments, interpolation, and TMP .text writes are covered without naming their current owners. |
| 376 | `[cathedral-mage-hp]` | `Assets/Editor/Regression/CathedralMageHpRegression.cs` | 1519 | no header |
| 377 | `[echo-harvest-assignment]` | `Assets/Editor/Regression/EchoHarvestAssignmentRegression.cs` | 1520 | Independent cap scenario: call AssignPet immediately after destruction, before any getter/yield path has a chance to prune the Unity-null slot. |
| 378 | `[world-hold]` | `Assets/Editor/Regression/TransactionWorldHoldRegression.cs` | 1526 | Pins WO-1149: a transaction freezes the world, and every exit unfreezes it. |
| 379 | `[ui-touch-oracle]` | `Assets/Editor/Regression/UiTouchClampRegression.cs` | 1536 | UiTouchClampRegression - WO-1060 section 6.2 / 6.3. THE RED-THEN-GREEN PROOF. |
| 380 | `[economy-credit-reporting]` | `Assets/Editor/Regression/EconomyCreditReportingRegression.cs` | 1544 | WO-978 regression slice: callers must report measured credit, never merely echo intent. Registration in DataRegression.cs is deliberately committer-fenced to the lead. |
| 381 | `[capture-provenance]` | `Assets/Editor/Regression/CaptureProvenanceRegression.cs` | 1554 | WO-1080 oracle. Proves the provenance chain is ALIVE, not merely present: the resolver really answers on this machine, the wire shape round-trips, the parser refuses m... |
| 382 | `[drops-to-inventory]` | `Assets/Editor/Regression/DropsGoToInventoryRegression.cs` | 1560 | DropsGoToInventoryRegression [drops-to-inventory]  -- WO-1214 |
| 383 | `[reward-fly]` | `Assets/Editor/Regression/WO1225RewardFlyRegression.cs` | 1564 | WO-1225: the reward acknowledgement outranks the modal band and never lies. |
| 384 | `[kill-reward-raid-suppression]` | `Assets/Editor/Regression/KillRewardRaidSuppressionRegression.cs` | 1565 | KillRewardRaidSuppressionRegression [kill-reward-raid-suppression] - WO-1227. |
| 385 | `[wo1232-enemy-level]` | `Assets/Editor/Regression/WO1232EnemyLevelSourceRegression.cs` | 1566 | WO1232EnemyLevelSourceRegression - "HP / 25 is not a level system" (owner, 2026-08-26). |
| 386 | `[vfx-ambient-budget]` | `Assets/Editor/Regression/VfxAmbientLoopBudgetRegression.cs` | 1567 | VfxAmbientLoopBudgetRegression [vfx-ambient-budget] - WO-1229. The oracle that keeps AMBIENT ROOM DRESS from starving the colourblind low-HP tell. |
| 387 | `[vfx-perf-gate]` | `Assets/Editor/Regression/VfxPerformanceGateRegression.cs` | 1568 | VfxPerformanceGateRegression [vfx-perf-gate] - WO-1242. The oracle that keeps the Seeker frame-time gate from silencing the one signal the owner cannot lose, and from ... |
| 388 | `[cost-row-fit]` | `Assets/Editor/Regression/CostRowFitRegression.cs` | 1569 | CostRowFitRegression -- WO-1060. THE COST ROW MUST STAY INSIDE ITS BAND. |
| 389 | `[first-raid-soft-gate]` | `Assets/Editor/Regression/FirstRaidSoftGateRegression.cs` | 1570 | WO-823 Phase E oracle - the first-raid soft gate, end to end. |
| 390 | `[maintenance-toggles]` | `Assets/Editor/Regression/MaintenanceTogglesRegression.cs` | 1571 | MaintenanceTogglesRegression [maintenance-toggles] |
| 391 | `[tunable-defaults]` | `Assets/Editor/Regression/RemoteTunablesDefaultsRegression.cs` | 1576 | RemoteTunablesDefaultsRegression [tunable-defaults] |
| 392 | `[night-store-aura]` | `Assets/Editor/Regression/NightStoreAuraSelectionRegression.cs` | 1584 | NightStoreAuraSelectionRegression [night-store-aura]  -- WO-1343 |
| 393 | `[over-time]` | `Assets/Editor/Regression/OverTimeEffectRegression.cs` | 1591 | OverTimeEffectRegression [over-time] - WO-1330. THE PROOF THAT THE EFFECT ACTUALLY TICKS. |
| 394 | `[catalog-seam]` | `Assets/Editor/Regression/RemoteCatalogSeamRegression.cs` | 1597 | RemoteCatalogSeamRegression [catalog-seam] |
| 395 | `[troop-strike-vfx]` | `Assets/Editor/Regression/TroopStrikeVfxRegression.cs` | 1598 | WO-935 Phase 3 oracle - troop melee + archer strike presentation. |
| 396 | `[starter-armour]` | `Assets/Editor/Regression/StarterArmourOwnershipRegression.cs` | 1599 | StarterArmourOwnershipRegression [starter-armour] (WO-1240) |
| 397 | `[armour-catalog-job]` | `Assets/Editor/Regression/ArmourCatalogJobRegression.cs` | 1600 | ArmourCatalogJobRegression [armour-catalog-job] (WO-1241) |
| 398 | `[stacktrace-logtype]` | `Assets/Editor/Regression/StackTraceLogTypeRegression.cs` | 1606 | StackTraceLogTypeRegression [stacktrace-logtype] |
| 399 | `[early-ladder]` | `Assets/Editor/Regression/WO1217EarlyEconomyLadderRegression.cs` | 1607 | WO1217EarlyEconomyLadderRegression [early-ladder] -- WO-1217 owner rulings, 2026-08-26, Seeker build 2026.08.26.341419. |
| 400 | `[palette-storage-tail]` | `Assets/Editor/Regression/PaletteStorageTailRegression.cs` | 1608 | PaletteStorageTailRegression [palette-storage-tail] |
| 401 | `[json-only-source]` | `Assets/Editor/Regression/JsonMirrorLiteralRegression.cs` | 1609 | JsonMirrorLiteralRegression [json-only-source] |
| 402 | `[siege-untouchable]` | `Assets/Editor/Regression/SiegeUntouchableRegression.cs` | 1610 | The standing guard on the owner's untouchable list: crystals, SKR, purchased goods and equipped gear are out of a siege's reach under every present and future stakes r... |
| 403 | `[resource-authority]` | `Assets/Editor/Regression/ResourceAuthorityRegression.cs` | 1611 | WO-1212: exactly one authority per player-facing balance. |
| 404 | `[echo-passive-mend]` | `Assets/Editor/Regression/EchoPassiveMendCommsRegression.cs` | 1612 | EchoPassiveMendCommsRegression -- headless oracle for WO-1231: passive Echo mending must SAY what it is doing, and must say where the materials went. |
| 405 | `[tutorial-coach]` | `Assets/Editor/Regression/TutorialCoachEscalationRegression.cs` | 1613 | TutorialCoachEscalationRegression [tutorial-coach]  --  WO-1238 guardrails for the escalating coach beat: the tutorial must SPEAK before the watchdog rescues. |
| 406 | `[mana-scroll]` | `Assets/Editor/Regression/ManaScrollFtueRegression.cs` | 1614 | ManaScrollFtueRegression [mana-scroll]  --  WO-1235 guardrails for the founding mana potions, the recipe-scroll drop, and THE LIVE-SAVE ANTI-RETRO-LOCK. |
| 407 | `[founders-wall]` | `Assets/Editor/Regression/FoundersMonumentWallRegression.cs` | 1619 | FoundersMonumentWallRegression [founders-wall] |
| 408 | `[structure-null-slot]` | `Assets/Editor/Regression/StructureNullMaterialSlotRegression.cs` | 1623 | StructureNullMaterialSlotRegression [structure-null-slot] |
| 409 | `[store-name-single-source]` | `Assets/Editor/Regression/StoreNameSingleSourceRegression.cs` | 1627 | StoreNameSingleSourceRegression [store-name-single-source] (WO-1398) - the store has ONE player-facing name and every face that opens it renders that name. |
| 410 | `[realm-store-single-registrar]` | `Assets/Editor/Regression/RealmStoreSingleRegistrarRegression.cs` | 1632 | RealmStoreSingleRegistrarRegression [realm-store-single-registrar] (WO-1395) PanelId.RealmStore has exactly ONE registrar per shipped artifact, and every registrar ans... |
| 411 | `[dock-settings-route]` | `Assets/Editor/Regression/DockSettingsRouteRegression.cs` | 1636 | DockSettingsRouteRegression [dock-settings-route] (WO-1399) - the gear dock's row labelled "Settings" opens SETTINGS, and Help is a row inside it. |
| 412 | `[deck-return-door]` | `Assets/Editor/Regression/DeckReturnDoorRegression.cs` | 1640 | DeckReturnDoorRegression [deck-return-door] (WO-1400) - a panel opened FROM a deck returns to that deck when it closes; a panel opened from the HUD does not. |
| 413 | `[cosmetic-shop-reach]` | `Assets/Editor/Regression/CosmeticShopReachabilityRegression.cs` | 1644 | CosmeticShopReachabilityRegression [cosmetic-shop-reach] (WO-1397) - the Cosmetic Shop has a door a PLAYER can tap, and the door goes through PanelRouter. |
| 414 | `[manage-row-benefit]` | `Assets/Editor/Regression/ManageRowBenefitRegression.cs` | 1645 | Headless contract for WO-1405's per-row benefit line and worded location. |
| 415 | `[night-market-no-wallet]` | `Assets/Editor/Regression/NightMarketNoWalletRegression.cs` | 1646 | NightMarketNoWalletRegression [night-market-no-wallet] (WO-1409) - the Night Market a player WITHOUT a wallet sees: one reason, nine prices, and a badge that is a word... |
| 416 | `[preview-rt-samples]` | `Assets/Editor/Regression/PreviewRenderTextureSamplesRegression.cs` | 1654 | PreviewRenderTextureSamplesRegression - WO-1451. The tower preview's RenderTexture and its camera must agree on sample count. |
| 417 | `[enemy-probe-cadence]` | `Assets/Editor/Regression/EnemyProbeCadenceRegression.cs` | 1663 | EnemyProbeCadenceRegression - WO-1450 (probe log evicts the logcat ring) and WO-1459 sec.2 suspect 3 (per-frame ProbeForStructure physics from Enemy:Update). |

## APPENDIX - suites with NO header comment (8)

These 8 registered suites carry no comment block above their class declaration, so their "what it
locks" cell cannot be derived. Adding a one-line header to each is a cheap, standing chore.

- `[glimmer]` -> `Assets/Editor/Regression/GlimmerEconomyRegression.cs` (registered at DataRegression.cs:405)
- `[repair-prompt-readability]` -> `Assets/Editor/Regression/RepairPromptReadabilityRegression.cs` (registered at DataRegression.cs:484)
- `[temporary-builder]` -> `Assets/Editor/Regression/TemporaryBuilderRegression.cs` (registered at DataRegression.cs:527)
- `[card-collection-foundation]` -> `Assets/Editor/Regression/CardCollectionFoundationRegression.cs` (registered at DataRegression.cs:528)
- `[build-collection-player]` -> `Assets/Editor/Regression/BuildCollectionPlayerRegression.cs` (registered at DataRegression.cs:529)
- `[night-market-shared-card]` -> `Assets/Editor/Regression/NightMarketSharedCardRegression.cs` (registered at DataRegression.cs:532)
- `[jeweler-discovery-ftue]` -> `Assets/Editor/Regression/JewelerDiscoveryFtueRegression.cs` (registered at DataRegression.cs:654)
- `[cathedral-mage-hp]` -> `Assets/Editor/Regression/CathedralMageHpRegression.cs` (registered at DataRegression.cs:1519)

---

*Maintained by the Sunday sweep. Regenerate by re-running the fence parse described above; never
hand-edit a row's line number - re-derive it.*
