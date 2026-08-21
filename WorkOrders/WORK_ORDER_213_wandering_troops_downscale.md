**Status:** CLOSED — owner range sweep 2026-08-21 (WO 0-800): completed or immaterial.

<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WO-213: Downscale Wandering Troop Sizes + Replace Battle UI Pills with Character Models

**Status: READY TO IMPLEMENT**

**Date:** 2026-06-01  
**Priority:** 🟠 MEDIUM (visual/UX improvement, zone battle clarity)  
**Owner:** CLI  
**Depends On:** None  
**Blocks:** None  
**Can Run In Parallel:** WO-212, WO-214 (all visual polish work can run together after WO-196 + WO-211)  

---

## Problem

### 1. Wandering troops are too large
Skeleton enemies spawned in the overworld (Zone/Region enemy encounters) appear oversized compared to the player. Should be 60–70% of current scale to match the village combat scale.

### 2. Battle UI shows pills instead of actual character visuals
When wandering troops engage in combat, the ATB battle panel shows circular generic pills for BOTH player party AND enemies instead of actual character portraits/visuals. 
- **Player side:** should show hero character visuals (Knight, Mage, Ranger) + pet
- **Enemy side:** should show enemy character visuals (skeleton types, boss, etc.) instead of generic pills

---

## Solution

### Part A: Downscale wandering enemy spawns

**In `Assets/_Modules/Village/Enemies/` or enemy spawning code:**
- Find the enemy instantiation for overworld/zone spawns
- Reduce `transform.localScale` by 0.6–0.7x (40–30% reduction)
- Test: enemy should now match village wave enemy proportions

**Candidates to check:**
- `RegionEnemySpawning.cs` or similar (if it exists)
- `WaveManager.cs` spawn method
- Any `VisualFactory.Skin()` calls for overworld enemies

### Part B: Replace ALL battle UI pills with actual character visuals

**In `Assets/_Modules/BattleATB/` (ATB battle panel):**

#### Player side:
- Locate the code that renders the player party in the battle HUD
- Currently renders: circular pill icons (generic cosmetic style)
- Change to: load hero character model previews (Knight/Mage/Ranger) + pet visuals

**Expected behavior:**
- Hero slot: shows Knight/Mage/Ranger model thumbnail based on current hero class
- Pet slot: shows pet model thumbnail (aether-sprite/flame-pup/ice-wolf)

#### Enemy side:
- Locate the code that renders the enemy party in the battle HUD
- Currently renders: circular pill icons (generic enemy style)
- Change to: load actual enemy model visuals (skeleton types, boss, etc.)

**Expected behavior:**
- Each enemy slot: shows the actual enemy model/portrait (Skeleton_Minion, Skeleton_Warrior, Skeleton_Mage, Boss_Dragon, etc.)
- Visuals should match the 3D models spawned on the battlefield

**Files likely involved:**
- `BattleController.cs` or `BattleHUD.cs`
- `BattleUIFactory.cs` or equivalent UI builder
- Hero class enum + visual mapping
- Enemy type enum + visual mapping

---

## Acceptance Criteria

- [ ] Overworld enemies spawn at 60–70% previous scale
- [ ] Skeleton enemies in zone encounters match village proportions
- [ ] Battle HUD player side: shows hero character model (Knight/Mage/Ranger, not generic pill) in hero slot
- [ ] Battle HUD player side: shows pet model (aether-sprite/flame-pup/ice-wolf) in pet slot
- [ ] Battle HUD enemy side: shows actual enemy character models/visuals (Skeleton_Minion, Skeleton_Warrior, etc., not generic pills)
- [ ] Launch WebGL build, walk into a zone encounter, verify all visual changes
- [ ] Commit: "WO-213: downscale wandering troops, replace all battle UI pills with character models"

---

## Risk

Scaling change affects only overworld spawning, not village waves (separate code path). If scaling is applied globally, village waves may become too small — audit spawn code to target only zone enemies.

---

**Estimate:** 35–50 min (find spawn code + adjust scale, locate battle UI code + swap both player AND enemy icon sources + test)

> **OWNER RULING 2026-08-21 (verbal, this session):** CLOSED by an explicit owner sweep of the WHOLE 0-800 RANGE: "I've eyeballed them many times, and all of them are already completed. or immaterial." This is a RANGE close on the owner's direct review, not a per-ticket verdict.
