// =============================================================================
// AmbientAuraPolicy -- the ONE place that says whether the owner-rejected ambient
// aura loop is allowed to play, and at what size.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// OWNER RULING (F8 seq 2306, 2026-08-10, verbatim):
//   "I already asked to remove the yellow glow from other items, i do not want
//    that vfx used at all or set height to .2 so its small"
// She has now asked THREE times: WO-890 (harvest node plume), WO-1002 (the hub
// Heart-of-Elarion tree, 2026-08-07), and this one. The repetition IS the failure,
// so the primary outcome shipped here is REMOVAL, not shrinking -- a smaller but
// still-present plume invites a fourth ask.
//
// THE TWO CALLERS (the only ambient consumers of the key):
//   * HeartAuraController  -- the hub centerpiece Heart of Elarion world tree.
//   * PoiCalloutSystem     -- harvest-node / POI near-field callout auras.
// Both consult this policy; neither hard-codes the key any more, so a retag in the
// VFX Caster that points a node or the tree back at the rejected loop is caught by
// the SAME gate instead of quietly re-shipping the plume.
//
// VFX NO-PICK RULE (memory: vfx-map-owner-tags-no-creative-pick): the owner owns the
// key -> prefab tag in Assets/Editor/VfxManualPicks.json. NOTHING here edits, swaps
// or re-points that tag. This policy only decides whether a hook PLAYS the key.
//
// THE "SMALL INSTEAD OF GONE" FLIP (the ruling's second acceptable outcome):
//   Set ShrinkInsteadOfWithhold = true and the withheld sites instead play the key
//   at ShrunkAmbientAuraScale (0.2). ONE value; no re-implementation. It ships FALSE.
//   (static readonly, not const, so flipping it never turns the other branch into
//   CS0162 unreachable code.)
//
// INSTRUMENTATION (CLAUDE.md section 12): this type decides nothing silently -- every
// caller FlowTrace.Step's the withhold with the key + the reason, so "the aura is
// gone" is a line in the capture rather than an absence nobody can prove.
// =============================================================================

using System;

namespace DeNelle.Village
{
    /// <summary>
    /// Owner-ruling gate for the ambient Tree-of-Life aura loop. Read by
    /// <see cref="HeartAuraController"/> (hub world tree) and <see cref="PoiCalloutSystem"/>
    /// (harvest nodes). See the file header for the ruling and the one-value flip.
    /// </summary>
    public static class AmbientAuraPolicy
    {
        /// <summary>The owner-tagged catalog key that renders as the rejected yellow plume
        /// ("TreeofLifeAura_Aura" -> FireFlies, isLoop). The KEY is untouched -- this is only
        /// the name of the thing the hub tree + harvest nodes must not start.</summary>
        public const string WithheldAmbientAuraKey = "TreeofLifeAura_Aura";

        /// <summary>FALSE (shipped) = the withheld sites play NOTHING. Flip to TRUE for the
        /// ruling's alternative -- they play the key at <see cref="ShrunkAmbientAuraScale"/>
        /// instead. One value; the callers already carry both branches.</summary>
        public static readonly bool ShrinkInsteadOfWithhold = false;

        /// <summary>"set height to .2 so its small" -- the scale multiplier used ONLY when
        /// <see cref="ShrinkInsteadOfWithhold"/> is true.</summary>
        public const float ShrunkAmbientAuraScale = 0.2f;

        /// <summary>True when <paramref name="key"/> is the owner-rejected ambient loop.
        /// Ordinal compare: catalog keys are exact identifiers, never localized.</summary>
        public static bool IsRejectedAmbientKey(string key)
            => string.Equals(key, WithheldAmbientAuraKey, StringComparison.Ordinal);

        /// <summary>True when a hub/harvest site must NOT spawn <paramref name="key"/> at all.
        /// False either because the key is a different effect, or because the owner flipped the
        /// policy to "small instead of gone".</summary>
        public static bool ShouldWithhold(string key)
            => IsRejectedAmbientKey(key) && !ShrinkInsteadOfWithhold;

        /// <summary>Scale multiplier a withheld-site caller should pass when it is still
        /// allowed to play <paramref name="key"/> (i.e. under the shrink flip). 1 for every
        /// other key, so a non-rejected effect is never quietly resized.</summary>
        public static float ScaleFor(string key)
            => IsRejectedAmbientKey(key) && ShrinkInsteadOfWithhold ? ShrunkAmbientAuraScale : 1f;

        /// <summary>One-line reason string for the FlowTrace at each withhold site, so the
        /// capture states WHY the aura is absent instead of leaving a hole.</summary>
        public static string WithholdReason(string siteName)
            => siteName + ": '" + WithheldAmbientAuraKey + "' WITHHELD by AmbientAuraPolicy " +
               "(owner F8 seq 2306 / WO-1002 / WO-890 -- the yellow glow is not to be used). " +
               "The VfxManualPicks tag is UNCHANGED; the emitter is simply not spawned. " +
               "Flip AmbientAuraPolicy.ShrinkInsteadOfWithhold to play it at scale " +
               ShrunkAmbientAuraScale.ToString("0.##") + " instead.";
    }
}
