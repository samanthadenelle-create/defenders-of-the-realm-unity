// =============================================================================
// FormationController — pure slot-offset calculator (WO-146).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// Stateless helper that, given (FormationType, slotIndex, slotCount, spacing),
// returns a leader-LOCAL offset (x = right, z = forward, y = 0). FamilyLeader
// converts to world via leaderPos + leaderRotation * offset.
//
// All shapes evenly distribute the slotCount members and add a small DETERMINISTIC
// per-slot noise (seed = slotIndex) so the pack doesn't look stamped. No Unity
// scene dependency beyond Vector3 — unit-testable, no allocation in the hot path.
// =============================================================================

using UnityEngine;
using DeNelle.Core;

namespace DeNelle.Village
{
    /// <summary>Computes per-slot leader-local offsets for the 5 family formations.</summary>
    public static class FormationController
    {
        /// <summary>
        /// Returns the leader-local offset for member <paramref name="slotIndex"/> of
        /// <paramref name="slotCount"/> in the given <paramref name="shape"/>.
        /// </summary>
        public static Vector3 LocalSlotOffset(
            FormationType shape, int slotIndex, int slotCount, float spacing,
            float ringRadius = 3f, float arcDegrees = 300f)
        {
            if (slotCount < 1) slotCount = 1;
            slotIndex = Mathf.Clamp(slotIndex, 0, slotCount - 1);

            Vector3 offset;
            switch (shape)
            {
                case FormationType.LooseRing:
                    offset = RingSlot(slotIndex, slotCount, ringRadius, arcDegrees, startBehind: true);
                    break;

                case FormationType.Wedge:
                    offset = WedgeSlot(slotIndex, spacing);
                    break;

                case FormationType.Line:
                    offset = new Vector3((slotIndex - (slotCount - 1) * 0.5f) * spacing, 0f, 0f);
                    break;

                case FormationType.TightPack:
                    offset = RingSlot(slotIndex, slotCount, ringRadius * 0.5f, 360f, startBehind: false);
                    break;

                case FormationType.Column:
                    offset = new Vector3(0f, 0f, -(slotIndex + 1) * spacing);
                    break;

                default:
                    offset = Vector3.zero;
                    break;
            }

            return offset + DeterministicNoise(slotIndex, spacing);
        }

        // Polar ring (or arc) of members; startBehind biases the arc to the rear.
        private static Vector3 RingSlot(int i, int count, float radius, float arcDegrees, bool startBehind)
        {
            float arc = Mathf.Clamp(arcDegrees, 1f, 360f);
            float startDeg = startBehind ? 180f - arc * 0.5f : 0f;
            float step = count > 1 ? arc / count : 0f;
            float deg = startDeg + i * step;
            float rad = deg * Mathf.Deg2Rad;
            // 0° = forward (+z); positive sweeps toward +x (right).
            return new Vector3(Mathf.Sin(rad) * radius, 0f, Mathf.Cos(rad) * radius);
        }

        // Arrowhead opening behind the leader: alternate L/R, each rank steps back+wide.
        private static Vector3 WedgeSlot(int i, float spacing)
        {
            int rank = i / 2 + 1;            // 1,1,2,2,3,3…
            float side = (i % 2 == 0) ? 1f : -1f;
            return new Vector3(side * rank * spacing, 0f, -rank * spacing);
        }

        // Deterministic per-slot jitter in the local plane (≈ 0.15 * spacing).
        private static Vector3 DeterministicNoise(int slotIndex, float spacing)
        {
            float mag = 0.15f * spacing;
            // Cheap hash → two pseudo-random components in [-1, 1].
            int h = slotIndex * 73856093;
            float nx = ((h & 0xFFFF) / 65535f) * 2f - 1f;
            float nz = (((h >> 8) & 0xFFFF) / 65535f) * 2f - 1f;
            return new Vector3(nx * mag, 0f, nz * mag);
        }
    }
}
