# Stair prefab script — the integration contract

**For:** whoever writes the stair-builder script (Grok) · **Date:** 2026-08-07 · **From:** CLI seat

Hand this over with the script brief. These are the things a from-scratch stair builder **cannot guess from
outside this repo**, and each one has already caused a real defect here. Get these right and the script drops
in; get any of them wrong and it compiles, runs, produces prefabs, and the dungeons are subtly broken.

---

## 1. Read the constants. Never re-type them.

```csharp
using DeNelle.Dungeons.RoomForge;   // RoomForgeCanon
RoomForgeCanon.Cell                 // 10.0   room footprint (1x1 = 10x10)
RoomForgeCanon.WallHeight           //  4.0
RoomForgeCanon.WallThickness        //  0.4
RoomForgeCanon.FloorSlabThickness   //  0.1
RoomForgeCanon.CeilingThickness     //  0.3
RoomForgeCanon.FloorOccupiedHeight  //  4.4   slab + wall + ceiling

DungeonBakerChecks.FloorSeparationY //  6.0   vertical distance between floor origins
```

`RoomForgeCanon` exists **because** these were previously copied into the builder, the baker, the dresser and
five oracles — and a cell widen left the oracles guarding a shape that had moved. A hardcoded `10f` or `6f`
anywhere in the new script is a bug the day either number changes.

**The tight constraint is `6.0 − 4.4 = 1.6 m` of dead space between floors.** Head clearance, not slope, is
what makes a stairwell hard here.

---

## 2. The stair socket contract — copy it exactly

`DefaultDungeonRoomsBuilder.AddStairSocket` is the reference. Any new stair room must produce the same shape:

```csharp
float halfFloor = DungeonBakerChecks.FloorSeparationY * 0.5f;      // 3.0
go.transform.localPosition = new Vector3(0f, down ? -halfFloor : halfFloor, 0f);
go.transform.localRotation = down
    ? Quaternion.LookRotation(Vector3.down, Vector3.forward)
    : Quaternion.LookRotation(Vector3.up,   Vector3.forward);
sock.id        = down ? "stair_down_01" : "stair_up_01";
sock.type      = down ? RoomSocketType.StairDown : RoomSocketType.StairUp;
sock.facing    = "U";
sock.halfWidth = 1.2f;
```

**⚠ X AND Z MUST BE EXACTLY 0.** This is not style. Sockets sit on the half-cell grid (`Cell/2` = 5.0) and the
composer relies on `cell = [round(x), round(y), round(z)]` being a lossless round-trip. A door helper offsets
sockets 0.5u off the wall face; the stair socket once inherited that, and **`RoundToInt` quantised each half
unit into a FULL unit of drift** — accumulating down a descent until rooms that should touch sat 1u too close
and **the bake aborted on overlap**. That was the original `dg_bonecrypt` / `dg_ember_deep` failure.

**Mate test is `dot(a.Outward, -b.Outward) >= 0.25`,** and `RoomSocket.Outward = transform.forward`. StairDown
points **−Y**, StairUp points **+Y**, so a pair scores **+1**. Both pointing down scores −1 and can never mate —
that, not the composer, is why no multi-level bake existed before.

**Each stair room also needs a HORIZONTAL door socket** so a corridor can reach it. A stair room with only a
vertical socket is unreachable.

---

## 3. Renderer vs collider — the split that makes nav work

`NavMeshSurface` is configured `useGeometry = NavMeshCollectGeometry.PhysicsColliders` (`DungeonBaker.cs:288`).
**Colliders, not meshes.** So:

| Part | Renderer | Collider |
|---|---|---|
| **Visual steps** | **ON** | **OFF** |
| **Ramp** (the walk surface) | **OFF** (destroy `MeshRenderer` + `MeshFilter`) | **ON** — `BoxCollider` |

- **A mesh with no collider is invisible to the navmesh bake.**
- **Step colliders and the ramp must not both exist** — two surfaces at slightly different heights make the
  agent walk one while the character renders on the other.
- The repo already has this exact pattern: `HideMesh()` destroys the renderer and keeps the collider. It is how
  the outpost wall cladding stays nav-neutral.

**Use a thin `Cube`, not `PrimitiveType.Plane`.** A Cube gives a `BoxCollider` — cheaper and more robust than
the `MeshCollider` a Plane brings. (A Plane is also 10×10 units at scale 1, which fights the 10 m cell.)

---

## 4. The four geometry rules

1. **Ramp sits on the NOSE LINE** — the front-top edge of every step, not tread centres. Too low, feet sink
   into treads; too high, the character floats.
2. **Ramp OVERLAPS both landings.** NavMesh needs continuous overlapping walkable surface to join regions. A
   hairline butt-joint produces two islands and a `PathPartial` — which is exactly the bug this is fixing.
   **The seam IS the connection, not cosmetics.**
3. **Cut the hole.** The flight passes through the lower room's ceiling and the upper room's floor slab. Both
   are solid today. A ramp that ends at a ceiling is a ramp into a wall.
4. **Slope:** 6.0 m rise. One 10 m cell run = **31°**; two 3 m half-flights over 5 m each = **31°** in half the
   footprint. Unity's default agent max slope is 45°. **Verify the real setting** — `NavMeshAreas.asset` has one
   agent type (`Humanoid`, id 0, **radius 0.5**, height 2, climb 0.75, slope 45), so the minimum walkable slot
   is **1.000 m**. Any gap narrower than that generates no navmesh at all.

---

## 5. ⚠ Decide which room owns the flight

The connector model pairs a `StairDown` room (upper floor) with a `StairUp` room (lower floor) on the vertical
socket. **A 6 m flight physically spans both** — it starts upstairs, passes through that room's floor slab, and
lands inside the lower room's volume.

Pick **one**, explicitly:
- **One owner** — `StairDown` carries the whole flight; `StairUp` is a bare landing with **no stair geometry**; or
- **Split** — each carries a half-flight meeting at a shared mid-height landing.

**If both prefabs place a flight you get two interpenetrating staircases**, and it will read as a rendering bug.

---

## 6. Where the script writes, and the overwrite trap

- A **generator** (code that produces the prefabs) should write to `Assets/Dungeon/Rooms/` like
  `DefaultDungeonRoomsBuilder` does, and be callable from the same batch entry point.
- **Hand-authored** prefabs must NOT live there. `BuildAll()` overwrites that folder and runs on every bake
  wave. Use `Assets/Dungeon/Rooms/Authoring/` (see `RoomAuthoringBench.cs`), then promote deliberately.
- New rooms must also land in `rooms-catalog.json`, which the builder writes — a prefab the catalog does not
  know about is a prefab the composer will never place.

---

## 7. Assembly rules

- Editor code → `DeNelle.Editor`. It **cannot** reference `DeNelle.Village` (that is why `DungeonBaker` uses
  `FindType()` reflection for Village types).
- `RoomForgeCanon` and `RoomSocket` live in the runtime `DeNelle.Dungeons` assembly — editor code may read them.
- Do **not** invent a type. Three separate WOs here referenced a `VfxEmitter` that exists **nowhere** in the
  tree. **Grep before you call.**

---

## 8. Before it is considered done

- `python` brace-balance check on every `.cs` (CLAUDE.md §1), no NUL bytes.
- `COMPILE_GATE_OK`.
- Re-run `BuildAll` → recompose all graphs (`GraphDungeonComposer.ComposeAllBatch`) → **every dungeon must print
  `PathComplete`**, not `PathPartial`. That assertion already exists in the bake summary. **Do not loosen it.**
- `REGRESSION_OK n/n suites`.
- A headless capture (`DungeonSceneCapture.CaptureAll`, marker `DUNGEON_CAPTURE_OK`) **opened and looked at** —
  compile-green has never once proved a room looks right in this project.
- A `[stair-shell]` oracle in the shape of `RoomForgeRegression` case 11 `[room-shell]`, reading the **shipped**
  prefabs and asserting: rise == `FloorSeparationY`; a collider-bearing ramp exists; that ramp has **no**
  renderer; slope under agent max; landings overlap; and the §5 ownership invariant holds. **Run it over every
  variant** — the two used least are the two that rot.

---

## 9. Existing pieces worth reusing rather than rewriting

| Want | Already exists |
|---|---|
| hide a mesh, keep the collider | `HideMesh()` — `KayKitChallengeOutpostBuilder.cs:1030` |
| strip colliders from visual-only geometry | `StripColliders()` — same file |
| measure a prefab's real bounds | `MeasureKit()` pattern — same file; logs `KIT MEASURED …` |
| wall with a central opening | `SpanWall()` — same file *(and note `BuildChoke` split on the **wrong axis** for months, producing two parallel barriers with no door; that is the failure mode this helper prevents)* |
| a shipped-prefab oracle | `RoomForgeRegression` case 11 `[room-shell]` |
| nav clearance reporting | `ReportNavClearances()` — prints every sub-3 m slot with PASS/FAIL vs 2×agentRadius |
