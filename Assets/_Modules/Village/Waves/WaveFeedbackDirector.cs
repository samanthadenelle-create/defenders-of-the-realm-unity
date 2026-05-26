// =============================================================================
// WaveFeedbackDirector — wave-lifecycle juice (WO-38 + WO-40).
// -----------------------------------------------------------------------------
//   WO-38 (wave complete): on OnWaveCleared → victory music sting, a "WAVE n
//          REPELLED" HUD banner, an amber Heart pulse, and a nudge to repair the
//          worst-damaged wall; village music resumes after a short dwell.
//   WO-40 (wave imminent): when the countdown drops to the alert threshold →
//          a red edge vignette, a tense danger sting, and a haptic pulse
//          (gamepad rumble on desktop / Handheld.Vibrate on mobile; WebGL relies
//          on the visual + audio). One-shot per countdown; reset on wave start.
//
// Added to the village scene at runtime by a SceneManager.sceneLoaded hook (no
// scene edit / re-bake). Lives in DeNelle.Village so it can call WaveManager,
// WallRepairController, HeartController, AbilityVfxKit + AbilityAudioBridge
// directly; the HUD (DeNelle.HUD) is reached by reflection (no asmdef ref), the
// project's established bridge pattern.
// =============================================================================

using System;
using System.Reflection;
using DeNelle.Core.State;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeNelle.Village
{
    [DisallowMultipleComponent]
    public sealed class WaveFeedbackDirector : MonoBehaviour
    {
        [Tooltip("Seconds-remaining at which the wave-imminent alert fires. 0 disables it.")]
        [SerializeField] private float _imminentThreshold = 3f;

        private WaveManager _wave;
        private object _hud;                 // DeNelle.HUD.VillageHudController
        private MethodInfo _showBanner;      // ShowWaveClearBanner(int, int)
        private MethodInfo _setImminent;     // SetWaveImminent(bool)
        private MethodInfo _setDirections;   // SetAttackDirections(bool,bool,bool,bool) — WO-39
        private MethodInfo _setCompassImminent; // SetCompassImminent(bool) — WO-40 compass flash
        private WallRepairController _repair;
        private bool _imminentFired;
        private float _compassTimer;         // WO-39 poll throttle
        private Transform _heartT;

        /// <summary>Wires the wave manager + HUD before the GameObject is activated.</summary>
        public void Bind(WaveManager wave, object hud)
        {
            _wave = wave;
            _hud = hud;
            ResolveHudMethods();
        }

        // FAIL #6 cause (b): the HUD may not have existed yet when TrySpawn ran, so
        // _hud / the method binds were null and every SetWaveImminent call was a
        // silent no-op. Re-find the HUD + (re)resolve the reflection binds on demand
        // so the alert works even when the HUD bootstraps after this director.
        private void ResolveHudMethods()
        {
            if (_hud == null) _hud = FindHud();
            if (_hud == null) return;

            var t = _hud.GetType();
            if (_showBanner == null)
                _showBanner = t.GetMethod("ShowWaveClearBanner", new[] { typeof(int), typeof(int) });
            if (_setImminent == null)
                _setImminent = t.GetMethod("SetWaveImminent", new[] { typeof(bool) });
            if (_setDirections == null)
                _setDirections = t.GetMethod("SetAttackDirections",
                    new[] { typeof(bool), typeof(bool), typeof(bool), typeof(bool) });
            if (_setCompassImminent == null)
                _setCompassImminent = t.GetMethod("SetCompassImminent", new[] { typeof(bool) });
        }

        // ── WO-39: poll live enemies → light the compass arms ────────────────
        private void Update()
        {
            if (_wave == null || _setDirections == null) return;
            _compassTimer -= Time.unscaledDeltaTime;
            if (_compassTimer > 0f) return;
            _compassTimer = 0.25f;

            if (_heartT == null)
            {
                var heart = UnityEngine.Object.FindObjectOfType<HeartController>();
                if (heart != null) _heartT = heart.transform;
            }
            Vector3 c = _heartT != null ? _heartT.position : Vector3.zero;

            bool n = false, e = false, s = false, w = false;
            var live = _wave.LiveEnemies;
            if (live != null)
            {
                for (int i = 0; i < live.Count; i++)
                {
                    var en = live[i];
                    if (en == null) continue;
                    Vector3 d = en.transform.position - c; d.y = 0f;
                    if (Mathf.Abs(d.x) >= Mathf.Abs(d.z)) { if (d.x >= 0f) e = true; else w = true; }
                    else { if (d.z >= 0f) n = true; else s = true; }
                }
            }
            try { _setDirections.Invoke(_hud, new object[] { n, e, s, w }); } catch { }
        }

        private void OnEnable()
        {
            if (_wave == null) return;
            _wave.OnWaveCleared.AddListener(OnWaveCleared);
            _wave.OnWaveStarted.AddListener(OnWaveStarted);
            _wave.OnCountdownTick.AddListener(OnCountdownTick);
        }

        private void OnDisable()
        {
            if (_wave == null) return;
            _wave.OnWaveCleared.RemoveListener(OnWaveCleared);
            _wave.OnWaveStarted.RemoveListener(OnWaveStarted);
            _wave.OnCountdownTick.RemoveListener(OnCountdownTick);
        }

        // ── WO-38: wave complete ─────────────────────────────────────────────
        private void OnWaveCleared(int waveId)
        {
            AbilityAudioBridge.PlayMusic("Victory");
            // WO-38: show the player's current crystal balance on the banner so the
            // "+N ◆" line actually renders (was hard-coded to 0). The wave reward is
            // credited by the wave/reward path before this fires, so the balance is
            // the freshest number we have without a per-wave reward field.
            int crystals = CurrentCrystals();
            try { _showBanner?.Invoke(_hud, new object[] { waveId, crystals }); } catch { }
            PulseHeart();

            if (_repair == null) _repair = UnityEngine.Object.FindObjectOfType<WallRepairController>();
            if (_repair != null) { try { _repair.SurfaceWorstRepair(); } catch { } }

            CancelInvoke(nameof(ReturnToVillageMusic));
            Invoke(nameof(ReturnToVillageMusic), 5.5f);
        }

        private void ReturnToVillageMusic() => AbilityAudioBridge.PlayMusic("Village");

        // WO-38: the player's current crystal balance for the wave-clear banner.
        // Reads GameStateService when Core is bootstrapped; falls back to 0 (the
        // banner then shows just "WAVE n REPELLED" — its own crystals>0 guard).
        private static int CurrentCrystals()
        {
            var svc = GameStateService.Instance;
            if (svc != null && svc.State != null)
                return Mathf.Max(0, svc.State.Resources.Crystals);
            return 0;
        }

        private void PulseHeart()
        {
            var heart = UnityEngine.Object.FindObjectOfType<HeartController>();
            Vector3 p = heart != null ? heart.transform.position : Vector3.zero;
            try
            {
                AbilityVfxKit.SpawnAbilityVfx(AbilityEffect.Heal,
                    new Color(1f, 0.84f, 0.40f), p + Vector3.up * 0.5f, 3.5f, p);
            }
            catch { /* cosmetic */ }
        }

        // ── WO-40: wave imminent ─────────────────────────────────────────────
        private void OnWaveStarted(int waveId)
        {
            // FAIL #6 cause (a): the owner starts waves with the HUD "START WAVE"
            // button → WaveManager.ForceBeginNextWave snaps the countdown straight
            // to 0 and only ever ticks OnCountdownTick(0f), so the 0 < x <= threshold
            // window is NEVER hit and the alert silently never fired. If the alert
            // hasn't fired by the time the wave starts (force-start or a zero/short
            // countdown), fire it now so the red vignette + compass flash still play.
            if (!_imminentFired)
                FireImminentAlert("wave-start (countdown skipped)");

            // Now reset for the NEXT wave's countdown and clear the active alert
            // shortly after, so the flash still reads on the wave kicking off.
            _imminentFired = false;
            CancelInvoke(nameof(ClearImminent));
            Invoke(nameof(ClearImminent), 2.2f);
        }

        private void OnCountdownTick(float secondsRemaining)
        {
            if (_imminentThreshold <= 0f || _imminentFired) return;
            if (secondsRemaining > 0f && secondsRemaining <= _imminentThreshold)
                FireImminentAlert($"countdown {secondsRemaining:0.0}s <= {_imminentThreshold:0.0}s");
        }

        // Single entry point for the wave-imminent alert (WO-40): red edge vignette,
        // amber compass flash, danger sting + haptic. Re-resolves the HUD binds first
        // (cause (b)) and logs so the alert is verifiable in the player log.
        private void FireImminentAlert(string reason)
        {
            _imminentFired = true;
            ResolveHudMethods();   // FAIL #6 cause (b): bind lazily if the HUD arrived late

            Debug.Log($"[WaveFeedbackDirector] Wave-imminent ALERT fired ({reason}); " +
                      $"setImminentBound={_setImminent != null}, compassBound={_setCompassImminent != null}.");

            SetImminent(true);
            SetCompassImminent(true);   // WO-40: flash ALL compass arms amber during the alert
            AbilityAudioBridge.PlayDangerSting();
            TriggerHaptic();
            CancelInvoke(nameof(ClearImminent));
            Invoke(nameof(ClearImminent), 2.2f);
        }

        private void ClearImminent()
        {
            SetImminent(false);
            SetCompassImminent(false);
        }

        private void SetImminent(bool on)
        {
            try { _setImminent?.Invoke(_hud, new object[] { on }); } catch { }
        }

        private void SetCompassImminent(bool on)
        {
            try { _setCompassImminent?.Invoke(_hud, new object[] { on }); } catch { }
        }

        // WO-40: a DOUBLE-PULSE — two short rumbles (~0.12s on, ~0.10s gap, ~0.12s
        // on) so the alert reads as a deliberate "bump-bump", not a single buzz.
        // Sequenced with Invoke so it works without coroutines on the gamepad;
        // Handheld.Vibrate is double-fired on mobile.
        private void TriggerHaptic()
        {
#if UNITY_ANDROID || UNITY_IOS
            try { Handheld.Vibrate(); } catch { }
            // Second mobile buzz lines up with the gamepad's second pulse.
            CancelInvoke(nameof(VibrateHandheldAgain));
            Invoke(nameof(VibrateHandheldAgain), 0.22f);
#endif
            // Gamepad rumble on desktop (best-effort; WebGL pads usually don't rumble).
            try
            {
                var pad = UnityEngine.InputSystem.Gamepad.current;
                if (pad != null)
                {
                    // First pulse now; stop it, then fire a second pulse after a gap.
                    pad.SetMotorSpeeds(0.45f, 0.75f);
                    CancelInvoke(nameof(StopHaptic));
                    CancelInvoke(nameof(SecondHapticPulse));
                    CancelInvoke(nameof(StopHapticFinal));
                    Invoke(nameof(StopHaptic), 0.12f);          // end first pulse
                    Invoke(nameof(SecondHapticPulse), 0.22f);   // start second pulse
                    Invoke(nameof(StopHapticFinal), 0.34f);     // end second pulse
                }
            }
            catch { }
        }

#if UNITY_ANDROID || UNITY_IOS
        private void VibrateHandheldAgain()
        {
            try { Handheld.Vibrate(); } catch { }
        }
#endif

        private void SecondHapticPulse()
        {
            try { UnityEngine.InputSystem.Gamepad.current?.SetMotorSpeeds(0.45f, 0.75f); } catch { }
        }

        private void StopHaptic()
        {
            try { UnityEngine.InputSystem.Gamepad.current?.SetMotorSpeeds(0f, 0f); } catch { }
        }

        private void StopHapticFinal()
        {
            try { UnityEngine.InputSystem.Gamepad.current?.SetMotorSpeeds(0f, 0f); } catch { }
        }

        // ── Runtime install (no scene edit) ──────────────────────────────────
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallHook()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            TrySpawn();   // the first scene is already loaded when this runs
        }

        private static void OnSceneLoaded(Scene s, LoadSceneMode mode) => TrySpawn();

        private static void TrySpawn()
        {
            var wave = UnityEngine.Object.FindObjectOfType<WaveManager>();
            if (wave == null) return;   // not a wave scene (Title/HeroSelect/etc.)
            if (UnityEngine.Object.FindObjectOfType<WaveFeedbackDirector>() != null) return;

            // Inactive-then-activate so Bind() runs before OnEnable subscribes.
            var go = new GameObject("WaveFeedbackDirector");
            go.SetActive(false);
            var dir = go.AddComponent<WaveFeedbackDirector>();
            var hud = FindHud();
            dir.Bind(wave, hud);
            go.SetActive(true);
            Debug.Log($"[WaveFeedbackDirector] Installed (wave feedback active). hudFound={hud != null} " +
                      "(binds re-resolve lazily if the HUD arrives later).");
        }

        private static object FindHud()
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType("DeNelle.HUD.VillageHudController", false);
                if (t == null) continue;
                return UnityEngine.Object.FindObjectOfType(t);
            }
            return null;
        }
    }
}
