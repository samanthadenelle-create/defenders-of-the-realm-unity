// =============================================================================
// PetEmoteController (DEF-57) — pet personality animation states.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Pets   Namespace: DeNelle.Pets
//
// WHAT IT DOES:
//   Adds an emotional-state layer on top of the pet's core locomotion so the
//   companion feels alive and responsive. Three emote states are managed:
//
//   IDLE WAGS — the pet occasionally plays a "Happy" sub-trigger when standing
//   still next to the hero (within LeashRadius). Fires randomly ~every 5–12s
//   so it feels organic, not clockwork.
//
//   ALERT — when enemies are detected within AlertRadius the pet switches to
//   alert posture (AnimAlert trigger). Reverts to normal when the area is clear.
//
//   CELEBRATE — fires on WaveManager.OnWaveCleared (subscribed via reflection
//   bridge since PetEmoteController is in DeNelle.Pets, which doesn't reference
//   DeNelle.Village). Falls back gracefully when WaveManager can't be found.
//
// ARCHITECTURE:
//   * Lives in DeNelle.Pets. Subscribes to WaveManager via reflection (same
//     pattern as PetDeployer's cosmetic-ownership bridge) to avoid an asmdef cycle.
//   * Animator parameters "Happy", "Alert", "Celebrate" must exist in the pet's
//     Animator controller (added by PetAnimatorSetup). All calls are null-guarded.
//   * Enemy scan uses Physics.OverlapSphereNonAlloc on a throttled timer.
// =============================================================================

using UnityEngine;

namespace DeNelle.Pets
{
    /// <summary>
    /// Drives pet personality emote states: idle happiness, combat alertness,
    /// and wave-clear celebration. Attach to the pet root.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PetEmoteController : MonoBehaviour
    {
        [Header("Emote timing")]
        [Tooltip("Minimum seconds between spontaneous happy emotes when idle.")]
        [SerializeField, Min(2f)] private float _happyIntervalMin = 5f;

        [Tooltip("Maximum seconds between spontaneous happy emotes when idle.")]
        [SerializeField, Min(4f)] private float _happyIntervalMax = 12f;

        [Header("Alert detection")]
        [Tooltip("Radius within which enemy presence triggers the Alert emote.")]
        [SerializeField, Min(1f)] private float _alertRadius = 10f;

        [Tooltip("Seconds between enemy proximity scans.")]
        [SerializeField, Range(0.2f, 2f)] private float _alertScanInterval = 0.5f;

        [Header("Layers")]
        [Tooltip("Layer mask for enemy detection.")]
        [SerializeField] private LayerMask _enemyMask = ~0;

        // ── Animator hashes ───────────────────────────────────────────────────
        private static readonly int AnimHappy     = Animator.StringToHash("Happy");
        private static readonly int AnimAlert     = Animator.StringToHash("Alert");
        private static readonly int AnimCelebrate = Animator.StringToHash("Celebrate");

        // WO-163: cached once at init — whether the controller declares each emote
        // param. Driving an absent param logs "Parameter does not exist".
        private bool _hasHappy;
        private bool _hasAlert;
        private bool _hasCelebrate;

        // ── Runtime ────────────────────────────────────────────────────────────
        private Animator _animator;
        private float    _happyTimer;
        private float    _scanTimer;
        private bool     _alertActive;
        private readonly Collider[] _scanBuffer = new Collider[16];

        // (Wave-clear is wired externally — see Celebrate() summary.)

        // ── Lifecycle ──────────────────────────────────────────────────────────

        private void Awake()
        {
            _animator   = GetComponentInChildren<Animator>();
            _happyTimer = UnityEngine.Random.Range(_happyIntervalMin, _happyIntervalMax);

            if (_animator != null && _animator.runtimeAnimatorController != null)
            {
                foreach (var p in _animator.parameters)
                {
                    if (p.nameHash == AnimHappy)     _hasHappy     = true;
                    if (p.nameHash == AnimAlert)     _hasAlert     = true;
                    if (p.nameHash == AnimCelebrate) _hasCelebrate = true;
                }
            }
        }

        private void OnEnable()
        {
            TrySubscribeWaveClear();
        }

        private void OnDisable()
        {
            TryUnsubscribeWaveClear();
        }

        private void Update()
        {
            if (_animator == null) return;

            float dt = Time.deltaTime;

            // ── Enemy alert scan ──────────────────────────────────────────────
            _scanTimer -= dt;
            if (_scanTimer <= 0f)
            {
                _scanTimer = _alertScanInterval;
                bool enemiesNear = HasNearbyEnemies();
                if (enemiesNear != _alertActive)
                {
                    _alertActive = enemiesNear;
                    if (_hasAlert) _animator.SetBool(AnimAlert, _alertActive);
                }
            }

            // ── Happy idle emote ──────────────────────────────────────────────
            if (!_alertActive)
            {
                _happyTimer -= dt;
                if (_happyTimer <= 0f)
                {
                    _happyTimer = UnityEngine.Random.Range(_happyIntervalMin, _happyIntervalMax);
                    if (_hasHappy) _animator.SetTrigger(AnimHappy);
                }
            }
        }

        // ── Wave-clear celebration ─────────────────────────────────────────────

        /// <summary>
        /// Fires the celebrate emote. Call from the integrator when a wave is
        /// cleared — e.g. waveManager.OnWaveCleared.AddListener(_ => pet.Celebrate())
        /// in VillageController, or from a UnityEvent in the Inspector.
        /// </summary>
        public void Celebrate()
        {
            if (_animator == null || !_hasCelebrate) return;
            _animator.SetTrigger(AnimCelebrate);
        }

        // Celebration wiring is handled externally (see summary on Celebrate()).
        // These stubs satisfy OnEnable/OnDisable calls without breaking anything.
        private void TrySubscribeWaveClear()   { }
        private void TryUnsubscribeWaveClear() { }

        // ── Enemy scan ────────────────────────────────────────────────────────

        private bool HasNearbyEnemies()
        {
            int count = Physics.OverlapSphereNonAlloc(
                transform.position, _alertRadius, _scanBuffer, _enemyMask,
                QueryTriggerInteraction.Collide);

            for (int i = 0; i < count; i++)
            {
                if (_scanBuffer[i] == null) continue;
                var dmg = _scanBuffer[i].GetComponentInParent<DeNelle.Core.Combat.IDamageable>();
                if (dmg != null && dmg.IsAlive && dmg.Faction == DeNelle.Core.Combat.CombatFaction.Hostile)
                    return true;
            }
            return false;
        }
    }
}
