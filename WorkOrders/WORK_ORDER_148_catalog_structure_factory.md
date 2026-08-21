**Status:** CLOSED — owner range sweep 2026-08-21 (WO 0-800): completed or immaterial.

<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK ORDER 148 — Catalog Structure Factory (ONE creation path, TWO callers)

**Status: READY TO IMPLEMENT**
**Priority: HIGH — P0 keystone.** This is the unwritten engine seam from
`docs/PLAYER_BASE_DESIGN_CATALOG_ROADMAP.md` (§C "P0 — prove the data path"): it turns the inert
Part A catalog into a *real, repeatable structure-creation process*. Everything downstream
(WO-136 castle rewrite, WO-108 player Build Mode, WO-139/140 placement + structural content) consumes it.
**Lane:** Catalog / code — **Core (verify-only) + Village (new `StructureFactory` + bootstrap +
generalize `TowerPlacementSystem`) + a thin `DeNelle.Editor` wrapper.** Runs in the Combat/AI + catalog
code lane (CLAUDE.md §9). **Does NOT touch the frozen `VillageSceneBuilder.cs` body and bakes no scene.**
**Created:** 2026-05-30
**Depends on:** WO-137 **Part A — DONE** (catalog data model compiles green; verified below). Part B
(`DefensiveCatalog`/`CatalogTowerFactory`) is **superseded by this WO** — its `CatalogTowerFactory` is
folded into the more general `StructureFactory` here (do not build a tower-only factory in parallel).

---

## The owner's core insight (honor it literally)

> The castle rewrite (WO-136) must NOT hand-code bespoke wall/rampart geometry. It must author
> structures through the **same** catalog creation methods the player will eventually use to place
> buildings. "Build the castle" (editor, bake-time) and "player places a wall" (runtime) become the
> **same operation — ONE creation path, TWO callers.**

This makes structure creation a **repeatable process**: every future structure — castle, dungeon,
region ruin, player base — is just more catalog **DATA** fed through one factory, not new builder code.
This is "enhance the wheel / don't reinvent" applied at the *authoring* level.

**North Star tie-in (`docs/NORTH_STAR.md`):** this is the engine under the CREATE verb (CoC × Warcraft
base-building, placement = role). The factory is the content engine the whole base-design business model
sits on — rungs 4–6 of the delivery ladder. One factory → every structure becomes data.

---

## Verified current state (read before implementing)

| Fact | Evidence |
|---|---|
| **`CatalogRegistry` is EMPTY at runtime** — the P0 finding | `CatalogRegistry.Register(...)` has **no caller anywhere in `Assets/`** (grep: only the method definition in `Assets/_Modules/Core/Catalog/CatalogRegistry.cs:20` + docs/WO-137 prose). Nothing populates it → `OfType()`/`Get()` return empty at runtime. The data path is unproven in-game. |
| Part A data model is BUILT & pure | `Assets/_Modules/Core/Catalog/`: `CatalogEntry` (`id, displayName, type, kind, visualPrefabPath, RepoProps repo, CellPlacement[] composite`), `RepoProps` (`navSurface, buildCost, behaviorId, PlacementRules placement, range, damage, fireRate, canHitAir, element`), `PlacementRules` (`mustSitOn, noOverlap, footprint, minDistanceFromGate, requiresSupport, checkAffordable, ownedGate`), `CatalogRegistry` (`Register/Get/OfType/Count/Clear`), `CatalogType`/`EntryKind`/`NavSurfaceKind`/`PlacementSurface`. Namespace `DeNelle.Core.Catalog`, assembly `DeNelle.Core`. **No Village refs.** |
| `behaviorId` is never resolved anywhere | grep: the string is declared in `RepoProps.cs` and described in docs, but **no code maps it → a MonoBehaviour**. The string→component bridge does not exist yet. This WO builds it. |
| Runtime prefab loading = **`Resources.Load`** | `Assets/_Modules/Village/VisualFactory.cs:59` — `VisualFactory.Skin(host, resourcesPath, SkinOptions)` does `Resources.Load<GameObject>(path)`, instantiates under host, fits/seats/strips/URP-fixes, `LogWarning`+null on miss. **This is the runtime-safe skinner the factory must reuse — do NOT add an Addressables path for structures.** (Addressables exists but is scoped to skins/UI, not structures.) |
| Editor builder uses **AssetDatabase** (a separate path) | `Assets/Editor/VillageSceneBuilder.cs:2346` `LoadModel` (`AssetDatabase.LoadAssetAtPath` + `.prefab` fallback) and `:2365` `InstantiateModel` (`PrefabUtility.InstantiatePrefab` + `ForceHexMaterial`). VisualFactory's own header notes the two asmdefs "can't share without a reference, so this mirrors that logic for the runtime side." |
| Existing runtime placement is tower-only | `Assets/_Modules/Village/Buildings/TowerPlacementSystem.cs` — singleton ghost loop: `StartPlacing(TowerData)`, `IsValidSurface` (flat upward face, not on Tower/Building), `SnapToGrid`, `CanPlace` (`EconomyService.CanAfford` + `SkillSystem.HasRequiredSkill` + `Physics.OverlapSphereNonAlloc` no-overlap), `PlaceTower` (`EconomyService.Spend` → `TowerConstructionQueue.AddToQueue`). Hard-wired to `TowerData` (`DeNelle.Core.Data`); ignores `PlacementRules.mustSitOn`. **Generalize, do not fork.** |
| The proven behavior | `Assets/_Modules/Village/Buildings/DefenseTower.cs` — `Range/Damage/FireRate/CanHitAir/Element` public fields = exactly the `RepoProps` combat fields. The factory copies repo → these. |
| WO-136 castle still uses bespoke geometry | `WORK_ORDER_136_castle_structure_ramparts.md` + `VillageSceneBuilder.cs:2801 BuildWallPerimeter` (driven by `WallLayout.Segments`/`.Gates`). The castle rewrite is the **editor caller** that will consume this factory instead of hand-coding wall placement. |

**Verdict:** the data model + a placement runtime + a proven behavior all exist but have **never been
connected end-to-end by data.** This WO builds the single factory that connects them, plus the bootstrap
that populates the registry, plus the two thin caller seams.

---

## The load-bearing architectural decision — TWO contexts, ONE core method

The factory must work in two contexts, so its **core creation logic is RUNTIME-SAFE** and an editor-only
wrapper layers bake-time concerns on top:

1. **RUNTIME** (player Build Mode, WO-108): places structures live. **NO `UnityEditor` dependency.**
   Loads via `Resources.Load` (through `VisualFactory`). This is the canonical path.
2. **EDITOR / bake-time** (the future-unfrozen `VillageSceneBuilder` + WO-136 castle rewrite): authors
   `Village.unity`. May use editor APIs (`PrefabUtility`, static flags, `Undo`, `AssetDatabase`).

=> **The crux:** `StructureFactory.Create(CatalogEntry → GameObject)` lives in **`DeNelle.Village`** and
is **runtime-safe** (no `using UnityEditor`). A thin **`DeNelle.Editor`** wrapper
(`StructureFactoryEditor`) calls the same core method and adds *only* bake-time concerns (mark static,
register `Undo`, optionally swap the runtime `Resources.Load` for an `AssetDatabase`/`PrefabUtility`
instantiate so the bake produces real prefab instances, not runtime clones). **One creation path, two
callers.** No logic is duplicated between them — the editor wrapper delegates placement/behavior/rules to
the runtime core and decorates the result.

---

## Goal

Build the single structure-creation factory + the registry bootstrap, and wire the two callers to it:

1. **`CatalogBootstrap`** — populates `CatalogRegistry` at startup (fixes the P0 "empty registry").
2. **`StructureFactory.Create(entry, pose, parent)`** — the ONE runtime-safe creation method:
   resolve `visualPrefabPath` → instantiate → attach the `behaviorId`-named component → apply
   `PlacementRules` → parent/position. Null-guarded throughout (LogWarning, never throw).
3. **`StructureFactory.CreateGroup(group, originPose, parent)`** — compose multiple entries as one unit
   (a "castle" = a group of wall/tower/gate entries with relative poses).
4. **`StructureFactoryEditor`** (thin `DeNelle.Editor` wrapper) — bake-time decoration over (2)/(3).
5. **Generalize `TowerPlacementSystem`** — add a `StartPlacing(CatalogEntry)` overload that places ANY
   catalog entry via the factory and honors `PlacementRules.mustSitOn`. The existing `TowerData` path
   stays working (additive; bridged, not forked).

---

## Section 1 — `CatalogBootstrap` (populate the registry; the P0 fix)

**File (new):** `Assets/_Modules/Village/Catalog/CatalogBootstrap.cs` — `DeNelle.Village`,
`namespace DeNelle.Village.Catalog`.

- A `static` class with a `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]`
  entry point — the established self-bootstrap pattern in this repo (`WaveSystemBridgeBootstrap.cs`,
  `AudioBootstrap.cs`, `DevBootstrap.cs`). No scene wiring required.
- On run: `CatalogRegistry.Clear()` (domain-reload safety — the registry's own doc-comment calls for
  this), then register the catalog content.
- **Content source — reconcile with WO-137 Part B, do not duplicate:** the 4 defensive tower entries
  (`docs/DEFENSIVE_CATALOG.md` / WO-137 table — Archer/Wizard/Cannon/Frost, with `visualPrefabPath`,
  `repo` stats copied off `DefenseTower`, and `mustSitOn` Ground/WallWalk). Author these as a small
  `DefensiveCatalog.Entries()` helper (this *is* the surviving useful half of WO-137 Part B) so adding a
  tower = adding a row. **Defer non-tower (Wall/Floor/Stairs/Gate/Room) content to WO-140** — this WO
  proves the path with the 4 towers; the factory must already be type-agnostic so WO-140 is data-only.
- Log a one-line summary on completion (`[CatalogBootstrap] registered N entries`); each entry whose
  `visualPrefabPath` fails to `Resources.Load` logs a **`Debug.LogWarning`** (pack may be unimported) —
  never an error, never a throw.

**Bootstrap shape decision (state in RESULT):** code-registered (a `DefensiveCatalog.Entries()` C# list)
is the chosen v1 — it matches the existing bootstrap pattern and keeps content in Village where prefab
paths + `behaviorId`s live. (A future SO-asset-driven registrar can replace the source of the list
without changing the factory — note it as the WO-140+ growth path, do not build it now.)

---

## Section 2 — `StructureFactory` (the ONE runtime-safe creation method)

**File (new):** `Assets/_Modules/Village/Catalog/StructureFactory.cs` — `DeNelle.Village`,
`namespace DeNelle.Village.Catalog`. **`using UnityEngine;` only — NO `using UnityEditor;`.**

```
public struct StructurePose { public Vector3 position; public Quaternion rotation; }   // (or reuse Vector3+yRot)

public static class StructureFactory
{
    // The ONE creation path. Returns the spawned root, or null on hard failure (logged).
    public static GameObject Create(CatalogEntry entry, StructurePose pose, Transform parent = null);

    // Composite: spawn every cell of a group at its relative pose under one root (see Section 3).
    public static GameObject CreateGroup(StructureGroup group, StructurePose originPose, Transform parent = null);

    // The string→component bridge (Section 2.2). Public so callers/tests can resolve too.
    public static MonoBehaviour AttachBehavior(GameObject host, RepoProps repo);
}
```

### 2.1 — `Create(entry, pose, parent)` step list (all null-guarded, LogWarning, never throw)
1. **Guard:** `entry == null` → `LogWarning` + return null. If `entry.kind == Composite` and
   `entry.composite != null`, delegate to `CreateGroup` (an entry can BE a group — Section 3).
2. **Host root:** `new GameObject(entry.displayName ?? entry.id)`; parent it; set
   `position`/`rotation` from `pose`. The host owns the gameplay collider + the behavior; the visual is
   a child (matches `VisualFactory`/`DefenseTower` convention).
3. **Resolve the LOOK (`visualPrefabPath`):** call
   `VisualFactory.Skin(host.transform, entry.visualPrefabPath, SkinOptions.Structure(<footprint-derived size>))`.
   This reuses the proven `Resources.Load` + fit/seat/URP-fix/strip-collider path. If it returns null
   (prefab missing), keep the host (so behavior + collider still exist) and `LogWarning` — pack-missing
   must degrade, not crash. (Derive the fit size from `repo.placement.footprint`; do not invent new
   sizing rules.)
4. **Attach the BEHAVIOR (`behaviorId`):** `AttachBehavior(host, entry.repo)` (Section 2.2).
5. **Apply `PlacementRules` to the spawned object:** add/size the gameplay collider from
   `placement.footprint`; tag per surface (`Tower`/`Building` so the existing overlap test sees it);
   set `NavMeshObstacle`/nav contribution from `repo.navSurface` (`Blocker` carves, `Walkable` is a
   walkable surface, `None` neither). **Validation** of whether the pose is *legal* (mustSitOn,
   overlap, affordability) is the CALLER's job (placement system / bake author) — `Create` only
   *applies* the rules to the made object. Keep that separation clean.
6. Return the host root.

### 2.2 — The `behaviorId` string→component bridge (keeps Core pure)
- `AttachBehavior(host, repo)` switches on `repo.behaviorId` (a plain string from Core) and
  `AddComponent<T>()` the matching Village MonoBehaviour, then copies `repo` stats onto it.
- **v1 mapping (extend as content grows — this is the seam WO-140 widens):**
  | `behaviorId` | Component | Stat copy |
  |---|---|---|
  | `"DefenseTower"` (or null on a `Tower`-type entry) | `DefenseTower` | `Range=repo.range; Damage=repo.damage; FireRate=repo.fireRate; CanHitAir=repo.canHitAir; Element=repo.element` |
  | `"WallSegment"` | `WallSegment` (reuse the existing damage/repair component) | HP/tier hook (WO-114) — leave defaults if WallSegment isn't stat-driven yet |
  | null / empty / unknown | **no component** (decoration/floor) | `LogWarning` on *unknown* (non-empty) ids; silent for intentionally-null |
- **Why a string, not a Type:** `behaviorId` lives in `DeNelle.Core.RepoProps`; Core must never
  reference a Village MonoBehaviour (CLAUDE.md §5, `core-cannot-reference-village`). The switch lives in
  Village, so the resolution is the boundary. **Do not** use `System.Reflection`/`Type.GetType` for this
  bridge (CLAUDE.md §10 forbids new reflection in bridge scripts) — an explicit `switch` is the bridge.

---

## Section 3 — `CreateGroup` (a castle is a GROUP)

**Group data shape (new, Village — keep Core's `CellPlacement` as the per-member primitive):**

`CatalogEntry` already carries `CellPlacement[] composite` (`cellEntryId`, `Vector3 offset`,
`float yRotation`) for `EntryKind.Composite`. **Reuse it** — a group is just a `CatalogEntry` of
`kind == Composite` whose `composite[]` lists member cell-entry ids + relative poses. So a "castle" is a
single composite catalog entry referencing wall/tower/gate cell entries.

- `CreateGroup(group, originPose, parent)`:
  1. Make a group root GameObject at `originPose` under `parent`.
  2. For each `CellPlacement` in the composite: `CatalogRegistry.Get(cellEntryId)` → if found, call
     `Create(member, originPose ∘ {offset, yRotation}, groupRoot.transform)`; if the id is missing,
     `LogWarning` and skip that member (partial group is better than a thrown bake).
  3. Return the group root.
- **This is exactly what the castle rewrite authors:** WO-136 becomes "define a castle composite entry
  (curtain segments + 4 corner towers + 4 gatehouses at their offsets) and call `CreateGroup` once,"
  instead of hand-coding `BuildWallPerimeter` geometry. A player could later place the same composite as
  one bundle (CoC-grain room/fort drop). The relative-pose math (`originPose ∘ member`) is the only new
  geometry logic, and it is shared by both callers.
- **For this WO**, ship `CreateGroup` + a tiny **smoke composite** (e.g. a 2-tower + 1-wall demo group)
  registered by the bootstrap to prove the path. **Authoring the full castle composite is WO-136's job**
  (it owns the wall layout + the unfreezing of the builder) — this WO gives WO-136 the method to call.

---

## Section 4 — Caller A: the editor / bake-time wrapper

**File (new):** `Assets/Editor/StructureFactoryEditor.cs` — `DeNelle.Editor`,
`namespace DeNelle.Editor`. **Thin. Delegates all creation logic to `StructureFactory`.**

- `public static GameObject Create(CatalogEntry entry, StructurePose pose, Transform parent)` and a
  matching `CreateGroup` — each calls the runtime `StructureFactory.Create`/`CreateGroup`, then adds
  **only** bake-time concerns:
  - `Undo.RegisterCreatedObjectUndo` on the returned root (so the bake is undoable in-editor).
  - `GameObjectUtility.SetStaticEditorFlags` (mark static for batching) per the project's existing bake
    conventions.
  - **Optional prefab-instance upgrade:** if a true prefab instance is wanted at bake time (vs a runtime
    clone), re-resolve `visualPrefabPath` via `AssetDatabase`/`PrefabUtility.InstantiatePrefab`
    (mirrors `VillageSceneBuilder.InstantiateModel` at `:2365`). State in the RESULT whether v1 keeps the
    runtime `Resources.Load` clone or upgrades to a prefab instance — **default to the simpler runtime
    clone** unless WO-136 needs prefab linkage.
- **No creation logic is duplicated here** — placement, behavior attach, and rules application all happen
  in the runtime core. The wrapper only decorates the result.
- This is the seam **WO-136's castle rewrite consumes.** WO-148 does **NOT** edit `VillageSceneBuilder.cs`
  or call this wrapper from the builder — WO-136 (which owns unfreezing the builder + the rebake) wires
  `BuildWallPerimeter` to call `StructureFactoryEditor.CreateGroup`. WO-148 only ships the wrapper +
  proves it on a throwaway editor smoke (a menu item or test that creates one group and discards it).

---

## Section 5 — Caller B: generalize `TowerPlacementSystem` (runtime player placement)

**File (edit):** `Assets/_Modules/Village/Buildings/TowerPlacementSystem.cs`. **Generalize, do NOT fork.**

- Add an **additive** `public void StartPlacing(CatalogEntry entry)` overload alongside the existing
  `StartPlacing(TowerData)`. Store the active entry in a new `CatalogEntry _selectedEntry` field
  (parallel to `_selectedTower`); the ghost loop branches on whichever is set.
- **Honor `PlacementRules.mustSitOn`** in the validity test: extend `IsValidSurface`/`CanPlace` so that
  - `Ground` / `AnyTerrain` → the existing flat-upward-face test;
  - `WallWalk` → the hit must be on the rampart walkable surface (tag/layer or `hit.normal.y` + a
    wall-walk height/area check — coordinate the exact wall-walk tag with WO-136/WO-109a; until the
    rampart exists, `WallWalk` simply fails-closed = red ghost, which is correct);
  - `Floor` → on a placed floor (defer real floor support to WO-140; fail-closed for now).
- **`CanPlace` reads the entry's rules, not `TowerData`:** `repo.buildCost` for affordability
  (`EconomyService.CanAfford` — keep the existing `?.`/null guards), `placement.footprint` for the
  `OverlapSphereNonAlloc` radius, `placement.noOverlap`/`minDistanceFromGate`/`checkAffordable` toggles.
- **On confirm:** `EconomyService.Instance?.Spend(entry.repo.buildCost)` then
  `StructureFactory.Create(entry, pose, parent)` — **the runtime factory IS the placement path.** (The
  `TowerData` branch keeps its existing `TowerConstructionQueue.AddToQueue` flow untouched.)
- **Reconcile TowerData ↔ CatalogEntry:** do **not** delete the `TowerData` path. The catalog overload is
  additive; `BuildMenu`/Build Mode migrate to it over WO-108/WO-139. (Optionally provide a private
  `TowerData → CatalogEntry` adapter so the two branches share one code path internally — keep it
  minimal; the public API is two overloads.) This satisfies the roadmap's "generalize, don't fork."

> **Scope note:** full Build-Mode UI / ghost-for-arbitrary-meshes / `PlacedStructure` persistence are
> WO-108/WO-139 (P1/P2). This WO only proves the runtime caller can place a `CatalogEntry` via the
> factory and that `mustSitOn` gates the ghost. Keep the cylinder ghost; richer ghost = later.

---

## Files to Create / Edit

**Create**
- `Assets/_Modules/Village/Catalog/CatalogBootstrap.cs` (`DeNelle.Village.Catalog`) — registry populate.
- `Assets/_Modules/Village/Catalog/DefensiveCatalog.cs` (`DeNelle.Village.Catalog`) — the 4 tower entries
  + smoke composite as data rows (surviving half of WO-137 Part B; reconcile if a stub exists).
- `Assets/_Modules/Village/Catalog/StructureFactory.cs` (`DeNelle.Village.Catalog`) — **runtime-safe core**
  (`Create`, `CreateGroup`, `AttachBehavior`, `StructureGroup`/pose helper).
- `Assets/Editor/StructureFactoryEditor.cs` (`DeNelle.Editor`) — thin bake-time wrapper + a discardable
  editor smoke (menu item) proving one `CreateGroup`.

**Edit**
- `Assets/_Modules/Village/Buildings/TowerPlacementSystem.cs` — additive `StartPlacing(CatalogEntry)`
  overload; rules-aware `CanPlace`/`IsValidSurface`; confirm → `StructureFactory.Create`.

**Verify-only (do NOT change)**
- `Assets/_Modules/Core/Catalog/*` — Part A is the contract; the factory binds to it, never edits it.

---

## What NOT to touch

- ❌ **`Assets/Editor/VillageSceneBuilder.cs` — FROZEN.** Read its `LoadModel`/`InstantiateModel`/
  `BuildWallPerimeter` patterns to design the editor wrapper, but **do not edit its body**. WO-136 (which
  owns unfreezing it + the rebake) is what later routes the builder through `StructureFactoryEditor`.
- ❌ **`Village.unity`** — no scene hand-edits; **no bake fired** in this WO (CLAUDE.md §3).
- ❌ **`Assets/_Modules/Core/Catalog/*`** — do not redesign Part A; bind to it as-is.
- ❌ Do **not** add an Addressables load path for structures — use `Resources.Load` via `VisualFactory`.
- ❌ Do **not** introduce `System.Reflection`/`Type.GetType` for the `behaviorId` bridge — explicit switch.
- ❌ Do **not** fork a second placement system — generalize `TowerPlacementSystem`.
- ❌ Do **not** delete the `TowerData` placement path or `TowerConstructionQueue` flow.
- ❌ Do **not** convert raw textures / `git add -A` (LFS clean-filter trap) — CLI stages by explicit path.

---

## Acceptance criteria

- [ ] Compiles green (CLI build-gate); brace balance passes on every new/edited `.cs`.
- [ ] **Registry is no longer empty at runtime:** after Play, `CatalogRegistry.Count >= 4` and
      `OfType(CatalogType.Tower)` returns the 4 defensive entries (the P0 fix, proven).
- [ ] `StructureFactory.Create(entry, pose, parent)` is **runtime-safe** — `StructureFactory.cs` contains
      **zero** `using UnityEditor` / `UnityEditor.` references (grep clean).
- [ ] `Create` on a tower entry spawns the real `visualPrefabPath` model (via `VisualFactory`), attaches
      `DefenseTower` with stats copied from `repo`, and the tower fires (placement=role holds: Ground
      can't hit a flier, WallWalk can). Missing prefab → `LogWarning` + host still has behavior/collider,
      no throw.
- [ ] `behaviorId` string→component bridge resolves `"DefenseTower"`/`"WallSegment"`/null without
      reflection; unknown non-empty id → `LogWarning`, no crash.
- [ ] `CreateGroup` spawns a multi-member composite from `CellPlacement[]` at correct relative poses; a
      missing member id is `LogWarning`-skipped, not fatal. Smoke composite proves it.
- [ ] `StructureFactoryEditor.Create`/`CreateGroup` call the runtime core (no duplicated creation logic)
      and add only bake-time decoration; the editor smoke menu item creates one group cleanly.
- [ ] `TowerPlacementSystem.StartPlacing(CatalogEntry)` overload exists, the `TowerData` path still
      works, `CanPlace` reads `repo.buildCost`/`placement.footprint`, and `mustSitOn=WallWalk` fails the
      ghost (red) when no rampart surface is under the cursor.
- [ ] **Core/Catalog has zero references to `DeNelle.Village`** (asmdef boundary intact); Village → Core
      only (CLAUDE.md §5).
- [ ] No scene baked, `VillageSceneBuilder.cs` unchanged (`git diff` clean on that file).

---

## Done checklist (CLAUDE.md §10)

- [ ] Brace-balance check passed on every `.cs` touched (`CatalogBootstrap`, `DefensiveCatalog`,
      `StructureFactory`, `StructureFactoryEditor`, `TowerPlacementSystem`).
- [ ] No `.unity` scene file hand-edited; **no bake fired**; `VillageSceneBuilder.cs` untouched.
- [ ] No new `System.Reflection` usage introduced (the `behaviorId` bridge is an explicit switch).
- [ ] `using DeNelle.Core.Combat;` present in any file touching `IDamageable`/`DamageElement`.
- [ ] Null-conditional `?.` on every cross-module service call (`EconomyService`, `VisualFactory` miss,
      `CatalogRegistry.Get` null members).
- [ ] `StructureFactory.cs` is `UnityEditor`-free (runtime-safe) — verified by grep.
- [ ] Assembly placement correct: Core untouched/pure; factory + bootstrap + content in `DeNelle.Village`;
      thin wrapper in `DeNelle.Editor`. Village → Core only.
- [ ] Acceptance criteria reviewed line by line.
- [ ] `WORK_ORDER_148_catalog_structure_factory.RESULT.md` written when complete (state: chosen fit-size
      mapping, runtime-clone vs prefab-instance decision for the wrapper, the wall-walk surface
      detection chosen, and the final `behaviorId` map).

---

## Why this is the keystone

After this WO, **structure creation is one repeatable process:** `CatalogEntry` data + `StructureFactory`
= a placed structure, whether the caller is the bake-time castle author (WO-136 via the editor wrapper) or
the player at runtime (generalized `TowerPlacementSystem`, then WO-108 Build Mode). The castle stops being
bespoke geometry and becomes a **composite of catalog rows**; the player base is the **same rows**, placed
live. Every future structure — dungeon, region ruin, fort — is then **data fed through one factory, not
new builder code.** That is the owner's "enhance the wheel" applied at the authoring level, and it is what
unblocks WO-136, WO-108, WO-139, and WO-140.

> **OWNER RULING 2026-08-21 (verbal, this session):** CLOSED by an explicit owner sweep of the WHOLE 0-800 RANGE: "I've eyeballed them many times, and all of them are already completed. or immaterial." This is a RANGE close on the owner's direct review, not a per-ticket verdict.
