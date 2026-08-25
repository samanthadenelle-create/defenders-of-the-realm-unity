# WORK ORDER 1198 - show the real price, and announce the saving

**Status:** READY TO IMPLEMENT
**Minted:** 2026-08-25 (CLI lead, main line; banner bumped 1198 -> 1199 in the same edit)
**Silo:** Monetization / store
**Ruling:** owner, 2026-08-25.

---

> *"Can't we update the USD price with the current price and announce savings?"*
> *"If it's a sale, that's the exact reason to offer more value."*

## The ruling

**Ship the effective (discounted) USD figure to the client, and PRESENT the saving.**

This settles the `usdEffective` question in the affirmative and goes further than the options put to
the owner: not merely "stop showing a wrong price" but "show the discount doing its job."

## What is broken today

On a discounted purchase the approval screen shows **three numbers that disagree**, on the screen
where money commits:

- the **discounted SKR** the player will actually send,
- a **full-price USD** figure,
- a line saying a discount was applied.

`api/_lib/purchase-catalog.js:344` already computes the right number:

    const quotedUsd = hasDiscount ? usd * (10_000 - bps) / 10_000 : usd;
    const amount = quoteAmount(quotedUsd, rate.usdPerSkr, rail.decimals);

It **prices the SKR off `quotedUsd`** and then `:355` ships only `usdAnchor: usd` - the undiscounted
one. The correct figure exists, is authoritative for what is charged, and is thrown away.

The client currently compensates with wording: `~ $9.99 before discount`. Honest, and clumsy, and it
still never tells the player what they saved.

## Build

1. **Server sends `usdEffective`** - the `quotedUsd` it already computed. Nullable exactly like
   `usdAnchor`, so the **pinned canary stays null** (it has no USD price by design and must render
   nothing rather than `$0.00`, which would read as free).
2. **`usdAnchor` STAYS.** It is the auditable authored price and the existing test calls it exactly
   that. Both travel; neither replaces the other.
3. **The client shows the effective price as THE price**, with the original and the saving alongside.
   Shape, not final copy - wording is the owner's:

       Aether Cart
       $2.39   (was $2.99 - save $0.60)
       240 SKR

4. ⭐ **The saving is a WORD AND A NUMBER, never a colour or a strikethrough alone.** The owner is
   red/green colourblind, and sale UI is conventionally red-struck text - which would convey nothing
   to her. `save $0.60` and `was $2.99` must survive a greyscale screenshot.
5. ASCII-only strings - non-ASCII renders as tofu in TMP on device.

## STOP THE PIN THAT MUST NOT BE DELETED

`test/purchases.quote.test.js:254` asserts:

    assert.equal('discountedUsd' in discounted, false,
        'do not create a second client-visible price authority');

⛔ **RE-POINT IT. DO NOT DELETE IT.** Its FEAR is correct and this repo has earned it - "one fact
written twice" is the dominant failure here and produced the stale WO-number block, a capture harness
asserting a retired constant, and a cost formatter written three times that had already drifted.

But the fear is about **AUTHORITY**, not about display:

- The **binding** number is `amountBaseUnits`. It always was.
- `PackDef.AmountFor` returns what was quoted, or ZERO, and zero renders as words.
- The client does **no price arithmetic** and fails CLOSED.

So a second DISPLAY figure creates no second authority - it replaces a wrong display with a right one.

⭐ **The re-pointed assertion should be STRICTER than what it replaces:** assert that `usdEffective`
is present when discounted, that it is NOT used to derive the SKR amount client-side, and that
`amountBaseUnits` remains the only figure any purchase path reads. That converts a blanket refusal
into a targeted guard - the same move made on `MonetizationActivationRegression` when the go-live
ruling changed: re-point a pin, never soften it.

## Do NOT

- ⛔ Compute the discounted USD **on the client**. The server already has it; deriving it client-side
  is the second authority the original test rightly feared.
- ⛔ Add a client-side SKU allowlist or any price arithmetic.
- ⛔ Let the SKR figure and the USD figure come from different places. `quotedUsd` prices the SKR;
  the same value is what ships.
- ⛔ Present a saving that is not real. If `discountBps` is absent, there is no sale and no "was"
  price - show the plain price.

## Acceptance

1. A discounted quote returns `usdEffective`; an undiscounted one is unchanged; the pinned canary is
   still null.
2. The approval screen's three numbers **agree**: effective USD, the SKR being sent, and the discount
   line.
3. The saving is legible in **greyscale** - prove it with a capture, not a source read.
4. The re-pointed test FAILS if anyone derives a price client-side or reads anything but
   `amountBaseUnits` as the binding amount.
5. Backend suite stays green (currently 56/56 with no `DATABASE_URL`).

## Note on scope

⚠ `usdEffective` is a **display** figure. It changes no amount, no rate and no settlement path.
`/verify` continues to re-derive the contract from the quote row it issued and to read **no amount
from the request body** - that is untouched by this ticket and must stay so.
