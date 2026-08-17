<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK ORDER 110 — Siege Warfare: Trebuchet Enemy + Wall Breach Mechanics

**Status:** READY TO IMPLEMENT
**Date:** 2026-05-30
**Priority:** High — late-wave escalation, core loop depth
**Scope:** Large — new enemy type (SiegeUnit) + WallSegment HP + breach VFX
**Depends on:** WO-104 (castle walls with HP), WO-109 (wall-top towers),
               EnemyBrain (built), WallSegment (built)
**North Star:** Defend + Escalate — the siege phase is where player investment pays off

---

## Vision

Waves 1–3 are infantry rushes. Wave 4 is the dragon (air threat). Wave 5+ introduces
**siege warfare** — trebuchets deploy outside the moat and bombard the walls before
the infantry assault. The player must counter with long-range wall-top towers, send
the hero out to destroy siege engines before the breach, or absorb the damage and
repair afterward.

Every wall upgrade the player has made now matters. A wood wall crumbles in 3 hits.
A reinforced wall can take 10. The moat forces trebuchets to deploy at range — inside
your wall-top tower coverage. The drainage between offense and defense is the game.

---

## Enemy Codex Entry: Trebuchet (Siege Engine)

| Property | Value |
|---|---|
| Type | Siege / Stationary |
| HP | 120 |
| Speed | 0.8 m/s (deploy, then locks in place) |
| Min range | 25m (can't fire closer — moat enforces this) |
| Max range | 55m |
| Fire interval | 8 seconds |
| Projectile damage | 40 per hit (to WallSegment HP) |
| AoE radius | 4m (can hit adjacent wall segments) |
| Deploy time | 4 seconds (animation + setup before first shot) |
| Reward | 120 XP, 40 crystals |
| First appears | Wave 5 |

---

## 1. `SiegeUnit.cs` — Trebuchet enemy controller

**Path:** `Assets/_Modules/Village/Enemies/SiegeUnit.cs`

```csharp
using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using DeNelle.Core.Combat;

namespace DeNelle.Village
{
    /// <summary>
    /// Siege engine (trebuchet). Advances to firing range, deploys, then
    /// bombards the nearest wall segment on a fixed interval.
    /// Cannot attack the Heart directly — only walls and towers.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class SiegeUnit : MonoBehaviour
    {
        [Header("Stats")]
        public float maxHp          = 120f;
        public float moveSpeed      = 0.8f;
        public float deployRange    = 45f;   // stops here and deploys
        public float minFireRange   = 25f;   // moat pushes enemies back to this
        public float fireInterval   = 8f;
        public float projectileDmg  = 40f;
        public float aoeRadius      = 4f;

        [Header("Prefabs")]
        public GameObject projectilePrefab;  // cannonball / rock VFX
        public Transform  launchPoint;       // trebuchet arm tip

        [Header("VFX")]
        public ParticleSystem deployDustVfx;
        public ParticleSystem impactVfx;

        private NavMeshAgent  _agent;
        private float         _hp;
        private bool          _deployed;
        private bool          _dead;
        private Transform     _targetWall;

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _agent.speed = moveSpeed;
            _hp = maxHp;
        }

        private void Start()
        {
            StartCoroutine(SiegeRoutine());
        }

        private IEnumerator SiegeRoutine()
        {
            // Phase 1 — advance to deploy range
            _targetWall = FindNearestWallSegment();
            if (_targetWall == null) yield break;

            Vector3 deployPos = GetDeployPosition(_targetWall.position);
            _agent.SetDestination(deployPos);

            while (Vector3.Distance(transform.position, deployPos) > 2f)
                yield return new WaitForSeconds(0.3f);

            // Phase 2 — deploy (lock in place, play setup animation)
            _agent.isStopped = true;
            deployDustVfx?.Play();
            yield return new WaitForSeconds(4f);   // deploy time
            _deployed = true;

            // Phase 3 — fire loop
            while (!_dead && _targetWall != null)
            {
                yield return new WaitForSeconds(fireInterval);
                if (_dead) break;
                FireAtWall();
                _targetWall = FindNearestWallSegment();   // retarget if wall destroyed
            }

            // All walls down — advance on Heart directly
            if (!_dead)
                StartCoroutine(AdvanceOnHeart());
        }

        private void FireAtWall()
        {
            if (_targetWall == null) return;

            // Visual: launch projectile arc
            if (projectilePrefab != null && launchPoint != null)
                StartCoroutine(LaunchProjectile(_targetWall.position));

            // Damage: AoE hit to all wall segments within aoeRadius
            var hits = Physics.OverlapSphere(_targetWall.position, aoeRadius);
            foreach (var hit in hits)
            {
                var wall = hit.GetComponentInParent<IDamageableStructure>();
                wall?.ApplyContactDamage(projectileDmg);
            }

            VFXManager.Instance?.Play(VFXType.Impact_ExplosionFire, _targetWall.position);
            CameraShakeBridge.Shake(0.25f, 0.4f);
        }

        private IEnumerator LaunchProjectile(Vector3 target)
        {
            var proj = Instantiate(projectilePrefab, launchPoint.position, Quaternion.identity);
            float elapsed = 0f, duration = 1.8f;
            Vector3 start = launchPoint.position;
            Vector3 peak  = (start + target) * 0.5f + Vector3.up * 12f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                // Quadratic bezier arc
                proj.transform.position = Mathf.Pow(1-t,2)*start
                    + 2*(1-t)*t*peak + Mathf.Pow(t,2)*target;
                yield return null;
            }
            Destroy(proj);
        }

        private Vector3 GetDeployPosition(Vector3 wallPos)
        {
            // Deploy at deployRange meters from wall, outside the moat
            Vector3 dir = (transform.position - wallPos).normalized;
            return wallPos + dir * deployRange;
        }

        private Transform FindNearestWallSegment()
        {
            // Find closest active WallSegment
            WallSegment[] walls = FindObjectsOfType<WallSegment>();
            Transform nearest = null;
            float minDist = float.MaxValue;
            foreach (var w in walls)
            {
                if (!w.IsAlive) continue;
                float d = Vector3.Distance(transform.position, w.transform.position);
                if (d < minDist) { minDist = d; nearest = w.transform; }
            }
            return nearest;
        }

        private IEnumerator AdvanceOnHeart()
        {
            _agent.isStopped = false;
            var heart = FindObjectOfType<HeartController>();
            if (heart != null) _agent.SetDestination(heart.transform.position);
            yield break;
        }

        public void TakeDamage(float amount)
        {
            if (_dead) return;
            _hp -= amount;
            VFXManager.Instance?.Play(VFXType.Impact_Physical, transform.position);
            if (_hp <= 0f) Die();
        }

        private void Die()
        {
            _dead = true;
            VFXManager.Instance?.Play(VFXType.Death_EnemyExplosion, transform.position);
            CameraShakeBridge.Shake(0.4f, 0.5f);
            Destroy(gameObject, 1.5f);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, deployRange);
            Gizmos.color = new Color(1f, 0f, 0f, 0.2f);
            Gizmos.DrawWireSphere(transform.position, minFireRange);
        }
    }
}
```

---

## 2. WallSegment — add HP + breach (extends existing WallSegment.cs)

`WallSegment` already implements `IDamageableStructure`. Ensure it has:

```csharp
[Header("Siege HP")]
public float maxWallHp      = 100f;   // Wood wall default
public float currentWallHp;

// Tier multipliers (set by wall upgrade system, WO-111 wall tiers)
// Wood = 1.0×, Stone = 2.5×, Reinforced = 5.0×

public event System.Action OnBreached;   // fires when HP = 0

private void Awake()
{
    currentWallHp = maxWallHp;
}

void IDamageableStructure.ApplyContactDamage(float amount)
{
    currentWallHp -= amount;
    VFXManager.Instance?.Play(VFXType.Impact_Physical, transform.position + Vector3.up);

    if (currentWallHp <= maxWallHp * 0.3f)
        ShowDamageDecals();   // cracks/scorch marks via ground decal system

    if (currentWallHp <= 0f)
        Breach();
}

private void Breach()
{
    OnBreached?.Invoke();
    // Play destruction VFX
    VFXManager.Instance?.Play(VFXType.Death_EnemyExplosion, transform.position);
    CameraShakeBridge.Shake(0.5f, 0.6f);
    // Disable the wall mesh + collider — gap appears in the perimeter
    GetComponent<MeshRenderer>().enabled = false;
    GetComponent<Collider>().enabled = false;
    // Notify WaveManager that a breach occurred
    WaveManager.Instance?.OnWallBreached(this);
}

public void Repair(float amount)
{
    currentWallHp = Mathf.Min(currentWallHp + amount, maxWallHp);
    // Re-enable if fully repaired
    if (currentWallHp >= maxWallHp * 0.5f)
    {
        GetComponent<MeshRenderer>().enabled = true;
        GetComponent<Collider>().enabled = true;
    }
}
```

---

## 3. Trebuchet polyperfect asset

Check catalog for siege equipment:
```
grep -i "catapult\|trebuchet\|siege\|ballista\|cannon" docs/polyperfect-asset-catalog.md
```

If a siege engine exists in polyperfect, use it. Likely candidates:
- `SM_Catapult_Medieval` or `SM_Siege_Engine`
- If none: use `SM_Wagon_Medieval` + `SM_Log` as a placeholder composition

---

## 4. WaveManager — siege wave composition

Add siege wave entries to `waves.json` (or `WaveData.cs`):

```json
{
  "waveNumber": 5,
  "name": "The First Siege",
  "spawnEntries": [
    { "enemyId": "trebuchet", "count": 1, "spawnPoint": "spawn-0", "delay": 0 },
    { "enemyId": "hollow-warrior", "count": 8, "spawnPoint": "spawn-0", "delay": 30 }
  ],
  "narrative": "The ground shakes. Something large moves through the Ashwood."
}
```

Siege unit spawns first, advances to range, deploys. Infantry follows 30 seconds later — timed to arrive at the wall just as the trebuchet has softened it.

---

## 5. Player counter-options

| Counter | How |
|---|---|
| Rush the trebuchet | Hero exits gate, closes to melee range |
| Long-range tower | Wall-top `Tower_Medieval_Wood` with elevation bonus (WO-109) hits at 45m+ |
| Repair crew (future) | Mending Salve targets `WallSegment.Repair()` |
| Thick walls | Stone/Reinforced walls (WO-111 tiers) take 2.5× / 5× hits |

---

## Files to Create / Edit

| File | Action |
|---|---|
| `Assets/_Modules/Village/Enemies/SiegeUnit.cs` | **Create** |
| `Assets/_Modules/Village/Walls/WallSegment.cs` | **Edit** — add HP, Breach(), Repair() |
| `Assets/StreamingAssets/Data/Canonical/waves.json` | **Edit** — add wave 5 siege entry |
| `Assets/StreamingAssets/Data/Canonical/enemies.json` | **Edit** — add trebuchet def |

---

## Acceptance Criteria

- [ ] Trebuchet spawns at wave 5, advances to 45m from wall, then stops
- [ ] 4-second deploy animation before first shot
- [ ] Wall segment HP decreases visibly on each hit
- [ ] Crack/scorch decals appear at 30% HP
- [ ] Wall mesh disappears and gap opens when HP = 0
- [ ] Infantry follows 30 seconds after trebuchet
- [ ] Hero can close and melee the trebuchet to destroy it
- [ ] Wall-top tower (WO-109) can destroy trebuchet before it breaches
- [ ] `WallSegment.Repair()` restores HP when hero uses Mending Salve on it
- [ ] Stone wall (WO-111) survives 2.5× more hits than wood wall
