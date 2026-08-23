using System;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Wallet
{
    internal static class SkrValuationOracle
    {
        private const string MarketsUrl = "https://api.coingecko.com/api/v3/coins/markets?vs_currency=usd&ids=seeker";
        private const float CacheSeconds = 300f;
        private static double _usdLow24h;
        private static float _fetchedAt;
        private static bool _fetching;

        internal static bool HasQuote => _usdLow24h > 0d && Time.realtimeSinceStartup - _fetchedAt < CacheSeconds;
        internal static double UsdLow24h => HasQuote ? _usdLow24h : 0d;
        internal static double SkrForUsd(double usd) => HasQuote && usd > 0d ? Math.Ceiling(usd / _usdLow24h) : 0d;

        internal static async UniTask<bool> Refresh()
        {
            if (HasQuote) return true;
            if (_fetching) { await UniTask.WaitUntil(() => !_fetching); return HasQuote; }
            _fetching = true;
            try
            {
                using var request = UnityWebRequest.Get(MarketsUrl);
                request.timeout = 8;
                await request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success)
                {
                    FlowTrace.Warn("Store", $"SKR 24h valuation unavailable: {request.error}");
                    return false;
                }
                var rows = JArray.Parse(request.downloadHandler.text);
                double low = rows.Count > 0 ? rows[0].Value<double?>("low_24h") ?? 0d : 0d;
                if (low <= 0d || double.IsNaN(low) || double.IsInfinity(low)) return false;
                _usdLow24h = low;
                _fetchedAt = Time.realtimeSinceStartup;
                FlowTrace.Step("Store", $"SKR valuation refreshed: 24h low=${low:0.########}.");
                return true;
            }
            catch (Exception ex)
            {
                FlowTrace.Warn("Store", $"SKR 24h valuation failed closed: {ex.Message}");
                return false;
            }
            finally { _fetching = false; }
        }
    }
}
