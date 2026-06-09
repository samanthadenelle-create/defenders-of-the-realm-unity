# WO-359: Combat Feedback System — Hits, Screen Shake, Parry Slowmo, VFX

**Status:** READY TO IMPLEMENT  
**Estimated Effort:** P1–P2 (3–5 days)  
**Priority:** High (core game feel)  
**Lane:** VFX/Audio (can run parallel with Combat/AI)

---

## Overview

Implement comprehensive combat feedback for attacking and defending:

1. **Hit Feedback** — World-space impact effects (blood spray, sparks, dust) at hit location
2. **Screen Shake** — Camera kick on successful hits (scales with damage)
3. **Parry Slowmo** — Brief time-scale reduction (0.3x) when parrying enemy attacks in first few tiles
4. **Visual Indicators** — Distinct VFX for weapon hits, spell impacts, and shield parries (color-coded by type)

**Why:** Combat currently feels floaty — hits have no weight. Screen shake + slowmo + VFX make timing, damage, and defense feel impactful and readable.

---

## Acceptance Criteria

- [ ] Impact VFX spawn at hit location (blood/sparks/dust based on hit type)
- [ ] Screen shake triggers on successful hits (configurable magnitude)
- [ ] Parry within hero's first 3 tiles triggers 0.2–0.3s slowmo (0.2x–0.5x timescale)
- [ ] Parry slowmo has visual indicator (VFX + UI feedback)
- [ ] Weapon hits: orange/gold sparks + impact ring
- [ ] Spell hits: blue/purple energy burst + damage numbers
- [ ] Shield blocks: bright white flash + block counter badge
- [ ] Each feedback type has audio cue (slash, impact, parry success)
- [ ] Feedback scales with damage (bigger hit = more shake)
- [ ] Works in battle (hero vs enemies, tower vs enemies)
- [ ] Zero GC allocation per hit (cache VFX, pool particles)

---

## Files to Create

### New Files
- `Assets/_Modules/VFX/CombatFeedbackManager.cs` — Central hub for hit feedback
- `Assets/_Modules/VFX/ImpactVFXSpawner.cs` — World-space impact effects
- `Assets/_Modules/VFX/ParrySlowmoController.cs` — Parry time-scale management
- `Assets/_Modules/VFX/HitIndicator.cs` — Visual & audio feedback for hit types

### No Changes Required
- Combat hit detection (use existing `IDamageableStructure.TakeDamage()` hook)
- Camera system (just add shake function)

---

## Design Spec

### Hit Feedback Types

| Type | VFX | Color | Audio | Behavior |
|------|-----|-------|-------|----------|
| **Weapon Hit** | Impact ring + slash spark burst | Orange/gold | Metal slash + impact | Screen shake (0.2s, 2–5 units) |
| **Spell Hit** | Energy orb explosion + afterglow | Blue/purple | Spell impact + crackle | Screen shake (0.3s, 3–7 units) |
| **Shield Block** | White flash + shield bash ring | Bright white | Block sound + chime | Small shake (0.1s, 0.5–1 units) |
| **Parry** | Slowmo trigger + parry counter | Gold glow on hero | Parry success sound + bell | Timescale 0.3x for 0.2s |

### Screen Shake Parameters

```csharp
public struct ScreenShakeData
{
    public float duration = 0.2f;      // How long to shake
    public float magnitude = 2f;       // Max offset (units)
    public float frequency = 10f;      // Shake speed (Hz)
    public float damageScale = 1f;     // Multiply magnitude by (damage / 10)
}

// Example: 20 damage hit → shake magnitude = 2 * (20/10) = 4 units
```

### Parry Slowmo Logic

**Trigger condition:**
- Hero's shield is raised (blocking state)
- Enemy attack lands within 3 tiles (9m) of hero
- Hero has stamina to parry (optional cost: 10 stamina)

**Effect:**
- Time.timeScale → 0.3 for 0.2 seconds
- Hero animation plays parry reaction
- Enemy frozen mid-attack
- VFX: Gold slash spark around hero's position
- HUD: "+Parry" counter badge (green)
- Audio: Success chime + hero grunt

**Visual indicator:**
- Screen edge flash (white, 50% opacity, 0.1s)
- Slow-motion blur trail on enemy
- Slowmo UI label "PARRY!" (fade out over 0.5s)

### Impact VFX Specifications

**Weapon Hit (Slash):**
```
- Position: Hit point (world space)
- Particles: 6–12 orange sparks, 45° cone spread
- Duration: 0.3s
- Size: 0.1–0.3m
- Rotation: Align with attack direction
- Emission: 1x burst (not looped)
```

**Spell Hit (Explosion):**
```
- Position: Spell impact location
- Particles: Blue/purple energy cloud, spherical spread
- Duration: 0.5s
- Size: 0.3–0.8m (scales with spell power)
- Glow: Additive blend, bright core fading to transparent
- Shockwave: Concentric ring, grows outward 0.5m/s
```

**Shield Block (Flash):**
```
- Position: Hero center
- Flash: Full-screen white (0.3 alpha) for 0.1s
- Particles: White/silver ring, 0.5m radius, 0.2s lifetime
- Glow: Shield mesh brightens to white, fades
- Ring grows outward while fading
```

### Audio Cues

- **Weapon hit:** `AudioId.SlashImpact` (varies by weapon type)
- **Spell hit:** `AudioId.SpellExplode` (varies by spell school)
- **Shield block:** `AudioId.ShieldBlock` + `AudioId.BlockChime`
- **Parry success:** `AudioId.ParrySuccess` + `AudioId.MomentumBell`

All cues mix at 0.7–1.0 volume, spatial (3D positioning at hit location).

---

## Implementation Notes

### CombatFeedbackManager.cs (Singleton)

```csharp
public sealed class CombatFeedbackManager : MonoBehaviour
{
    public static void OnHit(HitInfo hit)
    {
        // Log the hit type
        // Spawn impact VFX at hit.position
        // Screen shake based on damage
        // Play audio cue
        
        if (hit.type == HitType.Weapon)
            SpawnWeaponImpact(hit);
        else if (hit.type == HitType.Spell)
            SpawnSpellImpact(hit);
    }
    
    public static void OnParry(Vector3 heroPos)
    {
        // Trigger slowmo
        // Spawn parry VFX
        // Play parry audio
        // Flash screen
        // Show UI feedback
        
        ParrySlowmoController.Activate(0.3f, 0.2f);
        ImpactVFXSpawner.SpawnParryEffect(heroPos);
        AudioService.PlayCue(AudioId.ParrySuccess);
    }
}
```

### Integration Points

**In EnemyBrain.cs (attack hit):**
```csharp
private void OnAttackHit(IDamageableStructure target, int damage)
{
    // ... existing damage logic ...
    
    CombatFeedbackManager.OnHit(new HitInfo
    {
        position = target.Position,
        type = HitType.Weapon,
        damage = damage,
        direction = transform.forward
    });
}
```

**In HeroHealth.cs (incoming parry):**
```csharp
public void TakeDamage(int amount, Vector3 hitDirection)
{
    if (IsParrying && Vector3.Distance(IncomingAttackOrigin, transform.position) < 9f)
    {
        CombatFeedbackManager.OnParry(transform.position);
        // ... parry logic (reduce/negate damage) ...
    }
    else
    {
        // ... normal hit ...
        CombatFeedbackManager.OnHit(new HitInfo { ... });
    }
}
```

**In SmartMobileCamera.cs (shake):**
```csharp
public void Shake(ScreenShakeData data)
{
    StartCoroutine(ShakeCoroutine(data));
}

private IEnumerator ShakeCoroutine(ScreenShakeData data)
{
    float elapsed = 0;
    while (elapsed < data.duration)
    {
        float t = elapsed / data.duration;
        float amplitude = data.magnitude * (1 - t);  // Decay over time
        _camera.transform.localPosition += Random.insideUnitSphere * amplitude;
        elapsed += Time.deltaTime;
        yield return null;
    }
}
```

### VFX Pool Strategy

Use object pooling to avoid GC allocations:

```csharp
private Queue<ParticleSystem> _impactPool = new Queue<ParticleSystem>();

private ParticleSystem GetImpactEffect()
{
    if (_impactPool.Count > 0)
        return _impactPool.Dequeue();
    
    // Instantiate new if pool empty
    var ps = Instantiate(impactPrefab);
    ps.GetComponent<ParticleSystem>().Stop();
    return ps;
}

private void ReturnToPool(ParticleSystem ps)
{
    ps.Stop();
    ps.gameObject.SetActive(false);
    _impactPool.Enqueue(ps);
}
```

---

## Testing Checklist

- [ ] Hit VFX spawns at correct location (world space, aligned with hit direction)
- [ ] Screen shake triggers on hit (duration and magnitude correct)
- [ ] Weapon hits display orange sparks + slash sound
- [ ] Spell hits display blue energy burst + spell sound
- [ ] Shield blocks display white flash + block chime
- [ ] Parry within 3 tiles triggers slowmo (0.3x timescale, 0.2s duration)
- [ ] Parry slowmo has visual feedback (screen flash, UI label)
- [ ] Slowmo doesn't break animations or AI (time-aware)
- [ ] Feedback scales with damage (bigger hit = bigger shake)
- [ ] Audio plays without cutoff (mix is clean)
- [ ] Zero allocations during combat (profile with IL2CPP)
- [ ] Works in both hero combat and tower vs enemy combat
- [ ] Works in WebGL build

---

## What NOT to Touch

- Damage calculation (use existing TakeDamage values)
- Parry cost/stamina (if added, do in separate WO)
- Animation system (only play animations on hero, not enemies during slowmo)
- Difficulty scaling (feedback is cosmetic, not tied to damage values)

---

## Dependencies

- **Depends on:** Combat/AI systems (EnemyBrain, HeroHealth, IDamageableStructure)
- **Unblocks:** Combat feel polish, game balance tuning
- **Parallel:** Weapon/spell balance (this WO is purely feedback)

---

## Performance Notes

**Target:** 60 FPS on mobile (even with multiple hits/frame)

- Pool all VFX instances (no instantiate/destroy per hit)
- Use GPU instancing for particle systems
- Batch audio calls (AudioService handles mixing)
- Screen shake uses lerp (no allocations)
- Slowmo is a single Time.timeScale change (cheap)

**Memory:** ~5MB for VFX prefabs + pool (10 instances each type)

---

## Content TODO

Before implementing:
- [ ] Confirm audio cues exist (SlashImpact, SpellExplode, ShieldBlock, ParrySuccess, MomentumBell)
- [ ] Create/assign VFX prefabs (Weapon, Spell, Block, Parry)
- [ ] Tune screen shake magnitude per damage range
- [ ] Define parry detection radius (currently 3 tiles = 9m, adjust as needed)

---

## Acceptance Sign-Off

- [ ] All hit feedback types implemented and tested
- [ ] Parry slowmo works reliably (no timescale glitches)
- [ ] VFX pool prevents GC allocation
- [ ] Audio cues play cleanly (no clipping or cutoff)
- [ ] Works in both editor and WebGL build
- [ ] Combat feel noticeably improved (subjective, but testable with playtesters)
