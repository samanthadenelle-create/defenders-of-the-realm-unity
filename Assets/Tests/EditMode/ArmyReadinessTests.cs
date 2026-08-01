// SCAFFOLD — CLI must build-verify under Unity Test Framework
// =============================================================================
// ArmyReadinessTests (EditMode) — the ONE army-readiness formula (owner review
// 2026-08-01), consolidated from the three WO-820 call sites into
// DeNelle.Village.ArmyReadiness.Compute.
// -----------------------------------------------------------------------------
// Behavioral, headless (no MonoBehaviour singletons — mirrors
// BarracksProgressionTests): drives Compute over a
// ScriptableObject.CreateInstance<GameState>() + a real ArmyStorage roster.
// In EditMode BuildTimerService.Instance is null, so
// BarracksService.CommittedTrainingSlots() is 0 — the queued-slot leg is
// covered via the public Compute(army, deployable, queued) seam overload
// with an injected queued count (see the class-tail note on why the
// EnqueueTraining charge loop itself is NOT driven here).
// =============================================================================

using NUnit.Framework;
using UnityEngine;
using DeNelle.Core.State;
using DeNelle.Village;

namespace DeNelle.Tests.EditMode
{
    [TestFixture]
    public class ArmyReadinessTests
    {
        private GameState _state;

        [SetUp]
        public void SetUp()
        {
            _state = ScriptableObject.CreateInstance<GameState>();
            _state.Army = new ArmyStorage();
        }

        [TearDown]
        public void TearDown()
        {
            if (_state != null) UnityEngine.Object.DestroyImmediate(_state);
        }

        // Roster helper: N healthy footmen (1 slot each — SlotOf falls back to 1
        // even if troops.json is absent, so the slot math is deterministic here).
        private void AddHealthy(int n)
        {
            for (int i = 0; i < n; i++)
                _state.Army.GrantTrained("troop-footman");
        }

        private void AddWounded(int n)
        {
            for (int i = 0; i < n; i++)
            {
                var t = _state.Army.GrantTrained("troop-footman");
                t.Wounded = true;   // wounded -> IsDeployable false -> excluded from GetDeployable
            }
        }

        [Test]
        public void empty_army_is_not_ready()
        {
            var s = ArmyReadiness.Compute(_state);

            Assert.That(s.CapSlots, Is.EqualTo(_state.Army.MaxArmySize), "cap mirrors Army.MaxArmySize");
            Assert.That(s.CapSlots, Is.GreaterThanOrEqualTo(ArmyStorage.DefaultMaxArmySize), "base cap is 10");
            Assert.That(s.DeployableSlots, Is.EqualTo(0), "empty roster has zero deployable slots");
            Assert.That(s.QueuedSlots, Is.EqualTo(0), "no BuildTimerService in EditMode -> zero queued");
            Assert.That(s.RosterSlots, Is.EqualTo(0));
            Assert.That(s.Ready, Is.False, "0 deployable + 0 queued never covers the cap");
        }

        [Test]
        public void a_full_healthy_roster_is_ready()
        {
            int cap = _state.Army.MaxArmySize;
            AddHealthy(cap);   // cap x 1-slot healthy troops

            var s = ArmyReadiness.Compute(_state);

            Assert.That(s.DeployableSlots, Is.EqualTo(cap), "every healthy troop counts");
            Assert.That(s.RosterSlots, Is.EqualTo(cap), "roster slots match (nobody wounded)");
            Assert.That(s.Ready, Is.True, "deployable alone covers the cap");
        }

        [Test]
        public void a_full_but_wounded_roster_is_not_ready()
        {
            int cap = _state.Army.MaxArmySize;
            AddWounded(cap);   // cap-filling roster, ALL wounded

            var s = ArmyReadiness.Compute(_state);

            Assert.That(s.DeployableSlots, Is.EqualTo(0), "wounded troops never count as deployable");
            Assert.That(s.RosterSlots, Is.EqualTo(cap), "but they DO occupy roster slots (SlotsUsed)");
            Assert.That(s.Ready, Is.False, "a cap-full roster of wounded is NOT raid-ready");
        }

        [Test]
        public void queued_training_slots_count_toward_readiness_via_the_seam()
        {
            // The queued leg through the SEAM overload (injected queued count — see the
            // class-tail note): 6 healthy + 4 in-flight covers cap 10; 6 + 3 does not.
            AddHealthy(6);
            int cap = _state.Army.MaxArmySize;
            int deployable = 6;

            var ready = ArmyReadiness.Compute(_state.Army, deployable, cap - deployable);
            Assert.That(ready.Ready, Is.True, "deployable + queued exactly covering the cap is ready");
            Assert.That(ready.QueuedSlots, Is.EqualTo(cap - deployable));

            var oneShort = ArmyReadiness.Compute(_state.Army, deployable, cap - deployable - 1);
            Assert.That(oneShort.Ready, Is.False, "one queued slot short of the cap is not ready");

            // RosterSlots (the EnqueueTraining seed) counts wounded; DeployableSlots does not.
            AddWounded(2);
            var withWounded = ArmyReadiness.Compute(_state.Army, deployable, 0);
            Assert.That(withWounded.RosterSlots, Is.EqualTo(8), "6 healthy + 2 wounded roster slots");
            Assert.That(withWounded.DeployableSlots, Is.EqualTo(deployable), "wounded excluded from deployable");
        }

        // NOTE — enqueue-past-cap CHARGING (review item d): BarracksService.EnqueueTraining's
        // per-unit spend-after-cap-check loop requires a live BuildTimerService.Instance
        // (a bootstrapped MonoBehaviour singleton) and the ResourceLedger wallet — neither
        // exists in EditMode, and building a fake service harness here would be fragile
        // (BarracksProgressionTests drives the PURE ObsidianQueueEngine instead for exactly
        // this reason). Per the work order, this suite therefore locks Compute's math with an
        // injected queued count (test above); the charge-loop semantics stay covered by the
        // headless runtime fleet, not EditMode.
    }
}
