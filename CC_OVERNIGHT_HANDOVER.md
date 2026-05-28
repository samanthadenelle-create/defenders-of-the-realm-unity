# Defenders of the Realm — CC Overnight Handover
**Date:** 2026-05-26 | **Project:** Defenders of the Realm (Unity 6000.4.8f1 / URP)  
**Repo:** `defenders-unity` | **Linear:** https://linear.app/defenders-of-the-realm

---

## Standing Order (READ FIRST — NON-NEGOTIABLE)

> **Do NOT fix issues in-place and silently move on. Every bug, regression, or gap found during implementation must be filed as a Linear work order on the CC team, then addressed in the next pass.**

All code submitted for review goes through a correction loop. Code is only stamped and merged once it has zero outstanding issues. If you find a problem while implementing a stamped ticket, stop, file it, and keep going with clean work elsewhere.

---

## Architecture Non-Negotiables

Every file, every ticket, no exceptions.

| Rule | Requirement |
|------|-------------|
| **Component caching** | Always `Awake()`. Never `Initialize()`, `Start()`, or anywhere else. |
| **FindObjectOfType** | Deprecated. Use `FindObjectsByType<T>(FindObjectsSortMode.None)` with length guard. |
| **FindGameObjectWithTag** | Only in `Start()`, never `Awake()`. |
| **Initialize() signature** | Always additive. Never remove params, never reorder. `Transform target` always second. Never `virtual`. |
| **NavMesh SetDestination** | Throttled 0.2–0.8s. Never every frame. |
| **IDamageable** | `TakeDamage(float)` + `float CurrentHealth { get; }` |
| **ScriptableObjects** | Pure data. Never mutated at runtime. |
| **OverlapSphere** | Always `Physics.OverlapSphereNonAlloc` with pre-allocated `Collider[]` buffer. |
| **LayerMask** | `LayerMask.GetMask()` cached in `Awake()`. Never per-frame string lookup. |
| **Time in shake/hit-stop** | `Time.unscaledDeltaTime`. Never `Time.deltaTime` in shake coroutines or hit-stop. |
| **Distance comparisons** | `sqrMagnitude`. Never `Vector3.Distance()`. |
| **Animator hashes** | `static readonly int` via `Animator.StringToHash()`. Never string trigger lookup. |
| **UI updates** | Event-driven via `Action` events. No polling in `Update()`. |
| **Event subscription lifecycle** | Always `OnEnable` / `OnDisable`. |
| **Bob / sinusoidal animation** | `target.y += Mathf.Sin(Time.time * freq) * amplitude` — modulate the TARGET before SmoothDamp. Never `transform.position +=`. |
| **Singleton pattern** | `if (Instance != null && Instance != this) { Destroy(gameObject); return; }` + clear in `OnDestroy`. |
| **WaitForSeconds in hit-stop** | `WaitForSecondsRealtime`. Never `WaitForSeconds` when `Time.timeScale` is modified. |
| **Particle toggle** | `ParticleSystem.EmissionModule.enabled`. Never `SetActive()` on particle GameObjects. |
| **Throttled detection vs movement** | Detection throttled. Movement in regular `Update()` or coroutine. Never put movement inside the throttled block. |
| **Subclassing EnemyBrain** | Prohibited. All variants use optional params on `Initialize()`. No subclasses ever. |
| **Full-file replacements** | Prohibited. Always submit deltas — only changed methods/fields. Full replacements re-open fixed issues. |

---

## Assembly Map

```
DeNelle.Core             — base interfaces, singletons, ScriptableObjects
DeNelle.Core.Combat      — IDamageable, combat interfaces
DeNelle.Core.Data        — WaveData, WaveDifficulty, TacticalData, MissionData, CampaignData SOs
DeNelle.Core.Progression — HeroProgression, TalentTree
DeNelle.Village          — WaveManager, EnemyGroupSpawner, HeartOfTown
DeNelle.Village.UI       — HUD, BossHealthBar, TowerDamageVisuals
DeNelle.Village.Enemies  — EnemyBrain, EnemyGroupCoordinator
DeNelle.Village.VFX      — TowerChargeVFX, HeroChargeVFX
DeNelle.AI               — SmartMobileCamera
```

`DeNelle.Village.asmdef` → already references `DeNelle.Core.asmdef` and `DeNelle.Core.Data.asmdef`. ✓

---

## Shared Infrastructure (Confirmed)

### WaveManager Events
All four are already declared. Use as-is — do not redeclare.
```csharp
public UnityEvent OnWaveStarted;
public UnityEvent OnWaveCompleted;
public UnityEvent OnAllWavesCleared;
public UnityEvent OnBossSpawned;  // added in DEF-60
```
Subscribe/unsubscribe via `OnEnable`/`OnDisable` everywhere that uses them.

### HeartOfTown.OnHealthChanged
```csharp
public event Action<float> OnHealthChanged;  // passes health percentage 0–1
```

### EnemyType
ScriptableObject. Assigned per prefab in the Inspector. Never instantiated at runtime.

### BossWaveConfig
Exists — stamped in DEF-60. Import from `DeNelle.Core.Data`. Do not redefine.

---

## Implementation Priority

Work top to bottom. Do not start the next group until the current group compiles clean with zero warnings.

---

## GROUP 0 — Tower Building Core Loop ⭐ FIRST PRIORITY — START HERE

Six tickets. Strict dependency order — do not skip ahead.

```
DEF-78  →  DEF-73  →  DEF-74  →  DEF-75
                   →  DEF-76
                   →  DEF-77
```

| Order | Ticket | Files | Blocked until |
|-------|--------|-------|---------------|
| 1st | [DEF-78](https://linear.app/defenders-of-the-realm/issue/DEF-78) | EconomyService | — start here |
| 2nd | [DEF-73](https://linear.app/defenders-of-the-realm/issue/DEF-73) | TowerData, SkillTypes, SpecialAbility, TowerPlacementSystem, SkillSystem | DEF-78 stamp |
| 3rd | [DEF-74](https://linear.app/defenders-of-the-realm/issue/DEF-74) | Tower, TowerUpgradeButton | DEF-73 stamp |
| 3rd | [DEF-76](https://linear.app/defenders-of-the-realm/issue/DEF-76) | TowerConstructionQueue, TowerQueueItem, TowerConstruction, ProgressBar, Billboard | DEF-73 stamp |
| 4th | [DEF-75](https://linear.app/defenders-of-the-realm/issue/DEF-75) | Tower VFX delta | DEF-74 stamp |
| 4th | [DEF-77](https://linear.app/defenders-of-the-realm/issue/DEF-77) | SkillSystem delta, LevelUpSkillPopup, HeroProgression delta | DEF-73 stamp |

### Confirmed decisions — locked, no further discussion needed

| Question | Answer |
|----------|--------|
| EconomyService | Create fresh — DEF-78. Wood-only stub, expand later. |
| SkillType enum | `{ None, Blacksmith, Woodworking, Arcane, GatheringSpeed }` — single file, DeNelle.Core.Data |
| upgrades array | 3 entries. `[0]`=L1, `[1]`=L2, `[2]`=L3. No `basePrefab`. |
| Input system | **Legacy** — `Input.GetMouseButtonDown`, `Input.mousePosition` |
| HeroProgression.OnLevelUp | Does not exist — CC adds as delta |
| ProgressBar | **World-space** canvas + `Billboard.cs` facing camera |

### Shared corrections that apply across ALL tickets (fix once in DEF-78/73, carry through):

**TowerData.cs — must have in every submission:**
```csharp
namespace DeNelle.Core.Data
{
    [CreateAssetMenu(menuName = "Defenders/Tower Data", fileName = "TowerData")]
    public class TowerData : ScriptableObject { ... }
}
```

**Shared types — extract to own files in `DeNelle.Core.Data` before anything else:**
```
SkillTypes.cs     → enum SkillType { None, Blacksmith, Woodworking, Arcane }
                    [Serializable] class SkillRequirement { SkillType type; int minLevel; }
SpecialAbility.cs → enum SpecialAbility { None, SlowEnemies, HealAllies, FireAura, FrostNova, MagicalAffinity }
```

**TowerPlacementSystem.cs — required corrections from DEF-73 CP1:**
```csharp
namespace DeNelle.Village
{
    public class TowerPlacementSystem : MonoBehaviour
    {
        // Singleton:
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }
        private void OnDestroy() { if (Instance == this) Instance = null; }

        // Cache in Start() — NOT Awake (Camera isn't ready):
        private Camera _mainCamera;
        private Renderer _markerRenderer;
        private MaterialPropertyBlock _markerMPB;

        private void Start() { _mainCamera = Camera.main; }

        // After marker instantiated in StartPlacingTower():
        // _markerRenderer = _currentMarker.GetComponentInChildren<Renderer>();
        // _markerMPB = new MaterialPropertyBlock();

        // In Update():
        // Ray ray = _mainCamera.ScreenPointToRay(Input.mousePosition);
        // _markerMPB.SetColor("_BaseColor", valid ? Color.green : Color.red);
        // _markerRenderer.SetPropertyBlock(_markerMPB);

        // In CanPlaceTower() — pre-allocated buffer, cached layer:
        private readonly Collider[] _overlapBuffer = new Collider[16];
        private int _towerBuildingLayer; // = LayerMask.GetMask("Tower","Building") in Awake
        // int count = Physics.OverlapSphereNonAlloc(pos, 1.8f, _overlapBuffer, _towerBuildingLayer);
    }
}
```

**Tower.cs — required pattern from DEF-74 CP1:**
```csharp
namespace DeNelle.Village
{
    public class Tower : MonoBehaviour
    {
        private TowerData _data; // NOT [SerializeField] — injected via Initialize()

        public void Initialize(TowerData data)
        {
            _data = data;
            ApplyVisualForLevel(_currentLevel);
        }

        private void ApplyVisualForLevel(int level)
        {
            if (level < 1 || level > _data.upgrades.Length) return; // bounds check
            // ...
        }
    }
}
```

**After `Instantiate(prefab)` — always call Initialize:**
```csharp
GameObject towerObj = Instantiate(data.prefab, position, Quaternion.identity);
Tower tower = towerObj.GetComponent<Tower>();
if (tower != null) tower.Initialize(data);
TowerConstruction construction = towerObj.GetComponent<TowerConstruction>(); // baked in prefab
if (construction != null) construction.StartConstruction(data);
```

**TowerConstruction.cs — `SetActive` ordering fix:**
```csharp
// Use dedicated field — do NOT iterate all children:
[SerializeField] private GameObject _finalTowerVisual;

// StartConstruction: hide only the final visual
if (_finalTowerVisual) _finalTowerVisual.SetActive(false);

// CompleteConstruction: show only the final visual
if (_finalTowerVisual) _finalTowerVisual.SetActive(true);
```

**TowerConstructionQueue.cs — deadlock guard:**
```csharp
private IEnumerator BuildTowerRoutine(TowerQueueItem item)
{
    try { /* build logic */ }
    finally { _isBuilding = false; ProcessQueue(); }
}
```

**SkillSystem.cs — namespace + encapsulation:**
```csharp
namespace DeNelle.Core.Progression
{
    public class SkillSystem : MonoBehaviour
    {
        [SerializeField] private int _blacksmithLevel  = 1;
        [SerializeField] private int _woodworkingLevel = 1;
        [SerializeField] private int _arcaneLevel      = 1;
        public int BlacksmithLevel  => _blacksmithLevel;
        public int WoodworkingLevel => _woodworkingLevel;
        public int ArcaneLevel      => _arcaneLevel;
    }
}
```

---

## GROUP 1 — Active Correction Loops

### DEF-59 — Wave Scaling & Difficulty (Correction Pass 6)

**Submit as a DELTA. Fix these 3 issues only. Touch nothing else.**

**Issue 1 — Deprecated FindObjectOfType in `Awake()`**
```csharp
// WRONG:
_heroProgression = FindObjectOfType<HeroProgression>();

// CORRECT:
var found = FindObjectsByType<HeroProgression>(FindObjectsSortMode.None);
_heroProgression = found.Length > 0 ? found[0] : null;
```

**Issue 2 — `CheckWaveComplete()` not called after `_spawningInProgress = false`**

Required `RunWave` coroutine (exact — do not deviate):
```csharp
private IEnumerator RunWave(WaveData wave, WaveDifficulty difficulty)
{
    _waveInProgress = true;
    _spawningInProgress = true;
    _activeEnemies = 0;
    yield return StartCoroutine(_spawner.StartWave(wave, this, difficulty));
    _spawningInProgress = false;
    CheckWaveComplete(); // catches fast-clear during spawn
}
```

Required `CheckWaveComplete` guard (exact):
```csharp
private void CheckWaveComplete()
{
    if (!_waveInProgress || _spawningInProgress || _activeEnemies > 0) return;
    _waveInProgress = false;
    OnWaveCompleted?.Invoke();
    // ... award XP, advance wave index, etc.
}
```

**Issue 3 — EnemyGroupSpawner still accepts `int waveNumber`**

Required signature (exact):
```csharp
public IEnumerator StartWave(WaveData wave, WaveManager waveManager, WaveDifficulty difficulty = default)
{
    _waveManager = waveManager;
    yield return StartCoroutine(SpawnGroupRoutine(wave.enemyGroups, difficulty));
}
```
The spawner receives a fully resolved `WaveData`. No lookup inside the spawner. No `_codex.GetWaveTemplate()`.

**WaveScalingCurve evaluation (clarified):**
```csharp
// Keys authored as normalized 0–1
float t = (waveNumber - 1) / Mathf.Max(1f, _maxWaves - 1f);
float scale = _waveScalingCurve.Evaluate(t);
```

**XP award (clarified):**
```csharp
_heroProgression.AddXP(xpAmount);  // int — do not change the formula from DEF-49
```

**Required group loop structure — DO NOT DROP EITHER YIELD:**
```csharp
foreach (var group in enemyGroups)
{
    for (int i = 0; i < group.count; i++)
    {
        SpawnEnemy(group);
        yield return new WaitForSeconds(group.spawnDelayBetweenEnemies);
    }
    yield return new WaitForSeconds(group.groupDelay); // outer — do not drop
}
```

---

### DEF-60 — Boss Wave System (Correction Pass 4)

**Submit as a DELTA to `BossIntroSequence`. Fix these 2 lines only.**

```csharp
// WRONG:
SmartMobileCamera cam = FindObjectOfType<SmartMobileCamera>();
BossHealthBar bossBar = FindObjectOfType<BossHealthBar>();

// CORRECT:
var cams = FindObjectsByType<SmartMobileCamera>(FindObjectsSortMode.None);
SmartMobileCamera cam = cams.Length > 0 ? cams[0] : null;
var bars = FindObjectsByType<BossHealthBar>(FindObjectsSortMode.None);
BossHealthBar bossBar = bars.Length > 0 ? bars[0] : null;
```

---

### DEF-64 — Wildlife Ambient System (Correction Pass 3)

**Submit as a DELTA per script. Fix all issues in one pass.**

#### BirdFlock.cs — 4 fixes

**B1 — Implement OverlapSphereNonAlloc (player detection):**
```csharp
// In Awake():
_playerLayer = LayerMask.GetMask("Player");
_detectionBuffer = new Collider[4];

// In throttled detection Update (every 0.3s):
int count = Physics.OverlapSphereNonAlloc(transform.position, _scatterRadius, _detectionBuffer, _playerLayer);
if (count > 0) StartScattering();
```

**B2 — Route birds through `_waypoints` instead of teleporting:**
```csharp
// _waypoints is declared but unused. Use it in StartScattering():
private IEnumerator ScatterRoutine()
{
    _state = FlockState.Scattering;
    foreach (var bird in _birds)
    {
        Vector3 target = _waypoints[Random.Range(0, _waypoints.Length)].position;
        StartCoroutine(MoveBirdTo(bird, target));
        yield return new WaitForSeconds(0.1f); // stagger per bird
    }
    yield return new WaitForSeconds(_scatterDuration);
    _state = FlockState.Returning;
}
```

**B3 — Declare `Transform[] _birds`:**
```csharp
[SerializeField] private Transform[] _birds;
```

**B4 — Add movement in `Returning` state:**
```csharp
// In Update(), Returning state:
case FlockState.Returning:
    bool allHome = true;
    foreach (var bird in _birds)
    {
        bird.position = Vector3.MoveTowards(bird.position, _origin, _returnSpeed * Time.deltaTime);
        if ((bird.position - _origin).sqrMagnitude > 0.25f) allHome = false;
    }
    if (allHome) _state = FlockState.Idle;
    break;
```

#### ButterflyAmbient.cs — 2 fixes

**C1 — Bob accumulates drift. Modulate target, not transform:**
```csharp
// WRONG:
transform.position += Vector3.up * bob * Time.deltaTime;

// CORRECT — before SmoothDamp:
_target.y += Mathf.Sin(Time.time * _bobSpeed) * _bobHeight;
transform.position = Vector3.SmoothDamp(transform.position, _target, ref _velocity, _smoothTime);
```

**C2 — Replace `Vector3.Distance()` with `sqrMagnitude`:**
```csharp
// WRONG:
if (Vector3.Distance(transform.position, _target) < 0.1f)

// CORRECT:
if ((transform.position - _target).sqrMagnitude < 0.01f)
```

#### RabbitAmbient.cs — 3 fixes

**R1 — Implement OverlapSphereNonAlloc:**
```csharp
// In Awake():
_playerLayer = LayerMask.GetMask("Player");
_detectionBuffer = new Collider[4];

// In throttled detection (every 0.4s):
int count = Physics.OverlapSphereNonAlloc(transform.position, _fleeRadius, _detectionBuffer, _playerLayer);
if (count > 0) _state = RabbitState.Fleeing;
```

**R2 — Flee movement out of throttled block into regular Update:**
```csharp
// Detection is throttled. Movement is not.
private void Update()
{
    _detectionTimer -= Time.deltaTime;
    if (_detectionTimer <= 0f)
    {
        _detectionTimer = _detectionInterval; // 0.4s
        RunDetection(); // OverlapSphereNonAlloc only
    }

    // Movement runs every frame regardless
    switch (_state)
    {
        case RabbitState.Fleeing:
            transform.position = Vector3.MoveTowards(
                transform.position,
                _fleeTarget,
                _fleeSpeed * Time.deltaTime);
            break;
        case RabbitState.Idle:
            ReturnToOrigin();
            break;
    }
}
```

**R3 — Return to origin in Idle state:**
```csharp
private void ReturnToOrigin()
{
    if ((transform.position - _origin).sqrMagnitude < 0.04f) return;
    transform.position = Vector3.MoveTowards(
        transform.position, _origin, _wanderSpeed * Time.deltaTime);
}
```

---

### DEF-65 — Smart Mobile Camera (Correction Pass 2)

**Submit as a DELTA to `SmartMobileCamera.cs`. Touch only the items below.**

**Step 1 — Add missing field declarations:**
```csharp
private bool _isInDefendMode;
[SerializeField] private Vector3 _towerDefenseOffset = new Vector3(0f, 8f, -12f);
[SerializeField] private Vector3 _explorationOffset  = new Vector3(0f, 5f, -8f);
```

**Step 2 — Add `CameraMode.Defend` to enum:**
```csharp
public enum CameraMode { Exploration, Defend }
```

**Step 3 — Add `SetDefendMode(bool)`:**
```csharp
public void SetDefendMode(bool active)
{
    _isInDefendMode = active;
    // Wider offset, higher pullback scaled by enemy count, slight tilt
    if (active)
    {
        var enemies = FindObjectsByType<EnemyBrain>(FindObjectsSortMode.None);
        float pullback = Mathf.Clamp(enemies.Length * 0.5f, 0f, 6f);
        _activeOffset = _towerDefenseOffset + Vector3.back * pullback;
        _currentMode  = CameraMode.Defend;
    }
    else
    {
        _activeOffset = _explorationOffset;
        _currentMode  = CameraMode.Exploration;
    }
}
```

**Step 4 — Fix shake coroutine (`unscaledDeltaTime` + correct falloff):**
```csharp
private IEnumerator ShakeRoutine(float intensity, float duration)
{
    float elapsed = 0f;
    while (elapsed < duration)
    {
        float t = 1f - (elapsed / duration);                        // 1→0 falloff
        _shakeOffset = Random.insideUnitSphere * intensity * t;
        elapsed += Time.unscaledDeltaTime;                          // safe during hit-stop
        yield return null;
    }
    _shakeOffset = Vector3.zero;
}
```

**Step 5 — Subscribe to WaveManager events in `OnEnable`/`OnDisable`:**
```csharp
private void OnEnable()
{
    if (WaveManager.Instance != null)
    {
        WaveManager.Instance.OnWaveStarted.AddListener(OnWaveStarted);
        WaveManager.Instance.OnWaveCompleted.AddListener(OnWaveCompleted);
    }
}

private void OnDisable()
{
    if (WaveManager.Instance != null)
    {
        WaveManager.Instance.OnWaveStarted.RemoveListener(OnWaveStarted);
        WaveManager.Instance.OnWaveCompleted.RemoveListener(OnWaveCompleted);
    }
}

private void OnWaveStarted()  => SetDefendMode(true);
private void OnWaveCompleted() => SetDefendMode(false);
```

---

### DEF-66 — Tower Damage Visuals (Correction Pass 1)

**2 changes only — do not touch any logic.**

1. Rename class: `TowerDamageStateManager` → `TowerDamageVisuals`
2. Add namespace: `namespace DeNelle.Village`

The `OnEnable`/`OnDisable` subscription, `UpdateVisualState`, stage switching, and VFX emission toggle are all correct as submitted.

---

### DEF-72 — Advanced Enemy AI (Correction Pass 1)

**Full resubmission required. 8 issues. Submit all 4 files together.**

#### EnemyBrain.cs delta

**Required `Awake()` — all GetComponent caching here:**
```csharp
private void Awake()
{
    _agent    = GetComponent<NavMeshAgent>();
    _animator = GetComponent<Animator>();
    _rb       = GetComponent<Rigidbody>();
    _enemyLayer = LayerMask.GetMask("Enemy");
}
```

**Required Initialize signature — non-virtual, 7 params, no legacy overload:**
```csharp
public void Initialize(
    EnemyType data,
    Transform target,
    HeartOfTown heart        = null,
    WaveManager waveManager  = null,
    WaveDifficulty difficulty = default,
    BossWaveConfig bossConfig = null,
    TacticalData tacticalData = null)
{
    _data         = data;
    _target       = target;
    _heart        = heart;
    _waveManager  = waveManager;
    _difficulty   = difficulty;
    _bossConfig   = bossConfig;
    _tacticalData = tacticalData;   // local variable, not a serialized field

    _maxHealth = data.baseHealth * difficulty.healthMultiplier;
    _currentHealth = _maxHealth;
    _baseSpeed = data.moveSpeed;

    if (_agent != null)
    {
        _agent.speed = _baseSpeed;
        _agent.stoppingDistance = data.stoppingDistance;
    }
}
```

**All fields must be `private`:**
```csharp
private NavMeshAgent _agent;
private Animator _animator;
private Rigidbody _rb;
private float _baseSpeed;
private float _maxHealth;
private float _currentHealth;
private EnemyTacticalState _tacticalState = EnemyTacticalState.Idle;
// etc.
```

No `[SerializeField] private TacticalData _tacticalData` — TacticalData comes in via Initialize, stored in a local private field.

#### TacticalData.cs (new file — `DeNelle.Core.Data` namespace)

```csharp
using UnityEngine;

namespace DeNelle.Core.Data
{
    [CreateAssetMenu(menuName = "Defenders/TacticalData", fileName = "TacticalData")]
    public class TacticalData : ScriptableObject
    {
        public EnemyArchetype archetype    = EnemyArchetype.Flanker;
        public float flankDistance         = 14f;
        public float diveHeight            = 22f;
        public float retreatHealthThreshold = 0.2f;
    }
}
```

#### Enums (new file — correct namespaces)

```csharp
// In DeNelle.Village.Enemies:
public enum EnemyTacticalState { Idle, Rush, Flank, Retreat, Suppressed }

// In DeNelle.Core.Data (alongside TacticalData):
public enum EnemyArchetype { Flanker, SiegeUnit, Flyer, SupportUnit, Boss }
```

#### EnemyGroupCoordinator.cs (new file — `DeNelle.Village.Enemies`)

```csharp
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DeNelle.Village.Enemies
{
    public class EnemyGroupCoordinator : MonoBehaviour
    {
        [SerializeField] private float _staggerDelay = 0.15f;

        private readonly List<EnemyBrain> _group = new();

        public void RegisterEnemy(EnemyBrain brain) => _group.Add(brain);

        public void StartGroupAttack() => StartCoroutine(StaggerAttack());

        private IEnumerator StaggerAttack()
        {
            foreach (var brain in _group)
            {
                if (brain != null) brain.ReceiveAttackSignal();
                yield return new WaitForSeconds(_staggerDelay);
            }
        }
    }
}
```

`EnemyBrain` needs a corresponding method:
```csharp
public void ReceiveAttackSignal()
{
    if (_tacticalState == EnemyTacticalState.Idle)
        _tacticalState = EnemyTacticalState.Rush;
}
```

**Flyer DiveBomb — physics arc, not NavMesh:**
```csharp
private IEnumerator DiveBombRoutine()
{
    // Phase 1: climb to dive height
    Vector3 highPoint = transform.position + Vector3.up * (_tacticalData?.diveHeight ?? 22f);
    yield return MoveToPoint(highPoint, _flySpeed);

    // Phase 2: swoop to tower top
    Vector3 swoopTarget = _heart != null
        ? _heart.transform.position + Vector3.up * 2f
        : _target.position;

    yield return MoveToPoint(swoopTarget, _diveSpeed);

    // Impact
    if (_heart != null) _heart.TakeDamage(_data.attackDamage);
    Destroy(gameObject);
}

private IEnumerator MoveToPoint(Vector3 destination, float speed)
{
    while ((transform.position - destination).sqrMagnitude > 0.25f)
    {
        transform.position = Vector3.MoveTowards(
            transform.position, destination, speed * Time.deltaTime);
        yield return null;
    }
}
```

Flyers should have their `NavMeshAgent` disabled on `Initialize()` when `archetype == EnemyArchetype.Flyer`:
```csharp
if (tacticalData != null && tacticalData.archetype == EnemyArchetype.Flyer)
    _agent.enabled = false;
```

---

## GROUP 2 — World Expansion (implement in order — each blocks the next)

### DEF-61 — World Terrain Foundation
- 512×512m Unity Terrain
- 3 terrain layers: grass (albedo + normal), dirt path, rock face — Poly Haven CC0 assets
- Zone layout: village center ~40×40m cleared, winding path SE, open field NW, forest treeline S/W
- Fog: `RenderSettings.fogMode = FogMode.ExponentialSquared`, density `0.008`, color `#7A9BAF`
- Skybox: URP gradient — horizon `#E8D5B0`, zenith `#4A7FA5`
- Pure scene setup. No scripts.

### DEF-62 — Nature & Environment *(blocked by DEF-61)*
- Assets: Quaternius Fantasy Forest, Low Poly Nature, Ultimate Rocks (all free)
- Unity Terrain Tree system: 3 species (broadleaf, pine, dead oak), billboard LOD at 80m
- Detail layer: URP Mesh type only — no Billboard detail (URP incompatible)
- Mountain backdrop: 250m+, no colliders, scale 3–5×
- Grass coverage: 60% open field, 15% near path

### DEF-63 — Points of Interest *(blocked by DEF-61 + DEF-62)*
Each POI is a prefab + trigger collider + `PointOfInterest.cs` (fields: `string poiName`, `string description`, `float discoveryRadius`).

| POI | Location | Notes |
|-----|----------|-------|
| Abandoned Ruins | ~100m SE | Crumbling walls, no roof |
| Stone Shrine | ~180m elevated | Pedestal, ambient particle light |
| Hunter's Camp | ~70m forest edge | Tent + fire pit particle |
| Broken Bridge | Mid-map river | Broken arch, no NavMesh crossing |
| Cave Entrance | ~220m N elevated | `// TODO: DEF-?? AddressableDungeonPortal hook` |

---

## GROUP 3 — Systems (order matters where noted)

### DEF-67 — Audio & VFX Layering

**WaveMusicController** (`DeNelle.Village`):
```csharp
// A/B crossfade using unscaledDeltaTime
// Subscribe to WaveManager.Instance.OnWaveStarted / OnWaveCompleted in OnEnable/OnDisable
[SerializeField] private AudioClip _explorationTrack;   // assign in Inspector later
[SerializeField] private AudioClip _combatTrack;        // assign in Inspector later
```

**TowerAudioController** — event-driven only, no `Update()` polling.

**TowerVoiceController**:
```csharp
// Subscribe to HeartOfTown.OnHealthChanged in OnEnable/OnDisable
// Fire once per session — self-unsubscribe after firing
[SerializeField] private AudioClip[] _voiceLines;  // assign in Inspector later

private bool _voiceFired;

private void OnHealthChanged(float pct)
{
    if (_voiceFired || pct > 0.3f) return;
    _voiceFired = true;
    // Play random clip
    _audioSource.PlayOneShot(_voiceLines[Random.Range(0, _voiceLines.Length)]);
    // Self-unsubscribe
    _heart.OnHealthChanged -= OnHealthChanged;
}
```

Screen shake: `SmartMobileCamera.Instance?.Shake(intensity, duration)` — never direct camera manipulation.

---

### DEF-68 — Spire Chronicles Campaign

**MissionData SO** (`DeNelle.Core.Data`):
```csharp
[CreateAssetMenu(menuName = "Defenders/MissionData", fileName = "MissionData")]
public class MissionData : ScriptableObject
{
    public string missionName;
    public int waveGoal;                    // waves to survive
    public float maxTowerDamageAllowed;     // 0–1, fraction of max HP
    public string specialModifier;          // e.g. "no_archer", "double_spawn"
}
```

**CampaignData SO** — list of `MissionData` in sequence.

**CampaignProgressRecord** — plain `[Serializable]` (no persistence, in-memory only):
```csharp
[System.Serializable]
public class CampaignProgressRecord
{
    public int currentMissionIndex;
    public bool[] missionCompleted;
}
```

**CampaignManager** singleton:
- Hooks `WaveManager.Instance.OnAllWavesCleared`
- On complete: `_progress.missionCompleted[current] = true; current++` (auto-unlock next)

---

### DEF-69 — Monetization Framework

**BattlePassData SO** — two parallel tracks, same tier count:
```csharp
[CreateAssetMenu(menuName = "Defenders/BattlePassData", fileName = "BattlePassData")]
public class BattlePassData : ScriptableObject
{
    public int tierCount = 30;
    public BattlePassReward[] freeTrack;     // length == tierCount
    public BattlePassReward[] premiumTrack;  // length == tierCount
}
```

**RewardedAdManager**:
- `virtual ShowAdInternal()` for platform override (stub — no SDK)
- Cooldown: `480f` seconds (`8 minutes`) via `Time.realtimeSinceStartup`

---

### DEF-70 — Hero Animation Upgrades

Animator Controller exists. Use it. Do not create a new one.

**All hashes — cache as static readonly:**
```csharp
private static readonly int AimIKWeightHash = Animator.StringToHash("AimIKWeight");
private static readonly int ChargeHash      = Animator.StringToHash("Charge");
private static readonly int VictoryHash     = Animator.StringToHash("Victory");
private static readonly int BowRecoilHash   = Animator.StringToHash("BowRecoil");
```

**HeroAimIK** — `OnAnimatorIK` callback, upper body Avatar Mask on Animator layer 1.

**HeroChargeVFX** — `StartCharge()` / `ReleaseCharge()` public methods.

**Victory pose** — subscribe to `WaveManager.OnWaveCompleted` in `OnEnable`, reset (call `OnWaveStarted`) on `WaveManager.OnWaveStarted`.

---

### DEF-71 — Pet Contextual Animations

`PetType` is an existing ScriptableObject. Do not redefine.

**PetContextualBehaviour** subscribes (in `OnEnable`/`OnDisable`) to:
- `WaveManager.OnBossSpawned`
- `WaveManager.OnWaveCompleted`
- `HeartOfTown.OnHealthChanged`

`_whimperPlayed` bool guard — one-shot per session; reset on `WaveManager.OnWaveStarted`.

**TowerRepairVisuals**:
- Compare `_previousHealthPct` each frame to detect heal (current > previous)
- Shimmer via `MaterialPropertyBlock` — animate `_EmissionColor`, never `SetActive()`

```csharp
private IEnumerator ShimmerRoutine()
{
    float elapsed = 0f;
    while (elapsed < _shimmerDuration)
    {
        float t = Mathf.PingPong(elapsed * _shimmerSpeed, 1f);
        _mpb.SetColor("_EmissionColor", Color.Lerp(Color.black, _shimmerColor, t));
        _renderer.SetPropertyBlock(_mpb);
        elapsed += Time.deltaTime;
        yield return null;
    }
    _mpb.SetColor("_EmissionColor", Color.black);
    _renderer.SetPropertyBlock(_mpb);
}
```

---

## GROUP 4 — Deferred (do not start tonight)

- Save/Load system (HeroProgression, TalentTree, UnlockRegistry)
- HeroCombat (`OnAttackHit()` entry point)
- HeroInputHandler (`SetMovementInput()` entry point)
- Full pet system (PetType SO, pet spawning pipeline)
- Daily challenge system

---

## Correction Loop Protocol

1. Read the full Linear correction comment — every numbered issue.
2. Fix **all** issues in a **single** resubmission. Do not fix one and resubmit.
3. Do not modify behaviour outside the flagged items.
4. Post a reply comment listing each issue number and confirming it is resolved.
5. If a fix requires changing a file owned by another ticket, file a new Linear issue — do not silently modify it.

---

## Recurring Gotchas

**`EnemyGroupSpawner` receives `WaveData`, not `int`.** Never look up wave data inside the spawner. Receive it resolved.

**Both group-loop yields are required.** `spawnDelayBetweenEnemies` (inner) AND `groupDelay` (outer). Dropping either breaks wave pacing.

**`_spawningInProgress` deadlock.** Clear it in `RunWave` coroutine after the spawner yield, then call `CheckWaveComplete()`. If cleared only inside `CompleteCurrentWave()` you get a circular deadlock.

**`transform.localScale` — explicit assignment only.** `transform.localScale = Vector3.one * multiplier`. Never `*=`.

**`Time.deltaTime` in shake is always wrong.** Every shake coroutine uses `Time.unscaledDeltaTime`.

**Bob via `transform.position +=` accumulates drift.** Always modulate the target vector before SmoothDamp.

---

## Linear Quick Reference

| Ticket | Title | Tonight |
|--------|-------|---------|
| [DEF-59](https://linear.app/defenders-of-the-realm/issue/DEF-59) | Wave Scaling | Fix 3 issues (Group 1) |
| [DEF-60](https://linear.app/defenders-of-the-realm/issue/DEF-60) | Boss Wave System | Fix 2 issues (Group 1) |
| [DEF-61](https://linear.app/defenders-of-the-realm/issue/DEF-61) | World Terrain | Implement (Group 2) |
| [DEF-62](https://linear.app/defenders-of-the-realm/issue/DEF-62) | Nature & Environment | Implement after DEF-61 |
| [DEF-63](https://linear.app/defenders-of-the-realm/issue/DEF-63) | Points of Interest | Implement after DEF-62 |
| [DEF-64](https://linear.app/defenders-of-the-realm/issue/DEF-64) | Wildlife Ambient | Fix 9 issues (Group 1) |
| [DEF-65](https://linear.app/defenders-of-the-realm/issue/DEF-65) | Smart Mobile Camera | Fix 5 issues (Group 1) |
| [DEF-66](https://linear.app/defenders-of-the-realm/issue/DEF-66) | Tower Damage Visuals | Fix 2 issues (Group 1) |
| [DEF-67](https://linear.app/defenders-of-the-realm/issue/DEF-67) | Audio & VFX | Implement (Group 3) |
| [DEF-68](https://linear.app/defenders-of-the-realm/issue/DEF-68) | Campaign | Implement (Group 3) |
| [DEF-69](https://linear.app/defenders-of-the-realm/issue/DEF-69) | Monetization | Implement (Group 3) |
| [DEF-70](https://linear.app/defenders-of-the-realm/issue/DEF-70) | Hero Animations | Implement (Group 3) |
| [DEF-71](https://linear.app/defenders-of-the-realm/issue/DEF-71) | Pet Animations | Implement (Group 3) |
| [DEF-72](https://linear.app/defenders-of-the-realm/issue/DEF-72) | Advanced Enemy AI | Full resubmission (Group 1) |

---

*Defenders of the Realm — CC overnight brief — 2026-05-26*
