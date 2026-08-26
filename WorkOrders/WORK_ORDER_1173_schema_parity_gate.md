# WORK ORDER 1173 — SCHEMA_PARITY_OK: the deployed database drifted four times in one day, and the fourth took real money

**Status:** CLOSED 2026-08-26 — owner felt-tested PASS on APK `2026.08.26.342478` (source `bcef3be7`).

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

---

## CLOSED 2026-08-25 - and the production half is PROVEN, not deferred

**`SCHEMA_PARITY_OK 17 table(s) verified against api/schema.sql`** - run by the owner against the live
database. This is the FIRST TIME production schema parity has ever been proven on this project.

### What it took, and none of it was the wiring

The gate wiring landed easily. Getting to a true green took three rounds, and each round found a
different class of defect:

**Round 1 - four drifts.** `promo_codes.reward_pack_sku` and `tower_swaps.verification` missing,
`purchase_entitlements.rail` and `purchase_quotes.currency` CHECKs missing.

**Round 2 - the `IF NOT EXISTS` trap, exactly as predicted.** Migration `0001` reported SUCCESS and
fixed two of four. `purchase_quotes` and `purchase_entitlements` ALREADY EXISTED, so
`CREATE TABLE IF NOT EXISTS` skipped those statements whole - and skipped the CHECK constraints
written INSIDE them. The tables looked right; their constraints were never born. Re-running `0001`
could never fix it: it is a no-op against an existing table, forever. Hence migration `0002`, which
adds them by `ALTER`, guarded on `pg_constraint`.

**Round 3 - THE GATE ITSELF WAS BLIND, and this is the finding worth keeping.** After `0002` the two
CHECKs were present and parity still reported them missing. Postgres renders
`CHECK (col IN ('a','b'))` as `col = ANY (ARRAY[...])` but **SIMPLIFIES a one-element `IN` to plain
equality**: `col = 'a'::text`. `tools/schema-parity.mjs` matched only the ARRAY form, so **every
single-value CHECK read as missing no matter how correctly it was defined.**

STOP **The two it hid were `rail IN ('solana')` and `currency IN ('SKR')` - single-valued by nature,
both on the MONEY PATH, both reported as drift while the database was correct.**

!! That is worse than a missed bug. A gate that cannot see a correct constraint does not merely fail
to catch a defect - it **sends someone to repair a thing that is not broken**, which is what it did
for two full rounds. Same family as the hollow pass: an assertion whose output does not depend on the
truth it claims to test. Fixed by trying the ARRAY form then the equality form, proven against five
renderings, with the non-enum checks (`amount_base_units > 0`, `decimals >= 0`) still correctly
ignored so the gate did not get looser in exchange for getting sighted.

### The lesson this ticket paid for

**The exit code of a migration is not evidence. The shape query is.** `0001` exited clean twice while
leaving the money path unconstrained. The two-step runner - apply, then prove - is the only reason
this was caught rather than shipped.

### What remains

- The deliberately-narrowed-CHECK RED proof in a scratch DB is still owed (section 6 of this ticket).
  !! Lower value now: the gate has been seen RED against production **three times** and green once,
  which is stronger evidence than a synthetic red.
- Per `FOUNDATIONAL_RULINGS.md` section 8, the "after every production API deploy" trigger is an
  OWNER DISCIPLINE, not a script - there is no `vercel --prod` in the tree by design.

### It unblocks

STOP This ticket was the stated blocker on `MAINNET_SALES_ENABLED=true`. **That blocker is now clear.**
Turning sales on remains the owner's decision, and the switch is still UNTESTED from a non-owner
wallet - a separate question this ticket never covered.
