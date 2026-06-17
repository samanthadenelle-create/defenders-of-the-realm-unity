// =============================================================================
// GearLoadoutUnequipTests (EditMode) — WO-434 Phase A permission gate.
// -----------------------------------------------------------------------------
// Locks the additive Unequip API on GearLoadout (ARCHITECTURE_PRINCIPLES.md §2c):
//   • UnequipWeapon/Armor clear the slot, drop the stat to its no-gear baseline,
//     and raise OnGearChanged.
//   • The "none" choice is PERSISTED so a later Refresh() does NOT auto-re-equip
//     the best piece the player just removed (the auto-equip-respects-empty rule).
//
// Mirrors the project's EditMode lifecycle pattern (new GameObject + AddComponent +
// DestroyImmediate in TearDown; PlayerPrefs cleared) — see DevGrantSpendableTests.
// No PlayMode, no scene: GearLoadout is exercised as a bare component bound to a class.
//
// GRACEFUL on an empty catalog: if no weapon/armor fits the test class (gear JSON not
// shipped in this env), the equip-dependent asserts are skipped via Assume — the
// unequip-from-empty + persistence asserts still run and lock the core contract.
// =============================================================================

using NUnit.Framework;
using UnityEngine;
using DeNelle.Village;

namespace DeNelle.Tests.EditMode
{
    [TestFixture]
    public class GearLoadoutUnequipTests
    {
        private const string TestClass = "knight";
        private const string PrefWeaponKey = "dotr-equip-weapon-" + TestClass;
        private const string PrefArmorKey  = "dotr-equip-armor-"  + TestClass;
        private const string NoneSentinel  = "__none__";

        private GameObject _go;
        private GearLoadout _loadout;

        [SetUp]
        public void SetUp()
        {
            // Start each test from a clean persisted state for the test class.
            PlayerPrefs.DeleteKey(PrefWeaponKey);
            PlayerPrefs.DeleteKey(PrefArmorKey);
            PlayerPrefs.Save();

            _go = new GameObject("GearLoadout (test)");
            _loadout = _go.AddComponent<GearLoadout>();
            _loadout.BindOwnerClass(TestClass);   // deterministic class; triggers Refresh()
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
            PlayerPrefs.DeleteKey(PrefWeaponKey);
            PlayerPrefs.DeleteKey(PrefArmorKey);
            PlayerPrefs.Save();
        }

        // Pick a real catalog weapon that fits the test class, or null if the catalog is empty here.
        private static WeaponDef FirstFittingWeapon()
        {
            foreach (var w in GearCatalog.AllWeapons())
                if (w != null && GearCatalog.WeaponFitsClass(w, TestClass)) return w;
            return null;
        }

        private static ArmorDef FirstFittingArmor()
        {
            foreach (var a in GearCatalog.AllArmors())
                if (a != null && GearCatalog.ArmorFitsClass(a, TestClass)) return a;
            return null;
        }

        [Test]
        public void unequip_weapon_clears_slot_resets_mult_and_raises_changed()
        {
            var w = FirstFittingWeapon();
            Assume.That(w, Is.Not.Null, "no class-fitting weapon in the catalog (gear JSON absent in this env)");

            _loadout.EquipWeaponById(w.id);
            Assert.That(_loadout.EquippedWeapon, Is.Not.Null, "precondition: weapon equipped");

            int changed = 0;
            _loadout.OnGearChanged += () => changed++;

            _loadout.UnequipWeapon();

            Assert.That(_loadout.EquippedWeapon, Is.Null, "UnequipWeapon must clear the slot");
            Assert.That(_loadout.WeaponMult, Is.EqualTo(1f), "WeaponMult must fall back to 1.0 with no weapon");
            Assert.That(changed, Is.GreaterThan(0), "UnequipWeapon must fire OnGearChanged");
        }

        [Test]
        public void unequip_armor_clears_slot_resets_defense_and_raises_changed()
        {
            var a = FirstFittingArmor();
            Assume.That(a, Is.Not.Null, "no class-fitting armor in the catalog (gear JSON absent in this env)");

            _loadout.EquipArmorById(a.id);
            Assert.That(_loadout.EquippedArmor, Is.Not.Null, "precondition: armor equipped");

            int changed = 0;
            _loadout.OnGearChanged += () => changed++;

            _loadout.UnequipArmor();

            Assert.That(_loadout.EquippedArmor, Is.Null, "UnequipArmor must clear the slot");
            Assert.That(_loadout.ArmorDefense, Is.EqualTo(0f), "ArmorDefense must fall back to 0 with no armor");
            Assert.That(changed, Is.GreaterThan(0), "UnequipArmor must fire OnGearChanged");
        }

        [Test]
        public void unequip_weapon_persists_none_sentinel()
        {
            var w = FirstFittingWeapon();
            Assume.That(w, Is.Not.Null);

            _loadout.EquipWeaponById(w.id);
            _loadout.UnequipWeapon();

            Assert.That(PlayerPrefs.GetString(PrefWeaponKey, null), Is.EqualTo(NoneSentinel),
                "UnequipWeapon must persist the 'none' sentinel under the per-class key");
        }

        [Test]
        public void refresh_does_not_re_equip_after_unequip_weapon()
        {
            var w = FirstFittingWeapon();
            Assume.That(w, Is.Not.Null);

            _loadout.EquipWeaponById(w.id);
            _loadout.UnequipWeapon();
            Assert.That(_loadout.EquippedWeapon, Is.Null, "precondition: unequipped");

            // A later Refresh()/level-up must HONOUR the empty choice (sentinel persisted),
            // not silently auto-re-equip the best weapon the player just removed.
            _loadout.Refresh();

            Assert.That(_loadout.EquippedWeapon, Is.Null,
                "Refresh must not auto-re-equip after an explicit Unequip (sentinel respected)");
            Assert.That(_loadout.WeaponMult, Is.EqualTo(1f));
        }

        [Test]
        public void refresh_does_not_re_equip_after_unequip_armor()
        {
            var a = FirstFittingArmor();
            Assume.That(a, Is.Not.Null);

            _loadout.EquipArmorById(a.id);
            _loadout.UnequipArmor();
            Assert.That(_loadout.EquippedArmor, Is.Null, "precondition: unequipped");

            _loadout.Refresh();

            Assert.That(_loadout.EquippedArmor, Is.Null,
                "Refresh must not auto-re-equip armor after an explicit Unequip (sentinel respected)");
            Assert.That(_loadout.ArmorDefense, Is.EqualTo(0f));
        }

        [Test]
        public void unequip_on_empty_slot_is_safe_and_persists_sentinel()
        {
            // No equip first — unequip from an already-empty slot must not throw and still
            // records the empty choice (so auto-best stays off after a fresh Refresh).
            Assert.DoesNotThrow(() => _loadout.UnequipWeapon());
            Assert.That(_loadout.EquippedWeapon, Is.Null);
            Assert.That(PlayerPrefs.GetString(PrefWeaponKey, null), Is.EqualTo(NoneSentinel));
        }
    }
}
