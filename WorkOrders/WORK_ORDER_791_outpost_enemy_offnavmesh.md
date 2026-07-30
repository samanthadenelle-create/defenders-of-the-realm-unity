# WORK ORDER 791 — Outpost/garrison enemies spawn OFF the NavMesh → frozen + floating

**Status:** READY TO IMPLEMENT
**Lane:** Lane 2 (Combat/AI) + Lane 5 (World/NavMesh)
**Type:** EXISTING (spawn + AI built; NavMesh coverage/placement is the defect)
**Minted:** 2026-07-30 (owner felt-reports + screenshots)
**Author:** UI/RCA seat (agent-sourced RCA). CLI implements + gates. PO felt-verifies + closes.

---

## Symptom (owner)

In an EnemyOutpost garrison: enemies are **"non moving"** and one is **"floating in air."**

## RCA — proven from code (read-only agent, file:line)

Both symptoms share ONE root: the garrison agents are placed **off the NavMesh**, so they can't be
snapped to the ground (float) and can't be given a path (frozen).

- Spawn snaps to the NavMesh only within 6 m; if none found, the `NavMeshAgent` is added **off-mesh
  anyway** and self-reports: *"NavMeshAgent ... spawned OFF the navmesh (isOnNavMesh=false) — it will
  idle and never chase"* — `EnemyFactory.cs:45-54,318-324`.
- `Enemy.DriveNav` calls `SetDestination` **only when `_agent.isOnNavMesh`** — `Enemy.cs:1000-1008`
  (also guards `:644,:1015`); off-mesh → no destination ever set → body never moves.
- `EnemyBrain` Rush pathing is likewise gated on `isOnNavMesh` — `EnemyBrain.cs:849`.
- In `EnemyOutpost`, `SnapToNav` returns the point **unchanged** when no mesh is within
  `GarrisonRing+6` (~12 m) — `EnemyOutpost.cs:590-595`; `VerifyOnNavMesh` warns *"OFF-MESH SPAWN ...
  defender may never path/aggro"* — `:565-570`.
- **Floating** = the same off-mesh spawn: with no NavMesh hit, the agent keeps its raw spawn Y (no
  ground snap), leaving it hovering above the terrain.

**Most-likely root:** the OuterWorld NavMesh bake does not cover the ground under the outpost anchor
(`RaidOutpostSystem.cs:185-217` anchor selection), so the whole garrison is off-mesh → frozen +
floating. The off-mesh spawn is only *reported*, never *repaired* (`EnemyFactory.cs:45-54,318-324`).

## Candidate fix locations / options
- Ensure NavMesh coverage under the outpost anchor (bake/anchor selection in `RaidOutpostSystem.cs:185-217`).
- OR a runtime NavMesh bake for the outpost floor mirroring `ArenaNavMeshBaker` (`BattleArena.cs:390-396`).
- OR repair (not just report) the off-mesh spawn in `EnemyFactory.cs:45-54` — widen the sample radius
  and, on success, snap the transform Y to the hit (kills the floating too); on failure, log loudly.

## Proving steps (§12 — headless before/after)
- `[Flow:Enemy] NavMeshAgent ... spawned OFF the navmesh ... will idle and never chase`
  (`EnemyFactory.cs:320`) and `[Flow:Raid] OFF-MESH SPAWN: outpost ...` (`EnemyOutpost.cs:568`) →
  should be ABSENT after the fix.
- `[Flow:EnemyAggro] ... no COMPLETE path ... holding (walled off)` (`EnemyBrain.cs:878`) → gone.
- Confirm the garrison enemies chase the hero and stand on the ground (Y matches terrain).

## Acceptance
- [ ] Garrison enemies are ON the NavMesh (isOnNavMesh=true), move/chase the hero, and stand on the
      ground (no floating), verified via a headless outpost run trace + screenshot.
- [ ] Off-mesh warning lines absent for the garrison spawn.
- [ ] Brace/NUL gate green on any `.cs` edited; `COMPILE_GATE_OK`.
- [ ] Handed to owner; **PO closes**.

## What NOT to touch
- Materials/weapon presentation = WO-790 (don't fix here).
- Zero-damage-to-hero = WO-792 (don't fix here). Note: if the enemies never reach the hero because
  they're frozen, that could MASK or contribute to the zero-damage report — coordinate: verify WO-792
  with mobile (on-mesh) enemies once this lands.
- Do not touch the Village wave-enemy navmesh (works) — this is the OuterWorld outpost anchor.

*Notion row pending.*
