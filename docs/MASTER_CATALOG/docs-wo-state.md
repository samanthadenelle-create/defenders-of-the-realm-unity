# MASTER CATALOG — Area: `docs-wo-state`

Exhaustive reference catalog of repo-root work-order + pipeline-state documentation.
Compiled 2026-06-12 by reading the actual files (not summaries). Branch `feat/tower-core-loop`.

**Scope:** every `WORK_ORDER_*.md` / `*.RESULT.md`, `HANDOVER_*`, `MASTER_PIPELINES_BACKLOG_*`,
`PIPELINE_STATE.md`, `CLI_LANES_WO_NUMBERS.md`, `SESSION_START_HERE.md`, `PROJECT_INDEX.md`,
`NOTION_SOURCE_OF_TRUTH.md`, and the remaining root `*.md` state/design/process docs.

**Counts (root level):** ~98 non-WO `*.md`; **438** `WORK_ORDER_*.md` spec files (many are
duplicate-numbered / superseded — true distinct WO numbers ≈ 280); **41** `*.RESULT.md`
completion records. Items cataloged below: ~140 docs + 438 WO files grouped + the flagged
code mismatch.

> This is a DOCS area: it contains no live MonoBehaviours of its own. The one code file
> examined (for the mandated comment-vs-code check) is `HeroLocomotion.cs` — see FLAGS.

---

## 1. THE GOVERNANCE SPINE (source-of-truth docs — current, read these first)

| Doc | Title / gist | Current? |
|---|---|---|
| `CLAUDE.md` | Agent rules + project memory; binding architecture law, asmdef map, mount-sync rule, WO protocol. **Next free WO = 412; 344–351 reserved/skip.** | **CURRENT (binding)** |
| `PIPELINE_STATE.md` | Ground-truth BUILT/WIRED/STUB/MISSING catalog. Top "CURRENT STATE 2026-06-09" block supersedes everything below it (older sections kept as frozen history). | **CURRENT (top block only)** — lower sections explicitly marked stale |
| `SESSION_START_HERE.md` | Session entry map + living Order Log. Header says "Last updated 2026-05-31"; body has 2026-06-09 inserts. Order Log tables are **05-31-era and partly stale** (lists WO-173/177/158 as OPEN that later closed). | PARTLY STALE (entry rules current; Order Log dated) |
| `MASTER_PIPELINES_BACKLOG_2026-06-06.md` | SOURCE OF TRUTH backlog, 13 lanes, ✓/◐/▶/⏸/★/⚠ legend, full lane listings, Story→WO mapping, flat index 290–305. | **CURRENT (canonical backlog)** |
| `CLI_LANES_WO_NUMBERS.md` | Numbers-only run-order per lane (reconciled 2026-06-11 nightly). Lists out-of-band blocks 352–390 + 391–411, collisions, HUD gate (405 blocks 400/403/404/411). | **CURRENT (most-recent lane doc)** |
| `NOTION_SOURCE_OF_TRUTH.md` | 2026-06-06 switchover note: live board moved Linear→Notion (free-tier 250 cap hit). Notion "Work Orders" DB `5f66b263-c732-4075-b94a-f5f4de9f8087`; git holds full specs, Notion holds status/lane/deps. | **CURRENT** |
| `PROJECT_INDEX.md` | Root-file navigation map; living-docs table + WO/design/guide/historical buckets. Says "~370 md", "WO-05→383 next 384" (number now lags real 412). | CURRENT (numbering line lags) |

**Numbering authority chain:** `MASTER_PIPELINES_BACKLOG` + `CLI_LANES_WO_NUMBERS` (NOT filesystem max).
Next free = **412**. 287/288 used, 289 free, 290–305 minted, 306–343 used (339–343 refill),
**344–351 SKIPPED (treat as used, do not mint)**, 352–390 out-of-band (06-08/09), 391–411 on-board (06-10/11).

---

## 2. WORK ORDERS — grouped catalog (438 files / ~280 distinct numbers)

Status key: **DONE** = has `*.RESULT.md` or a fix commit found · **OPEN** = ready/in-queue ·
**SPEC** = design-only, not implemented · **HELD/SUPERSEDED** = dead. WO files follow a fixed
spec shape: `# WORK_ORDER_N — title` / `Status:` / Context / Goal / Files to edit / Acceptance
criteria checklist / "Do NOT touch". RESULT files: `# WO-N RESULT` / Status ✅ / Commit hash /
Resolution / Acceptance checkboxes.

### 2a. WOs with filed RESULT.md → DONE (41)
`05` magenta+pets · `06` hud_in_builds · `07` hero_abilities · `08` proximity_gates ·
`09` webgl_build · `10` mvp_smoke_test · `11` atb_battle_check · `12` regression_check ·
`18` hero_walk_anim · `19` dungeon_entrances · `20` hud_data_binding · `27` enemy_spawn_world ·
`36` hero_actions_skill_tree · `52` weather_manager · `55` torch_fire · `58` pet_aura ·
`59` dungeon_vfx · `66` boss_vfx · `73` shop_ui_battle_pass · `83` wave_clear_combo ·
`84` enemy_death_reactions · `86` scriptableobject_data · `87` cinemachine_camera ·
`106` (×3 RESULTs: default_gear_shop / pet_resource_outpost / xp_level_hud) · `108` player_build_mode ·
`153` world_crystal_mine · `166` playtest_regressions · `172` build_times_ad_speedup ·
`175` store_visual_polish · `178` hud_healthbar_styling · `283` canonical_anim_library ·
`284` unified_anim_routines · `285` 3d_combat_anim_lib · `286` hero_fbx_import_fix ·
`358` yarn_welcome · `368` camera_movement_regression · `380` gear_icon_minimap · `382` hero_hp_consolidation.

### 2b. WOs marked DONE in docs/commits but NO repo RESULT.md
From `CLI_LANES_WO_NUMBERS` ✓ marks + git fix commits: `107`(✓doc) `109`/`110`/`111`(QA-checklist wired,
code-inspection only) · `156`/`168`/`157`/`158`/`173`/`177` (Lane-A village pass, 05-31 rebake `8f4c6f3`) ·
`181` (partial split COMPLETE, SESSION_START_HERE) · `302` `310` `316` `317` `326` `334` `338` `385`(fade landed, playtest pending)
`387`(owner-validated, memory) `389`(partial built) `405`(kit-approved) `408`(scripts committed, **NOT run**) ·
`332`/`333`/`334` "Done-but-playtest-pending" per PIPELINE_STATE 06-09.
⚠ **Board-hygiene gap:** 376–382 have RESULT/commit evidence but were missing from Notion (PIPELINE_STATE 06-09).

### 2c. Foundational / questline block 290–305 (minted 2026-06-06) — mostly SPEC/READY
`290` QuestService+tracker (foundational, READY) · `291` 9-vendor Yarn pack + NPCCommandBridge quest verbs ·
`292` Keystone→Spire finale · `293` crafting tiers + legendary recipes · `294` Forgemasters' Saga Yarn+scenes ·
`295` Aegis-of-Elarion legendary set + Oathweld ward · `296` reforge-choice ending · `297` pet acquisition+slots ·
`298` pet skill catalog · `299` pet bond questlines · `300` weaponsmithing-lore integration ·
`301` party persistence (wallet-keyed; dup file `_wallet_keyed`) · `302` floating-healthbar fix (DONE) ·
`303` combat party HUD wire-to-data · `304` Brom's rumor board · `305` relic-recovery quests.
Dep chain: 290 → 291 → {293/294/295/296}, 297→298→299, 304, 305, 292.

### 2d. P0/P1 bug block 306–343 (06-08 session) — mixed
HUD overhaul `307`→`308`/`309` · `310` companion-color · `311` tree-of-life placement · `312` farm→food-node ·
`313` windmill production · `314` BuildPreviewModal cleanup · `315` enemies-walk-backwards · `316` family-spawning ·
DTT cluster `317`(grounding) `318`(aim-north) `319`(parity) `320`(loss-impact) `330`(cyan hero) `331`(hotkeys)
`332`(aim-sensitivity) · `321` missing side-gate · `322` compass-not-visible · `323` trees-all-white ·
`324` dungeon placeholder NPC/exit · `325` resource-node-does-nothing · `326` hero-facing-90° · `327` admin-trigger-wave-noop ·
`328` **CLOSED (no repro)** · `329` checkin-regression-suite (dup file: pet_deploy_timing) · `333` tree<30%→defense-modal (HIGH) ·
`335` ATB capsule/purple bug (HIGH) · `336` ATB village-wall env · `337` pet-house dialogue overlap · `338` Echo Hollow rebrand ·
`339` SaveSchema quest-versioning anchor · `340` PlayerPrefs migration · `341` backend auth-token refresh ·
`342` WebGL memory opt · `343` analytics event batching.

### 2e. Out-of-band block 352–390 (06-08/09) — UI/HUD + combat + castle
Build-mode/UI panels `352`/`353`/`355`/`356`/`357` · `354` upgrade-synergy · `358`✓ yarn-welcome ·
`359` combat-feedback · `360` companion echo outpost · `361` wave-rewards/passive-XP · `362` enemy-wave-composition ·
`363` orientation-validation gate · `364` companion-gear · `365`/`366`/`367` idle-poses/routines/town-camera ·
`368`✓ camera-regression · `369` arena-monument · `370`/`371`/`372` monument-VFX/combat-SFX/battle-music ·
`373` critical-regression gates · `374`–`378` UI fixes (char-select / `375` Yarn-threading / `376` hero-pose /
`377` dialogue-block / `378` town-HUD) · `379` echo auto-summon · `380`✓ gear-icon · `381` ATB-arena-cleanup ·
`382`✓ hero-HP · `383` castle↔outerworld seam (was ACTIVE/live-bug) · `384` castle two-level stairs ·
`385` castle camera (fade landed, playtest pending) · `386` battle-visualization · `387`✓ camera-relative move ·
`388` player-castle-as-arena-defender (SPEC) · `389` arena defense (partial) · `390` battle-potion-loadout (SPEC).

### 2f. On-board block 391–411 (06-10/11) — specs in Notion, only 405 has a repo file
`392` Warcraft-tiered building upgrades · `393` low-contrast yellow UI text · `394` build-click-no-feedback ·
`395` node/mine visual replacement · `398` Knight-still-ranged · `399` Knight melee skill set ·
`400` inventory rework · `401` blacksmith vendor presentation · `403` UNIFIED context HUD shell (RESPEC) ·
`404` combat HUD group · `405`✓ **`ElarionUiKit` UGUI design system (P0, blocks all HUD work)** ·
`406` empty shops · `407` arcane-tower tiers · `408` WebGL texture opt 223→<60MB (scripts committed, NOT run) ·
`409` magenta towers + UI `*`/`#` glyphs · `411` Town HUD ≠ `hud_mobile_town.png` (10 deviations, **BLOCKED on 405**).
`391`/`396`/`397`/`402`/`410` used on-board, rows not mirrored to repo. **413** upgradable-building-menu (READY, P1, data-driven
isUpgradable/isShoppable rule) · **414** store_stock_from_db. ⛔ **400/403/404/411 BLOCKED on 405 owner-approval gate.**

### 2g. Legacy / thematic clusters (≤288) — large, mostly DONE or superseded
- **Core loop / ATB / combat (21,46–49,68–70,81,93,94,130,169,170,259,276):** ATB FF-style party battle (169 = the big one),
  enemy-pet combat, capsule→model swap. Many OPEN/partly-built.
- **Defend-the-Tower / PatriciaLight (46,47,48,96–100,221,317–333 DTT subset):** **PILLAR REMOVED 2026-06-09** —
  module + scene deleted, only `Resources/PatriciaLight/tower2` kept. These WOs are frozen history.
- **VFX / audio / mobile-perf (37,50–66,85,191,195):** VFXManager, weather, LOD, culling, spell VFX. Mostly DONE.
- **Monetization / Solana (72–80,236):** WalletService/PackStore/Glimmer/staking — **~70% BUILT, do NOT greenfield**
  (memory: monetization-stack-already-built). Scene-wiring DISABLED (PanelSettings/UXML trap).
- **Village/castle geometry (22,26,101–105,136,137,156–168,176–183,204,247,278–280,311,321,384):** VillageSceneBuilder
  single-writer lane; many DONE via rebakes (`8f4c6f3`, `5834479`, `071e478`). **Village.unity ABANDONED;
  Village2 = raid target; MainCastle_Hall = home hub** (memory + PIPELINE_STATE 06-09).
- **World / regions / nodes / harvest (24,27,107,111,141–165,193,205,216,228,239,244,245):** OuterWorld streaming,
  zone foundation (164 keystone), crystal mines, claimable nodes, wandering tribes. Mostly OPEN/phased.
- **Character pipeline / animation (32,138,140,174,184,190,202,217–219,234,283–288,326,363–366,376):** CC5/AccuRIG
  import harness, shared animators, idle poses. Anim-library 283/284/285/286 DONE.
- **Hero-select / onboarding / narrative (42,185,222–227,230,238,277,290–291,304,358):** hero cards, tutorial, Yarn.
- **Jupiter / crypto swap (43,44,45,210):** `43_..._SUPERSEDED` is dead; phase1/2/3 swap panel — backend-gated.
- **Build mode (108,113,114,181,215,239,282,292,314,334,392,407):** keystone player-build lane. 108 DONE.

### 2h. Duplicate-numbered WO files (collision cleanup pending — Lane 0 item)
Repo has TWO+ files for: **43, 46, 106, 107, 108, 109, 110, 111, 129, 136, 137, 138, 152, 159, 179, 181,
253, 254, 255, 256, 257, 279, 280, 282, 301, 329, 330, 331, 333, 334.** Plus Notion carries a *divergent*
328–339 P0-bug block vs this repo's 328–339. **Do not reuse any of these numbers** (renumber from 391+ when cleaned).

### 2i. Dead / held WO files
- `WORK_ORDER_43_jupiter_swap_panel_SUPERSEDED.md` — dead, ignore.
- `WORK_ORDER_282_heroes_resources_to_addressables.HOLD.md` — HELD (daytime play-verified session).
- `RESULT.md` (bare, root) + `WO-124_REVIEW.md` + `WO-234_CLI_READY_SPEC.md` — one-off review/result fragments.
- `outpost_base_footprint` / `WORK_ORDER_outpost_base_footprint` — non-numbered footprint spec.

---

## 3. STATE / PROCESS DOCS (non-WO) — by category

### 3a. Pipeline / process (current)
| Doc | Gist | Status |
|---|---|---|
| `BUG_WORKFLOW.md` | How bugs flow + lane rules (referenced by SESSION_START_HERE as canonical process). | CURRENT |
| `BUG_LIST.md` | Live playtest bug punch-list, tagged to user stories, lane-grouped. **Dated 2026-05-31** — lists 14 open (1 P0 = WO-173) but WO-173/166/158 since closed → **STALE snapshot**. | STALE (05-31) |
| `PUNCHLIST.md` | Outstanding punch-list. | Snapshot |
| `PIPELINE.md` / `PIPELINE_REFILL_LOG.md` | Pipeline overview + nightly refill log. | Living |
| `CLI_GATEKEEPER_PLAYBOOK.md` | CLI agent batchmode/commit playbook. | CURRENT |
| `AGENT_OPENERS.md` / `PARALLEL_LANES.md` | Agent prompt openers + simultaneous-lane map. | CURRENT |
| `LANE_STATUS_LOG.md` | Lane status running log. | Living |

### 3b. Architecture / core
`ARCHITECTURE_REFERENCE.md` (earlier-phase ref) · `CORE_ARCHITECTURE_PLAN.md` (canonical pro structure: TD+dungeon+Solana+mobile+monetization) ·
`COHESION_AUDIT_AND_DECISIONS.md` · `BACKLOG_SILOS.md` + `SILO_FILE_MIGRATION_MAP.md` (**silo restructure SKIPPED** — memory) ·
`IMPLEMENTATION_PHASES.md`. Note: the **binding** architecture law lives in `docs/ARCHITECTURE_PRINCIPLES.md` (per CLAUDE.md), not these.

### 3c. Design specs (root)
`DESIGN_CORE_LOOP_AND_STRUCTURE.md` · `DESIGN_ELARION_CITY.md` · `DESIGN_VILLAGE_DISTRICTS.md` ·
`DESIGN_FORGEMASTERS_SAGA_LEGENDARY.md` · `DESIGN_PET_SYSTEM.md` · `DESIGN_SPEC_ATB_UI_FINAL_FANTASY_STYLE.md` ·
`DESIGN_VENDOR_STORYLINES_AND_QUESTLINES.md` (The Dimming meta-arc spine) · `ENEMY_WAVE_DESIGN.md` ·
`SPELL_BOOK_DESIGN.md` (targeting BUILT, creative spells SPEC) · `COMBAT_FEEL_PRIORITY_STACK.md` ·
`VILLAGE_SIZE_SPEC.md` · `WALL_LAYOUT_GUIDE_mirza_beig.md` · `ECONOMY_FOUNDATION_CODE.md` ·
`LORE_ELARION_WEAPONSMITHING.md` · `INTRO_VIDEO_FIRST_10_SECONDS.md` / `_SECONDS_10_20.md` ·
`BUILD_MODE_IMPROVEMENTS_ROADMAP.md` · `TITLE_SCREEN_BUTTON_STYLING_GUIDE.md` · `CityManifest.draft.README.md` · `DEF-TARGET-SELECTION.md`.
These drive the 290–305 + 392+ WO mappings (see backlog §"Story→WO mapping").

### 3d. Audio manifests / guides
`AUDIO_ECHO_THEME_MANIFEST.md` · `AUDIO_MUSIC_MANIFEST.md` · `SUNO_BATTLE_MUSIC_PROMPTS.md` ·
`ATB_DEBUGGING_GUIDE.md` · `DEPLOY_WEBGL_ITCH_GUIDE.md` · `WEBGL_ASSET_REVIEW.md` · `AM_VERIFY_CHECKLIST.md` ·
`ANIMATION_REQUIREMENTS_NOTICE.md` · `QA_CHECKLIST_FILLED.md` (marks 107–111 wired by inspection).

### 3e. Handovers / session snapshots (point-in-time; trust newest)
**Newest = authoritative state:** `HANDOVER_2026-06-10.md` (the most recent — CLI session; see §4).
Others (frozen): `HANDOVER_2026-06-09.md`, `HANDOVER_2026-06-09_hero-fixes.md`, `HANDOVER_2026-06-02.md`,
`HANDOVER_NEXT_CLI.md`, `HANDOVER_OVERNIGHT_2026-06-06.md`, `HANDOVER_VILLAGE2_SWAP.md`,
`MORNING_REPORT_2026-06-09.md`, `RUNNING_PIPELINES_HANDOVER_2026-06-06_PM.md`, `OVERNIGHT_STATUS_castle.md`,
`CC_OVERNIGHT_HANDOVER.md`, `CC_MONETIZATION_RECONCILIATION.md`, `SESSION_HANDOFF.md`, `SHIFT_CHANGE_2026-06-01.md`,
`STATUS_2026-06-02_AM.md`. **Overnight queues/reports:** `OVERNIGHT_BATCH.md`, `OVERNIGHT_HANDOFF_2026-05-29.md`,
`OVERNIGHT_QUEUE_2026-05-30/31/06-03/06-06.md`, `OVERNIGHT_REPORT(_2026-05-25).md`.
**CLI queue/dispatch:** `CLI_DISPATCH_2026_06_03.md`, `CLI_HANDOVER_2026-06-06.md`, `CLI_QUEUE_2026_06_01.md`,
`QUEUE_HEALTH_2026-06-03/04.md`, `WORK_QUEUE_CONSOLIDATED_2026_06_01.md`, `PIPELINE_SESSION_2026_06_01.md`.
**Execution plans (stale):** `ORCHESTRATION_PLAN.md` (05-28 VFX sprint — PROJECT_INDEX says DO NOT use for current state),
`ORCHESTRATION_LIVE.md`, `EXECUTION_PLAN_REQUEST.md`, `FINAL_EXECUTION_PLAN_2026_06_01.md`,
`REVISED_EXECUTION_PLAN.md`, `PARALLEL_EXECUTION_BRIEF_2026_06_01.md`, `WO_AUDIT_2026_06_01.md`.
**Playtest cards / buglogs:** `PLAYTEST_2026-06-06_BATCH_307-328.md`, `PLAYTEST_CARD_2026-06-01.md`,
`PLAYTEST_CARD_buildmode_2026-06-01.md`, `BUGLOG_playtest_2026-05-24.md`, `FIX_NOTES_2026-05-25.md`.
**Misc:** `PENDING_COMMIT.md`, `VILLAGE2_WIRING_NOTES.md`, `CASTLE_CAMERA_DIAGNOSIS_2026-06-09.md`,
`ISSUE_ANALYSIS_YARN_SPINNER_DEBUG_ELEMENT.md`, `AGENT_OPENERS.md`.

---

## 4. CURRENT GROUND-TRUTH STATE (synthesized from PIPELINE_STATE top block + HANDOVER_2026-06-10)

- **Home hub = `MainCastle_Hall`** (built by `Assets/Editor/CastleHubBuilder.cs`; owner hand-dialed, committed;
  builder NOT yet updated to reproduce the offsets — a regen would revert owner's work). Ground walkable;
  L2 ramp + castle camera playtest-pending. **OuterWorld streams additively** via `WorldSceneLoader`.
- **Village2** = raid-target stronghold. **`Village.unity` = ABANDONED / corruption-cursed — do not use.**
- **Defend-the-Tower / PatriciaLight = REMOVED 2026-06-09** (module + scene gone; only `Resources/PatriciaLight/tower2` kept).
- **🔴 OPEN BLOCKER (HANDOVER 06-10): OuterWorld runs ~1 fps ("frame by frame") even at 0 enemies.** 5-agent
  read-only RCA exhausted static analysis; moved to MEASUREMENT via `PerfDiagnostic.cs` (`c63dc03`). The one
  provable per-frame cost (`DefenseTower/ArcaneTower.Rescan()` whole-world `FindObjectsByType` every 0.4s) was
  fixed `4b5208c`; bridge scans retired to O(1) registries `463a5e8`. Owner playtest is the verdict.
- **~80 commits UNPUSHED** ahead of `origin/feat/tower-core-loop`, held for owner sign-off.
- **WO-403 unified HUD is STASHED** (`git stash`), to be redone modular (<800 lines) per `docs/WO-403_405_RECONCILIATION_AND_AM_PLAN.md`.
- **Monolith split:** 20 files >800 lines; plan in `docs/MONOLITH_SPLIT_PLAN.md`; only the provably-safe
  delete done (`BattleHud`+`BattleVfx` dead UIDocument HUD, ~1,675 lines, `01fd781`). Live one is `BattleHudUgui`.
- **WebGL:** itch rejected (`WebGL.data` 223 MB uncompressed); fix = Gzip OR run WO-408 texture-opt (committed, NOT run).
- **Store / monetization ~70% BUILT** but scene-wiring DISABLED (PackStore needs own PanelSettings; UXML doesn't render in builds).

---

## 5. FLAGS

### 5a. Stale comment-vs-code (the mandated check — CONFIRMED)
- **`Assets/_Modules/Village/Hero/HeroLocomotion.cs`** — header comment (lines 4–8) and the class XML-doc
  (lines 25–30) both claim *"Minimal kinematic transform translation … no Rigidbody, no NavMeshAgent — pure
  transform … smoothly faces the move direction."* **The code does the opposite:** it `using UnityEngine.AI;`,
  declares `private NavMeshAgent _agent` (line 205, comment "unified navigation: hero shares the enemies'
  NavMesh"), and in `Awake`/init does `_agent = GetComponent<NavMeshAgent>(); if null AddComponent<NavMeshAgent>()`
  (lines 242–243), then drives movement via `_agent.Move` and `NavMesh.SamplePosition` (lines 97, 158, 566).
  **It is a NavMeshAgent-driven locomotion, not pure transform.** This is exactly the flagged hazard class —
  the stale top comment hides the real navigation model. (Also: the comment says "hero is a primitive Capsule",
  but `Resources/Heroes/*` rigged FBX heroes override that at runtime.)

### 5b. Dead / duplicate / held docs + code
- **438 WO files for ~280 numbers** — 30 colliding numbers (§2h) plus a Notion 328–339 block divergent from repo.
- `WORK_ORDER_43_..._SUPERSEDED.md`, `WORK_ORDER_282_..._.HOLD.md` — dead/held.
- Dead code deleted this session: `BattleHud` + `BattleVfx` (old UIDocument HUD) — live HUD is `BattleHudUgui`.
- `ORCHESTRATION_PLAN.md` — explicitly stale (05-28); PROJECT_INDEX warns not to use it for current state.
- `BUG_LIST.md` — 05-31 snapshot; several "OPEN" rows (WO-173/158/166) are since closed.
- `SESSION_START_HERE.md` Order Log tables — 05-31-era, several statuses superseded by later rebakes.
- `BACKLOG_SILOS.md` / `SILO_FILE_MIGRATION_MAP.md` — describe a restructure that was **SKIPPED** (don't re-propose).

### 5c. Scene-gated / disabled / not-run systems
- **Store scene-wiring DISABLED** — `BuildMarketplace` commented out in `VillageSceneBuilder.cs` (UXML empty in
  build + shared-PanelSettings panel-collision). Re-enable only with own PanelSettings + code-built UI.
- **WO-408 texture-opt** — scripts committed, **NOT run** (WebGL still 223 MB).
- **WO-403/404 unified HUD** — stashed, blocked on **WO-405** owner-approval gate (also blocks 400/411).
- **Defend-the-Tower** — entire pillar removed; ~20 DTT WOs (317–333 subset, 46/47/96–100/221) are frozen history.
- **CastleHubBuilder** — owner hand-dialed scene committed but builder can't reproduce offsets → a regen reverts owner's work.

### 5d. Broken / contradictory
- **OuterWorld ~1 fps** open blocker (§4) — root cause unproven, awaiting owner profile verdict.
- **Numbering vs filesystem:** PROJECT_INDEX/SESSION_START_HERE say "next free 384" while the authoritative
  master/lane docs say **412** — the index docs lag the numbering authority (not wrong, just stale lines).
- **Notion vs repo WO divergence:** 376–382 had RESULT/commit evidence but were missing from the Notion board;
  Notion's 328–339 P0 block ≠ the repo's 328–339. Board hygiene incomplete.
- **Trust note (HANDOVER 06-10):** owner-mandated "no 'it's fixed' claims; only the owner's playtest is the
  verdict" — never mark a WO DONE on a green gate alone.
