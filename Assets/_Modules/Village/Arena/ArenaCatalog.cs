// =============================================================================
// ArenaOpponentDef + ArenaCatalog — the 3 SEEDED async-PvP opponents (ARENA MVP).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.Arena
//
// Each opponent = a base-recipe (a List<PlacedStructureData> fort, generated via
// the SAME OutpostFoundationGenerator.GenerateFootprintRecipe the village build
// mode + open-world camps use, at 3 sizes) + a garrison spec (boss is implicit in
// EnemyOutpost; N guards here, rising 1/4/8) + a rising threat tier (1/4/8 — drives
// stat scaling) + a wager tier (ArenaWagerTunables, defaults 50/100/200, in the channel's
// wager currency per CurrencySkinResolver - WO-1366) + a display name + flavour.
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
        /// <summary>
        /// Stake to raid this opponent, in the channel's wager currency (WO-1366: Crystals on
        /// Google Play, SKR on the dApp Store - resolved by CurrencySkinResolver, never here).
        /// Refreshed from <see cref="ArenaWagerTunables"/> on every <see cref="ArenaCatalog.All"/>
        /// read, so a rail knob reaches a running client; today's defaults are 50 / 100 / 200.
        /// </summary>
        public long Wager;
        /// <summary>The opponent's base layout (LOCAL grid cells) — realized as the fort.</summary>
        public List<PlacedStructureData> BaseRecipe;

        /// <summary>The purse paid on a win (default 200% = your stake back + theirs; ArenaWagerTunables.WinPursePct).</summary>
        public long WinPurse => ArenaWagerTunables.PurseFor(Wager);
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
                // WO-1366: the wager tiers are TUNABLES. Re-read them on every access so a
                // rail knob change reaches a running client without a rebuild; the forts and
                // garrisons are still built once. Cheap: three reads.
                ApplyWagerTunables(_opponents);
                return _opponents;
            }
        }

        // The ONE place the authored tier index becomes an amount. Tier 1/2/3 map to
        // ArenaWagerTunables.WagerTier1/2/3 (defaults 50 / 100 / 200 = today's constants).
        private static void ApplyWagerTunables(List<ArenaOpponentDef> opponents)
        {
            for (int i = 0; i < opponents.Count; i++)
            {
                var o = opponents[i];
                if (o != null) o.Wager = ArenaWagerTunables.WagerForTier(o.Tier);
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
                    Wager = ArenaWagerTunables.WagerTier1Default,   // live value applied in All (tunable)
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
                    Wager = ArenaWagerTunables.WagerTier2Default,   // live value applied in All (tunable)
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
                    Wager = ArenaWagerTunables.WagerTier3Default,   // live value applied in All (tunable)
                    BaseRecipe = OutpostFoundationGenerator.GenerateFootprintRecipe(9, 9, OutpostTier.Stone),
                },
            };
        }
    }
}
