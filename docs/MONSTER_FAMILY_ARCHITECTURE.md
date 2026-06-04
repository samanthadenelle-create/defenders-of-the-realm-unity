# Monster Family Architecture — packs that roam, protect, and out-think you

**Status:** ARCHITECTURE / spec source — spawns work orders (does not implement code)
**Date:** 2026-05-30
**Lane:** Combat / AI — code only (CLAUDE.md §9). NO `VillageSceneBuilder.cs` edits (frozen, §3), NO scene hand-edits, NO bake fired from UI.
**Assembly discipline (CLAUDE.md §5):** all behaviour in `DeNelle.Village`; shared pure data/enums in `DeNelle.Core`; **Village → Core only**, no HUD ref, cross-module via `CoreServices.Hud?.` / `CoreServices.Audio?.`.
**North Star:** `docs/NORTH_STAR.md` — "DEFEND base + mines from waves **and roaming enemies**." Families are the roaming enemies made intelligent: not a swarm, a *squad*.

> **Naming caution (verified):** `Assets/_Modules/Village/Enemies/EnemyFamilyTestSpawner.cs` already uses the word "family" loosely to mean a *role mix* (3 Grunts + 1 Tank + 1 Healer test pack on the `J` hotkey). That is **not** this system. To avoid a collision, the leader/follower pack system here is called a **Monster Family** with a `MonsterFamily` runtime type; the existing dev spawner keeps its name and becomes a convenient *driver* for spawning a test family (see Phase 0).

---

## 1. Vision

A **Monster Family** is a pack of enemies that moves and fights as one organism:

- **Roams together** — wanders a region as a loose group, not a line of independent agents all pathing to the same goal.
- **Protects its own** — keeps squishies (healer/caster) in the centre, tanks on the perimeter; retreats into a tight protective ball when the leader is hurt.
- **Prioritises threats smartly** — the leader picks the group's primary target with the Utility scorer (WO-145), so the pack focus-fires your pet / wounded defender, not whatever is nearest.
- **Coordinates strategy** — shifts formation by context (loose ring while roaming → wedge to charge → line to spread across a wide AoE front → tight pack to retreat/protect → column down a narrow path), and pincers via the existing suppress-release primitive.

The owner's design (leader + dynamic-slot followers, Unity-6 Behavior brain, formation theory, shared perception, leader-death promotion, perf budget) is captured below and **layered on the AI that already shipped** — it does not replace it.

---

## 2. How it reconciles with existing AI — exists vs new

The #1 project trap is greenfielding over built systems. Everything below was read and verified. **Build the family layer ON these; replace none.**

| Concern | EXISTS (verified path) | What it already does | NEW family layer |
|---|---|---|---|
| Per-enemy body | `Assets/_Modules/Village/Enemies/Enemy.cs` | `NavMeshAgent` march, HP, contact attack on `IDamageableStructure`, death/XP/VFX, `Configure(...)`, `SetBrainTarget`/`SetBrainTargetPosition`, `Died`/`ReachedHeart`. | **Reuse verbatim.** A follower IS an `Enemy`. Family drives it through the existing `SetBrainTargetPosition` override slot — no new movement system. |
| Per-enemy brain | `Assets/_Modules/Village/Enemies/EnemyBrain.cs` | Role targeting (`ChooseTarget` :336), tactical states (`ComputeTacticalDestination` :287 Rush/Flank/Retreat/Suppressed), `SetTacticalState` :278, dormant throttle `_targetEvalTimer` :122. | **Reuse.** Followers still run `EnemyBrain` for *engagement* once the family commits to a target. The family overrides only the *travel* destination (slot), same arbitration pattern WO-143's `RoamingRaider` defined (own the destination while roaming; yield to brain on engage). |
| Existing BT hook | `Assets/_Modules/Village/Enemies/EnemyBehaviorTree.cs` | Hand-rolled `Selector(Dead→LowHP→InRange→Chase)` using `DeNelle.AI` `BTNode`/`Selector`/`Sequence`/`Condition`/`ActionNode`. `EnemyBrain.Update` yields to it when `IsInitialized` (:211). | **Key reconciliation point (see §6).** The *leader brain* is the natural home for the Unity Behavior package, introduced **phased** — the hand-rolled BT keeps working; no rip-and-replace. |
| Group coordination seed | `Assets/_Modules/Village/Waves/EnemyGroupCoordinator.cs` | Per-spawn group: holds members `Suppressed` until all spawned, `ReleaseAll` charges them together, self-destructs. `_members` list, `RegisterMember`, `FinaliseGroup`. | **This is the seed of the family.** Decision (§4): **do NOT bloat `EnemyGroupCoordinator`** — keep it as the spawn-time "charge together" primitive; the persistent pack brain is a *new* `MonsterFamily` coordinator that can *use* a coordinator's suppress-release as its charge trigger. |
| Tactical postures | `Assets/_Modules/Village/Enemies/EnemyTacticalState.cs` | `enum { Rush, Flank, Retreat, Suppressed }` (WO-145 appends `Kite=4`, `Reposition=5`). | **Reuse.** Formation is a *group* concept layered above per-enemy tactical state — family sets each member's posture (e.g. tight-pack ⇒ members Rush to slots; retreat ⇒ Reposition). |
| Utility target scorer | `WORK_ORDER_145_advanced_enemy_tactics.md` §2 (`ScoreAndPickTarget` on `EnemyBrain`, `TacticalData` weights) | Weighted candidate scorer (RoleValue/LowHp/Threat/Distance × `TargetPriorityBias`) — focus-fire squishies/pet. | **The leader's target pick USES this** — do NOT re-spec a scorer. The family leader scores once for the pack; followers inherit the leader's target. Coordinated pincer (WO-145 §4) is the family's flank envelope. |
| Roaming layer | `WORK_ORDER_143_roaming_raids.md` (`RaidDirector`, `RoamingRaider`, `RaidRegion`, `RegionThreat`) | Sibling-of-WaveManager director; roamers wander a region then yield to `EnemyBrain` on aggro; organised raids spawn a `WaveEnemyGroup` via `EnemyGroupSpawner`. | **Families ARE the roaming raiders, upgraded from individuals to packs.** A `RaidDirector` "organised raid" becomes a spawned `MonsterFamily` (leader + followers) instead of N independent `RoamingRaider`s. The roam-vs-aggro arbitration is lifted to the *leader*. |
| Region identity | WO-143 `RaidRegion` (Core) + WO-107 `ZoneManager` | N/E/S/W classification. | **Reuse.** Families roam these regions; danger tier scales family size/toughness via the existing `Enemy.ApplyWaveScaling`/`WaveScalingCurve` (WO-143 §6). |
| Wander math reference | `Assets/_Modules/Village/NPCs/AmbientNPC.cs` | Random NavMesh point in a roam radius → `NavMesh.SamplePosition` → set destination → pause → repeat; graceful no-NavMesh idle. | **Reference pattern for the leader's roam**, exactly as WO-143 already lifts it. Followers do NOT roam independently — they pathfind to slots. |
| Dev driver | `Assets/_Modules/Village/Enemies/EnemyFamilyTestSpawner.cs` | `J`-key DDOL spawner: builds a Grunt/Tank/Healer capsule pack to watch roles live, no scene edit. | **Repurpose as the family test driver** (Phase 0): same self-bootstrapping hotkey, but assemble the pack as a `MonsterFamily` (1 leader + followers) so the formation/slot system is visible with zero scene work. |

**Hard rule:** one enemy body (`Enemy`), one per-enemy brain (`EnemyBrain`), one tactical enum, one Utility scorer (WO-145), one region identity (WO-143/107), one spawn-charge primitive (`EnemyGroupCoordinator`). The family layer adds **only**: a leader, a follower-slot binding, a formation calculator, a shared perception, and a family coordinator/director glue. Reconcile, never blind-replace (memory *wo-batch-reconcile-not-replace*).

---

## 3. Component architecture

All runtime types in `DeNelle.Village` (they drive `Enemy`/`EnemyBrain`). One shared enum (`FormationType`) and one optional tuning SO live in `DeNelle.Core`.

| Type | Assembly / path (proposed) | Responsibility |
|---|---|---|
| `FormationType` (enum) | `DeNelle.Core` — `Assets/_Modules/Core/FormationType.cs` | `{ LooseRing, Wedge, Line, TightPack, Column }`. Pure data so a future HUD/save/SO can reference it. |
| `FamilyData` (SO, optional) | `DeNelle.Core.Data` — `Assets/_Modules/Core/Data/FamilyData.cs` | Designer tuning: slot radii per formation, reposition threshold, reform cooldown, perception radius + cooldown, formation-switch thresholds. Mirrors `TacticalData` style. **Optional** — sensible code defaults so a family works with none assigned. |
| `MonsterFamily` (MonoBehaviour) | `DeNelle.Village` — `Assets/_Modules/Village/Families/MonsterFamily.cs` | The pack object (one per family). Owns the member roster, the current `FormationType`, the shared `GroupPerception`, the leader reference, and the leader-death → promote+reform logic. The persistent group brain (vs `EnemyGroupCoordinator`'s spawn-time-only role). |
| `FamilyLeader` (MonoBehaviour) | `DeNelle.Village` — `Assets/_Modules/Village/Families/FamilyLeader.cs` | The decision-maker. Drives its own `Enemy.SetBrainTargetPosition` for roam/approach; picks the **group target** via the WO-145 scorer; selects the **context formation**; broadcasts target + formation origin/heading to followers. The Behavior-package brain (when adopted) lives here (§6). |
| `FamilyMember` (MonoBehaviour) | `DeNelle.Village` — `Assets/_Modules/Village/Families/FamilyMember.cs` | A follower. Reads its assigned **slot** from `FormationController` and drives `Enemy.SetBrainTargetPosition(slotWorldPos)` while travelling; yields to `EnemyBrain` (re-enable / stop writing the slot) when in engage range of the group target — identical arbitration to WO-143 `RoamingRaider`. Promotable to leader. |
| `FormationController` (MonoBehaviour or helper) | `DeNelle.Village` — `Assets/_Modules/Village/Families/FormationController.cs` | The **slot calculator**. Given leader transform, formation type, and member roster, computes each member's local-space slot offset, distributes evenly with small noise, NavMesh-samples it, and exposes world slots. Caches offsets; recalcs only on formation change or significant leader move (§7). |
| `GroupPerception` (component, on `MonsterFamily`) | `DeNelle.Village` — folded into `MonsterFamily` or its own file | **Shared sensing.** One member detects the hero/base/node → the whole family reacts. Cooldown-throttled overlap scan (reuse `EnemyBrain`'s `_scanBuffer[32]` pattern + the `HeroTarget`/`Player`/`PetTarget` tag lookups). Replaces N independent per-enemy aggro scans with one per family. |

### How followers get slots (the core loop)

1. **Leader picks destination** — roam (random NavMesh point near region anchor, `AmbientNPC` math) or approach (toward the group target the WO-145 scorer chose).
2. **Leader picks formation** by context (§4) and writes it on `MonsterFamily`.
3. **`FormationController` computes slots** in the **leader's local space**: polar layout for ring/pack/wedge, grid/line for line/column. Even angular/lateral distribution, small per-slot noise so it doesn't look robotic, role-aware (squishies → inner slots, tanks → outer/front). Each slot is `NavMesh.SamplePosition`-snapped (same guard as `WaveManager.SpawnOne` / `EnemyFamilyTestSpawner`). Offsets cached; only recomputed on formation change or when the leader has moved past a reposition threshold (avoids per-frame jitter and per-frame cost).
4. **Each `FamilyMember` drives to its slot** via `Enemy.SetBrainTargetPosition(slot)`. Followers pathfind to a *nearby dynamic slot*, not the far goal — cheaper paths, synchronized motion. Rotation matches leader heading / agent velocity. NavMeshAgent local avoidance + the engine's separation keep them from stacking; the family does not implement its own steering.
5. **On engage** (group target in range): the member stops writing its slot and lets `EnemyBrain` run normal role targeting against the **leader's chosen group target** (so the pack focus-fires together). Smooth lerp between formations when the type changes (interpolate slot offsets over a short window) so transitions read as a maneuver, not a snap.

**Utility scorer is the leader's, not per-follower.** The leader scores the target universe once (WO-145 `ScoreAndPickTarget`) and publishes it. Followers inherit it — this is what produces emergent focus-fire and is far cheaper than every enemy scoring independently.

---

## 4. Formation types + slot theory

Formations are owner-specified; captured here as the `FormationType` set and their slot math + the context that selects them.

| Formation | Context (when leader selects it) | Slot layout (leader local space) | Intent |
|---|---|---|---|
| **LooseRing** | Roam / idle (no target committed) | Polar: members spread evenly on a ring of radius `R_roam` around the leader, small angular noise. | Wander as a loose group; covers ground; squishies drift to inner band. |
| **Wedge** (arrowhead) | Charge a committed target in the open | Two diverging lines behind the leader apex; grid offsets `(±lateral·i, −depth·i)`. | Concentrated assault; leader/tanks at the point. |
| **Line** (skirmish) | Wide AoE front (spread out to attack a broad target / avoid clumping) | Lateral grid across leader's right axis, shallow depth. | Spread the front; minimise splash damage clustering. |
| **TightPack** | Retreat / protect a wounded leader or squishy | Small-radius polar ring, tanks on the outer arc facing the threat, squishies centred. | Protective ball; bodyguard the weak member. |
| **Column** | Narrow path / corridor traversal | Single-file grid down leader's forward axis, slight alternating lateral noise. | Fit through choke points; reform to ring/wedge on exit. |

**Slot calc rules (owner spec, systematized):**
- Compute in **leader local space**, then transform to world (`leader.TransformPoint(localOffset)`).
- **Polar** for ring/pack/wedge (angle + radius); **grid** for line/column (lateral index × spacing, depth row).
- **Even distribution** across available members; **small noise** per slot so packs don't look stamped.
- **Reposition threshold:** a member only re-paths to its slot when the slot has drifted more than `slotResampleDistance` from where the member is heading — kills jitter and per-frame recompute.
- **NavMesh-sample every slot** (`NavMesh.SamplePosition(local→world, out hit, sampleDist, AllAreas)`); if a slot is off-mesh, fall back to nearest valid point (never error — matches the project's degrade-gracefully rule).
- **Rotation** matches leader heading (or agent velocity when moving) so the pack faces its travel/engage direction.
- **Rely on NavMeshAgent avoidance + separation** for collision; do not hand-roll boids.
- **Smooth lerp** slot offsets across a short window when `FormationType` changes (a maneuver, not a teleport).
- **Context switching** is the leader's job: roam ⇒ LooseRing; commit-to-target in open ⇒ Wedge; broad/AoE target or "spread" cue ⇒ Line; leader/squishy low HP or family-unhealthy ⇒ TightPack + retreat; narrow-path heuristic (later, needs nav data) ⇒ Column.

---

## 5. State flow

The **leader** runs the family state machine; members follow slots or engage.

```
        ┌─────────┐  no target in shared perception
        │  ROAM   │  leader: random NavMesh point near region anchor (AmbientNPC math)
        │ LooseRing│  followers: slots on the roam ring
        └────┬────┘
             │ GroupPerception sees hero / base / node (one sees → all react)
             ▼
        ┌─────────┐  leader: ScoreAndPickTarget (WO-145) → group target
        │ DETECT  │  choose context formation
        └────┬────┘
             ▼
   ┌──────────────────┐  leader moves toward target; FormationController lays slots
   │ FORM + APPROACH  │  formation = Wedge (open charge) / Line (wide) / Column (choke)
   │  (followers to   │  members drive to slots; smooth lerp on formation change
   │     slots)       │
   └────────┬─────────┘
            │ group target within engage range
            ▼
        ┌─────────┐  members stop writing slots → EnemyBrain engages the GROUP target
        │ ATTACK  │  (focus-fire). Coordinated pincer = WO-145 distinct flank angles.
        └────┬────┘
             │ family unhealthy (leader/squishy low HP, members lost)
             ▼
   ┌──────────────────┐  formation = TightPack; members → Reposition (WO-145) toward
   │  FLEE / REGROUP  │  rally/leader; bodyguard the weak member; reform when healthy
   └────────┬─────────┘
            │ recovered → back to ROAM/DETECT; leader died → PROMOTE + REFORM
            ▼
        (loop)
```

- **"Is Family Healthy?" decorator** (owner's design): a guard the leader brain evaluates each tick — `livingMembers / startCount`, leader HP, squishy HP. False ⇒ force TightPack + FLEE/REGROUP, pre-empting the normal formation choice (mirrors how `EnemyBrain.UpdateTacticalState` lets Retreat pre-empt at :259).
- **Leader death → promote + reform:** `MonsterFamily` subscribes to each member's `Died` (the `EnemyBrain.Died` event already exists, :172, and `EnemyGroupCoordinator` already uses it). On leader death, promote the highest-priority survivor (tankiest / nearest centroid) to `FamilyLeader`, recompute slots around the new leader, and continue. If the roster empties, the family self-destructs (mirror `EnemyGroupCoordinator`'s self-destruct + the WO-139 #4 OnDisable unsubscribe discipline so no stale `Died` callbacks fire into a torn-down family).
- **Perception is shared and cooldown-throttled** — one scan per family per cooldown, not one per member per frame.

---

## 6. Unity Behavior package adoption — PHASED, not rip-and-replace

**Decision: phased adoption. Keep the code-driven brain working; introduce `com.unity.behavior` as an authoring layer for the LEADER only, in a deliberate later phase. Do not rewrite EnemyBrain or EnemyBehaviorTree.**

**Verified state:**
- `com.unity.behavior` is **NOT** in `Packages/manifest.json` (checked — current deps include Cinemachine, Input System, URP 17.4, Addressables, Inference, etc., but no Behavior package). Adopting it is a **new manifest dependency**: add `"com.unity.behavior": "<version>"` to `Packages/manifest.json` (CLI lands this; editor-closed; verify it resolves on Unity 6000.4.8f1 before relying on it).
- A working hand-rolled BT already exists: `EnemyBehaviorTree.cs` with `DeNelle.AI` `BTNode`/`Selector`/`Sequence`/`Condition`/`ActionNode`, and `EnemyBrain.Update` already yields to it when present + initialized (:211). This is the proof that a BT-driven brain is wired into the codebase without engine coupling.

**Why phased (engineering rationale, flag for owner):**
- The shipped enemy prefabs run the **code role/tactical brain**, not a BT (WO-145 §0 confirms none assign a BT today). A rip-and-replace to the Behavior package would put a new, unproven authoring dependency on the critical path of the *whole* enemy AI, risk serialization/version churn on a mobile target, and throw away the working `EnemyBehaviorTree` hook.
- The owner's Behavior-package design (`Selector(Roam→Chase→Attack)`, `Sequence(see→score→move→attack)`, `Decorator "Is Family Healthy?"`) maps cleanly onto the **leader** brain — a single decision-maker per pack — which is exactly the unit where authored BTs pay off and where there are few instances (perf-friendly).

**Migration path:**
1. **Phase F1 — code-driven leader brain (ship first).** Implement `FamilyLeader` as a plain state machine (the §5 flow) in C#, reusing `EnemyBrain`'s scorer (WO-145) and tactical states. No new package. This is fully functional and is the fallback forever.
2. **Phase F2 — abstract the leader decisions behind an interface.** Define `IFamilyBrain` (decide target, decide formation, evaluate "is family healthy"). The code state machine is the default `IFamilyBrain`. `FamilyLeader` calls the interface, never concrete BT nodes — so the brain is swappable without touching follower/formation code.
3. **Phase F3 — add `com.unity.behavior` to the manifest** (CLI, editor-closed) and author a Behavior Graph that implements the owner's tree for the leader. Wrap it behind a second `IFamilyBrain` implementation (`BehaviorGraphFamilyBrain`). Pick per-family via `FamilyData` so designers can author boss/elite families as Behavior Graphs while grunt families stay on the cheap code brain.
4. **Existing `EnemyBehaviorTree.cs` stays untouched** — per-enemy BT keeps working; the Behavior package enters at the *family-leader* tier, not the per-enemy tier. The two never fight (a follower under a family yields its travel to slots and its engagement to its own `EnemyBrain`/BT exactly as today).

**Net:** the Behavior package is adopted as an *optional authoring upgrade for leader brains*, gated behind `IFamilyBrain`, after the code-driven family ships and proves the loop. No greenfield rewrite; no critical-path dependency on a package we haven't validated on this Unity version.

---

## 7. NavMesh dependency + sequencing

Leader/follower formation following **requires a baked NavMesh** — followers `NavMesh.SamplePosition` their slots and drive `NavMeshAgent`s to them. This is the hard gate.

**Verified:** WO-142 §4 / WO-143 §8 explicitly flag that **the village interior IS NavMesh-baked (waves move) but the OUTER WORLD is NOT baked.** Therefore:

- **Families work in the village (baked) TODAY** — at the walls/exterior approaches where the baked mesh reaches, exactly the envelope WO-143 ships raids in. Slots that fall off-mesh snap to the nearest valid point and the family degrades gracefully (never errors), reusing `Enemy.DriveNav`'s existing log-once-and-hold path.
- **Open-world deep-region families are GATED on the exterior NavMesh bake.** That bake is a **CLI architect-lane line, editor-closed** (CLAUDE.md §3/§9 — UI never fires a bake), owned by the WO-142 World/Environment lane. This doc does **not** fire it.
- **Sequencing is explicit:** family code (leader/follower/formation/perception) and the village-tier demo can land in the Combat/AI lane **now**, in parallel, with zero scene/bake dependency. Full deep-region roaming **rides WO-142's future exterior NavMesh bake for free** — when it lands, slot sampling and follower paths reach the deep regions with no code change (same as WO-143's roamers).
- **Do NOT** build a bespoke off-NavMesh slot-steerer to dodge the bake — it would duplicate `NavMeshAgent` and create a second mover to maintain (same call WO-143 §8 made). If deep roaming is wanted before the bake, the exterior NavMesh bake is the cleaner path. **Flag for owner.**

---

## 8. Performance + pooling (owner spec, systematized)

- **Recalc slots only when needed** — on formation change or when the leader has moved past a significant-move threshold; otherwise reuse cached world slots. Not per-frame, not per-member.
- **Cache offsets** — local-space slot offsets per formation are computed once and reused; only the leader→world transform is cheap-applied.
- **Shared perception, cooldown-throttled** — one overlap scan per family per cooldown (reuse `EnemyBrain._scanBuffer[32]`, no new allocations), replacing N per-enemy per-frame aggro scans. One sees → all react.
- **One scorer call per family per eval interval** — leader scores via the dormant `_targetEvalTimer`/`TargetEvalInterval` throttle WO-145 wires (every ~2s, cached between), not every follower every frame.
- **Pool families** — recycle `MonsterFamily` + member `Enemy` instances rather than Instantiate/Destroy churn (the project already has pooling discipline; align with `VfxPool` patterns and `WaveManager`'s `_maxSimultaneousEnemies` cap). A `_maxLiveFamilies` / member budget shares the device enemy budget alongside `WaveManager` + WO-143 `RaidDirector` (PerfBudgetWindow / DEF-48). Caps sum to the device-tier enemy budget.
- **Followers path to near slots, not the far goal** — cheaper NavMesh path queries (the owner's stated perf win) and synchronized motion.
- **Leader-only Behavior Graph (F3)** keeps the expensive authored brain to one instance per pack, not per enemy.

---

## 9. Phased roadmap — what ships per phase

| Phase | Ships | Depends on | Gate |
|---|---|---|---|
| **0 — Visible test family (no scene edit)** | Repurpose `EnemyFamilyTestSpawner` (`J` hotkey, DDOL) to assemble 1 leader + N followers as a `MonsterFamily` in the **baked village**, LooseRing roam, so the slot/formation system is *seen* live. | village NavMesh (exists) | Owner watches a pack roam in formation; zero scene/bake work. |
| **1 — Core family layer** | `FormationType` (Core), `MonsterFamily`, `FamilyLeader` (code brain), `FamilyMember`, `FormationController` (slot calc, all 5 formations), `GroupPerception`. Leader uses WO-145 scorer; followers slot-follow then yield to `EnemyBrain` on engage. Leader-death promote+reform. OnDisable unsub discipline. | WO-145 scorer (or graceful fallback), village NavMesh | Family roams (LooseRing), detects (shared perception), forms up, approaches, attacks the focus-fired target, retreats to TightPack when hurt, reforms; leader death promotes. Headless-safe. |
| **2 — Raid integration** | `RaidDirector` (WO-143) spawns organised raids as `MonsterFamily` packs instead of N independent `RoamingRaider`s; danger tier scales family size/toughness via existing `ApplyWaveScaling`. | WO-143 landed, Phase 1 | A region raid is a coordinated pack with a leader, not a trickle; clearing it raises region Safety (WO-143 §7). |
| **3 — Behavior package leader brain** | Add `com.unity.behavior` to manifest (CLI, editor-closed); `IFamilyBrain` abstraction (retro-fit Phase 1 brain behind it); author the owner's Behavior Graph (`Selector(Roam→Chase→Attack)` + "Is Family Healthy?" decorator) as `BehaviorGraphFamilyBrain` for elite/boss families. Grunt families stay on the code brain. | Phase 1, package validates on 6000.4.8f1 | A boss/elite family runs an authored Behavior Graph; grunt families unchanged; per-family brain choice via `FamilyData`. |
| **4 — Deep-region families** | Families roam the deep outer-world regions (WO-142 geography) with full formation movement. | **WO-142 exterior NavMesh bake (CLI architect lane, editor-closed)** | Packs wander deep regions; no code change from Phase 1 (slots already NavMesh-sampled). |

Phases 0–1 land entirely in the Combat/AI code lane with no scene or bake dependency. Phase 4 is the only one hard-gated on a bake.

---

## 10. Next WOs to cut

1. **WO-146 — Monster Family core layer (Phase 1).** Create `FormationType` (Core), `MonsterFamily`/`FamilyLeader`/`FamilyMember`/`FormationController`/`GroupPerception` (`Assets/_Modules/Village/Families/`), optional `FamilyData` SO (Core). Leader uses WO-145 `ScoreAndPickTarget`; followers slot-follow via `Enemy.SetBrainTargetPosition`, yield to `EnemyBrain` on engage (WO-143 arbitration pattern); leader-death promote+reform; OnDisable unsub (WO-139 #4). Reuse `Enemy`/`EnemyBrain` verbatim — no edits. Village→Core only.
2. **WO-147 — Family test driver (Phase 0).** Additive change to `EnemyFamilyTestSpawner` (`J` hotkey) to assemble a `MonsterFamily` (1 leader + followers) in the baked village so the formations are visible with zero scene/bake. Keep the existing role-pack behaviour as a second hotkey.
3. **WO-148 — Raid families (Phase 2).** `RaidDirector` (WO-143) spawns organised raids as `MonsterFamily` packs; tier scales size/toughness via `ApplyWaveScaling`. Soft dep on WO-143 + WO-146; no edit to `Enemy`/`EnemyBrain`/`WaveManager`.
4. **WO-149 — Behavior-package leader brain (Phase 3).** Add `com.unity.behavior` to `Packages/manifest.json` (CLI, editor-closed); introduce `IFamilyBrain`; author the owner's leader Behavior Graph as one `IFamilyBrain` impl for elite/boss families; per-family selection via `FamilyData`. Do NOT touch `EnemyBehaviorTree.cs`.
5. **WO-150 (gated) — Deep-region families (Phase 4).** Depends on the **WO-142 exterior NavMesh bake** (CLI architect lane). No new family code expected — verification + tuning that packs roam deep regions once the mesh exists. Cut only after the bake lands.

**Cross-cutting flags for owner:**
- **Naming collision** with the existing `EnemyFamilyTestSpawner` ("family" = role pack today) — resolved here by naming the system `MonsterFamily`; confirm.
- **`com.unity.behavior` is not yet a dependency** — adoption adds a manifest entry that must be validated on Unity 6000.4.8f1 before any family relies on it (Phase 3 only).
- **Deep roaming before the exterior bake** would require a bespoke off-NavMesh mover — recommended against; prefer the WO-142 bake.
- **Utility scorer (WO-145) is a soft dependency** — Phase 1 should degrade to nearest-target if WO-145 hasn't landed, so families aren't blocked on it.
```
