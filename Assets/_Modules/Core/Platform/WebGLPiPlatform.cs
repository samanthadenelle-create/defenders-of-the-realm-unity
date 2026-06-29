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

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern int  PiIsAvailable();
        [DllImport("__Internal")] private static extern void PiInit(int sandbox);
        [DllImport("__Internal")] private static extern void PiAuthenticate(string scopesCsv);
        [DllImport("__Internal")] private static extern void PiCreatePayment(string paymentId, double amount, string memo, string metadataJson);
        [DllImport("__Internal")] private static extern void PiShowAd(string adType);
#else
        private static int  PiIsAvailable() => 0;
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
                    OnApprovalReady?.Invoke(cb.paymentId, d.piPaymentId);
                    break;
                case "completionReady":
                    OnCompletionReady?.Invoke(cb.paymentId, d.piPaymentId, d.txid);
                    ResolvePayment(cb.paymentId, new PiPaymentResult {
                        Status = PiPaymentStatus.Completed, PaymentId = cb.paymentId,
                        PiPaymentId = d.piPaymentId, Txid = d.txid });
                    break;
                case "adReady":
                    _adTcs?.TrySetResult(true);
                    break;
                case "cancelled":
                    ResolvePayment(cb.paymentId, new PiPaymentResult {
                        Status = PiPaymentStatus.Cancelled, PaymentId = cb.paymentId, PiPaymentId = d.piPaymentId });
                    break;
                case "error":
                    FlowTrace.Warn("Pi", $"Pi error: {d.message} (id={cb.paymentId})");
                    if (!string.IsNullOrEmpty(cb.paymentId) && _payments.ContainsKey(cb.paymentId))
                        ResolvePayment(cb.paymentId, PiPaymentResult.Fail(cb.paymentId, d.message));
                    else if (_authTcs != null) { _authTcs.TrySetResult(PiAuthResult.Fail(d.message)); _authTcs = null; }
                    else if (_initTcs != null) { _initTcs.TrySetResult(false); _initTcs = null; }
                    else if (_adTcs != null) { _adTcs.TrySetResult(false); _adTcs = null; }
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
