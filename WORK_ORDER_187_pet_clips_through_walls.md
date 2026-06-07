# WORK ORDER 187 — Pet Clips Through Walls

**Status:** READY TO IMPLEMENT
**Lane:** B (Combat/AI — code, parallel-safe). Separate issue from WO-184 (pet T-pose).
**Source:** playtest 2026-05-31
**Priority:** P2 (immersion break; pet ignores collision)

## Problem
The pet companion walks/floats **through walls** — it ignores wall collision and/or isn't bound to
the navmesh, so it passes through solid geometry.

## Likely cause (CLI verify)
- Pet follow/locomotion uses direct transform movement (lerp-to-hero) instead of a NavMeshAgent, OR
  its agent ignores the wall colliders / off-mesh constraints.

## Acceptance
- Pet respects wall colliders — cannot pass through walls; routes around them to follow the hero.
- Still keeps up with the hero (no getting permanently stuck — add a teleport-to-owner failsafe if it falls too far behind).
- No new console spam from the pet pathing.

## Do NOT touch
- Pet animation (WO-184 handles T-pose), enemy pathing.

## Gate
Brace check; green build; commit `feat: implement WO-187 — pet wall collision`. No bake.
