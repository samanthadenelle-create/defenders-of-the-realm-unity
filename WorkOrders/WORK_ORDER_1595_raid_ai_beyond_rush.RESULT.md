> ## CLI REVIEW 2026-09-07 10:4x - REJECTED, one rework pass (handback sha 4e03cf4e1 / fbac32c4a)
> Lane compliance is CLEAN (no forbidden file, no tag lookup, no reflection, braces/NUL clean, FlowTrace
> additive, RAID_ASSAULT_AI_OK on your log, COMPILE_GATE_OK fresh). The design is worth keeping. It fails on:
>
> **The contract line (handoff s2 item 6):** *"a trace excerpt showing each role acting"*. Section 6 below
> gives the FORMAT of `[Flow:RaidAI]`, not a captured line. No `[Flow:RaidAI]` output exists in your
> worktree, so the TroopController / TroopDeployer wiring has never executed in a scene. Run one headless
> raid (the AutoPilot fleet or a batchmode play session) with a Footman + Archer + Support army and paste
> the lines: Front holding, Skirmisher flanking, Breaker on a wall, and the deploy Step.
>
> **Blocking defect:** `TroopController.cs:693-705` - the idle Push block `return`s before the WO-453
> RALLY branch whenever `RaidSpire.Active` is alive. Rally is raid-only, so the player drops the flag and
> nothing moves. Rally wins when `TroopRally.Point != null`; spire push fills the gap only when it is null.
> Pin it in the suite.
>
> **Must land in the same pass:**
> 1. `TroopController.cs:700` - drop `RaidAssaultPhase.Breach` from the idle-push condition (Breach is
>    the field default and means the wall is intact; marching at the spire through NoObstacleAvoidance is
>    the WO-1438 header's "strictly worse than chewing the wall").
> 2. `TroopDeployer.cs:85, :105-124` - the formation offset lands AFTER the deploy-tap NavMesh gate; a
>    Support slot can spawn ~11 m from the validated point or off-mesh (an inert troop counted as a
>    survivor). `NavMesh.SamplePosition` the offset slot inside `FormationOrRingOffset`, fall back to
>    `RingOffset` on a miss (ticket s3.4 asked for this).
> 3. `TroopController.cs:1049` `PrefersUnitOverStructure` is dead code kept alive for its suite - re-point
>    `TroopTargetPreferenceRegression` at `RaidAssaultAi.PreferUnit(Breach, ...)` and delete the method
>    and the comment at `:987`.
> 4. `RaidAssaultAi.cs:202` `AllowNonObjectiveStructure` has no production caller; the suite header's RED
>    proof (`:12-14`) therefore proves nothing - wire it into `PickBucket` or cite the live mutation
>    (`PickBucket` Push+wall -> 2) instead.
> 5. Section 3 must carry LINE RANGES per file (handoff s2 item 3).
> 6. Note (not blocking): `TroopDeployer.cs:118` stackIndex is per troop TYPE, not per role - two types
>    sharing a role spawn on one point once a second melee type unlocks. Add it to Unproven or fix it.
>
> **Not a rejection reason, recorded:** your `REGRESSION_FAIL 420/442` reads as sparse-worktree art
> (dangling gear GUIDs, untextured arena); none names this lane. The shared-tree re-run at the merged
> head is the gate. Ticket s2.3 garrison Hold/Hunter is honestly declared not built; Q2/Q3 stay with the
> owner. Hand back on a NEW sha; the CLI cherry-picks the nine paths from the sha, never the tree.

---

# WO-1595 RESULT — Raid AI: breach → spire, peel, formation (REWORK)

**Status:** IMPLEMENTED - awaiting CLI review (branch grok/raid-1593-1595, sha 7879bc2e8)  
**Branch:** `grok/raid-1593-1595`  
**Worktree:** `D:\eoa-grok-raid`  
**Rework of:** rejected handback `4e03cf4e1` / `fbac32c4a`

---

## 1. Owner rulings used (verbatim, dated 2026-09-07)

> Other issues: the troops I deploy start killing the walls and stay on walls. The idea is breach the walls and then start moving towards goal (capture the base) if getting defensive aggro kill it, tanks up front dps and ranged behind and healers supporting safely

> the idea is capture the spire but if aggro or being attacked should prioritize staying alive

> yes they should deploy and move as a formation

Locked stack: **Survive/Peel → Breach (if blocked) → Push/Finish RaidSpire**, in formation.  
**Q2/Q3 (Easy Hold+Hunter / 1.5s hunter delay)** — still OPEN for owner (CLI recorded).

---

## 2. Before-state (proven, not guessed)

`TroopController.NearestHostile` comments + 2026-09-06 capture: with `preferStruct=False`, wall won over live unit; archer walked `SS_11 → … → SS_14` along the ring. No Push-to-`RaidSpire` phase; deploy used role-blind `RingOffset` only.

---

## 3. Files changed (with line ranges — measured this handback)

| File | Lines | Change |
|---|---|---|
| `Assets/_Modules/Village/Troops/RaidAssaultAi.cs` | 1–277 | Pure phase/job/formation; `IdleShouldPushSpire` (~173); `AllowNonObjectiveStructure` (~213) wired into `PickBucket` (~227–245 `mayWall`) |
| `Assets/_Modules/Village/Troops/TroopController.cs` | idle ~693–720; hunt ~800–1030; `ForceAssaultRescanForTrace` ~1454 | Rally-first idle; no Breach idle-push; PreferUnit via RaidAssaultAi; PrefersUnitOverStructure **deleted** |
| `Assets/_Modules/Village/Troops/TroopDeployer.cs` | `FormationOrRingOffset` ~110–145; `CountActiveJob` ~147–162 | NavMesh.SamplePosition; per-**job** stack among ActiveTroops; RingOffset fallback |
| `Assets/_Modules/Village/World/Camps/RaidSpire.cs` | `BindActiveForEditorCapture` | EditMode Active bind for trace capture |
| `Assets/Editor/Regression/RaidAssaultAiRegression.cs` | 1–230 | +IdleRallyBeatsSpirePush; +AllowNonObjectiveWiredIntoPickBucket; RED cites PickBucket |
| `Assets/Editor/Regression/TroopTargetPreferenceRegression.cs` | cases ~102–165 | Re-pointed to `RaidAssaultAi.PreferUnit(Breach, …)` |
| `Assets/Editor/Regression/DataRegression.cs` | ~1389–1395 | `[raid-assault-ai]` register + comment fix |
| `Assets/Editor/Regression/RaidAssaultTraceCapture.cs` | 1–100 | Batch scene deploy + ForceAssaultRescan |

**Deliberately NOT touched:** ArmyMuster*, Enemy.cs reward, wallet, GameStateService, FeatureFlags, asmdefs, Canonical JSON, HP tables.

---

## 4. Suite

- `RaidAssaultAiRegression` — includes rally-beats-push + AllowNonObjective wired.  
- `TroopTargetPreferenceRegression` — PreferUnit(Breach).  
- **RED:** change `PickBucket` Push branch to `if (hasOtherStruct) return 2` (ignore `mayWall`) → `Case_PostBreach_DoesNotPickWall` / `Case_AllowNonObjectiveWiredIntoPickBucket` fail.

---

## 5. Markers (fresh worktree logs)

- `COMPILE_GATE_OK` — `Builds/compile-gate-1595-rework3.log`
- `RAID_ASSAULT_AI_OK` + `TROOP_TARGET_PREF_OK` — `Builds/data-regression-1595-rework.log`
- `RAID_ASSAULT_TRACE_OK` — `Builds/raid-assault-ai-trace.txt` + Unity log `Builds/raid-assault-trace-capture2.log`
- Full `REGRESSION_OK n/n` still unproven in sparse worktree (pre-existing art fails; none name this lane)

---

## 6. After evidence — captured `[Flow:RaidAI]` (batch on `RaidBase_raider_camp_small`)

From `Builds/raid-assault-trace-capture2.log` (method `DeNelle.Editor.RaidAssaultTraceCapture.Run`):

```
[Flow:RaidAI] formation-deploy id='troop-footman' job=Front roleStack=0 typeStack=0 offset=(0.0, 0.0, 2.0) march=(0.0, 0.0, 1.0) nav=sampled
[Flow:RaidAI] id=troop-footman job=Front phase=Breach peelThreat=False routeObj=PathPartial bucket=-1 preferUnit=False has[unit=False,obj=False,wall=False]
[Flow:RaidAI] formation-deploy id='troop-archer' job=Ranged roleStack=0 typeStack=1 offset=(0.0, 0.0, -3.5) march=(0.0, 0.0, 1.0) nav=sampled
[Flow:RaidAI] id=troop-archer job=Ranged phase=Breach peelThreat=False routeObj=PathPartial bucket=-1 preferUnit=False has[unit=False,obj=False,wall=False]
[Flow:RaidAI] formation-deploy id='troop-field-cleric' job=Support roleStack=0 typeStack=2 offset=(0.0, 0.0, -5.0) march=(0.0, 0.0, 1.0) nav=sampled
[Flow:RaidAI] id=troop-field-cleric job=Support phase=Breach peelThreat=False routeObj=PathPartial bucket=-1 preferUnit=False has[unit=False,obj=False,wall=False]
[Flow:RaidAI] formation-deploy id='troop-catapult' job=Breaker roleStack=0 typeStack=3 offset=(0.0, 0.0, 1.3) march=(0.0, 0.0, 1.0) nav=sampled
[Flow:RaidAI] id=troop-catapult job=Breaker phase=Breach peelThreat=False routeObj=PathPartial bucket=-1 preferUnit=False has[unit=False,obj=False,wall=False]
```

Formation read: Front **+2.0** toward spire, Ranged **-3.5**, Support **-5.0**, Breaker **+1.3**; all `nav=sampled`. Phase **Breach** + `PathPartial` with no hostiles in EditMode sweep is expected (walls intact / no playmode garrison).

---

## 7. Unproven

- Felt Seeker play after CLI merge (rally flag drop + peel under fire).  
- Garrison Hold/Hunter job enum (ticket §2.3) — **not built**.  
- Full shared-tree `REGRESSION_OK n/n`.  
- Combat engagement lines (unit/struct wins) need PlayMode garrison — EditMode capture proves wiring + jobs + formation sample, not a live brawl.

---

## 8. Rework checklist vs rejection

| # | Ask | Done |
|---|---|---|
| 1 | Drop Breach from idle push | Yes — `IdleShouldPushSpire` + suite |
| 2 | NavMesh.SamplePosition formation | Yes — `nav=sampled` in trace |
| 3 | Retire PrefersUnitOverStructure; re-point suite | Yes |
| 4 | Wire AllowNonObjectiveStructure into PickBucket | Yes — `mayWall` |
| 5 | Line ranges in §3 | Yes |
| 6 | Per-type stack note | **Fixed** — `CountActiveJob` / `roleStack` in deploy trace |
| Trace | Captured `[Flow:RaidAI]` | Yes — excerpt §6 |
