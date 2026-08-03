// =============================================================================
// ItemIdentity - the ONE id-keyed resolver for a non-gear item's IDENTITY
// (display name + kind + icon path + glyph + category), all from the SAME row.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.Items
//
// WHY THIS EXISTS (owner F8 seq 641, 2026-08-02 - "shows as poition but says
// iron scrap"):
//   A bag row used to resolve its two halves from DIFFERENT places.
//     NAME  came from the id itself (InventoryVM.BuildConsumables set name = id,
//           which the sidebar then spaced into "Iron Scrap").
//     ICON  came from a keyword guess over that id and, on a miss, from an
//           UNCONDITIONAL generic health-bottle fallback.
//   "IronScrap" is a materials.json row (displayName "Iron Scrap", category
//   "metal") and matches no potion keyword, so the row rendered a health potion
//   under a material's name. The two halves disagreed because they never read
//   the same row.
//
// THE RULE THIS ENFORCES: name and art are BOTH read off the row that the id
// resolves to, in ONE fixed order - consumables.json first, then materials.json.
// A row that resolves no art falls back to ITS OWN authored glyph; it NEVER
// borrows another item's sprite.
//
// Resolution order is deliberate and stable: an id may live in exactly one of the
// two catalogs (ItemIdentityRegression pins that there are no collisions), so the
// order is a tie-break that can never actually fire - it exists so the behaviour
// stays defined if content ever introduces one.
//
// PURE lookup: no UnityEngine UI types, never throws (both catalogs are graceful),
// safe to call from a ViewModel. ASCII-only strings.
// =============================================================================

namespace DeNelle.Village.Items
{
    /// <summary>Which catalog owns an id - i.e. what the row actually IS.</summary>
    public enum ItemIdentityKind
    {
        /// <summary>Neither catalog knows the id (a raw drop key / test fixture).</summary>
        Unknown = 0,
        /// <summary>consumables.json - a potion / food / tent the player can USE.</summary>
        Consumable = 1,
        /// <summary>materials.json - a crafting ingredient. NOT usable, NOT a potion.</summary>
        Material = 2,
    }

    /// <summary>One resolved identity row. Every field comes from the SAME catalog entry.</summary>
    public readonly struct ItemIdentityRow
    {
        public readonly string Id;
        public readonly string DisplayName;   // authored displayName, or the id when unauthored
        public readonly ItemIdentityKind Kind;
        public readonly string IconPath;      // Resources sprite path authored on the row (may be null)
        public readonly string Glyph;         // authored ASCII fallback glyph (may be null)
        public readonly string Category;      // material category (herb/metal/...); null for consumables

        public ItemIdentityRow(string id, string displayName, ItemIdentityKind kind,
                               string iconPath, string glyph, string category)
        {
            Id = id;
            DisplayName = displayName;
            Kind = kind;
            IconPath = iconPath;
            Glyph = glyph;
            Category = category;
        }

        public bool IsKnown => Kind != ItemIdentityKind.Unknown;
    }

    /// <summary>Static, id-keyed identity resolution across the two non-gear catalogs.</summary>
    public static class ItemIdentity
    {
        /// <summary>
        /// Resolve an id to its catalog row. Returns false (with an Unknown row carrying the
        /// raw id as its name) when neither catalog owns it. Never throws.
        /// </summary>
        public static bool TryResolve(string id, out ItemIdentityRow row)
        {
            row = new ItemIdentityRow(id, id ?? "", ItemIdentityKind.Unknown, null, null, null);
            if (string.IsNullOrEmpty(id)) return false;

            var c = ConsumableCatalog.Find(id);
            if (c != null)
            {
                row = new ItemIdentityRow(
                    id,
                    string.IsNullOrEmpty(c.DisplayName) ? id : c.DisplayName,
                    ItemIdentityKind.Consumable,
                    c.IconPath,
                    c.Glyph,
                    null);
                return true;
            }

            var m = MaterialCatalog.Find(id);
            if (m != null)
            {
                row = new ItemIdentityRow(
                    id,
                    string.IsNullOrEmpty(m.DisplayName) ? id : m.DisplayName,
                    ItemIdentityKind.Material,
                    m.IconPath,
                    m.Glyph,
                    m.Category);
                return true;
            }

            return false;
        }

        /// <summary>The row for an id (Unknown-kind row when no catalog owns it). Never null-refs.</summary>
        public static ItemIdentityRow Resolve(string id)
        {
            ItemIdentityRow row;
            TryResolve(id, out row);
            return row;
        }

        /// <summary>Authored display name for an id, or the id itself when unknown.</summary>
        public static string DisplayName(string id) => Resolve(id).DisplayName;

        /// <summary>What the id IS. Unknown when neither catalog owns it.</summary>
        public static ItemIdentityKind KindOf(string id) => Resolve(id).Kind;

        /// <summary>True when materials.json owns the id - i.e. it is a crafting ingredient,
        /// never a potion and never "usable".</summary>
        public static bool IsMaterial(string id) => KindOf(id) == ItemIdentityKind.Material;

        /// <summary>True when consumables.json owns the id - the ONLY case where a generic
        /// potion sprite is an honest fallback.</summary>
        public static bool IsConsumable(string id) => KindOf(id) == ItemIdentityKind.Consumable;

        /// <summary>The row's authored Resources icon path, or null.</summary>
        public static string IconPathOf(string id) => Resolve(id).IconPath;

        /// <summary>The row's authored ASCII glyph, or null.</summary>
        public static string GlyphOf(string id) => Resolve(id).Glyph;
    }
}
