<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WO-361: Wave Rewards (Resources) & Passive Defensive XP Indicators

**Status:** READY TO IMPLEMENT  
**Estimated Effort:** P1 (1–2 days)  
**Priority:** High (economy motivation + progression clarity)  
**Lane:** Economy

---

## Overview

Strengthen wave-running motivation and clarify passive progression:

1. **Wave Resource Rewards** — Defeating waves drops wood, iron, and food (not just crystal)
   - Motivates players to run waves regularly
   - Diversifies economy (not all crystal)
   - Supports building (towers need wood + iron)

2. **Passive Defensive XP Indicator** — Tooltip/badge showing towers/walls earn XP while player builds/explores
   - "Your towers gain +5 XP while you build" (visual indicator)
   - Clarifies that idle defenses are progress-generating
   - Reduces perception that only active combat matters

**Why:** Currently waves only reward crystal. Players don't realize towers gain XP passively (thought they're static). Both changes increase engagement and reduce grinding feel.

---

## Acceptance Criteria

### Wave Rewards
- [ ] Waves drop wood on every N-th wave (e.g., every 3rd wave: 30 wood)
- [ ] Waves drop iron on every M-th wave (e.g., every 4th wave: 20 iron)
- [ ] Waves drop food on every K-th wave (e.g., every 2nd wave: 40 food)
- [ ] Drops appear as floating loot pickups at hero location (visual + satisfying)
- [ ] Rewards scale with wave number (later waves = more loot)
- [ ] HUD shows gained resources ("+30 Wood" popup, brief fade)
- [ ] Rewards persist if player leaves mid-wave (dropped loot collected on return)
- [ ] Configurable via WaveRewardConfig ScriptableObject (no hardcoding)

### Passive Defensive XP Indicators
- [ ] Tooltip on tower/wall cards shows: "Gains XP passively while you build"
- [ ] "Earning 5 XP/min while idle" label on selected structure
- [ ] Badge/icon in village HUD: "Towers earning XP" (toggleable)
- [ ] HUD shows active towers' XP gain rate in real-time
- [ ] Popup notification on first idle structure upgrade: "Your towers are growing stronger while you build"
- [ ] Notification fade after 4s or dismiss on tap
- [ ] Works for towers, walls, gates (any structure with XP)

---

## Files to Create

### New Files
- `Assets/_Modules/Village/Economy/WaveRewardConfig.cs` — Scriptable config for drop rates
- `Assets/_Modules/Village/Economy/WaveRewardDropper.cs` — Drop loot on wave complete
- `Assets/_Modules/Village/UI/PassiveXPIndicator.cs` — HUD badge for idle XP gain
- `Assets/_Modules/Village/UI/Tooltips/PassiveXPTooltip.cs` — Structure card tooltip

### Existing Files (Modify)
- `Assets/_Modules/Village/Waves/WaveManager.cs` — Call WaveRewardDropper on victory
- `Assets/_Modules/Village/BuildMode/BuildSelectionUI.cs` — Add passive XP label to selection panel
- `Assets/_Modules/Village/EconomyService.cs` — Integrate resource grants (if not already done)

---

## Design Spec

### Wave Reward Schedule

| Wave | Wood | Iron | Food | Crystal | Notes |
|------|------|------|------|---------|-------|
| 1 | — | — | — | 50 | Intro (crystal only) |
| 2 | — | — | 30 | 50 | First food drop |
| 3 | 20 | — | — | 75 | First wood drop |
| 4 | — | 15 | 30 | 75 | Iron enters |
| 5 | 25 | — | — | 100 | Wood increases |
| 6 | — | 20 | 40 | 100 | Food increases |
| 7+ | 30 | 25 | 50 | 150 | Plateau + scale |

**Scaling:** Every 5 waves after Wave 7, increase resources by 20% (progressive economy growth).

**Rationale:**
- Early waves (1–3): Crystal-heavy (tower building)
- Mid waves (4–6): Food/wood balance (feeding troops, wall upgrades)
- Late waves (7+): All resources grow proportionally

### Loot Drop Visuals

**Visual:** Floating 3D pickup at hero location  
**Model:** Small glowing gem/sack (depends on resource type)  
**Color:** Wood=brown, Iron=silver/grey, Food=green, Crystal=blue  
**Animation:** Float upward + fade out, or player collects with pickup effect  
**Sound:** Coin/chime sound on drop, success chime on collect  
**Persistence:** If player leaves dungeon/world without collecting, loot waits (saved in level state)

### HUD Notifications

**On wave victory, show popup:**
```
┌─────────────────────┐
│  Wave 3 Victory!    │
│  +75 ◆              │
│  +30 Wood           │
│  +40 Food           │
└─────────────────────┘
```

**Duration:** 2–3s, fade out, or dismiss on tap  
**Position:** Center-top, doesn't block gameplay  
**Accumulation:** If player defeats multiple waves, aggregate rewards in one popup

### Passive XP Indicators

#### 1. Village HUD Badge
**Position:** Top-left, next to crystal balance  
**Content:** Icon + label  
```
Tower Icon  ⚡ Towers earning XP (5/min)
```

**Toggle:** Click to expand/collapse details:
```
⚡ Active Towers (3): +5 XP/min each
  - Tower 1: Level 2 → 3 (45% progress)
  - Tower 2: Level 1 → 2 (28% progress)
  - Wall A: Level 1 → 2 (62% progress)
```

**Color:** Green (passive = good), dims when no towers active

#### 2. Structure Card Tooltip (BuildSelectionUI)

**On tap, show info panel with section:**
```
─ PASSIVE PROGRESSION ─
This tower gains 5 XP/min while idle.
Currently: Level 2/4 (45% → Level 3)
Time to next level: ~2 minutes
```

**Tooltip trigger:** Hover on structure card or info icon  
**Auto-dismiss:** 5s or on click away

#### 3. First-Time Notification

**Trigger:** First time player enters build mode after towers are placed  
**Content:**
```
💡 Did you know?
Your towers are growing stronger while you build.
Towers earn XP passively even when you're not in combat.
─────────────────────
[Got it]
```

**Position:** Bottom-right, non-blocking  
**Persist:** Only shows once per save file (flag: `PassiveXPTutorialShown`)

---

## Implementation Notes

### WaveRewardConfig.cs

```csharp
[CreateAssetMenu(fileName = "WaveRewardConfig", menuName = "Defenders/Economy/Wave Rewards")]
public class WaveRewardConfig : ScriptableObject
{
    [System.Serializable]
    public struct WaveRewardData
    {
        public int waveNumber;
        public int wood;
        public int iron;
        public int food;
        public int crystals;
    }

    [SerializeField] private WaveRewardData[] _rewards;

    public WaveRewardData GetReward(int waveNumber)
    {
        // Linear search (small array, fine for this use case)
        foreach (var reward in _rewards)
        {
            if (reward.waveNumber == waveNumber)
                return reward;
        }

        // Fallback to wave 7+ template and scale
        var lastReward = _rewards[_rewards.Length - 1];
        float scale = 1f + ((waveNumber - 7) / 5) * 0.2f;  // +20% per 5 waves
        return new WaveRewardData
        {
            waveNumber = waveNumber,
            wood = (int)(lastReward.wood * scale),
            iron = (int)(lastReward.iron * scale),
            food = (int)(lastReward.food * scale),
            crystals = (int)(lastReward.crystals * scale)
        };
    }
}
```

### WaveRewardDropper.cs

```csharp
public sealed class WaveRewardDropper : MonoBehaviour
{
    [SerializeField] private WaveRewardConfig _config;
    [SerializeField] private GameObject _lootPrefab;  // Floating gem/sack model
    [SerializeField] private float _dropSpread = 2f;

    public void DropRewards(int waveNumber, Vector3 dropPosition)
    {
        var reward = _config.GetReward(waveNumber);

        // Create visual droplets
        if (reward.wood > 0)
            SpawnLoot(reward.wood, "Wood", dropPosition, Color.yellow);
        if (reward.iron > 0)
            SpawnLoot(reward.iron, "Iron", dropPosition, Color.gray);
        if (reward.food > 0)
            SpawnLoot(reward.food, "Food", dropPosition, Color.green);
        if (reward.crystals > 0)
            SpawnLoot(reward.crystals, "Crystal", dropPosition, Color.cyan);

        // Grant to economy
        var econ = EconomyService.Instance;
        if (econ != null)
        {
            econ.AddResource(reward.wood, ResourceType.Wood);
            econ.AddResource(reward.iron, ResourceType.Iron);
            econ.AddResource(reward.food, ResourceType.Food);
            econ.AddResource(reward.crystals, ResourceType.Crystal);
        }

        // Show HUD notification
        ShowRewardNotification(reward);
    }

    private void SpawnLoot(int amount, string type, Vector3 pos, Color color)
    {
        var offset = Random.insideUnitCircle * _dropSpread;
        var loot = Instantiate(_lootPrefab, pos + (Vector3)offset, Quaternion.identity);
        
        // Animate upward + fade
        StartCoroutine(LootFloatAndFade(loot.transform, 2f));
        
        // Particle effect + sound
        AudioService.PlayCue(AudioId.LootDrop, pos);
    }

    private void ShowRewardNotification(WaveRewardData reward)
    {
        var notif = Resources.Load<GameObject>("UI/WaveRewardNotif");
        var instance = Instantiate(notif);
        instance.GetComponent<WaveRewardNotification>().SetReward(reward);
    }
}
```

### PassiveXPIndicator.cs (Village HUD)

```csharp
public sealed class PassiveXPIndicator : MonoBehaviour
{
    [SerializeField] private Label _badge;
    [SerializeField] private VisualElement _expandedPanel;
    private int _activeTowerCount = 0;

    private void Update()
    {
        var towers = FindObjectsByType<Tower>(FindObjectsSortMode.None);
        _activeTowerCount = towers.Length;

        if (_activeTowerCount > 0)
        {
            int totalXpPerMin = _activeTowerCount * 5;  // 5 XP/min per tower
            _badge.text = $"⚡ Towers earning {totalXpPerMin}/min";
            _badge.style.display = DisplayStyle.Flex;
        }
        else
        {
            _badge.style.display = DisplayStyle.None;
        }
    }

    private void OnBadgeClicked()
    {
        _expandedPanel.style.display = 
            _expandedPanel.style.display == DisplayStyle.Flex 
            ? DisplayStyle.None 
            : DisplayStyle.Flex;
    }
}
```

### BuildSelectionUI Integration

Add to the show panel (after upgrade tier info):

```csharp
public void Show(PlacedStructure structure, ...)
{
    // ... existing code ...

    // NEW: Passive XP section
    if (structure.HasXPGain)  // Towers, walls, gates
    {
        var passiveXpLabel = new Label("⚡ Gains 5 XP/min while idle");
        passiveXpLabel.style.color = Color.green;
        passiveXpLabel.style.fontSize = 11;
        _root.Add(passiveXpLabel);

        // Show progress to next level
        var progress = GetXPProgress(structure);
        var progressLabel = new Label($"Level {progress.current}/{progress.max} " +
                                      $"({progress.percent}%)");
        progressLabel.style.fontSize = 10;
        progressLabel.style.color = new Color(0.8f, 0.8f, 0.8f);
        _root.Add(progressLabel);
    }
}
```

---

## Economy Balancing

### Before (WaveRewards Crystal-Only)
- Players farm waves for crystal
- Build towers immediately
- No motivation to run waves beyond required
- Walls/gates not competitive (no upgrade path incentive)

### After (WaveRewards + Resources)
- Waves reward diverse resources
- Players balance tower upgrades (crystal) with wall/gate building (wood/iron)
- Food supports troop training (future WO)
- Later waves feel more rewarding (scaling rewards)

**No breaking changes:** Existing crystal rewards unchanged, just add resources.

---

## Testing Checklist

- [ ] Wave rewards drop at correct intervals (every 2nd, 3rd, 4th)
- [ ] Loot visuals float + fade correctly
- [ ] Resources granted to economy (EconomyService)
- [ ] HUD notification shows correct amounts
- [ ] Passive XP badge appears when towers exist
- [ ] Badge updates on tower placement/removal
- [ ] Structure selection panel shows passive XP label
- [ ] First-time tooltip plays once per save
- [ ] Rewards scale correctly after wave 7
- [ ] Works in WebGL build (no Resources.Load failures)
- [ ] No GC allocation per drop (pool loot objects)

---

## What NOT to Touch

- Combat difficulty (waves unchanged)
- Tower combat XP gain (separate system, only passive gain shown)
- Building costs (economy not rebalanced, just new rewards)
- Troop mechanics (future WO: food for troops)

---

## Dependencies

- **Depends on:** WaveManager, EconomyService, BuildSelectionUI, Village HUD
- **Unblocks:** Troop training (WO-future: food → troops), economy balancing
- **Parallel:** None (1–2 days, self-contained)

---

## Future Enhancements

- [ ] Resource crates (spawn special waves dropping rarer resources)
- [ ] Quest chains ("Run 5 waves for 200 wood")
- [ ] Daily bonuses ("Win 1 wave today for 50% XP bonus")
- [ ] Tower XP milestones (visual celebration at each level)
- [ ] Passive income (warehouses generate food/wood per day)

---

## Acceptance Sign-Off

- [ ] Wave rewards implemented and balanced
- [ ] Passive XP indicators clear and non-intrusive
- [ ] Economy feels more rewarding (diverse loot)
- [ ] Players understand towers progress passively
- [ ] Works in WebGL build
