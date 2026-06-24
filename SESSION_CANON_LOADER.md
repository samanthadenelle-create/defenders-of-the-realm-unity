# DeNelle Studios — Project Canon Loader

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

## Current State
- **Game:** Defenders of the Realm (Unity 6 / URP, Solana integration, tower-defense + dungeon-crawler).
- **Hero Rig:** Blink full-body models + EquipmentController weapon seating (head/armor sync being tuned).
- **Tech Tree:** BuildingUpgradeVM + PanelMvvm (Warcraft 3-style perks, tier gate at the Heart of Elarion).
- **Raids:** Village2 + Outpost systems (GarrisonController, RaidGarrisonSpawner).
- **UI:** ElarionUiKit + BlinkChrome flag for re-skin.
- **Economy:** Gold on kills (small but meaningful), research costs.

## Key Files to Remember
- `docs/UI_MVVM_BINDING_MAP.md`
- `docs/BLINK_UI.md`
- `docs/ARCHITECTURE_PRINCIPLES.md`
- `docs/TICKET_PIPELINE.md` (QA→CLI→PO ticket lifecycle, BINDING)
- `WORK_ORDER_432` / `WORK_ORDER_433`

---
*Maintained by the owner. Keep it current; it is the at-a-glance SME primer pasted at the
start of every session.*
