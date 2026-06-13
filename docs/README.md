# docs/ — Index

~100 files. Find your category, don't grep blind. Root-level project files are
indexed separately in `../PROJECT_INDEX.md`.

## Start here / canon

- `NORTH_STAR.md`, `NORTH_STAR_PROGRESS.md` — vision + progress against it
- `DESIGN-DECISIONS.md` — **binding creative decisions** (Elarion naming, no Keep, etc.)
- `GAME_DESCRIPTION.md`, `BRAND_BIBLE.md`, `PI_PITCH.md`, `whitepaper.md`

## Asset catalogs & pack notes (check before referencing any prefab)

- `kaykit-asset-catalog.md`, `polyperfect-asset-catalog.md` — **the two prefab catalogs**
- `INSTALLED_PACKS_INDEX.md` — master list of installed packs
- Pack notes: `KAYKIT_NOTES.md`, `POLYPERFECT_NOTES.md`, `QUATERNIUS_NOTES.md`,
  `MIRZABEIG_VFX_NOTES.md`, `LANA_RPG_VFX_NOTES.md`, `SPELLS_PACK_NOTES.md`,
  `MAGIC_VFX_LIBRARY.md`, `MASTER_ASSET_REFERENCE.md`
- Library notes: `LEANTOUCH_NOTES.md`, `UNITASK_NOTES.md`, `YARNSPINNER_DIALOGUE_NOTES.md`
- `Assets/_Modules/HUD/README_HUD.md` — Dark Fantasy Mobile HUD (HUD-001) setup, Lean Touch exclusive input, integration wiring (Economy/Heart/Wave/HeroAbilities/Build), D-Pad locomotion tie, prefab + acceptance steps.

## Architecture

- `CORE_ARCHITECTURE_PLAN.md` — **root canonical plan** (professional structure, recommended folders, TD + dungeon + native Solana wallets + mobile + URP + cosmetic/seasonal monetization)
- `ARCHITECTURE_NORTH_STAR.md`, `ENGINE_MASTER_PLAN.md`, `WORLD_ENGINE_ARCHITECTURE.md`
- `ZONE_STREAMING_ARCHITECTURE.md`, `BUILD_MODE_ARCHITECTURE.md` (+ lowercase dup
  `build-mode-architecture.md`), `CHARACTER_ARCHITECTURE.md`, `MONSTER_FAMILY_ARCHITECTURE.md`
- `CATALOG_SYSTEM.md`, `refactor-feature-modules-spec.md`, `addressables-implementation-plan.md`
- `ANIMATION_PIPELINE.md` — **canonical animation method** (Shared + per-type, Humanoid retarget; all current/future models)
- `unity-decisions.md`, `UNITY_BEST_PRACTICES_AUDIT.md`
- `INSTRUMENTATION_STANDARD.md` — how new code is written observable-first (FlowTrace/Guard/regression authoring standard; the *method* that operationalizes `CLAUDE.md §12`)

## Game design specs

- Combat/enemies: `ENCOUNTER_SYSTEM_DESIGN.md`, `REGION_ENEMY_ROSTER.md`,
  `enemy-codex.md`, `enemy-mob-sets-work-order.md`, `DEFENSE_DEPTH_ANALYSIS.md`,
  `DEFENSIVE_CATALOG.md`, `ALERT_INTEL_SYSTEM_DESIGN.md`, `tower-empowerment-spec.md`
- Classes/progression: `BARD_SUPPORT_CLASS_DESIGN.md`, `TALENT_TREE_V2_DESIGN.md`,
  `LEGENDARY_GEAR_DESIGN.md`, `ITEM_DROPS_CONSUMABLES_DESIGN.md`,
  `SCROLL_BLUEPRINT_SYSTEM_DESIGN.md`, `BATTLE_2D_PARTY_DESIGN.md`
- Economy: `RESOURCE_ECONOMY_DESIGN.md`, `GLIMMER_ECONOMY_OPEN_QUESTION.md`,
  `monetization-v2-spec.md`, `anti-cheat-spec.md`
- World/village: `avalon-village-layout-spec.md` (historical name; village is Elarion),
  `PLAYER_BASE_DESIGN_CATALOG_ROADMAP.md`, `PROPER_VERTICALITY_PLAN.md`,
  `world-construction-plan.md`, `elemental-codex.md`
- Dungeons: `DUNGEON_DESIGNS.md`, `dungeon-3d-healers-cottage-design.md`,
  `dungeons-3d-unity-layout-spec.md`
- Other: `CAMERA_INPUT_OVERHAUL.md`, `CHARACTER_CREATOR.md`, `CHARACTER_REFACTOR_PLAN.md`,
  `audio-mix-spec.md`

## Narrative

- `narrative-bible.md`, `STORYLINE.md`, `ECHOES_OF_ELARION_NARRATIVE.md`,
  `PARTY_OF_FOUR_STORYLINE.md`, `dungeons-storyline.md`, `regions-narrative-and-npcs.md`,
  `BRAND_NOTE_wall_segment.md`

## Backend / platform

- `v2-unity-port-spec.md`, `v2-unity-port-backend-spec.md`, `draft-backend-endpoints`
- `admin-console-spec.md`, `webgl-hosting-notes.md`, `wallets-of-record.md`

## Process / QA / audits (mostly dated — newest wins)

- `BACKLOG_TRIAGE_2026-06-04.md`, `ARCHIVED_ISSUES_2026_06_04.md`, `WO_ROI_TRIAGE.md`,
  `ROI_PLAN_2026-06-03.md`, `REUSABILITY_AUDIT_2026-06-03.md`
- `QA_player_sanity_pass_2026-05-30.md`, `acceptance_verification_2026-05-30.md`,
  `VISION_GAP_ANALYSIS_2026-05-30.md`, `bug-triage.md`, `diagnosis-report.md`,
  `village-review-suggestions.md`, `recovery-work-orders.md`, `claude-code-work-order.md`
- Subfolders: `audit/`, `qa/`, `roadmap/`, `port-notes/`

## Media

- `screenshot-*.png` — village/dungeon/dragon reference shots

> Maintenance: add new docs to the right category when you create them.
