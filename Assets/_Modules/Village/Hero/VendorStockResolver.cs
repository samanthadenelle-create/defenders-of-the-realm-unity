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
//     Level-gated rows are returned locked ("Requires Lv N" — aspiration is fine,
//     wrong-class is not; owner rule) UNLESS the vendor opts into WO-860's
//     "onlyEquippable", which hides them as well.
//
// WO-860 — THE THINNED SHELF (owner felt-test 2026-08-02: "look at all the options
// in store for weapons and thin it out so there are only 2 options on each new
// level, isolate to only those, only show ones they can equip"). Four DATA knobs on
// the vendors.json row drive it, all inert at their defaults:
//   onlyEquippable    — hide, don't lock, what the shopper can't equip now.
//   perLevelCap       — at most N rows per REQUIRED LEVEL (see EmitCapped's sort).
//   excludeIdPrefixes — drop placeholder bands ("blink_") without touching catalogs.
//   footerLine        — the "come back after you level" cue under a capped list.
//
// WO-960 — THE LOCKED PREVIEW WINDOW (owner 2026-08-10: "display as greyed out with
// lvl and only show ones in the next 5 levels"). A fifth DATA knob, lockedPreviewLevels
// (armorer=5): a class-appropriate row locked ONLY by level ships LOCKED ("Requires
// Lv N") when req.level is in (shopperLevel, shopperLevel+N] — under onlyEquippable
// this re-admits the near-future ladder slice; beyond the window the row hides on
// every shelf mode. 0/absent = pre-960 exactly.
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
        // WO-861 Phase 0: the roster is no longer this file's own copy of the rule
        // ("KnightOnly ? {knight} : {knight,mage,ranger,cleric}"). It is delegated to the
        // ONE roster truth, DeNelle.Core.State.PlayableHeroes, which the hero-select screen
        // and GameStateService.ChooseHero also read — so the shelf can never stock gear for
        // a hero the select screen says does not exist (or hide gear for one it offers).
        //
        // LIVE SET since the 2026-08-05 unlock: ff.knightonly defaults OFF, so the shelf
        // roster is { knight, ranger, mage } (it was { "knight" } while the flag was ON, and
        // setting ff.knightonly=1 narrows it back). That is one row
        // NARROWER than this file's old FullRoster, which also listed "cleric" — deliberate:
        // the Cleric has no authored kit and is not playable, so cleric-ONLY weapons are
        // noise on the shelf (armor is unaffected — it gates by WEIGHT, and the Cleric
        // shares the Knight's "heavy"). See PlayableHeroes' header note.

        /// <summary>The classes the current build's player can play (lowercase job keys).</summary>
        public static IReadOnlyList<string> RosterClasses() =>
            DeNelle.Core.State.PlayableHeroes.JobKeys();

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

        /// <summary>
        /// The vendor's declared layout. vendors.json's `layout` field is its own small
        /// PRESENTATION vocabulary (gear | goods | jeweler) and stays the authority.
        /// <para>When a vendor is unregistered — or authored no layout — the catalog ROLE
        /// decides which shelf it gets, rather than the shop silently rendering a gear
        /// paper-doll for a jeweller. Gear remains the last-resort default (legacy).</para>
        /// </summary>
        public static VendorLayout LayoutFor(string vendorContext)
        {
            var v = VendorRegistry.Find(vendorContext);
            switch ((v?.Layout ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "goods":   return VendorLayout.Goods;
                case "jeweler": return VendorLayout.Jeweler;
                case "gear":    return VendorLayout.Gear;
            }

            // Unregistered / unauthored: ask what the building IS, never what it is called.
            switch (VendorStockContract.RoleFor(vendorContext))
            {
                case StructureRole.Jeweler:     return VendorLayout.Jeweler;
                case StructureRole.Marketplace: return VendorLayout.Goods;
            }

            return VendorLayout.Gear;
        }

        /// <summary>
        /// THE shop header, for every shop screen. ONE implementation — <c>ShopVM</c> and
        /// <c>PartyShopVM</c> both call this.
        ///
        /// <para>⛔ Both VMs used to invent titles from substrings of the vendor context
        /// ("Armorer's Shop", "The Forge", "Market Stalls", "Jeweler's Bench", "Lumbermill
        /// Stores") — the SAME wrong fact written twice, and one copy ("Lumbermill Stores")
        /// had already drifted from the catalog's "Lumber Mill". Two copies of one fact is
        /// the drift this whole pass exists to end.</para>
        ///
        /// Resolution order:
        ///   1. an explicit caller-supplied display name (the NPC/dialogue path passes one),
        ///   2. the catalog row that claims this vendor's ROLE — the single naming authority
        ///      (`StructureRoles.By[role].DisplayName`),
        ///   3. the vendors.json `displayName` (an authored shop header for a vendor the
        ///      catalog cannot answer for),
        ///   4. a titleized fallback so an unknown vendor still reads as a shop.
        /// </summary>
        public static string TitleFor(string vendorContext, string displayNameOverride = null)
        {
            if (!string.IsNullOrEmpty(displayNameOverride)) return displayNameOverride;

            string word = StructureRoles.By[VendorStockContract.RoleFor(vendorContext)].DisplayName;
            if (!string.IsNullOrEmpty(word)) return word;

            string authored = DisplayNameFor(vendorContext);
            if (!string.IsNullOrEmpty(authored)) return authored;

            if (string.IsNullOrEmpty(vendorContext)) return "Vendor Wares";
            return TitleizeVendor(vendorContext) + " Wares";
        }

        /// <summary>"blacksmith_forge" -> "Blacksmith forge". Last-resort shop-header cosmetics.</summary>
        private static string TitleizeVendor(string id)
        {
            if (string.IsNullOrEmpty(id)) return "Vendor";
            id = id.Replace('-', ' ').Replace('_', ' ').Trim();
            if (id.Length == 0) return "Vendor";
            return char.ToUpper(id[0]) + (id.Length > 1 ? id.Substring(1) : "");
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

        /// <summary>
        /// WO-860 Part B4 — the vendor's authored "come back after levelling for new stock"
        /// line, shown UNDER a NON-EMPTY (capped) list. Null when unauthored/unregistered,
        /// which means "render no footer" — this is deliberately NOT defaulted, because a
        /// vendor whose shelf is not capped has nothing to promise.
        ///
        /// WIRED 2026-08-14 (was dead copy — WEAPONS_DEEP_DIVE §3(e)): PartyShopVM.FooterLine
        /// reads this during BuildBuyGear and PartyShopPanelMvvm.RebuildList renders it as a
        /// footer row UNDER the last item. It is gated on the <c>thinnedByCap</c> out-flag of
        /// <see cref="Resolve(string,string,int,IReadOnlyList{string},out bool)"/>, so a FULL
        /// shelf shows nothing and an EMPTY shelf still shows <see cref="EmptyLineFor"/> only.
        /// </summary>
        public static string FooterLineFor(string vendorContext) =>
            VendorRegistry.FooterLineFor(vendorContext);

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
        /// returned locked ("Requires Lv N") UNLESS the vendor sets <c>onlyEquippable</c>, in
        /// which case it is hidden too (WO-860 B1). The gear bands are additionally filtered by
        /// <c>excludeIdPrefixes</c> and thinned to <c>perLevelCap</c> rows per required level
        /// (see <see cref="EmitCapped"/> for the documented sort). Never null, never throws
        /// (Guard.TryEach per loop).
        /// <paramref name="rosterOverride"/> lets the regression pin a deterministic roster.
        /// </summary>
        public static IReadOnlyList<VendorWare> Resolve(string vendorContext, string job, int level,
                                                        IReadOnlyList<string> rosterOverride = null)
            => Resolve(vendorContext, job, level, rosterOverride, out _);

        /// <summary>
        /// WO-860 Part B4 (render wire) — the same resolve, additionally reporting whether
        /// <c>perLevelCap</c> actually DROPPED rows from this shelf. That bit is the ONLY
        /// licence to render <see cref="FooterLineFor"/>: a shelf the cap did not thin has
        /// nothing to promise, and a shelf that came back EMPTY is the emptyLine's case.
        /// Computed inside <see cref="EmitCapped"/> (which is the only place that knows the
        /// pre-cap candidate count) — never re-derived by the View, which cannot see it.
        /// </summary>
        public static IReadOnlyList<VendorWare> Resolve(string vendorContext, string job, int level,
                                                        IReadOnlyList<string> rosterOverride,
                                                        out bool thinnedByCap)
        {
            int cappedDrops = 0;
            var result = new List<VendorWare>();
            var vendor = VendorRegistry.Find(vendorContext);
            var categories = (vendor != null && vendor.Categories != null && vendor.Categories.Count > 0)
                ? (IReadOnlyList<string>)vendor.Categories
                : DerivedCategories(vendorContext);
            var roster = rosterOverride ?? RosterClasses();
            bool rosterFilter = vendor == null ||
                !string.Equals(vendor.ClassFilter, "none", StringComparison.OrdinalIgnoreCase);
            int levelCap = vendor != null ? vendor.MaxReqLevel : 0;   // 0 = uncapped

            // ── WO-860 Part B: the THINNED shelf knobs, all read from vendors.json ──
            // Every one is inert at its default, so an unregistered vendor (vendor == null)
            // and any row that has not opted in keep the exact pre-860 shelf.
            bool onlyEquippable = vendor != null && vendor.OnlyEquippable;
            int perLevelCap = vendor != null ? vendor.PerLevelCap : 0;                 // 0 = uncapped
            var excludePrefixes = vendor != null ? vendor.ExcludeIdPrefixes : null;    // e.g. ["blink_"]
            // WO-960: the locked PREVIEW window ("greyed out with lvl, only the next 5 levels").
            // 0 = pre-960 behaviour on every shelf mode.
            int lockedPreviewLevels = vendor != null ? vendor.LockedPreviewLevels : 0;
            bool weaponCertificationActive = HasWeaponCertification();
            bool armorCertificationActive = HasArmorCertification();

            foreach (var rawCat in categories)
            {
                string cat = (rawCat ?? string.Empty).Trim().ToLowerInvariant();
                switch (cat)
                {
                    case "weapon":
                    case "weapons":
                    {
                        // Collect first, THEN cap: the per-level cap has to see the whole
                        // eligible set for a level before it can keep the top N of it.
                        var picked = new List<ShelfPick>();
                        Guard.TryEach("Vendor", "stock weapon", GearCatalog.AllWeapons(), w =>
                        {
                            if (w == null) return;
                            if (weaponCertificationActive && !w.IsVisuallyReady) return;
                            // ROSTER gate (the flag_08 fix): a class NO playable hero has is not
                            // aspiration, it's noise — excluded, never a locked row.
                            if (rosterFilter && !WeaponRosterObtainable(w, roster)) return;
                            if (OverLevelCap(w.req, levelCap)) return;
                            // WO-860 B2: authored-content filter (drops the blink_* placeholders).
                            if (IsExcludedId(w.id, excludePrefixes)) return;
                            bool classOk = string.IsNullOrEmpty(job) || GearCatalog.WeaponFitsClass(w, job);
                            bool levelOk = MeetsLevel(w.req, level);
                            // WO-960: a class-appropriate row locked ONLY by level is previewable
                            // (LOCKED, not hidden) when req sits within (level, level+window].
                            bool previewLocked = classOk && !levelOk &&
                                InPreviewWindow(w.req, level, lockedPreviewLevels);
                            // Beyond the window the row hides on EVERY shelf mode (ladder without
                            // a wall of lockeds). window==0 = pre-960: this branch never fires.
                            if (lockedPreviewLevels > 0 && classOk && !levelOk && !previewLocked) return;
                            // WO-860 B1: HIDE what the shopper cannot equip, instead of a locked row.
                            // Reuses the EXISTING equip gate (WeaponFitsClass/MeetsLevel) as the
                            // show-filter — the gate is correct; only its consequence changes.
                            // WO-960 carves out the preview window: those rows come back LOCKED.
                            if (onlyEquippable && !(classOk && levelOk) && !previewLocked) return;
                            picked.Add(new ShelfPick(w.id, ReqLevel(w.req), w.damageMult,
                                classOk && levelOk, LockReason(classOk, levelOk, Cap(w.job), w.req),
                                WeaponKind(w)));
                        });
                        // WEAPON_CATALOG.md 5.2 (2026-08-16): the shopper's OWN weapon kind gets one reserved slot per level
                        // bucket (see ReserveKindSlot). Empty job (no shopper class) => inert.
                        cappedDrops += EmitCapped(result, VendorWareKind.Weapon, picked, perLevelCap, "weapon",
                                                  PrimaryWeaponKind(job));
                        break;
                    }

                    case "armor":
                    case "armors":
                    {
                        var picked = new List<ShelfPick>();
                        Guard.TryEach("Vendor", "stock armor", GearCatalog.AllArmors(), a =>
                        {
                            if (a == null) return;
                            if (armorCertificationActive && !a.IsVisuallyReady) return;
                            if (rosterFilter && !ArmorRosterObtainable(a, roster)) return;
                            if (OverLevelCap(a.req, levelCap)) return;
                            if (IsExcludedId(a.id, excludePrefixes)) return;
                            bool classOk = GearCatalog.ArmorFitsClass(a, job);
                            bool levelOk = MeetsLevel(a.req, level);
                            // WO-960 (see the weapon band): the locked preview window re-admits
                            // class-appropriate level-locked rows within (level, level+window],
                            // and hides level-locked rows beyond it on every shelf mode.
                            bool previewLocked = classOk && !levelOk &&
                                InPreviewWindow(a.req, level, lockedPreviewLevels);
                            if (lockedPreviewLevels > 0 && classOk && !levelOk && !previewLocked) return;
                            if (onlyEquippable && !(classOk && levelOk) && !previewLocked) return;
                            string wt = (a.weight ?? string.Empty).Trim();
                            picked.Add(new ShelfPick(a.id, ReqLevel(a.req), a.defense,
                                classOk && levelOk,
                                LockReason(classOk, levelOk, wt.Length == 0 ? "other heroes" : Cap(wt) + " armor", a.req)));
                        });
                        cappedDrops += EmitCapped(result, VendorWareKind.Armor, picked, perLevelCap, "armor");
                        break;
                    }

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
                            // WO-1041 — the rough stone is category "stone" and so is NOT caught by
                            // IsGem; without this it would sit on the Market's ordinary-materials
                            // shelf and be buyable for gold, which is exactly the leak this ticket
                            // exists to close.
                            if (DeNelle.Core.Catalog.DungeonExclusiveItems.Contains(m.Id)) return;
                            result.Add(new VendorWare(VendorWareKind.Material, m.Id));
                        });
                        break;

                    case "gem":
                    case "gems":
                        Guard.TryEach("Vendor", "stock gem", MaterialCatalog.All, m =>
                        {
                            if (m == null || string.IsNullOrEmpty(m.Id)) return;
                            if (!IsGem(m)) return;
                            // ⛔ WO-1041 — THE PRE-EXISTING EXCLUSIVITY LEAK, CLOSED HERE.
                            // Until 2026-08-16 this band stocked EVERY crystal-category material, so
                            // the `jeweler` vendor (vendors.json categories: ring/amulet/gem) sold
                            // ing_ember_crystal + ing_aether_shard for 20 gold and
                            // ing_heartstone_crystal for 18 — the exact three gems jeweler-recipes.json
                            // consumes. A player could therefore buy the entire ring chain over the
                            // counter and never descend, which voids the dungeon pillar's whole
                            // justification. The band survives (a future NON-exclusive gem may still
                            // be shelved); the dungeon-exclusive ids are filtered out of it.
                            // Pinned by DungeonGemExclusivityRegression.
                            if (DeNelle.Core.Catalog.DungeonExclusiveItems.Contains(m.Id))
                            {
                                DeNelle.Core.Diagnostics.FlowTrace.Once("Vendor", "gem-exclusive-" + m.Id,
                                    $"gem '{m.Id}' withheld from vendor shelf - dungeon-exclusive (WO-1041). " +
                                    "Not a missing shelf row: it is earned underground or not at all.");
                                return;
                            }
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
            // WO-960: count the LOCKED rows that actually shipped (post-cap) so the trace
            // proves the preview window's output, not its candidates.
            int lockedShipped = 0;
            foreach (var ware in result)
                if (!ware.Eligible) lockedShipped++;
            string vendorId = vendor != null ? vendor.Id : (vendorContext ?? "<none>");
            string queryStr = $"cats=[{string.Join(",", categories)}] roster=[{string.Join(",", roster)}]" +
                              $" classFilter={(rosterFilter ? "roster" : "none")} maxReqLevel={levelCap}" +
                              $" onlyEquippable={onlyEquippable} perLevelCap={perLevelCap}" +
                              $" lockedPreviewLevels={lockedPreviewLevels}" +
                              $" exclude=[{(excludePrefixes != null ? string.Join(",", excludePrefixes) : string.Empty)}]" +
                              $" job='{job}' lvl={level} layout={LayoutFor(vendorContext)}";
            FlowTrace.Step("Vendor",
                $"{vendorId} resolved {result.Count} items ({lockedShipped} locked) (query: {queryStr})");
            if (result.Count == 0)
                FlowTrace.Warn("Vendor",
                    $"{vendorId} resolved EMPTY - authored emptyLine shown: \"{EmptyLineFor(vendorContext)}\"");

            // ── WO-860 B4 footer decision, traced so the THREE shelf states are distinguishable
            //    in the log without a screenshot. §1.4b: each branch prints a DIFFERENT line and
            //    names the reason — a single "footer:<bool>" line would prove nothing about WHY.
            thinnedByCap = cappedDrops > 0 && result.Count > 0;
            string footer = FooterLineFor(vendorContext);
            if (result.Count == 0)
                FlowTrace.Step("Vendor",
                    $"{vendorId} footer SUPPRESSED (shelf EMPTY - emptyLine owns this state; cap dropped {cappedDrops}).");
            else if (cappedDrops <= 0)
                FlowTrace.Step("Vendor",
                    $"{vendorId} footer SUPPRESSED (shelf FULL - perLevelCap={perLevelCap} dropped 0 of " +
                    $"{result.Count} shipped row(s); nothing to explain).");
            else if (string.IsNullOrEmpty(footer))
                FlowTrace.Warn("Vendor",
                    $"{vendorId} shelf THINNED (cap dropped {cappedDrops}) but vendors.json authors NO " +
                    "footerLine - the player gets no explanation for the short shelf.");
            else
                FlowTrace.Step("Vendor",
                    $"{vendorId} footer SHOWN under {result.Count} row(s) (cap dropped {cappedDrops}): \"{footer}\"");

            return result;
        }

        private static bool HasWeaponCertification()
        {
            foreach (var w in GearCatalog.AllWeapons())
                if (w != null && w.HasVisualCertification) return true;
            return false;
        }

        private static bool HasArmorCertification()
        {
            foreach (var a in GearCatalog.AllArmors())
                if (a != null && a.HasVisualCertification) return true;
            return false;
        }

        // ── WO-860 Part B3: the per-level cap ────────────────────────────────────

        /// <summary>One candidate shelf row, carrying the two keys the cap sorts on.</summary>
        private readonly struct ShelfPick
        {
            public readonly string Id;
            /// <summary>The row's REQUIRED level — the bucket key ("2 options on each new level").</summary>
            public readonly int ReqLevel;
            /// <summary>The ranking stat: damageMult for weapons, defense for armor.</summary>
            public readonly float Power;
            public readonly bool Eligible;
            public readonly string LockReason;
            /// <summary>
            /// The row's weapon FAMILY, lowercased ("sword"/"bow"/"staff"/"shield"/"axe"/...),
            /// or empty when the band has no kind axis (armor) or the row authors none.
            /// Read by <see cref="ReserveKindSlot"/> only — it never affects power ranking.
            /// </summary>
            public readonly string Kind;

            public ShelfPick(string id, int reqLevel, float power, bool eligible, string lockReason,
                             string kind = null)
            {
                Id = id;
                ReqLevel = reqLevel;
                Power = power;
                Eligible = eligible;
                LockReason = lockReason;
                Kind = kind ?? string.Empty;
            }
        }

        /// <summary>
        /// Buckets the candidates by REQUIRED LEVEL, keeps the top <paramref name="perLevelCap"/>
        /// of each bucket, and emits them into <paramref name="result"/>.
        ///
        /// THE SORT RULE (documented because it decides what the player sees, and an
        /// unspecified one silently changes with catalog/enumeration order):
        ///   1. BUCKET by <c>req.level</c> — the owner's ask is "2 options on each new level",
        ///      so the cap is per tier, not per shelf.
        ///   2. WITHIN a bucket, rank by POWER DESCENDING — damageMult (weapons) / defense
        ///      (armor). The tier's strongest picks are what a shopper is there for.
        ///   3. TIE-BREAK on ID, ORDINAL ASCENDING (StringComparer.Ordinal, culture- and
        ///      catalog-order-INDEPENDENT). Raw power alone is not a total order — at Lv1 five
        ///      knight weapons share damageMult 1.0 — so without this the surviving two would
        ///      depend on JSON row order and could change on any catalog re-export. Ordinal
        ///      (not OrdinalIgnoreCase, not the current culture) so the answer is identical on
        ///      every device and locale.
        ///   4. EMIT in bucket order ASCENDING, so the shelf reads low tier -> high tier.
        /// The whole thing is a no-op when <paramref name="perLevelCap"/> &lt;= 0.
        ///
        /// KNOWN CONSEQUENCE (flagged, not hidden): shields/off-hands rank by damageMult like
        /// everything else, and starter shields carry little or none — so with a cap of 2 the
        /// Forge will usually surface two MAIN-hand weapons. That is acceptable in V1 because
        /// every class is SEEDED its starter off-hand (WO-860 A3), but if the owner wants a
        /// shield always purchasable, the fix is a per-slot bucket key here, not a sort tweak.
        ///
        /// 5. THE CLASS-KIND RESERVED SLOT (<see cref="ReserveKindSlot"/>, 2026-08-16), added
        ///    because rules 2+3 alone produced a shelf with NOTHING the shopper's class wields.
        ///    PROVEN, not theorised — simulating this comparator over the live catalog gives a
        ///    level-1 Mage `blink_shield1h_04` + `blink_shield1h_05` (two shields, no staff) and
        ///    a level-1 Knight `knight_flameblade` + `blink_axe1h_12`. Every job:"any" shield
        ///    carries damageMult 1.0, ties the level-1 staves/blades and wins on id ordinal, so
        ///    the class weapon is capped out at every tier where the ladder values tie. The
        ///    2026-08-14 blink_ unhide commit named this exact displacement and said the fix
        ///    "needs a PO call or a .cs tie-break" — this is that tie-break.
        ///    ⛔ It is deliberately NOT a damageMult/rarity retune (that would move BALANCE to
        ///    fix a SORT) and NOT a class-weighted primary key (that would rank a weak class
        ///    weapon above a strictly stronger off-kind one everywhere, not just on ties).
        ///    It is a QUOTA: the LAST kept slot of a bucket is reserved for the highest-ranked
        ///    row of the shopper's own kind, and only when the kept slice contains none. Power
        ///    still orders every other slot, and a tier whose catalog holds no class-kind row is
        ///    left honestly as-is rather than padded.
        /// </summary>
        /// <summary>Returns the number of candidate rows the cap DROPPED (0 when uncapped or
        /// when every bucket fit) — the bit WO-860 B4's footer render is gated on.</summary>
        private static int EmitCapped(List<VendorWare> result, VendorWareKind kind,
                                      List<ShelfPick> picked, int perLevelCap, string label,
                                      string preferredKind = null)
        {
            if (picked == null || picked.Count == 0) return 0;

            if (perLevelCap <= 0)
            {
                // Uncapped (pre-860 behaviour): emit in catalog order, untouched.
                foreach (var p in picked)
                    result.Add(new VendorWare(kind, p.Id, p.Eligible, p.LockReason));
                return 0;
            }

            var byLevel = new Dictionary<int, List<ShelfPick>>();
            foreach (var p in picked)
            {
                if (!byLevel.TryGetValue(p.ReqLevel, out var bucket))
                {
                    bucket = new List<ShelfPick>();
                    byLevel[p.ReqLevel] = bucket;
                }
                bucket.Add(p);
            }

            var levels = new List<int>(byLevel.Keys);
            levels.Sort();

            int dropped = 0;
            foreach (int lvl in levels)
            {
                var bucket = byLevel[lvl];
                bucket.Sort(ComparePicks);
                int keep = Math.Min(perLevelCap, bucket.Count);
                ReserveKindSlot(bucket, keep, preferredKind, lvl, label);
                dropped += bucket.Count - keep;
                for (int i = 0; i < keep; i++)
                    result.Add(new VendorWare(kind, bucket[i].Id, bucket[i].Eligible, bucket[i].LockReason));
            }

            if (dropped > 0)
                FlowTrace.Step("Vendor",
                    $"perLevelCap={perLevelCap} on {label}: kept {picked.Count - dropped}/{picked.Count} " +
                    $"row(s) across {levels.Count} level bucket(s) (power desc, id ordinal asc).");

            return dropped;
        }

        /// <summary>The rank comparison of rule 2+3 above (power DESC, then id ORDINAL ASC).</summary>
        private static int ComparePicks(ShelfPick a, ShelfPick b)
        {
            int byPower = b.Power.CompareTo(a.Power);          // descending
            if (byPower != 0) return byPower;
            return string.CompareOrdinal(a.Id ?? string.Empty, b.Id ?? string.Empty);
        }

        /// <summary>
        /// Rule 5 of the documented sort: reserve the LAST kept slot of an already-ranked
        /// bucket for the shopper's own weapon kind, and ONLY when the kept slice holds none.
        /// No-ops when the cap is 0, when nothing is being dropped anyway, when the shelf has
        /// no kind axis (armor / no shopper class), or when the bucket itself contains no row
        /// of that kind — a tier the catalog never authored for this class stays honestly
        /// empty of it instead of being padded with something else.
        /// </summary>
        private static void ReserveKindSlot(List<ShelfPick> bucket, int keep, string preferredKind,
                                            int lvl, string label)
        {
            if (bucket == null || keep <= 0 || bucket.Count <= keep) return;
            if (string.IsNullOrEmpty(preferredKind)) return;

            for (int i = 0; i < keep; i++)
                if (KindMatches(bucket[i].Kind, preferredKind)) return;   // already represented

            int found = -1;
            for (int i = keep; i < bucket.Count; i++)
                if (KindMatches(bucket[i].Kind, preferredKind)) { found = i; break; }
            if (found < 0)
            {
                FlowTrace.Once("Vendor", $"kindslot-none-{label}-{preferredKind}-lv{lvl}",
                    $"{label} Lv{lvl}: no '{preferredKind}' row in this bucket at all - the shelf shows the " +
                    "power ranking unchanged (catalog gap, not a sort defect).");
                return;
            }

            var promoted = bucket[found];
            var evicted = bucket[keep - 1];
            bucket.RemoveAt(found);
            bucket.Insert(keep - 1, promoted);
            FlowTrace.Step("Vendor",
                $"{label} Lv{lvl}: reserved slot {keep} for the shopper's own kind '{preferredKind}' - " +
                $"'{promoted.Id}' promoted over '{evicted.Id}' (power ranking untouched on slots 1..{keep - 1}).");
        }

        private static bool KindMatches(string kind, string preferred) =>
            !string.IsNullOrEmpty(kind) &&
            kind.Equals(preferred, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// The weapon kind a class actually WIELDS — the one the reserved slot guarantees.
        /// Sourced from the authored starter kits + the designed ladders in weapons.json
        /// (knight: knight_starter..aegis_emberbrand are swords; ranger: ranger_starter /
        /// aegis_heartwood_longbow are bows; mage: mage_oak..aegis_aetherstaff are staves;
        /// cleric: aegis_hallowed_censer is a hammer), NOT invented here. Unknown/empty job
        /// (an unregistered shelf with no shopper) returns empty => the quota is inert.
        /// </summary>
        private static string PrimaryWeaponKind(string job)
        {
            switch ((job ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "knight": return "sword";
                case "ranger": return "bow";
                case "mage":   return "staff";
                case "cleric": return "hammer";
                default:       return string.Empty;
            }
        }

        /// <summary>
        /// The row's weapon family. The authored <c>category</c> is AUTHORITATIVE; the keyword
        /// arm below exists only for the 10 legacy designed rows that predate the field
        /// (knight_starter/iron/oath/dawn, ranger_starter, cleric_starter, the four aegis_*,
        /// knight_flameblade) and is deliberately narrow — it never overrides authored data, and
        /// every uncategorised row is TRACED so the data gap is visible rather than guessed at
        /// forever. A row it cannot place returns empty and simply never wins the reserved slot.
        /// </summary>
        private static string WeaponKind(WeaponDef w)
        {
            if (w == null) return string.Empty;
            if (!string.IsNullOrEmpty(w.category)) return w.category.Trim().ToLowerInvariant();

            string key = ((w.id ?? string.Empty) + " " + (w.name ?? string.Empty)).ToLowerInvariant();
            string guess =
                  HasWord(key, "bow") ? "bow"
                : HasWord(key, "staff", "scepter", "sceptre", "rod", "wand") ? "staff"
                : HasWord(key, "shield", "buckler", "targe", "heater") ? "shield"
                : HasWord(key, "sword", "blade", "longsword", "greatsword", "claymore") ? "sword"
                : HasWord(key, "axe", "hatchet") ? "axe"
                : HasWord(key, "hammer", "maul", "mace", "censer") ? "hammer"
                : string.Empty;

            FlowTrace.Once("Vendor", "weaponkind-uncategorised-" + (w.id ?? "<null>"),
                $"weapon '{w.id}' authors no 'category' in weapons.json -> kind inferred as " +
                $"'{(guess.Length == 0 ? "<unknown>" : guess)}' from its name. Authoring the field on the row " +
                "is the real fix; the shelf's class-kind slot is only as good as this data.");
            return guess;
        }

        private static bool HasWord(string haystack, params string[] needles)
        {
            for (int i = 0; i < needles.Length; i++)
                if (haystack.IndexOf(needles[i], StringComparison.Ordinal) >= 0) return true;
            return false;
        }

        /// <summary>
        /// WO-860 B2 — true when this id starts with any authored exclusion prefix
        /// (case-insensitive). Data-driven so the ~65 "blink_*" placeholder rows leave the
        /// SHELF without leaving the CATALOG (they stay equippable/ownable, and the WO
        /// explicitly forbids editing weapons.json/armor.json to fix the overload).
        /// </summary>
        private static bool IsExcludedId(string id, IReadOnlyList<string> prefixes)
        {
            if (string.IsNullOrEmpty(id) || prefixes == null || prefixes.Count == 0) return false;
            for (int i = 0; i < prefixes.Count; i++)
            {
                string p = prefixes[i];
                if (!string.IsNullOrEmpty(p) && id.StartsWith(p, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
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

        /// <summary>WO-960: true when a level-locked row sits inside the vendor's locked
        /// PREVIEW window — req.level in (level, level+previewLevels]. False when the
        /// window is off (0) so the caller's pre-960 paths are untouched.</summary>
        private static bool InPreviewWindow(GearReq req, int level, int previewLevels) =>
            previewLevels > 0 && ReqLevel(req) > level && ReqLevel(req) <= level + previewLevels;

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
