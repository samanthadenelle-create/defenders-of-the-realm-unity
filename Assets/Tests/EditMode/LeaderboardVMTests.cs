// =============================================================================
// LeaderboardVMTests (EditMode) — §2c gate for the leaderboard MVVM slice.
// -----------------------------------------------------------------------------
// Locks the profile/row projection + the async fetch MOVED out of LeaderboardPanel
// into the pure LeaderboardVM. FAKE ISource — no scene, no LeaderboardService.
// =============================================================================

using System;
using System.Collections.Generic;
using NUnit.Framework;
using DeNelle.Core.Services;
using DeNelle.HUD;

namespace DeNelle.Tests.EditMode
{
    [TestFixture]
    public class LeaderboardVMTests
    {
        private sealed class FakeSource : LeaderboardVM.ISource
        {
            public PlayerProfile Profile;
            public readonly List<LeaderboardEntry> Entries = new List<LeaderboardEntry>();
            public bool Stub = true;
            public string Label = "Local (offline)";
            public LeaderboardMetric LastMetric;
            public int Fetches;

            public event Action Changed;
            public PlayerProfile GetLocalProfile() => Profile;
            public void FetchTopAsync(LeaderboardMetric m, int limit, Action<IReadOnlyList<LeaderboardEntry>> cb)
            { LastMetric = m; Fetches++; cb?.Invoke(Entries); }
            public bool IsLocalStub => Stub;
            public string SourceLabel => Label;
            public void RaiseChanged() => Changed?.Invoke();
        }

        private static FakeSource Populated()
        {
            var s = new FakeSource
            {
                Profile = new PlayerProfile
                {
                    DisplayName = "You", HeroClass = "Ranger", InviteCode = "ABC",
                    BestWave = 12, Crystals = 340, Magic = 8, ArenaWins = 3, ArenaLosses = 1,
                }
            };
            s.Entries.Add(new LeaderboardEntry { Rank = 1, Name = "Aldric", Score = 540, IsLocalPlayer = false });
            s.Entries.Add(new LeaderboardEntry { Rank = 2, Name = "You", Score = 400, IsLocalPlayer = true });
            return s;
        }

        [Test]
        public void profile_and_rows_project_and_initial_fetch_runs()
        {
            var src = Populated();
            using var vm = new LeaderboardVM(src, null);

            Assert.That(vm.ProfileHeroLine, Is.EqualTo("You - Ranger   #ABC"));
            Assert.That(vm.ProfileStatsLine, Does.Contain("Best Wave 12"));
            Assert.That(vm.ProfileStatsLine, Does.Contain("Arena 3-1"));

            Assert.That(vm.Rows.Count, Is.EqualTo(2));
            Assert.That(vm.Rows[0].Rank, Is.EqualTo("1"));
            Assert.That(vm.Rows[0].Name, Is.EqualTo("Aldric"));
            Assert.That(vm.Rows[1].IsLocal, Is.True);
            Assert.That(vm.Rows[1].Score, Is.EqualTo("400"));

            Assert.That(src.Fetches, Is.GreaterThanOrEqualTo(1), "ctor Refresh must fetch");
            Assert.That(src.LastMetric, Is.EqualTo(LeaderboardMetric.BestWave));
            Assert.That(vm.FooterText, Does.Contain("Local (offline)"));
        }

        [Test]
        public void empty_rows_show_the_placeholder_row()
        {
            var src = Populated();
            src.Entries.Clear();
            using var vm = new LeaderboardVM(src, null);
            Assert.That(vm.Rows.Count, Is.EqualTo(1));
            Assert.That(vm.Rows[0].Name, Is.EqualTo("No entries yet."));
        }

        [Test]
        public void select_metric_refetches_and_raises_changed()
        {
            var src = Populated();
            using var vm = new LeaderboardVM(src, null);
            int changed = 0;
            vm.Changed += () => changed++;
            int before = src.Fetches;

            vm.SelectMetric(LeaderboardMetric.Crystals);
            Assert.That(vm.Metric, Is.EqualTo(LeaderboardMetric.Crystals));
            Assert.That(src.LastMetric, Is.EqualTo(LeaderboardMetric.Crystals));
            Assert.That(src.Fetches, Is.EqualTo(before + 1));
            Assert.That(changed, Is.GreaterThan(0));
        }

        [Test]
        public void hero_line_omits_class_when_none()
        {
            var src = Populated();
            src.Profile.HeroClass = "None";
            src.Profile.InviteCode = "";
            using var vm = new LeaderboardVM(src, null);
            Assert.That(vm.ProfileHeroLine, Is.EqualTo("You"));
        }

        [Test]
        public void footer_is_honest_for_stub_vs_live()
        {
            var stub = Populated(); stub.Stub = true;
            using (var vm = new LeaderboardVM(stub, null))
                Assert.That(vm.FooterText, Does.Contain("placeholder rivals"));

            var live = Populated(); live.Stub = false;
            using (var vm = new LeaderboardVM(live, null))
                Assert.That(vm.FooterText, Does.Not.Contain("placeholder rivals"));
        }

        [Test]
        public void changed_fires_on_source_change_and_stops_after_dispose()
        {
            var src = Populated();
            var vm = new LeaderboardVM(src, null);
            int changed = 0;
            vm.Changed += () => changed++;

            src.RaiseChanged();
            Assert.That(changed, Is.GreaterThan(0));

            int before = changed;
            vm.Dispose();
            src.RaiseChanged();
            Assert.That(changed, Is.EqualTo(before));
        }
    }
}
