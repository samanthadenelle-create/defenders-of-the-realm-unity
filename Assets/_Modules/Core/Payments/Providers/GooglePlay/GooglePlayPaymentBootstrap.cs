using UnityEngine;

namespace DeNelle.Core.Payments.Providers
{
    internal static class GooglePlayPaymentBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterForStampedArtifact()
        {
            if (PaymentChannelResolver.Current != PaymentChannel.GooglePlay) return;
            var provider = new GooglePlayBillingProvider();
            PaymentProviders.Register(provider, PaymentChannel.GooglePlay);
            provider.Initialize();
        }
    }
}
