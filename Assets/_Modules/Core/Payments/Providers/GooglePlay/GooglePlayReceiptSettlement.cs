using System;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace DeNelle.Core.Payments.Providers
{
    /// <summary>
    /// Durable grant boundary supplied by the store composition root. Implementations must persist
    /// the purchase-token marker and the pack mutation as one operation: a crash/retry may call
    /// IsApplied again, but must never apply the same token twice.
    /// </summary>
    public interface IGooglePlayGrantApplier
    {
        bool IsApplied(string purchaseToken);
        Task<bool> ApplyExactlyOnceAsync(string sku, string purchaseToken);
    }

    public interface IGooglePlaySettlementTransport
    {
        Task<GooglePlayVerifyReply> VerifyAsync(string sku, string productId, string purchaseToken);
        Task<bool> FulfillAsync(string sku, string productId, string purchaseToken);
    }

    public interface IGooglePlayAccountBindingSource
    {
        Task<string> FetchAccountBindingAsync();
    }

    [Serializable]
    public sealed class GooglePlayVerifyReply
    {
        public bool success;
        public string state;
        public string sku;
    }

    /// <summary>
    /// Executes the only safe order: parse token -> authenticated server verify -> durable local
    /// apply exactly once -> authenticated server fulfill. GooglePlayBillingProvider confirms the
    /// Unity order only after this method returns true.
    /// </summary>
    public sealed class GooglePlayReceiptSettlement
    {
        private readonly IGooglePlaySettlementTransport _transport;
        private readonly IGooglePlayGrantApplier _grantApplier;

        public GooglePlayReceiptSettlement(IGooglePlaySettlementTransport transport,
            IGooglePlayGrantApplier grantApplier)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _grantApplier = grantApplier ?? throw new ArgumentNullException(nameof(grantApplier));
        }

        public async Task<bool> SettleAsync(string sku, string productId, string receipt,
            string transactionId)
        {
            if (!GooglePlayProductCatalog.TryGetProductId(sku, out var expectedProduct) ||
                !string.Equals(expectedProduct, productId, StringComparison.Ordinal) ||
                !TryExtractPurchaseToken(receipt, productId, out var purchaseToken) ||
                !string.Equals(purchaseToken, transactionId, StringComparison.Ordinal))
                return false;

            GooglePlayVerifyReply verified;
            try { verified = await _transport.VerifyAsync(sku, productId, purchaseToken); }
            catch { return false; }
            if (verified == null || !verified.success || !IsSuccessfulServerState(verified.state) ||
                !string.Equals(verified.sku, sku, StringComparison.Ordinal))
                return false;

            if (!_grantApplier.IsApplied(purchaseToken))
            {
                bool applied;
                try { applied = await _grantApplier.ApplyExactlyOnceAsync(sku, purchaseToken); }
                catch { return false; }
                if (!applied) return false;
            }

            try { return await _transport.FulfillAsync(sku, productId, purchaseToken); }
            catch { return false; }
        }

        private static bool IsSuccessfulServerState(string state) =>
            string.Equals(state, "verified", StringComparison.Ordinal) ||
            string.Equals(state, "granted", StringComparison.Ordinal) ||
            string.Equals(state, "consumed", StringComparison.Ordinal) ||
            string.Equals(state, "acknowledged", StringComparison.Ordinal);

        public static bool TryExtractPurchaseToken(string receipt, string expectedProductId,
            out string purchaseToken)
        {
            purchaseToken = string.Empty;
            if (string.IsNullOrWhiteSpace(receipt) || string.IsNullOrWhiteSpace(expectedProductId))
                return false;
            try
            {
                // IAP v5 Order.Info.Receipt is the Google payload ({json,signature,...}).
                // Legacy Product.receipt wrapped that payload in {Store,Payload}; accept both,
                // but never accept a non-Google wrapper.
                string googlePayload = receipt;
                var envelope = JsonUtility.FromJson<UnityReceiptEnvelope>(receipt);
                if (envelope != null && (!string.IsNullOrWhiteSpace(envelope.Store) ||
                    !string.IsNullOrWhiteSpace(envelope.Payload)))
                {
                    if (!string.Equals(envelope.Store, "GooglePlay", StringComparison.Ordinal) ||
                        string.IsNullOrWhiteSpace(envelope.Payload)) return false;
                    googlePayload = envelope.Payload;
                }
                var payload = JsonUtility.FromJson<GooglePayloadEnvelope>(googlePayload);
                if (payload == null || string.IsNullOrWhiteSpace(payload.json)) return false;
                var purchase = JsonUtility.FromJson<GooglePurchase>(payload.json);
                if (purchase == null || !string.Equals(purchase.productId, expectedProductId,
                    StringComparison.Ordinal) || !IsTokenShape(purchase.purchaseToken)) return false;
                purchaseToken = purchase.purchaseToken;
                return true;
            }
            catch { return false; }
        }

        private static bool IsTokenShape(string token)
        {
            if (string.IsNullOrWhiteSpace(token) || token.Length < 20 || token.Length > 4096) return false;
            foreach (char c in token)
                if (!(char.IsLetterOrDigit(c) || ". _~+/=-".IndexOf(c) >= 0) || c == ' ') return false;
            return true;
        }

        [Serializable] private sealed class UnityReceiptEnvelope { public string Store; public string Payload; }
        [Serializable] private sealed class GooglePayloadEnvelope { public string json; }
        [Serializable] private sealed class GooglePurchase { public string productId; public string purchaseToken; }
    }

    /// <summary>
    /// Dormant HTTP transport. It cannot be constructed without a player identity and a callback
    /// that attaches an already-authenticated session; neither endpoint is called if attachment
    /// fails. The Play bootstrap deliberately does not compose this until Play account sessions
    /// and a durable grant applier exist.
    /// </summary>
    public sealed class GooglePlayBackendTransport : IGooglePlaySettlementTransport,
        IGooglePlayAccountBindingSource
    {
        public delegate bool SessionAttacher(UnityWebRequest request, string playerId);
        private const string BackendBase = "https://defenders-of-the-realm-v2.vercel.app";
        private readonly string _playerId;
        private readonly SessionAttacher _attachSession;

        public GooglePlayBackendTransport(string playerId, SessionAttacher attachSession)
        {
            _playerId = string.IsNullOrWhiteSpace(playerId)
                ? throw new ArgumentException("Authenticated player id is required.", nameof(playerId))
                : playerId;
            _attachSession = attachSession ?? throw new ArgumentNullException(nameof(attachSession));
        }

        public async Task<GooglePlayVerifyReply> VerifyAsync(string sku, string productId,
            string purchaseToken)
        {
            var response = await PostAsync("/api/purchases/google-play-verify",
                new PurchaseRequest(_playerId, sku, productId, purchaseToken));
            return string.IsNullOrWhiteSpace(response) ? null
                : JsonUtility.FromJson<GooglePlayVerifyReply>(response);
        }

        public async Task<string> FetchAccountBindingAsync()
        {
            var response = await PostAsync("/api/purchases/google-play-binding",
                new BindingRequest(_playerId));
            if (string.IsNullOrWhiteSpace(response)) return null;
            var reply = JsonUtility.FromJson<BindingReply>(response);
            return reply != null && reply.success && IsBindingShape(reply.accountBinding)
                ? reply.accountBinding : null;
        }

        public async Task<bool> FulfillAsync(string sku, string productId, string purchaseToken)
        {
            var response = await PostAsync("/api/purchases/google-play-fulfill",
                new PurchaseRequest(_playerId, sku, productId, purchaseToken));
            if (string.IsNullOrWhiteSpace(response)) return false;
            var reply = JsonUtility.FromJson<GooglePlayVerifyReply>(response);
            return reply != null && reply.success &&
                (reply.state == "consumed" || reply.state == "acknowledged") && reply.sku == sku;
        }

        private async Task<string> PostAsync(string path, object payload)
        {
            var bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(payload));
            using var request = new UnityWebRequest(BackendBase + path, UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(bytes),
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = 20
            };
            request.SetRequestHeader("Content-Type", "application/json");
            if (!_attachSession(request, _playerId)) return null;
            var operation = request.SendWebRequest();
            while (!operation.isDone) await Task.Yield();
            return request.result == UnityWebRequest.Result.Success
                ? request.downloadHandler.text : null;
        }

        private static bool IsBindingShape(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length != 64) return false;
            foreach (char c in value)
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'))) return false;
            return true;
        }

        [Serializable] private sealed class BindingRequest
        {
            public string playerId;
            public BindingRequest(string player) { playerId = player; }
        }
        [Serializable] private sealed class BindingReply
        {
            public bool success;
            public string accountBinding;
        }

        [Serializable]
        private sealed class PurchaseRequest
        {
            public string playerId;
            public string sku;
            public string productId;
            public string purchaseToken;
            public PurchaseRequest(string player, string pack, string product, string token)
            { playerId = player; sku = pack; productId = product; purchaseToken = token; }
        }
    }
}
