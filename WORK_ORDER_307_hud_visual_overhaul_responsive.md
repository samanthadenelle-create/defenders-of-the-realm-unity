# WORK_ORDER_307 — HUD visual overhaul (sleek, grouped, responsive web + mobile)

**Status: READY TO IMPLEMENT**
**Branch:** feat/tower-core-loop · **Lane:** 4 (UI/HUD) · **Origin:** owner playtest 2026-06-06 (screenshot)
**Depends on:** none · **Reconcile with:** WO-302, WO-303, WO-308, WO-309, WO-110

## Problem
Current in-game HUD reads as a programmer-art placeholder: flat grey boxes, ungrouped panels, plain text
resources, empty black ability slots, oversized party frames. Needs a sleek, stylish, cohesive look with
clear grouping and responsive controls that work for **web AND mobile-first** play.

## Goal
A unified, themed HUD: grouped clusters, consistent spacing/scale, fantasy theme (earthy #2c2115, stone
#8b5e3c, gold #d4af37, parchment text), responsive anchoring for landscape web + mobile (portrait/landscape),
large touch targets.

## Scope (this WO = the shell + style; sub-pieces are 308/309)
- Reconcile the two HUD paths: the in-scene `VillageHudController` (DeNelle.HUD) and the new `HUDManager`
  (combat party HUD). Pick ONE styled system; don't run two overlapping HUDs.
- Group clusters: party (top-left), target (top-centre), resources (top bar — see WO-309), ability bar
  (see WO-308), daily quests (right, collapsible), build button (bottom-right).
- Consistent panel frames (rounded, stone/gold trim), drop the flat grey boxes; size party frames down.
- Responsive: CanvasScaler ScaleWithScreenSize, safe-area margins, anchors that hold in portrait + landscape;
  touch targets ≥ ~80px.

## Files (HUD → Core only; bridges in Village)
- `Assets/_Modules/HUD/VillageHudController.cs` and/or `Assets/_Modules/HUD/HUDManager.cs` (consolidate)
- Shared theme constants (new `HudTheme.cs` in DeNelle.HUD)

## Acceptance criteria
- [ ] One cohesive themed HUD (no duplicate overlapping HUD systems).
- [ ] Clusters grouped + consistently styled; party frames right-sized; no flat grey placeholder boxes.
- [ ] Holds up in landscape (web) AND portrait/landscape mobile; targets ≥80px; safe-area respected.
- [ ] Code-built UI (no UXML in builds); HUD references Core only (bridges live in Village).
- [ ] Brace check; CompileGate `COMPILE_GATE_OK`; Windows build SUCCESS.

## Do NOT touch
- No `.unity` edits. Don't reference Village from DeNelle.HUD. Resource icons + ability bar are WO-309/308.
