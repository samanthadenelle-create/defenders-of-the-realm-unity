# WO-859 RESULT - Per-collector capacity in hours + offline collector accrual

**Status: DONE** (reconciled 2026-08-08 from the tree, NOT felt-verified).

**Shipping commit:** `5cd1ceb9`

**Decisive tree artifact:** `CollectorIncomeRegression.cs:31` case 7 `[offline-capped]`; run marker `COLLECTOR_INCOME_OK`.

**Audit trail:** `docs/reference/WO_TRUE_STATUS_2026-08-08.md`. The WO's Status line still read a
pre-implementation value until the 2026-08-08 reconciliation pass corrected it.

**Note:** Shipped under WO-901's number, which is why the board never moved.

**Outstanding:** owner felt-verification. This WO was closed by reconciling the working tree against
the board, not by the PO closing it at the time, so no one has confirmed how it FEELS in play. Per
CLAUDE.md section 13 the PO felt-verifies and closes - treat this RESULT as tree-proven only.
