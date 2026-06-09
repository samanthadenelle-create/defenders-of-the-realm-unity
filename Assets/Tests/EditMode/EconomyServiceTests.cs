// SCAFFOLD — CLI must build-verify under Unity Test Framework
// =============================================================================
// EconomyServiceTests (EditMode) — WO-329 regression suite.
// -----------------------------------------------------------------------------
// Pure, deterministic checks on the in-session resource economy that need NO
// GameState/PlayerPrefs: Wood/Iron Grant/TrySpend/CanAfford, the OnChanged
// event, and the outpost TerritoryMultiplier ramp. Crystals/Food are
// GameState-backed (see EconomyService.cs) and are covered by the service-level
// save/load tests, so they are intentionally NOT exercised here.
//
// EditMode never calls Awake(), so the EconomyService singleton is not wired;
// each test holds a direct reference to the component it spawns and tears it
// down with DestroyImmediate. Serialized defaults (Wood 200, Iron 80) apply on
// AddComponent.
// =============================================================================

using NUnit.Framework;
using UnityEngine;
using DeNelle.Village;

namespace DeNelle.Tests.EditMode
{
    [TestFixture]
    public class EconomyServiceTests
    {
        private EconomyService _econ;

        [SetUp]
        public void SetUp()
        {
            var go = new GameObject("EconomyService (test)");
            _econ = go.AddComponent<EconomyService>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_econ != null) Object.DestroyImmediate(_econ.gameObject);
            _econ = null;
        }

        [Test]
        public void starting_wood_and_iron_match_serialized_defaults()
        {
            Assert.That(_econ.Wood, Is.EqualTo(200), "default Wood");
            Assert.That(_econ.Iron, Is.EqualTo(80), "default Iron");
        }

        [Test]
        public void grant_adds_wood_and_iron()
        {
            int wood0 = _econ.Wood;
            int iron0 = _econ.Iron;
            _econ.Grant(wood: 30, iron: 10);
            Assert.That(_econ.Wood, Is.EqualTo(wood0 + 30));
            Assert.That(_econ.Iron, Is.EqualTo(iron0 + 10));
        }

        [Test]
        public void grant_clamps_negative_to_zero()
        {
            int wood0 = _econ.Wood;
            _econ.Grant(wood: -50);
            Assert.That(_econ.Wood, Is.EqualTo(wood0), "negative grant must not subtract");
        }

        [Test]
        public void can_afford_is_true_when_pools_cover_cost()
        {
            Assert.That(_econ.CanAfford(new ResourceCost(wood: 50, iron: 20)), Is.True);
        }

        [Test]
        public void can_afford_is_false_when_short()
        {
            Assert.That(_econ.CanAfford(new ResourceCost(wood: 9999)), Is.False);
        }

        [Test]
        public void try_spend_succeeds_and_deducts()
        {
            int wood0 = _econ.Wood;
            int iron0 = _econ.Iron;
            bool ok = _econ.TrySpend(new ResourceCost(wood: 40, iron: 15));
            Assert.That(ok, Is.True, "affordable spend must return true");
            Assert.That(_econ.Wood, Is.EqualTo(wood0 - 40));
            Assert.That(_econ.Iron, Is.EqualTo(iron0 - 15));
        }

        [Test]
        public void try_spend_fails_atomically_when_short()
        {
            int wood0 = _econ.Wood;
            int iron0 = _econ.Iron;
            bool ok = _econ.TrySpend(new ResourceCost(wood: 10, iron: 9999));
            Assert.That(ok, Is.False, "unaffordable spend must return false");
            Assert.That(_econ.Wood, Is.EqualTo(wood0), "no Wood deducted on failed spend");
            Assert.That(_econ.Iron, Is.EqualTo(iron0), "no Iron deducted on failed spend");
        }

        [Test]
        public void on_changed_fires_on_grant()
        {
            int fires = 0;
            _econ.OnChanged += _ => fires++;
            _econ.Grant(wood: 5);
            Assert.That(fires, Is.EqualTo(1), "OnChanged must fire once per mutation");
        }

        [Test]
        public void territory_multiplier_starts_at_one()
        {
            Assert.That(_econ.TerritoryMultiplier, Is.EqualTo(1f).Within(1e-4));
            Assert.That(_econ.SecuredOutpostCount, Is.EqualTo(0));
        }

        [Test]
        public void territory_multiplier_ramps_per_secured_outpost()
        {
            _econ.OnOutpostSecured();
            _econ.OnOutpostSecured();
            Assert.That(_econ.SecuredOutpostCount, Is.EqualTo(2));
            Assert.That(_econ.TerritoryMultiplier, Is.EqualTo(1.10f).Within(1e-4),
                "1 + 0.05 * securedCount");
        }
    }
}
