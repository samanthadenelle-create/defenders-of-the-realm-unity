# Animation Requirements — Dev Team Notice

**FROM:** Samantha  
**TO:** Dev Team  
**DATE:** 2026-06-08  
**PRIORITY:** HIGH

---

## Overview

16 work orders have been created with animation dependencies. **Please audit the required animations and flag any that are missing.** I will source them if needed.

---

## Animation Requirements by Work Order

### WO-360: Companion & Echo Outpost
- [ ] Companion walk-in animation (3–5s)
- [ ] Echo summon effect animation (particle + sound)
- [ ] Companion idle/stand animation

### WO-364: Companion Gear Setup
- [ ] Companion walk to forge animation
- [ ] Companion interaction with NPC (dialogue pose)
- [ ] Hero equip animation (armor swap, weapon draw)

### WO-365: Character Idle Poses
- [ ] Idle breathing loop (relaxed stance)
- [ ] Combat ready stance (crouched, alert)
- [ ] Smooth transition blend (0.3s) between idle/combat

### WO-366: Idle Routines
- [ ] **Sit** — Sit down animation (1.5s setup) + sitting idle loop (8.5s)
- [ ] **Play Dead** — Lie down dramatically (2s) + lying idle (3s) + stand up
- [ ] **Stretch** — Stretch arms animation (1s) + yawn animation (2s)
- [ ] **Fidget** — Shift weight loop (4s repeatable)

### WO-362: Enemy Wave Composition
- [ ] Enemy formation animations (no new anims, use existing)
- [ ] **Note:** Positioning logic, not animation-dependent

### WO-359: Combat Feedback System
- [ ] Screen shake (VFX/camera, not animation)
- [ ] Parry slowmo VFX (not animation)
- [ ] **Note:** Primarily VFX-driven, minimal animation needs

### Other WOs (352–358, 361, 363)
- [ ] No new animation requirements (UI/dialogue/systems work)

---

## Animation Status Checklist

**Please fill in:**

### WO-365: Character Idle Poses
- [ ] `Idle` breathing animation — **EXISTS / MISSING**
- [ ] `Combat` ready stance animation — **EXISTS / MISSING**

### WO-366: Idle Routines
- [ ] `Sit` down + idle animation — **EXISTS / MISSING**
- [ ] `PlayDead` lie down animation — **EXISTS / MISSING**
- [ ] `Stretch` arms + yawn animation — **EXISTS / MISSING**
- [ ] `Fidget` weight shift loop — **EXISTS / MISSING**

### WO-360 / WO-364: Companion & Gear
- [ ] Companion walk animation — **EXISTS / MISSING**
- [ ] Companion NPC interaction pose — **EXISTS / MISSING**
- [ ] Hero equip/armor swap animation — **EXISTS / MISSING**
- [ ] Echo summon effect — **EXISTS / MISSING**

---

## Action Required

**IF animations are missing:**
1. List which ones in a reply to this notice (just names, keep it short)
2. I will source them (commission, asset packs, etc.)
3. You integrate them into Animator when ready

**IF all animations exist:**
1. Confirm in a reply: "All animations found, ready to integrate"
2. Proceed with WO implementation

**Timeline:** Flag by **EOD 2026-06-08** so we don't block implementation.

---

## Notes

- Animations don't need to be perfect, just functional (polish later)
- Idle routines especially: sit/play dead/stretch need to feel cute, not stiff
- Combat pose should feel ready but not aggressive (player building, not battling)
- Companion animations can be simple (walk, stand, interact)

---

**Questions?** Ping me before starting implementation. Animations are the blocker here.
