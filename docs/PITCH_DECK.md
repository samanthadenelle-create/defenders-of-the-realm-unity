# Defenders of the Realm: Echoes of Elarion — Pitch Deck

> **One-line hook:** *Reclaim a fading world, one Knight at a time — a hand-crafted single-hero action-RPG with a living idle economy, built honestly for the dual frontier of Pi Browser and Solana Mobile.*

**Stage of truth (read first):** This deck is grounded entirely in the project's real design legwork and current code state as of **2026-06-28** (branch `wip/village2-and-f8-tickets`). It honestly distinguishes **BUILT** (playable today on a live WebGL build) from **DESIGNED** (specced, architecture-ready, not yet shipped). Web3 value rails, ads, cloud save, and store payments are **architected behind clean provider seams but stubbed/spec-only** — deferred on purpose under a self-paced "build it correct, not fast" decision. We do not overstate. The demonstrable asset is a working single-hero core loop plus an unusually disciplined, scalable engineering foundation.

**Format note:** This is a markdown deck. Each slide gives a **Headline**, an **On-slide** block (bullets + a described visual/mockup), and **Speaker notes**. Visuals are described, not embedded.

---

## Slide 1 — Title / Vision

**Headline:** Defenders of the Realm: *Echoes of Elarion*

**On-slide:**
- A single armored Knight, "Grom," stands at the foot of a glowing world-tree at the center of a dim village. Spirits of light drift up from the roots and fan out to gather wood, iron, and grain.
- Tagline: **"You are the one who holds the line."**
- Subtitle: *A single-hero action-RPG with a living idle economy — for Pi Browser and Solana Mobile.*
- Footer chips: `Unity 6 / URP` · `WebGL live on itch.io` · `Single-developer studio: DeNelle` · `2026`

**Visual:** Hero key-art — close third-person "battle stage" framing: Grom mid-shield-raise, a Tripo-rigged orc winding up an attack to his front, the Heart of Elarion (world-tree) glowing behind. Warm light at the edges, real stakes at the core.

**Speaker notes:** Echoes of Elarion is a cozy-but-serious action-RPG where you control exactly *one* hero and everything else in the world runs autonomously. It pairs the satisfying push-pull of real-time melee with an idle economy that keeps working while you're away — and it's being built deliberately for two emerging, underserved distribution frontiers: Pi Network's 60M+ pioneer base and Solana's Seeker mobile ecosystem. We have a playable core loop today. What we're raising for is the polish-to-launch runway and the phased web3 spike. The whole pitch rests on one disciplined idea: *constrain the design so quality and performance are guaranteed by construction, not bolted on later.*

---

## Slide 2 — Problem / Opportunity

**Headline:** Two huge audiences, almost no game built honestly for them

**On-slide:**
- **Idle-RPG retention is proven** — the global idle-games market was ~$13–14B in 2025, ~61–68% of it on mobile, growing ~9–11% CAGR. Idle RPG is ~15% of the top-1,000 RPG mobile titles. (Illustrative ranges, sourced.)
- **Web3 game trust is broken** — most crypto games are extractive, pay-to-win, ponzi-shaped token farms. Players are exhausted; the "fun-first" web3 game is still rare.
- **Pi Browser is a wide-open storefront** — ~60M users, 15.8M migrated to mainnet, 17.5M KYC'd, but only ~215–253 live apps. *A massive payment-enabled audience with thin quality supply.*
- **Solana Mobile is a real device channel** — Seeker shipped to 150,000+ pre-orders across 50+ countries with a dApp store that bypasses Apple/Google's 30% cut.
- The gap: **a genuinely fun, ethically monetized game that treats web3 as a rail, not the product.**

**Visual:** Two-column "supply vs. demand" chart — left bars show audience size (Pi 60M, Seeker 150k devices, idle market $14B); right bars show *quality supply* (tiny). The whitespace between is labeled "THE GAP."

**Speaker notes:** Three things are simultaneously true. Idle-RPG is a durable, large, mobile-first money-maker. Web3 gaming has burned its own audience with pay-to-win token schemes. And two new ecosystems — Pi and Solana Seeker — have enormous, payment-ready user bases but almost no high-quality, ethical games. We're not betting on a new behavior; we're bringing a proven genre, built with integrity, to channels that are starved for it.

---

## Slide 3 — The Solution (the game in one slide)

**Headline:** One Knight. One living world. A loop that respects your time.

**On-slide:**
- **Control ONE hero** in real-time, animated melee — read the enemy's wind-up, block with your shield, time your strikes and skills. Everything else is autonomous.
- **Reclaim the world:** raid enemy dungeons, outposts, and camps; each victory *permanently* strengthens your world-tree.
- **A living idle economy:** spirit "echoes" released by the tree harvest wood, iron, and grain for you — even while you're offline.
- **Build & grow** your home, your gear, and your workforce from what you reclaim.
- **Ethical monetization:** buy *time and beauty, never power.* Enforced as a build-gate, not a promise.
- **Dual-frontier distribution:** Unity WebGL → Pi Browser; phased Solana Seeker / dApp Store listing.

**Visual:** A four-node loop diagram — **EXPLORE → BATTLE → RECLAIM (tree grows) → HARVEST (offline)** — circling the glowing world-tree, with a small "store: cosmetics & convenience only" tag off to the side.

**Speaker notes:** The whole game is one tight sentence: walk the world, engage an enemy, drop into a real-time battle, win, and your victory makes the world a little brighter and your spirits a little faster. That coupling — combat feeding a persistent idle economy — is the hook. It's active enough to be a real action game and passive enough to be a real idle game, and the monetization never sells advantage.

---

## Slide 4 — Why Now

**Headline:** The two channels just went live — the window is open

**On-slide:**
- **Pi Open Network is live** (open mainnet since Feb 2025); Pi App Platform, JS SDK v2.0, Payments API, and Ads are all operational. The storefront exists *today* and is hungry for apps.
- **Solana Seeker is shipping** (150k+ units, 50+ countries, Seed Vault hardware wallet + dApp store) — a real device-level crypto-native distribution channel.
- **Web3 gaming is maturing** past the 2021–22 ponzi era; the market is rewarding fun-first, ownership-light, ethically-monetized titles.
- **Unity 6 + WebGL + Addressables** make a small studio capable of shipping a streaming, mobile-web game from one codebase.
- **First-mover quality advantage:** thin app supply on Pi means a polished game stands out disproportionately.

**Visual:** A timeline ribbon — "Feb 2025: Pi Open Network" → "Aug 2025: Seeker ships" → "2026: our V1 polish + web3 spike" → "Listing window." A vertical "WE ARE HERE" marker on 2026.

**Speaker notes:** Timing is the argument. A year ago neither channel was real. Now both are live and under-supplied. We don't need to build a marketplace or convince anyone to install a wallet — Pi and Seeker did that work. We just need to be the rare *good* game that shows up while the shelves are empty.

---

## Slide 5 — Product / Gameplay: the core loop

**Headline:** The single-hero loop — and the content engine behind it

**On-slide:**
- **Combat = animation-as-mechanics.** One fight at a time + a close third-person "battle stage" camera means every animation is load-bearing: the enemy's attack wind-up is a *readable telegraph*, and the shield-block is a real mechanic, not decoration. (BUILT — verified end-to-end.)
- **Isolated battle arenas via warp.** You engage a roaming enemy and *pop* into a dedicated, code-built real-time arena (Knight vs. an orc family); win returns you exactly where you were. No fragile cross-region seams. (BUILT.)
- **"One space primitive, three skins."** Dungeons, enemy outposts, and captured player camps are the **same** arena space, differentiated only by *data* — a `skin`, a `spawn-set`, and an `ownership` flag. Clear a garrison and the same space re-dresses in place as your forward camp. (DESIGNED, ratified 2026-06-28 — *zero new combat code*.)
- **Modular dungeon generator.** A seeded, self-avoiding "winding path" generator emits a recipe JSON the existing composer builds; one `budget` scalar (depth × player level) scales room count, twistiness, enemy count, levels, and healer density at once. Reachability is an *engine guarantee* (BFS + navmesh validation), not a hope. (DESIGN-COMPLETE, queued behind V1.)
- **Idle echo economy.** The world-tree births autonomous "echo" spirits (workforce cap 5: 3 organic + 2 flex). One interaction — drag-drop a spirit onto wood / iron / grain — then it's passive and gathers even offline. (BUILT at 1–4 echoes w/ offline real-clock accrual; 5-cap drag-assign is the target.)
- **Life-force coupling.** Driving enemies back strengthens the tree; a stronger tree harvests faster. *Offense becomes persistent world progress, not transactional loot.* (DESIGNED — the keystone direction.)

**Visual:** Split panel. Left: the battle-stage screenshot with a labeled "TELEGRAPH → BLOCK → STRIKE" callout over the orc's wind-up. Right: the "one primitive, three skins" diagram — a single arena box with three swappable data cards (Dungeon / Outpost / Player-Camp) feeding it, plus a `budget` dial driving a procedurally-snaking dungeon map.

**Speaker notes:** This is where the design discipline pays off. Because you only ever control one hero, we made combat *feel* deep through animation and timing instead of through party-management complexity. And because every combat space is the same underlying primitive driven by data, our content cost is near-flat: a new dungeon, outpost, or camp is a data entry plus art, not new engineering. The dungeon generator turns that into infinite, difficulty-scaled content from a single budget number — and it *can't* emit an unbeatable layout, because reachability is validated by the engine. That's the content-scalability story: a one-person studio with a AAA-shaped content pipeline.

---

## Slide 6 — Art & UX Direction

**Headline:** Build the chrome once. Reuse it everywhere.

**On-slide:**
- **"Obsidian UI" master-frame formula.** A single factory builds *every* panel from one black-and-gold sprite frame; screens drop chrome-less content into pre-styled zones (header / body / medallion / footer) and bind their model. Layout is tuned in exactly one place per frame. (BUILT — formula + seam solid; sprite mirroring in progress.)
- **Null-safe by construction.** Every art lookup falls back gracefully to a procedural black-gold panel — a screen *can never blank*. Gated by `ff.blinkchrome`, correct in both ON/OFF states.
- **Crown reward tiers.** Clash-of-Clans-style victory crowns (tier 1/2/3 + "perfect/flawless") render post-battle; premium cosmetics only ever swap the crown's *skin*, never power.
- **One hero model, visible flair.** A single polished Tripo-rigged Knight — weapon and shield are the *visible* upgrade slots; armor is static stat-only; rings/amulet/boots are invisible build-depth. No mesh-swapping, ever.
- **Production efficiency = the art story.** Shared humanoid rig + one retargeted clip set; mesh-baking 6 sections → 1; aggressive texture compression for WebGL/mobile.

**Visual:** A grid of four different game panels (Store / Character / Dialogue / Victory) that are visibly the *same* frame with different content — captioned "1 factory → N screens." Below it, the victory screen with a gold crown-tier row.

**Speaker notes:** Art and UX are usually where small studios bleed time. We turned UI into an engineering asset: one master frame, drop-in content zones, and a guarantee that a panel can never render blank. That means new screens are near-free and visually consistent. The hero pipeline is the same philosophy — one model, one rig, one clip set, retargeted across enemies — so every new character walks, attacks, and dies "for free." This is how one developer ships a coherent, polished-looking game.

---

## Slide 7 — Market & Audience

**Headline:** A large proven genre, intersected with two starved ecosystems

**On-slide:**
- **TAM — Idle / casual-RPG mobile gaming:** ~$13–14B (2025), growing to ~$25–35B by 2034 (~9–11% CAGR); ~61–68% mobile. *(Illustrative, sourced ranges.)*
- **SAM — Web3-distributed game audience we can actually reach:** Pi (~60M users / 15.8M mainnet / 17.5M KYC, ~215 live apps) **+** Solana Mobile (150k+ Seeker devices, 50+ countries) **+** crypto-curious idle-RPG players.
- **SOM (realistic early beachhead):** a few thousand to low-tens-of-thousands of engaged Pi/Seeker players in year one — winnable precisely *because* app supply is thin and our quality bar is high.
- **Audience psychographic:** idle-RPG comfort players + crypto-native early adopters who want a game that's *fun first* and doesn't try to extract from them.

**Visual:** Classic TAM/SAM/SOM nested-circles funnel, each ring labeled with the numbers above and a "sourced — illustrative" footnote.

**Speaker notes:** Be honest about the funnel. The *genre* is a multi-billion-dollar, mobile-dominant, growing market — that's the TAM and it's real. Our serviceable market is the overlap of idle-RPG appetite with the Pi and Seeker user bases that are payment-ready but under-served. Our realistic year-one obtainable market is modest and we'd rather under-promise: a few thousand engaged players on a thin-supply storefront is a winnable, fundable beachhead, not a moonshot.

---

## Slide 8 — Business Model

**Headline:** A full monetization suite — bound by an ethical covenant

**On-slide:**
- **Resource & boost packs** — buy wood/iron/grain bundles, timed harvest multipliers (2×/3×, capped at 5× effective), workforce slots (to the cap of 5), and storage-tier jumps. Grind path always free; premium is a *fast-track*.
- **Offline storage upgrades** — a 5-tier ladder (silo 4h→18h, offline window 10h→36h) bought with soft currency or SKR fast-track; a 15-SKR "Spirit Surge" instant-fill time-skip.
- **SKR premium store ("the Coffer")** — held-token store for cosmetics, convenience, and scarce prestige crowns/skins (5–1000 SKR bands).
- **Battle Pass + cosmetics** — seasonal (~35-day) free + premium lanes; *XP earned only by playing — never buyable.* Buying the pass unlocks the lane, not the tiers.
- **Monthly card** — pay once → daily reward drip + a month-exclusive cosmetic up front; missed days never lost (pool model).
- **Rewarded ads** — opt-in only, each ad grants what a spender could buy; monetizes the ~95–98% who never pay, "a path, never a wall."
- **THE COVENANT (trust differentiator):** *cosmetic + convenience only. Never combat stats, never higher caps, never pay-to-win.* Enforced by a **build-gate regression** — a `combat`/stat grant literally fails the build.

**Visual:** A "monetization stack" diagram — six revenue streams as horizontal bars feeding one funnel, with a glowing red "NO PAY-TO-WIN" gate filtering the output and a small "enforced in CI" badge.

**Speaker notes:** This is a complete, conventional free-to-play monetization suite — packs, boosts, storage, a season pass, a monthly card, cosmetics, and rewarded ads — so the revenue surface is broad. What makes it defensible is the covenant: money buys *time and beauty, never power.* And crucially, that's not a marketing line — it's enforced in code. Our content validator rejects any combat or stat-granting item; the build literally won't compile. In a genre infamous for pay-to-win, "we made it impossible to sell power" is a trust moat.

---

## Slide 9 — Tokenomics: SKR

**Headline:** SKR — a utility token designed to be sustainable, not a ponzi

**On-slide:**
- **SKR is a player-held premium balance** with a real-money / crypto acquisition path — *the only currency with one.* Soft resources (wood/iron/grain/gold) stay earnable, no wallet required.
- **Three acquisition paths, all optional:** (1) bought with money/crypto, (2) *earned* via play/achievements (e.g. first wave 0.5 SKR, first dungeon 2 SKR), (3) on-chain later. **No wallet is required to play or hold SKR.**
- **SINKS (where SKR goes):** the SKR store — cosmetics (5–80 SKR), convenience time-savers (10–60 SKR), curated packs (100–300), scarce prestige crowns/skins (250–1000). Spending *removes* SKR from circulation.
- **HOLDS (staking):** capped, sustainable staking — designed as a measured, bounded yield, **not** an inflationary emissions farm. (Note: an earlier staking spec granted production/XP buffs; the current covenant restricts holds to non-power benefits — we lead with the sustainable, cosmetic/convenience-bounded model.)
- **Staged custody:** **Stage 1** local ledger (no chain) → **Stage 2** cloud → **Stage 3** real Solana SPL token. Each stage is one swappable seam; V1 ships fully local.
- **Anti-ponzi by design:** value comes from *spending in a fun game*, not from recruiting buyers; no forced wallet, no emissions treadmill, no randomized loot-box SKR spends.

**Visual:** A circular-flow tokenomics diagram — acquisition (money / play / chain) → SKR balance → sinks (store) and bounded holds (staking) → "removed from circulation." A side panel shows the 3-stage custody ladder (Local → Cloud → Solana) as nested seams.

**Speaker notes:** SKR is deliberately boring in the best way. It's a utility balance you spend on cosmetics and convenience inside a game that's fun without it. The token isn't the product. We don't require a wallet, we don't pay people to recruit, and we don't run an emissions farm — the primary token motion is a *sink* (you spend it and it leaves circulation). Staking exists but is capped and sustainable, not a yield treadmill. And the on-chain piece is staged: V1 is a local integer behind one interface, so we can prove the game and the economy long before we ever touch the chain. That sequencing is the risk management.

---

## Slide 10 — Distribution & Go-to-Market

**Headline:** Two rails, one codebase, phased rollout

**On-slide:**
- **Rail 1 — Pi Browser (primary near-term).** Unity WebGL build at a validated domain + a JS-SDK bridge (`Pi.authenticate`, `Pi.createPayment`, `Pi.Ads`). Payments use a mandatory server-side approve/complete handshake. Pi is *a rail, not an economy* — Pi buys SKR exactly like USD/SOL do.
  - **Phase 0:** WebGL viability spike on a real phone (the #1 technical risk — mobile WebGL performance).
  - **Phase 1:** auth-only (no money). **Phase 2:** sandbox → mainnet payments. **Phase 3:** dApp listing + optional rewarded ads.
- **Rail 2 — Solana Seeker / dApp Store.** Native-feeling crypto distribution that bypasses the Apple/Google 30% cut; Seed Vault wallet already on-device. Lists the same build with the Solana payment seam activated.
- **Rail 0 — already live:** a WebGL build is **public on itch.io today** for open feedback.
- **Why phased:** each step is owner-gated; we never light up money before the rail is proven.

**Visual:** A pipeline graphic — one Unity WebGL artifact branching to three lanes (itch.io NOW / Pi Browser PHASED / Seeker dApp Store) — with a phase ladder (0→3) under the Pi lane and "30% platform cut bypassed" tags on Pi and Seeker.

**Speaker notes:** One build, multiple storefronts. We're already live on itch.io for raw feedback. Pi Browser is the near-term commercial target because the storefront and 60M-user audience already exist and we sidestep the app-store tax — but we're honest that mobile WebGL performance is our biggest unknown, which is exactly why Phase 0 is a viability spike before we build anything Pi-specific. Seeker is the second rail: same codebase, Solana seam switched on, distributed through a device that ships with a wallet. The phasing is the discipline — we never enable payments on a rail we haven't proven.

---

## Slide 11 — Traction & Roadmap

**Headline:** A real loop today, a phased path to the chain

**On-slide:**
- **BUILT & playable now:** single-Knight real-time combat (verified end-to-end), isolated warp arenas, wave loop, shared rig/animation pipeline, 1–4-echo idle economy with offline real-clock accrual, ~30-class data/catalog spine, save/migration system, build mode (~70% for towers), store scaffolding (devnet-stubbed), and a **live WebGL build on itch.io.**
- **Engineering maturity (traction of a different kind):** asmdef-enforced bounded architecture, headless `DataRegression` gate, an instrumentation-first QA culture (FlowTrace / Guard / F8 break-log flight recorder + a headless AutoPilot test fleet).
- **Roadmap:**
  1. **V1 polish** — Knight perfected; lock-on + 9-zone HUD; Obsidian UI restyle; dungeon/outpost/camp consolidation.
  2. **Content spike** — winding dungeon generator + 10–11 designed dungeons online.
  3. **Web3 spike** — Addressables-remote streaming (shrink the build) → Pi auth → Pi payments → SKR store → cloud save.
  4. **Distribution** — Pi dApp listing → Solana Seeker listing → on-chain SKR (last).
- **Honest note:** the grant we targeted (Pi2Day 2026) passed un-awarded; we pivoted to self-paced "correct, not fast." Pi celebrates **Pi2Day each June** and **Pi Day each March** — natural recurring launch/visibility windows we can aim a release at.

**Visual:** A two-band Gantt — top band "BUILT" (green, several bars already filled), bottom band "ROADMAP" (V1 polish → content → web3 spike → distribution) with the Pi2Day/Pi Day markers on the timeline.

**Speaker notes:** Traction here is a working game and an unusually mature engineering base for a solo studio, not revenue — and we won't pretend otherwise. The core loop is playable on a live build right now. The roadmap is deliberately sequenced so the riskiest, most-deferred pieces (payments, chain) come *after* the game is proven and the build is small enough to stream to a phone. We missed the Pi2Day 2026 grant window, which is exactly why we're here — but Pi's June (Pi2Day) and March (Pi Day) events recur annually and give us natural launch beats to target with the right runway.

---

## Slide 12 — Competition / Moat

**Headline:** What's defensible

**On-slide:**
- **The modular content engine.** "One space primitive, three skins" + a budget-driven, reachability-guaranteed dungeon generator means our content cost is near-flat. Competitors hand-build levels; we *generate validated, difficulty-scaled* ones from data. (A solo studio with a AAA-shaped content pipe.)
- **The ethical web3 model as a brand.** "Buy time and beauty, never power" — *enforced in CI.* In a category defined by pay-to-win distrust, provable fairness is a marketing and retention moat.
- **Dual-ecosystem distribution.** One Unity WebGL codebase reaching *both* Pi Browser and Solana Seeker — most studios pick one chain; we ride two under-supplied storefronts from a single build.
- **Engineering discipline as velocity.** Bounded asmdefs, data-driven catalogs, provider seams (ads / cloud / Solana already stubbed), instrumentation-first QA — we add features by data entry, not rewrites.
- **Art-production leverage.** One master UI frame, one shared rig — coherence and polish at a fraction of the usual labor.

**Visual:** A 2×2 positioning map — axes "Fun-first ↔ Extractive" and "Single-chain ↔ Multi-ecosystem." We sit top-right (fun-first + multi-ecosystem); typical web3 games cluster bottom-left.

**Speaker notes:** Our moat isn't a single feature — it's a *production model*. The content engine lets one person generate validated, scaling content. The covenant turns ethics into a defensible brand in a market starving for trust. The dual-rail distribution doubles our reachable audience from one codebase. And the architecture means we ship by adding data, not rewriting code. Any one of those is nice; together they're how a small team competes on content volume *and* trust *and* reach.

---

## Slide 13 — The Ask

**Headline:** Funding the polish-to-launch runway and the web3 spike

**On-slide:**
- **What we're raising for** (grant / ecosystem-funding shaped):
  - **Pi ecosystem support** (developer grants / Pi2Day & Pi Day programs, App Studio visibility).
  - **Solana / Seeker developer grants & hackathons** (dApp Store onboarding, on-chain SKR integration).
  - **Web3 gaming hackathons & accelerators** for non-dilutive runway and distribution partners.
- **Use of funds (illustrative split):**
  - ~40% — **Production & content:** finish V1 polish, dungeon generator + 10–11 dungeons, Knight/enemy art.
  - ~25% — **Web3 + backend:** Pi payment backend, Addressables-remote CDN, cloud save, Solana SKR integration + audit.
  - ~20% — **Distribution & UA:** Pi/Seeker listings, launch around a Pi2Day/Pi Day beat, community.
  - ~15% — **Ops & contingency.**
- **What a backer gets:** an early, polished, ethically-monetized flagship title on two under-supplied storefronts — exactly the kind of quality these ecosystems need to attract users.

**Visual:** A use-of-funds donut (40/25/20/15) beside a stacked list of named funding targets (Pi grants, Solana/Seeker grants, hackathons).

**Speaker notes:** We're structured to be grant- and ecosystem-funded rather than chasing large equity rounds — the project is self-paced and the channels (Pi, Solana) actively fund the kind of quality apps they're short on. The money buys two things: finishing the game to a polished V1, and executing the staged web3 spike with a real payment backend and audited on-chain integration. The pitch to an ecosystem funder is simple: we're the flagship-quality, trust-first game your storefront needs to convert your installed base into players.

---

## Slide 14 — Financial Model (illustrative)

**Headline:** Conservative, transparent assumptions — labeled as illustrative, not promises

**On-slide — ASSUMPTIONS TABLE:**

| Lever | Conservative | Base | Optimistic |
|---|---|---|---|
| Reachable installs (Y1) | 10,000 | 40,000 | 120,000 |
| Monthly active (of installs) | 25% | 30% | 35% |
| Paying conversion (of MAU) | 2.0% | 3.0% | 4.5% |
| ARPPU / month (packs+pass+SKR) | $6 | $9 | $14 |
| Ad ARPU (non-payers, /MAU/mo) | $0.08 | $0.12 | $0.20 |
| Pack mix | resource/boost 45% · storage 15% · season pass 20% · cosmetics/SKR 15% · monthly card 5% | — | — |

**Illustrative annualized revenue (rounded):**

| Scenario | IAP (paying) | Ads (non-payers) | **Total Y1 (illustrative)** |
|---|---|---|---|
| Conservative | ~$3.6k | ~$2.7k | **~$6k** |
| Base | ~$39k | ~$16k | **~$55k** |
| Optimistic | ~$255k | ~$95k | **~$350k** |

*(Base example math: 40k installs × 30% MAU = 12k MAU; 3% pay × $9 × 12 mo ≈ $39k IAP; 12k MAU × $0.12 × 12 ≈ $17k ads.)*

**On-slide caveats:**
- **These are illustrative scenario models, not forecasts or promises.** Pre-revenue today.
- Conversion/ARPPU bands reflect *ethical, no-pay-to-win* F2P norms (lower whale-skew by design); ad revenue intentionally carries the non-paying majority.

**Visual:** A three-bar scenario chart (Conservative / Base / Optimistic total revenue) with IAP vs. Ad split stacked, stamped "ILLUSTRATIVE — assumptions above."

**Speaker notes:** I want to be very clear these are illustrative scenario sketches, not projections — we're pre-revenue. The assumptions are deliberately conservative for an ethical F2P game: our no-pay-to-win stance trades whale extraction for broader, fairer conversion and leans on rewarded ads to monetize the silent majority. The model's purpose is to show the *shape* of the economics and that the unit levers are sane, not to promise a number. Even the base case is a modest, believable beachhead — which is the honest story for a thin-supply storefront in year one.

---

## Slide 15 — Team / Closing / Contact

**Headline:** Built with discipline. Shipped with integrity.

**On-slide:**
- **Studio:** DeNelle — a focused single-developer studio operating with B2B-grade engineering discipline (bounded-context architecture, instrumentation-first QA, "quality not speed" as a binding rule).
- **What we've proven:** a playable single-hero action-RPG core loop on a live WebGL build, on an architecture engineered to scale content by data and to slot in ads / cloud / Solana behind clean seams.
- **What we're asking:** ecosystem funding + partnership to finish V1 and execute the phased Pi + Seeker web3 launch.
- **The vision:** be the trustworthy, genuinely fun flagship that brings idle-RPG players into the Pi and Solana ecosystems.
- **Contact:** DeNelle Studios — *[contact / domain / itch.io build link]*

**Visual:** Closing key-art — the world-tree now bright and full, spirits swirling, the Knight silhouetted at its base looking outward to a reclaimed horizon. Caption: *"Hold the line. Reclaim the world."*

**Speaker notes:** We're a small studio that chose to build correctly instead of fast, and it shows — in a working loop, a scalable content engine, and an architecture already shaped for the web3 future without betting the game on it. The ask is partnership to cross the finish line and light up the two storefronts that need exactly this kind of game. Thank you.

---

## Appendix A — Risks & Mitigations

| # | Risk | Likelihood | Mitigation |
|---|---|---|---|
| 1 | **Mobile WebGL performance on Pi Browser** (single-threaded WASM, memory caps, large download) — the stated #1 technical risk. | High | Phase-0 viability spike on a real phone *before* any Pi-specific work; Addressables-remote streaming to ship a tiny base bundle; bounded agent counts + AI tick-throttling make the game cheap by construction. |
| 2 | **Solo-developer bandwidth / bus factor.** | High | Disciplined bounded architecture + instrumentation-first QA + headless regression fleet reduce regressions; grant funding to extend runway / add contract help. |
| 3 | **Token regulatory & custody risk (SKR on-chain).** | Med | Chain is the *last* stage; V1 fully local with no wallet; covenant keeps SKR a utility sink, not a security-shaped yield instrument; on-chain integration audited before mainnet. |
| 4 | **Pi/Seeker ecosystem or grant momentum stalls.** | Med | One codebase, multiple rails (itch.io live now, both chains optional seams) — not dependent on any single channel; soft-currency game is fully playable with zero web3. |
| 5 | **Monetization underperforms ethical (no-P2W) model.** | Med | Broad revenue surface (packs/pass/monthly/ads) + rewarded ads carry non-payers; cosmetics/prestige crowns give expression-spend headroom without selling power. |
| 6 | **Documentation/spec drift vs. build reality.** | Med | Canon ground-truth anchor + same-breath update rule; build-gate regressions enforce design invariants (e.g. no-combat-grant) in CI. |
| 7 | **Content fatigue / retention.** | Med | Budget-driven dungeon generator yields scaling validated content; life-force coupling makes progress persistent; idle layer drives return visits. |
| 8 | **Two-build complexity (store vs. crypto).** | Low | Compile-time assembly separation already in place — crypto code physically absent from store builds and vice-versa. |

---

## Appendix B — Elevator Pitch & 30-Second Verbal

**One-paragraph elevator pitch:**
*Echoes of Elarion is a cozy-but-serious single-hero action-RPG with a living idle economy, built for two under-supplied frontiers: Pi Browser's 60M-user storefront and Solana's Seeker phone. You control one Knight in real-time, animation-driven combat — read the wind-up, block, strike — and every victory permanently strengthens a world-tree whose autonomous spirit "echoes" harvest resources for you, even offline. It's monetized with a full free-to-play suite (packs, boosts, a battle pass, a monthly card, cosmetics, rewarded ads) under one hard rule enforced in code: buy time and beauty, never power. A playable core loop is live on the web today; web3 payments and on-chain SKR are architected behind clean seams and staged to switch on only after the game is proven. We're raising ecosystem grants to finish V1 and execute a phased Pi + Seeker launch — to be the trustworthy, genuinely fun flagship these chains are starving for.*

**30-second verbal pitch:**
*"Most web3 games are pay-to-win token farms players don't trust — and Pi and Solana Seeker have tens of millions of payment-ready users but almost no good games. We're fixing both. Echoes of Elarion is a single-hero action-RPG with a living idle economy: you fight one Knight in real-time, and every win makes your world-tree stronger so its spirits harvest more for you while you're away. We monetize fairly — cosmetics and convenience only, no pay-to-win, enforced in our build pipeline. The core loop is playable on the web today; the web3 rails are staged to switch on after the game's proven. We're raising ecosystem grants to finish it and launch on Pi and Seeker — one codebase, two storefronts that need exactly this."*

---

### Sources (market & platform figures — illustrative ranges)
- Idle / mobile games market sizing: [Dataintelo](https://dataintelo.com/report/idle-games-market), [Growth Market Reports](https://growthmarketreports.com/report/idle-games-market), [Sensor Tower — Top Idle RPGs](https://sensortower.com/blog/2025-q2-android-top-5-idle%20rpg-revenue-us-6040bbc2241bc16eb883a0cb)
- Pi Network users / mainnet / apps: [AInvest — Pi reaches 60M](https://www.ainvest.com/news/pi-network-reaches-60-million-users-awaits-mainnet-launch-2506/), [Coinfomania — 2025 recap](https://coinfomania.com/pi-network-2025-user-recap-mining-referrals/), [Gate — ecosystem 2025](https://www.gate.com/crypto-wiki/article/how-active-is-the-pi-network-community-in-2025)
- Solana Seeker shipping / pre-orders: [CoinMarketCap](https://coinmarketcap.com/academy/article/solana-news-solana-seeker-phone-ships-to-150000-pre-orders-globally), [Cryptopolitan](https://www.cryptopolitan.com/solana-mobiles-seeker-smartphone/)

*Project facts sourced from repo design legwork: `docs/COMBAT_PIVOT_NORTHSTAR.md`, `WorkOrders/WORK_ORDER_584_dungeon_outpost_arena_consolidation.md`, `WORK_ORDER_485_winding_dungeon_generator.md`, `WORK_ORDER_482_*`, `WORK_ORDER_skr_store_design.md`, `WORK_ORDER_pi_browser_integration.md`, `WORK_ORDER_offline_storage_logic.md`, `WORK_ORDER_economy_store_packs.md`, `WORK_ORDER_battle_and_monthly_packs.md`, `docs/UI_BLINK_TEMPLATE_CANON.md`, `docs/DATA_ARCHITECTURE_DECISION_2026-06-27.md`, `docs/ARCHITECTURE.md`, `CANON_GROUND_TRUTH_2026-06-26.md`, `PIPELINE_STATE.md`, `docs/MASTER_CATALOG.md`. BUILT-vs-DESIGNED status reflects code state on branch `wip/village2-and-f8-tickets` as of 2026-06-28.*
