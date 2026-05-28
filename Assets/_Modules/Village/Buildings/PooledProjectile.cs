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
//   • Builds its own visual (a thin unlit bolt + trail) — no authored prefab.
// =============================================================================

using UnityEngine;
using DeNelle.Core.Combat;

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
        private DamageElement _element = DamageElement.None;
        private float _timer;
        private TrailRenderer _trail;

        private void Awake() => BuildVisual();

        /// <summary>Arm the bolt: fly to <paramref name="target"/> and deal damage on arrival.</summary>
        public void Initialize(IDamageable target, float damage, DamageElement element = DamageElement.None)
        {
            _target = target;
            _damage = damage;
            _element = element;
            _timer = 0f;
            if (_trail != null) _trail.Clear();   // no streak from the last shot's path (pooled reuse)
            if (target != null)
                transform.rotation = Quaternion.LookRotation((target.WorldPosition - transform.position).normalized);
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
            transform.position += dir * (_speed * Time.deltaTime);
            transform.rotation = Quaternion.LookRotation(dir);
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
            if (didHit)
            {
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
            else gameObject.SetActive(false);
        }

        // --- procedural visual (thin amber bolt + fading trail) ------------------
        private void BuildVisual()
        {
            var bolt = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bolt.name = "Bolt";
            bolt.transform.SetParent(transform, false);
            bolt.transform.localScale = new Vector3(0.1f, 0.1f, 0.55f);   // long along forward (Z)
            var col = bolt.GetComponent<Collider>();
            if (col != null) Destroy(col);

            var amber = new Color(1f, 0.78f, 0.25f);
            var rend = bolt.GetComponent<Renderer>();
            if (rend != null)
            {
                Shader sh = Shader.Find("Universal Render Pipeline/Unlit")
                            ?? Shader.Find("Unlit/Color") ?? Shader.Find("Sprites/Default");
                var mat = new Material(sh);
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", amber);
                else mat.color = amber;
                rend.sharedMaterial = mat;
            }

            _trail = gameObject.AddComponent<TrailRenderer>();
            var trail = _trail;
            trail.time = 0.14f;
            trail.startWidth = 0.12f;
            trail.endWidth = 0f;
            trail.numCapVertices = 2;
            Shader ts = Shader.Find("Universal Render Pipeline/Unlit")
                        ?? Shader.Find("Sprites/Default");
            if (ts != null)
            {
                var tmat = new Material(ts);
                if (tmat.HasProperty("_BaseColor")) tmat.SetColor("_BaseColor", amber);
                else tmat.color = amber;
                trail.sharedMaterial = tmat;
            }
            trail.startColor = new Color(1f, 0.82f, 0.35f, 0.9f);
            trail.endColor = new Color(1f, 0.82f, 0.35f, 0f);
        }
    }
}
