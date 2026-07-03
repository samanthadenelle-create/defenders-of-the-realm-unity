// =============================================================================
// ShopCatalogShoppableTests (EditMode) — the permission gate for the UNIFIED
// shoppable resolver (DeNelle.Village.Hero.ShopCatalog).
// -----------------------------------------------------------------------------
// Locks the reconciled filter: ONE entry point, ShopCatalog.Shoppable(vendor, job,
// level), folds the vendor-KIND gate (VendorStockContract) and the class/level item
// gate (GearCatalog) into one list — and extends it to craftables via the Core seam
// (CraftableCatalogRegistry). Per ARCHITECTURE_PRINCIPLES §2c, this holistic change is
// only safe while these stay green.
//
// Asserts:
//   • a MAGE weapon context returns only mage-or-"any" weapons (never a ranger-only weapon),
//   • an ARMOR (armorer) context returns ONLY armor entries (no weapons, no craftables),
//   • a CRAFTING context returns the registered craftable recipes (and only craftables),
//   • an UNKNOWN context is never empty when gear data exists (the safe general default).
//
// Crafting is fed through a FAKE ICraftableCatalog registered into the Core registry,
// so the test needs NO DeNelle.Dungeons reference (matching the test asmdef) and is
// deterministic regardless of whether crafting-recipes.json is present in EditMode.
//
// Gear data: GearCatalog loads from canonical JSON. When EditMode cannot reach it the
// gear catalog is empty; the gear assertions are written to hold vacuously in that case
// (they assert "no WRONG entry", which is trivially true of an empty list), while the
// craftable + never-empty assertions stand on the fake/seam alone.
// =============================================================================

using System.Collections.Generic;
using NUnit.Framework;
using DeNelle.Village;
using DeNelle.Village.Hero;
using DeNelle.Core.Catalog;

namespace DeNelle.Tests.EditMode
{
    [TestFixture]
    public class ShopCatalogShoppableTests
    {
        // ── A controllable craftable provider for the seam ──────────────────────
        private sealed class FakeCraftableCatalog : ICraftableCatalog
        {
            private readonly List<ShoppableCraftable> _items;
            public FakeCraftableCatalog(params ShoppableCraftable[] items) =>
                _items = new List<ShoppableCraftable>(items ?? System.Array.Empty<ShoppableCraftable>());
            public IReadOnlyList<ShoppableCraftable> Craftables() => _items;
        }

        private ICraftableCatalog _savedProvider;

        [SetUp]
        public void SetUp()
        {
            // Preserve whatever the runtime registered (usually null in EditMode) and restore in TearDown.
            _savedProvider = CraftableCatalogRegistry.Provider;
            CraftableCatalogRegistry.Provider = null;
            GearCatalog.Reload();
        }

        [TearDown]
        public void TearDown()
        {
            CraftableCatalogRegistry.Provider = _savedProvider;
        }

        [Test]
        public void mage_weapon_context_returns_only_mage_or_any_weapons()
        {
            // "forge" -> Weapon|Armor kinds (WO-598: the vendors.json registry declares the
            // Forge as the full gear trade — weapons + armor). job = mage, generous level.
            var list = ShopCatalog.Shoppable("forge", "mage", 99);

            foreach (var e in list)
            {
                Assert.That(e.Kind, Is.EqualTo(ShoppableKind.Weapon).Or.EqualTo(ShoppableKind.Armor),
                    "a gear vendor must surface only weapon/armor entries");
                if (e.Kind == ShoppableKind.Weapon)
                {
                    var w = GearCatalog.FindWeapon(e.Id);
                    Assert.That(w, Is.Not.Null, $"entry '{e.Id}' must resolve to a real weapon def");
                    // class fit: a mage may only see "mage"/"any"/empty weapons — never a ranger-only one.
                    Assert.That(GearCatalog.WeaponFitsClass(w, "mage"), Is.True,
                        $"weapon '{w.id}' (job='{w.job}') must fit a mage — a ranger-only weapon leaked through");
                }
                else
                {
                    var a = GearCatalog.FindArmor(e.Id);
                    Assert.That(a, Is.Not.Null, $"entry '{e.Id}' must resolve to a real armor def");
                    Assert.That(GearCatalog.ArmorFitsClass(a, "mage"), Is.True,
                        $"armor '{a.id}' (weight='{a.weight}') must fit a mage — a heavy piece leaked through");
                }
            }
        }

        [Test]
        public void armorer_context_returns_only_armor()
        {
            // "armorer" -> Armor kind.
            var list = ShopCatalog.Shoppable("armorer", "knight", 99);

            foreach (var e in list)
            {
                Assert.That(e.Kind, Is.EqualTo(ShoppableKind.Armor),
                    $"an armorer must surface only armor — leaked a {e.Kind} ('{e.Id}')");
                Assert.That(GearCatalog.FindArmor(e.Id), Is.Not.Null,
                    $"entry '{e.Id}' must resolve to a real armor def");
            }
        }

        [Test]
        public void crafting_context_returns_registered_craftables()
        {
            CraftableCatalogRegistry.Provider = new FakeCraftableCatalog(
                new ShoppableCraftable("torch", "Torch", "A simple torch.", "T", craftable: true),
                new ShoppableCraftable("incomplete", "No Recipe", "missing ingredients", "?", craftable: false));

            var list = ShopCatalog.Shoppable("crafting", "knight", 1);

            // Only craftables, and only the actually-craftable one (the no-ingredient recipe is skipped).
            Assert.That(list.Count, Is.EqualTo(1), "only the craftable recipe should be offered");
            Assert.That(list[0].Kind, Is.EqualTo(ShoppableKind.Craftable));
            Assert.That(list[0].Id, Is.EqualTo("torch"));
            foreach (var e in list)
                Assert.That(e.Kind, Is.EqualTo(ShoppableKind.Craftable),
                    "a crafting vendor must surface only craftable entries");
        }

        [Test]
        public void crafting_context_with_no_provider_is_empty_not_thrown()
        {
            // No provider registered (SetUp cleared it) -> a crafting vendor yields nothing, never throws.
            Assert.DoesNotThrow(() =>
            {
                var list = ShopCatalog.Shoppable("workbench", "mage", 5);
                Assert.That(list.Count, Is.EqualTo(0),
                    "with no craftable provider, a crafting vendor must be empty (data-absent, not a crash)");
            });
        }

        [Test]
        public void unknown_context_is_never_empty_when_gear_data_exists()
        {
            // Unknown vendor -> the safe general default (Weapon|Armor|Potion gear). When the gear
            // catalog has ANY data, the resolver must return at least one entry — never a silent blank.
            bool gearDataExists = GearCatalog.AllWeapons().Count > 0 || GearCatalog.AllArmors().Count > 0;
            var list = ShopCatalog.Shoppable("totally-unknown-vendor", "knight", 99);

            if (gearDataExists)
                Assert.That(list.Count, Is.GreaterThan(0),
                    "an unknown vendor must never return empty when gear data exists (safe general default)");
            else
                Assert.That(list, Is.Not.Null,
                    "with no gear data the resolver still returns a (possibly empty) list, never null");
        }
    }
}
