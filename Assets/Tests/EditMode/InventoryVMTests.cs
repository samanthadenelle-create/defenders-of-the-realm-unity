// =============================================================================
// InventoryVMTests (EditMode) — WO-434 Phase B permission gate for InventoryVM.
// -----------------------------------------------------------------------------
// Locks the inventory STATE + LOGIC that Phase C will bind a View to, exercised with
// FAKE IInventoryStore / IEquipTarget so the VM runs with NO scene, NO singleton
// (ARCHITECTURE_PRINCIPLES.md §2 / §2c; mirrors ShopVMTests).
//
// Asserts:
//   • owned-list projection is non-empty + matches the fake store (data gap closed),
//   • tab filtering swaps Slots + resets the selection,
//   • Select fills the Selected detail,
//   • Use / Drop call the store (remove) + raise Changed,
//   • Equip routes a weapon to the equip target + raises Changed,
//   • Dispose unsubscribes (no callback after dispose).
//
// Uses synthetic WeaponDef/ArmorDef + a fully in-memory store, so the asserts hold
// regardless of whether the real gear JSON is shipped in this env.
// =============================================================================

using System;
using System.Collections.Generic;
using NUnit.Framework;
using DeNelle.Village;
using DeNelle.Village.Hero;

namespace DeNelle.Tests.EditMode
{
    [TestFixture]
    public class InventoryVMTests
    {
        // ── In-memory store seeded with synthetic defs (no gear JSON dependency) ──
        private sealed class FakeStore : IInventoryStore
        {
            public readonly Dictionary<string, int> Counts = new Dictionary<string, int>();
            public readonly Dictionary<string, WeaponDef> Weapons = new Dictionary<string, WeaponDef>();
            public readonly Dictionary<string, ArmorDef> Armors = new Dictionary<string, ArmorDef>();
            public int RemoveCalls;

            public event Action Changed;
            public void RaiseChanged() => Changed?.Invoke();

            public IReadOnlyDictionary<string, int> OwnedCounts => Counts;
            public int OwnedQuantity(string id) => Counts.TryGetValue(id, out var v) ? v : 0;
            public WeaponDef FindWeapon(string id) => Weapons.TryGetValue(id, out var w) ? w : null;
            public ArmorDef FindArmor(string id) => Armors.TryGetValue(id, out var a) ? a : null;
            public AccessoryDef FindAccessory(string id) => null;   // WO-543: not exercised here
            public IReadOnlyList<AccessoryDef> AccessoriesForSlot(string slot, int level) => Array.Empty<AccessoryDef>();

            public IReadOnlyList<(WeaponDef def, int qty)> OwnedWeapons()
            {
                var l = new List<(WeaponDef, int)>();
                foreach (var kv in Counts) if (kv.Value > 0 && Weapons.TryGetValue(kv.Key, out var w)) l.Add((w, kv.Value));
                return l;
            }
            public IReadOnlyList<(ArmorDef def, int qty)> OwnedArmor()
            {
                var l = new List<(ArmorDef, int)>();
                foreach (var kv in Counts) if (kv.Value > 0 && Armors.TryGetValue(kv.Key, out var a)) l.Add((a, kv.Value));
                return l;
            }
            public IReadOnlyList<(string id, int qty)> OwnedConsumables()
            {
                var l = new List<(string, int)>();
                foreach (var kv in Counts)
                    if (kv.Value > 0 && !Weapons.ContainsKey(kv.Key) && !Armors.ContainsKey(kv.Key)) l.Add((kv.Key, kv.Value));
                return l;
            }
            public bool WeaponFitsClass(WeaponDef w, string job) =>
                string.IsNullOrEmpty(w?.job) || w.job == "any" || string.Equals(w.job, job, StringComparison.OrdinalIgnoreCase);
            public bool ArmorFitsClass(ArmorDef a, string job) => true;

            public bool TryRemove(string id, int n)
            {
                if (!Counts.TryGetValue(id, out var have) || have < n) return false;
                RemoveCalls++;
                int left = have - n;
                if (left <= 0) Counts.Remove(id); else Counts[id] = left;
                RaiseChanged();
                return true;
            }
        }

        private sealed class FakeEquip : IEquipTarget
        {
            public string TargetName { get; set; } = "Grom";
            public string TargetClass { get; set; } = "knight";
            public int TargetLevel { get; set; } = 1;
            public WeaponDef EquippedWeapon { get; set; }
            public ArmorDef EquippedArmor { get; set; }
            public WeaponDef EquippedOffHand { get; set; }
            public AccessoryDef EquippedRing { get; set; }
            public AccessoryDef EquippedAmulet { get; set; }
            public string EquippedWeaponName => EquippedWeapon?.name;
            public string EquippedArmorName => EquippedArmor?.name;
            public float WeaponMult => EquippedWeapon != null ? EquippedWeapon.damageMult : 1f;
            public float ArmorDefense => EquippedArmor != null ? EquippedArmor.defense : 0f;
            public float CurrentHealth { get; set; }
            public float MaxHealth { get; set; }
            public float CurrentMana { get; set; }
            public float MaxMana { get; set; }
            public int EquipWeaponCalls;
            public event Action EquipChanged;
            public void EquipWeaponById(string id) { EquipWeaponCalls++; EquippedWeapon = new WeaponDef { id = id, name = id, damageMult = 2f }; EquipChanged?.Invoke(); }
            public void EquipArmorById(string id) { EquippedArmor = new ArmorDef { id = id, name = id, defense = 0.3f }; EquipChanged?.Invoke(); }
            public void UnequipWeapon() { EquippedWeapon = null; EquipChanged?.Invoke(); }
            public void UnequipArmor() { EquippedArmor = null; EquipChanged?.Invoke(); }
            public void EquipOffHandById(string id) { EquippedOffHand = new WeaponDef { id = id, name = id, category = "shield" }; EquipChanged?.Invoke(); }
            public void UnequipOffHand() { EquippedOffHand = null; EquipChanged?.Invoke(); }
            public void EquipAccessoryById(string id) { EquippedRing = new AccessoryDef { id = id, name = id, slot = "ring" }; EquipChanged?.Invoke(); }
            public void UnequipAccessory(string slot) { if (slot == "amulet") EquippedAmulet = null; else EquippedRing = null; EquipChanged?.Invoke(); }
        }

        private static FakeStore SeedStore()
        {
            var s = new FakeStore();
            s.Weapons["sword"] = new WeaponDef { id = "sword", name = "Iron Sword", job = "knight", damageMult = 1.2f, rarity = "common" };
            s.Weapons["axe"]   = new WeaponDef { id = "axe",   name = "War Axe",    job = "any",    damageMult = 1.4f, rarity = "rare" };
            s.Armors["mail"]   = new ArmorDef  { id = "mail",  name = "Chainmail",  job = "any",    defense = 0.2f,    rarity = "common" };
            s.Counts["sword"] = 1;
            s.Counts["axe"]   = 2;
            s.Counts["mail"]  = 1;
            s.Counts["heal-potion"] = 3;   // consumable (not in Weapons/Armors)
            return s;
        }

        [Test]
        public void owned_weapons_projection_non_empty_and_matches_store()
        {
            var store = SeedStore();
            using var vm = new InventoryVM(store);

            Assert.That(vm.ActiveTab, Is.EqualTo(InventoryTabKind.Weapons));
            Assert.That(vm.Slots.Count, Is.EqualTo(store.OwnedWeapons().Count));
            Assert.That(vm.Slots.Count, Is.EqualTo(2), "two owned weapons must project to two slots");

            var ids = new HashSet<string>();
            foreach (var s in vm.Slots) ids.Add(s.Id);
            Assert.That(ids.Contains("sword"), Is.True);
            Assert.That(ids.Contains("axe"), Is.True);
        }

        [Test]
        public void tab_counts_reflect_owned_categories()
        {
            var store = SeedStore();
            using var vm = new InventoryVM(store);
            Assert.That(vm.Tabs.Count, Is.EqualTo(4));
            Assert.That(vm.Tabs[(int)InventoryTabKind.Weapons].Count, Is.EqualTo(2));
            Assert.That(vm.Tabs[(int)InventoryTabKind.Armor].Count, Is.EqualTo(1));
            Assert.That(vm.Tabs[(int)InventoryTabKind.Consumables].Count, Is.EqualTo(1));
        }

        [Test]
        public void select_tab_swaps_slots_and_resets_selection()
        {
            var store = SeedStore();
            using var vm = new InventoryVM(store);

            vm.Select(0);
            Assert.That(vm.SelectedId, Is.Not.Null);

            vm.SelectTab((int)InventoryTabKind.Consumables);
            Assert.That(vm.ActiveTab, Is.EqualTo(InventoryTabKind.Consumables));
            Assert.That(vm.SelectedId, Is.Null, "switching tabs must reset selection");
            Assert.That(vm.Slots.Count, Is.EqualTo(1), "one owned consumable");
            Assert.That(vm.Slots[0].Id, Is.EqualTo("heal-potion"));
        }

        [Test]
        public void select_fills_selected_detail()
        {
            var store = SeedStore();
            using var vm = new InventoryVM(store);

            Assert.That(vm.Selected, Is.Null);
            vm.Select(0);
            Assert.That(vm.Selected, Is.Not.Null);
            var d = vm.Selected.Value;
            Assert.That(string.IsNullOrEmpty(d.Name), Is.False);
            Assert.That(d.CanEquip, Is.True, "a weapon detail must be equippable");
            Assert.That(d.CanUse, Is.False, "a weapon is not a consumable");
            Assert.That(vm.SelectedSlotIndex, Is.EqualTo(0));
        }

        [Test]
        public void use_consumable_calls_store_and_raises_changed()
        {
            var store = SeedStore();
            using var vm = new InventoryVM(store);
            vm.SelectTab((int)InventoryTabKind.Consumables);
            vm.Select(0);

            int changed = 0;
            vm.Changed += () => changed++;
            int before = store.RemoveCalls;

            vm.Use();

            Assert.That(store.RemoveCalls, Is.EqualTo(before + 1), "Use must remove one through the store");
            Assert.That(changed, Is.GreaterThan(0), "Use must raise Changed");
            Assert.That(store.OwnedQuantity("heal-potion"), Is.EqualTo(2), "one consumable consumed");
        }

        [Test]
        public void drop_calls_store_and_raises_changed()
        {
            var store = SeedStore();
            using var vm = new InventoryVM(store);
            vm.Select(0);   // a weapon

            int changed = 0;
            vm.Changed += () => changed++;
            int before = store.RemoveCalls;

            vm.Drop();

            Assert.That(store.RemoveCalls, Is.EqualTo(before + 1), "Drop must remove one through the store");
            Assert.That(changed, Is.GreaterThan(0), "Drop must raise Changed");
        }

        [Test]
        public void equip_routes_weapon_to_equip_target_and_raises_changed()
        {
            var store = SeedStore();
            var equip = new FakeEquip();
            using var vm = new InventoryVM(store, equip);

            // Select the owned weapon "sword".
            int idx = -1;
            for (int i = 0; i < vm.Slots.Count; i++) if (vm.Slots[i].Id == "sword") { idx = i; break; }
            Assert.That(idx, Is.GreaterThanOrEqualTo(0));
            vm.Select(idx);

            int changed = 0;
            vm.Changed += () => changed++;

            vm.Equip();

            Assert.That(equip.EquipWeaponCalls, Is.EqualTo(1), "Equip must route the weapon to the equip target");
            Assert.That(equip.EquippedWeapon?.id, Is.EqualTo("sword"));
            Assert.That(changed, Is.GreaterThan(0), "Equip must raise Changed");
        }

        [Test]
        public void store_changed_event_rebuilds_and_raises()
        {
            var store = SeedStore();
            using var vm = new InventoryVM(store);

            int changed = 0;
            vm.Changed += () => changed++;

            // External grant of a new weapon -> the VM must re-render off the store event.
            store.Weapons["spear"] = new WeaponDef { id = "spear", name = "Spear", job = "any", damageMult = 1.3f };
            store.Counts["spear"] = 1;
            store.RaiseChanged();

            Assert.That(changed, Is.GreaterThan(0), "store Changed must propagate to the VM");
            Assert.That(vm.Slots.Count, Is.EqualTo(3), "the new owned weapon must appear");
        }

        [Test]
        public void dispose_unsubscribes_no_callback_after_dispose()
        {
            var store = SeedStore();
            var vm = new InventoryVM(store);

            int changed = 0;
            vm.Changed += () => changed++;
            vm.Dispose();

            int before = changed;
            store.RaiseChanged();
            Assert.That(changed, Is.EqualTo(before), "after Dispose the VM must not raise Changed from store events");
        }
    }
}
