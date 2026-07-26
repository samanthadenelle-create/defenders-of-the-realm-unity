// =============================================================================
// ZoneManagerCastleSafeTests (EditMode) — locks the 2026-07-26 felt-fix
// "enemies spawn inside the castle during the tutorial" (ZoneManager.cs :29-42).
// -----------------------------------------------------------------------------
// The safe home-zone half-extents were widened from 42/33 to 52/52 so the whole
// merged Main_Castle_Overworld castle (walls ±44, cardinal gates ±50) classifies
// as the safe Village zone. These tests assert the ACTUAL classifier behaviour:
//   - courtyard / wall-band / gate-threshold points classify as RegionId.Village,
//   - a Village classification carries NO enemy roster (HasRoster == false), so
//     OverworldEncounterSpawner never anchors reps on castle-interior navmesh,
//   - the exact points that the OLD 42/33 box mis-classified as OUTER regions
//     (which lifted the FTUE peace window + spawned enemies inside) are now safe,
//   - Village threat level is 0 (no scaling inside the walls).
// Pure DeNelle.Core logic — no scene, headless-safe.
// =============================================================================

using NUnit.Framework;
using UnityEngine;
using DeNelle.Core.World;

namespace DeNelle.Tests.EditMode
{
    [TestFixture]
    public class ZoneManagerCastleSafeTests
    {
        [Test]
        public void castle_courtyard_point_classifies_as_village()
        {
            // Northern courtyard, |z| = 40 (inside walls ±44). Old box was ±33 Z -> Ashwood.
            Assert.That(ZoneManager.GetZone(new Vector3(0f, 0f, 40f)),
                Is.EqualTo(RegionId.Village),
                "(0,0,40) is inside the castle courtyard and must be the safe Village zone");
        }

        [Test]
        public void castle_wall_band_point_classifies_as_village()
        {
            // Eastern wall band, |x| = 43 (inside walls ±44). Old box was ±42 X -> Goldfields.
            Assert.That(ZoneManager.GetZone(new Vector3(43f, 0f, 0f)),
                Is.EqualTo(RegionId.Village),
                "(43,0,0) sits on the castle wall band and must be the safe Village zone");
        }

        [Test]
        public void castle_walls_and_gate_threshold_are_safe()
        {
            // Wall corner (±44) and the cardinal gate threshold (±50) both fall inside 52.
            Assert.That(ZoneManager.GetZone(new Vector3(44f, 0f, 44f)),
                Is.EqualTo(RegionId.Village), "wall corner (44,44) must be Village");
            Assert.That(ZoneManager.GetZone(new Vector3(0f, 0f, 50f)),
                Is.EqualTo(RegionId.Village), "north gate threshold (0,50) must be Village");
            Assert.That(ZoneManager.GetZone(new Vector3(50f, 0f, 0f)),
                Is.EqualTo(RegionId.Village), "east gate threshold (50,0) must be Village");
        }

        [Test]
        public void old_box_misclassified_points_are_now_village()
        {
            // These are the exact geometry regressions the fix targets: points that the
            // RETIRED 42/33 box classified as OUTER regions (HasRoster=true -> spawns).
            // z 33..52 (old Z half was 33) and x 42..52 (old X half was 42).
            Assert.That(ZoneManager.GetZone(new Vector3(0f, 0f, 34f)),
                Is.EqualTo(RegionId.Village), "z=34 was Ashwood under the old 33u Z box");
            Assert.That(ZoneManager.GetZone(new Vector3(42.5f, 0f, 0f)),
                Is.EqualTo(RegionId.Village), "x=42.5 was Goldfields under the old 42u X box");
        }

        [Test]
        public void village_classification_carries_no_enemy_roster()
        {
            // The full spawn chain: castle point -> Village zone -> no roster -> no spawn.
            // OverworldEncounterSpawner + the FTUE peace window both read HasRoster.
            RegionId z1 = ZoneManager.GetZone(new Vector3(0f, 0f, 40f));
            RegionId z2 = ZoneManager.GetZone(new Vector3(43f, 0f, 0f));
            Assert.That(RegionSpawnTable.HasRoster(z1), Is.False,
                "a castle-courtyard point must have NO enemy roster (no spawns inside the castle)");
            Assert.That(RegionSpawnTable.HasRoster(z2), Is.False,
                "a wall-band point must have NO enemy roster (no spawns inside the castle)");
        }

        [Test]
        public void village_threat_level_and_depth_are_zero()
        {
            Assert.That(ZoneManager.ThreatLevel(new Vector3(0f, 0f, 40f)), Is.EqualTo(0),
                "Village (safe home zone) never scales enemy level");
            Assert.That(ZoneManager.Depth(new Vector3(43f, 0f, 0f)), Is.EqualTo(0f),
                "Depth inside the safe box is 0");
        }

        [Test]
        public void points_well_past_the_gates_still_classify_as_outer_regions()
        {
            // Sanity floor: the fix reclaimed only the castle interior. The outer world
            // (past the moat, |axis| > 52) must still fan out by cardinal direction, or
            // we would have made the ENTIRE map safe.
            Assert.That(ZoneManager.GetZone(new Vector3(0f, 0f, 120f)),
                Is.EqualTo(RegionId.Ashwood), "far north is still Ashwood");
            Assert.That(ZoneManager.GetZone(new Vector3(120f, 0f, 0f)),
                Is.EqualTo(RegionId.Goldfields), "far east is still Goldfields");
            Assert.That(ZoneManager.GetZone(new Vector3(-120f, 0f, 0f)),
                Is.EqualTo(RegionId.Stoneback), "far west is still Stoneback");
            Assert.That(ZoneManager.GetZone(new Vector3(0f, 0f, -120f)),
                Is.EqualTo(RegionId.Mirewood), "far south is still Mirewood");
        }
    }
}
