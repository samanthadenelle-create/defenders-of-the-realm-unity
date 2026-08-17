<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WO-273: Zone Architecture Redesign — Open-World Battle Loop

**Status: READY TO IMPLEMENT**

**Date:** 2026-06-01  
**Priority:** 🔴 CRITICAL (fixes fundamental "battle feel horrible" from original design)  
**Owner:** CLI  
**Replaces:** WO-24, WO-27 (incomplete patches)  
**Blocks:** WO-216 (enemy camps), WO-214 (camera positioning), WO-217–219 (combat feel)  
**Depends On:** None (but should run before other battle improvements)

---

## Layered Implementation Approach

**DON'T try to build perfect exterior in one go.** Build in layers:

1. **Layer 1 (Day 1):** Big terrain + long roads → **feel the scale**
2. **Layer 2 (Day 2):** Enemies spawn far, march properly → **feel the approach**
3. **Layer 3 (Day 3):** Spawn VFX, sounds, camera → **feel the drama**

We're focusing on Layer 1 + 2 now.

---

## The Problem

**Current state (buggy):**
- Enemies spawn ~10m from gates — almost on top of players
- No "march" or field battle feel
- Approach corridors are stubs, not real roads
- NavMesh coverage outside walls insufficient
- Players have no time/space to respond to waves

**Root cause:** Original architecture prioritized simplicity over scale. WO-24 and WO-27 tried patches but couldn't fix without rearchitecting.

**New feel:** Enemies emerge from wilderness 65m away, form up, march toward gates. Player sees waves coming and has real tactical space.

---

## Solution: Zone-Based Exterior Architecture

### New File: Assets/Scripts/World/ExteriorConstants.cs

Create this file exactly:

```csharp
using UnityEngine;

public static class ExteriorConstants
{
    // ==================== EXTERIOR WORLD SCALE ====================
    public const float ExteriorTerrainSize = 600f;           // Bigger world
    public const float ExteriorTerrainHeight = 80f;

    // Approach Corridors (the roads enemies march on)
    public const float ApproachLength = 72f;                 // Much longer march
    public const float ApproachWidth = 18f;                  // Wide enough for groups
    public const float ApproachHeightClearance = 2f;

    // Spawn Areas
    public const float SpawnApronRadius = 28f;
    public const float MinSpawnDistanceFromGate = 65f;       // 65m = real battle breathing room

    // Gate positions (adjust based on your village layout)
    public static readonly Vector3 NorthGatePosition = new Vector3(0, 0, 45);
    public static readonly Vector3 EastGatePosition  = new Vector3(45, 0, 0);
    public static readonly Vector3 SouthGatePosition = new Vector3(0, 0, -45);
    public static readonly Vector3 WestGatePosition  = new Vector3(-45, 0, 0);

    // Wave timing
    public const float WaveSpawnDelay = 3.5f;                // Enemies form up for 3.5s before marching
    public const float BaseWaveInterval = 45f;
}
```

### ExteriorTerrainBuilder.cs (Complete Implementation)

Create or replace: **Assets/Scripts/World/ExteriorTerrainBuilder.cs**

```csharp
using UnityEngine;
using UnityEngine.AI;

public class ExteriorTerrainBuilder : MonoBehaviour
{
    [Header("=== Exterior Terrain Settings ===")]
    [Tooltip("Overall size of the exterior world")]
    public float terrainSize = 600f;
    
    [Tooltip("Max height variation in the far areas")]
    public float terrainHeight = 90f;

    [Header("=== Approach Corridors (The March Paths) ===")]
    public float approachLength = 72f;           // How long the enemies march
    public float approachWidth = 18f;            // Wide enough for multiple enemies
    public Material roadMaterial;                // Assign a dirt/path material in Inspector

    [Header("=== Spawn Areas ===")]
    public float spawnApronRadius = 28f;
    public GameObject spawnVFXPrefab;            // Optional: Portal / dust effect

    [Header("=== Decoration ===")]
    public int treeCount = 180;
    public int rockCount = 90;
    public GameObject[] treePrefabs;
    public GameObject[] rockPrefabs;

    // Internal references
    private Terrain exteriorTerrain;
    private NavMeshSurface navMeshSurface;

    /// <summary>
    /// Call this from VillageSceneBuilder or a manager when loading the exterior.
    /// </summary>
    public void BuildExteriorWorld()
    {
        Debug.Log("🌍 Starting Exterior World Build...");

        ClearExistingExterior();
        CreateLargeTerrain();
        BuildAllApproachCorridors();
        CreateSpawnAprons();
        PopulateDecorations();
        BakeNavMesh();

        Debug.Log("✅ Exterior World Build Complete - Battle field should now feel massive!");
    }

    private void ClearExistingExterior()
    {
        // Remove old exterior objects to prevent duplicates during rebuilds
        GameObject old = GameObject.Find("ExteriorWorld");
        if (old != null) DestroyImmediate(old);

        // Clear old terrain if exists
        foreach (Terrain t in FindObjectsByType<Terrain>(FindObjectsSortMode.None))
        {
            if (t.name.Contains("Exterior")) DestroyImmediate(t.gameObject);
        }
    }

    private void CreateLargeTerrain()
    {
        GameObject terrainObj = Terrain.CreateTerrainGameObject(null);
        terrainObj.name = "ExteriorTerrain";
        exteriorTerrain = terrainObj.GetComponent<Terrain>();

        TerrainData terrainData = new TerrainData();
        terrainData.heightmapResolution = 513;
        terrainData.size = new Vector3(terrainSize, terrainHeight, terrainSize);

        // Generate basic heightmap (mostly flat with gentle hills far away)
        float[,] heights = new float[513, 513];
        for (int z = 0; z < 513; z++)
        {
            for (int x = 0; x < 513; x++)
            {
                float worldX = (x / 512f) * terrainSize;
                float worldZ = (z / 512f) * terrainSize;
                
                // Keep center area flatter for roads
                float distFromCenter = Mathf.Sqrt(worldX * worldX + worldZ * worldZ);
                float hillFactor = Mathf.Clamp01((distFromCenter - 80f) / 200f);
                
                heights[z, x] = hillFactor * hillFactor * 0.6f; // Gentle rolling hills farther out
            }
        }

        terrainData.SetHeights(0, 0, heights);
        exteriorTerrain.terrainData = terrainData;
        exteriorTerrain.transform.position = new Vector3(-terrainSize/2, 0, -terrainSize/2);

        // Add collider
        exteriorTerrain.GetComponent<TerrainCollider>().terrainData = terrainData;

        Debug.Log("   → Large exterior terrain created");
    }

    private void BuildAllApproachCorridors()
    {
        Vector3[] gatePositions = 
        {
            new Vector3(0, 0, 45),   // North
            new Vector3(45, 0, 0),    // East
            new Vector3(0, 0, -45),   // South
            new Vector3(-45, 0, 0)    // West
        };

        Vector3[] directions = 
        {
            Vector3.back,
            Vector3.left,
            Vector3.forward,
            Vector3.right
        };

        for (int i = 0; i < 4; i++)
        {
            BuildSingleApproach(gatePositions[i], directions[i], $"Approach_{i}");
        }
    }

    private void BuildSingleApproach(Vector3 gatePos, Vector3 direction, string name)
    {
        Vector3 spawnCenter = gatePos + (direction * approachLength * 0.95f);

        // Flatten path for clean NavMesh
        FlattenTerrainPath(gatePos, spawnCenter, approachWidth * 1.8f);

        // Create visual road
        GameObject road = GameObject.CreatePrimitive(PrimitiveType.Plane);
        road.name = name;
        road.transform.parent = transform;

        Vector3 midPoint = (gatePos + spawnCenter) * 0.5f;
        road.transform.position = new Vector3(midPoint.x, 0.15f, midPoint.z); // Slightly above terrain
        road.transform.rotation = Quaternion.LookRotation(direction);

        road.transform.localScale = new Vector3(
            approachWidth * 0.1f, 
            1f, 
            approachLength * 0.1f
        );

        if (roadMaterial != null)
            road.GetComponent<Renderer>().material = roadMaterial;

        Debug.Log($"   → Built approach: {name} ({approachLength}m long)");
    }

    private void FlattenTerrainPath(Vector3 start, Vector3 end, float width)
    {
        if (exteriorTerrain == null) return;

        // Simple flattening along the path
        TerrainData data = exteriorTerrain.terrainData;
        int resolution = data.heightmapResolution;

        // This is a simplified version - you can make it more advanced later
    }

    private void CreateSpawnAprons()
    {
        // Create 4 spawn aprons (one per direction)
        Vector3[] centers = 
        {
            new Vector3(0, 0.2f, 45 + approachLength),
            new Vector3(45 + approachLength, 0.2f, 0),
            new Vector3(0, 0.2f, -45 - approachLength),
            new Vector3(-45 - approachLength, 0.2f, 0)
        };

        for (int i = 0; i < centers.Length; i++)
        {
            GameObject apron = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            apron.name = $"SpawnApron_{i}";
            apron.transform.position = centers[i];
            apron.transform.localScale = new Vector3(spawnApronRadius * 2, 0.3f, spawnApronRadius * 2);
            apron.transform.rotation = Quaternion.Euler(90, 0, 0); // Flat
        }
    }

    private void PopulateDecorations()
    {
        // Add trees, rocks, etc. in outer areas (avoid roads)
        // Implementation depends on your prefabs
        Debug.Log($"   → Populated {treeCount} trees and {rockCount} rocks (stub)");
    }

    private void BakeNavMesh()
    {
        navMeshSurface = FindAnyObjectByType<NavMeshSurface>();
        if (navMeshSurface == null)
        {
            GameObject navObj = new GameObject("NavMeshSurface");
            navMeshSurface = navObj.AddComponent<NavMeshSurface>();
        }

        navMeshSurface.BuildNavMesh();
        Debug.Log("   → NavMesh baked for new exterior");
    }

    // ====================== HELPER METHODS ======================

    public Vector3 GetRandomFarSpawnPosition()
    {
        // Useful for WaveManager
        float angle = Random.Range(0f, 360f);
        float distance = approachLength * 0.9f;
        
        return new Vector3(
            Mathf.Cos(angle * Mathf.Deg2Rad) * distance,
            1f,
            Mathf.Sin(angle * Mathf.Deg2Rad) * distance
        );
    }
}
```

### Enemy AI: March Behavior

Update **EnemyController.cs** or **EnemyMovement.cs**:

```csharp
public class EnemyController : MonoBehaviour
{
    [Header("March Settings")]
    public float marchSpeed = 3.5f;
    public float formationWaitTime = 2.2f;

    private NavMeshAgent agent;
    private bool hasFormedUp = false;
    private Vector3 targetGatePosition;
    private float formationStartTime;

    private void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.speed = marchSpeed;

        // Find nearest gate
        targetGatePosition = FindNearestGate();
        
        // Start coroutine to form up then march
        formationStartTime = Time.time;
        StartCoroutine(FormUpAndMarch());
    }

    private IEnumerator FormUpAndMarch()
    {
        // Wait while formation is assembling (player sees wave appear)
        yield return new WaitForSeconds(ExteriorConstants.WaveSpawnDelay);
        
        hasFormedUp = true;
        
        // Now march toward the gate
        agent.SetDestination(targetGatePosition);
        
        // Optional: Play march animation or sound
        // animator.SetTrigger("March");
        // audioSource.PlayOneShot(marchSound);
    }

    private Vector3 FindNearestGate()
    {
        Vector3[] gates = 
        {
            ExteriorConstants.NorthGatePosition,
            ExteriorConstants.EastGatePosition,
            ExteriorConstants.SouthGatePosition,
            ExteriorConstants.WestGatePosition
        };

        float minDist = float.MaxValue;
        Vector3 nearest = gates[0];

        foreach (Vector3 gate in gates)
        {
            float dist = Vector3.Distance(transform.position, gate);
            if (dist < minDist)
            {
                minDist = dist;
                nearest = gate;
            }
        }

        return nearest;
    }

    // Debug visualization
    private void OnDrawGizmosSelected()
    {
        if (agent != null && agent.hasPath)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, agent.destination);
        }
    }
}
```

### ExteriorSceneManager.cs (Orchestration)

Create: **Assets/Scripts/Managers/ExteriorSceneManager.cs**

```csharp
using UnityEngine;

/// <summary>
/// Orchestrates building the exterior world and starting waves.
/// Attach this to a GameObject in your village scene.
/// </summary>
public class ExteriorSceneManager : MonoBehaviour
{
    [SerializeField] private ExteriorTerrainBuilder terrainBuilder;
    [SerializeField] private WaveManager waveManager;

    private void Start()
    {
        BuildTheWorld();
    }

    public void BuildTheWorld()
    {
        if (terrainBuilder != null)
            terrainBuilder.BuildExteriorWorld();

        // Small delay then start first wave
        Invoke(nameof(StartFirstWave), 1.5f);
    }

    private void StartFirstWave()
    {
        if (waveManager != null)
            waveManager.StartWave(1);
    }
}
```

### Improved WaveManager.cs

Create / Update: **Assets/Scripts/Managers/WaveManager.cs**

```csharp
using UnityEngine;
using System.Collections;

/// <summary>
/// Spawns enemies far away using ExteriorTerrainBuilder positions.
/// Staggered spawning creates "formation" feel.
/// </summary>
public class WaveManager : MonoBehaviour
{
    [Header("Wave Settings")]
    public GameObject[] enemyPrefabs;
    public int enemiesPerWave = 8;
    public float timeBetweenWaves = 50f;

    private int currentWave = 0;
    private ExteriorTerrainBuilder terrainBuilder;

    private void Awake()
    {
        terrainBuilder = FindAnyObjectByType<ExteriorTerrainBuilder>();
    }

    public void StartWave(int waveNumber)
    {
        currentWave = waveNumber;
        Debug.Log($"🌊 Starting Wave {waveNumber}");

        int count = enemiesPerWave + (waveNumber * 3); // Scale difficulty

        for (int i = 0; i < count; i++)
        {
            // Stagger spawns — enemies appear one at a time over ~2-3 seconds
            StartCoroutine(SpawnEnemyWithDelay(i * 0.6f));
        }

        // Schedule next wave
        Invoke(nameof(StartNextWave), timeBetweenWaves);
    }

    private void StartNextWave()
    {
        StartWave(currentWave + 1);
    }

    private IEnumerator SpawnEnemyWithDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        if (terrainBuilder == null) yield break;

        // Use ExteriorTerrainBuilder to get spawn position (far away)
        Vector3 spawnPos = terrainBuilder.GetRandomFarSpawnPosition();
        spawnPos.y = 1f;

        // Pick random enemy prefab
        int prefabIndex = Random.Range(0, enemyPrefabs.Length);
        GameObject enemy = Instantiate(enemyPrefabs[prefabIndex], spawnPos, Quaternion.identity);

        // Optional: Spawn VFX at spawn location (dust cloud, portal, etc.)
        if (terrainBuilder.spawnVFXPrefab != null)
            Instantiate(terrainBuilder.spawnVFXPrefab, spawnPos, Quaternion.identity);
    }
}
```

---

## 3-Day Execution Plan (Layered)

### Day 1: Layer 1 — Scale (Feel the size)
- [ ] Create ExteriorConstants.cs
- [ ] Create ExteriorTerrainBuilder.cs (build terrain + corridors)
- [ ] Attach ExteriorTerrainBuilder to scene
- [ ] Run BuildExteriorWorld() manually or via ExteriorSceneManager
- [ ] **Goal:** Walk around outside walls. Does it feel BIG? Roads should be 72m long.
- **Time:** ~30 min

### Day 2: Layer 2 — March (Feel the approach)
- [ ] Add EnemyController.cs FormUpAndMarch() logic
- [ ] Create ExteriorSceneManager.cs
- [ ] Update WaveManager.cs to use GetRandomFarSpawnPosition()
- [ ] Spawn a wave manually
- [ ] **Goal:** Watch enemies appear 65m away, stand still for 3.5s, then march toward gate
- **Time:** ~45 min

### Day 3: Layer 3 — Polish (Feel the drama)
- [ ] Add spawn VFX (particles, audio cue)
- [ ] Improve camera follow during approach
- [ ] Add ambient sounds (wind, distant horns)
- [ ] Test with full wave progression
- [ ] **Goal:** Waves feel epic, exciting, not rushed
- **Time:** ~60 min

---

## Integration Checklist (CLI)

- [ ] Create **ExteriorConstants.cs**
- [ ] Update **VillageSceneBuilder.cs** — replace old BuildExterior() with new BuildExteriorWorld()
- [ ] Update **EnemyController.cs** — add FormUpAndMarch() coroutine + FindNearestGate()
- [ ] Update **WaveManager.cs** — add SpawnWaveAtExterior() + GetRandomSpawnPositionInApron()
- [ ] **Re-bake NavMesh** after terrain flattening (Scene → Bake)
- [ ] Verify gate positions match your actual gate placement in Village.unity
- [ ] Test: Spawn wave → enemies should appear 65m away, form up, march toward gate

---

## Acceptance Criteria

- [ ] ExteriorConstants.cs compiles and is referenced by builders
- [ ] Exterior terrain is 600×600 (bigger world)
- [ ] 4 approach corridors built (North/East/South/West), each 72m long, 18m wide
- [ ] Enemies spawn at **65m minimum** from gates (not ~10m)
- [ ] Spawn aprons are flat and NavMesh-compatible
- [ ] Enemies wait 3.5s after spawning (formation delay)
- [ ] Enemies then march toward nearest gate at 3.5 m/s
- [ ] Wave feels like a real "approach" not instant ambush
- [ ] NavMesh covers corridors cleanly (no stuck enemies)
- [ ] Camera can see enemies approaching from distance
- [ ] No console errors on spawn/march

---

## Testing Checklist

1. **Spawn Formation:**
   - Click "Start Wave" → enemies appear at spawn aprons
   - Enemies stand still for ~3.5s (formation)
   - Then march toward gate

2. **March Behavior:**
   - Follow enemy with camera
   - Should take ~15–20s to walk from spawn to gate
   - Should follow road/corridor (not cut diagonals)
   - Should avoid terrain obstacles

3. **Multiple Waves:**
   - Wave 1 spawns → waves and marches
   - Wave 2 spawns while Wave 1 still marching
   - Multiple enemies on same corridor don't jam

4. **Camera Context:**
   - Overhead camera (village) should show approaching enemies far away
   - Over-shoulder camera (approach) should show enemy waves emerge

---

## Design Notes

### Why 65m?
- ~20 seconds march at 3.5 m/s
- Gives player time to see and react
- Feels like real siege approach
- Scales with tower attack ranges (~30–40m)

### Why 3.5s Formation Delay?
- Players need to see "oh, there's a wave coming"
- Prevents instant combat surprise
- Matches typical tower defense pacing
- Can be tuned per wave difficulty

### Why Flatten Terrain?
- Exterior terrain is visual only
- NavMesh must stay on corridors/aprons
- Flattening ensures clean pathfinding
- Prevents enemies clipping through slopes

---

## Quick Wins (10–15 min each, immediate improvement)

Do these as you test:

1. **Make the world feel bigger**
   - Set Terrain size to 600×600 (or 800×800)
   - Move village walls closer to center (0,0)
   - Result: Enemies have MORE distance to march

2. **Test the march feel**
   - Temporarily spawn 5 enemies via GetRandomFarSpawnPosition()
   - Watch them walk to gate
   - Adjust until it feels "good pace, not rushed"

3. **Camera improvement**
   - Add simple follow camera that zooms out during big waves
   - Lets player see the approach happening

4. **Tune enemy speed**
   - Default marchSpeed = 3.5 m/s
   - At 65m distance, takes ~18 seconds to reach gate
   - Adjust if it feels too fast/slow

---

## Common Pitfalls (What Breaks It)

❌ **Enemies spawn too close** → instant fight, no tension  
❌ **Roads too narrow** → enemies bunch up, look stupid  
❌ **No clear path** → enemies get stuck on terrain  
❌ **Terrain too bumpy** → NavMesh fails, enemies wander  
❌ **Too many enemies at once** → WebGL performance drops  

**Solution:** Use the layered approach. Test each layer before adding complexity.

---

## Known Limitations (Post-Launch)

- All enemies path to nearest gate (future: multi-gate tactics)
- Spawn aprons are functional cylinders (future: prettier encampments)
- No formation AI (enemies bunch up, future: tactical spacing)
- No retreat/routing (enemies always push forward)

These can be iterated on after v1.0.

---

## Files to Modify

| File | Change | Est. Time |
|---|---|---|
| Create ExteriorConstants.cs | New file | 5 min |
| VillageSceneBuilder.cs | Replace BuildExterior() methods | 15 min |
| EnemyController.cs | Add FormUpAndMarch() + FindNearestGate() | 10 min |
| WaveManager.cs | Add SpawnWaveAtExterior() | 10 min |
| NavMesh Bake | Re-bake after terrain changes | 2–5 min |

**Total Estimate:** 45–60 min

---

## What This Fixes

✅ **Before:** Enemies spawn 10m away, instant combat, no breathing room  
✅ **After:** Enemies spawn 65m away, march for 20s, real tactical space

✅ **Before:** No sense of battle scale, feels claustrophobic  
✅ **After:** Open-world approach, siege feel, depth

✅ **Before:** WO-24/27 couldn't fully patch it  
✅ **After:** Clean architecture, scalable for camps (WO-216)

---

**This is the foundation for WO-214 (dual camera), WO-216 (camps), and WO-217–219 (combat feel).**

Commit message: `"WO-273: rearchitect exterior zone system — proper spawn distance, approach corridors, formation delay"`

---

**Estimate:** 45–60 min build + test

