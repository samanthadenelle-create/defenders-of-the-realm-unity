using System;
using Cysharp.Threading.Tasks;

namespace DeNelle.Core.Platform
{
    /// <summary>
    /// Platform seam for the Pi Network SDK (Pi Browser). Mirrors the ISaveProvider /
    /// ISkrLedger pattern: gameplay never references Pi directly — it resolves IPiPlatform
    /// and gets a WebGL-real impl inside Pi Browser or an inert Editor/stub everywhere else.
    /// Contract: PI_INTEGRATION_SPEC.md §2. The .jslib bridge marshals results back via
    /// SendMessage("PiBridge","OnPiCallback", json); WebGLPiPlatform owns that GameObject.
    /// V2-gated — no gameplay path calls this until the Phase-0 mobile-WebGL gate passes.
    /// </summary>
    public interface IPiPlatform
    {
        /// <summary>True only inside Pi Browser (window.Pi present). False in Editor / desktop / non-Pi web.</summary>
        bool IsAvailable { get; }

        /// <summary>Pi.init({version:"2.0", sandbox}). sandbox=true → Testnet sandbox. Idempotent.</summary>
        UniTask<bool> Init(bool sandbox);

        /// <summary>Pi.authenticate(scopes). Backend MUST re-verify accessToken via api.minepi.com/v2/me.</summary>
        UniTask<PiAuthResult> Authenticate(string[] scopes);

        /// <summary>
        /// Pi.createPayment. paymentId is OUR correlation id. The U2A flow fires server-approval
        /// then server-completion callbacks; subscribe to OnApprovalReady / OnCompletionReady to
        /// run the /approve + /complete backend handshake. The returned task resolves on a terminal
        /// state (Completed / Cancelled / Error).
        /// </summary>
        UniTask<PiPaymentResult> CreatePayment(string paymentId, double amount, string memo, string metadataJson);

        /// <summary>Pi.Ads.showAd("rewarded"|"interstitial"). true = ad completed/rewarded.</summary>
        UniTask<bool> ShowAd(string adType);

        /// <summary>Raised on onReadyForServerApproval — the orchestrator must POST /approve. (correlationId, piPaymentId)</summary>
        event Action<string, string> OnApprovalReady;

        /// <summary>Raised on onReadyForServerCompletion — the orchestrator must POST /complete. (correlationId, piPaymentId, txid)</summary>
        event Action<string, string, string> OnCompletionReady;

        /// <summary>
        /// WO-1318 — MANDATORY Pi SDK callback. Raised on Pi.authenticate's onIncompletePaymentFound:
        /// the player HAS ALREADY PAID for a payment we never finished settling. The orchestrator must
        /// drive it to completion through the backend (approve if it has no txid yet, then complete).
        /// Never ignore it: a dropped incomplete payment is a player who paid and got nothing.
        /// It fires on EVERY authenticate, so both sign-in and the payments-scoped re-auth surface it.
        /// </summary>
        event Action<PiIncompletePayment> OnIncompletePaymentFound;
    }

    /// <summary>
    /// A Pi payment the SDK reports as still in flight from a previous session. `Txid` is empty when
    /// the payment never reached the blockchain (needs server APPROVAL first); non-empty means it was
    /// submitted and only the server COMPLETION is missing. `QuoteId`/`Sku` come from the metadata we
    /// attached at createPayment, so the backend can re-validate the amount it originally quoted.
    /// </summary>
    [Serializable]
    public struct PiIncompletePayment
    {
        public string PiPaymentId;
        public string Txid;
        public string Sku;
        public string QuoteId;
        public string CorrelationId;

        public bool HasTxid => !string.IsNullOrEmpty(Txid);
    }

    public enum PiPaymentStatus { Completed, Cancelled, Error, Pending }

    [Serializable]
    public struct PiAuthResult
    {
        public bool Ok;
        public string AccessToken;
        public string Uid;
        public string Username;
        public string Error;

        public static PiAuthResult Fail(string err) => new PiAuthResult { Ok = false, Error = err };
    }

    [Serializable]
    public struct PiPaymentResult
    {
        public PiPaymentStatus Status;
        public string PaymentId;     // our correlation id
        public string PiPaymentId;   // Pi's identifier
        public string Txid;
        public string Error;

        public bool Ok => Status == PiPaymentStatus.Completed;
        public static PiPaymentResult Fail(string id, string err) =>
            new PiPaymentResult { Status = PiPaymentStatus.Error, PaymentId = id, Error = err };
    }
}
