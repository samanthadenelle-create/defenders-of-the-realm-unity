> **SOURCE: Grok execution package 2026-07-12** (owner-relayed, built from the docs/SME dossier fleet). Slotted into the WO numbering by CLI; reconcile against docs/SME/WO677_PHASE0_APPLICABILITY.md (the code-verified assessment).

# 🛠️ Work Order: Wire Remaining Sword & Shield Clips (Backlog Item #4)

**Priority:** P1  
**Effort:** Medium  
**Impact:** High — free combat depth

---

## Goal
We currently only use ~9 of the 45 Sword & Shield mocap clips. Wire the remaining high-value ones into the Motion Caster / Action system so they become playable.

## High-Value Unused Clips (from assessment)

### Must-wire first (highest combat value)
- Full directional shield hold-blocks (6 clips)
- Full sword parry set (especially the 4-beat backpedaling parry chain)
- `atk_shieldcharge`
- `atk_jump` (Heroic Leap — you originally wanted this as skill1)
- `atk_kick`
- Shield swipe combo (`shieldswipe01` → `shieldswipe02`)

### Also valuable
- All 8 strafe / backward locomotion clips (for proper 2-D directional blend tree)
- Remaining defensive and attack variants

## Current State
- skill1 / skill2 are currently unarmed kung-fu kicks from a different set.
- Only beat 1 of the shield bash combo is used.

---

## Tasks for Claude

1. **Catalog every remaining clip** in the Motion Caster with clean keywords:
   - `Shield_Block_Left`, `Shield_Block_Right`, `Shield_Block_Up`, etc.
   - `Sword_Parry_Back_Chain` (or individual beats)
   - `Shield_Charge`
   - `Heroic_Leap` / `Jump_Attack`
   - `Kick`
   - `Shield_Swipe_Combo`

2. **Update Action rows** so each new clip can also carry:
   - `vfxKey` (if any impact VFX)
   - `sfxId`
   - `vfxDelay`
   - `attachBone`

3. **Wire into the combat system**:
   - Map skill1 → `Heroic_Leap` (atk_jump)
   - Map skill2 → `Shield_Charge`
   - Add proper parry and dodge rows
   - Support the shield swipe combo (two-beat)

4. **Locomotion**  
   Add the 8 directional locomotion clips into the existing blend tree (or create a proper 2-D blend tree if we still only have forward).

5. **Validation**
   - Add a debug menu or console commands to force-play every new clip.
   - Confirm they play correctly on both the hero and any enemy that uses the shared humanoid rig.

---

## Deliverables
- Updated Motion Caster rows for all high-value clips
- skill1 / skill2 remapped
- Parry + charge + leap fully playable
- Short list of any clips that still need manual animation events

Keep the existing Action / keyword system intact. Just expand it.