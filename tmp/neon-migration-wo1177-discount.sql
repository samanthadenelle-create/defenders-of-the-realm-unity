-- =============================================================================
-- Neon migration — WO-1177, the shortfall discount columns on purchase_quotes.
--
-- ⛔⛔ THIS IS AN **ALTER**, NEVER A REBUILD. UNLIKE bug_reports, THIS TABLE HAS
--     REAL DATA — including the first completed mainnet purchase (391 SKR,
--     chain-confirmed, expected_base_units = observed_base_units = 391000000).
--     ⛔ DO NOT DROP THIS TABLE. There is no STEP 0 "prove it is empty" here
--     because it is NOT empty and must not become so.
--
-- ⭐ Both statements are IDEMPOTENT (ADD COLUMN IF NOT EXISTS / CREATE INDEX IF
--    NOT EXISTS), so re-running is safe and changes nothing the second time.
--
-- ⚠ WHY THIS FILE EXISTS AT ALL, AND WHY IT MUST BE RUN IN THE SAME CUT AS THE
--   CODE: this repo has NO MIGRATION RUNNER — a migration is a human running a
--   file. PROD-017 exists because a reconcile authored on 2026-08-02 was
--   committed and never reached the database, and nobody noticed for 22 DAYS.
--   A second forgettable file is the failure mode, not the fix.
--
-- ⚠ AND THE MONEY PATH FAILS AT THE WORST MOMENT BY CONSTRUCTION:
--   /api/purchases/verify runs AFTER the transfer settles, so a schema fault is
--   discovered with the money already gone and no refund route on an SPL
--   transfer. The chain settles first, always. Run this BEFORE the code that
--   writes these columns is deployed.
-- =============================================================================

-- ── 1. The two columns. Both NULLABLE by design. ───────────────────────────
--
-- ⭐ NULLABLE IS THE POINT: every quote issued before this migration, and every
--    undiscounted quote after it, legitimately has no discount. A NOT NULL
--    DEFAULT 0 would make "no discount" and "a zero-basis-point discount"
--    indistinguishable in the ledger, and this column exists precisely so the
--    real discount rate is a number we can READ rather than assume.
--
-- discount_bps    — basis points off the USD anchor (2000 = 20%). Applied inside
--                   buildQuoteBody BEFORE quoteAmount, so the client never sees a
--                   pre-discount number it could edit.
-- discount_reason — the SERVER's label for why, e.g. 'repair_shortfall'.
--                   ⛔ NOT the client's `reason` hint, which is logged and never
--                   trusted. Storing the client's string here would turn an audit
--                   column into a repetition of whatever the caller typed.
ALTER TABLE purchase_quotes ADD COLUMN IF NOT EXISTS discount_bps    INT;
ALTER TABLE purchase_quotes ADD COLUMN IF NOT EXISTS discount_reason TEXT;


-- ── 2. The index the RATE LIMIT will actually use. ─────────────────────────
--
-- ⭐ The 7-day window (owner ruling 2026-08-24) is enforced by asking "has this
--    wallet been issued a discounted quote since NOW() - 7 days". Without this
--    index that question is a full scan of every quote ever issued, on the money
--    path, at purchase time.
--
-- ⚠ PARTIAL INDEX, deliberately: only discounted rows are ever the answer, and
--    the overwhelming majority of quotes will carry no discount at all.
CREATE INDEX IF NOT EXISTS idx_purchase_quotes_discount
    ON purchase_quotes (wallet, issued_at DESC)
    WHERE discount_bps IS NOT NULL;


-- ── 3. Confirm the shape. All four rows should read exists = true. ─────────
SELECT c.name AS expected_column,
       EXISTS (SELECT 1 FROM information_schema.columns
                WHERE table_name = 'purchase_quotes' AND column_name = c.name) AS exists
FROM (VALUES ('discount_bps'), ('discount_reason'), ('usd_anchor'), ('issued_at'))
     AS c(name)
ORDER BY exists, c.name;


-- ── 4. ⛔ PROVE NO DATA WAS LOST. This must NOT be zero. ────────────────────
--
-- ⚠ The opposite check from the bug_reports rebuild, and for the opposite
--   reason: there, zero rows justified dropping. Here, a zero would mean the
--   ALTER was actually a rebuild and the settled mainnet purchase is gone.
SELECT COUNT(*) AS quotes_still_present,
       COUNT(*) FILTER (WHERE consumed_at IS NOT NULL) AS settled_quotes
FROM purchase_quotes;


-- ── 5. ⚠ THEN PROVE A WRITE, because a schema match is NOT evidence. ───────
-- After the WO-1177 code deploys, issue one discounted quote and check:
--
--     SELECT quote_ref, sku, usd_anchor, discount_bps, discount_reason, issued_at
--     FROM purchase_quotes
--     WHERE discount_bps IS NOT NULL
--     ORDER BY issued_at DESC LIMIT 5;
--
-- ⛔ And confirm the rate limit REFUSES the second one inside the window. A
--    discount that can be re-summoned by re-triggering a refusal is a permanent
--    20% off with extra taps — which is the entire reason the window exists.
