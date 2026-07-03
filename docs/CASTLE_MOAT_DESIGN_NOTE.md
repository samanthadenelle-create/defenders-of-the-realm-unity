> **SUPERSEDED (owner ruling 2026-07-03):** the moat band width is ~14m (the live
> CastleMoatBuilder r=44..58 band) — it must cover the SEAM from castle plinth to
> OuterWorld terrain. Any '~3 units wide' figure below is STALE.

# Castle Moat + Four Drawbridges — Design Note (WO-509 frame)

Status: DESIGN NOTE + first-pass visual BONES shipped (`CastleMoatBuilder`, flag `ff.castlemoat`).
Owner intent captured 2026-06-24 (overnight). This is the unifying frame for **boundary +
four-gate + base-defense**.

## The frame (one breath)

The castle's "you cannot go past here" edge should read as **deliberate**, not an invisible
wall. A **narrow water MOAT** ring is the natural impassable boundary ("water makes it make
sense"). The only ways across are **4 WIDE DRAWBRIDGES** at the cardinal gates (N/E/S/W) — the
**intentional exits**. Those same bridges are the defensive **CHOKEPOINTS** (enemies must funnel
across a single lane that towers/troops cover; a **raised** bridge seals the lane entirely). The
4 bridges ARE the **WO-509 four RegionGates** — today only the south is a functional crossing.

So the moat is three things at once:
1. **Boundary** — the diegetic no-go edge of the castle island.
2. **Four-gate exits** — wide, readable, deliberate ways out (WO-509).
3. **Base-defense backbone** — single-lane chokepoints, raisable to seal (ties to
   `ff.basebuilding`: tower-mages / troops / waves pillar).

## Boundary source (SME, cite)

- Castle hub = **MainCastle_Hall**, built by `Assets/Editor/CastleHubBuilder.cs` (editor batchmode).
- South gate (the authored side) = castle-local **(-4.37, 0, -40.6)** —
  `Assets/Resources/Data/castle-south-recipe.json:1`.
- The **4 cardinal gates** are that south gate rotated about origin by yaw {0,90,180,270}:
  `world = Quaternion.Euler(0,yaw,0) * southGate` — `CastleHubBuilder.BuildGateExitStrips` /
  `MakeGatePose` (`CastleHubBuilder.cs:712-725`), and the inner-ring sides array
  (`CastleHubBuilder.cs:483`). x4 rotational symmetry: every side shares the same lateral
  offset (`southGate.x`), read at `CastleHubBuilder.cs:480`.
- Perimeter scale: south gate radial ~40.6m; corner towers radial ~42m
  (`castle-south-recipe.json` `CornerTower_South` at x=-42.33); inner CoC wall ring at
  half-extent **18m** (`CastleHubBuilder.cs:435`).
- The existing **south seam** to OuterWorld = `region-gates.json` row `castle_to_outerworld`
  (`Assets/Resources/Data/region-gates.json:3-14`), assembled at runtime by
  `RuntimeRegionGate` (deck weld + masked-warp trigger + HeroLinkCrossing + funnel panels +
  AI NavMeshLink) — `Assets/_Modules/Village/World/RuntimeRegionGate.cs`. **This is the crossing
  primitive WO-509 replicates x4.**
- Water precedent: `MoatWaterShimmer` (DEF-195) — a proven shared-material ripple/scroll for a
  moat ring (`Assets/_Modules/Village/MoatWaterShimmer.cs`); built originally for the abandoned
  `Village.unity` moat (`VillageSceneBuilder.Fortify.cs BuildMoat`). REUSE it.

## The KEY lever (owner, emphasized twice): SHRINK the footprint, then a THIN moat

The moat must be **~3 units wide**, NOT a wide flood. The mechanism is **shrink the castle
playable footprint inward** so only a thin ~3-wide water ring remains between the castle edge
and the scene edge. **Moat width is the secondary tunable (default 3); the footprint shrink is
primary.**

This shrink is an **editor/architect-lane** change (CastleHubBuilder geometry + a NavMesh
re-bake) — it CANNOT be done blind from a runtime injector without breaking the hand-tuned
seam/navmesh weld. It is flagged here as the owner's key decision + the slice-1 work.

## What shipped now (first-pass visual BONES)

`Assets/_Modules/Village/World/CastleMoatBuilder.cs` (DeNelle.Village.World), flag
`FeatureFlags.CastleMoat` (`ff.castlemoat`, default ON), self-bootstrap mirroring
`OuterWorldBoundaryInjector` (AfterSceneLoad + sceneLoaded), idempotent, Guarded, WebGL-safe,
ASCII-only, engine-primitive + URP/Lit (no gitignored packs), `FlowTrace.Step("CastleMoat",...)`:

- A square **water MOAT ring** of 4 translucent teal quads at centreline radius **46m**
  (just outside the ~42m perimeter), width **3m**, sunk to y=-0.4 — with `MoatWaterShimmer`
  attached for the flowing-water read.
- **4 wide wooden DRAWBRIDGE decks** (width 9m) at the cardinal gates, each placed by the
  x4-symmetry gate radial + the recipe lateral offset, spanning the channel with bank overlap.

Tunables (top of `CastleMoatBuilder.cs`): `MoatCentreRadius`, `MoatWidth` (=3), `WaterY`,
`WaterColor`, `BridgeWidth`, `BridgeBankOverlap`, `BridgeY`, `BridgeColor`.

**Explicitly NOT done now** (needs the editor/architect lane; would break the navmesh if guessed):
- Footprint shrink (the primary lever) — CastleHubBuilder geometry + navmesh re-bake.
- N/E/W as FUNCTIONAL crossings — `region-gates.json` has only the south row.
- Moat carved into navmesh as an obstacle; raise/lower lever.

## Sliced build plan (WO-509)

1. **Footprint shrink (editor, architect lane, serialization bottleneck §9).** Pull the castle
   playable extent inward in `CastleHubBuilder` so the scene-edge margin is ~3m, then re-bake
   (`BatchAddFloorAndBakeCastle`). Re-tune `MoatCentreRadius` to the new perimeter. Keep the
   south seam weld intact (re-verify `RUNTIME_SEAM_NAV_OK`).
2. **Moat as real channel.** Replace the visual ring with a moat that is also a NavMesh obstacle
   OUTSIDE the bridges (so off-bridge crossing is impossible), bridges remain the only walkable
   spans. Reuse the `MoatWaterShimmer` material.
3. **Four functional RegionGates.** Add 3 rows to `region-gates.json` (`castle_north/east/west_to_*`)
   — `RuntimeRegionGate` already builds one crossing per matching `from==scene` row, so N/E/W
   light up with the south primitive's deck/trigger/link. Confirm `to` targets per WO-509.
4. **Drawbridge as defensible chokepoint.** Single-lane width tuned so a tower covers it; flank
   each bridge with tower mounts. Add the **raise/lower** lever: a raised bridge removes the deck
   collider + carves the NavMeshLink (seals the lane). Gate the gameplay behind `ff.basebuilding`.
5. **Polish.** Swap the primitive decks for a committed bridge prop; gatehouse framing; water VFX.

## Constraints honored
No `.unity` hand-edit (builder/injector only, §3). New file + one flag. No compile-gate / commit /
Unity run. Brace-balanced. ASCII-only. Mobile-cheap (shared materials, 4 quads + 4 decks).
