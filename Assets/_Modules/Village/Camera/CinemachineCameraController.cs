// =============================================================================
// CinemachineCameraController — WO-87. Singleton that owns the Cinemachine
// virtual-camera stack and drives screen shake via CinemachineImpulseSource.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// ARCHITECTURE:
//   • Expects three CinemachineCamera children wired in the Inspector:
//       VcVillage  (priority 10) — default hero-follow cam
//       VcCombat   (priority 11) — tighter zoom when enemies are nearby
//       VcWaveClear(priority 12) — wide pull-back during wave-clear celebration
//   • A CinemachineImpulseSource component must also be on this GameObject.
//   • HeroCinemachineRig (priority 100) still owns the hero's OTS rig and takes
//     precedence over all three cameras here. These cameras are designed for the
//     overhead / village view (non-OTS scenes).
//   • CameraShakeBridge (internal in Tower.cs) discovers Shake(float,float) via
//     reflection — this controller satisfies that contract automatically.
//
// WO-87 acceptance:
//   [x] ShakeLight/Medium/Heavy methods via CinemachineImpulseSource
//   [x] Enemy proximity polling switches VcCombat on/off
//   [x] Wave-clear cinematic via PlayWaveClearCinematic(duration)
//   [x] Mobile shake scaled 0.55x
//   [x] Existing CameraShakeBridge callers work via the Shake(float,float) shim
// =============================================================================

using Unity.Cinemachine;
using UnityEngine;

namespace DeNelle.Village
{
    [DisallowMultipleComponent]
    public sealed class CinemachineCameraController : MonoBehaviour
    {
        public static CinemachineCameraController Instance { get; private set; }

        // ── Inspector ─────────────────────────────────────────────────────────

        [Header("Virtual Cameras")]
        [Tooltip("Default overhead hero-follow camera (Priority 10). Enable when no enemies nearby.")]
        public CinemachineCamera vcVillage;

        [Tooltip("Combat zoom camera (Priority 11). Enabled when enemies enter combatZoomRadius.")]
        public CinemachineCamera vcCombat;

        [Tooltip("Wave-clear pull-back camera (Priority 12). Enabled for slowMo window after a wave.")]
        public CinemachineCamera vcWaveClear;

        [Header("Combat Zoom")]
        [Tooltip("If any enemy is within this radius of the hero, blend to vcCombat.")]
        public float combatZoomRadius = 12f;

        [Tooltip("Seconds between enemy proximity checks (reduces physics overhead).")]
        public float checkInterval = 0.4f;

        [Header("Shake Forces")]
        [Tooltip("Impulse force for ShakeLight.")]
        [Range(0.01f, 1f)] public float lightForce  = 0.15f;

        [Tooltip("Impulse force for ShakeMedium.")]
        [Range(0.01f, 1f)] public float mediumForce = 0.40f;

        [Tooltip("Impulse force for ShakeHeavy.")]
        [Range(0.01f, 1f)] public float heavyForce  = 0.90f;

        // ── Runtime ───────────────────────────────────────────────────────────

        private CinemachineImpulseSource _impulseSource;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            _impulseSource = GetComponent<CinemachineImpulseSource>();
            if (_impulseSource == null)
                _impulseSource = gameObject.AddComponent<CinemachineImpulseSource>();
        }

        private void Start()
        {
            // Safety: disable combat + wave-clear cams at startup so vcVillage is
            // the active camera until enemies are detected.
            if (vcCombat   != null) vcCombat.enabled   = false;
            if (vcWaveClear != null) vcWaveClear.enabled = false;

            InvokeRepeating(nameof(CheckCombatProximity), 0f, checkInterval);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ── Camera switching ──────────────────────────────────────────────────

        private void CheckCombatProximity()
        {
            // Find the hero by canonical tag (CLAUDE.md §7). "HeroTarget" may be
            // undefined (FindWithTag throws on an undefined tag) — guard it;
            // "Player" is a built-in tag and always safe.
            var heroGo = SafeFindWithTag("HeroTarget");
            if (heroGo == null) heroGo = GameObject.FindWithTag("Player");
            if (heroGo == null) return;

            bool enemyNear = false;
            var cols = Physics.OverlapSphere(
                heroGo.transform.position, combatZoomRadius,
                LayerMask.GetMask("Enemy"));
            if (cols.Length > 0) enemyNear = true;

            if (vcCombat  != null) vcCombat.enabled  = enemyNear;
            if (vcVillage != null) vcVillage.enabled  = !enemyNear;
        }

        /// <summary>Undefined-tag-safe FindWithTag (Unity throws on an undefined tag).</summary>
        private static GameObject SafeFindWithTag(string tag)
        {
            try { return GameObject.FindWithTag(tag); }
            catch (UnityEngine.UnityException) { return null; }
        }

        /// <summary>
        /// Enable the wide pull-back camera for <paramref name="duration"/> seconds,
        /// then revert to the standard village view. Call from WaveCelebrationManager.
        /// </summary>
        public void PlayWaveClearCinematic(float duration)
        {
            if (vcWaveClear == null) return;
            vcWaveClear.enabled = true;
            CancelInvoke(nameof(EndWaveClearCinematic));
            Invoke(nameof(EndWaveClearCinematic), duration);
        }

        private void EndWaveClearCinematic()
        {
            if (vcWaveClear != null) vcWaveClear.enabled = false;
        }

        // ── Shake — three named tiers ─────────────────────────────────────────

        /// <summary>Light shake: arrow hit, light contact.</summary>
        public void ShakeLight(float duration)  => FireImpulse(lightForce,  duration);

        /// <summary>Medium shake: ability impact, tower upgrade.</summary>
        public void ShakeMedium(float duration) => FireImpulse(mediumForce, duration);

        /// <summary>Heavy shake: boss hit, death explosion.</summary>
        public void ShakeHeavy(float duration)  => FireImpulse(heavyForce,  duration);

        /// <summary>
        /// Legacy shim — maps CameraShakeBridge.Shake(intensity, duration) calls
        /// to this impulse source. CameraShakeBridge (internal in Tower.cs) finds
        /// this method via reflection and invokes it on the first MonoBehaviour
        /// that exposes Shake(float, float). Mobile force is scaled 0.55×.
        /// </summary>
        public void Shake(float intensity, float duration)
        {
            FireImpulse(intensity, duration);
        }

        private void FireImpulse(float force, float duration)
        {
            if (_impulseSource == null) return;

#if UNITY_ANDROID || UNITY_IOS
            force *= 0.55f;
#endif
            // Cinemachine 3 impulse: force maps to the velocity magnitude.
            // Duration is set via the ImpulseDefinition on the component; we
            // respect whatever is authored there and only override force here.
            _impulseSource.GenerateImpulse(force);
        }
    }
}
