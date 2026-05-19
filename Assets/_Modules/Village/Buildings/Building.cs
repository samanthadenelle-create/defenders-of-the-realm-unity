// =============================================================================
// Building — one village building MonoBehaviour (Week-3 skeleton).
// -----------------------------------------------------------------------------
// Port spec Part 3 row: src/modules/village/buildings/ -> Building.cs.
//
// One Building MonoBehaviour, configured by a BuildingDef (the ScriptableObject
// the port table calls for lands with data/buildings.json in Week 4). Week-3
// depth: structure + serialized fields + the BuildingType enum for the five
// canonical buildings. HP, cost, upgrade costs, harvest-yield gameplay all land
// Week 4 (port spec Part 5).
//
// The five buildings (port spec Part 3 / docs/village-layout.md section 4):
//   Crystal Mine, Pet House, Arcane Tower, Workshop, Farm.
// VillageController instantiates one per building and calls Configure().
// =============================================================================

using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>
    /// The five canonical village buildings. Order is stable. Matches the
    /// per-type prefab list in the port table (CrystalMine, PetHouse,
    /// ArcaneTower, Farm, Workshop) and docs/village-layout.md section 4.
    /// </summary>
    public enum BuildingType
    {
        /// <summary>Primary resource building -- yields crystals.</summary>
        CrystalMine = 0,
        /// <summary>Houses the player's pet roster.</summary>
        PetHouse = 1,
        /// <summary>Forward defender near the heaviest spawn -- element-bonus passive.</summary>
        ArcaneTower = 2,
        /// <summary>Crafting station.</summary>
        Workshop = 3,
        /// <summary>Secondary resource building -- yields food.</summary>
        Farm = 4,
    }

    /// <summary>
    /// A single village building. Holds its type, footprint and (Week 4+) its
    /// HP / upgrade level. Configured by <see cref="VillageController"/> against
    /// the docs/village-layout.md placement table.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Building : MonoBehaviour
    {
        [Header("Identity")]
        [Tooltip("Which of the five canonical buildings this is.")]
        [SerializeField] private BuildingType _type = BuildingType.CrystalMine;

        [Tooltip("Stable id -- e.g. crystal-mine. Used as the damage / cooldown key.")]
        [SerializeField] private string _buildingId;

        [Tooltip("Display label -- e.g. \"Crystal Mine\". Week 4+ this flows through data/buildings.json.")]
        [SerializeField] private string _displayLabel;

        [Header("State (Week 4+)")]
        [Tooltip("Building HP. Loaded from data/buildings.json in Week 4.")]
        [SerializeField] private float _hp = 100f;

        [Tooltip("Max HP. Loaded from data/buildings.json in Week 4.")]
        [SerializeField] private float _maxHp = 100f;

        [Tooltip("Upgrade level. Drives upgrade-cost lookups in Week 4.")]
        [SerializeField, Min(1)] private int _level = 1;

        [Header("Footprint")]
        [Tooltip("AABB blocker so enemies path AROUND the building (village-layout.md section 6).")]
        [SerializeField] private BoxCollider _blocker;

        /// <summary>Which of the five canonical buildings this is.</summary>
        public BuildingType Type => _type;

        /// <summary>Stable id -- e.g. <c>crystal-mine</c>.</summary>
        public string BuildingId => _buildingId;

        /// <summary>Display label -- e.g. "Crystal Mine".</summary>
        public string DisplayLabel => _displayLabel;

        /// <summary>Building HP (Week 4+).</summary>
        public float Hp => _hp;

        /// <summary>Building max HP (Week 4+).</summary>
        public float MaxHp => _maxHp;

        /// <summary>Upgrade level (Week 4+).</summary>
        public int Level => _level;

        /// <summary>
        /// Wires this building's identity. Called by <see cref="VillageController"/>
        /// right after instantiation. HP / cost data is loaded later from
        /// <c>data/buildings.json</c> (Week 4).
        /// </summary>
        /// <param name="type">Which of the five canonical buildings this is.</param>
        /// <param name="buildingId">Stable id -- e.g. <c>crystal-mine</c>.</param>
        /// <param name="displayLabel">Display label -- e.g. "Crystal Mine".</param>
        public void Configure(BuildingType type, string buildingId, string displayLabel)
        {
            _type = type;
            _buildingId = buildingId;
            _displayLabel = displayLabel;
            EnsureBlocker();
        }

        private void Awake()
        {
            if (_blocker == null) _blocker = GetComponent<BoxCollider>();
        }

        /// <summary>
        /// Ensures the building has an AABB blocker so enemy pathing detours
        /// around it (village-layout.md section 6 -- "buildings should be
        /// axis-aligned bounding boxes that block pathfinding").
        /// </summary>
        private void EnsureBlocker()
        {
            if (_blocker == null) _blocker = GetComponent<BoxCollider>();
            if (_blocker == null) _blocker = gameObject.AddComponent<BoxCollider>();
        }
    }
}
