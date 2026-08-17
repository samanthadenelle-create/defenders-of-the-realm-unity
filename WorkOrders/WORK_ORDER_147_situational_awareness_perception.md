<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK ORDER 147 — Shared Situational-Awareness / Perception layer (consolidate the scattered scans into one sensor the brain READS FROM)

**Status:** READY TO IMPLEMENT
**Date:** 2026-05-30
**Priority:** High — the load-bearing substrate under WO-145 (target scoring) and WO-146 (formations / "GroupPerception"). Today every `Find*` method on `EnemyBrain` runs its own `Physics.OverlapSphere`; this WO **consolidates those scattered scans into ONE throttled sensor** the existing brain reads from, then layers escalating awareness states (Unaware → Alerted → Engaged) and a single shared-group aggregate on top — so enemies "naturally become more situationally aware" with **no rewire of the brain**.
**Lane:** **Combat / AI — code only.** NO scene-file edits, NO `VillageSceneBuilder.cs` (frozen, CLAUDE.md §3/§9), NO bake fired from UI. Runs in the Combat/AI parallel lane (CLAUDE.md §9 — "EnemyBrain, ATB — code only, no scene files"); never contends with the World/Environment lane.

**Depends on / reconciles with:**
- **DEF-21 / DEF-72 (built):** `EnemyBrain` role targeting + `EnemyTacticalState` + `TacticalData` SO + `EnemyGroupCoordinator`. **REUSE — never replace.** This WO **EDITS `EnemyBrain` to consolidate its own scans** into the new sensor; it does not add a parallel brain.
- **WO-145 (Advanced Enemy Tactics — READY):** its `ScoreAndPickTarget()` scorer **consumes** the awareness cache (threat list, ally list, HP fractions). **WO-147 supplies the perception data; WO-145 supplies the scoring.** Do NOT re-spec scoring here, and WO-145 must NOT add its own second scan — its scorer reads `AwarenessSensor`'s already-built candidate lists. They share the **one** `_targetEvalTimer` throttle (§4).
- **WO-146 (Formation Movement — READY) + `docs/MONSTER_FAMILY_ARCHITECTURE.md` §3 "GroupPerception":** the family doc proposes a `GroupPerception` component ("one sees, all react") folded into `MonsterFamily`. **This WO IS that component, at group scope** — there must be exactly ONE perception system. WO-146's `FamilyLeader` reads the **aggregated** awareness this WO exposes (§5) instead of building a separate `GroupPerception`. WO-146/the family doc's `GroupPerception` line is **redirected to this WO** (flag the convergence for owner).
- **WO-143 (Roaming Raids — READY):** roamers are the same `Enemy`+`EnemyBrain` body, so they inherit this sensor for free. A `RoamingRaider`'s aggro check (WO-143 §3) may read `AwarenessSensor.State`/`HasThreat` instead of doing its own distance check — soft seam, not required by this WO.
- **WO-139 #4 (OnDisable/unsub discipline):** any event subscription added here (leader↔member aggregation) MUST unsubscribe in `OnDisable`/`OnDestroy` so no stale callbacks fire across a scene reload.

**North Star:** `docs/NORTH_STAR.md` — "DEFEND base + mines from waves **and roaming enemies** — or lose them"; threat model is `WaveManager` + `EnemyBrain`. Enemies that *perceive* — notice the hero flanking them, see an ally dying, realise they're outnumbered — are the readable, escalating menace that makes the CoC×Warcraft defend loop worth mastering. This is the "enhance the wheel" pass: same perception behaviour, one sensor, smarter.

---

## 0. RECONCILE — the WHEEL we are enhancing (the scattered scans, verified line-by-line)

The whole point (owner): *"I don't want to reinvent the wheel, I want to take our object-of-type-wheel and ENHANCE it … allowing them to naturally become more situationally aware."* The "wheel" is the perception **already inside `EnemyBrain`** — currently **five independent `OverlapSphere`/distance scans scattered across five methods**, each re-querying physics. We consolidate them into one sensor; the `Find*` methods become thin reads off its cache.

| Existing scan (the wheel) | Location (verified `EnemyBrain.cs`) | What it scans today | After WO-147 |
|---|---|---|---|
| `FindMostDamagedAlly()` | `:385-403` — `Physics.OverlapSphereNonAlloc(pos, _healScanRadius, _scanBuffer)` (`:387`), iterates `GetComponentInParent<Enemy>`, picks worst `HpFraction` | one overlap scan for wounded allies | reads `Awareness.Allies` (cached enemies + HP) — **no own scan** |
| `FindNearestStructure()` | `:407-425` — `OverlapSphereNonAlloc(pos, _threatScanRadius, _scanBuffer)` (`:409`), iterates `GetComponentInParent<IDamageableStructure>` alive | one overlap scan for nearest live structure | reads `Awareness.NearestStructure` |
| `FindNearestTower()` | `:435-453` — `OverlapSphereNonAlloc(pos, _towerScanRadius, _scanBuffer)` (`:437`), iterates `GetComponentInParent<Tower>` alive | one overlap scan for nearest live tower | reads `Awareness.NearestTower` |
| `FindHighestThreatTarget()` | `:357-366` — distance check vs cached `_heroTransform` (`:361`), else `FindNearestStructure()` | hero-in-radius else structure | reads `Awareness.Hero` (in-range flag) ?? `Awareness.NearestStructure` |
| `FindNearbyHero()` | `:375-381` — distance check vs cached `_heroTransform` (`:379`) | hero within `_heroEngageRadius` | reads `Awareness.HeroWithin(_heroEngageRadius)` |
| `FindClosestTarget()` | `:461-467` — `GameObject.FindWithTag("HeroTarget"/"HeartTarget")` | tag fallback | unchanged (cheap, no physics) — optionally folded into sensor's hero/heart resolution |
| **Cached refs** | `Awake :196-203` — `_heartTransform`, `_heroTransform` (tag `HeroTarget`/`Player`) | scene-wide refs cached once | **moved into the sensor** (it owns the cached hero/heart/pet/tower/ally facts); brain reads them via the sensor |
| **Shared scan buffer** | `:120` — `private readonly Collider[] _scanBuffer = new Collider[32];` | reused overlap buffer | **moved to / shared by the sensor** — still one 32-collider buffer, no new allocation |
| **DORMANT throttle** | `:122-124` — `_targetEvalTimer` / `const TargetEvalInterval = 2f` — **declared but UNUSED today** (verified zero read/write sites) | nothing | **WIRED** to drive the sensor's scan cadence (§4) |

**Verified facts that shape the design:**
- `Enemy` exposes everything the sensor needs to cache: `Hp` (`Enemy.cs:199`), `HpFraction` (`:205`), `IsDead` (`:208`), `Died` event (`:184`). `Tower`/structures expose `IDamageableStructure.IsAlive`.
- `EnemyBrain` already has `_currentTarget` (`:132`), the `Died` re-raise (`:172`/`:179`), and the `OnDisable` that clears the nav override (`:240`).
- `DeNelle.Village.asmdef` already references `DeNelle.Core`, `DeNelle.Pets`, `DeNelle.Audio` (verified) — so a `PetTarget` candidate can be resolved without any new asmdef edit. **No asmdef change needed.**
- There is **no** existing `Perception`/`Awareness`/`IsAlert` component anywhere in `Assets` (verified by search) — this is greenfield-safe; we are not duplicating a thing.
- `EnemyTacticalState` (Rush/Flank/Retreat/Suppressed) is the **strategic posture** (how to move). Awareness state (Unaware/Alerted/Engaged) is a **separate, orthogonal concept** (how much it has perceived). Do NOT overload `EnemyTacticalState` — new enum (§3), mirroring how the codebase already keeps `EnemyState` ≠ `EnemyTacticalState` (see `EnemyTacticalState.cs:6-8` header).

**Hard reconciliation rules:**
- **ONE perception system.** This WO is the single source. WO-146's `GroupPerception` is redirected to read this; WO-145's scorer reads this; no second scan anywhere.
- **EnemyBrain is EDITED (its scans consolidated), NOT replaced.** Same five `Find*` results, sourced from one cached scan. Behaviour for a default enemy is unchanged.
- **Append-only** on `EnemyArchetype`/`EnemyTacticalState` if touched (we add a NEW enum, touch neither).
- Reconcile, never blind-replace (memory *wo-batch-reconcile-not-replace*; *two-combat-feel-stacks*: do not add a parallel sensor).

---

## 1. Architecture (assembly discipline — CLAUDE.md §5/§6)

All behaviour in **`DeNelle.Village`** (it drives `Enemy`/`EnemyBrain`, which are Village). **One small enum** (`AwarenessState`) goes in **`DeNelle.Core`** so a future HUD/save/SO (and the Animator `IsAlert` mapping) can reference it without a Village ref. **Village → Core only** (asmdef verified). No HUD reference. Any cross-module call uses `CoreServices.*?.` with `?.` (CLAUDE.md §6). **No `System.Reflection` introduced** (CLAUDE.md §10). New runtime types are **additive components / pure data**; the only EDIT is consolidating `EnemyBrain`'s scans (the enhancement).

| Type | Assembly / path | Kind | Responsibility |
|---|---|---|---|
| `AwarenessState` (enum) | `DeNelle.Core` — `Assets/_Modules/Core/AwarenessState.cs` | Core enum | `{ Unaware=0, Alerted=1, Engaged=2 }`. Append-only. Pure data so the Animator `IsAlert` map + a future HUD can read it. |
| `PerceptionSnapshot` (struct/class) | `DeNelle.Village` — `Assets/_Modules/Village/Enemies/Perception/PerceptionSnapshot.cs` (or nested in the sensor) | pure data | The cached result of ONE scan: `Hero` (Transform + in-range flags), `Pet`, `NearestTower`, `NearestStructure`, `Heart`, `Allies` (list of `Enemy` + their `HpFraction`), and **derived facts**: `AllyCount`, `MostDamagedAlly`, `IsOutnumbered`, `IsAllyDying`, `IsHeroFocusingMe`, `ThreatCount`. Reused buffers; no per-frame alloc. |
| `AwarenessSensor` (MonoBehaviour) | `DeNelle.Village` — `Assets/_Modules/Village/Enemies/Perception/AwarenessSensor.cs` | additive component | **The consolidated sensor.** Attached alongside `Enemy`(+`EnemyBrain`). Owns the `_scanBuffer[32]`, the cached hero/pet/heart/tower refs, performs **one throttled `OverlapSphereNonAlloc`** per cadence, builds the `PerceptionSnapshot`, derives the facts, and computes the `AwarenessState` escalation (§3). Exposes read-only accessors the brain/scorer/leader read. **`[RequireComponent(typeof(Enemy))]`.** |
| `EnemyBrain` (EDIT) | `DeNelle.Village` — `Assets/_Modules/Village/Enemies/EnemyBrain.cs` | **EDITED — the enhancement** | Acquire the sensor in `Awake` (add if absent). The five `Find*` methods (§0 table) become **thin reads off the sensor cache** instead of each running its own scan. Wire the dormant `_targetEvalTimer`/`TargetEvalInterval` to drive the sensor cadence. Push `AwarenessState` → Animator `IsAlert` (§3.4). `_scanBuffer` ownership moves to / is shared with the sensor. **No role/targeting behaviour change** — same results, one scan. |

> **Pet candidate seam:** Village already references `DeNelle.Pets` (asmdef `:9`), so the sensor MAY resolve the pet directly or by a `PetTarget` tag (mirror the hero `HeroTarget`/`Player` pattern, `EnemyBrain.cs:201-203`). Keep it **null-safe** — absent pet ⇒ no pet candidate (backward-safe). The sensor only *caches* the pet as a candidate; WO-145's scorer decides whether to focus it.

---

## 2. The consolidated scan — one sensor, one buffer, one cadence

`AwarenessSensor` replaces the five scattered scans with a single periodic pass:

1. **Cache scene refs once** (`Awake`, moved from `EnemyBrain.Awake:196-203`): Heart (`FindAnyObjectByType<HeartController>`), hero (`HeroTarget` ?? `Player` tag), pet (`PetTarget` tag, null-safe). These rarely change; re-resolve lazily only if the cached ref becomes null (scene reload safety).
2. **One overlap scan per cadence** using the shared `_scanBuffer[32]` and the **widest** radius any consumer needs (max of `_threatScanRadius`/`_towerScanRadius`/`_healScanRadius`, a serialized `_perceptionRadius` with that default). A single `Physics.OverlapSphereNonAlloc(transform.position, _perceptionRadius, _scanBuffer)` call. Iterate the hits **once**, classifying each into the snapshot:
   - `GetComponentInParent<Enemy>()` alive & ≠ self ⇒ add to `Allies` (with `HpFraction`); track `MostDamagedAlly`.
   - `GetComponentInParent<Tower>()` alive ⇒ candidate for `NearestTower` (track min sqr-dist).
   - `GetComponentInParent<IDamageableStructure>()` alive ⇒ candidate for `NearestStructure`.
   - (Hero/pet/Heart come from the cached refs + a distance check, not the overlap — they're known transforms.)
   This is the **exact union** of what `FindMostDamagedAlly`/`FindNearestStructure`/`FindNearestTower` did separately — now in ONE iteration, ZERO extra allocations (same `_scanBuffer`).
3. **Derive facts** from the snapshot (cheap, no physics): `AllyCount`, `IsOutnumbered` (allies-near < a serialized threshold of perceived hostiles, or hero+pet+towers present and few allies), `IsAllyDying` (`MostDamagedAlly.HpFraction` below a serialized `_allyDyingThreshold`), `IsHeroFocusingMe` (hero present, within radius, and roughly facing this enemy — dot of hero.forward vs to-enemy > threshold; hero forward read via the cached hero transform, null-safe), `ThreatCount` (hero + pet + live towers in radius).
4. **Compute `AwarenessState`** (§3) from the derived facts + decay timer.

**Per-frame cost when NOT scanning:** the sensor's `Update` only decrements timers and, if a scan isn't due, returns. The brain reads the **last** snapshot. No physics, no alloc between scans.

---

## 3. Escalating awareness states — "naturally more situationally aware"

`AwarenessState { Unaware=0, Alerted=1, Engaged=2 }` (Core). The sensor owns the state; responsiveness scales with what it has perceived. **Orthogonal to `EnemyTacticalState`** (posture) — do not merge.

### 3.1 States
| State | Meaning | Behavioural effect (read by consumers; this WO does not change targeting) |
|---|---|---|
| `Unaware=0` | No hero/pet/threat perceived; marching/roaming. | Sensor scans on the **slow** cadence (LOD, §4); brain runs default role targeting. `IsAlert` Animator param = false. |
| `Alerted=1` | A threat was *perceived* (hero/pet entered radius, an ally died/took damage nearby, or shared-alert from family §5) but not yet engaging. | Sensor scans on the **fast** cadence (it cares now); `IsAlert` = true (alert idle / head-turn pose). The scorer (WO-145) may already pick a target; this is the "notices you" beat before commit. |
| `Engaged=2` | This enemy has a committed offensive target in range (brain's `_currentTarget` is a live hostile within engage range) **or** is taking damage. | Fast cadence; `IsAlert` = true. Full combat responsiveness. |

### 3.2 Escalation triggers (Unaware → Alerted → Engaged)
- **Unaware → Alerted** when ANY of: hero within `_perceptionRadius`; pet within `_perceptionRadius`; `IsAllyDying` true; this enemy took damage since last scan (hook: a cheap "was damaged" flag — see §3.5); or a shared family alert arrives (§5).
- **Alerted → Engaged** when the brain has a live offensive `_currentTarget` within its engage range (the sensor reads `EnemyBrain.CurrentTarget` — see §3.5 accessor) OR the enemy is in melee/contact (Enemy is locked onto a structure / taking hits).
- **Engaged → Alerted** when no live committed target remains in range but threats are still perceived.
- **Alerted → Unaware** by **decay** (§3.3) — no trigger has fired for `_alertDecaySeconds`.

### 3.3 Decay (so awareness is not a one-way latch)
A serialized `_alertDecaySeconds` (≈ 4 s, owner-tunable). Each scan, if no escalation trigger is currently true, decrement a decay timer; when it hits zero, step **down** one state (Engaged→Alerted→Unaware). Any fresh trigger **resets** the timer and re-escalates. This makes an enemy that lost sight of the hero gradually "calm down" and resume marching — natural, not binary. Mirror the existing `_suppressTimer` decay idiom (`EnemyBrain.cs:252-254`).

### 3.4 Feed the Animator `IsAlert` param (owner's note)
Add a bool Animator param drive: when `AwarenessState >= Alerted`, set `IsAlert = true`, else false. Use the **null-guarded Animator pattern already in the codebase** (`Enemy.cs:171-181` hashes via `Animator.StringToHash`, every `Set*` null-checked). The sensor (or the brain, whichever holds the Animator ref — prefer the brain, it already caches `_animator` at `EnemyBrain.cs:185`) pushes `IsAlert` only on state change (not per-frame). If the controller has no `IsAlert` param the call is a harmless no-op (Unity ignores unknown params) — **backward-safe for every existing enemy prefab**. (Adding the actual `IsAlert` param + alert-idle state to the Animator controllers is an AnimatorSetup follow-up, flagged for owner — this WO only DRIVES the param.)

### 3.5 The two tiny additive `EnemyBrain` accessors (the only new surface)
- `public Transform CurrentTarget => _currentTarget;` — read-only getter (field exists `:132`). **This is the same accessor WO-146 §"Files to Edit" already requested** — define it ONCE here; WO-146 consumes it. No behaviour change.
- A cheap "recently damaged" signal so the sensor can escalate on being hit. Preferred: the sensor subscribes to a damage hook. `Enemy.TakeDamageFrom` (`Enemy.cs:634`) is where damage lands but `Enemy` has no `Damaged` event today. **Two options, owner picks (flag in RESULT):** (a) the sensor polls `Enemy.HpFraction` deltas across scans (zero `Enemy` edit, slightly coarse — recommended for v1); or (b) add a minimal additive `event Action<float> Damaged;` on `Enemy` raised in `TakeDamageFrom` (cleaner, one additive line). **Default to (a)** to keep `Enemy.cs` untouched.

---

## 4. Wire the DORMANT throttle (cadence + LOD)

`_targetEvalTimer` / `const TargetEvalInterval = 2f` at `EnemyBrain.cs:122-124` are **declared but unused** (verified). Wire them to drive the sensor cadence per the owner's "limit perception checks with cooldowns":

- The sensor exposes `Scan()` (the §2 pass). The **brain** (which owns the dormant timer) decrements `_targetEvalTimer` each `Update`; when it expires it calls `_sensor.Scan()` and resets the timer to the current cadence. Between scans, brain `Find*` reads the cached snapshot. This is the literal enhancement of the dead field — no new throttle field where one already exists.
- **WO-145 shares this same throttle.** The scorer re-scores only when a fresh snapshot is available (same `_targetEvalTimer` tick) — one scan + one score per interval, not two systems each on their own clock. Cache `_currentTarget` between ticks (prevents target-thrash; the WO-145 concern).
- **LOD cadence (optional, owner-tunable):** the cadence is **state-** and **distance-aware:
  - `Unaware` and far from the hero/Heart ⇒ **slow** cadence (e.g. `TargetEvalInterval` × 2–3, a serialized `_lodFarMultiplier`). A distant idle enemy barely scans.
  - `Alerted`/`Engaged` or near the hero ⇒ **fast** cadence (`TargetEvalInterval`, ≈ 2 s or tighter).
  - Distance band uses the already-cached hero/Heart ref (cheap sqr-dist), no extra scan.
- Reuse the **one** `_scanBuffer[32]` (now sensor-owned) — no new allocation, the existing perf contract (DEF-56 throttle philosophy, `Enemy.cs:111-125`) preserved.

---

## 5. SHARED group awareness — unify with WO-146's "GroupPerception" (ONE system)

`docs/MONSTER_FAMILY_ARCHITECTURE.md` §3 proposes a `GroupPerception` ("one sees, all react"). **This WO is that system; there is no separate `GroupPerception`.** Group awareness = the same `AwarenessSensor` aggregated at family scope, with the leader as aggregator.

- **Per-member sensor stays the unit of perception.** Each `Enemy` has its `AwarenessSensor` (the single scan). The family does NOT add a second scanner.
- **Leader aggregates.** WO-146's `FamilyLeader` holds the member roster; it reads each member's `AwarenessSensor` (via the accessors) and computes a **family aggregate**: union of perceived threats, max awareness state, `AnyMemberEngaged`, `AnyAllyDying`. Expose this as a tiny read surface on the sensor/leader (e.g. `AwarenessSensor.SharedState` settable by the leader, or `FamilyLeader.AggregateAwareness`).
- **Propagation ("one sees → all react"):** when the leader's aggregate reaches `Alerted`/`Engaged`, it **pushes a shared-alert** down to members: `member.Sensor.RaiseSharedAlert(threat)` bumps a member's state to at least `Alerted` and seeds its snapshot with the shared threat. This is the §3.2 "shared family alert arrives" escalation trigger. So a rear member that hasn't personally seen the hero still reacts because a front member did — **achieved by aggregating the existing per-member sensors, not a parallel group scanner.**
- **Throttle the aggregation** on the leader's own cadence (mirror `_targetEvalTimer`, ~0.5–2 s — WO-146 §6 already calls for this), NOT per-frame. One scan per member per cadence + one cheap aggregation per family — the perf win the family doc §8 describes, delivered by THIS sensor.
- **OnDisable discipline (WO-139 #4):** if the leader subscribes to member sensors/`Died`, it MUST unsubscribe in `OnDisable`/`OnDestroy`; `RaiseSharedAlert` must null-guard a torn-down member. No stale callbacks across reload.
- **Solo enemies (no family):** `SharedState` simply equals the enemy's own state — the aggregation is a no-op overlay. A wave enemy with no `FamilyLeader` behaves exactly as its own sensor dictates (backward-safe).

> **Convergence flag for owner:** WO-146's "Files to Edit" and the family doc's `GroupPerception` line are **superseded by WO-147** — the family layer reads this sensor's aggregate instead of building `GroupPerception`. WO-146 keeps `FamilyLeader`/`FamilyMember`/`FormationController`; only its perception responsibility moves here. Update WO-146 to depend on WO-147 (or note the redirect).

---

## 6. Integration seams (who reads the sensor)

| Consumer | Reads | Replaces / how |
|---|---|---|
| **`EnemyBrain` Find\*** (this WO) | `Awareness.Hero/Pet/NearestTower/NearestStructure/MostDamagedAlly/Heart` | the five `OverlapSphere`/distance scans (§0) — now thin cache reads. **Same results.** |
| **WO-145 `ScoreAndPickTarget`** | `Awareness` candidate lists (threats: hero/pet/towers/Heart; `HpFraction`s for the LowHp term) + `IsHeroFocusingMe`/`ThreatCount` as scoring inputs | the scorer iterates the sensor's already-built candidate set instead of re-scanning; uses the SAME `_targetEvalTimer` tick. |
| **WO-146 `FamilyLeader`** | `member.Sensor.State`/perceived threats → aggregate; pushes `RaiseSharedAlert` back | the family's perception = aggregation of member sensors; no separate `GroupPerception`. Also reads `EnemyBrain.CurrentTarget` (the §3.5 accessor) for engage context. |
| **WO-143 `RoamingRaider`** (soft) | `Awareness.State`/`HasThreat` for its roam→aggro switch | optional: replace its own distance check with a sensor read. Not required by this WO. |
| **Animator** | `IsAlert` bool driven from `AwarenessState >= Alerted` | new param drive (§3.4), null-safe no-op if param absent. |

---

## 7. Performance (owner perf notes)

- **One throttled `OverlapSphereNonAlloc` per enemy per cadence**, using the single existing `_scanBuffer[32]` (now sensor-owned) — replaces up to **three** separate overlap scans (`FindMostDamagedAlly` + `FindNearestStructure` + `FindNearestTower`) that today can run in a single brain frame. Net **fewer** physics queries than the wheel it enhances.
- **State/distance LOD cadence** (§4): distant `Unaware` enemies scan 2–3× less often. Near/alerted/engaged enemies scan on the tight interval.
- **No per-frame allocations** in the hot path: reused snapshot buffers (`List<Enemy>` allies list cleared+refilled, not re-newed; reused `_scanBuffer`). No LINQ in `Update`/`Scan`.
- **Group aggregation** is one cheap pass per family per cadence (no extra physics), per WO-146 §6 / family doc §8 — the documented perf win.
- Shares the device enemy budget already governed by `WaveManager._maxSimultaneousEnemies` / WO-143 `_maxLiveRaiders` (PerfBudgetWindow / DEF-48) — the sensor adds no new spawn, only consolidates existing work.

---

## 8. Files to EDIT / CREATE

**CREATE (3 files):**
- `Assets/_Modules/Core/AwarenessState.cs` — `enum AwarenessState { Unaware=0, Alerted=1, Engaged=2 }` in `DeNelle.Core` (append-only, XML docs, note orthogonality to `EnemyTacticalState`).
- `Assets/_Modules/Village/Enemies/Perception/AwarenessSensor.cs` — the consolidated sensor (`DeNelle.Village`): owns `_scanBuffer[32]` + cached hero/pet/heart refs; `Scan()` builds `PerceptionSnapshot`; derives facts; computes + decays `AwarenessState`; `RaiseSharedAlert(...)`; read-only accessors. `[RequireComponent(typeof(Enemy))]`.
- `Assets/_Modules/Village/Enemies/Perception/PerceptionSnapshot.cs` — the cached snapshot data type (`DeNelle.Village`). (MAY be nested in `AwarenessSensor.cs` instead — integrator's call; keep it Village.)
- Matching `.meta` files are generated by Unity on import — do not hand-author.

**EDIT (1 file — the enhancement, the WHOLE point of this WO):**
- `Assets/_Modules/Village/Enemies/EnemyBrain.cs` — **consolidate its scans into the sensor:**
  - `Awake`: acquire/`AddComponent` `AwarenessSensor`; move the cached hero/Heart resolution (`:196-203`) into the sensor (brain reads via sensor).
  - Rewrite the five `Find*` methods (`:357,375,385,407,435`) to **read the sensor's cache** instead of running `OverlapSphere`/distance scans. `ChooseTarget()` (`:336`) is unchanged — it still calls `Find*`, which now return cached results. **Same behaviour, one scan.**
  - Wire `_targetEvalTimer`/`TargetEvalInterval` (`:122-124`) to drive `_sensor.Scan()` cadence + the §4 LOD multiplier.
  - Add `public Transform CurrentTarget => _currentTarget;` (§3.5).
  - Push `AwarenessState` → Animator `IsAlert` on state change (§3.4), using the cached `_animator` (`:185`), null-safe.
  - `_scanBuffer` (`:120`) moves to / is shared with the sensor (do not keep two buffers).
  - **Do NOT change role/targeting logic, `ComputeTacticalDestination`, tactical states, or the BT yield path.** Pure consolidation.

**No `Enemy.cs` edit** in the default plan (the §3.5 `Damaged` event is option (b), owner-gated; v1 uses HP-delta polling — zero `Enemy` edit).

---

## 9. What NOT to touch

- ❌ `VillageSceneBuilder.cs` — frozen serialization bottleneck (CLAUDE.md §3/§9).
- ❌ Any `.unity` scene file — no hand-edits, no bake fired from UI (CLAUDE.md §3).
- ❌ `Enemy.cs` — no edit in the default plan (HP-delta polling avoids it). If owner picks the §3.5 `Damaged` event, that is ONE additive line, flagged first.
- ❌ `EnemyBrain.ChooseTarget` / `ComputeTacticalDestination` / `UpdateTacticalState` / tactical-state behaviour / the `EnemyBehaviorTree` yield path — unchanged. Only the SCAN SOURCE inside `Find*` changes.
- ❌ Do NOT add a second/parallel perception system. This is the ONE sensor (WO-146 `GroupPerception` redirects here).
- ❌ Do NOT renumber any existing enum (`EnemyTacticalState` 0-3, `EnemyArchetype` 0-5). New `AwarenessState` is append-only.
- ❌ Do NOT overload `EnemyTacticalState` with awareness — separate enum (orthogonal, like `EnemyState` ≠ `EnemyTacticalState`).
- ❌ No new `System.Reflection` (CLAUDE.md §10).
- ❌ No HUD edit; no Animator-controller authoring here (driving `IsAlert` only; param/state authoring is an AnimatorSetup follow-up, flagged).
- ❌ Do NOT add an asmdef reference — Village already references Core/Pets/Audio (verified); none needed.

---

## 10. Acceptance criteria

1. `AwarenessState` enum present in `DeNelle.Core` (`Unaware=0, Alerted=1, Engaged=2`); compiles; documented orthogonal to `EnemyTacticalState`.
2. `AwarenessSensor` compiles in `DeNelle.Village`, `[RequireComponent(typeof(Enemy))]`, performs **one** `OverlapSphereNonAlloc` per cadence using a single 32-collider buffer, and builds a `PerceptionSnapshot` (hero/pet/towers/structure/Heart/allies+HP + derived `IsOutnumbered`/`IsAllyDying`/`IsHeroFocusingMe`/`ThreatCount`).
3. **Scans consolidated (the enhancement):** `EnemyBrain.FindMostDamagedAlly`/`FindNearestStructure`/`FindNearestTower`/`FindHighestThreatTarget`/`FindNearbyHero` no longer call `Physics.OverlapSphere`/own distance scans — they read the sensor cache. Verifiable by reference search (no `OverlapSphere` left in `EnemyBrain` outside the sensor). A default enemy's targeting behaviour is **unchanged** (same target picks as before).
4. **Dormant throttle wired:** `_targetEvalTimer`/`TargetEvalInterval` (formerly unused, `EnemyBrain.cs:122-124`) now drive the sensor cadence; verifiable by reference search (no longer dead).
5. **Escalation + decay:** an enemy steps Unaware→Alerted when the hero/pet enters radius / an ally is dying / it takes damage / a shared alert arrives; Alerted→Engaged on a committed in-range target; and **decays back down** after `_alertDecaySeconds` with no trigger. State transitions are observable (log or inspector).
6. **`IsAlert` Animator drive:** `IsAlert` is set true when `AwarenessState >= Alerted`, pushed on state change, null-safe (no error on prefabs whose controller lacks the param).
7. **Shared group awareness = ONE system:** WO-146's `FamilyLeader` reads the per-member sensors and aggregates; `RaiseSharedAlert` propagates the leader's aggregate so a member that didn't personally perceive the threat escalates to ≥ Alerted. **No separate `GroupPerception` component exists.** A solo enemy's `SharedState` == its own state (no-op overlay).
8. **WO-145 seam:** the scorer reads the sensor's candidate lists + facts on the SAME `_targetEvalTimer` tick (one scan + one score per interval); no second scan introduced by WO-145.
9. **Perf / LOD:** distant `Unaware` enemies scan less often (state/distance LOD); no per-frame GC alloc in `Scan`/`Update`; net physics queries ≤ the pre-WO scattered-scan count.
10. **No regression:** an enemy with no `AwarenessSensor` author-assigned still gets one auto-added in `Awake`; an enemy with no family behaves exactly as before (Heart-march + role targeting). No `.unity`/builder/scene edits.

---

## 11. Done checklist (CLAUDE.md §10)

- [ ] Brace-balance check passed on every `.cs` file created/edited (`AwarenessState.cs`, `AwarenessSensor.cs`, `PerceptionSnapshot.cs`, `EnemyBrain.cs`).
- [ ] No `.unity` scene file hand-edited; `VillageSceneBuilder.cs` untouched; no bake fired from UI (CLAUDE.md §3/§9).
- [ ] No new `System.Reflection` introduced (CLAUDE.md §10).
- [ ] `using DeNelle.Core.Combat;` present where `IDamageableStructure` is referenced (sensor) — already in `EnemyBrain.cs:47`.
- [ ] Null-conditional `?.` on all cross-module/service calls (pet/hero/Heart/Animator/leader resolution null-safe).
- [ ] `AwarenessState` append-only; `EnemyTacticalState`/`EnemyArchetype` NOT renumbered or overloaded.
- [ ] `_targetEvalTimer`/`TargetEvalInterval` confirmed READ/USED (no longer dead); the five `Find*` confirmed to no longer run their own `OverlapSphere` (reference search).
- [ ] `EnemyBrain` is EDITED (scans consolidated) — confirmed it is NOT a parallel system; targeting behaviour unchanged for a default enemy.
- [ ] WO-146 `GroupPerception` convergence flagged for owner (family reads THIS sensor; no second perception).
- [ ] §3.5 `Damaged`-hook choice (HP-delta poll vs additive `Enemy.Damaged` event) flagged for owner in RESULT (default: poll, zero `Enemy` edit).
- [ ] `IsAlert` Animator-param authoring noted as an AnimatorSetup follow-up (this WO only drives it).
- [ ] No asmdef reference added (Village→Core/Pets/Audio already present).
- [ ] Acceptance criteria 1-10 reviewed line by line.
- [ ] CLI build-verifies in batchmode; saves `WORK_ORDER_147_situational_awareness_perception.RESULT.md`.
