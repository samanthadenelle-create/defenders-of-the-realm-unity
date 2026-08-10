# WORK ORDER 944 — RESULT

**Date:** 2026-08-09 (flagged 22:24, implemented + capture-verified 22:32 — same hour)  **Seat:** CLI
**Status:** IMPLEMENTED; owner felt-verify pending (she asked mid-play, she judges it).

- `BuildHudController.cs`: the name+cost pill pins STATIC top-centre (fixed px, clear of the
  corner Done); the whole follow/clamp pass in `LayoutGhostControlsNow` retired with it — the
  LAST follower on the build screen is gone (UI_PLAYBOOK §8's preferred answer). `TrackGhost`
  still feeds validity + the worded blocked reason; the verdict logic is unchanged.
- Gates: `COMPILE_GATE_OK` + `REGRESSION_OK 133/133` + `UI_CAPTURE_OK 62`/`FIDELITY_OK 44`;
  the PLACE PNG shows the pinned pill (opened, not counted). Geometry failures unchanged at the
  16 pre-existing WO-941 rows.
- Note for WO-942: `BuildGhostChips_edgeclamp` lost its subject entirely now (nothing follows,
  nothing clamps) — that case should assert the STATIC pill position instead.
