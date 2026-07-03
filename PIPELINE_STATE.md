# Pipeline State — Defenders of the Realm v2 (ground-truth catalog seed)

Compiled by the build-connected CLI from the actual code (2026-05-29), to seed the
PM catalog. Status legend: **BUILT** (works) · **WIRED** (in-scene/connected) ·
**STUB** (class exists, no runtime) · **MISSING** (not started) · **SPEC** (doc only).

> **Reconciliation rule (recurring trap):** several systems are *more built* than their
> specs assume. Always check this doc + memory before writing a WO that "creates" something
> — the store, animation, and monetization were all near-complete already. Reconcile, don't duplicate.

---

## CURRENT STATE — 2026-06-28 (supersedes ALL blocks below; anchor now `CANON_GROUND_TRUTH_2026-07-03.md`)

> The 2026-06-09 and 2026-05-29 blocks below are kept for history. Where they conflict, **this block wins.**

- **2026-07-03 delta (see the anchor for full detail):** current focus = **THE FEEL ARC** (ten-year-old
  test is the quality bar); south vertical slice fleet-proven 6/6 (natural seam: raise→moat→water→bridge),
  owner felt-pass pending. Post-processing was structurally DEAD until 07-02 (null postProcessData) —
  fixed via WorldFeelInjector (`ff.worldfeel`, dusk palette) + terrain relief/treelines. **Tutorial V2
  BUILT** behind `ff.tutorialv2` (default OFF, 7 steps, tutorial-steps.json + interpreter + telemetry).
  Monetization reviewed 07-02 (`docs/MONETIZATION_REVIEW_2026-07-02.md`: Curiosity Shop; loot boxes
  NO-GO mainnet / GO testnet). Next headline = WO-545 Addressables streaming
  (`docs/WEBGL_DELIVERY_PLAN_2026-07-03.md`).

- **Branch:** `wip/village2-and-f8-tickets` (the `feat/tower-core-loop` name everywhere below is STALE).
  HEAD `7c05cd1b` (2026-06-28); nothing pushed this arc.
- **Title:** **"Echoes of Elarion"** (chapter) within the **"Defenders of the Realm"** series; tagline
  **"Hold the last light."** (WO-570).
- **V1 = single controllable Knight ("Grom")** in an overworld with an isolated real-time **BattleArena**
  (lock-on WO-512, 9-zone HUD). **ATB is separate/flat.** Base-defense + tower-defense = **V2-gated**
  behind `ff.basebuilding`. **Defend-the-Tower/PatriciaLight = REMOVED (2026-06-09)** — not a pillar.
- **Combat space (WO-584, READY 06-28):** one warp-in space primitive, 3 skins (dungeon/outpost/arena),
  resolver + ownership flip; replaces the flat ATB dungeon fight (`ff.atbdungeon` OFF). UI canon =
  `docs/UI_BLINK_TEMPLATE_CANON.md` (BINDING master-frame formula).
- **Hero = single Tripo self-rigged model**, static armor, no mesh-swap. **Blink hero rig JUNKED (06-22)**
  (Blink = UI re-skin only). Roster = Tripo only; V1 = Knight + ORCS.
- **World:** home = `MainCastle_Hall`; `OuterWorld` additive; `Village2` = raid target; `Village.unity`
  ABANDONED. Castle↔OuterWorld = **four-side warp gates** (RuntimeRegionGate); moat + 4 drawbridges
  (`ff.castlemoat`); tree aura + tower glow (`ff.hubambientvfx`).
- **Economy:** Echo workforce wired (1–4 echoes, offline real-clock, **save v27**); village-tier upgrade
  unlocks the WO-432 building-upgrade tree; store redesign (WO-501) + gear balance (WO-500).
- **Dialogue:** Yarn being DROPPED for custom MVVM dialogue (WO-455).
- **Distribution (updated 2026-07-03):** itch web build LIVE; **game IS live on Vercel** (the stale
  "Vercel parked/blocked" claim is WRONG — preview `defenders-of-the-realm-v2-69mafg5pj` = the full
  convergence build at 79.7MB Brotli data; production stays on the 07-01 verified Pi sign-in build
  until owner promotes). Pi backend = Cloudflare Worker.
- **WO numbering (updated 2026-07-03):** WO specs now run through **602** (596–602 = the 07-02→03 arc,
  e.g. WO-596 bug report, WO-602 return crossings); previously through **584** (authority = `MASTER_PIPELINES_BACKLOG_2026-06-06.md`
  + `CLI_LANES_WO_NUMBERS.md`, not filesystem max; the WO-560→584 arc = UI Blink template, title rebrand,
  dungeon/outpost/arena consolidation, wave-loop-in-hub).
- **In-flight (do NOT push):** HEAD targeting sweep `ff.enemystructureaware` is UNVERIFIED (0 sweep acquires).

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
| Black Dragon | → **Resources/Enemies/Dragon.fbx** (committed) | tracked | DTT air flyer + boss model |
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
- **Apex DragonBoss** (`Assets/_Modules/Village/Enemies/DragonBoss.cs`): own kinematic-flight class (NOT Enemy/NavMesh), 3-phase encounter, baked Fly take + code-driven dives. Prefab: `Assets/Prefabs/Village/Generated/Boss_Dragon.prefab`. **BUILT.**

## 4. Village / town — **SUPERSEDED (see 2026-06-09 block above)**
> ⚠ **Out of date.** The home/start hub is now **`MainCastle_Hall`** (the Castle, built by
> `Assets/Editor/CastleHubBuilder.cs`); the town role moved to **`Village2`**, repurposed as a
> raid-target stronghold. **`Village.unity` is ABANDONED** (never canonical, corruption-cursed —
> do not use). `OuterWorld` streams in additively over the castle hub via `WorldSceneLoader`.
> The detail below describes the old `Village.unity` flow and is frozen history.

Scene: `Assets/Scenes/Village.unity` (⚠ corruption-on-resave history — regenerate ONLY via the builder, never hand-save). Builder: `Assets/Editor/VillageSceneBuilder.cs` (`BuildVillage`).
- Wave loop `Assets/_Modules/Village/Waves/WaveManager.cs`: countdown→spawn→breach/clear→next. **BUILT.** Spawn-spread + stuck-enemy failsafe added (77984d9).
- 5 gameplay buildings on lightweight **polyperfect _M Medieval** prefabs (WO-101 Phase A, 4cf0037) — Tripo meshes shed (Seeker file-size win); store/interactable wiring on the plot root (dispatch by `Building.Type`) untouched. **Rebake VERIFIED loads (no level3 crash); rebake is SAFE.** ✅ Materials URP-converted via `PolyperfectUrpFix` (69 mats built-in→URP/Lit) — render correctly (confirmed). Note: polyperfect is gitignored, so re-run `Defenders/Art/Fix Polyperfect URP Materials` on a fresh clone.
- **WO-101 Phase B** (pending): walls/ground/roads/prop-dressing/nature + MarketplaceInteractor store-wiring (same builder/rebake).
- ✅ **wave-4 apex dragon FIXED** (4cf0037): rebake re-wires `_apexBossPrefab` + WaveManager has a Resources/Enemies/Boss_Dragon fallback for the stale scene. (Pending in-game confirm that Syndrath flies.)

## 5. Store / monetization — **~70% BUILT** (do NOT greenfield)
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
