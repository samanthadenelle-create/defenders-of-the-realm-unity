# WORK ORDER 1069 — The shortfall resolver must never serve a dominated offer

**Status:** READY TO IMPLEMENT (CLI). No open rulings — the two governing rulings are already taken.
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
