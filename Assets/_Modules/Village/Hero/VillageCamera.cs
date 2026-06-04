// =============================================================================
// VillageCamera — third-person follow rig for Hero (Blaise).  (WO-30 + WO-32)
// -----------------------------------------------------------------------------
// Single active camera (verified: Village.unity has exactly one Camera + one
// AudioListener, no Cinemachine, HeroCinemachineRig is commented-out in the
// builder). Follow math is intentionally trivial so it CANNOT drift on its own
// when the target is stationary: desired = target + fixed world offset, then a
// SmoothDamp chase and a LookAt. If the camera is seen moving while the player
// gives no input, the TARGET is moving — the diagnostic below proves which.
//
// TEMP DIAGNOSTIC (owner 2026-05-25 "camera drifts/pans on its own while idle"):
// logs every script on the camera GameObject + the camera count at Start, and
// twice a second logs the hero velocity vs camera velocity. Strip once resolved.
// =============================================================================

using System.Text;
using UnityEngine;

namespace DeNelle.Village
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class VillageCamera : MonoBehaviour
    {
        [Tooltip("The hero root to follow. Wired by VillageSceneBuilder.")]
        [SerializeField] private Transform _target;

        [Tooltip("Fixed world-space offset from the hero (never orbits).")]
        [SerializeField] private Vector3 _followOffset = new Vector3(0f, 8.5f, -6.5f);

        [Tooltip("Look-at point height above the hero's feet (metres).")]
        [SerializeField] private float _lookAtHeight = 1.3f;

        [Tooltip("SmoothDamp time for the position chase (seconds).")]
        [SerializeField] private float _smoothTime = 0.12f;

        private Vector3 _velocity;
        private Vector3 _lastTargetPos;
        private float _diagTimer;
        private int _diagCount;
        private float _soleCamTimer;

        // Force stable values regardless of any stale serialized scene data.
        private void Awake()
        {
            // Owner 2026-05-25: the (0,8.5,-6.5) seat read as "almost bird's-eye".
            // Drop the height + push further back so the camera sits BEHIND the
            // hero looking out toward the horizon (third-person, not top-down).
            // Tighter smooth time too (was 0.12 -> felt laggy).
            _followOffset = new Vector3(0f, 5.5f, -9f);
            _lookAtHeight = 1.3f;
            _smoothTime = 0.08f;
        }

        private void Start()
        {
            if (_target == null) { Debug.LogError("[VillageCamera] Target is null!"); return; }
            transform.position = _target.position + _followOffset;
            _lastTargetPos = _target.position;
            AimAtHero();

#if UNITY_EDITOR
            // DIAG: list EVERY MonoBehaviour on the camera GameObject.
            var sb = new StringBuilder();
            foreach (var m in GetComponents<MonoBehaviour>())
                if (m != null) sb.Append(m.GetType().Name).Append(' ');
            Debug.Log($"[CAM DIAG] Start target={_target.name} camScripts=[{sb}]");
            LogAllCameras("Start");
#endif
            EnforceSoleCamera();
        }

        private void LateUpdate()
        {
            if (_target == null) return;

            Vector3 desired = _target.position + _followOffset;
            transform.position = Vector3.SmoothDamp(
                transform.position, desired, ref _velocity, _smoothTime);
            AimAtHero();

            // Keep enforcing sole-camera FOREVER (~1/sec), not just at startup, so
            // a pet that DIES and RESPAWNS mid-battle — re-instantiating its
            // embedded FBX camera — can never grab the display (owner Q 2026-05-25).
            // Cheap, and only logs when it actually finds a new rogue camera.
            _soleCamTimer -= Time.deltaTime;
            if (_soleCamTimer <= 0f)
            {
                _soleCamTimer = 1f;
                EnforceSoleCamera();
            }

            // DIAG ~2/sec for ~15s: is the TARGET moving (=> input/phantom drift)
            // or is the camera moving while the target is still (=> rogue script)?
            // Editor-only — suppressed in builds to avoid log spam.
#if UNITY_EDITOR
            _diagTimer -= Time.deltaTime;
            if (_diagCount < 30 && _diagTimer <= 0f)
            {
                _diagTimer = 0.5f;
                _diagCount++;
                float targetDelta = (_target.position - _lastTargetPos).magnitude;
                var loco = _target.GetComponent<HeroLocomotion>();
                float heroVel = loco != null ? loco.Velocity.magnitude : -1f;
                Debug.Log($"[CAM DIAG] targetPos={_target.position} " +
                          $"targetMovedThisTick={targetDelta:F3} heroVel={heroVel:F2} " +
                          $"camVel={_velocity.magnitude:F2} camPos={transform.position}");
                if (_diagCount == 3) LogAllCameras("tick3");    // after everything spawned
            }
#endif
            _lastTargetPos = _target.position;
        }

        // Tripo/Blender FBX exports embed CAMERA nodes; instantiated at runtime
        // (pets, hero body) those cameras render to the SCREEN from the model, so
        // the player sees a pet centred and the view "follows the pet" while this
        // VillageCamera sits correctly on the hero. The hero's camera must be the
        // ONLY one driving the display: disable every other screen camera and take
        // top render priority. Render-texture cameras (portrait renderers etc.)
        // are left alone. Re-run for a few ticks because pets spawn after Start.
        private Camera _self;
        private void EnforceSoleCamera()
        {
            if (_self == null) _self = GetComponent<Camera>();
            foreach (var c in Camera.allCameras)
            {
                if (c == null || c == _self) continue;
                if (c.targetTexture != null) continue;          // offscreen RT cams are fine
                if (!c.enabled) continue;
                c.enabled = false;
#if UNITY_EDITOR
                Debug.Log($"[CAM DIAG] disabled rogue screen camera '{c.name}' " +
                          $"(scene='{c.gameObject.scene.name}', pos={c.transform.position})");
#endif
            }
            if (_self != null)
            {
                _self.depth = 100f;          // always render on top of any straggler
                if (!_self.enabled) _self.enabled = true;
            }
        }

        // Enumerate EVERY live camera: the one with targetTexture==null and the
        // HIGHEST depth is what the player actually sees. If that is NOT this
        // VillageCamera, we've found why the screen shows a pet while our follow
        // cam sits correctly on the hero.
#if UNITY_EDITOR
        private void LogAllCameras(string when)
        {
            var sb = new StringBuilder();
            sb.Append($"[CAM DIAG] {when}: Camera.main='{(Camera.main != null ? Camera.main.name : "NULL")}' ")
              .Append($"count={Camera.allCamerasCount}\n");
            foreach (var c in Camera.allCameras)
            {
                if (c == null) continue;
                sb.Append($"   '{c.name}' scene='{c.gameObject.scene.name}' enabled={c.enabled} ")
                  .Append($"depth={c.depth} target={(c.targetTexture != null ? "RT" : "SCREEN")} ")
                  .Append($"tag={c.tag} pos={c.transform.position}\n");
            }
            Debug.Log(sb.ToString());
        }
#endif

        private void AimAtHero()
        {
            Vector3 lookAt = _target.position + Vector3.up * _lookAtHeight;
            Vector3 dir = lookAt - transform.position;
            if (dir.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(dir);
        }

        /// <summary>Editor-side hook so VillageSceneBuilder can wire the target.</summary>
        public void SetTarget(Transform hero) => _target = hero;
    }
}
