# WORK ORDER 586 — Fleet save-probe isolation (kill the concurrency false-positive)

**Status: READY TO IMPLEMENT** (small, test-harness only — no shipping code)
**Silo:** Combat/AI test-infra (gate-free, DevTools only)
**Priority:** Low (cosmetic to the fleet harvest; not a product bug)

## Problem
`AutoPilotDriver.AssertSaveRoundTrip` exercises the REAL save path via the
hardcoded `SaveSchema.PlayerPrefsKey` (`dotr-save`). On Windows, PlayerPrefs is a
per-company/product registry store, so **all N fleet instances share one save
store**. Concurrent instances stomp each other's `dotr-save` value, and a run
reading back another seed's probe (e.g. seed 7005 reads probe `…-7001`) flags a
false `ROSTER/QUEST drift`. This is pure test-isolation noise — single-instance
`RegressionSuite save-integrity` is green, and the atomic-write fix (`81015e80`)
already removed the related `[Flow:Save] HMAC mismatch`.

## Fix (pick one)
1. **Per-instance store (preferred):** give each fleet instance a distinct
   PlayerPrefs namespace (e.g. set `Application.productName` suffix by seed at
   boot in the AutoPilot bootstrap, or use a custom `ISaveProvider` that prefixes
   the slot with the run seed). Keeps every instance's save fully isolated.
2. **Serialize the phase:** only the seed-0 instance runs `AssertSaveRoundTrip`
   (cheap, but loses per-seed coverage of the save path).
3. **Probe a seed-namespaced key** via a test-only `ISaveProvider` swap that maps
   `dotr-save` → `dotr-save-<seed>` for the duration of the probe, then restores.

## Acceptance
- An 8-instance fleet run (`run-autopilot-fleet.ps1 -Count 8`) shows **0**
  `AssertSaveRoundTrip` drift/HMAC failures in `harvest.sh`.
- `RegressionSuite save-integrity` stays green.
- No change to shipping save code (`GameStateService`/`SaveSchema`/`LocalSaveProvider`).

## Do NOT touch
- The atomic save envelope (`81015e80`) — it's correct and verified.
- The real `SaveSchema.PlayerPrefsKey` for the actual game.
