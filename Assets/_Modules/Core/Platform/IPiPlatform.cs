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

        /// <summary>
        /// Pi.Ads.showAd("rewarded"|"interstitial").
        ///
        /// ⛔ WO-1320 — THIS USED TO RETURN <c>UniTask&lt;bool&gt;</c> AND THAT WAS A LATENT FREE
        /// REWARD. A bool cannot carry <c>adId</c>, and adId is the ONLY token the backend can
        /// verify a rewarded view with; without it the client's own word is the sole evidence,
        /// which the Pi docs explicitly refuse ("you must verify the rewarded status of the ad
        /// using Pi Platform API, before rewarding users", because players may run hacked SDK
        /// builds). Worse, the bool was produced by a callback handler that answered <c>true</c>
        /// for EVERY outcome. Nothing called ShowAd, so this never paid out - it was a defect
        /// waiting for its first caller. The struct is the fix, and it mirrors how the payment
        /// path already returns <see cref="PiPaymentResult"/> rather than a bool.
        ///
        /// The returned <see cref="PiAdResult.Result"/> is the SDK's string VERBATIM. Callers
        /// compare it against <see cref="PiAdResults"/>; anything unrecognised is logged as-is
        /// and treated as a generic failure. Never grant on this result alone.
        /// </summary>
        UniTask<PiAdResult> ShowAd(string adType);

        /// <summary>
        /// Pi.Ads.isAdReady(type) -> { ready }. Exists because <c>IAdService.IsRewardedReady</c>
        /// is a SYNCHRONOUS property: the provider polls this and caches the answer, so the UI
        /// can lead with availability instead of offering a button that fails after the tap.
        /// </summary>
        UniTask<bool> IsAdReady(string adType);

        /// <summary>
        /// Pi.Ads.requestAd(type) -> { result }. The documented ADVANCED preload path. Pi Browser
        /// preloads internally, so this is an optimisation, not a precondition for ShowAd.
        /// </summary>
        UniTask<PiAdResult> RequestAd(string adType);

        /// <summary>
        /// Pi.nativeFeaturesList(). The documented feature probe; <c>"ad_network"</c> in the list
        /// is the gate for the Pi Ad Network. Returns an EMPTY array (never null) when the SDK is
        /// absent or the call fails, so an absent feature and a failed probe are both "no".
        /// </summary>
        UniTask<string[]> NativeFeatures();

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

    /// <summary>
    /// The CONFIRMED Pi Ads result strings, and deliberately ONLY those.
    ///
    /// ⛔ THIS IS A CONSTANTS CLASS AND NOT AN ENUM, ON PURPOSE (WO-1320). The Pi docs do not
    /// publish an exhaustive list; these four were verified from the fetched documentation on
    /// 2026-09-02 and nothing else was. An enum would claim completeness we do not have, and the
    /// first undocumented string would then either fail to parse or, far worse, be mapped to
    /// whichever enum member happened to be zero. An in-repo work order additionally claimed
    /// "AD_NOT_AVAILABLE"; that string could NOT be confirmed and is therefore absent here.
    ///
    /// The rule for anything not listed: log it VERBATIM via FlowTrace.Warn and treat it as a
    /// generic failure that grants nothing. Add a constant here only after reading it in a doc.
    /// </summary>
    public static class PiAdResults
    {
        /// <summary>requestAd: an ad is loaded and ready to show.</summary>
        public const string AdLoaded = "AD_LOADED";

        /// <summary>showAd: the player watched to the reward threshold. NOT sufficient to grant.</summary>
        public const string AdRewarded = "AD_REWARDED";

        /// <summary>showAd: the player dismissed the ad. Grants nothing, ever.</summary>
        public const string AdClosed = "AD_CLOSED";

        /// <summary>The Pi client cannot serve ads at all (old app version, unsupported platform).</summary>
        public const string AdsNotSupported = "ADS_NOT_SUPPORTED";
    }

    /// <summary>
    /// One Pi Ads call's outcome. <see cref="Result"/> is the SDK's string UNTOUCHED - it is not
    /// normalised, upper-cased or mapped on the way in, because the diagnostic value of an
    /// unrecognised result is entirely in its exact text.
    /// </summary>
    [Serializable]
    public struct PiAdResult
    {
        /// <summary>True when the SDK answered at all. False = bridge error, timeout, or no Pi.Ads.</summary>
        public bool Ok;

        /// <summary>The SDK result string, verbatim. Empty when <see cref="Ok"/> is false.</summary>
        public string Result;

        /// <summary>
        /// The rewarded-ad token the BACKEND verifies. Documented as present on rewarded ads only.
        /// Empty means ungrantable: with no adId there is nothing /api/pi/ads-verify can check,
        /// and an unverifiable reward is refused rather than assumed.
        /// </summary>
        public string AdId;

        /// <summary>Bridge-level failure text (timeout, missing Pi.Ads, SDK rejection). Never a result string.</summary>
        public string Error;

        /// <summary>
        /// The CLIENT claims the reward was earned. This is a NECESSARY, NEVER SUFFICIENT
        /// condition: the grant additionally requires /api/pi/ads-verify to answer
        /// mediator_ack_status == "granted". Named "Claims" so no call site can read it as proof.
        /// </summary>
        public bool ClaimsRewarded =>
            Ok && string.Equals(Result, PiAdResults.AdRewarded, StringComparison.Ordinal);

        /// <summary>True for the confirmed "player dismissed it" result.</summary>
        public bool IsClosed =>
            Ok && string.Equals(Result, PiAdResults.AdClosed, StringComparison.Ordinal);

        /// <summary>True for the confirmed "this client cannot serve ads" result.</summary>
        public bool IsNotSupported =>
            Ok && string.Equals(Result, PiAdResults.AdsNotSupported, StringComparison.Ordinal);

        /// <summary>True when the SDK answered with a string none of the four confirmed values match.</summary>
        public bool IsUnrecognised =>
            Ok && !ClaimsRewarded && !IsClosed && !IsNotSupported &&
            !string.Equals(Result, PiAdResults.AdLoaded, StringComparison.Ordinal);

        public static PiAdResult Fail(string err) =>
            new PiAdResult { Ok = false, Result = string.Empty, AdId = string.Empty, Error = err };

        public override string ToString() =>
            Ok ? $"result={Result} adId={(string.IsNullOrEmpty(AdId) ? "<none>" : AdId)}"
               : $"failed: {Error}";
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
