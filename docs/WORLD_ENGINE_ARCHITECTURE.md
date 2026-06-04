# World Engine Architecture — the generic typed-dispatch substrate (implementation-ready)

> Hand-off doc for **UI** (the coding agent). CLI compile-gates + commits.
> The **wider** layer above `docs/CHARACTER_REFACTOR_PLAN.md`: that doc unifies actors
> (Hero/Enemy/Pet/Townsfolk) into one `Character` + `Brain` + `Equipment` + `CharacterFactory`.
> **This doc does NOT redo any of that** — it wraps it. The character engine becomes one *domain*
> behind a generic dispatcher that also drives **terrain, weather, structures, and player-built
> content** through the same seam: you pass the engine a typed `def`, a top-level dispatcher
> delegates to the right sub-controller, and everything in the world is built/driven one way.
>
> Routes against `docs/NORTH_STAR.md` (the CREATE verb), `docs/ARCHITECTURE_NORTH_STAR.md`
> (#1 data-driven, #2 server-authoritative, #3 deterministic/headless, #4 swappable-behind-interfaces),
> `docs/build-mode-architecture.md` (player layout = data), and `docs/world-construction-plan.md`
> (outward-in ring order). **This is EXTRACTION toward a dispatcher, not greenfield.** Every existing
> builder keeps building the world at every phase.

---

## 0. The one-paragraph thesis

The rampart already proved the core trick, and the character plan already proved the generalization
of *actors*. Generalize **once more, one level up**: every piece of the world — an actor, a hill, a
storm, a wall, a player-dropped platform — is authored as a typed **`WorldDef`** and handed to a
single **`EngineDispatcher.Build(def)`**. The dispatcher owns nothing but a **registry of typed
handlers** (`IBuildHandler`), looks up the handler for `def`'s concrete type, and delegates. The
character engine's `CharacterFactory` becomes the `CharacterDef` handler — unchanged. New handlers
(`TerrainController`, `WeatherController`, `StructureController`) each do one cohesive job. And the
punchline: **a player-built object is just another `def` the same dispatcher consumes** — build mode
is the engine pointed at player input, nothing bespoke. The substrate underneath all walkable world
is the **`NavSurface` decouple**: visual mesh (what it looks like) + an invisible nav-static plank
(where you can walk) — exactly what `BuildRamparts` shipped 2026-05-30.

---

## 1. The core insight to generalize — `NavSurface` (visual ⊥ navigable)

`VillageSceneBuilder.BuildRamparts` (lines ~3042–3136) ships the load-bearing pattern: a climbable
piece of world is **TWO parallel objects** —

- an **invisible nav plank** — a `Cube` box flagged `StaticEditorFlags.NavigationStatic`, renderer
  **disabled**, sloped under the 45° NavMesh limit (`~29°`), and
- a **visual** — the designed `Stairs_Medieval_Stone` prefab, colliders + rigidbodies stripped,
  sitting on top.

The bake (`BakeVillageNavMesh`, line ~4296, legacy `UnityEditor.AI.NavMeshBuilder.BuildNavMesh()`)
reads **only** `NavigationStatic` flags, so it connects `ground → ramp → walkway`. Hero AND enemies
share that mesh up to the rampart. **What it looks like and where you can walk are fully decoupled.**

Promote that ad-hoc pair into a **reusable abstraction**. Anything climbable/walkable = visual +
`NavSurface`.

```csharp
// New: Assets/_Modules/Environment/Nav/NavSurface.cs   (DeNelle.Village or a new DeNelle.Environment)
[DisallowMultipleComponent]
public sealed class NavSurface : MonoBehaviour
{
    // The invisible walkable plane. At edit/build time it is flagged NavigationStatic
    // and rendered-off; the visual mesh is a sibling/child with colliders stripped.
    public float maxSlopeDeg = 44f;       // stays under the bake's 45° agent limit
    public bool  isObstacle;              // walls/buildings: nav-static BUT blocks (no walkable top)

    // Editor-time: OR NavigationStatic onto this GO (mirrors BuildRamparts + BakeVillageNavMesh).
    // Runtime (player-built): attach a NavMeshObstacle w/ carving instead of a rebake (see §4).
}

// Authoring helper — the ONE place the "visual + plank" pair is built (lifts BuildRamparts' Box/Ramp):
public static class NavSurfaceFactory
{
    // visual = designed mesh (colliders/rigidbodies stripped); plank = invisible nav-static box.
    public static GameObject CreateWalkable(GameObject visual, Vector3 from, Vector3 to, float width);
    public static GameObject CreateFlat   (GameObject visual, Bounds footprint);   // walkways, platforms, biome floors
    public static GameObject CreateObstacle(GameObject visual, Bounds footprint);  // walls/buildings (nav-static blocker)
}
```

This is the spine of the whole world engine: **stairs, ramps, mountains, hill slopes, biome floors,
bridges, and player-built platforms are all `visual + NavSurface`.** A mountain you can climb is a
visual peak mesh + a `NavSurface` whose plank stays under `maxSlopeDeg`; a wall is a visual + an
`isObstacle` NavSurface. The `TerrainController` and `StructureController` (§3) both emit through
`NavSurfaceFactory`, so the rampart's hand-rolled decouple becomes the universal walkability contract.
**Why it matters:** "the hero can defend on top and enemies path up to attack" generalizes to *any*
elevated or sloped geometry without a bespoke nav hack per feature.

---

## 2. The generic dispatcher — `EngineDispatcher.Build(def)`

One entry point. A typed `def` in, the right controller does the work, via a **registry of typed
handlers behind one interface**. Not a god-object — the dispatcher is ~30 lines; all knowledge lives
in the cohesive handlers it delegates to.

```csharp
// New: Assets/_Modules/Core/Data/WorldDef.cs        (DeNelle.Core.Data — authorable anywhere)
public abstract class WorldDef : ScriptableObject { public string Id; }
// Concrete defs (each in Core.Data): CharacterDef : WorldDef   (FROM the character plan — reused as-is)
//   TerrainDef : WorldDef · WeatherDef : WorldDef · StructureDef : WorldDef · (future) DecorDef, ZoneDef …

// New: Assets/_Modules/Core/Engine/IBuildHandler.cs (DeNelle.Core — the ONE seam)
public interface IBuildHandler
{
    System.Type DefType { get; }                 // the WorldDef subtype it consumes
    GameObject Build(WorldDef def, BuildContext ctx);   // instantiate + wire; returns the root
}
public readonly struct BuildContext            // pos/rot/parent + grid + flags (edit vs runtime)
{ public readonly Vector3 Pos; public readonly Quaternion Rot; public readonly Transform Parent;
  public readonly bool RuntimePlaced; /* player-built → carve, don't rebake */ }

// New: Assets/_Modules/Village/Engine/EngineDispatcher.cs   (DeNelle.Village — references Core only)
public sealed class EngineDispatcher
{
    readonly Dictionary<System.Type, IBuildHandler> _handlers = new();

    public void Register(IBuildHandler h) => _handlers[h.DefType] = h;   // typed registration

    public GameObject Build(WorldDef def, BuildContext ctx)              // typed dispatch
    {
        if (def == null) return null;
        if (!_handlers.TryGetValue(def.GetType(), out var h))
        {
            Debug.LogWarning($"[EngineDispatcher] no handler for {def.GetType().Name} ({def.Id})");
            return null;                                                 // LogWarning, never throw (CLAUDE.md §4 spirit)
        }
        return h.Build(def, ctx);
    }
}
```

**Registration (composition root, e.g. a `WorldEngineBootstrap` MonoBehaviour or the scene builder):**

```csharp
dispatcher.Register(new CharacterController(characterFactory));  // wraps CharacterFactory from the char plan
dispatcher.Register(new TerrainController());
dispatcher.Register(new WeatherController());
dispatcher.Register(new StructureController());
// later: dispatcher.Register(new DecorController()); … additive, no dispatcher edit.
```

A new world domain = a new `WorldDef` subtype + a new `IBuildHandler` + one `Register` line. The
dispatcher never changes (open/closed). This is North Star #1 (data-driven) and #4 (swappable behind
interfaces) applied to the **whole world**, not just actors.

---

## 3. Domain controllers (each: visual + logic, data-driven, one cohesive job)

Each is an `IBuildHandler`. They build through `NavSurfaceFactory` (§1) for anything walkable and
route effects through the existing managers — **no new VFX/nav systems are invented.**

### 3.1 `CharacterController` — defers to the character plan (do NOT reimplement)

```csharp
public sealed class CharacterController : IBuildHandler {
    public System.Type DefType => typeof(CharacterDef);
    readonly CharacterFactory _factory;                          // FROM CHARACTER_REFACTOR_PLAN (WO-114)
    public GameObject Build(WorldDef def, BuildContext ctx)
        => _factory.Create((CharacterDef)def, ctx.Pos, ctx.Rot).gameObject;  // ONE line — it already does it all
}
```

This is a **thin adapter**, not a rewrite. All actor logic (`Character`, `Brain`, `Equipment`,
`ActionSet`, `CharacterFactory`) stays exactly as the character plan specifies. The dispatcher just
gains "characters are one kind of `def`."

### 3.2 `TerrainController` — ground / hills / mountains / biome floors (visual mesh + NavSurface)

Builds the **visual** (terrain plane / sculpted mesh / polyperfect `Terrain_Plane_*` from the
world-construction catalog) and a parallel **`NavSurface`**: flat biome floors via
`NavSurfaceFactory.CreateFlat`, climbable hills/mountains/slopes via `CreateWalkable` with the plank
held under `maxSlopeDeg`. **This is the rampart trick applied to landscape** — a mountain is climbable
because its visual peak has a sub-45° nav plank under it; impassable peaks get an `isObstacle` surface.
Reuses `ExteriorTerrainBuilder`'s splat/biome/Y=0-seam logic as the visual source; reuses
`world-construction-plan.md`'s `Terrain_Plane_Slope1–4` transition tiles as the walkable inclines.
`TerrainDef`: biome palette, footprint, slope profile, walkable/obstacle flag.

### 3.3 `WeatherController` — atmosphere via existing managers (no new effect system)

A **stateless façade** over the already-built `WeatherManager` (rain/shooting-stars/snow, pooled,
quality-gated), `SkyProgressionController` (fog/ambient/sun lerp over `RenderSettings`), and
`VFXManager` (pooled VFX). `WeatherController.Build(WeatherDef)` applies a named preset: set
`RenderSettings` fog/ambient targets, call `WeatherManager.Instance.ToggleRain/SetRainIntensity`,
play ambient `VFXManager` loops. **Weather is data** (`WeatherDef`: fog density/color, rain intensity,
sky palette, ambient VFX list) so a "storm during wave 8 in Mirewood" is an asset, not code. Honors
the two-combat-feel-stack rule (MEMORY): route through `VFXManager` only, don't double-fire.

### 3.4 `StructureController` — buildings / walls / ramparts / gates (visual + obstacle/walkable NavSurface)

Builds the visual prefab + the footprint blocker + the `NavSurface` (walls = `isObstacle`; ramparts
= `CreateWalkable`). Reuses verbatim: `VillageSceneBuilder.AddBuildingFootprintCollider` (mesh-bounds
box), `Building.EnsureBlocker`, `Building.Configure(BuildingDef)`, and the `BuildRamparts` plank/visual
pair (now via `NavSurfaceFactory`). `StructureDef` folds today's `BuildingDef`/`TowerData`/`WallLevel`
as the data source. This is the controller `build-mode-architecture.md`'s `BaseLayoutLoader`
ultimately calls per placed object.

---

## 4. Player-created content is first-class (the punchline)

A player-built object is **just another `def` the same dispatcher consumes.** Build mode = the engine
pointed at player input. This is the exact seam `build-mode-architecture.md` already designs toward —
this doc makes it literal.

```
Player taps a palette card  →  BuildModeController resolves it to a WorldDef (StructureDef/TerrainDef/…)
   →  PlacementGrid validates (CanPlace + surface + gate-clearance + affordable — all EXISTING rules)
   →  EngineDispatcher.Build(def, ctx{ Pos=CellToWorld(cell), RuntimePlaced=true })   ← SAME call as the builder
   →  append PlacedStructureData{ itemId, cellX, cellZ, yawSteps, level } to GameState.BaseLayout (persist)
```

The seam: **`BaseLayoutLoader` (designer/runtime authoring) and `BuildModeController` (live player
authoring) call the IDENTICAL `EngineDispatcher.Build(def, ctx)`.** Nothing about a player-placed wall
differs from a designer-placed wall except `ctx.RuntimePlaced` — which only tells the `NavSurface` to
**carve a `NavMeshObstacle`** into the baked mesh instead of triggering an editor rebake (the §1
runtime path; matches `build-mode-architecture.md` §7 risk #1). `PlacedStructureData` (grid cell +
discrete yaw) stays the persisted/server-replayable form; it resolves to a `WorldDef` on load. So
build-mode placement, designer authoring, and (later) server-side raid reconstruction are **one code
path** — North Star #1/#2/#3 fall out of the dispatcher being the only builder.

---

## 5. Reconciliation — reuse vs gap (extraction toward a dispatcher, not greenfield)

| Concern | Today (REUSE) | Where | Gap (this plan) |
|---|---|---|---|
| **Walkable/visual decouple** | invisible nav-static plank + designed-stair visual; bake reads only `NavigationStatic` | `VillageSceneBuilder.BuildRamparts` (~3042), `BakeVillageNavMesh` (~4296) | Extract into `NavSurface` + `NavSurfaceFactory` (the §1 abstraction) |
| **Actor build path** | `Character`/`Brain`/`Equipment`/`CharacterFactory` | `CHARACTER_REFACTOR_PLAN.md` (WO-106..118) | Wrap in a 1-line `CharacterController` adapter — **do not touch** |
| **Terrain visual** | Unity Terrain, 4 biomes, splats, Y=0 seam plateau, trees | `ExteriorTerrainBuilder.BuildExterior` | Becomes `TerrainController`'s visual source; add the `NavSurface` parallel |
| **Climate zones** | planned sectored zones + `ZoneManager` | `world-construction-plan.md` (WO-107) | `ZoneDef` handler later; aligns to `TerrainController` |
| **Weather/atmosphere** | rain/stars/snow pooled; sky fog/ambient/sun lerp; pooled VFX | `WeatherManager`, `SkyProgressionController`, `VFXManager` (all `DeNelle.Village`) | `WeatherController` façade + `WeatherDef`; **no new effect system** |
| **Structure footprint + blocker** | mesh-bounds box, blocker, `Configure(def)` | `AddBuildingFootprintCollider`, `Building.EnsureBlocker/Configure` | Reused verbatim inside `StructureController` |
| **Placement / grid / ghost / cost** | ghost, snap, overlap, gate-clearance, spend | `TowerPlacementSystem`, `PlacementGrid` (planned), `EconomyService`, `GameState.BaseLayout` | Feeds `EngineDispatcher.Build`; placement is unchanged |
| **Runtime nav for placed objects** | bake is editor-only | `BakeVillageNavMesh` | `NavMeshObstacle` carving on `RuntimePlaced` (build-mode §7 #1) |
| **NavMesh bake** | legacy `UnityEditor.AI.NavMeshBuilder`, reads `NavigationStatic` | `BakeVillageNavMesh` | Unchanged; `NavSurface` just OR-flags `NavigationStatic` like ramparts already do |

**Single biggest reuse:** the **rampart's visual+nav-static-plank decouple is already live and baked**
(`BuildRamparts` + `BakeVillageNavMesh`). The entire walkability spine of the world engine is a
generalization of code that shipped 2026-05-30 — not a new nav system. Second-biggest: `WeatherManager`
/ `SkyProgressionController` / `VFXManager` already are the atmosphere engine; `WeatherController` is a
data-fronted façade, zero new effects.

> **Serialization bottleneck (CLAUDE.md §9).** `VillageSceneBuilder.cs` is one-editor-at-a-time and is
> **currently owned by another agent.** This plan **reads** it (rampart/footprint/bake patterns) and
> **never writes** it. `NavSurface`, `EngineDispatcher`, and the controllers are **new files**; the
> builder later *calls* `dispatcher.Build(...)` (a small, coordinated WO), it is not rewritten. All new
> code lands in its own files so the builder lane and the engine lane stay parallel-safe.

---

## 6. Phased migration + risks (each phase ships; world keeps building)

Assembly rules (CLAUDE.md §5): **Village → Core only; HUD passive via Core seam; never Village ↔ HUD.**
`WorldDef`/`IBuildHandler`/`BuildContext`/`CharacterDef`/`TerrainDef`/`WeatherDef`/`StructureDef` go in
**`DeNelle.Core` / `DeNelle.Core.Data`** (authorable anywhere). `EngineDispatcher` + the controllers go
in **`DeNelle.Village`**. `NavSurface` may live in `DeNelle.Village` (or a thin new `DeNelle.Environment`
referencing Core only) — keep it Core-clean either way. Never big-bang; the world builds at every rung.

- **Phase W0 — `NavSurface` extraction (no behavior change).** Create `NavSurface` + `NavSurfaceFactory`
  lifting `BuildRamparts`' `Box`/`Ramp` lambdas. Builder still authors ramparts its own way; the new
  type is additive and proven against the existing bake. *Ships: identical world; the decouple is now a
  reusable type.*
- **Phase W1 — dispatcher + interfaces (pure additive).** `WorldDef`, `IBuildHandler`, `BuildContext`,
  `EngineDispatcher`. Nothing registered yet. Compiles, loop unchanged.
- **Phase W2 — `CharacterController` adapter.** Wrap `CharacterFactory` (must exist — char-plan WO-114).
  Register it; route ONE caller (e.g. a test spawn) through `dispatcher.Build(characterDef)` to prove the
  seam. *Ships: characters built two ways, identical result.*
- **Phase W3 — `StructureController`.** Reuse footprint/blocker/`Configure` + `NavSurface`. Build one
  structure type through the dispatcher. *Ships: a building placed via the generic path.*
- **Phase W4 — `WeatherController` façade + `WeatherDef`.** Data-fronted preset apply over the existing
  managers. *Ships: weather presets are assets.*
- **Phase W5 — `TerrainController` + climbable `NavSurface`.** Visual from `ExteriorTerrainBuilder`
  patterns + parallel nav plank; prove a hill the hero climbs and enemies path up (the §1 generalization).
- **Phase W6 — build-mode seam.** Point `BuildModeController`/`BaseLayoutLoader` at
  `EngineDispatcher.Build` with `RuntimePlaced=true` + `NavMeshObstacle` carving. *Ships: player-built
  objects flow through the same engine.*
- **Phase W7 — builder calls the dispatcher (coordinated).** A single WO has `VillageSceneBuilder` call
  `dispatcher.Build(...)` for newly-authored pieces. Coordinate the bottleneck via WO; do not rewrite the
  builder's scene authoring.

**Biggest risk — runtime NavMesh on player edits.** A full rebake per placement is too costly on a phone
(`build-mode-architecture.md` §7 #1). **Mitigation:** `NavSurface` carves a `NavMeshObstacle` at runtime
(no rebake); the gate-clearance rule guarantees a spawn→Heart lane always exists; fall back to one rebake
on build-mode `Exit()` if carving proves too heavy on low-end devices. Verify enemy paths from every
`spawn-N` to the Heart after any walkable change. Secondary risks: keep the dispatcher a thin registry
(resist a god-object — all logic in handlers); never hand-edit `Village.unity` (corruption-on-resave,
CLAUDE.md §3) — all world changes go through builders/handlers + a rebake; and don't double-fire VFX
across the two combat-feel stacks (MEMORY) — `WeatherController` routes through `VFXManager` only.

---

## 7. Work-order breakdown (continues after char-plan WO-118)

Each WO = one UI implementation + one CLI compile-gate; brace gate per `.cs` (CLAUDE.md §1). Order
respects the phases; the world keeps building after each.

- **WO-119 — `NavSurface` + `NavSurfaceFactory`.** Lift `BuildRamparts`' plank/visual pair (Box/Ramp,
  `CreateWalkable`/`CreateFlat`/`CreateObstacle`); OR-flag `NavigationStatic` exactly as the builder does;
  `isObstacle` + `maxSlopeDeg` honored. Additive; builder untouched. (W0)
- **WO-120 — Engine contracts.** `WorldDef` (+ `CharacterDef` reparent to `WorldDef`), `IBuildHandler`,
  `BuildContext` in `DeNelle.Core`/`Core.Data`. Pure additive. (W1)
- **WO-121 — `EngineDispatcher` + registry.** `Register`/`Build`, LogWarning on missing handler; unit-
  testable, no scene dep. (W1)
- **WO-122 — `CharacterController` adapter + register.** 1-line wrap of `CharacterFactory`; route a test
  spawn through `dispatcher.Build`. Depends on char-plan WO-114. (W2)
- **WO-123 — `StructureController` + `StructureDef`.** Reuse `AddBuildingFootprintCollider`/
  `Building.EnsureBlocker`/`Configure` + `NavSurface`; build one structure via the dispatcher. (W3)
- **WO-124 — `WeatherController` + `WeatherDef`.** Façade over `WeatherManager`/`SkyProgressionController`/
  `VFXManager`; preset apply; VFXManager-only routing. (W4)
- **WO-125 — `TerrainController` + `TerrainDef`.** Visual from `ExteriorTerrainBuilder` patterns +
  climbable/obstacle `NavSurface`; prove a climbable hill (hero up, enemies follow). (W5)
- **WO-126 — Build-mode → dispatcher seam.** `BuildModeController`/`BaseLayoutLoader` call
  `EngineDispatcher.Build(def, RuntimePlaced=true)`; `NavMeshObstacle` carving; `PlacedStructureData`
  resolves to a `WorldDef`. Reuses placement/grid/economy unchanged. (W6)
- **WO-127 — Builder calls the dispatcher (coordinated).** `VillageSceneBuilder` routes newly-authored
  pieces through `dispatcher.Build`; coordinate the serialization bottleneck via WO; scene authoring
  otherwise untouched. (W7)
- **WO-128 — (stretch) Headless build proof.** Run `EngineDispatcher.Build` over a `List<WorldDef>`
  with no scene/live deps (server-replay smoke test) — the North Star #2/#3 substrate check. (validates
  the seam)

---

*End of plan. Build it as extraction: `NavSurface` first (it already shipped as ramparts), then the
dispatcher wrapping the character engine and the new domain controllers, with player-built content as
just another `def`. Keep the dispatcher a thin typed registry and the world keeps building at every rung
— that registry is how "designer authors the village" and "player authors their base" and "server
replays a raid" become one code path.*
