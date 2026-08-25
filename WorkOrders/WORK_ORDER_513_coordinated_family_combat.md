<!-- status-reconcile-2026-08-22 -->
> # PARTIALLY STALE 2026-08-22 - THE HEADLINE ASK IS STILL REAL; THE RCA BELOW IS WRONG.
> **The feature this WO asks for - a family that GANGS the hero (surround / flank / expressed roles after
> arrival) - is genuinely UNBUILT, so the Status stays READY. But do not trust section "Diagnosed current
> state": it has drifted and will send you to the wrong code.**
>
> **The specific claim that is now FALSE: "all 3 orcs run identical solo melee-Rush - no roles expressed."**
> Arena orcs **DO** get role tactics today. `Assets/_Modules/Village/Arena/BattleArena.cs:1529-1539`:
> `EnemyRole role = EnemyBrain.RoleForId(id); brain.Role = role;` ... `brain.RosterId = id;` ...
> `EnemyBrain.ApplyRoleTactics(brain, role);` (line **1538**), followed by
> `FlowTrace.Step("BattleArena", $"ROLE '{id}': tactics applied for {role}.")`. The same block also sets
> `brain.SetHeroOnlyTarget(true)` (WO-482) so the orcs no longer mill toward a 7000m-away Heart - which is
> the behaviour the original felt-test read as "LAX".
>
> **Every line citation in that section is stale.** It cites `BattleArena.SpawnFamily (~L707-713)` and
> `MaybeDisbandOnArrival (~L728-739)`; the spawn/role/family-wiring block now lives around **L1520-1545**.
> Re-survey `BattleArena.cs`, `FamilyLeader.cs` and `FamilyMember.cs` at HEAD before scoping this.

# WORK_ORDER_513 — coordinated family combat AI (the orc family GANGS the hero)

**Status:** SPEC — RCA/SPEC PASS REQUIRED, FEATURE OPEN. The headline ask is still real: the arena **hard-disbands** the family, so a pack that GANGS the hero (surround / flank / expressed roles after arrival) is genuinely unbuilt. ⛔ But every path and ownership claim in "Diagnosed current state" is STALE — re-map `Assets/_Modules/Village/Arena/BattleArena.cs`, the `Village/Families` types (`FamilyLeader` / `FamilyMember`), and the now **wave-owned** `EnemyGroupCoordinator` at HEAD before scoping. Do not scope from the cited line numbers.

*(Board note 2026-08-24 — Ready-queue audit, `READY_FOR_REVIEW.md`: leading token corrected to a canonical bucket word. Only the status token and this note changed; the ticket body and its claims are untouched.)*
**Origin:** owner felt-test — "I thought they would FORMATION attack but they seem LAX." Diagnosis (this session) proved the family formation is TRAVEL-ONLY and disbands on arrival, after which all 3 orcs run identical solo melee-Rush — no surround, no flank, no roles expressed. This WO delivers the coordinated-family threat she pictures.

## Diagnosed current state (the seams to build on — verified from code)
- `FamilyLeader.cs` / `FamilyMember.cs` = a TRAVEL formation only: members hold slots vs the leader while approaching (`FamilyMember.GetDesiredSlotWorld`). `BattleArena.SpawnFamily` (~L707-713) registers leader+members.
- `BattleArena.MaybeDisbandOnArrival` (~L728-739) DISABLES the leader once it's within 6m of the hero -> `FamilyLeader.OnDisable -> Disband()` -> every `FamilyMember.StopFollowing()` re-enables each orc's SOLO `EnemyBrain`. After this instant there is ZERO group coordination.
- `EnemyBrain` only flanks/kites if `_tactics != null` (~L586,917); arena orcs never get tactics (Role unset in `BuildEncounterDef` ~L756-774). (The inline quick-fix sets the mage to Kiter + warrior to Flanker — this WO is the COORDINATION layer above that.)
- An `EnemyGroupCoordinator.SetCoordinatedFlankAngle` seam already exists (`EnemyBrain.cs:288`) — reuse it; do NOT greenfield.
- Depends on / assumes: the inline arena-navmesh bake fix (FloorPlaneScale 5->8) so orcs can physically path the kite floor (a coordinator is moot if they can't reach the hero).

## Goal
During the fight (not just the approach), the orc family applies COORDINATED pressure so the player can't safely tunnel one target:
- **Role-based positioning:** Tank closes/holds the FRONT (body-blocks, soaks), Warrior FLANKS to a side/behind, Mage holds the BACK ARC and pokes ranged (telegraphed casts). Reuse the Kiter/Flanker/Rush archetypes from the inline fix; this WO adds the *coordination* that assigns who-does-what.
- **Surround bearings:** a lightweight combat coordinator (keep it ALIVE during the fight instead of fully disbanding) assigns each live member a target BEARING around the hero (e.g. evenly-spaced angles via `SetCoordinatedFlankAngle`) so they encircle rather than stack on one point.
- **Staggered engage + telegraphs:** members don't all swing at once — staggered windups with the existing telegraph tells so the player can read + react (the "animated staged family fight", memory `atb-flat-vs-overworld-animated-combat`). Pull-one-out should feel punished by the others, not ignored.
- **Re-coordinate on the fly:** when a member dies or the hero re-locks/kites, the coordinator re-assigns bearings so the remaining orcs keep pressure + close the gap.

## Approach (reuse-first, don't disband)
1. Replace `MaybeDisbandOnArrival`'s hard disband with a HANDOFF to a combat coordinator (the leader's travel role ends, but a combat coordinator takes over the same member set) — or keep a thin coordinator component on the family root that runs through the fight.
2. The coordinator each tick: reads live members + the hero, assigns each a role-appropriate slot/bearing around the hero (Tank front, Warrior flank angle, Mage back-arc radius) via `SetCoordinatedFlankAngle` / each brain's tactics, and staggers their attack windows.
3. Members still run their own `EnemyBrain` movement/attack — the coordinator only sets their *desired bearing/role + engage timing*, so it's a thin orchestration layer, not a rewrite.
4. Instrument (FlowTrace "EnemyAggro"/"BattleArena"): coordinator assigns bearings, role each orc took, stagger timing — so headless can prove the family coordinates.

## Slices
- **S1:** keep a coordinator alive post-arrival (no disband-to-solo) + assign static surround bearings (Tank front / Warrior flank / Mage back). Gate: the 3 orcs encircle the hero instead of stacking; FlowTrace shows assigned bearings.
- **S2:** staggered engage windows + telegraphed mage casts so attacks are readable, not simultaneous.
- **S3:** re-coordinate on member death / hero re-lock (close the gap, keep pressure).

## Guardrails / Do NOT
- Reuse `EnemyGroupCoordinator.SetCoordinatedFlankAngle` + the existing Kiter/Flanker archetypes; do NOT greenfield a new AI stack.
- Don't break the TRAVEL formation (approach still uses FamilyLeader/Member); this only changes post-arrival combat.
- Flag-gate if risky; keep mobile-cheap (3-4 enemies, simple bearing math, no heavy pathfinding per frame).
- Felt-sensitive: the owner judges "does the family feel like a coordinated threat." Headless proves bearings/roles assigned; feel is her call.

## Acceptance
Pulling one orc out to duel is PUNISHED by the others closing/flanking; the family encircles + applies staggered, readable pressure; Mage pokes from the back arc, Warrior flanks, Tank holds front; re-coordinates on death. Gate-clean; arena-scoped; no regression to the travel approach or the ATB.

> **AUDIT 2026-08-21 (agent fleet, read-only):** OPEN — STILL VALID. Evidence: `BattleArena.cs:1559-1585 hard-disbands` — post-arrival coordinator unbuilt. Status left at READY deliberately: this work is real and unbuilt. Verified against HEAD 2f0b97bb5, not against the ticket's own claims.
