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
//   (c) SEAT PRECEDENCE (WO-1123) - the orient ladder, asserted IN ORDER: authored
//       offset row -> manual -> derived -> archetype default (WeaponOrientHelper
//       .ResolveSource, one pure function), plus an end-to-end probe that
//       WeaponDef.manual actually deserializes (it was authored on 81 rows and read
//       by NOTHING until WO-1123).
//
//   (d) THE GUARD IS NO LONGER BLIND (WO-1123 acceptance 5) - (a) asserts the LEGACY
//       'shield_A' rows; the shield the game actually equips is derived from the
//       STARTER LOADOUT at test time, so the assertion follows the next mesh swap
//       instead of staying pointed at a retired asset. That blindness is why this
//       suite passed green while the live default shield was unauthored in both poses.
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
using DeNelle.Core.Geometry;
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
            string starterSummary = "";
            try
            {
                Case(failures, "registry-rows",   () => rowSummary = Case1_RegistryRows(failures));
                Case(failures, "tripwire-wiring", () => Case2_TripwireWiringLint(failures));
                Case(failures, "seat-precedence", () => Case3_SeatPrecedence(failures));
                Case(failures, "starter-shield-key", () => starterSummary = Case4_StarterShieldKey(failures));
            }
            catch (Exception ex)
            {
                failures.Add("[suite] THREW " + ex.GetType().Name + ": " + ex.Message);
            }

            if (failures.Count == 0)
            {
                reason = "ATTACHMENT OFFSET OK - " + rowSummary +
                         "; WO-994 seat-drift tripwire (both ApplyHoldPose writes + the " +
                         "scene-load checkpoint + both registry probes) wired at source" +
                         "; " + starterSummary;
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

        // =====================================================================
        //  Case 3 - the WO-1123 PRECEDENCE LADDER, asserted IN ORDER
        // =====================================================================
        //
        // Precedence is: authored offset row -> manual -> derived -> archetype default. It lives in
        // ONE pure function (WeaponOrientHelper.ResolveSource) precisely so it can be asserted
        // without a scene, a hero or a mesh.
        //
        // WHAT BREAKS EACH ASSERTION (acceptance 6 - every assertion here can FAIL):
        //   - swap the manual and derived tiers  -> the manual row asserts Derived and this reddens;
        //   - drop the authored-row tier         -> the dialled row asserts Manual/Derived;
        //   - delete WeaponDef.manual again      -> the catalog probe below reads false on a row
        //     that authors true, which is EXACTLY the WO-1123 sec 1.2 defect (81 rows authored,
        //     zero read) re-appearing.
        private static void Case3_SeatPrecedence(List<string> failures)
        {
            Expect(failures, WeaponOrientHelper.ResolveSource(true, true, true),
                   SeatSource.AuthoredOffset,
                   "an authored Offset Forge row must outrank BOTH manual and the derivation");
            Expect(failures, WeaponOrientHelper.ResolveSource(true, false, false),
                   SeatSource.AuthoredOffset,
                   "an authored row must win even when nothing can be derived");
            Expect(failures, WeaponOrientHelper.ResolveSource(false, true, true),
                   SeatSource.Manual,
                   "manual:true must outrank the derivation - this is the tier that protects the " +
                   "owner's 81 dialled rows from the first auto pass");
            Expect(failures, WeaponOrientHelper.ResolveSource(false, false, true),
                   SeatSource.Derived,
                   "an unauthored, non-manual row with measurable geometry must DERIVE");
            Expect(failures, WeaponOrientHelper.ResolveSource(false, false, false),
                   SeatSource.ArchetypeDefault,
                   "with nothing authored and nothing measurable the hand-typed archetype constant " +
                   "is the last resort - it must still be reachable (it is never deleted)");

            if (!WeaponOrientHelper.MayDerive(false, false))
                failures.Add("[seat-precedence] MayDerive(authored=false, manual=false) is FALSE - " +
                             "nothing would ever derive; the helper is inert.");
            if (WeaponOrientHelper.MayDerive(false, true))
                failures.Add("[seat-precedence] MayDerive(manual=true) is TRUE - a derived pass would " +
                             "OVERWRITE an owner-dialled row. This is the WO-1123 sec 1.2 hazard.");
            if (WeaponOrientHelper.MayDerive(true, false))
                failures.Add("[seat-precedence] MayDerive(authoredRow=true) is TRUE - a derived pass " +
                             "would overwrite an Offset Forge seat the owner dialled by eye.");

            // The flag is READ end-to-end: a manual row must deserialize as manual, and the live
            // default shield must NOT (or the fix for it can never run).
            var dialled = GearCatalog.FindWeapon("tripo_shield_a");
            if (dialled == null)
                failures.Add("[seat-precedence] weapons.json row 'tripo_shield_a' did not load - the " +
                             "manual-flag probe cannot run.");
            else if (!dialled.manual)
                failures.Add("[seat-precedence] 'tripo_shield_a' authors manual:true in weapons.json but " +
                             "WeaponDef.manual deserialized FALSE - the field was dropped again. Every " +
                             "one of the 81 dialled rows is unprotected against the derived pass.");

            var starter = GearCatalog.FindWeapon(StarterShieldId);
            if (starter != null && starter.manual)
                failures.Add("[seat-precedence] '" + StarterShieldId + "' now carries manual:true, so the " +
                             "derived shield seat will NEVER run for the default shield. If that is " +
                             "intended the owner must have dialled it - check for an authored row first.");
        }

        private static void Expect(List<string> failures, SeatSource actual, SeatSource expected, string why)
        {
            if (actual != expected)
                failures.Add("[seat-precedence] expected " + expected + " but got " + actual + " - " + why + ".");
        }

        // =====================================================================
        //  Case 4 - the guard is no longer BLIND (WO-1123 acceptance 5)
        // =====================================================================
        //
        // WHY THIS EXISTS: Case 1 asserts 'shield_A' / 'shield_A@sheathed' - the LEGACY mesh, still
        // live for tripo_shield_a and still owner-dialled, so those assertions stay. But the shield
        // the game actually puts in the starter's hand is knight_shield_starter ->
        // "gear/weapon/ShieldWithItemLogic", which has NO authored row in EITHER pose. The suite
        // therefore passed green while the live default shield was broken.
        //
        // The key is DERIVED FROM THE LOADOUT AT TEST TIME (StarterLoadout -> catalog row ->
        // prefabPath basename), never hard-coded, so the next mesh swap moves this assertion with
        // the game instead of leaving it pointed at a retired asset.
        //
        // WHAT MAKES IT FAIL:
        //   - the starter kit stops naming a shield, or names an id the catalog does not have;
        //   - that row loses its prefabPath (the mesh key becomes underivable);
        //   - the live key has NO authored row AND the derived seat is not wired at source - i.e.
        //     the shield is back to "identity, no derivation of any kind", which is the exact state
        //     WO-1123 was raised to end.
        private const string StarterShieldId = "knight_shield_starter";

        private static string Case4_StarterShieldKey(List<string> failures)
        {
            string offHandId = StarterLoadout.OffHandFor("knight");
            if (string.IsNullOrEmpty(offHandId))
            {
                failures.Add("[starter-shield-key] StarterLoadout.OffHandFor(\"knight\") is EMPTY - the " +
                             "starter has no off-hand, so no assertion here can see the live shield.");
                return "starter shield: NONE";
            }

            var def = GearCatalog.FindWeapon(offHandId);
            if (def == null)
            {
                failures.Add("[starter-shield-key] the starter off-hand '" + offHandId + "' does not " +
                             "resolve in weapons.json - the loadout and the catalog have drifted apart.");
                return "starter shield: '" + offHandId + "' UNRESOLVED";
            }
            if (!def.IsOffHandItem)
                failures.Add("[starter-shield-key] the starter off-hand '" + offHandId + "' has category '" +
                             (def.category ?? "<null>") + "' - it is not a shield, so it seats through a " +
                             "different path than the one this suite guards.");

            string meshKey = MeshKeyFor(def);
            if (string.IsNullOrEmpty(meshKey))
            {
                failures.Add("[starter-shield-key] no mesh key can be derived for '" + offHandId + "' " +
                             "(prefabPath='" + (def.prefabPath ?? "<null>") + "') - the offset registry is " +
                             "keyed by mesh name, so this shield can never be dialled at all.");
                return "starter shield: '" + offHandId + "' has no mesh key";
            }

            bool drawnRow = AttachmentOffsetRegistry.TryGetOffset(meshKey, out _);
            bool sheathedRow = AttachmentOffsetRegistry.TryGetOffset(meshKey + "@sheathed", out _);

            // The live shield must be seated by SOMETHING that reads its geometry or its dial. If it
            // has no authored row in a pose AND the derived path is not wired, that pose is the bare
            // constant again - the defect, restated.
            string src = File.Exists(EquipSrc) ? Regex.Replace(File.ReadAllText(EquipSrc), @"//[^\r\n]*", "") : "";
            bool derivedDrawnWired = src.Contains("WeaponOrientHelper.TryResolveShieldFrame");
            bool derivedSheathWired = src.Contains("ComputeSheathedOffHandRotation(back)");
            bool manualReaderWired = src.Contains("IsManualOrientRow");

            if (!drawnRow && !derivedDrawnWired)
                failures.Add("[starter-shield-key] the LIVE shield mesh '" + meshKey + "' has no authored " +
                             "drawn row AND the WO-1123 derived shield seat is not wired in " + EquipSrc +
                             " - the drawn shield is back to IDENTITY with no derivation of any kind.");
            if (!sheathedRow && !derivedSheathWired)
                failures.Add("[starter-shield-key] the LIVE shield mesh '" + meshKey + "' has no authored " +
                             "'@sheathed' row AND the derived sheathed seat is not wired - the back carry " +
                             "is back to the hand-typed (0,90,192) with no relationship to this mesh.");
            if (!manualReaderWired)
                failures.Add("[starter-shield-key] nothing in " + EquipSrc + " reads the catalog's `manual` " +
                             "flag - a derived pass could overwrite an owner-dialled row (WO-1123 sec 1.2).");

            return "starter shield '" + offHandId + "' -> mesh key '" + meshKey + "' (drawnRow=" + drawnRow +
                   " sheathedRow=" + sheathedRow + " derivedDrawn=" + derivedDrawnWired +
                   " derivedSheathed=" + derivedSheathWired + ")";
        }

        /// <summary>The offset-registry key the equip path uses: the mesh name. For an Addressable
        /// row that is the address's last segment ("gear/weapon/ShieldWithItemLogic" ->
        /// "ShieldWithItemLogic"); otherwise the resource path's last segment; else the id.</summary>
        private static string MeshKeyFor(WeaponDef def)
        {
            string path = def != null ? def.prefabPath : null;
            if (string.IsNullOrEmpty(path)) return def != null ? def.id : null;
            int slash = path.LastIndexOf('/');
            return slash >= 0 && slash < path.Length - 1 ? path.Substring(slash + 1) : path;
        }

        private static void Require(List<string> failures, string src, string token, string why)
        {
            if (!src.Contains(token))
                failures.Add("[tripwire-wiring] token '" + token + "' absent from " + EquipSrc + " - " + why + ".");
        }
    }
}
