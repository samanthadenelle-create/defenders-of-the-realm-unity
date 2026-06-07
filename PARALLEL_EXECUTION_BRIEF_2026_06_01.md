# Parallel Execution Brief — Option 1 (Silo-First Approach)
**Date:** 2026-06-01  
**Strategy:** Create Silo folders NOW. Three people work simultaneously on WO-234/235/236.

---

## Folder Structure to Create (Right Now)

```
Assets/Scripts/
├── Silo.Combat/
│   ├── Hero/
│   │   ├── HeroLocomotion.cs (NEW — from WO-235)
│   │   └── HeroAnimator.cs (NEW — from WO-235)
│   └── (other combat files will be added)
│
├── Silo.UI/
│   ├── Store/
│   │   └── CosmeticShopUI.cs (NEW — from WO-236)
│   └── (other UI files will be added)
│
└── (existing scripts folder — keep as-is for now)
```

**Action:** Create these two folders + subfolders in Assets/Scripts/. They're empty except for the WO-235/236 files we're adding.

---

## Parallel Tracks (Start Now)

### Track 1: Person A — WO-234 (ATB Hot-Fix)
**Time:** 1–2 hours  
**Owner:** CLI  
**Status:** ✅ READY

**What to do:**
1. Create `Assets/_Modules/BattleATB/BattleVfx.cs` (copy from WO-234_CLI_READY_SPEC.md)
2. Update `BattleController.BindUi()` (replace method per spec)
3. Update `BattleHud.Render()` (replace method per spec)
4. Add logging to `RenderCommandBar()` (per spec)
5. Run brace checks on all 4 files
6. Test: Play ATBBattle.unity, verify HUD appears + bars animate

**Deliverable:** Console shows no errors, HUD visible, bars animate, interactive

---

### Track 2: Person B–C — WO-235 (In-World Combat Core)
**Time:** 2–3 hours  
**Owner:** CLI  
**Status:** ✅ READY (namespaces already updated to `DeNelle.Combat`)

**What to do:**
1. Create folder structure: `Assets/Scripts/Silo.Combat/Hero/`
2. Add `HeroLocomotion.cs` (use code from WORK_ORDER_235_inworld_combat_core.md, section 5)
3. Add `HeroAnimator.cs` (use code from WORK_ORDER_235_inworld_combat_core.md, section 6)
4. Update Hero prefab:
   - Add `HeroLocomotion` component
   - Add `HeroAnimator` component
   - Drag child Animator into `heroAnimator` field on HeroLocomotion
5. Verify Animator has Speed (float), InCombat (bool), Attack/Hit/Death (triggers)
6. Run brace checks on both files
7. Test: Hero moves smoothly with WASD + gamepad

**Deliverable:** Hero moves responsive, rotates smooth, no jank

---

### Track 3: Person D — WO-236 (Cosmetic Store UI)
**Time:** 1–2 hours  
**Owner:** CLI  
**Status:** ✅ READY (namespace already updated to `DeNelle.UI`)

**What to do:**
1. Create folder structure: `Assets/Scripts/Silo.UI/Store/`
2. Add `CosmeticShopUI.cs` (use code from WORK_ORDER_236_cosmetic_store_ui.md)
3. Create UIDocument + PanelSettings in Village scene
4. Add CosmeticShopUI component to canvas/panel
5. Link UIDocument field
6. Run brace check on CosmeticShopUI.cs
7. Test: Store UI displays with cards, tabs, hover effects

**Deliverable:** Store UI shows premium cards, tabs switchable, hover works

---

## What This Enables After All Three Complete

✅ **Phase 0 validated** (WO-234): ATB architecture confirmed working  
✅ **Combat foundation** (WO-235): Hero movement + animation ready for polish  
✅ **Shop UI** (WO-236): Store skeleton ready for economy system  

**Then:** Phase 1 (WO-196/211/215) → WO-232 silo restructure → Phase 3 features in parallel

---

## Key Notes

- **No cross-silo dependencies:** All three work streams are independent (different script files, different game systems)
- **Silo folders are permanent:** Unlike the old approach, these folders stay after WO-232 (no refactoring)
- **Namespaces are finalized:** Code written for `DeNelle.Combat` and `DeNelle.UI` — when WO-232 runs, these folders ARE the final structure
- **No merge conflicts expected:** Each person edits different files in different folders

---

## Commit Messages (After Each Completes)

**Track 1:**
```
WO-234: add BattleVfx stub + debug logging to ATB system — fixes silent failures in HUD binding
```

**Track 2:**
```
WO-235: hero movement refactor — smooth locomotion + responsive animation bridge
```

**Track 3:**
```
WO-236: cosmetic store UI — premium cards, tabs, hover effects
```

---

## Success Criteria (Parallel Phase)

- [ ] WO-234: Console shows ✓ logs, HUD visible, no errors
- [ ] WO-235: Hero moves smoothly (WASD/gamepad), rotates responsive, animator wired
- [ ] WO-236: Store displays, cards styled (dark purple/gold), tabs work, hover effects smooth
- [ ] All brace checks pass
- [ ] Zero errors/warnings in console (across all 3)
- [ ] Ready for Phase 1 gate after all complete

---

**Ready to execute. No blockers. Three people, three tracks, all in parallel.**
