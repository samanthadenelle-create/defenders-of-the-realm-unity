// =============================================================================
// AutoRampartDefense — automated defensive structure placed on castle ramparts/walls.
// -----------------------------------------------------------------------------
// Part of WO-108 Castle as Last Bastion. Activated when enemies approach gates/ramparts.
// Reuses existing patterns: IDamageableStructure (for targeting/damage), projectile systems
// from DefenseTower/Enemy, EnemyBrain threat logic.
// Placed by VillageSceneBuilder at rampart positions (turrets, wall segments, near gates).
// All benefits/productivity (if any) or activation costs route through EconomyService
// (the single source of truth — AddResource/Grant for any passive, TrySpend for maintenance).
// Mobile-perf: simple range scan + fire rate, poolable projectiles.
// =============================================================================
using UnityEngine;
using System.Collections.Generic;
using DeNelle.Core.Combat; // IDamageableStructure
using DeNelle.Village; // EconomyService

namespace DeNelle.Village.World
{
    /// <summary>
    /// Simple automated turret/emplacement for ramparts. Scans for hostiles in range,
    /// fires at nearest (prefers high-threat via basic role/HP heuristics).
    /// Integrates with Economy for any "maintenance" or "empower" (future).
    /// </summary>
    public sealed class AutoRampartDefense : MonoBehaviour
    {
        [Header("Defense")]
        [SerializeField] private float _range = 18f;
        [SerializeField] private float _fireRate = 1.2f;
        [SerializeField] private float _damage = 12f;
        [SerializeField] private GameObject _projectilePrefab; // reuse existing or simple sphere

        [Tooltip("Splash / AoE radius (m) on projectile impact. 0 = single-target (ARROW tower). " +
                 ">0 = area damage for CATAPULT / BALLISTA / FIRE towers — all hostiles within this " +
                 "radius of the impact take the hit. Set per tower type (catalog-ready).")]
        [SerializeField] private float _splashRadius = 0f;

        [Header("Economy Tie-in (optional maintenance)")]
        [SerializeField] private bool _hasUpkeep = false;
        [SerializeField] private ResourceCost _upkeepPerMinute = new ResourceCost(iron: 2);

        private float _nextFireTime;
        private Transform _currentTarget;
        private float _lastUpkeepTime;

        // Throttled scan + cached Enemy mask + reusable buffer (perf: don't scan every frame,
        // don't include the huge Default layer, don't allocate a Collider[] per scan).
        private float _nextScanTime;
        private const float ScanInterval = 0.25f;
        private int _enemyMask;
        private static readonly Collider[] _scanBuf = new Collider[64];

        private void Awake()
        {
            _enemyMask = LayerMask.GetMask("Enemy");
            if (_enemyMask == 0) _enemyMask = ~0;   // Enemy layer undefined -> fall back to all
        }

        /// <summary>
        /// Upgrade hook — a tower-upgrade tier calls this to "increase or extend" the turret.
        /// damageMul / fireRateMul are multipliers (fireRate is seconds-between-shots, so a value
        /// &lt;1 = faster firing); rangeAdd / splashAdd are additive metres. Catalog/upgrade-ready.
        /// </summary>
        public void ApplyUpgrade(float damageMul = 1f, float rangeAdd = 0f, float splashAdd = 0f, float fireRateMul = 1f)
        {
            _damage       = Mathf.Max(0f, _damage * damageMul);
            _range        = Mathf.Max(1f, _range + rangeAdd);
            _splashRadius = Mathf.Max(0f, _splashRadius + splashAdd);
            _fireRate     = Mathf.Clamp(_fireRate * fireRateMul, 0.05f, 10f);
        }

        private void Update()
        {
            if (_hasUpkeep && Time.time - _lastUpkeepTime > 60f)
            {
                _lastUpkeepTime = Time.time;
                EconomyService.Instance?.TrySpend(_upkeepPerMinute); // or Grant negative if supported
            }

            // Re-scan on a throttle; also rescan immediately if we have no target so we don't
            // sit idle. Dropping a dead/destroyed target happens naturally (best=null next scan).
            if (_currentTarget == null || Time.time >= _nextScanTime)
            {
                _nextScanTime = Time.time + ScanInterval;
                FindTarget();
            }
            if (_currentTarget != null && Time.time >= _nextFireTime)
            {
                Fire();
                _nextFireTime = Time.time + _fireRate;
            }
        }

        private void FindTarget()
        {
            // NonAlloc scan of the Enemy layer only (no Default-layer flood, no per-scan alloc).
            // Pick the NEAREST living damageable hostile (sqrMagnitude, not Vector3.Distance).
            int n = Physics.OverlapSphereNonAlloc(transform.position, _range, _scanBuf, _enemyMask, QueryTriggerInteraction.Collide);
            Transform best = null;
            float bestSqr = float.MaxValue;

            for (int i = 0; i < n; i++)
            {
                var hit = _scanBuf[i];
                if (hit == null || hit.transform == transform) continue;
                var dmg = hit.GetComponentInParent<IDamageableStructure>();
                if (dmg == null || !dmg.IsAlive) continue;

                float sqr = (hit.transform.position - transform.position).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = hit.transform;
                }
            }
            _currentTarget = best;
        }

        private void Fire()
        {
            if (_currentTarget == null) return;

            Vector3 dir = (_currentTarget.position - transform.position).normalized;
            Vector3 spawn = transform.position + dir * 1.5f;
            GameObject proj = _projectilePrefab
                ? Instantiate(_projectilePrefab, spawn, Quaternion.LookRotation(dir))
                : GameObject.CreatePrimitive(PrimitiveType.Sphere); // fallback
            // CreatePrimitive spawns at origin — place the fallback at the muzzle too.
            proj.transform.position = spawn;
            proj.transform.localScale = Vector3.one * 0.4f;

            // The damage applier uses OnTriggerEnter, which only fires if a collider on the
            // projectile is a TRIGGER. CreatePrimitive's SphereCollider is solid by default,
            // so it would pass through enemies dealing nothing — make it a trigger.
            var col = proj.GetComponent<Collider>();
            if (col == null) col = proj.AddComponent<SphereCollider>();
            col.isTrigger = true;

            var rb = proj.GetComponent<Rigidbody>();
            if (rb == null) rb = proj.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.linearVelocity = dir * 22f;

            // Simple damage on hit (destroy on collide / after lifetime)
            var damager = proj.AddComponent<SimpleProjectileDamager>();
            damager.Damage = _damage;
            damager.TargetLayer = _enemyMask;
            damager.SplashRadius = _splashRadius;   // 0 = single-target; >0 = AoE on impact

            Destroy(proj, 3f);
            // TODO: reuse existing projectile pool / VFX from DefenseTower if wired.
        }

        // Optional gizmo for editor
#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, _range);
        }
#endif
    }

    /// <summary>Minimal projectile damage applier (fallback; prefer existing systems).</summary>
    public sealed class SimpleProjectileDamager : MonoBehaviour
    {
        public float Damage = 10f;
        public LayerMask TargetLayer;
        public float SplashRadius = 0f;   // 0 = single-target (arrow); >0 = AoE (catapult/ballista/fire)

        private static readonly Collider[] _splashBuf = new Collider[32];

        private void OnTriggerEnter(Collider other)
        {
            if ((TargetLayer & (1 << other.gameObject.layer)) == 0) return;

            if (SplashRadius > 0f)
            {
                // AoE: damage every hostile within the splash radius of the impact point.
                int n = Physics.OverlapSphereNonAlloc(transform.position, SplashRadius, _splashBuf, TargetLayer, QueryTriggerInteraction.Collide);
                for (int i = 0; i < n; i++)
                {
                    var c = _splashBuf[i];
                    if (c == null) continue;
                    var t = c.GetComponentInParent<IDamageableStructure>();
                    if (t != null && t.IsAlive) t.ApplyContactDamage(Damage);
                }
            }
            else
            {
                var target = other.GetComponentInParent<IDamageableStructure>();
                if (target != null && target.IsAlive) target.ApplyContactDamage(Damage);
            }
            Destroy(gameObject);
        }
    }
}
