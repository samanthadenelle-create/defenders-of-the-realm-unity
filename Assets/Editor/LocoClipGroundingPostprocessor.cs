// =============================================================================
// LocoClipGroundingPostprocessor — hero FLOAT fix (2026-07-04).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Editor   (editor-only)
//
// THE BUG THIS KILLS (clip-import half):
//   The Knight locomotion clips import with Root Transform Position (Y) = "Original"
//   (meta: keepOriginalPositionY:1 / heightFromFeet:0). "Original" bakes the SOURCE
//   rig's hip height into the root motion, so when the clip is RETARGETED onto the
//   Knight the body rides at the mocap actor's hip elevation instead of planting on
//   the ground — the hero visibly FLOATS.
//
//   Setting Root Transform Position (Y) to "Based Upon: Feet" makes the importer
//   ground the clip on the character's feet, so the retargeted body plants correctly.
//
// WHY A POSTPROCESSOR (not a hand-edited .meta):
//   A prior attempt hand-edited the four .fbx.meta files to keepOriginalPositionY:0 /
//   heightFromFeet:1. Unity's model importer NORMALIZED those hand-edits away on the
//   next reimport (the metas reverted to keepOriginalPositionY:1 / heightFromFeet:0).
//   Meta hand-edits do NOT stick for model clip settings. An AssetPostprocessor that
//   sets the value in OnPreprocessAnimation DOES stick — it re-applies on every import
//   (same mechanism as WeaponPropReadablePostprocessor).
//
// SCOPE (narrow — exactly 4 clips):
//   Only the four hero locomotion FBXs listed in GroundedLocoClips below. An over-broad
//   rule could reground clips that must keep original Y (attacks, spellcasts, damage),
//   so this matches by exact asset path and touches nothing else.
//
// API (Unity 6000.4.8f1):
//   ModelImporterClipAnimation.keepOriginalPositionY (bool) — false => use "Based Upon"
//   ModelImporterClipAnimation.heightFromFeet       (bool) — true  => "Based Upon: Feet"
//   Both are public properties on ModelImporterClipAnimation in this version (they mirror
//   the serialized meta keys keepOriginalPositionY / heightFromFeet verified in the .meta).
//   The clipAnimations array must be REASSIGNED back to modelImporter.clipAnimations for
//   the change to take (mutating the array elements in place is not enough).
//
// NOTE: the orchestrator must trigger a reimport (an asset refresh / build) for this to
//   apply to the already-imported FBXs — GetVersion() is bumped so that happens on the
//   next refresh. This fixes the CONFIRMED clip-import cause of the float; any RESIDUAL
//   float (navmesh-agent root vs clip baseOffset) is a separate, still-NEEDS-CAPTURE angle.
// =============================================================================

using System;
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor
{
    public sealed class LocoClipGroundingPostprocessor : AssetPostprocessor
    {
        // Exactly the four hero locomotion clips that must plant on the ground.
        // Matched by exact (case-insensitive, forward-slash) asset path so nothing else
        // in the mocap folders is regrounded.
        private static readonly string[] GroundedLocoClips =
        {
            "Assets/Action/Knight/Motion/studio-mocap-sword-and-shield-moves/idle_ready.fbx",
            "Assets/Action/Knight/Motion/studio-mocap-sword-and-shield-moves/walkforward01.fbx",
            "Assets/Action/Knight/Motion/studio-mocap-sword-and-shield-moves/runforward_218667.fbx",
            "Assets/Action/Knight/Motion/studio-mocap-series-magical-moves/m-standby-idle.fbx",
        };

        // Bump to force Unity to REIMPORT these already-imported FBXs on the next asset
        // refresh, so they pick up the feet-based root Y without hand-editing any .meta.
        // Bump again only if the grounding rule changes.
        public override uint GetVersion() => 1;

        private static bool IsGroundedLocoClip(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return false;
            string p = assetPath.Replace('\\', '/');
            for (int i = 0; i < GroundedLocoClips.Length; i++)
            {
                if (string.Equals(p, GroundedLocoClips[i], StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private void OnPreprocessAnimation()
        {
            if (assetImporter is not ModelImporter mi) return;
            if (!IsGroundedLocoClip(assetPath)) return;

            // Prefer the explicit clip list; fall back to the importer's default clips.
            var clips = mi.clipAnimations;
            if (clips == null || clips.Length == 0)
                clips = mi.defaultClipAnimations;
            if (clips == null || clips.Length == 0) return;

            bool changed = false;
            for (int i = 0; i < clips.Length; i++)
            {
                var c = clips[i];
                if (c == null) continue;
                if (c.keepOriginalPositionY || !c.heightFromFeet)
                {
                    c.keepOriginalPositionY = false; // use "Based Upon" (not the source's original Y)
                    c.heightFromFeet = true;         // "Based Upon: Feet" — plant on the ground
                    changed = true;
                }
            }

            if (changed)
            {
                // Reassigning the array is REQUIRED for the setting to persist.
                mi.clipAnimations = clips;
                Debug.Log($"[LocoClipGrounding] Root Transform Position (Y) => Based Upon 'Feet' for " +
                          $"'{assetPath}' (kills hero FLOAT: retargeted clip plants on ground, not source hip height).");
            }
        }
    }
}
