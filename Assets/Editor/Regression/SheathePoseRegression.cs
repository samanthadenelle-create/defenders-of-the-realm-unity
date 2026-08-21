// =============================================================================
// SheathePoseRegression — the SHEATHED carry hangs from the HIPS, one socket per
// slot, long axis vertical and inverted.  Marker: SHEATHE_POSE_OK / SHEATHE_POSE_FAIL
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression (references DeNelle.Core + DeNelle.Village).
// Standalone entry: DeNelle.Editor.SheathePoseRegression.RunAll (Exits 1 on failure).
//
// THE OWNER'S RULING (2026-08-20, verbatim):
//   "sheathed should sit inverted with the longest mesh (y) up and down attached to
//    hip bone"
// plus, separately, "Shield isnt showing either" and "starter loop shield sword
// placement issues too".
//
// WHY THIS SUITE EXISTS, AND WHY IT ASSERTS *BOTH* DIRECTIONS
// ---------------------------------------------------------------------------
// The owner asked for it in the same breath as the fix: "write a regression test to
// always confirm it too." A one-directional guard is nearly worthless here, because
// every one of the three defects it pins LOOKED FINE IN SOURCE and was only visible
// in a capture:
//
//   1. THE ANCHOR. ResolveBackSocket() asked for HumanBodyBones.Chest. On the live CC
//      rig that resolves to the LOW SPINE — the capture line is
//        "ResolveBackSocket on 'Hero (Blaise)': sheathe anchor under bone 'CC_Base_Spine01'."
//      so the "back" carry was already sitting at the waist. Reading the source, "Chest"
//      looks like the back. Only the bone name in the trace tells the truth.
//
//   2. ONE SOCKET, TWO PROPS. Both the main weapon and the off-hand parented to the
//      SAME transform at the SAME origin:
//        "AttachOffHandProp MEASURED after hold: id='knight_shield_starter'
//         parent='SheatheSocket_Back' state=SHEATHED worldBounds=c(-0.13, 0.81, -5.07)
//         s(0.72, 0.92, 0.72)"
//      The shield was never missing. It rendered a real 0.72 x 0.92 x 0.72 m volume,
//      inside the hero's body, where the player cannot see it. A null-check oracle
//      would have passed this bug forever.
//
//   3. THE DIAGONAL. ComputeSheathRotation leaned the long axis
//      _sheatheBladeDiagonalDeg (28) off vertical — a baldric carry — which on a
//      waist-height anchor reads as a sword lying sideways across the belt
//      (logs/device/sheathed-weapon.png).
//
// So each case below runs the SAME predicate over the shipped state (must PASS) and
// over a tombstone of the KNOWN-BAD state (must FAIL). A rule that cannot fail on the
// bug it was written for is a rule that is not being evaluated, and this project has
// shipped that mistake before.
//
// HOW EACH CASE IS DRIVEN
// ---------------------------------------------------------------------------
//   (a) REAL GEOMETRY, REAL METHOD. Cases G1-G4 build a GameObject, AddComponent the
//       SHIPPED EquipmentController and invoke its private ComputeSheathRotation
//       through reflection. That method reads only `_animator` (null in a headless
//       editor, so it falls back to the component's own transform — which this suite
//       owns and rotates to an awkward angle on purpose) and the socket's rotation.
//       No play session, no Avatar, no re-implementation of the math: a re-implemented
//       copy could pass while the shipped path fails, which is worth nothing.
//       EquipmentController has no [ExecuteAlways], so AddComponent runs no Awake here.
//   (b) REAL HELPER. Case G5 drives WeaponOrientHelper.ComputeShieldMountRotation, the
//       same entry point the sheathed shield pose calls, with a synthetic ShieldFrame.
//   (d) REAL RENDERER, REAL MEASUREMENT. Cases G8/G9 build an actual MeshRenderer plate
//       at the live shield's proportions, run the shipped TryResolveShieldFrame on it at
//       a hostile attach rotation, apply the shipped pose and assert the RENDERED WORLD
//       VOLUME. Added 2026-08-20 second pass, after every angle in this suite read
//       perfect while the shield rendered flat on the owner's device.
//
// ⚠ THE SHIELD DOES NOT TAKE THE SWORD'S "INVERTED" RULE (owner ruling scope, 2026-08-20).
// The instruction "sheathed should sit inverted with the longest mesh (y) up and down" is
// about a SWORD - the long axis is the blade, inverted is tip-down in a scabbard. A shield
// has no tip and no meaningful end-for-end. It keeps the felt-approved WO-1123 rule
// (thickness away from the player, handle inward), applied at the new HIP anchor. G1-G4
// assert the sword rule; G5-G9 assert the shield rule; neither is applied to the other.
//   (c) SOURCE LINT for the two facts that are structural rather than numeric (which
//       BONE is asked for, and which VARIABLE each slot parents to). Both lints run on
//       COMMENT-BLANKED source — the tombstone comments in EquipmentController name the
//       retired bone and the retired socket name, and a rule that can match its own
//       tombstone punishes the author for documenting the fix.
// =============================================================================

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using DeNelle.Core.Geometry;
using DeNelle.Village;

namespace DeNelle.Editor
{
    /// <summary>
    /// Pins the owner's 2026-08-20 sheathe ruling: hip anchor, one socket per slot,
    /// long axis vertical + inverted. Returns true (summary) / false (detail); never throws.
    /// </summary>
    public static class SheathePoseRegression
    {
        private const string EquipRelPath = "_Modules/Village/Hero/EquipmentController.cs";

        // Angular slack. 2 deg is far tighter than any defect this pins (the retired
        // diagonal was 28 deg and the retired horizontal read ~90) and far looser than
        // float noise through two quaternion compositions.
        private const float AngleTolDeg = 2f;

        // ─────────────────────────────────────────────────────────────────────
        //  TOMBSTONES — the code as it stood BEFORE the ruling. Every predicate is
        //  run over these too and MUST reject them. They are deliberately verbatim
        //  (bone name, socket name, shared variable) rather than paraphrased: a
        //  paraphrase drifts away from the bug until the guard no longer covers it.
        // ─────────────────────────────────────────────────────────────────────
        private const string TombstoneAnchor = @"
        private Transform ResolveBackSocket()
        {
            if (_backSocket != null) return _backSocket;
            if (_animator == null || !_animator.isHuman) return null;
            Transform anchor = _animator.GetBoneTransform(HumanBodyBones.Chest);
            if (anchor == null) anchor = _animator.GetBoneTransform(HumanBodyBones.Spine);
            if (anchor == null) anchor = _animator.GetBoneTransform(HumanBodyBones.UpperChest);
            if (anchor == null) return null;
            var go = new GameObject(NAME);
            go.transform.SetParent(anchor, false);
            _backSocket = go.transform;
            return _backSocket;
        }
";

        private const string TombstoneSharedSocket = @"
        private void ApplyHoldPose()
        {
            bool drawn = _combatActive;
            Transform back = drawn ? null : ResolveBackSocket();
            if (_gripRoot != null)
            {
                if (!drawn && back != null)
                {
                    _gripRoot.SetParent(back, false);
                }
            }
            if (_currentOffHandProp != null)
            {
                var offT = _currentOffHandProp.transform;
                if (!drawn && back != null)
                {
                    offT.SetParent(back, false);
                }
            }
        }
";

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var log = new StringBuilder();
            log.AppendLine("--- SHEATHE POSE (owner ruling 2026-08-20: hips, one socket per slot, vertical + inverted) ---");

            string equipPath = null;
            try { equipPath = Path.Combine(Application.dataPath, EquipRelPath.Replace('/', Path.DirectorySeparatorChar)); }
            catch { }

            if (string.IsNullOrEmpty(equipPath) || !File.Exists(equipPath))
            {
                // NOT a hollow pass. EquipmentController is the subject of this suite; if it
                // cannot be read there is nothing here to confirm and saying OK would be a lie.
                reason = "Assets/" + EquipRelPath + " not found — the sheathe pose cannot be verified.";
                Debug.LogError(log + "SHEATHE_POSE_FAIL: " + reason);
                return false;
            }

            string raw = ReadOrEmpty(equipPath);
            string codeOnly = BlankComments(raw);              // comments gone, string literals KEPT
            string codeNoStrings = BlankStringLiterals(codeOnly); // comments AND string contents gone

            // ── CASE L1: the sheathe anchor asks for the HIPS bone, first ─────────────
            RunPredicate(failures, log, "L1 anchor=Hips",
                AnchorIsHips, codeNoStrings, TombstoneAnchor,
                "the sheathe socket resolves to HumanBodyBones.Hips before any spine/chest fallback");

            // ── CASE L2: the two slots parent to DIFFERENT sockets ────────────────────
            // Run on the STRING-BLANKED source: ApplyHoldPose is full of interpolated trace
            // messages, and a `{` inside one of those would unbalance the brace scan and hand
            // the predicate half a method to reason about.
            RunPredicate(failures, log, "L2 separate sockets",
                SlotsUseSeparateSockets, codeNoStrings, TombstoneSharedSocket,
                "the main-hand and off-hand sheathed branches parent to two DIFFERENT socket variables");

            // ── CASE L2b: and the two sockets are two NAMED objects ───────────────────
            // Checked against comment-blanked source (string literals KEPT) so it reads the
            // real GameObject names, and so the retired name surviving only in a tombstone
            // comment cannot trip it.
            int beforeL2b = failures.Count;
            foreach (var socketName in new[] { "SheatheSocket_HipMain", "SheatheSocket_HipOff" })
                if (codeOnly.IndexOf(socketName, StringComparison.Ordinal) < 0)
                    failures.Add("L2b: no sheathe socket named " + socketName + " is created. Two slots need " +
                                 "two named anchors; one shared anchor is what buried the shield in the body.");
            if (codeOnly.IndexOf("SheatheSocket_Back", StringComparison.Ordinal) >= 0)
                failures.Add("L2b: the shared 'SheatheSocket_Back' anchor is back in the code. It was ONE " +
                             "transform carrying both props at one origin, under a bone that resolved to " +
                             "CC_Base_Spine01 — the reported waist-carry and the invisible shield in one object.");
            if (failures.Count == beforeL2b)
                log.AppendLine("  L2b two named hip sockets, no shared back socket ....... ok");

            // ── CASE L3: the sheathed shield derivation is not gated on the DRAWN row ─
            // (The capture's proving line: "off-hand seat NOT derived ... source=AuthoredOffset
            //  (authoredRow=True ...)" — an authored DRAWN row switched off the SHEATHED
            //  derivation, and not one ShieldFrame line was emitted in the entire session.)
            if (codeNoStrings.IndexOf("_currentOffHandSheathDerivable", StringComparison.Ordinal) < 0)
                failures.Add("L3: EquipmentController has no _currentOffHandSheathDerivable — the sheathed " +
                             "shield pose is gated on the DRAWN pose's precedence again, which is what left " +
                             "the shield posed by a retired back-carry constant");
            else
                log.AppendLine("  L3 sheathed derivability is its own flag ................ ok");

            // ── CASE S1: the two hip sides are opposite, by construction ──────────────
            CheckSidesAreOpposite(failures, log);

            // ── CASES G1-G4: the SHIPPED ComputeSheathRotation, driven for real ───────
            CheckShippedRotation(failures, log);

            // ── CASE G5: the shield's sheathed rule, through the real helper ──────────
            CheckShieldMountRule(failures, log);

            // ── CASES G8/G9: the shield's RENDERED SHAPE, on a real MeshRenderer ──────
            // The one that would have caught the flat shield before the owner saw it.
            CheckSheathedShieldRendersAsAPlate(failures, log);

            // ── CASE D1: no SHIPPED absolute @sheathed row can outrank the derivation ─
            CheckNoShippedAbsoluteSheathedRow(failures, log);

            // ── CASE D2: the DEFAULT flags do not un-do the derived pose ─────────────
            CheckSheathedDefaultsDoNotOverrideDerivation(failures, log);

            // ── CASE P1: a grip-at-origin shield is CENTRED on the hip, not hung by it ─
            CheckSheathedOffHandIsCentredOnSocket(failures, log);

            if (failures.Count == 0)
            {
                reason = "sheathe pose: hip anchor, one socket per slot, SWORD vertical + inverted (tip down), " +
                         "SHIELD face-outward and still rendering as a plate — every rule proven to REJECT " +
                         "the pre-2026-08-20 state and the flat-shield state.";
                Debug.Log(log + "SHEATHE_POSE_OK - " + reason);
                return true;
            }

            reason = string.Join(" | ", failures.ToArray());
            Debug.LogError(log + "SHEATHE_POSE_FAIL: " + reason);
            return false;
        }

        /// <summary>Standalone entry point (run-unity-method).</summary>
        public static void RunAll()
        {
            bool ok = Run(out string reason);
            Debug.Log(reason);
            if (!ok) EditorApplication.Exit(1);
        }

        // =====================================================================
        //  BOTH-DIRECTIONS HARNESS
        // =====================================================================

        private delegate bool SourcePredicate(string source, out string why);

        /// <summary>
        /// Runs one predicate over the SHIPPED source (must pass) and over the tombstone of
        /// the state it was written to catch (must fail). The second half is not ceremony:
        /// it is the only thing that distinguishes a rule that holds from a rule that cannot
        /// fire — and every defect in this suite passed a source read before it was captured.
        /// </summary>
        private static void RunPredicate(List<string> failures, StringBuilder log, string label,
                                         SourcePredicate predicate, string shipped, string tombstone,
                                         string contract)
        {
            bool shippedOk = predicate(shipped, out string shippedWhy);
            if (!shippedOk)
                failures.Add(label + ": SHIPPED CODE VIOLATES the ruling — " + contract + ". " + shippedWhy);

            bool tombstoneOk = predicate(tombstone, out _);
            if (tombstoneOk)
                failures.Add(label + ": the rule ACCEPTS the known-bad pre-2026-08-20 code, so it is not " +
                             "evaluating anything. Contract: " + contract);

            if (shippedOk && !tombstoneOk)
                log.AppendLine("  " + label.PadRight(24) + " shipped=PASS tombstone=REJECTED ... ok");
        }

        // =====================================================================
        //  PREDICATES (source-lint, comment-blanked)
        // =====================================================================

        private static bool AnchorIsHips(string source, out string why)
        {
            // The resolver is found by its SIGNATURE, not by file position, so the check
            // survives the method moving. Either name is accepted as the entry point: the
            // retired one must still be FOUND in order to be rejected on its bone.
            string body = ExtractMethodBody(source, "Transform ResolveSheatheSocket")
                       ?? ExtractMethodBody(source, "Transform ResolveBackSocket");
            if (body == null)
            {
                why = "no sheathe-socket resolver found (looked for ResolveSheatheSocket / ResolveBackSocket).";
                return false;
            }

            int hips = body.IndexOf("HumanBodyBones.Hips", StringComparison.Ordinal);
            if (hips < 0)
            {
                why = "the resolver never asks for HumanBodyBones.Hips. The owner's ruling names the HIP " +
                      "BONE; asking for Chest resolves to CC_Base_Spine01 on the live rig, which is the " +
                      "waist-height carry that was reported.";
                return false;
            }

            // Hips must be asked for FIRST. A Hips line placed after a Chest fallback would
            // never be reached on the rig that matters, and the trace would still say Spine01.
            foreach (var later in new[] { "HumanBodyBones.Chest", "HumanBodyBones.Spine", "HumanBodyBones.UpperChest" })
            {
                int at = body.IndexOf(later, StringComparison.Ordinal);
                if (at >= 0 && at < hips)
                {
                    why = "the resolver asks for " + later + " BEFORE HumanBodyBones.Hips, so the hips " +
                          "branch is unreachable on any rig that maps both.";
                    return false;
                }
            }

            // Two distinct socket objects means the resolver must be told WHICH slot it is for.
            if (body.IndexOf("offHand", StringComparison.Ordinal) < 0)
            {
                why = "the resolver takes no slot argument, so it can only ever produce ONE socket — the " +
                      "shared-anchor shape that buried the shield inside the body.";
                return false;
            }

            why = null;
            return true;
        }

        private static bool SlotsUseSeparateSockets(string source, out string why)
        {
            string body = ExtractMethodBody(source, "void ApplyHoldPose");
            if (body == null)
            {
                why = "ApplyHoldPose not found — cannot verify which socket each slot parents to.";
                return false;
            }

            // Collect the first argument of every SetParent(...) call in the method. The rule is
            // about the SHEATHED parents, so the hand parents are ignored by name below.
            var parents = new List<string>();
            int i = 0;
            while (true)
            {
                int at = body.IndexOf("SetParent(", i, StringComparison.Ordinal);
                if (at < 0) break;
                int start = at + "SetParent(".Length;
                int comma = body.IndexOf(',', start);
                int close = body.IndexOf(')', start);
                int end = comma >= 0 && (close < 0 || comma < close) ? comma : close;
                if (end < 0) break;
                parents.Add(body.Substring(start, end - start).Trim());
                i = end;
            }

            var sheatheParents = new List<string>();
            foreach (var p in parents)
                if (p.IndexOf("sheathe", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    p.IndexOf("back", StringComparison.OrdinalIgnoreCase) >= 0)
                    if (!sheatheParents.Contains(p)) sheatheParents.Add(p);

            if (sheatheParents.Count < 2)
            {
                why = "the sheathed branches parent to " + sheatheParents.Count + " distinct socket " +
                      "variable(s) [" + string.Join(", ", sheatheParents.ToArray()) + "]. Both props on ONE " +
                      "transform at ONE origin is exactly the capture's buried shield.";
                return false;
            }

            // (The socket NAMES are case L2b in Run — they live in string literals, which this
            // predicate's input has deliberately blanked so the brace scan is safe.)
            why = null;
            return true;
        }

        // =====================================================================
        //  BEHAVIOURAL CASES
        // =====================================================================

        private static void CheckSidesAreOpposite(List<string> failures, StringBuilder log)
        {
            try
            {
                var t = typeof(EquipmentController);
                var main = t.GetField("SheatheSideMain", BindingFlags.NonPublic | BindingFlags.Static);
                var off = t.GetField("SheatheSideOff", BindingFlags.NonPublic | BindingFlags.Static);
                if (main == null || off == null)
                {
                    failures.Add("S1: SheatheSideMain / SheatheSideOff are gone. The two slots no longer " +
                                 "have declared opposite sides, so nothing stops them sharing a hip again.");
                    return;
                }
                float m = Convert.ToSingle(main.GetRawConstantValue(), CultureInfo.InvariantCulture);
                float o = Convert.ToSingle(off.GetRawConstantValue(), CultureInfo.InvariantCulture);
                if (Mathf.Approximately(m, 0f) || Mathf.Approximately(o, 0f) || m * o >= 0f)
                    failures.Add("S1: the sheathe sides are not opposite (main=" + m + " off=" + o + "). " +
                                 "Same-sign sides put the sword and the shield on the same hip.");
                else
                    log.AppendLine("  S1 hip sides opposite (main=" + m + " off=" + o + ") .......... ok");
            }
            catch (Exception e)
            {
                failures.Add("S1: could not read the sheathe side constants — " + e.Message);
            }
        }

        private static void CheckShippedRotation(List<string> failures, StringBuilder log)
        {
            GameObject probe = null;
            try
            {
                var t = typeof(EquipmentController);
                var method = t.GetMethod("ComputeSheathRotation",
                    BindingFlags.NonPublic | BindingFlags.Instance, null,
                    new[] { typeof(Transform), typeof(float) }, null);
                if (method == null)
                {
                    failures.Add("G: ComputeSheathRotation(Transform, float) is gone — the per-slot sheathe " +
                                 "derivation cannot be driven, so the vertical/inverted rule is unproven.");
                    return;
                }
                var diagonalField = t.GetField("_sheatheBladeDiagonalDeg", BindingFlags.NonPublic | BindingFlags.Instance);
                var signField = t.GetField("_sheatheLongAxisSign", BindingFlags.NonPublic | BindingFlags.Instance);
                if (diagonalField == null || signField == null)
                {
                    failures.Add("G: _sheatheBladeDiagonalDeg / _sheatheLongAxisSign are gone — the tilt and " +
                                 "the one-number 'inverted' flip are the two knobs the owner was promised.");
                    return;
                }
                var sideMain = t.GetField("SheatheSideMain", BindingFlags.NonPublic | BindingFlags.Static);
                var sideOff = t.GetField("SheatheSideOff", BindingFlags.NonPublic | BindingFlags.Static);
                float mainSide = sideMain != null ? Convert.ToSingle(sideMain.GetRawConstantValue(), CultureInfo.InvariantCulture) : -1f;
                float offSide = sideOff != null ? Convert.ToSingle(sideOff.GetRawConstantValue(), CultureInfo.InvariantCulture) : 1f;

                probe = new GameObject("SheathePoseProbe");
                // The body faces an arbitrary direction and the socket is rotated nowhere near it.
                // If the derivation were anchor-dependent (a typed euler in the socket's frame is
                // the classic way to be), these two would disagree and the assertions below would
                // read as noise instead of a pose.
                probe.transform.rotation = Quaternion.Euler(0f, 37f, 0f);
                var ec = probe.AddComponent<EquipmentController>();
                var socket = new GameObject("SheatheSocket_HipMain").transform;
                socket.SetParent(probe.transform, false);
                socket.localRotation = Quaternion.Euler(-18f, 164f, 25f);

                Transform body = probe.transform;
                float shippedDiagonal = Convert.ToSingle(diagonalField.GetValue(ec), CultureInfo.InvariantCulture);
                float shippedSign = Convert.ToSingle(signField.GetValue(ec), CultureInfo.InvariantCulture);

                // ── G1: shipped defaults → long axis VERTICAL ────────────────────────
                Vector3 longMain = LongAxisWorld(method, ec, socket, mainSide);
                float tilt = Vector3.Angle(longMain, longMain.y >= 0f ? body.up : -body.up);
                if (!Mathf.Approximately(shippedDiagonal, 0f))
                    failures.Add("G1: _sheatheBladeDiagonalDeg ships at " + shippedDiagonal + ", not 0. The " +
                                 "owner ruled the long axis runs 'up and down'; a non-zero shipped default is " +
                                 "the retired baldric diagonal coming back.");
                else if (tilt > AngleTolDeg)
                    failures.Add("G1: the sheathed long axis sits " + tilt.ToString("0.#") + " deg off vertical " +
                                 "(tolerance " + AngleTolDeg + "). ~28 = the retired baldric diagonal; ~90 = " +
                                 "lying across the body, the state in logs/device/sheathed-weapon.png.");
                else
                    log.AppendLine("  G1 long axis vertical (" + tilt.ToString("0.#") + " deg off) ......... ok");

                // ── G2: shipped default is INVERTED (tip down) ───────────────────────
                float dotUp = Vector3.Dot(longMain.normalized, body.up);
                if (shippedSign >= 0f)
                    failures.Add("G2: _sheatheLongAxisSign ships at " + shippedSign + " (tip UP). The owner asked " +
                                 "for 'inverted', which we implement as tip-DOWN (-1). If the owner has since " +
                                 "flipped it deliberately, update this case in the same commit as the flip.");
                else if (dotUp > -0.99f)
                    failures.Add("G2: the long axis is not hanging DOWN (dot(bodyUp) = " + dotUp.ToString("0.###") +
                                 ", expected ~-1). The sign field says inverted but the pose does not.");
                else
                    log.AppendLine("  G2 inverted / tip-down (dot=" + dotUp.ToString("0.##") + ") ............ ok");

                // ── G3: the sign is the ONE number that flips it ─────────────────────
                signField.SetValue(ec, 1f);
                Vector3 flipped = LongAxisWorld(method, ec, socket, mainSide);
                signField.SetValue(ec, shippedSign);
                float flippedDot = Vector3.Dot(flipped.normalized, body.up);
                if (flippedDot < 0.99f)
                    failures.Add("G3: setting _sheatheLongAxisSign = +1 did NOT stand the prop up " +
                                 "(dot(bodyUp) = " + flippedDot.ToString("0.###") + ", expected ~+1). The " +
                                 "owner was promised a one-number flip; it does not work.");
                else
                    log.AppendLine("  G3 sign flips the carry end-for-end ..................... ok");

                // ── G4: TEETH. Re-introduce the diagonal; G1's rule must now FAIL ────
                diagonalField.SetValue(ec, 28f);
                Vector3 diagonalLong = LongAxisWorld(method, ec, socket, mainSide);
                diagonalField.SetValue(ec, shippedDiagonal);
                float diagonalTilt = Vector3.Angle(diagonalLong, diagonalLong.y >= 0f ? body.up : -body.up);
                if (diagonalTilt <= AngleTolDeg)
                    failures.Add("G4: restoring the 28 deg baldric diagonal left the long axis reading vertical " +
                                 "(" + diagonalTilt.ToString("0.#") + " deg). The vertical assertion is therefore " +
                                 "measuring nothing and would not have caught the reported defect.");
                else
                    log.AppendLine("  G4 the retired 28 deg diagonal is REJECTED (" +
                                   diagonalTilt.ToString("0.#") + " deg off) ... ok");

                // ── G5a: the two slots hang on OPPOSITE sides ────────────────────────
                Quaternion mainLocal = (Quaternion)method.Invoke(ec, new object[] { socket, mainSide });
                Quaternion offLocal = (Quaternion)method.Invoke(ec, new object[] { socket, offSide });
                Vector3 mainFlat = (socket.rotation * mainLocal) * Vector3.forward;
                Vector3 offFlat = (socket.rotation * offLocal) * Vector3.forward;
                float sideDot = Vector3.Dot(mainFlat.normalized, offFlat.normalized);
                if (sideDot > -0.9f)
                    failures.Add("G5a: the main-hand and off-hand sheathe poses face the SAME way " +
                                 "(dot = " + sideDot.ToString("0.###") + ", expected ~-1). The two hips are " +
                                 "supposed to mirror; identical poses mean the side argument is ignored.");
                else
                    log.AppendLine("  G5a main/off hips mirror (dot=" + sideDot.ToString("0.##") + ") ......... ok");
            }
            catch (Exception e)
            {
                failures.Add("G: driving the shipped ComputeSheathRotation threw — " + e.GetType().Name +
                             ": " + e.Message);
            }
            finally
            {
                if (probe != null) UnityEngine.Object.DestroyImmediate(probe);
            }
        }

        private static Vector3 LongAxisWorld(MethodInfo method, EquipmentController ec, Transform socket, float side)
        {
            var local = (Quaternion)method.Invoke(ec, new object[] { socket, side });
            // Prop-local +Y is the long axis (NormalizeInto + SeatHiltLowerHalf put it there);
            // the pose is returned in the SOCKET's local frame, so compose the socket to reach world.
            return (socket.rotation * local) * Vector3.up;
        }

        private static void CheckShieldMountRule(List<string> failures, StringBuilder log)
        {
            GameObject probe = null;
            try
            {
                probe = new GameObject("SheatheShieldProbe");
                probe.transform.rotation = Quaternion.Euler(0f, -113f, 0f);
                Transform body = probe.transform;
                var socket = new GameObject("SheatheSocket_HipOff").transform;
                socket.SetParent(body, false);
                socket.localRotation = Quaternion.Euler(41f, -12f, 96f);

                // A synthetic plate: thickness on X, long axis on Y — the frame the runtime
                // MEASURES off the mesh (WeaponOrientHelper.TryResolveShieldFrame). Synthesised
                // here because a headless suite has no shield mesh; the SOLVE under test is the
                // shipped one either way.
                var frame = new WeaponOrientHelper.ShieldFrame
                {
                    Valid = true,
                    HandleResolved = true,
                    HandleOnPositiveSide = false,
                    ThicknessAxis = Vector3.right,
                    LongAxis = Vector3.up,
                };

                // ⛔ NOT INVERTED. This read `-body.up`, "inverted, matching the sword", and that
                // generalisation is the defect the owner reported on 2026-08-20 ("redo the sword and
                // sheild. not working"). Her instruction — "sheathed should sit inverted with the
                // longest mesh (y) up and down" — is about a SWORD: the long axis is the blade and
                // inverted means tip-down. A shield has no tip and no meaningful end-for-end, so the
                // extra constraint buys nothing and only gives a mis-measured axis a second way to
                // decide the pose. The shield keeps the felt-approved WO-1123 rule (thickness away
                // from the player, handle inward), now applied at the HIP anchor.
                Vector3 outward = body.right;    // the hip side: away from the leg
                Vector3 longUp = body.up;        // plain up — the sword's inversion is sword-only

                Quaternion local = WeaponOrientHelper.ComputeShieldMountRotation(frame, socket, outward, longUp);
                Quaternion world = socket.rotation * local;
                float faceOff = Vector3.Angle(world * frame.ThicknessAxis, outward);
                float longOff = Vector3.Angle(world * frame.LongAxis, longUp);

                if (longOff > AngleTolDeg)
                    failures.Add("G6: the sheathed SHIELD's long axis sits " + longOff.ToString("0.#") +
                                 " deg off the vertical it was handed — the solve did not honour the " +
                                 "second axis it was given, so the roll is unresolved.");
                else if (faceOff > AngleTolDeg)
                    failures.Add("G6: the sheathed SHIELD's thickness axis sits " + faceOff.ToString("0.#") +
                                 " deg off the hip's outward direction, so its face is not turned away from " +
                                 "the player.");
                else
                    log.AppendLine("  G6 shield: face outward, long axis vertical .............. ok");

                // TEETH: the RETIRED inputs (thickness along -body.forward, the BACK mount) must
                // NOT satisfy the hip rule. If they do, this case is measuring the solver's
                // internals rather than the pose, and the shield could go back to facing the wrong
                // way with the suite still green.
                Quaternion backLocal = WeaponOrientHelper.ComputeShieldMountRotation(frame, socket, -body.forward, body.up);
                float backFaceOff = Vector3.Angle((socket.rotation * backLocal) * frame.ThicknessAxis, outward);
                if (backFaceOff <= AngleTolDeg)
                    failures.Add("G7: the retired BACK-mount inputs (-body.forward) produce the same outward " +
                                 "face as the hip inputs, so G6 cannot tell the two poses apart.");
                else
                    log.AppendLine("  G7 the retired back-mount inputs are REJECTED (" +
                                   backFaceOff.ToString("0.#") + " deg off) ... ok");
            }
            catch (Exception e)
            {
                failures.Add("G6/G7: driving ComputeShieldMountRotation threw — " + e.GetType().Name +
                             ": " + e.Message);
            }
            finally
            {
                if (probe != null) UnityEngine.Object.DestroyImmediate(probe);
            }
        }

        // =====================================================================
        //  G8/G9 — THE RENDERED SHAPE, not the euler
        // =====================================================================
        //
        // WHY THIS CASE EXISTS, in one sentence: G6 passed while the shield rendered FLAT.
        //
        // The owner's capture (2026-08-20, pid 32572) is the whole argument:
        //   sheathed shield DERIVED: faceOffOutward=0deg longTiltFromVertical=0deg longAxisDotUp=-1
        //   MEASURED after hold: worldEuler=(90.00, 105.00, 0.00) worldBounds=s(0.92, 0.20, 0.81)
        // Every angle read perfect and the prop was a dinner plate at the hip — because the angles
        // are measured against the FRAME's axes, and the frame had named the mesh's longest extent
        // as its "thickness". An assertion phrased in degrees cannot separate "posed correctly" from
        // "posed correctly with respect to a lie". An assertion phrased in RENDERED VOLUME can: a
        // 0.20 m vertical extent on a 0.78 m plate is a collapse no wrong frame can talk its way out
        // of. So this case drives the REAL measurement (TryResolveShieldFrame) and the REAL solve on
        // a real MeshRenderer, at a hostile attach rotation, and asserts the SHAPE that comes out.
        //
        // The hostile attach rotation is the point, not decoration: the retired measurement read the
        // renderer's WORLD AABB and re-expressed its extents vector in the parent basis, which is
        // only correct when the prop is axis-aligned with that parent. Measured at identity the old
        // code passes; measured at a rotated seat — which is where the game measures, because the
        // prop is on the hand — it mis-orders the axes. G9 pins that the collapse is detectable.
        //
        // ⚠ AND THE SHAPE IS MEASURED ALONG THE DIRECTIONS THAT MEAN SOMETHING, NOT ALONG WORLD X/Y/Z.
        // The first draft of G8b asked whether ANY world-AABB axis was thin, and it red-flagged a
        // CORRECTLY posed shield at 0.46 m — because the fixture's body is yawed 64 deg, so the
        // 0.20 m thinness splits across world X and Z and no world axis is thin. An AABB is three
        // fixed directions; the pose is about `outward` and `up`. Projecting onto those two (see
        // ExtentAlong) is exact under any body or socket rotation. The world-Y clause is KEPT
        // alongside them because vertical extent is the one AABB number a yaw cannot smear, and it
        // is the number the device capture actually prints — the oracle should speak the log's
        // language as well as the geometry's.
        private const float PlateThickness = 0.20f;   // the live shield's measured thinness, to scale
        private const float PlateWidth = 0.63f;
        private const float PlateHeight = 0.78f;

        private static void CheckSheathedShieldRendersAsAPlate(List<string> failures, StringBuilder log)
        {
            GameObject probe = null;
            try
            {
                probe = new GameObject("SheatheShieldShapeProbe");
                // Body yaw ONLY. A pitched body would tilt the world Y axis and smear the vertical
                // extent, which would make this case an assertion about the fixture instead of about
                // the pose. The socket below still carries a hostile full rotation, which is what
                // proves the solve is anchor-independent.
                probe.transform.rotation = Quaternion.Euler(0f, 64f, 0f);
                Transform body = probe.transform;

                var socket = new GameObject("SheatheSocket_HipOff").transform;
                socket.SetParent(body, false);
                socket.localRotation = Quaternion.Euler(41f, -12f, 96f);

                // The grip root sits at a ROTATED seat, exactly as it does on the hand when the
                // runtime takes its one measurement.
                var gripRoot = new GameObject("EquipmentProp_OffHand").transform;
                gripRoot.SetParent(socket, false);
                gripRoot.localRotation = Quaternion.Euler(292f, 140f, 105f);

                // A real renderer with a real mesh: thickness on Z, long axis on Y, width on X —
                // the live shield's proportions (back-solved from the owner's capture).
                var plate = GameObject.CreatePrimitive(PrimitiveType.Cube);
                plate.name = "ShieldPlate";
                plate.transform.SetParent(gripRoot, false);
                plate.transform.localRotation = Quaternion.identity;
                plate.transform.localScale = new Vector3(PlateWidth, PlateHeight, PlateThickness);

                // ── G8a: the MEASUREMENT names the real axes despite the rotated seat ────────
                if (!WeaponOrientHelper.TryResolveShieldFrame(plate, gripRoot, out var frame) || !frame.Valid)
                {
                    failures.Add("G8: TryResolveShieldFrame could not measure a plain 0.63 x 0.78 x 0.20 " +
                                 "plate at a rotated seat. Every shield pose downstream falls back to a " +
                                 "hand-typed euler when this returns false.");
                    return;
                }

                bool axesRight = frame.Axes.NarrowestAxis == 2 && frame.Axes.LongestAxis == 1;
                if (!axesRight)
                    failures.Add("G8a: the plate measures narrowest=" + AxisLetter(frame.Axes.NarrowestAxis) +
                                 " longest=" + AxisLetter(frame.Axes.LongestAxis) + " (" + frame.Axes.Describe() +
                                 ") but it was built 0.63 x 0.78 x 0.20 — narrowest MUST be Z and longest Y. " +
                                 "A frame that mis-names the axes poses the prop perfectly about a lie: that " +
                                 "is the 2026-08-20 flat shield, and it comes from measuring the WORLD AABB " +
                                 "of a prop that is sitting at a rotated seat.");
                else
                    log.AppendLine("  G8a plate measured correctly at a rotated seat ........... ok");

                // ── G8b: the RENDERED VOLUME is still a plate after the sheathed pose ───────
                Vector3 outward = body.right;
                gripRoot.localRotation = WeaponOrientHelper.ComputeShieldMountRotation(
                    frame, socket, outward, body.up);

                // ⛔ DO NOT ASK A WORLD AABB WHETHER THE PLATE IS THIN. A clause here once read
                // `minExtent > PlateThickness * 2f` — "no axis of the rendered shield is thin" — and
                // it failed on a CORRECTLY posed shield, reporting 0.46 m. That number is not a bug
                // in the pose and not a bad threshold to nudge; it is arithmetic:
                //
                //   the fixture's body is yawed 64 deg, so with the thin axis pointing along
                //   body.right the 0.20 m thickness projects onto BOTH world X and world Z
                //     world X extent = 0.899*0.63 + 0.438*0.20 = 0.654
                //     world Y extent =               1.000*0.78 = 0.780
                //     world Z extent = 0.438*0.63 + 0.899*0.20 = 0.456   <- the reported 0.46
                //
                // A world-axis-aligned box around a rotated plate never exposes the plate's thinness
                // unless the plate happens to be square-on to the world axes. The owner's capture DID
                // read 0.20 — but in world Y, and only because there the thin axis was standing
                // exactly vertical (worldEuler pitch 90), and the VERTICAL extent is the one quantity
                // a yaw cannot smear. So: the vertical clause below is kept, because it is both
                // yaw-invariant and literally the number the device log prints; the "some axis is
                // thin" clause is replaced by extents measured along the axes that MEAN something —
                // outward and up. Those are exact under any body/socket rotation, which makes them
                // strictly stronger than the AABB clause they replace, not a relaxation of it.
                float alongOutward = ExtentAlong(plate, outward);   // must be the THICKNESS
                float alongUp = ExtentAlong(plate, body.up);        // must be the HEIGHT
                Vector3 size = WorldExtents(plate);

                if (alongOutward > PlateThickness * 1.5f)
                    failures.Add("G8b: the shield measures " + alongOutward.ToString("0.##") + " m across " +
                                 "the OUTWARD direction, but its thickness is only " +
                                 PlateThickness.ToString("0.##") + " m. The face is not turned away from " +
                                 "the player — an edge is. This is the dinner-plate defect stated in the " +
                                 "one direction that cannot be smeared by which way the hero happens to face.");
                else if (alongOutward < PlateThickness * 0.5f)
                    failures.Add("G8b: the shield measures only " + alongOutward.ToString("0.###") + " m " +
                                 "across the outward direction against a " + PlateThickness.ToString("0.##") +
                                 " m thickness - the prop has been squashed, not seated.");
                else if (alongUp < PlateHeight * 0.9f)
                    failures.Add("G8b: the shield stands only " + alongUp.ToString("0.##") + " m tall along " +
                                 "the body's up axis from a " + PlateHeight.ToString("0.##") + " m plate. Its " +
                                 "long axis is not upright, so it is hanging on a diagonal or on its side.");
                else if (size.y < PlateHeight * 0.7f)
                    failures.Add("G8b: the sheathed shield renders only " + size.y.ToString("0.##") +
                                 " m tall in WORLD Y from a " + PlateHeight.ToString("0.##") + " m plate. It " +
                                 "is lying FLAT — the exact defect the owner reported, and the exact shape " +
                                 "the capture measured as s(0.92, 0.20, 0.81) while every angle read 0 deg.");
                else
                    log.AppendLine("  G8b sheathed shield renders as a plate (thickness-out=" +
                                   alongOutward.ToString("0.##") + "m upright=" + alongUp.ToString("0.##") +
                                   "m worldY=" + size.y.ToString("0.##") + "m) ... ok");

                // ── G9: TEETH. Feed the solve the SWAPPED frame the old measurement produced and
                // prove the shape check REJECTS it. Without this, G8b could be passing because the
                // fixture cannot fail rather than because the pose is right.
                var swapped = frame;
                swapped.ThicknessAxis = frame.LongAxis;
                swapped.LongAxis = frame.ThicknessAxis;
                gripRoot.localRotation = WeaponOrientHelper.ComputeShieldMountRotation(
                    swapped, socket, outward, body.up);
                // Both of G8b's live clauses are re-run against it, not just one: after the 0.46
                // correction the PRIMARY clause is the outward extent, so proving only that the
                // vertical clause bites would leave the clause that actually guards the pose
                // unexercised. Expected on a swapped frame: outward reads the 0.78 long axis
                // (should trip the >1.5x thickness clause) and world Y collapses to the 0.20
                // thin axis (should trip the vertical clause).
                float badOutward = ExtentAlong(plate, outward);
                float badHeight = WorldExtents(plate).y;
                bool outwardClauseBites = badOutward > PlateThickness * 1.5f;
                bool verticalClauseBites = badHeight < PlateHeight * 0.7f;

                if (!outwardClauseBites)
                    failures.Add("G9: posing the plate off a SWAPPED frame (thickness<->long, which is " +
                                 "precisely what the retired world-AABB measurement produced) leaves only " +
                                 badOutward.ToString("0.##") + " m across the outward direction, so G8b's " +
                                 "PRIMARY clause does not fire. That clause is the one guarding the pose; " +
                                 "if it cannot detect a swapped frame it is not guarding anything.");
                else if (!verticalClauseBites)
                    failures.Add("G9: a SWAPPED frame still renders " + badHeight.ToString("0.##") + " m tall " +
                                 "in world Y, so G8b's vertical clause — the one phrased in the same number " +
                                 "the device capture prints — cannot detect the collapse it exists to detect.");
                else
                    log.AppendLine("  G9 a swapped frame is REJECTED by BOTH clauses (outward=" +
                                   badOutward.ToString("0.##") + "m worldY=" + badHeight.ToString("0.##") +
                                   "m) ... ok");
            }
            catch (Exception e)
            {
                failures.Add("G8/G9: the rendered-shape probe threw — " + e.GetType().Name + ": " + e.Message);
            }
            finally
            {
                if (probe != null) UnityEngine.Object.DestroyImmediate(probe);
            }
        }

        /// <summary>
        /// The world-axis-aligned size of a renderer's mesh — computed from the mesh's own corners
        /// rather than read off Renderer.bounds. Identical by definition, and deterministic: outside
        /// Play mode a renderer's cached bounds can lag a transform written in the same frame, and a
        /// stale read here would make this case flicker instead of assert.
        /// </summary>
        private static Vector3 WorldExtents(GameObject go)
        {
            var filter = go.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null) return Vector3.zero;
            Bounds mb = filter.sharedMesh.bounds;
            Vector3 c = mb.center, e = mb.extents;
            Vector3 min = Vector3.zero, max = Vector3.zero;
            for (int corner = 0; corner < 8; corner++)
            {
                var p = new Vector3(
                    c.x + ((corner & 1) == 0 ? -e.x : e.x),
                    c.y + ((corner & 2) == 0 ? -e.y : e.y),
                    c.z + ((corner & 4) == 0 ? -e.z : e.z));
                Vector3 w = go.transform.TransformPoint(p);
                if (corner == 0) { min = max = w; }
                else { min = Vector3.Min(min, w); max = Vector3.Max(max, w); }
            }
            return max - min;
        }

        /// <summary>
        /// The prop's extent along an ARBITRARY world direction — the span of its mesh corners
        /// projected onto that axis. This is the measurement a world AABB cannot give you: an AABB
        /// is only ever three fixed world directions, so a plate that is thin along `outward` but
        /// yawed away from the world axes reports a fat minimum extent (0.46 m on this fixture) and
        /// looks nothing like a plate. Projecting onto the direction the pose was ASKED to satisfy
        /// is exact under any body or socket rotation, which is what makes G8b both strict and stable.
        /// </summary>
        private static float ExtentAlong(GameObject go, Vector3 axis)
        {
            var filter = go != null ? go.GetComponent<MeshFilter>() : null;
            if (filter == null || filter.sharedMesh == null) return 0f;
            if (axis.sqrMagnitude < 1e-9f) return 0f;
            axis = axis.normalized;

            Bounds mb = filter.sharedMesh.bounds;
            Vector3 c = mb.center, e = mb.extents;
            float min = float.MaxValue, max = float.MinValue;
            for (int corner = 0; corner < 8; corner++)
            {
                var p = new Vector3(
                    c.x + ((corner & 1) == 0 ? -e.x : e.x),
                    c.y + ((corner & 2) == 0 ? -e.y : e.y),
                    c.z + ((corner & 4) == 0 ? -e.z : e.z));
                float d = Vector3.Dot(go.transform.TransformPoint(p), axis);
                if (d < min) min = d;
                if (d > max) max = d;
            }
            return max - min;
        }

        private static string AxisLetter(int axis) => axis == 0 ? "X" : axis == 1 ? "Y" : "Z";

        // =====================================================================
        //  SOURCE HELPERS
        // =====================================================================

        /// <summary>
        /// Returns the brace-balanced body that follows <paramref name="signatureFragment"/>,
        /// or null. Operates on comment-blanked (and, for brace safety, ideally string-blanked)
        /// source, so an interpolated string's braces cannot unbalance the scan.
        /// </summary>
        private static string ExtractMethodBody(string source, string signatureFragment)
        {
            if (string.IsNullOrEmpty(source)) return null;
            int at = source.IndexOf(signatureFragment, StringComparison.Ordinal);
            while (at >= 0)
            {
                int open = source.IndexOf('{', at);
                if (open < 0) return null;
                // An expression-bodied forwarder (`=> Other(...);`) is not the body we want;
                // skip past it and keep looking for the real one.
                int arrow = source.IndexOf("=>", at, StringComparison.Ordinal);
                int semi = source.IndexOf(';', at);
                if (arrow >= 0 && arrow < open && (semi < 0 || arrow < semi))
                {
                    at = source.IndexOf(signatureFragment, semi > at ? semi : at + signatureFragment.Length,
                                        StringComparison.Ordinal);
                    continue;
                }
                int depth = 0;
                for (int i = open; i < source.Length; i++)
                {
                    if (source[i] == '{') depth++;
                    else if (source[i] == '}')
                    {
                        depth--;
                        if (depth == 0) return source.Substring(open, i - open + 1);
                    }
                }
                return null;
            }
            return null;
        }

        private static string ReadOrEmpty(string path)
        {
            try { return File.ReadAllText(path); } catch { return string.Empty; }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  CASE D2 — A FLAG DEFAULT MUST NOT UN-DO THE DERIVED POSE EITHER
        // ─────────────────────────────────────────────────────────────────────
        // D1's sibling, and found the same night by the same capture. With the two stale absolute
        // rows deleted, the sheathed sword STILL read 81 deg off vertical and TIP UP. The trace:
        //
        //   [Flow:Equip]  sheathed long axis ... tiltFromVertical=0deg longAxisDotUp=-1
        //   [Flow:Offset] sheathed FALLBACK (drawn 'sword_A' on back pose):
        //                 pos=(0.01,0.03,-0.01) rot=(117.00,-2.00,110.00)
        //
        // With NO explicit @sheathed row, ApplySheathedOffset falls back to the DRAWN row — and
        // ff.sheathdrawnrot decides whether that row's ROTATION composes too. That euler was
        // authored in the HAND BONE's frame; composing it onto a hip-socket pose is a frame
        // mismatch, which the flag's own documentation said in as many words while its DEFAULT
        // said the opposite. It defaulted ON from a 2026-07-07 owner A/B — an experiment's setting
        // left switched on, defending a BACK carry that the 2026-08-20 ruling then retired.
        //
        // THE INVARIANT: with no explicit sheathed authoring, the derived pose is the pose. A
        // position-only nudge is fine; a rotation compose across frames is not. The flag stays
        // (never strip a seam) — this pins its DEFAULT.
        private static void CheckSheathedDefaultsDoNotOverrideDerivation(List<string> failures, StringBuilder log)
        {
            // Read the DEFAULT, not the machine's current PlayerPrefs value: a dev who flipped the
            // flag locally must not turn this rule off for everyone. The default is the shipped
            // literal in FeatureFlags.cs, so that is what is asserted.
            string flagsPath;
            try { flagsPath = Path.Combine(Application.dataPath, "_Modules/Core/FeatureFlags.cs".Replace('/', Path.DirectorySeparatorChar)); }
            catch { failures.Add("D2: could not resolve FeatureFlags.cs — the sheathed-rotation default is UNVERIFIED."); return; }
            string src = BlankComments(ReadOrEmpty(flagsPath));
            if (string.IsNullOrEmpty(src))
            {
                failures.Add("D2: FeatureFlags.cs not readable — the sheathed-rotation default is UNVERIFIED.");
                return;
            }
            const string Needle = "Get(\"sheathdrawnrot\"";
            int at = src.IndexOf(Needle, StringComparison.Ordinal);
            if (at < 0)
            {
                failures.Add("D2: no Get(\"sheathdrawnrot\", ...) in FeatureFlags.cs. The sheathed-rotation " +
                             "seam was renamed or removed; this rule can no longer see the default it guards.");
                return;
            }
            int close = src.IndexOf(')', at);
            string call = close > at ? src.Substring(at, close - at) : src.Substring(at);
            if (call.Replace(" ", string.Empty).IndexOf("defaultOn:true", StringComparison.OrdinalIgnoreCase) >= 0)
                failures.Add("D2: ff.sheathdrawnrot defaults ON again. That composes the DRAWN offset row's " +
                             "hand-frame euler onto the derived HIP pose for every player who has never " +
                             "touched the flag — measured on the live Knight as a sheathed sword 81 deg off " +
                             "vertical and TIP UP, i.e. exactly the carry the owner rejected on 2026-08-20. " +
                             "Position-only is the frame-safe fallback; keep the flag, keep the default OFF.");
            else
                log.AppendLine("  D2 ff.sheathdrawnrot defaults OFF (frame-safe) .......... ok");
        }

        // ─────────────────────────────────────────────────────────────────────
        //  CASE P1 — THE SHEATHED SHIELD IS CENTRED ON THE HIP
        // ─────────────────────────────────────────────────────────────────────
        // The third defect from the same capture, and the one every ANGLE-based case in this file
        // is blind to. G6/G8 both read perfect — "faceOffOutward=0deg longTiltFromVertical=0deg" —
        // while the shield floated at CHEST height with backdrop visible between it and the torso.
        //
        // Cause: the live shield is a NATIVE prop, so its origin is its GRIP, and its grip is at the
        // plate's BOTTOM EDGE (fantasy_shield localBounds c=(0, 0.315, 0.035) s=(0.512, 0.63,
        // 0.161)). Seating the ORIGIN at the hip hangs the whole 0.63 m plate upward from it.
        // EquipmentController.ApplyOffHandCentreOnSocket now shifts by the prop's own
        // origin-to-rendered-centre vector so the plate's MIDDLE lands on the hip.
        //
        // This case drives that SHIPPED method (reflection) on a real renderer whose pivot is at its
        // bottom edge — the shield's actual pathology — and asserts the rendered centre lands where
        // the seat put the origin. It also asserts the method is IDEMPOTENT, because ApplyHoldPose
        // re-asserts the pose every frame and a shift that accumulated would walk the shield away
        // over a few seconds of play, which no single screenshot would ever catch.
        private static void CheckSheathedOffHandIsCentredOnSocket(List<string> failures, StringBuilder log)
        {
            GameObject probe = null;
            try
            {
                probe = new GameObject("SheatheOffHandCentreProbe");
                probe.transform.rotation = Quaternion.Euler(0f, 37f, 0f);

                var socket = new GameObject("SheatheSocket_HipOff").transform;
                socket.SetParent(probe.transform, false);
                socket.localRotation = Quaternion.Euler(23f, -47f, 61f);   // a hostile, rig-arbitrary bone frame
                socket.localScale = Vector3.one * 1.67f;                   // the CC rig's real bone lossyScale

                var gripRoot = new GameObject("EquipmentProp_OffHand").transform;
                gripRoot.SetParent(socket, false);
                Vector3 baseLocal = new Vector3(0.12f, 0.03f, -0.02f);   // what the seat computed
                gripRoot.localPosition = baseLocal;
                gripRoot.localRotation = Quaternion.Euler(11f, 200f, 349f);
                Vector3 seatedOrigin = gripRoot.position;   // where the seat PUT the prop

                // A plate whose PIVOT IS ITS BOTTOM EDGE — a cube child pushed up by half its height
                // reproduces exactly the fantasy_shield pathology without needing the asset.
                var plate = GameObject.CreatePrimitive(PrimitiveType.Cube);
                plate.name = "ShieldPlate";
                plate.transform.SetParent(gripRoot, false);
                plate.transform.localScale = new Vector3(PlateWidth, PlateHeight, PlateThickness);
                plate.transform.localPosition = new Vector3(0f, PlateHeight * 0.5f, 0f);

                var rend = plate.GetComponent<Renderer>();
                float offBefore = Vector3.Distance(rend.bounds.center, seatedOrigin);
                if (offBefore < PlateHeight * 0.3f)
                {
                    failures.Add("P1: the fixture's plate is already centred on its pivot (" +
                                 offBefore.ToString("0.###") + " m) — it cannot demonstrate the bug it " +
                                 "was built for, so a passing result here would mean nothing.");
                    return;
                }

                var mi = typeof(EquipmentController).GetMethod("ApplyOffHandCentreOnSocket",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                if (mi == null)
                {
                    failures.Add("P1: EquipmentController.ApplyOffHandCentreOnSocket is GONE. Without it a " +
                                 "grip-at-origin shield hangs its whole plate upward from the hip and reads as " +
                                 "floating at the chest, with every angle in this suite still green.");
                    return;
                }
                var holder = new GameObject("~equipHolder");
                holder.transform.SetParent(probe.transform, false);
                var ctrl = holder.AddComponent<EquipmentController>();   // no Awake outside play mode

                mi.Invoke(ctrl, new object[] { gripRoot, socket, baseLocal });
                float offAfter = Vector3.Distance(rend.bounds.center, seatedOrigin);
                if (offAfter > CentreToleranceM)
                    failures.Add("P1: after ApplyOffHandCentreOnSocket the plate's rendered centre is still " +
                                 offAfter.ToString("0.###") + " m from the seated point (was " +
                                 offBefore.ToString("0.###") + " m, tolerance " + CentreToleranceM.ToString("0.##") +
                                 " m). A grip-at-origin shield is still hanging by its bottom edge — the " +
                                 "2026-08-20 chest-float.");
                else
                    log.AppendLine("  P1 sheathed shield centres on the hip (" + offBefore.ToString("0.##") +
                                   " -> " + offAfter.ToString("0.##") + " m) ...... ok");

                // IDEMPOTENT: re-asserting the pose must not walk the prop away.
                mi.Invoke(ctrl, new object[] { gripRoot, socket, baseLocal });
                mi.Invoke(ctrl, new object[] { gripRoot, socket, baseLocal });
                float offRepeat = Vector3.Distance(rend.bounds.center, seatedOrigin);
                if (Mathf.Abs(offRepeat - offAfter) > CentreToleranceM)
                    failures.Add("P1b: repeating the centring moved the plate again (" +
                                 offAfter.ToString("0.###") + " -> " + offRepeat.ToString("0.###") + " m). " +
                                 "ApplyHoldPose re-asserts the sheathed pose EVERY FRAME, so a shift that " +
                                 "accumulates walks the shield off the hero during play — and no single " +
                                 "screenshot would ever show it.");
                else
                    log.AppendLine("  P1b centring is idempotent across re-asserts .......... ok");
            }
            catch (Exception e)
            {
                failures.Add("P1: driving ApplyOffHandCentreOnSocket threw — " + e.GetType().Name + ": " + e.Message);
            }
            finally
            {
                if (probe != null) UnityEngine.Object.DestroyImmediate(probe);
            }
        }

        /// <summary>Slack on the centring assertion. The live plate is 0.63 m tall, so the defect it
        /// pins is a ~0.32 m error; 0.02 m is float noise through a scaled, rotated bone chain.</summary>
        private const float CentreToleranceM = 0.02f;

        // ─────────────────────────────────────────────────────────────────────
        //  CASE D1 — THE SHIPPED DATA MUST NOT OUTRANK THE DERIVED HIP POSE
        // ─────────────────────────────────────────────────────────────────────
        // WHY THIS CASE EXISTS, and why it is a DATA lint in a maths suite.
        //
        // Every G-case above passed while the sheathed sword hung diagonally, off the body, on
        // the WRONG hip — because the maths was never the thing that reached the screen. The
        // KnightGearProof play-mode capture (2026-08-20, Builds/KnightGearProof/) caught it in
        // two consecutive trace lines on the live Knight:
        //
        //   [Flow:Equip]  sheathed long axis on 'Hero (Grom)': tiltFromVertical=0deg
        //                 longAxisDotUp=-1 ... socket='SheatheSocket_HipMain'.
        //   [Flow:Offset] sheathed offset 'sword_A@sheathed' applied:
        //                 pos=(0.23,-0.14,0.12) rot=(180.00,-28.00,-51.00) full=True
        //
        // The first line is this suite's rule, computed correctly. The second is
        // ApplySheathedOffset REPLACING it — an `Explicit + fullOverride` row is ABSOLUTE, so it
        // overwrites localPosition AND localRotation outright. That row shipped in
        // Assets/Resources/OffsetForge/offsets.json (and its Assets/OffsetForge/ twin), authored
        // against the RETIRED spine/back socket; its -28 is literally the baldric diagonal G4
        // rejects. Its POSITIVE x also put the sword on the shield's hip, which is how both props
        // ended up on one side while S1 proved the sides were opposite by construction.
        //
        // THE INVARIANT: an absolute sheathed pose is only meaningful in the frame it was
        // authored in, and that frame moved from spine to hip. A SHIPPED one therefore cannot be
        // trusted and silently disables the derivation for every player at once. The owner's own
        // felt-tunes are not affected — the Seating Editor writes to persistentDataPath
        // (AttachmentOffsetRegistry.DevPath), not to these files.
        private static void CheckNoShippedAbsoluteSheathedRow(List<string> failures, StringBuilder log)
        {
            string[] rel =
            {
                "Resources/OffsetForge/offsets.json",   // the one the RUNTIME reads
                "OffsetForge/offsets.json",             // the Forge's authoring twin, kept in sync
            };
            int checkedFiles = 0;
            int before = failures.Count;
            foreach (string r in rel)
            {
                string path;
                try { path = Path.Combine(Application.dataPath, r.Replace('/', Path.DirectorySeparatorChar)); }
                catch { continue; }
                string json = ReadOrEmpty(path);
                if (string.IsNullOrEmpty(json)) continue;
                checkedFiles++;
                foreach (string id in AbsoluteSheathedIds(json))
                    failures.Add("D1: Assets/" + r + " ships an ABSOLUTE sheathed override '" + id +
                                 "' (fullOverride=true). ApplySheathedOffset applies that as an absolute " +
                                 "pos+rot in the sheathe SOCKET's frame, which REPLACES the derived hip " +
                                 "carry every G-case here asserts — so this suite would stay green while " +
                                 "the sword hangs diagonally on the wrong hip, exactly as it did on " +
                                 "2026-08-20. Delete the row (the derivation is the pose) or, if it is a " +
                                 "deliberate felt-tune, author it in the Seating Editor so it lands in " +
                                 "persistentDataPath instead of shipping to every player.");
            }
            if (checkedFiles == 0)
                failures.Add("D1: neither shipped offsets.json could be read — the absolute-override rule is UNVERIFIED.");
            else if (failures.Count == before)
                log.AppendLine("  D1 no shipped absolute @sheathed override (" + checkedFiles + " files) .. ok");
        }

        /// <summary>
        /// Every "<c>&lt;key&gt;@sheathed</c>" row in an offsets.json whose object also carries
        /// <c>"fullOverride": true</c>. Deliberately a scan over the raw text rather than a JSON
        /// parse: this suite must not acquire a dependency on the table's schema to answer a
        /// question about two literals, and a schema change must not silently switch the rule off.
        /// </summary>
        private static List<string> AbsoluteSheathedIds(string json)
        {
            var hits = new List<string>();
            const string Marker = "@sheathed\"";
            int i = 0;
            while (true)
            {
                int at = json.IndexOf(Marker, i, StringComparison.Ordinal);
                if (at < 0) break;
                i = at + Marker.Length;
                // The id, read back to its opening quote.
                int q = json.LastIndexOf('"', at);
                string id = q >= 0 ? json.Substring(q + 1, at + "@sheathed".Length - q - 1) : "<unknown>";
                // The object this id belongs to ends at the next '}' that closes it. Scan forward
                // from the id to that brace and look for fullOverride:true inside.
                int depth = 0, j = q >= 0 ? json.LastIndexOf('{', at) : at;
                if (j < 0) j = at;
                int end = j;
                for (; end < json.Length; end++)
                {
                    if (json[end] == '{') depth++;
                    else if (json[end] == '}') { depth--; if (depth == 0) break; }
                }
                string body = json.Substring(j, Math.Min(json.Length, end + 1) - j);
                if (body.Replace(" ", string.Empty).IndexOf("\"fullOverride\":true", StringComparison.OrdinalIgnoreCase) >= 0)
                    hits.Add(id);
            }
            return hits;
        }

        /// <summary>
        /// Blank out comments so a lint tests CODE, not prose. This file's whole subject is a
        /// change whose tombstone comments quote the retired bone and the retired socket name —
        /// an oracle that cannot tell code from a comment would fail the author for explaining
        /// the fix. (RaidScoringRegression learned this on 2026-08-07.)
        /// </summary>
        private static string BlankComments(string src)
        {
            if (string.IsNullOrEmpty(src)) return string.Empty;
            var sb = new StringBuilder(src.Length);
            bool inString = false, inChar = false, verbatim = false;
            for (int i = 0; i < src.Length; i++)
            {
                char c = src[i];
                if (!inString && !inChar)
                {
                    if (i + 1 < src.Length && c == '/' && src[i + 1] == '*')
                    {
                        int end = src.IndexOf("*/", i + 2, StringComparison.Ordinal);
                        sb.Append(' ');
                        if (end < 0) break;
                        i = end + 1;
                        continue;
                    }
                    if (i + 1 < src.Length && c == '/' && src[i + 1] == '/')
                    {
                        int nl = src.IndexOf('\n', i);
                        sb.Append(' ');
                        if (nl < 0) break;
                        sb.Append('\n');
                        i = nl;
                        continue;
                    }
                    if (c == '"')
                    {
                        inString = true;
                        verbatim = i > 0 && src[i - 1] == '@';
                        sb.Append(c);
                        continue;
                    }
                    if (c == '\'') { inChar = true; sb.Append(c); continue; }
                    sb.Append(c);
                    continue;
                }
                sb.Append(c);
                if (inString)
                {
                    if (verbatim)
                    {
                        if (c == '"' && !(i + 1 < src.Length && src[i + 1] == '"')) inString = false;
                        else if (c == '"' && i + 1 < src.Length && src[i + 1] == '"') { sb.Append('"'); i++; }
                    }
                    else if (c == '\\' && i + 1 < src.Length) { sb.Append(src[i + 1]); i++; }
                    else if (c == '"') inString = false;
                }
                else if (inChar)
                {
                    if (c == '\\' && i + 1 < src.Length) { sb.Append(src[i + 1]); i++; }
                    else if (c == '\'') inChar = false;
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// Blanks the CONTENTS of string literals (the quotes survive), so a brace-balanced
        /// scan cannot be thrown by an interpolated string and so a rule cannot match a name
        /// that appears only inside a trace message. Run AFTER BlankComments.
        /// </summary>
        private static string BlankStringLiterals(string src)
        {
            if (string.IsNullOrEmpty(src)) return string.Empty;
            var sb = new StringBuilder(src.Length);
            for (int i = 0; i < src.Length; i++)
            {
                char c = src[i];
                if (c == '\'')
                {
                    sb.Append(c);
                    i++;
                    while (i < src.Length && src[i] != '\'')
                    {
                        sb.Append(' ');
                        if (src[i] == '\\') { i++; if (i < src.Length) sb.Append(' '); }
                        i++;
                    }
                    if (i < src.Length) sb.Append('\'');
                    continue;
                }
                if (c != '"') { sb.Append(c); continue; }

                bool verbatim = i > 0 && src[i - 1] == '@';
                sb.Append('"');
                i++;
                while (i < src.Length)
                {
                    char d = src[i];
                    if (verbatim)
                    {
                        if (d == '"' && i + 1 < src.Length && src[i + 1] == '"') { sb.Append("  "); i += 2; continue; }
                        if (d == '"') break;
                    }
                    else
                    {
                        if (d == '\\' && i + 1 < src.Length) { sb.Append("  "); i += 2; continue; }
                        if (d == '"') break;
                    }
                    // Newlines are preserved so line-oriented reasoning about the blanked source
                    // still lines up with the file.
                    sb.Append(d == '\n' ? '\n' : ' ');
                    i++;
                }
                if (i < src.Length) sb.Append('"');
            }
            return sb.ToString();
        }
    }
}
