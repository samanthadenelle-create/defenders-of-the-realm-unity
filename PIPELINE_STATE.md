# Pipeline State — Defenders of the Realm v2 (ground-truth catalog seed)

Compiled by the build-connected CLI from the actual code (2026-05-29), to seed the
PM catalog. Status legend: **BUILT** (works) · **WIRED** (in-scene/connected) ·
**STUB** (class exists, no runtime) · **MISSING** (not started) · **SPEC** (doc only).

> **Reconciliation rule (recurring trap):** several systems are *more built* than their
> specs assume. Always check this doc + memory before writing a WO that "creates" something
> — the store, animation, and monetization were all near-complete already. Reconcile, don't duplicate.

---

## CURRENT STATE — 2026-08-02 (supersedes ALL blocks below; anchor = `CANON_GROUND_TRUTH_2026-08-02.md`)

> The 2026-08-01, 2026-07-26, 2026-06-09 and 2026-05-29 blocks below are kept for history. Where they
> conflict, **the newest block wins.** Live anchor of current reality =
> `CANON_GROUND_TRUTH_2026-08-02.md` (delta over 08-01 → 07-26 → the deep `2026-07-22` module anchor).

- **2026-08-02 delta — marathon day 2 (HEAD `e60b19e5`, local==origin, pushed; 21 commits dated 08-02):**
  - **Gates:** `COMPILE_GATE_OK` + `REGRESSION_OK` + **EditMode 884/884, zero reds** + **`UI_CAPTURE_OK 28`**.
    **Ten new oracles** today: raid-deploy-ui, wallet-provider, hud-actionbar, echo-picker,
    dungeon-room-ownership, realm-map, dungeon-treasure, echo-card-layout, starter-loadout, shield-defense.
  - **Save schema = `v36`** (WO-834 `everBuiltStructureIds`, the blank-town baked standdown). Echo lane
    tokens moved to a `<resource>:<level>` grammar and are **read-migrated — no further bump.**
  - **SHIPPED:** WO-830/831 Echo harvest program (**affinity = a MATCH BONUS, never a lock** — the player
    picks each Echo's resource, a match doubles yield; **Maren harvests Crystals, not Repairs**) ·
    WO-835 action bar (`HudActionBarModel` = single enum-ordered authority, holes impossible by
    construction) · WO-839 raid deploy · WO-840 armorer anchor fix · WO-841 upgrade countdown ·
    WO-842/843/844 felt fixes · WO-797 + **WO-849** dungeon room-ownership + pursuit bound + exit beacon ·
    **WO-850** deepest-room treasure cache · **WO-766 real Solana wallet + the tester program** ·
    **WO-836 MASTER_CATALOG all 19 area files rewritten from code** · WO-852 Echo card fixed bands ·
    **WO-860** starter loadout = sword+shield · WO-861 Phase 0 · tower research ladder restored ·
    shields actually defend (defense ladder + level gating) · respawn now MOVES you.
  - **OPEN:** **WO-848** restore Android managed stripping Medium (at **Low** for the Solana SDK's
    BouncyCastle CIL-linker resolve) · WO-851 spec-only · WO-861 in flight · WO-862 minted ·
    WO-774.0 / WO-838 / WO-837 rulings captured, nothing built · `auth_nonces` table missing in Neon.
  - **⚠ APK precondition:** the Solana SDK is a **git-URL** package that re-resolves into
    `Library/PackageCache` — run `tools/android/patch-solana-sdk.ps1` (idempotent) before ANY APK build.
  - **`waves.json` D1 ruling is CLOSED** (owner 07-30): batches are inert, both `WaveDataTest` cases now
    assert EMPTY and a re-add FAILS.
  - **WO numbering: read the `CLI_LANES_WO_NUMBERS.md` banner.** TWO disjoint blocks in use — main line
    (CLI) and **860–899 reserved** (UI seat); each seat bumps its own row in the same edit as the mint.
  - ⚠ The working tree is **NOT clean** — an in-flight item-identity lane is uncommitted.

- **2026-08-01 delta:** WO-818 KayKit NPC bodies (all phases) + WO-826 Realm Map SHIPPED; owner ruling —
  bar Queues button RETIRED (Builders chip = the one entry, 6-face bar); ProjectSettings batching RCA
  closed; gates 3-green (COMPILE+REGRESSION 103 checks+UI_CAPTURE 23); HEAD `ac0a52e3`+ pushed; Seeker
  APK + Firebase + WebGL preview shipped. WO next-free = 832 (banner authority).

- **2026-07-26 delta — DUNGEONS FUNCTIONAL + RAID LOOP LOCKED (felt-test wave, pushed to `wip`, HEAD `7dec0e07`):**
  Dungeons are now a real end-to-end loop (enter → explore → read lore → fight with a REAL win/loss → settle →
  leave → Village) — WO-770.1/.2/.3/.3b/.4/.7/.9 **DONE** (real-time `BattleArena.OnBattleEnded` now settles the
  encounter; a lost fight ends the run; lore stones readable; toasts + live Bryn). Dungeon backlog:
  770.5/.6/.8/.10/.11 (dungeon camera = FPV default-on, `ff.dungeonfpv`). **Raid loop LOCKED to Teleport/Deploy**
  (COC model, owner ruling; walk-to retired, `ff.raidwalk` OFF) — **CORRECTED 2026-07-26: the V1 spine ALREADY
  EXISTS end-to-end and is REACHABLE** (train `TroopTrainingVM` → `ArmyStorage` → `RaidSelectionScreen` →
  `RaidDeployScreen` → `SceneRouter.GoRaid` → four `RaidBase_*` scenes → `RaidDeployController` +
  `TroopDeployer.SpawnFromArmy` → `TroopController` auto-fight → `RaidScoring` 180s/stars/loot +
  `RaidHudController`); `ff.barracks` + `ff.buildtimers` ON so the roster/timers are reachable. The old
  "nothing built yet" was STALE. **Raid V1 UX polish IN FLIGHT = WO-774** (loadout handoff — `GoRaid` still
  takes only a scene name, no `RaidParams` bag; Army/Deploy naming split; deploy ring; "Defenders %" copy;
  Train-queue UI). Spine map + ladder = `docs/RAID_NORTHSTAR.md` §2A. **WO-773 (multi-channel Obsidian job
  queue: Builder/Train/Research) = SHIPPED** (`Core/Jobs/*`, schema **v35** via `SaveMigrator.MigrateToV35`).
  **WO-772 (shared enemy system) = UNBLOCKED** — Hollow Ones ratified, Wildlands deferred (PAIN_POINTS §1.1);
  `EnemyResolver` + barracks-catalog-structure **IN FLIGHT** (not done). Non-dungeon fixes: enemies-out-of-castle + battle-lock,
  towers-no-longer-through-walls, MagentaGuard Android, loading overlay+bar, gate-teleport off, collector vendor
  NPCs, Alchemy scroll-fix. WO next-free: see the `CLI_LANES_WO_NUMBERS.md` banner (**832** as of
  2026-08-01). Ticket table: `docs/qa/SUNDAY_STATUS_2026-07-26.md`.

- **2026-07-16 delta — BARRACKS 7-TROOP TIER-UNLOCKED ROSTER (program WO-732→737, VERIFIED):** the army is a
  **7-type roster trained at the Barracks**, gated by Barracks building tier — Footman + Archer day-one (tier 1),
  then Spearman (T2) · Shieldguard (T3) · Outrider (T4) · Battlemage (T5) · Echo Legionnaire (T6). Data-driven
  (`troops.json` `unlockBarracksTier` + `TroopUnlock` gate; barracks `building-tiers.json` T2–6 announce each unit);
  headless `TroopRosterRegression` (in `DataRegression.RunAll`) locks the ladder + dual-copy. **Not** "two troops
  forever." (`ff.barracks` default unchanged; CoC raid consume-loop = WO-726, separate.)

- **2026-07-13 delta — STRATEGIC PLACEMENT LOCKED ON (WO-695, ex-682 "strategic placement lock on"):**
  `ff.strategicplacement` is **REMOVED from the codebase** — the WO-673 player-defined town (Build →
  Town/Defenses/Walls palette tabs, movable functional storefronts, 260w/210i core-kit new-game seed,
  baked-storefront/vendor/collector standdown) is **ALWAYS ON in every build**. New Game = the **blank
  template** (marker set in ResetToNewGame; authored shell + zero functional buildings except the FTUE
  grace-default Forge record, WO-695 option b). Existing saves still migrate once via the v30 one-shot
  writer (marker-false path). DevPanel/OwnerDevToolsOverlay toggle rows removed.

- **2026-07-12 delta (see `CANON_GROUND_TRUTH_2026-07-12.md`):** focus = the **mobile-web demo
  wave**. **WO-677** (touch verb bar — uGUI rebuild) + **WO-678** (Pi SDK timeout clean wrap) + **WO-682**
  (quiet web errors: 13 Sfx WebGL-override metas swept, db-proven FSB decode root; audio prewarm on battle
  load; `SFX_WEBGL_OK` oracle) + **WO-683** (build-screen kit — d-pad moves the asset, text rotate labels)
  all committed **LOCAL, push HELD**. Save schema **v29**; gates green (3 known pre-existers only). The
  **WebTrace web-debug loop is PROVEN** (`?trace=1` → `api/trace` → Neon; CLI reads the runtime-log `[sig]`
  echo); `api/` lives in-repo and is git-TRACKED (25 files). A new **ship (non-dev) WebGL preview** was deploying at handoff;
  prod untouched. **WO numbering: next free = 684** (677/678 collisions flagged).

- **2026-07-08 delta (⚠ superseded by the 07-12 delta above — see `CANON_GROUND_TRUTH_2026-07-08.md` for full detail):** focus = THE FEEL
  ARC run through the **F8 ticket program**. **P0 "still cant do the tower" FIXED + owner felt-confirmed**
  (dialogue Closed re-entrancy froze build-mode input; per-VM Closed guard `82422d11`). The 07-07/08 F8
  board (30+ tickets) is fixed/spec'd/evidence-pinned (white-Paladin root fix, F8-24 stairs sweep, dialogue
  FrameCore rebuild, "Tap to continue" hint, scatter enemy families, tower identity, combat HUD v8 ON).
  **Wave 2 CLOSED** on exe **2026-07-08 05:10:11** (fleet ZERO tickets); fresh HEAD gates `COMPILE_GATE_OK`
  + `REGRESSION_OK`. Branch `wip/village2-and-f8-tickets`, HEAD `d944d161`, **71 ahead, push HELD**. Save
  schema **v28**. **WebGL preview `h0h6hfsf5`** (prod untouched, 07-01 Pi build). Next big lane = **WO-614
  skill-tree solo rework** (RULED, READY). Open owner directives: F8-40 max-tier tower identity / F8-41
  waves attack the city / F8-42 repair costs. Pre-existers: WO-602, CavePortal seam, WO-453.

- **2026-07-03 delta (⚠ superseded by the 07-08 delta above; see the anchor for full detail):** current focus = **THE FEEL ARC** (ten-year-old
  test is the quality bar); south vertical slice fleet-proven 6/6 (natural seam: raise→moat→water→bridge),
  owner felt-pass pending. Post-processing was structurally DEAD until 07-02 (null postProcessData) —
  fixed via WorldFeelInjector (`ff.worldfeel`, dusk palette) + terrain relief/treelines. **Tutorial V2
  BUILT** behind `ff.tutorialv2` (default OFF, 7 steps, tutorial-steps.json + interpreter + telemetry).
  Monetization reviewed 07-02 (`docs/MONETIZATION_REVIEW_2026-07-02.md`: Curiosity Shop; loot boxes
  NO-GO mainnet / GO testnet). Next headline = WO-545 Addressables streaming
  (`docs/WEBGL_DELIVERY_PLAN_2026-07-03.md`).

- **Branch:** `wip/village2-and-f8-tickets` (the `feat/tower-core-loop` name everywhere below is STALE).
  HEAD `ac0a52e3`+ (2026-08-01), local==origin, PUSHED.
- **Title:** **"Echoes of Elarion"** (chapter) within the **"Defenders of the Realm"** series; tagline
  **"Echoes of a Forgotten Civilization"** (owner 2026-07-24; "Hold the last light" retired).
- **V1 = single controllable Knight ("Grom")** in an overworld with an isolated real-time **BattleArena**
  (lock-on WO-512, 9-zone HUD). **ATB is separate/flat.** Base-defense + tower-defense = **V2-gated**
  behind `ff.basebuilding`. **Defend-the-Tower/PatriciaLight = REMOVED (2026-06-09)** — not a pillar.
- **Combat space (WO-584, READY 06-28):** one warp-in space primitive, 3 skins (dungeon/outpost/arena),
  resolver + ownership flip; replaces the flat ATB dungeon fight (real gate `ff.dungeonrealtime`, default ON). UI canon =
  `docs/UI_BLINK_TEMPLATE_CANON.md` (BINDING master-frame formula).
- **Hero = single Tripo self-rigged model**, static armor, no mesh-swap. **Blink hero rig JUNKED (06-22)**
  (Blink = UI re-skin only). Roster = Tripo only; V1 = Knight + ORCS.
- **World:** home hub **`Main_Castle_Overworld`** (merged world, one navmesh); `Village2` = raid target;
  `Village.unity` + `OuterWorld.unity` DELETED from the tree.
- **Economy:** Echo workforce wired (offline real-clock, save **v36**); village-tier upgrade
  unlocks the WO-432 building-upgrade tree; store redesign (WO-501) + gear balance (WO-500).
  **Echo harvest affinity is a MATCH BONUS, never a lock** (WO-830, 2026-08-02): the player picks each
  Echo's harvest resource; matching its affinity doubles the yield. Token grammar `<resource>:<level>`.
  Maren harvests **Crystals**, not Repairs.
- **Dialogue:** Yarn being DROPPED for custom MVVM dialogue (WO-455).
- **Distribution (updated 2026-07-03):** itch web build LIVE; **game IS live on Vercel** — production = the 07-16
  six-fix build `q2v5vj86g` (promoted 2026-07-16); WebGL preview refreshed 2026-08-01; promotion
  stays the owner's. Pi backend = Cloudflare Worker.
- **WO numbering (updated 2026-07-03):** WO specs now run through **602** (superseded — banner says 832) (596–602 = the 07-02→03 arc,
  e.g. WO-596 bug report, WO-602 return crossings); previously through **584** (authority = `MASTER_PIPELINES_BACKLOG_2026-06-06.md`
  + `CLI_LANES_WO_NUMBERS.md`, not filesystem max; the WO-560→584 arc = UI Blink template, title rebrand,
  dungeon/outpost/arena consolidation, wave-loop-in-hub).
- **In-flight (do NOT push):** HEAD targeting sweep `ff.enemystructureaware` is UNVERIFIED (0 sweep acquires). *(retired 2026-08-01 — tree is pushed)*

---

## CURRENT STATE — 2026-06-09 (⚠ SUPERSEDED by the 2026-06-26 block above — kept for history)

> The sections below this block are a 2026-05-29 snapshot kept for history. Where they
> conflict with this block, **this block wins.** Notion "Work Orders" is the source of truth
> for WO status (per `NOTION_SOURCE_OF_TRUTH.md`); this doc mirrors it.

- **Start / home hub = `MainCastle_Hall`** (the Castle hub). Routed via `SceneRouter.GoCastle()`;
  both onboarding and returning-player route there. Built entirely from script by
  `Assets/Editor/CastleHubBuilder.cs`. Ground level is owner-verified walkable; the L2 ramp +
  castle camera are pending playtest.
- **`Village2` = raid-target stronghold** (repurposed from the old town). Built by
  `EnemyStrongholdBuilder.Build()` (menu `Defenders/World/Build Village2 Enemy Stronghold`);
  garrison enemies come from `garrison-recipes.json` recipe `village2_stronghold` →
  `GarrisonController` → `EnemyFactory` (NOT `waves.json`). 2026-06-27: garrison de-skeletoned
  (now all-orc warband + troll) and walls rebuilt as a **flat-wall maze with chokepoints**
  (functional V1; flat box walls carve the navmesh cleanly so the input-driven NavMeshAgent
  hero can't tunnel them; difficulty design deferred). **`Village.unity` = ABANDONED** — it was
  never canonical and is corruption-cursed; do not use it.
- **`OuterWorld` streams in additively** over the castle hub via `WorldSceneLoader` (which lists
  `MainCastle_Hall` as a hub). Both `MainCastle_Hall.unity` and `OuterWorld.unity` are in Build Settings.
- **Castle ↔ OuterWorld seam: WIRED** (commits `b3b5cef`, `9c8c64f`, `e213e25`, `53640cf`).
  Live bug — the south-gate transition trigger teleports the hero to `(0,0.5,-80)`, past
  HeroLocomotion's ±50 off-mesh clamp → hero stranded + camera snap-back. Tracked as
  **WO-383 (Status: Ready, ACTIVE NOW)**.
- **Defend-the-Tower / PatriciaLight = REMOVED (2026-06-09).** The module folder + the
  `PatriciaLightMode` scene are gone and unreachable; only the `Resources/PatriciaLight/tower2`
  asset was kept. It is **no longer a built pillar** — any older line below calling it BUILT is wrong.
- **WO numbering:** highest WO is now **383**; **next free WO = 384.** Recent batch WO-358–373 = Done.
  WO-332/333/334 = marked Done in Notion but their Notes say "pending Tricia play-mode visual confirm"
  (treat as Done-but-playtest-pending). **Board-hygiene gap (open):** WO-380 & WO-382 (and likely the
  376–382 range) have repo `*.RESULT.md` files but are MISSING from the Notion board — not yet resolved.
- **Branch:** `feat/tower-core-loop`; 5 unpushed art/meta/doc commits; not merged to master (711 ahead).

---

## ☀️ MORNING SUMMARY — overnight run complete (read first)

**Tree state: GREEN ✅ and locked.** Last night's "red marathon" root cause was found + fixed: a flaky
**Linux-mount ↔ Windows sync** was truncating/duplicating files on the build side (NOT UI code quality —
see memory `mount-sync-corruption`). Recovered by truncating the mount-duplicated tails. The full green
build is committed + pushed — restore points **`00b1662` / `8e4fd35`**.

**🎮 Fresh build ready to play** (`Builds/Windows/DefendersOfTheRealm.exe`, boots to Village): the village
with WO-105 building repositions baked in, plus the dev portal — press **F1** / tap **DEV** for live
metrics, **+10,000 XP**, and **"Set hero to level N"**. Tricia's Defend-the-Tower is in there too.

**Overnight CLI run (each green-gated + committed):**
- ✅ **WO-103** village rebake — WO-105 repositions in the scene, **0 gate-clearance violations**.
- ✅ **Set-level dev tool** (`6149cf2`).
- ✅ **WO-122 §A** — `CrystalMine._useExternalVisual` flag, code half (`bde5080`).
- 🎉 **WO-123 WebGL build SUCCEEDED** — `Builds/WebGL/` (186 MB Brotli, 18.5 min). **Testers can get a browser link.**
- 🧹 Queue cleanup — my WO collisions deduped (→ 120/121/122), WO-123 slotted (`55d3fd5`).

**🌐 READY FOR YOU — host the web build for testers** (`docs/webgl-hosting-notes.md`): **itch.io recommended**
(handles 186 MB; Vercel may reject the 174 MB `.data.br`). `vercel.json` (Brotli headers) pre-placed in
`Builds/WebGL/`. Zip → upload → relay me the URL/errors → I adjust. Pairs with the dev portal (F1) for the `docs/qa` cases.

**⏸ Parked for you (need your eye — not safe to do blind):**
- **WO-104 castle + moat** — big *visual* build; placement needs your judgment. Best done together.
- **WO-110 scene wiring** — crystal-mine placement is *provisional* (NW was open space); finalize after WO-104.
- **WO-22** store re-enable (own PanelSettings + code-built UI).

**⚠ Coordination:** UI must stay **OFF** the shared tree — the mount-sync corrupts its writes; CLI on
Windows owns build-affecting files (rule in CLAUDE.md + memory `mount-sync-corruption`).

**ℹ️ Minor note:** `Assets/Black Dragon/` (source folder) shows **deleted in the working tree** —
pre-existing, NOT from the overnight run, **recoverable** (HEAD still has it). The *gameplay* dragon is
unaffected (it loads the `Resources/Enemies/Dragon.fbx` + `Boss_Dragon` copies, both present). If a
cinematic/preview needs the source, restore with `git checkout HEAD -- "Assets/Black Dragon"`. Left as-is
(didn't commit the deletion or undo it — your call, possibly an intentional lean-checkin cleanup).

**📜 Vision docs (committed):** `docs/NORTH_STAR.md` (vision/business/GTM source of truth — incl. delivery
ladder, Pi utility-sink, 3-build distribution, rewarded-ads pillar), `docs/PI_PITCH.md`,
`docs/ARCHITECTURE_NORTH_STAR.md`, WO-120 (backend reconcile) / 121 (web metrics) / 122 (crystal mine) / 111 (resource pillar).

---

## 0. How the pipeline works
- **Roles:** Claude UI = specs / work orders / logic + routing. The **CLI (this side) = writes + build-verifies all code.** Owner = PM, catalogs + routes, makes creative calls.
- **Asset packs:** owner imports Asset Store packs in the Unity editor (CLI can't); CLI integrates. Big packs are **gitignored** (re-import on clone); only the specific used assets are committed.
- **Build/bake (CLI, batchmode — refuse if the editor is open):**
  - `build-windows.ps1` → Win64 player (`Builds/Windows/…exe`); wipes output first (stale-exe crash guard).
  - `run-unity-method.ps1 -Method <X> -LogName <Y>` → runs an editor static method (bakes, animator builds, etc.).
  - Launch a scene directly: `…exe -bootScene <SceneName>` (e.g. `PatriciaLightMode`, `Village`).
  - 505 license line is transient; judge by the success marker, not the exit code.

## 1. Asset packs
| Pack | Location | Git | Use |
|---|---|---|---|
| KayKit Skeletons 1.1 | Assets/Models/KayKit (gitignored) → chars copied to **Resources/Enemies** (committed) | mixed | Enemy horde meshes + URP atlas |
| KayKit Character Animations 1.1 | Assets/Models/KayKit (gitignored) | ignored | Shared Rig_Medium/Large clip library |
| KayKit Medieval Hexagon | Assets/Models/KayKit (gitignored) | ignored | Village dressing + walls/gates/ground |
| Black Dragon | → **Resources/Enemies/Dragon.fbx** (committed) | tracked | DTT air flyer + boss model (⚠ 2026-08-01: Dragon.fbx git-rm'd — replaced by the licensed Asset-Store dragon `Assets/Dragon/` + `Resources/Enemies/Boss_Dragon.prefab`, WO-760) |
| Lean Touch (CW) | Assets/Plugins/CW (committed, examples trimmed) | tracked | Mobile gestures |
| Low Poly Ultimate Pack (polyperfect) | Assets/polyperfect (**gitignored**, 246MB) | ignored | Arena props (siege engines) + future village rebuild |

## 2. Defend-the-Tower (PatriciaLight) — **REMOVED (2026-06-09)**
> ⚠ **No longer a built pillar.** The PatriciaLight module folder + the `PatriciaLightMode` scene
> have been removed and are unreachable; only `Resources/PatriciaLight/tower2` was kept. The detail
> below is frozen history — disregard its **BUILT** markers.

Scene: `Assets/Scenes/PatriciaLightMode.unity` (baked by `Assets/Editor/PatriciaLightSceneBuilder.cs`). Director: `Assets/_Modules/Village/PatriciaLight/PatriciaLightController.cs`.
- Manual aim-assist combat — `TowerAimSystem` (input-agnostic reticle+target) + `LeanTouchAimDriver` (drag=aim, hold=fire, pinch=zoom) + desktop mouse/keyboard fallback. **BUILT.**
- HUD (code-built UIDocument): tower HP bar (top), circular cooldown rings (Painter2D), color-coded damage pops (cyan hero / green pet via `IDamageTintable`), pet Engage/Repair toggle, FX overlays. **BUILT.**
- Abilities resolve at the crosshair; heal/ward repairs the tower (`HeroAbilities.AimPointOverride` + `HealHandler`). **BUILT.**
- Enemies: KayKit skeleton families (HollowRoster: Walker/Warrior/Rogue/Caster/Brute, wave-gated, stat/size/speed-scaled) + Dragon flyers; spread formation; animated via factory. **BUILT.**
- Arena dressed with polyperfect siege engines + warmer ground. **BUILT.**
- Camera: `HeroOverShoulderCamera` (locked facing, pinch-zoom hooks). **BUILT.**

## 3. Enemy + animation pipeline — **BUILT**
- `Assets/Editor/AnimatorSetup.cs` → builds shared controllers (HumanoidEnemy / LargeEnemy / Boss / Hero / Pet / Npc) into `Assets/Generated/Animators/` (gitignored) from the Character Animations 1.1 library. Canonical (docs/enemy-codex.md §5).
- `EnemyAnimatorSetup.cs` (DTT) → skeleton avatars + copies controllers to Resources/Enemies for runtime load.
- `EnemyAnimatorFactory.Apply(mesh, modelName)` → picks the shared controller by rig family.
- `VisualFactory.Skin(host, key, SkinOptions)` → load→fit→seat→fix-materials→strip-colliders (Enemy/Structure/Prop presets).
- `Enemy.cs` already drives Speed/Attack/Hit/Dead → any rigged mesh walks/attacks/dies for free.
- **Apex DragonBoss** (`Assets/_Modules/Village/Enemies/DragonBoss.cs`): own kinematic-flight class (NOT Enemy/NavMesh), 3-phase encounter, baked Fly take + code-driven dives. Prefab: `Assets/Prefabs/Village/Generated/Boss_Dragon.prefab`. **BUILT.** (⚠ 2026-08-01: that prefab was git-rm'd in the WO-760 licensed-dragon swap — the live prefab is `Assets/Resources/Enemies/Boss_Dragon.prefab`)

## 4. Village / town — **SUPERSEDED (see 2026-06-09 block above)**
> ⚠ **Corrected 2026-08-01.** Home hub = **`Main_Castle_Overworld`** (merged world, one navmesh).
> `OuterWorld.unity` + `Village.unity` are DELETED. `MainCastle_Hall.unity` exists on disk but is
> NOT the hub. The detail below describes the old `Village.unity` flow and is frozen history.

Scene: `Assets/Scenes/Village.unity` (⚠ corruption-on-resave history — regenerate ONLY via the builder, never hand-save). Builder: `Assets/Editor/VillageSceneBuilder.cs` (`BuildVillage`).
- Wave loop `Assets/_Modules/Village/Waves/WaveManager.cs`: countdown→spawn→breach/clear→next. **BUILT.** Spawn-spread + stuck-enemy failsafe added (77984d9).
- 5 gameplay buildings on lightweight **polyperfect _M Medieval** prefabs (WO-101 Phase A, 4cf0037) — Tripo meshes shed (Seeker file-size win); store/interactable wiring on the plot root (dispatch by `Building.Type`) untouched. **Rebake VERIFIED loads (no level3 crash); rebake is SAFE.** ✅ Materials URP-converted via `PolyperfectUrpFix` (69 mats built-in→URP/Lit) — render correctly (confirmed). Note: polyperfect is gitignored, so re-run `Defenders/Art/Fix Polyperfect URP Materials` on a fresh clone.
- **WO-101 Phase B** (pending): walls/ground/roads/prop-dressing/nature + MarketplaceInteractor store-wiring (same builder/rebake).
- ✅ **wave-4 apex dragon FIXED** (4cf0037): rebake re-wires `_apexBossPrefab` + WaveManager has a Resources/Enemies/Boss_Dragon fallback for the stale scene. (Pending in-game confirm that Syndrath flies.)

## 5. Store / monetization — **~70% BUILT** (do NOT greenfield)
> ⚠ SUPERSEDED — the live store model = the WO-673/695 player-built town (strategic placement ALWAYS ON,
> movable storefronts + vendor NPCs). This section describes the dead Village.unity path; kept for history.

Specs: `docs/monetization-v2-spec.md` (locked), `WORK_ORDER_73/75`, `CC_MONETIZATION_RECONCILIATION.md`.
- `PackStore.cs` + `PackCatalog.cs` + `packs.json` (5 packs) — **BUILT** (devnet-stubbed payments).
- `MarketplaceInteractor.cs` — village [F] store-open trigger — **BUILT, not placed in scene**.
- `WalletService.cs` (SOL/USDC/SKR, devnet-stub), `GlimmerCurrencyService.cs`, `CosmeticShopPanel.cs` — **BUILT.**
- **MISSING:** scene wiring (Marketplace + store UI into the village), `CosmeticApplier`, `BattlePass` runtime (`BattlePassData.cs` = STUB), glimmer-grant in packs (1-line). UXML render risk in builds.
- ⚠ **Store scene-wiring ATTEMPTED then DISABLED.** `BuildMarketplace` placed the Marketplace + a PackStore UIDocument in the village, but walking up opened the **wrong panel (hero talent tree)** + a blank store: PackStore's UIDocument grabbed the **SHARED PanelSettings** (multiple UIDocuments on one panel render by sortingOrder) and its **UXML came up empty in the build**. The `BuildMarketplace` call is commented out in `VillageSceneBuilder.cs` (method kept). **Re-enable only after PackStore gets its OWN PanelSettings + a code-built UI** (not UXML-template-driven) — same trap as BattleHUD/BuildMenu (see memory: uxml-uidocuments-dont-render-in-builds).

## 6. Spell book — targeting **BUILT**, creative spells **SPEC**
- Fire/cast now resolve at the crosshair; backward-shots fixed; heal→tower. **BUILT.**
- Creative roster (Rage team-buff, slow-all, DoT zone, fireball-freeze) + the prerequisite "enemies act on slow/freeze" — **SPEC** (`SPELL_BOOK_DESIGN.md`).

## 7. Open backlog (ideas → WOs)
Wave-4 apex fix (#20) · DTT camera look-down + mobile on-screen buttons (#19) · village redesign (polyperfect buildings/walls) · store scene-wiring · compass target-triangles · "village under siege" wave affordance · enemy role behaviors + wave composition (`ENEMY_WAVE_DESIGN.md`) · idle/economy pillar (resource gathering + pet auto-harvest + offline) · air-dragon flap.

## 8. Design docs / roadmaps in-repo
`SPELL_BOOK_DESIGN.md` · `ENEMY_WAVE_DESIGN.md` · `docs/enemy-codex.md` · `docs/monetization-v2-spec.md` · `docs/port-notes/animation-setup.md` · `CC_MONETIZATION_RECONCILIATION.md` · WORK_ORDER_*.md.
