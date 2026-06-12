# Castle Blueprint — MainCastle_Hall (exact, measured)

**Spatial source-of-truth** for the home hub, extracted scientifically from the actual
placed geometry (real `Renderer` bounds — NOT extrapolated from prefab × scale).
Regenerate any time with `Defenders → run CastleBlueprint.Extract` (batchmode:
`-executeMethod DeNelle.Editor.CastleBlueprint.Extract`; writes `Builds/castle-blueprint.txt`).

> **Why this exists (the real payload):** this extractor is the prototype for
> **player-created maps**. A player places structures anywhere; you cannot hand-dial
> navmesh / gates / spawns. The only correct path is to *read the real bounds and derive
> everything deterministically* — exactly this. The castle is the controlled test case
> before the same engine runs on player-built bases (CoC×Warcraft base-build + raid pillar).

Origin (Heart of Elarion) = **(0, 0, 0)**. Extracted 2026-06-12, branch `feat/tower-core-loop`.

---

## Gates (4 entrances)
Mirrored ×4 around origin from the authored south gate (`castle-south-recipe.json`).
Frame: **15.75 m wide × 5.22 m deep × 10.52 m tall**, sits at floor Y = 0.

| Gate | Center (x,y,z) | Opening axis | Outward |
|---|---|---|---|
| South | (−4.37, 0, −40.60) | X (travel Z) | −Z |
| West  | (−40.60, 0, +4.37) | Z (travel X) | −X |
| North | (+4.37, 0, +40.60) | X (travel Z) | +Z |
| East  | (+40.60, 0, −4.37) | Z (travel X) | +X |

## Walls + corner towers (south side; others are this rotated 90/180/270)
| Object | Pos | Size | Yaw |
|---|---|---|---|
| Wall_South_L | (−24.80, 0, −40.55) | 25.59 × 8.54 × 2.39 | 180° |
| Wall_South_R | (18.11, 0, −40.93) | 30.65 × 8.49 × 2.39 | 180° |
| CornerTower_South | (−42.33, 0.04, −40.03) | 10.52 × 14.14 × 8.18 | 0° |

## Key structures
| Element | Pos / bounds center | Size |
|---|---|---|
| Keep (MainKeep_CastleWithTwoLevels_Home) | center (0, 16.72, −0.11) | 46.00 × 33.66 × 46.41 |
| Grand stair (courtyard→battlements) | center (30.17, 5.96, 9.38) | 16.93 × 12.50 × 21.60 |
| Courtyard floor (nav) | (0, 0.05, 0) | 90 × 90 |
| Upper battlements (nav) | (0, 11.50, 0) | 44 × 44 |
| Keep interior (nav) | (0, 0.12, 0) | 26 × 26 |
| Gate-exit strips (nav) | each gate ±45.60 | 12 × 26 each |

## Key points
| Point | Coordinate |
|---|---|
| Hero spawn (`HeroStartPoint_PlayerSpawn`) | (0, 0, 0) |
| Exit seam (`WorldGate_ConnectToOuterWorld_Marker`) | (−4.37, 1.50, −44.60), radius 9 |
| Exit seam → warps to | OuterWorld (0, 0.5, −80) |

## Baked NavMesh (committed surface)
- Verts 2127 · tris 953 · extents **min (−58, 0.1, −58) → max (58, 12.8, 58)**.
- Walkable mesh confirmed **8 m outside all 4 gates** (`onNavMesh = True` each) — gates are
  traversable; the visual seam at a gate is the two-scene boundary, not a connectivity break.

## Notes for diagnosing the exit
- `Application.CanStreamedLevelBeLoaded` returns **False for every scene in `-batchmode`**
  (no player manifest) — it is NOT a usable signal headless. Test the transition in editor
  Play mode (where it works) or in a build.
- The exit is a **proximity seam**, not a walk-through: reach within 9 m of the seam point
  and it loads OuterWorld + warps. It finds the hero via `FindWithTag("Player")`.
