// SCAFFOLD — CLI must build-verify under Unity Test Framework
// =============================================================================
// BuildPaletteVMTests (EditMode) — MVVM Silo C §2c permission gate.
// -----------------------------------------------------------------------------
// Locks the prior BuildPaletteUI list behaviour in BuildPaletteVM: the catalog
// query -> card projection, the unlock-gated filter, Configure re-projecting for a
// new verb, the crystal read-out, and the Changed event. Over fake providers +
// a fake IEconomy (no scene / no real CatalogRegistry).
// =============================================================================

using System;
using System.Collections.Generic;
using NUnit.Framework;
using DeNelle.Core.Catalog;
using DeNelle.Village;

namespace DeNelle.Tests.EditMode
{
    [TestFixture]
    public class BuildPaletteVMTests
    {
        private static CatalogEntry Entry(string id, CatalogType type = CatalogType.Tower)
            => new CatalogEntry { id = id, displayName = id, type = type, repo = new RepoProps { buildCost = 10 } };

        private static BuildCategory Category(CatalogType[] types, params string[] locked)
            => new BuildCategory
            {
                Types = types,
                LockedIds = new HashSet<string>(locked, StringComparer.OrdinalIgnoreCase),
            };

        [Test]
        public void cards_project_from_the_query_and_drop_unlock_gated_ids()
        {
            var entries = new List<CatalogEntry> { Entry("ok"), Entry("locked") };
            var vm = new BuildPaletteVM(
                new FakeEconomy { Crystals = 500 },
                _ => Category(new[] { CatalogType.Tower }, "locked"),
                _ => entries,
                _ => false,
                () => entries.Count,
                BuildType.Defense,
                null);

            Assert.That(vm.Cards.Count, Is.EqualTo(1), "the locked id is filtered out");
            Assert.That(vm.Cards[0].Id, Is.EqualTo("ok"));
            Assert.That(vm.Crystals, Is.EqualTo(500));
            Assert.That(vm.RegistryCount, Is.EqualTo(2));
        }

        [Test]
        public void configure_reprojects_for_the_new_verb_and_raises_changed()
        {
            var towerEntries = new List<CatalogEntry> { Entry("tower_a") };
            var townEntries = new List<CatalogEntry> { Entry("farm", CatalogType.Resource), Entry("mill", CatalogType.Resource) };

            var vm = new BuildPaletteVM(
                new FakeEconomy(),
                type => type == BuildType.Town
                    ? Category(new[] { CatalogType.Resource })
                    : Category(new[] { CatalogType.Tower }),
                types => types != null && types.Length > 0 && types[0] == CatalogType.Resource
                    ? townEntries : towerEntries,
                _ => false,
                () => 0,
                BuildType.Defense,
                null);

            Assert.That(vm.Cards.Count, Is.EqualTo(1), "Defense verb lists the tower");

            int changed = 0;
            vm.Changed += () => changed++;
            vm.Configure(BuildType.Town);

            Assert.That(vm.ActiveType, Is.EqualTo(BuildType.Town));
            Assert.That(vm.Cards.Count, Is.EqualTo(2), "Town verb lists the two resource rows");
            Assert.That(changed, Is.EqualTo(1), "Configure raised Changed once");
        }

        [Test]
        public void freebie_provider_marks_cards_free()
        {
            var entries = new List<CatalogEntry> { Entry("freebie_row") };
            var vm = new BuildPaletteVM(
                new FakeEconomy(),
                _ => Category(new[] { CatalogType.Tower }),
                _ => entries,
                _ => true,               // everything is a first-build freebie
                () => 1,
                BuildType.Defense,
                null);

            Assert.That(vm.Cards[0].Freebie, Is.True);
            Assert.That(vm.Cards[0].EffectiveCost.IsZero, Is.True);
        }

        [Test]
        public void dispose_stops_further_change_notifications()
        {
            var vm = new BuildPaletteVM(
                new FakeEconomy(),
                _ => Category(new[] { CatalogType.Tower }),
                _ => new List<CatalogEntry>(),
                _ => false,
                () => 0,
                BuildType.Defense,
                null);

            int changed = 0;
            vm.Changed += () => changed++;
            vm.Dispose();
            vm.Configure(BuildType.Town);   // must not fire after Dispose
            Assert.That(changed, Is.EqualTo(0));
        }
    }
}
