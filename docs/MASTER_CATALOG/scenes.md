# MASTER CATALOG — Scenes

Reference catalog of `Assets/Scenes/*.unity` (14 scene files) plus the boot/load-flow
code that wires them. Verified by reading the scene files (root GameObject names) and the
routing source — NOT comments. Stale comments are called out in FLAGS.

Scope: `Assets/Scenes/*.unity`. No YAML dumps. EditorBuildSettings membership verified
against `ProjectSettings/EditorBuildSettings.asset`.

---

## Build settings (load-by-name eligibility)

13 of 14 scenes are in `EditorBuildSettings.asset` (`enabled: 1`). Order:

1. Title  2. HeroSelect  3. PetSelect  4. **Village2**  5. Dungeon_HealersCottage
6. ATBBattle  7. Dungeon_FolksGranary  8. OuterWorld  9. **MainCastle_Hall**
10. Garrison_troll_outpost  11. Garrison_ruined_keep  12. Garrison_hill_fort
13. Garrison_frost_keep

**NOT in build settings:** `Village.unity` (abandoned — see below). Anything not in
build settings cannot be loaded by name: `SceneRouter`, `WorldSceneLoader`,
`SceneTransitionTrigger`, `DungeonEntrance` all guard with
`Application.CanStreamedLevelBeLoaded(...)` and abort with a warning.

Note: scene #0 (boot scene) = Title.

---

## Scenes

### Title.unity  — IN BUILD (#0, boot)
Role: studio bumper + title menu + cold-open / cinematic intro launch point. The boot
scene; the DontDestroyOnLoad Core singletons (GameStateService, audio director, scene
fader) spin up here.
Key roots: `Main Camera`, `EventSystem`, `TitleScreen UIDocument` (+ `TitleController`),
`StoryIntro`, `StudioBumper VideoPlayer`, `SplashLoading`.
Driver: `DeNelle.Onboarding.TitleController`. Routes — **Continue** → `GoCastle()`
(MainCastle_Hall, loads save); **Play Intro** → full Yarn cinematic via
`IntroLauncher.Play` → ends in HeroSelect (fallback: 3-line `StoryIntro` cold-open then
`RunArrival`); the in-Title hero pick confirm → `RouteToPetSelect()` → `GoPetSelect()`.

### HeroSelect.unity  — IN BUILD (#1)
Role: pick one of 4 heroes (Thrain/Mage, Grom/Knight, Sylas/Ranger, Elara/Cleric).
Key roots: Camera, EventSystem, UIDocument hosting `HeroSelectController`. UI is
**100% code-built** on a cleared root (UXML deliberately ignored — WebGL build safety).
Driver: `DeNelle.Onboarding.HeroSelectController`. Confirm ("Dive into Village") →
`PersistHero()` + `GoPetSelect()`. Returning-player skip (save has hero AND starter pet)
→ `GoCastle()`.

### PetSelect.unity  — IN BUILD (#2)
Role: pick one starter Warden (Aether Sprite / Flame Pup / Ice Wolf) from pets.json.
Driver: `DeNelle.Onboarding.PetSelectController` (also fully code-built UI). Confirm →
writes `GameState.StarterPetId`, Save(), then **`GoCastle()`** (lines 120, 468).
Per-class header says "routes to the Village (SceneRouter.GoVillage)" — STALE (FLAG-1).

### MainCastle_Hall.unity  — IN BUILD (#8)  **CANONICAL HOME HUB**
Role: the player's home base / first stop after onboarding and on every resume
(2026-06-08 castle-start pivot). OuterWorld streams in additively over it.
Key roots: `Hero (Blaise)`, `MainKeep_CastleWithTwoLevels_Home`,
`CentralCourtyard_Plaza`, `NavMeshSurface` (+ baked nav sub-objects: KeepInterior_Nav,
UpperBattlements_Nav, GateExit_East_Nav, NavMeshFloor_Invisible_Walkable),
`Gate_South`, `WorldGate_ConnectToOuterWorld_Marker` (seam to OuterWorld), wall/step/rail
geometry, NPC interactables (`NPC_Windmill_Interactable`, `NPC_ArcaneTower_Interactable`),
`StairChord_NavMeshLink`.
Built by `DeNelle.Editor.CastleHubBuilder.BuildCastleHub`. Hub per `Core.HubScenes`.
Reached via `SceneRouter.GoCastle()` → `LoadSceneWithFade("MainCastle_Hall")`.

### Village2.unity  — IN BUILD (#3)  **CANONICAL TOWN (raid target / TD loop)**
Role: the generated tower-defense town (Elarion). `SceneRouter.Village` const = "Village2".
Reached via `GoVillage()` → `LoadVillageWithLoader()` (code-built loading overlay, async,
no UXML). Per memory it's now also repurposed as the enemy-den / raid target.
Key roots: a large generated set of Quaternius MegaKit building/wall/floor/roof/prop
clones (HouseD(Clone), Floor_Brick(Clone), Wall_*Straight, Roof_*, Balcony_*, Stairs_*,
Window_*, Prop_*) — produced by the village factory generator, not hand-placed.
Hub per `Core.HubScenes`. Triggers `WorldSceneLoader` additive OuterWorld load.

### Village.unity  — **NOT IN BUILD — ABANDONED**
Role: the original hand-authored village. Corruption-cursed on re-save (multiple memory
entries; CLAUDE.md §3 "NEVER hand-edit Village.unity"). Superseded by the generated
Village2. Excluded from build settings; nothing routes to it (`SceneRouter.Village` =
"Village2", not "Village"). Keep but do not touch / do not re-add.

### OuterWorld.unity  — IN BUILD (#7)  **ADDITIVE over the active hub**
Role: the surrounding finite open world (4 elemental regions + mine nodes). Never the
active scene by itself — loaded ADDITIVELY on top of a hub (Village2 / MainCastle_Hall)
so town + world share one physics/render space.
Key roots: `OuterWorldRoot`, `ExteriorTerrain` (Terrain), `OuterWorld_NavMeshSurface`,
`RegionAnchors`, region objects `Region-Goldfields (T1 East)`, `Region-Stoneback (T2 West)`,
`Region-Mirewood (T3 South)`, `Region-Ashwood (T4 North)`, `MineNodes` group with many
`MineNode_*` / `MineNode-<Resource>-<Region>` (Wood/Iron/Stone/AetherCrystal),
`Rocks`, `DistantLandmarks` / `DistantTowerSilhouette`, `Directional Light (Dawn)`.
Built by OuterWorldBuilder. Loaded by `WorldSceneLoader` (see below). Hosts
`RaidOutpostSystem` (4 cardinal in-world EnemyOutposts, delayed spawn) and the
dungeon-portal / raid access points.

### ATBBattle.unity  — IN BUILD (#5)
Role: the ATB "Last Stand" turn-based battle scene (breach-time encounter). Single-load
with a fade, carrying `BattleParams` via `SceneRouter.PendingBattle`; returns to
`BattleParams.ReturnScene` (default Village).
Key roots: `Main Camera`, `EventSystem`, `ATBBattleRoot`, `BattleHUD UIDocument`,
`HeroCapsule`, `EnemyCapsule`, `Ground`, `Directional Light` — primitive placeholder
combatants swapped at runtime (`AtbCombatantSwapper`). Reached via `GoBattle(p)`.
Note: BattleHUD is UXML-sourced — see FLAG (UXML doesn't render in builds).

### Dungeon_HealersCottage.unity  — IN BUILD (#4)
Role: Week-1 starter dungeon (canonical content). `SceneRouter.DungeonHealersCottage`.
Key roots/content: `Walls`, `Ceiling`, boss `Keeper` (+ design-note placeholder objects
named with the DisplayName / HP / Special strings, e.g. "The Apprentice of the Apothecary",
"HP: 2.5x normal Hollow One"). Reached via `DungeonEntrance.EnterDungeon()` →
`SceneRouter.LoadScene("Dungeon_HealersCottage")` (single load; ATB return-trip lands back
here via `BattleParams.ReturnScene`).

### Dungeon_FolksGranary.unity  — IN BUILD (#6)
Role: second built dungeon. `SceneRouter.DungeonFolksGranary`. Reached the same way
(DungeonEntrance / DungeonWorldPortalSpawner). Other dungeon consts exist
(SunkenBellTower, WolfwardensVigil, FrostStair, GlassCathedral, ApothecarysVault) but
those scenes are NOT built — entry no-ops via `DungeonDef.SceneExists` guard.

### Garrison_troll_outpost.unity  — IN BUILD (#9)
Role: standalone enemy GARRISON scene (raid target), loaded ADDITIVELY for a raid.
Key roots: `GarrisonRoot` (carries `GarrisonController`), `EnemySpawnPoints` with
`Spawn_0..Spawn_7`, `GarrisonGround`, `HeroStartPoint_PlayerSpawn`,
`ReturnToOuterWorld_Seam`, `Environment`, `Props`, `Directional Light`.
Driver: `DeNelle.Village.World.Camps.GarrisonController` — `Activate()` spawns Troll/
Stonebelly (or recipe `enemyTypeIds`) defenders via the canonical `EnemyFactory` path;
`CleanupAndUnload()` unloads its own scene when the raid ends. Authored by
`DeNelle.Editor.GarrisonSceneBuilder`.

### Garrison_ruined_keep.unity  — IN BUILD (#10)
Same shape as troll_outpost; different recipe/threat. GarrisonController on GarrisonRoot.

### Garrison_hill_fort.unity  — IN BUILD (#11)
Same shape. GarrisonController on GarrisonRoot.

### Garrison_frost_keep.unity  — IN BUILD (#12)
Same shape. GarrisonController on GarrisonRoot.

---

## Boot / load flow (verified against source)

```
Title (#0, boot scene; Core singletons spin up)
  ├─ Continue ─────────────► GoCastle()  ──► MainCastle_Hall   (returning player, loads save)
  └─ Play Intro / New game ─► [Yarn cinematic | StoryIntro cold-open]
                               in-Title hero pick ─► GoPetSelect()
                                                     (older path: GoHeroSelect → HeroSelect)

HeroSelect ── confirm ─► GoPetSelect() ─► PetSelect
   └─ returning-player skip (hero+pet saved) ─► GoCastle()

PetSelect ── confirm ─► GoCastle()  ──►  MainCastle_Hall      (writes StarterPetId, Save)

MainCastle_Hall (active hub)
   └─ WorldSceneLoader auto-loads ► OuterWorld  (ADDITIVE, on any hub)
   └─ SceneTransitionTrigger (south gate seam) ► OuterWorld + WarpTo hero across the seam

OuterWorld (additive over hub)
   ├─ DungeonEntrance / DungeonWorldPortalSpawner ─► Dungeon_HealersCottage / Dungeon_FolksGranary  (single load)
   ├─ RaidOutpostSystem ─► 4 cardinal in-world EnemyOutposts (spawned in OuterWorld, delayed ~10s)
   └─ raid access ─► Garrison_troll_outpost / _ruined_keep / _hill_fort / _frost_keep  (ADDITIVE; GarrisonController.Activate)

Village2 (TD town / raid target)
   └─ GoVillage() = LoadVillageWithLoader() (async + code-built overlay)
   └─ WorldSceneLoader auto-loads ► OuterWorld (ADDITIVE — Village2 is a hub too)

Breach encounter (from Village2 / dungeon)
   └─ GoBattle(BattleParams) ─► ATBBattle ─► returns to BattleParams.ReturnScene (default Village)
```

### Routing code

- **`DeNelle.Core.SceneRouter`** (`Assets/_Modules/Core/SceneRouter.cs`, asmdef DeNelle.Core)
  — static scene-nav surface (React-router port). Public: `LoadScene(name)`,
  `LoadSceneWithFade(name, secs)` (UniTask), `LoadVillageWithLoader()` (async overlay),
  `GoTitle/GoHeroSelect/GoPetSelect/GoVillage/GoCastle/GoDungeon(id)/GoBattle(p)/GoPatriciaLight(p)`,
  `PendingBattle`, `PendingPatriciaLight`, `Dungeon(id)`, `Fader` (ISceneFader).
  Consts: Title, HeroSelect, PetSelect, **Village="Village2"**, **Castle="MainCastle_Hall"**,
  ATBBattle, PatriciaLight="PatriciaLightMode", Dungeon* names. Every load guards build-settings
  membership. Saves GameState before transitions.
- **`DeNelle.Village.WorldSceneLoader`** (`Assets/_Modules/Village/World/WorldSceneLoader.cs`,
  asmdef DeNelle.Village) — static, self-bootstrapping. `[RuntimeInitializeOnLoadMethod`
  `(SubsystemRegistration)]` resets the sceneLoaded handler; `[...(AfterSceneLoad)]` subscribes
  to `SceneManager.sceneLoaded` and additively loads `OuterWorld` whenever a HUB scene
  (`Core.HubScenes.IsHub`) becomes active. No scene wiring. Live/wired.
- **`DeNelle.Core.HubScenes`** (`Assets/_Modules/Core/HubScenes.cs`, asmdef DeNelle.Core) —
  single source of "is this a hub?": `Names = {Village2, MainCastle_Hall, CastleHub,
  CastleHub_MainKeep}`, `IsHub(name)` (exact or Contains). Both WorldSceneLoader and the HUD
  read it (fixed the WO-411 town-HUD-hidden-on-castle drift).
- **`DeNelle.Village.SceneTransitionTrigger`** (`.../World/SceneTransitionTrigger.cs`) — gate
  SEAM between a hub and OuterWorld. PROXIMITY-based (`ProximityRadius`, default 6m), not just
  OnTriggerEnter (a NavMeshAgent hero stops at the navmesh edge). On cross: ensure target scene
  loaded additive, then `HeroLocomotion.WarpTo(targetPosition)`. Public fields:
  `targetSceneName` (def "OuterWorld"), `targetPosition`, `loadAdditive`, `ProximityRadius`.
- **`DeNelle.Village.DungeonEntrance`** (`.../Dungeons/DungeonEntrance.cs`) — "press F / tap to
  enter" surface; hero identity via `HeroLocomotion` component (not tag). `EnterDungeon()` →
  `SceneRouter.LoadScene(def.SceneName)` guarded by `DungeonDef.SceneExists`. Public:
  `Configure(DungeonDef)`, `Def`, `IsHeroInRange`.
- **`DeNelle.Village.World.Camps.GarrisonController`** (`.../Camps/GarrisonController.cs`,
  namespace DeNelle.Village.World.Camps) — brain on a `GarrisonRoot` of an additive garrison
  scene. Public: `Activate()` (spawns garrison via EnemyFactory, idempotent),
  `SpawnInitialGuards()`, `CleanupAndUnload()` (unloads own scene), `AliveCount`,
  `TotalGarrison`, `Cleared`, `OnCleared` event. Inspector: `spawnPoints[]`, `enemyPrefabs[]`,
  `enemyTypeIds[]` (recipe), `threatLevel`, `minLevel/maxLevel`, `activateOnStart`.

---

## FLAGS

- **FLAG-1 (stale comment ↔ code) — routing-to-Village comments are wrong.**
  `PetSelectController.cs` header (line 31) and `SceneRouter.cs` header (line 17, the intro-flow
  diagram "…-> Village") both say the intro chain ends at the **Village**. The actual code
  routes the end of onboarding to the **Castle**: `PetSelectController` calls
  `SceneRouter.GoCastle()` (lines 120, 468); `TitleController.OnContinue` → `GoCastle()`. The
  castle-start pivot (2026-06-08) updated the code but left the "Village" prose. Trust the code:
  onboarding lands in **MainCastle_Hall**, not Village2.

- **FLAG-2 (stale comment ↔ code) — HeroLocomotion is a NavMeshAgent, not "pure transform".**
  `HeroLocomotion.cs` header (lines 4–5) says *"no Rigidbody, no NavMeshAgent — pure transform"*
  and "primitive Capsule with an auto-collider". The code contradicts it: line 205
  `private NavMeshAgent _agent`, lines 242–243 `_agent = GetComponent<NavMeshAgent>(); if (_agent
  == null) _agent = gameObject.AddComponent<NavMeshAgent>();`, movement via `NavMeshAgent.Move`,
  off-mesh clamp, and a teleport-aware `WarpTo` that disables/re-warps the agent. This is the
  exact comment-vs-code class the task called out. The hero shares the enemies' NavMesh; the
  whole seam-crossing design (SceneTransitionTrigger.WarpTo, two-scene navmesh edge) depends on
  it being an agent. The header comment is wrong and dangerous.

- **FLAG-3 (abandoned scene).** `Village.unity` is corruption-cursed, not in build settings, and
  unreferenced by routing. Canonical town = `Village2.unity`. Do not re-add or hand-edit
  (CLAUDE.md §3).

- **FLAG-4 (dead routing target — no scene).** `SceneRouter.PatriciaLight` ("PatriciaLightMode")
  + `GoPatriciaLight` + `PatriciaLightParams` remain in code, but Defend-the-Tower / PatriciaLight
  was REMOVED (PIPELINE_STATE.md 2026-06-09). `HeroSelectController` notes the WO-327 removal of
  its "Jump into the Action" CTA that used to call it. The router entry points are dead — the
  scene is not built and nothing live routes there.

- **FLAG-5 (scene-gated / not built — dungeons).** Of 7 dungeon name consts in SceneRouter, only
  2 scenes exist (Dungeon_HealersCottage, Dungeon_FolksGranary). The other 5
  (SunkenBellTower, WolfwardensVigil, FrostStair, GlassCathedral, ApothecarysVault) have no scene
  file; entry no-ops via the `DungeonDef.SceneExists` guard. Not broken — intentionally stubbed.

- **FLAG-6 (UXML-in-build risk).** `ATBBattle` ships a `BattleHUD UIDocument` (UXML-sourced).
  Per CLAUDE.md §8 / memory, UXML UIDocuments render empty in player builds (BattleHUD/BuildMenu
  hit this). The onboarding screens (Hero/PetSelect) were rewritten code-built for exactly this;
  ATBBattle's BattleHUD may still be exposed.

- **FLAG-7 (two distinct "garrison/outpost" raid paths — easy to conflate).** (a) Standalone
  `Garrison_*` SCENES loaded ADDITIVELY, driven by `GarrisonController` on `GarrisonRoot`,
  authored by GarrisonSceneBuilder. (b) `RaidOutpostSystem` spawns 4 cardinal `EnemyOutpost`s
  IN-WORLD inside OuterWorld (no separate scene). Different mechanisms with similar names — the
  Garrison_* scenes are NOT the RaidOutpostSystem outposts.

- **FLAG-8 (diagnostic noise left in).** `WorldSceneLoader.DiagTerrain` is a large DEF-108
  terrain-debug + runtime splatmap REPAINT block whose own comments say "Remove once resolved".
  It still runs on every OuterWorld additive load and mutates terrain alphamaps at runtime. Not a
  routing bug but live diagnostic/repair code that should be retired.
```
