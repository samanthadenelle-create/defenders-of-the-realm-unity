<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK ORDER 136 — Complete Castle Structure: Ramparts, Walkways & Upper Level (with correct-height collision)

**Status: READY TO IMPLEMENT**
**Priority:** High — owner removed the old perimeter; the village currently has no curtain wall
**Created:** 2026-05-30
**Requested by:** Samantha (owner) — "I manually removed [the walls] but can you have a designer complete the whole castle structure with ramparts and walkways up to the upper level… we will need to add the collision at the correct level — should be on the real wall, the new one." Reference Claude Code's notes.
**Lane:** Architect / World — **touches `Assets/Editor/VillageSceneBuilder.cs` (serialization bottleneck, CLAUDE.md §9) + `WallLayout.cs` + a Village rebake.** UI wrote this spec only; CLI/designer implements, compile-gates, and fires the bake (editor closed).

> UI has NOT edited `Assets/` or any `.cs`, and per CLAUDE.md §3 will NOT hand-edit `Village.unity`. Rebake via `Defenders > Week 3 > Build Village Scene` (batchmode `DeNelle.Editor.VillageSceneBuilder.BuildVillage`).

---

## Reference notes (read first — this WO consolidates them)

- **WO-104** (castle fortification + moat): polyperfect stone curtain at X=±42/Z=±33; corner towers `Tower_Castle_Round`/`Tower_Medieval_Big`; cardinal gates `Gate_Medieval`; moat ring ~±48/±39 with drawbridges at the four gates.
- **WO-109** (rampart-level walls + walkways): the upper-level spec this WO fulfills — walkway at wall-top, stair/ramp access, parapet with fall-off collider, elevated corner-tower platforms, nav surface linking up via stairs.
- **WO-114** (wall upgrade tiers): wood→stone→reinforced heights (`WALL_TARGET_H`); new wall should keep the tiering hook.
- **`docs/polyperfect-asset-catalog.md`** — confirm exact prefab names before referencing (`Wall_Medieval`, `Wall_Medieval_Gate`, `Wall_Medieval_Stairs`, `Tower_Medieval_Big/Small`, `Tower_Castle_Round`).
- **`WALL_LAYOUT_GUIDE_mirza_beig.md`** (this session) — modular layout method for a closed curtain wall + corner towers + gatehouse; geometry comes from polyperfect (Mirza Beig is VFX only).

---

## Goal

Rebuild the perimeter as a **complete, real castle wall** — not a hidden-collider/visual-overlay split, but one authored structure — that:

1. Forms a closed curtain wall around the village interior, with **corner towers**, **four cardinal gatehouses**, and the existing gate gaps preserved for enemy lanes.
2. Has a **walkable rampart / upper level**: a walkway behind the parapet at wall-top height, reachable by **stairs or ramps** (and/or through the towers), so the hero can climb up and patrol the upper level.
3. Carries **collision on the real (new) wall at the correct height** — see the dedicated section below. This is the owner's explicit must-fix: the barrier/walk colliders must sit on the actual new wall geometry at the right Y, not floating or at the old hidden-ring height.
4. Is surrounded by a **moat** (~±48/±39) with **drawbridges at the four cardinal gates** — walkable decks aligned to the gate gaps so enemies cross only at the gates (owner: moat folded into this build).

---

## Collision — the correct-level requirement (owner priority)

The old system put the `WallSegment` barrier collider on a **hidden inner ring** (±28/±21) while the visible wall was a separate overlay at ±42/±33 — collision and visuals were at different places. **That split is gone.** For the new wall:

1. **Barrier collision lives on the new wall geometry itself**, at the visible wall line — enemies and the hero collide with the wall they can see, not an invisible inner ring.
2. **Vertical extent is correct:** the barrier collider spans from ground (Y=0) to the wall-top height for the current tier (`WALL_TARGET_H[tier]`), so nothing clips over a too-short collider and there's no collider taller than the mesh.
3. **The rampart walkway has its own walkable surface collider** at wall-top height — flat, NavMesh-bakeable — so the hero stands on the upper level instead of falling through.
4. **A parapet fall-off collider** (low) runs along the outer edge of the walkway so the hero reads as protected and can't walk off the top.
5. The `DeNelle.Village.WallSegment` component (damage/repair target) re-attaches to the **new** wall sections so wall-HP/repair gameplay works against the real wall.

State the exact collider sizes/offsets chosen, per piece, in the RESULT doc.

---

## Acceptance criteria

1. After rebake, the village has a **single, coherent castle wall** (curtain + corner towers + 4 gatehouses) — visible mesh and collision are the **same** structure, no hidden-ring/overlay split.
2. Hero can **climb to the rampart upper level** via stairs/ramp (or tower) and **walk the full walkway** without falling through; the parapet stops him walking off the outer edge.
3. **Collision is on the real wall at the correct height** (per the section above): barrier from Y=0 to tier wall-top; walkway surface at wall-top; parapet fall-off on the outer edge. Verify by walking the hero into the wall (blocked) and onto the rampart (supported).
4. **Enemy pathing intact:** from every `spawn-0..3` an enemy still reaches the Heart through the gate gaps after a navmesh rebake. Gates remain open lanes; towers/walls don't block the corridor (DEF-101 8 m gate clearance still honored).
5. `WallSegment` damage/repair works against the new wall sections (HUD wall-HP, repair UI, WaveManager hooks all resolve to real segments — null-guarded if none).
6. Wall-tier hook (WO-114) preserved: wall-top height keys off `WALL_TARGET_H[tier]` so wood→stone→reinforced still raises the wall.
7. Brace-balance check passes on every `.cs` edited; village rebuilds with no missing-prefab errors; navmesh re-baked.

---

## Implementation pointers (designer/CLI's call on exact shape)

- The perimeter is built from `BuildWallPerimeter` / `BuildWallRing` / `BuildGates` (`VillageSceneBuilder.cs:560-740`) driven by `WallLayout.Segments` / `WallLayout.Gates`. Owner has stripped the old output; re-author these to emit the new single-structure wall.
- Use polyperfect wall pieces with walkable tops (`Wall_Medieval`, `Wall_Medieval_Stairs`, `Tower_Medieval_*`) — confirm names in `docs/polyperfect-asset-catalog.md`. No KayKit wall meshes.
- Put the barrier `BoxCollider` + `WallSegment` on each new wall section's root at the visible line, sized to `WALL_TARGET_H[tier]`. Add a separate thin walkway `BoxCollider` (walkable, NavMesh area) at wall-top, and a low parapet collider on the outer edge.
- Stairs: place `Wall_Medieval_Stairs` (or a ramp) at ≥2 interior points and/or inside the corner towers; ensure the stair surface is NavMesh-linked to both ground and walkway so the hero (and future breaching enemies) can traverse.
- Keep gate gaps at the existing cardinal positions; gatehouses frame the gaps without closing the lane.
- Build at **stone-tier** height; staircases at ≥2 interior points **and** tower-internal access (owner locked both).
- **Moat (folded in):** ring at ~±48/±39 (read live `WallHalfX/Z`/perimeter constants, do NOT hardcode if they've moved — see world-construction-plan coordinate flag), drawbridge decks walkable + NavMesh-linked at each cardinal gate, aligned to the gate gaps. Water/ditch per WO-104.

---

## Owner decisions (2026-05-30 — locked)

- **Wall tier:** build at **stone-tier height** now (`WALL_TARGET_H[stone]`). Keep the WO-114 tier hook so wood/reinforced remain available.
- **Rampart access:** **both** interior staircases **and** tower-internal access — maximize ways up.
- **Moat:** **folded into this build.** Designer also builds the WO-104 moat ring (~±48/±39) + **drawbridges at the four cardinal gates**, decks walkable (NavMesh) so enemies cross at the gates only. The drawbridge decks must line up with the gate gaps and not break the spawn→Heart path.

## What NOT to touch

- Do **not** remove/move `WaveSpawnPoint` (`spawn-0..3`), approach lanes, or gate world positions.
- Do **not** skip the navmesh bake (`DOTR_SKIP_NAVMESH=1` is crash-bisect only).
- Do **not** touch interior buildings, hex ground, Heart, or Keep.
- Do **not** hand-edit `Village.unity`; regenerate via the builder + rebake.

## Done checklist (CLAUDE.md §10)

- [ ] Brace-balance check passes on `VillageSceneBuilder.cs` (+ `WallLayout.cs` if edited)
- [ ] No `.unity` scene file hand-edited; rebuilt via batchmode builder, editor closed
- [ ] Single-structure wall: visuals + collision coincide (no hidden-ring split)
- [ ] Hero climbs + walks the rampart; parapet prevents fall-off
- [ ] Barrier collision Y=0→tier wall-top on the real wall; walkway surface at wall-top
- [ ] Path verified from every `spawn-0..3` to the Heart post-bake (across the drawbridges)
- [ ] WallSegment damage/repair works on new sections; references null-guarded
- [ ] Built at stone tier; stairs + tower access both present; hero reaches rampart by each
- [ ] Moat ring + 4 drawbridges present; decks walkable + aligned to gates; enemies cross only at gates
- [ ] `WORK_ORDER_136_castle_structure_ramparts.RESULT.md` written when complete
```
