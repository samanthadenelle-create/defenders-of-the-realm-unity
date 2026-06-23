# WORK ORDER 181 — Rampart Stairs + Upper-Level Siege Defenses

**Status:** READY TO IMPLEMENT
**Lane:** A (Village Scene — SERIAL, touches `VillageSceneBuilder.cs`)
**Sequence:** After PIPELINE Batch A rebake (WO-137). One pass, part of the next bake.
**Supersedes:** the remaining open piece of WO-136 (castle structure is built; this is the unfinished follow-on).

## Context
The castle is rebuilt (curtain walls, round towers, moat, drawbridges — WO-136 done). What's
missing is vertical access and the upper-level defensive layer. Per owner: add stairs that connect
the ground to the rampart upper levels, where the **secondary siege defenses** (unlockable) go.

## 🔒 LOCKED — EXACT WALL STRUCTURE (owner confirmed 2026-05-31): "the exact structure I want for walls"
**This is the definitive castle-wall design. Build every wall + corner tower to this, all four sides.**
1. **Thin, straight vertical curtain wall** — footprint tight against the town (the WALL is the inner line in the plan sketch).
2. **Corbels OUTWARD at the top** on stepped machicolation brackets → a **rampart deck ~2× the wall thickness that OVERHANGS the moat** (projects OUT over the water, NEVER inward into the town).
3. **Machicolations** (murder-hole gaps) on the outer/overhanging edge, over the moat.
4. **Crenellated parapet** (merlons + gaps) on top.
5. The wide overhanging deck = **the troop + siege-defense platform** (capacity gained over the water — does NOT shrink the moat or the town; the moat below is fully preserved).
6. **Four corner towers** do the identical corbel-overhang trick at the corners.
7. **Stairs climb from the town (interior) side** up to the walkway.
Net silhouette: **thin wall + fat overhanging machicolated fighting deck**, ringing the wide moat, with 4 gates + stone bridges.
*Reference: the cross-section + bird's-eye sketches (this session) are the canonical build reference.*

### 🔒 HARD RULE — stairs MUST attach to the top level (owner 2026-05-31)
**Every stair climbing to the rampart (second level) MUST connect flush to the walkway at its top landing** —
a continuous, walkable transition with the NavMesh carrying straight from the steps onto the deck. **No stair
may stop short of the top, float near it, end mid-air, or DEAD-END INTO A WALL FACE.** The top step must open
directly onto the WALKABLE rampart deck — which means **the deck must exist for the stair to land on** (stairs +
the overhanging rampart walkway are a PAIRED build; build the deck, then land every stair flush onto it). A stair
that climbs to a bare wall with nothing walkable on top = FAIL. This is a non-negotiable acceptance criterion
(stairs have regressed repeatedly): verify the hero can walk ground → steps → onto the rampart without a gap at EVERY stair instance.

### Larger defenses unlock as you upgrade (owner idea 2026-05-31)
The overhanging deck has room for **bigger** engines than a thin wall could hold — so the rampart siege slots are
**tiered by seat upgrade:** small (ballista) early → catapult → trebuchet/heavy as the seat tier climbs. Each unlock
is visibly **larger** on the rampart — upgrades give a dramatic, visible reward (the walls sprout progressively
bigger siege engines). Ties the seat-tier ladder + Commerce upgrades (DESIGN_CORE_LOOP). Size the slot footprints
to accommodate the largest tier; gate each by minSeatTier.

### ANTI-AIR role (owner 2026-05-31): the rampart is how you hit flyers
Elevation + reach means the rampart siege engines are the **anti-air layer** — they can engage the **dragon and
flying enemies** that ground defenders/towers can't reach (the old "dragon unhittable" problem, WO-125). Ballista
= classic anti-air/anti-dragon; bigger engines unlock more reach/punch vs tougher flyers. This gives the player a
real positional reason to climb the stairs and invest in the upper level. Consider tagging siege types: some
anti-air (ballista), some anti-ground (catapult/trebuchet over the moat). The elevated deck does triple duty:
murder-holes over the moat · troop/siege capacity · the only reliable answer to air attacks.

## RAMPART AESTHETIC — DECIDED (owner ref image 2026-05-31): MACHICOLATED / CORBELLED
The curtain walls run **straight and vertical**; the rampart **corbels OUTWARD at the top** so the walkway is
**~2× the wall thickness and OVERHANGS the wall face** — supported on stepped corbel brackets (machicolations),
topped with crenellations (merlons + gaps). NOT a uniformly thick wall — a thin wall + a cantilevered fighting
platform. Implementation: keep the wall mesh thin/straight; build the rampart walkway WIDER than the wall and
offset it OUT past the wall face; add the corbel/machicolation brackets underneath the overhang + crenellated
parapet on top. The overhang reads as defensive (murder-holes over the moat) and gives the wide rampart its reason.
Pairs with the wide moat (WO-179): defenders overhang attackers in the water.

**The overhang extends OVER the moat = more defensive real estate up top (owner 2026-05-31).** Because the
rampart cantilevers out over the water (not into the town), the wall-walk gains depth for free — that extra
projecting platform is where **troops + siege defenses live.** So the overhang directly increases capacity:
**more squad slots + more siege-defense slots on the ramparts** (ties the food-capped garrison, DESIGN_CORE_LOOP
§4a), each positioned right over the attackers crossing the moat/bridges. Size the siege-slot count to the
deeper (overhanging) walkway, not the thin wall.

## Intent
Give the player a reason to go up: walkable rampart tops reached by stairs, with placement slots
for unlockable upper-tier siege defenses (the high-ground answer to siege threats like the trebuchet
in WO-110).

## Scope / Acceptance
1. **Stairs** — stair geometry connecting ground level to each rampart walkway / tower upper level.
   - Hero can walk up and stand on the parapet walkway; collision + navmesh continuous (off-mesh links where needed).
   - No clipping through walls/towers; stairs read as part of the castle, not bolted on.
2. **Rampart walkway — WIDE, full-perimeter (owner 2026-05-31).** Widen the rampart so the player can
   walk the **entire perimeter** around the whole structure (continuous loop, no dead-ends), and so it's
   wide enough to **mount defenses on top without crowding the walkway**. The point: defenses live UP HERE,
   keeping the GROUND clear for the city (DESIGN_ELARION_CITY.md ground-vs-wall-top split). Walkable
   end-to-end around the perimeter and into each tower top; navmesh continuous around the full loop.
3. **Siege defense placement slots** — defined anchor points on the upper level where upper-tier
   defenses mount. Slots are **locked by default** (unlockable items — gated by progression/currency,
   wire to existing build/catalog system; do not greenfield a new one).
   - Empty slot shows a clear "locked / buildable" affordance.
   - Placing a defense in an unlocked slot spawns it facing outward over the parapet.
4. **One bake** lands this with the scene (batchmode, editor closed).

## Do NOT touch
- Ground-level building roster, wave loop, economy code.
- The drawbridge/gate work from Batch A (must already be landed first).

## Open sub-question for owner (can answer at build time)
- Which specific upper-tier defenses are the unlockables? (e.g. ballista, cannon, arcane spire.)
  Default: stub 2–3 slots; exact roster can follow in a content WO. Flagging so CLI doesn't guess.

## Gate
Brace check on any `.cs`; green build; commit `feat: implement WO-181 — rampart stairs + upper siege defenses`; bake; screenshot for UI validation.
