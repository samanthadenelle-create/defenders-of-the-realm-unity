# WO-1177 - The shortfall discount, issued by the server or not at all

**Status:** READY TO IMPLEMENT. **Silo:** Backend/monetization.
**Origin:** owner, 2026-08-24 - *"offer a 20% discount? buy it now?"*. Split out of **PROD-014**,
where it was the largest item and would otherwise have held up the reported defect.
**Ruling:** the owner ruled the discount **fires at the shortfall**. I argued for a one-time
first-purchase discount instead; she ruled otherwise and that is **settled** - do not re-open it.

## What must be true

⛔ **The discount is SERVER-ISSUED, inside the quote.** A client-computed percentage is trivially
edited from 20 to 100, and this is **real money on a live storefront**. Everything below exists to
keep the client from ever seeing a pre-discount number it could modify.

⭐ **There is exactly ONE price authority, verified this session end to end** - so there is exactly
one place this goes:
- `api/_lib/purchase-catalog.js` - `USD_ANCHORS` (`:83-114`) authors every price;
  **`buildQuoteBody` (`:338-357`) is the ONLY caller of `quoteAmount(usd, rate, decimals)`**.
- `api/purchases/quote.js` persists what that returns; `api/purchases/verify.js` checks the chain
  against the persisted row.
- `Assets/_Modules/Wallet/PurchaseQuoteService.cs` **computes nothing** - its only arithmetic is a
  guard proving the paid amount matches the quote.

⚠ **`grep -rni "discount" api/` returns ZERO.** This is greenfield; there is no existing concept to
extend and no second implementation to reconcile.

## Implementation

1. **`quote.js` accepts a `reason` on the body** (e.g. `"repair_shortfall"`). ⛔ **A hint, never an
   authorization** - logged, not trusted. Eligibility is decided server-side between authentication
   (`:120-129`) and the rate fetch (`:139`).
2. **Eligibility + rate limit** (Ruling 2 constraint 2) - a helper beside `walletAllowed`
   (`purchase-catalog.js:414`): has this wallet been issued a discounted quote inside the window?
   ⚠ **One per player per window, recorded server-side.** A discount the player can summon by
   re-triggering a refusal is a permanent 20% off with extra taps.
3. **`buildQuoteBody(network, sku, rate, discountBps)`** applies the reduction to `usd` **before**
   `quoteAmount`, and returns the discount on the wire body so the card can *display* it.
4. **Schema:** `purchase_quotes` gains nullable `discount_bps INT` + `discount_reason TEXT`, added to
   the INSERT at `quote.js:184-190`, with an idempotent `ADD COLUMN IF NOT EXISTS` block in the style
   already at `api/schema.sql:956-960`.
   ⛔ **And it goes in the SAME cut as any other pending migration.** This repo has no migration
   runner - a migration is a human running a file - and PROD-017 exists because the 2026-08-02
   reconcile was authored, committed, and **never run for 22 days**. A second forgettable file is the
   failure mode, not the fix.
5. **Log every issuance** (Ruling 2 constraint 3) via the existing
   `logApiEvent(... 'purchase_quote_issued', ...)` at `quote.js:196-198`, with the discount fields -
   so the real discount rate is a number we can **read**, not one we assume.
6. **Client, display only:** `PurchaseQuote` gains
   `[JsonProperty("discountBps")] public int? DiscountBps`.
   ⚠ **Nullable, following the `UsdAnchor` precedent** (`PurchaseQuoteService.cs:76`) - that field's
   history is a live warning: a non-nullable type on a legitimately-null server field **blanked the
   entire store shelf** and took two wrong diagnoses to find.
7. ⛔ **No second purchase path.** Buy stays `PackStore` -> `RequestQuoteAsync` -> **one** wallet
   prompt (WO-1157). The money path was made singular this week; this must not be its eighth caller.

## Acceptance

- [ ] No percentage, multiplier or pre-discount price exists anywhere in client code - asserted by a
      source lint, not by inspection
- [ ] A replayed/forged `reason` cannot obtain a second discount inside the window - proven by a test
      that **fails first**
- [ ] `discount_bps` is persisted on the quote row and checked by `verify.js` against the chain
      amount, so a discounted quote cannot be settled at the undiscounted price or vice versa
- [ ] `SCHEMA_PARITY_OK` covers the two new columns
- [ ] One wallet prompt, end to end
