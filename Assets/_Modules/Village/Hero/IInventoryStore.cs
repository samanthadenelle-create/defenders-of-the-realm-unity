// =============================================================================
// IInventoryStore — mockable model seam over VillageInventory + GearCatalog (WO-434 Phase A).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.Hero
//
// A future InventoryVM reads OWNED items + their defs through this seam so it never
// names the concretes (VillageInventory / GearCatalog) — the same pattern IShopEquipTarget
// / IEconomy proved (ARCHITECTURE_PRINCIPLES.md §2). PURE C#: no UnityEngine UI types, so a
// fake implementation drives the VM in EditMode with no scene / no singleton.
//
// Exposes exactly what an inventory viewer needs:
//   • owned items (id -> qty) and a single-id quantity lookup,
//   • def resolution (weapon / armor) by id,
//   • the OWNED gear projected by category (weapons / armor / consumables),
//   • fit-by-class checks (so the equip side can filter per party member),
//   • a Changed event so the VM re-renders when the owned set mutates.
// =============================================================================

using System;
using System.Collections.Generic;

namespace DeNelle.Village.Hero
{
    /// <summary>
    /// Mockable read seam over the owned-item model (VillageInventory) + the gear defs
    /// (GearCatalog). The VM binds this instead of the concretes so it stays unit-testable.
    /// </summary>
    public interface IInventoryStore
    {
        /// <summary>Raised when the owned-item set changes (mirrors VillageInventory.Changed).</summary>
        event Action Changed;

        /// <summary>Owned items as id -> quantity (never null; only quantities &gt; 0).</summary>
        IReadOnlyDictionary<string, int> OwnedCounts { get; }

        /// <summary>Quantity of a single owned id (0 when none / unknown).</summary>
        int OwnedQuantity(string id);

        /// <summary>Resolve a weapon def by id, or null.</summary>
        WeaponDef FindWeapon(string id);

        /// <summary>Resolve an armor def by id, or null.</summary>
        ArmorDef FindArmor(string id);

        /// <summary>WO-543: resolve a ring/amulet accessory def by id, or null.</summary>
        AccessoryDef FindAccessory(string id);

        /// <summary>WO-543: accessories that fit the slot ("ring"/"amulet") at the given hero level.
        /// Catalog-sourced (not owned-filtered) per the equip spec. Never null.</summary>
        IReadOnlyList<AccessoryDef> AccessoriesForSlot(string slot, int level);

        /// <summary>Owned weapons the player holds at least one of (def + owned qty).</summary>
        IReadOnlyList<(WeaponDef def, int qty)> OwnedWeapons();

        /// <summary>Owned armor the player holds at least one of (def + owned qty).</summary>
        IReadOnlyList<(ArmorDef def, int qty)> OwnedArmor();

        /// <summary>Owned consumables (anything owned that is neither a weapon nor an armor def): id + qty.</summary>
        IReadOnlyList<(string id, int qty)> OwnedConsumables();

        /// <summary>True when the given class may WIELD the weapon (job match / "any").</summary>
        bool WeaponFitsClass(WeaponDef w, string job);

        /// <summary>True when the given class may WEAR the armor (weight class / "any").</summary>
        bool ArmorFitsClass(ArmorDef a, string job);

        // ── Mutations (WO-434 Phase B: the inventory viewer's Use / Drop need to remove owned
        // units through the seam so InventoryVM never names VillageInventory). Both decrement
        // the owned count (and fire Changed via the underlying inventory). Use == consume-for-
        // effect, Drop == discard; the model treats both as "remove n", so they share TryRemove. ──

        /// <summary>Remove <paramref name="n"/> units of an owned id (consume / drop). True when removed.</summary>
        bool TryRemove(string id, int n);
    }

    /// <summary>
    /// Concrete adapter wrapping a <see cref="VillageInventory"/> (owned counts + Changed) and
    /// the static <see cref="GearCatalog"/> (def resolution + fit checks). Graceful when the
    /// inventory is null (EditMode / pre-boot): reports an empty owned set.
    /// </summary>
    public sealed class InventoryStore : IInventoryStore, IDisposable
    {
        private readonly DeNelle.Village.Crafting.VillageInventory _inventory;

        // WO-578: the owned set RECONCILES two sources of truth so the Inventory, the Forge, and the
        // Gear Preview (EquipVM) all agree on "owned":
        //   1) VillageInventory.Counts — gear the player EXPLICITLY acquired (shop buys, boss/quest
        //      drops via VillageInventory.Add, jeweler crafts), AND
        //   2) the gear each party member currently has AUTO-EQUIPPED from the catalog (GearCatalog.
        //      BestWeapon/BestArmor by class+level). That auto-equip is what the Forge surfaces as the
        //      hero's gear ("Current: Emberbrand"), so the player rightly considers it OWNED — but it
        //      was NEVER written to VillageInventory, which is why the inventory projected empty while
        //      the Forge/EquipVM showed the hero wielding a weapon (the divergence this WO closes).
        // We UNION the equipped pieces in (read-only, NO mutation) so auto-equip behaviour is fully
        // intact. Null/empty sources => behaves exactly as before (pure inventory projection).
        private readonly IReadOnlyList<IEquipTarget> _equippedSources;

        private bool _disposed;

        public event Action Changed;

        public InventoryStore(DeNelle.Village.Crafting.VillageInventory inventory)
            : this(inventory, null) { }

        public InventoryStore(DeNelle.Village.Crafting.VillageInventory inventory,
                              IReadOnlyList<IEquipTarget> equippedSources)
        {
            _inventory = inventory;
            _equippedSources = equippedSources;
            if (_inventory != null) _inventory.Changed += OnInventoryChanged;
        }

        private void OnInventoryChanged() { if (!_disposed) Changed?.Invoke(); }

        public IReadOnlyDictionary<string, int> OwnedCounts =>
            _inventory != null ? _inventory.Counts
                               : (IReadOnlyDictionary<string, int>)EmptyCounts;

        private static readonly Dictionary<string, int> EmptyCounts = new Dictionary<string, int>();

        public int OwnedQuantity(string id) => _inventory != null ? _inventory.Get(id) : 0;

        public WeaponDef FindWeapon(string id) => GearCatalog.FindWeapon(id);
        public ArmorDef FindArmor(string id) => GearCatalog.FindArmor(id);
        public AccessoryDef FindAccessory(string id) => GearCatalog.FindAccessory(id);
        public IReadOnlyList<AccessoryDef> AccessoriesForSlot(string slot, int level) =>
            GearCatalog.AccessoriesForSlot(slot, level);

        public IReadOnlyList<(WeaponDef def, int qty)> OwnedWeapons()
        {
            var list = new List<(WeaponDef, int)>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // (1) Explicitly-acquired weapons from VillageInventory (shop buys / boss-quest drops / crafts).
            foreach (var kv in OwnedCounts)
            {
                if (kv.Value <= 0) continue;
                var w = GearCatalog.FindWeapon(kv.Key);
                if (w != null && seen.Add(w.id)) list.Add((w, kv.Value));
            }

            // (2) WO-578: UNION the currently auto-equipped main-hand + off-hand of every party member
            // (what the Forge surfaces as "owned"). Read-only — auto-equip is untouched. qty = the
            // inventory count if also stocked, else 1 (the wielded copy).
            ForEachEquippedSource(t =>
            {
                AddEquippedWeapon(list, seen, t?.EquippedWeapon);
                AddEquippedWeapon(list, seen, t?.EquippedOffHand);
            });

            DeNelle.Core.Diagnostics.FlowTrace.Throttle("Inventory", "owned-weapons", 1f,
                $"OwnedWeapons resolved {list.Count} (inventory ∪ equipped; sources={SourceCount}).");
            return list;
        }

        public IReadOnlyList<(ArmorDef def, int qty)> OwnedArmor()
        {
            var list = new List<(ArmorDef, int)>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // (1) Explicitly-acquired armor from VillageInventory.
            foreach (var kv in OwnedCounts)
            {
                if (kv.Value <= 0) continue;
                var a = GearCatalog.FindArmor(kv.Key);
                if (a != null && seen.Add(a.id)) list.Add((a, kv.Value));
            }

            // (2) WO-578: UNION the currently auto-equipped chest armor of every party member.
            ForEachEquippedSource(t =>
            {
                var a = t?.EquippedArmor;
                if (a != null && !string.IsNullOrEmpty(a.id) && seen.Add(a.id))
                    list.Add((a, System.Math.Max(1, OwnedQuantity(a.id))));
            });

            DeNelle.Core.Diagnostics.FlowTrace.Throttle("Inventory", "owned-armor", 1f,
                $"OwnedArmor resolved {list.Count} (inventory ∪ equipped; sources={SourceCount}).");
            return list;
        }

        public IReadOnlyList<(string id, int qty)> OwnedConsumables()
        {
            var list = new List<(string, int)>();
            foreach (var kv in OwnedCounts)
            {
                if (kv.Value <= 0) continue;
                // Anything owned that resolves to neither a weapon nor an armor def is a consumable
                // (potions, crafting materials, drops) — the inventory's catch-all bucket. Equipped
                // gear is never a consumable, so the WO-578 equipped-union does not apply here.
                if (GearCatalog.FindWeapon(kv.Key) == null && GearCatalog.FindArmor(kv.Key) == null)
                    list.Add((kv.Key, kv.Value));
            }

            DeNelle.Core.Diagnostics.FlowTrace.Throttle("Inventory", "owned-consumables", 1f,
                $"OwnedConsumables resolved {list.Count}.");
            return list;
        }

        // WO-578 helpers — union the equipped pieces in without duplicating an id already counted.
        private int SourceCount => _equippedSources != null ? _equippedSources.Count : 0;

        private void ForEachEquippedSource(Action<IEquipTarget> apply)
        {
            if (_equippedSources == null) return;
            foreach (var t in _equippedSources)
            {
                if (t == null) continue;
                apply(t);
            }
        }

        private void AddEquippedWeapon(List<(WeaponDef, int)> list, HashSet<string> seen, WeaponDef w)
        {
            if (w == null || string.IsNullOrEmpty(w.id)) return;
            if (!seen.Add(w.id)) return;
            list.Add((w, System.Math.Max(1, OwnedQuantity(w.id))));
        }

        public bool WeaponFitsClass(WeaponDef w, string job) => GearCatalog.WeaponFitsClass(w, job);
        public bool ArmorFitsClass(ArmorDef a, string job) => GearCatalog.ArmorFitsClass(a, job);

        public bool TryRemove(string id, int n) =>
            _inventory != null && _inventory.TryConsume(id, n);

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_inventory != null) _inventory.Changed -= OnInventoryChanged;
            Changed = null;
        }
    }
}
