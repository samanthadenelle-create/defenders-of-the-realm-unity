using System;
using System.Collections.Generic;
using UnityEngine.Purchasing;

namespace DeNelle.Core.Payments.Providers
{
    /// <summary>
    /// Stable save SKU to Play Console product-id mapping. All current packs are consumables:
    /// the server ledger owns restoration after the Play purchase is consumed.
    /// </summary>
    public static class GooglePlayProductCatalog
    {
        public const string ProductPrefix = "com.denellestudios.echoesofelarion.";

        private static readonly string[] s_skus =
        {
            "hearth-spark", "keepers-satchel", "folks-thanks", "patron-of-elarion",
            "founders-vow", "frostfall-bundle", "embergrove-bundle", "bloomtide-bundle",
            "starters-hand", "echo-patron-pack", "hero-wardrobe-pack", "realm-defender-bundle",
            "builders-cache", "impulse-wood-small", "impulse-wood-medium", "impulse-wood-large",
            "impulse-iron-small", "impulse-iron-medium", "impulse-iron-large",
            "impulse-stone-small", "impulse-stone-medium", "impulse-stone-large",
            "impulse-crystals-small", "impulse-crystals-medium", "impulse-crystals-large",
            "permanent-builder"
        };

        private static readonly Dictionary<string, string> s_productBySku = BuildProductMap();
        private static readonly Dictionary<string, string> s_skuByProduct = BuildSkuMap();

        public static IReadOnlyList<string> Skus => s_skus;

        public static bool TryGetProductId(string sku, out string productId) =>
            s_productBySku.TryGetValue(sku ?? string.Empty, out productId);

        public static bool TryGetSku(string productId, out string sku) =>
            s_skuByProduct.TryGetValue(productId ?? string.Empty, out sku);

        public static List<ProductDefinition> ProductDefinitions()
        {
            var definitions = new List<ProductDefinition>(s_skus.Length);
            foreach (var sku in s_skus)
                definitions.Add(new ProductDefinition(s_productBySku[sku], ProductType.Consumable));
            return definitions;
        }

        private static Dictionary<string, string> BuildProductMap()
        {
            var map = new Dictionary<string, string>(s_skus.Length, StringComparer.Ordinal);
            foreach (var sku in s_skus)
                map.Add(sku, ProductPrefix + sku.Replace('-', '_'));
            return map;
        }

        private static Dictionary<string, string> BuildSkuMap()
        {
            var map = new Dictionary<string, string>(s_skus.Length, StringComparer.Ordinal);
            foreach (var pair in s_productBySku) map.Add(pair.Value, pair.Key);
            return map;
        }
    }
}
