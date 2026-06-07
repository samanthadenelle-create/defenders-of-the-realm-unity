# WO-219: Visual Feedback — Hit-Stop, Screen Shake, Particles, Trails

**Status: READY TO IMPLEMENT**

**Date:** 2026-06-01  
**Priority:** 🔴 CRITICAL (makes combat feel 10x better immediately)  
**Owner:** CLI  
**Depends On:** WO-217 (needs snappy animations first)  
**Blocks:** WO-220 (audio works better after visual feedback is in place)

---

## Problem

Combat lacks **visual punch**. Attacks feel soft because there's no impact feedback. Player swings sword or casts spell, but nothing happens except enemy health bar ticks. No visual "hit" to make the action feel satisfying.

**Solution:** Add industry-standard impact effects that are proven to multiply feel quality:
- Hit-stop (brief freeze on contact)
- Screen shake (camera recoil)
- Impact particles (burst on hit)
- Attack trails (motion blur lines)
- Damage numbers (floating combat text)

---

## Solution

### 1. Hit-Stop (Time Freeze)

Brief freeze (0.05–0.15 seconds) on attack contact. Makes hits feel **heavy**.

```csharp
// Pseudocode
public class HitStopManager : MonoBehaviour
{
    public static void TriggerHitStop(float duration = 0.1f)
    {
        Time.timeScale = 0f;  // Freeze everything
        StartCoroutine(ResumeAfterDelay(duration));
    }
    
    IEnumerator ResumeAfterDelay(float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        Time.timeScale = 1f;  // Resume
    }
}

// On hit:
HitStopManager.TriggerHitStop(0.1f);  // 100ms freeze
```

### 2. Screen Shake

Camera jolts on impact. Makes hits feel **impactful**.

```csharp
// Pseudocode
public class ScreenShake : MonoBehaviour
{
    public static void Shake(float intensity = 0.2f, float duration = 0.2f)
    {
        // Jolt camera position briefly
        // Intensity: 0.2 = subtle, 0.5 = medium, 1.0 = violent
        // Duration: 0.2s = quick, 0.5s = sustained
    }
}

// On hit:
ScreenShake.Shake(0.3f, 0.2f);  // Medium shake, quick
```

### 3. Impact Particles

Burst of particles at contact point. Makes hits feel **visible**.

- **Slash hit:** White/yellow spark burst
- **Spell hit:** Colored explosion (fire=orange, ice=blue, etc.)
- **Enemy death:** Bigger explosion

```csharp
// Pseudocode
ParticleSystem spark = Instantiate(sparklePrefab, hitPosition);
spark.Play();
Destroy(spark.gameObject, spark.main.duration);
```

### 4. Attack Trails

Motion blur lines during swing/cast. Makes attacks feel **fast**.

```
Knight slash → white/silver trail as blade swings
Mage fireball → orange/red trail as ball travels
Ranger shot → arrow trail from bow to target
```

Use Unity's **LineRenderer** or **TrailRenderer**:
- Attach to weapon/hand
- Enable on attack start
- Disable on attack end
- Automatically draws line as it moves

### 5. Damage Numbers

Floating text showing damage dealt. Makes hits feel **reactive**.

```csharp
// Pseudocode
public class DamageNumber : MonoBehaviour
{
    public static void Show(Vector3 position, int damage, DamageType type)
    {
        // Instantiate floating text prefab
        // Show damage value
        // Animate upward + fade out
        // Color: red=physical, blue=ice, orange=fire, etc.
    }
}

// On hit:
DamageNumber.Show(enemy.position, 45, DamageType.Physical);
```

---

## Implementation Plan

### Phase 1: Hit-Stop
1. Create HitStopManager singleton
2. Wire to existing damage system: on hit → call TriggerHitStop(0.1f)
3. Test: attacks feel heavier

### Phase 2: Screen Shake
4. Create ScreenShake component (attach to main camera)
5. Wire to damage: on hit → call ScreenShake.Shake(0.3f, 0.2f)
6. Test: impacts feel connected to camera

### Phase 3: Impact Particles
7. Create spark/explosion particle prefabs (3–5 variants)
8. Wire to damage: on hit → instantiate particle at hit point
9. Test: visual feedback clear

### Phase 4: Attack Trails
10. Add TrailRenderer to hero weapons
11. Enable on attack start, disable on attack end
12. Test: attacks look faster

### Phase 5: Damage Numbers
13. Create damage number prefab + animator
14. Wire to damage: on hit → show number
15. Color-code by damage type (physical, ice, fire, etc.)
16. Test: numbers visible, readable

---

## Files to Create/Modify

```
Assets/_Modules/Combat/Effects/
├── HitStopManager.cs
├── ScreenShake.cs
├── DamageNumber.cs
├── ParticleManager.cs
├── Prefabs/
│   ├── Effects/
│   │   ├── SparkBurst.prefab
│   │   ├── ExplosionBurst.prefab
│   │   └── DamageNumber.prefab
│   └── VFX/
│       └── (assign to damage system)
```

---

## Acceptance Criteria

- [ ] HitStopManager created + on-hit freezes for 0.1s
- [ ] Screen shake working (intensity + duration configurable)
- [ ] Impact particles burst on hit (3+ variants)
- [ ] Attack trails visible on hero attacks
- [ ] Damage numbers float upward + fade on hit
- [ ] Colors correct (red=physical, blue=ice, orange=fire, etc.)
- [ ] No performance issues (particles culled off-screen)
- [ ] WebGL tested: all effects visible + smooth
- [ ] Commit: "WO-219: add visual feedback (hit-stop, screen shake, particles, trails, damage numbers)"

---

## Feel Checklist

After all effects are in, do this test:

1. Attack enemy with hero
2. Verify: hit-stop + screen shake + particles + trail + damage number all fire together
3. Does it feel **punchy**? If yes, next phase. If no, adjust timings.

---

## Tuning Variables

All should be **configurable** (not hardcoded):

```csharp
[SerializeField] float hitStopDuration = 0.1f;
[SerializeField] float screenShakeIntensity = 0.3f;
[SerializeField] float screenShakeDuration = 0.2f;
```

This lets designers tweak feel without recompiling.

---

## Notes

- **Order matters:** Hit-stop → screen shake → particles all fire on same frame
- Old games like **Monster Hunter** prove: good hit-stop + particles = massively improved feel
- Damage numbers should **not** block gameplay (they're cosmetic)
- Particles should fade quickly (not linger and clutter screen)

---

**Estimate:** 2–3 hours (all 5 systems + tuning + testing)
