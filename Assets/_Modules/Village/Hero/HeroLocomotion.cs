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
                      $"newInputGp={(Gamepad.current != null ? "OK" : "null")}");
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

            Velocity = move * _moveSpeed;
            if (Velocity.sqrMagnitude > 0.0001f)
            {
                if (!_loggedFirstInput)
                {
                    _loggedFirstInput = true;
                    Debug.Log($"[HeroLocomotion] First input registered: ({input.x:F2}, {input.y:F2}) — moving from {transform.position}");
                }
                transform.position += Velocity * Time.deltaTime;
                Quaternion target = Quaternion.LookRotation(Velocity);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation, target, _rotationSpeed * Time.deltaTime);
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
