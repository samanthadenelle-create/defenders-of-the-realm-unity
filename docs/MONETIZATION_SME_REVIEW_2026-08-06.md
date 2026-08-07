# Monetization SME review — packs, pricing, ads, and the 2x harvest boost (2026-08-06)

> Commissioned by the owner 2026-08-06: "I want a professional in monetization to look them over and
> make sure everything for ads is functional as well ... set purchase price to whatever real amounts
> monetization wants ... make the packs actually work well since we now have a true structure and the
> real value is time (crystals) then resources, and speed ups that can increase resource speed by 2
> for a x duration."
>
> VERIFIED = opened at source, file:line cited. JUDGEMENT = recommendation.
> Read-only review. No files were edited to produce it.

---

## HEADLINE — four things that are all true at once

1. **Rewarded ads are not implemented, and the button gives the reward away for free.**
   `RewardedAdManager.ShowAdInternal` (`Assets/_Modules/Village/Monetization/RewardedAdManager.cs:97-100`)
   is `onReward?.Invoke();` — nothing else. No SDK, no impression, no revenue. The file header says so
   at `:3`. It IS reachable in a shipped build via `BuildTimerService.WatchAdToSkip` (`:419-433`).
   **Net: "Watch an ad to skip 10 minutes" is a free instant-skip button, 10x per rolling 4h.**

2. **Every convenience token in every pack evaporates on grant.**
   `PackStoreVM.cs:126-128`, verbatim: *"Convenience tokens are consumable items - the v2 foundation
   has no token tray yet."* 153 advertised tokens across 13 packs; zero handlers exist.

3. **The crystal sink is ~154 crystals for the ENTIRE game, and a fresh save starts with 250.**
   `instantFinishCrystalsPerMinute = 1`, floor 5 (`BuildTimerConfig.cs:111,114`); the most expensive
   live structure (`fountain_healing`, basket 440) is tier 3 -> 13.5 min -> 14 crystals; everything
   else prices at the 5-crystal floor. Starter grant is 250 (`GameState.cs:50`).
   **A brand-new player can instant-finish every timer in the catalog before buying anything.**

4. **Two files price SKR against USD at rates differing by 2.9x**, creating a 60%-off arbitrage.

---

## (1) PACK AUDIT

### What a pack actually delivers (VERIFIED, `PackStoreVM.ApplyPackContents:68-153`)

| Advertised | Delivered? |
|---|---|
| glimmer / crystals / food / coins | YES (`:101`, `:105-107`) |
| wood / iron | code path exists (`PackCatalog.cs:51,53`) but **no pack authors them** |
| cosmetics | recorded as owned (`:111-124`) — but see below |
| **convenience tokens** | **NO. Never. Not one kind.** (`:126-128`) |

**Cosmetics resolve to nothing.** 15 of 40 pack-referenced cosmetic SKUs have no row in
`cosmetics.json` — and they are exactly the five canon ladder packs. `packs.json:2` claims "no granted
cosmetic is dangling"; that statement is FALSE. The 25 that do exist have **no art-pointer field** in
the schema — a cosmetic IS a hex colour swatch. `CosmeticApplier` has zero callers.
**40 SKUs -> 25 rows -> 0 render anything.**

### Per-pack verdict

| # | Pack | Price | Actually delivers | Verdict |
|---|---|---|---|---|
| 1 | Hearth Spark | $1.99 / 25 SKR | currency only | Only pack inside the $5 ruling. Keep price, rebuild contents |
| 2 | Lanternlight | $4.99 / 60 | currency only | Keep price point, rebuild |
| 3 | Folk's Thanks | $9.99 / 120 | currency only | Over cap. Cut for early access |
| 4 | Patron of Elarion | $19.99 / 240 | currency only | Over cap. Cut |
| 5 | Founder's Vow | $49.99 / 600 | currency only | **10x over ruling; 15,000 crystals = ~97x the game's entire crystal sink. Do not ship** |
| 6 | Frostfall | $9.99 | currency only | Dominated by #3 at same price |
| 7 | Embergrove | $9.99 | currency only | **Byte-twin of #6, different palette.** Redundant |
| 8 | Spring Awakening | $4.99 | 600cr only | **Strictly dominated by #2** at the same price |
| 9 | Starter's Hand | $4.99 | 900cr/300f/600c | **The only coherent pack in the file** |
| 10 | Echo Patron | $19.99 | currency only | Dominated by #4 |
| 11 | Hero Wardrobe | $9.99 | 800cr only | **Worst SKU.** 100% of value is outfit swaps the hero rig cannot do |
| 12 | Realm Defender | $9.99 | 1000cr only | Cosmetic-only value, no art |
| 13 | Builder's Cache | $19.99 | 3500cr only | **91% of stated value is tokens, which do not exist** |

### Structural problems
- **Price points unanchored** — five SKUs at $9.99, three at $19.99; within each band one pack
  strictly dominates on every currency.
- **No pack sells RESOURCES** (her #2 value). Build costs are wood/iron (5-160 wood, 15-100 iron);
  **zero packs grant either**, though the C# fields are wired.
- Packs sell **food and coins**, which are near-worthless — only 3 of 29 structures cost food; coins
  have no build sink.
- `founderOnly` is **not enforced** — label only (`PackStore.cs:327-331`). No date gate.
- Dead JSON: `theme`, `packExclusiveCosmetic`, `convenience[].description`, `version`.
  `pricing.sol`/`usdc` parse but the rail selector was removed — **SKR is the only chargeable rail**.
- **Post-payment loss window**: grants go through `AppDomain` type-name reflection
  (`PackStoreVM.cs:170-181`). If a service is not up at that instant the player is charged and gets
  nothing. It logs loudly — the money is still gone.

---

## (2) PRICING

### The SKR arbitrage (VERIFIED — authored, not yet live)

| Source | Implied USD per SKR |
|---|---|
| `packs.json` ladder ($49.99 / 600) | $0.0833 |
| `monetization-v2-spec.md:87` peg | ~$0.083 (consistent) |
| `skr_store.json` Token Pouch | $0.0499 |
| `skr_store.json` Token Satchel | $0.0363 |
| `skr_store.json` Token Coffer | $0.0290 |

Consequence: **buy Token Coffer $19.99 -> 690 SKR -> buy Founder's Vow (600 SKR)** = a $49.99 pack for
$19.99 with change. **60% off.** Same bundle is 60 SKR in one file and 180 in the other.

**Not live today** — `skr_store.json` has no runtime C# loader (only an editor regression reads it).
But it is authored canon and ships the moment someone implements it.

### Recommended ladder — 3 SKUs, 2 price points (JUDGEMENT)

Inside the existing early-access ruling ($2/$5 tiers, $5 max, SKR).

| SKU | USD ref | SKR | Contents | Role |
|---|---|---|---|---|
| Hearth Spark | $1.99 | 25 | 150 crystals; 1,500 wood + 800 iron + 500 food; 1x "2x Harvest, 4h" | Impulse |
| Starter's Hand | $4.99 | 60 | 400 crystals; 4,000 wood + 2,000 iron + 1,500 food; 3x "2x Harvest, 4h"; 5x instant-build | Volume SKU |
| Keeper's Satchel | $4.99 | 60 | 900 crystals; 1x "2x Harvest, 24h"; 2 extra Builder slots (30 days) | Time-forward alternative |

- Crystals cut ~4x — matched to a ~154 sink. Resources are the BIGGEST line (the missing #2 value).
- Two SKUs at one price differ **on axis, not quantity** — resource-forward vs time-forward. Never
  again ship five SKUs at one price where one wins on everything.
- **Zero cosmetics until something renders.**
- Purchased grants are never clamped (`TownBankCapacity.cs:258`, Law 5) so large resource lines land
  in full and grandfather over cap — deliberate.
- **Derive every SKR amount from ONE peg field at build time.** Hand-authoring SKR in two files is
  exactly what produced the 2.9x arbitrage.

**Recommended addition to the ruling:** give the $5 cap an explicit graduation trigger — it holds
until (a) ads are a real SDK, (b) tokens redeem, (c) at least one cosmetic category renders. Today the
cap is silently doing the job of a content gate; name it as one.

**0% platform fee is a low-price weapon:** $2.00 gross nets ~$2.00, where an App Store $2.99 nets
$2.09. You can price ~30% under the mobile equivalent and still net more. That story lands at impulse
prices and is invisible at $49.99.

---

## (3) THE 2x HARVEST BOOST — covenant verdict and design

### ALLOWED — but only one of the two implementations, and the wrong one is the tempting one

`EchoService.cs:126` rate and `EchoService.cs:149` **silo capacity are scaled by the SAME multiplier.**

- **Version A — multiply inside `AggregateHarvestMultiplier()`:** rate doubles AND cap doubles. Fill
  time stays 4h; an offline player gets **exactly twice the resources**. That is MORE, not SOONER —
  **it crosses the covenant. And it is the version you get by accident**, because that function looks
  like the natural hook.
- **Version B — multiply `RatePerSecond` only, cap untouched:** silo fills in 2h instead of 4h. Total
  banked unchanged, it just arrives sooner. **Pure time. Ship this one.**

Be honest about B: to a player away 8 hours it delivers **nothing** (the silo capped either way). B
only pays a player who returns and dumps. **Pair it with auto-collect** so the silo drains while the
boost runs — then 2x rate becomes 2x banked because you automated a tap, which is time, not power.

Supporting facts: kind `harvest_boost` is **already covenant-allowlisted** (`PackCatalog.cs:219`), and
a 2.0x/1h doubler is already authored as an ad reward in `ad-placements.json`.

### Spec

Reuse `harvest_boost` + the `BoostSpec` already designed in `WORK_ORDER_economy_store_packs.md` §2c.
`ConvenienceItemDef` is only `{Kind, Count, Description}` today (`PackCatalog.cs:64-68`) — adding
`BoostSpec Boost` is the entire schema surface. **Timed buff, not a charge stack** (charges need the
token tray, which does not exist).

| Duration | Mult | Granted by | Price |
|---|---|---|---|
| 30 min | 2.0x | Rewarded ad | free, 3/day |
| **4 hours** | 2.0x | **Crystal purchase ~120 crystals** | **the recurring sink the game lacks** |
| 24 hours | 2.0x | Pack content | $1.99 / $4.99 tiers |

4h is chosen because `SiloCapHours = 4f` (`EchoService.cs:73`) — a boost longer than the silo cap is
wasted on an absent player. **Anything longer must ship with auto-collect or it is overselling.**

**This is the highest-value single recommendation in the report.** A repeatable 120-crystal boost turns
crystals from a 154-crystal lifetime sink into a recurring one — fixing the pricing problem from the
sink side, which is the only side that can be fixed.

### Two hard exclusions
1. **Never boost crystals.** `echoes-balance.json` states the intent: crystals *"remain the slowest
   faucet (monetization guard, WO-830 Sec.3b)."* `appliesTo` = wood | iron | food | all-harvestables.
2. **Cap effective multiplier at 2.0x, no multiplicative stacking.** The existing design doc permits
   2x * 2x = 4x capped at 5x — **reject it.** At 5x the non-payer is generations behind and "sooner"
   stops being honest. `extend` (add duration) is the only legal stack rule.

### Attach points (VERIFIED)
| Attach | file:line | Note |
|---|---|---|
| `EchoService.RatePerSecond` | `EchoService.cs:126` | **Primary.** Covers online tick AND offline claim. Add as a NEW trailing factor — never inside `AggregateHarvestMultiplier()` |
| `ResourceBuildingState.CurrentEffectiveYield` | `:94-103` | Multiply the YIELD, not the interval (interval clamps at 0.5s) |
| `OfflineHarvestService.ClaimAccrual` | `:132-181` | Needs its own factor if the boost covers node/pet accrual |
| `ModifierService.ProductionMultFor` | `:74-84` | **DO NOT USE** — 3-case switch; iron via `collector_forge` hits the `1f` default and is silently missed. This exact id-mismatch class already killed every food perk once |

### Two bugs it will ship with if unspecified
1. **Partial-window over-payment.** `ClaimAccrual` integrates rate x elapsed. Must integrate the
   OVERLAP: `boostedSec = max(0, min(now, boostEnd) - max(lastClaim, boostStart))`.
2. **The boost will DESTROY the resources it accelerates.** Earned income IS clamped
   (`TownBankCapacity.ClampGrant:450-497`) and overflow is logged `BANK FULL ... LOST n` and
   vaporised. **The boost must check `HasHeadroom` (`:423`) and refuse to start** with a plain-text
   warning ("Bank full - build or upgrade a Lumberyard first"). Text, not colour.

### Persistence
No timed-buff infrastructure exists in the save. Needs two fields, both default-inert so old saves
read-migrate: `HarvestBoostEndsAtMs` (double, unix-ms) and `HarvestBoostMult` (float). Use the SAME
clock as `LastHarvestClaimMs`.

---

## (4) ADS — NOT FUNCTIONAL

Beyond "not wired": it is **called, and it grants the reward with no ad**.

```csharp
// RewardedAdManager.cs:96-100
// TODO: integrate Unity Ads / AdMob SDK in a platform override of this method.
protected virtual void ShowAdInternal(Action onReward)
{
    onReward?.Invoke();
}
```

`IsAdReady` is a pure 480-second stopwatch (`:45-46`), not a fill check. The rolling-window ledger,
the conversion-trigger tuning and the tamper analysis are all real and all working — they are gating
**a button, not an ad**.

### Dead authored data
- `ad-placements.json` — complete placement/reward table, `"adProvider": "stub"`. **No C# reads it.**
- `skr_store.json` / `skr_staking.json` — no runtime loader.
- `offline-storage.json` — no reader, and **stale**: declares `maxEchoes: 4` (live is 6) and iron/food
  base cap 1500 (live is 2000). Will mislead the next reader.

### COVENANT VIOLATION in authored ad data
`ad-placements.json` -> `reward.revive.freeContinue`: *"Revive and continue the battle once"*, surface
`defeat`, cap 2/day. **That is exactly an OUTCOME you would otherwise lose** — the thing the covenant
forbids. Inert only because nothing reads the file. **Delete it before the interpreter is written.**

### Everything missing to ship ads
1. No ad SDK anywhere (no AdMob/Unity Ads/ironSource/AppLovin package, plugin, aar).
2. No ad unit / app / placement ID in any form.
3. No mediation, network account, or payment/tax setup.
4. No real `ShowAdInternal` override (the seam is correct — subclass it, do not edit the base).
5. `IsAdReady` must become a real fill check.
6. Reward must fire on `OnUserEarnedReward`, **never on show** — on a real SDK, granting on show is
   fraud against the network.
7. No no-fill / dismissed-early path.
8. **No server-side reward verification.**

### The tamper problem — fix BEFORE the SDK, not after
`BuildTimerService.cs:645-649`, verbatim: *"the window start is a DEVICE clock (UtcNow). Moving the
device clock forward past the window grants a fresh allowance ... once a real ad SDK is behind this it
is FABRICATED IMPRESSIONS against the ad account, which is what networks ban for."*
**An invalid-traffic ban is not recoverable.** Tracked as WO-912.

### Compliance gates specific to ads
Per `docs/SECURITY_COMPLIANCE_HARDENING_AUDIT.md` §2D (flagged, not legal advice): no age gate
(COPPA / child-directed declarations), no consent flow (GDPR/CCPA for personalised ads), and
`respectDoNotSell` is authored with nothing reading it.

### Timer/ad tension
`adSkipSeconds = 600s` against a **13.5-minute longest timer** means **two ads finish anything**. The
conversion-trigger reasoning in `BuildTimerConfig.cs:80-89` describes timers that DO NOT EXIST in the
live catalog. **Fix the timers before wiring the SDK.**

---

## RECOMMENDED ORDER OF OPERATIONS

1. **Fix the sink, not the price.** Retune `tierCostThresholds` / catalog costs so bands 4-5 are
   reachable. Nothing in pricing or ads is meaningful until a timer can outlast two ad watches.
2. **Stop selling vapor.** Pull cosmetics until one category renders; pull convenience tokens until a
   redeemer exists. Both were flagged 2026-06-28 and 2026-07-02 and neither moved.
3. **Build the 2x harvest boost (Version B) and sell it for crystals.** The recurring sink.
4. **Collapse to the 3-SKU / 2-price ladder**, deriving SKR from one peg field.
5. **Delete `skr_store.json` acquisitionPacks and `reward.revive.freeContinue`.**
6. **Then** wire a real ad SDK, with server-side window validation in the same change.

Until step 6, the honest description of the ad system is: **not implemented, and currently giving away
the reward for free.**
