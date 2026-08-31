using System;
using System.Collections.Generic;
using DeNelle.Commerce;
using DeNelle.Core.Payments;
using DeNelle.Wallet; // PackCatalog's namespace is a preserved runtime contract; its assembly is Commerce.
using UnityEngine;

namespace DeNelle.GooglePlay
{
    internal sealed class GooglePlayStorefrontVM
    {
        internal readonly struct Row
        {
            internal readonly string Sku, Label;
            internal readonly bool Available;
            internal Row(string sku, string label, bool available)
            { Sku = sku; Label = label; Available = available; }
        }

        private readonly Action<string> _status;
        private readonly List<Row> _rows = new List<Row>();
        internal IReadOnlyList<Row> Rows => _rows;
        internal static GooglePlayStorefrontVM CreateDefault(Action<string> status)
            => new GooglePlayStorefrontVM(PaymentProviders.Current, status);

        private GooglePlayStorefrontVM(IPaymentProvider provider, Action<string> status)
        {
            _status = status;
            foreach (var pack in PackCatalog.Packs)
            {
                if (pack == null || !pack.StoreVisible) continue;
                DisplayPrice price = provider != null ? provider.GetDisplayPrice(pack.Sku)
                    : DisplayPrice.Unavailable("Google Play Billing is unavailable.");
                _rows.Add(new Row(pack.Sku,
                    pack.Name + "  " + (price.Available ? price.LocalizedText : "Unavailable"), price.Available));
            }
        }

        internal void Purchase(string sku)
        {
            var provider = PaymentProviders.Current;
            if (provider == null) { _status("Google Play Billing is unavailable."); return; }
            _status("Preparing secure purchase...");
            provider.Purchase(sku, result => _status(result.Succeeded ? "Purchase restored to your realm." :
                result.Pending ? "Purchase pending verification. It will retry safely." : result.Error));
        }

        internal void Restore()
        {
            var provider = PaymentProviders.Current;
            if (provider == null) { _status("Google Play Billing is unavailable."); return; }
            _status("Checking purchases...");
            provider.RestorePurchases((ok, message) =>
                _status(ok ? "Purchases checked and restored." : (message ?? "Restore failed.")));
        }

        internal void OpenDeletionPage() =>
            Application.OpenURL("https://echoes-of-elarion.vercel.app/delete-account");
    }
}
