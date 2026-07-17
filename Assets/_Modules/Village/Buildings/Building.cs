// =============================================================================
// Building — one village building MonoBehaviour (Week-4: data-driven).
// -----------------------------------------------------------------------------
// Port spec Part 3 row: src/modules/village/buildings/ -> Building.cs.
//
// One Building MonoBehaviour, configured by a BuildingDef hydrated from the
// canonical data/buildings.json (Week 4). The Week-3 skeleton held structure +
// the BuildingType enum; Week 4 wires the data: a Configure(BuildingDef)
// overload pulls HP / max-HP / display-name key / crystal cost from the
// catalogue, and ApplyDamage / Repair drive the building's HP in waves.
//
// The five buildings (port spec Part 3 / docs/village-layout.md section 4):
//   Crystal Mine, Pet House, Arcane Tower, Workshop, Farm.
// VillageController instantiates one per building and calls Configure(); the
// build menu (BuildMenu.cs) does the same for a player-placed building.
// =============================================================================

using System;
using UnityEngine;
using DeNelle.Core.Combat;

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
        /// <summary>Sawmill -- yields lumber for upgrades (upgrade district).</summary>
        Lumbermill = 5,
        /// <summary>Forge -- sells + upgrades WEAPONS (vendor context "forge").</summary>
        Forge = 6,
        /// <summary>Armorer / Blacksmith -- sells + upgrades ARMOR (vendor context "armorer").</summary>
        Armorer = 7,
        /// <summary>Apothecary workbench -- opens the consumable-crafting / alchemy bench
        /// (PanelId.ConsumableCrafting). Opens the panel DIRECTLY (no Yarn structure hook).</summary>
        ApothecaryWorkbench = 8,
        /// <summary>Jeweler's bench -- opens the jewelry-crafting bench (PanelId.JewelerCrafting,
        /// WO-553). Opens the panel DIRECTLY (no Yarn structure hook), like the Apothecary.</summary>
        JewelersBench = 9,
    }

    /// <summary>
    /// A single village building. Holds its type, footprint and (Week 4+) its
    /// HP / upgrade level. Configured by <see cref="VillageController"/> against
    /// the docs/village-layout.md placement table.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Building : MonoBehaviour, IDamageableStructure
    {
        [Header("Identity")]
        [Tooltip("Which of the five canonical buildings this is.")]
        [SerializeField] private BuildingType _type = BuildingType.CrystalMine;

        [Tooltip("Stable id -- e.g. crystal-mine. Used as the damage / cooldown key.")]
        [SerializeField] private string _buildingId;

        [Tooltip("Display label -- e.g. \"Crystal Mine\". Week 4+ this flows through data/buildings.json.")]
        [SerializeField] private string _displayLabel;

        [Tooltip("canon-strings.json key for the display name (e.g. crystalMine). " +
                 "Loaded from data/buildings.json. NOT a literal -- resolved through CanonStrings.")]
        [SerializeField] private string _displayNameKey;

        [Tooltip("Crystal cost to raise this building. Loaded from data/buildings.json.")]
        [SerializeField] private int _crystalCost;

        [Header("State (Week 4)")]
        [Tooltip("Building HP. Loaded from data/buildings.json in Week 4.")]
        [SerializeField] private float _hp = 100f;

        [Tooltip("Max HP. Loaded from data/buildings.json in Week 4.")]
        [SerializeField] private float _maxHp = 100f;

        [Tooltip("Upgrade level. Drives upgrade-cost lookups in Week 4.")]
        [SerializeField, Min(1)] private int _level = 1;

        // Owner 2026-07-17 STRUCTURE-HP-per-tier: the building's UN-multiplied base max HP (the value
        // authored in the inspector / buildings.json), captured once so the tier HP bonus recomputes
        // idempotently as max = base * mult — a repeated apply never compounds. 0 = not yet captured.
        private float _baseMaxHp;

        [Header("Footprint")]
        [Tooltip("AABB blocker so enemies path AROUND the building (village-layout.md section 6).")]
        [SerializeField] private BoxCollider _blocker;

        /// <summary>Which of the five canonical buildings this is.</summary>
        public BuildingType Type => _type;

        /// <summary>Stable id -- e.g. <c>crystal-mine</c>.</summary>
        public string BuildingId => _buildingId;

        /// <summary>Display label -- e.g. "Crystal Mine".</summary>
        public string DisplayLabel => _displayLabel;

        /// <summary>
        /// canon-strings.json key for the display name (e.g. <c>crystalMine</c>).
        /// NOT a literal -- callers resolve it through CanonStrings (port spec
        /// Part 4: canon strings are never typed inline).
        /// </summary>
        public string DisplayNameKey => _displayNameKey;

        /// <summary>Crystal cost to raise this building (from data/buildings.json).</summary>
        public int CrystalCost => _crystalCost;

        /// <summary>Building HP (Week 4+).</summary>
        public float Hp => _hp;

        /// <summary>Building max HP (Week 4+).</summary>
        public float MaxHp => _maxHp;

        /// <summary>Upgrade level (Week 4+).</summary>
        public int Level => _level;

        /// <summary>HP as a 0-1 fraction of max HP.</summary>
        public float HpFraction => _maxHp > 0f ? Mathf.Clamp01(_hp / _maxHp) : 0f;

        /// <summary>True once the building's HP has reached zero.</summary>
        public bool IsDestroyed => _hp <= 0f;

        /// <summary>
        /// <see cref="IDamageableStructure"/> — true while the building still
        /// stands and an enemy can contact-attack it.
        /// </summary>
        public bool IsAlive => _hp > 0f;

        /// <summary>Raised whenever HP changes -- carries (current, max). HUD / damage flash subscribe.</summary>
        public event Action<float, float> HpChanged;

        /// <summary>Raised once when the building's HP reaches zero.</summary>
        public event Action<Building> Destroyed;

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

        /// <summary>
        /// Configures this building from its canonical <see cref="BuildingDef"/>
        /// (hydrated from <c>data/buildings.json</c> by <see cref="BuildingCatalog"/>).
        /// This is the Week-4 data-driven path: it sets identity AND pulls HP /
        /// max-HP / crystal cost / the display-name key from the catalogue, so
        /// no gameplay numbers are typed inline (port spec Part 4).
        /// Called by <see cref="VillageController"/> for the fixed buildings and
        /// by the build menu for a player-placed one.
        /// </summary>
        /// <param name="def">The canonical building definition. A null def is a no-op.</param>
        public void Configure(BuildingDef def)
        {
            if (def == null)
            {
                Debug.LogWarning("[Building] Configure(BuildingDef) called with a null def.");
                return;
            }

            _type = def.ResolvedType;
            _buildingId = def.Id;
            _displayNameKey = def.DisplayName;
            _crystalCost = def.CrystalCost;
            // _displayLabel stays a designer-facing fallback; the canon display
            // name flows through _displayNameKey -> CanonStrings at the UI layer.
            _maxHp = Mathf.Max(1f, def.MaxHp);
            _hp = Mathf.Clamp(def.Hp, 0f, _maxHp);
            // This is the authored BASE max HP — reset the capture so the tier bonus recomputes from it.
            _baseMaxHp = _maxHp;
            EnsureBlocker();
            HpChanged?.Invoke(_hp, _maxHp);
            // Owner 2026-07-17: fold in the current tier's STRUCTURE-HP bonus (no-op at tier 0 / non-city).
            SelfApplyTierHp(healToFull: false);
        }

        /// <summary>
        /// Convenience overload — looks the <see cref="BuildingDef"/> up in the
        /// <see cref="BuildingCatalog"/> by id and configures from it.
        /// </summary>
        public bool ConfigureFromCatalog(string buildingId)
        {
            var def = BuildingCatalog.Find(buildingId);
            if (def == null)
            {
                Debug.LogWarning($"[Building] No catalogue entry for id '{buildingId}'.");
                return false;
            }
            Configure(def);
            return true;
        }

        /// <summary>
        /// Applies wave damage to the building, clamps HP at zero and raises
        /// <see cref="HpChanged"/> / <see cref="Destroyed"/>. Enemies call this
        /// on contact (port spec Week 4 -- "attacks buildings/walls on contact").
        /// </summary>
        /// <param name="amount">Damage to apply. Non-positive values are ignored.</param>
        public void ApplyDamage(float amount)
        {
            if (amount <= 0f || IsDestroyed) return;
            _hp = Mathf.Max(0f, _hp - amount);
            HpChanged?.Invoke(_hp, _maxHp);
            if (_hp <= 0f) Destroyed?.Invoke(this);
        }

        /// <summary>
        /// Repairs the building, clamped at <see cref="MaxHp"/>. A repair on a
        /// destroyed building brings it back online.
        /// </summary>
        /// <param name="amount">HP to restore. Non-positive values are ignored.</param>
        public void Repair(float amount)
        {
            if (amount <= 0f) return;
            _hp = Mathf.Min(_maxHp, _hp + amount);
            HpChanged?.Invoke(_hp, _maxHp);
        }

        /// <summary>
        /// <see cref="IDamageableStructure"/> contact-attack entry point — an
        /// enemy in melee contact routes its hit here. A thin adapter onto the
        /// existing <see cref="ApplyDamage"/> so enemies can wear a building
        /// down (port spec Week 4; closes week4-waves.md integration item 5).
        /// </summary>
        public void ApplyContactDamage(float amount) => ApplyDamage(amount);

        private void Awake()
        {
            if (_blocker == null) _blocker = GetComponent<BoxCollider>();
        }

        private void Start()
        {
            // Owner 2026-07-17 STRUCTURE-HP-per-tier: on spawn, a building whose id is in the city-tier
            // catalog reads ITS current tier's HP bonus and raises max HP. Covers inspector-authored
            // buildings (Configure(type,id,label) leaves the default max HP) and reload of a saved tier.
            // Proportional (not heal-to-full) so a wave-damaged, saved building isn't silently topped off.
            SelfApplyTierHp(healToFull: false);
        }

        // ── STRUCTURE-HP-per-tier (owner 2026-07-17) ────────────────────────────

        /// <summary>
        /// The city-upgrade catalog id this building maps to (lumbermill / armorer / forge / arcane-tower /
        /// barracks / windmill), or null if it is not an upgradable city building. Mirrors
        /// BuildingInteractable.StructureHookIdFor so the HP bonus keys off the SAME id the upgrade panel
        /// uses. Resolves by explicit BuildingId first, then the GameObject name, then the BuildingType.
        /// </summary>
        public string UpgradeCatalogId
        {
            get
            {
                string id = (_buildingId ?? "").ToLowerInvariant();
                if (string.IsNullOrEmpty(id)) id = gameObject != null ? gameObject.name.ToLowerInvariant() : "";
                if (id.Length > 0)
                {
                    if (id.Contains("lumbermill")) return "lumbermill";
                    if (id.Contains("armorer")) return "armorer";
                    if (id == "forge" || id.Contains("forge")) return "forge";
                    if (id.Contains("barracks")) return "barracks";
                    if (id.Contains("windmill")) return "windmill";
                    if (id.Contains("arcane")) return "arcane-tower";
                }
                switch (_type)
                {
                    case BuildingType.Lumbermill: return "lumbermill";
                    case BuildingType.Forge:      return "forge";
                    case BuildingType.Armorer:    return "armorer";
                    case BuildingType.ArcaneTower: return "arcane-tower";
                }
                return null;
            }
        }

        /// <summary>
        /// Raises this building's max HP to base * <paramref name="mult"/> (idempotent — always computed
        /// from the captured base, never compounded). On <paramref name="healToFull"/> the HP is topped to
        /// the new max (an upgrade is a completed construction — CoC buildings finish full — so the boost is
        /// immediately visible); otherwise the current HP keeps its fraction of max (spawn / reload). A
        /// non-positive mult is treated as 1.0 (no-op). Fires <see cref="HpChanged"/> so HUD/bars refresh.
        /// </summary>
        public void ApplyStructureHpMultiplier(float mult, bool healToFull)
        {
            if (_baseMaxHp <= 0f) _baseMaxHp = _maxHp > 0f ? _maxHp : 1f;
            if (mult <= 0f) mult = 1f;
            float newMax = Mathf.Max(1f, _baseMaxHp * mult);
            float oldMax = _maxHp;
            if (healToFull)
            {
                _maxHp = newMax;
                _hp = newMax;
            }
            else
            {
                float frac = oldMax > 0f ? Mathf.Clamp01(_hp / oldMax) : 1f;
                _maxHp = newMax;
                _hp = Mathf.Clamp(frac * newMax, 0f, newMax);
            }
            HpChanged?.Invoke(_hp, _maxHp);
        }

        /// <summary>
        /// Look up THIS building's current-tier HP multiplier from the catalog (via
        /// ModifierService.StructureHpMultFor) and apply it. Null-safe no-op for a non-city building.
        /// </summary>
        private void SelfApplyTierHp(bool healToFull)
        {
            string cid = UpgradeCatalogId;
            if (string.IsNullOrEmpty(cid)) return;
            float mult = DeNelle.Core.State.ModifierService.StructureHpMultFor(cid);
            ApplyStructureHpMultiplier(mult, healToFull);
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
