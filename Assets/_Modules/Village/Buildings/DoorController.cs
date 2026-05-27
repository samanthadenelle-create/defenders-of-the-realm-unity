// =============================================================================
// DoorController — proximity-based door open/close (DEF-55).
// -----------------------------------------------------------------------------
// Attach to any door GameObject. The door rotates open when the hero enters
// the trigger radius and closes when they leave. Optionally enables/disables a
// NavMeshObstacle so AI correctly routes around a closed door.
//
// No direct dependency on HeroLocomotion — any Collider tagged "Player" OR
// carrying a HeroLocomotion component counts as the trigger actor. Degrades
// gracefully if no NavMeshObstacle is present.
// =============================================================================

using UnityEngine;
using UnityEngine.AI;

namespace DeNelle.Village
{
    [DisallowMultipleComponent]
    public sealed class DoorController : MonoBehaviour
    {
        [Header("Pivot")]
        [Tooltip("The transform that physically rotates to open/close. Defaults " +
                 "to this GameObject's transform when left unset.")]
        [SerializeField] private Transform _doorPivot;

        [Tooltip("Local Y-rotation (degrees) of the fully-open door. " +
                 "0 = closed. Typical values: 90 or -90.")]
        [SerializeField] private float _openAngle = 90f;

        [Tooltip("Seconds to lerp from closed to open (and back).")]
        [SerializeField] private float _animDuration = 0.35f;

        [Header("Trigger")]
        [Tooltip("World-unit radius around the door that triggers open. If a " +
                 "SphereCollider trigger is present on this GO it is used instead.")]
        [SerializeField] private float _triggerRadius = 3f;

        [Header("NavMesh")]
        [Tooltip("Optional NavMeshObstacle on this door. Enabled when closed, " +
                 "disabled when open so AI can pass through.")]
        [SerializeField] private NavMeshObstacle _obstacle;

        // ── Runtime ──────────────────────────────────────────────────────────

        private bool _isOpen;
        private float _animT;          // 0 = fully closed, 1 = fully open
        private Transform _hero;
        private bool _heroFound;
        private float _nextCheck;
        private const float CheckInterval = 0.12f;

        private Quaternion ClosedRot => _doorPivot != null
            ? Quaternion.Euler(_doorPivot.localEulerAngles.x, 0f,          _doorPivot.localEulerAngles.z)
            : Quaternion.identity;
        private Quaternion OpenRot   => _doorPivot != null
            ? Quaternion.Euler(_doorPivot.localEulerAngles.x, _openAngle,  _doorPivot.localEulerAngles.z)
            : Quaternion.Euler(0f, _openAngle, 0f);

        private void Awake()
        {
            if (_doorPivot == null) _doorPivot = transform;
            if (_obstacle == null) _obstacle = GetComponent<NavMeshObstacle>();
        }

        private void Start()
        {
            ResolveHero();
            SetObstacle(open: false);
        }

        private void Update()
        {
            // Throttled proximity check.
            if (Time.time >= _nextCheck)
            {
                _nextCheck = Time.time + CheckInterval;
                if (!_heroFound) ResolveHero();
                CheckProximity();
            }

            // Smooth open/close animation every frame.
            if (_isOpen && _animT < 1f)
            {
                _animT = Mathf.MoveTowards(_animT, 1f, Time.deltaTime / _animDuration);
                ApplyRotation();
            }
            else if (!_isOpen && _animT > 0f)
            {
                _animT = Mathf.MoveTowards(_animT, 0f, Time.deltaTime / _animDuration);
                ApplyRotation();
                // Re-enable obstacle once door is fully closed.
                if (_animT <= 0f) SetObstacle(open: false);
            }
        }

        // ── Proximity (trigger collider or manual radius) ─────────────────────

        private void OnTriggerEnter(Collider other)
        {
            if (IsHero(other)) Open();
        }

        private void OnTriggerExit(Collider other)
        {
            if (IsHero(other)) Close();
        }

        private void CheckProximity()
        {
            if (_hero == null) return;
            // Only used when there is no trigger collider set up in the scene.
            if (GetComponent<Collider>() != null) return;

            float dist = (_hero.position - transform.position).magnitude;
            if (!_isOpen && dist <= _triggerRadius) Open();
            else if (_isOpen && dist > _triggerRadius + 0.5f) Close();
        }

        private static bool IsHero(Collider other)
        {
            if (other == null) return false;
            if (other.CompareTag("Player")) return true;
            return other.GetComponentInParent<HeroLocomotion>() != null;
        }

        // ── State ─────────────────────────────────────────────────────────────

        public void Open()
        {
            if (_isOpen) return;
            _isOpen = true;
            SetObstacle(open: true);   // clear immediately so AI re-paths
        }

        public void Close()
        {
            if (!_isOpen) return;
            _isOpen = false;
            // Obstacle is re-enabled once the door animation completes (in Update).
        }

        private void ApplyRotation()
        {
            if (_doorPivot == null) return;
            _doorPivot.localRotation = Quaternion.Slerp(ClosedRot, OpenRot, _animT);
        }

        private void SetObstacle(bool open)
        {
            if (_obstacle != null) _obstacle.enabled = !open;
        }

        private void ResolveHero()
        {
            var h = UnityEngine.Object.FindObjectOfType<HeroLocomotion>();
            if (h != null) { _hero = h.transform; _heroFound = true; }
        }

        // ── Editor gizmo ─────────────────────────────────────────────────────
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.8f, 0.4f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, _triggerRadius);
        }
    }
}
