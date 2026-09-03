# WORK ORDER 1287 - Wave-three onboarding and Echo repair visibility

**Status:** CLOSED 2026-09-03 - owner felt-test PASS. PRIOR STATUS: FIXED — implemented 2026-08-31; evidence in `WORK_ORDER_1287_..._RESULT.md`: `COMPILE_GATE_OK` (`Builds/compilegate-wo1287.log`) + `REGRESSION_OK 332/332 suites` (`Builds/data-regression-wo1287-final.log`). Awaiting the owner's felt-verification (PO closes, CLAUDE.md §13). *(Board status audit 2026-09-02: the line carried no canonical marker and read as Unlabeled.)* *(Prior line:)* Status: DONE
Owner feedback: 2026-08-31

## Problem

New players find the defense opening too difficult. The wave-three Castle Defense
Plans can appear away from the player, their narrative dialogue does not explain the
next UI action, and the legacy staggered payout can grant no iron before wave four
despite repair obligations in the hundreds. Echo repair work is also invisible.

## Decisions

1. Spawn the once-ever plans pickup 3.25 m ahead of the live hero; retain the
   deterministic inside-gate seat only as a loading/headless fallback.
2. On pickup, say plainly: open Build, choose Defenses, place Arcane Spire.
3. Guarantee first-clear baskets of Wood/Iron/Stone: 180/120/80, 240/160/120,
   320/200/160. The first three clears therefore provide 480 iron. Use these as
   floors so stronger authored or talent payouts are never reduced; wave 4+ is unchanged.
4. Offer a once-ever complimentary repair from the plans dialogue. It consumes no
   materials, persists its entitlement before applying, and reuses the canonical
   repair target/fix set rather than inventing a second damage model.
5. During ordinary passive Echo repair, show a shared-kit world-space progress bar
   over the real worst-damaged target and name the contributing founding Echo.

## Acceptance

- Wave-three plans appear near a live hero and never auto-collect at spawn distance.
- Pickup copy identifies the scroll, unlock, exact Build navigation, and goal.
- Waves 1-3 each grant all three common resources; cumulative iron is at least 400.
- Complimentary repair can be claimed once per save and causes zero wallet spend.
- Ordinary Echo repair displays actual work-budget progress over the actual target.
- Unity compile gate and all 332 registered data-regression suites are green.
