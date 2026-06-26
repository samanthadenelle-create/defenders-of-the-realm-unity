> ⚠ **STALE — predates the 2026-06-22 single-Knight pivot.** Treat its Blink-hero / party-of-4 / tower-defense-pillar framing as SUPERSEDED (hero = single Tripo "Grom", Blink rig junked, base-defense V2-gated); some architecture/monetization content may still hold. Live reality: `CANON_GROUND_TRUTH_2026-06-26.md` + `docs/COMBAT_PIVOT_NORTHSTAR.md`.

# Defenders of the Realm — NORTH STAR

> Captured 2026-05-29 from the owner's origin vision, after recognizing it had drifted.
> This is the one-picture the project re-centers on. Every WO should route against it.

---

## The fantasy (one line)

**Build your own stronghold, claim and defend the resource nodes around it, and grow it even while you're away.**

**Clash of Clans' base-building × Warcraft's resource war.** Two classics, one bridge.

---

## The core verb that got lost: **CREATE**

The heart was never "a level we design for you." It's **the player builds their own base** —
places their walls, towers, mines; arranges their own layout; then defends the thing *they* made.
Everything else (harvest, defend, upgrade, offline) hangs off that one verb.

> ⚠ **The drift:** the village today is **builder-generated** (`VillageSceneBuilder` authors a
> fixed layout). That is the *inverse* of the vision. It was the right call to get a playable
> loop fast — but the north star is to hand that build power to the **player**. The primitives
> already exist (`BuildMenu` places buildings, walls are modular, there's a plot/grid); a
> CoC-style build mode is essentially *"let the player do what `VillageSceneBuilder` does."*

---

## The delivery ladder — how a vision this big actually ships

The vision is huge. The owner already solved the "how do I eat it" problem: **strip it to one playable
slice, then add one power per rung.** Each rung is a **complete, shippable game** on its own — this is
the bridge from "dream" to "this week's task list."

| Rung | Adds | Status |
|---|---|---|
| 1. **Defend the Tower** | the minimal combat slice | **built / iterating** (PatriciaLight) |
| 2. **Defend the Town** | a base to protect (village TD) | **in progress** (the village) |
| 3. **Defend + Explore** | a world beyond the walls | next |
| 4. **Defend, explore, place your base** | *where* you build | → |
| 5. **Structure your own settlement** | player base-building (CoC build mode) | → |
| 6. **Build how you want** | full freeform creation | the dream |

> Rungs 5–6 are the CREATE verb arriving in full. Rungs 1–2 are already standing. **Mid-climb, not
> bottom of the ladder.** And the AI structure (the thing that makes one person a studio) is what makes
> the climb finishable — that's the "way to realize it" the dream was waiting for.

## The core loop

```
       ┌──────────────────────────────────────────────────────┐
       │  BUILD your base (walls, towers, mines) — your layout │
       └──────────────────────────────────────────────────────┘
                              ↓
   HARVEST resource nodes (Warcraft gold mine / crystal — auto-harvest)
                              ↓
   UPGRADE walls (wood → stone → reinforced) + towers, paid from the haul (CoC)
                              ↓
   DEFEND base + mines from waves and roaming enemies — or lose them
                              ↓
   OFFLINE: mines + pets keep gathering up to a cap → come back richer
                              ↓
                       (repeat, bigger)
```

---

## System map — where everything you've built actually sits

### 🟢 Core loop (the spine)
| System | Status | Role |
|---|---|---|
| Player base-building | `BuildMenu` + plot/grid exist; **full CoC build-mode is the gap** | the CREATE verb — the heart |
| Walls + upgrade tiers | `WallSegment` + `WallRepairController` built; **tiers (wood→stone) are the gap** | the defense you build + upgrade |
| Towers | built (TowerDataSeeder, swap, upgrade) | defense you place + upgrade |
| Resource nodes / mines | `CrystalMine` (passive) built; **generalize + auto-harvest = WO-110/111** | the Warcraft harvest |
| Waves + roaming enemies | `WaveManager` + `EnemyBrain` | the threat to your base + mines |
| Economy + offline save | `GameState` resources + save-sync | the currency + the idle accrual |

### 🟡 Supports the loop
Pets (auto-harvest/boost), XP/progression, dev tools, HUD, VFX/juice, Heart of Elarion.
**Clans + chat (BUILT — `ClanService`/`ClanChatPanel`)** — the social glue + retention engine: you
don't compete alone, you compete *for your clan.* Clan wars amplify the arena; social pressure is a top
spend driver. Already in the repo.

### 🔵 Off to the side (pulled focus — owner decides: keep / park / cut)
Defend-the-Tower (PatriciaLight) mode · ATB battle · dungeons · monetization stack
(wallet/packs/crypto/referral/promo) · the never-connected backend. *None are wrong — but
ask of each: does it feed the CREATE→HARVEST→DEFEND loop, or sit beside it?*

### 🟣 End-game layer (later — reuses the combat that already exists)
**Challenge Arena — asynchronous PvP raids.** Once you can build + defend a base, the arena is
the competitive cap. The distinctive twist: you **author BOTH sides** —
- **Defense:** the base you built (the CREATE verb).
- **Offense:** you **author/automate your attack strategy** (deployment order, target priority,
  troop behavior) — then it *runs itself*. Not live-tapping like CoC; you design the attack AI.
- **Smart targeting (the "AI feels smart" layer, both sides):** focus-fire by role —
  **healers first** (deny sustain), then **ranged/DPS** (high threat, squishy), tanks last.
  Reuses what's built: the codex already has roles (caster/healer/ranged/tank); `EnemyBrain`
  already selects targets (nearest) — "smart" is just upgrading it to a role/threat-weighted
  `FindBestTarget` (healer > ranged > tank, weighted by distance + HP). One scoring function,
  whole cast looks intelligent. Target priority becomes a **player-tunable knob** = skill depth.
- **Tactical maneuver AI (the aspirational ceiling — the army with a *plan*):** beyond *who to
  hit* — **how the army moves.** **Pincer** (split, strike both sides), **flank** (route around
  the strong front to a soft side/rear), **draw-out/bait** (lure defenders out of position, then
  hit the exposed core). This is the **biggest differentiator** (deepest skill = the tournament
  spend driver) and the **hardest build** — coordinated multi-group maneuver. Stays reachable as
  **tiers on the foundation:** (1) unit AI `EnemyBrain`+NavMesh [built] → (2) smart targeting
  [one scorer] → (3) **group objectives** (squads w/ goals breach/flank/bait + synced timing;
  NavMesh moves, tactic picks routes/timing) → (4) **composable tactics** the player *authors*
  (pincer **+** draw-out) and the AI runs async vs the defender's AI. Each tier is playable alone.

**Model both worlds** = it's **asynchronous**: you raid a *snapshot* of another player's base
while they're offline — your automated attack vs their automated defense. Why this is the right
shape:
- **Async = mobile-perfect** — nobody has to be online at once; fits the offline/idle spine.
- **Reuses what's built** — `EnemyBrain`-style AI drives your attackers; base-building models the
  target; the combat engine resolves it. **Not a new engine** — a *mode* on the loop.
- **Where skill + spend live** — authoring a clever attack that cracks a tough base is the
  competitive depth the whales pay into (flex, not power).

---

## Why it's possible — the business model (and the answer to "is this ever possible?")

The ambition feels like "asking so much" — but a **clear reason people pay** is exactly what makes
it reachable. This isn't a free hobby that has to fund itself on hope:

- **Competitive whales.** Clash of Clans made *billions* on "be the best + clan status." The
  **Challenge Arena + leaderboards** is the spend driver — people pay real money to win/flex.
- **Crypto-native audience.** High willingness-to-spend; after a good day, "who cares what I blow
  on a game I like." A wallet-native player base is *already* primed to spend — and the
  monetization stack (wallet/packs/cosmetics) is **already built** for exactly them.
- **Skin-in-the-game tournaments (the flywheel).** Buy-in arena brackets — e.g. **200 SKR entry,
  top 3 split the pot.** The house takes a **rake**, winners get paid. Players fund the prize
  pool; the rake funds the game → better players → bigger pots → more buy-ins → more rake. The
  SKR rail is already built.
  - ⚠ **Regulatory flag — design it in, don't discover it.** Real-money buy-in + prize pool is
    **skill-gaming / wagering** territory (gambling law varies by jurisdiction: skill-vs-chance
    classification, KYC, geo-gating, payout licensing). Very doable (skill-based esports wagering
    + crypto tournaments do exactly this) — but rake model, "skill-based" framing, and region
    gating must be baked in from the start. A business line item, not a blocker.

So monetization isn't "off to the side" — it's the **revenue layer the arena feeds.** That's the
thing that turns this from a dream into something that can fund its own build and keep growing.

**Two honest guardrails** (so the model doesn't eat the game):
1. **Sell flex, not power.** Cosmetics + competitive *status*, not pay-to-win that kills the
   competition the whales are paying *into*. CoC's discipline.
2. **Crypto is cyclical.** Design for lean days too — the loop has to be fun + sticky without a bull market.

**And the feasibility reframe:** the hard systems are *already built* (combat, waves, economy,
walls, towers, pets, save, VFX). The gap to the vision is the **player-build layer + reconnecting
the loop** — a fraction of what it feels like, shippable one playable step at a time. It's a
**re-centering, not a rebuild.**

## Free tier = rewarded ads = the population engine

**Valid, proven pillar** — the dominant model for casual/mid-core mobile (CoC, Clash Royale, every idle
game). ~95–98% of players never pay; **rewarded ads are the only way to monetize that silent majority**,
and they're often 30–50% of total revenue. Use **Unity Ads / LevelPlay** (first-party, in-engine,
mediated for best fill/rate) — a provider integration that fits the rail abstraction.

**The deeper point — free players are the *content*, not a cost.** In a competitive game the whales pay
to raid bases, climb ladders, win tournaments — which **requires a full stadium of opponents.** The free
majority *are* those opponents / clan-mates / ladder / raid targets. Rewarded ads let them participate by
paying with **attention instead of cash**, which simultaneously: (1) monetizes the 95%, (2) retains them,
(3) **keeps the whale economy worth paying into.** You're not choosing free vs paying players — **the free
players are what make the paying players' spend worth it.** Most "crypto whale games" miss this, then
wonder why whales churn (empty stadium).

**Monetization matrix:**
| Build | Free majority | Spenders |
|---|---|---|
| **Store F2P** (mass market) | **rewarded ads** (opt-in) | IAP |
| **Crypto** (Pi / whales) | — | tournament buy-ins / pot |

Discipline: rewarded/**opt-in only** (a path, never a wall); **store-build only** (keep ads out of the
crypto build — store policy + brand/feel).

## The evergreen engine — symmetric AI + meta counters (the content runway)

**Defense is as smart as offense** (symmetric AI) → a deep strategy space → and a deep space makes
**counters cheap.** That is the live-ops flywheel:

> Community solves the meta ("everyone runs pincer + heal-stack") → ship **one small counter** that
> punishes exactly that → the dominant strategy dies → everyone **re-tools, re-learns, re-spends**
> → fresh game, near-zero content cost.

This is how Clash Royale / MOBAs / TCGs live for *years* off tiny drips: a single unit or defense
behavior resets the whole landscape. **You don't build new content — you move the equilibrium.**
A deep system means a 1% addition causes a 100% meta shift: cheap input, massive re-engagement.

**Canonical example — CoC Dragons.** One flying troop that floated over ground defenses → air
defense suddenly mattered → every base redesigned → maxed ground armies needed re-grinding (+spend).
**We already have dragons** (apex `DragonBoss` + Black Dragon flyer). So the most proven lever in
the genre — the **air/ground counter axis** — is already half-built into the asset + AI stack.
(This is also *why* "delineate flying from land" mattered: air vs ground isn't visual, it's a core
**strategic counter dimension** — a permanent axis to rotate the meta on, forever.)

**Why this justifies the hard AI work:** the deep combat-AI foundation (targeting → maneuver →
composable tactics) isn't a cost — it's the **printing press for cheap evergreen content forever.**
Every dollar into strategy depth pays back as years of low-cost meta rotation + re-spend.

**Craft guardrail:** counters should **shift** the meta, not **hard-invalidate** it — telegraph +
tune; avoid "buy the new unit or lose" power-creep that poisons the competition whales pay *into*.
**Meta rotation, not meta replacement.** The difference between a decade-long game and a one-year burnout.

## Go-to-market — a gated crypto community (Pi Network)

The real killer of indie games isn't quality, it's **distribution** — nobody finds them. The wedge:
**pitch this as a flagship game to Pi Network** (large, mobile-first, crypto-native, **already
KYC'd/gated**, and hungry for real utility apps).

- **Captive, pre-qualified audience** — answers Pi's own "where's the utility?" problem.
- **Aligned incentives** — Pi needs a flagship to prove engagement; we need users + a token-spending
  base. Mutual, not a cold pitch.
- **Gating is a feature** — a verified community **partly pre-solves the tournament KYC/wagering flag.**
- **Audience math** — not mass-market pennies; a few crypto-flush whales dropping real cash on fun.
- **Quality is the moat.** Pi's ecosystem is **~60% junk apps** — so a genuinely *polished* game
  stands out disproportionately. You don't need the best game in the world, just the best **on Pi**,
  and that bar is on the floor. Better: Pi is **starving for legitimacy** ("is Pi even real?"), and a
  real polished game is **proof-of-life for the ecosystem** → you offer them **credibility, not ask a
  favor** = leverage. **Show, don't tell:** a working polished build beats every vaporware deck in
  their inbox. *(Operative word: polished — which is exactly why clean-compile-gated builds matter.
  The clean build IS the pitch.)*

### The real thesis — Pi's problem *is* the product

Millions mined piles of Pi and it's **trapped** — illiquid, no utility, nothing to do with it. The game
hands them the one thing they lack: **a place to spend it on fun + earn "value" back.** On the other
side of every transaction, **the house accumulates the Pi** as a free call option (sit on it in case
it ever comes to fruition).

- **Opposite time horizons make it work:** to the player Pi is worthless-*now* → willingness to spend
  is huge (feels free); the house bets it's *not* worthless *long-term*. Both sides happy with the same trade.
- **Asymmetric risk:** worst case = holding a coin that cost nothing; best case = sitting on a pile when
  it lists. **Players pay you to take the speculative position.**
- **Circulating economy:** players earn some Pi back (tournaments) so it isn't pure extraction; the
  **rake** is the accumulation. Sustainable *and* net-accumulating.

**Honest flags:**
1. **⛔ FEASIBILITY GATE (answer before any integration): can Pi be transacted in-app TODAY?** Pi's
   mainnet is enclosed/gated/KYC-migration-bound. Does the **Pi SDK let an app take Pi as payment and
   programmatically hold/accumulate it** now? **Yes →** thesis is live. **Not yet →** it's a "when Pi
   opens" play: build on the SKR rail now, bridge later. *This one answer reshapes the architecture.*
2. **Exposure is the UPSIDE, not the risk.** For an indie game, a huge captive gated audience is the
   scarce thing — Pi *delivers* it. The "dependency" is the mechanism of the exposure, and the downside
   is floored ("worst case, a free worthless coin"). So embrace the Pi exposure as the launch wedge. The
   *only* thing to guard: **keep the game portable + the currency a swappable layer** — don't weld the
   foundation to Pi-specific concrete, so if Pi stalls the *fun* survives and ports. Abstract the rail.
3. **Integration fork — decide EARLY:** Pi-native (Pi SDK, **Pi as buy-in currency**) vs the Solana/SKR
   rail already built → pivot or bridge.

### Two builds, two channels (NOT a mixed bag)

Same game core, **two separate monetization skins** — never crypto features bolted into a store app:

| Build | Channel | Currency | Monetization model |
|---|---|---|---|
| **Pi build** | Pi / web / sideload (off-store) | **Pi** | full thesis — tournaments, buy-in/pot, utility-sink + accumulation |
| **Solana crypto build** | web / sideload (off-store) | **SOL / SKR** | full thesis — tournaments, buy-in/pot |
| **Store build** | Google Play / iOS | **NO crypto — fiat IAP** (Apple/Google billing) | **compliant F2P** — cosmetics + convenience IAP only; no crypto, no tournaments |

> The store constraint is **deeper than currency** — and the safest path is **zero crypto**, not "USDC
> on the store" (USDC is still crypto; Apple trips on it). Apple/Google ban the *mechanic* (real-money
> wagering / buy-in-cash-out, token earning) **and review the binary + bundled SDKs** — a Solana/Pi SDK
> sitting unused can still draw scrutiny. So the store build must **compile the crypto OUT**, not flag it
> off at runtime.
>
> **The modular asmdefs make this clean (3rd payoff of an existing decision):** `DeNelle.Wallet` /
> `DeNelle.Web3` are separate assemblies → the store build **excludes those modules** (build define /
> assembly exclusion) and the crypto + SDKs are *physically absent* from the binary. Compliance by
> construction. So: `CurrencyKind` + feature flags handle Pi vs Solana; the **modular-asmdef strip**
> handles the no-crypto store build.
>
> **Payment is a provider abstraction ("like adding Stripe").** Solana/Pi wallets, Stripe, Apple/Google
> IAP — all pluggable behind one interface. ⚠ **Channel wrinkle:** iOS/Android **mandate native IAP**
> for in-app digital goods (15–30% cut) — you **cannot** use Stripe there. Provider-per-channel: **web →
> Stripe**, **stores → native IAP (forced)**, **crypto builds → wallet**. Match the provider to each
> channel's rules.
>
> **Endpoint-heavy on the SERVER, not just the client** (this is the never-connected backend, WO-107).
> Every provider needs a matching **server-side verifier** — and verification **must** be server-side,
> because clients lie:
> - **Stripe** → payment-intent + **webhook** confirm.
> - **Apple/Google IAP** → **server-to-server receipt validation** (mandatory — clients forge receipts).
> - **Crypto** → RPC + **on-chain tx confirmation** + own verify.
>
> Provider abstraction is **symmetric**: client provider **+** server verifier per channel. "Verify on
> the server, never the client" is the line between a payment system and a free-money exploit.

**This is only cheap to maintain if the currency *and the wager/tournament layer* are swappable
modules, not baked into the loop** — the concrete payoff of "abstract the rail."

**✅ Already built (this was deliberate, not luck):** `WalletService` + `CurrencyKind` (SOL/USDC/SKR)
+ the provider pattern (`StubWalletProvider`) **is** the swappable rail — "select USDC + types" was
the seam for two builds. So the two-build plan is **wiring, not architecture:**
- **Base → Solana** (built). **Store build** = pick USDC + flip the **wager/tournament module to a
  feature flag (off)** — the flag system's first real job.
- **Pi build** = add a **`PiWalletProvider`** behind the *same* `WalletService` interface; the Pi SDK
  is the only genuinely new piece.

**Currency selection + feature flags = the entire two-build machine.**

## What "getting back on vision" means concretely
1. **Build mode** — let the player place/arrange walls, towers, mines on their own plot (give them `VillageSceneBuilder`'s power). **The palette already exists:** the polyperfect pack (WO-101) was chosen *for this* — a huge library of small, mobile-light objects, enough to draw a whole player-built base on a phone. The toolbox was stocked before the loop was reconnected.
2. **Wall tiers** — wood → stone → reinforced, paid from harvest (the CoC sink).
3. **Harvest nodes** — generalize `CrystalMine` → destructible auto-harvest mines you defend (WO-110 → WO-111).
4. **Offline** — mines/pets accrue while away.
5. **Re-sort the side systems** — decide what stays in the loop's orbit vs parks.

The bones are all in the repo. This is a **re-centering, not a rebuild.**
