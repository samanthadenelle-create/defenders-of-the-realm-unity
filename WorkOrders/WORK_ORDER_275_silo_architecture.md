# WO-275: Silo-Aligned Architecture & Folder Restructuring

**Status: READY TO IMPLEMENT**

**Date:** 2026-06-01  
**Priority:** 🔴 CRITICAL (foundation for all Phase 3 work)  
**Owner:** CLI  
**Time Estimate:** 14–15 hours  
**Unblocks:** WO-235, WO-236, WO-237, all Phase 3 feature work  
**Depends on:** WO-234, WO-196/211/215 (Phase 0 & 1 complete)

---

## Problem Statement

Current folder structure is chaotic:
- Files scattered across multiple locations with unclear ownership
- No clear boundaries between combat, world, economy, UI systems
- Team doesn't know who owns what → merge conflicts, duplicated code
- Hard to onboard new developers ("where does X go?")

**Solution:** Reorganize into 8 clear silos with dedicated folders, namespaces, and ownership rules.

---

## Target Silo Structure

```
Assets/Scripts/
├── Silo.Core/                    # Hub: GameManager, ServiceLocator, SaveSystem, Interfaces
├── Silo.Combat/                  # In-world 3D fights, ATB battles, damage systems
├── Silo.World/                   # Terrain, village buildings, enemy spawning, waves
├── Silo.Economy/                 # Currency, IAP, cosmetics, rewards
├── Silo.UI/                      # All UI screens, HUD, Store, Menus
├── Silo.Progression/             # Talents, leveling, pets, hero system
├── Silo.AudioVFX/                # Sound, particles, screen shake, ambient
└── Silo.Narrative/               # Story, dialogue, cutscenes, triggers

Assets/Prefabs/                   # Organized by silo (Prefabs/Combat/, Prefabs/World/, etc.)
Assets/ScriptableObjects/         # Organized by silo (SOData/Combat/, SOData/Economy/, etc.)
Assets/Scenes/
```

---

## Silo Definitions & Responsibilities

### Silo.Core (Namespace: DeNelle.Core.*)

**Owns:**
- GameManager.cs (DontDestroyOnLoad singleton)
- ServiceLocator pattern (CoreServices)
- Interface definitions (IDamageableStructure, IVillageHud, IAudioService, etc.)
- Enums (SfxId, MusicTrack, etc.)
- SaveSystem.cs
- SceneRouter.cs

**Rules:**
- Core → anyone (unidirectional dependency)
- No cross-silo imports (except Core)
- All service interfaces live here

---

### Silo.Combat (Namespace: DeNelle.Combat.*)

**Owns:**
- In-world 3D enemy fighting (EnemyController, EnemyCombatAnimator, EnemyAudio)
- WorldCombatManager (multi-enemy coordination)
- ATB battle system (BattleController, BattleHud, BattleState, ATBUnit)
- Damage, health, death logic (IDamageableStructure implementations)
- Hero attack/cast logic

**Depends on:** Core only  
**Key files:**
- EnemyController.cs
- EnemyCombatAnimator.cs
- EnemyAudio.cs
- WorldCombatManager.cs
- BattleController.cs
- ATBUnit.cs
- HeroLocomotion.cs (refactored)

---

### Silo.World (Namespace: DeNelle.World.*)

**Owns:**
- ExteriorTerrainBuilder (procedural world gen)
- VillageSceneBuilder (village layout)
- WaveManager (enemy spawn waves)
- NavMesh building
- Gate/Tower/Wall/Building placement
- Village grid system

**Depends on:** Core, Combat (for spawn points)  
**Key files:**
- ExteriorTerrainBuilder.cs
- VillageSceneBuilder.cs
- WaveManager.cs
- Building.cs, Tower.cs, Gate.cs, Wall.cs

---

### Silo.Economy (Namespace: DeNelle.Economy.*)

**Owns:**
- CurrencyService (Glimmer, Energy, Gems)
- Store logic (buying, pricing, inventory)
- IAP (In-App Purchase) integration
- Reward systems
- Cosmetics data (which skins are owned, equipped, etc.)

**Depends on:** Core, UI (for Store UI binding)  
**Key files:**
- CurrencyService.cs
- StoreManager.cs
- RewardSystem.cs
- CosmeticInventory.cs

---

### Silo.UI (Namespace: DeNelle.UI.*)

**Owns:**
- All UI Toolkit screens (Store, Talents, Hero Select, Settings, HUD)
- BattleHud.cs (visual rendering only)
- CosmeticShopUI.cs (improved version)
- VillageHudController.cs
- Menu navigation, transitions

**Depends on:** Core, Economy (for store data), Combat (for HUD binding)  
**Key files:**
- CosmeticShopUI.cs
- VillageHudController.cs
- BattleHud.cs (UI Toolkit renderer)
- TalentTreeUI.cs
- HeroSelectUI.cs

---

### Silo.Progression (Namespace: DeNelle.Progression.*)

**Owns:**
- HeroData, Talents, TalentTree
- Leveling system
- Pet system (acquisition, leveling, abilities)
- Hero selection (which hero is active)
- Progression save state

**Depends on:** Core, Economy (for cosmetics)  
**Key files:**
- HeroData.cs
- TalentTree.cs
- PetSystem.cs
- ProgressionService.cs

---

### Silo.AudioVFX (Namespace: DeNelle.AudioVFX.*)

**Owns:**
- AudioService.cs (spatial 3D sound)
- SfxClipLibrary.cs (clip registry)
- VFXManager.cs (particles, screen shake)
- MusicManager.cs
- Ambient sound systems

**Depends on:** Core only  
**Key files:**
- AudioService.cs
- SfxClipLibrary.cs
- VFXManager.cs
- CameraShaker.cs

---

### Silo.Narrative (Namespace: DeNelle.Narrative.*)

**Owns:**
- Story beats, dialogue trees
- Cutscene sequences
- Event triggers
- NPC dialogue logic
- Narrative state machine

**Depends on:** Core, UI (for dialogue UI)  
**Key files:**
- StoryManager.cs
- DialogueSystem.cs
- CutsceneController.cs

---

## Current State Assessment

Current files scattered across:
- Assets/_Modules/ (mixed organization)
- Assets/Scripts/ (some loose files)
- Root level prefabs/scenes

**Migration task:** Move all code into silo folders, update namespaces, fix cross-silo imports.

---

## Migration Steps (Phase by Phase)

### Phase 1: Create New Folder Structure (1 hour)
1. Create Assets/Scripts/ root
2. Create 8 silo folders (Silo.Core, Silo.Combat, etc.)
3. Create Assets/Prefabs/ with silo subfolders
4. Create Assets/ScriptableObjects/ with silo subfolders
5. Verify structure compiles (no compilation errors yet)

### Phase 2: Move Core Files (2 hours)
1. Move all Core interfaces, enums, services → Silo.Core/
2. Update namespaces to DeNelle.Core.*
3. Update all import statements
4. Test that GameManager starts without errors

### Phase 3: Move Combat System (2 hours)
1. Move EnemyController, BattleController, ATBUnit, etc. → Silo.Combat/
2. Update namespaces to DeNelle.Combat.*
3. Move combat prefabs → Assets/Prefabs/Combat/
4. Test that enemies spawn and ATB battle works

### Phase 4: Move World System (2.5 hours)
1. Move ExteriorTerrainBuilder, WaveManager, Building, Tower, etc. → Silo.World/
2. Update namespaces to DeNelle.World.*
3. Move world prefabs → Assets/Prefabs/World/
4. Test that village loads and waves spawn

### Phase 5: Move Economy System (1.5 hours)
1. Move CurrencyService, StoreManager, cosmetics → Silo.Economy/
2. Update namespaces to DeNelle.Economy.*
3. Move ScriptableObjects → Assets/ScriptableObjects/Economy/
4. Test that store opens (UI not yet updated)

### Phase 6: Move UI System (2 hours)
1. Move all UI scripts → Silo.UI/
2. Update namespaces to DeNelle.UI.*
3. Wire UI to Economy/Combat/World services
4. Test that all UI screens display

### Phase 7: Move Progression System (1 hour)
1. Move HeroData, TalentTree, PetSystem → Silo.Progression/
2. Update namespaces to DeNelle.Progression.*
3. Move ScriptableObjects → Assets/ScriptableObjects/Progression/
4. Test that hero select works

### Phase 8: Move AudioVFX System (1 hour)
1. Move AudioService, VFXManager → Silo.AudioVFX/
2. Update namespaces to DeNelle.AudioVFX.*
3. Move audio clips → Assets/Audio/ (organized by silo)
4. Test that sounds play in world

### Phase 9: Move Narrative System (1 hour)
1. Move StoryManager, DialogueSystem → Silo.Narrative/
2. Update namespaces to DeNelle.Narrative.*
3. Test that story triggers work

### Phase 10: Integration Testing (1.5 hours)
1. Full playthrough: spawn → fight → win
2. All menus work
3. Store accessible from HUD
4. Hero select works
5. No cross-silo import errors
6. Console clean (no namespace warnings)

### Phase 11: Cleanup & Documentation (0.5 hours)
1. Remove old folder paths
2. Update CLAUDE.md with new folder structure
3. Commit with message: "WO-275: Restructure to silo-aligned architecture"

---

## Key Rules for Future Work

**Rule 1: Unidirectional Dependencies**
```
Core ← anyone (ok)
Combat → Core only (ok)
Combat → World (NOT OK)
World → Combat (NOT OK)
```

**Rule 2: Cross-Silo Communication**
- Always go through Core (GameManager, ServiceLocator)
- Example: Combat needs to update UI → Call CoreServices.Hud, not direct import

**Rule 3: Namespace Consistency**
- File in Silo.Combat/ MUST be in DeNelle.Combat.* namespace
- File in Silo.World/ MUST be in DeNelle.World.* namespace
- Violating this = auto-reject on code review

**Rule 4: Prefab Organization**
- Enemy prefab → Assets/Prefabs/Combat/
- Building prefab → Assets/Prefabs/World/
- Store UI prefab → Assets/Prefabs/UI/

---

## Assembly Definitions (Optional but Recommended)

Create Assembly Definition Files for each silo:
```
Assets/Scripts/Silo.Core/Silo.Core.asmdef
  - References: none (foundation)

Assets/Scripts/Silo.Combat/Silo.Combat.asmdef
  - References: Silo.Core

Assets/Scripts/Silo.World/Silo.World.asmdef
  - References: Silo.Core

Assets/Scripts/Silo.UI/Silo.UI.asmdef
  - References: Silo.Core, Silo.Combat, Silo.World, Silo.Economy

(etc.)
```

Benefits: Faster compilation, forces dependency rules, prevents circular imports.

---

## Acceptance Criteria

- [ ] All 8 silo folders created with correct structure
- [ ] All code moved to correct silo folders
- [ ] All namespaces updated (no DeNelle.Village.* mixed with DeNelle.Combat.*)
- [ ] All cross-silo imports fixed (only go through Core)
- [ ] Project compiles with zero errors
- [ ] No "missing reference" errors in any scene
- [ ] Full playthrough test: spawn → fight → menu → store → hero select → win
- [ ] Console is clean (no namespace warnings)
- [ ] Brace balance check passes on all modified files (CLAUDE.md rule)
- [ ] CLAUDE.md updated with new folder structure
- [ ] Commit message: "WO-275: Restructure to silo-aligned architecture"

---

## Testing Checklist (After Migration Complete)

After all phases complete, verify:

```
[Scene: Village]
✓ Terrain builds
✓ Buildings place correctly
✓ Hero can move (WASD)
✓ Hero can attack enemies
✓ Enemies spawn in waves
✓ Waves progress correctly
✓ Village HUD displays (resources, health)
✓ Can click to build towers
✓ Store opens from HUD
✓ Can buy cosmetics

[Scene: ATBBattle]
✓ Battle loads
✓ HUD displays (title, enemies, party, log, command bar)
✓ ATB bars fill over time
✓ Can select Attack/Skills/Item/Defend
✓ Actions execute
✓ Battle resolves correctly
✓ Victory/Defeat screens show

[General]
✓ No errors in console
✓ No missing references
✓ No namespace conflicts
✓ All cross-silo calls go through Core services
```

---

## Risk Mitigation

**Risk 1: Breaking existing code during migration**
- Mitigation: Create new folders first, move incrementally, test after each phase
- Backup: Keep old folders temporarily until 100% validated

**Risk 2: Namespace conflicts**
- Mitigation: Use find-and-replace carefully, test compilation frequently
- Backup: Have a known-good backup before starting

**Risk 3: Missing cross-silo wiring**
- Mitigation: Use GameManager.cs as the validation point — all cross-silo calls must go through it
- Backup: Add debug logs to ServiceLocator to show all wiring attempts

---

## Timeline

**14–15 hours total:**
- Phase 1: 1 hr
- Phase 2: 2 hr
- Phase 3: 2 hr
- Phase 4: 2.5 hr
- Phase 5: 1.5 hr
- Phase 6: 2 hr
- Phase 7: 1 hr
- Phase 8: 1 hr
- Phase 9: 1 hr
- Phase 10: 1.5 hr
- Phase 11: 0.5 hr

**Total: 15 hours (conservative estimate)**

---

## Commit Message

`"WO-275: Restructure to silo-aligned architecture with clear ownership boundaries"`

---

## What Comes Next (Phase 3)

After WO-275 completes, all new work goes into silos:
- **WO-235:** In-world Combat Core (Silo.Combat/)
- **WO-236:** Cosmetic Store UI (Silo.UI/ + Silo.Economy/)
- **WO-237:** Hero Movement Refactor (Silo.Combat/)
- All Phase 3 work organized by silo

Each silo owner knows exactly where their code lives and who to coordinate with.

---

**This is the foundation for scaling. Do it right now, save hours later.**
