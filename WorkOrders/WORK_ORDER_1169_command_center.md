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
