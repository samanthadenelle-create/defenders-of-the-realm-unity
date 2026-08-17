<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK ORDER 188 — Replace Gates + Drawbridges with Solid Bridges (DESIGN CHANGE)

**Status:** READY TO IMPLEMENT
**Lane:** A (Village Scene — SERIAL, `VillageSceneBuilder.cs`)
**Source:** owner playtest 2026-05-31
**Priority:** P1 — supersedes the gate/drawbridge cluster

## Owner decision
The portcullis gates + drawbridges are broken (impassable, drawbridge geometry half upside-down,
black ropes misplaced). **Owner call: stop fixing them — remove the gates and drawbridges entirely
and replace each crossing with a SOLID STONE ENTRANCE/BRIDGE over the moat/water.** No portcullis,
no wooden drawbridge, no chains/ropes, no moving parts, always passable.

**Owner refinement 2026-05-31: STONE entrances, not wood.** Use the catalog stone bridge —
`Bridge_Medieval_Stone` (polyperfect, see `docs/polyperfect-asset-catalog.md` §1) — so each crossing
reads as a permanent stone causeway matching the castle, not a timber drawbridge.

**Owner clarification 2026-05-31: ALL 4 gates, STONE, SPANNING THE MOAT.** A stone bridge at each of the
four gates, positioned flat to **span the moat/water** as the exit crossing (laid over the water, not inside
the gate). **Remove the misplaced wooden drawbridge** entirely (the reuse idea is dropped — stone at all 4 for
consistency). The manifest already has `Bridge_Medieval_Stone` at all 4 gates — just verify each sits over the
moat span, flush at both ends, passable.

## Supersedes / re-scopes
- **WO-158** (gates impassable) → now: openings are permanently open with a solid bridge; just ensure passable + navmesh-continuous.
- **WO-167** (gatehouse pillar clip) → likely moot if the gatehouse arch is removed; if a decorative arch stays over the bridge, keep the clip fix, else drop.
- **WO-168** (navmesh seals openings) → still required (continuity across the bridge), now trivial (no portcullis to block).
- Drawbridge mechanics / chains / ropes → **deleted**, not fixed.

## Acceptance — EXPLICIT + TESTABLE (owner 2026-05-31; these MUST each pass)
- [ ] **A1 — ALL 4 GATE OPENINGS (N, E, S, W) ARE CLEAR & PASSABLE.** NOTHING in any gate opening — **no stone
      block, no portcullis, no wall segment, no bridge, no arch, no debris.** The hero walks **straight through
      every one of the 4 gates** out and back, AND enemies path through. Test each of N/E/S/W individually.
- [ ] **A2 — BRIDGES EXIST ONLY OUTSIDE, OVER THE MOAT. ZERO INNER BRIDGES.** No bridge or stone-arch span
      anywhere inside the village, and none sitting in or blocking a gate opening. **Remove any inner bridge.**
      The only bridges in the scene are the 4 stone bridges crossing the moat OUTSIDE the wall.
- [ ] **A3** — those 4 outer bridges are solid **stone** (`Bridge_Medieval_Stone`), span the moat bank-to-bank,
      flush (not floating/sunk), hero + enemies cross; navmesh continuous across each.
- [ ] **A4** — no wooden drawbridge, no chains/ropes, no portcullis anywhere; the `Drawbridge_Medieval` prefab
      and `DrawbridgeController` are not instantiated. (Verify `DrawbridgeController.cs` isn't wired.)

**Verification is per-gate and per-bridge — a "mostly works" does NOT pass. Screenshot each of the 4 gates clear.**

## Design note (for later, not this WO)
With permanent openings, the bridges become the **defended chokepoints** (towers cover them) instead
of closable gates — cleaner TD lanes. Defense routing/tuning is a later pass.

## Do NOT touch
- Wall tier/material logic, terrain (WO-173), stairs (WO-183).

## Gate
Brace check; green build; commit `feat: implement WO-188 — solid bridges replace gates/drawbridges`; folds into the Batch A village bake. Screenshot for UI validation.
