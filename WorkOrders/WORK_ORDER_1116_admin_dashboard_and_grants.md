# WORK ORDER 1116 — Operator dashboard: phase 1 SHIPPED (read-only), phase 2 SPEC (issuing codes)

**Status:** BLOCKED — PHASE 2 has 4 open owner rulings. PHASE 1 IMPLEMENTED (awaiting gate + owner felt-verify) · PHASE 2 SPEC — NOT IMPLEMENTED, 4 open rulings
**Minted:** 2026-08-17 (main line — banner bumped 1116 → 1117 in this same edit)
**Lane:** Monetization / live-ops · backend + public-site static page. **No Unity, no `Assets/**`.**
**Provenance:** owner, 2026-08-17, verbatim: *"we need to be able to do this from a dev panel for users
wallets and stuff"* and *"would be nice to see player stats and retention rates, active players,
purchase stats"*.
**Related:** **WO-1115** (redeem codes — the PLAYER-facing rail and its security ruling). This WO is the
**OPERATOR** side of the same machinery and deliberately does not restate it. Read 1115 §2 first.

---

## PHASE 1 — the read-only dashboard (built)

| File | What it is |
|---|---|
| `api/admin/stats.js` | New read-only aggregate endpoint. Five views: `overview`, `retention`, `funnel`, `economy`, `players`. |
| `site/admin.html` | New single self-contained page. Unlisted, `noindex`, key-gated, nothing links to it. |
| `api/DB_SETUP.md` §17 | What each metric means, and every caveat that makes a number less true than it looks. |

**Untouched on purpose:** `api/admin/db.js`, `api/promo/redeem.js`, `api/_lib/wallet-auth.js`.
`db.js` is the raw-table triage tool and stays a working tool; `stats.js` is a second, separate file so
dashboard work can never destabilise it.

### The invariants phase 1 holds (and phase 2 must not weaken)

1. **One auth scheme.** `X-Admin-Key` vs `process.env.ADMIN_DASH_KEY`, constant-time compare, copied
   verbatim from `db.js`. Never configured → refuse everything (never fail open).
2. **GET + OPTIONS only**, and every statement is a `SELECT`. There is no write path in the file.
3. **Every query has a hard LIMIT.** `analytics_events` is ~87k rows and only grows.
4. **Parameterised tagged templates only.** No string-built SQL.
5. **Player ids are masked** to `first4…last4` everywhere except one explicit single-player lookup.
6. **`properties` is never returned wholesale** — it is free-form client JSONB.
7. **The key is never persisted** by the page: memory only, never localStorage / sessionStorage /
   cookie / URL. `site/` is a PUBLIC deployment; a key written there is a key on the internet.

---

## PHASE 2 — issuing codes and grants from the panel (SPEC ONLY — DO NOT BUILD YET)

The owner's other half — *"do this ... for users wallets"* — is a **write** surface on a live,
payments-adjacent, PUBLISHED game. It is specified here and deliberately not built in the same pass as
the read-only view.

### 2.1 ⛔ Issue a BOUND CODE. Do not write to a save row.

There are two ways to give a player something, and they are not close in risk:

| Route | What happens | Risk |
|---|---|---|
| **(A) Issue a bound promo code** — insert one row in `promo_codes` with `bound_wallet = <player>`, tell the player the code | The player redeems in-game through `api/promo/redeem.js`, which already authenticates the wallet, checks expiry/caps/duplicates, writes the audit row in `promo_redemptions`, and grants through the client's normal reward path | **The safe primitive.** Every existing gate still runs. A leaked code is INERT — `redeem.js:172` refuses any other wallet and does not consume the code. |
| **(B) Write resources straight into `player_data.game_state`** | The operator edits a live save blob | **Do not build this.** It bypasses *every* ledger path the game has: no redemption row, no cap, no expiry, no client-side grant/animation/analytics, and it races the client's own delta-merge upsert — a player syncing mid-edit silently overwrites or is overwritten. A wrong keystroke is a corrupted save with no undo, and after the fact a direct write is indistinguishable from a compromised admin key. |

**Recommendation: build (A) and only (A).** It reuses a rail that is already reviewed, already
authenticated, and already audited. If the owner later needs something a code cannot express, that is a
new grant TYPE on the code rail (WO-1115 R1), not a new write path into saves.

### 2.2 The endpoint shape

`api/admin/grant.js` — **new file. Never add a write branch to `stats.js` or `db.js`.**

- **POST only** (plus OPTIONS). GET must 400 — a grant must never be reachable from a URL bar, a link
  preview, or a browser history entry.
- **Same `ADMIN_DASH_KEY`, same constant-time compare, same never-fail-open.** No second auth scheme.
- Body: `{ player_id, reward_crystals, reward_coins, message, expires_at, note }`.
- Behaviour: generate a random, unguessable code (≥10 chars, uppercase — the client uppercases before
  sending), `INSERT INTO promo_codes (..., bound_wallet = player_id, max_redemptions = 1,
  expires_at = <required>)`, return the code once.
- **`bound_wallet` is mandatory on every code this endpoint issues.** An unbound code from an admin
  panel is a public free-money code with extra steps. Public campaign codes stay a hand-written
  `INSERT` in the Neon console, where the deliberation is the safety feature.
- **`expires_at` is mandatory.** A never-expiring grant code is a permanent liability.
- Validate `player_id` against the same shape rules `wallet-auth` uses. Refuse `anonymous`. A guest id
  (`guest-local-…`) cannot key a wallet-gated feature and must be refused with that reason stated.

### 2.3 ⛔ An audit row per write, written in the same transaction

**Non-negotiable.** After the fact, an unaudited grant is indistinguishable from an exploit — there is
no way to tell the owner's own grant from a stolen key's. Every write appends:

```sql
CREATE TABLE IF NOT EXISTS admin_audit (
    audit_id    BIGINT      GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    action      TEXT        NOT NULL,       -- 'issue_bound_code'
    target      TEXT,                       -- the player_id the code is bound to
    detail      JSONB       NOT NULL DEFAULT '{}',  -- code, rewards, expiry, operator note
    actor_hint  TEXT,                       -- sha256(admin key)[0:12] + request IP hash — see below
    created_at  TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
```

- **Never store the admin key, or anything reversible to it.** `actor_hint` is a truncated hash — it
  distinguishes "a different key was used" from "the usual key", which is the question that matters,
  without becoming a credential at rest.
- The audit insert and the `promo_codes` insert go in **one transaction**. A grant that exists with no
  audit row is exactly the state this table is for.
- Surface the audit log as a **read-only view in `stats.js`** (`?view=audit`) so the owner can see her
  own grant history on the same page. Reads there are safe; writes stay in `grant.js`.

### 2.4 Rate limit and blast radius

Cap issuance (e.g. 20 codes/hour per key) and cap `reward_crystals` / `reward_coins` per code at a
sane ceiling. A leaked key with an uncapped mint is the whole economy. The cap is a constant in the
file, not a request field.

### 2.5 ⛔ Open rulings (owner)

- **P1** — Should the panel be able to issue a code to a player it looked up by masked id, or must the
  owner paste the full wallet? *(Recommendation: full-id lookup via the existing single-player view,
  then an explicit confirm showing the full address — a grant to the wrong wallet is unrecoverable.)*
- **P2** — Reward ceiling per code, and per hour.
- **P3** — Does phase 2 also need a **kill switch** (flip `promo_codes.active = false`)? *(That is a
  write, but a strictly de-privileging one, and it is the thing you want at 2am when a code leaks.
  Recommendation: yes, and it is the second write to build after issuance.)*
- **P4** — Should `ADMIN_DASH_KEY` be split into a read key and a write key? *(Recommendation: yes,
  eventually. The read key ends up typed on a phone, screenshotted and shared far more casually than a
  key that can mint value. Phase 1 shipping on one key is acceptable; a write surface on that same key
  is where it stops being.)*

---

## Acceptance (phase 1)

- [ ] `node --check` clean on `api/admin/stats.js` *(done)*
- [ ] Compile/deploy gate green; owner opens `https://echoes-of-elarion.vercel.app/admin`, enters the
      key, and all five tabs render against live data
- [ ] `ADMIN_DASH_KEY` confirmed present on the **`defenders-of-the-realm-v2`** Vercel project
- [ ] No full wallet address appears anywhere except the single-player drill-down
- [ ] Owner felt-verifies on the Seeker (phone layout) and CLOSES — per §13, PO closes, not CLI

## What this must NOT touch

`api/admin/db.js`, `api/promo/redeem.js`, `api/_lib/wallet-auth.js`, `site/index.html` (the dashboard
stays unlisted), and anything under `Assets/`.
