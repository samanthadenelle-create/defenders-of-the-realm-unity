# Solana dApp Store update checklist — Echoes of Elarion

**Authority date:** 2026-08-22

**Release posture:** update to the existing live dApp Store app

**Scope:** optional Unity LevelPlay rewarded ads and wallet-approved SKR packs

This is the authoritative operational checklist. `publishing/config.yaml` is the
portal copy source; `publishing/SUBMISSION_READY_2026-08-22.md` holds the expanded
evidence record and reviewer copy.

## Stop rules

- Do **not** create a new publisher, app, Android package, App NFT, or signing key.
- Do **not** enter the new-app/KYC flow. Use the existing publisher account and
  select the existing live app, then **New Version**.
- Do **not** submit an APK unless its Android signing certificate matches the live
  release and both `versionName` and `versionCode` have increased.
- Do **not** publish stale “no ads,” “no advertising SDK,” “wallet identity only,”
  or “no purchases” claims. This update contains LevelPlay rewarded ads and SKR
  purchases.
- Do **not** expose Devnet mint details, test wallets, portal API keys, signer
  keypairs, keystores, passwords, or private keys in listing copy or commits.
- Do **not** submit a local-test, Development, LevelPlay Test Suite, or Devnet APK.
- Do **not** flip the compile-time wallet network to Mainnet or enable the production
  purchase flag without the explicit owner rulings recorded in
  `publishing/SUBMISSION_READY_2026-08-22.md`.
- Do **not** use an APK merely because it exists at the configured path. Bind the
  submission to its recorded hash, version, signing certificate, commit, and device
  evidence.

## Preserved live identity — complete

- [x] Existing App NFT preserved:
      `5MG4atMRDSVn9t75oFz1KVxKdUkyz2wPi2MeunT8yFe6`.
- [x] Existing package preserved: `com.denellestudios.echoesofelarion`.
- [x] Existing publisher preserved: DeNelle Studios.
- [x] Publisher website preserved: https://echoes-of-elarion.vercel.app/
- [x] Support contact preserved: support.EoA@icloud.com
- [x] Existing live release observed on-device: `2026.08.17.328845`.
- [x] This release is documented as an update, not a first submission.

## Legal and disclosure preparation — complete

- [x] Privacy Policy updated for optional rewarded ads, LevelPlay mediation,
      advertising/device data, consent timing, and mediated partners.
- [x] Terms updated for optional rewarded ads and wallet-approved SKR purchases.
- [x] Privacy deployed 2026-08-22:
      https://echoes-of-elarion.vercel.app/privacy — HTTP 200 verified.
- [x] Terms deployed 2026-08-22:
      https://echoes-of-elarion.vercel.app/terms — HTTP 200 verified.
- [x] Portal declarations prepared: **Contains Ads = Yes** and
      **In-App/On-chain Purchases = Yes**.
- [x] Public copy states rewarded ads are optional and user initiated.
- [x] Public copy states SKR pack contents are non-transferable, have no cash-out,
      and are not required to complete the game.
- [x] Reviewer instructions support guest play without a wallet, ad, or purchase.
- [x] Copyright/licence URL resolved to the live Terms URL.

## Monetization infrastructure — complete

- [x] Devnet test SKR mint created with 9 decimals:
      `3BwWSAUZmyngXDSZiCawEnP7iLgY5ANNopBDz94AB77N`.
- [x] Correct Seeker test wallet funded with 100 valueless Devnet SKR:
      `CHKKFkPGz8VZfjpsZjJTqfAUW7vMpdNkkqCVuCcZsfkC`.
- [x] Purchase API routes `/verify`, `/reconcile`, and `/fulfill` are production-deployed
      with the Devnet SKR configuration used for today's canary, and respond with
      controlled validation errors rather than 404/500. Mainnet configuration remains
      a separate final-release gate.
- [x] Backend SKR verifier tests passed: 10/10.
- [x] Backend verifies finalized chain data, signer, server-owned recipient, exact
      mint, decimals, and exact `amountBaseUnits`.
- [x] Mainnet production authority recorded internally: official SKR mint
      `SKRbvo6Gf7GondiT3BbTfuRDPqLWei4j2Qy2NPGZhW3`, decimals `6` (CORRECTED 2026-08-22 - read off the chain; the 9 came from OUR Devnet TEST mint and is wrong for the real token. 1 SKR = 1_000_000 base units).
- [x] Daily Chest physical-device happy path previously proved a completed ad
      granted the displayed 1,000 Gold exactly once.
- [x] Current monetization integration regression passed 264/264 with zero skipped
      (`Builds/reg-f7.log`), and compile gate passed (`Builds/gate-f7.log`) at commit
      `7678cde626538000d5cc940375ce0f04f16ded83`.
- [x] Canary APK version `2026.08.22.336813` / code `336813` exceeded the observed
      live version. This proves the integration build, not the clean submission APK.
- [x] Canary hosted-content run reported `R2_PUSH_OK` and `R2_PARITY_OK 42 object(s)`.
      The clean submission build still needs its own push/parity proof because bundle
      names are content hashed.

The Devnet mint and wallet above are internal test evidence, not store-listing copy.
Devnet infrastructure readiness does not discharge the final APK/device matrix.

## Gate A — final build identity and provenance

- [x] CLI regression is green. Record log: `Builds/regression.log` - `REGRESSION_OK 358/358 suites, 0 red, 0 skipped` (2026-09-03 19:23).
- [x] Final production APK built from commit: `32c9630f5` (branch feat/synty-art-retheme, pushed).
- [x] Final APK path: `Builds/Android/DefendersOfTheRealm.apk` (459.3 MB, built 2026-09-03 19:31).
- [x] Final APK SHA-256: `8bd67ff3108d349d81e0bddfa07f425fa3bd010924d1b3a28bcbf8cc22950f1b`.
- [x] Final `versionName`: `2026.09.04.354266`.
- [x] Final `versionCode`: `354266`.
- [x] Both version values exceed the live release values (live observed `2026.08.17.328845` / `328845`; this build `2026.09.04.354266` / `354266`).
- [x] `apksigner verify --print-certs` passes. Signer #1 DN `CN=DeNelle Studios, OU=Games, O=DeNelle Studios, L=NA, ST=NA, C=US`. (JAVA_HOME must be set to the Unity OpenJDK at `<UnityEditor>/Data/PlaybackEngines/AndroidPlayer/OpenJDK` - apksigner fails without it.)
- [x] Signing certificate SHA-256: `733666ce4ce2c872ab6530eb28d6dbf1e19de26d88ed59d1b5c0209c3da62443`.
- [ ] ⛔ Certificate matches the existing live dApp Store release - **CANNOT BE PROVEN FROM THIS REPO, and that is a gap in the record, not a pass.** The live release's certificate SHA-256 was never captured (this file still reads `PENDING` for it at the evidence table), so there is nothing to compare against. What IS true: this APK is signed by `dotr-release.keystore`, the keystore configured in `ProjectSettings.asset` (`androidUseCustomKeystore: 1`, alias `dotr`), which is the key this project has always used. The one cheap way to CLOSE it rather than assume it: install this APK over the LIVE store build on a device - Android refuses an update signed by a different key, so a successful in-place update IS the proof. Record the live value here once observed so this is never PENDING again.
- [x] `aapt2 dump badging` confirms the preserved package ID and declared version: `package: name='com.denellestudios.echoesofelarion' versionCode='354266' versionName='2026.09.04.354266'`, `application-label:'Echoes of Elarion'`, `minSdkVersion:'26'`, `targetSdkVersion:'36'`.
- [ ] APK is ARM64/IL2CPP and uses the intended production Android configuration.
- [ ] No local-test defines, Development Build, Devnet endpoints/mint, mock provider,
      or LevelPlay Test Suite activation is present.
- [ ] Production client and backend both use the official Mainnet SKR mint and
      9 decimals; neither can fall through to Devnet.

## Gate B — final APK rewarded-ad device matrix

Run these against the exact hash recorded in Gate A.

- [ ] Consent choice is resolved before LevelPlay initialization.
- [ ] Daily Chest completion grants the displayed reward exactly once.
- [ ] Daily Chest dismiss grants nothing.
- [ ] Daily Chest no-fill/load/display failure grants nothing and does not block play.
- [ ] Harvest completion grants the displayed boost exactly once.
- [ ] Harvest dismiss grants nothing.
- [ ] Harvest no-fill/load/display failure grants nothing and does not block play.
- [ ] Rapid taps cannot open duplicate presentations or duplicate rewards.
- [ ] Background/foreground during an ad cannot duplicate a reward.
- [ ] Restart after a completed ad does not repeat its reward.
- [ ] Enabled placement IDs match the production LevelPlay dashboard.
- [ ] No banner or forced interstitial appears anywhere in ordinary play.
- [ ] Attach device log/screenshot/video evidence: `________________`.

## Gate C — final APK SKR purchase device matrix

Complete a bounded canary before the production flip, then repeat the critical
recipient/mint/amount assertions on the production-configured build.

- [ ] `hearth-spark` shows the ruled SKR price and contents.
- [ ] Confirmation UI shows pack, token, exact amount, network, and recipient.
- [ ] Wallet approval produces the expected SPL `transferChecked` transaction.
- [ ] Backend verifies the transaction and creates exactly one entitlement.
- [ ] Client grants the contents exactly once.
- [ ] `/fulfill` advances the durable record to `fulfilled`.
- [ ] Restart/reconcile restores the entitlement without another grant.
- [ ] Replaying the signature cannot duplicate the entitlement.
- [ ] Wallet cancellation grants nothing.
- [ ] Insufficient balance grants nothing.
- [ ] Timeout/network loss grants nothing and leaves a recoverable pending state.
- [ ] Wrong mint, amount, signer, recipient, or network is rejected.
- [ ] Production-configured build identifies official Mainnet SKR mint
      `SKRbvo6Gf7GondiT3BbTfuRDPqLWei4j2Qy2NPGZhW3` with 6 decimals (CORRECTED 2026-08-22 - the 9 was our Devnet test mint's value).
- [ ] Attach transaction, database row, device log, and restart evidence:
      `________________`.

## Gate D — listing assets and reviewer packet

- [ ] Portal field **Contains Ads** set to **Yes**.
- [ ] Portal field **In-App Purchases/Transactions** set to **Yes**.
- [ ] Privacy URL set to https://echoes-of-elarion.vercel.app/privacy.
- [ ] Terms/licence URL set to https://echoes-of-elarion.vercel.app/terms.
- [ ] What’s New finalized from the tested build:

      > Expanded dungeon adventures, a redesigned inventory and skill experience,
      > optional rewarded bonuses, and wallet-approved SKR packs.

- [ ] Reviewer instructions pasted from
      `publishing/SUBMISSION_READY_2026-08-22.md`.
- [ ] Four clean landscape screenshots captured from the exact final APK:
  - [ ] Village rebuilding/defence
  - [ ] Dungeon exploration/combat
  - [ ] Optional rewarded-ad offer before playback
  - [ ] SKR pack confirmation before wallet handoff
- [ ] Screenshots contain no debug overlays, test wallets, transaction signatures,
      Devnet labels, personal information, or placeholder content.
- [ ] Confirm the existing portal icon and banner remain suitable for this update. If
      either is replaced, the replacement meets `publishing/media/README.md` requirements.
- [ ] Listing description matches the monetized build and contains no stale claims.

## Gate E — submit the update

- [ ] Sign into https://publish.solanamobile.com with the existing publisher account
      and existing publisher wallet.
- [ ] Select the existing Echoes of Elarion app.
- [ ] Choose **New Version**. Do not choose Add a dApp/New dApp.
- [ ] Upload the exact APK hash proven in Gate A.
- [ ] Confirm the portal matched package `com.denellestudios.echoesofelarion`.
- [ ] Confirm version increment and existing signing identity before approval.
- [ ] Review listing disclosures, legal URLs, What’s New, reviewer instructions,
      and media one final time.
- [ ] Submit through the portal or current portal-backed CLI using secrets outside
      the repository.
- [ ] Approve every required publisher-wallet message/transaction.
- [ ] Record the new release ID/Release NFT: `________________`.
- [ ] Record submission timestamp: `________________`.
- [ ] Record submitted APK hash/version/commit in the post-submission record.

## Gate F — review follow-up

- [ ] Confirm the update entered the review queue.
- [ ] Watch the developer email for `publishersupport@dappstore.solanamobile.com`.
- [ ] Record review result and any requested changes.
- [ ] Official guidance currently states 3–5 business days. After five business
      days without a response, use Solana Mobile Discord’s Developer role and
      `#dev-answers` support path.
- [ ] After approval, install/update from the dApp Store on Seeker and rerun the
      purchase/ad smoke tests against the store-delivered binary.

## Final release record

| Evidence | Value |
|---|---|
| Git commit | `PENDING` |
| APK versionName | `PENDING` |
| APK versionCode | `PENDING` |
| APK SHA-256 | `PENDING` |
| Signing certificate SHA-256 | `733666ce4ce2c872ab6530eb28d6dbf1e19de26d88ed59d1b5c0209c3da62443` (THIS build; the LIVE value remains unrecorded) |
| Ad device evidence | `PENDING` |
| SKR transaction signature | `PENDING` |
| Entitlement/fulfillment evidence | `PENDING` |
| New release ID/Release NFT | `PENDING` |
| Submitted at | `PENDING` |
| Review result | `PENDING` |

## Official sources

- Update an existing app:
  https://docs.solanamobile.com/dapp-store/submit-an-update
- Current portal-backed publishing CLI:
  https://docs.solanamobile.com/dapp-store/publishing-cli
- Subsequent-release version/signing requirements:
  https://docs.solanamobile.com/dapp-store/publishing_releases
- APK signing and verification:
  https://docs.solanamobile.com/dapp-store/build-and-sign-an-apk
- Publisher policy:
  https://docs.solanamobile.com/dapp-store/publisher-policy
- Support and review follow-up:
  https://docs.solanamobile.com/dapp-store/support
