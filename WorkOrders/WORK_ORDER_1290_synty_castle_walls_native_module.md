# WO-1290 — Castle walls rebuilt on Synty's native 5m module

**Status:** IN PROGRESS (2026-09-01: ring built + gated; corner treatment NOT done — see RESULT notes)
**Minted:** 2026-09-01 (CLI, banner bumped 1289 -> 1293 in the same edit)
**Branch:** `feat/synty-art-retheme`   **Lane:** 2 of 4 (Synty art re-theme)
**Silo:** Castle perimeter geometry. File-disjoint from WO-1289 (terrain) / WO-1291 (buildings).
**Owner ruling 2026-09-01:** wall height = **Synty NATIVE, 5.00m panel + 1.38m battlement = 6.38m.
Zero scaling.** (Options of stacking to ~11.4m or scaling to today's 8.49m were both declined.)

---

## PROVING DATA — why the current walls look bad (measured 2026-09-01)

`Assets/Resources/Data/castle-south-recipe.json` is **four pieces**, mirrored x4:

| name | prefab | pos | scale |
|---|---|---|---|
| `Gate_South` | `Gate_Medieval_Medium` | (-4.37, 0, -40.60) | 1,1,1 |
| `Wall_South_L` | `Wall_Medieval_Stone` | (-24.80, 0, -40.55) | **1.62**, 1, 1 |
| `Wall_South_R` | `Wall_Medieval_Stone` | (18.11, 0, -40.93) | **1.95**, 1, 1 |
| `CornerTower_South` | `Tower_Castle_Round` | (-42.33, 0.04, -40.03) | **1.28**, 1, 1 |

Source mesh `_M/Meshes_M/Medieval_M/SM_Wall_Medieval_Stone.fbx` measures **15.75m x 8.49m x 2.39m**
(264 verts). Therefore:

- `Wall_South_L` = one 15.75m mesh stretched to **25.5m** — every stone/merlon **62% wider** than authored.
- `Wall_South_R` = stretched to **30.7m** — **95% wider**, visibly different from L on the same wall.
- `SeamFill_1` = `scaleX = gap / baseWallLen` (`CastleWallsFromRecipe.FillGap`) — a third, arbitrary stretch.
- `CornerTower_South` = a **round** tower scaled **1.28 on X only** — an ellipse.

Non-uniform scale also breaks the normal-map tangent basis, so L, R and the filler light differently.
Scene inventory of `Main_Castle_Overworld.unity` confirms **5 visible objects per side, 20 for the whole
castle**. There is no tiling and no modularity: each side is three differently-stretched copies of one
slab. **Every seam "fix" adds another stretch factor — the file is structurally incapable of a good wall.**

Also proven: **no `WallSegment` exists on the hub perimeter** (only `GridWallBuilder`,
`PerimeterWallGenerator`, `RaidBaseGenerator`, `StructureFactory` add it). The castle walls today have
no tier, no damage, no repair.

## THE REPLACEMENT ART — measured from the FBX (Synty is cm; 500 units = 5.00m)

| module | size (m) | tris |
|---|---|---|
| `SM_Bld_Castle_Wall_01` | **5.00 x 5.00 x 0.50** | **20** |
| `Wall_02 / 03 / 04 / 05` | 5.00 x 5.00 x 0.50-1.12 | 52 / 100 / 126 / 84 |
| `Wall_Arrowslit_01` | 5.00 x 5.00 x 0.50 | 116 |
| `Battlements_01` / `_Half` | 5.00 / 2.50 x 1.38 x 0.50 | 146 / 78 |
| `Wall_Corner_S / M / L_01` | 2.75 / 4.00 / 5.25 turn | 56 each |
| `Wall_Tower_S / M / L_01` | 2.44 / 3.05 / 3.82 dia x 7.52 tall | 608 each |
| `Wall_Gate_01` | 5.67 x 5.86 x 1.26 | 2,530 |
| `Wall_Gate_L_01` (gatehouse) | 7.35 x 15.34 x 2.00 | 3,032 |
| `Drawbridge_01` | 5.00 wide | 1,046 |
| `DestroyedWall_*` (11 prefabs) | damaged/rubble variants | ~94+ |

Budget at the current footprint (~85m/side, 17 modules x 4 sides): panels 68x20 = 1,360 + battlements
68x146 = 9,928 + 4 towers + gates = **~15k tris for the entire castle**. Today's 20 stretched slabs are
~8k; the `GridWallBuilder`/Tripo path is **1.28M**. This is better-looking AND cheaper than what ships.

**No magenta risk:** `Materials/Walls/Castle_Wall_0*.mat` -> `PolygonGeneric/Shaders/Generic_Basic.shadergraph`,
whose active targets include `UniversalTarget` + `UniversalLitSubTarget`. `com.unity.shadergraph 17.4.0`
is already in `Packages/manifest.json`. Prefabs ship with `BoxCollider` + assigned materials already.

## THE WORK

1. **Neutralise `Assets/Synty/SyntyPackageHelper/Editor/SyntyPackageHelper.cs` before any batchmode run.**
   It is `[InitializeOnLoad]` + `projectChanged` and calls `EditorUtility.DisplayDialog` plus a Package
   Manager `AddAndRemoveRequest` — an uninvited dialog and a `Packages/manifest.json` mutation inside our
   headless gate chain. Shader Graph is already installed so it should no-op, but do not rely on that.
2. **Author the module prefab set** under a tracked path (Synty itself is gitignored — see `.gitignore`
   "Synty POLYGON" block). Each module: URP material, **BoxCollider (not MeshCollider)**, `Structure`
   layer, static flags, lightmap UVs. Measure seat offsets — do not assume base-at-origin.
3. **Re-point `Assets/Editor/WallTools/PerimeterWallGenerator.cs`** at the module set at its **native
   5.00m spacing with scale 1,1,1**. Its algorithm is already correct (4 corner towers via the
   90/180/270 origin-mirror, span derived between tower ends, slot count forced ODD so the centre slot
   is a natural gate). Delete the 1.5m box-fit — it is the same distortion disease.
4. **Retire the stretch path.** `CastleWallsFromRecipe.Recreate()` stops being the shipping wall source;
   `CastleHubBuilder.BatchRebuildCastleFromRecipeAndBake` calls the new generator. Keep the recipe JSON
   as the source of the **gate lateral** (`gateX = -4.37`) — `CastleMoatBuilder` derives its four bridge
   positions from `southGate.x` (`CastleMoatBuilder.cs:246 gateLateral`), so moving the gate moves the bridges.
5. **Wire the damage ladder that finally has art:** put `WallSegment` on the panels and map its collapse
   states to the `DestroyedWall_*` prefabs.

## MUST SURVIVE — each is a scar with an F8 behind it

- [ ] **`Structure` layer on ALL masonry** (WO-449). Towers/`HeroTargetIndicator`/`PlayerAttackController`
      linecast against it; lose it and towers shoot through walls.
- [ ] **Gate doorjamb colliders + the 15m nav lane** (`GateClearHalf 7.5`, `DoorwayHalf 2.5`). The
      "invisible wall near the south gate, walk around it" defect.
- [ ] **`CastleWallNavObstacleInstaller`** must still find the masonry colliders at runtime — it matches
      `CastleSide_*`. The hero is a `NavMeshAgent` and **ignores physics colliders**; only a carved
      NavMesh stops her ("im in the wall", owner F8 2026-07-15). If the root name changes, update the match.
- [ ] **`CastleWallStairsSeatFix`** re-seats the 4 rampart stairs from live renderer bounds, so it adapts
      to the new 6.38m height automatically — but re-run it AFTER the rebuild and before the bake.
- [ ] **`CastleTroopWallNav.BakeAndVerify`** must still log `TROOP_WALL_NAV_OK` (troops garrison the ramparts).
- [ ] **No `Shader.Find` at bake time** — returns NULL in batchmode (`CastleHubBuilder.cs:2549`). Reference
      material assets explicitly.
- [ ] Perimeter stays inside **r=44** (the moat plinth face; water band 44..62).

## ACCEPTANCE CRITERIA

- [ ] Zero non-uniform scale on any wall/tower/gate object in the rebuilt perimeter (assert scale == 1,1,1).
- [ ] `COMPILE_GATE_OK` + `REGRESSION_OK <n>/<n> suites` + `TROOP_WALL_NAV_OK` on FRESH logs (markers, never exit codes).
- [ ] `CastleGateNavVerify` passes on all four gates.
- [ ] `RunCaptureHeadless` screenshots of the hub from ground level and from the gate approach, opened and looked at.
- [ ] Triangle count of the perimeter reported and under 25k.

## DO NOT TOUCH

- `Assets/Generated/Terrain/**` (WO-1289), `structures-catalog.json` (WO-1291).
- `MainCastle_Hall.unity` — LEGACY, not the hub. The hub is `Main_Castle_Overworld.unity`.
- Never hand-edit a `.unity` file (CLAUDE.md §3) — rebuild via the builder.
- `Assets/MedievalCastlePackLite/` — SHELVED, superseded by Synty (Built-in Standard shader, no prefabs,
  2.64m wall, single tier). Do not use it.
