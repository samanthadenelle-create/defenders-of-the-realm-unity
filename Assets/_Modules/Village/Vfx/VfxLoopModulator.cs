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
                    Ps       = ps,
                    Rate     = ps.emission.rateOverTime,
                    SimSpeed = ps.main.simulationSpeed,
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
        }

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
