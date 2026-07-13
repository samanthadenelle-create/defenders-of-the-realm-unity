# Docs Cleanup Manifest — 2026-07-12

Owner ask: "clean up the docs folder — doesn't feel like all these need to be there."

**Method:** NOTHING deleted. Dated point-in-time snapshots and completed one-offs were
relocated with `git mv` into a single archive location, **`docs/_archive/`** (`root/` for
former repo-root files, `docs/` for former `docs/` files). History is intact
(`git log --follow`) and any move is reversible. When a file's status was ambiguous it was
**left in place** and listed under UNSURE below for an owner ruling — conservative by design.

## Tally
- **Moved to archive: 112** (79 from repo root, 33 from `docs/`)
- **Kept in place (load-bearing + active + all undated design/spec docs):** the remainder
- **UNSURE (left in place, owner to rule): see list below**

Root `.md` count was 162; `docs/*.md` was 181. Root dropped by 79, docs top-level by 33
(plus `_archive/` + `README.md` added under docs).

---

## KEEP — load-bearing set (untouched, read every session)
Left exactly where they were, per binding canon rules:
- **Root:** `START_HERE.md`, `KEY_FACTS.md`, `SESSION_CANON_LOADER.md`, `CLAUDE.md`,
  `PREFLIGHT_GATE.md`, `SAMANTHA.md`, `PROJECT_INDEX.md`,
  `CANON_GROUND_TRUTH_2026-07-12.md` (newest — older ones archived),
  `PIPELINE_STATE.md`, `CLI_LANES_WO_NUMBERS.md`,
  `MASTER_PIPELINES_BACKLOG_2026-06-06.md` (WO-numbering authority per CLAUDE.md §2),
  `NOTION_SOURCE_OF_TRUTH.md` / `NOTION_CLI_ACCESS.md` (board-of-record).
- **docs/:** `HANDOVER.md`, `MASTER_CATALOG.md` (+ `MASTER_CATALOG/` subfolder),
  `ARCHITECTURE.md`, `ARCHITECTURE_PRINCIPLES.md`, `TICKET_PIPELINE.md`,
  `INSTRUMENTATION_STANDARD.md`, `README.md`, `COMBAT_PIVOT_NORTHSTAR.md`, `NORTH_STAR.md`,
  `ARCHITECTURE_NORTH_STAR.md`, `UI_BLINK_TEMPLATE_CANON.md`, `SECURITY_AUDIT_2026-07-12.md`
  (today), the whole `SME/` dossier folder, and every `*ARCHITECTURE*` deep-dive.

**Also kept (not archived):** all undated **design / spec / lore / manifest / catalog / how-to**
docs (e.g. `DESIGN_*`, `*_SPEC`, `*_DESIGN`, `LORE_*`, `AUDIO_*_MANIFEST`, `docs/*-catalog.md`,
`docs/*_NOTES.md`, `docs/*_ARCHITECTURE.md`). These read as living reference, not point-in-time
snapshots, so they stay until the owner says otherwise.

---

## ARCHIVED — repo root -> `docs/_archive/root/` (79)
Dated reports / handovers / resumes / overnight & morning logs / queue-health / playtest cards /
dated RCA & audits / superseded CANON_GROUND_TRUTH / retired SESSION_START_HERE:

ABILITY_ICON_AUDIT_2026-07-05, BACKLOG_FULL_RCA_REPORT_2026-06-13,
BACKLOG_READINESS_CHECK_2026-06-13, BACKLOG_RECONCILIATION_2026-06-21,
BACKLOG_RECONCILIATION_2026-06-27, BOARD_STATUS_2026-06-13, BREAK_LOG_TRIAGE_2026-06-14,
BUGLOG_playtest_2026-05-24, BUILDS_AUTOPILOT_FLEET_RESULT_2026-06-17,
CANON_GROUND_TRUTH_2026-06-26, CANON_GROUND_TRUTH_2026-06-28, CANON_GROUND_TRUTH_2026-07-01,
CANON_GROUND_TRUTH_2026-07-03, CANON_GROUND_TRUTH_2026-07-08, CANON_READINESS_LEDGER_2026-06-26,
CASTLE_CAMERA_DIAGNOSIS_2026-06-09, CC_OVERNIGHT_HANDOVER, CLI_DISPATCH_2026_06_03,
CLI_HANDOVER_2026-06-06, CLI_PREP_2026-07-08_next-session, CLI_QUEUE_2026_06_01,
FINAL_EXECUTION_PLAN_2026_06_01, FIX_NOTES_2026-05-25, GROK_CLI_SESSION_HANDOFF_2026-07-09,
HANDOVER_2026-06-02, HANDOVER_2026-06-09, HANDOVER_2026-06-09_hero-fixes, HANDOVER_2026-06-10,
HANDOVER_2026-07-04_SESSION_SUMMARY, HANDOVER_OVERNIGHT_2026-06-06, KNIGHT_SNIPE_RCA_2026-06-13,
MORNING_LEDGER_2026-06-29, MORNING_REPORT_2026-06-09, MORNING_REPORT_2026-06-24,
MORNING_REPORT_2026-07-03_HUD, MORNING_REPORT_2026-07-07, MORNING_WALKTHROUGH_2026-06-17,
OVERNIGHT_2026-06-27, OVERNIGHT_AUTOPILOT_LOG, OVERNIGHT_BATCH, OVERNIGHT_HANDOFF_2026-05-29,
OVERNIGHT_QUEUE_2026-05-30, OVERNIGHT_QUEUE_2026-05-31, OVERNIGHT_QUEUE_2026-06-03,
OVERNIGHT_QUEUE_2026-06-06, OVERNIGHT_REPORT, OVERNIGHT_REPORT_2026-05-25, OVERNIGHT_STATUS_castle,
OVERNIGHT_SUMMARY_2026-06-25, PARALLEL_EXECUTION_BRIEF_2026_06_01, PIPELINE_SESSION_2026_06_01,
PLAYTEST_2026-06-06_BATCH_307-328, PLAYTEST_CARD_2026-06-01, PLAYTEST_CARD_buildmode_2026-06-01,
PLAYTEST_CHECKLIST_2026-06-13, QUEUE_HEALTH_2026-06-03, QUEUE_HEALTH_2026-06-04,
RESUME_2026-06-30_seam-unstack, RESUME_2026-07-02_overnight, RESUME_2026-07-03_morning,
RESUME_2026-07-03_pipeline-handoff, RESUME_2026-07-04_overnight-ui-100,
RESUME_2026-07-05_skeleton-family-handoff, RESUME_2026-07-08_overnight-f8-sweep,
RUNNING_PIPELINES_HANDOVER_2026-06-06_PM, SEAM_RCA_2026-06-13, SESSION_HANDOVER_2026-06-20_overnight,
SESSION_RESUME_2026-06-20, SESSION_START_HERE (self-flagged RETIRED), SESSION_WRAP_2026-06-13,
SETTINGS_PANELSETTINGS_RCA_2026-06-13, SHIFT_CHANGE_2026-06-01, STASH_AUDIT_2026-07-05,
STATUS_2026-06-02_AM, WIGHT_TRIPO_FIX_2026-06-13, WO-468_SEAM_RCA_2026-06-27,
WORK_QUEUE_CONSOLIDATED_2026_06_01, WO_AUDIT_2026_06_01, WO_CLOSE_LIST_2026-06-27.

## ARCHIVED — `docs/` -> `docs/_archive/docs/` (33)
ANIMATION_DOSSIER_2026-07-03, ARCHIVED_ISSUES_2026_06_04, BACKLOG_SNAPSHOT_2026-06-23,
BACKLOG_TRIAGE_2026-06-04, BLINK_OBSIDIAN_UI_UNDERSTANDING_2026-06-28,
COVERAGE_FINDINGS_LEDGER_2026-07-08, COVERAGE_VALUE_SCORECARD_2026-07-08,
HANDOVER_2026-06-12_overnight, LAST_100_BUGS_AUDIT_2026-07-08, MONETIZATION_REVIEW_2026-07-02,
ONBOARDING_FLOW_AUDIT_2026-07-03, PM_BOARD_2026-07-03, PUBLISHER_CRITIQUE_2026-07-03,
QA_player_sanity_pass_2026-05-30, RCA_DIALOGUE_DOUBLE_FRAME_2026-07-07, RCA_WEAPON_OFFSETS_2026-07-07,
REUSABILITY_AUDIT_2026-06-03, ROI_PLAN_2026-06-03, SESSION_2026-06-20_yarn-pivot,
SESSION_HANDOFF_2026-06-16, SESSION_SUMMARY_2026-06-16_overnight, STRUCTURE_TRANSFORM_CENSUS_2026-07-08,
TICKET_LOG_2026-06-12, TICKET_TRIAGE_2026-06-13, UI_BLINK_CONFORMANCE_AUDIT_2026-07-02,
UI_COVERAGE_MATRIX_2026-07-03, VISION_GAP_ANALYSIS_2026-05-30, WEAPON_TRANSFORM_CENSUS_2026-07-07,
WO-403_405_RECONCILIATION_AND_AM_PLAN, WO466_BLINK_UI_FINDINGS_2026-06-16, WO_AUDIT_2026-06-18,
WO_SILO_PLAN_2026-06-18, acceptance_verification_2026-05-30.

---

## UNSURE — LEFT IN PLACE, owner to rule
Undated files whose status (live vs. stale) I could not determine from the name/content
without risking a live reference. All remain at their original path. Top candidates for a ruling:

1. `RESULT.md` (root) — generic "latest result"; **referenced** by PROJECT_INDEX & PIPELINE_STATE. Likely a rolling pointer — confirm if it should be archived-per-date.
2. `SESSION_HANDOFF.md` (root) — **referenced** by PROJECT_INDEX; rolling "current handoff"? Or stale snapshot to archive.
3. `PENDING_COMMIT.md` (root) — looks like a transient work-in-progress note.
4. `HANDOVER_NEXT_CLI.md` (root) — rolling next-session handoff vs. stale.
5. `HANDOVER_VILLAGE2_SWAP.md` (root) — feature handoff; is the Village2 swap still in flight?
6. `ORCHESTRATION_LIVE.md` / `ORCHESTRATION_PLAN.md` (root) — "live" implies current; confirm.
7. `PIPELINE.md` / `PIPELINE_REFILL_LOG.md` / `LANE_STATUS_LOG.md` (root) — rolling logs vs. superseded by PIPELINE_STATE.
8. `GROK_SYNC_PACK.md` / `GROK_WORK_ORDERS_FULL_AUDIT.md` (root) — escalation handoff packs; keep if Grok loop active.
9. `EXECUTION_PLAN_REQUEST.md` / `REVISED_EXECUTION_PLAN.md` / `PARALLEL_LANES.md` (root) — planning docs, possibly superseded.
10. `PUNCHLIST.md` / `BUG_LIST.md` / `QA_CHECKLIST_FILLED.md` (root) — rolling QA lists vs. stale.

Other undated-but-possibly-stale worth a glance: `CLI_GATEKEEPER_PLAYBOOK.md`,
`COHESION_AUDIT_AND_DECISIONS.md`, `CORE_ARCHITECTURE_PLAN.md`, `SILO_FILE_MIGRATION_MAP.md`,
`WO-124_REVIEW.md`, `WO-234_CLI_READY_SPEC.md`, `CC_MONETIZATION_RECONCILIATION.md`,
`docs/diagnosis-report.md`, `docs/avalon-village-layout-spec.md` (uses the **retired** "Avalon"
name), `docs/NORTH_STAR_PROGRESS.md`, `docs/recovery-work-orders.md`, `docs/bug-triage.md`.
