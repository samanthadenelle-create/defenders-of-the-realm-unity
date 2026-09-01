# WO-1292 — Environment + prop dressing onto Synty

**Status:** READY TO IMPLEMENT (BLOCKED ON WO-1291 — dress last, once the buildings set the language)
**Minted:** 2026-09-01 (CLI, banner bumped 1289 -> 1293 in the same edit)
**Branch:** `feat/synty-art-retheme`   **Lane:** 4 of 4 (Synty art re-theme)
**Owner ruling 2026-09-01:** FULL re-theme, everything Synty.

---

## AVAILABLE ART (counted 2026-09-01)

`Assets/Synty/PolygonFantasyKingdom/Prefabs/`: **Environments 189** (rocks, trees, cliffs, foliage) ·
**Props 499** — incl. `Banners/ 43`, `BattleGround/ 46`, `Furniture/ 116`, `Paths/ 27`, `Preset/ 16`,
`DeadBodies/ 44` · **Items 260** · **Generic 31** · **Vehicles 12**.
Plus `Assets/Synty/PolygonGeneric/Prefabs/` — **495** more.

## CURRENT STATE

The hub scene dressing is polyperfect + Quaternius: the scene carries ~140 `Rock_*_Color1` prefab
instances (`Rock_1_A` .. `Rock_6_G`), `Tree_Of_Life`, `DistantMountainPeak`, `CavePortal`, `Well`,
`Anvil`, `EchoHollow_Pets_RoamingArea`. These read as a different pack from the re-themed buildings.

## THE WORK

1. **Rocks / foliage** — replace the ~140 `Rock_*` instances with Synty `Environments/*` equivalents,
   preserving transforms. Script the swap by name mapping; do not hand-edit the `.unity` (CLAUDE.md §3).
2. **Paths** — `Props/Paths/ 27` for the town footpaths, reconciled with the `Path_Dirt` terrain layer
   stamped by `PaintNaturalPaths` (do not double up: one path authority).
3. **Banners** — `Props/Banners/ 43` for gate/tower/keep ownership dressing. Owner is red/green
   colourblind: separate by SHAPE and VALUE, never hue (memory `owner-colorblind-delegate-visual-creative`).
4. **Furniture / market dressing** for the storefront frontages.
5. **Keep `Tree_Of_Life` (Heart of Elarion) unless the owner rules otherwise** — it is canon, at
   world origin (0,0,0), not generic dressing.

## ACCEPTANCE CRITERIA

- [ ] Scene triangle count and draw calls reported before/after; no regression on mobile budget.
- [ ] `COMPILE_GATE_OK` + `REGRESSION_OK <n>/<n> suites` on FRESH logs; **`R2_PARITY_OK`** if any
      Addressable content changed (CLAUDE.md §16 — content-hashed bundles, every build needs its own push).
- [ ] NavMesh re-baked; `CastleGateNavVerify` and `TROOP_WALL_NAV_OK` still pass (props carry colliders).
- [ ] Greyscale check on the final frame: buildings, ground and props separate by value, not hue.
- [ ] `RunCaptureHeadless` screenshots — **this lane's output is the final picture the owner asked for.**

## DO NOT TOUCH

- `Assets/Generated/Terrain/**` (WO-1289) · castle perimeter (WO-1290) · `structures-catalog.json` (WO-1291).
- The Heart of Elarion at world origin. Village name is **Elarion**, never "Avalon".
