// =============================================================================
// VendorStockContract — the SINGLE SOURCE OF TRUTH for what each store TYPE sells.
// -----------------------------------------------------------------------------
// "Limit each pull to the store type." The owner reported the armorer selling
// weapons + potions (should be armor) and the marketplace selling swords. The old
// fix lived as ad-hoc ctx.Contains("armor")/ctx.Contains("forge") checks inside
// ShopPanel.ShowBuy that fell through to "sell everything" for market/jeweler —
// that scatter is the bug. This contract centralizes the mapping so there is ONE
// place that decides which gear categories a given vendor offers.
//
// ONE CONTRACT, TWO CONSUMERS:
//   1. ShopPanel.ShowBuy  — CONSUMES the contract to FILTER which catalog items it
//      stocks for a vendor (weapons / armor / potions).
//   2. The AutoPilot bot (DevTools / editor) — CONSUMES the same contract to ASSERT
//      that the store's ACTUAL built stock (ShopPanel.CurrentStock) matches what the
//      contract allows, catching a filter regression automatically.
// Because both sides read the same AllowedFor() mapping, the bot is checking the
// intent, not a duplicated copy of it. Keep this file pure data/logic (no
// UnityEngine dependency) so the editor/bot assembly can reference it freely.
// =============================================================================

using System;

namespace DeNelle.Village
{
    /// <summary>
    /// Gear categories a vendor may stock. [Flags] so a vendor can allow any
    /// combination (the general-default fallback allows all three).
    /// </summary>
    [Flags]
    public enum GearKind
    {
        None      = 0,
        Weapon    = 1,
        Armor     = 2,
        Potion    = 4,
        // CRAFTING as shoppable (capability-unify pass): a crafting/forge-craft vendor offers
        // CRAFTABLE RECIPES rather than finished gear. Additive flag — existing bitmask consumers
        // (ShopVM/AutoPilot/PartyShopVM) test Weapon/Armor/Potion explicitly, so they ignore this.
        Craftable = 8,
    }

    /// <summary>
    /// The single source of truth for what each store TYPE sells, keyed off the
    /// vendor context string the game passes (e.g. "forge", "jeweler", "market",
    /// "armorer"). Consumed by BOTH <c>ShopPanel</c> (to FILTER the stock it builds)
    /// AND the AutoPilot bot (to ASSERT the built stock matches). One contract, two
    /// consumers — so the assertion validates intent, not a divergent copy.
    /// </summary>
    public static class VendorStockContract
    {
        /// <summary>
        /// Maps a vendor context to the gear categories it is allowed to stock.
        /// The match is case-insensitive (the input is lowercased here) and uses
        /// substring contains so building ids ("armorer", "blacksmith_forge") and
        /// hand-authored vendor contexts both route correctly.
        ///
        /// Mapping (owner intent):
        ///   • "armor" / "armory" / "armorer"            -> Armor only
        ///   • "forge" / "blacksmith" / "smith"          -> Weapon only
        ///   • "jewel"                                   -> Armor only
        ///       (the crystal/jewelry -> adornment arc; no dedicated "jewelry" item
        ///        type exists yet, so jewelry is modeled as Armor for now. ASSUMPTION:
        ///        revisit when a jewelry GearKind / item type is added.)
        ///   • "market" / "marketplace" / "general" /
        ///     "trader" / "apothec" / "granary" / "farm" -> Potion only
        ///       (general-goods vendors sell consumables, NOT weapons/armor — this is
        ///        the fix for "the marketplace sold swords".)
        ///   • empty / unknown                           -> Weapon|Armor|Potion
        ///       (a SAFE general default so an unrecognized vendor is never broken or
        ///        empty; ShopPanel additionally never-empty-guards against the gear
        ///        catalog being empty.)
        /// </summary>
        public static GearKind AllowedFor(string vendorContext)
        {
            if (string.IsNullOrEmpty(vendorContext))
                return GearKind.Weapon | GearKind.Armor | GearKind.Potion;

            string ctx = vendorContext.ToLowerInvariant();

            // Armor specialists. Note: "armor" must be tested before any generic
            // catch so an "armorer" never falls through to the general default.
            // BLACKSMITH = ARMOR (owner ticket 2026-06-13: the FORGE sells weapons, the
            // BLACKSMITH sells armor). Tested HERE (before the weapon block) so "blacksmith"
            // resolves to Armor and never matches the weapon block's "smith" substring.
            if (ctx.Contains("armor") || ctx.Contains("armory") || ctx.Contains("armorer") ||
                ctx.Contains("blacksmith"))
                return GearKind.Armor;

            // Crafting / forge-craft stations — offer CRAFTABLE RECIPES, not finished gear.
            // Tested BEFORE the weapon block so "forge-craft"/"forgecraft"/"craft-forge" resolve to
            // Craftable and never match the weapon block's "forge" substring. A plain "forge" (no
            // "craft") still falls through to Weapon below — the smithy sells weapons, the crafting
            // station crafts. ("workbench"/"workshop" are the diegetic crafting verbs.)
            if (ctx.Contains("craft") || ctx.Contains("workbench") || ctx.Contains("workshop"))
                return GearKind.Craftable;

            // Weapon specialists — the FORGE (and a plain "smith"). "blacksmith" is already
            // resolved to Armor above, so it never reaches here despite the "smith" substring.
            if (ctx.Contains("forge") || ctx.Contains("smith"))
                return GearKind.Weapon;

            // Jeweler — adornment arc, modeled as Armor for now (see XML-doc above).
            if (ctx.Contains("jewel"))
                return GearKind.Armor;

            // General-goods vendors — consumables only.
            if (ctx.Contains("market") || ctx.Contains("marketplace") || ctx.Contains("general") ||
                ctx.Contains("trader") || ctx.Contains("apothec") || ctx.Contains("granary") ||
                ctx.Contains("farm"))
                return GearKind.Potion;

            // Unknown vendor -> safe general default (never broken / never empty).
            return GearKind.Weapon | GearKind.Armor | GearKind.Potion;
        }
    }
}
