// =============================================================================
// HeartHudBridge — WO-20: pushes Heart HP + the crystal balance into the village
// HUD. Companion to WaveHudBridge (wave) and HeroAbilitiesHudBridge
// (mana/cooldowns). Before this, VillageHudController.SetHeartHp / SetCrystals
// had no runtime caller (only the DevPanel pushed Heart HP), so the Heart HP bar
// and crystal counter stayed frozen at their UXML defaults during normal play.
//
// Cross-asmdef: DeNelle.Village cannot reference DeNelle.HUD, so the HUD is
// discovered by component-type name and its setters invoked by reflection — the
// same seam WaveHudBridge / HeroAbilitiesHudBridge use. Attached at runtime by
// VillageController (the gates/hero/HUD are baked by the edit-time scene builder,
// which the curated-scene rule forbids re-running).
//
// DEF-54 (this pass): Heart HP is now event-driven via HeartController.OnHealthChanged.
//   The per-frame SetHeartHp() push has been removed from Update(). Crystals have
//   no change event on GameStateService so they remain on a 0.5 s throttled poll —
//   cheap and correct. Update() now only runs the crystal throttle + deferred
//   Resolve() retries; it exits immediately once fully resolved + subscribed.
// =============================================================================

using System.Reflection;
using DeNelle.Core.State;
using UnityEngine;

namespace DeNelle.Village
{
    [DisallowMultipleComponent]
    public sealed class HeartHudBridge : MonoBehaviour
    {
        // HeartController.SetHp clamps to 0-100, so the Heart HP scale max is 100.
        private const float HeartMaxHp = 100f;

        private Object _hud;                 // VillageHudController (held as Object — no DeNelle.HUD ref)
        private MethodInfo _setHeartHp;      // SetHeartHp(float current, float max)
        private MethodInfo _setCrystals;     // SetCrystals(int amount)
        private HeartController _heart;
        private readonly object[] _hpArgs = new object[2];
        private readonly object[] _crystalArgs = new object[1];

        // The bridge is attached after the scene (incl. the HUD) loads, so it
        // resolves on the first frame in practice. The cap is a pure safety net:
        // if the HUD/Heart are genuinely absent, stop the per-frame scene scans
        // rather than running FindObjectsByType forever (~10s at 60fps).
        private const int MaxResolveAttempts = 600;
        private int _resolveAttempts;
        private bool _gaveUp;

        // DEF-54: event subscription tracking — must unsub on OnDisable.
        private HeartController _subscribedHeart;

        // Crystal poll throttle — GameStateService has no change event.
        private const float CrystalPollInterval = 0.5f;
        private float _crystalPollTimer;

        private void OnEnable()
        {
            Resolve();
            TrySubscribeHeart();
            // Push the initial HP and crystals immediately so the HUD is correct
            // on enable without waiting for the first event or poll interval.
            PushHp(_heart != null ? _heart.Hp : 0f);
            PushCrystals();
        }

        private void OnDisable()
        {
            if (_subscribedHeart != null)
            {
                _subscribedHeart.OnHealthChanged -= OnHeartHpChanged;
                _subscribedHeart = null;
            }
        }

        private void Resolve()
        {
            if (_gaveUp) return;
            if (_hud != null && _heart != null) return; // fully resolved — nothing to scan
            if (++_resolveAttempts > MaxResolveAttempts)
            {
                _gaveUp = true;
                Debug.LogWarning("[HeartHudBridge] VillageHudController / HeartController not found — Heart-HP / crystal push disabled.");
                return;
            }

            if (_hud == null)
            {
                foreach (var mb in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include))
                {
                    if (mb != null && mb.GetType().Name == "VillageHudController") { _hud = mb; break; }
                }
            }
            if (_hud != null && _setHeartHp == null)
            {
                var t = _hud.GetType();
                _setHeartHp = t.GetMethod("SetHeartHp", BindingFlags.Public | BindingFlags.Instance, null,
                    new[] { typeof(float), typeof(float) }, null);
                _setCrystals = t.GetMethod("SetCrystals", BindingFlags.Public | BindingFlags.Instance, null,
                    new[] { typeof(int) }, null);
            }
            if (_heart == null) _heart = FindAnyObjectByType<HeartController>();
        }

        /// <summary>
        /// Subscribes to <see cref="HeartController.OnHealthChanged"/> once the heart
        /// is resolved. Idempotent — safe to call multiple times.
        /// </summary>
        private void TrySubscribeHeart()
        {
            if (_subscribedHeart != null) return;     // already subscribed
            if (_heart == null) return;               // not yet resolved

            _subscribedHeart = _heart;
            _heart.OnHealthChanged += OnHeartHpChanged;
        }

        private void OnHeartHpChanged(float hp) => PushHp(hp);

        private void PushHp(float hp)
        {
            if (_setHeartHp == null || _hud == null) return;
            _hpArgs[0] = hp;
            _hpArgs[1] = HeartMaxHp;
            _setHeartHp.Invoke(_hud, _hpArgs);
        }

        private void PushCrystals()
        {
            if (_setCrystals == null || _hud == null) return;
            _crystalArgs[0] = CurrentCrystals();
            _setCrystals.Invoke(_hud, _crystalArgs);
        }

        private void Update()
        {
            // Deferred resolution — exits immediately once fully wired.
            if (_hud == null || _heart == null)
            {
                Resolve();
                if (_hud == null || _heart == null) return;
                // Resolved this frame — subscribe and push the current state now.
                TrySubscribeHeart();
                PushHp(_heart.Hp);
                PushCrystals();
                _crystalPollTimer = 0f;
                return;
            }

            // If subscription was deferred (e.g. heart resolved before Awake race),
            // pick it up here.
            TrySubscribeHeart();

            // Crystal poll — GameStateService has no change event; throttle to 0.5s.
            _crystalPollTimer -= Time.deltaTime;
            if (_crystalPollTimer <= 0f)
            {
                _crystalPollTimer = CrystalPollInterval;
                PushCrystals();
            }
        }

        private static int CurrentCrystals()
        {
            var svc = GameStateService.Instance;
            var state = svc != null ? svc.State : null;
            return state != null ? state.Resources.Crystals : 0;
        }
    }
}
