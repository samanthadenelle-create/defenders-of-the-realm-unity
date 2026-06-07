# Architecture Reference — Complete Package

**Date:** 2026-06-01  
**Purpose:** Complete specification for project restructuring, scene flow, and core systems  
**Related WO:** WO-232 (Project Restructuring)

---

## A) Complete Script Inventory & Responsibilities

### Core/Managers/

| Script | Responsibility |
|---|---|
| **GameManager.cs** | Central hub, holds all manager references, DontDestroyOnLoad |
| **SceneLoader.cs** | Handles scene transitions with fade effects |
| **WaveManager.cs** | Controls wave spawning, timing, enemy composition |
| **BattleController.cs** | Manages ATB combat state, turn order, transitions |
| **UIManager.cs** | Central UI hub, shows/hides screens and HUD |
| **AudioManager.cs** | Global sound & music playback |
| **SaveManager.cs** | Save/Load game state |

### World/Builders/

| Script | Responsibility |
|---|---|
| **ExteriorTerrainBuilder.cs** | Builds large exterior terrain, roads, spawn aprons (WO-231) |
| **VillageSceneBuilder.cs** | Builds village walls, gates, buildings, towers |

### Entities/Enemies/

| Script | Responsibility |
|---|---|
| **EnemyController.cs** | Single enemy AI, movement, decision-making |
| **EnemyHealth.cs** | Enemy health, damage, death (implements IDamageableStructure) |
| **EnemyBrain.cs** | Enemy state machine (idle, march, attack, die) |

### Entities/Towers/

| Script | Responsibility |
|---|---|
| **TowerController.cs** | Tower placement, targeting, behavior |
| **TowerAttack.cs** | Tower firing, projectiles, damage |
| **TowerAiming.cs** | Find targets, aim at enemies |

### Entities/Player/

| Script | Responsibility |
|---|---|
| **PlayerController.cs** | Player input, camera control, build mode |

### Entities/Buildings/

| Script | Responsibility |
|---|---|
| **BuildingController.cs** | Village buildings (houses, shops, etc.) |
| **BuildingHealth.cs** | Building HP, damage, destruction (implements IDamageableStructure) |

### Combat/

| Script | Responsibility |
|---|---|
| **ATBSystem.cs** | Active Time Battle logic, turn queue, action timing |
| **DamageSystem.cs** | Calculate damage, apply effects |
| **IDamageableStructure.cs** | Interface for anything that can take damage |

### Data/ (ScriptableObjects)

| Script | Responsibility |
|---|---|
| **EnemyData.cs** | Enemy configuration (stats, prefab, animations) |
| **WaveData.cs** | Wave composition (which enemies, how many, timing) |
| **TowerData.cs** | Tower configuration (stats, cost, range, damage) |
| **GameSettings.cs** | Global settings (difficulty, balance tweaks) |

### Utils/

| Script | Responsibility |
|---|---|
| **ExteriorConstants.cs** | All world scale constants (spawn distance, road length, etc.) |
| **EventBus.cs** | Global event system (optional, for future use) |

---

## B) GameManager + Service Locator Pattern

### GameManager.cs (Complete Implementation)

```csharp
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    // Core Systems (Service Locator Pattern)
    public ExteriorTerrainBuilder TerrainBuilder { get; private set; }
    public VillageSceneBuilder VillageBuilder { get; private set; }
    public WaveManager WaveManager { get; private set; }
    public BattleController BattleController { get; private set; }
    public UIManager UIManager { get; private set; }
    public AudioManager AudioManager { get; private set; }
    public SaveManager SaveManager { get; private set; }
    public SceneLoader SceneLoader { get; private set; }

    // Game State
    public int CurrentWave { get; set; }
    public bool IsInBattle { get; set; }
    public bool IsPaused { get; set; }

    private void Awake()
    {
        // Enforce singleton pattern
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Find all managers in the scene
        FindAllManagers();

        Debug.Log("✅ GameManager initialized");
    }

    private void FindAllManagers()
    {
        TerrainBuilder = FindAnyObjectByType<ExteriorTerrainBuilder>();
        VillageBuilder = FindAnyObjectByType<VillageSceneBuilder>();
        WaveManager = FindAnyObjectByType<WaveManager>();
        BattleController = FindAnyObjectByType<BattleController>();
        UIManager = FindAnyObjectByType<UIManager>();
        AudioManager = FindAnyObjectByType<AudioManager>();
        SaveManager = FindAnyObjectByType<SaveManager>();
        SceneLoader = FindAnyObjectByType<SceneLoader>();

        Debug.Log("   → All managers found");
    }

    public void InitializeNewGame()
    {
        CurrentWave = 1;
        IsInBattle = false;

        // Build world
        TerrainBuilder?.BuildExteriorWorld();
        VillageBuilder?.BuildVillageScene();

        // Start first wave
        WaveManager?.StartWave(1);

        Debug.Log("✅ Game initialized");
    }

    public void PauseGame()
    {
        IsPaused = true;
        Time.timeScale = 0f;
        UIManager?.ShowPauseMenu();
    }

    public void ResumeGame()
    {
        IsPaused = false;
        Time.timeScale = 1f;
        UIManager?.HidePauseMenu();
    }

    public void TransitionToBattle()
    {
        IsInBattle = true;
        SceneLoader?.LoadBattleScene();
    }

    public void TransitionToVillage()
    {
        IsInBattle = false;
        SceneLoader?.LoadVillageScene();
    }
}
```

**How to use from other scripts:**

```csharp
public class EnemyController : MonoBehaviour
{
    private void Start()
    {
        // Access WaveManager through GameManager
        GameManager.Instance.WaveManager.RegisterEnemy(this);
    }

    private void OnDeath()
    {
        // Tell WaveManager this enemy died
        GameManager.Instance.WaveManager.UnregisterEnemy(this);
    }
}
```

---

## C) Scene Flow Architecture

### Scene Structure

```
Bootstrap (Persistent)
  └─ GameManager (Singleton, DontDestroyOnLoad)
  └─ SceneLoader
  └─ AudioManager
  └─ SaveManager

                ↓ Load Village

Village (Main Defense Scene)
  ├─ ExteriorTerrainBuilder (builds exterior on Start)
  ├─ VillageSceneBuilder (builds village on Start)
  ├─ WaveManager (spawns waves)
  ├─ PlayerController (input & camera)
  ├─ BattleController (optional — can be same scene)
  └─ UIManager (shows HUD)

                ↓ Load Battle (if using separate scene)

Battle (ATB Combat)
  ├─ ATBSystem (turn order, combat flow)
  ├─ Hero & Enemy instances (from prefabs)
  └─ BattleUI (combat HUD)

                ↓ Back to Village
```

### SceneLoader.cs (Complete Implementation)

```csharp
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class SceneLoader : MonoBehaviour
{
    [SerializeField] private CanvasGroup fadeCanvasGroup; // Assign in Inspector
    [SerializeField] private float fadeDuration = 0.5f;

    public void LoadVillageScene()
    {
        StartCoroutine(LoadSceneWithFade("Village"));
    }

    public void LoadBattleScene()
    {
        StartCoroutine(LoadSceneWithFade("Battle"));
    }

    public void ReloadCurrentScene()
    {
        StartCoroutine(LoadSceneWithFade(SceneManager.GetActiveScene().name));
    }

    private IEnumerator LoadSceneWithFade(string sceneName)
    {
        // Fade out
        if (fadeCanvasGroup != null)
        {
            yield return StartCoroutine(FadeTo(1f)); // Black
        }

        // Load scene
        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
        op.allowSceneActivation = false;

        while (op.progress < 0.9f)
            yield return null;

        op.allowSceneActivation = true;
        yield return new WaitForSeconds(0.5f);

        // Fade in
        if (fadeCanvasGroup != null)
        {
            yield return StartCoroutine(FadeTo(0f)); // Clear
        }
    }

    private IEnumerator FadeTo(float targetAlpha)
    {
        float startAlpha = fadeCanvasGroup.alpha;
        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            fadeCanvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / fadeDuration);
            yield return null;
        }

        fadeCanvasGroup.alpha = targetAlpha;
    }
}
```

### How to Set Up Scenes

**Bootstrap Scene:**
1. Create empty GameObject called "Managers"
2. Add GameManager.cs script
3. Add SceneLoader.cs script (with Canvas for fade, assign in Inspector)
4. Add AudioManager.cs script
5. Add SaveManager.cs script
6. **Set this as first scene in Build Settings**

**Village Scene:**
1. Create empty GameObject called "World"
2. Add ExteriorTerrainBuilder.cs
3. Add VillageSceneBuilder.cs
4. Add WaveManager.cs
5. Add PlayerController.cs (for input & camera)
6. Create Canvas with UIManager.cs

**Battle Scene:**
1. Create empty GameObject called "Battle"
2. Add ATBSystem.cs
3. Create Canvas with BattleUI.cs
4. Instantiate Hero and Enemy prefabs dynamically

---

## D) ScriptableObject Templates

### EnemyData.cs

```csharp
using UnityEngine;

[CreateAssetMenu(menuName = "Defenders/Enemy Data")]
public class EnemyData : ScriptableObject
{
    [Header("Identity")]
    public string enemyName = "Goblin";
    public Sprite icon;

    [Header("Stats")]
    public float maxHealth = 120f;
    public float moveSpeed = 3.8f;
    public float attackDamage = 15f;
    public int goldReward = 8;

    [Header("Prefab")]
    public GameObject prefab;
    public RuntimeAnimatorController animator;
}
```

**Usage:** Create assets like `Assets/ScriptableObjects/Enemies/Goblin_Data.asset`

Then in WaveManager:
```csharp
[SerializeField] private EnemyData[] enemyDatabase;

public GameObject SpawnEnemy(EnemyData data, Vector3 position)
{
    GameObject enemy = Instantiate(data.prefab, position, Quaternion.identity);
    EnemyController controller = enemy.GetComponent<EnemyController>();
    controller.SetData(data);
    return enemy;
}
```

### WaveData.cs

```csharp
using UnityEngine;

[CreateAssetMenu(menuName = "Defenders/Wave Data")]
public class WaveData : ScriptableObject
{
    public int waveNumber;
    
    [Header("Enemies")]
    public EnemyData[] enemyTypes;
    public int[] enemyCounts;        // e.g., [5, 3, 1] = 5 goblins, 3 orcs, 1 boss
    
    [Header("Timing")]
    public float spawnInterval = 0.8f;  // Time between spawns
    public float timeBeforeNextWave = 45f;

    [Header("Difficulty")]
    public float healthMultiplier = 1f;
    public float damageMultiplier = 1f;
}
```

**Usage in WaveManager:**
```csharp
[SerializeField] private WaveData[] waves;

public void StartWave(int waveIndex)
{
    WaveData wave = waves[waveIndex - 1];
    
    int totalEnemies = 0;
    for (int i = 0; i < wave.enemyTypes.Length; i++)
    {
        for (int j = 0; j < wave.enemyCounts[i]; j++)
        {
            StartCoroutine(SpawnEnemyWithDelay(
                wave.enemyTypes[i],
                totalEnemies * wave.spawnInterval
            ));
            totalEnemies++;
        }
    }

    // Schedule next wave
    Invoke(nameof(NextWave), wave.timeBeforeNextWave);
}
```

### TowerData.cs

```csharp
using UnityEngine;

[CreateAssetMenu(menuName = "Defenders/Tower Data")]
public class TowerData : ScriptableObject
{
    public string towerName = "Crystal Tower";
    
    [Header("Stats")]
    public float attackRange = 25f;
    public float attackCooldown = 2f;
    public float damage = 30f;
    
    [Header("Cost")]
    public int goldCost = 100;
    public int crystalCost = 50;
    
    [Header("Prefab")]
    public GameObject prefab;
}
```

---

## E) Complete Folder Structure Quick Reference

```
Assets/
├── Scripts/
│   ├── Core/Managers/
│   │   ├── GameManager.cs
│   │   ├── SceneLoader.cs
│   │   ├── WaveManager.cs
│   │   ├── BattleController.cs
│   │   ├── UIManager.cs
│   │   ├── AudioManager.cs
│   │   └── SaveManager.cs
│   ├── Core/Data/
│   │   ├── EnemyData.cs
│   │   ├── WaveData.cs
│   │   ├── TowerData.cs
│   │   └── GameSettings.cs
│   ├── Core/Utils/
│   │   ├── ExteriorConstants.cs
│   │   └── EventBus.cs (optional)
│   ├── World/Builders/
│   │   ├── ExteriorTerrainBuilder.cs
│   │   └── VillageSceneBuilder.cs
│   ├── Entities/Enemies/
│   │   ├── EnemyController.cs
│   │   ├── EnemyHealth.cs
│   │   └── EnemyBrain.cs
│   ├── Entities/Towers/
│   │   ├── TowerController.cs
│   │   ├── TowerAttack.cs
│   │   └── TowerAiming.cs
│   ├── Entities/Player/
│   │   └── PlayerController.cs
│   ├── Entities/Buildings/
│   │   ├── BuildingController.cs
│   │   └── BuildingHealth.cs
│   ├── Combat/
│   │   ├── ATBSystem.cs
│   │   ├── DamageSystem.cs
│   │   └── IDamageableStructure.cs
│   ├── UI/
│   │   ├── UIManager.cs
│   │   └── [UI screen scripts]
│   └── Audio/
│       └── AudioManager.cs
├── Prefabs/
│   ├── Enemies/
│   ├── Towers/
│   ├── World/
│   ├── UI/
│   └── Effects/
├── ScriptableObjects/
│   ├── Enemies/
│   │   ├── Goblin_Data.asset
│   │   ├── Orc_Data.asset
│   │   └── [More enemy configs]
│   ├── Waves/
│   │   ├── Wave_1.asset
│   │   ├── Wave_2.asset
│   │   └── [More wave configs]
│   ├── Towers/
│   │   └── [Tower configs]
│   └── GameSettings/
│       └── GlobalSettings.asset
├── Scenes/
│   ├── Bootstrap.unity (First scene)
│   ├── Village.unity
│   └── Battle.unity
├── Materials/
├── Textures/
├── Models/
├── Audio/
├── Animations/
└── Resources/ (Optional)
```

---

## F) Getting Started Checklist

### Phase 1: Setup (Day 1)
- [ ] Create all folders per the structure above
- [ ] Create Bootstrap scene with GameManager + SceneLoader
- [ ] Create Village scene (empty for now)
- [ ] Create Battle scene (empty for now)
- [ ] Set Bootstrap as first scene in Build Settings

### Phase 2: Core Managers (Day 1–2)
- [ ] Implement GameManager.cs (service locator)
- [ ] Implement SceneLoader.cs (scene transitions)
- [ ] Implement WaveManager.cs (wave spawning)
- [ ] Create sample EnemyData, WaveData assets

### Phase 3: World Building (Day 2–3)
- [ ] Implement ExteriorTerrainBuilder.cs (WO-231)
- [ ] Implement VillageSceneBuilder.cs
- [ ] Build and test exterior + village scenes

### Phase 4: Entities (Day 3–4)
- [ ] Implement EnemyController.cs
- [ ] Implement TowerController.cs
- [ ] Test spawning enemies, placing towers

### Phase 5: Combat (Day 4–5)
- [ ] Implement ATBSystem.cs
- [ ] Implement BattleController.cs
- [ ] Test ATB combat flow

### Phase 6: Polish (Day 5+)
- [ ] Add audio, VFX, UI
- [ ] Test end-to-end flow

---

## G) Pro Tips

1. **Always start from GameManager** — other scripts get managers via `GameManager.Instance.ManagerName`
2. **Use ScriptableObjects for data** — easier to tune, no code recompile needed
3. **One scene per major context** — Bootstrap → Village → Battle keeps things clean
4. **Prefabs in Prefabs/ folder** — makes them easy to find and manage
5. **Keep scripts focused** — each script should have ONE main responsibility

---

**This is the complete architecture reference. Use it as your guide for WO-232 restructuring and all future development.**

