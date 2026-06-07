// =============================================================================
// TitleStarfield — gentle animated background for the Title scene.
// -----------------------------------------------------------------------------
// Owner feedback (2026-05-20): the original React build had a slow scrolling
// star + comet field on the landing page — set the tone, kept eyes engaged
// during the 10-15 s conversion window before the player tapped Start. Without
// it the Title was inert.
//
// This builds a ParticleSystem at runtime so we don't have to author it into
// the scene asset. Two layers:
//   • ~240 twinkling stars at random positions in a 30 × 18 × 25 m box,
//     fading in and out on a per-particle randomized loop. Pure white with a
//     subtle violet rim from the additive blend.
//   • ~4 slow comets — larger streaks drifting across the field every few
//     seconds. Same particle system, a separate burst at low rate.
//
// Spawned by TitleController at scene-arrival so it never lives outside Title.
// =============================================================================

using UnityEngine;

namespace DeNelle.Onboarding
{
    [DisallowMultipleComponent]
    public sealed class TitleStarfield : MonoBehaviour
    {
        private void Awake()
        {
            BuildStars();
            BuildComets();
        }

        private void BuildStars()
        {
            var go = new GameObject("Stars");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 0f, 6f);

            var ps = go.AddComponent<ParticleSystem>();
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            ApplyAdditiveMaterial(renderer);

            // AddComponent auto-plays the system; MainModule.duration can't be set
            // while playing. Stop first, configure, then Play() at the end.
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.duration = 10f;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(3.5f, 7.0f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.18f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.02f, 0.12f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.92f, 0.92f, 1.0f, 1f), new Color(0.78f, 0.70f, 1.0f, 1f));
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = 320;

            var emission = ps.emission;
            emission.rateOverTime = 48f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(30f, 18f, 25f);

            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.04f, 0.04f);
            velocity.y = new ParticleSystem.MinMaxCurve(-0.02f, 0.04f);
            // z MUST be set too: x/y use TwoConstants mode, but an unset z defaults
            // to Constant mode — the mismatch spams "Particle Velocity curves must
            // all be in the same mode" EVERY FRAME (16k+ lines an intro). Match it.
            velocity.z = new ParticleSystem.MinMaxCurve(-0.02f, 0.02f);

            // Per-particle alpha twinkle: fade in, hold, fade out.
            var color = ps.colorOverLifetime;
            color.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(new Color(0.85f, 0.78f, 1f), 0.5f),
                    new GradientColorKey(Color.white, 1f),
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(1f, 0.35f),
                    new GradientAlphaKey(1f, 0.65f),
                    new GradientAlphaKey(0f, 1f),
                });
            color.color = new ParticleSystem.MinMaxGradient(grad);

            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortingOrder = -10;

            // Pre-warm the system so the field is alive on frame zero — the
            // player should never see "no stars".
            ps.Play();
            ps.Simulate(6f, true, true);
            ps.Play();
        }

        private void BuildComets()
        {
            var go = new GameObject("Comets");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 4f, 6f);

            var ps = go.AddComponent<ParticleSystem>();
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            ApplyAdditiveMaterial(renderer);

            // AddComponent auto-plays the system; MainModule.duration can't be set
            // while playing. Stop first, configure, then Play() at the end.
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.duration = 16f;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2.8f, 4.2f);
            // Bright small head — was a blob, now a pin-prick that the trail
            // tapers off of. Owner direction 2026-05-20: refined, polished.
            main.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.32f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2.6f, 3.8f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, 0.96f, 0.78f, 1f), new Color(0.95f, 0.80f, 1f, 1f));
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = 5;

            var emission = ps.emission;
            emission.rateOverTime = 0.35f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(30f, 1f, 1f);

            // Mostly horizontal drift with a gentle downward arc.
            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = new ParticleSystem.MinMaxCurve(-2.4f, 2.4f);
            velocity.y = new ParticleSystem.MinMaxCurve(-0.6f, -0.2f);
            velocity.z = new ParticleSystem.MinMaxCurve(-0.2f, 0.2f); // match x/y mode (kills the per-frame warning)

            // Head shrinks as it fades — looks like a comet burning out.
            var sizeOverLife = ps.sizeOverLifetime;
            sizeOverLife.enabled = true;
            var sizeCurve = new AnimationCurve(
                new Keyframe(0f, 0.2f, 0f, 4f),
                new Keyframe(0.15f, 1f),
                new Keyframe(1f, 0.25f, -1f, 0f));
            sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            // Head also gets a tiny scale-and-glow boost at the start.
            var colorOverLife = ps.colorOverLifetime;
            colorOverLife.enabled = true;
            var headGrad = new Gradient();
            headGrad.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(1f, 0.98f, 0.82f), 0f),
                    new GradientColorKey(new Color(1f, 0.85f, 0.65f), 0.5f),
                    new GradientColorKey(new Color(0.85f, 0.65f, 1f), 1f),
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(1f, 0.15f),
                    new GradientAlphaKey(1f, 0.7f),
                    new GradientAlphaKey(0f, 1f),
                });
            colorOverLife.color = new ParticleSystem.MinMaxGradient(headGrad);

            // Head as a small circular billboard.
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortingOrder = -9;

            // Trail does the actual "comet" feel — long, tapered, two-tone.
            var trails = ps.trails;
            trails.enabled = true;
            trails.mode = ParticleSystemTrailMode.PerParticle;
            trails.lifetime = new ParticleSystem.MinMaxCurve(1.4f);
            trails.minVertexDistance = 0.05f;
            trails.dieWithParticles = true;
            trails.sizeAffectsWidth = false;
            trails.inheritParticleColor = false;
            // Width tapers from head to tail (1.0 → 0).
            var widthCurve = new AnimationCurve(
                new Keyframe(0f, 1f),
                new Keyframe(1f, 0f));
            trails.widthOverTrail = new ParticleSystem.MinMaxCurve(0.22f, widthCurve);
            // Color fades from warm bright head → soft violet → transparent.
            var trailGrad = new Gradient();
            trailGrad.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(1f, 0.95f, 0.78f), 0f),
                    new GradientColorKey(new Color(1f, 0.78f, 0.45f), 0.35f),
                    new GradientColorKey(new Color(0.62f, 0.40f, 0.95f), 1f),
                },
                new[]
                {
                    new GradientAlphaKey(0.95f, 0f),
                    new GradientAlphaKey(0.60f, 0.45f),
                    new GradientAlphaKey(0f, 1f),
                });
            trails.colorOverTrail = new ParticleSystem.MinMaxGradient(trailGrad);
            renderer.trailMaterial = renderer.sharedMaterial;

            ps.Play();
            ps.Simulate(6f, true, true);
            ps.Play();
        }

        /// <summary>
        /// Builds a tiny additive-blend material so the particles glow instead
        /// of looking like flat opaque squares. URP-friendly fallback chain.
        ///
        /// Owner direction 2026-05-20: the starfield was rendering crisp purple
        /// SQUARES instead of soft dots because no texture was assigned and the
        /// default particle quad reads as a hard rect. Generate a soft radial
        /// disk (32×32) once and hand it to every starfield/comet material so
        /// each particle reads as a glowing pinprick of light.
        /// </summary>
        private static Texture2D _softDisk;
        private static void ApplyAdditiveMaterial(ParticleSystemRenderer renderer)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                            ?? Shader.Find("Particles/Standard Unlit")
                            ?? Shader.Find("Sprites/Default");
            var mat = new Material(shader);
            mat.name = "Starfield (additive)";
            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);  // Transparent
            if (mat.HasProperty("_Blend"))   mat.SetFloat("_Blend", 1f);    // Additive
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", new Color(1f, 1f, 1f, 1f));

            var disk = GetOrCreateSoftDiskTexture();
            if (disk != null)
            {
                if (mat.HasProperty("_BaseMap"))  mat.SetTexture("_BaseMap", disk);
                if (mat.HasProperty("_MainTex"))  mat.SetTexture("_MainTex", disk);
            }
            mat.renderQueue = 3500;
            renderer.sharedMaterial = mat;
        }

        /// <summary>
        /// Generates a 32×32 radial-falloff disk on first call and caches it.
        /// Centre alpha = 1, edge alpha = 0 with a smooth quadratic falloff so
        /// each particle reads as a glowing dot, not a square.
        /// </summary>
        private static Texture2D GetOrCreateSoftDiskTexture()
        {
            if (_softDisk != null) return _softDisk;
            const int size = 32;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false, true)
            {
                name = "StarfieldSoftDisk",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave,
            };
            const float center = (size - 1) * 0.5f;
            float invR = 1f / center;
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - center) * invR;
                    float dy = (y - center) * invR;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1f - r);
                    a = a * a;   // quadratic falloff softens the edge
                    px[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            }
            tex.SetPixels(px);
            tex.Apply(false, true);
            _softDisk = tex;
            return tex;
        }
    }
}
