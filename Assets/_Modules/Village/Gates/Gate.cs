// =============================================================================
// Gate — one cardinal force-field gate MonoBehaviour (Week-3 skeleton).
// -----------------------------------------------------------------------------
// Port spec Part 3 row: src/modules/village/walls/Gate.tsx -> Gate.cs.
//
// One MonoBehaviour per cardinal gate (N / E / S / W). VillageController
// instantiates one per WallLayout.Gates entry and calls Configure(). Week-3
// depth: structure + serialized fields. The violet force-field shimmer shader,
// the damage / collapse-below-25% AI, the pass-through filter (hero/pet through,
// Hollow Ones blocked) all land Week 4 per the build order (port spec Part 5).
//
// Geometry comes from docs/four-cardinal-gates-spec.md: on the SQUARE wall a
// gate sits centred in one side, the wall sections meet the pillars flush, and
// gate HP rides on the buildingDamage map as gate-0 .. gate-3.
// =============================================================================

using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>Cardinal direction of a gate -- parallel to <c>GATE_DIRECTIONS</c> in gate.ts.</summary>
    public enum GateDirection
    {
        North = 0,
        East = 1,
        South = 2,
        West = 3,
    }

    /// <summary>
    /// A single cardinal force-field gate centred in one side of the square
    /// wall. Holds its stable damage id (<c>gate-0</c> .. <c>gate-3</c>),
    /// direction, and (Week 4+) force-field HP / collider toggle.
    /// Instantiated by <see cref="VillageController"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Gate : MonoBehaviour
    {
        [Header("Identity")]
        [Tooltip("Stable damage id -- gate-0 (N) .. gate-3 (W). From WallLayout.Gates.")]
        [SerializeField] private string _gateId;

        [Tooltip("Cardinal direction -- drives HUD copy / repair-modal headlines.")]
        [SerializeField] private GateDirection _direction = GateDirection.North;

        [Tooltip("Cardinal heading (radians) from village centre toward this gate's side.")]
        [SerializeField] private float _angle;

        [Header("Force field (Week 4+)")]
        [Tooltip("Gate HP, 0-100. The force field collapses below 25%, letting Hollow Ones through.")]
        [SerializeField, Range(0f, 100f)] private float _hp = 100f;

        [Tooltip("Collider that blocks enemies while the force field is up. Toggled off on collapse.")]
        [SerializeField] private BoxCollider _forceFieldCollider;

        [Tooltip("Half-width (world units) of the gate opening. Matches WallLayout.GateHalfWidth.")]
        [SerializeField] private float _halfWidth = WallLayout.GateHalfWidth;

        /// <summary>HP fraction below which the force field collapses (Week 4 gameplay).</summary>
        public const float CollapseThreshold = 0.25f;

        /// <summary>Stable damage id -- <c>gate-0</c> .. <c>gate-3</c>.</summary>
        public string GateId => _gateId;

        /// <summary>Cardinal direction of this gate.</summary>
        public GateDirection Direction => _direction;

        /// <summary>Cardinal heading (radians) toward this gate's square side.</summary>
        public float Angle => _angle;

        /// <summary>Gate HP, 0-100.</summary>
        public float Hp => _hp;

        /// <summary>True while the force field still blocks enemies (HP above the collapse threshold).</summary>
        public bool IsForceFieldUp => _hp > 100f * CollapseThreshold;

        /// <summary>
        /// Wires this gate from a <see cref="GateGap"/> layout record. Called by
        /// <see cref="VillageController"/> right after instantiation.
        /// </summary>
        /// <param name="gap">The <see cref="WallLayout"/> gate-gap record this gate fills.</param>
        public void Configure(GateGap gap)
        {
            _gateId = gap.Id;
            _direction = (GateDirection)Mathf.Clamp(gap.Index, 0, 3);
            _angle = gap.Angle;
            _halfWidth = WallLayout.GateHalfWidth;
            RebuildCollider();
        }

        private void Awake()
        {
            if (_forceFieldCollider == null) _forceFieldCollider = GetComponent<BoxCollider>();
        }

        /// <summary>Sizes the force-field collider to span the gate opening.</summary>
        private void RebuildCollider()
        {
            if (_forceFieldCollider == null) _forceFieldCollider = GetComponent<BoxCollider>();
            if (_forceFieldCollider == null) _forceFieldCollider = gameObject.AddComponent<BoxCollider>();
            // Span runs along local X (matches the gate's WallLayout rotation rule).
            _forceFieldCollider.size = new Vector3(_halfWidth * 2f, 4f, WallLayout.WallThickness);
            _forceFieldCollider.center = new Vector3(0f, 2f, 0f);
            // Week 4 will gate this on IsForceFieldUp + the pass-through filter.
            _forceFieldCollider.isTrigger = false;
        }
    }
}
