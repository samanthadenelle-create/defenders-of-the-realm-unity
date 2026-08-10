// =============================================================================
// VfxLoopModulator - lets a HELD loop's owner drive emission / speed / size at
// runtime WITHOUT permanently mutating the pooled instance.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// WHY THIS EXISTS (WO-888, the accessibility ticket):
//   The low-HP tell must read by PULSE RATE and GUTTERING SHAPE, never by hue -
//   the owner is red/green colourblind and the old tell was a RED edge vignette
//   she cannot reliably see. "Pulse rate" is not a property of a prefab; it is a
//   property of how the effect is DRIVEN while it is held. So the owner of the
//   loop has to be able to push emission density and simulation speed frame by
//   frame off the HP fraction.
//
// THE TRAP THIS CLOSES:
//   VFXManager pools loop instances and ReturnToPool does NOT reset anything it
//   did not itself change (VFXManager.ReturnToPool: StopAllParticles + reparent +
//   SetActive(false), nothing more). So a caller that reached into a pooled
//   instance and halved its emission would hand the NEXT user of that pool slot a
//   half-strength effect, forever, with no error anywhere. That is the same class
//   of silent, un-catchable defect as a leaked loop slot.
//
//   The fix is that the BASELINE lives on the instance, not in the caller:
//     * Capture() records the pristine per-layer emission + simulation speed and
//       the root local scale, ONCE per instance, before anything is modulated.
//     * Restore() puts every one of them back.
//     * Restore() is called from BOTH ends - VFXHandle.Stop/StopSoft (the normal
//       exit) AND VFXManager.ReturnToPool (which also covers the timed return and
//       the destroyed-host sweep reclaim). A modulated instance therefore cannot
//       reach the pool dirty by ANY path, including the ones no caller owns.
//
// ALL MODULATION IS RELATIVE (a multiplier on the captured baseline), never an
// absolute value: the recipes are owner-felt-tunable bones, so a caller must
// never overwrite a tuned number - only scale it.
// =============================================================================

using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>
    /// Runtime modulation of one pooled VFX instance's emission density, simulation
    /// speed and root scale, with a guaranteed restore to the prefab's authored
    /// values before the instance re-enters the pool. Added on demand by
    /// <see cref="VFXHandle.Modulator"/>; never authored on a prefab.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VfxLoopModulator : MonoBehaviour
    {
        // One layer's pristine values. A recipe is a MULTI-LAYER tree (handbook 1.2),
        // so every layer is captured - modulating only the root would desynchronise a
        // recipe's embers from its body.
        private struct Layer
        {
            public ParticleSystem Ps;
            public ParticleSystem.MinMaxCurve Rate;
            public float SimSpeed;
            // WO-956: the authored start colour, captured so a faction tint override
            // (hostile-palette re-tint of a green enemy aura) restores like every
            // other modulation - the pool can never be handed a re-tinted instance.
            public ParticleSystem.MinMaxGradient StartColor;
        }

        private Layer[] _layers;
        private Vector3 _baseLocalScale;
        private bool _captured;

        // Live multipliers, tracked so a repeated identical call is a no-op (this is
        // driven per frame; writing an unchanged MinMaxCurve every frame on every
        // layer is pure waste).
        private float _emissionMul = 1f;
        private float _speedMul    = 1f;
        private float _scaleMul    = 1f;
        // WO-956: true while a faction tint override has replaced the authored start
        // colours; Restore() puts the authored gradients back and clears it.
        private bool  _tintOverridden;

        /// <summary>True once the pristine baseline has been recorded.</summary>
        public bool HasBaseline => _captured;

        /// <summary>
        /// Record the pristine per-layer emission + simulation speed and the root local
        /// scale. Idempotent and capture-ONCE-ever: the first capture happens on a clean
        /// instance, and <see cref="Restore"/> returns it to that state before the pool
        /// hands it out again, so re-capturing would only risk baking in a modulated
        /// value if a restore were ever missed.
        /// </summary>
        public void Capture()
        {
            if (_captured) return;

            var systems = GetComponentsInChildren<ParticleSystem>(true);
            _layers = new Layer[systems.Length];
            for (int i = 0; i < systems.Length; i++)
            {
                var ps = systems[i];
                _layers[i] = new Layer
                {
                    Ps         = ps,
                    Rate       = ps.emission.rateOverTime,
                    SimSpeed   = ps.main.simulationSpeed,
                    StartColor = ps.main.startColor,   // WO-956: authored colour baseline
                };
            }

            _baseLocalScale = transform.localScale;
            _captured = true;
        }

        /// <summary>
        /// Scale every layer's emission rate by <paramref name="mul"/> against the captured
        /// baseline. This is the PULSE: a caller sweeping this between a deep trough and a
        /// crest produces a rhythm that reads in greyscale, which a hue never does.
        /// Clamped non-negative; 1 = the authored density.
        /// </summary>
        public void SetEmissionScale(float mul)
        {
            if (!_captured) Capture();
            mul = Mathf.Max(0f, mul);
            if (Mathf.Approximately(mul, _emissionMul)) return;
            _emissionMul = mul;
            ApplyEmission();
        }

        /// <summary>
        /// Scale every layer's simulation speed by <paramref name="mul"/> against the
        /// captured baseline - the GUTTERING half of the read (a faster-running flame
        /// snaps and recovers instead of drifting). Clamped to a sane band so a bad
        /// caller cannot freeze an effect or run it into a strobe.
        /// </summary>
        public void SetSimulationSpeed(float mul)
        {
            if (!_captured) Capture();
            mul = Mathf.Clamp(mul, 0.1f, 4f);
            if (Mathf.Approximately(mul, _speedMul)) return;
            _speedMul = mul;
            ApplySpeed();
        }

        /// <summary>
        /// Scale the instance's root local scale by <paramref name="mul"/> against the
        /// captured baseline. Used to seat a room-scale pack recipe onto a BODY: several
        /// of these recipes ship at the size their demo scene wanted, and the phone is
        /// LANDSCAPE 2670x1200, where a tall column spends the scarce axis and crops.
        /// Clamped to a sane band. 1 = the authored scale.
        /// </summary>
        public void SetScaleMul(float mul)
        {
            if (!_captured) Capture();
            mul = Mathf.Clamp(mul, 0.05f, 10f);
            if (Mathf.Approximately(mul, _scaleMul)) return;
            _scaleMul = mul;
            transform.localScale = _baseLocalScale * _scaleMul;
        }

        /// <summary>
        /// Put every modulated value back to the captured baseline. Safe to call any
        /// number of times, on a never-modulated instance, and after layers have been
        /// destroyed. Called from VFXHandle.Stop / StopSoft AND VFXManager.ReturnToPool
        /// so a modulated instance can never reach the pool dirty.
        /// </summary>
        public void Restore()
        {
            if (!_captured) return;

            _emissionMul = 1f;
            _speedMul    = 1f;
            _scaleMul    = 1f;

            ApplyEmission();
            ApplySpeed();
            transform.localScale = _baseLocalScale;

            // WO-956: hand back the authored start colours if a faction tint override
            // ran - same contract as emission/speed/scale, called from both exits, so
            // a re-tinted enemy aura can never contaminate the next pool user (who may
            // be player-side and OWED the authored green).
            if (_tintOverridden)
            {
                _tintOverridden = false;
                for (int i = 0; i < _layers.Length; i++)
                {
                    var ps = _layers[i].Ps;
                    if (ps == null) continue;
                    var main = ps.main;
                    main.startColor = _layers[i].StartColor;
                }
            }
        }

        // =====================================================================
        //  WO-956 - faction tint override (enemy-side effects never green)
        // =====================================================================

        /// <summary>
        /// WO-956: true when any captured layer's AUTHORED start colour presents on
        /// the green axis (<see cref="HostilePalette.IsGreenDominant"/>). Baseline
        /// only - an applied override does not change the answer, so a holder can
        /// re-ask idempotently.
        /// </summary>
        public bool BaselineReadsGreen()
        {
            if (!_captured) Capture();
            if (_layers == null) return false;
            for (int i = 0; i < _layers.Length; i++)
            {
                if (_layers[i].Ps == null) continue;
                if (GradientReadsGreen(_layers[i].StartColor)) return true;
            }
            return false;
        }

        /// <summary>
        /// WO-956: true when any layer's LIVE start colour presents on the green
        /// axis - the post-override verification read (regression + headless use).
        /// </summary>
        public bool CurrentReadsGreen()
        {
            var systems = GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < systems.Length; i++)
            {
                if (systems[i] == null) continue;
                if (GradientReadsGreen(systems[i].main.startColor)) return true;
            }
            return false;
        }

        /// <summary>
        /// WO-956: replace every layer's start-colour RGB with <paramref name="tint"/>
        /// while PRESERVING the authored alpha structure (gradient alpha keys, per-key
        /// times, two-colour ranges) - the fade envelope is part of the recipe's shape
        /// read and must survive a faction re-tint. Restored by <see cref="Restore"/>
        /// from both pool-return ends, so the override can never leak to the next user
        /// of the pool slot.
        /// </summary>
        public void SetTintOverride(Color tint)
        {
            if (!_captured) Capture();
            if (_layers == null) return;
            for (int i = 0; i < _layers.Length; i++)
            {
                var ps = _layers[i].Ps;
                if (ps == null) continue;   // a layer was destroyed - skip, never throw
                var main = ps.main;
                main.startColor = Retinted(_layers[i].StartColor, tint);
            }
            _tintOverridden = true;
        }

        /// <summary>Green test across every representation a MinMaxGradient can hold.</summary>
        private static bool GradientReadsGreen(ParticleSystem.MinMaxGradient g)
        {
            switch (g.mode)
            {
                case ParticleSystemGradientMode.Color:
                    return HostilePalette.IsGreenDominant(g.color);
                case ParticleSystemGradientMode.TwoColors:
                    return HostilePalette.IsGreenDominant(g.colorMin)
                        || HostilePalette.IsGreenDominant(g.colorMax);
                case ParticleSystemGradientMode.Gradient:
                case ParticleSystemGradientMode.RandomColor:
                    return GradientKeysReadGreen(g.gradient);
                case ParticleSystemGradientMode.TwoGradients:
                    return GradientKeysReadGreen(g.gradientMin)
                        || GradientKeysReadGreen(g.gradientMax);
                default:
                    return false;
            }
        }

        private static bool GradientKeysReadGreen(Gradient grad)
        {
            if (grad == null) return false;
            var keys = grad.colorKeys;
            for (int i = 0; i < keys.Length; i++)
                if (HostilePalette.IsGreenDominant(keys[i].color)) return true;
            return false;
        }

        /// <summary>
        /// Rebuild <paramref name="baseline"/> in its AUTHORED representation with every
        /// colour key's RGB replaced by <paramref name="tint"/> - alphas, key times and
        /// the min/max structure survive, mirroring ScaledCurve's "never collapse the
        /// authored shape" rule for the colour channel.
        /// </summary>
        private static ParticleSystem.MinMaxGradient Retinted(ParticleSystem.MinMaxGradient baseline, Color tint)
        {
            switch (baseline.mode)
            {
                case ParticleSystemGradientMode.Color:
                    return new ParticleSystem.MinMaxGradient(WithAlpha(tint, baseline.color.a));
                case ParticleSystemGradientMode.TwoColors:
                    return new ParticleSystem.MinMaxGradient(
                        WithAlpha(tint, baseline.colorMin.a),
                        WithAlpha(tint, baseline.colorMax.a));
                case ParticleSystemGradientMode.Gradient:
                case ParticleSystemGradientMode.RandomColor:
                {
                    var g = new ParticleSystem.MinMaxGradient(RetintedGradient(baseline.gradient, tint));
                    g.mode = baseline.mode;   // keep RandomColor as RandomColor
                    return g;
                }
                case ParticleSystemGradientMode.TwoGradients:
                    return new ParticleSystem.MinMaxGradient(
                        RetintedGradient(baseline.gradientMin, tint),
                        RetintedGradient(baseline.gradientMax, tint));
                default:
                    return baseline;
            }
        }

        private static Gradient RetintedGradient(Gradient src, Color tint)
        {
            if (src == null) return null;
            var srcKeys = src.colorKeys;
            var outKeys = new GradientColorKey[srcKeys.Length];
            for (int i = 0; i < srcKeys.Length; i++)
                outKeys[i] = new GradientColorKey(new Color(tint.r, tint.g, tint.b), srcKeys[i].time);
            var g = new Gradient { mode = src.mode };
            g.SetKeys(outKeys, src.alphaKeys);
            return g;
        }

        private static Color WithAlpha(Color c, float a) => new Color(c.r, c.g, c.b, a);

        private void ApplyEmission()
        {
            if (_layers == null) return;
            for (int i = 0; i < _layers.Length; i++)
            {
                var ps = _layers[i].Ps;
                if (ps == null) continue;   // a layer was destroyed - skip, never throw
                var em = ps.emission;
                em.rateOverTime = ScaledCurve(_layers[i].Rate, _emissionMul);
            }
        }

        private void ApplySpeed()
        {
            if (_layers == null) return;
            for (int i = 0; i < _layers.Length; i++)
            {
                var ps = _layers[i].Ps;
                if (ps == null) continue;
                var main = ps.main;
                main.simulationSpeed = _layers[i].SimSpeed * _speedMul;
            }
        }

        /// <summary>
        /// Scale a MinMaxCurve in whatever mode it was AUTHORED in. A rate authored as a
        /// two-constants range or a curve must stay that shape - collapsing it to a single
        /// constant would silently re-author the recipe's density variation, which for a
        /// gutter effect is exactly the thing being read.
        /// (Mirrors ParticlePackVfxBatchBuilder.Scaled; that one is editor-side.)
        /// </summary>
        private static ParticleSystem.MinMaxCurve ScaledCurve(ParticleSystem.MinMaxCurve c, float k)
        {
            switch (c.mode)
            {
                case ParticleSystemCurveMode.Constant:
                    return new ParticleSystem.MinMaxCurve(c.constant * k);
                case ParticleSystemCurveMode.TwoConstants:
                    return new ParticleSystem.MinMaxCurve(c.constantMin * k, c.constantMax * k);
                case ParticleSystemCurveMode.Curve:
                    return new ParticleSystem.MinMaxCurve(c.curveMultiplier * k, c.curve);
                case ParticleSystemCurveMode.TwoCurves:
                    return new ParticleSystem.MinMaxCurve(c.curveMultiplier * k, c.curveMin, c.curveMax);
                default:
                    return c;
            }
        }
    }
}
