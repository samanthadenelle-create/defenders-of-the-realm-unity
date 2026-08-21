<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WO-369: Arena Monument — Iconic Endgame Economic Hub Landmark

**Status:** DONE — audit-verified as shipped (2026-08-21 backlog audit).
**Estimated Effort:** P0 (1–1.5 days — asset placement + dressing)  
**Priority:** HIGH (endgame hook, visual landmark)  
**Lane:** World/Environment

---

## Overview

Replace the pink flag placeholder with an **epic combat arena monument** — a grand landmark that serves as the holy grail of economic endgame.

**Current:** Pink flag at spawn point (placeholder)  
**Target:** Iconic stone monument surrounded by battle trophies, torches, and standing stones. Moved away from spawn. Visible from anywhere in the village.

---

## Acceptance Criteria

- [ ] Pink flag removed from spawn area
- [ ] Arena monument placed at a prominent location (away from spawn points)
- [ ] Central statue/monument installed (`Statue_Knight` or equivalent)
- [ ] Surrounding props placed (torches, siege trophies, standing stones)
- [ ] Feels iconic and grand (endgame hook visual)
- [ ] Matches village scale and medieval fantasy aesthetic
- [ ] Visible from most of the village (landmark prominence)
- [ ] Collision-free (players can walk around it)
- [ ] No performance hit (all Polyperfect low-poly assets)

---

## Design: Monument + Battle Trophy Enclosure

### Central Monument
- **Centerpiece:** `Statue_Knight` (Fantasy_M) — standing warrior statue
  - Pose: Victory stance, sword raised, authoritative
  - Base: Stone foundation ring (use `Stone_Big` ×4)
  - Scale: 1.5–2x normal size to be visible across village
  
### Trophy Display (Around Monument)
Arrange siege weapons as "conquered trophies" in a semicircle:
- **`Catapult` (Medieval_M)** ×1 — off to right
- **`Ballista` (Medieval_M)** ×1 — off to left
- **`Cannonballs` scatter** — on ground near weapons
- **`Stakes` (Medieval_M)** ×4 — marking arena perimeter

### Atmosphere
- **`Torche_Wall` (Fantasy_M)** ×6–8 — ring of torches around monument
- **`Rock_Pillar` (Environment)** ×4 — standing stones at cardinal points
- **`Flag_Medieval_Big`** ×1 — flying banner above (guild colors or victory pennant)

### Ground Treatment
- **Arena floor:** `Floor_Stone_3x3m_A` ×4–6 — stone paving in circular pattern
- **Approach path:** `Stone_Brick` (Medieval_M) — leading from village center to arena

### Dressing Props (Minimal, High Impact)
- **`Skull_Human` ×2** (Fantasy_M) — on pedestals, symbolic victory markers
- **`Goblet`** ×2 (Fantasy_M) — trophy cups
- **`Chain` or `Rope`** (Medieval_M) — around trophy weapons (captured/displayed)

---

## Asset Manifest

**From Polyperfect catalog:**

| Asset | Count | Purpose | Notes |
|-------|-------|---------|-------|
| `Statue_Knight` | 1 | Central monument | Hero of Elarion theme |
| `Torche_Wall` | 6–8 | Lighting ring | Creates circular arena feel |
| `Catapult` | 1 | Trophy | Siege weapon display |
| `Ballista` | 1 | Trophy | Different silhouette than catapult |
| `Cannonballs` | 1 set | Ammo scatter | On ground near weapons |
| `Stakes` | 4 | Perimeter marker | Arena boundary |
| `Rock_Pillar` | 4 | Standing stones | Cardinal points |
| `Stone_Big` | 4 | Monument base | Statue foundation |
| `Floor_Stone_3x3m_A` | 6 | Arena paving | Circular pattern |
| `Stone_Brick` | 8 | Approach path | Leading walkway |
| `Flag_Medieval_Big` | 1 | Banner | Flying above statue |
| `Skull_Human` | 2 | Victory marker | On pedestals |
| `Goblet` | 2 | Trophy cups | Display props |

**Path reference:** All assets from `Assets/polyperfect/Low Poly Ultimate Pack/_M/Meshes_M/<Category>_M/`

---

## Location Placement

### Current Pink Flag Location
- Find and remove the pink flag from spawn area
- **Note position for reference**

### Proposed New Location
- **Distance from spawn:** 30–40m away (visible but separate)
- **Height:** Slightly elevated (1–2m) for prominence
- **Sight lines:** Central in village, visible from most approaches
- **Accessibility:** Easily walkable, no clipping, clear approach path
- **Space required:** ~30×30m cleared area for monument + surrounding props

### Coordinate Placement
- Monument center: TBD (coordinate with VillageSceneBuilder)
- Statue: (0, 0, 0) relative to monument center
- Surrounding props: Radial ring at 5–8m from center

---

## Implementation Steps

1. **Remove placeholder:**
   - Delete pink flag GameObject from Village scene
   - Remove any arena-flag prefab reference

2. **Build monument base:**
   - Create empty GameObject: `ArenaMonument` (parent)
   - Place `Statue_Knight` at center, elevated on `Stone_Big` ring

3. **Add trophy display:**
   - Position `Catapult` and `Ballista` on left/right flanks
   - Scatter `Cannonballs` near weapons
   - Place `Stakes` in 4 cardinal points

4. **Add atmosphere:**
   - Ring of `Torche_Wall` ×6–8 around monument
   - `Rock_Pillar` ×4 at diagonal cardinal points
   - `Flag_Medieval_Big` above statue (parented, floating slightly)

5. **Ground treatment:**
   - Circular `Floor_Stone_3x3m_A` paving under monument
   - `Stone_Brick` path leading from village center

6. **Victory markers:**
   - Place `Skull_Human` ×2 on small pedestals (use `Stone_Big` as base)
   - Place `Goblet` ×2 on display stands

7. **Test & iterate:**
   - Walk around monument, check sightlines
   - Verify no clipping into terrain
   - Confirm visible from village entrances
   - Performance check (no LOD issues)

---

## Visual Reference

```
                    🚩 Flag_Medieval_Big
                         |
                  Torche  |  Torche
                    \     |     /
       Rock_Pillar   \  Statue_Knight  /   Rock_Pillar
                      \  (1.5x scale) /
              Catapult  \_________/  Ballista
                        Stone_Big ring
                           |
                      Floor_Stone
                           |
                    Stone_Brick path →→ to Village
```

---

## Lore Tie-In

**Arena as Economic Endgame:**
- Monument represents "Victory over All Challenges"
- Trophy weapons show conquered enemies
- Torches represent eternal vigilance
- Skull markers symbolize enemies vanquished
- Standing stones = ancient sacred ground
- **Economic link:** Players come here to collect arena rewards, battle for glory, trade victory spoils

---

## Testing Checklist

- [ ] Pink flag removed
- [ ] Monument placed at prominent location (away from spawn)
- [ ] Statue visible from village entrances
- [ ] Torches light properly (no shadows clipping through)
- [ ] Siege trophies positioned symmetrically
- [ ] Ground paving aligns with terrain
- [ ] No collision issues (walkable around monument)
- [ ] Stones/props don't float (all grounded)
- [ ] Flag animates smoothly if wind applied
- [ ] Performance: No FPS drop near monument
- [ ] Works in WebGL build
- [ ] Monument feels iconic and grand (subjective check)

---

## Performance Notes

**All Polyperfect assets:**
- Ultra-low poly counts (Statue_Knight ~2k tris)
- Single shared atlas texture (excellent batching)
- Build size: ~200 KB for all monument assets combined
- **No VFX or particle systems** (pure geometry)

---

## What NOT to Touch

- Village terrain (monument sits on existing ground)
- Spawn logic (arena is just visual landmark)
- Economic systems (WO-361 handles rewards)
- Combat mechanics (arena visual only)
- Other buildings (monument is isolated feature)

---

## Future Enhancements

- [ ] Animated statue (sword raising/lowering, victory animation)
- [ ] Dynamic flag waving (shader-based cloth sim)
- [ ] Arena scoring board (carved stone tablet listing top players)
- [ ] Fireworks/VFX on victory triggers
- [ ] Arena entrance gate (framing the approach)
- [ ] Spectator stands around perimeter
- [ ] Seasonal decoration (banners, wreaths)

---

## Acceptance Sign-Off

- [ ] Pink flag replaced with grand monument
- [ ] Monument feels iconic and endgame-worthy
- [ ] Positioned prominently, away from spawn
- [ ] All props visible and unclipped
- [ ] No performance issues
- [ ] Ready for economic systems (WO-361) to integrate reward triggers
- [ ] Ready for players to gather/celebrate here

---

## Dependency Notes

- **Unblocks:** WO-361 (Wave Rewards) — arena location now exists for reward dispensing
- **Depends on:** VillageSceneBuilder coordinate system (to place accurately)
- **Parallel:** All other location work (no conflicts)

---

## Notes for CLI

**BraceBalance:** If creating C# script for arena positioning, ensure all `{` matched.  
**VillageSceneBuilder:** Coordinate monument placement with existing structure layout.  
**No hand-editing Village.unity** — rebuild via script using provided coordinates.

> **AUDIT 2026-08-21 (agent fleet, read-only):** FIXED. Evidence: `ArenaHeraldSpawner.cs:231-239,53` — arena monument replaces placeholder. Status was flipped from READY by the AUDIT, not by an implementation pass. The body below is left intact; if this call is wrong, the evidence cited here is what to challenge.
