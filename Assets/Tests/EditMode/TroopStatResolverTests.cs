// =============================================================================
// TroopStatResolverTests (EditMode) — WO-771.9 foundation regression suite.
// -----------------------------------------------------------------------------
// Behavioral tests for the pure DeNelle.Village.TroopStatResolver:
//   - Reach curve    -> AttackRange + AggroRadius scaling
//   - Strength curve -> MaxHp + DPS + AttackDamage scaling
//   - Ability-unlock thresholds (below = absent, at/above = present)
//   - Missing upgrade def -> pure baseline (all multipliers 1x, no abilities)
//   - Null def / level clamping
//
// NOTE: this fixture deliberately does NOT reference StatusKind — that type lives
// in DeNelle.BattleATB (not referenced by the EditMode test asmdef). We assert on
// ability ids/counts + numeric stats only. DataRegression wiring is owned by the
// sibling WO-773 lane; these run via -runTests (EditMode).
// =============================================================================

using System.Linq;
using NUnit.Framework;
using DeNelle.Village;

namespace DeNelle.Tests.EditMode
{
    [TestFixture]
    public class TroopStatResolverTests
    {
        private const float Tol = 0.001f;

        [SetUp]
        public void SetUp()
        {
            // Fresh reads so a prior test/session can't leave a stale cache.
            TroopCatalog.Reload();
            TroopUpgradeCatalog.Reload();
        }

        private static TroopDef Footman() => TroopCatalog.Find("troop-footman");
        private static TroopDef Archer() => TroopCatalog.Find("troop-archer");

        private static bool HasAbility(TroopRuntimeStats s, string abilityId) =>
            s.UnlockedAbilities.Any(a => a != null && a.AbilityId == abilityId);

        // ---------------------------------------------------------------------
        // Baseline sanity — catalogs load and the fixtures exist.
        // ---------------------------------------------------------------------

        [Test]
        public void Catalogs_Load_And_Fixtures_Exist()
        {
            Assert.That(Footman(), Is.Not.Null, "troops.json must contain troop-footman");
            Assert.That(Archer(), Is.Not.Null, "troops.json must contain troop-archer");
            Assert.That(TroopUpgradeCatalog.Find("troop-footman"), Is.Not.Null,
                "troop-upgrades.json must contain a row for troop-footman");
            // Every trainable troop must have an upgrade row.
            foreach (var def in TroopCatalog.All)
                Assert.That(TroopUpgradeCatalog.Find(def.Id), Is.Not.Null,
                    $"troop-upgrades.json is missing a row for {def.Id}");
        }

        // ---------------------------------------------------------------------
        // Level 1 == pure baseline (curves start at 1.0).
        // ---------------------------------------------------------------------

        [Test]
        public void Level1_Is_Baseline()
        {
            var def = Footman();
            var s = TroopStatResolver.Effective(def, 1);

            Assert.That(s.ReachMultiplier, Is.EqualTo(1f).Within(Tol));
            Assert.That(s.StrengthMultiplier, Is.EqualTo(1f).Within(Tol));
            Assert.That(s.AttackRange, Is.EqualTo(def.AttackRange).Within(Tol));
            Assert.That(s.AggroRadius, Is.EqualTo(def.HuntScanRadius).Within(Tol));
            Assert.That(s.MaxHp, Is.EqualTo(def.MaxHp).Within(Tol));
            Assert.That(s.AttackDamage, Is.EqualTo(def.AttackDamage).Within(Tol));
            Assert.That(s.UnlockedAbilities, Is.Empty, "no abilities unlock at level 1");
        }

        // ---------------------------------------------------------------------
        // Reach curve -> AttackRange + AggroRadius.
        // ---------------------------------------------------------------------

        [Test]
        public void Reach_Curve_Scales_Range_And_Aggro()
        {
            var def = Archer(); // reach-focused: level 2 multiplier = 1.12
            var s = TroopStatResolver.Effective(def, 2);

            Assert.That(s.ReachMultiplier, Is.EqualTo(1.12f).Within(Tol));
            Assert.That(s.AttackRange, Is.EqualTo(def.AttackRange * 1.12f).Within(Tol));
            Assert.That(s.AggroRadius, Is.EqualTo(def.HuntScanRadius * 1.12f).Within(Tol));
        }

        // ---------------------------------------------------------------------
        // Strength curve -> MaxHp + DPS + AttackDamage.
        // ---------------------------------------------------------------------

        [Test]
        public void Strength_Curve_Scales_Hp_Damage_And_Dps()
        {
            var def = Footman(); // strength-focused: level 2 multiplier = 1.16
            var s = TroopStatResolver.Effective(def, 2);

            float baseDps = def.AttackDamage / def.AttackCooldown;

            Assert.That(s.StrengthMultiplier, Is.EqualTo(1.16f).Within(Tol));
            Assert.That(s.MaxHp, Is.EqualTo(def.MaxHp * 1.16f).Within(Tol));
            Assert.That(s.AttackDamage, Is.EqualTo(def.AttackDamage * 1.16f).Within(Tol));
            Assert.That(s.Dps, Is.EqualTo(baseDps * 1.16f).Within(Tol));
            // Cadence is unchanged by the strength curve.
            Assert.That(s.AttackCooldown, Is.EqualTo(def.AttackCooldown).Within(Tol));
        }

        [Test]
        public void Curves_Plateau_Above_Authored_Length()
        {
            var def = Footman();
            // Curve has 7 values; level 7 and level 99 must resolve identically (clamp).
            var s7 = TroopStatResolver.Effective(def, 7);
            var s99 = TroopStatResolver.Effective(def, 99);

            Assert.That(s99.StrengthMultiplier, Is.EqualTo(s7.StrengthMultiplier).Within(Tol));
            Assert.That(s99.ReachMultiplier, Is.EqualTo(s7.ReachMultiplier).Within(Tol));
            Assert.That(s99.MaxHp, Is.EqualTo(s7.MaxHp).Within(Tol));
        }

        // ---------------------------------------------------------------------
        // Ability-unlock thresholds (footman = 3 / 5 / 7).
        // ---------------------------------------------------------------------

        [Test]
        public void Abilities_Below_Threshold_Are_Absent()
        {
            var def = Footman();
            var s2 = TroopStatResolver.Effective(def, 2);
            Assert.That(s2.UnlockedAbilities, Is.Empty, "nothing unlocks before level 3");
            Assert.That(HasAbility(s2, "knight.sweeping-cut"), Is.False);
        }

        [Test]
        public void Abilities_At_And_Above_Threshold_Are_Present()
        {
            var def = Footman();

            var s3 = TroopStatResolver.Effective(def, 3);
            Assert.That(s3.UnlockedAbilities.Count, Is.EqualTo(1), "level 3 unlocks the first ability");
            Assert.That(HasAbility(s3, "knight.sweeping-cut"), Is.True);
            Assert.That(HasAbility(s3, "knight.wardens-roar"), Is.False, "the level-5 ability must not be early");

            var s6 = TroopStatResolver.Effective(def, 6);
            Assert.That(s6.UnlockedAbilities.Count, Is.EqualTo(2), "levels 3 & 5 are unlocked at level 6");
            Assert.That(HasAbility(s6, "knight.wardens-roar"), Is.True);
            Assert.That(HasAbility(s6, "knight.champions-combo"), Is.False, "the level-7 ability must not be early");

            var s7 = TroopStatResolver.Effective(def, 7);
            Assert.That(s7.UnlockedAbilities.Count, Is.EqualTo(3), "all three abilities unlocked at level 7");
            Assert.That(HasAbility(s7, "knight.champions-combo"), Is.True);
        }

        // ---------------------------------------------------------------------
        // Missing upgrade def -> pure baseline.
        // ---------------------------------------------------------------------

        [Test]
        public void Missing_Upgrade_Def_Is_Pure_Baseline()
        {
            // A troop id with NO row in troop-upgrades.json.
            var def = new TroopDef
            {
                Id = "troop-does-not-exist",
                AttackRange = 5f,
                HuntScanRadius = 10f,
                MaxHp = 50f,
                AttackDamage = 10f,
                AttackCooldown = 2f,
                MoveSpeed = 3f,
            };

            Assert.That(TroopUpgradeCatalog.Find(def.Id), Is.Null, "guard: fixture id must be absent");

            var s = TroopStatResolver.Effective(def, 6);

            Assert.That(s.ReachMultiplier, Is.EqualTo(1f).Within(Tol));
            Assert.That(s.StrengthMultiplier, Is.EqualTo(1f).Within(Tol));
            Assert.That(s.AttackRange, Is.EqualTo(5f).Within(Tol));
            Assert.That(s.AggroRadius, Is.EqualTo(10f).Within(Tol));
            Assert.That(s.MaxHp, Is.EqualTo(50f).Within(Tol));
            Assert.That(s.AttackDamage, Is.EqualTo(10f).Within(Tol));
            Assert.That(s.Dps, Is.EqualTo(5f).Within(Tol)); // 10 / 2
            Assert.That(s.UnlockedAbilities, Is.Empty);
        }

        // ---------------------------------------------------------------------
        // Edge cases — null def + level clamping.
        // ---------------------------------------------------------------------

        [Test]
        public void Null_Def_Returns_Empty_Baseline()
        {
            var s = TroopStatResolver.Effective(null, 5);
            Assert.That(s, Is.Not.Null);
            Assert.That(s.TroopId, Is.Null);
            Assert.That(s.ReachMultiplier, Is.EqualTo(1f).Within(Tol));
            Assert.That(s.StrengthMultiplier, Is.EqualTo(1f).Within(Tol));
            Assert.That(s.UnlockedAbilities, Is.Empty);
        }

        [Test]
        public void NonPositive_Level_Clamps_To_One()
        {
            var def = Footman();
            var s0 = TroopStatResolver.Effective(def, 0);
            var sNeg = TroopStatResolver.Effective(def, -4);
            var s1 = TroopStatResolver.Effective(def, 1);

            Assert.That(s0.Level, Is.EqualTo(1));
            Assert.That(sNeg.Level, Is.EqualTo(1));
            Assert.That(s0.MaxHp, Is.EqualTo(s1.MaxHp).Within(Tol));
            Assert.That(sNeg.MaxHp, Is.EqualTo(s1.MaxHp).Within(Tol));
        }
    }
}
