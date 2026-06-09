# WO-365: Character Idle Pose States — Town vs Combat Stance

**Status:** READY TO IMPLEMENT  
**Estimated Effort:** P1 (0.5 days)  
**Priority:** High (visual polish, immersion)  
**Lane:** Build/Perf

---

## Overview

Character should switch between **idle poses** based on context:

- **Town/Village:** Relaxed stance, weapon sheathed, natural breathing animation
- **Battle/Exploration:** Combat-ready pose, weapon drawn, aggressive stance
- **Transition:** Smooth blend between poses (0.3s animation)

**Rule:** If not in combat, character is NOT in combat pose.

---

## Acceptance Criteria

- [ ] Hero enters village → Switches to idle pose (weapon sheathed)
- [ ] Hero approaches wave spawn → Switches to combat pose (weapon drawn)
- [ ] Hero enters battle → Stays in combat pose
- [ ] Hero wins wave, returns to village → Switches back to idle pose
- [ ] Animation transition smooth (0.2–0.3s blend)
- [ ] Idle pose shows no weapon (sheathed or invisible)
- [ ] Combat pose shows weapon ready
- [ ] Idle breathing animation plays (chest rises/falls subtly)
- [ ] Can toggle debug to see pose state

---

## Files to Create

### New Files
- `Assets/_Modules/Village/Hero/HeroPoseController.cs` — Manage pose state transitions

### Existing Files (Modify)
- `Assets/_Modules/Village/Hero/HeroLocomotion.cs` — Call SetPose() on state change
- `Assets/_Modules/Village/Waves/WaveManager.cs` — OnWaveStart() → SetPose(Combat)
- `Assets/_Modules/Village/VillageController.cs` → SetPose(Idle) on scene load

---

## Design Spec

### Pose States

| State | Animation | Weapon | Stance | Context |
|-------|-----------|--------|--------|---------|
| **Idle** | Breathing, relaxed | Sheathed (invisible) | Standing naturally | Town, menu, building |
| **Combat** | Ready, alert | Drawn/visible | Crouched slightly, ready | Battle, wave active, outpost |
| **Moving** | Walk/run | Depends on state | Natural movement | Navigation (any context) |

### State Transitions

```
Town Entry
    ↓
SetPose(Idle)
    ↓
[Hero breathing, weapon sheathed, relaxed stance]
    ↓
Wave Spawn Detected / Combat Starts
    ↓
SetPose(Combat)
    ↓
[Hero crouched, weapon drawn, ready]
    ↓
Wave Victory / Return to Village
    ↓
SetPose(Idle)
    ↓
[Back to breathing, weapon sheathed]
```

### Animation Blending

Transition time: **0.3 seconds** (smooth, not jarring)

```csharp
animator.CrossFade("Idle", 0.3f);  // Town
animator.CrossFade("Combat", 0.3f);  // Battle
```

---

## Implementation

### HeroPoseController.cs

```csharp
public sealed class HeroPoseController : MonoBehaviour
{
    public enum PoseState { Idle, Combat }

    [SerializeField] private Animator _animator;
    [SerializeField] private float _blendDuration = 0.3f;
    [SerializeField] private GameObject _weaponModel;
    
    private PoseState _currentPose = PoseState.Idle;

    public void SetPose(PoseState pose)
    {
        if (_currentPose == pose) return;  // Already in this pose
        
        _currentPose = pose;
        
        switch (pose)
        {
            case PoseState.Idle:
                EnterIdlePose();
                break;
            case PoseState.Combat:
                EnterCombatPose();
                break;
        }
    }

    private void EnterIdlePose()
    {
        // Blend to idle animation
        _animator.CrossFade("Idle", _blendDuration);
        
        // Sheath weapon (hide model or play sheath animation)
        if (_weaponModel != null)
            _weaponModel.SetActive(false);
        
        Debug.Log("[HeroPose] Switched to Idle (weapon sheathed)");
    }

    private void EnterCombatPose()
    {
        // Blend to combat animation
        _animator.CrossFade("Combat", _blendDuration);
        
        // Draw weapon (show model or play draw animation)
        if (_weaponModel != null)
            _weaponModel.SetActive(true);
        
        Debug.Log("[HeroPose] Switched to Combat (weapon drawn)");
    }
}
```

### HeroLocomotion Integration

```csharp
public class HeroLocomotion : MonoBehaviour
{
    private HeroPoseController _poseController;

    private void Update()
    {
        // Walking doesn't change pose (idle or combat pose + walking)
        var moveInput = GetMovementInput();
        if (moveInput != Vector3.zero)
        {
            _animator.SetBool("IsMoving", true);
        }
        else
        {
            _animator.SetBool("IsMoving", false);
        }
    }

    public void OnEnterVillage()
    {
        _poseController.SetPose(HeroPoseController.PoseState.Idle);
    }

    public void OnEnterBattle()
    {
        _poseController.SetPose(HeroPoseController.PoseState.Combat);
    }
}
```

### WaveManager Integration

```csharp
public class WaveManager : MonoBehaviour
{
    private HeroPoseController _heroPose;

    public void BeginWave(int waveNumber)
    {
        // Switch to combat pose before wave spawns
        _heroPose.SetPose(HeroPoseController.PoseState.Combat);
        
        // Spawn enemies
        SpawnWave(waveNumber);
    }

    public void OnWaveVictory()
    {
        // Switch back to idle after victory
        _heroPose.SetPose(HeroPoseController.PoseState.Idle);
        
        // Grant rewards, etc.
        GrantWaveRewards();
    }
}
```

### VillageController Integration

```csharp
public class VillageController : MonoBehaviour
{
    private void Start()
    {
        // Ensure hero starts in idle pose when village loads
        var heroPose = FindObjectOfType<HeroPoseController>();
        if (heroPose != null)
            heroPose.SetPose(HeroPoseController.PoseState.Idle);
    }
}
```

---

## Animation States Required

**Animator parameters:**
- `CurrentPose` (string): "Idle" or "Combat"
- `IsMoving` (bool): true when moving, false when stationary

**Animator transitions:**
```
[Idle State]
  ↓ (blend 0.3s)
[Combat State]
  ↓ (blend 0.3s)
[Idle State]
```

**Sub-states (optional, can be handled in animation):**
- Idle → IdleBreathing (looped)
- Combat → CombatReady (looped)
- Either + IsMoving → WalkCycle / RunCycle

---

## Visual Checklist

### Idle Pose
- [ ] Weapon invisible (sheathed)
- [ ] Shoulders relaxed
- [ ] Stance natural (feet hip-width apart)
- [ ] Breathing animation (subtle chest/shoulder movement)
- [ ] No aggressive stance

### Combat Pose
- [ ] Weapon visible (drawn)
- [ ] Shoulders back
- [ ] Crouched slightly (knees bent)
- [ ] Feet wide (ready to move)
- [ ] Weight forward (aggressive stance)

---

## Testing Checklist

- [ ] Hero spawns in village with idle pose
- [ ] Weapon sheathed on village entry
- [ ] Wave spawn triggers combat pose
- [ ] Weapon drawn on combat transition
- [ ] Transition smooth (0.3s, not jarring)
- [ ] Victory returns to idle pose
- [ ] Weapon sheathed after wave ends
- [ ] Moving animation blends correctly (idle + moving / combat + moving)
- [ ] Idle breathing animation visible
- [ ] Debug output shows pose transitions
- [ ] Works in WebGL build

---

## Debug Features

**Console log output:**
```
[HeroPose] Switched to Idle (weapon sheathed)
[HeroPose] Switched to Combat (weapon drawn)
```

**Inspector display (optional):**
```
Hero Pose State: Idle | Combat
Weapon Visible: true/false
Blend Duration: 0.3s
```

---

## What NOT to Touch

- Movement code (pose is separate from locomotion)
- Combat damage (pose is cosmetic)
- Animation timings (use 0.3s default, tune if needed)
- Weapon stats (pose only hides/shows weapon model)

---

## Dependencies

- **Depends on:** HeroLocomotion, WaveManager, Animator setup
- **Unblocks:** Polish pass (visual consistency)
- **Parallel:** None (0.5 days)

---

## Future Enhancements

- [ ] Emotes/poses (victory pose, rest pose, dodge stance)
- [ ] Weapon draw/sheath animation (sword comes from scabbard)
- [ ] Contextual poses (talking to NPC, interacting with object)
- [ ] Status effects (poisoned = stumbling, frozen = rigid)

---

## Acceptance Sign-Off

- [ ] Hero idle pose in town (weapon sheathed)
- [ ] Hero combat pose in battle (weapon drawn)
- [ ] Transition smooth and natural
- [ ] Pose state persists correctly across scenes
- [ ] Works in WebGL build
