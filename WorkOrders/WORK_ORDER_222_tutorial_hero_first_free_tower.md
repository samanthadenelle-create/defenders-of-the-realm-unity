<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WO-222: Tutorial Redesign — Hero Combat First, Free Tower Placement

**Status: READY TO IMPLEMENT**

**Date:** 2026-06-01  
**Priority:** 🟡 HIGH (new player experience, onboarding)  
**Owner:** CLI  
**Depends On:** WO-215 (build mode click-to-place must work first)  
**Blocks:** None (but improves player retention)

---

## Problem

Current tutorial likely teaches tower placement before hero mechanics. New players don't understand what they're building or why before they're managing resources.

**Better flow:**
1. Learn hero attacks (what does my character do?)
2. Learn tower placement (how do I defend?)
3. Learn resource management (then costs matter)

---

## Solution

### Tutorial Phase 1: Hero Combat (FREE, NO ENEMIES YET)
- Spawn player hero in village
- Show attack button / ability UI
- Let player practice attacking nothing / training dummies
- Goal: "Get comfortable attacking"
- No tower yet, no enemies

### Tutorial Phase 2: Tower Placement (FREE TOWER COST)
- Show grid overlay
- Explain building placement (green = valid, red = invalid)
- Let player place 1–2 towers **for free** (no crystal cost)
- Goal: "Learn where towers go"

### Tutorial Phase 3: First Wave (TOWER WORKS, FREE STILL)
- Spawn 2–3 weak enemies
- Tower auto-attacks
- Player hero can join in
- Goal: "See tower defend with you"

### Tutorial Phase 4: Resource Management (COSTS NOW APPLY)
- Show crystal counter
- Explain tower cost (e.g., 50 crystals)
- Player places tower with real cost
- Goal: "Understand resource/placement tradeoff"

### Tutorial Phase 5: Need More Supplies (EXPANSION TEACHING)
- After first wave, say: "That tower helped, but one isn't enough."
- Suggest placing a second tower
- Show cost: "You need 50 more crystals for another tower"
- Explain: "You'll need to find resources. Check the camps around Elarion."
- Quest marker / hint: Point to overworld entrance or camp location
- Goal: "Understand you must gather before building more"

### Tutorial Phase 6: Real Game (FULL RULES)
- Player leaves to find camps / gather resources
- Normal wave escalation
- All mechanics active
- Player ready for actual gameplay

---

## Implementation

**Files to modify:**
- `Assets/_Modules/Core/Tutorial/TutorialManager.cs` (or create if missing)
- `Assets/_Modules/Village/Build/BuildModeManager.cs` (toggle free placement)
- `Assets/_Modules/Core/Combat/CostSystem.cs` (allow zero-cost towers during tutorial)

**Pseudocode:**
```csharp
public class TutorialManager : MonoBehaviour
{
    public enum TutorialPhase { HeroAttack, TowerPlacement, FirstWave, Resources, NeedSupplies, FreePlay }
    
    public void SetPhase(TutorialPhase phase)
    {
        switch(phase)
        {
            case TutorialPhase.HeroAttack:
                // Show hero, disable tower UI
                break;
            case TutorialPhase.TowerPlacement:
                // Show grid, enable placement, cost = 0
                break;
            case TutorialPhase.FirstWave:
                // Spawn 2–3 enemies, tower auto-attacks
                break;
            case TutorialPhase.Resources:
                // Cost applies now
                break;
            case TutorialPhase.NeedSupplies:
                // Show "need more crystals" message
                // Suggest second tower
                // Quest marker: point to camps/overworld
                break;
            case TutorialPhase.FreePlay:
                // Normal game, all rules active
                break;
        }
    }
}
```

---

## Acceptance Criteria

- [ ] Tutorial phase manager created
- [ ] Phase 1: Hero attack tutorial works (no tower UI visible)
- [ ] Phase 2: Tower placement tutorial (grid visible, cost = 0, green/red validation)
- [ ] Phase 3: First wave spawns 2–3 weak enemies
- [ ] Phase 4: Tower cost re-enabled, player places real tower
- [ ] Phase 5: "Need supplies" message shown, second tower suggested
- [ ] Phase 5: Quest marker points to camps/overworld for resource gathering
- [ ] Phase 6: Normal gameplay resumes
- [ ] New player can complete all 6 phases without confusion
- [ ] WebGL tested: tutorial feels smooth, teaches mechanics
- [ ] Commit: "WO-222: redesign tutorial (hero first, free tower placement)"

---

## Testing

1. New player loads game
2. Follows hero attack tutorial — understands attack mechanics ✓
3. Follows tower placement tutorial — understands grid validation ✓
4. Sees first wave — understands tower defends ✓
5. Tries placing tower with cost — understands resource tradeoff ✓
6. Tutorial says "need more supplies" → suggests second tower ✓
7. Quest marker appears → "go find resources in camps" ✓
8. Player exits to overworld, understands they must gather before expanding ✓
9. Ready for real game

---

## Notes

- **Order matters:** Hero → Tower → Resources (not the reverse)
- **Free tower teaches without penalty:** Player can experiment
- **Real cost applies after understanding:** No frustration from hidden mechanics
- Tutorial should be **skippable** (button to skip and go straight to game)

---

**Estimate:** 1.5–2 hours (tutorial flow, phasing logic, UI updates, testing)
