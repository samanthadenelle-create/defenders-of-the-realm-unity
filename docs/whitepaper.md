# Defenders of the Realm — White Paper

**Version:** 1.0
**Date:** 2026-05-18
**Publisher:** DeNelle Studios
**Lead developer:** Samantha Denelle
**Publisher wallet (Solana):** `C5ummRoS1bB73gnBC57VqpGfD9QjM9g1iv3vc7cDbgYQ` _(Seeker Seed Vault — hardware-backed at birth; publisher identity + grant receipt only. 1M SKR stake is held separately in an owner-private wallet — see `docs/wallets-of-record.md` §1.1.)_
**Live build:** https://defenders-of-the-realm.vercel.app

---

## Abstract

Defenders of the Realm is a mobile-first 3D tower-defense game with native Solana integration, built for the Solana Mobile dApp Store and the Seeker phone. It pairs a cozy, story-driven gameplay loop in the Studio Ghibli register with first-class crypto payment rails (SKR, SOL, USDC, and Stripe USD) and a self-sustaining player-rewards economy funded by staking yield, not principal. The game's guiding covenant — _"you are never required to spend anything, ever"_ — holds throughout: paying buys time and beauty, never combat advantage.

This document is the comprehensive overview of the project: vision, design philosophy, technology architecture, economic model, security posture, anti-cheat strategy, development roadmap, team, risk register, and the grant story we present to the Solana Foundation. Everything here links back to specific specs in `docs/` that contain the implementation detail.

---

## 1. The vision in one paragraph

> _In an old valley, an old tree. In the dark beyond, a slow cold rot. You are the Keeper. The song is yours now._

You tend a sentient crystal-veined tree called **Elarion** — the Heart-Grove — at the center of a small valley. The **Hollow Ones**, silent former villagers unmade by the slow rot called **the Withering**, march on the Heart every wave. You defend with towers, walls, three bonded pet spirits, and your own mage / knight / ranger abilities. The Heart is the only barrier between the remembered world and a cold silent one closing in. When your defenses break, the camera cuts to a turn-based last-stand combat screen for an epic close-quarters resolution. Between waves you walk dungeons, follow questlines, repair what the dark broke, and — eventually — sit at the edge of the Wound itself for a single conversation with the figure who fell before you.

The game's tone is cozy at the edges, real stakes at the core. The Keeper does not hate the Hollow Ones — she mourns them, even while ending them.

The full narrative bible is at `docs/narrative-bible.md`. The storyline arc is at `docs/dungeons-storyline.md`.

---

## 2. Design philosophy — the cozy covenant

Seven non-negotiable design constraints govern every product decision (`docs/monetization-design.md` §1):

1. **Free-to-play first.** Every piece of content is reachable without spending. Payment shortens time or sells expression — never gates progress.
2. **Never required to spend, ever.** A public covenant; lifted directly from the developer's word.
3. **No loot boxes, no gacha, no randomized purchases.** Every price is known before you pay.
4. **No energy systems, no FOMO countdowns, no dark patterns, no whale-shaming.**
5. **No gameplay interruption.** The store is always player-initiated; offers never pop up mid-run.
6. **Generosity over extraction.** The gift mechanic — recipient always gets free value — is the monetization template.
7. **Cozy tone.** Tending a Lantern, not conquest. The Hollow Ones are grief, not Sauron. A purchase flow that feels grabby is itself off-tone.

The one constraint **explicitly bent in v1** is constraint #1 — the convenience-power layer of the pack system sells time-saving items (instant-build, instant-repair, XP boosts). The covenant rewrites as: _"You are never required to spend anything. Ever. And when you do, you cannot buy victory — only time and beauty."_ Combat stats remain forever off the table. A rip-out path exists if community feedback requires it (see `docs/monetization-v2-spec.md` §2 and §11.1).

---

## 3. Why Solana — why now

Three reasons this project is built for Solana specifically, not for crypto-in-general:

### 3.1 The Solana Mobile dApp Store needs flagship games

The dApp Store curates apps that exercise the Seeker phone's native capabilities — Mobile Wallet Adapter (MWA), SKR token integration, on-chain payments. A polished mobile-first tower-defense game with native SKR integration raises the catalog's quality bar. The Foundation has stated interest in featured apps that demonstrate ecosystem alignment beyond "uses Solana wallets for sign-in."

### 3.2 SKR utility, not just token receipt

Most Solana games accept tokens as payment. This one **operates on the SKR yield curve.** 1 million SKR staked by the developer funds an ongoing player-rewards economy (achievement drops, weekly leaderboard contests, seasonal tournaments) — yield only, principal preserved. SKR isn't just received; it's actively circulated to players who then spend some of it back through packs. The flywheel is closed. This is a meaningfully new utility pattern for the token.

### 3.3 Mobile-first gaming is undersupplied in Solana

The desktop Solana gaming ecosystem is competitive. Mobile is less crowded. A game that ships on Seeker first and Android-broad second can establish position before the crowd arrives. Our timing — building during a low-saturation moment in the Seeker app catalog — is intentional.

---

## 4. The game loop — what players actually do

### 4.1 The core gameplay

Each session looks like this:

```
[VILLAGE 3D — PREP PHASE]
   ↓
Build towers · raise walls · upgrade buildings · deploy pets
Walk the village · harvest crystals · check questlines
   ↓
[WAVE INCOMING]  alert + countdown
   ↓
[VILLAGE 3D — WAVE]
   ↓
Enemies spawn at edges and walk toward the Heart
Towers fire · walls block · pets defend · hero casts Q/F/E/R abilities
   ↓
   ┌─────────────────────────────┬────────────────────────────────┐
   │ DEFENSES HOLD               │ AN ENEMY BREACHES THE WALL     │
   ▼                             ▼
[WAVE CLEAR]                  [ATB LAST STAND — turn-based]
Minor damage from breach      Hero + bonded pets vs. surviving enemies
Loot popup                    Win → minor damage; Lose → major damage
Prep phase resumes            Damage Report modal · repair flow
                              Prep phase resumes
```

A breach is a real consequence — buildings take damage that must be repaired between waves. The Heart-bound force-field gate (`docs/gate-design-spec.md`) gives the player a controlled entry/exit point that itself takes damage and must be repaired.

### 4.2 Dungeons

When the Hollowmouth portal opens (Wave 8), the player can step into the realm beyond the valley. Dungeons are SVG-rendered top-down explorers — a different verb from the 3D village. Each dungeon is a hand-painted "postcard of a forgotten corner of Elarion": rooms, lore stones, chests, NPC dialogues, encounter triggers that hand off to the same battle system as breaches.

Six locked questlines weave through the dungeons (`docs/dungeons-storyline.md`):

- _The Healer's Garden_ — Alduin the Mournful was a healer, once
- _The Folk Who Forgot_ — the Hollow Ones were villagers
- _The Wolfwarden's Vigil_ — the Ice Wolf's first night in the valley
- _The Cold-Wandered's Pack_ — where the Ice Wolf came from, why it stayed
- _The Last Keeper's Walk_ — the previous Keeper's letters
- _At the Edge_ — the final conversation, four canonical responses

The endgame doesn't end the game — it locks the Hollowmouth and continues the village's watch. The Keeper writes a letter that persists to the next Keeper through New Game+.

### 4.3 What's deferred to v2

Real-time multiplayer combat, asynchronous PvP, an in-game economy with player-to-player trading, custom-painted dungeons by players. All viable; all out of v1 scope. The full v2 roadmap is at `docs/v2-roadmap.md`.

---

## 5. The technology architecture

### 5.1 Stack

- **Frontend:** React 19, TypeScript, Vite 6
- **3D scene:** Three.js with @react-three/fiber and drei
- **State:** Zustand stores, persisted to localStorage via a versioned schema with migration
- **Audio:** Single AudioManager singleton with crossfade between title / village / battle tracks
- **Backend:** Hono on Cloudflare Workers (after the Poof platform removal — see §5.3); Vercel Postgres for entitlements + game stats + leaderboards; ArDrive for permanent asset hosting
- **Wallet:** Mobile Wallet Adapter (MWA) for Seeker; Phantom / Solflare / Backpack on desktop
- **Build:** TWA-wrapped APK via Bubblewrap for the dApp Store; PWA + Vercel for web
- **CI/CD:** GitHub Actions (post-refactor); Vercel auto-deploy on `main`

### 5.2 Feature-module architecture

The codebase is organized as isolated feature modules. Top-level structure (`docs/refactor-feature-modules-spec.md`):

```
src/
  core/         — App shell, routing, providers
  modules/      — Feature modules, one folder each
    player/, village/, battle-atb/, battle-tower-sim/,
    dungeons/, pets/, clans/, chat/, wallet/
  contracts/    — Pure TypeScript type shapes (no runtime)
  ui/           — Reusable presentational primitives (SkillNode, GameTooltip)
  assets/       — Static asset registries
  services/     — External boundaries (DB, auth, persistence, payments)
  state/        — Zustand stores + save schema
  content/      — Static content (story, tooltips, quests, dungeons)
```

Three hard rules govern module interaction:

1. **No module imports another module's runtime.** Shared type shapes live in `contracts/`; cross-module coordination flows through `state/` or `services/`.
2. **`ui/`, `services/`, `assets/` are leaves** — they never import from `modules/`. Modules are branches; `core/` is the trunk.
3. **Every module has an `OWNERSHIP.md`** declaring what it owns, what it may consume, and what it may never touch.

The largest architectural decision in v1 was the refactor itself. The pre-refactor codebase had a 9,000-line `Village3D.tsx` god-file that one Claude Code session accidentally truncated mid-statement. The refactor split that into the feature-module structure above. **No file in the codebase should now exceed 500 lines for logic / 700 for rendering orchestrators.**

### 5.3 The Poof rip-out

The project initially used the Poof Cloud platform (Pooflabs' hosted auth + DB + AI). On 2026-05-17 the project decided to operate independently of any platform — the Poof removal landed as commit `7a3ae89e1` and removed all `@pooflabs/*` dependencies, replacing them with local shims and Cloudflare Workers infrastructure. The game now runs on its own infrastructure end-to-end. See `docs/poof-removal-overnight-spec.md` for the migration spec.

---

## 6. The economic model

### 6.1 Money in — five-tier pack ladder, four currency rails

`docs/monetization-v2-spec.md` defines the canonical economic system. Five themed packs at mobile-game-standard tier psychology:

| Tier | Pack name                          | USD / USDC | SOL   | SKR |
| ---- | ---------------------------------- | ---------- | ----- | --- |
| 1    | Hearth Spark                       | $1.99      | 0.018 | 25  |
| 2    | Lanternlight                       | $4.99      | 0.045 | 60  |
| 3    | Folk's Thanks                      | $9.99      | 0.09  | 120 |
| 4    | Patron of Elarion              | $19.99     | 0.18  | 240 |
| 5    | Founder's Vow (launch-window only) | $49.99     | 0.45  | 600 |

Each pack contains: a unique-to-the-pack cosmetic + additional cosmetics from the regular shop + economy top-ups (Glimmer / Crystals / Food / Coins) + finite convenience tokens (instant-build, instant-repair, XP boosters, harvest auto-collect). **No combat stats are sold.** Crypto rails (SOL / USDC / SKR) display a USD reference; SKR-pegged amounts hold at launch values with manual repricing if the token moves >20% sustained over 14 days.

A generous seasonal pass — the Keeper's Almanac — costs $9.99 / 120 SKR per season. Permanent unlock, no expiry, cosmetic-only 30-tier track, with a 10-tier free track in parallel. No FOMO.

### 6.2 Money out — yield-funded player rewards

The developer holds **1 million SKR staked**. At conservative ~5% APY, that's ~50,000 SKR per year of yield — never touching principal. The yield funds three player-reward streams:

- **Stream A — Achievement drops (~40% of yield):** small SKR per first-time achievement (0.5 to 25 SKR each). Total reachable per player ≈ 100 SKR for completing the game.
- **Stream B — Weekly leaderboard contest (~40% of yield):** "The Watcher's Roll" — multi-category, ~250 SKR weekly prize pool, skill-based.
- **Stream C — Seasonal tournament (~20% of yield):** once per 90-day season, ~3,000–4,000 SKR purse, format TBD.

Streams B and C are gated by a legal opinion on skill-based contest defensibility (`docs/monetization-v2-spec.md` §12.4). Stream A ships in v1 unblocked; Streams B and C ship once the lawyer's opinion lands.

### 6.3 The closed loop

```
   Staking yield
        ↓
   Player rewards (Streams A/B/C)
        ↓
   Engaged players (achievement drops, leaderboard culture)
        ↓
   Some players spend back through packs (Stripe / SOL / USDC / SKR)
        ↓
   Reinforced treasury → reinforced stake → more yield
```

The loop is closed. The treasury principal is preserved; the game's economy runs on the yield curve. Critically, this means **SKR has utility** — it's not just received as payment, it's actively circulated as rewards to a real engaged player base, who in turn use some of it. This is a meaningful pattern for the Solana ecosystem.

### 6.4 Anti-cheat — keeping the rewards honest

The yield-funded rewards are an attractive target for bots. Five layers of defense (`docs/anti-cheat-spec.md`):

1. **Server-authoritative event validation** — the server records gameplay events independently; achievement claims must match server-recorded state
2. **Wallet behavior scoring** — 0-100 score per wallet from age, tx history, OFAC checks, IP clustering, pack purchase history; low scores hold payouts in review
3. **Statistical anomaly detection** — timing variance, frame-rate consistency, achievement claim ordering; bot patterns are mathematically detectable
4. **Honeypot achievements** — hidden achievements only triggered by patterns a real player can't produce; trigger = score penalty
5. **Economic disincentives** — weekly per-wallet payout caps; consecutive-win prize decay; behavior-score-eligible leaderboard pool (Sybil mitigation)

The five layers compound: a bot operator has to defeat all five simultaneously AND scale through the cap to be profitable. The cost of defeating each compounds; the cost of building each doesn't.

An owner review queue (`docs/anti-cheat-tuning-playbook.md`) catches anything ambiguous, with manual approval / rejection / KYC-request actions. **No automatic ban-on-flag.** False positives (legit players denied) are worse than false negatives (some bots get paid).

---

## 7. Security posture

The full audit spec is at `docs/cyber-audit-end-to-end-spec.md` covering 16 domains across developer security and operational + compliance + threat-modeling layers.

Highlights:

- **Treasury:** four separate multi-sig vaults (one each for SOL / USDC / SKR receive, plus a yield + payouts vault). 2-of-2 multi-sig via Squads. Hot signer + hardware-backed cold key. The publisher wallet (dApp Store NFT minter) is strictly separate.
- **Secrets:** zero private keys, seed phrases, or signer keypairs in the repo. Stripe webhook secret, RPC keys, AI provider keys env-var only.
- **On-chain verification:** server-side fetches every payment tx from Solana RPC; verifies destination, mint, amount (1% tolerance), finality commitment, network. Idempotent on tx hash. Stripe webhook signature validated. Idempotent on Stripe session ID.
- **XSS / injection:** every player-input field (banner inscription, clan name, chat messages, mailbox bodies, save imports) is text-rendered, length-capped, and regex-allowlisted. Save imports are Zod-validated; no hand-rolled validators.
- **Privacy:** Privacy Policy + Terms hosted at `/privacy` and `/terms`; deletion endpoint and data-access endpoint will ship before any user-facing public release. Application logs retain ≤ 7 days for IP addresses (GDPR). No analytics; no cookies; no third-party trackers.
- **Threat model:** full STRIDE analysis across 8 components (client, partyserver, Postgres, four treasury wallets, player wallet, Solana RPC, Stripe, localStorage) with ≥30 enumerated threats and mitigation status.
- **External penetration test:** deferred to the week before live mainnet treasury activation. Internal audit covers the cyber posture for v1 launch; external pentest gates the moment real money starts flowing in.

---

## 8. Performance — mobile-first, Seeker-tuned

The game's performance targets are calibrated for the actual deployment environment.

### 8.1 Generic mobile (Pixel 6a class)

The mid-market reference device (`docs/mobile-3d-perf-spec.md`):

- Cold-start bundle ≤ 6 MB uncompressed (≤ 1.5 MB gzipped) — **achieved 2026-05-18** via route code-splitting and build-config fixes
- Time-to-interactive on 4G ≤ 15s
- Village3D scene FPS ≥ 30 at wave 5 peak
- Memory ceiling ≤ 350 MB
- ≤ 150 draw calls per frame, ≤ 350k triangles, ≤ 80 MB texture memory

### 8.2 Seeker-specific (Snapdragon flagship class, 120Hz display)

The actual review device (`docs/seeker-perf-tuning-spec.md`):

- 60 fps lock at idle and through wave 5 — Seeker can hold it
- Stretch target: 90 fps on Seeker (display is 120Hz)
- Cold start on 5G ≤ 6 seconds
- 30-minute thermal sustain test: frame degradation ≤ 20%, battery drain ≤ 12%
- SKR pack purchase end-to-end on 5G ≤ 3 seconds median (wallet sign → entitlement)

### 8.3 What's been done

Phase 1 of the perf spec (bundle surgery) shipped 2026-05-18:

- Route-level lazy imports — landing page is < 500 KB; everything else streams on navigation
- Vite manual chunks for `three`, `@react-three/fiber`, `@react-three/drei`
- Build config fix — `build:vercel` was shipping unminified before; now does
- Duplicate animation library removed (`framer-motion` was a duplicate of `motion`)

Phases 2-5 (scene optimization, GC pressure, audio compression, dev perf HUD) run during the v1.x sprint.

---

## 9. Development roadmap

`docs/two-week-roadmap.md` is the canonical roadmap. Headlines:

### 9.1 What's already shipped (as of 2026-05-18)

- 3D village with tower defense + Q/F/E/R hero abilities (three classes: Mage, Knight, Ranger)
- Three pet companions (Aether Sprite, Flame Pup, Ice Wolf) with bond mechanics
- 400-enemy bestiary (`docs/enemy-codex` — Heartforge SVG pack, 403 sprites)
- ATB Last Stand turn-based breach combat
- SVG dungeon system + first authored dungeon (Healer's Cottage)
- Solana wallet integration via Mobile Wallet Adapter
- Clan + chat + mailbox UI
- Save system: localStorage + import/export
- Audio: title / village / battle BGM with crossfade
- Splash cinematic with staged reveal
- First-breach ATB tutorial (5-step coachmark)
- Poof platform removal (the project now operates independently)
- Feature-module refactor (modules / contracts / ownership declarations)
- Mobile 3D performance Phase 1 (bundle surgery)

### 9.2 What's next (v1 launch, ~2-4 weeks)

- Close the 4 remaining UAT P0s (gate build, Tower-Sim hide, file-size reductions, lint enforcement) — see `docs/regression-report-post-refactor.md`
- Cyber audit pass — 16 domains across `docs/cyber-audit-end-to-end-spec.md`
- Seeker performance tuning — measured on real hardware
- TWA APK build via Bubblewrap
- Solana dApp Store submission
- External pentest (during dApp Store review window)
- Treasury wallets enabled for live mainnet (only after all above)

### 9.3 v1.1 (~3 months post-launch)

- Tower-Sim breach mode FPS engine (alternative to ATB)
- Second and third dungeons (Folk's Old Granary, Frost-Stair of the Cold-Wandered)
- Six questlines fully wired
- Streams B + C of yield rewards (after legal opinion lands)
- Hero talent trees + pet skill trees
- On-chain save sync (Solana PDA per `docs/persistence-onchain-spec.md`)
- Accessibility audit + WCAG compliance pass
- CI/CD GitHub Actions enforcement

### 9.4 v1.2+ — speculative

- Full Realm Map exploration (map/regions/discovery)
- Hero-talent specialization paths
- Endgame "Mournful Echoes" wave 30+ content with narrative callbacks
- New Game+ refinement with persistent letter-to-the-next mechanic
- Community-authored dungeons (sandbox tools)
- Multilingual localization

---

## 10. Team and operating model

### 10.1 The team

**DeNelle Studios** — one developer (Samantha Denelle, founder) operating with AI-assisted development. The owner brings deep PM experience overseeing HP global projects — global scope, security audits, compliance, refactoring, multi-channel pipelines. That discipline is visible in this project's structure: specs precede builds, refactors run on isolated branches, every architectural decision has a written rationale and a tracked changelog.

### 10.2 AI-assisted development cadence

Claude Code agents run autonomously per spec; parallel worktrees handle independent feature streams. The cadence in the project's first weeks ran at ~60 commits per day across architecture, content, and polish. The discipline that prevents "60 commits/day" from becoming spaghetti is documented in the refactor spec (`docs/refactor-feature-modules-spec.md` §1.3): one hard rule — **do not refactor behavior and change gameplay in the same commit** — and a system of ownership declarations and contracts that future AI sessions read before touching anything.

### 10.3 Open contributor potential

The codebase is documented to a contributor-onboarding standard. CLAUDE.md is the canonical entry point. Specs in `docs/` provide complete context for any non-trivial change. The architecture is designed to admit a second human or AI contributor without conflicts — every module has a public surface (`index.ts`) and a contract (`OWNERSHIP.md`) that defines what it owns.

This is intentional. The project may grow beyond solo. The architecture is ready.

---

## 11. Risk register

The honest list of what could go wrong and how we plan for it.

| Risk                                                               | Likelihood | Impact       | Mitigation                                                                                                                                                                                           |
| ------------------------------------------------------------------ | ---------- | ------------ | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| dApp Store review rejects v1                                       | Medium     | Medium       | The submission packet (`docs/solana-dapp-store-submission.md`) is calibrated to the published guidelines; review feedback is iterable.                                                               |
| Mobile performance falls short on Seeker                           | Medium     | High         | The Seeker perf spec is a calibrated tuning pass with measured targets; Phase 1 already de-risked bundle size.                                                                                       |
| Treasury wallet compromise                                         | Low        | Catastrophic | 2-of-2 multi-sig + hardware co-signer + incident response plan (`docs/incident-response-plan.md`). Single compromised key cannot drain anything.                                                     |
| Solana Foundation grant denied                                     | Medium     | Low          | The grant is multiplier, not lifeline. Project ships regardless. Multiple grant programs targeted (Builder track, dApp Store featured-app, Game Day).                                                |
| Bot farms drain the rewards economy                                | Medium     | Medium       | Five-layer anti-cheat (`docs/anti-cheat-spec.md`); review queue + manual approval; weekly cap; Sybil mitigation via behavior-score-eligible pool.                                                    |
| Legal opinion on skill-based contests excludes major jurisdictions | Medium     | Medium       | Streams B + C are gated behind the opinion (`docs/monetization-v2-spec.md` §12.4). Stream A ships unblocked. If contests aren't viable everywhere, geo-fence eligibility.                            |
| SKR price moves 50%+ overnight                                     | High       | Low          | Manual reprice within 72 hours (`docs/monetization-v2-spec.md` §4.1). Cozy framing for the in-game message.                                                                                          |
| Convenience-power compromise feels predatory in playtest           | Medium     | Medium       | §11.1 rip-out path in monetization spec. One config-file change removes convenience layer; packs revert to cosmetic + economy only.                                                                  |
| Single developer health / availability event                       | Low        | High         | Codebase is documented to contributor standard; ownership declarations + specs make handoff possible. Treasury keys + APK keystore have offline paper backups in geographically separated locations. |
| Solana network outage during launch                                | Low        | Medium       | Localstorage-first design means players can play during outages; payment rails degrade gracefully (Stripe still works); on-chain features queue and reconcile when network recovers.                 |

---

## 12. The grant story — what we're asking for, why it makes sense

`docs/monetization-v2-spec.md` §13 contains the full grant pitch. Headlines:

### 12.1 The ask

**$10,000 USD from the Solana Foundation Builder track grant.** Funds approximately 6 months of focused v1.1 / v1.2 development. We are not pitching a $250K grant for 12 engineers; we are pitching a small grant for one developer to do another 6 months of high-velocity work that builds on the demonstrable cadence already shown.

### 12.2 What it buys (line-itemized budget)

| Line item                                                         | 6-month cost |
| ----------------------------------------------------------------- | ------------ |
| Hosting (Vercel + treasury infra)                                 | $180         |
| Domain renewal                                                    | $20          |
| AI build agents (Claude API spend)                                | $2,400       |
| Art commissions (icon polish, feature graphic, missing pet skins) | $1,500       |
| Music commission (one new track for Hollow Deep / endgame)        | $1,000       |
| SFX bundle license                                                | $300         |
| Audio mixing pass                                                 | $500         |
| Lawyer (contests / sweepstakes opinion)                           | $1,500       |
| Solana mainnet tx fees                                            | $50          |
| Marketing (X promo + Discord boost)                               | $1,000       |
| Contingency 15%                                                   | $1,550       |
| **TOTAL**                                                         | **$10,000**  |

### 12.3 The differentiator

We are not pitching against teams who are doing crypto features for crypto's sake. We are pitching as a competent game that happens to use crypto correctly. That positioning — competence first, crypto-native integration second — is what we believe the Foundation actually wants to fund.

The 5-minute demo we'd give a grant committee runs live software end-to-end: title → wave → breach → ATB battle → store → SKR pack purchase → Solscan tx → game responds → `/treasury/payouts` showing the yield-funded payouts log → leaderboard with this week's prize pool from yield. **No slides. Just live software.** Velocity + integration + transparency are the three things grant committees notice; the demo shows all three.

---

## 13. The complete spec index

Every system referenced above has a buildable spec or design doc. The complete index:

### Vision + narrative

- `docs/narrative-bible.md` — world, characters, voice
- `docs/dungeons-storyline.md` — the 6 questline arcs + endgame conversation

### Architecture

- `docs/refactor-feature-modules-spec.md` — module structure + ownership + contracts
- `docs/poof-removal-overnight-spec.md` — platform independence migration

### Gameplay systems

- `docs/atb-last-stand-spec.md` — original ATB combat spec
- `docs/battle-design-atb-v2.md` — refined ATB combat spec (current canonical)
- `docs/battle-design-tower-sim.md` — Tower-Sim alternative breach mode (deferred to v1.1)
- `docs/dungeons-system-design.md` — SVG dungeon system
- `docs/gate-design-spec.md` — force-field gate
- `docs/enemy-aggression-spec.md` — opportunistic-attack AI
- `docs/touch-movement-spec.md` — tap-to-move + joystick auto-detect
- `docs/economy-design.md` — in-game currency model
- `docs/cosmetic-shop-spec.md` — vanity store

### Performance

- `docs/mobile-3d-perf-spec.md` — generic mobile perf
- `docs/seeker-perf-tuning-spec.md` — Seeker-specific calibration

### Monetization + economy

- `docs/monetization-design.md` — the philosophy + constraints
- `docs/monetization-v2-spec.md` — the canonical spec (packs, seasonal pass, yield rewards, grant pitch)
- `docs/persistence-onchain-spec.md` — on-chain save sync (v1.1 candidate)

### Security + ops

- `docs/cyber-audit-end-to-end-spec.md` — 16-domain audit spec
- `docs/anti-cheat-spec.md` — 5-layer anti-cheat
- `docs/anti-cheat-tuning-playbook.md` — owner's review queue runbook
- `docs/threat-model.md` — STRIDE (produced by audit)
- `docs/incident-response-plan.md` — IR runbook (produced by audit)
- `docs/privacy-compliance-matrix.md` — GDPR / CCPA / app-store privacy (produced by audit)

### Distribution

- `docs/solana-dapp-store-submission.md` — Solana dApp Store submission packet

### Operations

- `docs/two-week-roadmap.md` — sprint plan
- `docs/regression-report-post-refactor.md` — current ship-readiness gate
- `docs/uat-playthrough-report.md` — UAT findings

### Roadmap

- `docs/v2-roadmap.md` — v1.x + v2 candidate features
- `docs/v3-ideas.md` — speculative future

---

## 14. Closing

Defenders of the Realm is what happens when an experienced PM applies enterprise-grade discipline to indie crypto game development.

The cozy game is real — the narrative is voiced, the art is shipping, the gameplay loop works. The crypto integration is real — players really pay in SKR, real on-chain transactions hit the treasury vault, real on-chain payouts flow back to players from staking yield. The architecture is real — every system has a spec, every module has an ownership declaration, every commit obeys the discipline that prevents spaghetti.

We are building a small, beautiful, ethically-monetized mobile-first tower defense game that ships on the Solana Mobile dApp Store and demonstrates what first-class Solana integration looks like at the game-design level. The Foundation grant is the multiplier; the project ships either way.

**The Lantern is the only barrier between the remembered world and the cold silent one. The Keeper holds.**

---

_Tend the Heart. Hold the dark._

— Defenders of the Realm

---

## Appendix A — Glossary

- **The Keeper** — the player character, bound to Elarion
- **Elarion (the Heart)** — the sentient crystal-veined tree at the center of the village
- **The Hollow Ones** — silent former villagers unmade by the Withering
- **The Withering** — the slow cold rot the game is fighting
- **Alduin the Mournful** — the antagonist, a former healer drank by the Wound
- **The Wound** — the buried tear in the world from which the Withering seeps
- **Aether Sprite / Flame Pup / Ice Wolf** — the three pet companions
- **The Lantern** — alternate poetic name for Elarion
- **SKR** — Solana Mobile's Seeker token, used as a first-class in-game currency
- **MWA** — Mobile Wallet Adapter, the Solana Mobile wallet integration protocol
- **TWA** — Trusted Web Activity, the Android wrapper for the web app
- **Stream A / B / C** — the three yield-funded player reward streams (achievement drops / weekly leaderboard / seasonal tournament)
- **The covenant** — the seven non-negotiable design constraints documented in `docs/monetization-design.md` §1

---

## Appendix B — Document version history

| Version | Date       | Notes                                                                            |
| ------- | ---------- | -------------------------------------------------------------------------------- |
| 1.0     | 2026-05-18 | Initial publication. Reflects state through the post-refactor regression report. |

This document will be updated at major milestones: v1 launch, dApp Store approval, first grant, v1.1 release, v2 commencement.
