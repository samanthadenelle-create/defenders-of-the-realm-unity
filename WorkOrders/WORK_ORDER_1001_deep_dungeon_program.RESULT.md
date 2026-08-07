# WORK ORDER 1001 — RESULT (PARTIAL: Phase 1 slices 1–5 foundation)

**Date:** 2026-08-07 · **Seat:** CLI · **Status:** slices 1–2 + 1b + 3–5 foundation LANDED; slice 6–7 + Phase 2 NOT started  
**Follow-up (same day, owner: assume defaults + proceed):**  
- **1b** triggered stair ports (Descend/Climb) in `DungeonBaker` + `HeroLocomotion.WarpTo` on `DungeonPortLink`  
- **3** boss: `EncounterSpec.isBoss` / `enemyType` → spawner count=1 + MiniBoss role  
- **4** chests: `ComposeChest` → `BreakableContainer.Create` at bake  
- **5** oil: `oilStones` + `ComposedOilStone` + `Lantern.ConfigureStandalone` via `ComposedDungeonBootstrap`  
**Assumptions locked:** catalog difficulty, no productName rename, **triggered** stairs (not walk-through yet).  
**Re-bake required** for probe/starter to materialize ports/chests/oil in scenes.

**Earlier overnight:**  
**Gates:** `COMPILE_GATE_OK` · `REGRESSION_OK 125/125 suites`  
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

## 4b. A defect slice 1 introduced, found by re-reading the change

Moving the stair sockets half a floor off the room origin meant an **unmated** stair socket would
seal with a 2.4 × 2.5 wall slab **hanging three metres up in mid-air**. `dg_starter_loop` has
exactly that shape — its `StairUp`/`StairDown` rooms are attached by their horizontal `s_door_01`,
and their stair sockets appear in **no** edge — so the artifact would have shown up in the one
dungeon the owner actually plays, the next time anyone re-baked it.

The logic was wrong before the move too, just less visibly: a stair socket is a hole in the
**floor**, so a vertical slab is meaningless at any height. Unmated stair sockets now seal
invisibly (`SEALED_VERTICAL`). The bake trace also stopped printing a `WALL`/`SECRET` ternary that
would have called these `SECRET` — a door-shaped lie about a floor socket.

`sealedN` counts every seal regardless of geometry, so the pinned sample-layout counts are
untouched — `[room-forge]` still reports `spine+demo green sealed=1`. Case 6 covers all three seal
kinds so a future change cannot blank real walls while fixing stairs.

---

## 4c. Runtime verification (AutoPilot fleet, against the shipped EXE)

Static gates do not prove the game runs. Two headless player runs on the 2026-08-07 03:13 build:

```
BootToGameplay    — ok (1.3s)  — loaded Main_Castle_Overworld
ResolveHero       — ok
AssertDungeonLoop — ok (95.9s) — entered=real-tap won=True baselineClean=True
  A_combatCapable=PASS  B_onNavMesh=PASS  C_canMove=PASS(delta=5.53m)
  D_notBlack=PASS       E_singlePoseWriter=PASS       verdict=PASS
```

The dungeon loop survives tonight's changes end to end, including the tougher hollows. `D_notBlack`
also speaks to ticket #19 (post-victory fade never lifting).

**The fleet surfaced a NEW issue, reproduced 2/2 runs** — `EnvTreeFix`'s own post-fix verify fails
on ~300 tree/bush renderers in the hub (`shader='Universal Render Pipeline/Lit' still reads
white/grey`), ~308 error lines per run. Same failure class as the castle "pink floor". Filed as
task #39 **with the caveat that the fleet runs `-nographics`**, so "trees are white on screen" is
not yet proven — what is proven is a failing fix flooding the error log, which drowns the F8
harvest window.

---

## 4d. PHASE 2 — all three themed dungeons bake as real descents

| Dungeon | Rooms | Floors | Boss | Portal |
|---|---|---|---|---|
| `dg_sunken_vault` | 17 | 4 (Y 0 → −18) | Hollow Warden | NW `(-100, 0, 100)` |
| `dg_bonecrypt` | 21 | 5 (Y 0 → −24) | Necromancer, key-gated deep floor | SW `(-100, 0, -100)` |
| `dg_ember_deep` | 22 | 6 (Y 0 → −30) | Ember Warlord, orc → troll | N `(0, 0, 145)` |

All three: `matesFail=0`, `saved=True`. Floor counts match the WO §3 spec (4–5 / 5–6 / 6+).
Authoring is **data only** — three graph JSONs. `Phase2DungeonBatch.cs` is deliberately a separate
file from `GraphDungeonComposer.cs` (which the other seat was editing) so there is no merge point.

Portals go on four distinct compass pulls — E starter, NW vault, SW crypt, N ember, S cottage — so
no two dungeons send the player the same way. Ember Deep gets the longest walk because it is the
hardest crawl.

### The slice-1 defect Phase 2 exposed

Bonecrypt and Ember Deep first **aborted, one overlap each**. Read from the emitted layout, not
guessed: `stair_up_1` landed at `z=5` when its `StairDown` parent sat at `z=6`.

The stair socket was at local `(0, ±3, 0.5)`. That `0.5` is a wall standoff inherited from the
door-socket helper, which I never stripped when I repurposed the pose in slice 1 — **a hole in the
floor has no wall to stand off from.** It silently violated the composer's own documented invariant
(*"sockets at multiples of 3u … so `cell=[round(x),round(y),round(z)]` is a lossless round-trip"*):
every stairwell injected a half unit, `RoundToInt` quantised it into a **full** unit of drift, and
it accumulated down the descent until rooms that should exactly touch sat 1u too close. Four floors
survived it; five and six did not.

The regression now pins the invariant that actually broke — a stair socket with a fractional X or Z
fails, naming the whole-unit-per-floor drift — rather than only the symptom.

### A false green I shipped and then caught

`Phase2DungeonBatch` printed `composed=3/3` while **two of the three bakes had aborted**. It counted
"the call returned"; the baker aborts *without throwing*, it simply does not save. It now judges by
the scene file moving on disk and `FlowTrace.Fail`s any bake that did not save. Worth recording
because it is precisely the failure mode this project treats as worse than a red.

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
