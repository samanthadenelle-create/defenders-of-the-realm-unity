<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WO-225: Add Mage Hero Card to Hero Select Screen

**Status: READY TO IMPLEMENT**

**Date:** 2026-06-01  
**Priority:** 🟢 LOW (polish, can defer)  
**Owner:** CLI  
**Depends On:** None  
**Blocks:** None  
**Can Run In Parallel:** WO-223, WO-224, any visual polish work

---

## Problem

Hero select screen needs updated mage card artwork. Current placeholder or outdated asset.

---

## Solution

### Asset Integration

**Artwork provided:**
- Hero name: **Thrain** (mage)
- Theme: Ice/crystal magic (glowing staff with ice shard, runes, arcane symbols)
- Weapon: Staff with crystalline orb (blue/white glow)
- Robes: Purple, blue, brown (scholarly wizard aesthetic)
- Style: Elderly wizard with long white beard, wise expression
- Symbols: Runes on robes, glowing crystal ball, mystical aura
- Props: Pouch, scrolls, adventurer gear

### Implementation

1. **Import artwork to project**
   - Save as: `Assets/Art/Heroes/ThrainCard.png` (or similar)
   - Ensure correct aspect ratio (matches other hero cards)

2. **Add to Hero Select UI**
   - File: `Assets/_Modules/HUD/HeroSelect/HeroSelectUI.cs` (or equivalent)
   - Update Mage card from old artwork to Thrain
   - Card should show:
     - Portrait (the artwork)
     - Name: "Thrain"
     - Class: "Mage"
     - Stats/abilities (if hero select shows them)

3. **Wire to Hero Selection**
   - Thrain is the mage hero (replaces old mage)
   - Spawn Thrain mage when selected
   - Load Thrain abilities into ability bar

---

## Acceptance Criteria

- [ ] Artwork imported to project
- [ ] Thrain card visible in hero select screen
- [ ] Card displays name, class, artwork correctly
- [ ] Clicking Thrain card selects mage
- [ ] Mage hero spawns with Thrain model
- [ ] WebGL tested: hero select works
- [ ] Commit: "WO-225: add Thrain mage hero card to hero select"

---

## Notes

- Artwork is high-quality fantasy illustration (wizard archetype)
- Ice/crystal theme shows clearly via staff and rune symbols
- Elderly wizard aesthetic distinct from other heroes
- Hero select layout should not be broken by card update (check spacing)

---

**Estimate:** 30–45 min (import asset, update UI, ensure mage spawns, test)
