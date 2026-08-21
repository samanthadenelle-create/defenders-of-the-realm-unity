**Status:** CLOSED — owner range sweep 2026-08-21 (WO 0-800): completed or immaterial.

<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WO-218: Animation Layering — Upper Body Combat Layer for Responsive Feel

**Status: READY TO IMPLEMENT**

**Date:** 2026-06-01  
**Priority:** 🔴 CRITICAL (combat feel multiplier — makes combat responsive)  
**Owner:** CLI  
**Depends On:** WO-217 (animation polish must complete first)  
**Blocks:** WO-219, WO-220 (layering is required for other polish to shine)

---

## Problem

Current animations are **stiff and unresponsive**. When hero attacks, whole body stops — player movement halts, feels sluggish. Can't attack while moving, making combat feel delayed.

**Solution:** Use animation layers so upper body can attack while lower body continues walking/running. This is a core technique in responsive action games.

---

## Solution

### The Layering Strategy

Unity Animator supports **multiple layers** (Base, Upper Body, Additive). Use:

1. **Base Layer** (Weight: 1.0)
   - Movement animations (idle, walk, run)
   - Applies to whole body

2. **Upper Body Layer** (Weight: 1.0, mask: upper body only)
   - Attack animations (slash, cast, shoot)
   - Only affects arms/torso, NOT legs
   - Plays over base layer movement

3. **Additive Layer** (optional, Weight: 1.0, additive mode)
   - Hit reactions, flinches, knockback
   - Layers on top of everything

### Implementation

**Files to modify:**
- `Assets/Generated/Animators/Hero.controller`
- `Assets/Generated/Animators/HumanoidEnemy.controller`
- `Assets/Generated/Animators/LargeEnemy.controller`
- `Assets/Generated/Animators/Boss.controller`
- `Assets/Generated/Animators/Pet.controller`

**Per animator:**

1. **Create Upper Body Layer**
   - Right-click Layers panel → New Layer
   - Name it "Upper Body"
   - Set weight to 1.0
   - Set blending mode to **Override** (not Additive initially)

2. **Create Avatar Mask (if not exists)**
   - Right-click in Assets → Create → Avatar Mask
   - Name: `HumanoidUpperBodyMask` (or per-character)
   - In inspector: uncheck legs, hands (keep arms, torso)
   - Assign to Upper Body layer

3. **Set up Upper Body state machine**
   - States: AttackStart, AttackActive, AttackRecover, Idle
   - Transitions based on attack input
   - Exits back to Idle when attack completes
   - **Sync with Base Layer:** Base layer continues walk/run underneath

4. **Wire to code**
   - When player presses attack, trigger Upper Body layer animation
   - Base layer continues movement uninterrupted
   - Upper body animation plays over top

---

## Code Example

```csharp
// Pseudocode
public class HeroAnimator : MonoBehaviour
{
    private Animator animator;
    private int attackHashUpper;
    
    void Awake()
    {
        animator = GetComponent<Animator>();
        attackHashUpper = Animator.StringToHash("AttackUpper");
    }
    
    public void PlayAttack()
    {
        // Base layer continues movement
        // Upper body layer plays attack
        animator.SetTrigger(attackHashUpper);  // Triggers on upper body layer
    }
    
    public void UpdateMovement(Vector3 input)
    {
        // Base layer handles movement
        animator.SetFloat("Speed", input.magnitude);
    }
}
```

---

## Visual Comparison

**Before (stiff):**
```
Player running → Clicks attack → ENTIRE BODY stops → Plays attack animation → Resumes running
Feels: Delayed, interrupts flow
```

**After (responsive):**
```
Player running → Clicks attack → Legs keep running, arms attack simultaneously → Attack finishes → Back to pure running
Feels: Responsive, smooth, in-control
```

---

## Acceptance Criteria

- [ ] Upper Body layer created on all hero animators
- [ ] Upper Body layer created on all enemy animators
- [ ] Avatar masks created (upper body only, exclude legs)
- [ ] Upper Body layer transitions working (attack → idle)
- [ ] Hero can attack while moving (legs + upper body layered)
- [ ] Enemies can attack while moving toward player
- [ ] No animation clipping (legs/arms crossing weirdly)
- [ ] Attack animations still feel snappy (WO-217 timing preserved)
- [ ] WebGL tested: attacking while moving feels natural, responsive
- [ ] Commit: "WO-218: add upper body animation layer (attack while moving)"

---

## Testing

1. Load game, pick hero, move forward
2. Hold movement input + press attack
3. Verify: legs move forward, arms attack, doesn't stall
4. Verify: attack still feels snappy (not slowed by layering)
5. Test on all enemy types (should also feel reactive)

---

## Notes

- Avatar masks are per-character usually (Humanoid can share)
- If animations look weird, adjust mask (may need to include/exclude hands)
- Layer blending can be tweaked (try Override first, Additive if too harsh)
- This is a **multiplier** for WO-217 — makes snappy animations feel even better

---

**Estimate:** 1–1.5 hours (set up layers, create masks, test blending, iterate)

> **OWNER RULING 2026-08-21 (verbal, this session):** CLOSED by an explicit owner sweep of the WHOLE 0-800 RANGE: "I've eyeballed them many times, and all of them are already completed. or immaterial." This is a RANGE close on the owner's direct review, not a per-ticket verdict.
