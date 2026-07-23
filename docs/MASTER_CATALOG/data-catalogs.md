# MASTER CATALOG — data-catalogs

> ⚠ **STALE 2026-07-22 — corrections (live anchor `CANON_GROUND_TRUTH_2026-07-22.md`):** there are now **~70 Resources + ~72 StreamingAssets canonical files** (not 26/32); the "6 WebGL-broken StreamingAssets-only" catalogs are all MIRRORED now (that risk is closed, DATAWEB pins them); ~40 catalogs this file never listed now exist. Body below is the 2026-06-12 point-in-time map; trust these lines + the anchor over it.

Area: the JSON catalogs under `Assets/Resources/Data`, `Assets/StreamingAssets/Data`,
`Assets/Data`, plus the single WebGL-safe loader and the ~30 typed catalog classes that
consume them. **Verified by reading the actual JSON + .cs files** (not comments).

---

## 1. THE LOADER (the dual/triple-copy sync rule)

### `CanonicalJson` — `Assets/_Modules/Core/Data/CanonicalJson.cs`
- Namespace `DeNelle.Core`, asmdef `DeNelle.Core`. Static class. **The single read path** for every canonical catalog.
- `public static string Read(string relativePath)` — `relativePath` is StreamingAssets-relative (e.g. `"Data/Canonical/abilities.json"`). Returns raw JSON text or `null`.
  - **Step 1 (wins on ALL platforms incl. WebGL):** strips `.json`, calls `Resources.Load<TextAsset>("Data/Canonical/<name>")`. If non-empty, returns it. → **The `Assets/Resources/Data/Canonical/` copy is authoritative at runtime.**
  - **Step 2 (desktop/editor only):** `File.ReadAllText(Application.streamingAssetsPath + relativePath)`, wrapped in try/catch (WebGL has no filesystem → throws, swallowed).
- **Sync rule (THE law for this area):** canonical JSON lives in TWO synced copies — `Assets/Resources/Data/Canonical/` (WebGL-safe, **wins**) and `Assets/StreamingAssets/Data/Canonical/` (desktop fallback + source/superset). **Resources wins; keep them in sync.** Comment in file (lines 13-18) matches code — accurate.

### Copy inventory (verified by `comm`/`ls`)
| Root | Canonical files | Role |
|---|---|---|
| `Assets/Resources/Data/Canonical/` | **26** json | WebGL-safe, **wins at load** |
| `Assets/StreamingAssets/Data/Canonical/` | **32** json | desktop fallback + source; **superset** |
| `Assets/Data/Canonical/` | **2** json (armor, weapons) | partial stale duplicate — see FLAGS |

**StreamingAssets-ONLY (6, no Resources copy → desktop-only, WebGL would get null):**
`audio-mix.json`, `enemy-roles.json`, `heart.json`, `realm-map.json`, `towers.json`, `walls.json`.

Non-Canonical data also under `Assets/Resources/Data/`:
`castle-south-recipe.json`, `orientation-recipes.json`, `Upgrades/FarmUpgrades.json`, `Upgrades/WatchtowerUpgrades.json`.

### Integrity test — `Assets/Data/Tests/CanonicalJsonIntegrityTest.cs`
- Namespace `DeNelle.Data.Tests` (EditMode NUnit). Scans `StreamingAssets/Data/Canonical/**` (NOT Resources).
- Checks: required files present+non-empty; every file parses (Newtonsoft `JToken.Parse`); **no stray agent-output markup** (`</content>`, `</invoke>`, `</antml`, `<parameter name=` etc. — guards BUG-013 packs.json leak); tail must end in `}`/`]`; 6 "versioned" files (abilities/buildings/enemies/packs/pets/waves) must carry positive-int `version`. **Live/wired.**

---

## 2. CANONICAL JSON CATALOGS (Resources copy = authoritative)

Entry counts verified by parsing. Schema = first-entry key set.

| File | Top shape | Count | Entry schema (keys) | version |
|---|---|---|---|---|
| `abilities.json` | `classes{mage,knight,ranger}` | 3 classes × 4 abilities (q/w/e/r) | ability: slot,key,name,description,icon,color,effect,cooldown,manaCost,damage,range(,freeze) | 1 |
| `weapons.json` | `weapons[]` | **16** | id,name,icon,job,rarity,damageMult,req,buyWood,buyCrystals | none |
| `armor.json` | `armor[]` | **5** | id,name,icon,job,rarity,defense,hpBonus,req,buyWood,buyFood,buyIron | none |
| `enemies.json` | `enemies[]` | **9** | id,name,displayName,family,role,spawn,modelKey,ai,hp,moveSpeed,contactDamage,attackInterval,height,boss,flavor | 2 |
| `buildings.json` | `buildings[]` | **7** | id,type,displayName,descriptionKey,hp,maxHp,crystalCost,model,footprint,buildMenuOrder,isUpgradable,upgradeType | 1 |
| `garrison-recipes.json` | `recipes[]` | **4** | id,kind,size,theme,lighting,enemies,levelRange,threat,props,element | none |
| `gear-recipes.json` | `recipes[]` | **8** | id,displayName,outputGearId,outputKind,tier,cost,components,requiresQuestId,saga | 1 |
| `crafting-recipes.json` | ingredients[3]+recipes[1]+ingredientPlacements[3]+pedestal{} | 1 recipe, 3 ingredients | (forge/pedestal crafting authoring) | 1 |
| `consumables.json` | `consumables[]` | **4** | id,displayName,kind,effect,magnitude,duration,usableInFight,glyph | 1 |
| `consumable-recipes.json` | `recipes[]` | **4** | (consumable crafting) | 1 |
| `loot-tables.json` | `tables[]`+defaults | **2** tables | id,source,drops | 1 |
| `packs.json` | `packs[]` | **5** | sku,tier,name,tagline,theme,pricing,contents,packExclusiveCosmetic | 1 |
| `cosmetics.json` | `items[]` | **12** | id,category,appliesTo,displayName,description,glimmerCost,unlockMethod,previewColor | 1 |
| `pets.json` | `pets[]` | **3** | id,species,name,element,archetype,tint,glowColor,particleColor,huntSpeed,attackRange,attackCooldown,slotIndex,bondRanks | 1 |
| `pet-skill-trees.json` | `trees{}` | **11** trees | per-tree skill nodes | 2 |
| `hero-talents.json` | `trees{}`+tierCosts | **3** trees | talent trees w/ respec+tier costs | 1 |
| `waves.json` | `waves[]` | **4** | waveId,name,countdownSeconds,enemies (each entry has `_comment`) | 1 |
| `quests.json` | `quests[]` | **24** | id,title,stages | 2 |
| `daily-quests.json` | slots[3]+templates[19] | 19 templates, 3 slots | daily quest pool + reroll config | 1 |
| `structures-catalog.json` | `entries[]` | **18** | id,displayName,type,kind,visualPrefabPath,repo,orientation | 2 |
| `lore-fragments.json` | `fragments[]` | **6** | id,kind,speaker,title,placeholder,body | 1 |
| `themes.json` | `themes{}`+default | **7** themes | palette/theme defs | none |
| `chat-phrases.json` | categories[4]+phrases[24] | 24 phrases | NPC chatter | 1 |
| `wallets.json` | rewardsDistributor{}+devnetPurchaseRecipient{} | (web3 wallet config) | — | 1 |
| `canon-strings.json` | flat string map | ~scalar keys (elarion/heart canon) | string→string | none |
| `en.json` | flat string map | UI/intro localization strings | string→string | none |
| `dungeons/healers-cottage.json` | dungeon layout | 1 | (room layout for healer's cottage dungeon) | — |

### StreamingAssets-ONLY canonical (NO Resources copy — desktop fallback only)
| File | Top shape | Count | Entry schema |
|---|---|---|---|
| `enemy-roles.json` | roles{9}+creatures[25] | 25 creatures, 9 roles | id,display,role,hpScale,atkScale,speedScale,behavior |
| `towers.json` | zones[3]+levels[4] | 3 zones, 4 levels | id,name,color,glow,sectorAngleRadians |
| `walls.json` | tiers[4] | 4 | level,name,emoji,effect,heartDamageMultiplier,targetHeight,meshStraight,meshGate |
| `realm-map.json` | regions[5]+homeBase+withering+progressLedger | 5 regions | id,title,description,biome,propSet,waveCount,elementBias,gate,clearReward,mapPoint,mapOrder,dungeonRegion,adjacency |
| `heart.json` | phases[3] | 3 | id,hpThreshold,label,description |
| `audio-mix.json` | tracks{6}+transitions[10]+volumeNudges[5]+accessibility | 6 tracks, 10 transitions | music mix config |

---

## 3. NON-CANONICAL DATA (Assets/Resources/Data)

| File | Shape | Count | Notes |
|---|---|---|---|
| `castle-south-recipe.json` | `{pieces[], parentPos[3], parentRot[3]}` | 4 pieces | Captured south-side wall/gate offsets. Loaded via `Resources.Load<TextAsset>("Data/castle-south-recipe")` + `JsonUtility.FromJson` (NOT CanonicalJson) by `Assets/Editor/CastleWallsFromRecipe.cs` & `CastleHubBuilder.cs`. Editor-only; written by `Assets/Editor/CastleOffsetCapture.cs`. |
| `orientation-recipes.json` | **JSONL** (5 newline-delimited records, NOT a JSON document) | 5 lines | Each: `{id,euler[3],offset[3],scale}`. Appended by `TowerPlacementRotateMenu.cs` (line ~922, `File.AppendAllText`). Prop-orientation memory. **Parses per-line, not as one object** — a `JToken.Parse` of the whole file would fail (it's not in the integrity-test dir, so OK). |
| `Upgrades/FarmUpgrades.json` | `{upgrades[3]}` | 3 | id,title,description,woodCost,stoneCost,ironCost,crystalCost,boosts |
| `Upgrades/WatchtowerUpgrades.json` | `{upgrades[3]}` | 3 | same schema |

---

## 4. CONSUMER CATALOG CLASSES (each Read()s one file via CanonicalJson)

All resolve their file via `CanonicalJson.Read("Data/Canonical/<x>.json")`. Pattern: a `const string ...RelativePath`, lazy `Load()`, `JsonConvert.DeserializeObject<T>` (Newtonsoft), in-memory cache. **All wired/live** unless noted.

| Class | File path | Reads | asmdef/ns |
|---|---|---|---|
| `AbilityCatalog` | `_Modules/Village/Hero/AbilityCatalog.cs` | abilities.json | DeNelle.Village |
| `GearCatalog` | `_Modules/Village/Hero/GearCatalog.cs` | weapons.json + armor.json | DeNelle.Village |
| `BuildingCatalog` | `_Modules/Village/Buildings/BuildingCatalog.cs` | buildings.json | DeNelle.Village |
| `GarrisonRecipeCatalog` | `_Modules/Core/Data/GarrisonRecipeCatalog.cs` | garrison-recipes.json | DeNelle.Core |
| `GearCraftingRecipeCatalog` | `_Modules/Village/Crafting/GearCraftingRecipeCatalog.cs` | gear-recipes.json | DeNelle.Village |
| `CraftingRecipeCatalog` | `_Modules/Village/Crafting/CraftingRecipeCatalog.cs` | crafting-recipes.json | DeNelle.Village |
| `ConsumableCatalog` | `_Modules/Village/Items/ConsumableCatalog.cs` | consumables.json | DeNelle.Village |
| `ConsumableCraftingCatalog` | `_Modules/Village/Items/ConsumableCraftingCatalog.cs` | consumable-recipes.json | DeNelle.Village |
| `LootTableCatalog` | `_Modules/Village/Items/LootTableCatalog.cs` | loot-tables.json | DeNelle.Village |
| `PackCatalog` | `_Modules/Wallet/PackCatalog.cs` | packs.json | DeNelle.Wallet |
| `WalletRegistry` | `_Modules/Wallet/WalletRegistry.cs` | wallets.json | DeNelle.Wallet |
| `CosmeticCatalog` | `_Modules/Cosmetics/CosmeticCatalog.cs` | cosmetics.json | DeNelle.Cosmetics |
| `PetCatalog` | `_Modules/Pets/PetCatalog.cs` | pets.json | DeNelle.Pets |
| `PetSkillTreeCatalog` | `_Modules/Pets/PetSkillTreeCatalog.cs` | pet-skill-trees.json | DeNelle.Pets |
| `HeroTalentCatalog` | `_Modules/Village/Talents/HeroTalentCatalog.cs` | hero-talents.json | DeNelle.Village |
| `WaveData` | `_Modules/Village/Waves/WaveData.cs` | waves.json | DeNelle.Village |
| `QuestCatalog` | `_Modules/Core/Quests/QuestCatalog.cs` | quests.json | DeNelle.Core |
| `DailyQuests` | `_Modules/Core/Quests/DailyQuests.cs` | daily-quests.json | DeNelle.Core |
| `CatalogBootstrap` | `_Modules/Village/Catalog/CatalogBootstrap.cs` | structures-catalog.json | DeNelle.Village |
| `Theme` | `_Modules/Core/Theme/Theme.cs` | themes.json | DeNelle.Core |
| `ChatPhraseCatalog` | `_Modules/Core/Services/ChatPhraseCatalog.cs` | chat-phrases.json | DeNelle.Core |
| `VillageStrings` | `_Modules/Village/VillageStrings.cs` | en.json (and/or canon) | DeNelle.Village |
| `CanonStrings` | `_Modules/Onboarding/CanonStrings.cs` | canon-strings.json | DeNelle.Onboarding |
| `IntroPetCatalog` | `_Modules/Onboarding/IntroPetCatalog.cs` | pets.json | DeNelle.Onboarding |
| `LoreFragments` | `_Modules/Dungeons/LoreFragments.cs` | lore-fragments.json | DeNelle.Dungeons |
| `DungeonLayout` | `_Modules/Dungeons/DungeonLayout.cs` | dungeons/*.json | DeNelle.Dungeons |
| `CraftingData` | `_Modules/Dungeons/Crafting/CraftingData.cs` | crafting recipes | DeNelle.Dungeons |
| `DataInjector` | `_Modules/Core/Data/DataInjector.cs` | (generic loot/table loader) | DeNelle.Core |

Notes:
- `DataInjector.cs` header comment (line 4) flags that "monetization catalogs all hand-roll today (CanonicalJson.Read → JsonConvert...)" — accurate description of the per-catalog pattern.
- `enemy-roles.json`, `towers.json`, `walls.json`, `realm-map.json`, `heart.json`, `audio-mix.json` have NO Resources copy; any consumer of them returns `null` on WebGL (desktop/editor only). Verify a consumer exists before assuming live.

---

## 5. FLAGS

1. **`Assets/Data/Canonical/{armor,weapons}.json` is a stale/orphan third copy.** Only `armor.json` + `weapons.json` exist there. `GearCatalog` reads `Data/Canonical/weapons.json` **via CanonicalJson** → resolves to the **Resources** copy, NOT this one. This `Assets/Data/Canonical/` pair is dead weight and a drift hazard (a 3rd copy nobody loads). Either delete or fold into a documented source-of-truth.

2. **6 StreamingAssets-only catalogs are WebGL-broken-by-omission.** `enemy-roles.json`, `towers.json`, `walls.json`, `realm-map.json`, `heart.json`, `audio-mix.json` have no Resources copy → `CanonicalJson.Read` returns `null` in WebGL (Resources miss + no filesystem). This is the exact failure class CanonicalJson exists to prevent. If any are needed in the web build, they must be mirrored to `Assets/Resources/Data/Canonical/`.

3. **`Upgrades/FarmUpgrades.json` + `WatchtowerUpgrades.json` are orphaned spec-era data.** Referenced ONLY in `WORK_ORDER_237_building_upgrade_panel.md` (`Resources.Load<TextAsset>("Data/Upgrades/{name}Upgrades")`), NOT in any live `.cs`. The shipped upgrade flow uses `BuildingCatalog.UpgradeType` + `ResourceBuildingState`/`ResourceBuildingProgression` (DialogueCommandBridge), not these JSON files. Dead data unless WO-237's panel is wired.

4. **`orientation-recipes.json` is JSONL, not JSON.** 5 newline-delimited objects, no enclosing array. Whole-file `JsonUtility`/`JToken.Parse` would fail; it is appended line-by-line by `TowerPlacementRotateMenu`. Correct as-is but mislabeled `.json` — a naive parser will break on it. (Not in the integrity-test scope, so the test doesn't catch it.)

5. **`castle-south-recipe.json` bypasses CanonicalJson** — loaded by editor scripts (`CastleWallsFromRecipe.cs`, `CastleHubBuilder.cs`) via `Resources.Load<TextAsset>` + `JsonUtility.FromJson`. Editor-only, so WebGL-safety is moot, but it is the one canonical-area file NOT routed through the single loader.

6. **`version` field is inconsistent.** Present on most typed catalogs but ABSENT on `armor`, `weapons`, `garrison-recipes`, `canon-strings`, `en`, `themes`. The integrity test only asserts version on 6 files (abilities/buildings/enemies/packs/pets/waves), so the gaps pass CI silently. `weapons.json`/`armor.json` having no version means a dropped-version hand-edit on gear won't be caught.

7. **StreamingAssets superset vs Resources subset = silent divergence risk.** 26 Resources vs 32 StreamingAssets means the "keep in sync" rule is already only partially held (the 6 extras are intentionally StreamingAssets-only, but nothing enforces that the 26 shared files stay byte-identical between the two roots). No automated cross-root diff test exists — only the within-StreamingAssets integrity test.

8. **No comment-vs-code lie found in this area's loader** (unlike the HeroLocomotion "pure transform" case). `CanonicalJson.cs`'s header (Resources-first, StreamingAssets-fallback, Resources-wins) matches its code exactly. The `DataInjector.cs` comment also matches. Flagged here for completeness: the data-loader layer's comments are accurate.
