// =============================================================================
// CollectBurstVfx - WO-1347 (second owner tag). A one-shot celebratory burst for a
// reward beat that happens on a UI SCREEN rather than on a world object.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// ⛔ THIS IS NOT A SECOND SPAWNER OR A SECOND POOL. There is exactly ONE spawn owner
// in this project - VFXManager.PlayKey - and this file calls it. What it adds is the
// PLACEMENT decision, which is the part a UI caller genuinely cannot make for itself.
//
// -----------------------------------------------------------------------------
// THE PROBLEM IT SOLVES, STATED PLAINLY.
//
// The owner's tagged effects are WORLD-SPACE particle composites. The daily chest is
// a ScreenSpaceOverlay UI modal (ElarionUiKit.ObsidianModal). Parenting a world-space
// particle system into an overlay Canvas is the classic silent failure: it renders at
// the wrong scale, at the wrong depth, or not at all, and it looks exactly like the
// tag simply did not work. So the burst is NOT parented into the canvas. It is seated
// in WORLD space on a plane a fixed distance in front of the active camera and left
// unparented, so the modal's own teardown (Close() destroys the modal canvas on the
// very next statement) cannot take the effect with it.
//
// -----------------------------------------------------------------------------
// LIFETIME IS BOUNDED EXPLICITLY, AND THAT IS DELIBERATE.
//
// An explicit lifetime is passed on every raise. For a one-shot row it changes nothing
// (the effect auto-returns anyway). For a LOOP row it is what makes VFXManager treat
// the loop as TIMED and auto-return it - so if the owner ever retags this key
// isLoop:true, this call site still cannot strand an orphan loop playing forever in
// front of the camera with nobody holding a handle. A UI beat has no object whose
// state could own that lifetime, which is exactly why it is stated here.
//
// -----------------------------------------------------------------------------
// NO PICKING. The caller passes the owner's KEY; the catalog owns the key -> prefab
// mapping (memory vfx-map-owner-tags-no-creative-pick). Nothing here reads, chooses,
// substitutes or rescales a prefab, and no pack asset is modified on disk. No tint is
// applied either - the owner is red/green colourblind and a collect flash must read by
// motion and luminance, never by hue.
//
// ASCII only. FlowTrace tag "CollectVfx". Never strip it (CLAUDE.md section 12).
// =============================================================================

using DeNelle.Core.Diagnostics;
using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>
    /// Raises an owner-tagged VFX key as a short, unparented WORLD-space burst in front
    /// of the active camera - the placement a UI reward beat needs. One spawn owner
    /// (<see cref="VFXManager.PlayKey"/>); this only decides where and for how long.
    /// </summary>
    public static class CollectBurstVfx
    {
        private const string Sys = "CollectVfx";

        /// <summary>Metres in front of the camera. Far enough to clear any near plane the
        /// mobile camera rig uses, close enough that the burst reads at phone size.</summary>
        private const float Distance = 6f;

        /// <summary>Seconds the burst is allowed to live. Bounds a LOOP row too - see the
        /// file header for why that matters.</summary>
        private const float Lifetime = 1.6f;

        /// <summary>
        /// Play <paramref name="key"/> once as a world-space burst in front of the camera.
        /// NEVER throws, and always traces its decision: the key, whether the prefab
        /// RESOLVED, the space it was placed in, the resolved position and the camera it
        /// was resolved from. A missing effect and a deliberately brief one are otherwise
        /// indistinguishable.
        /// </summary>
        /// <param name="key">The owner's catalog key, verbatim.</param>
        /// <param name="why">One phrase naming the beat, for the trace.</param>
        public static void Raise(string key, string why)
        {
            if (string.IsNullOrEmpty(key)) return;

            Guard.Try(Sys, "raise collect burst '" + key + "'", () =>
            {
                var cam = Camera.main;
                if (cam == null) cam = Object.FindAnyObjectByType<Camera>();
                if (cam == null)
                {
                    // Not an error worth failing a grant over - the reward already landed and
                    // the toast already spoke. But it IS a silent visual no-op, so it is said.
                    FlowTrace.Warn(Sys,
                        "no camera available - collect burst '" + key + "' (" + why + ") was NOT " +
                        "raised. The reward itself is unaffected; only the flourish is missing.");
                    return;
                }

                bool resolves = VFXManager.CanPlayKey(key);
                var camT = cam.transform;
                Vector3 at = camT.position + camT.forward * Distance;

                // Face the camera. No parent: the caller's modal is destroyed on the next
                // statement and must not be able to take this with it. No tint, no scale
                // override - the catalog row's own DefaultScale stands, so nothing here
                // rescales the owner's effect.
                VFXManager.PlayKey(key, at, Quaternion.LookRotation(-camT.forward), null,
                                   null, 0f, Lifetime);

                FlowTrace.Step(Sys,
                    "collect burst '" + key + "' (" + why + "): prefabResolved=" + resolves +
                    " space=WORLD (deliberately NOT parented into the overlay Canvas) pos=" + at +
                    " camera='" + cam.name + "' lifetime=" + Lifetime.ToString("0.0") + "s. " +
                    (resolves
                        ? "Unparented and time-bounded, so neither the modal teardown nor a loop retag can strand it."
                        : "Key not in the runtime catalog yet - regenerate it (Defenders/VFX/Generate Hovl VFX " +
                          "Catalog) or the burst stays absent. The grant and the toast are unaffected."));
            });
        }
    }
}
