# Solana dApp Store — Path A readiness audit (2026-08-06)

> STALE: 2026-08-09 — §5b names the listing assets at `D:\EoA\Builds\StoreAssets\`. The repo root is
> **machine-dependent** (`C:\eoa` / `D:\eoa`); read it as the repo-root-relative `Builds/StoreAssets/`.

> Audit of the LOCKED Path A checklist in `docs/SOLANA_STORE_LISTING_PLAN_2026-07-22.md` §0.1,
> re-run against the working tree 15 days later. **That plan stays the authority** — this is a
> status pass over its 8 steps, not a replacement. Path A (list a clean free game first, SKR packs
> as the first post-launch update) remains the owner's decision.
>
> `[CLI]` = my lane. `[OWNER]` = her hands, cannot be delegated.

---

## Status against the 8 locked steps

| # | Step | Lane | Status |
|---|---|---|---|
| 1 | Strip the two exploits (S1 HelpMenu 5-tap, S2 dev tools) | CLI | **DONE (verify #1 below)** |
| 2 | Apex dragon + audio licence | OWNER→CLI | **DRAGON CLOSED. AUDIO STILL OPEN** |
| 3 | Privacy policy drafted + hosted + wired | OWNER→CLI | **NOT STARTED — hard blocker** |
| 4 | SKR badge OFF + dead buy flows hidden | CLI | **Defaults correct; needs a release-build proof** |
| 5 | Final release APK + owner sideload test | CLI→OWNER | **Ongoing — 313819 on device** |
| 6 | Publisher KYC/KYB on publish.solanamobile.com | OWNER | **APPROVED 2026-08-06 — portal confirmed** |
| 7 | Mint Publisher→App→Release NFTs (~0.2 SOL mainnet) | OWNER | **UNBLOCKED — now the next action** |
| 8 | Submit via `dapp-store` CLI + listing assets | CLI+OWNER | **Assets not produced — now the long pole** |

> ### KYB 2026-08-06 — SUMSUB APPROVED, PORTAL NOT YET IN AGREEMENT
> Sumsub emailed "Your account has been approved" (Sumsub is the verification provider behind the
> Solana Mobile Publisher Portal). **However the Portal itself still shows
> `Identity Verification - In Progress`**, "Verification In Progress", Submitted At
> 2026-08-06 21:27:27, screenshotted at 21:29:50.
>
> **Do not treat step 6 as complete until the PORTAL says so.** The vendor's approval email and the
> Portal's gate are two different facts and only the second one unblocks minting. Most likely a sync
> lag of minutes; if it persists beyond ~1 hour, chase it rather than wait.
>
> **Publisher wallet observed: `CHKK...sfkC` — the SAME pubkey used for in-game devnet testing.**
> Functionally fine, but it concentrates risk: this wallet is about to own the Publisher NFT, and
> losing it means the app can never be updated again. Recommend separating the publisher identity
> from the everyday test wallet BEFORE it holds anything irreplaceable.
>
> The privacy policy that gated this is live and publicly readable at
> `https://echoes-of-elarion.vercel.app/privacy` (verified 2026-08-06 by external fetch AND by the
> owner in a browser). Website field: `https://echoes-of-elarion.vercel.app/`. Both from WO-863.
>
> **⚠ BEFORE MINTING (step 7) — two irreversible-loss checks:**
> 1. **Back up the publisher wallet seed.** Losing it means the app can NEVER be updated again.
> 2. **Back up `keystore.properties` + the keystore file, and confirm the key is dedicated to the
>    dApp Store.** A Google-Play-signed APK is rejected, and losing the key is equally terminal.
> Neither is recoverable after the fact. Do both before spending the ~0.2 SOL.

---

## What CHANGED since the plan was written

**Wallet connect now works on device.** The plan's long pole included "one on-device wallet-connect
test". As of 2026-08-06 (build 313763+) the full MWA handshake completes against the Seeker's own
wallet: `MWA association -> package=com.solanamobile.wallet`, `authorize response received`,
`Connect OK` in 5.1s. Root cause of the previous failure was `runInBackground: 0` freezing the Unity
main thread mid-handshake (fixed, commit `5ec4a983`). **This de-risks Path B, and it also satisfies
the plan's `[OWNER]` sideload/wallet verification item.**

**Dragon licence (L1) is closed** — already banner-corrected in the plan on 2026-08-04.

---

## VERIFY BEFORE SUBMITTING — concrete, checkable

### 1. Exploits are compile-stripped (step 1)
- `HelpMenu.cs` — the 5-tap dev unlock + Grant Resources are inside
  `#if DEVELOPMENT_BUILD || UNITY_EDITOR` at `:166`, `:319`, `:568`. **Verified present.**
- `Village/Dev/ResourceDevTool.cs` — carries a guard. **Verified present.**
- `Core/Dev/FlagCaptureButton.cs` — **NOT `#if`-stripped by design.** It is runtime-gated by
  `ShouldShow()`: `Application.isEditor || Debug.isDebugBuild || FeatureFlags.FlagButton` (`:84-87`).
  The tester APK is a RELEASE build, so it surfaces via the FLAG for testers deliberately.
  **ACTION: confirm `ff.flagbutton` is OFF in the store build.** FeatureFlags reads PlayerPrefs, so
  a flag flipped on in an earlier session is STICKY on that device — a clean-install check is the
  only honest proof.

### 2. Crypto surface OFF for the honest Path A build (step 4)
- `FeatureFlags.cs:555` — `RealmStorePurchase => Get("realmstorepurchase", defaultOn: IsDevBuild)`.
  Default OFF in release. **Same PlayerPrefs stickiness caveat applies.**
- The "Powered with SKR" badge is flag-gated (`FeatureFlags.cs:530-542`, `:714`, `:853`).
- **⚠ HARD ITEM: `packs.json` currently ships TEST PRICING.** All 13 packs are set to `skr: 1` with
  `_TEST_PRICING_ACTIVE: true` for the owner's solo testing. **This MUST be reverted before any
  public build.** Originals are preserved in `_TEST_PRICING_ORIGINAL_SKR` per pack.

### 3. Audio licence (step 2, L2) — the remaining legal blocker
Non-commercial audio: dragon roar CC-BY-NC plus unverified combat SFX
(`docs/SME/AUDIO_SME.md:222`). Complicated by a known quirk: `SfxClipLibrary.asset` does not exist,
so the `SfxId` path falls back to procedural audio and live SFX come from the Village-side `GameSfx`
authored-Resources-else-synth pattern — **what SHIPS is not the same as what is in the repo.**
Resolve the audit before assuming this is either clear or blocking.

### 4. Privacy policy (step 3) — hard blocker, nothing exists yet
Required by the store because we collect analytics + wallet address. No policy URL exists anywhere
in the tree (searched `Assets/_Modules`, `Assets/Resources/Data`). Needs: owner drafts → hosted at a
stable URL (the `api/` Vercel project is the natural home) → URL wired into the listing.

### 5a. LISTING COPY — owner decisions

**Short description (<= 30 chars): `Echoes of Elarion`** — owner, 2026-08-06. 17 characters, fits.

> ⚠ The owner typed "Echos of Elarion". Treated as a slip and spelled **Echoes** to match the title
> screen lockup, `canon-strings.json`, the marketing site, and the package id
> `com.denellestudios.echoesofelarion`. Flagged to her explicitly rather than silently corrected -
> a misspelled name on a live store listing needs a new release to fix. If she confirms "Echos" is
> deliberate, use it verbatim.

**Tagline RULED (owner, 2026-08-06): "They gave their souls to survive."** — the title-screen line.

> ⚠ CANON DIVERGENCE, NOT YET RESOLVED IN CODE. `canon-strings.json` still carries the tagline as
> "Echoes of a Forgotten Civilization" (which itself retired "Hold the last light" on 2026-07-24),
> and `CLAUDE.md` section 7 repeats it. The owner's ruling above was given for the LISTING.
> **In-game strings have deliberately NOT been changed** - swapping a player-facing tagline is a
> separate act from choosing store copy, and doing it silently would violate section 15 (canon
> updated in the same breath as the change).
>
> **Needs one word from the owner:** does the title-screen line become the project-wide tagline
> (update `canon-strings.json` + `CLAUDE.md` section 7), or do the two coexist as different slots -
> a story hook on the title screen, a descriptor in canon? Until she says, the listing uses her
> ruling and the game keeps its current strings.

**Long description, category (Games?), and "what's new" text: not yet written.**

### 5b. LISTING ASSETS — PRODUCED 2026-08-06, in `D:\EoA\Builds\StoreAssets\`

| Asset | File | Notes |
|---|---|---|
| Icon 512x512 | `icon_512.png` | Straight downscale of `Assets/Branding/AppIcon.png` (1024x1024) - the SAME icon installed on the device, so store and app match. Busy at small sizes; flagged to owner, not changed. |
| Banner 1200x600 | `banner_FINAL_1200x600.png` | **USE THIS ONE.** Pure downscale from a 1408x704 (exactly 2:1) source - no upscale, no crop. |
| ~~banner_A_canopy~~ | | rejected - clips the bottom of "ELARION" |
| ~~banner_B_mist~~ | | **rejected - the source art's decorative "Play Intro / Start New / Continue" buttons are in frame** |
| ~~banner_C_centered~~ | | superseded; was a 1.03x upscale of `Title_L.jpg` |
| ~~banner_D_grok~~ | | superseded by the exact-size final |

**PREVIEW FORMAT RULED (owner, 2026-08-06): native 2670x1200.** The device captures at 2670x1200
(2.22:1) rather than the store's recommended 1920x1080 (1.78:1). The binding rule is only that ALL
previews share matching dimensions, which a consistent native set satisfies. Chosen over letterboxing
so no game content is cropped. Deliver as JPG - the PNG captures are ~2.3MB against a 3MB cap.

**Preview status: 1 of 4.** Title screen captured. Still needed: hub, live wave, build mode, and/or
the Echoes screen.
> ⚠ The captured title screen has the `Wallet CHKK...sfkC` chip visible top-right - that publishes the
> owner's wallet address on the listing permanently. Re-take disconnected, or accept deliberately.

### 5. Listing assets (step 8) — original requirements
Per the plan's cited requirements:
- Icon **512x512**
- **>= 4 screenshots**, >= 1080px both dimensions, **all same orientation and equal aspect ratio**
  (we are landscape-locked at 2670x1200 — capture all shots landscape)
- Short description **<= 30 characters**
- Long description, category **Games**, "what's new" text
- Privacy policy URL (see 4), developer support email
- Banner 1200x600 / feature graphic 1200x1200 — *plan flags these as portal-reported, verify live*

### 6. Signing key (step 6/7) — check BEFORE minting
The store **rejects an app signed with a Google Play key** and takes **APK, not AAB**. Our release
signing comes from `keystore.properties` (present; keys `keystore.path/alias/storepass/keypass`).
**ACTION: confirm this keystore is dedicated to the dApp Store and is backed up.** Losing the
publisher wallet OR the keystore means the app can never be updated again.

---

## OWNER SEQUENCING RULING 2026-08-06: payment path is AFTER submit

The `Web3.Wallet`-always-null gap (`SolanaWalletProvider.cs:459-461`) means `SendPayment` can never
complete, so no real pack purchase can be tested end-to-end. **Owner ruled this waits until AFTER
store submission** ("after submit"). Correct call: Path A ships with `RealmStorePurchase` OFF, so the
payment path is not on the critical path to listing. It becomes Path B's first job.

Do NOT let this creep into the pre-submission work.

## Recommended order (dependency-correct)

1. `[OWNER]` Privacy policy text — it blocks submission and only she can write it.
2. `[OWNER]` Confirm publisher KYC/KYB status + fund ~0.2 SOL mainnet.
3. `[CLI]` Revert the 1-SKR test pricing; produce a clean-install release APK.
4. `[CLI+OWNER]` Clean-install verification that FLAG button + store purchase are OFF.
5. `[OWNER]` Resolve the audio licence question (or confirm what actually ships).
6. `[CLI]` Capture >= 4 landscape screenshots + 512px icon from that exact build.
7. `[OWNER]` Mint Publisher -> App -> Release NFTs.
8. `[CLI+OWNER]` Submit. Expect 3-5 business days review.

**Nothing here is blocked on code today except step 3.** The critical path is the privacy policy and
the owner's on-chain publisher setup, exactly as the 2026-07-22 plan predicted.
