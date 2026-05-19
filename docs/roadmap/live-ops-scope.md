# Live-Ops & Admin Portal — Scope Note

**Status:** Future workstream. **NOT** part of the 8-week v2 Unity port
(`docs/v2-unity-port-spec.md`). Captured so the operator/live-ops layer is
tracked for the post-Week-8 scope/budget decision.

---

## 1. What this is

The v2 Unity port delivers the **game client** — a playable foundation. A
monetized, *live* game also needs an **operator side**: a backend plus an admin
dashboard for running the game as a service.

This is a **separate project** — a web backend, a database, an admin web app,
and a secured Solana signing service. It is not Unity work and must not be
folded into the client foundation. The 8-week spec deliberately ends Week 8
with an owner decision (continue to v2.1 / pause / hire a contractor); this
live-ops layer is exactly the kind of scope that decision governs.

## 2. Why it matters (not optional for a real economy)

The **security audit (SEC-006)** and the **missing-components audit** flag the
same root cause: **entitlements are currently client-trusted.** A hand-edited
local save grants paid pack contents for free. For any real-money economy the
monetization loop must be **server-authoritative** — and that server *is* the
live-ops backend.

So this is not merely "ops convenience tooling." It is the missing **server
half** of the monetization the audits describe as currently "half a loop."

## 3. Capabilities

### 3.1 Admin portal (umbrella)
A web dashboard with operator authentication + role-based access; hosts 3.2-3.6.
**Stack:** web backend (API + database) + a dashboard frontend.
**Current state:** none. **Size:** medium — it is the foundation the rest sits on.

### 3.2 Issue / support resolution tracking
A ticket/support view — player reports, status, assignee, resolution — keyed to
player accounts. Could be custom or an integration (Zendesk-style).
**Current state:** none; no player-account system exists yet either.
**Dependency:** player accounts/identity. **Size:** medium.

### 3.3 Purchase clearing + histories
Every pack purchase recorded: player, pack, on-chain tx signature, amount,
currency, status (pending / confirmed / failed / refunded), timestamp.
"Clearing" = reconciling on-chain transactions against entitlement grants and
flagging failed/disputed ones.
**Current state:** Solana's chain is the raw tx ledger, but the game records
**no durable entitlement and no receipt** (missing-components P0). A
server-side entitlement/receipt service is the prerequisite.
**Dependency:** the server-authoritative entitlement model (§2). **Size:** large
— this is the core of fixing the "half a loop."

### 3.4 Pack deployment
Define / price / schedule / retire packs **server-side**; the client fetches the
catalog at runtime instead of shipping a static file.
**Current state:** packs are static client data (`packs.json`) — changing them
needs a full game update.
**Dependency:** a pack-config service + the client switching from a bundled
`packs.json` to a fetched catalog (a small, well-isolated client change).
**Size:** medium.

### 3.5 Competitions / events
Leaderboards, time-limited events, entry, scoring, prize pools.
**Current state:** none; not specced anywhere.
**Dependency:** player accounts + a scoring/leaderboard service. **Size:** large.

### 3.6 Reward payouts
Paying SOL / USDC / SKR from the Rewards Distributor wallet to players
(competition prizes, etc.): a payout service, batching, and an audit trail.
**Security-critical:** per spec Part 10, signer keys never touch the client —
payouts run server-side through a **secured signer** (HSM / KMS / a hardware
wallet), with a full audit log. Mainnet payouts need explicit owner approval.
**Current state:** only the distributor's *public* address exists (in
`wallets.json`, for transparency). **Size:** large + high-security.

## 4. Architecture sketch

Not Unity. A typical shape:

- **Backend API + database** — accounts, entitlements, purchase ledger, pack
  config, tickets, competitions.
- **Admin dashboard** — a web frontend over that API (the "portal").
- **Solana service** — verifies inbound purchase transactions on-chain (the
  authoritative entitlement grant) and signs outbound reward payouts through a
  secured signer.
- **Client touch-points** (small, isolated changes to the Unity game): fetch
  the pack catalog instead of bundling it; submit purchases to the server for
  verification; read entitlements from the server, not local PlayerPrefs.

## 5. Suggested sequencing

1. **Server-authoritative entitlements + purchase ledger** (3.3 core) — closes
   the monetization-integrity hole the audits flagged. Highest priority.
2. **Admin portal shell + accounts** (3.1) — the foundation for everything else.
3. **Pack deployment** (3.4) — high operator value, moderate cost.
4. **Reward payouts** (3.6) — needed before any competition can pay out.
5. **Competitions** (3.5) and **support tracking** (3.2) — once the above exist.

## 6. Bottom line

None of §3 exists today, and none is in the 8-week Unity spec. It is a real,
necessary system for a monetized live game — and a **distinct project** on the
order of multiple person-months, best built as its own workstream with its own
plan. Recommended as a **post-Week-8 / v2.1+ decision**, not work to fold into
the client foundation.

The single most important item — **server-authoritative entitlements (3.3)** —
should be flagged now: until it exists, the devnet/mainnet purchase flow cannot
be trusted, regardless of how polished the in-game store looks.
