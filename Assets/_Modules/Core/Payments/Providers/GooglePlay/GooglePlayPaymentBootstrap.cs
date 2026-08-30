using UnityEngine;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Core.Payments.Providers
{
    /// <summary>
    /// Self-registering composition root for the Google Play rail. Runs ONLY on an artifact whose
    /// stamped channel is GooglePlay (the GOOGLE_PLAY define); on a Seeker/dApp-Store or WebGL
    /// artifact it returns on the first line and nothing below it executes.
    /// </summary>
    /// <remarks>
    /// ⛔ SETTLEMENT IS CONFIGURED BEFORE THE STORE IS CONNECTED, AND THAT ORDER IS DELIBERATE.
    /// Until WO-1282, <c>GooglePlayBillingProvider.ConfigureSettlement</c> had no caller anywhere in
    /// the tree, so <c>VerifyAndGrantAsync</c> stayed null and every Play purchase would have been
    /// taken and never granted. Configuring it here — before <c>Initialize()</c> opens the store
    /// connection — means the provider can never reach a PendingOrder without a settlement path.
    /// If the chain cannot be built the provider stays UNCONFIGURED, <c>CanBuy</c> refuses every SKU,
    /// and the store sells nothing. That refusal is the feature.
    /// </remarks>
    internal static class GooglePlayPaymentBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterForStampedArtifact()
        {
            if (PaymentChannelResolver.Current != PaymentChannel.GooglePlay) return;

            var provider = new GooglePlayBillingProvider();
            PaymentProviders.Register(provider, PaymentChannel.GooglePlay);

            if (!GooglePlaySettlementComposer.TryConfigure(provider))
                FlowTrace.Fail("PlayBilling",
                    "Google Play billing registered WITHOUT settlement — the store will refuse every " +
                    "purchase. Failing closed on purpose: an unsettleable sale charges the player and " +
                    "grants nothing.");

            provider.Initialize();
        }
    }
}
