> ## RECONCILED 2026-08-08 - true status is STILL LIVE - the stated P0 is unresolved at HEAD
> Audit `docs/reference/WO_TRUE_STATUS_2026-08-08.md`. Evidence: the headline claim below (floors
> placed but not connected) is still true at HEAD. Canon 2026-08-08 records PathPartial and a floor
> delta start-to-stop of 0.00m, and there are ZERO instantiated `NavMeshLink`/`OffMeshLink` components
> anywhere in the dungeon code - only a comment and a diagnostic string. No `NavMeshAgent` can cross
> floors. A Status line has been added - the file previously had none.

# HANDOFF → GROK — Multi-level dungeon traversal is not connected, plus 8 open visual defects

**Status:** STILL LIVE - the stated P0 is unresolved at HEAD (reconciled 2026-08-08)
**Date:** 2026-08-07 · **From:** CLI seat · **For:** Grok (spec/design pass) · **Then:** UI refines → CLI implements
**Everything below was read from source or measured from a bake TODAY.** Where something is inference, it says so.

---

## 0. THE HEADLINE, WITH THE PROOF

**Every multi-level dungeon in the project is unfinishable. The floors are placed correctly and are not connected.**

Measured from tonight's full recompose (`COMPOSE_ALL_OK 5/5`, all `matesFail=0`, all `saved=True`):

| Dungeon | Rooms | Levels | Path entry → deep target |
|---|---|---|---|
| `dg_starter_loop` | 11 | **single floor** | **`PathComplete`** ✅ |
| `dg_descent_probe` | 5 | multi | `PathPartial` |
| `dg_sunken_vault` | 17 | multi | `PathPartial` |
| `dg_bonecrypt` | 21 | multi | `PathPartial` |
| `dg_ember_deep` | 22 | multi | `PathPartial` |

**The only dungeon that completes is the only one with no stairs.** That is not a coincidence and it is not a
pathfinding tuning problem — there is no walkable surface between floors at all.

### ★ IT IS NOT JUST TRAVERSAL — THE DESIGN ALREADY ASSUMES REAL STAIRS ★

Owner, from a play capture (2026-08-07): *"this shows why we need the stairs to be real."*

The layout data agrees, by name. `dg_descent_probe.json`:

```json
"locks":  [{ "id": "gate-to-stairs", "keyId": "probe-key",
             "fromRoomId": "corr1", "toRoomId": "stair_down" }]
"keys":   [{ "id": "probe-key", "roomId": "corr1" }]
"extracts": [{ "id": "extract-entry", "roomId": "entry",      "label": "Extract" },
             { "id": "extract-deep",  "roomId": "deep_vault", "label": "Extract (deep)" }]
```

**There is a keyed gate whose destination is literally `stair_down`.** The intended loop is: find
`probe-key` → open `gate-to-stairs` → descend. The key, the lock and the destination all exist. **Only the
stairs do not.** So the gate opens onto nothing walkable.

**Two consequences, and the second is the one that matters:**

1. **The `Extract` button is a WORKAROUND, not a feature.** Each layout ships two of them because, with no
   descent, a run has no other way to resolve. A dungeon whose exit is a UI button rather than a *place* is one
   the player **dismisses** rather than **escapes** — and that is a different feeling entirely. Tension has
   nowhere to land.
2. **It explains why the gate looks the way it does** (§3.1). Nobody ever had to make that slab read as *an
   entrance to somewhere*, because there was no somewhere. It is a flat unlit rectangle by neglect, not by
   choice. Build the stairwell and the gate stops being a green wall and becomes a doorway with steps falling
   away behind it, lit from below — which is a *moment*. That is also why §3.1 should be solved **after** this,
   not before: re-shading a gate that opens onto void is polishing the wrong thing.

**So the priority argument is not "traversal is missing."** It is: **the extract loop is currently
non-diegetic, and the stairs are what make it real.**

### ⚠ CORRECTION — "unfinishable" was WRONG. The floors ARE reachable, by TELEPORT.

An earlier draft of this doc (and my report to the owner) said multi-level dungeons are unfinishable. **That is
not true and the correction matters**, because it changes what the stairs are actually fixing.

`DungeonPortLink.WarpHero()` **warps the hero** between floors — `DungeonHero.Teleport` (the
CharacterController-safe path) or `HeroLocomotion.WarpTo` — behind an interact prompt reading **"Climb"**.
`DungeonBaker.cs:648` states the design intent verbatim:

> *"Reuses the cottage WO-711 port idiom — **no staircase mesh, no NavMeshLink**"*

So descent works today. It is a **deliberate teleport**, not a bug. The bake reports `stairPorts=8`
(bonecrypt), `10` (ember deep), `2` (probe) — those ports exist and function.

**WHAT IS ACTUALLY BROKEN, and it is worse in one specific way:**

Grep for `NavMeshLink` / `OffMeshLink` across `Assets/_Modules/Dungeons` and `Assets/Editor/RoomForge`:
**zero hits.** The navmesh is genuinely two disconnected islands. So:

- **NO `NavMeshAgent` CAN CROSS FLOORS.** Every enemy, companion and pet is **stranded on the floor it spawned
  on.** You descend and the fight simply ends — nothing follows you down. That is a combat-design failure, not
  a traversal inconvenience, and nothing in the ticket list had named it.
- `NavMesh.CalculatePath(entry → deep target)` returns `PathPartial` — which is exactly what the bake oracle
  measures. The oracle is honest; it was reporting the navmesh, not the player's ability to progress.

**So the real statement is:** the dungeons are *completable* and *not diegetic* — the entire vertical axis is a
UI interaction (locked gate → prompt → warp), and **no AI can use it at all.**

**This also sharpens what the ramp must deliver.** A ramp is not just nicer than a warp; it is the only one of
the two that **enemies can use**. If a design ever chooses to keep warping for the hero, it still needs a
`NavMeshLink` for the AI — otherwise chases end at every floor.

### ⚠ OPEN DESIGN QUESTION — which room owns the flight?

The connector model pairs a `StairDown` room (upper floor) with a `StairUp` room (lower floor), mated on the
vertical socket. **A 6 m flight physically spans both**: it starts on the upper floor, passes through that
room's floor slab, and lands inside the lower room's volume.

So the kit has to decide, explicitly, one of:
- **One owner** — e.g. the `StairDown` prefab carries the whole flight and the `StairUp` room is a bare landing
  (its prefab must then contain **no** stair geometry, or the two interpenetrate); **or**
- **Split** — each carries a half-flight meeting at a shared landing at the mid-height.

**Whichever is chosen, the other room must NOT also place a flight.** Both prefabs placing one is two
interpenetrating staircases, and it will look like a rendering bug rather than a design mistake. The
`[stair-shell]` oracle should assert the chosen invariant so it cannot regress.

### Why it happens

The composer solves the graph, mates the stair sockets and places the upper room exactly `FloorSeparationY`
above the lower one. It never creates anything to *walk on* between them. Unity's NavMesh does not bridge a
vertical gap on its own; it needs either

- a **walkable slope** whose angle is under the agent's max slope, or
- an explicit **`NavMeshLink`** / off-mesh link.

Neither exists. `RoomSocketType.StairUp` / `StairDown` are *placement* metadata — they tell the composer where
to put the next room. Nothing consumes them as geometry.

Related open ticket: task #37, *"Slice 1b: stair rooms have no stair geometry and no navmesh link, so floors are
placed but not walkable."* This handoff is the full workup for that.

---

## 1. THE EXACT NUMBERS (verified at source tonight — do not re-derive, do not re-type)

All shared geometry lives in `Assets/_Modules/Dungeons/RoomForge/RoomForgeCanon.cs`. **Read from it; never
hardcode a copy.** That file exists specifically because these numbers were previously duplicated across the
builder, the baker, the dresser and five oracles, and a cell widen left the oracles guarding a shape that had
moved.

```
RoomForgeCanon.Cell                  = 10.0   // 1x1 room = 10m x 10m, 2x2 = 20m x 20m
RoomForgeCanon.WallHeight            =  4.0
RoomForgeCanon.WallThickness         =  0.4
RoomForgeCanon.DoorGap               =  2.2
RoomForgeCanon.FloorSlabThickness    =  0.1
RoomForgeCanon.CeilingThickness      =  0.3
RoomForgeCanon.FloorOccupiedHeight   =  4.4   // slab + wall + ceiling

DungeonBakerChecks.FloorSeparationY  =  6.0   // vertical distance between floor origins
```

**`FloorSeparationY` is 6, and its only constraint is clearing what a floor OCCUPIES (4.4).** It is NOT tied to
`Cell` — Cell is horizontal. That leaves **1.6 m of dead vertical space** between the ceiling of one floor and
the slab of the next. That gap is where a stairwell has to live, and it is tight.

### Stair socket contract (`DefaultDungeonRoomsBuilder.AddStairSocket`)

```csharp
float halfFloor = DungeonBakerChecks.FloorSeparationY * 0.5f;   // 3.0
go.transform.localPosition = new Vector3(0f, down ? -halfFloor : halfFloor, 0f);
go.transform.localRotation = down
    ? Quaternion.LookRotation(Vector3.down,  Vector3.forward)
    : Quaternion.LookRotation(Vector3.up,    Vector3.forward);
sock.halfWidth = 1.2f;
```

- Mate test is `dot(a.Outward, -b.Outward) >= 0.25`. StairDown points **−Y**, StairUp points **+Y**, so a pair
  scores **+1**. (Both used to point down, scoring −1 — that, not the composer, is why no multi-level bake
  existed before WO-1001 slice 1.)
- Each socket sits **half a floor** off its own room origin, so when the composer slides the child until socket
  origins coincide, the rooms land exactly `FloorSeparationY` apart with no elevation field in the graph schema.

### ⚠ THE GRID INVARIANT — the trap that has already cost one debugging cycle

Sockets must sit on the **half-cell grid** (`Cell/2` = 5.0), and **X and Z must be exactly 0** for a stair
socket. A door helper offsets sockets 0.5u off the wall face; the stair socket inherited that offset, which
bought nothing (a floor hole has no wall to stand off from) and **broke** the composer's invariant that
`cell = [round(x), round(y), round(z)]` is a lossless round-trip. Each stairwell injected a half unit that
`RoundToInt` quantised into a **full unit of drift**, accumulating down a descent until rooms that should touch
sat 1u too close and **the bake aborted on overlap** — that was the original `dg_bonecrypt` / `dg_ember_deep`
failure.

**Any geometry added for traversal must not move the socket, and must not introduce fractional X/Z on it.**

### NavMesh bake settings (composed pipeline, `DungeonBaker.cs:286-290`)

```csharp
var surface = navHost.AddComponent<NavMeshSurface>();
surface.collectObjects = CollectObjects.All;
surface.useGeometry   = NavMeshCollectGeometry.PhysicsColliders;   // <-- COLLIDERS, not meshes
surface.BuildNavMesh();
```

**`PhysicsColliders`, not `RenderMeshes`.** So a ramp contributes to navmesh **only if it has a collider**. A
mesh with no collider is invisible to the bake — and conversely a collider with a hidden mesh still works,
which is the pattern the wall cladding already uses.

---

## 2. THE ASK — design the stairwell

### 2a. Ramp vs NavMeshLink — recommendation and reasoning

**Recommend a real walkable RAMP, not a link.**

| | Ramp | NavMeshLink |
|---|---|---|
| AI + player agree | ✅ same surface | ❌ agent teleports across the link |
| Reads as architecture | ✅ | ❌ invisible seam |
| Camera / LOS behave | ✅ | ❌ hero pops between floors |
| Cost | geometry per stair room | one component |

A link is the cheap fix and it will look like a bug the first time an enemy crosses it. The dungeons now have
ceilings, fog and torch pools; a teleport in the middle of that reads as broken, not as a shortcut.

### 2b. ★ THE METHOD — owner-specified, and it is the correct one ★

> **Owner, 2026-08-07:** *"you place stairs below and plane directly on top of that … and seam the edges if
> needed … important geometry to understand on stairs."*

**Visual stair steps below; an INVISIBLE ramp collider laid over them; seam the landings.** This is the standard
solution and it is right for a specific reason: **stepped geometry bakes a terrible navmesh.** Unity voxelises
each tread and riser separately, so an agent either refuses the surface (risers read as walls above step height)
or gets a jagged, snagging path. A single smooth collider over the top bakes one clean walkable strip, while the
player still sees real stairs.

**The ramp's RENDERER IS STRIPPED — collider only.** That is what makes it invisible, and it also settles the
`PrimitiveType.Plane` question: with no renderer, single-sidedness is irrelevant because nobody ever sees the
ramp. The project already has this exact pattern — `KayKitChallengeOutpostBuilder.HideMesh` destroys the
`MeshRenderer` + `MeshFilter` and keeps the collider, which is how the wall cladding stays nav-neutral.

**Still prefer a thin `Cube` over a `Plane`**, but now for a narrower reason: a Cube gives a **`BoxCollider`**,
which is cheaper and more numerically robust than the `MeshCollider` a Plane brings, and `NavMeshSurface` is
collecting **`PhysicsColliders`**. (A Plane is also 10×10 units at scale 1, which fights the 10 m cell and
invites a scale mistake.) An authored wedge mesh is also fine if it ships a convex collider.

### ⚠ THE GEOMETRY THAT ACTUALLY MATTERS — get these four right or it feels broken

1. **The ramp sits on the NOSE LINE.** Its walking surface must touch the **front-top edge of every step**, not
   the step midpoints and not the tread centres. Too low and the character's feet sink into the treads; too high
   and they visibly float. The nose line is the plane through all the leading top edges — for uniform steps it is
   exactly the constant-slope line from the bottom nose to the top nose.
2. **Overlap both landings.** The ramp must extend slightly ONTO the upper and lower floor surfaces, not butt
   against them. NavMesh needs continuous overlapping walkable surface to connect regions; a hairline gap at a
   landing produces two disconnected islands and a `PathPartial` that looks exactly like today's bug. **This is
   the "seam the edges" part and it is not cosmetic — it is the connection.**
3. **Strip colliders from the visual steps**, or keep them and accept they are redundant. What must NOT happen is
   the stepped colliders competing with the ramp: two surfaces at slightly different heights make the agent pick
   one and the character render on the other. Recommend: **steps are visual only (no collider), the ramp is the
   only collider.** That mirrors how the ceiling tiles are already handled (`StripColliders`).
4. **Cut the hole.** The ramp has to pass through the lower room's **ceiling** and the upper room's **floor
   slab**. Both are currently solid. A ramp that ends at a ceiling is a ramp into a wall.

### ★★ 2b-zero. THE SHAPE OF THE SOLUTION — a STAIR PREFAB, built once, instantiated everywhere ★★

> **Owner, 2026-08-07:** *"you can even better create a prefab that is stairs … and just call the prefab."*

**Take this as the architecture.** Do not compute stair geometry during the dungeon bake. Build **one stair
prefab** (a `Stair_Down` / `Stair_Up` pair, or one prefab flipped), verify it once, and have the composer simply
**instantiate it at the stair socket.**

**This is already the project's own pattern.** `DefaultDungeonRoomsBuilder` builds 17 room prefabs into
`Assets/Dungeon/Rooms/`, and `GraphDungeonComposer` / `DungeonBaker` just instantiate them. Stairs should sit in
exactly that pipeline, not in a special path.

Why it is strictly better than measuring per bake:

| | Prefab | Compute-at-bake |
|---|---|---|
| When the geometry maths runs | **once**, at prefab build | every bake, every dungeon |
| Can a human open and inspect it? | **yes** | no |
| Can an oracle read the SHIPPED artifact? | **yes** — the way `[room-shell]` already reads the room prefabs | only the source that generates it |
| Ramp ↔ nose-line alignment | **frozen and provable** | recomputed, can drift |
| Cost of a wrong number | fix the prefab, re-run one builder | every dungeon silently wrong until someone looks |

**What the prefab contains, pre-assembled:**
- the **visual step geometry** (renderer on, **collider off** — see §2b item 3)
- the **invisible ramp**: a thin Cube on the nose line, **`MeshRenderer` + `MeshFilter` destroyed, `BoxCollider`
  kept** (`HideMesh` pattern), extending slightly **onto both landings** so navmesh regions connect
- **rise exactly `DungeonBakerChecks.FloorSeparationY` (6.0)**, so the composer never scales it
- optional side walls / railing, and the ceiling/floor-slab **cut-outs** the shaft needs

**Then the bake is one line:** instantiate at the stair socket, yaw to the socket's travel direction. No maths,
no per-asset offsets, no guessing.

### The VARIANT FAMILY — straight / left / right (owner, 2026-08-07)

> *"you can create stairs left stairs right or stairs vertical"*

**Build a small stair KIT, the same way the room kit is a family of 17.** Three variants, and the reason is
geometric rather than decorative:

| Variant | Shape | Rise ÷ run | Footprint | Why it exists |
|---|---|---|---|---|
| **Straight** (`vertical`) | one flight along the run axis | 6.0 m over **10.0 m** = **31°** | consumes a **full 1×1 cell** end to end | simplest; but it eats the entire room and forces the descent to continue in one direction |
| **Left** | half-flight → landing → quarter-turn left → half-flight | two 3.0 m rises over **5.0 m** each = **31°** | ~**5×5 m + landing** — fits a 1×1 with room to spare | same slope, **half the linear footprint**, and the exit heading turns 90° |
| **Right** | mirror of Left | identical | identical | lets a descent turn either way |

**The footprint is the real argument.** A straight flight needs the whole 10 m cell for a 6 m rise at a sane
angle. Splitting into two 3 m half-flights around a landing gives the **same 31° slope in half the run**, which
leaves space in the stair room for anything else — and a landing is a natural place to put the ceiling/floor
cut-out.

**The turn variants also matter to the COMPOSER, not just the art.** With only a straight stair, every descent
continues along one heading — descents get repetitive and are far more likely to collide with already-placed
rooms (which is the class of failure that produced the original `dg_bonecrypt` / `dg_ember_deep` overlap
aborts). Left/right give the solver somewhere to go.

**Left and Right must be genuinely mirrored, not one rotated 180°.** A rotated left-turn is still a left turn;
it just faces the other way. Mirroring flips the handedness — and if the art has any asymmetry (railing on one
side, a wall torch), mirroring must not leave it inside geometry.

**Each variant is still ONE prefab with the frozen contract above** — visual steps (no collider), hidden ramp
collider on the nose line, rise exactly `FloorSeparationY`, landings overlapped. The `[stair-shell]` oracle
should run over **every** variant, not just the straight one, or the two that get used least are the two that rot.

**And it gets an oracle for free.** `RoomForgeRegression` case 11 `[room-shell]` already reads the *shipped*
room prefabs and asserts cell, floor span, wall height and ceiling presence. A `[stair-shell]` case in the same
shape should read the shipped stair prefab and assert: rise == `FloorSeparationY`; a collider-bearing ramp
exists; that ramp has **no renderer**; its slope is under the agent max; and it **overlaps both landings**. That
is the difference between "we think stairs connect" and "the build fails if they ever stop".

The algorithm below is then **how you BUILD the prefab**, run once — not something the dungeon bake repeats.

### ★ 2b-bis. THE SEATING ALGORITHM — owner's rule, and it removes the guessing entirely ★

> **Owner, 2026-08-07:** *"when visualizing stairs the width does not change — tells you one position. The
> lowest vertical is where you connect from (if going up) and highest is top. That logic allows AI to determine
> how stairs should seat and not guess."*

**This is the important one. Write the builder to MEASURE a stair asset and derive its seating, rather than
carrying a hardcoded offset per asset.** The same discipline that fixed the torches today: their glTF `POSITION`
bounds told us `torch_mounted` was a wall bracket and the other two were floor-standing, which no amount of
reading filenames would have.

A staircase has exactly one invariant and two anchors, and all three are readable from the mesh:

| Property | How you find it | What it gives you |
|---|---|---|
| **WIDTH axis** | the horizontal axis along which the top surface height **does not change** | the lateral axis — fixes orientation |
| **RUN axis** | the other horizontal axis: top surface height **climbs monotonically** along it | the travel direction |
| **BOTTOM anchor** | `bounds.min.y`, at the run-axis end where height is lowest | where it connects to the LOWER floor |
| **TOP anchor** | `bounds.max.y`, at the opposite run-axis end | where it connects to the UPPER floor |

**The test is computable, not visual.** Sample the mesh's upper surface at several points along each horizontal
axis. Along the **width** axis the sampled height is constant; along the **run** axis it increases. That single
comparison resolves orientation for *any* stair asset, including ones nobody has imported yet.

**Then seating is arithmetic, with no free parameters:**
```
rise            = bounds.size.y
requiredRise    = DungeonBakerChecks.FloorSeparationY        // 6.0
seatY           = lowerFloorWalkSurfaceY - bounds.min.y      // bottom nose lands on the lower floor
yaw             = align RUN axis to the socket's travel direction
```
- If `rise == requiredRise` → one flight, seat and done.
- If `rise < requiredRise` → **tile flights with landings**, do not scale. Scaling a stair distorts tread depth
  and riser height together, and the moment treads stop matching stride the stairs read as toy-sized or
  monumental. Tiling preserves the authored proportions.
- If `rise > requiredRise` → wrong asset for a 6 m floor gap, or `FloorSeparationY` needs a ruling. **Fail loudly
  and name both numbers** — do not silently squash it.

**Log what was measured, every bake.** `DefaultDungeonRoomsBuilder` already does this for the room kit
(`KIT MEASURED wall=4.00L x 4.00H …`) and `KayKitChallengeOutpostBuilder` for the outpost. A stair pass must
print its measured width axis, run axis, rise, and chosen flight count, so a wrong seat is a readable line rather
than a screenshot argument.

**This also gives the ramp for free.** Once the run axis and both anchors are known, the invisible ramp is the
segment from the bottom nose to the top nose along the run axis, at the constant width — i.e. the nose line
(§2b item 1) is *derived*, not authored.

### 2c. The slope maths

Rise is fixed at **6.0 m** (`FloorSeparationY`). Unity's default agent max slope is **45°**.

| Horizontal run | Angle | Verdict |
|---|---|---|
| 6.0 m | 45.0° | at the limit — no margin, do not |
| 8.0 m | 36.9° | workable |
| **10.0 m (one full cell)** | **31.0°** | **recommended** |
| 14.1 m (cell diagonal) | 23.0° | gentle, needs a 2×2 or a switchback |

⚠ **Verify the real agent radius and max slope rather than assuming Unity's defaults** — read the actual
`NavMeshBuildSettings` the surface uses. A default-looking number that was changed once is how this whole class
of bug survives.

⚠ **Head clearance is the hard constraint, not the slope.** A floor occupies 4.4 m and floors are 6.0 m apart,
so there is only **1.6 m** of dead space. A ramp climbing through the ceiling of the lower room needs a hole in
that ceiling *and* enough headroom along its length, or the agent walks up into a ceiling slab. **Solve the
clearance before the angle.** Options: a 2×2 stair room, a switchback, or an open stairwell shaft that cuts the
ceiling and floor slabs.

### 2d. What "done" looks like — the acceptance is already instrumented

`DungeonBaker` already prints a path result per dungeon. **All five must read `PathComplete`:**

```
[Flow:DungeonBake] SUMMARY id=dg_bonecrypt ... path[entry->necromancer_keep]=PathComplete
```

Plus: `COMPILE_GATE_OK`, `REGRESSION_OK n/n suites`, and a headless capture
(`DeNelle.Editor.DungeonSceneCapture.CaptureAll`, marker `DUNGEON_CAPTURE_OK`) opened and eyeballed. **Do not
loosen the path assertion to make it pass.**

---

## 3. THE OTHER OPEN DEFECTS — concrete instances, all seen in captures tonight

These are separate from the nav problem and each needs an owner. Listed with the evidence.

### 3.1 The legendary gate is a neon slab — NEW, created by tonight's relight
`ComposedLegendaryGate`'s "Sheet" uses **`Universal Render Pipeline/Unlit`**, so it ignores the new ambient
(0.05) entirely and renders full-bright. It blended into the old daylight greybox; against a properly dark
dungeon it is **the brightest thing on screen and occupies a third of the frame** in
`dg_bonecrypt_eye.png` and `dg_sunken_vault_eye.png`. It is *supposed* to be a locked-gate tell, so the fix is
to re-shade it as a lit emissive that reads as a magical seal — not to delete it.

### 3.2 EXIT labels render through walls
`dg_bonecrypt_eye.png` shows **three "EXIT" labels at once at three different sizes**, one of them on a flat
wall with no door behind it. These are world-space labels from `DungeonExitInteractable`'s `Beacon_Label`
(builds at `DungeonExitInteractable.cs:245`). They appear to draw with **no depth test**, so every exit in the
dungeon shows through the geometry. Reads as a HUD bug, breaks the enclosure illusion, and makes wayfinding
meaningless.

### 3.3 Green colour cast on stone and props — UNDIAGNOSED
Walls and crates read **green** in `dg_bonecrypt` and `dg_sunken_vault`. The same crate model rendered
orange/grey in `dg_starter_loop`. Fog is `#0a0a10` (near-black blue), so it is not fog. **I do not have the
cause and will not guess it.** Candidates worth checking: the unlit gate dominating exposure/auto-exposure, a
per-dungeon material tint, or a light colour applied per theme.

### 3.4 Camera background is still Unity default blue
`HeroControlEnsurer.cs:283-290` creates `"GameplayCamera (ensured)"` and sets **neither `clearFlags` nor
`backgroundColor`**. With `RenderSettings.skybox = null`, `CameraClearFlags.Skybox` falls back to clearing with
`backgroundColor`, whose Unity default is **`#314D79` blue**. Any hairline in the shell will read as sky.
**Two independent agents proposed the identical one-line fix today and neither owned the file:**
```csharp
cam.clearFlags = CameraClearFlags.SolidColor;
cam.backgroundColor = new Color(0.027f, 0.027f, 0.035f);   // DungeonSceneBuilder.cs:2067
```
Must be keyed off the scene so overworld scenes are untouched.

### 3.5 `Env_Candle` and `PP_GroundFog` are built, pooled, shipped — and never played
`Env_Candle` has **exactly one reference in the entire codebase: its own enum declaration**
(`VFXType.cs:227`). It is generated, mirrored, pooled (`isLoop:true, poolSize:6`) and ships in the APK.
Tonight both pipelines seated **`CandleAnchor` / `GroundFogAnchor` empties** at the measured flame tips
(25 + 24 in the outpost alone), deliberately stopping short of playing them, because:
- `VfxEmitter` — the type WO-1004 §1.3 and WO-1000 §2.3 both name — **does not exist anywhere in the tree.**
- `VFXManager` lives in `DeNelle.Village`; `DeNelle.Editor` cannot reference it, so a *baker* cannot spawn them.
- ~44 looping instances in one dungeon would blow VFXManager's **20-slot global loop budget** — the exact
  failure `HarvestAura` was written to prevent.

**The open seam:** a runtime `MonoBehaviour` in `DeNelle.Dungeons`, armed by `ComposedDungeonBootstrap.TryArm`
(which already walks the `DungeonCompose_*` root and arms the Lantern + AmbushDirector), collecting anchors and
playing on the **nearest N to the hero** with the same static arbiter shape as `HarvestAura`. **Needs a WO.**

### 3.6 Outpost: floor z-fights the nav slab *(in progress)*
`MakeFloor` puts the slab top face at **y = 0.0**; `BuildFloorTiles` puts the tile top face at **y = 0.0**.
Coplanar. Walls avoid this via `clad: true` → `HideMesh` (collider kept, mesh destroyed); the floor passes
`clad: false`. Visible as a pale blue wash with brown texture bleeding through.

### 3.7 Outpost: `NAV_FAIL`, centre unreachable *(in progress)*
`NAV_PROBE_FAIL entry->center (0,0,0) status=PathPartial`; all four quadrant probes pass.
**Not a regression** — `VerifyNav` did not exist before tonight (grep count 0 in the prior commit) and
`BuildChoke` centres/dimensions are byte-identical. The check revealed a latent defect.
Working hypothesis (**inference, being measured**): `Choke_SouthMid` occupies `z −10.675..−10.325` and the inner
ring wall occupies `z −9.6..−8.4`, leaving a **0.725 m corridor** — likely under the agent diameter.

### 3.8 Corner towers have never existed
`DressCornerTowers` calls `FindKay("tower")`. **No file in the KayKit pack contains "tower"** (all ~200 names
enumerated). Every corner has been silently empty since it was written — one warning, then skipped. WO-1000
§2.2 says "keep the corner KayKit towers", which are not there to keep. **Needs a creative decision:** pick a
different asset, or drop the concept.

---

## 4. TRAPS — these each cost real cycles today. Do not re-pay them.

1. **Torch models are not interchangeable.** Measured glTF bounds: `torch_mounted` has a back plate at z=0 and
   projects forward (a **wall bracket**); `torch_lit` and `torch` are radially symmetric (**floor-standing**).
   Both pipelines were picking **randomly** and seating floor torches at wall height — floating in mid-air. One
   of the three is also **unlit**.
2. **Never stretch the KayKit atlas over a primitive cube.** `dungeon_texture.png` is a grid of solid-colour
   swatches; a cube maps the full 0..1 UV per face, so it renders as rainbow stripes. Use the kit's own modular
   pieces at authored scale, or flat untextured stone.
3. **Directional shadows must be OFF now that there are ceilings** — with shadows on, the ceiling occludes the
   directional completely and rooms fall to near-black.
4. **Do not invent a type.** Three WOs today referenced `VfxEmitter`, which exists only inside WO documents.
   **Grep before you call.**
5. **Do not re-type a constant that has an authority.** Copy-drift caused several defects today. `RoomForgeCanon`
   exists for exactly this.
6. **Verify WO premises.** Three separate WO claims were stale tonight: ceilings "missing" (they were baked),
   skybox "not overridden" (it was), "keep the corner towers" (they never existed).
7. **A number written for `Cell = 6` is probably wrong at `Cell = 10`.** WO-921 specified torch range 4–5 m,
   written when a corner sat 3.54 m from centre. At 10 m cells the corner is 6.36 m out, so the same *intent*
   scales to ~7.6 m. A literal 4–5 m would leave the middle of every room — where the fight is — unlit.

---

## 5. WHAT NOT TO TOUCH

- `Assets/_Modules/Dungeons/RoomForge/RoomForgeCanon.cs` values, without updating every oracle that reads them.
- The stair socket's **X/Z = 0** and its **half-floor Y offset** — see §1's grid invariant.
- `DungeonBakerChecks.SealSocket`'s seal height — it was just fixed (it built a 2.5 m cube centred on a
  floor-level socket, which at 4 m walls left a 2.75 m letterbox of open sky at every dead end).
- The dresser's KayKit props' own materials — they legitimately use the atlas with authored UVs. Running
  `ApplyToRoomRoot` over a dressed room would bulldoze barrels and crates to grey stone.

---

## 6. DELIVERABLE ASKED OF GROK

1. A **stairwell design** that connects floors: ramp geometry (or switchback / shaft), solving **head clearance
   inside the 1.6 m dead space first**, then the slope. Say which room archetypes change and whether a 2×2 stair
   room is needed.
2. A **creative ruling** on §3.1 (what a locked legendary gate should look like when it can no longer be a flat
   unlit slab) and §3.8 (corner towers: substitute asset or drop).
3. A **spec** for the runtime candle/fog play-site seam in §3.5 — nearest-N, pooled, budget-aware.

Return as a WO draft. UI refines, CLI implements and gates.
