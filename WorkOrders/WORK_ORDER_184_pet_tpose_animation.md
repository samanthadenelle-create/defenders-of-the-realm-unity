# WORK ORDER 184 — Pet Walks in T-Pose (animator not applied)

**Status:** DONE

> **DONE - verified in HEAD 2026-08-14 (phantom sweep).** The work is present at PetClipPlayer.cs:40-79.
> Status had read READY because the landing commit did not flip this line in the same commit
> (CLAUDE.md §2), so the DERIVED board (BOARD.html) kept re-serving finished work.
> _Prior status line, preserved: Status: READY TO IMPLEMENT_

**Lane:** B/E (Combat/anim — code, parallel-safe; separate from VillageSceneBuilder)
**Source:** playtest 2026-05-31
**Priority:** P1 (companion visibly broken)

## Problem
The pet moves around the village in **T-pose** — no animator controller bound, or the wrong/empty
controller, or locomotion params never driven. (Regression-adjacent to WO-166 pet/anim work.)

## Likely cause (CLI verify)
- Pet prefab missing Animator / controller, OR AnimatorSetup not generating a pet controller, OR
  the pet's locomotion script isn't writing Speed/Move params, OR avatar/rig mismatch (humanoid vs generic).

## Acceptance
- Pet plays idle when stationary and a walk/run cycle when moving — no T-pose at any time.
- Works on spawn and after scene load.
- No new console warnings from the pet animator.

## Do NOT touch
- Enemy animators, hero animator (unless shared factory is the root cause — if so, note it).

## Gate
Brace check; green build; commit `feat: implement WO-184 — pet animation T-pose fix`. No bake.
