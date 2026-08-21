**Status:** CLOSED — owner range sweep 2026-08-21 (WO 0-800): completed or immaterial.

<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WO-226: Add Cleric Hero to Hero Select (Fourth Hero Class)

**Status: READY TO IMPLEMENT**

**Date:** 2026-06-01  
**Priority:** 🟡 MEDIUM (new hero class, expands gameplay)  
**Owner:** CLI  
**Depends On:** WO-215 (build mode), WO-217 (animation polish for combat feel)  
**Blocks:** None  
**Can Run In Parallel:** After core combat feel work (WO-217–219)

---

## Problem

Game currently has 3 hero classes (Knight, Archer, Mage). Adding cleric (healer) class provides new playstyle and strategy option for players.

---

## Solution

### Asset Integration

**Hero provided:**
- Hero name: **Elara** (cleric)
- Title: "The Divine Healer" — Master of Holy Light
- Theme: Holy/light magic (golden staff, glowing cross, divine aura)
- Weapon: Staff with glowing magical orb (gold/white light)
- Robes: Green/brown tunic, brown cloak, holy symbols
- Style: Priestess with braided hair, gentle but powerful expression
- Symbols: Holy cross, runes, divine light effects
- Role: **HEALER** (supports allies, damages enemies)

### Implementation

#### Phase 1: Hero Select Integration
1. **Import artwork to project**
   - Save as: `Assets/Art/Heroes/ElaraCard.png`
   - Ensure correct aspect ratio (matches other hero cards)

2. **Update Hero Select UI Layout**
   - File: `Assets/_Modules/HUD/HeroSelect/HeroSelectUI.cs`
   - **Currently has 3 slots (Knight, Archer, Mage)**
   - Add 4th slot for Elara card
   - Update layout/grid to accommodate 4 heroes (2x2 grid, or horizontal row)
   - Ensure spacing/alignment is clean
   - Card shows:
     - Portrait (the artwork)
     - Name: "Elara"
     - Class: "Cleric"
     - Tagline: "Master of Holy Light"

#### Phase 2: Hero Class Setup
3. **Create Cleric Hero Prefab**
   - `Assets/_Modules/Village/Heroes/ElaraCleric.prefab`
   - Base stats (health, damage, speed)
   - Animation setup (attack, cast, walk, idle)

#### Phase 3: Cleric Abilities (Core Kit)
4. **Design cleric ability set** — Example:
   - **Holy Strike:** Single target damage + weak heal (30% of damage back)
   - **Group Heal:** Heal all allies in radius (20 HP + 5s duration regen)
   - **Divine Protection:** Buff — reduce damage taken by 30% (2 targets, 8s)
   - **Smite:** Holy attack — deal extra damage to undead/dark enemies

5. **Implement abilities**
   - Create ability prefabs/scripts
   - Wire to ability bar UI
   - Add cooldown + mana/energy costs

#### Phase 4: Animations
6. **Create cleric animations**
   - Attack (staff swing)
   - Cast (healing/buff spell)
   - Walk, idle, death
   - Integrate into animator controller

#### Phase 5: Combat Integration
7. **Wire to combat system**
   - Add to auto-battle enemy spawning
   - Support in wave definitions
   - Test healing mechanics work with other heroes

---

## Acceptance Criteria

- [ ] Artwork imported to project
- [ ] Hero select UI layout updated (3 slots → 4 slots, clean spacing)
- [ ] Elara card visible in hero select screen (4th option)
- [ ] Card displays name, class, artwork, tagline correctly
- [ ] Clicking Elara card selects cleric
- [ ] Cleric hero spawns with correct model/stats
- [ ] Cleric animations (attack, cast, walk, idle) working
- [ ] 4 cleric abilities implemented + cooldowns working
- [ ] Cleric can heal allies in combat
- [ ] Cleric damage works correctly
- [ ] Party composition works (Knight + Archer + Mage + Cleric)
- [ ] WebGL tested: hero select shows 4 heroes, cleric playable
- [ ] Commit: "WO-226: add Elara cleric hero (4th class, healer role)"

---

## Design Notes

### Cleric Role
- **Primary:** Healing/support (unique vs Knight damage, Archer dps, Mage spell damage)
- **Secondary:** Utility (buffs, debuffs, crowd control)
- **Tertiary:** Moderate single-target damage

### Ability Philosophy
- Healing should be **meaningful** (not overpowered, not useless)
- Cleric should feel **different** to play vs other heroes
- Abilities should support **diverse strategies** (aggressive, defensive, balanced)

### Balance Consideration
- Cleric healing scales with attack stat (incentivize damage building)
- Cooldowns prevent spam healing (requires strategy)
- Mana/energy costs balance power vs cooldown

---

## Testing

1. Load game, hero select shows 4 heroes ✓
2. Select Elara, cleric spawns ✓
3. Attack enemies, verify damage ✓
4. Cast heal ability, allies heal ✓
5. Party with cleric feels natural (not overpowered, not useless) ✓
6. Cleric animations smooth ✓

---

## Notes

- Cleric is the 4th hero class — expands game's strategic depth
- Holy light theme is visually distinct from other heroes
- Healer role is a classic ARPG archetype, familiar to players
- Can add more abilities later (WO-future)

## Story Adaptation (Future)

With 4 hero choices, tutorial + early narrative should adapt based on hero selection:
- **Knight (Grom):** Tank/protector narrative
- **Archer (Sylas):** Scout/ranger narrative
- **Mage (Thrain):** Scholar/mystic narrative
- **Cleric (Elara):** Healer/priest narrative

This allows story beats, dialogue, and intro sequence to feel personalized to player choice. (See WO-222 for tutorial flow — can extend with hero-specific variations later.)

---

**Estimate:** 3–4 hours (hero prefab, 4 abilities, animations, combat integration, testing)

> **OWNER RULING 2026-08-21 (verbal, this session):** CLOSED by an explicit owner sweep of the WHOLE 0-800 RANGE: "I've eyeballed them many times, and all of them are already completed. or immaterial." This is a RANGE close on the owner's direct review, not a per-ticket verdict.
