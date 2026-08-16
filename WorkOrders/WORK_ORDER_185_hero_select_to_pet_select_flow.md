# WORK ORDER 185 — Hero Select Skips Pet Select (drops straight into village)

**Status:** READY — PARTIAL - remaining: it is flag-gated OFF at FeatureFlags.cs:181

> **PARTIAL - re-scoped 2026-08-14 (phantom sweep).** Remaining work: shipped but FLAG-GATED OFF at FeatureFlags.cs:181.
> Everything else in this WO is present in HEAD. The named remainder IS the ticket now - do not
> re-implement the shipped part.
> _Prior status line, preserved: Status: READY TO IMPLEMENT_

**Lane:** UI / FTUE — code, parallel-safe
**Source:** playtest 2026-05-31
**Priority:** P1 (onboarding flow broken — player never picks a pet)

## Problem
After the player selects a hero, the game **drops them directly into the village**. It should first
present a **pet selection screen** (choose starter pet: Aether Sprite / Flame Pup / Ice Wolf), then
enter the village with the chosen pet.

## Expected flow
Title → hero select → **PET SELECT (missing)** → enter village with chosen hero + pet.

## Acceptance
- After hero select, a pet-select screen appears with the three starter pets.
- Selecting a pet sets the active companion, then loads into the village.
- Chosen pet is the one that spawns beside the hero (verify it's not a hardcoded default).
- Code-built UI (no UXML — does not render in builds, per project memory).
- Wires into FTUE sequence (relates to WO-133, WO-42 hero-select screen).

## Open question for owner
- Is pet locked per hero class, or free choice for any hero? *Default: free choice of all 3.*

## Gate
Brace check; green build; commit `feat: implement WO-185 — pet select screen in onboarding`. No bake.
