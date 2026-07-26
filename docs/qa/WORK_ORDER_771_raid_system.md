# WORK ORDER 771 — COC-Style Raid / Attack System (build plan, v2)

**Status:** SPEC (v2 — rebuilt after an adversarial implementability review of v1).
New `DeNelle.Raid` module; sub-orders sequenced by dependency.
**Date:** 2026-07-26
**Author:** raid-systems design pass.
**Design reference:** `docs/qa/coc-raid-system-design.md` (Parts A/B).

**Goal (owner):** attacker deploys troops on a defender's base → combat runs
**automatically** (watch, don't micro) → scored COC-style (stars / loot / %
destruction) → **deterministic replay** + async matchmaking → a **Barracks upgrade
structure** that unlocks troops and scales reach/strength/special abilities. Reuse
existing systems **where the code actually supports it** (v1 over-claimed reuse; this
version pins what is real vs new).

**Art:** troops + defenders + enemies use **KayKit Adventurers + Skeletons**
(animation-ready, modular armor/weapons, mobile-perf); Tripo/custom heroes for hero
units only. One shared Animator Controller — built once in **WO-771.13** — drives
troops, enemies, and the dungeon hero (unblocks WO-770.10). `/Assets/Models/` is
gitignored → packs travel by zip; prefabs resolve by GUID; a builder reconstructs.

> **v2 fixed what v1 got wrong** (from the review): `IDamageableStructure` lives in
> `DeNelle.Village`, **not** Core → WO-771.0 moves it before anything else. **No tower-
> fire code exists** → tower combat is greenfield (WO-771.10), not reuse. `Pet.cs` is at
> `Assets/_Modules/Pets/Pet.cs`; `Enemy.cs` at `Assets/_Modules/Village/Enemies/Enemy.cs`.
> `SaveSchema.CurrentVersion` is **already 10** → one consolidated v11 migration
> (WO-771.1b), not three. Loot grant is a **new** mutator on the `GameStateService`
> patch pattern (`:266`), not `RecordRun`. And the spatial sim's **float/pathing
> determinism is NOT covered by the ATB proof** → a mandatory determinism discipline
> (below) plus a deterministic breach-aware pathing decision (WO-771.3).

---

## Shared determinism discipline (BINDING on WO-771.3, .7, .9, .10)

The replay + server anti-cheat contract requires `{seed, deployLog, baseSnapshot} →
byte-identical result on any machine (mobile ARM play vs x86 server re-sim)`. The ATB
engine is deterministic only because it is **turn-based `double` math with no geometry**
(`Engine/Rng.cs` mulberry32 + `Combat.cs` all-`double`, proven by `BattleATB/Tests/
RngGoldenVectorTest.cs`). The raid adds movement/distance/range — **new** determinism
surface. Therefore, non-negotiable for all sim code:

1. **No `float`, no `UnityEngine.Mathf`, no `Vector2/3` in the sim hot path.** Use
   **fixed-point (Q32.32 `long`)** or pinned `double` with integer-only comparisons.
   `sqrt`/normalize via a deterministic fixed-point routine — never `Mathf.Sqrt` (float,
   arch-varying). (`Math.Sqrt(double)` is IEEE correctly-rounded/deterministic, but the sim
   is fixed-point, so use the fixed-point routine for consistency.)
2. **Iterate stable ordered lists, never dictionaries.** `BuildingDamage` is a
   `SerializableDict` (`GameState.cs:82`); snapshot/target lists derived from it must be
   sorted to a fixed order before the sim reads them.
3. **Tie-breaks by lowest stable index**, mirroring `Ai.cs:82`'s strict `<` (earliest
   wins) — explicit, for equidistant targets.
4. **Define tick-damage rounding once.** `BattleUnit.Hp` is `int` (`Types.cs:264`);
   troop DPS is fractional. Accumulate damage in fixed-point and apply the rounding rule
   (banker's vs floor) identically every 1/30 s tick so golden vectors survive Barracks
   upgrades (WO-771.9 acceptance depends on this).
5. **A golden-vector test (`RaidSimGoldenTest`) is an acceptance gate** on every WO that
   touches the sim.

---

## Corrected reuse map (verified against code)

| Need | Real reuse | New work |
|---|---|---|
| Deterministic per-hit damage number | `Combat.CalculateDamage` (`Engine/Combat.cs:46`, all `double`) via a `BattleUnit` adapter | DPS-over-time, Flame splash, `wallDamageMult`, heal/s = new sim resolution |
| Seeded RNG | `Engine/Rng.cs` mulberry32 (`RngGoldenVectorTest`) | fixed-point vector math |
| "Run to completion, hands-free" precedent | `AutoResolveBattle` (`Engine/Turn.cs:260`) proves the pattern | the spatial sim body is new |
| Troop actor (presentation) | `Pets/Pet.cs` hunt/move/attack shape | **disable** its `Physics.OverlapSphere` acquisition (`Pet.cs:299`) + `System.Random(GetInstanceID())` in raid mode; interpolate to sim ticks |
| Base snapshot | `GameState` `Towers:72`/`TowerAbilities:74`/`WallLevel:76`/`BuildingDamage:82` | snapshot record + capture |
| Being-damaged contract | `IDamageable` in `DeNelle.Core.Combat` (`IDamageable.cs:42`, `TakeDamage(float, DamageElement)`) | troops need an `IDamageable` adapter (Pet's `TakeDamage(float)` at `Pet.cs:268` has the wrong signature; enemies use `EnemyDamageable.cs:41`) |
| Structure-damage contract | — | `IDamageableStructure` is in **`DeNelle.Village`** (`Enemy.cs:34/43`) → **move to Core** (WO-771.0) |
| Tower firing | **none — greenfield** (`towers.json range/damage/cooldownSeconds` is unwired data; towers are `List<int>` tiers) | WO-771.10 |
| Loot grant | `GameStateService` patch pattern (`:266`, `if p.Resources.HasValue`) | new `LootStash→Resources` mutator (not `RecordRun`, `:321`) |
| Special abilities data | `AbilityDef`/`StatusKind` **types** (`Types.cs:178,33`) | real-time (per-tick) resolution ≠ ATB turn-based `StatusEffect.Turns` |
| Async social | `ClanService` stub seam (`ClanService.cs:5-9`), `Inbox`/`MyInviteCode` (`GameState.cs:128,134`) | matchmaker stub |

**Save schema:** `SaveSchema.CurrentVersion` is **already 10** (`SaveSchema.cs:30`). All
new `GameState` fields (`TroopRoster`, `TrophyRating`, `BarracksLevel`, `TroopLevels`)
land in **one** v10→v11 migration owned by **WO-771.1b**.

**Critical path:** 771.0 → 771.1 → 771.1b → 771.2 → 771.3 → 771.10 → 771.4/771.5 →
771.6 → 771.7 → 771.8 → 771.9 → 771.11/771.12 → 771.13 → 771.14. 771.4 (deploy) and
771.5 (playback) parallelize once 771.3's tick-log contract is frozen. **When
implementing, fan out one agent per sub-order via a pipeline after 771.0/771.1b (the
shared contracts) land.**

---

## WO-771.0 — Move `IDamageableStructure` into Core + extend the contract (Prereq) [CODE]
**Priority:** Critical (unblocks module isolation) · **Depends on:** —

**Problem.** `IDamageableStructure` is declared in `namespace DeNelle.Village` inside
`Assets/_Modules/Village/Enemies/Enemy.cs:34/43`. The Raid asmdef must **not** reference
Village (module isolation, per `Pet.cs`), yet troops must deal structure damage and
towers/Heart must implement it. Contradiction → resolve by moving the contract to Core.

**Spec.**
1. Move `IDamageableStructure` to `Assets/_Modules/Core/Combat/IDamageableStructure.cs`
   (`namespace DeNelle.Core.Combat`, beside `IDamageable`). Extend it with what a raid
   sim and towers need: `Vector3 WorldPosition { get; }`, `float Hp { get; }`, `bool
   IsAlive { get; }`, `StructureFaction Faction { get; }`, plus the existing
   `ApplyContactDamage(float)`.
2. Update every Village implementer (`WallSegment`, `Gate`, `Building`, `Enemy`'s usage)
   and consumer to the Core type. Pure namespace move — no behavior change.

**Acceptance.** Solution compiles; Village behavior unchanged (wave enemies still damage
gates/Heart); `DeNelle.Raid` can reference the contract with **zero** Village dependency;
`WORK_ORDER_771_0_*.RESULT.md`.

**Key files:** new `Core/Combat/IDamageableStructure.cs`, `Village/Enemies/Enemy.cs`,
`Village/**` structure implementers.

---

## WO-771.1 — Troop data schema + canonical `troops.json` (Foundation) [CODE — data]
**Priority:** High · **Depends on:** — · **Reuse:** `WaveData.cs`/`PetCatalog.cs` loader.

**Spec.**
1. `_Modules/Raid/DeNelle.Raid.asmdef` (refs `DeNelle.Core`, `DeNelle.BattleATB.Engine`;
   **no** `DeNelle.Village` — enabled by WO-771.0).
2. `_Modules/Raid/Data/TroopData.cs` — `TroopDef, TroopCost, TroopCatalog` (design B1),
   Newtonsoft `[JsonProperty]`, incl. `Level`, `UnlockBarracksLevel`, `ModelKey`,
   `TargetPreference`, `WallDamageMult`.
3. `_Modules/Raid/Data/TroopCatalog.cs` — static loader from
   `StreamingAssets/Data/Canonical/troops.json` (verbatim `PetCatalog.cs`).
4. `troops.json` — the 6 troops (design B1), roles keyed to `enemy-roles.json`,
   `ModelKey` = KayKit mesh ids. Enemy classes/families reference **WO-772**.
5. Optional `TroopDefSO` inspector mirror per `CombatantDefSO.cs`.
   **(Save/`GameState` changes are NOT here — see WO-771.1b.)**

**Acceptance.** `DeNelle.Raid` compiles with no Village ref; `TroopCatalog.Troops`
returns 6 defs (EditMode test); each `Role` valid vs `enemy-roles.json`;
`WORK_ORDER_771_1_*.RESULT.md`.

**Key files:** `_Modules/Raid/DeNelle.Raid.asmdef`, `Data/TroopData.cs`,
`TroopCatalog.cs`, `troops.json`.

---

## WO-771.1b — Consolidated save-schema migration v10→v11 [CODE]
**Priority:** High · **Depends on:** 771.1 · **Owns ALL new GameState fields.**

**Problem.** v1 said bump to `10`, but `SaveSchema.CurrentVersion` is **already 10**
(`SaveSchema.cs:30`; `GameState.cs:30-31`). Three separate WOs each added fields +
migrations — an uncoordinated multi-migration hazard. One WO owns it.

**Spec.** Bump `SaveSchema.CurrentVersion → 11`. Add in one migration step:
`SerializableDict<string,int> TroopRoster`, `int TrophyRating`, `int BarracksLevel`,
`SerializableDict<string,int> TroopLevels`, `long ShieldUntilUnix`, and the
`ObsidianQueueState` (WO-773) — migrating existing `PendingBuilds`/`BuildingCooldowns`
(`GameState.cs:78,80`) into `ObsidianJob`s so no in-flight build is lost. Defaults:
empty/0/1/empty/0/empty-queue. Extend `SaveMigrator` v10→v11 and the round-trip test.

**Acceptance.** A v10 save migrates to v11 with the fields at defaults; a v11 save
round-trips all of them (extend `SaveLoadRoundTripTest`); `SaveMigratorTest` covers
v10→v11; `WORK_ORDER_771_1b_*.RESULT.md`.

**Key files:** `SaveSchema.cs:30`, `GameState.cs`, `SaveMigrator.cs`, `Core/Tests/*`.

---

## WO-771.2 — Base snapshot model + capture service [CODE]
**Priority:** High · **Depends on:** 771.0, 771.1b · **Reuse:** `GameState` fields, `towers/walls/heart.json`, `DungeonLayoutLoader`.

**Spec.** `BaseSnapshot` (design B2) incl. a **`LootableBalance`** field (capped fraction
of the owner's `crystals/food/coins` for WO-771.6/771.12) and the **baked pathing grid**
(WO-771.3 §2). `BaseSnapshotService.Capture(GameState)` reads
`Towers/TowerAbilities/WallLevel/BuildingDamage` (`GameState.cs:72-82`) → **stable-ordered**
placement lists (determinism rule 2); tower world XZ from `towers.json` geometry; Heart
from `heart.json`; gates/walls from `walls.json`. Async `BaseSnapshotLoader` (per
`DungeonLayout.cs:315`). Ship `raid-bases/sample-avalon.json`.

**Acceptance.** Capture on a fresh `GameState` → 9 towers, 1 Heart (maxHp 160), 4 gates,
correct wall level, deterministic list order; `Capture→JSON→Load` lossless;
`sample-avalon.json` loads clean; `WORK_ORDER_771_2_*.RESULT.md`.

**Key files:** `Data/BaseSnapshot.cs`, `BaseSnapshotService.cs`, `raid-bases/sample-avalon.json`.

---

## WO-771.3 — Deterministic raid sim core + breach-aware pathing [CODE — pure]
**Priority:** Critical · **Depends on:** 771.0, 771.1, 771.2 (**hard**); 771.9 **soft** — the
sim consumes only `TroopStatResolver`'s *contract* and runs on the level-1 baseline before
any upgrade data exists, so it is built **before** 771.9 per the critical path. · **BINDING:
determinism discipline above.**

**Spec.**
1. `_Modules/Raid/Sim/RaidSim.cs` — fixed `dt` (1/30 s); **fixed-point** actor state (no
   `float`/`Mathf`/`Vector3` in the hot path, rule 1). Per-hit damage via a `BattleUnit`
   adapter over `Combat.CalculateDamage`; DPS/splash/`wallDamageMult ×`/heal-per-second
   are **new** fixed-point resolution (not ATB turn-based).
2. **Breach-aware pathing (decided).** Static baked polylines cannot model COC wall-
   breaching (Sappers open a gap, troops re-route). Use a **deterministic integer
   flow-field over a baked grid** stored in the snapshot: on wall/gate death the sim
   recomputes the flow-field with integer BFS/Dijkstra (fully deterministic, no NavMesh,
   no `Mathf`). Makes Sappers meaningful **and** keeps cross-arch determinism.
   `NavMesh`/`NavMeshAgent` are used **only** in presentation (WO-771.5), never in authority.
3. `TargetSelector` — nearest / defenses-first / walls-only / heal-allies, fixed-point
   distances, tie-break lowest-index (rule 3). Not a mirror of `Ai.cs:72` (that is
   non-spatial) — new algorithm, same discipline.
4. Effective stats = `TroopDef` baseline × `TroopUpgradeDef` curve at troop level (771.9),
   resolved once at sim start; tick-damage rounding per rule 4.
5. Scoring per design B5 (★ 50%/heart/100%, 180 s clock); `RaidTickLog` per-tick state.

**Acceptance.** `RaidSimGoldenTest`: same `{snapshot, deployLog, seed}` → byte-identical
`RaidResult` across 100 runs **and** across a second build config (proxy for cross-arch);
razing the Heart ≥★2, 100% = ★3, <50% = 0; a Sapper opening a wall visibly re-routes
following troops through the gap (flow-field recompute); no `float`/`Mathf`/`Vector3` in
`Sim/` (grep-enforced); `sample-avalon` sim < 5 ms headless; `WORK_ORDER_771_3_*.RESULT.md`.

**Key files:** `Sim/RaidSim.cs`, `Sim/FlowField.cs`, `Sim/FixedMath.cs`, `RaidResult.cs`,
`TargetSelector.cs`, `Tests/RaidSimGoldenTest.cs`. Reuses `Engine/Combat.cs`, `Engine/Rng.cs`.

---

## WO-771.10 — Defensive tower combat (sim authority + presentation) [CODE]
**Priority:** Critical (largest v1 gap — greenfield) · **Depends on:** 771.0, 771.3 · **BINDING: determinism discipline.**

**Problem.** v1 claimed towers "already implement `IDamageableStructure` and fire" —
false. No tower actor, no projectile/aggro/cooldown code exists; `towers.json`
`range/damage/cooldownSeconds` is unwired. Fully new.

**Spec.**
1. **Sim authority:** in `RaidSim`, defense structures acquire and fire at troops each
   tick using `towers.json` per-tier `range/damage/cooldownSeconds` — fixed-point range
   checks, deterministic target pick (nearest in range, tie-break lowest index), Bulwark
   `defender`-role **taunt/aggro-draw** rule (towers prefer the highest-taunt troop in
   range). Damage applied through the troops' **new `IDamageable` adapter** (WO-771.5 §3).
2. **Presentation:** `RaidTower` visual fires a hitscan/projectile synced to the sim tick
   (visual only — the sim is authority).
3. Zones (ice/fire/aether from `towers.json`) apply the matching `ElementType` via the
   `Combat` element table.

**Acceptance.** Towers damage troops per tier stats; out-of-range troops untouched; a
Bulwark in range draws fire off squishier troops; tower fire is in the golden-vector
(`RaidSimGoldenTest` still deterministic with towers active); `WORK_ORDER_771_10_*.RESULT.md`.

**Key files:** `Sim/RaidSim.cs` (tower pass), `_Modules/Raid/RaidTower.cs`, `towers.json`
(read), `Core/Combat/IDamageableStructure.cs` (WO-771.0).

---

## WO-771.4 — Deploy UI + `RaidDeployLog` capture [CODE — UI]
**Priority:** High · **Depends on:** 771.1, 771.2, 771.9 · **Reuse:** WO-27 corridors, `DungeonHero` tap-to-point, `NavMesh.SamplePosition`.

**Spec.** `RaidDeployLog {Seed, Entries[{TroopId,AtSeconds,Xz}]}`. `RaidDeployController`
(UI Toolkit): troop bar bound to `GameState.TroopRoster`, **filtered to Barracks-unlocked
troops** (771.9), army-camp housing cap (771.8). Tap-to-place: ground raycast →
`NavMesh.SamplePosition` snap (`WaveManager.cs:468`), gated to the deploy ring outside the
walls (WO-27 aprons). **Quantize each placement to the sim's fixed-point grid cell at
capture** — `RaidDeployLog.Xz` stores the grid coordinate, not a raw `float`, so the
`{seed, deployLog, snapshot}` triple is architecture-independent by construction (feeds the
determinism contract of WO-771.3). "Begin Assault" → `SceneRouter.GoRaid(RaidParams)`.

**Acceptance.** Bar shows unlocked+owned counts; placing decrements; cap blocks over-
deploy; illegal taps rejected; `RaidDeployLog` records order/time/position exactly; routes
with params intact; `WORK_ORDER_771_4_*.RESULT.md`.

**Key files:** `RaidDeployController.cs`, `UI/RaidDeploy.uxml/.uss`, `Data/RaidDeployLog.cs`,
`Core/SceneRouter.cs` (add `Raid`/`GoRaid`/`PendingRaid`).

---

## WO-771.5 — Raid scene, `RaidRuntimeState`, troop actors & playback (KayKit) [CODE]
**Priority:** Critical · **Depends on:** 771.3, 771.4, 771.10 · **Reuse:** `Pets/Pet.cs`, `Village/Enemies/Enemy.cs` NavMesh march, `ATBRuntimeState`/`BattleController` bridge.

**Spec.**
1. `State/RaidRuntimeState.cs` — runtime SO mirroring `ATBRuntimeState` (`StartRaid`,
   `OnRaidChanged`/`OnResult`, `Result`, `OnEnable` reset).
2. `RaidController` — `Raid` scene bridge (mirror `BattleController.cs`): reads
   `PendingRaid`, builds the base from the snapshot, runs `RaidSim`, drives presentation,
   returns via `RaidParams.ReturnScene`.
3. `RaidTroop` = `Pets/Pet.cs` **retargeted with its physics acquisition DISABLED**:
   branch off `Physics.OverlapSphere` `NearestHostile` (`Pet.cs:299`) and
   `System.Random(GetInstanceID())` in raid mode — the actor **interpolates to `RaidSim`
   tick positions** (visuals == authority), it does not think for itself. Add an
   **`IDamageable` adapter** (`TakeDamage(float, DamageElement)`) so towers (771.10) can
   damage troops — `Pet.TakeDamage(float)` (`:268`) is the wrong signature; follow the
   `EnemyDamageable.cs:41` adapter precedent.
4. `RaidTower`/`RaidHeart` implement the Core `IDamageableStructure` (WO-771.0); Heart
   uses `heart.json maxHp`.
5. KayKit prefab + shared Animator Controller (WO-771.13) by `TroopDef.ModelKey`; graceful
   placeholder if a mesh is missing (WO-23 rule).

**Acceptance.** Booting `Raid` with `sample-avalon` + a canned deploy log plays hands-free
to a settled result; troop positions **exactly track** the sim; the reused Pet physics
acquisition is provably inactive (no divergence between visual and sim targeting); towers
damage troops; Heart death ends it; missing KayKit mesh degrades to placeholder, not a
crash; `WORK_ORDER_771_5_*.RESULT.md`.

**Key files:** `State/RaidRuntimeState.cs`, `RaidController.cs`, `RaidTroop.cs`,
`RaidTroopDamageable.cs`, `Assets/Scenes/Raid.unity`, `Assets/Editor/RaidSceneBuilder.cs`.

---

## WO-771.6 — Scoring, stars, loot & economy payout (attacker side) [CODE]
**Priority:** High · **Depends on:** 771.3, 771.5 · **Reuse:** `LootStash`, `GameStateService`, `WalletService`.

**Spec.** `RaidScoring` — stars/timer/defeat per design B5; loot = `LootStash` ×
destruction%, capped against the snapshot's `LootableBalance` (771.2). Grant via a **new**
`GameStateService` mutator following the patch pattern at `GameStateService.cs:266` (NOT
`RecordRun`, `:321`). Update `TrophyRating` (field from 771.1b). Post-raid summary UI.
SKR/Stream payout gated behind server validation (771.7) — client never self-grants SKR
(`WalletService.cs`, anti-cheat `:156`).

**Acceptance.** ★ thresholds + defeat/timeout resolve; loot added to `GameState.Resources`
persists across save/load; `TrophyRating` round-trips; no client-side SKR grant without
validation; `WORK_ORDER_771_6_*.RESULT.md`.

**Key files:** `RaidScoring.cs`, `UI/RaidResult.uxml`, `GameStateService.cs` (new mutator).

---

## WO-771.7 — Deterministic replay + async matchmaking + anti-cheat [CODE — service]
**Priority:** Medium-High · **Depends on:** 771.3, 771.4, 771.6 · **Reuse:** `ClanService` stub, `DungeonRuntimeState` seed, `Inbox`, `anti-cheat-spec.md`. · **BINDING: determinism (cross-arch).**

**Spec.** `RaidReplay {Seed, RaidDeployLog, BaseSnapshotRef, ClaimedResult}` +
`RaidReplayService` (PlayerPrefs/JSON, `ClanService` pattern). Re-watch feeds the triple
back through `RaidSim`→`RaidController`. `RaidMatchmaker` (local stub) serves opponent
snapshots by `TrophyRating`, **excluding shielded bases** (771.12); addressing via
`MyInviteCode`/`ClanService.AccountId`; document the server-bridge seam (`ClanService.cs:5-9`).
`ValidateClaim(replay)→bool` by re-simulating; endpoint shape vs `anti-cheat-spec.md` §3.
Defender "you were raided" via `Inbox` (`GameState.cs:134`).

**Acceptance.** Re-running a stored replay reproduces stars/loot **byte-identically**,
including across a second build config; matchmaker returns a valid unshielded opponent per
trophy band; `ValidateClaim` rejects a tampered result; a completed raid drops a defender
inbox entry; module has **no** network SDK dependency; `WORK_ORDER_771_7_*.RESULT.md`.

**Key files:** `RaidReplay.cs`, `RaidReplayService.cs`, `RaidMatchmaker.cs`.

---

## WO-771.8 — Integration, army camp + timed training [CODE + OWNER-GATED]
**Priority:** Medium · **Depends on:** 771.4–771.7, 771.9 · **Reuse:** `SceneRouter`, `DungeonEntrance` entry pattern, `BuildingCooldowns`/`PendingBuilds`.

**Spec.**
1. Register `Raid`/`RaidDeploy` in Build Settings; add `Raid`/`GoRaid`/`PendingRaid`/
   `ReturnScene` to `SceneRouter.cs` (mirror `ATBBattle`/`GoBattle`).
2. Village **"War Table"** interactable (mirror `DungeonEntrance.cs`) → `GoRaid`.
3. **Army camp** = a `buildings.json` entry **with a housing-capacity curve** + a camp def;
   the deploy/roster cap reads it.
4. **Timed training (decided — COC parity):** training a troop spends `Resources` and
   **enqueues a `TrainTroop` job on the common Obsidian queue (WO-773)** — the troop lands
   in `TroopRoster` when the job completes (offline-fair, slot-gated), not instantly. Do
   NOT reinvent a private timer.
5. End-to-end: Village → War Table → target → deploy → watch → result/loot → Village, saved.

**Acceptance.** Full loop eyes-on, 0 compile errors; training debits resources, enqueues a
timer, grants the troop on completion (not instant); housing cap enforced from the camp
curve; all scenes in Build Settings; `WORK_ORDER_771_8_*.RESULT.md`.

**Key files:** `Core/SceneRouter.cs`, `Raid.unity`/`RaidDeploy.unity`, `Village/Buildings/`
(War Table), `buildings.json` (army-camp + capacity), `EditorBuildSettings.asset`.

---

## WO-771.9 — Barracks & troop upgrade progression (reach / strength / special abilities) [CODE]
**Priority:** High · **Depends on:** 771.1, 771.1b · **Feeds (soft):** 771.3/771.4/771.8 consume
`TroopStatResolver`; they run on the level-1 baseline until this lands, so 771.9 is built
**after** 771.3 per the critical path (no cycle). · **Reuse:** tower-tier model, Obsidian queue (WO-773), ATB `AbilityDef`/`StatusKind` types.

**Spec.** `Data/BarracksData.cs` — `BarracksDef` (levels: unlocks + cost + build-time) +
`TroopUpgradeDef` (per-troop stat curve + ability-unlock thresholds). Canonical
`barracks.json` + `troop-upgrades.json`. Three tracks per troop: **REACH** (attackRange +
aggro), **STRENGTH** (Hp + Dps multiplier curve), **SPECIAL ABILITY** (unlocks at a level
threshold, expressed as an `AbilityDef`/`StatusKind` **re-resolved in the fixed-`dt` sim**,
not the ATB turn engine — determinism rule 4). `BarracksService.Upgrade(...)` spends
`Resources` + **enqueues an `Upgrade` job on the common Obsidian queue (WO-773)** (no
private timer).
`TroopStatResolver.Effective(TroopDef, level)` feeds 771.3/771.4/771.8. Upgrade UI. Uses
`BarracksLevel`/`TroopLevels` from 771.1b (no schema work here).

**Acceptance.** Upgrading the Barracks unlocks the gated troop in deploy/training; a level-up
raises effective reach + strength (`TroopStatResolver` unit test) and grants the special
ability at threshold (visible in `RaidSim` behavior); upgrades spend the right resources +
respect the timer; **`RaidSimGoldenTest` stays deterministic with upgraded stats** (rule 4);
`WORK_ORDER_771_9_*.RESULT.md`.

**Key files:** `Data/BarracksData.cs`, `barracks.json`, `troop-upgrades.json`,
`BarracksService.cs`, `TroopStatResolver.cs`, `UI/BarracksPanel.uxml`.

---

## WO-771.11 — Live raid HUD [CODE — UI]
**Priority:** Medium · **Depends on:** 771.5 · **New (v1 only had a post-raid summary).**

**Spec.** UI-Toolkit HUD during the raid: 180 s countdown, star-progress bar (50%/heart/100%
thresholds lighting up), live destruction %, loot ticker, remaining-troop counts by type.
Reads `RaidRuntimeState`/`RaidSim` tick state (passive).

**Acceptance.** All five readouts update live and match the sim; timer expiry ends the raid;
`WORK_ORDER_771_11_*.RESULT.md`.

**Key files:** `UI/RaidHud.uxml/.uss`, `RaidHudController.cs`, `RaidController.cs`.

---

## WO-771.12 — Defender-side economy: loot loss, shields, revenge [CODE]
**Priority:** Medium · **Depends on:** 771.6, 771.7 · **New (v1 = infinite faucet).**

**Problem.** v1 grants attacker loot but never debits the defender — a pure faucet.

**Spec.** On a validated raid, **debit** the raided owner's `Resources` by the looted amount
(bounded by `LootableBalance`), persisted via `GameStateService`. Add a **shield**: after
being raided (or a star-loss threshold) set `ShieldUntilUnix` (field from 771.1b) during
which the base is un-attackable — `RaidMatchmaker` (771.7) excludes it. Add a **revenge**
hook: the defender's inbox entry offers a raid-back on the attacker.

**Acceptance.** A completed raid reduces the defender's stored resources by the looted
amount; a shielded base is excluded from matchmaker results until the shield expires; revenge
routes a raid at the original attacker; `WORK_ORDER_771_12_*.RESULT.md`.

**Key files:** `GameStateService.cs`, `RaidMatchmaker.cs`, `GameState.cs` (`ShieldUntilUnix`
from 771.1b), `RaidReplayService.cs`.

---

## WO-771.13 — Shared troop/enemy/hero Animator Controller + KayKit prefab builder [CONTENT + CODE]
**Priority:** Medium · **Depends on:** — (asset-staging) · **Shared with WO-770.10 + WO-772.**

**Problem.** v1 buried the shared Animator + prefab builder inside 771.5's acceptance. It's a
standalone deliverable — and the same controller unblocks the dungeon hero walk-anim (WO-770.10).

**Spec.** Build one Animator Controller (idle / move / attack / hit / die) against the KayKit
Adventurers/Skeletons rig. Build an **editor prefab builder** that assembles troop/enemy
prefabs from staged KayKit modular parts (armor/weapon/helmet by key — WO-772), resolving by
GUID with a graceful placeholder when a part is absent (WO-23 rule). Wire `TroopDef.ModelKey`
and the WO-772 enemy model keys to it.

**Acceptance.** With KayKit staged, a troop and an enemy prefab build with the shared
controller and animate through all five states; a missing part degrades to placeholder; the
same controller drives the dungeon hero (WO-770.10); `WORK_ORDER_771_13_*.RESULT.md`.

**Key files:** shared `RaidUnit.controller`, `Assets/Editor/KayKitUnitBuilder.cs`,
`troops.json`/WO-772 model keys.

---

## WO-771.14 — Balancing / tuning pass [CONTENT/DESIGN]
**Priority:** Low (last) · **Depends on:** 771.3–771.12 · **Gated on:** `docs/monetization-v2-spec.md`, `docs/wallets-of-record.md`.

**Spec.** Resolve every `[OPEN]` constant: troop stats (B1), loot % + caps, star thresholds,
timer, upgrade curves (B7), SKR-payout cadence, shield/revenge windows. Playtest to tune. All
values live in canonical JSON — no code changes.

**Acceptance.** No `[OPEN]` constants remain; a documented balance table; `WORK_ORDER_771_14_*.RESULT.md`.

**Key files:** `troops.json`, `troop-upgrades.json`, `barracks.json`, `raid.json`, `buildings.json`.

---

## End-to-end COC loop → coverage (every step is owned)

| COC step | WO |
|---|---|
| Train troops (timed) | 771.8 |
| Barracks unlock/upgrade (reach/strength/ability) | 771.9 |
| Pick a target (matchmaking, trophy-banded, shield-aware) | 771.7 |
| Deploy on the perimeter (tap-to-place) | 771.4 |
| Watch automated combat | 771.5 (+ sim 771.3) |
| Defensive towers fire back / aggro | 771.10 |
| Walls breached, troops re-route | 771.3 §2 |
| Live HUD (timer/stars/%/loot) | 771.11 |
| Win/lose, 3-star, timer | 771.3/771.6 |
| Loot grant (attacker) + loot loss (defender) + shield/revenge | 771.6 + 771.12 |
| Deterministic replay + anti-cheat | 771.7 |
| Art (KayKit troops/enemies, shared anim) | 771.13 (+ WO-772 enemy model) |
| Tuning | 771.14 |

## Open items (routed to WO-771.14)
- Troop / War-Table / faction canon names — `narrative-bible.md`, `enemy-codex.md` (+ WO-772).
- Loot %, shields, SKR cadence — `monetization-v2-spec.md`, `wallets-of-record.md`.
- Confirm KayKit Adventurers + Skeletons are in the owner's staged set.
