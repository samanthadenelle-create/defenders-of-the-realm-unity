# Week 4 — Village Buildings + Build Menu + Crystal Economy

**Date:** 2026-05-19
**Slice:** v2-unity-port-spec.md Part 5 Week 4 — "the village buildings: Crystal Mine, Pet House, Arcane Tower, Workshop, Farm. KayKit medieval buildings; one prefab each; HP from data/buildings.json. Build menu (UI Toolkit): floating menu near the build cursor; tap-to-place at valid tiles. Costs in crystals."
**Status:** Source files written. Integration items below are open (no Unity access — cannot build prefabs, create the UIDocument, or wire the scene).

## Files produced

| File | Purpose |
| ---- | ------- |
| `Assets/StreamingAssets/Data/Canonical/buildings.json` | Canonical data for the five gameplay buildings — id, type, displayName (canon-strings key), descriptionKey, HP/maxHP, crystalCost, KayKit model name, footprint, build-menu order. |
| `Assets/_Modules/Village/Buildings/BuildingCatalog.cs` | Typed records (`BuildingDef`, `BuildingCatalogData`, `BuildingFootprint` enum) + `BuildingCatalog` static loader — the canonical-JSON loader pattern. |
| `Assets/_Modules/Village/VillageStrings.cs` | Village-local read-only resolver for `canon-strings.json` + `en.json` (building names + descriptions). A scoped twin of `CanonStrings.cs` — see "Decisions" below. |
| `Assets/_Modules/Village/Buildings/UI/BuildMenu.uxml` | UI Toolkit document — floating panel chrome (header + live crystal balance + close, dynamic card list, status banner). |
| `Assets/_Modules/Village/Buildings/UI/BuildMenu.uss` | Styling for the build menu — matches the BattleHUD.uss / PackStore.uss visual language. |
| `Assets/_Modules/Village/Buildings/UI/BuildMenu.cs` | The build-menu controller — builds cards from `BuildingCatalog`, runs the floating-panel + tap-to-place flow, snaps to a hex grid, validates tiles, deducts crystals. |

**Extended (not rewritten):** `Assets/_Modules/Village/Buildings/Building.cs` — added a `Configure(BuildingDef)` overload + `ConfigureFromCatalog(id)`, a `DisplayNameKey` + `CrystalCost` field/accessor, and `ApplyDamage` / `Repair` HP gameplay with `HpChanged` / `Destroyed` events. The Week-3 skeleton (`BuildingType` enum, `Configure(type,id,label)`, footprint blocker) is untouched.

All `.cs` files compile into the existing `DeNelle.Village` asmdef — it already references `DeNelle.Core` (for `GameStateService`/`GameState`/`ResourceBalance`) and `Unity.InputSystem` (for the tap raycast). No asmdef change needed. Newtonsoft.Json is the project's precompiled, auto-referenced JSON library (the same way `Theme.cs`/`PackCatalog.cs` use it without listing it).

`VillageController.cs` was deliberately NOT edited — controller/scene wiring is the integrator's job (see Integration items).

## Sourced from React v1 vs. authored

**Sourced from the React v1 repo** (`C:\Users\Kayden-Laptop\Documents\defenders-of-the-realm\` — read-only, nothing written there):

- `src/modules/village/buildings/BUILDING_SPOTS.ts` — building **ids** (`crystal-mine`, `pet-house`, `arcane-tower`, `workshop`, `farm`) and the **KayKit mesh urls**. The React urls are `building_<name>_<color>.gltf`; the `model` field strips that to the bare mesh name per `docs/village-buildings-overhaul-spec.md` §3 staging table (`mine_crystal`, `home_B`, `tower_A`, `workshop`, `windmill`).
- `docs/village-buildings-overhaul-spec.md` §3/§4/§5 — the staging-table mesh mapping and the §4 scale convention, which the `footprint` size category is derived from (Arcane Tower 4u tall = `large`; civic buildings 2.5–3u = `medium`; mines 2.5u single structure = `small`).
- `Assets/StreamingAssets/Data/Canonical/canon-strings.json` — already carries `crystalMine` / `petHouse` / `arcaneTower` / `workshop` / `farm` keys. `buildings.json` `displayName` is one of those KEYS, never a literal (port spec Part 4).
- `Assets/StreamingAssets/Data/Canonical/en.json` — already carries `buildingDesc.crystalMine` … `buildingDesc.workshop`. `descriptionKey` points at those.
- `src/state/slices/villageSlice.ts` — `TOWER_TIER_COSTS` (120 crystals to build a tower) is the **only** crystal-cost reference in React v1; used as the anchor the authored building costs were scaled around.

**Authored (defensible values — NOT present in React v1):**

- **Per-building HP and `crystalCost`.** React v1 has no per-building HP table and no build-cost table. Its five buildings are **fixed map placements** (`BUILDING_SPOTS` is a static array), not player-built — only towers and walls carry a cost in React, and buildings have no HP at all (no enemy in v1 attacks a building; they attack the Heart). Authored values:

  | Building | HP | Crystal cost | Rationale |
  | --- | --- | --- | --- |
  | Crystal Mine | 140 | 80 | Cheapest — the economy building must bootstrap; modest HP. |
  | Farm | 120 | 60 | Cheapest, lowest HP — secondary economy, expendable. |
  | Pet House | 160 | 120 | Mid cost/HP — anchored near React's 120-crystal tower build cost. |
  | Workshop | 180 | 150 | Higher — a crafting hub worth protecting. |
  | Arcane Tower | 240 | 220 | Highest both — the defensive structure (`large` footprint, 4u silhouette). |

  These are **tuning constants the owner may rebalance**. JSON wins over any ScriptableObject (port spec Part 4) — rebalancing is a JSON edit, no recompile.

- **`footprint` size category** (`small` / `medium` / `large`). Derived from the §4 scale convention; consumed by the build menu's tile-validity check (`_minClearRadius`). A judgment call, not a React value.
- **`type` enum strings.** Match the existing `BuildingType` enum in `Building.cs` (Week-3 skeleton). `BuildingDef.ResolvedType` parses them.
- **`buildMenuOrder`.** Authored display order — cheapest first so a new player sees an affordable build at the top.

## API wired against

- **`BuildingCatalog`** — `Theme.cs` / `PackCatalog.cs` loader pattern: synchronous `File.ReadAllText` from `Application.streamingAssetsPath`, `JsonConvert.DeserializeObject`, lazy `EnsureLoaded`, `Reload()` for the Monday sync. Same Android caveat as those files (StreamingAssets is inside the APK on Android — a `UnityWebRequest` read is a Week-7/8 follow-up).
- **`Building.Configure(BuildingDef)`** — the Week-4 data-driven entry point. Pulls HP/maxHP/cost/displayName-key off the def. `VillageController` and `BuildMenu` both call it.
- **`GameStateService`** — `BuildMenu.SpendCrystals` mirrors `PackStore.ApplyPackContents` exactly: read `service.State.Resources` (a struct), mutate `.Crystals`, write the struct back whole, `service.Save()`, then `service.ResourcesChanged.Invoke()` so the HUD resource bar refreshes. `GameState.Resources` is a public field, so the whole-struct write-back is required.
- **Unity Input System** — `BuildMenu` reads `Mouse.current` / `Touchscreen.current` for the build-cursor position and tap detection (port spec Part 2 forbids the legacy `Input` API). The placement raycast uses built-in 3D `Physics.Raycast` against `_groundMask`.

## Build menu — how it works

1. The HUD's "Build" button calls `BuildMenu.Open()` — the floating panel shows; `Render()` builds one card per `BuildingCatalog` entry with the canon name, flavour text, `◆ cost` and `HP`. Unaffordable cards dim and their cost turns red.
2. Tapping a card *arms* that `BuildingDef` (a second tap on the armed card disarms). A ghost-preview prefab (optional, `_ghostPrefab`) follows the cursor.
3. `Update()` raycasts the build cursor to the ground, snaps the hit to the nearest hex-tile centre (`SnapToHex` — pointy-top axial layout, odd rows half-offset), and validates: inside the buildable square, and clear of any `Building`/`WallSegment`/`Gate` within `_minClearRadius`.
4. A world tap on a valid tile commits: `SpendCrystals` deducts the cost (deducted *before* the spawn so a failed spawn never gives a free building), the prefab matched to the building's `BuildingType` is instantiated, and `Building.Configure(def)` is called.
5. `BuildingPlaced(Building, BuildingDef)` event fires for the integrator to hook (e.g. `VillageController.RegisterBuilding`).

`BuildMenu` runs standalone for testing: set `_useGameState = false` and it spends from a local `_localCrystalBalance` int instead of `GameState`.

## Integration items (open — need Unity)

1. **Build the UIDocument.** Create a GameObject in the Village HUD with a `UIDocument` (source asset = `BuildMenu.uxml`) + the `BuildMenu` component. The panel hides itself in `OnEnable` until `Open()` is called.
2. **Wire the HUD "Build" button** to `BuildMenu.Open()` (or `Toggle()`). `en.json` already has `tooltip.buttonBuild.*` copy for that button.
3. **`_buildCamera`** — assign the village camera, or leave blank to default to `Camera.main`.
4. **`_groundMask`** — set to the layer the village ground plane is on, so the placement raycast hits ground only (not buildings/walls).
5. **`_buildingPrefabs`** — assign one prefab per `BuildingType` (5 entries). Each prefab must carry a `Building` component (or one on a child). KayKit meshes per `buildings.json` `model`: `mine_crystal`, `home_B`, `tower_A`, `workshop`, `windmill` — from `public/kaykit/medieval/` (port spec Part 7). Until a type's prefab is wired, placing it shows a "no prefab yet" status and no crystals are spent.
6. **`_ghostPrefab`** (optional) — a translucent preview mesh. With none assigned the menu still works; there is just no ghost.
7. **Tune the grid constants** — `_hexSize` (default 4u), `_buildAreaHalfExtent` (default 26u — should sit inside the `WallLayout` rectangle, half-extents 28u × 21u), `_minClearRadius` (default 2.5u). Tune against the actual built scene.
8. **`VillageController` hook (optional).** Per the task constraints `VillageController.cs` was not edited. To have the controller track player-built buildings, the integrator subscribes to `BuildMenu.BuildingPlaced` and calls the existing `VillageController.RegisterBuilding(building)` — a one-line wire, no controller edit needed.
9. **`buildings.json` `.meta`** — Unity will generate it on first import (do not hand-create, per task constraints).

## Cross-reference — `week4-waves.md` item 5

The sibling Week-4 waves slice (`docs/port-notes/week4-waves.md`, item 5) noted that `Building.cs` had `_hp` but **no public damage method**, so enemies path straight through buildings. This slice adds `Building.ApplyDamage(float)` / `Building.Repair(float)` plus `HpChanged` / `Destroyed` events. To close that item, the integrator should make `Building` implement the `IDamageableStructure` interface defined in `Enemy.cs` (a thin adapter: `IDamageableStructure.TakeDamage` → `Building.ApplyDamage`). That interface lives in the Waves slice's `Enemy.cs`; this slice did not edit it to avoid two slices racing the same file.

## Decisions worth a row in unity-decisions.md (not added — that file is integrator-owned)

- **Loader placement: `_Modules/Village/Buildings/`, not the `Assets/Data` module.** The task brief said "the correct Data/Core module," and a `DeNelle.Data` asmdef does exist (and is referenced by `DeNelle.Village`). But it is **empty** — every canonical loader written so far lives co-located with its consumer module: `Theme` in `Core/Theme`, `PackCatalog` in `Wallet`, `CanonStrings` in `Onboarding`. `BuildingCatalog` follows that established convention — it sits with its consumers `Building.cs` and `BuildMenu.cs`, all in `DeNelle.Village`. Reversible: moving the three data types to `DeNelle.Data` is mechanical (`DeNelle.Data` already references `DeNelle.Core`; it would need Newtonsoft, which is auto-referenced anyway).
- **`VillageStrings` is a Village-local twin of `CanonStrings`.** The build menu must resolve building display names from `canon-strings.json` (port spec Part 4 — never type canon strings inline). `CanonStrings.cs` already does exactly this read, but it lives in `DeNelle.Onboarding`, which `DeNelle.Village` does not reference — and growing a Village→Onboarding asmdef dependency just for string lookup violates module isolation (port spec Part 2). So `VillageStrings` duplicates the ~40-line flat-map loader, scoped to Village. Reversible: when the Unity Localization package owns these strings (port spec Part 3), both `CanonStrings` and `VillageStrings` collapse into string-table lookups.
- **Crystal spend is a direct `GameState.Resources` mutation** (read struct → mutate → write back → `Save()` → raise `ResourcesChanged`), mirroring `PackStore.cs`. There is no `SpendCrystals` mutator on `GameStateService` yet. If a typed resource mutator is later added there, `BuildMenu.SpendCrystals` should delegate to it. Reversible — mechanical.
