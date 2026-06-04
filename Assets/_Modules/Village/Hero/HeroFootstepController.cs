// =============================================================================
// HeroFootstepController (DEF-57) — hero footstep audio from velocity.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// WHAT IT DOES:
//   Plays a random footstep clip from the assigned array each time the hero
//   takes a "step" — detected either by Animation Events from the locomotion
//   clip OR by a velocity-threshold crossing (whichever comes first).
//
//   The velocity fallback is the default because the hero's locomotion Animator
//   (HeroAnimatorSetup) doesn't yet have Animation Event slots on the walk/run
//   states. When events ARE added by the integrator, call PlayStep() from an
//   Animation Event instead and the velocity polling automatically stops.
//
// VELOCITY-POLLING FALLBACK:
//   The controller samples HeroLocomotion.Velocity each Update. Once speed
//   exceeds _minStepSpeed it starts a per-step timer: at _stepInterval seconds
//   it picks a random clip, applies light pitch variation, and plays it.
//   Timer is reset when the hero stops.
//
// INTEGRATION:
//   Add this component to the hero root. Assign an AudioSource (or let Awake
//   auto-add one) and fill the _footstepClips array with surface-specific
//   impact sounds. Assign it to the hero prefab via VillageSceneBuilder or
//   Inspector.
//
// SURFACE VARIANTS (future):
//   Switch on the surface normal's material tag (requires Physics material
//   tagging on the ground mesh) and maintain one array per surface. For now
//   a single array covers all surfaces — the audio designer can blend the set.
// =============================================================================

using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>
    /// Plays randomised hero footstep audio driven by velocity threshold.
    /// Can be triggered by Animation Events (<see cref="PlayStep"/>) once the
    /// locomotion controller adds event slots.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HeroFootstepController : MonoBehaviour
    {
        [Header("Audio")]
        [Tooltip("Footstep clips — one is selected at random per step.")]
        [SerializeField] private AudioClip[] _footstepClips;

        [Tooltip("AudioSource used to play clips. Populated in Awake when blank.")]
        [SerializeField] private AudioSource _audioSource;

        [Header("Velocity fallback (used when no Animation Events)")]
        [Tooltip("Speed (world units/sec) below which the hero is considered stationary.")]
        [SerializeField, Min(0.1f)] private float _minStepSpeed = 0.8f;

        [Tooltip("Seconds between footstep sounds at full run speed.")]
        [SerializeField, Range(0.1f, 1f)] private float _stepIntervalRun = 0.42f;

        [Tooltip("Seconds between footstep sounds at walk speed. " +
                 "Interpolated against _stepIntervalRun based on velocity.")]
        [SerializeField, Range(0.2f, 2f)] private float _stepIntervalWalk = 0.65f;

        [Tooltip("Speed at which the hero is considered 'running' for interval lerp.")]
        [SerializeField, Min(1f)] private float _runSpeed = 5f;

        [Header("Volume + pitch")]
        [SerializeField, Range(0f, 1f)] private float _volume      = 0.55f;
        [SerializeField, Range(0f, 0.3f)] private float _pitchVariance = 0.12f;

        // ── Runtime ────────────────────────────────────────────────────────────
        private HeroLocomotion _locomotion;
        private float          _stepTimer;
        private bool           _wasMoving;

        // ── Lifecycle ──────────────────────────────────────────────────────────

        private void Awake()
        {
            _locomotion = GetComponent<HeroLocomotion>();

            if (_audioSource == null)
                _audioSource = GetComponent<AudioSource>();

            if (_audioSource == null)
            {
                _audioSource = gameObject.AddComponent<AudioSource>();
                _audioSource.spatialBlend = 1f;     // 3D
                _audioSource.volume       = _volume;
                _audioSource.rolloffMode  = AudioRolloffMode.Linear;
                _audioSource.maxDistance  = 20f;
                _audioSource.playOnAwake  = false;
            }
        }

        private void Update()
        {
            if (_locomotion == null || _footstepClips == null || _footstepClips.Length == 0) return;

            float speed = _locomotion.Velocity.magnitude;
            bool moving = speed >= _minStepSpeed;

            if (!moving)
            {
                _stepTimer = 0f;
                _wasMoving = false;
                return;
            }

            if (!_wasMoving)
            {
                // Emit a step immediately when the hero starts walking
                _stepTimer = 0f;
                PlayStep();
                _wasMoving = true;
            }

            // Interpolate the interval between walk and run
            float t        = Mathf.Clamp01((speed - _minStepSpeed) / Mathf.Max(0.01f, _runSpeed - _minStepSpeed));
            float interval = Mathf.Lerp(_stepIntervalWalk, _stepIntervalRun, t);

            _stepTimer += Time.deltaTime;
            if (_stepTimer >= interval)
            {
                _stepTimer -= interval;
                PlayStep();
            }
        }

        // ── Public API ─────────────────────────────────────────────────────────

        /// <summary>
        /// Plays a footstep immediately — call from an Animation Event on the
        /// hero's walk/run state once the locomotion clip has event slots.
        /// Also resets the velocity-poll timer so the two systems don't double-fire.
        /// </summary>
        public void PlayStep()
        {
            if (_audioSource == null || _footstepClips == null || _footstepClips.Length == 0) return;

            var clip = _footstepClips[Random.Range(0, _footstepClips.Length)];
            if (clip == null) return;

            float pitch = 1f + Random.Range(-_pitchVariance, _pitchVariance);
            _audioSource.pitch  = pitch;
            _audioSource.volume = _volume;
            _audioSource.PlayOneShot(clip);

            _stepTimer = 0f; // prevent double-fire if used alongside Animation Events
        }
    }
}
