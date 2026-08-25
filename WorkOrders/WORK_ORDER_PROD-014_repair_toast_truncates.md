# PROD-014 — The "NEED MORE TO REPAIR" toast truncates on both lines

**Status:** READY - PARTIAL (board reconcile 2026-08-25). **Slice (b) CODE HAS LANDED (`10912de95`) but its acceptance capture is still OWED, and slices (c) + (d) remain blocked** - so the ticket stays in Ready. Per-slice state:
- **(a) Label clipping — FIXED 2026-08-24** (`130ec84ab`: `HubRepairAffordance` now calls `FitBlock` at the legibility floor, copy moved to `WallRepairStrings.cs`, plus a new ellipsis detector). Awaiting **owner felt-verify only** — no code work here.
- **(b) Acknowledge / close control — READY. THIS is the slice to take.** Component, UI primitive, handler and the selection-clearing path (`WallRepairController.CancelRepair`) are all specified in the body below.
- **(c) Smallest-sufficient pack offer — ⛔ BLOCKED** until WO-1069 is integrated; it must reuse `PackStore.FocusShortfall`, never a second offer path.
- **(d) Crystals-for-repair — ⛔ BLOCKED.** The ruling says price it *above the natural exchange* but names **no rate**; choosing one would be inventing economy policy. Needs an owner number first.
- **(e) Discount — NOT PART OF THIS TICKET**, split out to **WO-1177**.

**Silo:** HUD.
⚠ **The TITLE is historical.** The truncation it names is fixed; what actually remains is *a refused repair needs an exit*. ⛔ **Do NOT rename the file** — it is referenced elsewhere.
*(Board note 2026-08-24: the previous `READY — PARTIAL` banner was too broad and would have authorised the whole ticket, including the two blocked slices. Bucket unchanged (Ready); the slices are now named individually.)*
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

## ⭐ DIAGNOSIS 2026-08-24 - the cause is proven from source, and it is self-inflicted

**⚠ It is not a toast.** It is a **persistent mid-left button** -
`Assets/_Modules/Village/Walls/HubRepairAffordance.cs:293-294` composes both lines. The HUD repair
prompt (`HudKitController.ShowRepairPrompt`) is a *different* surface and it **does** have
Repair + Cancel. That is why the owner had no buttons: she was looking at the other one.

### The truncation - three facts compose

1. `ElarionUiKit.Button` runs **`FitSingleLine`** on every label
   (`ElarionUiKitObsidian.cs:2924-2942`): `NoWrap` + `TextOverflowModes.Ellipsis` + autosizing
   `[30..50]`.
2. ⛔ **`HubRepairAffordance.cs:430-431` then opts OUT** - `enableAutoSizing = false;
   fontSize = 26f;` - **without clearing `NoWrap` or `Ellipsis`.** Those stay armed. *(Verified at
   source.)*
3. Line 293 writes a **two-line string with an embedded `
`** into that label.

So each `
`-separated line ellipsizes independently - exactly the two-line clip photographed.
⚠ Turning autosizing off **also disarms `UiKitTextFitGuard`**, which only manipulates
`fontSizeMin`/`fontSizeMax` and is inert when autosizing is off. **And 26px is below the kit's own
mobile legibility floor (`FontFloor = 30`)** - the label is clipped *and* sub-legible.

⚠ Confirmed: **zero `repair` keys in `canon-strings.json`** (both copies). The family's home is
`WallRepairStrings.cs`; neither truncating string is in it.

### ⛔ The dead end - confirmed, and PROD-013 DOES return by another route

**There is no dismissal control at all.** `BuildCanvas` (`:389-437`) creates one Button and one
label, nothing else. A shortfall does not hide the affordance. Tapping the button on the unaffordable
branch is a **dead tap** - `OnClick` traces, calls `Refresh()`, returns to the identical state.

And the marker stays lit. `WallRepairController.HandleTap:398` is a bare **`if (HasSelection)
return;`** - a world tap while a structure is selected is discarded **before any raycast**, by design
(the prompt is deliberately modal). `ClearSelection` is reachable **only** via `CancelRepair()`,
fired only by the HUD prompt's Cancel. **So tapping empty ground clears nothing**, and the player is
left with the violet marker still reading "Repair?" - PROD-013's exact symptom through a different
door.

### The fix, in order

1. ⭐ **Delete `HubRepairAffordance.cs:430-431`** and call `ElarionUiKit.FitBlock(_label, minSize:
   FontFloor)` - `FitBlock` sets `Normal` wrap + `Truncate`, the correct policy for a deliberately
   two-line label, and **re-arms the fit guard**.
2. Move all four strings into `WallRepairStrings.cs` as `// LOCALIZE:` consts, shortened to fit the
   narrowest width rather than growing the box: `"NEED MORE"` (19 glyphs -> 9), `"{0} short"`
   (drop `" - go farm"` - the farm instruction is not what a refused player needs; the actions are).
3. A refusal card with an **`ElarionUiKit.ObsidianCloseButton`** (⛔ never an X glyph) whose handler
   calls the **existing** `WallRepairController.CancelRepair()` - which already clears the selection,
   hides the marker, and rescans. **No new deselect path.**
4. ⚠ **`HandleTap:398` is its own line item, not part of this.** Making a world tap clear an open
   selection changes a deliberately-modal interaction documented at `:391-397`. Ship the explicit
   Close first.

### Crystals (Ruling 1) - one choke point, already

**`WallRepairController.CostForFraction:590`** is the single line that says no
(`crystals = 0, // owner 2026-07-11`). ⛔ **Do not touch it** - the base price stays in-kind. Add
`CrystalPriceFor(shortfall)` beside it, with the conversion rate **authored in data**
(`structures-catalog.json`, beside `repair_default`), **never a C# literal**, and priced **above**
the natural exchange so a player holding iron still spends iron. Then one
`TryRepairAllWithCrystals()` that spends held wood/iron **plus** the crystal top-up in a **single**
`EconomyService.TrySpend` - one atomic debit, no second repair system. The spend rail already carries
crystals (`EconomyService.cs:298/:315`).
⚠ `MaterialsZero` (`:698`) **ignores the crystals slot entirely**, so a crystals-only cost reads as
"free" today - fix that in the same pass or the carve-out ships with a hole.

### Pack offer

`ShortfallPackOffer` already picks correctly: first rung whose `ImpulseAmount >= missing`, and its
own comment names any further comparison "literally the upsell WO-1037 §1 forbids". For **115 iron**
it returns **`impulse-iron-small` (400 iron @ $1.99)**. ⛔ Call
**`PackStore.FocusShortfall(label, missing)`** - do not call the resolver directly and render your
own buy button; the resolver deliberately cannot grant or charge.
⚠ **Sequence behind WO-1069:** `hearth-spark` grants **800 iron at the same $1.99** - twice the rung
this would offer, plus four other lines. Wiring the refusal to a dominated pack at the point of
maximum motivation is the worst possible place for it.

### The 20% - SPLIT OUT to WO-1177

Confirmed: **exactly one price authority** (`api/_lib/purchase-catalog.js` `USD_ANCHORS` +
`buildQuoteBody`; the client is arithmetic-free by construction) and **zero existing discount concept
anywhere under `api/`** - `grep -rni discount api/` returns **0**. It is greenfield server work and
the largest item here, so it ships separately rather than holding up the reported defect.
