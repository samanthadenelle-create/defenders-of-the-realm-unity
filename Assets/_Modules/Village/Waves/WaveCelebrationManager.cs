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
// Floating text: instantiates waveTextPrefab (a world-space GameObject with a
//   TextMeshPro component). Falls back to an IMGUI toast when the prefab is
//   null so the text still appears in builds.
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

        [Header("Floating Text")]
        [Tooltip("World-space prefab with a TextMeshPro component. " +
                 "Falls back to IMGUI toast when null.")]
        [SerializeField] private GameObject _waveTextPrefab;
        [Tooltip("World-space anchor for floating text (centre of village). " +
                 "Falls back to Vector3.zero + up when null.")]
        [SerializeField] private Transform  _textSpawnPoint;

        [Header("Mobile")]
        [SerializeField] private bool _reducedOnMobile = true;

        // ── Bloom runtime handle ──────────────────────────────────────────────
        private UnityEngine.Rendering.Universal.Bloom _bloom;
        private bool _bloomAvailable;

        // ── IMGUI toast fallback ──────────────────────────────────────────────
        private string _toastText;
        private float  _toastTimer;

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

        // ── Main sequence ─────────────────────────────────────────────────────

        private IEnumerator WaveClearRoutine(int waveNumber)
        {
            bool mobile = false;
#if UNITY_ANDROID || UNITY_IOS
            mobile = _reducedOnMobile;
#endif
            float mobileMult = mobile ? 0.6f : 1f;

            // 1. Bloom spike (fire-and-forget coroutine).
            if (_bloomAvailable)
                StartCoroutine(BloomSpike(_bloomPeakIntensity * mobileMult));

            // 2. Screen flash.
            StartCoroutine(ScreenFlash(mobile));

            // 3. Slow-mo dip.
            StartCoroutine(SlowMoDip(_slowMoDuration * mobileMult));

            // 4. VFX rain bursts.
            int bursts = mobile ? Mathf.Max(1, _celebrationBursts - 1) : _celebrationBursts;
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

            // 5. Floating "Wave X Cleared!" text.
            SpawnWaveText(waveNumber, origin);

            // 6. Camera shake.
            float shakeIntensity = mobile ? 0.25f : 0.42f;
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

        private void SpawnWaveText(int waveNumber, Vector3 origin)
        {
            string text = $"Wave {waveNumber} Cleared!";

            if (_waveTextPrefab != null)
            {
                Vector3 spawnPos = origin + Vector3.up * 2.5f;
                var obj = Instantiate(_waveTextPrefab, spawnPos, Quaternion.identity);

                // Try TextMeshPro (TMPro.TMP_Text via GetComponent — no hard TMPro dep).
                var tmp = obj.GetComponent("TMP_Text") as UnityEngine.UI.Text
                       ?? obj.GetComponent<UnityEngine.UI.Text>();
                if (tmp != null) tmp.text = text;
                // TMPro path via reflection so no hard assembly reference is needed.
                else
                {
                    var comp = obj.GetComponent("TextMeshPro")
                            ?? obj.GetComponentInChildren<Component>();
                    if (comp != null)
                    {
                        var textProp = comp.GetType().GetProperty("text");
                        textProp?.SetValue(comp, text);
                    }
                }

                Destroy(obj, 2.5f);
            }
            else
            {
                // IMGUI toast fallback — visible without a prefab in builds.
                _toastText  = text;
                _toastTimer = 2.5f;
            }
        }

        // ── IMGUI toast fallback ──────────────────────────────────────────────

        private void Update()
        {
            if (_toastTimer > 0f) _toastTimer -= Time.unscaledDeltaTime;
        }

        private void OnGUI()
        {
            if (_toastTimer <= 0f || string.IsNullOrEmpty(_toastText)) return;

            float alpha = Mathf.Clamp01(_toastTimer);   // fade out in last second
            GUI.color = new Color(1f, 0.95f, 0.5f, alpha);

            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 28,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal    = { textColor = new Color(1f, 0.95f, 0.5f, alpha) }
            };

            float w = Screen.width;
            float h = Screen.height;
            GUI.Label(new Rect(0f, h * 0.3f, w, 50f), _toastText, style);
            GUI.color = Color.white;
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
            if (FindObjectOfType<WaveManager>() == null) return;
            if (Instance != null) return;

            var go  = new GameObject("[WaveCelebrationManager]");
            var mgr = go.AddComponent<WaveCelebrationManager>();

            // Wire to WaveManager's OnWaveCleared event.
            var wave = FindObjectOfType<WaveManager>();
            if (wave != null)
                wave.OnWaveCleared.AddListener(mgr.PlayWaveClear);

            Debug.Log("[WaveCelebrationManager] Installed and wired to WaveManager.");
        }
    }
}
