// =============================================================================
// HeroLocomotion — WASD / dpad walking for Hero (Blaise) in the village.
// -----------------------------------------------------------------------------
// Minimal kinematic transform translation, mirroring the Pet.cs movement
// pattern (no Rigidbody, no NavMeshAgent — pure transform). Reads input from
// either Keyboard.current (WASD / arrows) or Gamepad.current (left stick /
// dpad) via the new Input System. activeInputHandler is set to "Both" in
// ProjectSettings so the new input package is live.
//
// Wiring: VillageSceneBuilder.BuildHero adds this component onto the hero
// root. The hero is a primitive Capsule with an auto-collider, so wall
// collisions are handled by Unity's depenetration on transform move (good
// enough for tower-defense pacing). Movement is in the XZ plane only; Y is
// preserved so the hero stays grounded.
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

namespace DeNelle.Village
{
    /// <summary>
    /// Walks the hero with WASD / arrows / dpad / left stick. Kinematic
    /// transform translation in the XZ plane; smoothly faces the move
    /// direction. Y position is preserved so the hero stays on the ground.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HeroLocomotion : MonoBehaviour
    {
        [Tooltip("World units per second at full input deflection.")]
        [SerializeField] private float _moveSpeed = 4.0f;

        [Tooltip("How quickly the hero rotates toward the move direction (rad/sec-ish).")]
        [SerializeField] private float _rotationSpeed = 12f;

        [Tooltip("Acceleration (m/s²) when input is applied. Lower = sluggier start. " +
                 "Owner feedback 2026-05-20: instant max-speed felt rigid; ramp instead.")]
        [SerializeField] private float _accelMetresPerSec2 = 22f;

        [Tooltip("Deceleration (m/s²) when input is released. Higher = sharper stop.")]
        [SerializeField] private float _decelMetresPerSec2 = 28f;

        /// <summary>Current XZ velocity, exposed for the follow camera / animator.</summary>
        public Vector3 Velocity { get; private set; }

        private bool _loggedFirstInput;
        private Animator _animator;
        private NavMeshAgent _agent;   // unified navigation: hero shares the enemies' NavMesh
#if UNITY_EDITOR
        // Collision diagnostic: log each unique blocking collider once.
        private readonly HashSet<Collider> _loggedColliders = new HashSet<Collider>();
#endif
        private static readonly int AnimSpeed   = Animator.StringToHash("Speed");

        // DEF-70: victory pose — triggered when WaveManager fires OnWaveCleared.
        // Suppresses movement so the hero holds the pose until the next wave begins.
        private static readonly int AnimVictory = Animator.StringToHash("Victory");
        private bool _victoryPose;
        private float _victoryPoseTimer;
        // DEF-70 fix: the victory pose is a brief celebration, NEVER a lock. It
        // ends after this many seconds OR the instant the player gives input.
        private const float VictoryPoseSeconds = 2.5f;
        private WaveManager _waveManager;

        private void Awake()
        {
            _animator = GetComponentInChildren<Animator>();
            // Owner 2026-05-25 "movement feels laggy": the baked serialized values
            // (speed 4 / accel 22) spool up slowly. Force snappier response here —
            // the scene's stale values can't be changed without a risky re-bake.
            _moveSpeed = 6f;
            _accelMetresPerSec2 = 55f;
            _decelMetresPerSec2 = 45f;

            // Owner 2026-05-30: unify hero navigation onto the SAME NavMesh the enemies use,
            // so hero + enemies share one definition of "walkable" and traverse the world
            // (ground, stairs, ramparts, hills, caves) identically — in every scene with a
            // baked NavMesh, and so enemies can climb to attack a hero defending up top.
            // Input still drives movement directly via NavMeshAgent.Move below; the agent
            // just constrains the hero to the walkable surface + follows its height.
            _agent = GetComponent<NavMeshAgent>();
            if (_agent == null) _agent = gameObject.AddComponent<NavMeshAgent>();
            _agent.radius = 0.4f;
            _agent.height = 1.8f;
            _agent.baseOffset = 0f;
            _agent.speed = 30f;              // we drive via Move(); keep high so it never caps us
            _agent.acceleration = 200f;
            _agent.angularSpeed = 0f;
            _agent.updateRotation = false;   // facing handled manually (mesh forward is -X)
            _agent.updateUpAxis = false;
            _agent.autoBraking = false;
            _agent.stoppingDistance = 0f;
            _agent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;
        }

        private void Start()
        {
            Debug.Log($"[HeroLocomotion] Start — pos={transform.position}, " +
                      $"newInputKb={(Keyboard.current != null ? "OK" : "null")}, " +
                      $"newInputGp={(Gamepad.current != null ? "OK" : "null")}, " +
                      $"animator={(_animator != null ? _animator.name : "null")}, " +
                      $"controller={(_animator != null && _animator.runtimeAnimatorController != null ? _animator.runtimeAnimatorController.name : "null")}");

            // DEF-10: SpawnHeroMarker / HeroIndicatorRing removed — was a debug
            // visibility aid, confirmed unwanted in review screenshots (2026-05-26).

            // DEF-70: subscribe to wave clear so the hero plays a victory pose.
            // WaveManager has no static Instance — FindObjectOfType once in Start
            // (same pattern as DungeonPortal, TowerVoiceController). Safe: Start
            // runs after all Awake() calls so WaveManager is already initialised.
            _waveManager = Object.FindObjectOfType<WaveManager>();
            if (_waveManager != null)
                _waveManager.OnWaveCleared.AddListener(OnWaveCleared);
            else
                Debug.LogWarning("[HeroLocomotion] WaveManager not found — victory pose will not fire.");
        }

        private void OnDestroy()
        {
            if (_waveManager != null)
                _waveManager.OnWaveCleared.RemoveListener(OnWaveCleared);
        }

        // DEF-70: called by WaveManager.OnWaveCleared (WaveNumberEvent — int waveId).
        private void OnWaveCleared(int waveId)
        {
            _victoryPose = true;
            _victoryPoseTimer = VictoryPoseSeconds;
            Velocity = Vector3.zero; // zero carried velocity so the hero stops in place

            ResolveAnimator();
            if (_animator != null)
            {
                _animator.SetFloat(AnimSpeed, 0f);
                _animator.SetTrigger(AnimVictory);
            }

            Debug.Log($"[HeroLocomotion] Victory pose triggered — wave {waveId} cleared.");
        }

        // DEF-70: shared animator resolver — safe to call before HeroBodySwapper fires.
        private void ResolveAnimator()
        {
            if (_animator != null) return;
            var bodyT = transform.Find("HeroBody");
            if (bodyT != null) _animator = bodyT.GetComponentInChildren<Animator>();
            if (_animator == null) _animator = GetComponentInChildren<Animator>();
        }

        // WO-139 #9: WaveManager may spawn after the hero, leaving _waveManager
        // null forever (victory pose never wires). Retry the resolve+subscribe
        // each frame until it's found, then stop.
        private void TryResolveWaveManager()
        {
            if (_waveManager != null) return;
            _waveManager = Object.FindObjectOfType<WaveManager>();
            if (_waveManager != null)
                _waveManager.OnWaveCleared.AddListener(OnWaveCleared);
        }

        private void Update()
        {
            TryResolveWaveManager();
            Vector2 input = ReadMoveInput();

            // DEF-70 fix: the victory pose briefly suppresses movement after a wave
            // clear — but it must NEVER lock the hero. It ends the instant the player
            // gives movement input, or after VictoryPoseSeconds. (It previously
            // latched true forever: OnWaveCleared set it and NOTHING cleared it, so
            // the hero froze permanently after the first wave clear — the reported
            // "herolocomotion stopped after the level up.")
            if (_victoryPose)
            {
                _victoryPoseTimer -= Time.deltaTime;
                if (_victoryPoseTimer > 0f && input.sqrMagnitude < 0.01f)
                    return;            // still celebrating, no input — hold the pose
                _victoryPose = false;  // player moved or the timer elapsed — resume
            }

            // WORLD-relative movement (owner 2026-05-25): W = +Z (up-screen / north),
            // D = +X (right). The village camera is now a FIXED-angle follow looking
            // north, so world axes ARE screen axes — and decoupling movement from the
            // camera entirely eliminates the old camera-relative feedback loop that
            // made the hero curl into circles. Bulletproof: no Camera.main dependency.
            Vector3 move = new Vector3(input.x, 0f, input.y);
            if (move.sqrMagnitude > 1f) move.Normalize();

            // Smooth velocity toward target — instant max-speed felt rigid.
            // Higher accel when grabbing speed, higher decel when releasing,
            // so the hero responds promptly to a key press but glides slightly
            // when stopped (no instant-snap to zero).
            Vector3 targetVelocity = move * _moveSpeed;
            float maxStep = (targetVelocity.sqrMagnitude > Velocity.sqrMagnitude
                ? _accelMetresPerSec2
                : _decelMetresPerSec2) * Time.deltaTime;
            Velocity = Vector3.MoveTowards(Velocity, targetVelocity, maxStep);

            if (Velocity.sqrMagnitude > 0.0001f)
            {
                if (!_loggedFirstInput)
                {
                    _loggedFirstInput = true;
                    Debug.Log($"[HeroLocomotion] First input registered: ({input.x:F2}, {input.y:F2}) — moving from {transform.position}");
                }

                // Move on the shared NavMesh: the agent constrains the hero to the walkable
                // surface (so it can't enter walls/buildings — there's no NavMesh there) and
                // follows the surface height, so stairs / ramparts / hills "just work" exactly
                // like the enemies. Fall back to a raw transform move if the hero isn't on a
                // NavMesh yet (scene without a bake / spawned off-mesh) so movement never breaks.
                Vector3 step = Velocity * Time.deltaTime;
                if (_agent != null && _agent.isOnNavMesh)
                    _agent.Move(step);
                else
                    transform.position += step;

                // Face the move direction. HeroBodySwapper already orients each hero
                // BODY (child) to face +Z forward via its per-class yaw, so the ROOT
                // just points +Z at the velocity — NO extra offset here. The old
                // blanket Euler(0,-90,0) was tuned for a prior mesh and fought the
                // swapper's CC5-Knight +90 body yaw, producing the side-step/glide
                // (hero walked sideways). Root = LookRotation(velocity); body yaw owns
                // the mesh-forward correction. (WO: hero side-step fix, 2026-05-30.)
                Quaternion target = Quaternion.LookRotation(Velocity.normalized);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation, target, _rotationSpeed * Time.deltaTime);
            }

            // Edge/floor clamp ONLY when off the NavMesh (the transform fallback). When the
            // hero is on the NavMesh, the bake defines the walkable bounds + height, so a
            // manual clamp would fight the agent (and break ramparts/hills by pinning Y to 0).
            if (_agent == null || !_agent.isOnNavMesh)
            {
                var p = transform.position;
                const float PlayableHalf = 50f;
                p.x = Mathf.Clamp(p.x, -PlayableHalf, PlayableHalf);
                p.z = Mathf.Clamp(p.z, -PlayableHalf, PlayableHalf);
                if (p.y < 0f) p.y = 0f;
                transform.position = p;
            }

            // Self-heal the Animator reference (see ResolveAnimator for rationale).
            // Cheap: only runs while _animator is null, stops once wired.
            ResolveAnimator();
            if (_animator != null) _animator.SetFloat(AnimSpeed, Velocity.magnitude);
        }

        private static Vector2 ReadMoveInput()
        {
            Vector2 v = Vector2.zero;

            // New Input System path — Keyboard.current / Gamepad.current.
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) v.y += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) v.y -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) v.x += 1f;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) v.x -= 1f;
            }

            var gp = Gamepad.current;
            if (gp != null)
            {
                Vector2 stick = gp.leftStick.ReadValue();
                if (stick.sqrMagnitude > 0.04f) v += stick;
                if (gp.dpad.up.isPressed) v.y += 1f;
                if (gp.dpad.down.isPressed) v.y -= 1f;
                if (gp.dpad.right.isPressed) v.x += 1f;
                if (gp.dpad.left.isPressed) v.x -= 1f;
            }

            // Legacy Input Manager fallback — activeInputHandler=2 (Both)
            // means UnityEngine.Input is always available too. This is the
            // belt-and-braces path for builds where the new system's device
            // singletons aren't populated for some reason.
            if (v == Vector2.zero)
            {
                float h = UnityEngine.Input.GetAxisRaw("Horizontal");
                float ve = UnityEngine.Input.GetAxisRaw("Vertical");
                // DEADZONE (owner 2026-05-25 "camera drifts on its own while idle"):
                // a connected gamepad / VR / joystick resting slightly off-centre
                // feeds tiny constant values here. The new-input path is already
                // deadzoned, but this legacy fallback had none — so resting-stick
                // noise crept the hero forward with no player input and the camera
                // followed, reading as autonomous camera drift. Kill the noise.
                if (Mathf.Abs(h) < 0.25f) h = 0f;
                if (Mathf.Abs(ve) < 0.25f) ve = 0f;
                v.x += h;
                v.y += ve;
            }

            return v;
        }
    }
}
