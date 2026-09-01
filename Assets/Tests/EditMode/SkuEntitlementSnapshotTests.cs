using System;
using Cysharp.Threading.Tasks;
using DeNelle.Core.Entitlements;
using NUnit.Framework;

namespace DeNelle.Tests.EditMode
{
    [TestFixture]
    public sealed class SkuEntitlementSnapshotTests
    {
        private const long ServerNow = 1787961600000L;

        [Test]
        public void exact_server_payload_restores_permanent_and_temporary_skus()
        {
            var view = new SkuEntitlementSnapshot();
            string expiry = DateTimeOffset.FromUnixTimeMilliseconds(ServerNow + 5000).ToString("O");
            Assert.That(view.ApplyPayload(Payload(
                Row("gate_stone", 1, "progression", null) + "," +
                Row("tower_trial", 2, "tournament", expiry)), 100d), Is.True);
            Assert.That(view.IsEntitled("gate_stone", 100d), Is.True);
            Assert.That(view.Quantity("tower_trial", 100d), Is.EqualTo(2));
        }

        [Test]
        public void expiry_uses_server_anchor_plus_monotonic_elapsed_not_device_clock()
        {
            var view = new SkuEntitlementSnapshot();
            string expiry = DateTimeOffset.FromUnixTimeMilliseconds(ServerNow + 5000).ToString("O");
            Assert.That(view.ApplyPayload(Payload(Row("tower_trial", 1, "tournament", expiry)), 10d), Is.True);
            Assert.That(view.IsEntitled("tower_trial", 14.999d), Is.True);
            Assert.That(view.IsEntitled("tower_trial", 15d), Is.False);
        }

        [Test]
        public void duplicate_sku_grants_keep_independent_expiry_buckets_and_sum_active_quantity()
        {
            var view = new SkuEntitlementSnapshot();
            string early = DateTimeOffset.FromUnixTimeMilliseconds(ServerNow + 2000).ToString("O");
            string late = DateTimeOffset.FromUnixTimeMilliseconds(ServerNow + 5000).ToString("O");
            Assert.That(view.ApplyPayload(Payload(
                Row("tower_trial", 2, "tournament", early) + "," +
                Row("tower_trial", 3, "community", late)), 10d), Is.True);
            Assert.That(view.Count, Is.EqualTo(1));
            Assert.That(view.Quantity("tower_trial", 11d), Is.EqualTo(5));
            Assert.That(view.Quantity("tower_trial", 12d), Is.EqualTo(3));
            Assert.That(view.Quantity("tower_trial", 15d), Is.Zero);
        }

        [Test]
        public void already_expired_rows_never_enter_the_snapshot()
        {
            var view = new SkuEntitlementSnapshot();
            string expiry = DateTimeOffset.FromUnixTimeMilliseconds(ServerNow).ToString("O");
            Assert.That(view.ApplyPayload(Payload(Row("tower_trial", 1, "tournament", expiry)), 0d), Is.True);
            Assert.That(view.IsEntitled("tower_trial", 0d), Is.False);
        }

        [Test]
        public void failed_refresh_clears_remote_ownership_but_packaged_baseline_stays_available()
        {
            var view = new SkuEntitlementSnapshot();
            Assert.That(view.ApplyPayload(Payload(Row("remote_only", 1, "operator", null)), 1d), Is.True);
            view.FailClosed();
            Assert.That(view.CanUseSku("remote_only", false, 1d), Is.False);
            Assert.That(view.CanUseSku("packaged_core", true, 1d), Is.True);
        }

        [Test]
        public void cache_or_malformed_payload_cannot_create_ownership()
        {
            var view = new SkuEntitlementSnapshot();
            Assert.That(view.ApplyPayload("{\"success\":true,\"entitlements\":[]}", 1d), Is.False,
                "serverNowMs is mandatory authority");
            Assert.That(view.CanUseSku("cached_art", false, 1d), Is.False);
            Assert.That(view.ApplyPayload(Payload("{\"sku\":\"BAD SKU\",\"quantity\":1,\"source\":\"operator\"}"), 1d), Is.False);
            Assert.That(view.ApplyPayload(Payload(Row("valid_sku", 1, "client_claim", null)), 1d), Is.False,
                "unknown source categories cannot create ownership");
        }

        [Test]
        public async System.Threading.Tasks.Task injected_transport_restores_and_failure_revokes_standing_remote_view()
        {
            var transport = new FakeTransport(Payload(Row("gate_stone", 1, "progression", null)));
            var service = new SkuEntitlementService(transport);
            Assert.That(await service.RefreshAsync("11111111111111111111111111111111"), Is.True);
            Assert.That(service.Snapshot.IsEntitled("gate_stone", UnityEngine.Time.realtimeSinceStartupAsDouble), Is.True);
            transport.Success = false;
            Assert.That(await service.RefreshAsync("11111111111111111111111111111111"), Is.False);
            Assert.That(service.Snapshot.IsEntitled("gate_stone", UnityEngine.Time.realtimeSinceStartupAsDouble), Is.False);
        }

        [Test]
        public void only_progression_source_can_restore_a_permanent_progression_flag()
        {
            var view = new SkuEntitlementSnapshot();
            Assert.That(view.ApplyPayload(Payload(
                Row("gate_stone", 1, "tournament", null) + "," +
                Row("healing_caravan", 1, "progression", null)), 1d), Is.True);

            Assert.That(view.IsEntitled("gate_stone", 1d), Is.True,
                "temporary/catalog ownership remains usable");
            Assert.That(view.IsProgressionEntitled("gate_stone", 1d), Is.False,
                "a tournament grant must not become a permanent local unlock");
            Assert.That(view.IsProgressionEntitled("healing_caravan", 1d), Is.True);
        }

        private static string Payload(string rows) =>
            "{\"success\":true,\"serverNowMs\":" + ServerNow + ",\"entitlements\":[" + rows + "]}";

        private static string Row(string sku, int quantity, string source, string expiresAt) =>
            "{\"sku\":\"" + sku + "\",\"quantity\":" + quantity + ",\"source\":\"" + source +
            "\",\"granted_at\":\"2026-08-29T00:00:00Z\",\"expires_at\":" +
            (expiresAt == null ? "null" : "\"" + expiresAt + "\"") + "}";

        private sealed class FakeTransport : ISkuEntitlementTransport
        {
            private readonly string _body;
            public bool Success = true;
            public FakeTransport(string body) { _body = body; }
            public UniTask<EntitlementTransportResult> GetAsync(string playerId) =>
                UniTask.FromResult(new EntitlementTransportResult(Success, Success ? _body : null));
        }
    }
}
