// =============================================================================
// StructureHitReaction - the PER-HIT flinch on anything that can be damaged.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// ## THE GAP THIS CLOSES (verified at source before a line was written)
//
// The game had a damage-STATE ladder and no damage-EVENT at all.
//
//   * Building.ApplyDamage (Building.cs) subtracts HP, raises HpChanged, and returns.
//     Nothing anywhere subscribes to HpChanged for a visual. The ONLY thing that ever
//     reacts is StructureDamageVisuals, whose Evaluate runs on a 0.3 s poll (and whose
//     Scan runs on a 2.0 s poll) and which only reacts when the HP crosses a data
//     THRESHOLD - so a hit that does not cross 0.5 or 0.25 produces literally nothing,
//     and one that does produces it up to a third of a second late. Being hit and being
//     BADLY hurt were the same channel, and the first one was silent.
//
//   * HeartController.SetHp is worse: it fires OnHealthChanged and derives a HeartState,
//     and StructureDamageVisuals never scans the Heart at all (deliberately - it has a
//     bespoke tell). HeartAuraController DOES read Hp continuously and drives a genuinely
//     colour-free tell off it (aura SIZE, glow LUMINANCE, pulse RATE) - so the claim
//     "the Heart has zero visual response" is not quite right and is worth stating
//     precisely: it has a good STATE read and NO EVENT read. That state read is lerped
//     across the whole 0-100 range, so a single contact hit moves the aura by a percent
//     or two - invisible. The thing the game is named for did not flinch when struck.
//
// This component is the missing event read, and it is deliberately ONE component rather
// than a per-type edit: it observes an HP fraction through a delegate - exactly the
// surface StructureDamageVisuals already builds for every structure it tracks - so
// walls, buildings, gates, towers, collectors, harvest sites and the Heart are all
// covered without a new damage model, a new event, or a line in any gameplay class.
//
// ## WHY POLLING IS CORRECT HERE AND WRONG THERE
//
// StructureDamageVisuals polls at 0.3 s because its work (a FindObjectsByType scan, a
// worst-first burn-loop re-assignment) is expensive. This polls EVERY FRAME because its
// work is one float compare against a cached value. A hit is an instant, so the read
// has to be too; a third of a second late reads as unrelated to the blow that caused it.
//
// ## COST
//
// ZERO loop slots. The flinch is a Family B one-shot - fired and forgotten, no handle,
// no stop path, and it cannot leak one of VFXManager's 20 global slots no matter how
// many enemies chew on how many walls. It is additionally rate-limited per structure
// (MinBurstInterval) and gated on a minimum drop, because a wave contact-attacking a
// wall would otherwise fire one burst per damage tick per attacker.
//
// COLOURBLIND (owner is red/green): the read is MOTION and TIMING - a puff bursts off
// the structure AT THE INSTANT of contact. There is no tint in it at all, and it is
// redundant with the existing state ladder (smoke density -> flame presence -> break)
// rather than replacing it.
//
// LANDSCAPE PHONE (2670x1200): seated at the structure's bounds CENTRE, not above it -
// a burst that grows upward off a tall wall leaves the frame.
// =============================================================================

using System;
using UnityEngine;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village
{
    /// <summary>
    /// Watches one structure's HP fraction and fires a one-shot impact burst the frame
    /// it drops. Presentation only: it never reads or writes gameplay state beyond the
    /// read-only fraction delegate it is handed.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StructureHitReaction : MonoBehaviour
    {
        /// <summary>
        /// Minimum seconds between bursts on ONE structure. A wave contact-attacking a
        /// wall lands hits far faster than the eye separates them, so without this the
        /// tell would be a continuous smear instead of a series of blows.
        /// </summary>
        private const float MinBurstInterval = 0.15f;

        /// <summary>
        /// Minimum HP-fraction drop that counts as a hit. Guards against float jitter and
        /// against a max-HP recalculation (ApplyStructureHpMultiplier, tier bonuses) being
        /// mistaken for damage.
        /// </summary>
        private const float MinDropFraction = 0.004f;

        /// <summary>
        /// A drop larger than this in one frame is a re-scale or a save-restore, not a
        /// blow - fire the flinch but do not treat the number as meaningful. Kept so a
        /// reload that restores a half-damaged structure does not read as a huge hit.
        /// </summary>
        private const float MaxCredibleDrop = 0.75f;

        private Func<float> _hpFraction;
        private Action      _onHit;          // optional host-owned flinch (see the Heart)
        private string      _label = "structure";
        private float       _last  = -1f;    // -1 = not yet sampled
        private float       _nextBurstAt;

        /// <summary>
        /// Attach (or re-point) the hit reaction on <paramref name="host"/>. Idempotent -
        /// a second call re-points the SAME component, so two bootstraps wiring the same
        /// structure cannot produce two bursts per hit.
        /// </summary>
        /// <param name="host">The structure GameObject.</param>
        /// <param name="hpFraction">Read-only 0..1 HP fraction. Must be null-safe.</param>
        /// <param name="label">Trace label.</param>
        /// <param name="onHit">
        /// Optional callback the HOST owns, invoked on the same frame as the burst. This is
        /// how the Heart adds its own flinch (a kick in its existing colour-free pulse)
        /// without this component knowing anything about the Heart.
        /// </param>
        /// <remarks>
        /// There is deliberately NO scale parameter. VFXManager.Play(VFXType, ...) takes no
        /// scale (only the Hovl string-key path does), so a scale argument here would be a
        /// knob that silently did nothing - the exact shape of defect this codebase keeps
        /// paying for. If per-structure burst size is wanted, it belongs in the catalog row
        /// or in a scaled recipe, not in a parameter that cannot reach the manager.
        /// </remarks>
        public static StructureHitReaction Attach(GameObject host, Func<float> hpFraction,
                                                  string label, Action onHit = null)
        {
            if (host == null || hpFraction == null) return null;
            var r = host.GetComponent<StructureHitReaction>();
            if (r == null) r = host.AddComponent<StructureHitReaction>();
            r._hpFraction = hpFraction;
            r._onHit      = onHit;
            r._label      = string.IsNullOrEmpty(label) ? "structure" : label;
            r._last       = -1f;   // re-baseline: never fire a burst for the attach itself
            return r;
        }

        private void Update()
        {
            if (_hpFraction == null) return;

            float now;
            try
            {
                now = Mathf.Clamp01(_hpFraction());
            }
            catch (Exception e)
            {
                // No silent failures (CLAUDE.md section 12): a source that throws is
                // dropped loudly, and this component goes quiet rather than spamming.
                FlowTrace.Fail("DamageVis",
                    $"StructureHitReaction '{_label}': HP source threw ({e.Message}) - hit flinch disabled " +
                    "on this structure. The damage-state ladder is unaffected.");
                _hpFraction = null;
                return;
            }

            if (_last < 0f) { _last = now; return; }   // first sample is a baseline, never a hit

            float drop = _last - now;
            _last = now;

            if (drop < MinDropFraction) return;        // healed, unchanged, or float noise
            if (Time.time < _nextBurstAt) return;      // still inside this structure's cadence
            _nextBurstAt = Time.time + MinBurstInterval;

            // Bounds CENTRE, not the pivot and not above the mesh: a wall's pivot is at its
            // foot, and a burst on the ground under a wall does not read as the wall being
            // struck. Recomputed per burst because a structure can swap its tier model.
            Vector3 at = transform.position + Vector3.up * 0.5f;
            var rends = GetComponentsInChildren<Renderer>(false);
            bool have = false;
            Bounds b = default;
            for (int i = 0; i < rends.Length; i++)
            {
                var rr = rends[i];
                if (rr == null || rr is ParticleSystemRenderer) continue;   // never the effect's own particles
                if (!have) { b = rr.bounds; have = true; }
                else b.Encapsulate(rr.bounds);
            }
            if (have) at = b.center;

            // Family B one-shot: no handle, no loop slot, nothing to stop. Env_DestructionDust
            // is the LANDED enum value whose own doc names this exact moment - "Destroyable
            // object impact dust (barrel, crate, wall section)" - so this is transcription,
            // not a new creative pick, and the enum append stays Grok's single-owner edit.
            VFXManager.Play(VFXType.Env_DestructionDust, at);

            if (_onHit != null) Guard.Try("DamageVis", $"host hit-flinch '{_label}'", _onHit);

            if (drop > MaxCredibleDrop)
                FlowTrace.Throttle("DamageVis", "hit-implausible", 10f,
                    $"StructureHitReaction '{_label}': a {drop:P0} drop in one frame is a re-scale or a " +
                    "save-restore rather than a blow - the flinch played, but do not read the number as damage.");
            else
                FlowTrace.Throttle("DamageVis", "hit:" + _label, 2f,
                    $"HIT '{_label}': hp {(_last + drop):0.00} -> {_last:0.00} (-{drop:0.000}) - per-hit flinch " +
                    "fired. Before this the only reaction was the 0.3 s damage-STATE poll, which says nothing " +
                    "at all until an HP THRESHOLD is crossed.");
        }
    }
}
