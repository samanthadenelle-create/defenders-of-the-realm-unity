**Status:** FIXED 2026-08-23 (`e526e013f`) — AWAITING OWNER FELT-TEST TO CLOSE. The server quotes the price: `POST /api/purchases/quote` issues a single-use 5-minute binding row; `PackStore` fetches it immediately before the wallet prompt and states the exact SKR, the rate and its source; `verify.js` builds its exact-equality contract FROM THE QUOTE ROW IT ISSUED and **reads no amount field from the body at all**; the entitlement records `quote_ref`/`usd_anchor`/`usd_rate`/`rate_source`. Expiry is judged against the transaction's own `blockTime` + 180s grace, never wall-clock at verify time (which would refuse honest players whose money already moved). Proven: `node --test` **37/37**, including *"the verified contract is built from the QUOTE ROW"* and *"transferring a DIFFERENT amount than quoted is REFUSED"*. Found a live-fire schema bug on the way — `purchase_entitlements.network` CHECKed `IN ('devnet','mainnet')` while the code writes `'mainnet-beta'`, so any DB built from `schema.sql` would have REJECTED EVERY MAINNET INSERT after settlement. ⚠ Felt-test still owed and it needs a **real ladder SKU**: both canaries answer `pinned:true` with **no quote row**, so a canary purchase proves the rail and proves NOTHING about the quote path. Prior status: "READY TO IMPLEMENT — HIGH. This blocks selling any real pack for SKR." — the work shipped and the line never moved (board-lies-about-tickets class). Unblocked WO-1159 (go live).

# WORK ORDER 1158 — The server must QUOTE the price, not verify a constant

**Minted:** 2026-08-23 (CLI, banner bumped 1157 -> 1159 covering WO-1157 and this)
**Lane:** Wallet / backend / money path. **Class:** ARCHITECTURE — the client cannot be authoritative for a number the server checks.
**Owner framing, 2026-08-23:** *"its they buy for 3 skr at X price so thats what resolves on db"* — *"3 times X value at purchase time"*.

---

## 1. THE PROBLEM, STATED EXACTLY

A pack is priced in **USD** and paid in **SKR**, so the SKR amount depends on the rate at the moment
of purchase. The client now resolves that rate from a market oracle (`SkrValuationOracle`,
`PackCatalog.AmountFor`).

But the backend verifies the on-chain transfer against a **hardcoded** `amountBaseUnits`
(`api/_lib/purchase-catalog.js`) by **EXACT EQUALITY**.

> ### ⛔ A CLIENT-RESOLVED PRICE AND A SERVER-PINNED CONSTANT CANNOT BOTH BE RIGHT.
> The moment the market moves, the client sends N and the server expects M. `/verify` runs **AFTER**
> the transfer settles, so the purchase fails with **the money already gone and nothing granted**.
> Same paid-but-not-granted family as the 6-vs-9 decimals near-miss, arriving through a different door
> — and this one fires on a *market move*, which is not a deploy, so nobody is watching when it does.

**Today the blast radius is small only by accident:** the server catalog contains ONLY the two canary
SKUs, so every real pack already 503s at `/verify`. That is not safety, it is absence. Add one real
pack and the bug is live.

An interim guard is in place — `PackCatalog.IsServerPinnedSku` exempts the two canary SKUs from
repricing (their amount IS a protocol constant, being a proof-of-rail rather than a sale). ⚠ **That
guard is a stopgap, not the design.** Do not extend it by pinning real packs; pinning a USD-priced
pack to a fixed SKR amount just moves the error to the player's wallet.

## 2. THE DESIGN — the server decides the number

1. **Quote.** Client names the SKU. The **server** resolves the rate, computes the amount, and returns
   `{ quoteId, sku, currency, amountBaseUnits, decimals, rate, rateSource, expiresAt }`.
2. **Pay.** Client transfers **exactly** `amountBaseUnits` — it does no arithmetic of its own.
3. **Verify.** `/verify` checks the chain against **the quote it issued** (looked up by `quoteId`),
   not against a constant.
4. **Record.** The row stores the amount, the rate and the quote id, so *"3 SKR at $0.00755954"* is
   what the DB says forever — the owner's requirement, and what makes revenue reporting truthful.

**Why this is the standard shape:** it is the same move as WO-1157's session token — a short-lived,
server-issued artefact the client merely presents. The client stops being authoritative for anything
the server checks.

## 3. ⛔ CONSTRAINTS

- **Quotes EXPIRE** (suggest 2-5 min) and are **single-use**. A stale quote must be refused with a
  worded reason and a re-quote, never silently honoured — an unexpiring quote is a free option on a
  volatile asset, and a player could sit on a favourable rate indefinitely.
- **The rate is read server-side.** A client-supplied rate is a client-supplied price.
- ⚠ **Decide and DOCUMENT the rounding rule**, including who it favours. The current client uses
  `ceil(USD / 24h-low)`, which resolves to MORE SKR than spot — that is a real pricing decision, not a
  rounding detail, and it is the owner's to rule on. Whatever is chosen, the quote must return the
  exact integer base units so nobody re-derives it.
- **The oracle must fail CLOSED.** If the rate is unavailable, refuse to quote with a worded message.
  ⛔ Never fall back to a stale or catalog price for a real sale — that is charging a made-up number.
- **Exactly-once survives.** The entitlement is still claimed before the grant (`PurchaseGate.cs:285`)
  and still keyed by the transaction signature.
- The backend `package.json` is the **Vercel** deployment: no runtime dependency for a market fetch
  that a plain `fetch` can do, and cache the rate server-side rather than calling per request.
- ⚠ Third-party rate source = a third-party dependency on the money path. Log which source and which
  value backed every quote, so a disputed charge can be reconstructed.

## 5. TRANSPARENCY — the player sees a real-money price, not just a token count

**Owner ruling 2026-08-23:** *"we should be transparent that price is approx 2.99 or 5.99 or 9.99"*.

The USD figure is the **authored anchor** (the 2.99 / 5.99 / 9.99 ladder people already understand).
The SKR figure is **derived from it at purchase time**. A card showing only "396 SKR" tells a player
nothing about what they are spending — and a store that obscures real-money cost reads as a store
with something to hide, whatever the intent.

**What every pack must show:**
- the **USD anchor**, marked approximate: **`≈ $2.99`**
- the **exact SKR amount** that will actually be transferred

**⚠ Which number is "approximate" is not a wording preference — get it the right way round.** The
player pays **SKR**, and the amount charged is EXACT (the quote pins it to the base unit). What
floats is the dollar value, because the rate moves. So the SKR is stated precisely and the USD
carries the "≈". Saying "$2.99" flat while charging a rate-derived amount is the misleading version.

**At the confirmation step** — the moment before the wallet prompt — state the exact SKR amount and
the rate it came from. That is the last screen where a player can still decline, so it is the one
that must be unambiguous.

⛔ **Never let hue carry any of this.** The owner is RED/GREEN COLOURBLIND: affordability, discount
and state are WORDS (`StorePackCard` already does this deliberately — follow it). The greyscale
check is the acceptance test.

⚠ If the quote's USD anchor and the displayed one can ever disagree, show the one the SERVER quoted.
Two prices on one screen is worse than a stale one.

---

## 4. ACCEPTANCE

- [ ] Buying a real pack succeeds end-to-end with a server-issued quote
- [ ] The DB row records amount, rate, rate source and quote id
- [ ] An expired quote is refused with a worded reason and a clean re-quote
- [ ] A tampered amount (client sends a different number than quoted) is REFUSED
- [ ] The rate oracle being down refuses the sale rather than inventing a price
- [ ] The canary SKUs still work unchanged (fixed amount, no quote needed)
- [ ] `node --test api/test/` green, with cases for quote / expiry / tamper / oracle-down
- [ ] Every pack card shows an approximate USD anchor (`≈ $2.99`) AND the exact SKR amount
- [ ] The confirm step states the exact SKR and the rate behind it, before the wallet prompt
- [ ] Greyscale screenshot: price, affordability and state all still read
