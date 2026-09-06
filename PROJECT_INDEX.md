# PROJECT_INDEX — Root File Map

> ## > CORRECTED 2026-09-03 - this file's own banner below is STALE and is kept as history
>
> **Branch is `feat/synty-art-retheme`, pushed** - read it off `git status`, never off a doc. The
> single live anchor is **`CANON_GROUND_TRUTH_2026-09-03.md`**; the session narrative is
> **`docs/HANDOVER_2026-09-03_production_build.md`**. The banner beneath this one still says branch
> `wip/village2-and-f8-tickets` and anchor `CANON_GROUND_TRUTH_2026-08-16.md` - both wrong since, and
> frozen per CLAUDE.md §15 rather than rewritten in place.
>
> ⚠ **The `REGRESSION_OK 120/120 suites` figure below is likewise frozen history** - the marker on a
> fresh log now reads a different count. **Read it off the marker; that is what the line beneath already
> tells you to do.**


How to navigate the markdown files at project root without reading them all
(**118 as counted 2026-08-16 evening** — the number drifts every week; count it, do not quote it).
Code map: `Assets/_Modules/README.md`. Assets map: `Assets/README.md`.
Docs index: `docs/README.md`.

> **Branch = `wip/village2-and-f8-tickets`** *(as of 2026-08-16)*. The single live anchor of current
> reality is **`CANON_GROUND_TRUTH_2026-08-16.md`** — **read it first; if any file below contradicts
> it, the anchor wins.** Every earlier dated anchor (08-09/08-08/08-07/08-06/08-05/08-03/08-02/08-01/
> 07-26/07-22/07-19/07-18/07-13/07-12/07-08 and older) is superseded/frozen.
> **HEAD as of 2026-08-16; read `git status` for push state — never trust a copied hash.**
> Gates last emitted
> `COMPILE_GATE_OK` + `REGRESSION_OK 120/120 suites` (⚠ read the count off the marker, never off a doc —
> it moved 117 → 120 in eight hours on 2026-08-05).
> **WO next-free: read the `CLI_LANES_WO_NUMBERS.md` banner — never a number copied here.** TWO
> disjoint blocks are in use as of 2026-08-02: **main line (CLI)** and a **reserved block (UI seat)** —
> ⚠ **the ranges are deliberately NOT written here.** This line named "860–899" until 2026-08-20; that
> block CLOSED at 899 and the seat moved on, so the copy sat here stale and re-seeded the very
> collision it was meant to prevent (CLAUDE.md §2 says this in as many words). **Read both rows off
> the banner.** Each seat bumps its own banner row in the same edit as the mint.
> Dungeons are a functional end-to-end loop; the raid loop is locked to the
> COC Teleport/Deploy model; **save schema `v38`** (v35 = WO-773 Obsidian queue; v36 = WO-834
> `everBuiltStructureIds`; v37 = WO-911 the per-job paid basket; v38 = WO-934 the army loadout bank).
> **Read it off `SaveSchema.CurrentVersion` (`Assets/_Modules/Core/State/SaveSchema.cs:41`)**, never
> off this line. Files this
> index historically called "living" that read as pre-pivot (tower-defense + Solana + party-of-4 +
> Blink hero rig) are STALE — corrected per `docs/_archive/root/CANON_READINESS_LEDGER_2026-06-26.md`.

## Living documents (read these; they are current)

| File | Purpose |
|---|---|
| `HOME.html` | **Generated wiki-style home page over the doc lake** (`python tools/home_build.py`) — one click to rules / architecture / north star / board / catalogs / the VFX+sound organization registries; derived view, never hand-edit, dead links fail the generator (WO-943) |
| `CANON_GROUND_TRUTH_2026-08-16.md` | **The single live anchor of current reality — read FIRST** (delta over 08-06 → 08-05 → 08-03 → 08-02 → 08-01 → 07-26 → the deep `CANON_GROUND_TRUTH_2026-07-22.md` module anchor; all earlier anchors frozen/bannered). ⚠ This row named the **08-06** anchor until 2026-08-16 while the banner at the top of this same file already said 08-16 — one file, two answers. There is exactly ONE live anchor: the newest dated `CANON_GROUND_TRUTH_*.md`. If this row and the banner ever disagree again, trust neither and `ls CANON_GROUND_TRUTH_*.md` |
| `docs/reference/SESSION_INDEX_2026-08-06.md` | The 2026-08-05/06 session as a **known dictionary** — every defect with its proving line, every **REFUTED** belief with the evidence that killed it, the owner rulings, the open items |
| `docs/reference/DEFECT_INDEX_2026-08-05.md` | Same, for the earlier half of 2026-08-05 (dungeon P0, wallet, catalog fallback drift). **Frozen ledger** |
| `KEY_FACTS.md` | The living fact sheet + ⭐ NORTH STAR state — always current, updated in place |
| `START_HERE.md` | The single entry point / boot sequence a fresh CLI session follows |
| `CLI_SESSION_PLAYBOOK.md` | The executed step-by-step script of a whole CLI session (boot -> routing -> see the truth -> close the tree -> Codex lane -> instrument -> ship -> close), a measured RECEIPT per step, STOP rules (owner directive 2026-09-05) |
| `CLAUDE.md` | **Agent rules — read first, non-negotiable** (§15 = canon maintenance) |
| `SESSION_CANON_LOADER.md` | At-a-glance SME primer (live thread + current state + key files) |
| `docs/HANDOVER.md` | Operator's manual + newest ★★ session block (**2026-08-06**) |
| `PIPELINE_STATE.md` | Pipeline/build state (current block re-dated **2026-08-06**; deep history below it) |
| `docs/COMBAT_PIVOT_NORTHSTAR.md` | Single-Knight pivot — supersedes Blink/party-of-4 canon |
| `docs/MASTER_CATALOG.md` | Verified-from-code SME catalog (code mechanics current) |
| `docs/GROK_MEMORY.md` | Grok (AI PM) fast path |
| `docs/qa/UI_REVIEW_2026-08-01.md` | Frozen 20-panel real-pixel readability review |
| `SUNDAY_HOUSEKEEPING.md` | The weekly full-sweep + housekeeping ritual (BINDING) |
| `PARALLEL_LANES.md` | Which work lanes can run simultaneously |
| ~~`PUNCHLIST.md`~~ | ⚠ **NOT living — frozen 2026-05-27, PRE-PIVOT** (its "Defend-the-Tower transition" framing describes a module deleted 06-09). History only; the live backlog is `WorkOrders/` + the DERIVED `BOARD.html`. ⚠ This row said "the Notion Work Orders DB" until 2026-08-20 — **Notion is RETIRED** (owner ruling 2026-08-08) and so is Linear (08-09). |
| `BOARD.html` | **The live board — GENERATED from the repo** (`python tools/board_build.py`, ~2 s; parses `WorkOrders/*.md` Status lines + RESULT markers + the numbering banner). Derived view, **never hand-edit**; regenerate at session boot and before any board read. See `docs/BOARD.md` |
| `AGENT_OPENERS.md` | Prompt openers for spawning agents |

> Point-in-time session ledgers (`CLI_PREP_2026-07-08_next-session.md`, the `RESUME_*` files, etc.)
> were moved to `docs/_archive/root/` — they are history, not current.

> ⚠ **Demoted as STALE (do not treat as current — see ledger):** `SESSION_START_HERE.md` (⛔ RETIRED — **moved to `docs/_archive/root/`**, not at root),
> `ARCHITECTURE_REFERENCE.md`, `CORE_ARCHITECTURE_PLAN.md` (TD+Solana framing), `BUG_LIST.md` +
> `BUG_WORKFLOW.md` (Linear-era), `PIPELINE.md`, `CLI_GATEKEEPER_PLAYBOOK.md` (stale branch/path).

## Work orders — `WorkOrders/WORK_ORDER_NNN_name.md`

> **WO next-free: the `CLI_LANES_WO_NUMBERS.md` banner — the SOLE authority. Never copy a number here;
> point at the banner.** As of 2026-08-02 **two disjoint blocks** are in use: the **main line (CLI)** and
> a **reserved block (UI seat)** — ⚠ **ranges deliberately not copied here; read them off the banner**
> (the "860–899" that used to sit on this line closed at 899 and went stale).
> Each seat bumps ITS OWN banner row in the SAME edit as the
> mint — skipping that bump caused FIVE collisions on 08-02 alone; collisions resolve
> first-on-disk-and-referenced-wins.
> Recent arc *(refreshed 2026-08-06)*: **865-870/878-883** UI rework set SHIPPED · **871** build-site
> workers SHIPPED · **759** boss fire breath SHIPPED · **886/887/888** VFX (death ladder / empowered-tower
> element routing / colourblind low-health tell) **SHIPPED** · **894** victory screen SHIPPED (with a
> documented deviation from its own wireframe) · **908** side-menu gear icons SHIPPED · **909** Mage/Ranger
> activation SHIPPED (**its premise — a parked `.tripo-extracted` FBX — was REFUTED**) · **884/885** READY ·
> **889/890/891/892/893** IN FLIGHT *as of 2026-08-06* (specs on disk, untracked, not implemented —
> ⚠ dated snapshot, re-read the board before acting) · **910 RESOLVED 2026-08-16** (all three talent
> trees re-authored to 3 bases branching wider — knight 3/7/8/7/7, ranger 3/5/6/6, mage 3/6/6/5;
> ranger and mage had had **no authored x/y at all**) · **848 OPEN** (restore Android stripping Medium) ·
> 904/905/906/907 SPEC. Earlier: WO-818 KayKit NPC bodies, 826 Realm Map, 830/831 Echo program, 836 catalog
> SME refresh — all SHIPPED. Some historical numbers were reused/collided
> (e.g. 677/678); a `.RESULT.md` beside a spec means it is done.

The unit of work. **Moved out of root into `WorkOrders/` 2026-06-22** to declutter
(504 spec + result files). The numbering authority `CLI_LANES_WO_NUMBERS.md` +
`WO_AUDIT_*.md` stay at root.

**Manage redesign program (WO-1418+, 2026-09-06):** `WorkOrders/ManageRedesign/` folder
holds the coordinated WO set, owner rulings (Rulings 21/23 on barracks/containers; Ruling 6 on Heart Level naming),
and the redesigned flow mapping (`docs/manage-flow-map/MAP.md`). Prerequisite audit
(`docs/PREREQUISITE_REGISTRY_2026-09-06.md`) verifies all gates and costs are covered by containers and the unified
gate.

- `WORK_ORDER_NNN_name.md` — the spec. Status line inside says if READY TO IMPLEMENT
- `WORK_ORDER_NNN_name.RESULT.md` — CLI's completion report. **If a .RESULT.md
  exists, the WO is done** — don't re-implement
- HUD-001 (this session): Dark Fantasy Mobile HUD Controller — `HUDManager.cs` + `VirtualDPadLean.cs` (rich reference layout, Lean Touch exclusive input, full integration with Economy/Heart/Wave/HeroAbilities/Build via events + reflection, D-Pad feeds locomotion loosely, self-contained drop-in, coexists with lean `VillageHudController`). See `Assets/_Modules/HUD/README_HUD.md`.
- Numbering quirks: some numbers were reused with different names (e.g. three
  WO-136s, two WO-129s/137s/152s/179s); WO numbers ≥182 supersede earlier
  same-topic WOs (e.g. WO-198 supersedes WO-129 pipeline reconciliation)
- `_SUPERSEDED` suffix = dead, ignore (e.g. `WORK_ORDER_43_..._SUPERSEDED.md`)

## Ship / release scripts (root + `tools/`) — added 2026-08-20

The index used to cover only markdown, which is how a release step could exist with nothing pointing
at it. These are the files that put a build in front of a player:

| File | What it is |
|---|---|
| `morning-ship-chain.ps1` | The distribution chain (gates → APK → **R2 content ship** → distribute). **BLOCKS** if content parity fails |
| `overnight-apk-build.ps1` | Unattended APK build; same content ship step |
| `install-apk-to-seeker.ps1` | Sideload to the Seeker over `adb`. Content ship runs `-WarnOnly` here **on purpose** — a knowingly-offline/experimental sideload is legitimate |
| `tools/r2-ship.ps1` | ⛔ **NEW 2026-08-20 (WO-1130) — THE one way content reaches players.** `push → verify → judge the MARKER`. The three scripts above all call it; none carries its own copy any more (they had already drifted). Switches: default = block, `-WarnOnly`, `-VerifyOnly` |
| `tools/r2_sync.py` | The transport underneath it (PROD-011). ⚠ **Push the PARENT `ServerData`; verify the EXPLICIT `ServerData/Android`** — the docstring at `:21` still teaches the wrong push form |

> ⛔ **Enemy and structure ART is served REMOTELY from R2 and there is NO local fallback.** Bundle
> names are **content-hashed**, so **every build needs its own push** — a previous build's push can
> never cover this one. An unpushed APK installs, launches, and shows tinted capsules with **no error
> on screen**. That shipped three times (2026-08-18, 08-19 = WO-1124, 08-20 = WO-1130).

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
