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

            if (failures.Count == 0)
            {
                reason = "sheathe pose: hip anchor, one socket per slot, long axis vertical + inverted (tip down) " +
                         "— and every rule proven to REJECT the pre-2026-08-20 state.";
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

                Vector3 outward = body.right;    // the hip side: away from the leg
                Vector3 longUp = -body.up;       // inverted, matching the sword

                Quaternion local = WeaponOrientHelper.ComputeShieldMountRotation(frame, socket, outward, longUp);
                Quaternion world = socket.rotation * local;
                float faceOff = Vector3.Angle(world * frame.ThicknessAxis, outward);
                float longOff = Vector3.Angle(world * frame.LongAxis, longUp);

                if (longOff > AngleTolDeg)
                    failures.Add("G6: the sheathed SHIELD's long axis sits " + longOff.ToString("0.#") +
                                 " deg off the vertical it was handed. The owner's 'longest mesh (y) up and " +
                                 "down' applies to the shield as much as the sword.");
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
