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
//
// RELEASE (WO-1155) — THE PROJECTILE OWNS ITS TRAVEL FX SLOT, NOT A GLOBAL SWEEP:
//   A travelling Hovl trail is a VFXManager LOOP that FOLLOWS this transform (it is
//   deliberately NOT parented — see RangedAttackVFX.PlayHovlTravel / DefenseTower).
//   Its host is a POOLED VFX instance, so it is never destroyed; VFXManager's
//   per-frame ReclaimDestroyedLoops only frees loops whose host GameObject was
//   DESTROYED, which that host never is. So if the only Stop() lives inside the
//   ARRIVAL closure, a body that is recycled/disabled/destroyed in flight strands
//   that loop slot for the rest of the session — the sweep cannot see it.
//
//   Hence `onRelease`: a SECOND, single-fire callback taken by Launch and fired by
//   whichever of arrive / OnDisable / OnDestroy / lifetime-timeout happens FIRST.
//   ReleaseOnce() latches (`_released`), so arrive-then-recycle releases EXACTLY
//   once — a double-release would push the derived loop count negative, and WO-1057
//   deliberately removed the Mathf.Max(0,…) clamp that used to hide exactly that.
//   Do NOT fold this back into the arrival payload, and do NOT ask the registry to
//   release on the projectile's behalf: the registry OBSERVES, the owner releases.
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

        // ── Release (WO-1155) ─────────────────────────────────────────────────
        // Single-fire teardown for anything this shot HOLDS while it flies (today: the
        // followed Hovl travel-loop handle). Fired by the FIRST of arrive / OnDisable /
        // OnDestroy / timeout; `_released` is the idempotency latch.
        private System.Action _onRelease;
        private bool  _released = true;    // starts latched: an un-launched body holds nothing
        private float _elapsed;            // seconds since Launch (timeout accounting)
        private float _maxFlightSeconds;   // hard backstop — see ComputeMaxFlightSeconds

        // Backstop only. A shot that has taken this much longer than its own predicted flight
        // is not in flight any more in any meaningful sense, so its loop slot must come back
        // even though nothing destroyed or recycled the body. Deliberately generous: it must
        // never cut a legitimately slow lob short.
        private const float FlightTimeoutFactor  = 3f;    // x the predicted flight time
        private const float FlightTimeoutGrace   = 1f;    // + this, so short shots aren't tight
        private const float FlightTimeoutFloor   = 2f;    // never shorter than this
        private const float FlightTimeoutCeiling = 30f;   // never longer than this

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
            // WO-1155: the previous lease's release has already fired (OnDisable at Release, at
            // the latest). Clear the slot and re-latch so this fresh lease starts holding nothing.
            _onRelease     = null;
            _released      = true;
            _elapsed       = 0f;

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
        /// <param name="onRelease">
        /// WO-1155 — teardown for whatever this shot HOLDS in flight (the followed travel-FX
        /// loop handle). Fired EXACTLY ONCE, by the first of arrival, OnDisable, OnDestroy or
        /// the lifetime timeout. Pass the trail's Stop here rather than burying it in
        /// <paramref name="onArrive"/>: an arrival-only stop strands the loop slot whenever the
        /// body is recycled or destroyed mid-flight (VFXManager's sweep cannot reclaim a pooled,
        /// never-destroyed FX host).
        /// </param>
        public void Launch(Vector3 targetWorld, float speed, float arc, System.Action onArrive = null,
                           System.Action onRelease = null)
        {
            // A body re-armed without a pool reset must not carry the previous shot's hold.
            ReleaseOnce();

            _onRelease        = onRelease;
            _released         = onRelease == null;   // nothing held => already "released"
            _elapsed          = 0f;

            _start         = transform.position;
            _end           = targetWorld;
            _speed         = Mathf.Max(0.1f, speed);
            _arc           = arc;
            _totalDistance = Vector3.Distance(_start, _end);
            _t             = 0f;
            _launched      = true;
            _onArrive      = onArrive;
            _maxFlightSeconds = ComputeMaxFlightSeconds();
            FlowTrace.Step("Projectile", $"Launch dist={_totalDistance:0.0} speed={_speed:0.0} arc={_arc:0.00} pooled={_pooled} hasPayload={(onArrive != null)} holdsFx={(onRelease != null)} timeout={_maxFlightSeconds:0.0}s");
        }

        // ── Update ────────────────────────────────────────────────────────────

        private void Update()
        {
            if (!_launched) return;

            // WO-1155 lifetime backstop: neither arrived nor torn down. Ends the flight and
            // gives the travel-FX slot back rather than letting the shot hold it indefinitely.
            _elapsed += Time.deltaTime;
            if (_elapsed > _maxFlightSeconds)
            {
                AbortFlight($"lifetime timeout after {_elapsed:0.0}s (predicted {_totalDistance / _speed:0.0}s, cap {_maxFlightSeconds:0.0}s) at t={_t:0.00}");
                return;
            }

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
            // F8 2026-07-11 "spell cast on a 60 degree angle not flat" — the muzzle is
            // chest-high (+1.2) while the target point is at the enemy base, so at close
            // range the travel tangent pitches the body steeply downward from the first
            // frame. Flatten Y for the ROTATION ONLY — flat launch, full-3D travel (the
            // position lerp above is untouched, so the shot still reaches the target).
            // Guard near-zero horizontal (fall back to the unflattened tangent).
            Vector3 flatDir = new Vector3(dir.x, 0f, dir.z);
            if (flatDir.sqrMagnitude >= 0.0001f) dir = flatDir;
            if (dir.sqrMagnitude > 0.00001f)
                transform.rotation = Quaternion.LookRotation(dir);

            if (_t >= 1f) Arrive();
        }

        // ── Impact ────────────────────────────────────────────────────────────

        private void Arrive()
        {
            _launched = false;
            FlowTrace.Step("Projectile", $"Arrive at {transform.position} impactFX={(ImpactFX != null)} pooled={(_pooled && _pool != null)}");

            // WO-1155: hand the travel-FX loop slot back FIRST — same ordering the old
            // arrival closures used (`h?.StopSoft(); inner?.Invoke();`), so a soft-stopped
            // trail still finishes its tail while the impact payload runs.
            ReleaseOnce();

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

            DisposeBody();
        }

        /// <summary>Pool-bound (hero/companion) bodies return to the pool; unbound bodies
        /// (DefenseTower's per-shot primitive) keep the legacy self-destruct. ONE body-disposal
        /// path, shared by arrival and the timeout abort, so the two can never drift.</summary>
        private void DisposeBody()
        {
            if (_pooled && _pool != null)
                _pool.Release(this, _poolKind);
            else
                Destroy(gameObject);
        }

        // ── Release / teardown (WO-1155) ──────────────────────────────────────

        /// <summary>
        /// Fire the in-flight hold teardown EXACTLY ONCE. The latch is what makes
        /// arrive-then-recycle safe: Arrive() releases, then the pool's SetActive(false)
        /// re-enters through OnDisable and finds nothing left to do.
        /// <para/>
        /// ⚠ A double-release would return the same loop slot twice and drive VFXManager's
        /// DERIVED loop count negative — and WO-1057 deliberately deleted the
        /// <c>Mathf.Max(0, …)</c> clamp that used to hide precisely that. Never "fix" a
        /// negative count with a clamp; fix the caller that released twice.
        /// <para/>
        /// Guarded: the callback reaches VFXManager, which may already be torn down during a
        /// scene unload / quit. A throw here must not skip the body disposal that follows.
        /// </summary>
        private void ReleaseOnce()
        {
            if (_released) return;
            _released = true;

            var release = _onRelease;
            _onRelease  = null;          // clear BEFORE invoking — no re-entrant second fire
            if (release == null) return;

            Guard.Try("Projectile", "release in-flight travel FX", () => release());
        }

        /// <summary>End a flight that will never arrive (lifetime backstop). Warns with the
        /// numbers, gives the FX slot back, and disposes the body. The arrival payload is
        /// deliberately NOT fired — a shot this far past its predicted flight has stopped being
        /// the hit the player saw leave, and paying damage from it would be a silent gameplay
        /// change. The Warn is the loud half of that trade.</summary>
        private void AbortFlight(string why)
        {
            _launched = false;
            _onArrive = null;   // dropped on purpose — see the summary above
            FlowTrace.Warn("Projectile",
                $"AbortFlight: {why} — dropping the arrival payload, releasing the travel FX slot " +
                $"and returning the body (pooled={_pooled && _pool != null}).");
            ReleaseOnce();
            DisposeBody();
        }

        /// <summary>Predicted flight time x a generous factor, clamped. Backstop, never a cap on
        /// a legitimate lob (a normal shot arrives at 1x and never sees this).</summary>
        private float ComputeMaxFlightSeconds()
        {
            float predicted = _totalDistance / Mathf.Max(0.1f, _speed);
            return Mathf.Clamp(predicted * FlightTimeoutFactor + FlightTimeoutGrace,
                               FlightTimeoutFloor, FlightTimeoutCeiling);
        }

        /// <summary>Recycled into the pool (SetActive(false)) or deactivated with its owner —
        /// the release path that arrival never reaches. Idempotent via ReleaseOnce.</summary>
        private void OnDisable()
        {
            if (!_released)
                FlowTrace.Step("Projectile",
                    $"OnDisable while still holding travel FX (launched={_launched}, t={_t:0.00}) — " +
                    "releasing the loop slot from the OWNER instead of leaving it to a global sweep " +
                    "that cannot see a pooled, never-destroyed FX host.");
            _launched = false;
            ReleaseOnce();
        }

        /// <summary>Destroyed in flight (scene unload, owner teardown, unbound self-destruct).
        /// OnDisable normally fires first; this is the belt-and-braces twin and is a no-op then.</summary>
        private void OnDestroy() => ReleaseOnce();
    }
}
