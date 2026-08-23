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
// for the registered vendors (forge/armorer/market/jeweler) and the fallback below
// covers unregistered contexts. VendorRegistry loads via CanonicalJson
// (graceful: registry absent ⇒ fallback unchanged). One truth, all consumers.
//
// WO-1161 follow-up (2026-08-23) — ⛔ THE FALLBACK NO LONGER MATCHES DISPLAY WORDS.
// This file's own header said it existed to retire the ad-hoc `ctx.Contains("armor")`
// checks; what it actually did was CENTRALISE them — eleven substrings ("armor",
// "armory", "armorer", "blacksmith", "craft", "workbench", "workshop", "forge",
// "smith", "market", "farm") deciding what a shop sells by looking for a word inside
// a string. That is exactly how `forge` (which sells WEAPONS) and `armorer` (which
// sells ARMOUR) collapsed onto one another for weeks: "armorer" CONTAINS "armor", and
// the displayed word had drifted away from the function anyway, so the substring was
// matching a label that was already lying.
//
// The fallback now resolves the vendor's ROLE — `StructureRoles`, i.e. the `role`
// field the catalog row itself declares — and branches on that. A role is an identity,
// not a spelling: renaming the Forge to "Weaponsmith" tomorrow cannot re-route its
// stock, and a brand-new building routes the moment its catalog row declares a role.
// vendors.json REMAINS the authority on what a vendor STOCKS; the role only answers
// "which vendor is this?" when the registry has no row to answer with.
// =============================================================================

using System;
using DeNelle.Core.Catalog;   // StructureRole / StructureRoles — the role table

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
        ///
        /// THREE STEPS, in this order, and each one is data:
        ///   1. <b>vendors.json</b> (<see cref="VendorRegistry"/>) — the AUTHORITY on what a
        ///      vendor stocks. Its declared `categories` become the GearKind flags.
        ///   2. <b>the catalog ROLE</b> (<see cref="StructureRoles"/>) — for a vendor with no
        ///      registry row, the role its catalog row declares says which trade it is:
        ///      weaponsmith -> Weapon, armorer -> Armor, jeweler -> Accessory,
        ///      marketplace -> Potion, crafting_station -> Craftable.
        ///   3. a narrow residual list of diegetic general-goods stalls that own no catalog
        ///      row at all ("trader" / "granary" / "apothecary" / "general" / "farm") ->
        ///      Potion, so a stall NPC can never end up selling swords. The moment one of
        ///      those earns a catalog row with a role, step 2 answers first and it drops out.
        ///   • empty / unknown -> Weapon|Armor|Potion, a SAFE general default so an
        ///     unrecognized vendor is never broken or empty (ShopPanel additionally
        ///     never-empty-guards against the gear catalog being empty).
        ///
        /// ⛔ NO STEP MATCHES A DISPLAY WORD. See the file header: "armorer" contains
        /// "armor", so the old substring ladder could not tell the weapon shop from the
        /// armour shop, and it was reading labels that had themselves drifted.
        /// </summary>
        public static GearKind AllowedFor(string vendorContext)
        {
            if (string.IsNullOrEmpty(vendorContext))
                return GearKind.Weapon | GearKind.Armor | GearKind.Potion;

            // ── 1. WO-598: the vendors.json REGISTRY is consulted FIRST (one truth). A
            //    registered vendor's kinds derive from its declared stock-query categories,
            //    so the legacy shop, ShopCatalog, the MVVM PartyShop AND the AutoPilot
            //    oracle all read the same mapping. Unregistered contexts (and a missing/
            //    broken vendors.json) fall through to the role below. ──
            var registered = RegistryKinds(vendorContext);
            if (registered != GearKind.None) return registered;

            // ── 2. The catalog says WHAT THIS BUILDING IS. Identity, never spelling. ──
            switch (RoleFor(vendorContext))
            {
                case StructureRole.Weaponsmith:     return GearKind.Weapon;
                case StructureRole.Armorer:         return GearKind.Armor;
                // WO-543: the Jeweler sells rings + amulets — the GearKind.Accessory band,
                // sourced from accessories.json / GearCatalog.Accessories.
                case StructureRole.Jeweler:         return GearKind.Accessory;
                case StructureRole.Marketplace:     return GearKind.Potion;
                // A crafting station offers CRAFTABLE RECIPES, not finished gear.
                case StructureRole.CraftingStation: return GearKind.Craftable;
            }

            // ── 3. General-goods stalls with no catalog row of their own — consumables
            //    only. This is the surviving fix for "the marketplace sold swords"; the
            //    marketplace itself now arrives via step 1 or 2. ──
            if (IsGeneralGoodsStall(vendorContext))
                return GearKind.Potion;

            // Unknown vendor -> safe general default (never broken / never empty).
            return GearKind.Weapon | GearKind.Armor | GearKind.Potion;
        }

        /// <summary>
        /// The catalog ROLE this vendor context is, or <see cref="StructureRole.None"/>.
        /// The ONE place a vendor context becomes an identity — every consumer (stock,
        /// shop title, shelf layout, vendor gold pool) routes through this instead of
        /// re-inventing its own word test.
        /// <para>Resolution order: the context AS a catalog id (the normal case — the
        /// vendor context IS the structure id the dialogue/NPC path passes), then, for a
        /// composite/hand-authored context, whatever real catalog id vendors.json maps it
        /// to. <see cref="VendorRegistry.Find"/> already owns that exact-then-substring
        /// routing, so the fuzzy matching stays in ONE place and stays keyed on IDS —
        /// which are frozen save keys — never on player-facing words, which move.</para>
        /// <para>Returns None (never throws) when the catalog has not loaded, which every
        /// caller already treats as "no opinion" and falls back from.</para>
        /// </summary>
        public static string RoleFor(string vendorContext)
        {
            if (string.IsNullOrEmpty(vendorContext)) return StructureRole.None;
            string ctx = vendorContext.Trim().ToLowerInvariant();

            string role = Normalize(StructureRoles.RoleOf(ctx));
            if (role != StructureRole.None) return role;

            var v = VendorRegistry.Find(ctx);
            if (v != null && !string.IsNullOrEmpty(v.Id))
            {
                role = Normalize(StructureRoles.RoleOf(v.Id));
                if (role != StructureRole.None) return role;
            }

            return StructureRole.None;
        }

        /// <summary>Roles are compared ordinally against the lowercase StructureRole constants.</summary>
        private static string Normalize(string role) =>
            string.IsNullOrEmpty(role) ? StructureRole.None : role.Trim().ToLowerInvariant();

        // The residual, and deliberately SHORT, list of diegetic stall contexts that name a
        // kind of trade but own no catalog row (so no role) and no vendors.json row (so no
        // categories). None of them can collide with a vendor's display word — that is the
        // property the old ladder lacked. Adding to this list is the wrong move: give the
        // stall a catalog row with a role instead, and step 2 picks it up for free.
        private static readonly string[] GeneralGoodsStalls =
        {
            "general", "trader", "apothec", "granary", "farm",
        };

        private static bool IsGeneralGoodsStall(string vendorContext)
        {
            string ctx = vendorContext.Trim().ToLowerInvariant();
            foreach (var s in GeneralGoodsStalls)
                if (ctx.Contains(s)) return true;
            return false;
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
