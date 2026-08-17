<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-28
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-28) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK ORDER — Battle Packs + Monthly Reward Packs (Monetization Family Design)

**Type:** DESIGN SPEC (ideas + data schema + sample data). **No `.cs` in this WO** — implementation is a follow-up WO.
**Status:** DRAFT FOR OWNER REVIEW — not yet READY TO IMPLEMENT.
**Author lane:** Monetization/Backend (§9 parallel lane — fully isolated; no scene/combat files).
**Date:** 2026-06-28
**Supersedes nothing.** *Layers on top of* `docs/monetization-v2-spec.md` (the covenant) + `Assets/_Modules/Wallet/PackCatalog.cs` (`PackDef`) + `packs.json` + the sibling WOs `WORK_ORDER_skr_store_design.md` / `WORK_ORDER_offline_storage_logic.md` / `WORK_ORDER_economy_store_packs.md`. Does **not** replace them.
**Sample data delivered:** `Assets/StreamingAssets/Data/Canonical/battle_monthly_packs.sample.json` (PackDef-shaped + two small additive extension blocks). The live `packs.json` is **untouched**.

---

## 0. The one-sentence shape

> Two new monetization *families* that obey the **bent covenant 100%** — **(A) Battle Packs**: a seasonal **Battle Pass** whose tiers are earned by *playing arena battles* (free lane + premium SKR/real-money lane), paying out **cosmetics, SKR, soft currency, and out-of-combat convenience only**; plus one-off **battle cosmetic SKUs** (arena/crown/VFX skins). **(B) Monthly Reward Packs**: a classic "monthly card" — pay once, a **daily reward drip** over ~30 days that layers onto the existing daily system as a *bonus*, never a timer you must pay to avoid.

### The HARD firewall (binding on every entry in this WO)

Inherited verbatim from `docs/monetization-v2-spec.md` §2 and `combat-pivot-single-hero-northstar`:

- **Zero combat power. No pay-to-win.** Nothing in any battle pack, battle SKU, or monthly card may grant in-fight advantage: **no revives, no mid-battle heals, no damage/armor/crit/fire-rate boosts, no extra lives, no stat passives, no level/cap raises, no consumables usable during an arena fight.** There is **no `combat` category** and the validator rejects one (`SkrStoreRegression`-style gate, §6).
- What battle packs MAY grant: **cosmetics** (arena skins, victory **crown** tiers/skins, spell-VFX colorways, weapon/armor *cosmetic* reskins — static per the single-Knight pivot), **SKR** (the premium token, sibling WO), **soft currency** (glimmer/crystals/food/coins — *out-of-combat* economy), and the **already-sanctioned convenience tokens** (`instant-build` / `instant-repair` / `harvest-auto-collect` / `xp-weekend` — `ConvenienceItemDef.Kind`, all out-of-combat time-savers).
- The Battle Pass **track is earned by playing**, not bought. Money/SKR buys the *premium reward lane* (better cosmetics) — it **never buys tiers/power**, and never skips the *need to play* for the premium cosmetics. (Optional paid tier-skips, if the owner ever wants them, buy only **cosmetic** catch-up — see Open Q #4.)

---

## 1. GROUNDING — what already exists (read, not assumed)

| System | File | What we reuse |
|---|---|---|
| **Pack schema** | `Assets/_Modules/Wallet/PackCatalog.cs` | `PackDef` (sku/tier/name/tagline/theme/founderOnly/pricing{usd,usdc,sol,skr}/contents{cosmetics[],economy{glimmer,crystals,food,coins},convenience[]}/packExclusiveCosmetic). **Battle cosmetic SKUs are plain `PackDef`s** — no new type. |
| **Live pack data** | `Assets/StreamingAssets/Data/Canonical/packs.json` | Pattern + `CanonicalJson.Read` WebGL-safe load. We **don't edit it**; we ship a sibling sample. |
| **Covenant** | `docs/monetization-v2-spec.md` §2/§5.3 | Cosmetic+convenience-only; the four legal convenience kinds. |
| **SKR rail** | `WORK_ORDER_skr_store_design.md`, `WalletService.cs` `CurrencyKind.Skr` | SKR is the premium token; the pass's premium lane + battle SKUs price/pay in SKR. The pass credits the **same `ISkrLedger`**. |
| **Arena W/L ledger** | `Assets/_Modules/Village/Arena/ArenaProgressStore.cs` | `RecordWin(purse)` already tracks `Wins`/`Streak`/`TotalPurse` in `GameState.Arena` + PlayerPrefs. **This is the Battle-Pass XP source** — every recorded win emits pass XP. No new combat hook. |
| **Victory crowns** | `BattleArenaHud.cs` + `RpgUiCatalog` (`crown_tier1/2/3`, `CrownPerfect`) | Crown tiers already render post-victory. **Battle-pass crown rewards swap the crown *skin/material*** — pure presentation, the firewall is intact. |
| **Earned cosmetic currency** | `Assets/_Modules/Cosmetics/GlimmerCurrencyService.cs` | `TryAddGlimmer` / `TryPurchase` / `Owns` / `Equip` — the **free-lane grant sink** + ownership writer for cosmetics. Already daily-quest-fed. |
| **Daily reward dispenser** | `Assets/_Modules/Village/Quests/DailyQuestRewardBridge.cs` | The proven **daily grant routing** (`AddCrystals`/`AddFood`/`TryAddGlimmer`/`WisdomGrant`/`VillageInventory.Add`) + the **`ClaimedAtUnix` double-grant latch**. **The Monthly Card's daily drip reuses these exact grant routes + the latch pattern.** |

**Net:** both families are mostly *data + a thin claim/credit interpreter over systems that already exist.* New code is a loader, a claim/ledger, and code-built Obsidian UI — no new combat surface, no new currency primitive.

---

## 2. FAMILY A — BATTLE PACKS

### 2.1 The Battle Pass (seasonal track)

A season is a **data table**: a list of **tiers**, each with a **free reward** and a **premium reward**. The player climbs tiers by **earning Battle XP**, and Battle XP comes from **playing arena battles** — never from a purchase.

#### XP source (earned by PLAYING — binding)
- **Primary:** each `ArenaProgressStore.RecordWin(purse)` → grants `xpPerWin` Battle XP (default **100**).
- **Secondary (engagement, still play-only):**
  - a *participation* trickle on a completed battle even on loss (`xpPerLoss`, default **25**) so a rough night still advances the free lane,
  - a **streak bonus** (`xpPerStreakStep` × current `Arena.Streak`, capped) reusing the streak the store already tracks,
  - a **perfect/flawless** bonus (`xpPerfectBonus`) once the no-hit signal lands (`BattleArenaHud` already has the `perfect` crown hook).
- **Daily soft cap (anti-burnout, non-predatory):** `dailyXpSoftCap` (default 1500) — past the cap, wins still pay *purse + cosmetics*, XP just tapers (50%). This protects players from feeling they "must grind"; it is a generosity cap, not an energy gate. **No XP is ever buyable** (Open Q #4 covers optional cosmetic-only catch-up).

#### Two lanes
- **Free lane** — every player, no purchase. Pays **soft currency + glimmer + the occasional convenience token + a couple of free cosmetics + small SKR drips**. A non-payer who plays the season earns real stuff.
- **Premium lane** — unlocked by buying the **Season Pass SKU** (priced in real-money/SOL/USDC/**SKR**). Unlocking it **retroactively grants the premium reward for every tier already earned** (standard battle-pass courtesy). Pays the **good cosmetics** (exclusive arena skin, animated crown skin, spell-VFX colorways, weapon/armor cosmetic), **more SKR**, and **more convenience tokens**. **Still requires playing to climb** — buying the pass unlocks the *lane*, not the *tiers*.

#### Season lifecycle
- `seasonId`, `name`, `startUtc`/`endUtc`, `lengthDays` (default **35** — ~5 weeks).
- At `endUtc`: **earned rewards are kept forever** (owned cosmetics stay owned); the *track* resets for the next season. Unclaimed-but-earned tier rewards auto-grant at season close (no "you lost it" trap).
- The **capstone tier** (last) premium reward is the season's prestige cosmetic (e.g. an animated champion crown skin) — scarce *expression*, never advantage.
- **Lapse is gentle:** if you buy the premium pass late in the season, the retroactive grant means you lose nothing you earned; if you never buy it, you keep all free-lane rewards.

#### Reward kinds per tier (reuses the `Grant` shape from the SKR WO §4.2 Table D)
`cosmetic_sku` (→ `GlimmerCurrencyService`/wardrobe `OwnedItemIds`), `skr` (→ `ISkrLedger.Credit`), `economy` (→ `GameStateService.Add*` / `TryAddGlimmer`), `convenience_token` (→ token tray, `ConvenienceItemDef.Kind` only). **No `combat` kind exists.**

### 2.2 Battle cosmetic packs (one-off SKUs)

Plain **`PackDef`s** (no new type) sold for real-money/SOL/USDC/SKR, whose `contents.cosmetics` are battle-flavored:
- **Arena skins** — the battle-arena ground/ambiance reskin (presentation only).
- **Crown skins** — swap the `crown_tier*` / `CrownPerfect` material/sprite shown on victory (`BattleArenaHud` already renders the crown; the skin is a cosmetic SKU the HUD resolves).
- **Spell-VFX colorways** — recolor the Knight's skill VFX (no damage/scale change — pure shader/particle tint).
- **Weapon/armor cosmetic reskins** — static per the single-Knight pivot (no stat delta).

These carry **`packExclusiveCosmetic`** like every other pack and slot into the **existing PackStore** unchanged — they are content rows, not a new store.

### 2.3 What is BANNED in battle packs (and why) — the explicit list

| Banned | Why |
|---|---|
| Revives / extra lives / continue-tokens in a fight | Directly buys a win → pay-to-win. Covenant §2. |
| Mid-battle heal / shield / potion usable in the arena | In-fight power. Convenience tokens are **out-of-combat only**. |
| Damage / armor / crit / attack-speed / cooldown boosts (any duration) | Combat stat advantage. No `combat` category exists; validator rejects. |
| Permanent passives / stat trees bought with money or SKR | Power creep behind a paywall. Talents are earned (Wisdom), never sold. |
| Buying Battle XP / tiers / "instant max pass" that confers power | The track must be *earned by playing*. Only cosmetic catch-up is even discussable (Open Q #4). |
| Loot boxes / gacha / randomized pass rewards | No randomized spend (monetization spec C3). Every tier reward is shown up front. |
| Level/cap raises, extra arena entries that gate progression | Energy-gate / progression-paywall. The arena is free to play unlimited. |

`xp-weekend` (2× **XP**) is explicitly **allowed** — it accelerates *out-of-combat progression pacing*, the same as the existing packs (`packs.json` Patron/Founder), and confers **no in-fight advantage**.

---

## 3. FAMILY B — MONTHLY REWARD PACKS (the "monthly card")

### 3.1 Shape

Classic, transparent **monthly card**: **pay once → a daily reward drip over ~30 days**, plus a **month-exclusive cosmetic** granted up front. Two tiers (a **Wayfarer's Ledger** basic + a **Keeper's Ledger** premium). The drip pays **soft currency + glimmer + occasional convenience tokens + (premium) small SKR**, and **never combat power.**

### 3.2 Non-predatory stance (binding)

- The drip is a **bonus on top of** the existing free daily system (`DailyQuestRewardBridge`), **not** a timer you must pay to avoid and **not** a nerf to the free daily. A non-buyer's daily rewards are unchanged.
- **No "claim or lose it forever" pressure:** missed days do **not** burn the card. The card has a fixed budget of `durationDays` *claims*; if you miss a day, that day's reward **rolls into the remaining claim pool** (or extends the expiry by the missed days — Open Q #2). You always receive the full value you paid for.
- **No login-streak punishment** for the paid card. (A *free* streak bonus may exist elsewhere; the card itself never resets on a miss.)
- **Full 30-day table shown pre-purchase.** No hidden "mystery day." No randomized daily (no gacha).
- **Month-exclusive cosmetic granted immediately** on purchase (so value is never back-loaded / hostage to daily logins).

### 3.3 Claim / streak / lapse rules (data-driven)

- `durationDays` (default **30**) = number of daily claims the card grants.
- `dailyTable[]` = one reward `Grant` per day (may be uniform or escalating; the sample escalates slightly + front-loads the cosmetic).
- **Claim model** = *consume one claim per UTC day*, latched exactly like `DailyQuestRewardBridge.ClaimedAtUnix` (a per-day `claimedUtc` latch so a re-open can't double-grant).
- **Missed day** → the unclaimed day is **not lost**: either (a) **pool model** — remaining claims simply take longer to exhaust (card lives until all `durationDays` claims are spent), or (b) **calendar model** — expiry = `purchaseUtc + durationDays`, missed days forfeit. **Recommend the pool model** (most generous, fully non-predatory). Owner picks (Open Q #2).
- **Stacking:** buying a new month while one is active **extends** the claim pool (appends `durationDays` claims), never overwrites. The exclusive cosmetic only grants once per distinct monthly SKU (`repeatable:false` on the cosmetic grant).
- **Lapse / expiry:** when claims run out, the card is simply **done** — the exclusive cosmetic is kept forever; no penalty, no "renew or lose your stuff."

### 3.4 Hook into the existing daily system

- Reuse `DailyQuestRewardBridge`'s grant routes verbatim (`AddCrystals`/`AddFood`/`TryAddGlimmer`/`WisdomGrant`/`VillageInventory.Add`) + add `ISkrLedger.Credit` for the premium SKR line. **One fulfillment path** — the card never invents a parallel inventory.
- A new lightweight `MonthlyCardService` (future WO) holds the active card(s) + per-day claim latch in save (mirrors `GlimmerCurrencyService` PlayerPrefs-blob pattern, or `GameState` when the schema wires it). On daily boot it checks "is a UTC day available?" and, if so, surfaces a **claim** affordance (never auto-pops mid-play — discovery rule C5).
- The free daily quest reward and the monthly card claim are **independent** — claiming one never consumes the other.

---

## 4. DATA MODEL — additive, backward-compatible

PackDef stays the canonical schema. Battle **cosmetic SKUs** are pure `PackDef`s (zero schema change). The **Battle Pass season** and the **Monthly Card** need a few fields `PackDef` lacks, so they live as **two small additive extension blocks** in a *separate* sample file — `packs.json` is untouched and the loader can ignore unknown blocks.

### 4.1 Extension A — `battlePassSeasons[]` (new optional block)

```
BattlePassSeason {
  seasonId       : string                 // "season-01-emberwake"
  name           : string                 // display
  tagline        : string                 // narrative-bible voice
  startUtc       : string                 // ISO-8601
  endUtc         : string                 // ISO-8601
  lengthDays     : int                    // 35
  premiumPassSku : string                 // the PackDef SKU that unlocks the premium lane
  xp : {                                   // XP-from-PLAY rules (no buyable XP)
    perWin          : int,                 // 100
    perLoss         : int,                 // 25
    perStreakStep   : int,                 // 10 * Arena.Streak, capped
    streakStepCap   : int,                 // 10
    perfectBonus    : int,                 // 150 (when no-hit signal lands)
    dailySoftCap    : int,                 // 1500 (taper past, never hard-gate)
    softCapTaperPct : number               // 0.5
  }
  tiers : [ BattlePassTier ]
}
BattlePassTier {
  tier        : int                        // 1..N (sequential)
  xpRequired  : int                        // cumulative XP to unlock this tier
  free        : Grant?                      // null = empty free slot this tier
  premium     : Grant?                      // null = empty premium slot this tier
  isCapstone  : bool                        // last-tier prestige flag
}
```
`premiumPassSku` is a real **`PackDef`** row (sold via PackStore) whose purchase flips the player's `premiumUnlocked` flag for that season + retro-grants earned premium tiers. `Grant` = the SKR-WO §4.2 Table D shape (`cosmetic_sku | skr | economy | convenience_token | bundle`). **No `combat` kind.**

### 4.2 Extension B — `monthlyCards[]` (new optional block)

```
MonthlyCard {
  sku            : string                  // "monthly-wayfarer", reuses PackStore purchase rails
  tier           : int                     // 1 basic / 2 premium
  name           : string
  tagline        : string
  pricing        : { usd, usdc, sol, skr } // SAME PackPricing shape (reuse type)
  durationDays   : int                     // 30
  exclusiveCosmetic : string               // granted up-front on purchase (own-once)
  claimModel     : enum { pool, calendar } // recommend 'pool' (non-predatory)
  stackable      : bool                    // true = extends claim pool on re-buy
  dailyTable     : [ DailyDrip ]           // length == durationDays
}
DailyDrip {
  day            : int                     // 1..durationDays
  grant          : Grant                   // soft currency / glimmer / convenience token / (premium) skr
  highlight      : bool                    // milestone day (e.g. day 7/14/30 bigger drop)
}
```
The per-day claim latch (`claimedUtc` per day) mirrors `DailyQuestRewardBridge.ClaimedAtUnix`. Grants route through the **existing** sinks (§3.4). The `exclusiveCosmetic` is granted via the same cosmetic-ownership writer (`GlimmerCurrencyService.GrantAchievement`-style path) so it lands in `OwnedItemIds`/wardrobe.

### 4.3 The thin interpreter (design only — no code here)

```
// Battle Pass — XP credit (hooked to the EXISTING win ledger, no new combat code)
OnArenaResult(win, streak, perfect):
  xp = win ? season.xp.perWin : season.xp.perLoss
  xp += min(streak * season.xp.perStreakStep, season.xp.streakStepCap * season.xp.perStreakStep)
  if perfect: xp += season.xp.perfectBonus
  if todayXp >= season.xp.dailySoftCap: xp *= season.xp.softCapTaperPct
  Pass.AddXp(xp)                       // crosses tier thresholds → queue earned rewards
  for each newly-crossed tier:
     Grant(tier.free)                  // always
     if Pass.PremiumUnlocked: Grant(tier.premium)
  FlowTrace.Step("Pass", ...)          // §12 instrument every step

OnBuyPremiumPass(season):
  WalletService.Purchase(season.premiumPassSku)   // existing rail
  Pass.PremiumUnlocked = true
  for each already-earned tier: Grant(tier.premium)   // retroactive courtesy
  Save()

// Monthly Card — daily claim (mirrors DailyQuestRewardBridge latch)
OnDailyBoot(card):
  if UtcDayAvailable(card):            // pool: claims remaining; calendar: within expiry
     day = NextUnclaimedDay(card)
     Guard: day.claimedUtc == 0       // double-grant latch
     day.claimedUtc = now
     Grant(day.grant)                 // existing sinks (§3.4)
     Save()
     FlowTrace.Step("MonthlyCard", ...)
```
`Grant(...)` is the same recursive dispatcher the SKR WO defines — it **never switches on a SKU/reward name**; a new cosmetic is a JSON row. Both families converge on **one fulfillment writer** (`OwnedItemIds` + `ResourceBalance`/Glimmer + token tray + `ISkrLedger` + `Save()`).

---

## 5. SAMPLE DATA (delivered)

`Assets/StreamingAssets/Data/Canonical/battle_monthly_packs.sample.json` ships alongside this WO. It contains:
- **`packs[]`** — 4 battle **cosmetic** SKUs + 1 **Season Pass** SKU, all **pure `PackDef`** (drop straight into `packs.json` if approved).
- **`battlePassSeasons[]`** — 1 full season ("Emberwake", 8 tiers shown as a representative slice; a shipping season would have ~30–50 tiers) with free+premium lanes and XP-from-play rules.
- **`monthlyCards[]`** — 2 monthly cards (basic + premium) each with a 30-day `dailyTable`.

All cosmetic ids are **`cosmetic.*` placeholders** — **art is still needed** (arena skins, crown skins, VFX colorways, weapon/armor reskins, monthly-exclusive cosmetics). The ids are stable so art can be authored against them.

### Example SKU inventory (10 concrete entries)
1. `battle-crown-emberforged` (PackDef) — crown skin for `crown_tier1/2/3` + perfect.
2. `battle-arena-ashen-coliseum` (PackDef) — arena ground/ambiance reskin.
3. `battle-vfx-emberglow` (PackDef) — spell-VFX colorway (Knight skills).
4. `battle-armor-emberward` (PackDef) — static weapon+armor cosmetic reskin.
5. `season-01-emberwake-pass` (PackDef) — the Season Pass premium-lane unlock SKU.
6. `season-01-emberwake` (BattlePassSeason) — the 8-tier track, free + premium lanes.
7. `monthly-wayfarer` (MonthlyCard, tier 1) — basic 30-day drip + exclusive cosmetic.
8. `monthly-keeper` (MonthlyCard, tier 2) — premium 30-day drip + SKR line + exclusive cosmetic.
9. (within #6) capstone tier reward — `cosmetic.crown.emberwake-champion` animated crown skin (prestige expression).
10. (within #7/#8) `cosmetic.banner.wayfarer-ledger` / `cosmetic.pet-skin.keeper-emberfox` — month-exclusive cosmetics.

---

## 6. VALIDATION INVARIANTS (regression-gated — the pay-to-win firewall is a build gate)

A `BattleMonthlyRegression` (future WO, mirrors the SKR `SkrStoreRegression`) asserts headlessly:
1. **No `combat`/stat grant anywhere.** Every `Grant.kind ∈ {cosmetic_sku, skr, economy, convenience_token, bundle}`; every `convenience_token.kind ∈ ConvenienceItemDef.Kind` (`instant-build|instant-repair|harvest-auto-collect|xp-weekend`). A revive/heal/damage grant **fails the build.**
2. **Battle XP is never a grantable/purchasable reward** — no `Grant` may credit pass XP; XP comes only from `OnArenaResult`.
3. Every `BattlePassTier.xpRequired` is strictly increasing; `premiumPassSku` resolves to a real `PackDef`.
4. Each `MonthlyCard.dailyTable.length == durationDays`; every `day` 1..N present exactly once.
5. Every `cosmetic_sku` resolves to a real cosmetic id; every `iconId`/cosmetic is a **pointer string only** — no binary inlined (`data-architecture` T1).
6. Monthly daily drips do not exceed an anti-inflation ceiling vs the soft-economy curve (no "buy a card, skip the whole economy").

---

## 7. CROSS-SYSTEM OPEN QUESTIONS (route to owner before IMPLEMENT)

1. **Season length & cadence** — 35 days / ~5 weeks per season, or calendar-month? And how many tiers (sample shows 8; shipping ~30–50)?
2. **Monthly claim model** — **pool** (most generous: missed days never lost, card lives until all claims spent) vs **calendar** (expiry = purchase + 30d, missed days forfeit). Recommend **pool**. Your call sets the non-predatory bar.
3. **Premium-pass currency** — should the Season Pass be buyable with **held SKR** (sibling SKR-store rail) in addition to real-money/SOL/USDC? (Lets earned-SKR players buy in without spending cash — very generous, on-covenant.)
4. **Paid tier-skip?** — do we ever allow a **cosmetic-only** catch-up purchase (buy past tiers' *cosmetics* late in a season) — or **never sell tiers at all** (purest stance)? No version may sell power; this is purely whether to sell cosmetic catch-up.
5. **Perfect/flawless XP** — `BattleArenaHud` notes the no-hit signal isn't tracked yet. Do we ship the pass with `perfectBonus=0` and light it up when the flawless signal lands, or block the pass on that signal first? (Recommend ship with 0, auto-light later.)
6. **Free-lane SKR drips** — do we want the free lane to drip a *little* SKR (very generous, funds the SKR store for non-payers per the covenant "never required to spend") or keep SKR premium-lane-only? (Sample drips a small amount on the free lane.)
7. **Where does pass state live** — PlayerPrefs blob (Glimmer pattern, ships now) vs wait for `GameState`/`SaveSchema` round-trip (cleaner, but `ArenaProgress` itself isn't wired yet — see `ArenaProgressStore` note). Recommend PlayerPrefs-blob now, migrate with the save owner later.
8. **Cosmetic dual-sourcing** — which battle/monthly cosmetics are *also* earnable via Glimmer/achievements (covenant transparency) vs purchase-exclusive expression? Needs an SKU map (shared with the SKR WO Open Q #4).

---

## 8. WHAT NOT TO TOUCH / BUILD (scope guard)

- **Do NOT** edit the live `packs.json` — propose via the sibling sample; the owner merges approved rows.
- **Do NOT** introduce any `combat`/stat/revive/heal grant — §6 inv.1 is a build gate.
- **Do NOT** make Battle XP buyable or grantable — it is earned by playing only (§6 inv.2).
- **Do NOT** add a new currency primitive — reuse Glimmer (cosmetic), `ResourceBalance` (soft), `ISkrLedger` (premium).
- **Do NOT** invent a parallel inventory/claim path — reuse `DailyQuestRewardBridge` grant routes + the `ClaimedAtUnix` latch.
- **Do NOT** auto-pop the pass/card mid-play — discovery is a player-initiated glyph (monetization spec C5).
- **Do NOT** author UI in UXML — code-built Obsidian uGUI only (`ui-blink-template-master-frame-formula`); UXML renders empty in builds.
- **Do NOT** make any reward "claim or lose forever" under time pressure (the monthly card's non-predatory promise).
- **Do NOT** put binary (skins/icons) in the catalog/DB — pointer strings only.

## 9. ACCEPTANCE (for the follow-up IMPLEMENTATION WO, not this one)

- [ ] Battle cosmetic SKUs load as plain `PackDef`s through the existing PackStore (no schema change).
- [ ] `battlePassSeasons[]` + `monthlyCards[]` load via `CanonicalJson.Read`; typed records hydrate (mirror `PackCatalog`).
- [ ] Battle XP credited ONLY from `ArenaProgressStore.RecordWin`/result hook; never from a `Grant`.
- [ ] Buying the Season Pass flips premium-lane + retro-grants earned premium tiers; earned rewards kept at season close.
- [ ] Monthly card grants exclusive cosmetic up-front; daily drip claims via per-day latch through the EXISTING daily grant routes; missed days never lost (pool model).
- [ ] `BattleMonthlyRegression` enforces all §6 invariants headlessly (pay-to-win firewall is a build gate).
- [ ] FlowTrace on every XP-credit / tier-grant / daily-claim step; no silent failure.
- [ ] Zero combat-power surface introduced; zero Solana/wallet required for V1 (SKR via `LocalSkrLedger`).

---

## 10. SOURCES READ (grounding)

- `Assets/_Modules/Wallet/PackCatalog.cs` (`PackDef`/`PackPricing`/`PackContents`/`ConvenienceItemDef`), `Assets/StreamingAssets/Data/Canonical/packs.json`
- `Assets/_Modules/Village/Arena/ArenaProgressStore.cs` (`RecordWin`/`Streak`/`TotalPurse` — the XP source), `BattleArenaHud.cs` + `RpgUiCatalog` (`crown_tier1/2/3`, `CrownPerfect`)
- `Assets/_Modules/Cosmetics/GlimmerCurrencyService.cs` (earned cosmetic currency + ownership writer), `Assets/_Modules/Village/Quests/DailyQuestRewardBridge.cs` (daily grant routes + `ClaimedAtUnix` latch)
- Sibling WOs: `WorkOrders/WORK_ORDER_skr_store_design.md` (SKR rail + `Grant` Table D + `ISkrLedger`), `WORK_ORDER_offline_storage_logic.md`, `WORK_ORDER_economy_store_packs.md`
- `docs/monetization-v2-spec.md` (the bent covenant §2, convenience §5.3, discovery C5, no-gacha C3)
- Memory: `combat-pivot-single-hero-northstar`, `owner-thinks-in-data-structures`, `wardrobe-dressable-capability`, `ui-blink-template-master-frame-formula`, `data-architecture-hybrid-db-direction`
