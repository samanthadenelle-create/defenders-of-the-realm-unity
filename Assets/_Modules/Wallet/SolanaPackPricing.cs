// =============================================================================
// SolanaPackPricing - the RAIL half of PackDef, split out by WO-1282.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Wallet   Namespace: DeNelle.Wallet   (STATIC, extension methods)
//
// WHY THIS FILE EXISTS. PackDef and the rest of PackCatalog.cs moved to the
// rail-neutral DeNelle.Commerce assembly (Assets/_Modules/Commerce/PackCatalog.cs)
// so DeNelle.Village could stop referencing DeNelle.Wallet and a Google Play
// artifact could exclude the Solana rail whole. Three members could NOT go with it,
// because each one names a rail-bound type that must stay in Wallet:
//
//   * AmountFor(CurrencyKind)   - CurrencyKind IS the rail (Sol/Usdc/Skr, WalletService.cs:45-53)
//   * AmountLabel(CurrencyKind) - same
//   * UsdApprox()               - reads PurchaseQuoteService, which takes a WalletService
//   * IsServerPinnedSku(sku)    - names PurchaseGate + MainnetCanaryCatalog
//
// They are EXTENSION METHODS so every existing call site is unchanged: any file in
// `namespace DeNelle.Wallet` still writes `pack.AmountFor(CurrencyKind.Skr)` and
// `pack.AmountLabel(...)` exactly as before. The ONE call-shape change is that
// `pack.UsdApprox` became `pack.UsdApprox()` - C# has no extension properties.
//
// ⛔ THE BODIES ARE VERBATIM. Every ruling recorded in the comments below (WO-1158's
//    "the client does no price arithmetic", the ZERO-is-honest branch, the
//    server-pinned canary exception, the colourblind "Price unavailable" wording) is
//    unchanged by the move and none of it was re-decided. Do not "tidy" them.
//
// ⛔ NEVER MOVE THESE BACK INTO PackCatalog.cs. That file is compiled into the Play
//    artifact; CurrencyKind is not. See the pointer block in PackDef.
// =============================================================================

namespace DeNelle.Wallet
{
    /// <summary>
    /// The Solana-rail pricing surface of <see cref="PackDef"/>. Lives with the rail, not with the
    /// data, so the data can ship in a build that has no wallet in it.
    /// </summary>
    public static class SolanaPackPricing
    {
        /// <summary>The native amount payable in the given currency rail.</summary>
        public static double AmountFor(this PackDef pack, CurrencyKind currency)
        {
            if (pack == null || pack.Pricing == null) return 0d;
            switch (currency)
            {
                case CurrencyKind.Sol: return pack.Pricing.Sol;
                case CurrencyKind.Usdc: return pack.Pricing.Usdc;
                case CurrencyKind.Skr:
                    // ⛔ THE CLIENT DOES NO PRICE ARITHMETIC. WO-1158.
                    //
                    // This branch used to read `SkrValuationOracle.SkrForUsd(Pricing.Usd)` - the
                    // CLIENT resolving a market rate and rounding it into an SKR amount - while the
                    // BACKEND checked the settled transfer against its own figure. Those two can
                    // never be made to agree, because they are two different opinions about a
                    // moving number, and /verify runs AFTER the transfer settles: the moment they
                    // diverge the player has paid and been granted nothing. The trigger is a MARKET
                    // MOVE, not a deploy, so nobody would be watching when it fired.
                    //
                    // The SERVER now issues the price (api/purchases/quote.js) and this returns
                    // whatever it quoted, or ZERO.
                    //
                    // ⛔ ZERO IS DELIBERATE AND IT IS THE HONEST ANSWER. It is not "free" and it is
                    // not "fall back to Pricing.Skr" - the authored `pricing.skr` in packs.json is a
                    // stale hand-typed figure from before the SKR rail existed at a real rate, and
                    // rendering it would put a number on screen that nobody will honour. Callers
                    // turn 0 into the WORDS "Price unavailable" (see AmountLabel) and
                    // WalletService.Pay refuses an amount <= 0 outright, so the fail is closed on
                    // both the display and the charge path.
                    //
                    // The two CANARIES are the one exception and they keep their authored number:
                    // their amount IS a protocol constant that the backend pins by exact equality
                    // (a proof-of-rail, not a sale), so a market rate must never touch it.
                    if (IsServerPinnedSku(pack.Sku)) return pack.Pricing.Skr;
                    return PurchaseQuoteService.SkrAmountFor(pack.Sku);
                default: return 0d;
            }
        }

        /// <summary>
        /// True when the BACKEND pins this SKU's on-chain amount and verifies it by exact equality
        /// (<c>api/_lib/purchase-catalog.js</c>). Such a price may never be resolved from a market
        /// oracle: client and server must agree to the base unit or the purchase is refused after
        /// the funds have already moved.
        /// <para>⚠ Keep this in step with the server catalog. If a SKU is added there, add it here
        /// in the SAME change - a server-pinned SKU that is missing from this list is a silent
        /// paid-but-not-granted bug that only fires when the market crosses the price.</para>
        /// </summary>
        private static bool IsServerPinnedSku(string sku)
            => string.Equals(sku, MainnetCanaryCatalog.Sku, System.StringComparison.Ordinal)
            || string.Equals(sku, PurchaseGate.DevnetCanarySku, System.StringComparison.Ordinal);

        /// <summary>
        /// The USD anchor, MARKED APPROXIMATE - <c>"~ $2.99"</c> (WO-1158 §5, owner ruling
        /// 2026-08-23: "we should be transparent that price is approx 2.99 or 5.99 or 9.99").
        ///
        /// <para>⚠ WHICH NUMBER CARRIES THE "APPROX" IS NOT A WORDING PREFERENCE AND IT IS EASY TO
        /// GET BACKWARDS. The player pays SKR and the amount charged is EXACT - the server's quote
        /// pins it to the base unit. What FLOATS is the dollar value, because the rate moves. So the
        /// SKR is stated precisely (<see cref="AmountLabel"/>) and it is the DOLLARS that get the
        /// tilde. Printing a flat "$2.99" while charging a rate-derived amount is the misleading
        /// version, and it is the one a reader assumes.</para>
        ///
        /// <para>The SERVER's anchor wins when it differs from the authored one: two prices on one
        /// screen is worse than a stale one, and the server's is the one the charge is derived from.
        /// "~" not "≈" on purpose - TMP is ASCII-only here and U+2248 renders as a tofu box.</para>
        /// </summary>
        public static string UsdApprox(this PackDef pack)
        {
            if (pack == null) return string.Empty;
            double served = PurchaseQuoteService.UsdAnchorFor(pack.Sku);
            double usd = served > 0d ? served : (pack.Pricing != null ? pack.Pricing.Usd : 0d);
            return usd > 0d ? $"~ ${usd:0.00}" : string.Empty;
        }

        /// <summary>Formats one currency rail's amount + symbol, e.g. <c>"60 SKR"</c>.</summary>
        public static string AmountLabel(this PackDef pack, CurrencyKind currency)
        {
            var amount = pack.AmountFor(currency);
            switch (currency)
            {
                case CurrencyKind.Sol: return $"{amount:0.###} SOL";
                case CurrencyKind.Usdc: return $"{amount:0.00} USDC";
                // ⛔ NO SERVER QUOTE = NO NUMBER. WORDS, not a zero and not a stale authored
                // figure: "0 SKR" reads as free and a stale figure reads as a promise. The owner is
                // RED/GREEN COLOURBLIND, so an unavailable price can never be signalled by a tint -
                // it says so in text, and the greyscale capture is the acceptance test.
                case CurrencyKind.Skr: return amount > 0d ? $"{amount:0.######} SKR"
                                                          : "Price unavailable";
                default: return amount.ToString("0.##");
            }
        }
    }
}
