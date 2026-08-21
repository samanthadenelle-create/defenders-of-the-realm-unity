**Status:** CLOSED — owner range sweep 2026-08-21 (WO 0-800): completed or immaterial.

<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WO-224: Add Knight Hero Card to Hero Select Screen

**Status: READY TO IMPLEMENT**

**Date:** 2026-06-01  
**Priority:** 🟢 LOW (polish, can defer)  
**Owner:** CLI  
**Depends On:** None  
**Blocks:** None  
**Can Run In Parallel:** WO-223, any visual polish work

---

## Problem

Hero select screen needs updated knight card artwork. Current placeholder or outdated asset.

---

## Solution

### Asset Integration

**Artwork provided:**
- Hero name: **Grom** (knight)
- Theme: Gold/light holy warrior (blessed armor, sacred symbols)
- Weapon: Hammer/mace (ornate, glowing)
- Shield: Large kite shield with sun emblem
- Style: Imposing warrior with beard, red cape, glowing light effects
- Symbols: Holy runes, winged emblems (light magic theme)

### Implementation

1. **Import artwork to project**
   - Save as: `Assets/Art/Heroes/GromCard.png` (or similar)
   - Ensure correct aspect ratio (matches other hero cards)

2. **Add to Hero Select UI**
   - File: `Assets/_Modules/HUD/HeroSelect/HeroSelectUI.cs` (or equivalent)
   - Update Knight card from old artwork to Grom
   - Card should show:
     - Portrait (the artwork)
     - Name: "Grom"
     - Class: "Knight"
     - Stats/abilities (if hero select shows them)

3. **Wire to Hero Selection**
   - Grom is the knight hero (replaces old knight)
   - Spawn Grom knight when selected
   - Load Grom abilities into ability bar

---

## Acceptance Criteria

- [ ] Artwork imported to project
- [ ] Grom card visible in hero select screen
- [ ] Card displays name, class, artwork correctly
- [ ] Clicking Grom card selects knight
- [ ] Knight hero spawns with Grom model
- [ ] WebGL tested: hero select works
- [ ] Commit: "WO-224: add Grom knight hero card to hero select"

---

## Notes

- Artwork is high-quality fantasy illustration (holy warrior theme)
- Gold/light theme matches knight identity
- Hammer + shield combo shown clearly
- Hero select layout should not be broken by card update (check spacing)

---

**Estimate:** 30–45 min (import asset, update UI, ensure knight spawns, test)

> **OWNER RULING 2026-08-21 (verbal, this session):** CLOSED by an explicit owner sweep of the WHOLE 0-800 RANGE: "I've eyeballed them many times, and all of them are already completed. or immaterial." This is a RANGE close on the owner's direct review, not a per-ticket verdict.
