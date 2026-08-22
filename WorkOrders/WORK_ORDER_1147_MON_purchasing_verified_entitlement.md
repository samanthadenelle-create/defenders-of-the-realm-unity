**Status:** READY TO IMPLEMENT - BLOCKED ON OWNER R5 FOR ACTIVATION ONLY (build may proceed)

# WO-1147 - MON - Purchasing: verified on-chain payment to durable entitlement

**Minted:** 2026-08-22 (CLI, banner bumped 1147 -> 1148 in the SAME edit)
**Lane:** **MON** - monetization, dedicated and prioritised.
**Split from:** WO-1146 (owner ruling 2026-08-22).
**Sibling:** `WORK_ORDER_1146_MON_rewarded_ads_activation.md` - Lane A, independent.

## ⭐ WHAT R5 DOES AND DOES NOT BLOCK

R5 asks: *does the public Buy button stay OFF until payment is proven end to end, or go ON as soon
as the mint is ready?* The programme's own recommendation is **(A) OFF until the checklist is green**.

**R5 IS A SHIPPING DECISION, NOT A BUILD DECISION.** Every step below can be built, tested on devnet
and gated while `RealmStorePurchase` stays `defaultOn:false`. R5 gates only the moment the flag
flips. **Do not wait on it to start.**

The asymmetry that argues for (A), recorded so it is not re-litigated: under (A) the worst case is a
player who cannot buy yet. Under (B) the worst case is **a player who paid and got nothing**, on a
live storefront, with no entitlement record to reconcile from. A delay versus a refund, a support
thread and a store dispute.

> ### ⚠ SPLIT FROM ONE TICKET, 2026-08-22 (owner: "split it")
> This was one 354-line WO carrying BOTH lanes. They share only the laws in section 1 and the
> release contract in sections 5-10; their EVIDENCE is completely different - chain/verifier and a
> backend on one side, physical device and an ad dashboard on the other - and so are their blockers.
> Held together, either lane stalling froze the other. They now run and land independently.
>
> **The shared halves are DUPLICATED into both files ON PURPOSE.** A seat must not have to open the
> sibling ticket to learn a non-negotiable law. ⛔ If a law in section 1 changes, change it in BOTH.


---

## 0. Outcome

Deliver two independently gated monetization lanes:

1. **Purchasing:** a real owner-wallet devnet payment travels through the same architecture intended for mainnet, is independently verified by the backend, creates one durable entitlement, grants exactly once, survives interruption/relaunch, and produces a usable receipt. After that proof, mainnet activation is a controlled configuration and owner-approval step—not a rewrite.
2. **Rewarded ads:** all three Android placements present a real LevelPlay ad, grant only from the earned-reward callback, obey consent/caps/cooldowns, produce dashboard and ILRD evidence, and fail safely. Ads may be enabled independently of purchasing.

**Today is not measured by flags changed. It is measured by proof gathered.** A lane that fails a gate remains honestly OFF and ships with its completed foundation intact.

---

## 1. Non-negotiable laws

### 1.1 Purchasing

- The canonical sequence is: **select SKU → wallet signs → chain confirms → server verifies → durable entitlement is recorded → client grants → save verifies → receipt is shown**.
- A client-reported signature is not payment proof. The server must query the chain and verify status, signer, recipient, currency/mint, amount, and SKU binding.
- Never grant merely because the wallet returned a signature.
- The transaction signature/payment ID is the idempotency key. One settled payment can create one entitlement and one grant, including across retries, crashes, reinstalls, and devices.
- “Paid but not granted” is a recoverable pending state, not a terminal error and not a silent log line.
- No stub wallet, simulated balance, local bypass, or `STORE_RAIL_LOCAL_TEST` build may be distributed publicly.
- Devnet must use the production-shaped verifier, entitlement store, reconciliation route, and client fulfillment path. Only network-specific configuration may change for mainnet.
- Start with **SOL**. SKR is not on today’s critical path while its mint addresses are unprovisioned. Add USDC/SKR only through the same verified contract.
- `RealmStorePurchase` remains OFF by default until the applicable activation gate is signed by the owner.

### 1.2 Rewarded ads

- Reward only from the LevelPlay earned-reward callback, exactly once.
- Show, open, click, close, timeout, load failure, and early dismissal never grant.
- No ad reward may grant crystals, premium currency, tradable value, revive, or combat continuation.
- Consent/privacy settings must be applied before SDK initialization.
- Every live surface must route through the placement catalog and `AdGateService`; no legacy path may bypass placement caps, cooldowns, or accounting.
- Test Suite/test-mode calls and flags must not exist in the production artifact.
- `RewardedAdSkip` remains OFF by default until physical-device and dashboard proof is captured.

### 1.3 Shared release discipline

- No secrets, seed phrases, private keys, RPC secrets, advertising IDs, or dashboard credentials enter source control or logs.
- Network, treasury, app key, and ad-unit configuration must be environment-specific and fail closed when absent.
- Each lane has its own commit and evidence packet. Do not combine unrelated dirty-tree files.
- A successful compile or regression is necessary but cannot substitute for physical-device, chain/backend, or dashboard evidence.

---

## 2. Known starting state — verify at HEAD before editing

The CLI must record the inspected commit SHA and mark each claim **confirmed**, **changed**, or **stale**:

### Purchasing baseline

- `RealmStorePurchase` defaults OFF publicly and may default ON only under `STORE_RAIL_LOCAL_TEST`.
- `WalletService.DefaultNetwork` is Devnet.
- `WalletService` refuses stub/non-signing providers.
- `SolanaWalletProvider.SendPayment` refuses Mainnet and currently depends on `Web3.Wallet`, which the targeted Mobile Wallet Adapter connection does not populate.
- SOL and USDC endpoint data exist; SKR devnet/mainnet mint constants are empty.
- `PurchaseGate` has a bounded PlayerPrefs retry ledger, but that is not a durable server entitlement authority.
- The current backend schema has no proven pack-purchase entitlement table/endpoint that verifies recipient and amount.
- The shelf and pack-grant seam exist; visible paid packs must still be reconciled against actual delivered contents before activation.

### Ads baseline

- `com.unity.services.levelplay` 9.5.1 is installed behind a leaf provider assembly and version define.
- App key and three rewarded ad-unit IDs are present; treat dashboard approval/fill as unverified external state.
- Consent handling, placement catalog, `AdGateService`, callback-based grants, retry handling, and ILRD wiring exist.
- `RewardedAdSkip` defaults OFF.
- Live UI callers appear to use the asynchronous build-skip overload, but the obsolete synchronous `WatchAdToSkip`/`TryShowAd` route remains available and must be proven unreachable or retired.
- Android declares `com.google.android.gms.permission.AD_ID`; merged-manifest and packaged dependencies still require build verification.

**Stop condition:** If any baseline claim changed materially, update this section in the RESULT before implementation continues. Do not silently implement against an obsolete audit.

---

---

## 3. Lane P — purchasing, ordered implementation plan

### P0 — Freeze the commercial contract

- [ ] Select one cheapest, fully deliverable pack as the devnet canary SKU.
- [ ] Record its canonical SKU, SOL amount, exact grant contents, ownership semantics, and refund/support description.
- [ ] Reconcile every displayed field against `PackCatalog` and `ApplyPackContents`; hide any undeliverable SKU from the enabled-purchase surface.
- [ ] Declare **SOL/devnet** the first rail. Keep SKR unavailable and labelled honestly; do not fabricate a mint.
- [ ] Confirm the devnet recipient is a non-production test treasury approved by the owner.
- [ ] Define a maximum canary amount and refuse zero, negative, NaN, excess, or catalog-mismatched amounts.

**Gate P0:** The server and client can derive the same immutable `{sku, currency, amount, recipient, network}` contract without trusting client-supplied price data.

### P1 — Implement real Mobile Wallet Adapter transaction signing

- [ ] Reuse the active targeted Mobile Wallet Adapter association that established wallet authority.
- [ ] Construct an unsigned SOL transfer with a recent blockhash and the connected account as fee payer/signer.
- [ ] Present the transaction to MWA for user authorization/signing; do not revive implicit `Web3.Login` wallet election.
- [ ] Submit the signed transaction through the configured devnet RPC and return a structured pending result containing the signature.
- [ ] Distinguish cancel, wallet rejection, association loss, RPC submission failure, expiration, and unknown/pending; none may grant.
- [ ] Prevent concurrent taps from creating accidental duplicate payments. Re-enable deliberately after a terminal refusal or expose the existing pending transaction.
- [ ] Redact transaction bytes and sensitive wallet-session data from logs while retaining signature, SKU, network, and state for support.

**Gate P1:** Owner device signs and submits the canary SOL transfer through MWA; Solana Explorer/RPC shows the expected devnet signer, recipient, and amount. No entitlement is granted yet.

### P2 — Build the authoritative verification and entitlement backend

- [ ] Add a durable pack-purchase table with, at minimum: transaction signature/payment ID, authenticated wallet, SKU, rail, network, expected amount, expected recipient, observed amount/recipient, chain status, entitlement status, timestamps, failure reason, and a uniqueness constraint preventing replay.
- [ ] Add an authenticated verify/reconcile endpoint. Bind the authenticated wallet to the transaction signer.
- [ ] Resolve SKU/price/recipient from a server-owned catalog/configuration. Never accept the client’s amount or recipient as authority.
- [ ] Query the network-specific RPC and require finalized/owner-approved commitment, successful execution, correct signer, exact recipient, exact currency/mint, and at least the exact catalog amount according to the declared rounding rule.
- [ ] Reject wrong-network signatures, reused signatures, mismatched SKU, wrong recipient, underpayment, incorrect mint, failed transactions, and unrecognized wallets.
- [ ] Treat a not-yet-visible/not-yet-final transaction as **pending and retryable**, not failed and not granted.
- [ ] Insert or return the same entitlement atomically and idempotently for retries. A signature cannot buy two SKUs or belong to two wallets.
- [ ] Provide a query/reconcile route so pending or already-owned entitlements can be restored after app loss/reinstall.
- [ ] Add structured operational events for submitted, pending, verified, rejected, entitlement-created, grant-acknowledged, and manual-review states.
- [ ] Apply rate limits and safe error responses. Never return secrets or raw infrastructure errors.

**Gate P2:** Automated tests prove valid canary verification plus wrong signer, recipient, amount, SKU, network, duplicate, pending, and failed-chain cases. Database retry produces one entitlement row.

### P3 — Fulfill and reconcile on the client

- [ ] After submission, show a non-destructive “payment pending verification” state and persist enough non-secret data to resume.
- [ ] Poll/retry verification with bounded backoff and resume reconciliation on launch, wallet reconnect, and store reopen.
- [ ] Invoke `ApplyPackContents` only from a verified durable entitlement.
- [ ] Make the local grant exactly-once while allowing recovery if grant/save fails. Do not permanently claim the local ledger before the entitlement is demonstrably applied and saved.
- [ ] Verify the SKU is owned and every promised resource/cosmetic landed; persist and reload it.
- [ ] Acknowledge fulfillment to the backend after local proof. Backend entitlement remains authoritative if acknowledgement is lost.
- [ ] Render a receipt/support surface containing safe identifiers: SKU, amount/currency, network, transaction signature, verification state, and time.
- [ ] Provide honest states for cancelled, rejected, pending, verified/grant-pending, completed, and support-required.

**Gate P3:** The same verified entitlement restores without a second payment after process death, deleted local grant-ledger state, or reinstall/sign-in on the same wallet.

### P4 — Devnet destructive/failure matrix

Run on the owner Android device and retain timestamped evidence:

- [ ] Happy path: tap → approve → chain confirmation → server verification → one exact grant → save → relaunch.
- [ ] Cancel in wallet: no transaction and no grant.
- [ ] Reject/close association: no grant and UI recovers.
- [ ] Double-tap/repeated callback: at most one submitted intent and one grant.
- [ ] Kill after submission but before verification: relaunch reconciles.
- [ ] Kill after verification but before grant/save: relaunch grants once.
- [ ] Retry completed signature: no second grant.
- [ ] Submit wrong SKU/amount/recipient/network/signing wallet to the verifier: rejected.
- [ ] Airplane mode before signing, after signing, and during verification: no loss; pending state recovers.
- [ ] RPC timeout/not-found before finality: remains pending, never free and never falsely failed.
- [ ] Insufficient balance and expired blockhash: clear refusal, no grant.
- [ ] Verify analytics/support logs contain no secrets and can trace the payment state.

**DEVNET ACTIVATION GATE:** Compile/regressions green; all P0–P4 evidence green; owner signs the Result. Only a private tester/devnet build may enable purchasing at this point.

### P5 — Mainnet readiness and flip

- [ ] Provision a production treasury explicitly intended for revenue; do not reuse an identity/grant wallet.
- [ ] Configure a production RPC with operational ownership, quota monitoring, and secret handling.
- [ ] Configure mainnet recipient and supported mints per environment. SOL may launch alone; do not block it on SKR.
- [ ] Remove the unconditional Mainnet refusal only behind an explicit production configuration gate that fails closed.
- [ ] Verify server catalog amounts, currency decimals, recipient, commitment, and network against mainnet configuration.
- [ ] Confirm terms, refund/support route, privacy disclosure, regional/store-policy requirements, and accounting ownership.
- [ ] Build a release candidate with no stub/test define, no devnet endpoint, and purchase flag still OFF by default.
- [ ] Owner performs one lowest-value mainnet canary purchase and repeats pay → verify → grant → save → relaunch → reconcile.
- [ ] Confirm treasury receipt, backend entitlement, client receipt, analytics event, and support lookup all join on the same signature.
- [ ] Owner explicitly approves `RealmStorePurchase` ON for the intended Android release audience.
- [ ] Document a kill switch that stops new purchases without preventing reconciliation of already-paid transactions.

**MAINNET ACTIVATION GATE:** Owner approval plus complete evidence. Any ambiguity keeps new Buy actions OFF while reconciliation remains ON.

---

---

## 5. Required automated gates

CLI must run the repository’s canonical versions and record exact commands, artifact/log paths, timestamps, and commit SHA:

- [ ] Clean compile gate.
- [ ] Full DataRegression with zero monetization failures.
- [ ] Wallet/provider tests: stub and non-signing refusal, devnet selection, Mainnet configuration refusal/allowance.
- [ ] Payment-verifier tests: valid, pending, failed, replay, wrong signer/recipient/amount/mint/network/SKU.
- [ ] Entitlement tests: atomic uniqueness, retry, reconciliation, grant/save failure recovery, relaunch/restore.
- [ ] Pack integrity: every enabled SKU’s promise equals its actual exact grant.
- [ ] Ad async contract: reward once, dismissal/no-fill zero, double callback once, completion-without-reward zero.
- [ ] Ad covenant and placement-catalog tests.
- [ ] Static scan for public `STORE_RAIL_LOCAL_TEST`, test-suite metadata/calls, direct SDK bypasses, and secrets.
- [ ] Android build/Gradle dependency resolution and merged-manifest inspection.

No oracle may derive its expected result solely from the same constants or catalog it is validating. At least one independent fixture/expected contract must pin each money amount and reward.

---

## 6. Today’s execution order

Parallel work is allowed only where files do not overlap. Preferred critical path:

1. **CLI baseline audit and configuration inventory** — P0 + A0.
2. **Purchasing architecture first:** P1 MWA signing and P2 backend verifier/schema can be developed in parallel if their request/response contract is frozen first.
3. **Ads risk closure:** A1/A2 while device/build work proceeds.
4. **Automated gates** for each completed slice before device testing.
5. **Ads physical-device Test Suite and production-fill proof** — likely the shortest route to revenue today.
6. **Purchasing devnet owner-device matrix** after verifier deployment.
7. Produce two separate decisions: `ADS: ENABLE | HOLD` and `PURCHASE DEVNET: ENABLE TESTER | HOLD`.
8. Mainnet work begins only after the devnet Result is signed; do not wait for SKR if SOL is green.

---

## 7. Stop/escalation conditions

Stop the affected lane and report evidence if:

- a payment can grant before authoritative verification;
- server verification cannot prove recipient and amount;
- a paid entitlement can be lost or duplicated under an interruption test;
- the proposed treasury is not explicitly a revenue wallet;
- environment configuration could point a release build at devnet or a test build at mainnet without an obvious hard failure;
- any ad grants without the earned-reward callback or grants premium/monetary value;
- consent is applied after initialization;
- production fill/account approval cannot be verified;
- Test Suite/test mode remains in the release candidate;
- required evidence depends on editor mocks where physical-device behavior is required;
- unrelated dirty-tree changes overlap the intended commit.

A stop does not discard completed work. Commit a safe restore point only if it compiles, its limitations are explicit, public flags remain OFF, and the commit contains only MON001-attributable paths.

---

## 8. Commit and push contract

- Inspect `git status` before staging and again after every gate.
- Stage explicit paths/hunks only. Never use broad staging in a dirty tree.
- Review `git diff --cached` and attribute every hunk.
- Suggested commits:
  1. `feat(monetization): add verified devnet purchase authority [MON001-P]`
  2. `fix(ads): close rewarded activation gates [MON001-A]`
  3. `test(monetization): pin payment and rewarded activation contracts [MON001-G]`
  4. `docs(monetization): record activation evidence and owner rulings [MON001-R]`
- Shared registration/configuration files require hunk-by-hunk review and must be committed with the lane whose change they contain.
- Before push, sweep all uncommitted changes and explicitly list: ours/staged, ours/not staged, and not ours.
- Do not mark either lane complete merely because its implementation commit was pushed.

---

## 9. Required RESULT

Create `WorkOrders/MON001_RESULT.md` with:

- inspected and tested commit SHAs;
- exact files changed by each lane and commit hashes;
- baseline reconciliation (confirmed/changed/stale);
- primary purchase rail, network, canary SKU, amount, and safe recipient identifier;
- backend deployment/schema migration identity and rollback notes;
- transaction signature(s), entitlement record identifier(s), and Explorer link(s), with no secrets;
- complete P0–P5 and A0–A5 checklist with PASS/FAIL/HOLD and evidence paths;
- exact automated gate commands/results;
- Android artifact hash/version and device/OS tested;
- dashboard impression/ILRD evidence identifiers;
- interruption/reconciliation test results;
- all external blockers and named owner inputs;
- kill-switch procedure for purchases and ads;
- four explicit rulings:
  - `PURCHASE DEVNET TESTER: ENABLE | HOLD`
  - `PURCHASE MAINNET PUBLIC: ENABLE | HOLD`
  - `REWARDED ADS ANDROID PUBLIC: ENABLE | HOLD`
  - `SKR RAIL: ENABLE | HOLD`
- owner name/date sign-off for every `ENABLE` ruling.

---

## 10. Completion definition

MON001 is complete only when:

- both lanes have a filled Result and evidence packet;
- every enabled lane passed its activation gate and has owner sign-off;
- every held lane is safely OFF with a concrete blocker and next action;
- reconciliation remains available for already-paid transactions even when new purchases are disabled;
- the exact production artifact contains no stub rail, dev/test enable define, test-suite mode, or secret;
- commits contain only attributable MON001 changes and are pushed by the CLI seat.

It is valid for MON001 to finish with **ads public ON, purchasing devnet tester ON, purchasing mainnet HOLD, and SKR HOLD**. That is staged activation, not partial truth.
