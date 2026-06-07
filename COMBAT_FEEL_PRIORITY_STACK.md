# Combat Feel — Priority Stack (WO-217 to WO-220)

**Status:** All READY TO IMPLEMENT  
**Total Scope:** 5–8 hours  
**Biggest Impact:** Phase 1 (WO-217 animation polish)

---

## Why Combat Feel Matters

From playtesting: **"animation is the worst part of combat"**

This stack targets the exact issue: sluggish, unresponsive attacks that lack punch. Each phase adds a multiplier of feel.

---

## Execution Order (Must Follow)

### 🔴 Phase 1: Animation Polish (WO-217)
**Do this FIRST — biggest bang for buck**

- **Goal:** Make attacks snappy (anticipation → impact → recovery)
- **Time:** 2–3 hours
- **Impact:** Immediately feels better, foundation for everything else
- **Tests:** Does attacking feel fast & responsive? Yes = move to Phase 2

### 🔴 Phase 2: Animation Layering (WO-218)
**Do immediately after WO-217**

- **Goal:** Upper body layer so you can attack while moving
- **Time:** 1–1.5 hours
- **Impact:** Multiplies WO-217 effect (snappy attacks + responsive feel)
- **Tests:** Can attack while moving? Do legs + arms layer correctly? Yes = move to Phase 3

### 🔴 Phase 3: Visual Feedback (WO-219)
**Do immediately after WO-218**

- **Goal:** Hit-stop, screen shake, particles, trails, damage numbers
- **Time:** 2–3 hours
- **Impact:** Makes hits feel **heavy** and **satisfying**
- **Tests:** Do all effects fire on hit? Does it feel punchy? Yes = move to Phase 4

### 🟡 Phase 4: Audio Feedback (WO-220) — NOT YET WRITTEN
**Do after WO-219**

- **Goal:** Attack whoosh, impact sounds, spell casts, hit feedback sounds
- **Time:** 1.5–2 hours
- **Impact:** Locks in the feel with sound design
- **Tests:** Do sounds reinforce hits? Feel cohesive with visuals?

---

## Parallel Work

After Phases 1–3, these can run in parallel with other work:
- WO-212: Gate z-fighting fix
- WO-213: Troop downscale + battle UI pills
- WO-214: Dual-camera system
- WO-221: Defend tower camera

---

## Expected Outcome

**Before:** Attacks feel sluggish, soft, unresponsive  
**After WO-217:** Attacks feel snappy, have rhythm  
**After WO-218:** Can attack while moving, feels fluid  
**After WO-219:** Attacks feel heavy, satisfying, impactful  
**After WO-220:** Combat feels polished, cohesive, complete

---

## Quality Gate

After all 4 phases:
- Load WebGL
- Go to battle
- Attack enemies
- Does it feel like a **good action game**? (Satisfying, responsive, punchy)
- If yes: **combat feel is FIXED**
- If no: iterate on timing/intensity values

---

## Notes

- **Don't skip phases.** They build on each other.
- **WO-217 is foundational.** Bad animation timing ruins everything after it.
- **WO-219 is the multiplier.** Good animations + visual feedback = 10x improvement.
- **All values should be configurable** (sliders, not hardcoded) so you can tweak feel without recompiling.

---

**CLI Recommendation:**

```
1. Claim WO-217, implement, test animation feel
2. Claim WO-218, implement, test movement + attack
3. Claim WO-219, implement all 5 feedback systems, test punchy feel
4. (Optional) Claim WO-220 when written, add audio
5. Retest full game — combat should feel GOOD
```

---

**Checked in:** 2026-06-01
