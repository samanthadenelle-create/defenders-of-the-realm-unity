// =============================================================================
// VillageController — the Avalon village scene orchestrator (Week-3 skeleton).
// -----------------------------------------------------------------------------
// Port spec Part 3 row: src/modules/village/Village3D.tsx -> VillageController.cs.
//
// One controller orchestrates the Village scene: the square wall ring, the four
// cardinal gates, the Heart (Elarion) at the centre, and the five buildings.
// Later weeks add the wave manager, build menu, hero rig and camera (port spec
// Part 5 Week 3-4) -- this Week-3 skeleton holds the structure + serialized
// references + the assemble pass that walks WallLayout.
//
// CANON: the town is "Avalon", the world-tree is "Elarion" / "the Heart". Those
// strings are not typed inline in user-facing copy -- the port spec routes them
// through data/canon-strings.json. This file uses them only in comments.
//
// The Editor-side VillageSceneBuilder builds the scene and wires the serialized
// fields below by reflection (the Editor asmdef does not reference this module).
// =============================================================================

using System.Collections.Generic;
using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>
    /// Orchestrates the Avalon village scene. Holds references to the wall ring,
    /// the four cardinal gates, the Heart and the five buildings; in Week 3 it
    /// owns the assemble pass that places the wall sections + gates against
    /// <see cref="WallLayout"/>. Wave manager / build menu / hero rig / camera
    /// are wired in later weeks.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VillageController : MonoBehaviour
    {
        [Header("Wall tier")]
        [Tooltip("Wall tier 0-3 (Wooden / Stone / Fortified / Warded). Sets section height.")]
        [SerializeField, Range(0, 3)] private int _wallTier;

        [Tooltip("Wall height per tier (world units). Mirrors WALL_TARGET_H in walls/constants.ts.")]
        [SerializeField] private float[] _wallHeightPerTier = { 3.0f, 3.8f, 4.5f, 5.2f };

        [Header("Scene roots (wired by VillageSceneBuilder)")]
        [Tooltip("Parent transform for the 20 wall sections (16 straight + 4 corners).")]
        [SerializeField] private Transform _wallRoot;

        [Tooltip("Parent transform for the 4 cardinal gates.")]
        [SerializeField] private Transform _gateRoot;

        [Tooltip("Parent transform for the 5 buildings.")]
        [SerializeField] private Transform _buildingRoot;

        [Header("Scene actors")]
        [Tooltip("Elarion -- the Heart at the village centre.")]
        [SerializeField] private HeartController _heart;

        [Tooltip("The 20 wall sections instantiated against WallLayout.Segments.")]
        [SerializeField] private List<WallSegment> _wallSegments = new List<WallSegment>();

        [Tooltip("The 4 cardinal gates instantiated against WallLayout.Gates.")]
        [SerializeField] private List<Gate> _gates = new List<Gate>();

        [Tooltip("The 5 village buildings.")]
        [SerializeField] private List<Building> _buildings = new List<Building>();

        /// <summary>Current wall tier (0-3).</summary>
        public int WallTier => _wallTier;

        /// <summary>Wall height (world units) for the current tier.</summary>
        public float WallHeight => _wallHeightPerTier[Mathf.Clamp(_wallTier, 0, _wallHeightPerTier.Length - 1)];

        /// <summary>The Heart at the village centre.</summary>
        public HeartController Heart => _heart;

        /// <summary>The instantiated wall sections.</summary>
        public IReadOnlyList<WallSegment> WallSegments => _wallSegments;

        /// <summary>The four cardinal gates.</summary>
        public IReadOnlyList<Gate> Gates => _gates;

        /// <summary>The five village buildings.</summary>
        public IReadOnlyList<Building> Buildings => _buildings;

        /// <summary>
        /// Registers a wall section with the controller and configures it from
        /// its <see cref="WallLayout"/> record. Called by the scene builder /
        /// runtime instantiation as each section is created.
        /// </summary>
        public void RegisterWallSegment(WallSegment segment, WallSegmentData data)
        {
            if (segment == null) return;
            segment.Configure(data, WallHeight);
            if (!_wallSegments.Contains(segment)) _wallSegments.Add(segment);
        }

        /// <summary>
        /// Registers a cardinal gate with the controller and configures it from
        /// its <see cref="WallLayout"/> gate-gap record.
        /// </summary>
        public void RegisterGate(Gate gate, GateGap gap)
        {
            if (gate == null) return;
            gate.Configure(gap);
            if (!_gates.Contains(gate)) _gates.Add(gate);
        }

        /// <summary>Registers a building with the controller.</summary>
        public void RegisterBuilding(Building building)
        {
            if (building == null) return;
            if (!_buildings.Contains(building)) _buildings.Add(building);
        }

        /// <summary>
        /// Sets the wall tier and re-applies the matching height to every
        /// registered wall section. Week 4+ also swaps the tier mesh / material.
        /// </summary>
        public void SetWallTier(int tier)
        {
            _wallTier = Mathf.Clamp(tier, 0, 3);
            float h = WallHeight;
            foreach (var seg in _wallSegments)
            {
                if (seg == null) continue;
                var data = new WallSegmentData
                {
                    Id = seg.SegmentId,
                    Index = seg.SegmentIndex,
                    Corner = seg.IsCorner,
                    Length = seg.Length,
                };
                seg.Configure(data, h);
            }
        }
    }
}
