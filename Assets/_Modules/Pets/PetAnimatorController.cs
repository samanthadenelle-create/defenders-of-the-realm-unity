// =============================================================================
// PetAnimatorController — DEF-57: Animation controller for companion pets.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Pets   Namespace: DeNelle.Pets
//
// Drives the pet's Animator via cached parameter hashes. Exposes movement-speed
// setter and trigger methods for attack, hit, and death — same contract as
// HeroLocomotion so the pet combat system can treat them uniformly.
//
// Animation Events wired in the pet FBX/clip (Pet_Attack) fire OnAttackHit()
// at the impact frame. IMPORTANT: this component must live on the SAME
// GameObject as the Animator. If the Animator is on a child mesh, move this
// component there or add a forwarding relay on that child.
// =============================================================================

using UnityEngine;

namespace DeNelle.Pets
{
    /// <summary>
    /// Locomotion and combat animation driver for pet companions.
    /// Attach on the same GameObject as the pet's <see cref="Animator"/>.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]
    public sealed class PetAnimatorController : MonoBehaviour
    {
        private Animator _animator;

        // Cached parameter hashes — string lookup only at load time.
        private static readonly int SpeedHash  = Animator.StringToHash("Speed");
        private static readonly int AttackHash = Animator.StringToHash("Attack");
        private static readonly int HitHash    = Animator.StringToHash("Hit");
        private static readonly int DeadHash  = Animator.StringToHash("Dead");

        // WO-163: cached once at load — whether this controller declares each
        // param. Driving an absent param logs "Parameter does not exist" (per
        // frame for Speed). Guard every setter with these.
        private bool _hasSpeed;
        private bool _hasAttack;
        private bool _hasHit;
        private bool _hasDead;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            if (_animator == null)
                Debug.LogError($"[PetAnimatorController] No Animator on '{gameObject.name}'. " +
                               "This component must be on the same GameObject as the Animator.");

            if (_animator != null && _animator.runtimeAnimatorController != null)
            {
                foreach (var p in _animator.parameters)
                {
                    if (p.nameHash == SpeedHash)  _hasSpeed  = true;
                    if (p.nameHash == AttackHash) _hasAttack = true;
                    if (p.nameHash == HitHash)    _hasHit    = true;
                    if (p.nameHash == DeadHash)  _hasDead  = true;
                }
            }
        }

        // ── Movement ──────────────────────────────────────────────────────────

        /// <summary>
        /// Update the locomotion blend. Call each frame from the pet's movement
        /// script with the pet's current world-units-per-second speed.
        /// </summary>
        public void UpdateMovement(float speed)
        {
            if (_animator != null && _hasSpeed) _animator.SetFloat(SpeedHash, speed);
        }

        // ── Combat triggers ───────────────────────────────────────────────────

        /// <summary>Trigger the attack animation (e.g. when a target is in range).</summary>
        public void PlayAttack() { if (_animator != null && _hasAttack) _animator.SetTrigger(AttackHash); }

        /// <summary>Trigger the hit-react animation (called on taking damage).</summary>
        public void PlayHit()    { if (_animator != null && _hasHit) _animator.SetTrigger(HitHash); }

        /// <summary>Trigger the death animation (called when HP reaches zero).</summary>
        public void PlayDeath()  { if (_animator != null && _hasDead) _animator.SetBool(DeadHash, true); }

        // ── Animation Events ──────────────────────────────────────────────────
        // These MUST be on the same GameObject as the Animator.

        /// <summary>
        /// Called at the attack impact frame via Animation Event in Pet_Attack clip.
        /// Wire to PetCombat.TriggerHitWindow() when the pet combat system is built.
        /// </summary>
        public void OnAttackHit()
        {
            // TODO: wire to PetCombat when pet combat ticket is filed.
            // PetCombat?.TriggerHitWindow();
        }

        /// <summary>
        /// Called at each foot-down frame via Animation Event in locomotion clips.
        /// Wire to AudioService when audio ticket is filed.
        /// </summary>
        public void OnFootstep()
        {
            // TODO: AudioService.Instance?.PlayPetFootstep(transform.position);
        }
    }
}
