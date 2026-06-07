# WORK_ORDER_322 — Compass not visible (can't orient at town exits)

**Status: READY TO IMPLEMENT**
**Branch:** feat/tower-core-loop · **Lane:** 4 (UI/HUD) · **Origin:** owner playtest 2026-06-06
**Reconcile with:** `CompassHud` / `CompassHudBootstrap` (WO-39); related DEF-152 (gate-crossing intel)

## Problem
The player can't tell which direction/location an exit leads because the **compass isn't visible** on the
HUD. The compass system exists (`CompassHud`, `CompassHudBootstrap`) but isn't showing in-scene.

## Goal
The compass is visible and functional on the HUD — shows heading (N/S/E/W) and enemy/POI direction so the
player can orient at the town exits (e.g. the Pet-House-side gate).

## Where to look
- `CompassHudBootstrap` — is it actually instantiated in this scene? (bootstrap not running / wrong scene gate.)
- `CompassHud` canvas sort order / anchoring (off-screen, behind another panel, or alpha 0) — and whether the
  HUD overhaul (WO-307) should own its placement.
- Confirm it updates heading + dots at runtime.

## Acceptance criteria
- [ ] Compass is visible on the HUD in town (and DTT/world where intended), correctly anchored, not clipped/behind panels.
- [ ] Shows heading (cardinal) and enemy/POI direction; updates live as the player turns/moves.
- [ ] Readable on web + mobile; consistent with the HUD theme (coordinate with WO-307).
- [ ] HUD→Core only; code-built; brace check; CompileGate OK; build SUCCESS.

## Do NOT touch
- No `.unity` edits. Don't fork CompassHud — fix the bootstrap/visibility. Gate-destination intel = DEF-152 (separate).
