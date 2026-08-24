# WORK ORDER 1173 — SCHEMA_PARITY_OK: the deployed database drifted four times in one day, and the fourth took real money

**Status:** READY. ⛔ **BLOCKS `MAINNET_SALES_ENABLED=true`.** The rail is proven; this is what makes it safe for someone other than the owner.

**Minted:** 2026-08-24 (CLI), banner bumped 1173 → 1174 in the same edit.
**Provenance:** four production failures on 2026-08-24, all the same shape, found one at a time by
whatever happened to touch them.

---

## 1. What actually happened, in order

| # | Drift | Found by | Cost |
|---|---|---|---|
| 1 | `dungeon_status` table **missing** | `/api/dungeon-status` 500 in the runtime log | noise |
| 2 | `auth_sessions` table **missing** (WO-1157) | the owner's signed handshake returned 500 | **every wallet save had never been written** |
| 3 | `purchase_quotes` table **missing** (WO-1158) | a `SELECT to_regclass` sweep | **the quote rail could never have run** |
| 4 | `purchase_entitlements` **at an old version** | ⛔ **a real 391 SKR payment settled and could not be recorded** | real money, unrecorded |

⛔ **Drift 4 is the one that matters.** The table existed, so every "does it exist" check passed. It
was missing four WO-1158 columns *and* its `network` CHECK predated mainnet:

```
deployed : CHECK (network IN ('devnet','mainnet'))
declared : CHECK (network IN ('devnet','mainnet','mainnet-beta'))
```

The quote was right, the rate was right, the decimals were right, the transfer was right, the money
moved — and the final `INSERT` was rejected by a constraint written before mainnet existed.

⚠ **AND IT FAILS AT THE WORST POSSIBLE MOMENT.** `/verify` runs **after** the transfer settles. Every
schema fault on that path is discovered with the money already gone and no refund route on an SPL
transfer. There is no ordering fix for this — the chain settles first, always — so the schema has to
be right *before* the first transaction, which means a gate, not vigilance.

## 2. Why every existing gate missed it

`COMPILE_GATE_OK`, `REGRESSION_OK`, `R2_PARITY_OK`, `CATALOG_FALLBACK_GEN_OK` were **all green all
day.** They validate the *artifact*. Not one of them looks at the *database the artifact talks to*.

⭐ **This is precisely the §16 bundle-parity shape, one layer down.** There, a build installs and runs
perfectly with capsule enemies because bundles were never pushed. Here, a build installs and runs
perfectly and takes money because a column was never added. Same lesson: **the thing you shipped and
the thing it depends on are two artifacts, and only one of them was checked.**

⚠ And the failures were **individually invisible**. A missing table 500s only on the one route that
touches it; a narrow CHECK rejects only the one value that exceeds it. Nothing sweeps, so each was
found by a human tripping over it — four times, on the day it mattered most.

## 3. The gate

**`tools/schema-parity.mjs`** — connects with `DATABASE_URL`, reads `api/schema.sql` as the
declaration, compares against `information_schema` + `pg_constraint`, and emits **`SCHEMA_PARITY_OK`**
or fails with the exact diff.

It must check, in this order (each caught a real failure today):

1. **Tables present** — caught drifts 1–3.
2. **Columns present, per table** — caught drift 4a (`quote_ref`, `usd_anchor`, `usd_rate`,
   `rate_source`).
3. ⛔ **CHECK constraints match the declared set** — caught drift 4b, the expensive one. A deployed
   CHECK that is **narrower** than declared is the dangerous direction: it silently rejects valid
   rows. Compare the value sets, not the constraint text (Postgres rewrites `IN (...)` to
   `= ANY (ARRAY[...])`, so string equality will produce false alarms and get the gate ignored).
4. Indexes and NOT NULL — report, do not fail; they cost performance, not correctness.

**Judged by the MARKER on a fresh log, never the exit code** (§8 — this repo's runners exit 0 on
refusals).

## 4. Where it runs

- ⛔ **A pre-ship gate**, beside `R2_PARITY_OK`. **BLOCKING for anything that reaches a device or a
  store.**
- After every `vercel --prod` that changes `api/`.
- ⚠ **And after any `api/schema.sql` edit** — that file changing is exactly the moment the deployed DB
  becomes stale, and today proves nobody notices on their own.

## 5. Also owed, same root cause

- **A migration path.** `psql "$DATABASE_URL" -f api/schema.sql` is idempotent for CREATEs but does
  **nothing** for an existing table that needs a column or a widened CHECK — which is why drift 4
  survived every prior apply. `ALTER TABLE ... ADD COLUMN IF NOT EXISTS` and constraint replacement
  need to live in a tracked, ordered migration, not in an ad-hoc SQL file pasted into a console
  during an incident (which is how it was fixed today).
- **`api/admin/db.js` should probe every table `schema.sql` declares**, generated from that file
  rather than hand-listed. The three money tables were absent from the probe list for the same
  reason a table can be absent from the DB: someone maintained a list by hand.

## 6. Acceptance

- [ ] `SCHEMA_PARITY_OK` on a fresh log against production
- [ ] Deliberately narrow a CHECK / drop a column in a scratch DB and watch it go **red** — a gate
      nobody has seen fail is not a gate (WO-1170's rule, applied here)
- [ ] Wired into the pre-ship chain as BLOCKING
- [ ] Migration file covering today's four repairs, so a fresh environment reaches the same state
      without anyone pasting SQL
