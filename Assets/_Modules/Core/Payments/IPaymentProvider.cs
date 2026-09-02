using System;
using System.Collections.Generic;

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

    /// <summary>
    /// WO-1323 - OPTIONAL companion to <see cref="IPaymentProvider"/>: a rail whose shelf price is
    /// only knowable by ASKING THE SERVER can pre-fetch those prices so the storefront has something
    /// honest to draw BEFORE the player taps Buy.
    ///
    /// <para>⛔ IT IS A REFRESH, NEVER A CALCULATOR. The implementation's only sanctioned move is to
    /// ask its own backend and cache what comes back; a provider that DERIVES a price here (from a
    /// USD anchor, a cached rate, anything local) has become a second pricing authority, which is
    /// precisely the failure WO-1318's server-side quote exists to prevent. A refusal must CLEAR the
    /// cached figure, never leave the last one standing.</para>
    ///
    /// <para>⛔ AND IT MAY NEVER PROMPT. This runs on store OPEN, unattended: anything that raises a
    /// consent sheet, a wallet dialog or a payment sheet from here would put a modal in front of a
    /// player who only browsed. Read whatever identity is already established, or send none.</para>
    ///
    /// <para>Separate from <see cref="IPaymentProvider"/> on purpose: it is additive, and the two
    /// rails that do not need it (Google Play prices locally through the billing client; the
    /// Solana rail quotes through PurchaseQuoteService) are untouched by its existence.</para>
    /// </summary>
    public interface IDisplayPriceRefresher
    {
        /// <summary>
        /// Refreshes the cached <see cref="IPaymentProvider.GetDisplayPrice"/> answer for each sku it
        /// can price, then reports whether ANY answer changed (so the caller can repaint once rather
        /// than per sku). Never throws; a failure reports false.
        /// </summary>
        void RefreshDisplayPrices(IReadOnlyList<string> skus, Action<bool> onComplete);
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
