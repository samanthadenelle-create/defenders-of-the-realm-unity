// =============================================================================
// BackendRequestSigner — the ONE client-side auth-header attach for backend POSTs
// -----------------------------------------------------------------------------
// The backend's identity gate (api/_lib/wallet-auth.js `authenticate`) routes by
// the SHAPE of the playerId being acted on, never by which headers arrive:
//
//   WALLET RAIL  base58 32-44 id  -> X-Wallet + X-Nonce + X-Signature, an ed25519
//                signature over the canonical message
//                    dotr-save:v1:<wallet>:<nonce>:<sha256-hex-of-body>
//                with a single-use, server-burned nonce.
//   GUEST RAIL   "guest-local-<64 hex>" -> X-Guest-Id only. The id IS the
//                credential; the server rate-limits it and marks the row guest.
//
// WHY THIS FILE EXISTS: that attach logic previously lived ONLY inside
// GameStateService.TryAttachAuthHeaders, private to the save/load pipeline. Every
// OTHER route that acts on a player identity (promo redeem, referral generate,
// referral claim) therefore posted a bare playerId with no proof at all, so any
// caller could name a victim's id. Rather than a second auth scheme, those routes
// now call THIS — the identical protocol, the identical canonical message, the
// identical fail-closed rule.
//
// FAIL CLOSED: every method returns false when it cannot produce the proof the
// rail demands, and the caller MUST abort the request rather than send it
// unauthed. An unauthed request on a gated route is a guaranteed 401 anyway; the
// difference is that aborting never leaves the player wondering why nothing
// happened while a forged-identity path stays open.
//
// Mirrors: api/_lib/wallet-auth.js (authenticate / buildSignedMessage / verifyGuest)
//          GameStateService.TryAttachAuthHeaders (save+load; unchanged)
// =============================================================================

using System;
using System.Security.Cryptography;
using System.Text;
using Cysharp.Threading.Tasks;
using DeNelle.Core.State;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace DeNelle.Core.Web3
{
    /// <summary>
    /// Attaches the backend's identity headers to an outgoing UnityWebRequest.
    /// Shared by every route that acts on a player identity.
    /// </summary>
    public static class BackendRequestSigner
    {
        /// <summary>Root of the deployed serverless API.</summary>
        public const string BackendBase = "https://defenders-of-the-realm-v2.vercel.app";

        private const string NonceUrl = BackendBase + "/api/auth/nonce";
        private const int    RequestTimeoutSeconds = 15;
        private const string GuestWalletPrefix = "guest-local-";

        /// <summary>
        /// The identity the backend will key on. Empty when there is no account at
        /// all — callers must treat that as "cannot authenticate", NOT as
        /// "anonymous": the server rejects an unshaped id with PLAYER_ID_BAD_SHAPE.
        /// </summary>
        public static string CurrentPlayerId()
        {
            var id = GameStateService.Instance?.State?.BoundWallet;
            return string.IsNullOrWhiteSpace(id) ? string.Empty : id.Trim();
        }

        /// <summary>True for the "guest-local-&lt;64 hex&gt;" shape the client mints offline.</summary>
        public static bool IsGuestIdentity(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            if (!id.StartsWith(GuestWalletPrefix, StringComparison.Ordinal)) return false;
            if (id.Length != GuestWalletPrefix.Length + 64) return false;
            for (int i = GuestWalletPrefix.Length; i < id.Length; i++)
            {
                char c = id[i];
                bool hex = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f');
                if (!hex) return false;
            }
            return true;
        }

        /// <summary>Lowercase hex SHA-256 — byte-identical to Node's crypto sha256 hex digest.</summary>
        public static string Sha256Hex(byte[] bytes)
        {
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(bytes ?? Array.Empty<byte>());
            var sb = new StringBuilder(hash.Length * 2);
            foreach (var b in hash) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }

        /// <summary>
        /// Attach the headers the backend needs to believe <paramref name="playerId"/>.
        /// </summary>
        /// <param name="req">the request being prepared (headers set in place)</param>
        /// <param name="playerId">the id the request acts on — must match the rail</param>
        /// <param name="bodyRaw">the EXACT bytes that will be uploaded (the signature covers them)</param>
        /// <returns>true when it is safe to send; false means ABORT the request.</returns>
        public static async UniTask<bool> TryAttachAsync(UnityWebRequest req, string playerId, byte[] bodyRaw)
        {
            if (req == null) return false;

            if (string.IsNullOrWhiteSpace(playerId))
            {
                Debug.LogWarning("[BackendAuth] No player identity available - aborting request (fail-closed).");
                return false;
            }

            // GUEST RAIL first: a guest has no signer at all and would otherwise
            // fail closed below and never reach the backend.
            if (IsGuestIdentity(playerId))
            {
                req.SetRequestHeader("X-Guest-Id", playerId);
                return true;
            }

            var signer = CoreServices.WalletSigner;
            if (signer == null || !signer.CanSign)
            {
                Debug.LogWarning("[BackendAuth] Wallet identity but no real signer available - " +
                                 "aborting request (fail-closed; refusing to send unauthed).");
                return false;
            }

            var wallet = signer.WalletAddress;
            if (string.IsNullOrEmpty(wallet))
            {
                Debug.LogWarning("[BackendAuth] Signer reports CanSign but has no address - aborting (fail-closed).");
                return false;
            }
            if (!string.Equals(wallet, playerId, StringComparison.Ordinal))
            {
                // The server enforces this too (AUTH_WALLET_MISMATCH); refusing here
                // makes the reason legible instead of an opaque 401.
                Debug.LogWarning("[BackendAuth] Signing wallet does not match the acted-on player id - aborting.");
                return false;
            }

            // 1. Fresh single-use nonce bound to this wallet.
            var nonce = await FetchNonceAsync(wallet);
            if (string.IsNullOrEmpty(nonce))
            {
                Debug.LogWarning("[BackendAuth] Could not obtain an auth nonce - aborting (fail-closed).");
                return false;
            }

            // 2. The EXACT canonical message the backend reconstructs.
            var payloadTag = (bodyRaw != null && bodyRaw.Length > 0) ? Sha256Hex(bodyRaw) : "load";
            var message = $"dotr-save:v1:{wallet}:{nonce}:{payloadTag}";

            string signature;
            try
            {
                signature = await signer.SignMessageBase58(message);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[BackendAuth] Wallet signing failed ({ex.GetType().Name}) - aborting (fail-closed).");
                return false;
            }

            if (string.IsNullOrEmpty(signature))
            {
                Debug.LogWarning("[BackendAuth] Wallet returned an empty signature - aborting (fail-closed).");
                return false;
            }

            req.SetRequestHeader("X-Wallet", wallet);
            req.SetRequestHeader("X-Nonce", nonce);
            req.SetRequestHeader("X-Signature", signature);
            return true;
        }

        /// <summary>
        /// GET /api/auth/nonce?wallet=&lt;base58&gt; -> the issued one-time nonce, or
        /// null on any failure (the caller then aborts rather than sending unauthed).
        /// </summary>
        private static async UniTask<string> FetchNonceAsync(string wallet)
        {
            var url = $"{NonceUrl}?wallet={Uri.EscapeDataString(wallet)}";
            using var req = UnityWebRequest.Get(url);
            req.timeout = RequestTimeoutSeconds;
            req.SetRequestHeader("Accept", "application/json");

            try
            {
                await req.SendWebRequest();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[BackendAuth] Nonce fetch threw ({req.responseCode}): {e.GetType().Name}");
                return null;
            }

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[BackendAuth] Nonce fetch failed ({req.responseCode}).");
                return null;
            }

            try
            {
                var resp = JsonConvert.DeserializeObject<NonceResponse>(req.downloadHandler.text);
                return resp != null && resp.Success ? resp.Nonce : null;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[BackendAuth] Nonce parse error: {ex.GetType().Name}");
                return null;
            }
        }

        private sealed class NonceResponse
        {
            [JsonProperty("success")]    public bool   Success    { get; set; }
            [JsonProperty("nonce")]      public string Nonce      { get; set; }
            [JsonProperty("expiresAt")]  public string ExpiresAt  { get; set; }
            [JsonProperty("ttlSeconds")] public int    TtlSeconds { get; set; }
        }
    }
}
