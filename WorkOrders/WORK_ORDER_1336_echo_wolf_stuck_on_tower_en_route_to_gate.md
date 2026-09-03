# WORK ORDER 1336 - Aldwin gets stuck on a tower on his way to the gate and never moves again

**Status:** CLOSED 2026-09-03 - owner felt-test PASS. PRIOR STATUS: FIXED - the guide-lead carrot is now NAVMESH-ROUTED (shape 1: a carving structure closed the route and Pet.MoveToward has no pathfinding), so it walks around ANY blocking structure; owner felt-verifies and closes.
**Silo / Lane:** Echo presence / navmesh + escort pathing
**Type:** EXISTING (built, blocked by world geometry)
**Minted:** 2026-09-03 (CLI) from an owner felt-test on the Seeker, build `2026.09.03.352921`.
**Severity:** P2 - the founding companion visibly breaks during the escort the FTUE depends on.

## The owner's report, verbatim

> *"check the wolf when he runs to the gate, there is a tower in his way so gets stuck and doesn't
> move"*

## What is known BEFORE any code is read (do not re-derive this)

- The wolf is **Aldwin, the Ice Echo** - Echo #1, the founding companion (`EchoRosterCatalog.cs:149`).
  ⛔ **NOT** "Alduin the Mournful", who is the Necromancer boss. They are two separately-authored
  characters and two suites forbid conflating them; the mistake has been minted TWICE already.
- **`EchoWorldPresence` is the SINGLE appearance owner** for the Echo (WO-1108 Lane B, canon
  CLAUDE.md §7). It escorts the player to the gate, vanishes, and returns **once** after the battle -
  **one owner, one lifecycle, no second spawner.** Pinned by
  `Editor/Regression/EchoWorldPresenceRegression.cs`.
  ⛔ **Do NOT add a second mover, a second spawner, or a bespoke unstick coroutine.** Fix it in the
  one owner.
- `PetDeployer.DespawnEcho` is the FIRST despawn path in the game - treat it as the seam, not as one
  of several.

## ⛔ INSTRUMENT BEFORE YOU EDIT (CLAUDE.md §12)

A stuck agent has MANY plausible causes and static reading cannot choose between them. Get the data
first, then fix the step the data names. Candidate shapes, all of which look identical from outside:

1. **The tower CARVES the navmesh** and the Echo's path is genuinely blocked. Build-mode structures
   carve via `NavMeshObstacle` rather than a runtime bake - a tower placed on the route can close it.
2. **The path is fine but the agent is stopped** - `NavMeshAgent.isStopped`, a zero speed, or a
   destination it already considers reached.
3. **Off-mesh / partial path** - `NavMeshPathStatus.PathPartial`, so it walks to the nearest reachable
   point and halts there forever with no error.
4. **The destination itself is inside the carve** - the gate anchor resolves to a point the agent can
   never stand on.

⚠ **The hero moves by `NavMeshAgent` too, and `HeroLocomotion`'s class header LIES about this** (it
claims "pure transform"). If you compare the Echo's movement against the hero's, read the CODE.

**Add `FlowTrace` so a future stick names ITSELF** - path status, remaining distance, `isStopped`,
whether the destination is on the navmesh, and what the agent believes it is doing. A silent stick
cost this ticket; the next one should cost a single capture.

## The fix, once the data has named the cause

Prefer a fix that is **robust to ANY structure being placed on the route**, not one that special-cases
this tower. The player builds their own town (strategic placement is always on and structures are
movable), so **the route can be blocked again tomorrow by something else**. A fix that only clears
today's tower will be re-reported.

- If the destination is unreachable, the Echo should still resolve to a sane nearby point and continue
  its lifecycle rather than latching forever.
- Whatever happens, it must not strand the escort: the FTUE and the gate hand-off depend on this beat.

## Constraints

- ⛔ **Never hand-edit a `.unity` scene** (resave-corruption history). Runtime injectors or the builder
  method only.
- Do not move or delete the tower as the fix - the owner placed it, and player-placed structures are
  the whole point of build mode.
- Do not touch `HeroLocomotion` (WO-1298's lane, awaiting her felt-verify) or the store lane.
- `EchoWorldPresenceRegression` must stay green: one owner, one lifecycle.

## Acceptance

- [ ] The proving capture is quoted - path status / stopped state / reachability - naming which of the
      shapes above it was.
- [ ] Aldwin completes the escort with a structure on the route.
- [ ] `FlowTrace` names a future stick without needing another investigation.
- [ ] An oracle pins it if the cause is expressible in data. Prove it RED first.
- [ ] `COMPILE_GATE_OK` + `REGRESSION_OK <n>/<n> suites` on FRESH logs.
- [ ] ⛔ Owner felt-verifies and closes - a stuck companion is a felt defect no headless gate sees.
