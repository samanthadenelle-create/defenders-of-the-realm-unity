// =============================================================================
// ShopCatalog — the ONE capability-style entry point for "what is shoppable here".
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.Hero
//
// THE RECONCILED FILTER (capability-unify pass). Before this, the shop's stock was
// decided by TWO mechanisms scattered through PartyShopVM.BuildBuy():
//   1. VendorStockContract.AllowedFor(ctx)  — which KINDS a vendor offers (Weapon/Armor/Potion).
//   2. GearCatalog.WeaponFitsClass / ArmorFitsClass / MeetsReq(level) — which items a
//      given class+level may equip.
// Both are correct and both already work — this file does NOT replace them, it
// RECONCILES them behind a single resolver so every caller asks ONE question:
//
//     ShopCatalog.Shoppable(vendorContext, job, level)  ->  the filtered shoppable list
//
// Owner directive: the generic is IsShoppable(type: gear, type: class) -> filtered gear,
// and IsShoppable(type: crafting, type: craftable) -> the craftable list. So this resolver
// ALSO extends the unify to CRAFTING: when the vendor's contract allows GearKind.Craftable
// it surfaces the craftable recipes (via the DeNelle.Core.Catalog seam — see the asmdef note
// below), filtered by craftability. One entry point, gear AND craftables.
//
// BEHAVIOR-PRESERVING for gear: the gear branch applies the EXACT same gates the inline
// BuildBuy loops applied (VendorStockContract kinds + WeaponFitsClass/ArmorFitsClass + level),
// in the same weapons-then-armor order, so the gear result is identical.
//
// ASMDEF NOTE (the crafting seam): DeNelle.Village references DeNelle.Core but NOT
// DeNelle.Dungeons (where CraftingData lives), and must not — coupling the heavy Village
// assembly to Dungeons risks a cycle. So craftables are pulled through a thin Core interface,
// DeNelle.Core.Catalog.ICraftableCatalog, that the crafting module registers at boot
// (CraftableShopProvider). The resolver depends only on Core; crafting feeds it. Clean.
//
// INSTRUMENTED per §12 / TGVRU (mirrors HeroArmorVisual): FlowTrace.Step "stocked N",
// FlowTrace.Warn on empty data-vs-filter, Guard.TryEach over each catalog loop so one bad
// row logs + is skipped, never throwing the whole stock build.
// =============================================================================

using System.Collections.Generic;
using DeNelle.Core.Catalog;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village.Hero
{
    /// <summary>The kind of a shoppable entry — which catalog it came from + how a row renders it.</summary>
    public enum ShoppableKind
    {
        Weapon,
        Armor,
        Craftable,
    }

    /// <summary>
    /// One shoppable entry the resolver returns — the unified row shape the View-Model maps.
    /// Carries the originating id + kind; the VM re-resolves the rich def (WeaponDef / ArmorDef)
    /// from GearCatalog by id for its existing row builders, or reads the craftable payload for
    /// a craft row. Presentation-free: no UnityEngine types (the VM is unit-testable per §2c).
    /// </summary>
    public readonly struct ShoppableEntry
    {
        /// <summary>Which catalog this came from (drives the VM's row builder + icon role).</summary>
        public readonly ShoppableKind Kind;
        /// <summary>Stable id — keys GearCatalog.FindWeapon/FindArmor (gear) or the recipe (craftable).</summary>
        public readonly string Id;
        /// <summary>The craftable payload — only meaningful when Kind == Craftable (else default).</summary>
        public readonly ShoppableCraftable Craftable;

        private ShoppableEntry(ShoppableKind kind, string id, ShoppableCraftable craftable)
        {
            Kind = kind;
            Id = id;
            Craftable = craftable;
        }

        public static ShoppableEntry Weapon(string id) => new ShoppableEntry(ShoppableKind.Weapon, id, default);
        public static ShoppableEntry Armor(string id)  => new ShoppableEntry(ShoppableKind.Armor, id, default);
        public static ShoppableEntry FromCraftable(ShoppableCraftable c) =>
            new ShoppableEntry(ShoppableKind.Craftable, c.Id, c);
    }

    /// <summary>
    /// The single shoppable-resolver. Reconciles the vendor KIND gate (VendorStockContract) with
    /// the class/level item gate (GearCatalog) — and extends it to craftables — behind one call.
    /// </summary>
    public static class ShopCatalog
    {
        /// <summary>
        /// THE unified shoppable list for a vendor: every entry the given <paramref name="job"/> at
        /// <paramref name="level"/> may buy at the vendor identified by <paramref name="vendorContext"/>.
        ///   • Gear kinds (Weapon/Armor) → GearCatalog items filtered by class fit + level requirement.
        ///   • Craftable kind            → the registered craftable recipes filtered by craftability.
        /// Never null. Never throws (every loop is Guard.TryEach'd). The result preserves the inline
        /// BuildBuy ordering for gear (weapons, then armor) so the existing UI is unchanged.
        /// </summary>
        public static IReadOnlyList<ShoppableEntry> Shoppable(string vendorContext, string job, int level)
        {
            var result = new List<ShoppableEntry>();

            GearKind allowed = VendorStockContract.AllowedFor(vendorContext ?? string.Empty);

            // ── Gear: weapons (class fit + level), same gates the inline loop applied ──
            if ((allowed & GearKind.Weapon) != 0)
            {
                Guard.TryEach("ShopCatalog", "stock weapon", GearCatalog.AllWeapons(), w =>
                {
                    if (w == null) return;
                    if (!string.IsNullOrEmpty(job) && !GearCatalog.WeaponFitsClass(w, job)) return;
                    if (!MeetsLevel(w.req, level)) return;
                    result.Add(ShoppableEntry.Weapon(w.id));
                });
            }

            // ── Gear: armor (weight/class fit + level) ──
            if ((allowed & GearKind.Armor) != 0)
            {
                Guard.TryEach("ShopCatalog", "stock armor", GearCatalog.AllArmors(), a =>
                {
                    if (a == null) return;
                    if (!GearCatalog.ArmorFitsClass(a, job)) return;
                    if (!MeetsLevel(a.req, level)) return;
                    result.Add(ShoppableEntry.Armor(a.id));
                });
            }

            // ── Crafting: craftable recipes (filtered by craftability) via the Core seam ──
            if ((allowed & GearKind.Craftable) != 0)
            {
                Guard.TryEach("ShopCatalog", "stock craftable", CraftableCatalogRegistry.GetCraftables(), c =>
                {
                    // Only OFFER a recipe that is actually craftable (has ingredient lines defined).
                    if (!c.Craftable || string.IsNullOrEmpty(c.Id)) return;
                    result.Add(ShoppableEntry.FromCraftable(c));
                });
            }

            // ── §12 / TGVRU: trace stock, and split empty into data-vs-filter (never a silent blank) ──
            if (result.Count > 0)
            {
                FlowTrace.Step("ShopCatalog",
                    $"stocked {result.Count} for '{vendorContext}' job='{job}' lvl={level} (kinds {allowed}).");
            }
            else
            {
                bool dataEmpty =
                    GearCatalog.AllWeapons().Count == 0 &&
                    GearCatalog.AllArmors().Count == 0 &&
                    CraftableCatalogRegistry.GetCraftables().Count == 0;
                if (dataEmpty)
                    FlowTrace.Warn("ShopCatalog",
                        $"EMPTY for '{vendorContext}': no gear/craftable DATA loaded (catalogs empty).");
                else
                    FlowTrace.Warn("ShopCatalog",
                        $"EMPTY for '{vendorContext}' job='{job}' lvl={level} (kinds {allowed}) — " +
                        "data present but every entry filtered out by kind/class/level.");
            }

            return result;
        }

        private static bool MeetsLevel(GearReq req, int level) => req == null || level >= req.level;
    }
}
