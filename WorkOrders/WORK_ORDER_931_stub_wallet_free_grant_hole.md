# WORK ORDER 931 — Close the StubWalletProvider free-grant hole

**Status:** DONE — implemented 2026-08-10 (option b, owner-picked), gates green, RESULT filed;
felt/owner close per pipeline. See `WORK_ORDER_931_stub_wallet_free_grant_hole.RESULT.md`.
**Minted:** 2026-08-08 (CLI seat, main line — banner bumped 931 → 932 in the same edit)
**Silo:** Wallet / Monetization (`Assets/_Modules/Wallet/*`) — file-disjoint from gameplay lanes
**Type:** EXISTING (built, latent) — security / entitlement integrity, NOT a new feature
**Blocks:** re-enabling `FeatureFlags.RealmStorePurchase` (precondition **3** of 3 in that flag's
"DO NOT TURN THIS BACK ON" block, `Assets/_Modules/Core/FeatureFlags.cs:598-609`)

---

## 1. The defect in one line

`StubWalletProvider` fake-succeeds a payment and `PackStore` grants the pack in full for **zero
money**. The **only** thing standing in front of that chain today is one feature flag's default.

---

## 2. The defect chain — every hop verified at source (2026-08-08)

| # | Hop | File:line | What it does |
|---|---|---|---|
| 0 | **No build guard** | `Assets/_Modules/Wallet/StubWalletProvider.cs` (whole file, 221 lines) | There is **no `#if` of any kind** in this file — not `UNITY_EDITOR`, not `DEVELOPMENT_BUILD`. It therefore compiles into **every shipped player**. |
| 1 | **Auto-select picks the stub** | `WalletService.cs:360-383` | The real provider is taken only when `SolanaWalletProvider.IsSupportedOnThisPlatform` is true (`:360`); the `else` branch does `_provider = new StubWalletProvider();` (`:374`). |
| 1a | …and that capability is Android-only | `SolanaWalletProvider.cs:140-146`, guard at `:142` | `IsSupportedOnThisPlatform` is compiled from `#if SOLANA_SDK && UNITY_ANDROID && !UNITY_EDITOR`. So **release desktop, WebGL, and Android-without-SOLANA_SDK all land on the stub.** |
| 2 | **Buy entry** | `PackStore.cs:476-480` | `if (!FeatureFlags.RealmStorePurchase) → refuse`. This is the *whole* defense. Flag on ⇒ flow continues. |
| 3 | **Fabricated connect** | `PackStore.cs:502` → `StubWalletProvider.cs:97-112`, address minted at `:202-205` | `Connect()` waits 350 ms and returns a 44-char `"stub-wallet-" + 32 base58` address; `IsValid` passes, so `PackStore.cs:503` does not abort. |
| 4 | **Mock balance check** | `PackStore.cs:512` → `WalletService.Pay` `:538-577` (provider call `:563`) → `StubWalletProvider.SendPayment` `:146-172` | Affordability is tested against `StartingBalance` (`StubWalletProvider.cs:68-73`: **Sol 5 / Usdc 250 / Skr 2000**), deliberately seeded "generous… so all five packs are purchasable". Every pack is affordable. |
| 5 | **Fabricated signature → `Ok`** | `StubWalletProvider.cs:169-171`, generator at `:208-211` | 1.1 s fake finality, mock balance debited, `RandomBase58(88)` returned as the tx signature, `PaymentResult.Success(...)`. **No chain was ever touched.** |
| 6 | **Full grant** | `PackStore.cs:514-518` | `result.Ok` ⇒ `_vm.ApplyPackContents(pack)` — the entitlement lands, permanently, for zero payment. |
| 7 | **Poisoned analytics** | `PackStore.cs:524-530` | `EventTracker.Track("purchase_completed", { packId, packName, currency, txSig = result.TxSignature })` fires with the **fabricated** signature — a fake revenue event, indistinguishable downstream from a real one. |

Supporting fact (not a hop, but load-bearing for fix (b)): the codebase already has a
**positive provider attestation** — `WalletService.IsRealSigningWallet` (`WalletService.cs:303-304`)
= `IsConnected && !(_provider is StubWalletProvider) && _provider.CanSignMessages`, and
`StubWalletProvider.CanSignMessages => false` (`StubWalletProvider.cs:183`). The save layer already
refuses to key a cloud identity off the stub using exactly this test. **The payment layer does not
consult it at all.** That asymmetry is the bug in one sentence.

---

## 3. Why this is NOT urgent today — and IS a hard blocker later

`FeatureFlags.RealmStorePurchase` declares `defaultOn: false`
(`Assets/_Modules/Core/FeatureFlags.cs:607`), re-gated 2026-08-08 for the store-submission build.
With it off, `PackStore.Purchase` refuses at `PackStore.cs:476` before any wallet call, so **nothing
in the shipping build reaches the stub**. There is no live exposure and no hotfix pressure.

It becomes live **the instant monetization is switched on** — a flag flip, a `ff.realmstorepurchase`
PlayerPrefs entry on any device, or a future WO that turns the rail on for a real payment rail. At
that moment the hole is not a dead button, it is **free packs on desktop, WebGL, and any Android
build missing SOLANA_SDK**. Fix it *before* the flip, not as part of it — landing this WO first
removes the scariest of the three preconditions from the monetization critical path.

---

## 4. Candidate fixes — ARCHITECTURE CALL, DO NOT PICK ONE HERE

This WO deliberately does not choose. The seam question (compile-time vs runtime) is an
architecture decision for the owner / lead.

### (a) Build-guard the stub out of release players
Wrap `StubWalletProvider` in `#if UNITY_EDITOR || DEVELOPMENT_BUILD`.

- **Pro:** strongest possible guarantee — the free-grant code physically does not exist in a release
  binary. Nothing to bypass, nothing to mis-configure at runtime, nothing a PlayerPrefs toggle can
  reach. Also shrinks the attack surface for anyone decompiling the APK/exe.
- **Con — this is the real cost:** `WalletService`'s auto-select fallback (`WalletService.cs:374`),
  the null-coalesce in the explicit ctor (`:312`), and `WalletService.Create(useStub: true)` (`:392-397`)
  all name the type unconditionally. On a release desktop/WebGL build the `else` branch would have
  **no provider at all**. Unless a release-safe alternative is supplied, `_provider` is null and the
  first `IsConnected` / `Connect` / `GetBalance` call **throws** rather than degrading — turning a
  benign "wallet unavailable" into an exception on a UI path. Any implementation of (a) therefore
  owes a `NullWalletProvider` (or equivalent) that refuses everything politely, plus the same guard
  discipline on every other unconditional reference to the type.
- **Also affects:** EditMode/regression code that pins the stub explicitly, and
  `IsRealSigningWallet`'s `is StubWalletProvider` type test — both must still compile in the
  configurations they run in.

### (b) Refuse at the payment seam when the resolved provider is the stub
A runtime check inside `WalletService.Pay` / `PayFlat` (before `_provider.SendPayment`), returning
`PaymentResult.Failure` with a clear reason.

- **Pro:** matches the pattern already in the codebase — `PackStore.cs:473-480` is explicitly
  documented as defense-in-depth at the entry, and `IsRealSigningWallet` already encodes exactly the
  predicate needed. One seam, both payment entry points, no platform/build-config surgery, no null
  provider risk, and the stub keeps working for editor/dev flows that *aren't* payments (connect,
  balances, identity plumbing).
- **Con:** the free-grant code still ships in the binary; the guarantee is a runtime branch that a
  future refactor could route around, and it is only as good as the audit that every payment path
  goes through `WalletService`. Weaker than (a) against tampering.

### (c) Both
Build-guard for release **and** the runtime refusal as the belt to (a)'s braces — the runtime check
being what keeps a *development* build from fabricating a `purchase_completed` event into real
analytics. Highest cost, highest assurance.

---

## 5. In scope: `WalletService.PayFlat` — unguarded but currently dead

`WalletService.PayFlat` (`WalletService.cs:585-620`, provider call at `:606`) reaches the **same**
`StubWalletProvider.SendPayment` as `Pay` does, and it is **not gated by `RealmStorePurchase` — or by
anything else.** It is not a second instance of the bug only because it is unreachable:

- Its two callers are `TowerSwapService.cs:220` and `CryptoPaymentManager.cs:186`.
- Both are `MonoBehaviour`s (`TowerSwapService.cs:46`, `CryptoPaymentManager.cs:48`) — they only run
  if something instantiates them.
- **GUID sweep, run 2026-08-08:** `TowerSwapService` guid `7307a9b44af195e4aa694768299a2206`,
  `CryptoPaymentManager` guid `d675552ee54bfb2438c2c53102c0eaec` — **zero occurrences across every
  `.unity`, `.prefab` and `.asset` in `Assets/`.** Every remaining textual reference to either type
  is a comment or a doc-comment (`PackStoreVM.cs:165`, `Tower.cs:61/91`, `TowerSwapMenu.cs:4/125/…`,
  `GlimmerCurrencyService.cs:200`); no `AddComponent`, no `new`, no scene wiring.
- The owner confirms the **tower swap feature was deleted**.

So: **unguarded but dead.** A one-line gate at the `PayFlat` seam closes it permanently and has
**zero behavioural effect today**. Do it in this WO while the seam is open rather than rediscovering
it the day someone revives a flat-fee purchase.

---

## 6. Acceptance criteria (testable)

1. **No free grant on a release desktop/WebGL build.** With `RealmStorePurchase` forced ON, a release
   (non-development) desktop or WebGL player cannot reach `PackStore.ApplyPackContents`
   (`PackStore.cs:518`) without a signature from a real, key-holding provider. Proof: a regression
   case that drives `PackStore.Purchase` against a `WalletService` pinned to `StubWalletProvider` and
   asserts the returned `PaymentResult.Ok == false` **and** that pack ownership did not change.
2. **No `purchase_completed` from a stub payment.** Zero `EventTracker.Track("purchase_completed", …)`
   (`PackStore.cs:524`) may fire when the resolved provider is the stub — in *any* build
   configuration, including Editor and development. The fabricated `RandomBase58(88)` txSig
   (`StubWalletProvider.cs:208-211`) must never enter the analytics stream. Proof: assert on a
   captured/spied `EventTracker` (or a `FlowTrace` marker) that the event count is 0 for a stub run.
3. **`PayFlat` is gated too.** `WalletService.PayFlat` refuses (or cannot compile a stub path) under
   the same condition as `Pay`. Proof: a case calling `PayFlat` on a stub-pinned service asserts
   `Ok == false`. Behaviour of live gameplay is unchanged (both callers remain scene-absent —
   re-run the GUID sweep in §5 as part of the proof).
4. **Refusal is loud, not silent.** Every refusal emits a `FlowTrace.Warn`/`Fail` naming the reason
   (stub provider) — §12 no-silent-failure. A blank status label with no trace line fails this WO.
5. **Nothing degrades into a throw.** If fix (a) or (c) is chosen, a release desktop/WebGL build must
   still complete a wallet-related UI flow (open the store, view balances, tap Connect) without a
   `NullReferenceException` — the fallback must refuse, not crash. Proof: headless run + Player.log
   with zero exceptions on that path.
6. **The flag's precondition block is updated.** Once this lands, precondition 3 in
   `Assets/_Modules/Core/FeatureFlags.cs:603-609` is amended in the SAME commit to record that it is
   satisfied and how (§15 canon-in-the-same-breath). Do **not** flip the flag's default — that is a
   separate owner decision requiring preconditions 1 and 2 as well.
7. **The lock-in regression still bites.** `DataRegression.RunAll` stays green and
   `WalletProviderSelectionRegression` case 4b (below) is updated only if its narration is now wrong
   — its *pin* on `defaultOn: false` must survive untouched.

---

## 7. Lock-in regression — already exists, read it first

`Assets/Editor/Regression/WalletProviderSelectionRegression.cs`, **case 4b** (`:163-211`).

It is a deliberate **source-text lint**, not a runtime read, because `FeatureFlags.Get` consults
PlayerPrefs first — the declared default is the only deterministic oracle for what a fresh install
gets (`:164-173`). It pins the exact declaration with a regex (`:191-192`) and its failure message
(`:199-210`) already narrates this entire hole, ending with "Do NOT re-flip it until the unguarded
stub is closed (separate fix)". **This WO is that separate fix.**

The file also has a `PROVE IT BITES` recipe at `:175-178` — flip the one token to `defaultOn: true`,
run `DataRegression.RunAll`, watch `[wallet-provider]` go red, revert. Use it to confirm the harness
is actually running your case.

⚠ Any edit to the `RealmStorePurchase` declaration line must keep the regex at `:192` matching:
`RealmStorePurchase => Get("realmstorepurchase", defaultOn: false)`.

---

## 8. What NOT to touch

- **Do not change the value or declaration of `RealmStorePurchase`** — comment-only there.
- Do not touch `Assets/Editor/AndroidBuild.cs`, `Assets/Editor/DesktopBuild.cs`, or
  `ProjectSettings/ProjectSettings.asset` (concurrent lane, 2026-08-08).
- Do not weaken `IsRealSigningWallet` (`WalletService.cs:303-304`) — the save layer's cloud-identity
  attestation depends on it; reuse it, don't rewrite it.
- Do not revive `TowerSwapService` / `CryptoPaymentManager`. Gate `PayFlat`; leave the dead callers dead.
- Do not delete `StubWalletProvider` — editor/EditMode/offline dev flows depend on it.

---

## 9. Files in scope

- `Assets/_Modules/Wallet/StubWalletProvider.cs`
- `Assets/_Modules/Wallet/WalletService.cs`
- `Assets/_Modules/Wallet/PackStore.cs` (only if the refusal needs a caller-side companion)
- `Assets/Editor/Regression/WalletProviderSelectionRegression.cs` (new cases for §6.1-6.3)
- `Assets/_Modules/Core/FeatureFlags.cs` (comment only, §6.6)
