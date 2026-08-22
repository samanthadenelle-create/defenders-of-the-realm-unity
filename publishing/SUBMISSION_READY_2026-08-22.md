# Solana Mobile dApp Store update packet

Prepared 2026-08-22 for the next **update** to the existing Echoes of Elarion listing.
This is the execution sheet for the Publisher Portal. It separates established facts
from evidence that can only be captured after the final APK passes device testing.

---

## STOP - TWO OWNER RULINGS GATE THIS SUBMISSION (added 2026-08-22, CLI)

Both are recorded here because the packet's own evidence gate contains items that
**cannot pass against HEAD**, and neither contradiction was written down. Neither is a
defect: both are deliberate owner-gated switches. But a submission attempted without
ruling on them fails review, or - worse - passes review describing a product the
reviewer cannot actually reach.

### BLOCKER 1 - the build is pinned to Devnet by a compile-time constant

`Assets/_Modules/Wallet/WalletService.cs:225`

```csharp
public const WalletNetwork DefaultNetwork = WalletNetwork.Devnet;
```

It is a `const`, and `Network` is seeded from it with a private setter. There is no
PlayerPrefs key, URL, or runtime toggle that reaches it - **the flip to Mainnet is a
one-line source edit plus a rebuild.** The code says so in its own words: *"It ships,
and stays, Devnet. Mainnet requires explicit written owner approval (Part 10)"*, and
*"the agent never sets this to Mainnet without written owner approval."*

That directive is being honoured: **the CLI has not changed it and will not.**

Consequence for this packet: the gate item *"SKR purchase: wallet displays correct
mainnet mint"* **cannot pass** while HEAD is Devnet. Requires: explicit written owner
approval to flip, then a rebuild, then that gate item re-run against Mainnet.

### BLOCKER 2 - a compliant production build has the purchase rail switched OFF

The gate item *"Production build contains no local-test scripting defines"* is correct
and should stay. But without `STORE_RAIL_LOCAL_TEST`:

```csharp
RealmStorePurchase => Get("realmstorepurchase", defaultOn: false);
```

So a clean production build ships the store rail **off**, while the listing declares
**contains in-app purchases: Yes** and the reviewer instructions describe selecting Buy
and approving an SKR transfer. **A reviewer following those instructions would find no
purchase UI at all** - which reads as a false declaration rather than a disabled feature.

Exactly one of these must change before submission, and it is an owner call:

- **(a)** flip `realmstorepurchase` to `defaultOn: true` for the store build, so the
  listing copy is true; or
- **(b)** leave it OFF and strip every purchase claim from the listing, the reviewer
  instructions and the ads/IAP declarations, submitting as a free game with rewarded
  ads only.

⛔ **Do not resolve this by shipping the local-test defines.** That would make the copy
true by disabling the gate that keeps monetization off by default, and it also carries
`MONETIZATION_LOCAL_TEST`, which is unrelated to the store rail.

### BLOCKER 3 - every listing image is missing

`publishing/media/` contains **only `README.md`**. Zero of the six required assets
exist, and the portal cannot accept a release without them:

| Asset | Spec | Status |
|---|---|---|
| `icon-512.png` | exactly 512x512 | MISSING |
| `banner-1200x600.png` | exactly 1200x600 | MISSING |
| `screenshot-01..04.png` | >= 1080px on BOTH axes; 1920x1080 landscape suits this game | MISSING (4 of 4) |

Screenshots must come from the **clean** submission build - not today's canary - and
must carry no debug overlay, Devnet label, test wallet, or private information.

### BLOCKER 4 - the short description is two different strings, and both are too long

The same field is authored in two places and they have already drifted apart:

| Source | Text | Length |
|---|---|---|
| `config.yaml` `short_description` | `Echoes of a Forgotten Civilization` | 34 |
| this file, "Portal copy" | `Rebuild Elarion. Awaken its Echoes.` | 35 |

Neither fits `dapp-store-cli` 0.15.0's 30-character ceiling; both fit the 50-character
`releaseJsonMetadata` limit; the live portal's real limit is unverified.

Two decisions, both the owner's: **which line**, and **whether to shorten it**. The
first is the canon tagline (CLAUDE.md section 7), so it must not be silently trimmed.
Once ruled, ⛔ **delete the losing copy** rather than correcting both - a value authored
in two files is the duplicated-state failure this repo keeps paying for, and it is
precisely how these two drifted.

Under 30 characters, if shortening is chosen: `Rebuild Elarion.` (16),
`Awaken the Echoes.` (18), `Echoes of Elarion` (17).

### Consequence: today's APK is NOT the submission APK

`Builds/Android/DefendersOfTheRealm.apk` (2026-08-22 16:30, 545 MB) was built with
`-Defines 'STORE_RAIL_LOCAL_TEST;MONETIZATION_LOCAL_TEST'` for the owner's Devnet
purchase canary. It is disqualified by this packet's own gate item on local-test
defines. **A separate clean build is required after testing**, and the hash/version
recorded below must come from THAT build, never this one.

---

## Release posture: existing live app, update only

Echoes of Elarion is already live in the Solana dApp Store. This release updates that
existing listing; it does **not** create a publisher, app, package identity, App NFT,
or new signing identity. Preserve the existing App NFT, Android package, publisher
wallet, and Android signing key. A key mismatch would create an uninstallable update.

The current Solana Mobile update workflow is portal-backed. Upload a release-ready APK
whose `versionName` and monotonically increasing `versionCode` both exceed the live
release and whose certificate matches the existing listing. Update the listing details
and What's New, then submit the new version through the Publisher Portal or its current
portal-backed CLI. The portal derives the app identity from the APK package name.

Official sources:

- https://docs.solanamobile.com/dapp-store/submit-an-update
- https://docs.solanamobile.com/dapp-store/publishing-cli
- https://docs.solanamobile.com/dapp-store/build-and-sign-an-apk
- https://docs.solanamobile.com/dapp-store/publishing_releases
- https://docs.solanamobile.com/dapp-store/publisher-policy

## Proven and ready

- Existing App NFT: `5MG4atMRDSVn9t75oFz1KVxKdUkyz2wPi2MeunT8yFe6`.
- Existing live release observed on-device: `2026.08.17.328845`. The final
  `versionName` and `versionCode` must exceed the corresponding live values.
- Android package: `com.denellestudios.echoesofelarion`.
- Publisher: DeNelle Studios.
- Website: https://echoes-of-elarion.vercel.app/
- Support: support.EoA@icloud.com
- Privacy URL: https://echoes-of-elarion.vercel.app/privacy
- Terms/licence URL: https://echoes-of-elarion.vercel.app/terms
- Architecture: Android ARM64, Unity IL2CPP, landscape.
- Guest play is available; a wallet and purchase are optional.
- Monetization declarations: **contains ads: Yes**; **contains in-app/on-chain
  purchases: Yes**.
- Ads are user-initiated rewarded ads through Unity LevelPlay; there are no forced
  interstitial or banner placements in the submission design.
- Purchases are explicit SKR SPL-token transfers authorized in the user's wallet.
  Purchased game content is non-transferable, has no cash-out, and is not required
  to complete the game.
- Mainnet SKR authority for production: mint
  `SKRbvo6Gf7GondiT3BbTfuRDPqLWei4j2Qy2NPGZhW3`, decimals `9`.
- Devnet-only integration authority: mint
  `3BwWSAUZmyngXDSZiCawEnP7iLgY5ANNopBDz94AB77N`, decimals `9`.
  This is an engineering record only; do not publish it or a tester wallet in the
  store listing.

## Portal copy

**Name:** Echoes of Elarion

**Short description:** Rebuild Elarion. Awaken its Echoes.

**Long description:**

> Elarion has fallen, and the Heart of the village still remembers everyone it
> once sheltered. Rebuild the town stone by stone, place your walls and towers
> where they matter, and hold the line as the waves come. Awaken the Echoes—the
> essences of the people the Heart still guards—and take them into the dungeons
> beneath the realm. Play the complete adventure free. Optional rewarded ads and
> non-transferable cosmetic and convenience packs support continued development.

**What's new (replace the bracketed evidence line after final testing):**

> Expanded dungeon adventures, a redesigned inventory and skill experience,
> optional rewarded bonuses, and wallet-approved SKR packs. [FINAL DEVICE PASS:
> replace with the tested version and date.]

**What's new - candidate rewritten for THIS release (2026-08-22, CLI).** The text
above predates the work below and describes none of it. Player-facing wording only;
no internal system names. ⚠ If BLOCKER 2 is resolved as **(b)**, delete the purchase
sentence from this text as well as from the listing:

> Two new dungeons to explore, rebuilt so every room is reachable. Purchases now
> pause the world while a transaction is in flight, so nothing can happen to your
> town while you are approving one - and a purchase that is interrupted now
> recovers on its own instead of stranding. Steadier hero previews, clearer
> inventory and skill screens, and quieter audio handling throughout.

Provenance of each claim, so the copy can be defended in review:

| Claim | Where it came from |
|---|---|
| two new dungeons, every room reachable | legacy hand-built dungeons replaced through `GraphDungeonComposer`; the owner had never once passed room 1 in five months |
| purchases pause the world | WO-1149; `WorldHold` is the single writer of `Time.timeScale`, acquired as the first statement of the charge path |
| interrupted purchase recovers | `/verify` + `/reconcile` + `/fulfill`, exactly-once entitlement, reconciliation on restart grants nothing twice |
| steadier hero previews | preview framing fix - a `TrailRenderer`'s world-space bounds were aiming the camera past the far clip plane |
| clearer inventory and skill screens | inventory/skills layout pass |
| quieter audio handling | optional-SFX misses no longer raise false errors |

**Reviewer instructions:**

> Echoes of Elarion is a landscape, single-player town-defence and dungeon RPG.
> Reviewers can select Play as Guest; no account, wallet, advertisement, or
> purchase is required for ordinary play. Rewarded ads are optional and appear
> only after the player selects a clearly labelled reward offer. A completed ad
> grants the displayed reward once; cancellation or failure grants nothing.
> Optional packs use SKR. Selecting Buy presents the exact token amount and sends
> the transaction to the user's Solana wallet for approval. The app never receives
> a seed phrase or private key. Purchased content is non-transferable and cannot
> be withdrawn, traded, or cashed out.

## Final APK evidence gate — complete after testing

**Discharged 2026-08-22 (CLI) - these carry over to the clean build unchanged:**

- [x] Regression green: `REGRESSION_OK 264/264 suites -- 264 green, 0 red, 0 skipped`,
      `Builds/reg-f7.log`, fresh. Compile gate: `COMPILE_GATE_OK`, `Builds/gate-f7.log`.
- [x] Commit SHA at time of build: `7678cde626538000d5cc940375ce0f04f16ded83`
      (branch `wip/village2-and-f8-tickets`).
- [x] Version exceeds live: built `2026.08.22.336813` / code `336813` vs live
      `2026.08.17.328845`. Both name and code increase.
- [x] Hosted content proven: `R2_PUSH_OK` + `R2_PARITY_OK 42 object(s) verified`
      for catalog `2026.08.22.336804` - presence, size and content of every remote
      object the catalog names. ⚠ Bundle names are content-hashed, so **the clean
      submission build re-hashes everything and needs its OWN push and parity run.**
      This proof does not carry over; it is listed here only to show the chain ran.

**Still required, and only obtainable from the CLEAN build (see BLOCKER section):**

- [ ] Final regression run is green; attach log path and commit SHA.
- [ ] Final APK version name/code recorded; both exceed the live release.
- [ ] Final APK SHA-256 recorded.
- [ ] `apksigner verify --print-certs` passes and the certificate matches the
      signing identity used by the existing dApp Store listing.
- [ ] `aapt2 dump badging` confirms package `com.denellestudios.echoesofelarion`,
      ARM64 compatibility, version, and declared permissions.
- [ ] Production build contains no local-test scripting defines and no LevelPlay
      Test Suite launch flag.
- [ ] Privacy/consent choice is resolved before LevelPlay initialization.
- [ ] Daily Chest: completed rewarded ad grants exactly once.
- [ ] Daily Chest: dismiss/failure/no-fill grants nothing.
- [ ] Harvest: completed rewarded ad grants the displayed boost exactly once.
- [ ] Harvest: dismiss/failure/no-fill grants nothing.
- [ ] Rapid taps and background/foreground do not duplicate an ad reward.
- [ ] SKR purchase: wallet displays correct mainnet mint, exact amount, recipient,
      and network before approval.
- [ ] SKR purchase: verifier grants one entitlement; fulfillment acknowledgement
      persists; restart reconciliation does not duplicate the grant.
- [ ] Wallet cancellation, insufficient funds, timeout, and network failure grant
      nothing and leave a recoverable state.
- [ ] Privacy and Terms pages deployed from the updated repo copies and return 200.
- [ ] Four clean 1920x1080 listing screenshots captured from this exact APK; no
      debug overlays, test wallets, Devnet labels, or private information.
- [ ] Final APK copied to the submission path only after all checks above pass.

## Submission execution

1. Sign into https://publish.solanamobile.com with the existing publisher account
   and wallet, then select the existing Echoes of Elarion app. Do not create a new
   publisher or app and do not mint another App NFT.
2. Choose **New Version**, update the listing disclosures and What's New, and use
   media from `publishing/media/`.
3. Confirm the APK uses the existing Android signing key and has increased
   `versionName` and `versionCode` values.
4. If using the CLI, use the existing publisher signer and a portal API key exposed
   only as `DAPP_STORE_API_KEY`; never commit either secret. Publish the exact proven
   APK with the current portal-backed command:

   ```text
   dapp-store --apk-file <final.apk> --keypair <publisher-keypair.json> --whats-new "<final tested release note>"
   ```

5. The update creates a new release version/Release NFT for this APK while preserving
   the existing App NFT and package identity. Approve every required wallet
   message/transaction; skipped signatures can leave release assets incomplete.
6. Record the new release ID/NFT, APK hash, submitted version, submission timestamp,
   and evidence paths here after submission.
7. Expect review feedback at the developer email. Official guidance currently says
   3–5 business days; use Solana Mobile's `#dev-answers` support path after five.

## Post-submission record

- Release version/code: `PENDING_FINAL_TEST`
- APK SHA-256: `PENDING_FINAL_TEST`
- Git commit: `PENDING_FINAL_TEST`
- Submission timestamp: `PENDING`
- Release ID/NFT: `PENDING`
- Review result: `PENDING`
