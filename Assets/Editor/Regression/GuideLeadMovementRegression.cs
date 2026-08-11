// =============================================================================
// GuideLeadMovementRegression [guide-lead-move]
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression (Editor-only).
//
// WO-1014 Half C. PROVEN CAUSE (owner F8 seq 2320, Main_Castle_Overworld, build 20:42):
//
//   [Flow:Pets] guide-lead TICK 'pet-ice-wolf': moved=0.00 m/s over 1.00s ->
//               BODY DID NOT MOVE (carrot written, zero displacement - the write is
//               being ignored downstream). dist=41.98m heroDist=6.73m mode=Defend
//
// The FTUE guide's lead carrot was written into Pet.HomePost every frame, but BOTH of
// Pet.Update's early returns - the mode gate and the ff.petcombat gate - sit ABOVE
// MoveToward(_homePost). mode=Defend cleared the first; FeatureFlags.PetCombat ships
// OFF, so the second returned and the body never moved. The guide's ability to WALK
// was gated behind a COMBAT feature flag.
//
// THE INVARIANT THIS SUITE DEFENDS, in both directions:
//   (1) a pet with an ACTIVE GUIDE LEAD reaches the movement path with PetCombat OFF,
//       in EVERY mode; and
//   (2) combat behaviour stays gated - the ff.petcombat return is still there, and the
//       hunt/anti-ranged/Attack code still sits BELOW it.
//
// WHAT IT PROVES AND HOW:
//   (a) LIVE PREDICATE PROBE - Pet.GuideLeadOwnsMovement is a pure static rule, so the
//       full truth table is executed for real. DeNelle.EditorRegression does not
//       reference DeNelle.Pets (asmdef is outside this lane's file fence), so the call
//       goes through reflection over the loaded assembly - a test-harness lookup, not a
//       bridge-script reflection. A missing type/method FAILS the suite loudly.
//   (b) SOURCE ORDER INVARIANT (comment-stripped lint) - the lane's PLACEMENT cannot be
//       observed from a predicate: it must sit above both early returns and integrate
//       _homePost. Pinned at source, together with the two suppressors that would
//       silently re-break the beat (the ff.petcombat return moving, and PetHarvester
//       disabling the leash while a lead is live).
//
//   NOT provable here: the wolf visibly walking ahead of the hero - owner felt-verify.
//
// Markers: GUIDE_LEAD_MOVE_OK / GUIDE_LEAD_MOVE_FAIL.
// Standalone: run-unity-method DeNelle.Editor.Regression.GuideLeadMovementRegression.RunAll
// Covenant contract Run(out reason) is DataRegression-shaped; wiring into
// DataRegression.RunAll is left to the committer (that file is lane-fenced).
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;

namespace DeNelle.Editor.Regression
{
    public static class GuideLeadMovementRegression
    {
        private const string PetSrc       = "Assets/_Modules/Pets/Pet.cs";
        private const string HarvesterSrc = "Assets/_Modules/Pets/PetHarvester.cs";

        /// <summary>Standalone batch entry - prints the marker.</summary>
        public static void RunAll()
        {
            if (Run(out string reason)) Debug.Log("GUIDE_LEAD_MOVE_OK - " + reason);
            else Debug.LogError("GUIDE_LEAD_MOVE_FAIL: " + reason);
        }

        /// <summary>Covenant contract (DataRegression-shaped). Never throws.</summary>
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            try
            {
                Case(failures, "lead-rule",     () => Case1_LeadRuleTruthTable(failures));
                Case(failures, "gate-order",    () => Case2_UpdateGateOrder(failures));
                Case(failures, "combat-gated",  () => Case3_CombatStaysGated(failures));
                Case(failures, "harvest-yield", () => Case4_HarvestYieldsToLead(failures));
            }
            catch (Exception ex)
            {
                failures.Add("[suite] THREW " + ex.GetType().Name + ": " + ex.Message);
            }

            if (failures.Count == 0)
            {
                reason = "GUIDE LEAD MOVE OK - an active guide lead owns pet movement with " +
                         "ff.petcombat OFF in every mode (yielding only to an explicitly ENABLED " +
                         "combat Defend pet), the lane sits above both Pet.Update early returns and " +
                         "integrates _homePost, the ff.petcombat return still gates every combat " +
                         "behaviour, and PetHarvester releases the leash back to a live lead.";
                return true;
            }
            reason = "guide-lead-move FAIL x" + failures.Count + ": " + string.Join(" | ", failures);
            return false;
        }

        private static void Case(List<string> failures, string name, Action body)
        {
            try { body(); }
            catch (Exception ex) { failures.Add("[" + name + "] THREW " + ex.GetType().Name + ": " + ex.Message); }
        }

        // =====================================================================
        //  Case 1 - the live rule: LEAD + PetCombat OFF must own movement
        // =====================================================================

        private static void Case1_LeadRuleTruthTable(List<string> failures)
        {
            Type petType  = FindType("DeNelle.Pets.Pet");
            Type modeType = FindType("DeNelle.Pets.PetMode");
            if (petType == null || modeType == null)
            {
                failures.Add("[lead-rule] could not resolve DeNelle.Pets.Pet / DeNelle.Pets.PetMode in the " +
                             "loaded assemblies - the guide-lead rule cannot be verified.");
                return;
            }

            MethodInfo rule = petType.GetMethod("GuideLeadOwnsMovement",
                BindingFlags.Public | BindingFlags.Static);
            if (rule == null)
            {
                failures.Add("[lead-rule] Pet.GuideLeadOwnsMovement(bool,bool,PetMode) is GONE - the guide-lead " +
                             "movement lane has been removed, so the FTUE guide is mute again (F8 seq 2320).");
                return;
            }

            string[] modes = { "Idle", "Defend", "Fortify" };

            // No lead -> the lane never engages, whatever the flag says.
            foreach (string m in modes)
            {
                Expect(failures, rule, modeType, false, false, m, false,
                       "no lead must never divert movement");
                Expect(failures, rule, modeType, false, true, m, false,
                       "no lead must never divert movement");
            }

            // THE FIX: lead active + combat OFF (the shipped default) -> the lane owns movement
            // in EVERY mode. This is the exact case F8 seq 2320 caught standing still.
            foreach (string m in modes)
            {
                Expect(failures, rule, modeType, true, false, m, true,
                       "an active guide lead must reach the movement path with ff.petcombat OFF - " +
                       "walking as the guide is NOT combat and must not be gated behind a combat flag");
            }

            // Combat explicitly ENABLED: the pre-existing Defend hunt priority is preserved
            // verbatim; non-combat modes still lead.
            Expect(failures, rule, modeType, true, true, "Defend", false,
                   "with pet combat ENABLED a Defend pet keeps its hunt priority (unchanged behaviour)");
            Expect(failures, rule, modeType, true, true, "Idle", true,
                   "an Idle pet never fought, so it leads even with combat enabled");
            Expect(failures, rule, modeType, true, true, "Fortify", true,
                   "a Fortify pet never hunts, so it leads even with combat enabled");
        }

        private static void Expect(List<string> failures, MethodInfo rule, Type modeType,
                                   bool lead, bool combat, string mode, bool expected, string why)
        {
            object modeValue = Enum.Parse(modeType, mode);
            bool actual = (bool)rule.Invoke(null, new object[] { lead, combat, modeValue });
            if (actual != expected)
                failures.Add("[lead-rule] GuideLeadOwnsMovement(lead=" + lead + ", petCombat=" + combat +
                             ", mode=" + mode + ") returned " + actual + ", expected " + expected +
                             " - " + why + ".");
        }

        private static Type FindType(string fullName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type t = asm.GetType(fullName, false);
                if (t != null) return t;
            }
            return null;
        }

        // =====================================================================
        //  Case 2 - the lane's PLACEMENT in Pet.Update (source order)
        // =====================================================================

        private static void Case2_UpdateGateOrder(List<string> failures)
        {
            string pet = StripComments(File.ReadAllText(PetSrc));

            int lane      = pet.IndexOf("GuideLeadOwnsMovement(DeNelle.Pets.PetHeroLeash.IsLeading",
                                        StringComparison.Ordinal);
            int laneMove  = lane >= 0 ? pet.IndexOf("MoveToward(_homePost", lane, StringComparison.Ordinal) : -1;
            int modeGate  = pet.IndexOf("_mode != PetMode.Defend", StringComparison.Ordinal);
            int combatGate = pet.IndexOf("!DeNelle.Core.FeatureFlags.PetCombat", StringComparison.Ordinal);

            if (lane < 0)
            {
                failures.Add("[gate-order] " + PetSrc + " no longer calls GuideLeadOwnsMovement(PetHeroLeash." +
                             "IsLeading, ...) inside Update - the guide-lead lane is gone and the lead carrot " +
                             "is inert again.");
                return;
            }
            if (laneMove < 0)
            {
                failures.Add("[gate-order] the guide-lead lane in " + PetSrc + " no longer calls " +
                             "MoveToward(_homePost, ...) - it would write the carrot and still never move.");
            }
            if (modeGate < 0 || combatGate < 0)
            {
                failures.Add("[gate-order] could not locate Pet.Update's mode gate and/or ff.petcombat gate " +
                             "in " + PetSrc + " - the ordering invariant cannot be checked.");
                return;
            }
            if (lane > modeGate)
                failures.Add("[gate-order] the guide-lead lane sits BELOW the mode gate (_mode != Defend) - " +
                             "an Idle/Fortify guide returns before it and stands still.");
            if (lane > combatGate)
                failures.Add("[gate-order] the guide-lead lane sits BELOW the ff.petcombat gate - this is the " +
                             "EXACT F8 seq 2320 defect: the guide's walk is gated behind a combat flag that " +
                             "ships OFF.");
            if (laneMove >= 0 && laneMove > modeGate)
                failures.Add("[gate-order] the lane's MoveToward(_homePost) resolves past the mode gate - the " +
                             "lead is still never integrated.");
        }

        // =====================================================================
        //  Case 3 - combat behaviour is STILL behind ff.petcombat
        // =====================================================================

        private static void Case3_CombatStaysGated(List<string> failures)
        {
            string pet = StripComments(File.ReadAllText(PetSrc));

            int combatGate = pet.IndexOf("!DeNelle.Core.FeatureFlags.PetCombat", StringComparison.Ordinal);
            if (combatGate < 0)
            {
                failures.Add("[combat-gated] the ff.petcombat gate is GONE from " + PetSrc + " - pet combat " +
                             "would run in every build. The guide-lead fix must never be implemented by " +
                             "removing or flipping that flag.");
                return;
            }

            // Every hunt/attack seam must resolve BELOW the gate.
            CheckBelow(failures, pet, combatGate, "NearestHostile()",
                       "the hunt scan");
            CheckBelow(failures, pet, combatGate, "UpdateAntiRanged(",
                       "the anti-ranged dash");
            CheckBelow(failures, pet, combatGate, "Attack(foe)",
                       "the attack call");

            // And the flag itself must still default OFF at its definition site.
            string flags = StripComments(File.ReadAllText("Assets/_Modules/Core/FeatureFlags.cs"));
            if (!Regex.IsMatch(flags, @"PetCombat\s*=>\s*Get\(\s*""petcombat""\s*,\s*defaultOn:\s*false\s*\)"))
                failures.Add("[combat-gated] FeatureFlags.PetCombat no longer reads " +
                             "Get(\"petcombat\", defaultOn: false) - pet combat must stay OFF by default; " +
                             "flipping it is the WRONG axis for making the tutorial guide walk.");
        }

        private static void CheckBelow(List<string> failures, string src, int gate, string needle, string what)
        {
            int at = src.IndexOf(needle, StringComparison.Ordinal);
            if (at < 0) return;                       // seam renamed/removed - not this suite's business
            int lastBefore = src.LastIndexOf(needle, Math.Max(0, gate), StringComparison.Ordinal);
            if (lastBefore >= 0 && lastBefore < gate && src.IndexOf(needle, gate, StringComparison.Ordinal) < 0)
                failures.Add("[combat-gated] " + what + " (" + needle + ") now runs ABOVE the ff.petcombat " +
                             "gate - combat behaviour escaped the flag.");
        }

        // =====================================================================
        //  Case 4 - the THIRD suppressor: harvesting must not eat the lead
        // =====================================================================

        private static void Case4_HarvestYieldsToLead(List<string> failures)
        {
            string harvester = StripComments(File.ReadAllText(HarvesterSrc));

            int leadGuard = harvester.IndexOf("PetHeroLeash.IsLeading", StringComparison.Ordinal);
            int yieldCall = harvester.IndexOf("ShouldYieldToCombat()", StringComparison.Ordinal);

            if (leadGuard < 0)
                failures.Add("[harvest-yield] " + HarvesterSrc + " no longer consults PetHeroLeash.IsLeading - " +
                             "SuspendLeash disables the lead listener while gathering, which re-breaks the " +
                             "guide during the founding arc even with Pet.Update fixed.");
            else if (yieldCall >= 0 && leadGuard > yieldCall)
                failures.Add("[harvest-yield] the guide-lead release in " + HarvesterSrc + " resolves after the " +
                             "combat-yield branch - it must be the first thing Update honours so a live lead " +
                             "always gets the leash back.");

            if (!Regex.IsMatch(harvester, @"StopHarvesting\(\s*restoreLeash:\s*true\s*\)"))
                failures.Add("[harvest-yield] " + HarvesterSrc + " no longer calls StopHarvesting(restoreLeash: " +
                             "true) - without the restore the PetHeroLeash component stays disabled and the " +
                             "lead anchor never reaches the pet again.");

            if (!Regex.IsMatch(harvester, @"if\s*\(\s*!\s*DeNelle\.Core\.FeatureFlags\.PetCombat\s*\)\s*return\s+false\s*;"))
                failures.Add("[harvest-yield] PetHarvester.ShouldYieldToCombat no longer short-circuits on " +
                             "ff.petcombat - harvesting would freeze near enemies the pet cannot engage.");
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
