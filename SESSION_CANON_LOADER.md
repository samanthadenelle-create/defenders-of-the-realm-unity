# DeNelle Studios — Project Canon Loader

> ## ▶ LIVE THREAD (2026-07-18) — READ BEFORE WORKING
> **Current reality anchor = `CANON_GROUND_TRUTH_2026-07-18.md` (read it first).** Since 07-13:
> **Pi Hackathon WON** (the "July-31 deadline / build mode IS the demo" framing is RETIRED); the
> **whole-game MVVM migration is DONE** (WO-744 — every panel View binds an `IPanelViewModel`, the
> `[ui-mvvm]` conformance oracle is armed HARD-FAIL); **Room Forge merged to mainline** (WO-740–745,
> green); **save v33**; `wip` pushed to origin. Two-session shared-tree hazard is live (dungeon
> session should use its own worktree). WO banner next-free = **746**. **The 07-12 thread below is
> SUPERSEDED — do not act on its "demo readiness" framing.**
>
> ## ▶ LIVE THREAD (2026-07-12 evening) — SUPERSEDED (see the 07-18 thread above)
> Current focus = **MOBILE-WEB DEMO READINESS** (Pi hackathon **July 31**; **build mode IS the demo**
> per the player-defined-map pivot, 07-11). Tonight's arc: **WO-677/678/682/683 committed local**
> (`66b3272f`, `c963a553`, `33799026`, `965309a6`, `683b917b`); gates = `COMPILE_GATE_OK` +
> DataRegression at the 3-known-pre-exister baseline; new **SFX_WEBGL_OK oracle** swept 13 broken
> clip metas (db-proven `Loading FSB failed` SwordSwing root). **WebTrace web-debug loop PROVEN**
> end-to-end: `?trace=1` → `POST /api/trace` → Neon `analytics_events`; the CLI read path = the
> `[sig]` echo in Vercel runtime logs (`DATABASE_URL` is sensitive/unpullable). New **WebGL ship
> preview deploying tonight** (non-dev build — the giant-error-overlay class dies); prior preview
> `mexharnff` is superseded when the new URL lands; **prod UNTOUCHED**; **push HELD**. Live anchor =
> **`CANON_GROUND_TRUTH_2026-07-13.md`** (07-12 + 07-08 anchors bannered SUPERSEDED). Notes: `api/`
> lives **IN-REPO (gitignored)** — the "separate React repo" line is dead; save schema **v30**;
> **`SAMANTHA.md` + the new `START_HERE.md` are the boot gate**; WO numbering **next-free = 684**
> (677/678 collisions flagged). **BINDING: read-before-assert applies to EVERYTHING (code + non-code).**

**READ THIS FIRST on any new session (owner directive 2026-06-20).** Every CLI/agent
loads this before doing anything, to stay an SME. It is the fast-path summary; the
binding depth lives in `CLAUDE.md`, `docs/ARCHITECTURE_PRINCIPLES.md`, `docs/HANDOVER.md`,
`docs/MASTER_CATALOG.md`, and `docs/INSTRUMENTATION_STANDARD.md`.

> ## 🟥 DAY-1 BOOT — the owner should NEVER have to remind you of this
> The #1 recurring waste (owner, EVERY day, 2026-06-23): she has to re-teach a fresh CLI to read docs / canons /
> absorb memories / orchestrate / stop guessing — even though it's all written here and in memory. **Reading this
> is not doing it.** So turn ONE, unprompted, BEFORE your first task reply:
> 1. **Read + be SME:** this file + `docs/MASTER_CATALOG.md` (relevant area) + the `docs/*ARCHITECTURE*` for what you'll touch. Reuse built systems; never reinvent.
> 2. **Boot posture = VERIFY + DELEGATE + INSTRUMENT-FIRST:** delegate deep work to agents (your hands = gates + commits); on ANY non-trivial bug, READ the captured data (F8 break-log / Editor.log / FlowTrace) and cite the line BEFORE any edit. Never guess / inference-fix.
> 3. **Hold the line** (pleasing ≠ right): park off-focus shiny things into a WO; bank wins before building more.
> 4. **Never say "I'll mark it" — write the memory AND the doc in the moment** (persist in both places).
> If you catch yourself about to guess, solo-dig, or please-and-slide → STOP and do the above. The reminder being needed AT ALL is the failure to eliminate.

## Core Rules (always follow)
- **One Model:** Capability is a property on the entry. Never hard-code per type/tag.
- **Presentation never touches objects** — HUD → Core only, Village → Core only.
- **MVVM strict:** the VM holds all logic/state; the View is a dumb skin (no game-state reads).
- **Flag-gated changes only** (BlinkChrome, BuildingUpgradePanel, etc.).
- **Instrument, don't guess — THE HARD GATE (BINDING, CLAUDE.md §12):** NO code edit on a real bug
  until CAPTURED DATA proves the cause. Loggers step IN/OUT → run HEADLESS → data pinpoints → fix THAT.
  Static reading locates candidates, never concludes. Never inference-fix; it's the OPENING move, unprompted.
- **One thing at a time, fully verified before the next.**
- **Deliver complete + felt-verified. No piecemeal.**
- **Ticket pipeline (BINDING):** QA (read-only RCA, classify NEW-feature vs EXISTING) → CLI
  (implement + headless-verify) → PO (felt-verify + close). Shared board = the Task list; log
  every hand-off. Full spec: `docs/TICKET_PIPELINE.md`.

## Current State (updated 2026-07-12 evening — anchored to `CANON_GROUND_TRUTH_2026-07-12.md`)
- **Strategic placement = ALWAYS ON (2026-07-13, WO-695 ex-682):** `ff.strategicplacement` is REMOVED —
  Build → Town/Defenses/Walls tabs, movable functional storefronts and the 260w/210i core-kit seed are
  the unconditional path; New Game = the BLANK template (+ one FTUE grace-default Forge record);
  existing saves migrate once via the v30 one-shot writer.
- **Branch:** `wip/village2-and-f8-tickets` (NOT `feat/tower-core-loop` — stale). **HEAD = tonight's WO-677/678/682/683 arc (through `683b917b`), ~100 commits ahead of origin, push HELD for owner word.** Save schema **v30** (v29 heroLevel/heroXp/heroLifetimeXp, F8-47). Fresh headless gates = `COMPILE_GATE_OK` + DataRegression at the 3-known-pre-exister baseline.
- **MOBILE-WEB DEMO READINESS is the live focus** (Pi hackathon July 31; build mode IS the demo — player-defined-map pivot 07-11), riding on the feel-arc/F8 program's quality bar (ten-year-old test). **WebGL ship preview deploying tonight** (non-dev build; `mexharnff` superseded when the new URL lands); **prod untouched** (Pi build at `defenders-of-the-realm-v2.vercel.app`). **Next big lane = WO-614 skill-tree solo rework** (RULED, READY).
- **Title:** **"Echoes of Elarion"** (chapter) within the **"Defenders of the Realm"** series; tagline **"Hold the last light."** (WO-570).
- **Combat space:** WO-584 consolidation (READY) — one warp-in space primitive, 3 skins (dungeon/outpost/arena), ownership flip; replaces flat ATB dungeon (`ff.atbdungeon` OFF).
- **Game:** Echoes of Elarion / Defenders of the Realm (Unity 6 / URP). **V1 = ONE controllable hero
  (Knight "Grom") in an overworld with isolated real-time BattleArena combat.** Base-defense/tower-defense
  is V2-gated behind `ff.basebuilding`. (itch web build LIVE; Solana→Pi/Cloudflare backend; Vercel LIVE — prod = 07-01 Pi build, preview = 07-03 convergence build.)
- **Hero Rig:** a **single Tripo self-rigged model**, static armor, **NO mesh-swap**. *Blink full-body rig
  is JUNKED (06-22)* — Blink survives only as a **UI re-skin kit** (`BlinkChrome` flag), not the hero body.
- **Combat:** animated real-time battle = the **OVERWORLD BattleArena** (lock-on WO-512, 9-zone HUD).
  **ATB is separate** (flat/static, single hero vs static enemies). Arena trio = OFF/gated.
- **Tech Tree:** BuildingUpgradeVM + PanelMvvm (Warcraft 3-style perks, tier gate at the Heart of Elarion);
  unlocked this arc by the wired village-tier upgrade (WO-432).
- **World:** home hub `MainCastle_Hall`; `OuterWorld` streams additively; `Village2` = raid target
  (`Village.unity` ABANDONED). Castle↔OuterWorld = four-side warp gates (RuntimeRegionGate); moat +
  4 drawbridges (`ff.castlemoat`); tree aura + tower glow (`ff.hubambientvfx`).
- **Economy:** Echo workforce wired (offline real-clock, WO-587 Population & Echo growth; save now **v29**); gold on kills; research costs.
- **In-flight:** HEAD targeting sweep (`ff.enemystructureaware`) is **UNVERIFIED** — do not push until proven.

## Key Files to Remember
- `CANON_GROUND_TRUTH_2026-07-13.md` (the single live anchor of current reality — read FIRST; the 07-12/07-08 anchors and earlier snapshots are SUPERSEDED)
- `CLI_PREP_2026-07-08_next-session.md` (wave-2 close prep + open F8-37..F8-42 tickets + WO-614 rulings)
- `docs/COMBAT_PIVOT_NORTHSTAR.md` (single-Knight pivot — supersedes all "Blink/party-of-4" canon)
- `docs/ARCHITECTURE_PRINCIPLES.md` · `docs/ARCHITECTURE.md` (hub)
- `docs/TICKET_PIPELINE.md` (QA→CLI→PO ticket lifecycle, BINDING)
- `docs/PATH_TO_V1.md` · `V1_ASSEMBLY_MAP.md` · `ECHO_WORKFORCE_SPEC.md`
- `docs/UI_MVVM_BINDING_MAP.md` · `docs/UI_BLINK_TEMPLATE_CANON.md` (BINDING — master-frame UI formula) · `docs/BLINK_UI.md` (UI re-skin only)
- `WORK_ORDER_432` / `WORK_ORDER_433`

> **Maintenance (WO-520):** after any commit that changes architecture/state, update the relevant canon
> doc in the same breath, and keep `CANON_GROUND_TRUTH_<date>.md` current. See CLAUDE.md §15.

---
*Maintained by the owner. Keep it current; it is the at-a-glance SME primer pasted at the
start of every session.*
