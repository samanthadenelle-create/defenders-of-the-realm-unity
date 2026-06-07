# WO-217: Animation Polish — Anticipation, Impact, Recovery

**Status: READY TO IMPLEMENT**

**Date:** 2026-06-01  
**Priority:** 🔴 CRITICAL (combat feel — biggest win for buck)  
**Owner:** CLI  
**Depends On:** None  
**Blocks:** WO-218, WO-219, WO-220 (visual/UI polish depends on solid animations first)

---

## Problem

Current animations feel **sluggish and unresponsive**. Attacks lack punch. No clear anticipation/impact/recovery phases.

From playtesting: **animation is the worst part of combat feel.**

---

## Solution

Polish existing attack animations (don't re-mocap):

### Phase 1: Hero Attacks (Knight/Mage/Ranger)

**For each hero, audit & improve:**

1. **Anticipation phase (2–4 frames)**
   - Wind-up before swing/cast
   - Example: Knight sword raise before slash
   - Example: Mage hand glow/charge before fireball
   - Makes player see attack is coming, feels intentional

2. **Active phase (4–8 frames)**
   - The actual swing/cast
   - Should be fast & snappy (not slow)
   - Contact frame is important (when spell hits or blade connects)

3. **Recovery phase (4–6 frames)**
   - Quick return to neutral/idle
   - NOT a long wind-down
   - Player should feel in control again quickly
   - Old Zelda trick: recover faster than player expects

**Example timings:**
```
Knight Slash: 
  Anticipation (2f) + Active (6f) + Recovery (4f) = 12 frames total @ 30fps = 0.4s

Mage Fireball Cast:
  Anticipation (4f: glow/charge) + Active (4f: cast) + Recovery (3f) = 11f = 0.37s
```

### Phase 2: Enemy Attacks

Same treatment for all enemy types:
- Skeleton slash
- Skeleton mage cast
- Boss attacks
- Pet attacks

### Phase 3: Pet Attacks

Pet engagement/attack animations:
- Pet engage (run toward enemy)
- Pet attack (bite/claw)
- Pet recovery

---

## Implementation

**Files to modify:**
- `Assets/Generated/Animators/HumanoidEnemy.controller` (or equivalent)
- `Assets/Generated/Animators/LargeEnemy.controller`
- `Assets/Generated/Animators/Boss.controller`
- `Assets/Generated/Animators/Hero.controller`
- `Assets/Generated/Animators/Pet.controller`

**Tools:**
- Unity Animator window (scrub through animation timeline)
- Frame-by-frame playback (slow down in inspector)
- Preview on actual game character (not just in animator)

**Process:**
1. Load each animation in Animator
2. Play frame-by-frame (Shift+Right Arrow)
3. Identify where anticipation should start
4. Identify contact frame (where impact happens)
5. Trim/extend frames to match desired timing (12–16 frames for attacks)
6. Test in-game: does it feel snappy?

---

## Acceptance Criteria

- [ ] Knight slash: clear anticipation (sword raise), fast active, quick recovery
- [ ] Mage fireball: clear charge/glow anticipation, snappy cast, quick recovery
- [ ] Ranger shot: clear draw anticipation, instant release, quick recovery
- [ ] All enemy attacks: anticipation + active + recovery phases visible
- [ ] Pet engage + attack: snappy, responsive
- [ ] WebGL tested: attacking feels responsive, not sluggish
- [ ] Commit: "WO-217: polish attack animations (anticipation/impact/recovery)"

---

## Testing

Load WebGL, go to village or zone, attack enemies. Does attacking feel:
- ✅ Responsive (not delayed)?
- ✅ Punchy (clear start/stop)?
- ✅ In control (recovery is quick)?

If yes, move to WO-218 (layering). If no, iterate more on timing.

---

## Notes

- **Key principle:** Good keyframed animations + timing > mocap smoothness
- **Don't aim for realistic.** Aim for **game feel** (snappy, responsive, punchy)
- Zelda/Monster Hunter philosophy: recovery is faster than player expects
- Even 12-frame attacks can feel amazing if timed right

---

**Estimate:** 2–3 hours (audit each animation, adjust frames, test, iterate)
