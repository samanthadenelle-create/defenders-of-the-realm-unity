# Security Audit — 2026-07-12 (read-only; no secret values)

Surface: Vercel serverless `api/` (20 endpoints), Neon Postgres, Unity WebGL client.
SQL: uniformly parameterized (tagged templates) — no injection found. No committed secrets
(placeholders only). **CRITICAL: none.**

## Top-3 fix-before-Pi-go-live
1. **H1/H2 — Open unauthenticated write endpoints, no rate limit, unbounded DB growth.**
   `trace.js`, `events/track.js`, `bug-report.js` (~300KB/POST), `tower-swap/log.js`,
   `promo/redeem.js`, `referral/*` all accept client-supplied playerId with no auth.
   **The 7-day web_trace TTL cron DOES NOT EXIST** (no `crons` in vercel.json, no cleanup fn).
   Fix: Vercel Cron `DELETE FROM analytics_events WHERE event_name='web_trace' AND received_at
   < NOW() - INTERVAL '7 days'` (+ `auth_nonces` sweep) + WAF rate-limit on /api/trace,
   /api/events/track, /api/bug-report.
2. **H4 — Release builds let any player self-grant premium currency.** The HelpMenu 5-tap dev
   unlock (`HelpMenu.cs:70-75,155-175,234-276`) is NOT behind `#if DEVELOPMENT_BUILD` (unlike the
   DevTools launcher at :315) and grants 25k crystals; save.js guards only rollbacks, and
   `BackendAuthConfig.Enforced` is OFF. Fix: wrap the unlock + grant in
   `#if DEVELOPMENT_BUILD || UNITY_EDITOR` (or owner-wallet gate) before any public build.
3. **M2 — Reward endpoints unauthenticated:** `promo/redeem`, `referral/claim` + `generate` grant
   crystals on an invented playerId (self-referral farming). `install-brag.js` IS nonce-gated —
   apply the same `verifyAndConsume` to the other three before crystals carry value.

## Medium / Low (detail)
- M1 `?trace=1` = amplification into H1 (flag allow-list itself is sound — no game-state flip).
- M3 CORS `*` on trace/track/bug-report/pi-verify — fine while bearer-token (no cookies); note it.
- M4 `auth_nonces` pruned only per-wallet opportunistically — fold into the H1 cron.
- L1 reward amounts fall back to hardcoded literals if env unset (claim=25, brag=50) — confirm.
- L2 referral codes via Math.random (share codes, not tokens) — accepted.
- L3 auth error `reason` strings are a minor probing oracle — accepted.
- L4 bug-report legacy host mismatch (schema.sql:414) — ops concern.

## Accepted-by-design (verified)
PlayerPrefs local save editable (soft-currency-client-owned stance; nonce-auth scaffolding exists,
flip `BackendAuthConfig.Enforced` when currency is real — save/load signature scheme verified
sound: single-use atomically-burned nonce, wallet+nonce+payload-hash binding). AdminOverlay fully
`#if`-gated + chord kill-switch. No key/mint in the client (empty/placeholder verified). Leaderboard
/profile endpoints expose no private data. `api/` is git-TRACKED (not gitignored as earlier canon
said — correct the anchor next session). One action: DEPLOY.md:11 flags a Neon credential once
pasted in chat — **confirm it was rotated**.
