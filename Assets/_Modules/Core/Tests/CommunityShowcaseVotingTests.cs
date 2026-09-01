using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using DeNelle.Core.Entitlements;
using DeNelle.Core.Social;

namespace DeNelle.Core.Tests
{
    public sealed class CommunityShowcaseVotingTests
    {
        private sealed class FakeTransport : ICommunityShowcaseTransport
        {
            public int DiscoverCalls, CountCalls, VoteCalls;
            public ShowcaseTransportResult Discovery;
            public ShowcaseTransportResult Counts;
            public ShowcaseTransportResult Vote;
            public UniTask<ShowcaseTransportResult> DiscoverAsync(string playerId, string contestId, string categoryId)
            { DiscoverCalls++; return UniTask.FromResult(Discovery); }
            public UniTask<ShowcaseTransportResult> GetCountsAsync(string contestId, string categoryId)
            { CountCalls++; return UniTask.FromResult(Counts); }
            public UniTask<ShowcaseTransportResult> CastVoteAsync(
                string playerId, string contestId, string categoryId, string showcaseId)
            { VoteCalls++; return UniTask.FromResult(Vote); }
        }

        [Test]
        public async System.Threading.Tasks.Task production_gate_is_default_off_and_never_calls_transport()
        {
            var transport = new FakeTransport();
            var service = new CommunityShowcaseVotingService(transport);
            var vote = await service.CastVoteAsync("player", "season_1", "best_realm", "sh_1234567890123456");
            var discovery = await service.DiscoverAsync("player", "season_1", "best_realm");
            var counts = await service.FetchCountsAsync("season_1", "best_realm");
            Assert.That(CommunityShowcaseVotingFeature.Enabled, Is.False);
            Assert.That(vote.State, Is.EqualTo(ShowcaseVoteState.Unavailable));
            Assert.That(counts, Is.Empty);
            Assert.That(discovery, Is.Empty);
            Assert.That(transport.VoteCalls + transport.DiscoverCalls + transport.CountCalls, Is.Zero);
        }

        [Test]
        public async System.Threading.Tasks.Task enabled_seam_rejects_anonymous_vote_before_transport()
        {
            var transport = new FakeTransport();
            var service = new CommunityShowcaseVotingService(transport, true);
            var result = await service.CastVoteAsync(" ", "season_1", "best_realm", "sh_1234567890123456");
            Assert.That(result.Error, Is.EqualTo("AUTH_REQUIRED"));
            Assert.That(transport.VoteCalls, Is.Zero);
        }

        [Test]
        public async System.Threading.Tasks.Task counts_are_clamped_and_sorted_deterministically()
        {
            var transport = new FakeTransport { Counts = new ShowcaseTransportResult(true, 200,
                "{\"success\":true,\"contestId\":\"season_1\",\"categoryId\":\"best_realm\",\"candidates\":[" +
                "{\"showcaseId\":\"sh_bbbbbbbbbbbbbbbb\",\"votes\":4}," +
                "{\"showcaseId\":\"sh_aaaaaaaaaaaaaaaa\",\"votes\":4}," +
                "{\"showcaseId\":\"sh_cccccccccccccccc\",\"votes\":-9}]}" ) };
            var service = new CommunityShowcaseVotingService(transport, true);
            var rows = await service.FetchCountsAsync("season_1", "best_realm");
            Assert.That(rows.Count, Is.EqualTo(3));
            Assert.That(rows[0].ShowcaseId, Is.EqualTo("sh_aaaaaaaaaaaaaaaa"));
            Assert.That(rows[1].ShowcaseId, Is.EqualTo("sh_bbbbbbbbbbbbbbbb"));
            Assert.That(rows[2].Votes, Is.Zero);
            Assert.That(rows[0].Rank, Is.EqualTo(1));
            Assert.That(rows[1].Rank, Is.EqualTo(2));
            Assert.That(rows[2].Rank, Is.EqualTo(3));
        }

        [Test]
        public async System.Threading.Tasks.Task discovery_preserves_blinded_server_order_and_deduplicates()
        {
            var transport = new FakeTransport { Discovery = new ShowcaseTransportResult(true, 200,
                "{\"success\":true,\"contestId\":\"season_1\",\"categoryId\":\"best_realm\"," +
                "\"candidates\":[{\"showcaseId\":\"sh_bbbbbbbbbbbbbbbb\"}," +
                "{\"showcaseId\":\"sh_aaaaaaaaaaaaaaaa\"}," +
                "{\"showcaseId\":\"sh_bbbbbbbbbbbbbbbb\"}]}" ) };
            var service = new CommunityShowcaseVotingService(transport, true);
            var rows = await service.DiscoverAsync("verified-player", "season_1", "best_realm");
            Assert.That(rows.Count, Is.EqualTo(2));
            Assert.That(rows[0].ShowcaseId, Is.EqualTo("sh_bbbbbbbbbbbbbbbb"));
            Assert.That(rows[1].ShowcaseId, Is.EqualTo("sh_aaaaaaaaaaaaaaaa"));
            Assert.That(transport.DiscoverCalls, Is.EqualTo(1));
        }

        [Test]
        public async System.Threading.Tasks.Task category_is_required_and_response_scope_must_match()
        {
            var transport = new FakeTransport { Counts = new ShowcaseTransportResult(true, 200,
                "{\"success\":true,\"contestId\":\"season_1\",\"categoryId\":\"other\"," +
                "\"candidates\":[{\"showcaseId\":\"sh_aaaaaaaaaaaaaaaa\",\"votes\":99}]}" ) };
            var service = new CommunityShowcaseVotingService(transport, true);
            Assert.That(await service.FetchCountsAsync("season_1", ""), Is.Empty);
            Assert.That(await service.FetchCountsAsync("season_1", "best_realm"), Is.Empty);
            Assert.That(transport.CountCalls, Is.EqualTo(1));
        }

        [Test]
        public async System.Threading.Tasks.Task vote_requires_matching_category_and_showcase_echo()
        {
            var transport = new FakeTransport { Vote = new ShowcaseTransportResult(true, 200,
                "{\"success\":true,\"state\":\"cast\",\"categoryId\":\"other\"," +
                "\"showcaseId\":\"sh_1234567890123456\"}") };
            var service = new CommunityShowcaseVotingService(transport, true);
            var result = await service.CastVoteAsync(
                "verified-player", "season_1", "best_realm", "sh_1234567890123456");
            Assert.That(result.State, Is.EqualTo(ShowcaseVoteState.Rejected));
            Assert.That(result.Error, Is.EqualTo("VOTE_REJECTED"));
        }

        [Test]
        public void winning_badge_expires_to_safe_unbadged_fallback()
        {
            var snapshot = new SkuEntitlementSnapshot();
            Assert.That(snapshot.ApplyPayload("{\"success\":true,\"serverNowMs\":1000," +
                "\"entitlements\":[{\"sku\":\"cosmetic.showcase.gold\",\"quantity\":1," +
                "\"source\":\"community\",\"expires_at\":\"1970-01-01T00:00:03Z\"}]}", 10d), Is.True);
            var active = ShowcaseCardPresenter.Create("sh_1234567890123456", 8,
                "cosmetic.showcase.gold", snapshot, 11d);
            var expired = ShowcaseCardPresenter.Create("sh_1234567890123456", 8,
                "cosmetic.showcase.gold", snapshot, 12d);
            Assert.That(active.HasWinningBadge, Is.True);
            Assert.That(expired.HasWinningBadge, Is.False);
            Assert.That(expired.Votes, Is.EqualTo(8));
        }
    }
}
