> ⚠ **NUMBER COLLISION — this document does not own WO-129; `WORK_ORDER_129_pipeline_architecture_reconciliation.md` does.**
> Referred to hereafter as **WO-129-B (leaderboard / profile / social)**.
> Flagged by the 2026-08-16 Sunday board-grooming pass (`python tools/board_build.py` → `DUPLICATE_WO_NUMBERS`);
> ownership decided by **first-on-disk** (`git log --follow --diff-filter=A`): the winner's file was created first.
> Banner only — nothing was renumbered or deleted.

# WORK ORDER 129 — Leaderboards, Player Profiles & Social Install Bonus

**Status:** READY TO IMPLEMENT (design / user-story stage)
**Date:** 2026-05-30
**Author:** UI (product/design)
**Priority:** High — this is the retention + acquisition layer of the NORTH STAR competitive thesis.
**Lanes:** product/design (this doc, now) · backend endpoints + persistence (WO-120, Kayden) · client UI + EventTracker hooks (CLI, later) · metrics overlap (WO-121)

> **One line:** Persistent, server-backed leaderboards that *everyone* can see (top 3 highlighted + your own rank), a player profile with a username + headline stats, opt-in social linking, and an "I just installed Defenders of the Realm" share that grants a one-time, server-validated bonus. Together these are the **acquisition + social-pressure flywheel** the arena and whale economy feed on — "free players are the stadium," and this is what fills and ranks the stadium.

---

## 0. Why this routes against the NORTH STAR

- **Leaderboards are the spend driver.** Per `docs/NORTH_STAR.md`: *"The Challenge Arena + leaderboards is the spend driver — people pay real money to win/flex."* Competitive whales pay for *status*; status needs a public ranking everyone can see.
- **Social pressure is a top spend driver** (NORTH STAR, clans section). A visible top-3 + "your rank #847, climbing" is the social pressure that converts.
- **Free players are the content, not a cost.** The leaderboard gives the silent 95% a *reason to keep playing for free* (chase the ladder), which keeps the stadium full so whale spend is worth it.
- **The install-share is the viral-acquisition loop.** It fills the stadium at near-zero CAC — the front door to the same flywheel.
- **Discipline carries over.** Social linking and the install bonus are an **opt-in path, never a wall** (the same ad/spend discipline NORTH STAR demands of rewarded ads).

---

## 1. USER STORIES

### Leaderboards
- **US-1.** *As a competitive player, I want to see a persistent global leaderboard with the top 3 highlighted, so that I know who the best Defenders are and have someone to chase.*
- **US-2.** *As a ladder-climber, I want to see my own rank and the players just above and below me even when I'm nowhere near the top, so that the next rung always feels reachable.*
- **US-3.** *As any player (paying or free), I want my best run to count toward a public ranking, so that my time invested earns visible standing.*
- **US-4.** *As a clan member, I want a clan leaderboard, so that I'm competing for my clan, not just myself* (NORTH STAR: "you compete *for your clan*").
- **US-5.** *As a returning player, I want leaderboards to reset on a period (e.g. weekly) with a fresh start, so that a bad week doesn't lock me out and there's a recurring reason to come back.*

### Player profile & username
- **US-6.** *As a new player, I want a username and an avatar/hero on my profile, so that I have an identity on the leaderboard instead of a wallet address.*
- **US-7.** *As a player, I want to see my own headline stats (best wave, longest hold, total resources, rank) in one place, so that I can track my progress and have something to show off.*
- **US-8.** *As a player, I want to set my username once for free and rename it later for a cost, so that I can claim my identity without it being abused for spam.*
- **US-9.** *As any player, I want other players' usernames to be unique and free of offensive content, so that the public board stays clean and trustworthy.*

### Social linking
- **US-10.** *As a social player, I want to optionally link a social account (X, Discord), so that friends can find me and my profile shows verified handles — but I never want to be forced to, so that I can play fully without connecting anything.*
- **US-11.** *As a player who linked social, I want to find/follow friends already playing, so that I have rivals on my ladder.*

### Install / referral share bonus
- **US-12.** *As a new user, I want to post "I just installed Defenders of the Realm" and receive a one-time bonus, so that I'm rewarded for spreading the word.*
- **US-13.** *As a referrer, I want a friend who installs from my link to earn me a bonus, so that inviting people pays off* (extends the existing `ReferralService`).
- **US-14.** *As the game (anti-abuse stance), the system must grant the install/referral bonus only once per real player and validate it server-side, so that the reward can't be farmed by fake installs or self-referral.*

---

## 2. SCOPED DESIGN

### 2.1 Leaderboards — which ones, and what ships first

Persistent, **server-authoritative** standings (client never writes its own score — see §5). Every board is read-only to the client and displays: **top 3 highlighted** (podium treatment), then a scrollable list, plus a **pinned "You" row** showing the player's rank + the ±2 neighbors around them.

Backend already has the shape for this (`docs/v2-unity-port-backend-spec.md` §3.3 `leaderboard_scores`: `wallet_address, period_id, metric, score`, and `/api/leaderboard?period=weekly` in §2.4). The design below maps onto that `metric` column.

**Proposed leaderboard set:**

| Board (`metric`) | Source | Period | Ship phase | Notes |
|---|---|---|---|---|
| **Highest Wave Survived** | `WaveManager` max wave reached on a run | Weekly + All-time | **SHIP FIRST** | Clearest single skill number; already instrumented for WO-121 (`wave_cleared` / `run_ended`). Lowest backend lift. |
| **Longest Hold (time)** | survival time before Heart falls | Weekly | Phase 1 | Second-best "I'm good at defending" metric. |
| **Total Resources Harvested** | lifetime crystal/coin/glimmer (idle-loop flex) | All-time | Phase 2 | Rewards the harvest/offline spine, not just combat. |
| **Clan Leaderboard** | sum/avg of member scores per clan | Weekly | Phase 2 | Ties into existing `ClanService`. "Compete for your clan." |
| **Challenge Arena Rank** | async PvP ELO/ladder | Weekly + Season | **LATER** (gated on Arena) | The eventual *primary* competitive board + tournament feed. Reserve the metric name now; do not build until Arena exists. |

**Recommendation:** ship **Highest Wave Survived (weekly + all-time)** as the MVP board — it reuses WO-121 events, needs only one `metric`, and proves the whole top-3 + your-rank UI. Add Longest Hold next (same UI, new metric). Clan + Total Resources are Phase 2. Arena is the end-game cap and stays reserved.

**Period model:** weekly boards reset Monday 00:00 UTC (matches monetization §12 Stream B weekly leaderboard and the SKR payout cadence). All-time boards never reset. `period_id` carries the week key (e.g. `2026-W22`) or the literal `alltime`.

### 2.2 Player profile & username

A profile is the **identity layer the leaderboard needs** — a wallet address is not a name. Profile fields:

| Field | Required? | Notes |
|---|---|---|
| **Username** | **YES (recommended required)** | The leaderboard display name. See policy below. |
| Avatar / hero portrait | No (defaults to current hero) | Pulls from existing hero roster; no new art needed for MVP. |
| Headline stats | Auto | Best wave, longest hold, total resources, current rank(s), clan tag. |
| Linked socials | No (opt-in) | See §2.3. |

**Username policy (recommendation):**
- **Required for leaderboard identity, but never blocks play.** On first launch the player is auto-assigned a default (`Defender#1234`, derived from a server-issued id) so they can play immediately and still appear on boards. A non-blocking prompt invites them to pick a real name.
- **One free rename**, then renames cost a soft-currency (or are capped, e.g. 1 / 30 days) — kills churn-spam and name-squatting (US-8).
- **Uniqueness:** enforced **server-side** (case-insensitive). Client does a soft availability check; server is the authority and returns `USERNAME_TAKEN` on collision.
- **Profanity / safety:** server-side denylist + basic normalization (leetspeak/whitespace) on set and rename; reject with `USERNAME_REJECTED`. Keep the list server-side so it can be updated without a client ship. This mirrors the existing templated-phrase chat-safety stance (no free-text moderation surface) — usernames are the one free-text field, so they get the gate.
- **No PII in usernames** — surface a one-line notice that the username is public.

> **Identity key vs display name:** the durable identity key stays the **wallet address** (already the save key, per WO-120 §D). Username is a *display label* mapped to that wallet. This keeps profiles portable across the React web build and the Unity build (NORTH STAR: "continuous social state").

### 2.3 Social linking — opt-in, a path never a wall

- **Strictly opt-in.** The game is fully playable, fully rankable, with zero social accounts linked. Linking only *adds* (display flair, friend-find, verified handle, share convenience).
- **What it unlocks:** (1) a verified handle badge on the profile, (2) **friend-find** ("who that I follow is already playing") for ladder rivals (US-11), (3) one-tap share for the install post.
- **Providers (MVP):** X (Twitter) — already the share target in `ReferralService.ShareOnX`. Discord as a Phase-2 add (community lives there).
- **Privacy:** linking stores only the minimum (handle + provider id), surfaced on the public profile only if the player toggles "show on profile." Unlink any time; unlinking purges the stored handle (ties to the GDPR delete cascade, backend §3.3 `deletion_requests`).

### 2.4 Install / referral bonus — the viral-acquisition loop

This **extends the already-built `ReferralService`** (`Assets/_Modules/Core/Referral/ReferralService.cs`), which already does generate / share-on-X / claim with server-side abuse prevention. We are layering the *"I just installed"* share + bonus onto that rail, not greenfielding.

**Two distinct moments:**
1. **Install brag (the new user's own post).** After onboarding, a non-blocking prompt: *"Tell the realm you've arrived."* One tap posts a pre-composed *"I just installed Defenders of the Realm"* message (with the player's referral link) and grants a **one-time install bonus**. This is the player rewarding *themselves* for becoming an advertiser.
2. **Referral claim (the friend who arrives).** Unchanged from existing flow — a new player enters/deep-links a referral code; claimer gets a reward, referrer gets a capped reward (US-13). Already built and server-guarded.

**Reward (recommendation — owner sets final numbers):**
- Install-brag bonus: a modest one-time grant (e.g. Aether Crystals + a cosmetic profile flair "Founding Herald"). Cosmetic flair = flex, not power (NORTH STAR guardrail #1: *sell/grant flex, not power*).
- Referral rewards: keep the existing crystal grant; cap per referrer per period (already enforced server-side).
- **Keep high-value/crypto rewards out of the install-brag.** The brag bonus is small + cosmetic so it can't be farmed for real value.

**Attribution & anti-abuse (US-14) — this is the load-bearing part:**
- **One-time, server-validated.** The install bonus is granted by the server, keyed on the durable identity (wallet), recorded once — the client never self-grants. This is the same idempotency pattern as `achievement_grants` (PK = `(wallet, achievement_id)` structurally prevents double-grant, backend §3.3). Model the install bonus as a one-time achievement grant `install_brag`.
- **Referral attribution:** referral-code or deep-link (the existing `referralUrl`). Self-referral rejected server-side (`SELF_REFERRAL`), one claim per player (`ALREADY_CLAIMED`), referrer cap per period (`CAP_REACHED`) — all already in `ReferralService`'s contract.
- **High-value rewards MUST be server-validated** (NORTH STAR / WO-120 §D / backend §2.6, §3.3). The client *requests*; the server *grants*. The brag/referral endpoints move (eventually) into the wallet-signed-nonce protected set if any reward has real value.
- **Posting cannot be verified directly** (we can't read the user's timeline). So the bonus fires on *the share intent completing once per player*, not on proof-of-post — and is therefore deliberately small/cosmetic. The real attribution value comes from the **referral link** that rides along (verifiable installs), not the brag itself.

---

## 3. DEPENDENCIES

- **Backend / server (WO-120) — REQUIRED, BLOCKING for persistence + anti-abuse.** Per WO-120 the **backend was NEVER connected** — leaderboards, profiles, and validated bonuses are all server-authoritative and cannot persist without it. The `leaderboard_scores` table + `/api/leaderboard` already exist as spec (backend §3.3, §2.4); this WO adds **profile/username** and **install-brag** endpoints to that contract. Until the backend stands up, client UI ships behind the existing env flag (no-op / mocked, same pattern as cloud-save).
- **Metrics (WO-121) — OVERLAP.** The wave/run events WO-121 instruments (`wave_cleared`, `run_ended`) are the **same signal** that feeds the Highest-Wave board. Coordinate: emit once, consume in both the metrics table and the leaderboard score write. Do not double-instrument.
- **Store vs crypto build considerations:** usernames, profiles, social linking, and the cosmetic install bonus are **fine in BOTH builds** (no crypto mechanic). **Keep crypto out of the store build** — the SKR/tournament *prize-pool* leaderboard payouts (monetization §12) are crypto-build-only; the **display** leaderboard is universal. In the store build the board shows ranks + cosmetic rewards only; payouts are compiled out with the `DeNelle.Wallet`/`DeNelle.Web3` modules (NORTH STAR two-build strip). Username/social do not pull in any crypto SDK.

---

## 4. FILES / SYSTEMS TABLE

### Client (Unity — CLI, gated behind env flag, through the brace/compile gate)
| System | New or existing | Role |
|---|---|---|
| `LeaderboardService` (Core) | **NEW** | Fetches `/api/leaderboard?period=&metric=`; returns top-N + caller rank window. Read-only. `Result<T>` pattern. |
| `LeaderboardPanel` (HUD) | **NEW** (code-built UI — no UXML, per pipeline) | Top-3 podium + scroll list + pinned "You" row; board/period tabs. |
| `ProfileService` (Core) | **NEW** | Get/set username (soft availability check), read headline stats, link/unlink social. |
| `ProfilePanel` (HUD) | **NEW** (code-built) | Username, avatar/hero, headline stats, linked-social toggles. |
| `ReferralService` (Core) | **EXISTING** — extend | Add the "install brag" one-time bonus request on top of generate/share/claim. |
| `InviteFriendsUI` (Core) | **EXISTING** — extend | Surface the "I just installed" brag prompt. |
| `EventTracker` hooks | **EXISTING** — reuse WO-121 events | `wave_cleared`/`run_ended` feed the score write (no new events). |

### Backend endpoints (Kayden — extends WO-120 contract)
| Endpoint | Purpose | State |
|---|---|---|
| `GET /api/leaderboard?period=<id>&metric=<m>&wallet=<addr>` | Top-N for a board + the caller's rank window | spec'd (§2.4) — needs metric + rank-window params + deploy |
| `POST /api/leaderboard/submit` (or server-derived from events) | Server records authoritative score per (wallet, period, metric) | **NEW** — server-side write; client never sets score directly |
| `GET /api/profile?wallet=<addr>` | Read a profile (username, avatar, headline stats, public socials) | **NEW** |
| `POST /api/profile/username` | Set/rename username — uniqueness + profanity + rename-cost enforced | **NEW** (free-text safety gate lives here) |
| `POST /api/profile/social/link` / `/unlink` | Opt-in link/unlink a social handle | **NEW** |
| `POST /api/referral/install-brag` | One-time install bonus grant, idempotent on wallet (`install_brag` achievement-style) | **NEW** — server-validated, server-granted |
| `/api/referral/generate`, `/api/referral/claim` | Existing referral flow | spec'd (WO-120) — needs deploy |

New backend table(s) implied: `player_profiles` (`wallet_address PK, username UNIQUE (ci), avatar_id, social_links JSONB, created_at, renamed_at`) — Kayden owns the migration. `leaderboard_scores` already exists. Install bonus reuses the `achievement_grants` idempotency pattern (`install_brag`).

---

## 5. DO NOT TOUCH / GUARDRAILS

- **NO client-authoritative scores.** The client **never** writes or asserts a leaderboard score, never self-grants a bonus. The server derives/validates scores from gameplay events and is the sole authority (backend §2.6, WO-120 §D: "verify on the server, never the client"). A client-set score is a free-money / fake-rank exploit.
- **Username is the only free-text field — it gets the server-side safety gate.** Do not add free-text fields without a moderation surface (chat stays templated, per `docs/mvp-chat-spec.md`).
- **Social linking and install bonus are OPT-IN, non-blocking.** Never gate play, ranking, or the core loop behind linking a social account or posting.
- **No crypto in the store build** — leaderboard *display* is universal; SKR/tournament *payouts* are crypto-build-only and compile out with the wallet modules.
- **Sell/grant flex, not power** — install bonus + leaderboard rewards are cosmetic/status, not stat-boosting (NORTH STAR guardrail).
- No `.cs` edits from UI; CLI builds + brace/compile-gates all client code. No scene hand-edits.

---

## 6. ACCEPTANCE CRITERIA

- [ ] USER STORIES (US-1…US-14) captured and traceable to design sections.
- [ ] Leaderboard set proposed with a recommended **ship-first** board (Highest Wave Survived, weekly + all-time).
- [ ] Leaderboard UX defined: **top 3 highlighted + the player's own rank window**, visible to everyone, read-only client.
- [ ] Username decision: **required (with auto-default + one free rename)**; uniqueness (server, case-insensitive) + profanity (server denylist) handling specified.
- [ ] Player profile fields defined (username, avatar/hero, headline stats).
- [ ] Social linking specified as **opt-in**, with what it unlocks and that it is never a wall.
- [ ] Install/referral bonus: reward defined (one-time, small/cosmetic), attribution via referral-code/deep-link, **server-validated + idempotent** anti-abuse, tied to WO-120.
- [ ] Dependencies on backend (WO-120, blocking) + metrics (WO-121, overlap) flagged; store-vs-crypto build note included.
- [ ] Files/Systems table (client UI + backend endpoints) + "Do NOT touch" guardrails present.

---

## 7. OPEN PRODUCT QUESTIONS (owner — Samantha)

1. **Username required or optional?** Recommended **required** with an auto-assigned default + one free rename. Confirm — or do you want it fully optional (wallet shown until they set one)?
2. **Which board ships first?** Recommended **Highest Wave Survived**. Agree, or lead with **Longest Hold** / a combined "score"?
3. **Period cadence:** weekly reset (Mon 00:00 UTC, matches SKR payouts) + a permanent all-time board — confirm, or daily/seasonal too?
4. **Install-brag reward:** crystals + a cosmetic "Founding Herald" flair? Set the exact amounts. Confirm it stays **cosmetic/small** (not real-value) so it can't be farmed.
5. **Social providers for MVP:** X only first, Discord Phase 2 — agree?
6. **Rename cost:** soft-currency price, or a hard cap (1 / 30 days)? Pick the lever.
7. **Clan leaderboard scoring:** sum of member scores, average, or top-N members? (Affects whether big clans always win.)
8. **Store-build leaderboard:** confirm it shows ranks + cosmetic rewards only, with SKR payouts compiled out — no crypto in the store binary.

---

🤖 Drafted by UI (product/design). Backend persistence is server-authoritative and blocks on WO-120; metrics instrumentation overlaps WO-121. Ready for owner routing + final number-setting.
