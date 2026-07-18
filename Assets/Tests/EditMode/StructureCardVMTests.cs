// SCAFFOLD — CLI must build-verify under Unity Test Framework
// =============================================================================
// StructureCardVMTests (EditMode) — MVVM Silo C §2c permission gate.
// -----------------------------------------------------------------------------
// Locks the prior BuildPaletteUI + BuildStructureInfoPanel behaviour in the shared
// StructureCardVM: cost/affordability projection, targeting, tier badge/clamp, the
// current + next-tier stat math, and the freebie path. Over a fake IEconomy + a
// hand-built CatalogEntry (no scene).
// =============================================================================

using NUnit.Framework;
using DeNelle.Core.Catalog;
using DeNelle.Village;

namespace DeNelle.Tests.EditMode
{
    [TestFixture]
    public class StructureCardVMTests
    {
        private static CatalogEntry Tower(int wood = 20, int maxLevel = 2,
            float damage = 10f, float range = 8f, float fireRate = 2f,
            bool airOnly = false, bool canHitAir = false)
            => new CatalogEntry
            {
                id = "tower_test",
                displayName = "Test Tower",
                type = CatalogType.Tower,
                repo = new RepoProps
                {
                    cost = new DeNelle.Core.Catalog.ResourceCost { wood = wood },
                    maxLevel = maxLevel,
                    damage = damage,
                    range = range,
                    fireRate = fireRate,
                    airOnly = airOnly,
                    canHitAir = canHitAir,
                },
            };

        [Test]
        public void effective_cost_and_display_project_from_entry()
        {
            var e = Tower(wood: 20);
            var card = new StructureCardVM(e, new FakeEconomy { Wood = 100 }, freebie: false);
            Assert.That(card.Id, Is.EqualTo("tower_test"));
            Assert.That(card.DisplayName, Is.EqualTo("Test Tower"));
            Assert.That(card.EffectiveCost.wood, Is.EqualTo(20));
            Assert.That(card.Freebie, Is.False);
        }

        [Test]
        public void freebie_zeroes_the_cost_and_is_affordable()
        {
            var card = new StructureCardVM(Tower(wood: 20), new FakeEconomy { Wood = 0 }, freebie: true);
            Assert.That(card.EffectiveCost.IsZero, Is.True, "a live freebie pays nothing");
            Assert.That(card.Affordable, Is.True, "a zero cost is always affordable");
        }

        [Test]
        public void affordable_tracks_the_wallet()
        {
            var e = Tower(wood: 20);
            Assert.That(new StructureCardVM(e, new FakeEconomy { Wood = 20 }, false).Affordable, Is.True);
            Assert.That(new StructureCardVM(e, new FakeEconomy { Wood = 19 }, false).Affordable, Is.False);
        }

        [Test]
        public void crystals_only_row_falls_back_to_buildCost()
        {
            var e = new CatalogEntry
            {
                id = "wall", type = CatalogType.Wall,
                repo = new RepoProps { buildCost = 30 },   // no multi-cost -> crystals fallback
            };
            var card = new StructureCardVM(e, new FakeEconomy { Crystals = 30 }, false);
            Assert.That(card.EffectiveCost.crystals, Is.EqualTo(30));
            Assert.That(card.Affordable, Is.True);
        }

        [Test]
        public void targeting_tag_and_line_match_the_repo_flags()
        {
            var ground = new StructureCardVM(Tower(canHitAir: false), new FakeEconomy(), false);
            Assert.That(ground.TargetingTag, Is.EqualTo("Land only"));
            Assert.That(ground.TargetingLine, Is.EqualTo("Targets: Land only"));

            var mixed = new StructureCardVM(Tower(canHitAir: true), new FakeEconomy(), false);
            Assert.That(mixed.TargetingTag, Is.EqualTo("Land + Air"));
            Assert.That(mixed.TargetingLine, Is.EqualTo("Targets: Land + Air"));

            var air = new StructureCardVM(Tower(airOnly: true), new FakeEconomy(), false);
            Assert.That(air.TargetingTag, Is.EqualTo("Air only"));
            Assert.That(air.TargetingLine, Is.EqualTo("Targets: Air only"));
        }

        [Test]
        public void non_tower_has_no_targeting()
        {
            var e = new CatalogEntry { id = "wall", type = CatalogType.Wall, repo = new RepoProps() };
            var card = new StructureCardVM(e, new FakeEconomy(), false);
            Assert.That(card.TargetingTag, Is.Null);
            Assert.That(card.TargetingLine, Is.Null);
        }

        [Test]
        public void tier_badge_and_max_level_clamp()
        {
            Assert.That(new StructureCardVM(Tower(maxLevel: 1), new FakeEconomy(), false).TierBadge, Is.EqualTo("Lv 1"));
            Assert.That(new StructureCardVM(Tower(maxLevel: 2), new FakeEconomy(), false).TierBadge, Is.EqualTo("Lv 1 / 2"));
            // maxLevel is clamped to 1..3.
            Assert.That(new StructureCardVM(Tower(maxLevel: 9), new FakeEconomy(), false).MaxLevel, Is.EqualTo(3));
        }

        [Test]
        public void current_stats_compute_dps_range_firerate()
        {
            var card = new StructureCardVM(Tower(damage: 10f, range: 8f, fireRate: 2f), new FakeEconomy(), false);
            var stats = card.CurrentStats;
            Assert.That(stats.Count, Is.EqualTo(3));
            Assert.That(stats[0].Key, Is.EqualTo("DPS"));
            Assert.That(stats[0].Value, Is.EqualTo("20"));   // 10 * 2
            Assert.That(stats[1].Key, Is.EqualTo("Range"));
            Assert.That(stats[1].Value, Is.EqualTo("8m"));
            Assert.That(stats[2].Key, Is.EqualTo("Fire Rate"));
            Assert.That(stats[2].Value, Is.EqualTo("2/s"));
        }

        [Test]
        public void non_combat_structure_shows_a_type_row()
        {
            var e = new CatalogEntry { id = "wall", type = CatalogType.Wall, repo = new RepoProps() };
            var card = new StructureCardVM(e, new FakeEconomy(), false);
            Assert.That(card.CurrentStats.Count, Is.EqualTo(1));
            Assert.That(card.CurrentStats[0].Key, Is.EqualTo("Type"));
            Assert.That(card.CurrentStats[0].Value, Is.EqualTo("Wall"));
        }

        [Test]
        public void next_tier_preview_computes_deltas_and_cost()
        {
            var card = new StructureCardVM(Tower(wood: 20, maxLevel: 2, damage: 10f, range: 8f, fireRate: 2f),
                new FakeEconomy(), false);
            Assert.That(card.HasNextTier, Is.True);
            Assert.That(card.NextTierTitle, Is.EqualTo("Upgrade to Lv 2"));
            // L2 x1.25: DPS 20 -> 25, Range 8m -> 10m.
            Assert.That(card.NextTierStats, Does.Contain("DPS 20 -> 25"));
            Assert.That(card.NextTierStats, Does.Contain("Range 8m -> 10m"));
            // No authored upgrade table -> the build cost scaled by the level left (x1).
            Assert.That(card.NextTierCost.wood, Is.EqualTo(20));
        }

        [Test]
        public void single_tier_entry_has_no_next_tier()
        {
            var card = new StructureCardVM(Tower(maxLevel: 1), new FakeEconomy(), false);
            Assert.That(card.HasNextTier, Is.False);
        }
    }
}
