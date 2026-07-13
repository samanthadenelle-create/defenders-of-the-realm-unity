# WO Silo Plan — File-Disjoint Lanes for Parallel Implementation

**Compiled:** 2026-06-18 · branch `feat/tower-core-loop` · READ-ONLY analysis (no code edited).
**Input:** `open-wos.txt` (147 open WOs) + `Assets/_Modules/` module map + `CLAUDE.md` §5/§9 + `docs/MASTER_CATALOG*`.
**Goal:** group the open WOs into FILE-DISJOINT silos so N implementer agents run in parallel without touching the same files, and the CLI can verify each lane against real code.

> **Scope caveat (owner read first):** WO scope is inferred from titles + a sample of `WORK_ORDER_*.md` specs + the module map. `open-wos.txt` carries **renumber noise** — several lines show a NEW number with an OLD title in the text (e.g. `WO-466 — WO-453 Offensive Troop System`, `WO-458 — WO-327…`). I keyed off the **leading WO number** and the title. Items I couldn't pin from the title are in **NEEDS-SPEC**. The MASTER_CATALOG numbering note still says "next free WO = 412" — it is stale vs. the board (next free = 430); treat the board as truth.

---

## Bottleneck files (ONE agent at a time — flag on every touching WO)

Per CLAUDE.md §9 + the editor-tools catalog, these are **serialization points**. Any two WOs that both write the same file below CANNOT run concurrently:

| Bottleneck file/system | Path | Why |
|---|---|---|
| **VillageSceneBuilder.\*** (12 partials) | `Assets/Editor/VillageSceneBuilder.*.cs` | §9 named serialization bottleneck; Village2/town scene gen |
| **CastleHubBuilder** | `Assets/Editor/CastleHubBuilder.cs` | Builds `MainCastle_Hall` home hub; owner hand-dialed offsets |
| **GarrisonSceneBuilder** | `Assets/Editor/GarrisonSceneBuilder.cs` + `.Scenes.cs` | Raid outpost scene gen |
| **OuterWorldBuilder** | `Assets/Editor/OuterWorldBuilder.cs` | OuterWorld region gen (added bottleneck — many world WOs) |
| **VillageHudController** | `Assets/_Modules/HUD/VillageHudController.cs` | The single town/combat HUD god-object; ~all HUD WOs converge here |
| **ElarionUiKit** | `Assets/_Modules/Core/UI/ElarionUiKit.cs` | WO-405 design-system kit; every HUD WO consumes it → 405 must land FIRST |
| **FeatureFlags** | `Assets/_Modules/Core/FeatureFlags.cs` | Shared flag enum; append-only, coordinate |
| **CoreServices** | `Assets/_Modules/Core/CoreServices.cs` | Shared service registry; append-only, coordinate |
| **EnemyBrain** | `Assets/_Modules/Village/.../EnemyBrain.cs` | All enemy-AI WOs converge here |
| **WaveManager** | `Assets/_Modules/Village/Waves/…` | Wave-loop WOs converge here |
| **Scene `.unity` bakes** | `Assets/Scenes/*.unity` | NEVER hand-edit (CLAUDE.md §3); rebuild via builder; bakes are one-at-a-time, editor-closed |

---

## LANE A — World / Environment / Scene Gen  (architect lane, §9)
Editor scene-builders + Village/World runtime. **Heavy bottleneck contention** — most are scene-gen.

| WO | status | title | primary files / system | P |
|---|---|---|---|---|
| WO-104 | Ready | Castle fortification + moat | CastleHubBuilder / VillageSceneBuilder.Fortify · **BOTTLENECK** | P2 |
| WO-137 | Ready | Castle/rampart rebake | CastleHubBuilder + scene bake · **BOTTLENECK** | P2 |
| WO-181 | Ready | Rampart stairs + upper siege defenses | World/StairwayBuilder, RampartNavLinkInstaller | P2 |
| WO-142 | Ready | Outer world regions | OuterWorldBuilder · **BOTTLENECK** | P1 |
| WO-159 | Ready | Node settlements (claim/defend/deplete) | World/Camps + settlement runtime | P1 |
| WO-239 | Ready | Node claiming + outpost build | World/Outpost* runtime | P1 |
| WO-426 | Ready | Enable node/outpost claim loop in OuterWorld | World/Outpost* + OuterWorld · **BOTTLENECK** | P1 |
| WO-165 | Ready | Dungeon world portals | Village/Buildings/DungeonPortal, World portal spawner | P2 |
| WO-467 | Ready | Faction Base Scene Generator | new editor scene-gen + Garrison · **BOTTLENECK** | P1 |
| WO-456 | Ready | **P0** MainCastle_Hall has no Tree of Life / win-target | CastleHubBuilder + Heart · **BOTTLENECK** | **P0** |
| WO-463 | Spec | Tree of Life fail (owner playtest) | Heart / win-target wiring (overlaps 456) | **P0** |
| WO-323 | Ready | Trees render all white (material/shader) | MagentaMaterialFixer / tree materials | P1 |
| WO-402 | Held | Stray blue sphere in sky — remove | scene-gen object removal · **BOTTLENECK** | P2 |
| WO-435 | Held | Mirza Beig Terrain Rain shaders missing dep | shader/import only | P3 |
| WO-464 | Ready | Elemental Zone Layer on RegionZone | World/ZoneManager + RegionZone | P2 |

**Within-lane serialization:** 456/463 (Heart/Castle), 104/137 (Castle), 142/426/402/467 (OuterWorld+Garrison gen) must each go one-at-a-time on their builder. 181/159/239/323/435/464 are runtime/asset-only and file-disjoint from the builders.

---

## LANE B — Combat / Enemy-AI  (code-only, no scene files, §9)
Converges on `EnemyBrain` / `Enemy` / `WaveManager`.

| WO | status | title | primary files / system | P |
|---|---|---|---|---|
| WO-145 | Ready | Advanced enemy tactics | EnemyBrain · **BOTTLENECK** | P1 |
| WO-146 | Ready | Formation movement | EnemyBrain / EnemyFactory · **BOTTLENECK** | P1 |
| WO-147 | Ready | Situational awareness / perception | EnemyBrain · **BOTTLENECK** | P1 |
| WO-143 | Ready | Roaming raids | RegionMobSpawner / raid loop | P2 |
| WO-160 | Ready | Wandering tribes (randomized raids) | tribe/settlement spawner | P2 |
| WO-155 | Blocked | Region enemy spawning + red-skull | RegionMobSpawner + ThreatSkullPlate | P1 |
| WO-454 | Ready | **P1** EnemyBrain NavMeshPath from ctor throws | EnemyBrain · **BOTTLENECK** | **P0** |
| WO-419 | In prog | **P1** Enemies don't attack after castle→OuterWorld | EnemyBrain / WaveManager seam · **BOTTLENECK** | **P0** |
| WO-445 | Ready | Enemy brute animation retarget | EnemyAnimatorSetup / brute controller | P2 |
| WO-111 | Ready | Audio depth + boss battles + enemy outposts | DragonBoss + outpost + audio (cross-lane) | P2 |
| WO-128 | Ready | Pet anti-ranged ability | Pets module (could go Lane G) | P2 |
| WO-458 | Ready | Admin 'Trigger next wave' no-op | WaveManager.ForceBeginNextWave · **BOTTLENECK** | P1 |
| WO-466 | Spec | Offensive Troop System (train/level/deploy) | Village/Troops + new | P2 |

**Within-lane serialization:** 454→145/146/147/419/458 all touch EnemyBrain/WaveManager → strictly one-at-a-time. **Sequence: WO-454 (ctor throw) FIRST**, then 419, then tactics WOs. 445/111/128/143/160/466 are file-disjoint from EnemyBrain.

---

## LANE C — ATB / Battle  (BattleATB assembly, isolated)

| WO | status | title | primary files / system | P |
|---|---|---|---|---|
| WO-386 | Spec | Battle Visualization System (ATB sim→seen) | BattleATB/Engine + BattleController | P1 |
| WO-388 | In prog | Load player's real castle as Arena defender | Village/Arena + Battle | P1 |
| WO-389 | In prog | Arena Defense System (pre-placed troops) | Village/Arena/ArenaDefense* | P1 |
| WO-390 | Spec | Battle Potion Loadout (3 slots) | BattleATB + loadout data | P2 |
| WO-421 | Ready | **P1** Battle HUD broken — skill bar empty | BattleHudUgui (Battle HUD; gated on 405) | **P0** |
| WO-437 | Ready | Combat HUD restyle from Tech pack | BattleHudUgui (gated on 405) | P2 |

**Note:** ATB Engine WOs (386/390) are pure-C# and fully disjoint from Arena (388/389). 421/437 touch the Battle HUD → coordinate with Lane D's HUD foundation (WO-405).

---

## LANE D — UI / HUD / Onboarding  (HUD + Onboarding + BuildMode UI)
**GATED:** WO-405 builds the `ElarionUiKit` foundation that nearly every HUD WO consumes. **405 must land first; the rest then fan out but most still converge on `VillageHudController`.**

| WO | status | title | primary files / system | P |
|---|---|---|---|---|
| WO-405 | In prog | **P0** Complete UGUI design system (ElarionUiKit) — DO FIRST | `Core/UI/ElarionUiKit.cs` + VillageHudController · **BOTTLENECK/GATE** | **P0** |
| WO-403 | Ready | In-town HUD rework (parchment/rune frame) | VillageHudController · **BOTTLENECK** (after 405) | P1 |
| WO-411 | Blocked | Town HUD doesn't match mockup (10 deviations) | VillageHudController · **BOTTLENECK** (after 405) | P1 |
| WO-307 | Ready | HUD visual overhaul (responsive web+mobile) | VillageHudController · **BOTTLENECK** (after 405) | P1 |
| WO-309 | Ready | Resource bar icons + qty (Gems→Crystals) | VillageHudController resource bar · **BOTTLENECK** | P1 |
| WO-438 | Blocked | Global Tech-hud styling rollout (all screens) | many HUD files · **BOTTLENECK** (after 405) | P2 |
| WO-353 | Ready | Palette filters & category tabs | BuildMode/BuildPaletteUI | P2 |
| WO-354 | Ready | Upgrade tier display & synergy bonuses | BuildMode/BuildStructureInfoPanel | P2 |
| WO-355 | Ready | Portrait/vertical layout responsiveness | BuildMode UI | P2 |
| WO-356 | Ready | Placement validation messages & grid toggle | BuildMode/PlacementGrid + feedback | P2 |
| WO-357 | Ready | Mobile touch gestures & accessibility | BuildMode/LeanTouchBuildDriver | P2 |
| WO-465 | Ready | **P1** WebGL build-mode palette empty | BuildMode/BuildPaletteUI · (overlaps 353) | **P0** |
| WO-282 | Ready | BuildPreviewModal premium rotation | BuildMode/BuildPreviewModal | P2 |
| WO-455 | Ready | Icon_hud_talk missing CanvasGroup → HUD partial | VillageHudController · **BOTTLENECK** | P1 |
| WO-446 | Ready | Companion portrait missing in party HUD | VillageHudController party bridge · **BOTTLENECK** | P1 |
| WO-448 | Ready | Compass N/S/E/W heading | HUD/CompassHud | P1 |
| WO-416 | Ready | Hide floating "Talk:" world prompt | HUD talk prompt + AttentionGlowUi | P2 |
| WO-230 | Ready | Hero Select: 4 character cards | Onboarding/HeroSelect | P1 |
| WO-235 | Ready | Death & spire-destroyed screens | UI/GameOverUI + screens | P1 |
| WO-447 | Ready | Remove PetSelect screen → Echo Hollow route | Onboarding/PetSelect removal | P1 |
| WO-133 | Ready | Onboarding FTUE wiring | Onboarding flow (cross w/ Lane I) | P1 |
| WO-129 | Ready | Leaderboard / profile / social | HUD/LeaderboardPanel + ClanChatPanel | P2 |
| WO-121 | Ready | Metrics / analytics dashboard | HUD dashboard (or backend Lane H) | P2 |

**Within-lane serialization:** 405→403/411/307/309/438/455/446 ALL hit `VillageHudController` → strict serial after 405. BuildMode WOs (353/354/355/356/357/465/282) are file-disjoint among themselves *mostly* but 353⟷465 overlap on BuildPaletteUI. CompassHud (448), GameOverUI (235), Onboarding (230/447/133), Leaderboard (129) are file-disjoint silos that can run in parallel with the HUD-controller work.

---

## LANE E — Economy / Harvest / Crafting / Progression / Buildings

| WO | status | title | primary files / system | P |
|---|---|---|---|---|
| WO-453 | Ready | **P0** ResourceBuildingProgression type-init throws | Buildings/Progression/ResourceBuildingProgression.cs | **P0** |
| WO-325 | Ready | Nothing happens at resource node (harvest dead) | Buildings/CrystalMine + Harvest | **P0** |
| WO-424 | In prog | **P1** Harvested resources not added to HUD count | Harvest + CrystalEconomy→HUD bridge | **P0** |
| WO-228 | Ready | Resource nodes + pet harvesting | Harvest/Worker + Pets bridge | P1 |
| WO-117 | Ready | Worker dispatch autocollect | Harvest/WorkerManager | P1 |
| WO-144 | Ready | Regional crystal subtypes | CrystalMine + crystal data | P2 |
| WO-154 | Ready | Rare timed crystal spawns | CrystalMine spawn logic | P2 |
| WO-395 | Ready | Resource node visual replacement (asset audit) | CrystalMine visuals + asset audit | P1 |
| WO-313 | Ready | Windmill production crafter station | Crafting + Building | P2 |
| WO-392 | Ready | Warcraft-style tiered building upgrade (Lumbermill/Forge/Armorer) | Buildings/Progression/TechTree | P1 |
| WO-407 | Blocked | Tiered upgrade: Arcane Tower (extends 392) | Buildings/ArcaneTower + TechTree | P2 |
| WO-113 | Ready | Arcane tower buildable | Buildings/ArcaneTower | P2 |
| WO-114 | Ready | Wall upgrade tiers | Walls/WallLayout + upgrade | P2 |
| WO-413 | In prog | **P1** Upgradable buildings wrongly offer shop menu | BuildingInteractable / dialogue split | **P0** |
| WO-293 | Ready | Crafting tiers + legendary recipe system | Crafting/GearCraftingRecipeCatalog | P1 |
| WO-295 | Ready | Legendary Aegis set + ward | Crafting + gear data | P2 |
| WO-151 | Blocked | Village progression + crafting | Crafting + progression (broad) | P1 |

**Within-lane serialization:** 453→392/407 touch Progression/TechTree → serialize. 325/144/154/395 all touch CrystalMine → serialize. 424/228/117 touch Harvest → coordinate (424 first). 293/295/151 touch Crafting → serialize. 413 touches BuildingInteractable (disjoint from Progression).

---

## LANE F — VFX / Audio  (no gameplay deps, §9)

| WO | status | title | primary files / system | P |
|---|---|---|---|---|
| WO-171 | Ready | ATB battle theme | Audio/MusicTrack + Village/Audio/WaveMusicController | P2 |
| WO-220 | Spec | Audio feedback | AudioService SFX hooks | P2 |
| WO-243 | Ready | Audio full pass | AudioService + SfxClipLibrary (broad) | P2 |
| WO-219 | Ready | Visual feedback (hit-stop, particles, dmg #) | Village/Vfx + Combat/FloatingHealthBar | P1 |
| WO-217 | Ready | Animation polish (anticipation/impact/recovery) | Hero animator + clips | P2 |
| WO-218 | Ready | Animation layering (attack while moving) | Hero animator controller | P2 |
| WO-420 | Ready | **P2** Fire/spell VFX = giant pixelated quads | Vfx effect textures / materials | P2 |
| WO-366 | Ready | Idle routines (sit/play-dead/cute anims) | Pets animator (or Lane G) | P2 |

**Note:** 217/218 both touch Hero animator → serialize. 171/220/243 are Audio (171 disjoint music vs 220/243 SFX). 219/420 are VFX, mostly disjoint.

---

## LANE G — Pets

| WO | status | title | primary files / system | P |
|---|---|---|---|---|
| WO-297 | Ready | Pet acquisition (tame/egg/rescue) + slots | Pets/acquisition + slots | P1 |
| WO-298 | Ready | Pet skill catalog content + balance | Pets/skill catalog (data) | P2 |
| WO-299 | Ready | Pet bond questlines (Wild Hearts) | Pets + Quests + Yarn (cross Lane I) | P2 |
| WO-470 | Held | Heroes → Addressables | Hero load path (build/perf, cross Lane J) | P2 |

(WO-128 pet anti-ranged, WO-366 pet idle, WO-447 PetSelect-removal are listed in their primary lanes but are Pets-adjacent — coordinate.)

---

## LANE H — Monetization / Backend / Persistence  (fully isolated, §9)

| WO | status | title | primary files / system | P |
|---|---|---|---|---|
| WO-74 | Ready | Solana crypto payments | Wallet/payments | P2 |
| WO-75 | Ready | Shop UI crypto tabs | Wallet PackStore UI | P2 |
| WO-76 | Ready | Staked SKR bonus | Wallet/staking | P2 |
| WO-77 | Ready | Staking full integration | Wallet/staking | P2 |
| WO-78 | Ready | Tx verification + staking dashboard | Wallet/staking + backend | P2 |
| WO-80 | Ready | Vercel + Neon backend | backend (non-Unity) | P1 |
| WO-118 | Ready | Rewarded ads route | Wallet/ads route | P2 |
| WO-120 | Ready | Backend spec reconciliation | backend spec (doc) | P2 |
| WO-129 | Ready | Leaderboard / profile / social | backend + HUD (also Lane D) | P2 |
| WO-412 | Ready | **P1** Vendor Wares catalog empty — BUY tab | StoreService + vendor catalog | **P0** |
| WO-429 | Ready | Store stock from Neon DB (offline-first) | StoreService + backend | P1 |
| WO-444 | Ready | Vendor stock limited by store type | vendor catalog filter | P1 |
| WO-415 | Blocked | Vendor storefront UI (armor) skinned | Vendor modal UI (cross Lane D) | P1 |
| WO-301 | Ready | Party persistence (wallet-keyed roster) | Core/save + Wallet | P1 |

**Within-lane:** 74/76/77/78/118 Wallet → serialize on shared Wallet files. 412/429/444 touch StoreService/vendor catalog → serialize (412 first). 80/120 are non-Unity backend → fully parallel.

---

## LANE I — Narrative / FTUE / Dialogue  (Yarn + DialogueUI + Tutorial)

| WO | status | title | primary files / system | P |
|---|---|---|---|---|
| WO-116 | Ready | NPC dialogue / bark system | NPCs/AmbientNPC + Yarn | P2 |
| WO-222 | Blocked | Tutorial redesign | Tutorial/TutorialDirector | P1 |
| WO-227 | Blocked | Opening cutscene + story companion | DialogueUI/intro + Yarn | P1 |
| WO-238 | Ready | Sylas first-meeting narrative | NPCs/SylasFirstMeeting + Yarn | P1 |
| WO-277 | Ready | Tutorial companion onboarding | Tutorial/CompanionSpawner | P1 |
| WO-294 | Ready | Forgemasters' Saga Yarn + reconciliation | Yarn nodes + scenes | P2 |
| WO-296 | Ready | Reforge choice → finale/ending | Yarn + finale wiring | P2 |
| WO-292 | Ready | Keystone → Spire finale wiring | finale trigger + Yarn | P1 |
| WO-299 | Ready | Pet bond questlines (also Lane G) | Yarn + Quests | P2 |
| WO-300 | Ready | Weaponsmithing lore integration | Yarn + crafting lore | P2 |
| WO-305 | Ready | Relic-recovery quests (Elarion blades) | Quests + Yarn | P2 |
| WO-396 | Ready | Yarn resource-node interaction + pet cutscene | Yarn + node interaction (cross Lane E) | P1 |
| WO-401 | Ready | Blacksmith vendor presentation (sign + Yarn) | BuildingSign + Yarn (cross Lane H) | P1 |
| WO-375 | Ready | Yarn Spinner threading safety + debug removal | DialogueService threading | P1 |
| WO-379 | Held | Echo auto-summoning on Yarn dialogue | DialogueCommandBridge | P2 |
| WO-432 | Spec | NPC companion breaks (playtest) | StoryCompanion + DialogueUI | **P0** |
| WO-238/277/222/227 | — | (FTUE cluster) | overlap on Tutorial/companion | — |

**Within-lane:** 222/227/277/238/432 cluster on Tutorial/companion → serialize. Yarn-content WOs (294/296/300/305/116) edit `.yarn` files → mostly disjoint, but coordinate node names. 375 touches DialogueService core → run alone.

---

## LANE J — Build / Perf / QA / Bugfix-misc

| WO | status | title | primary files / system | P |
|---|---|---|---|---|
| WO-51 | Ready | Mobile performance pass | MobileSettings + quality | P1 |
| WO-54 | Ready | LOD setup | LOD import/setup | P2 |
| WO-191 | Ready | WebGL size optimization | build config + asset strip | P1 |
| WO-211 | Ready | WebGL optimize (remove unused assets) | asset audit/strip | P1 |
| WO-408 | Ready | **P1** WebGL texture optimization 223→<60MB | TextureBatchOptimizer (scripted) | **P0** |
| WO-213 | Ready | Troop downscale → real character models | character model swap | P2 |
| WO-246 | Ready | Replace KayKit NPCs with character pack | NpcPack* editor + prefabs | P2 |
| WO-331 | In prog | **P0** WebGL crash on Village load (Yarn) | triage → DialogueService/Yarn | **P0** |
| WO-434 | Ready | **P1** NUL-padded .cs commit guard | CompileGate.cs | P1 |
| WO-436 | Spec | Deprecation sweep CS0618/CS0414 | WaveManager/GameOverScreen lines | P3 |
| WO-443 | In prog | AutoPilot autonomous playtest bot | Diagnostics/PlayerBot | P1 |
| WO-452 | Ready | AutoPilot hardening (oracle assertions) | Diagnostics/PlayerBot (after 443) | P1 |
| WO-459 | Ready | Recurring NRE spam — root cause | investigation (multi-file) | P1 |
| WO-460 | In prog | Check-in regression test suite | RegressionSuite.cs + tests | P1 |
| WO-468 | Ready | Dev Capture Toolkit (DevCaptureService) | DevTools/new service | P1 |
| WO-462 | Ready | Notion MCP connected (Cowork) | infra/MCP (non-code) | P2 |
| WO-430 | Ready | Handover Triage: WO consolidation | docs only | P1 |

**Within-lane:** 191/211/408 are asset/build optimization → 408 scripted (TextureBatchOptimizer) disjoint from 211 manual strip. 443→452 serialize (PlayerBot). Most others file-disjoint.

---

## Cross-lane bug/canon fixes (Hero / tags — small, file-disjoint)

| WO | status | title | primary files | P | lane |
|---|---|---|---|---|---|
| WO-326 | Ready | Hero walks north but model 90° right | Hero/HeroLocomotion rotation | P1 | B/F |
| WO-423 | Ready | **P1** Hero attacks without facing target | Hero combat/rotate-to-target | **P0** | B |
| WO-254 | Ready | Hero hover exploit fix | Hero/HeroLocomotion or input | P2 | B |
| WO-425 | Ready | **P2** Hero spawns unarmed — default weapon | Hero/GearLoadout / EquipmentController | P2 | E/B |
| WO-449 | Ready | Hero can target/attack from inside wall | Hero targeting + wall collider | P1 | B |
| WO-433 | Ready | HeroTarget/SpawnPoint tags undefined | TagManager (project setting) | P1 | J |
| WO-450 | Ready | Declare Player+HeroTarget tags + guard FindWithTag | TagManager + call sites · **shared-tags** | P1 | J |
| WO-455 | (Lane D) | Icon_hud_talk CanvasGroup | VillageHudController | P1 | D |
| WO-324 | Ready | Dungeon pill lantern NPC + exit placeholders | Dungeons module | P2 | misc |
| WO-461 | Spec | Consolidate proximity-interaction service | new Core service + HUD read | P1 | D/E tech-debt |
| WO-287 | Spec | Threat Assessment / Defensibility intel | World/GateIntelHud + intel | P2 | A/D |
| WO-288 | In prog | Class signature combat moves | Hero abilities | P1 | B |
| WO-399 | Spec | Knight melee weapon skill set | Hero/class data | P1 | B |
| WO-398 | Ready | **P1** Knight dealing ranged damage (should be melee) | Hero/class combat | **P0** | B |
| WO-451 | Ready | Shorten cold-open + remove shooting stars | DialogueUI/intro | P1 | I |

**TagManager note:** WO-433 and WO-450 both edit `ProjectSettings/TagManager.asset` → **serialize** (one tag file). 450 supersedes 433 (both touch the same shared project setting).

---

# PARALLEL-SAFE SET — recommended FIRST WAVE (mutually file-disjoint, high priority)

These 10 are **P0/P1, hit different files, and have no shared-file conflict** — hand each to its own agent simultaneously. CLI batch-gates the combined tree once, commits per lane by explicit path.

| # | WO | title | files (disjoint) | lane |
|---|---|---|---|---|
| 1 | **WO-453** | ResourceBuildingProgression type-init throws (P0) | `Buildings/Progression/ResourceBuildingProgression.cs` | E |
| 2 | **WO-454** | EnemyBrain NavMeshPath ctor throw (P0) | `Village/.../EnemyBrain.cs` | B |
| 3 | **WO-408** | WebGL texture optimization (scripted) | `Editor/TextureBatchOptimizer.cs` | J |
| 4 | **WO-448** | Compass N/S/E/W heading | `HUD/CompassHud.cs` | D |
| 5 | **WO-412** | Vendor Wares catalog empty (P1) | `StoreService` / vendor catalog | H |
| 6 | **WO-235** | Death & spire-destroyed screens | `UI/GameOverUI` + screens | D |
| 7 | **WO-375** | Yarn threading safety + debug removal | `Tutorial/DialogueService.cs` | I |
| 8 | **WO-420** | Fire/spell VFX pixelated quads | `Village/Vfx` textures/materials | F |
| 9 | **WO-450** | Declare Player+HeroTarget tags + guard | `TagManager.asset` + call sites | J |
| 10 | **WO-326** | Hero model 90° rotation | `Village/Hero/HeroLocomotion` (rotation) | B |

**Disjointness check:** E-Progression, EnemyBrain, an Editor optimizer, CompassHud, StoreService, GameOverUI, DialogueService, VFX materials, TagManager, HeroLocomotion — no two share a file. WO-454 (EnemyBrain) and WO-326 (HeroLocomotion) are different files. CLI verifies each against the real code independently.

**Hold out of wave-1 (need gate/sequence):** WO-405 (must land alone — it is the HUD foundation gate; everything HUD waits on it; **start it as its own dedicated track in parallel with wave-1**). WO-456/463 (Castle bottleneck). WO-419 (waits on 454). WO-465/353 (BuildPaletteUI overlap).

---

# SERIALIZED / BOTTLENECK — one-at-a-time within the shared file

| Shared file | WOs (serialize in this order) |
|---|---|
| `Core/UI/ElarionUiKit.cs` + `HUD/VillageHudController.cs` | **WO-405 FIRST**, then 403 → 411 → 307 → 309 → 455 → 446 → 438 |
| `Editor/CastleHubBuilder.cs` (+ scene bake) | WO-456/463 → 104 → 137 |
| `Editor/OuterWorldBuilder.cs` / Garrison gen | WO-142 → 426 → 467 → 402 |
| `Village/.../EnemyBrain.cs` | WO-454 → 419 → 145 → 146 → 147 → 155 |
| `Village/Waves/WaveManager` | WO-419 (seam) → 458 |
| `Buildings/Progression/TechTree.cs` | WO-453 → 392 → 407 |
| `Buildings/CrystalMine.cs` | WO-325 → 144 → 154 → 395 |
| `Village/Harvest/*` | WO-424 → 228 → 117 |
| `Village/Crafting/*` | WO-293 → 295 → 151 → 313 |
| `Wallet/*` (PackStore/staking) | WO-74 → 76 → 77 → 78 → 118 |
| `StoreService` / vendor catalog | WO-412 → 429 → 444 → 415 |
| `BuildMode/BuildPaletteUI.cs` | WO-465 → 353 |
| `Tutorial/*` companion cluster | WO-432 → 277 → 238 → 222 → 227 |
| `Tutorial/DialogueService.cs` | WO-375 alone, then 379 |
| `ProjectSettings/TagManager.asset` | WO-450 (supersedes 433) |
| Hero animator controller | WO-217 → 218 |
| `Diagnostics/PlayerBot.cs` | WO-443 → 452 |
| `Core/FeatureFlags.cs` + `Core/CoreServices.cs` | append-only — any WO adding a flag/service coordinates here |
| Scene `.unity` bakes | NEVER concurrent; editor closed; one bake at a time (CLAUDE.md §3) |

---

# NEEDS-SPEC / AMBIGUOUS — owner please pin scope/files

| WO | why ambiguous |
|---|---|
| WO-120 | "Backend spec reconciliation" — doc vs code? which files? |
| WO-121 | "Metrics/analytics dashboard" — HUD panel vs backend endpoint? lane H or D? |
| WO-129 | "Leaderboard/profile/social" — spans HUD (LeaderboardPanel) AND backend; split into 2 WOs? |
| WO-151 | "Village progression + crafting" — Blocked & very broad; overlaps 392/293; what's the remaining delta? |
| WO-287 | Spec-only; "Threat Assessment intel" — GateIntelHud vs new system? confirm files |
| WO-386 | Spec; "Battle Visualization" — Engine sim vs new presentation layer? big scope |
| WO-432 | Spec; "NPC companion breaks" — needs repro/stack to pin the file (instrument per §12) |
| WO-459 | "Recurring NRE spam — root cause" — investigation, no file yet (instrument first, §12) |
| WO-461 | Tech-debt; "proximity-interaction service" — new Core service touches HUD + Village; confirm seam |
| WO-466 | "Offensive Troop System" — Village/Troops exists but scope (train/level/deploy) is large; spec needed |
| WO-467 | "Faction Base Scene Generator" — new editor scene-gen; confirm it doesn't collide with existing builders |
| WO-468 | "Dev Capture Toolkit" — new DevTools service; confirm spine vs probes split |
| WO-430 | TWO distinct things in open list (Handover Triage doc AND "Notion MCP" as WO-462) — renumber noise; reconcile |
| WO-470 | Held; "Heroes → Addressables" — large build-system change, gated; confirm trigger |

**Renumber-noise flags (titles disagree with leading number):** WO-458(=327), WO-459(=328), WO-460(=329), WO-462(=430), WO-463(=430), WO-464(=450), WO-465(=452), WO-466(=453), WO-467(=454), WO-468(=455), WO-470(=282). Owner should reconcile the board numbering before assigning.

---

## Summary — lane counts
A World/Scene 15 · B Combat-AI 13 · C ATB/Battle 6 · D UI-HUD-Onboarding 23 · E Economy/Crafting 17 · F VFX/Audio 8 · G Pets 4 · H Monetization/Backend 14 · I Narrative/FTUE 16 · J Build/Perf/QA 17 · plus cross-lane Hero/tag/misc fixes. (WOs counted in their primary lane; cross-lane items noted in-table.)
