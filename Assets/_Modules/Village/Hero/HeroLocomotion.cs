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
            // Owner 2026-05-25 "movement feels laggy": the baked serialized values
            // (speed 4 / accel 22) spool up slowly. Force snappier response here —
            // the scene's stale values can't be changed without a risky re-bake.
            _moveSpeed = 6f;
            _accelMetresPerSec2 = 55f;
            _decelMetresPerSec2 = 45f;
        }

        private void Start()
        {
            Debug.Log($"[HeroLocomotion] Start — pos={transform.position}, " +
                      $"newInputKb={(Keyboard.current != null ? "OK" : "null")}, " +
                      $"newInputGp={(Gamepad.current != null ? "OK" : "null")}, " +
                      $"animator={(_animator != null ? _animator.name : "null")}, " +
                      $"controller={(_animator != null && _animator.runtimeAnimatorController != null ? _animator.runtimeAnimatorController.name : "null")}");

            SpawnHeroMarker();
        }

        // Owner/Grok 2026-05-25: the hero has no walk anim (it slides) and reads
        // small under the high camera, so the animated pet steals the eye and the
        // camera "looks like" it follows the pet (it doesn't — it's locked to the
        // hero). A bright emissive ground ring makes the PLAYER hero unmistakable.
        // Child of the root, so it survives HeroBodySwapper rebuilds.
        private void SpawnHeroMarker()
        {
            if (transform.Find("HeroIndicatorRing") != null) return;
            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "HeroIndicatorRing";
            foreach (var col in ring.GetComponents<Collider>()) Destroy(col);
            ring.transform.SetParent(transform, false);
            ring.transform.localPosition = new Vector3(0f, 0.06f, 0f);
            ring.transform.localScale = new Vector3(1.9f, 0.03f, 1.9f); // flat disc at feet
            var mr = ring.GetComponent<Renderer>();
            var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var mat = new Material(sh);
            Color ringColor = new Color(0.15f, 0.95f, 1f); // bright cyan
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", ringColor);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", ringColor);
            if (mat.HasProperty("_EmissionColor"))
            {
                mat.SetColor("_EmissionColor", ringColor * 2.2f);
                mat.EnableKeyword("_EMISSION");
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            mr.sharedMaterial = mat;
        }

        private void Update()
        {
            Vector2 input = ReadMoveInput();

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

            // Floor + map-edge clamp (WO-33). Floor: never let the hero fall below
            // the village ground plane (any gravity-applying component on the
            // imported mesh would pull it into the void). Edge: keep the hero on the
            // 300x300 exterior terrain — PlayableHalf (142 m) sits ~8 m inside the
            // terrain edge so the drop-off lip never shows. Pure runtime clamp, no
            // boundary geometry / scene edit needed (mirrors the speed override in
            // Awake — runtime code over a risky village re-bake).
            {
                var p = transform.position;
                const float PlayableHalf = 142f;
                p.x = Mathf.Clamp(p.x, -PlayableHalf, PlayableHalf);
                p.z = Mathf.Clamp(p.z, -PlayableHalf, PlayableHalf);
                if (p.y < 0f) p.y = 0f;
                transform.position = p;
            }

            // Self-heal the Animator reference. HeroLocomotion.Awake() caches
            // GetComponentInChildren<Animator>() BEFORE HeroBodySwapper.Start()
            // swaps the real FBX body in — so the Awake cache is null (the baked
            // placeholder has no Animator). HeroBodySwapper re-caches this via
            // reflection after the swap, but re-resolve here too as a backstop so
            // a future change to swap order can never silently break the walk anim.
            // Cheap: only runs while _animator is null, stops once wired.
            if (_animator == null)
            {
                var bodyT = transform.Find("HeroBody");
                if (bodyT != null) _animator = bodyT.GetComponentInChildren<Animator>();
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
