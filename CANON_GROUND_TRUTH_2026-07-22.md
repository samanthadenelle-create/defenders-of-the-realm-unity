# CANON GROUND TRUTH — 2026-07-22 (SME fan-out synthesis)

> **LIVE ANCHOR (owner-blessed 2026-07-22).** Produced by a 17-agent read-only SME fan-out (12 module +
> 5 high-level), each verifying **from code, not comments** (§12), against the live tree at HEAD `148ab637`.
> **Supersedes `CANON_GROUND_TRUTH_2026-07-19.md`** (bannered). If a doc contradicts a line here, the doc is
> stale. Read order unchanged: this → `KEY_FACTS.md` → `SESSION_CANON_LOADER.md` → `SAMANTHA.md` →
> `docs/HANDOVER.md` → `docs/MASTER_CATALOG.md`. **Push still HELD** (owner authorizes push + prod promotion).
>
> **Coverage:** all **17 of 17 domains code-verified** below (12 module + 5 high-level).

---

## 0. STAR NORTH (unchanged)
- **Pi Hackathon WON** (owner, 2026-07-17). The "July-31 deadline / build mode IS the demo" framing is
  **RETIRED**; roadmap is **OPEN**, owner sets the next north star. Any doc leaning on the hackathon
  deadline is STALE.
- Product: **"Echoes of Elarion"** (Chapter One) in **"Defenders of the Realm"**; tagline
  **"Hold the last light."** Mobile web (Pi Browser), portrait-first; desktop = dev proxy, never the verdict.
- V1 = **one controllable Knight "Grom"** + isolated real-time `BattleArena` combat + player-built city.
- Bar = the **ten-year-old test** ("wow, this feels good" on a phone). Headless proves *binding*; only the
  owner's hands prove *feel*. **The player never sees a failure** — errors loud in the db, invisible on screen.
- Economy direction: **V1 ships ZERO crypto**; soft currency client-owned now, flips server-authoritative
  when it carries real value; SKR a later separate arc. Monetization = **sell time, never power**.

## 1. THE UNIFYING SPINE (the Rosetta stone)
**You directly control exactly ONE thing — the hero. Everything else is AUTONOMOUS. Allies exist only where
autonomy is a feature, never something you micro.** This one principle wins on 3 axes at once:
- **Design filter:** companions-as-followers (micro'd) = BUST → deleted; troops-as-auto-defenders = good;
  pets-as-combatants = bust → **Echoes-as-autonomous-harvesters**.
- **Scope:** one hero rig; combat is a composed **battle-anchor tableau** (fixed stances + one camera,
  defined once) — no combat nav, no camera chase. Animation IS mechanics (the enemy wind-up is the telegraph).
- **Performance (a consequence, not an optimization):** throttle autonomous AI ~4 ticks/s; auto-resolve the
  unwatched (continue = math, watch = render); bounded agent counts by design — **the game-feel caps and the
  perf budget are the same caps.**

## 2. THE ECONOMIC WEB
Two loops that feed each other; every *producer* autonomous, only the hero controlled.
- **Loop A (V1, left half):** hero offense → drives enemy back → **strengthens the Tree of Life's life force**
  → faster/more **Echo harvest**. Offense = persistent world-reclamation ("reclaiming the world"), not loot.
- **Loop B (V2, right half, `ff.basebuilding` OFF):** build town/defenses → **player-triggered escalating
  waves** (the more you trigger, the deeper they push) → each wave drops rewards → **press-your-luck banking**
  (line breaks = lose unbanked rewards). Troops auto-defend; you never lead them.
- **3 LOCKED resources:** WOOD (structures), IRON (hero gear), GRAIN (troop upkeep). Gold = separate STORE
  currency. Echo workforce **cap = 5** (3 organic + 2 flex); ONE interaction = in-game drag-drop assign.
- Phasing is dependency order, not compromise: base has nothing to feed on until offense is real.

---

## 3. REPO / GIT / GATES (verified this session)
- **Branch `wip/village2-and-f8-tickets`. HEAD = `148ab637`** (2026-07-20 22:46), **local == origin (0/0) —
  the 07-20 overnight arc IS pushed.** Prod UNTOUCHED (still 07-16 `q2v5vj86g` on
  `defenders-of-the-realm-v2.vercel.app`). Push of anything new + prod promotion remain the owner's.
- **Gates GREEN:** `COMPILE_GATE_OK` (compile + brace + NUL); **`DataRegression.RunAll = REGRESSION_OK`,
  ZERO reds** (`Builds/data-regression11.log`, Jul-20 19:49). ⚠ `logs/night-regression.log` shows
  `REGRESSION_FAIL: 8` but it is **Jul-16 STALE** — do not cite it.
- **16 P1 SME suites green** (WAVE_SCALING, ENEMY_REWARDS, WALL_MITIGATION, UPGRADE_AUTHORITY, PACK_GRANT,
  CRYSTAL_PRODUCTION, SFX_RESOLVE, DUNGEON_EXIT, DUNGEON_DRESSING, MODAL_REGISTRATION, FOUNDING_REACH,
  FTUE_HONESTY, ECHO_CARD_COPY, SHADER_PIN, WAVES_SCHEMA, PACK_COSMETIC_INTEGRITY) + ratchets
  `[ui-mvvm]` (baseline EMPTY, HardFailOnNew), `[ui-obsidian]`, `[room-forge]` at 0.
- **Save schema v34** (persists Tribes/Wards/Arena + pet active-slot). WO next-free = **754**.
- Working tree: churn only (`.meta`/`.mat` re-import + untracked gitignored art packs + `Logs/*.pid`); **zero `.cs`**.
- No Unity.exe running → gate/bake path clear.
- **Branch hygiene (2026-07-22):** 2 stale agent worktrees removed + their local branches deleted (dungeon
  work confirmed already merged into wip); **2 stale remote branches purged** — `feat/tower-core-loop`
  (recover: `cea673e4`) and `samantha-village-progress-2025-05-23` (recover: `40a570a6`). Remotes now =
  `master` (base) + `wip/village2-and-f8-tickets` only. No code pushed; only the branch deletions.

---

## 4. ARCHITECTURE LAW (HP-B2B) — unchanged, binding
1. **Bounded context per component** (asmdef boundaries: Village→Core, HUD→Core, never Village↔HUD).
2. **Presentation never touches the objects** — objects expose state; presentation observes/renders.
3. **The One Model** — Realm ⊃ City-State ⊃ Building(entry) ⊃ composable **capabilities** (Interactable /
   Upgradable / Destructible / Targetable). Behavior = SUM of capabilities, never inherited per type.
   **POOL by default**, one owner per concern (the two-VFX-stack scar). ⚠ **The clean model is SPEC** — the
   directive doc self-labels "NOT yet implemented"; there is **no `Capability` enum** (only `NavSurfaceKind`),
   capabilities live as ad-hoc `RepoProps` fields + `IDamageableStructure` + `behaviorId` strings.
4. **Queue by leverage, not effort** — player-felt earns the queue; holistic/structural is leverage, logged,
   never smuggled into player-facing work. **Tests are the permission gate** for holistic change.
- Orient/grip/seat **derived from bounds + name**, never guessed (`WEAPON_ARMOR_ORIENT_LOGIC.md`).

---

## 5. CODE-VERIFIED SYSTEM STATE (module SME digests)

### 5.1 Core / Save / Services
- **Save schema v34** (code truth; `MASTER_CATALOG/core.md` still says v33 — STALE). Migrator explicit steps
  2-10,14,17,18,21-34; additive-default-on-read otherwise. HMAC-SHA256 signed local save (`dotr-save`),
  client-embedded key = obfuscation only (real authority is server, undeployed).
- **`CoreServices` = 7 slots** (Hud, HudModel, Population, Audio, Jupiter, WalletSigner, SceneLinkResolver) —
  header says 3 (stale). **`SceneRouter.Castle` is now a PROPERTY**: `MergedWorld ? "Main_Castle_Overworld"
  : "MainCastle_Hall"`, MergedWorld default ON → **live home hub = `Main_Castle_Overworld`.**
- `CanonicalJson` → swappable `ICatalogSource`; Resources-first, StreamingAssets fallback. `PanelManager`
  single-modal arbiter with WO-437 battle-lock + WO-465 invisible-scrim self-verify + F8-15 DeathTrace.
- **Reflection bridges** (Core→Village, fragile): `WaveManager.OnWaveCleared`, `HeroLocomotion.WarpTo` — now
  loud-fail on sever. Backend undeployed (resilient/local-only, `BackendAuthConfig.Enforced` OFF).
- ⚠ **`FeatureFlags` DevResourceTool + FlagButton default ON** — in-code note: flip OFF before any public build.

### 5.2 Village Hero + combat feel (Grom)
- Body chain: `HeroClass.Knight` → `ff.knightv3` ON → `Resources/Heroes/KnightV3.fbx` → `ff.mocaploco` ON →
  **`KnightMocap.controller`** (code-generated by `HeroAnimatorFactory.BuildKnightMocapController`). Paladin
  lane (`weaponskill-animations.json`) DEAD for Grom.
- **HeroLocomotion = NavMeshAgent kinematically input-driven** (`_agent.Move`, updateRotation=false, speed=30);
  the in-file "pure transform" lie is now annotated-corrected. Camera = SmartMobileCamera.
- Ability kit = **ATTACK pill + Q/W/E/R** (5 inputs): Sword Wielding / Sword Heroic (Q dash+stun) / Shield
  Charge (W cone knockback) / Warden's Grace (E, WO-750 redesign → `gracebuff` heal+HoT+shield) / Radiant
  Strike (R meteor 220). `CastVariantKeyword[4]=null` → R cast VFX **silent by design**.
- Gear: weapon+shield **visible**, armor **static** (tint only, no mesh swap), rings+amulet **invisible stat-only**.
- **LANDMINE — stale bake:** generic Cast v0 on-disk = `atk_slashright` (a sword slash) not the registry
  spell clip → F8-48 violation if any equipped ability resolves to variant 0. Fix = re-run
  `BuildKnightMocapController`. Also: E's -20% DR window not wired; R clip still `atk_spin` pending re-bake;
  weapon meshes absent from `Resources/Heroes/Props/Weapons/` → primitive fallback.

### 5.3 Village Systems (BuildMode / Economy / Upgrades / Arena)
- Build-mode CREATE verb wired end-to-end for towers (enter→ghost→charge-after-commit→`GameState.BaseLayout`→
  `BaseLayoutLoader` rebuild). FoundingKit = {pet-house, lumberyard, tower_ground_archer} free once each;
  everything else ONE free total (`FreeBuildsUsed`, never resets). `ff.strategicplacement` **REMOVED** — always on.
- **Dual-wallet:** Wood/Iron = in-session pool; Food/Crystals/Coins = read-through `GameState.Resources`.
  R2 fix (`ef6f097b`) mirrors Wood/Iron **income** into GameState. ⚠ **RESIDUAL:** `TrySpend` still decrements
  only the pool, not GameState → spend asymmetry (income mirrored, spend not). `CrystalEconomy` delegation unverified.
- Upgrades = two families keyed like `DialogueCommandBridge`: CITY-tier (`BuildingTierCatalog` → `GameState.BuildingTiers`,
  WC3 perks bought with Gold) vs LEGACY resource buildings (`ResourceBuildingProgression`, hardcoded ladder).
  **Collector-id resolution** (`CatalogRegistry.ResolveUpgradeId`, `collector_lumbermill`→`lumbermill`) is the
  fix that unblanks collector grids. VillageTier gate prepended to every grid (sole `VillageTierService` caller).
- WO-751 Y-height (4m/tower 7m/siege 3m, fit-to-height) + WO-753 `Destructible` (one-owner VFX teardown) landed.
- Arena ~75% stub: `ArenaWalletService` PlayerPrefs SKR stub; ArenaCatalog/DefenseCatalog hardcoded (TODO→json).

### 5.4 Echo workforce + Tutorial/FTUE
- Roster = **6 named souls CODE TABLE** (`EchoRosterCatalog`, no SO): Aldwin(1)/Elowen(2)/Corvin(3)/Bran(4)/
  Doran(5)/Maren(6). Balance in `echoes-balance.json` (v1, maxLevel 8). **`EchoBonusCalculator` = single math source.**
- **Only Harvest reads live.** Crafting/Defense/Exploration = **write-only stubs** (grep: zero production
  readers) — assigning to them changes a number nothing consumes (picker agency ~75% placebo).
- **Founding-identity contradiction:** Aldwin is taught + auto-assigned as HARVESTER, but his identity is
  `PreferredLane=Exploration, HarvestResource=null` → never earns the 0.75 match bonus, can't honor "wood,
  iron, or grain." (Deliberate-weak or bug — owner ruling.)
- FTUE-01 (founding choice reachable via both HeroSelect + PetSelect) and FTUE-02 (honest defense copy)
  appear **RESOLVED in code** — the `MASTER_BACKLOG_2026-07-19` entries for them are stale.
- Gaps: claim/Dump loop **never taught** (FTUE-05); `ctx_gear_equip` hint dead (FTUE-06); WO-752 Part B
  post-tutorial pet handoff unbuilt; `return_home` "through the gate" copy stale for merged world.
- **`ECHO_WORKFORCE_SPEC.md` + `EchoService` header say "max 4" — code ships 6** (STALE).

### 5.5 Enemies / World / Waves
- `Enemy`/`EnemyFactory`/`EnemyBrain` NavMeshAgent-driven. **orc-raider SSOT unified** via new
  `WildlandsRoster.cs` (reads enemies.json hp 130, byte-identical fallback) across the 5 Wildlands spawners.
  ⚠ **NOT closed:** `TribeManager.BuildRaiderDef` (hp 60) + `WardTetherService.BuildKindleDef` (hp 55) still
  hardcoded off-SSOT (EW-2/EW-4); caveman/feral-wolf/tiefling-cultist still divergent code-defs.
- Waves: `EnsureScalingCurve()` never returns null (WAVE_SCALING fix — runtime-default curve HP 1.0→2.5,
  dmg 1.0→2.0 by wave 20). enemies.json **v4** (xp/coinReward). `WallDefense` (static, in `WallTierData.cs`)
  reads walls.json `heartDamageMultiplier` (Heart hits only). waves.json v1, 20-wave schedule.
- World: `WorldSceneLoader` additive; TWO raid mechanisms — in-world `RaidOutpostSystem` (**4** cardinal
  outposts, `_enabled` ON; header "ONE outpost/180s" STALE, actual 4/10s) vs standalone `Garrison_*` scenes.
  `ZoneManager` classifier (4 cardinal regions by danger tier). `DiagTerrain` splatmap side-effect still live.

### 5.6 Combat — BattleArena (LIVE) + ATB (gated)
- **LIVE = real-time `BattleArena`** (`ArenaCentre 5000,0,5000`, isolated far-offset stage, composed tableau,
  runtime NavMesh bake). Gates `ff.overworldencounter` ON + `ff.dungeonrealtime` ON. Reuses
  PlayerAttackController/HeroAbilities/HeroHealth under `BattleLock`. **Lock-on `ff.lockon` default OFF** →
  dormant. WO-584 arena-trio slices 2-5 (resolver/outpost/ownership-flip) NOT shipped — only dungeon→Arena live.
- **ATB = built + golden-vector tested but bypassed/dormant** (BattleController/swapper/BattleHudUgui in the
  ATBBattle scene, `ff.battlehudvm` OFF). ATB reads static `Defs.HERO_ABILITIES`, never the talent tree/loadout.
- **CANON LIE:** `ff.atbdungeon` **does not exist** (grep = 0 hits). Docs/WO-584/COMBAT_PIVOT say "ATB dungeon
  behind ff.atbdungeon (OFF)"; the real gate is **`ff.dungeonrealtime` (default TRUE)** routing dungeons INTO
  BattleArena. Inverted sense + wrong name — fix the canon.
- **LANDMINES:** ARENA-1 leash writes `transform.position` on a live NavMeshAgent (desync); ARENA-2 global
  rep-freeze latch + 240s battle-timeout pin (watchdog-patched, not re-architected).

### 5.7 HUD / Panels / UI-MVVM
- HUD **rebuilt to HudKit** (`HudKitController` + `PostureEvaluator` + `hud-areas.json`); VillageHudController
  is now the command/host shell. Context = **posture** (calm/hostile/modal), not scene+radius. Combat layout
  behind `ff.combathud611` (ON). **WO-750 verified:** ability medallions render with **no Q/W/E/R key badge**.
- `PanelManager` arbiter + `ModalArbiterRegistrationRegression` [`modal-registration`] source-lint: any
  TopBand (≥31000) modal must call Register/NotifyOpened/NotifyClosed. **WO-744 MVVM COMPLETE** —
  `[ui-mvvm]` baseline EMPTY + HardFailOnNew=true (limitation: file-level, not call-level). Shared `WalletVM`
  emits **colour-free letter badges** (W/I/F/C/G) for red/green colorblindness.
- **UXML-in-builds prohibition is in-code law**; `ElarionUiKit` is the ONE code-built uGUI factory.

### 5.8 Data catalogs
- `CanonicalJson`/`LocalJsonCatalogSource`: **Resources/Data/Canonical WINS**, StreamingAssets fallback.
  `DataWebRegression` (DATAWEB) enforces dual-copy + WebGL-omission (pins the "known six" mirrored) +
  parse + version + gear curation. **WebGL-null risk effectively closed** for canonical catalogs.
- SSOT/version: buildings v2 (crystalsPerWave), enemies **v4** (xp/coin), walls v1 (heartDamageMultiplier —
  ⚠ field added but version NOT bumped), structures-catalog v4, echoes-balance v1.
- **Deliberate drift (WO-747 GEAR-1):** weapons.json/armor.json Resources = curated superset, byte-exempt from
  DATAWEB; `CheckGearCuration` asserts picks present + rows well-formed. Resources is truth for gear.
- Orphan 3rd copy `Assets/Data/Canonical/{weapons,armor}.json` (unloaded, delete candidate). **IronScrap =
  faucet with no drain** (dropped 6×, consumed by 0 recipes) — open economy gap.

### 5.9 Economy-meta / Monetization
- **3 unreconciled persistence stores:** packs→`GameState.OwnedItemIds` (dotr-save); Glimmer+cosmetics→
  `GlimmerCurrencyService` (dotr-cosmetics-v1); battle pass→`BP_*` ints. Pack→cosmetic split-brain **bridged**
  07-20 via `GlimmerCurrencyService.MarkCosmeticOwned` (catalog-independent write into `_ownedSet`).
- ECON P1s all **green + guarded** (`PackGrantRegression`, `PackCosmeticIntegrityRegression` over all packs).
  **packs.json now has 13 packs** (WO-755) — `economy-meta.md` says "5" (STALE). Pet active-slot persists (v34).
- Wallet/crypto **all stubbed, devnet, `BackendAuthConfig.Enforced` OFF**; SKR mint empty; Jupiter signs nothing.
  Aligns with V1-ships-zero-crypto. PackStore code-built (UXML-safe) but WalletConnect/JupiterSwap panels still
  UXML → empty in player builds.

### 5.10 Dungeons / RoomForge / Scenes
- **Room Forge** built: socketed rooms → layout JSON → `DungeonBaker` (`DungeonBakerChecks.Compose` single
  oracle, hard-gate abort) → `DungeonDresser.DressRoom` (07-20 prop seeding, colliders stripped) → NavMesh.
  `RoomForgeRegression` [`room-forge`] 10 cases green.
- **Two dungeon paths:** (A) `dg_starter_loop` composed (11 rooms, hero+enemies+**exit** via 07-20
  `DungeonExitInteractable`, but **NO loot bank**); (B) `Dungeon_HealersCottage` data-driven (WO-749
  `DungeonLootGrant`→larder, chests). `d4_sunken_crypt` **PURGED**. `Dungeon_FolksGranary` = stub.
- **Scene graph:** live home hub = **`Main_Castle_Overworld`** (MergedWorld ON, one continuous navmesh, no
  additive seam). **23 build scenes** (catalog says 13). `Village.unity` **deleted from disk**. Dungeon portals
  **ON by default** (`ff.dungeonportals` defaultOn:true) despite a "gated OFF" comment lie.
- **LANDMINE DGN-1:** `DungeonCompose/dg_starter_loop.unity` ships **BINARY-serialized** (batchmode SaveScene
  can't honor ForceText) — valid/loadable but un-diffable (same class as the old d4 corruption).

### 5.11 Editor tools / Gates / Regression / Build
- Gates: `CompileGate.Run` (compile→NUL-scan→brace-scan) → `COMPILE_GATE_OK`; `DataRegression.RunAll` (16 P1
  suites + ratchets, each Guard.Try-wrapped) → `REGRESSION_OK`; `UICaptureLaunch.RunCaptureHeadless` (edit-mode
  synchronous render) → `UI_CAPTURE_OK` + PNGs. Legacy `RegressionSuite.cs` superseded by DataRegression.
- Headless loop: gate → `build-windows.ps1` → `run-autopilot-fleet.ps1` → `harvest.sh` (Unity editor MUST be
  closed). `-nographics` = no pixels (magenta/UITK bugs need F8/human). break-log = ERROR-level only.
- Build config: **WebGL ship = `BuildOptions.None`** (`-devBuild` opts into Development — never deploy a
  DevBuild); Desktop Windows ships Development (dev QA). ⚠ **Duplicate MenuItem** `Defenders/Build/WebGL Player`
  in both WebGLBuild + DesktopBuild (divergent settings; only one binds).

### 5.12 Web backend / Dialogue / Audio / Resources-art
- **Web trace read path (code-verified):** `api/` is git-tracked in-repo. `WebTrace.cs` — both gates OPEN
  (`FeatureFlags.WebTrace defaultOn:true`; `TraceEndpoint` hardcoded to **prod** `…/api/trace`), so EVERY
  build (prod + previews) POSTs into Neon `analytics_events` (`event_name='web_trace'`); `@host` build-id is
  the only deployment discriminator. **The real CLI read path = `api/admin/db.js?view=traces`** (X-Admin-Key
  = `ADMIN_DASH_KEY`, set 07-15; `order=asc`+`offset` for the diagnostic HEAD). ⚠ **The canon "read the
  `[sig]` echo in Vercel runtime logs" is a DEAD END** — `vercel logs` returns only the 1 summary line per
  request, never the per-line `[sig]` echoes; a naive vercel-logs watcher fires never. Web-F8 = `websig-watch`
  daemon polling the admin endpoint into the shared `logs/f8-inbox`.
- **Dialogue:** ONE shared Yarn runner (`DialogueService` hosts `Resources/Dialogue/DialogueSystem.prefab` —
  ClassicRPG **Canvas, not UXML** → WebGL-safe; 64 nodes/21 files). `DialogueCommandBridge` = the single live
  bridge, **~40 verbs** (header says ~30 — stale). **`NPCCommandBridge` DEAD** (registers nothing) — the
  **single-register rule**: every Yarn action name must register EXACTLY ONCE project-wide or the source
  generator throws and breaks ALL dialogue. Vendor `.yarn` headers still credit verbs to NPCCommandBridge (lie).
- **Audio:** `AudioService` (A/B crossfade + 8-voice SFX pool, `CoreServices.Audio`). ⚠ **`GameAudioMixer.mixer`
  is a STUB** — Master group only, `m_ExposedParameters:[]`, no Music/SFX/UI/Voice — so every `SetFloat`/
  `FirstGroup` silently fails; **only the AudioSource-direct fallback controls volume/mute.** The documented
  5-group/5-param mixer was never built (5 sources assert it). **`SfxClipLibrary.asset` + `DeNelleAudioService.prefab`
  don't exist** → `PlaySfxAtPosition(SfxId)` is a dead no-op; live SFX = Village-side `GameSfx` (`Resources.Load("Sfx/<id>")`
  ?? procedural; only LookoutHorn ships) — the **07-20 SFX_RESOLVE fix lives on that Village path, not the Audio module.**
- **Resources/art:** ⚠ **`Resources/HeroPortraits/` folder does NOT exist** → every hero-portrait load returns
  null despite code/comments claiming otherwise (portraits never render). `Resources/Pets/*` + `Cosmetics/Pets/*`
  empty → procedural. Typo'd shipped icons (`wiard.jpg`, `Wizard_Lightining.jpg`). **Two-machine drift:** big
  packs gitignored; `Resources/Structures|Enemies` prefabs ARE committed (runtime art survives) but source
  `Models/KayKit*` don't — **"black village" on a machine that hasn't re-imported = missing Models packs; no git
  signal tells you which state you're in.**
- **⚠ Deploy chain lies on failure:** `webgl-vercel-overnight.ps1` writes `CHAIN_DONE` regardless of vercel exit
  code. The **07-18 run FALSELY reported success** (`CHAIN_DONE` written, but `DEPLOY_URL` was an OAuth
  device-login URL — CLI unauthenticated — and `vercel-deploy.txt` ended `fetch failed EXIT=1`). **Never trust
  `CHAIN_DONE`;** verify `vercel-deploy.txt` EXIT + that `DEPLOY_URL` is an `https://…vercel.app`.

### 5.13 Engine-architecture vision (BUILT vs SPEC — the honesty gap)
The engine docs are one coherent single-law vision, but **the grand unifiers are mostly SPEC, not code.**
- **BUILT (the bounded first payment):** the catalog⊥repo data model (`CatalogEntry`/`RepoProps`/`CatalogRegistry`/
  `PlacementRules`) + build-mode end-to-end **for towers** (`BuildModeController`/`PlacementGrid`/`GhostPreview`/
  `BaseLayoutLoader` — carves `NavMeshObstacle`, does NOT runtime-bake) persisting `PlacedStructureData`
  (grid-relative, save v14, headless-replayable) rebuilt via **`StructureFactory`** — the ONE concrete create
  path (`behaviorId`→component switch, "add cases, not reflection"). Monster-Family layer **partially built**
  (`Village/Families/`: FamilyLeader/FamilyMember/FormationController). Orient template built
  (`CatalogOrientationBaker` + `HeroBowAttachment.NormalizeInto`).
- **SPEC / ABSENT (grep-verified none exist):** `EngineDispatcher`, `IBuildHandler`, `WorldDef`, `NavSurface`/
  `NavSurfaceFactory`, the `Character`/`CharacterFactory`/`CharacterDef`/`WeaponDef`/`ActionSet`/`*Brain` actor
  substrate, `WeaponOrientHelper`, the composable `Capability` enum. The generic typed-dispatch "one builder for
  designer + player-build + server-replay" is entirely on paper (WO-119…128 unbuilt); `StructureFactory` is its
  concrete non-generic stand-in. Zone-streaming = Phase-0 (logical zones only). Orient **generalization** unbuilt;
  in practice auto-orient is advisory — **only human-verified `manual=true` corrections are applied** (a real-world
  tempering of the "derive deterministically" law).
- **Test permission gate:** harness real (`Data/Tests/BuildingCatalogTest.cs` + ~29 files) but no
  capability-composition suite exists because the model isn't built. The gap to the full vision is a **climb, not
  a rewrite** — the docs are explicit: reconcile additively, guard the VFX scar (pool-by-default) before scaling
  the One Model past the buildings leaf.

---

## 6. CATALOG-DRIFT LEDGER (the §15 fix-list — the sweep's headline output)
Every agent independently confirmed: **code is healthy; the `MASTER_CATALOG/<area>` sections have drifted
weeks behind** (they are dated 2026-06-12 on the stale `feat/tower-core-loop` label). Their *method + area
maps* remain trustworthy; their *counts + state facts* are STALE. Prioritized corrections:

| Doc / section | Says (stale) | Reality (code-verified) |
|---|---|---|
| `MASTER_CATALOG/core.md` | save v33; Tribes/Wards/Arena NOT persisted; CoreServices 4 slots | **v34**; those 3 persisted; **7 slots**; `SceneRouter.Castle` is a property (MergedWorld) |
| `MASTER_CATALOG/scenes.md` | 13/14 scenes; home hub `MainCastle_Hall`; OuterWorld always additive | **23 build scenes**; home hub **`Main_Castle_Overworld`** (merged, one navmesh); `Village.unity` deleted |
| `MASTER_CATALOG/misc-modules.md` | (no RoomForge) | omits entire RoomForge/DungeonDresser/DungeonLootGrant/DungeonExit stack |
| `MASTER_CATALOG/data-catalogs.md` | 26 Resources / 32 SA / "6 WebGL-broken" | **~70 Resources / ~72 SA**; the six are mirrored; ~40 catalogs uncatalogued |
| `MASTER_CATALOG/economy-meta.md` | packs.json = "5 packs" | **13 packs** (WO-755); PetActiveSlot now persisted (v34) |
| `MASTER_CATALOG/hud.md` | 3-canvas VillageHudController HUD | **HudKit** rebuild (posture-driven); MVVM ratchet closed |
| `MASTER_CATALOG/editor-tools.md` | `RegressionSuite` is the gate; WebGL ships Development | **`DataRegression.RunAll`** (16 suites); WebGL ships **None**; ~150-WO behind |
| `MASTER_CATALOG/village-hero.md` | Blaise + class bodies + party-of-4 | STALE banner present; live = single Knight Grom |
| `ECHO_WORKFORCE_SPEC.md` + `EchoService` header | max 4 echoes | **6** |
| `NORTH_STAR.md` | Blink hero / party / crypto-forward V1 | STALE banner; live = single Knight, V1 zero-crypto |
| PROJECT_INDEX / docs/README / Assets/_Modules/README | STALE:2026-07-12 banners | read banner first |

## 7. COMMENT-vs-CODE LIES REGISTRY (verified this sweep)
- **`ff.atbdungeon` does not exist** — real gate is `ff.dungeonrealtime` (default TRUE, inverted sense). *(canon-wide)*
- **`ff.dungeonportals` comment "gated OFF"** — code `defaultOn:true` (portals ON).
- **`WebTrace.cs` "DORMANT / default OFF"** — `FeatureFlags.WebTrace defaultOn:true`, live endpoint (already annotated).
- **HeroLocomotion "pure transform"** — NavMeshAgent (annotated-corrected in-file; lie persists in other docs/grep).
- **`RaidOutpostSystem` "ONE outpost / 180s"** — 4 outposts / 10s.
- **Generic Cast v0 clip** = `atk_slashright` (sword) not the spell clip — live F8-48 risk (stale bake).
- `CoreServices` header 3 slots (7). `SaveSchema` header v33 (34). `EchoLaneBonuses` header "read by hosts" (no readers).
- `IsCasterHeroClass` string-matches dev fallback "Blaise". `README_HUD.md`/`VirtualDPadLean` reference non-existent `HUDManager`.
- **`[sig]`-in-Vercel-logs read path is a DEAD END** — only `api/admin/db.js` returns actual lines (canon says otherwise).
- **`CHAIN_DONE` in the deploy chain is written unconditionally** — the 07-18 preview deploy failed (OAuth/EXIT=1) yet reported success.
- **`GameAudioMixer` documented as 5-group/5-param** (5 sources) — asset is Master-only stub; volume rides AudioSource-direct fallback.
- **`NPCCommandBridge` credited in vendor `.yarn` headers** — dead; all verbs are on `DialogueCommandBridge`. `DialogueCommandBridge` header "~30 verbs" (actual ~40).
- **HeroPortrait code/comments claim `Resources/HeroPortraits/`** — folder doesn't exist; portraits never render.

## 8. OPEN GAPS / LANDMINES (consolidated)
- **Echo lanes:** 3 of 4 (Crafting/Defense/Exploration) are write-only stubs — picker advertises +% nothing reads.
- **Founding echo identity** contradicts its taught harvester role (Aldwin = Exploration/null).
- **Dual-wallet spend asymmetry** (income mirrored, spend not) — build-mode Wood/Iron spend can drift pool < GameState.
- **Arena AI:** leash transform-write on live agent (ARENA-1); 240s rep-freeze latch (ARENA-2) — watchdog-patched only.
- **orc-raider SSOT incomplete:** TribeManager (60) + WardTetherService (55) still off-SSOT; other roster ids divergent.
- **IronScrap** faucet-with-no-drain; **weapon meshes** absent → primitive fallback; **Warden's Grace -20% DR** unwired.
- **dg_starter_loop** ships binary + carries no loot bank; **duplicate WebGL MenuItem**.
- **Claim/Dump loop never taught** (FTUE); WO-752 Part B pet handoff unbuilt.
- **Audio mixer stub** (no per-group control), **`SfxClipLibrary.asset` missing** (SfxId path dead), **HeroPortraits folder missing** (portraits never render), `Resources/Pets` empty (procedural).
- **Deploy chain false-success** (`CHAIN_DONE` unconditional) — verify `vercel-deploy.txt` EXIT + real `…vercel.app` URL, never trust the marker.
- **Engine grand-unifiers unbuilt** (EngineDispatcher/Character substrate/Capability enum/WeaponOrientHelper) — the generic-dispatch vision is a climb from the built catalog⊥repo + StructureFactory payment.
- **Headed-web render-fidelity band** (`VISUAL_AUDIT_STATUS.md`): capture renders into bottom ~50% of canvas;
  5 hypotheses ruled out; **pivot available today** = edit-mode `RunCaptureHeadless` / Windows DevBuild
  `-uiCapture` for the layout audit; web↔exe pixel parity specifically waits.
- **Security/legal (WO-684):** TTL cron for trace rows; rate-limit open POSTs; gate HelpMenu 5-tap crystal
  self-grant behind `#if DEVELOPMENT_BUILD`; **apex dragon = CC BY-NC — license/replace before commercial ship.**

## 9. OWNER RULINGS IN FORCE
One-free-total build + FoundingKit exemption; gear drop 2%/slot; founding Echo fires full EchoUnlockDialogue;
Echo lanes Harvest-only wired; destroyed = no-rebuild + full-cost + VFX cleanup; Y-height normalization
(4/7/3m); Right ActionBar = Attack + Q/W/E/R named skills, **no mobile key-letters**; ASCII-only TMP;
never meaning by color alone (red/green colorblind); sell time never power; **do NOT sell Echoes directly**;
headless UI-screenshot pass before any UI ship; WWCD tiebreaker.

## 10. OPEN / OWNER'S
- **Felt-verify queue:** the 16-suite fix wave on mobile; WO-748/749 screens; founding Echo card; MVVM/modal
  screens; wave escalation + upgrade-tap + pack grant + dressed dungeon.
- **Minted-but-open WOs:** 750 (2 clip IDs), 752 Part B (copy sign-off + pet handoff), 753 (spec file), 754
  (Ad App Key), 755 (2 grant fixes + pricing), 756. Grok 715-722 stalled at **PAIRWALK_716** sign-off.
- **Blockers on owner:** `vercel` CLI (deploy), `/mcp` (Notion), Ad App Key, **push authorization**, prod
  promotion; design calls (dungeon-dressing breadth, dual-economy balance, WO-752 Part B, WO-755 pricing).
- ⚠ **WO-724→726 "CoC offense loop"** named as canon's highest lane but **no WO files exist on disk** — owner
  to confirm whether those numbers exist or need minting.

---
*Live anchor, all 17 domains landed. Committed local, push HELD. 07-19 bannered SUPERSEDED; load-bearing set
updated same-breath (§15). Still queued: the §6 catalog-drift fixes + §7 comment-lie fixes as a housekeeping
WO, and the CS-1 equipped-ring/amulet non-persist bug as a ticket.*
