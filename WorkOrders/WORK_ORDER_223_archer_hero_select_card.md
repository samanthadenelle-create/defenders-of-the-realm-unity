**Status:** CLOSED — owner range sweep 2026-08-21 (WO 0-800): completed or immaterial.

<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WO-223: Add Archer Hero Card to Hero Select Screen

**Status: READY TO IMPLEMENT**

**Date:** 2026-06-01  
**Priority:** 🟢 LOW (polish, can defer)  
**Owner:** CLI  
**Depends On:** None  
**Blocks:** None  
**Can Run In Parallel:** Any visual polish work

---

## Problem

Hero select screen needs archer card artwork. Current placeholder or missing asset.

---

## Solution

### Asset Integration

**Artwork provided:**
- Hero name: **Sylas** (archer)
- Theme: Green magic/nature (leaf emblems, wind effects)
- Weapon: Bow (ornate, curved)
- Style: Fantasy archer with elf-like features

### Implementation

1. **Import artwork to project**
   - Save as: `Assets/Art/Heroes/SylasCard.png` (or similar)
   - Ensure correct aspect ratio (matches other hero cards)

2. **Add to Hero Select UI**
   - File: `Assets/_Modules/HUD/HeroSelect/HeroSelectUI.cs` (or equivalent)
   - Add Sylas card alongside Knight, Mage
   - Card should show:
     - Portrait (the artwork)
     - Name: "Sylas"
     - Class: "Archer"
     - Stats/abilities (if hero select shows them)

3. **Wire to Hero Selection**
   - Add Sylas prefab/class to selectable heroes
   - Spawn Sylas hero when selected
   - Load Sylas abilities into ability bar

---

## Acceptance Criteria

- [ ] Artwork imported to project
- [ ] Sylas card visible in hero select screen
- [ ] Card displays name, class, artwork correctly
- [ ] Clicking Sylas card selects archer
- [ ] Archer hero spawns in game
- [ ] WebGL tested: hero select works
- [ ] Commit: "WO-223: add Sylas archer hero card to hero select"

---

## Notes

- Artwork is high-quality fantasy illustration
- Green/nature theme should match other archer visuals
- Hero select layout should not be broken by new card (check spacing)

---

**Estimate:** 30–45 min (import asset, add to UI, wire to hero system, test)

> **OWNER RULING 2026-08-21 (verbal, this session):** CLOSED by an explicit owner sweep of the WHOLE 0-800 RANGE: "I've eyeballed them many times, and all of them are already completed. or immaterial." This is a RANGE close on the owner's direct review, not a per-ticket verdict.
