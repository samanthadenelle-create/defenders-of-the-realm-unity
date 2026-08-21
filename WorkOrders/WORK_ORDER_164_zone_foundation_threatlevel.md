**Status:** CLOSED — owner range sweep 2026-08-21 (WO 0-800): completed or immaterial.

<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK ORDER 164 — Zone Foundation: depth read + ThreatLevel + zone records (the keystone)

**Status: READY TO IMPLEMENT**
**Priority:** HIGH (keystone) — WO-155 (region enemies), WO-159 (settlements), WO-160 (tribes) all
**read `ThreatLevel`/depth, which does not exist yet.** Build this first or those WOs hard-code placeholders.
**Date:** 2026-05-30
**Lane:** Core data + world. Code only; no `VillageSceneBuilder`; no bake.
**Source:** `ZONE_STREAMING_ARCHITECTURE.md` Phase 1 (zone records + graph) + the two-axis difficulty model.

---

## What exists vs the gap
- **Exists:** `ZoneManager.GetZone(Vector3)` / `ZoneAt` / `DangerTierAt`, `RegionId`/`RegionZone` (danger
  tier 1–4), `GameState.Regions` (`RegionProgress { Discovered, Cleared }`).
- **Missing (this WO):** the **depth** read (how deep into a region a position is) + the combined
  **`ThreatLevel`** + a per-zone **state record** + the **neighbor/City-Horde graph**. These are the
  shared inputs the world features need.

## Build

### 1. Depth + ThreatLevel on ZoneManager (Core, extends the existing classifier)
```csharp
// ZoneManager additions (Core/World) — no new system, extend the existing static
static float Depth(Vector3 worldPos);        // 0 at the region's safe edge → 1 at its core/center
static int   ThreatLevel(Vector3 worldPos);  // combine(DangerTierAt(pos), Depth(pos)) → an enemy-level number
```
- `Depth` = normalized distance from the region's safe boundary toward its core (use the region anchor/
  center the OuterWorldBuilder already places, or a per-region edge def). Clamp 0..1.
- `ThreatLevel` = a smooth combine, e.g. `baseForTier(dangerTier) + depthBand(depth)` → an integer level
  enemies/raids/nodes scale against. Tune the curve in a `ProgressionConstants`/SO, not hard-coded.
- Pure Core, no Village ref — same module as `GetZone`. This is the single shared difficulty read.

### 2. ZoneState record + neighbor/City-Horde graph (Core data, persisted)
Extend `RegionProgress` (or add a parallel `ZoneState`) per `RegionId`:
```
ZoneState { RegionId id; bool discovered, cleared; RegionId[] neighbors;
            NodeType destination;  // City | Horde | Neutral (zone doc)
            /* later: node/tribe/settlement sub-records hang here */ }
enum NodeType { City, Horde, Neutral }
```
- Author the **neighbor graph** (Ashwood↔Village↔Goldfields etc.) + tag each zone's `destination` (City
  for low-danger, Horde for high — largely derivable from `DangerTier`, manual overrides allowed).
- Persist via the existing `GameState`/`SaveSchema`/`SaveMigrator` round-trip (extend `RegionProgress`;
  bump schema per convention — coordinate w/ SaveMigrator owner).

## Why first
This is the **shared spine**: enemy spawning (WO-155), node settlements (WO-159), and tribes (WO-160)
each call `ThreatLevel(pos)` to scale; the graph feeds streaming (later) + the City/Horde rhythm. Build
it once here so those WOs consume a real API instead of stubbing it. **No streaming machinery yet** (zone
doc Phase 2+) — just the data + reads.

## Acceptance criteria
1. `ZoneManager.Depth(pos)` returns 0→1 edge→core; `ThreatLevel(pos)` returns a tunable level from danger
   tier × depth; both pure Core, no Village ref; curve in SO/constants not hard-coded.
2. `ZoneState` per region (discovered/cleared/neighbors/destination) persisted + round-tripped in save.
3. Neighbor graph + City/Horde tagging authored for the 4 regions + Village.
4. Reds: WO-155/159/160 can call `ThreatLevel` instead of placeholders (no behavior change required here —
   just the API exists + is correct).
5. Brace balance; Village→Core only; no bake; no streaming/unload yet.

## Done checklist (CLAUDE.md §10)
- [ ] Depth + ThreatLevel on ZoneManager, tunable, Core-only
- [ ] ZoneState + neighbor/City-Horde graph; persisted + save round-trip
- [ ] Consumable by WO-155/159/160; brace balance; no bake
- [ ] `WORK_ORDER_164_zone_foundation_threatlevel.RESULT.md` when complete

> **OWNER RULING 2026-08-21 (verbal, this session):** CLOSED by an explicit owner sweep of the WHOLE 0-800 RANGE: "I've eyeballed them many times, and all of them are already completed. or immaterial." This is a RANGE close on the owner's direct review, not a per-ticket verdict.
