# WORK ORDER 1001 — RESULT (PARTIAL: Phase 1, slice 1 only)

**Date:** 2026-08-07 (overnight run) · **Seat:** CLI · **Status:** slice 1 LANDED + PROVEN; slices 2–7 NOT started
**Gates:** `COMPILE_GATE_OK` · `REGRESSION_OK 124/124 suites` (was 123 — `[dungeon-multilevel]` is the new one)
**Commits:** `6e2ceb1b` (the contract + suite), the descent-probe bake, the version/mirror fix

---

## 0. The headline

**A composed dungeon can now descend floors. Before tonight it could not — at all.**

The WO's own premise was wrong, and that mattered: §1 said Pipeline A "supports multi-level via
`StairDown`/`StairUp` sockets", so slice 1 was written as *"confirm the composer solves vertical
socket mates"*. It didn't solve them. It couldn't. That correction is now banner-flagged at the top
of the WO so nobody re-plans against it.

---

## 1. Why it was impossible — three independent blockers, all green to every oracle

Read at source, not inferred:

1. **Both stair sockets pointed DOWN, at local Y = 0.** `DefaultDungeonRoomsBuilder.AddStairSocket`
   applied `LookRotation(Vector3.down)` and `localPosition (0, 0, 0.5)` for *both* `StairUp` and
   `StairDown` — the `stairType` only picked the id string. The mate test is
   `dot(a.Outward, -b.Outward) >= 0.25`, so a pair scored **−1**. They could never mate, and carried
   no height to mate *across*.
2. **`TryMate`'s corrective nudge was planar-only** (`new Vector3(delta.x, 0f, delta.z)`), so a Y gap
   could never close and reported `MateFailReason.Distance` forever.
3. **`RoomsOverlap` was XZ-only.** A *correctly* stacked pair — whose footprints coincide completely
   by design — was reported as an overlap, and any overlap is a hard bake abort (no navmesh, no
   scene, no Build Settings).

Corroborating: no graph JSON in the tree referenced a stair socket in any edge, every room placement
in every layout had `cell[1] == 0`, and `RoomForgeRegression` had no vertical case at all.

**What was already fine** (so it was left alone): the composer's *position* solve
(`pos = pPos - rotatedSocket`) is full 3D and always was; the emit already rounds `p.y` into
`cell[1]`; the baker already applies `cy * cellSize` to world Y. The data path carried Y end to end.
Only the solver never produced it and the verifier forbade it.

---

## 2. What changed

| File | Change |
|---|---|
| `DefaultDungeonRoomsBuilder.cs` | `StairDown` leads **down**, `StairUp` leads **up**; each socket sits **half a floor** off its room origin, so mating the pair drops the child exactly one floor. Explicit up-vectors (bare `LookRotation(up)` is degenerate). |
| `DungeonBakerChecks.cs` | New `FloorSeparationY = 6f` + `IsVertical(type)`. Stair pairs take the **full 3D** nudge; **doors deliberately keep the planar slide**. `RoomsOverlap` now returns false for rooms more than half a floor apart in Y. |
| `GraphDungeonComposer.cs` | The vertical branch is now a documented, `FlowTrace.Step`-traced design path instead of a `Warn` about an anomaly. New `ComposeDescentProbeBatch` entry point. |
| `DungeonComposeLayout.cs` | Emits a top-level `"version"` — see §5. |

**Design note — why the height lives in the sockets.** Splitting `FloorSeparationY` across the two
sockets (−3 / +3) means the existing position solve produces the floor drop for free. No `level` or
`elevation` field was added to the graph schema, so authoring a descent stays "one node + one edge",
which is the whole reason Pipeline A was chosen over hand-coded builders.

**Why doors keep the planar-only nudge.** If the 3D nudge applied to doors, a room authored at the
wrong height would be silently *lifted into place* instead of failing the bake. The existing
`RoomForgeRegression` case that pins this (a door pair with a Y gap must fail on distance) still
passes, and `DungeonMultiLevelRegression` case 4 pins it from the other side.

---

## 3. The proof

`dg_descent_probe` — the smallest graph that descends a floor. From the bake log:

```
mate OK conn=stair_down.stair_down_01::stair_up.stair_up_01 dist=0.00 align=1.00 nudge=0.00
SUMMARY id=dg_descent_probe rooms=5 matesOk=4 matesFail=0 sealed=1 saved=True
```

Emitted layout — **two distinct Y levels**:

| room | prefab | cell |
|---|---|---|
| entry | EntryHall | `[0, 0, 0]` |
| corr1 | Straight | `[0, 0, 6]` |
| stair_down | StairDown | `[0, 0, 12]` |
| **stair_up** | StairUp | **`[0, -6, 12]`** |
| **deep_vault** | RewardVault | **`[0, -6, 6]`** |

Note `deep_vault (0,-6,6)` sits **directly beneath** `corr1 (0,0,6)` — the stacked-footprint case
that used to abort the whole bake.

The scene is registered in Build Settings **DISABLED**: it is a diagnostic, not content, and the
baker enables new scenes by default.

---

## 4. ⚠ THE HONEST LIMIT — placed, not yet walkable

The same log line says `path[entry->deep_vault]=PathPartial`. **The floors are two disconnected
navmesh islands.**

`DefaultDungeonRoomsBuilder.BuildOne` emits only Floor, perimeter walls, optional choke, sockets and
`Anchor_Center` — verified in the built prefabs. `StairDown`/`StairUp` are ordinary flat 6×6
dead-end rooms with a socket marker: **no staircase mesh, no hole in the floor, no navmesh link.**

So slice 1 delivers the *placement primitive* — the thing that was blocking everything — and not a
playable descent. Tracked as **slice 1b** (task #37): stair geometry, a floor cut where the shaft
lands, a `NavMeshLink` across floors, and a ruling on whether descending is walk-through or a
triggered transition. **Until 1b, do not seat a hero in a multi-level composed dungeon** — it would
look enterable and dead-end. That is why the probe leaves `populateForPlay: false`.

---

## 5. Two standing rules the probe tripped (caught by the oracle, not by me)

Worth recording because both were *reasoned past* and both were right:

1. **Every canonical catalog needs a top-level `"version"`.** The composer never emitted one —
   `dg_starter_loop`'s layout has `version: 1` only because it was hand-added after composing. That
   stays invisible until someone composes a **new** dungeon. `DungeonComposeLayout` now emits it, so
   future composes are born correct.
2. **Canonical subdirectory catalogs need a Resources mirror** or they read null on WebGL. I judged
   the probe "a dev artifact, no mirror needed"; `[data-web]` applies the rule to every canonical
   catalog and is right to. Both files are now byte-identical mirrors (SHA256-verified).

---

## 6. New regression: `DungeonMultiLevelRegression` — `[dungeon-multilevel]`, 5/5

Standalone marker `DUNGEON_MULTILEVEL_OK`. Each blocker in §1 gets a case, because any one of them
reverting silently re-breaks descents while every other suite stays green.

1. `[oppose]` — a stacked StairDown/StairUp pair mates; **the old both-down pose still fails**
   (so the fix isn't "the alignment gate got loosened"); type-compat and `IsVertical` pinned.
2. `[floor-drop]` — the solve lands exactly `−FloorSeparationY` with no XZ drift, and the separation
   clears the 2.8u walls.
3. `[stack-not-overlap]` — stacked ⇒ not an overlap; same-position ⇒ still an overlap; a 0.4u jitter
   is **not** a floor change; adjacent rooms still share a wall.
4. `[door-planar]` — a door pair with a Y gap must still fail, and must not be moved in Y.
5. `[prefab-poses]` — reads the **shipped** `StairDown.prefab` / `StairUp.prefab`. The room prefabs
   are *generated*, so a builder edit is inert until `DefaultDungeonRoomsBuilder.BuildAll` re-runs.
   This case is what makes a forgotten rebuild fail loudly instead of looking fixed. Its failure
   message names the menu item to run.

*(The rebuild changed only the two stair prefabs and the two catalog copies — which also
re-confirms the room builder is deterministic.)*

---

## 7. NOT done — slices 2–7, and one thing the WO assumed

Slices 2–7 are untouched. One finding reshapes them: **the composed dungeon has no
`DungeonController`, no `Lantern`, no `EncounterTrigger`, no `BreakableContainer` and no chests.**
`DungeonBaker.PopulateForPlay` seats a hero root and the enemy spawner markers and *nothing else*;
the lantern/oil pillar is hand-wired by `DungeonSceneBuilder` into the single Healer's Cottage scene.

So slices 3–6 (boss wiring, loot/chests, oil/lantern, the darkness consequence) are **not**
"extend the composer" — they each first require the composed path to gain a controller. That is a
shared prerequisite the WO does not name, and it should probably become its own slice before them.

Other verified gaps for whoever picks this up:
- `OutpostEnemyGroupSpawner.WeightedSkeletonId` is four hardcoded Hollow string literals, and
  `DefFor` hand-writes four `EnemyDef`s that **ignore `enemies.json` entirely** (and disagree with
  its numbers). `EncounterSpec.kind` is only ever compared to `"none"` — `"orc-group"` would
  silently spawn hollows.
- `EncounterSpec.seatMode`, `confine.mode`, `confine.returnHome` are read by **zero** code.
- `inDarkness` is hardcoded `false` at `EncounterTrigger.cs:285`, and the random-encounter path is
  unreachable anyway (`ConfigureRandom` has no callers).
- `enemies.json` has no `undead` or `beast` family — the undead are authored as `family: "hollow"`.
  Families present: `hollow`, `orc`, `troll`.
- `dungeon-deepboss` (the only legendary-component source) is reachable **only** via a chest
  authored `rewardKey: "rare-crafting-shard"`; nothing in code selects it.
