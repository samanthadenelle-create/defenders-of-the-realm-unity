# Player Base-Design Catalog — Architecture + Roadmap

> **Owner ask (2026-05-30):** *"set an architect to determine how we can make a player-friendly catalog
> of options that they can start designing bases that they want, and how we get from there to here on a
> roadmap."*
>
> This is the **CREATE verb** (`docs/NORTH_STAR.md`) — CoC × Warcraft player base-building where
> **placement = role**. The player browses a catalog, picks pieces, and freely designs the base *they*
> want. This document is an **architecture + roadmap**, not a single WO; it spawns the WO list in §D.4.
>
> **Reconcile, not replace.** The catalog *data model* + placement-rules *engine* already exist
> (`Assets/_Modules/Core/Catalog/`). The placement *runtime* (`TowerPlacementSystem`) already exists.
> This roadmap wires a **player-facing catalog UI** onto those, fills the proven gaps, and stages the
> climb. Verified file paths + symbols are cited throughout.

---

## A. Current-State Audit (verified by inspection, 2026-05-30)

### A.1 — What EXISTS and is solid

**The catalog DATA MODEL (WO-137 Part A) — BUILT, compiles green, currently inert.**
`Assets/_Modules/Core/Catalog/` (assembly `DeNelle.Core`, namespace `DeNelle.Core.Catalog`, pure data):

| File | Symbol | Role |
|---|---|---|
| `CatalogType.cs` | `enum CatalogType { Wall, Stairs, Floor, Room, Tower, Gate, Resource, Decoration }`, `EntryKind { Cell, Composite }`, `NavSurfaceKind`, `PlacementSurface { AnyTerrain, Ground, WallWalk, Floor }` | the palette-tab taxonomy + the placement=role surface enum |
| `CatalogEntry.cs` | `CatalogEntry { id, displayName, type, kind, visualPrefabPath, RepoProps repo, CellPlacement[] composite }`, `CellPlacement` | one def = LOOK (`visualPrefabPath` string) + BEHAVIOR (`repo`); composites are pre-snapped cell sets |
| `RepoProps.cs` | `RepoProps { navSurface, buildCost, behaviorId, PlacementRules placement, range, damage, fireRate, canHitAir, element }` | the behavior half; combat stats copied off `DefenseTower`; `behaviorId` string keeps Core free of Village refs |
| `PlacementRules.cs` | `PlacementRules { mustSitOn, noOverlap, footprint, minDistanceFromGate, requiresSupport, checkAffordable, ownedGate }` | declarative "does this naturally work HERE", evaluated at a free cursor (no grid) |
| `CatalogRegistry.cs` | `static CatalogRegistry { Register(entry), Get(id), OfType(type), Count, Clear() }` | the runtime registry the palette reads; populated at startup by content |

This is exactly the `catalog ⊥ repo` (look vs behavior) split from `docs/CATALOG_SYSTEM.md`. **It is
the foundation. Everything below binds to it. Do not redesign it.**

**The placement RUNTIME — BUILT (`TowerPlacementSystem`).**
`Assets/_Modules/Village/Buildings/TowerPlacementSystem.cs` (`DeNelle.Village`, singleton). Already does
the entire ghost-placement loop the catalog needs, but hard-wired to one `TowerData`:
- `StartPlacing(TowerData)` → green/red ghost marker tracking the cursor (`MaterialPropertyBlock` tint).
- `IsValidSurface(hit)` (flat upward face, not on a Tower/Building), `SnapToGrid`, `CanPlace(pos)`
  (`EconomyService.CanAfford` + `SkillSystem` gate + `Physics.OverlapSphereNonAlloc` no-overlap).
- `PlaceTower(pos)` → `EconomyService.Spend` + hands to `TowerConstructionQueue`.
- **This is the one generic placement flow the catalog should drive** — it already implements
  `noOverlap`, `checkAffordable`, ghost preview. It just needs to take a `CatalogEntry` instead of a
  `TowerData`, honor `PlacementRules.mustSitOn`, and instantiate `entry.visualPrefabPath`.

**The placement=role PROOF — BUILT (`DefenseTower`).**
`Assets/_Modules/Village/Buildings/DefenseTower.cs` (`DeNelle.Village`). Ground (`CanHitAir=false`,
short range) can't touch a flier; wall-walk (`CanHitAir=true`, long, elevated) can. Role-priority
targeting (`Priority()` reads `EnemyBrain.Role`: Healer→Ranged/DPS→MiniBoss→Tank) via `IDamageable`
(`DeNelle.Core.Combat`) + `ProjectileMover`. **This is the behavior `RepoProps` was extracted from** —
the def supplies its numbers, `DefenseTower` is the reused behavior. *(Currently fires a primitive
sphere bolt and is spawned only by hand — see A.2.)*

**The current build UI — BUILT but tower-only (`BuildMenu`).**
`Assets/_Modules/Village/Buildings/UI/BuildMenu.cs` (`DeNelle.Village`). A 3-screen flow (Build Tower /
Upgrade Tower / Repair Wall) with a **hard-coded `TowerVariantDef[] Variants`** table (Flame/Ice/Aether/
Physical). Critically it already carries the **code-built fallback** (`ShowCodeFallbackMenu`) because
**UXML doesn't render in player builds** (`uxml-uidocuments-dont-render-in-builds`). It arms
`TowerPlacementSystem`. **This is the UI the catalog palette replaces/generalizes** — today its content
is a typed C# table, not catalog data.

**Supporting systems — BUILT:**
- `CrystalMine.cs` (`DeNelle.Village`) — passive 3-level resource node; the seed for the harvest economy.
- `CosmeticApplier.cs` (`DeNelle.Cosmetics`) — material/prefab/VFX swap on any skinnable object; the
  hook for the `ownedGate` cosmetic lane (catalog swaps `visualPrefabPath`, never `repo` stats).
- `GameState.WallLevel` (persisted, inert) — the wall-tier slot WO-114 wires.
- `EconomyService` (`CanAfford`/`TrySpend`/`Spend`) + `GameStateService` (`State.Resources.Crystals`,
  `ResourcesChanged`, save sync) — the cost + persistence backbone.

### A.2 — What is STUBBED / MISSING (the real gaps)

| Gap | Evidence | Impact |
|---|---|---|
| **WO-137 Part B — content** (`DefensiveCatalog`, `CatalogTowerFactory`) | grep: no `DefensiveCatalog`, no `CatalogTowerFactory`, no call site of `CatalogRegistry.Register` outside the registry itself | **The registry is empty at runtime.** Part A is a foundation with no content on it. |
| **The F7 demo** (`DefenseTestSetup`) | grep: `class DefenseTestSetup` — **no files found** | WO-137's acceptance demo (F7 spawns 4 catalog towers) was never wired; the data path is unproven in-game. *(Memory's "catalog engine proven in-game" refers to the raw `DefenseTower` F7 test, NOT the data-driven catalog path.)* |
| **WO-108 player Build Mode** — whole module | glob `Assets/_Modules/Village/BuildMode/**` → **no files**; no `BuildModeController`, `PlacementGrid`, `BuildPaletteUI`, `PlacedStructure`, `GhostPreview` | **The CREATE verb does not exist yet.** The player cannot enter a build mode, see a palette, or place anything but a single tower via `BuildMenu`. |
| **Catalog-driven placement** | `TowerPlacementSystem.StartPlacing(TowerData)` — no `CatalogEntry` overload; ignores `PlacementRules.mustSitOn` | placement runtime exists but isn't catalog-aware; can't enforce ground-vs-wall-walk from data. |
| **`PlacedStructure` persistence** | `GameState` has no `PlacedStructures` list (WO-108 §5 is unimplemented) | player layouts wouldn't survive a session. |
| **Wall tiers** (WO-114) | `WallLevel` slot persisted but inert; `WallSegment` doesn't read it | the CoC upgrade sink is unbuilt. |
| **Harvest economy** (WO-110/111/115/117/119) | `CrystalMine` is passive/per-wave only; no buildable mines, no auto-harvest, no offline accrual | build *costs* have no production source feeding them yet. |
| **Non-tower catalog content** | only tower stats in `RepoProps`; no Wall/Floor/Stairs/Room/Gate/Resource/Decoration entries authored | the catalog is tower-shaped today; base *design* needs the structural types. |

### A.3 — One-line verdict
**The engine seam is real and clean; the content + the player UI on top of it are the gap.** We have a
data model (Part A), a placement runtime (`TowerPlacementSystem`), and a proven behavior (`DefenseTower`)
— three pieces that have **never been connected end-to-end by data**. The roadmap's job is to connect
them, then grow the catalog from "4 towers" to "design a whole base."

---

## B. Target Vision — the Player-Friendly Catalog UX

**One sentence:** the player taps **Build**, a tabbed palette of real building pieces slides up, they
pick a piece, a green/red ghost shows where it *naturally fits*, they place + rotate + edge-snap it, and
the base they assemble is theirs — saved, defended, upgraded.

### B.1 — Browse: the palette IS the catalog
The palette is a thin **view over `CatalogRegistry`**, tabbed by `CatalogType` (`OfType(type)`):

```
[ Walls ] [ Towers ] [ Gates ] [ Stairs ] [ Floors ] [ Rooms ] [ Resources ] [ Decor ]
 ┌────┐ ┌────┐ ┌────┐ ┌────┐   ← horizontal card strip, one card per CatalogEntry
 │icon│ │icon│ │icon│ │icon│      card = thumbnail + displayName + cost + a role/zone badge
 │cost│ │cost│ │cost│ │cost│
 └────┘ └────┘ └────┘ └────┘
```

- **Categories = `CatalogType` tabs.** Start with the 4 core (`Wall · Stairs · Floor · Room`) +
  `Tower · Gate` (per `CATALOG_SYSTEM.md` "start with the structure core").
- **Filter by ROLE / PLACEMENT-ZONE** — a card badge reads `PlacementRules.mustSitOn`
  (Ground / Wall-walk / Floor / Any) so the player learns **placement = role** *from the catalog itself*:
  a Wizard Tower card is badged "Wall-walk · anti-air"; an Archer Tower "Ground · anti-infantry". This
  is the teaching surface for the core skill.
- **Affordability + ownership** shade the card: unaffordable greys out (`checkAffordable` + live
  `EconomyService`/`GameStateService.ResourcesChanged`); locked cosmetics show a gate (`ownedGate`).
- **Two grains, one strip** (`CATALOG_SYSTEM.md`): `EntryKind.Cell` (one wall tile) and
  `EntryKind.Composite` (a pre-snapped Room = floor+walls+door) sit side by side — drop a whole room,
  then isolate and edit one of its cells.

### B.2 — Select → Preview → Place (the one generic flow)
1. Tap a card → `BuildModeController.BeginPlace(CatalogEntry)`.
2. A **ghost** of `entry.visualPrefabPath` follows the cursor/finger. It turns **green when
   `PlacementRules` pass, red when they fail** — evaluated at the *free* cursor, no grid lock
   (Fallout 4 / Valheim grain; owner ruled out grid-snap in `CATALOG_SYSTEM.md`).
3. `mustSitOn` decides the surface: a Wizard Tower only greens on the **rampart wall-walk** (the baked
   `NavSurface` from WO-109a); an Archer Tower only on the **ground**. *This is placement = role enforced
   from data.*
4. **Edge-snap to neighbours** — a wall clicks onto a floor edge, stairs onto a wall top (connection
   points, not a grid). A Composite is a pre-snapped connector graph dropped as one bundle.
5. Confirm → `EconomyService.TrySpend(entry.repo.buildCost)` → instantiate visual + attach the behavior
   resolved from `repo.behaviorId` (e.g. `DefenseTower`) → register a `PlacedStructure` → carve a
   `NavMeshObstacle` so enemies re-path live.
6. **Move / Sell** an existing placed piece (tap → highlight → Move ghost or Sell for ~50% refund).

### B.3 — The base the player designs (real candidate pieces)
All pieces are **real assets already in the catalogs** (`docs/polyperfect-asset-catalog.md`,
`docs/DEFENSIVE_CATALOG.md`) — this is classification + data, not an art pipeline:

| Tab | Cells (parts) | Composites | Source |
|---|---|---|---|
| **Wall** | `Wall_Wood_Horizontal_3x3m` (tier 0), `Wall_Stone_3x3_A/B/C` (tier 1–2), corners | wall run · corner bastion | WO-114 tiers |
| **Tower** | Archer (`Tower_Medieval_Wood`, Ground), Wizard (`Tower_Castle_Round`, Wall-walk), Cannon/Ballista (`Tower_Castle_Square`, Ground), Frost Spire (`Tower_Medieval_Big`, Wall-walk), **Arcane** (`Tower_Castle_Square`, WO-113) | — | WO-137 / WO-113 |
| **Stairs** | `Stairs_Medieval_Stone` (+ NavSurface plank) | full ramp → rampart | WO-109a |
| **Floor** | `Terrain_Plane_*`, plaza/road tile | room floor pad | CATALOG_SYSTEM |
| **Gate** | `Gate_Medieval_Small/Large` | gatehouse | — |
| **Room** | — | floor + 4 walls + door | CATALOG_SYSTEM |
| **Resource** | Crystal node, buildable Mine | — | WO-111 |
| **Decoration** | banners, torches, props | courtyard set | cosmetic lane |

**The emergent skill (the fun)** — from `DEFENSIVE_CATALOG.md`: wall line (wizards/frost, elevated) =
the FAR layer thinning the wave on approach; ground line (archers/cannons) = the CLOSE layer for
breaches + heavies; concentrate fire at the gate funnel; overlap range circles so there are no gaps.
**Where + spacing = the skill, exactly like CoC** — and it all falls out of `PlacementRules` data.

### B.4 — Ties to the North Star ladder
This catalog *is* rungs 4–6 of the `NORTH_STAR.md` delivery ladder (place your base → structure your
settlement → build how you want). The free majority designs bases that become the raid targets the
Challenge Arena (the spend driver) needs a full stadium of. The catalog is the content engine the whole
business model sits on.

---

## C. The Gap + Roadmap (CURRENT → VISION)

Phased by **player-value × dependency**. Each phase ships something testable. The spine is:
**connect the data path → give the player a build mode → grow the catalog → feed it an economy →
make it a designable, persistent, beautiful base.**

### Phase table

| Phase | Name | Ships | Unblocks | Depends on |
|---|---|---|---|---|
| **P0** | **Prove the data path** | WO-137 **Part B**: `DefensiveCatalog` registers 4 tower entries; `CatalogTowerFactory` builds entry→prefab+`DefenseTower`; `DefenseTestSetup` (F7) spawns them from data | the catalog as a real content pipeline (1 tower = 1 row) | Part A (done), `DefenseTower`, polyperfect import |
| **P1** | **Catalog-driven placement** | Generalize `TowerPlacementSystem` → `CatalogPlacementSystem`: `StartPlacing(CatalogEntry)`, honor `PlacementRules.mustSitOn` (Ground vs WallWalk), instantiate `visualPrefabPath`, resolve `behaviorId` | one placement flow for *every* catalog type; placement=role enforced from data | P0; rampart `NavSurface` (WO-109a) |
| **P2** | **Player Build Mode (CREATE verb)** | WO-108: `BuildModeController` (enter/exit, freeze waves, top-down cam), `BuildPaletteUI` (code-built, catalog-bound, tabbed by `CatalogType`), `GhostPreview`, `PlacedStructure` + `GameState.PlacedStructures` persistence, Move/Sell | **the player designs + saves a base** — the heart of the vision | P1; `EconomyService`, `GameStateService` |
| **P3** | **Structural catalog content** | Author Wall / Stairs / Floor / Gate / Room entries (cells + first Composites) into the registry; the palette gains its structure tabs | base *design* (not just tower placement); the 4-core-types catalog | P1 (placement honors `mustSitOn`/`requiresSupport`); P2 (palette) |
| **P4** | **Upgrade sink + harvest economy** | WO-114 wall tiers (wood→stone→reinforced, reads `WallLevel`); WO-111/110 buildable harvest mines; WO-117/119 worker/pet auto-harvest; WO-115 offline accrual | the CoC progression loop: harvest → upgrade → defend → offline; **build costs now have a source** | P2/P3 (mines + walls are catalog entries); `EconomyService`, `CrystalMine` |
| **P5** | **Rampart depth + zones** | WO-109c player-placeable Wall Tower + `BuildZone.WallTop` restriction; elevation range bonus (WO-109b) surfaced in the palette badge | two-layer defense (FAR wall line / CLOSE ground line) the player builds | P2 (palette/zones); WO-109a rampart (architect lane) |
| **P6** | **Cosmetics + polish** | `ownedGate` lane wired through `CosmeticApplier` (skin a piece, never its `repo` stats); WO-113 Arcane Tower imbue ceiling; composites for fast CoC-grain drops; juice (place SFX/VFX) | the flex/spend layer (sell flex not power); the "feels good to build" polish | P2/P3; `CosmeticApplier` (done), WO-113 |

### Notes on sequencing
- **P0 is the keystone and is small** — it's the unwritten half of WO-137, and it turns the inert Part A
  into a proven pipeline. *Do this first; it de-risks everything after it.*
- **P1 before P2** — Build Mode should drive **one** catalog-aware placement flow, not reimplement
  ghosting. Generalize `TowerPlacementSystem` (don't fork it) so WO-108's `BuildModeController.BeginPlace`
  delegates to it.
- **P2 is the North-Star milestone** — the moment the player can design + save a base, the CREATE verb
  is alive. Everything before it is plumbing; everything after it is depth.
- **P4 closes the loop economically** — until then, build costs are paid from per-wave crystals only.
  P4 (harvest) is what makes "keep building" sustainable, but the *building* (P2) ships first and is fun
  on the existing crystal economy.
- **P5 depends on WO-109a (architect lane)** landing the baked walkable rampart `NavSurface` — coordinate
  through the single-touch `VillageSceneBuilder` WO; this roadmap does **not** touch that file.
- **Architect-lane constraint:** any phase needing the wall ring / rampart geometry rides
  `VillageSceneBuilder` and must be queued as its own WO (CLAUDE.md §3, §9). The catalog UI + placement
  runtime are all runtime code and run in parallel.

---

## D. Architecture

### D.1 — How the player UI binds to the existing model (data-driven, one flow)

```
                 ┌───────────────────────── DeNelle.Core (pure data) ─────────────────────────┐
                 │  Catalog/  CatalogRegistry · CatalogEntry · RepoProps · PlacementRules      │
                 │            CatalogType · PlacementSurface · NavSurfaceKind   (BUILT, P-A)    │
                 └───────────────▲───────────────────────────────▲────────────────────────────┘
        reads OfType(type)       │ registers entries (startup)    │ reads repo/PlacementRules
                 │               │                                │
   ┌─────────────┴───────┐  ┌────┴───────────────────┐   ┌────────┴──────────────────────────┐
   │  BuildPaletteUI     │  │  DefensiveCatalog +     │   │  CatalogPlacementSystem            │
   │  (DeNelle.Village,  │  │  StructuralCatalog      │   │  (generalized TowerPlacementSystem)│
   │   code-built UI)    │  │  (DeNelle.Village)      │   │  ghost · mustSitOn · TrySpend      │
   │  tabbed by Catalog- │  │  registers entries +    │   │  · resolve behaviorId · carve nav  │
   │  Type · cards       │  │  prefab paths + stats   │   └────────┬───────────────────────────┘
   └─────────┬───────────┘  │  CatalogTowerFactory:   │            │ instantiate visual + behavior
             │ BeginPlace   │  entry → prefab +       │            ▼
             ▼              │  AddComponent<Defense-  │   ┌────────────────────────────────────┐
   ┌─────────────────────┐  │  Tower> + copy stats    │   │  PlacedStructure (runtime comp) +    │
   │ BuildModeController  │  └─────────────────────────┘   │  GameState.PlacedStructures (save)  │
   │ (DeNelle.Village)    │──── delegates placement ──────▶└────────────────────────────────────┘
   │ enter/exit · cam ·   │
   │ freeze waves         │
   └──────────────────────┘
```

- **The palette never hard-codes content.** It calls `CatalogRegistry.OfType(tab)` and renders a card
  per `CatalogEntry`. Adding a building = adding a registry row (the WO-137 promise: *"one new tower = one
  new entry, no new code"*). This kills the `BuildMenu` hard-coded `TowerVariantDef[] Variants` table.
- **One generic placement flow.** `CatalogPlacementSystem` (the generalized `TowerPlacementSystem`) takes
  a `CatalogEntry`, reads `PlacementRules` for valid/invalid, spends `repo.buildCost`, instantiates
  `visualPrefabPath`, and resolves `repo.behaviorId` → the behavior component (`DefenseTower`, `Mine`,
  `WallSegment`, …). No per-type special-casing.
- **`catalog ⊥ repo` guarantee.** A cosmetic is a `CatalogEntry` with an `ownedGate` that swaps
  `visualPrefabPath` only — never `repo` stats — applied via `CosmeticApplier`. Look changes, power never.

### D.2 — Assembly placement (CLAUDE.md §5 — Village → Core only)

| New piece | Assembly | Why |
|---|---|---|
| Catalog data model (Part A) | `DeNelle.Core` (`DeNelle.Core.Catalog`) | pure data; **already there**; Core must never ref Village |
| `DefensiveCatalog`, `StructuralCatalog`, `CatalogTowerFactory` | `DeNelle.Village` | resolve `behaviorId` → real components + reference prefabs (`DefenseTower`, `WallSegment`) — content lives in Village |
| `CatalogPlacementSystem` (generalize `TowerPlacementSystem`) | `DeNelle.Village` | already there; references `EconomyService` + prefabs |
| `BuildModeController`, `BuildPaletteUI`, `GhostPreview`, `PlacedStructure` | `DeNelle.Village` (`Assets/_Modules/Village/BuildMode/`) | gameplay + scene; reads Core catalog data |
| `PlacedStructures` save list | `DeNelle.Core.State` (`GameState`) | persisted save data is Core |
| Cosmetic gating | `DeNelle.Cosmetics` (`CosmeticApplier`) | already there; queried at call site via `?.` |

**The string bridge keeps Core pure:** `visualPrefabPath` (string) and `behaviorId` (string) mean Core
never hard-refs a prefab or a Village type — Village resolves both. This is the same pattern that already
ships in Part A.

### D.3 — Hard constraints baked in
- **UXML does NOT render in player builds** (`uxml-uidocuments-dont-render-in-builds`,
  `unity-intro-panelsettings-regression`). `BuildPaletteUI` and `GhostPreview` are **code-built UIElements
  only** — follow `BuildMenu.ShowCodeFallbackMenu` as the proven pattern, not the `.uxml` path.
- **No grid.** Free/organic placement + edge-snap-to-neighbours (owner-ruled, `CATALOG_SYSTEM.md`).
  *Note:* WO-108's spec mentions a `PlacementGrid`; the catalog system supersedes that with free
  placement + `PlacementRules` — reconcile WO-108 to the no-grid model (snap to connection points, not
  cells) when it's cut.
- **Don't touch `VillageSceneBuilder.cs` / `Village.unity`** (CLAUDE.md §3, §9). Runtime placement carves
  `NavMeshObstacle` and rebakes on build-mode exit; the wall-ring geometry rides the architect lane WO.
- **`?.` on every cross-module service call**; `using DeNelle.Core.Combat;` on anything touching
  `IDamageable`; brace-gate every `.cs` (CLAUDE.md §1).
- **Reconcile, don't duplicate.** Generalize `TowerPlacementSystem` (don't fork a 2nd placement system);
  reuse `DefenseTower` as the tower behavior; reuse `GameState.WallLevel` for tiers; reuse
  `CosmeticApplier` for skins; reuse `EconomyService`/`GameStateService` for cost + save.

### D.4 — Next concrete WOs to cut (in order)

1. **WO-137 Part B** *(P0 — exists as spec; just needs implementing)* — `DefensiveCatalog` +
   `CatalogTowerFactory` + `DefenseTestSetup` (F7). Acceptance = F7 spawns 4 real polyperfect towers
   from data rows. **The keystone; cut first.**
2. **WO-139 — `CatalogPlacementSystem`** *(P1, NEW)* — generalize `TowerPlacementSystem` to take a
   `CatalogEntry`, honor `PlacementRules.mustSitOn`, instantiate `visualPrefabPath`, resolve `behaviorId`.
   Keep the existing `TowerData` path working (additive overload) until the palette migrates.
3. **WO-108 (re-scoped) — Player Build Mode** *(P2)* — `BuildModeController` + code-built catalog-bound
   `BuildPaletteUI` + `GhostPreview` + `PlacedStructure`/`GameState.PlacedStructures`. **Reconcile to the
   no-grid free-placement model** (drop the `PlacementGrid` cell-lock; use edge-snap + `PlacementRules`).
   Delegates placement to WO-139.
4. **WO-140 — Structural catalog content** *(P3, NEW)* — author Wall/Stairs/Floor/Gate/Room entries (cells
   + first Composites) into the registry; extend `RepoProps`/factory to non-tower `behaviorId`s
   (`WallSegment`, etc.). The palette gains its structure tabs.
5. **WO-114 — Wall tiers** *(P4)* — wire `WallLevel` → tier HP/mesh (already specced; rides architect
   rebake for placement). Surfaces in the palette as wall upgrade entries.
6. **WO-111 / WO-110 / WO-117 / WO-119 / WO-115 — Harvest economy** *(P4)* — buildable mines as
   `Resource` catalog entries, auto-harvest, offline accrual; feeds build costs.
7. **WO-109c — Player Wall Tower + `BuildZone.WallTop`** *(P5)* — wall-walk-only palette entry; depends on
   WO-109a rampart (architect lane).
8. **WO-141 — Cosmetic catalog lane** *(P6, NEW)* — wire `ownedGate` entries through `CosmeticApplier`;
   prove a skin swaps `visualPrefabPath` and never `repo`.

---

## Appendix — verified file index

| Path | Status |
|---|---|
| `Assets/_Modules/Core/Catalog/CatalogType.cs` | BUILT (Part A) |
| `Assets/_Modules/Core/Catalog/CatalogEntry.cs` | BUILT (Part A) |
| `Assets/_Modules/Core/Catalog/RepoProps.cs` | BUILT (Part A) |
| `Assets/_Modules/Core/Catalog/PlacementRules.cs` | BUILT (Part A) |
| `Assets/_Modules/Core/Catalog/CatalogRegistry.cs` | BUILT (Part A) — **empty at runtime (no content registers)** |
| `Assets/_Modules/Village/Buildings/TowerPlacementSystem.cs` | BUILT — generalize in P1 (WO-139) |
| `Assets/_Modules/Village/Buildings/DefenseTower.cs` | BUILT — the reused tower behavior |
| `Assets/_Modules/Village/Buildings/UI/BuildMenu.cs` | BUILT — tower-only, hard-coded variants; superseded by catalog palette |
| `Assets/_Modules/Village/Buildings/CrystalMine.cs` | BUILT — seed for buildable mines (P4) |
| `Assets/_Modules/Cosmetics/CosmeticApplier.cs` | BUILT — the `ownedGate` skin hook (P6) |
| `Assets/_Modules/Village/Catalog/DefensiveCatalog.cs` | **MISSING — WO-137 Part B (P0)** |
| `…/Village/Catalog/CatalogTowerFactory.cs` | **MISSING — WO-137 Part B (P0)** |
| `…/Village/(F7) DefenseTestSetup.cs` | **MISSING — WO-137 Part B (P0)** |
| `Assets/_Modules/Village/BuildMode/*` | **MISSING — WO-108 (P2)** |

**Source docs:** `docs/NORTH_STAR.md`, `docs/CATALOG_SYSTEM.md`, `docs/DEFENSIVE_CATALOG.md`,
`docs/ENGINE_MASTER_PLAN.md`, `docs/BRAND_BIBLE.md`, `WORK_ORDER_137`, `_108`, `_109`, `_113`, `_114`,
`_110`, `_111`, `_115`.
