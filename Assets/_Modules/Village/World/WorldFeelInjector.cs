// =============================================================================
// WorldFeelInjector -- runtime, NON-DESTRUCTIVE world-aesthetics pass for the
// outdoor scenes (MainCastle_Hall / OuterWorld / Village2). FIRST-PASS BONES
// the owner finesses by eye.
// -----------------------------------------------------------------------------
// Owner felt-test verdict (2026-07-01): "It doesn't feel polished. World feels
// empty. Very flat -- I want TERRAIN. At least maybe some aesthetics."
// Screenshots: near-BLACK empty sky in every shot, cold dead ambient, no
// atmosphere.
//
// ROOT CAUSE (captured data, CLAUDE.md 12 -- not a guess):
//   * MainCastle_Hall.unity line 7008: the hub camera ships m_ClearFlags: 2
//     (SolidColor) with m_BackGroundColor (0.16, 0.17, 0.19) -- a near-black
//     charcoal. The camera CLEARS TO SOLID DARK GREY, so no skybox ever draws.
//     That IS the "black void sky" in every owner screenshot.
//   * FloorDiag: ambient RGBA(0.212, 0.227, 0.259) -- cold, dead blue-grey.
//   * Settings/DeNelle-UniversalRenderer.asset line 31: postProcessData
//     {fileID: 0} -- URP post-processing was structurally DISABLED project-wide
//     (fixed in the same change by assigning the default URP PostProcessData).
//
// WHAT IT DOES (on every outdoor-scene activation, idempotent):
//   1. CAMERA  -- forces Camera.main clearFlags to Skybox (the black-sky fix).
//   2. SKYBOX  -- a dusk "hold the last light" procedural skybox (warm amber
//      horizon, deep blue zenith, low warm sun disc) built in code; ambient +
//      reflection probes refreshed from it (DynamicGI.UpdateEnvironment).
//   3. SUN     -- the scene's directional light re-aimed to a low warm dusk
//      angle; registered as RenderSettings.sun so the skybox sun disc tracks it.
//   4. AMBIENT -- warm trilight ambient (replaces the cold dead 0.21/0.23/0.26).
//   5. FOG     -- gentle warm exponential-squared haze so the horizon reads
//      soft instead of empty.
//   6. POST    -- a global URP Volume built in code: subtle Bloom (torch/tree
//      aura pop), gentle Vignette, slight warm ColorAdjustments grade. The
//      camera's renderPostProcessing is switched on. Priority 10 -- far below
//      the BattleArena's fight-local bloom volume (priority 100), so the arena
//      look still wins during combat.
//   7. MOTES   -- a cheap drifting-dust ParticleSystem that follows the camera
//      in the OPEN WORLD scenes only (OuterWorld), so the air reads alive.
//
// WHY A RUNTIME INJECTOR (not a scene edit) -- same rationale as
//   HubAmbientVfxInjector: re-saving .unity files carries the scene-resave
//   corruption risk (CLAUDE.md 3), so this self-bootstrapping DDOL singleton
//   applies RenderSettings + camera state at runtime and never touches a scene
//   file. Fully reversible: PlayerPrefs "ff.worldfeel" = 0 restores the exact
//   prior look with no rebuild.
//
// SCOPE / LANDMINES HONOURED:
//   * Dungeons keep their own authored mood -- the allowlist below covers only
//     the outdoor scenes.
//   * SkyProgressionController (DEF-66 wave darkening) is attached in NO scene
//     (verified: its script guid appears in no .unity) -- nothing fights this.
//   * NightTorchLightSystem only RAISES a low ambient floor at night; a bright
//     dusk ambient keeps it dormant (lum above its DayLum threshold).
//   * BattleArena saves/restores camera post-fx state around fights; its
//     restore writes back the value THIS injector set -- consistent.
//
// Village -> Core only (FeatureFlags + FlowTrace/Guard). URP types come from
// Unity.RenderPipelines.Universal.Runtime, already referenced by
// DeNelle.Village.asmdef (BattleArena precedent). ASCII only.
// =============================================================================

using DeNelle.Core;
using DeNelle.Core.Diagnostics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace DeNelle.Village.World
{
    /// <summary>Runtime, non-destructive world-aesthetics pass: dusk skybox, warm ambient,
    /// haze fog, subtle post grade, and open-world ambient motes on the outdoor scenes.</summary>
    public sealed class WorldFeelInjector : MonoBehaviour
    {
        public static WorldFeelInjector Instance { get; private set; }

        private const string HolderName = "WorldFeel (runtime)";

        // =====================================================================
        //  TUNABLES -- the owner dials these by eye. All plain consts (no
        //  inspector drag-drop, per the never-dragdrop rule). Colours RGBA 0..1.
        // =====================================================================

        // ---- Scene allowlist (outdoor scenes only -- dungeons keep their mood) ----
        // WO-608: the merged single scene (Main_Castle_Overworld, ff.MergedWorld) is the
        // outdoor home hub too — it must get the same dusk world-feel pass. Safe when the
        // flag's OFF (that scene never loads on the legacy path).
        private static readonly string[] OutdoorScenes = { "MainCastle_Hall", "Village2", "Main_Castle_Overworld" };

        // ---- (2) SKYBOX -- dusk, "hold the last light" ------------------------
        // Procedural skybox: warm glowing horizon (the last light), deep blue
        // zenith, a visible low sun disc. Exposure lifts the whole sky out of
        // the murk.
        private const float SkySunSize        = 0.05f;
        private const float SkySunConvergence = 4.5f;
        private const float SkyAtmosphere     = 1.25f;   // horizon warmth thickness
        private static readonly Color SkyZenithTint  = new Color(0.42f, 0.50f, 0.72f); // deep dusk blue
        private static readonly Color SkyGroundColor = new Color(0.86f, 0.62f, 0.44f); // warm amber horizon
        // BRIGHTNESS LIFT (tester feedback 2026-07-16 "way too dark, even day too dim"; measured
        // baseline avg luminance 24%). The dusk "hold the last light" mood was genuinely under-lit on
        // device. Raised sky/sun/ambient AND added a global post-exposure lift (below) so EVERY
        // time-of-day brightens, not just this state.
        private const float SkyExposure       = 1.55f;   // was 1.25

        // ---- (3) SUN -- low, warm, long shadows -------------------------------
        private const float SunPitchDeg   = 24f;    // low above the horizon = long dusk shadows
        private const float SunYawDeg     = -38f;
        private static readonly Color SunColor = new Color(1.00f, 0.84f, 0.64f); // warm gold
        private const float SunIntensity  = 1.75f;   // was 1.15 (+52%)

        // ---- (4) AMBIENT -- warm trilight (replaces the cold dead grey) -------
        private static readonly Color AmbientSky     = new Color(0.66f, 0.70f, 0.86f);  // was 0.46/0.50/0.66
        private static readonly Color AmbientEquator = new Color(0.82f, 0.72f, 0.62f);  // was 0.62/0.52/0.44
        private static readonly Color AmbientGround  = new Color(0.46f, 0.42f, 0.36f);  // was 0.30/0.26/0.22

        // ---- (5) FOG -- gentle warm haze ---------------------------------------
        private static readonly Color FogColor = new Color(0.78f, 0.66f, 0.58f);  // warm dusk haze
        private const float FogDensity = 0.0012f;  // soft horizon; play space stays crisp

        // ---- (6) POST -- subtle global grade -----------------------------------
        private const float PostVolumePriority = 10f;   // below BattleArena's 100
        // WO-678 (2026-07-12): raised from 0.45 to demo parity. The Hovl RPG VFX
        // Bundle demo scenes run Bloom intensity 5 / threshold 1.1 (verified in the
        // pack's VolumeURP.asset, docs/HOVL_STUDIO_SME.md) -- at 0.45 every spell
        // rendered glow-less ("not like the demo"). 4.5 = demo range, owner dials.
        // Threshold 1.1 keeps the glow on HDR-bright VFX cores, off ordinary albedo.
        private const float BloomIntensity     = 4.5f;
        private const float BloomThreshold     = 1.1f;
        private const float BloomScatter       = 0.7f;
        private const float VignetteIntensity  = 0.10f; // was 0.22 — less frame darkening (brightness pass)
        private const float VignetteSmoothness = 0.42f;
        private const float GradeSaturation    = 10f;   // +10 lifts the single-tone flatness
        private const float GradeContrast      = 8f;
        // Global post-exposure (EV) — the single "everything brighter" knob; lifts the whole rendered
        // image at ALL times of day (independent of the sun/skybox state). +0.75 EV ~ 1.68x.
        private const float GradePostExposure  = 0.75f;
        private static readonly Color GradeFilter = new Color(1.00f, 0.97f, 0.92f); // faint warm filter

        // ---- (7) MOTES -- drifting dust/pollen around the camera (open world) --
        private const float MotesRadius     = 14f;   // emit shell around the camera (m)
        private const float MotesRate       = 8f;    // particles / second (cheap)
        private const float MotesLifetime   = 7f;
        private const float MotesDriftSpeed = 0.35f;
        private const float MotesSizeMin    = 0.05f;
        private const float MotesSizeMax    = 0.14f;
        private const int   MotesMax        = 80;    // hard cap
        private static readonly Color MotesColor = new Color(1.0f, 0.92f, 0.72f, 0.35f); // warm pollen glint

        // =====================================================================

        private Material _skyboxMat;          // built once, reused across scene loads
        private VolumeProfile _postProfile;   // built once, reused
        private GameObject _motes;            // follows the camera in the open world

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            new GameObject(nameof(WorldFeelInjector)).AddComponent<WorldFeelInjector>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
            TryApply();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        }

        // RenderSettings always come from the ACTIVE scene, so re-apply whenever
        // the active scene changes OR a load lands while an outdoor scene is active
        // (an additive load can bring in a new camera).
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => TryApply();
        private void OnActiveSceneChanged(Scene from, Scene to) => TryApply();

        private static bool IsOutdoor(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) return false;
            if (DeNelle.Core.HubScenes.IsOverworld(sceneName)) return true;  // WO-608: merged overworld
            for (int i = 0; i < OutdoorScenes.Length; i++)
                if (sceneName == OutdoorScenes[i]) return true;
            return false;
        }

        private void TryApply()
        {
            string active = SceneManager.GetActiveScene().name;
            if (!IsOutdoor(active))
            {
                ClearMotes();
                return;
            }

            if (!FeatureFlags.WorldFeel)
            {
                FlowTrace.Step("WorldFeel", "ff.worldfeel OFF -- world aesthetics pass skipped (prior look preserved).");
                ClearMotes();
                return;
            }

            bool sky = false, post = false;
            Guard.Try("WorldFeel", "apply sky/sun/ambient/fog", () => { ApplySkySunAmbientFog(); sky = true; });
            Guard.Try("WorldFeel", "apply post volume", () => { ApplyPostVolume(); post = true; });
            Guard.Try("WorldFeel", "apply ambient motes", ApplyMotes);

            FlowTrace.Step("WorldFeel",
                $"scene='{active}' skybox={(sky ? "dusk-procedural" : "FAILED")} " +
                $"ambient=warm-trilight({AmbientEquator.r:0.00},{AmbientEquator.g:0.00},{AmbientEquator.b:0.00}) " +
                $"post={(post ? "on" : "FAILED")} motes={(_motes != null ? "on" : "off")}");
        }

        // ---- (1)-(5) camera clear + skybox + sun + ambient + fog ---------------
        private void ApplySkySunAmbientFog()
        {
            // (1) CAMERA: the proven black-sky root -- hub camera ships
            // SolidColor near-black. Force Skybox clear so the sky draws at all.
            var cam = Camera.main;
            if (cam != null && cam.clearFlags != CameraClearFlags.Skybox)
            {
                FlowTrace.Step("WorldFeel",
                    $"camera '{cam.name}' clearFlags {cam.clearFlags} -> Skybox (was clearing to solid {cam.backgroundColor}).");
                cam.clearFlags = CameraClearFlags.Skybox;
            }

            // (2) SKYBOX: dusk procedural. Shader ships in builds (OuterWorld's
            // baked AvalonDawnSkybox.mat references it), but null-guard anyway.
            if (_skyboxMat == null)
            {
                var shader = Shader.Find("Skybox/Procedural");
                if (shader == null)
                {
                    FlowTrace.Warn("WorldFeel", "Skybox/Procedural shader missing (stripped?) -- sky left as-is; ambient/fog still applied.");
                }
                else
                {
                    _skyboxMat = new Material(shader) { name = "WorldFeel_DuskSkybox (runtime)" };
                    _skyboxMat.SetFloat("_SunSize", SkySunSize);
                    _skyboxMat.SetFloat("_SunSizeConvergence", SkySunConvergence);
                    _skyboxMat.SetFloat("_AtmosphereThickness", SkyAtmosphere);
                    _skyboxMat.SetColor("_SkyTint", SkyZenithTint);
                    _skyboxMat.SetColor("_GroundColor", SkyGroundColor);
                    _skyboxMat.SetFloat("_Exposure", SkyExposure);
                }
            }
            if (_skyboxMat != null)
            {
                RenderSettings.skybox = _skyboxMat;
                RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
                RenderSettings.customReflectionTexture = null;
            }

            // (3) SUN: reuse the scene's directional light; create one only if absent.
            Light sun = null;
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (l != null && l.type == LightType.Directional) { sun = l; break; }
            }
            if (sun == null)
            {
                var go = new GameObject("Directional Light (WorldFeel dusk)");
                sun = go.AddComponent<Light>();
                sun.type = LightType.Directional;
                sun.shadows = LightShadows.Soft;
            }
            sun.transform.rotation = Quaternion.Euler(SunPitchDeg, SunYawDeg, 0f);
            sun.color = SunColor;
            sun.intensity = SunIntensity;
            RenderSettings.sun = sun;

            // (4) AMBIENT: warm trilight -- kills the cold dead grey.
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = AmbientSky;
            RenderSettings.ambientEquatorColor = AmbientEquator;
            RenderSettings.ambientGroundColor = AmbientGround;

            // (5) FOG: warm haze, exp-squared so the near field stays crisp.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = FogColor;
            RenderSettings.fogDensity = FogDensity;

            // Refresh the ambient/reflection probes from the new sky so lit
            // materials pick up the dusk environment (cheap, once per apply).
            DynamicGI.UpdateEnvironment();
        }

        // ---- (6) POST: global volume + camera post-fx on ------------------------
        private void ApplyPostVolume()
        {
            if (_postProfile == null)
            {
                _postProfile = ScriptableObject.CreateInstance<VolumeProfile>();
                _postProfile.name = "WorldFeelPostProfile";

                var bloom = _postProfile.Add<Bloom>(overrides: true);
                bloom.intensity.Override(BloomIntensity);
                bloom.threshold.Override(BloomThreshold);
                bloom.scatter.Override(BloomScatter);

                var vignette = _postProfile.Add<Vignette>(overrides: true);
                vignette.intensity.Override(VignetteIntensity);
                vignette.smoothness.Override(VignetteSmoothness);

                var grade = _postProfile.Add<ColorAdjustments>(overrides: true);
                grade.postExposure.Override(GradePostExposure);   // global brightness lift (all times of day)
                grade.saturation.Override(GradeSaturation);
                grade.contrast.Override(GradeContrast);
                grade.colorFilter.Override(GradeFilter);
            }

            // One global volume, parented to this DDOL singleton so it survives
            // scene swaps (idempotent -- created once).
            if (transform.Find("WorldFeelVolume") == null)
            {
                var go = new GameObject("WorldFeelVolume");
                go.transform.SetParent(transform, false);
                var vol = go.AddComponent<Volume>();
                vol.isGlobal = true;
                vol.priority = PostVolumePriority;   // BattleArena's fight volume (100) outranks us
                vol.sharedProfile = _postProfile;
            }

            // The volume only renders if the camera opts into post-processing.
            var cam = Camera.main;
            if (cam != null)
            {
                var data = cam.GetComponent<UniversalAdditionalCameraData>();
                if (data == null) data = cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
                if (!data.renderPostProcessing)
                {
                    data.renderPostProcessing = true;
                    FlowTrace.Step("WorldFeel", $"camera '{cam.name}' renderPostProcessing -> ON.");
                }
            }
        }

        // ---- (7) MOTES: drifting warm dust around the camera (open world only) --
        private void ApplyMotes()
        {
            string active = SceneManager.GetActiveScene().name;
            bool openWorld = DeNelle.Core.HubScenes.IsOverworld(active);  // WO-608: merged overworld
            if (!openWorld) { ClearMotes(); return; }
            if (_motes != null) return;   // already live

            var cam = Camera.main;
            if (cam == null) { FlowTrace.Warn("WorldFeel", "motes skipped -- no Camera.main."); return; }

            _motes = new GameObject("WorldFeel_AmbientMotes");
            _motes.transform.SetParent(cam.transform, worldPositionStays: false);
            _motes.transform.localPosition = Vector3.forward * 6f;   // just ahead of the lens

            var ps = _motes.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop            = true;
            main.playOnAwake     = true;
            main.duration        = 8f;
            main.startLifetime   = new ParticleSystem.MinMaxCurve(MotesLifetime * 0.7f, MotesLifetime);
            main.startSpeed      = new ParticleSystem.MinMaxCurve(MotesDriftSpeed * 0.4f, MotesDriftSpeed);
            main.startSize       = new ParticleSystem.MinMaxCurve(MotesSizeMin, MotesSizeMax);
            main.startColor      = new ParticleSystem.MinMaxGradient(MotesColor);
            main.gravityModifier = -0.005f;                // near-weightless drift
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles    = MotesMax;

            var emission = ps.emission;
            emission.rateOverTime = MotesRate;

            var shape = ps.shape;
            shape.enabled   = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius    = MotesRadius;

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(1f, 0.25f),
                    new GradientAlphaKey(0.5f, 0.75f),
                    new GradientAlphaKey(0f, 1f),
                });
            col.color = new ParticleSystem.MinMaxGradient(grad);

            var r = ps.GetComponent<ParticleSystemRenderer>();
            if (r != null)
            {
                r.renderMode = ParticleSystemRenderMode.Billboard;
                // Same URP-safe material chain HubAmbientVfxInjector uses (never magenta).
                if (!AbilityVfxKit.ApplyParticleMaterial(r))
                    FlowTrace.Warn("WorldFeel", "motes ApplyParticleMaterial returned false -- material unassigned (no magenta).");
                else if (r.sharedMaterial != null)
                    r.sharedMaterial.color = MotesColor;
            }
            ps.Play();
            FlowTrace.Step("WorldFeel", $"ambient motes attached to camera '{cam.name}' (rate={MotesRate}, max={MotesMax}).");
        }

        private void ClearMotes()
        {
            if (_motes == null) return;
            Destroy(_motes);
            _motes = null;
        }
    }
}
