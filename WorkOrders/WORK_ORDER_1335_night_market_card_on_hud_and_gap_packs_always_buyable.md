# WORK ORDER 1335 - The Night Market card anchors on the HUD, and the gap packs become always-buyable

**Status:** FIXED 2026-09-03 - Night Market card seated top-left of the (empty) Minimap mount, measured clear of the movement stick, gear, Heart bar and hero plate at 2670x1200 / 2400x1080 / 1920x1080; opens the SAME `PackStore` as the walk-up vendor. Gap-pack authoring notes rewritten. ⭐ AND THE INVESTIGATION CORRECTED THE PREMISE: there is no client-side shortfall gate to remove - all three packs already pass `IsOnBrowsableShelf` and `PurchaseGate.CanBuy` has no impulse clause. The `UNAVAILABLE` she photographed is `Price unavailable` from the QUOTE service holding no server row on that pass - her original words were literally accurate. Separate ticket. Built and INSTALLED in `2026.09.03.353742`. AWAITING HER FELT-VERIFY; PO closes. *(Prior line:)* **Status:** READY TO IMPLEMENT
**Silo / Lane:** HUD / store entry + monetization surfacing
**Type:** EXISTING assets, RE-SITED + a ruled design change
**Minted:** 2026-09-03 (CLI) on two direct owner rulings, from a device felt-test.
**Severity:** P2 - the revenue surface. She felt-tested everything else as *"tested perfect"*.

## RULING 1 - the store entry is a CARD ON THE LEFT, not a bar face

> *"the realm store is hidden away needs a permanent face on hud"*
> *"can you take the realm store card from settings > night market and anchor it smaller to left side
> on hud"*

**This supersedes WO-1334's approach.** WO-1334 proposed re-pointing a dormant action-bar ordinal.
She has ruled otherwise: **reuse the existing Night Market CARD** (the one already built under
`settings > night market`), shrink it, and **anchor it to the LEFT side of the HUD** as a permanent
element.

- **REUSE the existing card.** Do not author a second store entry widget. The card exists and she
  picked it by name - take that asset, do not reinterpret it.
- **Smaller.** It is a permanent HUD element competing with gameplay, not a menu row.
- **Left side.** ⚠ Read the current left-edge occupancy from a device screenshot before placing it:
  the hero portrait/health block sits top-left, the settings gear and the virtual stick are lower-left.
  **Do not overlap the stick** - that is the movement control.
- The action bar keeps its five faces (`BUILD · TALK · HERO · JOURNEY · MANAGE`). `ButtonCount`
  stays 7, no ordinal is renumbered, and the dormant `Map` ordinal is left alone (CLAUDE.md s7).
- **One destination, two doorways** (WO-1164): this card and the walk-up Realm Store building must
  open the SAME `PackStore` screen. Do not greenfield a second store.

## RULING 2 - the "Close the Gap" packs are ALWAYS BUYABLE

> Asked directly whether the shortfall gating should stand, she chose: **"They should always be
> buyable."**

⛔ **THIS OVERTURNS A PRIOR OWNER RULING, DELIBERATELY. Record it, do not treat it as a bug fix.**

`packs.json` authors these three with a theme note reading:
> *"Surfaced ONLY against a real shortfall, never as a storefront row."*
(WO-1037, owner ruling 2026-08-16 option (b); WO-947 s12c guardrail 1.)

**That gating is now RETIRED by the owner, 2026-09-03.** `Timber Wagon` (`impulse-wood-medium`),
`Ingot Crate` (`impulse-iron-*`) and `Quarry Cart` (`impulse-stone-*`) become permanent storefront
rows.

⭐ **THEY ARE NOT MISSING PRICES - VERIFY THIS BEFORE "FIXING" ANYTHING.** Every one of them IS in
`USD_ANCHORS` in `api/_lib/purchase-catalog.js`, so the server can quote them today. They read
`UNAVAILABLE` purely because of the client-side shortfall gate. **The fix is to remove the gate, NOT
to add anchors, NOT to touch pricing.**

- Update the `theme` authoring note on all three so it no longer asserts the retired rule - a note
  that contradicts shipped behaviour is how this repo's most expensive bugs start.
- Keep the single-resource guarantee intact (WO-947 s12c): each grants exactly ONE economy key and
  nothing else. That guardrail is NOT overturned.
- ⚠ Both canonical JSON twins must stay byte-identical (`Resources/` wins at load).

## The other defects from the same screenshot

- **"CLOSE THE GAP" overlaps the "MONTHLY LEDGER" button**, hiding about half its label.
- The top price row (`993 SKR ~$19.99` / `2481 SKR ~$49.99`) is clipped by the scroll region.
- (The top-right wallet chip is WO-1334's lane - do not touch it here.)

## Constraints

- ⛔ **Do NOT touch prices, SKUs, entitlements, grants, or `purchase-catalog.js`.** The game takes
  real money on mainnet and `/verify` runs AFTER settlement. This ticket changes SURFACING only.
- The owner is **red/green colourblind**: the card must read by shape and word, never by hue.
- Phone-first landscape; touch targets **>= 112px**.
- ASCII-only in player-facing strings.
- Code-built uGUI via `ElarionUiKit`. **UXML does not work in builds.**

## Acceptance

- [ ] A smaller Night Market card is permanently anchored on the LEFT of the town HUD, not
      overlapping the movement stick or the hero block.
- [ ] It opens the SAME `PackStore` screen as the walk-up building.
- [ ] The three gap packs are buyable with no shortfall present, and their authoring notes no longer
      assert the retired gate.
- [ ] No pricing, SKU or entitlement file was modified. Say so explicitly in the RESULT.
- [ ] An oracle pins both: the card's presence, and that the three SKUs are offerable without a
      shortfall. Prove it RED first and report the mutation.
- [ ] `COMPILE_GATE_OK` + `REGRESSION_OK <n>/<n> suites` on FRESH logs, markers asserted.
- [ ] A device screenshot a human opened.
- [ ] PO felt-verifies and closes.
