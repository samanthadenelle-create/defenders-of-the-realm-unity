using System;
using DeNelle.Core.Platform;

namespace DeNelle.Core.Payments
{
    /// <summary>
    /// Resolves the one payment channel for this artifact. Android channels are compile-stamped;
    /// Pi is detected only in the WebGL host. Ambiguous release artifacts fail closed.
    /// </summary>
    public static class PaymentChannelResolver
    {
        private static PaymentChannel? s_override;

        public static PaymentChannel Current => s_override ?? ResolveStampedChannel();

        public static PaymentChannel ResolveStampedChannel()
        {
#if GOOGLE_PLAY && DAPP_STORE
            throw new InvalidOperationException(
                "Invalid payment artifact: GOOGLE_PLAY and DAPP_STORE are both defined.");
#elif GOOGLE_PLAY
            return PaymentChannel.GooglePlay;
#elif UNITY_WEBGL && !UNITY_EDITOR
            return WebGLPiPlatform.IsPiBrowserEnvironment
                ? PaymentChannel.PiBrowser
                : PaymentChannel.Unknown;
#elif DAPP_STORE
            return PaymentChannel.SolanaDappStore;
#else
            return PaymentChannel.Unknown;
#endif
        }

        public static void OverrideForTests(PaymentChannel channel) => s_override = channel;
        public static void ClearTestOverride() => s_override = null;
    }
}
