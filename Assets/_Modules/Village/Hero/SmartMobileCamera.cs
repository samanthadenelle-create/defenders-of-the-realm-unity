// =============================================================================
// SmartMobileCamera (DEF-53) — adaptive third-person follow with auto-framing.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// WHAT IT DOES:
//   A drop-in companion/replacement for VillageCamera with three layers of
//   adaptive behaviour tuned for mobile touch play:
//
//   1. MOVEMENT LEAD — the look-at point scoots forward in the hero's movement
//      direction, creating a sense of purpose and giving the player more runway
//      to react to incoming threats.
//
//   2. COMBAT ZOOM — when enemies are in range the camera subtly widens FOV and
//      increases the chase distance so the player sees more of the battle. The
//      zoom smoothly reverts to the idle offset when the area is clear.
//
//   3. AUTO-FRAMING (optional) — with framing enabled the camera interpolates
//      the look-at point toward the centroid of the hero + the nearest visible
//      threat, keeping both on screen. Can be toggled at runtime.
//
// ARCHITECTURE:
//   * Add this component alongside (or instead of) VillageCamera. Call
//     SetTarget(heroTransform) from VillageController / VillageSceneBuilder.
//   * Uses Physics.OverlapSphereNonAlloc for enemy scans — one per
//     _enemyScanInterval seconds, not per-frame.
//   * All camera motion is in LateUpdate (same as VillageCamera) so it
//     composes cleanly with Animator root-motion.
//   * Uses Time.unscaledDeltaTime so the camera doesn't freeze during hit-stop.
//
// TUNING CHEATSHEET (Inspector):
//   _followOffset      — idle world-space offset behind/above the hero
//   _combatZoomOut     — extra distance added to offset.z during combat
//   _combatFovBoost    — extra degrees added to the camera's FOV in combat
//   _leadDistance      — how far ahead of movement to bias the look-at
//   _smoothTime        — SmoothDamp follow smoothness
//   _combatScanRadius  — enemy detection radius for combat zoom / framing
//   _enemyScanInterval — seconds between enemy proximity scans
//   _framingEnabled    — enable auto-framing toward the nearest enemy
// =============================================================================

using UnityEngine;
using System.Collections.Generic;

namespace DeNelle.Village
{
    /// <summary>
    /// Adaptive mobile follow camera with movement-lead, combat zoom, and
    /// auto-framing. Drop-in companion / replacement for <see cref="VillageCamera"/>.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class SmartMobileCamera : MonoBehaviour
    {
        [Header("Target")]
        [Tooltip("The hero root to follow. Set by VillageController.SetTarget.")]
        [SerializeField] private Transform _target;

        [Header("Follow offset (idle)")]
        [Tooltip("World-space offset from the hero in idle/explore state. Owner 2026-06-02: " +
                 "this is a 3D game — committed to a CLOSE cinematic third-person (was 0,18,-22 " +
                 "= flat top-down board-game seat that killed the 3D feel). Awake() auto-migrates " +
                 "the legacy high value baked into Village.unity to this default, so it applies on " +
                 "the next Play with NO rebake and NO scene edit. Tune Y=height, Z=distance live to " +
                 "taste; any value below the legacy threshold is honored.")]
        [SerializeField] private Vector3 _followOffset = new Vector3(0f, 3.5f, -6f);

        // Close cinematic 3D third-person. Awake() snaps the retired top-down seat to this.
        private static readonly Vector3 DefaultFollowOffset = new Vector3(0f, 3.5f, -6f);
        private const float LegacyHighOffsetY = 14f;   // old TD seat sat at y=18; >=14 => retire it

        [Tooltip("Look-at height above hero feet (metres).")]
        [SerializeField] private float _lookAtHeight = 2.5f;

        [Header("Movement lead")]
        [Tooltip("How far ahead of the hero's movement direction the look-at point is biased. " +
                 "0 = no lead; 3–5 is a comfortable mobile feel.")]
        [SerializeField, Min(0f)] private float _leadDistance = 3.5f;

        [Tooltip("Seconds for the lead point to catch up when the hero stops.")]
        [SerializeField, Min(0.05f)] private float _leadSmoothTime = 0.3f;

        [Header("Smoothing")]
        [Tooltip("Position SmoothDamp time (seconds). Lower = snappier.")]
        [SerializeField, Min(0.01f)] private float _smoothTime = 0.10f;

        [Header("Combat zoom")]
        [Tooltip("Radius within which enemies trigger the combat-zoom state.")]
        [SerializeField, Min(1f)] private float _combatScanRadius = 12f;

        [Tooltip("Seconds between enemy proximity scans (use 0.2–0.4 for mobile).")]
        [SerializeField, Range(0.05f, 1f)] private float _enemyScanInterval = 0.25f;

        [Tooltip("Extra backward distance added to the follow offset during combat.")]
        [SerializeField, Min(0f)] private float _combatZoomOut = 2.5f;

        [Tooltip("Extra FOV degrees added during combat (0 = no FOV change).")]
        [SerializeField, Range(0f, 15f)] private float _combatFovBoost = 4f;

        [Tooltip("Speed at which combat zoom transitions (higher = snappier).")]
        [SerializeField, Min(0.1f)] private float _combatZoomSpeed = 2.5f;

        [Tooltip("Layer mask for enemy detection sweeps. Set to the Enemy layer.")]
        [SerializeField] private LayerMask _enemyMask = ~0;

        [Header("Auto-framing")]
        [Tooltip("When enabled the look-at point interpolates toward the centroid of " +
                 "hero + nearest enemy so both stay in frame during combat.")]
        [SerializeField] private bool _framingEnabled = true;

        [Tooltip("Fraction of the offset from hero to nearest enemy applied to the " +
                 "look-at centroid. 0.5 = halfway between hero and enemy. Kept low " +
                 "so the hero always stays well inside the frame.")]
        [SerializeField, Range(0f, 0.7f)] private float _framingBias = 0.2f;

        [Tooltip("How quickly the look-at recentres on the hero when no enemies are near.")]
        [SerializeField, Min(0.5f)] private float _framingReturnSpeed = 4f;

        [Header("Orbit-behind (third-person)")]
        [Tooltip("Owner 2026-06-02 (\"will the camera pivot behind me when I walk around the " +
                 "castle?\"): when ON, the follow offset rotates with the hero's TRAVEL direction " +
                 "so the camera swings to your back as you round a building — true third-person. " +
                 "OFF = the legacy fixed compass angle (always behind world -Z).")]
        // 2026-06-02: DEFAULT OFF — the first cut (auto-orbit chasing travel heading) fed
        // back into camera-relative movement and made the hero curve/"always turn left"
        // while walking + firing. Reverted to the fixed offset the owner called "much
        // better"; a proper orbit needs a manual/aim-driven yaw, not movement-driven, and
        // must be decoupled from the locomotion input frame. Left here, off, for that pass.
        [SerializeField] private bool _orbitBehind = false;

        // DEF-202/204 (owner: camera was the "worst feature" — world-locked yaw). The fix
        // ships behind _orbitBehind, but Village.unity BAKES _orbitBehind=0, so a BUILD ships
        // world-locked — the exact reason this kept reopening (a C# default flip is overridden
        // by the baked scene value). Force it ON at runtime, no rebake, mirroring the
        // legacy-offset migration in Awake. Set false in code to A/B the legacy camera.
        [SerializeField] private bool _forceCameraFix = true;

        [Tooltip("Degrees/sec the camera yaw chases the hero's travel heading. Lower = lazier, " +
                 "more cinematic swing; higher = snaps behind faster.")]
        [SerializeField, Min(15f)] private float _orbitYawSpeed = 150f;

        [Tooltip("Min planar speed (m/s) before the orbit yaw updates — below this the camera " +
                 "holds its current angle (so standing still / tiny nudges don't spin the view).")]
        [SerializeField, Min(0.01f)] private float _orbitMoveThreshold = 0.4f;

        // DEF-202/204 player-authoritative orbit (CAMERA_INPUT_OVERHAUL.md §2). The yaw is
        // driven ONLY by player pan input (CameraPanInput → AddYaw) plus an optional damped
        // pull toward the hero's FACING (never velocity). This structurally excludes the old
        // velocity-chasing curl/spiral: {yaw←velocity} is absent, only {move←yaw} remains.
        [Tooltip("Min vertical pitch (deg) the player can tilt the orbit camera to (negative = look up).")]
        [SerializeField] private float _panPitchMin = -10f;

        [Tooltip("Max vertical pitch (deg) the player can tilt the orbit camera to.")]
        [SerializeField] private float _panPitchMax = 35f;

        [Tooltip("DEF-202/204: gentle auto-recenter that swings the camera toward the hero's FACING " +
                 "(never velocity) after an idle delay. OFF by default — the camera holds the player's " +
                 "last yaw until they drag again. Loop gain < 1 (damped + idle-gated + suspended during " +
                 "drag) so it converges, never spirals.")]
        [SerializeField] private bool _facingRecenterEnabled = false;

        [Tooltip("Seconds of no-drag before the facing-recenter resumes (only if enabled).")]
        [SerializeField, Min(0f)] private float _facingRecenterDelay = 2f;

        [Tooltip("Degrees/sec the facing-recenter swings the camera toward the hero's facing.")]
        [SerializeField, Min(0f)] private float _facingRecenterSpeed = 90f;

        [Header("Wall collision (DEF-151)")]
        [Tooltip("When ON, the camera spherecasts from the hero pivot toward its desired position " +
                 "each frame and pulls IN to just in front of any wall/world geometry in the way, " +
                 "so it never embeds in a wall mesh and loses the hero. OFF = legacy fixed offset " +
                 "(the DEF-151 clipping bug). Leave ON.")]
        [SerializeField] private bool _collisionEnabled = true;

        [Tooltip("World geometry the camera collides against (walls, buildings, towers, ground). " +
                 "EXCLUDES Enemy/Water/Ignore Raycast/UI so mobs and triggers never shove the view. " +
                 "Default = Default + Building + Tower (the layers village walls/structures live on).")]
        [SerializeField] private LayerMask _collisionMask = ~0;

        // Default collision mask, resolved in Awake from the project's named layers so it
        // stays correct even if layer indices shift. Default(0) | Building(6) | Tower(3).
        // Enemy(8), Water(4), Ignore Raycast(2), UI(5), TransparentFX(1) are deliberately OUT.
        [Tooltip("Radius of the occlusion spherecast - the camera keeps at least this much clearance " +
                 "from a wall so the near clip plane never punches through the surface.")]
        [SerializeField, Min(0.05f)] private float _collisionRadius = 0.35f;

        [Tooltip("Extra gap (metres) kept between the wall hit point and the camera, on top of the " +
                 "spherecast radius. Stops the wall's inside face from filling the screen.")]
        [SerializeField, Min(0f)] private float _collisionSkin = 0.2f;

        [Tooltip("Closest the camera is ever allowed to pull in toward the hero pivot (metres). " +
                 "Prevents the camera snapping onto the hero's head in a tight corner.")]
        [SerializeField, Min(0.5f)] private float _minCollisionDistance = 1.2f;

        [Tooltip("How fast the camera pulls IN when a wall appears (higher = snappier, avoids a " +
                 "clip frame). Pull-in is near-instant; pull-out is eased by _collisionReturnSpeed.")]
        [SerializeField, Min(1f)] private float _collisionApproachSpeed = 40f;

        [Tooltip("How fast the camera eases back OUT to the full offset once the wall is clear " +
                 "(lower = smoother, no jitter as you walk along a wall).")]
        [SerializeField, Min(1f)] private float _collisionReturnSpeed = 8f;

        private bool _collisionMaskInit;
        // Smoothed 0..1 fraction of the desired distance currently allowed (1 = no wall, full offset).
        private float _distanceFrac = 1f;

        // ── Runtime state ──────────────────────────────────────────────────────

        private Camera  _cam;
        private float   _baseFov;
        private Vector3 _posVelocity;
        private Vector3 _leadPoint;
        private Vector3 _leadVelocity;
        private float   _scanTimer;
        private float   _combatBlend;       // 0 = idle, 1 = full combat zoom
        private Vector3 _nearestEnemyPos;
        private bool    _enemyInRange;
        private float   _orbitYaw;          // legacy: smoothed camera yaw (deg) — no longer drives rotation
        private bool    _orbitYawInit;      // seeded from the hero's facing on first frame

        // ── Player-authoritative camera yaw (DEF-202/204, CAMERA_INPUT_OVERHAUL.md §2) ──
        // Written ONLY by AddYaw/AddPitch (pan input) plus an optional damped pull toward
        // hero FACING. NEVER a function of hero velocity/position/MoveIntent. This is the
        // single yaw authority for both the camera seat AND HeroLocomotion's movement basis
        // (read via CameraYaw), so a constant stick yields a straight line — no spiral.
        private float _panYaw;
        private float _panPitch;          // clamped to [_panPitchMin, _panPitchMax]
        private float _timeSinceLastDrag; // drives the idle-gated facing-recenter

        private readonly Collider[] _scanBuffer = new Collider[32];

        // ── Sole-camera guard (same pattern as VillageCamera) ─────────────────
        private float _soleCheckTimer;

        // ── Screen shake (DEF-67) ──────────────────────────────────────────────
        private Vector3 _shakeOffset;
        private Coroutine _shakeRoutine;

        // ── Singleton (DEF-67: audio controllers + VFX need to reach the camera) ──
        /// <summary>
        /// The active SmartMobileCamera in the scene (null between scenes).
        /// Audio / VFX systems use this to call <see cref="Shake"/>.
        /// </summary>
        public static SmartMobileCamera Instance { get; private set; }

        // ── Player-authoritative camera yaw API (DEF-202/204) ──────────────────
        /// <summary>
        /// The camera's current yaw (deg) used as the basis for camera-relative movement
        /// (HeroLocomotion reads this). Returns 0 when orbit-behind is OFF, so movement
        /// stays world-relative and byte-identical to the legacy shipped build (A/B parity).
        /// This is a pure player-input value — NEVER derived from hero velocity — so holding
        /// a constant stick produces a straight line with no spiral.
        /// </summary>
        public float CameraYaw => _orbitBehind ? _panYaw : 0f;

        /// <summary>Player drag / right-stick yaw delta (deg). The ONLY way external input rotates the view.</summary>
        public void AddYaw(float deg)
        {
            _panYaw += deg;
            _timeSinceLastDrag = 0f;
        }

        /// <summary>Optional pitch from vertical drag; clamped to the safe [_panPitchMin, _panPitchMax] band.</summary>
        public void AddPitch(float deg)
        {
            _panPitch = Mathf.Clamp(_panPitch + deg, _panPitchMin, _panPitchMax);
            _timeSinceLastDrag = 0f;
        }

        // ── Lifecycle ──────────────────────────────────────────────────────────

        private void Awake()
        {
            Instance = this;
            _cam    = GetComponent<Camera>();
            _baseFov = _cam.fieldOfView;

            // SINGLE-AUTHORITY GUARD: the scene builder attaches BOTH VillageCamera
            // and SmartMobileCamera to the Main Camera. Two follow rigs writing the
            // same transform every LateUpdate fight each other — the hero drifts
            // off-screen and the camera can swing to the ground/wall. SmartMobileCamera
            // is the intended DEF-53 driver (combat zoom + Shake singleton), so we
            // disable the legacy VillageCamera here and own the transform alone.
            var legacy = GetComponent<VillageCamera>();
            if (legacy != null) legacy.enabled = false;

            // One-time migration (owner 2026-06-02): retire the legacy high top-down
            // seat (0,18,-22) baked into Village.unity. Anything this high is the old
            // board-game framing that flattened the 3D feel — snap it to the close 3D
            // third-person default. Applies on Play with no rebake/scene edit; any
            // genuinely-tuned lower offset is left untouched.
            if (_followOffset.y >= LegacyHighOffsetY)
            {
                Debug.Log($"[SmartMobileCamera] retiring legacy top-down offset {_followOffset} " +
                          $"-> {DefaultFollowOffset} (close 3D third-person).");
                _followOffset = DefaultFollowOffset;
            }

            // DEF-202/204: override the baked _orbitBehind=0 / _facingRecenterEnabled=0 so the
            // BUILD gets the camera fix (camera-relative movement + slide-to-pan + lazy
            // swing-behind). No rebake — same one-time-migration pattern as the offset above.
            if (_forceCameraFix)
            {
                _orbitBehind = true;
                _facingRecenterEnabled = true;
            }

            ResolveCollisionMask();
        }

        // DEF-151: build the camera-occlusion mask from the project's NAMED layers so it
        // tracks the real layer indices (walls/buildings/towers = world geometry the camera
        // must not enter) and deliberately omits Enemy/Water/UI/triggers (which must never
        // push the camera). If the inspector value was left at the "~0" sentinel we replace
        // it; an explicitly-narrowed mask the owner set is honored.
        private void ResolveCollisionMask()
        {
            if (_collisionMaskInit) return;
            _collisionMaskInit = true;

            // Treat the default "everything" value as "unset" and compute a sane world mask.
            if (_collisionMask.value == ~0)
            {
                int mask = 1 << 0; // Default (walls/ground/most structures live here)
                int building = LayerMask.NameToLayer("Building");
                int tower    = LayerMask.NameToLayer("Tower");
                if (building >= 0) mask |= 1 << building;
                if (tower    >= 0) mask |= 1 << tower;
                _collisionMask = mask;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            // Fallback: if the scene builder didn't wire a target (or the hero
            // spawned after this camera), find the hero by canonical tag so the
            // camera is never left staring at the origin/ground (CLAUDE.md §7).
            if (_target == null) TryFindHero();
            if (_target == null) return;
            transform.position = _target.position + _followOffset;
            _leadPoint         = _target.position + Vector3.up * _lookAtHeight;
            AimAt(_leadPoint);
            EnforceSoleCamera();
        }

        private void TryFindHero()
        {
            var heroGo = GameObject.FindWithTag("Player");
            // "HeroTarget" may be undefined (FindWithTag throws on an undefined tag).
            if (heroGo == null) heroGo = SafeFindWithTag("HeroTarget");
            if (heroGo != null) _target = heroGo.transform;
        }

        /// <summary>Undefined-tag-safe FindWithTag (Unity throws on an undefined tag).</summary>
        private static GameObject SafeFindWithTag(string tag)
        {
            try { return GameObject.FindWithTag(tag); }
            catch (UnityEngine.UnityException) { return null; }
        }

        private void LateUpdate()
        {
            // Hero may spawn a frame or two after this camera — keep looking until
            // we have a target so we never sit framing the origin/ground forever.
            if (_target == null)
            {
                TryFindHero();
                if (_target == null) return;
                transform.position = _target.position + _followOffset;
                _leadPoint         = _target.position + Vector3.up * _lookAtHeight;
                AimAt(_leadPoint);
            }

            float dt = Time.unscaledDeltaTime;

            // ── 1. Enemy scan (throttled) ──────────────────────────────────────
            _scanTimer -= dt;
            if (_scanTimer <= 0f)
            {
                _scanTimer = _enemyScanInterval;
                ScanForEnemies();
            }

            // ── 2. Combat blend ───────────────────────────────────────────────
            float combatTarget = _enemyInRange ? 1f : 0f;
            _combatBlend = Mathf.MoveTowards(_combatBlend, combatTarget, _combatZoomSpeed * dt);

            // ── 3. Follow offset with combat zoom (+ orbit-behind) ────────────
            Vector3 zoomOffset = _followOffset + new Vector3(0f, 0f, -_combatZoomOut * _combatBlend);

            // Orbit-behind (DEF-202/204, CAMERA_INPUT_OVERHAUL.md §2): rotate the offset by
            // the PLAYER-authoritative _panYaw (set via AddYaw from pan input) — NOT the hero's
            // velocity. The old velocity-chasing yaw fed back into camera-relative movement and
            // produced the "always turn left" curl; that {yaw←velocity} edge is now structurally
            // absent. _panYaw is a pure accumulator of player input plus an optional damped pull
            // toward the hero's FACING, so holding a constant stick yields a straight line.
            if (_orbitBehind)
            {
                if (!_orbitYawInit) { _panYaw = _target.eulerAngles.y; _orbitYawInit = true; }

                // Gentle facing-recenter (OFF by default). Targets the hero's FACING (never
                // velocity), suspended while the player is actively dragging (AddYaw resets
                // _timeSinceLastDrag). Loop gain < 1 (small step, idle-gated) → converges.
                _timeSinceLastDrag += dt;
                if (_facingRecenterEnabled && _timeSinceLastDrag > _facingRecenterDelay)
                    _panYaw = Mathf.MoveTowardsAngle(_panYaw, _target.eulerAngles.y, _facingRecenterSpeed * dt);

                zoomOffset = Quaternion.Euler(_panPitch, _panYaw, 0f) * zoomOffset;
            }

            Vector3 desired = _target.position + zoomOffset;

            // ── 3b. Wall collision / occlusion (DEF-151) ──────────────────────
            // ROOT CAUSE of the bug this fixes: the camera was placed at a fixed
            // offset behind the hero with NO awareness of geometry in between, so a
            // wall sitting between the pivot and the desired seat let the camera slide
            // straight through the mesh — the screen filled with the wall's inside face
            // and the hero shrank to a speck. Fix: spherecast from the hero pivot toward
            // the desired position; if it hits world geometry, pull the seat IN to just
            // in front of the hit so the camera body (+ near clip) never enters the wall.
            desired = ApplyCollision(desired, dt);

            transform.position = Vector3.SmoothDamp(transform.position, desired, ref _posVelocity, _smoothTime, float.MaxValue, dt);
            // DEF-67: apply shake offset on top of the smoothed position.
            if (_shakeOffset.sqrMagnitude > 0.0001f)
                transform.position += _shakeOffset;

            // ── 4. FOV ────────────────────────────────────────────────────────
            _cam.fieldOfView = Mathf.Lerp(_baseFov, _baseFov + _combatFovBoost, _combatBlend);

            // ── 5. Movement lead ──────────────────────────────────────────────
            // Hero velocity is used ONLY for the look-at lead bias here — it MUST NOT feed
            // the camera yaw (_panYaw), or the old curl/spiral returns. This is the single
            // GetHeroVelocity() call, deliberately below the yaw block (CAMERA_INPUT_OVERHAUL.md §2.3).
            Vector3 heroVelFlat = GetHeroVelocity();
            heroVelFlat.y = 0f;
            Vector3 heroBase = _target.position + Vector3.up * _lookAtHeight;
            Vector3 leadTarget = heroBase;
            if (heroVelFlat.sqrMagnitude > 0.01f)
                leadTarget += heroVelFlat.normalized * _leadDistance;

            // ── 6. Auto-framing ───────────────────────────────────────────────
            if (_framingEnabled && _enemyInRange && _combatBlend > 0.1f)
            {
                Vector3 midpoint = Vector3.Lerp(heroBase, _nearestEnemyPos, _framingBias);
                leadTarget = Vector3.Lerp(leadTarget, midpoint, _combatBlend);
            }

            _leadPoint = Vector3.SmoothDamp(_leadPoint, leadTarget, ref _leadVelocity,
                _leadSmoothTime, float.MaxValue, dt);

            AimAt(_leadPoint);

            // ── Sole-camera check ─────────────────────────────────────────────
            _soleCheckTimer -= dt;
            if (_soleCheckTimer <= 0f)
            {
                _soleCheckTimer = 1f;
                EnforceSoleCamera();
            }
        }

        // ── Public API ─────────────────────────────────────────────────────────

        /// <summary>Wires the hero target (called by VillageController).</summary>
        public void SetTarget(Transform hero) => _target = hero;

        /// <summary>Toggles the auto-framing behaviour at runtime.</summary>
        public bool FramingEnabled
        {
            get => _framingEnabled;
            set => _framingEnabled = value;
        }

        /// <summary>
        /// Plays a screen-shake impulse. Safe to call from any MonoBehaviour.
        /// Cancels any in-progress shake and starts a fresh one.
        /// <para>DEF-67: called by TowerAudioController on heavy creak/debris events
        /// and by WaveMusicController on boss-wave transitions.</para>
        /// </summary>
        /// <param name="intensity">Peak displacement in world units (0.1 = subtle, 0.5 = heavy).</param>
        /// <param name="duration">Total duration of the shake in seconds.</param>
        public void Shake(float intensity, float duration)
        {
            if (!gameObject.activeInHierarchy) return;
            if (_shakeRoutine != null) StopCoroutine(_shakeRoutine);
            _shakeRoutine = StartCoroutine(ShakeRoutine(intensity, duration));
        }

        private System.Collections.IEnumerator ShakeRoutine(float intensity, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                // Envelope: full intensity at start, fades to zero by duration.
                float envelope = 1f - (elapsed / duration);
                _shakeOffset   = Random.insideUnitSphere * (intensity * envelope);
                _shakeOffset.z = 0f; // keep depth axis stable; shake only X/Y
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            _shakeOffset  = Vector3.zero;
            _shakeRoutine = null;
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private void ScanForEnemies()
        {
            if (_target == null) { _enemyInRange = false; return; }

            int count = Physics.OverlapSphereNonAlloc(
                _target.position, _combatScanRadius, _scanBuffer, _enemyMask, QueryTriggerInteraction.Collide);

            float closestSqr = float.MaxValue;
            Vector3 closestPos = Vector3.zero;
            bool found = false;

            for (int i = 0; i < count; i++)
            {
                var col = _scanBuffer[i];
                if (col == null) continue;
                // Only count live hostile IDamageable targets (avoids counting the hero).
                var dmg = col.GetComponentInParent<DeNelle.Core.Combat.IDamageable>();
                if (dmg == null || !dmg.IsAlive || dmg.Faction != DeNelle.Core.Combat.CombatFaction.Hostile) continue;

                float sqr = (col.transform.position - _target.position).sqrMagnitude;
                if (sqr < closestSqr)
                {
                    closestSqr = sqr;
                    closestPos = dmg.WorldPosition + Vector3.up;
                    found = true;
                }
            }

            _enemyInRange    = found;
            _nearestEnemyPos = found ? closestPos : _target.position + Vector3.up * _lookAtHeight;
        }

        // DEF-151: camera-collision pass. Casts a small sphere from the hero pivot out to
        // the desired camera seat; if world geometry (walls/buildings/towers) is in the way
        // it returns a seat just IN FRONT of the obstruction instead of inside it. When the
        // path is clear it eases back out to the full offset, so the validated framing is
        // untouched except when something is actually between the hero and the camera.
        private Vector3 ApplyCollision(Vector3 desired, float dt)
        {
            if (!_collisionEnabled || _target == null)
            {
                _distanceFrac = 1f;
                return desired;
            }

            // Pivot = where the camera looks (hero chest/head), well above the feet so the
            // cast doesn't immediately bury itself in the ground collider at the hero's base.
            Vector3 pivot = _target.position + Vector3.up * _lookAtHeight;
            Vector3 toCam = desired - pivot;
            float fullDist = toCam.magnitude;
            if (fullDist <= 0.0001f)
            {
                _distanceFrac = 1f;
                return desired;
            }

            Vector3 dir = toCam / fullDist;

            // Target fraction of the full distance we're allowed this frame (1 = no wall).
            float targetFrac = 1f;

            // SphereCast so the camera body — not just an infinitely-thin ray — clears the
            // wall; gives a margin before the surface ever reaches the near clip plane.
            // QueryTriggerInteraction.Ignore so combat-scan / pickup triggers never block.
            if (Physics.SphereCast(pivot, _collisionRadius, dir, out RaycastHit hit,
                    fullDist, _collisionMask, QueryTriggerInteraction.Ignore))
            {
                // Skip the hero's own colliders (hero is on Default, same layer as walls).
                if (!IsTargetCollider(hit.collider))
                {
                    float allowed = hit.distance - _collisionSkin;
                    if (allowed < _minCollisionDistance) allowed = _minCollisionDistance;
                    if (allowed > fullDist)               allowed = fullDist;
                    targetFrac = allowed / fullDist;
                }
            }

            // Pull IN fast (avoid a clip frame), ease OUT slowly (no jitter along a wall).
            float speed = targetFrac < _distanceFrac ? _collisionApproachSpeed : _collisionReturnSpeed;
            _distanceFrac = Mathf.MoveTowards(_distanceFrac, targetFrac, speed * dt);

            return pivot + dir * (fullDist * _distanceFrac);
        }

        // True if the hit collider belongs to the hero we're following (its own body must
        // never count as an occluder, or the camera would jam onto the hero's back).
        private bool IsTargetCollider(Collider col)
        {
            if (col == null || _target == null) return false;
            return col.transform == _target || col.transform.IsChildOf(_target);
        }

        private Vector3 GetHeroVelocity()
        {
            // Try HeroLocomotion first (has velocity), fall back to transform delta.
            var loco = _target.GetComponent<HeroLocomotion>();
            return loco != null ? loco.Velocity : Vector3.zero;
        }

        private void AimAt(Vector3 point)
        {
            Vector3 dir = point - transform.position;
            if (dir.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(dir);
        }

        private void EnforceSoleCamera()
        {
            if (_cam == null) return;
            _cam.depth = 100f;
            if (!_cam.enabled) _cam.enabled = true;

            foreach (var c in Camera.allCameras)
            {
                if (c == null || c == _cam) continue;
                if (c.targetTexture != null) continue;
                if (!c.enabled) continue;
                c.enabled = false;
                Debug.Log($"[SmartMobileCamera] disabled rogue screen camera '{c.name}'.");
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (_target == null) return;
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.35f);
            Gizmos.DrawWireSphere(_target.position, _combatScanRadius);

            if (Application.isPlaying)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(_target.position + Vector3.up * _lookAtHeight, _leadPoint);
                Gizmos.DrawWireSphere(_leadPoint, 0.2f);
            }
        }
#endif
    }
}
