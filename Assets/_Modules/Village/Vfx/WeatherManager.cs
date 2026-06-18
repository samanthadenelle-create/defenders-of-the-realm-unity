// =============================================================================
// WeatherManager — shooting stars, rain, and extensible atmosphere. DEF-VFX-05.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// WHAT IT DOES:
//   • Shooting Stars — randomised bright streaks across the night sky, pooled,
//     with occasional dramatic "super-star" variants.
//   • Rain — toggleable, intensity-scaled rain streaks + ground splashes.
//   • Extensible — easy to add Snow, Fog, Leaf, Wind particles later.
//
// QUICK REFERENCE:
//   WeatherManager.Instance.ToggleRain(true);
//   WeatherManager.Instance.SetRainIntensity(0.7f);
//   WeatherManager.Instance.SpawnShootingStar();          // manual trigger
//   WeatherManager.Instance.SetWeatherQuality(VFXQuality.Low);
//
// SETUP — required prefabs (create these in the Unity editor):
//
//   ShootingStarPrefab:
//     • Root GO with ParticleSystem (Duration=1, Loop=false, StopAction=Destroy)
//     • Main: StartLifetime=0.4-0.8, StartSpeed=60-120, StartSize=0.08-0.18
//     • Shape: Cone, Angle=0, Radius=0.01 (one tight beam direction)
//     • Renderer: Stretch, VelocityScale=0.02, LengthScale=4.0
//     • Color over Lifetime: white core → pale yellow → transparent
//     • Trails module enabled: Lifetime=0.15 relative
//     • Optional: add a Light (Intensity=3, Range=5) with VfxLightFade (FadeTime=0.3)
//
//   RainPrefab:
//     • Particle System (Duration=∞, Loop=true)
//     • Main: StartLifetime=0.5-0.8, StartSpeed=20-28, StartSize=0.03-0.06
//     • Gravity: 1.5  Simulation: World
//     • Shape: Box (Width=40, Height=0, Length=40) positioned ~25m above origin
//     • Renderer: Stretch, VelocityScale=0.03, LengthScale=3.0, Alpha=0.5
//     • Color: near-white with blue tint, alpha 0.4-0.6
//
//   RainSplashPrefab:
//     • Particle System (Duration=∞, Loop=true)
//     • Main: StartLifetime=0.2-0.4, StartSpeed=0.5-2, StartSize=0.05-0.15
//     • Shape: Box (Width=40, Length=40) at ground level (Y=0)
//     • Emission: rateOverTime scales with RainIntensity × MaxSplashRate
//     • Renderer: Billboard, semi-transparent ring texture (or soft dot)
//
// ASSIGN the three prefabs in the Inspector on the WeatherManager GameObject.
// The system works WITHOUT prefabs (procedural fallback), but the prefabs look
// dramatically better — wire them in as soon as they exist.
// =============================================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DeNelle.Village
{
    // ── Weather quality (mirrors VFXQuality for consistency) ─────────────────

    // WeatherManager uses VFXQuality directly (Low/Medium/High) so there is a
    // single quality setting in the game rather than two separate ones.

    // ── WeatherManager ────────────────────────────────────────────────────────

    /// <summary>
    /// Singleton weather system. Manages shooting stars, rain, and future
    /// atmospheric effects. DontDestroyOnLoad — add to the boot scene.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WeatherManager : MonoBehaviour
    {
        // ── Singleton ─────────────────────────────────────────────────────────

        public static WeatherManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitialisePools();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // =====================================================================
        // ── INSPECTOR ─────────────────────────────────────────────────────────
        // =====================================================================

        [Header("Prefabs (null = procedural fallback)")]
        [Tooltip("Shooting star prefab. See file header for setup instructions.")]
        [SerializeField] private GameObject _shootingStarPrefab;

        [Tooltip("Rain particle system prefab (looping, large area).")]
        [SerializeField] private ParticleSystem _rainPrefab;

        [Tooltip("Ground rain splash particle system prefab (looping, ground level).")]
        [SerializeField] private ParticleSystem _rainSplashPrefab;

        [Header("Shooting Stars")]
        [Tooltip("Minimum seconds between automatic shooting stars.")]
        [SerializeField, Min(1f)] private float _starIntervalMin = 8f;

        [Tooltip("Maximum seconds between automatic shooting stars (0 = disabled — manual only).")]
        [SerializeField, Min(0f)] private float _starIntervalMax = 25f;

        [Tooltip("Probability that a shooting star is a bright 'super star' (0-1).")]
        [SerializeField, Range(0f, 1f)] private float _superStarChance = 0.12f;

        [Tooltip("Sky dome radius from which stars spawn. " +
                 "Stars appear to travel across the sky when spawned at the rim.")]
        [SerializeField, Min(10f)] private float _skyRadius = 80f;

        [Tooltip("Height above the camera at which stars spawn.")]
        [SerializeField, Min(5f)] private float _skyHeight = 45f;

        [Tooltip("Maximum pool size for shooting star instances.")]
        [SerializeField, Min(1)] private int _starPoolSize = 6;

        [Header("Rain")]
        [Tooltip("Rain is enabled at startup.")]
        [SerializeField] private bool _rainOnStart = false;

        [Tooltip("0 = light drizzle, 1 = heavy downpour.")]
        [SerializeField, Range(0f, 1f)] private float _rainIntensity = 0.5f;

        [Tooltip("Maximum particles/second for the rain system at intensity 1.0.")]
        [SerializeField, Min(10f)] private float _maxRainRate = 600f;

        [Tooltip("Maximum particles/second for ground splashes at intensity 1.0.")]
        [SerializeField, Min(5f)] private float _maxSplashRate = 120f;

        [Tooltip("The rain area box follows the main camera so rain always covers the player's view.")]
        [SerializeField] private bool _rainFollowCamera = true;

        [Header("Quality")]
        [Tooltip("Weather is disabled below this quality level " +
                 "(e.g. Low = no weather at all on budget devices).")]
        [SerializeField] private VFXQuality _minQuality = VFXQuality.Low;

        [Header("Audio (placeholder — wire AudioSource here when audio is live)")]
        [Tooltip("Optional AudioSource on this GameObject for rain ambient sound.")]
        [SerializeField] private AudioSource _rainAudio;

        // =====================================================================
        // ── RUNTIME STATE ─────────────────────────────────────────────────────
        // =====================================================================

        private VFXQuality _currentQuality = VFXQuality.High;

        // Shooting stars
        private readonly Queue<GameObject> _starPool = new Queue<GameObject>();
        private Coroutine _starRoutine;

        // Rain
        private ParticleSystem _rain;
        private ParticleSystem _splash;
        private bool           _rainActive;

        // Camera ref (for rain follow)
        private Camera _mainCam;

        // =====================================================================
        // ── PUBLIC API ────────────────────────────────────────────────────────
        // =====================================================================

        // ── Quality ───────────────────────────────────────────────────────────

        /// <summary>Change weather quality at runtime (e.g. from the Settings screen).</summary>
        public void SetWeatherQuality(VFXQuality q)
        {
            _currentQuality = q;
            bool ok = IsQualityOk();

            // Re-apply rain state.
            if (_rainActive && !ok) ApplyRainState(active: false);
            else if (_rainActive && ok) ApplyRainState(active: true);

            // Restart / stop the shooting star loop.
            if (_starRoutine != null) StopCoroutine(_starRoutine);
            if (ok && _starIntervalMax > 0f)
                _starRoutine = StartCoroutine(ShootingStarLoop());
        }

        // ── Shooting Stars ────────────────────────────────────────────────────

        /// <summary>
        /// Manually spawn one shooting star right now. Useful for dramatic moments
        /// (boss intro, wave clear, scripted cinematic beats).
        /// </summary>
        public void SpawnShootingStar(bool forceSuperStar = false)
        {
            if (!IsQualityOk()) return;
            bool isSuperStar = forceSuperStar || Random.value < _superStarChance;
            SpawnStar(isSuperStar);
        }

        /// <summary>True if the automatic shooting-star loop is running.</summary>
        public bool ShootingStarsActive => _starRoutine != null;

        /// <summary>Start or stop the automatic shooting-star loop.</summary>
        public void SetShootingStars(bool enabled)
        {
            if (_starRoutine != null) { StopCoroutine(_starRoutine); _starRoutine = null; }
            if (enabled && IsQualityOk() && _starIntervalMax > 0f)
                _starRoutine = StartCoroutine(ShootingStarLoop());
        }

        // ── Rain ──────────────────────────────────────────────────────────────

        /// <summary>Turn rain on or off.</summary>
        public void ToggleRain(bool active)
        {
            _rainActive = active;
            if (IsQualityOk()) ApplyRainState(active);
        }

        /// <summary>
        /// Adjust rain intensity (0 = light drizzle, 1 = heavy downpour).
        /// Takes effect immediately whether rain is on or off.
        /// </summary>
        public void SetRainIntensity(float intensity)
        {
            _rainIntensity = Mathf.Clamp01(intensity);
            if (_rainActive && IsQualityOk()) UpdateRainEmission();
        }

        /// <summary>True if rain is currently active and playing.</summary>
        public bool RainActive => _rainActive;

        /// <summary>Current rain intensity (0-1).</summary>
        public float RainIntensity => _rainIntensity;

        // ── Integration hooks ─────────────────────────────────────────────────

        /// <summary>
        /// Trigger dramatic rain for a boss fight. Ramps intensity to 0.9 and
        /// starts rain if it was off. Call StopBossRain() to revert.
        /// </summary>
        public void StartBossRain()
        {
            ToggleRain(true);
            SetRainIntensity(0.9f);
        }

        /// <summary>Revert after a boss fight — fades back to previous intensity.</summary>
        public void StopBossRain()
        {
            StartCoroutine(FadeRainOut(2.5f));
        }

        /// <summary>
        /// Wave clear celebration — suppress weather briefly for a clear sky effect
        /// while the celebration VFX plays, then restore.
        /// </summary>
        public void OnWaveClear()
        {
            if (!_rainActive) return;
            StartCoroutine(PauseWeather(3f));
        }

        // =====================================================================
        // ── LIFECYCLE ─────────────────────────────────────────────────────────
        // =====================================================================

        private void Start()
        {
            _mainCam = Camera.main;

            // Shooting stars loop (if auto-interval is set).
            if (IsQualityOk() && _starIntervalMax > 0f)
                _starRoutine = StartCoroutine(ShootingStarLoop());

            // Rain startup.
            if (_rainOnStart) ToggleRain(true);
        }

        private void LateUpdate()
        {
            // Keep rain centered on camera so rain always covers the viewport.
            if (_rainFollowCamera && _rain != null && _mainCam != null)
            {
                Vector3 camPos = _mainCam.transform.position;
                _rain.transform.position = new Vector3(camPos.x, camPos.y + 25f, camPos.z);
                if (_splash != null)
                    _splash.transform.position = new Vector3(camPos.x, 0f, camPos.z);
            }
        }

        // =====================================================================
        // ── SHOOTING STARS ─────────────────────────────────────────────────────
        // =====================================================================

        private IEnumerator ShootingStarLoop()
        {
            while (true)
            {
                float wait = Random.Range(_starIntervalMin, _starIntervalMax);
                yield return new WaitForSeconds(wait);

                if (!IsQualityOk()) continue;
                bool superStar = Random.value < _superStarChance;
                SpawnStar(superStar);
            }
        }

        private void SpawnStar(bool superStar)
        {
            var go = AcquireStar();
            if (go == null) return;

            // Position: random point on the top hemisphere of the sky sphere.
            Vector3 origin = PickSkySpawnPoint();
            go.transform.position = origin;

            // Direction: diagonally downward — shooting stars travel at a steep angle.
            float angleH = Random.Range(-35f, 35f);   // horizontal spread
            float angleV = Random.Range(-60f, -40f);  // always going down
            go.transform.rotation = Quaternion.Euler(angleV, angleH, 0f);

            go.SetActive(true);

            // Scale and brightness based on star type.
            float scale = superStar ? Random.Range(1.6f, 2.4f) : Random.Range(0.7f, 1.2f);
            go.transform.localScale = Vector3.one * scale;

            // If using a procedural star, build it. If using the prefab, it plays itself.
            bool hasPrefab = _shootingStarPrefab != null;
            if (!hasPrefab) BuildProceduralStar(go, superStar);

            float lifetime = superStar ? 1.8f : 1.1f;
            StartCoroutine(ReturnStarAfter(go, lifetime));
        }

        private Vector3 PickSkySpawnPoint()
        {
            // Random point on a wide arc above and around the scene.
            float azimuth  = Random.Range(0f, 360f);
            float elevRad  = Mathf.Deg2Rad * Random.Range(30f, 70f);
            Vector3 camPos = _mainCam != null ? _mainCam.transform.position : Vector3.zero;

            return camPos + new Vector3(
                _skyRadius * Mathf.Cos(elevRad) * Mathf.Sin(Mathf.Deg2Rad * azimuth),
                _skyHeight + _skyRadius * Mathf.Sin(elevRad),
                _skyRadius * Mathf.Cos(elevRad) * Mathf.Cos(Mathf.Deg2Rad * azimuth));
        }

        // ── Star pool ─────────────────────────────────────────────────────────

        private void InitialisePools()
        {
            if (_shootingStarPrefab == null) return;

            // Pre-warm the star pool.
            for (int i = 0; i < Mathf.Min(_starPoolSize, 3); i++)
                _starPool.Enqueue(CreatePooledStar());
        }

        private GameObject CreatePooledStar()
        {
            if (_shootingStarPrefab != null)
            {
                var go = Instantiate(_shootingStarPrefab, transform);
                go.SetActive(false);
                go.name = "[ShootingStar]";
                return go;
            }

            // Procedural shell — particles built in SpawnStar.
            var shell = new GameObject("[ShootingStar_Proc]");
            shell.transform.SetParent(transform);
            shell.SetActive(false);
            return shell;
        }

        private GameObject AcquireStar()
        {
            if (_starPool.Count > 0)
            {
                var reused = _starPool.Dequeue();
                reused.transform.SetParent(null);
                return reused;
            }
            // Pool empty but below max? Instantiate one more.
            if (_starPool.Count < _starPoolSize)
                return CreatePooledStar();

            return null;   // max concurrent stars reached
        }

        private IEnumerator ReturnStarAfter(GameObject go, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (go == null) yield break;
            // Stop any particles before pooling.
            foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true))
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            go.transform.SetParent(transform);
            go.SetActive(false);
            _starPool.Enqueue(go);
        }

        // ── Procedural star builder (fallback when no prefab is assigned) ──────

        private static void BuildProceduralStar(GameObject host, bool superStar)
        {
            // Remove any previous procedural star children.
            for (int i = host.transform.childCount - 1; i >= 0; i--)
                Destroy(host.transform.GetChild(i).gameObject);

            Color core   = Color.white;
            Color body   = new Color(0.95f, 0.97f, 1.00f, 1f);
            Color tail   = new Color(0.75f, 0.82f, 1.00f, 0f);
            int   count  = superStar ? 18 : 9;
            float speed  = superStar ? Random.Range(90f, 130f) : Random.Range(55f, 80f);
            float size   = superStar ? Random.Range(0.12f, 0.20f) : Random.Range(0.06f, 0.10f);

            // Main streak — stretched billboard in the travel direction.
            var streak = new GameObject("Streak"); streak.transform.SetParent(host.transform, false);
            var ps = streak.AddComponent<ParticleSystem>();
            var m  = ps.main;
            m.loop = false; m.playOnAwake = false; m.duration = 0.5f;
            m.startLifetime  = new ParticleSystem.MinMaxCurve(0.35f, 0.6f);
            m.startSpeed     = new ParticleSystem.MinMaxCurve(speed * 0.9f, speed * 1.1f);
            m.startSize      = new ParticleSystem.MinMaxCurve(size * 0.8f, size * 1.2f);
            m.gravityModifier = 0f;
            m.simulationSpace = ParticleSystemSimulationSpace.World;

            // Shape: very tight cone in the forward direction.
            var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Cone;
            sh.angle = 0.5f; sh.radius = 0.01f;

            // Burst.
            ps.emission.SetBursts(new[] { new ParticleSystem.Burst(0f, count) });

            // Colour: white core → blue-white → transparent tail.
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(core, 0f), new GradientColorKey(body, 0.5f), new GradientColorKey(tail, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.05f), new GradientAlphaKey(0.8f, 0.5f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(g);

            // Renderer: stretched streak so it reads as a bright line, not a dot.
            var r = streak.GetComponent<ParticleSystemRenderer>();
            if (r != null)
            {
                r.renderMode   = ParticleSystemRenderMode.Stretch;
                r.velocityScale = 0.015f;
                r.lengthScale   = superStar ? 5f : 3.5f;
                // WO-420: logged resolve, never the URP magenta default.
                AbilityVfxKit.ApplyParticleMaterial(r);
            }

            // Glow: a short-lived bright flash at the star's head.
            var glow = new GameObject("Glow"); glow.transform.SetParent(host.transform, false);
            var gps  = glow.AddComponent<ParticleSystem>();
            var gm   = gps.main;
            gm.loop = false; gm.playOnAwake = false; gm.duration = 0.15f;
            gm.startLifetime = 0.15f; gm.startSpeed = 0f;
            gm.startSize = superStar ? size * 4f : size * 2.5f;
            gm.startColor = new ParticleSystem.MinMaxGradient(core);
            gm.simulationSpace = ParticleSystemSimulationSpace.World;
            var gem = gps.emission; gem.SetBursts(new[] { new ParticleSystem.Burst(0f, 2) });
            var gsh = gps.shape; gsh.enabled = false;
            var gcol = gps.colorOverLifetime; gcol.enabled = true;
            var gg = new Gradient();
            gg.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(body, 1f) },
                new[] { new GradientAlphaKey(superStar ? 0.9f : 0.6f, 0f), new GradientAlphaKey(0f, 1f) });
            gcol.color = new ParticleSystem.MinMaxGradient(gg);
            var gr = glow.GetComponent<ParticleSystemRenderer>();
            if (gr != null)
            {
                gr.renderMode = ParticleSystemRenderMode.Billboard;
                AbilityVfxKit.ApplyParticleMaterial(gr);   // WO-420
            }

            // Optional URP point light at the glow position for extra sparkle.
            if (superStar)
            {
                var lg = new GameObject("StarLight"); lg.transform.SetParent(host.transform, false);
                var l = lg.AddComponent<Light>();
                l.type = LightType.Point; l.color = new Color(0.9f, 0.93f, 1f);
                l.intensity = 3f; l.range = 8f; l.shadows = LightShadows.None;
                // Fade out using the existing VfxLightFade helper.
                lg.AddComponent<VfxLightFade>().FadeTime = 0.5f;
            }

            // Play all systems.
            foreach (var p in host.GetComponentsInChildren<ParticleSystem>(true))
                p.Play();
        }

        // =====================================================================
        // ── RAIN ─────────────────────────────────────────────────────────────
        // =====================================================================

        private void ApplyRainState(bool active)
        {
            EnsureRainSystems();

            if (_rain != null)
            {
                if (active) { if (!_rain.isPlaying) _rain.Play(); UpdateRainEmission(); }
                else        { _rain.Stop(true, ParticleSystemStopBehavior.StopEmitting); }
            }

            if (_splash != null)
            {
                if (active) { if (!_splash.isPlaying) _splash.Play(); UpdateSplashEmission(); }
                else        { _splash.Stop(true, ParticleSystemStopBehavior.StopEmitting); }
            }

            // Audio.
            if (_rainAudio != null)
            {
                if (active && !_rainAudio.isPlaying)
                {
                    _rainAudio.volume = 0f;
                    _rainAudio.Play();
                    StartCoroutine(FadeAudio(_rainAudio, 0f, _rainIntensity * 0.6f, 1.5f));
                }
                else if (!active && _rainAudio.isPlaying)
                {
                    StartCoroutine(FadeAudio(_rainAudio, _rainAudio.volume, 0f, 1.5f,
                        () => _rainAudio.Stop()));
                }
            }
        }

        private void UpdateRainEmission()
        {
            if (_rain == null) return;
            var em = _rain.emission;
            em.rateOverTime = _maxRainRate * _rainIntensity;
            UpdateSplashEmission();
        }

        private void UpdateSplashEmission()
        {
            if (_splash == null) return;
            var em = _splash.emission;
            em.rateOverTime = _maxSplashRate * _rainIntensity;
        }

        private void EnsureRainSystems()
        {
            // Use assigned prefabs or create procedural fallbacks.
            if (_rain == null)
            {
                if (_rainPrefab != null)
                    _rain = Instantiate(_rainPrefab, transform);
                else
                    _rain = BuildProceduralRain();
            }

            if (_splash == null)
            {
                if (_rainSplashPrefab != null)
                    _splash = Instantiate(_rainSplashPrefab, transform);
                else
                    _splash = BuildProceduralSplash();
            }

            // Initial emission is 0 — ApplyRainState drives it.
            var re = _rain.emission; re.rateOverTime = 0f;
            var se = _splash.emission; se.rateOverTime = 0f;
        }

        // ── Procedural rain fallbacks ─────────────────────────────────────────

        private ParticleSystem BuildProceduralRain()
        {
            var go = new GameObject("[ProceduralRain]"); go.transform.SetParent(transform);
            go.transform.localPosition = new Vector3(0f, 25f, 0f);

            var ps = go.AddComponent<ParticleSystem>();
            var m  = ps.main;
            m.loop = true; m.playOnAwake = false;
            m.startLifetime  = new ParticleSystem.MinMaxCurve(0.5f, 0.75f);
            m.startSpeed     = new ParticleSystem.MinMaxCurve(20f, 28f);
            m.startSize      = new ParticleSystem.MinMaxCurve(0.03f, 0.055f);
            m.gravityModifier = 1.5f;
            m.startColor     = new ParticleSystem.MinMaxGradient(new Color(0.75f, 0.82f, 1f, 0.5f));
            m.maxParticles   = 2000;
            m.simulationSpace = ParticleSystemSimulationSpace.World;

            var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Box;
            sh.scale = new Vector3(40f, 0.1f, 40f);

            var em = ps.emission; em.rateOverTime = 0f;

            var r = go.GetComponent<ParticleSystemRenderer>();
            if (r != null)
            {
                r.renderMode    = ParticleSystemRenderMode.Stretch;
                r.velocityScale = 0.025f;
                r.lengthScale   = 2.5f;
                AbilityVfxKit.ApplyParticleMaterial(r);   // WO-420
            }
            return ps;
        }

        private ParticleSystem BuildProceduralSplash()
        {
            var go = new GameObject("[ProceduralSplash]"); go.transform.SetParent(transform);
            go.transform.localPosition = Vector3.zero;   // ground level

            var ps = go.AddComponent<ParticleSystem>();
            var m  = ps.main;
            m.loop = true; m.playOnAwake = false;
            m.startLifetime  = new ParticleSystem.MinMaxCurve(0.2f, 0.4f);
            m.startSpeed     = new ParticleSystem.MinMaxCurve(0.5f, 1.8f);
            m.startSize      = new ParticleSystem.MinMaxCurve(0.06f, 0.14f);
            m.startColor     = new ParticleSystem.MinMaxGradient(new Color(0.8f, 0.87f, 1f, 0.6f));
            m.gravityModifier = 0.3f;
            m.maxParticles   = 400;
            m.simulationSpace = ParticleSystemSimulationSpace.World;

            var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Box;
            sh.scale = new Vector3(40f, 0.1f, 40f);

            var em = ps.emission; em.rateOverTime = 0f;

            var sol = ps.sizeOverLifetime; sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0f, 1f, 1f));

            var r = go.GetComponent<ParticleSystemRenderer>();
            if (r != null)
            {
                r.renderMode = ParticleSystemRenderMode.Billboard;
                AbilityVfxKit.ApplyParticleMaterial(r);   // WO-420
            }
            return ps;
        }

        // =====================================================================
        // ── COROUTINE HELPERS ─────────────────────────────────────────────────
        // =====================================================================

        private IEnumerator FadeRainOut(float duration)
        {
            float start = _rainIntensity;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                SetRainIntensity(Mathf.Lerp(start, 0f, t / duration));
                yield return null;
            }
            ToggleRain(false);
        }

        private IEnumerator PauseWeather(float duration)
        {
            float savedIntensity = _rainIntensity;
            // Fade out quickly.
            yield return FadeRainOut(0.8f);
            yield return new WaitForSeconds(duration);
            // Restore.
            ToggleRain(true);
            SetRainIntensity(savedIntensity);
        }

        private static IEnumerator FadeAudio(AudioSource src, float from, float to,
                                             float duration, System.Action onComplete = null)
        {
            float t = 0f;
            while (t < duration && src != null)
            {
                t += Time.deltaTime;
                src.volume = Mathf.Lerp(from, to, t / duration);
                yield return null;
            }
            if (src != null) src.volume = to;
            onComplete?.Invoke();
        }

        // ── Quality check ─────────────────────────────────────────────────────

        private bool IsQualityOk()
            => (int)_currentQuality >= (int)_minQuality;
    }
}
