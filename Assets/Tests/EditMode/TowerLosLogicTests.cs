// =============================================================================
// TowerLosLogicTests (EditMode) — locks the unit-testable slices of the tower
// "shoot through walls" LoS fix (TowerCombat.cs :194-208, TowerData.cs :80-88).
// -----------------------------------------------------------------------------
// The full fix has two halves (see the [tower-wall-los] source-lint,
// Assets/Editor/Regression/TowerWallLosRegression.cs):
//   FIX 1 — wall spawners put walls on the "Structure" physics layer,
//   FIX 2 — towers reject shots blocked by a Structure-mask Physics.Linecast,
//           WITH a flyer exemption (a ground wall must not silence the tower vs.
//           the apex dragon — the "can't see dragon, too high" F8 fix).
//
// EDITMODE-TESTABLE (asserted here, real behaviour):
//   - the air/ground targeting matrix (TowerData.CanTarget) — the flyer/ground
//     rock-paper-scissors the acquire loop gates on,
//   - the "Structure" LayerMask the LoS gate depends on actually resolves to a
//     non-zero mask (if the layer were undefined the gate degrades OPEN = towers
//     shoot through walls again — this catches a project-config regression),
//   - the flyer-exemption BRANCH of BlockedByWall (a Flying target is NEVER
//     blocked, returns before any physics) + a null target is always "blocked".
//
// PLAYMODE-ONLY (NOT covered here — scheduled follow): the POSITIVE case "a wall
// collider on the Structure layer between fire-point and a GROUND target blocks
// the shot" runs Physics.Linecast against live colliders, which needs a PlayMode
// scene with a baked physics world. Interim floor = the [tower-wall-los]
// source-lint (asserts the Linecast + Structure-mask + BlockedByWall gate exist
// in DefenseTower/ArcaneTower/TowerCombat source). A PlayMode test under
// Assets/Tests/PlayMode should assert the blocked-vs-clear linecast outcome.
// =============================================================================

using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using DeNelle.Core.Combat;
using DeNelle.Core.Data;
using DeNelle.Village;

namespace DeNelle.Tests.EditMode
{
    // A minimal cross-module combat target that declares an air/ground layer —
    // used to exercise the flyer-exemption branch without a live enemy.
    internal sealed class FakeLayeredTarget : IDamageable, ICombatLayered
    {
        public CombatLayer Layer { get; set; }
        public Vector3 WorldPosition { get; set; }
        public CombatFaction Faction => CombatFaction.Hostile;
        public float Hp => 100f;
        public bool IsAlive => true;
        public void TakeDamage(float amount, DamageElement element) { }
        public void ApplyStatus(StatusEffect effect, float seconds) { }
    }

    [TestFixture]
    public class TowerLosLogicTests
    {
        // ── FIX 2 companion — the air/ground targeting matrix (pure static) ──────

        [Test]
        public void ground_tower_hits_ground_only()
        {
            Assert.That(TowerData.CanTarget(TowerTargets.Ground, CombatLayer.Ground), Is.True);
            Assert.That(TowerData.CanTarget(TowerTargets.Ground, CombatLayer.Flying), Is.False,
                "a ground tower must NOT acquire a flyer");
        }

        [Test]
        public void air_tower_hits_flying_only()
        {
            Assert.That(TowerData.CanTarget(TowerTargets.Air, CombatLayer.Flying), Is.True);
            Assert.That(TowerData.CanTarget(TowerTargets.Air, CombatLayer.Ground), Is.False,
                "an anti-air tower must NOT acquire a ground creep");
        }

        [Test]
        public void both_tower_hits_everything()
        {
            Assert.That(TowerData.CanTarget(TowerTargets.Both, CombatLayer.Ground), Is.True);
            Assert.That(TowerData.CanTarget(TowerTargets.Both, CombatLayer.Flying), Is.True);
        }

        // ── FIX 1/2 dependency — the "Structure" LoS mask must resolve ──────────

        [Test]
        public void structure_layer_resolves_to_a_nonzero_mask()
        {
            // The LoS gate degrades OPEN (never blocks) when the Structure layer is
            // absent (mask 0). If this fails, walls fall back to Default and towers
            // shoot through them — the exact bug the fix closed.
            int mask = LayerMask.GetMask("Structure");
            Assert.That(mask, Is.Not.EqualTo(0),
                "the \"Structure\" layer must be defined (GetMask != 0) or the wall-LoS gate is inert");
            Assert.That(LayerMask.NameToLayer("Structure"), Is.GreaterThanOrEqualTo(0),
                "\"Structure\" must be a real named layer the wall spawners can assign");
        }

        // ── FIX 2 — the flyer-exemption branch of BlockedByWall ─────────────────

        private static bool InvokeBlockedByWall(TowerCombat combat, IDamageable target)
        {
            var mi = typeof(TowerCombat).GetMethod(
                "BlockedByWall", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(mi, Is.Not.Null,
                "TowerCombat.BlockedByWall(IDamageable) must exist — it is the wall-LoS gate");
            return (bool)mi.Invoke(combat, new object[] { target });
        }

        [Test]
        public void flyer_is_exempt_from_the_wall_los_gate()
        {
            var go = new GameObject("TowerCombatTestHost");
            try
            {
                var combat = go.AddComponent<TowerCombat>();
                var flyer = new FakeLayeredTarget
                {
                    Layer = CombatLayer.Flying,
                    WorldPosition = new Vector3(0f, 30f, 20f)
                };
                // The flyer branch returns false BEFORE any physics — a ground wall on the
                // Structure layer must never silence a tower against the apex dragon.
                Assert.That(InvokeBlockedByWall(combat, flyer), Is.False,
                    "a Flying target must be EXEMPT from the wall-LoS gate (never blocked)");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void null_target_is_always_blocked()
        {
            var go = new GameObject("TowerCombatTestHost");
            try
            {
                var combat = go.AddComponent<TowerCombat>();
                Assert.That(InvokeBlockedByWall(combat, null), Is.True,
                    "a null target has no line of sight and must be treated as blocked");
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
