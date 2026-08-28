# WORK ORDER 1269 — Command Center purchase-alert acknowledgement

**Minted:** 2026-08-28 by Codex CLI from Samantha's unnumbered request; banner bumped 1269 → 1270 in the same edit.
**Lane:** Work Order, not PROD. **Seat:** Codex CLI.

**Status:** READY TO IMPLEMENT

## Problem

The money view correctly raises a client/server mismatch when `purchase_completed.txSig` has no
matching server entitlement. A reviewed legacy stub event remains in the alert forever, however,
so the operator cannot distinguish an unresolved incident from a known false positive.

The current case is `builders-cache`, masked guest `gues…9768`, one event at
2026-07-31T00:02:27.936Z. Its repeated stub signature is rejected by Solana RPC as the wrong size;
there is no payment to refund and no entitlement to issue.

## Scope

1. Add separately-keyed `purchase.alert_acknowledge` to the existing Command Center write endpoint.
2. Record an append-only `admin_ops_write` audit row containing signature, operator, timestamp,
   outcome, and a bounded reason. Never delete or edit the source telemetry.
3. Exclude only signatures carrying that acknowledgement from the active mismatch alert. Preserve
   the acknowledgement in Recent operator writes.
4. Put an `Acknowledge — no action` button on each mismatch row with an explicit confirmation.
5. Never add refund, grant, SKU issuance, or any write to `purchase_entitlements`/
   `purchase_quotes`.

## Acceptance

- The action requires both existing admin keys and refuses malformed/blank signatures.
- Acknowledgement fails closed if its durable audit insert fails.
- Refresh removes the signature from the active alert while the original event remains intact.
- The action is visible in operator history and tests pin the read/write and no-money-write rules.
- Command Center tests and board build pass before deployment.
