<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK ORDER 146 — Leader/Follower Formation Movement (first slice of "monster families")

**Status:** DONE — audit-verified as shipped (2026-08-21 backlog audit).
**Date:** 2026-05-30
**Priority:** High — the first *implementable* slice of the "monster families" pillar. Turns a loose swarm into a **coherent pack that moves as one** (leader pathfinds, followers hold dynamic slots). The visible groundwork for the family Behavior-tree brain (a *later* WO) and the readable, intimidating raid parties WO-143 spawns.
**Lane:** **Combat / AI — code only.** NO scene-file edits, NO `VillageSceneBuilder.cs` (frozen, CLAUDE.md §3/§9), NO bake fired from UI. Runs in the Combat/AI parallel lane (CLAUDE.md §9 — "EnemyBrain, ATB — code only, no scene files"). Never contends with the World/Environment lane that owns the NavMesh bakes.

**Depends on:**
- **A baked NavMesh — HARD DEPENDENCY.** Follower slot-following *requires* a baked NavMesh (every desired slot is `NavMesh.SamplePosition`-validated and the leader/followers drive `NavMeshAgent`s). **The VILLAGE is baked and works today** (`Enemy`/`EnemyBrain` move there now). **The OUTER WORLD is NOT baked** — that bake is owned by **WO-142 (`OuterWorldBuilder`)** and is not yet done. **Therefore this WO ships + is tested IN THE VILLAGE first. Open-world formation use is BLOCKED on the WO-142 exterior NavMesh bake** and must not be claimed working there until that bake lands. State this in Acceptance.
- **`Enemy` (built)** — `Assets/_Modules/Village/Enemies/Enemy.cs`. Owns the `NavMeshAgent`, march, HP, contact attack. The follower drives its movement **only** via `Enemy.SetBrainTargetPosition(Vector3?)` (`Enemy.cs:229`) — the established brain→nav override seam (DEF-72). **REUSE VERBATIM. Do NOT add a second NavMeshAgent path or fight `Enemy`'s own `DriveNav`.**
- **`EnemyBrain` (built)** — `Assets/_Modules/Village/Enemies/EnemyBrain.cs`. Per-enemy role/tactics brain that *also* writes `SetBrainTargetPosition`. Formation is a **GROUP layer that sits ABOVE EnemyBrain** — see §1 ownership rule (only ONE writer of `SetBrainTargetPosition` per enemy at a time).
- **`EnemyGroupCoordinator` (built)** — `Assets/_Modules/Village/Waves/EnemyGroupCoordinator.cs`. Per-group suppress→release. The **existing group-coordination seam**; the new `FamilyLeader` is the natural successor/sibling for ongoing (not one-shot) group cohesion. Reconcile, don't duplicate its suppress-release responsibility.
- **WO-143 (Roaming Raids — READY)** — `WORK_ORDER_143_roaming_raids.md`. Its `RaidDirector` (NOT yet built) is the **runtime spawner seam** that will assemble a family (spawn a leader + followers, assign slots) instead of a loose `EnemyGroupSpawner` batch. This WO does **not** build `RaidDirector`; it defines the family components `RaidDirector`/`EnemyGroupSpawner` will wire, and ships a **dev test spawner** so the formation is testable today with no scene edit (mirrors `EnemyFamilyTestSpawner.cs`).
- **WO-145 (Advanced Enemy Tactics — READY)** — `WORK_ORDER_145_advanced_enemy_tactics.md`. The **leader's** target selection defers to WO-145's Utility/role scorer (the leader IS an `Enemy`+`EnemyBrain`; engage context comes from the brain's chosen target). **Do NOT re-spec target scoring here** — formation only consumes "who is the leader engaging?" to pick the engage formation.

**North Star:** `docs/NORTH_STAR.md` — "DEFEND base + mines from waves **and roaming enemies** — or lose them"; the threat model is `WaveManager` + `EnemyBrain`. Monster *families* that move as a cohesive pack are the readability + menace that makes the CoC×Warcraft defend loop worth mastering (a wedge charging your line reads as a threat; a blob does not).

---

## 0. RECONCILE — what already EXISTS (read before writing a line; project trap #1)

Verified by full read of all files below. **Build additively on every one of these — none is replaced.**

| System | Where (verified) | What it already does | How WO-146 relates |
|---|---|---|---|
| **`Enemy`** | `Assets/_Modules/Village/Enemies/Enemy.cs` | Single enemy body. `NavMeshAgent` march (throttled `DriveNav` `:425`), HP, contact attack, death/VFX. **`SetBrainTargetPosition(Vector3?)` `:229`** overrides the nav destination with an explicit world point; null reverts to the Heart-march. `Configure(id, EnemyDef, heart)` `:278`. `avoidancePriority` randomised `:308`, `autoRepath=false` `:304`. | **REUSE VERBATIM.** A follower computes its slot world-position and pushes it through `SetBrainTargetPosition` — *exactly* the seam EnemyBrain already uses. The leader is a plain `Enemy` whose nav is unchanged (it marches/engages normally). **No `Enemy` edit in this WO.** |
| **`EnemyBrain`** | `Assets/_Modules/Village/Enemies/EnemyBrain.cs` | Writes `SetBrainTargetPosition` every frame from role/tactics (`Update` `:206`, `ComputeTacticalDestination` `:287`). `OnDisable` clears it (`:240`). Caches Heart/hero. | The **leader** keeps its `EnemyBrain` (it targets + engages normally). A **follower must NOT also run an independent destination-writing brain** — two writers of `SetBrainTargetPosition` on the same `Enemy` duel frame-to-frame. §1 ownership rule resolves this: a follower's `FamilyMember` is the sole writer while following; it reads the leader's *engage target* (from the leader's brain) for context, but does not run its own role-targeting destination. |
| **`EnemyGroupCoordinator`** | `Assets/_Modules/Village/Waves/EnemyGroupCoordinator.cs` | One per spawned group; holds members `Suppressed` until all spawned, then `ReleaseAll` → `Rush` simultaneously, then self-destructs (`:132`). Registers `EnemyBrain` (`RegisterMember` `:64`), prunes on death (`HandleMemberDied` `:137`). | **COMPLEMENTARY, not duplicated.** The coordinator does a **one-shot** "charge together" release then dies. `FamilyLeader` provides **ongoing** cohesion (slots tracked every frame for the family's life). A family MAY still spawn through the coordinator for the synchronized initial charge; the leader takes over cohesion after release. Do NOT fold suppress-release into `FamilyLeader` — reference the coordinator. |
| **`WaveEnemyGroup.SpawnFormation` + `GetFormationOffset`** | `Assets/_Modules/Village/Waves/WaveEnemyGroup.cs:82,151` | `enum SpawnFormation { Line, Wedge, Scattered }` + a **static, spawn-time** offset table. Used **once** at instantiation to spread a group at its spawn point; it is NOT tracked after spawn. | **DISTINCT — do NOT extend or reuse this enum for the runtime layer.** `SpawnFormation` is a *one-shot spawn spread*; this WO's `FormationShape` is a *continuously-tracked dynamic slot field in leader-local space*. Different lifetime, different math. Name the new enum **`FormationShape`** to avoid collision. (Flag for owner: a later pass could converge the spawn-spread onto `FormationShape`; out of scope here.) |
| **`EnemyFamilyTestSpawner`** | `Assets/_Modules/Village/Enemies/EnemyFamilyTestSpawner.cs` | Self-bootstrapping DDOL dev tool: **'J'** key in Village spawns a code-built pack (3 Grunts/Tank/Healer capsules + `Enemy`+`EnemyBrain`+`NavMeshAgent`) with no scene edit. NavMesh-snaps each spawn. | **PATTERN TO MIRROR** for this WO's dev test spawner (§7). Use a **different hotkey** ('K') so both can coexist. Code-built, no scene/prefab/SO needed — proves formation in the live Village immediately. |
| **`EnemyTacticalState`** | `Assets/_Modules/Village/Enemies/EnemyTacticalState.cs` | `enum { Rush, Flank, Retreat, Suppressed }` (WO-145 appends Kite/Reposition). | **READ-ONLY reference.** Formation context (roam/engage/flee) is a SEPARATE concept from per-enemy tactical state. Do not overload this enum. The follower may *read* the leader's tactical state as one input to context selection, but formation context is its own small enum (§4). |

**Hard reconciliation rules:**
- **One `Enemy` body, one `SetBrainTargetPosition` writer per enemy at any instant** (§1). The leader's writer is its `EnemyBrain`; a follower's writer is its `FamilyMember`. A follower does NOT also run destination-writing role logic while in formation.
- **Formation is a group layer ABOVE EnemyBrain**, not a fork of it. No `Enemy.cs`/`EnemyBrain.cs` edits required by this WO (additive components only).
- **`FormationShape` ≠ `SpawnFormation`.** New enum, new file, new lifetime. Do not extend the spawn-time enum.
- Reconcile, never blind-replace (memory *wo-batch-reconcile-not-replace*).
- **`docs/MONSTER_FAMILY_ARCHITECTURE.md` does not exist yet** (verified). This WO **proposes** the canonical component names **`FamilyLeader` / `FamilyMember` / `FormationController`**. If that umbrella doc lands first and names them differently, **conform to the doc** (flag the rename for owner; do not race).

---

## 1. Architecture (assembly discipline — CLAUDE.md §5/§6)

All new code lives in **`DeNelle.Village`** (it drives `Enemy`/`EnemyBrain`, which are Village; verified `DeNelle.Village.asmdef` references `DeNelle.Core`). **Village → Core only.** No HUD reference; any cross-module call uses `CoreServices.*?.` with `?.`. **No `System.Reflection` introduced** (CLAUDE.md §10). New components are **additive MonoBehaviours / a pure helper** — no edits to `Enemy.cs` or `EnemyBrain.cs`.

### Component split (responsibilities)

| New type | Assembly / path | Responsibility |
|---|---|---|
| **`FormationShape`** (enum) | `DeNelle.Village` — `Assets/_Modules/Village/Families/FormationShape.cs` | The 5 dynamic shapes: `LooseCircle=0, Wedge=1, Line=2, TightPack=3, Column=4`. Append-only (serialized). Pure data. (Lives in Village; if a future HUD/save needs it, promote to Core then — not now.) |
| **`FormationContext`** (enum) | same file or `Families/FormationContext.cs` | `{ Roam=0, Engage=1, Flee=2 }` — the high-level posture that picks a shape. Separate from `EnemyTacticalState`. |
| **`FormationController`** (pure helper, NOT a MonoBehaviour) | `DeNelle.Village` — `Assets/_Modules/Village/Families/FormationController.cs` | **Computes slot offsets.** Stateless `static` (or a small cached instance owned by `FamilyLeader`): given `(FormationShape, slotIndex, slotCount, leaderForward, leaderRight)` returns a **leader-LOCAL offset** (forward+right plane). Owns the per-shape slot-generation functions (§3), even distribution, deterministic per-slot noise. **No Unity scene dependency** beyond `Vector3`/`Quaternion` — unit-testable. |
| **`FamilyLeader`** (MonoBehaviour) | `DeNelle.Village` — `Assets/_Modules/Village/Families/FamilyLeader.cs` | **The leader.** Attached alongside `Enemy`(+`EnemyBrain`) on the lead enemy. Owns the **roster of `FamilyMember`s + their slot indices**, the **current `FormationShape`/`FormationContext`**, and the **cached slot-offset table**. Each tick: reads its own `EnemyBrain` engage target → picks context (§4) → if shape changed, recomputes + caches slots (lerping offsets, §4/§5) → publishes each member's **desired world slot** (leader pos + leader-rotation × cached local offset). The leader itself pathfinds normally via its own `Enemy`/`EnemyBrain` (untouched). Recalc gated on formation-change or significant leader move (§6). |
| **`FamilyMember`** (MonoBehaviour) | `DeNelle.Village` — `Assets/_Modules/Village/Families/FamilyMember.cs` | **One follower.** Attached alongside `Enemy` (+optionally `EnemyBrain`, see §1 ownership). Holds a back-ref to its `FamilyLeader` + its `slotIndex`. Each tick: reads its assigned desired world slot from the leader → applies the **reposition threshold** (§5) → `NavMesh.SamplePosition` the slot (fallback to leader pos on failure, §5) → calls **`Enemy.SetBrainTargetPosition(sampledSlot)`**. It is the **sole** `SetBrainTargetPosition` writer while following. Rotation rule per §5. |
| **`FamilyTestSpawner`** (dev MonoBehaviour) | `DeNelle.Village` — `Assets/_Modules/Village/Families/FamilyTestSpawner.cs` | Dev-only **'K'** hotkey (mirrors `EnemyFamilyTestSpawner` 'J'): code-builds 1 leader + N followers as capsule `Enemy`s in the Village, assigns slots, lets the owner SEE the formation move/switch. No scene/prefab/SO. (§7) |

### The ownership rule (the load-bearing decision)

`SetBrainTargetPosition` has exactly **one** writer per `Enemy` at any instant.
- **Leader:** writer = its `EnemyBrain` (normal targeting/tactics — unchanged).
- **Follower (in formation):** writer = its `FamilyMember`. To avoid a duel with `EnemyBrain` (which also writes every frame, `EnemyBrain.cs:233`), the follower's `EnemyBrain` is **disabled while in formation** (`brain.enabled = false`) — its `OnDisable` already clears the override (`EnemyBrain.cs:240`), so `FamilyMember` is left as the clean sole writer. When a follower **breaks formation to engage** (its own aggro), `FamilyMember` re-enables the brain and stops writing for that enemy (it yields). Document this handoff explicitly in `FamilyMember`. (A future Behavior-tree family brain — separate WO — may replace this enable/disable toggle; keep the seam simple now.)

### How a follower is assigned to a leader + slot

The **spawner** (dev `FamilyTestSpawner` now; `RaidDirector`/`EnemyGroupSpawner` later) is responsible for assembly:
1. Spawn the leader `Enemy`, add `FamilyLeader`.
2. For each follower `Enemy`: add `FamilyMember`, call `leader.RegisterMember(member)` → leader assigns the **next free `slotIndex`** (0-based, dense) and stores it on the member.
3. On a member's death (`Enemy.Died`), `FamilyLeader.UnregisterMember` frees its slot and **re-packs** indices (or marks the slot empty and recomputes distribution) so the shape stays even. If the **leader** dies, the family either **promotes a member to leader** (simplest: nearest member) or **disbands** (each survivor re-enables its `EnemyBrain` and reverts to solo). Ship **disband** for v1 (promotion is a later refinement — flag for owner).

---

## 2. Formation shapes as DATA — `FormationShape` + slot generation (§ the 5 types)

`FormationController` exposes one function:

```
Vector3 LocalSlotOffset(FormationShape shape, int slotIndex, int slotCount, float spacing)
```

returning an offset in the **leader's local frame** (x = right, z = forward, y = 0). `FamilyLeader` converts to world via `leaderPos + leaderRotation * offset`. All shapes **evenly distribute** the `slotCount` members and add a small **deterministic per-slot noise** (seed = `slotIndex`) so the pack isn't robotic — noise magnitude ≈ `0.15 * spacing`, applied in the local plane.

| `FormationShape` | Context | Slot math (leader-local, polar or grid) |
|---|---|---|
| **`LooseCircle=0`** (roam) | Roam | **Polar ring** behind/around the leader. `angle = startAngle + i * (arc / slotCount)`, `radius = ringRadius`. Even angular distribution; members orbit at a loose radius so the pack reads as a wandering family, not a column. + noise. |
| **`Wedge=1`** (charge) | Engage | **Arrowhead** opening *behind* the leader (leader is the tip). Split members left/right of the centre-line; each successive pair steps back (−z) and wider (±x): `x = side * ((rank+1) * spacing)`, `z = -(rank+1) * spacing`. Even split L/R. + noise. (Mirrors the *intent* of `WaveEnemyGroup` Wedge but in leader-local, continuously tracked.) |
| **`Line=2`** (wide AoE front) | Engage (wide) | **Skirmish row** abreast of the leader: `x = (i - (slotCount-1)/2) * spacing`, `z = 0`. Even, centred. Presents a wide front. + noise. |
| **`TightPack=3`** (retreat/protect) | Flee | **Dense cluster** close behind the leader: small `radius`, packed grid or tight polar with **reduced spacing** (≈ `0.5 * spacing`) so they bunch to protect/retreat. + small noise. |
| **`Column=4`** (narrow) | Roam-narrow / corridor | **Single-file** behind the leader: `x ≈ 0`, `z = -(i+1) * spacing`. Narrow footprint (gates, bridges, choke lanes). + small lateral noise. |

Notes:
- Functions are **pure** (no allocation in the hot path — compute into a reused buffer the leader caches). Unit-testable without a scene.
- `spacing` and per-shape `ringRadius`/`arc` are **serialized fields on `FamilyLeader`** (sensible defaults), so the owner can tune in the inspector without code.

---

## 3. (covered above — slot generation lives in `FormationController`; §2 is the data spec)

---

## 4. Context switching — pick a shape from posture, LERP between shapes

`FamilyLeader` chooses a **`FormationContext`** each tick (throttled, §6), then maps it to a `FormationShape`:

| Context | Trigger (inputs the leader already has) | Default shape |
|---|---|---|
| **`Roam`** | Leader's `EnemyBrain` has **no live engage target** near (it's marching/wandering). | **`LooseCircle`** (or `Column` in a narrow corridor — optional, gated on a future corridor hint; default `LooseCircle`). |
| **`Engage`** | Leader's `EnemyBrain` **has a target** (hero/tower/structure) within engage range — read the leader brain's chosen target (defers to **WO-145** scoring; do NOT re-score here). | **`Wedge`** (charge the target). `Line` is the wide-front alternative for AoE-front families (a `FamilyLeader` serialized flag picks Wedge vs Line). |
| **`Flee`** | Leader (or the family) is **retreating** — read the leader's `EnemyTacticalState == Retreat` (set by WO-145/DEF-72 health-threshold logic) **or** a family-morale flag. | **`TightPack`** (bunch + protect). |

- **Reading the leader's engage target:** `FamilyLeader` gets it from its own `EnemyBrain` (the brain already computes `_currentTarget`; expose it via a small read-only `public Transform CurrentTarget => _currentTarget;` accessor on `EnemyBrain` — **the one allowed additive touch to `EnemyBrain`**, a getter, no behaviour change). If that accessor is undesirable, fall back to a proximity check the leader does itself (hero/tower within radius). Prefer the accessor; flag the choice for owner.
- **LERP, don't snap.** On a context/shape change, do **not** swap the offset table instantly. Lerp from the old cached local-offset set to the new one over `formationBlendSeconds` (serialized, ≈ 0.6 s): `current = Vector3.Lerp(old, new, t)`. Followers chase the blending slots, so the pack *flows* from circle → wedge rather than teleporting. Cache both old + new tables during the blend.

---

## 5. Stability — threshold, NavMesh-sample, fallback, rotation

`FamilyMember` per tick (this is the jitter-prevention core the owner called out):

1. **Reposition threshold.** Compute the desired world slot. If the member is **within `repositionThreshold`** (serialized, ≈ `0.6 m`) of where it's *already heading* (its last committed slot), **do not issue a new destination** — prevents micro-jitter as the leader wobbles. Only re-commit when the slot has drifted past the threshold.
2. **NavMesh-sample every desired slot.** `NavMesh.SamplePosition(slotWorld, out hit, sampleRadius≈2f, NavMesh.AllAreas)`. Use `hit.position` as the committed destination. This is the **hard NavMesh dependency** in action — off-mesh slots are snapped onto the mesh.
3. **Fallback to leader.** If `SamplePosition` **fails** (slot is off-NavMesh, e.g. over a wall/void), **move toward the leader's position directly** instead (sample the leader pos; it is on-mesh by construction). Guarantees followers never freeze or path into the void.
4. **Drive movement via `Enemy.SetBrainTargetPosition(committedSlot)`** — never touch the `NavMeshAgent` directly (no second nav path; respects `Enemy`'s throttle/avoidance, `Enemy.cs:469-480`).
5. **Rotation rule.** While **moving** (agent velocity above a small epsilon), let `Enemy`/`NavMeshAgent` face velocity (default — do nothing). While **stationary at slot** (arrived, low velocity), **match the leader's facing** so the formation reads as a unit (set the agent's `updateRotation=false` only while snapping facing, then restore — or lerp `transform.rotation` toward `leader.rotation` when speed ≈ 0). Keep it simple: lerp toward leader facing only when stopped.
6. **Avoidance/separation.** Rely on `NavMeshAgent`'s built-in avoidance (already on; `Enemy` randomises `avoidancePriority` `Enemy.cs:308`). Optional light **separation** nudge (push apart if two members' slots overlap) is allowed but **keep it off by default** (serialized toggle) — avoidance usually suffices; separation can fight the slot target.

---

## 6. Performance — recalc gating + caching

- **Slot offsets are cached** on `FamilyLeader`. Recompute the local-offset table **only when**:
  - the `FormationShape` changes (context switch — and then blend, §4), **or**
  - `slotCount` changes (a member died / joined), **or**
  - (during a blend) each frame until the blend completes.
- **Do NOT recompute local offsets every frame** when the shape is stable — they're leader-LOCAL and constant; only the world conversion (`leaderPos + leaderRot * offset`) is per-frame and cheap.
- **Throttle context evaluation** — re-pick `FormationContext` on a timer (≈ every 0.5 s), not every frame (mirror `EnemyBrain`'s `_targetEvalTimer` pattern, `EnemyBrain.cs:122-124`).
- **Significant-move gate for publishing slots:** the leader re-publishes member world-slots only when it has moved past a small delta or rotated past a small angle since last publish (mirrors `Enemy`'s `_pathMinMoveDelta` throttle philosophy, `Enemy.cs:125`). Combined with the per-member reposition threshold (§5.1), a stationary family issues almost no `SetDestination` calls.
- No per-frame allocations: reuse offset buffers (`Vector3[]` sized to max members), no LINQ in `Update`.

---

## 7. Integration & test path (no scene edit, Village-first)

- **Spawner seam:** the production assembler is **WO-143's `RaidDirector`** (not built) / `EnemyGroupSpawner`. This WO does **not** build them — it ships the components they will wire (`FamilyLeader.RegisterMember`, slot assignment) and a **dev spawner** so it's testable now.
- **`FamilyTestSpawner` (dev):** mirror `EnemyFamilyTestSpawner.cs` exactly — self-bootstrapping DDOL, **'K'** key in the **Village** scene, code-built capsule `Enemy`s (leader + ~5 followers), NavMesh-snap each spawn, add `FamilyLeader`/`FamilyMember`, assign slots. Add a **second dev key to cycle `FormationShape`** (e.g. 'L') so the owner can watch circle→wedge→line→pack→column live and confirm the LERP + stability. Logs the hotkeys on scene load.
- **Leader target / engage context defers to WO-145** — formation only reads "is there a target / am I retreating". No scoring code here.

---

## Files to Create

- `Assets/_Modules/Village/Families/FormationShape.cs` — `enum FormationShape` (+ `FormationContext`), `DeNelle.Village`.
- `Assets/_Modules/Village/Families/FormationController.cs` — pure slot-offset helper (the 5 shape functions, even distribution, noise).
- `Assets/_Modules/Village/Families/FamilyLeader.cs` — roster, context pick, shape select + blend, slot cache, per-frame world-slot publish, member register/unregister, leader-death disband.
- `Assets/_Modules/Village/Families/FamilyMember.cs` — slot read, reposition threshold, `NavMesh.SamplePosition` + leader fallback, `Enemy.SetBrainTargetPosition`, rotation rule, brain enable/disable handoff.
- `Assets/_Modules/Village/Families/FamilyTestSpawner.cs` — dev 'K'/'L' hotkey spawner + shape cycler (mirrors `EnemyFamilyTestSpawner.cs`).
- Matching `.meta` files are created by Unity on import — do not hand-author.

## Files to Edit

- `Assets/_Modules/Village/Enemies/EnemyBrain.cs` — **ONE additive change only**: a read-only `public Transform CurrentTarget => _currentTarget;` getter (the field exists at `EnemyBrain.cs:132`, written `:229`). No behaviour change. (If the umbrella architecture doc prefers a proximity-only leader, this edit can be dropped — flag for owner.)
- **No `Enemy.cs` edit.** (`SetBrainTargetPosition` already exists.)

## What NOT to touch

- `Assets/Editor/VillageSceneBuilder.cs` — **FROZEN** (CLAUDE.md §3/§9, serialization bottleneck).
- `Village.unity` / any `.unity` scene file — **never hand-edit** (CLAUDE.md §3).
- `Enemy.cs`'s `DriveNav` / `NavMeshAgent` config — followers go through `SetBrainTargetPosition` only.
- `EnemyGroupCoordinator.cs` suppress-release logic — referenced, not modified.
- `WaveEnemyGroup.SpawnFormation` / `GetFormationOffset` — distinct spawn-time enum; do not extend or repurpose.
- `EnemyTacticalState` — read-only; do not overload with formation context.
- No bake, no batchmode fired from UI; no `System.Reflection`.

---

## Acceptance Criteria

1. **Compiles green** in `DeNelle.Village` (CLI build-verifies); no new asmdef references needed (verified Village→Core already present).
2. **Village-first (the only place it can be tested today):** Enter the **Village** (baked NavMesh), press **'K'** → a leader + followers spawn. Followers hold **dynamic slots relative to the moving leader** (not the goal) — visibly a cohesive pack, not a blob.
3. **Shape cycling ('L')** switches `LooseCircle → Wedge → Line → TightPack → Column` and the pack **LERPs** between shapes (flows, does not snap).
4. **Context auto-switch:** when the leader acquires an engage target (e.g. the tagged hero comes near), the family auto-switches Roam→Engage (`LooseCircle`→`Wedge`); on leader retreat it goes `TightPack`.
5. **Stability:** followers do **not** jitter when the leader is stationary (reposition threshold holds them); a slot placed off-mesh (near a wall) **falls back to following the leader** rather than freezing or pathing into the void.
6. **Single nav writer:** followers move **only** via `Enemy.SetBrainTargetPosition` — no second `NavMeshAgent` path; a follower's `EnemyBrain` is disabled while following and cleanly re-enabled on disband/engage-break (no duelling-writer jitter).
7. **Perf:** a stationary family issues near-zero `SetDestination` calls (verified by the recalc/threshold gating); no per-frame GC alloc in the formation hot path.
8. **OPEN-WORLD IS EXPLICITLY OUT OF SCOPE / BLOCKED:** formation following in the **outer world is NOT claimed working** — it is **blocked on the WO-142 exterior NavMesh bake**. The RESULT must state "tested in Village; open-world pending WO-142 bake." Do **not** mark open-world done.

---

## Done checklist (CLAUDE.md §10)

- [ ] Brace-balance check passed on every `.cs` file created/edited (CLAUDE.md §1).
- [ ] No `.unity` scene file hand-edited; `VillageSceneBuilder.cs` untouched (CLAUDE.md §3).
- [ ] No new `System.Reflection` usage introduced.
- [ ] New components live in `DeNelle.Village`; Village → Core only (no HUD/Village↔ cross-ref) (CLAUDE.md §5).
- [ ] Null-conditional `?.` on any cross-module service call (none expected here).
- [ ] Followers drive movement solely through `Enemy.SetBrainTargetPosition` (no direct `NavMeshAgent` writes).
- [ ] `FormationShape` is a NEW enum (append-only), NOT an extension of `SpawnFormation`.
- [ ] Single `SetBrainTargetPosition` writer per enemy enforced (brain disable/re-enable handoff documented).
- [ ] Hard NavMesh dependency stated; tested in Village; open-world deferred to WO-142 bake (Acceptance #8).
- [ ] If `docs/MONSTER_FAMILY_ARCHITECTURE.md` exists, component names conform to it (else `FamilyLeader`/`FamilyMember`/`FormationController` proposed).
- [ ] Acceptance criteria reviewed line by line.

> **AUDIT 2026-08-21 (agent fleet, read-only):** FIXED. Evidence: `FamilyLeader.cs, FamilyMember.cs, FormationController.cs` — formations shipped. Status was flipped from READY by the AUDIT, not by an implementation pass. The body below is left intact; if this call is wrong, the evidence cited here is what to challenge.
