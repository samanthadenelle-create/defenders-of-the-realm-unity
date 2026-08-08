# Design — the connector is the only contract

**Owner, 2026-08-07, in three statements:**
> *"couldn't we have steps start and end in a single room … create a room which is simply the steps and the plane auto connector from floor A to floor B … ends in the middle of that room with four doors."*
> *"make the connector, as far as each prefab room is concerned, the door."*
> *"whether it connects to stairs, a corridor or treasure rooms — don't care. They snap to connectors."*

**Status:** design · **For:** UI/Grok to turn into a WO · **Supersedes the pair model, does not patch it**

---

## 0. The principle

**A room exposes connectors. It does not know, and must not care, what is on the other side.**

Whether the space beyond a north door is a corridor, a vault, or a stairwell that drops a floor is the
*neighbour's* business. The room's contract ends at the connector.

Everything below follows from that one sentence.

---

## 1. What it deletes — measured, not estimated

### 1.1 Half the socket enum

`RoomSocketType` today:

```csharp
Door = 0,       // standard door
Arch = 1,       // open passage, no door mesh
StairUp = 2,    // vertical connect up   (mates with StairDown)
StairDown = 3,  // vertical connect down (mates with StairUp)
```

**`StairUp` and `StairDown` cease to exist.** What remains — `Door` vs `Arch` — is a *cosmetic* distinction
(is there a door mesh) and not a topological one. For mating purposes there is exactly **one connector type**,
which is the owner's principle expressed in code.

### 1.2 Four special cases that only exist to support vertical sockets

| Special case | Where | Why it goes |
|---|---|---|
| `IsVertical(RoomSocketType)` | `DungeonBakerChecks` | nothing is vertical any more |
| `SEALED_VERTICAL` seal branch | `SealSocket` | an unmated connector is an unmated connector |
| `yaw = 0f` vertical fork | `GraphDungeonComposer.SolveMate:563` | one yaw rule for every mate |
| 3D nudge for stair pairs | `DungeonBakerChecks` | one nudge rule for every mate |

### 1.3 The whole `_Down` / `_Up` pair model

No ownership question ("which room owns the flight"), no interpenetration risk, no floor-hole-versus-
ceiling-shaft alignment **between two prefabs that can drift apart**, and no mate arithmetic to get wrong.
The stairwell is one object, so its geometry and its openings cannot disagree.

### 1.4 The 180° rotation bug — deleted, not fixed

The owner hit this walking the rig: `v_up`'s assembly needed `Y += 180`. **That bug exists because the flight
has a fixed authored orientation.** Under *"the steps always connect at the top facing the way of the connector
being used,"* the stair aims at whichever connector is in play — so there is no fixed yaw left to be backwards.

This is the deepest part of the idea. It does not correct the symptom; it removes the thing that can be wrong.

---

## 2. Why it works with machinery already in the tree

**A connector at a non-zero height already works.** `AddStairSocket` places sockets at local `y = ±3` today, and
`GraphDungeonComposer.SolveMate` solves position as `pos = pPos - rotatedSocket` — which resolves Y for free.
That is precisely how the current stair pair lands exactly `FloorSeparationY` apart.

So a stairwell room with:
- a **top** connector at local `y = 0`, facing horizontally, and
- a **bottom** connector at local `y = −FloorSeparationY`, facing horizontally

mates a floor-A room and a floor-B room using **the ordinary door path**, with no new code and no vertical
concept anywhere. The elevation change is carried by the stairwell's own geometry, which is exactly where the
owner put it.

**The graph gets simpler too:** three edges per descent become two, and the graph stops needing to know that
floors exist.

```
BEFORE  flat_A --door--> stair_down --VERTICAL--> stair_up --door--> flat_B
AFTER   flat_A --door--> stairwell --door--> flat_B
```

---

## 3. The two real costs — both tractable, neither hidden

### 3.1 `RoomsOverlap` must understand vertical EXTENT, not a single Y

`DungeonBakerChecks.RoomsOverlap:190`:

```csharp
if (Mathf.Abs(aPos.y - bPos.y) > FloorSeparationY * 0.5f) return false;
```

Rooms on different floors never overlap — correct for single-storey rooms, and it is what stopped a correct
vertical stack being fail-gated as an overlap.

**A stairwell spans two layers.** Its lower half sits at the floor-B level, and against a floor-B room this test
short-circuits to "no overlap" and never checks. So a stairwell could be placed straight through an existing
room and nothing would catch it.

**Fix:** give `RoomPrefabMeta` a vertical extent (default: one floor) and test overlap as an interval on Y
rather than a single plane. Only the stairwell is ever non-default, so the change is small and the default
preserves today's behaviour exactly.

### 3.2 `[room-shell]` assumes one floor and one ceiling per room

`RoomForgeRegression` case 11 resolves **one** child by exact name and asserts its `localScale` covers the
footprint. A two-storey stairwell has neither in that shape.

**This oracle already needs rework for the shaft** (it is the reason 6 failures are outstanding right now), so
this is the same job, not an extra one. Do it once, for the right shape:

> Declare the opening on `RoomPrefabMeta` (e.g. `verticalShaft : Rect`) and assert coverage of
> **footprint minus declared shaft**, *plus* that the shaft is genuinely **open**. Union-bounds alone would let
> a perimeter-ring ceiling pass — which is the exact bug found on 2026-08-07, where `Ceil_N/S/E/W` formed a ring
> with a permanently open centre and every stairwell was open to sky.

---

## 3.3 ★ CHECK COLLISIONS BEFORE PLACING, not after (owner, 2026-08-07) ★

> *"we could easily check for collisions before placing each component"*

**Verified at source: the composer has NO collision awareness at all.** `GraphDungeonComposer` calls
`SolveMate(pSock, cSock, childGo)` (`:428`), which sets the child's transform and moves on. Every overlap test
lives downstream in `DungeonBakerChecks.Compose`, where the only available response is to **abort the whole
bake**. That is generate-then-validate, and it is why `dg_bonecrypt` and `dg_ember_deep` died wholesale on the
socket-drift bug instead of reporting one bad room.

### Why this idea and §1's connector model are the same idea

A pre-check only helps if there is something to **do** when it fails. Today `SolveMate` derives exactly one
position from the socket pair — there is no choice, so a pre-check could refuse but never resolve.

**The generic connector model is what creates the choice.** Once every room is interchangeable on any
connector, a blocked placement has alternatives: a different shape, a different socket on the parent, a
different traversal order. Collision-aware placement turns a hard abort into a **solver**. Without the
connector model there is nothing to try; with it, there is a search space.

So these are not two features. §1 makes §3.3 useful, and §3.3 is what makes §1 safe to place freely.

### Two payoffs that land before any backtracking exists

1. **Diagnostics.** *"room `stair_dn_1` cannot be placed at `[0,-6,20]` — collides with `corr3`"* names the
   offending **edge**. The current abort prints a pair list and kills the run.
2. **It absorbs §3.1 entirely.** The vertical-extent gap (a two-storey stairwell's lower half never being
   checked, because `RoomsOverlap` short-circuits on floor difference) simply cannot exist if the candidate's
   real occupied **volume** is tested before placement. The check stops depending on an assumption about how
   tall a room is.

### ★ The pattern already exists — town build mode (owner: *"a validator like with builder … enforces you work as allowed"*) ★

`Assets/_Modules/Village/BuildMode/PlacementGrid.cs` is the proven shape. **Copy it; do not invent one.**

```csharp
public bool   CanPlace(Vector2Int cell, Vector2Int footprint)   // bounds + occupancy, PURE
public string OccupantAt(Vector2Int cell)                       // NAMES the blocker
public bool   InBounds(...)                                     // separate, so "outside the area" != "no space"
```

**Three properties worth stealing verbatim:**

1. **It is PURE.** No side effects, no placement — ask, then act. That is what lets it be called speculatively
   while a player drags a building, and it is exactly what a composer needs to try a candidate before committing.
2. **It NAMES the blocker.** `OccupantAt`'s own comment records why it exists: an F8 *"anonymous Occupied storm"*
   — the gate could say no but not say what. **A validator that only says no is a validator you end up
   debugging.** The dungeon composer should inherit this lesson rather than re-learn it.
3. **It separates the REASONS.** Out-of-bounds and occupied are different answers, because they lead to
   different fixes. A dungeon needs at least: out-of-extent · overlaps room X · violates rule Y.

### Rules should be DATA (owner: *"could have rules dynamic"*)

Occupancy is one constraint. Once placement is gated, the gate is the natural home for **every** placement
rule, and those should be authored rather than compiled:

- no reward room within N rooms of the entry
- at most one stairwell per floor per wing
- the boss room is the furthest node from the entry
- no two lore shrines adjacent
- a stairwell may not open directly onto another stairwell

**None of these are expressible today** — `LintPacing` can only *report* pacing after the fact, exactly the
same generate-then-validate shape as the overlap check. Moving them into a data-driven placement gate turns
"the dungeon came out badly, here is a warning" into "the dungeon cannot come out that way."

Keep the rule set in the graph's existing `rules` block (`dg_*.json` already carries one), so a dungeon can
declare its own constraints without a code change.

### Keep the downstream check

Move the test **earlier**; do not delete the late one. `DungeonBakerChecks.Compose` is the hard gate that has
caught real drift, and a solver that believes its own placement is a solver with no independent verifier. Cheap
to keep, and it is what proves the pre-check is working rather than merely running.

---

## 4. How it composes with the four-door / mask idea

These are the same design, not two.

- **Every room is a shell with four connectors** and a mask saying which are open (`DESIGN_GENERIC_DUNGEON_MAPPER.md`).
- **A stairwell is that same shell**, with connectors at two heights and stairs inside it.
- **Eleven of seventeen kit rooms** are already the same 1×1 shell differing only in door count, and `SealSocket`
  already closes unmated connectors on every bake — the mask exists, implicitly.

So the end state is: **one shell generator, one connector type, one mate rule.** Room *identity* (combat, lore,
reward, stairwell) becomes what is *inside* the shell and what the graph node says — never a different prefab
family and never a different socket type.

⚠ **The prerequisite from that doc still binds:** archetype currently rides on the *prefab name*, and
`DungeonBaker.LintPacing` reads it. Move archetype to the graph node **before** collapsing prefabs, or the
pacing linter goes green on a dungeon that is 100% combat.

---

## 5. Recommendation — replace, do not retrofit

**Do not bolt this onto the `_Down`/`_Up` pair.** Retrofitting means maintaining two mate models simultaneously,
and the pair model only exists because the vertical socket type came first. Build the single-room stairwell as
the replacement and retire `StairUp`/`StairDown` in the same change.

Suggested order, each step independently shippable:

1. **`RoomPrefabMeta` gains vertical extent** (§3.1). Default = one floor. No behaviour change; unblocks the rest.
2. **Rework `[room-shell]`** to declared-shaft coverage (§3.2). Turns today's 6 red into a real assertion instead
   of a naming argument.
3. **Build the single-room stairwell** with two horizontal connectors and stairs that orient to the connector in
   use. Test it in `dg_stair_rig` — the fixture already exists.
4. **Retire `StairUp`/`StairDown`**, `IsVertical`, `SEALED_VERTICAL`, the `yaw = 0f` fork and the 3D nudge.
5. **Then** the four-door mask and the prefab collapse, archetype having moved first.

---

## 6. What this does NOT change

- `RoomForgeCanon` metrics. `Cell = 10`, `WallHeight = 4`, `FloorSeparationY = 6` are unaffected; a stairwell
  still rises 6 m and still has only **1.6 m** of dead space to fit a landing into. **Head clearance stays the
  binding constraint, not slope.**
- Steps visual-only / ramp collider-only. `NavMeshSurface` collects `PhysicsColliders`, so that split is what
  makes the stair walkable at all, and it is orthogonal to this.
- The `Descend`/`Climb` teleport ports stay as the fallback until a bake reports `PathComplete`. **Removing the
  only working traversal before its replacement is proven is how a dungeon becomes unplayable.**
