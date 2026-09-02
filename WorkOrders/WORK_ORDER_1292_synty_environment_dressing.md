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

---

## ⭐ OWNER DIRECTION 2026-09-02 (added by CLI from a live browsing session)

She reviewed the Synty environment content in-editor and ruled, verbatim:

- **"the enviornment stuff i love"** / **"it looks amazing"** — the environment direction is APPROVED,
  not merely permitted. Proceed with confidence on this lane.
- **"follow your guidance on Synty. LEts use as much as we can cohesively"** — the operative word is
  COHESIVELY. The audit's finding is that a MIXED-pack look reads worse than either pack alone; the
  hub still carries ~140 Polyperfect `Rock_*` instances sitting beside now-Synty buildings. Replacing
  a scatter of them piecemeal is worse than either finishing a region or leaving it. Dress by
  coherent AREA, not by asset count.
- **"id love to get some type of coblestone or castle floor"** — a NAMED, concrete ask. Highest
  priority within this ticket.

### The floor/ground inventory that exists (measured 2026-09-02, do not re-derive)

Castle floors, `Assets/Synty/PolygonFantasyKingdom/`:
`SM_Bld_Castle_Floor_Stone_01` .. `_04` · `_Stone_Gap_01/_02` · `_Stone_Pool_01` ·
`_Stone_Round_S/_M/_L_01` · and the wood twins `SM_Bld_Castle_Floor_Wood_01`..`_04`,
`_Wood_Hatch_01`, `_Wood_Round_S/_M/_L_01`. Also `SM_Bld_Base_Floor_*` (Half, Hole, Round, 45,
Combined) and `SM_Bld_House_Floor_Stone_01`.

Counts by token across the Synty tree: **floor 49 · tile 48 · path 43 · ground 85 · road 26 ·
street 10**. The Round S/M/L set plus Gap pieces means a courtyard can have a proper centre inset and
clean edges rather than a tiled rectangle.

### Constraints that bind this work

- ⛔ **The hub scene is `Main_Castle_Overworld` and is NEVER hand-edited** (CLAUDE.md sec.3,
  resave-corruption history). Dress via the builder / a runtime injector, not by dragging in the
  editor. Never bake with the editor open.
- ⛔ **461 MB of raw pack must not enter the APK.** Anything used goes through Addressables/remote.
  Note the 5 existing perimeter prefabs are DIRECT scene instances, i.e. in-build - do not follow that
  precedent for new dressing.
- ⛔ **Any Addressables addition re-hashes bundles**: content build + `tools\r2-ship.ps1`, judged by
  `R2_PUSH_OK` + `R2_PARITY_OK` on a FRESH log. A prior push never covers a new build (CLAUDE.md
  sec.16 - four incidents).
- ⛔ **Do NOT run the Polyperfect URP repair over Synty.** Synty is already URP-native with atlas-shared
  materials; rebinding would break it.
- ⚠ Give any new Synty entry a DISTINCT address. Re-wraps reusing original filenames are what let a
  stone castle tower masquerade as her Tripo watchtower; 27 addresses still carry that ambiguity
  (WO-1305 Part B). Do not add a 28th.
- The owner is red/green colourblind - ground/path readability must not depend on hue contrast alone.

Full inventory, usage method and ranked opportunities: `docs/reference/SYNTY_PACK_REGISTRY.md`.
