using System;
using System.Collections.Generic;
using DeNelle.Core.Services;
using DeNelle.Core.Social;
using DeNelle.HUD;
using NUnit.Framework;

namespace DeNelle.Tests.EditMode
{
    [TestFixture]
    public sealed class TownShowcaseLeaderboardIntegrationTests
    {
        [Test]
        public void compatible_published_top_town_becomes_visit_affordance_on_matching_rank()
        {
            var scores = new Scores(false);
            var towns = new Towns();
            using var vm = new LeaderboardVM(scores, null, towns);
            towns.Complete(new TopTownVisitEntry
            {
                Rank = 1, Username = "Aldric", Score = 99,
                ShowcaseId = "sh_7Hy3qP9mN2xK4v8Q",
            });

            Assert.That(vm.Rows[0].CanVisit, Is.True);
            Assert.That(vm.Rows[0].ShowcaseId, Is.EqualTo("sh_7Hy3qP9mN2xK4v8Q"));
            Assert.That(vm.Rows[1].CanVisit, Is.False, "unpublished rank must have no affordance");
            Assert.That(vm.VisitEntries.Count, Is.EqualTo(1));
        }

        [Test]
        public void offline_or_non_wave_board_never_claims_that_placeholder_rivals_are_visitable()
        {
            var offlineTowns = new Towns();
            using (var offline = new LeaderboardVM(new Scores(true), null, offlineTowns))
                Assert.That(offlineTowns.Fetches, Is.Zero);

            var towns = new Towns();
            using var live = new LeaderboardVM(new Scores(false), null, towns);
            Assert.That(towns.Fetches, Is.EqualTo(1));
            live.SelectMetric(LeaderboardMetric.Crystals);
            Assert.That(live.Rows[0].CanVisit, Is.False);
            Assert.That(live.VisitEntries, Is.Empty);
            Assert.That(towns.Fetches, Is.EqualTo(1), "only Best Wave owns the Top-10 town directory");
        }

        [Test]
        public void late_top_town_response_cannot_leak_affordances_into_a_different_metric()
        {
            var towns = new Towns();
            using var vm = new LeaderboardVM(new Scores(false), null, towns);
            vm.SelectMetric(LeaderboardMetric.ArenaWins);
            towns.Complete(new TopTownVisitEntry { Rank = 1, ShowcaseId = "sh_7Hy3qP9mN2xK4v8Q" });
            Assert.That(vm.Rows[0].CanVisit, Is.False);
        }

        private sealed class Scores : LeaderboardVM.ISource
        {
            private readonly bool _stub;
            public Scores(bool stub) { _stub = stub; }
            public event Action Changed { add { } remove { } }
            public PlayerProfile GetLocalProfile() => new PlayerProfile { DisplayName = "You" };
            public void FetchTopAsync(LeaderboardMetric metric, int limit,
                Action<IReadOnlyList<LeaderboardEntry>> onResult) => onResult(new[]
            {
                new LeaderboardEntry { Rank = 1, Name = "Aldric", Score = 99 },
                new LeaderboardEntry { Rank = 2, Name = "Brynn", Score = 80 },
            });
            public bool IsLocalStub => _stub;
            public string SourceLabel => _stub ? "Local (offline)" : "Production";
        }

        private sealed class Towns : LeaderboardVM.IShowcaseSource
        {
            private Action<IReadOnlyList<TopTownVisitEntry>> _callback;
            public int Fetches { get; private set; }
            public void FetchTopTen(Action<IReadOnlyList<TopTownVisitEntry>> onResult)
            { Fetches++; _callback = onResult; }
            public void Complete(params TopTownVisitEntry[] entries) => _callback?.Invoke(entries);
        }
    }
}
