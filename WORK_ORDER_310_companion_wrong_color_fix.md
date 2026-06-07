# WORK_ORDER_310 — Companion renders wrong color (green tint)

**Status: READY TO IMPLEMENT**
**Branch:** feat/tower-core-loop · **Lane:** 2 (Combat/AI) · **Origin:** owner playtest 2026-06-06 (screenshot)

## Problem
The story companion model renders **green** (missing/placeholder material or wrong diffuse), like the earlier
Ranger-green issue (WO-286 fixed Ranger via `HeroBodySwapper` diffuse repoint). The companion's material/texture
isn't resolving, so it falls back to a green/untextured tint.

## Goal
The companion renders with its correct material/texture, matching the other heroes.

## Likely cause / where to look
- `StoryCompanionInjector` / `HeroBodySwapper` material + diffuse assignment for the companion's class.
- A texture path that doesn't resolve (mirror the WO-286 Ranger fix: repoint to an existing loadable diffuse).
- Confirm it's not the gear/primitive path (WO-302/GearVisualApplier already disabled) and not a shader fallback.

## Acceptance criteria
- [ ] Companion renders with correct skin/material — no green/untextured tint — in village + combat.
- [ ] Root cause identified (missing texture path vs material assignment) and fixed at source, not tinted over.
- [ ] No regression to the other heroes' materials.
- [ ] Brace check; CompileGate OK; Windows build SUCCESS; verify in a play session.

## Do NOT touch
- No `.unity` edits. Reuse the WO-286 diffuse-repoint pattern; don't fork HeroBodySwapper.
