# WO-2017 RESULT — Add Heart Management Surface

**Status:** FIXED (commit a6bbc523d; COMPILE_GATE_OK [Builds/c26, 2026-09-06 11:18], REGRESSION_OK 400/400 suites [Builds/r24, 11:19], CATALOG_FALLBACK_GEN_OK [Builds/catgen2]) *(was: READY)*

**Date:** 2026-09-06

**Depends on:** WO-2003 (Heart Progression Spine), WO-2004, WO-2013

**Entry points implemented:**
- Direct prerequisite CTA: **VIEW HEART** — prerequisite-blocked CTAs across Manage now route here.
- BUILD/CIVIC Heart tile — Heart appears as a structure in the Buildings/Civic filter (see WO-2005).
- ⛔ **CORRECTED by the CLI seat 2026-09-06: there is NO in-world Heart interaction, and no scaffolding for one.** `Assets/_Modules/Village/Heart/` contains GameOverScreen, HeartAuraController, HeartController, HeartHudBridge, HeartHudBridgeBootstrap and HeartRegen - and NONE declares an Interactable, an Interact member or a proximity prompt (grepped at source). The implementing lane said so plainly in its hand-back; this line overstated it. The Heart is reachable from Manage ONLY. Wiring an in-world door needs scene/prefab work no edit-only lane can do.

All entry points bind to the single Heart model (`HeartProgression.cs`).

**Display contracts:**
- Current level and label
- Realm-progression description
- Current reach (if applicable)
- Next reach (if applicable)
- Next-level unlock preview (derived from catalog, never calculated by UI)
- Costs
- Duration
- Prerequisite state (ordered list + primary recommended next action if prerequisites unmet)
- Primary action (UPGRADE HEART / VIEW PREREQUISITES / VIEW QUEUE) or blocker state

**Panel responsibilities — enforced:**
- UI does NOT calculate unlock previews.
- UI does NOT calculate reach.
- UI does NOT inspect structure levels, queue capacity, or resources.
- UI does NOT mutate Heart level.
- All state comes from `HeartProgression` model.

**Markers on fresh logs:**
- `COMPILE_GATE_OK` (c26, 11:18)
- `REGRESSION_OK 400/400 suites` (r24, 11:19)

## Known gaps (recorded, not hidden)

Same as WO-2003, since they shipped together as the Heart progression system:

1. **Heart upgrade is INSTANT** — no queue timer state exists.
2. **No gate authoring yet** — catalog structure supports prerequisites on troops / defenses / research / reach; content not yet plugged in.
3. **In-world Heart interaction — DOES NOT EXIST.** Not scaffolded, not started. See the correction above. The Heart has exactly one door today: the HEART face in Manage's header.

## Cross-reference

- **WO-2003 (Heart Progression Spine):** defines the `HeartProgression` model that this surface consumes.
- **WO-2003 + WO-2017 landed together** — both FIXED in commit a6bbc523d.
- Every Heart-gated Manage item now has a valid destination (acceptance criterion met).

---

*This is Wave 0 of the Manage redesign (commit a6bbc523d) — three pilots launched end-to-end on 2026-09-06 09:xx-11:xx, each shipping distinct state contracts and data reconciliation across the core loop. See WO-2011, WO-2005 for the parallel action-state model and inventory filters.*
