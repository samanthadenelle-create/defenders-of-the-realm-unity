# Master Catalog — village-enemies-world

Reference catalog for `Assets/_Modules/Village/Enemies` + `Assets/_Modules/Village/World` (incl. `World/Camps`),
plus the Core seams these depend on (`DeNelle.Core.World`: ZoneManager / WardContent / WorldContent /
RegionSpawnTable). Verified by reading source on branch `feat/tower-core-loop` (2026-06-12).

**Assemblies:** all `Enemies/*` and `World/*` runtime code is `DeNelle.Village` assembly, namespace
`DeNelle.Village` (camps nest in `DeNelle.Village.World.Camps`; world systems in `DeNelle.Village.World`).
The Core data/classifier files are `DeNelle.Core` assembly, namespace `DeNelle.Core.World`.
**Cross-assembly law:** Village → Core only. Reflection bridge used for Village→Cosmetics (Glimmer).

---

## ENEMIES — core combat actors

### Enemy.cs `DeNelle.Village.Enemy`
One wave/roamer/garrison enemy: NavMeshAgent march toward Heart, HP, on-contact attack, death.
- `[DisallowMultipleComponent][RequireComponent(NavMeshAgent)][RequireComponent(EnemyDamageable)]`.
- **IS a NavMeshAgent driver** (the class header explicitly documents this — no stale "pure transform" mismatch here). `_agent.updateRotation=false`; root facing slerped toward agent velocity (WO-315).
- Key public: `Configure(string enemyId, EnemyDef def, Transform heart)`; `ApplyWaveScaling(hpMult,speedMult,damageMult)`; `SetBrainTarget(Transform)` / `SetBrainTargetPosition(Vector3?)` (DEF-21/72 nav override); `Heal(float)`; `TakeDamage(float)` / `TakeDamageFrom(float, Vector3)`; `RangedAttack(Transform,float)` (WO-145 hit-scan); `Kill()`; `SetNextDamageTint(Color)` / `SetNextImpactElement(DamageElement)`.
- Props: `EnemyId`, `EnemyDefId`, `Hp`, `MaxHp`, `HpFraction`, `IsDead`, `IsAlive`, `Ai`, `IsFlying`, `CombatLayer`, `EngineDefId` (maps to ATB engine keys).
- Events: `Died(Enemy)`, `ReachedHeart(Enemy)`, `Damaged(Vector3)` (retaliation seam → EnemyBrain).
- Nav priority order in `DriveNav`: brain Vector3 override → hero-aggro (DEF-224, self-contained, brain-independent, hysteresis) → static brain target / tether → Heart. WO-397: hero-aggro moved ABOVE static tether (fixes "brute idle at melee range").
- DEF-56 path throttle via `NavPathCoordinator`; auto-attaches `EnemyHitReaction` + `FloatingHealthBar` (hideAtFull) in Awake; OnEnable/OnDisable register w/ `TargetManager`.
- WO-163 animator-param caching (avoids per-frame "param does not exist" spam); DEF-48 telegraph→attack coroutine; Glimmer reward via reflection bridge to `DeNelle.Cosmetics.GlimmerCurrencyService`.
- **WIRED/LIVE** — central to every spawn path.

### EnemyBrain.cs `DeNelle.Village.EnemyBrain`
Role-based AI overlay (DEF-21) + tactical state machine (DEF-72) layered on Enemy. `[RequireComponent(Enemy)]`.
- Public: `TriggerAttack()`, `SetTacticalState(EnemyTacticalState)`, `SetCoordinatedFlankAngle(float)`, `TauntTo(Transform,float)` (Tier-2 Knight taunt). Props: `TacticalState`, `SuppressDelay`, `IsDead`, `CurrentHealth`, `WantsCoordinatedFlank`, `FlankAngleOffset`, `CurrentTarget`. Event `Died(Enemy)`.
- Roles (`EnemyRole`): Tank (charge hero/structure), Healer (heal wounded ally), DPS/Ranged/MiniBoss (weighted scorer `ScoreAndPickTarget`, falls to legacy chain w/o TacticalData). Tactical states: Rush/Flank/Retreat/Suppressed/Kite/Reposition.
- Override precedence in `Update`: Knight taunt → retaliation provoke (6s, re-armed on hit) → BehaviorTree (if `EnemyBehaviorTree.IsInitialized`) → perception tick → tactical state → role target → tactical destination. **Single owner of enemy targeting** (no second authority).
- Deps: Enemy, AwarenessSensor (auto-added WO-147), EnemyBehaviorTree (optional), Tower/HeroHealth (target scoring), EnemyData SO (WO-86 overlay), TacticalData SO (DEF-72).
- **WIRED/LIVE** — but NOT auto-added to plain wave/roamer enemies (EnemyFactory builds brain-less bodies; spawners add EnemyBrain only for roles: RegionMobSpawner packs, outpost boss, family test, garrison boss).

### EnemyFactory.cs `DeNelle.Village.EnemyFactory` (static)
THE single skinned-enemy builder (CLAUDE.md §9). `Build(EnemyDef, pos, rot, parent, modelOverride=null) → Enemy`.
- Unit-scale root + offset trigger capsule; NavMesh-snaps spawn (6m) before adding agent (DEF-268); skins via `VisualFactory.Skin("Enemies/"+model)`; tinted-capsule fallback if model missing.
- `ModelForEnemy(EnemyDef) → string` maps ids→Resources/Enemies models (hollow-* → Skeleton_*, orc-* → Orc_*, troll → Troll, necromancer → Necromancer; height fallback). agentTypeID 0 (shared hero navmesh/links). Auto-adds EnemyDamageable, ActorAnimator, OrientationGuard.
- WO-315 rig-forward: OrcWarband family gets -90° yaw on visual child (RigFor authority).
- **WIRED/LIVE** — every spawner routes through here.

### EnemyAnimatorFactory.cs `DeNelle.Village.EnemyAnimatorFactory` (static) + enum `EnemyRig`
Stamps the right shared animator controller on an enemy mesh. `RigFor(modelName)→EnemyRig`, `Apply(GameObject visual, modelName)`.
- Rigs: HumanoidMedium→`HumanoidEnemy`, HumanoidLarge→`LargeEnemy` (Skeleton_Golem), Boss→`Boss` (Necromancer), Dragon→`Dragon`, OrcWarband→`OrcWarband` (orc-* Tripo). Loads `Resources/Enemies/<controller>`; root motion off; no-op-safe if controller absent.
- **WIRED/LIVE.** Controllers built by editor `EnemyAnimatorSetup` (outputs to Resources/Enemies).

### EnemyDamageable.cs `DeNelle.Village.EnemyDamageable`
Bridges Enemy → `DeNelle.Core.Combat.IDamageable` (the cross-module seam so DeNelle.Pets/abilities can hit enemies without referencing Village). Also `ICombatLayered` (air/ground gate). Auto-required by Enemy. **WIRED/LIVE.**

### DragonBoss.cs `DeNelle.Village.DragonBoss`
Apex flying-boss (Black Dragon fbx, Generic rig, baked takes Fly/Idel/Run/Walk). Kinematic flight — NOT a NavMeshAgent, NOT in WaveManager `_liveEnemies` (tracked separately as `_liveApexBoss`). `Configure(bossId, heartAnchor, hp)`, event `Died`. Spawned by WaveManager on apex waves (prefab or `Resources/Enemies/Boss_Dragon` fallback). **WIRED/LIVE.**

### TargetManager.cs `DeNelle.Village.TargetManager` (static registry)
Persistent registry of live Enemy (replaces overflow-prone Physics.OverlapSphere reticle sweep, owner 2026-06-02). `Register`/`Unregister` (Enemy OnEnable/OnDisable). **WIRED/LIVE** — reticle/towers/auto-combat query it.

### EnemyBehaviorTree.cs `DeNelle.Village.EnemyBehaviorTree`
DEF-43 BT wrapper attached alongside EnemyBrain; when present + `IsInitialized`, `Evaluate()` replaces EnemyBrain's per-frame targeting. Leaf hooks call back into EnemyBrain (StopAndEngage→TriggerAttack). **Present/optional** — only active if added to a prefab; brain yields to it.

### EnemyTacticalState.cs — enum `DeNelle.Village.EnemyTacticalState` (DEF-72). Orthogonal to Core.Combat EnemyState. **DATA/LIVE.**

### NavPathCoordinator.cs `DeNelle.Village.NavPathCoordinator` (DEF-56, static)
Staggered NavMesh path-request scheduler. `RequestInitialPath(agent, dest)` — staggers a 20-enemy spawn's SetDestination spike across frames. **WIRED/LIVE** (called from Enemy.Configure).

### EnemyFamilyTestSpawner.cs `DeNelle.Village.EnemyFamilyTestSpawner`
**DEV TOOL.** Self-bootstrapping DDOL singleton (`[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]`). In scene `Village2`, **'J' key** spawns a test pack (3 Grunt DPS + 1 Tank + 1 Healer) via EnemyFactory + EnemyBrain roles to watch role AI. Code-built EnemyDefs. **LIVE but dev-only** (hotkey-gated, harmless otherwise).

### Other Enemies (juice / support — adjacent, brief):
- **EnemyHitReaction** — blink-red hit flash (auto-added by Enemy). **LIVE.**
- **EnemyCombatAudio** (WO-220, static) — fallback hit/death SFX via CoreServices.Audio when no type-set clip. `PlayHit()`/`PlayDeath()`. **LIVE.**
- **EnemyAlertTell** — fire-and-forget "!" spot tell. `Flash(transform)` (called by RegionMobSpawner on aggro). **LIVE.**
- **PlayerAttackController** (DEF-47) — player swing w/ perfect-hit window. **Present** (player-side, not enemy).
- **EnemyTypeVfxSet** (SO), **EliteVFXController** (WO-66), **BossHealthBar**, **DamageNumberSpawner**, **Perception/AwarenessSensor** (WO-147, auto-added by EnemyBrain). **LIVE/support.**

---

## WORLD — spawners, streaming, zones

### RegionMobSpawner.cs `DeNelle.Village.RegionMobSpawner`
WO-155 ambient roaming-mob population around the player in OuterWorld. Self-bootstrapping DDOL singleton (`[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]`).
- Region-appropriate (`RegionSpawnTable` + `ZoneManager.GetZone/Depth`), threat-scaled (`ZoneManager.ThreatLevel`), red-skull telled (`ThreatSkullPlate`). Per-mob roam anchor keeps them OFF the Heart-march. Leash/aggro/cull/wander maintenance. WO-316 spawns small FAMILY PACKS (Tank/Ranged/Healer roles via EnemyBrain). Effective population RAMPS with `GameState.BestWave` (WO-216 onboarding). Early-ease HP/dmg x0.35→1.0 by BestWave 6.
- Reuses EnemyFactory; synthesises code EnemyDefs (`BuildRoamerDef`) for not-yet-JSON roster (orc-raider/caveman/feral-wolf/tiefling-cultist). **WIRED/LIVE.**
- ⚠ Dead private helper: `ModelForRoamer(string)` is unused (EnemyFactory.ModelForEnemy owns model mapping now) — see FLAGS.

### WaveManager.cs `DeNelle.Village.WaveManager` — (in `Village/Waves/`, included as the wave-loop owner)
Drives the Elarion village wave loop. `[DisallowMultipleComponent]`. 1557 lines (read 1–1144; remainder is wave-clear/reward/breach handlers, same patterns).
- Public: `BeginLoop()` (UniTask), `ForceBeginNextWave()` (HUD DEFEND button kickoff), `GetEnemyCatalogAsync()`, `SpawnEnemyForExternalMode(def,pos,heart,id)` (PatriciaLight reuse), `EnemyPrefab`. Props: `Phase`(`WavePhase` enum Idle/Countdown/Active/Breached/Complete/Defeated), `CurrentWaveId`, `CountdownRemaining`, `LiveEnemies`, `LiveApexBoss`, `Heart`.
- UnityEvents: `OnCountdownTick(float)`, `OnWaveStarted/OnWaveCleared/OnBreach(int)`, `OnApexBossSpawned(DragonBoss)`, `OnDefeat()`.
- **DEFEND-GATED START:** `_autoStart` default OFF — loop sits Idle at load; HUD DEFEND button kicks it. Loads `waves.json`/`enemies.json` via `WaveDataLoader`.
- Three spawn paths (priority): WO-362 `_smartComposition` (SmartEnemySpawner, generated roster, rotating gate) → WO-316 `_composeFamilyGroups` (EnemyGroupSpawner role-mix squads) → legacy flat `SpawnBatch` (waves.json batches). All subscribe Died/ReachedHeart, apply WaveScalingCurve, route through EnemyFactory.
- Breach detection: inner ring (`_innerRingRadius` 9u) around Heart → `SceneRouter.GoBattle(BattleParams)` → ATB. Stuck-enemy failsafe (12s no progress → Kill). Apex boss tracked separately (kinematic). Heart-death → `OnDefeat`. Wave-clear resource reward (WO-330/361 Wood/Iron/Food every Nth wave, scaled).
- Deps: HeartController, WaveSpawnPoint, EnemyFactory, EnemyGroupSpawner/SmartEnemySpawner, WaveScalingCurve, GameStateService (Difficulty/Onboarded/BestWave), SceneRouter, VfxPool. **WIRED/LIVE.**
- Note: `BuildPlaceholderEnemy` (primitive capsule) is legacy fallback — live path always uses EnemyFactory (skinned).

### TribeManager.cs `DeNelle.Village.TribeManager`
WO-160 wandering tribes: radius-triggered spawn + state-saving. Self-bootstrapping DDOL singleton. A tribe is a `TribeState` record (GameState.Tribes) that materialises to live Enemy within `ActivationRadius`, despawns past `DeactivationRadius` (hysteresis), writes members-remaining/cleared back. WO-159 link: active members target nearest Settlement (IDamageableStructure) → raze undefended claims. Raid size randomised in region threat band, early-ramp via BestWave. Reuses Enemy/Configure + ZoneManager. **WIRED/LIVE.**

### ZoneManager.cs `DeNelle.Core.World.ZoneManager` (static, Core)
THE shared region classifier (WO-142/107/164). Pure logic, headless. Village at origin (~±42 X / ±33 Z); 4 cardinal regions: East+X Goldfields(t1) / West-X Stoneback(t2) / South-Z Mirewood(t3) / North+Z Ashwood(t4).
- `GetZone(Vector3)→RegionId`, `ZoneAt`, `DangerTierAt`, `Depth(Vector3)→0..1`, `ThreatLevel(Vector3)→int` (5×tier + depthBand), `DefaultZoneGraph()→IReadOnlyList<ZoneState>`, `DefaultDestination(RegionId)→NodeType`. **WIRED/LIVE** — single difficulty/region read for all world systems.

### WorldSceneLoader.cs `DeNelle.Village.WorldSceneLoader` (static)
Additively loads `OuterWorld` over a hub scene. `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]` ResetGuard + `(AfterSceneLoad)` Init subscribing to `sceneLoaded`.
- Hub detection via `DeNelle.Core.HubScenes.IsHub` (shared source w/ HUD, WO-411). Const `VillageSceneName="Village2"`.
- Carries `DiagTerrain` — heavy DEF-108 diagnostic that ALSO runtime-repaints an empty terrain splatmap (per-quadrant biomes). **WIRED/LIVE** — see FLAGS (diagnostic spam + side-effecting repaint left in).

### SceneTransitionTrigger.cs `DeNelle.Village.SceneTransitionTrigger`
Proximity gate/portal seam hub↔OuterWorld. **PROXIMITY-based** (`ProximityRadius` distance check, NOT OnTriggerEnter — the NavMeshAgent hero stops at the castle navmesh edge and can't trip a trigger across the two-scene seam; OnTriggerEnter kept as fallback). On cross: ensures target scene loaded additive, `WarpTo`s hero (WO-383 teleport-aware via `HeroLocomotion.WarpTo` — NOT hard transform.position, to avoid fighting the agent + camera break). Wired by CastleHubBuilder. **WIRED/LIVE.**
- Note: `HeroLocomotion` IS a NavMeshAgent-backed mover (relevant to the comment-vs-code class flagged in the task) — this file correctly treats it as such (uses WarpTo, comments the seam reason).

### DungeonWorldPortalSpawner.cs `DeNelle.Village.World.DungeonWorldPortalSpawner`
WO-165/DEF-188 hidden discoverable dungeon portals in OuterWorld. Self-bootstrapping DDOL singleton. Region-gated NavMesh-valid placement (one per available dungeon, round-robin by danger), reuses existing `DungeonPortal` (glow + [F] prompt + `SceneManager.LoadScene("Dungeon_"+id)`). Fog-of-war reveal (dim→fade-in on approach, PlayerPrefs-persisted). DungeonDef from Resources else inline fallback (HealersCottage/FolksGranary only). `MaxPlaceAttempts=24` cap (CalculateTriangulation is heavy → uncapped retry hung OuterWorld load). **WIRED/LIVE.**

### Ward system (WO-112):
- **WardStone.cs** `DeNelle.Village.WardStone` — one in-field ward-stone: lit/unlit state, glow (child point-light + emissive), relight affordance. Built at runtime by WardTetherService (code-built, no prefab). `Initialise(WardStoneDef, WardStoneState)`, `SetLit(bool,silent)`, props `IsLit/Region/Id/Cost`. **WIRED/LIVE.**
- **WardTetherService.cs** `DeNelle.Village.WardTetherService` — reach authority + "forgetting" driver; seeds WardStoneDefs, owns reach math, node-claim hook. **WIRED/LIVE.**
- **WardContent.cs** `DeNelle.Core.World` (DATA) — `WardStoneDef` (authoring), `WardStoneState` (persisted, GameState.Wards), `WardReach` (static headless reach math: `BaseReach=12`, `ReachForRegion`, `DistancePastReach`). JsonUtility-safe; **NOT yet wired into SaveSchema/SaveMigrator** (in-memory per-session, save-owner follow-up).

### WorldContent.cs `DeNelle.Core.World` (DATA)
Pure records for the territory loop: `TribeDef`/`TribeState` (WO-160 wandering tribes), `SettlementState`+`SettlementPhase` enum (WO-159 node settlements), `WorldPoint` struct (JsonUtility-safe x/y/z). Ride in GameState.Tribes/.Settlements. **NOT yet in SaveSchema/SaveMigrator** (in-memory, save-owner follow-up). **DATA/LIVE.**

### Other World support (brief, adjacent):
- **SettlementPlacer** (WO-159) — claim verb for node settlements. **LIVE.**
- **Settlement / SettlementPlacer / OutpostHub / OutpostDefender** — claimed-outpost defense grid + recruitable Tank/DPS/Healer troops via Economy (Priority 2/3 pet-farming/expansion). **Present.**
- **HarvestSite / MineNode / MineNodeVisual / CrystalMineNode / RareCrystalSpawner / NodeDiscoverySystem** (DEF-189 fog-of-war) — resource/reward half of explore loop. **LIVE.**
- **GateIntelHud** (DEF-152) — "North → Ashwood · Threat 20" gate intel label. **LIVE.**
- **WorldMusicDirector, ResourceGainPopup, PetHarvestBootstrap, TribeManager** — ambient/economy. **LIVE.**
- **Nav/geometry installers:** StairNavLink, RampartNavLinkInstaller, RampartLiftInstaller, WallNavObstacleInstaller, LiftPlatform, StairwayStructure/Builder, MoatWaterShimmer, SeatOnGroundOnStart. **LIVE** (navmesh/link/geometry helpers).

---

## WORLD/CAMPS — clear→claim→build + raid loop

### CampSystem.cs `DeNelle.Village.World.Camps.CampSystem` (static feature-flag + bootstrap)
Self-bootstrapping (`[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]`) spawner for the clear→claim→build-outpost loop. **SHIPS DARK** via `DefaultEnabled` (`#if DOTR_CAMPS`) — but `_enabled = true` is HARDCODED ON (owner enabled 2026-06-03 for playtest). Spawns 4 `ClaimableCamp` at cardinal anchors (±95) in OuterWorld only. `Enabled`/`Enable()`/`Disable()`/`SpawnNow()`, `Camps` list. Consts `DefaultKillsRequired=6`, `DefaultCampRadius=9`. **WIRED/LIVE (flag forced ON).** See FLAGS.

### RaidOutpostSystem.cs `DeNelle.Village.World.Camps.RaidOutpostSystem` (static feature-flag + bootstrap)
Spawns walk-to ENEMY OUTPOSTS (the raid bite). Self-bootstrapping; **`_enabled = true` HARDCODED ON** (DefaultEnabled `#if DOTR_RAID`). **FOUR cardinal outposts** (E/W/N/S at ±70) — comment header still says "ONE outpost"/single Goldfields anchor (STALE — code builds 4, see FLAGS). `CardinalOutpostCount=4`, `OutpostAnchorPositions()`, `Outposts[]`, `Outpost` (east). `SpawnDelaySeconds=10` (was 180s → owner never found it, cut 2026-06-11). Delayed realize off the city-emerge load frame. **WIRED/LIVE (flag forced ON).**

### EnemyOutpost.cs `DeNelle.Village.World.Camps.EnemyOutpost`
The walk-to outpost itself: WOOD fort + boss-led garrison (1 MiniBoss + 5–8 guards), threat-scaled. `[DisallowMultipleComponent]`. Clear by killing garrison → flat reward (resources/crystals/XP) + threat-scaled loot table (gear/rare-gem/quest-token, routed to existing systems). PlayerPrefs persistence (`dotr-raid-cleared-<id>`).
- `Configure(region,threat)` / `Configure(region,threat,idSuffix)` (distinct per-cardinal key); `ConfigureArena(id,threat,recipe,guardCount)` (WO async-PvP reuse: seeded opponent base, suppresses open-world loot); `Clear()`, `GrantArenaLoot()`. Props: `Region/ThreatLevel/OutpostId/Cleared/AliveCount/TotalGarrison/EverCleared`. Events `OnCleared`, `OnArenaSpawnFailed` (Arena-only non-win).
- Staggered realize coroutine (~2 fort pieces/frame, 1 guard/frame — off the OuterWorld-load spike). Reuses OutpostFoundationGenerator, EnemyFactory, EnemyBrain (boss MiniBoss role), tethered anchors. WO-389 Arena defenders (friendly placed units/structures). WO-360 EchoAutoDeployTrigger. **WIRED/LIVE.**

### GarrisonController.cs `DeNelle.Village.World.Camps.GarrisonController`
Runtime brain on an additively-loaded enemy GARRISON scene (Garrison_TrollOutpost/RuinedKeep/HillFort). `[DisallowMultipleComponent]`. `Activate()` (idempotent, post-navmesh) spawns Troll/Stonebelly garrison at authored spawn points via EnemyFactory (or optional `enemyPrefabs[]` round-robin). Recipe-driven `enemyTypeIds[]` + `[minLevel,maxLevel]` band; `BuildTypedDef` maps many family ids. `CleanupAndUnload()→AsyncOperation` (additive teardown). Props `AliveCount/TotalGarrison/Cleared`, event `OnCleared`. Inspector: `spawnPoints[]/enemyPrefabs[]/threatLevel/enemyTypeIds[]/minLevel/maxLevel/activateOnStart`. **Present** — wired by GarrisonSceneBuilder (additive garrison scenes); not the open-world EnemyOutpost path.

### ClaimableCamp.cs `DeNelle.Village.World.Camps.ClaimableCamp`
One outer-world camp: Hostile→Cleared→Claimed lifecycle (`CampStage` enum). Clear by killing guards within `CampRadius` (CampGuards), then claim (CampPromptUI) → build Watchtower/Lumber/Farm Outpost → defend counterattack (CampDefenseWave). `Configure(region,threat,kills,radius)`. Events `OnCleared/OnClaimed/OnDefended/OnDefenseLost`. PlayerPrefs persistence. Owned by CampSystem. **WIRED/LIVE (via CampSystem flag).**

### Camp support (brief):
- **CampGuards** — hostile guard pack (3+, threat-scaled) standing on a Hostile camp, tethered, EnemyFactory-built; `AllCleared` event. **LIVE.**
- **Outpost** — player-built structure on claimed camp; auto-harvests trickle into wallet; implements `IDamageableStructure`; `OutpostType` enum (Watchtower=Iron/Lumber=Wood/Farm=Stone). **LIVE.**
- **CampDefenseWave** — defend stage (post-build counterattack). **LIVE.**
- **OutpostFoundationGenerator** (static) — WOOD catalog-piece fort via `StructureFactory.Create` (same as village build mode); `GenerateFootprintRecipe`, `Realize`/`RealizeStaggered`, `CellSize`. **LIVE** (used by EnemyOutpost).
- **EchoAutoDeployTrigger** (WO-360) — `Attach(go, radius)`; summons Echo pet on entering outpost combat zone + mini-tutorial. **LIVE.**
- **EchoTutorialUI / CampPromptUI / CampVisual** — code-built UI/visuals (no UXML). **LIVE.**

---

## DATA JSON

### enemies.json — `Assets/StreamingAssets/Data/` (source) + `Assets/Resources/Data/Canonical/` (WINS at load via CanonicalJson; DUAL-COPY — keep in sync)
Schema per def: `id` (wave-type key + ATB ENEMY_DEFS bridge), `hp`, `moveSpeed`, `contactDamage`, `attackInterval`, `ai` (walker/charger/skirmisher), `height`, `family`, `role`, `aggroRadius`, `xpReward`, `glimmerReward`, `movement` (→ IsFlying). Loaded by `WaveDataLoader.LoadEnemiesAsync` → `EnemyCatalog`.
**Authored ids (10):** hollow-walker, hollow-warrior, hollow-rogue, hollow-acolyte, necromancer, orc-berserker, orc-shaman, orc-necromancer, troll (+ a schema-doc first entry). NOTE: many open-world roster ids (orc-raider/caveman/feral-wolf/tiefling-cultist) are NOT in JSON — synthesised as code EnemyDefs by RegionMobSpawner/EnemyOutpost/GarrisonController (forward design, swap to catalog lookup when JSON gains them).

### waves.json — same dual-copy. **4 wave entries.** Schema: wave id, `countdownSeconds`, `enemies[]` (WaveBatch: type/count/spawnPoint/delay/interval), optional `boss`, optional `apexBoss` (ApexBossDef id/hp). Loaded by `WaveDataLoader.LoadWavesAsync` → `WaveSchedule`. Smart composition (WO-362) can override these and generate rosters from wave number.

---

## FLAGS

### Stale comment vs. code
- **RaidOutpostSystem.cs** — file/class header + `Placement` comment say "ONE walk-to outpost" / "single reachable Goldfields-edge anchor (+X)". The code builds **FOUR cardinal outposts** (E/W/N/S, `OutpostAnchors[4]`). The header is STALE; the in-method comments (lines 68–95) are correct. (Same class as the task's flagged pattern — comment understates the code.)
- **RaidOutpostSystem.cs** `SpawnDelaySeconds` comment still references "~3-MINUTE DELAY" / "180s" in the doc block; actual value is `10f` (cut 2026-06-11). Comment partially stale (it documents the history, but the headline "3-min" misleads).
- **WardStone.cs** header references "the Keeper" relight affordance, but the proximity-relight logic lives in WardTetherService (the WardStone is "intentionally thin") — header is accurate on that point. No mismatch.
- **SceneTransitionTrigger / HeroLocomotion** — correctly documented: HeroLocomotion is NavMeshAgent-backed, and this file uses `WarpTo` precisely because of that (no stale "pure transform" claim — this is the *correct* handling of the class the task warned about).

### Scene-gated / disabled / dark-by-default-but-forced-on
- **CampSystem** — `DefaultEnabled = false` (ships dark, `#if DOTR_CAMPS`), but `_enabled = true` HARDCODED (owner playtest 2026-06-03). Live in current builds despite "ships dark" framing.
- **RaidOutpostSystem** — identical: `DefaultEnabled` dark, `_enabled = true` hardcoded ON for testing.
- **GarrisonController** — only active in additive Garrison_* scenes (GarrisonSceneBuilder); not in the open-world EnemyOutpost path. `activateOnStart` default OFF (a raid manager calls Activate()).
- **EnemyFamilyTestSpawner** — DEV ONLY, 'J'-key gated, scene `Village2` only.
- **WardContent / WorldContent records (TribeState/SettlementState/WardStoneState)** — JsonUtility-safe but **NOT wired into SaveSchema/SaveMigrator** → live in-memory per-session only; do NOT survive reload until save-owner adds (de)serialization + schema bump.

### Dead / duplicate / leftover
- **RegionMobSpawner.ModelForRoamer(string)** — private static, **UNUSED**. Model mapping moved to `EnemyFactory.ModelForEnemy`; this stale helper is dead code (its own comment even says "swap to bespoke when packs land", but nothing calls it).
- **WorldSceneLoader.DiagTerrain** — heavy DEF-108 diagnostic still in (verbose per-load logging) AND it side-effects: runtime-repaints an empty terrain splatmap. Its own comment says "Remove once resolved." Left in; not dead but should be gated/removed.
- **WaveManager.BuildPlaceholderEnemy / placeholder-capsule path** — legacy fallback; live spawns always use EnemyFactory (skinned). Kept for "before-prefab" testability but effectively superseded.
- **EnemyDef.AggroRadius** was authored across rosters but historically unapplied (silently fell to 7m inspector default) — fixed WO-397 in Enemy.Configure (now applied). Noted as a resolved data-not-wired class.

### Contradictory / risk
- **Synthesised vs. JSON enemy stats** — open-world roster ids exist ONLY as code EnemyDefs in THREE places (RegionMobSpawner.BuildRoamerDef, EnemyOutpost.BuildGuardDef, GarrisonController.BuildTypedDef) with **divergent stat blocks** for the same id (e.g. orc-raider hp 95 in roamer/outpost vs 170 in garrison). No single source of truth for these until they land in enemies.json — a balance-drift hazard.
- **Two enemy aggro authorities coexist:** Enemy.cs self-contained hero-aggro (DEF-224, brain-less enemies) vs EnemyBrain role/provoke targeting. Documented as intentionally additive (brain wins when present, DriveNav gives brain override priority) — not a bug, but a subtle precedence to respect when editing either.
