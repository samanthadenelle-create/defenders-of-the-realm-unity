# WORK ORDER 1190 - browsing the store must not ask for an authorization

**Status:** CLOSED 2026-08-27 — owner felt-tested PASS on APK 2026.08.27.343739.
**Minted:** 2026-08-25 (CLI lead, main line; banner bumped 1190 -> 1192 with WO-1191 in the same edit)
**Silo:** Monetization / wallet
**Origin:** owner, 2026-08-25: *"I don't think that as soon as you click the store button that it
should go to an authorization field. It just feels wrong. Why do I need to authorize if I'm just
looking."*

---

## She is describing a real defect, not a preference

`PurchaseQuoteService.RefreshPricesAsync` is the SHELF DISPLAY path. Its own doc comment says it
**"Binds nothing and charges nothing."** And then it does this:

    if (wallet == null || !wallet.IsRealSigningWallet)
    {
        FlowTrace.Step("Store", "quote list skipped: no signing wallet ...");
        return false;
    }
    ...
    var text = await PostAsync(body, playerId, "price list");

`PostAsync` goes through `BackendRequestSigner`, which mints a backend session from a **wallet
signature** when it does not already hold one. So **opening the store authenticates, for a read that
binds nothing and charges nothing.**

**The principle: a shelf shows prices. Eligibility is checked at the till.** Every store the player
has ever used works that way, which is exactly why this one feels wrong.

## It is entangled with two findings already on the record

1. **Guests cannot get a quote at all** (`PurchaseQuoteService.cs:349-353`, `:377-381`), which
   contradicts `PurchaseGate.WalletRequiredAboveUsd = 4.99` (`PurchaseGate.cs:106`) ruling
   <= $4.99 guest-buyable. No wallet -> no quote -> no price -> no sale.
2. **`MAINNET_SALES_ENABLED` filters the LIST.** `quote.js:171` runs every candidate through
   `walletAllowed`, so when the switch is off a non-owner wallet gets an empty price array,
   `HasDisplayPrices` goes false, and every card reads **"Price unavailable"** - a shop with no
   prices, no badge and no message.

So the same coupling produces three symptoms: an authorization prompt for browsing, a store guests
cannot price, and a silent blank shelf when the sales switch is off.

## What to build

1. **Decouple BROWSE from AUTH.** The display-price path must work with **no wallet and no session**.
   It is a public price list.
2. **Server: LIST mode must serve unauthenticated.** Decide and state what an unauthenticated list
   returns: the **public ladder** - what anyone could buy - rather than a per-wallet filtered set.
   Per-wallet eligibility (`walletAllowed`, `MAINNET_SALES_ENABLED`, the canary's stricter gate)
   stays exactly where it is and is enforced at the BINDING quote and at `/verify`. Loosening the
   list must not loosen what can actually be sold. The mirror law still binds: `USD_ANCHORS`, both
   canonical `packs.json` copies and the quote test's key list move together or the build is red.
3. **Authorize at purchase INTENT, not at store open.** The first wallet interaction happens when the
   player commits to buying - which is already where `RequestQuoteAsync` lives, and its comment
   already says *"CALL THIS AS LATE AS POSSIBLE... a quote fetched when the store opened and paid
   against ten minutes later is the expired-after-payment case."* The binding quote is already late;
   only the DISPLAY path is early.
4. **When sales are off or a SKU is not sellable to this player, show the price and disable the buy
   control with a WORDED reason.** Never a blank shelf, never a bare "Price unavailable" with no
   explanation, never meaning by colour alone.

## What NOT to do

- Do **not** move the backend session mint to the purchase moment as a fix for this. It is not
  purchase-only: `save.js`, `promo/redeem`, `referral/claim` and `bug-report` all authenticate with
  it, and without a session `BackendRequestSigner.TryAttachSession` falls back to signing EVERY
  request. Deferring it multiplies prompts and lands them mid-play on a cloud save.
- Do not add client price arithmetic or a client sellable-SKU allowlist. The server quote remains the
  sole authority on what is sellable and at what amount; the client fails CLOSED.
- Do not weaken `RequestQuoteAsync`. The BINDING quote must stay wallet-authenticated and late.

## The separate question this does NOT answer

Once browsing is free, the remaining handshake happens when the player CONNECTS. It is currently a
second, separate prompt after a silent reconnect, which is why it reads as foreign - the industry
norm (Sign In With Solana) makes connect and authenticate ONE action. Folding them together is its
own ticket and its own owner decision. Note also that the session is deliberately IN MEMORY ONLY
(`BackendRequestSigner.cs:61`) and lives 15 minutes, so it cannot survive an app restart by design -
no caching strategy removes that handshake, only combining it with connect does.

---

## OWNER RULING 2026-08-25 - the open question in section 2 is SETTLED

**Owner, 2026-08-25**, answering a direct question from the CLI lead. Elevated to
`FOUNDATIONAL_RULINGS.md` **section 12** ("a shelf shows prices; eligibility is checked at the
till"), which is the binding text - ⛔ cite that section, do not paraphrase it here.

- **Section 2's "decide and state what an unauthenticated list returns" is answered: the PUBLIC
  LADDER.** The body already proposed exactly that; the ruling confirms it, so the item is no longer
  a decision this ticket carries.
- ⚠ **This ticket does NOT get to loosen sellability.** `walletAllowed`, `MAINNET_SALES_ENABLED` and
  the canary's stricter gate stay exactly where they are, enforced at the BINDING quote and at
  `/verify`. Loosening the LIST must not loosen what can be SOLD.
- ⛔ **Guest checkout is NOT authorised.** The owner chose **browse-only**:
  `PurchaseGate.WalletRequiredAboveUsd = 4.99` is **not** to be implemented as a guest purchase path
  by this ticket. **A guest who taps buy is asked to connect.** Section 2's finding 1 stands as a
  recorded contradiction, not as a licence to resolve it in the guest's favour here.
