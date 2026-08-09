# MASTER CATALOG — Project Index

> # ▶ DELTA 2026-08-09 — read this before the banners below
> **Live anchor = `../CANON_GROUND_TRUTH_2026-08-09.md`** (every "live anchor" reference further down this
> file naming 08-02, 08-03 or 08-06 is stale). **HEAD `c8320434`, PUSHED — local == origin, 0/0.**
> Save schema **v37** (`SaveSchema.cs:36`). ⚠ **Read every gate count off the marker file, never off this
> doc** — the three entry points emit DISTINCT markers (`REGRESSION_OK` / `CHECKIN_SUITE_OK` /
> `SESSION_GUARDS_OK`), and the newest run's markers are named in the anchor's gate block.
>
> **⚠ THE BODY OF THIS INDEX IS A FILENAME LIST ONLY — the `docs/MASTER_CATALOG/<area>.md` files are the
> trustworthy layer.** §1–§3 below were never refreshed by WO-836 and remain known-stale in a way that
> needs a real code-verified pass; they were deliberately **not** touched by the 2026-08-09 canon
> re-anchor. Use them to find a file, never to assert a fact.
>
> **Moved on 2026-08-08 — read the anchor, not this index:** the **dungeon stairs are SOLVED** (WO-930,
> `3ab1bfb6` → `cb092b7f`; all 4 content dungeons `PathComplete`; root cause was `SolveMate` hardcoding
> `yaw = 0f` on vertical sockets) · **structure orientation** now has a per-catalog-row
> `RepoProps.preservePrefabRotation` (default false, one opt-in) after a global apply laid the town on its
> side — **headless gates cannot see orientation** · **store purchases are re-gated OFF and locked**
> (WO-931, `StubWalletProvider` free-grant hole) · the desktop player now ships **RELEASE**.
>
> **Areas that moved on 2026-08-05/06 — read the anchor + the area file's own dated delta, not this index:**
> - **VFX (the night's headline, two P0s):** `IsLoop` was a hand-authored sticky checkbox, **53 of 122 picks
>   wrong**, and a fire-and-forget loop **permanently consumed one of the 20 global slots** — the archer and
>   ballista were starving the whole VFX budget. Both catalog generators now DERIVE it, with **standing
>   owner rulings PINNED above the derivation**. Separately, `CopyAsset` copies the **prefab only**, so
>   **27 of 28 tracked VFX prefabs / 183 references** pointed into gitignored art; now 0, with ~23.85 MB
>   mirrored to `Assets/Resources/VFX/_Shared/`. ⚠ **`VFXType` serialises by ORDINAL — appends only.**
>   ⚠ **`Build()` does `entries.arraySize = rows.Count`** — a row written only by a builder is silently
>   dropped by the next regenerate. See `resources-art.md`.
> - **Hero:** `ff.knightonly` defaults **OFF** — roster Knight/Ranger/Mage. Any area file still saying
>   "dormant under knight-only" is stale. A **latent invisible-hero P0** is closed (Ranger/Mage had no FBX,
>   fell to a **gitignored** Blink body, and instantiated **nothing** on a fresh clone). **WO-910: their
>   talent trees are effectively empty — 31 dead nodes; READY FOR OWNER RULING.** See `village-hero.md`.
> - **Structures:** one owner-ruled **height cadence** — 1.25 landmark / **1.2 towers** / 1.0 base / 0.75
>   siege / 0.35 decoration, recorded in the data as `_heightCadence`, **catalog v8** (6→7 archer, 7→8 cadence). **Walls deliberately
>   excluded** (narrowing opens pathable gaps in saved wall runs). Any "towers 1.25" line is stale.
>   See `village-systems.md` + `data-catalogs.md`.
> - **Accessibility:** the low-health tell is **no longer a red vignette** — pulse rate, guttering depth and
>   a recipe swap below a quarter health. Shape and timing, never hue.
> - **Session ledgers:** `reference/SESSION_INDEX_2026-08-06.md` (incl. every REFUTED belief) and
>   `reference/DEFECT_INDEX_2026-08-05.md` (frozen).

> # ⚠ THIS INDEX FILE WAS **NOT** REFRESHED BY WO-836 — use it as a FILENAME LIST ONLY (flagged 2026-08-03)
> WO-836 rewrote the **19 section files** under `docs/MASTER_CATALOG/`. **It did not rewrite this file's own
> body.** §1–§3 below are ~2026-07 fiction and contradict both the section files and the live anchor:
> Village-Hero "Blaise + class bodies" · NPCs "party-of-4" · Enemies/World "OuterWorld streaming"
> (that scene is DELETED) · Dialogue "64 `.yarn` nodes + vendored Yarn" (Yarn is FULLY REMOVED, WO-557)
> · `SaveSchema CurrentVersion=30` (it is **36**) · "next free WO = 412" (**never trust a copied number — read the
> `CLI_LANES_WO_NUMBERS.md` banner; corrected 2026-08-06, the 853/863 figures previously printed here
> were themselves stale**) · EconomyService "4-resource wallet" (5 with Coins) · `ZoneManager` village ±42/±33 (actual
> **52/52** — the 42/33 figure mis-classifies the courtyard and IS the 07-26 "enemies inside the castle" bug).
> Several §3 ledger rows are also closed (Aegis set reachable, the six WebGL-broken catalogs pinned,
> Settings/Pause UXML converted, `HUDManager`/`VirtualDPadLean` deleted, backend live, OuterWorld gone).
>
> **⚠ And the 19 section files are code-true as of `b77a178e` (2026-08-02 morning), NOT current HEAD** —
> ~20 commits landed after that fleet ran. Known drift: `economy-meta.md` says WO-830 is "spec only, NOT in
> code" (it shipped) · `docs-wo-state.md` says save v35 / next-WO 836 · `resources-art.md` says the KayKit
> bodies have no Animator wiring (WO-833 shipped it) · `village-npcs.md` documents the
> `"Forge"`→`"Blacksmith"` anchor mapping as correct (that mapping **was** the WO-840 bug).
>
> **Read order that actually works** *(anchor corrected 2026-08-06)*: `CANON_GROUND_TRUTH_2026-08-06.md` → `KEY_FACTS.md` →
> the `CLI_LANES_WO_NUMBERS.md` banner → the specific `docs/MASTER_CATALOG/<area>.md` → `CLAUDE.md` →
> `docs/ARCHITECTURE_PRINCIPLES.md` → the newest `docs/HANDOVER.md` block.

The single master index a new session reads to understand the whole project
**without operating on assumptions**. Each area below has a deep section catalog
under `docs/MASTER_CATALOG/<id>.md`, verified file-by-file (read, not from comments).

> ## ✅ FULL SME REFRESH 2026-08-02 (WO-836 — the owner-ordered 14-agent fleet)
> **ALL 19 section catalogs under `docs/MASTER_CATALOG/` were REWRITTEN 2026-08-02, verified from code
> at HEAD `b77a178e`+** (file:line cites; comments-lie law applied; per-file inventory + seams + risk
> ledger each). The 07-22 §6 catalog-drift ledger is PAID for every area. Read the section files
> directly — they are current. **Live anchor = `CANON_GROUND_TRUTH_2026-08-06.md`** (corrected 2026-08-06).
> Fleet risk roll-up: see the **★★ SESSION HANDOVER — 2026-08-02** block in `docs/HANDOVER.md` (no longer
> the newest — the newest is **2026-08-06**).
> **Any banner below that calls the `<area>` files "2026-06-12-stale" is SUPERSEDED by this line.**

Section catalogs compiled **2026-08-02** (previously 2026-06-12; the stale-framing banner below is
retained for history only — the section files no longer carry the pre-pivot framing).
Current branch = **`wip/village2-and-f8-tickets`**.

> ⚠ **HISTORICAL (2026-06-12, pre-pivot — no longer describes the section files):** the old section files
> described the hero as **"Blaise"
> + Blink/class bodies** and a **party-of-4** — both SUPERSEDED by the 06-22 single-Knight pivot (hero =
> single Tripo self-rigged "Grom", Blink hero rig JUNKED, everything else autonomous). For LIVE state read
> `CANON_GROUND_TRUTH_2026-06-26.md` + `docs/COMBAT_PIVOT_NORTHSTAR.md`. The per-area code mechanics below
> remain trustworthy; the hero-identity / party / Defend-the-Tower framing does not.

> ⚠ **SUPERSEDED 2026-08-02 by the WO-836 refresh banner at the top of this file — the two notes below
> are kept for history ONLY. The `<area>` files are NOT 06-12-stale and `misc-modules.md` is NOT
> "doubly stale"; all 19 were rewritten from code on 2026-08-02. Do not act on the fix-lists below.**
>
> ~~STALE: 2026-07-26 — the live anchor is now `CANON_GROUND_TRUTH_2026-07-26.md` (delta over the deep `2026-07-22` module anchor); HEAD is `7dec0e07`, local==origin. The `docs/MASTER_CATALOG/<area>.md` section files below are still dated **2026-06-12** — their "how it works" mechanics are largely accurate but their COUNTS + STATE facts are weeks stale (fix-list = the 07-22 anchor §6 catalog-drift ledger + §7 comment-lie registry). **`misc-modules.md` (Dungeons) is doubly stale** — it predates the RoomForge pipeline AND the 07-26 dungeon functional-loop wave (WO-770.1/.2/.3/.3b/.4/.7/.9: exits, correct-return, real win/loss, real-time settle, readable lore, toasts, live Bryn). Trust the 07-26 anchor for live dungeon/raid state.~~
>
> ~~STALE: 2026-07-12 — the live anchor is now `CANON_GROUND_TRUTH_2026-07-12.md` (the 06-26 anchor below is superseded), and HEAD is `f123859d`, not `8aa24c32` (see CANON_GROUND_TRUTH_2026-07-12.md)~~

> READ ORDER for a cold start *(anchor + handover dates corrected 2026-08-06)*:
> **`CANON_GROUND_TRUTH_2026-08-06.md`** (live anchor) → this file → the
> relevant section file → `CLAUDE.md` (binding rules) → `docs/ARCHITECTURE_PRINCIPLES.md` (architecture
> law) → `docs/HANDOVER.md` (**newest = the 2026-08-06 block**). Trust the ground-truth anchor + newest
> handover for live state; trust the section files for "how it actually works."

---

## 1b. NEW SYSTEMS SHIPPED SINCE THE 06-12 CATALOG (added 2026-07-26 — not yet folded into the area files)

> These systems postdate the 2026-06-12 section-file compile. Catalogued here (from code, at HEAD) so the
> index stays green (§15). **⚠ The "the area-file bodies are still 06-12 and do not mention them" caveat
> this section was written under is SUPERSEDED — WO-836 (2026-08-02) rewrote all 19 area files from code,
> so they DO cover these systems now; this section is a summary, no longer the only record.** State legend:
> **SHIPPED** = present + wired · **IN FLIGHT** = present but not asserted done.

**Raid V1 spine — SHIPPED, reachable end-to-end** (CoC deploy-and-watch; `ff.raidwalk` OFF, `ff.barracks` +
`ff.buildtimers` ON). Full beat→class map + P0/P1/P1.5/P2 ladder = `docs/RAID_NORTHSTAR.md` §2A. Classes:
- `TroopTrainingVM` (`Assets/_Modules/Village/Hero/TroopTrainingVM.cs`) + `TroopTrainingPanel` — train UI/queue.
- `ArmyStorage` (`Assets/_Modules/Core/State/ArmyStorage.cs`) — housing cap + perk + veterancy.
- `RaidSelectionScreen` + `RaidSelectionVM`, `RaidDeployScreen` + `RaidDeployVM` (`Assets/_Modules/Village/Hero/`) — pick target + pre-raid.
- `SceneRouter.GoRaid(sceneName)` (`Assets/_Modules/Core/SceneRouter.cs`) → scenes `RaidBase_IronBastion`, `RaidBase_fortified_garrison`, `RaidBase_mage_enclave`, `RaidBase_raider_camp_small` (`Assets/Scenes/`). **No `RaidParams`/loadout bag yet** — the WO-774 P0 seam.
- `RaidDeployController` + `TroopDeployer.SpawnFromArmy(...)` + `TroopController` (`Assets/_Modules/Village/Troops/`) — tap-deploy tray + spawn + auto-fight.
- `RaidScoring` + `RaidHudController` (`Assets/_Modules/Village/Troops/`; oracle `Assets/Editor/Regression/RaidScoringRegression.cs`) — 180s clock, stars, loot.

**Core/Jobs — multi-channel "Obsidian" work queue — SHIPPED (WO-773, landed at save schema v35; live schema is now **v36**).** `Assets/_Modules/Core/Jobs/`:
- `JobKind.cs` (Build/Upgrade/TowerBuild/TrainTroop/Research/…), `IJobEffect.cs` (per-job apply hook),
  `ObsidianQueueState.cs` (Builder/Train/Research channels + `ChannelId`), `ObsidianQueueEngine.cs` (offline-fair resolve).
- Persistence: `SaveSchema.CurrentVersion = 35`; `SaveMigrator.MigrateToV35` appends `ObsidianQueue` and folds
  legacy `BuildJobs`/`PendingBuilds`/`BuildingCooldowns` into the Builder channel (idempotent, no-loss).
- Surfaced by `Village/BuildMode/ObsidianQueueHud.cs` + `Village/Buildings/BuildTimerService.cs` (now the
  common multi-channel queue front). Player copy = "Builders"/"Training"/"Research", never "Obsidian queue".

**Troops foundation — SHIPPED.** `BarracksData` (`Assets/_Modules/Village/Troops/Data/BarracksData.cs`),
`TroopStatResolver` (`Assets/_Modules/Village/Troops/TroopStatResolver.cs`); data `Assets/Resources/Data/Canonical/barracks.json`,
`troop-upgrades.json`, `troops.json` (dual-copied to `StreamingAssets/Data/Canonical/`).

**IN FLIGHT (present, do NOT assert done):** `EnemyResolver` (`Assets/_Modules/Core/Enemies/EnemyResolver.cs`,
+ `Editor/Regression/EnemyResolverRegression.cs`, `Tests/PlayMode/EnemyResolverSpawnTests.cs`); the
barracks-catalog-structure (Barracks as an upgradable placeable building, PAIN_POINTS §3.3); the WO-774
raid-UX polish (loadout handoff / naming split / deploy ring / "Defenders %" copy / Train-queue UI).

---

## 1. INDEX TABLE — areas → section file → role

> STALE: 2026-07-12 — the docs-wo-state row's "next free WO = 412" is ~270 stale: WO specs on disk run through 683, next free = 684, with number collisions on 677/678 (see CANON_GROUND_TRUTH_2026-07-12.md)

| Area | Section file | 1-line role |
|---|---|---|
| **Core** | `docs/MASTER_CATALOG/core.md` | `DeNelle.Core` foundation: interfaces/enums/pure data, GameState + SaveSchema/Migrator persistence spine, SceneRouter, CoreServices registry, CanonicalJson loader, PanelManager, World/Catalog/Quests/Services/Web3 + the `DeNelle.AI` BT primitives. Refs nothing first-party. |
| **Village — Hero** | `docs/MASTER_CATALOG/village-hero.md` | Player hero (Blaise + class bodies): HeroLocomotion (NavMeshAgent), abilities Q/W/E/R, body swap, gear/equip (GearLoadout + EquipmentController), combat-feel/projectiles, SmartMobileCamera, input drivers, inventory/shop UI. |
| **Village — Systems** | `docs/MASTER_CATALOG/village-systems.md` | BuildMode (CREATE verb), Harvest (offline + worker), Tutorial/FTUE + DialogueService/CommandBridge, Arena async-PvP, world-space combat tells, EconomyService + building/upgrade progression. |
| **Village — NPCs** | `docs/MASTER_CATALOG/village-npcs.md` | StoryCompanions (party-of-4), join beats, castle hub injectors + interactables, ambient townsfolk + bubbles, HUD talk/party bridges, companion gear-up sub-beat. |
| **Village — Enemies/World** | `docs/MASTER_CATALOG/village-enemies-world.md` | Enemy/EnemyBrain/EnemyFactory, WaveManager loop, DragonBoss, RegionMobSpawner, OuterWorld streaming, ZoneManager seam, ward/tribe/settlement, camps/outposts/garrison raid loop, enemies.json/waves.json. |
| **HUD** | `docs/MASTER_CATALOG/hud.md` | `DeNelle.HUD` code-built uGUI town/combat HUD (`VillageHudController`, 3 canvases) + 12 Village→HUD push bridges + PanelManager modal arbiter + popups + diagnostics. |
| **Battle / ATB** | `docs/MASTER_CATALOG/battle-atb.md` | Turn-based Active-Time-Battle: deterministic pure-C# `Engine/` + runtime SO store (`ATBRuntimeState`) + scene `BattleController` + code-built `BattleHudUgui` + `AtbCombatantSwapper`. The breach/dungeon encounter combat. |
| **Dialogue** | `docs/MASTER_CATALOG/dialogue.md` | One shared Yarn runner: `DialogueService` + `DialogueCommandBridge` (~40 verbs) + ClassicRPG `CompanionDialoguePresenter`; intro cinematic bridge; 64 `.yarn` nodes; vendored Yarn addons. |
| **Audio** | `docs/MASTER_CATALOG/audio.md` | `DeNelle.Audio`: AudioService (A/B music crossfade + SFX pool + mixer), AudioBootstrap, MusicTrack registry, SfxClipLibrary, WebGL unlock, jukebox panel. |
| **Economy / Meta** | `docs/MASTER_CATALOG/economy-meta.md` | Pets, Wallet (Solana/SKR), Web3 (Jupiter swap), Cosmetics (Glimmer/BattlePass), PackStore monetization — all reflection-bridged off Village. |
| **Data catalogs** | `docs/MASTER_CATALOG/data-catalogs.md` | The `CanonicalJson` WebGL-safe loader + dual/triple-copy sync rule + ~30 typed catalog classes + every JSON catalog (abilities/enemies/buildings/gear/quests/pets/packs/themes…). |
| **Scenes** | `docs/MASTER_CATALOG/scenes.md` | 14 `Assets/Scenes/*.unity` + build-settings eligibility + the full boot/load-flow routing code (SceneRouter/WorldSceneLoader/HubScenes/SceneTransitionTrigger). |
| **DevTools / Settings / Onboarding** | `docs/MASTER_CATALOG/devtools-settings-onboarding.md` | Two dev panels (DevPanelController dev-only + AdminOverlay ships), Settings/Pause, OnboardingMode + flow + TitleController, DifficultyTuning, the two grant paths. |
| **Misc modules** | `docs/MASTER_CATALOG/misc-modules.md` | Dungeons (`DeNelle.Dungeons`: data-driven Healers Cottage + stub Granary, crafting, Bryn, lantern), Environment (torches/night lights), Data (`MasterAssetCatalog`), UI (`GameOverUI`). |
| **Editor tools** | `docs/MASTER_CATALOG/editor-tools.md` | `DeNelle.Editor` (reflection-only into Village): castle/outerworld/garrison scene builders, animator factories, build tools, QA gates (CompileGate/RegressionSuite), magenta material fixers. |
| **Resources / Art** | `docs/MASTER_CATALOG/resources-art.md` | `Resources.Load` path map (code → asset), Resources art folders (Heroes/Enemies/Structures/HudIcons/…), Assets/Art sources, art-consumer factories, gitignored model packs. |
| **Asset inventory (vendor packs)** | `docs/asset-inventory/README.md` | ★ Exhaustive map of ~21k meshes across vendor packs — most **GITIGNORED + previously uncatalogued** (gitignored ≠ invisible, owner caught the blind spot 2026-06-24). Three UNUSED shared-rig character libs (KayKit Adventurers/MM, Supercyan, + the Action clip lib), polyperfect/Quaternius env, ~1000 Mirza Beig/Spells VFX (only ~38 wired). What we own vs what actually ships (current hero = `Resources/Heroes/Knight.fbx` Tripo). 5 section docs. |
| **Docs — design** | `docs/MASTER_CATALOG/docs-design.md` | The `docs/**` design tree (137 md): canon/vision, narrative, engine-architecture specs, build-mode, combat/economy design, asset-pack notes, audits, port-notes, QA docs. |
| **Docs — WO state** | `docs/MASTER_CATALOG/docs-wo-state.md` | Repo-root governance + 438 work-order spec files + pipeline-state docs; numbering authority (next free WO = 412); current ground-truth state synthesis. |

---

## 2. ARCHITECTURE MAP

### 2a. Assembly / dependency graph (DeNelle.*)

Bounded-context assemblies (HP-B2B architecture law, `docs/ARCHITECTURE_PRINCIPLES.md`).
**Presentation is a separate layer that never touches the gameplay objects.** Core is the
shared spine; nothing references up; nothing references first-party from Core.

```
                         DeNelle.Core   (interfaces, enums, pure data, services,
                          ▲  ▲  ▲  ▲      SceneRouter, GameStateService, CanonicalJson,
                          │  │  │  │      PanelManager, CoreServices, HubScenes, World/
        ┌─────────────────┘  │  │  └──────────────────┐   Catalog/Quests; +DeNelle.AI BT)
        │            ┌───────┘  └────────┐            │
   DeNelle.Data   DeNelle.Village    DeNelle.HUD   DeNelle.BattleATB
   (typed         (Enemy, EnemyBrain, (VillageHud-  (ATB engine + store +
    catalog        WaveManager,        Controller,   BattleController +
    helpers)       HeartController,    +12 bridges   BattleHudUgui)
                   HeroLocomotion,     live HERE on  refs Core, Data
                   EconomyService,     the Village
                   buildings, bridges) side)
        │
   DeNelle.Pets · DeNelle.Wallet · DeNelle.Web3(→Wallet) · DeNelle.Cosmetics ·
   DeNelle.Onboarding · DeNelle.Dungeons · DeNelle.Audio · DeNelle.Settings ·
   DeNelle.DialogueUI(→Village) · DeNelle.DevTools(→Village,HUD,Wallet) · DeNelle.Editor
   (each → Core, some → Data)
```

**Cross-asmdef rules (BINDING — verified held in the section catalogs):**
- `DeNelle.Village → DeNelle.Core` only. `DeNelle.HUD → DeNelle.Core` only.
  **Never Village ↔ HUD directly, never HUD/BattleATB → Village.**
- Core → Village would be a **circular ref (CS0234)** — Core awards crystals by writing
  `GameState` directly (not `Village.CrystalEconomy`); damage attribution / XP go through
  the Core `XpEarnerRegistry` / `DamageAttribution` id-keyed registries, not direct calls.
- **HUD pushes from Village go IN via two seams:** the `IVillageHud` interface
  (`CoreServices.Hud`) for interface methods, and **reflection-by-name** on the concrete
  `VillageHudController` for the "extra" setters not on the interface (Talk/Party/Town/etc).
  The same reflection-across-the-boundary pattern is how HUD/BattleATB/Pets/Cosmetics read
  Village types (HeroLocomotion, WaveManager, Enemy, MineNode, GlimmerCurrencyService)
  without an asmdef ref. `CoreServices` slots: `Hud`, `Audio`, `Jupiter`, `WalletSigner`.
- **`DeNelle.Editor` deliberately does NOT ref Village** — every Village type is reached by
  `FindType` over AppDomain + reflection; all editor entries are menu/`-executeMethod` (no bootstrap).
- **`DeNelle.DevTools`** is the module-isolation EXCEPTION (tooling may ref gameplay) and is
  compiled OUT of release (`UNITY_EDITOR || DEVELOPMENT_BUILD`).
- BattleATB `Engine/` is **pure C#, no UnityEngine** (except the optional unused
  `CombatantDefSO`); deterministic mulberry32 RNG, golden-vector bit-parity tested.

### 2b. Scene boot / load flow (verified against routing source)

```
Title (#0, boot scene; Core DDOL singletons spin up)
  ├─ Continue ─────────────► GoCastle() ──► MainCastle_Hall   (returning player, loads save)
  └─ Play Intro / New game ─► [Yarn cinematic | StoryIntro cold-open]
                               in-Title hero pick ─► GoPetSelect()

HeroSelect ── confirm ─► GoPetSelect() ─► PetSelect
   └─ returning-player skip (hero+pet saved) ─► GoCastle()

PetSelect ── confirm (writes StarterPetId, Save) ─► GoCastle() ──► MainCastle_Hall

MainCastle_Hall (HOME HUB)
   └─ WorldSceneLoader auto-loads ► OuterWorld (ADDITIVE on any hub)
   └─ SceneTransitionTrigger (south gate seam) ► OuterWorld + WarpTo hero across the seam

OuterWorld (additive over hub)
   ├─ DungeonEntrance / DungeonWorldPortalSpawner ─► Dungeon_HealersCottage / _FolksGranary
   ├─ RaidOutpostSystem ─► 4 cardinal in-world EnemyOutposts (spawned in OuterWorld, ~10s delay)
   └─ raid access ─► Garrison_{troll_outpost,ruined_keep,hill_fort,frost_keep} (ADDITIVE)

Village2 (TD town / raid target) — GoVillage() = LoadVillageWithLoader() (async overlay)
   └─ WorldSceneLoader auto-loads ► OuterWorld (Village2 is a hub too)

Breach (from Village2 / dungeon) ─► GoBattle(BattleParams) ─► ATBBattle ─► returns to ReturnScene
```

- **Home hub = `MainCastle_Hall`** (built by `Assets/Editor/CastleHubBuilder.cs`; owner
  hand-dialed + committed — a regen would REVERT owner's offsets, builder not yet updated
  to reproduce them). The Title→HeroSelect→PetSelect→**Castle** boot chain is the 2026-06-08
  castle-start pivot; stale "…→ Village" prose remains in `PetSelectController`/`SceneRouter`
  headers (trust the code: onboarding lands in MainCastle_Hall).
- **`Village2`** = generated TD town / raid-target stronghold (canonical).
  **`Village.unity` = ABANDONED / corruption-cursed — never use or re-save it.**
- `SceneRouter.Village = "Village2"`, `Castle = "MainCastle_Hall"`. Every load guards
  `Application.CanStreamedLevelBeLoaded`. `HubScenes.IsHub` (Village2/MainCastle_Hall/
  CastleHub/CastleHub_MainKeep) is the single hub source read by WorldSceneLoader + HUD.
- Menu scenes (Title/HeroSelect/PetSelect/Intro/Store/…) are on the HUD bootstrap
  **allowlist-skip**; all other gameplay scenes auto-bootstrap a `VillageHudController`.
- **Defend-the-Tower / PatriciaLight = REMOVED 2026-06-09** (module + scene gone; only
  `Resources/PatriciaLight/tower2` kept). All DTT/PatriciaLight WOs + router consts are dead.

### 2c. CRITICAL-PATH systems — where each lives + how it ACTUALLY works

(Verified from the section catalogs by reading source, NOT from code comments.)

- **Hero NAVIGATION — `Assets/_Modules/Village/Hero/HeroLocomotion.cs` (`DeNelle.Village`).**
  Despite a stale header claiming "no Rigidbody, no NavMeshAgent — pure transform," the code
  is the OPPOSITE: Awake() gets/adds a **`NavMeshAgent`** (radius 0.4, height 1.8,
  `updateRotation=false`, speed 30 so Move never caps), reads input → eased `Velocity` →
  **`_agent.Move(step)`** when on-mesh, else `transform.position += step` (off-mesh fallback);
  manual `LookRotation` for facing. So it is a NavMeshAgent **kinematically driven by input**
  (not pathfinding, not pure transform). Awake also OVERRIDES serialized move speeds.
  Input is camera-relative in follow (rotated by `SmartMobileCamera.CameraYaw`), world-absolute
  in top-down. Live mobile input = Village `VirtualJoystick` (HUD `VirtualDPadLean` is orphaned).
  `WarpTo` disables→warps→re-enables the agent (seam crossing). **Treat hero locomotion as
  agent-driven** — debug "can't move/exit" via NavMesh bakes, not colliders. The same trap
  recurs on `Pet.cs` (also a self-added NavMeshAgent) and `Enemy.cs` (NavMeshAgent, honestly
  documented). FTUE auto-walk + SceneTransitionTrigger.WarpTo depend on the agent.

- **Dialogue / Yarn option+command flow — `DeNelle.Village` DialogueService + DialogueCommandBridge.**
  Every Yarn conversation plays through ONE shared runner (`Resources/Dialogue/DialogueSystem.prefab`,
  ClassicRPG Canvas UI, code/Canvas not UXML → WebGL-safe). `DialogueService.Play/PlayStructure`
  hosts-or-reuses it and installs `DialogueCommandBridge` (~40 verbs: camera/audio/structure/
  movement/HUD/combat/pets/quests + the **vendor verbs OpenShop/OpenUpgrade/OpenCraft/OpenEquip/
  OpenArena/OpenRumorBoard**). Vendors, building Buy/Sell/upgrade, yes/no confirms ALL route here,
  NOT bespoke panels. **`NPCCommandBridge` is DEAD/neutralized** — its verbs were consolidated into
  DialogueCommandBridge because YarnSpinner's source generator throws on any action name registered
  twice (every name must register exactly ONCE project-wide). Gotcha: a Yarn **bare command-arg is
  literal** (`<<cmd $var>>` passes the string "$var"); stash in C# (`DialogueService.CurrentStructureId`)
  and read it back, or use `{$var}`. The single parameterized `StructureMenu` node drives all
  building interactions. `TalkHudBridge` gates `SetTalkAvailable`/routes `TalkRequested` to nearest NPC.

- **Economy / wallets — the SPLIT (`DeNelle.Village.EconomyService` vs `DeNelle.Core` GameState).**
  `EconomyService` (DDOL singleton) is a 4-resource wallet where **Wood/Iron live in an in-session
  pool** (shop + HUD bar read this) while **Food/Crystals read-through to `GameState.Resources`**
  (single source of truth). `CanAfford/TrySpend/Grant`. **Wood/Iron dual-wallet hazard:** the
  building-upgrade flow's `ResourceLedger` reads/spends **GameState.Wood/Iron**, which do NOT
  auto-sync with the pool — `GrantSpendable(w,f,i,c)` exists solely to write BOTH (both dev grant
  paths use it). Crystal stores: `GameState.Resources.Crystals` is canonical; `CrystalEconomy`
  is a separate singleton to verify-or-retire; `GameState.AetherCrystals` is DEPRECATED (folded
  into Resources.Crystals at save v18). Persistence spine: `GameState` (SO, 41 partialize fields)
  + `GameStateService` (Load/Save via PlayerPrefs `dotr-save` → migrate → validate → apply) +
  `SaveSchema` (CurrentVersion=30) + `SaveMigrator` (v1→v20). Resource model (memory): Wood/Iron/
  Food build structures; Crystals = special arc (unlock spells → jewelry → armor).

- **Companion / FTUE / introducer — `DeNelle.Village` StoryCompanion + `DeNelle.Onboarding`.**
  One unified roster = heroes ARE companions: Knight→Grom, Ranger→Sylas, Mage→Thrain, Cleric→Elara.
  `StoryCompanionInjector` (hub-gated DDOL) spawns ONE mortal body per persisted party member;
  companions follow+fight (leashed 22m, NavMeshAgent or lerp). Canon join order:
  **Sylas (beat-1) → Elara (wave 3) → Grom (first OuterWorld return)** (all hub-gated, one-shot,
  substitute a different free class if it clashes with the player). The **canonical companion-intro
  is now a walk-up NPC** (`CastleCompanionIntroducerInjector`, owner 2026-06-12) at courtyard
  `(-4,0,-30)`; on Talk it plays Yarn `SylasFirstMeeting` (`<<RecruitCompanion Ranger>>`). The old
  `SylasFirstMeeting` auto-beat stands DOWN whenever that injector is `Active`. `PartyHudBridge`
  pushes StoryCompanions (real Hp/MaxHp) into HUD party slots 1..3. Vendors: `CastleVendorNpcInjector`
  (exact `MainCastle_Hall`) places 8 static vendor NPCs; `VillageNpcInjector` (exact `Village2`)
  the 4 townsfolk. Note the gating inconsistency: vendors use exact-scene, companions use HubScenes.

- **World camps / outposts / dungeons / garrisons — `DeNelle.Village.World(.Camps)` + `DeNelle.Dungeons`.**
  OuterWorld streams additively over any hub. **Two raid mechanisms, easy to conflate:**
  (a) `RaidOutpostSystem` spawns 4 cardinal `EnemyOutpost`s IN-WORLD inside OuterWorld (no scene;
  `_enabled` hardcoded ON; spawn delay cut 180s→10s 2026-06-11; header still says "ONE outpost"—STALE);
  (b) standalone `Garrison_*` SCENES loaded ADDITIVELY, driven by `GarrisonController` on `GarrisonRoot`
  (recipe-fed from `garrison-recipes.json`, 4 recipes). `CampSystem` adds 4 claimable camps (clear→
  claim→build outpost→defend); also flag-forced ON. Dungeons via `DungeonLayout`
  (`dungeons/healers-cottage.json`, 12 rooms — full data-driven) vs `Dungeon_FolksGranary` (STUB,
  no JSON); both enter the same ATB combat via `EncounterTrigger`→`SceneRouter.GoBattle`. Region/map
  data: `ZoneManager` (Core, static classifier; village ±42/±33, 4 cardinal regions Goldfields/
  Stoneback/Mirewood/Ashwood by danger tier); `realm-map.json` is StreamingAssets-only → WebGL-null.
  "Missing feature" in the world is usually a working system gated/delayed/region-excluded — check first.

- **Build / upgrade — `DeNelle.Village` BuildMode + Buildings/Progression.**
  Curated predefined catalog (Fallout-4 settlements model, NOT free-form), resource-gated, ~70% built
  end-to-end for towers: HUD Build → top-down cam + frozen waves → palette card → ghost → place →
  charge → persist to `GameState.BaseLayout` (save v14) → `BaseLayoutLoader` rebuilds on reload.
  `BuildButtonBridge` wires HUD `BuildRequested`→`BuildModeController.Toggle` by reflection;
  `BuildModeHudBridge` hides combat HUD while building. Resource-building upgrades (Farm/Lumbermill/
  Forge, 5 levels + Magic-gated Arcane Forge) use `ResourceBuildingProgression`/`State` (HARDCODED
  balance table) via `DialogueCommandBridge`'s `OpenUpgrade` — NOT the orphaned `*Upgrades.json` spec
  data. Buildings spawn their own front NPC (Talk routes to the NPC, dissolving "Talk: Windmill").

- **HUD / PanelManager modal discipline — `DeNelle.Core.UI.PanelManager` + `DeNelle.HUD`.**
  `PanelManager` is a pure-static single-modal arbiter: at most ONE registered panel open at a time
  (`Register(name, close, isOpen)` + `NotifyOpened` closes the prior). HelpMenu, AdminOverlay, and
  cosmetics/village/inventory popups all register + obey it; `MobileInteractButton` suppresses world
  prompts while a modal owns the screen. The HUD is `VillageHudController` (one code-built uGUI HUD;
  three nested canvases — base chrome 100 / Battle 150 / Town 140; context = scene Village2 AND hero
  within TownRadius 60 of origin). `BattleHudVisibilityManager` cross-fades BATTLE / TOWN / HIDDEN.
  **All HUD is code-built — UXML/UIDocument HUDs do NOT render in player builds** (project law; the
  reason onboarding/compass/battle HUDs were rewritten code-built).

---

## 3. STALE / RISK LEDGER (consolidated, prioritized)

> **2026-07-03:** the 07-02→03 convergence session touched ~50 systems (see
> `CANON_GROUND_TRUTH_2026-07-03.md`); per-area docs village-systems/resources-art have same-breath
> notes; full catalog refresh queued.

Every flag the 18 section agents raised, in one prioritized list.
**P1 = blocks/misleads work or breaks a platform · P2 = wrong behavior but contained ·
P3 = dead/stale, cleanup.**

### P1 — blockers / platform breakage / actively misleading

1. **HeroLocomotion comment LIES about the navigation model** (village-hero §1, docs-wo §5a,
   editor-tools, scenes FLAG-2). Header + class XML-doc say "pure transform, no NavMeshAgent";
   the code is **NavMeshAgent + `_agent.Move` + `NavMesh.SamplePosition`**. A reader trusting
   the comment mis-diagnoses every hero-movement bug. Doubly dangerous: **RegressionSuite
   source-greps this very file** for the WO-387 camera-yaw basis — a stale comment can fool a
   source-grep gate. Fix the comment; treat nav as agent-driven. (Same class on `Pet.cs` line ~582
   "kinematic drift; NavMeshAgent wiring is the integrator's" — it self-wires the agent via WO-187.)

2. **OuterWorld ~1 fps open blocker** (docs-wo §4). Even at 0 enemies the streamed open world
   runs "frame by frame." Two provable per-frame costs fixed (`DefenseTower/ArcaneTower.Rescan()`
   whole-world `FindObjectsByType` every 0.4s → `4b5208c`; bridge scans → O(1) registries `463a5e8`),
   root cause UNPROVEN — awaiting owner profile verdict. Worse on mobile/WebGL (OOM risk).
   Related live per-cast alloc: `StoryCompanion.TryClericMend` still `FindObjectsByType` every heal.

3. **6 StreamingAssets-only catalogs are WebGL-broken-by-omission** (data §FLAG-2):
   `enemy-roles`, `towers`, `walls`, `realm-map`, `heart`, `audio-mix` have NO Resources copy →
   `CanonicalJson.Read` returns `null` in WebGL (Resources miss + no filesystem). Exactly the
   failure class CanonicalJson exists to prevent. Mirror any needed in web to Resources.

4. **WebGL ships at 223 MB (itch rejected).** Fix = Gzip OR run **WO-408** texture-opt (scripts
   committed, **NOT run**). Blocks the web distribution build.

5. **WO-405 `ElarionUiKit` design-system gate** blocks all unified-HUD work (WO-400/403/404/411).
   WO-403 unified HUD is STASHED, to be redone modular (<800 lines). Owner-approval gate.

6. **GameAudioMixer is a stub, not the documented 5-group/5-param mixer** (audio §FLAG-1). The
   `.mixer` asset has ONLY a `Master` group, `m_ExposedParameters: []`. Every `SetFloat`/`FirstGroup`
   for Music/SFX/UI/Voice silently fails — only the AudioSource-direct fallback controls volume/mute.
   AudioMixerBridge + Settings sliders persist but don't drive the (absent) per-group mix. The
   documented mixer was never built into the asset.

7. **Numbering authority vs filesystem drift** (docs-wo §5d). Authoritative next-free WO = **412**
   (`MASTER_PIPELINES_BACKLOG` + `CLI_LANES_WO_NUMBERS`); 344–351 reserved (skip). PROJECT_INDEX/
   SESSION_START_HERE still say "next 384" — index lines lag. **Never mint from filesystem max**;
   30 WO numbers collide (docs-wo §2h) — renumber 391+. 438 WO files for ~280 distinct numbers.

8. **RESOLVED (verified from code 2026-07-13, WO-714 W8).** Settings/Pause are NO LONGER UXML-bound:
   both were rebuilt as code-built kit modals on 2026-07-03 (WO-F conversion, coverage rows #47/#47b —
   `ElarionUiKit.BuildObsidianModal`, FrameSettings/FrameOptions); `SettingsScreen.uxml` +
   `PauseOverlay.uxml` are DELETED from the tree (script GUIDs appear in no scene). The REAL residual
   gap — no scene placed the controllers and nothing called `PauseGate.RequestBack()`, so the panels
   were unreachable in-game — closed 2026-07-13 by `PauseHudBootstrap` (DeNelle.Settings): auto-installs
   PauseController+SettingsController per gameplay scene + the on-screen pause chip that calls
   RequestBack. (ATBBattle `BattleHUD.uxml`, dungeon panels, PromoCodeUI/InviteFriendsUI/
   WalletConnectDialog/JupiterSwapPanel remain the outstanding UXML-in-build risks.)

### P2 — wrong behavior, contained

9. **Aegis legendary set is UNREACHABLE** (village-hero §FLAGS): the 4 aegis WEAPONS in weapons.json
   have NO `setId` (only `aegis_plate` armor does) → `WeaponDef.IsAegis` is FALSE for all → 
   `GearLoadout.AegisSetActive` (needs both) can never be true → the Oathweld ward + per-class Aegis
   weapon perk are dead. **Likely a data bug** — add `"setId":"aegis"` to the four aegis weapons.

10. **EquipmentController shows tinted-primitive fallback** (village-hero §FLAGS): real KayKit weapon
    meshes aren't in `Resources/Heroes/Props/Weapons/` → every hero's weapon is a tinted primitive
    until art is copied. `abilities.json` has no `cleric` class → Cleric fires the Mage loadout (by design).

11. **ATB enemy model never varies** (battle F-SWAP-2): `AtbCombatantSwapper.ResolveEnemySlug()` is
    hard-coded `"Skeleton_Warrior"` despite a rich 7-entry `ENEMY_DEFS` + `EnemyControllerFor` map.

12. **ATB HUD always shows "WAVE 1"** (battle F-WAVE-1): `BattleHudUgui.Render` hard-codes the wave
    text though `BattleState.Wave` is real and scaled.

13. **ATB caster-vs-melee anim mis-pick** (battle F-CTRL-comment): `IsCasterHeroClass()` string-matches
    the DEV-fallback name `_fallbackHeroName` ("Blaise"), not the resolved hero → attack-vs-cast anim
    can pick wrong for the real selected hero.

14. **ATB idle auto-attack disabled** (battle F-MGR-1): `ATBCombatManager`'s 8s idle timer fires
    `onEnemyAutoAttack`/`onPlayerTurnStart` UnityEvents with **no listeners** → punitive auto-attack
    (WO-93) effectively off. **ATB control-mode toggle unbuilt** (F-CTL-1): engine `ControlMode`
    plumbing complete + tested but `HandleControlModeToggled`/`OnControlModeToggled` never invoked.

15. **Synthesised enemy stat divergence** (enemies-world §contradictory): open-world roster ids
    (orc-raider/caveman/feral-wolf/tiefling-cultist) exist ONLY as code EnemyDefs in THREE places
    (RegionMobSpawner/EnemyOutpost/GarrisonController) with **divergent stat blocks** for the same
    id (e.g. orc-raider hp 95 vs 170) — no single source until they land in enemies.json. Balance-drift hazard.

16. **DailyQuestHud is display-only** (HUD §FLAG-6) — no claim/reward dispense flow yet.
    `DailyQuestService.FeatureShipped` also returns false for harvesting/tower-build/cosmetic-shop/
    hero-talents, filtering out quest templates for features that DO exist (stale gate vs feature state).

17. **3rd stale gear copy** (data §FLAG-1): `Assets/Data/Canonical/{armor,weapons}.json` is loaded by
    nobody (`GearCatalog` reads the Resources copy via CanonicalJson) — drift hazard. `version` field
    missing on armor/weapons (data §FLAG-6) so a dropped-version hand-edit on gear won't be caught.

18. **CastleHubBuilder can't reproduce the owner's hand-dialed offsets** (docs-wo §5c, editor-tools)
    — a scene regen REVERTS owner's committed work. Don't regen MainCastle_Hall.

19. **Three unreconciled persistence stores in economy-meta** (economy §16): PackStore→GameStateService
    (unified save); GlimmerCurrencyService→PlayerPrefs `dotr-cosmetics-v1`; BattlePassManager→PlayerPrefs
    `BP_*`. Two cosmetic-ownership sources of truth (pack SKUs in GameState.OwnedItemIds vs Glimmer-shop
    in the PlayerPrefs blob) not reconciled. PetAcquisitionService active-slot assignment not persisted
    (only StarterPetId survives reload). `pet-skill-trees.json` over-specifies (11 trees, only 3 species
    have PetDefs + map to the enum).

20. **Append-only GameState fields not yet in SaveSchema** (core §append-only, enemies-world):
    `Tribes`, `Settlements`, `Wards`, `Arena`, `PetName` live in-memory per session but do NOT survive
    reload (deferred save-owner follow-up). `Zones/BaseLayout/PartyMemberIds/ArenaDefense` ARE wired.

21. **Arena SKR wager + seed data are stubs** (village-systems §stub): `ArenaWalletService` is a
    PlayerPrefs client stub (seed 500); ArenaCatalog (3 opponents)/ArenaDefenseCatalog (6)/
    DefensePatternLibrary are HARDCODED with `// TODO → *.json`. **SKR mint empty everywhere**
    (`WalletEndpoints.SkrMint* = ""`, `JupiterSwapService._skrMint = "REPLACE_..."`); Jupiter targets
    MAINNET while the wallet stack is DEVNET-only (unreconciled); swap signing is a stub that hard-fails
    in release. SOLANA_SDK off by default → all wallet ops run through the devnet StubWalletProvider.

### P3 — dead / stale / cleanup

22. **`HUDManager` does not exist** (HUD §FLAG-1) — yet `README.md` + the whole `README_HUD.md`
    describe it as shipped, and `VirtualDPadLean.cs` is orphaned by its no-op bootstrap. Live input is
    Village `VirtualJoystick`. Delete README_HUD.md + VirtualDPadLean.cs, or restore HUDManager.

23. **Dead BattleATB infrastructure** (battle §dead): `CombatantDefSO` family (no `.asset` instances),
    `ATBBackgroundController` (dormant orphan, `ATB/Video/*.mp4` unused), `Defs.ATB_BASE_FILL`/
    `AtbCombatantSwapper.HideOwnRenderer` (dead), and the scene's orphaned `_hudDocument`/UIDocument/
    `BattleHUD.uxml`/`BattlePanelSettings` (live HUD is code-built `BattleHudUgui`). `BattleSceneBuilder.cs`
    is STALE (re-wires the gone UXML path). README lists non-existent `BattleHud`/`BattleVfx` + wrong
    "FF7 blue" aesthetic (live is parchment/gilt).

24. **Dead/duplicate hero+village code**: legacy equip stack `HeroEquipment + EquipmentPanel`
    (hardcoded demo items — do not extend; route equip through GearLoadout). `HeroCinemachineRig`,
    `HeroChargeVFX`, `HeroAimIK` (no SetAimTarget caller), `HeroReachRing` (DEF-205 not-attached),
    `GearVisualApplier` (primitive cubes gated off), two victory-pose paths. `NPCCommandBridge` dead.
    `RegionMobSpawner.ModelForRoamer` unused. `WaveManager.BuildPlaceholderEnemy` legacy.

> STALE: 2026-07-12 — item 25's "Both build tools ship BuildOptions.Development" is false for WebGL: `WebGLBuild.cs:124` ships `BuildOptions.None` (Development is opt-in via `-devBuild`, WO-408); the DESKTOP Development flag remains (DesktopBuild.cs:178) (see CANON_GROUND_TRUTH_2026-07-12.md)

25. **DUPLICATE MenuItem `Defenders/Build/WebGL Player`** (editor-tools §dead) in both `WebGLBuild` and
    `DesktopBuild` with contradictory settings (Brotli/Development/512MB vs Gzip/None) — only one binds.
    Both build tools ship `BuildOptions.Development` for the "ship" path → DevTools leak into release.
    `OuterWorldBuilder.BakeWorldNavMesh` + `SpawnPathVerifier` both open the **abandoned `Village.unity`**
    (corruption-cursed) — stale/risky; use `OuterWorldNavBake` (OuterWorld-solo) instead.

26. **Audio dead/missing**: `SfxClipLibrary.asset` + `DeNelleAudioService.prefab` don't exist →
    `PlaySfxAtPosition(SfxId)` silent no-op, prefab bootstrap branch dead. Dungeon/GameOver/Overworld
    `world.mp3` clips absent (guarded silent). Two MusicTrack enums (Audio-side decl order vs Core-side
    explicit indices) — jukebox PlayerPrefs persists the Audio-side ordinal (reorder = shifted picks).

27. **Unbacked Resources.Load paths → silent null** (resources-art §unbacked): `Pets/*`, `Cosmetics/Pets/*`,
    `Cosmetics/Previews/*`, `HeroPortraits/*`, `Intro/intro-*`, `heart-wing`, `UI/panel_bg|menu_bg`, all
    `Sfx/*` except LookoutHorn → callers fall back (procedural pet/SFX, null portraits, solid fills).
    Shipped-icon typos: `HudIcons/Wizard/wiard.jpg`, `Wizard_Lightining.jpg`. `EnemyVfxSet_Default.asset`
    all arrays empty. `Resources/PatriciaLight/tower2` dead-art remnant. Fresh-clone "black village" =
    gitignored `Assets/Models` KayKit packs absent (Resources prefabs survive).

28. **Stale comment-only `UIDocument` tokens** (HUD §FLAG-3) in `CompassHud`/`CompassHudBootstrap` —
    code is pure uGUI now; comments narrate the retired UIDocument design (false grep flag). AdminOverlay
    dead-but-retained handlers + wallet auth path (`OwnerWalletAddress=""` never passes — chord-only by
    design). PauseController header says "new Input System" but body uses legacy `Input.GetKeyDown`.

29. **Orphaned spec-era data** (data §FLAG-3/4/5): `Upgrades/FarmUpgrades.json` + `WatchtowerUpgrades.json`
    referenced only by WO-237's unwired panel. `orientation-recipes.json` is JSONL not JSON (whole-file
    parse fails). `castle-south-recipe.json` bypasses CanonicalJson (editor-only, OK). Cross-root sync
    unenforced: 26 Resources vs 32 StreamingAssets canonical files, no cross-root diff test.

30. **Backend never deployed** (core §backend-dependent, economy): GameStateService delta-sync, EventTracker,
    PromoCodeService, ReferralService, LeaderboardService all target a Vercel URL that was never deployed.
    They run resilient (local-save-only, circuit-breaker, honest stubs) — pre-deploy stubs, NOT live bugs.
    `BackendAuthConfig.Enforced` off; ClanService is a pure PlayerPrefs stub.

31. **Stale code-banners (the comment-vs-code class, non-load-bearing)**: `SaveSchema.cs` banner says
    v10 (code v20); `SaveMigrator.cs` banner says "v1→v10 nine-step" (code v1→v20); `Theme.cs` banner
    says StreamingAssets/`forbids Resources.Load` (code uses CanonicalJson Resources-first);
    `ResourceType.cs` maps to the deprecated `AetherCrystals`; `PartyHudBridge` header says companions
    are immortal (code reads real Hp); the "no Player tag" comment recurs across NPC files while many
    now `FindWithTag("Player")` first. `RaidOutpostSystem` header says "ONE outpost"/180s (code = 4/10s).

32. **Stale docs** (docs-design §FLAGS, docs-wo §5b): "Avalon" town name + "Blaise" hero baked into
    v2-unity-port-spec + ~10 docs (live canon = **Elarion** + Thrain/Grom/Sylas/Elara). Pi-Network
    economy (PI_PITCH + NORTH_STAR line) superseded by Solana/$SKR. Heart-Tree premise → Cathedral Spire.
    Lantern motif → Stone Choir. `port-notes/week4-*` wire the abandoned `Village.unity`. Unity version
    `6000.4.7f1` → live `6000.4.8f1`. Most C/D/E/F engine docs are SPEC (designed, not built). `BUG_LIST.md`/
    `SESSION_START_HERE` Order Log are 05-31 snapshots; `ORCHESTRATION_PLAN.md` (05-28) do-not-use;
    `BACKLOG_SILOS`/`SILO_FILE_MIGRATION_MAP` describe a restructure that was SKIPPED. 376–382 missing
    from Notion; Notion 328–339 ≠ repo 328–339.

> **Trust rule (owner-mandated, docs-wo §4):** never mark a WO/fix DONE on a green gate alone —
> only the owner's playtest is the verdict. Don't patch-and-claim-fixed.
