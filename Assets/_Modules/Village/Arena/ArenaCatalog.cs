// =============================================================================
// ArenaOpponentDef + ArenaCatalog — the 3 SEEDED async-PvP opponents (ARENA MVP).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.Arena
//
// Each opponent = a base-recipe (a List<PlacedStructureData> fort, generated via
// the SAME OutpostFoundationGenerator.GenerateFootprintRecipe the village build
// mode + open-world camps use, at 3 sizes) + a garrison spec (boss is implicit in
// EnemyOutpost; N guards here, rising 1/4/8) + a rising threat tier (1/4/8 — drives
// stat scaling) + a wager tier (50/100/200 SKR) + a display name + flavour.
//
// These are HAND-AUTHORED, SEEDED opponents — NOT real matchmaking / a backend
// roster (that is a later bite). The base-as-opponent payload is the SAME
// List<PlacedStructureData> a real async raid would replay from a server, so when
// the backend lands these three are swapped for fetched opponent layouts with no
// change to the raid path (ArenaMode.Realize already takes a recipe).
//
// ASCII-only strings. Pure data + a static factory; no scene side-effects.
// =============================================================================

using System.Collections.Generic;
using DeNelle.Core.State;
using DeNelle.Village.World.Camps;

namespace DeNelle.Village.Arena
{
    /// <summary>One SEEDED Arena opponent: name, base recipe, garrison size, threat, wager.</summary>
    public sealed class ArenaOpponentDef
    {
        /// <summary>Stable id (persistence + raid-anchor key), e.g. "arena_ironhold".</summary>
        public string Id;
        /// <summary>Display name shown on the pick / result UI ("Ironhold Marauders").</summary>
        public string DisplayName;
        /// <summary>One-line flavour for the pick card.</summary>
        public string Flavour;
        /// <summary>Difficulty tier label (Tier 1 / 2 / 3) for the UI.</summary>
        public int Tier;
        /// <summary>Threat level feeding EnemyOutpost stat scaling (1 / 4 / 8).</summary>
        public int Threat;
        /// <summary>Guard count (excludes the implicit boss): 3 / 6 / 9.</summary>
        public int GuardCount;
        /// <summary>SKR stake to raid this opponent (50 / 100 / 200). Win pays 2x.</summary>
        public long Wager;
        /// <summary>The opponent's base layout (LOCAL grid cells) — realized as the fort.</summary>
        public List<PlacedStructureData> BaseRecipe;

        /// <summary>The purse paid on a win (the stake doubled — your stake back + theirs).</summary>
        public long WinPurse => Wager * 2L;
    }

    /// <summary>The 3 seeded Arena opponents (rising tier / garrison / wager).</summary>
    public static class ArenaCatalog
    {
        private static List<ArenaOpponentDef> _opponents;

        /// <summary>All seeded opponents, easiest-first. Built once, cached.</summary>
        public static IReadOnlyList<ArenaOpponentDef> All
        {
            get
            {
                if (_opponents == null) _opponents = Build();
                return _opponents;
            }
        }

        /// <summary>Look up a seeded opponent by id, or null.</summary>
        public static ArenaOpponentDef Get(string id)
        {
            foreach (var o in All)
                if (o != null && o.Id == id) return o;
            return null;
        }

        private static List<ArenaOpponentDef> Build()
        {
            return new List<ArenaOpponentDef>
            {
                // -- TIER 1: small wood fort, light garrison, low stake -----------
                new ArenaOpponentDef
                {
                    Id = "arena_ironhold",
                    DisplayName = "Ironhold Marauders",
                    Flavour = "A ragged border camp. Easy purse for a first wager.",
                    Tier = 1,
                    Threat = 1,
                    GuardCount = 3,
                    Wager = 50L,
                    // SAME generator the village build + open-world camps use, small footprint.
                    BaseRecipe = OutpostFoundationGenerator.GenerateFootprintRecipe(5, 5, OutpostTier.Wood),
                },

                // -- TIER 2: mid fort, real garrison, mid stake -------------------
                new ArenaOpponentDef
                {
                    Id = "arena_grimwatch",
                    DisplayName = "Grimwatch Reavers",
                    Flavour = "A fortified warband. The walls bite back. Decent stake.",
                    Tier = 2,
                    Threat = 4,
                    GuardCount = 6,
                    Wager = 100L,
                    BaseRecipe = OutpostFoundationGenerator.GenerateFootprintRecipe(7, 7, OutpostTier.Wood),
                },

                // -- TIER 3: large stone-equivalent fort, heavy garrison, big stake -
                new ArenaOpponentDef
                {
                    Id = "arena_blackbanner",
                    DisplayName = "Blackbanner Host",
                    Flavour = "A warlord's stronghold. High risk, high purse.",
                    Tier = 3,
                    Threat = 8,
                    GuardCount = 9,
                    Wager = 200L,
                    BaseRecipe = OutpostFoundationGenerator.GenerateFootprintRecipe(9, 9, OutpostTier.Stone),
                },
            };
        }
    }
}
