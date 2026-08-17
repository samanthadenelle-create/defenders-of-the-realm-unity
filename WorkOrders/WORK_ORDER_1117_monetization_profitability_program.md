# WO-1117 — Monetization profitability program (post-dApp-Store live)

> ## ⛔ OWNER RULING 2026-08-17 — BLOCKED BEHIND THE PROD TICKETS. DO NOT START ANY PHASE.
> Verbatim: ***"All I am saying is that these are suggestions, I want you to flush out the ideas,
> and only apply the solid parts of Grok's strategy after we close out prod tickets."***
>
> Three constraints, and all three bind every phase (1118–1122):
> 1. **SUGGESTIONS, NOT A PLAN.** This program is Grok-authored input. Per the three-seat flow it is
>    refined, never accepted verbatim — and at least one premise has already failed verification
>    (see the ADS correction below). Treat every claim here as unproven until read at source.
> 2. **ONLY THE SOLID PARTS SHIP.** Each phase must state what was VERIFIED against the repo and
>    what was dropped, before any code.
> 3. **PROD-001…004 CLOSE FIRST.** Those are live defects on a shipped build; this is revenue
>    optimisation on the same build. A defect the player is living with outranks a SKU we would
>    like to sell them.
>
> ### VERIFIED AT SOURCE 2026-08-17 (so the next seat does not re-derive it)
> ✅ `packs.json` **v5, 25 SKUs**. 12 impulse packs, all ≤ $4.99. **Nine** ladder/bundle SKUs exceed
>    the $5 early-access cap, up to `founders-vow` **$49.99**. `frostfall-bundle` and
>    `embergrove-bundle` are both $9.99 (the dominated-pricing point). Field is `pricing.usd`.
> ✅ Banner correctly bumped to 1123 in the minting edit; the six WO files are on disk. No collision.
>
> ### ⛔ FAILED VERIFICATION — WO-1120's PREMISE IS WRONG
> The program states *"the ad button is a free skip (stub calls reward with no SDK)"* and scopes
> WO-1120 as **"Stop free grants"**. **THERE ARE NO FREE GRANTS.**
> `RewardedAdManager.ShowAdInternal` returns **false** and emits `FlowTrace.Fail`: *"NO ad SDK is
> wired… The reward is WITHHELD on purpose — it may only ever be granted from a real
> OnUserEarnedReward callback, never from having shown something."* `TryShowAd` returns true ONLY
> when a reward was genuinely earned, and `FeatureFlags.RewardedAdSkip` defaults **OFF** — the path
> is closed twice over. Nothing is being given away.
> **Why this matters beyond a wording fix:** "stop the leak" implies live revenue loss and urgency
> that do not exist, which is exactly the pressure that gets a payment path rushed onto a live
> build. WO-1120's real scope is SDK wiring + placements only. Re-scope it before starting.

**Status:** SPEC — BLOCKED behind PROD-001…004 (owner ruling above); phases 1118–1122 are the
implementable tickets once unblocked  
**Minted:** 2026-08-17 (CLI seat) — banner bumped 1117 → 1123 in the same edit (this WO + five phase WOs)  
**Lane:** Monetization (CLAUDE.md §9)  
**Class:** PRODUCT PROGRAM for a **live** Solana dApp Store build — not a PROD defect.  
  PROD tickets (001–004) stay defects. This program **makes money work** without selling vapor.  
**Sources (read before implementing any phase):**
- `docs/MONETIZATION_SME_REVIEW_2026-08-06.md` (verified pack/ads/sink audit)
- `docs/monetization-v2-spec.md` (covenant + pass model)
- Live `Assets/Resources/Data/Canonical/packs.json` **v5** (25 SKUs as of 2026-08-17)
- `docs/design/ECONOMY_PROGRESSION_THESIS_2026-08-02.md` (sink gates)
- Existing WOs: **PROD-003**, **915**, **912**, **1037**, **931**, `WORK_ORDER_economy_store_packs.md`

---

## 0. One-line truth

**You cannot become profitable by pricing vapor.**  
Today the store can *display* 25 SKUs, but most of the ladder/bundle catalog advertises cosmetics that do not render and convenience tokens that never redeem. The **only honest, deliverable money products** already in data are the **12 single-resource impulse packs** (wood/iron/food/crystals × S/M/L). Everything else is either a future surface or a refund risk.

Profitability = **real sink × honest SKU × reachable store × live payment × free-path ads × measurement**.  
Miss any one and spend dies or trust dies.

---

## 1. Live pack audit (2026-08-17, from `packs.json` v5)

### 1.1 What actually delivers (code path exists)

| Delivered on grant | Path | Notes |
|---|---|---|
| crystals / food / wood / iron | `PackStoreVM.ApplyPackContents` → `EconomyService.GrantSpendable` | **Wood/iron wired** (not vapor) |
| glimmer / coins | grant path exists | Coins have almost no sink; glimmer cosmetic shop thin |
| cosmetics (owned flag) | recorded as owned | **Most do not render** (no art / no applier callers) |
| convenience tokens | **NOT** | Explicit no-op in VM: *"no token tray yet"* |

### 1.2 Catalog map — 25 SKUs

#### A. Impulse family (12) — **KEEP; primary revenue for early access**

| SKU | USD | SKR | Grant | Verdict |
|---|---:|---:|---|---|
| impulse-wood-small/med/large | 1.99 / 2.99 / 4.99 | 25 / 36 / 60 | 1k / 3.5k / 8k wood | **Profit core** — closes real upgrade shortfalls |
| impulse-iron-small/med/large | 1.99 / 2.99 / 4.99 | 25 / 36 / 60 | 400 / 1.2k / 3k iron | **Profit core** |
| impulse-food-small/med/large | 1.99 / 2.99 / 4.99 | 25 / 36 / 60 | 1k / 3.5k / 8k food | **Park or thin** — food almost unused in build baskets |
| impulse-crystals-small/med/large | 1.99 / 2.99 / 4.99 | 25 / 36 / 60 | 250 / 700 / 1600 cr | **Keep only after crystal sink is real** (WO-1119). Today starter grant (250) + tiny sink ≈ free crystal lifetime |

**Why these are profitable when payment is on:**
- Player already wants a *specific* upgrade (high intent).
- Contents are visible and land in bank.
- Price ladder respects early-access **$5 max** ruling.
- dApp Store **0% platform fee** → $1.99 nets ~$1.99 (vs ~$1.39 after Apple 30%).

#### B. Canon ladder (5) — **REWRITE or HIDE until honest**

| SKU | USD | Crystals alone | Problem |
|---|---:|---:|---|
| hearth-spark | 1.99 | 200 | Cosmetics vapor; 1× instant-build never redeems; no wood/iron |
| lanternlight | 4.99 | 700 | Same |
| folks-thanks | 9.99 | 1800 | **Over $5 early-access cap** |
| patron-of-elarion | 19.99 | 5000 | Cap + vapor |
| founders-vow | 49.99 | **15000** | Cap + ~100× lifetime crystal sink + vapor — **do not sell live** |

#### C. Themed bundles (8) — **DOMINATED / REDUNDANT**

Same-price strict dominance (SME): Spring/Bloomtide loses to Lanternlight/Starter's Hand; Frostfall ≈ Embergrove byte-twins; Hero Wardrobe sells outfits the rig cannot show; Builder's Cache is ~tokens that do not exist.

**Rule:** never ship two SKUs at one price where one wins on every axis.

### 1.3 Profitability ranking (honest, today)

| Rank | Product | Why | Blocker |
|---:|---|---|---|
| 1 | Impulse wood/iron S/M/L | Real grant, real sink, high intent | Payment flag + mainnet + shortfall UX (1037) |
| 2 | Crystal packs / Finish-Now | Recurring if sink exists | Sink too small; boost not built |
| 3 | 2× harvest boost (time, not power) | Recurring crystal sink | Engine not built (WO-1119) |
| 4 | Rewarded ads (free path) | eCPM + conversion to paid skip | SDK stub; free grants today (WO-912 / 1120) |
| 5 | 3-SKU value ladder ($1.99/$4.99) | Basket packs for non-shortfall buyers | Must strip vapor first (WO-1118) |
| 6 | Season pass (Keeper's Almanac) | Retention LTV, not day-1 ARPU | Needs cosmetic track that **renders** |
| 7 | Cosmetics / Founder's banner | High LTV later | Zero render pipeline today |
| ❌ | Revive-on-defeat ads | Forbidden (covenant + network) | Already deleted from ad-placements |
| ❌ | Founder's Vow at $49.99 live | Trust + economy nuke | Hide until graduation |

---

## 2. The money machine (target architecture)

```
  FREE PATH                         PAID PATH
  ─────────                         ─────────
  Wait for timer                    Impulse wood/iron (shortfall)
  Optional rewarded ad (minutes)    Crystal impulse → Finish-Now
  Daily ad harvest 2× (1h)          2× Harvest 4h/24h (crystal or pack)
                                    3-SKU value ladder (resources+time)
                                    Later: Season Pass (cosmetic track)
                                    Later: expression cosmetics

  BOTH feed the same sinks: build time, harvest wait, upgrade shortfall.
  NEITHER sells combat power.
```

**Covenant (binding):** *"You are never required to spend. When you do, you buy time and beauty — never victory."*

---

## 3. Phase sequence (do not reorder)

| Phase | WO | Goal | Depends |
|---|---|---|---|
| **P0** | **1117** (this) | Strategy + owner ruling checklist | — |
| **P1** | **1118** | Honest shelf: hide vapor; keep impulse; 3-SKU rewrite | Owner $5-cap confirm |
| **P2** | **1119** | Crystal sink + harvest boost Version B | P1 contents that reference boost |
| **P3** | **PROD-003** + **1037** | Storefront + shortfall offer surface | Placement ruling; flag OFF until P4 |
| **P4** | **1121** (+ **915**) | Live money: mainnet / SKR mint / purchase gate | Owner A/B on public Buy |
| **P5** | **1120** (+ **912**) | Ads: real SDK; stop free grants; placement table live | LevelPlay account / D3 |
| **P6** | **1122** | Season pass + KPI ops | Cosmetic render OR pure-currency track ruling |
| **Ops** | **1116** | Admin dashboard (already Phase 1 built) | Grants = Phase 2 owner rulings |

**Until P4 is green, the honest live product is: storefront browse + shortfall display + Buy OFF.**  
Never flip Buy on to sell vapor (WO-931 lesson).

---

## 4. Owner rulings needed (block wrong builds)

Answer in this file or a session note; phases stay SPEC until marked:

| # | Question | Recommendation |
|---|---|---|
| R1 | Keep **$5 max** early-access until graduation? | **YES.** Graduate when: (a) ads real, (b) boost redeemable, (c) ≥1 cosmetic category renders |
| R2 | Hide ladder >$5 and Founder's on live shelf now? | **YES** — data flag `shelfVisible: false` or remove from default filter |
| R3 | Drop food impulse family from shortfall offers? | **YES** for shortfall; optional keep in full store as low priority |
| R4 | Realm Store placement (PROD-003 a/b/c)? | **(a) market plaza across from Coppin** |
| R5 | Public Buy: OFF until payment proven (915 A) vs ON when mint ready (B)? | **(A)** until checklist green |
| R6 | Season pass: delay until cosmetics render, or ship currency-only free track? | **Delay paid pass** until one cosmetic category ships; free track optional |
| R7 | Harvest boost stack rule | **extend duration only; hard cap 2.0×; never boost crystals** (SME) |

---

## 5. Success metrics (when 1122 lands)

| KPI | Target (first 30 days post-Buy-ON) | Notes |
|---|---|---|
| Purchase conversion (store open → paid) | Measure baseline | No target until n≥100 opens |
| Impulse share of revenue | **≥60%** early | Proves shortfall thesis |
| Crystal ARPU vs pack ARPU | Track weekly | Sink health |
| Ad fill rate / eCPM | Network baseline | Free path health |
| Refund / never-equipped rate | **≈0** for impulse | Cosmetics later |
| Covenant incidents | **0** combat sells | Hard fail |

Events already planned in `docs/biz/ANALYTICS_KPI_PLAN.md` — implement taxonomy in WO-1122, do not greenfield transport (`EventTracker` exists).

---

## 6. Explicitly NOT this program

- Combat power packs, revive ads, loot boxes, energy systems, FOMO countdowns  
- Greenfield second store (extend PackStore)  
- Hand-edit hub scene (PROD-003 uses builder)  
- Activating `skr_store.json` acquisition packs without killing the 2.9× SKR arbitrage  
- Selling Founder's Vow on a live client until R1 graduation  

---

## 7. Acceptance for the PROGRAM (not a single PR)

1. Every phase WO has Status flipped DONE with RESULT file.  
2. Live shelf sells only deliverable grants.  
3. Buy-ON only after WO-1121 checklist.  
4. Ads never grant free rewards without impression (no stub).  
5. Owner can name weekly revenue + top SKU from admin/KPI without grepping logs.  
