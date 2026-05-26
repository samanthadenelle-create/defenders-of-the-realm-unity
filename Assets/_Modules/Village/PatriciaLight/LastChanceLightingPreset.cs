// =============================================================================
// LastChanceLightingPreset — the dramatic "Defend the Tower" lighting mood.
// -----------------------------------------------------------------------------
// WO-47 Phase 2. Owns the "Last Chance" look the PatriciaLightController applies
// when the assault begins: the world is dim and tense, the tower glows under a
// raked AMBER key, and a cool BLUE rim light rakes in from behind so the tower +
// hero read with strong silhouette/separation against the murk.
//
// DELIBERATELY CHEAP + RELIABLE (WO-47, mobile per WO-47):
//   • No URP post-process Volume — Volumes silently no-op in this project's setup
//     (the intro/PanelSettings lesson). Everything here is plain RenderSettings +
//     real Lights, which always render.
//   • Dark FLAT ambient + ExponentialSquared fog in a deep amber→violet, so depth
//     reads and the far spawners fade into gloom.
//   • A warm amber KEY directional (low, raked) for the dramatic side-light, plus
//     a cool blue RIM directional from behind for contrast/separation.
//   • An optional slow, subtle flicker on the key (a torch-lit "last stand" feel)
//     driven by this component's own Update — no coroutine, no allocation.
//
// USAGE: PatriciaLightController.ApplyLastChanceLighting() attaches this to a
// child GameObject and calls Apply(). Lives in DeNelle.Village (no new asmdef).
// =============================================================================

using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>
    /// Applies + maintains the dramatic "last chance" lighting mood for the
    /// Defend-the-Tower mode. Attach to a GameObject and call <see cref="Apply"/>;
    /// the key light then flickers subtly each frame (toggle with
    /// <see cref="EnableFlicker"/>). All effect comes from RenderSettings + real
    /// Lights — no post-process Volume (those no-op in this project).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LastChanceLightingPreset : MonoBehaviour
    {
        // ── Mood palette ──────────────────────────────────────────────────────
        private static readonly Color AmbientColor  = new Color(0.10f, 0.07f, 0.11f); // near-black violet
        private static readonly Color FogColor       = new Color(0.16f, 0.07f, 0.13f); // deep amber→violet murk
        private const float           FogDensity     = 0.014f;

        private static readonly Color KeyColor       = new Color(1.00f, 0.58f, 0.34f); // warm amber
        private const float           KeyIntensity   = 0.95f;
        private static readonly Vector3 KeyEuler     = new Vector3(18f, 35f, 0f);      // low, raked key

        private static readonly Color RimColor       = new Color(0.36f, 0.55f, 1.00f); // cool blue rim
        private const float           RimIntensity   = 0.85f;
        private static readonly Vector3 RimEuler      = new Vector3(12f, 210f, 0f);     // low, from behind

        // ── Flicker (subtle slow torch flutter on the key) ────────────────────
        private const float FlickerAmplitude = 0.10f;   // ±10% of KeyIntensity
        private const float FlickerSpeed     = 1.6f;     // base wobble rate

        private Light _key;
        private Light _rim;
        private bool  _flicker = true;
        private float _seed;

        /// <summary>Enables/disables the subtle key-light flicker (on by default).</summary>
        public bool EnableFlicker { get => _flicker; set => _flicker = value; }

        /// <summary>
        /// Static convenience: builds a host under <paramref name="parent"/>,
        /// attaches the preset and applies the mood. Returns the preset so the
        /// caller can toggle the flicker. The controller uses this so its inline
        /// lighting body is just one call into the preset.
        /// </summary>
        public static LastChanceLightingPreset Install(Transform parent)
        {
            var go = new GameObject("LastChanceLighting");
            if (parent != null) go.transform.SetParent(parent, false);
            var preset = go.AddComponent<LastChanceLightingPreset>();
            preset.Apply();
            return preset;
        }

        /// <summary>
        /// Applies the dramatic mood: dark flat ambient + amber/violet exp² fog,
        /// retunes any existing directional light into the warm raked KEY, and
        /// spawns a cool blue RIM/fill directional from behind for separation.
        /// Idempotent — re-applying just refreshes the RenderSettings + relights.
        /// </summary>
        public void Apply()
        {
            _seed = Random.value * 10f;

            // ── Atmosphere: dark flat ambient + deep exponential-squared fog ──
            RenderSettings.ambientMode  = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = AmbientColor;
            RenderSettings.fog          = true;
            RenderSettings.fogMode      = FogMode.ExponentialSquared;
            RenderSettings.fogDensity   = FogDensity;
            RenderSettings.fogColor     = FogColor;

            // ── KEY: reuse the scene's existing directional as the warm amber key ──
            _key = FindMainDirectional();
            if (_key == null)
            {
                var keyGo = new GameObject("LastChance Key Light");
                keyGo.transform.SetParent(transform, false);
                _key = keyGo.AddComponent<Light>();
                _key.type = LightType.Directional;
            }
            _key.color = KeyColor;
            _key.intensity = KeyIntensity;
            _key.transform.rotation = Quaternion.Euler(KeyEuler);
            _key.shadows = LightShadows.Soft;

            // ── RIM: cool blue back-light for silhouette/separation against the murk ──
            EnsureRim();
        }

        private void EnsureRim()
        {
            if (_rim == null)
            {
                var rimGo = new GameObject("LastChance Rim Light");
                rimGo.transform.SetParent(transform, false);
                _rim = rimGo.AddComponent<Light>();
                _rim.type = LightType.Directional;
            }
            _rim.color = RimColor;
            _rim.intensity = RimIntensity;
            _rim.transform.rotation = Quaternion.Euler(RimEuler);
            _rim.shadows = LightShadows.None;     // fill only — no second shadow caster
            _rim.renderMode = LightRenderMode.ForcePixel;
        }

        /// <summary>
        /// Picks the brightest enabled directional light in the scene as the key
        /// candidate (the arena's own directional, if BuildArena added one).
        /// </summary>
        private static Light FindMainDirectional()
        {
            Light best = null;
            float bestI = -1f;
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (l == null || l.type != LightType.Directional) continue;
                // Skip our own rim if it already exists.
                if (l.gameObject.name == "LastChance Rim Light") continue;
                if (l.intensity > bestI) { bestI = l.intensity; best = l; }
            }
            return best;
        }

        private void Update()
        {
            if (!_flicker || _key == null) return;
            // Two summed sines (different rates) → an organic, non-repeating flutter.
            float t = Time.time + _seed;
            float wobble = 0.6f * Mathf.Sin(t * FlickerSpeed)
                         + 0.4f * Mathf.Sin(t * FlickerSpeed * 2.37f + 1.3f);
            _key.intensity = KeyIntensity * (1f + FlickerAmplitude * wobble);
        }
    }
}
