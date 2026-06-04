# WORK ORDER 143 — Roaming Raids: a living threat layer beyond the narrow gate waves

**Status:** READY TO IMPLEMENT
**Date:** 2026-05-30
**Priority:** High — the DEFEND half of the core loop's "make the world dangerous, not just the wave timer." Delivers the second clause of the owner ask ("defend then build up, make safe from invading raids").
**Lane:** **Combat / AI — code only.** NO scene-file edits, NO `VillageSceneBuilder.cs`, NO bake fired from UI (CLAUDE.md §3/§9). Runs in the **Combat/AI parallel lane** (CLAUDE.md §9 — "EnemyBrain, ATB — code only, no scene files"), so it never contends with the World/Environment lane that owns WO-142's `OuterWorldBuilder`.
**Depends on:**
- **WO-142** (`OuterWorldBuilder` — the outer world these raiders roam; this WO *populates* its regions). Soft: the raid layer runs against the village exterior even before WO-142 dressing lands; it does not require WO-142's props.
- **WO-107** (`ZoneManager` + the four cardinal regions Goldfields/Stoneback/Mirewood/Ashwood + `GetZone(Vector3)` classification) — the region identity raids scale against. Reconcile, don't redefine.
- **WO-141 / WO-117** (`ResourceNode` harvest nodes) — raids *threaten* these; soft seam only (raids target a node if one is near; no hard ref required, null-safe).
- Existing built loop: `WaveManager`, `EnemyGroupSpawner`, `EnemyGroupCoordinator`, `Enemy`, `EnemyBrain`, `AmbientNPC` — **all reused, none replaced.**

**North Star:** `docs/NORTH_STAR.md` — line 60 *"DEFEND base + mines from waves **and roaming enemies** — or lose them"*; line 78 *"Waves + roaming enemies | `WaveManager` + `EnemyBrain` | the threat to your base + mines"*; Rung 3 *"Defend + Explore — a world beyond the walls"* (line 40). This WO is the literal implementation of the named-but-unbuilt "roaming enemies" pillar.

---

## Goal

A **roaming-raid layer** that runs *alongside* the scripted gate-lane waves: free-roaming raiders wander the outer-world regions, patrol/aggro on the player or base when near, and — on a cadence that coexists with `WaveManager` — occasionally organise into a **raid** that assaults the base from a region (not down the single scripted lane). Higher-danger regions field bigger/tougher raids (danger = reward; correlates with crystal-richness). **Clearing/holding a region drops its raid pressure** — the build-up payoff that "makes safe."

Crucially this is a **parallel spawning + AI layer that reuses `Enemy` + `EnemyBrain` + `EnemyGroupSpawner`** — NOT a new enemy class hierarchy. The only new runtime types are the *coordinators* (a roaming spawner/director + a per-region threat model + a roam-behaviour overlay), all built on the existing `Enemy`/`EnemyBrain` body.

---

## 0. RECONCILE — what already exists (read before writing a line; this is the project's #1 trap)

I read `WaveManager.cs`, `Enemy.cs`, `EnemyBrain.cs`, `EnemyGroupSpawner.cs`, `EnemyGroupCoordinator.cs` (referenced), `WaveSpawnPoint.cs`, `EnemyTacticalState.cs`, `WaveEnemyGroup.cs` (EnemyRole), `AmbientNPC.cs`, `ZoneManager` (WO-107), WO-141/142, NORTH_STAR, and CLAUDE.md §5/§9 before writing this.

| System | Where (verified) | What it already does | How WO-143 relates |
|---|---|---|---|
| **`WaveManager`** | `Assets/_Modules/Village/Waves/WaveManager.cs` | The scripted gate-lane loop: countdown → spawn batches/groups at `WaveSpawnPoint`s → inner-ring breach detect → ATB / Defend-the-Tower hand-off. Owns `WavePhase`, `_liveEnemies`, the `OnDisable` unsub pattern (WO-139 #4 hardened this), the stuck-enemy failsafe, and crystal awards. | **DO NOT MODIFY its loop.** WO-143 runs as a **sibling MonoBehaviour** (`RaidDirector`) that NEVER writes `WaveManager` state. It only *reads* `WaveManager.Phase` (public) to decide *when* a raid is polite to launch, so the two layers don't pile on. See §5 coexistence. |
| **`Enemy`** | `Assets/_Modules/Village/Enemies/Enemy.cs` | The single enemy body: `NavMeshAgent` march, HP, contact attack on `IDamageableStructure`, death/XP/VFX, `Configure(id, EnemyDef, heart)`, `SetBrainTarget`/`SetBrainTargetPosition`, `Died`/`ReachedHeart` events, `Kill()`. | **REUSE VERBATIM** as the raider body. A raider IS an `Enemy` — same prefab path, same `Configure`. The raid layer hands it a roam/base target instead of the Heart. |
| **`EnemyBrain`** | `Assets/_Modules/Village/Enemies/EnemyBrain.cs` | Role targeting (`EnemyRole` Tank/Healer/DPS…) + tactical states (`EnemyTacticalState` Rush/Flank/Retreat/Suppressed) over `Enemy`. Picks a target, computes a destination, calls `Enemy.SetBrainTargetPosition`. `Awake` caches Heart + hero (`HeroTarget`/`Player` tags). **`TryAttack()` is declared-but-unimplemented (WO-139 #7) — brain enemies steer but deal no brain damage; the hero/structure only take damage from `Enemy`'s own contact probe.** | **REUSE.** The roam overlay (§3) is an *additive new tactical state path*, NOT a fork of EnemyBrain's role switch. Roaming raiders still run EnemyBrain for hero/tower/structure engage once aggro'd. WO-139 #7 is relevant: a *patrolling* raider that bumps a `ResourceNode` or wall relies on `Enemy.TickContactAttack` (which DOES damage `IDamageableStructure`) — so node/wall damage already works; only hero brain-damage is the open #7 gap, out of scope here. |
| **`EnemyGroupSpawner` + `EnemyGroupCoordinator`** | `Assets/_Modules/Village/Waves/EnemyGroupSpawner.cs` | `SpawnGroup(WaveEnemyGroup, pos, heart, root, waveId, ref counter)` instantiates a `WaveEnemyGroup` in formation, NavMesh-snaps each, sets `EnemyBrain.Role`, and registers them with a per-group `EnemyGroupCoordinator` that holds them Suppressed until all spawned, then releases together. Returns `List<Enemy>`. | **REUSE for the "organised raid" event (§4).** A raid party spawns via `SpawnGroup` exactly like a wave group — same formation + suppress-release charge — but at a **region origin**, marching a **base/POI anchor**, owned by `RaidDirector` not `WaveManager`. No new spawner. |
| **`WaveEnemyGroup` + `EnemyRole`** | `Assets/_Modules/Village/Waves/WaveEnemyGroup.cs` | SO defining group entries/formation/threat; `EnemyRole { Tank, Healer, DPS, Ranged, MiniBoss }`; `ThreatValue`; `GetFormationOffset`. | **REUSE as the raid-party authoring asset.** Raid parties ARE `WaveEnemyGroup` assets (a "raid roster"). No new SO needed for the party composition. |
| **`AmbientNPC`** | `Assets/_Modules/Village/NPCs/AmbientNPC.cs` | The wander pattern: NavMesh roam around a home anchor (`PickNewDestination` inside `_roamRadius`, pause, repeat), graceful idle if no NavMesh, proximity engage. | **REFERENCE PATTERN for roam (§3).** The roamer's idle-wander math (random NavMesh point in a radius, pause, repeat; degrade to idle with no NavMesh) is lifted conceptually — but a *raider* roams as an `Enemy`+`EnemyBrain`, NOT as an `AmbientNPC`. Do not make raiders AmbientNPCs (they need HP/combat/death). |
| **`ZoneManager` (WO-107)** | `Assets/_Modules/Environment/ZoneManager.cs` | Names the 4 cardinal regions; `GetZone(Vector3)` classifies a world point N/E/S/W (z>30 N, z<-30 S, x>30 E, else W). | **REUSE for region classification.** The per-region threat model keys off this exact classification. Do NOT add a second region enum. If `ZoneManager` isn't in the scene, fall back to the same N/E/S/W rule inline (null-safe). |
| **`WaveSpawnPoint`** | `Assets/_Modules/Village/Waves/WaveSpawnPoint.cs` | The 4 gate-lane spawn markers (`spawn-0..3`, `GateIndex`, `Direction`, `HeadingToGate`). | **READ-ONLY reference.** Raids do NOT spawn at these (that's the *narrow* lane). Raids spawn at **region perimeters** away from the gate corridor — that's the whole point. The director may *read* a spawn point's `Direction` to map a region → a base anchor, but never spawns raiders on the lane. |

**Hard reconciliation rules:**
- **One enemy body (`Enemy`), one brain (`EnemyBrain`), one group spawner (`EnemyGroupSpawner`), one region identity (`ZoneManager`).** WO-143 adds only: a **director** (when/where raids spawn), a **threat model** (per-region pressure + make-safe), and a **roam tactical overlay** (idle-wander until aggro). Nothing else.
- **`WaveManager` is not edited.** The two loops coexist by the director *reading* `WaveManager.Phase`, never writing it (§5).
- Reconcile, never blind-replace (memory *wo-batch-reconcile-not-replace*).

---

## 1. Architecture (assembly discipline — CLAUDE.md §5/§6)

All new code lives in **`DeNelle.Village`** (it drives `Enemy`/`EnemyBrain`, which are Village). One shared enum goes in **`DeNelle.Core`**. **Village → Core only.** No HUD reference; cross-module calls go through `CoreServices.Hud?.` / `CoreServices.Audio?.` with `?.` (CLAUDE.md §6). No `System.Reflection` introduced (the reflection bridge is only for the established cross-asmdef cases — not here).

| New type | Assembly / path | Role |
|---|---|---|
| `RaidRegion` (enum) | `DeNelle.Core` — `Assets/_Modules/Core/RaidRegion.cs` | `{ North, East, South, West }` — the four cardinal regions, mirroring `ZoneManager`'s classification. Pure data in Core so a future HUD/save can reference it without a Village ref. **Reuse WO-107's region identity — this enum is just the typed handle for it; map 1:1 to ZoneManager's N/E/S/W. Do NOT introduce a different naming.** If WO-107 later exposes its own region enum, converge on ONE (flag for owner). |
| `RegionThreat` (data class / struct) | `DeNelle.Village` — folded into `RaidDirector` or its own file `Assets/_Modules/Village/Raids/RegionThreat.cs` | Per-region runtime state: `Pressure` (0..1 raid intensity), `DangerTier` (from region scaling §6), `Safety` (0..1 — rises as you clear/hold, drops raid rate), live-raider count. |
| `RoamingRaider` (overlay MonoBehaviour) | `DeNelle.Village` — `Assets/_Modules/Village/Raids/RoamingRaider.cs` | A thin overlay added alongside `Enemy`+`EnemyBrain` on a raider. Owns the **roam-vs-aggro** state: idle-wander a region (AmbientNPC-style) until the player/base/node comes within an aggro radius, then yield to `EnemyBrain` to engage. Additive — does not modify `Enemy`/`EnemyBrain`. |
| `RaidDirector` (scene MonoBehaviour) | `DeNelle.Village` — `Assets/_Modules/Village/Raids/RaidDirector.cs` | The sibling-of-`WaveManager` coordinator: holds the four `RegionThreat`s, spawns roamers + organised raids per region, reads `WaveManager.Phase` for coexistence, applies region scaling, owns the make-safe loop. Self-contained sub-system MonoBehaviour like `WaveManager`. |
| `RaidPartyData` (optional) | — | **Do NOT create.** Raid parties reuse `WaveEnemyGroup` assets (§0). |

---

## 2. Where raiders spawn — outer world vs the narrow gate lane

The defining distinction from waves:

- **Gate waves** (`WaveManager`): spawn at the 4 `WaveSpawnPoint`s (`spawn-0..3`), march the single scripted approach lane straight at the Heart, breach the inner ring → ATB/DTT hand-off. Unchanged.
- **Roaming raiders** (`RaidDirector`): spawn at a **region perimeter ring** (a radius band well outside the village footprint and *off* the gate corridor — respect the same gate-clear rule WO-142 §2 honors), classified into one of the four `RaidRegion`s via `ZoneManager.GetZone` (or the inline N/E/S/W fallback). They do **not** target the Heart by default — they wander their region (§3) and only converge on the base/a node when aggro'd or when the director promotes them into an organised raid (§4).

**Spawn placement rule (null-safe, NavMesh-aware):**
1. Pick a point on the region's perimeter ring (region centre is WO-107's `(0,0,±80)` / `(±80,0,0)`; ring radius is a tunable inspector band, e.g. 60–110m from origin).
2. Keep it outside a **gate-corridor exclusion** (the first ~8m of each lane) so raiders never spawn on top of the scripted lane (mirrors WO-142 §2.6).
3. `NavMesh.SamplePosition(raw, out hit, 8f, AllAreas)` snap — **same guard as `WaveManager.SpawnOne` / `EnemyGroupSpawner`.** If no NavMesh nearby, see §7 (movement call).

---

## 3. Roam / patrol behaviour (the "wandering, not just timer" feel)

A spawned roamer is an `Enemy` + `EnemyBrain` + the new `RoamingRaider` overlay. `RoamingRaider` owns a small state machine:

- **Roam** (default): pick a random NavMesh point within a roam radius of its region anchor, walk there (drive via `Enemy.SetBrainTargetPosition(point)` — the existing override path), pause, pick another — **exactly the `AmbientNPC.PickNewDestination` pattern** (random disc in radius → `NavMesh.SamplePosition` → set destination → arm a pause). While roaming, `RoamingRaider` sets the destination; it does NOT fight `EnemyBrain` (see arbitration below).
- **Aggro**: each tick, check distance to (a) the hero (`HeroTarget`/`Player` tag, same lookup EnemyBrain uses), (b) the base/Heart, (c) the nearest `ResourceNode` (WO-141, soft/optional). If any is within `_aggroRadius`, switch to **Engage**.
- **Engage**: stop driving the roam destination — clear the override (`SetBrainTargetPosition(null)`) and let **`EnemyBrain` take over** its normal role targeting (hero engage / nearest tower / nearest structure / Heart march). This is the reuse seam: aggro'd raiders behave exactly like wave enemies. When the aggro source leaves a (hysteresis-widened) radius and the raider isn't part of an organised raid, it may de-aggro back to Roam.

**Arbitration with EnemyBrain (critical, additive):** `EnemyBrain.Update` and `RoamingRaider.Update` both want to call `Enemy.SetBrainTargetPosition`. To avoid a tug-of-war **without editing EnemyBrain**, `RoamingRaider` runs in a later script-execution order (or simply: while in Roam state, `RoamingRaider` disables `EnemyBrain` via `enabled = false` and owns the destination; on Engage it re-enables `EnemyBrain` and stops writing the destination itself). Disabling a sibling MonoBehaviour is additive and reversible — no EnemyBrain code change. Document this clearly in the file header.

- **No NavMesh present:** the roamer **idles gracefully** (no error, no drift) — identical to `AmbientNPC`'s degrade path and `Enemy.DriveNav`'s "log once, hold position." See §7.

**Tactical states reuse:** organised-raid members (§4) still use `EnemyGroupCoordinator`'s Suppressed→Rush release and any `TacticalData` (Flank/Retreat) on their prefab — that's already wired through `EnemyGroupSpawner`. Roaming solo raiders just use Rush once engaged.

---

## 4. Raid events — the organised assault (layered on top of waves)

Beyond ambient roamers, the director occasionally promotes a region's pressure into a **raid event**: a coordinated pack that decides to assault the base.

- **Composition:** a `WaveEnemyGroup` asset (the "raid roster") spawned via the existing `EnemyGroupSpawner.SpawnGroup(...)` — same formation + `EnemyGroupCoordinator` suppress-release charge as a wave group, but:
  - **Origin:** the raiding region's perimeter (not a gate lane spawn point).
  - **Target:** the base anchor / Heart transform passed as `heart` to `SpawnGroup` (so they march the base), OR a `ResourceNode` if the raid is a "node raid" (danger = they're after your richest harvest — ties WO-141).
  - **Ownership:** `RaidDirector` adds the returned `List<Enemy>` to its OWN live-raider list and subscribes `Died`/`ReachedHeart` — it does **not** put them in `WaveManager._liveEnemies` (keeping the two loops' rosters separate is what prevents them fighting).
- **Trigger / cadence (coexists with `WaveManager`, doesn't fight it):**
  - A per-region **raid timer** scaled by that region's `DangerTier` and inverse `Safety` (a dangerous, un-held region raids more often; a held one rarely). Base cadence is a tunable inspector range (e.g. one organised raid every 90–180s in the most dangerous region; far rarer in safe regions).
  - **Politeness gate:** the director only *launches* an organised raid when `WaveManager.Phase` is `Countdown` or `Active` **but not** `Breached`/`Complete` — i.e. never stack a raid on top of an in-progress breach hand-off (the player is in the ATB/DTT scene then). Reading `WaveManager.Phase` (public getter) is the only `WaveManager` touch. If no `WaveManager` is in the scene, the director runs raids unconditionally (it's self-sufficient).
  - **Concurrency cap:** a hard `_maxLiveRaiders` cap (mirrors `WaveManager._maxSimultaneousEnemies` perf discipline, DEF-48) so roamers + a raid never blow the enemy budget. New spawns stall when capped.

**Raid resolution:** unlike gate waves, an organised raid does **not** force the ATB/DTT hand-off — it's a real-time defend-in-the-world fight (the player/towers/walls kill it via the normal `Enemy` damage path). If raiders reach the base structures they damage them via `Enemy.TickContactAttack` → `IDamageableStructure.ApplyContactDamage` (already works for walls/towers/Heart/nodes). A raider that crosses the **inner ring** is left to `WaveManager`'s existing breach detection if a wave is also active — but to avoid double-handling, the director's raiders are NOT in `WaveManager._liveEnemies`, so `WaveManager` won't see them; the **director owns its own "raider reached Heart" response** (e.g. apply Heart damage / escalate threat / optionally surface its own breach choice). **Owner decision flag:** whether a raider reaching the Heart should (a) just damage it (recommended — keeps raids as the real-time pressure layer distinct from the scripted breach cutscene), or (b) trigger the same `BreachChoiceOverlay` hand-off. Recommend (a) for the first cut.

---

## 5. Coexistence with the wave loop (don't break the gate lane)

The hardened wave loop (WO-139 #4 event-leak fixes) must stay intact:

- **No writes to `WaveManager`.** `RaidDirector` only reads `WaveManager.Phase` (public). It never touches `_liveEnemies`, `_breachRoster`, the countdown, or the phase.
- **Separate rosters.** Director-spawned raiders live in `RaidDirector`'s own list; wave enemies live in `WaveManager._liveEnemies`. No enemy is in both.
- **Mirror the OnDisable unsub discipline (WO-139 #4).** `RaidDirector.OnDisable` must unsubscribe `Died`/`ReachedHeart` from every live raider and clear its lists, exactly as `WaveManager.OnDisable` does — so stale callbacks never fire into a torn-down director across a scene reload. This is a hard acceptance item.
- **Shared perf budget.** Roamers + raiders + wave enemies all instantiate `Enemy`/`NavMeshAgent`s. The director's `_maxLiveRaiders` is sized to leave headroom under the device budget alongside `WaveManager._maxSimultaneousEnemies` (PerfBudgetWindow / DEF-48). Recommend the two caps sum to the device-tier enemy budget.

---

## 6. Region difficulty scaling (danger = reward; "more hordes / higher levels")

Each `RaidRegion` carries a `DangerTier` that scales raid size + toughness, correlating with the region's narrative dread gradient (WO-142: Goldfields safest → Ashwood front line) and with crystal-richness (the parallel crystal-subtype WO — richer regions raid harder):

| Region | Dir | Dread (WO-142) | Default DangerTier | Raid feel |
|---|---|---|---|---|
| Goldfields | E | low | 1 (lowest) | small, infrequent roamer parties; gentle |
| Stoneback | W | neutral | 2 | medium roamers; occasional organised raid |
| Mirewood | S | heavy | 3 | the main lane's region — frequent, larger raids |
| Ashwood | N | front | 4 (highest) | biggest/toughest raids, fastest cadence — the rotting front |

**How scaling applies (pure data + existing knobs — no new stat system):**
- **Bigger:** higher tier → larger raid-party `WaveEnemyGroup` (more entries) and/or more concurrent roamers per region. Tier indexes a director-side list of raid-roster SOs (e.g. tier 4 picks a heavier `WaveEnemyGroup`).
- **Tougher:** reuse the **existing `WaveScalingCurve`** (already on `WaveManager`, `Enemy.ApplyWaveScaling(hp, speed, dmg)`) — the director applies a tier-derived multiplier through the *same* `ApplyWaveScaling` call right after `Configure`, exactly as `WaveManager.SpawnOne` does. No new scaling math. Higher tier = higher HP/damage multiplier.
- **Owner-tunable:** tiers, cadences, and roster-by-tier are inspector/SO data on `RaidDirector` so balance is a no-rebuild change. The default tiers above are a starting point — flag for owner balance.

This is the mechanical answer to "supplementing the narrow waves" and "more hordes, higher levels": the world's danger is regional and graded, and the richest regions are the most dangerous to harvest in.

---

## 7. "Make safe" — clearing/holding a region drops its raid pressure (the build-up payoff)

This is the second half of the loop ("defend then build up, make safe"). Each region has a `Safety` value (0 = fully contested, 1 = held/safe). `RaidDirector` runs the make-safe loop:

- **Safety rises** when the player *holds* a region: e.g. killing that region's raiders without losing structures, or no raider being alive in-region for a sustained window, or (soft tie) claiming/defending its `ResourceNode`s (WO-141/WO-112 ward claim). Recommend a simple first cut: **each cleared raid (all its raiders dead) raises that region's `Safety`; a raider damaging a base structure lowers it.**
- **Safety lowers raid pressure:** the per-region raid cadence and roamer count scale by `(1 - Safety)` against the `DangerTier` base — a held region raids rarely; a contested one raids hard. A safe region can decay back toward danger slowly if neglected (optional `safetyDecayPerSecond`, default 0 for the first cut — flag for owner; the idle-decay tension is a nice-to-have, not required).
- **Player-visible:** push region safety/threat to the HUD via `CoreServices.Hud?.` (a `SetRegionThreat(region, pressure)`-style seam, with `?.`, null-safe — **do NOT edit the HUD here**, just call the seam if it exists; if not, log/no-op and leave it for the HUD WO). The mechanic must work headless without the HUD.

So: build up walls/towers (WO-108/137) + clear raids → region goes safe → fewer raids there → you can harvest its (richest) nodes in peace → push into the next, more dangerous region. That is the DEFEND → BUILD UP → MAKE SAFE loop, made real.

---

## 8. NavMesh / movement — the call (flagged per WO-142)

**Finding:** `Enemy`, `EnemyBrain`, `EnemyGroupSpawner`, and `AmbientNPC` all drive `NavMeshAgent`s and all already **degrade gracefully with no NavMesh** (log once + hold / disable agent + idle — see `Enemy.DriveNav` lines ~436–447 and `AmbientNPC.Start`'s `_hasNavMesh` guard). The village interior IS NavMesh-baked (waves move). **The outer world is NOT NavMesh-baked** (WO-142 §4 explicitly notes this and ships outer-world NPCs as idlers for that reason).

**The call (recommended, lowest-risk, additive):**
1. **First cut — NavMesh-only, no new bake required from this WO.** Raiders spawn on the perimeter ring and **`NavMesh.SamplePosition` snap** to the nearest baked surface (same guard as the wave path). Where the baked NavMesh reaches (the village exterior near the walls / gate approaches), raiders roam and engage normally. Beyond the baked mesh they snap to the nearest valid point and hold/idle — **never error** (the existing degrade path covers this). This means raids are *fully playable as a base-defense pressure layer at the walls/exterior today*, with zero bake and zero scene edit — satisfying the lane constraint.
2. **For true deep-region roaming (later, NOT this WO):** an exterior NavMesh bake is needed. That is a **CLI architect-lane bake line** (WO-142's domain — it already flags baking an exterior NavMesh as a follow-up phase). **This WO does NOT fire that bake** (CLAUDE.md §3/§9). When the exterior NavMesh lands, roamers automatically gain full deep-region wander with no code change (they already sample the mesh).
3. **Do NOT** implement a bespoke off-NavMesh steering/waypoint mover in this WO. It would duplicate `NavMeshAgent` and create a second movement system to maintain. If the owner wants deep roaming before an exterior bake, the cleaner path is the WO-142 NavMesh bake — recommend that over a custom steerer. **Flag for owner.**

**Net:** raids ship NavMesh-only, no new bake, playable at the exterior/walls now; deep-region wander rides WO-142's future exterior NavMesh bake for free.

---

## Files to Create / Edit

| File | Action | Note |
|---|---|---|
| `Assets/_Modules/Core/RaidRegion.cs` | **Create** | `enum RaidRegion { North, East, South, West }` in `DeNelle.Core`. Pure data. Maps 1:1 to WO-107 `ZoneManager` N/E/S/W. Converge with any WO-107 region enum (flag). |
| `Assets/_Modules/Village/Raids/RaidDirector.cs` | **Create** | Sibling-of-WaveManager coordinator: 4 `RegionThreat`s, roamer + organised-raid spawning via `EnemyGroupSpawner`, reads `WaveManager.Phase`, region scaling via existing `WaveScalingCurve`/`ApplyWaveScaling`, make-safe loop, `OnDisable` unsub (WO-139 #4 discipline). Self-contained MonoBehaviour. |
| `Assets/_Modules/Village/Raids/RoamingRaider.cs` | **Create** | Roam-vs-aggro overlay added alongside `Enemy`+`EnemyBrain`. AmbientNPC-style wander via `Enemy.SetBrainTargetPosition`; yields to `EnemyBrain` on aggro (enable/disable arbitration, additive). Graceful no-NavMesh idle. |
| `Assets/_Modules/Village/Raids/RegionThreat.cs` | **Create (or fold into RaidDirector)** | Per-region `Pressure`/`DangerTier`/`Safety`/live-count state + the scaling/decay helpers. |
| `Assets/Data/Raids/RaidRoster_*.asset` | **Create** | `WaveEnemyGroup` instances used as raid parties, one (or a few) per DangerTier. **Reuse the existing SO type — do NOT make a new SO.** |
| `Assets/_Modules/Village/Waves/WaveManager.cs` | **Reference only — DO NOT EDIT** | Read `WaveManager.Phase` (public getter). No code change. |
| `Assets/_Modules/Village/Enemies/Enemy.cs` / `EnemyBrain.cs` / `EnemyGroupSpawner.cs` | **Reference only — DO NOT EDIT** | Reused verbatim. (If a tiny additive read-only accessor is unavoidable, flag it for owner first — default is zero edits.) |
| `IVillageHud` / `VillageHudController` | **Reference only — DO NOT EDIT** | Push region threat via `CoreServices.Hud?.` if the seam exists; else no-op. HUD WO owns the display. |

**What NOT to create:** a new enemy class/hierarchy, a second enemy body, a parallel spawner, a new region/zone enum that diverges from WO-107, a new raid-party SO type (reuse `WaveEnemyGroup`), a bespoke off-NavMesh mover, any `VillageSceneBuilder` edit, any scene file.

---

## What NOT to touch

- **`WaveManager.cs`** — read `Phase` only; never write its state, roster, or phase. The WO-139 #4 OnDisable/unsub hardening must remain untouched.
- **`Enemy.cs` / `EnemyBrain.cs` / `EnemyGroupSpawner.cs` / `EnemyGroupCoordinator.cs`** — reused verbatim; no edits (the roam overlay is additive and external). WO-139 #7 (`EnemyBrain.TryAttack` unimplemented) is acknowledged but **out of scope** — node/wall damage already works via `Enemy.TickContactAttack`; do not implement #7 here.
- **`VillageSceneBuilder.cs`** — frozen serialization bottleneck (CLAUDE.md §3/§9). No edit, no bake fired from UI.
- **`Village.unity`** — never hand-edited (CLAUDE.md §3). `RaidDirector` is added/run at runtime or via a future architect-lane rebake line owned by CLI — not by this WO.
- **No exterior NavMesh bake** fired here — that's a WO-142/CLI architect-lane line (§8).
- **The HUD** (`IVillageHud`/`VillageHudController`) — call the `CoreServices.Hud?.` seam only; don't edit it.
- **`ResourceNode`/`CrystalMine` (WO-141)** — soft, optional target only (raid a node if near); no edit, null-safe.
- **No `System.Reflection`** introduced; **no UXML** (PIPELINE_STATE.md §8); **Village → Core only** (no `DeNelle.HUD` ref).
- ATB, WalletService, monetization, clan, backend — untouched.

---

## Acceptance Criteria

- [ ] `RaidRegion` enum present in `DeNelle.Core` (North/East/South/West), mapping 1:1 to WO-107 `ZoneManager` classification — no divergent region enum introduced.
- [ ] `RaidDirector` compiles, runs as a self-contained Village MonoBehaviour, and spawns **roaming raiders at region perimeters** (NOT at the `WaveSpawnPoint` gate lanes), NavMesh-snapped with the same `SamplePosition(…, 8f, …)` guard as `WaveManager.SpawnOne`.
- [ ] Roamers **wander** their region (AmbientNPC-style random-point roam via `Enemy.SetBrainTargetPosition`) and **aggro** the hero / base / nearby `ResourceNode` within an aggro radius, then **yield to `EnemyBrain`** for engagement (verified by the arbitration: EnemyBrain is re-enabled / RoamingRaider stops driving on Engage) — **without editing `Enemy` or `EnemyBrain`.**
- [ ] **Organised raids** spawn a `WaveEnemyGroup` via the existing `EnemyGroupSpawner.SpawnGroup(...)` from a region origin toward a base/node anchor, with suppress-release charge intact.
- [ ] **Coexistence:** `RaidDirector` only **reads** `WaveManager.Phase`; never writes `WaveManager` state. Director raiders are NOT in `WaveManager._liveEnemies`. Raids do not launch during `Breached` phase. With no `WaveManager` in scene, raids still run.
- [ ] **OnDisable unsub (WO-139 #4 discipline):** `RaidDirector.OnDisable` unsubscribes `Died`/`ReachedHeart` from every live raider and clears its lists — no stale callbacks across reload.
- [ ] **Region scaling:** higher-`DangerTier` regions field bigger and/or tougher raids; toughness applied through the **existing** `Enemy.ApplyWaveScaling`/`WaveScalingCurve` path (no new stat system). Tiers/cadence/rosters are owner-tunable data.
- [ ] **Make-safe:** clearing a region's raids raises its `Safety`; higher `Safety` measurably lowers that region's raid cadence/count. Works headless (no HUD required).
- [ ] **Perf:** a `_maxLiveRaiders` cap stalls new raider spawns at capacity (DEF-48 discipline), sized to share the device enemy budget with `WaveManager`.
- [ ] **NavMesh call honored:** raiders ship NavMesh-only, snap-and-degrade gracefully off-mesh (log/idle, never error); **no exterior NavMesh bake fired from this WO**; deep-region wander deferred to WO-142's exterior bake.
- [ ] HUD region-threat push is via `CoreServices.Hud?.` with `?.` (null-safe) — no HUD edit; runs fine if the seam is absent.
- [ ] `DeNelle.Village` references **Core only**; no `DeNelle.HUD` ref; no `System.Reflection`; no UXML.
- [ ] **No edit** to `WaveManager.cs`, `Enemy.cs`, `EnemyBrain.cs`, `EnemyGroupSpawner.cs`, `VillageSceneBuilder.cs`; **no hand-edit** to `Village.unity`; **no bake fired from UI.**

---

## Done checklist (CLAUDE.md §10)

- [ ] Brace balance check passed on every `.cs` file created (`RaidRegion.cs`, `RaidDirector.cs`, `RoamingRaider.cs`, `RegionThreat.cs`).
- [ ] No `.unity` scene file hand-edited; no bake fired from UI (raid director added at runtime / future architect-lane line owned by CLI).
- [ ] No new `System.Reflection` usage introduced.
- [ ] `using DeNelle.Core.Combat;` present in any file referencing `IDamageableStructure` (only if a raider directly probes structures beyond `Enemy`'s own contact path — default: none, `Enemy` already owns it).
- [ ] Null-conditional operators (`?.`) used on all cross-module service calls (`CoreServices.Hud?.` / `CoreServices.Audio?.`) and on the optional `WaveManager` / `ResourceNode` references.
- [ ] `WaveManager.Phase` is the ONLY `WaveManager` member touched, and read-only.
- [ ] Acceptance criteria reviewed line by line.
- [ ] If any `Enemy`/`EnemyBrain`/`EnemyGroupSpawner` edit turned out unavoidable, it was flagged to owner first and kept strictly additive (default expectation: zero edits to those files).
