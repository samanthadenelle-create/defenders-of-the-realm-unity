# Solana dApp Store — Listing Plan (2026-07-22)

> Synthesis of a 2-agent deep dive: (A) Solana Mobile dApp Store publishing requirements (official
> `docs.solanamobile.com` + corroborating guides) and (B) a code-verified store-readiness gap audit of
> this repo. **Draft plan for owner decisions — nothing here is committed to yet.** Push HELD.
>
> **Strategic note:** this cuts against current canon (`CANON_GROUND_TRUTH_2026-07-22.md`: *V1 = Pi Browser,
> ships ZERO crypto, SKR later*). A dApp Store listing is a **new/parallel platform** and re-activates the
> crypto/wallet stack that today is **all stubbed on devnet**. The Seeker APK already builds as a genuine
> release (grounds this), so it's viable — this plan makes the cost of "Solana-store-ready" explicit.

---

## 0. THE ONE DECISION THAT SHAPES EVERYTHING

The store accepts a plain game — **MWA/wallet integration is NOT required to list**, a non-crypto game lists
fine under **Games**, and Solana Mobile takes **0% platform fee**. So there are two launch shapes:

| Path | What ships | Blockers | Time-to-list | SKR packs |
|---|---|---|---|---|
| **A — List now, honest** | Free game APK, crypto surface OFF | ship-blockers only (§2) | **fast (~1–2 wk)** | NO (added later) |
| **B — Crypto-live early access** | A + real MWA + SKR packs ($2/$5) | A + the whole wallet/SKR build (§3.C) | **weeks+** | YES at launch |

**✅ DECIDED (owner, 2026-07-22): PATH A → B.** List a legally-clean, exploit-free free game on Seeker FIRST
(Path A), then add the **$2/$5-max SKR early-access packs** (memory `solana-store-early-access-pack-pricing`)
as the **first post-launch update** (a new Release NFT) once the wallet stack is real (Path B). Path B's wallet
work cannot be faked; the store's 0% fee means nothing is lost by shipping A first.

---

## 1. HOW THE dApp STORE WORKS (mechanics, cited)

- **On-chain NFT trio (Solana mainnet-beta):** **Publisher NFT** (org identity, one-time), **App NFT** (one per
  app, one-time), **Release NFT** (one per version — new mint every update). Losing the publisher wallet =
  can never update the app again. [submit-new-app](https://docs.solanamobile.com/dapp-store/submit-new-app)
- **Publishing model is Portal-first + a thin CLI** (⚠ the old `config.yaml` `init/create/publish submit`
  walkthrough in most blog posts is **deprecated** — do not follow it). Portal = **publish.solanamobile.com**
  (create publisher → **KYC/KYB** → mint App NFT → upload signed APK → submit). CLI just uploads versions:
  `dapp-store --apk-file app.apk --keypair keypair.json --whats-new "…"` with `DAPP_STORE_API_KEY` env; it
  reads the package name from the APK. [publishing-cli](https://docs.solanamobile.com/dapp-store/publishing-cli)
- **Cost:** keep **~0.2 SOL mainnet** in the publisher wallet (fees + ArDrive decentralized storage of the APK).
- **Keystore:** must be a **fresh signing key made solely for the dApp Store** — an app signed with a **Google
  Play key is rejected**. Format is **APK** (not Play's AAB). [publishing-from-google-play](https://docs.solanamobile.com/dapp-store/publishing-from-google-play)
- **Listing assets:** icon **512×512**; **≥4 screenshots**, ≥1080px both dims, **all same orientation + equal
  aspect ratio**; short description **≤30 chars**; long description; category **Games**; "what's new" per
  release; **privacy policy URL** (required — we collect analytics + wallet address); dev support email.
  Banner **1200×600** / feature graphic **1200×1200** (portal-reported, verify live).
  [listing-page-guidelines](https://docs.solanamobile.com/dapp-publishing/listing-page-guidelines)
- **Review:** submit → queue → **~3–5 business days** (updates faster); results emailed; approved = live
  immediately. [support](https://docs.solanamobile.com/dapp-publishing/support)
- **Policy vs Play:** **zero platform fee**, **crypto/NFT/DeFi/token IAP explicitly allowed** (the thing Play
  restricts is fine here); prohibitions are the standard illegal/hate/malware/deception set.
  [publisher-policy](https://docs.solanamobile.com/dapp-store/publisher-policy)
- **Device/upside:** ships pre-installed on every **Seeker** (~150k+ preorders, 57 countries); a "quality app"
  historically qualified devs for the **SKR airdrop** (developer incentive, not a requirement).
- **Verify-before-lock (agent flags):** CLI = Portal+thin-CLI not config.yaml; **min/target SDK (API 33?)
  third-party-only — confirm before setting Unity min API**; arm64-v8a / Node 18–21 / banner+feature sizes are
  high-confidence but portal/third-party; no confirmed APK-size cap, permission allowlist, or age-rating
  questionnaire.

---

## 2. SHIP-BLOCKERS — must fix for ANY listing (Path A and B)

Legally-clean, exploit-free, honest. Each is code-cited in the readiness ledger; effort S/M/L.

| # | Sev | Fix | Where | Eff |
|---|-----|-----|-------|-----|
| **S1** | BLOCKER | **HelpMenu 5-tap resource self-grant ships in RELEASE.** 5 taps → 50k wood/iron + 25k food/crystals + 50k gold in *any* public APK. Wrap the grant button + `OnTitleTapped` + `OnGrantResources` in `#if DEVELOPMENT_BUILD \|\| UNITY_EDITOR`. | `HelpMenu.cs:149-154,223-267` | S |
| **S2** | BLOCKER | **`DevResourceTool` + `FlagButton` default ON** → unlimited-resource tooling exposed to players. Flip both defaults OFF (or `#if` gate). | `FeatureFlags.cs:285,298` | S |
| **L1** | BLOCKER | **Apex dragon model = CC BY-NC** (non-commercial) — legal hard stop for a monetized listing. License commercially (CGTrader) OR replace the wave-20 boss model. | `docs/SME/DRAGON_OPTIONS.md:7` | M |
| **L3** | BLOCKER | **No privacy policy** (store + law require one; app POSTs analytics + wallet address). Author + host a policy; declare data collection; link the URL in the listing. | (none exists) | S |
| **L2** | SHOULD | **Non-commercial audio** (dragon roar CC-BY-NC + unverified combat SFX). Re-source/license + add CC-BY credits. | `docs/SME/AUDIO_SME.md:222` | M |
| **C4** | SHOULD | **"Powered with SKR" preview badge defaults ON.** On a zero-crypto (Path A) build this is a misleading crypto claim — flip `SkrPreview` OFF. | `FeatureFlags.cs:510` | S |
| **M1** | SHOULD | **Hide dead buy-flows.** Pack "buy" UI routes to stubbed payment; a live buy button that can't transact is a review/UX risk. For Path A, disable/hide purchase-confirm rails. | `SolanaWalletProvider.cs:230-347` | M |
| **B3** | SHOULD | **Verify launcher icon** actually renders on the installed APK (build script doesn't call `SetIcons` for Android). | `AndroidBuild.cs:103-131` | S |

**Two of these (S1, S2) are one-line flips that close live in-build exploits — do them first regardless.**

**Verified-GOOD (not blockers):** Seeker APK is a genuine **release** build (`BuildOptions.None`, IL2CPP/ARM64/
minSdk26 — `AndroidBuild.cs:81`); release keystore wired (`:128-169` — ⚠ confirm it's a *fresh dApp-store key*,
not a Play key); trace-row TTL cron now exists (`api/admin/cleanup.js` — the WO-684 "no TTL" item was stale);
admin endpoint auth'd with constant-time compare.

---

## 3. THE PHASED PLAN

### Phase 0 — Owner decisions (blocks everything; see §6)
Path A vs B; dragon license-vs-replace; whether SKR packs ship at launch or as an update; SKR mint provisioning.

### Phase 1 — Ship-blocker hardening (code; §2)
S1 + S2 (one-liners, day 1) → L1 dragon → L3 privacy policy → L2 audio → C4/M1 crypto-surface-off (Path A) →
B3 icon. Gate + regression each. **This is the critical path to a clean APK and is required for both paths.**

### Phase 2 — Store setup (no code, parallelizable now)
- Publisher Portal account at **publish.solanamobile.com** + **KYC/KYB** (budget time for identity verification).
- **Publisher wallet:** a dedicated browser wallet (Phantom/Solflare/Backpack), fund **~0.2 SOL mainnet**,
  **BACK UP THE SEED** (loss = can never update the app).
- **Fresh dApp-store keystore** (distinct from any Play key). Confirm our existing `keystore.properties` key
  qualifies or make a new one; **back it up**.
- **Listing assets:** 512×512 icon, **≥4 screenshots same-orientation** (from `RunCaptureHeadless` / device
  captures), feature graphic 1200×1200 + banner 1200×600, ≤30-char short desc, long desc, category **Games**,
  privacy-policy URL (from L3), support email. Age-rating/locale fields TBD (unconfirmed — handle at submit).

### Phase 3 — Crypto activation (ONLY if SKR packs at launch — Path B)
The expensive lane; can be deferred to a post-launch update.
- Define **`SOLANA_SDK`** (`ProjectSettings.asset:763` is empty today) + install the Solana Unity SDK; wire
  **MWA / Seed Vault** login (`SolanaWalletProvider` real path). — L
- **Provision the SKR SPL mint** (`WalletEndpoints.SkrMint*` + `JupiterSwapService._skrMint` are empty). — M
- Wire the **$2 / $5-max SKR pack payment** through real `SendPayment` (SKR), map the pack ladder, cap at $5-eq. — M
- Reconcile **Jupiter network** (mainnet URLs vs devnet wallet) + real signer (`WalletBridgeStub` hard-fails in
  release today) OR gate the swap panel OFF. — M
- **Real device test on Seeker** (MWA can't be validated headless). — M
- Reconcile the 3 persistence stores + dual-wallet **spend asymmetry** before real value rides the economy. — M

### Phase 4 — Build + submit
Release APK (arm64-v8a; **verify min SDK vs API-33 flag** before locking) → sign with the dApp-store keystore →
mint App NFT in Portal → upload APK (Portal or `dapp-store` CLI) → submit → **~3–5 day review** → live.

### Phase 5 — Post-launch / updates
Updates = **new Release NFT, same publisher wallet**, faster review. **If SKR packs were deferred (Path A
launch), Phase 3 ships here as the first update** — this is the clean way to get listed fast and monetize next.

---

## 4. THE $2/$5 SKR EARLY-ACCESS MODEL — how it maps
Owner ruling: early-access packs **cheap** — **$2 and $5 tiers, $5 the max, all in SKR**. Mechanically this is
**Phase 3** (needs real SKR mint + MWA payment). Two ways to honor it:
1. **List Path A now** (free, no packs) → **add the $2/$5 SKR ladder as the first post-launch update** once
   Phase 3 lands. *(Recommended — fast, low-risk, and the store's 0% fee means no economics lost by waiting.)*
2. **Hold the listing for Path B** and launch with SKR packs live. *(Slower; carries the full wallet-build risk
   on the critical path.)*
This is the pricing WO-755's spec was waiting on; fold the $2/$5-cap-SKR ladder into WO-755 either way.

---

## 5. WORK ORDERS TO MINT (from banner, next-free 757+)
- **WO-757 — Store ship-blocker hardening** (S1/S2 `#if` + flag flips; C4/M1 crypto-surface-off). *(S/M)*
- **WO-758 — Dragon license/replace** (L1) + **non-commercial audio re-source** (L2). *(M)*
- **WO-759 — Privacy policy** authoring + hosting + listing wiring (L3). *(S)*
- **WO-760 — Store listing asset bundle** (icon/screenshots/graphics/descriptions). *(M, no code)*
- **WO-761 — Solana wallet activation** (SOLANA_SDK + MWA + SKR mint + $2/$5 SKR pack payment + Jupiter
  reconcile + Seeker device test) — the Phase-3 crypto lane. *(L)*
- Extend **WO-755** with the $2/$5-cap-SKR early-access ladder.
*(Numbers proposed; mint from the `CLI_LANES_WO_NUMBERS.md` banner, not filesystem max.)*

## 6. OWNER DECISIONS
1. ✅ **DECIDED (2026-07-22): Path A → B** — list free game now, SKR packs as first update. *(Still open below.)*
2. **Dragon:** buy commercial license or replace the model?
3. **SKR mint:** does the SKR token/mint exist to provision, or is it still to be created? (Gates all pack revenue.)
4. **Publisher identity:** DeNelle Studios org for KYC/KYB; who holds/backs-up the publisher wallet + keystore.
5. **Canon:** does this promote the Solana dApp Store to an official V1 platform (alongside/instead of Pi Browser)?
   If yes, update `KEY_FACTS`/`CANON_GROUND_TRUTH` platform lines same-breath (§15).

---
*Sources: internal readiness ledger (code-verified, file:line) + external requirements research
(docs.solanamobile.com primary, blueshift/helius corroborating). Draft — awaiting owner decisions in §6.*
