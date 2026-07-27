// =============================================================================
// TroopRecoveryTests (EditMode) — wounded-troop recovery wiring (WO-781).
// -----------------------------------------------------------------------------
// ArmyStorage.TickRecovery had ZERO callers, so wounded troops never healed. This
// fixture proves the wall-clock resolver ArmyStorage.AdvanceRecovery(nowMs) heals
// wounded troops FORWARD off a simulated clock (advancing nowMs = "ticking the
// TimeSource"), exactly the way ObsidianQueueTests drives the queue engine — headlessly,
// no MonoBehaviour / GameStateService / real clock needed.
//
// Acceptance (WO-781): a wounded troop whose recovery has elapsed becomes available; one
// whose recovery is still in the future stays wounded; advancing the clock past it
// recovers it. Plus the roster/veterancy no-double-resurrect guard, null/empty-army
// safety, the fresh-anchor retroactive-heal guard, and a reachability check that the
// live caller (DeNelle.Village.TroopRecoveryService) exists.
// =============================================================================

using NUnit.Framework;
using DeNelle.Core.State;

namespace DeNelle.Tests.EditMode
{
    [TestFixture]
    public class TroopRecoveryTests
    {
        private const double T0 = 1_000_000.0;   // a realistic wall-clock base (unix-ms)

        // Build an army with one wounded troop and its recovery clock seeded to T0.
        private static ArmyStorage WoundedArmy(float recoverySeconds, out PlayerTroop troop,
                                               string id = "troop-1", int veterancy = 0)
        {
            var army = new ArmyStorage();
            troop = new PlayerTroop(id, "troop-footman") { VeterancyRank = veterancy };
            army.Owned.Add(troop);
            army.AdvanceRecovery(T0);                 // seed the anchor to T0 (no heal — fresh anchor)
            army.MarkWounded(troop, recoverySeconds); // wound AFTER seeding → countdown vs a live anchor
            return army;
        }

        // =====================================================================
        //  Acceptance — recoverAt in the PAST → available again
        // =====================================================================
        [Test]
        public void wounded_with_elapsed_past_recovery_becomes_available()
        {
            var army = WoundedArmy(60f, out var troop);
            Assert.That(troop.Wounded, Is.True, "wounded to start");

            int recovered = army.AdvanceRecovery(T0 + 120_000.0);   // 120s elapsed > 60s recovery

            Assert.That(recovered, Is.EqualTo(1), "one troop recovered");
            Assert.That(troop.Wounded, Is.False, "recovery elapsed → healed");
            Assert.That(troop.IsDeployable, Is.True, "healed troop is deployable");
            Assert.That(troop.RecoveryRemaining, Is.EqualTo(0f), "countdown zeroed");
            CollectionAssert.Contains(new System.Collections.Generic.List<PlayerTroop>(army.GetDeployable()),
                                      troop, "back in the deployable roster");
        }

        // =====================================================================
        //  Acceptance — recoverAt in the FUTURE → still wounded
        // =====================================================================
        [Test]
        public void wounded_with_future_recovery_stays_wounded()
        {
            var army = WoundedArmy(300f, out var troop);

            int recovered = army.AdvanceRecovery(T0 + 60_000.0);    // 60s elapsed < 300s recovery

            Assert.That(recovered, Is.EqualTo(0), "nothing recovered yet");
            Assert.That(troop.Wounded, Is.True, "still recovering");
            Assert.That(troop.IsDeployable, Is.False, "not deployable while wounded");
            Assert.That(troop.RecoveryRemaining, Is.EqualTo(240f).Within(0.5f), "≈240s remaining");
        }

        // =====================================================================
        //  Acceptance — advance the clock PAST the remaining recovery → recovers
        // =====================================================================
        [Test]
        public void advancing_clock_past_recovery_then_recovers()
        {
            var army = WoundedArmy(300f, out var troop);

            Assert.That(army.AdvanceRecovery(T0 + 60_000.0), Is.EqualTo(0), "not yet at 60s");
            Assert.That(troop.Wounded, Is.True);

            int recovered = army.AdvanceRecovery(T0 + 60_000.0 + 300_000.0);   // +300s more → past

            Assert.That(recovered, Is.EqualTo(1), "recovers once the clock passes it");
            Assert.That(troop.Wounded, Is.False);
            Assert.That(troop.IsDeployable, Is.True);
        }

        // =====================================================================
        //  No double-resurrect — roster / OwnedTroopId / veterancy untouched + idempotent
        // =====================================================================
        [Test]
        public void recovery_preserves_id_and_veterancy_and_is_idempotent()
        {
            var army = WoundedArmy(60f, out var troop, id: "troop-7", veterancy: 3);

            army.AdvanceRecovery(T0 + 120_000.0);   // heal it

            Assert.That(army.Owned.Count, Is.EqualTo(1), "no troop added/removed (wounded-recovery, not permadeath)");
            Assert.That(troop.Id, Is.EqualTo("troop-7"), "stable OwnedTroopId preserved through recovery");
            Assert.That(troop.VeterancyRank, Is.EqualTo(3), "veterancy untouched by recovery");
            Assert.That(troop.Wounded, Is.False);

            // A second advance heals nothing more (already healthy) and mutates nothing — idempotent.
            int again = army.AdvanceRecovery(T0 + 999_000.0);
            Assert.That(again, Is.EqualTo(0), "no re-resurrect of an already-healthy troop");
            Assert.That(army.Owned.Count, Is.EqualTo(1));
            Assert.That(troop.VeterancyRank, Is.EqualTo(3));
        }

        // =====================================================================
        //  Fresh-anchor guard — a pre-anchor save can't bank a giant retroactive heal
        // =====================================================================
        [Test]
        public void fresh_anchor_seeds_without_retroactive_heal()
        {
            var army = new ArmyStorage();
            var troop = new PlayerTroop("troop-1", "troop-footman");
            army.Owned.Add(troop);
            army.MarkWounded(troop, 300f);   // wounded; anchor is still 0 (an old/pre-WO-779 save)

            // First advance with a far-future clock: anchor <= 0 → SEED to now, tick nothing.
            int recovered = army.AdvanceRecovery(T0 + 10_000_000.0);

            Assert.That(recovered, Is.EqualTo(0), "fresh anchor credits nothing (no retroactive over-heal)");
            Assert.That(troop.Wounded, Is.True, "still wounded — recovery advances only from now forward");
            Assert.That(army.LastRecoveryTickMs, Is.EqualTo(T0 + 10_000_000.0), "anchor seeded to now");
        }

        // =====================================================================
        //  Null / empty-army safety + backwards clock
        // =====================================================================
        [Test]
        public void empty_null_army_and_backwards_clock_are_safe()
        {
            var empty = new ArmyStorage();
            Assert.That(empty.AdvanceRecovery(T0), Is.EqualTo(0), "empty army: seed only");
            Assert.That(empty.AdvanceRecovery(T0 + 5_000.0), Is.EqualTo(0), "empty army: nothing to heal");

            var nullArmy = new ArmyStorage { Owned = null };
            Assert.DoesNotThrow(() => nullArmy.AdvanceRecovery(T0), "null roster must not throw");
            Assert.DoesNotThrow(() => nullArmy.AdvanceRecovery(T0 + 5_000.0));

            // Backwards clock (device time rewound) must never re-heal.
            var army = WoundedArmy(60f, out var troop);
            int recovered = army.AdvanceRecovery(T0 - 999_000.0);
            Assert.That(recovered, Is.EqualTo(0), "backwards clock heals nothing");
            Assert.That(troop.Wounded, Is.True, "still wounded after a rewound clock");
        }

        // =====================================================================
        //  Pure step — TickRecovery(dt) decrements and reports the heal count
        // =====================================================================
        [Test]
        public void tick_recovery_pure_step_counts_and_clears()
        {
            var army = new ArmyStorage();
            var a = new PlayerTroop("troop-1", "troop-footman");
            var b = new PlayerTroop("troop-2", "troop-archer");
            army.Owned.Add(a);
            army.Owned.Add(b);
            army.MarkWounded(a, 10f);
            army.MarkWounded(b, 100f);

            Assert.That(army.TickRecovery(0f), Is.EqualTo(0), "dt<=0 is a no-op");
            Assert.That(army.TickRecovery(20f), Is.EqualTo(1), "only 'a' (10s) recovered at dt=20s");
            Assert.That(a.Wounded, Is.False);
            Assert.That(b.Wounded, Is.True, "'b' (100s) still recovering");
            Assert.That(army.TickRecovery(100f), Is.EqualTo(1), "'b' recovers at the next big step");
        }

        // =====================================================================
        //  Reachability — the LIVE CALLER exists (the zero-caller gap is closed).
        //  Referencing the Village hook type compiles ONLY if the service is present;
        //  deleting it would fail this test at compile time (like the queue-toggle gap).
        // =====================================================================
        [Test]
        public void recovery_advance_has_a_live_caller_service()
        {
            var t = typeof(DeNelle.Village.TroopRecoveryService);
            Assert.That(t, Is.Not.Null, "TroopRecoveryService exists");
            Assert.That(typeof(UnityEngine.MonoBehaviour).IsAssignableFrom(t),
                        Is.True, "it is a MonoBehaviour that ticks AdvanceRecovery (Start/resume/Update)");
        }
    }
}
