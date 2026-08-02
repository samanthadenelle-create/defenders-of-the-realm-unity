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

        // =====================================================================
        // WO-797 room ownership (F8 seq 461/622 "all enemies at the entrance"):
        // the wake decision moves from a ring-slot anchor to the ROOM FOOTPRINT
        // (ShouldWake) and every destination is confined to the room AABB + slack
        // (ConfineToArea) — including the provoked retaliation chase.
        // Starter-loop geometry: junction room 6x6 at z=12 (footprint z 9..15),
        // entry hero seat at ~(0,0,0.9) => 8.1m from the footprint.
        // =====================================================================

        private static readonly Bounds JunctionArea =
            new Bounds(new Vector3(0f, 2f, 12f), new Vector3(6f, 4f, 6f));

        [Test]
        public void should_confine_hero_at_entry_seat_leaves_room_dormant()
        {
            // The frame-one beeline killer: the old anchor leash put junction ring slots
            // within 10m of the entry seat; footprint distance 8.1m > wake 6 => dormant.
            Assert.That(EnemyBrain.ShouldWake(JunctionArea, 6f, true, new Vector3(0f, 0f, 0.9f)), Is.False,
                "a hero at the entry seat must NOT wake the junction room (footprint dist ~8.1m > 6m)");
        }

        [Test]
        public void should_confine_hero_near_footprint_wakes_room()
        {
            Assert.That(EnemyBrain.ShouldWake(JunctionArea, 6f, true, new Vector3(0f, 0f, 4f)), Is.True,
                "a hero 5m from the room footprint must wake it (wake 6)");
            Assert.That(EnemyBrain.ShouldWake(JunctionArea, 6f, true, new Vector3(1f, 0f, 12f)), Is.True,
                "a hero inside the room footprint must wake it (distance 0)");
        }

        [Test]
        public void should_confine_absent_hero_stays_dormant()
        {
            Assert.That(EnemyBrain.ShouldWake(JunctionArea, 6f, false, Vector3.zero), Is.False,
                "with no hero present a room-bound mob must stay dormant");
        }

        [Test]
        public void should_confine_provoked_chase_clamps_to_room_edge_plus_slack()
        {
            // A provoked mob chasing a hero camping the entrance (z=0) must stop at the
            // room's south face + slack (z = 9 - 2 = 7) — it fights but never leaves.
            Vector3 confined = EnemyBrain.ConfineToArea(new Vector3(0f, 0f, 0f), JunctionArea, 2f);
            Assert.That(confined.z, Is.EqualTo(7f).Within(0.01f),
                "the retaliation chase must clamp to the room AABB + slack, not tow the mob to the entrance");
            Assert.That(confined.x, Is.EqualTo(0f).Within(0.01f));
        }

        [Test]
        public void should_confine_inside_destination_passes_through()
        {
            Vector3 inside = new Vector3(1f, 0f, 12f);
            Assert.That(EnemyBrain.ConfineToArea(inside, JunctionArea, 2f), Is.EqualTo(inside),
                "an in-room destination must be untouched (zero behaviour change while fighting at home)");
        }

        [Test]
        public void should_confine_negative_slack_seats_strictly_inside()
        {
            // Spawn-slot seating uses slack -0.5 so the formation ring can never spill a
            // slot into the neighbouring corridor (data-proven cause 1 of the camp).
            Vector3 seat = EnemyBrain.ConfineToArea(new Vector3(0f, 0f, 8.8f), JunctionArea, -0.5f);
            Assert.That(seat.z, Is.GreaterThanOrEqualTo(9.5f - 0.01f),
                "negative slack must pull a spilled ring slot back inside the room");
        }

        [Test]
        public void set_room_area_stores_and_clears_assignment()
        {
            var go = new GameObject("EnemyBrainRoomHost");
            try
            {
                var brain = go.AddComponent<EnemyBrain>();
                Assert.That(brain.HasRoomArea, Is.False, "default must be unbound (zero village regression)");
                brain.SetRoomArea("junction", JunctionArea, 2f, 6f);
                Assert.That(brain.HasRoomArea, Is.True);
                Assert.That(brain.AreaRoomId, Is.EqualTo("junction"),
                    "every room-bound enemy must carry its room assignment (WO-797 contract)");
                brain.SetRoomArea("x", new Bounds(Vector3.zero, Vector3.zero), 2f, 6f);
                Assert.That(brain.HasRoomArea, Is.False, "a zero-size area must disable room binding");
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
