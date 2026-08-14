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
//
// ── OWNERSHIP + FEED + BASIS (WO-968 / WO-1016, 2026-08-10) — READ THIS FIRST ──
// * TRANSFORM: this component is the SOLE integrator while its CharacterController is
//   ENABLED. The injected village HeroLocomotion now stands down on exactly that
//   condition (HeroLocomotion.ForeignMoverOwnsTransform / SelfMayWriteTransform), which
//   is the mirror of the "CC disabled -> the arena owns the hero" guard in Update below.
//   Exactly one of the two writes this transform on any frame, decided by capability —
//   NOT by the old static side-channel, which the owner's capture proved can lapse.
// * ANIMATOR: Speed has ONE writer. When an ActorAnimator is on this rig (HeroLocomotion
//   adds it), IT owns Speed and is fed the MEASURED root speed — i.e. whatever this
//   component's CharacterController.Move actually did. We write Speed only when no
//   ActorAnimator is present. The Animator handle is re-resolved every frame while dead;
//   caching it once in Awake (before the async body swap) made the write a permanent
//   silent no-op, which is half of "the hero slides in an idle clip".
// * BASIS: every stick sampler goes through CameraRelative(), which re-resolves the
//   camera and FAILS LOUDLY when there is none. See the P1 owner pin on DungeonStickBasis
//   — camera-relative vs Keeper-relative is one value away.
// =============================================================================

using UnityEngine;
using UnityEngine.InputSystem;
using DeNelle.Core;
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
        // WO-163: whether the live controller declares "Speed". The per-frame SetFloat below
        // logs an error every frame if it doesn't.
        // WO-968 E13 — THIS USED TO BE RESOLVED EXACTLY ONCE, IN Awake, AND THAT IS A DEFECT.
        // Awake runs BEFORE the async HeroBodySwapper rebuilds the Keeper's rig (the swap lands
        // at the END of DungeonController's ready sequence, ~160 ms later), so on a body-swapped
        // Keeper this component could hold a DESTROYED / placeholder Animator for the entire run
        // — and the guarded SetFloat below then wrote NOTHING, forever, with no error. The rig is
        // now re-resolved through ResolveAnimator(), the same self-heal idiom HeroLocomotion has
        // had since DEF-70/WO-174 (this component simply never got it).
        private bool _hasSpeedParam;
        private Animator _paramCheckedAnimator;

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
            ResolveMoveCamera();
            // The Animator sits on the KayKit Keeper mesh child of the hero rig. This is only a
            // FIRST attempt — the real rig may not exist yet (async body swap). ResolveAnimator
            // re-runs every frame while the handle is dead, so a late swap is picked up.
            ResolveAnimator();
        }

        // ── Animator self-heal (WO-968 E13/E15) ──────────────────────────────────────
        // Mirrors HeroLocomotion.ResolveAnimator + RefreshParamCache deliberately, rather than
        // inventing a second mechanism: re-resolve while the handle is null (a DESTROYED Unity
        // object compares == null, so a body swap heals itself), drop a handle that is no longer
        // part of this rig, and re-scan the declared parameters whenever the Animator INSTANCE
        // changes (a swap rebinds the runtimeAnimatorController too). Cheap on the steady state:
        // one reference compare once the rig is wired.
        private void ResolveAnimator()
        {
            // A stale-but-alive handle: the swapper can leave the old body alive-but-detached.
            if (_animator != null && !_animator.transform.IsChildOf(transform))
                _animator = null;

            if (_animator == null)
            {
                var bodyT = transform.Find("HeroBody");
                if (bodyT != null) _animator = bodyT.GetComponentInChildren<Animator>();
                if (_animator == null) _animator = GetComponentInChildren<Animator>();
            }

            if (_animator != null && _animator != _paramCheckedAnimator)
                RefreshParamCache();
        }

        private void RefreshParamCache()
        {
            _paramCheckedAnimator = _animator;
            _hasSpeedParam = false;
            if (_animator == null || _animator.runtimeAnimatorController == null) return;
            foreach (var p in _animator.parameters)
                if (p.nameHash == AnimSpeed) { _hasSpeedParam = true; break; }

            FlowTrace.Step("DungeonMover",
                $"animator RE-RESOLVED on '{name}': animator='{_animator.name}' " +
                $"controller='{(_animator.runtimeAnimatorController != null ? _animator.runtimeAnimatorController.name : "<null>")}' " +
                $"hasSpeedParam={_hasSpeedParam}. (Awake-only caching was WO-968 E13 — a handle " +
                "resolved before the async body swap made every Speed write a silent no-op.)");
        }

        // ── ONE SPEED OWNER (WO-968 F2) ──────────────────────────────────────────────
        // Two components were writing the SAME Animator "Speed" parameter on the dungeon Keeper:
        // this one (directly) and DeNelle.Core.Combat.ActorAnimator (driven by HeroLocomotion,
        // which HeroBodySwapper injects onto the same rig). ActorAnimator is the canonical writer
        // — it re-resolves the Animator AND its controller across body swaps, guards missing
        // params, and damps the feed — and it is now fed the MEASURED root speed, so it publishes
        // whatever ACTUALLY moved the hero, including this component's CharacterController.Move.
        // So when it is present on this rig we YIELD rather than fight it; when it is absent
        // (a dungeon Keeper with no village rig injected) we remain the writer, which is why the
        // resolve above still has to be correct.
        private DeNelle.Core.Combat.ActorAnimator _actorProbe;

        private bool ActorAnimatorOwnsSpeed()
        {
            // Re-probe while null so it self-heals in BOTH directions (injected late by the body
            // swap; destroyed on teardown — a destroyed Unity object compares == null).
            if (_actorProbe == null) _actorProbe = GetComponent<DeNelle.Core.Combat.ActorAnimator>();
            return _actorProbe != null && _actorProbe.isActiveAndEnabled;
        }

        /// <summary>
        /// Pure ownership rule for the Animator Speed parameter — regression-testable with no
        /// scene. This component writes Speed only when no ActorAnimator owns it AND it has a
        /// live handle with the parameter declared.
        /// </summary>
        public static bool ShouldWriteSpeed(bool actorAnimatorPresent, bool animatorResolved, bool hasSpeedParam)
        {
            return !actorAnimatorPresent && animatorResolved && hasSpeedParam;
        }

        /// <summary>Guarded Speed write that honours <see cref="ShouldWriteSpeed"/>.</summary>
        private void DriveSpeed(float speed)
        {
            if (!ShouldWriteSpeed(ActorAnimatorOwnsSpeed(), _animator != null, _hasSpeedParam)) return;
            _animator.SetFloat(AnimSpeed, speed);
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
            // Self-heal the Animator handle FIRST, every frame, before anything reads it — the
            // async body swap lands mid-run and Awake's resolve is stale from that moment on.
            ResolveAnimator();

            // Audit R-A1 (2026-08-01): while DungeonController disables this
            // CharacterController for a real-time arena fight (the arena's
            // HeroLocomotion is then the SOLE mover on this transform), skip the
            // whole movement step — Move() on a disabled controller is ignored,
            // and accumulating gravity/velocity here would slam the hero on
            // re-enable. State is held neutral; Teleport's same-frame CC toggle
            // never observes this guard (it re-enables before Update runs).
            if (_controller == null || !_controller.enabled)
            {
                FlowTrace.Once("Dungeon", "hero-cc-disabled",
                    "DungeonHero.Update: CharacterController disabled -- movement step skipped (arena owns the hero).");
                _planarVelocity = Vector3.zero;
                _verticalVelocity = 0f;
                _hasMoveTarget = false;
                DriveSpeed(0f);
                return;
            }

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

            // Feed the Animator's Speed float from the planar move speed so the Keeper rig blends
            // idle <-> walk — but ONLY while no ActorAnimator owns the parameter (WO-968 F2).
            DriveSpeed(_planarVelocity.magnitude);

            TraceMoverHeartbeat();
        }

        // ── [Flow:DungeonMover] 1 Hz heartbeat (WO-968, §12) ─────────────────────────
        // Owner F8 2312, verbatim: "Everything is wrong check locomotion". The capture proved the
        // hero's world position changed while [Flow:HeroLoco] reported vel=0.00 and the animator
        // held a single idle clip — but nothing in the log could say whether THIS component was the
        // one moving it, nor whether its Animator write was landing anywhere.
        //
        // The specific hazard this line makes visible: `_animator` and `_hasSpeedParam` are resolved
        // EXACTLY ONCE, in Awake (:143-148), and Awake runs BEFORE the async HeroBodySwapper builds
        // the real rig (DungeonController:753 — the swap lands at the END, ~160 ms later). So on a
        // body-swapped Keeper this component can be holding a destroyed/placeholder Animator, or
        // none, for the entire run — and the guarded SetFloat above then silently writes NOTHING,
        // forever, with no error. That is invisible in every existing trace; it is one field here.
        // Pair it with [Flow:HeroOwner] on the same rig to attribute the move in one read.
        private void TraceMoverHeartbeat()
        {
            if (!FlowTrace.Enabled) return;

            FlowTrace.Throttle("DungeonMover", "mover", 1f,
                $"planarVel={_planarVelocity.magnitude:F2} inputEnabled={_inputEnabled} " +
                $"cc={(_controller != null && _controller.enabled ? "LIVE" : "disabled/none")} " +
                $"tapTarget={_hasMoveTarget} yaw={transform.eulerAngles.y:F1} pos={transform.position:F2} " +
                $"animator={(_animator != null ? _animator.name : "<null/destroyed>")} " +
                $"hasSpeedParam={_hasSpeedParam} " +
                // WO-968 F2: which component owns the Speed parameter on this rig.
                $"speedOwner={(ActorAnimatorOwnsSpeed() ? "ActorAnimator(fed measured root speed)" : "DungeonHero")} " +
                $"animSpeed={(_animator != null && _hasSpeedParam ? _animator.GetFloat(AnimSpeed) : float.NaN):F2} " +
                $"basis={DungeonStickBasis} " +
                $"moveCam={(_moveCamera != null ? _moveCamera.name : "<null>")} " +
                $"camYaw={(_moveCamera != null ? _moveCamera.transform.eulerAngles.y : -1f):F1}");

            // Called out as a failure the moment it is true: this component is translating the root
            // and NOBODY can publish that to the animator — its own handle is dead AND no
            // ActorAnimator owns the parameter — so the walk cycle can never play. (When
            // ActorAnimator DOES own it, a null handle here is expected, not a defect.)
            if (_planarVelocity.magnitude > 0.5f && !ActorAnimatorOwnsSpeed()
                && (_animator == null || !_hasSpeedParam))
                FlowTrace.Throttle("DungeonMover", "dead-anim", 1f,
                    $"DungeonHero is MOVING at {_planarVelocity.magnitude:F2} m/s but its Animator handle is " +
                    $"{(_animator == null ? "NULL/DESTROYED" : "missing the Speed parameter")} and no " +
                    "ActorAnimator owns Speed — nothing can drive the walk cycle on this rig.");
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

            // Mobile: the shared on-screen movement joystick (reused village
            // VirtualJoystick — bottom-left stick, touch-only). Camera-relative like WASD.
            Vector3 stick = SampleJoystickMove();
            if (stick.sqrMagnitude > 0.0001f)
            {
                _hasMoveTarget = false;
                return stick;
            }

            // F8 2026-07-30 ("dungeon doesnt seem to allow movement"): the kit HUD D-pad
            // publishes DeNelle.HUD.Kit.HudMoveInput, which only HeroLocomotion reads — and
            // the dungeon deliberately zeroes HeroLocomotion input every frame
            // (EnsureSingleDungeonMover's scripted-move stomp). So the owner's on-screen
            // D-pad moved NOTHING here; only WASD worked. Read the same seam (loose
            // reflection — Dungeons must not reference DeNelle.HUD), camera-relative
            // like the stick.
            Vector3 dpad = SampleKitDpadMove();
            if (dpad.sqrMagnitude > 0.0001f)
            {
                _hasMoveTarget = false;
                return dpad;
            }

            // TAP-TO-MOVE FPV GATE: in first-person a screen tap is a LOOK, not a walk
            // (DungeonCameraRig consumes right-half drags for the free-look), so do not
            // arm a tap-to-move destination while FPV is active. The over-the-shoulder /
            // iso modes keep tap-to-move.
            if (!FeatureFlags.DungeonFpv)
                TrySampleTap();
            return ResolveTapDirection();
        }

        // ── THE MOVEMENT BASIS — one site, never a silent identity (WO-968 S3) ───────────────
        // Three samplers (WASD, joystick, kit D-pad) each carried their OWN copy of the
        // camera-projection block, and every copy degraded to `Vector3.forward / Vector3.right`
        // when _moveCamera was null — a SILENT world-absolute basis, indistinguishable in the log
        // from a working camera-relative one. _moveCamera was also resolved once in Awake, so a
        // camera created after this component (the dungeon rig is bound later by DungeonController)
        // left it null for the whole run. One resolver, one projection, one loud failure.
        //
        // ⚠ OPEN OWNER PIN P1 (WO-968 §12) — THE FEEL CALL IS NOT OURS.
        // Camera-relative is shipped because it matches town and is what players expect. The
        // alternative — Keeper-relative, where the stick is read against the hero's own visual
        // facing — is ONE VALUE away: change DungeonStickBasis to StickBasis.KeeperRelative.
        // Nothing else needs to change; both bases resolve through CameraRelative() below.
        private enum StickBasis
        {
            /// <summary>Stick is read against the view (town parity). SHIPPED.</summary>
            CameraRelative,
            /// <summary>Stick is read against the Keeper's own visual forward.</summary>
            KeeperRelative
        }

        private const StickBasis DungeonStickBasis = StickBasis.CameraRelative;

        /// <summary>Degrees the Tripo FBX visual forward leads the root transform (DEF-7).</summary>
        private const float ModelYawOffset = 90f;

        private float _nextBasisFailAt;

        /// <summary>
        /// Re-resolves the camera the stick is read against while it is null. A destroyed camera
        /// compares == null, so this self-heals in both directions (same idiom as ResolveAnimator).
        /// </summary>
        private void ResolveMoveCamera()
        {
            if (_moveCamera == null) _moveCamera = Camera.main;
        }

        /// <summary>
        /// Converts a raw stick/keys vector into a world-space XZ direction using the ACTIVE basis.
        /// Magnitude is preserved (analog speed) and clamped to 1. A missing basis is REPORTED,
        /// never silently resolved to identity.
        /// </summary>
        private Vector3 CameraRelative(Vector2 raw)
        {
            Vector3 fwd, right;

            if (DungeonStickBasis == StickBasis.KeeperRelative)
            {
                // ⚠ STALE AS OF 2026-08-14 — this branch is DEAD (DungeonStickBasis is
                // CameraRelative) and its premise is now false: FaceHeading no longer applies a
                // -90 model offset, so the root forward IS the visual forward and this +90 would
                // over-rotate. If KeeperRelative is ever switched on, ModelYawOffset must go to 0
                // or be derived — do not resurrect this branch as written.
                Quaternion visual = transform.rotation * Quaternion.Euler(0f, ModelYawOffset, 0f);
                fwd = Vector3.ProjectOnPlane(visual * Vector3.forward, Vector3.up);
                right = Vector3.ProjectOnPlane(visual * Vector3.right, Vector3.up);
            }
            else
            {
                ResolveMoveCamera();
                if (_moveCamera == null)
                {
                    // §12: no silent failures. With no camera the stick is world-absolute — say so.
                    if (Time.realtimeSinceStartup >= _nextBasisFailAt)
                    {
                        _nextBasisFailAt = Time.realtimeSinceStartup + 5f;
                        FlowTrace.Fail("DungeonMover",
                            "NO MOVEMENT BASIS: DungeonHero has no camera to read the stick against " +
                            "(_moveCamera null and Camera.main absent), so 'forward' means world +Z " +
                            "regardless of the view. The dungeon rig binds the camera via " +
                            "DungeonController/SetCamera — if this fires, that bind never happened.");
                    }
                    fwd = Vector3.forward;
                    right = Vector3.right;
                }
                else
                {
                    fwd = Vector3.ProjectOnPlane(_moveCamera.transform.forward, Vector3.up);
                    right = Vector3.ProjectOnPlane(_moveCamera.transform.right, Vector3.up);
                }
            }

            // Degenerate basis (camera looking straight down): keep the world axes rather than
            // normalising a zero vector into a NaN heading.
            if (fwd.sqrMagnitude < 0.0001f) fwd = Vector3.forward;
            if (right.sqrMagnitude < 0.0001f) right = Vector3.right;
            fwd.Normalize();
            right.Normalize();

            Vector3 dir = right * raw.x + fwd * raw.y;
            if (dir.sqrMagnitude > 1f) dir.Normalize();
            return dir;
        }

        // ── Kit D-pad seam (loose reflection; cached) — F8 2026-07-30 ─────────
        private static System.Reflection.PropertyInfo s_hudMoveProp;
        private static bool s_hudMoveResolved;

        private Vector3 SampleKitDpadMove()
        {
            if (!s_hudMoveResolved)
            {
                s_hudMoveResolved = true;
                try
                {
                    var t = System.Type.GetType("DeNelle.HUD.Kit.HudMoveInput, DeNelle.HUD");
                    s_hudMoveProp = t != null ? t.GetProperty("Move") : null;
                }
                catch { s_hudMoveProp = null; }
                if (s_hudMoveProp == null)
                    FlowTrace.Warn("Dungeon",
                        "SampleKitDpadMove: HudMoveInput.Move not resolvable — kit D-pad cannot move the Keeper.");
            }
            if (s_hudMoveProp == null) return Vector3.zero;

            Vector2 raw = default;
            try { raw = (Vector2)s_hudMoveProp.GetValue(null); } catch { return Vector3.zero; }
            if (raw.sqrMagnitude < 0.02f * 0.02f) return Vector3.zero;
            return CameraRelative(raw);
        }

        /// <summary>
        /// Reads the shared on-screen <see cref="DeNelle.Village.VirtualJoystick"/> (the
        /// same bottom-left thumbstick the village hero uses; touch-only, self-bootstrapping)
        /// and returns a camera-relative move vector on the XZ plane, magnitude 0..1 for
        /// analog speed. Zero when the stick is idle or absent (desktop uses WASD).
        /// </summary>
        private Vector3 SampleJoystickMove()
        {
            Vector2 stick = DeNelle.Village.VirtualJoystick.Move;
            if (stick.sqrMagnitude < 0.02f * 0.02f) return Vector3.zero;   // deadzone
            return CameraRelative(stick);
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

            // Make the raw input camera-relative so "up" is screen-up under the tilt.
            Vector3 dir = CameraRelative(new Vector2(x, z));
            return dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector3.zero;
        }

        /// <summary>
        /// If a fresh touch / mouse press happened this frame, raycasts onto the
        /// walkable layers and arms a tap-to-move walk to the hit point.
        /// </summary>
        private void TrySampleTap()
        {
            if (!TryGetTapScreenPosition(out Vector2 screenPos)) return;
            ResolveMoveCamera();   // WO-968: the Awake-time cache can be null for the whole run
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

            // ⛔ THE -90 IS REMOVED — owner report 2026-08-14: "rotation of hero off. Facing left
            // always so 90 degrees." It was a DOUBLE CORRECTION, and the comment that justified it
            // was false.
            //
            // It read: "DEF-7: Tripo FBX exports have a 90° model offset — same fix as
            // HeroLocomotion.cs". HeroLocomotion does NOT do this: a sweep for Euler(0f, ±90) in
            // that file returns ZERO hits; town writes rotation straight from the heading
            // (HeroLocomotion.cs:232 / :1063). So the dungeon was the only place applying it.
            //
            // The model offset is already owned, PER CLASS, by HeroBodySwapper.cs:263 —
            // `forwardYaw = (cls == HeroClass.Knight) ? 15f : -90f` — applied to the body root at
            // swap time. Adding another -90 here rotated the whole rig on top of a body that was
            // already correct, which is exactly the reported 90° left-facing on KnightV3 (whose
            // body yaw is 15f, not -90f — the -90 premise belongs to the RETIRED Tripo body).
            //
            // Now: face the heading, and let the body's own convention do the model correction.
            // One owner per concern (ARCHITECTURE_PRINCIPLES §2b.1).
            Quaternion target = Quaternion.LookRotation(heading, Vector3.up);
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
