> ⚠ **UNRESOLVED NUMBER COLLISION — WO-137 is claimed by more than one file and OWNERSHIP IS NOT DECIDED.**
> Co-claimants: `WORK_ORDER_137_castle_rampart_rebake.md`, `WORK_ORDER_137_catalog_data_model_and_defensive_content.md`
> Both files were added in the SAME commit (first-on-disk is a dead tie) and neither is cited by any other doc, RESULT file, or commit message — there is no evidence on either side.
> Flagged (not resolved) by the 2026-08-16 Sunday board-grooming pass — a wrong ownership call is worse
> than a flagged unknown, so this needs an **owner ruling**. Nothing renumbered or deleted. Until it is
> ruled, cite this WO by FILENAME, never by bare number.

# WORK ORDER 137 — Catalog Data Model + Defensive Catalog (first content)

**Status: READY TO IMPLEMENT**
**Lane:** Engine foundation (Phase 0 — catalog half). Additive, green-but-unused, no big-bang.
**Owner directive (2026-05-30):** *"set up work order for UI to start writing those catalogs and
structures — rules based off what we already coded, libraries from our asset libraries."*

> Two hard constraints from the owner, baked into every section below:
> 1. **Rules are EXTRACTED, not invented** — every repo/placement value comes from a field that
>    already exists in shipped code (`DefenseTower`, `IDamageable`, `EnemyRole`, `ProjectileMover`,
>    `TowerPlacementSystem`, the rampart `NavSurface`). No new mechanics.
> 2. **Visuals are REAL prefabs** — every catalog entry's `visual` points at a polyperfect prefab
>    from `docs/polyperfect-asset-catalog.md`. **No primitives in the catalog** (cylinders were the
>    throwaway F7 test only).

Design source of truth: `docs/CATALOG_SYSTEM.md` (data model) + `docs/DEFENSIVE_CATALOG.md` (the 4
starter towers) + `docs/ENGINE_MASTER_PLAN.md` (foundation-first sequence). This WO **implements** the
catalog half — it does not re-design it.

---

## Why now
The F7 defensive test (`DefenseTower` + `DefenseTestSetup`) proved the thesis live: **placement = role**
(ground archers can't hit the flying dragon; wall wizards can), **role-priority targeting** off
`EnemyBrain.Role`, all reusing `IDamageable`/`ProjectileMover`/the rampart. The behavior works. This WO
turns that proof into **data**: the catalog/repo model + the 4 defensive entries as defs, so adding a
tower becomes a row, not a code change.

---

## Part A — Catalog data model (Core, pure data, no behavior)

**Assembly:** `DeNelle.Core` (namespace `DeNelle.Core.Catalog`). **Pure data only** — Core must NOT
reference Village (existing asmdef rule; see `core-cannot-reference-village`). The `visual` is a
**string prefab path** (Resources/polyperfect path), so Core never hard-refs a prefab or a Village type.

Create:
- `Assets/_Modules/Core/Catalog/CatalogType.cs`
  `enum CatalogType { Wall, Stairs, Floor, Room, Tower, Gate, Resource, Decoration }` (from CATALOG_SYSTEM.md).
- `Assets/_Modules/Core/Catalog/CatalogEntry.cs` — the def:
  ```
  id, displayName, CatalogType type, EntryKind kind { Cell, Composite }
  string visualPrefabPath      // LOOK  — e.g. "polyperfect/.../Tower_Castle_Round"
  RepoProps repo               // BEHAVIOR
  CellPlacement[] composite    // Composites only (offset+rotation); null for cells
  ```
- `Assets/_Modules/Core/Catalog/RepoProps.cs` — the behavior half (data only):
  footprint (m), build cost, `NavSurfaceKind` (None / Walkable / Blocker), `PlacementRules` ref,
  ownership-gate id (for cosmetics), and a **`behaviorId` string** (Village resolves it to a component —
  keeps Core pure; see Part B). Combat stats (range/dmg/rate/canHitAir/element) live here as plain
  fields so a tower def is fully data.
- `Assets/_Modules/Core/Catalog/PlacementRules.cs` — **declarative** rules as data (from CATALOG_SYSTEM.md):
  `mustSitOn` (Ground | WallWalk | Floor | AnyTerrain), `noOverlap` (footprint), `minDistanceFromGate`,
  `requiresSupport`, `affordable`, `owned`. The rule answers *"does this naturally work here"* at the
  free cursor — **no grid** (owner: organic placement + edge-snap, not grid-snap).
- `Assets/_Modules/Core/Catalog/CatalogRegistry.cs` — registry: register/lookup entries by id + by type.
  Static, populated at startup. (Content registers itself from Village — Part B.)

**Rule extraction map (Part A fields ← already-coded source):**

| RepoProps / PlacementRules field | Extracted from (shipped code) |
|---|---|
| `range, damage, fireRate, canHitAir, element` | `DefenseTower` public fields (verbatim) |
| target priority (Healer→DPS→Ranged→Tank) | `DefenseTower.Priority()` + `EnemyRole` (WaveEnemyGroup.cs) |
| `mustSitOn = Ground / WallWalk` | the F7 test's ground-vs-wall split = the rampart `NavSurface` we baked |
| `noOverlap` / footprint / cost / ghost | `TowerPlacementSystem` (already does ghost + overlap + cost) |
| `minDistanceFromGate` | `ValidateBuildingGateClearance` (existing distance check) |
| projectile launch | `ProjectileMover.Launch(target, speed, arc)` |

Nothing here is new mechanics — it is the **classification + data layer** over code that already runs.

---

## Part B — Defensive Catalog content (Village, references real prefabs + DefenseTower)

**Assembly:** `DeNelle.Village` (so it may reference `DefenseTower`, `EnemyRole`, prefabs). This is
where Core's `behaviorId` resolves to the actual component.

Create:
- `Assets/_Modules/Village/Catalog/DefensiveCatalog.cs` — registers the **4 starter entries** from
  `docs/DEFENSIVE_CATALOG.md` into `CatalogRegistry` at startup, each with a **real prefab path** and
  repo stats **copied from the F7 tower tunings** (rules-from-code). Provisional prefab map (from
  `docs/polyperfect-asset-catalog.md` — adjust to best visual fit, keep the *placement* rule):

  | Entry | `visualPrefabPath` (real asset) | `mustSitOn` | range / dmg / rate / air (from F7) |
  |---|---|---|---|
  | **Archer Tower** | `Tower_Medieval_Wood` | Ground | 16 / 6 / 2.5 / false |
  | **Wizard Tower** | `Tower_Castle_Round` | WallWalk | 55 / 14 / 1.0 / **true** |
  | **Cannon / Ballista** | `Tower_Castle_Square` | Ground | (heavy: 28 / 22 / 0.5 / false) |
  | **Frost Spire** | `Tower_Medieval_Big` | WallWalk | (slow+ice: 30 / 4 / 1.2 / true, `Element=Ice`) |

- A small **`CatalogTowerFactory`** (Village): given a `CatalogEntry`, `Instantiate` the real prefab at
  the placement, then `AddComponent<DefenseTower>()` and copy repo stats onto it. **DefenseTower is
  reused verbatim as the behavior** — the def just supplies its numbers + which prefab to wear.

- **Wire the F7 test to prove the data path:** `DefenseTestSetup` (F7) should build its towers **via
  `CatalogTowerFactory` + the registry** instead of spawning bare cylinders — so pressing F7 now spawns
  the **real polyperfect tower models** driven entirely by catalog defs. That is the acceptance demo:
  *the same scene, but every tower came from a data row + a library prefab.*

---

## Acceptance criteria
- [ ] Compiles green (CLI build-gate); brace balance passes on every new `.cs`.
- [ ] `CatalogRegistry` returns 4 defensive entries by id and by `CatalogType.Tower`.
- [ ] Each entry's `visualPrefabPath` resolves to a **real polyperfect prefab** (logged on register;
      `Debug.LogWarning` — not error — if a prefab is missing, pack may be unimported).
- [ ] **F7 now spawns real tower models** (not cylinders), positioned by `mustSitOn` (Ground vs
      WallWalk at y≈5.4), firing via `DefenseTower` — placement=role still holds vs the apex dragon.
- [ ] `PlacementRules` (`mustSitOn`, `minDistanceFromGate`, `noOverlap`) are **data on the entry**, not
      hand-coded in the test.
- [ ] Core/Catalog has **zero** references to `DeNelle.Village` (asmdef boundary intact).

## What NOT to touch
- ❌ `VillageSceneBuilder.cs` — single-touch serialization bottleneck; this WO needs none of it.
- ❌ `Village.unity` — no scene hand-edits; F7 is runtime-only.
- ❌ Do **not** start the `Enemy → Character` migration here — that's WO-106/119 foundation, a separate
  landed step. This WO is the **catalog half only**.
- ❌ Do **not** rewrite `DefenseTower` — it is the proven behavior; the catalog points *at* it.
- ❌ Do **not** convert raw textures / `git add -A` / commit (CLI is sole committer).

## Carry-over constraints (from CLAUDE.md + memory)
- **catalog ⊥ repo:** a cosmetic swaps `visualPrefabPath` only, never repo stats (structural
  cosmetic-only guarantee).
- **Core → Village forbidden:** data model in Core, content in Village, bridged by `behaviorId` +
  prefab path string.
- **`?.` on cross-module service calls**, `using DeNelle.Core.Combat;` where `IDamageable` is used.
- Mobile-scale note (later, not this WO): runtime placement carves `NavMeshObstacle`, rebake on
  build-mode exit — out of scope here, just don't architect against it.

---

## Definition of done
Pressing **F7** in a Village build spawns the four real polyperfect towers — wood archers on the
ground, castle-round wizards up on the rampart — every stat and placement coming from a `CatalogEntry`
data row, firing on the apex dragon through the reused `DefenseTower`. **One new tower thereafter = one
new entry, no new code.** That is the catalog proven as the content pipeline.
