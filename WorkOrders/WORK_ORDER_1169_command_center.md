# WORK ORDER 1169 — The Command Center: the parts are built, there is no console — and the MONEY is the part nobody can see

**Status:** SPEC — consolidated 2026-08-29. The original console and money-observability gaps are delivered by WO-1244, WO-1243, WO-1269, and PROD-017. The remaining umbrella work is the richer identified support/promo/money drill-down program tracked by WO-1267. F8 egress remains explicitly deferred by owner ruling. Do not duplicate delivered descendants.

## Consolidated implementation truth — 2026-08-29

| Original requirement | Current truth | Authority |
|---|---|---|
| Server-authoritative transaction log and quote→settle funnel | **Delivered.** `api/admin/stats.js?view=purchases` reads `purchase_entitlements` and `purchase_quotes`; client and server figures remain separate. | WO-1169 §3 / WO-1244 |
| Surface client-completed/server-missing disagreement | **Delivered.** Active mismatches are shown; reviewed false positives can be acknowledged without changing telemetry or money tables. | WO-1269 (FIXED, live-smoked) |
| Phone-first Command Center surface | **Delivered in code and deployed.** Read and write endpoints remain separate; writes require the second admin key and are audited. Owner phone felt-test remains the closure step for WO-1244. | WO-1244 (FIXED) |
| Emergency operator gates | **Delivered.** Six maintenance areas are surfaced with explicit word states and audited seal/open actions. | WO-1243 / WO-1244 |
| Player-report channel and report list | **Delivered foundation.** The schema repair was owner-proven from Seeker and the ops view lists bounded, masked reports. | PROD-017 (CLOSED) / WO-1244 |
| Promo-code authoring | **Delivered.** Authoring and activation controls use the separately gated write endpoint; notifications remain out of scope. | WO-1244 |
| Reviewed purchase-alert removal | **Delivered.** Append-only acknowledgement suppresses only the matching active alert. | WO-1269 (FIXED) |
| Identified gate/promo/player/money drill-downs | **Partial and still open.** Gate count → matching bounded refusal rows is present in the current implementation; richer promo health, wallet-bound support relationships, pagination, and scoped operator-session access remain WO-1267 work. | WO-1267 (SPEC) |
| Upload local F8 captures to the console | **Not authorized.** Owner ruled "not yet"; captures remain local. | Owner ruling below |

This umbrella stays **SPEC**, not Fixed: WO-1267 still contains material undelivered acceptance and its wallet-bound operator-auth design requires product/security validation. Delivered descendant work must be tested and closed in those tickets rather than reimplemented here.

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
| Transaction log | `purchase_quotes`, `purchase_entitlements`, `/purchases/{quote,verify,fulfill,reconcile}` | Command Center Money view (`stats?view=purchases`) |
| Troubleshoot | `bug_reports`, `analytics_events` (87k+ rows), `/bug-report`, `/trace`, F8 `break-log.jsonl` | `admin/db.js` (raw rows), `tools/db-viewer/index.html` |
| Tickets | `WorkOrders/*.md` → `BOARD.html` (`tools/board_build.py`) | BOARD.html — **dev tickets only, not player issues** |
| Promos | `promo_codes`, `promo_redemptions`, `/promo/redeem` | Command Center authoring/status surface (WO-1244) |

Admin plumbing is real and sound: `api/admin/db.js` (raw-table viewer, **read-only by construction**
— every statement a SELECT with a hard LIMIT), `api/admin/stats.js` (aggregates), `admin/cleanup.js`,
gated by `ADMIN_DASH_KEY`.

## 2. Historical finding (resolved): the money tables were invisible to both admin surfaces

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

- [x] Both purchase tables appear in `db.js` with counts + latest timestamp
- [x] `stats.js` `purchases` view reads `purchase_entitlements`; figures reconcile against chain for
      a known settled purchase
- [x] Client-reported vs server-settled shown SEPARATELY, with disagreements surfaced
- [x] Every added statement is a SELECT with a hard LIMIT — the read-only contract holds
- [x] No secrets, wallet addresses or tx signatures logged beyond what the tables already store

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

### ⭐ OWNER RULING 2026-08-24 - §5 Q2 (F8 egress): **NOT YET.**
F8 captures stay local. Revisit **after `bug_reports` has accepted one real row** - the
player-submitted channel has never worked, and adding a second richer stream before the first one
functions would be building on an unproven base. The captures are not lost meanwhile.
### ⭐ OWNER RULING 2026-08-24 - **the wallet GOES ON the report.** IMPLEMENTED.

`bug_reports` gains a `wallet TEXT` column, so *"this reporter also has an unfulfilled purchase"*
becomes answerable for the first time.

⛔ **ONE INVARIANT: the column holds a SERVER-VERIFIED wallet or NOTHING.** `api/bug-report.js`
resolves it by calling `verifySession()` on the `x-session` bearer - the same rail
`b43fbce69` put under the grant-bearing endpoints. **A client-asserted wallet is never written
there.** A column that sometimes holds a proof and sometimes a claim cannot be joined against
`purchase_entitlements` safely: you would never know which rows are evidence, and an ops view saying
"this player also has an unfulfilled purchase" would be repeating whatever the client typed.

⚠ **An unverified report is still STORED, `wallet` NULL - deliberately.** The player whose auth is
broken is precisely the player most likely to file a bug. Gating the sink on the signed rail would
silently drop the highest-value reports we have, which is the opposite of why the endpoint exists.
A failed session **downgrades identity; it never refuses the report.** The admin view renders those
as **"unverified"** rather than blank, because a burst of them means auth is broken - itself the
triage signal that would otherwise go unreported.

⭐ **Folded into the PENDING rebuild, not added as a second migration.** The table was already being
rebuilt (PROD-017) and that file has **not been run yet**, so the column costs nothing there - whereas
a follow-up `ALTER` would be a second file for a human to remember, and *the entire reason PROD-017
exists is that the 2026-08-02 reconcile was authored, committed and never run.* Adding a second
forgettable file would be repeating the exact failure this ticket documents.

⭐ **The five-shape INSERT cascade now has a real job for the first time.** A deploy can reach
production before a human runs the SQL, so a new `no_wallet` shape degrades ONE step (keeping the
hash) instead of falling to `no_player_id` and losing that too. The wallet folds into
`context.verifiedWallet`, and `api/admin/db.js` reads
`COALESCE(wallet, context->>'verifiedWallet')` - so reports filed during the migration gap are still
correlatable rather than silently reading as unverified.

**Still to build:** the actual joined triage view (report -> that wallet's purchases / auth rejects).
The column makes it *possible*; nothing consumes it yet.

---

## ⭐ OWNER RULING 2026-08-24 — batch 2, ruling 7 — §5 Q4: **"push promos" means promo-code AUTHORING ONLY.**

⛔ **Owner, verbatim:** *"Do not let 'push promo' casually smuggle an entire notification platform into
an admin-console work order."*

- ✅ **In scope for this ticket:** authoring a promo code — a table and an admin form (and per the
  already-taken Q3 lead decision, a script, because it is auditable in git).
- ⛔ **Out of scope, entirely:** pushing a notification to players. No push service, no device tokens,
  no consent surface, no send-time policy exists anywhere in this repo. **If notifications are ever
  wanted, they are a SEPARATE TICKET**, and the code one ships first.

⚠ **And if that separate ticket is ever written:** `FOUNDATIONAL_RULINGS.md` §3's hard fence — ⛔ *a
notification may never be paired with a shield offer* — has to be **extended to cover promos in the
same change**, or the marketing surface walks straight through the door the fence was built to close.

⭐ **This closes the §5–§7 scoping question**, which was the only thing keeping that section open.
