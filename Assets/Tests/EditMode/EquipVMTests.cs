// =============================================================================
// EquipVMTests (EditMode) — WO-434 Phase B permission gate for EquipVM.
// -----------------------------------------------------------------------------
// Locks the equip-screen STATE + LOGIC that Phase C will bind a View to, exercised
// with FAKE IInventoryStore / IEquipTarget so the VM runs with NO scene
// (ARCHITECTURE_PRINCIPLES.md §2 / §2c; mirrors ShopVMTests / InventoryVMTests).
//
// Asserts:
//   • slots + stats build for the active target,
//   • the compatible-items filter changes with the selected slot (weapon vs armor),
//   • Equip / Unequip / Swap route to the target + raise Changed,
//   • SelectTarget switches the active member (party picker),
//   • Dispose unsubscribes (no callback after dispose).
// =============================================================================

using System;
using System.Collections.Generic;
using NUnit.Framework;
using DeNelle.Village;
using DeNelle.Village.Hero;

namespace DeNelle.Tests.EditMode
{
    [TestFixture]
    public class EquipVMTests
    {
        private sealed class FakeStore : IInventoryStore
        {
            public readonly Dictionary<string, int> Counts = new Dictionary<string, int>();
            public readonly Dictionary<string, WeaponDef> Weapons = new Dictionary<string, WeaponDef>();
            public readonly Dictionary<string, ArmorDef> Armors = new Dictionary<string, ArmorDef>();

            public event Action Changed;
            public void RaiseChanged() => Changed?.Invoke();

            public IReadOnlyDictionary<string, int> OwnedCounts => Counts;
            public int OwnedQuantity(string id) => Counts.TryGetValue(id, out var v) ? v : 0;
            public WeaponDef FindWeapon(string id) => Weapons.TryGetValue(id, out var w) ? w : null;
            public ArmorDef FindArmor(string id) => Armors.TryGetValue(id, out var a) ? a : null;

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
            public IReadOnlyList<(string id, int qty)> OwnedConsumables() => Array.Empty<(string, int)>();
            public bool WeaponFitsClass(WeaponDef w, string job) =>
                string.IsNullOrEmpty(w?.job) || w.job == "any" || string.Equals(w.job, job, StringComparison.OrdinalIgnoreCase);
            public bool ArmorFitsClass(ArmorDef a, string job) => true;
            public bool TryRemove(string id, int n) => false;
        }

        private sealed class FakeEquip : IEquipTarget
        {
            public string TargetName { get; set; }
            public string TargetClass { get; set; } = "knight";
            public WeaponDef EquippedWeapon { get; set; }
            public ArmorDef EquippedArmor { get; set; }
            public string EquippedWeaponName => EquippedWeapon?.name;
            public string EquippedArmorName => EquippedArmor?.name;
            public float WeaponMult => EquippedWeapon != null ? EquippedWeapon.damageMult : 1f;
            public float ArmorDefense => EquippedArmor != null ? EquippedArmor.defense : 0f;
            public int EquipWeaponCalls, EquipArmorCalls, UnequipWeaponCalls;
            public event Action EquipChanged;
            public void EquipWeaponById(string id) { EquipWeaponCalls++; EquippedWeapon = new WeaponDef { id = id, name = id, damageMult = 2f }; EquipChanged?.Invoke(); }
            public void EquipArmorById(string id) { EquipArmorCalls++; EquippedArmor = new ArmorDef { id = id, name = id, defense = 0.3f }; EquipChanged?.Invoke(); }
            public void UnequipWeapon() { UnequipWeaponCalls++; EquippedWeapon = null; EquipChanged?.Invoke(); }
            public void UnequipArmor() { EquippedArmor = null; EquipChanged?.Invoke(); }
        }

        private static FakeStore SeedStore()
        {
            var s = new FakeStore();
            s.Weapons["sword"] = new WeaponDef { id = "sword", name = "Iron Sword", job = "knight", damageMult = 1.2f, rarity = "common" };
            s.Weapons["bow"]   = new WeaponDef { id = "bow",   name = "Longbow",    job = "ranger", damageMult = 1.3f, rarity = "common" };
            s.Armors["mail"]   = new ArmorDef  { id = "mail",  name = "Chainmail",  job = "any",    defense = 0.2f,    rarity = "common" };
            s.Counts["sword"] = 1;
            s.Counts["bow"]   = 1;
            s.Counts["mail"]  = 1;
            return s;
        }

        [Test]
        public void slots_and_stats_build_for_active_target()
        {
            var store = SeedStore();
            var hero = new FakeEquip { TargetName = "Grom", TargetClass = "knight" };
            using var vm = new EquipVM(store, new IEquipTarget[] { hero });

            Assert.That(vm.EquipSlots.Count, Is.EqualTo(2), "mainhand + chest");
            Assert.That(vm.EquipSlots[0].SlotKey, Is.EqualTo(EquipVM.SlotMainhand));
            Assert.That(vm.EquipSlots[1].SlotKey, Is.EqualTo(EquipVM.SlotChest));
            Assert.That(vm.Stats.Count, Is.EqualTo(4), "HP / MP / Damage / Defense");
            Assert.That(vm.CharacterLabel, Does.Contain("Grom"));
        }

        [Test]
        public void compatible_items_filter_changes_with_selected_slot()
        {
            var store = SeedStore();
            var hero = new FakeEquip { TargetName = "Grom", TargetClass = "knight" };
            using var vm = new EquipVM(store, new IEquipTarget[] { hero });

            // Mainhand selected by default -> weapons fitting knight (sword=knight, bow=ranger excluded).
            Assert.That(vm.SelectedSlotKey, Is.EqualTo(EquipVM.SlotMainhand));
            var weaponIds = new HashSet<string>();
            foreach (var i in vm.CompatibleItems) weaponIds.Add(i.Id);
            Assert.That(weaponIds.Contains("sword"), Is.True, "knight sword must be compatible");
            Assert.That(weaponIds.Contains("bow"), Is.False, "ranger bow must be filtered out for a knight");

            // Select the chest slot -> armor list.
            vm.SelectSlot(1);
            Assert.That(vm.SelectedSlotKey, Is.EqualTo(EquipVM.SlotChest));
            var armorIds = new HashSet<string>();
            foreach (var i in vm.CompatibleItems) armorIds.Add(i.Id);
            Assert.That(armorIds.Contains("mail"), Is.True, "armor list after selecting chest");
            Assert.That(armorIds.Contains("sword"), Is.False, "weapons must not appear in the armor slot");
        }

        [Test]
        public void equip_routes_to_target_and_raises_changed()
        {
            var store = SeedStore();
            var hero = new FakeEquip { TargetName = "Grom", TargetClass = "knight" };
            using var vm = new EquipVM(store, new IEquipTarget[] { hero });

            int changed = 0;
            vm.Changed += () => changed++;

            vm.Equip("sword");

            Assert.That(hero.EquipWeaponCalls, Is.EqualTo(1));
            Assert.That(hero.EquippedWeapon?.id, Is.EqualTo("sword"));
            Assert.That(changed, Is.GreaterThan(0));
        }

        [Test]
        public void unequip_clears_selected_slot_and_raises_changed()
        {
            var store = SeedStore();
            var hero = new FakeEquip { TargetName = "Grom", TargetClass = "knight" };
            hero.EquippedWeapon = new WeaponDef { id = "sword", name = "Iron Sword", damageMult = 2f };
            using var vm = new EquipVM(store, new IEquipTarget[] { hero });

            int changed = 0;
            vm.Changed += () => changed++;

            vm.Unequip();   // mainhand selected by default

            Assert.That(hero.UnequipWeaponCalls, Is.EqualTo(1));
            Assert.That(hero.EquippedWeapon, Is.Null);
            Assert.That(changed, Is.GreaterThan(0));
        }

        [Test]
        public void swap_routes_like_equip_and_raises_changed()
        {
            var store = SeedStore();
            var hero = new FakeEquip { TargetName = "Grom", TargetClass = "knight" };
            using var vm = new EquipVM(store, new IEquipTarget[] { hero });

            int changed = 0;
            vm.Changed += () => changed++;

            vm.Swap("sword");

            Assert.That(hero.EquipWeaponCalls, Is.EqualTo(1));
            Assert.That(changed, Is.GreaterThan(0));
        }

        [Test]
        public void select_target_switches_active_member()
        {
            var store = SeedStore();
            var hero = new FakeEquip { TargetName = "Grom", TargetClass = "knight" };
            var comp = new FakeEquip { TargetName = "Sylas", TargetClass = "ranger" };
            using var vm = new EquipVM(store, new IEquipTarget[] { hero, comp });

            Assert.That(vm.ActiveTargetIndex, Is.EqualTo(0));
            Assert.That(vm.CharacterLabel, Does.Contain("Grom"));

            vm.SelectTarget(1);
            Assert.That(vm.ActiveTargetIndex, Is.EqualTo(1));
            Assert.That(vm.CharacterLabel, Does.Contain("Sylas"));

            // With the ranger active, the mainhand compatible list should now admit the bow.
            var ids = new HashSet<string>();
            foreach (var i in vm.CompatibleItems) ids.Add(i.Id);
            Assert.That(ids.Contains("bow"), Is.True, "ranger may wield the bow");
            Assert.That(ids.Contains("sword"), Is.False, "knight sword filtered out for ranger");
        }

        [Test]
        public void equip_change_event_rebuilds_and_raises()
        {
            var store = SeedStore();
            var hero = new FakeEquip { TargetName = "Grom", TargetClass = "knight" };
            using var vm = new EquipVM(store, new IEquipTarget[] { hero });

            int changed = 0;
            vm.Changed += () => changed++;

            // External equip change (e.g. shop or auto-equip) must re-render the VM.
            hero.EquipWeaponById("sword");

            Assert.That(changed, Is.GreaterThan(0), "target EquipChanged must propagate to the VM");
            Assert.That(vm.EquipSlots[0].Content?.Id, Is.EqualTo("sword"), "mainhand slot reflects the equipped weapon");
        }

        [Test]
        public void dispose_unsubscribes_no_callback_after_dispose()
        {
            var store = SeedStore();
            var hero = new FakeEquip { TargetName = "Grom", TargetClass = "knight" };
            var vm = new EquipVM(store, new IEquipTarget[] { hero });

            int changed = 0;
            vm.Changed += () => changed++;
            vm.Dispose();

            int before = changed;
            store.RaiseChanged();
            hero.EquipWeaponById("sword");
            Assert.That(changed, Is.EqualTo(before), "after Dispose no model event may reach the VM");
        }
    }
}
