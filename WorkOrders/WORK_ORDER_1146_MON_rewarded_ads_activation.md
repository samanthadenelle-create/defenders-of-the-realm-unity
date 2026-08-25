**Status:** BLOCKED on public activation (dashboard/device evidence per placement + owner sign-off). CODE DONE 2026-08-22 - ACTIVATION HELD. Three placement-specific LevelPlay rewarded units, consent-before-init, main-thread ILRD forwarding, server-anchored placement accounting, earned-callback-only grants, duplicate/cross-unit callback refusal, and permanent refusal of the synchronous bypass are all present and gated ([monetization-activation] green). NOT DONE: public activation, which needs dashboard/device evidence per placement plus owner sign-off. RewardedAdSkip stays defaultOn:false until then.

# WO-1146 - MON - Rewarded ads: activation behind earned-reward proof

**Minted:** 2026-08-22 (CLI; renamed in place from the combined ticket)
**Lane:** **MON** - monetization, dedicated and prioritised.
**Split from:** the combined WO-1146 (owner ruling 2026-08-22).
**Sibling:** `WORK_ORDER_1147_MON_purchasing_verified_entitlement.md` - Lane P, independent.

## ⭐ WHY THIS LANE GOES FIRST

**Nothing here waits on an owner ruling.** Its blockers are physical-device and dashboard evidence,
which can be obtained today - where the purchasing lane's verifier work needs backend decisions that
are not made yet. `RewardedAdSkip` stays `defaultOn:false` until the owner signs the gate on that
evidence, but every step below can be built and proven before then.

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

## 4. Lane A — rewarded ads, ordered implementation plan

### A0 — Reconcile dashboard and source configuration

- [ ] Confirm Android package ID, LevelPlay App Key, and all three rewarded ad-unit IDs match the production dashboard.
- [ ] Confirm the ironSource/LevelPlay account is approved and each unit/network instance is active. Record approval/fill evidence without credentials.
- [ ] Confirm the owner test device is registered only in the dashboard, not source control.
- [ ] Confirm package and adapter versions are compatible and native dependencies resolve in the Android build.
- [ ] Inspect the merged Android manifest for `AD_ID` and required SDK declarations; record the artifact evidence.

**Gate A0:** Production configuration is internally consistent and the dashboard is capable of serving test inventory.

### A1 — Close path and callback risks

- [ ] Enumerate every call to `TryShowAd`, synchronous `WatchAdToSkip`, provider `Show`, and direct LevelPlay APIs.
- [ ] Route every live surface through `AdGateService.Present` and the asynchronous completion contract.
- [ ] Retire, hard-refuse, or regression-pin the obsolete synchronous real-SDK route so a future caller cannot bypass the placement ledger or report a false failure.
- [ ] Confirm reward callbacks are one-shot guarded and Unity state changes occur safely on the Unity thread.
- [ ] Confirm subscriptions are installed before initialization and removed appropriately; reinitialization must not duplicate rewards or ILRD.
- [ ] Confirm readiness is per ad unit and backed by actual SDK readiness, not only a cooldown.

**Gate A1:** Static regression lists all monetized call sites and fails if a new bypass or grant-on-show path appears.

### A2 — Privacy, consent, and lifecycle

- [ ] Apply GDPR consent, CCPA choice, and child-directed status before LevelPlay initialization.
- [ ] Verify undecided consent blocks initialization and presents the consent surface.
- [ ] Verify accept, reject/do-not-sell, withdrawal, and re-prompt behavior persists correctly.
- [ ] Confirm non-personalized operation where required and document the current age/COPPA owner ruling.
- [ ] Test app background/foreground, activity recreation, network changes, and repeated scene entry without duplicate initialization or listeners.

**Gate A2:** Device logs prove privacy configuration precedes the single SDK initialization in every tested consent state.

### A3 — Placement contract verification

Verify the live catalog rather than copying assumptions from older WOs:

- [ ] `place.build.skip`: exactly the configured limited time reduction; never completes more than policy permits and never grants crystals.
- [ ] `place.harvest.doubler`: exactly the configured multiplier/window; repeated ads obey stacking/extension policy.
- [ ] `place.daily.chest`: exact soft-currency grant once; does not overlap the normal claim or double-pay on reopen.
- [ ] Global and placement daily caps, cooldowns, and server-anchored time behavior persist across relaunch and resist local clock rollback/advance.
- [ ] No enabled or hidden placement grants premium currency, monetary value, revive, or combat power.
- [ ] A refused/no-fill presentation spends neither reward nor cooldown unless the documented policy explicitly records an attempted presentation.
- [ ] An earned presentation records cap/cooldown exactly once.

**Gate A3:** Covenant and placement regressions are green and each placement has a manually verified expected-delta record.

### A4 — Physical-device Test Suite matrix

- [ ] Build/install the Android release-shaped test artifact with ads enabled only for the owner/test cohort.
- [ ] Launch the official LevelPlay Test Suite on the registered physical device.
- [ ] Verify initialization, rewarded availability, adapters/networks, load success/failure, show, click, close, reward, and ILRD paths.
- [ ] For each of the three placements: completed ad grants exactly once; early dismissal grants nothing; repeated callback grants once.
- [ ] Test no-fill, airplane mode, connection loss during ad, background/resume, and repeated button taps. Gameplay must never deadlock.
- [ ] Confirm the LevelPlay dashboard records impressions and the game records matching placement/ad-unit ILRD without touching Unity APIs from a background thread.
- [ ] Remove/disable every Test Suite/test-mode call or metadata flag, rebuild, and scan the production artifact/source to prove removal.

**Gate A4:** Physical-device evidence and dashboard evidence join for all three placements; production candidate contains no test mode.

### A5 — Ads production enablement

- [ ] Confirm real production fill on the owner device after Test Suite removal.
- [ ] Confirm reward-completion, no-fill, cap-binding, and revenue events are distinguishable in telemetry.
- [ ] Define alert/hold thresholds for crash rate, reward mismatch, zero fill, and abnormal grant rate.
- [ ] Define an ads kill switch that disables new presentations without damaging earned rewards already in flight.
- [ ] Owner explicitly approves `RewardedAdSkip` ON for Android production.
- [ ] Perform a final smoke test from the exact signed release artifact.

**ADS ACTIVATION GATE:** Ads may turn ON independently when A0–A5 are green. Purchasing status does not block ads.

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
