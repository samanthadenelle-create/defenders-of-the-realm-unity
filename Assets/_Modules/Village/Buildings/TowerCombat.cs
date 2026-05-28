// =============================================================================
// TowerCombat — WO-82. Auto-fire: a built tower acquires the nearest live enemy
// in range and fires a pooled projectile at it, scaling with the tower's level.
// -----------------------------------------------------------------------------
// Reconciled to this project (per WO-82 key reconciliations):
//   • Targets via WaveManager.LiveEnemies (zero-GC, authoritative) — NOT
//     OverlapSphere; WaveManager is not a singleton here, so we cache a ref.
//   • Each live Enemy's IDamageable is its EnemyDamageable adapter; validity via
//     IsAlive, faction via Faction (enemies are Hostile), position via WorldPosition.
//   • Range/damage come from the tower's per-level TowerData (CurrentRange /
//     CurrentDamage); fire rate speeds up with level (cooldown / CurrentLevel).
//   • Damage is dealt by the pooled projectile via TakeDamage(amount, DamageElement).
//   • Detection is throttled (only runs on the fire tick / a short idle re-scan),
//     never an OverlapSphere every frame.
// Added at runtime by Tower.EnsureCombat once the tower is built; the FirePoint
// child it creates is resolved here in Awake.
// =============================================================================

using UnityEngine;
using DeNelle.Core.Combat;

namespace DeNelle.Village
{
    /// <summary>Per-tower auto-fire controller. Attached by Tower once built.</summary>
    [RequireComponent(typeof(Tower))]
    public sealed class TowerCombat : MonoBehaviour
    {
        [Header("Combat (level scales range/damage via TowerData; fire rate via level)")]
        [SerializeField] private float _baseCooldown  = 1.1f;   // seconds between shots at level 1
        [SerializeField] private float _idleRescan     = 0.2f;  // re-scan cadence when no target
        [SerializeField] private float _fallbackRange  = 12f;   // used only if TowerData has no range
        [SerializeField] private float _fallbackDamage = 22f;   // used only if TowerData has no damage

        private Tower _tower;
        private Transform _firePoint;
        private WaveManager _wave;
        private float _nextAttackTime;

        private void Awake()
        {
            _tower = GetComponent<Tower>();
            _firePoint = transform.Find("FirePoint");   // created by Tower.EnsureCombat
            if (_firePoint == null) _firePoint = transform;
            ResolveWave();
        }

        private void ResolveWave()
        {
            var found = FindObjectsByType<WaveManager>(FindObjectsSortMode.None);
            _wave = found.Length > 0 ? found[0] : null;
        }

        private void Update()
        {
            if (Time.time < _nextAttackTime) return;

            float range = _tower != null && _tower.CurrentRange > 0f ? _tower.CurrentRange : _fallbackRange;
            IDamageable target = FindBestTarget(range);

            if (target == null)
            {
                _nextAttackTime = Time.time + _idleRescan;   // throttled re-scan while idle
                return;
            }

            FireAt(target);
            float level = _tower != null ? Mathf.Max(1, _tower.CurrentLevel) : 1;
            _nextAttackTime = Time.time + (_baseCooldown / level);   // higher level = faster
        }

        private IDamageable FindBestTarget(float range)
        {
            if (_wave == null) { ResolveWave(); if (_wave == null) return null; }

            var list = _wave.LiveEnemies;
            if (list == null) return null;

            Vector3 myPos = transform.position;
            float maxSq = range * range;
            float bestSq = float.MaxValue;
            IDamageable best = null;

            for (int i = 0; i < list.Count; i++)
            {
                var enemy = list[i];
                if (enemy == null) continue;

                float sq = (enemy.transform.position - myPos).sqrMagnitude;
                if (sq > maxSq || sq >= bestSq) continue;

                var dmg = enemy.GetComponent<EnemyDamageable>();   // the IDamageable adapter
                if (dmg == null || !dmg.IsAlive || dmg.Faction != CombatFaction.Hostile) continue;

                bestSq = sq;
                best = dmg;
            }
            return best;
        }

        private void FireAt(IDamageable target)
        {
            if (ProjectilePool.Instance == null) return;

            Vector3 firePos = _firePoint != null ? _firePoint.position : transform.position;
            var proj = ProjectilePool.Instance.GetProjectile();
            proj.transform.position = firePos;

            float damage = _tower != null && _tower.CurrentDamage > 0f ? _tower.CurrentDamage : _fallbackDamage;
            proj.Initialize(target, damage, DamageElement.None);

            // ── VFX: muzzle flash at the fire point ──────────────────────────
            // Choose VFXType based on the tower's level — higher levels get
            // a more dramatic muzzle effect.
            int level = _tower != null ? _tower.CurrentLevel : 1;
            var muzzleType = level >= 3
                ? VFXType.Cast_MageCharge          // L3 towers get a charged burst
                : VFXType.Projectile_TowerArcane;  // L1-L2 standard arcane orb
            VFXManager.Play(muzzleType, firePos);

            // ── Hit Stop: light tier for standard tower shots ─────────────────
            // (feels better than no feedback at all; heavy shots escalate in
            //  TowerEmpowerment.ApplyEmpowermentEffect when wired)
            HitStopManager.DoImpact(HitTier.Light);
        }

        /// <summary>
        /// Called externally (e.g. by PooledProjectile.OnHit) to trigger an
        /// impact VFX at the hit position.  Determines element from the tower's
        /// current level and empowerment state.
        /// </summary>
        public void OnProjectileImpact(Vector3 hitPosition)
        {
            int level = _tower != null ? _tower.CurrentLevel : 1;

            // Basic element escalation by level.
            var impactType = level switch
            {
                3 => VFXType.Impact_ExplosionAether,
                2 => VFXType.Impact_Aether,
                _ => VFXType.Impact_Physical,
            };
            VFXManager.Play(impactType, hitPosition);

            // Scale hit-stop with level.
            HitStopManager.DoImpact(level >= 3 ? HitTier.Medium : HitTier.Light);
        }

        private void OnDrawGizmosSelected()
        {
            float r = _tower != null && _tower.CurrentRange > 0f ? _tower.CurrentRange : _fallbackRange;
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, r);
        }
    }
}
