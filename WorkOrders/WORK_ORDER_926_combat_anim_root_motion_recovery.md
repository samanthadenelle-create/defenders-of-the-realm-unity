# WORK ORDER 926 — Combat animation: legs/hips, foot slide, recovery, shield clip

**Status: SPEC / READY FOR OWNER PRIORITY**  
**Minted:** 2026-08-07 (Grok Imagine visual review — Development Build)  
**Silo:** Animation / Hero combat  
**Roles:** CLI implement after owner picks priority vs dungeon WOs; may need anim clip retarget  
**Related:** HeroLocomotion, attack controllers, CC5 / class FBX pipeline  

---

## 0. One-line truth

Imagine review: upper-body swings + VFX look OK, but **body is rigid**, legs/hips barely contribute, **foot sliding**, **shield/sword clip**, and **long static recovery** before locomotion resumes. Feels glued to the floor.

---

## 1. Symptoms (from review — felt)

1. Attack = upper body rotate/swing; legs planted.  
2. Foot slide in move + combat.  
3. Back shield + kite shield clip body/sword mid-swing.  
4. Recovery holds end pose too long before move blend.

---

## 2. Investigation order (instrument / measure before rewriting)

1. Which animator controller + attack state machine for Grom/Knight.  
2. Root motion: on/off on clips; does locomotion apply root motion in combat?  
3. Upper-body layer mask vs full-body attacks.  
4. Avatar mask / IK (feet).  
5. Weapon/shield attach bones vs clip curves.  
6. Exit time / transition duration from Attack → Locomotion.

---

## 3. Scope (V1 — high leverage)

| Item | Direction |
|------|-----------|
| Attack → loco | Shorten exit time / add early transition on release |
| Legs | Prefer full-body attack clips or add lower-body attack layer; avoid pure upper mask if legs idle |
| Foot slide | Match animator speed to loco velocity; root motion or stop root conflict |
| Shield clip | Adjust bind pose / socket offset; optional hide back shield during heavy swing |
| VFX | Keep sword trails; do not “fix” feel by more particles |

### Out of scope V1

- Full new mocap set (unless owner commissions).  
- Dungeon geometry (WO-923/924).  

---

## 4. Acceptance (felt — PO closes)

- [ ] Swings show hip/weight shift, not T-pose torso on frozen legs.  
- [ ] Noticeably less foot slide in combat.  
- [ ] Recovery returns to walk within ~0.15–0.25 s of clip end (tune with PO).  
- [ ] Shield does not clip through torso on primary combo.  
- [ ] Capture 5 s combat before/after.  

## RESULT

`WorkOrders/WORK_ORDER_926_combat_anim_root_motion_recovery.RESULT.md`
