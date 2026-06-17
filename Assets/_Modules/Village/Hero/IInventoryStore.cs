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
        private bool _disposed;

        public event Action Changed;

        public InventoryStore(DeNelle.Village.Crafting.VillageInventory inventory)
        {
            _inventory = inventory;
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

        public IReadOnlyList<(WeaponDef def, int qty)> OwnedWeapons()
        {
            var list = new List<(WeaponDef, int)>();
            foreach (var kv in OwnedCounts)
            {
                if (kv.Value <= 0) continue;
                var w = GearCatalog.FindWeapon(kv.Key);
                if (w != null) list.Add((w, kv.Value));
            }
            return list;
        }

        public IReadOnlyList<(ArmorDef def, int qty)> OwnedArmor()
        {
            var list = new List<(ArmorDef, int)>();
            foreach (var kv in OwnedCounts)
            {
                if (kv.Value <= 0) continue;
                var a = GearCatalog.FindArmor(kv.Key);
                if (a != null) list.Add((a, kv.Value));
            }
            return list;
        }

        public IReadOnlyList<(string id, int qty)> OwnedConsumables()
        {
            var list = new List<(string, int)>();
            foreach (var kv in OwnedCounts)
            {
                if (kv.Value <= 0) continue;
                // Anything owned that resolves to neither a weapon nor an armor def is a consumable
                // (potions, crafting materials, drops) — the inventory's catch-all bucket.
                if (GearCatalog.FindWeapon(kv.Key) == null && GearCatalog.FindArmor(kv.Key) == null)
                    list.Add((kv.Key, kv.Value));
            }
            return list;
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
