<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

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
