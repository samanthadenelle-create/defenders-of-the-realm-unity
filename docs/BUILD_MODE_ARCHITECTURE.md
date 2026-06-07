# Build Mode Architecture — the CREATE verb (Rungs 4→6)

> Player base-building. The player enters Build Mode, places/moves/rotates/sells walls, towers, mines and buildings on a grid, pays from harvested resources, the layout persists, and they defend what they built. CoC-style, mobile-first. **This is a reconciliation doc: ~70% of the machine already exists** — the gap is content, full economy, the upgrade verb, mobile touch, a real plot, and the defend/arena tie-in. Supersedes the forward-looking `docs/build-mode-architecture.md` (WO-108 spec), now largely *implemented*.

## 0. TL;DR
Build Mode is **already wired end-to-end for towers**: enter → top-down camera + frozen waves → tap a palette card → green/red ghost → tap to place → charge crystals → persists in `GameState.BaseLayout` (save v14) → reload rebuilds it. Move + sell (50% refund) work. NavMesh is handled by **carving obstacles, not runtime bake**. The save struct is grid-relative + headless-replayable — the async-Arena seam is already designed in.

What's missing is **not the engine** — it's: (1) palette empty except towers, (2) cost is crystals-only (4-resource `EconomyService` ignored), (3) no **upgrade** verb (wall tiers don't exist), (4) input is legacy mouse, (5) no bounded **plot**, (6) nothing explicitly couples "defend *this* layout"/Arena snapshot.

## 1. Already built (do not greenfield)
`BuildModeController` (enter/exit/place/move/sell, camera, wave freeze) · `PlacementGrid` (pure cell grid, 3m, 28×22) · `GhostPreview` (green/red ghost via VisualFactory.Skin) · `BuildPaletteUI` (code-built, reads CatalogRegistry.OfType) · `BuildSelectionUI` (move/sell) · `PlacedStructure` · `BaseLayoutLoader` (rebuilds from GameState.BaseLayout, **carving NavMeshObstacle**) · `StructureFactory` (the ONE create path) · `CatalogRegistry`/`CatalogEntry` (Core) · `PlacedStructureData` (Core, grid-relative, save v14 + v13→v14 migration) · `EconomyService` (Wood/Food/Iron/Crystals, ResourceCost) · `ResourceBuildingProgression`/`ResourceLedger`/`TechTree` (CoC level tables, Magic tier) · `WallSegment` · `WallNavObstacleInstaller` · `BuildMenu` "Build Mode" entry.

## 2. Gaps (the actual work)
- **G1 Empty palette** — only Towers registered in `CatalogBootstrap`; Wall/Gate/Resource/Decoration buckets empty.
- **G2 Crystals-only cost** — `BuildModeController.Place` charges `AddCrystals(-cost)` from a single int; never touches `EconomyService.ResourceCost`.
- **G3 No upgrade verb** — `level` persists but is unused; no wall tiers (wood→stone→reinforced); the CoC sink lives only in the separate `ResourceBuildingProgression` panel.
- **G4 No rotation footprint swap** — `FootprintCells` returns square (n,n); yaw never swaps W/H (wrong for 1×3 walls/gates).
- **G5 Legacy mouse input** — all `Input.GetMouseButton*`/`KeyCode`; touch driver was deferred (P2).
- **G6 No bounded plot** — placement = whole village interior.
- **G7 Seed empty** — default village is `VillageSceneBuilder` output, NOT `PlacedStructure`s, so players add on top, can't move/sell the existing town.
- **G8 "Defend what you built" implicit** — base + waves coexist but aren't coupled; no Arena snapshot.
- **G9 `StructureFactory.AttachBehavior`** only knows DefenseTower + WallSegment — mines/gates/resource-buildings have no behavior case.

## 3. Architecture decisions
- **NavMesh = carving, NOT runtime bake** (the hardest-won lesson; already implemented). Add `carveOnlyStationary=true` to `BaseLayoutLoader.AddFootprintBlocker` (matches `WallNavObstacleInstaller`). Gate-clearance rule prevents a fully-walled-off NavMesh dead-state. Fallback if profiling demands: ONE rebake on `Exit()` only, never per-placement.
- **Keep `PlacementGrid.CanPlace` + cost checks PURE** (no scene/camera dep) — that's the headless-replay seam the Arena anti-cheat needs.
- **Economy:** route Build Mode spend through `ResourceLedger` (GameState-backed for all four) — unifies build cost + upgrade cost + harvest credit on one persisted surface. (EconomyService Wood/Iron are session-only by design; Crystals/Food GameState-backed.)
- **Persistence = recipes not objects** (built): `BaseLayout = List<PlacedStructureData>` (itemId+cell+yaw+level), grid-relative, replayed via `StructureFactory.Create` — the same path builder + future Arena server use. New fields = additive-nullable + schema bump + default-on-read (v14/v15 precedent).
- **Boundaries:** PlacedStructureData/CatalogRegistry/GameState in Core (pure data); behavior in Village; `behaviorId`→component switch in `StructureFactory.AttachBehavior` IS the boundary (no reflection — add `case`s for G9, not reflection).

## 4. "Defend what you built" + Arena
- **Now:** `BaseLayoutLoader` spawns the base; `WaveManager`/`EnemyBrain` already attack `IDamageableStructure`s. A placed wall/tower is instantly a carved obstacle + a real target. `FreezeWaves`/`ResumeWaves` = the CoC build↔defend rhythm; `Exit()` = "lock my layout, bring the wave."
- **Later (Arena):** the same `BaseLayout` IS the raid snapshot (designed for it — grid-relative, prefab-keyed, headless-replayable). Defense snapshot = the save payload; raid loads via `BaseLayoutLoader.Rebuild`; server re-verifies headless (pure functions); offense = player-authored attack AI on `EnemyBrain` + smart targeting (healer>ranged>tank scorer). Async = both sides automated vs snapshots. **No Arena code exists yet, but nothing blocks it — every current choice enables it.**

## 5. Delivery plan (smallest shippable first → North Star rungs)
| Step | Scope | Rung | Status |
|---|---|---|---|
| S0 data spine (PlacedStructureData + BaseLayout + v14 + loader + factory) | 4 | **DONE** |
| S1 place + persist (towers) | 5 min | **DONE** |
| S2 edit verbs (move/sell/select/gate-clearance) | 5 | **DONE** (rotate axis-swap G4 TODO) |
| **S3 fill the palette** (register walls/gates/mines/decor G1 + behavior cases G9 + tabs) — *a player can draw a whole base* | 5 full | **TODO — highest leverage** |
| S4 full economy (multi-resource cost via ResourceLedger, G2) | 5 | TODO |
| S5 upgrade verb + wall tiers (wire `level`, wood→stone→reinforced, G3) — *the CoC sink* | 5→6 | TODO |
| S6 mobile touch (LeanTouchBuildDriver behind IBuildInput, G5) | 5 | TODO (parallel-safe) |
| S7 seed default town as recipes (G7) | 4→5 | TODO |
| S8 bounded plot (G6) | 4 | TODO |
| S9 Arena snapshot (export/import, headless re-verify, attack AI, G8) | 6 | LATER (post-backend) |

**Recommended next sprint:** S3 → S4 → S6 (parallel). Turns "place towers" into "build a base from walls + towers + a mine, paid from your harvest, on a phone" — the CREATE verb landing.

## 6. Risks
- **R1 NavMesh carve cost** (mitigated: carveOnlyStationary; Exit()-only rebake fallback if profiled).
- **R2 Economy persistence split** (route through ResourceLedger before offline/Arena; demo-safe now).
- **R3 Save migration** (additive-nullable + schema bump + default-on-read; BaseLayout = Arena public contract once raids land).
- **R4 Touch precision** (3m cells + 90° yaw stay forgiving; isolate placement behind IBuildInput).
- **R5 VillageSceneBuilder bottleneck** — Build Mode must NEVER edit it; seed town via a separate snapshot util, never hand-edit the builder or Village.unity.
- **R6/R7 Catalog scope/empty tabs** — start at ~6–8 entries; hide unregistered tabs until S3; check polyperfect catalog before referencing `_M` prefabs; LogWarning (not error) on missing.

---
**Headline:** the CREATE-verb engine is built and proven for towers; the remaining work is content registration, multi-resource cost, the upgrade verb, touch input, and the defend/arena coupling — **all additive on seams that already exist. A re-centering, not a rebuild.**
