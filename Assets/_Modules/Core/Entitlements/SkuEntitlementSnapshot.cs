using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace DeNelle.Core.Entitlements
{
    /// <summary>
    /// In-memory ownership view from one authenticated server snapshot. It never reads cache,
    /// PlayerPrefs, SaveSchema, scene objects, or device wall-clock time.
    /// </summary>
    public sealed class SkuEntitlementSnapshot
    {
        private readonly Dictionary<string, List<Entry>> _active =
            new Dictionary<string, List<Entry>>(StringComparer.Ordinal);
        private static readonly HashSet<string> AllowedSources = new HashSet<string>(StringComparer.Ordinal)
        {
            "progression", "tournament", "promotion", "community", "operator", "migration"
        };
        private long _serverNowMs;
        private double _anchorMonotonicSeconds;

        public bool HasVerifiedSnapshot { get; private set; }
        public int Count => _active.Count;

        public bool ApplyPayload(string json, double monotonicSeconds)
        {
            Response response;
            try { response = JsonConvert.DeserializeObject<Response>(json); }
            catch { return false; }
            if (response == null || !response.Success || response.ServerNowMs <= 0 ||
                response.Entitlements == null || monotonicSeconds < 0) return false;

            var next = new Dictionary<string, List<Entry>>(StringComparer.Ordinal);
            foreach (var wire in response.Entitlements)
            {
                if (wire == null || !ValidSku(wire.Sku) || wire.Quantity <= 0 ||
                    !AllowedSources.Contains(wire.Source)) return false;

                long? expiresMs = null;
                if (!string.IsNullOrWhiteSpace(wire.ExpiresAt))
                {
                    if (!DateTimeOffset.TryParse(wire.ExpiresAt, out var expires)) return false;
                    expiresMs = expires.ToUnixTimeMilliseconds();
                    if (expiresMs.Value <= response.ServerNowMs) continue;
                }

                if (!next.TryGetValue(wire.Sku, out var grants))
                {
                    grants = new List<Entry>();
                    next.Add(wire.Sku, grants);
                }
                grants.Add(new Entry(wire.Quantity, wire.Source, expiresMs));
            }

            _active.Clear();
            foreach (var pair in next) _active.Add(pair.Key, pair.Value);
            _serverNowMs = response.ServerNowMs;
            _anchorMonotonicSeconds = monotonicSeconds;
            HasVerifiedSnapshot = true;
            return true;
        }

        /// <summary>Transport/auth failure removes remote ownership. Packaged content is a separate baseline.</summary>
        public void FailClosed()
        {
            _active.Clear();
            _serverNowMs = 0;
            _anchorMonotonicSeconds = 0;
            HasVerifiedSnapshot = false;
        }

        public bool IsEntitled(string sku, double monotonicSeconds)
        {
            if (!HasVerifiedSnapshot || !ValidSku(sku) || monotonicSeconds < _anchorMonotonicSeconds ||
                !_active.TryGetValue(sku, out var grants)) return false;
            long nowMs = AnchoredNowMs(monotonicSeconds);
            for (int i = 0; i < grants.Count; i++)
                if (!grants[i].ExpiresAtMs.HasValue || nowMs < grants[i].ExpiresAtMs.Value) return true;
            return false;
        }

        /// <summary>Permanent local progression may only be restored from a progression grant;
        /// temporary tournament/promotion ownership must never burn a permanent unlock flag.</summary>
        public bool IsProgressionEntitled(string sku, double monotonicSeconds)
        {
            if (!HasVerifiedSnapshot || !ValidSku(sku) || monotonicSeconds < _anchorMonotonicSeconds ||
                !_active.TryGetValue(sku, out var grants)) return false;
            long nowMs = AnchoredNowMs(monotonicSeconds);
            for (int i = 0; i < grants.Count; i++)
            {
                var grant = grants[i];
                if (!string.Equals(grant.Source, "progression", StringComparison.Ordinal)) continue;
                if (!grant.ExpiresAtMs.HasValue || nowMs < grant.ExpiresAtMs.Value) return true;
            }
            return false;
        }

        /// <summary>Card/content gate: packaged baseline never depends on the remote entitlement rail.</summary>
        public bool CanUseSku(string sku, bool packagedBaseline, double monotonicSeconds)
        {
            return packagedBaseline || IsEntitled(sku, monotonicSeconds);
        }

        public int Quantity(string sku, double monotonicSeconds)
        {
            if (!HasVerifiedSnapshot || !ValidSku(sku) || monotonicSeconds < _anchorMonotonicSeconds ||
                !_active.TryGetValue(sku, out var grants)) return 0;
            long nowMs = AnchoredNowMs(monotonicSeconds);
            long total = 0;
            for (int i = 0; i < grants.Count; i++)
            {
                var grant = grants[i];
                if (grant.ExpiresAtMs.HasValue && nowMs >= grant.ExpiresAtMs.Value) continue;
                total += grant.Quantity;
                if (total >= int.MaxValue) return int.MaxValue;
            }
            return (int)total;
        }

        private long AnchoredNowMs(double monotonicSeconds)
        {
            double elapsed = Math.Max(0d, monotonicSeconds - _anchorMonotonicSeconds) * 1000d;
            if (elapsed >= long.MaxValue - _serverNowMs) return long.MaxValue;
            return _serverNowMs + (long)Math.Floor(elapsed);
        }

        private static bool ValidSku(string sku)
        {
            if (string.IsNullOrEmpty(sku) || sku.Length > 96) return false;
            for (int i = 0; i < sku.Length; i++)
            {
                char c = sku[i];
                if (!(c >= 'a' && c <= 'z') && !(c >= '0' && c <= '9') &&
                    c != '.' && c != '_' && c != '-') return false;
            }
            return true;
        }

        private readonly struct Entry
        {
            public readonly int Quantity;
            public readonly string Source;
            public readonly long? ExpiresAtMs;
            public Entry(int quantity, string source, long? expiresAtMs)
            {
                Quantity = quantity; Source = source; ExpiresAtMs = expiresAtMs;
            }
        }

        private sealed class Response
        {
            [JsonProperty("success")] public bool Success { get; set; }
            [JsonProperty("serverNowMs")] public long ServerNowMs { get; set; }
            [JsonProperty("entitlements")] public List<WireEntry> Entitlements { get; set; }
        }

        private sealed class WireEntry
        {
            [JsonProperty("sku")] public string Sku { get; set; }
            [JsonProperty("quantity")] public int Quantity { get; set; }
            [JsonProperty("source")] public string Source { get; set; }
            [JsonProperty("granted_at")] public string GrantedAt { get; set; }
            [JsonProperty("expires_at")] public string ExpiresAt { get; set; }
        }
    }
}
