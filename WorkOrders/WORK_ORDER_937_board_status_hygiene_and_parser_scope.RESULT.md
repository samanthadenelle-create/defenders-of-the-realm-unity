# WORK ORDER 937 — RESULT

**Date:** 2026-08-16  **Seat:** edit-only tooling agent (python/tools lane; no Unity, no .cs, no commit — CLI reconciles + commits)
**Status:** DONE (tool-verified headless; no game code touched)

## What was already in-tree vs what this session added

- **A. Parser scope — FOUND ALREADY IMPLEMENTED.** `tools/board_build.py` already scoped by
  document KIND (`is_work_order()` on the `WORK_ORDER_` filename prefix, never on "has a number"),
  bucketing the 18 companion docs as the non-defect `Doc` bucket (kept discoverable, not dropped),
  and `docs/BOARD.md` §3a already documents it. Nothing to do.
- **B. Status lines — FOUND ALREADY DONE.** Baseline run this session:
  `BOARD_CHECK_OK 0 unlabeled` (958 rows = 940 work orders + 18 docs). The 71-file hygiene wave
  had landed before this session. Nothing to do.
- **C. Gate wiring — IMPLEMENTED THIS SESSION.** `tools/regression/checkin_gate.ps1` now runs
  `python tools/board_build.py --check` as stage **1b** (right after the static gate, ~1 s, no
  Unity). A board-check FAIL fails the gate summary but does not short-circuit the code stages
  (docs defect, not a compile one). Parse-verified under PowerShell 5.1
  (`Parser::ParseFile` → `PARSE_OK` — the WO-329 unparseable-gate failure mode is guarded).
- **Duplicate numbers — IMPLEMENTED THIS SESSION.** `board_build.py` now prints a report-only
  `DUPLICATE_WO_NUMBERS` block: **56 numbers claimed by more than one file** (136 has 3 claimants,
  430 has SIX, 482 has 2 — full list in the tool output). Flagged, never silently renumbered;
  does not change the `--check` exit contract.
- `docs/BOARD.md` §5 updated in the same session (gate wiring + duplicate reporting documented).

## Proof (this session's run)

```
BOARD.html written: 958 rows = 940 work orders + 18 docs (Ready:468, Blocked:15, Spec:55, Unlabeled:0, Done:287, Closed:115, Doc:18)
DUPLICATE_WO_NUMBERS 56 number(s) claimed by more than one file (flagged, not renumbered ...)
BOARD_CHECK_OK 0 unlabeled
```

## Acceptance criteria

- [x] Non-WO files not counted as Unlabeled, still discoverable (pre-existing; verified)
- [x] All real WOs carry a canonical keyword — Unlabeled = 0 (pre-existing; verified)
- [x] Duplicate WO numbers reported, not renumbered (56 found, incl. 136 ×3 and 482 ×2 from the spec)
- [x] `--check` exits 0 with `BOARD_CHECK_OK 0 unlabeled`
- [x] Wired into the check-in gate (stage 1b of checkin_gate.ps1)
- [x] No game files touched — only `tools/board_build.py`, `tools/regression/checkin_gate.ps1`,
      `docs/BOARD.md`, this WO's status line, and the derived `BOARD.html`
