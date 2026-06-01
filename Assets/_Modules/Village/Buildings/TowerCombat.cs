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

using System.Collections;
using UnityEngine;
using DeNelle.Core.Combat;
using DeNelle.Core.Data;

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

        // ── Empowerment state ────────────────────────────────────────────────

        private EmpowermentAbility _empowerment = EmpowermentAbility.None;
        private bool _isEmpowered;

        // ManaSurge: count shots; every 5th fires a 3-bolt burst.
        private int _shotsSinceLastBurst;
        private const int ManaSurgeBurstInterval = 5;
        private const float ManaSurgeBurstDamageMult = 0.6f;

        // GlacialCore: pulse AoE slow on all enemies in range.
        private const float GlacialPulseInterval = 2.5f;   // seconds between pulses
        private const float GlacialSlowDuration  = 3.0f;   // how long each slow lasts
        private const float GlacialAngle         = 120f;    // not used (full circle)

        // EternalEmber: burn DoT on every hit.
        private const float EternalEmberBurnDps      = 4f;  // damage per second
        private const float EternalEmberBurnDuration = 4f;  // seconds

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

        // ── Empowerment public entry point ────────────────────────────────────

        /// <summary>
        /// Called by <see cref="Tower.TryEmpower"/> after the crystal cost is deducted.
        /// Stores the ability and starts any persistent loops (GlacialCore slow field).
        /// </summary>
        public void OnEmpowered(EmpowermentAbility ability)
        {
            _empowerment = ability;
            _isEmpowered = true;
            _shotsSinceLastBurst = 0;

            if (ability == EmpowermentAbility.GlacialCore)
                StartCoroutine(GlacialCoreSlowLoop());

            Debug.Log($"[TowerCombat] Empowerment activated — {ability} on '{(_tower != null ? _tower.Data?.towerName : name)}'.");
        }

        // ── Update — fire loop ────────────────────────────────────────────────

        private void Update()
        {
            if (Time.time < _nextAttackTime) return;

            float range = _tower != null && _tower.CurrentRange > 0f ? _tower.CurrentRange : _fallbackRange;

            // TrueAim: acquire secondary (highest-HP) target before primary sweep.
            IDamageable secondary = null;
            if (_isEmpowered && _empowerment == EmpowermentAbility.TrueAim)
                secondary = FindHighestHpTarget(range);

            IDamageable target = FindNearestTarget(range);
            if (target == null)
            {
                _nextAttackTime = Time.time + _idleRescan;
                return;
            }

            FireAt(target, secondary);
            float level = _tower != null ? Mathf.Max(1, _tower.CurrentLevel) : 1;
            _nextAttackTime = Time.time + (_baseCooldown / level);
        }

        // ── Target selection ──────────────────────────────────────────────────

        private IDamageable FindNearestTarget(float range)
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
                var dmg = enemy.GetComponent<EnemyDamageable>();
                if (dmg == null || !dmg.IsAlive || dmg.Faction != CombatFaction.Hostile) continue;
                bestSq = sq;
                best = dmg;
            }

            // WO-125 Bug 2: the apex dragon is NOT in LiveEnemies (it owns kinematic
            // flight, not a NavMesh agent) and carries no EnemyDamageable adapter — it
            // implements IDamageable directly. So the ground-roster scan above can never
            // see it. Consider it here through the Core seam (Village->Core is allowed).
            var boss = _wave?.LiveApexBoss;
            if (boss != null && boss.IsAlive && ((IDamageable)boss).Faction == CombatFaction.Hostile)
            {
                float bsq = (((IDamageable)boss).WorldPosition - myPos).sqrMagnitude;
                if (bsq <= maxSq && bsq < bestSq)
                {
                    bestSq = bsq;
                    best = boss;
                }
            }
            return best;
        }

        // Kept for backward compat (EnemyBrain + test callers used FindBestTarget).
        private IDamageable FindBestTarget(float range) => FindNearestTarget(range);

        /// <summary>
        /// TrueAim secondary targeting — returns the highest-HP enemy in range
        /// (the most dangerous target). Returns null when only one enemy is in range.
        /// </summary>
        private IDamageable FindHighestHpTarget(float range)
        {
            if (_wave == null) { ResolveWave(); if (_wave == null) return null; }

            var list = _wave.LiveEnemies;
            if (list == null) return null;

            Vector3 myPos = transform.position;
            float maxSq = range * range;
            float bestHp = -1f;
            IDamageable best = null;

            for (int i = 0; i < list.Count; i++)
            {
                var enemy = list[i];
                if (enemy == null) continue;
                float sq = (enemy.transform.position - myPos).sqrMagnitude;
                if (sq > maxSq) continue;
                var dmg = enemy.GetComponent<EnemyDamageable>();
                if (dmg == null || !dmg.IsAlive || dmg.Faction != CombatFaction.Hostile) continue;
                if (dmg.Hp > bestHp) { bestHp = dmg.Hp; best = dmg; }
            }

            // WO-125 Bug 2 (mirror): also weigh the apex dragon for TrueAim's
            // highest-HP pick — it's the biggest health pool on the field by far.
            var boss = _wave?.LiveApexBoss;
            if (boss != null && boss.IsAlive && ((IDamageable)boss).Faction == CombatFaction.Hostile)
            {
                float bsq = (((IDamageable)boss).WorldPosition - myPos).sqrMagnitude;
                if (bsq <= maxSq && ((IDamageable)boss).Hp > bestHp)
                {
                    bestHp = ((IDamageable)boss).Hp;
                    best = boss;
                }
            }
            return best;
        }

        // ── Fire ──────────────────────────────────────────────────────────────

        private void FireAt(IDamageable target, IDamageable trueAimSecondary = null)
        {
            if (ProjectilePool.Instance == null) return;

            float damage  = _tower != null && _tower.CurrentDamage > 0f ? _tower.CurrentDamage : _fallbackDamage;
            int   level   = _tower != null ? _tower.CurrentLevel : 1;
            Vector3 firePos = _firePoint != null ? _firePoint.position : transform.position;

            // ── ManaSurge: every 5th shot → 3-bolt burst at 60 % each ────────
            if (_isEmpowered && _empowerment == EmpowermentAbility.ManaSurge)
            {
                _shotsSinceLastBurst++;
                if (_shotsSinceLastBurst >= ManaSurgeBurstInterval)
                {
                    _shotsSinceLastBurst = 0;
                    float burstDmg = damage * ManaSurgeBurstDamageMult;
                    for (int i = 0; i < 3; i++)
                        FireSingleProjectile(target, burstDmg, DamageElement.Aether, firePos);
                    VFXManager.Play(VFXType.Cast_MageCharge, firePos);
                    HitStopManager.DoImpact(HitTier.Medium);
                    return;  // burst REPLACES the standard shot this tick
                }
            }

            // ── Standard shot ─────────────────────────────────────────────────
            DamageElement element = _isEmpowered
                ? AbilityToElement(_empowerment)
                : DamageElement.None;

            FireSingleProjectile(target, damage, element, firePos);

            // ── EternalEmber: apply Burn status + start DoT coroutine ─────────
            if (_isEmpowered && _empowerment == EmpowermentAbility.EternalEmber)
            {
                target.ApplyStatus(StatusEffect.Burn, EternalEmberBurnDuration);
                StartCoroutine(BurnDoTCoroutine(target, EternalEmberBurnDps, EternalEmberBurnDuration));
            }

            // ── TrueAim: fire at secondary target simultaneously ───────────────
            if (_isEmpowered && _empowerment == EmpowermentAbility.TrueAim && trueAimSecondary != null)
                FireSingleProjectile(trueAimSecondary, damage, DamageElement.None, firePos);

            // ── VFX: muzzle flash ─────────────────────────────────────────────
            var muzzleType = (level >= 3 || _isEmpowered)
                ? VFXType.Cast_MageCharge
                : VFXType.Projectile_TowerArcane;
            VFXManager.Play(muzzleType, firePos);

            HitStopManager.DoImpact(_isEmpowered ? HitTier.Medium : HitTier.Light);
        }

        private void FireSingleProjectile(IDamageable target, float damage, DamageElement element, Vector3 firePos)
        {
            if (ProjectilePool.Instance == null) return;
            var proj = ProjectilePool.Instance.GetProjectile();
            proj.transform.position = firePos;
            proj.Initialize(target, damage, element);
        }

        private static DamageElement AbilityToElement(EmpowermentAbility ability) =>
            ability switch
            {
                EmpowermentAbility.ManaSurge    => DamageElement.Aether,
                EmpowermentAbility.GlacialCore  => DamageElement.Ice,
                EmpowermentAbility.EternalEmber => DamageElement.Flame,
                EmpowermentAbility.TrueAim      => DamageElement.None,    // Physical — intentional
                _                               => DamageElement.None,
            };

        // ── Empowerment coroutines ────────────────────────────────────────────

        /// <summary>
        /// GlacialCore — pulses an AoE slow field every <see cref="GlacialPulseInterval"/>
        /// seconds. Runs for the tower's lifetime (stops when the component is destroyed).
        /// </summary>
        private IEnumerator GlacialCoreSlowLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(GlacialPulseInterval);

                float range = _tower != null && _tower.CurrentRange > 0f ? _tower.CurrentRange : _fallbackRange;
                if (_wave == null) { ResolveWave(); continue; }

                var list = _wave.LiveEnemies;
                if (list == null) continue;

                Vector3 myPos = transform.position;
                float maxSq   = range * range;

                for (int i = 0; i < list.Count; i++)
                {
                    var enemy = list[i];
                    if (enemy == null) continue;
                    float sq = (enemy.transform.position - myPos).sqrMagnitude;
                    if (sq > maxSq) continue;
                    var dmg = enemy.GetComponent<EnemyDamageable>();
                    if (dmg == null || !dmg.IsAlive) continue;
                    dmg.ApplyStatus(StatusEffect.Slow, GlacialSlowDuration);
                }
            }
        }

        /// <summary>
        /// EternalEmber — ticks 4 damage per second for <paramref name="duration"/>
        /// seconds on <paramref name="target"/>. Automatically stops if the target dies.
        /// </summary>
        private IEnumerator BurnDoTCoroutine(IDamageable target, float dps, float duration)
        {
            float elapsed = 0f;
            const float tickInterval = 1f;
            while (elapsed < duration)
            {
                yield return new WaitForSeconds(tickInterval);
                elapsed += tickInterval;
                if (target == null || !target.IsAlive) yield break;
                target.TakeDamage(dps * tickInterval, DamageElement.Flame);
            }
        }

        // ── Impact callback (called by PooledProjectile.OnHit) ───────────────

        /// <summary>
        /// Called externally (e.g. by PooledProjectile.OnHit) to trigger an
        /// impact VFX at the hit position. Scales by level and empowerment state.
        /// </summary>
        public void OnProjectileImpact(Vector3 hitPosition)
        {
            int level = _tower != null ? _tower.CurrentLevel : 1;

            var impactType = (_isEmpowered || level >= 3)
                ? VFXType.Impact_ExplosionAether
                : level == 2
                    ? VFXType.Impact_Aether
                    : VFXType.Impact_Physical;
            VFXManager.Play(impactType, hitPosition);

            HitStopManager.DoImpact((_isEmpowered || level >= 3) ? HitTier.Medium : HitTier.Light);
        }

        private void OnDrawGizmosSelected()
        {
            float r = _tower != null && _tower.CurrentRange > 0f ? _tower.CurrentRange : _fallbackRange;
            Gizmos.color = _isEmpowered ? Color.cyan : Color.red;
            Gizmos.DrawWireSphere(transform.position, r);
        }
    }
}
