# PROJECT_INDEX — Root File Map

How to navigate the ~370 markdown files at project root without reading them all.
Code map: `Assets/_Modules/README.md`. Assets map: `Assets/README.md`.
Docs index: `docs/README.md`.

> **Branch = `wip/village2-and-f8-tickets`**. The live anchor of current reality is
> `CANON_GROUND_TRUTH_2026-07-03.md` (updated 2026-07-03). Several files this index historically called "living/current" are now
> STALE (pre-pivot: tower-defense + Solana + party-of-4 + Blink) — corrected per `CANON_READINESS_LEDGER_2026-06-26.md`.

## Living documents (read these; they are current)

| File | Purpose |
|---|---|
| `CANON_GROUND_TRUTH_2026-07-03.md` | **The single live anchor of current reality — read FIRST** (supersedes the 07-01 / 06-28 / 06-26 anchors, now frozen) |
| `RESUME_2026-07-03_morning.md` | **The live work thread / session resume** — 07-02→03 convergence session results + owner decisions + next steps |
| `RESUME_2026-06-30_seam-unstack.md` | Seam un-stack (WO-453) resume path — still parked as of 07-03; FROZEN-VALID |
| `CLAUDE.md` | **Agent rules — read first, non-negotiable** (§15 = canon maintenance) |
| `SESSION_CANON_LOADER.md` | At-a-glance SME primer (current state + key files) |
| `docs/HANDOVER.md` | Operator's manual + newest 2026-07-03 session block |
| `PIPELINE_STATE.md` | Pipeline/build state (current block + 2026-07-03 delta at top) |
| `docs/COMBAT_PIVOT_NORTHSTAR.md` | Single-Knight pivot — supersedes Blink/party-of-4 canon |
| `docs/MASTER_CATALOG.md` | Verified-from-code SME catalog (code mechanics current) |
| `PARALLEL_LANES.md` | Which work lanes can run simultaneously |
| `PUNCHLIST.md` | Outstanding punch-list items |
| `AGENT_OPENERS.md` | Prompt openers for spawning agents (current architecture, updated 2026-06-27) |

> ⚠ **Demoted as STALE (do not treat as current — see ledger):** `SESSION_START_HERE.md` (⛔ RETIRED — see banner),
> `ARCHITECTURE_REFERENCE.md`, `CORE_ARCHITECTURE_PLAN.md` (TD+Solana framing), `BUG_LIST.md` +
> `BUG_WORKFLOW.md` (Linear-era), `PIPELINE.md`, `CLI_GATEKEEPER_PLAYBOOK.md` (stale branch/path).

## Work orders — `WorkOrders/WORK_ORDER_NNN_name.md`

The unit of work. **Moved out of root into `WorkOrders/` 2026-06-22** to declutter
(504 spec + result files). The numbering authority `CLI_LANES_WO_NUMBERS.md` +
`WO_AUDIT_*.md` stay at root.

- `WORK_ORDER_NNN_name.md` — the spec. Status line inside says if READY TO IMPLEMENT
- `WORK_ORDER_NNN_name.RESULT.md` — CLI's completion report. **If a .RESULT.md
  exists, the WO is done** — don't re-implement
- HUD-001 (this session): Dark Fantasy Mobile HUD Controller — `HUDManager.cs` + `VirtualDPadLean.cs` (rich reference layout, Lean Touch exclusive input, full integration with Economy/Heart/Wave/HeroAbilities/Build via events + reflection, D-Pad feeds locomotion loosely, self-contained drop-in, coexists with lean `VillageHudController`). See `Assets/_Modules/HUD/README_HUD.md`.
- Numbering quirks: some numbers were reused with different names (e.g. three
  WO-136s, two WO-129s/137s/152s/179s); WO numbers ≥182 supersede earlier
  same-topic WOs (e.g. WO-198 supersedes WO-129 pipeline reconciliation)
- `_SUPERSEDED` suffix = dead, ignore (e.g. `WORK_ORDER_43_..._SUPERSEDED.md`)
- **WOs 596–602** (2026-07-02→03 arc): 596 player bug report · 597 bot popup-close oracle · 598 vendor wares content mapping · 599 combat locks modals · 600 uncraftable recipe ingredients · 601 NPC portrait art brief · 602 way back home ("Enter Elarion" return crossings). Numbering runs through **602**.

## Design docs (root-level; deeper specs live in `docs/`)

`DESIGN_CORE_LOOP_AND_STRUCTURE.md`, `DESIGN_ELARION_CITY.md`,
`DESIGN_VILLAGE_DISTRICTS.md`, `ENEMY_WAVE_DESIGN.md`, `SPELL_BOOK_DESIGN.md`,
`COMBAT_FEEL_PRIORITY_STACK.md`, `VILLAGE_SIZE_SPEC.md`,
`WALL_LAYOUT_GUIDE_mirza_beig.md`, `ECONOMY_FOUNDATION_CODE.md`,
`INTRO_VIDEO_FIRST_10_SECONDS.md` / `_SECONDS_10_20.md`,
`CityManifest.draft.README.md`, `DEF-TARGET-SELECTION.md`,
`CORE_ARCHITECTURE_PLAN.md` (root-level canonical architecture for the Unity 6 mobile TD + dungeon + Solana game)

## Guides

`DEPLOY_WEBGL_ITCH_GUIDE.md`, `ATB_DEBUGGING_GUIDE.md`, `WEBGL_ASSET_REVIEW.md`,
`AM_VERIFY_CHECKLIST.md`

## Historical / dated session files (context only — do not treat as current)

Anything matching these patterns is a point-in-time snapshot; trust the newest
date, prefer living docs above:

- `HANDOVER_*`, `SESSION_HANDOFF.md`, `SHIFT_CHANGE_*`, `STATUS_*`
- `OVERNIGHT_*` (queues, reports, handoffs, batches)
- `CLI_QUEUE_*`, `CLI_DISPATCH_*`, `QUEUE_HEALTH_*`, `WORK_QUEUE_CONSOLIDATED_*`
- `ORCHESTRATION_*`, `EXECUTION_PLAN_*`, `FINAL_EXECUTION_PLAN_*`,
  `REVISED_EXECUTION_PLAN.md`, `PARALLEL_EXECUTION_BRIEF_*`, `PIPELINE_SESSION_*`
- `PLAYTEST_CARD_*`, `BUGLOG_*`, `FIX_NOTES_*`, `WO_AUDIT_*`, `WO-*_REVIEW.md`,
  `RESULT.md`
- `BACKLOG_SILOS.md`, `SILO_FILE_MIGRATION_MAP.md` (silo restructure was SKIPPED),
  `COHESION_AUDIT_AND_DECISIONS.md`, `CC_*` (CC handover/reconciliation),
  `IMPLEMENTATION_PHASES.md`, `VILLAGE2_WIRING_NOTES.md` (Village2 swap context),
  `HANDOVER_VILLAGE2_SWAP.md`

> Maintenance: new living docs get a row in the first table; new file-name
> patterns get added to the right section.
