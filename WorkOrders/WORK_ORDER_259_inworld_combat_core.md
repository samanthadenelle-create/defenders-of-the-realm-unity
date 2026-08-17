<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WO-259: In-World Combat Core

**Status: READY TO IMPLEMENT**

**Date:** 2026-06-01  
**Priority:** 🟡 HIGH (Phase 3 feature work, combat feel priority)  
**Owner:** CLI  
**Time Estimate:** 2–3 hours  
**Unblocks:** WO-237 (Hero Movement Refactor), combat feel improvements (WO-217/218/219)  
**Depends on:** WO-232 (Silo restructuring complete)

---

## Problem Statement

Current in-world 3D combat feels unresponsive and unpolished:
- Enemies don't animate smoothly during attacks
- No 3D spatial audio feedback (attacks feel muted)
- Hero movement is choppy, not responsive to input
- Multiple enemies (3+) don't coordinate well
- Attack timing has no visual impact

**Solution:** Implement core in-world combat systems with clean animation bridges, 3D audio, and multi-enemy support.

---

## What Gets Built

### 1. EnemyController.cs (Core Combat Logic)
- Enemy detection (25m range)
- Attack range (3m)
- Combat state (in/out of combat)
- Attack cooldown (2.2s)
- Coordination with WorldCombatManager

**Key methods:**
- `EnterCombat()` — Switch to combat behavior
- `ExitCombat()` — Return to idle
- `AttackPlayer()` — Execute attack + sound
- `TakeDamage(damage)` — Hit reaction
- `Die()` — Death sequence

---

### 2. EnemyCombatAnimator.cs (Animation Bridge)
- State management (Speed, InCombat, Attack, Hit, Death)
- Smooth blending (Idle → Walk → Run)
- Attack variation (cycle through 2–3 attacks)
- Hit reactions (flinch animation)
- Death animation

**Key methods:**
- `EnterCombat()` — Set InCombat=true
- `ExitCombat()` — Set InCombat=false
- `PlayAttack()` — Trigger attack animation
- `PlayHitReaction()` — Trigger flinch animation
- `PlayDeath()` — Trigger death animation

---

### 3. EnemyAudio.cs (3D Spatial Sound)
- 3D audio sources (spatialBlend=1.0)
- Attack sounds (random from array)
- Hit reaction sounds (pain/grunt)
- Death sounds (final cry)
- Footstep sounds (synced to animation events)

**Settings:**
- Max distance: 40m (can be heard across entire village)
- Rolloff mode: Linear (realistic attenuation)
- PlayOnAwake: false (triggered by events)

---

### 4. WorldCombatManager.cs (Multi-Enemy Coordination)
- Tracks active enemies in combat (up to 3+)
- Singleton pattern (like AudioService)
- Manages enemy entry/exit from combat state
- Provides query methods (GetActiveEnemyCount, etc.)

**Key methods:**
- `AddEnemy(enemy)` — Register entering combat
- `RemoveEnemy(enemy)` — Unregister leaving combat
- `GetActiveEnemyCount()` — Query count for difficulty scaling

---

### 5. HeroLocomotion.cs (Refactored Hero Movement)
**What's New:**
- Clean input handling (keyboard + gamepad)
- NavMeshAgent control (smooth agent-based movement)
- Smooth velocity damping (no jerky acceleration)
- Manual rotation (responsive turning)
- Animation bridge integration (calls HeroAnimator)

**Key methods:**
- `Update()` — Read input, move agent, update animator
- `PlayAttack()` — Trigger hero attack animation
- `PlayHit()` — Trigger hero hit reaction

**Improvements over current:**
- No more jittery movement
- Gamepad support included
- Diagonal movement normalized properly
- Rotation smooth and responsive

**Code:**
```csharp
using UnityEngine;
using UnityEngine.InputSystem;

namespace DeNelle.Combat
{
    [DisallowMultipleComponent]
    public sealed class HeroLocomotion : MonoBehaviour
    {
        [Header("Movement Settings")]
        [SerializeField] private float moveSpeed = 6.8f;
        [SerializeField] private float rotationSpeed = 18f;

        [Header("References")]
        [SerializeField] private HeroAnimator heroAnimator;

        private NavMeshAgent agent;
        private Vector3 currentVelocity;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>() ?? gameObject.AddComponent<NavMeshAgent>();

            agent.speed = 30f;
            agent.acceleration = 200f;
            agent.angularSpeed = 0f;
            agent.updateRotation = false;
            agent.updateUpAxis = false;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;
        }

        private void Update()
        {
            Vector2 input = ReadInput();
            Vector3 desiredMove = new Vector3(input.x, 0f, input.y).normalized * moveSpeed;

            currentVelocity = Vector3.MoveTowards(currentVelocity, desiredMove, 45f * Time.deltaTime);

            if (currentVelocity.sqrMagnitude > 0.01f)
            {
                agent.Move(currentVelocity * Time.deltaTime);

                Quaternion targetRot = Quaternion.LookRotation(currentVelocity);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
            }

            heroAnimator?.UpdateMovement(currentVelocity.magnitude);
        }

        private Vector2 ReadInput()
        {
            Vector2 input = Vector2.zero;

            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) input.y += 1;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) input.y -= 1;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) input.x += 1;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) input.x -= 1;
            }

            var gp = Gamepad.current;
            if (gp != null)
            {
                Vector2 stick = gp.leftStick.ReadValue();
                if (stick.sqrMagnitude > 0.1f) input += stick;
            }

            if (input.sqrMagnitude > 1f) input.Normalize();
            return input;
        }

        public void PlayAttack() => heroAnimator?.PlayAttack();
        public void PlayHit() => heroAnimator?.PlayHit();
    }
}
```

---

### 6. HeroAnimator.cs (NEW - Animation State Manager)
Separated from HeroLocomotion to keep concerns clean.

**Owns:**
- Speed parameter updates
- Attack/Hit/Death trigger calls
- All animation state management

**Key methods:**
- `UpdateMovement(speed)` — Update Speed animator parameter
- `PlayAttack()` → SetTrigger(Attack)
- `PlayHit()` → SetTrigger(Hit)
- `PlayDeath()` → SetTrigger(Death)

**Code:**
```csharp
using UnityEngine;

namespace DeNelle.Combat
{
    public class HeroAnimator : MonoBehaviour
    {
        private Animator animator;

        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int AttackHash = Animator.StringToHash("Attack");
        private static readonly int HitHash = Animator.StringToHash("Hit");
        private static readonly int DeathHash = Animator.StringToHash("Death");

        private void Awake()
        {
            animator = GetComponentInChildren<Animator>();
        }

        public void UpdateMovement(float speed)
        {
            if (animator != null)
                animator.SetFloat(SpeedHash, speed);
        }

        public void PlayAttack() => animator?.SetTrigger(AttackHash);
        public void PlayHit()    => animator?.SetTrigger(HitHash);
        public void PlayDeath()  => animator?.SetTrigger(DeathHash);
    }
}
```

---

## Integration Steps

### Step 1: Create Silo.Combat/ Folder Structure
```
Assets/Scripts/Silo.Combat/
├── Combat/
│   ├── EnemyController.cs
│   ├── EnemyCombatAnimator.cs
│   ├── EnemyAudio.cs
│   └── WorldCombatManager.cs
├── Hero/
│   ├── HeroLocomotion.cs (refactored)
│   └── HeroAnimator.cs (new)
└── Battle/
    ├── BattleController.cs (moved from Phase 0)
    ├── BattleHud.cs (moved from Phase 0)
    └── (other battle files)
```

### Step 2: Update Namespaces
All files use `namespace DeNelle.Combat.* { }`

Example:
```csharp
namespace DeNelle.Combat
{
    public class EnemyController : MonoBehaviour { ... }
}
```

### Step 3: Set Up Hero Prefab
1. Replace HeroLocomotion component with refactored version
2. Add HeroAnimator component (new)
3. On HeroLocomotion: drag child Animator into `heroAnimator` field
4. Test movement with WASD

### Step 4: Set Up Enemy Prefab
1. Add EnemyController component
2. Add EnemyCombatAnimator component
3. Add EnemyAudio component
4. Assign audio clips to EnemyAudio fields:
   - `attackClips[]` (2–3 attack sounds)
   - `hitClips[]` (1–2 pain sounds)
   - `deathClips[]` (1 death sound)
5. Test: Move near enemy, should enter combat state

### Step 5: Set Up WorldCombatManager
1. Create empty GameObject in Village scene called "WorldCombatManager"
2. Add WorldCombatManager.cs component
3. This is a singleton — the script does the Awake() → Instance = this setup

### Step 6: Wire Animator Triggers
Your Animator Controller needs these parameters:

| Parameter | Type | Purpose |
|-----------|------|---------|
| Speed | Float | Movement velocity (Idle → Walk → Run) |
| InCombat | Bool | Switch combat-idle animations |
| Attack | Trigger | Play attack animation |
| Cast | Trigger | Play spell/cast animation |
| Hit | Trigger | Play flinch animation |
| Death | Trigger | Play death animation |

Create a Blend Tree for Speed (Idle at 0, Walk at 3, Run at 6+).

### Step 7: Add Animation Events
On each attack animation, add an Animation Event at the impact frame:
```
Event: DealDamage()
Time: 0.5s (middle of swing)
```

This triggers damage at the right visual moment.

---

## Code Summary

### EnemyController.cs
```csharp
public class EnemyController : MonoBehaviour
{
    public float attackDamage = 18f;
    public float attackCooldown = 2.2f;
    public float detectionRange = 25f;
    public float attackRange = 3f;
    
    public EnemyCombatAnimator combatAnimator;
    public EnemyAudio enemyAudio;
    
    public void EnterCombat() { /* ... */ }
    public void ExitCombat() { /* ... */ }
    private void AttackPlayer() { /* ... */ }
    public void TakeDamage(float damage) { /* ... */ }
    public void Die() { /* ... */ }
}
```

### HeroLocomotion.cs (Refactored)
```csharp
public sealed class HeroLocomotion : MonoBehaviour
{
    public float moveSpeed = 6.5f;
    public float rotationSpeed = 15f;
    
    public HeroAnimator heroAnimator;
    
    private Vector3 currentVelocity;
    
    private void Update()
    {
        Vector2 input = ReadMoveInput();
        Vector3 desiredMove = new Vector3(input.x, 0f, input.y).normalized * moveSpeed;
        currentVelocity = Vector3.MoveTowards(currentVelocity, desiredMove, 40f * Time.deltaTime);
        
        // Move via NavMeshAgent
        agent.Move(currentVelocity * Time.deltaTime);
        
        // Rotate smoothly
        Quaternion targetRot = Quaternion.LookRotation(currentVelocity);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
        
        // Update animator
        heroAnimator?.UpdateMovement(currentVelocity.magnitude);
    }
}
```

### HeroAnimator.cs (NEW)
```csharp
public class HeroAnimator : MonoBehaviour
{
    private Animator animator;
    
    public void UpdateMovement(float speed) => animator?.SetFloat(SpeedHash, speed);
    public void PlayAttack() => animator?.SetTrigger(AttackHash);
    public void PlayCast() => animator?.SetTrigger(CastHash);
    public void PlayHit() => animator?.SetTrigger(HitHash);
}
```

### WorldCombatManager.cs
```csharp
public class WorldCombatManager : MonoBehaviour
{
    public static WorldCombatManager Instance;
    private List<EnemyController> activeEnemies = new List<EnemyController>();
    
    private void Awake() => Instance = this;
    
    public void AddEnemy(EnemyController enemy)
    {
        if (!activeEnemies.Contains(enemy))
            activeEnemies.Add(enemy);
    }
    
    public void RemoveEnemy(EnemyController enemy) => activeEnemies.Remove(enemy);
    public int GetActiveEnemyCount() => activeEnemies.Count;
}
```

---

## Acceptance Criteria

- [ ] All 6 scripts created in Silo.Combat/
- [ ] All files use DeNelle.Combat.* namespaces
- [ ] Project compiles with zero errors
- [ ] Hero prefab updated with new components
- [ ] Enemy prefab updated with new components
- [ ] WorldCombatManager singleton in Village scene
- [ ] Animator has all required parameters (Speed, InCombat, Attack, Cast, Hit, Death)
- [ ] Blend tree created for Speed movement
- [ ] Animation events added to attack animations (DealDamage at 0.5s)
- [ ] Test: Hero moves smoothly with WASD + gamepad
- [ ] Test: Enemy detects hero at 25m, enters combat
- [ ] Test: Enemy attacks hero (sound + animation)
- [ ] Test: Hero takes damage, plays hit reaction
- [ ] Test: Multiple enemies (3) attack simultaneously (WorldCombatManager tracking)
- [ ] Test: Enemy dies, plays death animation, removes from manager
- [ ] Console clean (no errors, no namespace warnings)
- [ ] Brace balance check passes on all 6 files (CLAUDE.md rule)
- [ ] Commit: "WO-259: Implement in-world combat core with animation bridges + 3D audio"

---

## Testing Checklist

After integration complete:

```
[Village Scene]
✓ Hero moves smoothly (WASD)
✓ Hero rotates smoothly (turns face direction of movement)
✓ Gamepad stick moves hero
✓ Enemy 25m away — no combat
✓ Move within 25m → Enemy enters combat state
✓ Enemy plays walk animation
✓ Within 3m → Enemy attacks
✓ Attack animation plays
✓ Attack sound plays (3D spatial, fades with distance)
✓ Hero takes damage (HUD shows health)
✓ Hero plays hit reaction
✓ Hero counter-attacks (if hero attack system wired)
✓ 3 enemies attack simultaneously (no lag, WorldCombatManager tracking)
✓ Kill enemy → Death animation plays, sound plays, removed from manager
✓ Move 25m away → Enemy stops attacking, exits combat

[Console Output]
✓ No errors
✓ No namespace conflicts
✓ WorldCombatManager shows active enemy count
```

---

## What This Enables

Once WO-259 completes:
- **WO-237** (Hero Movement Refactor) can polish feel further
- **WO-217/218/219** (Combat Feel) can add animation polish on top of solid foundation
- **WO-216** (Enemy Camps) can spawn multiple combat encounters
- Full village playtest with responsive combat

---

## Known Limitations (Acceptable for Phase 3A)

- No special abilities yet (just Attack)
- No hero health system yet (takes damage but no visible health)
- No loot/rewards yet (enemies just disappear)
- No difficulty scaling (always same enemy stats)

These are Phase 3B+ work (WO-220+).

---

## Timeline

- Create 6 scripts: 30 min
- Update namespaces + integration: 45 min
- Set up Animator controller: 30 min
- Testing + iteration: 45 min

**Total: 2–3 hours**

---

## Commit Message

`"WO-259: In-world combat core — EnemyController, Animator, Audio, HeroLocomotion refactor"`

---

**This is the combat foundation. Everything else (feel, effects, difficulty) layers on top of this.**
