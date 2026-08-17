<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WO-216: Enemy Camps System — Early-Game Grinding & Learning Content

**Status: READY TO IMPLEMENT**

**Date:** 2026-06-01  
**Priority:** 🟡 HIGH (foundational content, new player experience)  
**Owner:** CLI  
**Depends On:** WO-215 (build mode should be working first for spatial reasoning)  
**Blocks:** None (but unblocks world content testing)  
**Can Run In Parallel:** None — wait for WO-215, then this is next logical step

---

## Vision

Add a system for **persistent, local enemy camps** around Elarion that serve as grinding and mechanics-learning content. These are the first content new players encounter outside the village — low-stakes, repeatable combat to practice builds and earn resources.

---

## Architecture

### Folder Structure
```
Assets/_Modules/Camps/
├── CampDefinition.cs
├── CampType.cs
├── HollowCamp.cs
├── CampTrigger.cs
├── CampManager.cs
├── Data/
│   └── CampDefinitions/
│       ├── FrostbiteCamp.asset
│       ├── EmberfangCamp.asset
│       └── WraithveilCamp.asset
└── Prefabs/
    └── Camps/
        ├── FrostbiteCamp.prefab
        ├── EmberfangCamp.prefab
        └── WraithveilCamp.prefab
```

### Core Classes

#### 1. CampType.cs (Enum)
```csharp
public enum CampType
{
    Frostbite,   // North — ice-themed
    Emberfang,   // South — fire-themed
    Wraithveil   // West/East — shadow-themed
}
```

#### 2. CampDefinition.cs (ScriptableObject)
```csharp
[CreateAssetMenu(menuName = "Camps/Camp Definition")]
public class CampDefinition : ScriptableObject
{
    public string campId;                    // e.g. "frostbite_01"
    public CampType campType;
    public string displayName;
    
    [Header("Enemies")]
    public int minEnemies = 3;
    public int maxEnemies = 6;
    public List<EnemyWeight> enemyPool;     // Enemy prefab + spawn weight
    
    [Header("Visuals")]
    public GameObject campVisualPrefab;     // Tent/fire/banner prefab
    
    [Header("Rewards")]
    public LootTable lootTable;
    public int crystalReward = 25;
    public int foodReward = 15;
}
```

#### 3. HollowCamp.cs (MonoBehaviour)
Main camp controller:
```csharp
public class HollowCamp : MonoBehaviour
{
    public CampDefinition definition;
    public string uniqueId;                 // Generated at spawn
    public bool isCleared = false;
    public DateTime clearedTime;            // For respawn calculation
    
    [SerializeField] private Transform[] enemySpawnPoints;
    private CampTrigger trigger;
    
    private void Awake()
    {
        trigger = GetComponentInChildren<CampTrigger>();
        trigger.camp = this;
    }
    
    public void StartCombat()
    {
        if (isCleared) return;
        CampManager.Instance.StartCampBattle(this);
    }
    
    public void MarkAsCleared()
    {
        isCleared = true;
        clearedTime = DateTime.UtcNow;
        CampManager.Instance.SaveCampState();
    }
}
```

#### 4. CampTrigger.cs
Simple trigger that calls parent camp's StartCombat() when player enters.

#### 5. CampManager.cs (Singleton)
Main controller. Responsibilities:
- Spawn initial local camps on scene load (3–5 camps around village)
- Track all active camps
- Handle save/load of cleared state
- Check respawn timers (20–36 hours per camp)
- Provide StartCampBattle(HollowCamp) method
- Integrate with combat system

---

## Camp Types (Early Game)

### Frostbite Camp (North)
- **Enemy composition:** 70% Skeletons, 30% Ice Hollows
- **Visuals:** Blue fire, ice spikes, frozen ground corruption
- **Enemies:** 3–5 per camp
- **Rewards:** 25 crystals, 15 food, ice-themed loot

### Emberfang Camp (South)
- **Enemy composition:** 70% Orc-like Hollows, 30% Fire Hollows
- **Visuals:** Burning tents, red glow, scorched earth
- **Enemies:** 3–5 per camp
- **Rewards:** 25 crystals, 15 food, fire-themed loot

### Wraithveil Camp (West/East)
- **Enemy composition:** Mostly fast wispy shadow enemies
- **Visuals:** Shadowy/transparent, dark fog, corrupted grass
- **Enemies:** 3–5 per camp
- **Rewards:** 25 crystals, 15 food, shadow-themed loot

---

## Implementation Plan

### Phase 1: Core System (Foundations)
1. Create CampType enum
2. Create CampDefinition ScriptableObject
3. Create HollowCamp MonoBehaviour
4. Create CampTrigger component
5. Create CampManager singleton with:
   - Camp spawn logic
   - Save/load integration
   - Respawn timer logic (20–36 hours)

### Phase 2: Integration
6. Wire CampManager.StartCampBattle() to existing auto-battle system
   - On victory: call camp.MarkAsCleared()
   - On defeat: camp remains active, retry available
7. Add camp cleared state to save data:
   ```csharp
   public List<CampSaveData> clearedCamps;
   // Where CampSaveData = { uniqueId, clearedTime }
   ```

### Phase 3: Prefabs & Content
8. Create Frostbite camp prefab + CampDefinition asset
9. Create Emberfang camp prefab + CampDefinition asset
10. Create Wraithveil camp prefab + CampDefinition asset
11. Create visual prefabs (tent, fire, banners) for each type

### Phase 4: World Integration
12. Add BuildLocalCamps() method to VillageSceneBuilder or new WildernessBuilder
13. Spawn 3–5 camps in logical positions around village (not too close to gates)
14. Test placement on Village scene

### Phase 5: Testing
15. Load game, verify camps spawn
16. Walk into camp, trigger battle
17. Victory → camp clears, respawn timer starts
18. Load save, verify camp cleared state persists
19. Wait/simulate 36 hours, verify respawn

---

## Acceptance Criteria

- [ ] CampType enum defined
- [ ] CampDefinition ScriptableObject created + functional
- [ ] HollowCamp MonoBehaviour spawns enemies on combat trigger
- [ ] CampManager singleton created + handles all camps
- [ ] Save/load system integrated (cleared camps persist)
- [ ] Respawn timer logic working (20–36 hours)
- [ ] 3 camp types created (Frostbite, Emberfang, Wraithveil)
- [ ] 3–5 camps spawned around village at game start
- [ ] Camps visible from distance (smoke/corruption visuals)
- [ ] Walk into camp → auto-battle starts
- [ ] Victory → camp marks cleared
- [ ] Defeat → camp remains, retry available
- [ ] WebGL tested: camps spawn, battles work, save persists
- [ ] Commit: "WO-216: add enemy camps system (Frostbite, Emberfang, Wraithveil)"

---

## Integration Checklist

- [ ] Combat system receives camp enemy list and resolves victory/defeat
- [ ] Loot system awards crystal/food rewards on victory
- [ ] Save system stores `clearedCamps` data
- [ ] Load system restores camp cleared state + respawn timers
- [ ] Scene builder places camps spatially (no overlap with gates/structures)

---

## Notes

- Camps are the **first repeatable, no-stakes content** for new players
- Use existing auto-battle + enemy spawning systems (no new combat logic needed)
- Respawn timer is "real time" (20–36 hours wall-clock, not playtime)
- Camps should be **avoidable** — player can walk around them if they want
- Visual smoke/corruption should be visible from ~50m away so player knows camps exist

---

**Design credit:** Samantha (vision + full spec)

**Estimate:** 4–6 hours (core system + 3 camp types + integration + testing)
