// =============================================================================
// HeroHitReaction — screen-edge damage flash + death slow-mo. Combat feel.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// WHAT IT DOES:
//   • Damage flash — a red full-screen tint spikes and fades whenever the hero's
//     HP drops, giving the "I got hit" read that HP-number changes alone don't.
//   • Death slow-mo — on death, time briefly ramps to a crawl and recovers, for a
//     dramatic beat. One-shot; always restores Time.timeScale to 1.
//
// DESIGN (deliberately self-contained + low-risk):
//   • Driven off HeroHealth's real C# events (OnHealthChanged / OnDied) — NOT the
//     greenfield WO's UnityEvents, which this branch's HeroHealth doesn't use.
//   • Rendered with IMGUI (OnGUI), NOT a URP post-process Vignette: it needs no
//     scene Volume/profile and always renders in player builds (UI-Toolkit HUDs
//     have repeatedly come up empty here — see HeroHealth's own IMGUI bar).
//   • Added to the hero automatically by HeroHealthBootstrap alongside HeroHealth,
//     so it needs no prefab wiring.
// =============================================================================

using System.Collections;
using UnityEngine;
using DeNelle.Core.Combat;   // WO-285: ActorAnimator hit-reaction driver
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village
{
    /// <summary>Red screen flash on hero damage and a slow-motion beat on death.</summary>
    [DisallowMultipleComponent]
    public sealed class HeroHitReaction : MonoBehaviour
    {
        [Header("Damage flash")]
        [Tooltip("Peak opacity of the red full-screen flash on a hit (0-1).")]
        [SerializeField, Range(0f, 1f)] private float _flashPeak = 0.28f;

        [Tooltip("Seconds for the flash to fade from peak to nothing.")]
        [SerializeField, Min(0.05f)] private float _flashFade = 0.32f;

        [Header("Death slow-mo")]
        [Tooltip("Time.timeScale at the moment of death.")]
        [SerializeField, Range(0.05f, 1f)] private float _deathTimeScale = 0.30f;

        [Tooltip("Real seconds over which time recovers from the death slow-mo.")]
        [SerializeField, Min(0.1f)] private float _deathSlowMoSeconds = 1.2f;

        private HeroHealth _health;
        private float _flashAlpha;
        private float _lastHp = -1f;
        private bool  _diedHandled;

        // FIX 1 (owner "walk became messed up"): under heavy aggro HeroHealth broadcasts an HP tick
        // every frame; without a cooldown each one re-latches the full-body Hit trigger and pins the
        // stagger over locomotion. Debounce the body flinch (the red flash still fires on every hit).
        private float _nextHitAnimTime;
        private const float HitAnimCooldown = 0.5f;

        // WO-285: plays the body hit-reaction clip on damage (the screen flash was the
        // only "I got hit" read before). Resolved on the hero root; guarded internally.
        private ActorAnimator _actor;

        private static readonly Color FlashColor = new Color(0.8f, 0.05f, 0.05f);

        private void OnEnable()
        {
            // Start() / OnEnable run after HeroHealth.Awake, so the instance + its
            // events exist. Resolve on the same GameObject, fall back to the singleton.
            _health = GetComponent<HeroHealth>();
            if (_health == null) _health = HeroHealth.Instance;
            if (_health == null)
            {
                FlowTrace.Warn("HitReact", "OnEnable: no HeroHealth on GO or singleton — hit reaction INERT (no flash / no death slow-mo).");
                return;
            }

            _lastHp = _health.Hp;
            if (!TryGetComponent(out _actor)) _actor = gameObject.AddComponent<ActorAnimator>();
            _health.OnHealthChanged += HandleHealthChanged;
            _health.OnDied         += HandleDied;
        }

        private void OnDisable()
        {
            if (_health != null)
            {
                _health.OnHealthChanged -= HandleHealthChanged;
                _health.OnDied         -= HandleDied;
            }
        }

        private void HandleHealthChanged(float current, float max)
        {
            // Flash only on a decrease (ignore heals / the initial broadcast).
            if (_lastHp >= 0f && current < _lastHp)
            {
                if (FlowTrace.Enabled) FlowTrace.Step("HitReact", $"damage flash: hp {_lastHp:0.#}->{current:0.#}/{max:0.#} fatal={(current <= 0f)}");
                _flashAlpha = _flashPeak;
                // WO-285: play the body flinch — but NOT on the killing blow (the death
                // anim owns that beat; HandleDied fires for it). No attacker bearing is
                // available here, so use a generic front/gut flinch.
                // FIX 1: debounce so rapid multi-hits don't re-latch the Hit trigger and pin the stagger.
                if (current > 0f && Time.unscaledTime >= _nextHitAnimTime)
                {
                    _actor?.PlayHit(HitDirection.Gut);
                    _nextHitAnimTime = Time.unscaledTime + HitAnimCooldown;
                }
            }
            _lastHp = current;
        }

        private void HandleDied()
        {
            if (_diedHandled) return;
            _diedHandled = true;
            FlowTrace.Step("HitReact", "hero died — strong flash + death slow-mo beat.");
            _flashAlpha = _flashPeak;            // strong flash on the killing blow
            StartCoroutine(DeathSlowMo());
        }

        private void Update()
        {
            if (_flashAlpha > 0f)
                _flashAlpha = Mathf.Max(0f, _flashAlpha - (_flashPeak / _flashFade) * Time.unscaledDeltaTime);
        }

        private IEnumerator DeathSlowMo()
        {
            Time.timeScale = _deathTimeScale;
            float t = 0f;
            while (t < _deathSlowMoSeconds)
            {
                t += Time.unscaledDeltaTime;
                Time.timeScale = Mathf.Lerp(_deathTimeScale, 1f, t / _deathSlowMoSeconds);
                yield return null;
            }
            Time.timeScale = 1f;
            FlowTrace.Step("HitReact", "death slow-mo complete — Time.timeScale restored to 1.");
            // Game-over flow hooks off HeroHealth.OnDied elsewhere (WO46 P11).
        }

        private void OnGUI()
        {
            if (_flashAlpha <= 0f) return;
            var prev = GUI.color;
            GUI.color = new Color(FlashColor.r, FlashColor.g, FlashColor.b, _flashAlpha);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = prev;
        }
    }
}
