# Silo File Migration Map — Complete Reference

**Purpose:** Exact list of where every file goes in the new silo structure.

**Format:**
```
Current Path → New Path | New Namespace | Status
```

---

## SILO.CORE — The Hub (Foundation for Everything)

**Purpose:** Interfaces, enums, services, GameManager, constants. **Core → everyone** (unidirectional).

### Core Files (Create or Move)

| Current | New | Namespace | Status |
|---------|-----|-----------|--------|
| Assets/_Modules/Core/Managers/GameManager.cs | Assets/Scripts/Silo.Core/Managers/GameManager.cs | DeNelle.Core.Managers | Move |
| Assets/_Modules/Core/Services/ServiceLocator.cs | Assets/Scripts/Silo.Core/Services/CoreServices.cs | DeNelle.Core.Services | Move + Rename |
| Assets/_Modules/Core/Combat/IDamageableStructure.cs | Assets/Scripts/Silo.Core/Interfaces/IDamageableStructure.cs | DeNelle.Core.Interfaces | Move |
| Assets/_Modules/Core/HUD/IVillageHud.cs | Assets/Scripts/Silo.Core/Interfaces/IVillageHud.cs | DeNelle.Core.Interfaces | Move |
| Assets/_Modules/Core/Audio/IAudioService.cs | Assets/Scripts/Silo.Core/Interfaces/IAudioService.cs | DeNelle.Core.Interfaces | Move |
| Assets/_Modules/Core/Combat/CombatEnums.cs | Assets/Scripts/Silo.Core/Enums/CombatEnums.cs | DeNelle.Core.Enums | Move |
| Assets/_Modules/Core/Audio/SfxId.cs | Assets/Scripts/Silo.Core/Enums/SfxId.cs | DeNelle.Core.Enums | Move |
| Assets/_Modules/Core/Audio/MusicTrack.cs | Assets/Scripts/Silo.Core/Enums/MusicTrack.cs | DeNelle.Core.Enums | Move |
| Assets/_Modules/Core/SaveSystem/SaveSystem.cs | Assets/Scripts/Silo.Core/Save/SaveSystem.cs | DeNelle.Core.Save | Move |
| Assets/Scripts/SceneRouter.cs | Assets/Scripts/Silo.Core/Utils/SceneRouter.cs | DeNelle.Core.Utils | Move |
| Assets/Scripts/Constants.cs | Assets/Scripts/Silo.Core/Utils/Constants.cs | DeNelle.Core.Utils | Move (if exists) |
| — | Assets/Scripts/Silo.Core/Utils/BraceValidator.cs | DeNelle.Core.Utils | Create (if needed) |

### Core ScriptableObjects

| Current | New | Folder |
|---------|-----|--------|
| Assets/_Modules/Core/SaveData/ | Assets/ScriptableObjects/Silo.Core/SaveData/ | Move |

---

## SILO.COMBAT — Fighting Systems

**Purpose:** In-world 3D combat, ATB battles, hero animation, damage logic.

### Combat Scripts

| Current | New | Namespace | Status |
|---------|-----|-----------|--------|
| Assets/_Modules/BattleATB/BattleController.cs | Assets/Scripts/Silo.Combat/Battle/BattleController.cs | DeNelle.Combat.Battle | Move |
| Assets/_Modules/BattleATB/BattleHud.cs | Assets/Scripts/Silo.Combat/Battle/BattleHud.cs | DeNelle.Combat.Battle | Move |
| Assets/_Modules/BattleATB/BattleState.cs | Assets/Scripts/Silo.Combat/Battle/BattleState.cs | DeNelle.Combat.Battle | Move |
| Assets/_Modules/BattleATB/ATBUnit.cs | Assets/Scripts/Silo.Combat/Battle/ATBUnit.cs | DeNelle.Combat.Battle | Move |
| Assets/_Modules/BattleATB/BattleVfx.cs | Assets/Scripts/Silo.Combat/Battle/BattleVfx.cs | DeNelle.Combat.Battle | Create (WO-234) |
| Assets/_Modules/BattleATB/ATBCombatManager.cs | Assets/Scripts/Silo.Combat/Battle/ATBCombatManager.cs | DeNelle.Combat.Battle | Move |
| Assets/_Modules/BattleATB/Generated/ATBRuntimeState.cs | Assets/Scripts/Silo.Combat/Battle/ATBRuntimeState.cs | DeNelle.Combat.Battle | Move |
| — | Assets/Scripts/Silo.Combat/Enemy/EnemyController.cs | DeNelle.Combat.Enemy | Create (WO-235) |
| — | Assets/Scripts/Silo.Combat/Enemy/EnemyCombatAnimator.cs | DeNelle.Combat.Enemy | Create (WO-235) |
| — | Assets/Scripts/Silo.Combat/Enemy/EnemyAudio.cs | DeNelle.Combat.Enemy | Create (WO-235) |
| — | Assets/Scripts/Silo.Combat/Enemy/WorldCombatManager.cs | DeNelle.Combat.Enemy | Create (WO-235) |
| Assets/Scripts/HeroLocomotion.cs (current) | Assets/Scripts/Silo.Combat/Hero/HeroLocomotion.cs | DeNelle.Combat.Hero | Move + Refactor (WO-235) |
| — | Assets/Scripts/Silo.Combat/Hero/HeroAnimator.cs | DeNelle.Combat.Hero | Create (WO-235) |
| Assets/_Modules/Village/HeroHealth.cs | Assets/Scripts/Silo.Combat/Health/HeroHealth.cs | DeNelle.Combat.Health | Move |
| Assets/_Modules/Village/HeartController.cs | Assets/Scripts/Silo.Combat/Health/HeartController.cs | DeNelle.Combat.Health | Move |

### Combat Prefabs

| Current | New |
|---------|-----|
| Assets/Prefabs/Enemy* | Assets/Prefabs/Silo.Combat/Enemy/ |
| Assets/Prefabs/Hero* | Assets/Prefabs/Silo.Combat/Hero/ |
| Assets/Prefabs/Battle* | Assets/Prefabs/Silo.Combat/Battle/ |

### Combat ScriptableObjects

| Current | New |
|---------|-----|
| Assets/ScriptableObjects/EnemyData/ | Assets/ScriptableObjects/Silo.Combat/Enemy/ |
| Assets/ScriptableObjects/WaveData/ | Assets/ScriptableObjects/Silo.Combat/Waves/ |

---

## SILO.WORLD — Environment & Spawning

**Purpose:** Terrain generation, village layout, enemy waves, buildings.

### World Scripts

| Current | New | Namespace | Status |
|---------|-----|-----------|--------|
| Assets/_Modules/Editor/VillageSceneBuilder.cs | Assets/Scripts/Silo.World/Building/VillageSceneBuilder.cs | DeNelle.World.Building | Move |
| Assets/_Modules/World/ExteriorTerrainBuilder.cs | Assets/Scripts/Silo.World/Terrain/ExteriorTerrainBuilder.cs | DeNelle.World.Terrain | Move |
| Assets/_Modules/World/TerrainConfig.cs | Assets/Scripts/Silo.World/Terrain/TerrainConfig.cs | DeNelle.World.Terrain | Move |
| Assets/_Modules/Village/WaveManager.cs | Assets/Scripts/Silo.World/Waves/WaveManager.cs | DeNelle.World.Waves | Move |
| Assets/_Modules/Village/Building.cs | Assets/Scripts/Silo.World/Building/Building.cs | DeNelle.World.Building | Move |
| Assets/_Modules/Village/Tower.cs | Assets/Scripts/Silo.World/Building/Tower.cs | DeNelle.World.Building | Move |
| Assets/_Modules/Village/Gate.cs | Assets/Scripts/Silo.World/Building/Gate.cs | DeNelle.World.Building | Move |
| Assets/_Modules/Village/WallSegment.cs | Assets/Scripts/Silo.World/Building/WallSegment.cs | DeNelle.World.Building | Move |
| Assets/_Modules/Village/NavigationGrid.cs | Assets/Scripts/Silo.World/Navigation/NavigationGrid.cs | DeNelle.World.Navigation | Move |
| Assets/_Modules/Village/SpawnPoint.cs | Assets/Scripts/Silo.World/Waves/SpawnPoint.cs | DeNelle.World.Waves | Move |

### World Prefabs

| Current | New |
|---------|-----|
| Assets/Prefabs/Building* | Assets/Prefabs/Silo.World/Building/ |
| Assets/Prefabs/Tower* | Assets/Prefabs/Silo.World/Building/ |
| Assets/Prefabs/Gate* | Assets/Prefabs/Silo.World/Building/ |
| Assets/Prefabs/Wall* | Assets/Prefabs/Silo.World/Building/ |

### World ScriptableObjects

| Current | New |
|---------|-----|
| Assets/ScriptableObjects/TowerData/ | Assets/ScriptableObjects/Silo.World/Building/ |
| Assets/ScriptableObjects/BuildingData/ | Assets/ScriptableObjects/Silo.World/Building/ |

---

## SILO.ECONOMY — Store & Currency

**Purpose:** In-game store, cosmetics, currency systems, IAP.

### Economy Scripts

| Current | New | Namespace | Status |
|---------|-----|-----------|--------|
| — | Assets/Scripts/Silo.Economy/Store/CosmeticShopUI.cs | DeNelle.Economy.Store | Create (WO-236) |
| — | Assets/Scripts/Silo.Economy/Currency/CurrencyService.cs | DeNelle.Economy.Currency | Create |
| — | Assets/Scripts/Silo.Economy/Store/StoreManager.cs | DeNelle.Economy.Store | Create |
| — | Assets/Scripts/Silo.Economy/Cosmetics/CosmeticInventory.cs | DeNelle.Economy.Cosmetics | Create |
| — | Assets/Scripts/Silo.Economy/Rewards/RewardSystem.cs | DeNelle.Economy.Rewards | Create |
| — | Assets/Scripts/Silo.Economy/IAP/IAPManager.cs | DeNelle.Economy.IAP | Create |

### Economy ScriptableObjects

| Current | New |
|---------|-----|
| — | Assets/ScriptableObjects/Silo.Economy/Store/CosmeticData.asset |
| — | Assets/ScriptableObjects/Silo.Economy/Currency/CurrencyTypes.asset |
| — | Assets/ScriptableObjects/Silo.Economy/Rewards/RewardTable.asset |

---

## SILO.UI — All User Interface

**Purpose:** All UI screens, HUD, menus, Store UI, Battle UI.

### UI Scripts

| Current | New | Namespace | Status |
|---------|-----|-----------|--------|
| Assets/_Modules/HUD/VillageHudController.cs | Assets/Scripts/Silo.UI/HUD/VillageHudController.cs | DeNelle.UI.HUD | Move |
| Assets/_Modules/HUD/ResourceDisplay.cs | Assets/Scripts/Silo.UI/HUD/ResourceDisplay.cs | DeNelle.UI.HUD | Move |
| — | Assets/Scripts/Silo.UI/Store/CosmeticShopUI.cs | DeNelle.UI.Store | Create (WO-236) |
| — | Assets/Scripts/Silo.UI/Battle/BattleHudRenderer.cs | DeNelle.UI.Battle | Create (if separating BattleHud) |
| — | Assets/Scripts/Silo.UI/Talents/TalentTreeUI.cs | DeNelle.UI.Talents | Create |
| — | Assets/Scripts/Silo.UI/HeroSelect/HeroSelectUI.cs | DeNelle.UI.HeroSelect | Create |
| — | Assets/Scripts/Silo.UI/Menus/MainMenu.cs | DeNelle.UI.Menus | Create |
| — | Assets/Scripts/Silo.UI/Menus/SettingsMenu.cs | DeNelle.UI.Menus | Create |

### UI Prefabs

| Current | New |
|---------|-----|
| Assets/Prefabs/UI/ | Assets/Prefabs/Silo.UI/ |

---

## SILO.PROGRESSION — Hero & Pet Systems

**Purpose:** Heroes, talents, leveling, pet system.

### Progression Scripts

| Current | New | Namespace | Status |
|---------|-----|-----------|--------|
| — | Assets/Scripts/Silo.Progression/Hero/HeroData.cs | DeNelle.Progression.Hero | Create |
| — | Assets/Scripts/Silo.Progression/Talents/TalentTree.cs | DeNelle.Progression.Talents | Create |
| — | Assets/Scripts/Silo.Progression/Talents/TalentNode.cs | DeNelle.Progression.Talents | Create |
| — | Assets/Scripts/Silo.Progression/Pet/PetSystem.cs | DeNelle.Progression.Pet | Create |
| — | Assets/Scripts/Silo.Progression/Pet/PetData.cs | DeNelle.Progression.Pet | Create |
| — | Assets/Scripts/Silo.Progression/Leveling/LevelingSystem.cs | DeNelle.Progression.Leveling | Create |
| — | Assets/Scripts/Silo.Progression/ProgressionService.cs | DeNelle.Progression | Create |

### Progression ScriptableObjects

| Current | New |
|---------|-----|
| — | Assets/ScriptableObjects/Silo.Progression/Hero/ |
| — | Assets/ScriptableObjects/Silo.Progression/Talents/ |
| — | Assets/ScriptableObjects/Silo.Progression/Pet/ |

---

## SILO.AUDIOVFX — Sound & Effects

**Purpose:** Audio, music, particles, camera shake, VFX.

### AudioVFX Scripts

| Current | New | Namespace | Status |
|---------|-----|-----------|--------|
| Assets/_Modules/Audio/AudioService.cs | Assets/Scripts/Silo.AudioVFX/Audio/AudioService.cs | DeNelle.AudioVFX.Audio | Move |
| Assets/_Modules/Audio/SfxClipLibrary.cs | Assets/Scripts/Silo.AudioVFX/Audio/SfxClipLibrary.cs | DeNelle.AudioVFX.Audio | Move |
| Assets/_Modules/Audio/MusicManager.cs | Assets/Scripts/Silo.AudioVFX/Audio/MusicManager.cs | DeNelle.AudioVFX.Audio | Move |
| — | Assets/Scripts/Silo.AudioVFX/VFX/VFXManager.cs | DeNelle.AudioVFX.VFX | Create |
| — | Assets/Scripts/Silo.AudioVFX/VFX/CameraShaker.cs | DeNelle.AudioVFX.VFX | Create |
| — | Assets/Scripts/Silo.AudioVFX/VFX/ParticleController.cs | DeNelle.AudioVFX.VFX | Create |

### AudioVFX Folders

| Current | New |
|---------|-----|
| Assets/Audio/ | Assets/Audio/Silo.AudioVFX/ |
| Assets/VFX/ | Assets/VFX/Silo.AudioVFX/ |

---

## SILO.NARRATIVE — Story & Dialogue

**Purpose:** Story progression, dialogue trees, cutscenes, events.

### Narrative Scripts

| Current | New | Namespace | Status |
|---------|-----|-----------|--------|
| — | Assets/Scripts/Silo.Narrative/Story/StoryManager.cs | DeNelle.Narrative.Story | Create |
| — | Assets/Scripts/Silo.Narrative/Dialogue/DialogueSystem.cs | DeNelle.Narrative.Dialogue | Create |
| — | Assets/Scripts/Silo.Narrative/Dialogue/DialogueNode.cs | DeNelle.Narrative.Dialogue | Create |
| — | Assets/Scripts/Silo.Narrative/Cutscenes/CutsceneController.cs | DeNelle.Narrative.Cutscenes | Create |
| — | Assets/Scripts/Silo.Narrative/Events/EventTrigger.cs | DeNelle.Narrative.Events | Create |

### Narrative ScriptableObjects

| Current | New |
|---------|-----|
| — | Assets/ScriptableObjects/Silo.Narrative/Story/ |
| — | Assets/ScriptableObjects/Silo.Narrative/Dialogue/ |

---

## Editor-Only (Silo.Editor)

**Purpose:** Scene builders, animator setup, tools (editor-only, not in runtime).

### Editor Scripts

| Current | New | Namespace | Status |
|---------|-----|-----------|--------|
| Assets/_Modules/Editor/VillageSceneBuilder.cs | Assets/Scripts/Silo.Editor/VillageSceneBuilder.cs | DeNelle.Editor | Move |
| Assets/_Modules/Editor/AnimatorSetup.cs | Assets/Scripts/Silo.Editor/AnimatorSetup.cs | DeNelle.Editor | Move (if exists) |

**Note:** These are editor-only. Wrap with `#if UNITY_EDITOR` if needed.

---

## Summary: Total File Count

| Silo | Scripts | Move | Create |
|------|---------|------|--------|
| Core | 8 | 7 | 1 |
| Combat | 14 | 8 | 6 |
| World | 10 | 9 | 1 |
| Economy | 6 | 0 | 6 |
| UI | 8 | 2 | 6 |
| Progression | 7 | 0 | 7 |
| AudioVFX | 7 | 4 | 3 |
| Narrative | 5 | 0 | 5 |
| **TOTAL** | **65** | **30** | **35** |

**Breakdown:**
- **30 files to move** (existing code)
- **35 files to create** (new systems)
- **Total: 65 scripts** when complete

---

## Migration Checklist (By Phase)

### Phase 1: Create Folder Structure
```
[ ] Assets/Scripts/Silo.Core/
[ ] Assets/Scripts/Silo.Combat/
[ ] Assets/Scripts/Silo.World/
[ ] Assets/Scripts/Silo.Economy/
[ ] Assets/Scripts/Silo.UI/
[ ] Assets/Scripts/Silo.Progression/
[ ] Assets/Scripts/Silo.AudioVFX/
[ ] Assets/Scripts/Silo.Narrative/
[ ] Assets/Scripts/Silo.Editor/
[ ] Assets/Prefabs/Silo.* (subdirs)
[ ] Assets/ScriptableObjects/Silo.* (subdirs)
```

### Phase 2: Move Core Files
```
[ ] GameManager.cs → Silo.Core/Managers/
[ ] ServiceLocator.cs → Silo.Core/Services/ (rename to CoreServices)
[ ] All interfaces → Silo.Core/Interfaces/
[ ] All enums → Silo.Core/Enums/
[ ] SaveSystem.cs → Silo.Core/Save/
[ ] SceneRouter.cs → Silo.Core/Utils/
[ ] Update all imports in other files
```

### Phase 3: Move Combat Files
```
[ ] Move all BattleATB/ files → Silo.Combat/Battle/
[ ] Move HeroLocomotion → Silo.Combat/Hero/
[ ] Move HeroHealth, HeartController → Silo.Combat/Health/
[ ] Create EnemyController, Animator, Audio, Manager
[ ] Create HeroAnimator
[ ] Update namespace references (DeNelle.Combat.*)
[ ] Update prefab references
```

### Phase 4–9: Move Other Silos
(Repeat pattern: move files, update namespaces, fix imports)

### Phase 10: Integration Testing
```
[ ] Full playthrough: spawn → fight → win
[ ] All menus work
[ ] Store accessible
[ ] Hero select works
[ ] No cross-silo imports
[ ] Console clean
```

---

## Key Namespace Rules

**Core never imports anything else:**
```csharp
// ✅ OK
using DeNelle.Core.Managers;

// ❌ NEVER
using DeNelle.Combat.Enemy;  // Cross-silo!
```

**All silos import Core:**
```csharp
// ✅ OK in any silo
using DeNelle.Core.Services;
using DeNelle.Core.Interfaces;
```

**Silos use CoreServices for cross-silo communication:**
```csharp
// ✅ OK (goes through Core)
CoreServices.Audio.PlaySound(...);

// ❌ NOT OK (direct cross-silo import)
audioService.PlaySound(...);  // Where did this come from?
```

---

## Quick Reference: "Where does X go?"

| If You Have... | It Goes In... | Namespace |
|---|---|---|
| Hero animation code | Silo.Combat/Hero/ | DeNelle.Combat.Hero |
| Tower targeting logic | Silo.World/Building/ | DeNelle.World.Building |
| Store purchase button | Silo.UI/Store/ | DeNelle.UI.Store |
| Currency data | Silo.Economy/ | DeNelle.Economy.Currency |
| Pet leveling | Silo.Progression/Pet/ | DeNelle.Progression.Pet |
| Sound effect playback | Silo.AudioVFX/Audio/ | DeNelle.AudioVFX.Audio |
| Story triggers | Silo.Narrative/Events/ | DeNelle.Narrative.Events |
| Game startup logic | Silo.Core/Managers/ | DeNelle.Core.Managers |

---

This map is your reference for WO-232 migration. Print it out, check it off as you go.
