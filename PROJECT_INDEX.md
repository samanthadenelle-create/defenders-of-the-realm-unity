# PROJECT_INDEX — Root File Map

How to navigate the **100** markdown files at project root without reading them all.
Code map: `Assets/_Modules/README.md`. Assets map: `Assets/README.md`.
Docs index: `docs/README.md`.

> **Branch = `wip/village2-and-f8-tickets`.** The single live anchor of current reality is
> `CANON_GROUND_TRUTH_2026-08-02.md` (a delta over 08-01 → 07-26 → the deep
> `CANON_GROUND_TRUTH_2026-07-22.md` module anchor) — **read it first; if any file below contradicts
> it, the anchor wins.** Every earlier dated anchor (08-01/07-26/07-22/07-19/07-18/07-13/07-12/07-08
> and older) is superseded/frozen.
> **WO next-free: read the `CLI_LANES_WO_NUMBERS.md` banner — never a number copied here.** TWO
> disjoint blocks are in use as of 2026-08-02: **main line (CLI)** and **860–899 reserved (UI seat)**;
> each seat bumps its own banner row in the same edit as the mint.
> Dungeons are a functional end-to-end loop; the raid loop is locked to the
> COC Teleport/Deploy model; **save schema `v36`** (v35 = WO-773 Obsidian queue; v36 = WO-834
> `everBuiltStructureIds`). Files this
> index historically called "living" that read as pre-pivot (tower-defense + Solana + party-of-4 +
> Blink hero rig) are STALE — corrected per `docs/_archive/root/CANON_READINESS_LEDGER_2026-06-26.md`.

## Living documents (read these; they are current)

| File | Purpose |
|---|---|
| `CANON_GROUND_TRUTH_2026-08-02.md` | **The single live anchor of current reality — read FIRST** (delta over 08-01 → 07-26 → the deep `CANON_GROUND_TRUTH_2026-07-22.md` module anchor; all earlier anchors frozen/bannered) |
| `KEY_FACTS.md` | The living fact sheet + ⭐ NORTH STAR state — always current, updated in place |
| `START_HERE.md` | The single entry point / boot sequence a fresh CLI session follows |
| `CLAUDE.md` | **Agent rules — read first, non-negotiable** (§15 = canon maintenance) |
| `SESSION_CANON_LOADER.md` | At-a-glance SME primer (live thread + current state + key files) |
| `docs/HANDOVER.md` | Operator's manual + newest ★★ session block (**2026-08-02**) |
| `PIPELINE_STATE.md` | Pipeline/build state (current block re-dated **2026-08-02**; deep history below it) |
| `docs/COMBAT_PIVOT_NORTHSTAR.md` | Single-Knight pivot — supersedes Blink/party-of-4 canon |
| `docs/MASTER_CATALOG.md` | Verified-from-code SME catalog (code mechanics current) |
| `docs/GROK_MEMORY.md` | Grok (AI PM) fast path |
| `docs/qa/UI_REVIEW_2026-08-01.md` | Frozen 20-panel real-pixel readability review |
| `SUNDAY_HOUSEKEEPING.md` | The weekly full-sweep + housekeeping ritual (BINDING) |
| `PARALLEL_LANES.md` | Which work lanes can run simultaneously |
| ~~`PUNCHLIST.md`~~ | ⚠ **NOT living — frozen 2026-05-27, PRE-PIVOT** (its "Defend-the-Tower transition" framing describes a module deleted 06-09). History only; the live backlog is the Notion Work Orders DB + `WorkOrders/`. |
| `AGENT_OPENERS.md` | Prompt openers for spawning agents |

> Point-in-time session ledgers (`CLI_PREP_2026-07-08_next-session.md`, the `RESUME_*` files, etc.)
> were moved to `docs/_archive/root/` — they are history, not current.

> ⚠ **Demoted as STALE (do not treat as current — see ledger):** `SESSION_START_HERE.md` (⛔ RETIRED — **moved to `docs/_archive/root/`**, not at root),
> `ARCHITECTURE_REFERENCE.md`, `CORE_ARCHITECTURE_PLAN.md` (TD+Solana framing), `BUG_LIST.md` +
> `BUG_WORKFLOW.md` (Linear-era), `PIPELINE.md`, `CLI_GATEKEEPER_PLAYBOOK.md` (stale branch/path).

## Work orders — `WorkOrders/WORK_ORDER_NNN_name.md`

> **WO next-free: the `CLI_LANES_WO_NUMBERS.md` banner — the SOLE authority. Never copy a number here;
> point at the banner.** As of 2026-08-02 **two disjoint blocks** are in use: the **main line (CLI)** and
> the **860–899 reserved block (UI seat)**. Each seat bumps ITS OWN banner row in the SAME edit as the
> mint — skipping that bump caused FIVE collisions on 08-02 alone; collisions resolve
> first-on-disk-and-referenced-wins.
> Recent arc: WO-818 (KayKit NPC bodies, SHIPPED), 819/820/823 (singleton /
> army-gate / hardening, SHIPPED, PO felt-verify open), 825-829 immersive Realm Map program (826 SHIPPED;
> 827-829 READY), **830/831 Echo program SHIPPED**, 836 catalog SME refresh SHIPPED, 842-844/849/850/852
> SHIPPED, **848 OPEN** (restore Android stripping Medium), **851 spec-only**, **860 shipped / 861 in
> flight / 862 minted** (UI-seat block). Some historical numbers were reused/collided
> (e.g. 677/678); a `.RESULT.md` beside a spec means it is done.

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

## Design docs (root-level; deeper specs live in `docs/`)

`DESIGN_CORE_LOOP_AND_STRUCTURE.md`, `DESIGN_ELARION_CITY.md`,
`DESIGN_VILLAGE_DISTRICTS.md`, `ENEMY_WAVE_DESIGN.md`, `SPELL_BOOK_DESIGN.md`,
`COMBAT_FEEL_PRIORITY_STACK.md`, `VILLAGE_SIZE_SPEC.md`,
`WALL_LAYOUT_GUIDE_mirza_beig.md`, `ECONOMY_FOUNDATION_CODE.md`,
`INTRO_VIDEO_FIRST_10_SECONDS.md`, `INTRO_VIDEO_SECONDS_10_20.md`,
`CityManifest.draft.README.md`, `DEF-TARGET-SELECTION.md`,
`CORE_ARCHITECTURE_PLAN.md` (historical pre-pivot architecture plan — TD + Solana framing; the live architecture hub is `docs/ARCHITECTURE.md`)

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
