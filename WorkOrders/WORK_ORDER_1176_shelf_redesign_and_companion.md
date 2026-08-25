# WORK ORDER 1176 — The shelf redesign: a one-time starter, choose-your-resource, discounts, and a companion worth $50

**Status:** SPEC — design is largely settled (2026-08-24) but three things stop it being implementable: **§4c leaves companion IDENTITY owner-open**; the header claims a SCHEMA CHANGE while §3 says use the existing `purchase_entitlements` (pick one); and the discount scope OVERLAPS WO-1177, which is already assigned. ⛔ Still sequenced AFTER WO-1173 (schema-parity gate). *(Status audit 2026-08-24: lead-verified bucket correction; body unchanged.)*

**Minted:** 2026-08-24 (CLI), banner bumped 1176 → 1177 in the same edit.
**Provenance:** a single design conversation with the owner, 2026-08-24, immediately after the first
real mainnet purchase settled. Four asks that turn out to be one problem — quoted at each section.

---

## 0. WHY ONE TICKET

These do not stand alone:

- The **companion** (§4) is what makes a $50 tier honest — but only if a $50 tier is worth buying.
- The **one-time starter** (§2) is what stops the cheapest pack flattening the whole ladder.
- **Purchase limits** (§3) are what make "one-time" mean anything — and they **do not exist**.
- **Discounts** (§5) are how contests, goodwill and upsells are run, and today there is no way to
  make a player whole at all.

Split them and each ships into a shelf the others have not fixed yet.

---

## 1. ⛔ THE FINDING UNDER ALL OF IT: nothing limits any purchase

`founderOnly` is **decoration**. It is parsed into `PackCatalog.FounderOnly`, passed once as an
**analytics label** in a `bundle_viewed` event, and read by nothing else. There is no purchase cap
client-side and none server-side.

**So `starters-hand` is not one-time. Nothing is.** Every pack is infinitely repeatable.

⚠ **This is the FIFTH complete-but-unwired mechanism found on 2026-08-24** —
`WalletService.Disconnect`, `PublishWalletDisconnected`, `WalletConnectDialog.SetWalletService`,
`PackStore.SetWalletService`, and now this. The shared failure mode: **nothing fails loudly.** A flag
nobody reads looks exactly like a flag that works.

## 2. The value ladder, and why the bottom rung breaks it

Computed from `packs.json`, not quoted from an earlier review:

| SKU | USD | goods | per $ |
|---|---|---|---|
| hearth-spark | 1.99 | 3,050 | 1,533 |
| starters-hand | 4.99 | 8,500 | 1,703 |
| folks-thanks | 9.99 | 19,200 | 1,922 |
| patron-of-elarion | 19.99 | 39,350 | **1,968** |
| founders-vow | 49.99 | 98,100 | **1,962** ⛔ inverts |

**And `hearth-spark` is repeatable**, giving `1,500 wood + 800 iron + 150 crystals + 500 food +
100 coins` for $1.99. That single fact dominates every single-resource impulse pack **permanently**:

```
impulse-wood-small   $1.99 -> 1,000 wood          (503 wood/$)
hearth-spark         $1.99 -> 1,500 wood + 4 more (754 wood/$ + extras)
```

⛔ **A repeatable everything-basket at the BOTTOM of a ladder flattens the ladder.** It out-values
several rungs above it and every impulse pack below it.

**RULED (owner):** *"make hearth spark one time only."*

## 3. Purchase limits — server-side, or they are not limits

⛔ **A client-side limit is a suggestion.** The server already refuses to price a SKU it will not
sell (`walletAllowed`), so the cap belongs in that same gate, checked against
`purchase_entitlements` — which already records every settled purchase per wallet. **No new table.**

- Author `purchaseLimit` (or `oneTimeOnly`) in `packs.json`, mirrored into the server's catalog under
  the existing **MIRROR LAW** (`USD_ANCHORS` already works this way and a test enforces it).
- `/api/purchases/quote` refuses a SKU the wallet has already bought to its limit, with a **worded**
  reason — the store fails closed and shows words, never a dead button.
- The client may *also* grey the card, but only as presentation. The refusal is the server's.

⚠ **A per-wallet limit means losing the wallet loses the one-time purchases.** That is the same
no-restore-path problem the existing "wallet required above $4.99" rule already reasons about — make
it a deliberate decision, not a discovered consequence.

## 4. ⭐ THE COMPANION — the product the shelf is actually missing

**Owner:** *"can we add a companion that cosmetically traverses the world with them (cosmetic no
value)"* · *"we pull the one if purchased"* · *"use the existing pet leash"* · *"the companion
selections live in cdn"*.

### Why it answers the $50 question

The owner's own framing: *"if they are spending $50 they are expecting items that are worth at least
50 right?"* Resources cannot answer that. Wood/iron/food are **capped** (2,000 base, 34,000 ceiling),
the faucet clears every one-time sink in ~4 hours, and **overflow above cap is DISCARDED** — while
paid grants BYPASS the cap, so a large purchase parks the player above the ceiling and their own
production is thrown away for hours. **98,100 units of something the game gives you in an afternoon
is not worth $50, however the ladder is curved.**

A companion is **permanent, uncapped, never discarded, on screen constantly, and squarely inside the
covenant** — *"convenience and BEAUTY, never combat power."* WO-1165 §3 asked for exactly this: a
product immune to the cap.

⭐ It also unblocks real backlog: **9 of 13 non-incidental SKUs are hidden for one reason — cosmetics
do not render.** A companion is a cosmetic impossible to miss.

### It is assembly, not invention

| Piece | State |
|---|---|
| Follow behaviour | ✅ `Pet.cs` **Idle mode** — *"Follows the hero around the village; does not fight"*, walkable-surface pathing, height-follows stairs/ramparts |
| Remote art on the CDN | ✅ Addressables → R2, same as structures/enemies |
| On-demand pull, cache, offline fallback | ✅ `OfflineContentService` (PROD-010) — bulk today, one key is the narrower case |
| Ownership | ✅ `purchase_entitlements` + `CosmeticOwnershipService` |
| Applying appearance | ✅ `CosmeticApplier` — **the one appearance owner** |
| Companion catalog + pull-the-one-you-own | ⬜ **new** |
| Telling the player a paid asset did not resolve | ⬜ **new, and §4b is why it is not optional** |

### 4a. ⛔ ONE APPEARANCE OWNER — the rule this will trip

CLAUDE.md §7: `EchoWorldPresence` is **the one appearance owner** for an Echo — one owner, one
lifecycle, **no second spawner**, pinned by `EchoWorldPresenceRegression`. A companion is another
world-following body, so it routes through the existing owner or clearly extends it. **A parallel
spawner is the natural mistake here**, and it is the exact one §7 exists to forbid.

### 4b. ⛔ THE CDN RISK IS DIFFERENT WHEN THE ASSET IS PAID FOR

§16's failure mode: *"a build whose bundles were never uploaded installs perfectly, launches
perfectly, and plays… no error on screen."* For enemies that is capsules — embarrassing and free.

**For a companion it is: the player paid real money and sees nothing.** On a rail with **no refund
path**.

⚠ So this line needs something enemies do not: **the game must KNOW a purchased cosmetic failed to
resolve and SAY SO** — "your companion is downloading" / "couldn't fetch it, retrying" — never a
silent placeholder. The entitlement is durable server-side, so the purchase is never lost and retry
is idempotent; only the asset is missing. **That distinction must reach the player, or they conclude
they were charged for nothing.**

⚠ And the standing §16 rule applies unchanged: **bundle names are content-hashed, so every content
build needs its own push.** `tools/r2-ship.ps1` stays the one path.

### 4c. ⚠ OPEN, AND IT IS THE OWNER'S: what IS a companion?

Thematically it must read as **clearly not an Echo**. Both are creatures that accompany you; if they
look like the same category, players will reasonably expect the companion to *do* something — and
the moment they suspect it affects harvest or combat, an inert cosmetic has created a pay-to-win
*perception* problem. Distinguishing them in silhouette and copy is cheap now and expensive later.

## 5. Choose-your-resource, and discounts

### 5a. One card, one picker

**Owner:** *"can we add choose iron stone or wood and get x amount"* · *"maybe 1000 wood / or 800
stone / or 600 Iron"*.

**1000 : 800 : 600** is a legible scarcity ladder — wood abundant, stone middling, iron scarce — and
it fixes an authored oddity: today at $1.99 wood gives **1,000** and iron **400**, a 2.5× penalty
with nothing explaining it.

⭐ It also collapses **12 impulse SKUs into 3 cards with a picker**, which is the "very little
options" the owner asked for. ⚠ Simplest implementation is **presentation over the existing SKUs** —
the picker selects which existing sku is quoted. **No schema change, no new failure mode on the money
path**, which matters given §4b and today.

### 5b. Discounts — one mechanism, three uses

**Owner:** *"i need codes to be able to dispense coupons for like 20% off or free gift for contest
winner"* · *"even good will retention stuff"* · *"or upsell option do you want to add wood for x
discount?"*

✅ **Free gift / contest prize / goodwill ALREADY WORK.** `promo_codes` carries `reward_pack_sku`
(a whole pack's contents), `bound_wallet` (only that wallet may redeem), `max_redemptions`,
`expires_at`, and an `active` kill-switch; the store already has **"Redeem a Code"**.
⭐ **And that is today's only way to make someone whole** — an SPL transfer cannot be reversed, but
goods can be granted, with an audit row in `promo_redemptions`.

⬜ **Percentage discounts do NOT exist.** A discount is not a grant: it must reduce the **price**
before the token amount is derived.

⛔ **Server-side, always.** The discount modifies the USD anchor inside `buildQuoteBody`, the
discounted figure is persisted on the quote row, and `/verify` re-derives from that row. **The client
never computes a discount** — the same principle that made the first real purchase safe.

### 5c. Upsell: WHERE, not whether

`ShortfallPackOffer` encodes a deliberate rule: *"the SMALLEST SUFFICIENT size. **No upsell at the
shortfall moment.**"*

- **On the shelf** an upsell is ordinary commerce — the player is browsing and can weigh it.
- ⛔ **At the shortfall moment it is the opposite**: the player is blocked, wants it now, and is least
  able to evaluate. Highest conversion, worst defence. WO-1165 already called the current
  dominated-pack offer *"the hardest finding to defend publicly."*

**Ruling: allow upsell on the shelf; keep the shortfall moment clean.**

## 6. Order of work

1. **Purchase limits, server-side** (§3) — nothing else means anything without them.
2. **Hearth Spark → one-time** (§2). Ladder shape restored.
3. **Choose-your-resource as presentation** (§5a) — no schema change.
4. **Discount on the quote** (§5b) — unlocks coupons, goodwill, shelf upsell.
5. ⏸ **Companion** (§4) — biggest, and gated on §4c (what it *is*) plus §4b (failure messaging).
6. Revisit the `founders-vow` inversion (§2) once the companion gives the top rung something to be.

## 7. Acceptance

- [ ] A one-time SKU is refused by the **SERVER** on a second attempt, with a worded reason
- [ ] Buying it once, then reinstalling, still refuses — the limit is per wallet, not per device
- [ ] A 20% code produces a quote 20% lower, and `/verify` accepts **only** that amount
- [ ] A discount cannot be requested by the client — asserted by a test
- [ ] The resource picker quotes the correct existing SKU for each choice
- [ ] Companion: owned → pulled → follows; **NOT owned → never spawns**
- [ ] ⛔ Companion asset fails to download → the player is TOLD, retry succeeds, entitlement intact
- [ ] `EchoWorldPresenceRegression` still green — no second spawner
