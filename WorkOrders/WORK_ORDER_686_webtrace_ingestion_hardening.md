<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-07-12
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-07-12) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK ORDER 686 — Web-trace ingestion hardening (rate-limit + size-cap the open POST sinks)

**Status: READY TO IMPLEMENT.** **Lane:** Persistence/Backend (Lane 7) + Build/Deploy (Lane 10 for the
WAF half). **Type:** EXISTING (the endpoints ship live and unbounded). **Silo:** `api/` serverless +
Vercel WAF config (file-disjoint from Unity lanes).

**Numbering note:** minted **686** from the `CLI_LANES_WO_NUMBERS.md` banner (part of the 685/686/687
web-trace lifecycle trio; next-free bumped to 688 in the same edit).

## Symptom

The three unauthenticated client-write endpoints — `POST /api/trace`, `POST /api/events/track`,
`POST /api/bug-report` — accept **any** body from **any** origin (`CORS *`) with **no auth, no rate
limit, and no size cap**. A single scripted client can flood Neon with rows (each web_trace batch is up
to ~200 lines; bug-report bodies run ~300KB) — unbounded DB growth + Vercel function-invocation cost +
a trivial amplification/DoS surface. WO-685 reaps old rows on a 7-day lag; this WO stops the abusive
INSERT from happening in the first place.

## RCA / evidence (cited)

- **`docs/SECURITY_AUDIT_2026-07-12.md:7-14`** — H1/H2 top-of-list: *"Open unauthenticated write
  endpoints, no rate limit, unbounded DB growth. `trace.js`, `events/track.js`, `bug-report.js`
  (~300KB/POST) … all accept client-supplied playerId with no auth."* Prescribed: *"WAF rate-limit on
  /api/trace, /api/events/track, /api/bug-report."*
- **`docs/SECURITY_AUDIT_2026-07-12.md:26`** — M3: `CORS *` on trace/track/bug-report *"fine while
  bearer-token (no cookies); note it"* — CORS stays `*` (mobile Pi cross-origin needs it); the control is
  rate + size, not origin.
- **`docs/SECURITY_AUDIT_2026-07-12.md:24`** — M1: `?trace=1` is *"amplification into H1"* — a support
  toggle that any URL can flip on, multiplying ingest volume; the size/rate cap is its backstop.
- **`api/trace.js:21-56`** — no body-size check; `lines` is built from an unbounded array
  (`body.map(toStr)` / `body.entries.map(toStr)`) then INSERTed whole (`:73-81`). No per-session cap.
- **`api/events/track.js:42-88`** — loops `for (const ev of events)` with **one INSERT per event** and
  **no cap on `events.length`** — a 10k-event array = 10k INSERTs in one call.
- **`api/bug-report.js`** — the 4000-char cap is **client-side only** (`api/schema.sql:433` comment:
  *"Description is capped at 4000 chars client-side"*); the server trusts it.
- **Client already sizes its own batches** (`WebTrace.cs:67-70`: `RingCap 500`, `MaxBatch 200`) — so a
  legitimate client never exceeds those; a server cap only rejects abuse, never real traffic.

## Exact steps

**Lane A — server-side body/shape caps (in each function, cheap, deploy-independent):**
1. `api/trace.js`: reject (`400`) or truncate when the normalized `lines` array exceeds a hard cap
   (e.g. **250** lines, ≥ the client's `MaxBatch 200`), and reject when the raw body exceeds a byte
   cap (e.g. **256 KB**). Keep the fire-and-forget 200 contract for legitimate sizes.
2. `api/events/track.js`: cap `events.length` (e.g. **200** per call); insert the first N, drop the
   rest (or `400`). Prefer a **single multi-row INSERT** over the N-INSERT loop to bound DB round-trips.
3. `api/bug-report.js`: enforce the 4000-char `description` cap **server-side** (truncate + flag), and
   cap the total body bytes. Do not trust the client cap.
4. All three: keep status codes **200 | 400 | 500 only**; keep `CORS *` (M3 — bearer model, no cookies).

**Lane B — Vercel WAF rate-limit (platform, deploy-time; see `vercel:vercel-firewall`):**
5. Add WAF rate-limit rules scoped to the three paths (`/api/trace`, `/api/events/track`,
   `/api/bug-report`) — a per-IP request budget (e.g. N req / 10s, tune from live volume) with a
   `429`/challenge action. Stage first, then enforce. Owner applies in the Vercel dashboard / firewall
   CLI (document the exact rule in the RESULT).

**Lane C — regression:** a headless probe that POSTs an oversize `lines` array + an oversize `events`
array + an oversize bug-report body and asserts each is rejected/truncated (not INSERTed whole), and
that a normal-sized batch still returns `200 inserted:N`.

## Acceptance

- [ ] Oversize `POST /api/trace` (e.g. 5000 lines / >256KB) is rejected or truncated to the cap — never
      INSERTed whole; a ≤200-line batch still returns `200`.
- [ ] `POST /api/events/track` with a 10k-event array inserts at most the cap and does not fan out to
      10k INSERTs.
- [ ] `POST /api/bug-report` description > 4000 chars is truncated server-side; oversize body rejected.
- [ ] WAF rate-limit rule live on all three paths (owner-applied); documented in the RESULT with the
      exact threshold + action.
- [ ] Status codes remain 200|400|500 from the functions; CORS unchanged.

## What NOT to touch

- `CORS *` on these endpoints (M3 — required for Pi cross-origin; the control is rate + size, not origin).
- The `WebTrace.cs` client (its `RingCap`/`MaxBatch` already self-bound; server caps sit ≥ client caps
  so legitimate traffic is untouched) — client changes are a separate ticket if ever needed.
- `api/admin/cleanup.js` / the retention cron (WO-685) — complementary, not a substitute.
- The reward/auth endpoints (`promo/redeem`, `referral/*`, `game/save`) — audit M2/H4 are separate WOs.

*Proof source: `docs/SECURITY_AUDIT_2026-07-12.md` H1/H2/M1/M3; `api/trace.js`, `api/events/track.js`,
`api/bug-report.js`, `api/schema.sql`, `WebTrace.cs` as cited.*
