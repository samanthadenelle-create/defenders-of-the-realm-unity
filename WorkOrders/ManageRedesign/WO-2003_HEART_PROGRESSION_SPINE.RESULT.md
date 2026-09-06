# WO-2003 RESULT — Heart as the Realm Progression Spine

**Status:** FIXED (commit a6bbc523d; COMPILE_GATE_OK [Builds/c26, 2026-09-06 11:18], REGRESSION_OK 400/400 suites [Builds/r24, 11:19], CATALOG_FALLBACK_GEN_OK [Builds/catgen2]) *(was: READY)*

**Date:** 2026-09-06

**Files shipped:**
- `Assets/_Modules/Core/Manage/HeartProgression.cs` — **NEW** — authoritative Heart level, upgrade state, costs, duration, prerequisites, affordability, post-upgrade unlock result set.
- `Assets/_Modules/Core/Manage/HeartPanel.cs` — **NEW** — read-only view; does not calculate unlocks, reach, structure levels, queue capacity, resources, or mutate Heart state.
- `Assets/_Modules/Core/Manage/HeartPanelBootstrap.cs` — **NEW** — wiring and lifecycle.
- `Assets/Resources/Portraits/Buildings/heart.png` — **NEW** — art asset.

**Content integration:**
- Heart gained direct route from Manage header (present in all Manage states).
- Four CTAs that said "UPGRADE THE HEART" now consistently open the Heart panel.
- Five spellings (Heart Tier, Village Tier, Realm Level, …) unified to **HEART LEVEL** in player-facing copy.
- What each level unlocks is data-driven via the catalog; never typed into the UI.

**New regression suite:**
- `HeartSurfaceRegression` — validates Heart state transitions, CTA conditions, and unlock previews.

**Markers on fresh logs:**
- `COMPILE_GATE_OK` (c26, 11:18)
- `REGRESSION_OK 400/400 suites` (r24, 11:19)

## Known gaps (recorded, not hidden)

The following are KNOWN LIMITATIONS that do not block this first surface:

1. **Heart upgrade is INSTANT** — no queue timer state exists. Heart upgrades take zero time and resolve immediately. Builder queue is not involved.
2. **No gate authoring for Heart progression** — nothing in the live catalog yet authors prerequisites on Heart level for troops, defenses, research schools, or buildable reach. The catalog has the shape to support it; no content yet plugs in.
3. **No in-world Heart interactable** — the Manage panel is the only entry point. An in-world "touch the tree" interaction may follow in a later WO.

These are not bugs. They are deliberate first-pass scope and are noted so future gate-authoring WOs / content work orders will not rediscover them.

## Cross-reference

- **WO-2017 (Heart Manage Surface):** builds the entry points and view contracts on top of this progression spine.
- **WO-2003 + WO-2017 landed together** — both FIXED in commit a6bbc523d.

---

*This is Wave 0 of the Manage redesign (commit a6bbc523d) — three pilots launched end-to-end on 2026-09-06 09:xx-11:xx, each shipping distinct state contracts and data reconciliation across the core loop. See WO-2011, WO-2005 for the parallel action-state model and inventory filters.*
