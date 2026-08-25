# PROD-012 - RESULT: honest first-run connection containment

**Status:** FIXED 2026-08-25 - source acceptance complete; owner/device and store-listing operations remain.
**Code commit:** `c3b40829b`

## Landed

- A disconnected first run without a verified per-build cache receives the owner's exact connection-required copy and a persistent Retry action.
- The full-screen overlay atomically owns visibility and input while content is unavailable; converting a dismissing overlay restores alpha, interaction, and raycast blocking together.
- Retry re-enters `OfflineContentService`, and the overlay dismisses only for a proven online catalog or verified local cache.
- Radio reachability alone is not treated as content availability: catalog-check/update failure remains contained instead of exposing an unusable town.
- Both canon-string mirrors carry the exact ASCII owner string. No offline asset floor was invented.

## Fresh evidence

- `COMPILE_GATE_OK`
- `OFFLINE_PULL_OK 8/8`
- The focused oracle behaviorally exercises the connection-barrier state and pins the honest source-resolution path.

## Residual - owner/OPS, not source work

- On Seeker, verify disconnected first run stays contained, Retry is tappable, and reconnect dismisses only after content becomes usable.
- Update the dApp Store listing to declare that first-run setup requires an internet connection.

Do not mark owner-closed until the device reconnect flow and listing claim are confirmed.
