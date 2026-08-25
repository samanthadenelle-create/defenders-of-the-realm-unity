# WORK ORDER 1069 — The shortfall resolver must never serve a dominated offer

**Status:** FIXED 2026-08-24 (`6bb61a810`) — awaiting owner felt-verify. Verified at source: `api/_lib/purchase-catalog.js:84` reads `'hearth-spark': 4.99`, committed, and the quote suite is green. `ShortfallPackOffer` was correctly left UNCHANGED; the original resolver-oriented acceptance criteria are **superseded by the lead ruling** and do not read as open. *(Status audit 2026-08-24: lead-verified bucket correction; body unchanged.)*
**Minted:** 2026-08-24 (UI seat), banner header bumped 1069 → 1074 in the same edit (with 1070–1073).
**Provenance:** WO-1165 §6 (CLI-verified finding) + the external monetization review the owner
ADOPTED 2026-08-24 (*"Find all SKUs capable of satisfying the shortfall → calculate effective player
value → recommend the strongest valid offer. Never knowingly show a dominated offer."*), refined
against the owner's WO-1176 §5c ruling — the two compose; see §3.

---

## 1. RCA — the defect, from the sources already on file

`ShortfallPackOffer` stops at the **first rung that covers the gap**. A 900-wood shortfall therefore
offers `impulse-wood-small` — **1,000 wood for $1.99** — while `hearth-spark` at the **same $1.99**
grants 1,500 wood + 800 iron + 150 crystals + 500 food + 100 coins. Strictly dominated: more of the
shortfall resource AND four other lines, for identical money.

**The data already knows.** `packs.json:562` records verbatim that *"the small rung is strictly
dominated by Hearth Spark at the same $1.99"* — and hid the small rung from the SHELF. The shortfall
resolver was never taught the same fact, so the store's one moment of peak purchase intent (player
blocked, wants it now) is the one surface still serving the known-bad offer. WO-1165 called this
*"the hardest finding here to defend publicly"*; the adopted review calls the fix *"both better
monetization and less likely to make players feel fleeced."*

## 2. The rule to implement

> Among the SKUs that (a) cover the shortfall resource gap, (b) sit **at or below the price of the
> smallest sufficient SKU**, and (c) are actually purchasable by this wallet — offer the one with
> the **highest player value**. Never offer a SKU that another eligible SKU dominates.

"Dominates" = ≥ in every granted line and > in at least one, at the same-or-lower USD anchor.

## 3. ⛔ The two rulings this composes — do not re-litigate either

1. **WO-1176 §5c (owner):** *"allow upsell on the shelf; keep the shortfall moment clean."* The
   smallest-sufficient PRICE stays the ceiling. This WO changes **which SKU is offered at that
   price**, never the price itself — swapping `impulse-wood-small` for the same-priced
   `hearth-spark` is not an upsell, it is removing a value trap. An implementation that surfaces a
   higher-priced "better deal" at the shortfall moment has broken §5c.
2. **WO-1176 §2 (owner):** *"make hearth spark one time only."* Once limits land, a wallet that has
   consumed its one Hearth Spark can no longer be offered it — condition (c) above. Until server
   limits exist, the resolver may treat all SKUs as purchasable; the seam must be the same
   entitlement check WO-1176 §3 builds, not a parallel one.

## 4. Files / seams (verified names; CLI to confirm at HEAD)

- `ShortfallPackOffer` — the resolver (client). The change is the candidate-gather + compare; the
  offer surface/UI is untouched.
- `packs.json` grants + `USD_ANCHORS` (mirror law) are the value inputs — **read, not edited**.
- WO-1176 §3's entitlement check, when it lands, feeds condition (c).

## 5. What NOT to touch

- No price changes, no SKU deletions (the dominated small rungs' fate is WO-1165 §8 / WO-1176's
  ladder work, not this ticket).
- No new offer chrome at the shortfall moment; same single-offer presentation.
- The shelf. This is the shortfall surface only.

## 6. Acceptance

- [ ] 900-wood shortfall offers `hearth-spark`, not `impulse-wood-small` (today's exact case)
- [ ] The offered SKU's price ≤ the smallest sufficient SKU's price — asserted by a test across
      every authored shortfall-capable SKU pair (no hand-picked cases)
- [ ] Property test: for every possible single-resource shortfall amount, the offer is never
      dominated by another eligible SKU at ≤ its price
- [ ] With a (mocked) consumed one-time Hearth Spark entitlement, the resolver falls to the next
      best non-dominated candidate — never a dead offer
- [ ] Oracle registered in `DataRegression.RunAll`; `REGRESSION_OK` fresh

## ⛔ LEAD RULING 2026-08-24 - THIS TICKET POINTED AT THE WRONG LAYER. Codex was right.

The Codex intake pass refused to implement this and was **correct to refuse**. Verified at source:

1. `hearth-spark` is **not an impulse pack at all** - `impulse=None`, no `impulseResource`, no
   `impulseSize`. `FindValid` never even considers it.
2. ⛔ Even if it did, **`IsSingleKeyResourceOnly` would reject it**, and not incidentally: that guard
   enforces **WO-947 §12c guardrail 1**, whose own error text says a multi-resource impulse bundle
   *"re-mixes the cost baskets through the back door and is FORBIDDEN."*

⭐ **So "serve `hearth-spark` at the shortfall" asks the resolver to break a binding ruling. The
resolver is right. The ticket was wrong.**

### The defect is real, and it is in the DATA

| SKU | price | grant |
|---|---|---|
| `impulse-iron-small` | **$1.99** | 400 iron |
| `hearth-spark` | **$1.99** | **800 iron** + 1500 wood + 500 food + 150 crystals + 100 coins |

Twice the iron **plus four other resources, at the identical price.** A player who buys the targeted
offer gets objectively less for the same money. That is a genuine value trap and WO-1165 §6 was right
to call it the hardest finding to defend publicly.

⚠ **But the fix is pricing, not resolver logic.** Changing the resolver would breach guardrail 1 to
paper over a pricing mistake - and would leave the trap intact everywhere else the two SKUs meet.

### Restated scope

- ⛔ **`ShortfallPackOffer` is NOT to be modified.** Its guardrails are correct.
- ⭐ Add a **regression** asserting **no impulse rung is strictly dominated by any other purchasable
  pack at the same USD anchor** - so this cannot silently return the next time a bundle is authored.
  That test is the durable fix; the price change is the one-time correction.
- The price/grant correction itself is an **owner ruling** (below) - real-money ladder policy.

⚠ **`hearth-spark` is `DEVNET_CANARY_SKU`** (`api/_lib/purchase-catalog.js:29`) - the pinned canary
the quote path is tested against. Any price change to it touches the test path; check that first.

### ⭐ OWNER RULING 2026-08-24: **`hearth-spark` moves to $4.99.**

It grants five resources including 800 iron; a **targeted single-resource top-up must not cost the
same as a full starter bundle**. `impulse-iron-small` stays at $1.99/400 iron, and the domination
disappears because the two SKUs stop sharing a price point.

⛔ **`ShortfallPackOffer` is still NOT modified.** The resolver was always right.

⚠ **`hearth-spark` is `DEVNET_CANARY_SKU`** (`api/_lib/purchase-catalog.js:29`) - the pinned SKU the
quote path is tested against. **Re-check the quote/verify test path with the new anchor before
shipping**, or the canary starts asserting a price that no longer exists.

**Change `USD_ANCHORS['hearth-spark']` 1.99 -> 4.99 in `api/_lib/purchase-catalog.js`, and mirror it
wherever the client shelf reads a price** - then the new regression (no impulse rung strictly
dominated at its own USD anchor) proves it and keeps proving it.
