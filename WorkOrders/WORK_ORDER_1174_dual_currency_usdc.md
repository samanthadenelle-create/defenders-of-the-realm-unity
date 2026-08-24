# WORK ORDER 1174 — Sell in USDC as well as SKR. The player swaps in their own wallet; we integrate nothing.

**Status:** ⏸ **PARKED by the owner, 2026-08-24** — see §0. The analysis stands and the work is
real; it is simply not the next thing. Sequenced AFTER WO-1173 (schema-parity gate) whenever it
is picked up, because this adds a currency to the money path and today proved that path is only
as safe as the schema behind it.

## 0. ⏸ WHY THIS IS PARKED — the funnel is empty, not narrow

**Owner, 2026-08-24, verbatim:** *"lets table the currency thing for usdc and sol"* ·
*"first we need players then a desire to buy"* · *"then a purchase"*.

⭐ **She is right, and it is worth writing down because the pull is strong in the other
direction.** Today the purchase rail went from never-having-worked to a proven mainnet sale, and
the instinct after that is to keep improving the rail. But a second currency widens the *last*
step of a funnel whose *first* step has one person in it. USDC removes friction for players who
do not exist yet.

**The order is: players → desire to buy → purchase.** Work that adds players or reasons to want
something outranks work that smooths the checkout. Revisit this when there is traffic to
measure — at that point the SKR-swap friction becomes a number instead of a theory.


**Minted:** 2026-08-24 (CLI), banner bumped 1174 → 1176 in the same edit (with WO-1175).
**Provenance:** owner, 2026-08-24 — *"next can we test with SOL > Jup > or usd"* → *"not fiat USD
USDC (Have to assume crypto already"* → *"the idea is they use their wallet to swap right?"*.

---

## 1. The ruling that shapes everything: THE SWAP IS THEIRS, NOT OURS

⛔ **No Jupiter integration. No swap inside the purchase flow.** Every wallet worth supporting — the
Seeker's included — has swap built in.

**Why this is a correctness decision, not just a scope one:** a swap inside the purchase adds a
SECOND transaction and slippage between "money left the player" and "goods were granted". That gap
is the paid-but-not-granted window, and on 2026-08-24 we watched a real 391 SKR payment fall into
it (a stale CHECK constraint) and only recover because the quote was consumable exactly once with
that signature. **Do not widen a window we have already seen swallow real money.**

## 2. Why USDC, and why it is EASIER than what we ship today

| | SKR (today) | USDC | SOL |
|---|---|---|---|
| Player already holds it | **rarely** | very often | almost always |
| Rate oracle needed | yes | **no** | yes |
| `ceil` rounding hazard | tolerable | **none** | ⛔ severe |
| Transfer shape | SPL → ATA | **SPL → ATA (identical)** | native, second verify branch |

⭐ **The rounding hazard vanishes.** USDC is 1:1 with USD at 6 decimals, so `$2.99` is exactly
`2_990_000` base units. No oracle, no `Math.ceil`, no rate drift between card and signature.

⛔ **AND IT REMOVES A LIVE RISK.** WO-1165 §10: selling in a volatile token means a player can check
our rate against a public market in ten seconds, with no receipt to arbitrate and no refund path on
an SPL transfer. In USDC **the price is the price**. That exposure disappears rather than being
mitigated.

⚠ **SKR is not our token.** It is Solana Mobile's governance token — we do not mint it, and we do
not control its liquidity. Every purchase today therefore forces a swap into a token the player had
no other reason to hold, on a pair whose depth we cannot influence. That is a funnel cost AND a
liquidity risk we carry without owning either side.

**Keep both.** USDC is the easy path; SKR keeps its ecosystem alignment and becomes the *rewarded*
path (WO-1175).

## 3. The work

| # | Change | Notes |
|---|---|---|
| 1 | Widen `purchase_quotes.currency` CHECK | today `IN ('SKR')`. ⚠ Do it via WO-1173's migration path, NOT pasted SQL — that is how the constraint drift happened. |
| 2 | **USDC ATA on the treasury vault** | one-time, on-chain. ⛔ Must exist BEFORE the first quote is issued: `/verify` runs after settlement, so a missing ATA is discovered with the money already moved. |
| 3 | `purchaseRail(network)` → `purchaseRail(network, currency)` | returns mint / recipientAta / decimals per currency. Today it hardcodes the SKR mint. |
| 4 | Quote skips the rate path for USDC | `amountBaseUnits = usdAnchor * 10^6`, integer math. `rate` and `rateSource` are NULL for USDC — **and that is meaningful, not missing**: there was no conversion. |
| 5 | Currency choice in the store | the only real UI work. See §4. |
| 6 | `/verify` checks the quote's mint | already derives its contract from the persisted row, so this should need no change — **prove it, do not assume it**. |

## 4. ⛔ ONE QUOTE, ONE CURRENCY — the invariant that keeps this safe

A SKU may be offered in either currency, but **each quote names exactly one**, and `/verify` checks
against **that quote's** mint and amount — never a default, never the request body.

⚠ This is precisely why WO-1158's design holds: `/verify` re-derives the whole transfer contract from
the persisted quote row. A currency the player did not choose can therefore never be verified
against, and a player who switches currency mid-flow gets a NEW quote rather than a reinterpreted
one. **Any implementation that lets currency arrive from the request body has broken the rail.**

## 5. Acceptance

- [ ] `SCHEMA_PARITY_OK` green first (WO-1173) — including the widened currency CHECK
- [ ] Treasury USDC ATA exists and is verified on chain BEFORE the first USDC quote
- [ ] A $2.99 USDC quote is exactly `2990000` base units, with `rate`/`rateSource` NULL
- [ ] A SKR quote for the same SKU is unchanged from today
- [ ] `/verify` refuses a payment made in the OTHER currency than the quote names
- [ ] One real USDC purchase: chain-confirmed delta, `expected = observed`, `fulfilled`
- [ ] `?view=purchases` shows currency per row so ops can tell the rails apart

## 6. Not in scope

Jupiter or any in-app swap (§1). SOL (⛔ needs the `ceil`-to-base-units fix AND a native-transfer
verify branch — its own ticket). Fiat (a processor, chargebacks, and a refund path we do not have).
