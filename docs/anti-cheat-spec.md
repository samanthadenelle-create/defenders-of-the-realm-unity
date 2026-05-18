# Anti-Cheat / Anti-Bot — Layered Defense Spec

**Status:** Buildable spec. Extends `docs/monetization-v2-spec.md` §12.3 (which defines bare-minimum anti-gaming for the yield-funded rewards) with a layered defense designed for an indie-budget, crypto-payout game. Owner-locked 2026-05-17.

**Audience:** Claude Code (build), owner (tuning + manual review queue).

**Lives alongside:** `docs/monetization-v2-spec.md` (the reward streams this defends), `docs/cyber-audit-end-to-end-spec.md` (the broader cyber posture this is one part of), `partyserver/src/routes/` (where server-authoritative validation lives), `services/` (new sub-services this introduces).

**One-line:** Bots and cheaters threaten the SKR-yield rewards economy. Cheap server-authoritative validation + wallet behavior scoring + statistical anomaly detection + honeypots make the math not work for bots, without requiring a multi-million-dollar anti-cheat platform.

---

## 1. The attack surface — what's being defended

The yield-funded rewards economy (`docs/monetization-v2-spec.md` §12) creates THREE attractive targets:

| Stream                      | Per-event payout                    | Max per wallet                                        | Attacker math                                                                                  |
| --------------------------- | ----------------------------------- | ----------------------------------------------------- | ---------------------------------------------------------------------------------------------- |
| **A — Achievement drops**   | 0.5–25 SKR each                     | ~100 SKR per save (all achievements)                  | Farm 1,000 wallets = 100K SKR / yr (≈$8K). Need 1,000 saves through wave 30, all achievements. |
| **B — Weekly leaderboard**  | 1.25–40 SKR per win                 | ~80 SKR per wallet per week if winning all categories | 52 weeks × 80 = 4,160 SKR / yr (≈$345) per perfect-winning wallet                              |
| **C — Seasonal tournament** | Up to ~1,000 SKR for a winning slot | One purse per quarter                                 | 4 × 1,000 = 4,000 SKR / yr (≈$330) per winning wallet                                          |

Stream A is by far the biggest attack surface — flat payouts for verifiable events, can be done at scale. Stream B/C are smaller targets but more visible (leaderboard names = community-spotted). **Most anti-cheat effort goes to Stream A.**

The attacker math is what makes layered defense pencil out. **If a bot operator's wallet-prep + KYC bypass costs more than $8K/year of farming yields, they don't bother.** The whole spec is sized to push that ratio in our favor without needing enterprise-grade detection.

---

## 2. The five defense layers

| #   | Layer                                     | Stops                                                        | Cost to build                              | Cost to bypass                                             |
| --- | ----------------------------------------- | ------------------------------------------------------------ | ------------------------------------------ | ---------------------------------------------------------- |
| 1   | **Server-authoritative event validation** | Direct client-side achievement-claim fraud                   | Medium (server endpoints + event emission) | High (must replay realistic gameplay timing)               |
| 2   | **Wallet behavior scoring**               | Freshly-minted farm wallets                                  | Low (read-only RPC queries)                | Medium (must age wallets + transact normally)              |
| 3   | **Statistical anomaly detection**         | Botted gameplay patterns (perfect timing, no input variance) | Medium (analytics infrastructure)          | High (must simulate human variance)                        |
| 4   | **Honeypot achievements**                 | Bots that only look at the right places to fake              | Low (extra achievement entries + flagging) | Very high (must perfectly model legit-player UI traversal) |
| 5   | **Economic disincentives**                | Sybil attacks (many wallets, small payouts each)             | Already in monetization spec §12.3         | Medium (must scale wallet count + KYC)                     |

A bot operator has to defeat layers 1–4 simultaneously AND scale through layer 5 to be profitable. The cost of defeating each layer compounds; the cost of building each layer doesn't.

---

## 3. Layer 1 — Server-authoritative event validation

The client cannot be trusted to say "I cleared wave 30" or "I bonded my pet to rank 5" because a tampered client says anything it wants. The server must independently verify.

### 3.1 The event-emission model

Every gameplay event that COULD trigger an achievement, leaderboard score, or pack-purchase-related drop emits to the server in real time:

```ts
// On the client, when a meaningful event happens:
events.emit({
  kind: 'wave_cleared',
  wave: 5,
  hero: 'mage',
  petsAlive: 2,
  durationMs: 142_300,
  damageDealt: 1842,
  damageTaken: 220,
  // anti-tamper: monotonic counter, signed by a session key
  seq: 47,
  sessionId: 'abc...',
});
```

The server records these events in a Vercel Postgres table:

```sql
CREATE TABLE gameplay_events (
  id BIGSERIAL PRIMARY KEY,
  identity_kind TEXT NOT NULL,
  identity_value TEXT NOT NULL,
  session_id TEXT NOT NULL,
  seq INTEGER NOT NULL,
  kind TEXT NOT NULL,
  payload JSONB NOT NULL,
  received_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  UNIQUE (identity_kind, identity_value, session_id, seq)
);
CREATE INDEX gameplay_events_identity ON gameplay_events (identity_kind, identity_value, received_at DESC);
```

When the client later claims "I deserve the wave-30 achievement drop," the server checks: did we record the events that lead to wave 30? Are the timestamps plausible? Did seq numbers progress monotonically? Are the durations within human-reasonable ranges?

### 3.2 What's validated server-side, what's NOT

**Server-authoritative (must be on the server):**

- Wave cleared events (for wave-N achievements, leaderboard)
- Boss defeated events
- Dungeon completed events
- Pack purchase events (already server-authoritative via §2.A.6 of cyber-audit-end-to-end-spec.md)
- Pet bond rank increase events
- Letter-to-the-next written (for the endgame achievement)
- Founder's Vow purchase (already server via Stripe webhook / on-chain verification)

**Trust-the-client (acceptable):**

- Hero outfit equip (cosmetic only, no payout)
- Settings changes (no payout impact)
- Tap-to-move destination (no payout impact)
- Building placement (no payout impact)

The rule: **if a payout depends on it, the server records it independently of the client's claim.**

### 3.3 Plausibility checks per event

The server doesn't trust the event payload either — it validates it against game design constants:

```ts
// services/event-validator.ts
function validateWaveCleared(payload: WaveClearedEvent, prior: GameStats): Plausibility {
  // Wave N requires at least N waves of accumulated time
  const minDuration = MIN_WAVE_DURATION[payload.wave] * 1000;
  if (payload.durationMs < minDuration) return 'IMPLAUSIBLE_TOO_FAST';

  // Wave N requires having cleared waves 1..N-1
  if (payload.wave > prior.highestWaveCleared + 1) return 'IMPLAUSIBLE_SKIPPED_WAVES';

  // Damage dealt has a known upper bound per wave
  if (payload.damageDealt > MAX_DAMAGE_PER_WAVE[payload.wave]) return 'IMPLAUSIBLE_OVERDAMAGE';

  // No more pets alive than the player has bonded
  if (payload.petsAlive > prior.bondedPets) return 'IMPLAUSIBLE_TOO_MANY_PETS';

  return 'OK';
}
```

Each plausibility constant (`MIN_WAVE_DURATION`, `MAX_DAMAGE_PER_WAVE`, etc.) is derived from the actual game tuning + a small headroom for skilled players. The constants live in `services/event-validator-constants.ts` and are updated when game balance changes.

### 3.4 Cumulative-stats consistency

The server keeps a per-identity `game_stats` row that's the truth-of-the-game for that player:

```sql
CREATE TABLE game_stats (
  identity_kind TEXT NOT NULL,
  identity_value TEXT NOT NULL,
  highest_wave_cleared INTEGER NOT NULL DEFAULT 0,
  total_dungeons_cleared INTEGER NOT NULL DEFAULT 0,
  bonded_pets INTEGER NOT NULL DEFAULT 0,
  total_play_time_ms BIGINT NOT NULL DEFAULT 0,
  first_seen_at TIMESTAMPTZ NOT NULL,
  last_event_at TIMESTAMPTZ NOT NULL,
  PRIMARY KEY (identity_kind, identity_value)
);
```

Every event updates `game_stats` only if it passes plausibility. Achievement-drop claims check `game_stats`, not the client's claim. A client that says "give me the wave-30 achievement" only succeeds if `game_stats.highest_wave_cleared >= 30`.

### 3.5 Cost + acceptance

- ~3-5 days of build (event-emission client hooks + server endpoints + plausibility validators + game_stats reconciliation)
- Acceptance: every achievement in the Stream A table has a server-authoritative trigger; no payout is granted on client claim alone

---

## 4. Layer 2 — Wallet behavior scoring

A freshly-minted wallet that immediately connects, clears all achievements, and claims maximum SKR is a bot. Wallet behavior tells us this BEFORE we pay.

### 4.1 Score components

When a wallet connects and is about to receive ANY payout > 5 SKR, the server computes a 0–100 score from:

| Signal                                                                                   | Weight              | Source                                                           |
| ---------------------------------------------------------------------------------------- | ------------------- | ---------------------------------------------------------------- |
| **Wallet age in days** (capped at 365)                                                   | 25                  | Solana RPC `getSignaturesForAddress` — first signature timestamp |
| **Total transaction count** (capped at 100)                                              | 15                  | Same RPC call                                                    |
| **Non-prize-incoming tx count** (capped at 20)                                           | 15                  | Manual filter — txs not from our payouts wallet                  |
| **Holds SOL or USDC** (binary 0 / 1)                                                     | 10                  | RPC `getBalance`                                                 |
| **Holds NFTs other than ours** (binary)                                                  | 10                  | Metaplex token-account scan                                      |
| **OFAC SDN blocklist** (binary, 0 if listed)                                             | -100 (instant fail) | Local OFAC list (per cyber-audit §3.B.8)                         |
| **Is a known mixer / privacy tool destination** (binary)                                 | -25                 | Curated list of mixer wallets (Tornado-like, Privacy Pools)      |
| **Pack-purchase history with us** (binary 0 / 1)                                         | 15                  | Our entitlements DB — has this wallet ever bought a pack?        |
| **Same-IP wallet count** (negative weight if >5 distinct wallets share an IP within 24h) | -10                 | Cloudflare request headers + our session log                     |
| **Time-since-first-game-event** (capped at 30 days)                                      | 10                  | Our `game_stats.first_seen_at`                                   |

Total score:

- **80–100:** Likely legit. Payout proceeds normally.
- **50–79:** Hold for manual review for any single payout > 25 SKR. Pay immediately for smaller drops.
- **20–49:** Hold ALL payouts in a review queue. Manual approval required.
- **0–19:** Reject. Score reported to owner. Wallet enters a "watch list" for future attempts.
- **Negative:** Hard reject. OFAC or mixer hit. Never payable.

### 4.2 Score is cached but refreshed

Caching for an hour is fine. Re-score on:

- Wallet first connect
- Score-based hold for any reason (re-check; conditions may have changed)
- Manual review trigger from the owner

Each score check is ~3 RPC calls to Solana + a DB read. Free-tier RPC handles this for our anticipated volume.

### 4.3 Cost + acceptance

- ~2 days of build (`services/wallet-score.ts` + score gates on payout endpoints)
- Acceptance: every payout > 5 SKR consults the wallet score; score < 50 holds the payout in review queue

---

## 5. Layer 3 — Statistical anomaly detection

Bots are mathematically detectable because they don't introduce human variance. Three concrete signals:

### 5.1 Timing variance

Real players have noisy timing. Bots have low-variance timing. Measure:

- **Inter-event interval variance** per session. Real player: high standard deviation. Bot: near-zero variance.
- **Wave-clear time per wave per session**. Real players progress non-linearly (some waves much faster than others as they get the hang of it). Bots clear each wave in the same time.
- **Time-between-input-events** (taps, key presses). Real players cluster around 200–800ms with jitter. Bots cluster very tightly at one value.

Server-side, compute the coefficient of variation (CV = stddev / mean) for each session. CV < 0.15 on a sustained session is a flag.

### 5.2 Frame rate consistency

Bots running in headless browsers have synthetic frame rates (often locked at 60.000 or 0.000 fps). Real players have frame rate that drifts with thermal throttling, background apps, scene complexity.

The client emits per-30-second frame-rate samples. The server flags wallets whose frame-rate samples are too consistent (CV < 0.05 sustained) or always exactly 60.000.

### 5.3 Achievement claim ordering

Real players hit achievements roughly in the order the game presents them — early waves first, then pets, then dungeons, then endgame. Bots that're optimizing for high-value drops sometimes go for the big-value achievements first.

The server checks the order in which a wallet's achievements arrived. If the wallet's first 5 achievements are all 10+ SKR drops (skipping the 0.5 SKR onboarding ones), that's a flag.

### 5.4 What the anomaly engine does

For each session that completes (wallet disconnect, or 30 min idle):

1. Compute the three signals above.
2. Score each on a per-signal threshold: GREEN (definitely human), YELLOW (suspicious), RED (likely bot).
3. If ANY signal is RED, hold all unpaid drops from this session in review queue. Owner reviews and approves or rejects.
4. If TWO or more signals are YELLOW, same hold.
5. Pattern-detection across multiple sessions for the same wallet: 3 YELLOW sessions in a row escalates to RED.

### 5.5 Cost + acceptance

- ~3-4 days of build (`services/anomaly-detector.ts` + client telemetry emission + review queue UI for the owner)
- Acceptance: every paid session passes through anomaly detection; RED sessions hold payouts pending owner review

---

## 6. Layer 4 — Honeypot achievements

The cheapest layer with the highest precision. Bots are detectable because they trigger things real players never would.

> **Amended 2026-05-18** — honeypot detection is **entirely server-side**. The client never knows which events are honeypots vs. normal achievements. Matchers live in `partyserver/services/honeypot-detector.ts`; private definitions live in the gitignored `docs/honeypots-list-DO-NOT-COMMIT.md`. CI guard at `scripts/check-no-honeypot-leak.sh` enforces zero `honeypot` references in client `src/`. See `docs/defensive-hardening-spec.md` §2 for the architecture and rationale. The sub-sections below describe the LOGICAL design; read §2 of that doc for how it actually wires.

### 6.1 What a honeypot achievement is

Hidden, invisible "achievements" defined in the client achievement registry but never shown in any UI. A bot that scrapes the registry to "complete all achievements" will trigger these; a real player can't (because they have no way to know they exist).

Examples (DO NOT publicize):

- `honeypot-tap-debug` — fires if the player ever taps the (hidden) version pill in the dev HUD. Real players don't see the version pill in production. A bot scraping clickable elements does.
- `honeypot-zerostate-pet` — fires if a player tries to deploy a pet they don't own. Real UI prevents this; a bot bypassing UI checks may trigger it.
- `honeypot-impossible-combo` — fires if a player accumulates >1 of a strictly one-time item simultaneously. Server-side check; client-replay only.
- `honeypot-zero-input-wave` — fires if a player clears wave 1 without recording any input events. AFK-clearing is theoretically possible at wave 1 with all towers placed; a bot that's faking events without tapping triggers this.

### 6.2 What happens when a honeypot fires

The server records the trigger but does NOT immediately ban or notify. Instead:

- Honeypot trigger = score adjustment of -50 to that wallet's behavior score (Layer 2)
- Two honeypot triggers in a single session = automatic hold on all payouts, manual review
- Three across separate sessions = wallet enters the watch list

We don't ban-on-honeypot-trigger because a real user pressing buttons quickly could maybe trigger one by accident. We use it as a strong signal in a layered system, not a death sentence.

### 6.3 Cost + acceptance

- ~1 day of build (define 5-10 honeypots in `services/honeypots.ts`; add to existing achievement event system)
- Honeypot names + triggers DOCUMENTED PRIVATELY in `docs/honeypots-list-DO-NOT-COMMIT.md` (gitignored)
- Acceptance: at least 5 honeypots active; each integrated with Layer 2 score adjustment

---

## 7. Layer 5 — Economic disincentives

The reactive layer — even with everything above, some bots get through. Make the math not work for them:

### 7.1 Already in monetization §12.3

- Minimum account age for prizes > 5 SKR
- Single-claim guard per achievement
- Rate-limit suspicious patterns (3 leaderboard wins consecutively + <$50 pack history → manual review before 4th)
- Soft KYC for prizes > 50 SKR
- Public payouts log
- Treasury watch-window

### 7.2 Additions this spec introduces

- **Per-wallet weekly payout cap** of 200 SKR. A perfect-playing legit player rarely exceeds this; a farming bot hits it constantly. Excess goes to a "cap reached" message in-game with no payout. Re-evaluates Monday 00:00 UTC.
- **Sliding-scale leaderboard prizes.** Top 1 wallet gets 40 SKR; top 2-3 get 20 SKR each; top 4-10 get 5 SKR each. But if the top 1 wallet has also won the previous week, its prize halves (and so on, geometric). After 4 consecutive wins, prize is 2.5 SKR. Encourages leaderboard rotation; soft-caps consistent winners regardless of legit-or-bot.
- **Sybil-mitigation pool**: instead of "top 10 wallets," prizes go to "top 10 distinct REAL players" as judged by behavior score. A wallet with score 100 counts; a wallet with score 30 is invisible to the prize pool. This means a bot farm spinning up many low-score wallets sees zero of them on the leaderboard.

### 7.3 Cost + acceptance

- ~1-2 days of build (extends existing reward-distribution logic)
- Acceptance: weekly cap enforced; consecutive-win decay applied; behavior-scored prize-pool eligibility live

---

## 8. The review queue + owner workflow

All five layers feed a review queue. Owner reviews flagged items.

### 8.1 The queue

`services/review-queue.ts` — Postgres table:

```sql
CREATE TABLE review_queue (
  id BIGSERIAL PRIMARY KEY,
  identity_kind TEXT NOT NULL,
  identity_value TEXT NOT NULL,
  amount_skr NUMERIC(10,4) NOT NULL,
  reason TEXT NOT NULL,           -- which layer flagged it
  reason_details JSONB NOT NULL,  -- evidence
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  reviewed_at TIMESTAMPTZ,
  decision TEXT,                  -- 'paid' | 'rejected' | 'requested_kyc'
  decided_by TEXT
);
```

### 8.2 The owner-facing dashboard

A simple admin page at `/admin/review` (auth-gated to owner wallet only) showing the pending queue, each row with:

- Wallet address (truncated, click for Solscan)
- Amount in SKR + USD-equivalent
- Reason flagged (which layer + signal)
- Wallet behavior score with breakdown
- Last 10 gameplay events for context
- Approve / Reject / Request-KYC buttons

Owner reviews each item. Typical session: 5–15 minutes per week.

### 8.3 KYC fallback

For amounts that genuinely warrant it (per §12.3 of monetization spec, soft KYC at > 50 SKR), the player gets an email asking them to verify identity. Pay on confirmation. Reject (silent — no notification) on no-response after 14 days.

### 8.4 Cost + acceptance

- ~2 days of build (Postgres table + admin UI + auth gating)
- Acceptance: every flagged payout lands in the queue; owner-only dashboard functional; KYC flow callable from within

---

## 9. What's deliberately OUT of scope (don't build this)

- **Real-time captchas in-game.** Anti-cozy. Players hate it. Trust the layered system.
- **Full proof-of-personhood integration (Worldcoin, etc.).** Heavy infra; not warranted at our scale.
- **Behavioral biometrics (mouse-movement-as-signature).** Privacy-heavy; client-only signal; bypass-able.
- **Aggressive IP geo-blocking.** Some legit players use VPNs; blocking by region creates more support tickets than it stops bots.
- **Automatic ban-on-flag.** Always-manual review. Reduces false-positive harm; flagged-but-legitimate players don't get angry.
- **Public leaderboard for low-rep wallets.** They appear in the queue, not the leaderboard. The leaderboard already filters on behavior score.
- **Anti-cheat for non-paying gameplay** (just village defense, no rewards). If a player wants to cheat to clear wave 50 for personal satisfaction with no payout, let them.

---

## 10. Implementation order

Following `docs/two-week-roadmap.md` cadence and the monetization spec's build priority (`docs/monetization-v2-spec.md` §18):

1. **Layer 5 (economic disincentives)** — already partially in monetization spec; the new additions (weekly cap, decay, behavior-scored pool) take 1-2 days. **Ship first** because it's the cheapest and reduces attack profitability without any new infrastructure.
2. **Layer 2 (wallet behavior scoring)** — 2 days. Required before Stream A goes live since payouts >5 SKR all gate on score.
3. **Layer 1 (server-authoritative event validation)** — 3-5 days. Required before any leaderboard or wave-30 achievement pays out.
4. **Layer 4 (honeypots)** — 1 day. Quick win once Layer 1 event-emission is live.
5. **Layer 3 (statistical anomaly detection)** — 3-4 days. Builds on Layer 1's event-emission infrastructure. Can ship right after Layer 1.
6. **Review queue + admin dashboard** — 2 days. Ship alongside Layer 2 because that's when payouts first start gating.

**Total: ~14 days of focused build, spread across the build queue.** Most of this overlaps with the existing payment-verifier / entitlements work.

---

## 11. Acceptance criteria — when this is "done"

The anti-cheat system ships when ALL of these are true:

- [ ] Server-authoritative `game_stats` table exists and updates from validated events only (Layer 1)
- [ ] Wallet behavior score is computed at every payout > 5 SKR (Layer 2)
- [ ] Anomaly detector runs at every session-end and flags YELLOW/RED sessions (Layer 3)
- [ ] At least 5 honeypot achievements active; documented privately in gitignored file (Layer 4)
- [ ] Weekly per-wallet 200 SKR cap enforced
- [ ] Consecutive-leaderboard-win prize decay enforced
- [ ] Behavior-score-eligible leaderboard pool (Sybil mitigation)
- [ ] Review queue Postgres table exists; owner-only admin dashboard live at `/admin/review`
- [ ] KYC fallback email path callable from the dashboard
- [ ] No automatic ban-on-flag; all decisions go through owner review
- [ ] Public payouts log at `/treasury/payouts` shows EVERY paid drop; rejected/held drops not shown
- [ ] First 100 drops post-launch: owner manually reviews each one (even when not flagged) for calibration; tunes thresholds; documents findings in `docs/anti-cheat-calibration-log.md`

---

## 12. Tuning + iteration

Anti-cheat is a moving target. Schedule:

- **Weekly** for the first month: review the queue + tune thresholds. Honeypot fires? Real players or bots? Refine.
- **Monthly** after month 1: lighter touch. Owner reviews aggregate stats — false-positive rate, false-negative rate (any community-reported "they're definitely a bot" that we missed).
- **Quarterly**: review the spec itself. Add new honeypots; rotate old ones if pattern-leaked; adjust score weights based on what's worked.

The calibration log (`docs/anti-cheat-calibration-log.md`) accumulates these tuning decisions over time. Future Claude sessions read it before adjusting thresholds.

---

## 13. The honest tradeoff

**A determined attacker WILL get some payouts.** No system is bulletproof. The goal is to make the attack uneconomical — to push the cost of farming above the value extractable. The 5-layer system above does that for at least the first 6-12 months of operation. After that, attackers will have figured out our patterns and we'll need to evolve.

**The signal we're sending matters as much as the prevention itself.** A Solana Foundation grant reviewer or dApp Store reviewer sees this spec and concludes: _"this team thought about the attack surface seriously."_ That's worth at least as much as the prevention itself, in the grant context.

**False positives are real cost.** Every legitimate player held in review is a worse experience. The system is tuned to favor false negatives over false positives — when uncertain, pay. The treasury can absorb some bot farming; it can't absorb pissed-off legitimate players who got dropped from leaderboards. The 100-drop owner-review calibration in §11 is specifically to catch over-aggressive thresholds before they hurt anyone.

---

_This spec is the contract for the anti-cheat layer. Implementation follows §10 in order. Calibration follows §12. The honesty of §13 is the framing reviewers will appreciate. Combined with the §12.3 baseline from monetization-v2-spec.md, this gives us a credible defense without the cost of an enterprise anti-cheat platform._
