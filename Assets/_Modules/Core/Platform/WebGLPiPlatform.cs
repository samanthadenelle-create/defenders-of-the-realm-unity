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
        [DllImport("__Internal")] private static extern void PiShowAd(string adType);
#else
        private static int  PiIsAvailable() => 0;
        private static int  PiIsPiBrowser() => 0;
        private static void PiInit(int sandbox) { }
        private static void PiAuthenticate(string scopesCsv) { }
        private static void PiCreatePayment(string paymentId, double amount, string memo, string metadataJson) { }
        private static void PiShowAd(string adType) { }
#endif

        private UniTaskCompletionSource<bool> _initTcs;
        private UniTaskCompletionSource<PiAuthResult> _authTcs;
        private UniTaskCompletionSource<bool> _adTcs;
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

        public UniTask<bool> ShowAd(string adType)
        {
            if (!IsAvailable) return UniTask.FromResult(false);
            _adTcs = new UniTaskCompletionSource<bool>();
            FlowTrace.Step("Pi", $"PiShowAd({adType})");
            PiShowAd(string.IsNullOrEmpty(adType) ? "rewarded" : adType);
            return _adTcs.Task;
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
                case "adReady":
                    _adTcs?.TrySetResult(true);
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
                    if (!string.IsNullOrEmpty(cb.paymentId) && _payments.ContainsKey(cb.paymentId))
                        ResolvePayment(cb.paymentId, PiPaymentResult.Fail(cb.paymentId, d.message));
                    else if (_authTcs != null) { _authTcs.TrySetResult(PiAuthResult.Fail(d.message)); _authTcs = null; }
                    else if (_initTcs != null) { _initTcs.TrySetResult(false); _initTcs = null; }
                    else if (_adTcs != null) { _adTcs.TrySetResult(false); _adTcs = null; }
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
