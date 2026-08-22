# MASTER CATALOG — data-catalogs

**Rebuilt 2026-08-02, verified from the actual code + data on `wip/village2-and-f8-tickets`**
(file reads, byte counts, live drift diff, consumer grep — not from comments; comments that lie
are flagged in §7). Supersedes the 2026-06-12 body and its 2026-07-22 STALE banner.

Area: every canonical JSON catalog under `Assets/Resources/Data/Canonical/**` and
`Assets/StreamingAssets/Data/Canonical/**`, the single loader seam, the byte-parity /
integrity oracles that enforce the dual-copy law, the VFX key-index trio, plus the
non-canonical `Resources/Data` stragglers.

---

## DELTA 2026-08-21 — one new canonical file, and two catalogs that grew rows

Verified on disk 2026-08-21.

- **NEW: `battle_monthly.json`** — present in **both** canonical trees and **byte-identical**
  (`cmp` clean). Top-level keys: `_comment`, `_firewall`, `_noGlimmer`, `_grantSchema`, `version`,
  `battlePassSeasons` (**1** row), `monthlyCards` (**2** rows). Read through `CanonicalJson` by
  `DeNelle.Wallet.BattleMonthlyCatalog`. It is a **sibling** of `packs.json`, deliberately not a
  block inside it — a season is a tiered track and a monthly card is a thirty-claim pool, and
  neither shape fits `PackDef`. `packs.json` is untouched.
- **`canon-strings.json`** (both copies) grew the raid-cooldown lines (read by
  `DeNelle.Core.UI.RaidStrings`), The Night Market's buy-gate refusal lines (`DeNelle.Wallet.StoreStrings`),
  the Season Track / Monthly Ledger state words, and the chest refusal sentence
  (`VillageStrings.Canon`, read by `BreakableContainer`).
- **`scene-configs.json`** (both copies) grew the per-camp raid cooldown durations
  (`RaidCooldownService.DurationFor(SceneConfigDef)`).
- **`structures-catalog.json`**, `barracks.json`, `troops.json`, `building-tiers.json`,
  `gear-levels.json`, `jeweler-recipes.json` all moved in the WO-1129 economy-sink rescale.
  ⛔ **`structures-catalog.json` holds 28 `entries`; `CatalogBootstrap.RegisterFallback()`
  hardcodes THREE of them.** If the JSON ever fails to load, the player gets a silent, different,
  3-row game. See **WO-1137** in the master risk ledger.

---

## 1. THE LAW — dual copies, Resources WINS

### Loader seam
- **`CanonicalJson`** — `Assets/_Modules/Core/Data/CanonicalJson.cs` (ns `DeNelle.Core`).
  The single read path: `CanonicalJson.Read("Data/Canonical/<file>.json")`. Since the
  Tier-0 seam (docs/DATA_ARCHITECTURE_DECISION_2026-06-27.md) it delegates to a swappable
  **`ICatalogSource`** (`CanonicalJson.cs:36` — `Source` property, defaults to
  `LocalJsonCatalogSource`; null-Source is self-healing at `CanonicalJson.cs:45-51`).
  Every read emits `FlowTrace.Step/Warn` under `[Flow:CanonJson]` (`CanonicalJson.cs:52-58`).
- **`LocalJsonCatalogSource`** — `Assets/_Modules/Core/Data/LocalJsonCatalogSource.cs`.
  Precedence (verified `LocalJsonCatalogSource.cs:31-52`):
  1. `Resources.Load<TextAsset>("Data/Canonical/<name>")` (extension stripped) — synchronous
     on ALL platforms **including WebGL**. Non-empty → returned. **Resources WINS.**
  2. Desktop/editor fallback: `File.ReadAllText(Application.streamingAssetsPath + rel)`,
     wrapped in `Guard.Try` (WebGL has no filesystem — never reached there).
- **The sync rule:** each catalog lives in TWO copies —
  `Assets/Resources/Data/Canonical/` (WebGL-safe, wins at load) and
  `Assets/StreamingAssets/Data/Canonical/` (desktop fallback + authoring source). Keep them
  byte-identical **except the two curation-exempt gear files (§5)**. The rule is now
  ENFORCED, not just documented — see §3 `DataWebRegression`.

### Copy inventory (counted 2026-08-02)
| Root | JSON files | Role |
|---|---|---|
| `Assets/Resources/Data/Canonical/` | **65** (57 top-level + 8 in subdirs) | runtime winner, WebGL surface |
| `Assets/StreamingAssets/Data/Canonical/` | **65** (57 top-level + 8 in subdirs) | desktop fallback + source |
| `Assets/Data/Canonical/` | **2** (`armor.json` 2.9KB, `weapons.json` 20KB) | **orphan third copy — §7.1** |

Subdirs (mirrored 1:1 in both roots): `dialogue/dialogues.json`,
`dungeon-graphs/dg_starter_loop.json`, `dungeon-layouts/{d4_sunken_crypt_spine,
demo_branching_kit, dg_starter_loop, rooms-catalog}.json`, `dungeons/healers-cottage.json`,
`tutorial/tutorial-steps.json`.

Root asymmetries (all deliberate, all oracle-covered or flagged):
- **Resources-only (3):** `ad-creatives.json`, `ad-placements.json`, `widget-params.json`
  (337KB — the largest file in the area; see §4 + §7.3).
- **StreamingAssets-only (3):** `skr_store.json`, `skr_staking.json`,
  `battle_monthly_packs.sample.json` — read on a StreamingAssets-DIRECT path, excluded from
  the dual-copy law by design (`DataWebRegression.cs:101-105` `IsNonDualCopyByDesign`:
  `skr_*` / `battle_*` / `*.sample.json`; same exclusion in `CoreDataHubRegression.cs`).

**Live drift status (diffed 2026-08-02, BOM/CRLF-normalized):** exactly **2 of 62 paired
files drift** — `weapons.json` (S=267,125B vs R=58,492B) and `armor.json` (S=20,130B vs
R=15,555B). Both are the **deliberate WO-747 gear-curation exemption (§5)**. Everything
else is byte-parity green, including all 8 subdir files. The "6 StreamingAssets-only
WebGL-broken catalogs" of the old doc (enemy-roles/towers/walls/realm-map/heart/audio-mix)
are ALL mirrored — that risk is closed and pinned (§3.1 check 2b).

---

## 2. PER-CATALOG INVENTORY (the actual set, 2026-08-02)

`version` read from the Resources copy (the runtime winner). "Consumers" = every `.cs`
holding the `"Data/Canonical/<file>"` literal (grep-verified); Editor-assembly consumers
marked *(ed)*. Oracle column = the check(s) beyond the blanket §3 gates.

### 2.1 Top-level catalogs

| File | v | Consumers | Oracle / notes |
|---|---|---|---|
| `abilities.json` | 2 | `Village/Hero/AbilityCatalog.cs`, `Onboarding/HeroCatalog.cs`, `Data/Tests/AbilityCatalogTest.cs` | Q/W/E/R per class + swap-pool + universal pool. **VFX keys:** rows carry `vfxCast` / `vfxProjectile` / `vfxImpact` / `vfxResidual` — each value is a **verbatim `HovlVfxCatalog` key** (§6). Includes keys on the extended-pool rows (e.g. `suppressing-volley` line 102, `thunderbolt` line 104) — the pool rows not currently slotted still carry live keys. v2 added optional `castAnim` (cast-anim keyword decoupled from slot). |
| `accessories.json` | 1 | `Village/Hero/GearCatalog.cs` | rings/amulets/gems band for the jeweler shelf; in byte-parity (NOT curation-exempt). |
| `ad-creatives.json` | 1 | **NONE** | Resources-only. Data-first ad-creative TEMPLATE table for a thin `AdCreativeGenerator` interpreter (`WORK_ORDER_ad_generator.md` §B) — **interpreter not built**; §7.3. |
| `ad-placements.json` | 1 | **NONE** | Resources-only. Rewarded-ad placement/reward table for `AdGateService` over the `RewardedAdManager` seam — **interpreter not built**; §7.3. |
| `armor.json` | **2 (R) / 1 (S)** | `Village/Hero/GearCatalog.cs`, `Village/Hero/HeroBodySwapper.cs`; *(ed)* GearCaster/Generator/IconRenderer/CurationExporter | **Curation-exempt (§5).** Version intentionally diverges across copies (`DataWebRegression.cs:376-384`). |
| `audio-mix.json` | 1 | **NONE via CanonicalJson** | Music mix config; the live audio system is CODE tables (`_Modules/Audio/MusicTrack.cs:114` static registry port of audio-mix-spec §2). Mirrored + pinned (§3.1 2b) but currently data-inert; §7.4. |
| `barracks.json` | 1 | `Village/Troops/TroopStatResolver.cs`, `Village/Troops/Data/BarracksData.cs` | training/queue config. |
| `build-categories.json` | 2 | `Village/Catalog/BuildCategoryRegistry.cs` | build-menu carousel category bands. |
| `building-tiers.json` | 5 | `Core/State/BuildingTierCatalog.cs` | CoC-style tier/upgrade table. |
| `buildings.json` | 2 | `Village/Buildings/BuildingCatalog.cs`, `Village/Buildings/CrystalMine.cs`; *(ed)* `CrystalProductionRegression` | 5 gameplay buildings; `displayName` is a **canon-strings KEY, not a literal** (its `_authoringNotes.displayName`). |
| `canon-strings.json` | — | `Village/VillageStrings.cs`, `Onboarding/CanonStrings.cs`; *(ed)* `VfxAuraDifferentiationRegression` | Versionless-by-design (`DataWebRegression.cs:90-97`). Proper-noun canon incl. **`theHeartTagline`: "The living core of Elarion — if it falls, the village falls."** Elarion/Avalon/Keeper names — never paraphrase (its `_comment`). |
| `chat-phrases.json` | 1 | `Core/Services/ChatPhraseCatalog.cs` | NPC chatter pools. |
| `concept-icons.json` | 1 | `Core/UI/ConceptIconResolver.cs` | concept→icon map for code-built UI. |
| `consumable-recipes.json` | 1 | `Village/Items/ConsumableCraftingCatalog.cs` | |
| `consumables.json` | 1 | `Village/Items/ConsumableCatalog.cs` | |
| `cosmetics.json` | 1 | `Cosmetics/CosmeticCatalog.cs`; *(ed)* `GlimmerEconomyRegression`, `EconomyMetaCatalogRegression` | |
| `crafting-recipes.json` | 2 | `Village/Crafting/CraftingRecipeCatalog.cs`, `Dungeons/Crafting/CraftingData.cs` | forge/pedestal authoring. |
| `daily-quests.json` | 2 | `Core/Quests/DailyQuests.cs`, HUD `DailyQuestVM` (via instances) | 3 slots + weighted `templates[]`. **Template contract: `label` carries a literal `{target}` token** (e.g. `"Build {target} defensive towers"`, `target: 4`). The substitution lives in the VM: `DailyQuestVM.ResolveLabel` (`_Modules/HUD/DailyQuestVM.cs:213-216`) does `Label.Replace("{target}", Target)` — **committed** (mvvm-E `fc0c1d5e`), no longer in flight. Any new label surface MUST route through ResolveLabel or re-implement the token replace. |
| `damage-states.json` | 1 | `Village/Vfx/StructureDamageVisuals.cs`; *(ed)* `BuildEconomyRegression` | structure damage-tier visuals. |
| `echoes-balance.json` | 1 | `Village/Harvest/EchoBalanceCatalog.cs`; *(ed)* `EchoSpecializationRegression` | WO-738 Echo tuning knobs ONLY (maxLevel 8, lane-match bonus 0.75, per-echo base rates). **Identity stays in the `EchoRosterCatalog` CODE table; only numbers live here** (its `_authoringNotes`). Owner re-tunes with no recompile. |
| `en.json` | — | `Village/VillageStrings.cs`, `Onboarding/CanonStrings.cs`, `Onboarding/OnboardingFlow.cs`; *(ed)* `LocalizationBuilder` | Versionless-by-design. UI/intro strings. |
| `enemies.json` | **4** | `Village/Waves/WaveData.cs`, `Village/Waves/WaveCompositionBuilder.cs`, `Village/World/WildlandsRoster.cs`; *(ed)* `RegressionSuite` | Two families in one codex (hollow + orc). Schema: `family` / `role` / `spawn` / `ai` / `modelKey` (+`_schemaNotes` block is accurate). Wave-data `type` and smart composition both key into `id`. |
| `enemy-roles.json` | 1 | **NONE via CanonicalJson** — `Core/Enemies/EnemyTaxonomy.cs:79` references its role-token vocabulary in a doc-comment only | Mirrored + pinned; currently data-inert; §7.4. |
| `garrison-recipes.json` | — | `Core/Data/GarrisonRecipeCatalog.cs`; *(ed)* `GarrisonSceneBuilder`, `EnemyStrongholdBuilder`, `CoreCatalogRegression` | Versionless-by-design. |
| `gear-levels.json` | 1 | `Village/Hero/GearProgression.cs`; *(ed)* `GearLevelsRegression` | |
| `gear-recipes.json` | 1 | `Village/Crafting/GearCraftingRecipeCatalog.cs` | |
| `guide-content.json` | 1 | `Village/UI/Guide/GuideContentCatalog.cs` | in-game guide pages (27KB). |
| `heart.json` | 1 | **NONE via CanonicalJson** | Heart phase thresholds; mirrored + pinned; currently data-inert; §7.4. |
| `hero-talents.json` | 2 | `Village/Talents/HeroTalentCatalog.cs`; *(ed)* `TalentStrategyRegression` | 3 trees + tier costs (33KB). **2026-08-06 note (`04d375c3`): the FILE IS UNTOUCHED (md5 unchanged) — but its ranger + mage trees had NEVER been audited.** `TalentStrategyRegression` hardcoded `HiddenTrees = {ranger, mage}` from the `ff.knightonly` era and was not updated at the 2026-08-05 unlock, so guard G3 (no dead talent nodes) silently skipped both entire trees while players could reach them. Emptying that set surfaced **31 pre-existing dead nodes across 40 player-reachable talents** (17 ranger + 14 mage); knight's 32 and the 9 shared are green. They are held as a dated **WO-910 ratcheted baseline** (`KnownDeadNodeBaseline`) — new dead debt FAILS, and a baseline id that STOPS reporting dead also FAILS. **`hidden: true` per node is now a real lever** (`HeroTalentNodeDef.Hidden` had ZERO runtime readers before this and is now wired into both `HeroSkillTreeVM.Rebuild` loops) — **no node sets it today**, and setting one is the OWNER's call. **WO-910 READY FOR OWNER RULING.** **2026-08-16 SHAPE LAW (owner ruling, `TalentTreeShapeRegression`):** every tree — common AND specialty — starts from **at most THREE simple, cheapest, root nodes on its bottom row** and branches **wider** as it rises. Rows now: knight `3/7/8/7/7` (32), ranger `3/5/6/6` (20), mage `3/6/6/5` (20), shared `3/4/4` (11). **Ids, names, tiers, slots, costs, icons and effects are UNCHANGED** — the reshape moved `x`/`y` and re-pointed `prerequisites` only, so no save-side unlock is orphaned. **Every node in all three trees now carries an authored `x`/`y`** (ranger/mage had NONE before, so `ResolveGraphNorms`' fallback — not the designer — decided their shape). `x`/`y` are an **ordering hint** consumed by `HeroSkillTreePanelMvvm.SolveGraphLatticePx`, not shipped geometry; **y ascends downward, so a prerequisite always carries a LARGER y than its child.** Correction to the line above: **two shared nodes DO set `hidden: true`** (`shared.n3` Wisdom Surge, `shared.n4` Battle Instinct) — the "no node sets it today" claim was already stale; the new oracle also pins that no VISIBLE node depends on a hidden one. |
| `hud-areas.json` | **1** | `HUD/Kit/HudAreasConfig.cs`; *(ed)* `ObsidianQueueRegression` | HUD kit occupancy table: `postures[] → areas[] → widgets[]` (calm(town) / battle; WO-609 battle layout). |
| `jeweler-recipes.json` | 1 | `Village/Crafting/JewelerRecipeCatalog.cs` | |
| `loot-tables.json` | 2 | `Village/Items/LootTableCatalog.cs` | |
| `lore-fragments.json` | 1 | `Dungeons/LoreFragments.cs`; *(ed)* `UICaptureLaunch` | |
| `materials.json` | 2 | `Village/Items/MaterialCatalog.cs` | |
| `motion-castings.json` | **3** | **runtime:** `Core/Combat/ActionKeywords.cs`, `Village/Vfx/ActionBundleCatalog.cs`; *(ed)* `MotionCastings.cs`, `MotionCasterWindow`, `HeroLocomotionClipRegression`, `KnightPackageControllerBuilder` (weaponskill wraps) | KEYWORD→ACTION registry: (enemy family \| hero class) × keyword → clip + optional `vfxKey`/`sfxId`. **Owner-pick law (Offset Forge, WO-490): rows with `manual: true` are CANON — never overwritten by any generator/bake.** v3 = WO-750 knight castHeal rebind + skill sfx. Its `_comment` also documents what is NOT here (R-slot atk_slashup is a hardcoded `HeroAnimatorFactory.MocapSpellClips[4]` pick). **Header stale-flag: §7.2.** |
| `packs.json` | 2 | `Wallet/PackCatalog.cs`; *(ed)* `PackGrantRegression`, `PackCosmeticIntegrityRegression`, `MonetizationCovenantRegression`, `EconomyMetaCatalogRegression`, `PackCatalogTest` | store packs (BUG-013 markup-leak guard lives in the integrity test, §3.3). |
| `pets.json` | 1 | `Pets/PetCatalog.cs`, `Onboarding/IntroPetCatalog.cs`; *(ed)* `EconomyMetaCatalogRegression`, `PetCatalogTest` | `pet-skill-trees.json` is **DELETED (2026-07-08)** along with `PetSkillTreeCatalog.cs` — the old doc's row is dead (`EconomyMetaCatalogRegression.cs:27,66,205` records the retirement). |
| `population-milestones.json` | 1 | `Village/Population/PopulationMilestonesCatalog.cs` | |
| `quests.json` | 2 | `Core/Quests/QuestCatalog.cs` | |
| `realm-map.json` | **1** | `Core/World/RealmMapCatalog.cs`; *(ed)* `RealmMapRegression` | region-progression overworld: `regions[]` mirror React `RegionDef` verbatim; `gate` is a discriminated union on `kind` (`bestWave`/`regionCleared`); `state` is DERIVED, never stored; `progressLedger` documents the persisted save shape (not content). Home base row `id: "avalon"`, `title: "Elarion"`. |
| `scene-configs.json` | 1 | `Village/World/SceneConfigCatalog.cs`, `Village/SceneOwnership.cs`, `Core/HubScenes.cs` | |
| `skin.json` | 1 | `Core/Platform/CurrencySkinResolver.cs` | currency skin per platform. |
| `spawn-areas.json` | 1 | `Core/World/SpawnAreaTable.cs` | |
| `stake-rewards.json` | 1 | `Core/Platform/StakeRewardsResolver.cs` | |
| `structures-catalog.json` | **8** *(corrected 2026-08-06; was 6 — `0ac59581` bumped 6→7, `d42e2817` bumped 7→8)* | `Village/Catalog/CatalogBootstrap.cs` (→ CatalogRegistry at startup); *(ed)* `CatalogOrientationBaker`, `StructureHeightAudit`, `RegressionSuite`, `DataRegression`, `BuildEconomyRegression`, `StrategicPlacementRegression`, `SessionRegression`, `DefenseTargetableRegression`, `VfxAuraDifferentiationRegression` | THE build-mode structure table (31KB, 5+ oracles — the most oracle-covered file in the area). **NEW top-level `_heightCadence` key (line 3, `d42e2817`) — the owner's 2026-08-05 height ruling now lives IN THE DATA so the authority travels with the file** (1.25 landmark / 1.2 tower ANCHOR / 1.0 building base / 0.75 siege / 0.35 decoration; `collector_farm` 1.4 is a bounds COMPENSATION, not a cadence value; wall rows deliberately unauthored for save-compat). Per-row `_heightNote` keys carry the exceptions; `StructureFactory`/`RepoProps.heightMul` carry only the summary. Full rationale: `docs/MASTER_CATALOG/village-systems.md` §4 DELTA. **The JSON-failure fallback `CatalogBootstrap.RegisterFallback` is now parity-guarded** against this file by a reflection deep-compare inside `BuildEconomyRegression` (tag `[fallback-parity]`, rides `BUILDECON_OK` — no new suite, no change to the `REGRESSION_OK n/n` count; `21c11327`). Schema law in its `notes` (now line 4): `type/mustSitOn/navSurface/element` are enum NAMES; `visualPrefabPath` Resources-relative; `behaviorId` → `StructureFactory.AttachBehavior` (only DefenseTower/WallSegment/CrystalMine/Gate wired; null = inert); S4 `repo.cost` multi-resource via ResourceLedger; S5 `repo.maxLevel`/`upgradeCost` CoC upgrade sink; v5 **`repo.bakedTwins`** = legacy baked scene-root names a singleton row represents (StructureSingleton standdown/resurface, e.g. lines 493, 552); v6 adds **`npcModel`** (structure-bound NPC mesh: Cleric line 404, Druid line 491, Engineer line 536). |
| `themes.json` | — | `Core/Theme/Theme.cs` | Versionless-by-design. |
| `tower-perks.json` | 1 | `Village/Buildings/Tower.cs` | WC3-style tower perk rows. |
| `towers.json` | 1 | **NONE via CanonicalJson** — `BuildModeController.cs:2119,2148` reference its tier data in comments only | Mirrored + pinned; currently data-inert; §7.4. |
| `troop-upgrades.json` | 1 | `Village/Troops/TroopStatResolver.cs` | |
| `troops.json` | 2 | `Village/Troops/TroopCatalog.cs`, `Village/Troops/TroopDef.cs` | |
| `vendors.json` | **1** | `Village/Hero/VendorRegistry.cs` | WO-598 vendor registry — **each shelf is a QUERY over the item catalogs, never a hardcoded list**: `categories` (catalog bands) + `classFilter: "roster"` (drops items no playable class can use) + `maxReqLevel` tier gate + authored `emptyLine` (never a raw empty grid). `VendorStockContract.AllowedFor` consults it FIRST so legacy shop, MVVM PartyShop, ShopCatalog and the AutoPilot oracle read ONE truth (its `_comment`). Id match: exact then substring. |
| `wallets.json` | 1 | `Wallet/WalletRegistry.cs` + tests; *(ed)* `EconomyMetaCatalogRegression` | |
| `walls.json` | 1 | `Village/Walls/WallTierData.cs`; *(ed)* `WallHeartMitigationRegression` | |
| `waves.json` | **1** | `Village/Waves/WaveData.cs`; *(ed)* `RegressionSuite`, `DataInjector` | **THE INERT-`enemies[]` TRUTH (WO-783 D1, 2026-07-30):** the per-wave `enemies` batch arrays were STRIPPED because they were INERT — `WaveManager.StartWave` runs the WO-362 smart-composition path (`_smartComposition` serialized 1 in both live hubs), so `WaveCompositionBuilder` GENERATES every wave's roster and the authored batches were never released (they sat dead ~4 weeks). **Still live:** `countdownSeconds`, `boss` (enemies.json id, every-6 cadence), `apexBoss` (the kinematic DragonBoss prefab — NOT an enemies.json row), and the `endless` block (cycles waves 4–20 past wave 20, count growth 0.05/wave cap 3.0×). **Do NOT re-add an `enemies` array** — the [wave-authoring] regression FAILS the gate if live-looking batches reappear while smart composition is on (its `_RETIRED_batchFields` note). Authored design intent preserved at `docs/design/WAVE_AUTHORING_REFERENCE_2026-07-30.md`. |
| `weapons.json` | **1** | `Village/Hero/GearCatalog.cs`, `Core/Data/DataInjector.cs`; *(ed)* GearCaster/Generator/IconRenderer/CurationExporter, `GearCatalogTest` | **Curation-exempt (§5).** Resources 58KB curated+authored vs StreamingAssets 267KB full library. |
| `weaponskill-animations.json` | 1 | *(ed)* `KnightPackageControllerBuilder` | editor-consumed controller-bake data. |
| `widget-params.json` | — | `Core/UI/ElarionUiKitObsidian.cs` (loads `"Data/Canonical/widget-params"` — the Resources no-extension form); *(ed)* `PrefabParamExtractor` (the writer) | Resources-only, 337KB, versionless, extractor-generated UI-kit parameter dump; §7.3. |

StreamingAssets-direct (non-dual-copy by design): `skr_store.json`, `skr_staking.json`,
`battle_monthly_packs.sample.json` — consumed by the monetization path +
*(ed)* `MonetizationCovenantRegression` (which derives the convenience-only covenant
from `skr_staking.json`).

### 2.2 Subdirectory catalogs (all mirrored, all oracle-covered per §3.1)

| Path | Consumers |
|---|---|
| `dialogue/dialogues.json` | `Core/Dialogue/DialogueModel.cs` (runtime); *(ed)* `DialogueRegression`, `FtueHonestyRegression` |
| `tutorial/tutorial-steps.json` | `Core/Tutorial/TutorialStepModel.cs`, `Village/BuildMode/BuildModeController.cs` |
| `dungeons/healers-cottage.json` | `Dungeons/DungeonLayout.cs`, `Dungeons/State/DungeonRuntimeState.cs`, `Dungeons/DungeonController.cs` (prefix) |
| `dungeon-layouts/*.json` (4: rooms-catalog + 3 layouts) | `Dungeons/RoomForge/DungeonComposeLayout.cs` (runtime); *(ed)* RoomForge suite (`RoomForgeWindow`, `DungeonBaker`, `DefaultDungeonRoomsBuilder`, `GraphDungeonComposer`), `RoomForgeRegression` |
| `dungeon-graphs/dg_starter_loop.json` | *(ed)* `GraphDungeonComposer` |

---

## 3. THE ORACLES (what proves the law)

### 3.1 `DataWebRegression` — the byte-parity gate (PRIMARY)
`Assets/Editor/Regression/DataWebRegression.cs`, asm `DeNelle.EditorRegression`. Headless,
no-scene. Markers `DATAWEB_OK` / `DATAWEB_FAIL` (FAIL via `Debug.LogError` → break-log).
**Wired into the suite at `DataRegression.cs:295`** (`DataRegression.RunAll`). Five checks:

1. **DUAL-COPY DRIFT** (`:204-242`) — every paired `*.json` (recursive incl. subdirs)
   content-compared, BOM-stripped + CRLF-normalized; EOL/BOM-only diffs are notes, not
   fails. `weapons.json`/`armor.json` skipped → delegated to check 5 (`:214-217`).
2. **WEBGL-BROKEN-BY-OMISSION** (`:246-324`) — (2a) scans every runtime (non-Editor) `.cs`
   for `"Data/Canonical/….json"` literals; each must have a Resources copy on disk
   (prefix literals logged as statically-unresolvable). (2b) **the historically
   StreamingAssets-only six** (`enemy-roles/towers/walls/realm-map/heart/audio-mix`,
   `:121-129`) are pinned by name — un-mirroring any flips red. Plus subdir mirrors
   (CoreDataHub only sweeps top level).
3. **PARSE** (`:328-348`) — every `*.json` under BOTH roots parses via `JToken.Parse`
   (the Resources side — the copy that actually wins — had no parse gate before this).
4. **VERSION** (`:352-398`) — every StreamingAssets top-level catalog must carry
   `version`, except **VersionlessByDesign** = `canon-strings/en/garrison-recipes/themes`
   (`:90-97`); cross-copy version mismatch fails by name; the gear pair is exempt from the
   cross-copy compare (`:376-384`).
5. **GEAR CURATION** (`:416-464`, WO-747) — replaces byte-drift for the gear pair; §5.

### 3.2 `CoreDataHubRegression` — the read-contract gate
`Assets/Editor/Regression/CoreDataHubRegression.cs` (also wired from `DataRegression.RunAll`).
Proves every top-level StreamingAssets catalog (minus `skr_*`/`battle_*`/`*.sample.json`)
**reads NON-EMPTY through the real game path** (`CanonicalJson.Read`) and has its
Resources dual-copy present. Deliberately not duplicated by 3.1 (division of labor stated
in `DataWebRegression.cs:12-14`).

### 3.3 `CanonicalJsonIntegrityTest` — EditMode NUnit (legacy layer)
`Assets/Data/Tests/CanonicalJsonIntegrityTest.cs`. StreamingAssets side only: required
files present + parse + **no stray agent-output markup** (`</content>`, `</invoke>` etc. —
the BUG-013 packs.json leak guard) + tail sanity + version on the historical six
(abilities/buildings/enemies/packs/pets/waves).

### 3.4 Per-catalog domain oracles (beyond parity)
Named in the §2 table per file; the heavy hitters: `RegressionSuite` (structures/enemies/
waves), `BuildEconomyRegression`, `RealmMapRegression`, `MonetizationCovenantRegression`,
`EconomyMetaCatalogRegression`, `TowerProjectileMapRegression` + 
`VfxAuraDifferentiationRegression` (§6), `ObsidianQueueRegression` (hud-areas),
`EchoSpecializationRegression` (echoes-balance), the wave-authoring check (§2 waves row).

---

## 4. NON-CANONICAL `Resources/Data` (outside the dual-copy law)

Top level, verified on disk 2026-08-02: `castle-south-recipe.json` (editor-only —
`CastleWallsFromRecipe`/`CastleHubBuilder` via raw `Resources.Load`+`JsonUtility`, NOT
CanonicalJson), `castle-wall-collider-offsets.json`, `dungeon-kit.json`,
`scene-links.json`, **`orientation-recipes.json` — still JSONL, not JSON** (newline-
delimited records appended by `TowerPlacementRotateMenu`; whole-file parse would fail;
`DataWebRegression` excludes `*.jsonl`-class files and this lives outside the canonical
roots), plus `Upgrades/{Farm,Watchtower}Upgrades.json` (WO-237 spec-era, still unwired)
and `Dungeons/`, `Economy/` subdirs. These are NOT catalogs of record — the canonical
roots are.

---

## 5. THE GEAR CURATION DRIFT (deliberate, byte-exempt — WO-747)

- **Model (verified `DataWebRegression.cs:131-158` + live diff):** the StreamingAssets
  `weapons.json` (267KB, v1) + `armor.json` (20KB, v1) are the **full generated library**
  (GearCasterWindow's browse surface). The Resources copies (58KB / 15.5KB, armor v2) are
  the **runtime truth**: ALL current Resources rows UNION the owner's curated picks —
  the exporter (`Assets/Editor/Catalog/GearCurationExporter.cs`) is **ADDITIVE, never
  drops**, and Resources may hold authored ids that exist ONLY there (class-tier
  progression armor, loot/vendor weapons, e.g. the Flameblade commit `6ef34a84`).
- Byte-identity is therefore **impossible by design** → both files exempt from drift (§3.1
  check 1) and cross-copy version compare (§3.1 check 4).
- **Replacement oracle — `CheckGearCuration`** (`DataWebRegression.cs:416-464`,
  `GEAR_CURATION_OK/FAIL`): (a) every `included:true` id in
  `Assets/Editor/GearCurationPicks.json` (present ✓) + every
  `ReferencedDefaultArmorIds` entry (`blink_armor_{centurion,beasthunter,dragonic,basic1}`
  — the HeroBodySwapper class defaults + SaveIntegrityRegression seed, `:149-158`) must
  resolve in the Resources catalog; (b) every Resources row well-formed (non-empty id, no
  duplicate ids — a dup = ambiguous GearCatalog lookup).
- **Curation flow:** owner picks in GearCasterWindow → `GearCurationPicks.json` →
  `GearCurationExporter` merges into Resources → gate proves curation reached runtime.

---

## 6. THE VFX KEY TRIO (owner-tag law: keys verbatim, never substituted)

Three files, one contract — a string **key** authored by the owner resolves to a Hovl
prefab at runtime; agents map keys VERBATIM and never creative-pick a substitute
(memory `vfx-map-owner-tags-no-creative-pick`):

| File | Role |
|---|---|
| `Assets/Editor/VfxManualPicks.json` | **The owner's canon picks** — rows `{key, prefabPath, isLoop, scale, manual:true}`. `manual:true` = CANON, merged as an OVERLAY that beats the generator's automatic map (`HovlVfxCatalogGenerator.cs:52` ManualPicksPath; merge at `:327-345`). Written back by the VfxCaster save path (`VfxCasterWindow.cs:1174-1182` → `WriteManualPick`). |
| `Assets/Resources/VFX/HovlVfxCatalog.asset` | **The runtime resolver** — generated ScriptableObject (`DeNelle.Village.HovlVfxCatalog`) built by `Assets/Editor/HovlVfxCatalogGenerator.cs` (batchmode `DeNelle.Editor.HovlVfxCatalogGenerator.Generate`, asset path at `:47`): automatic Map rows + the manual-picks overlay. `VFXManager.Hovl` (`_Modules/Village/Vfx/VFXManager.Hovl.cs:23-24`) resolves key → prefab + pool at runtime. |
| `Assets/Editor/VfxCasterLibraryIndex.json` | **The browse index** — a generated scan of every VFX prefab (`scannedUtc: 2026-07-26`, `count: 2871`, rows `{pack,key,catalogued,path}`) consumed by `VfxCasterWindow` (`:35`) so the owner tags keys against the full library. Regenerable; not shipped. |

**Key producers (where owner-tagged keys live):** `abilities.json`
`vfxCast/vfxProjectile/vfxImpact/vfxResidual` fields; `motion-castings.json` `vfxKey`
(validated against catalog keys by `MotionCasterWindow.cs:932-944` — free-text falls back
UNVALIDATED with a warning); tower rows (per-tier projectile keys).
**Oracles:** `TowerProjectileMapRegression` (every tower-referenced projectile key must be
catalogued or it "fires a bare pellet", `:88`; wired at `DataRegression.cs:361`) and
`VfxAuraDifferentiationRegression` (aura keys must resolve in Map or ManualPicks;
`UpgradeStructureComplete_Aura` must be `isLoop:false`, `:96`).

### DELTA 2026-08-06 - `isLoop` is DERIVED DATA now, and `VFXType` serialises by ORDINAL

*Two contract changes landed in the 2026-08-05 VFX wave. Both change how this data may be
edited, so they belong here and not only in the VFX docs.*

1. **`isLoop` is no longer a hand-set field (`bd532d5b`).** It had been a **sticky manual UI
   checkbox** that `VfxCasterWindow` FORCE-SET true for any row tagged Projectile or Aura;
   nothing ever read the prefab's actual emission. **95 of 135 Hovl rows carried `isLoop:1`**,
   including every `PP_*Impacts` and `PP_MuzzleFlash` — all single bursts at t=0. Both catalog
   generators now **DERIVE** it from the art, and the rule is stated **once, in one place**
   (`Assets/Editor/Regression/VfxLoopFlagRegression.cs`, the shared resolver every other
   builder calls): `main.loop` AND a positive rate over time or distance, with emission
   enabled; **the authority is the ROOT system UNLESS the root cannot emit**, in which case it
   falls through to the first system that can. **53 of 122 picks were wrong.** The
   `VfxCasterWindow` checkbox is now **read-only and derived**, and the role-based force-set is
   deleted. New marker `VFX_LOOPFLAG_OK`. **Do not hand-author `isLoop` in
   `VfxManualPicks.json` — fix the ART, or pin the row (next point).**
2. **STANDING OWNER RULINGS OUTRANK THE DERIVATION.** Deriving promoted some genuinely
   continuous prefabs TO loops — one of them, the upgrade fireworks, is played
   fire-and-forget. The owner had already reported "perma-fireworks" and ruled it one-shot. So
   **the prefab is the authority on what the art DOES, not on what the game SHOULD DO**:
   standing owner rulings are **PINNED in a table with their reason**, and **every consumer
   resolves through ONE method**, so a pin cannot be honoured in one place and forgotten in
   another. (This is why `VfxAuraDifferentiationRegression`'s
   `UpgradeStructureComplete_Aura`/`isLoop:false` assertion still holds.)
3. **`VFXCatalog.asset` serialises `VFXType` by ORDINAL, not by name (`0011b8ba`).** 16 new
   values were **APPENDED after `Boss_FireBreath`**, and append-only is precisely what makes
   that safe: **an insert anywhere above would silently re-point every row below it at the
   wrong art.** Verified after the append: `Boss_FireBreath` still reads `Type: 79`.
   **NEVER insert, reorder or delete a `VFXType` value — append only.**
4. **A catalog ROW written by a builder alone is silently DROPPED (`a12c6d22`).**
   `VFXCatalogGenerator.Build()` does `entries.arraySize = rows.Count`, so map entries MUST
   land in `VFXCatalogGenerator` alongside the rows — otherwise the next regenerate erases them
   and the effect falls back to something that still looks like it works.

---

## 7. RISK LEDGER (2026-08-02, priority order)

1. **The orphan third copy is ALIVE and being fed.** `Assets/Data/Canonical/{armor,weapons}.json`
   still exists, is loaded by NOTHING (grep: zero code paths reference `Assets/Data/Canonical`),
   yet its `weapons.json` was **updated in gear commit `6ef34a84` (2026-07-24)** — someone/
   something is maintaining a copy nobody reads. Drift hazard + wasted effort; delete it or
   document a source-of-truth role. (Old-doc flag confirmed still true, now worse.)
2. **`motion-castings.json`'s own `_comment` lies about its consumers.** It still says
   "Editor-consumed … in V1 — no Resources mirror until a runtime reader exists" — but the
   Resources mirror EXISTS (in byte-parity) and TWO runtime readers exist
   (`Core/Combat/ActionKeywords.cs`, `Village/Vfx/ActionBundleCatalog.cs`). Trusting the
   comment would mis-scope any motion-castings change to "editor-only". Fix the comment.
3. **Resources-ONLY files escape the drift + version oracles.** §3.1 checks 1/2/4 iterate
   the *StreamingAssets* side, so `ad-creatives.json`, `ad-placements.json`,
   `widget-params.json` get only the parse check (3). Compounding: the two ad files have
   **NO consumer at all** — they are spec-era data for `AdGateService`/`AdCreativeGenerator`
   interpreters (`WORK_ORDER_ad_generator.md`) that are **not built**. Per §13 pipeline law
   that is NEW-FEATURE territory — do not RCA-"fix" the unbuilt; either build the
   interpreters or banner the WO.
4. **Four of the pinned six are mirrored but data-inert.** `audio-mix.json`, `heart.json`,
   `enemy-roles.json`, `towers.json` have NO CanonicalJson consumer (audio is a CODE
   registry at `MusicTrack.cs:114`; the others are referenced only in comments). The
   WebGL-null risk is closed, but a change to these files changes NOTHING at runtime —
   don't "tune" them expecting effect; wire a reader first (or retire them).
5. **The `{target}` label contract is easy to re-break.** `daily-quests.json` labels ship
   raw `{target}` tokens; the ONLY substitution point is `DailyQuestVM.ResolveLabel`
   (`DailyQuestVM.cs:213-216`). Any new surface that renders `DailyQuestInstance.Label`
   directly (toast, tooltip, web HUD) will show literal `{target}` — route through the VM
   or replicate the replace. No oracle asserts token substitution today.
6. **Version divergence is legal for exactly two files** (`armor` v2-R/v1-S, `weapons`) —
   for everything else a cross-copy version mismatch fails the gate by name. When bumping
   any catalog version, bump BOTH copies in the same edit.
7. **Never re-add `waves.json` `enemies[]` batches** — the wave-authoring regression fails
   the gate if live-looking batches reappear while smart composition is on (§2 waves row).
   Authoring intent goes to `docs/design/WAVE_AUTHORING_REFERENCE_2026-07-30.md`, not here.
8. **Dead rows from the old doc — do not resurrect:** `pet-skill-trees.json` +
   `PetSkillTreeCatalog.cs` deleted 2026-07-08 (oracle retirement recorded at
   `EconomyMetaCatalogRegression.cs:27`); the "6 WebGL-broken StreamingAssets-only
   catalogs" flag is closed (all mirrored + pinned); `armor/weapons` "no version field"
   is stale (both carry `version` since ≤2026-07-12, `DataWebRegression.cs:54-57`).
