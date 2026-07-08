// =============================================================================
// ProjectileMover — DEF-23: Shared projectile movement for Ranger arrows and
//                   Mage spell orbs.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// Lerps between a launch position and a target position over time, applying an
// optional parabolic arc. Faces the direction of travel each frame. Instantiates
// an impact VFX prefab on arrival and destroys itself.
//
// USAGE:
//   var arrow = Instantiate(arrowPrefab, handPos, Quaternion.identity);
//   arrow.GetComponent<ProjectileMover>().Launch(target.position, 18f, 0.4f);
//
// ARC: 0 = straight line, positive = upward parabola (arrow gravity feel).
//
// POOLING (hero/companion shots):
//   When a body is leased from MoverProjectilePool it is BOUND to that pool via
//   BindToPool. On Arrive a pool-bound body RELEASES back to the pool (no Destroy);
//   an UNBOUND body (e.g. DefenseTower's per-shot primitive bolt) keeps the legacy
//   Destroy(gameObject). So this stays drop-in for the non-pooled caller. The
//   per-lease reset contract is in ResetForLease (called by the pool on Acquire).
// =============================================================================

using UnityEngine;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village
{
    /// <summary>
    /// Moves a projectile from its spawn position to a target world point.
    /// Supports straight travel (arc = 0) and parabolic arcs (arc > 0).
    /// Shared between Ranger arrows and Mage spell orbs.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ProjectileMover : MonoBehaviour
    {
        [Tooltip("Optional particle/VFX prefab spawned at the impact point.")]
        [SerializeField] public GameObject ImpactFX;

        [Tooltip("Self-destruct time for the ImpactFX GameObject (seconds).")]
        [SerializeField, Min(0.1f)] private float _impactFXLifetime = 1.5f;

        // ── Runtime ───────────────────────────────────────────────────────────

        private Vector3 _start;
        private Vector3 _end;
        private float   _speed;
        private float   _arc;
        private float   _totalDistance;
        private float   _t;
        private bool    _launched;
        private System.Action _onArrive;   // DEF (combat feel): payload fired when the projectile lands

        // ── Pooling ───────────────────────────────────────────────────────────
        // Set once when leased from MoverProjectilePool. When non-null, Arrive
        // releases this body back to the pool instead of Destroy'ing it.
        private MoverProjectilePool _pool;
        private ProjectileBodyKind  _poolKind;
        private bool _pooled;

        /// <summary>Bind this body to a pool (called once by MoverProjectilePool on creation).
        /// Pool-bound bodies are returned to the pool on arrival instead of being destroyed.</summary>
        public void BindToPool(MoverProjectilePool pool, ProjectileBodyKind kind)
        {
            _pool     = pool;
            _poolKind = kind;
            _pooled   = pool != null;
        }

        /// <summary>Per-lease RESET (MoverProjectilePool.Acquire). Stops any in-flight motion,
        /// clears the trail so the next shot doesn't streak from the old land point, drops any
        /// stale onArrive payload, and replays the flying particle FX. Launch then re-arms the
        /// flight fresh. Reset is invisible to gameplay — Launch fully re-specifies the shot.</summary>
        public void ResetForLease()
        {
            _launched      = false;
            _onArrive      = null;
            _t             = 0f;
            _totalDistance = 0f;

            // Clear any trail streak carried over from the previous shot.
            var trail = GetComponent<TrailRenderer>();
            if (trail != null) trail.Clear();

            // Replay the persistent flying particle FX child (built once by the pool).
            ProjectileVFXCatalog.ReplayFlying(gameObject);
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Launch the projectile toward <paramref name="targetWorld"/>.
        /// </summary>
        /// <param name="targetWorld">World position to travel to.</param>
        /// <param name="speed">World units per second.</param>
        /// <param name="arc">
        /// Peak height of the parabola above the straight-line path.
        /// 0 = straight; 0.4 = arrow-style arc.
        /// </param>
        public void Launch(Vector3 targetWorld, float speed, float arc, System.Action onArrive = null)
        {
            _start         = transform.position;
            _end           = targetWorld;
            _speed         = Mathf.Max(0.1f, speed);
            _arc           = arc;
            _totalDistance = Vector3.Distance(_start, _end);
            _t             = 0f;
            _launched      = true;
            _onArrive      = onArrive;
            FlowTrace.Step("Projectile", $"Launch dist={_totalDistance:0.0} speed={_speed:0.0} arc={_arc:0.00} pooled={_pooled} hasPayload={(onArrive != null)}");
        }

        // ── Update ────────────────────────────────────────────────────────────

        private void Update()
        {
            if (!_launched) return;

            // Instant-arrive safety (same-position launch).
            if (_totalDistance < 0.001f)
            {
                FlowTrace.Warn("Projectile", "Update: zero-distance launch (start==target) — arriving immediately.");
                Arrive();
                return;
            }

            _t += Time.deltaTime * _speed / _totalDistance;
            _t  = Mathf.Min(_t, 1f);

            // Parabolic arc: Y offset = arc * sin(t*π) — peaks at t=0.5.
            Vector3 pos = Vector3.Lerp(_start, _end, _t);
            pos.y += _arc * Mathf.Sin(_t * Mathf.PI);
            transform.position = pos;

            // Face direction of travel — sample slightly ahead to get a smooth
            // tangent rather than the literal delta (avoids jitter at t≈1).
            float tAhead = Mathf.Min(_t + 0.02f, 1f);
            Vector3 ahead = Vector3.Lerp(_start, _end, tAhead);
            ahead.y += _arc * Mathf.Sin(tAhead * Mathf.PI);
            Vector3 dir = ahead - pos;
            if (dir.sqrMagnitude > 0.00001f)
                transform.rotation = Quaternion.LookRotation(dir);

            if (_t >= 1f) Arrive();
        }

        // ── Impact ────────────────────────────────────────────────────────────

        private void Arrive()
        {
            _launched = false;
            FlowTrace.Step("Projectile", $"Arrive at {transform.position} impactFX={(ImpactFX != null)} pooled={(_pooled && _pool != null)}");

            if (ImpactFX != null)
            {
                // Pooled impact FX (GC-free) when the pool is up; falls back to the legacy
                // Instantiate+Destroy if it isn't (e.g. a non-pooled caller pre-bootstrap).
                if (ImpactFXPool.Instance != null)
                    ImpactFXPool.Instance.Play(ImpactFX, transform.position, Quaternion.identity, _impactFXLifetime);
                else
                {
                    var fx = Instantiate(ImpactFX, transform.position, Quaternion.identity);
                    Destroy(fx, _impactFXLifetime);
                }
            }

            // DEF (combat feel): fire the on-arrival payload — damage + status land WHEN the
            // projectile reaches the target, so the hit reads as the shot connecting (not an
            // instant hit-scan at cast time). Cleared after firing so it can't double-apply.
            var onArrive = _onArrive;
            _onArrive = null;
            onArrive?.Invoke();

            // Pool-bound (hero/companion) bodies return to the pool; unbound bodies
            // (DefenseTower's per-shot primitive) keep the legacy self-destruct.
            if (_pooled && _pool != null)
                _pool.Release(this, _poolKind);
            else
                Destroy(gameObject);
        }
    }
}
