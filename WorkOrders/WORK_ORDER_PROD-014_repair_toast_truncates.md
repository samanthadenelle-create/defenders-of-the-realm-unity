# PROD-014 — The "NEED MORE TO REPAIR" toast truncates on both lines

**Status:** READY. **Silo:** HUD.
**Reported:** owner felt-test, Seeker, 2026-08-24.

## Symptom

```
NEED MORE TO REP…
115 iron short - go fa…
```

Both lines clipped.

## Why it matters more than it looks

This is the toast that explains **why a repair the player just tried was refused**. Truncated, it names neither the problem nor the remedy — the player is told "no" and not told what to do about it.

⚠ **Same class as the "Price unavailable" clipping** found on this same device the same day (14 of 16 glyphs rendered). Same lesson: **a compile-green build proves nothing about layout.** Both were found by eye, on a device, after every gate had passed.

## Investigate

- Fixed-width container vs the string length; whether the copy is authored or composed at runtime.
- ⚠ Whether these strings live in `canon-strings.json` (§7 requires player-facing copy to). The sibling `RepairHighlight` labels are **hardcoded literals** (`"Repair"` / `"Repair?"`, zero `repair` keys in canon), so this family has form.
- Prefer copy that fits the narrowest supported width over a container that grows — a container sized to the longest string moves the problem rather than removing it.

## Acceptance

- [ ] Both lines render complete at 2670x1200 **and** at the narrowest supported width
- [ ] Proven by a captured PNG that is actually opened, not by a compile

## ⭐ SCOPE EXPANDED 2026-08-24 (owner) — this is a dead end, not a text bug

**Owner, verbatim:** *"if you cannot afford you can only click off screen — should be an acknowledge,
maybe use crystals to repair, upsell small pack"*.

The truncation is the symptom. The real defect is that **a refused repair has no exit**: the player
is told "no", the words are clipped, and the only way out is tapping off-screen — which reads as a
bug, not a choice. A refusal must offer at least one thing to DO.

### The three asks

1. **An acknowledge control.** Dismissing by tapping nowhere is not a decision. ⚠ It must clear the
   marker selection too, or the player is left with a selected structure and a violet marker and no
   prompt — which is precisely the PROD-013 symptom returning by another route.

2. ⭐ **Offer the smallest sufficient pack.** ⚠ AND THIS IS NOT AN UPSELL, which matters because
   `ShortfallPackOffer` encodes a deliberate rule: *"the SMALLEST SUFFICIENT size. **No upsell at
   the shortfall moment.**"* Offering the smallest pack that closes a 115-iron gap IS that rule, not
   an exception to it. ⛔ Do NOT offer a larger rung here — the shortfall moment is when the player
   is blocked, wants it now, and is least able to evaluate. Highest conversion, worst defence.
   ⚠ **Sequence behind WO-1069** (`shortfall_resolver_never_dominated`): the resolver currently
   serves `impulse-wood-small`, which is **strictly dominated by `hearth-spark` at the same $1.99**.
   Wiring this surface to it before that is fixed would put a value trap at the point of maximum
   motivation — WO-1165 §6 calls it "the hardest finding here to defend publicly."

3. ⚠ **"Use crystals to repair" NEEDS AN OWNER RULING — it crosses WO-947.** That ruling separates
   the baskets: **regular structures cost wood + iron; magical structures cost crystals.** Letting
   crystals substitute for iron in a repair makes crystals a universal solvent and quietly retires
   the separation — and there is a live regression (`CostBasketSeparationRegression`) that exists to
   catch exactly that. It is a coherent thing to want (crystals are the uncapped premium currency and
   this gives them a sink), but it is a **composition** change, not a convenience, and it should be
   ruled explicitly rather than arriving through a repair button.

### Acceptance (additions)

- [ ] A refused repair has an explicit acknowledge that ALSO clears the marker selection
- [ ] The offered pack is the smallest that closes the gap — asserted by a test, so no future edit
      can quietly promote a bigger rung into this slot
- [ ] Crystals-for-repair ships only behind an explicit owner ruling on WO-947

### 4. The discount question (owner, same session): *"offer a 20% discount? buy it now?"*

**Recommendation: yes to a discount, NO to the shortfall being what triggers it.**

⛔ **A discount that appears BECAUSE the player was just refused is a distress discount**, and it
teaches one lesson quickly: *do not buy on the shelf, wait until you are blocked.* Three costs:
- The 20% becomes the real price and the shelf price becomes fiction.
- The players who paid full price on the shelf are the ones penalised — the worst group to punish.
- On a real-money storefront it is the shape that reads worst: a price cut aimed at the moment of
  maximum motivation and minimum judgement. ⚠ We are LIVE on the Solana dApp Store; this is not
  hypothetical.

⭐ **The fix is to make the discount a property of the OFFER, not of the MOMENT.** A one-time-ever
**first-purchase 20%** converts identically here — the player still sees "20% off" on the pack that
closes their gap — but it cannot be farmed by getting stuck on purpose, because it would have
surfaced anywhere the player met the shelf. It just happens that many players meet it here first.
That is a legitimate acquisition discount; the other is a lever on distress.

⚠ **"Buy it now" cannot be literally one tap.** WO-1157 established **server-issued quotes and
exactly ONE wallet prompt** — the wallet confirmation IS the signature and it is not skippable, by
design and by the wallet's own contract. The honest version is **one tap to REACH the confirm, with
the quote already fetched** so there is no spinner between intent and prompt. That is also the
better product: the delay we would be removing is our own latency, not a step of the player's.

⛔ And whatever it is, it must NOT be a second purchase path. The money path was consolidated
today onto server quotes for a reason; a bespoke buy-from-toast flow would be an eighth caller of
the thing we just finished making singular.

### Acceptance (discount)

- [ ] Any discount shown here is sourced from a **pack/offer property**, never computed from the
      refusal — a test asserts the shortfall surface passes no discount of its own
- [ ] First-purchase discount is one-time-ever, server-recorded (client-side would be trivially
      replayed), and identical wherever the pack appears
- [ ] The buy path is the SAME quote + confirm path as the shelf — no second implementation

## ⭐ OWNER RULINGS 2026-08-24 - BOTH SETTLED, both overriding my recommendation

I argued the other way on both. She ruled. These are now canon for this ticket and the concerns are
CLOSED, not open - do not re-litigate them in a later session.

### RULING 1: **crystals ARE a universal repair currency.**

⛔ **This AMENDS WO-947, it does not bypass it.** The basket separation now reads: *regular
structures are BUILT and UPGRADED with wood + iron; magical structures with crystals; **REPAIR may be
paid in crystals for anything**.* `Assets/Editor/Regression/CostBasketSeparationRegression.cs` must
be **amended to encode the repair exception explicitly** - a deliberate, named carve-out. ⚠ If the
suite is instead loosened or the case deleted, the separation stops being enforced at all and the
next accidental crystal cost lands silently. The exception is the point; the enforcement stays.

⭐ **Crystals gain a real sink.** WO-1165 §3 found crystals are the only currency that holds value -
uncapped, gating rare+ gear. A repair sink is the first thing that consumes them at the pace they
accumulate.

⚠ **Set the crystal price so it is a convenience, not a discount.** If crystals-per-iron is cheap,
crystals become the default repair currency and iron's sink disappears - which would undo the reason
iron was unlocked this morning. Price it above the natural exchange so the player who HAS iron uses
iron.

### RULING 2: **the 20% fires at the shortfall.**

Implement as asked. Three implementation constraints that are correctness, not objection:

1. ⛔ **The discount is SERVER-ISSUED, inside the quote.** A client-computed 20% is trivially
   spoofed into 100%, and this is real money on a live storefront. It rides the WO-1157 quote path -
   `PurchaseQuoteService` - like every other price. There is no second price authority.
2. ⚠ **Rate-limit it server-side.** A discount the player can summon by re-triggering a refusal is
   a permanent 20% off with extra taps. One per player per window, recorded server-side; the client
   never decides eligibility.
3. **Log every issuance** to `purchase_quotes` with the reason, so the discount rate is a number we
   can read later rather than a thing we assume.

### RULING 3 (WO-1169 §5 Q2): **F8 captures stay on this machine for now** - revisit once
`bug_reports` has accepted a single real row. Nothing is lost meanwhile; captures still land locally.
