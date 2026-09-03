# WORK ORDER 1336 - RESULT

**Status:** FIXED (code + oracle). ⛔ Owner felt-verifies and closes.
**Date:** 2026-09-03
**Lane:** Echo presence / navmesh + escort pathing

---

## Which of the four shapes it was: **SHAPE 1** - the route is genuinely closed by a carve

...but the *reason it never recovers* is a fifth fact the WO could not have known before the data
was read: **the pet has no pathfinding at all.**

### The proving capture (owner F8 seq 4225, `Main_Castle_Overworld`; identical sticks at seq 3604 /
3605 / 3606 / 4162, i.e. this has been reproducing for weeks at the same spot)

`logs/f8-inbox/capture-20260901-131021-seq4225.md:64`

```
[Flow:Pets] guide-lead TICK 'pet-ice-wolf': moved=0.01 m/s over 1.02s -> BODY DID NOT MOVE
  (carrot written, zero displacement - the write is being ignored downstream).
  dist=21.09m heroDist=13.00m mode=Defend
  agent(enabled=True, onNavMesh=True, isStopped=False, velocity=0.00)
  carrot=(-1.31, 0.08, -17.65) homePost=(-1.31, 0.08, -17.65).
```

and, four lines earlier in the SAME capture, every downstream gate passing:

```
[Flow:Pets] guide-lead LANE ACTIVE on 'pet-ice-wolf': lead PASSES the mode gate (mode=Defend)
  and the ff.petcombat gate (PetCombat=False) -> MoveToward(_homePost=(-1.31, 0.08, -17.65))
  IS being integrated this frame (WO-1014 Half C).
```

### What the data eliminates, line by line

| Shape | Verdict from the capture |
|---|---|
| 2 - agent stopped / zero speed / thinks it arrived | **EXCLUDED.** `enabled=True, onNavMesh=True, isStopped=False`, and `dist=21.09m` is nowhere near the 2.2 m `LeadArriveRadius`. |
| 3 - partial path | **NOT APPLICABLE.** No path was ever computed - see below. |
| 4 - destination inside the carve | **NOT THE CAUSE HERE** (the gate anchor is reachable; the hero walks to it and the beat completes). Defended anyway, see the fix. |
| 1 - the carve closes the route | **CONFIRMED**, with the mechanism named below. |

### The mechanism (read at source, not inferred)

- `Pet.MoveToward` (`Assets/_Modules/Pets/Pet.cs:705-740`) integrates the carrot with
  **`_agent.Move(displacement)`**. `NavMeshAgent.Move` *slides* the body and **clamps that slide to
  the walkable surface** - it computes **no route**. There is no `SetDestination` anywhere in
  `Pet.cs`. That is also why `velocity=0.00` on a healthy agent in every one of these traces.
- The lead carrot was a **dead-straight projection**:
  `leadCarrot = petPos + toAnchor.normalized * LeadDistance` (`PetHeroLeash.cs`, pre-fix).
- Build-mode structures **carve** the navmesh: `BaseLayoutLoader.cs:513-518` adds a
  `NavMeshObstacle` with `carving = true` on every placed structure (its own comment even names
  `tower_ground_archer`).
- So: tower on the line -> carrot lands inside the carve -> `Move()` clamps the step to nothing ->
  the guide presses into the tower face **forever**. The healthy escort trace
  (`logs/device/2026-08-20-equip.log:2600303+`) shows the same rule in the clear case: a
  *dead-straight* carrot marching -4.5 m in z per second with **zero** deviation. It was never
  routing; it just never had anything in the way.

---

## The fix

**One file changes behaviour**, and it changes only *where the carrot is put* - the mover, the
appearance owner and the lifecycle are all untouched.

### `Assets/_Modules/Pets/PetHeroLeash.cs`
- **The guide-lead carrot is now placed `LeadDistance` along the corner polyline of a real
  `NavMesh.CalculatePath`** (`CarrotAlongCorners` + `EnsureLeadPath`, re-routed every 0.35 s and
  immediately when the anchor moves; `GetCornersNonAlloc` into a fixed 32-slot buffer, no per-frame
  garbage).
- **The anchor is snapped onto the navmesh first** (`NavMesh.SamplePosition`, 6 m) - that closes
  **shape 4** pre-emptively: a structure dropped over the gate mouth resolves to a sane nearby
  standable point instead of a destination the agent can never occupy.
- **A no-progress watch** (`UpdateLeadBlockedWatch`): if the guide makes no headway for 3 s while it
  is *free to move*, it holds at the furthest **reachable** corner and stands looking around rather
  than running on the spot into a wall. It deliberately does **not** fire when the guide has arrived,
  nor when the hero-leash clamp is holding it back (the guide WAITS for a lagging hero by design -
  accusing the navmesh of that would be a false positive).
- **The `ReturnRadius` hero-clamp result is itself navmesh-snapped.** The clamp is a straight-line
  projection like the old carrot was, so without this the "never desert the hero" rule could
  re-create the exact wedge this ticket fixes, just at the leash limit instead.
- **New permanent FlowTrace** (never strip, CLAUDE.md §12): every `guide-lead ENGAGED` and 1 Hz
  `guide-lead TICK` line now carries
  `route(status=..., corners=N, anchorOnNavMesh=..., resolvedAnchor=..., noProgressFor=...s, bestDist=...m)`,
  and a one-per-episode `guide-lead BLOCKED` **Warn** names the shape in words
  (no route / partial / invalid / complete-but-wedged). **A future stick names itself in one capture.**

### Why it generalises to ANY blocking structure
The carrot follows whatever the **live** navmesh says is walkable. Carving obstacles cut real holes,
so `CalculatePath` routes around **this** tower, a wall the player moves there tomorrow, a storefront,
anything - with no per-structure knowledge and no special case. Nothing about the tower is named
anywhere in the fix. **On a clear route the first path leg IS the straight line, so the escort's feel
on open ground is byte-for-byte the old behaviour** (pinned by the `straight` oracle case).

---

## The oracle - `Assets/Editor/Regression/GuideLeadRoutingRegression.cs` (NEW)

`CarrotAlongCorners` was written as a **pure static** function precisely so the shipped rule can be
executed against synthetic corner geometry with no navmesh bake - the same reflection-probe pattern
`GuideLeadMovementRegression` uses for `Pet.GuideLeadOwnsMovement`. Five cases:

| Case | Pins |
|---|---|
| `dogleg` | **THE TICKET.** Tower carves the direct line; the walkable route doglegs. The carrot must be on the open leg. |
| `straight` | Open ground is unchanged - routing must not alter the escort's feel where nothing blocks it. |
| `short-route` | A route shorter than the carrot aims at its **end**, never past the last reachable corner (that would re-wedge on the final approach). |
| `no-route` | `count < 2` / null buffer degrade to the historical straight projection - a momentary off-mesh query must never freeze the guide. |
| `seam` | Source-lint: `NavMesh.CalculatePath`, `CarrotAlongCorners(`, the anchor snap, the `guide-lead BLOCKED` warn, and the `pathStatus=` / `anchorOnNavMesh=` forensics all still exist. |

Markers: `GUIDE_LEAD_ROUTE_OK` / `GUIDE_LEAD_ROUTE_FAIL`.
Entry point: `DeNelle.Editor.Regression.GuideLeadRoutingRegression.RunAll`.
`Run(out reason)` is DataRegression-shaped; **wiring into `DataRegression.RunAll` is left to the
committer** (that file is lane-fenced) - same convention as `GuideLeadMovementRegression`.

### RED proof + the mutation
The mutation: revert `CarrotAlongCorners` to the pre-fix rule
`from + normalize(anchor - from) * leadDistance`.

Both rules were executed numerically against the `dogleg` geometry
(guide at origin, anchor `(5, 0, 20)`, walkable corners `(0,0,0) -> (5,0,0) -> (5,0,20)`, lead 3.5 m)
before the suite was written:

```
NEW routed    carrot=(3.50, 0.00)   dogleg-assert=PASS
OLD straight  carrot=(0.85, 3.40)   dogleg-assert=FAIL      <-- aims INTO the carve
straight corridor  NEW = (0, 0, 3.5)   OLD = (0, 0, 3.5)    <-- parity holds
short route        NEW = (1, 0, 1)                          <-- route end, no overshoot
no route           NEW = (0, 0, 3.5)                        <-- straight fallback
```

So the `dogleg` case is genuinely discriminating: under the old rule it returns the exact class of
point that `NavMeshAgent.Move` clamps to zero, and the suite fails. The other four cases hold under
both rules by design - they are the *parity* guard, not the discriminator.

---

## Checks

| File | Braces | NUL |
|---|---|---|
| `Assets/_Modules/Pets/PetHeroLeash.cs` | BALANCED | clean |
| `Assets/Editor/Regression/GuideLeadRoutingRegression.cs` | BALANCED | clean |

Edited lines re-read after each edit (a brace check does not catch a missing semicolon).
Both files written with the Write/Edit tools on Windows paths; no bash redirects.

## Deliberately NOT touched

- **`EchoWorldPresence` / `EchoAutoDeployTrigger` / `PetDeployer`** - the single appearance owner and
  its despawn seam are correct and unchanged. `EchoWorldPresenceRegression` is untouched and its
  invariant (one owner, one lifecycle, no second spawner) is preserved: no mover, spawner or unstick
  coroutine was added.
- **`Pet.cs`** - the mover itself is unchanged, so `GuideLeadMovementRegression`'s Pet.cs/PetHarvester
  source-order lints are unaffected.
- **`HeroLocomotion`** (WO-1298's lane), the store lane, the WebGL payload lane.
- **The tower** - not moved, not deleted, not special-cased. No `.unity` scene was touched.
- **No FlowTrace was stripped**; the WO-1014 forensics are intact and were extended, not replaced.

## Gates still owed (the lead runs these - this was an edit-only task)

- [ ] `COMPILE_GATE_OK` on a fresh log
- [ ] `REGRESSION_OK <n>/<n> suites` on a fresh log
- [ ] `GUIDE_LEAD_ROUTE_OK` (new suite, standalone or once wired into `DataRegression.RunAll`)
- [ ] ⛔ **Owner felt-verifies and closes** - a stuck companion is a felt defect no headless gate sees.
