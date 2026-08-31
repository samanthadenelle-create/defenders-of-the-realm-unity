# WORK ORDER 1285 - Defense upgrade needs its own card / second entrance

**Status:** IMPLEMENTED — AWAITING FELT VERIFY 2026-08-30. The fresh-save Defense empty state now explains the gate and opens the defensive build list; the owned-structure route remains the upgrade entrance. Compile and full regression gates are green. Phone-width fresh-save and owned-save captures/taps remain required before DONE.
**Minted:** 2026-08-30 (CLI seat, main line; banner bumped 1285 -> 1286 in the same edit)
**Provenance:** owner, 2026-08-30: *"in defense can we add a card for upgrades, that allows
another enterance to it?"*

## Why
The owner could not find the defensive upgrade screen and reported it as missing. It was not
missing - it is CORRECTLY GATED (proven: `ResetToNewGame` at 09:45 left `BaseLayout` empty, and
`ManageScreenVM.BuildVisibleTabs` derives every tab from PLACED structures; baked scene walls are
not `BaseLayout` records). So the defect is DISCOVERABILITY, not routing - the owner of the game
could not find her own upgrade path.

## Ask
Add a CARD in the Defense area that acts as a SECOND ENTRANCE to structure upgrades, so the route
does not depend solely on the tab appearing once something is placed.

## Implemented interaction decision
The card appears on a fresh save as an honest teaching/empty state and opens the defensive
structures list. Once an owned defensive structure exists, the existing placed-structure route
continues to open the specific upgrade detail. This adds discoverability without defeating the
progressive-disclosure gate.

## Constraints
- Respect the existing progressive-disclosure contract; `ManageProgressiveDisclosureRegression`
  pins it deliberately. Do NOT defeat the gate - ADD a route.
- Do not resurrect a retired action-bar face and never renumber `ActionBarButtonId` ordinals (s7).
- Add a DOOR-level regression, like `ManageDefenseUpgradeDoorRegression` added today.

## Close criteria
The owner can reach a structure upgrade from Defense WITHOUT being told where to look, on a fresh
save AND on a save with structures placed; `COMPILE_GATE_OK` + `REGRESSION_OK` on fresh logs; and
a phone-width capture, since Manage is a phone-first surface.
