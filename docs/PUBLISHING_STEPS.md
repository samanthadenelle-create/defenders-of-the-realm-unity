> # ⚠ STALE 2026-08-09 — **Rail 1 below is OBSOLETE. Do not follow its Steps 1-6.**
>
> Verified while scaffolding the submission (HEAD `c8320434`): **`dapp-store-cli@1.0.0` has NO `init`,
> `create`, `validate` or `publish` subcommands.** Its entire surface is
> `dapp-store --apk-file <apk> --whats-new <text>` — and it requires that **the app ALREADY EXISTS in the
> portal with an App NFT.**
>
> **What actually happens now:** the **publisher and the app are created in the WEB PORTAL with a browser
> wallet**, not from the CLI. `publishing/config.yaml` is kept as the **verified paste-source** for that
> web form — it is no longer CLI input. See `publishing/SUBMIT_CHECKLIST.md`.
>
> Rail 2 (Google Play) and the fee/policy framing below are unaffected. Per CLAUDE.md §15 this is a banner,
> not a rewrite — the body is preserved as the record of what was believed.

# Publishing / Getting Listed — the real steps

Two rails, very different. **Solana dApp Store (Seeker) is primary (0% fees); Google Play is
secondary reach (30% + friction).** iOS is a separate, restricted decision (Mac + Apple's
crypto rules + 30%) — see `android-seeker-distribution-and-wallet-strategy` memory.

App package id: `com.denellestudios.echoesofelarion`. Release APK already builds + signs.

---

## Rail 1 — Solana dApp Store (Seeker/Saga) — PRIMARY, 0% fees

Publishing is CLI + on-chain NFTs (publisher / app / release are NFTs you mint). Real steps:

### Prereqs
- A **release-signed APK** (have it: `Builds/Android/DefendersOfTheRealm.apk`).
- **Node.js** + the CLI: `npx @solana-mobile/dapp-store-cli` (aka `npx dapp-store`).
- A **Solana keypair file** (a wallet) funded with a **small amount of mainnet SOL** — minting
  the publisher/app/release NFTs costs a few cents of SOL + rent. This keypair IS your
  publisher identity — back it up; losing it means you can't push updates under the same app.
- **Store assets:** app icon (512), a set of **screenshots**, a short + long **description**,
  category, and any required media (feature graphic/video optional).

### Steps
1. `npx dapp-store init` → scaffolds a **`config.yaml`** (app metadata, media paths, release notes).
2. Fill `config.yaml` — name, description, icon/screenshots, version, the APK path.
3. **Publisher NFT** (one-time): `dapp-store create publisher -k <keypair> -u <rpc>` → mints your
   studio's publisher identity.
4. **App NFT** (one per app): `dapp-store create app -k <keypair> -u <rpc>`.
5. **Release NFT** (one per version): `dapp-store create release -k <keypair> -u <rpc>` → validates
   the APK, uploads APK + media, mints the release.
6. **Submit for review:** `dapp-store publish submit -k <keypair> -u <rpc> --requestor-is-authorized`
   → goes to the Solana Mobile team. Review is lighter than Apple/Google (runs, safe, policy-ok).
7. Approved → **listed** in the on-device dApp Store on Seeker/Saga. Updates = new release NFT +
   submit again.

- **Fees: 0%** on payments — the whole reason this is the primary rail.
- Crypto/wallet monetization (your SKR store) is **allowed** here — it's the native audience.

---

## Rail 2 — Google Play — SECONDARY (mass Android reach, 30% / 15%)

Real steps + the new friction that catches people:
1. **Play Console account** — **$25 one-time** (not annual). Requires **identity verification**
   (D-U-N-S for orgs, ID for individuals).
2. **NEW personal-account rule (post-2023):** you must run a **closed test with ≥12 testers for
   14 continuous days** before you can even apply for production access. Budget ~2+ weeks. (Org
   accounts are exempt.) → this is where your Firebase/closed-track testers count.
3. Build an **AAB** (App Bundle) — Play no longer accepts raw APKs. (I can flip `AndroidBuild`
   to emit an `.aab`.)
4. **Store listing:** title, short + full description, screenshots (phone + tablet), feature
   graphic, app icon, category, contact, and a **privacy policy URL** (required).
5. **Declarations:** content rating questionnaire, **Data safety** form, target-audience, ads
   declaration, financial-features (crypto) declaration.
6. **App signing:** Play manages the upload/signing key (Play App Signing) — enroll on first upload.
7. **Tracks:** internal → closed (the 12-tester test) → production. Submit → review (hours–days).
8. **Crypto:** Play is more permissive than Apple — wallets/blockchain allowed under policy —
   but digital goods sold in-app still route through Play billing (30%, or 15% under $1M/yr).

---

## What to prep once, reused by both
- **Icon** (512), **screenshots** (grab from the build on-device), **short + long description**,
  **category**, **privacy policy URL** (host a simple page).
- For the dApp Store: a **funded mainnet keypair** (publisher identity — back it up).
- For Play: an **AAB** + the declaration forms + 12-tester closed test.

## Where I can help
- Build the **AAB** for Play (small `AndroidBuild` flag).
- Scaffold + fill the dApp Store **`config.yaml`** from our app metadata.
- Assemble the **listing asset checklist** (capture screenshots on the tester build).
- I can NOT: create your accounts, hold your keypair, or pay SOL/fees — those are yours.
