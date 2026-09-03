# WORK ORDER 1286 RESULT - Mobile-first Obsidian card navigation

**Status:** CLOSED 2026-09-03 - owner felt-test PASS. PRIOR STATUS: FIXED — RESULT record for WO-1286; the navigation stack landed in 486cd7b17 `feat: finalize mobile UI combat art and Windows handover`. Awaiting the owner's felt-verification (PO closes, CLAUDE.md §13). *(Board status audit 2026-09-02: this RESULT file carried no canonical `**Status:**` keyword, so board_build.py bucketed it Unlabeled. Body unchanged; the `**Result:** DONE` line below is the author's own record and is left as written.)*

**Result:** DONE - 2026-08-31

## Delivered

- Added the pure, tested `NavigationStack<T>` and shared `ObsidianNavigationWorkspace<T>` foundation.
- Established one universal contract: Back returns one workspace level, Close returns to play, and
  Done commits before closing and returning to the declared parent.
- Migrated Build Collections to category cards and shared refresh/done behavior without moving
  placement, catalog, capacity or progression authority.
- Migrated Manage to a stable four-card launcher with readable locked requirements and Back to the
  launcher from operational content.
- Added Realm, Hero and Journey workspaces and routed the five stable HUD peers to them.
- Consolidated Store under Realm and Raids under Journey; removed their competing direct HUD routes.
- Preserved existing destination authorities, including `PanelRouter`, `RaidEntryGate`, build
  placement callbacks, economy, billing and persistence seams.
- Added focused multi-device capture coverage and updated regression oracles for the new hierarchy.

## Acceptance result

Every WO-1286 gate passed:

- Five stable primary destinations; word labels and visual identity on every peer.
- Common content reachable in no more than two navigation selections.
- Predictable Back/Close/Done semantics across the migrated experience.
- 112 px authored touch floor, safe geometry and explicit non-color lock states across 15 scoped
  captures at 1920x1080, 2340x1080 and 2670x1200.
- Final EditMode: **1,033/1,033 passed**.
- Data regression: **332/332 passed**.
- Focused capture: **15/15 fidelity, geometry and touch passed**.
- Full release capture after SME tightening: **100/100 canvases geometry-clean and 100/100 panels touch-clean**.
- Phone-scale PNG review completed.
- SME verdict: **PASS - strongest shippable solution within the approved constraints**.

See `docs/qa/WO_1286_MOBILE_NAVIGATION_SME_REVIEW_2026-08-31.md` for the evidence-based review.
