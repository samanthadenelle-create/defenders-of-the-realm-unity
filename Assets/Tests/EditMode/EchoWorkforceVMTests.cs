// =============================================================================
// EchoWorkforceVMTests (EditMode) -- §2c lock for the shared Echo workforce VM.
// -----------------------------------------------------------------------------
// Over a fake IEchoWorkforce (no scene / no GameState): asserts the count / silo /
// ETA / harvest-perk projection branches, the Collect-All command mutation + Changed,
// and the first-run / empty / roster-complete edges. Mirrors EconomyServiceTests.
// =============================================================================
using System;
using NUnit.Framework;
using DeNelle.Village;

namespace DeNelle.Tests.EditMode
{
    /// <summary>Fake workforce model with settable snapshot values + manual event raises.</summary>
    internal sealed class FakeEchoWorkforce : IEchoWorkforce, IEchoHarvestBonusReadout
    {
        public bool Available { get; set; } = true;
        public int EchoCount { get; set; } = 1;
        public int MaxEchoes { get; set; } = 6;
        public int WavesPerEcho { get; set; } = 5;
        public int WavesUntilNextEcho { get; set; } = 3;
        public float NextEchoProgress { get; set; } = 0.4f;
        public double GlobalHarvestMultiplier { get; set; } = 1.0;
        public double HarvestTogetherBonusPct { get; set; }
        public float FillFraction { get; set; } = 0.5f;
        public int PendingCollect { get; set; } = 0;
        public float CollectorMaxFill { get; set; } = 0f;

        public int CollectAllReturn = 42;
        public int CollectAllCalls;
        public int CollectAll() { CollectAllCalls++; return CollectAllReturn; }

        public event Action Changed;
        public event Action<int> EchoUnlocked;
        public void RaiseChanged() => Changed?.Invoke();
        public void RaiseUnlocked(int n) => EchoUnlocked?.Invoke(n);
    }

    [TestFixture]
    public class EchoWorkforceVMTests
    {
        private static EchoWorkforceVM Vm(FakeEchoWorkforce f) => new EchoWorkforceVM(f, null);

        [Test]
        public void hud_count_line_projects_owned_over_max()
        {
            var f = new FakeEchoWorkforce { EchoCount = 2, MaxEchoes = 6 };
            Assert.That(Vm(f).HudCountLine, Is.EqualTo("Echoes  2/6"));
        }

        [Test]
        public void hud_silo_line_projects_pending_and_percents()
        {
            var f = new FakeEchoWorkforce { PendingCollect = 7, FillFraction = 0.5f, CollectorMaxFill = 0.25f };
            Assert.That(Vm(f).HudSiloLine, Is.EqualTo("Pending  7   Echo 50%   Collectors 25%"));
        }

        [Test]
        public void eta_reads_roster_complete_at_cap()
        {
            var f = new FakeEchoWorkforce { EchoCount = 6, MaxEchoes = 6 };
            var vm = Vm(f);
            Assert.That(vm.RosterComplete, Is.True);
            Assert.That(vm.RosterEtaText, Is.EqualTo("Echoes 6/6   -   Roster complete!"));
        }

        [Test]
        public void eta_reads_first_run_invite_at_one()
        {
            var f = new FakeEchoWorkforce { EchoCount = 1, MaxEchoes = 6, WavesUntilNextEcho = 3 };
            var vm = Vm(f);
            Assert.That(vm.FirstRun, Is.True);
            Assert.That(vm.RosterEtaText, Is.EqualTo("Echoes 1/6   -   3 more waves to your next spirit"));
        }

        [Test]
        public void eta_reads_normal_countdown_past_first_run()
        {
            var f = new FakeEchoWorkforce { EchoCount = 3, MaxEchoes = 6, WavesUntilNextEcho = 2 };
            var vm = Vm(f);
            Assert.That(vm.FirstRun, Is.False);
            Assert.That(vm.RosterEtaText, Is.EqualTo("Echoes 3/6   -   Next Echo in 2 waves"));
        }

        [Test]
        public void waves_to_next_falls_back_to_cadence_when_zero()
        {
            var f = new FakeEchoWorkforce { EchoCount = 3, WavesUntilNextEcho = 0, WavesPerEcho = 5 };
            Assert.That(Vm(f).WavesToNext, Is.EqualTo(5), "0 waves-until-next falls back to the per-echo cadence");
        }

        [Test]
        public void harvest_perk_line_hidden_when_empty_shown_otherwise()
        {
            var empty = new FakeEchoWorkforce { EchoCount = 0 };
            Assert.That(Vm(empty).HarvestPerkLine, Is.Null);

            var owned = new FakeEchoWorkforce
            {
                EchoCount = 2,
                MaxEchoes = 6,
                HarvestTogetherBonusPct = 17
            };
            Assert.That(Vm(owned).HarvestPerkLine,
                Is.EqualTo("Echoes 2/6 - harvest +17% together"));
        }

        [Test]
        public void empty_first_run_flags_track_owned_count()
        {
            Assert.That(Vm(new FakeEchoWorkforce { EchoCount = 0 }).Empty, Is.True);
            Assert.That(Vm(new FakeEchoWorkforce { EchoCount = 1 }).FirstRun, Is.True);
            Assert.That(Vm(new FakeEchoWorkforce { EchoCount = 2 }).FirstRun, Is.False);
        }

        [Test]
        public void collect_all_command_returns_banked_and_raises_changed()
        {
            var f = new FakeEchoWorkforce { CollectAllReturn = 99 };
            var vm = Vm(f);
            int fires = 0; vm.Changed += () => fires++;

            int banked = vm.CollectAll();

            Assert.That(banked, Is.EqualTo(99));
            Assert.That(f.CollectAllCalls, Is.EqualTo(1));
            Assert.That(fires, Is.EqualTo(1), "CollectAll must raise Changed once");
        }

        [Test]
        public void model_changed_re_snapshots_and_raises_changed()
        {
            var f = new FakeEchoWorkforce { EchoCount = 1, MaxEchoes = 6 };
            var vm = Vm(f);
            int fires = 0; vm.Changed += () => fires++;

            f.EchoCount = 4;
            f.RaiseChanged();

            Assert.That(fires, Is.EqualTo(1), "model Changed re-raises VM Changed");
            Assert.That(vm.HudCountLine, Is.EqualTo("Echoes  4/6"), "snapshot re-read on Changed");
        }

        [Test]
        public void echo_unlocked_is_re_raised()
        {
            var f = new FakeEchoWorkforce();
            var vm = Vm(f);
            int got = -1; vm.EchoUnlocked += n => got = n;
            f.RaiseUnlocked(3);
            Assert.That(got, Is.EqualTo(3));
        }

        [Test]
        public void has_workforce_tracks_availability()
        {
            Assert.That(Vm(new FakeEchoWorkforce { Available = true }).HasWorkforce, Is.True);
            Assert.That(Vm(new FakeEchoWorkforce { Available = false }).HasWorkforce, Is.False);
        }
    }
}
