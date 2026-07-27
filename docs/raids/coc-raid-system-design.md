# COC-Style Raid / Attack System — Design Reference

**Date:** 2026-07-26
**Author:** raid-systems design pass (read-only audit → design).
**Status:** DESIGN. Implementation is sequenced in `WORK_ORDER_29_raid_system.md`.
**Method:** grounded in first-hand reads of the existing code + canonical JSON.
Every architectural claim cites `path:line`. Content/economy constants flagged
`[OPEN: confirm vs <doc>]` are tuning values, not architecture.

**Thesis:** Reuse the deterministic ATB engine as the *combat resolver*, the
Village NavMesh/corridor as the *arena*, `Pet.cs`/`Enemy.cs` as the *troop actor*,
the persisted `GameState` as the *base snapshot*, and `LootStash`/wallet as
*rewards*. A raid is the **attacker-side inversion of the wave loop**: instead of
the Hollow Ones marching on your Heart, *your troops march on someone's base*.

---

## Part A — What already exists (the reuse substrate)

### A1. ATB battle engine = a ready-made deterministic auto-battler
`Assets/_Modules/BattleATB/` is a fully-built, pure-C#, deterministic tick/event
engine (React/TS port), cleanly split into `Engine/` (pure), `State/ATBRuntimeState.cs`
(store), `BattleController.cs` (scene bridge).
- Entry `SceneRouter.GoBattle(BattleParams)` (`Core/SceneRouter.cs:188`) → stashes
  `PendingBattle` → fades into `ATBBattle`. Return honors `BattleParams.ReturnScene`
  (`BattleController.cs:417,432`).
- **`AutoResolveBattle(state, maxTurns)`** (`Engine/Turn.cs:260`) + `AutoHeroAction`
  (`:296`) run a **whole fight to completion, everyone AI-controlled, purely and
  deterministically** — the exact "watch, don't control" primitive a raid needs.
- Unit model `BattleUnit` (`Engine/Types.cs:257`): `Hp/MaxHp/Atk/Speed/Defense/
  Element/Statuses/Cooldowns…`. Damage `Combat.CalculateDamage` (`Engine/Combat.cs:46`):
  `raw = basePower·(1−effDef)·elementMul·markMul·defendMul·critMul`. AI by archetype
  in `Engine/Ai.cs` (`ChooseEnemyAction:21`, `PickEnemyAttackTarget:72` — Tanks hit
  lowest-Defense). Determinism proven by `Tests/RngGoldenVectorTest.cs`; RNG is a
  snapshot cursor (`Types.cs:369`). **We reuse the formula + RNG + determinism, not
  the turn queue.**

### A2. Village wave loop = the closest existing raid shape
`spawn → NavMesh-march → attack structures → breach the Heart` is COC-attack-shaped:
- `WaveManager.cs` (`BeginLoop:204`, `SpawnBatch:422`, breach watch `TickActiveWave:508`,
  `TriggerBreach:575`).
- `Enemy.cs` — per-enemy `NavMeshAgent` marcher (`:59`), sphere-casts for an
  `IDamageableStructure` (`:43`) and deals contact damage (`TickContactAttack:297`).
- **WO-27 baked arena**: four 40 m paved NavMesh march corridors + spawn aprons
  (`WORK_ORDER_27_enemy_spawn_world.RESULT.md`), directly reusable as raid-troop
  pathing/deploy ground. Spawn snapping via `NavMesh.SamplePosition` (`WaveManager.cs:468`).
- Objective `HeartController.cs` (`heart.json maxHp:160`, ring 4.4).

### A3. Troop role taxonomy already exists as canon data
`StreamingAssets/Data/Canonical/enemy-roles.json` defines **25 archetypes with
COC-style roles** — `defender, attacker, dps_ranged, dps_caster, healer, cc, swarm,
trap, boss_tier` — each with `hpScale/atkScale/speedScale/behavior` (e.g. `golem`
defender hp×3.0 spd×0.5 "a walking wall"; `archer` dps_ranged atk×1.3; `wisp` healer).
This is the Giant/Barbarian/Wizard/Healer table, already authored. ATB engine enemy
table (`Engine/Defs.cs:370`) and hero stats (`Defs.cs:212`) supply concrete numbers.

### A4. Pets/heroes are already autonomous deployable units
`Pet.cs` is essentially a raid troop already: `PetMode{Idle,Defend,Fortify}` (`:34`),
and in Defend it **hunts the nearest hostile `IDamageable` and attacks on cooldown**
(`Update:217`, `NearestHostile:297`, `Attack:320`, kinematic `MoveToward:339`), talking
only to `Core.Combat.IDamageable` (module isolation). `PetDeployer.DeployStarterPets`
(`:97`) spawns a set at ring slots — the deploy-placement scaffold. `PetDef`
(`PetCatalog.cs:54`) mirrors the troop schema.

### A5. Pathing
Two systems: `NavMeshAgent` on the baked village NavMesh (real pathing, WO-27
corridors), and kinematic `MoveToward` drift for pets. **Caveat:** live `NavMeshAgent`
stepping is frame-rate dependent, **not deterministic** across machines — so the
authoritative sim uses **baked waypoint polylines**, not live agents (see B4/B6).
**(V2-only.** This determinism concern applies to the V2 authority sim; V1 uses the existing
real-time combat's live pathing and does not need baked-waypoint determinism — see `docs/RAID_NORTHSTAR.md`.)

### A6. No raid/PvP exists yet — but the social/async substrate does
Grep confirms **no raid/attack/PvP/matchmaking** code. But: `Core/Services/ClanService.cs`
is a **local single-player stub of Clans+Chat** with an explicit "network bridge swaps
in later" seam (`:6-9`); `GameState` already persists cross-player addressing
(`MyInviteCode` 6-char, `Contacts`, `Inbox`, `:126-136`); `realm-map.json` is an
async region-run/clear-reward loop (the closest "attack an external location" pattern);
and `docs/anti-cheat-spec.md` mandates **server-authoritative, replayable** reward
validation ("no payout on client claim alone", `:156`).

### A7. The base is already a persisted, defendable snapshot
`GameState` persists `Towers[9]` (tier/slot, `:72`), `TowerAbilities`, `WallLevel` 0–3
(`:76`), and **`BuildingDamage`** (`SerializableDict<string,double>` keyed `gate-0..3`,
`heart`, building ids → 0..100, `:82`). Canon geometry in `towers.json` (3 tiers,
range 14/17/21, dmg 12/22/40), `walls.json` (4 tiers, heartDmgMult 1.0→0.70),
`buildings.json` (5), `heart.json` (maxHp 160). Rewards: `LootStash` (`NestedTypes.cs:113`
— crystals/food/coins/stone/iron/wood/shards/skillPoints), `ResourceBalance`
(`:41`), on-chain `WalletService` (SKR rewards token, owner-gated). **A raidable base
snapshot is ~90% already in the save schema.**

### A8. Canon (from `canon-strings.json`)
Village **Elarion** ("the Lantern of Elarion"); Heart = **the Heart of Elarion** / "the Heart-Grove";
`[STALE: canon-strings.json may still carry the retired name "Avalon" — Elarion is canon per CLAUDE.md §7; flag for a data pass, do not hand-edit here.]`
enemies **the Hollow Ones**, led by **Alduin the Mournful**; apex **Syndrath the
Devourer** (Black Dragon); hero **Blaise**, mentor **Warden Aelwyn**; the **Wardens**
are the protector order; afflictions **the Withering / the Wound**. Troops below are
themed as the **Warden muster** — Elarion's defenders marching out.
`[OPEN: confirm full roster vs docs/narrative-bible.md + docs/enemy-codex.md.]`

---

## Part B — The raid system design

> ⚠ **V1 vs V2 (owner ruling 2026-07-26 — see `docs/RAID_NORTHSTAR.md`).**
> **V1 presentation = real-time combat (`EnemyFactory` / `TargetManager`) on `RaidBaseGenerator`
> bases** — that is the "watch." **The authority / deterministic fixed-point sim (B4b, B6) is V2**
> and must NOT be built for the CoC PvE ship. All "authoritative sim / baked waypoints / flow-field /
> byte-exact replay / server anti-cheat" text in B4b, B6 (and A5's determinism caveat) is **V2-only**
> — it applies when a rewarded/SKR PvP ladder lands, not before. For V1, "replay" = a re-watch from
> the recorded deploy log + the stored result.

### B0. Art direction — KayKit (owner decision, 2026-07-26)
Battle/raid **troops and defenders use KayKit** — specifically the **KayKit
Adventurers + Skeletons** packs — not Tripo/custom meshes:
- **Animation-ready.** KayKit Adventurers/Skeletons ship rigged with idle/walk/run/
  attack/hit/die clips. The Tripo hero FBX are the opposite (`Humanoid` avatar,
  `clipAnimations: []`) — that's the very cause of the hero walk-anim blocker
  (SESSION_HANDOFF §4). Building an *animated* auto-battle on clip-less meshes would
  stall on the same wall; KayKit sidesteps it.
- **Modular = free troop tiers.** Swappable armor/helmet/weapon/skin on one rig gives
  Footman→Bulwark→Longbow→Embermage variants at ~1/8 the art cost — the same
  "one prefab, many defs" pattern `TroopDef`/`DungeonDef` use.
- **Consistent + mobile-perf.** Matches the existing KayKit village/dungeon look;
  low-poly + shared atlas batches well for 20–40 on-screen units.
- **Reserve Tripo/custom heroes for hero units only.**
- **One shared Animator Controller** (idle/move/attack/hit/die) drives every troop —
  and is the same asset that finally unblocks the *hero* walk animation.

Pipeline caveats (see WO-29.x): `/Assets/Models/` is gitignored, so KayKit packs
travel by zip (`export/import-assets.ps1`) and troop prefabs resolve by GUID with a
builder that reconstructs from whatever's staged (same fresh-clone rule as WO-23).
**Confirm KayKit Adventurers/Skeletons are in the owner's staged set** (SESSION_HANDOFF
says Adventurers is present; Dungeon Remastered is currently absent).

### B1. Troop types & stats — the Warden muster (starting roster of 6)
Mapped onto `enemy-roles.json` roles; every stat field already exists on
`BattleUnit`/`PetDef`. `[OPEN: canon troop names vs narrative-bible.md — "Bulwark/
Sapper" are placeholders.]`

| Troop | Role | HP | DPS | Move | Range | Target preference | Housing | Train cost |
|---|---|---|---|---|---|---|---|---|
| **Footman** (levy) | attacker | 120 | 18 | 2.6 | 1.2 | Nearest structure | 1 | 40 coins |
| **Bulwark** (shieldbearer) | defender | 420 | 12 | 1.6 | 1.2 | Defenses-first (soaks tower fire) | 5 | 120 coins |
| **Sapper** (wall-breaker) | trap/breacher | 45 | 200 vs walls | 3.2 | 0.8 | Walls/Gates only, ×6 vs walls | 2 | 25 crystals |
| **Longbow** (ranger) | dps_ranged | 70 | 22 | 2.4 | 6.0 | Nearest (any), holds range | 1 | 50 coins |
| **Embermage** (mage) | dps_caster | 60 | 30 (Flame splash) | 2.2 | 5.0 | Nearest cluster/structure | 3 | 40 crystals |
| **Lifewisp** (healer) | healer | 90 | 0 (heal 18/s AoE) | 2.4 | 4.0 | Follows allies, heals lowest-HP | 2 | 60 crystals |

Direct 1:1 with Giant/Barbarian/Wall-Breaker/Archer/Wizard/Healer. Element/status
hooks reuse `ElementType`/`StatusKind`. **Stats are level-1 baselines** — the Barracks
upgrade system (B7) scales HP/DPS/range and unlocks abilities per level.

Schema — new `TroopDef` + canonical `troops.json` mirroring `WaveData.EnemyDef`/
`PetDef` (plain `[Serializable]` + Newtonsoft loader per `WaveDataLoader`/`PetCatalog`):
`Id, Name, Role, Element, Hp, Dps, MoveSpeed, AttackRange, AttackInterval,
TargetPreference("nearest"|"defenses"|"walls"|"heal-allies"), WallDamageMult,
HousingCost, TrainCost{coins,crystals,food}, ModelKey(KayKit mesh), Level, unlock gate`.

### B2. Base / defender snapshot
**Reuse the Village as the base.** `BaseSnapshot` = `{Version, OwnerCode, Name,
WallLevel, Towers[{slot,tier,ability,zone,worldXZ}], Heart{maxHp:160,worldXZ},
Buildings[{id,worldXZ,hp}], Gates[gate-0..3], TrophyRating, SnapshotAtUnix}`, captured
from `GameState` (`Towers/TowerAbilities/WallLevel/BuildingDamage`, `:72-82`) + canon
geometry (`towers.json` angles, `walls.json halfSize`, `heart.json`). Snapshotted on
logout/base-edit, served to attackers for **async** raiding (owner offline).
Defensive towers implement `IDamageableStructure` (already `Enemy.cs:43`) *and* fire
at troops using `towers.json range/damage/cooldown`. Heart = the ★3 objective.

### B3. Deployment (strategic, pre-combat)
New `RaidDeploy` scene via `SceneRouter.GoRaid(RaidParams)` (extend the `GoBattle`
pattern). Flow: **target select** (matchmaker returns a `BaseSnapshot`) → **deploy bar**
(trained-troop counts from `GameState.TroopRoster`, army-camp housing cap) →
**tap-to-place on the perimeter** (ground raycast → `NavMesh.SamplePosition` snap,
gated to the WO-27 deploy ring outside the walls). Underlying model: `RaidDeployLog`
= ordered `[{troopId, atSeconds, worldXZ}]` + seed — this is the replay seed and the
anti-cheat payload.

### B4. Automated combat (watch, don't control) — hybrid resolver
- **(a) Presentation (the 3D "watch"):** each troop is a `RaidTroop` MonoBehaviour =
  **`Pet.cs` retargeted** (nearest *structure* per `targetPreference` instead of
  nearest hostile), marching the NavMesh arena, dealing DPS via
  `IDamageableStructure.ApplyContactDamage` (`Enemy.cs:49`); towers fire back. Bulwark
  taunts (reuse Tank-targets-lowest-Defense, `Ai.cs:79`); Lifewisp heals via the
  `Pet.Heal` path. **Watch-only** after deploy — like an armed wave.
- **(b) Authority (deterministic tick sim):** a pure, seeded, fixed-`dt` (1/30 s)
  `RaidSim` in the `Engine/` style, moving troops along **precomputed NavMesh waypoint
  polylines** (baked into the snapshot → machine-independent), resolving DPS/tower-fire
  with the **existing `Combat.CalculateDamage` + `Rng`**. The 3D layer plays back the
  same tick stream, so **what you watch == what scores.** AI reuses `Ai.cs` logic;
  "run to completion deterministically" is what `AutoResolveBattle` (`Turn.cs:260`)
  already proves.
- **Scene bridge:** `RaidController` (mirror `BattleController.cs`) owns the scene,
  reads `SceneRouter.PendingRaid`, drives `RaidRuntimeState` (mirror `ATBRuntimeState`),
  returns via `RaidParams.ReturnScene`.

### B5. Scoring & rewards
**Stars (COC 3-star):** ★1 destruction ≥50%; ★2 **Heart destroyed** (Town-Hall
analog); ★3 100%. 180 s clock; defeat on timeout or all-troops-dead pre-★1.
**Loot:** a `LootStash` scaled by destruction%, capped against the defender's lootable
balance, granted via `GameStateService` mutators (the `RecordRun` pattern, `:265,321`).
Star/best-raid feed a new **`TrophyRating`** for matchmaking. SKR/Stream payouts attach
at ★-thresholds **only after server validation** (B6). No new currency —
reuse `LootStash`/`WalletService`. `[OPEN: loot %/shields/SKR cadence vs
monetization-v2-spec.md + wallets-of-record.md.]`

### B6. Replay / watch-automated (deterministic)
Fully reconstructible from **`{Seed, RaidDeployLog, BaseSnapshot}`** — the same seed
pattern already shipped in `DungeonRuntimeState._dungeonRunSeed` (`:54`) and the ATB
`BattleState.Rng` cursor. **Record:** deploy phase writes the log + seed; snapshot is
immutable for the raid. **Re-watch / validate:** feed the triple to `RaidSim`; baked
waypoints + deterministic `Combat`/`Rng` → byte-identical stars/loot on any machine.
**Anti-cheat:** server re-simulates the triple and grants rewards only on match —
satisfies `anti-cheat-spec.md` (`:33,:156`). Defender "your base was raided" uses the
existing `Inbox` (no video, just data — async by construction).

### B7. Barracks & troop upgrade progression (owner requirement, 2026-07-26)
The **Barracks is an upgradeable building** — the progression backbone that unlocks
troops and scales their power, mirroring the game's existing "building + level →
effects" model (`Towers` tiers, `WallLevel`, `buildings.json`).

- **Data-driven.** `BarracksDef` (levels, per-level unlocks + costs + build time) +
  `TroopUpgradeDef` (per-troop stat curve + ability-unlock thresholds), both
  `[Serializable]` records + a canonical `barracks.json` / `troop-upgrades.json`
  (mirror `towers.json` tiering + `TroopDef`).
- **Three upgrade tracks per troop** (the owner's list): **REACH** (attackRange +
  aggro radius), **STRENGTH** (Hp + Dps via a level multiplier curve), and **SPECIAL
  ABILITY** (unlocks at a level threshold — e.g. Lifewisp AoE-heal, Sapper splash,
  Bulwark rage/taunt-pulse, reusing `AbilityDef`/`StatusKind` from the ATB engine).
- **Unlock gate.** Each `TroopDef` carries an `unlockBarracksLevel`; the deploy roster
  (B3) and army-camp training (WO-29.8) only surface troops the current Barracks level
  has unlocked. Upgrading the Barracks is what makes raids progress.
- **Economy-gated, COC-timer.** Upgrade cost draws from `ResourceBalance`
  (`GameStateService` mutators) with a build-time cooldown reusing the existing
  `BuildingCooldowns`/`PendingBuilds` machinery (`GameState.cs`). Persisted as a new
  `BarracksLevel` + `TroopLevels` (`SerializableDict<string,int>`) in `GameState`
  (save-schema migration, like `TroopRoster`).
- **Effective stats = `TroopDef` baseline × `TroopUpgradeDef` curve at the troop's
  level**, resolved once at deploy and fed into `RaidSim` — so upgrades are pure data,
  never touching sim code.

This becomes **WO-29.9** (depends on the troop schema WO-29.1; gates deploy/training).

---

## Reuse summary
- **Thin adapters over existing systems:** base snapshot = persisted `GameState`;
  sim math = ATB `Combat`/`Rng`; troop actor = `Pet.cs`; arena = WO-27 NavMesh;
  rewards = `LootStash`/`GameStateService`/`WalletService`; async = `ClanService`/
  `Inbox`/seed pattern; upgrades = tower-tier/`buildings.json` model.
- **Genuinely new:** the deterministic `RaidSim` waypoint mover, tap-to-place deploy,
  the raid playback bridge, and the Barracks upgrade tracks.
- **Determinism is the linchpin:** baked waypoints + the ATB engine's proven-
  deterministic `Combat`+`Rng` let one `{seed, deployLog, snapshot}` triple power
  watch, re-watch, and server anti-cheat validation alike.
