// =============================================================================
// InventorySeamTests (EditMode) — WO-434 Phase A permission gate.
// -----------------------------------------------------------------------------
// Locks the two mockable model seams a future InventoryVM / EquipVM bind to
// (ARCHITECTURE_PRINCIPLES.md §2 / §2c):
//   • IInventoryStore  — owned items (id->qty), def resolution, category projection,
//     fit-by-class, and a Changed event mirrored from VillageInventory.
//   • IEquipTarget     — equipped names/defs + stats + equip/unequip + identity over
//     a GearLoadout.
//
// Asserts BOTH that a FAKE drives the contract (proving mockability for the VMs) AND
// that the concrete adapters (InventoryStore / GearLoadoutEquipTarget) wrap the real
// model correctly. Mirrors the EditMode lifecycle pattern (GameObject + AddComponent +
// DestroyImmediate; no scene, no PlayMode).
// =============================================================================

using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using DeNelle.Village;
using DeNelle.Village.Hero;
using DeNelle.Village.Crafting;

namespace DeNelle.Tests.EditMode
{
    [TestFixture]
    public class InventorySeamTests
    {
        // ── A fully in-memory IInventoryStore: proves the VM can mock the seam ──
        private sealed class FakeInventoryStore : IInventoryStore
        {
            public readonly Dictionary<string, int> Counts = new Dictionary<string, int>();
            public event Action Changed;
            public void RaiseChanged() => Changed?.Invoke();

            public IReadOnlyDictionary<string, int> OwnedCounts => Counts;
            public int OwnedQuantity(string id) => Counts.TryGetValue(id, out var v) ? v : 0;
            public WeaponDef FindWeapon(string id) => GearCatalog.FindWeapon(id);
            public ArmorDef FindArmor(string id) => GearCatalog.FindArmor(id);

            public IReadOnlyList<(WeaponDef def, int qty)> OwnedWeapons()
            {
                var l = new List<(WeaponDef, int)>();
                foreach (var kv in Counts) { var w = GearCatalog.FindWeapon(kv.Key); if (w != null) l.Add((w, kv.Value)); }
                return l;
            }
            public IReadOnlyList<(ArmorDef def, int qty)> OwnedArmor()
            {
                var l = new List<(ArmorDef, int)>();
                foreach (var kv in Counts) { var a = GearCatalog.FindArmor(kv.Key); if (a != null) l.Add((a, kv.Value)); }
                return l;
            }
            public IReadOnlyList<(string id, int qty)> OwnedConsumables()
            {
                var l = new List<(string, int)>();
                foreach (var kv in Counts)
                    if (GearCatalog.FindWeapon(kv.Key) == null && GearCatalog.FindArmor(kv.Key) == null)
                        l.Add((kv.Key, kv.Value));
                return l;
            }
            public bool WeaponFitsClass(WeaponDef w, string job) => GearCatalog.WeaponFitsClass(w, job);
            public bool ArmorFitsClass(ArmorDef a, string job) => GearCatalog.ArmorFitsClass(a, job);

            public bool TryRemove(string id, int n)
            {
                if (!Counts.TryGetValue(id, out var have) || have < n) return false;
                int left = have - n;
                if (left <= 0) Counts.Remove(id); else Counts[id] = left;
                RaiseChanged();
                return true;
            }
        }

        // ── Fake IEquipTarget: a pure equip surface with no GearLoadout ──
        private sealed class FakeEquipTarget : IEquipTarget
        {
            public string TargetName { get; set; } = "Tester";
            public string TargetClass { get; set; } = "knight";
            public WeaponDef EquippedWeapon { get; set; }
            public ArmorDef EquippedArmor { get; set; }
            public string EquippedWeaponName => EquippedWeapon?.name;
            public string EquippedArmorName => EquippedArmor?.name;
            public float WeaponMult => EquippedWeapon != null ? EquippedWeapon.damageMult : 1f;
            public float ArmorDefense => EquippedArmor != null ? EquippedArmor.defense : 0f;
            public float CurrentHealth { get; set; }
            public float MaxHealth { get; set; }
            public float CurrentMana { get; set; }
            public float MaxMana { get; set; }
            public event Action EquipChanged;
            public void EquipWeaponById(string id) { EquippedWeapon = new WeaponDef { id = id, name = id, damageMult = 2f }; EquipChanged?.Invoke(); }
            public void EquipArmorById(string id) { EquippedArmor = new ArmorDef { id = id, name = id, defense = 0.3f }; EquipChanged?.Invoke(); }
            public void UnequipWeapon() { EquippedWeapon = null; EquipChanged?.Invoke(); }
            public void UnequipArmor() { EquippedArmor = null; EquipChanged?.Invoke(); }
        }

        // ── Fake (mock) seam ─────────────────────────────────────────────────────

        [Test]
        public void fake_inventory_store_reports_owned_and_resolves_defs_and_raises_changed()
        {
            var store = new FakeInventoryStore();
            store.Counts["potion-x"] = 3;

            Assert.That(store.OwnedQuantity("potion-x"), Is.EqualTo(3));
            Assert.That(store.OwnedQuantity("nope"), Is.EqualTo(0));

            int changed = 0;
            store.Changed += () => changed++;
            store.RaiseChanged();
            Assert.That(changed, Is.EqualTo(1), "Changed must propagate to subscribers");

            // potion-x is not a gear def -> classified as a consumable.
            var cons = store.OwnedConsumables();
            Assert.That(cons.Count, Is.EqualTo(1));
            Assert.That(cons[0].id, Is.EqualTo("potion-x"));
        }

        [Test]
        public void fake_equip_target_reflects_equip_and_unequip()
        {
            var t = new FakeEquipTarget();
            Assert.That(t.WeaponMult, Is.EqualTo(1f));
            Assert.That(t.ArmorDefense, Is.EqualTo(0f));

            t.EquipWeaponById("sword");
            Assert.That(t.EquippedWeaponName, Is.EqualTo("sword"));
            Assert.That(t.WeaponMult, Is.EqualTo(2f));

            t.UnequipWeapon();
            Assert.That(t.EquippedWeapon, Is.Null);
            Assert.That(t.WeaponMult, Is.EqualTo(1f));
        }

        // ── Concrete adapter: InventoryStore over a real VillageInventory ─────────

        [Test]
        public void inventory_store_adapter_reports_owned_and_categorizes()
        {
            var go = new GameObject("VillageInventory (test)");
            var inv = go.AddComponent<VillageInventory>();   // Awake sets Instance + EnsureLoaded
            try
            {
                inv.Clear();                 // deterministic empty start
                inv.Add("potion-test", 2);   // a non-gear id -> consumable bucket

                using var store = new InventoryStore(inv);

                Assert.That(store.OwnedQuantity("potion-test"), Is.EqualTo(2));
                Assert.That(store.OwnedCounts.ContainsKey("potion-test"), Is.True);

                var cons = store.OwnedConsumables();
                bool found = false;
                foreach (var c in cons) if (c.id == "potion-test") { found = true; Assert.That(c.qty, Is.EqualTo(2)); }
                Assert.That(found, Is.True, "owned non-gear id must surface as a consumable");

                // Changed must mirror VillageInventory.Changed.
                int changed = 0;
                store.Changed += () => changed++;
                inv.Add("potion-test", 1);
                Assert.That(changed, Is.GreaterThan(0), "adapter must relay VillageInventory.Changed");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void inventory_store_adapter_is_graceful_with_null_inventory()
        {
            using var store = new InventoryStore(null);
            Assert.That(store.OwnedCounts.Count, Is.EqualTo(0));
            Assert.That(store.OwnedQuantity("anything"), Is.EqualTo(0));
            Assert.That(store.OwnedWeapons().Count, Is.EqualTo(0));
            Assert.That(store.OwnedConsumables().Count, Is.EqualTo(0));
        }

        // ── Concrete adapter: GearLoadoutEquipTarget over a real GearLoadout ─────

        [Test]
        public void equip_target_adapter_reflects_equip_and_unequip_on_loadout()
        {
            const string cls = "knight";
            string weaponKey = "dotr-equip-weapon-" + cls;
            PlayerPrefs.DeleteKey(weaponKey); PlayerPrefs.Save();

            var go = new GameObject("GearLoadout (test)");
            var loadout = go.AddComponent<GearLoadout>();
            loadout.BindOwnerClass(cls);
            try
            {
                var target = new GearLoadoutEquipTarget(loadout, "Hero", cls);
                Assert.That(target.TargetName, Is.EqualTo("Hero"));
                Assert.That(target.TargetClass, Is.EqualTo(cls));

                WeaponDef w = null;
                foreach (var cand in GearCatalog.AllWeapons())
                    if (cand != null && GearCatalog.WeaponFitsClass(cand, cls)) { w = cand; break; }
                Assume.That(w, Is.Not.Null, "no class-fitting weapon in catalog (gear JSON absent in this env)");

                target.EquipWeaponById(w.id);
                Assert.That(target.EquippedWeaponName, Is.EqualTo(w.name));
                Assert.That(target.EquippedWeapon, Is.Not.Null);
                Assert.That(target.WeaponMult, Is.GreaterThan(0f));

                target.UnequipWeapon();
                Assert.That(target.EquippedWeapon, Is.Null, "adapter Unequip must clear the slot on the loadout");
                Assert.That(target.WeaponMult, Is.EqualTo(1f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                PlayerPrefs.DeleteKey(weaponKey); PlayerPrefs.Save();
            }
        }

        [Test]
        public void equip_target_adapter_is_graceful_with_null_loadout()
        {
            var target = new GearLoadoutEquipTarget(null);
            Assert.That(target.WeaponMult, Is.EqualTo(1f));
            Assert.That(target.ArmorDefense, Is.EqualTo(0f));
            Assert.That(target.EquippedWeapon, Is.Null);
            Assert.That(target.TargetName, Is.EqualTo(""));
            Assert.DoesNotThrow(() => target.UnequipWeapon());
            Assert.DoesNotThrow(() => target.EquipArmorById("x"));
        }
    }
}
