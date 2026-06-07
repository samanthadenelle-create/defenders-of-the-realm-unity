# WORK ORDER 186 — No Wave Timer (add visible countdown)

**Status:** READY TO IMPLEMENT
**Lane:** UI / HUD — code, parallel-safe
**Source:** playtest 2026-05-31
**Priority:** P1 (player can't read wave pacing)

## Problem
There's a START WAVE button + an hourglass icon, but **no visible countdown timer** for waves.
The player can't tell when the next wave hits or how long until it spawns.

## Acceptance
- A visible countdown (number and/or bar) shows time until the next wave during the build/prep phase.
- Timer reads correctly (relates to prior WO-91 countdown-scale fix — confirm not regressed).
- Manual START WAVE still works (skip the timer, start now).
- Code-built UI (no UXML in builds).

## Open question for owner
- Are waves **timed/auto** (countdown then auto-start) or **manual** (player presses START WAVE)?
  *Default: timed with a manual early-start option* (fits the mobile-passive direction).

## Gate
Brace check; green build; commit `feat: implement WO-186 — wave countdown timer`. No bake.
