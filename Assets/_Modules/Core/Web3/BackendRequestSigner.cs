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
using DeNelle.Core.Diagnostics;   // FlowTrace - the connect-time session warm-up traces its outcome
using DeNelle.Core.State;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

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
        private const string SessionUrl = BackendBase + "/api/auth/session";

        // ── WO-1157: the cached session ──────────────────────────────────────────────
        // Buying one pack used to prompt the wallet THREE times: MWA connect, an auth
        // signature per backend call, and the transfer. Only the transfer should ever be
        // seen. The other two are session setup, and a session is what we were missing.
        //
        // ⛔ IN MEMORY ONLY, DELIBERATELY. This is a bearer credential: whoever holds it
        // speaks for the wallet until it expires. MwaSessionStore had to seal its token
        // with AES-GCM precisely because PlayerPrefs on Android is readable by a backup;
        // rather than repeat that machinery for a 15-minute token, we simply never write
        // it down. The cost is one extra signature after an app restart. That is the
        // right trade for a credential this powerful.
        //
        // ⛔ AND IT IS SCOPED TO ONE WALLET. Switching accounts must never inherit the
        // previous account's session - hence _sessionWallet, checked on every use.
        private static string   _sessionToken;
        private static string   _sessionWallet;
        private static DateTime _sessionExpiresUtc = DateTime.MinValue;

        // Re-sign a little BEFORE the server would reject: a token that expires in flight
        // becomes a 401 the player experiences as a failed purchase.
        private static readonly TimeSpan SessionSkew = TimeSpan.FromSeconds(60);

        private static bool SessionUsable(string wallet)
            => !string.IsNullOrEmpty(_sessionToken)
            && string.Equals(_sessionWallet, wallet, StringComparison.Ordinal)
            && DateTime.UtcNow + SessionSkew < _sessionExpiresUtc;

        /// <summary>
        /// Why a live session cannot be reused. Values are only <c>missing</c> or
        /// <c>expired</c> — never a wallet address (WO-1157: do not log wallets).
        /// </summary>
        private static string SessionGapWhy(string wallet)
        {
            if (string.IsNullOrEmpty(_sessionToken)) return "missing";
            if (!string.Equals(_sessionWallet, wallet, StringComparison.Ordinal)) return "missing";
            if (DateTime.UtcNow + SessionSkew >= _sessionExpiresUtc) return "expired";
            return "missing";
        }

        /// <summary>Drop the cached session. Call on wallet change, sign-out, or a 401.</summary>
        public static void ClearSession()
        {
            _sessionToken = null;
            _sessionWallet = null;
            _sessionExpiresUtc = DateTime.MinValue;
        }
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

            // ── WO-1157 Fail bounce 2026-08-27 ───────────────────────────────────────────
            // Device: every extra sheet was MintSessionAsync (MWA SignMessage) on Title
            // after CONTINUE, and again ~15 min later walking into a dungeon (TTL). Those
            // callers are cloud SAVE, not a purchase. Authed calls that are not a purchase
            // reuse a live session or WAIT — they must not pop SignMessage while walking.
            //
            // Purchase keeps the till prompt: if a live session exists, attach it and the
            // only wallet sheet is the transfer. If not, mint then transfer (first purchase
            // of a cold session). Per-request signature stays as a purchase-only fallback
            // when the session endpoint is down — never for Title CONTINUE / dungeon-enter.
            bool allowMint = IsPurchaseRoute(req);
            if (await TryAttachSession(req, wallet, allowMint)) return true;
            if (!allowMint) return false;

            FlowTrace.Warn("Wallet",
                $"purchase session mint failed; falling back to per-request signature. " +
                $"scene={CurrentSceneName()} caller={DescribeCaller(req)}");

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
        /// Connect/auto-resume hook. Returns true when a usable in-memory session is already held.
        /// Does not mint — boot/auto-resume must stay silent (WO-1211).
        /// <para>
        /// ⛔ WHY (owner, 2026-08-24): <i>"normally on a new game ... I see a Wallet connect, which
        /// gives it the account, but another one which is the handshake for the authentication. Then
        /// when you go to the store you already have two, so then you get the third one as the actual
        /// payment."</i> That is the pattern players know. Ours minted the session LAZILY, so the
        /// prompts landed 1-at-connect then TWO-at-first-purchase (session + payment) - the same
        /// three signatures, but the handshake interrupts the PURCHASE instead of the setup, which is
        /// precisely where a player is least willing to be surprised by an extra prompt.
        /// </para>
        /// <para>
        /// ⚠ WO-1211: auto-resume AND boot share this entry, so this method MUST NOT mint.
        /// A SignMessage here is the "every launch" sheet. Non-purchase authed calls wait.
        /// Purchase mints if no live session. Explicit connect uses
        /// <see cref="MintSessionForExplicitConnectAsync"/>.
        /// </para>
        /// </summary>
        public static UniTask<bool> WarmUpSessionAsync(string wallet)
        {
            bool held = !string.IsNullOrEmpty(wallet) && SessionUsable(wallet);
            FlowTrace.Step("Wallet", held
                ? "session warm-up found an existing usable in-memory session; no wallet action needed."
                : "session warm-up deferred - first authenticated action will mint; boot/connect never signs.");
            return UniTask.FromResult(held);
        }

        /// <summary>
        /// Mint now because the player explicitly connected. Auto-resume/boot must keep
        /// calling <see cref="WarmUpSessionAsync"/> (deferred). A live session is a no-op.
        /// </summary>
        public static UniTask<bool> MintSessionForExplicitConnectAsync(string wallet)
        {
            if (string.IsNullOrEmpty(wallet)) return UniTask.FromResult(false);
            if (SessionUsable(wallet))
            {
                FlowTrace.Step("Wallet",
                    $"MintSessionAsync why=explicit-connect scene={CurrentSceneName()} caller=explicit-connect (already live)");
                return UniTask.FromResult(true);
            }
            return MintSessionAsync(wallet, "explicit-connect", "explicit-connect");
        }

        /// <summary>Attach proof already available in memory without minting or signing.</summary>
        public static bool TryAttachCachedSession(UnityWebRequest req, string playerId)
        {
            if (req == null || string.IsNullOrWhiteSpace(playerId)) return false;
            if (IsGuestIdentity(playerId))
            {
                req.SetRequestHeader("X-Guest-Id", playerId);
                return true;
            }
            if (!SessionUsable(playerId)) return false;
            req.SetRequestHeader("X-Session", _sessionToken);
            req.SetRequestHeader("X-Wallet", playerId);
            return true;
        }

        /// <summary>
        /// WO-1157. Attach a cached session if we hold a usable one. Minting is opt-in
        /// (<paramref name="allowMint"/>) and reserved for purchase / explicit-connect —
        /// Title CONTINUE and dungeon-enter saves pass false so they never raise SignMessage.
        /// </summary>
        private static async UniTask<bool> TryAttachSession(UnityWebRequest req, string wallet, bool allowMint)
        {
            if (SessionUsable(wallet))
            {
                AttachSessionHeaders(req, wallet);
                return true;
            }

            string why = SessionGapWhy(wallet);
            string scene = CurrentSceneName();
            string caller = DescribeCaller(req);
            if (!allowMint)
            {
                FlowTrace.Step("Wallet",
                    $"authed call has no live session; waiting without SignMessage. " +
                    $"why={why} scene={scene} caller={caller}");
                return false;
            }

            var minted = await MintSessionAsync(wallet, why, caller);
            if (!minted) return false;
            AttachSessionHeaders(req, wallet);
            return true;
        }

        private static void AttachSessionHeaders(UnityWebRequest req, string wallet)
        {
            req.SetRequestHeader("X-Session", _sessionToken);
            // ⛔ X-Wallet travels alongside so the server can still bind the session to the player
            // being acted on. The session names the wallet; this lets the server CHECK that claim
            // rather than take it. Never send a session without it.
            req.SetRequestHeader("X-Wallet", wallet);
        }

        /// <summary>
        /// POST /api/auth/session with one nonce+signature, cache the returned bearer token.
        /// This is the ONLY place a wallet-signature prompt happens for backend auth.
        /// <paramref name="why"/> is missing / expired / explicit-connect.
        /// ⛔ Never logs a wallet address.
        /// </summary>
        private static async UniTask<bool> MintSessionAsync(string wallet, string why, string caller)
        {
            string scene = CurrentSceneName();
            FlowTrace.Step("Wallet",
                $"MintSessionAsync why={why} scene={scene} caller={caller}");

            var signer = CoreServices.WalletSigner;
            if (signer == null || !signer.CanSign)
            {
                FlowTrace.Warn("Wallet",
                    $"MintSessionAsync aborted (no signer) why={why} scene={scene} caller={caller}");
                return false;
            }

            var nonce = await FetchNonceAsync(wallet);
            if (string.IsNullOrEmpty(nonce))
            {
                FlowTrace.Warn("Wallet",
                    $"MintSessionAsync aborted (no nonce) why={why} scene={scene} caller={caller}");
                return false;
            }

            // Bodyless request, so the canonical message uses the 'load' payload tag - exactly
            // the shape the server rebuilds for a null payload. These two must not drift.
            var message = $"dotr-save:v1:{wallet}:{nonce}:load";

            string signature;
            try { signature = await signer.SignMessageBase58(message); }
            catch (Exception ex)
            {
                FlowTrace.Warn("Wallet",
                    $"MintSessionAsync signing failed ({ex.GetType().Name}) why={why} scene={scene} caller={caller}");
                Debug.LogWarning($"[BackendAuth] Session signing failed ({ex.GetType().Name}) - falling back to per-request signing.");
                return false;
            }
            if (string.IsNullOrEmpty(signature))
            {
                FlowTrace.Warn("Wallet",
                    $"MintSessionAsync empty signature why={why} scene={scene} caller={caller}");
                return false;
            }

            using var req = new UnityWebRequest(SessionUrl, UnityWebRequest.kHttpVerbPOST);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.timeout = RequestTimeoutSeconds;
            req.SetRequestHeader("Accept", "application/json");
            req.SetRequestHeader("X-Wallet", wallet);
            req.SetRequestHeader("X-Nonce", nonce);
            req.SetRequestHeader("X-Signature", signature);

            try { await req.SendWebRequest(); }
            catch (Exception e)
            {
                FlowTrace.Warn("Wallet",
                    $"MintSessionAsync threw ({req.responseCode}/{e.GetType().Name}) why={why} scene={scene} caller={caller}");
                Debug.LogWarning($"[BackendAuth] Session mint threw ({req.responseCode}): {e.GetType().Name}");
                return false;
            }
            if (req.result != UnityWebRequest.Result.Success)
            {
                FlowTrace.Warn("Wallet",
                    $"MintSessionAsync http {req.responseCode} why={why} scene={scene} caller={caller}");
                Debug.LogWarning($"[BackendAuth] Session mint failed ({req.responseCode}).");
                return false;
            }

            try
            {
                var res = JsonConvert.DeserializeObject<SessionResponse>(req.downloadHandler.text);
                if (res == null || !res.Ok || string.IsNullOrEmpty(res.Token))
                {
                    FlowTrace.Warn("Wallet",
                        $"MintSessionAsync empty token why={why} scene={scene} caller={caller}");
                    return false;
                }

                _sessionToken = res.Token;
                _sessionWallet = wallet;
                // Prefer the server's own expiry; fall back to ttlSeconds. ⛔ Never invent one -
                // a client-guessed expiry that outlives the server's produces a 401 the player
                // sees as a failed purchase.
                _sessionExpiresUtc = DateTime.TryParse(res.ExpiresAt, null,
                        System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal,
                        out var parsed)
                    ? parsed
                    : DateTime.UtcNow.AddSeconds(res.TtlSeconds > 0 ? res.TtlSeconds : 60);
                FlowTrace.Step("Wallet",
                    $"MintSessionAsync held why={why} scene={scene} caller={caller}");
                return true;
            }
            catch (Exception ex)
            {
                FlowTrace.Warn("Wallet",
                    $"MintSessionAsync parse {ex.GetType().Name} why={why} scene={scene} caller={caller}");
                Debug.LogWarning($"[BackendAuth] Session parse error: {ex.GetType().Name}");
                return false;
            }
        }

        private static bool IsPurchaseRoute(UnityWebRequest req)
        {
            string path = RequestPath(req);
            return path.IndexOf("/api/purchases/", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string CurrentSceneName()
        {
            try
            {
                string n = SceneManager.GetActiveScene().name;
                return string.IsNullOrEmpty(n) ? "none" : n;
            }
            catch
            {
                return "none";
            }
        }

        /// <summary>Path-only request id. Query is stripped so a wallet never lands in a log.</summary>
        private static string RequestPath(UnityWebRequest req)
        {
            if (req == null || string.IsNullOrEmpty(req.url)) return "no-url";
            string url = req.url;
            int q = url.IndexOf('?');
            if (q >= 0) url = url.Substring(0, q);
            int scheme = url.IndexOf("://", StringComparison.Ordinal);
            if (scheme >= 0)
            {
                int pathStart = url.IndexOf('/', scheme + 3);
                if (pathStart >= 0) url = url.Substring(pathStart);
            }
            return string.IsNullOrEmpty(url) ? "no-url" : url;
        }

        private static string DescribeCaller(UnityWebRequest req)
        {
            string path = RequestPath(req);
            string frame = FirstExternalFrame();
            return string.IsNullOrEmpty(frame) ? path : frame + " " + path;
        }

        private static string FirstExternalFrame()
        {
            try
            {
                var st = new System.Diagnostics.StackTrace(2, false);
                for (int i = 0; i < st.FrameCount; i++)
                {
                    var m = st.GetFrame(i)?.GetMethod();
                    var t = m?.DeclaringType;
                    if (t == null || t == typeof(BackendRequestSigner)) continue;
                    return t.Name + "." + m.Name;
                }
            }
            catch { /* IL2CPP may omit frames */ }
            return string.Empty;
        }

        private sealed class SessionResponse
        {
            [JsonProperty("ok")]         public bool   Ok         { get; set; }
            [JsonProperty("token")]      public string Token      { get; set; }
            [JsonProperty("expiresAt")]  public string ExpiresAt  { get; set; }
            [JsonProperty("ttlSeconds")] public int    TtlSeconds { get; set; }
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
