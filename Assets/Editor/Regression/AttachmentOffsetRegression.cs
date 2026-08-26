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
//       has a usable scale and a non-identity rotation. Exact euler VALUES are
//       canon data the owner may re-dial - asserted non-degenerate, never pinned
//       to constants.
//       ⚠ THE '@sheathed' HALF OF THIS LINE IS RETIRED AND INVERTED (2026-08-20).
//       It used to read "'shield_A@sheathed' exists for the back carry" and the
//       case REQUIRED that row. The owner's hip ruling retired the back carry and
//       made the sheathed pose DERIVED, so a shipped absolute @sheathed row now
//       REPLACES the derivation instead of preserving a dial - proven on the live
//       Knight in Builds/KnightGearProof/. The case now asserts the opposite; see
//       Case1_RegistryRows for the captured trace lines.
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
            string shieldSummary = "";
            try
            {
                Case(failures, "registry-rows",   () => rowSummary = Case1_RegistryRows(failures));
                Case(failures, "tripwire-wiring", () => Case2_TripwireWiringLint(failures));
                Case(failures, "seat-precedence", () => Case3_SeatPrecedence(failures));
                Case(failures, "starter-shield-key", () => starterSummary = Case4_StarterShieldKey(failures));
                Case(failures, "staff-neutral-default", () => Case5_StaffNeutralDefault(failures));
                Case(failures, "shield-seat-substantiation",
                     () => shieldSummary = Case6_ShieldSeatSubstantiation(failures));
                Case(failures, "drawn-seat-verticality",
                     () => Case7_DrawnSeatVerticality(failures));
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
                         "; " + starterSummary + "; " + shieldSummary;
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

            // ⛔ THE '@sheathed' RULE IS INVERTED, AND DELIBERATELY (2026-08-20).
            //
            // This line used to read: `if (!TryGetOffset("shield_A@sheathed", out _)) failures.Add(
            // "'shield_A@sheathed' row MISSING - the back-carry pose falls to the built-in default
            // euler; the owner's sheathed dial is lost.")` — i.e. it REQUIRED a shipped absolute
            // sheathed row. That rule was correct while the sheathed carry was a BACK carry posed
            // by a hand-dialed euler. The owner's 2026-08-20 ruling ("sheathed should sit inverted
            // with the longest mesh (y) up and down attached to hip bone") retired that carry: the
            // pose is now DERIVED from the body's own axes at a HIP socket, and an absolute row is
            // applied by ApplySheathedOffset as pos+rot IN THE SOCKET'S FRAME — a frame that moved
            // from spine to hip. So the surviving rows were not a preserved dial; they were a stale
            // absolute pose that REPLACED the new derivation.
            //
            // MEASURED, not argued (Builds/KnightGearProof/, play-mode capture on the live Knight):
            //   [Flow:Equip]  sheathed long axis ... tiltFromVertical=0deg longAxisDotUp=-1
            //                 socket='SheatheSocket_HipMain'          <- the ruled pose, computed
            //   [Flow:Offset] sheathed offset 'sword_A@sheathed' applied:
            //                 pos=(0.23,-0.14,0.12) rot=(180,-28,-51) full=True   <- and discarded
            // whose -28 is literally the retired baldric diagonal, and whose POSITIVE x moved the
            // sword onto the SHIELD's hip. On screen: both props on one side, sword diagonal, off
            // the body. Requiring that row is requiring the bug.
            //
            // Keeping the row asserted PRESENT would also have made this suite fight
            // SheathePoseRegression D1, which now asserts the shipped table carries no absolute
            // sheathed override at all. Two suites cannot hold opposite rules about one file.
            // The owner's felt-tunes are untouched: the Seating Editor writes to
            // persistentDataPath (AttachmentOffsetRegistry.DevPath), never to the shipped table.
            if (AttachmentOffsetRegistry.TryGetOffset("shield_A@sheathed", out var sheathedRow) && sheathedRow.fullOverride)
                failures.Add("[registry-rows] 'shield_A@sheathed' is back as an ABSOLUTE (fullOverride) " +
                             "row. That REPLACES the derived hip carry the 2026-08-20 ruling installed - " +
                             "it is the stale back-socket pose, expressed in a frame that no longer exists. " +
                             "Delete it, or author the felt-tune through the Seating Editor so it lands in " +
                             "persistentDataPath instead of shipping to every player.");

            return "registry " + rows + " rows; shield_A present" +
                   " (fullOverride=" + (AttachmentOffsetRegistry.TryGetOffset("shield_A", out var d2) && d2.fullOverride) +
                   ") + no absolute shield_A@sheathed override (hip carry is derived)";
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

        // ⭐ RE-POINTED 2026-08-26 BY OWNER RULING — this case used to pin _staffGripEuler to
        // NEUTRAL (0f,0f,0f) as a WO-970 residual (once WeaponBoundsOrient learned to put a Z-long
        // staff on +Y, the old +90Y compensation was stranded, so neutral was the safe default).
        // That pin encoded the PRE-RULING state and is exactly why six staff fixes bounced: any
        // repair that gave the staff its own drawn correction reddened this case and got reverted.
        //
        // The owner ruled on 2026-08-26, felt-testing in combat: *"staff drawn is showing
        // horizontal"* / *"should be up and down vertical"*, with her standing rule *"the pointed
        // object is Y top, flat is bottom"*. THE DRAWN STAFF STANDS VERTICAL, long axis on the
        // body's up axis, pointed end up. The archetype correction that achieves it is
        // EquipmentController.StaffDrawnGripNudgeDefault = (90,0,0) — derived, not dialled: with
        // the shipped rig axes the rig-aware step is the IDENTITY, so the seat reduces to
        // Euler(N)*(0,1,0), and Euler(90,0,0) is the unique X-rotation sending the prop's +Y (its
        // tip) onto the grip-up axis (+Z), which the hand bone carries to world UP.
        //
        // THE PIN MOVES WITH THE RULING — it is not deleted. It still does the two jobs it was
        // written for: (a) exactly ONE staff default exists, so a stray second one cannot drift,
        // and (b) the independently authored WAND +90Y calibration is untouched by a staff repair.
        // It now additionally guards that the correction is sourced from the single shared constant
        // rather than re-typed, so the game and the [drawn-seat-verticality] oracle cannot disagree.
        // ⛔ DRAWN pose only. Nothing here licenses touching the sheathed seat or _sheatheLongAxisSign
        // (WO-1136).
        private static void Case5_StaffNeutralDefault(List<string> failures)
        {
            if (!File.Exists(EquipSrc))
            {
                failures.Add("[staff-neutral-default] source not found: " + EquipSrc);
                return;
            }

            string src = Regex.Replace(File.ReadAllText(EquipSrc), @"//[^\r\n]*", "");

            // The field must take its value from the ONE shared owner-ruled constant.
            MatchCollection staffDefaults = Regex.Matches(src,
                @"_staffGripEuler\s*=\s*StaffDrawnGripNudgeDefault\s*;");
            if (staffDefaults.Count != 1)
                failures.Add("[staff-neutral-default] expected exactly one _staffGripEuler default sourced " +
                             "from StaffDrawnGripNudgeDefault; found " + staffDefaults.Count +
                             ". Owner ruling 2026-08-26 (\"staff drawn is showing horizontal\" / \"should be " +
                             "up and down vertical\"): the drawn staff stands vertical, and its archetype " +
                             "correction lives in ONE constant so the game and the [drawn-seat-verticality] " +
                             "oracle cannot disagree. Do not re-type the euler at the field.");

            // ...and that constant must still be the owner-ruled value. (90,0,0) is DERIVED: with the
            // shipped rig axes the rig-aware step is the identity, so Euler(90,0,0) is the unique
            // X-rotation putting the prop's +Y tip on the grip-up axis == the body's vertical, tip up.
            MatchCollection staffConst = Regex.Matches(src,
                @"StaffDrawnGripNudgeDefault\s*=\s*new\s+Vector3\s*\(\s*90f\s*,\s*0f\s*,\s*0f\s*\)");
            if (staffConst.Count != 1)
                failures.Add("[staff-neutral-default] expected exactly one StaffDrawnGripNudgeDefault = " +
                             "new Vector3(90f, 0f, 0f); found " + staffConst.Count + ". That value IS the " +
                             "owner ruling of 2026-08-26 (drawn staff vertical, pointed end up). Neither the " +
                             "retired +90Y yaw compensation (WO-970) nor a return to neutral (0,0,0) is " +
                             "correct any more - neutral is what left the staff lying across the body.");

            Require(failures, src, "_wandGripEuler  = new Vector3(0f, 90f, 0f)",
                "the independent wand +90Y calibration changed with the staff repair");
            Require(failures, src, "case WeaponClass.Staff:  return _staffGripEuler;",
                "staff no longer consumes its archetype calibration - the owner-ruled drawn correction would never reach the seat");
            Require(failures, src, "return Staff(\"staff_A\");",
                "the shipped staff_A fallback no longer routes through the Staff weapon definition");
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
        //     WO-1123 was raised to end;
        //   - the live key has a DRAWN row, no '@sheathed' row, and the sheathed derivation carries
        //     no gate of its own (added 2026-08-20). "Wired" and "reached" are different properties,
        //     and this case asserted only the first for a month while the shield sat on a hand-typed
        //     euler in every capture.
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
            // ⚠ THIS USED TO READ `src.Contains("ComputeSheathedOffHandRotation(back)")` — the NAME of
            // a local variable. The owner's 2026-08-20 instruction moved the sheathed props to two
            // per-slot HIP sockets, so `back` became `sheatheOff`, and this went red while the seam it
            // guards was still wired exactly as required. A guard pinned to a spelling fails on a
            // rename and says nothing about a repeal. It now matches the CALL SHAPE: the derivation
            // method invoked with a single argument that is not its own parameter declaration.
            bool derivedSheathWired = Regex.IsMatch(
                src, @"ComputeSheathedOffHandRotation\(\s*(?!Transform\b)[A-Za-z_]\w*\s*\)");
            // AND THE GATE, which is the half this suite could not see before. The derivation being
            // PRESENT in the file never meant it RAN: it was gated on _currentOffHandDerivable, which
            // folds in the DRAWN pose's authored row. This very shield has one, so the device capture
            // (logs/device/2026-08-20-equip.log) reads
            //   "off-hand seat NOT derived for 'knight_shield_starter' key='ShieldWithItemLogic':
            //    source=AuthoredOffset (authoredRow=True manual=False native=True fullOverride=False)"
            // and contains ZERO ShieldFrame lines in the whole session — the frame was never measured,
            // and the sheathed shield sat on the hand-typed (0,90,192) with this case GREEN. A row
            // dialled for the hand must not speak for the pose on the hip.
            bool sheathGateIsOwn = src.Contains("_currentOffHandSheathDerivable");
            bool manualReaderWired = src.Contains("IsManualOrientRow");

            if (!drawnRow && !derivedDrawnWired)
                failures.Add("[starter-shield-key] the LIVE shield mesh '" + meshKey + "' has no authored " +
                             "drawn row AND the WO-1123 derived shield seat is not wired in " + EquipSrc +
                             " - the drawn shield is back to IDENTITY with no derivation of any kind.");
            if (!sheathedRow && !derivedSheathWired)
                failures.Add("[starter-shield-key] the LIVE shield mesh '" + meshKey + "' has no authored " +
                             "'@sheathed' row AND the derived sheathed seat is not wired - the sheathed " +
                             "carry is back to the hand-typed (0,90,192) with no relationship to this mesh.");
            if (!sheathedRow && drawnRow && !sheathGateIsOwn)
                failures.Add("[starter-shield-key] '" + meshKey + "' has an authored DRAWN row but no " +
                             "'@sheathed' row, and the sheathed derivation has no gate of its own " +
                             "(_currentOffHandSheathDerivable absent from " + EquipSrc + "). That is the " +
                             "exact shape that shipped broken: the drawn row switches off the SHEATHED " +
                             "derivation, the shield frame is never measured, and the sheathed pose " +
                             "silently falls back to the hand-typed (0,90,192) while the derivation sits " +
                             "in the file looking wired.");
            if (!manualReaderWired)
                failures.Add("[starter-shield-key] nothing in " + EquipSrc + " reads the catalog's `manual` " +
                             "flag - a derived pass could overwrite an owner-dialled row (WO-1123 sec 1.2).");

            return "starter shield '" + offHandId + "' -> mesh key '" + meshKey + "' (drawnRow=" + drawnRow +
                   " sheathedRow=" + sheathedRow + " derivedDrawn=" + derivedDrawnWired +
                   " derivedSheathed=" + derivedSheathWired + " sheathGateIsOwn=" + sheathGateIsOwn + ")";
        }

        // =====================================================================
        //  Case 6 - WO-1215: every shield seats from SOMETHING, and `manual` only
        //           vetoes when it names a correction that exists
        // =====================================================================
        //
        // THE DEFECT THIS PINS (owner felt-test 2026-08-26, tmp/shield-seat-101829.png): a dropped
        // shield rendered as a flat slab through the hero's chest. 18 of the 19 shield rows in
        // weapons.json are `generated:true` + `manual:true` with NO row in offsets.json, so the
        // precedence ladder's manual tier vetoed the derived seat to protect a pose nobody had ever
        // dialled — and the NATIVE addressable path's fallback is IDENTITY, which
        // ARCHITECTURE_PRINCIPLES §4 bans by name.
        //
        // WHAT BREAKS EACH ASSERTION (so none of them is decorative):
        //   - delete WeaponDef.generated again  -> every generated row reads generated:false, the
        //     substantiation test passes vacuously, and the 18 shields go back to vetoing. The
        //     catalog probe below reddens on the row that authors generated:true.
        //   - make ManualSeatIsSubstantiated ignore hasAuthoredSeat -> tripo_shield_a loses its
        //     protection and the auto pass would overwrite the owner's shield_A dial. Reddens.
        //   - make it ignore `manual` entirely  -> the 4 hand-authored manual rows lose canon.
        //   - re-point EquipmentController at the 2-arg MayDerive -> the source lint reddens.
        //   - touch the shield_A row in offsets.json -> the byte-guard below names the field.
        private static string Case6_ShieldSeatSubstantiation(List<string> failures)
        {
            // ── 6a. THE PURE TRUTH TABLE. Scene-free, mesh-free, hero-free. ──────────────────
            ExpectBool(failures, WeaponOrientHelper.ManualSeatIsSubstantiated(false, false, false), false,
                "a row that does not claim manual can never be 'substantiated manual'");
            ExpectBool(failures, WeaponOrientHelper.ManualSeatIsSubstantiated(false, true, true), false,
                "manual:false stays false however the row was produced");
            ExpectBool(failures, WeaponOrientHelper.ManualSeatIsSubstantiated(true, false, false), true,
                "a HAND-AUTHORED row (generated:false) that claims manual is trusted unconditionally " +
                "- a human wrote the row, so a human may have meant the flag");
            ExpectBool(failures, WeaponOrientHelper.ManualSeatIsSubstantiated(true, true, true), true,
                "a generated row WITH an authored Offset Forge seat is canon - this is exactly " +
                "tripo_shield_a -> shield_A, and it must never lose its protection");
            ExpectBool(failures, WeaponOrientHelper.ManualSeatIsSubstantiated(true, true, false), false,
                "a MACHINE-EMITTED row claiming manual with NO authored seat behind it names a " +
                "correction that does not exist - honouring it preserves IDENTITY, not a dial. " +
                "This single cell IS the WO-1215 defect");

            // ── 6b. THE GATE THE CALL SITE ACTUALLY USES ─────────────────────────────────────
            if (!WeaponOrientHelper.MayDerive(false, true, true))
                failures.Add("[shield-seat-substantiation] MayDerive(authored=false, manual=true, " +
                             "generated=true) is FALSE - the 18 unseated shields still veto their own " +
                             "derivation and stay at identity. This is the reported defect, unfixed.");
            if (WeaponOrientHelper.MayDerive(false, true, false))
                failures.Add("[shield-seat-substantiation] MayDerive(manual=true, generated=false) is " +
                             "TRUE - a derived pass would overwrite a HAND-AUTHORED manual row. The " +
                             "WO-1215 narrowing has overshot into the canon it was written to keep.");
            if (WeaponOrientHelper.MayDerive(true, true, true))
                failures.Add("[shield-seat-substantiation] MayDerive(authoredRow=true, ...) is TRUE - " +
                             "an Offset Forge seat the owner dialled by eye would be overwritten.");

            // ── 6c. THE SHIPPED CATALOG, RE-PARSED. Not a restated constant. ─────────────────
            AttachmentOffsetRegistry.Reload();
            int shields = 0, nowDerivable = 0, protectedByAuthoredSeat = 0, stillVetoed = 0;
            var stuck = new List<string>();
            foreach (var def in GearCatalog.AllWeapons())
            {
                if (def == null || !def.IsOffHandItem) continue;
                shields++;
                string meshKey = MeshKeyFor(def);
                bool authored = !string.IsNullOrEmpty(meshKey) &&
                                (AttachmentOffsetRegistry.TryGetOffset(meshKey, out _) ||
                                 AttachmentOffsetRegistry.TryGetOffset(def.id, out _));
                bool derivable = WeaponOrientHelper.MayDerive(authored, def.manual, def.generated);
                if (authored) protectedByAuthoredSeat++;
                if (derivable) nowDerivable++;
                else if (!authored)
                {
                    // No authored row AND not derivable = the prop takes the archetype constant,
                    // which on the native addressable path is identity. That is the defect, and it
                    // must be IMPOSSIBLE for a shield to land here.
                    stillVetoed++;
                    stuck.Add(def.id + "(manual=" + def.manual + " generated=" + def.generated + ")");
                }
            }
            if (shields == 0)
                failures.Add("[shield-seat-substantiation] weapons.json resolved ZERO category:shield " +
                             "rows - the catalog did not load, so nothing below was actually measured.");
            if (stuck.Count > 0)
                failures.Add("[shield-seat-substantiation] " + stuck.Count + " shield row(s) have NO " +
                             "authored Offset Forge seat AND cannot derive one, so they attach at the " +
                             "archetype constant (IDENTITY on the native addressable path - " +
                             "ARCHITECTURE_PRINCIPLES §4): " + string.Join(", ", stuck));

            // The protection half, named by id so a future narrowing cannot quietly drop it.
            var dialled = GearCatalog.FindWeapon("tripo_shield_a");
            if (dialled == null)
                failures.Add("[shield-seat-substantiation] 'tripo_shield_a' did not load - the " +
                             "protected-row probe could not run.");
            else if (WeaponOrientHelper.MayDerive(
                         AttachmentOffsetRegistry.TryGetOffset("shield_A", out _),
                         dialled.manual, dialled.generated))
                failures.Add("[shield-seat-substantiation] 'tripo_shield_a' is now DERIVABLE. It has " +
                             "the owner's hand-dialled 'shield_A' row (rot -160/-180/-84, dialled " +
                             "2026-07-07); a derived pass over it would overwrite canon.");

            // ── 6d. THE shield_A BYTE-GUARD. WO-1215 acceptance 4, made permanent. ───────────
            // Asserted VALUE BY VALUE rather than "it exists", because "the row is still there" is
            // what Case 1 already says and is not what acceptance 4 asks. These are the numbers
            // read off Assets/OffsetForge/offsets.json at source on 2026-08-26, before the change.
            if (!AttachmentOffsetRegistry.TryGetOffset("shield_A", out var sa))
                failures.Add("[shield-seat-substantiation] 'shield_A' MISSING - the owner's dialled " +
                             "shield seat is gone.");
            else
            {
                ExpectVec(failures, sa.eulerRot, new Vector3(-160f, -180f, -84f), "shield_A rot");
                ExpectVec(failures, sa.pos, new Vector3(0.12f, -0.01f, 0f), "shield_A pos");
                if (Mathf.Abs(sa.scale - 1.04f) > 1e-3f)
                    failures.Add("[shield-seat-substantiation] shield_A scale is " + sa.scale +
                                 ", owner-dialled value is 1.04 - a derived/auto pass has moved it.");
                if (!sa.fullOverride)
                    failures.Add("[shield-seat-substantiation] shield_A is no longer fullOverride.");
            }

            // ── 6e. SOURCE LINT: the call site must use the 3-arg gate. ──────────────────────
            string src = File.Exists(EquipSrc) ? Regex.Replace(File.ReadAllText(EquipSrc), @"//[^\r\n]*", "") : "";
            if (!src.Contains("ManualSeatIsSubstantiated"))
                failures.Add("[shield-seat-substantiation] " + EquipSrc + " no longer calls " +
                             "WeaponOrientHelper.ManualSeatIsSubstantiated - the raw catalog flag is " +
                             "back in the ladder and the 18 shields veto themselves again.");
            if (!src.Contains("IsGeneratedCatalogRow"))
                failures.Add("[shield-seat-substantiation] " + EquipSrc + " no longer reads " +
                             "WeaponDef.generated - the substantiation test cannot tell a machine " +
                             "stamp from an owner dial and will trust every stamp.");

            return "shields " + shields + ": derivable " + nowDerivable + ", authored-seat " +
                   protectedByAuthoredSeat + ", stuck-at-constant " + stillVetoed +
                   "; shield_A dial byte-checked";
        }

        private static void ExpectBool(List<string> failures, bool actual, bool expected, string why)
        {
            if (actual != expected)
                failures.Add("[shield-seat-substantiation] expected " + expected + " but got " +
                             actual + " - " + why + ".");
        }

        private static void ExpectVec(List<string> failures, Vector3 actual, Vector3 expected, string what)
        {
            if ((actual - expected).sqrMagnitude > 1e-4f)
                failures.Add("[shield-seat-substantiation] " + what + " is " + actual +
                             ", owner-dialled value is " + expected +
                             " - the hand-dialled seat was overwritten (ARCHITECTURE_PRINCIPLES §4).");
        }

        /// <summary>The offset-registry key the equip path uses: the mesh name. For an Addressable
        /// row that is the address's last segment ("gear/weapon/ShieldWithItemLogic" ->
        /// "ShieldWithItemLogic"); otherwise the resource path's last segment; else the id.</summary>
        // =====================================================================
        //  Case 7 - WO-1226: THE SEATED WORLD ROTATION, NOT THE DERIVED VALUE
        // =====================================================================
        //
        // WHY THIS CASE HAD TO BE NEW. Six prior commits asserted the DERIVER. The one number the
        // shipped build prints - "sheathed long axis ... tiltFromVertical=0deg" - is computed
        // inside ComputeSheathRotation from the quaternion that method is about to RETURN, and it
        // asks Vector3.up rather than the mesh. It is therefore green on a build in which the staff
        // is visibly lying across the body, which is exactly what the owner captured on 2026-08-26
        // on TWO builds. An assertion built on that number can never redden. This case asserts the
        // COMPOSED SEAT instead, through the SHIPPED composition function, on a synthetic bone -
        // no scene, no rig, no hero.
        //
        // WHAT IT PROVES, in the owner's own words ("the pointed object is Y top, flat is bottom"):
        // take a prop whose long axis is prop-local +Y (NormalizeInto guarantees this on the
        // geometry path, which is the path every staff takes - no staff mesh has an offsets.json
        // row, so fullOverride/native never apply), seat it with the DRAWN melee composition for
        // its archetype, hang it on a hand bone that points its local +Y horizontally out of the
        // fist, and ask how far the long axis ends up from the body's vertical.
        //
        //   - SWORD: expected to lie forward out of the fist. That is the archetype, it is
        //     felt-verified, and the case asserts it STAYS that way (a guard against "fixing" the
        //     staff by rotating every melee family).
        //   - STAFF: the owner's rule stands it UP. ComposeMeleeGripRotation with the shipped
        //     defaults - _handBladeAxis (0,1,0), _handGripUpAxis (0,0,1) - is
        //     Quaternion.LookRotation((0,0,1),(0,1,0)) == IDENTITY, so the whole drawn seat reduces
        //     to Euler(N)*RotY(180) and EVERY degree of staff verticality comes from the archetype
        //     nudge N. This case reads N from the shipped constant and measures the result.
        //
        // ⭐ RESOLVED 2026-08-26 BY OWNER RULING. This case was RED BY CONSTRUCTION while N was
        // (0,0,0): the staff inherited the bone's horizontal blade axis - the sword rule, applied
        // to a staff. The owner then ruled ("staff drawn is showing horizontal" / "should be up and
        // down vertical"), N became EquipmentController.StaffDrawnGripNudgeDefault = (90,0,0), and
        // the case goes green FOR THE RIGHT REASON: the composed seat genuinely puts the shaft on
        // the body's vertical, tip up. The collision this case existed to surface is also resolved
        // - Case5_StaffNeutralDefault no longer pins the WO-970 neutral; the pin MOVED WITH THE
        // RULING and now pins the same constant. The two cases agree again, on the ruled value.
        // ⛔ If this ever reddens, the ROTATION is wrong. Fix the rotation. Never the oracle.
        private static void Case7_DrawnSeatVerticality(List<string> failures)
        {
            // A hand bone whose LOCAL +Y points along world +Z - "out of the fist", horizontal,
            // which is what the shipped _handBladeAxis (0,1,0) selects. Quaternion.LookRotation
            // (forward: +Y-of-world? no) - build it explicitly: we want local +Y -> world +Z and
            // local +Z -> world -Y, i.e. a -90 deg pitch about X.
            Quaternion handWorld = Quaternion.Euler(-90f, 0f, 0f);
            Vector3 bodyUp = Vector3.up;

            // The prop's long axis after NormalizeInto is prop-local +Y, and the grip root carries
            // it unrotated, so the long axis in grip-root-local space is +Y.
            Vector3 longAxisLocal = Vector3.up;

            // Sanity on the probe itself: if the bone did not point local +Y horizontally, the
            // whole case is measuring nothing. (A probe that cannot fail is the WO-1138 lesson.)
            Vector3 boneBladeWorld = handWorld * Vector3.up;
            if (Vector3.Angle(boneBladeWorld, bodyUp) < 60f)
            {
                failures.Add("[drawn-seat-verticality] the synthetic hand bone is not horizontal " +
                             "(bone local +Y -> " + boneBladeWorld.ToString("0.###") + "); the case " +
                             "would pass for the wrong reason. Fix the probe, not the game.");
                return;
            }

            // SWORD - the felt-verified archetype. It should NOT stand up; assert it stays put so a
            // staff repair that rotates all melee is caught here instead of on the owner's screen.
            Quaternion swordSeat = EquipmentController.ComposeDrawnMeleeLocalRotation(
                new Vector3(0f, 1f, 0f), new Vector3(0f, 0f, 1f), new Vector3(-25f, 90f, 0f));
            var sword = EquipmentController.MeasureSeatedLongAxis(
                handWorld, swordSeat, longAxisLocal, bodyUp);
            if (sword.TiltFromVerticalDeg < 40f)
                failures.Add("[drawn-seat-verticality] the SWORD now stands up (tiltFromVertical=" +
                             sword.TiltFromVerticalDeg.ToString("0.#") + "deg). The bladed archetype " +
                             "is felt-verified extending forward from the fist (owner 2026-08-19); a " +
                             "staff repair must not rotate every melee family to fix one.");

            // STAFF - the owner's rule, now ruled and implemented (2026-08-26). The nudge is READ
            // FROM THE SHIPPED CONSTANT, never re-typed here: re-typing is precisely how six prior
            // fixes asserted a rotation the game never applied ("derivation is not self-proving").
            Quaternion staffSeat = EquipmentController.ComposeDrawnMeleeLocalRotation(
                new Vector3(0f, 1f, 0f), new Vector3(0f, 0f, 1f),
                EquipmentController.StaffDrawnGripNudgeDefault);
            var staff = EquipmentController.MeasureSeatedLongAxis(
                handWorld, staffSeat, longAxisLocal, bodyUp);
            if (staff.TiltFromVerticalDeg > 30f)
                failures.Add("[drawn-seat-verticality] THE DRAWN STAFF IS LYING ACROSS THE BODY: " +
                             "seated long axis is " + staff.TiltFromVerticalDeg.ToString("0.#") +
                             "deg off the body's vertical (owner rule: 'the pointed object is Y top, " +
                             "flat is bottom' - it should stand). Composed seat localEuler=" +
                             staffSeat.eulerAngles.ToString("0.#") + ", long axis in the bone's frame=" +
                             staff.ParentUnit.ToString("0.###") + ", in WORLD=" +
                             staff.WorldUnit.ToString("0.###") + ". PROVING CHAIN: " +
                             "ComposeMeleeGripRotation with the shipped _handBladeAxis (0,1,0) / " +
                             "_handGripUpAxis (0,0,1) is Quaternion.LookRotation((0,0,1),(0,1,0)) == " +
                             "IDENTITY, so the drawn staff seat reduces to Euler(N)*RotY(180) and the " +
                             "shaft lands wherever the ARCHETYPE NUDGE N puts it. N is read here from " +
                             "EquipmentController.StaffDrawnGripNudgeDefault, which ships as " +
                             EquipmentController.StaffDrawnGripNudgeDefault.ToString("0.#") + ". The " +
                             "owner-ruled value is (90,0,0): Euler(90,0,0)*(0,1,0) = (0,0,1) = the " +
                             "grip-up axis, which the hand bone carries to the body's vertical. If this " +
                             "line is firing, that constant has been moved off the ruling (owner " +
                             "2026-08-26: 'staff drawn is showing horizontal' / 'should be up and down " +
                             "vertical'). Neither staff_A nor tripo_staff_a has an offsets.json row, so " +
                             "nothing else corrects it. FIX THE CONSTANT, NOT THIS ORACLE. Do NOT flip " +
                             "_sheatheLongAxisSign (WO-1136) - that is the SHEATHED path.");

            // DIRECTION, not just axis: TiltFromVerticalDeg is folded to 0..90 on purpose (a
            // tip-down staff still reads 0), so it alone cannot tell "stands up" from "stands
            // upside down". The owner's standing rule is "the pointed object is Y top, flat is
            // bottom", and the prop's tip is prop-local +Y - so the seated axis must point along
            // body UP, not against it. This is an ADDED assertion; it narrows nothing.
            float dotUp = Vector3.Dot(staff.WorldUnit, bodyUp);
            if (dotUp < 0.5f)
                failures.Add("[drawn-seat-verticality] the drawn staff is not POINTED-END-UP: seated " +
                             "long axis . bodyUp = " + dotUp.ToString("0.##") + " (want ~+1). The prop's " +
                             "tip is prop-local +Y (owner: 'the pointed object is Y top, flat is bottom'), " +
                             "so a negative dot means the archetype correction stood the shaft up the " +
                             "wrong way round - the X nudge sign is inverted (+90, not -90).");
        }

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
