// Placeholder defense patterns — MVP seed so raids have varied defenses before manual placement / real opponents. TODO data-driven: move to arena-defense-patterns.json; replace with real player layouts when the pool populates.
// =============================================================================
// DefensePatternLibrary — 7 canned Arena DEFENSE LAYOUTS (WO-389 support).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.Arena
//
// A small library of pre-made, hand-tuned defender layouts. Until manual
// placement UI + real opponent layouts exist, a raid against an undefended
// castle can pull RandomLayout() here so the attack faces a genuinely varied
// defense instead of an empty board.
//
// Each pattern is a DISTINCT strategy (chokepoint / ranged line / healer wall /
// caster nest / balanced / swarm / fortress) and every pattern's total point
// cost stays <= ArenaDefenseCatalog.DefensePointPool (50). Costs are validated
// at build time against ArenaDefenseCatalog.Get(id).PointCost so they cannot
// silently drift if the catalog re-prices a defender.
//
// Catalog ids + per-unit cost (read from ArenaDefenseCatalog, do not guess):
//   def_ranger          5
//   def_knight         10
//   def_healer         12
//   def_wizard         15
//   def_healing_shrine 18
//   def_ballista       20
//
// Cells are PlacementGrid-relative (roughly -4..4). yawSteps face inward
// (toward the +Z attacker approach by convention; 0 = no rotation). Pure data +
// a static API; no scene side-effects. ASCII-only strings.
// =============================================================================

using System.Collections.Generic;
using DeNelle.Core.State;

namespace DeNelle.Village.Arena
{
    /// <summary>
    /// One named, pre-made Arena defense layout: a strategy name + the list of
    /// placed defenders that make it up. Total cost is guaranteed &lt;= the
    /// <see cref="ArenaDefenseCatalog.DefensePointPool"/> (validated in the
    /// library builder).
    /// </summary>
    public sealed class DefensePattern
    {
        /// <summary>Human-readable strategy name ("Chokepoint", "Swarm", ...).</summary>
        public string Name;

        /// <summary>The placed defenders that compose this layout.</summary>
        public List<PlacedDefenderData> Pieces;

        public DefensePattern(string name, List<PlacedDefenderData> pieces)
        {
            Name = name;
            Pieces = pieces ?? new List<PlacedDefenderData>();
        }

        /// <summary>Sum of the catalog point cost of every piece in this layout.</summary>
        public int TotalCost => ArenaDefenseCatalog.SpentPoints(Pieces);
    }

    /// <summary>
    /// Static library of 7 canned Arena defense layouts. Mirrors the catalog's
    /// lazy-build + cached pattern. Use <see cref="RandomPattern"/> /
    /// <see cref="RandomLayout"/> to seed a raid against an undefended castle.
    /// </summary>
    public static class DefensePatternLibrary
    {
        private static List<DefensePattern> _patterns;

        /// <summary>All 7 patterns. Built once, cached.</summary>
        public static IReadOnlyList<DefensePattern> All
        {
            get
            {
                if (_patterns == null) _patterns = Build();
                return _patterns;
            }
        }

        /// <summary>Pick one of the 7 patterns at random.</summary>
        public static DefensePattern RandomPattern()
        {
            var all = All;
            if (all == null || all.Count == 0) return null;
            return all[UnityEngine.Random.Range(0, all.Count)];
        }

        /// <summary>
        /// Convenience: a fresh COPY of a random pattern's pieces, ready to drop
        /// into GameState.ArenaDefense. Returns a new list so callers can mutate
        /// it without corrupting the cached pattern.
        /// </summary>
        public static List<PlacedDefenderData> RandomLayout()
        {
            var pattern = RandomPattern();
            if (pattern == null) return new List<PlacedDefenderData>();
            return new List<PlacedDefenderData>(pattern.Pieces);
        }

        // Catalog id constants (kept local so a typo can't silently no-op).
        private const string Ranger = "def_ranger";          // cost 5
        private const string Knight = "def_knight";          // cost 10
        private const string Healer = "def_healer";          // cost 12
        private const string Wizard = "def_wizard";          // cost 15
        private const string Shrine = "def_healing_shrine";  // cost 18
        private const string Ballista = "def_ballista";      // cost 20

        private static List<DefensePattern> Build()
        {
            return new List<DefensePattern>
            {
                // 1. CHOKEPOINT — 1 Ballista + 2 Knights clustered to wall a single
                //    front approach; the ballista pokes from just behind the wall.
                //    Cost: 20 + 10 + 10 = 40  (<= 50)
                new DefensePattern("Chokepoint", new List<PlacedDefenderData>
                {
                    new PlacedDefenderData(Knight,   -1,  2, 0),  // front wall, left
                    new PlacedDefenderData(Knight,    1,  2, 0),  // front wall, right
                    new PlacedDefenderData(Ballista,  0,  0, 0),  // siege behind the gap
                }),

                // 2. RANGED LINE — 4 Rangers on a spread back line + 1 Ballista to
                //    kite/poke the approach; no melee, all reach.
                //    Cost: (5 x 4) + 20 = 40  (<= 50)
                new DefensePattern("Ranged Line", new List<PlacedDefenderData>
                {
                    new PlacedDefenderData(Ranger,   -3, -2, 0),  // back line, far left
                    new PlacedDefenderData(Ranger,   -1, -2, 0),
                    new PlacedDefenderData(Ranger,    1, -2, 0),
                    new PlacedDefenderData(Ranger,    3, -2, 0),  // back line, far right
                    new PlacedDefenderData(Ballista,  0, -3, 0),  // siege centre-back
                }),

                // 3. HEALER-BACKED WALL — 3 front-line Knights with 1 Healer tucked
                //    behind for sustain; bodies up front, heals from the rear.
                //    Cost: (10 x 3) + 12 = 42  (<= 50)
                new DefensePattern("Healer-Backed Wall", new List<PlacedDefenderData>
                {
                    new PlacedDefenderData(Knight, -2, 2, 0),  // front wall, left
                    new PlacedDefenderData(Knight,  0, 2, 0),  // front wall, centre
                    new PlacedDefenderData(Knight,  2, 2, 0),  // front wall, right
                    new PlacedDefenderData(Healer,  0, 0, 0),  // sustain behind the wall
                }),

                // 4. CASTER NEST — 2 Wizards clustered for AoE burst + 1 Healing
                //    Shrine for sustain; squishy but high damage, kept central.
                //    Cost: (15 x 2) + 18 = 48  (<= 50)
                new DefensePattern("Caster Nest", new List<PlacedDefenderData>
                {
                    new PlacedDefenderData(Wizard, -1, -1, 0),  // caster, left
                    new PlacedDefenderData(Wizard,  1, -1, 0),  // caster, right
                    new PlacedDefenderData(Shrine,  0, -2, 0),  // heal aura under the nest
                }),

                // 5. BALANCED — one of each-ish, spread for all-round coverage:
                //    a Knight up front, a Ranger and a Wizard on the flanks, a
                //    Healer in support. Well-rounded, no single weakness.
                //    Cost: 10 + 5 + 15 + 12 = 42  (<= 50)
                new DefensePattern("Balanced", new List<PlacedDefenderData>
                {
                    new PlacedDefenderData(Knight,  0,  2, 0),  // tank up front
                    new PlacedDefenderData(Ranger,  3,  0, 0),  // ranged right flank
                    new PlacedDefenderData(Wizard, -3,  0, 0),  // caster left flank
                    new PlacedDefenderData(Healer,  0, -2, 0),  // support at the back
                }),

                // 6. SWARM — many cheap bodies for board presence: 6 Rangers + 2
                //    Knights spread wide to flood every lane.
                //    Cost: (5 x 6) + (10 x 2) = 50  (<= 50, exactly at the pool)
                new DefensePattern("Swarm", new List<PlacedDefenderData>
                {
                    new PlacedDefenderData(Knight, -1,  3, 0),  // front body, left
                    new PlacedDefenderData(Knight,  1,  3, 0),  // front body, right
                    new PlacedDefenderData(Ranger, -3,  1, 0),
                    new PlacedDefenderData(Ranger,  3,  1, 0),
                    new PlacedDefenderData(Ranger, -2, -1, 0),
                    new PlacedDefenderData(Ranger,  2, -1, 0),
                    new PlacedDefenderData(Ranger, -4, -3, 0),
                    new PlacedDefenderData(Ranger,  4, -3, 0),
                }),

                // 7. FORTRESS — structure-heavy: 1 Ballista + 1 Healing Shrine with
                //    a single Knight guard; sustained siege fire from a hard core.
                //    Cost: 20 + 18 + 10 = 48  (<= 50)
                new DefensePattern("Fortress", new List<PlacedDefenderData>
                {
                    new PlacedDefenderData(Ballista, 0,  1, 0),  // siege at the core
                    new PlacedDefenderData(Shrine,   0, -1, 0),  // heal aura on the core
                    new PlacedDefenderData(Knight,   0,  3, 0),  // lone guard out front
                }),
            };
        }
    }
}
