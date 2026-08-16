// =============================================================================
// WeaponBoundsOrient — canonical mesh-axis seating (BINDING: docs/WEAPON_ARMOR_ORIENT_LOGIC.md).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.Geometry
//
// Y = LONG (longest — blade/haft). X = NARROW (thinnest — edge thickness).
// Z = WIDE (remaining axis — crossguard / blade width; thickest at the hilt).
// Handle = the SHORTER end of Y (min-Y after orient); blade points +Y.
// =============================================================================

using System.Collections.Generic;
using UnityEngine;

namespace DeNelle.Core.Geometry
{
    /// <summary>Orients a weapon prop: Y-long, X-narrow, Z-wide; hilt at the short Y end.</summary>
    public static class WeaponBoundsOrient
    {
        public enum GripAnchor
        {
            /// <summary>Bounds centre at the parent origin (shield strap).</summary>
            Centre,
            /// <summary>Handle end (min Y) at the origin after Y-long seating (melee hilt).</summary>
            HiltEnd,
            /// <summary>
            /// WO-1105 R4 (owner rule, verbatim): "the longest piece is gonna be the y axis. You find
            /// the straight edge of the y axis, and you go down halfway. And that's gonna meet on the
            /// edge of a curve. The edge of the curve or the ninety degree from the midpoint is where
            /// the hand is gonna hold."
            /// <para>
            /// WHY THIS IS NOT <see cref="Centre"/>: a bow's bounding-box centre lies in the HOLLOW
            /// between the string and the belly — empty air BESIDE the wood — so seating the hand at
            /// bounds-centre floats the grip off the mesh. This anchor keeps the SAME midpoint on the
            /// long axis and projects it PERPENDICULAR, out to the ROUNDED EDGE, which is where a hand
            /// can close. Derived from measured geometry every time — never a per-weapon dialed
            /// offset. Owner-tuned manual=true entries in attachment-offsets.json are applied by the
            /// equip path AFTER this and are never overwritten here.
            /// </para>
            /// <para>
            /// ⚠ OWNER CORRECTION 2026-08-16 (the reason this is not the first-surface cast it was
            /// this morning), verbatim: "You're seating the bow on the correct axis in the right spot.
            /// However, you're doing it on the perpendicular from the midpoint of the y axis. You
            /// wanna follow that perpendicular from the y axis over to the rounded hilt. The round
            /// part of the bow is where the grip is. That's where you handle... You want the mid of
            /// the rounded or the perpendicular from the longest side, the y axis, where it intersects
            /// with the rounded edge." The AXIS assignment and the mid-Y start were CONFIRMED
            /// CORRECT; only the TERMINATION was wrong. The perpendicular must run all the way to the
            /// APEX of the bulge in Z at mid-Y — the riser's outermost point — not stop at the first
            /// surface it meets behind the string. Stopping early seats the hand on the string side of
            /// the riser (or on the string itself), which is why the bow still read wrong in hand.
            /// </para>
            /// <para>
            /// CROSSBOWS ARE EXCLUDED (R4a): a crossbow is widest on X / narrowest on Y and is held
            /// across the body, so the "longest -> +Y" premise this anchor stands on is wrong for it
            /// by construction. The inverted mapping is deliberately NOT implemented until the plain
            /// bow is proven on device; RangedPrimaryRegression pins that no crossbow id can reach
            /// the runtime weapons catalog while that exclusion stands.
            /// </para>
            /// </summary>
            BowGrip
        }

        // ── BowGrip tuning (all DIMENSIONLESS fractions of the prop's own measured size, so the
        //    solve is scale-free and there is not a single metre literal in the derivation) ──────
        /// <summary>Y bins used to profile the two long edges of the silhouette.</summary>
        private const int GripBins = 48;
        /// <summary>Fraction of the Y span (centred) used to judge which edge is STRAIGHT — the tips
        /// converge, so the ends cannot decide it.</summary>
        private const float StraightJudgeBand = 0.6f;
        /// <summary>Half-height of the cross-section sampled at the midpoint, as a fraction of the
        /// Y span (~3% = a ~3 cm slice on a 0.92 m bow).</summary>
        private const float SectionHalfBand = 0.03f;

        /// <summary>
        /// Parents <paramref name="prop"/> under <paramref name="parent"/>, seats Y-long / X-narrow /
        /// Z-wide, scales +Y to <paramref name="targetLength"/> m, orients handle at the short Y end
        /// (Z thickest at hilt), then applies the grip anchor.
        /// </summary>
        public static void NormalizeInto(GameObject prop, Transform parent, float targetLength,
                                         GripAnchor grip = GripAnchor.Centre,
                                         bool resolveBladeUpFromHilt = true)
        {
            prop.transform.SetParent(parent, false);
            prop.transform.localPosition = Vector3.zero;
            prop.transform.localRotation = Quaternion.identity;
            prop.transform.localScale = Vector3.one;

            if (!TryLocalBounds(prop, parent, out Bounds b0)) return;
            AlignAxesYLongXNarrowZWide(prop, b0.size);

            // SCALE BY THE LONGEST MEASURED AXIS — not blindly by Y. Captured proof (2026-07-06,
            // shield-size RCA): shield_A measured b0=(0.008,0.002,0.01) and AFTER AlignAxes the
            // longest axis sat on X, not Y (b1=(0.01,0.002,0.008)) — the align composition fails
            // for the longest=Z/shortest=Y permutation. Dividing by b1.size.y then scaled by the
            // 2mm THICKNESS (0.45/0.002 = 193x) and the 1cm face rendered 1.93m — the owner's
            // "shield larger than hero". The longest axis is the held length by definition, so
            // this is correct whether or not the align landed it on Y; the align's ROTATION is
            // left as-is (owner-dialed Offset Forge nudges compose on today's orientation).
            if (TryLocalBounds(prop, parent, out Bounds b1))
            {
                float longest = Mathf.Max(b1.size.x, Mathf.Max(b1.size.y, b1.size.z));
                if (longest > 1e-4f)
                    prop.transform.localScale = Vector3.one * (targetLength / longest);
            }
            // §12 solve trace: one line names this solve's inputs per prop.
            DeNelle.Core.Diagnostics.FlowTrace.Step("Equip",
                $"NormalizeInto '{prop.name}': raw b0={b0.size:0.###} aligned b1={b1.size:0.###} " +
                $"target={targetLength:0.###} -> propScale={prop.transform.localScale.x:0.###}");

            if (resolveBladeUpFromHilt)
                EnsureHandleAtShortYEnd(prop, parent);

            if (!TryLocalBounds(prop, parent, out Bounds b2)) return;
            if (grip == GripAnchor.HiltEnd)
            {
                Vector3 lp = prop.transform.localPosition;
                lp.x -= b2.center.x;
                lp.y -= b2.center.y - b2.extents.y;
                lp.z -= b2.center.z;
                prop.transform.localPosition = lp;
            }
            else if (grip == GripAnchor.BowGrip)
            {
                // WO-1105 R4 (+ the 2026-08-16 owner correction): seat the hand on the ROUNDED EDGE
                // the perpendicular from the straight edge's midpoint runs out to — not the hollow
                // at the bounds centre, and not the first surface behind the string. Falls back to
                // the bounds centre (the pre-R4 seat) only when the mesh cannot be measured, and
                // says so out loud (Section 12: no silent failures).
                if (TryDeriveBowGrip(prop, parent, b2, out Vector3 gripLocal))
                    prop.transform.localPosition -= gripLocal;
                else
                    prop.transform.localPosition -= b2.center;
            }
            else
                prop.transform.localPosition -= b2.center;
        }

        // =====================================================================
        //  WO-1105 R4 — BOW GRIP DERIVATION (owner rule; generalises to every bow)
        // =====================================================================

        /// <summary>
        /// Derives the grip point, in <paramref name="parent"/>-local space, for a prop already
        /// seated long-axis-on-+Y with measured bounds <paramref name="b"/>. In order:
        /// <list type="number">
        /// <item>bin the mesh by Y and record the two long EDGES of the silhouette (min/max local Z);</item>
        /// <item>over the middle <see cref="StraightJudgeBand"/> of the Y span, the edge whose Z
        ///       varies LEAST is the STRAIGHT edge (a bow's string side) — the other bows away;</item>
        /// <item>take that straight edge's MIDPOINT (mid-Y) and run PERPENDICULAR to it (along Z,
        ///       90 degrees from the long axis) toward the curved side;</item>
        /// <item>the grip is where that perpendicular reaches the ROUNDED EDGE — the Z-EXTREME of the
        ///       mid-Y cross-section on the curved side, i.e. the APEX of the riser's bulge. Owner
        ///       correction 2026-08-16: this is deliberately NOT a first-hit sample. A first hit
        ///       stops on the string, or on the string-facing back of the riser; the round part that
        ///       raises out in Z is the part the hand closes on.</item>
        /// </list>
        /// The apex is a MAX over the whole band (not a single ray), so a sparse or triangulated
        /// mesh cannot miss it the way a ray between two verts can.
        /// Returns false (grip unchanged) when no readable mesh vertices exist.
        /// </summary>
        private static bool TryDeriveBowGrip(GameObject prop, Transform parent, Bounds b, out Vector3 gripLocal)
        {
            gripLocal = b.center;

            float yMin = b.center.y - b.extents.y;
            float yMax = b.center.y + b.extents.y;
            float length = yMax - yMin;
            if (length < 1e-4f)
            {
                DeNelle.Core.Diagnostics.FlowTrace.Warn("Equip",
                    $"BowGrip '{prop.name}': degenerate Y span {length:0.####}m - falling back to the " +
                    "bounds-centre seat (the pre-R4 hollow grip).");
                return false;
            }

            var zLo = new float[GripBins];
            var zHi = new float[GripBins];
            var hit = new bool[GripBins];
            if (!CollectZEnvelope(prop, parent, yMin, length, GripBins, zLo, zHi, hit))
            {
                DeNelle.Core.Diagnostics.FlowTrace.Warn("Equip",
                    $"BowGrip '{prop.name}': no readable mesh vertices (Read/Write disabled?) - " +
                    "falling back to the bounds-centre seat (the pre-R4 hollow grip).");
                return false;
            }

            // (2) Which long edge is STRAIGHT? Judge only over the middle band: at the tips both
            //     edges converge, so the ends carry no information about straightness.
            int loBin = Mathf.Clamp(Mathf.RoundToInt(GripBins * (1f - StraightJudgeBand) * 0.5f), 0, GripBins - 1);
            int hiBin = Mathf.Clamp(GripBins - 1 - loBin, loBin, GripBins - 1);
            float loMin = float.MaxValue, loMax = float.MinValue;
            float hiMin = float.MaxValue, hiMax = float.MinValue;
            int judged = 0;
            for (int i = loBin; i <= hiBin; i++)
            {
                if (!hit[i]) continue;
                judged++;
                if (zLo[i] < loMin) loMin = zLo[i];
                if (zLo[i] > loMax) loMax = zLo[i];
                if (zHi[i] < hiMin) hiMin = zHi[i];
                if (zHi[i] > hiMax) hiMax = zHi[i];
            }
            if (judged == 0)
            {
                DeNelle.Core.Diagnostics.FlowTrace.Warn("Equip",
                    $"BowGrip '{prop.name}': no populated bins in the middle {StraightJudgeBand:0.##} " +
                    "band - falling back to the bounds-centre seat.");
                return false;
            }
            float loSpan = loMax - loMin;   // how much the -Z edge wanders along the length
            float hiSpan = hiMax - hiMin;   // how much the +Z edge wanders along the length
            bool straightIsLow = loSpan <= hiSpan;

            // (3) Midpoint of the straight edge, halfway down the long axis.
            float midY = 0.5f * (yMin + yMax);
            int midBin = NearestHitBin(hit, Mathf.Clamp((int)((midY - yMin) / length * GripBins), 0, GripBins - 1));
            if (midBin < 0) return false;
            float zStraight = straightIsLow ? zLo[midBin] : zHi[midBin];
            float dir = straightIsLow ? 1f : -1f;   // perpendicular cast direction, toward the curve

            // (4) Where the perpendicular reaches the ROUNDED EDGE. Sample the cross-section at the
            //     midpoint and take its Z-EXTREME on the curved side — the apex of the bulge.
            var band = new List<Vector3>();
            CollectBandVerts(prop, parent, midY, length * SectionHalfBand, band);
            if (band.Count == 0)
            {
                DeNelle.Core.Diagnostics.FlowTrace.Warn("Equip",
                    $"BowGrip '{prop.name}': cross-section at midY={midY:0.####} is empty - " +
                    "falling back to the bounds-centre seat.");
                return false;
            }

            // Walk the whole section once: the APEX is the vertex furthest along the perpendicular
            // (max signed distance from the straight edge, in the cast direction). Because the seat
            // is a max over every vertex in the band rather than a first-hit, the string — which
            // lives AT the straight edge, at distance ~0 — can never win, and neither can the
            // riser's string-facing back face. Nearest/furthest are both tracked so the trace can
            // state the section's real depth (the "was there a string in front of it" evidence).
            float apexDist = float.MinValue, nearDist = float.MaxValue;
            float xMin = float.MaxValue, xMax = float.MinValue;
            for (int i = 0; i < band.Count; i++)
            {
                float d = (band[i].z - zStraight) * dir;
                if (d > apexDist) apexDist = d;
                if (d < nearDist) nearDist = d;
                if (band[i].x < xMin) xMin = band[i].x;
                if (band[i].x > xMax) xMax = band[i].x;
            }
            float depth = apexDist - nearDist;

            // Mid-X of the band (the prop is X-narrow, so this is the thickness centre-line — the
            // hand closes AROUND the stave, not on one of its faces), mid-Y as ruled, apex Z.
            gripLocal = new Vector3(0.5f * (xMin + xMax), midY, zStraight + dir * apexDist);

            // Section 12: every number here is MEASURED off this prop's mesh this attach — a future
            // capture can re-derive the seat from this one line without re-running the solve.
            // `apexOverNearest` is the discriminator the owner correction turns on: when it is large,
            // the old first-surface rule was seating the hand that far off the rounded edge.
            DeNelle.Core.Diagnostics.FlowTrace.Step("Equip",
                $"BowGrip '{prop.name}': ySpan={length:0.####}m midY={midY:0.####} " +
                $"edgeWander(-Z)={loSpan:0.####} (+Z)={hiSpan:0.####} -> straightEdge=" +
                (straightIsLow ? "-Z" : "+Z") + $" curvedSide=" + (straightIsLow ? "+Z" : "-Z") +
                $" zStraight={zStraight:0.####} castDir=" + (dir > 0f ? "+Z" : "-Z") +
                $" sectionVerts={band.Count} sectionDepth={depth:0.####} " +
                $"nearest={nearDist:0.####} APEX={apexDist:0.####} apexZ={gripLocal.z:0.####} " +
                $"apexOverNearest={(apexDist - nearDist):0.####} -> grip=" +
                $"({gripLocal.x:0.####},{gripLocal.y:0.####},{gripLocal.z:0.####}) " +
                $"vs boundsCentre=({b.center.x:0.####},{b.center.y:0.####},{b.center.z:0.####}) " +
                $"offMesh={(gripLocal - b.center).magnitude:0.####}m");
            return true;
        }

        // =====================================================================
        //  BOW HELD ORIENTATION (owner ruling 2026-08-16 — "the bow must stand UPRIGHT")
        // =====================================================================
        //
        // WHY THIS EXISTS — the defect it fixes, stated as the seam it closes.
        // ---------------------------------------------------------------------
        // NormalizeInto + BowGrip solve the bow in the GRIP ROOT's OWN frame: long axis on +Y,
        // narrow on +X, grip seated on the riser apex. That solve is correct and is pinned
        // (RangedPrimaryRegression 'bow-grip-apex'). What it CANNOT know is anything about the
        // hand it is about to be parented to — the grip root's frame is not the bone's frame.
        //
        // HeroBowAttachment used to parent the solved bow to the LeftHand bone with an IDENTITY
        // local rotation (GripLocalEuler == 0), which maps prop-local +Y onto the HAND BONE's
        // local +Y. On this rig that axis is the "points out of the fist" direction — which is
        // exactly right for a SWORD (a blade continues the fist) and wrong by ~90 degrees for a
        // BOW (the hand closes AROUND the riser, so the limb span runs PERPENDICULAR to the fist,
        // not along it). That is the owner's report verbatim: the bow "lying horizontally across
        // his body ... rotated roughly 90 degrees about the grip point".
        //
        // The same class of defect is already documented in this repo for the melee families —
        // EquipmentController._staffGripEuler's RC5 note (2026-07-04): "these were ZERO, so an
        // un-corrected staff/wand/axe/mace inherited the bone's raw local axes and read SIDEWAYS
        // across the torso". The bow is the ONE long-axis-on-+Y family that never got a
        // correction, because ComputeMeleeGripRotation is gated on melee `kind` and the hero's bow
        // does not go through EquipmentController at all (it is deferred to HeroBowAttachment).
        //
        // WHY DERIVED, NOT A NUDGE EULER: the melee families settled this with a hand-typed
        // per-archetype nudge, which is a tuned constant that only holds for one rig. This solves
        // it the way ComputeSheathRotation already does (EquipmentController :2274) — build the
        // target in WORLD from the BODY's own axes, then express it in the bone's LOCAL frame so
        // it follows that bone through animation. No magic euler, no sign to guess, and it holds
        // on any rig whose body has an up and a forward. Per that precedent the result is used
        // WITHOUT ApplyGlobalWeaponYaw: the 180-degree yaw exists to correct grips that INHERITED
        // the raw bone axes, and composing it onto a fully-derived world target would spin the
        // belly to face backwards.

        /// <summary>
        /// The rotation, expressed in <paramref name="hand"/>'s LOCAL frame, that stands a
        /// BowGrip-seated bow UPRIGHT in the hand. Maps prop-local +Y — the limb-to-limb span
        /// that <see cref="AlignAxesYLongXNarrowZWide"/> puts there — onto the body's UP, and
        /// prop-local +Z — the riser's belly, the side the grip apex sits on, i.e. the face AWAY
        /// from the string — onto the body's FORWARD, so the curved limbs open away from what the
        /// archer is aiming at.
        /// <para>
        /// Owner rule (binding, applies to EVERY bow): "for a bow the LONGEST axis is Y (the
        /// limb-to-limb span), the grip is at mid-Y on the rounded side, and the hand grips
        /// perpendicular to that Y axis." CROSSBOWS ARE EXCLUDED — a crossbow IS held across the
        /// body and is widest on X, so this solve is wrong for it by construction;
        /// RangedPrimaryRegression case 1 pins that no crossbow can reach the runtime catalog.
        /// </para>
        /// Returns identity (and says so out loud) when either transform is missing — Section 12:
        /// no silent failures.
        /// </summary>
        public static Quaternion ComputeBowHeldRotation(Transform hand, Transform body)
        {
            if (hand == null || body == null)
            {
                DeNelle.Core.Diagnostics.FlowTrace.Warn("Equip",
                    "BowOrient: hand or body transform is NULL - falling back to an IDENTITY hand-local " +
                    "rotation, which is the pre-fix seat that lays the bow across the body. The bow will " +
                    "render horizontal; this line is the reason.");
                return Quaternion.identity;
            }

            // The two world directions the bow must answer to, read off the BODY (not the wrist,
            // which the animation owns): limbs stand along the body's up, belly faces its forward.
            Vector3 limb = body.up.normalized;
            Vector3 belly = body.forward;

            // Orthonormalize belly against limb so LookRotation can never be degenerate even on a
            // body whose forward has been tipped (ragdoll, slope-aligned root, editor fixture).
            belly -= Vector3.Dot(belly, limb) * limb;
            if (belly.sqrMagnitude < 1e-6f)
            {
                belly = Mathf.Abs(limb.z) < 0.9f ? Vector3.forward : Vector3.right;
                belly -= Vector3.Dot(belly, limb) * limb;
            }
            belly.Normalize();

            // LookRotation(forward, upwards): +Z -> forward, +Y -> upwards. We want prop +Z on the
            // belly and prop +Y on the limb span, so feed forward=belly, upwards=limb — the SAME
            // construction ComputeSheathRotation uses, for the same reason.
            Quaternion worldTarget = Quaternion.LookRotation(belly, limb);
            Quaternion handLocal = Quaternion.Inverse(hand.rotation) * worldTarget;

            // ── §12 PROVING LINE ──────────────────────────────────────────────────────────────
            // Re-composes the answer and MEASURES it, so the next capture proves the fix instead of
            // asserting it. `limbTiltFromVertical` is the number the owner's defect IS: it must read
            // ~0 deg (bow upright). `identityTilt` is what the SAME bow would have read under the
            // pre-fix identity seat — when that prints ~90 deg it is the captured evidence that the
            // hand bone's +Y is the fist axis and that an identity seat lays the bow horizontal.
            Quaternion composed = hand.rotation * handLocal;
            Vector3 limbWorld = composed * Vector3.up;
            Vector3 bellyWorld = composed * Vector3.forward;
            float limbTilt = Vector3.Angle(limbWorld, limb);
            float bellyOff = Vector3.Angle(bellyWorld, belly);
            float identityTilt = Vector3.Angle(hand.rotation * Vector3.up, limb);
            DeNelle.Core.Diagnostics.FlowTrace.Step("Equip",
                $"BowOrient hand='{hand.name}': bodyUp={limb:0.##} bodyFwd={belly:0.##} " +
                $"handLocalEuler={handLocal.eulerAngles:0.#} -> limbAxisWorld={limbWorld:0.##} " +
                $"bellyAxisWorld={bellyWorld:0.##} limbTiltFromVertical={limbTilt:0.#}deg " +
                $"bellyOffAim={bellyOff:0.#}deg | identitySeatWouldTilt={identityTilt:0.#}deg " +
                "(that is the pre-fix seat: ~90deg = the bow lying across the body)");
            return handLocal;
        }

        /// <summary>Nearest populated bin to <paramref name="start"/> (the midpoint bin can be empty
        /// on a sparse mesh); -1 when the profile is empty.</summary>
        private static int NearestHitBin(bool[] hit, int start)
        {
            if (hit[start]) return start;
            for (int step = 1; step < hit.Length; step++)
            {
                int a = start - step, c = start + step;
                if (a >= 0 && hit[a]) return a;
                if (c < hit.Length && hit[c]) return c;
            }
            return -1;
        }

        /// <summary>Per-Y-bin min/max local Z — the two long EDGES of the silhouette (signed, unlike
        /// <see cref="CollectZProfile"/>'s |z|, because BowGrip must tell the two sides apart).</summary>
        private static bool CollectZEnvelope(GameObject prop, Transform parent,
            float yMin, float length, int bins, float[] zLo, float[] zHi, bool[] hit)
        {
            bool any = false;
            float inv = bins / length;
            foreach (var src in ReadableMeshes(prop))
            {
                var verts = src.mesh.vertices;
                for (int v = 0; v < verts.Length; v++)
                {
                    Vector3 local = parent.InverseTransformPoint(src.owner.TransformPoint(verts[v]));
                    int bin = Mathf.Clamp((int)((local.y - yMin) * inv), 0, bins - 1);
                    if (!hit[bin]) { zLo[bin] = local.z; zHi[bin] = local.z; hit[bin] = true; }
                    else
                    {
                        if (local.z < zLo[bin]) zLo[bin] = local.z;
                        if (local.z > zHi[bin]) zHi[bin] = local.z;
                    }
                    any = true;
                }
            }
            return any;
        }

        /// <summary>Local-space vertices inside a thin Y slab centred on <paramref name="yCentre"/>.</summary>
        private static void CollectBandVerts(GameObject prop, Transform parent,
            float yCentre, float halfBand, List<Vector3> outVerts)
        {
            foreach (var src in ReadableMeshes(prop))
            {
                var verts = src.mesh.vertices;
                for (int v = 0; v < verts.Length; v++)
                {
                    Vector3 local = parent.InverseTransformPoint(src.owner.TransformPoint(verts[v]));
                    if (Mathf.Abs(local.y - yCentre) <= halfBand) outVerts.Add(local);
                }
            }
        }

        /// <summary>Every readable mesh on the prop (MeshFilter + SkinnedMeshRenderer) with the
        /// transform its vertices are authored in.</summary>
        private static IEnumerable<(Mesh mesh, Transform owner)> ReadableMeshes(GameObject prop)
        {
            foreach (var mf in prop.GetComponentsInChildren<MeshFilter>(true))
                if (mf != null && mf.sharedMesh != null && mf.sharedMesh.isReadable)
                    yield return (mf.sharedMesh, mf.transform);
            foreach (var smr in prop.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (smr != null && smr.sharedMesh != null && smr.sharedMesh.isReadable)
                    yield return (smr.sharedMesh, smr.transform);
        }

        /// <summary>Read-only shape signature after normalization (Y length / X thickness).</summary>
        public static bool TryAspectRatio(GameObject prop, Transform parent, out float yLongOverXNarrow)
        {
            yLongOverXNarrow = 0f;
            if (!TryLocalBounds(prop, parent, out Bounds b)) return false;
            float xNarrow = Mathf.Max(b.size.x, 1e-4f);
            yLongOverXNarrow = b.size.y / xNarrow;
            return yLongOverXNarrow > 0f;
        }

        // Longest → +Y, narrowest → +X, medium → +Z (crossguard width).
        //
        // ── WO-970 ROOT FIX (2026-08-10) — this method could only ever YAW ────────────────────────
        // PROVEN BY CAPTURE, owner Player.log, Mage carrying 'tripo_staff_a' (Emberglass Staff):
        //   NormalizeInto 'EquipmentProp_Weapon': raw b0=(0.001, 0.001, 0.021)
        //                                    aligned b1=(0.021, 0.001, 0.001)
        // and the same shape a month earlier for shield_A: raw (0.008,0.002,0.01) -> (0.01,0.002,0.008).
        // In BOTH the "aligned" result is the raw box with X and Z SWAPPED — i.e. a 90 deg yaw — and
        // the longest axis lands on X, never on +Y. The old construction below built its result as
        //   Quaternion.LookRotation(Cross(xAxis, yAxis), yAxis)   with   yAxis = Vector3.up (CONSTANT)
        // so the output rotation ALWAYS had its up on world +Y and its forward horizontal: a yaw-only
        // rotation, by construction. `alignLong` — the ONLY term that could tilt the mesh's long axis
        // up onto +Y — was used solely to pick the narrow-axis SIGN and was then thrown away. A yaw can
        // never lift a Z-long or X-long mesh to Y-long, so every prop whose SOURCE mesh is not already
        // authored Y-long stayed lying flat.
        //
        // Everything downstream is built on the premise this method is supposed to establish:
        // EnsureHandleAtShortYEnd bins on Y, SeatHiltLowerHalf seats the grip on Y, and
        // EquipmentController.ComputeMeleeGripRotation / ComputeSheathRotation both map "prop-local +Y
        // = the blade/haft line" onto the rig. With the premise false they were all operating on the
        // 1 mm THICKNESS axis: the captured sheathed staff measured worldBounds=(0.079, 0.097, 1.265)
        // — its whole 1.265 m ran along world Z, dead horizontal through the hero's back — and the
        // captured grip shift was 0.022 m instead of the ~0.4 m a 1.3 m haft needs.
        //
        // The 2026-07-06 shield RCA (comment in NormalizeInto above) found this same failure and
        // patched only the SCALE symptom, recording verbatim that "the align's ROTATION is left as-is".
        // This is that rotation, fixed at the root — DERIVED from the bounds permutation, never a
        // hand-typed Euler (docs/ARCHITECTURE_PRINCIPLES.md §4, docs/WEAPON_ARMOR_ORIENT_LOGIC.md).
        private static void AlignAxesYLongXNarrowZWide(GameObject prop, Vector3 size)
        {
            int lng = (size.x >= size.y && size.x >= size.z) ? 0 : (size.y >= size.z ? 1 : 2);
            int sht = (size.x <= size.y && size.x <= size.z) ? 0 : (size.y <= size.z ? 1 : 2);
            if (sht == lng) sht = (lng + 1) % 3;
            int med = 3 - lng - sht;

            // Build the basis change directly: we need the rotation R that carries the MESH's own
            // long axis onto +Y and its medium axis onto +Z (the narrow axis then falls on +X, which
            // is what "Y-long / X-narrow / Z-wide" means). Quaternion.LookRotation(forward, upwards)
            // yields the rotation S mapping (+Z, +Y) -> (med, long); its INVERSE is exactly the R we
            // want, because Inverse(S) * long = +Y and Inverse(S) * med = +Z by definition.
            // med and long are always distinct unit axes, so they are orthogonal and LookRotation is
            // never degenerate here. The result is a proper rotation, so handedness needs no fixup —
            // the old Dot(zAxis, medDir) sign patch existed only to repair the hand-built basis.
            // WHICH END is up is NOT decided here: EnsureHandleAtShortYEnd resolves hilt-vs-tip from
            // the Z profile immediately after, so this stays a pure axis solve.
            Quaternion meshToParent = Quaternion.Inverse(
                Quaternion.LookRotation(Axis(med), Axis(lng)));
            prop.transform.localRotation = meshToParent;

            // §12 permanent trace: names the permutation this solve read and the axis it seated the
            // length on. If a future capture ever shows longAxis on anything but Y after this line,
            // the derivation regressed — that is the single line that proves it.
            DeNelle.Core.Diagnostics.FlowTrace.Step("Equip",
                $"AlignAxes '{prop.name}': meshSize={size:0.###} longAxis={AxisName(lng)} " +
                $"narrowAxis={AxisName(sht)} wideAxis={AxisName(med)} -> seated long on +Y " +
                $"(localEuler={prop.transform.localRotation.eulerAngles:0.#})");
        }

        private static string AxisName(int i) => i == 0 ? "X" : i == 1 ? "Y" : "Z";

        // Z is thickest at the hilt; handle is the shorter Y segment — flip if blade points -Y.
        private static void EnsureHandleAtShortYEnd(GameObject prop, Transform parent)
        {
            if (!TryLocalBounds(prop, parent, out Bounds b)) return;
            float yMin = b.center.y - b.extents.y;
            float yMax = b.center.y + b.extents.y;
            float length = yMax - yMin;
            if (length < 1e-4f) return;

            const int Bins = 48;
            var zHi = new float[Bins];
            var hit = new bool[Bins];
            if (!CollectZProfile(prop, parent, yMin, length, Bins, zHi, hit))
            {
                // No readable verts: compare Z half-extent at each end via bounds slabs (coarse).
                float zLow = EndZExtent(prop, parent, yMin, length * 0.2f, true);
                float zHigh = EndZExtent(prop, parent, yMax, length * 0.2f, false);
                if (zHigh > zLow * 1.05f) Flip180AboutLocalX(prop);
                return;
            }

            int spikeBin = -1;
            float spikeZ = 0f;
            for (int i = 0; i < Bins; i++)
            {
                if (!hit[i]) continue;
                if (zHi[i] > spikeZ) { spikeZ = zHi[i]; spikeBin = i; }
            }
            if (spikeBin < 0) return;

            float binH = length / Bins;
            float spikeY = yMin + (spikeBin + 0.5f) * binH;
            float toMin = spikeY - yMin;
            float toMax = yMax - spikeY;
            // Handle = shorter segment; it must live at min-Y so the blade points +Y.
            if (toMin > toMax) Flip180AboutLocalX(prop);
        }

        private static void Flip180AboutLocalX(GameObject prop)
        {
            var flip = Quaternion.AngleAxis(180f, Vector3.right);
            prop.transform.localRotation = flip * prop.transform.localRotation;
            prop.transform.localPosition = flip * prop.transform.localPosition;
        }

        // Max |local.z| per Y bin — Z thickest at the hilt / crossguard.
        private static bool CollectZProfile(GameObject prop, Transform parent,
            float yMin, float length, int bins, float[] zHi, bool[] hit)
        {
            bool any = false;
            float inv = bins / length;
            foreach (var mf in prop.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf == null || mf.sharedMesh == null || !mf.sharedMesh.isReadable) continue;
                var verts = mf.sharedMesh.vertices;
                Transform mt = mf.transform;
                for (int v = 0; v < verts.Length; v++)
                {
                    Vector3 local = parent.InverseTransformPoint(mt.TransformPoint(verts[v]));
                    int bin = Mathf.Clamp((int)((local.y - yMin) * inv), 0, bins - 1);
                    float z = Mathf.Abs(local.z);
                    if (!hit[bin] || z > zHi[bin]) zHi[bin] = z;
                    hit[bin] = true;
                    any = true;
                }
            }
            foreach (var smr in prop.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (smr == null || smr.sharedMesh == null || !smr.sharedMesh.isReadable) continue;
                var verts = smr.sharedMesh.vertices;
                Transform mt = smr.transform;
                for (int v = 0; v < verts.Length; v++)
                {
                    Vector3 local = parent.InverseTransformPoint(mt.TransformPoint(verts[v]));
                    int bin = Mathf.Clamp((int)((local.y - yMin) * inv), 0, bins - 1);
                    float z = Mathf.Abs(local.z);
                    if (!hit[bin] || z > zHi[bin]) zHi[bin] = z;
                    hit[bin] = true;
                    any = true;
                }
            }
            return any;
        }

        private static float EndZExtent(GameObject prop, Transform parent, float yEdge, float band, bool fromMin)
        {
            float best = 0f;
            foreach (var mf in prop.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf == null || mf.sharedMesh == null || !mf.sharedMesh.isReadable) continue;
                var verts = mf.sharedMesh.vertices;
                Transform mt = mf.transform;
                for (int v = 0; v < verts.Length; v++)
                {
                    Vector3 local = parent.InverseTransformPoint(mt.TransformPoint(verts[v]));
                    bool inBand = fromMin ? local.y <= yEdge + band : local.y >= yEdge - band;
                    if (!inBand) continue;
                    best = Mathf.Max(best, Mathf.Abs(local.z));
                }
            }
            return best;
        }

        private static Vector3 Axis(int i) =>
            i == 0 ? Vector3.right : i == 1 ? Vector3.up : Vector3.forward;

        private static bool TryLocalBounds(GameObject prop, Transform parent, out Bounds bounds)
        {
            bounds = new Bounds();
            bool any = false;
            foreach (var r in prop.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null) continue;
                Bounds wb = r.bounds;
                Vector3 c = parent.InverseTransformPoint(wb.center);
                Vector3 e = parent.InverseTransformVector(wb.extents);
                var lb = new Bounds(c, new Vector3(Mathf.Abs(e.x), Mathf.Abs(e.y), Mathf.Abs(e.z)) * 2f);
                if (!any) { bounds = lb; any = true; }
                else bounds.Encapsulate(lb);
            }
            return any;
        }
    }
}