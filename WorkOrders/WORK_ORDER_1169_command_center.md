# WORK ORDER 1169 — The Command Center: the parts are built, there is no console — and the MONEY is the part nobody can see

**Status:** READY — ⭐ **§3 IS DONE 2026-08-24** (4f8c2f23d + ecbd5047a): `purchase_quotes` / `purchase_entitlements` are in the `api/admin/db.js` probe list, and `api/admin/stats.js` serves `?view=purchases` from `purchase_entitlements` with the quote→settle funnel, the analytics view kept and relabelled, and `client_events_without_entitlement` as the alert. §5–§7 still need scoping — that is what keeps this Ready.

**Minted:** 2026-08-24 (CLI), banner bumped 1169 → 1170 in the same edit.
**Provenance:** owner, 2026-08-24 — *"I wanna really start thinking about how we set up a command
center. I think we have a structure in place but we need to be able to transaction log the
troubleshoot things. See tickets maybe even push promos."*

⭐ **The owner's read is correct: the structure IS in place.** This is not a greenfield. Every one of
the four pillars has working backend today. What is missing is a **surface** — and, in one case, a
data source that is wrong.

---

## 1. What already exists, verified at source

| Pillar | Backend today | Surface today |
|---|---|---|
| Transaction log | `purchase_quotes`, `purchase_entitlements`, `/purchases/{quote,verify,fulfill,reconcile}` | ⛔ **NONE** |
| Troubleshoot | `bug_reports`, `analytics_events` (87k+ rows), `/bug-report`, `/trace`, F8 `break-log.jsonl` | `admin/db.js` (raw rows), `tools/db-viewer/index.html` |
| Tickets | `WorkOrders/*.md` → `BOARD.html` (`tools/board_build.py`) | BOARD.html — **dev tickets only, not player issues** |
| Promos | `promo_codes`, `promo_redemptions`, `/promo/redeem` | ⛔ **no authoring surface** — codes must be inserted by hand |

Admin plumbing is real and sound: `api/admin/db.js` (raw-table viewer, **read-only by construction**
— every statement a SELECT with a hard LIMIT), `api/admin/stats.js` (aggregates), `admin/cleanup.js`,
gated by `ADMIN_DASH_KEY`.

## 2. ⛔ THE FINDING: the money tables are invisible to BOTH admin surfaces

`api/purchases/*.js` reads and writes **`purchase_quotes`** and **`purchase_entitlements`**. These are
the SERVER's authoritative record: the exact base-unit amount, mint, decimals, destination, the tx
signature, and settlement state.

- **`api/admin/db.js` names them ZERO times.** Its table list has 15 entries — `player_data`,
  `analytics_events`, `bug_reports`, `promo_codes`, `referrals`, `tower_swaps`, `leaderboard_scores`
  … and **neither purchase table**.
- **`api/admin/stats.js`'s `economy` view does not read them either.** It aggregates
  `purchase_completed` out of `analytics_events` — a **client-emitted** event.

⚠ **And that event carries no money.** The file says so in its own comment: *"`purchase_completed`
carries NO price field. PackStore.cs:582 emits `{packId, packName, currency, txSig}` only… this view
reports COUNTS, never revenue."*

> ## So the only "purchase" view we have counts what the CLIENT said happened, while the SERVER's
> settled record of what was actually PAID is unreadable by any console.

⛔ **This is the wrong direction of trust, and it is the same class of bug WO-1158 already fixed in
the purchase rail itself** — the server-issued quote exists precisely because the client's number is
not authoritative. The admin view never got the memo. A refund question, a double-grant dispute, or
"did that $1.99 actually land" cannot be answered from the client's word.

## 3. ⭐ FIRST SLICE — make the transaction log readable (do this before public sales)

Smallest change, highest value, and it is *additive to a proven read-only contract*:

1. Add `purchase_quotes` + `purchase_entitlements` to the `db.js` table list (row counts + latest).
2. Add a **`purchases` view to `stats.js` sourced from `purchase_entitlements`, not analytics** —
   settled count, SKR total, USD anchor total, per-SKU breakdown, and the **quote→settle funnel**
   (quotes issued vs. paid vs. expired), which is the single best health signal the rail has.
3. Keep the analytics view; **relabel it** as client-reported intent (`bundle_viewed` →
   `purchase_completed` is a real funnel). ⛔ Do NOT delete it and do NOT merge the two numbers into
   one figure — they answer different questions and a blended number would hide the disagreement,
   which is exactly the signal worth watching.
4. **A row where the two disagree is the alert.** Client says completed, server has no entitlement =
   a grant that may have been given without settlement. Surface it; do not reconcile it silently.

⛔ **READ-ONLY IS THE CONTRACT, NOT A PHASE.** `db.js`/`stats.js` are read-only *by construction*.
Do not add a write path to either for refunds/grants — that is a separate, separately-audited
surface if it is ever wanted.

## 4. What the four pillars need, beyond the first slice

**Transaction log** — §3, plus a per-player purchase history and the `reconcile` result surfaced.

**Troubleshoot** — the pieces are all here and disconnected: `bug_reports` (player-submitted),
`analytics_events`, F8 `break-log.jsonl` + `logs/f8-inbox/QUEUE.jsonl` (owner-captured, **local**).
⚠ The F8 queue is a LOCAL file on one machine and reaches no console. Deciding whether it should is
part of scoping — it is currently the highest-signal troubleshooting stream we have.

**Tickets** — ⚠ **there are two different things called "tickets"** and they must not be conflated:
`BOARD.html` is DEV work (derived from `WorkOrders/*.md`, and the repo is deliberately the source of
truth — see the Linear→Notion→derived history). A player-issue queue from `bug_reports` is a
DIFFERENT board. ⛔ Do not fold player issues into `BOARD.html`: it is generated, so anything written
there is overwritten on the next run.

**Promos** — `promo_codes` + `promo_redemptions` + `/promo/redeem` all exist; there is no way to
CREATE a code except by hand. An authoring surface is the gap. ⚠ Promo creation is a **write** path
that grants value — it does not belong in the read-only viewer, and it needs its own audit trail
(who created it, cap, expiry, redemptions).

## 5. Open questions for the owner

1. **Where does this live?** Extend `tools/db-viewer/index.html`, or a new admin route on the Vercel
   app? (Auth already exists: `ADMIN_DASH_KEY`.)
2. **Does the F8 stream leave this machine?** It is the best troubleshooting data we have and it is
   currently local-only.
3. **Promo authoring: console or CLI script?** A script is auditable in git; a console is usable from
   a phone.
4. **"Push promos"** — is that *authoring a code*, or *pushing a notification to players*? The second
   is a different system (no push infrastructure exists today) and a much bigger piece.

## 6. Acceptance for §3

- [ ] Both purchase tables appear in `db.js` with counts + latest timestamp
- [ ] `stats.js` `purchases` view reads `purchase_entitlements`; figures reconcile against chain for
      a known settled purchase
- [ ] Client-reported vs server-settled shown SEPARATELY, with disagreements surfaced
- [ ] Every added statement is a SELECT with a hard LIMIT — the read-only contract holds
- [ ] No secrets, wallet addresses or tx signatures logged beyond what the tables already store

## 2026-08-24 - TROUBLESHOOT pillar (§4): code side landed, consolidation ASSESSED not built

Landed in `05f14790b`: `api/bug-report.js` instrumented (six swallowed failures named, response
contract unchanged), `site/admin.html` gains a **read-only** `Player reports` tab over the existing
`db.js view=bugreports`. No write path, no new SQL. See PROD-017 for why the table itself is still
not proven.

### ⛔ Why the five-shape INSERT cascade is KEPT rather than deleted

It **has never once done its job.** The deployed `id TEXT NOT NULL` raises **`23502`**, which is not
in the retry list - so the cascade rethrew on attempt 1 and never fired. And retrying could not have
helped: **no shape writes `id`**, so narrowing the column list cannot satisfy a column we never name.
It guards *"columns were removed"*; the real failure was *"a required column exists that we do not
write."*

Kept anyway because there is **no migration runner in this repo** - a migration is a human running a
file - and `schema-shape.js` records **five** file-vs-database drifts on 2026-08-24 alone.
⭐ **Deletion trigger, written down so it is not a judgement call later:** when `SCHEMA_PARITY_OK`
**blocks a deploy**, delete shapes 2-5 and keep shape 1 plus the instrumentation.

⚠ Its degradation was **silent and lossy**: shapes 3-5 fold `route`/`app_version`/`playerId` into the
`description` TEXT and shape 5 drops `RETURNING`, **and the endpoint still answers 200**. Those rows
render in the new view with empty Route/Version and no id. Now a `console.error`.

### §4 consolidation - what a single triage view needs, and what is genuinely out of reach

Four streams, **no shared join key and no common time base**:

| stream | key | identity | clock |
|---|---|---|---|
| `bug_reports` | `report_id` | `player_id` = **salted hash** | `created_at` TIMESTAMPTZ (was epoch ms) |
| `analytics_events` | - | `player_id` = **raw wallet** + `session_id` | TIMESTAMPTZ |
| authrejects | correlation `ref` | (analytics rows: `api_auth_reject`) | TIMESTAMPTZ |
| F8 `QUEUE.jsonl` | monotonic `seq` | **none** | its own |

Minimum work: one correlation key (⚠ `sessionId` is the only candidate, and the bug-report one is a
client-local `"br-..."` id with **no evidence** it matches analytics'), one time axis, and a
kind/severity discriminator so player-submitted, auto-captured and auth-refusal rows stay visibly
different classes instead of blending. It is a **view over existing tables** - no new storage.

**Unreachable today:**

1. ⛔ **The F8 stream, which is the highest-signal one.** `logs/f8-inbox/QUEUE.jsonl` and
   `break-log.jsonl` are files on **one machine**. Confirmed: **no ingest endpoint exists anywhere in
   `api/`**. Making it reachable is a new write endpoint plus a shipper - and captures carry full
   trace tails and screenshots, so it is a **data-egress decision, not plumbing**. ⭐ **Owner's to
   rule (§5 Q2). Untouched.**
2. **Every pre-migration report.** Zero rows; the client's local fallback sits on each tester's
   device. Gone, not merely unqueried.
3. **Any future cascade-shape-3-to-5 rows** - `route`/`app_version` inside a TEXT blob are greppable
   but not filterable, so a consolidated view would silently under-report them.
4. ⚠ **Correlating a bug report with that player's purchases or auth rejects.**
   `bug_reports.player_id` is a **salted hash**; money and auth key on the **raw wallet**. They cannot
   be joined without storing the wallet on the report or building a mapping table. So *"this player
   also has an unfulfilled purchase"* is **not possible today**, and making it possible is a
   **privacy call, not an engineering one**. ⭐ **Owner's to rule.**
