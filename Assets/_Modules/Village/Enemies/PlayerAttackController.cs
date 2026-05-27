// =============================================================================
// PlayerAttackController — DEF-47: Player attack with perfect-hit timing window.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// Gives player attacks timing depth — a "Perfect Hit" window on each swing that
// deals bonus damage and triggers dramatic feedback. Rewards skilled play without
// requiring complex input chains.
//
// ADAPTION NOTES (from spec against this codebase):
//   • IDamageable.TakeDamage(float, DamageElement) — correct signature here.
//   • DamageNumberSpawner.Spawn() / SpawnLabel() — replaces FloatingTextSpawner
//     which doesn't exist; DamageNumberSpawner is the project's equivalent.
//   • CombatFeedbackManager.Hit(worldPos, damage) — static helper, no Instance call.
//   • No HitIntensity enum in this project; bonus damage is the sole feedback signal.
// =============================================================================

using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using DeNelle.Core.Combat;

namespace DeNelle.Village
{
    /// <summary>
    /// Handles player melee attacks with a timing-based perfect-hit window.
    /// Attach to the Hero root alongside <see cref="HeroLocomotion"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerAttackController : MonoBehaviour
    {
        [Header("Base Attack")]
        [Tooltip("Flat damage per hit before any talent multipliers.")]
        [SerializeField] private float _baseDamage = 30f;

        [Tooltip("Radius of the OverlapSphere damage check around the hero.")]
        [SerializeField, Min(0.1f)] private float _attackRange = 2.5f;

        [Tooltip("Minimum seconds between attacks.")]
        [SerializeField, Min(0.1f)] private float _attackCooldown = 0.6f;

        [Tooltip("Layer mask covering enemy colliders.")]
        [SerializeField] private LayerMask _enemyLayer;

        [Header("Perfect Hit Window")]
        [Tooltip("Seconds after swing input when the perfect-hit window opens.")]
        [SerializeField, Min(0f)] private float _perfectHitWindowStart = 0.08f;

        [Tooltip("Seconds after swing input when the perfect-hit window closes.")]
        [SerializeField, Min(0f)] private float _perfectHitWindowEnd = 0.18f;

        [Tooltip("Damage multiplier applied when the player hits in the perfect window.")]
        [SerializeField, Min(1f)] private float _perfectHitMultiplier = 1.75f;

        [Tooltip("Sound played on a perfect hit (optional).")]
        [SerializeField] private AudioClip _perfectHitSound;

        [Header("Weapon Whoosh")]
        [Tooltip("Pool of whoosh sounds — one is chosen at random per swing.")]
        [SerializeField] private AudioClip[] _whooshSounds;

        [Tooltip("Pitch variation range for the whoosh sample.")]
        [SerializeField] private Vector2 _whooshPitchRange = new Vector2(0.9f, 1.1f);

        // ── Runtime ───────────────────────────────────────────────────────────

        private Animator     _animator;
        private AudioSource  _audioSource;
        private float        _nextAttackTime;
        private float        _swingStartTime;
        private bool         _isInSwing;

        private static readonly int AnimAttack = Animator.StringToHash("Attack");

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            _animator    = GetComponentInChildren<Animator>();
            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null)
                _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.spatialBlend = 0f; // 2D — punchy response without attenuation
        }

        private void Update()
        {
            bool attackPressed = false;

            // New Input System path.
            var kb = Keyboard.current;
            if (kb != null && kb.spaceKey.wasPressedThisFrame) attackPressed = true;

            var gp = Gamepad.current;
            if (gp != null && gp.buttonSouth.wasPressedThisFrame) attackPressed = true;

            // Legacy Input fallback.
            if (!attackPressed && UnityEngine.Input.GetMouseButtonDown(0)) attackPressed = true;

            if (attackPressed && !_isInSwing && Time.time >= _nextAttackTime)
                StartAttack();
        }

        // ── Attack flow ───────────────────────────────────────────────────────

        private void StartAttack()
        {
            _nextAttackTime = Time.time + _attackCooldown;
            _swingStartTime = Time.time;
            _isInSwing      = true;

            if (_animator != null) _animator.SetTrigger(AnimAttack);
            PlayWhoosh();

            StartCoroutine(ResolveAttack());
        }

        private IEnumerator ResolveAttack()
        {
            // Wait until the hit frame (start of perfect window).
            yield return new WaitForSeconds(_perfectHitWindowStart);

            float elapsed   = Time.time - _swingStartTime;
            bool isPerfect  = elapsed >= _perfectHitWindowStart
                           && elapsed <= _perfectHitWindowEnd;

            Collider[] hits = Physics.OverlapSphere(transform.position, _attackRange, _enemyLayer);

            foreach (var col in hits)
            {
                if (col == null) continue;
                var damageable = col.GetComponentInParent<IDamageable>();
                if (damageable == null || !damageable.IsAlive) continue;
                if (damageable.Faction != CombatFaction.Hostile)      continue;

                float damage = _baseDamage;
                if (isPerfect) damage *= _perfectHitMultiplier;

                Vector3 hitPos = col.transform.position + Vector3.up;

                damageable.TakeDamage(damage, DamageElement.None);
                DamageNumberSpawner.Spawn(damage, hitPos);
                CombatFeedbackManager.Hit(hitPos, damage);

                if (isPerfect)
                    TriggerPerfectHitFeedback(hitPos);
            }

            _isInSwing = false;
        }

        private void TriggerPerfectHitFeedback(Vector3 hitPos)
        {
            if (_perfectHitSound != null)
                _audioSource.PlayOneShot(_perfectHitSound, 1.0f);

            // "PERFECT!" label above the hit — uses the project's DamageNumberSpawner.
            DamageNumberSpawner.SpawnLabel("PERFECT!", hitPos + Vector3.up * 1.2f,
                new Color(1f, 0.93f, 0.2f), 1.5f);
        }

        // ── Audio ─────────────────────────────────────────────────────────────

        private void PlayWhoosh()
        {
            if (_whooshSounds == null || _whooshSounds.Length == 0) return;
            var clip = _whooshSounds[Random.Range(0, _whooshSounds.Length)];
            if (clip == null) return;

            _audioSource.pitch = Random.Range(_whooshPitchRange.x, _whooshPitchRange.y);
            _audioSource.PlayOneShot(clip, 0.7f);
            StartCoroutine(ResetPitchAfter(clip.length));
        }

        private IEnumerator ResetPitchAfter(float delay)
        {
            yield return new WaitForSeconds(delay);
            _audioSource.pitch = 1f;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.4f, 0.1f, 0.25f);
            Gizmos.DrawWireSphere(transform.position, _attackRange);
        }
#endif
    }
}
