# Master Catalog — Editor Tools (`Assets/Editor`)

Reference catalog of the scoped editor tooling. **All files live in the single
`DeNelle.Editor.asmdef`** (namespace `DeNelle.Editor`, Editor-only platform). That
asmdef references `DeNelle.Core`, `DeNelle.Data`, `DeNelle.Sandbox`,
`Unity.InputSystem`, `Unity.Localization(.Editor)`, URP runtime, `Unity.AI.Navigation`,
`UnityEngine.UI`. It deliberately does **NOT** reference `DeNelle.Village` — every
Village type (WaveManager, MineNode, DragonBoss, GarrisonController, HeartController…)
is reached by **reflection / `FindType` over AppDomain**. None of these are
MonoBehaviours and none use `[RuntimeInitializeOnLoadMethod]`; they are all
menu/`-executeMethod` entry points (no auto-bootstrap).

"Destructive" below = mutates scene/asset/GameObjects (run with care / save-state).
"Safe" = read-only or additive-under-Undo (writes nothing until you save).

---

## CASTLE TOOLS

### CastleHubBuilder — `Assets/Editor/CastleHubBuilder.cs`
Generates the entire Central Castle Hub (`CastleHubRoot`) from polyperfect `_M` +
Quaternius MegaKit prefabs: outer walls/4 corner towers/south gate, 8 courtyard
structures, main keep, upper battlements, stairs, OuterWorld connection marker.
Idempotent (destroys prior `CastleHubRoot` first). Loads prefabs via `LoadPoly`/
`LoadQuat`, missing = LogWarning. Has its own `GateRecipe`/`GatePiece` serializable
classes (separate from castle-south-recipe).
- `[MenuItem Defenders/Scenes/Build CastleHub_MainKeep] BuildCastleHub()` — full build. **DESTRUCTIVE** (wipes+rebuilds CastleHubRoot in open scene).
- `[MenuItem …/Add NavMesh Floor to Current Castle] AddNavMeshFloorToCurrentCastle()` — adds invisible walkable floor. **Destructive** (additive GO, not under Undo).
- `[MenuItem …/Wire Current Castle to OuterWorld] WireCurrentCastleToOuterWorld()` — adds NavMeshLink + OuterWorldTransitionTrigger on gate marker (reflection; idempotent, strips priors). **Destructive**.
- `[MenuItem …/Make CastleHub Primary Start (current scene) + Wire Everything] MakeCastleHubPrimaryStartAndWire()` — **Destructive**.
- `BatchWireCastleAndSave()` (batchmode, no menu) — opens `MainCastle_Hall.unity`, promotes `Capsule`→`HeroStartPoint_PlayerSpawn` (strips mesh/collider), wires, **SAVES scene**. **DESTRUCTIVE**.
- `BatchRebuildGrandStairAndBake()` — force-rebuilds GrandStair then bakes+saves. **DESTRUCTIVE**.
- `BatchAddFloorAndBakeCastle()` — opens MainCastle_Hall, deletes stray planes, ensures floor+stair, bakes NavMesh (reflection on NavMeshSurface), **SAVES**. **DESTRUCTIVE**.
- Dependencies: polyperfect/Quaternius prefabs, NavMeshSurface/NavMeshLink (reflection), `MainCastle_Hall.unity`. **WIRED/LIVE** (the home-hub generator).

### CastleWallKitSpawner — `Assets/Editor/CastleWallKitSpawner.cs`
Drops a fresh 4-piece south wall kit (gate + 2 walls + 1 corner tower) under a
`CastleSide_South` parent, symmetric about x=0, for hand-arrangement before mirroring.
- `[MenuItem Defenders/Castle/Spawn Fresh Wall Kit (South side)] SpawnKit()` — additive prefab instances under Undo; confirms if parent exists. **SAFE** (writes no scene). Never runs CastleHubBuilder.
- Deps: polyperfect Medieval_M prefabs. **WIRED/LIVE**.

### CastleSideMirror — `Assets/Editor/CastleSideMirror.cs`
Clones hand-authored `CastleSide_South` into West/North/East copies rotated
90/180/270° around world origin (the Heart at 0,0,0). Re-runnable (destroys prior
copies first).
- `[MenuItem Defenders/Castle/Mirror South Side x4 (90deg around origin)] MirrorSouthSide()` — additive cloning under Undo; deletes prior mirror copies. **SAFE-ish** (additive, no scene save; but DOES `Undo.DestroyObjectImmediate` prior copies). **WIRED/LIVE**.

### CastleOffsetCapture — `Assets/Editor/CastleOffsetCapture.cs`
Captures owner's hand-authored `CastleSide_South` (each child prefab + local TRS +
parent world TRS) to `Assets/Resources/Data/castle-south-recipe.json` so the layout
is code-reproducible.
- `[MenuItem Defenders/Castle/Capture South Side -> Recipe] Capture()` — **opens** `MainCastle_Hall.unity` (Single), reads transforms, writes JSON. **SAFE** (does not modify/save the scene; only writes the JSON asset).
- **FLAG:** header comment refers to the recreate builder as "CastleSideFromRecipe" but the actual class is named **`CastleWallsFromRecipe`** (stale name in comment).

### CastleWallsFromRecipe — `Assets/Editor/CastleWallsFromRecipe.cs`
Deletes the four `CastleSide_*` groups and rebuilds South from the recipe JSON, then
mirrors ×4 (same 90/180/270 origin-rotation). Idempotent. Reads via
`Resources.Load<TextAsset>("Data/castle-south-recipe")`.
- `[MenuItem Defenders/Castle/Recreate Walls from Recipe (delete + rebuild + mirror)] Recreate()` — **DESTRUCTIVE** (deletes the 4 wall groups; under Undo). Additive prefab instances; writes no scene itself.
- Deps: `castle-south-recipe.json`, polyperfect Medieval_M prefabs. **WIRED/LIVE**.

### CastleBuilderTester / CastleHomeBuilder / CastleWalkable / CastleHubBuilder
(Other Castle*.cs files exist — CastleBuilderTester.cs, CastleHomeBuilder.cs,
CastleWalkable.cs — outside the explicit scope list; not deep-read here.)

---

## OUTER WORLD TOOLS

### OuterWorldBuilder — `Assets/Editor/OuterWorldBuilder.cs`
Builds the OuterWorld region layer (WO-142 Phase A): 4 region anchors
(Goldfields/Stoneback/Mirewood/Ashwood) + 8 starter MineNodes placed per-region with
resource/yield/finite-reserve tuned to danger tier. MineNode + CrystalMineNode added
by **reflection** (no Village ref). Uses `DeNelle.Core.World.RegionId`.
- `[MenuItem Defenders/World/Build Outer World (Regions + Mine Nodes)] BuildOuterWorld()` — `NewScene(EmptyScene)` then saves to `OuterWorld.unity`. **DESTRUCTIVE** (overwrites OuterWorld.unity).
- `[MenuItem Defenders/World/Bake World NavMesh (Village + Terrain)] BakeWorldNavMesh()` — opens **`Village.unity`** (Single) + OuterWorld (Additive), levels terrain to Y=0, flags terrains NavigationStatic, `NavMeshBuilder.BuildNavMesh()`, **SAVES BOTH SCENES**. **DESTRUCTIVE**.
  - **FLAG (corruption risk):** this opens & re-saves the **corruption-cursed `Village.unity`** (CLAUDE.md §3 / memory `village-scene-resave-corruption`). `Village.unity` is abandoned (Village2 is canonical) — this legacy bake is **likely STALE/dangerous**; prefer `OuterWorldNavBake` (solo, touches only OuterWorld).
- **WIRED/LIVE** for BuildOuterWorld; BakeWorldNavMesh = **stale/risky**.

### OuterWorldNavBake — `Assets/Editor/OuterWorldNavBake.cs`
The corruption-safe replacement: bakes a solo NavMeshSurface for **OuterWorld only**
so the Castle→OuterWorld warp (~0,0.5,-80) lands on walkable ground. NavMeshSurface via
**reflection** (no hard package dep). Levels terrain to Y=0 first.
- `[MenuItem Defenders/World/Bake OuterWorld NavMesh (solo surface)] Bake()` — opens OuterWorld, bakes, writes `Assets/Scenes/OuterWorld/NavMesh-OuterWorld.asset`, **SAVES scene**. **DESTRUCTIVE** (but touches only OuterWorld — the intended safe path). **WIRED/LIVE**, supersedes BakeWorldNavMesh.

### ExteriorTerrainBuilder — `Assets/Editor/ExteriorTerrainBuilder.cs`
(Referenced by OuterWorldBuilder/OuterWorldNavBake as the terrain source; not deep-read — out of explicit scope.)

---

## GARRISON / RAID-TARGET TOOLS

### GarrisonSceneBuilder (partial) — `Assets/Editor/GarrisonSceneBuilder.cs` + `.Scenes.cs`
Recipe-driven, catalog-first garrison/dungeon builder: reads
`Assets/Resources/Data/Canonical/garrison-recipes.json` and realizes a full
`Garrison_<id>.unity` per recipe (Environment/Props/EnemySpawnPoints + themed lighting
+ GarrisonController + baked NavMeshSurface + registers in Build Settings). Vision:
"add a fort = one JSON line, zero code." Village types (GarrisonController,
SceneTransitionTrigger) added by **reflection**; `Unity.AI.Navigation.NavMeshSurface`
referenced directly (`using`). Prop ROLES resolved to prefabs via `ResolveRole`
(polyperfect first → Resources/Structures → tinted primitive).
- `.cs`: `[MenuItem Defenders/Scenes/Build All Garrisons (From Recipes)] BuildAll()` — loops every recipe. **DESTRUCTIVE** (NewScene + SaveScene + edits Build Settings).
- `.cs`: `BuildById(string id)` — single recipe (batchmode-friendly). **DESTRUCTIVE**.
- `.cs` shared helpers: `BakeNavMesh`, `SaveSceneTo`, `AddSceneToBuildSettings`, `SetField`/`FindType` (reflection), `Log/Warn/Err`.
- `.Scenes.cs`: `BuildFromRecipe(GarrisonRecipe)` (the single generic realizer) + prop-role map. Paths: `ScenesDir=Assets/Scenes`, `PolyPrefabRoot`, `ResStructRoot`; `OuterWorldReturnPos=(0,0.5,-80)`.
- Deps: `garrison-recipes.json`, `DeNelle.Core.World.GarrisonRecipe(Catalog)`, polyperfect/Resources structures. **WIRED/LIVE**.

---

## ANIMATOR FACTORIES

### AnimatorSetup — `Assets/Editor/AnimatorSetup.cs`
Canonical shared-character controller factory (docs/enemy-codex.md §5). Scans the
**KayKit Character Animations 1.1** FBX libraries (Rig_Medium 8 FBX + Rig_Large 6 FBX),
builds the HumanoidEnemy/LargeEnemy/Boss/… `.controller` assets into
`Assets/Generated/Animators/`. Params (must match gameplay strings):
`Speed`(float)/`Attack`/`Hit`(trigger)/`Dead`(bool)/`Cast`(trigger). WO-217 enemy
snappiness baked in (EnemyAttackSpeed=1.15). Idempotent (overwrites in place).
- `[MenuItem Defenders/Animation/Build Animator Controllers] BuildAnimators()` — **DESTRUCTIVE** (writes `.controller` assets). **WIRED/LIVE** (called by EnemyAnimatorSetup).

### HeroAnimatorFactory — `Assets/Editor/HeroAnimatorFactory.cs`
Builds per-class hero controllers from **Mixamo clips in `Assets/Action/`** (WO-140;
replaces obsolete Tripo-era HeroAnimatorSetup). Data-driven `HeroSpec[]` for
Knight/Mage/Ranger/Cleric → `Assets/Resources/Heroes/<slug>.controller`. Runtime
contract: `Speed`(float)/`Cast`/`Victory`(trigger) driven by HeroLocomotion/HeroAbilities.
WO-217/218: AttackSpeed=1.3, upper-body attack layer + generated
`HeroUpperBody.mask`, StandingSpeedMax gates full-body vs upper-body swing. Per-type
clip folders (WO-283) Knight/Ranger/Wizard + Shared/. Null-guarded clip loads.
- `[MenuItem Defenders/Animation/Build Hero Animators (Mixamo)] BuildAll()` — **DESTRUCTIVE** (writes 4 controllers + mask). **WIRED/LIVE**.

### DragonAnimatorSetup — `Assets/Editor/DragonAnimatorSetup.cs`
Builds the Black Dragon controller + boss prefab from
`Assets/Black Dragon/Dragon_Baked_Actions_fbx_7.4_binary.fbx` (4 baked takes:
Fly/Idle/Run/Walk; **no Attack or Death clip** — those states reuse Fly/Idle, the
strike/death are code-driven in DragonBoss.cs). Params `Speed`/`Attack`/`Dead`.
DragonBoss MonoBehaviour added by **reflection**. Outputs
`Generated/Animators/Dragon.controller` + `Prefabs/Village/Generated/Boss_Dragon.prefab`.
Idempotent.
- `[MenuItem …/Build Dragon Boss (Controller + Prefab)] BuildAll()` — both. **DESTRUCTIVE**.
- `[MenuItem …/Build Dragon Animator Controller] BuildDragonAnimator()` — **DESTRUCTIVE** (writes controller).
- `[MenuItem …/Build Dragon Boss Prefab] BuildDragonBossPrefab()` — **DESTRUCTIVE** (writes prefab; warns if controller missing). **WIRED/LIVE**.

### EnemyAnimatorSetup (= "EnemyAnimatorFactory") — `Assets/Editor/EnemyAnimatorSetup.cs`
Prepares DTT **runtime** enemy animation: gives 6 KayKit skeleton meshes a Generic
Avatar (`EnsureAvatar`: animationType=Generic, CreateFromThisModel, importAnimation=false),
runs `AnimatorSetup.BuildAnimators()`, then **copies** HumanoidEnemy/LargeEnemy/Boss/
Dragon controllers from `Generated/Animators/` into `Resources/Enemies/` for
`EnemyAnimatorFactory` (runtime) to load. WO-218 layering intentionally skipped for
Generic enemy rigs (documented in header).
- `[MenuItem Defenders/Animation/Setup Enemy Animators (DTT)] Setup()` — **DESTRUCTIVE** (reimports FBX, writes Resources controllers). **WIRED/LIVE**.
- **NOTE:** scope named "EnemyAnimatorFactory" — there is **no such editor file**; the runtime `EnemyAnimatorFactory` lives in gameplay code and this setup feeds it. The editor pass is `EnemyAnimatorSetup`.

---

## BUILD TOOLS

### WebGLBuild — `Assets/Editor/WebGLBuild.cs`
One-shot WebGL player build → `Builds/WebGL/`. IL2CPP, Brotli (or `-noBrotli` for
itch), 512MB, exceptionSupport=ExplicitlyThrownExceptionsOnly (or
`-debugExceptions`=FullWithStacktrace), dataCaching, Minimal stripping. **Ships as
`BuildOptions.Development`** (DevTools panel + stack traces — comment says flip to
`None` for launch).
- `[MenuItem Defenders/Build/WebGL Player] BuildWebGL()` — **DESTRUCTIVE** (writes build dir; `EditorApplication.Exit` on fail). **WIRED/LIVE**.

### DesktopBuild — `Assets/Editor/DesktopBuild.cs`
Windows x64 standalone → `Builds/Windows/DefendersOfTheRealm.exe`, **plus a second
WebGL entry**. Windows build applies crash mitigations: Static Batching OFF (via
reflection `SetBatchingForPlatform` — level3-corruption fix), force Direct3D11 (D3D12
upload-buffer crash), Windowed 1600×900. `BuildOptions.Development`.
- `[MenuItem Defenders/Build/Windows x64 Player] BuildWindows()` — **DESTRUCTIVE**. **WIRED/LIVE**.
- `[MenuItem Defenders/Build/WebGL Player] BuildWebGL()` — Gzip/Minimal/exceptionSupport=None, `BuildOptions.None`. **DESTRUCTIVE**.
- **FLAG (DUPLICATE MENUITEM):** `Defenders/Build/WebGL Player` is registered **twice** — `WebGLBuild.BuildWebGL` **and** `DesktopBuild.BuildWebGL`. Unity binds only one to the menu (the other is shadowed); they have **divergent settings** (Brotli+Development+512MB vs Gzip+None). Direct `-executeMethod` still hits whichever is named. This is a real dead/contradictory-code class — one should be removed or renamed.

---

## QA / GATE TOOLS

### CompileGate — `Assets/Editor/CompileGate.cs`
Headless compile gate: batchmode open forces full recompile; if clean, `Run()` prints
the marker. CLI's authoritative "does the tree compile" check.
- `Run()` (no menu) — logs `COMPILE_GATE_OK :: scripts compiled clean`. **SAFE** (read-only; just a marker). **WIRED/LIVE** (the §11 commit gate).

### RegressionSuite — `Assets/Editor/RegressionSuite.cs`
Per-check-in headless regression battery (WO-329/330/373). Runs independent CASES,
logs single `REGRESSION_OK`/`REGRESSION_FAIL` verdict + per-case PASS/FAIL; batchmode
exits 0/1. Cases: compile-gate, catalog-parse, catalog-byte-equal (Resources vs
StreamingAssets must be byte-equal), data-files-parse, catalog-ids-present,
catalog-prefabs-resolve, structures-kit-present, **no-duplicate-landmines** (checks for
`DeNelle.Core.Debug`/`.Addressables` namespace shadows — memory landmine),
perf-lint-reentrancy, scene-opens-village2, core-wiring-village2, etc. Village types
resolved by **reflection** (FindType). Probes `Village2.unity` (canonical; Village3
removed 2026-06-10).
- `[MenuItem Defenders/QA/Run Regression Suite] RunAll()` → bool. **SAFE-ish** (opens scenes read-only; no save).
- `[MenuItem Defenders/QA/Run Critical Regression Gates] VerifyCriticalGates()` → bool — 4 WO-373 gates: tree-of-life-origin (Heart at 0,0,0), wasd-camera-relative, scene-loads-clean, camera-yaw-is-authority. Logs `CRITICAL_GATES_OK/FAIL`.
- `VerifyTreeOrigin(out detail)` (public) — opens Village2, asserts HeartController at world origin within eps.
- **FLAG:** several gates are **SOURCE-GREP static checks** (e.g. camera-yaw, HeroLocomotion WO-387 basis read from `HeroLocomotion.cs` text). A source-grep gate trusts the file's text — **vulnerable to the exact stale-comment-vs-code class** (see FLAGS). **WIRED/LIVE** (the §11 regression gate).

### SpawnPathVerifier — `Assets/Editor/SpawnPathVerifier.cs`
WO-27 loop verification: opens the baked Village scene, for each `WaveSpawnPoint`
checks it samples onto NavMesh within 8m and that a NavMesh path to the Heart is
PathComplete. Logs PASS/CHECK.
- `[MenuItem Defenders/Week 3/Verify Spawn Paths (NavMesh)] VerifySpawnPaths()` — opens **`Village.unity`** (Single). **SAFE** (read-only; no save).
- **FLAG (STALE):** targets the abandoned `Village.unity` (Village2 is canonical). Likely **stale** — verifies the wrong scene's spawn routing.

---

## MAGENTA / MATERIAL TOOLS

### MagentaMaterialScanner — `Assets/Editor/MagentaMaterialScanner.cs`
READ-ONLY scan for magenta renderers across ALL prefabs + ALL build scenes (causes:
null sharedMaterial, null shader, Hidden/InternalErrorShader). Writes CSV to
`Builds/MagentaScan/magenta_scan.csv`. WO-409 Bug 1 diagnosis.
- `[MenuItem Defenders/Art/Scan Magenta Materials] Run()` — **SAFE** (no asset modified; opens scenes read-only; writes a CSV report).
- `ClassifyMaterial(Material, out shaderName, out matName)` (public helper).
- **WIRED/LIVE**.

### MagentaMaterialFixer — `Assets/Editor/MagentaMaterialFixer.cs`
Repairs the magenta renderers (idempotent): built-in/error-shader materials → URP/Lit
(carrying `_Color`→`_BaseColor`, `_MainTex`→`_BaseMap`); null slots → a shared
`Assets/Materials/MagentaFix_DefaultLit.mat`. Delegates to `PolyperfectUrpFix.Fix()`
first. In-place same-GUID swaps (no re-bake needed).
F8-49 pass: prefab renderer slots referencing Unity's read-only BUILT-IN legacy
particle materials (`Default-Particle`, Resources/unity_builtin_extra — `Legacy
Shaders/Particles/Alpha Blended Premultiply`, magenta under URP) → a shared
`Assets/Materials/MagentaFix_DefaultParticle_URP.mat` (URP Particles/Unlit,
premultiply blend + built-in Default-Particle glow texture). Hovl/Mirza packs are
gitignored, so this pass is the durable source fix — re-run after pack re-import.
- `[MenuItem Defenders/Art/Fix Magenta Materials] Run()` — **DESTRUCTIVE** (mutates material assets + prefab/scene slots; SaveAssets).
- `[MenuItem Defenders/Art/Fix Built-in Particle Materials (F8-49)] FixBuiltinParticles()` — **DESTRUCTIVE** (prefab slot swaps only; batchmode: `DeNelle.Editor.MagentaMaterialFixer.FixBuiltinParticles`).
- `UpgradeMaterialToUrp(Material, Shader lit)` → bool (public; idempotent).
- Deps: URP/Lit + URP Particles/Unlit shaders, `PolyperfectUrpFix`. **WIRED/LIVE**.

---

## DATA / RECIPES

### `Assets/Resources/Data/castle-south-recipe.json` (512 B)
Schema: `{ "pieces":[{name,prefab,pos[3],rot[3],scale[3]}], "parentPos"[3], "parentRot"[3] }`.
Count: **4 pieces** (Gate_South, Wall_South_L, Wall_South_R, CornerTower_South),
parent at origin. Written by `CastleOffsetCapture.Capture`; consumed by
`CastleWallsFromRecipe.Recreate` (loaded as `Resources.Load<TextAsset>("Data/castle-south-recipe")`).
Copy note: single copy under Resources (NOT a dual Resources/StreamingAssets canonical
catalog — does not go through the byte-equal regression gate).

### `Assets/Resources/Data/Canonical/garrison-recipes.json` (1.9 KB)
Schema: array of garrison recipes (parsed by `GarrisonRecipeCatalog` →
`DeNelle.Core.World.GarrisonRecipe`: id, kind, theme/lighting/element, size, levelRange,
threat, enemies[], props/roles). Count: **4 recipes** — `troll_outpost`, `ruined_keep`,
`hill_fort`, `frost_keep`. Consumed by `GarrisonSceneBuilder.BuildAll/BuildById`.
Lives under the Canonical/ tree (dual Resources/StreamingAssets convention applies).

---

## FLAGS

### Stale comment vs. code (the named-risk class)
- **`HeroLocomotion.cs` (`Assets/_Modules/Village/Hero/HeroLocomotion.cs`) — CONFIRMED MISMATCH.** Header comment line 5: *"(no Rigidbody, no NavMeshAgent — pure transform)"*. The code is a **NavMeshAgent**: field `_agent` (L205), `GetComponent<NavMeshAgent>()` + `AddComponent<NavMeshAgent>()` (L242-243), `NavMeshAgent.Move` driving (L240), agent suspended for rampart lift (L670). The "pure transform" comment is **stale/false**. This matters doubly because **RegressionSuite source-greps this very file** for the WO-387 camera-yaw basis — a source-grep gate can be fooled by a comment that no longer matches code.
- **`CastleOffsetCapture.cs`** header names the recreate builder **"CastleSideFromRecipe"**; the real class is **`CastleWallsFromRecipe`** (stale name).

### Dead / duplicate code
- **DUPLICATE MenuItem `Defenders/Build/WebGL Player`** in both `WebGLBuild.BuildWebGL` and `DesktopBuild.BuildWebGL`, with **contradictory settings** (Brotli/Development/512MB/ExplicitlyThrown vs Gzip/None/None). Only one binds to the menu; ambiguous & contradictory — consolidate.
- Scope-named **"EnemyAnimatorFactory"** has **no editor file**; the editor entry is `EnemyAnimatorSetup` (the runtime `EnemyAnimatorFactory` is gameplay code it feeds).

### Scene-gated / stale-target
- **`OuterWorldBuilder.BakeWorldNavMesh`** opens & re-saves the **corruption-cursed `Village.unity`** (CLAUDE.md §3). Village.unity is abandoned (Village2 canonical). Legacy & risky — `OuterWorldNavBake.Bake` (solo, OuterWorld-only) is the safe replacement.
- **`SpawnPathVerifier.VerifySpawnPaths`** opens the abandoned **`Village.unity`**, not canonical Village2 — likely verifying the wrong scene.

### Contradictory / latent
- `WebGLBuild` + `DesktopBuild` both ship **`BuildOptions.Development`** for the "ship" path (DevTools panel compiled in) — comments say flip to `None` for launch; easy to forget → a dev panel leaks into a release build.
- Build/bake tools that `EditorSceneManager.SaveScene` or `EditorApplication.Exit` are batchmode-destructive: never run while the editor is open on that scene (project-lock / §3).

---

*Cataloged 21 editor tools/files + 2 data recipes (scope: Castle×5, OuterWorld×2,
Garrison×2 partials, Animator factories×4, Builds×2, QA gates×3 [CompileGate/
RegressionSuite/SpawnPathVerifier], Magenta×2). Full `Assets/Editor` contains ~100
.cs files; this catalog covers the explicitly-scoped subset.*
