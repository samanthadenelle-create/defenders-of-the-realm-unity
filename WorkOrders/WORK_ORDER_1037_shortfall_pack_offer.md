# WORK ORDER 1037 — Turn "Missing resources" into a pack offer (STUBBED, flag-gated off prod)

**Status:** FIXED — AWAITING OWNER FELT-TEST TO CLOSE. Prior status: DONE — audit-verified as shipped (2026-08-21 backlog audit).

> ⚠ THE OLD STATUS LINE WAS STALE BY A DAY. It read *"needs owner ruling on §3 (no pack can currently
> fulfil this offer)"* — but §3 already carries `⛔ RULED 2026-08-16 — OPTION (b)` with the owner's
> verbatim words (*"we should have small instant packs"* · *"small wood only"*). The blocker it named
> was spent; the ticket has been implementable since. Caught in the 2026-08-17 staleness sweep.
>
> This is the §2 failure mode in its purest form: the ruling was written INTO the body and the STATUS
> LINE was never flipped — and the board is DERIVED from the status line, so the whole project read
> this as blocked-on-the-owner for a day while it was actually ready to build.
>
> **Remaining dependency (not a blocker on this WO):** option (b) means real money now buys the
> REGULAR basket (wood/iron), which WO-947 was written on the assumption it never would. That needs the
> **WO-947 §12 amendment** (drafted 2026-08-17) — see `WORK_ORDER_947_cost_basket_separation_regular_vs_arcane.md`.
> The amendment does NOT change any structure's cost basket and does not reopen WO-947.
**Minted:** 2026-08-16 (UI seat) — provenance stack bumped 1037 → 1038 in the same edit
**Lane:** Building upgrade panel + PackStore surface. ⚠ Monetization-adjacent — read §2 before coding.
**Provenance:** owner 2026-08-16 — *"could we use this opportunity to suggest a pack that would allow
them to build?"*, with the Lumber Mill Enhancements screenshot (`900 Wood - need 880 more`,
`Missing resources`). Rulings same day: *"we can add a flag against Prod push till monetization is
activated"* and *"but for now stubbed"*.

---

## 1. The design — why this moment is the right one

The player has opened an upgrade, read the perks (`Wood production +10%`, `Structure HP +20%`), decided
they want it, and hit a wall: **880 wood short**. That is the highest-intent moment the economy
produces. Clash of Clans monetises exactly this beat, and it works because the offer is **informative,
not interruptive** — it answers a question the player is already asking ("how do I get past this?")
rather than injecting one.

**Design guardrails (these are what keep it from reading predatory):**

- **It appears only on a genuine shortfall** — never on an affordable upgrade
- **It is an offer, not a gate.** The existing path (go harvest) stays first-class and visible. The
  pack is an *alternative*, never the recommended route
- **It states exactly what it closes** — "this pack covers the 880 wood you need", not a generic
  storefront dump. A relevant offer is respectful; an irrelevant one is a billboard
- **One offer, the smallest sufficient one.** ⚠ Do not upsell to a larger tier at the shortfall moment;
  that is the turn from helpful to extractive. Owner pricing intent is already **cheap: $2 and $5
  tiers, $5 max** (memory `solana-store-early-access-pack-pricing`)
- **Dismissible, and it stays dismissed** for that upgrade in that session

## 2. ⛔ THE WO-931 GUARDRAIL — a stub here is NOT a free-grant surface

⚠ **Read `WO-931` before writing a line of this.** Canon (anchor 2026-08-09):

> **"STORE PURCHASES ARE RE-GATED OFF AND LOCKED"** (`576601e3`). `StubWalletProvider` has **no
> `#if UNITY_EDITOR`/`DEVELOPMENT_BUILD` guard**, ships in every player, fabricates a wallet + a
> **2000 SKR mock balance** + a base58 signature, and `ApplyPackContents` **grants the pack for ZERO
> payment** while firing `purchase_completed` with the fake txSig. **The submitted store build had a
> tappable Buy button.**

**So "stubbed" must mean the SURFACE is stubbed, not the PURCHASE.** Concretely:

- The offer may **display** and may **route to the pack detail**
- ⛔ It must **NOT** reach `ApplyPackContents`, and must not present a tappable Buy that grants anything
- ⛔ It must **NOT** flip `FeatureFlags.RealmStorePurchase` — that has **3 preconditions** in WO-931 and
  this ticket satisfies none of them
- **The owner's flag ruling is the mechanism:** a feature flag, **default OFF**, that also **blocks the
  prod push path** until monetization is activated. ⚠ Model it on the WO-931 lesson — the failure there
  was a stub with **no build-configuration guard** that therefore shipped. A flag that only defaults off
  is not enough; it must be *unable* to ship enabled.

## 3. ⛔ OWNER RULING REQUIRED — NO PACK CAN FULFIL THIS OFFER TODAY

Measured from `Assets/Resources/Data/Canonical/packs.json` (13 packs), 2026-08-16:

```
ALL economy keys across all 13 packs: ['coins', 'crystals', 'food', 'glimmer']
any wood? False    any iron? False
```

**Not one pack grants wood or iron.** The screenshot's shortfall is **880 wood**. So the feature as
asked — *"suggest a pack that would allow them to build"* — **cannot be fulfilled by any existing
pack**, for the most common upgrade class in the game.

⚠ **This may be deliberate, and that is why it needs your ruling.** WO-947 separates the baskets:
**regular structures = wood + iron; magical = crystal-based.** Packs grant crystals, food, coins and
glimmer — i.e. money currently buys the **magical** economy and **not** the regular build economy. That
may be an intentional line: *money cannot buy the town's core material loop.*

| option | consequence |
|---|---|
| **(a) Keep the line — no wood/iron in packs** | The offer only ever appears for **crystal-costed** (magical) upgrades. Honest, narrow, preserves WO-947. ⚠ It will **not** appear on the Lumber Mill screenshot that prompted this |
| **(b) Add wood/iron to packs** | The offer works everywhere, ⚠ but money now buys the regular basket — a real change to the game's economic character, and it needs a WO-947 amendment |
| **(c) Offer a convenience instead** | Packs already carry `instant-build` and `harvest-auto-collect`. Suggest *speed*, not *materials* — keeps the line **and** gives the moment something true to say |

### ⛔ RULED 2026-08-16 — OPTION (b). Single-resource impulse packs.

> Owner, verbatim: **"we should have small instant packs"** · **"small wood only"** · **"or medium
> wood"** · **"or large wood"** · **"same with all types for impulse small purchases"**

**The line in §3 is deliberately crossed. Money may buy the regular basket.** My (c) recommendation is
withdrawn — the owner has ruled and this ticket proceeds on (b).

#### The new pack family: `<resource> × {small, medium, large}`

A **single-resource, single-purpose** SKU. Not a bundle: no cosmetics, no convenience riders, no
mixed economy block. The player is 880 wood short; they buy wood.

| axis | values |
|---|---|
| **resource** | one SKU family per harvestable type — **Wood, Iron, Food, Crystals** (+ any other type the build economy actually consumes) |
| **size** | **small / medium / large** |
| **contents** | ⛔ **exactly ONE economy key.** A "wood pack" grants wood and nothing else |
| **price** | impulse tier. ⚠ Owner's standing pricing ruling: **cheap — $2 and $5 tiers, $5 max** (memory `solana-store-early-access-pack-pricing`). Small must feel impulse-priced, large must stay under the ceiling |

⚠ **Size the tiers against REAL upgrade costs, not round numbers.** The screenshot's shortfall is 880
wood on a **Tier 1 of 4** building — the cheapest upgrade in the game. Read actual costs out of the
structures catalog and pick amounts so that **small** meaningfully closes an early shortfall and
**large** is not absurd at tier 4. A pack that cannot close the gap it is offered against is worse than
no pack.

#### ⚠ THIS AMENDS WO-947 — record it or canon self-contradicts

WO-947's cost-basket ruling (regular = wood + iron, magical = crystal) was written on the assumption
that the **regular basket is earned, not bought**. Selling wood changes that. **Add a note to WO-947
and to the canon anchor** stating the amendment and its date, so the next seat does not read the
basket rule as forbidding these SKUs and "fix" them away.

**This is the §15 rule:** a state change with no canon update is an incomplete change.

#### Consequences to handle

- **The §3 blocker is resolved** — the shortfall offer can now name a real, fulfilling SKU. Match the
  offer to the **shortfall's resource**, and to the **smallest sufficient size** (§1 guardrail: one
  offer, smallest sufficient, no upsell at the shortfall moment).
- ⚠ **Stockpile caps interact** (memory `stockpiles-cap-capacity`): lumberyard/foundry/silo cap
  capacity. **Decide what a pack does when it would overflow the cap** — reject, partial-fill, or
  temporarily exceed. Selling a resource the player cannot receive is a refund request.
- ⛔ **All of §2 still applies.** These SKUs are **display-only** until WO-931's three preconditions are
  met. Adding purchasable-looking single-resource packs to a build where `ApplyPackContents` still
  grants for zero payment is precisely the WO-931 defect, at greater volume.

## 3b. Rewarded ad at the shortfall — owner 2026-08-16: *"maybe even watch an ad for free 500 wood?"*

**Design-wise this is the right partner to §3.** The shortfall moment wants two doors: a paid one and a
free one. Offering only the paid door at the exact instant a player is blocked is what makes a game
feel extractive; offering *"watch 30s"* beside *"buy small wood"* makes the same moment feel generous,
and it monetises the players who will never spend. It also gives the §1 guardrail — *the harvest path
stays first-class* — a middle rung.

### ⚠ DO NOT BUILD THIS HERE — it is WO-912, and the seam ALREADY SHIPPED

`WORK_ORDER_912_ad_revenue_free_path.md` — **"Ad revenue for the FREE PATH (provider, rolling window,
remote config, ad-boost packs)"**, status **PARTIAL — BLOCKED ON D3** (reconciled 2026-08-08):

- **The ad seam is BUILT** — `IAdService.cs`, markers `AD_SEAM_OK` + `AD_COVENANT_OK`
- **Provider is RULED** — Unity **LevelPlay**, settled by eligibility (D2); D1/D4 also ruled
- ⛔ **NO IronSource/LevelPlay SDK exists under `Assets/`** — only skill docs. **D3 is the sole hard
  blocker.**

So *"watch an ad for 500 wood"* is **a new consumer of an existing seam**, not a new system. **Do not
mint a second ad ticket and do not re-decide the provider** — that work is done and the rulings are in.

**What belongs in THIS ticket:** the shortfall panel presents the free-path option **through
`IAdService`**, gated exactly like §2's paid path, and it is **inert until WO-912's D3 lands**. What
belongs in WO-912: the SDK, the rolling window, and the reward economy.

⚠ **The rolling window is WO-912's, not this panel's.** *"500 wood per ad"* must be rate-limited
somewhere, and that somewhere already exists in 912's scope. A per-panel cap invented here would be a
second, competing limiter — the duplicate-authority failure this project keeps hitting.

### ⛔ LEGAL FLAG — the LIVE privacy policy currently claims NO ADS

Canon (anchor 2026-08-09) records `PRIVACY_POLICY.md:87-89` as carrying one false sentence on a **live,
published page**, and states plainly that **"the core no-ads claim is TRUE"** — the page is live at the
hosted policy URL.

**Shipping a rewarded ad makes that live page false.** ⚠ The same canon line says: **"do not edit it,
live legal copy is the owner's/attorney's call."**

- [ ] ⛔ **Owner/attorney must update the published privacy policy BEFORE any ad ships** — this is a
      release gate, not a nicety
- [ ] The `AD_COVENANT_OK` marker (already in the seam) is the mechanical guard; confirm what covenant
      it actually asserts and that it still holds with a rewarded placement

**Do not treat this as blocking the WO.** Build the surface stubbed per §2; the legal update gates the
*activation*, alongside D3.

## 4. Bonus defects visible in the same screenshot (small, same panel — fold in)

- **`UPGRADE CO...` is truncated.** A label ellipsized mid-word. ⚠ Canon forbids ellipsis in
  currency/cost grammar (Grok-02 §4: *"CompactNumber + CurrencyChip — no ellipsis"*). Give the band its
  full line box.
- **`Missing resources` reads as an empty black square.** The affordability tell is a colour/fill
  signal with no shape or text carrying it. ⚠ Colourblind law — and canon already lists build
  affordability as an **open colour-only defect**. It should read in greyscale.

## 5. Acceptance criteria

- [ ] On a genuine shortfall the panel surfaces **one** relevant offer per §3's ruling
- [ ] It **never** appears when the upgrade is affordable
- [ ] The harvest path stays first-class and visually primary
- [ ] ⛔ **No purchase can complete** — `ApplyPackContents` unreachable from this surface;
      `RealmStorePurchase` untouched; **verify by attempting it**
- [ ] The gating flag defaults OFF **and blocks the prod push path** — prove a prod build cannot ship it
      enabled (§2; a default-off flag alone is what failed in WO-931)
- [ ] `UPGRADE CO...` no longer truncates
- [ ] `Missing resources` legible in **greyscale**
- [ ] Dismissible; stays dismissed for that upgrade that session

## 6. Verify

1. `COMPILE_GATE_OK` + `REGRESSION_OK <n>/<n> suites`
2. A regression asserting the offer surface **cannot** reach a grant path — this is the WO-931 class and
   deserves an oracle, not a code review
3. `UI_CAPTURE_OK` — **open the PNGs**, shortfall and affordable states, plus a greyscale pass
4. Owner felt-verifies: *"does this help me, or is it selling at me?"* — the only test that matters here

> **AUDIT 2026-08-21 (agent fleet, read-only):** FIXED. Evidence: `ShortfallPackOffer.cs:92; BuildingUpgradeVM.cs:427` — shortfall resolver wired. Status was flipped from READY by the AUDIT, not by an implementation pass. The body below is left intact; if this call is wrong, the evidence cited here is what to challenge.
