using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Purchasing;

namespace DeNelle.Core.Payments.Providers
{
    /// <summary>
    /// Google Play Billing adapter. Orders remain pending until the authenticated game backend
    /// verifies and grants the receipt; no client-only callback can grant paid content.
    /// </summary>
    public sealed class GooglePlayBillingProvider : IPaymentProvider
    {
        public delegate Task<bool> ReceiptVerifier(string sku, string productId, string receipt,
            string transactionId);

        private readonly Dictionary<string, Action<ProviderPurchaseResult>> _callbacks =
            new Dictionary<string, Action<ProviderPurchaseResult>>(StringComparer.Ordinal);
        private StoreController _store;
        private bool _connected;
        private string _failure = "Google Play Billing is still initializing.";

        /// <summary>Set by the authenticated backend composition root. Null deliberately fails closed.</summary>
        public ReceiptVerifier VerifyAndGrantAsync { get; set; }

        public PaymentChannel Channel => PaymentChannel.GooglePlay;

        public async void Initialize()
        {
            if (_store != null) return;
            try
            {
                _store = UnityIAPServices.StoreController();
                _store.OnStoreDisconnected += failure =>
                {
                    _connected = false;
                    _failure = "Google Play Billing disconnected: " + failure.message;
                };
                _store.OnProductsFetched += products =>
                {
                    _connected = true;
                    _failure = string.Empty;
                };
                _store.OnProductsFetchFailed += failure =>
                {
                    _connected = false;
                    _failure = "Google Play products unavailable: " + failure.FailureReason;
                };
                _store.OnPurchasePending += HandlePendingOrder;
                _store.OnPurchaseFailed += HandleFailedOrder;
                _store.OnPurchaseDeferred += HandleDeferredOrder;

                await _store.Connect();
                _store.FetchProducts(GooglePlayProductCatalog.ProductDefinitions());
            }
            catch (Exception ex)
            {
                _connected = false;
                _failure = "Google Play Billing initialization failed: " + ex.Message;
                Debug.LogError(_failure);
            }
        }

        public DisplayPrice GetDisplayPrice(string sku)
        {
            if (!TryGetProduct(sku, out var product, out var reason))
                return DisplayPrice.Unavailable(reason);
            var metadata = product.metadata;
            if (metadata == null || string.IsNullOrWhiteSpace(metadata.localizedPriceString))
                return DisplayPrice.Unavailable("Google Play did not return a localized price.");
            return DisplayPrice.Ready(metadata.localizedPriceString, metadata.isoCurrencyCode);
        }

        public bool CanBuy(string sku, out string reason)
        {
            if (VerifyAndGrantAsync == null)
            {
                reason = "Secure Google Play receipt verification is unavailable.";
                return false;
            }
            return TryGetProduct(sku, out _, out reason);
        }

        public void Purchase(string sku, Action<ProviderPurchaseResult> onComplete)
        {
            if (onComplete == null) throw new ArgumentNullException(nameof(onComplete));
            if (!CanBuy(sku, out var reason) || !GooglePlayProductCatalog.TryGetProductId(sku, out var productId))
            {
                onComplete(ProviderPurchaseResult.Failure(sku, reason));
                return;
            }
            if (_callbacks.ContainsKey(productId))
            {
                onComplete(ProviderPurchaseResult.Failure(sku, "A purchase for this item is already pending."));
                return;
            }
            _callbacks.Add(productId, onComplete);
            _store.PurchaseProduct(productId);
        }

        public void RestorePurchases(Action<bool, string> onComplete)
        {
            if (onComplete == null) throw new ArgumentNullException(nameof(onComplete));
            if (!_connected)
            {
                onComplete(false, _failure);
                return;
            }
            // Consumables restore from the game's verified entitlement ledger. This call only
            // replays unconfirmed/pending Play orders; it does not invent consumed entitlements.
            _store.RestoreTransactions(onComplete);
        }

        private bool TryGetProduct(string sku, out Product product, out string reason)
        {
            product = null;
            if (!_connected || _store == null)
            {
                reason = _failure;
                return false;
            }
            if (!GooglePlayProductCatalog.TryGetProductId(sku, out var productId))
            {
                reason = "This item is not mapped in the Google Play catalog.";
                return false;
            }
            product = _store.GetProducts().FirstOrDefault(p => p.definition.id == productId);
            if (product == null || !product.availableToPurchase)
            {
                reason = "This item is unavailable from Google Play in your region.";
                return false;
            }
            reason = string.Empty;
            return true;
        }

        private async void HandlePendingOrder(PendingOrder order)
        {
            var product = order.CartOrdered.Items().FirstOrDefault()?.Product;
            var productId = product?.definition.id ?? string.Empty;
            if (!GooglePlayProductCatalog.TryGetSku(productId, out var sku)) return;
            _callbacks.TryGetValue(productId, out var callback);

            if (VerifyAndGrantAsync == null)
            {
                callback?.Invoke(ProviderPurchaseResult.AwaitingSettlement(sku, order.Info.TransactionID));
                return;
            }

            bool granted;
            try
            {
                granted = await VerifyAndGrantAsync(sku, productId, order.Info.Receipt,
                    order.Info.TransactionID);
            }
            catch (Exception ex)
            {
                Debug.LogError("Google Play receipt verification failed: " + ex.Message);
                granted = false;
            }

            if (!granted)
            {
                callback?.Invoke(ProviderPurchaseResult.AwaitingSettlement(sku, order.Info.TransactionID));
                return;
            }

            // Confirmation consumes the catalog's consumable only after server grant succeeds.
            _store.ConfirmPurchase(order);
            _callbacks.Remove(productId);
            callback?.Invoke(ProviderPurchaseResult.Success(sku, order.Info.TransactionID));
        }

        private void HandleFailedOrder(FailedOrder order)
        {
            CompleteFailure(order, "Google Play purchase failed: " + order.FailureReason);
        }

        private void HandleDeferredOrder(DeferredOrder order)
        {
            var product = order.CartOrdered.Items().FirstOrDefault()?.Product;
            var productId = product?.definition.id ?? string.Empty;
            if (!GooglePlayProductCatalog.TryGetSku(productId, out var sku)) return;
            if (_callbacks.TryGetValue(productId, out var callback))
                callback(ProviderPurchaseResult.AwaitingSettlement(sku, order.Info.TransactionID));
        }

        private void CompleteFailure(Order order, string error)
        {
            var productId = order.CartOrdered.Items().FirstOrDefault()?.Product?.definition.id ?? string.Empty;
            if (!GooglePlayProductCatalog.TryGetSku(productId, out var sku)) return;
            if (_callbacks.TryGetValue(productId, out var callback)) callback(ProviderPurchaseResult.Failure(sku, error));
            _callbacks.Remove(productId);
        }
    }
}
