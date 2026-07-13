# WORK ORDER 708 — Wall Builder: drag-to-draw wall lines (the last base-creation tool)

**Status: PARKED — POST-V1** (owner 2026-07-13: "lets figure those out after we ship v1" — pins
+ implementation wait for the V1 ship; the vision is canon now, the tool comes after).
(owner vision statement 2026-07-13, verbatim:
"this really is the very center of anyone creating their own base. the only thing we add is
wall builder and its a full build generic structure and can completely create any base").
**Lane:** BuildMode/UI. **Depends:** WO-707 (groomed palette, seed), WO-702 (founding arc — the
walls beat could later ride it).

## The vision (CANON — the player-defined-map pivot completing)

With the blank start (WO-703), the founding seed + singleton/containers taxonomy (WO-707), the
guided founding (WO-702), and a wall builder, the player can **completely create any base**:
tree + well at the center, then every structure, container, defense, and the wall PERIMETER
are theirs. Bases become as individual as the players — which is also what makes raiding
other layouts interesting (flip-a-base, WO-673).

## What exists today
- Walls tab (WO-673 split): `wall_wood` (Wooden Palisade) + `wall_stone` rows, placed ONE
  Cell/segment at a time through the standard BuildMode placement.
- The authored castle wall ring (the shell) is baked and permanent — this WO is about
  PLAYER-BUILT walls (outposts, inner keeps, chokepoints), not replacing the shell.
- Gate rows exist (`gate_stone`, Defense tab, locked).

## The tool (proposed shape — pins below)
1. **Drag-to-draw:** arm a wall type → press/drag → a ghost LINE of segments previews along
   the drag (grid-snapped, straight runs; corner turns allowed at cell boundaries) → release
   → per-segment cost is charged for as many segments as affordable; unaffordable tail is
   dropped with the standard cost feedback. Each placed segment = a normal BaseLayout record
   (persistence/replay/repair identical to any placement).
2. **Mobile parity:** drag works with the same touch input as d-pad placement (MOB-1/2
   lessons); a tap-tap (start/end) alternative for imprecise fingers.
3. **Gates in the run:** while drawing, tapping a placed ghost segment toggles it to a gate
   slot (or: gates stay a separate placement dropped onto an existing wall run — pin #3).
4. **Navmesh/carve:** wall segments carve navmesh exactly as today's single placements do
   (Blocker navSurface); enemy pathing respects player walls (that IS the strategy).

## Owner pins
1. Drag-draw (hold and sweep) vs tap-tap (mark A, mark B, fill between)? Or both?
2. Max run length per gesture (cost clamp already bounds it — hard cap needed?)
3. Gates: toggled inside a wall run, or separate placement snapped onto a run?
4. Does the founding arc (WO-702) gain a walls beat once this lands ("close your perimeter")?

## Acceptance (draft)
- [ ] A full enclosure (e.g. 12+ segments with a gate) buildable in under ~15 seconds on
      mobile; per-segment records persist + replay; repair/destroy per segment.
- [ ] Cost clamps honestly (partial runs charge only what placed); singleton NOT applied.
- [ ] Enemy pathing respects the new walls (fleet probe: wall a corridor, assert reroute).
- [ ] COMPILE_GATE_OK + DataRegression + owner felt-pass building a fort of her own design.

*Cross-refs:* owner vision 2026-07-13 · WO-673 (player-defined map) · WO-707 (taxonomy/seed) ·
WO-702 (founding arc) · memory `player-defined-map-pivot` · `no-seams-ever-port-around` (walls
are placements, never scene edits).
