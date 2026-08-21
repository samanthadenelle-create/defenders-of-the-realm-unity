# WORK ORDER 27 — Enemy Spawn World + Spawn→March→Attack Loop (EXTERIOR)

**Status:** CLOSED — owner range sweep 2026-08-21 (WO 0-800): completed or immaterial.
**Applying this requires re-running the village/exterior scene builder**
(`Defenders > Week 3 > Build Village Scene`, i.e.
`-executeMethod DeNelle.Editor.VillageSceneBuilder.BuildVillage`, which chains
`ExteriorTerrainBuilder.BuildExterior` and the NavMesh bake). That step is a
**hard rule: owner-gated** and is NOT performed by this work order. This document
specifies parameter/value/geometry changes only; do not edit code or re-bake the
scene on the basis of this file alone.

**Date:** 2026-05-25
**Author:** game-systems architecture pass
**Problem (owner, P0):** *"There was supposed to be a LARGER MAP where the enemies
spawn from, and none of that is there. Without it we can't have spawned enemies
attacking the town — hence no playable loop."*

The tower-defense core loop — **enemies spawn out in the world → march toward the
town → attack the gates/Heart → player + pets + abilities defend → gate breach
hands off to ATB** — does not function because the exterior spawn world is
missing/insufficient. This WO owns the **EXTERIOR / spawn world + the loop**.

### Coordination with WORK_ORDER_26 (do not conflict)
WO-26 owns the **village INTERIOR** (enlarging the walled town, footprint
colliders, street width, building re-spacing). It changes the wall ring to
`WallHalfX=42`, `WallHalfZ=33`, `SouthBowDepth=6` in BOTH `WallLayout.cs` and the
mirrored consts in `VillageSceneBuilder.cs`. **This WO assumes WO-26's enlarged
ring is the baseline** and expresses every exterior distance **relative to the
gate position / `WallHalf*`**, never as an absolute that would desync. The two
WOs touch different methods:
- WO-26: `WallLayout` consts, `AddBuildingFootprintCollider`, `Buildings[]`,
  `BuildCityDressing`, `BuildRoads/LayRoadPair`, `BuildPlaza`.
- WO-27 (this): `ExteriorTerrainBuilder` (navmesh-bakeable approach corridors +
  spawn aprons), `VillageSceneBuilder.BuildApproaches` (spawn-point distance),
  `BakeVillageNavMesh` (include the corridors), and the WaveManager breach ring.

The one shared file region is `BuildApproaches` and the NavMesh bake roots — both
in `VillageSceneBuilder.cs`. WO-27's changes there are additive to WO-26 and use
`WallHalf*`-relative math, so applying WO-26 first then WO-27 is order-independent.

---

## 1. Diagnosis — WHY the loop is dead today

The wave system code is correct and runs (the runtime log
`[WaveManager] Loop armed — wave 1, countdown 300.0s` proves it). The breakage is
purely **world geometry + NavMesh coverage**: there is no navigable space outside
the gates for enemies to spawn into and march across.

### How spawning + approach is *supposed* to work
1. `WaveManager.StartWave` reads `waves.json` (wave 1 = 8 `hollow-walker` at
   `spawn-0`, the NORTH spawn point) and calls `SpawnBatch → SpawnOne` per enemy.
2. `SpawnOne` instantiates an `Enemy` at the `WaveSpawnPoint`'s world position,
   **snapping to the nearest NavMesh sample within 8 m**
   (`NavMesh.SamplePosition(pos, out hit, 8f, AllAreas)`); if no NavMesh is within
   8 m the enemy is left at the raw position and `NavMeshAgent.isOnNavMesh` stays
   false.
3. Each `Enemy` (has `[RequireComponent(NavMeshAgent)]`) runs `DriveNav` →
   `_agent.SetDestination(_heart.position)` every frame. **If the agent is not on
   a baked NavMesh it logs once and holds position — it never moves.**
4. Enemy reaches a gate's force-field blocker collider, `ProbeForStructure`
   (`SphereCast` 1.1 m) finds the `Gate` (which implements `IDamageableStructure`),
   stops, and deals `contactDamage` every `attackInterval`. Gate HP falls; below
   25% `Gate.ApplyForceFieldState` drops the blocker collider and the enemy paths
   through the opening toward the Heart.
5. `WaveManager.TickActiveWave` watches each live enemy; when one crosses the
   `_innerRingRadius = 9 m` ring around the Heart (or fires `ReachedHeart`), it
   builds `BattleParams { Wave, BreachedIds, ParticipatingPetIds }` and calls
   `SceneRouter.GoBattle` → ATBBattle, pausing the loop.

That chain only works if there is a **baked NavMesh from the spawn point, through
the gate, to the Heart**. There is not enough of one.

### Cause A — Spawn points are only ~10–12 m beyond the gate (no "world out there")
`VillageSceneBuilder.BuildApproaches` places the `WaveSpawnPoint` at
`spawnCentre = gatePos + outward * (7f * step)`, where `step = HexDepth (1.5)` for
N/S gates and `HexWidth (1.732)` for E/W gates. So:
- North spawn sits **~10.5 m** beyond the north gate.
- East/West spawn sits **~12.1 m** beyond their gates.

The paved approach is only **5 hexes (~7.5 m)** of road, 2 tiles wide. There is no
"larger map" — the enemy materializes almost on the doorstep, takes a couple of
steps, and is at the gate. There is no spawn ring, no march distance, no room for
the player to engage enemies in the field before they reach the wall. The owner's
"larger map where the enemies spawn from" is literally absent.

### Cause B — The exterior terrain is cosmetic and is NOT in the NavMesh
`ExteriorTerrainBuilder.BuildExterior` builds a 300×300 Unity `Terrain` under a
GameObject named **`ExteriorRoot`**, and it runs **AFTER**
`VillageSceneBuilder.BakeVillageNavMesh` (the call order in `BuildVillage` is:
bake NavMesh → save → … → `ExteriorTerrainBuilder.BuildExterior()` near the end).
`BakeVillageNavMesh` only flags renderers under these `VillageRoot` children as
`NavigationStatic`:

```
"Ground", "Roads", "Approaches", "Walls", "Gates", "Buildings"
```

The `Terrain` is never marked navigation-static and isn't even present when the
bake runs. **Result: the entire wilderness is non-walkable for NavMeshAgents.** An
enemy that spawned out on the terrain would have `isOnNavMesh == false` and would
stand frozen, logging the "not on a baked NavMesh" warning.

### Cause C — The only navmesh outside the walls is the thin ground-floor seam
The walkable NavMesh outside the wall is just `BuildGroundFloor`'s flat hex disc,
which extends to `halfX = WallHalfX + 14` and `halfZ = WallHalfZ + SouthBowDepth + 14`
(today 42 m / 39 m half-extents; with WO-26 it grows to 56 m / 53 m) plus the
short approach lanes. So the spawn point at ~10.5 m out *does* currently land on
navmesh (barely), and wave 1 *can* technically run — but the march is trivially
short and there is no field depth. The world the owner wants (enemies emerging
from distant biomes and crossing real ground) does not exist as navmesh.

### Cause D — Approach corridors don't reach a real spawn ring
The approach is a 5-hex stub with a 3×3 grass apron at the end and nothing beyond.
Even if we move the spawn point far out (Cause A fix), there is no **continuous
baked corridor** from a distant spawn ring back to the gate — the terrain in
between is the cosmetic, non-navmesh `Terrain`. We must build navmesh-bearing
corridor geometry out to the spawn ring.

**Net:** the loop's code is healthy; it dies on world geometry. Fix = (1) push
spawn points out to a real ring, (2) build navmesh-bearing approach corridors +
spawn aprons reaching that ring, (3) include them in the NavMesh bake, (4) keep
the cosmetic terrain as a non-navmesh backdrop straddling the corridors.

---

## 2. Design — the larger enemy-spawn world + spawn→march→attack flow

### 2.1 Spawn ring & corridor geometry (quantitative)
Center everything on the village origin. Express distances **outward from each
gate** along the gate's `OutwardNormal` so the design follows whatever `WallHalf*`
the interior WO has set (WO-26 → 42/33/6).

Per cardinal gate (N/E/S/W):

| Element | Value | Notes |
|---|---|---|
| **Gate position** | `WallLayout.Gates[i].Position` | N: (0, +WallHalfZ); S: (0, −(WallHalfZ+SouthBowDepth)); E: (+WallHalfX, 0); W: (−WallHalfX, 0). |
| **Spawn ring distance from gate** | **40 m** along `OutwardNormal` | The owner's "spawn ~40 m outside the gate". Gives a real march + field-engagement zone. |
| **Spawn apron** | **16 m × 16 m** flat walkable pad centered on the spawn point | Room for a whole batch (up to 12 enemies) to materialize without overlap; NavMesh-baked. |
| **Approach corridor** | **8 m wide**, from the gate threshold out to the spawn apron (40 m long) | Continuous walkable lane; NavMesh-baked; the enemy "march path". 8 m clears the 4 m interior street + agent radius margin. |
| **Corridor surface** | KayKit `hex_road_A` paving over flat Y=0 hex grass | Reads as a paved approach road; both are nav-static. |
| **Spawn-point Y** | 0 (flat) | The march is on flat ground; the cosmetic terrain dips/rises around it. |

This makes the world read as: **walled town → 40 m paved/grassy approach corridor
out each gate → spawn apron at the corridor end → cosmetic biome terrain
everywhere off the corridors.** The corridors are the "roads the Hollow Ones march
down"; the terrain is the backdrop.

### 2.2 March flow (unchanged code, now with somewhere to march)
1. Wave 1: 8 `hollow-walker` spawn on the **north** apron at Z ≈ `WallHalfZ + 40`
   (= 73 m with WO-26's `WallHalfZ=33`), snapped to the baked apron navmesh.
2. Each enemy `SetDestination(Heart)` → NavMesh path runs: apron → corridor →
   gate threshold → interior road → plaza → Heart. The corridor + interior ground
   are one continuous baked surface, so the path solves end to end.
3. Enemy hits the north gate's force-field blocker, attacks it
   (`Gate.ApplyContactDamage`), drops it below 25% → blocker disabled → enemy
   pours through.
4. Enemy crosses the Heart inner ring (`_innerRingRadius`) → `TriggerBreach` →
   `SceneRouter.GoBattle` → ATBBattle. Loop pauses; on return, re-armed.

No change to `Enemy.cs`, `WaveManager` spawn/breach logic, `Gate.cs`,
`SceneRouter`, or `waves.json` is required for the loop to work — only the world
geometry and navmesh need to grow. (One optional WaveManager tuning value below.)

### 2.3 NavMesh area & bake
- Walkable navmesh footprint after this WO: the enlarged interior disc **plus four
  40 m corridors + 16 m aprons** radiating out the cardinal gates — a rough
  plus-shape ~`(2·WallHalfX + 2·40)` ≈ **164 m** E-W tip-to-tip and
  ~`(2·(WallHalfZ+SouthBowDepth) + 2·40)` ≈ **158 m** N-S tip-to-tip (with WO-26).
- The cosmetic `Terrain` (300×300) is **deliberately excluded** from the bake — it
  stays a backdrop. Only the corridor/apron/ground geometry is nav-static.
- Agent: the bake uses the project's default Humanoid agent (radius 0.5, height
  2.0); the skeleton enemy `NavMeshAgent` (radius 0.4, height 2.0) fits inside it.
  Corridors are 8 m wide → ≥ 8 agent-diameters, ample.

### 2.4 Cosmetic terrain coexistence
The `Terrain` is offset `TerrainBaseDepth = 0.5 m` below Y=0 and the village/seam
sit at Y=0. The corridors are flat hex tiles at Y≈0.015 (road) over Y=0 grass, so
they float just above the terrain along their length — same z-order treatment the
interior ground already uses. The `ExteriorTerrainBuilder` seam plateau
(`VillageHalfX/Z`, `SeamFalloff`) already holds the terrain flat under the village
footprint; we extend a flat strip under each corridor so the corridor doesn't
clip into a biome hill (see §3.1).

---

## 3. Concrete builder/WaveManager changes (old → new)

### 3.1 `ExteriorTerrainBuilder.cs` — flatten terrain under the spawn corridors
The corridors run 40 m out each gate, well past the current `SeamFalloff = 20`
flat band, so the north corridor would otherwise climb the rising north biome
(`NorthHeight` reaches +28 m). Hold the terrain flat (Y≈0) under each corridor +
apron so the baked corridor tiles sit flush on the terrain backdrop.

Add a corridor mask to the seam weight. In `SeamWeight(worldX, worldZ)` (currently
only the rectangular village footprint), OR-in a per-gate corridor rectangle:

```
// NEW: each cardinal corridor is a flat lane 40 m out, half-width 9 m
// (corridor 8 m + 1 m shoulder), plus the 16 m apron at the end.
float corridorSeam = CorridorSeamWeight(worldX, worldZ);
return Mathf.Max(rectangleSeam, corridorSeam);
```

`CorridorSeamWeight` returns 1.0 inside any of the 4 corridor+apron rectangles
(N/E/S/W), smoothstepping to 0 over a ~10 m falloff so the lane edge blends into
the biome. Corridor rectangles (relative to gate, half-width 9 m, length
`gateDist..gateDist+48`):
- North: X ∈ [−9, +9], Z ∈ [`WallHalfZ`, `WallHalfZ + 48`]
- South: X ∈ [−9, +9], Z ∈ [`−(WallHalfZ+SouthBowDepth) − 48`, `−(WallHalfZ+SouthBowDepth)`]
- East:  Z ∈ [−9, +9], X ∈ [`WallHalfX`, `WallHalfX + 48`]
- West:  Z ∈ [−9, +9], X ∈ [`−WallHalfX − 48`, `−WallHalfX`]

Constants to add at the top of `ExteriorTerrainBuilder`:

| New const | Value | Why |
|---|---|---|
| `CorridorHalfWidth` | `9f` | 8 m lane + 1 m shoulder, flat. |
| `CorridorLength` | `48f` | Reaches the 40 m spawn ring + 8 m apron margin. |
| `CorridorFalloff` | `10f` | Soft blend from flat lane into the biome. |

Also exclude corridor footprints from the tree/rock scatter: in `PaintTrees` and
`ScatterRocks` the existing guard is `if (SeamWeight(...) > 0.05f) continue;` —
because corridors now feed `SeamWeight`, trees/rocks automatically avoid the lanes.
No extra change needed there (a free win from routing corridors through
`SeamWeight`).

> NOTE: keep the biome *visuals* — the corridors are flat ground the enemies march
> on, with forest/snow/barren terrain rising on either side, exactly the "enemies
> emerge from the wilderness and march down the road" read the owner wants.

### 3.2 `VillageSceneBuilder.BuildApproaches` — push spawn out + build the corridor
Current (≈ lines 1405–1489): 5 road hexes + a 3×3 apron at `7 * step` (~10.5 m).
Replace the magic `7f * step` and the 5-hex loop with corridor-length math driven
by a new const, and lay road the full corridor length.

| Element | Old | New |
|---|---|---|
| Approach road length | `for i in 1..5` (~7.5 m), 2 tiles wide | Road tiles from the gate out to **40 m**, **5 tiles wide (~8 m)** to match the 8 m corridor. Loop `i` until `i*step >= ApproachLength`. |
| Spawn-point distance | `spawnCentre = gatePos + outward * (7f * step)` | `spawnCentre = gatePos + outward * ApproachLength` with `ApproachLength = 40f` (a world-unit const, NOT a hex multiple — so it's independent of N/S vs E/W step). |
| Spawn apron | 3×3 hex grass (~5 m) | **16 m × 16 m** flat grass apron (e.g. `gx,gz in −5..+5` at `HexWidth`/`HexDepth` pitch) centered on the spawn point, so a 12-enemy batch fits. |
| Corridor lateral width | `{ -HexWidth*0.5, +HexWidth*0.5 }` (2 tiles) | `{ -2*HexWidth, -HexWidth, 0, HexWidth, 2*HexWidth }` (5 tiles ≈ 8 m). |
| `WaveSpawnPoint.Configure` | `("spawn-"+index, index, direction, gatePos)` | UNCHANGED — id/index/direction/gatePosition contract is identical; only the marker's world position moves out to 40 m. `HeadingToGate` still resolves correctly (gatePosition − transform.position). |

Add a const near the other geometry consts in `VillageSceneBuilder`:

```
// Spawn corridor: enemies materialize this far OUTSIDE each gate and march in.
private const float ApproachLength = 40f;   // world units, gate -> spawn ring
private const float SpawnApronHalf = 8f;     // 16 m apron half-extent
```

> Keep the owner-directed removals: no approach boulders/foliage (the lane stays
> bare paving + grass). The 40 m corridor is paving + grass only.

### 3.3 `VillageSceneBuilder.BakeVillageNavMesh` — corridors already covered
The corridor road + apron grass are built under the existing **`Approaches`** root
(`BuildApproaches`'s `approachRoot`), and `Approaches` is already in
`BakeVillageNavMesh`'s `navStaticRoots` list. So the longer corridors + bigger
aprons are **automatically baked** — no list change needed, provided
`BuildApproaches` keeps parenting everything under `approachRoot`. **Verify** the
bake's walkable surface now spans gate→apron after the rebuild (acceptance §4).

One real risk to call out: the legacy `UnityEditor.AI.NavMeshBuilder.BuildNavMesh`
uses the project's default agent **max slope / step** settings. The corridors are
flat (held by §3.1), so slope is a non-issue; but confirm the project's default
agent `maxSlope` is ≥ ~30° and `stepHeight` ≥ 0.4 so the corridor↔interior seam
(the 0.5 m terrain step exists only OFF the corridor) never breaks the mesh. No
code change expected; this is a bake-settings sanity check.

### 3.4 `WaveManager` — breach ring (tuning, optional; with WO-26 coordination)
`_innerRingRadius = 9 m` is fine and works with the existing march. With WO-26's
larger interior (Heart still at center, plaza ~12 m radius walkable), 9 m sits
inside the plaza — keep it. **No code change required.** If playtest shows the
breach firing before an enemy visually reaches the Heart steps, nudge
`_innerRingRadius` down to ~6–7 m via the inspector (it is a `[SerializeField]`),
not in the builder. Document only; do not change the default here.

### 3.5 No changes needed
`Enemy.cs`, `Gate.cs`, `SceneRouter.cs`, `BattleParams`, `WaveSpawnPoint.cs`,
`waves.json`, `BuildWaveManager`/`WireSpawnPointList` — all unchanged. The
spawn-point list is auto-wired from the scene; the enemy prefab, breach hand-off,
and ATB round-trip are all already correct.

---

## 4. Acceptance criteria

A re-built scene (`BuildVillage` → chains exterior + bake) passes when:

1. **Spawn distance:** each `WaveSpawnPoint` sits **40 m** outside its gate along
   the gate's outward normal (N spawn at Z ≈ `WallHalfZ + 40`), measurable in the
   scene, not ~10 m.
2. **Wave 1 spawns in the field:** on Play, after the countdown, **8 Hollow
   Walkers materialize on the north spawn apron ~40 m outside the north gate** (not
   on the doorstep, not inside the walls).
3. **Navigable march:** every spawned enemy has `NavMeshAgent.isOnNavMesh == true`
   (no "not on a baked NavMesh" warning in the log) and **paths across a continuous
   baked corridor** from the apron → north gate → interior → Heart. Enemies visibly
   walk the full 40 m+ corridor.
4. **Baked corridor coverage:** the baked NavMesh forms a plus-shape — interior
   disc + four ~8 m-wide × 40 m corridors + 16 m aprons out the cardinal gates.
   Selecting the navmesh in the editor shows walkable surface from each apron to
   the Heart with no gaps at the gate threshold.
5. **Attack the gate:** an enemy reaching the closed north gate stops, deals
   contact damage (`Gate.Hp` falls), and once the gate drops below 25% the blocker
   disables and the enemy pours through toward the Heart.
6. **Breach → ATB:** the first enemy to cross the Heart inner ring (or fire
   `ReachedHeart`) triggers `WaveManager.TriggerBreach` →
   `SceneRouter.GoBattle(BattleParams{ Wave=1, BreachedIds=[…] })` → the ATBBattle
   scene loads; on battle return the loop re-arms.
7. **Cosmetic terrain intact, non-navmesh:** the 300×300 biome `Terrain` still
   renders (forest N, farmland E, barren S, river valley W, dawn skybox/fog), is
   **held flat under each corridor** (no hill clipping the lane), and is NOT in the
   navmesh (enemies never path onto the open terrain).
8. **No interior desync / no WO-26 conflict:** all exterior distances derive from
   `WallLayout.Gates` / `WallHalf*`, so they remain correct whether or not WO-26's
   enlarged ring is applied; the 4 cardinal gates still open and are walk-through.

---

## 5. Applying (OWNER-GATED — do NOT run as part of this work order)

> **Hard rule:** Implementing this requires re-running the village scene builder,
> which re-bakes the entire Village scene, the exterior terrain, and the NavMesh.
> That is **owner-gated**. This work order is design + spec only. To apply, the
> owner makes the §3.1–§3.3 edits (in `ExteriorTerrainBuilder.cs` and
> `VillageSceneBuilder.cs`), lets `DeNelle.Village`/`DeNelle.Editor` recompile, then
> runs:
>
> `Defenders > Week 3 > Build Village Scene`
> (or `-executeMethod DeNelle.Editor.VillageSceneBuilder.BuildVillage`)
>
> The build is idempotent (it nukes + rebuilds `VillageRoot` and `ExteriorRoot`),
> so it is safe to re-run while iterating on the corridor numbers. If applying
> alongside WO-26, make both files' wall-const edits first; the WO-27 corridor
> math is `WallHalf*`-relative and order-independent.

**Key files:**
`Assets/Editor/ExteriorTerrainBuilder.cs` (SeamWeight/corridor flattening),
`Assets/Editor/VillageSceneBuilder.cs` (`BuildApproaches` spawn distance + corridor,
`BakeVillageNavMesh` verify), `Assets/_Modules/Village/Waves/WaveManager.cs`
(breach-ring tuning only, inspector), `Assets/_Modules/Village/Enemies/Enemy.cs`
(NavMeshAgent march — reference, no change),
`Assets/_Modules/Village/Waves/WaveSpawnPoint.cs` (marker — no change),
`Assets/StreamingAssets/Data/Canonical/waves.json` (schedule — no change).

> **OWNER RULING 2026-08-21 (verbal, this session):** CLOSED by an explicit owner sweep of the WHOLE 0-800 RANGE: "I've eyeballed them many times, and all of them are already completed. or immaterial." This is a RANGE close on the owner's direct review, not a per-ticket verdict.
