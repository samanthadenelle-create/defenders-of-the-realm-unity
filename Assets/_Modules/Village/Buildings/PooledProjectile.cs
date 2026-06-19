// =============================================================================
// PooledProjectile — WO-82. A reusable homing tower bolt. Leased from
// ProjectilePool, Initialized with an IDamageable target + damage, flies to the
// target and deals damage on arrival, then returns itself to the pool.
// -----------------------------------------------------------------------------
// Reconciled to this project:
//   • Targets DeNelle.Core.Combat.IDamageable; validity via IsAlive (not a
//     GetCurrentHealth() that doesn't exist); position via WorldPosition.
//   • Damage via TakeDamage(amount, DamageElement) — DamageElement.None = plain.
//   • COLLIDER-FREE hit: arrival is a sqrMagnitude distance check, not
//     OnTriggerEnter — enemies here aren't guaranteed trigger colliders, and this
//     avoids a Rigidbody/collider dependency. (A trigger hit is also honoured if a
//     collider happens to be present.)
//   • VISUAL: a real particle-system FX body (Spells Pack) parented to the
//     projectile and oriented along travel via ProjectileVFXCatalog. The old
//     camera-facing sprite-billboard / procedural amber-cube visual was removed
//     (owner: "the way the projectiles fire is horrible — use VFX instead").
// =============================================================================

using UnityEngine;
using DeNelle.Core.Combat;
using DeNelle.Core.Diagnostics;   // TGVRU: instrument the projectile flow (§12)

namespace DeNelle.Village
{
    /// <summary>A pooled homing projectile that damages its IDamageable target on arrival.</summary>
    public sealed class PooledProjectile : MonoBehaviour
    {
        [SerializeField] private float _speed = 28f;
        [SerializeField] private float _maxLifetime = 4f;
        [SerializeField] private float _hitDistance = 0.6f;

        private IDamageable _target;
        private float _damage;
        private DamageElement _element = DamageElement.None;        // damage typing
        private DamageElement _visualElement = DamageElement.None;  // sprite/impact look
        private float _timer;
        private GameObject _vfxBody;                      // particle FX flying visual
        private Vector3 _lastDir = Vector3.forward;

        // No Awake-built visual: the FX body is spawned per-shot in Initialize (it is
        // element-matched), and cleaned up on Return.

        /// <summary>Arm the bolt: fly to <paramref name="target"/> and deal damage on arrival.
        /// The DAMAGE typing is <paramref name="element"/>; the VISUAL sprite uses the same
        /// element. Use the 4-arg overload when the look should differ from the damage type
        /// (e.g. an un-empowered Frost tower deals physical but should LOOK like ice).</summary>
        public void Initialize(IDamageable target, float damage, DamageElement element = DamageElement.None)
            => Initialize(target, damage, element, element);

        /// <summary>Arm the bolt with a separate VISUAL element for the art sprite + impact.
        /// <paramref name="damageElement"/> drives <see cref="IDamageable.TakeDamage"/>;
        /// <paramref name="visualElement"/> picks the projectile/impact sprite.</summary>
        public void Initialize(IDamageable target, float damage, DamageElement damageElement, DamageElement visualElement)
        {
            using var _ = FlowTrace.Enter("Projectile", $"Initialize dmg={damage:F0} elem={damageElement} target={(target == null ? "<null>" : "set")}");

            _target = target;
            _damage = damage;
            _element = damageElement;
            _timer = 0f;

            // R(fallback never silent): a null target means this bolt has nothing to home/hit — it
            // will expire on lifetime. Warn so a fire path that armed with no target is visible.
            if (target == null)
                FlowTrace.Warn("Projectile", "Initialize: null target — bolt has no enemy to hit, will expire on lifetime.");

            if (target != null)
            {
                _lastDir = (target.WorldPosition - transform.position).normalized;
                transform.rotation = Quaternion.LookRotation(_lastDir);
            }

            // VISUAL: spawn the element-matched particle FX body, oriented along travel
            // (the FX rides this transform, which Update keeps facing the flight dir).
            // G(uard): the catalog reskin can throw (bad shader / prefab); a thrown reskin must
            // not abort the whole fire path. On throw we keep the prior body (or null) and Warn.
            _visualElement = visualElement;
            _vfxBody = FlowTrace.Try("Projectile", "ReskinFlying visual",
                () => ProjectileVFXCatalog.ReskinFlying(_vfxBody, transform, visualElement), _vfxBody);

            // V(erify the visual was built): no FX body => an INVISIBLE projectile (the class of bug
            // this gate targets). Once-log it so a capture pinpoints a silent visual miss vs a render
            // problem elsewhere — never a silent invisible bolt.
            if (_vfxBody == null)
                FlowTrace.Once("Projectile", $"no-vfx:{visualElement}",
                    $"Initialize: ReskinFlying returned no FX body for element={visualElement} — bolt will fly INVISIBLE. Check ProjectileVFXCatalog.");
        }

        private void Update()
        {
            _timer += Time.deltaTime;
            if (_timer > _maxLifetime || _target == null || !_target.IsAlive)
            {
                Return(didHit: false);
                return;
            }

            Vector3 to = _target.WorldPosition - transform.position;
            if (to.sqrMagnitude <= _hitDistance * _hitDistance)
            {
                _target.TakeDamage(_damage, _element);   // EnemyDamageable -> Enemy.TakeDamage
                Return(didHit: true, hitPosition: transform.position);
                return;
            }

            Vector3 dir = to.normalized;
            _lastDir = dir;
            transform.position += dir * (_speed * Time.deltaTime);
            transform.rotation = Quaternion.LookRotation(dir);   // FX body rides this, points along travel
        }

        // Bonus path if a collider is present — only damages the intended target.
        private void OnTriggerEnter(Collider other)
        {
            if (_target == null) return;
            var hit = other.GetComponentInParent<IDamageable>();
            if (hit != null && ReferenceEquals(hit, _target))
            {
                _target.TakeDamage(_damage, _element);
                Return(didHit: true, hitPosition: transform.position);
            }
        }

        private void Return(bool didHit = false, Vector3 hitPosition = default)
        {
            // Tear down the flying FX body (pooled reuse re-spawns a fresh, element-matched
            // one on the next Initialize). Done on EVERY return (hit or expiry).
            if (_vfxBody != null) { Destroy(_vfxBody); _vfxBody = null; }

            if (didHit)
            {
                // Pop the element-matched particle IMPACT burst at the hit point.
                ProjectileVFXCatalog.SpawnImpact(hitPosition, _visualElement);

                // DEF-VFX-01: fire impact VFX at the hit point via the owning tower.
                // Walk up to TowerCombat on the nearest tower — projectiles don't hold
                // a back-reference to avoid coupling, so we resolve via the pool owner.
                var towerCombat = GetComponentInParent<TowerCombat>();
                towerCombat?.OnProjectileImpact(hitPosition);

                // Fallback: if not parented to a tower, fire VFX directly.
                if (towerCombat == null)
                    VFXManager.Play(VFXType.Impact_Aether, hitPosition);
            }

            _target = null;
            if (ProjectilePool.Instance != null) ProjectilePool.Instance.ReturnToPool(this);
            else
            {
                // R(eturn-fallback never silent): no live pool to return to (scene unloaded / pool
                // destroyed). We disable the body so it stops flying, but Warn so a pool-lifecycle gap
                // surfaces instead of silently orphaning the projectile outside the pool.
                FlowTrace.Once("Projectile", "return-no-pool",
                    "Return: ProjectilePool.Instance is null — disabling body in place (orphaned outside the pool). Pool may have been destroyed.");
                gameObject.SetActive(false);
            }
        }
    }
}
