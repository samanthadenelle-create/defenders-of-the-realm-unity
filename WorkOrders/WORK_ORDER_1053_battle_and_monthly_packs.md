<!-- era-sweep-2026-08-17 -->
> ### ✅ RE-VERIFIED 2026-08-21 (UI seat) — see "RE-VERIFICATION" below. Four things moved; two gate the reward tables.
> ### (superseded banner, kept for history) ⚠ AGED 2026-08-17 — unverified since 2026-06-28
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-28) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK ORDER 1053 — Battle Packs + Monthly Reward Packs (Monetization Family Design)

> **NUMBERED 2026-08-21 (owner instruction).** This ticket had no WO number in its filename, so
> `tools/board_build.py` keyed it as **WO-?** and bucketed it with the unnumbered parked tickets —
> the owner's TOP PRIORITY item was the one thing on the board nobody could cite in a handoff or a
> commit message. Minted **1053** from the UI-seat block (`CLI_LANES_WO_NUMBERS.md`), banner bumped
> 1053 -> 1054 in the SAME edit, file moved with `git mv` so history follows. **Content unchanged.**

**Type:** DESIGN SPEC (ideas + data schema + sample data). **No `.cs` in this WO** — implementation is a follow-up WO.
**Status:** IMPLEMENTED 2026-08-21 - 30-tier season + two monthly cards, firewall enforced at load on three axes. Cosmetic and SKR rows DELIBERATELY UNAUTHORED (no art; no ISkrLedger) with the regression failing the build if either is authored early. Monetization stays OFF.
**Author lane:** Monetization/Backend (§9 parallel lane — fully isolated; no scene/combat files).
**Assigned:** **CLI seat implements.** The DESIGN pass is DONE — authored by the **UI seat (Claude UI)**
2026-08-21: the re-verification (§V), the four owner rulings (§R) and the two missing screens (§U).
UI writes no `.cs` (CLAUDE.md §2); everything below the design sections is CLI's to build.
**Design artifact:** https://claude.ai/code/artifact/c097fe70-a82d-4b45-a48d-6ab529a7e21f
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

---

# RE-VERIFICATION 2026-08-21 (UI seat) — the aged banner asked for this before pulling

The `<!-- era-sweep-2026-08-17 -->` banner says this WO is READY but unverified since 2026-06-28 and
must be re-verified before it is pulled. Done, at source, this session. **The design is sound and the
grounding in §1 is almost entirely still true — but FOUR things moved underneath it, and two of them
change what this can ship as.**

## V1. CONFIRMED STILL TRUE

| Claim in §1 | Verified |
|---|---|
| `PackDef` schema, battle SKUs need no new type | ✅ `PackCatalog.cs` — `sku/tier/pricing/contents/storeVisible/legacySkus` intact |
| Arena W/L ledger is the XP source | ✅ `ArenaProgressStore.RecordWin(purse)` at `:42`, mutating `Wins`/`Streak`/`TotalPurse`, `FlowTrace.Step` at `:51` |
| ...and it is actually wired | ✅ **one** live call site: `ArenaMode.cs:386`. `RecordLoss` exists for the participation trickle |
| Daily grant routes + double-grant latch | ✅ `DailyQuestRewardBridge.cs` present |
| Earned cosmetic currency + ownership writer | ✅ `GlimmerCurrencyService.cs` present |
| Sample data shipped | ✅ `Assets/StreamingAssets/Data/Canonical/battle_monthly_packs.sample.json` on disk |
| Live `packs.json` untouched by this WO | ✅ |

**So the spine holds.** The XP source is real, wired and already instrumented — this is genuinely
"data + a thin interpreter", as §1 claims.

## V2. ⛔ FOUR THINGS MOVED — read before authoring a single reward row

### (1) GLIMMER WAS STRIPPED FROM EVERY PACK — owner ruling, today

`packs.json` `_comment`, WO-1121 ruling (5), verbatim: *"remove all glimmer from packs as its nothing
real and money has never been active"* — *"its only sink is cosmetics, and CosmeticApplier is called
from nowhere, so an equipped cosmetic changes a flag and nothing visible."* Pinned by
`BuyGateAndPriceLadderRegression` (`:220-235`) — **a pack carrying a `glimmer` key now FAILS the
build.**

This WO uses glimmer as a **free-lane grant sink** (§2.1, §3.1, §4). A glimmer line in a pass tier is
the same product decision the owner just reversed one file over. **The rule is about pack CONTENTS,
and a pass tier is pack contents by another name.**

⚠ Note the asymmetry, because it matters: **glimmer the CURRENCY is untouched** — still earned
(`TierSystem` / `Enemy` / `DailyQuestRewardBridge`) and still spent (`GlimmerCurrencyService` /
`BattlePassManager`). What was removed is glimmer as a **paid reward line**. The free lane may still
*route through* glimmer's sinks; it should not *advertise glimmer as a prize*.

### (2) THE COSMETIC RENDER SEAM IS LANDING RIGHT NOW — this is the load-bearing dependency

The ruling above rests on *"CosmeticApplier is called from nowhere."* **That is no longer true in the
working tree.** Verified this session:

```
HeroBodySwapper.cs:1058   DeNelle.Cosmetics.CosmeticApplier.Attach(...)
HeroArmorVisual.cs:371    DeNelle.Cosmetics.CosmeticApplier.RefreshOn(gameObject);
HeroArmorVisual.cs:889    DeNelle.Cosmetics.CosmeticApplier.RefreshOn(gameObject);
```

and `CosmeticApplyRegression.cs` now *asserts* both seams exist. `CosmeticApplier.cs` is **modified
and uncommitted** — another seat is fixing this as we speak.

**This is the single most important fact for this WO.** Both families pay out primarily in
cosmetics: the entire premium lane, every capstone, every month-exclusive. The owner's premise was
correct when she ruled, and it is being repaired underneath us.

⛔ **Do not author a cosmetic-heavy reward table until that seam is COMMITTED and gate-green.** A
battle pass whose premium lane grants invisible flags is precisely the vapor WO-1118 exists to
refuse, and it would be worse than a pack — a pack disappoints once, a season disappoints for
35 days.

### (3) `ISkrLedger` DOES NOT EXIST — and neither does `LocalSkrLedger`

§4.3 and §9 both spend `ISkrLedger.Credit`, and §9 promises *"zero Solana/wallet required for V1 (SKR
via `LocalSkrLedger`)"*. Grepped the whole tree: **the only occurrence of the name is a doc comment**
in `IPiPlatform.cs:8` describing the *pattern*. There is no interface, no local implementation, no
writer.

What DOES exist: `skr_store.json` (a data catalogue with `costSkr` rows), `CurrencyKind.Skr` on the
wallet rail, and `FeatureFlags.SkrPreview` (**`defaultOn: false`**).

**So every `skr` Grant in this design currently has nowhere to land.** That is a prerequisite ticket,
not a footnote — and §9's acceptance line asserting V1 works via `LocalSkrLedger` cannot be met until
someone writes it.

### (4) THE PRICE CEILING MOVED — $4.99 -> $49.99

WO-1121 owner ruling (3), recorded in `packs.json`: *"THE PRICE CEILING IS $49.99, NOT $4.99 — the
$4.99 cap was an EARLY-ACCESS constraint, not a permanent one."* This is **good news** for this WO:
a Season Pass and a monthly card can be priced on the full `monetization-v2-spec` §4 ladder instead
of being squeezed under a $5 cap. The SKR peg is authored: $9.99 -> 120 SKR, $19.99 -> 240,
$49.99 -> 600.

Note the surviving exception: **the $5 ceiling still binds the IMPULSE family specifically**, pinned
by `ImpulsePackRegression`. It does not bind a pass.

## V3. THE ORDER THIS FORCES — you cannot ship a rewards program whose rewards do not exist

The firewall in §0 is about what a pack may **not** grant. V2 exposes the mirror problem: what it
**can** grant, today, and actually deliver.

| Gate | Blocks | Status |
|---|---|---|
| **G1** cosmetic render seam committed + gate-green | every cosmetic reward, both families | **in flight, uncommitted** |
| **G2** an SKR writer exists | every `skr` Grant, the free-lane drip, the premium lane's SKR | **not started — no ticket** |
| **G3** `RealmStorePurchase` on + mainnet block lifted | *selling* the pass or the card | **defaultOn:false**, block unlifted (WO-1121) |

**G1 and G2 do not block AUTHORING and do not block the UI.** They block the reward tables being
honest. So the sane sequence is:

1. **Now:** the two screens (§Y below), the season/card data shapes, the regression gate from §6, and
   the XP interpreter — all of which are testable with resource-only rewards.
2. **On G1:** re-cut the premium lane to the cosmetics that now visibly render.
3. **On G2:** light up the SKR lines.
4. **On G3:** the pass becomes buyable.

**The alternative, if the owner wants it sooner:** re-cut both reward tables to what is grantable
*today* — wood / iron / food / crystals / coins through `EconomyService.GrantSpendable`, plus the
**one** convenience kind with a live redeemer (`lantern-oil-2x-expedition`; see
`PackCatalog.IsRedeemableConvenience`). That is a less glamorous pass, and it is an honest one. It is
the WO-1118 honest-shelf rule applied to a season.

## V4. Open questions — resolved where a default is obvious, escalated where it is not

| # | Question | Resolution |
|---:|---|---|
| 2 | Monthly claim model | **POOL.** Take the WO's own recommendation. Missed days roll into the pool; the card lives until all `durationDays` claims are spent. It is the only model that keeps the §3.2 non-predatory promise literally true. |
| 5 | Perfect/flawless XP | **Ship with `perfectBonus = 0`,** auto-light when the no-hit signal lands. Do not block a season on a signal that does not exist. |
| 7 | Where pass state lives | **Implementation call, CLI's.** Note only: save schema is at **v38** and actively versioned (`SaveSchema.CurrentVersion`), so the "wait for GameState" option is cheaper than it was in June. |
| 8 | Cosmetic dual-sourcing | **Downstream of G1.** Cannot be answered before we know which cosmetics render. Park it. |
| 1, 3, 4, 6 | Season length/tiers · SKR-buyable pass · paid cosmetic tier-skip · free-lane SKR drips | **GENUINE OWNER CALLS — raised directly, not buried here.** |

---

# OWNER RULINGS 2026-08-21 (this session) — four open questions CLOSED

| # | Question | **RULING** |
|---:|---|---|
| 1 | Season length & cadence | **CALENDAR MONTH, ~30 tiers.** Seasons start on the 1st and run the length of the month (28-31 days). |
| 3 | Premium-pass currency | **THE SEASON PASS IS BUYABLE WITH EARNED SKR**, in addition to real-money / SOL / USDC. |
| 4 | Paid tier-skip | **NEVER SELL TIERS.** No cosmetic catch-up, no XP purchase, nothing. Buying the pass unlocks the *lane*; the *tiers* are earned by playing, full stop. |
| 6 | Free-lane SKR drips | **NOT TAKEN.** The free lane does **not** drip SKR. |
| — | Sequencing (raised this session) | **DESIGN NOW, REWARD TABLES LATER.** Screens, data shapes, XP interpreter and the §6 regression gate are built now against resource-only rewards; cosmetic and SKR reward lines are authored once G1/G2 land (§V3). |

Questions 2, 5, 7, 8 keep the defaults recorded in §V4 (pool claim model; `perfectBonus = 0` at
launch; state location is CLI's implementation call; dual-sourcing parked behind G1).

## R1. What ruling 1 buys us — the season and the card become ONE rhythm

Calendar-month seasons land the pass on the **same cadence as the monthly card**. That is worth
building on deliberately:

- One date to communicate. *"A new season and a new ledger, on the 1st."*
- The monthly card's `durationDays: 30` and the season's length now describe **the same window**, so
  a player who buys both is on one clock instead of two drifting ones.
- ⚠ **It does NOT mean merging them.** They stay separate SKUs with separate value propositions —
  the pass is *earned by playing*, the card is *a daily drip for showing up*. Selling them as one
  thing would make the pass look purchasable, which ruling 4 exists to prevent.

⚠ **Cost of the ruling, recorded honestly:** a 28-day February and a 31-day March award the same
~30 tiers over different windows, so the required XP-per-day drifts by ~10%. Either scale
`xpRequired` to the month's actual length, or accept that short months are slightly tighter. **Scale
it** — it is a one-line derivation from `lengthDays` and it keeps every month equally completable.

## R2. What ruling 3 requires — and the gap it leaves

`premiumPassSku` must accept **`CurrencyKind.Skr`** alongside the cash rails. The wallet rail already
carries `Skr` and `skr_store.json` already prices in it, so this is a rail selection, not a new
system.

⛔ **But it still needs the G2 writer.** A pass bought with SKR must *debit* a balance, and there is
no ledger to debit — the same missing piece §V2(3) names for crediting. **Ruling 3 promotes the SKR
ledger from "nice to have" to a hard prerequisite of the premium lane.**

⚠ **An honest tension, flagged rather than silently resolved:** with ruling 6 not taken, the free
lane drips no SKR — so a pure free-to-play player has **no path to earn the SKR** that ruling 3 lets
them spend. The two rulings are only complementary if SKR is earned *somewhere else* in the game.
Today it is not: `FeatureFlags.SkrPreview` is `defaultOn: false` and no earn path is wired. **This is
not a contradiction to fix by guessing** — it is a question for when the SKR ledger is specced:
*where does a non-paying player get SKR?* Recorded here so it is not lost.

## R3. What ruling 4 simplifies

"Never sell tiers" removes an entire class of surface area:

- **No catch-up SKU**, no partial-season pricing, no "unlock past cosmetics" flow, no pro-rating.
- The §6 validator gets **stricter and simpler**: invariant 2 already forbids a `Grant` crediting
  pass XP; ruling 4 extends it to forbid **any SKU that references a tier index**. A pack row naming
  a tier is now a build failure.
- The UI loses a whole state — no "buy the tiers you missed" affordance anywhere on the track. **The
  only purchasable object on the entire pass screen is the lane unlock.**
- It makes the pass trivially defensible in a store listing: *the track is earned by playing.*

---

# THE MISSING HALF — the UI (added 2026-08-21, UI seat)

The WO specifies data, grants, firewall and validation, and says nothing about what the player looks
at. Two screens are needed. Both are **code-built Obsidian uGUI** (UXML renders empty in builds),
landscape, and both obey `MinTouchPx = 112`.

**Interactive wireframes:** see the artifact linked in the session hand-off; geometry below is
authoritative.

## U1. The Season Track screen

Canvas **2670 x 1200**. The problem this screen solves is that ~30 tiers do not fit on one landscape
screen and must not become a wall of identical cells.

| Zone | Size | Holds |
|---|---:|---|
| Header | full width x **120** | season name, days remaining, current tier, XP-to-next bar |
| **Track** | full width x **660** | the horizontally-scrolling tier rail — the screen's spine |
| Lane labels | **200** wide, inside the track | two stacked row labels: FREE / PREMIUM |
| Footer | full width x **420** | the lane-unlock CTA (the *only* purchasable thing) + the earn-rate line |

**The track is two parallel rows, not a grid.** Free rewards on the top row, premium on the bottom,
one column per tier, scrolling horizontally with the current tier auto-centred on open. A column is
**184 wide x 300 tall** including both rows — comfortably above the touch floor.

**Four column states**, each carrying a **word or a shape**, never colour alone (owner is red/green
colourblind): `EARNED` (claimed), `READY` (earned, unclaimed — the only state that animates),
`LOCKED` (not yet reached), `PREMIUM-LOCKED` (earned but the lane is not owned — shows the reward
plainly, with a small lock glyph, so the player sees exactly what they would get).

⛔ **`PREMIUM-LOCKED` must show the reward, not hide it.** Concealing it turns the track into a
mystery box, and §8 forbids gacha framing. Showing it is also the honest sell.

## U2. The Monthly Ledger screen

Canvas **2670 x 1200**. A calendar, because the thing being described is a calendar.

| Zone | Size | Holds |
|---|---:|---|
| Header | full width x **120** | card name, claims remaining (**pool model** — not a date) |
| Grid | **1700** wide | the 30-day table, **10 columns x 3 rows**, cell **158 x 158** |
| Side panel | **840** wide | today's claim, the up-front exclusive cosmetic, the full-value promise |
| Footer | full width x **150** | the claim CTA |

**Every one of the 30 days is visible at once, pre-purchase.** That is §3.2's "full table shown"
promise made structural — you cannot hide a day in a layout that draws all of them.

**Cell states:** `CLAIMED` (a mark), `TODAY` (the only animated cell), `AVAILABLE` (pool model —
missed days stay claimable, and this is the state that proves it), `UPCOMING`.

⛔ **No countdown timer anywhere on this screen.** Under the pool model nothing expires, so a ticking
clock would be a lie that manufactures urgency — exactly the pressure §3.2 promises not to apply.
The header says *"12 claims left"*, never *"expires in 4d 06h"*.

## U3. Rules binding both screens

- **Palette:** the four lights shared with WO-1050 and WO-1133 — gold 195 / verdant 177 / ember 145 /
  aether 113 (rec.709 luminance of 255), on violet-biased near-black. **Free-lane surfaces take
  verdant**, matching the Night Market's free band, so "this costs nothing" reads identically across
  all three screens.
- **Greyscale is the gate.** Strip hue and every state must still be readable from its word or glyph.
- **One animated element per screen, maximum** — the `READY` tier and the `TODAY` cell. Motion means
  *"this is claimable now"* and means nothing else.
- **Never auto-pop mid-play** (§8, discovery rule C5). Both screens are opened by the player.
- **Strings** to `canon-strings.json`, both copies, ASCII-only, flat camelCase per the file's
  existing convention.

## 10. SOURCES READ (grounding)

- `Assets/_Modules/Wallet/PackCatalog.cs` (`PackDef`/`PackPricing`/`PackContents`/`ConvenienceItemDef`), `Assets/StreamingAssets/Data/Canonical/packs.json`
- `Assets/_Modules/Village/Arena/ArenaProgressStore.cs` (`RecordWin`/`Streak`/`TotalPurse` — the XP source), `BattleArenaHud.cs` + `RpgUiCatalog` (`crown_tier1/2/3`, `CrownPerfect`)
- `Assets/_Modules/Cosmetics/GlimmerCurrencyService.cs` (earned cosmetic currency + ownership writer), `Assets/_Modules/Village/Quests/DailyQuestRewardBridge.cs` (daily grant routes + `ClaimedAtUnix` latch)
- Sibling WOs: `WorkOrders/WORK_ORDER_skr_store_design.md` (SKR rail + `Grant` Table D + `ISkrLedger`), `WORK_ORDER_offline_storage_logic.md`, `WORK_ORDER_economy_store_packs.md`
- `docs/monetization-v2-spec.md` (the bent covenant §2, convenience §5.3, discovery C5, no-gacha C3)
- Memory: `combat-pivot-single-hero-northstar`, `owner-thinks-in-data-structures`, `wardrobe-dressable-capability`, `ui-blink-template-master-frame-formula`, `data-architecture-hybrid-db-direction`

> **OWNER RULING 2026-08-21 (verbal, this session):** Owner: these need to be finished. Lifted out of DRAFT/owner-review. Blocking reality to carry into the work: RealmStorePurchase is defaultOn:false (FeatureFlags.cs:659) and the mainnet block at :651 is unlifted, so packs can be BUILT and authored but cannot reach players yet.

> **OWNER RULING 2026-08-21 (verbal, this session):** Owner: "that's what I want done now." Reality to carry in: RealmStorePurchase is defaultOn:false (FeatureFlags.cs:659) and the mainnet block at :651 is unlifted, so packs can be authored and built but cannot reach players until that gate moves.


---

## RETIRED DUPLICATE - BattleMonthlyPanels.cs (superseded 2026-08-21)

Two seats independently built the Season Track and Monthly Ledger screens without being able to see
each other, and this 145-line static wrapper was the losing half of that collision: it was the only
one actually WIRED (it registered `PanelId.BattlePass` and `PanelId.MonthlyLedger` with `PanelRouter`),
but it typed player-facing sentences inline (`"PLAY ARENA BATTLES TO EARN TIERS"`, `"CLAIMS LEFT"`,
`"CLAIM TODAY"`, `"No monthly ledger is available right now."`) and derived on-screen state words from
`enum.ToString()`, which puts a developer identifier such as `PremiumLocked` in front of a player -
both of which CLAUDE.md section 7 forbids.

It was deleted in favour of `SeasonTrackPanel.cs` + `MonthlyLedgerPanel.cs`, whose registration gap was
closed by `BattleMonthlyPanelsBootstrap.cs` (the router door) plus a `PanelHandle` in each panel (the
lifecycle discipline, which is the one thing this file got right). It was **uncommitted work with no
commit to fall back to**, so its full original text is preserved verbatim below and nothing is
unrecoverable.

```csharp
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.UI;

namespace DeNelle.Wallet
{
    /// <summary>Player-initiated, code-built views for the season track and monthly claim pool.</summary>
    public static class BattleMonthlyPanels
    {
        private static ElarionUiKit.ObsidianModal _modal;
        private static PanelHandle _handle;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            PanelRouter.Register(PanelId.BattlePass, OpenBattlePass);
            PanelRouter.Register(PanelId.MonthlyLedger, OpenMonthlyLedger);
        }

        public static void OpenBattlePass()
        {
            Close();
            var season = BattlePassService.Season;
            _handle = PanelManager.Register("Season Track", Close, IsOpen);
            _modal = ElarionUiKit.BuildObsidianModal("BattlePassUI", season?.Name ?? "Season Track",
                new Vector2(0.035f, 0.06f), new Vector2(0.965f, 0.94f), Close, 31030);
            Transform body = Body();
            if (season == null)
            {
                ElarionUiKit.Label(body, "No season is available right now.", 0.35f, 0.65f,
                    ElarionUi.Parchment, 30, TextAlignmentOptions.Center, 0.08f, 0.92f);
                FinishOpen(); return;
            }

            string next = BattlePassService.NextTier == null ? "CAPSTONE REACHED" :
                (BattlePassService.XpFor(BattlePassService.NextTier) - BattlePassService.Xp) + " XP TO NEXT";
            ElarionUiKit.Label(body, BattlePassService.DaysRemaining + " DAYS LEFT  |  TIER " +
                BattlePassService.HighestTierReached + "/" + BattlePassService.TierCount + "  |  " + next,
                0.89f, 0.98f, ElarionUi.Parchment, 22, TextAlignmentOptions.Center, 0.03f, 0.97f);

            var viewport = MakeViewport(body, new Vector2(0.08f, 0.25f), new Vector2(0.98f, 0.87f), out var content);
            float cell = 184f, width = Mathf.Max(viewport.rect.width, season.Tiers.Count * cell);
            content.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 560f);
            for (int i = 0; i < season.Tiers.Count; i++) BuildTier(content, season.Tiers[i], i, cell);

            ElarionUiKit.Label(body, "FREE", 0.61f, 0.80f, new Color(0.75f, 0.95f, 0.85f), 20,
                TextAlignmentOptions.Center, 0f, 0.075f);
            ElarionUiKit.Label(body, "PREMIUM", 0.31f, 0.49f, ElarionUi.Gold, 20,
                TextAlignmentOptions.Center, 0f, 0.075f);
            ElarionUiKit.Label(body, "PLAY ARENA BATTLES TO EARN TIERS. TIERS ARE NEVER SOLD.", 0.10f, 0.20f,
                ElarionUi.ParchmentDim, 19, TextAlignmentOptions.Center, 0.05f, 0.95f);
            if (BattlePassService.HasClaimable)
                ElarionUiKit.BuildObsidianButton(body, "CLAIM READY REWARDS",
                    ElarionUiKit.ObsidianButtonStyle.Style1, ElarionUiKit.ObsidianButtonColor.Yellow,
                    new Vector2(0.34f, 0.02f), new Vector2(0.66f, 0.14f), () => { BattlePassService.ClaimAllReady(); OpenBattlePass(); });
            FinishOpen();
        }

        public static void OpenMonthlyLedger()
        {
            Close();
            var cards = MonthlyCardService.Cards;
            var card = cards != null && cards.Count > 0 ? cards[0] : null;
            _handle = PanelManager.Register("Monthly Ledger", Close, IsOpen);
            _modal = ElarionUiKit.BuildObsidianModal("MonthlyLedgerUI", card?.Name ?? "Monthly Ledger",
                new Vector2(0.035f, 0.06f), new Vector2(0.965f, 0.94f), Close, 31030);
            Transform body = Body();
            if (card == null)
            {
                ElarionUiKit.Label(body, "No monthly ledger is available right now.", 0.35f, 0.65f,
                    ElarionUi.Parchment, 30, TextAlignmentOptions.Center, 0.08f, 0.92f);
                FinishOpen(); return;
            }
            ElarionUiKit.Label(body, MonthlyCardService.ClaimsRemaining(card.Sku) + " CLAIMS LEFT  |  MISSED DAYS NEVER EXPIRE",
                0.90f, 0.98f, ElarionUi.Parchment, 22, TextAlignmentOptions.Center, 0.03f, 0.97f);
            for (int i = 0; i < card.DailyTable.Count && i < 30; i++) BuildDay(body, card, card.DailyTable[i], i);
            string today = card.Day(MonthlyCardService.NextDay(card.Sku))?.Grant?.Describe() ?? "No claim available";
            ElarionUiKit.Label(body, "TODAY\n" + today + "\n\nThe full reward table is shown. Your free daily rewards are unchanged.",
                0.32f, 0.82f, ElarionUi.Parchment, 23, TextAlignmentOptions.TopLeft, 0.69f, 0.97f);
            var label = MonthlyCardService.CanClaimToday(card.Sku) ? "CLAIM TODAY" : "NO CLAIM READY";
            var btn = ElarionUiKit.BuildObsidianButton(body, label,
                ElarionUiKit.ObsidianButtonStyle.Style1, ElarionUiKit.ObsidianButtonColor.Yellow,
                new Vector2(0.70f, 0.12f), new Vector2(0.96f, 0.27f), () => { MonthlyCardService.Claim(card.Sku); OpenMonthlyLedger(); });
            btn.interactable = MonthlyCardService.CanClaimToday(card.Sku);
            FinishOpen();
        }

        private static void BuildTier(RectTransform parent, BattlePassTier tier, int index, float width)
        {
            if (tier == null) return;
            float x = index * width;
            BuildCell(parent, "T" + tier.Tier + "  " + Word(BattlePassService.FreeState(tier)) + "\n" + (tier.Free?.Describe() ?? "-"), x, 285f, width - 8f, 255f,
                BattlePassService.FreeState(tier) == TierState.Ready ? new Color(0.12f, 0.35f, 0.25f, 0.95f) : new Color(0.08f, 0.08f, 0.11f, 0.95f));
            BuildCell(parent, "T" + tier.Tier + "  " + Word(BattlePassService.PremiumState(tier)) + "\n" + (tier.Premium?.Describe() ?? "-"), x, 10f, width - 8f, 255f,
                new Color(0.20f, 0.15f, 0.05f, 0.95f));
        }

        private static void BuildDay(Transform body, MonthlyCard card, MonthlyDailyDrip drip, int index)
        {
            if (drip == null) return;
            int col = index % 10, row = index / 10;
            float x0 = 0.02f + col * 0.065f, x1 = x0 + 0.059f;
            float y1 = 0.84f - row * 0.245f, y0 = y1 - 0.215f;
            var state = MonthlyCardService.DayState(card.Sku, drip.Day);
            var plate = new GameObject("Day" + drip.Day, typeof(RectTransform), typeof(Image));
            plate.transform.SetParent(body, false);
            var rt = (RectTransform)plate.transform; rt.anchorMin = new Vector2(x0, y0); rt.anchorMax = new Vector2(x1, y1);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            plate.GetComponent<Image>().color = state == MonthlyDayState.Today ? new Color(0.12f, 0.35f, 0.25f, 0.95f) : new Color(0.08f, 0.08f, 0.11f, 0.95f);
            ElarionUiKit.Label(plate.transform, "DAY " + drip.Day + "\n" + state.ToString().ToUpperInvariant() + "\n" + drip.Grant.Describe(),
                0.05f, 0.95f, ElarionUi.Parchment, 13, TextAlignmentOptions.Center, 0.04f, 0.96f);
        }

        private static void BuildCell(RectTransform parent, string text, float x, float y, float w, float h, Color color)
        {
            var go = new GameObject("TierCell", typeof(RectTransform), typeof(Image)); go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform; rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = Vector2.zero; rt.anchoredPosition = new Vector2(x, y); rt.sizeDelta = new Vector2(w, h);
            go.GetComponent<Image>().color = color;
            ElarionUiKit.Label(go.transform, text, 0.05f, 0.95f, ElarionUi.Parchment, 16, TextAlignmentOptions.Center, 0.05f, 0.95f);
        }

        private static RectTransform MakeViewport(Transform parent, Vector2 min, Vector2 max, out RectTransform content)
        {
            var go = new GameObject("TrackViewport", typeof(RectTransform), typeof(Image), typeof(Mask), typeof(ScrollRect));
            go.transform.SetParent(parent, false); var rt = (RectTransform)go.transform;
            rt.anchorMin = min; rt.anchorMax = max; rt.offsetMin = rt.offsetMax = Vector2.zero;
            go.GetComponent<Image>().color = new Color(0.02f, 0.015f, 0.03f, 0.96f); go.GetComponent<Mask>().showMaskGraphic = true;
            var cg = new GameObject("Content", typeof(RectTransform)); cg.transform.SetParent(go.transform, false); content = (RectTransform)cg.transform;
            content.anchorMin = new Vector2(0f, 0f); content.anchorMax = new Vector2(0f, 1f); content.pivot = new Vector2(0f, 0.5f);
            var scroll = go.GetComponent<ScrollRect>(); scroll.viewport = rt; scroll.content = content; scroll.horizontal = true; scroll.vertical = false;
            return rt;
        }

        private static string Word(TierState state) => state == TierState.PremiumLocked ? "PREMIUM-LOCKED" : state.ToString().ToUpperInvariant();
        private static Transform Body() => _modal.chrome.layout != null && _modal.chrome.layout.body != null ? _modal.chrome.layout.body : _modal.chrome.content.transform;
        private static void FinishOpen() { if (!PanelManager.NotifyOpened(_handle)) Close(); }
        private static bool IsOpen() => _modal != null && _modal.canvas != null && _modal.canvas.activeInHierarchy;
        private static void Close() { PanelManager.NotifyClosed(_handle); if (_modal?.canvas != null) UnityEngine.Object.Destroy(_modal.canvas); _modal = null; }
    }
}
```
