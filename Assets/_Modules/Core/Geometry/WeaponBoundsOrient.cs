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

            if (TryLocalBounds(prop, parent, out Bounds b1) && b1.size.y > 1e-4f)
                prop.transform.localScale = Vector3.one * (targetLength / b1.size.y);

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
        private static void AlignAxesYLongXNarrowZWide(GameObject prop, Vector3 size)
        {
            int lng = (size.x >= size.y && size.x >= size.z) ? 0 : (size.y >= size.z ? 1 : 2);
            int sht = (size.x <= size.y && size.x <= size.z) ? 0 : (size.y <= size.z ? 1 : 2);
            if (sht == lng) sht = (lng + 1) % 3;
            int med = 3 - lng - sht;

            Quaternion alignLong = Quaternion.FromToRotation(Axis(lng), Vector3.up);
            Vector3 yAxis = Vector3.up;
            Vector3 narrowDir = alignLong * Axis(sht);
            narrowDir -= Vector3.Dot(narrowDir, yAxis) * yAxis;
            if (narrowDir.sqrMagnitude < 1e-6f) narrowDir = Vector3.right;
            Vector3 xAxis = narrowDir.normalized;
            Vector3 zAxis = Vector3.Cross(xAxis, yAxis).normalized;
            Vector3 medDir = (alignLong * Axis(med)).normalized;
            if (Vector3.Dot(zAxis, medDir) < 0f) xAxis = -xAxis;

            prop.transform.localRotation = Quaternion.LookRotation(Vector3.Cross(xAxis, yAxis), yAxis);
        }

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