using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Cysharp.Threading.Tasks;
using UnityEngine;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Core.Platform
{
    /// <summary>
    /// Real Pi platform for WebGL-in-Pi-Browser. Calls PiBridge.jslib via [DllImport("__Internal")]
    /// and receives results through a persistent "PiBridge" GameObject (SendMessage → OnPiCallback).
    /// Off WebGL the externs are stubbed out and IsAvailable=false (EditorPiPlatform is used instead).
    /// Contract: PI_INTEGRATION_SPEC.md §2.
    /// </summary>
    public sealed class WebGLPiPlatform : IPiPlatform
    {
        public event Action<string, string> OnApprovalReady;
        public event Action<string, string, string> OnCompletionReady;
        public event Action<PiIncompletePayment> OnIncompletePaymentFound;

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern int  PiIsAvailable();
        [DllImport("__Internal")] private static extern int  PiIsPiBrowser();
        [DllImport("__Internal")] private static extern void PiInit(int sandbox);
        [DllImport("__Internal")] private static extern void PiAuthenticate(string scopesCsv);
        [DllImport("__Internal")] private static extern void PiCreatePayment(string paymentId, double amount, string memo, string metadataJson);
        [DllImport("__Internal")] private static extern void PiShowAd(string adType, int timeoutMs);
        [DllImport("__Internal")] private static extern void PiIsAdReady(string adType, int timeoutMs);
        [DllImport("__Internal")] private static extern void PiRequestAd(string adType, int timeoutMs);
        [DllImport("__Internal")] private static extern void PiNativeFeatures(int timeoutMs);
#else
        private static int  PiIsAvailable() => 0;
        private static int  PiIsPiBrowser() => 0;
        private static void PiInit(int sandbox) { }
        private static void PiAuthenticate(string scopesCsv) { }
        private static void PiCreatePayment(string paymentId, double amount, string memo, string metadataJson) { }
        private static void PiShowAd(string adType, int timeoutMs) { }
        private static void PiIsAdReady(string adType, int timeoutMs) { }
        private static void PiRequestAd(string adType, int timeoutMs) { }
        private static void PiNativeFeatures(int timeoutMs) { }
#endif

        // ── WO-1320: LOCAL TIMEOUTS ON EVERY AD CALL, in ONE place ──────────────────────────
        // WO-678 established that outside Pi Browser the SDK's host channel never answers and the
        // promise hangs ~120s before it rejects. ShowAd had NO timeout, so a caller awaiting it
        // never resumed. These budgets are passed DOWN to the jslib guard rather than being
        // duplicated in JS, so the number that decides how long a player waits lives here, once.
        //
        // ShowAd's is generous because a rewarded video legitimately runs for a while and the
        // player is watching it; the probes are short because they are meant to answer instantly
        // and a slow probe is indistinguishable from a missing one.
        private const int ShowAdTimeoutMs = 180_000;
        private const int ProbeTimeoutMs = 15_000;

        private UniTaskCompletionSource<bool> _initTcs;
        private UniTaskCompletionSource<PiAuthResult> _authTcs;
        private UniTaskCompletionSource<PiAdResult> _adShowTcs;
        private UniTaskCompletionSource<bool> _adReadyTcs;
        private UniTaskCompletionSource<PiAdResult> _adRequestTcs;
        private UniTaskCompletionSource<string[]> _featuresTcs;
        private readonly Dictionary<string, UniTaskCompletionSource<PiPaymentResult>> _payments
            = new Dictionary<string, UniTaskCompletionSource<PiPaymentResult>>();

        public WebGLPiPlatform()
        {
            PiBridgeReceiver.Ensure(this);
        }

        public bool IsAvailable
        {
            get { try { return PiIsAvailable() != 0; } catch { return false; } }
        }

        /// <summary>
        /// WO-678 Lane C: true only in the real Pi Browser app (UA token check in the jslib).
        /// IsAvailable merely means pi-sdk.js loaded and window.Pi exists — TRUE in any browser.
        /// Only in the actual Pi Browser does the SDK's host channel ever answer; calling
        /// Pi.init anywhere else spawns a doomed promise the SDK rejects after 120s.
        /// Static (not on IPiPlatform): it is a WebGL/browser-environment fact, and the
        /// off-WebGL stub returns 0 so it is simply false wherever EditorPiPlatform runs.
        /// </summary>
        public static bool IsPiBrowserEnvironment
        {
            get { try { return PiIsPiBrowser() != 0; } catch { return false; } }
        }

        public UniTask<bool> Init(bool sandbox)
        {
            if (!IsAvailable) return UniTask.FromResult(false);
            _initTcs = new UniTaskCompletionSource<bool>();
            FlowTrace.Step("Pi", $"PiInit(sandbox={sandbox})");
            PiInit(sandbox ? 1 : 0);
            return _initTcs.Task;
        }

        public UniTask<PiAuthResult> Authenticate(string[] scopes)
        {
            if (!IsAvailable) return UniTask.FromResult(PiAuthResult.Fail("Pi unavailable"));
            _authTcs = new UniTaskCompletionSource<PiAuthResult>();
            string csv = (scopes == null || scopes.Length == 0) ? "username" : string.Join(",", scopes);
            FlowTrace.Step("Pi", $"PiAuthenticate(scopes={csv})");
            PiAuthenticate(csv);
            return _authTcs.Task;
        }

        public UniTask<PiPaymentResult> CreatePayment(string paymentId, double amount, string memo, string metadataJson)
        {
            if (!IsAvailable) return UniTask.FromResult(PiPaymentResult.Fail(paymentId, "Pi unavailable"));
            var tcs = new UniTaskCompletionSource<PiPaymentResult>();
            _payments[paymentId] = tcs;
            FlowTrace.Step("Pi", $"PiCreatePayment(id={paymentId}, amount={amount})");
            PiCreatePayment(paymentId, amount, memo ?? "", metadataJson ?? "{}");
            return tcs.Task;
        }

        public UniTask<PiAdResult> ShowAd(string adType)
        {
            if (!IsAvailable) return UniTask.FromResult(PiAdResult.Fail("Pi unavailable"));
            string type = string.IsNullOrEmpty(adType) ? "rewarded" : adType;
            _adShowTcs = new UniTaskCompletionSource<PiAdResult>();
            FlowTrace.Step("PiAds", $"PI_AD_SHOW_CALL type={type} timeoutMs={ShowAdTimeoutMs}");
            PiShowAd(type, ShowAdTimeoutMs);
            return _adShowTcs.Task;
        }

        public UniTask<bool> IsAdReady(string adType)
        {
            if (!IsAvailable) return UniTask.FromResult(false);
            string type = string.IsNullOrEmpty(adType) ? "rewarded" : adType;
            _adReadyTcs = new UniTaskCompletionSource<bool>();
            FlowTrace.Step("PiAds", $"PI_AD_ISREADY_CALL type={type}");
            PiIsAdReady(type, ProbeTimeoutMs);
            return _adReadyTcs.Task;
        }

        public UniTask<PiAdResult> RequestAd(string adType)
        {
            if (!IsAvailable) return UniTask.FromResult(PiAdResult.Fail("Pi unavailable"));
            string type = string.IsNullOrEmpty(adType) ? "rewarded" : adType;
            _adRequestTcs = new UniTaskCompletionSource<PiAdResult>();
            FlowTrace.Step("PiAds", $"PI_AD_REQUEST_CALL type={type}");
            PiRequestAd(type, ProbeTimeoutMs);
            return _adRequestTcs.Task;
        }

        public UniTask<string[]> NativeFeatures()
        {
            if (!IsAvailable) return UniTask.FromResult(Array.Empty<string>());
            _featuresTcs = new UniTaskCompletionSource<string[]>();
            FlowTrace.Step("PiAds", "PI_NATIVE_FEATURES_CALL");
            PiNativeFeatures(ProbeTimeoutMs);
            return _featuresTcs.Task;
        }

        // Invoked by PiBridgeReceiver on the main thread (Unity SendMessage).
        internal void HandleCallback(string json)
        {
            PiCallback cb;
            try { cb = JsonUtility.FromJson<PiCallback>(json); }
            catch (Exception e) { FlowTrace.Fail("Pi", $"OnPiCallback parse failed: {e.Message}"); return; }
            if (cb == null) return;
            var d = cb.data ?? new PiCallbackData();

            switch (cb.type)
            {
                case "ready":
                    _initTcs?.TrySetResult(true);
                    break;
                case "auth":
                    _authTcs?.TrySetResult(new PiAuthResult {
                        Ok = !string.IsNullOrEmpty(d.accessToken),
                        AccessToken = d.accessToken, Uid = d.uid, Username = d.username
                    });
                    break;
                case "approvalReady":
                    FlowTrace.Step("PiPay", $"onReadyForServerApproval corr={cb.paymentId} piPaymentId={d.piPaymentId}");
                    OnApprovalReady?.Invoke(cb.paymentId, d.piPaymentId);
                    break;
                case "completionReady":
                    FlowTrace.Step("PiPay", $"onReadyForServerCompletion corr={cb.paymentId} piPaymentId={d.piPaymentId} txid={d.txid}");
                    OnCompletionReady?.Invoke(cb.paymentId, d.piPaymentId, d.txid);
                    ResolvePayment(cb.paymentId, new PiPaymentResult {
                        Status = PiPaymentStatus.Completed, PaymentId = cb.paymentId,
                        PiPaymentId = d.piPaymentId, Txid = d.txid });
                    break;
                case "incompletePaymentFound":
                    // WO-1318: the player already paid; nothing was granted. This is the ONLY signal
                    // we ever get for it. Warn (not Step) so it is visible at a glance in a capture --
                    // it always means a previous session dropped a settled payment.
                    FlowTrace.Warn("PiPay", $"onIncompletePaymentFound piPaymentId={d.piPaymentId} txid={(string.IsNullOrEmpty(d.txid) ? "<none>" : d.txid)} sku={d.sku} quoteId={d.quoteId}");
                    OnIncompletePaymentFound?.Invoke(new PiIncompletePayment {
                        PiPaymentId = d.piPaymentId, Txid = d.txid, Sku = d.sku,
                        QuoteId = d.quoteId, CorrelationId = d.correlationId });
                    break;
                // ⛔ WO-1320 — THIS CASE WAS THE LATENT FREE REWARD.
                // It read `case "adReady": _adTcs?.TrySetResult(true);` — i.e. it resolved TRUE on
                // the mere ARRIVAL of a callback, never looking at what the ad actually did. The
                // reason it could not look is one line further down: PiCallbackData declared no
                // `result` and no `adId`, so JsonUtility dropped both in silence and there was
                // nothing to look AT. AD_CLOSED, ADS_NOT_SUPPORTED and a dismissed rewarded ad all
                // resolved as "rewarded". Nothing has ever called ShowAd, which is the ONLY reason
                // this never paid out.
                //
                // Now the jslib sends flat `adResult` / `adId` strings and the outcome is decided
                // from them. The result is passed through VERBATIM — this layer classifies nothing
                // and grants nothing; that is PiAdGrantDecision's job, behind the server check.
                case "adShown":
                case "adReady":   // legacy type name, kept so an older cached jslib cannot hang a caller
                    LogAdResult("showAd", d);
                    _adShowTcs?.TrySetResult(new PiAdResult {
                        Ok = true, Result = d.adResult ?? string.Empty,
                        AdId = d.adId ?? string.Empty, Error = null });
                    _adShowTcs = null;
                    break;
                case "adRequested":
                    LogAdResult("requestAd", d);
                    _adRequestTcs?.TrySetResult(new PiAdResult {
                        Ok = true, Result = d.adResult ?? string.Empty,
                        AdId = d.adId ?? string.Empty, Error = null });
                    _adRequestTcs = null;
                    break;
                case "adReadyCheck":
                    FlowTrace.Step("PiAds", $"PI_AD_ISREADY_RESULT type={d.adType} ready={d.adReady}");
                    _adReadyTcs?.TrySetResult(d.adReady);
                    _adReadyTcs = null;
                    break;
                case "nativeFeatures":
                    string csv = d.featuresCsv ?? string.Empty;
                    string[] features = csv.Length == 0
                        ? Array.Empty<string>()
                        : csv.Split(',');
                    for (int i = 0; i < features.Length; i++) features[i] = features[i].Trim();
                    FlowTrace.Step("PiAds", $"PI_NATIVE_FEATURES_RESULT count={features.Length} list=[{csv}]");
                    _featuresTcs?.TrySetResult(features);
                    _featuresTcs = null;
                    break;
                case "cancelled":
                    FlowTrace.Warn("PiPay", $"onCancel corr={cb.paymentId} piPaymentId={d.piPaymentId} (player dismissed the Pi payment sheet)");
                    ResolvePayment(cb.paymentId, new PiPaymentResult {
                        Status = PiPaymentStatus.Cancelled, PaymentId = cb.paymentId, PiPaymentId = d.piPaymentId });
                    break;
                case "error":
                    // WO-678 Lane A→B: the template's global unhandledrejection handler forwards the
                    // suppressed benign SDK rejection (the 120s host-channel timeout) tagged
                    // where:'sdk-global'. It is telemetry only — Warn, never Fail/LogError (must not
                    // land in break-log.jsonl / the F8 recorder), and it must never settle a TCS
                    // (a genuinely in-flight init/auth keeps waiting for ITS OWN callback).
                    if (d.where == "sdk-global")
                    {
                        FlowTrace.Warn("Pi", $"SDK global rejection (suppressed in template, expected outside Pi Browser): {d.message}");
                        break;
                    }
                    FlowTrace.Warn("Pi", $"Pi error: {d.message} (id={cb.paymentId}, where={d.where})");

                    // WO-1320 — ROUTE AD ERRORS BY `where`, BEFORE the payment/auth/init chain.
                    // The old chain settled _adTcs only as the LAST `else if`, so a showAd timeout
                    // arriving while an auth was in flight silently cancelled the AUTH instead and
                    // left the ad caller hanging forever. The jslib tags every ad failure with a
                    // `where` of 'showAd' / 'isAdReady' / 'requestAd' / 'nativeFeatures' (plus a
                    // '-timeout' suffix for the local guard), so the right waiter can be settled
                    // by NAME rather than by position in an if-chain.
                    if (!string.IsNullOrEmpty(d.where) && ResolveAdError(d.where, d.message))
                        break;

                    if (!string.IsNullOrEmpty(cb.paymentId) && _payments.ContainsKey(cb.paymentId))
                        ResolvePayment(cb.paymentId, PiPaymentResult.Fail(cb.paymentId, d.message));
                    else if (_authTcs != null) { _authTcs.TrySetResult(PiAuthResult.Fail(d.message)); _authTcs = null; }
                    else if (_initTcs != null) { _initTcs.TrySetResult(false); _initTcs = null; }
                    else
                        // WO-678 Lane B: an error callback with no live consumer = the SDK's own late
                        // rejection arriving after our 20s/30s local timeouts already settled the flow.
                        // Expected noise outside Pi Browser — Warn only, never Fail (stays out of break-log).
                        FlowTrace.Warn("Pi", "late SDK callback after local timeout — ignored (expected outside Pi Browser)");
                    break;
                default:
                    FlowTrace.Warn("Pi", $"Unknown Pi callback type: {cb.type}");
                    break;
            }
        }

        /// <summary>
        /// WO-1320 — log one Pi Ads result VERBATIM, and flag the unrecognised ones loudly.
        ///
        /// The Pi docs do not publish an exhaustive result vocabulary; four strings are confirmed
        /// (AD_LOADED / AD_REWARDED / AD_CLOSED / ADS_NOT_SUPPORTED) and nothing else is. So an
        /// unknown string is not swallowed, not normalised, and above all not guessed at — it is
        /// printed exactly as the SDK sent it, which is the only way the next seat learns the real
        /// vocabulary instead of re-deriving it from another work order's memory.
        /// </summary>
        private static void LogAdResult(string call, PiCallbackData d)
        {
            string result = d.adResult ?? string.Empty;
            bool known =
                result == PiAdResults.AdLoaded || result == PiAdResults.AdRewarded ||
                result == PiAdResults.AdClosed || result == PiAdResults.AdsNotSupported;

            string line = $"PI_AD_RESULT call={call} type={d.adType} result='{result}' " +
                          $"adId={(string.IsNullOrEmpty(d.adId) ? "<none>" : d.adId)}";

            if (known) FlowTrace.Step("PiAds", line);
            else FlowTrace.Warn("PiAds", line + " <- UNRECOGNISED result string; not one of the four " +
                                "confirmed Pi Ads values. Treated as a generic failure (no grant). " +
                                "If this string is real, confirm it in the Pi docs before adding it " +
                                "to PiAdResults.");
        }

        /// <summary>
        /// Settle whichever ad call the jslib's `where` names. Returns true when it handled the
        /// error, so the caller stops before the payment/auth/init chain claims it.
        /// A timeout arrives as '<call>-timeout', hence StartsWith rather than an equality test.
        /// </summary>
        private bool ResolveAdError(string where, string message)
        {
            string err = string.IsNullOrEmpty(message) ? "(no message)" : message;

            if (where.StartsWith("showAd", StringComparison.Ordinal))
            {
                if (_adShowTcs == null) return false;
                FlowTrace.Warn("PiAds", $"PI_AD_SHOW_FAILED where={where}: {err}. No adId, so nothing is grantable.");
                _adShowTcs.TrySetResult(PiAdResult.Fail(err));
                _adShowTcs = null;
                return true;
            }
            if (where.StartsWith("isAdReady", StringComparison.Ordinal))
            {
                if (_adReadyTcs == null) return false;
                FlowTrace.Warn("PiAds", $"PI_AD_ISREADY_FAILED where={where}: {err}. Reported as NOT ready.");
                _adReadyTcs.TrySetResult(false);
                _adReadyTcs = null;
                return true;
            }
            if (where.StartsWith("requestAd", StringComparison.Ordinal))
            {
                if (_adRequestTcs == null) return false;
                FlowTrace.Warn("PiAds", $"PI_AD_REQUEST_FAILED where={where}: {err}");
                _adRequestTcs.TrySetResult(PiAdResult.Fail(err));
                _adRequestTcs = null;
                return true;
            }
            if (where.StartsWith("nativeFeatures", StringComparison.Ordinal))
            {
                if (_featuresTcs == null) return false;
                FlowTrace.Warn("PiAds", $"PI_NATIVE_FEATURES_FAILED where={where}: {err}. Empty list - " +
                                        "the ad provider will not register.");
                _featuresTcs.TrySetResult(Array.Empty<string>());
                _featuresTcs = null;
                return true;
            }
            return false;
        }

        private void ResolvePayment(string paymentId, PiPaymentResult result)
        {
            if (string.IsNullOrEmpty(paymentId)) return;
            if (_payments.TryGetValue(paymentId, out var tcs))
            {
                tcs.TrySetResult(result);
                _payments.Remove(paymentId);
            }
        }

        [Serializable] private class PiCallback { public string type; public string paymentId; public PiCallbackData data; }
        [Serializable] private class PiCallbackData
        {
            public string accessToken; public string uid; public string username;
            public string piPaymentId; public string txid; public string message;
            public string where; // jslib always sends it; 'sdk-global' = template-forwarded benign rejection (WO-678)
            // WO-1318 — carried on 'incompletePaymentFound' only (read off the payment's metadata).
            public string sku; public string quoteId; public string correlationId;

            // ⛔ WO-1320 — THE FIELDS WHOSE ABSENCE WAS THE BUG.
            // The jslib had always sent the ad outcome, but as `result: <the SDK's object>`
            // nested inside `data`. JsonUtility cannot deserialise an unknown object shape and
            // drops it WITHOUT AN ERROR, and no `result`/`adId` field was declared here anyway —
            // so the C# side literally could not see what the ad did and answered `true` for
            // everything. These are flat strings for exactly that reason. Do not nest anything
            // into `data` on the JS side; it will vanish the same way.
            public string adType;
            public string adResult;    // the SDK string, verbatim - never parsed in the jslib
            public string adId;        // rewarded only; empty = unverifiable = ungrantable
            public bool adReady;       // isAdReady's { ready }
            public string featuresCsv; // nativeFeaturesList(), comma-separated
        }

        /// <summary>The persistent "PiBridge" GameObject that Unity SendMessage targets.</summary>
        private sealed class PiBridgeReceiver : MonoBehaviour
        {
            private static PiBridgeReceiver _instance;
            private WebGLPiPlatform _owner;

            public static void Ensure(WebGLPiPlatform owner)
            {
                if (_instance == null)
                {
                    var go = new GameObject("PiBridge");
                    DontDestroyOnLoad(go);
                    _instance = go.AddComponent<PiBridgeReceiver>();
                }
                _instance._owner = owner;
            }

            // Name MUST match the .jslib SendMessage("PiBridge","OnPiCallback", json).
            public void OnPiCallback(string json) => _owner?.HandleCallback(json);
        }
    }
}
