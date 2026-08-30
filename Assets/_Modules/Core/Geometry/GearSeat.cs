// =============================================================================
// GearSeat — ONE mount resolver every weapon / off-hand calls while seating.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.Geometry
//
// WHY: EquipmentController had a different parent + axis story per family
// (LeftHand vs RightHand vs SheatheSocket_ArmOff vs Socket_Shield). Owner
// diagrams 2026-08-29 made the sockets and mesh-read rules explicit for
// shield / staff / bow. This class is the common door: classify, pick the
// bone or dedicated empty, bake the family's axes. Mesh math stays in
// WeaponOrientHelper / WeaponBoundsOrient — this file does not re-solve
// a bow or a heater, it names WHERE they hang and WHICH WAY is "out".
//
// Families (owner diagrams):
//   SHIELD — Socket_Shield under LeftLowerArm. Outer (convex, painted) toward
//            the enemy; inner (straps) against the arm; top toward shoulder;
//            point toward hip. SAME pose drawn and sheathed.
//   STAFF  — RightHand. Head/finial up, butt down, front of head faces the
//            world (not into the chest). Grip = lower-middle third of shaft.
//   BOW    — LeftHand. Belly (string side) faces the archer; back faces the
//            target. SAME pose drawn and sheathed.
//   SWORD  — RightHand (default melee). Tip away from the fist.
// =============================================================================

using System;
using UnityEngine;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Core.Geometry
{
    /// <summary>Where a prop hangs, and the two world axes the family's mesh-read uses.</summary>
    public struct GearSeatPlan
    {
        public WeaponArchetype Archetype;
        public Transform Mount;
        /// <summary>True when town-carry and combat-hold must not re-parent or re-pose.</summary>
        public bool SamePoseDrawnAndSheathed;
        /// <summary>Shield outer / staff head-front / bow back (toward the world).</summary>
        public Vector3 OutwardWorld;
        /// <summary>Shield top / staff finial / bow up.</summary>
        public Vector3 UpWorld;
        public string Why;
        public bool Ok => Mount != null;
    }

    /// <summary>
    /// Common seater. Call from any attach path (main-hand, off-hand, companion).
    /// </summary>
    public static class GearSeat
    {
        public const string ShieldSocketName = "Socket_Shield";

        /// <summary>Map a live kind name + catalog category + id/mesh onto an archetype.</summary>
        public static WeaponArchetype Classify(string kindName, string category, string idOrMesh)
        {
            if (!string.IsNullOrEmpty(kindName))
            {
                switch (kindName.Trim().ToLowerInvariant())
                {
                    case "shield": return WeaponArchetype.Shield;
                    case "bow":    return WeaponArchetype.Bow;
                    case "staff":
                    case "wand":   return WeaponArchetype.Staff;
                    case "sword":
                    case "dagger":
                    case "axe":
                    case "hammer": return WeaponArchetype.Sword;
                }
            }
            return WeaponOrientHelper.Classify(category, idOrMesh);
        }

        /// <summary>
        /// Resolve the Humanoid mount for this family. Shield creates
        /// <see cref="ShieldSocketName"/> under LeftLowerArm if needed.
        /// </summary>
        public static GearSeatPlan ResolveMount(Animator animator, Transform body, WeaponArchetype archetype)
        {
            var plan = new GearSeatPlan { Archetype = archetype };
            if (animator == null || !animator.isHuman)
            {
                plan.Why = "animator not Humanoid — cannot resolve a bone mount";
                FlowTrace.Warn("Equip", "GearSeat.ResolveMount: " + plan.Why);
                return plan;
            }
            if (body == null) body = animator.transform;

            switch (archetype)
            {
                case WeaponArchetype.Shield:
                    plan.Mount = EnsureShieldSocket(animator, body);
                    plan.SamePoseDrawnAndSheathed = true;
                    GetShieldAxes(animator, body, out plan.OutwardWorld, out plan.UpWorld);
                    plan.Why = "SHIELD: Socket_Shield mid-LeftLowerArm; long axis ∥ arm (wide top at elbow, point at hand); paint outboard; inner toward body; arm through handle loop";
                    break;

                case WeaponArchetype.Bow:
                    plan.Mount = animator.GetBoneTransform(HumanBodyBones.LeftHand);
                    plan.SamePoseDrawnAndSheathed = true;
                    plan.OutwardWorld = body.forward;
                    plan.UpWorld = body.up;
                    plan.Why = "BOW: LeftHand; belly->character back->target; same pose drawn+sheathed";
                    break;

                case WeaponArchetype.Staff:
                    plan.Mount = animator.GetBoneTransform(HumanBodyBones.RightHand);
                    plan.SamePoseDrawnAndSheathed = false;
                    plan.OutwardWorld = body.forward;
                    plan.UpWorld = body.up;
                    plan.Why = "STAFF: RightHand; head/finial up, butt down, front of head faces world; grip lower-middle third";
                    break;

                default:
                    plan.Mount = animator.GetBoneTransform(HumanBodyBones.RightHand);
                    plan.SamePoseDrawnAndSheathed = false;
                    plan.OutwardWorld = body.forward;
                    plan.UpWorld = body.up;
                    plan.Why = "SWORD/default: RightHand";
                    break;
            }

            if (plan.Mount == null)
            {
                plan.Why += " — bone missing on this Avatar";
                FlowTrace.Fail("Equip", "GearSeat.ResolveMount archetype=" + archetype + ": " + plan.Why);
            }
            else
                FlowTrace.Step("Equip",
                    "GearSeat.ResolveMount archetype=" + archetype + " mount='" + plan.Mount.name +
                    "' samePose=" + plan.SamePoseDrawnAndSheathed + " — " + plan.Why);
            return plan;
        }

        /// <summary>
        /// Dedicated empty under LeftLowerArm. Created after the Avatar is Humanoid.
        /// Existing empties are reused (already bone-local, so clips do not yank them).
        /// </summary>
        public static Transform EnsureShieldSocket(Animator animator, Transform body)
        {
            if (animator == null || !animator.isHuman) return null;

            Transform forearm = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            string boneUsed = "LeftLowerArm";
            if (forearm == null)
            {
                forearm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
                boneUsed = "LeftUpperArm";
            }
            if (forearm == null)
            {
                FlowTrace.Warn("Equip",
                    "GearSeat.EnsureShieldSocket: Avatar has no LeftLowerArm/LeftUpperArm.");
                return null;
            }

            Transform existing = forearm.Find(ShieldSocketName);
            Transform socket;
            if (existing != null)
                socket = existing;
            else
            {
                var go = new GameObject(ShieldSocketName);
                go.layer = forearm.gameObject.layer;
                go.transform.SetParent(forearm, false);
                socket = go.transform;
                FlowTrace.Step("Equip",
                    "GearSeat.EnsureShieldSocket created under '" + forearm.name + "' (" + boneUsed + ").");
            }
            // Pin every resolve: a leftover LookRotation from the previous seat would tilt
            // with whatever was baked that frame. Identity + midpoint is the owner rule.
            OrientShieldSocket(socket, forearm, animator, body);
            return socket;
        }

        /// <summary>
        /// Mid-forearm empty. Local rotation is IDENTITY so heater-up rides the
        /// forearm bone and LeftHand wrist clips cannot tilt the plate. Position is
        /// the midpoint of LeftLowerArm (elbow→wrist) — the black dot on the diagram.
        /// </summary>
        public static void OrientShieldSocket(Transform socket, Transform forearm, Animator animator, Transform body)
        {
            if (socket == null || forearm == null) return;
            socket.localRotation = Quaternion.identity;

            Transform wrist = animator != null ? animator.GetBoneTransform(HumanBodyBones.LeftHand) : null;
            if (wrist != null)
            {
                Vector3 midWorld = (forearm.position + wrist.position) * 0.5f;
                socket.localPosition = forearm.InverseTransformPoint(midWorld);
            }
            else
                socket.localPosition = new Vector3(0f, 0.12f, 0f);

            FlowTrace.Throttle("Equip", "gear-seat-shield-socket", 5f,
                "GearSeat.OrientShieldSocket on '" + forearm.name +
                "': localRot=identity midForearm localPos=" + socket.localPosition +
                " (hand is a follower, not the pivot).");
        }

        /// <summary>
        /// Owner 3-panel (yellow line = arm). The forearm IS the heater's long
        /// axis: widest end (top) at the elbow, point at the hand. Paint faces
        /// outboard (left profile shows the emblem; front shows the inner/edge;
        /// back shows straps). Opening depth is the remaining perpendicular,
        /// inner toward the body, only the handle loop on the bone.
        /// </summary>
        public static void GetShieldAxes(Animator animator, Transform body, out Vector3 outward, out Vector3 up)
        {
            outward = body != null ? -body.right : -Vector3.right;
            up = body != null ? body.up : Vector3.up;
            if (animator == null || !animator.isHuman) return;

            Transform forearm = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            if (forearm == null) forearm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            Transform wrist = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            if (forearm == null) return;

            Vector3 alongArm = wrist != null
                ? (wrist.position - forearm.position)
                : forearm.TransformDirection(Vector3.up);
            if (alongArm.sqrMagnitude < 1e-8f) alongArm = forearm.up;
            alongArm.Normalize();

            // Yellow line: elbow → hand = point of the heater. Top (widest) is
            // the other way, toward the shoulder.
            up = -alongArm;
            Vector3 bodyUp = body != null ? body.up : Vector3.up;
            if (Vector3.Dot(up, bodyUp) < 0f) up = -up;

            // Paint outboard of the left arm, not toward body.forward (that is
            // what put the emblem on the front camera). ⊥ the bone so the
            // opening depth is the handle loop around the forearm.
            Vector3 leftOut = body != null ? -body.right : -Vector3.right;
            outward = Vector3.ProjectOnPlane(leftOut, alongArm);
            if (outward.sqrMagnitude < 1e-6f) outward = Vector3.ProjectOnPlane(leftOut, up);
            if (outward.sqrMagnitude < 1e-6f) outward = leftOut;
            outward.Normalize();
            up = Vector3.ProjectOnPlane(up, outward);
            if (up.sqrMagnitude < 1e-6f) up = -alongArm;
            up.Normalize();
        }

        /// <summary>
        /// Opening / straps / concave = INNER (toward the arm). Opposite = OUTER (paint).
        /// If that outer faces the rear camera, or is more than 90° off outboard, yaw 180°
        /// about heater-up. That yaw is the screenshot bug (full coat of arms from behind).
        /// Does not swap top/point.
        /// </summary>
        public static Quaternion EnsureShieldOuterFaces(
            Quaternion mountLocal, Transform mount, Vector3 desiredOutward, Vector3 heaterUp,
            WeaponOrientHelper.ShieldFrame frame, Transform body)
        {
            if (mount == null || !frame.Valid) return mountLocal;
            Quaternion world = mount.rotation * mountLocal;
            Vector3 thickWorld = world * frame.ThicknessAxis;
            // Handle / straps / opening sit on one side of thickness — that side is INNER.
            // Outer is the other side (smooth convex, painted).
            Vector3 innerWorld = (frame.HandleResolved && frame.HandleOnPositiveSide)
                ? thickWorld : -thickWorld;
            if (!frame.HandleResolved) innerWorld = -thickWorld;
            Vector3 outerWorld = -innerWorld;

            float faceOff = Vector3.Angle(outerWorld, desiredOutward);
            Vector3 rearCam = body != null ? -body.forward : Vector3.back;
            bool paintAtCamera = Vector3.Dot(outerWorld.normalized, rearCam.normalized) > 0.25f;
            if (faceOff <= 90f && !paintAtCamera) return mountLocal;

            world = Quaternion.AngleAxis(180f, heaterUp) * world;
            FlowTrace.Step("Equip",
                "GearSeat.EnsureShieldOuterFaces: opening/inner was aimed wrong (outerOff=" +
                faceOff.ToString("0.#") + "deg paintAtCamera=" + paintAtCamera +
                ") — yaw 180 about heater-up so the opening sits on the arm, paint outboard.");
            return Quaternion.Inverse(mount.rotation) * world;
        }

        /// <summary>
        /// Owner inner-face diagram (corrected): the ARM SEAT is the handle slot
        /// ACROSS the inner face (elbow one side, hand the other). The HANDLE is
        /// where that slot meets the bone — the only allowed intersection.
        /// After the AABB centre is on the socket, shift OUT along the opening
        /// (perpendicular to the arm) by half-thickness so the inner face/handle
        /// sits on the bone and the painted plate sits on TOP of the arm, not in it.
        /// </summary>
        public static Vector3 ShieldPlateOffBone(
            Transform socket, Vector3 outwardWorld, WeaponOrientHelper.ShieldFrame frame, Transform grip)
        {
            if (socket == null || !frame.Valid || outwardWorld.sqrMagnitude < 1e-8f)
                return Vector3.zero;
            Vector3 tWorld = grip != null
                ? grip.TransformVector(frame.ThicknessAxis)
                : frame.ThicknessAxis;
            float halfT = 0.5f * frame.Axes.NarrowestLen * Mathf.Max(tWorld.magnitude, 1e-4f);
            if (halfT > 0.12f) halfT = 0.12f;
            return socket.InverseTransformVector(outwardWorld.normalized * halfT);
        }

        /// <summary>
        /// Prefab dummy / handle loop (DUMMY_fantasy_shield). The forearm goes
        /// through this loop — snap it onto Socket_Shield.
        /// </summary>
        public static Transform FindHandleDummy(Transform root)
        {
            if (root == null) return null;
            var all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].name.IndexOf("DUMMY", StringComparison.OrdinalIgnoreCase) >= 0)
                    return all[i];
            }
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != root &&
                    all[i].name.IndexOf("Handle", StringComparison.OrdinalIgnoreCase) >= 0)
                    return all[i];
            }
            return null;
        }

        /// <summary>
        /// Puts the handle loop on the mid-forearm socket so the arm bone runs
        /// through the opening. When the forearm is parallel to the shoulders
        /// the heater is posed upright (world-up).
        /// </summary>
        public static void SnapHandleToSocket(Transform gripRoot, Transform socket)
        {
            Transform dummy = FindHandleDummy(gripRoot);
            if (dummy == null || socket == null || dummy == gripRoot) return;
            Vector3 delta = socket.position - dummy.position;
            gripRoot.position += delta;
            FlowTrace.Step("Equip",
                "GearSeat.SnapHandleToSocket '" + dummy.name + "' onto '" + socket.name +
                "' delta=" + delta + " (arm through the loop).");
        }
    }
}
