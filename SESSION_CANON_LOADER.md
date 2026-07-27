# DeNelle Studios — Project Canon Loader

> ## ▶ LIVE THREAD (2026-07-26) — READ BEFORE WORKING
> **Current reality anchor = `CANON_GROUND_TRUTH_2026-07-26.md`** (supersedes 07-22). A large **dungeon+raid
> felt-test wave** landed on `wip/village2-and-f8-tickets` and **is PUSHED** (HEAD `7dec0e07`, local==origin —
> a change from 07-22's push-HELD). **Dungeons are now a functional end-to-end loop** (enter → explore → read
> lore → fight with a REAL win/loss → settle → leave → Village): WO-770.1/.2/.3/.3b/.4/.7/.9 shipped, plus
> DungeonHero sole-mover / taller camera / Bryn pill-hide. **The raid loop is LOCKED to Teleport/Deploy** (COC
> model, owner 2026-07-26); walk-to retired as the raid loop. **WO-770 (dungeon), 771 (raid v2), 772 (shared
> enemy system), 773 (Obsidian job queue)** are firmed + validation-signed-off (`docs/qa/`), but only 770 is
> partly built — 770.5/.6/.8/.10/.11 + all of 771 + 773 are BACKLOG; **772 Phase 1 UNBLOCKED** (Hollow Ones
> APPROVED / Wildlands DEFERRED — `docs/PAIN_POINTS_2026-07-26.md`). Non-dungeon felt fixes shipped: enemies-out-of-castle + battle-lock, towers-no-longer-through-
> walls, MagentaGuard Android, loading overlay+bar, gate-traversal-teleport off, collector vendor NPCs, Alchemy
> scroll-fix. **WO next-free = 774** (761–773 consumed). Ticket table: `docs/qa/SUNDAY_STATUS_2026-07-26.md`.
> Save still **v34** (no new persisted fields this wave). **The 07-22 thread below is SUPERSEDED** (its §5/§6/§7
> module digests remain the deep reference).
>
> ## ▶ LIVE THREAD (2026-07-22) — SUPERSEDED (see 07-26 above; deep module state still valid)
> **Reality anchor = `CANON_GROUND_TRUTH_2026-07-22.md`** (supersedes 07-19). A **17-agent read-only
> SME fan-out** (12 module + 5 high-level, verified from code) produced that anchor: the code is HEALTHY and
> gates are GREEN (`COMPILE_GATE_OK` + `REGRESSION_OK`, 16 P1 suites, 0 reds, save v34, HEAD `148ab637`,
> local==origin) — **the debt is DOCUMENTATION DRIFT.** The `MASTER_CATALOG/<area>` sections (dated 2026-06-12
> on the stale `feat/tower-core-loop` label) have drifted weeks behind: see the 07-22 anchor's **§6
> catalog-drift ledger** + **§7 comment-vs-code lies registry** (e.g. `ff.atbdungeon` doesn't exist — real
> gate is `ff.dungeonrealtime`; home hub is `Main_Castle_Overworld` not `MainCastle_Hall`; save v34 not v33;
> 23 build scenes not 13; audio 5-group mixer never built). **Branch hygiene done 07-22:** 2 stale agent
> worktrees + 4 stale branches (2 local, 2 remote: `feat/tower-core-loop`, `samantha-village-progress-2025`)
> purged; remotes now `master` + `wip` only. WO next-free = **754**. Push still HELD. **The 07-19 threads
> below are SUPERSEDED.**
>
> ## ▶ LIVE THREAD (2026-07-19 EVENING) — READ BEFORE WORKING
> **Current reality anchor = `CANON_GROUND_TRUTH_2026-07-19.md` (still current).** On top of the 07-19
> morning arc below: a **FELT-TEST FIX WAVE** (CLI committing) — pet-screen sort-order, HUD de-overlap,
> **WO-751 Y-height normalization** (default 4m / tower 7m / siege 3m + audit tool), Echo modal single-
> arbiter via `PanelManager`, upgrade-panel visuals (event-driven rebuild, text-fit, hotkeys removed),
> flag-screenshot save-on-release; **in-flight:** upgrade no-op blocker, white-ballista/magenta-weapon
> materials, **WO-753 Destructible** (no-rebuild + full-cost + VFX cleanup). **New WOs:** 750 (Right
> ActionBar naming + Warden's Grace redesign, SPEC), 751 (Y-normalization, DONE), 752 (Echo founding-card
> overhaul + post-tutorial interjection, SPEC + creative sign-off), 753 (Destructible, IN PROGRESS).
> **New rulings:** Right ActionBar = Attack + Q/W/E/R named skills (Sword Wielding/Sword Heroic/Shield
> Charge/Warden's Grace/Radiant Strike), mobile HUD shows NO key-letters; all items normalized by
> Y-height; Echo = essence of a person the tree guards (Aldwin/Elowen/Corvin/Bran/Doran/Maren); destroyed
> items never rebuild (full-cost + VFX cleanup); headless UI-screenshot pass runs before builds.
> **WO next-free = 754** (750-753 consumed). **The morning 07-19 line below is still valid history.**
>
> ## ▶ LIVE THREAD (2026-07-19) — READ BEFORE WORKING
> **Current reality anchor = `CANON_GROUND_TRUTH_2026-07-19.md` (read it first).** Since 07-18: HEAD
> `98ff1135`, **local ahead of origin by 7, PUSH HELD**. **DataRegression is `REGRESSION_OK` — ZERO reds**
> (all 5 long-standing FAIL-BY-DESIGN reds fixed 07-19 per the owner's plan: arena texture, dual-wallet,
> pet-slot persist, Tribes/Wards/Arena persist, orc-raider SSOT). **Save v34** (persist Tribes/Wards/Arena +
> pet active-slot). **WO-748 (Default Town) + WO-749 (dungeon ingredients) DONE + RESULT-filed.** Corrupt
> `d4_sunken_crypt` scene PURGED + stale branch junked. New: `SUNDAY_HOUSEKEEPING.md` weekly ritual +
> known-dictionaries; Notion setup kit staged (`docs/notion/`, awaiting owner `/mcp`). WO next-free = **750**.
> **The 07-18 thread below is SUPERSEDED.**
>
> ## ▶ LIVE THREAD (2026-07-18) — READ BEFORE WORKING
> **Current reality anchor = `CANON_GROUND_TRUTH_2026-07-18.md` (read it first).** Since 07-13:
> **Pi Hackathon WON** (the "July-31 deadline / build mode IS the demo" framing is RETIRED); the
> **whole-game MVVM migration is DONE** (WO-744 — every panel View binds an `IPanelViewModel`, the
> `[ui-mvvm]` conformance oracle is armed HARD-FAIL); **Room Forge merged to mainline** (WO-740–745,
> green); **save v33**; `wip` pushed to origin. Two-session shared-tree hazard is live (dungeon
> session should use its own worktree). WO banner next-free = **750**. **The 07-12 thread below is
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

## Current State (updated 2026-07-18 — anchored to `CANON_GROUND_TRUTH_2026-07-18.md`)
- **Strategic placement = ALWAYS ON (2026-07-13, WO-695 ex-682):** `ff.strategicplacement` is REMOVED —
  Build → Town/Defenses/Walls tabs, movable functional storefronts and the 260w/210i core-kit seed are
  the unconditional path; New Game = the BLANK template (+ one FTUE grace-default Forge record);
  existing saves migrate once via the v30 one-shot writer.
- **Branch:** `wip/village2-and-f8-tickets` (NOT `feat/tower-core-loop` — stale). **HEAD = the 07-18 arc: WO-744 strict-MVVM whole-game migration + WO-740–745 Room Forge into mainline + WO-746 Build-Mode felt-fixes, pushed to origin (local == origin after each push).** Save schema **v34** (persist Tribes/Wards/Arena + pet active-slot; every 21→34 bump has a SaveMigrator step). Fresh headless gates = `COMPILE_GATE_OK` + DataRegression **`REGRESSION_OK` — 0 reds** (all 5 long-standing baseline reds fixed 2026-07-19: arena texture, dual-wallet, pet-slot persist, Tribes/Wards/Arena persist, orc-raider SSOT) + the `[ui-mvvm]`/`[room-forge]` ratchets at 0 NEW.
- **Pi Hackathon WON (2026-07-17)** — the "July-31 deadline / build mode IS the demo" framing is **RETIRED**; there is NO upcoming demo and the roadmap is OPEN. The quality bar (feel-arc/F8, ten-year-old test) still governs. **Prod untouched** (promotion stays the owner's separate call at `defenders-of-the-realm-v2.vercel.app`). **Highest-leverage open lane = the CoC offense loop (WO-724→726, Path A convergence)** now the MVVM + Room Forge foundations have landed; WO-739 generic upgrade panel is the parallel-safe start.
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
- `CANON_GROUND_TRUTH_2026-07-18.md` (the single live anchor of current reality — read FIRST; the 07-13/07-12/07-08 anchors and earlier snapshots are SUPERSEDED)
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
