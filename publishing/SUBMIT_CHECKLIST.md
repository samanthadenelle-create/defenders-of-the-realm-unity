# Solana dApp Store — submit checklist (Echoes of Elarion)

**Written 2026-08-08. Every claim below was read at source this session; provenance is cited inline.**

---

## 🔴 READ THIS FIRST — `docs/PUBLISHING_STEPS.md` IS OUT OF DATE

`docs/PUBLISHING_STEPS.md` Rail 1, steps 1–6, describes this flow:

```
dapp-store init            →  scaffolds config.yaml
dapp-store create publisher -k <keypair>
dapp-store create app       -k <keypair>
dapp-store create release   -k <keypair>
dapp-store publish submit   -k <keypair> --requestor-is-authorized
```

**None of those commands exist any more, and the ones that did cannot be run.**
Verified this session by installing the packages and reading their source:

1. **`@solana-mobile/dapp-store-cli@1.0.0` (current) has exactly two entry
   points.** Its `--help`, run locally:
   ```
   Usage: dapp-store [options] [command]
   Portal-backed CLI for Solana Mobile dApp version publishing
     dapp-store --apk-file ./app.apk --whats-new "Bug fixes"
     dapp-store resume --release-id <id> [--session-id <id>]
   ```
   No `init`. No `create`. No `validate`. No `publish`. Its help also states:
   *"The target app must already exist in the portal and already have its App NFT."*

2. **`0.15.0` was the last config.yaml version.** `0.16.0` onward is the
   portal-backed CLI (confirmed by installing 0.13.1 / 0.15.0 / 0.16.1 / 1.0.0
   and diffing their `src/` trees).

3. **The legacy CLI is self-disabling.** In `0.15.0`, every command *except*
   `init` calls `checkForSelfUpdate()` (`CliSetup.ts:109, 156, 208, 272, 367,
   442, 505`), and that function *throws* when a newer major/minor exists on npm
   (`CliUtils.ts` — `"Please update to the latest version of the dApp Store CLI
   before proceeding."`). npm's latest is `1.0.0`. So
   `npx @solana-mobile/dapp-store-cli@0.15.0 create app` **hard-fails today**.
   This is why `validate` was not run — it is gated the same way, *and* it
   requires `-k <keypair>`.

**The real flow now:** create the publisher + app **in the web portal at
<https://publish.solanamobile.com>**, connecting a browser wallet (Phantom /
Solflare / Backpack); then push each version's APK with the one-line CLI.
Source: <https://docs.solanamobile.com/dapp-store/submit-new-app> and
<https://docs.solanamobile.com/dapp-store/publishing-cli>.

👉 **`publishing/config.yaml` is still worth having**: it is the single, verified,
provenance-annotated home for every listing string and URL, so the portal form is
copy-paste instead of retyped-from-memory. It also stays directly usable if the
config flow returns. It is **not** a file the current CLI reads.

**Action for the owner:** `docs/PUBLISHING_STEPS.md` Rail 1 should be updated or
banner-flagged `STALE` per `CLAUDE.md` §15. Not done here — this agent was scoped
to scaffolding, and rewriting canon is the owner's/CLI's call.

---

## Legend

| Mark | Meaning |
|---|---|
| ✅ **DONE** | Already true in the repo — nothing to do |
| 🟡 **HERS** | Only the owner can do this (keypair, SOL, screenshots, accounts) |
| 🔵 **CLI/agent** | Can be done by an agent on request |
| 💰 | Costs real money |

---

## ⚠️ KEYPAIR SAFETY — read before you create anything

> **The keypair file IS your publisher identity.**
> Losing it means you can never ship an update under the same app listing —
> you would have to publish a brand-new listing and abandon every install.
> Leaking it means someone else can publish as DeNelle Studios.

**Rules:**

1. **Never commit it.** `.gitignore` already covers `*.keypair.json`,
   `**/*-keypair.json`, `*.keystore`, `keystore.properties` (lines 80, 229, 231,
   309). **Store the keypair OUTSIDE `D:\eoa` anyway** — outside the repo it
   cannot be caught by a stray `git add -A`.
2. **This repo has already had one near-miss.** A live Arweave wallet private key
   (`key.json`, RSA JWK, generated at the repo root 2026-08-07) sat one
   `git add -A` away from GitHub and had to be defensively gitignored
   (`.gitignore:478-481`). Do not repeat it with a Solana key.
3. **Back it up offline** — encrypted USB / password manager. Two copies.
4. **Fund it, don't reuse it.** Use a keypair dedicated to publishing; do not
   point the CLI at your main wallet.
5. Before *every* commit near a key: `git status` and read the list. Never
   `git add -A` (also `CLAUDE.md` §11).

---

## Already done ✅

- ✅ App package id: `com.denellestudios.echoesofelarion` — `Assets/Editor/AndroidBuild.cs:46`
- ✅ Release **signing** configured (stable signature, updates land in place) — `AndroidBuild.cs:175-212`, keystore read from gitignored `keystore.properties`
- ✅ Monotonic `versionCode` / `versionName` per build — `AndroidBuild.cs:158-173`
- ✅ Privacy Policy live — <https://echoes-of-elarion.vercel.app/privacy> (probed 2026-08-08 → 200)
- ✅ Terms of Use live — <https://echoes-of-elarion.vercel.app/terms> (probed 2026-08-08 → 200)
- ✅ Website live — <https://echoes-of-elarion.vercel.app/> (probed → 200)
- ✅ Listing metadata drafted + provenance-annotated — `publishing/config.yaml`
- ✅ Media spec written from the CLI's own validation source — `publishing/media/README.md`
- ✅ No ad SDK in the build (3-way verified — see Declarations below)
- ✅ ARM64 / IL2CPP / minSdk 26 — Seeker-correct — `AndroidBuild.cs:111-119`

---

## Step 0 — 🟡 HERS: unblock the two open questions

- [ ] **`copyright_url`.** `config.yaml` has it as `TODO_OWNER_...`. `/copyright`
      and `/license` both return **404**; only `/privacy` and `/terms` are live.
      Either point it at `/terms` (which carries the licence grant,
      `docs/TERMS_OF_USE.md:53`) or publish a `/copyright` page first.
- [ ] **`short_description` length.** The canon subtitle *"Echoes of a Forgotten
      Civilization"* is 34 chars. The JSON schema allows 50; the legacy CLI
      capped it at 30. The portal's limit is UNVERIFIED. Decide at the form.
- [ ] **`new_in_version`.** Required, release-specific, currently `TODO_OWNER_...`.
- [ ] **Reviewer login.** `testing_instructions` has a `TODO_OWNER` asking whether
      a reviewer needs a test account or whether guest play suffices.

## Step 1 — 🔴 BLOCKER: the purchase flag, then rebuild the APK

- [ ] **Confirm `FeatureFlags.RealmStorePurchase` now defaults OFF.**
      As read this session, `Assets/_Modules/Core/FeatureFlags.cs:594` still says
      `=> Get("realmstorepurchase", defaultOn: true)` — i.e. **ON**. Another agent
      is flipping it. **If it did not land, the "no in-app purchases" declaration
      you make in the portal is FALSE.**
- [ ] **Rebuild the APK after the flag change** — 🔵 CLI/agent:
      `Defenders/Build/Android APK (Seeker)`, or headless
      `-buildTarget Android -executeMethod DeNelle.Editor.AndroidBuild.BuildSeekerApk`.
      > ⚠ The APK at `Builds\Android\DefendersOfTheRealm.apk` right now
      > (572,202,298 bytes, 2026-08-08 14:41) is the **TESTER** build, built
      > *before* the flag change. Publishing it mints an on-chain release NFT
      > pointing at that exact binary. Undoing that is expensive and slow.
      > **Rebuild first. Then felt-verify on the Seeker.**
      >
      > (Also per memory `desktop-build-after-android-target`: after an Android
      > build the active build target stays Android — pass `-buildTarget Win64`
      > for the next desktop build.)
- [ ] 🟡 **HERS: felt-verify the rebuilt APK on the Seeker** — install and confirm
      no Buy button anywhere, no ads, game plays. Headless cannot judge this
      (`CLAUDE.md` §13: PO felt-verifies).

## Step 2 — 🟡 HERS: capture the media

- [ ] 4+ screenshots, icon, banner → drop into `publishing/media/` using the exact
      filenames in `publishing/media/README.md`. Full specs and the `adb` capture
      command are in that file.

## Step 3 — 🟡 HERS 💰: wallet + SOL

- [ ] Create/choose a **dedicated publisher wallet** (Phantom, Solflare or
      Backpack — the portal connects via browser extension).
- [ ] Export/keep a **keypair JSON file** for the CLI's `--keypair` (the CLI signs
      the on-chain version record with it). Same wallet as the portal.
- [ ] **Fund it.** The docs state *"sufficient SOL (~0.2 SOL)"* for transaction
      fees and upload costs
      (<https://docs.solanamobile.com/dapp-store/submit-new-app>).
      ⚠ **That figure almost certainly does not cover a ~546 MiB APK** on
      permanent storage. Budget extra; the exact per-GB rate is **UNVERIFIED**.
- [ ] **Back the keypair up offline, outside `D:\eoa`.** See the safety box above.

## Step 4 — 🟡 HERS 💰: publisher + app in the web portal (ONE-TIME)

At <https://publish.solanamobile.com>:

- [ ] Sign up for a **Publisher Account**
- [ ] **Connect** the publisher wallet
- [ ] **Set a storage provider** (docs recommend **ArDrive**) 💰 — this is what
      pays for the APK + media upload
- [ ] **"Add a dApp" → "New dApp"** → fill the form. **Paste every value from
      `publishing/config.yaml`** — publisher name/website/email, app name,
      package id, the three URLs, descriptions, and the media files.
- [ ] Answer the **declarations** — prepared answers in the next section.
- [ ] Approve the wallet transaction that mints the **publisher + app NFTs**. 💰
      *(One-time. Do not repeat per release.)*

## Step 5 — 🟡 HERS 💰: push the release (PER VERSION)

Once the app exists in the portal:

- [ ] Get the portal **API key** and put it in the environment:
      ```powershell
      $env:DAPP_STORE_API_KEY = "<your portal api key>"
      ```
      (Default env var name is `DAPP_STORE_API_KEY`; `--api-key-stdin` reads it
      from stdin instead. Never paste it into a committed file.)
- [ ] Publish the version:
      ```powershell
      cd D:\eoa
      npx @solana-mobile/dapp-store-cli `
        --apk-file "D:\eoa\Builds\Android\DefendersOfTheRealm.apk" `
        --keypair  "<PATH OUTSIDE D:\eoa>\publisher-keypair.json" `
        --whats-new "<the new_in_version text>" `
        --verbose
      ```
      💰 Costs SOL: the on-chain release record + the storage upload. Per release.
      The portal matches the APK's package name to your app and decides whether
      this is the first release or an update.
- [ ] If it dies part-way, **resume rather than re-run** (re-running risks a
      duplicate paid upload):
      ```powershell
      npx @solana-mobile/dapp-store-cli resume --release-id <id> [--session-id <id>]
      ```

## Step 6 — 🟡 HERS: submit for review

- [ ] Submit from the portal. Review checks that it runs, is safe, and is
      policy-compliant — lighter than Apple/Google.
- [ ] Approved → listed in the on-device dApp Store on Seeker/Saga.

---

## The Android build-tools path

```
C:\Program Files\Unity\Hub\Editor\6000.4.8f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\build-tools\36.0.0
```
✅ **Verified to exist this session.** The legacy CLI used it for
`aapt2 dump badging` (to read package/versionCode/minSdk/permissions/locales out
of the APK — `PublishDetails.ts:322`). **The current CLI takes no
`--build-tools-path` flag** — the portal extracts that itself. Keep the path
handy for local APK inspection:

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.4.8f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\build-tools\36.0.0\aapt2.exe" dump badging "D:\eoa\Builds\Android\DefendersOfTheRealm.apk"
```

Useful **before** publishing, to confirm the binary you are about to mint really
is the rebuilt one (check `versionCode` / `versionName` moved).

`adb` lives one folder up, under `SDK\platform-tools\adb.exe`.

---

## Declarations — prepared answers for the portal form

> ⚠ **`config.yaml` has no field for any of these.** Verified by grepping the
> full source of `dapp-store-cli` 0.15.0 **and** 1.0.0 **and**
> `dapp-store-publishing-tools` 1.0.0 for
> `ads|advertis|iap|in_app_purchase|age_rating|content_rating`: **zero matches.**
> These are answered in the **web portal form**, not in a file. Nothing was
> invented to fill a field that does not exist.

### Advertising → **NO / "contains ads: No"**
Verified three independent ways:
- `Packages/manifest.json` contains **no ad SDK** (no ironsource / admob /
  levelplay / unity-ads / applovin package).
- `NullAdService` (`Assets/_Modules/Core/Ads/IAdService.cs:172`) is the shipping
  default implementation.
- `docs/TERMS_OF_USE.md:236` — *"The Game does not currently show advertisements,
  and no advertising network is contacted."*

### In-app purchases → **NO**
- The real-money purchase rail is feature-gated OFF for this submission.
- Two hard blocks survive the flag regardless (both read at source):
  `SolanaWalletProvider.SendPayment` hard-blocks `WalletNetwork.Mainnet`
  (`FeatureFlags.cs:588`), and `WalletEndpoints.SkrMintDevnet` is `""`
  (`FeatureFlags.cs:590`) so the default SKR currency cannot resolve a mint.
- 🔴 **BUT SEE STEP 1.** At read time `FeatureFlags.cs:594` still defaulted the
  flag **ON**. Confirm the flip landed *and* that the submitted APK was rebuilt
  after it, or this declaration is false.

### Crypto / tokens → **no minting, no airdrop, no trading, no secondary market, no cash-out**
Value flows one way only; items and currency are non-transferable
(`docs/TERMS_OF_USE.md:115`). A connected wallet is identity + cloud-save message
signing only — it moves no funds (`WalletService.cs:362-369`).

### Age rating → **13+**
- `docs/PRIVACY_POLICY.md:117-124` — not directed to children under **13** (COPPA
  threshold); where local law sets higher (EU/UK digital-consent age 13–16), the
  higher age applies.
- `docs/TERMS_OF_USE.md:39-42` — *"not directed to children under 13, and you may
  not use it if you are under 13."*
- ⚠ The portal's rating **vocabulary is UNVERIFIED** — the published docs do not
  list the options. **Pick the lowest option that is ≥ 13** (e.g. "Teen" on an
  ESRB-style scale). If the only choices straddle 13 (e.g. 12+ / 16+), choose the
  **higher** one — never declare an age floor below 13.

### Privacy policy → **required, and live**
<https://echoes-of-elarion.vercel.app/privacy> (200). Mandatory under the
Publisher Policy.

---

## Cost summary

| Item | Cost | Frequency |
|---|---|---|
| Publisher account | free | one-time |
| Publisher NFT + App NFT (portal) | SOL — part of the *"~0.2 SOL"* the docs quote for fees + upload | **one-time** |
| Storage provider (ArDrive/Turbo) upload of APK + media | scales with size; **UNVERIFIED per-GB rate**, and the APK is ~546 MiB — expect this to dominate | per release |
| Release record on-chain (`dapp-store --apk-file`) | SOL transaction fee (small) | **per release** |
| Review submission | free | per release |
| **Total quoted by the docs** | *"sufficient SOL (~0.2 SOL)"* — treat as a **floor**, not a budget | — |

> The ~0.2 SOL figure is quoted from
> <https://docs.solanamobile.com/dapp-store/submit-new-app>. It is the only
> official number published. Whether it covers a half-gigabyte permanent-storage
> upload is **UNVERIFIED** — fund with headroom and check the portal's quote
> before approving.

---

## Rail 2 — Google Play

Untouched and unstarted. See `docs/PUBLISHING_STEPS.md` Rail 2: $25 console
account, identity verification, and (for a personal account) a **12-tester /
14-continuous-day closed test** before production access, plus an `.aab` instead
of an `.apk`. Budget 2+ weeks. Not a blocker for the dApp Store rail.
