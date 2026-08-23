// WO-1067: geometry evidence for weapon orientation. Bounds are deliberately not a verdict:
// +90 and -90 degree rotations have identical AABBs. This samples real mesh vertices at the
// two longitudinal ends, mirroring the taper proof that settled JewelerPitchSolver.
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor
{
    public readonly struct GearEndSpreadResult
    {
        public readonly float PositiveSpread;
        public readonly float NegativeSpread;
        public readonly float Ratio;
        public readonly int PositiveSamples;
        public readonly int NegativeSamples;
        public readonly bool Conclusive;

        public GearEndSpreadResult(float positive, float negative, int positiveSamples, int negativeSamples)
        {
            PositiveSpread = positive;
            NegativeSpread = negative;
            PositiveSamples = positiveSamples;
            NegativeSamples = negativeSamples;
            Ratio = negative > 0.0001f ? positive / negative : 1f;
            Conclusive = positiveSamples > 0 && negativeSamples > 0 &&
                         (Ratio < 0.8f || Ratio > 1.25f);
        }
    }

    public static class GearVisualGeometryAudit
    {
        /// <summary>
        /// Compares actual vertex spread in the positive and negative end bands of an axis.
        /// It can distinguish an inverted asymmetric mesh when AABB dimensions cannot. A
        /// conclusive result is evidence only; it never writes visualReadiness="ready".
        /// </summary>
        public static GearEndSpreadResult MeasureEndSpread(GameObject root, Vector3 worldAxis)
        {
            if (root == null || worldAxis.sqrMagnitude < 0.0001f) return default;
            worldAxis.Normalize();
            var filters = root.GetComponentsInChildren<MeshFilter>(true);
            if (filters.Length == 0) return default;

            float min = float.PositiveInfinity, max = float.NegativeInfinity;
            for (int f = 0; f < filters.Length; f++)
            {
                var mesh = filters[f].sharedMesh;
                if (mesh == null) continue;
                var vertices = mesh.vertices;
                for (int i = 0; i < vertices.Length; i++)
                {
                    float along = Vector3.Dot(filters[f].transform.TransformPoint(vertices[i]), worldAxis);
                    min = Mathf.Min(min, along); max = Mathf.Max(max, along);
                }
            }
            float span = max - min;
            if (float.IsNaN(span) || float.IsInfinity(span) || span <= 0.0001f) return default;
            float negativeCut = min + span * 0.2f;
            float positiveCut = min + span * 0.8f;
            Vector3 origin = root.transform.position;
            double positiveSum = 0d, negativeSum = 0d;
            int positiveCount = 0, negativeCount = 0;

            for (int f = 0; f < filters.Length; f++)
            {
                var mesh = filters[f].sharedMesh;
                if (mesh == null) continue;
                var vertices = mesh.vertices;
                for (int i = 0; i < vertices.Length; i++)
                {
                    Vector3 world = filters[f].transform.TransformPoint(vertices[i]);
                    float along = Vector3.Dot(world, worldAxis);
                    Vector3 radial = world - origin - worldAxis * Vector3.Dot(world - origin, worldAxis);
                    if (along >= positiveCut) { positiveSum += radial.magnitude; positiveCount++; }
                    else if (along <= negativeCut) { negativeSum += radial.magnitude; negativeCount++; }
                }
            }

            float positive = positiveCount > 0 ? (float)(positiveSum / positiveCount) : 0f;
            float negative = negativeCount > 0 ? (float)(negativeSum / negativeCount) : 0f;
            return new GearEndSpreadResult(positive, negative, positiveCount, negativeCount);
        }

        [MenuItem("Defenders/Art/Measure Selected Gear End Spread")]
        private static void MeasureSelected()
        {
            GameObject selected = Selection.activeGameObject;
            if (selected == null) { Debug.LogError("GEAR_GEOMETRY_FAIL - select a weapon instance."); return; }
            var result = MeasureEndSpread(selected, selected.transform.up);
            Debug.Log($"[GEAR_GEOMETRY] '{selected.name}' endSpread +={result.PositiveSpread:0.000} " +
                      $"-={result.NegativeSpread:0.000} ratio={result.Ratio:0.00} " +
                      $"samples={result.PositiveSamples}/{result.NegativeSamples} " +
                      $"conclusive={result.Conclusive}. Evidence only; owner visual approval still required.");
        }
    }
}
