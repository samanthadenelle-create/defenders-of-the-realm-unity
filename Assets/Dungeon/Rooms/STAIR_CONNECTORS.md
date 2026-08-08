# Stair connector room prefabs (snap-on)

Built by `DefaultStairConnectorRoomsBuilder`.

| Prefab | Shape | Vertical socket | Snap door | Stair geometry |
|--------|-------|-----------------|-----------|----------------|
| `StairConnector_Vertical_Down` | Vertical | StairDown | S (`s_door_01`) | **none** — upper landing + floor hole |
| `StairConnector_Vertical_Up` | Vertical | StairUp | S (`s_door_01`) | **owns the full flight** + solid floor |
| `StairConnector_Left_Down` | Left | StairDown | S (`s_door_01`) | **none** — upper landing + floor hole |
| `StairConnector_Left_Up` | Left | StairUp | S (`s_door_01`) | **owns the full flight** + solid floor |
| `StairConnector_Right_Down` | Right | StairDown | S (`s_door_01`) | **none** — upper landing + floor hole |
| `StairConnector_Right_Up` | Right | StairUp | S (`s_door_01`) | **owns the full flight** + solid floor |

## ⚠ One owner — do not put a flight in both

A StairDown socket sits `FloorSeparationY/2` BELOW its room origin and a StairUp
socket the same distance ABOVE, so the composer stacks the mated pair as
`Y_down = Y_up + FloorSeparationY`. A flight authored from the room origin ascends
exactly one floor, so:

- the **`_Up`** room's flight spans `Y_up → Y_down` — it **is** the connection;
- the same flight in the **`_Down`** room would span `Y_down → Y_down + 6`, i.e.
  through its own ceiling into open air, interpenetrating the first.

So `_Up` owns the whole flight and gets a SOLID floor; `_Down` has no steps and no
ramp, and its floor carries the HOLE the arriving flight comes up through. Both the
flight and the hole are generated from one `FlightPlan()`.

## Snap-on use
- Mate any corridor door to `s_door_01` (composer rotates the room).
- Mate `stair_down_01` on an upper connector to `stair_up_01` on the lower.
- **Pair the SAME shape** (`Vertical_Down` over `Vertical_Up`, …): the landing's hole
  is cut for that shape's arrival point.
- Walk surface = invisible ramp (BoxCollider); steps are visual only.

Rebuild: **Defenders → Dungeon → Build Stair Connector Room Prefabs**
Batch: `DeNelle.Editor.RoomForge.DefaultStairConnectorRoomsBuilder.BuildAllBatch`
