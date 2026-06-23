# WORK ORDER 66 — Boss / Special Enemy VFX Differentiation — RESULT

**Status:** DONE
**Implemented:** 2026-05-29

## Files Created / Edited

| File | Action | Braces |
|---|---|---|
| `Assets/_Modules/Village/Enemies/EliteVFXController.cs` | Created | 14/14 BALANCED |
| `Assets/_Modules/Village/Enemies/Enemy.cs` | Edited | 66/66 BALANCED |
| `Assets/_Modules/Village/Vfx/VFXType.cs` | Edited | 2/2 BALANCED (shared with WO-59) |
| `Assets/_Modules/Village/Vfx/VFXManager.cs` | Edited | 44/44 BALANCED (shared with WO-59) |

## What Was Done

### EliteVFXController.cs
- `DeNelle.Village` namespace, no `[RequireComponent]` so it can be added freely to any enemy prefab.
- Inspector booleans `isElite` / `isBoss` control which VFX tier fires.
- `Start()`: caches `_auraLight` intensity as `_baseAuraIntensity` (avoids drift over multiple pulse cycles), plays `auraParticles`, starts `PulseAura()` and `DramaticSpawnRoutine()` coroutines.
- `PulseAura()`: infinite coroutine, `Mathf.Sin` drives `_auraLight.intensity` between 60% and 130% (elite) or 180% (boss) of base — clear visual distinction.
- `DramaticSpawnRoutine()`: waits `spawnDramaticDelay` seconds (default 0.5 s), then plays `VFXType.Boss_Spawn` or `VFXType.Elite_Spawn` via `VFXManager.Play()`, then fires `CameraShakeBridge.Shake(0.5f, 0.5f)` for boss or `(0.25f, 0.3f)` for elite.
- `OnEliteDeath()`: plays `Boss_Death` or `Elite_Death` VFX, fires `CameraShakeBridge.Shake(0.7f, 0.7f)` (boss) or `(0.3f, 0.3f)` (elite). Called by Enemy.cs Die() — no return value needed.
- `OnEliteAttack(Vector3 hitPos)`: plays `Boss_AttackImpact` or `Impact_ExplosionAether` (elite), boss shake `(0.25f, 0.25f)`.
- Audio hooks present but commented — wired when `SfxId.BossSpawn` / `SfxId.BossDeath` land.

### Enemy.cs — elite death hook
Inserted at the top of the `if (killed)` death block, before the normal VfxPool path:
```csharp
var eliteVfx = GetComponent<EliteVFXController>();
if (eliteVfx != null)
{
    eliteVfx.OnEliteDeath();   // handles VFX + heavy camera shake
}
else
{
    // DEF-46: per-type VFX / VfxPool fallback (unchanged)
}
// Audio always plays; regular 0.18/0.22 shake only fires when eliteVfx is null.
```
- Preserves all existing DEF-46, DEF-48, DEF-45, DEF-32, DEF-88 logic.
- Null-safe: `GetComponent<EliteVFXController>()` returns null for all regular enemies with zero cost.

### VFXType.cs additions (WO-66 section)
```
Elite_Spawn, Elite_Death, Boss_Spawn, Boss_Death, Boss_AttackImpact
```

### VFXManager.cs additions (WO-66 section)
- VfxToSfx: Elite_Death / Boss_Death → SfxId.EnemyDeath; Boss_AttackImpact → SfxId.Shockwave; spawns → SfxId.None (no clip yet).
- ProceduralFallback: each new type gets a distinct AbilityVfxKit call with appropriate scale (Boss_Death = Meteor at 4×, Boss_AttackImpact = Cleave at 3×, etc.).

## Adaptation Notes

- `CameraShakeManager` / `ShakeTier` do not exist. Used `CameraShakeBridge.Shake(float, float)` throughout — same pattern as PortalVFXController and CombatFeedbackManager. Heavy ≈ 0.5–0.7 intensity, Medium ≈ 0.25–0.3.
- EnemyBrain has no `TryAttack()` method (contact attacks live in Enemy.TickContactAttack). `OnEliteAttack()` is ready on the component — call it from whichever attack codepath fires when an elite lands a hit.

## Inspector Tasks (for Samantha / art team)

For each boss/elite prefab:
1. Add `EliteVFXController` component.
2. Set `isBoss = true` or `isElite = true`.
3. Add a pulsing aura `ParticleSystem` child (Mirza Beig dark energy loop).
4. Add a `Light` child (intense purple or red, range 4–6 m).
5. Wire `auraParticles` slot if the PS is not the first child.

Register prefabs in VFXCatalog for the 5 new VFXType entries:
- `Elite_Spawn`: rising dark energy, lifetime 1 s.
- `Boss_Spawn`: dark energy + lightning, lifetime 2 s.
- `Boss_Death`: large explosion + lingering smoke, lifetime 4 s.
- `Boss_AttackImpact`: oversized shockwave ring, lifetime 1.5 s.

## Acceptance Criteria Check

- [x] Boss spawn fires `Boss_Spawn` VFX + `CameraShakeBridge.Shake(0.5f, 0.5f)`
- [x] Boss aura pulses 60–180% intensity via `PulseAura()` coroutine
- [x] Boss death fires `Boss_Death` VFX + heavy shake (0.7f, 0.7f)
- [x] Elite enemies use smaller spawn/death VFX types (`Elite_Spawn` / `Elite_Death`)
- [x] Regular enemies unaffected — `GetComponent<EliteVFXController>()` returns null, existing path runs unchanged
- [x] All VFX calls null-safe via static `VFXManager.Play()` with internal `Instance?.` guard
