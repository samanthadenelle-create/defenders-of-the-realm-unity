# WORK_ORDER_307 — HUD visual overhaul (sleek, grouped, responsive web + mobile)

**Status:** SUPERSEDED

> **SUPERSEDED - determined 2026-08-14 (phantom sweep).** Successor: `HudKitController` (the Obsidian HUD kit program).
> This WO was still being re-served by the DERIVED board (BOARD.html) because its Status line was
> never flipped when the successor work landed (CLAUDE.md §2). Body preserved below unchanged.
> _Prior status line, preserved: Status: READY TO IMPLEMENT (reconciled 2026-08-09 - queued as post-core polish by the owner 2026-06-07; no `.RESULT.md` and no commit references WO-307. RE-SCOPE FIRST: the HUD has since been rebuilt under the Obsidian / `HudKitController` program (WO-899 and siblings), so much of this list may be moot)_

**Status: QUEUED — later/polish (post-core; owner 2026-06-07).** Visual targets:
`docs/design/hud-landscape-concept.jpg` (Grok, landscape) + `docs/design/hud-vertical-mobile-concept.jpg`
(portrait). Sibling: WO-331 inventory/character screen (`docs/design/inventory-screen-concept.png`).
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

## Root cause (triage 2026-06-06)
**Confidence: Confirmed.** Two HUD systems DO coexist, exactly as the WO suspected:
- `VillageHudController` (code-built uGUI, the WIRED one) registers via `CoreServices.RegisterHud(this)`
  (`Assets/_Modules/HUD/VillageHudController.cs:99`), Canvas `sortingOrder = 100` (`:124`). All Village
  bridges target it.
- `HUDManager` (`Assets/_Modules/HUD/HUDManager.cs:23`) is a SECOND code-built Canvas singleton at
  `sortingOrder = 200` (`:141`) — i.e. it draws ON TOP of VillageHudController. Its `Start()` injects a
  hardcoded demo party "Archer/Mage/Knight" at 1900 HP (`:72-83`) → this is the "oversized party frames"
  placeholder the owner saw. Nothing gameplay feeds HUDManager (no Update; pure demo).
- Resource strip labels read "Gems" not "Crystals" (`VillageHudController.cs:146`, `SetResources` `:355-358`,
  `SetCrystals` `:349`) — that is WO-309.

**Suggested minimal fix:** keep `VillageHudController` as the single HUD; remove/disable `HUDManager` (or strip
its demo `Start()` and stop instantiating it) so there is no second overlapping Canvas. Then restyle frames /
add grouping on VillageHudController. No `.cs` brace risk beyond the file(s) touched.
**Overlap:** this is the shell for 308 (ability bar lives in VillageHudController.BuildSkillBar) and 309
(resource strip). Do 307 first; 308/309 are edits inside the same controller.

## Mobile-portrait layout (owner concept 2026-06-07 — docs/design/hud-vertical-mobile-concept.jpg)
The "responsive/mobile-first" half. Portrait arrangement to support:
- **Top status bar:** hero portrait + level/XP bar · Heart "Castle %" HP bar · **SKR + gold** currency ·
  "Wave N — <state>" banner.
- **Left rail:** ability / tower quick-icons (build + cast).
- **Bottom tray:** build-card deck — placeable towers (ghost preview) w/ cost + gem, refresh/cycle, hero cards
  (CoC / mobile-TD style).
- **Combat overlay:** damage numbers + CRIT! + skull-kills + target rings + enemy/tower health bars
  (DamageNumberSpawner + FloatingHealthBar — already built; FloatingHealthBar pill fixed WO-302).

**DECISION (owner roundtable 2026-06-07): COMPASS over minimap for the grant build.** The concept shows a
minimap, but a minimap is new work + intrusive on portrait mobile. Keep the existing **CompassHud** (smaller,
less intrusive) and make it **SMART** — point to the nearest objective/threat (incoming horde / gate / camp)
with an icon + distance — which closes the "hard to tell what you need" gap at a fraction of the cost.
Minimap = aspirational / post-core-polish. (Fix CompassHud visibility = WO-322; smart-target upgrade folds in.)

## Do NOT touch
- No `.unity` edits. Don't reference Village from DeNelle.HUD. Resource icons + ability bar are WO-309/308.
