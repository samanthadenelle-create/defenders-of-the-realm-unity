# WORK ORDER 1072 — ONE crystal valuation, then promote the crystal row

**Status:** SPEC — ⛔ NOT a data-authoring task; there is a genuine CONTRADICTION the owner must resolve. The ticket adopts a modest volume curve ($1.99 base, $4.99 at base +5%) **while also treating the existing impulse-crystal rungs as the anchor** — and those rungs run ~126 / ~234 / ~321 crystals per dollar ($1.99→250, $2.99→700, $4.99→1600), i.e. the $4.99 rung is ~155% above the $1.99 rate, not 5%. **No single base rate satisfies both.** The owner must pick the authoritative anchor (the volume curve OR the impulse family) or authorize repricing the impulse family, and must rule rounding. *(Status audit 2026-08-24: lead-verified bucket correction; body unchanged.)*
**Minted:** 2026-08-24 (UI seat), banner header bumped with the 1069–1074 block.
**Provenance:** WO-1165 §5 (CLI-verified numbers) + the external review the owner ADOPTED
2026-08-24 (*"first establish one crystal valuation … then make every SKU respect that underlying
valuation. Otherwise the spreadsheet detectives will discover that half your storefront is
decorative foliage."*).

---

## 1. RCA — the 3.46× hole, verified

`4 × impulse-crystals-large` = **$19.96 → 6,400 crystals** while `patron-of-elarion` = **$19.99 →
1,850 crystals** (WO-1165 §5). Same money, 3.46× the one currency that holds value (uncapped, gates
rare+ gear). And the good deal is the HIDDEN one — `impulse-crystals-*` is shortfall-only, so the
best-value product in the store is invisible while the shelf sells capped commodities. Any player
with a spreadsheet finds this in minutes; on a crypto rail those players are the audience.

## 2. The rule

**One base crystals-per-dollar rate, with a modest volume bonus by price tier:**

```
base rate at $1.99 → +5% at $4.99 → +10% at $9.99 → +15% at $19.99 → +20% at $49.99
```

Every SKU's **crystal line** — impulse packs, baskets, ledgers, the WO-1070 Vow grant — is derived
from (or checked against) this curve. The curve itself is DATA (one authored table), mirrored under
the existing **MIRROR LAW** so `test/purchases.quote.test.js` (or a sibling) fails on drift — a
valuation that lives in a comment is a hope, not a law.

## 3. Order inside this ticket

1. Author the valuation table; pick the base rate so the CURRENT impulse-crystal rungs are the
   anchor (they are the honest price today; re-pricing the baskets' crystal lines up beats nerfing
   the impulse rungs down — nerfing punishes the players who found the fair price).
2. Re-derive every SKU's crystal line; adjust the outliers (the §1 pair first).
3. **Then** promote a crystals row to the shelf (shortfall-only today) — promote AFTER the fix,
   never before, or the shelf advertises the discrepancy.
4. Re-examine `BEST VALUE` (WO-1165 §9.2): the badge must be defensible under the new valuation,
   on a stated metric — or removed.

## 4. What NOT to touch

- USD anchor prices themselves (the 1.99/4.99/9.99/19.99/49.99 ladder is settled, WO-1158 §5).
- Non-crystal grant lines (the wood/iron/food shape is WO-1176's ladder work).
- `keepers-satchel` stays hidden (WO-1165 §8: 180 crystals/$ — it fails this curve; its fate is
  delete-or-reprice under the new table, decided at implementation).

## 5. Acceptance

- [ ] A single authored valuation table exists in data; a test derives every SKU's crystal line
      from it and fails on any deviation (the mirror-law pattern)
- [ ] The §1 pair is within the tier-bonus envelope of each other (no >1.25× cross-SKU discrepancy
      at equal spend)
- [ ] Crystal row visible on the shelf, cards quote correctly on the live rail
- [ ] `BEST VALUE` badge either justified under the stated metric or gone
