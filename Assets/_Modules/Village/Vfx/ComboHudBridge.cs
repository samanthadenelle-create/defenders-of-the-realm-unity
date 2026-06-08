// =============================================================================
// ComboHudBridge — surfaces the combat MOMENTUM on the HUD.
// -----------------------------------------------------------------------------
// CombatFeedbackManager raises OnComboChanged / OnKillStreakChanged on every hit
// and kill, but NOTHING subscribed — so the combo / kill-streak momentum the
// combat-feel stack tracks was completely invisible to the player. This bridge
// closes that gap: it subscribes to those events (CombatFeedbackManager is in
// THIS assembly, DeNelle.Village, so the read is a direct reference) and pushes
// the counts into the village HUD's "N× COMBO" / "N× KILLS" badge.
//
// Cross-asmdef: DeNelle.Village cannot reference DeNelle.HUD, so the HUD is
// discovered by component-type name and its SetComboCount(int) / SetKillStreak(int)
// setters are invoked by reflection — the SAME seam HeartHudBridge /
// HeroAbilitiesHudBridge / WaveHudBridge use. Attached at runtime alongside the
// other HUD bridges (the curated scene is not re-baked).
//
// CombatFeedbackManager is a BeforeSceneLoad DontDestroyOnLoad singleton, so its
// Instance survives scene loads. This bridge re-subscribes in OnEnable and tracks
// the subscribed instance so OnDisable cleans up exactly once.
// =============================================================================

using System.Reflection;
using UnityEngine;

namespace DeNelle.Village
{
    [DisallowMultipleComponent]
    public sealed class ComboHudBridge : MonoBehaviour
    {
        private Object _hud;                 // VillageHudController (held as Object — no DeNelle.HUD ref)
        private MethodInfo _setCombo;        // SetComboCount(int)
        private MethodInfo _setStreak;       // SetKillStreak(int)
        private readonly object[] _comboArgs = new object[1];
        private readonly object[] _streakArgs = new object[1];

        // The CombatFeedbackManager we're subscribed to (singleton; tracked so the
        // unsubscribe in OnDisable always matches the subscribe).
        private CombatFeedbackManager _subscribed;

        // Deferred resolution safety net (the HUD may load a frame after this bridge,
        // and the combat singleton bootstraps BeforeSceneLoad but is guarded anyway).
        private const int MaxResolveAttempts = 600;
        private int _resolveAttempts;
        private bool _gaveUp;

        private void OnEnable()
        {
            Resolve();
            TrySubscribe();
        }

        private void OnDisable()
        {
            if (_subscribed != null)
            {
                _subscribed.OnComboChanged -= OnComboChanged;
                _subscribed.OnKillStreakChanged -= OnKillStreakChanged;
                _subscribed = null;
            }
        }

        private void Update()
        {
            // Exit immediately once fully wired; otherwise keep trying to resolve
            // the HUD + subscribe to the combat singleton.
            if (_hud == null || _setCombo == null) Resolve();
            TrySubscribe();
        }

        private void Resolve()
        {
            if (_gaveUp) return;
            if (_hud != null && _setCombo != null) return; // fully resolved
            if (++_resolveAttempts > MaxResolveAttempts)
            {
                _gaveUp = true;
                Debug.LogWarning("[ComboHudBridge] VillageHudController not found — combo / kill-streak badge disabled.");
                return;
            }

            if (_hud == null)
            {
                foreach (var mb in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include))
                {
                    if (mb != null && mb.GetType().Name == "VillageHudController") { _hud = mb; break; }
                }
            }
            if (_hud != null && _setCombo == null)
            {
                var t = _hud.GetType();
                _setCombo = t.GetMethod("SetComboCount", BindingFlags.Public | BindingFlags.Instance, null,
                    new[] { typeof(int) }, null);
                _setStreak = t.GetMethod("SetKillStreak", BindingFlags.Public | BindingFlags.Instance, null,
                    new[] { typeof(int) }, null);
            }
        }

        private void TrySubscribe()
        {
            var mgr = CombatFeedbackManager.Instance;
            if (mgr == null || mgr == _subscribed) return;

            // Singleton swapped (rare — scene reload) — drop the stale subscription.
            if (_subscribed != null)
            {
                _subscribed.OnComboChanged -= OnComboChanged;
                _subscribed.OnKillStreakChanged -= OnKillStreakChanged;
            }
            _subscribed = mgr;
            _subscribed.OnComboChanged += OnComboChanged;
            _subscribed.OnKillStreakChanged += OnKillStreakChanged;
        }

        private void OnComboChanged(int count)
        {
            if (_setCombo == null || _hud == null) return;
            _comboArgs[0] = count;
            _setCombo.Invoke(_hud, _comboArgs);
        }

        private void OnKillStreakChanged(int streak)
        {
            if (_setStreak == null || _hud == null) return;
            _streakArgs[0] = streak;
            _setStreak.Invoke(_hud, _streakArgs);
        }
    }
}
