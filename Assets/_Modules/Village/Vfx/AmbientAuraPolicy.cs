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

        // -- WO-1025 HEART-TREE FIREFLIES EXEMPTION (owner rulings 2026-08-16) --------------------
        // Verbatim: "For the tree of life use the butterflies or fireflies." and, on the effect,
        // "that was already there" -- i.e. the EXISTING catalogued 'TreeofLifeAura_Aura' ->
        // FireFlies loop returns to the hub Heart tree. These rulings are NEWER than and specific
        // against the WO-1002 withhold at that one site. Everything else from WO-1002/WO-890 stays
        // byte-intact: ShouldWithhold above is UNCHANGED for every other caller (harvest nodes
        // still refuse the key at PoiCalloutSystem.EnsureNodeAura), and the yellow-aura rejection
        // itself stands -- WO-1025 sec 2 proved the amateurish yellow cone is NOT the fireflies.
        // This is the narrowest possible exemption: ONE key, ONE site, one readonly flag to undo.

        /// <summary>TRUE (owner 2026-08-16): the hub Heart tree plays the FireFlies loop again.
        /// Read ONLY by <see cref="ShouldWithholdAtHeartTree"/> -- no other site consults it.
        /// (static readonly, not const, so neither branch is ever CS0162 unreachable.)</summary>
        public static readonly bool HeartTreeFirefliesExempt = true;

        /// <summary>The HEART-TREE-SITE variant of <see cref="ShouldWithhold"/>: identical for
        /// every key except the rejected FireFlies key, which the 2026-08-16 owner ruling exempts
        /// at this one site while <see cref="HeartTreeFirefliesExempt"/> is true. Harvest nodes
        /// and any future site keep calling <see cref="ShouldWithhold"/> and are unaffected.</summary>
        public static bool ShouldWithholdAtHeartTree(string key)
            => ShouldWithhold(key) && !(HeartTreeFirefliesExempt && IsRejectedAmbientKey(key));

        // -- WO-1476 THE TREE-FOOT AURA THAT CLIMBS THE SKY -------------------------------------
        // Owner validation note on UI-001, 2026-09-07T00:50Z, verbatim:
        //   "there is a VFX exiting about town along Y and it needs removed or turned off"
        //
        // WHICH OF THE TWO CANDIDATES IT IS, PROVEN FROM THE PREFAB BYTES (CLAUDE.md s11B -- the
        // WO named two candidates and a candidate is not a conclusion). Both prefabs were read
        // as YAML on 2026-09-06:
        //
        //   TreeofLifeAura_Aura -> FireFlies.prefab  -- BOTH of its ParticleSystems carry
        //     VelocityModule enabled: 0, ForceModule enabled: 0, gravityModifier scalar 0 and
        //     startSpeed scalar 0. Nothing in that prefab can impart directed motion, so it
        //     CANNOT be the thing rising along Y. It stays exactly as she tagged it.
        //
        //   atfootprintoftree_Aura -> Aura_Nature.prefab -- its "Energy" sub-emitter carries
        //     VelocityModule enabled: 1 with y minMaxState 3 (two constants) minScalar 0.3 /
        //     scalar 0.5, i.e. every particle is pushed UP local +Y at 0.3-0.5 u/s for a
        //     startLifetime of 2-4 s, inWorldSpace 0. That is the column climbing over the town.
        //     (Its "Trails" sub-emitter is z-only; the other four have VelocityModule enabled: 0.)
        //
        // THE SEAM IS THIS POLICY, NOT THE PICK. VfxManualPicks.json is HERS
        // (memory vfx-map-owner-tags-no-creative-pick) and this exact row is additionally pinned
        // by NightStoreAuraSelectionRegression's PinnedTags table, whose own comment says a red
        // case means ASK HER, never re-point the file. So the row is left byte-intact and the
        // SPAWN is withheld here -- the same shape WO-1002 established for the FireFlies loop
        // above, which is why this lives in this file and not in a second gate.

        /// <summary>The owner-tagged key whose prefab (Aura_Nature) drives particles up local +Y
        /// at 0.3-0.5 u/s -- the effect she reported "exiting about town along Y". The KEY and its
        /// pick are untouched; this is only the name of the seat that must not start.</summary>
        public const string WithheldTreeFootAuraKey = "atfootprintoftree_Aura";

        /// <summary>TRUE (owner 2026-09-07): the tree-foot Aura_Nature seat is NOT spawned.
        /// Flip to false to restore it verbatim -- the pick, the seat, the position and the trace
        /// all still exist. (static readonly, not const, so neither branch is CS0162.)</summary>
        public static readonly bool WithholdTreeFootAura = true;

        /// <summary>True when <paramref name="key"/> is the rising tree-foot aura and the owner
        /// ruling above is in force. Ordinal compare: catalog keys are exact identifiers.</summary>
        public static bool ShouldWithholdTreeFootAura(string key)
            => WithholdTreeFootAura &&
               string.Equals(key, WithheldTreeFootAuraKey, StringComparison.Ordinal);

        /// <summary>One-line reason for the FlowTrace at the tree-foot seat, so the capture states
        /// WHY the aura is absent instead of leaving a hole nobody can prove.</summary>
        public static string TreeFootWithholdReason(string siteName)
            => siteName + ": '" + WithheldTreeFootAuraKey + "' WITHHELD by AmbientAuraPolicy " +
               "(WO-1476, owner 2026-09-07 -- a VFX rises over the town along Y). PROOF: its " +
               "Aura_Nature prefab's Energy sub-emitter has VelocityModule enabled with y = 0.3 " +
               "to 0.5 u/s over a 2-4 s lifetime. The VfxManualPicks tag is UNCHANGED and the " +
               "seat still exists; the emitter is simply not spawned. Flip " +
               "AmbientAuraPolicy.WithholdTreeFootAura to false to restore it.";

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
