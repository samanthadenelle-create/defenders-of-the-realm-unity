<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WO-362: Enemy Wave Composition System — Smart Grouping & Variety

**Status:** DONE — audit-verified as shipped (2026-08-21 backlog audit).
**Estimated Effort:** P1 (1–2 days)  
**Priority:** High (combat feel, strategic depth)  
**Lane:** Combat/AI

---

## Overview

Replace flat enemy spawning (5 Skeletons in a row) with intelligent composition:

1. **Wave Composition Algorithm** — Each wave mixes enemy types based on difficulty/tier
   - Example: Wave 3 = 1 Archer + 2 Skeletons + 1 Brute (not 5 Skeletons)
   - Variety keeps combat engaging and unpredictable

2. **Smart Grouping Logic** — Position enemies in tactical formations, not linear spawn lines
   - Archers + melee separate (backline support)
   - Brutes in front (tanking)
   - Casters at mid-range (optimal positioning)

3. **Difficulty Scaling** — Composition adjusts per wave tier, not just enemy count
   - Wave 1–2: 100% weak enemies (Skeleton, Goblin)
   - Wave 3–5: 60% weak + 40% medium (add Archer, Orc)
   - Wave 6+: Mix of all (Skeleton, Archer, Brute, Caster)

4. **Spawn Point Variance** — Don't spawn all at one gate; distribute across gates for tactical challenge

**Why:** Five identical enemies in a line is easy to AoE/trivialize. Mixed comps force player adaptation. Grouping by role (tank/DPS/support) makes AI feel coordinated, not random.

---

## Acceptance Criteria

- [ ] Each wave has 2–4 different enemy types (not all same)
- [ ] Enemy types grouped by tactical role (tanks front, DPS mid, support back)
- [ ] Spawn locations distributed across gates (not all at one entry)
- [ ] Composition scales with wave number (early = easy mix, late = hard mix)
- [ ] No two consecutive waves have identical composition
- [ ] Weak enemies still appear late-game (mixed with strong) for pacing variety
- [ ] Boss waves (every 5th?) have special composition (e.g., 1 Elite + 3 Medium)
- [ ] Composition is deterministic per wave (same result per playthrough for testing)
- [ ] Can toggle debug mode to see wave composition before spawn

---

## Files to Create

### New Files
- `Assets/_Modules/Village/Enemies/EnemyWaveComposition.cs` — Wave composition data
- `Assets/_Modules/Village/Enemies/WaveCompositionBuilder.cs` — Algorithm to generate compositions
- `Assets/_Modules/Village/Enemies/SmartEnemySpawner.cs` — Position + spawn enemies strategically

### Existing Files (Modify)
- `Assets/_Modules/Village/Waves/WaveManager.cs` — Use new composer instead of flat spawning
- `Assets/_Modules/Village/Enemies/EnemySpawner.cs` — Integrate smart positioning

---

## Design Spec

### Enemy Tiers

| Tier | Examples | Role | Difficulty |
|------|----------|------|------------|
| **Weak** | Skeleton, Goblin, Slime | Cannon fodder | 1 HP, low dmg |
| **Medium** | Archer, Orc, Necromancer | DPS/Support | 3–5 HP, medium dmg |
| **Strong** | Brute, Werewolf, Troll | Tank | 8–12 HP, high dmg |
| **Elite** | Vampire, Demon Lord | Boss-like | 15+ HP, special abilities |

### Wave Composition Rules

**Composition = (Weak Count, Medium Count, Strong Count, Elite Count)**

| Wave | Weak | Medium | Strong | Elite | Total | Example |
|------|------|--------|--------|-------|-------|---------|
| 1 | 5 | 0 | 0 | 0 | 5 | 5× Skeleton |
| 2 | 4 | 1 | 0 | 0 | 5 | 4× Skeleton + 1× Archer |
| 3 | 3 | 2 | 0 | 0 | 5 | 3× Skeleton + 2× Orc |
| 4 | 3 | 1 | 1 | 0 | 5 | 3× Goblin + 1× Archer + 1× Brute |
| 5 | 2 | 2 | 1 | 1 | 6 | 2× Skeleton + 2× Orc + 1× Brute + 1× Vampire |
| 6 | 2 | 3 | 1 | 0 | 6 | 2× Goblin + 3× Medium + 1× Brute |
| 7+ | 2 | 2 | 2 | 1 | 7 | Balanced mix + 1 Elite |

**Scaling:** After wave 7, increase by +1 enemy per 2 waves (wave 9 = 8 enemies, wave 11 = 9 enemies).

### Spawn Positioning Logic

**Gate-based distribution:**
- 4 gates (North, East, South, West)
- Rotate spawn gate per wave (not all at North)
- Example: Wave 1 → North, Wave 2 → East, Wave 3 → South, etc.

**Role-based positioning within spawn group:**
```
Tank (Brute)
    ↓
[Weak] [Medium] [Weak]
    ↑           ↑
  Melee       Archer (backline)
```

- **Tanks:** Spawn first, in front
- **Melee/Medium:** Surround tank
- **Archers/Ranged:** Spawn in back, away from hero
- **Elite:** Single spawn, center of group

**Formation example (Wave 5 spawn):**
```
Gate: North

Formation:
     Vampire (Elite, center)
     /    |    \
  Skeleton Orc Archer
    /       |      \
Skeleton Brute Skeleton

Spacing: 2m between units (prevents all dying to single AoE)
```

### Wave Composition Builder Algorithm

```python
def build_composition(wave_number):
    # Tier distribution based on wave
    if wave_number <= 2:
        weak_ratio = 1.0
        medium_ratio = 0.0
        strong_ratio = 0.0
        elite_count = 0
    elif wave_number <= 5:
        weak_ratio = 0.6 - (wave_number - 3) * 0.1
        medium_ratio = 0.4 + (wave_number - 3) * 0.1
        strong_ratio = 0.0
        elite_count = 1 if wave_number == 5 else 0
    else:
        weak_ratio = 0.2
        medium_ratio = 0.3 + (wave_number - 6) * 0.05
        strong_ratio = 0.3 + (wave_number - 6) * 0.05
        elite_count = 1 if wave_number % 5 == 0 else 0

    total_enemies = 5 + (wave_number // 2)
    
    # Pick random enemy within each tier
    weak_count = int(total_enemies * weak_ratio)
    medium_count = int(total_enemies * medium_ratio)
    strong_count = int(total_enemies * strong_ratio)
    
    # Adjust rounding
    total_count = weak_count + medium_count + strong_count + elite_count
    if total_count < total_enemies:
        medium_count += (total_enemies - total_count)
    
    return {
        'weak': [pick_random_weak() for _ in range(weak_count)],
        'medium': [pick_random_medium() for _ in range(medium_count)],
        'strong': [pick_random_strong() for _ in range(strong_count)],
        'elite': [pick_random_elite() for _ in range(elite_count)]
    }
```

---

## Implementation Notes

### EnemyWaveComposition.cs

```csharp
[System.Serializable]
public struct WaveComposition
{
    public List<EnemyType> weakEnemies;      // Skeleton, Goblin, Slime
    public List<EnemyType> mediumEnemies;    // Archer, Orc
    public List<EnemyType> strongEnemies;    // Brute, Werewolf
    public List<EnemyType> eliteEnemies;     // Vampire, Demon Lord
    
    public int TotalCount => 
        weakEnemies.Count + mediumEnemies.Count + 
        strongEnemies.Count + eliteEnemies.Count;
}

public enum EnemyTier { Weak, Medium, Strong, Elite }
public enum SpawnGate { North, East, South, West }
```

### WaveCompositionBuilder.cs

```csharp
public sealed class WaveCompositionBuilder
{
    [SerializeField] private EnemyType[] _weakEnemies = 
        { EnemyType.Skeleton, EnemyType.Goblin, EnemyType.Slime };
    [SerializeField] private EnemyType[] _mediumEnemies =
        { EnemyType.Archer, EnemyType.Orc, EnemyType.Necromancer };
    [SerializeField] private EnemyType[] _strongEnemies =
        { EnemyType.Brute, EnemyType.Werewolf, EnemyType.Troll };
    [SerializeField] private EnemyType[] _eliteEnemies =
        { EnemyType.Vampire, EnemyType.DemonLord };

    public WaveComposition BuildComposition(int waveNumber, int seed = -1)
    {
        if (seed >= 0)
            Random.InitState(seed);  // Deterministic for testing

        float weakRatio = Mathf.Max(0.2f, 1.0f - (waveNumber * 0.1f));
        float mediumRatio = Mathf.Min(0.5f, (waveNumber * 0.08f));
        float strongRatio = Mathf.Min(0.4f, (waveNumber * 0.06f));
        
        int totalEnemies = 5 + (waveNumber / 2);
        int weakCount = Mathf.RoundToInt(totalEnemies * weakRatio);
        int mediumCount = Mathf.RoundToInt(totalEnemies * mediumRatio);
        int strongCount = Mathf.RoundToInt(totalEnemies * strongRatio);
        int eliteCount = (waveNumber % 5 == 0 && waveNumber > 3) ? 1 : 0;

        // Adjust rounding
        int sum = weakCount + mediumCount + strongCount + eliteCount;
        if (sum < totalEnemies)
            mediumCount += (totalEnemies - sum);

        var composition = new WaveComposition
        {
            weakEnemies = RandomizeEnemies(_weakEnemies, weakCount),
            mediumEnemies = RandomizeEnemies(_mediumEnemies, mediumCount),
            strongEnemies = RandomizeEnemies(_strongEnemies, strongCount),
            eliteEnemies = RandomizeEnemies(_eliteEnemies, eliteCount)
        };

        return composition;
    }

    private List<EnemyType> RandomizeEnemies(EnemyType[] pool, int count)
    {
        var result = new List<EnemyType>();
        for (int i = 0; i < count; i++)
            result.Add(pool[Random.Range(0, pool.Length)]);
        return result;
    }
}
```

### SmartEnemySpawner.cs

```csharp
public sealed class SmartEnemySpawner : MonoBehaviour
{
    [SerializeField] private WaveCompositionBuilder _builder;
    [SerializeField] private List<Gate> _gates;
    [SerializeField] private float _unitSpacing = 2f;
    [SerializeField] private float _tierSpacing = 3f;

    public void SpawnWave(int waveNumber)
    {
        var composition = _builder.BuildComposition(waveNumber);
        var gate = SelectGate(waveNumber);
        var basePos = gate.SpawnPoint;

        // Spawn by tier (tanks first, then DPS, then support)
        float yOffset = 0;

        // Tanks (strong/elite)
        yOffset = SpawnTier(composition.strongEnemies, basePos + Vector3.forward * yOffset, "Tank");
        yOffset += _tierSpacing;

        // Melee/Medium DPS
        yOffset = SpawnTier(composition.mediumEnemies, basePos + Vector3.forward * yOffset, "DPS");
        yOffset += _tierSpacing;

        // Weak (support/filler)
        yOffset = SpawnTier(composition.weakEnemies, basePos + Vector3.forward * yOffset, "Support");
        
        // Elite (single, center)
        if (composition.eliteEnemies.Count > 0)
        {
            SpawnElite(composition.eliteEnemies[0], 
                       basePos + Vector3.forward * 10f);
        }

        Debug.Log($"[Wave {waveNumber}] Spawned {composition.TotalCount} enemies " +
                  $"({composition.weakEnemies.Count}W + {composition.mediumEnemies.Count}M + " +
                  $"{composition.strongEnemies.Count}S + {composition.eliteEnemies.Count}E)");
    }

    private float SpawnTier(List<EnemyType> tier, Vector3 basePos, string role)
    {
        if (tier.Count == 0) return 0;

        for (int i = 0; i < tier.Count; i++)
        {
            // Spread horizontally, slightly offset forward
            float xOffset = (i - tier.Count / 2f) * _unitSpacing;
            var spawnPos = basePos + Vector3.right * xOffset;
            SpawnEnemy(tier[i], spawnPos, role);
        }

        return _tierSpacing;
    }

    private void SpawnEnemy(EnemyType type, Vector3 pos, string role)
    {
        var prefab = EnemyCatalog.GetPrefab(type);
        var enemy = Instantiate(prefab, pos, Quaternion.identity);
        // Optional: Set behavior hint (role) for AI
    }

    private Gate SelectGate(int waveNumber)
    {
        int gateIndex = waveNumber % _gates.Count;
        return _gates[gateIndex];
    }
}
```

### WaveManager Integration

```csharp
public void StartWave(int waveNumber)
{
    // Old: _enemySpawner.SpawnRandomEnemies(waveNumber);
    
    // New:
    _smartSpawner.SpawnWave(waveNumber);
    
    // Rest of wave logic unchanged
}
```

---

## Testing Checklist

- [ ] Each wave has 2–4 different enemy types
- [ ] Composition varies from wave to wave (not repeated)
- [ ] Weak enemies present in all waves (mixed ratios)
- [ ] Strong enemies only appear Wave 4+
- [ ] Elite enemies spawn every 5th wave
- [ ] Enemies position in tactical formations (not linear)
- [ ] Spawn gates rotate per wave (not all at North gate)
- [ ] Debug log shows composition before spawn (for QA)
- [ ] Wave difficulty increases progressively (not suddenly spike)
- [ ] Works in WebGL build

---

## What NOT to Touch

- Enemy AI (SmartEnemySpawner only handles positioning, not behavior)
- Enemy stats/balance (use existing HP/DMG values)
- Gate spawning mechanics (just re-use existing spawn points)
- Wave difficulty tuning (composer handles scaling, not this WO)

---

## Dependencies

- **Depends on:** WaveManager, EnemySpawner, EnemyAI
- **Unblocks:** Combat balance tuning (can now test varied comps)
- **Parallel:** None (1–2 days, self-contained)

---

## Debug Features

**Console command: `/wave-composition [waveNumber]`**
```
Output:
Wave 5 Composition:
  Weak (2): Skeleton, Goblin
  Medium (2): Archer, Orc
  Strong (1): Brute
  Elite (1): Vampire
Total: 6 enemies
Spawn Gate: South (rotated from previous)
```

**Visual indicator in editor:**
- Gizmo lines show spawn formation
- Color-coded by tier (weak=green, medium=yellow, strong=red, elite=purple)

---

## Acceptance Sign-Off

- [ ] Wave composition algorithm working (varied mixes per wave)
- [ ] Enemy positioning tactical (not linear spawn line)
- [ ] Difficulty scaling smooth (not sudden spikes)
- [ ] Debug features enable QA testing
- [ ] Works in WebGL build

> **AUDIT 2026-08-21 (agent fleet, read-only):** FIXED. Evidence: `WaveCompositionBuilder.cs:1-20` — tiered composition + elite cadence. Status was flipped from READY by the AUDIT, not by an implementation pass. The body below is left intact; if this call is wrong, the evidence cited here is what to challenge.
