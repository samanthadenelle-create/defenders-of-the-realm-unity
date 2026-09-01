using System.Collections.Generic;
using DeNelle.Core.Social;
using DeNelle.Core.State;
using NUnit.Framework;

namespace DeNelle.Core.Tests
{
    [TestFixture]
    public sealed class TownShowcaseClientTests
    {
        private static PublicTownSnapshot Snapshot(params string[] ids)
        {
            var layout = new List<PlacedStructureData>();
            for (int i = 0; i < ids.Length; i++) layout.Add(new PlacedStructureData(ids[i], i, -i, i % 4, 2));
            return PublicTownSnapshotPolicy.FromLayout(layout, "sh_7Hy3qP9mN2xK4v8Q",
                "po_Z4c8V1s6Q0rT5y2M", 1, 1, "1.0.0");
        }

        [Test]
        public void top_ten_affordance_is_only_enabled_for_opaque_showcase_ids()
        {
            var visitable = new TopTownVisitEntry { ShowcaseId = "sh_7Hy3qP9mN2xK4v8Q" };
            var privateTown = new TopTownVisitEntry { ShowcaseId = null };
            Assert.That(visitable.CanVisit, Is.True);
            Assert.That(visitable.VisitLabel, Is.EqualTo("Visit Town"));
            Assert.That(privateTown.CanVisit, Is.False);
            Assert.That(privateTown.VisitLabel, Is.EqualTo("Town not shared"));
        }

        [Test]
        public void reconstruction_keeps_valid_neighbors_and_marks_missing_skus_explicitly()
        {
            var view = new ReadOnlyTownShowcaseView();
            Assert.That(view.Reconstruct(Snapshot("known_tower", "remote_missing"), new Catalog("known_tower")), Is.True);
            Assert.That(view.Structures.Count, Is.EqualTo(2));
            Assert.That(view.Structures[0].IsFallback, Is.False);
            Assert.That(view.Structures[1].IsFallback, Is.True);
            Assert.That(view.Structures[1].PresentationItemId, Is.EqualTo(ReadOnlyTownShowcaseView.MissingStructurePlaceholder));
            Assert.That(view.Structures[1].FallbackLabel, Does.Contain("remote_missing"));
        }

        [Test]
        public void ambient_placeholders_are_bounded_and_repeatable()
        {
            var a = TownShowcaseAmbient.Sample("sh_seed", 500, 12.5f);
            var b = TownShowcaseAmbient.Sample("sh_seed", 500, 12.5f);
            Assert.That(a.Count, Is.EqualTo(TownShowcaseAmbient.MaxAmbientEntities));
            Assert.That(b[7], Is.EqualTo(a[7]));
        }

        [Test]
        public void navigation_skips_unpublished_rows_and_preserves_exact_return_anchor()
        {
            var top = new List<TopTownVisitEntry>
            {
                new TopTownVisitEntry { Rank = 1, ShowcaseId = "sh_7Hy3qP9mN2xK4v8Q" },
                new TopTownVisitEntry { Rank = 2 },
                new TopTownVisitEntry { Rank = 3, ShowcaseId = "sh_Z4c8V1s6Q0rT5y2M" },
            };
            var nav = new TownVisitNavigation(top, 0, 6, .4375f);
            Assert.That(nav.Next().Rank, Is.EqualTo(3));
            Assert.That(nav.Previous().Rank, Is.EqualTo(1));
            Assert.That(nav.LeaderboardRow, Is.EqualTo(6));
            Assert.That(nav.LeaderboardScrollPosition, Is.EqualTo(.4375f));
        }

        [TestCase("1.2.0", "1.1.9", 1)]
        [TestCase("1.0.0", "1.0.0", 0)]
        [TestCase("0.9.0", "1.0.0", -1)]
        public void compatibility_check_is_semantic(string installed, string required, int sign)
        {
            Assert.That(System.Math.Sign(TownShowcaseClient.CompareVersions(installed, required)), Is.EqualTo(sign));
        }

        private sealed class Catalog : IReadOnlyTownCatalog
        {
            private readonly string _known;
            public Catalog(string known) { _known = known; }
            public bool ContainsStructure(string itemId) => itemId == _known;
        }
    }
}
