# Master Catalog — Misc Modules

Scope: `Assets/_Modules/Dungeons`, `Environment`, `Data`, `UI` (and any `_Modules` subfolder not owned by another code area). Verified by reading source, not comments. Out of scope (other areas): ATB, Audio, BattleATB, Characters, Core, Cosmetics, DevTools, DialogueUI, Economy, HUD, Onboarding, Pets, Settings, Village, Wallet, Web3.

---

## Dungeons — asmdef `DeNelle.Dungeons` (ns `DeNelle.Dungeons`)

Refs: DeNelle.Core, DeNelle.Data, Unity.Localization, UniTask, Unity.Cinemachine, Unity.InputSystem, Unity.Addressables, Unity.ResourceManager. (Does NOT reference DeNelle.BattleATB — ATB handoff goes via `SceneRouter`.)

3D walk-around dungeon gameplay. Two scenes, both **in EditorBuildSettings (LIVE)**:
- `Scenes/Dungeon_HealersCottage.unity` — full **data-driven** dungeon (loads `healers-cottage.json`).
- `Scenes/Dungeon_FolksGranary.unity` — **STUB** scene (uses `DungeonStubEncounter`/`DungeonStubReturn` + capsule placeholder hero); no JSON layout.

### Root code

| Class | File | Responsibility | Key public API | Notes |
|---|---|---|---|---|
| `DungeonController` | DungeonController.cs | Orchestrates a full data-driven run: loads layout JSON, places rooms/interactables, spawns hero at spawn point, wires Lantern/Bryn/crafting/HUD | `async UniTask EnterDungeon()`, `UniTask ExitToVillage()` | MonoBehaviour. **Bootstrap = `Start()` calls `EnterDungeon().Forget()`** (scene-placed in HealersCottage). `OnDestroy` guards BUG-008 encounter-handoff round-trip. Heavy `[SerializeField]` wiring (runtimeState, hero, camera, lantern, Bryn, inventory, pedestal, panel, HUD). LIVE. |
| `DungeonHero` | DungeonHero.cs | Keeper locomotion: WASD (camera-relative) + tap-to-move, drives Animator `Speed` | `void SetCamera(Camera)`, `void SetInputEnabled(bool)`, `void Teleport(Vector3,float?)`; props `IsMoving`,`CurrentSpeed`,`HasTapTarget` | MonoBehaviour. **Uses `CharacterController`** (gravity, planar accel) — NOT NavMeshAgent. Comment-vs-code is CONSISTENT here. Animator null-guarded; caches `_hasSpeedParam` (WO-163 per-frame-error fix). Scene-placed. |
| `DungeonCameraRig` | DungeonCameraRig.cs | Optional isometric Cinemachine framing helper | `void Bind(Transform)`, `void RefreshFraming()`, `void SetFollowOffset(Vector3)` | MonoBehaviour. Optional — DungeonController applies inline framing when null. |
| `DungeonLayout` (+ data types) | DungeonLayout.cs | Pure data model of a dungeon: rooms/walls/bounds/points + `DungeonLayoutLoader` | `DungeonRoom FindRoom(string)`, `DungeonRoom RoomAt(Vector3)`; statics `DungeonLayoutLoader.LoadAsync` (UniTask, never async void) | Serializable POCO classes/structs: `DungeonPointXZ`,`DungeonPoint`,`DungeonBounds`,`DungeonWall`(solid/doorway/illusory),`DungeonRoom`, mini-boss/chest/encounterPool/bryn/lantern/oilStone defs. Loads from `StreamingAssets/Data/Canonical/dungeons/` (Android-async). |
| `Checkpoint` | Checkpoint.cs | Heal-and-save shrine; first entry heals hero+pets, sets respawn, settles violet→gold pulse, raises toast | `void Configure(...)`, `void SetReducedMotion(bool)` | MonoBehaviour, scene/data-placed. **Heal is wired-but-redundant in v1** (v1 fully restores HP/MP after every ATB fight) — functions as save/respawn marker. Self-flagged in header. |
| `EncounterTrigger` | EncounterTrigger.cs | Scripted + random + boss ATB encounter zones; handoff via `SceneRouter.GoBattle` | `void ConfigureScripted(...)`,`ConfigureBoss(...)`,`ConfigureRandom(...)`,`SetCurrentRoomKind(string)`,`bool ResumePendingEncounter(bool victory)` | MonoBehaviour. **Random path gated OFF in v1** (`DungeonLayout.disableRandomEncounters`; Cottage = scripted only); random code intact for v1.1 — scene-gated/dormant. |
| `RandomEncounterTable` | RandomEncounterTable.cs | Pure-C# random-roll math (cooldown, quiet-stretch ramp, reward cap, weighted tiers) | `Verdict Roll(...)`; nested readonly struct `Verdict` | Plain class (no MonoBehaviour). Consumed only by the v1-OFF random path → effectively **dormant in v1**. |
| `Lantern` | Lantern.cs | Hero oil lantern: light radius/oil meter, oil-stone refills, tincture buff, cloak interaction | `void Configure(...)`,`SetCloakOwned(bool)`,`SetReducedMotion(bool)`,`TriggerTincture()`,`ClearTincture()` | MonoBehaviour, scene-placed (attached to hero rig). |
| `LoreStone` | LoreStone.cs | Readable lore stone; marks fragment read in runtime state | `void Configure(...)`,`SetLoreFragments(LoreFragmentSet)`,`Read()` | MonoBehaviour, data-placed. |
| `LoreFragments` | LoreFragments.cs | Lore fragment data set + lookup | `LoreFragment Find(string id)` | Data types (`LoreFragment`,`LoreFragmentSet`); loaded from `lore-fragments.json`. |
| `DungeonStubEncounter` | DungeonStubEncounter.cs | STUB-scene proximity pad → fires ATB encounter; static return-position snapshot survives ATB round-trip | `void Configure(string[],string,float)` | MonoBehaviour. Default `_returnScene = SceneRouter.DungeonFolksGranary` → **belongs to Folks Granary STUB scene**. `_armed` flag prevents endless re-fire loop on reload. Proximity poll + OnTriggerEnter. LIVE in stub scene only. |
| `DungeonStubReturn` | DungeonStubReturn.cs | STUB-scene exit pad → `SceneRouter.GoVillage()`; F-key fallback | (none public) | MonoBehaviour. Best-effort hero detection by name/`Player` tag. STUB scene only. |

### Crafting/ (Workstream C)

| Class | File | Responsibility | Key public API | Notes |
|---|---|---|---|---|
| `CraftingData` (+ loader) | Crafting/CraftingData.cs | Data model: ingredients, recipes, placements, pedestal def | static `CraftingDataLoader` (load) | Serializable POCOs: `CraftPoint`,`CraftingIngredient`,`RecipeIngredient`,`CraftingRecipe`,`IngredientPlacement`,`CraftingPedestalDef`,`CraftingDataSet`. From `crafting-recipes.json`. |
| `CraftingPedestal` | Crafting/CraftingPedestal.cs | Interactable craft station; opens panel | `void Configure(...)`,`SetReducedMotion(bool)`,`Interact()`,`ClosePanel()` | MonoBehaviour. Emits `CraftingPanelRequest`. |
| `DungeonInventory` | Crafting/DungeonInventory.cs | Runtime ingredient inventory SO; persisted via PlayerPrefs | `int CountOf(string)`,`bool HasCollectedPickup(string)`,`bool HasCrafted(string)`,`bool CollectPickup(...)`,`void AddIngredient(...)`,`bool CanCraft(CraftingRecipe)`,`bool Craft(CraftingRecipe)`,`void Clear()` | **ScriptableObject** with `[CreateAssetMenu]` (designer-droppable; runtime state is PlayerPrefs-backed, not the asset). Instance: `State/HealersCottageInventory.asset`. |
| `IngredientPickup` | Crafting/IngredientPickup.cs | Collectible ingredient mote → adds to inventory | `void Configure(IngredientPlacement,DungeonInventory,Transform)`,`SetReducedMotion(bool)` | MonoBehaviour, spawned per `ingredientPlacements[]`. |

### UI/ (UI Toolkit — code-driven controllers + UXML/USS)

| Class | File | Responsibility | Key public API | Notes |
|---|---|---|---|---|
| `CraftingPanelController` | UI/CraftingPanelController.cs | UI Toolkit crafting panel; binds pedestal | `void BindPedestal(CraftingPedestal)`,`Show(CraftingPanelRequest)`,`Hide()` | MonoBehaviour. Styled by `CraftingPanel.uss`/`.uxml`. **NOTE: §8 memory says UXML doesn't render in player builds** — dungeon panels are UXML-sourced; verify in-build rendering (potential flag). |
| `DungeonHudController` | UI/DungeonHudController.cs | Dungeon HUD (oil meter reads Lantern API each frame) | `void SetLantern(Lantern)` | MonoBehaviour. Styled by `DungeonHud.uss`/`.uxml`. Same UXML-in-build caveat. |

UXML/USS assets: `UI/CraftingPanel.uxml|.uss`, `UI/DungeonHud.uxml|.uss`.

### Wanderer/ (Bryn the wandering NPC)

| Class | File | Responsibility | Key public API | Notes |
|---|---|---|---|---|
| `Bryn` | Wanderer/Bryn.cs | Wandering NPC; tiered dialogue by deaths/clears/level | `void Configure(DungeonBrynDef,DungeonRuntimeState)`,`SetHero(Transform)`,`SetLoreFragments(...)`,`SetHistory(int,int)`,`SetRecommendedLevel(int?)`,`SetReducedMotion(bool)` | MonoBehaviour, data-placed from layout `bryn` block. |
| `WandererBubble` | Wanderer/WandererBubble.cs | Speech bubble UI; implements `IWandererBubble` | `void Show(string,string)`,`Hide()` | MonoBehaviour. |
| `WandererDialogue` | Wanderer/WandererDialogue.cs | Static dialogue-line provider; `enum WandererTier` | static helpers | Plain static class + enum. |

### State/

| Class | File | Responsibility | Key public API | Notes |
|---|---|---|---|---|
| `DungeonRuntimeState` | State/DungeonRuntimeState.cs | Runtime-only run state SO: current room, checkpoints, lore read, chests, hero vitals, encounter handoff | `StartRun(...)`,`EndRun()`,`SetHeroPosition`,`SetCurrentRoom`,`MarkSecretRoomFound`,`TickEncounterClock`,`Register*Encounter`,`BeginEncounterHandoff`,`bool ResumeAfterEncounter(bool)`,`ClearPendingEncounter`,`SetHeroVitals`,`bool HealHeroToFull()`,`ResolveEncounter`,`MarkBossDefeated`,`bool ReachCheckpoint(string)`,`bool ReadLoreStone(string,int)`,`bool OpenChest(string)`; query: `HasReadLore`,`HasReachedCheckpoint`,`HasFiredScriptedEncounter`,`HasOpenedChest`; prop `RunActive` | **ScriptableObject** `[CreateAssetMenu]` (designer-droppable; not a persisted save). Instance: `State/HealersCottageRuntimeState.asset`. |

### Generated/ + asset instances

- `Generated/DungeonPanelSettings.asset` — UI Toolkit PanelSettings for the dungeon HUD/crafting panels (own PanelSettings, per the §8 store-wiring lesson).
- `State/HealersCottageInventory.asset` — DungeonInventory SO instance.
- `State/HealersCottageRuntimeState.asset` — DungeonRuntimeState SO instance.

### Dungeon DATA JSON (in global Resources + StreamingAssets, NOT under _Modules; referenced by this module)

Dual-copy convention (StreamingAssets source + Resources copy that wins via CanonicalJson). Both copies present.

- `Data/Canonical/dungeons/healers-cottage.json` — schema top keys: `version,id,title,tier,questlineId,entryRoomId,disableRandomEncounters,ambientBgm,rooms[],spawn,loreStones,checkpoints,scriptedEncounters,miniBoss,chests,encounterPool,bryn,lanternPosts,oilStones` (+ `_comment/_sources/_schemaNotes`). **12 rooms** (garden-approach, entrance-room, main-room, kitchen, pantry-alcove, workshop, loft-bedroom, loft-study, root-cellar, storage, crypt-sublevel, hidden-vault), 5–8 walls each. `disableRandomEncounters` set (v1 scripted-only).
- `Data/Canonical/crafting-recipes.json` — keys `version,ingredients[3],recipes[1],ingredientPlacements[3],pedestal` (+ meta). Currently **1 recipe, 3 ingredients, 3 placements** — minimal v1 set.
- `Data/Canonical/lore-fragments.json` — keys `version,fragments[6]` (+ `_comment/_provenance`). **6 fragments.**
- No `folks-granary.json` exists — Folks Granary is the stub scene (no data-driven layout). **Granary is a STUB, not a data dungeon.**

### Dungeons DOC pointers (not in scope folder, referenced by README)
`docs/DUNGEON_DESIGNS.md`, `docs/dungeon-3d-healers-cottage-design.md`, `docs/dungeons-storyline.md`.

---

## Environment — Assembly-CSharp (NO asmdef, global namespace)

Both files are global-namespace by design (so cross-asmdef code can't see them — they self-bootstrap or attach to scene props). Code-vs-comment: header docs match code here.

| Class | File | Responsibility | Key public API | Bootstrap / Live |
|---|---|---|---|---|
| `NightTorchLightSystem` | NightTorchLightSystem.cs | Self-installing warm point-light system: ramps gate/plaza/hero lights up as night ambient darkens; lifts ambient floor; mobile-cheap (capped 10 lights, shadows off) | `static Instance` | **`[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]` self-bootstrap → DDOL singleton.** **Scene-gated to `"Village2"`** (`TargetScene`), tears down on leaving. Polls `RenderSettings.ambientLight` (no event coupling). Attaches to existing `TorchFireController` lights if present. **LIVE in Village2 only.** Singleton uses `Destroy(gameObject)` on a dedicated GO (safe — not a shared host, cf. singleton-dedup memory). |
| `TorchFireController` | TorchFireController.cs | Per-torch flame VFX + flickering warm light; intensifies on nearby combat (`Physics.OverlapSphere` per Update) | public fields `fireParticles`,`emberParticles`,`pointLight`,tuning | MonoBehaviour, **scene-placed on torch props** (no self-bootstrap). Auto-finds child ParticleSystem/Light in Awake. Undefined-tag-safe `HasTag` guard. Perf note: OverlapSphere/Update OK for ≤8 torches (self-flagged scaling concern). |

README (`Environment/README.md`): accurate, current.

---

## Data — Assembly-CSharp (NO asmdef, global namespace)

| Class | File | Responsibility | Key public API | Notes |
|---|---|---|---|---|
| `MasterAssetCatalog` | MasterAssetCatalog.cs | ScriptableObject catalog of prefabs by key: buildings/defensePieces/props/nature | `GameObject GetBuilding(string)`,`GetDefense(string)`,`GetProp(string)`,`GetNature(string)` | **ScriptableObject** `[CreateAssetMenu("Defenders/Catalog/Master Asset Catalog")]`. Serializable entry classes `BuildingEntry`(key/prefab/district),`DefenseEntry`(key/prefab/type),`PropEntry`(key/prefab/category),`NatureEntry`(key/prefab). Linear lookup via `List.Find`. Doc: `docs/MASTER_ASSET_REFERENCE.md`. **No callers found in scope** — verify whether any catalog `.asset` instance/consumer is wired (possible dormant; cf. data-driven `StructureFactory`/recipe pipeline that may supersede it). |

README (`Data/README.md`): accurate. NOTE name collision: this is folder-asmdef-less `MasterAssetCatalog` (global ns), distinct from the `DeNelle.Data` asmdef referenced by Dungeons — `DeNelle.Data` lives elsewhere.

---

## UI — Assembly-CSharp (NO asmdef, global namespace)

| Class | File | Responsibility | Key public API | Notes |
|---|---|---|---|---|
| `GameOverUI` | UI/GameOverUI.cs | uGUI game-over screen ("Elarion has fallen…"), restart button reloads active scene | `void Show()` | MonoBehaviour. Hides self in Awake. Uses TMP + uGUI Button. README notes overlap with `Village/Heart/GameOverScreen` + WO-235 death/spire screens → **possible duplicate/legacy death-screen path; verify which is wired in the live loop.** |

README (`UI/README.md`): accurate, flags the overlap itself.

---

## FLAGS

### Stale-comment-vs-code
- **None of the HeroLocomotion class found in this scope.** `DungeonHero` correctly uses `CharacterController` and its comments say so — comment-vs-code CONSISTENT (the HeroLocomotion NavMeshAgent mismatch is in the Village/locomotion area, not here).
- `Checkpoint` header honestly self-flags its heal as "wired-but-redundant in v1" — comment matches code (redundant but functional). Not stale, just dormant-by-design.

### Dead / dormant / dual-path
- `RandomEncounterTable` + the random branch of `EncounterTrigger`: **dormant in v1** (`disableRandomEncounters` set in healers-cottage.json; Cottage uses scripted encounters only). Intact for v1.1 — not dead, but currently unreachable.
- `MasterAssetCatalog` (Data): **no consumer found in scope** — likely superseded by the recipe/`StructureFactory` data pipeline (village-factory-architecture memory). Confirm any `.asset` instance is still referenced before relying on it.
- `GameOverUI` (UI): **possible duplicate** of `Village/Heart/GameOverScreen` and WO-235 death/spire screens. One of these is the live death path; the other is legacy. Needs verification.

### Scene-gated / disabled
- `NightTorchLightSystem`: hard-gated to scene `"Village2"` only (string `TargetScene`); inert everywhere else by design.
- `DungeonStubEncounter` / `DungeonStubReturn`: only live in the **Folks Granary STUB scene**; not part of the data-driven Healers Cottage run.
- Folks Granary has **no JSON layout** — it is a placeholder stub scene, in contrast to the full data-driven Healers Cottage.

### Build / rendering risk
- Dungeon UI Toolkit panels (`CraftingPanelController`, `DungeonHudController`) are **UXML-sourced**. Project memory (§8, uxml-uidocuments-dont-render-in-builds) warns UXML HUDs come up empty in player builds. These have a dedicated `DungeonPanelSettings.asset`, but in-build rendering should be verified — potential empty-panel risk.
- `DungeonController.OnDestroy` carries a BUG-008 encounter-handoff guard — fragile scene round-trip path (dungeon→ATB→dungeon); worth a regression check after any scene-routing change.

### Architecture notes (not bugs)
- Environment + Data + UI scope folders have **no asmdef** (compile into Assembly-CSharp, global namespace) — intentional for `RuntimeInitializeOnLoadMethod` self-install and scene-prop attachment without cross-asmdef visibility.
- Dungeons module reaches ATB only through `SceneRouter` (no `DeNelle.BattleATB` ref) — clean boundary, consistent with CLAUDE.md §5 cross-assembly law.
