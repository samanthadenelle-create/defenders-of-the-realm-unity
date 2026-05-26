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
        private WallRepairController _repair;
        private bool _imminentFired;
        private float _compassTimer;         // WO-39 poll throttle
        private Transform _heartT;

        /// <summary>Wires the wave manager + HUD before the GameObject is activated.</summary>
        public void Bind(WaveManager wave, object hud)
        {
            _wave = wave;
            _hud = hud;
            if (hud != null)
            {
                var t = hud.GetType();
                _showBanner = t.GetMethod("ShowWaveClearBanner", new[] { typeof(int), typeof(int) });
                _setImminent = t.GetMethod("SetWaveImminent", new[] { typeof(bool) });
                _setDirections = t.GetMethod("SetAttackDirections",
                    new[] { typeof(bool), typeof(bool), typeof(bool), typeof(bool) });
            }
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
            try { _showBanner?.Invoke(_hud, new object[] { waveId, 0 }); } catch { }
            PulseHeart();

            if (_repair == null) _repair = UnityEngine.Object.FindObjectOfType<WallRepairController>();
            if (_repair != null) { try { _repair.SurfaceWorstRepair(); } catch { } }

            CancelInvoke(nameof(ReturnToVillageMusic));
            Invoke(nameof(ReturnToVillageMusic), 5.5f);
        }

        private void ReturnToVillageMusic() => AbilityAudioBridge.PlayMusic("Village");

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
            _imminentFired = false;
            SetImminent(false);
        }

        private void OnCountdownTick(float secondsRemaining)
        {
            if (_imminentThreshold <= 0f || _imminentFired) return;
            if (secondsRemaining > 0f && secondsRemaining <= _imminentThreshold)
            {
                _imminentFired = true;
                SetImminent(true);
                AbilityAudioBridge.PlayDangerSting();
                TriggerHaptic();
                CancelInvoke(nameof(ClearImminent));
                Invoke(nameof(ClearImminent), 2.2f);
            }
        }

        private void ClearImminent() => SetImminent(false);

        private void SetImminent(bool on)
        {
            try { _setImminent?.Invoke(_hud, new object[] { on }); } catch { }
        }

        private void TriggerHaptic()
        {
#if UNITY_ANDROID || UNITY_IOS
            try { Handheld.Vibrate(); } catch { }
#endif
            // Gamepad rumble on desktop (best-effort; WebGL pads usually don't rumble).
            try
            {
                var pad = UnityEngine.InputSystem.Gamepad.current;
                if (pad != null)
                {
                    pad.SetMotorSpeeds(0.45f, 0.75f);
                    CancelInvoke(nameof(StopHaptic));
                    Invoke(nameof(StopHaptic), 0.18f);
                }
            }
            catch { }
        }

        private void StopHaptic()
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
            dir.Bind(wave, FindHud());
            go.SetActive(true);
            Debug.Log("[WaveFeedbackDirector] Installed (wave feedback active).");
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
