// =============================================================================
// VendorStockResolver — ONE service that resolves a vendor's declared stock QUERY
// (vendors.json via VendorRegistry) against the item catalogs + the CURRENT ROSTER.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.Hero   (WO-598)
//
// THE HONEST SHELF. The owner's F8 sweep (flags 03/05/08/11) showed the Market
// opening an equip shop, the Forge selling Mage wands to a Knight in a Knight-only
// V1, and the Jeweler listing weapons + raw "No wares in stock". Root causes (from
// the code): PartyShopVM forced every non-gear vendor to Weapon|Armor, and the
// gear resolver surfaced roster-UNOBTAINABLE classes as "locked" rows.
//
// This resolver is the single answer to "what does this vendor stock for this
// player": it reads the vendor's QUERY (categories + classFilter + maxReqLevel +
// emptyLine + layout) from VendorRegistry and resolves it against:
//   • weapons/armor  — GearCatalog, ROSTER-FILTERED: an item no currently-playable
//     class can use (Mage wands under ff.knightonly) is EXCLUDED, never listed.
//     Level-gated rows are still returned locked ("Requires Lv N" — aspiration is
//     fine, wrong-class is not; owner rule).
//   • consumables    — ConsumableCatalog (Market's potions/food/tents).
//   • materials/gems — MaterialCatalog (gems = the crystal band, Jeweler stock).
//   • rings/amulets  — GearCatalog.Accessories by slot (the v26 equip slots).
//   • craftables     — CraftableCatalogRegistry (workshop recipes), unchanged.
//
// Views/VMs bind the RESULT; no View ever assembles a shelf list itself. For an
// UNREGISTERED vendor context the resolver derives categories from the legacy
// VendorStockContract heuristic so nothing is ever broken/empty by omission.
//
// PURE data/logic apart from FlowTrace/Guard (§12 instrumented): every catalog
// loop is Guard.TryEach'd; every resolve traces
//   "[Flow:Vendor] <id> resolved N items (query: ...)".
// =============================================================================

using System;
using System.Collections.Generic;
using DeNelle.Core.Catalog;        // ShoppableCraftable / CraftableCatalogRegistry
using DeNelle.Core.Diagnostics;    // FlowTrace / Guard
using DeNelle.Village.Items;       // ConsumableCatalog / MaterialCatalog

namespace DeNelle.Village.Hero
{
    /// <summary>Which shelf PRESENTATION a vendor binds (vendors.json "layout").</summary>
    public enum VendorLayout
    {
        /// <summary>Weapons/armor + party fit + equip actions (Forge, Armorer).</summary>
        Gear,
        /// <summary>Flat consumables/materials list — NO equip tabs / paper-doll (Market).</summary>
        Goods,
        /// <summary>Rings + amulets + gems (Jeweler).</summary>
        Jeweler,
    }

    /// <summary>The catalog band one resolved ware came from (drives the VM's row builder).</summary>
    public enum VendorWareKind
    {
        Weapon,
        Armor,
        Craftable,
        Consumable,
        Material,
        Gem,
        Ring,
        Amulet,
    }

    /// <summary>
    /// One resolved shelf entry. Carries the originating id + band; the VM re-resolves the
    /// rich def (WeaponDef/ArmorDef/AccessoryDef/ConsumableDef/MaterialDef) by id for its row
    /// builders. Eligible=false rows are SHOWN locked with <see cref="LockReason"/> (level
    /// gate); roster-unobtainable items are never returned at all.
    /// </summary>
    public readonly struct VendorWare
    {
        public readonly VendorWareKind Kind;
        public readonly string Id;
        /// <summary>The craftable payload — only meaningful when Kind == Craftable.</summary>
        public readonly ShoppableCraftable Craftable;
        public readonly bool Eligible;
        public readonly string LockReason;

        public VendorWare(VendorWareKind kind, string id, bool eligible = true,
                          string lockReason = null, ShoppableCraftable craftable = default)
        {
            Kind = kind;
            Id = id;
            Eligible = eligible;
            LockReason = lockReason;
            Craftable = craftable;
        }
    }

    /// <summary>
    /// Resolves a vendor's stock query (vendors.json) against the item catalogs + roster.
    /// The ONE entry point Views/VMs bind for shelf content (WO-598).
    /// </summary>
    public static class VendorStockResolver
    {
        // ── Roster (which classes the player can CURRENTLY play) ────────────────
        // V1 canon = Knight-only (ff.knightonly, default ON). Flipping the flag restores
        // the full roster with zero data changes — the shelf follows the roster.

        private static readonly string[] KnightOnlyRoster = { "knight" };
        private static readonly string[] FullRoster = { "knight", "mage", "ranger", "cleric" };

        /// <summary>The classes the current build's player can play (lowercase job keys).</summary>
        public static IReadOnlyList<string> RosterClasses() =>
            DeNelle.Core.FeatureFlags.KnightOnly ? KnightOnlyRoster : FullRoster;

        /// <summary>True when SOME roster class may wield this weapon ("any"/empty job always fits).</summary>
        public static bool WeaponRosterObtainable(WeaponDef w, IReadOnlyList<string> roster)
        {
            if (w == null) return false;
            if (roster == null || roster.Count == 0) return true;
            foreach (var job in roster)
                if (GearCatalog.WeaponFitsClass(w, job)) return true;
            return false;
        }

        /// <summary>True when SOME roster class may wear this armor (weight "any"/empty always fits).</summary>
        public static bool ArmorRosterObtainable(ArmorDef a, IReadOnlyList<string> roster)
        {
            if (a == null) return false;
            if (roster == null || roster.Count == 0) return true;
            foreach (var job in roster)
                if (GearCatalog.ArmorFitsClass(a, job)) return true;
            return false;
        }

        // ── Query surface ────────────────────────────────────────────────────────

        /// <summary>The vendor's declared layout, or Gear when unregistered (legacy behavior).</summary>
        public static VendorLayout LayoutFor(string vendorContext)
        {
            var v = VendorRegistry.Find(vendorContext);
            switch ((v?.Layout ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "goods":   return VendorLayout.Goods;
                case "jeweler": return VendorLayout.Jeweler;
                default:        return VendorLayout.Gear;
            }
        }

        /// <summary>
        /// The vendor's AUTHORED empty-shelf line — never null/empty (falls back to a generic
        /// authored default so no shop can ever render a raw empty grid; WO-598 acceptance).
        /// </summary>
        public static string EmptyLineFor(string vendorContext)
        {
            string authored = VendorRegistry.EmptyLineFor(vendorContext);
            return !string.IsNullOrEmpty(authored)
                ? authored
                : "Nothing in stock right now - come back after the next delivery.";
        }

        /// <summary>The registry displayName (panel header), or null when unregistered.</summary>
        public static string DisplayNameFor(string vendorContext)
        {
            var v = VendorRegistry.Find(vendorContext);
            return v != null && !string.IsNullOrEmpty(v.DisplayName) ? v.DisplayName : null;
        }

        // ── Goods pricing (data-first: consumables/materials carry an authored gold
        //    "price"; the defaults below only catch an unpriced future entry). ──────

        public static int PriceFor(ConsumableDef c)
        {
            if (c == null) return 10;
            if (c.Price > 0) return c.Price;
            switch (c.Kind)
            {
                case ConsumableKind.Tent: return 25;
                case ConsumableKind.Food: return 6;
                default:                  return 12;
            }
        }

        public static int PriceFor(MaterialDef m)
        {
            if (m == null) return 5;
            if (m.Price > 0) return m.Price;
            return IsGem(m) ? 20 : 5;
        }

        /// <summary>The Jeweler's "gem" band: crystal-category materials (plus the crystal-named
        /// stones the jeweler recipes consume, e.g. ing_heartstone_crystal which is category
        /// "stone" in materials.json — data-verified against JewelerRecipeCatalog's gem set).</summary>
        public static bool IsGem(MaterialDef m)
        {
            if (m == null) return false;
            if (string.Equals(m.Category, "crystal", StringComparison.OrdinalIgnoreCase)) return true;
            return !string.IsNullOrEmpty(m.Id) &&
                   m.Id.IndexOf("crystal", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        // ── THE resolve ──────────────────────────────────────────────────────────

        /// <summary>
        /// Resolve the vendor's stock query for the given shopper (<paramref name="job"/> at
        /// <paramref name="level"/>). Roster-unobtainable gear is EXCLUDED; level-gated gear is
        /// returned locked ("Requires Lv N"). Never null, never throws (Guard.TryEach per loop).
        /// <paramref name="rosterOverride"/> lets the regression pin a deterministic roster.
        /// </summary>
        public static IReadOnlyList<VendorWare> Resolve(string vendorContext, string job, int level,
                                                        IReadOnlyList<string> rosterOverride = null)
        {
            var result = new List<VendorWare>();
            var vendor = VendorRegistry.Find(vendorContext);
            var categories = (vendor != null && vendor.Categories != null && vendor.Categories.Count > 0)
                ? (IReadOnlyList<string>)vendor.Categories
                : DerivedCategories(vendorContext);
            var roster = rosterOverride ?? RosterClasses();
            bool rosterFilter = vendor == null ||
                !string.Equals(vendor.ClassFilter, "none", StringComparison.OrdinalIgnoreCase);
            int levelCap = vendor != null ? vendor.MaxReqLevel : 0;   // 0 = uncapped

            foreach (var rawCat in categories)
            {
                string cat = (rawCat ?? string.Empty).Trim().ToLowerInvariant();
                switch (cat)
                {
                    case "weapon":
                    case "weapons":
                        Guard.TryEach("Vendor", "stock weapon", GearCatalog.AllWeapons(), w =>
                        {
                            if (w == null) return;
                            // ROSTER gate (the flag_08 fix): a class NO playable hero has is not
                            // aspiration, it's noise — excluded, never a locked row.
                            if (rosterFilter && !WeaponRosterObtainable(w, roster)) return;
                            if (OverLevelCap(w.req, levelCap)) return;
                            bool classOk = string.IsNullOrEmpty(job) || GearCatalog.WeaponFitsClass(w, job);
                            bool levelOk = MeetsLevel(w.req, level);
                            result.Add(new VendorWare(VendorWareKind.Weapon, w.id,
                                classOk && levelOk, LockReason(classOk, levelOk, Cap(w.job), w.req)));
                        });
                        break;

                    case "armor":
                    case "armors":
                        Guard.TryEach("Vendor", "stock armor", GearCatalog.AllArmors(), a =>
                        {
                            if (a == null) return;
                            if (rosterFilter && !ArmorRosterObtainable(a, roster)) return;
                            if (OverLevelCap(a.req, levelCap)) return;
                            bool classOk = GearCatalog.ArmorFitsClass(a, job);
                            bool levelOk = MeetsLevel(a.req, level);
                            string wt = (a.weight ?? string.Empty).Trim();
                            result.Add(new VendorWare(VendorWareKind.Armor, a.id,
                                classOk && levelOk,
                                LockReason(classOk, levelOk, wt.Length == 0 ? "other heroes" : Cap(wt) + " armor", a.req)));
                        });
                        break;

                    case "consumable":
                    case "consumables":
                        Guard.TryEach("Vendor", "stock consumable", ConsumableCatalog.All, c =>
                        {
                            if (c == null || string.IsNullOrEmpty(c.Id)) return;
                            result.Add(new VendorWare(VendorWareKind.Consumable, c.Id));
                        });
                        break;

                    case "material":
                    case "materials":
                        Guard.TryEach("Vendor", "stock material", MaterialCatalog.All, m =>
                        {
                            if (m == null || string.IsNullOrEmpty(m.Id)) return;
                            if (IsGem(m)) return;   // gems are the Jeweler's band, not the Market's
                            result.Add(new VendorWare(VendorWareKind.Material, m.Id));
                        });
                        break;

                    case "gem":
                    case "gems":
                        Guard.TryEach("Vendor", "stock gem", MaterialCatalog.All, m =>
                        {
                            if (m == null || string.IsNullOrEmpty(m.Id)) return;
                            if (!IsGem(m)) return;
                            result.Add(new VendorWare(VendorWareKind.Gem, m.Id));
                        });
                        break;

                    case "ring":
                    case "rings":
                        AddAccessories(result, VendorWareKind.Ring, level, levelCap, ac => ac.IsRing);
                        break;

                    case "amulet":
                    case "amulets":
                        AddAccessories(result, VendorWareKind.Amulet, level, levelCap, ac => ac.IsAmulet);
                        break;

                    case "accessory":
                    case "accessories":
                        AddAccessories(result, VendorWareKind.Ring, level, levelCap, ac => ac.IsRing);
                        AddAccessories(result, VendorWareKind.Amulet, level, levelCap, ac => ac.IsAmulet);
                        break;

                    case "craftable":
                    case "craftables":
                        Guard.TryEach("Vendor", "stock craftable", CraftableCatalogRegistry.GetCraftables(), c =>
                        {
                            if (!c.Craftable || string.IsNullOrEmpty(c.Id)) return;
                            result.Add(new VendorWare(VendorWareKind.Craftable, c.Id, craftable: c));
                        });
                        break;

                    default:
                        FlowTrace.Warn("Vendor",
                            $"vendors.json category '{rawCat}' on '{vendorContext}' is unknown - skipped.");
                        break;
                }
            }

            // ── §12: trace every resolve; never a silent blank ──────────────────
            string vendorId = vendor != null ? vendor.Id : (vendorContext ?? "<none>");
            string queryStr = $"cats=[{string.Join(",", categories)}] roster=[{string.Join(",", roster)}]" +
                              $" classFilter={(rosterFilter ? "roster" : "none")} maxReqLevel={levelCap}" +
                              $" job='{job}' lvl={level} layout={LayoutFor(vendorContext)}";
            FlowTrace.Step("Vendor", $"{vendorId} resolved {result.Count} items (query: {queryStr})");
            if (result.Count == 0)
                FlowTrace.Warn("Vendor",
                    $"{vendorId} resolved EMPTY - authored emptyLine shown: \"{EmptyLineFor(vendorContext)}\"");

            return result;
        }

        // ── helpers ──────────────────────────────────────────────────────────────

        private static void AddAccessories(List<VendorWare> result, VendorWareKind kind, int level,
                                           int levelCap, Func<AccessoryDef, bool> slotMatch)
        {
            Guard.TryEach("Vendor", "stock accessory", GearCatalog.Accessories, ac =>
            {
                if (ac == null || string.IsNullOrEmpty(ac.id) || !slotMatch(ac)) return;
                if (OverLevelCap(ac.req, levelCap)) return;
                bool levelOk = MeetsLevel(ac.req, level);
                result.Add(new VendorWare(kind, ac.id, levelOk,
                    levelOk ? null : "Requires Lv " + ReqLevel(ac.req)));
            });
        }

        /// <summary>Categories for an UNREGISTERED vendor, derived from the legacy contract
        /// heuristic (VendorStockContract) so unknown contexts keep their old behavior.</summary>
        private static IReadOnlyList<string> DerivedCategories(string vendorContext)
        {
            var kinds = VendorStockContract.AllowedFor(vendorContext ?? string.Empty);
            var cats = new List<string>();
            if ((kinds & GearKind.Weapon) != 0) cats.Add("weapon");
            if ((kinds & GearKind.Armor) != 0) cats.Add("armor");
            if ((kinds & GearKind.Potion) != 0) cats.Add("consumable");
            if ((kinds & GearKind.Material) != 0) cats.Add("material");
            if ((kinds & GearKind.Accessory) != 0) cats.Add("accessory");
            if ((kinds & GearKind.Craftable) != 0) cats.Add("craftable");
            return cats;
        }

        private static bool MeetsLevel(GearReq req, int level) => req == null || level >= req.level;
        private static int ReqLevel(GearReq req) => req != null ? req.level : 1;
        private static bool OverLevelCap(GearReq req, int cap) => cap > 0 && req != null && req.level > cap;

        // Class lock beats level lock (a hard "not for this hero" never masquerades as
        // "come back later") — mirrors ShopCatalog's lock-reason precedence.
        private static string LockReason(bool classOk, bool levelOk, string classLabel, GearReq req)
        {
            if (classOk && levelOk) return null;
            if (!classOk) return "Class: " + classLabel;
            return "Requires Lv " + ReqLevel(req);
        }

        private static string Cap(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return char.ToUpperInvariant(s[0]) + (s.Length > 1 ? s.Substring(1) : string.Empty);
        }
    }
}
