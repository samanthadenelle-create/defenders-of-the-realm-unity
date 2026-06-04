# Village Size Spec — match this exactly (Unity meters, +X = East, +Z = North)

Whatever generates the town (Grok's quadrant generator or our builder), it must hit
these dimensions so the gameplay systems (gates, spawns, navmesh, Heart) line up.

## Coordinate space
- **Center of the map = world origin (0, 0, 0).** Ground plane at Y = 0.
- Unity meters, 1 unit = 1 m. Buildings sit ON the ground (feet at Y=0).

## Footprint — RECTANGLE, not a square
- **Curtain-wall line: X = ±42, Z = ±33** → interior ≈ **84 m (E–W) × 66 m (N–S)**.
- Ground/terrain extends **~14 m beyond the walls on every side** (the gate approach + enemy spawn apron). So the full playable rectangle is roughly **±56 X / ±47 Z**.
- (Grok's `quadrantSize = 45` makes a square ±36 ring — too small AND wrong shape. Use the ±42/±33 rectangle.)

## The 4 gates — the walls MUST have a clear opening at each (do not ring solid)
| Gate  | Wall it sits on | Opening center | Opening width |
|-------|-----------------|----------------|---------------|
| North | Z = +33         | X = 0          | **12 m**      |
| South | Z = −33         | X = 0          | **6 m**       |
| East  | X = +42         | Z = 0          | **6 m**       |
| West  | X = −42         | Z = 0          | **6 m**       |
- A **spawn marker 12 m OUTSIDE each gate** (e.g. North spawn ≈ (0,0,45)): enemies appear there and path gate → Heart. Keep the gate→center lane clear of buildings (a road runs each gate to the plaza).

## Center
- **Tree of Life / Heart of Elarion at (0,0,0)**, with a clear **~10 m-radius plaza** around it (no buildings inside the plaza). It's the lose-condition object.

## Layout (Grok's 4-quadrant idea is good — fit it in the rectangle)
Quadrant centers roughly at **(±18, 0, ±14)** so they sit between the plaza and the walls, clear of the 4 gate lanes:
- **NE — Crafting/Industry:** Forge (Blacksmith), Armorer, Workshop/Arcane Tower
- **NW — Lumber/Resource:** Lumbermill, Farm, storage
- **SE — Residential:** small + medium houses
- **SW — Market:** Market stalls, Tavern, houses
- Fill remaining space with houses + props (barrels, crates, fences, wagons, vines from the pack). Keep gate roads + plaza open.

## Gameplay buildings that MUST exist (we wire panels/behaviour to these by name/type)
Forge, Market, Workshop (Arcane Tower), PetHouse, Farm, Lumbermill, Armorer — plus
**Defense Towers along the wall** (the rampart walkway top is at Y ≈ 5.4 m; walls ~5–6 m tall).
Everything else (houses, tavern, church, props) is set dressing.

## Deliverable back to us
Assembled building **prefabs** (from the modular Medieval Village kit) + the laid-out town
(prefab or generator). We then attach: HeartController (Tree), Gate+GateProximityOpener+SpawnPoint
per gate, NavMesh bake (buildings = obstacles, gate lanes clear), Building+BuildingInteractable
on the gameplay buildings, and swap the scene loader to it.

## Scale sanity
Hero ≈ 1.8 m tall. A small house ≈ 6×6 m footprint, 5–6 m tall. A gate opening of 6 m
fits the hero + a small enemy group comfortably; the 12 m North gate is the "main" gate.
