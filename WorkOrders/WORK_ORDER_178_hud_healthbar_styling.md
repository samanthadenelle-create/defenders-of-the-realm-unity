# WORK ORDER 178 — HUD health-bar styling (match the game's themed UI)

**Status: READY TO IMPLEMENT**
**Priority:** Medium — HUD polish; the health bars are the most-seen UI and currently look unstyled.
**Date:** 2026-05-31
**Lane:** UI — `VillageHudController` (code-built HUD) styling. No gameplay change; no bake.
**Source:** owner playtest — *"health bar UI should match the [tree/themed UI] above in styling."*

---

## The problem
The top-left HUD bars — **Elarion (Heart) HP** and **Hero HP** — are plain flat green bars in a basic
dark box. They look **unstyled / placeholder** next to the rest of the HUD, which already has a polished
themed look: the **Daily Quest panels** (top-right) are nicely styled (rounded dark-purple cards, themed
borders, clean type), and the ability bar + currency chips are themed too. The health bars don't match —
they're the odd one out.

## The goal
Restyle the **Heart (Elarion) and Hero health bars** to **match the game's existing themed HUD language**
— the same treatment as the Daily Quest panels / ability bar (themed frame, palette, typography, rounded
corners, proper fill styling). Make the HUD read as **one coherent styled set**, not a styled HUD with two
placeholder green bars stuck on.

## What to do (code-built HUD styling — `VillageHudController`)
- The bars are built in `VillageHudController` (`heart-hp-fill` / hero HP fill VisualElements,
  `:93/:137/:356`, width-driven fills with `HeartCriticalClass`/`HeartWarningClass`). This is a **USS/style
  pass**, not a rewrite.
- **Match the themed look:** themed bar frame/background (the dark-purple card style the quest panels use),
  rounded corners, a styled fill (gradient or themed color — keep the green→amber→red HP states, just
  styled, not flat), themed label typography ("Elarion", "Hero 100/100"), maybe a small icon (a Heart/tree
  crest for Elarion, a hero crest for Hero).
- **Keep the HP-state coloring** (critical/warning classes already exist) — restyle them to fit the palette
  (e.g. healthy = themed green, warning = amber, critical = red pulse) rather than removing the feedback.
- **Consistency:** the Heart bar and Hero bar should share the style and sit in a cohesive top-left cluster
  matching the quest-panel / ability-bar treatment. Mobile-legible.

## Constraints
- **Style/USS pass only** — keep the bar logic (fill fraction, HP-state classes, bindings) intact. Don't
  touch the HP gameplay.
- **Code-built UI** (the HUD uses code-built UIDocument — repo rule; no UXML reliance).
- Reuse the existing HUD palette/fonts/card style (the Daily Quest panel style is the reference target).
- Brace balance; no bake.

## Acceptance criteria
1. Heart (Elarion) + Hero HP bars are **restyled to match the themed HUD** (quest-panel/ability-bar language) — framed, rounded, themed fill + type, optional crest icon — not flat green placeholders.
2. HP-state feedback (healthy/warning/critical color) preserved, restyled to the palette.
3. HUD reads as one coherent styled set; mobile-legible.
4. Bar logic/bindings unchanged (style-only); code-built; brace balance; no bake.

## Open questions for owner
- **Reference target** — match the **Daily Quest panel** style specifically, or do you have a different
  "tree above" element in mind for the bars to echo? (Confirm what "the tree above" refers to — the Heart/
  world-tree crest, or a specific UI panel's styling.)
- **Crest icons** — want a Heart/tree icon on the Elarion bar + a hero icon on the Hero bar, or text-only?

> Note: the broader "town rebuild not fully here" is the **WO-152 full city redesign** (designer-led) —
> the current bake shows the interim roster + walls/tower; the full district city is that separate WO.
> This WO is HUD-only.

## Done checklist (CLAUDE.md §10)
- [ ] Heart + Hero HP bars restyled to the themed HUD language (frame/fill/type/icon)
- [ ] HP-state coloring preserved + palette-matched; coherent HUD set; mobile-legible
- [ ] Style-only (logic/bindings intact); code-built; brace balance; no bake
- [ ] `WORK_ORDER_178_hud_healthbar_styling.RESULT.md` when complete
