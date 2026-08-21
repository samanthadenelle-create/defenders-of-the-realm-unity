**Status:** CLOSED — owner range sweep 2026-08-21 (WO 0-800): completed or immaterial.

# WORK ORDER 449 — World layout: big terrain, distance-GATED challenge ring, content scatter, wayfinding

**Status: DESIGN SPEC (do-it-right pass).** Owner-directed 2026-06-17 ("B — always do it right not fast").
Supersedes/absorbs the WO-448 seam fix into a proper world-layout pass. Lane: World/Environment (architect
lane — ExteriorTerrainBuilder, CampSystem, scatter, CastleHubBuilder landing, NavMesh). Big multi-system pass;
**requires terrain rebuild + camp reposition + NavMesh rebake** (CLI, editor closed, §3).

## The keystone (owner): DISTANCE is the progression gate
"Before they can go find camps they need to stay close enough to build up their towers and defenses to hold
longer." → The camps (challenges) are gated NOT by a level/paywall but by **geography**: early game keeps the
player near the hub (build towers, hold waves, grow stronger); only once built up have they earned the range
to venture out to the camps. **The world's size is a game mechanic.** Early tower-defense = the core fun; the
world opens as it's earned. (Fun-first, spatially expressed.)

## Design
1. **Expand the terrain** — current **300×300** (`ExteriorTerrainBuilder.cs:102 TerrainSizeXZ = 300`) → **~450-500**
   ("bigger the better", owner). Gives real room for: hub → transition → content runway → challenge ring →
   future expansion/outpost zones (the multi-base vision) → beyond. Build once for the full vision.
2. **Real-distance landing** out each gate — far from the hub overlap (fixes the WO-448 z-fight by NOT landing
   on it). A genuine "travelled out" transition, not a seam-hop. (`CastleHubBuilder` target positions per gate;
   keep auto-cross, fire at the gate mouth — radius ~5, not 18.)
3. **Push the challenge ring FAR** — camps currently at ±95 (`CampSystem.cs:95-101`: Goldfields E (95,10),
   Stoneback W (-95,-10), Mirewood S (12,-95), Ashwood N (-12,95)). Push them out (scaled with the bigger
   terrain) so the distance gates them behind defense build-up. Keep on valid NavMesh, clear of the edge.
4. **Content scatter ("random patterns") — fills the big world so it's ALIVE.** Trees, stumps, rocks, **little
   animals** (we have `polyperfect/Low Poly Ultimate Pack` animals + `Quaternius` nature). A randomized scatter
   pass across the world + the runway corridors (between landing and camps). Avoid overlapping nodes/camps/paths.
   **CREATIVE/ARCHITECTURE defines the patterns** — what species/props, density, clustering, biome variation per
   region. (Extend existing spawners: mine nodes, `RareCrystalSpawner`, rocks.)
5. **Wayfinding — "not so far they can't find their way back."** The **Heart-Tree is the landmark**: tall,
   central (0,0,0), glowing — always visible, so the player heads toward it to return home. Ensure it's
   sightline-visible from across the map (height/glow/no occlusion). Plus the existing `CompassHud` points to
   the hub. Optionally a road/path from the hub outward. **Big world, never lost.**

## Emergency recall — the safety valve for the distance gate (owner 2026-06-17)
Pushing camps FAR creates a risk: a player 200m out can't defend a town that's being breached → dread of
venturing → kills the exploration the big world is for. **Fix: a once-a-day INSTANT recall to town**, available
ONLY when the town is **under active attack AND about to breach** (Heart/wall HP below a breach threshold).
- **Once per day** (cooldown) — an emergency tool, NOT free fast-travel (free travel would erase the geography
  gate). Keeps the distance meaningful.
- **Contextual trigger** — lights up only at real danger; creates a tense "abandon the raid and port home?" beat.
- **Instant** (vs normal travel's natural transition — the emergency justifies it; you don't stroll home mid-breach).
- **UX:** a clear "Recall to Defend" prompt only while the breach condition holds.
- **§5 monetization rail:** if a 2nd recall is ever sold (rewarded-video/purchase), it stays **convenience** — the
  **free daily recall must always be enough that a player never pays to avoid LOSING their town.** "Pay-or-lose-
  your-base" is the banned dark pattern. The recall earns trust because it's free when it matters.
- Files (later): tie to `WaveManager`/`HeartController` (the breach state) + the seam/recall in `SceneTransitionTrigger`.

## Acceptance
- [ ] Terrain expanded; world reads as BIG and ALIVE (scatter), not empty.
- [ ] Walking out each gate = a real-distance transition into open world; NO z-fighting floor at/near landing.
- [ ] Camps are far enough that a fresh player must build up hub defenses + hold waves BEFORE they can reach/clear
      them (distance gates progression — verify the early game keeps you near the hub).
- [ ] From anywhere on the map the player can locate home (Heart-Tree sightline + CompassHud) and path back.
- [ ] All landings + camps on valid NavMesh, clear of edges/obstacles; NavMesh rebaked.
- [ ] §12: seam emits `[Flow:Seam]`; consider a `[Flow:World]` step on region entry.
- [ ] Compile gate green; rebuilt via builders (no hand-edited `.unity`); bake with editor closed.

## Creative brief (for creative/architecture — the "random patterns")
Define the scatter: which props/animals (from polyperfect + Quaternius), per-region density + clustering, biome
feel per cardinal (the camps are themed — Goldfields/Stoneback/Mirewood/Ashwood), and the path/landmark dressing
toward the Heart-Tree. Keep it performant (no per-frame alloc; instanced/batched where possible — mobile-first).

## What NOT to touch / notes
- Keep auto-cross (owner intent) — fix landing + radius, not the confirm.
- §9 architect lane (VillageSceneBuilder/terrain) = one agent/branch at a time (serialization bottleneck).
- §3: rebuild via builders, never hand-edit scenes; bake editor-closed. §0: CLI on Windows path.
- Mobile-first: a bigger world + scatter must stay within the WebGL perf/payload budget (LOD, instancing,
  texture caps per WO-408). Bigger world ≠ heavier build if scatter is instanced + culled.

*Cross-ref:* WO-448 (seam fix, absorbed), `ExteriorTerrainBuilder.cs`, `CampSystem.cs`, `CastleHubBuilder.cs`,
`CompassHud`, the Heart-Tree (`HeartController`/world-tree), `docs/BRAND_AND_PLATFORM_CANON.md` (the multi-base
expansion this world must hold), polyperfect + Quaternius asset packs.

> **OWNER RULING 2026-08-21 (verbal, this session):** CLOSED by an explicit owner sweep of the WHOLE 0-800 RANGE: "I've eyeballed them many times, and all of them are already completed. or immaterial." This is a RANGE close on the owner's direct review, not a per-ticket verdict.
