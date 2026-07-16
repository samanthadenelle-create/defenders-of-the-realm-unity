// =============================================================================
// DungeonHero — the Keeper's dungeon-walk controller (Week 5).
// -----------------------------------------------------------------------------
// Port spec Part 5 Week 5:
//   "Hero collision: CharacterController + wall mesh colliders. Verify no
//    walk-through bug. Smooth tap-to-move on touch; WASD on desktop."
//
// This is the dungeon-layer hero locomotion. The village hero rig (port spec
// Week 3) is a separate scene actor with its own controller; the dungeon hero
// is deliberately its own MonoBehaviour so the two scenes' movement feel can be
// tuned independently and neither asmdef depends on the other.
//
// ── Movement model ──
// A UnityEngine.CharacterController IS the collision body — it slides along the
// KayKit wall mesh colliders and cannot pass through them (this is the port
// spec's "no walk-through bug" guarantee, given the wall meshes carry colliders;
// see week5-dungeon-foundation.md for the integrator note on that).
//
//   • Desktop: WASD / arrow keys give a continuous move vector. The vector is
//     camera-relative so "up" is always screen-up under the isometric tilt.
//   • Touch / mouse: a tap raycasts onto the floor; the Keeper walks a straight
//     line to the tapped point, stopping on arrival. A new tap retargets. Any
//     keyboard input cancels the tap-move (desktop players expect WASD to win).
//
// Gravity is applied every frame so the controller stays grounded on stairs and
// uneven KayKit floor tiles — without it isGrounded never latches and the
// controller can "float" off a step edge.
//
// Input uses the Input System's low-level device polling (Keyboard.current /
// Mouse.current / Touchscreen.current) rather than an .inputactions asset — the
// project ships no such asset yet, and dungeon movement is a small fixed scheme.
// If a project-wide input asset lands later, SampleDesktopMove / consumed-tap
// reads are the two seams to swap.
//
// All public surface is plain MonoBehaviour — no UniTask flows here (movement is
// per-frame, not async).
// =============================================================================

using UnityEngine;
using UnityEngine.InputSystem;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Dungeons
{
    /// <summary>
    /// CharacterController-based dungeon locomotion for the Keeper — WASD on
    /// desktop, smooth tap-to-move on touch, sliding collision against the
    /// dungeon's wall mesh colliders.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public sealed class DungeonHero : MonoBehaviour
    {
        // ── Tuning — movement ────────────────────────────────────────────────

        [Header("Movement")]
        [Tooltip("Top walking speed in world units per second.")]
        [SerializeField] private float _moveSpeed = 4.2f;

        [Tooltip("How fast the hero eases toward the target speed (units/sec²). " +
                 "Higher = snappier starts/stops; lower = a softer drift.")]
        [SerializeField] private float _acceleration = 28f;

        [Tooltip("Degrees per second the hero rig turns to face its heading.")]
        [SerializeField] private float _turnSpeed = 720f;

        [Tooltip("Downward acceleration applied each frame so the controller " +
                 "stays planted on stairs / uneven floor tiles.")]
        [SerializeField] private float _gravity = 22f;

        // ── Tuning — tap-to-move ─────────────────────────────────────────────

        [Header("Tap-to-move (touch / mouse)")]
        [Tooltip("Layers a tap-to-move raycast is allowed to hit (the dungeon " +
                 "floor / walkable ground). Leave empty to hit everything.")]
        [SerializeField] private LayerMask _walkableMask = ~0;

        [Tooltip("Distance from the tapped point at which the Keeper is treated " +
                 "as 'arrived' and stops — avoids jittering on the exact point.")]
        [SerializeField] private float _arriveDistance = 0.25f;

        [Tooltip("Max ray length for a tap-to-move floor pick (world units).")]
        [SerializeField] private float _tapRayLength = 200f;

        [Header("Camera")]
        [Tooltip("Camera whose yaw makes WASD screen-relative. Auto-binds to " +
                 "Camera.main when left unset; the DungeonController also pushes " +
                 "the active camera in via SetCamera().")]
        [SerializeField] private Camera _moveCamera;

        // ── Runtime ──────────────────────────────────────────────────────────

        private CharacterController _controller;

        // ── Animation ─────────────────────────────────────────────────────────
        // The KayKit Keeper rig carries an Animator (the AnimatorSetup editor
        // script builds Hero.controller; the integrator assigns it to the hero
        // prefab — see docs/port-notes/animation-setup.md). DungeonHero DRIVES it:
        // the Speed float blends idle <-> walk from the planar move speed. The
        // Animator ref is null-guarded so locomotion still runs without a rig.
        private Animator _animator;

        /// <summary>Animator <c>Speed</c> float hash — matches AnimatorSetup.cs.</summary>
        private static readonly int AnimSpeed = Animator.StringToHash("Speed");
        // WO-163: cached once — whether the controller declares "Speed". The
        // per-frame SetFloat below logs an error every frame if it doesn't.
        private bool _hasSpeedParam;

        /// <summary>The current horizontal velocity (XZ), eased toward the input.</summary>
        private Vector3 _planarVelocity;

        /// <summary>Accumulated vertical velocity from gravity (negative = falling).</summary>
        private float _verticalVelocity;

        /// <summary>The active tap-to-move destination on the floor plane, when set.</summary>
        private Vector3 _moveTarget;

        /// <summary>True while a tap-to-move walk is in progress.</summary>
        private bool _hasMoveTarget;

        /// <summary>While false, input is ignored — the Keeper stands still (load / cutscene).</summary>
        private bool _inputEnabled = true;

        // ── Read-only state ──────────────────────────────────────────────────

        /// <summary>True when the Keeper is moving under its own power this frame.</summary>
        public bool IsMoving => _planarVelocity.sqrMagnitude > 0.0025f;

        /// <summary>The current planar speed in world units per second.</summary>
        public float CurrentSpeed => _planarVelocity.magnitude;

        /// <summary>True while a tap-to-move walk is in progress.</summary>
        public bool HasTapTarget => _hasMoveTarget;

        // ── Lifecycle ────────────────────────────────────────────────────────

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            if (_moveCamera == null) _moveCamera = Camera.main;
            // The Animator sits on the KayKit Keeper mesh child of the hero rig.
            _animator = GetComponentInChildren<Animator>();
            if (_animator != null && _animator.runtimeAnimatorController != null)
            {
                foreach (var p in _animator.parameters)
                    if (p.nameHash == AnimSpeed) { _hasSpeedParam = true; break; }
            }
        }

        /// <summary>
        /// Binds the camera the WASD vector is made relative to. Called by
        /// <see cref="DungeonController"/> once the Cinemachine rig is live so
        /// screen-up always maps to the camera's forward under the isometric tilt.
        /// </summary>
        public void SetCamera(Camera camera)
        {
            if (camera != null) _moveCamera = camera;
        }

        /// <summary>
        /// Enables or disables movement input. The dungeon controller disables it
        /// during the load teleport and (later) for ATB hand-off so the Keeper
        /// does not drift while a cutscene or battle owns the frame.
        /// </summary>
        public void SetInputEnabled(bool enabled)
        {
            _inputEnabled = enabled;
            if (!enabled)
            {
                _planarVelocity = Vector3.zero;
                _hasMoveTarget = false;
            }
        }

        /// <summary>
        /// Hard-places the Keeper at <paramref name="position"/> (and optional
        /// heading), cancelling any in-flight tap-move. Disables the
        /// CharacterController across the move so it does not fight the teleport
        /// — mirrors the pattern in <see cref="DungeonController"/>.
        /// </summary>
        public void Teleport(Vector3 position, float? facingY = null)
        {
            bool wasEnabled = _controller.enabled;
            _controller.enabled = false;

            transform.position = position;
            if (facingY.HasValue)
                transform.rotation = Quaternion.Euler(0f, facingY.Value, 0f);

            _controller.enabled = wasEnabled;

            _planarVelocity = Vector3.zero;
            _verticalVelocity = 0f;
            _hasMoveTarget = false;
        }

        // ── Per-frame ────────────────────────────────────────────────────────

        private void Update()
        {
            Vector3 desired = _inputEnabled ? ResolveDesiredDirection() : Vector3.zero;

            // Ease the planar velocity toward the desired heading × top speed.
            Vector3 targetVelocity = desired * _moveSpeed;
            _planarVelocity = Vector3.MoveTowards(
                _planarVelocity, targetVelocity, _acceleration * Time.deltaTime);

            ApplyGravity();
            FaceHeading();

            // One Move call per frame — planar slide + gravity together so the
            // CharacterController resolves wall collision and grounding once.
            Vector3 motion = (_planarVelocity + Vector3.up * _verticalVelocity)
                             * Time.deltaTime;
            _controller.Move(motion);

            // Feed the Animator's Speed float from the planar move speed so the
            // Keeper rig blends idle <-> walk. Null-guarded — no-op without a rig.
            if (_animator != null && _hasSpeedParam)
                _animator.SetFloat(AnimSpeed, _planarVelocity.magnitude);
        }

        // ── Input resolution ─────────────────────────────────────────────────

        /// <summary>
        /// Resolves this frame's desired move direction (unit-length on XZ, or
        /// zero). Keyboard input wins and cancels any tap-move; otherwise a tap
        /// is sampled and an in-flight tap-walk is advanced.
        /// </summary>
        private Vector3 ResolveDesiredDirection()
        {
            Vector3 keyboard = SampleDesktopMove();
            if (keyboard.sqrMagnitude > 0.0001f)
            {
                // Desktop players expect WASD to override a stale tap target.
                _hasMoveTarget = false;
                return keyboard;
            }

            TrySampleTap();
            return ResolveTapDirection();
        }

        /// <summary>
        /// Reads WASD / arrow keys and returns a camera-relative, unit-length
        /// move vector on the XZ plane (or zero when no key is held).
        /// </summary>
        private Vector3 SampleDesktopMove()
        {
            var kb = Keyboard.current;
            if (kb == null) return Vector3.zero;

            float x = 0f, z = 0f;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) x -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) x += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) z -= 1f;
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) z += 1f;

            if (x == 0f && z == 0f) return Vector3.zero;

            // Make the raw input camera-relative so "up" is screen-up under the
            // isometric tilt — project the camera basis onto the floor plane.
            Vector3 fwd = Vector3.forward, right = Vector3.right;
            if (_moveCamera != null)
            {
                fwd = Vector3.ProjectOnPlane(_moveCamera.transform.forward, Vector3.up);
                right = Vector3.ProjectOnPlane(_moveCamera.transform.right, Vector3.up);
                // Degenerate basis (camera looking straight down) — fall back.
                if (fwd.sqrMagnitude < 0.0001f) fwd = Vector3.forward;
                if (right.sqrMagnitude < 0.0001f) right = Vector3.right;
                fwd.Normalize();
                right.Normalize();
            }

            return (right * x + fwd * z).normalized;
        }

        /// <summary>
        /// If a fresh touch / mouse press happened this frame, raycasts onto the
        /// walkable layers and arms a tap-to-move walk to the hit point.
        /// </summary>
        private void TrySampleTap()
        {
            if (!TryGetTapScreenPosition(out Vector2 screenPos)) return;
            if (_moveCamera == null) return;

            Ray ray = _moveCamera.ScreenPointToRay(screenPos);
            if (Physics.Raycast(ray, out RaycastHit hit, _tapRayLength,
                    _walkableMask, QueryTriggerInteraction.Ignore))
            {
                _moveTarget = hit.point;
                _hasMoveTarget = true;
                FlowTrace.Step("Dungeon",
                    $"DungeonHero tap-to-move: armed walk to {hit.point} (tap screen={screenPos}).");
            }
        }

        /// <summary>
        /// True with the screen position of a tap that BEGAN this frame — a new
        /// touch contact or a left-mouse press. Held input does not re-arm a tap
        /// (that would fight a deliberate WASD or a settled destination).
        /// </summary>
        private static bool TryGetTapScreenPosition(out Vector2 screenPos)
        {
            screenPos = default;

            var touch = Touchscreen.current;
            if (touch != null && touch.primaryTouch.press.wasPressedThisFrame)
            {
                screenPos = touch.primaryTouch.position.ReadValue();
                return IsRealPointer(screenPos);
            }

            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                screenPos = mouse.position.ReadValue();
                return IsRealPointer(screenPos);
            }

            return false;
        }

        /// <summary>
        /// Rejects a phantom / synthesized pointer that reports position (0,0). On touch
        /// targets (incl. WebGL on iPad) there is no hover and the synthesized mouse or a
        /// stale finger can report (0,0) — the bottom-LEFT screen corner. A tap-to-move
        /// armed there walks the Keeper into the bottom-left corner and, if blocked, never
        /// arrives, so it slides there forever (owner repro: dungeon, "stuck sliding to the
        /// bottom-left, can't stop"). Mirrors BuildModeController's edge-scroll guard
        /// (realPointer = p.x &gt; 0.5 || p.y &gt; 0.5). A real tap always clears this.
        /// </summary>
        private static bool IsRealPointer(Vector2 screenPos)
        {
            bool real = screenPos.x > 0.5f || screenPos.y > 0.5f;
            if (!real)
            {
                FlowTrace.Throttle("Dungeon", "dungeon-hero-phantom-tap", 1f,
                    "DungeonHero tap-to-move: IGNORED a phantom pointer at (0,0) — a " +
                    "synthesized/no-hover finger would have driven the Keeper into the " +
                    "bottom-left corner. Guarded; no tap armed.");
            }
            return real;
        }

        /// <summary>
        /// Advances an in-flight tap-to-move walk: returns the unit direction to
        /// the target, or zero (and clears the target) once within the arrive
        /// radius. No-op when no tap target is armed.
        /// </summary>
        private Vector3 ResolveTapDirection()
        {
            if (!_hasMoveTarget) return Vector3.zero;

            Vector3 toTarget = _moveTarget - transform.position;
            toTarget.y = 0f;

            if (toTarget.sqrMagnitude <= _arriveDistance * _arriveDistance)
            {
                _hasMoveTarget = false;
                return Vector3.zero;
            }

            return toTarget.normalized;
        }

        // ── Motion helpers ───────────────────────────────────────────────────

        /// <summary>
        /// Applies downward gravity, latching a small ground-stick velocity while
        /// grounded so the controller stays planted on stairs / floor seams.
        /// </summary>
        private void ApplyGravity()
        {
            if (_controller.isGrounded && _verticalVelocity < 0f)
            {
                // A small constant keeps isGrounded latched without accumulating
                // an ever-growing fall speed while standing still.
                _verticalVelocity = -2f;
            }
            else
            {
                _verticalVelocity -= _gravity * Time.deltaTime;
            }
        }

        /// <summary>Turns the hero rig smoothly to face its current planar heading.</summary>
        private void FaceHeading()
        {
            Vector3 heading = _planarVelocity;
            heading.y = 0f;
            if (heading.sqrMagnitude < 0.0025f) return;

            // DEF-7: Tripo FBX exports have a 90° model offset — same fix as HeroLocomotion.cs
            Quaternion target = Quaternion.LookRotation(heading, Vector3.up)
                                * Quaternion.Euler(0f, -90f, 0f);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, target, _turnSpeed * Time.deltaTime);
        }

        // ── Editor gizmo ─────────────────────────────────────────────────────

        private void OnDrawGizmosSelected()
        {
            // Draw the live tap-to-move destination so the walk path is visible
            // while tuning in play mode.
            if (!_hasMoveTarget) return;
            Gizmos.color = new Color(0.965f, 0.788f, 0.478f, 0.9f);
            Gizmos.DrawWireSphere(_moveTarget, _arriveDistance);
            Gizmos.DrawLine(transform.position, _moveTarget);
        }
    }
}
