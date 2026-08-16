// =============================================================================
// DungeonExclusiveItems — the ONE authority on which item ids may ONLY be earned
// underground (WO-1041 §4 / §5; WO-1042).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.Catalog
//
// ⛔ WHY THIS EXISTS
//
// WO-1041's entire thesis is "something we can do to justify a dungeon": the dungeon
// is worth descending into because it pays what you CANNOT GET ANYWHERE ELSE. The
// moment a gem can be bought with gold, bought with crystals, or handed out in a
// resource pack, the dungeon stops justifying itself and the pillar is void. That
// invariant is not self-enforcing — it erodes silently, one convenient data edit at
// a time, and nobody notices until the pillar is already pointless.
//
// So the exclusive set is NAMED HERE, in one place, and:
//   • VendorStockResolver consults it, so no vendor shelf can stock these ids
//     (see the "material" and "gem" bands there).
//   • DungeonGemExclusivityRegression (marker DUNGEON_GEM_EXCLUSIVITY_OK) fails the
//     gate if any of these ids appears in ANY purchasable/grantable catalog —
//     packs.json, vendors.json shelves, stake-rewards, quest rewards, daily quests.
//
// ⚠ A PRE-EXISTING VIOLATION THIS FIXES (found 2026-08-16, WO-1041):
// vendors.json's `jeweler` vendor carries the "gem" category, and VendorStockResolver's
// gem band stocked EVERY crystal-category material — so the Jeweler was SELLING
// ing_ember_crystal / ing_aether_shard (20 gold) and ing_heartstone_crystal (18 gold)
// outright. The dungeon-exclusivity thesis was already void in the shipped tree before
// any dungeon drop existed. The vendor bands now filter through this set.
//
// ⚠ NOTE ON WHAT THIS SET DOES *NOT* CLAIM. These ids also drop from overworld/arena
// BOSS loot tables (loot-tables.json: necromancer, orc-warlord, orc-shaman, orc-berserker,
// orc-warrior — WO-556). Those are EARNED COMBAT DROPS, not purchases, and retiring them
// is a live design decision that belongs to the owner, not to this file. This set governs
// the PURCHASE/GRANT surface (shops, packs, gifts, quest payouts) — the surface where
// exclusivity actually dies. See the WO-1041 RESULT notes.
// =============================================================================

using System;
using System.Collections.Generic;

namespace DeNelle.Core.Catalog
{
    /// <summary>
    /// Item ids that may only ever enter the player's inventory by descending — never sold by a
    /// vendor, never bundled in a purchasable pack, never granted as a quest/stake payout.
    /// </summary>
    public static class DungeonExclusiveItems
    {
        /// <summary>The rough, unidentified stone a dungeon run pays out (WO-1042). Polished at the Jeweler.</summary>
        public const string RoughStoneId = "ing_rough_stone";

        /// <summary>The three refined gems jeweler-recipes.json consumes (verified at source 2026-08-16).</summary>
        public const string EmberCrystalId = "ing_ember_crystal";
        public const string AetherShardId = "ing_aether_shard";
        public const string HeartstoneCrystalId = "ing_heartstone_crystal";

        private static readonly HashSet<string> Ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            RoughStoneId,
            EmberCrystalId,
            AetherShardId,
            HeartstoneCrystalId,
        };

        /// <summary>Every dungeon-exclusive id, for oracles and shelf filters. Never null.</summary>
        public static IReadOnlyCollection<string> All => Ids;

        /// <summary>The three REFINED gems only (the rough stone excluded) — the jeweler-recipe gem set.</summary>
        public static IReadOnlyList<string> RefinedGems { get; } =
            new[] { EmberCrystalId, AetherShardId, HeartstoneCrystalId };

        /// <summary>True when <paramref name="id"/> may only be earned underground. Null-safe.</summary>
        public static bool Contains(string id) => !string.IsNullOrEmpty(id) && Ids.Contains(id);
    }
}
