// =============================================================================
// ActorAnimator — the one guarded driver for actor animation (WO-284).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.Combat
//
// Resolves the actor's Animator (it usually sits on a child mesh — the hero's
// HeroBody, the enemy's skeleton), caches which parameters the live controller
// actually declares, and routes every IActorAnimator verb to a guarded
// Set* call. Driving an ABSENT parameter logs an error every frame in Unity
// (the project's well-documented param-spam pitfall), so every call checks the
// cache first and silently no-ops when the state isn't present.
//
// RUNTIME CONTROLLER SWAP: the hero's body (and thus its Animator + controller)
// is replaced at runtime by HeroBodySwapper. ActorAnimator re-resolves the
// Animator whenever its cached one is gone, and re-scans the parameter cache
// whenever the Animator instance OR its runtimeAnimatorController changes — so a
// post-swap controller is always picked up without a manual refresh.
//
// Add it to any actor root: `GetComponent<ActorAnimator>() ?? AddComponent<>()`.
// =============================================================================

using System.Collections.Generic;
using UnityEngine;

namespace DeNelle.Core.Combat
{
    /// <summary>Concrete <see cref="IActorAnimator"/> — guarded, swap-aware.</summary>
    [DisallowMultipleComponent]
    public sealed class ActorAnimator : MonoBehaviour, IActorAnimator
    {
        private Animator _animator;
        private Animator _scannedAnimator;
        private RuntimeAnimatorController _scannedController;
        private readonly HashSet<int> _present = new HashSet<int>();

        /// <summary>The resolved Animator (may be null until a body is present).</summary>
        public Animator Animator { get { EnsureAnimator(); return _animator; } }

        private void Awake() => EnsureAnimator();

        /// <summary>
        /// Resolve-or-refresh the Animator + parameter cache. Cheap on the steady
        /// state (a couple of reference compares); only does real work when the
        /// body/controller changed.
        /// </summary>
        private void EnsureAnimator()
        {
            // Re-resolve when the cached animator is gone (destroyed body) or never found.
            if (_animator == null)
                _animator = GetComponentInChildren<Animator>(true);
            if (_animator == null) { _present.Clear(); _scannedAnimator = null; _scannedController = null; return; }

            var ctrl = _animator.runtimeAnimatorController;
            if (_animator == _scannedAnimator && ctrl == _scannedController) return;

            // Animator instance or its controller changed — rescan declared params.
            _scannedAnimator = _animator;
            _scannedController = ctrl;
            _present.Clear();
            if (ctrl == null) return;
            foreach (var p in _animator.parameters) _present.Add(p.nameHash);
        }

        private bool Has(int hash) => _present.Contains(hash);

        // ── IActorAnimator ───────────────────────────────────────────────────────

        public void SetLocomotion(float worldSpeed)
        {
            EnsureAnimator();
            if (_animator != null && Has(AnimParams.SpeedHash))
                _animator.SetFloat(AnimParams.SpeedHash, worldSpeed);
        }

        public void SetCombatStance(bool inCombat)
        {
            EnsureAnimator();
            if (_animator != null && Has(AnimParams.InCombatHash))
                _animator.SetBool(AnimParams.InCombatHash, inCombat);
        }

        public void PlayAttack(int combo = 0)
        {
            EnsureAnimator();
            if (_animator == null) return;
            if (Has(AnimParams.ComboHash)) _animator.SetInteger(AnimParams.ComboHash, combo);
            if (Has(AnimParams.AttackHash)) _animator.SetTrigger(AnimParams.AttackHash);
        }

        public void PlayCast()
        {
            EnsureAnimator();
            if (_animator != null && Has(AnimParams.CastHash))
                _animator.SetTrigger(AnimParams.CastHash);
        }

        public void PlayWindUp()
        {
            EnsureAnimator();
            if (_animator != null && Has(AnimParams.WindUpHash))
                _animator.SetTrigger(AnimParams.WindUpHash);
        }

        public void SetBlocking(bool on)
        {
            EnsureAnimator();
            if (_animator != null && Has(AnimParams.BlockHash))
                _animator.SetBool(AnimParams.BlockHash, on);
        }

        public void PlayHit(HitDirection dir)
        {
            EnsureAnimator();
            if (_animator == null) return;
            if (Has(AnimParams.HitDirHash)) _animator.SetInteger(AnimParams.HitDirHash, (int)dir);
            if (Has(AnimParams.HitHash)) _animator.SetTrigger(AnimParams.HitHash);
        }

        public void Die(DeathDirection dir = DeathDirection.Fall)
        {
            EnsureAnimator();
            if (_animator == null) return;
            if (Has(AnimParams.DeathDirHash)) _animator.SetInteger(AnimParams.DeathDirHash, (int)dir);
            if (Has(AnimParams.DeadHash)) _animator.SetBool(AnimParams.DeadHash, true);
        }

        public void Revive()
        {
            EnsureAnimator();
            if (_animator != null && Has(AnimParams.DeadHash))
                _animator.SetBool(AnimParams.DeadHash, false);
        }

        public void PlayVictory()
        {
            EnsureAnimator();
            if (_animator != null && Has(AnimParams.VictoryHash))
                _animator.SetTrigger(AnimParams.VictoryHash);
        }

        public void PlayTurn(TurnDirection dir)
        {
            EnsureAnimator();
            if (_animator != null && Has(AnimParams.TurnDirHash))
                _animator.SetInteger(AnimParams.TurnDirHash, (int)dir);
        }

        public void PlayEmote(EmoteType emote)
        {
            EnsureAnimator();
            if (_animator != null && Has(AnimParams.EmoteHash))
                _animator.SetInteger(AnimParams.EmoteHash, (int)emote);
        }

        /// <summary>
        /// Force a re-resolve of the Animator on the next call — optional hook for
        /// callers that just swapped the body and want the rescan to happen now.
        /// </summary>
        public void InvalidateAnimator()
        {
            _animator = null;
            _scannedAnimator = null;
            _scannedController = null;
            _present.Clear();
        }
    }
}
