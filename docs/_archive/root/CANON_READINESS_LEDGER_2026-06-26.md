# CANON READINESS LEDGER — WO-520 (2026-06-26)

> Master staleness ledger from a full read-only audit of **all 1090 `.md` files** in the repo,
> each read completely by a 9-partition agent fleet, classified against
> `CANON_GROUND_TRUTH_2026-06-26.md`. No guessing — verdicts are sourced from the actual file
> contents vs the HEAD commit arc / working tree / memory index / verified summaries.
>
> **Verdict legend:** CURRENT (accurate) · STALE-ACTIVE (reads as live canon but contradicts
> reality — FIX) · CONTRADICTS-CANON (direct conflict — FIX/flag) · HISTORICAL-OK (correctly-dated
> record — leave frozen) · HISTORICAL-MISLABELED (old, reads live — needs a frozen banner) ·
> DUPLICATE-SUPERSEDED.

## Totals (1090 files)

| Partition | Files | CURRENT | STALE-ACTIVE | CONTRADICTS | HIST-OK | HIST-MISLABELED | DUP |
|---|---|---|---|---|---|---|---|
| WorkOrders 0 | 135 | 1 | 0 | 1 | 119 | 9 | 5 |
| WorkOrders 1 | 135 | 0 | 6 | 0 | 126 | 0 | 3 |
| WorkOrders 2 | 135 | 17 | 0 | 0 | 107 | 8 | 3 |
| WorkOrders 3 | 133 | 73 | 1 | 0 | 59 | 0 | 0 |
| docs 0 | 104 | 61 | 11 | 3 | 26 | 2 | 1 |
| docs 1 | 104 | 23 | 3 | 1 | 57 | 5 | 0 |
| root | 129 | 18 | 17 | 6 | 78 | 9 | 0 |
| memory/ (repo) | 171 | ~150 | 7 | 0 | 2 | 6 | 0 |
| Assets/Pkg/misc | 45 | 33 | 6 | 1 | 5 | 0 | 0 |
| **TOTAL** | **1091** | **~376** | **51** | **12** | **579** | **39** | **12** |

The dominant staleness vectors, repo-wide:
1. **`feat/tower-core-loop` branch label** is stamped across the entire MASTER_CATALOG family + many headers (current = `wip/village2-and-f8-tickets`).
2. **Blink full-body hero rig** still presented as canonical (JUNKED 06-22; hero = single Tripo self-rigged Knight "Grom").
3. **Defend-the-Tower / PatriciaLight** specs reading as live (REMOVED 2026-06-09).
4. **Village.unity as home** + VillageSceneBuilder build-order (ABANDONED; home = MainCastle_Hall, raid = Village2).
5. **Cathedral Spire replaces the world-Tree** narrative fork — ⚠️ **CREATIVE DECISION, owner's call** (see below).
6. **ATB framed as an animated party battle** (canon: ATB is flat/static single-hero; animated combat lives in the OVERWORLD BattleArena).
7. **Yarn dialogue** specs (Yarn being dropped, WO-455).
8. **Linear / Solana-grant / Vercel** as live (→ Notion board / Pi-Cloudflare + self-funded / itch-live).

---

## ⚠️ CREATIVE FORK — needs the owner, do NOT auto-resolve
**Cathedral Spire vs. living world-Tree.** `docs/STORYLINE.md`, `docs/DESIGN-DECISIONS.md` (#2/#3/#20),
and `docs/GAME_DESCRIPTION.md` assert the Heart-Tree BURNED and is replaced by a **Cathedral Spire**
("Elarion-of-the-Spire", "Hold the Chord. Defend the Spire."), and say this "supersedes the Heart-Tree
premise." The combat-pivot canon (memory `combat-pivot-single-hero-northstar`, 2026-06-22, the freshest
source) keeps the **living world-tree** as the economy north star ("drive enemies back → tree grows →
spirits harvest"). These directly conflict and it is a narrative/creative decision — **flagged for the
owner; narrative docs are NOT being rewritten until she rules.**

---

## TIER 1 — load-bearing read-first canon (FIX FIRST; a fresh session trusts these)
- `SESSION_CANON_LOADER.md` — "Current State" says Hero Rig = Blink full-body. → single Tripo Knight. **[fixing this pass]**
- `docs/HANDOVER.md` — SESSION block dated 06-19 (Blink migration / WO-453 / DO-NOT-PUSH-6). → 06-26 reality. **[fixing]**
- `PIPELINE_STATE.md` — "CURRENT STATE" block dated 06-09 (branch feat/tower-core-loop, next-WO 384, WO-383 active, DTT BUILT, Village.unity home). → 06-26. **[fixing]**
- `docs/MASTER_CATALOG.md` — header branch feat/tower-core-loop + Blink hero / party-of-4 framing (code sub-sections accurate). **[fixing header/framing]**
- `PROJECT_INDEX.md` — branch/WO pointers + "tower-defense + Solana" framing. **[fixing]**
- `SESSION_START_HERE.md` — "feat/tower-core-loop GREEN + fully pushed", next-WO 384, 4-hero roster. **[fixing or retiring]**
- `CLAUDE.md` — add the WO-520 ongoing-maintenance rule. **[fixing]**

## TIER 2 — active canon / design docs presenting as live but superseded (STALE-ACTIVE)
Root: `ARCHITECTURE_REFERENCE.md`, `CORE_ARCHITECTURE_PLAN.md`, `BUG_WORKFLOW.md`, `HANDOVER_NEXT_CLI.md`,
`IMPLEMENTATION_PHASES.md`, `LANE_STATUS_LOG.md`, `CLAUDE_BEST_SUGGESTIONS_*.md`, `CLI_GATEKEEPER_PLAYBOOK.md`,
`CLI_LANES_WO_NUMBERS.md` (header only; numbers correct), `SILO_FILE_MIGRATION_MAP.md`, `AGENT_OPENERS.md`,
`DESIGN_ELARION_CITY.md`, `DESIGN_VILLAGE_DISTRICTS.md`, `DESIGN_SPEC_ATB_UI_FINAL_FANTASY_STYLE.md`,
`DESIGN_FORGEMASTERS_SAGA_LEGENDARY.md`, `DESIGN_PET_SYSTEM.md`, `DESIGN_VENDOR_STORYLINES_AND_QUESTLINES.md`,
`DESIGN_CORE_LOOP_AND_STRUCTURE.md` (CONTRADICTS — DTT prologue), `SPELL_BOOK_DESIGN.md` (CONTRADICTS — DTT),
`WO-124_REVIEW.md` (CONTRADICTS — DTT), `PENDING_COMMIT.md` (CONTRADICTS — retire), `PIPELINE.md` (CONTRADICTS — retire),
`DEPLOY_WEBGL_ITCH_GUIDE.md`, `WEBGL_ASSET_REVIEW.md`, `VILLAGE2_WIRING_NOTES.md`, `WO-234_CLI_READY_SPEC.md`.
docs/: `TOWN_LOOP_CANON.md` (tower-defense core loop — superseded), `WARDROBE_ARCHITECTURE.md` (CONTRADICTS — Blink body),
`world-construction-plan.md`, `recovery-work-orders.md`, `BLINK_NOTES.md`, `BRAND_AND_PLATFORM_CANON.md`,
`CAMERA_INPUT_OVERHAUL.md`, `ITEM_MODEL.md` (§6 Blink only + `</content>` artifact), `NORTH_STAR.md` (Rung-1 DTT),
`docs/README.md` (points at stale GAME_DESCRIPTION/BRAND_BIBLE), `STORE_EQUIP_SPEC.md` (party selector + Blink render),
`MASTER_CATALOG/docs-design.md`, `MASTER_CATALOG/docs-wo-state.md` (06-12 numbering/branch).
modules: `Assets/_Modules/BattleATB/README.md` (CONTRADICTS — animated party), `Assets/_Modules/HUD/README.md` +
`README_HUD.md` (DTT target), `Assets/_Modules/Village/README.md` (PatriciaLight folder live; omits Arena/),
`Assets/_Modules/Onboarding/README.md` (PetSelect→Village endpoint), `tools/regression/MANUAL_QA_CHECKLIST.md` +
`tools/regression/README.md` (DTT QA section).
WorkOrders (UNDATED asserting current state): `WORK_ORDER_208_webgl_rebuild_current_tree.md`,
`WORK_ORDER_277_tutorial_companion_onboarding.md`, `WORK_ORDER_278_village_rebuild_modular.md`,
`WORK_ORDER_280_go_live_blockers.md`, `WORK_ORDER_280_village2_wiring_gate.md`,
`WORK_ORDER_282_BuildPreviewModal_Premium_Rotation.md`, `WORK_ORDER_466_gear_display_equip_and_anim.md`,
`WORK_ORDER_197_zelda_overworld_combat_design.md` (CONTRADICTS — DTT live option).

## TIER 3 — HISTORICAL-MISLABELED (correct content for their date; need a frozen banner)
Root: `ORCHESTRATION_LIVE.md`, `SESSION_HANDOFF.md`, `QA_CHECKLIST_FILLED.md`, `CC_OVERNIGHT_HANDOVER.md`,
`BUG_LIST.md`, `BACKLOG_SILOS.md`, `ENEMY_WAVE_DESIGN.md`, `RUNNING_PIPELINES_HANDOVER_2026-06-06_PM.md`,
`MORNING_WALKTHROUGH_2026-06-17.md`.
docs/: `ARENA_SOLUTION.md`, `CHARACTER_CREATOR.md`, `avalon-village-layout-spec.md`,
`dungeons-3d-unity-layout-spec.md`, `refactor-feature-modules-spec.md` (retired React v1),
`YARNSPINNER_DIALOGUE_NOTES.md`.
WorkOrders DTT cluster (need "DTT REMOVED 2026-06-09" banner): `WORK_ORDER_317/318/319/320/330/331/332_dtt_*`,
`WORK_ORDER_333_village_death_no_dtt_atb_trigger.md`, plus the 100–111/179/195 pre-pivot Village.unity/Yarn block
(neutralize with a single WorkOrders-index banner rather than touching 119 files).
memory/ (repo store, superseded by the live auto-memory store): the 6 DTT memories + `city-builder-empty-map-authoring.md`.

## TIER 4 — repo `memory/` store (separate from live auto-memory)
STALE-ACTIVE direction memories now overtaken by the pivot: `scope-discipline-not-an-mmo.md` (no-skill-tree),
`companion-roster-canon.md` (party-of-4), `raid-combat-model-hero-led-warband.md`, `two-repo-lineage-divergence.md`,
`grant-submission-goal.md`, `village2-is-canonical-never-revert.md` (home-village framing), `cc5-field-test-state.md`.
**MEMORY.md index issues:** ~31 existing memory files have NO index line (incl. high-value `verify-then-regress`,
`fan-out-by-default`, `hp-b2b-architecture-law`, `world-entities-create-use-destroy`); index links to old
`Documents/defenders-unity` path in 2 entries; several dangling `[[wikilinks]]`; 3 files with empty `name:` frontmatter;
`hp-b2b-architecture-law.md` has no frontmatter at all.

## CONFIRMED CURRENT (no action) — examples
`CLAUDE.md`, `docs/ARCHITECTURE.md`, `docs/ARCHITECTURE_PRINCIPLES.md`, `docs/COMBAT_PIVOT_NORTHSTAR.md`,
`docs/TICKET_PIPELINE.md`, `docs/INSTRUMENTATION_STANDARD.md`, `docs/PATH_TO_V1.md`, `docs/PRODUCTS.md`,
`docs/MARKET_RESEARCH.md`, `OVERNIGHT_SUMMARY_2026-06-25.md`, `V1_ASSEMBLY_MAP.md`, `ECHO_WORKFORCE_SPEC.md`,
`PI_INTEGRATION_SPEC.md`, `NOTION_SOURCE_OF_TRUTH.md`, the WO-446..514 current-direction spec block, and all
correctly-dated overnight/session/playtest ledgers (579 HISTORICAL-OK).

---

## RECONCILE PLAN / STATUS
- [x] Audit all 1090 (this ledger).
- [x] TIER 1 load-bearing canon — explicit-path edits: `SESSION_CANON_LOADER.md`, `PIPELINE_STATE.md`,
      `docs/HANDOVER.md`, `docs/MASTER_CATALOG.md`, `PROJECT_INDEX.md`.
- [x] WO-520 ongoing maintenance rule installed in `CLAUDE.md` §15 + `docs/HANDOVER.md` + `SESSION_CANON_LOADER.md`.
- [x] Owner ruling: **living world-Tree is canon** (Cathedral Spire reversed) → narrative docs bannered, not rewritten.
- [x] TIER 2 STALE-ACTIVE — ~50 docs stamped with targeted top-of-file STALE/SUPERSEDED banners
      (DTT-removed / single-Knight-pivot / Village.unity-abandoned / process-stale / Spire-reversed). Bodies untouched.
- [x] TIER 3 HISTORICAL-MISLABELED — banners stamped; `WorkOrders/README.md` created to neutralize the
      ~127 frozen pre-pivot/DTT WO specs at the index level (per the audit's own recommendation).
- [x] TIER 4 — legacy repo `memory/` store: `memory/MEMORY.md` bannered (legacy/pre-pivot + superseded-notes
      list + index-incomplete note). Live auto-memory store is authoritative; WO-520 memory written there.
- Dated historical ledgers (579) — **left frozen, not rewritten.**

### Remaining (smaller, optional follow-ups — none block code work)
- Deep body-rewrites (vs banners) for `docs/WARDROBE_ARCHITECTURE.md` (Blink-body→Tripo), `docs/STORE_EQUIP_SPEC.md`
  (party-selector→single-hero), `docs/ITEM_MODEL.md` §6 + trailing `</content>` artifact — when each system is next touched.
- Legacy `memory/` per-note frontmatter banners + the ~31 missing index lines (low value; store is vestigial).
- `docs/MASTER_CATALOG/*` section files still carry the stale `feat/tower-core-loop` label (parent now corrected).
- `CANON_GROUND_TRUTH_2026-06-26.md` itself: strip the stray trailing `</content>` artifact (cosmetic).
