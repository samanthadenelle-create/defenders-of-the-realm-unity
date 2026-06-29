# Live-Ops & Retention — the Post-Launch Engagement Engine

**Status:** DESIGN (ideas + data model + covenant compliance). **No `.cs` written.**
**Type:** Cross-cutting meta-system design — *assembles* shipped systems (daily quests,
offline harvest, arena ledger, pack store, SKR yield); does **not** greenfield.
**Silo:** Monetization / Live-Ops meta (CLAUDE.md §9 isolated lane — no scene/combat files).
**Author:** design pass, 2026-06-28.
**Scope discipline:** this doc is the *spine that ties the engagement loops together*. Every
monetization SKU, pack, pass, and yield-stream is owned by the WOs cited in §0 — **referenced,
never duplicated.** This doc owns the *retention cadence + the covenant rails around it*.

---

## 0. SOURCES OF TRUTH (read these — this doc orchestrates them, never replaces)

| Concern | Owning doc / file | What it owns (do NOT re-spec here) |
|---|---|---|
| **The covenant** (the law all of this obeys) | `docs/monetization-v2-spec.md` §2 (C1–C7, bent-C1) | The 7 constraints; "never required to spend; can't buy victory, only time and beauty"; convenience-power line; the §11.1 rip-out valve. |
| **Brand monetization thesis** | `docs/BRAND_AND_PLATFORM_CANON.md` | Fun-first; Arena = cash cow; rewarded-video (session + return drivers); **sell TIME never POWER**; don't-sell-Echoes rail. |
| **Battle Pass + Monthly Card** (the season + the card) | `WorkOrders/WORK_ORDER_battle_and_monthly_packs.md` | Seasonal battle pass (XP-from-arena, free+premium lanes), monthly card daily-drip, the pay-to-win firewall, `BattleMonthlyRegression`. |
| **Offline-return economy** | `WorkOrders/WORK_ORDER_offline_storage_logic.md` | Silo/window caps (hours), storage-tier ladder, welcome-back popup, the exact offline-accrual formula. |
| **Economy store packs + weekly deal** | `WorkOrders/WORK_ORDER_economy_store_packs.md` | Resource/boost/storage packs, the rotating "weekly deal" slot, bundles, `category` tab. |
| **SKR yield rewards** (contests) | `docs/monetization-v2-spec.md` §12 | Stream A achievement drops, Stream B "Watcher's Roll" weekly leaderboard, Stream C seasonal tournament, anti-gaming, the legal gate. |
| **Daily-quest engine** (shipped) | `Assets/_Modules/Core/Quests/DailyQuests.cs` + `DailyQuestRewardBridge.cs` | 3-slot daily roll, no-FOMO reset, reroll, `QuestCompleted` event, the daily grant routes + `ClaimedAtUnix` latch. |
| **Operator backend** (server half) | `docs/roadmap/live-ops-scope.md` | Admin portal, server-authoritative entitlements, pack-deploy, competitions, payout signer. **Live-ops *content* needs this backend to be authoritative — see §10.** |

**Net:** the engagement engine is *mostly data + a thin scheduler/claim interpreter over systems
that already exist.* New surface = a cadence config, an event/login-streak ledger, and one
re-engagement notification hook. **No new currency, no new combat surface, no new store.**

---

## 1. THE COVENANT RAIL (the retention firewall — binding on every loop below)

Retention design is where free-to-play games go predatory. Every loop in this doc passes the
**covenant gate** or it does not ship. Inherited verbatim from `monetization-v2-spec.md` §2:

| Rule | Live-ops application |
|---|---|
| **C1 (bent)** — sell time + beauty, never power | Loops may award **cosmetics, soft currency, convenience tokens, SKR**. **Never** combat stats / revives / heals / permanent passives. A "comeback bonus" is resources + a cosmetic, never a damage buff. |
| **C2** — never required to spend | Every event reward, season tier, and daily reward is **earnable by playing**. Paid lanes buy *better cosmetics + convenience*, never the only path. |
| **C3** — no loot boxes / gacha / randomized spend | Login rewards, event rewards, season tiers are **shown up front**. A daily quest *rolls* (free gameplay variety) but its **reward is fixed per slot** — no paid mystery box, ever. |
| **C4** — no energy gates, no FOMO countdowns, no dark patterns | **The hard one for live-ops (see §6).** Time-boxed *content* is allowed; **expiring entitlements, "buy or lose forever", login-streak punishment, and pay-to-skip-a-timer are NOT.** Earned cosmetics are kept forever. |
| **C5** — no gameplay interruption | Daily/season/event UI is **player-initiated** (a glyph), never an auto-popup mid-play. The welcome-back popup is the *one* sanctioned reveal — and it's a reveal of an already-banked grant, not a transaction. |
| **C6** — generosity over extraction | Gifting wraps packs; the free lane of every season/card is genuinely rewarding; caps are generous (overnight/weekend). |
| **C7** — cozy tone | Event copy is narrative-bible voice ("The Folk gather for the Emberwake"), never "LAST CHANCE — 02:59:59". |

### 1.1 Reconciling Decision-4 ("no daily/weekly *deals*") with daily/weekly *loops*

`monetization-v2-spec.md` §1 Decision-4 says **"No daily/weekly deals."** That is a **store**
constraint (no rotating price pressure that manufactures urgency). It does **not** forbid
**daily/weekly gameplay LOOPS** — daily quests already ship (`DailyQuests.cs`). The line:

> **Daily/weekly LOOPS = gameplay cadence (quests, login rewards, contests) — ALLOWED & encouraged.**
> **Daily/weekly DEALS = store price urgency — constrained.** The economy-WO "weekly deal" is a
> *rotating spotlight on an always-available pack at an honest discount*, **with no expiring
> entitlement and no FOMO countdown framing** — a *curation* convenience, not a "buy now or lose it".
> When in doubt, the loop awards *gameplay value the player earned*, the store never pressures.

This reconciliation is the single most important covenant call in the doc; every §below honors it.

---

## 2. THE RETENTION MODEL — three vectors × four cadences (data-driven)

Owner thinks in data structures (`owner-thinks-in-data-structures`): the engine is a **cadence
table** a thin scheduler interprets, not a tree of hardcoded events. Two axes:

**Vectors (the *why-they-return* lever):**
- **SESSION** — what makes *this* play session longer / richer (do more now).
- **RETURN** — what makes them *come back tomorrow* (a reason to log in again).
- **RE-ENGAGEMENT** — what wins back a *lapsed* player (3+ days gone).

**Cadences (the *clock*):**
- **DAILY** · **WEEKLY** · **SEASONAL (≈5-week)** · **LIFECYCLE (one-shot / milestone)**.

```
                SESSION driver            RETURN driver               RE-ENGAGEMENT driver
DAILY      daily quests (×3)         login reward calendar        "your realm missed you" notif
           rewarded 2× boost         daily card drip (paid)       comeback day-1 grant
WEEKLY     Watcher's Roll standings  weekly quest meta-goal        lapsed-player win-back bonus
           (chase the board)         weekly deal spotlight
SEASONAL   battle-pass tier climb    season cosmetic chase         "new season started" notif
           (arena XP)                capstone prestige cosmetic
LIFECYCLE  achievement SKR drops     storage-tier "hold more"      Founder window (launch only)
           milestone Echo unlocks    offline-window growth
```

Every cell is an **existing or sibling-WO system**. Live-ops' job = **schedule them, tie their
rewards into one fulfillment writer, and instrument the funnel** (§9). The matrix IS the spec.

---

## 3. THE DAILY LOOP (session + return)

### 3.1 Daily quests — SHIPPED, reuse verbatim
`DailyQuests.cs` already rolls 3 slots (combat / exploration / wildcard), resets per local-date
**with no streak guilt** (the code comment: *"no streak guilt, no FOMO, matching the spec"* — this
is C4-clean by construction), supports 1 free reroll + paid rerolls (crystals, capped), and fires
`QuestCompleted` → `DailyQuestRewardBridge` dispenses the slot reward. **Live-ops adds nothing
here except surfacing it in the daily hub (§3.4).** Do not re-implement.

### 3.2 Login reward calendar (NEW — small, covenant-safe)
A **non-punishing** daily-login reward: a fixed `loginCalendar[]` of ~28 entries (cosmetic
shards, soft currency, the occasional convenience token, a milestone cosmetic on day 7/14/28).

**Covenant rails (binding):**
- **No streak *punishment*.** Missing a day does **not** reset to day 1 (that is the classic dark
  pattern). The calendar is a **pool/odometer**: each distinct UTC login advances the pointer by
  one — you always collect entry N on your Nth login, whenever those logins happen. (Mirrors the
  monthly-card **pool model**, `WORK_ORDER_battle_and_monthly_packs.md` §3.3 — recommended there
  for the exact same non-predatory reason.)
- A **separate, optional** "consecutive-day flourish" may award a *tiny bonus* for back-to-back
  logins, but **the base calendar never regresses** and the flourish is additive (C6 generosity,
  never penalty-removal — same rule as the rewarded boosts in `BRAND_AND_PLATFORM_CANON.md` §5).
- Full table shown up front (C3). Player-initiated claim from the daily hub (C5).

### 3.3 Rewarded video — the free-layer session/return engine (DESIGN OWNED BY BRAND CANON)
`BRAND_AND_PLATFORM_CANON.md` defines two rewarded-video units; live-ops just **schedules their
cooldowns/caps** so they accelerate without trivializing (the canon's explicit guard):
- **Session driver:** 2× resources for 1 hour → watch, then play to capitalize.
- **Return driver:** extend an Echo's offline hold → watch *before logging off*, collect more on
  return (feeds directly into the §5 offline loop — a fatter haul = a bigger swarm = the
  risk/reward escalation counter).
- **Caps:** `rewardedDailyCap` + per-unit cooldown live in the cadence config; tuned to
  *accelerate, not flatten the curve* (canon §5 guard). **Additive only — never "watch or lose".**

### 3.4 The Daily Hub (presentation — one player-initiated glyph)
A single Obsidian panel (per `ui-blink-template-master-frame-formula`, code-built uGUI) reachable
from a HUD glyph (C5, never auto-pops). Shows, in one place: today's 3 quests + progress, the
login-calendar claim, the active rewarded-boost timers, today's monthly-card claim (if owned), and
"Season: tier N, next reward →". **One discovery surface for every daily affordance** — the player
opens it on their terms. Presentation only; pulls from the existing services.

---

## 4. THE WEEKLY LOOP (session + return)

### 4.1 Watcher's Roll — weekly leaderboard (DESIGN OWNED BY §12.2.2 of the monetization spec)
The weekly competitive driver is **already specced** as SKR-yield Stream B (`The Watcher's Roll`):
categories (highest wave, fastest dungeon clear, most pets bonded, most repairs), a dynamic
prize pool funded from staking yield, anti-gaming + a **legal gate** before B/C go live. Live-ops
**does not redesign it** — it (a) **surfaces standings in the daily hub** as a return hook ("you're
#6 — 3 waves from #5"), and (b) treats the weekly reset (Monday) as the master weekly clock all
other weekly cadences align to.

> **Covenant note:** prizes are **SKR (time/expression value), wallet-gated, transparently
> framed** ("Watcher prizes are sent to bonded wallets"). Non-wallet/excluded-jurisdiction players
> *see* the board but compete for the **free weekly quest meta-goal** (§4.2) instead — they are
> never shut out of a reason to play, only out of the *cash* prize (C2).

### 4.2 Weekly quest meta-goal (NEW — thin layer over daily quests)
A **weekly aggregate objective** ("complete 12 daily quests this week", "win 10 arena battles")
that rewards a chunk of glimmer + a convenience token + battle-pass XP. Implemented as a **weekly
counter** ticked by the *same* `DailyQuestService.Report(...)` event stream and
`ArenaProgressStore.RecordWin` — **no new gameplay hooks**, just a second accumulator with a
Monday reset. Non-punishing: progress that falls short still kept its daily rewards; the weekly is
pure upside. This is the **free-player weekly chase** that parallels the wallet-gated Roll.

### 4.3 Weekly deal spotlight (store curation — covenant-bounded)
Owned by `WORK_ORDER_economy_store_packs.md` §6 (`featured` + `rotationGroup` flag on an
*always-available* pack). Live-ops constraint (the §1.1 reconciliation): the spotlight is a
**curation convenience** — a steeper honest discount on a pack you could always buy — **rendered
WITHOUT a FOMO countdown and WITHOUT an expiring entitlement.** Copy is cozy ("This week the Folk
favor the Granary Haul"), not "ENDS IN 02:59:59". If a playtester reads it as urgency-pressure, it
fails the C4 gate and the discount becomes permanent (the rip-out is a flag flip).

---

## 5. THE OFFLINE-RETURN LOOP (the #1 return driver — DESIGN OWNED BY THE OFFLINE-STORAGE WO)

The single strongest "come back tomorrow" lever. **Fully designed** in
`WORK_ORDER_offline_storage_logic.md` — live-ops does **not** re-derive the formula or the tiers;
it frames *why it is the retention spine* and how the other loops feed it.

**The mechanic (reference):** Echoes fill a **silo** (capacity in HOURS, base 4h) and mine/pet
**nodes** accrue over an **offline window** (base 10h), both **clamped to cap then STOP**
(`EchoService` / `OfflineHarvestService`). On return, the **welcome-back popup** reveals the
already-banked haul (a *reveal*, not a transaction — closing the app never loses it; C4-clean).

**Why it is the retention engine (the felt loop, per that WO §4):**
1. **The cap creates the reason to return** — a bottomless barn kills the check-in; a generous-but-
   finite barn makes coming home *satisfying*. This is the classic idle loop, covenant-safe because
   idle waste past cap is **transparently disclosed** ("your mines filled up") and **never a loss of
   banked goods**.
2. **Hitting the cap is the honest upsell** — "I came back to a full barn and wasted 5h" is the felt
   pain that motivates the **storage-tier ladder** (the WO's §2, soft-currency path always available;
   SKR fast-track optional). The upgrade *directly* converts lost time into resources (WO worked-
   example C: same 9h away pays 720 → 2160 after upgrade). **Sells TIME, never POWER** — exactly the
   `BRAND_AND_PLATFORM_CANON.md` line.
3. **The other loops feed it** — the rewarded "extend offline hold" video (§3.3), the multi-Echo
   parallel-base scaling (brand canon: each Echo = one offline stream), and farm boosts that
   *multiply offline accrual* (`economy_store_packs` §4a, wall-clock based) all pour into this loop.
   A fatter haul = a bigger incoming swarm (the escalation counter) = a reason to *play* on return,
   not just collect. **Offline accrual → online defense → progression → another Echo → more streams.**

**Live-ops adds:** the welcome-back popup's *nudge copy* ("your realm gathered for Xh — it filled
early; a bigger Heartstore holds more") routes to the storage-tier panel — the only "sell" in the
return loop, and it sells convenience the player just *felt the need for*, never a stopper.

---

## 6. LIMITED-TIME EVENTS (LTEs) — the covenant-safe way to do "seasonal moments"

LTEs are the hardest covenant collision: the genre's LTE playbook is FOMO ("limited! expiring!
buy the bundle before it's gone!"). **We do the *content* without the *coercion*.** The rule:

> **Time-box the CONTENT and the EARNING WINDOW, never the OWNERSHIP and never with a loss-frame.**
> An event is a *themed reason to play this week*; what you earn you **keep forever**; nothing you
> already own expires; no countdown is framed as a threat.

### 6.1 The LTE data model (`liveEvents[]` — additive config block)
```
LiveEvent {
  eventId        : string          // "emberwake-festival"
  name, tagline  : string          // narrative-bible voice (C7)
  startUtc/endUtc: ISO-8601        // the EARNING window (when bonus rewards are active)
  theme          : string          // visual reskin id (ambient, banners) — cosmetic only
  modifiers      : [ EventModifier ]   // gameplay flavor (see 6.2) — NEVER power-for-pay
  rewardTrack    : [ EventReward ]     // earned by PLAYING during the window; kept forever
  tiePass        : bool            // true = event XP also feeds the active season pass
}
EventReward { milestone:int, grant:Grant, shownUpFront:true }   // Grant = battle-pass §4 shape; NO combat kind
```
- **`rewardTrack` is earned by playing**, shown up front (C2/C3). At `endUtc`, **unclaimed-but-
  earned rewards auto-grant** (the season-close courtesy from the battle-pass WO §2.1) — there is
  no "you earned it but lost it" trap.
- **The only thing the window gates is the *chance to earn the themed cosmetic by playing*** — i.e.
  it is scarce *expression*, never power, and never purchasable-only. (Same ethic as the season
  capstone + the single sanctioned Founder's-Vow launch window in `monetization-v2-spec.md` §4.)
- **Event SKUs (optional):** a themed **cosmetic** pack in the store during the event — a plain
  `PackDef` (no new type), no expiring entitlement, **stays earnable via glimmer after** (dual-
  sourcing transparency, battle-pass Open-Q #8). Cosmetic-only; the firewall rejects any combat kind.

### 6.2 Event modifiers — flavor, never pay-to-win
Modifiers are **global gameplay flavor** that apply to **everyone equally, free** — e.g. "Emberwake:
all Echoes harvest a themed *ember-grain* resource", "double *event-token* drops from waves". They
**never** alter combat stats and are **never** sold (`BattleMonthlyRegression`-class gate). An event
can run a **bonus-XP weekend** (`xp-weekend` is the *only* sanctioned multiplier — out-of-combat
pacing, brand-canon approved). No modifier may be a paid advantage.

### 6.3 Event cadence
One light **monthly** themed event (a week-long earning window) + the **season** as the big
quarterly-ish beat (§7). Events draw themes from the narrative bible (Emberwake, the Hollow's
Summer…). Events **never** overlap-pressure (no "3 events expiring at once" anxiety stacking).

---

## 7. SEASONS — the battle pass IS the season spine (RECONCILED)

There are two pass designs in canon; this doc **reconciles them and names the live one:**

| Design | Source | Status |
|---|---|---|
| **Keeper's Almanac** — 90-day, 30 tiers, milestone-unlocked, cosmetic-only, *permanent unlock, no expiry* | `monetization-v2-spec.md` §6 | **Earlier model.** Its *generosity DNA* (free track, no FOMO, keep-forever) is **retained as the covenant floor.** |
| **Seasonal Battle Pass** (Emberwake) — ~5-week season, XP **earned from arena play**, free + premium lanes, capstone prestige cosmetic | `WORK_ORDER_battle_and_monthly_packs.md` §2 | **The live design** (newer, ties to the Arena cash-cow). **This is the season spine.** |

**Reconciliation ruling (for the owner to confirm — Open-Q #1):** the **Battle Pass is the season**;
it **inherits the Almanac's covenant guarantees** — (a) a genuinely rewarding **free lane**, (b)
**earned cosmetics kept forever** at season close (the *track* resets, the *rewards* don't), (c)
**premium lane buys better cosmetics + convenience, never tiers/power** (XP is play-only, firewall-
gated), (d) **retroactive grant** on late premium purchase so nothing earned is lost. The one
*intentional* divergence from "permanent unlock, no expiry": a **season has an earning window** —
acceptable under C4 **because the loss-frame is removed**: you keep everything you earned, the only
thing that "ends" is the *opportunity to climb that specific track*, and a new one opens. That is
time-boxed *content*, not an expiring *entitlement*.

**Season as the master seasonal clock:** the season's `startUtc/endUtc` (battle-pass WO §2.1,
`lengthDays` ≈ 35) is the cadence anchor that LTEs (§6, `tiePass:true`) and the seasonal SKR
tournament (Stream C, `monetization-v2-spec.md` §12.2.3) align to. One season = one narrative theme
= one pass track + ~5 weekly Rolls + 1 themed LTE + 1 tournament finale. Live-ops **schedules** this;
the battle-pass WO **owns** the pass mechanics.

---

## 8. RE-ENGAGEMENT HOOKS (winning back the lapsed player — covenant-safe)

The hardest retention vector and the easiest to make predatory. Rails first, then the hooks.

### 8.1 The rails (binding)
- **No punishment for lapsing.** A returning player **lost nothing** — offline accrual was banked
  (capped, never zeroed), the login calendar never regressed (§3.2), owned cosmetics/season rewards
  are permanent. Re-engagement is **all carrot, zero stick.**
- **Notifications are opt-in, capped, and cozy** (C5/C7). Hard cap (e.g. ≤2/week), user-toggleable,
  narrative voice ("Elarion's lanterns are low — the Heart could use you"), **never** manufactured
  urgency ("your village is UNDER ATTACK, return NOW or lose it!" — banned, it is a fabricated-loss
  dark pattern and the village is safe offline by design, `village2-hand-tuned` canon).

### 8.2 The hooks (`comeback[]` config — milestone-keyed, not time-pressure)
| Hook | Trigger | Reward (covenant-safe) | Vector |
|---|---|---|---|
| **Welcome-back haul reveal** | first boot after ≥ offline-cap away | the already-banked offline grant (existing `WelcomeBackPopup`) | RETURN (existing) |
| **"Your realm missed you" notif** | 1 day idle (opt-in) | none in the notif — just a cozy nudge | RE-ENGAGE |
| **Comeback day-1 grant** | first login after ≥3 days away | a one-shot soft-currency + cosmetic-shard bundle (shown, fixed; **no power**) | RE-ENGAGE |
| **Lapsed win-back** | ≥7 days away | a richer one-shot bundle + "here's what's new" (new season/event digest) | RE-ENGAGE |
| **Re-onboarding tip** | ≥14 days away | surface a "what changed" card + re-pin the daily hub | RE-ENGAGE |
- **Anti-abuse:** comeback grants are **one-shot per lapse-window, latched** (mirror the
  `ClaimedAtUnix` double-grant latch) so a player can't toggle away/back to farm them. Generosity,
  not an exploit.
- **What re-engagement NEVER does:** offer a "catch-up pay-to-skip" that confers power; create an
  expiring "comeback offer" countdown; or make the lapsed player feel they *lost their place*. The
  comeback is "welcome home, here's a gift," full stop.

### 8.3 Push-notification dependency
There is **no notification system today** (the only `notification` hits in the repo are unrelated).
Mobile-web (WebGL) push is **constrained** — true push needs the PWA/native shell. **V1 reality:**
the welcome-back reveal + in-session "what's new" digest cover the **return** vector without push;
true **re-engagement push** is gated on the platform shell + the live-ops backend (§10). Flag as a
post-launch dependency, not a V1 blocker. Track in `docs/roadmap/live-ops-scope.md`.

---

## 9. THE DATA MODEL + INSTRUMENTATION (one config, one writer, one funnel)

### 9.1 One cadence config, one fulfillment writer
Per `owner-thinks-in-data-structures`: live-ops is a **schedule table + a claim interpreter**, not
branches. A single additive config (`Assets/Resources/Data/LiveOps/liveops-cadence.json`, runtime-
read, hot-tunable) holds: `loginCalendar[]`, `weeklyMetaGoals[]`, `liveEvents[]`, `comeback[]`,
`rewardedCaps`, and the season/weekly clock anchors. Every reward is the **`Grant` shape** from
`WORK_ORDER_battle_and_monthly_packs.md` §4 (`cosmetic_sku | skr | economy | convenience_token |
bundle` — **no `combat` kind exists**) and routes through the **one existing fulfillment writer**
(`DailyQuestRewardBridge` grant routes + `GlimmerCurrencyService` ownership + `ISkrLedger` +
`Save()`). **No parallel inventory, no new currency** — same convergence the battle-pass/monthly WOs
mandate.

### 9.2 The thin scheduler (design only — no code here)
```
OnDailyBoot:
  EnsureToday()                         // DailyQuestService already does this
  if loginCalendar.UtcDayAvailable():   // pool model, never regresses
     surfaceLoginClaim()                // player-initiated; latch on claim
  evaluateComeback(daysSinceLastSeen)   // one-shot, latched
  refreshActiveEvents(now)              // start/end windows; auto-grant earned-at-close
  FlowTrace.Step("LiveOps","dailyBoot", ...)   // §12 instrument every step

OnArenaResult / OnQuestComplete / OnHarvestClaim:
  tickWeeklyMeta(event); tickSeasonXp(event)   // reuse EXISTING event streams; no new hooks
```

### 9.3 Retention funnel instrumentation (CLAUDE.md §12 — binding)
Retention is **measured, not guessed.** `FlowTrace` (and the live-ops backend's analytics, §10)
capture the funnel so we tune from data, not theory:
- **`[Flow:LiveOps]`** Step on every claim / event start-end / comeback grant / boost activation.
- **Self-reporting metrics** (to the backend when it lands): D1/D7/D30 return rate, daily-hub open
  rate, login-calendar claim rate, offline-cap-hit rate (the upsell signal), season-tier
  distribution, event participation, comeback-grant redemption, rewarded-video opt-in rate.
- A headless **`LiveOpsRegression`** oracle (mirrors `BattleMonthlyRegression`) asserts the covenant
  invariants as a **build gate** (§9.4) — predatory config can't ship.

### 9.4 Covenant invariants (regression-gated — the firewall is a build gate)
1. **No `combat`/stat grant anywhere** in any login/event/comeback/weekly reward (every
   `Grant.kind ∈ {cosmetic_sku, skr, economy, convenience_token, bundle}`; every convenience
   `kind ∈ ConvenienceItemDef.Kind`). A revive/heal/damage reward **fails the build.**
2. **No reward is ever lost on a miss** — login calendar is pool-model (no regression check on the
   pointer); event `rewardTrack` auto-grants earned items at `endUtc`; offline grant persists on claim.
3. **No login-streak punishment** — the base calendar pointer never decrements.
4. **No expiring entitlement** — owned cosmetics/packs/season-rewards have no `expiresUtc`.
5. **No randomized paid reward** — every purchasable/claimable reward is shown up front (C3).
6. **Comeback grants are one-shot-latched** per lapse window (no farm-by-toggling).
7. **Notification cap respected** + opt-in flag honored; no fabricated-loss copy (string lint).
8. Every `cosmetic_sku` resolves to a real cosmetic id; **no binary inlined** (pointer strings only,
   `data-architecture-hybrid-db-direction` T1).

---

## 10. SERVER DEPENDENCY (what must be authoritative before money-real live-ops)

`docs/roadmap/live-ops-scope.md` is explicit: **entitlements are client-trusted today** (a hand-
edited save grants paid content free). Retention *content* (daily quests, login calendar, offline
accrual) is fine client-side for V1 felt-test. But **anything that pays real value or competes for
prizes must be server-authoritative**:
- **Battle-pass premium ownership, monthly-card claims, event-pack entitlements** → server entitlement
  ledger (live-ops-scope §3.3) before real-money.
- **Watcher's Roll standings + SKR payouts** → server score recording + the secured payout signer +
  the legal gate (live-ops-scope §3.5/§3.6, `monetization-v2-spec.md` §12.3/§12.4).
- **Pack/event scheduling** → server-side pack-deploy (live-ops-scope §3.4) so a season/event can be
  pushed/retired without a client update — the whole point of live-ops.

**Sequencing (matches live-ops-scope §5):** ship the *client-side felt loops* (daily quests already
live; login calendar, offline tiers, daily hub) on local save now → wire **server-authoritative
entitlements** → then light up *paid* seasons/cards/events and *prize* contests. **Do not enable a
real-money or prize live-ops feature on client-trusted state.**

---

## 11. WHAT NOT TO BUILD / TOUCH (scope guard)

- **Do NOT** re-implement daily quests, offline harvest, the arena ledger, the pack store, or SKR
  yield — they are owned by the §0 sources; live-ops **schedules + surfaces** them.
- **Do NOT** introduce any `combat`/stat/revive/heal/permanent-passive reward — §9.4 inv.1 is a gate.
- **Do NOT** add a FOMO countdown, an expiring entitlement, a login-streak *reset*, an energy gate, a
  pay-to-skip-a-timer, or a "buy or lose forever" frame — every one violates C4.
- **Do NOT** add a new currency primitive or a parallel inventory — reuse the §9.1 single writer.
- **Do NOT** auto-pop any live-ops UI mid-play (C5) — the daily hub + store are player-initiated; the
  welcome-back reveal is the lone sanctioned popup (and it's a reveal of a banked grant).
- **Do NOT** ship a real-money/prize loop on client-trusted entitlements (§10).
- **Do NOT** author UI in UXML — code-built Obsidian uGUI (`ui-blink-template-master-frame-formula`).
- **Do NOT** put binary (icons/skins) in any catalog/config — pointer strings only.
- **Do NOT** edit live `packs.json` / `daily-quests.json` to wire this — propose additive sibling
  config; the owner merges.

---

## 12. OPEN QUESTIONS (route to owner before any IMPLEMENT WO)

1. **Pass reconciliation (§7):** confirm **Battle Pass = the season** (inheriting Almanac
   generosity), retiring the standalone Keeper's-Almanac milestone model? (Recommend yes.)
2. **Login calendar model:** pool/odometer (recommended, never regresses) vs a *gentle* consecutive-
   bonus flourish on top — and the ~28-entry reward list (which days carry the milestone cosmetic).
3. **LTE cadence:** one light themed event per month + the season as the big beat — or seasons only
   for V1 (events V1.1)? (Recommend seasons-first; events as the next layer.)
4. **Re-engagement push:** is the PWA/native shell on the roadmap (enables true push), or is V1
   return-vector limited to the welcome-back reveal + in-session "what's new" digest? (§8.3)
5. **Weekly deal framing (§4.2/§1.1):** confirm the spotlight renders **without** a countdown/loss-
   frame (curation, not urgency) to stay C4-clean — or drop the rotating spotlight entirely for V1.
6. **Free-lane SKR drips:** does the free season/weekly lane drip a *little* SKR to fund non-payers'
   SKR-store access (very generous, on-covenant) — shared with battle-pass Open-Q #6.
7. **Comeback grant sizing:** the day-3 / day-7 / day-14 bundle contents (must stay convenience +
   cosmetic + soft currency; never power) — and the lapse thresholds.
8. **Server gating order (§10):** which paid/prize live-ops features wait on server-authoritative
   entitlements, and which client-side felt loops ship first for playtest.

---

## 13. ACCEPTANCE (of THIS design deliverable)
- [x] Daily/weekly/seasonal/lifecycle loops mapped as a **vector × cadence matrix** (§2) over
      existing systems — no greenfield.
- [x] Seasons **tied to the battle pass** (§7) with an explicit Almanac-vs-Emberwake reconciliation,
      referencing `WORK_ORDER_battle_and_monthly_packs.md` (not duplicating it).
- [x] **Limited-time events** designed **covenant-safe** (§6) — time-box content + earning, never
      ownership; no FOMO/loss-frame; auto-grant earned-at-close.
- [x] **Offline-return loop** tied to `WORK_ORDER_offline_storage_logic.md` (§5) — referenced as the
      #1 return driver, fed by rewarded video + boosts + multi-Echo streams; not re-derived.
- [x] **Re-engagement hooks** designed all-carrot-no-stick (§8) with the push dependency flagged.
- [x] **Covenant compliance** is explicit and **regression-gated as a build gate** (§1, §9.4) —
      C1–C7 + bent-C1, with the Decision-4 "no daily/weekly *deals*" reconciliation (§1.1).
- [x] **One config + one fulfillment writer + FlowTrace funnel** (§9); server-authoritative
      dependency for real-money/prize loops flagged (§10).
- [x] Monetization WOs **referenced, not duplicated** (§0 source table); scope-guard + open
      questions + no-touch list authored.
- [x] **No `.cs` written**; grounded in `DailyQuests.cs`, `EchoService`/`OfflineHarvestService`,
      `ArenaProgressStore`, `PackCatalog`, and the cited WOs/specs.
</content>
</invoke>
