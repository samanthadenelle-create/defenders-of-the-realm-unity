// =============================================================================
// ArenaDefenseDef + ArenaDefenseCatalog — the 6 pre-placeable Arena DEFENDERS
// (CoC-style defense layer) + the point-pool budget rules (WO-389 Phase 1).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.Arena
//
// The defender spends a LIMITED point pool (DefensePointPool = 50) to pre-place
// troops/structures on their castle for Arena defense — SEPARATE from the normal
// base buildings. When someone raids the castle (WO-388), these spawn FRIENDLY
// (CombatFaction.Friendly) and auto-fight the raiders. This catalog is the data
// spine (Phase 1): the 6 defs + the budget math. Spawning + placement UI = later
// phases (reuse StoryCompanion / StructureFactory + the FF-style placement).
//
// MIRRORS ArenaCatalog (static class, lazy Build() into a cached List, All /
// Get(id)) + a plain def class. Troops are UNITS (HeroClass bodies), not
// CatalogEntry structures — so this is its own def type, not a base-build entry.
//
// STATS ARE HARDCODED for Phase 1. Every value is commented
//   // TODO data-driven: arena-defense.json
// because these eventually load via the project's CanonicalJson/CatalogRegistry
// path, exactly like the other catalogs. ASCII-only strings. Pure data + a
// static factory; no scene side-effects.
// =============================================================================

using System.Collections.Generic;
using DeNelle.Core.State;

namespace DeNelle.Village.Arena
{
    /// <summary>Whether an Arena defender is a mobile UNIT (a HeroClass body) or a
    /// static STRUCTURE (a placed behavior like a heal-aura shrine or a ballista).</summary>
    public enum DefenderKind
    {
        Unit,
        Structure,
    }

    /// <summary>
    /// One pre-placeable Arena defender: id, display name, point cost, kind, and
    /// hardcoded combat stats. Units carry a <see cref="UnitClass"/> (the HeroClass
    /// body reused for the troop); structures carry a <see cref="BehaviorId"/> (the
    /// StructureFactory behavior to attach, e.g. "HealAura" / "DefenseTower").
    /// Mirrors <c>ArenaOpponentDef</c> — a plain data class.
    /// </summary>
    public sealed class ArenaDefenseDef
    {
        /// <summary>Stable id (persistence + catalog key), e.g. "def_ranger".</summary>
        public string Id;
        /// <summary>Display name shown on the placement / setup UI ("Ranger").</summary>
        public string DisplayName;
        /// <summary>Point-pool cost to place this defender (sum must stay &lt;= the pool).</summary>
        public int PointCost;
        /// <summary>Unit (a HeroClass body) or Structure (a placed behavior).</summary>
        public DefenderKind Kind;
        /// <summary>For UNIT defenders — the HeroClass body reused as the troop. Null for structures.</summary>
        public HeroClass? UnitClass;
        /// <summary>For STRUCTURE defenders — the StructureFactory behavior id (e.g. "HealAura" / "DefenseTower"). Null for units.</summary>
        public string BehaviorId;

        // ── Hardcoded combat stats (Phase 1) ─────────────────────────────────
        /// <summary>Max hit points.</summary>
        public float Hp;
        /// <summary>Damage per attack (heal magnitude for support kinds).</summary>
        public float Damage;
        /// <summary>Targeting / attack range in metres.</summary>
        public float Range;
        /// <summary>Seconds between attacks.</summary>
        public float AttackInterval;
    }

    /// <summary>
    /// The 6 pre-placeable Arena defenders + the point-pool budget rules. Mirrors
    /// <see cref="ArenaCatalog"/> (static class, lazy <c>Build()</c> into a cached
    /// list, <c>All</c> / <c>Get(id)</c>).
    /// </summary>
    public static class ArenaDefenseCatalog
    {
        /// <summary>The defender point pool the player spends to pre-place defense.
        /// Sum of placed <see cref="ArenaDefenseDef.PointCost"/> must stay &lt;= this.</summary>
        public const int DefensePointPool = 50;   // TODO data-driven: arena-defense.json

        private static List<ArenaDefenseDef> _defs;

        /// <summary>All defenders, cheapest-first. Built once, cached.</summary>
        public static IReadOnlyList<ArenaDefenseDef> All
        {
            get
            {
                if (_defs == null) _defs = Build();
                return _defs;
            }
        }

        /// <summary>Look up a defender def by id, or null.</summary>
        public static ArenaDefenseDef Get(string id)
        {
            foreach (var d in All)
                if (d != null && d.Id == id) return d;
            return null;
        }

        private static List<ArenaDefenseDef> Build()
        {
            return new List<ArenaDefenseDef>
            {
                // -- Ranger: cheap ranged unit -----------------------------------
                new ArenaDefenseDef
                {
                    Id = "def_ranger",
                    DisplayName = "Ranger",
                    PointCost = 5,                  // TODO data-driven: arena-defense.json
                    Kind = DefenderKind.Unit,
                    UnitClass = HeroClass.Ranger,
                    Hp = 80f,                       // TODO data-driven: arena-defense.json
                    Damage = 12f,                   // TODO data-driven: arena-defense.json
                    Range = 14f,                    // TODO data-driven: arena-defense.json
                    AttackInterval = 1.2f,          // TODO data-driven: arena-defense.json
                },

                // -- Knight: tanky melee unit ------------------------------------
                new ArenaDefenseDef
                {
                    Id = "def_knight",
                    DisplayName = "Knight",
                    PointCost = 10,                 // TODO data-driven: arena-defense.json
                    Kind = DefenderKind.Unit,
                    UnitClass = HeroClass.Knight,
                    Hp = 220f,                      // TODO data-driven: arena-defense.json
                    Damage = 18f,                   // TODO data-driven: arena-defense.json
                    Range = 2.5f,                   // TODO data-driven: arena-defense.json
                    AttackInterval = 1.0f,          // TODO data-driven: arena-defense.json
                },

                // -- Wizard: high-damage caster unit -----------------------------
                new ArenaDefenseDef
                {
                    Id = "def_wizard",
                    DisplayName = "Wizard",
                    PointCost = 15,                 // TODO data-driven: arena-defense.json
                    Kind = DefenderKind.Unit,
                    UnitClass = HeroClass.Mage,
                    Hp = 100f,                      // TODO data-driven: arena-defense.json
                    Damage = 30f,                   // TODO data-driven: arena-defense.json
                    Range = 16f,                    // TODO data-driven: arena-defense.json
                    AttackInterval = 1.8f,          // TODO data-driven: arena-defense.json
                },

                // -- Healer: support unit (Damage = heal magnitude) --------------
                new ArenaDefenseDef
                {
                    Id = "def_healer",
                    DisplayName = "Healer",
                    PointCost = 12,                 // TODO data-driven: arena-defense.json
                    Kind = DefenderKind.Unit,
                    UnitClass = HeroClass.Cleric,
                    Hp = 110f,                      // TODO data-driven: arena-defense.json
                    Damage = 16f,                   // TODO data-driven: arena-defense.json (heal magnitude)
                    Range = 12f,                    // TODO data-driven: arena-defense.json
                    AttackInterval = 2.0f,          // TODO data-driven: arena-defense.json
                },

                // -- Healing Shrine: static heal-aura structure ------------------
                new ArenaDefenseDef
                {
                    Id = "def_healing_shrine",
                    DisplayName = "Healing Shrine",
                    PointCost = 18,                 // TODO data-driven: arena-defense.json
                    Kind = DefenderKind.Structure,
                    BehaviorId = "HealAura",        // TODO data-driven: arena-defense.json
                    Hp = 300f,                      // TODO data-driven: arena-defense.json
                    Damage = 10f,                   // TODO data-driven: arena-defense.json (heal-per-tick)
                    Range = 10f,                    // TODO data-driven: arena-defense.json (aura radius)
                    AttackInterval = 1.5f,          // TODO data-driven: arena-defense.json (tick interval)
                },

                // -- Ballista: static ranged structure (DefenseTower behavior) ---
                new ArenaDefenseDef
                {
                    Id = "def_ballista",
                    DisplayName = "Ballista",
                    PointCost = 20,                 // TODO data-driven: arena-defense.json
                    Kind = DefenderKind.Structure,
                    BehaviorId = "DefenseTower",    // TODO data-driven: arena-defense.json
                    Hp = 260f,                      // TODO data-driven: arena-defense.json
                    Damage = 40f,                   // TODO data-driven: arena-defense.json
                    Range = 20f,                    // TODO data-driven: arena-defense.json
                    AttackInterval = 2.5f,          // TODO data-driven: arena-defense.json
                },
            };
        }

        // =====================================================================
        //  Point-pool validation — sum of placed costs must stay <= the pool
        // =====================================================================

        /// <summary>
        /// Total point cost of an existing placed-defense layout (sum of each
        /// placed defender's <see cref="ArenaDefenseDef.PointCost"/>). An unknown
        /// itemId (no catalog match) contributes 0.
        /// </summary>
        public static int SpentPoints(IEnumerable<PlacedDefenderData> placed)
        {
            if (placed == null) return 0;
            var spent = 0;
            foreach (var p in placed)
            {
                var def = Get(p.itemId);
                if (def != null) spent += def.PointCost;
            }
            return spent;
        }

        /// <summary>
        /// Remaining points in the pool given an existing layout
        /// (<see cref="DefensePointPool"/> minus <see cref="SpentPoints"/>).
        /// </summary>
        public static int RemainingPoints(IEnumerable<PlacedDefenderData> placed)
            => DefensePointPool - SpentPoints(placed);

        /// <summary>
        /// True when adding the defender <paramref name="addId"/> to the
        /// <paramref name="existing"/> layout keeps the total spend within the
        /// pool. A null/unknown <paramref name="addId"/> is not affordable.
        /// </summary>
        public static bool CanAfford(IEnumerable<PlacedDefenderData> existing, string addId)
        {
            var def = Get(addId);
            if (def == null) return false;
            return SpentPoints(existing) + def.PointCost <= DefensePointPool;
        }

        /// <summary>
        /// True when an entire placed-defense layout fits the pool
        /// (sum of costs &lt;= <see cref="DefensePointPool"/>).
        /// </summary>
        public static bool IsWithinBudget(IEnumerable<PlacedDefenderData> placed)
            => SpentPoints(placed) <= DefensePointPool;
    }
}
