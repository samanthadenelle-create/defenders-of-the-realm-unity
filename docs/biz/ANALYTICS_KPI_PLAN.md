# Analytics / Telemetry / KPI Plan — Echoes of Elarion

**Status:** PROPOSAL (v1) · 2026-06-28 · author: analytics agent
**Scope:** what to instrument for a data-driven live game, mapped onto the telemetry
already in the codebase. This is the *measurement contract*, not an implementation WO.
Implementation lands as a follow-up WO per §9.

> **One-line thesis:** we already ship a resilient analytics pipe
> (`DeNelle.Core.Analytics.EventTracker`) and a dev flight-recorder
> (`FlowTrace` + `BreakCaptureHarness`). This plan defines the **canonical event
> taxonomy** that flows through the production pipe (`EventTracker.Track`) so we can
> compute funnel, retention, economy, monetization, and balance KPIs — and draws the
> hard line between **product analytics (ships, every player)** and **diagnostic
> tracing (dev-only, verbose)** so the two never get conflated.

---

## 0. Two pipes, one rule — do not conflate them

The repo has two instrumentation systems. They serve different masters and must stay
separate.

| | **Product analytics** | **Diagnostic tracing** |
|---|---|---|
| Class | `EventTracker.Track(name, props)` | `FlowTrace.Step/Warn/Fail/...` |
| Sink | HTTPS POST → backend `/api/events/track` | Unity log / `WebTraceSink` → `break-log.jsonl` |
| Audience | analysts, biz, LiveOps | engineers debugging a specific flow |
| Ships? | **YES — every player, all platforms** | dev/desktop only; **off on WebGL** (`BreakCaptureHarness` early-returns on `WebGLPlayer`) |
| Volume | low, curated (≈ tens/session) | high, verbose (hundreds/run), toggled off when stable |
| Lifetime | permanent KPI contract | ephemeral, stripped once a system is proven |
| PII | playerId = `BoundWallet` or `"anonymous"` | none (local file) |

**Rule (BINDING for this plan):** KPI events go through **`EventTracker.Track` only**.
`FlowTrace` is *not* an analytics channel — it is verbose, gated off in production, and
not WebGL-safe. The one bridge that already exists is correct and is the *only* sanctioned
crossover: `BreakCaptureHarness` fires `EventTracker.Track("playtest_break", …)` so that
**stability** (a KPI) rides the production pipe while the *detail* stays local. Do not add
`EventTracker.Track` calls inside hot `FlowTrace` loops, and do not route KPI events to
`FlowTrace` "to save wiring."

### Why `EventTracker` is the right backbone (already built — do NOT greenfield)
`Assets/_Modules/Core/Analytics/EventTracker.cs` already gives us, for free, everything a
live-game telemetry SDK needs:
- **Batching** — up to `BatchSize` (10) per POST, flushed every `FlushIntervalSeconds` (30s) or on a full batch.
- **Offline durability** — queue persists to `PlayerPrefs` across scene loads *and* app restarts; cap `MaxQueueSize` (200), oldest-dropped.
- **Resilient delivery** — exponential-backoff retry (1→2→4→8s, 4 attempts) + circuit breaker (opens after 5 consecutive fails, 60s cooldown, half-open probe).
- **Identity** — `playerId = GameStateService.State.BoundWallet ?? "anonymous"`; `clientTs` unix-ms stamped at enqueue.
- **WebGL-safe** — fire-and-forget, no local filesystem dependency.

Backend contract (already implemented at `https://defenders-of-the-realm-v2.vercel.app/api/events/track`):
```
POST /api/events/track
Body: { "events": [ { "playerId", "eventName", "properties" (JSON string), "clientTs" (unix ms) }, … ] }
Resp: { "success": true }
```

**The work of this plan is taxonomy + coverage, not transport.** Transport is solved.

---

## 1. Events already firing (baseline coverage)

Verified from the working tree (`EventTracker.Track(` call sites):

| Event | Source file | Props today |
|---|---|---|
| `session_start` | `EventTracker.cs` (on boot) | `platform, appVersion, unityVersion` |
| `wave_completed` | `Village/Waves/WaveManager.cs:1675` | `waveId, liveEnemiesKilled(=0 TODO)` |
| `purchase_completed` | `Wallet/PackStore.cs:591` | `packId, price` (per usage doc) |
| `bundle_viewed` | `Wallet/PackStore.cs:379` | `bundleId` |
| `tower_swap_completed` | `Village/Buildings/TowerSwapService.cs:276` | swap context |
| `promo_redeemed` | `Core/Promo/PromoCodeService.cs:161` | code/value |
| `referral_code_generated` | `Core/Referral/ReferralService.cs:142` | `code` |
| `referral_shared` | `Core/Referral/ReferralService.cs:166` | `code, platform` |
| `referral_claimed` | `Core/Referral/ReferralService.cs:241` | claim context |
| `playtest_break` | `Core/Diagnostics/BreakCaptureHarness.cs:405` | `kind, message, scene, t, utc` |

**Gaps that block KPIs:** no FTUE/onboarding step events, no `first_battle` /
`battle_start` / `battle_end`, no D1/D7 retention anchor (`session_start` exists but
carries no `sessionNumber`/`daysSinceInstall`), no economy source/sink ledger events, no
`store_opened`/`pack_viewed` funnel above `bundle_viewed`, no combat balance signals
(damage, deaths, skill usage, difficulty), `wave_completed.liveEnemiesKilled` is hardcoded
`0`. §3–§7 fill these.

---

## 2. Event schema conventions (canon)

- **Name:** `snake_case`, `noun_verb-pasttense` where it's a completion (`wave_completed`),
  `noun_verb` for an action/intent (`store_opened`). Stable forever once shipped — never
  rename; deprecate + add.
- **Props:** a flat anonymous object, serialized to a JSON string by the pipe. Keep it
  flat (no nested objects) so the warehouse can column-map. Numbers as numbers, not strings.
- **Always-on envelope** (added by the pipe, do not duplicate in props): `playerId`,
  `clientTs`. 
- **Recommended common props** to add to *every* domain event (cheap, huge analytic value):
  `sessionId` (GUID minted at boot), `sessionNumber` (monotonic, from save),
  `appVersion`, `buildType` (`editor`/`dev`/`release`), `featureFlags` (compact hash of
  active flags — lets us slice KPIs by flag cohort, critical given the flag-gated V1/V2 split).
- **Money:** always `priceUsd` (decimal), `currency` (ISO, default `USD`), `store`
  (`apple`/`google`/`web`/`solana`). Never log raw payment tokens.
- **IDs:** reference catalog keys (`packId`, `heroId`, `enemyId`, `talentNodeId`,
  `buildingId`) — never display strings — so renames in UI don't break analysis.
- **No PII beyond wallet.** Wallet is pseudonymous and already the identity key; do not add
  email/IP/device-id to props.

---

## 3. FUNNEL — install → FTUE → first battle → retention

The acquisition-to-activation funnel. Each step is one event; the funnel is the ordered
drop-off between them. Anchor everything to **install** (first-ever boot) and **session**
(each boot).

### 3.1 Install & session anchors
| Event | When | Key props |
|---|---|---|
| `app_installed` | first boot ever (save absent → minted) | `installTs, platform, store, referralCode?, appVersion` |
| `session_start` *(extend existing)* | every boot | **add:** `sessionId, sessionNumber, daysSinceInstall, secondsSinceLastSession, isFirstSession` |
| `session_end` | app pause/quit (`OnApplicationPause/Quit`) | `sessionId, sessionLengthSec, scenesVisited, battlesPlayed` |

`daysSinceInstall` + `sessionNumber` are the **retention backbone** — D1/D7/D30 are derived
server-side from `app_installed.installTs` vs. the set of distinct `session_start` UTC days
per `playerId`. We do NOT compute retention client-side; we just stamp the anchors.

### 3.2 FTUE / onboarding steps (the activation funnel)
The new-player path is hub (`MainCastle_Hall`) → tutorial beats → first overworld
encounter → first battle. Emit one event per beat so we see exactly where new players quit.

| Event | Step | Props |
|---|---|---|
| `ftue_step` | each onboarding beat reached | `step` (enum: `intro_dialogue`, `hero_select`, `first_move`, `hub_arrived`, `first_build_prompt`, `overworld_entered`, `first_encounter_seen`), `stepIndex, secondsIntoSession` |
| `ftue_completed` | tutorial flow done | `totalSeconds, stepsCompleted` |
| `ftue_abandoned` | session_end before `ftue_completed` | `lastStep, lastStepIndex` (derived server-side; optionally explicit on quit) |

> **Mapping note:** several of these beats *already have* `FlowTrace.Step` calls in the
> onboarding/seam/hero-select flow. Do **not** reuse the FlowTrace line as the analytics
> event (§0). Instead, at each beat add a sibling `EventTracker.Track("ftue_step", …)`
> next to the existing `FlowTrace.Step(...)`. The FlowTrace line stays for debugging; the
> Track line is the durable KPI. (Pattern: `BreakCaptureHarness` already does exactly this
> double-emit for breaks.)

### 3.3 First-battle activation
"First battle" is the core activation event — the strongest D1-retention predictor in
combat games.
| Event | When | Props |
|---|---|---|
| `first_battle_started` | the player's first-ever `battle_start` (guard on a save flag) | `source` (`overworld_encounter`/`wave`), `heroId, daysSinceInstall, secondsSinceInstall` |
| `first_battle_result` | that battle ends | `outcome` (`win`/`loss`/`flee`), `durationSec` |

### 3.4 Funnel KPIs (computed in the warehouse)
- **Install→FTUE-start rate**, **FTUE step completion %** (per `step`), **FTUE→first-battle rate**.
- **Activation rate** = `% installs that reach first_battle_result(win)` within session 1.
- **D1 / D7 / D30 retention** = `% of an install-day cohort with ≥1 session on day N`.
- **Stickiness** = DAU/MAU. **Session frequency** = sessions per DAU. **Session length** = `session_end.sessionLengthSec` p50/p90.

---

## 4. ECONOMY — sources & sinks (the soft-currency ledger)

The economy currencies are fixed in `DeNelle.Core.ResourceType`:
`Iron, Wood, Food, AetherCrystal` (mirrors `GameState` wallet fields; `AetherCrystal` =
the premium/aether axis). Plus the design's **life-force / echo** loop (echoes harvest
wood/iron/grain; life force grows the tree). Every grant and spend must emit a **ledger
event** so we can balance the economy (faucets vs. sinks) and detect inflation/starvation.

### 4.1 The universal ledger event
One event for *all* currency movement — analysts pivot on `reason`:
```
currency_granted   { currency, amount, reason, balanceAfter, sourceId? }
currency_spent     { currency, amount, reason, balanceAfter, sinkId? }
```
- `currency` ∈ `iron|wood|food|aether|lifeforce`.
- `reason` is a controlled vocabulary (see below) — this is what makes the ledger analyzable.
- `balanceAfter` lets us reconstruct holdings without replaying the whole stream.

**Single chokepoint:** route every grant/spend through `EconomyService.Grant(...)` /
`Spend(...)` and emit the ledger event **there, once**, rather than at each call site.
`WaveManager.AwardWaveResources` (the primary faucet, `WaveManager.cs:1669`) and the
BuildMenu/upgrade spend paths already funnel through `EconomyService` — instrument the
service, not the callers. This guarantees no source/sink is ever missed.

### 4.2 Sources (faucets) — `reason` values
| reason | Trigger | Notes |
|---|---|---|
| `wave_reward` | `AwardWaveResources` / `AwardWaveCrystals` | primary income; scales with `waveId` |
| `echo_harvest` | echo workforce passive tick | the watch-it-grow loop; tag `echoCount` |
| `battle_reward` | overworld arena win | crown/tier rewards |
| `building_passive` | Farm/mine passive accrual | offline-accrual included |
| `quest_reward` | quest completion | |
| `promo_grant` / `referral_grant` | promo/referral | ties to §6 |
| `iap_grant` | resource included in a purchased pack | bridges economy↔monetization |

### 4.3 Sinks (drains) — `reason` values
| reason | Trigger |
|---|---|
| `building_construct` / `building_upgrade` | BuildMenu + Forge/Armorer/Arcane research (WC3 tech-tree, WO-432) |
| `tower_swap` | already has `tower_swap_completed`; also emit the spend leg |
| `talent_unlock` | 68-node talent tree node purchase |
| `gear_craft` / `gear_buy` | crafting + store gear |
| `echo_recruit` | adding an echo to the workforce (cap 5) |

### 4.4 Economy KPIs
- **Faucet/sink ratio per currency** (granted vs. spent per DAU) — the inflation gauge.
- **Net holdings distribution** (p50/p90 `balanceAfter`) — detect hoarding (sink too weak) or starvation (faucet too weak / sink too greedy).
- **Sink mix** — which sink absorbs each currency; reveals dead/ignored sinks.
- **Time-to-first-upgrade**, **upgrade cadence** — economy pacing health.
- **AetherCrystal source split** (earned vs. purchased) — premium-currency balance, feeds §6.

---

## 5. MONETIZATION — conversion, ARPPU, pack mix

The store is `PackStore` + `PackCatalog` (`Assets/_Modules/Wallet/`). Existing:
`bundle_viewed`, `purchase_completed`. We need the **full purchase funnel** and the **revenue
attribution** to compute the standard monetization KPIs.

### 5.1 Purchase funnel events
| Event | Step | Props |
|---|---|---|
| `store_opened` | store/pack screen shown | `source` (where they entered from: `hud`, `out_of_currency`, `offer_popup`, `victory`) |
| `pack_viewed` *(supersedes/augments `bundle_viewed`)* | a pack/bundle detail shown | `packId, priceUsd, position` |
| `purchase_started` | checkout/pay invoked | `packId, priceUsd, store` |
| `purchase_completed` *(extend existing)* | payment confirmed | **add:** `priceUsd, currency, store, packId, isFirstPurchase, sessionNumber, secondsSinceInstall` |
| `purchase_failed` | payment error/cancel | `packId, store, failReason` (`cancelled`/`error`/`pending`) |
| `purchase_restored` | restore-purchases | `packIds[]` |

### 5.2 Offer / merchandising events (LiveOps)
| Event | Props |
|---|---|
| `offer_shown` | `offerId, packId, placement, priceUsd, discountPct` |
| `offer_clicked` | `offerId, packId` |
| `offer_dismissed` | `offerId, dwellSec` |

### 5.3 Monetization KPIs
- **Conversion rate** = `% DAU (or installs) with ≥1 purchase_completed`. Split **first-time** vs **repeat**.
- **ARPDAU** = revenue / DAU. **ARPPU** = revenue / paying users. **LTV** = cumulative ARPPU by install-cohort age.
- **Pack mix** = `purchase_completed` count + revenue share by `packId` — what actually sells.
- **Funnel drop-off:** `store_opened → pack_viewed → purchase_started → purchase_completed` (each ratio).
- **Price-point performance:** revenue & conversion by `priceUsd` tier.
- **Time/sessions-to-first-purchase** (`secondsSinceInstall`, `sessionNumber` on first purchase).
- **Offer efficiency:** `offer_shown → clicked → purchase` per `offerId`/`placement`.
- **Premium-currency loop:** AetherCrystal purchased (`iap_grant`, §4.2) vs. spent — does bought currency get used (good) or hoarded (offer fatigue)?

> **Solana note (from data-architecture canon):** when wallet/Solana payment lands,
> `store` gains a `solana` value and `purchase_completed` should carry an opaque `txRef`
> (chain tx hash) — never keys/seed. Identity is already wallet-based (`playerId =
> BoundWallet`), so on-chain purchases attribute natively.

---

## 6. ACQUISITION LOOPS — referral & promo (already partly wired)

Referral/promo events exist (§1). Add the **funnel + attribution** so we can value the loop:
- `referral_code_generated` → `referral_shared{platform}` → (new install with `app_installed.referralCode`) → `referral_claimed`.
- **KPIs:** **K-factor** (invites sent × accept rate), **referred-install share**,
  **referred-user D7 & conversion** vs. organic (cohort by `referralCode` present),
  **promo redemption rate** + **post-redemption spend lift**.

---

## 7. COMBAT / BALANCE signals

V1 combat = single Knight, ATB + isolated real-time overworld BattleArena, plus the village
wave loop. Balance tuning needs per-battle and per-encounter telemetry. `wave_completed`
exists but is thin (and `liveEnemiesKilled` is a hardcoded `0` TODO at `WaveManager.cs:1678`).

### 7.1 Battle lifecycle
| Event | When | Props |
|---|---|---|
| `battle_start` | arena/wave engagement begins | `battleId, source(overworld/wave), heroId, heroLevel, enemyFamily(orc/skeleton/troll), enemyComposition[], enemyCount, waveId?, difficultyTier` |
| `battle_end` | resolved | `battleId, outcome(win/loss/flee), durationSec, heroHpPct, enemiesKilled, heroDeaths, dmgDealt, dmgTaken, skillsUsed[], potionsUsed` |
| `wave_completed` *(fix existing)* | replace `liveEnemiesKilled=0` | populate real `enemiesKilled, durationSec, heroHpPctRemaining, resourcesAwarded` |

### 7.2 Granular combat signals (throttled / aggregated — NOT per-hit spam)
Aggregate per battle and emit on `battle_end`; do **not** fire an event per swing.
| Signal | Carried as |
|---|---|
| Skill/talent usage | `skillsUsed[]` = `[{skillId, casts}]` — feeds talent-balance |
| Damage breakdown | `dmgByAbility[]`, `dmgTaken`, top enemy source |
| Death/wipe analysis | `heroDeaths`, `killedByEnemyId`, `deathAtSec` |
| Difficulty signal | `outcome` + `durationSec` + `heroHpPct` per `(enemyFamily, difficultyTier)` |
| Flee/retreat | `outcome=flee` + `fleeAtSec` (rage-quit / too-hard signal) |

### 7.3 Progression
| Event | Props |
|---|---|
| `hero_level_up` | `heroId, newLevel, source` |
| `talent_unlocked` | `talentNodeId, tier, treePath, isCapstone` (68-node tree) |
| `building_upgraded` | `buildingId, newTier` (WC3 tech-tree) |
| `wave_milestone` | `bestWave` (the cross-session resume seed `RecordRun` already tracks) |

### 7.4 Balance KPIs
- **Win rate by `(enemyFamily, difficultyTier, heroLevel)`** — the master difficulty curve. Target a tuned band (e.g. 60–80% at-level); outliers flag over/under-tuned encounters.
- **Battle duration distribution** per encounter — too-short (trivial) / too-long (slog).
- **Wave drop-off** — which `waveId` players churn at (`wave_completed` max per player vs. `battle_end(loss)` at that wave).
- **Skill/talent pick & impact** — usage frequency × associated win-rate; surfaces dead nodes and must-picks (talent-tree rebalance input).
- **Death heatmap** — `killedByEnemyId` × `deathAtSec`; identifies spike-damage enemies.
- **First-battle win rate** — ties back to §3 activation.

---

## 8. KPI → event traceability matrix

| KPI | Primary event(s) | Derived how |
|---|---|---|
| D1/D7/D30 retention | `app_installed`, `session_start` | distinct session-days per install cohort |
| Activation rate | `app_installed`, `first_battle_result` | win within session 1 / installs |
| FTUE completion | `ftue_step`, `ftue_completed` | per-step survival |
| Session length / freq | `session_start`, `session_end` | per playerId per day |
| Faucet/sink ratio | `currency_granted`, `currency_spent` | granted vs spent by currency |
| Holdings health | `currency_*` `balanceAfter` | p50/p90 distribution |
| Conversion | `purchase_completed`, DAU | payers / active |
| ARPPU / ARPDAU / LTV | `purchase_completed.priceUsd` | revenue / (payers|DAU|cohort) |
| Pack mix | `purchase_completed.packId` | count & revenue share |
| Purchase funnel | `store_opened`→`…`→`purchase_completed` | step ratios |
| K-factor | `referral_shared`, `app_installed.referralCode`, `referral_claimed` | invites × accept |
| Win-rate curve | `battle_end` | win% by enemy/tier/level |
| Wave churn | `wave_completed`, `battle_end(loss)` | max wave per player |
| Talent balance | `battle_end.skillsUsed`, `talent_unlocked` | usage × win-rate |
| Stability (crash/softlock) | `playtest_break` | breaks per session/scene |

---

## 9. Implementation phasing (proposed WOs — for CLI, not done here)

Ordered by KPI leverage per unit of wiring. Each phase is one `EventTracker.Track` coverage
pass; transport is already done.

- **Phase 1 — Retention & activation backbone (highest leverage).** Mint `app_installed`
  (save flag), extend `session_start` (+`sessionId/sessionNumber/daysSinceInstall/isFirstSession`),
  add `session_end` on pause/quit, add `first_battle_started/result`. *Unlocks D1/D7, activation,
  session KPIs — the non-negotiables of a live game.*
- **Phase 2 — Monetization funnel.** Add `store_opened`, `pack_viewed`, `purchase_started`,
  `purchase_failed`; enrich `purchase_completed` (`priceUsd/currency/store/isFirstPurchase`).
- **Phase 3 — Economy ledger.** Single `currency_granted`/`currency_spent` chokepoint in
  `EconomyService` with the `reason` vocabulary (§4.2/§4.3).
- **Phase 4 — FTUE steps.** `ftue_step`/`ftue_completed`/`ftue_abandoned`, double-emitted
  next to existing onboarding `FlowTrace.Step` calls.
- **Phase 5 — Combat/balance.** `battle_start`/`battle_end` with aggregated combat signals;
  fix `wave_completed.liveEnemiesKilled`; add progression events.
- **Phase 6 — Common envelope.** Add `sessionId/appVersion/buildType/featureFlags` to every
  domain event (one helper in `EventTracker.Enqueue`, so call sites stay clean).

### Cross-cutting implementation notes
- **One helper, not N call sites.** Add `EventTracker.Track` at *service chokepoints*
  (`EconomyService`, `PackStore`, `WaveManager`, battle controller, onboarding director),
  never sprinkled per-UI-button.
- **Respect the off-switch.** Diagnostic `FlowTrace` is toggled/stripped when stable;
  product `EventTracker` events are **permanent** — never gate them behind a debug flag.
- **Sampling.** If combat granular volume grows, sample `battle_end` granular props at the
  *warehouse*, never drop whole events client-side (keep the funnel complete).
- **Privacy/consent.** Before store launch, gate analytics behind the platform consent
  prompt (ATT/GDPR); the pipe already supports an off state (don't `EnsureExists`, or add a
  `SetEnabled(false)`); wallet is the only identifier and is already pseudonymous.
- **Validation.** Add a headless-fleet oracle (AutoPilot) assertion that the canonical
  events fire in order during a scripted run (e.g. `session_start` → `ftue_step` →
  `first_battle_started`), so a regression that silences an event is caught by the gate, not
  in production. This matches the project's instrument-and-verify discipline (§12).

---

## 10. Open questions for the owner (PM/creative calls)
1. **Backend warehouse:** is the Vercel `/api/events/track` endpoint persisting to a queryable
   store (Postgres/BigQuery) yet, or just accepting? KPIs need a warehouse + dashboards
   (Metabase/Looker/etc.). *Out of scope for client; flagged.*
2. **Consent model:** mobile-first → ATT (iOS) + GDPR consent required before launch. When?
3. **Currency canon for telemetry:** confirm the `currency` enum for the ledger —
   `iron/wood/food/aether/lifeforce` — and whether `lifeforce` is a true wallet value or a
   derived tree-growth metric (affects whether it's a ledger currency or a separate
   progression signal).
4. **Difficulty tiers:** is `difficultyTier` a defined axis yet, or do we derive it from
   `waveId`/seed-budget? Needed for the §7.4 win-rate curve.
5. **A/B / LiveOps:** do we want an experiment-id prop (`abBucket`) in the common envelope
   now, so offer/price tests are sliceable from day one?

---

### Source grounding (verified from working tree, 2026-06-28)
- `Assets/_Modules/Core/Analytics/EventTracker.cs` — the production pipe (batching, offline, retry, circuit breaker, backend contract).
- `Assets/_Modules/Core/Diagnostics/FlowTrace.cs`, `BreakCaptureHarness.cs` — dev tracing + flight recorder + the sanctioned `playtest_break` crossover.
- `Assets/_Modules/Core/ResourceType.cs` — `Iron/Wood/Food/AetherCrystal` wallet axes.
- Existing `Track` call sites: `WaveManager.cs:1675`, `PackStore.cs:379,591`,
  `TowerSwapService.cs:276`, `PromoCodeService.cs:161`, `ReferralService.cs:142,166,241`,
  `BreakCaptureHarness.cs:405`.
- Canon: `docs/COMBAT_PIVOT_NORTHSTAR.md` (single-Knight V1), data-architecture memory
  (hybrid local/remote, Solana-staged), echo-workforce + life-force economy memories.
