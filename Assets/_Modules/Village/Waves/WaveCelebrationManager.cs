// =============================================================================
// WaveCelebrationManager — wave-clear dopamine burst (WO-83).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// Called by WaveManager.CompleteWave() (via OnWaveCleared event hook or direct
// call). Fires bloom spike, screen flash, slow-mo dip, VFX rain, floating
// "Wave X Cleared!" text, and a camera shake — all mobile-safe.
//
// Bloom: uses UnityEngine.Rendering.Universal.Bloom from a Volume profile.
//        If the Volume or Bloom component is absent the spike is skipped
//        gracefully. Same for all other optional systems (VFXManager, etc.).
//
// Results banner: routes through the ONE shared Obsidian end-state template
//   (DeNelle.Village.UI.EndStateView, compact wave-results variant — UI audit
//   2026-07-02 §3.2 WO-B). The old world-space prefab text + IMGUI OnGUI toast
//   are RETIRED (the IMGUI path was a LEGACY-verdict surface in the audit).
//
// Time.timeScale: always restored in a finally-equivalent path — the ease-back
//   coroutine always runs to completion via WaitForSecondsRealtime.
//
// Bootstrapped at runtime: a [RuntimeInitializeOnLoadMethod] installs the
// singleton after each scene load when a WaveManager is present, so no scene
// edit is required.
// =============================================================================

using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

#if UNITY_POST_PROCESSING_STACK_V2
// URP Bloom is accessed below via conditional compilation; no hard PPv2 dep.
#endif

namespace DeNelle.Village
{
    /// <summary>
    /// Plays a full celebration sequence on wave clear: bloom spike, screen flash,
    /// slow-mo, VFX rain, floating text, and camera shake. Installed automatically
    /// when a <see cref="WaveManager"/> exists in the scene.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WaveCelebrationManager : MonoBehaviour
    {
        public static WaveCelebrationManager Instance { get; private set; }

        // ── Screen effects ────────────────────────────────────────────────────

        [Header("Bloom (optional — skipped if Volume/Bloom absent)")]
        [Tooltip("Post-process Volume that owns the Bloom override. " +
                 "Leave null to skip the bloom spike.")]
        [SerializeField] private UnityEngine.Rendering.Volume _postProcessVolume;

        [SerializeField] private float _bloomPeakIntensity = 6f;
        [SerializeField] private float _bloomBaseline      = 1.2f;
        [SerializeField] private float _bloomDuration      = 0.55f;

        [Header("Screen Flash")]
        [SerializeField] private float _flashDuration = 0.3f;
        [SerializeField] private Color _flashColor    = new Color(1f, 0.95f, 0.7f, 0.7f);

        [Header("Slow Motion")]
        [SerializeField] private float _slowMoScale    = 0.28f;
        [SerializeField] private float _slowMoDuration = 0.9f;   // real seconds

        [Header("VFX Rain")]
        [SerializeField] private VFXType _celebrationVFX    = VFXType.WaveClear_Celebration;
        [SerializeField] private int     _celebrationBursts = 3;
        [SerializeField] private float   _burstSpread       = 4f;

        [Header("Celebration Anchor")]
        [Tooltip("World-space anchor for the VFX bursts (centre of village). " +
                 "Falls back to Vector3.zero + up when null.")]
        [SerializeField] private Transform  _textSpawnPoint;

        [Header("Mobile")]
        [SerializeField] private bool _reducedOnMobile = true;

        // ── Bloom runtime handle ──────────────────────────────────────────────
        private UnityEngine.Rendering.Universal.Bloom _bloom;
        private bool _bloomAvailable;

        // ─────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            // Resolve Bloom from the Volume profile (URP).
            if (_postProcessVolume != null && _postProcessVolume.profile != null)
            {
                if (_postProcessVolume.profile.TryGet(
                        out UnityEngine.Rendering.Universal.Bloom b))
                {
                    _bloom          = b;
                    _bloomAvailable = true;
                }
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            // Safety: restore time scale if destroyed mid-sequence.
            Time.timeScale = 1f;
        }

        // ── Called by WaveManager (via OnWaveCleared listener) ───────────────

        /// <summary>Trigger the full wave-clear celebration for <paramref name="waveNumber"/>.</summary>
        public void PlayWaveClear(int waveNumber)
        {
            StartCoroutine(WaveClearRoutine(waveNumber));
        }

        public static float Significance01(int waveNumber)
        {
            if (waveNumber <= 1) return 0f;
            if (waveNumber >= 7 && waveNumber % 7 == 0) return 1f;
            return Mathf.Clamp01((waveNumber - 1f) / 12f);
        }

        // ── Main sequence ─────────────────────────────────────────────────────

        private IEnumerator WaveClearRoutine(int waveNumber)
        {
            bool mobile = false;
#if UNITY_ANDROID || UNITY_IOS
            mobile = _reducedOnMobile;
#endif
            float mobileMult = mobile ? 0.6f : 1f;
            float significance = Significance01(waveNumber);
            float celebrationMult = Mathf.Lerp(0.45f, 1f, significance);

            // 1. Bloom spike (fire-and-forget coroutine).
            if (_bloomAvailable)
                StartCoroutine(BloomSpike(_bloomPeakIntensity * mobileMult * celebrationMult));

            // 2. Screen flash.
            StartCoroutine(ScreenFlash(mobile));

            // 3. Slow-mo dip.
            StartCoroutine(SlowMoDip(_slowMoDuration * mobileMult * celebrationMult));

            // 4. VFX rain bursts.
            int maxBursts = mobile ? Mathf.Max(1, _celebrationBursts - 1) : _celebrationBursts;
            int bursts = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(1f, maxBursts, significance)), 1, maxBursts);
            Vector3 origin = _textSpawnPoint != null
                ? _textSpawnPoint.position
                : Vector3.zero;

            for (int i = 0; i < bursts; i++)
            {
                Vector3 offset = new Vector3(
                    Random.Range(-_burstSpread, _burstSpread), 0f,
                    Random.Range(-_burstSpread, _burstSpread));
                VFXManager.Play(_celebrationVFX, origin + offset + Vector3.up * 1.5f);
                yield return new WaitForSecondsRealtime(0.12f);
            }

            // 5. "Wave X Cleared!" results banner — the shared end-state template
            //    (compact variant: non-blocking, auto-dismissing, one Continue).
            //
            // ARENA GUARD (owner-reported twice: Seeker 313763, desktop EXE F8 seq=2140).
            // EndStateView.Show DESTROYS whatever end-state is already open (EndStateView.cs:92).
            // The village WaveManager keeps ticking while the hero is away fighting a real-time
            // arena encounter 7km out at ArenaCentre, so a village wave can clear seconds after an
            // ARENA VICTORY panel appears. This banner then replaced that panel and took its
            // Continue action with it - and that action was the ONLY route home. The owner tapped
            // what she thought was Continue, hit this banner's action=dismiss instead, and was left
            // standing in the arena with the HUD locked in Battle.
            //
            // A wave-clear banner is pure garnish; an arena victory summary is load-bearing. When
            // they collide, the garnish yields. The wave is still cleared, rewarded and persisted -
            // only the cosmetic banner is skipped.
            // AnyBattleInProgress (BattleArena.cs:227) is the STATIC, null-safe accessor -
            // BattleInProgress itself is an instance property. Fully qualified because
            // BattleArena lives in DeNelle.Village.Arena and this file is DeNelle.Village.
            if (DeNelle.Village.Arena.BattleArena.AnyBattleInProgress)
            {
                DeNelle.Core.Diagnostics.FlowTrace.Warn("WaveCelebration",
                    $"wave {waveNumber} clear banner SUPPRESSED - an arena battle is in progress and " +
                    "showing it would destroy the arena victory summary (and its home-return action). " +
                    "The wave still cleared and rewarded; only the banner is skipped.");
            }
            else
            {
                DeNelle.Village.UI.EndStateView.Show(
                    DeNelle.Village.UI.EndStateVM.FromWaveClear(waveNumber));
            }

            // 6. Camera shake.
            float shakeIntensity = (mobile ? 0.25f : 0.42f) * celebrationMult;
            CameraShakeBridge.Shake(shakeIntensity, 0.35f);

            // AudioService.Instance?.PlaySfx(SfxId.WaveClear);
        }

        // ── Sub-routines ──────────────────────────────────────────────────────

        private IEnumerator BloomSpike(float peak)
        {
            if (!_bloomAvailable || _bloom == null) yield break;

            float elapsed = 0f;
            float rampUp  = _bloomDuration * 0.4f;

            // Ramp up (unscaled — survives slow-mo).
            while (elapsed < rampUp)
            {
                elapsed += Time.unscaledDeltaTime;
                _bloom.intensity.Override(Mathf.Lerp(_bloomBaseline, peak, elapsed / rampUp));
                yield return null;
            }

            // Decay back to baseline.
            elapsed = 0f;
            float decay = _bloomDuration * 0.6f;
            while (elapsed < decay)
            {
                elapsed += Time.unscaledDeltaTime;
                _bloom.intensity.Override(Mathf.Lerp(peak, _bloomBaseline, elapsed / decay));
                yield return null;
            }

            _bloom.intensity.Override(_bloomBaseline);
        }

        private IEnumerator ScreenFlash(bool mobile)
        {
            var cam = Camera.main;
            if (cam == null) yield break;

            Color orig  = cam.backgroundColor;
            float alpha = mobile ? _flashColor.a * 0.5f : _flashColor.a;
            Color flash = new Color(_flashColor.r, _flashColor.g, _flashColor.b, alpha);

            cam.backgroundColor = flash;
            yield return new WaitForSecondsRealtime(_flashDuration * 0.2f);

            float elapsed = 0f;
            while (elapsed < _flashDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                cam.backgroundColor = Color.Lerp(flash, orig, elapsed / _flashDuration);
                yield return null;
            }

            cam.backgroundColor = orig;
        }

        private IEnumerator SlowMoDip(float duration)
        {
            Time.timeScale = _slowMoScale;
            yield return new WaitForSecondsRealtime(duration);

            // Ease back to 1× over 0.3 real seconds.
            float elapsed = 0f;
            float ease    = 0.3f;
            while (elapsed < ease)
            {
                elapsed += Time.unscaledDeltaTime;
                Time.timeScale = Mathf.Lerp(_slowMoScale, 1f, elapsed / ease);
                yield return null;
            }
            Time.timeScale = 1f;
        }

        // ── Bootstrap — auto-install when WaveManager is present ─────────────

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallHook()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            TryInstall();
        }

        private static void OnSceneLoaded(Scene s, LoadSceneMode m) => TryInstall();

        private static void TryInstall()
        {
            if (FindAnyObjectByType<WaveManager>() == null) return;
            if (Instance != null) return;

            var go  = new GameObject("[WaveCelebrationManager]");
            var mgr = go.AddComponent<WaveCelebrationManager>();

            // Wire to WaveManager's OnWaveCleared event.
            var wave = FindAnyObjectByType<WaveManager>();
            if (wave != null)
                wave.OnWaveCleared.AddListener(mgr.PlayWaveClear);

            Debug.Log("[WaveCelebrationManager] Installed and wired to WaveManager.");
        }
    }
}
