# WO-1568 RESULT - the composed-dungeon door is a framed doorway

**Status:** IMPLEMENTED - 2026-09-07 uncommitted, awaiting gate
**Lane:** World/Environment - presentation only. Door LOGIC untouched.

## What was built

- **`CommonDungeonDoor.BuildDoorVisual(Transform socket, float halfWidth, bool open)`** - the static
  seam WO section 7(a) recommended. The runtime `BuildDoor`, the capture and the oracle all drive it,
  so the thing photographed and the thing pinned are the thing the game builds.
- **Frame (new, render-only):** two jambs at `+/-(halfWidth + JambWidth/2)`, full
  `RoomForgeCanon.WallHeight`, standing 0.18 m proud of each wall face; a **lintel** running from the
  measured top of the leaf up to `WallHeight`, which closes the 1.6 m letterbox and keeps the opening
  reading as a doorway while the door is OPEN. **Zero colliders** on all three (no NavMesh re-bake,
  never trap the hero).
- **Leaf:** KayKit `wall_doorway_door` resolved through the `DungeonExitInteractable.ResolveExitProp`
  ladder (tracked Resources -> editor `AssetDatabase` -> warned fallback). Bounds are measured
  **detached, before parenting** and scaled to `DoorGap - 2*0.1` = 2.0 m, giving a real 0.1 m reveal
  each side. Exactly one collider: a `BoxCollider` derived from those bounds, z clamped to
  `WallThickness` - it is the same `_blocker` `SetOpen` already toggled.
- **Fallback is still door-shaped:** body + hinge-side stile + two raised panel reliefs + handle,
  height derived as `WallHeight * 0.7`. The old flat slab cannot come back through the fallback.
- **`FlowTrace`** names the resolved source (`resources` / `editor-kit` / `primitive-fallback`);
  every fallback is a `Warn`. Nothing silent, nothing stripped.

**Unchanged, as specified:** `CommonDoorPolicy`, `Configure`, `Update`, `SetOpen`, `OnDisable`,
`ClaimedConnections`, `OpenDistance`, `CloseDistance`, `DegreesPerSecond`, `PromptPriority`, the
hinge pivot at `-halfWidth`, and every `RoomSocket` field. `OpenAngle` changed from `private` to
`public const` so the oracle can assert against it - **visibility only, value still 100**.

## Files

| File | +/- |
|---|---|
| `Assets/_Modules/Dungeons/RoomForge/CommonDungeonDoor.cs` | +364 / -21 |
| `Assets/Editor/DungeonSceneCapture.cs` (new `CaptureDoor` + mock wall) | +113 / -0 |
| `Assets/Editor/Regression/DungeonDoorShapeRegression.cs` (new, + `.meta`) | +246 |
| `Assets/_Modules/Dungeons/RoomForge/README.md` | +12 / -0 |
| `Assets/Resources/Dungeon/Door/wall_doorway_door.obj` + `.mtl` + 3 fresh-guid `.meta` | new, tracked |

Brace balance and NUL scan pass on all three `.cs`; every line authored here is ASCII + LF
(`DungeonSceneCapture.cs` keeps its pre-existing non-ASCII header, untouched).
`git status`: **no `.unity` and no `Assets/Dungeon/Rooms/*.prefab` modified.** No `RoomForgeCanon`
value re-typed - every dimension is read from it.

## Registration line (DataRegression.cs NOT edited, per the lane)

```
DeNelle.Core.Diagnostics.Guard.Try("Regression", "dungeon-door-shape suite", () => { if (!DeNelle.Editor.Regression.DungeonDoorShapeRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[dungeon-door-shape] " + r); });
```

## Owed before this closes

- [ ] `COMPILE_GATE_OK` + `REGRESSION_OK <n>/<n> suites` on fresh logs (judge the marker, not the exit code).
- [ ] `DungeonSceneCapture.CaptureDoor` run -> `Builds/dungeon-capture/door_closed.png` + `door_open.png`,
      marker `DUNGEON_DOOR_CAPTURE_OK 2`.
- [ ] **Both PNGs opened and looked at**, then **desaturated to greyscale** and confirmed the door
      still reads by frame / inset / relief / depth. Colour is not a cue.
- [ ] Owner felt-verifies and closes (PO closes, not CLI).
