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
        /// <summary>
        /// WO-1282 - installs <see cref="Create"/> as PackCatalog's build-gated pack provider.
        /// <para>PackCatalog moved to the rail-neutral DeNelle.Commerce assembly so a Google Play
        /// artifact can exclude DeNelle.Wallet whole. Commerce may never name this type (that would
        /// be Commerce -&gt; Wallet, the forbidden direction, and it would put the owner-only
        /// real-money SKU's name in the Play artifact), so the <c>#if</c> lives HERE now instead of
        /// inside PackCatalog.EnsureLoaded. The compile-time isolation is identical: this method
        /// does not exist in a build without the symbol, so nothing can register the canary.</para>
        /// <para><see cref="PackCatalog.Reload"/> follows the registration so a catalogue that was
        /// already read (an editor domain reload, a test) picks the canary up. Ordering therefore
        /// cannot silently drop it.</para>
        /// </summary>
        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterGatedPack()
        {
            PackCatalog.BuildGatedPackProvider = Create;
            PackCatalog.Reload();
            DeNelle.Core.Diagnostics.FlowTrace.Warn("Covenant",
                "MainnetCanaryCatalog: MAINNET_CANARY_TEST build - the owner-only real-money canary SKU '" +
                Sku + "' is registered with PackCatalog. This must NEVER be a shipped build.");
        }

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
