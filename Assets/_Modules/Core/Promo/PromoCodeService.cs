// =============================================================================
// PromoCodeService — WO5: Promo Code Redemption.
// -----------------------------------------------------------------------------
// Manages the full redemption lifecycle for operator-issued promo codes:
//   1. Client validates the code has not already been redeemed locally.
//   2. POSTs to api/promo/redeem — backend enforces one-time-use globally.
//   3. On 200 OK: applies reward (crystals + coins) + fires analytics event.
//
// BACKEND CONTRACT:
//   POST api/promo/redeem
//   Body:    { playerId, code }
//   Success: { success: true, reward: { crystals, coins }, message }
//   Failure: { success: false, error: "INVALID_CODE" | "ALREADY_REDEEMED"
//                                   | "EXPIRED" | "PLAYER_LIMIT_REACHED" }
//
// LOCAL DEDUP:
//   Redeemed codes are stored in PlayerPrefs key "dotr-redeemed-promos" as a
//   comma-separated list. This is a UX guard only — the backend is the source
//   of truth for one-time enforcement.
//
// INSPECTOR SETUP: none — add to a persistent scene GO or bootstrap via
//   PromoCodeService.EnsureExists() from GameStateService.Awake().
// =============================================================================

using System;
using System.Collections.Generic;
using System.Text;
using Cysharp.Threading.Tasks;
using DeNelle.Core.Analytics;
using DeNelle.Core.State;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace DeNelle.Core.Promo
{
    /// <summary>
    /// Singleton MonoBehaviour that drives promo code redemption.
    /// Subscribe to <see cref="OnRedeemed"/> or <see cref="OnRedeemFailed"/>
    /// to react in UI.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PromoCodeService : MonoBehaviour
    {
        // ── Constants ─────────────────────────────────────────────────────────

        private const string BackendRedeemUrl = "https://defenders-of-the-realm-v2.vercel.app/api/promo/redeem";
        private const string PlayerPrefsKey   = "dotr-redeemed-promos";

        // ── Singleton ─────────────────────────────────────────────────────────

        private static PromoCodeService _instance;
        public  static PromoCodeService Instance => _instance;

        public static void EnsureExists()
        {
            if (_instance != null) return;
            var go = new GameObject("[PromoCodeService]");
            DontDestroyOnLoad(go);
            go.AddComponent<PromoCodeService>();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            DontDestroyOnLoad(gameObject);
            LoadRedeemedSet();
        }

        // ── State ─────────────────────────────────────────────────────────────

        private readonly HashSet<string> _redeemedLocally = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // ── Events ────────────────────────────────────────────────────────────

        /// <summary>Fired when a code is successfully redeemed. Carries the reward.</summary>
        public event Action<PromoReward> OnRedeemed;

        /// <summary>Fired when redemption fails. Carries a human-readable reason.</summary>
        public event Action<string>      OnRedeemFailed;

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>Returns true when the code is already in the local dedup set.</summary>
        public bool IsAlreadyRedeemedLocally(string code) =>
            !string.IsNullOrWhiteSpace(code) && _redeemedLocally.Contains(code.Trim().ToUpperInvariant());

        /// <summary>
        /// Attempts to redeem <paramref name="code"/>. Fires <see cref="OnRedeemed"/>
        /// or <see cref="OnRedeemFailed"/> on completion.
        /// </summary>
        public async UniTask RedeemAsync(string code)
        {
            code = code?.Trim().ToUpperInvariant() ?? string.Empty;
            if (string.IsNullOrEmpty(code))
            {
                OnRedeemFailed?.Invoke("Please enter a code.");
                return;
            }

            if (_redeemedLocally.Contains(code))
            {
                OnRedeemFailed?.Invoke("You've already redeemed that code.");
                return;
            }

            var playerId = GameStateService.Instance?.State?.BoundWallet ?? "anonymous";

            var payload = JsonConvert.SerializeObject(new { playerId, code });
            var bodyRaw = Encoding.UTF8.GetBytes(payload);

            using var req = new UnityWebRequest(BackendRedeemUrl, "POST");
            req.uploadHandler   = new UploadHandlerRaw(bodyRaw);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");

            try
            {
                await req.SendWebRequest();
            }
            catch (Exception ex)
            {
                OnRedeemFailed?.Invoke($"Network error: {ex.Message}");
                return;
            }

            if (req.result != UnityWebRequest.Result.Success)
            {
                OnRedeemFailed?.Invoke("Could not reach server. Try again later.");
                return;
            }

            RedeemResponse resp;
            try
            {
                resp = JsonConvert.DeserializeObject<RedeemResponse>(req.downloadHandler.text);
            }
            catch
            {
                OnRedeemFailed?.Invoke("Unexpected server response.");
                return;
            }

            if (resp == null || !resp.Success)
            {
                string reason = MapError(resp?.Error);
                OnRedeemFailed?.Invoke(reason);
                return;
            }

            // ── Apply reward ──────────────────────────────────────────────────
            var reward = resp.Reward ?? new PromoReward();
            ApplyReward(reward);

            // ── Local dedup ───────────────────────────────────────────────────
            _redeemedLocally.Add(code);
            PersistRedeemedSet();

            // ── Analytics ─────────────────────────────────────────────────────
            EventTracker.Track("promo_redeemed", new
            {
                code,
                crystals = reward.Crystals,
                coins    = reward.Coins,
                message  = resp.Message,
            });

            Debug.Log($"[PromoCodeService] Code '{code}' redeemed — crystals:{reward.Crystals} coins:{reward.Coins}");
            OnRedeemed?.Invoke(reward);
        }

        // ── Reward application ────────────────────────────────────────────────

        private static void ApplyReward(PromoReward reward)
        {
            var state = GameStateService.Instance?.State;
            if (state == null) return;

            // Crystals + Coins both live on Resources (the single wallet). Crystals were
            // unified onto Resources.Crystals (save v18); ResourceBalance is a struct, so
            // read-modify-write-assign-back. CrystalEconomy lives in DeNelle.Village which
            // Core cannot reference, so we award via shared GameState here (Core-internal).
            if (reward.Crystals > 0 || reward.Coins > 0)
            {
                var r = state.Resources;
                if (reward.Crystals > 0) r.Crystals += reward.Crystals;
                if (reward.Coins > 0)    r.Coins    += reward.Coins;
                state.Resources = r;
                GameStateService.Instance.Save();
                GameStateService.Instance.ResourcesChanged?.Invoke();
            }
        }

        // ── Error mapping ─────────────────────────────────────────────────────

        private static string MapError(string errorCode) => errorCode switch
        {
            "INVALID_CODE"        => "That code doesn't exist.",
            "ALREADY_REDEEMED"    => "This code has already been used.",
            "EXPIRED"             => "That code has expired.",
            "PLAYER_LIMIT_REACHED"=> "You've already redeemed the maximum number of promo codes.",
            _                     => "Redemption failed. Please try again.",
        };

        // ── PlayerPrefs persistence ───────────────────────────────────────────

        private void LoadRedeemedSet()
        {
            _redeemedLocally.Clear();
            var raw = PlayerPrefs.GetString(PlayerPrefsKey, string.Empty);
            if (string.IsNullOrEmpty(raw)) return;
            foreach (var code in raw.Split(','))
                if (!string.IsNullOrWhiteSpace(code))
                    _redeemedLocally.Add(code.Trim());
        }

        private void PersistRedeemedSet()
        {
            PlayerPrefs.SetString(PlayerPrefsKey, string.Join(",", _redeemedLocally));
            PlayerPrefs.Save();
        }

        // ── DTOs ──────────────────────────────────────────────────────────────

        private sealed class RedeemResponse
        {
            [JsonProperty("success")] public bool         Success { get; set; }
            [JsonProperty("reward")]  public PromoReward  Reward  { get; set; }
            [JsonProperty("message")] public string       Message { get; set; }
            [JsonProperty("error")]   public string       Error   { get; set; }
        }

#if UNITY_EDITOR
        [UnityEditor.MenuItem("Defenders/Debug/Simulate Promo Redeem (TEST10)")]
        private static void EditorSimulateRedeem()
        {
            if (_instance == null) { Debug.LogWarning("[PromoCodeService] No instance in scene."); return; }
            _instance.RedeemAsync("TEST10").Forget();
        }
#endif
    }

    /// <summary>Reward payload returned by the backend on successful redemption.</summary>
    [Serializable]
    public sealed class PromoReward
    {
        [JsonProperty("crystals")] public int Crystals { get; set; }
        [JsonProperty("coins")]    public int Coins    { get; set; }
        [JsonProperty("message")]  public string Message { get; set; }
    }
}
