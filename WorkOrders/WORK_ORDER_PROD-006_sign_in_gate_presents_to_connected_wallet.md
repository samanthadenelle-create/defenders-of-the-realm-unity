# PROD-006 — The SIGN IN gate presents to a player whose wallet is ALREADY connected

**Status:** DONE (implemented in the working tree, COMPILE_GATE_OK) — AWAITING OWNER FELT-VERIFY ON DEVICE; NOT yet committed at time of writing.
**Minted:** 2026-08-18 (docs seat) — PROD series, post-launch defect, jumps the dev-era backlog.
**Priority:** HIGH — it is the FIRST screen of the LIVE dApp Store build, and it blocks play.
**Silo:** Onboarding / identity. **Lane:** `Assets/_Modules/Onboarding` + `Core/State`. No scenes.
**Provenance:** owner, on the LIVE build, 2026-08-18 (device screenshot + captured trace).

---

## 1. The symptom, in the player's words

> *"im already signed in should not show this screen"*
> *"but stops on wallet connect or play as guest"*

The device screenshot shows the **SIGN IN** modal presented over a HUD that is **already rendering the
bound wallet `KK...sfkC`**. The game announces the connected identity and then demands the player
authenticate again, with the only doors being *Connect Wallet* or *Play as Guest*.

## 2. Root cause — failure class **(b) WRONG SOURCE**. Not a race.

The gate read exactly **one** input, and it was the wrong one.

`Assets/_Modules/Onboarding/LoginPanelController.cs` — **pre-fix line 106**:

```csharp
signedIn = ready && FirebaseAuthService.Instance.IsSignedIn;
```

Nothing in the decision read wallet state at all.

That is fatal in combination with this file's own identity law, still present at
`LoginPanelController.cs:556-557`:

```csharp
// IDENTITY LAW: an EMAIL/GOOGLE success binds NOTHING (Firebase = access);
// only the wallet path re-keys the save. View just proceeds either way.
```

**A wallet-only player is never Firebase-signed-in.** So this panel would have presented on **every
launch, forever**, for exactly the players the wallet rail was built for.

### Proof it is not a race (class (a)), and not a dead publish (class (c))

Captured on the owner's device, same boot:

```
20:21:38.597   wallet published connected=True
20:21:38.598   corner button relabelled  (a subscriber ran — the publish is live)
20:21:43.478   login gate decided        (~4.9 s LATER)
```

The gate decided **~5 seconds after** the correct value was already published and consumed by another
subscriber. The value simply sat unread in
`Assets/_Modules/Core/Platform/CurrencySkinResolver.cs:128` (`public static bool IsWalletConnected`),
set at `:158`. A delay would not have fixed this; reading the right source did.

## 3. The fix (applied in the working tree tonight)

| What | Where | State |
|---|---|---|
| Pure decision seam `ShouldContinueWithoutLogin(walletConnected, walletIdentityBound, firebaseSignedIn)` — `=> walletConnected \|\| walletIdentityBound \|\| firebaseSignedIn` | `Assets/_Modules/Onboarding/LoginPanelController.cs:113-115` | **applied** |
| `PresentOrContinue` samples BOTH wallet inputs inside `Guard.Try` and calls the seam | `LoginPanelController.cs:154-164` | **applied** |
| `HasAttestedWalletIdentity` — reads the PERSISTED save key + device attestation, so it is TRUE **synchronously at boot** with no publish to wait for | `Assets/_Modules/Core/State/GameStateService.cs:1308` (`=> IsRealWalletConnected()`, `:1310`) | **applied** |
| Decision trace line naming all three inputs | `LoginPanelController.cs:164-173` | **applied** |

⛔ **NO delay, timeout, retry or "wait for the wallet" constant was added, and none may be added.**
A timing knob here would encode the wrong diagnosis (class (a)) into the code permanently. The bug was
the source, not the clock.

## 4. Coverage

`Assets/Editor/Regression/LoginGateRegression.cs` — tag `[login-gate]`, markers
`LOGIN_GATE_OK` / `LOGIN_GATE_FAIL`, wired into `DataRegression.RunAll`. It asserts the seam exists
(`:79` fails loudly if `ShouldContinueWithoutLogin(bool,bool,bool)` is deleted), asserts
`GameStateService.HasAttestedWalletIdentity` still exists (`:138`), and drives a **truth table** that
includes both the captured case and the fresh-install case, with the failure text spelling out which
way it broke (`:111-112`).

## 5. Guest-downgrade risk — assessed, found LOW

The obvious fear is that continuing past the gate silently downgrades a wallet player to a guest key.
It does not:

- `LoginViewModel.ContinueAsGuest()` (`Assets/_Modules/Onboarding/LoginViewModel.cs:149-152`) is a
  **trace-only no-op** — it writes one `FlowTrace.Step` and binds nothing.
- The only code that clears `BoundWallet` is `GameStateService.RetireLegacyIdentity`
  (`GameStateService.cs:1268`, `_state.BoundWallet = null` at `:1287`), and it **explicitly skips
  wallet-shaped keys** — its own warning says it retires *"a Firebase UID bound by the old email
  sign-in path"*, preserving the old id in PlayerPrefs.

## 6. Acceptance criteria

1. On the owner's device, a boot with the bound wallet shows **NO sign-in modal** and the trace reads
   `login gate decision=CONTINUE` with `walletConnected` and/or `walletIdentityBound` True.
2. A **clean install** (no save, no wallet) still shows the modal, and the trace reads
   `decision=PRESENT` with **all three inputs False**.
3. `LOGIN_GATE_OK` green in `DataRegression.RunAll`.
4. No new delay/timeout constant appears in the diff.

## 7. What NOT to touch

- The identity law at `:556-557` — email/Google binding nothing is DELIBERATE, not the bug.
- `CurrencySkinResolver`'s publish path — it was working (proved by the 20:21:38.598 subscriber).
- Any wait/retry knob. See §3.
