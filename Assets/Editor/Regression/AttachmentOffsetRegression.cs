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
            try
            {
                Case(failures, "registry-rows",   () => rowSummary = Case1_RegistryRows(failures));
                Case(failures, "tripwire-wiring", () => Case2_TripwireWiringLint(failures));
                Case(failures, "seat-precedence", () => Case3_SeatPrecedence(failures));
                Case(failures, "starter-shield-key", () => starterSummary = Case4_StarterShieldKey(failures));
                Case(failures, "staff-neutral-default", () => Case5_StaffNeutralDefault(failures));
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

        // WO-970 residual: once WeaponBoundsOrient learned to put a Z-long staff on +Y,
        // the old +90Y staff default became a stranded compensation. Keep the staff
        // neutral without erasing the independently authored wand calibration.
        private static void Case5_StaffNeutralDefault(List<string> failures)
        {
            if (!File.Exists(EquipSrc))
            {
                failures.Add("[staff-neutral-default] source not found: " + EquipSrc);
                return;
            }

            string src = Regex.Replace(File.ReadAllText(EquipSrc), @"//[^\r\n]*", "");
            MatchCollection staffDefaults = Regex.Matches(src,
                @"_staffGripEuler\s*=\s*new\s+Vector3\s*\(\s*0f\s*,\s*0f\s*,\s*0f\s*\)");
            if (staffDefaults.Count != 1)
                failures.Add("[staff-neutral-default] expected exactly one neutral _staffGripEuler default; found " +
                             staffDefaults.Count + ". The retired +90Y compensation must not return.");

            Require(failures, src, "_wandGripEuler  = new Vector3(0f, 90f, 0f)",
                "the independent wand +90Y calibration changed with the staff repair");
            Require(failures, src, "case WeaponClass.Staff:  return _staffGripEuler;",
                "staff no longer consumes its explicit neutral calibration");
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
