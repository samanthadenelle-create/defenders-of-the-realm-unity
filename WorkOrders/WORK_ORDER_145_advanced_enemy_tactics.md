<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK ORDER 145 — Advanced Enemy Tactics: smart focus-fire, kiting, coordinated pincer, reposition

**Status:** DONE — audit-verified as shipped (2026-08-21 backlog audit).
**Date:** 2026-05-30
**Priority:** High — turns the DEFEND half of the loop from "a swarm walks at the Heart" into "a squad that picks off your healer/pet, kites your hero, and envelops your line." The visible payoff of the role/tactical scaffolding already shipped (DEF-21/DEF-72).
**Lane:** **Combat / AI — code only.** NO scene-file edits, NO `VillageSceneBuilder.cs` (frozen, CLAUDE.md §3/§9), NO bake fired from UI. Runs in the Combat/AI parallel lane (CLAUDE.md §9 — "EnemyBrain, ATB — code only, no scene files"). Never contends with the World/Environment lane.

**Depends on:**
- **DEF-21 / DEF-72** (built): `EnemyBrain` role targeting + `EnemyTacticalState` Rush/Flank/Retreat/Suppressed + `TacticalData` SO + `EnemyGroupCoordinator`. **REUSE — never replace.**
- **WO-143** (Roaming Raids — just written, READY): raiders reuse `Enemy`+`EnemyBrain`+`EnemyGroupSpawner`. These tactics apply to raiders too (a raid party is a `WaveEnemyGroup`, so the new `TacticalData` fields + pincer apply unchanged). Soft: WO-145 does not edit WO-143's `RaidDirector`/`RoamingRaider`; they inherit the new brain behaviour for free.
- **WO-139 #7** (`EnemyBrain.TryAttack()` declared-but-unimplemented, `EnemyBrain.cs:161-166` — the doc-comment hook is the no-op `TriggerAttack()`): **the kiting tactic (§3) depends on a ranged attack existing.** This WO includes a **minimal ranged attack** scoped to close #7 for the kite path only (see §3.4). Reconcile, don't fork.
- **WO-128** (pet anti-ranged ability spec): proposes moving `EnemyRole` to `DeNelle.Core.Combat`. NOT a hard dep — WO-145 keeps `EnemyRole` where it lives today (`WaveEnemyGroup.cs`, Village). If WO-128 lands the Core move first, this WO's target-scoring code just changes its `using`. Flag overlap for owner; do not race the enum move here.

**North Star:** `docs/NORTH_STAR.md` — "DEFEND base + mines from waves and roaming enemies — or lose them"; the threat model is `WaveManager` + `EnemyBrain`. Smarter enemy AI is the difficulty/skill curve that makes the defend loop worth mastering (CoC×Warcraft skill ceiling).

---

## 0. RECONCILE — what already EXISTS (read before writing a line; project trap #1)

Verified by full read of all six files below. **Build additively on every one of these — none is replaced.**

| System | Where (verified) | What it already does | How WO-145 extends it |
|---|---|---|---|
| **`EnemyRole`** | `Assets/_Modules/Village/Waves/WaveEnemyGroup.cs:43-73` | `enum { Tank=0, Healer=1, DPS=2, Ranged=3, MiniBoss=4 }`. Drives `EnemyBrain.ChooseTarget`. | REUSE. No new role values. The Ranged role gains real kiting (today its doc-comment says "Same nav logic as DPS today — extended in the behaviour-tree pass"; this is that pass). |
| **`EnemyBrain`** | `Assets/_Modules/Village/Enemies/EnemyBrain.cs` | `Role` field; `ChooseTarget()` (`:336`) role switch; `FindHighestThreatTarget` (`:357`), `FindMostDamagedAlly` (`:385`), `FindNearestStructure` (`:407`), `FindNearestTower` (`:435`), `FindNearbyHero` (`:375`), `FindClosestTarget` (`:461`); `ComputeTacticalDestination()` (`:287`) Rush/Flank/Retreat switch; `UpdateTacticalState()` (`:247`); `SetTacticalState()` (`:278`); `TriggerAttack()` no-op (`:161`); caches `_heartTransform`/`_heroTransform` in `Awake` (`:196-203`); `_scanBuffer[32]` (`:120`); throttle scaffold `_targetEvalTimer`/`TargetEvalInterval=2f` (`:122-124` — **declared but UNUSED today**). | EXTEND. Add a **weighted target-scoring selector** (§2) used by the existing `ChooseTarget` switch; add **Kite** + **Reposition** cases to the `ComputeTacticalDestination` switch (§3, §5); wire the dormant `_targetEvalTimer` to throttle scoring. |
| **`EnemyTacticalState`** | `Assets/_Modules/Village/Enemies/EnemyTacticalState.cs` | `enum { Rush=0, Flank=1, Retreat=2, Suppressed=3 }`. | EXTEND additively (§6) — append `Kite=4`, `Reposition=5`. **Do NOT renumber 0-3.** |
| **`TacticalData`** | `Assets/_Modules/Core/Data/TacticalData.cs` (`DeNelle.Core.Data`) | SO with `Archetype`, `FlankAngleOffset`, `RetreatHealthThreshold`, `SuppressDelay`, **`TargetPriorityBias` (`:90` — declared but UNUSED; verified zero read sites)**. `EnemyArchetype { Standard=0, Flanker=1, Siege=2, Flyer=3, Support=4, Boss=5 }` (`:32`). | EXTEND additively (§6). Add scoring-weight + kite-band + rally fields. Wire `TargetPriorityBias`. Append `Kiter=6` to `EnemyArchetype`. **Do NOT renumber existing 0-5.** This is the one Core file touched (data only). |
| **`EnemyGroupCoordinator`** | `Assets/_Modules/Village/Waves/EnemyGroupCoordinator.cs` | Per-group suppress→release. `_members` list (`:39`), `RegisterMember` (`:64`), `FinaliseGroup` (`:84`), `ReleaseAll` (`:113`, only overrides Suppressed→Rush). Self-destructs after release. | EXTEND (§4) — `ReleaseAll` assigns **distinct flank angles** across the group (left/right/rear envelope) instead of every Flanker using its own static `FlankAngleOffset`. The coordinator is the ONLY seam where a *coordinated* (vs per-enemy-random) pincer can be authored. |
| **`Enemy`** | `Assets/_Modules/Village/Enemies/Enemy.cs` | `NavMeshAgent` march; HP; `TickContactAttack` (`:500`, melee-only via `ProbeForStructure` SphereCast `:587`); `SetBrainTargetPosition` (`:229`); `SetBrainTarget` (`:220`); `Heal` (`:235`); `_attackInterval`/`_contactDamage`; throttled `DriveNav` (`:425`). **No ranged attack — contact only.** | EXTEND (§3.4) — add a minimal `RangedAttack(target, damage)` that the kiting brain calls (closes WO-139 #7 for the kite path). Routes damage to `HeroHealth` / `IDamageableStructure` exactly like the brain's WO-90 path. |
| **`EnemyBehaviorTree`** | `Assets/_Modules/Village/Enemies/EnemyBehaviorTree.cs` | Optional BT override; when present + `IsInitialized`, `EnemyBrain.Update()` yields to it entirely (`EnemyBrain.cs:211-215`). Priority selector: Dead → LowHP-hold → InRange-engage → Chase. `StopAndEngage` calls `_brain.TriggerAttack()` (`:133`). | **OUT OF SCOPE to fork.** WO-145 targets the **non-BT path** (the role/tactical brain that actually runs on shipped enemy prefabs — none assign a BT today). The BT keeps working unchanged. The one touch: when the ranged attack lands in §3.4, `TriggerAttack()`'s body gains a ranged branch the BT also benefits from. |

**Hard reconciliation rules:**
- One brain (`EnemyBrain`), one tactical enum (`EnemyTacticalState`), one tuning SO (`TacticalData`), one group coordinator. WO-145 adds **scoring + two tactical states + group flank-angle assignment + a minimal ranged attack**. Nothing else.
- **Append-only on every enum.** Renumbering breaks serialized prefab/SO inspector values.
- Reconcile, never blind-replace (memory *wo-batch-reconcile-not-replace*).

---

## 1. Architecture (assembly discipline — CLAUDE.md §5/§6)

All behaviour code lives in **`DeNelle.Village`** (drives `Enemy`/`EnemyBrain`). Only **data fields + two enum appends** go in **`DeNelle.Core.Data`** (`TacticalData.cs`, `EnemyArchetype`). **Village → Core only** (asmdef already references `DeNelle.Core`, verified `DeNelle.Village.asmdef:5`). No HUD reference. No `System.Reflection` introduced (CLAUDE.md §10). Null-conditional `?.` on any cross-module call. `EnemyTacticalState` stays in Village (it already does).

| Change | Assembly / path | Kind |
|---|---|---|
| Append `Kite=4`, `Reposition=5` to `EnemyTacticalState` | `DeNelle.Village` — `Assets/_Modules/Village/Enemies/EnemyTacticalState.cs` | enum append |
| Append `Kiter=6` to `EnemyArchetype` + add scoring/kite/rally fields | `DeNelle.Core.Data` — `Assets/_Modules/Core/Data/TacticalData.cs` | data + enum append |
| Target-scoring selector; Kite + Reposition destination cases; wire `_targetEvalTimer`; minimal ranged-attack call | `DeNelle.Village` — `Assets/_Modules/Village/Enemies/EnemyBrain.cs` | logic |
| `RangedAttack(...)` damage method | `DeNelle.Village` — `Assets/_Modules/Village/Enemies/Enemy.cs` | logic |
| Coordinated flank-angle assignment in `ReleaseAll` | `DeNelle.Village` — `Assets/_Modules/Village/Waves/EnemyGroupCoordinator.cs` | logic |

---

## 2. TACTIC A — Smart target priority (focus-fire the squishy / healer / pet)

**Gap:** today targeting is role-hardcoded and mostly *nearest*. Tank takes `FindHighestThreatTarget` (hero-if-near else nearest structure), Healer takes most-damaged ally, everyone else takes `FindNearbyHero ?? FindNearestTower ?? FindClosestTarget`. There is **no weighting** — a DPS will hit whatever is closest, not the player's **pet** or a **low-HP defender**. `TacticalData.TargetPriorityBias` exists but is read **nowhere** (verified).

**Design — a weighted candidate scorer.** Add a private `ScoreAndPickTarget()` to `EnemyBrain` that the role switch's offensive roles (DPS/Ranged/MiniBoss, and Tank as a tiebreak) call instead of the raw `FindNearbyHero ?? FindNearestTower ?? FindClosestTarget` chain. Healer keeps `FindMostDamagedAlly` (ally-side, untouched).

**Player-side target universe (verified what exists to target):**
| Target | How found today | Scoring intent |
|---|---|---|
| **Hero** | `_heroTransform` (tag `HeroTarget`/`Player`, cached `Awake:201-203`) | Medium role-value; high threat (it kills you). |
| **Pet** | NOT currently found by EnemyBrain — **new candidate.** Resolve by tag (see note) | **High priority** — squishy, low-HP, force-multiplier. The owner ask: "find best targets … focus-fire squishies." |
| **Towers** | `FindNearestTower` (`:435`, `Tower` component, `_towerScanRadius=20`) | Medium role-value; the static DPS threat. |
| **Heart** | `_heartTransform` (cached `Awake:196`) | Low priority unless nothing else — the fallback/win-condition objective. |
| **Wounded allied enemy** | (Healer only) | n/a for offensive scoring. |

> **Pet targeting seam:** the pet is `DeNelle.Pets` (Village does NOT reference it for AI; verified asmdef). Resolve the pet **by tag**, mirroring the existing hero pattern — add a scan for a `"PetTarget"` tag (designer tags the pet prefab root; null-safe, no asmdef edit). Do NOT add a `DeNelle.Pets` dependency. If the tag is absent the scorer simply has no pet candidate (backward-safe).

**Scoring function (data-driven; the emergent "focus the healer/pet" behaviour):**

```
score(candidate) =
      RoleValueWeight  * RoleValueOf(candidate)          // pet/healer-class high, Heart low
    + LowHpWeight      * (1 - candidate.HpFraction)      // wounded/squishy ⇒ higher
    + ThreatWeight     * ThreatOf(candidate)             // hero/tower DPS pressure
    - DistanceWeight   * normalizedDistance              // nearer ⇒ slightly higher
    ) * candidateTargetPriorityBias                       // per-target multiplier (see below)
```

- `RoleValueOf` is a designer-tunable lookup: **Pet > Hero ≈ Healer-class structure > Tower > Heart.** (The player's *pet* and a low-HP target are the "squishy/healer" focus.)
- `candidate.HpFraction` exists on `Enemy` (`:205`); for the hero read `HeroHealth` (already referenced by WO-90 attack path); towers expose `IDamageableStructure.IsAlive` — add an HP-fraction read where cheap, else treat structure HP as unknown (LowHp term = 0). Keep null-safe.
- **`TargetPriorityBias` wiring (the dormant field):** it is a property of the *attacker's* `TacticalData`, so it scales how aggressively *this* enemy chases priority targets — multiply the whole `RoleValue+LowHp+Threat` cluster by `_tactics?.TargetPriorityBias ?? 1f`. (A "skirmisher/assassin" archetype with `TargetPriorityBias = 2` will abandon a nearby tank to dive a far pet; a `Standard` with `1` behaves close to today.) Document this clearly in the SO tooltip.

**Throttle:** wire the **already-declared-but-unused** `_targetEvalTimer` / `TargetEvalInterval` (`EnemyBrain.cs:122-124`). Re-score only every `TargetEvalInterval` (2s) and cache `_currentTarget`; between evals reuse the cached target (prevents 20 enemies all re-scoring per frame, and prevents jittery target-thrashing). Reuse the existing `_scanBuffer[32]` for the overlap scan — no new allocations.

**Weights live on `TacticalData`** (see §6) so designers tune per-archetype with zero code. Defaults must reproduce today's "nearest-ish" feel for `Standard` archetype (DistanceWeight dominant) so existing prefabs don't change behaviour until a designer opts in.

---

## 3. TACTIC B — Kiting (Ranged maintains standoff + attacks instead of closing)

**Gap:** the `Ranged` role uses DPS nav (closes to melee) and there is **no ranged attack** — `Enemy` only has contact melee (`TickContactAttack`/`ProbeForStructure`), and `EnemyBrain.TriggerAttack()` is a no-op (WO-139 #7). A "ranged" enemy today just walks into your face.

### 3.1 New tactical state: `Kite`
Add `EnemyTacticalState.Kite = 4`. A Kite-state enemy maintains a **desired-range band** around its target:
- Read `KiteDesiredRange` and `KiteMinRange` from `TacticalData` (§6).
- Compute distance to target.
  - `dist < KiteMinRange` → **back off**: destination = `target.pos + (self - target).normalized * KiteDesiredRange` (retreat to band). If that point is off-NavMesh, sample the nearest NavMesh point (use `NavMesh.SamplePosition`, same defensive pattern as the existing path validation in `ComputeTacticalDestination` default case `:318-327`).
  - `dist > KiteDesiredRange` → **close** to the outer band edge (destination toward target, stop at `KiteDesiredRange`).
  - `KiteMinRange <= dist <= KiteDesiredRange` → **hold** (destination = current position, i.e. `SetBrainTargetPosition(transform.position)`) and **fire** (§3.4). A small `KiteStrafeJitter` lateral offset (perpendicular to the target vector, sign flipping on a timer) makes the kiter feel alive and dodge-y rather than frozen — optional, behind a `> 0` guard.

### 3.2 Where Kite is assigned
In `UpdateTacticalState()` (`:267-271`), extend the archetype switch: `EnemyArchetype.Kiter => EnemyTacticalState.Kite`. The new `Kiter` archetype (§6) is what makes an enemy kite; the `Ranged` **role** + `Kiter` **archetype** is the canonical "ranged kiter" combo (role = what it targets via scoring §2; archetype = how it moves). Retreat still pre-empts Kite when HP < `RetreatHealthThreshold` (the existing early-return at `:259` is checked first — keep that order).

### 3.3 Add Kite case to `ComputeTacticalDestination`
Add `case EnemyTacticalState.Kite:` to the switch (`:291`). Implements the band logic above. Returns the computed standoff/hold position. Keep the existing NavMesh-path-validity guard pattern.

### 3.4 Minimal ranged attack — the WO-139 #7 dependency (IN SCOPE, scoped tight)
Kiting is pointless without a ranged hit. **This WO includes a minimal ranged attack — NOT a projectile/VFX system, just damage-at-range** (VFX/projectile art is a follow-on WO):
- Add `Enemy.RangedAttack(Transform target, float damage)` (or `EnemyBrain` calls a new lightweight path). It applies damage to `HeroHealth` or `IDamageableStructure` on/under `target` — **reuse the exact resolution WO-90 already does in the brain's attack** (the `damage`/`attackCooldown` fields exist on `EnemyBrain` `:96-99`). Respect `attackCooldown` via the existing `_nextAttackTime` (`EnemyBrain.cs:130`).
- Implement `EnemyBrain.TriggerAttack()` (currently no-op `:161-166`) to, **when in Kite state and target in band**, fire `RangedAttack`. This closes #7 for the kite path while leaving melee contact (Enemy.TickContactAttack) the path for everyone else. Fire the `Attack` animator trigger if present (null-safe, the hash already exists on Enemy).
- **Scope boundary:** no projectile prefab, no travel time, no homing, no new VFX type. Instant hit-scan damage on cooldown. The owner gets a functioning kiter now; juice later. Flag this scoping for owner sign-off in the RESULT.
- **Balance guard:** ranged damage uses the same `EnemyData`-overlaid `damage` field; default ranged `attackCooldown` should be *slower* than melee so kiters aren't oppressive. Designer-tunable.

---

## 4. TACTIC C — Coordinated pincer (group flanks from multiple angles at once)

**Gap:** `Flank` today is **per-enemy independent** — every Flanker rotates the direct-path vector by its *own* static `FlankAngleOffset` (`ComputeTacticalDestination:301-311`). Five Flankers with `FlankAngleOffset=90` all arc the **same side** → a conga line, not a pincer.

**Design — assign DISTINCT angles across the group at release.** The `EnemyGroupCoordinator` is the only place that knows the whole group (`_members` list). Extend `ReleaseAll()` (`:113`):
- When releasing, partition members flagged for coordinated flanking into an **envelope**: distribute angles across the group — e.g. left wing `-FlankAngleOffset`, right wing `+FlankAngleOffset`, and (for larger groups) a rear element at `±150-180°`. A simple, data-light scheme: index-based — even members → left, odd → right, last 1-2 → rear — or evenly spread `[-max .. +max]`.
- Push each member's assigned angle via a **new additive setter** on `EnemyBrain`, e.g. `SetCoordinatedFlankAngle(float signedDegrees)`, that overrides the per-enemy `FlankAngleOffset` used in the Flank destination math. The brain's Flank case (`:301`) reads the coordinated angle when set, else falls back to `_tactics.FlankAngleOffset` (backward-compatible).
- Gate the whole behaviour behind a `TacticalData.CoordinatedFlank` bool (§6) and/or `EnemyArchetype.Flanker`, so non-pincer groups (the default) are unchanged. Only groups whose members opt in get the envelope.
- Keep `ReleaseAll`'s existing "only override Suppressed members" guard (`:124`) — don't re-target a member already in Retreat from damage.

This is a **real pincer**: a Suppressed Flanker group releases simultaneously (existing behaviour) AND fans out to left/right/rear (new), converging on the target from multiple bearings at once. Reuses the existing suppress→release timing as the synchronization primitive — no new coordinator.

---

## 5. TACTIC D — Reposition (retreat TO a better position, not blindly away)

**Gap:** `Retreat` today is a dumb `-away` vector — `transform.position + (self - target).normalized * 8f` (`ComputeTacticalDestination:293-299`). A wounded enemy runs in a straight line away from the hero, often into a wall or off alone to die.

**Design — `Reposition = 5`: retreat to a rally point, then re-engage.**
- Add `EnemyTacticalState.Reposition = 5`. When HP drops below `RetreatHealthThreshold`, archetypes flagged `RepositionInsteadOfFlee` (§6) enter `Reposition` instead of `Retreat` (branch in `UpdateTacticalState` `:258-264`; default archetypes keep plain `Retreat` — backward-compatible).
- **Rally destination** (priority order):
  1. **Nearest ally cluster** — reuse the `Physics.OverlapSphereNonAlloc` + `_scanBuffer` pattern (as `FindMostDamagedAlly`/`FindNearestStructure` do) to find the centroid of living allied `Enemy`s within a `RallyScanRadius`. Move toward that centroid (regroup with the pack / behind the tank).
  2. If no allies, **back to standoff range** — for a `Kiter`, fall to the Kite band; for others, a point `RepositionFallbackDistance` away from the target but biased toward the spawn/region origin rather than a random wall.
  3. Sample onto NavMesh (`NavMesh.SamplePosition`) so the rally point is always reachable.
- **Re-engage condition:** leave `Reposition` and return to the archetype-default state when EITHER (a) HP recovers above a `ReengageHealthThreshold` (a Healer topped it up — Healer role already heals, §0), OR (b) it has reached the rally point AND `RepositionRegroupSeconds` have elapsed (it rejoined the pack and re-commits). Track with a small timer field on `EnemyBrain` (mirror `_suppressTimer` pattern). This makes wounded enemies *retreat, heal/regroup, and come back* — a feel upgrade over one-way flee.

---

## 6. Data-driven knobs — `TacticalData` additions (append-only)

All new tuning on `Assets/_Modules/Core/Data/TacticalData.cs` so designers tune per-archetype with **zero code** (CLAUDE.md spirit; matches the existing SO pattern). **Append fields; do not reorder existing ones.**

```csharp
// EnemyArchetype — APPEND ONLY (existing 0..5 unchanged):
Kiter = 6,   // maintains standoff range + ranged attacks (Tactic B)

// TacticalData — new [Header] groups, appended after TargetPriorityBias:

[Header("Target scoring (WO-145 Tactic A)")]
public float RoleValueWeight   = 1f;   // weight of designer role-value (pet/healer high)
public float LowHpWeight       = 1f;   // weight of (1 - target HpFraction) — focus squishies
public float ThreatWeight      = 1f;   // weight of target's damage threat (hero/tower)
public float DistanceWeight    = 1f;   // nearer = slightly preferred; dominant for Standard
// NOTE: TargetPriorityBias (existing :90) is now READ — it scales this attacker's
//       whole priority cluster. >1 = dives priority targets past nearer ones.

[Header("Kiting (WO-145 Tactic B — requires EnemyArchetype.Kiter)")]
public float KiteDesiredRange  = 8f;   // preferred standoff distance
public float KiteMinRange      = 5f;   // back off if target closer than this
public float KiteStrafeJitter  = 0f;   // 0 = stand still in band; >0 = lateral weave

[Header("Coordinated flank (WO-145 Tactic C)")]
public bool  CoordinatedFlank  = false; // group fans to L/R/rear via EnemyGroupCoordinator

[Header("Reposition (WO-145 Tactic D)")]
public bool  RepositionInsteadOfFlee = false; // Reposition (rally) vs dumb Retreat
public float RallyScanRadius          = 12f;   // search radius for ally cluster centroid
public float RepositionFallbackDistance = 8f;  // standoff fallback when no allies
public float ReengageHealthThreshold  = 0.5f;  // HP frac to re-commit
public float RepositionRegroupSeconds = 3f;    // time at rally before re-engage
```

**Backward-compat:** every default reproduces today's behaviour for `Standard` archetype (scoring ≈ nearest via DistanceWeight; no kite; no coordinated flank; plain Retreat). Existing prefabs/SOs with no new fields deserialize to these defaults — **zero behavioural regression** until a designer opts an archetype in.

---

## 7. Files to EDIT / CREATE

**EDIT (5 files):**
- `Assets/_Modules/Village/Enemies/EnemyTacticalState.cs` — append `Kite=4`, `Reposition=5` (with XML docs).
- `Assets/_Modules/Core/Data/TacticalData.cs` — append `Kiter=6` to `EnemyArchetype`; append §6 fields; update `TargetPriorityBias` tooltip to note it is now read.
- `Assets/_Modules/Village/Enemies/EnemyBrain.cs` — `ScoreAndPickTarget()` + pet-tag scan; wire `_targetEvalTimer`; Kite + Reposition cases in `ComputeTacticalDestination`; archetype→state mapping in `UpdateTacticalState`; implement `TriggerAttack()` ranged branch; `SetCoordinatedFlankAngle(float)` setter + Flank-case read; reposition timer field.
- `Assets/_Modules/Village/Enemies/Enemy.cs` — `RangedAttack(Transform, float)` (hit-scan, routes to `HeroHealth`/`IDamageableStructure`, animator-trigger null-safe).
- `Assets/_Modules/Village/Waves/EnemyGroupCoordinator.cs` — distinct flank-angle envelope in `ReleaseAll()`.

**CREATE:** none required. (If `ScoreAndPickTarget` grows large, a private `EnemyTargetScorer` helper class in `Assets/_Modules/Village/Enemies/` is acceptable — Village assembly — but inline is preferred to keep the brain cohesive.)

---

## 8. What NOT to touch

- ❌ `VillageSceneBuilder.cs` — frozen serialization bottleneck (CLAUDE.md §3/§9).
- ❌ Any `.unity` scene file — no hand-edits (CLAUDE.md §3). No bake fired from UI.
- ❌ `EnemyBehaviorTree.cs` structure — do not fork the BT. (Only `TriggerAttack()`'s body changes, which the BT calls through unchanged.)
- ❌ `WaveManager` loop — untouched (WO-143 owns coexistence; this WO is brain-local).
- ❌ Do NOT renumber existing `EnemyTacticalState` (0-3) or `EnemyArchetype` (0-5) values — append only (serialization safety).
- ❌ Do NOT add a `DeNelle.Pets` asmdef reference — resolve the pet by tag (null-safe).
- ❌ Do NOT move `EnemyRole` to Core in this WO — that is WO-128's call (flag overlap, don't race).
- ❌ No new `System.Reflection` (CLAUDE.md §10).
- ❌ No projectile/VFX system for the ranged attack — hit-scan damage only (juice is a follow-on WO).

---

## 9. Acceptance criteria

1. `EnemyTacticalState` has `Kite=4`, `Reposition=5`; existing 0-3 unchanged. Compiles.
2. `TacticalData` has `Kiter=6` + all §6 fields; existing fields/values unchanged; SO inspector shows the new headed groups; existing assets load with defaults.
3. **Target priority:** a DPS/Ranged enemy with `TargetPriorityBias > 1` and a high `RoleValueWeight`/`LowHpWeight` measurably prefers the **pet** (tagged `PetTarget`) and **low-HP defenders** over a nearer tank/wall — verifiable by a log of the chosen target each eval, or a play test. A `Standard` archetype with default weights behaves ≈ today (nearest).
4. `TargetPriorityBias` (formerly dead, `TacticalData.cs:90`) is **read** in the scorer (verifiable by reference search).
5. `_targetEvalTimer`/`TargetEvalInterval` (formerly dead, `EnemyBrain.cs:122-124`) now throttle scoring — target is re-evaluated on the interval, not per-frame.
6. **Kiting:** a `Kiter` archetype + `Ranged` role enemy keeps `dist` within `[KiteMinRange, KiteDesiredRange]` of its target, backs off when the hero closes, and deals **ranged damage** on cooldown (HeroHealth HP drops without contact). `Enemy.RangedAttack` resolves both `HeroHealth` and `IDamageableStructure`. WO-139 #7's `TriggerAttack()` no longer a no-op for the kite path.
7. **Pincer:** a Suppressed group of `Flanker`s with `CoordinatedFlank=true`, on `ReleaseAll`, converges from **distinct bearings** (left/right/rear) — not all the same arc. Non-coordinated groups unchanged.
8. **Reposition:** an enemy with `RepositionInsteadOfFlee=true` dropping below `RetreatHealthThreshold` moves toward the **nearest ally cluster centroid** (or standoff fallback), then **re-engages** when healed above `ReengageHealthThreshold` or after `RepositionRegroupSeconds` at the rally. Plain-Retreat archetypes unchanged.
9. Raiders (WO-143) inherit all of the above with no edit to `RaidDirector`/`RoamingRaider` (raid party `WaveEnemyGroup` members just get `TacticalData` assets with the new fields).
10. No regression: enemies with no `TacticalData` (the common case) behave exactly as before — Heart-march + role targeting.

---

## 10. Done checklist (CLAUDE.md §10)

- [ ] Brace-balance check passed on every `.cs` file edited (5 files).
- [ ] No `.unity` scene file hand-edited; no bake fired.
- [ ] No new `System.Reflection` introduced.
- [ ] `using DeNelle.Core.Combat;` present where `IDamageableStructure` is used (already in Enemy.cs:33 / EnemyBrain.cs:47).
- [ ] Null-conditional `?.` on all cross-module/service calls (pet/hero/HeroHealth resolution null-safe).
- [ ] Both enums append-only — existing values not renumbered (serialization check).
- [ ] `TargetPriorityBias` and `_targetEvalTimer` confirmed read (no longer dead).
- [ ] Ranged-attack scoping (hit-scan, no projectile/VFX) flagged for owner sign-off in RESULT.
- [ ] WO-128 `EnemyRole`-to-Core overlap flagged for owner (not raced here).
- [ ] Acceptance criteria 1-10 reviewed line by line.
- [ ] CLI build-verifies in batchmode; saves `WORK_ORDER_145_advanced_enemy_tactics.RESULT.md`.

> **AUDIT 2026-08-21 (agent fleet, read-only):** FIXED. Evidence: `EnemyTacticalState.cs:50,58; EnemyBrain.cs:1534-1570` — scorer + kite states. Status was flipped from READY by the AUDIT, not by an implementation pass. The body below is left intact; if this call is wrong, the evidence cited here is what to challenge.
