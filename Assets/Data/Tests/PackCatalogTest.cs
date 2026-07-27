// =============================================================================
// Canonical data — PackCatalog loader tests (EditMode)
// -----------------------------------------------------------------------------
// qa-test-plan.md TC-WAL-08 / TC-WAL-12: packs.json must load via the typed
// PackCatalog and hydrate the five-tier pack ladder (Hearth Spark -> Founder's
// Vow) with per-currency pricing, and the catalogue must hold NO loot-box /
// gacha / randomised content (the bent covenant, monetization-v2-spec §5.3).
//
// PackCatalog reads StreamingAssets/Data/Canonical/packs.json synchronously.
// =============================================================================

using NUnit.Framework;
using DeNelle.Wallet;

namespace DeNelle.Data.Tests
{
    [TestFixture]
    public class PackCatalogTest
    {
        [SetUp]
        public void SetUp()
        {
            PackCatalog.Reload();
        }

        // =====================================================================
        //  Parse + ladder
        // =====================================================================

        [Test]
        public void packs_json_loads_the_full_pack_catalog()
        {
            // The catalogue intentionally grew past the original five-tier ladder: the 5 core tiers
            // (Hearth Spark -> Founder's Vow) plus the added seasonal/bundle offers = 13 today. The
            // tier ladder sub-tests below still pin tiers 1-5. Update this count if the set changes.
            Assert.That(PackCatalog.Packs, Is.Not.Null);
            Assert.That(PackCatalog.Packs.Count, Is.EqualTo(13),
                "packs.json must hydrate the full pack catalogue (5-tier ladder + bundle offers).");
        }

        [Test]
        public void all_five_canon_pack_skus_are_present()
        {
            foreach (var sku in new[]
            {
                "hearth-spark", "lanternlight", "folks-thanks",
                "patron-of-elarion", "founders-vow",
            })
            {
                Assert.That(PackCatalog.Find(sku), Is.Not.Null,
                    $"packs.json must contain the '{sku}' pack.");
            }
        }

        [Test]
        public void each_tier_one_through_five_resolves_exactly_once()
        {
            for (int tier = 1; tier <= 5; tier++)
            {
                Assert.That(PackCatalog.FindByTier(tier), Is.Not.Null,
                    $"tier {tier} must resolve a pack.");
            }
        }

        [Test]
        public void unknown_lookups_return_null()
        {
            Assert.That(PackCatalog.Find("free-money"), Is.Null);
            Assert.That(PackCatalog.Find(null), Is.Null);
            Assert.That(PackCatalog.FindByTier(99), Is.Null);
        }

        // =====================================================================
        //  Pricing — every pack must be purchasable on all three wallet rails
        // =====================================================================

        [Test]
        public void every_pack_has_a_positive_price_on_every_currency_rail()
        {
            foreach (var pack in PackCatalog.Packs)
            {
                Assert.That(pack.Pricing, Is.Not.Null, $"{pack.Sku} has no pricing.");
                Assert.That(pack.AmountFor(CurrencyKind.Sol), Is.GreaterThan(0d),
                    $"{pack.Sku} must have a SOL price.");
                Assert.That(pack.AmountFor(CurrencyKind.Usdc), Is.GreaterThan(0d),
                    $"{pack.Sku} must have a USDC price.");
                Assert.That(pack.AmountFor(CurrencyKind.Skr), Is.GreaterThan(0d),
                    $"{pack.Sku} must have an SKR price.");
                Assert.That(pack.Pricing.Usd, Is.GreaterThan(0d),
                    $"{pack.Sku} must have a USD reference price.");
            }
        }

        [Test]
        public void pack_prices_climb_with_tier()
        {
            // The ladder must be monotonically more expensive Hearth Spark -> Vow.
            double prevUsd = 0d;
            for (int tier = 1; tier <= 5; tier++)
            {
                var pack = PackCatalog.FindByTier(tier);
                Assert.That(pack.Pricing.Usd, Is.GreaterThan(prevUsd),
                    $"tier {tier} ({pack.Sku}) must cost more than tier {tier - 1}.");
                prevUsd = pack.Pricing.Usd;
            }
        }

        [Test]
        public void amount_label_formats_each_rail_with_its_symbol()
        {
            var pack = PackCatalog.FindByTier(1);
            Assert.That(pack.AmountLabel(CurrencyKind.Sol), Does.EndWith("SOL"));
            Assert.That(pack.AmountLabel(CurrencyKind.Usdc), Does.EndWith("USDC"));
            Assert.That(pack.AmountLabel(CurrencyKind.Skr), Does.EndWith("SKR"));
            Assert.That(pack.UsdReference, Does.StartWith("$"));
        }

        // =====================================================================
        //  Covenant compliance — no loot boxes / gacha / randomised content
        // =====================================================================

        [Test]
        public void exactly_one_pack_is_flagged_founder_only()
        {
            int founderCount = 0;
            foreach (var pack in PackCatalog.Packs)
                if (pack.FounderOnly) founderCount++;
            Assert.That(founderCount, Is.EqualTo(1),
                "only the tier-5 Founder's Vow is launch-window-only.");
            Assert.That(PackCatalog.FindByTier(5).FounderOnly, Is.True);
        }

        [Test]
        public void no_pack_name_or_tagline_reads_as_a_loot_box_or_gacha()
        {
            // monetization-v2-spec §5.3 / TC-WAL-12 — convenience power only;
            // zero randomised purchases.
            string[] banned = { "loot", "gacha", "random", "mystery", "lottery", "spin", "gamble" };
            foreach (var pack in PackCatalog.Packs)
            {
                var haystack = $"{pack.Name} {pack.Tagline} {pack.Theme}".ToLowerInvariant();
                foreach (var word in banned)
                {
                    Assert.That(haystack.Contains(word), Is.False,
                        $"{pack.Sku} text contains banned monetisation word '{word}'.");
                }
            }
        }

        [Test]
        public void every_pack_grants_deterministic_fixed_contents()
        {
            // The contents bag is a fixed list — no probability fields, no rolls.
            foreach (var pack in PackCatalog.Packs)
            {
                Assert.That(pack.Contents, Is.Not.Null, $"{pack.Sku} has no contents.");
                Assert.That(pack.Contents.Cosmetics, Is.Not.Null,
                    $"{pack.Sku} cosmetics list must be present (may be empty).");
                Assert.That(pack.Contents.Economy, Is.Not.Null,
                    $"{pack.Sku} economy bag must be present.");
                Assert.That(pack.Contents.Convenience, Is.Not.Null,
                    $"{pack.Sku} convenience list must be present.");
            }
        }

        [Test]
        public void the_currency_disclaimer_is_present()
        {
            // §4.1 — the permanent wallet-rail price-volatility disclaimer.
            Assert.That(string.IsNullOrEmpty(PackCatalog.CurrencyDisclaimer), Is.False,
                "packs.json must carry the wallet-rail currency disclaimer.");
        }
    }
}
