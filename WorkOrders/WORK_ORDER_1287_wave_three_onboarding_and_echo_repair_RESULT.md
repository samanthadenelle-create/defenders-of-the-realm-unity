# WO-1287 result - Wave-three onboarding and Echo repair visibility

**Status:** CLOSED 2026-09-03 - owner felt-test PASS. PRIOR STATUS: FIXED — RESULT record for WO-1287. Gates cited below: `COMPILE_GATE_OK` + `REGRESSION_OK 332/332 suites`. Awaiting the owner's felt-verification (PO closes, CLAUDE.md §13). *(Board status audit 2026-09-02: no canonical `**Status:**` keyword; body unchanged.)*

Implemented 2026-08-31.

- Plans now prefer a camera-visible seat 3.25 m ahead of the hero.
- Echo dialogue explicitly routes Build -> Defenses -> Arcane Spire.
- First three wave floors total 740 Wood, 480 Iron, and 360 Stone.
- The plans screen offers one persisted complimentary repair of current damage.
- Passive repair shows a target-bound progress bar and the founding Echo's name.
- No save-schema bump: both one-time states use the existing SeenTutorials store.

Validation evidence:

- `COMPILE_GATE_OK` (`Builds/compilegate-wo1287.log`).
- `REGRESSION_OK 332/332 suites` (`Builds/data-regression-wo1287-final.log`),
  including the new opening-repair-runway assertion.
