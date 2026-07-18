// =============================================================================
// GearIconCatalog — the PRESENTATION seam that resolves a gear item's art sprite
// (and glyph fallback) BY ROLE + ID, doing the gameplay-catalog (GearCatalog) +
// art-sheet (ItemIconCatalog) work INTERNALLY so a View never names GearCatalog.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// WHY (strict-MVVM, UI_MVVM_MIGRATION_PLAN §1 Phase 1 — the "icon leak"): the shop /
// inventory / equipment / party-shop Views each re-pulled `GearCatalog.Find*` to feed
// `ItemIconCatalog.For*` when painting a row/slot icon. That is a gameplay-catalog read
// inside a dumb-skin View (the [ui-mvvm] oracle bans GearCatalog in a View). The VMs
// already carry the ROLE + ID on each ItemVM; this catalog absorbs the Find+For pair so
// the View resolves art from those KEYS through ONE presentation entry point.
//
// PRESERVES BEHAVIOR EXACTLY: Resolve(role,id) returns the SAME sprite the old
// `ItemIconCatalog.For*(GearCatalog.Find*(id))` pair returned (ForWeapon/ForArmor already
// null-guard a null def, so a missing id -> null -> the caller's existing glyph fallback).
// This is a reskin of the seam, not a logic change.
// =============================================================================

using UnityEngine;
using DeNelle.Village.Hero;   // InventoryVM.IconRole* constants (the shared role keys)

namespace DeNelle.Village
{
    /// <summary>Resolves gear item art (sprite-first) + type glyph from a role + id — the
    /// presentation seam that keeps GearCatalog out of the Views (UI_MVVM_MIGRATION_PLAN §1).</summary>
    public static class GearIconCatalog
    {
        /// <summary>
        /// The art sprite for an item, keyed by its icon ROLE (weapon / armor / potion) + id,
        /// or null (the caller uses its own glyph / pack-icon fallback). Does the
        /// GearCatalog.Find* + ItemIconCatalog.For* pair internally so the View never names
        /// GearCatalog. <paramref name="displayName"/> is the consumable's display name (potion
        /// role only; ignored for gear).
        /// </summary>
        public static Sprite Resolve(string iconRole, string idOrName, string displayName = null)
        {
            switch (iconRole)
            {
                case InventoryVM.IconRoleWeapon: return ItemIconCatalog.ForWeapon(GearCatalog.FindWeapon(idOrName));
                case InventoryVM.IconRoleArmor:  return ItemIconCatalog.ForArmor(GearCatalog.FindArmor(idOrName));
                case InventoryVM.IconRolePotion: return ItemIconCatalog.ForConsumable(idOrName, displayName);
                default:                         return null;
            }
        }

        /// <summary>
        /// The at-a-glance TYPE glyph for a gear item, keyed by role + id — the glyph-fallback
        /// passthrough (mirrors the old InventoryGrid.GlyphForRole weapon/armor branches) so a
        /// View can drop GearCatalog there too. Weapon/armor resolve the def + derive the glyph;
        /// any other role returns "?" (the caller owns the potion/consumable glyph).
        /// </summary>
        public static string Glyph(string iconRole, string idOrName)
        {
            switch (iconRole)
            {
                case InventoryVM.IconRoleWeapon: return WeaponTypeGlyph(GearCatalog.FindWeapon(idOrName));
                case InventoryVM.IconRoleArmor:  return ArmorTypeGlyph(GearCatalog.FindArmor(idOrName));
                default:                         return "?";
            }
        }

        // ── Type-glyph tables (moved verbatim from HeroInventoryController so the View drops
        // GearCatalog): id+name keyword match, then class fallback. All BMP glyphs (WebGL/TMP-safe). ──

        private static string WeaponTypeGlyph(WeaponDef w)
        {
            if (w == null) return "?";
            string k = ((w.id ?? "") + " " + (w.name ?? "")).ToLowerInvariant();
            // Most specific first.
            if (Has(k, "dagger", "knife", "dirk", "stiletto"))          return "D"; // dagger
            if (Has(k, "bow", "recurve", "longbow", "shortbow"))        return "B"; // bow / ranged shot
            if (Has(k, "wand"))                                         return "W"; // wand
            if (Has(k, "staff", "scepter", "sceptre", "stave", "rod"))  return "S"; // arcane staff
            if (Has(k, "censer", "censor", "thurible"))                 return "C"; // cleric censer
            if (Has(k, "axe", "hatchet"))                               return "A"; // axe
            if (Has(k, "hammer", "maul", "mace"))                       return "H"; // hammer/mace
            if (Has(k, "greatsword", "claymore", "sword", "blade",
                       "longsword", "saber", "sabre", "edge", "brand",
                       "breaker", "keeper")) return "/";                            // sword
            // Fallback by class.
            switch ((w.job ?? "").ToLowerInvariant())
            {
                case "mage":   return "S"; // staff
                case "ranger": return "B"; // bow
                case "cleric": return "C"; // censer
                case "knight": return "/"; // sword
                default:        return "/";
            }
        }

        private static string ArmorTypeGlyph(ArmorDef a)
        {
            if (a == null) return "?";
            string k = ((a.id ?? "") + " " + (a.name ?? "")).ToLowerInvariant();
            if (Has(k, "shield", "aegis", "buckler", "ward"))           return "O"; // shield boss
            if (Has(k, "plate", "platemail"))                           return "#"; // plate
            if (Has(k, "chain", "mail", "chainmail"))                   return "x"; // mail
            if (Has(k, "leather", "hide"))                              return "x"; // leather
            if (Has(k, "cloth", "robe", "cloak", "garb", "wanderer"))   return "~"; // cloth/robe
            if (Has(k, "helm", "helmet", "hood", "crown", "cap"))       return "^"; // helm
            return "x";                                                            // generic armor
        }

        private static bool Has(string haystack, params string[] needles)
        {
            for (int i = 0; i < needles.Length; i++)
                if (haystack.IndexOf(needles[i], System.StringComparison.Ordinal) >= 0) return true;
            return false;
        }
    }
}
