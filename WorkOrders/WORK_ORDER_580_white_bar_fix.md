# WORK ORDER 580 — Stray white bar/box in MainCastle_Hall hub

**Status:** IMPLEMENTED (CLI to gate/commit; PO to felt-verify + close)
**Source:** Owner F8 felt-test 2026-06-28, MainCastle_Hall — "white bar in front of me,
nothing there i can see." Plus coordinator-relayed companion artifact: "looks like
outworld bends under castle."
**Silo:** Hub world/visual (Core + Village.Items). Edit-only; no scene hand-edit (§3).

---

## RCA

### The "strong lead" (nav helpers rendering white) is STALE — NOT the current cause
The prior ground-Y RCA flagged `GateExit_*_Nav` / `NavMeshFloor_Invisible_Walkable` /
`CourtyardFloor_Nav` as white nav-only objects. **They are already non-rendering in BOTH
the baked scene AND the builder**, so they cannot be the white bar:

- Scene `Assets/Scenes/MainCastle_Hall.unity`: every nav helper's `MeshRenderer` has
  `m_Enabled: 0` (e.g. `GateExit_North_Nav` GO @ line 8961 → renderer @ 9002 `m_Enabled: 0`;
  `CourtyardFloor_Nav` GO @ 6648 → renderer @ 6703 `m_Enabled: 0`). A full parse of the
  scene found **zero** enabled renderers with nav/floor/strip/placeholder names — the only
  enabled suspiciously-named renderers are the legitimate `Gate_South` arch meshes.
- Builder `Assets/Editor/CastleHubBuilder.cs`: `BuildGateExitStrips` disables each strip
  renderer (`r.enabled = false`, **line 765**) and `CreateInvisibleFloor` disables the
  courtyard nav plane renderer (**line 880**). So a rebuild keeps them hidden too.

Conclusion: the nav helpers are correctly invisible and were not touched.

### Actual cause — bare `CreatePrimitive` placeholders left on Unity's DEFAULT material (renders FLAT WHITE under URP)
The white bar/box is **runtime-created**, not baked. The only runtime primitives in the hub
that keep the default (untinted) material — which URP draws as flat white — are the
pack-missing **placeholder cubes**:

- `Assets/_Modules/Village/Items/CraftingStationInjector.cs:200` — `Apothecary_Placeholder`,
  `CreatePrimitive(Cube)`, scale (2,2,2), **no material assigned**, renderer enabled. Spawned
  only when no apothecary structure model resolves (Resources/Structures pack absent — the
  packs are gitignored on fresh/lean clones, so the owner's build shows it).
- `Assets/_Modules/Village/Items/JewelerStationInjector.cs:181` — `JewelersBench_Placeholder`,
  identical shape/condition.

To the owner (whose build is missing the station art) the station model is "nothing there,"
yet a stark white 2×2×2 box appears at the station spot = "white bar/box in front of me,
nothing there i can see." This is exactly the "default-primitive with default white URP
material" culprit class the work order called out.

Belt-and-suspenders: `Assets/_Modules/Core/GroundZFightFixer.cs:230` builds the 90×90 m
`HubOpaqueFloor (runtime)` plane and only assigns a stone material **if one resolves**
(`LoadHubFloorMaterial()` returns null only if the URP/Lit shader is stripped). If it ever
returned null the bare plane would render as a giant white slab. Improbable in a real URP
build, but it shares the same root, so it is hardened too.

### Separate finding (distinct root) — "outworld bends under castle"
This is NOT a white primitive; do not force the white-bar fix to cover it.
- `Assets/Editor/ExteriorTerrainBuilder.cs:204` `CastleDepressionDepth = -3f`, applied via
  `Mathf.Lerp(elevatedY, CastleDepressionDepth, castleW)` (**line 394**) across the castle
  footprint out to `CastleClearHalfX/Z` (~±62 m). The OuterWorld terrain is intentionally
  bowled down to −3 m under the castle so it can't poke through the floor.
- The runtime hub floor (`GroundZFightFixer.cs:233`, scale 9 → 90×90 m = ±45 m) only covers
  the ±44 m wall interior. So the ring from ±45 m to ±62 m is **uncovered depressed terrain
  bowl**, visible through the gate openings / at the floor edge as the OuterWorld "bending /
  warping" down under the castle.
- **Recommendation (NOT applied — needs PO call + felt-verify):** either widen the
  `HubOpaqueFloor` plane to ~±62 m to fully cap the depression, or reduce the depression
  depth/radius. Owner decision: this is intentional anti-poke depression, so changing it is
  a design tradeoff, not an obvious bug. Flagged for owner.

---

## FIX (implemented, edit-only)

1. `Assets/_Modules/Village/Items/CraftingStationInjector.cs` — after creating
   `Apothecary_Placeholder`, call new `TintPlaceholderStone(cube, ...)` which assigns a
   neutral warm-stone URP/Lit material (Standard fallback for non-URP editor). The station
   stays visible + interactable (collider/Building untouched); it just never renders white.
   FlowTrace.Step proof line on tint (§12).
2. `Assets/_Modules/Village/Items/JewelerStationInjector.cs` — same `TintPlaceholderStone`
   for `JewelersBench_Placeholder`.
3. `Assets/_Modules/Core/GroundZFightFixer.cs` — `EnsureHubOpaqueFloor`: if the stone
   material can't be resolved, DISABLE the renderer (nav plane still handles walkability)
   instead of leaving the default-white slab; FlowTrace.Warn proof line.

Colliders / NavMesh / Building interaction are all preserved — only the white default
material is eliminated. Fix lives in the runtime CODE so it cannot recur on rebuild.

### Why it won't recur
The placeholders are created fresh every load by these injectors; the tint now happens at
creation, so any future rebuild/reload of MainCastle_Hall re-applies it. The nav helpers
remain disabled at both scene and builder level.

---

## VALIDATION
- Brace check: CraftingStationInjector.cs 25/25 OK; JewelerStationInjector.cs 25/25 OK;
  GroundZFightFixer.cs 43/43 OK.
- No `.unity` scene files hand-edited (§3). No reflection added. FlowTrace used (§12).
- Worktree fast-forwarded to branch tip d455bd42 before editing.

## Modified files (for reconcile, explicit paths)
- `Assets/_Modules/Village/Items/CraftingStationInjector.cs`
- `Assets/_Modules/Village/Items/JewelerStationInjector.cs`
- `Assets/_Modules/Core/GroundZFightFixer.cs`
- `WorkOrders/WORK_ORDER_580_white_bar_fix.md` (this file)

## OWNER-DECISION FLAG
Live F8 capture was not run (build/gate disallowed for this agent), so the exact white-bar
identity is inferred from static data, not a captured `[Flow:*]` line. The fix hardens ALL
confirmed default-white runtime primitives in the hub, so it covers the bar regardless of
which fired — but on PO re-test, the new `[Flow:Crafting] WO-580: tinted ...` line will
prove which placeholder was the culprit (or its absence will say the bar was something else,
e.g. the depression-bowl artifact). The "outworld bends under castle" item is a separate
root (terrain depression coverage) left for an owner design call.
