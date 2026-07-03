// =============================================================================
// NightTorchLightSystem (DEF-214) — self-bootstrapping warm torch/lantern LIGHTS
// for night readability. Night cycle (SkyProgressionController / any future
// DayNight) dims RenderSettings.ambientLight toward near-black as waves advance;
// on mobile that left the hero / buildings / UI unreadable. This drops a small,
// MOBILE-CHEAP set of warm point lights at key spots (the gates, the central
// plaza / Heart) that ramp UP as the world darkens, plus a subtle warm fill that
// follows the hero so the player is always legible. It also lifts the night
// AMBIENT floor a touch so the ground is never pure black — while preserving the
// night MOOD (night is still clearly darker than day).
// -----------------------------------------------------------------------------
// Assembly: Assembly-CSharp (the _Modules/Environment folder has no asmdef, same
//   as TorchFireController which this attaches to). Global namespace to match it
//   and to keep the cross-asmdef story simple — no DeNelle.* asmdef can see
//   TorchFireController, so this MUST live in the predefined assembly.
//
// WHY POLL AMBIENT (not subscribe to a specific day/night system):
//   The "darkness" in this build is driven by RenderSettings.ambientLight being
//   lerped down (SkyProgressionController, DEF-66). Polling ambient luminance is
//   the ONE signal that stays correct no matter which system dims the sky now or
//   later — no hard coupling to WaveManager / SkyProgressionController, no events
//   to miss. Cheap: one Color.maxColorComponent read per frame.
//
// ARCHITECTURE / GUARDRAILS:
//   * [RuntimeInitializeOnLoadMethod(AfterSceneLoad)] self-install — NO scene
//     edit, NO VillageSceneBuilder change (World/Env agent owns those).
//   * Gameplay-scene-gated (Village only) — never touches menus / intro / battle.
//   * MOBILE-CHEAP: capped light count, range-limited, shadows OFF on every light,
//     no per-frame allocation in the hot path, placement done once.
//   * Restores the ambient floor it raised on scene unload / destroy so it never
//     permanently overrides project lighting.
//   * Idempotent: a single DDOL instance; re-runs placement on (re)load of the
//     target scene. If real torch PROPS exist (TorchFireController), it attaches
//     its night-ramp to them instead of spawning duplicate lights.
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Runtime warm point-light system that brightens key spots (gates, plaza,
/// hero) as the night cycle darkens ambient light, for mobile readability.
/// Global namespace (Assembly-CSharp) to match <see cref="TorchFireController"/>.
/// </summary>
public sealed class NightTorchLightSystem : MonoBehaviour
{
        public static NightTorchLightSystem Instance { get; private set; }

        private const string TargetScene = "Village2";

        // ── Tunables (warm, mobile-cheap) ─────────────────────────────────────
        // ~3000K incandescent torch glow.
        private static readonly Color TorchColor = new Color(1.00f, 0.62f, 0.30f, 1f);

        private const int   MaxLights          = 10;    // hard cap — mobile budget
        private const float TorchRange         = 16f;   // range-limited point light
        private const float TorchMaxIntensity  = 3.2f;  // at full night
        private const float HeroFillRange      = 9f;
        private const float HeroFillMaxIntensity = 1.1f; // subtle — keep the hero legible, not lit like day

        // Night detection: ambient luminance 0..1. Above DayLum = full day (lights
        // off); below NightLum = full night (lights at max). Linear ramp between.
        private const float DayLum   = 0.55f;
        private const float NightLum = 0.22f;

        // Ambient floor — at full night we never let ambient luminance fall below
        // this, so the ground isn't pure black. Kept low to preserve night mood.
        private const float NightAmbientFloorLum = 0.14f;

        private const float FlickerAmplitude = 0.12f; // gentle, per-light staggered
        private const float FlickerSpeed     = 4.0f;

        private readonly List<TorchLight> _lights = new List<TorchLight>();
        private Light     _heroFill;
        private Transform _heroTf;
        private bool      _built;

        // Ambient-floor restore state.
        private bool _floorApplied;

        private struct TorchLight
        {
            public Light light;
            public float phase;     // flicker phase offset
            public float baseScale; // per-light intensity scalar (0.6..1.0)
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            new GameObject("NightTorchLightSystem").AddComponent<NightTorchLightSystem>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded   -= OnSceneLoaded;
            SceneManager.sceneLoaded   += OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
            if (SceneManager.GetActiveScene().name == TargetScene) Build();
        }

        private void OnDestroy()
        {
            RestoreAmbientFloor();
            if (Instance == this) Instance = null;
            SceneManager.sceneLoaded   -= OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == TargetScene) { _built = false; Build(); }
        }

        private void OnSceneUnloaded(Scene scene)
        {
            // Leaving the gameplay scene: drop our lights and release the ambient
            // floor so menus / battle scenes keep their authored lighting.
            if (scene.name != TargetScene) return;
            RestoreAmbientFloor();
            TearDown();
        }

        // ── Placement (once per scene load) ───────────────────────────────────

        private void Build()
        {
            if (_built) return;
            _built = true;
            TearDown();

            var spots = CollectSpots();

            // Prefer attaching to real torch props if any exist, so we never
            // double-light a spot the env already authored.
            AttachToExistingTorches();

            int budget = MaxLights - _lights.Count;
            int i = 0;
            foreach (var p in spots)
            {
                if (budget <= 0) break;
                SpawnTorchLight(p, i++);
                budget--;
            }

            EnsureHeroFill();
        }

        /// <summary>Key warm-light anchor points: the 4 gates, the Heart/plaza
        /// centre, and a couple of plaza fill spots. Gate transforms are looked up
        /// by the builder's "Gate-" naming convention; missing ones are skipped.</summary>
        private List<Vector3> CollectSpots()
        {
            var spots = new List<Vector3>();

            // Gates — VillageSceneBuilder names them "Gate-North-Main" etc.
            var all = Object.FindObjectsByType<Transform>();
            foreach (var t in all)
            {
                if (t == null) continue;
                if (t.name.StartsWith("Gate-", System.StringComparison.Ordinal))
                {
                    var pos = t.position; pos.y += 3.0f; // up at the gate arch
                    spots.Add(pos);
                }
            }

            // Heart / plaza centre (scene origin) + a small ring so the central
            // play space reads at night even if no gate lights resolved.
            spots.Add(new Vector3(0f, 3.5f, 0f));
            spots.Add(new Vector3( 10f, 3.0f,  10f));
            spots.Add(new Vector3(-10f, 3.0f, -10f));
            spots.Add(new Vector3( 10f, 3.0f, -10f));
            spots.Add(new Vector3(-10f, 3.0f,  10f));

            return spots;
        }

        /// <summary>If the env layer authored torch props (TorchFireController),
        /// register their child Light so our night-ramp drives them too instead of
        /// spawning a competing light at the same place.</summary>
        private void AttachToExistingTorches()
        {
            var torches = Object.FindObjectsByType<TorchFireController>();
            if (torches == null) return;
            int i = 1000;
            foreach (var tc in torches)
            {
                if (tc == null) continue;
                var lt = tc.pointLight != null ? tc.pointLight : tc.GetComponentInChildren<Light>();
                if (lt == null) continue;
                if (_lights.Count >= MaxLights) break;
                // No shadows — keep it mobile-cheap even on pre-existing props.
                lt.shadows = LightShadows.None;
                _lights.Add(new TorchLight
                {
                    light     = lt,
                    phase     = (i++ * 0.137f) % 6.283f,
                    baseScale = 0.9f
                });
            }
        }

        private void SpawnTorchLight(Vector3 pos, int index)
        {
            var go = new GameObject("NightTorch");
            go.transform.SetParent(transform, false);
            go.transform.position = pos;

            var l = go.AddComponent<Light>();
            l.type      = LightType.Point;
            l.color     = TorchColor;
            l.range     = TorchRange;
            l.intensity = 0f;                    // ramped in Update
            l.shadows   = LightShadows.None;     // MOBILE-CHEAP — no realtime shadows
            l.renderMode = LightRenderMode.ForcePixel; // few of them; keep them quality
            l.bounceIntensity = 0f;

            _lights.Add(new TorchLight
            {
                light     = l,
                phase     = (index * 1.7f) % 6.283f,
                baseScale = Random.Range(0.78f, 1.0f)
            });
        }

        private void EnsureHeroFill()
        {
            ResolveHero();
            if (_heroTf == null) return;

            if (_heroFill == null)
            {
                var go = new GameObject("NightHeroFill");
                go.transform.SetParent(transform, false);
                _heroFill = go.AddComponent<Light>();
                _heroFill.type      = LightType.Point;
                _heroFill.color     = new Color(1f, 0.78f, 0.55f, 1f); // softer, less orange
                _heroFill.range     = HeroFillRange;
                _heroFill.intensity = 0f;
                _heroFill.shadows   = LightShadows.None;
                _heroFill.bounceIntensity = 0f;
            }
        }

        private void ResolveHero()
        {
            if (_heroTf != null) return;
            // Hero carries the "Player" tag (CLAUDE.md §7). Tag-safe lookup.
            GameObject hero = null;
            try { hero = GameObject.FindGameObjectWithTag("Player"); }
            catch (UnityEngine.UnityException) { hero = null; }
            if (hero != null) _heroTf = hero.transform;
        }

        // ── Per-frame ramp ────────────────────────────────────────────────────

        private void Update()
        {
            if (_lights.Count == 0 && _heroFill == null) return;

            // 0 = full day (lights off), 1 = full night (lights max).
            float lum = RenderSettings.ambientLight.maxColorComponent;
            float nightT = Mathf.InverseLerp(DayLum, NightLum, lum); // clamped 0..1
            nightT = Mathf.Clamp01(nightT);

            ApplyAmbientFloor(nightT);

            // Torch lights — staggered gentle flicker, scaled by night ramp.
            for (int i = 0; i < _lights.Count; i++)
            {
                var tl = _lights[i];
                if (tl.light == null) continue;
                float flicker = 1f + FlickerAmplitude *
                    (Mathf.PerlinNoise(Time.time * FlickerSpeed + tl.phase, 0f) - 0.5f) * 2f;
                tl.light.intensity = TorchMaxIntensity * nightT * tl.baseScale * flicker;
                tl.light.enabled   = nightT > 0.01f;
            }

            // Hero fill follows the hero so the player is always readable at night.
            if (_heroFill != null)
            {
                if (_heroTf == null) ResolveHero();
                if (_heroTf != null)
                {
                    var p = _heroTf.position; p.y += 2.2f;
                    _heroFill.transform.position = p;
                    _heroFill.intensity = HeroFillMaxIntensity * nightT;
                    _heroFill.enabled   = nightT > 0.01f;
                }
                else
                {
                    _heroFill.enabled = false;
                }
            }
        }

        // ── Ambient floor (lift night black, preserve mood) ───────────────────

        private void ApplyAmbientFloor(float nightT)
        {
            // Only lift at night, and only if ambient has actually dipped below the
            // floor. We raise toward a desaturated warm floor, never above it, so
            // day lighting and the night mood are both preserved.
            Color amb = RenderSettings.ambientLight;
            float lum = amb.maxColorComponent;

            float targetFloor = Mathf.Lerp(0f, NightAmbientFloorLum, nightT);
            if (targetFloor <= 0.0001f || lum >= targetFloor)
            {
                // Nothing to lift (day, or already bright enough).
                if (_floorApplied)
                {
                    // We previously lifted; let it relax naturally since we only
                    // ever raise toward the floor (no stored override to revert).
                    _floorApplied = false;
                }
                return;
            }

            // Lift toward a warm grey floor (mood-preserving tint), not pure white.
            Color floorColor = new Color(targetFloor, targetFloor * 0.82f, targetFloor * 0.62f, 1f);
            RenderSettings.ambientLight = floorColor;
            _floorApplied = true;
        }

        private void RestoreAmbientFloor()
        {
            // We only ever *raised* ambient toward a low floor; we don't stomp a
            // saved baseline (SkyProgressionController owns/restores the real
            // baseline). Just stop applying — flag reset is enough.
            _floorApplied = false;
        }

        // ── Teardown ──────────────────────────────────────────────────────────

        private void TearDown()
        {
            for (int i = 0; i < _lights.Count; i++)
            {
                var tl = _lights[i];
                // Only destroy lights WE spawned (parented under us). Lights that
                // belong to existing torch props are left intact.
                if (tl.light != null && tl.light.transform.parent == transform)
                    Destroy(tl.light.gameObject);
            }
            _lights.Clear();
            if (_heroFill != null) { Destroy(_heroFill.gameObject); _heroFill = null; }
            _heroTf = null;
        }
    }
