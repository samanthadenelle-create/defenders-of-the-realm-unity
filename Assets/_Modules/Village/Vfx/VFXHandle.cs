// =============================================================================
// VFXHandle — returned by VFXManager for persistent (loop) effects.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// Usage:
//   var handle = VFXManager.Instance.PlayAura(VFXType.Aura_Necromancer, transform);
//   // later…
//   handle.Stop();          // graceful: stops emitting, waits for particles to die
//   handle.Stop(immediate); // kills the effect instantly and returns to pool
//
// Handles survive across frames; check IsAlive before calling Stop to avoid
// double-free. VFXManager.ReturnToPool() clears the internal reference.
// =============================================================================

using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>
    /// Opaque reference to a playing loop/aura VFX. Call <see cref="Stop"/> when
    /// the effect is no longer needed. Safe to call Stop on a dead handle.
    /// </summary>
    public sealed class VFXHandle
    {
        private GameObject _go;
        private VFXType    _type;
        // WO-VFX-002: non-null when this handle represents a Hovl string-keyed effect —
        // Stop() then returns it via VFXManager.ReturnHovl* instead of the VFXType pool.
        private readonly string _hovlKey;

        /// <summary>True while the underlying GameObject is still alive.</summary>
        public bool IsAlive => _go != null;

        /// <summary>The VFX type this handle represents (for debugging).</summary>
        public VFXType Type => _type;

        /// <summary>
        /// WO-888: the live modulator for this loop - the seam an aura's OWNER uses to drive
        /// emission density / simulation speed / body-scale while the loop is held (the
        /// colourblind-safe "pulse rate + guttering shape" read). Attached on demand and
        /// baselined on first touch; every value is restored before the instance returns to
        /// the pool (see <see cref="Stop"/> and VFXManager.ReturnToPool), so a modulated
        /// effect can never contaminate the next user of that pool slot.
        /// Null on a dead handle - callers use <c>handle?.Modulator?.SetX(...)</c>.
        /// </summary>
        public VfxLoopModulator Modulator
        {
            get
            {
                if (_go == null) return null;
                var m = _go.GetComponent<VfxLoopModulator>();
                if (m == null) m = _go.AddComponent<VfxLoopModulator>();
                m.Capture();
                return m;
            }
        }

        // Internal — only VFXManager creates handles.
        internal VFXHandle(GameObject go, VFXType type)
        {
            _go   = go;
            _type = type;
        }

        // WO-VFX-002: handle for a Hovl string-keyed loop (routes Stop() to the Hovl pool).
        internal VFXHandle(GameObject go, string hovlKey)
        {
            _go      = go;
            _type    = VFXType.None;
            _hovlKey = hovlKey;
        }

        /// <summary>
        /// Stop this effect and return it to the pool.
        /// </summary>
        /// <param name="immediate">
        /// If true, the effect is killed and pooled instantly (no particle tail).
        /// If false (default), emission stops and the pool-return is deferred until
        /// all existing particles have died (up to 3 s).
        /// </param>
        public void Stop(bool immediate = false)
        {
            if (_go == null) return;

            var go = _go;
            _go = null;   // clear first — prevents double-stop

            // WO-888: hand back any runtime modulation (emission / sim speed / scale) BEFORE
            // the instance can reach a pool. VFXManager.ReturnToPool repeats this for the
            // return paths no handle owns (timed return, destroyed-host sweep) — cheap and
            // idempotent, and between them a dirty instance cannot be handed out again.
            go.GetComponent<VfxLoopModulator>()?.Restore();

            var mgr = VFXManager.Instance;
            if (mgr == null)
            {
                Object.Destroy(go);
                return;
            }

            // WO-VFX-002: Hovl string-keyed effects return through the Hovl pool.
            if (_hovlKey != null)
            {
                if (immediate)
                {
                    mgr.ReturnHovlToPool(go, _hovlKey);
                }
                else
                {
                    foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true))
                        ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                    mgr.ReturnHovlAfterDelay(go, _hovlKey, 2.5f);
                }
                return;
            }

            if (immediate)
            {
                mgr.ReturnToPool(go, _type);
            }
            else
            {
                // Stop emission on all child ParticleSystems; let particles die
                // naturally, then pool the root after a short grace window.
                foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true))
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);

                mgr.ReturnAfterDelay(go, _type, 2.5f);
            }
        }

        /// <summary>
        /// WO-VFX #3 (soft-stop projectiles): stop EMISSION only (no Clear) and defer the
        /// pool-return by a short grace so an in-flight projectile's TRAIL particles finish
        /// their natural lifetime instead of popping out of the air on impact. Projectile
        /// callers use this on arrival; loop/aura callers keep <see cref="Stop"/>. Hovl-keyed
        /// handles route to the short-grace Hovl return; VFXType handles fall back to the
        /// standard graceful Stop (never worse than before). Safe on a dead handle.
        /// </summary>
        /// <param name="grace">Seconds to let live particles die before pooling (default ~0.6).</param>
        public void StopSoft(float grace = 0.6f)
        {
            if (_go == null) return;

            // Non-Hovl (VFXType) handles have no short-grace Hovl route - defer to the
            // standard graceful Stop so a projectile still finishes its tail.
            if (_hovlKey == null) { Stop(false); return; }

            var go = _go;
            _go = null;   // clear first - prevents double-stop

            // WO-888: same restore as the graceful Stop path (see the note there).
            go.GetComponent<VfxLoopModulator>()?.Restore();

            var mgr = VFXManager.Instance;
            if (mgr == null)
            {
                Object.Destroy(go);
                return;
            }

            // StopEmitting WITHOUT clear: existing trail particles keep rendering and age out
            // during the grace window; then ReturnHovlAfterDelay pools the root.
            foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true))
                ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            mgr.ReturnHovlAfterDelay(go, _hovlKey, grace);
        }

        /// <summary>
        /// Move the effect to follow a new world position (if it is a free-floating
        /// loop not parented to a Transform). Has no effect on pooled/inactive handles.
        /// </summary>
        public void SetPosition(Vector3 worldPos)
        {
            if (_go != null) _go.transform.position = worldPos;
        }

        /// <summary>Re-parent the effect to a new Transform (pass null to un-parent).</summary>
        public void SetParent(Transform parent, bool worldPositionStays = true)
        {
            if (_go != null) _go.transform.SetParent(parent, worldPositionStays);
        }
    }
}
