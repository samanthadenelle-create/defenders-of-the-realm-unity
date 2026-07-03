# DeNelle Studios — Project Canon Loader

> ## ▶ LIVE THREAD (2026-07-03) — READ BEFORE WORKING
> Current focus = **THE FEEL ARC** (owner: "the most important thing is how it FEELS"; quality bar =
> the ten-year-old test). The 07-02→03 convergence session (~25 agents) landed: world feel (post-processing
> was structurally DEAD until now — fixed; dusk sky, terrain relief), castle-raise fallout (all fixed),
> character/anim/combat feel, NPC cards, vendors data-mapped, WC3 perk grid, end-state template, Tutorial V2
> (built, ff.tutorialv2 OFF), new fleet oracles. **South vertical slice fleet-proven 6/6 (masked warp fires
> mid-bridge)** — owner felt-pass pending; N/W/E waits for south "feels perfect". Read the live anchor
> **`CANON_GROUND_TRUTH_2026-07-03.md`** + **`RESUME_2026-07-03_morning.md`** + `SESSION_BOOT_DIRECTIVE.md`.
> **Next headline = WO-545 full Addressables** ("what CAN stream SHOULD stream") — blueprint in
> `docs/WEBGL_DELIVERY_PLAN_2026-07-03.md`. Vercel preview of the convergence build is LIVE
> (`defenders-of-the-realm-v2-69mafg5pj.vercel.app`); prod stays on the 07-01 Pi-sign-in build until promoted.
> Seam un-stack (WO-453) still PARKED in `stash@{0}` (encounter-return strands 7.1km — publisher critique #1;
> owner decision pending). **BINDING owner rule 07-03: read-before-assert applies to EVERYTHING (code + non-code).**

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

## Current State (updated 2026-07-03 — anchored to `CANON_GROUND_TRUTH_2026-07-03.md`)
- **Branch:** `wip/village2-and-f8-tickets` (NOT `feat/tower-core-loop` — stale). The 07-02→03 convergence session is in the tree, uncommitted — commit lanes staged by path, **push held for owner felt-pass**. Save schema **v28** (WO-587).
- **THE FEEL ARC is the live focus** (2026-07-03 — supersedes web/Pi stabilization): ten-year-old test = quality bar; south vertical slice fleet-proven 6/6; Tutorial V2 built behind `ff.tutorialv2` (OFF). Pi sign-in RESOLVED; **game IS live on Vercel** (preview = convergence build at 79.7MB Brotli — the "blocked by 100MB" claim is stale; WO-545 Addressables is still the next headline). Seam un-stack PARKED in `stash@{0}` (publisher critique ranks closing it #1).
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
- **Economy:** Echo workforce wired (offline real-clock, **save v28** — WO-587 Population & Echo growth); gold on kills; research costs.
- **In-flight:** HEAD targeting sweep (`ff.enemystructureaware`) is **UNVERIFIED** — do not push until proven.

## Key Files to Remember
- `CANON_GROUND_TRUTH_2026-07-03.md` (the single live anchor of current reality — read FIRST; supersedes the 07-01 / 06-28 / 06-26 snapshots)
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
