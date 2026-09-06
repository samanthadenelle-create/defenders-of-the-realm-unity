# Manage Redesign — Recommended Implementation Sequence

## Wave 0 — Inventory and model seams

1. WO-2011 — Unified action-state model
2. WO-2005 — Reconcile BUILD inventory
3. WO-2003 — Heart progression spine
4. WO-2004 — Heart unlock data

Do this first. The UI should not be redesigned against guessed states or guessed structure counts.

## Wave 1 — Common shell

5. WO-2001 — New Manage information architecture
6. WO-2002 — Common dumb Manage UI contract
7. WO-2012 — Global Queue

The Queue is P0 because the shared contextual strip depends on it.

## Wave 2 — Primary tabs

8. WO-2006 — BUILD grid
9. WO-2007 — Building detail
10. WO-2008 — ARMY 3×3
11. WO-2009 — Troop detail
12. WO-2010 — Research schools

## Wave 3 — Progression navigation

13. WO-2017 — Heart Manage surface
14. WO-2013 — Direct prerequisite navigation

Direct navigation is P0, not polish. A lock without a route is a dead end.

## Wave 4 — Copy and visual cohesion

15. WO-2014 — Copy reduction
16. WO-2015 — Art cohesion

## Wave 5 — Regression closure

17. WO-2016 — Capture/regression rewrite and scroll auditor fix

Do not ship while the automated auditor still treats valid scrolled content as failure.

---

# Definition of Done for the program

The redesign is complete when:

- Manage opens directly to BUILD/ARMY/RESEARCH
- old four-tile launcher is no longer required
- no destination uses the old 2.2-row rail
- BUILD uses complete filters including CIVIC
- BUILD inventory count comes from live definitions, not planning prose
- ARMY shows all 9 troops
- RESEARCH is school-first
- Heart is a real upgradeable progression system
- Heart Level is the player-facing tier
- queue-blocked is first-class
- max upgrade track does not block unrelated actions
- every tile exposes state
- prerequisite CTAs route directly
- global Queue is shared
- panels do not implement game rules
- service mutation happens only through model/VM commands
- scrolled captures pass a corrected auditor


---

## Provenance and reconciliation (added by the CLI seat, 2026-09-06)

Authored outside this repo and delivered by the owner as `Elarion_Manage_Redesign_Detailed_WorkOrders.zip`
on 2026-09-06. Filed verbatim; **the body above is the author's and has not been edited.** Everything the CLI
seat adds appears under a heading like this one.

**Numbering:** this set uses a **2000-block**, a THIRD namespace alongside the CLI main line and the UI seat's
reserved block. It is declared on `CLI_LANES_WO_NUMBERS.md` so it cannot collide (CLAUDE.md section 2 - the banner is the
sole authority). Do not renumber these into the main line.

**Supersedes:** `WORK_ORDER_1427` (why-can't-I) and `WORK_ORDER_1428` (the Manage card grows to the mockup).
Both were minted earlier the same day from the owner's playtest and her mockup; this program subsumes them and
goes further by replacing the rail model rather than enriching the card.

**Measured facts this set is consistent with** (from `docs/manage-flow-map/MAP.md`, run `Builds/flowmap1`):
43 rail rows across four areas, about two visible at a time; Buildings 6 + Defense 11 = 17, which is the number
the canon cites; the scroll auditor reporting `geometry=5 touch=5` on deliberately scrolled frames, which WO-2016
is right to call a fix rather than a waiver.
