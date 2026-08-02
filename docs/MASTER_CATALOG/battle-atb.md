# MASTER CATALOG — battle-atb (COMBAT: live BattleArena real-time + dormant ATB)

> **Rewritten 2026-08-02, verified from code (not comments).** Supersedes the ATB-only
> version. This area now covers BOTH combat systems:
> **(1) the LIVE path — real-time `BattleArena`** (warp-in isolated stage, the route every
> dungeon + overworld encounter actually takes), and
> **(2) the DORMANT ATB module** (`Assets/_Modules/BattleATB/` — pure-C# turn engine,
> retired-but-reversible, still compiled + unit-tested).
>
> **Truth block (grep/code-verified 2026-08-02):**
> - `ff.dungeonrealtime` default **ON** (`FeatureFlags.cs:273`) → dungeon fights route to
>   **BattleArena**, NOT the ATBBattle scene. `ff.overworldencounter` default **ON**
>   (`FeatureFlags.cs:154`, owner reversal 2026-07-30) → overworld reps engage into the same arena.
> - **`ff.atbdungeon` NEVER EXISTED** (doc-lie class; grep = 0 code hits). Every doc/WO line saying
>   "ATB dungeon behind ff.atbdungeon OFF" means `ff.dungeonrealtime` with **inverted sense**
>   (see `CANON_GROUND_TRUTH_2026-07-22.md:169`, `WORK_ORDER_584...md:5-7`, `docs/ARCHITECTURE.md:101`).
> - **WO-584 slices 2–5 NOT shipped** (resolver registry / outpost prefab / ownership flip /
>   KayKit pipeline). WO-584 file status is still "READY TO IMPLEMENT"; only its slice-1 intent
>   (dungeon→arena flag-gate) exists in-tree, delivered as WO-591 via `ff.dungeonrealtime`.
> - `ff.lockon` default **OFF** (`FeatureFlags.cs:334`); `ff.battlehudvm` default **OFF**
>   (`FeatureFlags.cs:572`).

---

## PART 1 — THE LIVE COMBAT PATH (real-time BattleArena)

Scope: `Assets/_Modules/Village/Arena/` (encounter files only — the async-PvP `ArenaMode`
raid family shares the folder but is a separate verified system) + the routing/seam files.
Assembly `DeNelle.Village`, ns `DeNelle.Village.Arena`.

### BattleArena.cs (2,395 lines) — the generic isolated real-time battle controller (WO-482)
**Runtime singleton MonoBehaviour**, self-hosting (`Instance` creates a DontDestroyOnLoad host,
guarded editor-instantiable for the headless ArenaCombatOracle — `BattleArena.cs:204-220`).
Non-creating accessors: `Existing` (:224), `AnyBattleInProgress` (:227).

**Stage geometry:** the arena is staged at `ArenaCentre = (5000, 0, 5000)` — ~7km from the world
origin, while the home scene STAYS LOADED in memory (:81). `IsArenaPosition(pos)` = within 200m of
centre (:85-94) — the distance discriminator shared systems use to tell arena hits from home-scene
bleed-through. Kite floor 45×36 (`ArenaHalfWidth 22.5` / `ArenaHalfDepth 18`, −25% tighten, :98-99).

**Entry — `BeginEncounter(EncounterParams p)` (:323-404), returns bool:**
1. Refuses if a battle is in progress (:325), params null/empty (:326), or BOTH
   `ff.overworldencounter` AND `ff.dungeonrealtime` are OFF (:334) — either flag ON authorizes.
2. Force-exits Build Mode if active (hub-softlock guard, :346-350).
3. **REFUSES to stage with no `Player`-tagged hero** (F8 2026-07-30 phantom-fight fix, :359-365) —
   callers must handle `false` (EncounterTrigger rolls back its combat lock).
4. `RepEngageWatcher.PauseAll()` — freezes EVERY home-scene rep for the fight (:381).
5. `MaybeAddBoss` — 5% roll appends `orc-warlord` (→ Orc_Necromancer model) to the family
   (:149-150, :1353-1369; idempotent, instrumented).
6. Raises **`OnBattleStaged`** (:400, event decl :238) — the dungeon FPV rig hooks this for
   OTS camera framing — then starts `StageRoutine`.

**`StageRoutine` (:409-547):** `BuildArena` → runtime navmesh (`ArenaNavMeshBaker.BakeForCastle(root, 6f)`,
:418-424, + 4-corner on-mesh probe :433-450) → ScreenFader fade-out masking the ~7km warp (:457-459) →
`WarpHero` to south stance Z=−9 facing north (:466-467) → `SmartMobileCamera.SnapBehindTarget()`
(stale-yaw framing fix, :486) → `SpawnFamily` → abort `Resolve(false)` if nothing spawned (:493-498) →
**fight starts UNLOCKED** (deliberate lock-on only, :502-510) → `BattleArenaHud.Create()` + Flee handler +
intro card (:521-531) → `MusicTrack.Arena` (:532) → `EnableCombatBloomCamera` (:536) → fade-in (:543) →
`WatchToResolution` (:546).

**`BuildArena` (:551-639):** `Resources/Arena/ForestClearingArena` prefab (:577) with
`RethemeLandscapeForBiome` for stone biomes behind `ff.combatfeel` (:759-803); fallback lit plane
(:737-745); 4 invisible boundary walls (:605-608); 8-quad + top-cap painted backdrop enclosure with
double-sided materials + camera SolidColor sky override (:810-909, gradient fallback :925); local
bloom Volume priority 100, intensity 4.5 / threshold 1.1 + Neutral tonemapping (:196-199, :1019-1048);
per-biome particles (:625); cavern-only fog/ambient mood with save/restore (:629, :1114-1150);
`AuditArenaRenderers` F8-37 mesh audit — any Cylinder/Capsule mesh = `FlowTrace.Fail` (:655-714).

**`SpawnFamily` (:1228-1316):** clamps 1..7 (:1234); low-level hero cap via
`OverworldEncounterSpawner.LowLevelEnemyCap` (:1241-1246); role-ranked squad formation — Tanks front /
DPS mid / Healers rear (`FormationRankForRole` :1392-1400, spacing consts :138-140); each enemy via the
SHARED `EnemyFactory.Build` + `Enemy.Configure` + `EnemyBrain` with `SetHeroOnlyTarget(true)`
(no ~7km Heart-siege milling, :1287-1301); MonsterFamily wiring — index 0 gets `FamilyLeader`, rest
`FamilyMember` (:1303-1306); `Died += HandleEnemyDied` (:1312). Enemy stats come from
**`BuildEncounterDef` — a HARDCODED in-code stat table** (orc + hollow rows, threat +8%/tier,
:1406-1445; the code itself flags a follow-up ticket to read the canonical enemy catalog :1429-1432).

**`WatchToResolution` (:1571-1706), 0.25s tick.** In order each tick:
- `MaybeDisbandOnArrival` — leader within 4.5m of hero → `_familyEngaged = true`, leader disabled →
  Disband → real 1vN (:1321-1348).
- **Watchdog (C) ABANDONMENT** (patch 6): `_arenaRoot == null` (scene unloaded under the fight) →
  `ResolveAbandoned` immediately (:1610-1614); `Player` hero missing ≥ 1.0s
  (`HeroMissingGraceSeconds` :131) → `ResolveAbandoned` (:1615-1624). Checked ABOVE the win gate so an
  emptied `_liveEnemies` after a scene load can never read as victory.
- **Watchdog (A) hero OUT of arena** ≥ 2.5s (`HeroOutOfArenaGraceSeconds` :108) → force
  `Resolve(false)` to un-freeze home reps (:1627-1639).
- **Watchdog (B) fled pack** — only when `heroInArena && _familyEngaged` (:1648):
  `LeashStagedEnemies` clamps drifters past 16m back to ~15.2m (:1714-1732, **see ARENA-1**), and no
  enemy within 18m for 7s → break off as loss (:1653-1663, consts :121-123).
- `ff.lockon` ON → `MaybeRebindLockCamera` each tick (:1672, :1544-1556).
- **Outcome arbitration — DEATH PREEMPTS VICTORY** (:1674-1699): prune dead; win gate =
  `_liveEnemies.Count == 0 && heroAlive` (null `HeroHealth` = alive, test scenes) → slow-mo
  `PlayDeathCam` on the climax body → `Resolve(true)`; hero down → death-cam (no slow-mo) →
  `Resolve(false)`.
- 240s `BattleTimeoutSeconds` hard stop → loss (:101, :1702).

**`Resolve(bool won)` (:1903-2077):** star rating from duration (`BattleStarRating.StarsForDuration`,
:1910-1912) → victory/defeat music cue with ORACLE FIRED lines (:1926-1930) → `GrantWinReward`
(:1936) → `PlayVictoryBurst` VFX (:1951, :2093-2132) → restore mood/sky/bloom (:1955-1962) →
captures stage+survivors into locals and NULLS fields so a fresh fight can't collide (:1971-1974) →
loss: `DespawnRepImmediate` + `BeginPostLossGrace` ~3.5s no-engage (:1983-1987) + `SafeLossReturnPosition`
(18m retreat past the rep's 14m aggro; **defaults to the engage spot** and only a successful navmesh
sample upgrades it — the F8 seq512 unbaked-dungeon void-warp fix, :1478-1500) →
WIN shows the `ShowResult` summary whose Continue fires the deferred masked return (20s auto-timeout;
no-HUD → immediate return, :2029-2036); LOSS defers the banner to home arrival (`_pendingLossBanner`,
double-death-screen fix, :2039-2049) → `RepEngageWatcher.ResumeAll()` (:2056) +
`QuietNonPursuersOnBattleEnd` on win (:2062) → ambient restore after 2.5s cue (:2069) →
**`OnBattleEnded(params, won)`** (:2075).

**`ReturnHomeWithFade` (:2140-2227):** under black — destroy survivors+stage, `WarpHero` home UNLESS
`_returnWarpCancelled` (`CancelPendingReturnWarp`, :1783-1788 — the dungeon DEFEAT settle cedes the
warp, :2158-2161); on loss **revive the hero IN PLACE** (`HeroHealth.Respawn`, arena owns the death
cycle, :2175-2181); `ff.noautoheal` ON (default) = NO post-fight heal (:2190-2202);
`ReacquireFollowCamera` + `ClearHeroTargetLock` under black (:2207-2208); fade-in; show deferred loss
banner (:2221-2226).

**`ResolveAbandoned(reason)` (:1809-1878):** NO outcome — no reward, no result UI, **no WarpHero**,
**deliberately does NOT raise `OnBattleEnded`** (:1874). Stops coroutines, restores every override,
despawns combatants + stage, `_hud.Close()`, `RepEngageWatcher.ResumeAll()`, camera back, clears fade,
drops BattleInProgress.

**Rewards (`GrantWinReward` :2244-2302):** XP `(20 + 8·family + 4·threat)·starMult` via
HeroProgression reflection (:2257-2263); **Wisdom = 0** (WO-763 — level-up-only minting, :2267-2274);
wood/iron via `EconomyService.Grant` (:2278-2287); gear via `TryGrantArenaGear` — **hard-capped ~4%
per roll (~2%/slot), star bonus clamped away** (GEAR RARE directive, :2308-2357), equips through the
real `GearLoadout.Equip*ById` + `GearCatalog`, biased common/uncommon.

**Headless seam:** `ResolveForTest(p, won, duration)` (:1890-1898) — drives the REAL private
`Resolve` for `DeNelle.Editor` ArenaCombatOracle; never called by gameplay.

**`WarpHero` (:1451-1472):** finds hero by `Player` tag, calls `HeroLocomotion.WarpTo` **via
reflection** (raises OnTeleported for camera snap); raw-transform fallback logged through
`DeathTrace.HeroMoved`.

### EncounterParams.cs — the PvE hand-off payload (presentation-free)
`EnemyIds` (index 0 = leader, 1..6), `Threat`, `BackdropContext` ("outerworld"/"castle"/"cavern"),
`ReturnScene`/`ReturnPosition`/`ReturnYaw`, `RepId` (rep consumed on victory),
`ArenaPreset` (**carried as data ONLY — no size hook reads it yet**, :50-53).

### BattleArenaHud.cs — battle overlay VIEW (P23-slimmed, Obsidian A2)
`Create()` builds a DontDestroyOnLoad ScreenSpaceOverlay canvas at sort 5000 (:40-48, :103-113).
- `SetFleeHandler` → `HudCommands.RegisterFlee` — the HUD kit's system-area Flee button fires it;
  **no in-file Flee button remains** (:50-52).
- `ShowIntro` = shared `ElarionUiKit.ToastCard` centre card (:59-71).
- `ShowResult` routes through the ONE shared **`EndStateVM`/`EndStateView`** template
  (win = stars/time/spoils/Continue; loss = defeat sting) then `Close()`es itself (:79-92).
- `Close()` clears the flee handler + tears down (:95-100).
- `BattleHud9Zone.Create()` is a **RETIRED SHIM** — logs, registers default flee, returns null
  (`BattleHud9Zone.cs:38-49`); kept only for teardown shape.

### BattleStarRating.cs / ArenaNavMeshBaker.cs / ArenaBiomeDressing.cs / ArenaDeathCam.cs
Support: duration→1..3 stars + reward multiplier; runtime NavMeshSurface bake over children
colliders; biome resolve + backdrop/particles; WO-493 climactic death-cam hold (7s safety cap at
`BattleArena.cs:1767`).

### BattleLock.cs (`Assets/_Modules/Core/Combat/BattleLock.cs`) — assembly-neutral battle gate
Static probe registry (:40-77): battle owners register `Func<bool>`; `IsInBattle()` true if ANY probe
fires (throwing probes skipped). **BattleArena registers `() => BattleInProgress` in `Awake`**
(`BattleArena.cs:303-309`) — this is what flips HudPosture to `hostile(activebattle)` and gates
panels/hotkeys. (The file's own header comment listing only ATBCombatManager/ArenaMode is stale.)

### ROUTING — how a fight reaches the arena (3 live entries + 1 legacy)
1. **Dungeon scripted/boss — `Assets/_Modules/Dungeons/EncounterTrigger.cs`.** `TickScripted`
   fires once on proximity → `RegisterScriptedEncounter` + `BeginEncounterHandoff` (combat lock)
   (:216-238) → `LaunchBattle` (:308):
   - **`ff.dungeonrealtime` ON (default):** builds `EncounterParams` (Threat 3 boss / 1 else,
     `BackdropContext="cavern"`, ReturnScene = CURRENT active scene per WO-770.2, :334-344) →
     `BattleArena.Instance.BeginEncounter(ep)` (:348). A refusal OR throw → **`RollbackHandoff`**
     (:355, :364; :408-418 clears pending encounter + InCombat lock + re-arms `_hasFired`).
   - **OFF:** legacy `SceneRouter.GoBattle(BattleParams{Wave=0, BreachedIds, ReturnScene})` to the
     flat ATBBattle scene (:369-400), also rollback-guarded.
   - Random encounters (`TickRandom` :247-266) are wired but dormant in v1
     (`DungeonLayout.disableRandomEncounters`).
2. **Overworld reps — `OverworldEncounterSpawner.cs:1418`** (engage a wandering rep →
   `arena.BeginEncounter`), gated by `ff.overworldencounter` (default ON since 2026-07-30 reversal).
3. **Stub dungeons — `DungeonStubEncounter.cs:215`** (controller-free scenes; also carries the
   legacy `GoBattle` fallback at :227).
4. Legacy-only ATBBattle entries: `WaveManager.cs:2358` (abandoned Village scene),
   `Assets/_Sandbox/EncounterTrigger.cs:160`, `OwnerDevToolsOverlay.cs:244` (dev button).

### DUNGEON SEAM (WO-770.3b) — `Assets/_Modules/Dungeons/DungeonController.cs`
The real-time fight has **NO scene round-trip**, so the arena's completion event is the dungeon's
only settle signal:
- `SubscribeRealtimeSettle` (:1196-1209, guarded by `_arenaSubscribed` :194 + the flag) hooks
  `OnBattleStaged` + `OnBattleEnded`; `UnsubscribeRealtimeSettle` (:1211-1221) never CREATEs a host.
- **`OnRealtimeBattleStaged`** (:1248-1267): `_cameraRig.SetCombatFraming(true)` — **OTS in fights,
  FPV traversal** (:1252); `_arenaOwnsHero = true`; `SetInputEnabled(false)`;
  **R-A1 guard `SetHeroCharacterController(false)`** (:1230-1239, :1265) — input-off alone left
  `DungeonHero.Update` calling `_controller.Move(gravity)` every frame, i.e. TWO collision bodies
  (CC + arena-driven HeroLocomotion/NavMeshAgent) fighting over one transform;
  `RestoreInjectedHeroMover()` so the arena's mover is sole driver (:1266).
- **`OnRealtimeBattleEnded`** (:1269-1288): restore framing → CC re-enabled BEFORE input returns →
  guard `HasPendingEncounter` (stray-event/double-settle) → **`SettleEncounter(won, wasBoss)`**.
- **`SettleEncounter`** (:1161-1186) — the ONE settlement authority shared with the ATB reload path:
  VICTORY = `DungeonLootGrant.GrantEncounter(wasBoss)` + `ResumeAfterEncounter(true)` (boss credit →
  back-door unlock), hero resumes in place; DEFEAT = `CancelPendingReturnWarp` (the seq512 warp-cede)
  + `ResumeAfterEncounter(false)` + `ExitToVillage` (:1165-1179).
- **`AbandonRealtimeBattle`** (:1303-1336): gated strictly on `_arenaOwnsHero`; called from
  `OnDestroy` (:226) and `ExitToVillage` (:455) → `arena.ResolveAbandoned(...)` + mover/CC/framing
  restore + `ClearPendingEncounter`/`ResolveEncounter` (no settle, no loot).
- ATB (`ff.dungeonrealtime` OFF) resume path: `ResolvePendingEncounter` (:1130-1145) reads the
  outcome off `SceneRouter.PendingBattle.LastOutcome` — stamped by
  `BattleController.cs:622-624` (WO-770.3; missing carrier = loss) — then the same `SettleEncounter`.

---

## PART 2 — DORMANT ATB MODULE (`Assets/_Modules/BattleATB/`)

**Status: DORMANT but compiled, tested, and reversibly reachable** (`ff.dungeonrealtime = 0`
restores the dungeon→ATBBattle route verbatim; the sandbox/stub/dev entries above also reach it).
It reads **static `Defs` tables only — never the talent tree / gear loadout** (the stated reason for
the retire, `FeatureFlags.cs:267-272`). Assembly `DeNelle.BattleATB`; scene `Assets/Scenes/ATBBattle.unity`;
tests `Tests/` (NUnit, editor-only) incl. `RngGoldenVectorTest` bit-parity vectors.

### Per-file inventory (verified; unchanged findings from the prior catalog re-checked where cited)
| File | Role / key facts |
|---|---|
| `BattleController.cs` | Scene orchestrator: builds `BattleSetup` from `SceneRouter.PendingBattle` (dev fallback seed 42 / "skeleton" / "Blaise"), drives `ATBRuntimeState`, code-builds `BattleHudUgui`, swaps capsules, anims via log-diff cursor. **WO-770.3: stamps `PendingBattle.LastOutcome = Victory/Defeat` at :622-624** so the dungeon reload settle reads a real result. Idle-timer wired (`HandleIdleTimeout` ← `ATBCombatManager.onEnemyAutoAttack`). Known intent-gap: `IsCasterHeroClass()` string-matches `_fallbackHeroName`, not the resolved hero. |
| `BattleHudUgui.cs` | FF7-style code-built uGUI HUD (no UXML — UXML dead in builds). **WO-744 MVVM seam: reads `FeatureFlags.BattleHudVm` at :200-201; flag ON binds `BattleHudVM` for abilities/items/active-class resolution (:502-507, :579-581) and pushes snapshots at :818; flag OFF (default) = byte-identical legacy self-resolve.** Visual ATB feel-sim (`TickVisualAtb`) + `OnAction` contract deliberately NOT moved into the VM. |
| `BattleHudVM.cs` | **NEW since prior catalog (WO-744, landmine 1).** Pure-C# read-only snapshot ViewModel: `PushSnapshot(BattleState)` projects ActiveUnitId/ActiveHeroClass/UsableAbilities (off `Defs.HERO_ABILITIES`)/UsableItems (off `Defs.ITEM_DEFS`); `Changed` event. No UnityEngine, unit-testable (`Tests/BattleHudVMTests.cs`). **Dormant at runtime: `ff.battlehudvm` default OFF.** |
| `ATBRuntimeState.cs` (`State/`) | Runtime-only ScriptableObject store (Zustand port): immutable `BattleState` snapshot, all mutation via Engine statics, UnityEvents (`OnActionSubmitted/OnTurnResolved/OnOutcome/OnBattleChanged`), `StartBattle/ChooseAction/StepAi/AutoResolve/EndBattle`, clone-at-boundary, full party reset on win. Asset: `Generated/ATBRuntimeState.asset`. |
| `ATBCombatManager.cs` | Singleton turn idle-timer (8s → `onEnemyAutoAttack`). Registers a `BattleLock` probe. |
| `AtbCombatantSwapper.cs` | `[RuntimeInitializeOnLoadMethod]` bootstrap on ATBBattle scenes: hides stray DDoL village objects (reflection), swaps hero/enemy capsules for `Resources/Heroes|Enemies` models (hero class = direct field read; enemy slug from live `PendingBattle.BreachedIds`). `HideOwnRenderer` still dead. |
| `AtbControlModeStore.cs` | PlayerPrefs Player/AI control-mode persistence (WO-169). Engine plumbing complete; **the player-facing toggle UI is still unbuilt** (`HandleControlModeToggled`/`OnControlModeToggled` never invoked). |
| `ATBBackgroundController.cs` | Dormant orphan (never wired; `_Modules/ATB/Video/*.mp4` unused). |
| `Engine/` (pure C#) | `Types` (all enums/data + `PortMath.RoundTs` JS half-up), `Defs` (static tuning tables — 7 `ENEMY_DEFS`, 3-class `HERO_ABILITIES`, items/statuses; dead `ATB_BASE_FILL`), `BattleScaling` (wave curve, boss every 6), `Rng` (mulberry32, golden-vector bit-parity with the TS reference), `BattleStateOps` (build/clone/read), `Turn` (ATB-fill order + pipeline, `IsPlayerControlled` reads ControlMode), `Actions` (resolve family + `ApplyAction`), `Combat` (damage/element RPS/status), `Ai` (RNG-free pet AI + archetype enemy AI), `Targeting`. `CombatantDefSO` designer mirrors: **no asset instances — dead infrastructure.** |
| Scene/assets | `ATBBattle.unity` still carries the orphaned UIDocument/`BattleHUD.uxml`/`BattlePanelSettings` + stale serialized `_hudDocument`; `Assets/Editor/BattleSceneBuilder.cs` still builds that stale wiring. Harmless, ignored at load. |
| `Tests/` | `ActionsTest, AiTest, BattleScalingTest, BattleStateTest, CombatTest, RngGoldenVectorTest, TargetingTest, TurnTest, BattleHudVMTests, TestSupport` — LIVE, editor-only asmdef. |

---

## FLAGS REGISTRY (combat-relevant, `Assets/_Modules/Core/FeatureFlags.cs`)
| Flag | Default | Gates |
|---|---|---|
| `ff.overworldencounter` (:154) | **ON** (reversal 2026-07-30) | Wandering reps + engage → BattleArena |
| `ff.dungeonrealtime` (:273) | **ON** | Dungeon encounters → BattleArena (OFF = legacy ATB GoBattle, reversible retire) |
| `ff.lockon` (:334) | OFF | Deliberate lock-on (camera frame/face/strafe via HeroTargetIndicator; WO-512) |
| `ff.battlehudvm` (:572) | OFF | ATB HUD snapshot-VM binding (WO-744) |
| `ff.noautoheal` | ON | No post-battle heal (potions/safe-zone recovery; gate at `BattleArena.cs:2190`) |
| `ff.combatfeel` | (see file) | Arena stone-biome retheme (`BattleArena.cs:761`) |
| `ff.atbdungeon` | **DOES NOT EXIST** | Nothing. Doc-lie class — read as `ff.dungeonrealtime` inverted |

---

## RISK LEDGER (known landmines, verified in code 2026-08-02)

- **ARENA-1 — leash writes `transform.position` on a live NavMeshAgent** (`BattleArena.cs:1728`,
  in `LeashStagedEnemies`). An active NavMeshAgent owns its transform; the raw write is silently
  reverted to `agent.nextPosition` next tick, so the fled-pack leash is likely a **no-op** — the
  disengage-resolve timer (7s → loss) is the watchdog actually catching fled packs. Fix requires
  `agent.Warp()`/nextPosition. **No regression exists** — RED-spec only
  (`docs/reference/REGRESSION_COVERAGE_MATRIX.md:102,243`).
- **ARENA-2 — `_familyEngaged` never latches if the leader dies at range → 240s pin.**
  Leash + disengage watchdogs are gated on `heroInArena && _familyEngaged`
  (`BattleArena.cs:1648`); the ONLY latch is `MaybeDisbandOnArrival`'s 4.5m leader-distance gate
  (:1342-1346), and a dead leader fake-nulls `_familyLeader` → early return (:1323) → the latch
  can never set. Members DO break to fight (FamilyLeader.OnDisable → Disband), but if the pack then
  becomes unreachable the fight waits out the full `BattleTimeoutSeconds` 240s (:101, :1702).
  The abandonment/out-of-arena watchdogs (patch 6) do NOT cover this case. **Watchdog-patched only;
  no regression** (`REGRESSION_COVERAGE_MATRIX.md:103,244`).
- **Hardcoded arena enemy stat table** — `BuildEncounterDef` (`BattleArena.cs:1406-1445`) mirrors
  but does not read `enemies.json`/ATB Defs; balance is a code edit (follow-up ticket noted in code
  :1429-1432). Substring matching is order-sensitive (hollow rows deliberately before orc rows, F8-8).
- **Reflection seams in the hot path:** `WarpHero` → `HeroLocomotion.WarpTo` (:1461-1465) and
  `GrantWinReward` → `HeroProgression.AddXp` (:2258-2262) — a rename breaks silently to
  fallback/no-XP (Guard/logs exist).
- **`EncounterParams.ArenaPreset` is dead-carried** (:50-53) — WO-606 geotag data flows in, nothing
  scales the fixed 45×36 footprint from it.
- **`ResolveAbandoned` deliberately skips `OnBattleEnded`** (:1874) — any future listener that
  expects a completion signal on every stage must also watch abandonment.
- **`BattleLock.cs` header comment is stale** (lists ATBCombatManager/ArenaMode only; BattleArena is
  a third registrar via `Awake`). Comment-lie class, code is fine.
- **ATB dormant-path drift risk:** the ATB engine's static Defs vs the arena's hardcoded table are
  kept coherent by hand (arena comment :1424 cites Defs parity). Flipping `ff.dungeonrealtime = 0`
  re-enters a path that is compile-green + unit-tested but not felt-tested since ~2026-06-29.
- **WO-584 slices 2–5 unshipped** — do not treat "dungeon/outpost/arena consolidation" as done;
  only the flag-gated dungeon→arena route exists.
- Carried-forward ATB flags (still true): control-mode toggle UI unbuilt (F-CTL-1);
  `CombatantDefSO` dead; ATBBattle scene's UIDocument/BattleSceneBuilder stale;
  `IsCasterHeroClass` fallback-name string-match intent gap.

## SEAM MAP (one line each)
- Trigger → arena: `EncounterTrigger.LaunchBattle` / `OverworldEncounterSpawner:1418` /
  `DungeonStubEncounter:215` → `BattleArena.BeginEncounter` (returns bool; false ⇒ caller rollback).
- Arena → scene owner: `OnBattleStaged` (camera framing) / `OnBattleEnded` (dungeon
  `SettleEncounter` WO-770.3b) / `CancelPendingReturnWarp` (defeat settle cedes the warp).
- Arena → shared systems: `BattleLock` probe (HUD posture + panel gate), `RepEngageWatcher`
  PauseAll/ResumeAll/QuietNonPursuers, `EnemyFactory`/`EnemyBrain`, `HeroHealth.Respawn`,
  `EconomyService`/`GearLoadout`/`HeroProgression` (rewards), `EndStateView` (results),
  `IsArenaPosition` (arena-vs-home hit discrimination).
- ATB → dungeon (legacy): `SceneRouter.PendingBattle.LastOutcome` stamped at
  `BattleController.cs:622-624`, read at `DungeonController.cs:1137-1144`.
- Headless: `BattleArena.ResolveForTest` ← ArenaCombatOracle; ATB engine ← NUnit suite +
  `DungeonRealtimeSettleRegression` (Editor).
