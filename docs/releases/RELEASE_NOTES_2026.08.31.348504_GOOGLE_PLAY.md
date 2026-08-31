# Release Notes — Echoes of Elarion `2026.08.31.348504` Google Play RC

**Compared with:** production Seeker build `2026.08.30.347462`

**Google Play artifact:** `Builds/Android/EchoesOfElarion-GooglePlay.aab`

**Size:** 482,839,103 bytes (460.47 MiB)

**SHA-256:** `A33E36EC9E54BAC857D9E91ECA0095B3882ADBE5A90B6520ACC822A2DE71A9E3`

## Player-facing highlights

- Added a dedicated Google Play edition with Google account sign-in and an authenticated
  cloud-save identity.
- Added a native Google Play storefront using Play-localized fiat prices.
- Added purchase restoration for reinstall and supported-device changes.
- Restored the Skills entrance so the hero talent tree is reachable from the seven-tab Manage
  screen.
- Improved the Defense Manage entrance: fresh accounts receive a useful defensive-building
  teaching state, while owned defenses retain their direct upgrade route.
- Corrected Manage screen routing and training-door behavior.
- Corrected claimed-camp visual verification so it runs after the camp visual has initialized.
- Corrected food-harvest node orientation.
- Corrected weapon orientation handling across the equipment catalogue.
- Corrected the hero death-animation shake caused by competing death transitions.
- Hardened battle cleanup so a completed encounter cannot strand the game in a slowed or locked
  state.

## Google Play billing

- Added Google Play Billing with Play-owned product details and localized pricing.
- Added server-side purchase-token verification through the Google Play Developer API; client
  callbacks alone cannot authorize a grant.
- Added exact account/product binding, global token deduplication, and idempotent fulfillment.
- Consumable products are consumed only after a durable grant; permanent products are
  acknowledged only after a durable grant.
- Added restore/reconciliation handling so eligible permanent purchases can be recovered after
  reinstall or on another supported device using the same account.
- Billing is deliberately default-off in this RC. It must not be described as live until all Play
  products and credentials are configured and the licensed-device purchase matrix passes.

## Play policy and platform separation

- Removed wallet, Mobile Wallet Adapter, Web3, Solana runtime, token-address, staking, and
  crypto-facing surfaces from the physical Play artifact.
- Kept the existing wallet/Solana behavior in the Seeker APK; the Play isolation work does not
  replace or weaken that distribution channel.
- Replaced crypto-oriented Android presentation with Play-neutral identity, currency, settings,
  login, storefront, privacy, and terms surfaces.
- Added an in-app route to account/data-deletion instructions and a public deletion URL.
- Updated Android texture overrides conservatively to create Play size margin without reducing
  source art, default-platform imports, or the protected hero texture tier.

## Verification completed for this exact AAB

- Official bundletool 1.18.3 validates the bundle.
- Local Play delivery estimate: 479,363,936–479,444,447 bytes, leaving approximately 20.56 MB
  below the published 500 MB compressed-download ceiling.
- Physical artifact scan: `PLAY_ARTIFACT_CLEAN_OK`.
- Unity compile gate: `COMPILE_GATE_OK`.
- Data regression: `REGRESSION_OK 332/332`.
- The Seeker/store-shaped APK was rebuilt after the AAB and passed schema plus all 54 remote-object
  parity checks.

## Still required before public release

- Upload this exact hash to Play Internal testing and record Play Console's authoritative
  compressed-download size, artifact diagnostics, and App Bundle Explorer result.
- Configure Play App Signing OAuth, Developer API service-account access, every canonical product,
  localized prices, and license testers.
- Run Play-delivered tests for successful, cancelled, pending, interrupted, duplicated, restored,
  refunded, voided, unavailable-product, and account-deletion cases.
- Complete the Play Console Data Safety, content-rating, ads, target-audience, financial-features,
  app-access, account-deletion, privacy, store-listing, and pre-launch-report declarations.
- Implement and verify an idempotent entitlement reversal policy before enabling billing for
  refunded consumables.

## Post-build hardening status

Authenticated deletion-request intake and secure Google Play real-time developer-notification
ingestion were implemented after the AAB hash above was produced. Their focused tests and compile
gate are green and their additive database migrations are applied, but they require a fresh AAB
before they can be claimed as part of the shipped client artifact. Billing remains disabled.
