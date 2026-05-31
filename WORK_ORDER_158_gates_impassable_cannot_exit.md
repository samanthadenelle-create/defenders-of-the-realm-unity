# WORK ORDER 158 — Gates impassable: hero cannot exit the castle at any of the 4 points

**Status: READY TO IMPLEMENT**
**Priority:** HIGH — playtest blocker. The player is sealed inside the castle; no gate is passable, so the open world is unreachable.
**Date:** 2026-05-30
**Lane:** Architect / World — `VillageSceneBuilder.cs` + NavMesh rebake (CLI). UI spec.
**Source:** owner playtest — *"no gate at all 4 points so cannot exit castle."*

---

## Prime suspect — UI's WO-136 wall-barrier collision (own it, check first)

The wall-barrier collision I (UI) added in `BuildRamparts` is the most likely culprit and must be
checked first. The math *intends* a 6 m opening at South/East/West and a solid North (no gate there):

- `gateHalf = 3f` → gap should be 6 m at S/E/W; **North is intentionally unbroken** (`WallBarrier-North`
  is full-width — the wall mesh has no north gate either).

**But verify these failure modes — any one seals the gates:**

1. **North has no gate by design — confirm that's intended.** If the owner expects **4 exits**, North
   needs a gate gap added in BOTH `BuildWallPerimeter` (the mesh skips `|x|<3` like the others) AND the
   `WallBarrier-North` box (split into two flanking spans like South). Today North is solid on purpose —
   if the design is "4 gates," that's the bug for the north point.
2. **Barrier thickness pinches the opening.** Each barrier is `barThk = 1.2 m` thick and sits at the
   wall line; combined with the gate prefab mesh + any collider at the gate mouth, the *walkable* gap may
   be narrower than the hero's nav agent radius. Check the actual clear width at each gate vs agent radius.
3. **Barrier overlaps the gate prefab.** The two flanking spans are centered at `±(gateHalf + sideHalf)`;
   confirm they don't extend into the 6 m gate mouth (rounding / the gate prefab's own footprint).

## The other prime suspect — the drawbridge/moat is not crossable

Even with an open gate, the **moat** sits just outside (±42→±48). If the **drawbridge deck is not a
walkable NavMesh surface** (or there's no nav link across the moat), the hero reaches the gate and then
hits impassable water — functionally "can't exit." Check:

- Each gate's **drawbridge deck has a walkable collider + is NavMesh-baked** (nav-static, area Walkable),
  and the navmesh is **continuous** gate-mouth → drawbridge → exterior ground.
- The moat tiles are **not** baked as walkable (they shouldn't be) but must not also block the bridge span.

## What to verify / fix

1. Confirm intended **number of gates** — 3 (S/E/W, current) or **4** (add North). Owner decision below.
2. At each intended gate: the **wall mesh gap + the WallBarrier gap align** and leave a clear walkable
   width ≥ the nav agent diameter.
3. Each gate has a **walkable, NavMesh-linked drawbridge** across the moat to the exterior.
4. **Rebake NavMesh** and verify a path exists **interior → through each gate → exterior** (and for
   enemies, exterior spawn → through gate → Heart, unchanged).

## Acceptance criteria
1. Hero can walk **out of every intended gate** to the exterior — no invisible seal at any point.
2. NavMesh is continuous interior↔exterior through each gate + drawbridge.
3. WallBarrier still blocks the **solid wall runs** (the WO-136 fix holds — hero can't walk through actual wall).
4. Enemy spawn→Heart paths still valid.
5. Brace balance; rebuild via builder, editor closed.

## Owner decision — LOCKED: 4 gates (add North)

Owner confirmed **4 gates** — a symmetric four-exit castle. So **North needs a gate added** (it's
currently solid by design). CLI must, for the north point:
1. **Wall mesh** — in `BuildWallPerimeter`, the north wall loop must skip the `|x|<3` center like
   South/East/West do (currently north places an unbroken run) so there's a 6 m opening, and add a
   `Gate_Medieval_*` at `(0, 0, +33)`.
2. **Wall barrier** — split `WallBarrier-North` into two flanking spans (mirror `WallBarrier-South-W/E`):
   `Box("WallBarrier-North-W", (-(gateHalf+sideHalf), barY, barZ), (2*sideHalf, barH, barThk))` and
   `WallBarrier-North-E` at `+(gateHalf+sideHalf)`. (Replaces the single full-width north barrier.)
3. **Moat + drawbridge** — `BuildMoat` currently notes "no north gate"; add the north drawbridge across
   the moat at `(0,0,+~36)` and leave the north gate span clear of moat tiles (like the other three).
4. **Spawn point** — if enemies should also use the north gate, ensure a `WaveSpawnPoint` exists outside
   it (coordinate with the wave system; this may already exist as spawn-3/north).

> This is the UI-authored `WallBarrier-North` being changed — UI is OFF the file (CLI owns it); this WO
> hands CLI the exact edit. The other 3 gates' "can't exit" cause (drawbridge walkability / opening
> width) still applies and must be fixed alongside.

## Done checklist (CLAUDE.md §10)
- [ ] Gate count confirmed (3 vs 4); north gate added if 4
- [ ] Walkable width ≥ agent diameter at each gate (barrier + mesh aligned)
- [ ] Drawbridge decks walkable + NavMesh-linked across the moat at each gate
- [ ] NavMesh rebuilt; interior→exterior path verified at every gate; enemy paths intact
- [ ] WallBarrier still blocks solid wall runs (WO-136 holds); brace balance
- [ ] `WORK_ORDER_158_gates_impassable_cannot_exit.RESULT.md` when complete
