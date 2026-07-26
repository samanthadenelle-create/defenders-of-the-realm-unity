// =============================================================================
// TowerProjectileTierTests (EditMode) — owner VfxManualPicks per-tier archer keys.
// -----------------------------------------------------------------------------
// Behavioral proof of the tier -> projectile-key mapping the source-lint
// TowerProjectileMapRegression only proves is WIRED. Drives the REAL private
// DefenseTower.ProjectileKeyFor over a DefenseTower + PlacedStructure (the same
// GameObject pairing the build pipeline produces) at levels 1/2/3 and asserts the
// ground-archer default branch returns the owner's tier-named arrow keys verbatim.
// No scene / no PlayMode: reflection reaches the private method + private BoltStyle.
// =============================================================================
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using DeNelle.Core.Combat;
using DeNelle.Village;

namespace DeNelle.Tests.EditMode
{
    [TestFixture]
    public class TowerProjectileTierTests
    {
        private static readonly System.Type DtType = typeof(DefenseTower);

        // Invoke the private DefenseTower.ProjectileKeyFor(element, BoltStyle) for a ground
        // archer (Bolt style, element None, not AirOnly) placed at the given upgrade level.
        private static string GroundArcherKeyAtLevel(int level)
        {
            var go = new GameObject($"TierTower_L{level}");
            try
            {
                var tower = go.AddComponent<DefenseTower>();
                tower.AirOnly = false;
                tower.Element = DamageElement.None;

                var placed = go.AddComponent<PlacedStructure>();
                placed.level = level;

                var boltStyleType = DtType.GetNestedType("BoltStyle", BindingFlags.NonPublic);
                Assert.IsNotNull(boltStyleType, "DefenseTower.BoltStyle nested enum not found (renamed?)");
                object bolt = System.Enum.Parse(boltStyleType, "Bolt");

                var method = DtType.GetMethod("ProjectileKeyFor", BindingFlags.NonPublic | BindingFlags.Instance);
                Assert.IsNotNull(method, "DefenseTower.ProjectileKeyFor not found (renamed?)");

                return (string)method.Invoke(tower, new object[] { DamageElement.None, bolt });
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Tier1_GroundArcher_UsesLevel1ArrowKey()
            => Assert.AreEqual("ArcherTowerLevel1_Projectile", GroundArcherKeyAtLevel(1));

        [Test]
        public void Tier2_GroundArcher_UsesLevel2ArrowKey()
            => Assert.AreEqual("ArcherTowerLevel2_Projectile", GroundArcherKeyAtLevel(2));

        [Test]
        public void Tier3_GroundArcher_UsesBaseArcherKey()
            => Assert.AreEqual("ArcherTower_Projectile", GroundArcherKeyAtLevel(3));

        // No PlacedStructure -> tier defaults to 1 (an EnemyOwned garrison turret / un-placed).
        [Test]
        public void NoPlacedStructure_DefaultsToTier1Key()
        {
            var go = new GameObject("TierTower_NoPlaced");
            try
            {
                var tower = go.AddComponent<DefenseTower>();
                tower.AirOnly = false;
                tower.Element = DamageElement.None;

                var boltStyleType = DtType.GetNestedType("BoltStyle", BindingFlags.NonPublic);
                object bolt = System.Enum.Parse(boltStyleType, "Bolt");
                var method = DtType.GetMethod("ProjectileKeyFor", BindingFlags.NonPublic | BindingFlags.Instance);

                string key = (string)method.Invoke(tower, new object[] { DamageElement.None, bolt });
                Assert.AreEqual("ArcherTowerLevel1_Projectile", key);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        // The AirOnly ranger path must be UNCHANGED by the tier mapping (owner-verbatim).
        [Test]
        public void AirOnlyBallista_UsesRangerBaseKey_Unchanged()
        {
            var go = new GameObject("TierTower_AirOnly");
            try
            {
                var tower = go.AddComponent<DefenseTower>();
                tower.AirOnly = true;
                tower.Element = DamageElement.None;
                var placed = go.AddComponent<PlacedStructure>();
                placed.level = 2;   // even at a higher tier, the AA path is untouched

                var boltStyleType = DtType.GetNestedType("BoltStyle", BindingFlags.NonPublic);
                object bolt = System.Enum.Parse(boltStyleType, "Bolt");
                var method = DtType.GetMethod("ProjectileKeyFor", BindingFlags.NonPublic | BindingFlags.Instance);

                string key = (string)method.Invoke(tower, new object[] { DamageElement.None, bolt });
                Assert.AreEqual("RangerTowerBaseProjectile_Projectile", key);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
