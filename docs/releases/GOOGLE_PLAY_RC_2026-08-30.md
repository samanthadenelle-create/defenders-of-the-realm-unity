# Google Play release-candidate evidence — 2026-08-30

This record supersedes the *current-state conclusions* in
`docs/GOOGLE_PLAY_READINESS_AUDIT_2026-08-30.md`. That audit remains preserved as the red
baseline that drove this work. It is not evidence for the current artifact.

## Candidate artifacts

| Channel | Artifact | Bytes | SHA-256 |
|---|---|---:|---|
| Google Play | `Builds/Android/EchoesOfElarion-GooglePlay.aab` | 482,843,623 | `F353DE81A8E1C63950E9B0E0AF415AB7F7A14DF9F15C153F2F9B214DF5A29ADE` |
| Latest Seeker/store-shaped APK | `Builds/Android/DefendersOfTheRealm.apk` | 493,482,245 | `DC7891ACC2A8E75E1EB0936A0C65671B9CC058E319EBFAE22E95279B0B94FA92` |

The raw AAB size is **not** the Google Play size verdict. Play's published limit for the base
module is 500 MB compressed download size **as calculated by Play Console after upload**. The
candidate must therefore be uploaded to an internal track before size acceptance can be closed.
The prior audit's 200 MB upload-blocker conclusion is obsolete; 200 MB is the large-download
warning threshold, not the current base-module rejection limit.

Google's official `bundletool` 1.18.3 validates this AAB. A default split APK set generated from
the exact candidate reports `get-size total` MIN 479,371,759 and MAX 479,452,660 bytes. This is a
close local estimate, not the authoritative Console calculation, but it creates at least 20.5 MB
of margin even if the published 500 MB threshold is interpreted as decimal bytes. The preceding
candidate estimated 513,590,887–513,670,750 bytes; the existing conservative Android texture
pass reduced 65 eligible overrides (source/default platforms and hero fidelity preserved) before
the final rebuild.

## Fresh local gates

- `Builds/goal-play-compile18-size-margin.log`: `COMPILE_GATE_OK :: scripts compiled clean`.
- `Builds/goal-play-data-regression10-size-margin.log`: `REGRESSION_OK 332/332 suites -- 332 green, 0 red, 0 skipped`.
- `Builds/goal-play-aab9-policy-hardening.log`: optimized product build succeeded and emitted
  `[GooglePlayPackagingGate] PLAY_ARTIFACT_CLEAN_OK` after scanning the physical AAB.
- `Builds/overnight-apk-status.txt`: `SCHEMA_PARITY_OK`, fresh `APK_OK`,
  `R2_PARITY_OK 54 object(s) verified`, then `APK_DONE`.

The Play artifact gate scans readable content plus compiled/native payloads. It rejects wallet,
MWA, Solana SDK/runtime, crypto copy, live token addresses, stake URLs, and wallet brands. The
Play build uses its own immutable `GOOGLE_PLAY` stamp; persistent Android PlayerSettings no longer
carry `DAPP_STORE` or `SOLANA_SDK`. The separately built Seeker APK proves those exclusions did
not damage the normal channel; version `2026.08.31.348504` is installed on the connected Seeker.

## Implemented review surfaces

- Play-only Google identity bridge and authenticated session/save path.
- Play-only billing provider and storefront with store-localized fiat pricing.
- Server-verified purchase/grant seam, account binding, idempotency, and restore/reconcile path.
- Wallet/Web3/MWA and crypto-facing resources physically excluded from the Play artifact.
- Play-neutral login, settings, storefront, staking, currency, and canonical copy.
- Public account-deletion URL and in-app deletion entry point are present. Deployment and backend
  deletion execution must be verified in the target production environment before submission.

## Console acceptance checklist — still required

These are external acceptance steps, not satisfied by a local green build:

1. Upload the exact AAB hash above to **Internal testing** in Play Console.
2. Record Play Console's base-module compressed download size and retain the App Bundle Explorer
   screenshot/export. Reject this RC if Console reports more than 500 MB.
3. Resolve every Console artifact error/warning; record package identity, version code, target API,
   signing certificate SHA-256, supported devices, and native-code diagnostics.
4. Configure the Google OAuth client using the Play App Signing certificate SHA; configure the
   server audience/client IDs and deploy the Google identity/session backend.
5. Create every canonical Play product ID with the expected product type and localized price.
   Configure the Play service account/Developer API access and server verification secrets.
6. Add license testers. Install from Google Play—not by sideload—and execute: sign-in, catalog
   load, purchase success, cancel, pending purchase, network loss after charge, duplicate callback,
   restore/reinstall, refund/void, unavailable SKU, and account deletion.
7. Complete IARC/content rating, Data Safety, ads declaration, app access/reviewer instructions,
   privacy policy, account deletion declaration, target audience, financial-features declaration,
   store listing, screenshots, and contact details.
8. If this is a personal developer account created after 13 November 2023, complete the current
   closed-test requirement (at least 12 opted-in testers continuously for 14 days) before applying
   for production access.
9. Capture Pre-launch report results and fix or explicitly disposition every crash, ANR,
   accessibility, security, and compatibility finding.

## Current release decision

**Locally release-candidate ready; not yet Play-approved.** Artifact isolation, compile, regression,
and APK parity are green. Approval remains gated by authoritative Play upload diagnostics, licensed
billing/restore testing, production backend configuration, Console declarations, and review.

## Post-artifact policy hardening

Work completed and incorporated into the fresh candidate hash above:

- authenticated, identity-bound account/data-deletion request intake with an idempotent operations
  queue and a bounded second-tap confirmation in the Play client;
- corrected public deletion instructions for the now-live Google identity rail;
- OIDC-authenticated Google Play RTDN ingestion with strict Pub/Sub-envelope validation,
  message-id deduplication, ProductPurchaseV2 re-query, and durable quarantine for refunds,
  partial voids, unknown tokens, unsupported notifications, and pending refund review;
- additive migrations `0014` and `0015` applied to the target database, followed by
  `SCHEMA_PARITY_OK 40 table(s)`;
- focused Node coverage green at 38/38, fresh `COMPILE_GATE_OK`, and fresh full
  `REGRESSION_OK` in `Builds/goal-play-policy-regression.log`.

These changes remain default-off where activation could move value. They do not claim that a
refunded consumable has been reversed: billing activation remains gated until the entitlement
reversal policy and licensed-device refund/void matrix are implemented and verified. The fresh
`2026.08.31.348534` AAB recorded above includes the client-side deletion confirmation.

Baseline-to-candidate release notes are recorded in
`docs/releases/RELEASE_NOTES_2026.08.31.348534_GOOGLE_PLAY.md`.

## Production policy deployment — 2026-08-30

The pushed commit `37837f585` was deployed from a clean detached worktree after both previews
reached `READY` and their routes were probed. Outgoing production deployment IDs were captured in
`Builds/PROD_ROLLBACK.txt` before promotion.

- API/WebGL project `defenders-of-the-realm-v2`: production deployment
  `dpl_9VAD85zaBih91ecQxM2tYbVsW3us`, status `READY`.
- Legal-site project `echoes-of-elarion`: production deployment
  `dpl_9NdshgufPHD4YZwpMrtxpF2Aug37`, status `READY`.
- Production `/api/purchases/google-play-rtdn`: GET returns 405 and an unauthenticated POST returns
  503 while Play RTDN configuration is absent, proving the value-moving rail remains default-off.
- Production `/api/account/delete-request`: GET returns the expected quiet 400 contract; malformed
  POST returns `PLAYER_ID_MISSING`/400 without creating a request.
- Production `/delete-account` serves the current Google Play sign-in and in-app request
  instructions; the obsolete “Google sign-in is planned” claim is absent.

This closes code/site deployment, not Console configuration. Play OAuth, Publisher API, RTDN
audience/service-account identity, product catalogue, and billing enable flags remain intentionally
unset until Internal-track and licensed-device verification are ready.
