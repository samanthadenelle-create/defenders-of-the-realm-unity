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

using UnityEngine;
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
        private static readonly int AnimSpeed = Animator.StringToHash("Speed");

        private void Awake()
        {
            _animator = GetComponentInChildren<Animator>();
        }

        private void Start()
        {
            Debug.Log($"[HeroLocomotion] Start — pos={transform.position}, " +
                      $"newInputKb={(Keyboard.current != null ? "OK" : "null")}, " +
                      $"newInputGp={(Gamepad.current != null ? "OK" : "null")}, " +
                      $"animator={(_animator != null ? _animator.name : "null")}, " +
                      $"controller={(_animator != null && _animator.runtimeAnimatorController != null ? _animator.runtimeAnimatorController.name : "null")}");
        }

        private void Update()
        {
            Vector2 input = ReadMoveInput();

            // Camera-relative movement: W = into the screen, A/D = strafe.
            // Project the camera's forward/right onto the XZ plane so steep
            // pitch on the camera doesn't shrink the move vector.
            Vector3 camFwd = Vector3.forward;
            Vector3 camRight = Vector3.right;
            var cam = Camera.main;
            if (cam != null)
            {
                camFwd = cam.transform.forward; camFwd.y = 0f;
                camRight = cam.transform.right; camRight.y = 0f;
                if (camFwd.sqrMagnitude > 0.0001f) camFwd.Normalize();
                else camFwd = Vector3.forward;
                if (camRight.sqrMagnitude > 0.0001f) camRight.Normalize();
                else camRight = Vector3.right;
            }

            Vector3 move = camRight * input.x + camFwd * input.y;
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

                // CapsuleCast forward to test for buildings/walls before we
                // commit the move — owner 2026-05-20 reported walking THROUGH
                // structures. If the path is blocked, clamp distance to just
                // before the hit so the hero stops cleanly at the wall.
                Vector3 step = Velocity * Time.deltaTime;
                float distance = step.magnitude;
                Vector3 dir = step / Mathf.Max(0.0001f, distance);
                Vector3 capsuleBottom = transform.position + Vector3.up * 0.4f;
                Vector3 capsuleTop = transform.position + Vector3.up * 1.6f;
                if (Physics.CapsuleCast(capsuleBottom, capsuleTop, 0.4f, dir,
                        out RaycastHit hit, distance + 0.05f,
                        ~0, QueryTriggerInteraction.Ignore))
                {
                    distance = Mathf.Max(0f, hit.distance - 0.06f);
                }
                transform.position += dir * distance;

                Quaternion target = Quaternion.LookRotation(Velocity);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation, target, _rotationSpeed * Time.deltaTime);
            }

            // Floor clamp — never let the hero fall below the village ground
            // plane. Without this, any gravity-applying component on the
            // imported mesh would pull the hero into the void below the map.
            if (transform.position.y < 0f)
            {
                var p = transform.position; p.y = 0f;
                transform.position = p;
            }

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
                v.x += UnityEngine.Input.GetAxisRaw("Horizontal");
                v.y += UnityEngine.Input.GetAxisRaw("Vertical");
            }

            return v;
        }
    }
}
