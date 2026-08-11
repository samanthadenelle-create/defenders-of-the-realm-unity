// =============================================================================
// WeaponBoundsOrient — canonical mesh-axis seating (BINDING: docs/WEAPON_ARMOR_ORIENT_LOGIC.md).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.Geometry
//
// Y = LONG (longest — blade/haft). X = NARROW (thinnest — edge thickness).
// Z = WIDE (remaining axis — crossguard / blade width; thickest at the hilt).
// Handle = the SHORTER end of Y (min-Y after orient); blade points +Y.
// =============================================================================

using UnityEngine;

namespace DeNelle.Core.Geometry
{
    /// <summary>Orients a weapon prop: Y-long, X-narrow, Z-wide; hilt at the short Y end.</summary>
    public static class WeaponBoundsOrient
    {
        public enum GripAnchor
        {
            /// <summary>Bounds centre at the parent origin (bow centre-grip, shield strap).</summary>
            Centre,
            /// <summary>Handle end (min Y) at the origin after Y-long seating (melee hilt).</summary>
            HiltEnd
        }

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
            else
                prop.transform.localPosition -= b2.center;
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