// =============================================================================
// EquipDrawerContentsRegression — WO-1061. The equip drawer listed NOTHING, and you
// could not change your weapon.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression (references DeNelle.Village), so this drives the
// REAL EquipVM against the REAL GearCatalog through the REAL InventoryStore. No scene,
// no PlayMode: EquipVM is pure C# behind IInventoryStore / IEquipTarget by design.
//
// ⚠ THIS ORACLE MEASURES ROWS, NOT METHODS. Every case asserts a COUNT of items the
// drawer actually produces for a known fixture. That distinction is the whole point:
// an oracle that asserted "RebuildCompatible exists" or "the builder was called" would
// have passed happily on every day this defect shipped, because the builder WAS called
// and DID run — it just emitted zero rows. Counts are the only assertion that can tell
// the difference between a list and an empty list.
//
// WHAT EACH CASE PINS (WO-1061 §6 acceptance):
//   1. GRANT PATH (the reported symptom, and the highest-value case): a wearer with a
//      weapon EQUIPPED but an EMPTY VillageInventory must still see that weapon in its
//      own slot's drawer. This runs the PRODUCTION InventoryStore with a null inventory,
//      so it exercises the WO-578 "inventory ∪ equipped" union for real. If that union
//      ever regresses, the drawer silently empties again and THIS case is what catches it.
//   2. AT LEAST THE EQUIPPED ITEM (§6.2): the mainhand list contains the equipped staff.
//   3. HAND SPLIT (§6.4): mainhand EXCLUDES shields; offhand lists ONLY shields.
//   4. CLASS GATE NOT WEAKENED (§6.5): a wrong-class weapon in the fixture is still
//      refused. ⛔ The fix for an empty drawer must never be to loosen this gate — every
//      non-UI caller leans on it (GearCatalog.cs F8 seq-642 Fix B). This case is the
//      tripwire that makes "just make the filter permissive" fail the suite.
//   5. TRUE-ZERO (the anti-tautology case): an empty fixture must produce EXACTLY 0 rows.
//      Without this, a bug that made the list unconditionally non-empty would pass 1-4.
//
// HOW THIS CAN GENUINELY FAIL (it is not decoration):
//   • the equipped-union in InventoryStore.OwnedWeapons breaks      -> case 1 fails
//   • weapons.json loses its mage rows / their `job` values drift   -> cases 2-4 fail
//   • the hand split (WeaponDef.IsOffHandItem / category) regresses -> case 3 fails
//   • someone loosens JobMatches to "fix" an empty drawer           -> case 4 fails
//   • the row builder stops emitting                                -> every count -> 0
//
// SCOPE (stated honestly): this measures the VIEWMODEL's row set. Whether those rows
// then reach the screen as GameObjects is the View's half, and EquipmentPanel.RebuildList
// already instruments that seam itself ("stocked N gear row(s) (wanted N, failed N)" +
// its data-empty / built-but-broken split). The two together cover the full chain.
//
// Wire into the suite from DataRegression.RunAll (one line, registered EXACTLY once).
// =============================================================================
using System;
using System.Collections.Generic;
using System.Text;
using DeNelle.Village;
using DeNelle.Village.Hero;

namespace DeNelle.Editor.Regression
{
    public static class EquipDrawerContentsRegression
    {
        private const string MageStaff   = "mage_oak";        // job=mage, category=staff
        private const string AnyShield   = "tripo_shield_a";  // job=any,  category=shield
        private const string KnightSword = "knight_starter";  // job=knight — must NEVER fit a mage

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var log = new StringBuilder();
            log.AppendLine("--- EQUIP DRAWER CONTENTS (WO-1061: the drawer listed nothing) ---");

            // Read the REAL catalog through the game's own load path (Resources-first, the
            // copy that WINS at runtime) — never a re-parse of a JSON file, which would prove
            // something about a file the game may not even read.
            GearCatalog.Reload();

            var staff  = GearCatalog.FindWeapon(MageStaff);
            var shield = GearCatalog.FindWeapon(AnyShield);
            var sword  = GearCatalog.FindWeapon(KnightSword);

            // Fixture integrity first: if the catalog cannot supply the fixture, say SO rather
            // than reporting a misleading zero-row failure against absent data.
            if (staff == null)  failures.Add($"fixture: weapon '{MageStaff}' missing from the live catalog");
            if (shield == null) failures.Add($"fixture: weapon '{AnyShield}' missing from the live catalog");
            if (sword == null)  failures.Add($"fixture: weapon '{KnightSword}' missing from the live catalog");
            if (failures.Count > 0) { reason = Join(log, failures); return false; }

            if (!shield.IsOffHandItem)
                failures.Add($"fixture: '{AnyShield}' is not an off-hand item (category='{shield.category}') — the hand-split case cannot be trusted");
            if (staff.IsOffHandItem)
                failures.Add($"fixture: '{MageStaff}' reports IsOffHandItem — a staff must be main-hand");

            log.AppendLine($"fixture: staff='{staff.id}' job='{staff.job}' | shield='{shield.id}' job='{shield.job}' " +
                           $"| sword='{sword.id}' job='{sword.job}'");

            // ── CASE 1 + 2 — THE GRANT PATH, through the PRODUCTION InventoryStore ──────
            // A mage holding the staff, with a COMPLETELY EMPTY inventory (null VillageInventory).
            // "Equipped" and "owned" are different sets; WO-578 unions them. This is the exact
            // shape of the reported bug, so it is measured against the real store, not a fake.
            {
                var mage = new FakeTarget("Thrain the Wise", "mage", level: 10, weapon: staff);
                var targets = new IEquipTarget[] { mage };
                using var realStore = new InventoryStore((DeNelle.Village.Crafting.VillageInventory)null, targets);

                var owned = realStore.OwnedWeapons();
                log.AppendLine($"[grant-path] real InventoryStore(null inventory, 1 equipped source) -> OwnedWeapons={owned.Count}");
                if (owned.Count == 0)
                    failures.Add("grant-path: the REAL InventoryStore returned 0 owned weapons for a wearer holding an equipped staff — " +
                                 "the WO-578 'inventory ∪ equipped' union is not delivering the equipped item (equipped is not the same as owned)");

                var vm = new EquipVM(realStore, targets);
                int mainCount = CountFor(vm, EquipVM.SlotMainhand, out var mainIds);
                log.AppendLine($"  [grant-path] mainhand drawer rows={mainCount} ids=[{mainIds}]");

                if (mainCount == 0)
                    failures.Add("grant-path: the mainhand drawer produced 0 rows for a mage with an equipped staff and an empty inventory — " +
                                 "this is the WO-1061 defect (an item you are WEARING must appear in its own slot's list)");
                if (!mainIds.Contains(MageStaff))
                    failures.Add($"grant-path: the mainhand drawer does not list the EQUIPPED weapon '{MageStaff}' (listed: [{mainIds}])");
            }

            // ── CASE 3 + 4 — hand split and the class gate, over an explicit fixture ────
            // Fixture = staff (mage, main) + shield (any, off) + sword (knight, main).
            // For a MAGE: mainhand must list the staff ONLY (shield is off-hand, sword is
            // wrong-class); offhand must list the shield ONLY.
            {
                var mage = new FakeTarget("Thrain the Wise", "mage", level: 10, weapon: staff, offHand: shield);
                var targets = new IEquipTarget[] { mage };
                var store = new FakeStore(new[] { staff, shield, sword });
                var vm = new EquipVM(store, targets);

                int mainCount = CountFor(vm, EquipVM.SlotMainhand, out var mainIds);
                int offCount  = CountFor(vm, EquipVM.SlotOffHand,  out var offIds);
                log.AppendLine($"[fixture] mage over 3 owned weapons -> mainhand={mainCount} [{mainIds}] | offhand={offCount} [{offIds}]");

                if (mainCount != 1)
                    failures.Add($"hand-split/class-gate: mainhand listed {mainCount} row(s), expected exactly 1 (the staff). Listed: [{mainIds}]");
                if (!mainIds.Contains(MageStaff))
                    failures.Add($"class-gate: mainhand omitted the mage's own staff '{MageStaff}' (listed: [{mainIds}])");
                if (mainIds.Contains(AnyShield))
                    failures.Add($"hand-split: mainhand listed the shield '{AnyShield}' — shields belong in the OFF hand only (WO-543)");
                if (mainIds.Contains(KnightSword))
                    failures.Add($"⛔ CLASS GATE WEAKENED: a mage's mainhand listed the knight weapon '{KnightSword}'. " +
                                 "The gate was NOT to be loosened to fix the empty drawer (WO-1061 §5) — every non-UI caller relies on it.");

                if (offCount != 1)
                    failures.Add($"hand-split: offhand listed {offCount} row(s), expected exactly 1 (the shield). Listed: [{offIds}]");
                if (!offIds.Contains(AnyShield))
                    failures.Add($"hand-split: offhand omitted the shield '{AnyShield}' (listed: [{offIds}])");
                if (offIds.Contains(MageStaff))
                    failures.Add($"hand-split: offhand listed the staff '{MageStaff}' — the off hand takes shields only");
            }

            // ── CASE 5 — TRUE ZERO (anti-tautology) ────────────────────────────────────
            // Nothing owned, nothing equipped => EXACTLY 0 rows. If this ever reports a
            // non-zero count, the list is being padded from somewhere and every count
            // assertion above becomes meaningless.
            {
                var bare = new FakeTarget("Nobody", "mage", level: 1);
                var vm = new EquipVM(new FakeStore(Array.Empty<WeaponDef>()), new IEquipTarget[] { bare });
                int zero = CountFor(vm, EquipVM.SlotMainhand, out var zeroIds);
                log.AppendLine($"[true-zero] empty fixture -> mainhand rows={zero}");
                if (zero != 0)
                    failures.Add($"true-zero: an empty fixture produced {zero} row(s) [{zeroIds}] — the drawer is padding rows from somewhere, " +
                                 "which would make every other count in this suite meaningless");
            }

            reason = Join(log, failures);
            return failures.Count == 0;
        }

        /// <summary>Select a slot on the VM and MEASURE the rows it produces for it.</summary>
        private static int CountFor(EquipVM vm, string slotKey, out string ids)
        {
            for (int i = 0; i < vm.EquipSlots.Count; i++)
                if (vm.EquipSlots[i].SlotKey == slotKey) { vm.SelectSlot(i); break; }

            var sb = new StringBuilder();
            foreach (var item in vm.CompatibleItems)
            {
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(item.Id);
            }
            ids = sb.ToString();
            return vm.CompatibleItems.Count;
        }

        private static string Join(StringBuilder log, List<string> failures)
        {
            if (failures.Count == 0)
            {
                log.Append("EQUIP_DRAWER_OK — the drawer lists the wearer's own equipped weapon, ")
                   .Append("the hand split holds, and the class gate is intact.");
                return log.ToString();
            }
            log.AppendLine($"EQUIP_DRAWER_FAIL — {failures.Count} problem(s):");
            foreach (var f in failures) log.Append("  • ").AppendLine(f);
            return log.ToString();
        }

        // ── Fakes (pure C#, no scene) ────────────────────────────────────────────────

        /// <summary>Owned-set fake. Fit checks delegate to the REAL GearCatalog so the suite
        /// tests the production gate, never a re-implementation of it that could agree with a bug.</summary>
        private sealed class FakeStore : IInventoryStore
        {
            private readonly List<(WeaponDef, int)> _weapons = new List<(WeaponDef, int)>();

            public FakeStore(IReadOnlyList<WeaponDef> weapons)
            {
                foreach (var w in weapons) if (w != null) _weapons.Add((w, 1));
            }

            public event Action Changed { add { } remove { } }

            public IReadOnlyDictionary<string, int> OwnedCounts { get; } = new Dictionary<string, int>();
            public int OwnedQuantity(string id) => 1;
            public WeaponDef FindWeapon(string id) => GearCatalog.FindWeapon(id);
            public ArmorDef FindArmor(string id) => GearCatalog.FindArmor(id);
            public AccessoryDef FindAccessory(string id) => GearCatalog.FindAccessory(id);
            public IReadOnlyList<AccessoryDef> AccessoriesForSlot(string slot, int level) =>
                GearCatalog.AccessoriesForSlot(slot, level);
            public IReadOnlyList<(WeaponDef def, int qty)> OwnedWeapons() => _weapons;
            public IReadOnlyList<(ArmorDef def, int qty)> OwnedArmor() => Array.Empty<(ArmorDef, int)>();
            public IReadOnlyList<(string id, int qty)> OwnedConsumables() => Array.Empty<(string, int)>();
            public bool WeaponFitsClass(WeaponDef w, string job) => GearCatalog.WeaponFitsClass(w, job);
            public bool ArmorFitsClass(ArmorDef a, string job) => GearCatalog.ArmorFitsClass(a, job);
            public bool TryRemove(string id, int n) => false;
        }

        /// <summary>A wearer with a class, a level and an equipped loadout. Commands are inert —
        /// this suite measures what the drawer LISTS, not what equipping does.</summary>
        private sealed class FakeTarget : IEquipTarget
        {
            public FakeTarget(string name, string job, int level,
                              WeaponDef weapon = null, WeaponDef offHand = null, ArmorDef armor = null)
            {
                TargetName = name; TargetClass = job; TargetLevel = level;
                EquippedWeapon = weapon; EquippedOffHand = offHand; EquippedArmor = armor;
            }

            public string TargetName { get; }
            public string TargetClass { get; }
            public int TargetLevel { get; }

            public string EquippedWeaponName => EquippedWeapon != null ? EquippedWeapon.name : string.Empty;
            public string EquippedArmorName  => EquippedArmor  != null ? EquippedArmor.name  : string.Empty;

            public WeaponDef EquippedWeapon { get; }
            public ArmorDef EquippedArmor { get; }
            public WeaponDef EquippedOffHand { get; }
            public AccessoryDef EquippedRing => null;
            public AccessoryDef EquippedAmulet => null;

            public float WeaponMult => 1f;
            public float ArmorDefense => 0f;
            public float CurrentHealth => 0f;
            public float MaxHealth => 0f;
            public float CurrentMana => 0f;
            public float MaxMana => 0f;

            public event Action EquipChanged { add { } remove { } }

            public void EquipWeaponById(string id) { }
            public void EquipArmorById(string id) { }
            public void UnequipWeapon() { }
            public void UnequipArmor() { }
            public void EquipOffHandById(string id) { }
            public void UnequipOffHand() { }
            public void EquipAccessoryById(string id) { }
            public void UnequipAccessory(string slot) { }
        }
    }
}
