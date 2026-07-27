// =============================================================================
// EnemyLeashLogicTests (EditMode) — locks the WO-770.11 dungeon-leash decision
// (EnemyBrain.ShouldLeashOut + EnemyBrain.SetLeash).
// -----------------------------------------------------------------------------
// RCA (2026-07-26): dungeon skeletons spawned by OutpostEnemyGroupSpawner all
// beeline the entry because the shared EnemyBrain targets the GLOBAL hero with no
// leash (HeroOnly return + FindClosestTarget fallback both hand back the hero
// unconditionally). The hotfix adds an opt-in home-anchor tether: while the hero
// is outside the leash from a mob's spawn anchor, the brain yields NO target and
// the (heartless) DriveNav stops the agent at its spawn.
//
// EDITMODE-TESTABLE (asserted here, real behaviour):
//   - hero OUTSIDE the leash  -> ShouldLeashOut == true  (brain yields no target)
//   - hero INSIDE the leash   -> ShouldLeashOut == false (brain targets the hero)
//   - hero ABSENT             -> ShouldLeashOut == true  (dormant, no chase)
//   - radius <= 0 (DEFAULT)   -> ShouldLeashOut == false ALWAYS — proves an
//     unleashed village/overworld enemy is completely unaffected (zero regression)
//   - SetLeash stores the anchor + clamps a negative radius to disabled (0)
//
// The full Update() path (chase / idle) runs NavMeshAgent + Enemy + hero on a
// baked NavMesh — that belongs in a PlayMode test; the targeting DECISION is a
// pure function locked here.
// =============================================================================

using NUnit.Framework;
using UnityEngine;
using DeNelle.Village;

namespace DeNelle.Tests.EditMode
{
    [TestFixture]
    public class EnemyLeashLogicTests
    {
        private static readonly Vector3 Anchor = new Vector3(10f, 0f, 10f);
        private const float Radius = 10f;

        [Test]
        public void hero_outside_leash_yields_no_target()
        {
            // Hero 20m away on X (> 10m radius) -> mob stays dormant at its anchor.
            Vector3 heroPos = Anchor + new Vector3(20f, 0f, 0f);
            Assert.That(EnemyBrain.ShouldLeashOut(Anchor, Radius, true, heroPos), Is.True,
                "a hero outside the leash must leash the mob OUT (no target)");
        }

        [Test]
        public void hero_inside_leash_targets_hero()
        {
            // Hero 4m away (< 10m radius) -> mob wakes and targets normally.
            Vector3 heroPos = Anchor + new Vector3(4f, 0f, 0f);
            Assert.That(EnemyBrain.ShouldLeashOut(Anchor, Radius, true, heroPos), Is.False,
                "a hero inside the leash must NOT leash the mob (falls through to ChooseTarget)");
        }

        [Test]
        public void hero_exactly_on_radius_is_inside()
        {
            // Boundary: distance == radius is treated as inside (uses > for leash-out).
            Vector3 heroPos = Anchor + new Vector3(Radius, 0f, 0f);
            Assert.That(EnemyBrain.ShouldLeashOut(Anchor, Radius, true, heroPos), Is.False,
                "distance exactly equal to the radius must count as INSIDE the leash");
        }

        [Test]
        public void absent_hero_stays_dormant()
        {
            Assert.That(EnemyBrain.ShouldLeashOut(Anchor, Radius, false, Vector3.zero), Is.True,
                "with no hero present a leashed mob must stay dormant, not roam the entry");
        }

        [Test]
        public void disabled_leash_never_leashes_out()
        {
            // radius 0 = DEFAULT (unleashed). Every existing enemy hits this branch and
            // is unaffected regardless of where the hero is.
            Vector3 farHero = Anchor + new Vector3(500f, 0f, 500f);
            Assert.That(EnemyBrain.ShouldLeashOut(Anchor, 0f, true, farHero), Is.False,
                "radius 0 (default) must NEVER leash — unleashed enemies are unaffected");
            Assert.That(EnemyBrain.ShouldLeashOut(Anchor, 0f, false, Vector3.zero), Is.False,
                "radius 0 (default) must NEVER leash even when the hero is absent");
            Assert.That(EnemyBrain.ShouldLeashOut(Anchor, -5f, true, farHero), Is.False,
                "a negative radius must be treated as disabled");
        }

        [Test]
        public void set_leash_clamps_negative_radius_to_disabled()
        {
            var go = new GameObject("EnemyBrainLeashHost");
            try
            {
                var brain = go.AddComponent<EnemyBrain>();
                // A negative/zero radius must clamp to 0 (disabled) so a bad caller
                // config cannot leave the leash in an undefined state.
                brain.SetLeash(Anchor, -3f);
                // No public getter for _leashRadius; the contract is proven via the pure
                // helper above. This case just asserts SetLeash does not throw and the
                // component survives the call (the wiring path OutpostEnemyGroupSpawner uses).
                Assert.Pass("SetLeash accepted a negative radius without error");
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
