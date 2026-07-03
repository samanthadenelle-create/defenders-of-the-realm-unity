// =============================================================================
// WaveFeedbackDirector — wave-lifecycle juice (WO-38 + WO-40).
// -----------------------------------------------------------------------------
//   WO-38 (wave complete): on OnWaveCleared -> victory music sting, a "WAVE n
//          REPELLED" HUD banner, an amber Heart pulse, and a nudge to repair the
//          worst-damaged wall; village music resumes after a short dwell.
//   WO-40 (wave imminent): when the countdown drops to the alert threshold ->
//          a red edge vignette, a tense danger sting, and a haptic pulse
//          (gamepad rumble on desktop / Handheld.Vibrate on mobile; WebGL relies
//          on the visual + audio). One-shot per countdown; reset on wave start.
//
// WO-41 refactor: reflection removed throughout. HUD calls now go through
// CoreServices.Hud (IVillageHud) and audio through AbilityAudioBridge which
// itself uses CoreServices.Audio. FindHud() stub kept so TrySpawn compiles.
// =============================================================================

using DeNelle.Core;
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
        private WallRepairController _repair;
        private bool _imminentFired;
        private float _compassTimer;         // WO-39 poll throttle
        private Transform _heartT;

        /// <summary>Wires the wave manager before the GameObject is activated.</summary>
        public void Bind(WaveManager wave, object hud)
        {
            _wave = wave;
            // hud parameter kept for call-site compatibility; CoreServices.Hud used directly.
        }

        // ── WO-39: poll live enemies -> light the compass arms ────────────────
        private void Update()
        {
            if (_wave == null) return;
            _compassTimer -= Time.unscaledDeltaTime;
            if (_compassTimer > 0f) return;
            _compassTimer = 0.25f;

            if (_heartT == null)
            {
                var heart = UnityEngine.Object.FindAnyObjectByType<HeartController>();
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
            CoreServices.Hud?.SetAttackDirections(n, e, s, w);
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
            // "+N diamond" line actually renders (was hard-coded to 0). The wave reward is
            // credited by the wave/reward path before this fires, so the balance is
            // the freshest number we have without a per-wave reward field.
            int crystals = CurrentCrystals();
            CoreServices.Hud?.ShowWaveClearBanner(waveId, crystals, string.Empty);
            PulseHeart();

            // ── Per-wave soft-currency income (tunables in one place) ────────────
            // Wisdom (DEF-12): talent-tree income hook — a small amount each cleared
            // wave so the skill tree is progressable through normal play.
            // Glimmer (DEF-29): cosmetic-shop income — the cosmetic costs 80 and the
            // player starts at 25, with the only other earn paths being level-5+ tier
            // milestones or IAP. A modest per-wave trickle lets a player reach the
            // first cosmetic over ~a dozen waves of normal play — earned, not grindy.
            const int wisdomPerWave  = 2;
            const int glimmerPerWave = 4;
            try { DeNelle.Village.Talents.WisdomCurrencyService.Instance?.Grant(wisdomPerWave); } catch { }
            try { DeNelle.Cosmetics.GlimmerCurrencyService.Instance?.TryAddGlimmer(glimmerPerWave); } catch { }

            if (_repair == null) _repair = UnityEngine.Object.FindAnyObjectByType<WallRepairController>();
            if (_repair != null) { try { _repair.SurfaceWorstRepair(); } catch { } }

            CancelInvoke(nameof(ReturnToVillageMusic));
            Invoke(nameof(ReturnToVillageMusic), 5.5f);
        }

        private void ReturnToVillageMusic() => AbilityAudioBridge.PlayMusic("Village");

        // WO-38: the player's current crystal balance for the wave-clear banner.
        private static int CurrentCrystals()
        {
            var svc = GameStateService.Instance;
            if (svc != null && svc.State != null)
                return Mathf.Max(0, svc.State.Resources.Crystals);
            return 0;
        }

        private void PulseHeart()
        {
            var heart = UnityEngine.Object.FindAnyObjectByType<HeartController>();
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
            // DEF-183: a distinct wave-START battle horn ("here they come"), played
            // through the existing audio surface (CoreServices.Audio, guarded). This
            // is separate from the countdown-phase danger sting fired by
            // FireImminentAlert below — the horn lands the moment a wave actually
            // begins, the sting telegraphs it during the countdown.
            GameSfx.PlayWaveStart();

            // FAIL #6 cause (a): the owner starts waves with the HUD "START WAVE"
            // button -> WaveManager.ForceBeginNextWave snaps the countdown straight
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
        // amber compass flash, danger sting + haptic.
        private void FireImminentAlert(string reason)
        {
            _imminentFired = true;

            Debug.Log($"[WaveFeedbackDirector] Wave-imminent ALERT fired ({reason}); " +
                      $"hudBound={CoreServices.Hud != null}.");

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
            CoreServices.Hud?.SetWaveImminent(on);
        }

        private void SetCompassImminent(bool on)
        {
            // SetCompassImminent is an internal-only method on VillageHudController
            // (not part of IVillageHud); SetWaveImminent drives the imminent state.
            CoreServices.Hud?.SetWaveImminent(on);
        }

        // WO-40: a DOUBLE-PULSE -- two short rumbles (~0.12s on, ~0.10s gap, ~0.12s
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
            var wave = UnityEngine.Object.FindAnyObjectByType<WaveManager>();
            if (wave == null) return;   // not a wave scene (Title/HeroSelect/etc.)
            if (UnityEngine.Object.FindAnyObjectByType<WaveFeedbackDirector>() != null) return;

            // Inactive-then-activate so Bind() runs before OnEnable subscribes.
            var go = new GameObject("WaveFeedbackDirector");
            go.SetActive(false);
            var dir = go.AddComponent<WaveFeedbackDirector>();
            var hud = FindHud();
            dir.Bind(wave, hud);
            go.SetActive(true);
            Debug.Log($"[WaveFeedbackDirector] Installed (wave feedback active). hudBound={CoreServices.Hud != null}.");
        }

        private static object FindHud()
        {
            // WO-41: HUD is now registered in CoreServices.Hud; no reflection needed.
            // Stub kept so Bind(wave, hud) call-site compiles unchanged.
            return null;
        }
    }
}
