using System;

namespace DeNelle.Core.Payments
{
    /// <summary>The immutable distribution/payment channel stamped or detected at boot.</summary>
    public enum PaymentChannel
    {
        Unknown = 0,
        SolanaDappStore = 1,
        GooglePlay = 2,
        PiBrowser = 3,
    }

    public readonly struct DisplayPrice
    {
        public readonly bool Available;
        public readonly string LocalizedText;
        public readonly string CurrencyCode;
        public readonly string UnavailableReason;

        private DisplayPrice(bool available, string localizedText, string currencyCode, string reason)
        {
            Available = available;
            LocalizedText = localizedText ?? string.Empty;
            CurrencyCode = currencyCode ?? string.Empty;
            UnavailableReason = reason ?? string.Empty;
        }

        public static DisplayPrice Ready(string localizedText, string currencyCode) =>
            new DisplayPrice(true, localizedText, currencyCode, string.Empty);

        public static DisplayPrice Unavailable(string reason) =>
            new DisplayPrice(false, string.Empty, string.Empty, reason);
    }

    public readonly struct ProviderPurchaseResult
    {
        public readonly bool Succeeded;
        public readonly bool Pending;
        public readonly string Sku;
        public readonly string ProviderTransactionId;
        public readonly string Error;

        private ProviderPurchaseResult(bool succeeded, bool pending, string sku,
            string providerTransactionId, string error)
        {
            Succeeded = succeeded;
            Pending = pending;
            Sku = sku ?? string.Empty;
            ProviderTransactionId = providerTransactionId ?? string.Empty;
            Error = error ?? string.Empty;
        }

        public static ProviderPurchaseResult Success(string sku, string transactionId) =>
            new ProviderPurchaseResult(true, false, sku, transactionId, string.Empty);

        public static ProviderPurchaseResult AwaitingSettlement(string sku, string transactionId) =>
            new ProviderPurchaseResult(false, true, sku, transactionId, string.Empty);

        public static ProviderPurchaseResult Failure(string sku, string error) =>
            new ProviderPurchaseResult(false, false, sku, string.Empty, error);
    }

    /// <summary>
    /// Provider-neutral payment boundary. Implementations live in provider assemblies; gameplay
    /// and storefront code may depend only on this contract.
    /// </summary>
    public interface IPaymentProvider
    {
        PaymentChannel Channel { get; }
        DisplayPrice GetDisplayPrice(string sku);
        bool CanBuy(string sku, out string reason);
        void Purchase(string sku, Action<ProviderPurchaseResult> onComplete);
        void RestorePurchases(Action<bool, string> onComplete);
    }

    /// <summary>Exactly one provider may own the active channel. Missing/mismatched rails fail closed.</summary>
    public static class PaymentProviders
    {
        private static IPaymentProvider s_current;

        public static IPaymentProvider Current => s_current;
        public static bool HasProvider => s_current != null;

        public static void Register(IPaymentProvider provider, PaymentChannel resolvedChannel)
        {
            if (provider == null) throw new ArgumentNullException(nameof(provider));
            if (resolvedChannel == PaymentChannel.Unknown)
                throw new InvalidOperationException("Cannot register a payment provider before channel resolution.");
            if (provider.Channel != resolvedChannel)
                throw new InvalidOperationException(
                    $"Payment provider channel mismatch: resolved={resolvedChannel}, provider={provider.Channel}.");
            if (s_current != null && !ReferenceEquals(s_current, provider))
                throw new InvalidOperationException(
                    $"Payment provider already registered for {s_current.Channel}; refusing {provider.Channel}.");

            s_current = provider;
        }

        public static void Unregister(IPaymentProvider provider)
        {
            if (ReferenceEquals(s_current, provider)) s_current = null;
        }

        public static void ResetForTests() => s_current = null;
    }
}
