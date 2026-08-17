<!-- era-sweep-2026-08-17 -->
> ### ⛔ ERA SWEEP 2026-08-17 — CLOSED as OBSOLETE (deleted system)
> **Dead thing:** OuterWorld.unity. **Git first-add:** 2026-06-22.
> **Evidence:** `Assets/Scenes/OuterWorld.unity` is absent from disk and from `git ls-files`; the WO's own lane line is "OuterWorld files only, not Village.unity" and DEF-61 is "create a Unity Terrain object in OuterWorld scene".
> Only the `**Status:**` line was rewritten. The body below is UNTOUCHED — CLAUDE.md §15, *"frozen, never rewrite"*.
> **TO REVIVE:** nothing was deleted and not one line of the body below was changed. If this work is still wanted, re-date the WO (add a `**Minted:** <today>` line), re-point it at the live scene/system, and set `**Status:** READY TO IMPLEMENT`.

# WORK ORDER 245 — World Terrain Foundation + Nature Population + POIs
**Status:** CLOSED — OBSOLETE: OuterWorld.unity no longer exists (era sweep 2026-08-17)
**WO:** 245 | **Lane:** WORLD (parallel safe — OuterWorld files only, not Village.unity)
**Closes:** DEF-61, DEF-62, DEF-63

---
## DEF-61 — Terrain foundation + fog

Create a Unity Terrain object in OuterWorld scene. Village sits at centre (0,0,0).

```
Size:          800 × 800 Unity units
Height map:    Gentle hills in outer rings, flat near village (inner 200m radius)
Ground tex:    Grass (inner), patchy dirt/rock (mid), dark/dead (outer edges)
Fog:           Exponential squared. Start 60m, end 140m from player. Dark grey/purple.
               Matches Hollow Ones / Withering tone — fog = danger, not cosiness
```

**Files:** `Assets/Editor/ExteriorTerrainBuilder.cs` (already exists — extend `BuildTerrain()`)

**Acceptance criteria:**
- [ ] Unity Terrain exists in OuterWorld scene, 800×800
- [ ] Village centre sits on flat ground at Y=0
- [ ] Exponential fog conceals terrain edge at ≥80m from village boundary
- [ ] Frame rate ≥30fps on mobile WebGL with fog active
- [ ] No pink/missing materials

---
## DEF-62 — Nature population

Use Unity Terrain Tree and Detail systems. No manually placed GameObjects in outer rings.

```
Inner ring (0–120m):   No trees — village-visible area kept clear
Mid ring (120–300m):   Scattered forest. Mix 3 tree types from polyperfect Nature_M.
                       Density: 40 trees per 100m². Rock outcrops every ~30m.
Outer ring (300m+):    Dense. Darker foliage. More rock. Less grass detail.
```

**Acceptance criteria:**
- [ ] Trees, foliage, rocks placed via Terrain Tree/Detail (not scene GameObjects)
- [ ] No pink/missing materials on any nature asset in WebGL
- [ ] Frame rate ≥30fps with full nature population active on mobile WebGL
- [ ] BLOCKED — implement after DEF-61 terrain exists

---
## DEF-63 — Points of interest

Place 3 named POI locations in mid and far rings. Visual/exploration only — no gameplay logic yet.

| POI | Location | Asset |
|---|---|---|
| The Ashen Shrine | ~180m NE | KayKit shrine or stone altar |
| Abandoned Watchtower | ~220m NW | KayKit tower ruin |
| The Sunken Camp | ~260m SE | KayKit tent/barrel/campfire cluster |

**Acceptance criteria:**
- [ ] ≥3 distinct named POI locations exist in mid/far rings
- [ ] Each visible from ≥30m without fog occlusion
- [ ] Each has a BoxCollider (for future interaction)
- [ ] No gameplay logic attached — visual/exploration only
- [ ] BLOCKED — implement after DEF-61 and DEF-62

---
## What NOT to touch
- `Village.unity` — do not hand-edit
- `VillageSceneBuilder.cs` — this WO is OuterWorld only
