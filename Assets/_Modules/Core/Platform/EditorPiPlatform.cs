using System;
using Cysharp.Threading.Tasks;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Core.Platform
{
    /// <summary>
    /// Inert Pi platform for Editor / desktop / non-Pi web. IsAvailable=false so callers
    /// short-circuit; mirrors the offline-safe stub pattern (StubWalletProvider). Lets the
    /// whole game run unchanged where window.Pi does not exist.
    /// </summary>
    public sealed class EditorPiPlatform : IPiPlatform
    {
#pragma warning disable 67 // events are part of the seam; never raised in the stub.
        public event Action<string, string> OnApprovalReady;
        public event Action<string, string, string> OnCompletionReady;
#pragma warning restore 67

        public bool IsAvailable => false;

        public UniTask<bool> Init(bool sandbox)
        {
            FlowTrace.Step("Pi", "EditorPiPlatform.Init — Pi unavailable off-Pi-Browser (no-op).");
            return UniTask.FromResult(false);
        }

        public UniTask<PiAuthResult> Authenticate(string[] scopes)
        {
            FlowTrace.Warn("Pi", "Authenticate called off Pi Browser — returning Fail (stub).");
            return UniTask.FromResult(PiAuthResult.Fail("Pi unavailable (not in Pi Browser)"));
        }

        public UniTask<PiPaymentResult> CreatePayment(string paymentId, double amount, string memo, string metadataJson)
        {
            FlowTrace.Warn("Pi", "CreatePayment called off Pi Browser — returning Error (stub).");
            return UniTask.FromResult(PiPaymentResult.Fail(paymentId, "Pi unavailable (not in Pi Browser)"));
        }

        public UniTask<bool> ShowAd(string adType)
        {
            FlowTrace.Step("Pi", "ShowAd called off Pi Browser — no-op (stub).");
            return UniTask.FromResult(false);
        }
    }
}
