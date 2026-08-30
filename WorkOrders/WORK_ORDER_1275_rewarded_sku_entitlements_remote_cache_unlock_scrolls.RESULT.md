# WO-1275 Result — rewarded SKU entitlements and progression unlocks

## Verdict

DONE and headless-verified. Entitlements restore only from authenticated server truth, keep independent expiry buckets, use a server anchor plus monotonic elapsed time, clear remote ownership on failed refresh, and cannot be created by malformed cache data. Stone Gate and Healing Caravan Plans progression is permanent and idempotent.

## Proof

- `Builds/tests-EditMode.xml`: `RewardedProgressionTests` 11/11 passed, including Wave 7 exactly-once and the palisade-to-stone trigger.
- `Builds/tests-EditMode.xml`: `SkuEntitlementSnapshotTests` 8/8 passed, including restore, expiry, failed refresh, malformed cache refusal, and progression-source isolation.
- Focused Node tests: 37/37 passed; the entitlement endpoint is read-only, wallet-isolated, bounded, and returns safe fields only.
- Full gate: `COMPILE_GATE_OK`; `REGRESSION_OK 322/322`; EditMode 1,029/1,029; PlayMode 6/6.

