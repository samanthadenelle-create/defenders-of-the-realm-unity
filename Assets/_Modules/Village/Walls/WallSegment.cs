// =============================================================================
// WallSegment — one wall-ring section MonoBehaviour (Week-3 skeleton).
// -----------------------------------------------------------------------------
// Port spec Part 3 row: src/modules/village/walls/KayWalls.tsx -> WallSegment.cs.
//
// One MonoBehaviour per wall section on the square perimeter. VillageController
// instantiates one per WallLayout.Segments entry and calls Configure() to wire
// the section's identity + footprint. Week-3 depth: structure + serialized
// fields, no damage / rubble-collapse gameplay yet (that lands Week 4 with the
// enemy aggression pass, KayWalls.tsx's `isDestroyed` -> rubble logic).
//
// The actual KayKit straight-wall mesh is supplied as a child by the scene
// builder; this component just owns the section's data + collider sizing.
// =============================================================================

using System;
using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>
    /// A single section of the square wall ring. Holds the section's stable
    /// damage id, its <see cref="WallLayout"/> source data, and (Week 4+) its
    /// damage HP. Instantiated by <see cref="VillageController"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WallSegment : MonoBehaviour, IDamageableStructure
    {
        [Header("Identity")]
        [Tooltip("Stable damage id from WallLayout -- wall-<index>.")]
        [SerializeField] private string _segmentId;

        [Tooltip("Ordinal index in the generated WallLayout.Segments list.")]
        [SerializeField] private int _segmentIndex;

        [Tooltip("True for the four corner pieces (short square block, stands taller).")]
        [SerializeField] private bool _isCorner;

        [Header("Footprint")]
        [Tooltip("Length (world units) of this section along its side.")]
        [SerializeField] private float _length = 1f;

        [Tooltip("Thickness (world units) -- the section's short radial axis.")]
        [SerializeField] private float _thickness = WallLayout.WallThickness;

        [Tooltip("Wall height (world units). Set per tier by VillageController.")]
        [SerializeField] private float _height = 3f;

        [Header("State (Week 4+)")]
        [Tooltip("Accumulated damage 0-100. 100 = collapsed to rubble. Wired in Week 4.")]
        [SerializeField, Range(0f, 100f)] private float _damage;

        [Tooltip("Box collider blocking the hero / enemies on this section.")]
        [SerializeField] private BoxCollider _blocker;

        /// <summary>Stable damage id -- <c>wall-&lt;index&gt;</c>.</summary>
        public string SegmentId => _segmentId;

        /// <summary>Ordinal index in <see cref="WallLayout.Segments"/>.</summary>
        public int SegmentIndex => _segmentIndex;

        /// <summary>True for the four square corner pieces.</summary>
        public bool IsCorner => _isCorner;

        /// <summary>Length (world units) of this section along its side.</summary>
        public float Length => _length;

        /// <summary>Wall height (world units) for the current tier.</summary>
        public float Height => _height;

        /// <summary>Accumulated damage, 0-100. 100 = destroyed (Week 4+).</summary>
        public float Damage => _damage;

        /// <summary>True once the section has taken full damage (Week 4+).</summary>
        public bool IsDestroyed => _damage >= 100f;

        /// <summary>
        /// <see cref="IDamageableStructure"/> — true while the section still
        /// stands and an enemy can contact-attack it. False once it is rubble.
        /// </summary>
        public bool IsAlive => _damage < 100f;

        /// <summary>
        /// Raised whenever the section's accumulated damage changes — carries
        /// the new 0-100 damage value. HUD / rubble swap subscribe.
        /// </summary>
        public event Action<float> DamageChanged;

        /// <summary>Raised once when the section's damage reaches 100 (collapsed to rubble).</summary>
        public event Action<WallSegment> Collapsed;

        /// <summary>
        /// Wires this section from a <see cref="WallSegmentData"/> layout record.
        /// Called by <see cref="VillageController"/> right after instantiation.
        /// Sizes the box collider to the section footprint.
        /// </summary>
        /// <param name="data">The <see cref="WallLayout"/> record this section renders.</param>
        /// <param name="height">Wall height for the current tier (world units).</param>
        public void Configure(WallSegmentData data, float height)
        {
            _segmentId = data.Id;
            _segmentIndex = data.Index;
            _isCorner = data.Corner;
            _length = data.Length;
            _thickness = WallLayout.WallThickness;
            _height = height;
            RebuildCollider();
        }

        /// <summary>
        /// <see cref="IDamageableStructure"/> contact-attack entry point — an
        /// enemy in melee contact with this section routes its hit here.
        /// Accumulates onto <see cref="Damage"/> (clamped 0-100); at 100 the
        /// section is rubble and <see cref="Collapsed"/> fires. Closes
        /// week4-waves.md integration item 5 — enemies can wear walls down
        /// instead of pathing straight through (port spec Week 4).
        /// </summary>
        /// <param name="amount">Damage to accumulate. Non-positive values are ignored.</param>
        public void ApplyContactDamage(float amount)
        {
            if (amount <= 0f || IsDestroyed) return;
            _damage = Mathf.Clamp(_damage + amount, 0f, 100f);
            DamageChanged?.Invoke(_damage);
            if (_damage >= 100f) Collapsed?.Invoke(this);
        }

        /// <summary>
        /// Repairs the section, reducing accumulated damage (the village repair
        /// flow). Clamped at 0.
        /// </summary>
        /// <param name="amount">Damage to remove. Non-positive values are ignored.</param>
        public void Repair(float amount)
        {
            if (amount <= 0f) return;
            _damage = Mathf.Clamp(_damage - amount, 0f, 100f);
            DamageChanged?.Invoke(_damage);
        }

        private void Awake()
        {
            if (_blocker == null) _blocker = GetComponent<BoxCollider>();
        }

        /// <summary>Sizes the box collider to the section's box footprint.</summary>
        private void RebuildCollider()
        {
            if (_blocker == null) _blocker = GetComponent<BoxCollider>();
            if (_blocker == null) _blocker = gameObject.AddComponent<BoxCollider>();
            // Long axis is local X (matches the WallLayout rotation rule).
            _blocker.size = new Vector3(_length, _height, _thickness);
            _blocker.center = new Vector3(0f, _height * 0.5f, 0f);
        }
    }
}
