// =============================================================================
// SkrValuationOracle — RETIRED AS A PRICE AUTHORITY (WO-1158, 2026-08-23)
// -----------------------------------------------------------------------------
// ⛔ THIS CLASS USED TO PRICE REAL SALES FROM THE CLIENT, AND THAT IS THE BUG.
//
// It fetched CoinGecko's 24h low for SKR and exposed
//
//     SkrForUsd(usd) => Math.Ceiling(usd / _usdLow24h)
//
// which PackCatalog.AmountFor(CurrencyKind.Skr) returned as the price of a pack.
// Meanwhile api/_lib/purchase-catalog.js verified the settled transfer against a
// number of its own.
//
//   A CLIENT-RESOLVED PRICE AND A SERVER-CHECKED ONE CANNOT BOTH BE RIGHT.
//
// The moment the market moved, the client sent N and the server expected M. And
// /verify runs AFTER the transfer settles, so the purchase failed with the money
// ALREADY GONE and nothing granted. The trigger was a MARKET MOVE, which is not
// a deploy — nobody would have been watching when it fired.
//
// ⛔ THE BODY IS DELETED ON PURPOSE, AND THE FILE IS NOT. Two reasons, and the
// second is the load-bearing one:
//   1. Nothing may call a client-side pricer again by reaching for a name that is
//      still in the assembly. There is no SkrForUsd to find.
//   2. §12 forbids discarding the record of a failure. A deleted file takes the
//      lesson with it, and the next seat that needs a "quick" rate on the client
//      re-derives this exact class from first principles in an afternoon.
//
// WHERE THE RATE LIVES NOW: the SERVER reads it, caches it, and hands the client
// a short-lived, single-use QUOTE — see api/_lib/purchase-catalog.fetchSkrUsdRate
// and api/purchases/quote.js. The client transports it (PurchaseQuoteService) and
// pays exactly what it was told. The oracle FAILS CLOSED there: no rate means no
// quote and no sale, never a stale or invented price.
//
// Do not restore a client-side rate fetch here for "display only" either. A price
// shown to a player is a price they act on, and the display path is how the pay
// path gets its number back (PackCatalog.AmountFor is read by WalletService.Pay).
// =============================================================================

namespace DeNelle.Wallet
{
    /// <summary>
    /// Tombstone. The client no longer resolves SKR prices — see the file header and
    /// <see cref="PurchaseQuoteService"/>. Intentionally empty; intentionally still here.
    /// </summary>
    internal static class SkrValuationOracle
    {
        /// <summary>
        /// Where the price actually comes from now. Referenced from diagnostics and from this
        /// file's own header so the trail from the retired name to the live one is one hop.
        /// </summary>
        internal const string ReplacedBy =
            "PurchaseQuoteService (client transport) over api/purchases/quote.js (server authority)";
    }
}
