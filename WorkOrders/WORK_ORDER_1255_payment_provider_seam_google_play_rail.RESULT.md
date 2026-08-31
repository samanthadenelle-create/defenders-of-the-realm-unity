# WO-1255 Result — Google Play payment-provider seam

## Verdict

Implemented and locally release-candidate gated; awaiting Play Console and licensed-test acceptance. The prior architecture and dirty-artifact blockers described below are retained as historical implementation evidence, but were superseded on 2026-08-30 by the clean AAB, Play identity/storefront/grant composition, and 332/332 integrated regression result recorded in `docs/releases/GOOGLE_PLAY_RC_2026-08-30.md`. No claim of Play approval is made: no artifact has been submitted from this workspace and production credentials remain external.

## Implemented dormant foundation

- `8eb1c758d` — additive Google purchase ledger migration and server-side verify/fulfill endpoints.
- `7377d510f` — Play AAB packaging gate and deep artifact scanner; contaminated artifacts are refused.
- `9bbfb65d7` — receipt parsing and token/order/product matching, verify-before-grant settlement, retry-safe state handling, explicit consumable/durable product types, and HMAC-pseudonymous account binding.
- `4c9ccf002` — applied the additive dormant ledger migration to Neon.

The grant path is intentionally not composed into live bootstrap. There is no safe wallet-free Play account/session issuer yet and no durable `IGooglePlayGrantApplier` that atomically records purchase-token settlement with the local pack mutation. A failed or incomplete rail cannot fall through to SKR, Pi, or an unverified local grant.

## Test evidence

- Focused server/payment tests: 61/61 green during implementation.
- Client provider/settlement suite: 23/23 green.
- Unity compile gate: green.
- Full integrated data regression after this wave: `REGRESSION_OK 318/318 suites`.
- Neon migration: `GOOGLE_PLAY_LEDGER_MIGRATION_OK columns=16 indexes=4 rows=0 rail=disabled`.
- Schema parity: `SCHEMA_PARITY_OK 21 table(s)`.
- Packaging regression: `PLAY_PACKAGING_REGRESSION_OK`.

## Hard blockers proven by Gate 0

The actual Play AAB build is refused with named contamination/dependency blockers:

1. `DeNelle.Wallet` has no `!GOOGLE_PLAY` assembly boundary.
2. `DeNelle.Web3` has no `!GOOGLE_PLAY` assembly boundary.
3. Village code directly references Wallet types, so removing Wallet currently breaks the player/store surface.
4. The Solana Mobile Wallet Adapter Android plugin is included unconditionally.

The owner-required Play storefront design is not approved, and the external Play Console app/service-account configuration is not present. Those conditions prevent honest end-to-end receipt verification and store review testing.

## Safe next slice

Approve the fiat-only store design, split rail-neutral store/grant contracts out of Wallet, add explicit artifact-level assembly/plugin exclusion, then produce and scan a Play AAB before installing Unity IAP or enabling any server flag. Only after a clean artifact exists should credentials be added to Vercel and a licensed test purchase exercise verify → durable grant → acknowledge/consume → restore/refund.
