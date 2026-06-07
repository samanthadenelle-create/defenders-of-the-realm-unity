// =============================================================================
// RepoProps — the BEHAVIOR half of a CatalogEntry (catalog ⊥ repo). Pure data.
// Combat stats are EXTRACTED VERBATIM from DefenseTower (rules-from-code); a
// cosmetic swaps the entry's visualPrefabPath and NEVER these — the structural
// cosmetic-only guarantee. behaviorId is resolved to a component by Village
// (keeps Core free of Village refs).
// =============================================================================
using DeNelle.Core.Combat;

namespace DeNelle.Core.Catalog
{
    /// <summary>
    /// S4 — a Core-friendly, pure-data multi-resource cost (Wood/Food/Iron/Crystals).
    /// Lives in Core so <see cref="RepoProps"/> (which Core owns) can carry it without a
    /// Village reference; the Village boundary (BuildModeController) maps it 1:1 to
    /// <c>EconomyService.ResourceCost</c> and charges it through the persisted ledger.
    /// All four slots default to 0 = "no multi-cost" (fall back to crystals-only buildCost).
    /// JSON deserializes the optional "cost" object's wood/food/iron/crystals straight in.
    /// </summary>
    [System.Serializable]
    public struct ResourceCost
    {
        public int wood;
        public int food;
        public int iron;
        public int crystals;

        /// <summary>True when every slot is zero — no multi-resource cost was authored.</summary>
        public bool IsZero => wood == 0 && food == 0 && iron == 0 && crystals == 0;
    }

    [System.Serializable]
    public sealed class RepoProps
    {
        /// <summary>What this contributes to the NavMesh once placed.</summary>
        public NavSurfaceKind navSurface = NavSurfaceKind.None;

        /// <summary>
        /// Crystals-only build cost (the legacy affordable rule reads this). Kept as a
        /// FALLBACK: when no multi-resource <see cref="cost"/> is supplied (all four
        /// slots zero), placement charges this many Crystals so older catalog rows and
        /// any back-compat path never regress. When <see cref="cost"/> is non-zero it
        /// takes precedence.
        /// </summary>
        public int buildCost = 0;

        /// <summary>
        /// S4 — the full multi-resource build cost (Wood/Food/Iron/Crystals). Pure-data,
        /// Core-friendly (no Village ref); the Village boundary maps it to
        /// EconomyService.ResourceCost and charges it through the persisted ResourceLedger.
        /// All-zero (the default) means "no multi-cost" → fall back to <see cref="buildCost"/>
        /// Crystals. JSON deserializes the optional "cost" object straight into this.
        /// </summary>
        public ResourceCost cost = new ResourceCost();

        /// <summary>
        /// S5 — the highest upgrade level this structure can reach (the CoC sink). 1 means
        /// "not upgradeable"; 3 is the canon wood→stone→reinforced / tower L1..L3 ceiling
        /// (matches StructureTierVisual's 1..3 tier clamp). Default 1 so a row that omits it
        /// stays a single-tier structure.
        /// </summary>
        public int maxLevel = 1;

        /// <summary>
        /// S5 — per-step upgrade cost, indexed by the level being upgraded TO minus 2:
        /// <c>upgradeCost[0]</c> = the cost to go L1→L2, <c>upgradeCost[1]</c> = L2→L3, …
        /// (so a structure with maxLevel 3 authors two entries). Pure-data, multi-resource.
        /// When NULL / too short for the requested step, the Village boundary falls back to a
        /// data scaler (the build <see cref="cost"/> scaled by the level being left), so a row
        /// can opt into upgrades with just <see cref="maxLevel"/> and no explicit table.
        /// JSON deserializes the optional "upgradeCost" array straight into this.
        /// </summary>
        public ResourceCost[] upgradeCost = null;

        /// <summary>Village resolves this string -> the actual behaviour component (Core stays pure).</summary>
        public string behaviorId = null;

        /// <summary>
        /// Phase 2 (owner): true = at most ONE of these may exist in the village (pet-house,
        /// forge, mill, arcane-tower, Heart). Pure-data flag only for now — the enforce /
        /// auto-find-existing wiring is a follow-up; this just carries the intent so build /
        /// designer mode and the placement validator can consult it later. Default false
        /// (most structures — towers, walls, decorations — are freely repeatable).
        /// </summary>
        public bool singleton = false;

        /// <summary>Placement conditions, evaluated at the free cursor.</summary>
        public PlacementRules placement = new PlacementRules();

        /// <summary>
        /// Real-world VISUAL height (metres) the skinned model is fit to. &gt;0 fits to
        /// HEIGHT (correct for tall structures like towers, so they read upright and
        /// right-sized regardless of the source asset's import scale); 0 (default)
        /// falls back to the legacy footprint-largest fit. DEF-208: a watchtower wants
        /// ~5 m, not its footprint (2.5 m) which squashed it.
        /// </summary>
        public float visualHeight = 0f;

        // --- Combat stats (Tower defs) — copied straight off DefenseTower's public fields ---
        public float         range     = 0f;
        public float         damage    = 0f;
        public float         fireRate  = 0f;     // shots per second
        public bool          canHitAir = false;  // ground = false · wall-walk = true
        public DamageElement element   = DamageElement.None;
    }
}
