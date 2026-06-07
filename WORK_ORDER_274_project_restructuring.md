# WO-274: Comprehensive Project Restructuring

**Status: READY TO IMPLEMENT**

**Date:** 2026-06-01  
**Priority:** 🔴 CRITICAL (foundation for all future work)  
**Owner:** CLI  
**Blocks:** WO-231, WO-228/229, WO-214, all future development  
**Depends On:** None (but should run FIRST before other major work)  
**Time Estimate:** 8–10 hours

---

## Why This Matters

Current project structure is disorganized → makes everything harder:
- Scripts scattered randomly → hard to find things
- No clear responsibility boundaries → code does too much
- Data mixed with logic → difficult to configure/test
- Managers not centralized → reference chaos
- Future developers (or you) waste time looking for files

**After restructuring:** Clean, scalable, professional structure. All future work (WO-231, WO-228, etc.) goes into RIGHT places.

---

## Target Structure

```
Assets/
├── Scripts/
│   ├── Core/                        # Base systems (highest priority)
│   │   ├── Managers/
│   │   │   ├── GameManager.cs       # Central hub (NEW)
│   │   │   ├── ExteriorSceneManager.cs
│   │   │   └── WaveManager.cs
│   │   ├── Data/                    # ScriptableObjects
│   │   │   ├── EnemyData.cs
│   │   │   ├── WaveData.cs
│   │   │   ├── TowerData.cs
│   │   │   └── GameSettings.cs
│   │   ├── Utils/
│   │   │   ├── Constants.cs
│   │   │   ├── ExteriorConstants.cs
│   │   │   └── Extensions.cs
│   │   └── Events/                  # Event system (optional, for future)
│   │
│   ├── World/                       # World building
│   │   ├── Builders/
│   │   │   ├── VillageSceneBuilder.cs
│   │   │   └── ExteriorTerrainBuilder.cs
│   │   ├── Components/
│   │   │   └── [Environmental components]
│   │   └── Generation/
│   │
│   ├── Entities/                    # All game objects
│   │   ├── Enemies/
│   │   │   ├── EnemyController.cs
│   │   │   ├── EnemyBrain.cs
│   │   │   └── EnemyHealth.cs
│   │   ├── Towers/
│   │   │   ├── TowerController.cs
│   │   │   ├── TowerAiming.cs
│   │   │   └── TowerAttack.cs
│   │   ├── Heroes/
│   │   │   ├── HeroController.cs
│   │   │   └── HeroHealth.cs
│   │   ├── Buildings/
│   │   │   ├── BuildingController.cs
│   │   │   └── BuildingHealth.cs
│   │   └── Player/
│   │       └── PlayerInputHandler.cs
│   │
│   ├── Combat/                      # ATB & damage systems
│   │   ├── ATBCombatManager.cs
│   │   ├── BattleController.cs
│   │   ├── DamageSystem.cs
│   │   └── IDamageableStructure.cs
│   │
│   ├── UI/
│   │   ├── Screens/
│   │   │   ├── HeroSelectScreen.cs
│   │   │   ├── BattleUI.cs
│   │   │   └── VillageHUD.cs
│   │   ├── HUD/
│   │   │   ├── ResourceDisplay.cs
│   │   │   ├── HealthBar.cs
│   │   │   └── WaveIndicator.cs
│   │   └── Components/
│   │       └── [Reusable UI components]
│   │
│   ├── Audio/
│   │   ├── AudioService.cs
│   │   ├── SfxClipLibrary.cs
│   │   └── MusicManager.cs
│   │
│   ├── VFX/
│   │   ├── VFXManager.cs
│   │   └── [Particle effect controllers]
│   │
│   ├── SaveSystem/
│   │   ├── SaveManager.cs
│   │   └── SaveData.cs
│   │
│   └── Animation/
│       └── AnimationManager.cs
│
├── Prefabs/
│   ├── Enemies/
│   │   ├── Skeleton.prefab
│   │   ├── Ghoul.prefab
│   │   └── [Other enemy types]
│   ├── Towers/
│   │   ├── CrystalTower.prefab
│   │   ├── ArcaneTower.prefab
│   │   └── [Other towers]
│   ├── World/
│   │   ├── Buildings/
│   │   └── Gates/
│   ├── UI/
│   │   └── [UI prefabs]
│   └── Effects/
│       ├── SpawnPortal.prefab
│       └── [VFX prefabs]
│
├── ScriptableObjects/
│   ├── Waves/
│   │   ├── Wave_1.asset
│   │   ├── Wave_2.asset
│   │   └── [Wave configs]
│   ├── Enemies/
│   │   ├── Skeleton_Data.asset
│   │   ├── Ghoul_Data.asset
│   │   └── [Enemy configs]
│   ├── Towers/
│   │   ├── CrystalTower_Data.asset
│   │   └── [Tower configs]
│   └── GameSettings/
│       ├── GlobalSettings.asset
│       └── DifficultySettings.asset
│
├── Scenes/
│   ├── Bootstrap.unity              # Managers only (load other scenes additively)
│   ├── Village.unity
│   ├── BattleArena.unity
│   └── [Other scenes]
│
├── Materials/
├── Textures/
├── Models/
├── Audio/
│   ├── Music/
│   ├── SFX/
│   └── Ambience/
├── Animations/
├── Prefabs/
└── Resources/                       # Only runtime-loaded assets
```

---

## Migration Steps (Detailed)

### Phase 1: Create Folder Structure (30 min)

1. **In Unity, create all folders** listed above (right-click Assets → Create Folder)
2. **Do NOT move files yet** — just create empty structure
3. **Verify structure** by looking at Assets/ in Project browser

### Phase 2: Move Core Systems (2 hours)

**Move these first (highest priority):**

1. **Core/Managers/**
   - Move/create: GameManager.cs (NEW — see code below)
   - Move: WaveManager.cs → Core/Managers/
   - Move: ExteriorSceneManager.cs → Core/Managers/ (from WO-231)
   - Move: BattleController.cs → Core/Managers/

2. **Core/Utils/**
   - Move: ExteriorConstants.cs → Core/Utils/
   - Create: GameConstants.cs (global game config)

3. **Core/Data/**
   - Create: EnemyData.cs (ScriptableObject for enemy stats)
   - Create: WaveData.cs (ScriptableObject for wave configs)
   - Create: TowerData.cs (ScriptableObject for tower stats)

### Phase 3: Move World Systems (1.5 hours)

4. **World/Builders/**
   - Move: VillageSceneBuilder.cs → World/Builders/
   - Move: ExteriorTerrainBuilder.cs → World/Builders/

### Phase 4: Move Entity Systems (2.5 hours)

5. **Entities/Enemies/**
   - Move: EnemyController.cs → Entities/Enemies/
   - Move: EnemyBrain.cs → Entities/Enemies/
   - Move: EnemyHealth.cs → Entities/Enemies/ (implements IDamageableStructure)

6. **Entities/Towers/**
   - Move: TowerController.cs → Entities/Towers/
   - Move: TowerAiming.cs → Entities/Towers/
   - Move: TowerAttack.cs → Entities/Towers/

7. **Entities/Heroes/**
   - Move: HeroController.cs → Entities/Heroes/
   - Move: HeroHealth.cs → Entities/Heroes/

8. **Entities/Buildings/**
   - Move: BuildingController.cs → Entities/Buildings/
   - Move: BuildingHealth.cs → Entities/Buildings/ (implements IDamageableStructure)

### Phase 5: Move Combat Systems (1 hour)

9. **Combat/**
   - Move: ATBCombatManager.cs → Combat/
   - Move: BattleController.cs → Combat/
   - Move: DamageSystem.cs → Combat/
   - Move: IDamageableStructure.cs → Combat/

### Phase 6: Move UI Systems (1 hour)

10. **UI/Screens/ & UI/HUD/**
    - Move: HeroSelectScreen.cs → UI/Screens/
    - Move: BattleUI.cs → UI/Screens/
    - Move: VillageHUD.cs → UI/Screens/
    - Move: ResourceDisplay.cs → UI/HUD/
    - Move: HealthBar.cs → UI/HUD/

### Phase 7: Move Supporting Systems (1 hour)

11. **Audio/, VFX/, SaveSystem/**, etc.
    - Move corresponding scripts to their folders
    - Keep support systems organized

### Phase 8: Prefabs → Prefabs/ (1 hour)

12. **Organize all prefabs:**
    - Enemy prefabs → Prefabs/Enemies/
    - Tower prefabs → Prefabs/Towers/
    - UI prefabs → Prefabs/UI/
    - Effect prefabs → Prefabs/Effects/

### Phase 9: ScriptableObjects (30 min)

13. **Organize all .asset files:**
    - Wave configs → ScriptableObjects/Waves/
    - Enemy configs → ScriptableObjects/Enemies/
    - Tower configs → ScriptableObjects/Towers/

### Phase 10: Fix References (3–4 hours)

14. **Update all script references**
    - Search for old folder paths
    - Update Resources.Load() calls
    - Update prefab references in inspector
    - Fix assembly definitions if used

15. **Test each subsystem:**
    - Load Village scene → no errors?
    - Load Battle scene → no errors?
    - Spawn wave → enemies appear?
    - Place tower → tower works?

---

## Core GameManager.cs (NEW)

Create: **Assets/Scripts/Core/Managers/GameManager.cs**

```csharp
using UnityEngine;

/// <summary>
/// Central hub for all game managers.
/// Attach this to a GameObject in the Bootstrap scene.
/// DO NOT destroy on load — this persists across scenes.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("=== Core Managers ===")]
    public ExteriorSceneManager ExteriorSceneManager;
    public ExteriorTerrainBuilder TerrainBuilder;
    public WaveManager WaveManager;
    public BattleController BattleController;
    public UIManager UIManager;
    public AudioService AudioService;
    public SaveManager SaveManager;

    [Header("=== Game State ===")]
    public int CurrentWave { get; set; }
    public bool IsInBattle { get; set; }

    private void Awake()
    {
        // Singleton pattern
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        Debug.Log("✅ GameManager initialized");
    }

    private void Start()
    {
        InitializeGame();
    }

    public void InitializeGame()
    {
        // Initialize in order of dependency
        if (AudioService != null) AudioService.Initialize();
        if (SaveManager != null) SaveManager.LoadGame();
        if (TerrainBuilder != null) TerrainBuilder.BuildExteriorWorld();
        if (WaveManager != null) WaveManager.StartWave(1);

        Debug.Log("✅ Game initialized");
    }

    public void PauseGame()
    {
        Time.timeScale = 0f;
    }

    public void ResumeGame()
    {
        Time.timeScale = 1f;
    }
}
```

---

## Key Refactoring Rules

### 1. One Responsibility Per Script
- ExteriorTerrainBuilder → only builds terrain
- WaveManager → only manages waves
- EnemyController → only controls one enemy
- Don't mix concerns

### 2. Use GameManager as Central Hub
- All managers register with GameManager.Instance
- Other scripts query GameManager to find what they need
- Avoids direct hard references between systems

### 3. ScriptableObjects for Data
**Example: EnemyData.cs**
```csharp
[CreateAssetMenu(fileName = "Enemy_", menuName = "Game/Enemy Data")]
public class EnemyData : ScriptableObject
{
    public string enemyName;
    public float maxHealth = 50f;
    public float moveSpeed = 3.5f;
    public int goldReward = 10;
    public GameObject prefab;
}
```

Then in EnemyController:
```csharp
public class EnemyController : MonoBehaviour
{
    [SerializeField] private EnemyData data;
    
    private void Start()
    {
        health = data.maxHealth;
        agent.speed = data.moveSpeed;
    }
}
```

### 4. Clear Naming
- Classes: PascalCase (GameManager, EnemyController)
- Methods: PascalCase (SpawnWave, OnEnemyDeath)
- Private fields: _camelCase (_health, _moveSpeed)
- Public fields: camelCase (speed, health)

---

## Validation Checklist

- [ ] All folders created
- [ ] All scripts moved (no scripts remain in root Assets/Scripts/)
- [ ] All prefabs moved to Prefabs/
- [ ] All ScriptableObjects moved to ScriptableObjects/
- [ ] All .meta files intact (Unity auto-generates these)
- [ ] GameManager.cs created and attached to scene
- [ ] All script references updated (no missing references in Inspector)
- [ ] Village scene loads with no errors
- [ ] Battle scene loads with no errors
- [ ] Spawn wave → enemies appear
- [ ] Place tower → tower targets enemies
- [ ] No console errors on startup

---

## Assembly Definitions (Optional, for Performance)

After restructuring, consider creating assembly definitions for faster compilation:

- `Assets/Scripts/Core/Core.asmdef` → all Core/ scripts
- `Assets/Scripts/World/World.asmdef` → all World/ scripts
- `Assets/Scripts/Entities/Entities.asmdef` → all Entities/ scripts
- etc.

This lets Unity compile only changed modules (massive speedup on large projects).

**For now, skip this.** Add later if compile times become slow.

---

## Expected Benefits After Restructuring

✅ **Easier to find files** — organized by system, not random  
✅ **Easier to add features** — know exactly where code goes  
✅ **Easier to debug** — clear system boundaries  
✅ **Easier to onboard** — new devs understand structure  
✅ **Easier to refactor** — systems are isolated  
✅ **Cleaner Inspector** — fewer references to hunt for  

---

## Common Pitfalls to Avoid

❌ **Don't move files while Unity is open** → can break references  
**Fix:** Close Unity, move files in File Explorer, reopen

❌ **Don't change script names during move** → breaks prefab links  
**Fix:** Move first, rename second

❌ **Don't forget .meta files** → Unity loses track of assets  
**Fix:** Move entire folders (includes .meta), don't just copy code

❌ **Don't leave any scripts in root Assets/Scripts/** → defeats the purpose  
**Fix:** Every script should be in a subsystem folder

---

## Timeline

| Phase | Task | Time |
|---|---|---|
| 1 | Create folder structure | 30 min |
| 2 | Move Core systems | 2 hr |
| 3 | Move World systems | 1.5 hr |
| 4 | Move Entity systems | 2.5 hr |
| 5 | Move Combat systems | 1 hr |
| 6 | Move UI systems | 1 hr |
| 7 | Move Support systems | 1 hr |
| 8 | Organize Prefabs | 1 hr |
| 9 | Organize ScriptableObjects | 30 min |
| 10 | Fix references + test | 3–4 hr |
| **Total** | | **14–15 hr** |

**Practical recommendation:** Do in 2 sessions (8 hr each) with a break in between.

---

## After Restructuring

✅ **All future work** (WO-231, WO-228, etc.) goes into the right folders  
✅ **New code** automatically follows clean patterns  
✅ **Debugging** becomes faster (know where to look)  
✅ **Onboarding** new devs becomes easy (structure is obvious)

---

**Commit message:** `"WO-274: restructure project for scalability — organize scripts by system, create GameManager hub, establish folder conventions"`

**This WO should run FIRST, before WO-231, WO-228, and other major work.**

