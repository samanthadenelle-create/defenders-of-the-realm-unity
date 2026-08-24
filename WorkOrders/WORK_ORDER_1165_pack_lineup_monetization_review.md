# WORK ORDER 1165 — The pack lineup: a covenant collision, two unbuyable SKUs, and an inverted top rung

**Status:** READY — §1 needs an OWNER RULING before WO-1163 lands. The rest is implementable.

**Minted:** 2026-08-23 (CLI), banner bumped 1165 → 1166 in the same edit.
**Source:** monetization SME review requested by the owner. **CLI-verified at source** before filing — verdicts marked CONFIRMED are re-read by the CLI, not taken on the reviewer's word.

---

## 1. ⛔ P0 — WO-1163 TURNS EVERY PAID PACK INTO A COMBAT-POWER SALE. Rule this before it lands.

**CONFIRMED at source.** Three facts that are individually fine and jointly a problem:

1. **Gold IS Coins.** `DevPanelController.cs:1235` — *"Gold — the shop/sell wallet
   (`GameState.Resources.Coins`)"*; `BuildingTierCatalog.cs:39` — *"Gold (economy Coins) cost"*.
2. **WO-1163 rules troop training PURE GOLD** (owner, 2026-08-23: *"troops go to pure gold sink"* ·
   *"in the barracks"*).
3. **Every paid pack grants coins.** `packs.json` says it in its own authoring note —
   *"starters-hand: 4000 wood / 2000 iron / 400 crystals / 1500 food / **600 coins**"*. The reviewer
   tabulated the ladder: hearth-spark 100 · starters-hand 600 · folks-thanks 1,400 ·
   patron-of-elarion 2,800 · **founders-vow 7,000**. Coins also appear in **both** battle-pass lanes
   and all 60 monthly-ledger days.

**Against a covenant that says, verbatim in the same file: "never combat power."** And
`battle_monthly.json:3` makes ZERO COMBAT POWER a **build gate**.

⛔ **So the moment troop training is gold-priced, $49.99 buys ~17 echo-legionnaires or ~140
footmen, and `BattleMonthlyRegression`'s firewall is violated by data it currently passes.** Nobody
noticed because coins are inert today — the economy ruling is what creates the breach.

### ⭐ RULED 2026-08-24 — option (c), on a principled distinction: GOLD BUYS TEMPO, NOT POWER

**Owner, verbatim:** *"The video just allows queuing troops for a raid, not a tactical edge since
gold is easy and enemies give generously"* · *"Still time constraint to train, battle stats and
skill to win"*.

**The covenant line is redrawn, not abandoned.** "Never combat power" means never buying *capability*
— stats, gear tiers, damage. Buying **gold** buys the ability to QUEUE sooner, and the binding
constraints on actually winning are untouched: **training time, battle stats, and player skill.**
Gold is also abundantly earnable from kills, so a purchase compresses a schedule rather than opening
a door that was closed.

### ⚠ THE ONE DEPENDENCY THIS RULING RESTS ON — verified, and it is narrower than it looks

The ruling's premise is *"still time constraint to train"*. That premise is **partially exposed**:
`CanWatchAdToSkip(ChannelId channel, …)` is **WO-911 ad-skip on ANY channel** — Builder, Train AND
Research — so a rewarded ad *does* skip training time.

**It survives on the CAPS, so the caps are now load-bearing covenant infrastructure:**

| Bound | Value |
|---|---|
| `place.build.skip` dailyCap | **3 / day** |
| Skip per watch | 600s |
| **Max training time skippable per day** | **30 minutes** |
| Cooldown | 480s · `hardDailyCap` 7 across all placements |
| Scope | **RUNNING JOBS ONLY** — accelerates a job already training, cannot conjure one |

`ad-placements.json` already reasoned its way here: *"the retention-first 2026-08-21 pass cut build
skips from 10 to 3 per four-hour window so ads help a session **without becoming the progression
loop**."*

⛔ **SO: 30 minutes/day is TEMPO. The training constraint is dented, not deleted, and the ruling
holds.** But it holds *because of a number*. **Raising `dailyCap` on `place.build.skip` weakens this
covenant ruling in direct proportion** — that is not a balance knob any more, it is the thing the
"never combat power" claim now rests on. Anyone changing it is re-opening an owner ruling.

⚠ **Ads are the SECOND door into this, not the first.** `reward.daily.chest` grants **+500 coins**
and `reward.coins.small` **+250** — so ads pay gold directly, as well as purchases. Both paths are
covered by the ruling above; both are bounded by the same caps.

## 2. ⭐ HIGHEST-VALUE, LOWEST-EFFORT: two authored SKUs are unbuyable

`battle_monthly.json:876-886` (`monthly-wayfarer`, $4.99) and `:1331-1341` (`monthly-keeper`,
$9.99) are **fully authored — 30 days of grants each** — and **absent from `USD_ANCHORS`**
(`api/_lib/purchase-catalog.js:69-95`). So `usdAnchor()` returns null → `buildQuoteBody()` returns
null → **no quote → unbuyable on the live rail.**

**Sixty authored reward days, zero revenue path, blocked by two missing lines.**

⭐ And they are the only product shape that survives §3: a ledger **drips below the cap over 30
sessions** instead of dumping above it once. Recurring revenue today is **$0** — there is no
repeat-purchase loop in the entire lineup.

## 3. The store sells the one thing that has no scarcity

Wood / iron / food are capped (2,000 base, 34,000 ceiling) against a faucet that clears every
one-time sink in ~4 hours, and **overflow above cap is DISCARDED** (`EconomyService.cs:463-466`).

⛔ **And paid grants BYPASS the cap** (`GrantSpendablePurchased`), so a purchase parks the player
above the ceiling — **and then their own production ticks are discarded for hours.** Buying a
resource pack near cap makes your own income worth zero. That is the shape of a refund ticket, and
no card discloses it.

**Crystals are the only currency that holds value** (uncapped, gates rare+ gear) — and they are the
one family NOT promoted to the shelf.

**The lineup cannot be fixed by re-pricing.** It needs a product immune to the cap: permanent
storage capacity, a permanent workforce/builder slot, or crystals. All three are covenant-legal.
⭐ **A permanent storage upgrade is the single best missing SKU** — it sells the fix to the problem
the resource packs currently make worse.

## 4. The ladder INVERTS at the top rung

| SKU | usd | total goods | goods/$ |
|---|---|---|---|
| hearth-spark | 1.99 | 3,050 | 1,533 |
| starters-hand | 4.99 | 8,500 | 1,703 |
| folks-thanks | 9.99 | 19,200 | 1,922 |
| patron-of-elarion | 19.99 | 39,350 | **1,968** |
| **founders-vow** | **49.99** | 98,100 | **1,962** ⛔ |

**The $49.99 is worse value per dollar than the $19.99.** The marginal $10.01 above two Patrons buys
1,938/$ — below every rung above $4.99. **A negative volume bonus at the price point where whales
self-identify.** The informed $50 play is 2× Patron + 1× Starter's Hand.

Its intended differentiator was never goods — it was cosmetic + banner + naming, **all unauthored**
(`cosmetics: []`). And `BEST VALUE` currently sits on the rung *below* it, telling the highest-intent
buyer to spend less.

## 5. Crystals mispriced ~3.5×, and the good SKU is hidden

`4 × impulse-crystals-large = $19.96 → 6,400 crystals` vs `patron-of-elarion $19.99 → 1,850`.
**Same money, 3.46× the only currency that matters** — and `impulse-crystals-*` is the one impulse
family not on the shelf (shortfall-only). Best-value, uncapped, highest-utility product: invisible.

## 6. The shortfall offer serves a strictly dominated pack at peak intent

`ShortfallPackOffer` stops at the first rung covering the gap, so a 900-wood shortfall offers
`impulse-wood-small` — **1,000 wood for $1.99**, when `hearth-spark` at the **same $1.99** gives
1,500 wood + 800 iron + 150 crystals + 500 food + 100 coins. **Strictly dominated.**

⚠ `packs.json:562` **already knows** ("the small rung is strictly dominated by Hearth Spark at the
same $1.99") and hid it from the shelf — but the shortfall resolver still serves it **at the moment
the player is most motivated.** A value trap by construction, and the hardest finding here to defend
publicly.

## 7. Food → Stone: the copy dies, and three SKUs get weaker

The slot rename is right, but **the copy is 100% grain fiction**: *"Basket of Grain… the Folk eat
tonight"*, *"Grain Cart"*, *"Harvest Wagon… the season's yield"*. **`impulse-food-medium` is LIVE ON
THE SHELF NOW** — at rename it becomes a card selling grain and delivering stone. Rename copy in the
SAME change.

⚠ **And the value proposition genuinely weakens.** Food's real sink was troops (~122k, re-spent
every raid). That migrates to **gold**. Stone inherits only L2 building tiers — one-time,
non-repeatable. Post-rename those three are the weakest SKUs in the store **unless the siege/rebuild
drain lands first**.

## 8. Named weak / redundant SKUs

`impulse-wood-small` + `impulse-iron-small` (dominated — delete or re-price) · `keepers-satchel` (do
NOT unhide: 180 crystals/$ vs 321 at the same price) · `founders-vow` (§4) · `frostfall-bundle` and
`embergrove-bundle` (**identical contents, identical price** — two SKUs, one product) ·
`bloomtide-bundle` (third clone) · `echo-patron-pack` + `builders-cache` (**every convenience token
they sell has no redeemer** — one `storeVisible` flip from selling nothing).

**Live store = 4 baskets + 3 impulse rows. Everything else is hidden vapor.**

## 9. ⛔ Copy that over-promises

1. **`packs.json:188` — "Founders are named on the Heart."** A permanent forever-promise on the
   $49.99 SKU with **no implementation anywhere in the codebase**. It replaced a `LAUNCH ONLY`
   FOMO line — good instinct, but it swapped manufactured scarcity for something **undeliverable**.
   **Highest-risk sentence in the store. Remove it or build it.**
2. `BEST VALUE` on patron-of-elarion — defensible only on a metric that prices 1 wood = 1 crystal.
3. Grain copy on soon-to-be-stone SKUs (§7).
4. "Resources auto-tick 24 hours" / "Skip the build animation" / "2× XP buffs" — **none do anything.**
   Hidden, correctly. Flag loudly for whoever flips `storeVisible`.

## 10. Selling in a volatile token — presentation only (pricing policy is CLOSED)

The ruled policy costs ~6% over spot before `ceil()`. **The exposure is not the 6% — it is that a
player can CHECK it** in ten seconds against a public token, with no App Store receipt to arbitrate,
no refund path on an SPL transfer, and a 1-of-1 treasury behind any goodwill send.

**Fixes that touch no pricing:** make **SKR the price and USD the reference** · show the rate,
its source and the quote timer on the confirm sheet (already recorded in `buildQuoteBody`, just not
surfaced) · **say "24h low" in the UI**, converting a hidden markup into a stated policy · receipts
in SKR + signature + rate + timestamp · surface the 5-minute quote lock, which is consumer-favourable
and currently invisible.

## 11. Order of work

1. **Rule §1** — the coins/covenant collision. Blocks WO-1163.
2. Add the two Monthly Ledger SKUs to `USD_ANCHORS` (§2) — smallest change, largest revenue.
3. Delete/re-price the dominated small rungs; make the shortfall resolver compare against shelf SKUs (§6).
4. Remove or implement *"Founders are named on the Heart."* (§9.1).
5. Fix or retire the $49.99 rung (§4).
6. Promote a crystals row; re-examine `BEST VALUE` (§5).
7. Design the permanent storage / workforce SKU (§3).
8. Rename grain copy in the same commit as the food→stone rename (§7).
9. SKR-primary card presentation (§10).
