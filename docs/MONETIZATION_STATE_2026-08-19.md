# Monetization — where we actually sit, 2026-08-19

**Audience:** the owner (PO), and the next seat that is asked "can we ship a paid build."
**Method:** every claim in §1 and §2 was **read at source by the CLI seat on 2026-08-19**. Claims sourced
from an audit agent and *not* independently re-verified are marked **[AGENT, UNVERIFIED]** — that agent
self-reported fabricating citations in an earlier pass, so its unverified output is treated as a lead, not
a fact. Anything neither verified nor agent-sourced is marked **UNPROVEN**.

---

## 0. THE BOTTOM LINE

**No. A monetized build cannot ship today, and the reason is not polish — the payment rail terminates in a
documented dead end.** Four independent things each independently prevent money from moving, and all four
were verified at source.

The state is *safe*, not *leaky*: nothing can take a player's money and fail to deliver, because nothing
can take a player's money at all. `PackStore.Purchase` refuses while `FeatureFlags.RealmStorePurchase` is
OFF (the release default), so the failing path is not even reached. **That is the correct posture for
today; it is not a shippable revenue model.**

---

## 1. THE FOUR PAYMENT BLOCKERS — all verified at source

| # | blocker | proof |
|---|---|---|
| **B1** | **SKR has no mint address, on either network.** | `Assets/_Modules/Wallet/WalletEndpoints.cs:54` `public const string SkrMintDevnet = "";` and `:56` `SkrMintMainnet = ""`. The comment at `:53`: *"Left empty so an SKR transfer fails loudly instead of sending wrongly."* `:55` marks mainnet **"owner-gated; not the agent's to provision."** |
| **B2** | **The default rail is devnet.** | `Assets/_Modules/Wallet/WalletService.cs:225` — `public const WalletNetwork DefaultNetwork = WalletNetwork.Devnet;` |
| **B3** | **`SendPayment` can never succeed.** | `Assets/_Modules/Wallet/SolanaWalletProvider.cs:597-604`, verbatim: *"Web3.Wallet is only populated by a Web3.Login\* call … and Connect no longer makes one - it authorizes through TargetedLocalAssociationScenario instead. So this is ALWAYS null today and SendPayment always returns the failure below."* |
| **B4** | **No server-side purchase record exists.** | `api/schema.sql` — 14 tables, **none** for purchases, entitlements or receipts. Even a successful transfer would have nothing to grant against, and nothing to reconcile a dispute with. |

**B3 names its own exit.** The same comment block says the later payments WO must route signing through
the targeted scenario (`client.SignTransactions`) the way `SignMessageBase58` already does — **and
explicitly not** by reviving the Web3 login, which drags back the implicit-intent wallet election and the
SDK's dequeue-after-close bug. That is the design already decided; it just is not built.

**B1 and B4 are the owner's, not ours.** Provisioning a mint is a business act. Designing an entitlement
table is cheap; deciding what an entitlement *is* (consumable? account-bound? wallet-bound? refundable?)
is a product decision that has never been made on the record.

---

## 2. WHAT IS BUILT AND WORKING

Verified by the CLI seat today:

- **Wallet-as-identity works and is separate from wallet-as-payment.** Connect authorizes through
  `TargetedLocalAssociationScenario`; `SignMessageBase58` is live. Login and cloud-save keyed to
  `BoundWallet` are a different concern from money moving, and only the latter is blocked.
- **The store listing config is real and filled in.** `publishing/config.yaml` carries
  `privacy_policy_url` (`:65`) and `license_url` (`:52`), both on `echoes-of-elarion.vercel.app`, and
  both pages return 200.
- **The APK content pipeline is now provably correct** (fixed today — see §4).

---

## 3. STORE-SUBMISSION BLOCKERS, severity ordered

**S1 — `targetSdkVersion` was `AndroidApiLevelAuto`. FIXED TODAY.**
`Assets/Editor/AndroidBuild.cs:151` resolved the shipped target API to *whatever SDK the build machine had
installed*, so the declared target was a property of the builder, not the project — two machines building
the same commit could submit two different compatibility claims. Now pinned to `AndroidApiLevel36`, which
is what Auto has been resolving to here (the 08-18 APK reports `targetSdk=36` under `dumpsys package`), so
the binary is unchanged and merely reproducible.

**S1 — the content-parity gate was wired into ONE of four ship paths. FIXED TODAY** for two of them; see §4.

**S2 — PROD-012, internet-required on first run.** The CDN migration deleted `Assets/Resources/Structures`
and `Assets/Resources/Enemies`, so the Addressables-first chain has no second tier: a disconnected first
run has no buildings and no enemy models. Bundles cache, so it is a **first-run-per-build** requirement,
not per-launch. **Q1 of that ticket is not a decision — it is a factual disclosure the listing currently
omits**, and it is a listing edit with no code. Q2 (an honest no-connection screen with retry) and Q3
(whether a minimal offline floor is wanted) are genuine owner calls and should not hold Q1 hostage.
**[AGENT, UNVERIFIED]** on the framing; the underlying deletion is recorded in the numbering banner.

**S3 — `api/trace.js` was an unauthenticated, uncapped, `Allow-Origin: *` write into production Neon.
FIXED TODAY** — see §4.

**S3 — no in-app link to the privacy policy or terms. FIXED TODAY** — see §4.

**S3 — four PROD status lines are stale**, so the derived board advertises shipped work as pending.
Being corrected in a parallel lane today. **[AGENT, UNVERIFIED]** pending that lane's report.

**S4 — `publishing/media/` holds only a README; the real store assets live in `Builds/StoreAssets/`, which
is gitignored.** One disk failure from gone. **[AGENT, UNVERIFIED]** — worth an owner decision on whether
store art should be tracked (it is small, unlike the art packs the gitignore exists for).

---

## 4. WHAT CHANGED TODAY (CLI seat, gate-green, uncommitted at time of writing)

1. **`Assets/Editor/AndroidBuild.cs`** — `targetSdkVersion` pinned to 36, with the reasoning in place so
   nobody returns it to Auto.
2. **`overnight-apk-build.ps1`** — rewritten to (a) pass `-BuildTarget Android` so the Addressables
   content build cannot land in the wrong platform folder, and (b) push and then **verify parity**,
   writing `R2_PARITY_OK` / `R2_PARITY_FAILED` into the status file. It previously had neither.
3. **`morning-ship-chain.ps1`** — its parity call was `--verify-catalog` with **no target folder**, which
   now fails outright (`FAIL: cannot pick a build target`) because `ServerData` holds both `Android` and
   `StandaloneWindows64`. **The chain would have Died at the parity step today**, for a reason unrelated
   to parity. Now passes `ServerData/Android` explicitly.
4. **`api/trace.js`** — ingest caps (500 lines / 2000 chars per line / 256 KB total), with truncation
   **recorded into the stored row** (`truncated`, `rawLines`, `droppedLines`, `droppedChars`, `lineCuts`)
   and a `console.warn`, so a short trace in the DB can always be distinguished from a short trace on the
   client. **Deliberately NOT authenticated:** the shipped WebGL clients carry no key, so adding auth
   would silently kill web tracing for every build already in the wild — the exposure is bounded by size
   instead of identity. Verified with `node --check` and exercised against a 50k-line flood, a single
   500 KB line, and a normal 150-line batch (which passes through untouched).
5. **`Assets/_Modules/Settings/SettingsController.cs`** — a **Legal** section with Privacy Policy and
   Terms of Service buttons opening the listing URLs via `Application.OpenURL`, guarded so a dead button
   is a logged line rather than silence. It also fixes a latent layout bug: `Caption` advances y by 54
   while a button row occupies 120, so the Help row and the Developer section overlapped in dev builds.

**None of this is deployed.** `api/` changes are code-only; deploying is the owner's call and stays
preview-only per standing rule.

---

## 5. WHAT IS STILL GENUINELY UNANSWERED

- **"If a purchase happened, could we see it?"** The analytics/revenue-instrumentation lane never
  reported. `EventTracker` and the Neon `analytics_events` table exist, and `api/schema.sql:233` shows
  `purchase_completed` named as an **event name in a comment** — but an analytics event is not a receipt.
  **UNPROVEN** whether any end-to-end purchase-visibility path exists.
- **The FTUE funnel.** Captured F8 evidence from today shows four consecutive tutorial steps timing out at
  120 s each behind a modal that never clears, with combat opening on a frozen hero. **No monetization
  surface is reachable in that state.** Whatever the payment rail eventually does, a paying first session
  currently cannot get past the founding sequence. This is arguably the real revenue blocker and it is
  not a payments problem.
- **Whether we are already listed.** Canon asserts a live dApp Store presence, but **no artifact in the
  repo records the publish** — no App NFT address, release id, or tx signature — and
  `docs/SOLANA_STORE_READINESS_2026-08-06.md:22` still lists the NFT mint as the next action.
  **Confirm with the owner whether the next submission is an update or a first listing** before minting
  anything; `publishing/config.yaml:119-128` warns that minting a release NFT against the wrong binary is
  expensive to undo. **[AGENT, UNVERIFIED]**

---

## 6. OWNER DECISIONS THIS IS WAITING ON

1. **Provision the SKR mint** (devnet to test, mainnet to earn). Nothing about payments can be verified
   end-to-end until a mint resolves. **B1.**
2. **Rule on what an entitlement is** — consumable vs account-bound vs wallet-bound, and whether it
   survives a wallet change. That ruling is the schema. **B4.**
3. **Send the Unity/LevelPlay pre-approval email.** One action, and it gates the entire declared
   ad-revenue path — the cheapest real unblock on this page. **[AGENT, UNVERIFIED]** as to its being the
   only remaining gate on ads.
4. **PROD-012 Q2/Q3** — the no-connection screen, and whether a minimal offline floor is wanted.
   (Q1 is a listing edit; it needs no ruling.)
5. **Update or first listing?** See §5.
6. **Should `Builds/StoreAssets/` be tracked?**

---

## 7. THE HONEST SUMMARY IN ONE LINE

**The store path is close — the money path is not.** Everything blocking *submission* is either fixed
today or a short listing edit. Everything blocking *revenue* needs decisions only the owner can make,
and none of it is code we are waiting on.
