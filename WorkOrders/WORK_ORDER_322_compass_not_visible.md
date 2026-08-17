<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

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

## Root cause (triage 2026-06-06)
**Confidence: Likely.** The compass is a **UI Toolkit (UIDocument) overlay**, but the HUD has migrated to
code-built uGUI — so the compass has no PanelSettings to attach to and **self-disables**:
- `CompassHud.Awake` requires a `UIDocument.panelSettings`; it tries to borrow one from any other UIDocument in
  the scene, and if none is found it does `enabled = false; return;`
  (`Assets/_Modules/HUD/CompassHud.cs:51-61`).
- The main HUD is now `VillageHudController` — pure uGUI Canvas, NO UIDocument/PanelSettings
  (`Assets/_Modules/HUD/VillageHudController.cs:23-24`, the file explicitly dropped UXML). So in a scene whose
  only HUD is VillageHudController, there is no PanelSettings to borrow → CompassHud disables → invisible.
- Secondary: `CompassHudBootstrap` only spawns the compass if it resolves a `HeroLocomotion` by reflection
  (`Assets/_Modules/HUD/CompassHudBootstrap.cs:37-38, 56-62`) — fine if a hero exists.

**Suggested minimal fix:** either (a) rebuild the compass as a code-built uGUI overlay (mirror
VillageHudController) so it no longer needs PanelSettings, or (b) guarantee a PanelSettings-bearing UIDocument
exists in the scene. Option (a) is consistent with the WO-307 uGUI HUD and PIPELINE_STATE §8 (UXML doesn't
render in builds). Coordinate placement with WO-307. Don't fork CompassHud's math.

## Do NOT touch
- No `.unity` edits. Don't fork CompassHud — fix the bootstrap/visibility. Gate-destination intel = DEF-152 (separate).
