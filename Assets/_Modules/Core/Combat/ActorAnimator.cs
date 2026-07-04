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

using System.Collections;
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

        // WO-218: cached index of the "Upper Body" attack layer (-1 = none / not yet
        // resolved). The HeroAnimatorFactory authors an upper-body Override layer
        // (masked to arms+torso) so the hero can swing/cast WHILE moving — the base
        // layer keeps the legs in Locomotion. We force that layer's weight to 1 after
        // a controller (re)scan in case a runtime-swapped controller came in with the
        // layer weight reset; a controller without the layer leaves this -1 (no-op).
        private int _upperBodyLayer = -1;
        private const string UpperBodyLayerName = "Upper Body";

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
            _upperBodyLayer = -1;
            if (ctrl == null) return;
            foreach (var p in _animator.parameters) _present.Add(p.nameHash);

            // WO-218: locate the upper-body attack layer (if this controller has one)
            // and make sure it contributes at full weight, so an attack can play on the
            // arms while the legs keep their locomotion on the base layer. layer 0 is the
            // base layer — never touch it. Safe no-op on controllers without the layer.
            int layers = _animator.layerCount;
            for (int i = 1; i < layers; i++)
            {
                if (_animator.GetLayerName(i) == UpperBodyLayerName)
                {
                    _upperBodyLayer = i;
                    if (_animator.GetLayerWeight(i) < 1f) _animator.SetLayerWeight(i, 1f);
                    break;
                }
            }
        }

        private bool Has(int hash) => _present.Contains(hash);

        // ── IActorAnimator ───────────────────────────────────────────────────────

        // Short critically-damped smoothing on the Speed feed (owner 2026-07-04, KnightMocap V1).
        // HeroLocomotion feeds Speed = travel velocity, which snaps 0→_moveSpeed(6) in a single
        // frame on digital (keyboard/stick-to-max) input; an un-damped write jumps the 1-D blend
        // straight past the WALK band (~2) into RUN, so the walk clip never reads. Unity's damped
        // SetFloat overload eases the animator's Speed toward the target over ~SpeedDampTime, so a
        // start/stop ramps idle→walk→run (and back) through the walk pose. Universal (also smooths
        // enemy locomotion); near-zero cost. The RAW velocity is unchanged — only the animator's
        // view of it is smoothed.
        private const float SpeedDampTime = 0.12f;

        public void SetLocomotion(float worldSpeed)
        {
            EnsureAnimator();
            if (_animator != null && Has(AnimParams.SpeedHash))
                _animator.SetFloat(AnimParams.SpeedHash, worldSpeed, SpeedDampTime, Time.deltaTime);
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
            EnsureUpperBodyActive();   // WO-218: arms swing while legs keep walking
            if (Has(AnimParams.ComboHash)) _animator.SetInteger(AnimParams.ComboHash, combo);
            if (Has(AnimParams.AttackHash)) _animator.SetTrigger(AnimParams.AttackHash);
        }

        public void PlayCast() => PlayCast(0);

        public void PlayCast(int variant)
        {
            EnsureAnimator();
            if (_animator == null || !Has(AnimParams.CastHash)) return;
            EnsureUpperBodyActive();   // WO-218: cast plays on the arms while moving
            // Per-spell cast clip selection: set the variant (if the controller declares it)
            // BEFORE the trigger so the Any→CastN transition reads the right value this frame.
            // Controllers without CastVariant / per-variant states fall back to the default Cast.
            if (Has(AnimParams.CastVariantHash)) _animator.SetInteger(AnimParams.CastVariantHash, variant);
            _animator.SetTrigger(AnimParams.CastHash);
        }

        /// <summary>
        /// WO-218: guarantee the upper-body attack layer (if the controller has one)
        /// contributes at full weight, so a swing/cast overlays the arms while the base
        /// layer keeps the legs in locomotion — i.e. the hero can attack WHILE moving.
        /// Cheap (a single weight compare) and a safe no-op on controllers with no such
        /// layer (_upperBodyLayer stays -1). NOT a HasParameter concern — layer weight
        /// is not an animator parameter, so only the null/layer guards apply.
        /// </summary>
        private void EnsureUpperBodyActive()
        {
            if (_animator == null || _upperBodyLayer < 1) return;
            if (_animator.GetLayerWeight(_upperBodyLayer) < 1f)
                _animator.SetLayerWeight(_upperBodyLayer, 1f);
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

        public void SetInjured(bool injured)
        {
            EnsureAnimator();
            if (_animator != null && Has(AnimParams.InjuredHash))
                _animator.SetBool(AnimParams.InjuredHash, injured);
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

        // ── WO-217: attack tempo shaping (anticipation → impact → recovery) ──────
        // A flat clip read as a single uniform swing. Modulating Animator.speed over
        // the swing gives it WEIGHT: a brief slow wind-up (anticipation), a fast snap
        // through the contact frame (impact), then a settle back to normal (recovery).
        // This drives Animator.speed (a global multiplier, NOT a parameter), so no
        // HasParameter guard applies — only a null-guard on the Animator. Re-entrant:
        // a new shape cancels the previous one and always restores speed = 1 on exit.
        private Coroutine _tempoRoutine;

        /// <summary>
        /// WO-217: shape this attack's tempo for felt weight. Plays a slow anticipation
        /// for <paramref name="anticipation"/>s at <paramref name="windUpSpeed"/>, snaps
        /// to <paramref name="impactSpeed"/> through the contact for <paramref name="impact"/>s,
        /// then settles back to 1.0 over <paramref name="recovery"/>s. All durations are
        /// caller-supplied (data-driven). Safe no-op when no Animator is resolved.
        /// </summary>
        public void ShapeAttackTempo(float anticipation, float windUpSpeed,
                                     float impact, float impactSpeed, float recovery)
        {
            EnsureAnimator();
            if (_animator == null) return;
            if (_tempoRoutine != null) StopCoroutine(_tempoRoutine);
            _tempoRoutine = StartCoroutine(
                ShapeAttackTempoRoutine(anticipation, windUpSpeed, impact, impactSpeed, recovery));
        }

        private IEnumerator ShapeAttackTempoRoutine(float anticipation, float windUpSpeed,
                                                    float impact, float impactSpeed, float recovery)
        {
            // Anticipation: ease INTO the wind-up speed (the coil before the strike).
            yield return LerpSpeed(1f, Mathf.Max(0.05f, windUpSpeed), Mathf.Max(0f, anticipation));
            // Impact: snap to the fast contact speed and hold it briefly (the strike).
            if (_animator != null) _animator.speed = Mathf.Max(0.05f, impactSpeed);
            if (impact > 0f) yield return new WaitForSeconds(impact);
            // Recovery: ease back to normal (the follow-through settle).
            yield return LerpSpeed(Mathf.Max(0.05f, impactSpeed), 1f, Mathf.Max(0f, recovery));
            if (_animator != null) _animator.speed = 1f;
            _tempoRoutine = null;
        }

        private IEnumerator LerpSpeed(float from, float to, float dur)
        {
            if (_animator == null) yield break;
            if (dur <= 0f) { _animator.speed = to; yield break; }
            float t = 0f;
            while (t < dur)
            {
                if (_animator == null) yield break;
                t += Time.deltaTime;
                _animator.speed = Mathf.Lerp(from, to, Mathf.Clamp01(t / dur));
                yield return null;
            }
            _animator.speed = to;
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
