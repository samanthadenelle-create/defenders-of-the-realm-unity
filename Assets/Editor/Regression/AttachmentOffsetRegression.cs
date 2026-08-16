// =============================================================================
// AttachmentOffsetRegression [attachment-offset]
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression (references DeNelle.Core + DeNelle.Village).
//
// WO-994 (owner directive 2026-08-16: "always check the shield placement step-in
// values to the offset forge work and verify" / "the offset sets it correctly -
// look at the data at each point and compare"): the shield's authored Offset Forge
// seat is CORRECT; the port bug is a later step changing the numbers. The 2026-08-16
// harness audit found NOTHING covered AttachmentOffsetRegistry or seated-prop
// transforms - this suite closes that gap at the two levels a headless gate CAN
// prove:
//
//   (a) REGISTRY ROWS - the owner-dialed shield rows load through the REAL
//       AttachmentOffsetRegistry read path (Resources-first, the RC3b ship order):
//       'shield_A' exists, is fullOverride (absolute seat - the WO-970 lesson),
//       has a usable scale and a non-identity rotation; 'shield_A@sheathed'
//       exists for the back carry. Exact euler VALUES are canon data the owner
//       may re-dial - asserted non-degenerate, never pinned to constants.
//
//   (b) TRIPWIRE WIRING (comment-stripped source lint) - the WO-994 seat-drift
//       tripwire + probes in EquipmentController must stay wired: both
//       ApplyHoldPose seat writes record (sheathed + drawn), the scene-load
//       checkpoint verifies BEFORE the re-equip, and the registry probe prints
//       on BOTH the first-equip and scene-load paths. Removing any of them
//       turns the port seam back into a silent flow (CLAUDE.md sec 12: never
//       strip FlowTrace).
//
//   NOT provable here: the shield LOOKING right on the hero after a dungeon->town
//   port - that is the DungeonLoop fleet run + the owner's felt-verify screenshot
//   (docs/HANDOVER.md 2026-08-09 lesson: seam-orientation defects need eyes).
//
// Markers: ATTACHMENT_OFFSET_OK / ATTACHMENT_OFFSET_FAIL.
// Standalone: run-unity-method DeNelle.Editor.Regression.AttachmentOffsetRegression.RunAll
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using DeNelle.Village;

namespace DeNelle.Editor.Regression
{
    public static class AttachmentOffsetRegression
    {
        private const string EquipSrc = "Assets/_Modules/Village/Hero/EquipmentController.cs";

        /// <summary>Standalone batch entry - prints the marker.</summary>
        public static void RunAll()
        {
            if (Run(out string reason)) Debug.Log("ATTACHMENT_OFFSET_OK - " + reason);
            else Debug.LogError("ATTACHMENT_OFFSET_FAIL: " + reason);
        }

        /// <summary>Covenant contract (DataRegression-shaped). Never throws.</summary>
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            string rowSummary = "";
            try
            {
                Case(failures, "registry-rows",   () => rowSummary = Case1_RegistryRows(failures));
                Case(failures, "tripwire-wiring", () => Case2_TripwireWiringLint(failures));
            }
            catch (Exception ex)
            {
                failures.Add("[suite] THREW " + ex.GetType().Name + ": " + ex.Message);
            }

            if (failures.Count == 0)
            {
                reason = "ATTACHMENT OFFSET OK - " + rowSummary +
                         "; WO-994 seat-drift tripwire (both ApplyHoldPose writes + the " +
                         "scene-load checkpoint + both registry probes) wired at source.";
                return true;
            }
            reason = "attachment-offset FAIL x" + failures.Count + ": " + string.Join(" | ", failures);
            return false;
        }

        private static void Case(List<string> failures, string name, Action body)
        {
            try { body(); }
            catch (Exception ex) { failures.Add("[" + name + "] THREW " + ex.GetType().Name + ": " + ex.Message); }
        }

        // =====================================================================
        //  Case 1 - the owner-dialed shield rows load through the REAL registry
        // =====================================================================

        private static string Case1_RegistryRows(List<string> failures)
        {
            // Fresh read through the real path (Resources-first mirror = what SHIPS).
            AttachmentOffsetRegistry.Reload();

            int rows = AttachmentOffsetRegistry.Count;
            if (rows <= 0)
                failures.Add("[registry-rows] AttachmentOffsetRegistry loaded 0 rows - the " +
                             "Resources mirror is missing/unparseable; EVERY dialed seat is lost.");

            if (!AttachmentOffsetRegistry.TryGetOffset("shield_A", out var drawn))
            {
                failures.Add("[registry-rows] 'shield_A' row MISSING - the owner's drawn shield " +
                             "seat (dialed 2026-07-07) does not load; the shield falls back to " +
                             "the un-dialed preset grip.");
            }
            else
            {
                if (!drawn.fullOverride)
                    failures.Add("[registry-rows] 'shield_A' is no longer fullOverride - the seat " +
                                 "silently changed from an absolute pose to a nudge; the composed " +
                                 "result is a different orientation (the WO-970 stranded-pair shape).");
                if (drawn.scale <= 0f)
                    failures.Add("[registry-rows] 'shield_A' scale=" + drawn.scale + " (must be > 0).");
                if (drawn.eulerRot == Vector3.zero)
                    failures.Add("[registry-rows] 'shield_A' rot is IDENTITY - a dialed fullOverride " +
                                 "row with zero rotation means the authored delta was wiped.");
            }

            if (!AttachmentOffsetRegistry.TryGetOffset("shield_A@sheathed", out _))
                failures.Add("[registry-rows] 'shield_A@sheathed' row MISSING - the back-carry pose " +
                             "falls to the built-in default euler; the owner's sheathed dial is lost.");

            return "registry " + rows + " rows; shield_A present" +
                   " (fullOverride=" + (AttachmentOffsetRegistry.TryGetOffset("shield_A", out var d2) && d2.fullOverride) +
                   ") + shield_A@sheathed present";
        }

        // =====================================================================
        //  Case 2 - the WO-994 tripwire + probes stay wired (source lint)
        // =====================================================================

        private static void Case2_TripwireWiringLint(List<string> failures)
        {
            if (!File.Exists(EquipSrc))
            {
                failures.Add("[tripwire-wiring] source not found: " + EquipSrc);
                return;
            }
            // Strip line comments so a commented-out call can never satisfy the lint.
            string src = Regex.Replace(File.ReadAllText(EquipSrc), @"//[^\r\n]*", "");

            Require(failures, src, "RecordOffHandSeatWrite(\"ApplyHoldPose.sheathed\")",
                "the SHEATHED seat write no longer records - back-carry drift is invisible again");
            Require(failures, src, "RecordOffHandSeatWrite(\"ApplyHoldPose.drawn\")",
                "the DRAWN seat write no longer records - in-hand drift is invisible again");
            Require(failures, src, "VerifyOffHandSeat(\"scene-load-pre-reapply\")",
                "the scene-load checkpoint is gone - the dungeon->town port drift check no longer runs");
            Require(failures, src, "registryProbe path=START",
                "the first-equip registry probe is gone - candidate A can no longer be discriminated");
            Require(failures, src, "registryProbe path=SCENELOAD",
                "the scene-load registry probe is gone - candidate A can no longer be discriminated");
            Require(failures, src, "Quaternion.Euler(fo.eulerRot)",
                "the fullOverride seat no longer applies the authored Offset Forge rotation");
        }

        private static void Require(List<string> failures, string src, string token, string why)
        {
            if (!src.Contains(token))
                failures.Add("[tripwire-wiring] token '" + token + "' absent from " + EquipSrc + " - " + why + ".");
        }
    }
}
