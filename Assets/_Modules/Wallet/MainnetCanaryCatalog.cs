using System.Collections.Generic;

namespace DeNelle.Wallet
{
    /// <summary>
    /// MON002's isolated owner-only product. It is compiled out of every ordinary build, so the
    /// production shelf and canonical pack count cannot accidentally expose a real-money test.
    /// </summary>
    internal static class MainnetCanaryCatalog
    {
        internal const string Sku = "mainnet-wood-canary";
        internal const string OwnerWallet = "CHKKFkPGz8VZfjpsZjJTqfAUW7vMpdNkkqCVuCcZsfkC";
        internal const double SkrPrice = 1d;
        internal const int WoodReward = 1;

#if MAINNET_CANARY_TEST
        internal static PackDef Create() => new PackDef
        {
            Sku = Sku,
            Tier = 1002,
            Name = "Mainnet Verification",
            Tagline = "One live-rail proof. Nothing more.",
            Theme = "Owner-only Mainnet canary",
            StoreVisible = true,
            ShelfCurated = true,
            StoreSection = "essentials",
            Band = "gap",
            Pricing = new PackPricing { Usd = 0.006d, Skr = SkrPrice },
            Contents = new PackContents
            {
                Economy = new PackEconomy { Wood = WoodReward },
                Cosmetics = new List<string>(),
                Convenience = new List<ConvenienceItemDef>(),
            },
        };
#endif
    }
}
