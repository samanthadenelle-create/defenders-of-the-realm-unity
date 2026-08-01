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

        /// <summary>
        /// S5 visual — OPTIONAL per-level model swap, indexed the SAME way as
        /// <see cref="upgradeCost"/>: <c>upgradeVisualPath[0]</c> = the Resources-relative
        /// prefab shown at L2, <c>upgradeVisualPath[1]</c> = at L3, … The base
        /// <see cref="CatalogEntry.visualPrefabPath"/> is always L1. NULL / empty / too-short
        /// for the requested level means "keep the previous level's model" (StructureTierVisual
        /// still steps scale + the bronze/silver/gold accent), so a row opts into a distinct
        /// upgraded silhouette (archer tower → round keep) with just this array and the rest
        /// of the progression stays the graceful data/tint fallback. JSON deserializes the
        /// optional "upgradeVisualPath" string array straight into this.
        /// </summary>
        public string[] upgradeVisualPath = null;

        /// <summary>
        /// S5 visual (WO-719 upgrade-tier albedo) - OPTIONAL per-level FORCED texture, indexed
        /// the SAME way as <see cref="upgradeVisualPath"/>: <c>upgradeTexturePath[0]</c> = the
        /// Resources-relative albedo forced onto the L2 tier model, <c>upgradeTexturePath[1]</c> =
        /// at L3. The base <see cref="CatalogEntry.visualTexturePath"/> is always L1. This is the
        /// per-tier twin of that field: when a <see cref="upgradeVisualPath"/> tier model is a Tripo
        /// FBX whose only Color map lives buried in its <c>.fbm</c> folder (does NOT survive a player
        /// build -> renders WHITE, exactly like the L1 spire before its fix), the reskin routes this
        /// flat Resources albedo through the fresh <c>TripoMaterialFixer.SetForcedTexture</c> so the
        /// upgraded model keeps its colour in the build. NULL / empty / too-short for the requested
        /// level -> falls back to the base <see cref="CatalogEntry.visualTexturePath"/> (which itself
        /// may be null = no forced texture). JSON deserializes "upgradeTexturePath" straight in.
        /// </summary>
        public string[] upgradeTexturePath = null;

        /// <summary>Village resolves this string -> the actual behaviour component (Core stays pure).</summary>
        public string behaviorId = null;

        /// <summary>
        /// COLLECTOR IDENTITY (owner 2026-07-10 generic build-mode) — for a
        /// <c>behaviorId:"ResourceCollector"</c> row, the ResourceBuildingProgression id
        /// (<c>farm</c> / <c>lumbermill</c> / <c>forge</c>) the placed collector accrues for.
        /// Pure-data (no Village ref); StructureFactory.AttachBehavior passes it to
        /// <c>ResourceCollector.Configure</c>. Null/empty → falls back to the entry id.
        /// Ignored by every other behaviorId. JSON deserializes "collectorBuildingId" straight in.
        /// </summary>
        public string collectorBuildingId = null;

        /// <summary>
        /// Phase 2 (owner): true = at most ONE of these may exist in the village (pet-house,
        /// forge, mill, arcane-tower, Heart). Pure-data flag only for now — the enforce /
        /// auto-find-existing wiring is a follow-up; this just carries the intent so build /
        /// designer mode and the placement validator can consult it later. Default false
        /// (most structures — towers, walls, decorations — are freely repeatable).
        /// </summary>
        public bool singleton = false;

        /// <summary>
        /// StructureSingleton v2 (owner only-ever-one ruling) - legacy baked scene-root
        /// GameObject names that REPRESENT this catalog row (singleton twin standdown /
        /// resurface + IsBuilt). A row with repo.singleton=true plus this list is FULLY
        /// enforced with zero code: an active baked twin counts as "built", a placed /
        /// recorded instance stands the twins down, and a post-sell empty state resurfaces
        /// them. Null/empty = none. JSON deserializes "bakedTwins" straight in.
        /// </summary>
        public string[] bakedTwins = null;

        /// <summary>
        /// WO-818 (owner mapping table 2026-08-01) - the KayKit NPC body SLUG for this
        /// structure's speaker (drillmaster / vendor). Resolved by the Village NPC
        /// injectors as <c>Resources.Load("NPCs/KayKit/" + npcModel)</c> FIRST, falling
        /// back to the legacy People-pack prefab chain, then the capsule placeholder.
        /// Pure data (no Village ref), modeled on <see cref="bakedTwins"/>. Null/empty
        /// (the default) = no KayKit body authored -> the People chain is used as before.
        /// OWNER-ONLY creative pick: a swap is a one-word JSON retag, never a code pick.
        /// JSON deserializes "npcModel" straight in.
        /// </summary>
        public string npcModel = null;

        /// <summary>
        /// WO-707 (owner taxonomy 2026-07-13) — STORAGE CONTAINER capacity. The three
        /// dedicated container buildings (Lumberyard wood / Foundry iron / Silo grain)
        /// hold the village's stock; trade buildings are pure vendor/upgrade shops and
        /// carry NO storage field. 0 (default) = not a container. Data-only today:
        /// capacity enforcement + the visible-fill readout land with the WO-672
        /// damage-to-stores loop. JSON deserializes "storageCapacity" straight in.
        /// </summary>
        public int storageCapacity = 0;

        /// <summary>
        /// WO-707 — which resource this container stores ("wood" / "iron" / "food").
        /// Enum-by-name string kept loose on purpose (pure data, no Village ref),
        /// matching the projectileStyle convention. Null (default) = none.
        /// JSON deserializes "storageResource" straight in.
        /// </summary>
        public string storageResource = null;

        /// <summary>
        /// WO-707 targeting ruling (owner, same session): CONTAINERS ONLY are enemy
        /// raid targets — trade/shop buildings never are (a raid can never soft-lock a
        /// vendor/talk-route). A row is a container iff it authors a positive
        /// <see cref="storageCapacity"/>. TODO(WO-707/WO-672): wire this seam into the
        /// ff.enemystructureaware sweep (Enemy.SweepForNearestStructure currently
        /// scores ANY live IDamageableStructure via ISiegeLootTarget) so shops are
        /// excluded and the container set is the loot-target set.
        /// </summary>
        public bool IsStorageContainer => storageCapacity > 0;

        /// <summary>
        /// COLLECTOR RESERVE capacity (owner creative 2026-07-24, TIGHT collect-loop) — the
        /// base number of units a resource COLLECTOR (behaviorId "ResourceCollector") holds
        /// in its pending buffer before it reads FULL, at level 1. ResourceCollector.ComputeCapacity
        /// reads this when &gt;0 and deepens it +50% per level above 1 (upgrading a collector
        /// holds more); the STEWARD collectorCap talent still multiplies on top. 0 (default)
        /// = not authored → the collector falls back to its legacy ~2h-of-production formula.
        /// Right-sizes the collect loop so collectors actually fill (a farm at ~150/min fills
        /// 1000 in ~7 min). DESIGNER-TUNABLE DATA — never hardcode the number in C#. Distinct
        /// from <see cref="storageCapacity"/> (that flags a raidable stock CONTAINER; this sizes
        /// a collector's pending buffer). JSON deserializes "capacity" straight in.
        /// </summary>
        public int capacity = 0;

        /// <summary>Placement conditions, evaluated at the free cursor.</summary>
        public PlacementRules placement = new PlacementRules();

        /// <summary>
        /// WO-764 — the per-item Y-height MULTIPLIER against the ONE global base ceiling
        /// (StructureFactory.YHeightVariable = 4 m). The skinned model is fit-to-HEIGHT so its
        /// world-bounds Y == <c>YHeightVariable * heightMul</c>. DEFAULT 1.0 = every building
        /// normalizes to the base ceiling (uniform, script-built town — the owner-locked model).
        /// Author an exception only for a class that should read taller/shorter: towers = 1.25
        /// (5 m at base 4), siege engines = 0.75 (3 m). Change the base in ONE place and the whole
        /// town re-scales together. JSON deserializes "heightMul" straight in. SUPERSEDES the
        /// deprecated absolute <see cref="visualHeight"/> below.
        /// </summary>
        public float heightMul = 1.0f;

        /// <summary>
        /// DEPRECATED (WO-764) — the legacy ABSOLUTE visual height (metres) the model was fit to.
        /// Superseded by <see cref="heightMul"/> (base × multiplier) and NO LONGER READ by
        /// StructureFactory.EffectiveVisualHeight. Retained only so any older serialized JSON that
        /// still carries a "visualHeight" key deserializes without error. Do NOT author new rows
        /// against it — use <see cref="heightMul"/>.
        /// </summary>
        public float visualHeight = 0f;

        // --- Combat stats (Tower defs) — copied straight off DefenseTower's public fields ---
        public float         range     = 0f;
        public float         damage    = 0f;
        public float         fireRate  = 0f;     // shots per second
        public bool          canHitAir = false;  // ground = false · wall-walk = true
        // ANTI-AIR SPECIALIST (owner 2026-07-08 — the Ballista counters the flying dragon):
        // when true the DefenseTower behaviour acquires ONLY flying targets (ICombatLayered.Layer
        // == Flying) and ignores all ground traffic. Implies canHitAir. Default false = every
        // existing tower keeps its ground-or-mixed behaviour. Read by StructureFactory → DefenseTower.AirOnly.
        public bool          airOnly   = false;
        public DamageElement element   = DamageElement.None;

        /// <summary>
        /// TOWER IDENTITY (owner 2026-07-08 "ballista shoots arrows not round pellet") —
        /// OPTIONAL projectile VISUAL style for tower behaviours. Enum-by-name string kept
        /// loose on purpose (pure data, no Village ref): "pellet" (legacy sphere, the
        /// default when null/empty/unknown), "bolt" (elongated shaft + tip, oriented along
        /// velocity — ballista/archer), "spell" (glowing orb + cast/impact VFX — arcane).
        /// Visual ONLY: damage/targeting/travel logic never read it. JSON deserializes the
        /// optional "projectileStyle" string straight in.
        /// </summary>
        public string projectileStyle = null;

        // --- AoE / debuff stats (WO-113 ArcaneTower) — OPTIONAL, additive ---
        // Only the ArcaneTower behaviour reads these; every other behaviorId ignores
        // them. All default 0 so existing rows are unaffected and the component falls
        // back to its own serialized defaults when a row omits them. JSON deserializes
        // "aoeRadius" / "slowSeconds" / "splashFraction" straight in.
        public float aoeRadius      = 0f;   // splash radius (m) around the impact; 0 = use component default
        public float slowSeconds    = 0f;   // Slow debuff duration (s) on every blast victim; 0 = no slow
        public float splashFraction = 0f;   // 0-1 fraction of damage to non-primary victims; 0 = use default
    }
}
