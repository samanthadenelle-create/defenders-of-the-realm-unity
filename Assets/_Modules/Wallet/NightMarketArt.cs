using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeNelle.Wallet
{
    /// <summary>One runtime resolver for the owner's Night Market sprite set.</summary>
    internal static class NightMarketArt
    {
        private const string Root = "UI/NightMarket/";
        private static readonly Dictionary<string, Sprite> Cache =
            new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);

        internal static string ForSku(string sku)
        {
            switch ((sku ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "folks-thanks": return "folks-thanks";
                case "starters-hand": return "starters-hand";
                case "permanent-builder": return "permanent-builder";
                case "impulse-wood-medium": return "timber-wagon";
                case "impulse-iron-medium": return "ingot-crate";
                case "impulse-stone-medium": return "quarry-cart";
                case "patron-of-elarion": return "resource-pack-1";
                case "founders-vow": return "resource-pack-2";
                default: return string.Empty;
            }
        }

        internal static Sprite Load(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            if (Cache.TryGetValue(name, out var cached)) return cached;

            var sprite = Resources.Load<Sprite>(Root + name);
            if (sprite == null)
            {
                var texture = Resources.Load<Texture2D>(Root + name);
                if (texture != null)
                    sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height),
                        new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            }
            Cache[name] = sprite;
            return sprite;
        }
    }
}
