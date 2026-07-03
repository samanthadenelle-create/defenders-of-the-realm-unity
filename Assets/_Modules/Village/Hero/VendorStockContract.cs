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
//
// WO-598 (vendor wares content mapping): AllowedFor now consults the vendors.json
// REGISTRY first (VendorRegistry — categories → kinds), so the mapping is CONTENT
// for the registered vendors (forge/armorer/market/jeweler) and the heuristic below
// is the fallback for unregistered contexts. VendorRegistry loads via CanonicalJson
// (graceful: registry absent ⇒ heuristic unchanged). One truth, all consumers.
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
        // WO-543: ACCESSORIES (rings + amulets) — sold exclusively at the Jeweler (Sable Vey).
        Accessory = 16,
        // WO-598: CRAFTING MATERIALS as shoppable (the Market's second band next to
        // consumables; the crystal/gem sub-band is Jeweler stock). Additive flag —
        // existing bitmask consumers test their own flags explicitly and ignore this.
        Material = 32,
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
        ///   • "jewel"                                   -> Accessory only
        ///       (WO-543: the Jeweler sells rings + amulets — the GearKind.Accessory band,
        ///        sourced from accessories.json / GearCatalog.Accessories.)
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

            // ── WO-598: the vendors.json REGISTRY is consulted FIRST (one truth). A
            //    registered vendor's kinds derive from its declared stock-query categories,
            //    so the legacy shop, ShopCatalog, the MVVM PartyShop AND the AutoPilot
            //    oracle all read the same mapping. Unregistered contexts (and a missing/
            //    broken vendors.json) fall through to the heuristic below unchanged. ──
            var registered = RegistryKinds(vendorContext);
            if (registered != GearKind.None) return registered;

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

            // Jeweler — sells ACCESSORIES (rings + amulets), Sable Vey's specialty (WO-543).
            if (ctx.Contains("jewel"))
                return GearKind.Accessory;

            // General-goods vendors — consumables only.
            if (ctx.Contains("market") || ctx.Contains("marketplace") || ctx.Contains("general") ||
                ctx.Contains("trader") || ctx.Contains("apothec") || ctx.Contains("granary") ||
                ctx.Contains("farm"))
                return GearKind.Potion;

            // Unknown vendor -> safe general default (never broken / never empty).
            return GearKind.Weapon | GearKind.Armor | GearKind.Potion;
        }

        /// <summary>
        /// WO-598: map a registered vendor's declared stock-query categories (vendors.json)
        /// to GearKind flags. GearKind.None when the vendor is unregistered / the registry is
        /// absent — the caller then uses the legacy heuristic. Never throws (registry loads
        /// gracefully; a null Categories yields None).
        /// </summary>
        private static GearKind RegistryKinds(string vendorContext)
        {
            var v = VendorRegistry.Find(vendorContext);
            if (v == null || v.Categories == null) return GearKind.None;

            GearKind kinds = GearKind.None;
            foreach (var raw in v.Categories)
            {
                switch ((raw ?? string.Empty).Trim().ToLowerInvariant())
                {
                    case "weapon": case "weapons":           kinds |= GearKind.Weapon;    break;
                    case "armor": case "armors":             kinds |= GearKind.Armor;     break;
                    case "consumable": case "consumables":   kinds |= GearKind.Potion;    break;
                    case "material": case "materials":
                    case "gem": case "gems":                 kinds |= GearKind.Material;  break;
                    case "ring": case "rings":
                    case "amulet": case "amulets":
                    case "accessory": case "accessories":    kinds |= GearKind.Accessory; break;
                    case "craftable": case "craftables":     kinds |= GearKind.Craftable; break;
                }
            }
            return kinds;
        }
    }
}
