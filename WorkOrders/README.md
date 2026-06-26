# WorkOrders/ — index & staleness banner (2026-06-26)

This folder holds **all WO spec + `.RESULT.md` files** (the unit of work). It is a mostly-frozen
archive: each WO is a **point-in-time** spec, accurate as-of its date. **Do not treat the folder as a
live actionable queue** — the live board is the **Notion "Work Orders" DB** (per `NOTION_SOURCE_OF_TRUTH.md`),
and current reality is `CANON_GROUND_TRUTH_2026-06-26.md`.

## ⚠ Read these as HISTORY, not live work (verified by the WO-520 audit)

- **WO-44…111 (and 179/195/197/198):** the pre-pivot block — they target the now-**ABANDONED `Village.unity`**
  castle/walls/moat, the **`feat/tower-core-loop`** era, **Yarn** dialogue, **Solana** monetization, and the
  4-class **party**. Frozen history. Home is now `MainCastle_Hall`; raid target = `Village2`.
- **Defend-the-Tower / PatriciaLight WOs (WO-46/47/48/96/99/221/317/318/319/320/330/331/332 + 333 death-modal):**
  **DTT was REMOVED 2026-06-09.** Do NOT implement these — the mode does not exist.
- **ATB party / pet-combat WOs (WO-68/69/70/93/94/234 etc.):** ATB is now **flat/static single-hero**; the
  animated combat lives in the **overworld BattleArena**. Party-of-4 descoped (single-Knight pivot, 06-22).
- A few **UNDATED** WOs assert current state and were flagged STALE (branch/"#1 priority"/"go-live"):
  `WORK_ORDER_208`, `_277`, `_278`, `_280` (×2), `_282`, `_466`, `_197`. Their branch/priority lines are stale;
  the underlying feature intent may still be valid — check against the ground truth before actioning.

## Current-direction specs (queued, NOT historical)
`WORK_ORDER_446…514` are the live single-Knight / BattleArena / store / VFX / world-seam direction (many
queued, some shipped). `WO-509` (moat seams), `WO-513` (coordinated orcs), `WO-514` (tower cap + saved echoes
+ siege-AI), `WO-430` (offline-garrison) are the captured-but-unbuilt next items. `WO-520` = this canon
reconciliation (`CANON_READINESS_LEDGER_2026-06-26.md`).

> WO-numbering authority = `MASTER_PIPELINES_BACKLOG_2026-06-06.md` + `CLI_LANES_WO_NUMBERS.md`, **not** the
> filesystem max. Number/filename collisions across this folder are expected, not defects.
