// =============================================================================
// HeroLocomotion — WASD / dpad walking for Hero (Blaise) in the village.
// -----------------------------------------------------------------------------
// CORRECTED 2026-06-12 — the old header LIED: it claimed "no NavMeshAgent — pure
// transform". The code is the OPPOSITE and trusting that comment mis-diagnosed
// every movement bug this session. HeroLocomotion is a NavMeshAgent driven
// KINEMATICALLY by input: Awake gets-or-adds a NavMeshAgent (updateRotation off,
// high speed so Move never caps), reads input -> eased Velocity -> _agent.Move(step)
// when on the navmesh, else transform.position += step (off-mesh fallback); facing
// via manual LookRotation. So debug "can't move / can't exit" via the NavMesh BAKE,
// not colliders. Input is camera-relative in follow (rotated by
// SmartMobileCamera.CameraYaw), world-absolute in top-down; read from Keyboard.current
// (WASD / arrows) or Gamepad.current via the new Input System (activeInputHandler
// "Both"). WarpTo disables -> warps -> re-enables the agent for scene-seam crossings.
// Movement is in the XZ plane; the agent keeps the hero grounded.
// =============================================================================

using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using DeNelle.Core.Combat; // ActorAnimator (IActorAnimator impl) for guarded Speed/Cast etc. drive

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

        // WO-423: face-the-target on attack. The player hero previously only faced its
        // MOVE direction (LookRotation on Velocity), so standing still froze facing at the
        // last travel dir — attacks/projectiles fired the wrong way. FaceToward lets the
        // attack/cast code request a brief yaw-slew toward the target; the existing
        // rotation update (the SOLE rotation writer — _agent.updateRotation=false) honors
        // it while there is ~no movement input, and movement input cancels it immediately
        // (we never fight LookRotation(Velocity)). Mirrors the companion slerp pattern
        // (StoryCompanion ~L612) but as a target-yaw + timer the locomotion already owns.
        private bool  _facingActive;        // a FaceToward request is in flight
        private float _faceTargetYaw;       // desired root Y-euler (degrees) to slew toward
        private float _faceHoldRemaining;   // seconds the face-request stays valid
        private const float FaceYawDegPerSec = 540f; // yaw slew rate while facing a target

        // WO-512 slice 3: lock-face / strafe. While a soft lock-on is engaged (driven by
        // HeroTargetIndicator), the hero continuously slews its root yaw toward the LOCKED
        // enemy INSTEAD of the move-direction LookRotation(Velocity) writer — even while
        // moving. The camera-relative MOVE vector is deliberately left untouched, so pressing
        // A/D translates the hero sideways while it keeps facing the orc → strafe falls out
        // for free. When _lockFaceActive is false / the flag is off / the target is null/dead,
        // the EXISTING LookRotation(Velocity) path runs byte-identical (zero regression).
        private bool      _lockFaceActive;  // a lock-face is engaged (HeroTargetIndicator drives it)
        private Transform _lockFaceTarget;  // the locked enemy transform to keep facing

        /// <summary>
        /// WO-423 — request a brief yaw-slew so the hero turns to face <paramref name="worldPoint"/>
        /// before/while attacking. Only applies while movement input is ~0 (so it never fights
        /// the move-direction LookRotation); a fresh movement input cancels it. Lasts up to
        /// <paramref name="hold"/> seconds — long enough to cover the attack's impact delay.
        /// Null-safe: a worldPoint level with the hero (no horizontal delta) is ignored.
        /// </summary>
        public void FaceToward(Vector3 worldPoint, float hold = 0.35f)
        {
            Vector3 to = worldPoint - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude < 0.0004f) return;   // target is on top of the hero — nothing to face

            _faceTargetYaw    = Quaternion.LookRotation(to.normalized, Vector3.up).eulerAngles.y;
            _faceHoldRemaining = Mathf.Max(0f, hold);
            _facingActive     = true;
        }

        /// <summary>
        /// WO-512 slice 3 — engage lock-face: while a lock-on holds <paramref name="target"/>, the hero
        /// auto-faces it (yaw slew, reusing <see cref="StepYaw"/>/_rotationSpeed) even while moving, so
        /// A/D strafe around it. Only the FACING is overridden — the camera-relative move vector is
        /// untouched. Null target is treated as a clear. Honored only while <c>FeatureFlags.LockOn</c>;
        /// suspended by the existing InputSuppressed / auto-walk early-returns (dialogue/cutscene win).
        /// </summary>
        public void SetLockFace(Transform target)
        {
            if (target == null) { ClearLockFace(); return; }
            _lockFaceTarget = target;
            _lockFaceActive = true;
            DeNelle.Core.Diagnostics.FlowTrace.Step("BattleArena",
                "LOCKON face-lock on '" + target.gameObject.name.Replace("(Clone)", "").Trim() + "'.");
        }

        /// <summary>
        /// WO-512 slice 3 — clear lock-face: the hero returns to the normal LookRotation(Velocity)
        /// facing writer (byte-identical to today). Called on lock release / target death.
        /// </summary>
        public void ClearLockFace()
        {
            if (!_lockFaceActive && _lockFaceTarget == null) return;
            _lockFaceActive = false;
            _lockFaceTarget = null;
            DeNelle.Core.Diagnostics.FlowTrace.Step("BattleArena", "LOCKON face-lock cleared -> free facing.");
        }

        // WO-512 slice 3: slew the root yaw toward the locked enemy (XZ direction only), reusing
        // StepYaw for the shortest-arc step at _rotationSpeed-equivalent feel. Caller already
        // verified _lockFaceTarget != null. If the target is on top of the hero (no horizontal
        // delta) we leave facing as-is (nothing meaningful to face). Yaw only — Y/pitch untouched,
        // matching the move-direction LookRotation writer it replaces.
        private void ApplyLockFaceYaw()
        {
            Vector3 to = _lockFaceTarget.position - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude < 0.0004f) return;   // enemy on top of the hero — nothing to face

            float curYaw    = transform.eulerAngles.y;
            float targetYaw  = Quaternion.LookRotation(to.normalized, Vector3.up).eulerAngles.y;
            // _rotationSpeed is a Slerp factor (~rad/sec-ish); scale to a per-frame degree cap so
            // the slew feel matches the normal Slerp path without re-introducing a second tunable.
            float maxDelta  = _rotationSpeed * 60f * Time.deltaTime;
            float nextYaw   = StepYaw(curYaw, targetYaw, maxDelta);
            transform.rotation = Quaternion.Euler(0f, nextYaw, 0f);
        }

        /// <summary>
        /// WO-423 — pure, edit-mode-testable shortest-arc yaw slew: step <paramref name="currentYaw"/>
        /// toward <paramref name="targetYaw"/> by at most <paramref name="maxDelta"/> degrees,
        /// wrapping across the ±180° seam so it always turns the short way. Degrees in/out.
        /// </summary>
        public static float StepYaw(float currentYaw, float targetYaw, float maxDelta)
        {
            float delta = Mathf.DeltaAngle(currentYaw, targetYaw); // shortest signed arc, [-180,180]
            if (Mathf.Abs(delta) <= maxDelta) return targetYaw;
            return currentYaw + Mathf.Sign(delta) * maxDelta;
        }

        // WO-377: global player-input suppression. Set true while a Yarn dialogue is on
        // screen so the player can't move / attack / cast / build during story beats —
        // a click on the dialogue box used to fall through to the world and fire the
        // hero's primary attack, breaking the sequence. HeroLocomotion is the canonical
        // hero-input component, so it OWNS this gate: it subscribes to the Yarn
        // DialogueRunner's start/complete events (see HookDialogueGate) and raises/clears
        // the flag. The other per-frame input readers (HeroAbilityInput,
        // PlayerAttackController, BuildModeController) consult it via this single static
        // so the whole player-input surface is suppressed from ONE place. Static (not
        // instance) so those readers don't need a hero reference; defaults false so
        // outside dialogue everything behaves exactly as before.
        public static bool InputSuppressed { get; private set; }

        // WO-557 (Yarn removed): we subscribe to OUR dialogue stack's engine-wide
        // Started/Ended signals (DeNelle.Core.Dialogue.DialogueService) to suppress player
        // input while a conversation is on screen. A static event is always available — no
        // runner to find, no retry coroutine. Guard so we subscribe exactly once per hero.
        private bool _dialogueHooked;

        // WO-383: teleport-aware warp. SceneTransitionTrigger / seam handoffs set the
        // hero far outside the off-mesh ±50 clamp and onto a separately-baked NavMesh
        // (OuterWorld). A raw transform.position = fights the agent + clamp every frame
        // (the "camera + direction break at the gate" bug). WarpTo disables the agent,
        // moves it, re-warps it onto the destination mesh, and raises OnTeleported so the
        // follow camera can snap instead of smooth-chasing the jump. _isTeleporting tells
        // the off-mesh clamp below to leave the warp frame alone.
        private bool _isTeleporting;

        /// <summary>
        /// WO-383 — raised after the hero is warped to a new world position (e.g. a scene
        /// seam). SmartMobileCamera subscribes to snap its seat so it never smooth-chases
        /// the teleport through intermediate positions.
        /// </summary>
        public event System.Action OnTeleported;

        /// <summary>
        /// WO-383 — teleport-aware reposition. Samples the nearest NavMesh point near
        /// <paramref name="worldPos"/> (so the hero lands on valid mesh even if the caller's
        /// target is slightly off), then disables / moves / re-warps the agent so it
        /// re-acquires the (possibly additively-loaded) destination NavMesh. Raises
        /// <see cref="OnTeleported"/> for the follow camera. Null-safe: with no agent it
        /// falls back to a plain transform move.
        /// </summary>
        public void WarpTo(Vector3 worldPos, Quaternion? rot = null)
        {
            _isTeleporting = true;   // clamp/movement skips this frame

            // §12 ticket #2: prove ground-side vs hero-side at the garrison warp. A MISS (or a large
            // sample distance) means the destination navmesh isn't online yet / isn't there — the agent
            // lands off-mesh and (no physics collider) falls through. Captured to break-log.
            bool sampled = NavMesh.SamplePosition(worldPos, out NavMeshHit hit, 5f, NavMesh.AllAreas);
            DeNelle.Core.Diagnostics.FlowTrace.Step("Seam",
                sampled
                    ? $"WarpTo sample HIT @ {hit.position} dist={Vector3.Distance(worldPos, hit.position):F2} (req {worldPos}) scene='{gameObject.scene.name}'"
                    : $"WarpTo sample MISS for {worldPos} (no navmesh within 5m) scene='{gameObject.scene.name}' — hero will land OFF-MESH.");
            if (sampled)
                worldPos = hit.position;   // land on valid mesh

            if (_agent != null)
            {
                _agent.enabled = false;            // critical before moving the transform
                transform.position = worldPos;
                _agent.Warp(worldPos);             // re-acquires the destination NavMesh
                _agent.enabled = true;
                _agent.ResetPath();
                DeNelle.Core.Diagnostics.FlowTrace.Step("Seam",
                    $"WarpTo post-warp: agent.isOnNavMesh={_agent.isOnNavMesh} @ {transform.position}");
            }
            else
            {
                transform.position = worldPos;
            }

            if (rot.HasValue) transform.rotation = rot.Value;

            Velocity = Vector3.zero;   // don't carry pre-warp momentum across the seam
            OnTeleported?.Invoke();

            _isTeleporting = false;
        }

        private ActorAnimator _actor; // lazy, for driving Speed into the (Humanoid) controller blendtree

        // WO-277 (tutorial auto-walk): while _autoWalkTarget is set, the tutorial
        // OWNS the hero — player input is ignored and the hero is driven toward the
        // target along the shared NavMesh (same agent/Move path as manual walking),
        // so the village tour "companion leads, hero follows" plays without input.
        // ClearAutoWalk restores normal control. Null-safe and self-contained: off
        // the tutorial these stay null and movement behaves exactly as before.
        private Transform _autoWalkTarget;
        // How close (XZ) the hero must get to the target before auto-walk reports
        // arrival (TutorialAutoWalk polls AutoWalkArrived to advance the waypoint).
        private const float AutoWalkArriveRadius = 1.6f;

        /// <summary>
        /// WO-277 — true while the tutorial is driving the hero (player input
        /// suppressed). TutorialAutoWalk / the director read this to know the hero
        /// is under scripted control.
        /// </summary>
        public bool IsAutoWalking => _autoWalkTarget != null;

        /// <summary>
        /// WO-277 — true once the hero is within <see cref="AutoWalkArriveRadius"/>
        /// (XZ) of the current auto-walk target. False when not auto-walking.
        /// </summary>
        public bool AutoWalkArrived
        {
            get
            {
                if (_autoWalkTarget == null) return false;
                Vector3 d = _autoWalkTarget.position - transform.position;
                d.y = 0f;
                return d.sqrMagnitude <= AutoWalkArriveRadius * AutoWalkArriveRadius;
            }
        }

        /// <summary>
        /// WO-277 — hand the hero to the tutorial: ignore player input and drive the
        /// hero toward <paramref name="target"/> via the existing NavMeshAgent. Call
        /// <see cref="ClearAutoWalk"/> to return control to the player. A null target
        /// is treated as a clear.
        /// </summary>
        public void SetAutoWalk(Transform target)
        {
            _autoWalkTarget = target;
            if (target == null) Velocity = Vector3.zero;
        }

        /// <summary>WO-277 — return movement control to the player (ends auto-walk).</summary>
        public void ClearAutoWalk()
        {
            _autoWalkTarget = null;
            Velocity = Vector3.zero;
        }

        // DEF-147 (Part B): off-mesh ground-snap / re-bind. When the hero leaves the
        // baked NavMesh (walks off a ledge / rampart edge), the agent's height-clamp
        // stops applying and the raw transform-fallback has NO downward force — so the
        // hero keeps its last Y forever ("hover exploit"). This re-binds the hero to
        // the navmesh (NavMesh.SamplePosition → pull Y down, re-warp the agent on)
        // instead of letting it float. Flag-guarded so it's instantly flippable to
        // false in playtest if it ever misbehaves; default true (it's a correctness fix).
        public static bool GroundSnapEnabled = true;

        // Accumulated downward fall velocity (m/s, magnitude) for a gravity-like snap
        // rather than a hard teleport. Reset to 0 whenever the hero is grounded/on-mesh.
        private float _fallSpeed;
        private const float FallAccel    = 30f;   // m/s² downward accel while floating
        private const float FallSpeedMax = 20f;   // m/s terminal fall speed (cap)
        // How far down/around to look for the nearest navmesh point to re-bind onto.
        private const float SnapSampleRadius = 6f;
        // Vertical band within which we re-warp the AGENT back onto the mesh (rather
        // than only nudging the transform) — i.e. the hero is essentially at ground.
        private const float ReBindYBand = 0.35f;

        private bool _loggedFirstInput;
        private Animator _animator;
        // WO-174 param-guard: cache whether the live controller actually declares the
        // float/trigger params we drive. SetFloat/SetTrigger on a controller WITHOUT
        // the param logs an error EVERY frame (the project-wide param-spam pitfall).
        // Recomputed whenever _animator is (re)resolved — HeroBodySwapper swaps the
        // controller at runtime, so we can't trust a one-shot Awake check.
        private Animator _paramCheckedAnimator;
        private bool _hasSpeedParam;
        private bool _hasVictoryParam;
        private NavMeshAgent _agent;   // unified navigation: hero shares the enemies' NavMesh

        // WO-387: follow camera for camera-relative movement. CameraYaw returns 0 in
        // top-down, so movement auto-degrades to world-absolute there. Cached in Start; lazy-refreshed.
        private SmartMobileCamera _smartCamera;
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
            _agent.updateRotation = false;   // facing handled manually by root LookRotation; HeroBodySwapper's -90f body yaw (WO-326) aligns the visual forward to root +Z
            _agent.updateUpAxis = false;
            _agent.autoBraking = false;
            _agent.stoppingDistance = 0f;
            _agent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;

            if (!TryGetComponent(out _actor)) _actor = gameObject.AddComponent<ActorAnimator>();
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

            // WO-387: cache the follow camera for the camera-relative movement basis.
            _smartCamera = Object.FindObjectOfType<SmartMobileCamera>();

            // WO-377 / WO-557: suppress player input while a conversation is on screen. We
            // subscribe to OUR dialogue stack's engine-wide Started/Ended signals (Yarn removed),
            // and reconcile to the LIVE state in case a dialogue is already running when we spawn.
            HookDialogueGate();
        }

        private void OnDestroy()
        {
            if (_waveManager != null)
                _waveManager.OnWaveCleared.RemoveListener(OnWaveCleared);

            // WO-557: unhook the dialogue signals and clear the global gate so a hero
            // destroyed mid-dialogue (e.g. scene swap) never leaves input stuck off.
            if (_dialogueHooked)
            {
                DeNelle.Core.Dialogue.DialogueService.Started -= OnDialogueStarted;
                DeNelle.Core.Dialogue.DialogueService.Ended -= OnDialogueEnded;
                _dialogueHooked = false;
            }
            InputSuppressed = false;
        }

        // WO-557: subscribe to OUR dialogue stack's Started/Ended events (parameterless,
        // always available — no runner to find, no retry coroutine). Then reconcile to the
        // live state: if a conversation is ALREADY running when this hero spawns (intro/FTUE),
        // raise suppression now so input never bleeds through the beat (the WO-377 symptom).
        private void HookDialogueGate()
        {
            if (_dialogueHooked) return; // already hooked
            DeNelle.Core.Dialogue.DialogueService.Started += OnDialogueStarted;
            DeNelle.Core.Dialogue.DialogueService.Ended   += OnDialogueEnded;
            _dialogueHooked = true;

            bool running = DeNelle.Core.Dialogue.DialogueService.IsRunning;
            if (running && !InputSuppressed)
            {
                InputSuppressed = true;
                Velocity = Vector3.zero;
                DeNelle.Core.Diagnostics.FlowTrace.Warn("UI",
                    "HeroLocomotion hooked a dialogue ALREADY in progress — suppressing input (catch-up for the missed Started).");
            }
            else if (!running && InputSuppressed)
            {
                // No dialogue live now — don't inherit a stale lock that would dead-freeze this fresh hero.
                InputSuppressed = false;
            }
        }

        // WO-377: dialogue opened — suppress player input and hold the hero in place. The
        // idle POSE is handled by WO-376 (HeroBodySwapper); here we only freeze control:
        // zero the velocity so the hero stops cleanly, and Update() short-circuits its
        // input read while the flag is set.
        private void OnDialogueStarted()
        {
            InputSuppressed = true;
            Velocity = Vector3.zero;
        }

        // WO-377: dialogue closed — restore normal player input.
        private void OnDialogueEnded()
        {
            InputSuppressed = false;
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
                if (_hasSpeedParam)   _animator.SetFloat(AnimSpeed, 0f);
                if (_hasVictoryParam) _animator.SetTrigger(AnimVictory);
            }

            Debug.Log($"[HeroLocomotion] Victory pose triggered — wave {waveId} cleared.");
        }

        // DEF-70: shared animator resolver — safe to call before HeroBodySwapper fires.
        private void ResolveAnimator()
        {
            if (_animator == null)
            {
                var bodyT = transform.Find("HeroBody");
                if (bodyT != null) _animator = bodyT.GetComponentInChildren<Animator>();
                if (_animator == null) _animator = GetComponentInChildren<Animator>();
            }
            // WO-174: (re)cache the param presence whenever the resolved Animator
            // changes — HeroBodySwapper assigns a fresh runtimeAnimatorController at
            // runtime, so a controller swap means we must re-scan its parameters.
            if (_animator != null && _animator != _paramCheckedAnimator)
                RefreshParamCache();
        }

        // WO-174 param-guard: scan the live controller's declared parameters once
        // per (re)resolve so the per-frame SetFloat/SetTrigger never hits a missing
        // param (which spams an error every frame). A null controller leaves every
        // flag false → the setters silently no-op until a valid controller binds.
        private void RefreshParamCache()
        {
            _paramCheckedAnimator = _animator;
            _hasSpeedParam = false;
            _hasVictoryParam = false;
            if (_animator == null || _animator.runtimeAnimatorController == null) return;
            foreach (var p in _animator.parameters)
            {
                if (p.type == AnimatorControllerParameterType.Float   && p.name == "Speed")   _hasSpeedParam = true;
                if (p.type == AnimatorControllerParameterType.Trigger && p.name == "Victory") _hasVictoryParam = true;
            }
        }

        /// <summary>
        /// WO-581: explicit animator injection — <see cref="HeroBodySwapper"/> calls this
        /// DIRECTLY after a body swap (no reflection) so this component drives Speed→Walk on the
        /// LIVE swapped rig. Re-scans the controller's params (a swap rebinds the animator).
        /// Replaces the brittle name-based reflection write that wrote 0 when this component
        /// wasn't on the root yet at swap time (the castle-hub "hero will not animate" regression).
        /// </summary>
        public void SetAnimator(Animator anim)
        {
            if (anim == null) return;
            _animator = anim;
            RefreshParamCache();
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

        // True only while a wave is genuinely live (Countdown or Active). Matches the
        // canonical combat signal used by HeroPoseController / CameraModeController /
        // BattleMusicManager — an idle WaveManager sitting in the hub is NOT combat, so
        // the hero keeps a relaxed idle in town and only draws ready when a wave runs.
        private bool IsWaveInCombat()
        {
            if (_waveManager == null) return false;
            var phase = _waveManager.Phase;
            return phase == WavePhase.Countdown || phase == WavePhase.Active;
        }

        // #51 footstep loop: a dedicated 2D AudioSource that loops the walk clip while the hero
        // moves and pauses when idle. No-op (clip stays null) until Resources/Sfx/FootstepsWalk drops.
        private AudioSource _footstepSrc;
        private bool _footstepTried;
        private const float FootstepMoveThreshold = 0.6f; // m/s of XZ velocity to count as "walking"

        private void DriveFootsteps()
        {
            if (!_footstepTried)
            {
                _footstepTried = true;
                var clip = Resources.Load<AudioClip>("Sfx/FootstepsWalk");
                if (clip != null)
                {
                    _footstepSrc = gameObject.AddComponent<AudioSource>();
                    _footstepSrc.clip = clip;
                    _footstepSrc.loop = true;
                    _footstepSrc.playOnAwake = false;
                    _footstepSrc.spatialBlend = 0f; // 2D — it's the player's own steps
                    _footstepSrc.volume = 0.10f;    // owner 2026-06-29: much softer (was 0.35)
                }
            }
            if (_footstepSrc == null) return;
            // Footsteps play ONLY while genuinely walking AND not in a battle — the kite-fight has the
            // hero moving constantly, so steps under combat audio are noise (owner 2026-06-29: stop in battle).
            bool walking = Velocity.sqrMagnitude > (FootstepMoveThreshold * FootstepMoveThreshold)
                           && !DeNelle.Village.Arena.BattleArena.AnyBattleInProgress;
            if (walking && !_footstepSrc.isPlaying) _footstepSrc.Play();
            else if (!walking && _footstepSrc.isPlaying) _footstepSrc.Pause();
        }

        private void Update()
        {
            TryResolveWaveManager();
            DriveFootsteps();

            // WO-377: while a Yarn dialogue is on screen, the player has no control —
            // hold the hero in place (zero velocity, no input read) so a click meant for
            // the dialogue box can't walk/turn the hero. The idle POSE is held by WO-376
            // (HeroBodySwapper). We still drive Speed=0 into the animator so the walk
            // blend settles to idle. Returns BEFORE the auto-walk branch so dialogue
            // suppression also pauses a scripted tour mid-step.
            if (InputSuppressed)
            {
                Velocity = Vector3.zero;
                ResolveAnimator();
                if (_animator != null && _hasSpeedParam) _animator.SetFloat(AnimSpeed, 0f);
                _actor?.SetLocomotion(0f);
                return;
            }

            // WO-277 (tutorial auto-walk): while the tutorial owns the hero, IGNORE
            // player input entirely and steer toward the scripted target. We build a
            // WORLD-SPACE move vector here and skip the camera-relative basis below
            // (so the synthetic heading isn't re-rotated by the camera yaw). Arrival
            // is reported via AutoWalkArrived; the tutorial clears the target to hand
            // control back. Returns through the SAME NavMesh Move() path as manual
            // walking so stairs/ramparts/walls behave identically.
            if (_autoWalkTarget != null)
            {
                // Crossings must fire during auto-walk too (bot tests + scripted cutscene walks), not just
                // player input — otherwise the hero auto-walks INTO a HeroLinkCrossing and never warps.
                if (TryTraverseSeamLink()) return;
                AutoWalkStep();
                return;
            }

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

            // WO-387: camera-relative in follow mode, world-absolute in top-down. SmartMobileCamera
            // .CameraYaw returns the player-pan yaw in 3rd-person follow and 0 in top-down/legacy, so a
            // camera-mode change can't break input (WO-368/363 intent preserved). Curl-safe: CameraYaw is
            // pan-driven, never velocity-driven. (Lazy re-fetch so the fix engages if the camera wired
            // up after the hero — otherwise it would silently no-op.)
            if (_smartCamera == null) _smartCamera = Object.FindObjectOfType<SmartMobileCamera>();
            float yaw = _smartCamera != null ? _smartCamera.CameraYaw : 0f;
            Quaternion cameraRotation = Quaternion.Euler(0f, yaw, 0f);
            Vector3 move = cameraRotation * new Vector3(input.x, 0f, input.y);
            if (move.sqrMagnitude > 1f) move.Normalize();

            // Smooth velocity toward target — instant max-speed felt rigid.
            // Higher accel when grabbing speed, higher decel when releasing,
            // so the hero responds promptly to a key press but glides slightly
            // when stopped (no instant-snap to zero).
            Vector3 targetVelocity = move * (_moveSpeed * HeroHealth.MoveSpeedMultiplier);   // injured-stance slow (1.0 = healthy)
            float maxStep = (targetVelocity.sqrMagnitude > Velocity.sqrMagnitude
                ? _accelMetresPerSec2
                : _decelMetresPerSec2) * Time.deltaTime;
            Velocity = Vector3.MoveTowards(Velocity, targetVelocity, maxStep);

            // WO-423: face-the-attack-target yaw slew. A fresh movement input cancels the
            // request immediately (we never fight the move-direction LookRotation below);
            // otherwise, while there is ~no movement input, slew the root yaw toward the
            // requested target for up to the hold duration. This is the SOLE rotation writer
            // (updateRotation=false), so honoring the request here is enough — no animator
            // turn-in-place (v1 = yaw slew only, deferred Lane 3 WO).
            bool hasMoveInput = input.sqrMagnitude > 0.0001f;
            if (_facingActive)
            {
                if (hasMoveInput)
                {
                    _facingActive = false;   // player took back control of facing
                }
                else
                {
                    _faceHoldRemaining -= Time.deltaTime;
                    if (_faceHoldRemaining <= 0f)
                    {
                        _facingActive = false;
                    }
                    else
                    {
                        float curYaw  = transform.eulerAngles.y;
                        float nextYaw = StepYaw(curYaw, _faceTargetYaw, FaceYawDegPerSec * Time.deltaTime);
                        transform.rotation = Quaternion.Euler(0f, nextYaw, 0f);
                    }
                }
            }

            // Manual seam-link traversal (WO-468 / navlink RCA 2026-06-20): the hero is
            // INPUT-driven (we call _agent.Move, NOT SetDestination), so it CANNOT auto-cross a
            // NavMeshLink — links are only traversed by a pathfinding agent. We carry it across the
            // castle<->OuterWorld gap IN-WORLD (no warp/fade). While a crossing runs it OWNS
            // movement, so suppress the normal Move this frame.
            bool seamConsumed = TryTraverseSeamLink();

            // WO-512 slice 3: is the lock-face governing facing this frame? Only when a lock is
            // engaged with a live target AND the flag is on. (InputSuppressed/auto-walk already
            // early-returned above, so dialogue/cutscene facing is never fought.) When false, the
            // EXISTING LookRotation(Velocity) writer below runs byte-identical — zero regression.
            bool lockFacing = _lockFaceActive && _lockFaceTarget != null &&
                              DeNelle.Core.FeatureFlags.LockOn;

            if (!seamConsumed && Velocity.sqrMagnitude > 0.0001f)
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
                // NOTE (WO-512): the move STEP is the camera-relative Velocity — UNCHANGED by
                // lock-face. Only the facing writer below differs, so locking the facing to the
                // orc while A/D keeps feeding a sideways move vector = strafe, for free.
                Vector3 step = Velocity * Time.deltaTime;
                if (_agent != null && _agent.isOnNavMesh)
                    _agent.Move(step);
                else
                    transform.position += step;

                if (lockFacing)
                {
                    // WO-512 slice 3: face the LOCKED enemy instead of the move direction. Reuse
                    // StepYaw + _rotationSpeed (degrees/sec) for the same slew feel; the move vector
                    // above is untouched, so this is the only difference vs. the normal path.
                    ApplyLockFaceYaw();
                }
                else
                {
                    // Face the move direction. HeroBodySwapper applies a root-child LocalRotation
                    // (WO-326: -90f, the proven companion value) so the visual mesh's authored +X
                    // forward aligns to the locomotion root's +Z. HeroLocomotion therefore does
                    // pure LookRotation on the velocity for the root transform — no extra Euler
                    // offset. (If the hero ever sidesteps again, the forwardYaw sign in
                    // HeroBodySwapper is the single place to flip.)
                    Quaternion target = Quaternion.LookRotation(Velocity.normalized);
                    transform.rotation = Quaternion.Slerp(
                        transform.rotation, target, _rotationSpeed * Time.deltaTime);
                }
            }
            else if (lockFacing && !seamConsumed)
            {
                // WO-512 slice 3: standing still but locked — keep facing the orc (the normal
                // path only re-faces while moving, which would freeze facing at the last travel
                // dir during a stationary duel). Move vector is zero here, so this is facing-only.
                ApplyLockFaceYaw();
            }

            // Core animation fix: drive the guarded ActorAnimator (used by DTT + village
            // heroes). This feeds the Speed float into the HeroAnimatorFactory blendtree
            // (Idle 0 / Walk 6 / Run 9) so basic locomotion plays. The old direct
            // _animator.SetFloat is kept for any legacy listeners; ActorAnimator is the
            // canonical (re-resolves on body swap, guards missing params).
            // During a manual seam slide we drive transform.position directly (the agent is released),
            // so Velocity may be ~0 even though the hero is moving — feed the animator a WALK speed so
            // the locomotion cycle plays through the crossing instead of freezing.
            _actor?.SetLocomotion(_crossingSeam ? _moveSpeed : Velocity.magnitude);

            // Battle Ready (stance) vs casual Idle: combat stance ONLY when actually in
            // combat — a live wave (Countdown/Active phase). Merely having a WaveManager in
            // the scene is NOT combat (the hub/town keeps one idle), so presence alone must
            // NOT raise the ready pose, or the hero stands weapon-ready in town. Movement is
            // intentionally NOT a combat trigger here: walking around town is relaxed, not
            // battle-ready. speed=0/moving + !combat = casual idle/walk; in-wave = ready.
            bool engaged = IsWaveInCombat();
            _actor?.SetCombatStance(engaged);

            // Edge/floor clamp + ground-snap ONLY when off the NavMesh (the transform
            // fallback). When the hero is ON the NavMesh, the bake defines the walkable
            // bounds + height, so a manual clamp would fight the agent (and break
            // ramparts/hills by pinning Y to 0).
            // WO-383: a seam warp (WarpTo) deliberately places the hero past ±50 and onto a
            // separately-baked NavMesh — skip the off-mesh clamp on that frame so the warp
            // isn't yanked back to the castle bounds.
            if ((_agent == null || !_agent.isOnNavMesh) && !_isTeleporting)
            {
                var p = transform.position;
                const float PlayableHalf = 50f;
                p.x = Mathf.Clamp(p.x, -PlayableHalf, PlayableHalf);
                p.z = Mathf.Clamp(p.z, -PlayableHalf, PlayableHalf);

                // DEF-147: off-mesh ground-snap / re-bind. The agent goes off-mesh both
                // when the hero walks off a real edge AND when the rampart lift
                // SUSPENDS the agent to hand-carry the hero up/down. We must NEVER snap
                // during the lift carry (it would yank the hero off the slab) — so gate
                // the whole snap on the lift NOT actively carrying. When uncertain, we
                // do nothing (preserve today's behavior) and just floor Y at 0 below.
                if (GroundSnapEnabled && !IsLiftCarrying())
                {
                    // Find the nearest navmesh point to the hero. If one exists within
                    // the sample radius, that's valid ground — pull the hero DOWN toward
                    // its Y at a gravity-like rate (never up; up is the agent's job /
                    // the lift's), so the hero falls to the surface instead of freezing.
                    // Walkable-only mask: exclude the built-in NotWalkable area (index 1) so the
                    // hero's off-mesh re-bind can't re-acquire the moat-water carve (CastleHubBuilder).
                    if (NavMesh.SamplePosition(p, out NavMeshHit hit, SnapSampleRadius, NavMesh.AllAreas & ~(1 << 1)))
                    {
                        float groundY = hit.position.y;
                        if (p.y > groundY + 0.01f)
                        {
                            // Accelerate a downward fall, capped, and step Y toward ground.
                            _fallSpeed = Mathf.Min(_fallSpeed + FallAccel * Time.deltaTime, FallSpeedMax);
                            p.y = Mathf.MoveTowards(p.y, groundY, _fallSpeed * Time.deltaTime);
                        }
                        else
                        {
                            // At or below the sampled ground — clamp to it, stop falling.
                            p.y = groundY;
                            _fallSpeed = 0f;
                        }

                        transform.position = p;

                        // Once essentially down at ground, re-bind the AGENT onto the mesh
                        // so it resumes its own height-follow (this also auto-heals the
                        // lift's "suspended-on-deck" float once the lift releases). Only
                        // when the agent is enabled but off-mesh — never re-enable an agent
                        // the lift deliberately disabled mid-ride (excluded above).
                        if (_agent != null && _agent.enabled && !_agent.isOnNavMesh &&
                            Mathf.Abs(p.y - hit.position.y) <= ReBindYBand)
                        {
                            _agent.Warp(hit.position);
                            _fallSpeed = 0f;
                        }
                        return;   // ground-snap handled the Y this frame
                    }
                }

                // Fallback (snap disabled, lift carrying, or no navmesh nearby). DEF-147 /
                // WO-254 hover-exploit ROOT FIX: when the snap finds no navmesh within
                // SnapSampleRadius — the exact case of the "walk off a ramp edge to float
                // above enemies" cheese, where the hero hangs HIGH over a gap with no mesh
                // below — the old code only clamped at Y=0 and otherwise PRESERVED the
                // airborne Y forever (the hover). Apply the same gravity-like fall toward the
                // world floor (Y=0) so the hero always descends instead of suspending midair.
                // The lift-carry case is excluded above (snap-gated), so this never fights the
                // rampart lift. Once at/below the floor, clamp to 0 and reset the fall.
                if (GroundSnapEnabled && !IsLiftCarrying() && p.y > 0f)
                {
                    _fallSpeed = Mathf.Min(_fallSpeed + FallAccel * Time.deltaTime, FallSpeedMax);
                    p.y = Mathf.MoveTowards(p.y, 0f, _fallSpeed * Time.deltaTime);
                }
                if (p.y <= 0f) { p.y = 0f; _fallSpeed = 0f; }
                transform.position = p;
            }
            else
            {
                _fallSpeed = 0f;   // on-mesh: agent owns height, reset any carried fall
            }

            // Self-heal the Animator reference (see ResolveAnimator for rationale).
            // Cheap: only runs while _animator is null, stops once wired.
            ResolveAnimator();
            if (_animator != null && _hasSpeedParam) _animator.SetFloat(AnimSpeed, Velocity.magnitude);
        }

        // ── Manual NavMeshLink traversal (WO-468) ────────────────────────────────────
        // The castle<->OuterWorld seam is a NavMeshLink. An INPUT-driven agent (Move, not
        // SetDestination) never auto-crosses links, so we do it ourselves: when the hero
        // reaches one end of the seam while pushing toward the other, slide it across the
        // gap IN-WORLD and re-snap onto the far navmesh (a continuous walk, NOT a warp/fade).
        // Endpoints MUST match CastleHubBuilder.BuildSeamlessOuterWorldSeam's
        // NavLink_CastleToOuterWorld (start on castle navmesh, end on OuterWorld).
        // Castle<->OuterWorld seam endpoints — the original in-world SLIDE crossing (kept working).
        private static readonly Vector3 SeamCastleEnd     = new Vector3(-4.37f, 0f, -63f);
        private static readonly Vector3 SeamOuterWorldEnd = new Vector3(-4.37f, 0f, -76f);
        private bool _crossingSeam;
        private Vector3 _seamTarget;
        private float _seamReengageAt;
        private bool _crossArmed = true;   // paired-crossing: fire on ENTER, re-arm when clear of all crossings

        /// <summary>True while it OWNS movement this frame (a crossing is starting or continuing);
        /// the caller must then skip the normal Move so we don't double-move.</summary>
        private bool TryTraverseSeamLink()
        {
            if (_crossingSeam)
            {
                Vector3 pos = transform.position;
                Vector3 flatTarget = new Vector3(_seamTarget.x, pos.y, _seamTarget.z);
                Vector3 to = flatTarget - pos; to.y = 0f;
                float stepLen = _moveSpeed * Time.deltaTime;
                if (to.magnitude <= stepLen + 0.05f)
                {
                    // Arrived: re-place the agent on the FAR navmesh (Warp re-binds the agent to
                    // whatever surface is under the point), then HAND CONTROL BACK to the agent.
                    if (_agent != null && _agent.enabled)
                    {
                        _agent.Warp(_seamTarget);
                        _agent.updatePosition = true;   // agent drives the transform again
                        _agent.updateRotation = true;
                    }
                    else transform.position = _seamTarget;
                    _crossingSeam = false;
                    _isTeleporting = false;
                    _seamReengageAt = Time.time + 1.0f;            // cooldown so we don't bounce back
                    Debug.Log($"[HeroLocomotion] seam-cross DONE -> {_seamTarget} (now on the far navmesh).");
                }
                else
                {
                    transform.position = pos + to.normalized * stepLen;
                    transform.rotation = Quaternion.Slerp(transform.rotation,
                        Quaternion.LookRotation(to.normalized), _rotationSpeed * Time.deltaTime);
                }
                return true;
            }

            Vector3 here = transform.position;

            // PAIRED-CROSSING WARP (HeroLinkCrossing, owner 2026-06-21): explicit id-paired portal. ENTER-TRIGGERED
            // (arm/disarm) — NO input-velocity requirement, so it fires whether the hero is driven by player input
            // OR auto-walk, and it's bot-testable. Fires once when the hero ENTERS an enterable crossing's radius;
            // re-arms only when clear of ALL crossings (so warping ONTO the partner never bounces back). Distance-
            // independent, no navmesh adjacency, no "closest" guessing. Checked BEFORE the legacy slide.
            bool nearAnyCrossing = false;
            for (int ci = 0; ci < HeroLinkCrossing.Registry.Count; ci++)
            {
                var c = HeroLinkCrossing.Registry[ci];
                if (c == null || !c.isActiveAndEnabled || !c.bidirectional) continue;
                if (HorizDist(here, c.transform.position) > c.enterRadius) continue;
                nearAnyCrossing = true;
                var partner = c.Partner();
                if (partner == null) continue;
                if (_crossArmed)
                {
                    WarpTo(partner.transform.position, transform.rotation);
                    _crossArmed = false;   // re-arms below once the hero leaves all crossing radii
                    Debug.Log($"[HeroLocomotion] crossing '{c.crossingId}' -> spawned at partner {partner.transform.position}.");
                    return true;
                }
            }
            if (!nearAnyCrossing) _crossArmed = true;   // clear of every crossing -> ready to fire again

            // ── LEGACY castle<->OuterWorld SLIDE (needs input velocity + direction) ──
            if (Time.time < _seamReengageAt) return false;
            Vector3 v = Velocity; v.y = 0f;
            if (v.sqrMagnitude < 0.0001f) return false;
            // CORRIDOR GUARD (additive safety): the slide may ONLY engage inside the seam corridor
            // (x≈-4.37, z∈[-63,-76]) so no stray navmesh edge triggers a false slide.
            bool inCorridor = Mathf.Abs(here.x - SeamCastleEnd.x) <= 8f && here.z <= -55f && here.z >= -84f;
            if (!inCorridor) return false;

            // Near one endpoint AND pushing toward the other? Begin a crossing.
            if (HorizDist(here, SeamCastleEnd) < 2.5f && Vector3.Dot(v, SeamOuterWorldEnd - SeamCastleEnd) > 0f)
            { BeginSeamCross(SeamOuterWorldEnd, here, v); return true; }
            if (HorizDist(here, SeamOuterWorldEnd) < 2.5f && Vector3.Dot(v, SeamCastleEnd - SeamOuterWorldEnd) > 0f)
            { BeginSeamCross(SeamCastleEnd, here, v); return true; }
            return false;
        }

        private void BeginSeamCross(Vector3 target, Vector3 from, Vector3 vel)
        {
            _crossingSeam = true;
            _seamTarget = target;
            _isTeleporting = true;   // skip the off-mesh ±50 clamp while we cross the gap (z<-50)
            // RELEASE the agent's grip on the transform during the slide — otherwise updatePosition
            // clamps the hero back onto the castle navmesh every frame and it never crosses the gap
            // (the BEGIN-without-DONE bug). We drive transform.position ourselves, then Warp + re-arm.
            if (_agent != null && _agent.enabled)
            {
                _agent.updatePosition = false;
                _agent.updateRotation = false;
            }
            // Capture from-pos + velocity so a fleet/playtest can CONFIRM whether any unexpected
            // SECOND cross fires (owner's "sliding again"). A forward cross reads from≈(-4.37,-63);
            // any BEGIN logged from a different spot is the stray second seam to chase.
            string dir = (target == SeamOuterWorldEnd) ? "castle→outer" : "outer→castle";
            Debug.Log($"[HeroLocomotion] seam-cross BEGIN [{dir}] from {from} -> {target} vel={vel} " +
                      "(manual NavMeshLink traversal, in-world walk).");
        }

        private static float HorizDist(Vector3 a, Vector3 b)
        {
            a.y = 0f; b.y = 0f; return Vector3.Distance(a, b);
        }

        // WO-277: one frame of tutorial-driven movement toward _autoWalkTarget. A
        // condensed twin of the manual-input branch above — world-space heading
        // (no camera basis), eased velocity, NavMesh Move(), face-the-direction,
        // Speed animator drive — so the scripted tour reads identically to the
        // player walking. Eases to a stop inside the arrive radius so the hero
        // settles at each waypoint instead of jittering against the target.
        private void AutoWalkStep()
        {
            Vector3 to = _autoWalkTarget.position - transform.position;
            to.y = 0f;
            float dist = to.magnitude;

            // Ease the desired speed down as the hero nears the target so it stops
            // cleanly at the waypoint (mirrors the pet's arrival damping).
            Vector3 dir = dist > 0.0001f ? to / dist : Vector3.zero;
            float speedScale = Mathf.Clamp01(dist / Mathf.Max(0.01f, AutoWalkArriveRadius));
            Vector3 targetVelocity = dir * (_moveSpeed * speedScale * HeroHealth.MoveSpeedMultiplier);   // injured-stance slow (1.0 = healthy)

            float maxStep = (targetVelocity.sqrMagnitude > Velocity.sqrMagnitude
                ? _accelMetresPerSec2
                : _decelMetresPerSec2) * Time.deltaTime;
            Velocity = Vector3.MoveTowards(Velocity, targetVelocity, maxStep);

            if (Velocity.sqrMagnitude > 0.0001f)
            {
                Vector3 step = Velocity * Time.deltaTime;
                if (_agent != null && _agent.isOnNavMesh)
                    _agent.Move(step);
                else
                    transform.position += step;

                Quaternion face = Quaternion.LookRotation(Velocity.normalized);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation, face, _rotationSpeed * Time.deltaTime);
            }

            // Drive the walk animation off the actual speed (guarded — controller
            // may lack the param, per WO-174).
            ResolveAnimator();
            if (_animator != null && _hasSpeedParam)
                _animator.SetFloat(AnimSpeed, Velocity.magnitude);
        }

        // DEF-147: the rampart lift suspends the hero's NavMeshAgent and hand-carries
        // the hero by setting transform.position every frame. During that ride the
        // ground-snap MUST stay out of the way (snapping would yank the hero off the
        // slab / fight the lift). LiftPlatform.AnyCarrying() is true only while a lift
        // is actively carrying — both live in DeNelle.Village, so this is a direct,
        // reflection-free static read that does not change any lift behavior.
        private static bool IsLiftCarrying()
        {
            return LiftPlatform.AnyCarrying();
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

            // On-screen virtual joystick (the web/mobile build's touch movement — the
            // only nav input when there's no keyboard/gamepad). Added BEFORE the legacy
            // fallback so it counts as real input and the deadzoned fallback is skipped.
            v += VirtualJoystick.Move;

            // HUD-001 / Lean D-Pad tie-in (rich dark fantasy mobile HUD): loose reflection read of
            // VirtualDPadLean.Move so the rich HUD's thumb D-Pad drives locomotion without creating
            // a hard assembly reference from DeNelle.Village to DeNelle.HUD. When the HUD is present
            // in a battle/map scene, its normalized Vector2 feeds movement (multi-touch safe).
            v += ReadHudDpadMove();

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

        // HUD-001: loose (reflection) read of the rich dark-fantasy HUD's VirtualDPadLean.Move.
        // No compile dependency on DeNelle.HUD — Type.GetType + static property lookup.
        // When the HUD prefab/manager is dropped into a battle or map scene, its thumb D-Pad
        // (Lean Touch only) supplies normalized movement that is OR-ed with other inputs.
        private static Vector2 ReadHudDpadMove()
        {
            try
            {
                var t = System.Type.GetType("DeNelle.HUD.VirtualDPadLean, DeNelle.HUD");
                if (t == null) return Vector2.zero;
                var p = t.GetProperty("Move", BindingFlags.Public | BindingFlags.Static);
                if (p == null) return Vector2.zero;
                var val = p.GetValue(null);
                return val is Vector2 v ? v : Vector2.zero;
            }
            catch
            {
                return Vector2.zero;
            }
        }
    }
}
