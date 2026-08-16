// =============================================================================
// HubTreeAuraWithholdRegression [hub-tree-aura]
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression (references DeNelle.Core + DeNelle.Village).
//
// OWNER RULING (F8 seq 2306, 2026-08-10, verbatim):
//   "I already asked to remove the yellow glow from other items, i do not want
//    that vfx used at all or set height to .2 so its small"
// Asked THREE times: WO-890 (harvest node plume), WO-1002 (the hub Heart-of-Elarion
// tree, 2026-08-07, which sat READY and unimplemented), and this capture. This suite
// exists so a fourth ask is impossible without a RED regression first.
//
// SUPERSEDING OWNER RULINGS AT ONE SITE (WO-1025, 2026-08-16, verbatim):
//   "For the tree of life use the butterflies or fireflies."  /  "that was already there"
// The EXISTING 'TreeofLifeAura_Aura' -> FireFlies loop returns to the HUB HEART TREE via
// AmbientAuraPolicy.HeartTreeFirefliesExempt (site-scoped ShouldWithholdAtHeartTree).
// WO-1025 sec 2 proved the amateurish yellow cone is NOT the fireflies, so the 08-10
// rejection and the 08-16 return do not conflict: the harvest-node withhold and the
// generic ShouldWithhold gate stay byte-intact. Case 2 now asserts the NEW canon (hub
// plays), and Case 1 asserts the exemption is surgical (generic gate still closed).
//
// WHAT IT PROVES, EXECUTABLY (not by lint, where it can be executed):
//
//   (a) POLICY CONTRACT - AmbientAuraPolicy names the rejected key, ships with
//       ShrinkInsteadOfWithhold FALSE (removal is the primary outcome), keeps the
//       0.2 alternative available as ONE value, never resizes an unrelated key,
//       and scopes the WO-1025 fireflies exemption to the heart-tree site only.
//
//   (b) HUB PLAYS (WO-1025) vs COMBAT/RAID KEEP - the real decision predicate
//       HeartAuraController.ShouldWithholdTreeAura is called with both worlds:
//         hasTreeBody = TRUE  (the hub centerpiece, a visible Tree-of-Life child)
//                            -> NOT withheld; the FireFlies loop plays at the crown
//                               (owner 2026-08-16 exemption; withholding here is now a FAIL).
//         hasTreeBody = FALSE (a bare combat/raid Heart)
//                            -> NOT withheld; those Hearts keep their aura, which
//                               WO-1002 section 1 makes an explicit non-goal to remove.
//       This is the shipped predicate itself, so the suite cannot pass over a stub.
//
//   (c) HARVEST NODES - PoiCalloutSystem.NodeAuraKey (the live constant) is NOT the
//       rejected key, so nodes do not start it; and EnsureNodeAura consults the policy,
//       so a retag back to the rejected key is refused at the hook instead of quietly
//       re-shipping the plume.
//
//   (d) SOURCE INVARIANTS (comment-stripped lint) - BuildAura must actually branch on
//       the withhold, must still call StartGreenTreeAura on the non-hub arm, and the
//       withhold must be TRACED. A silent early-return is the failure mode that let
//       this go unnoticed for three days, so "silent" is itself a FAIL here.
//
//   NOT provable here: that no yellow pixel remains on screen - that is the owner's
//   felt-verify / a UI capture (PO closes, per docs/TICKET_PIPELINE.md).
//
// Markers: HUB_TREE_AURA_OK / HUB_TREE_AURA_FAIL.
// Standalone: run-unity-method DeNelle.Editor.Regression.HubTreeAuraWithholdRegression.RunAll
// Covenant contract Run(out reason) is DataRegression-shaped; wiring into
// DataRegression.RunAll is left to the committer (that file is lane-fenced).
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using DeNelle.Village;

namespace DeNelle.Editor.Regression
{
    public static class HubTreeAuraWithholdRegression
    {
        private const string HeartSrc = "Assets/_Modules/Village/Heart/HeartAuraController.cs";
        private const string PoiSrc   = "Assets/_Modules/Village/Vfx/PoiCalloutSystem.cs";

        /// <summary>Standalone batch entry - prints the marker.</summary>
        public static void RunAll()
        {
            if (Run(out string reason)) Debug.Log("HUB_TREE_AURA_OK - " + reason);
            else Debug.LogError("HUB_TREE_AURA_FAIL: " + reason);
        }

        /// <summary>Covenant contract (DataRegression-shaped). Never throws.</summary>
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            try
            {
                Case(failures, "policy",       () => Case1_PolicyContract(failures));
                Case(failures, "hub-vs-raid",  () => Case2_HubWithholdRaidKeeps(failures));
                Case(failures, "harvest-node", () => Case3_HarvestNodeKey(failures));
                Case(failures, "trace-wiring", () => Case4_SourceInvariants(failures));
            }
            catch (Exception ex)
            {
                failures.Add("[suite] THREW " + ex.GetType().Name + ": " + ex.Message);
            }

            if (failures.Count == 0)
            {
                reason = "HUB TREE AURA OK - the '" + AmbientAuraPolicy.WithheldAmbientAuraKey +
                         "' FireFlies loop PLAYS on the hub centerpiece Heart (WO-1025 owner exemption, " +
                         "2026-08-16) while staying WITHHELD on harvest nodes (generic gate intact), a " +
                         "bare combat/raid Heart still starts its aura, the withhold arm is traced " +
                         "rather than silent, and the 0.2 'small instead of gone' alternative is one " +
                         "value away (ShrinkInsteadOfWithhold).";
                return true;
            }
            reason = "hub-tree-aura FAIL x" + failures.Count + ": " + string.Join(" | ", failures);
            return false;
        }

        private static void Case(List<string> failures, string name, Action body)
        {
            try { body(); }
            catch (Exception ex) { failures.Add("[" + name + "] THREW " + ex.GetType().Name + ": " + ex.Message); }
        }

        // =====================================================================
        //  Case 1 - the policy contract
        // =====================================================================

        private static void Case1_PolicyContract(List<string> failures)
        {
            if (AmbientAuraPolicy.WithheldAmbientAuraKey != "TreeofLifeAura_Aura")
                failures.Add("[policy] WithheldAmbientAuraKey is '" + AmbientAuraPolicy.WithheldAmbientAuraKey +
                             "' - the owner-rejected loop is the FireFlies row keyed 'TreeofLifeAura_Aura'. " +
                             "The KEY is the owner's tag and must not be renamed here.");

            if (AmbientAuraPolicy.ShrinkInsteadOfWithhold)
                failures.Add("[policy] ShrinkInsteadOfWithhold is TRUE - the shipped outcome is REMOVAL. " +
                             "The owner has asked three times; a shrunken-but-present plume invites a " +
                             "fourth ask. Flip this only on an explicit owner instruction.");

            if (!AmbientAuraPolicy.ShouldWithhold(AmbientAuraPolicy.WithheldAmbientAuraKey))
                failures.Add("[policy] ShouldWithhold(rejected key) returned FALSE - the GENERIC gate is " +
                             "open (harvest nodes would play the rejected loop). The WO-1025 fireflies " +
                             "exemption is heart-tree-site-scoped ONLY and must never open this gate.");

            // WO-1025 (owner 2026-08-16): the heart-tree site plays the FireFlies loop again.
            // The exemption flag ships TRUE; an accidental flip-off silently re-removes an effect
            // the owner explicitly asked for, so it goes red here.
            if (!AmbientAuraPolicy.HeartTreeFirefliesExempt)
                failures.Add("[policy] HeartTreeFirefliesExempt is FALSE - the owner ruled 2026-08-16 " +
                             "('use the butterflies or fireflies' / 'that was already there') that the " +
                             "FireFlies loop returns to the hub Heart tree. Flip only on owner word.");
            if (AmbientAuraPolicy.ShouldWithholdAtHeartTree(AmbientAuraPolicy.WithheldAmbientAuraKey))
                failures.Add("[policy] ShouldWithholdAtHeartTree(rejected key) returned TRUE - the " +
                             "WO-1025 fireflies exemption is not reaching the heart-tree site.");

            if (AmbientAuraPolicy.ShouldWithhold("Cathedral_Aura") ||
                AmbientAuraPolicy.ShouldWithhold("Aura_HeartPulse") ||
                AmbientAuraPolicy.ShouldWithhold(null))
                failures.Add("[policy] ShouldWithhold matched a key that is NOT the rejected loop - the " +
                             "withhold must be surgical; unrelated auras keep playing.");

            if (Math.Abs(AmbientAuraPolicy.ShrunkAmbientAuraScale - 0.2f) > 0.0001f)
                failures.Add("[policy] ShrunkAmbientAuraScale is " + AmbientAuraPolicy.ShrunkAmbientAuraScale +
                             " - the owner's alternative was explicitly '.2 so its small'.");

            // With the shrink flip OFF, the withheld sites must never silently resize anything.
            if (Math.Abs(AmbientAuraPolicy.ScaleFor(AmbientAuraPolicy.WithheldAmbientAuraKey) - 1f) > 0.0001f)
                failures.Add("[policy] ScaleFor(rejected key) is not 1 while ShrinkInsteadOfWithhold is " +
                             "FALSE - under removal the scale path must be inert.");
            if (Math.Abs(AmbientAuraPolicy.ScaleFor("Cathedral_Aura") - 1f) > 0.0001f)
                failures.Add("[policy] ScaleFor() resized an unrelated key - only the rejected loop may " +
                             "ever be shrunk.");

            if (string.IsNullOrEmpty(AmbientAuraPolicy.WithholdReason("probe")))
                failures.Add("[policy] WithholdReason() returned empty - the withhold must be able to " +
                             "state WHY in the capture (CLAUDE.md section 12: no silent failures).");
        }

        // =====================================================================
        //  Case 2 - hub withholds, combat/raid keeps (the shipped predicate)
        // =====================================================================

        private static void Case2_HubWithholdRaidKeeps(List<string> failures)
        {
            // hasTreeBody TRUE == the hub static-town Heart of Elarion: a visible non-particle
            // Tree-of-Life renderer under the anchor. This is the SAME single hub-detection the
            // white Aura_HeartPulse swirl already rides - no second gate was invented.
            // WO-1025 (owner 2026-08-16) FLIPS this assertion from the WO-1002 era: the hub tree
            // must now PLAY the FireFlies loop (exemption live), so withholding here is the FAIL.
            if (HeartAuraController.ShouldWithholdTreeAura(true))
                failures.Add("[hub-vs-raid] the HUB centerpiece Heart (hasTreeBody=true) is WITHHOLDING " +
                             "'" + AmbientAuraPolicy.WithheldAmbientAuraKey + "' - the owner ruled " +
                             "2026-08-16 that the FireFlies loop returns to the world tree " +
                             "(WO-1025 heart-tree exemption is not in effect).");

            // hasTreeBody FALSE == a bare combat / raid Heart. WO-1002 section 1 makes keeping their
            // aura an explicit requirement, so over-removal fails here just as loudly.
            if (HeartAuraController.ShouldWithholdTreeAura(false))
                failures.Add("[hub-vs-raid] a COMBAT/RAID Heart (hasTreeBody=false) is withholding its " +
                             "ambient aura - the withhold is HUB-ONLY (WO-1002 section 1); combat Hearts " +
                             "must keep theirs.");
        }

        // =====================================================================
        //  Case 3 - harvest nodes never start the rejected loop
        // =====================================================================

        private static void Case3_HarvestNodeKey(List<string> failures)
        {
            if (AmbientAuraPolicy.IsRejectedAmbientKey(PoiCalloutSystem.NodeAuraKey))
                failures.Add("[harvest-node] PoiCalloutSystem.NodeAuraKey is pointed at the rejected loop '" +
                             AmbientAuraPolicy.WithheldAmbientAuraKey + "' - harvest nodes would re-ship the " +
                             "WO-890 yellow plume. Retag the node key in the VFX Caster.");

            // The hook must ALSO consult the policy, so a future retag is refused at the call site
            // rather than depending on the constant above staying correct forever.
            string poi = StripComments(File.ReadAllText(PoiSrc));
            if (!poi.Contains("AmbientAuraPolicy.ShouldWithhold(NodeAuraKey)"))
                failures.Add("[harvest-node] " + PoiSrc + " no longer gates EnsureNodeAura on " +
                             "AmbientAuraPolicy.ShouldWithhold(NodeAuraKey) - a retag back to the rejected " +
                             "key would spawn the plume with nothing to stop it.");
        }

        // =====================================================================
        //  Case 4 - the branch + the trace, pinned at source
        // =====================================================================

        private static void Case4_SourceInvariants(List<string> failures)
        {
            string heart = StripComments(File.ReadAllText(HeartSrc));

            // (1) BuildAura branches on the withhold flag.
            if (!Regex.IsMatch(heart, @"if\s*\(\s*_suppressTreeAura\s*\)"))
                failures.Add("[trace-wiring] " + HeartSrc + " no longer branches on _suppressTreeAura in " +
                             "BuildAura - the hub withhold has been removed.");

            // (2) The flag comes from the shared predicate (which is what Case 2 tests).
            if (!heart.Contains("_suppressTreeAura = ShouldWithholdTreeAura(_hasTreeBody)"))
                failures.Add("[trace-wiring] " + HeartSrc + " no longer derives _suppressTreeAura from " +
                             "ShouldWithholdTreeAura(_hasTreeBody) - the tested predicate and the shipped " +
                             "decision have diverged, so a green suite would prove nothing.");

            // (3) The non-hub arm STILL starts the aura - the combat/raid keep, at source.
            if (!heart.Contains("StartGreenTreeAura("))
                failures.Add("[trace-wiring] " + HeartSrc + " no longer calls StartGreenTreeAura at all - " +
                             "combat/raid Hearts lost their aura (over-removal).");

            // (4) The withhold is TRACED. A silent withhold is how this went unnoticed for three days;
            //     CLAUDE.md section 12 forbids the silent variant outright.
            int branchAt = heart.IndexOf("if (_suppressTreeAura)", StringComparison.Ordinal);
            if (branchAt >= 0)
            {
                int window = Math.Min(600, heart.Length - branchAt);
                string after = heart.Substring(branchAt, window);
                if (!after.Contains("FlowTrace."))
                    failures.Add("[trace-wiring] the _suppressTreeAura branch in " + HeartSrc + " does not " +
                                 "FlowTrace - a silent withhold leaves the next capture with no evidence " +
                                 "that the aura was suppressed on purpose (CLAUDE.md section 12).");
            }
        }

        /// <summary>Strip // line and /* */ block comments so a lint never matches doc text.</summary>
        private static string StripComments(string src)
        {
            src = Regex.Replace(src, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
            src = Regex.Replace(src, @"//[^\r\n]*", string.Empty);
            return src;
        }
    }
}
