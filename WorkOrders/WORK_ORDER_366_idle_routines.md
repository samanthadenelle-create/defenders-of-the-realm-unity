# WO-366: Idle Routines — Sitting, Playing Dead, Cute Animations

**Status:** READY TO IMPLEMENT  
**Estimated Effort:** P1 (0.5–1 day)  
**Priority:** Medium (polish, charm, visual feedback)  
**Lane:** Build/Perf

---

## Overview

When hero is inactive (no input for 5+ seconds), play cute idle routines:

1. **Sit Down** — Hero sits on ground, relaxed (10s loop)
2. **Play Dead** — Hero lies down dramatically (5s, then stands up)
3. **Stretch** — Hero stretches arms, yawns (3s)
4. **Fidget** — Hero shifts weight, looks around (4s)
5. **Random** — Pick random routine from pool

**Why:** Makes waiting feel less static. Visual feedback that game is responsive. Adds charm and personality. Breaks up long idle periods.

---

## Acceptance Criteria

- [ ] After 5 seconds of inactivity, hero plays idle routine
- [ ] Routine completes, then loops (or returns to idle pose)
- [ ] Any input (movement, action) cancels routine immediately
- [ ] Routines are cute/non-aggressive (sitting, stretching, lying down)
- [ ] Randomization so same routine doesn't repeat 2x in a row
- [ ] Transitions smooth (0.3s blend in/out)
- [ ] Works in all contexts (village, exploration, battle)
- [ ] Can toggle debug to see routine timings
- [ ] Routines don't play during active combat (waves spawned)

---

## Files to Create

### New Files
- `Assets/_Modules/Village/Hero/IdleRoutineManager.cs` — Manage idle animations

### Existing Files (Modify)
- `Assets/_Modules/Village/Hero/HeroLocomotion.cs` — Track input idle time, call routine manager
- `Assets/_Modules/Village/Waves/WaveManager.cs` — Disable routines during active wave

---

## Design Spec

### Idle Routines

| Routine | Animation | Duration | Transition | Notes |
|---------|-----------|----------|------------|-------|
| **Sit** | Sit down, sit idle | 10s loop | 0.5s blend in | Relaxed, patient |
| **Play Dead** | Lie down dramatically | 5s | 0.3s blend | Humorous, gets up after |
| **Stretch** | Stand, stretch arms, yawn | 3s | 0.3s blend | Natural, tired |
| **Fidget** | Shift weight, look around | 4s | 0.2s blend | Restless, impatient |
| **Idle Breathing** | Stand, breathing animation | Infinite loop | 0.3s blend | Default (no routine) |

### Trigger Logic

```
Player Input
    ↓
Reset Idle Timer (0s)
    ↓
[No input for 5s]
    ↓
Idle Timer = 5s
    ↓
Select Random Routine
    ↓
Play Animation
    ↓
[Routine ends]
    ↓
Return to Idle Breathing
    ↓
[Wait 3s, then repeat]
```

### State Diagram

```
Standing Idle (5s)
    ↓
Select Routine
    ├→ Sit (10s) ↘
    ├→ Play Dead (5s) ↘
    ├→ Stretch (3s) ↘
    └→ Fidget (4s) ↘
              ↓
         Routine ends
              ↓
     Return to Idle Breathing
              ↓
       [Wait 3s, repeat]
```

---

## Implementation

### IdleRoutineManager.cs

```csharp
public sealed class IdleRoutineManager : MonoBehaviour
{
    public enum RoutineType { Sit, PlayDead, Stretch, Fidget, None }

    [SerializeField] private Animator _animator;
    [SerializeField] private float _idleTriggerTime = 5f;
    [SerializeField] private float _routineRepeatDelay = 3f;
    [SerializeField] private bool _enabled = true;
    
    private float _inactiveTimer = 0f;
    private RoutineType _currentRoutine = RoutineType.None;
    private RoutineType _lastRoutine = RoutineType.None;
    private Coroutine _routineCoroutine;

    private void Update()
    {
        if (!_enabled) return;

        _inactiveTimer += Time.deltaTime;

        // Trigger routine after idle time
        if (_inactiveTimer >= _idleTriggerTime && _currentRoutine == RoutineType.None)
        {
            PlayIdleRoutine();
        }
    }

    public void ResetIdleTimer()
    {
        _inactiveTimer = 0f;
        CancelRoutine();  // Interrupt any routine
    }

    private void PlayIdleRoutine()
    {
        var routines = new[] { RoutineType.Sit, RoutineType.PlayDead, RoutineType.Stretch, RoutineType.Fidget };
        
        // Don't repeat same routine twice
        var availableRoutines = routines.Where(r => r != _lastRoutine).ToList();
        var selected = availableRoutines[Random.Range(0, availableRoutines.Count)];

        _lastRoutine = selected;
        
        if (_routineCoroutine != null)
            StopCoroutine(_routineCoroutine);
        
        _routineCoroutine = StartCoroutine(ExecuteRoutine(selected));
    }

    private IEnumerator ExecuteRoutine(RoutineType routine)
    {
        _currentRoutine = routine;
        float duration = 0f;

        switch (routine)
        {
            case RoutineType.Sit:
                _animator.CrossFade("Sit", 0.5f);
                duration = 10f;
                break;

            case RoutineType.PlayDead:
                _animator.CrossFade("PlayDead", 0.3f);
                duration = 5f;
                break;

            case RoutineType.Stretch:
                _animator.CrossFade("Stretch", 0.3f);
                duration = 3f;
                break;

            case RoutineType.Fidget:
                _animator.CrossFade("Fidget", 0.2f);
                duration = 4f;
                break;
        }

        Debug.Log($"[IdleRoutine] Playing {routine} ({duration}s)");

        yield return new WaitForSeconds(duration);

        // Return to idle breathing
        _animator.CrossFade("Idle", 0.3f);
        _currentRoutine = RoutineType.None;
        _inactiveTimer = 0f;  // Reset timer for next routine

        // Wait before allowing next routine
        yield return new WaitForSeconds(_routineRepeatDelay);
    }

    private void CancelRoutine()
    {
        if (_routineCoroutine != null)
        {
            StopCoroutine(_routineCoroutine);
            _routineCoroutine = null;
        }

        _currentRoutine = RoutineType.None;
        _animator.CrossFade("Idle", 0.3f);
    }
}
```

### HeroLocomotion Integration

```csharp
public class HeroLocomotion : MonoBehaviour
{
    private IdleRoutineManager _idleRoutines;

    private void Update()
    {
        var inputDir = GetMovementInput();
        
        // Any input cancels idle routine
        if (inputDir != Vector3.zero)
        {
            _idleRoutines.ResetIdleTimer();
            // ... rest of movement logic ...
        }
    }

    public void OnAction()  // Any action button press
    {
        _idleRoutines.ResetIdleTimer();  // Cancel idle routine
    }
}
```

### WaveManager Integration

```csharp
public class WaveManager : MonoBehaviour
{
    private IdleRoutineManager _idleRoutines;

    public void BeginWave(int waveNumber)
    {
        // Disable routines during active combat
        _idleRoutines.enabled = false;
        
        // ... wave logic ...
    }

    public void OnWaveVictory()
    {
        // Re-enable routines after wave ends
        _idleRoutines.enabled = true;
    }
}
```

---

## Animation Requirements

**New animations to create/import:**

| Animation | Duration | Loop | Notes |
|-----------|----------|------|-------|
| `Sit` | 1.5s setup + 8.5s idle | Yes (idle part) | Graceful sit down, then sitting |
| `PlayDead` | 2s lie down + 3s lie | No | Dramatic collapse, then get up |
| `Stretch` | 1s stretch + 2s yawn | No | Arms up, yawn, relax |
| `Fidget` | 4s loop | Yes | Shift weight, look around, repeat |

**Setup in Animator:**
- Create blend tree or state machine for routines
- Transitions: `Idle` ↔ `Sit` / `PlayDead` / `Stretch` / `Fidget`
- Blend time: 0.2–0.5s depending on routine

---

## Testing Checklist

- [ ] Standing idle for 5+ seconds triggers routine
- [ ] Routine plays correctly (animation + duration)
- [ ] Any movement input cancels routine immediately
- [ ] Returns to idle breathing after routine ends
- [ ] Doesn't repeat same routine twice in a row
- [ ] Transitions smooth (no jerky blend)
- [ ] Disabled during active waves (no sitting in combat)
- [ ] Debug log shows routine timings
- [ ] Works in WebGL build

---

## Visual Feedback

**Console output (optional):**
```
[IdleRoutine] Playing Sit (10s)
[IdleRoutine] Playing PlayDead (5s)
[IdleRoutine] Playing Stretch (3s)
```

**HUD indicator (optional):**
- Small idle state label (debug mode only)
- Useful for tuning timings

---

## Charm Details

Make routines feel natural, not mechanical:

- **Sit:** Graceful crouch → sit on heels, look peaceful
- **Play Dead:** Dramatic collapse (could add sound effect like "oof")
- **Stretch:** Arms up high, yawn, satisfied sigh
- **Fidget:** Shuffle feet, look around, glance at player

**Audio:** Optional SFX for routine starts (stretching grunt, sigh, etc.)

---

## What NOT to Touch

- Movement code (routines are purely animation)
- Combat logic (routines disabled during waves)
- Pose state (routines play within idle pose)
- Input handling (movement input always cancels)

---

## Future Enhancements

- [ ] Context-specific routines (sit on bench if near it, lean on wall)
- [ ] Emotes (wave, salute, dance)
- [ ] Pet interactions (pet Echo when idle nearby)
- [ ] Bored animation (foot tapping, impatient look)
- [ ] Victory animations (celebrate after wave)

---

## Configuration

**In IdleRoutineManager:**
- `idleTriggerTime`: 5s (delay before routine plays)
- `routineRepeatDelay`: 3s (wait between routines)
- `enabled`: Toggle on/off (disabled during combat)

---

## Acceptance Sign-Off

- [ ] Idle routines play after 5s inactivity
- [ ] Routines are cute/charming (sit, play dead, stretch, fidget)
- [ ] Any input cancels routine immediately
- [ ] Transitions smooth and natural
- [ ] Disabled during active combat
- [ ] Works in WebGL build
