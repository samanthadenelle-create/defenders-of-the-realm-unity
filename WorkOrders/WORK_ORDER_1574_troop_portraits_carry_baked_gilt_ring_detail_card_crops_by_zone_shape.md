# WO-1574: Troop portraits carry baked gilt ring; detail card crops by zone shape

**Status:** READY TO IMPLEMENT
**Silo:** Art + UI wiring - `Assets/Resources/Portraits/Troops/` + detail card panel.
**Source:** Manage pass-three lane handback 2026-09-07. Minted from the banner
(`CLI_LANES_WO_NUMBERS.md`, main line 1574 -> 1575 in the same edit).

## 1. EVIDENCE (re-read at source 2026-09-07)

- `Assets/Resources/Portraits/Troops/troop-*.png` are 1254x1254 medallions with a baked gilt
  ring as the outer frame.
- The Manage redesign mockup requires a rectangular troop portrait (the painting only, no ring).
- The detail card zone is currently non-square (cropped) as a workaround to hide the ring by
  envelope crop.
- The mockup authors rectangular framing; the current crop is a placeholder waiting for art.

## 2. FIX

**OWNER ACTION:** deliver nine rectangular troop paintings (remove the gilt ring, paint-only
format) for the following troop ids from `troops.json`:
- troop-archer
- troop-battlemage
- troop-catapult
- troop-echo-legionnaire
- troop-field-cleric
- troop-footman
- troop-outrider
- troop-shieldguard
- troop-spearman

**CLI ACTION (when art lands):** remove the zone-shape crop workaround from the detail card panel
and restore normal rectangular framing.

## 3. WHAT NOT TO TOUCH

Troop data model, stat displays, other portrait assets (hero, enemies, NPCs).

## 4. ACCEPTANCE

- [x] Nine rectangular troop paintings authored (no gilt ring, painting-only).
- [x] Crop workaround removed from detail card zone shape.
- [x] Detail card displays rectangular troop portrait without letterboxing.
- [ ] `COMPILE_GATE_OK` + `REGRESSION_OK n/n` on a fresh log (gate lane).

## FILES TO EDIT

- `Assets/Resources/Portraits/Troops/troop-*.png` (nine art files)
- Detail card panel code (zone shape restore)
