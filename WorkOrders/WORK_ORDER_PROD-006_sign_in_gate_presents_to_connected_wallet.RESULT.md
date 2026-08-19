# RESULT — PROD-006 — the SIGN IN gate presented to an already-connected wallet

**Verdict:** **LANDED AND PROVEN.** Code present and reachable at HEAD; proven on a real device build.
**Commit:** `ba76f67eb` — *"fix(prod-006): the sign-in gate reads the wallet, not Firebase"*, 2026-08-18 21:41.
**Written:** 2026-08-19 by a read-only verification pass (HEAD `399bfb900`). No Unity run for this file.

---

## 1. What was wrong

Failure class **(b) WRONG SOURCE**, not a race. The boot gate read exactly one input:

```csharp
signedIn = ready && FirebaseAuthService.Instance.IsSignedIn;   // pre-fix
```

Nothing in `PresentOrContinue` consulted wallet state — while the same file's identity law states that an
email/Google success **binds nothing** (Firebase = access) and only the wallet path re-keys the save. So
**a wallet-only player is never Firebase-signed-in**, and this panel would have presented on **every launch,
forever**, for exactly the players the wallet rail was built for. On the live dApp Store build it is the
FIRST screen and it blocked play.

Proof it was not a race, from the owner's device capture, same boot (quoted in the WO §2 and `ba76f67eb`):

```
20:21:38.597   wallet published connected=True
20:21:38.598   corner button relabelled   (a subscriber ran — the publish was live)
20:21:43.478   login gate decided         (~4.9 s LATER)
```

## 2. What shipped — verified at HEAD, not from the commit message

| Mechanism | file:line |
|---|---|
| Pure decision seam `ShouldContinueWithoutLogin(walletConnected, walletIdentityBound, firebaseSignedIn) => a \|\| b \|\| c` | `Assets/_Modules/Onboarding/LoginPanelController.cs:113-115` |
| Both wallet inputs sampled inside `Guard.Try`, then the seam is called | `LoginPanelController.cs:154-164` |
| `HasAttestedWalletIdentity` — reads the PERSISTED save key + device attestation, so it is TRUE **synchronously at boot** with no publish to await | `Assets/_Modules/Core/State/GameStateService.cs:1308` (`=> IsRealWalletConnected()`) |
| Trace reports the decision AND all three inputs — it can now report **why it PRESENTED**, which the old Step could never do | `LoginPanelController.cs:166-173` |
| Regression `[login-gate]`, **registered** (not standalone) | suite `Assets/Editor/Regression/LoginGateRegression.cs`; registration `Assets/Editor/Regression/DataRegression.cs:479` |

**No delay, timeout, retry or frame-count constant was added** — checked by reading `PresentOrContinue` at
HEAD. The one `Timeout(...)` present (`LoginPanelController.cs:139-140`) is the pre-existing Firebase-init
softlock ceiling, not a wallet wait; on expiry it PRESENTS the panel, so it cannot lock the boot.

## 3. THE PROVING EVIDENCE

`docs/proof/2026-08-18-overnight-gear-structures/README.md` (committed `fef3656d8`), Solana Seeker
SM02G4061955851, build **331367**:

```
PROD-006 sign-in gate
  auto-resume SUCCEEDED - connected at boot with no player action
  NO LoginPanelController:Build line in the whole session - the panel never constructed.
```

plus `01_AFTER_boot_331367.png` — *"title screen, wallet CHKK...sfkC connected top-right, NO SIGN IN modal"*.

**The absence of a `Build` line is the strong form of the proof:** it is not "the modal closed quickly", it
is "the modal was never constructed". That is a captured-data claim, not a felt one.

Gate markers: `COMPILE_GATE_OK` + regression green per `ba76f67eb`; `[login-gate]` runs inside
`DataRegression.RunAll`.

## 4. WHAT IS NOT PROVEN

1. **The PRESENT path on real hardware.** Acceptance §6.2 — a **clean install** (no save, no wallet) must
   still show the modal with all three inputs False — was proven only by the regression truth table
   (`LoginGateRegression.cs:111-112`), **never on a device**. A wrong-direction failure here is the worst
   possible one: a genuinely new player who can never sign in. *What would settle it:* one wipe-and-install
   on the Seeker, capturing `login gate decision=PRESENT (walletConnected=False, walletIdentityBound=False,
   firebaseSignedIn=False)`.
2. **The guest-downgrade assessment is a code read, not a run.** `LoginViewModel.ContinueAsGuest()` being a
   trace-only no-op and `RetireLegacyIdentity` skipping wallet-shaped keys were read at source; no session
   exercised the Guest button with a wallet bound.
3. **`LOGIN_GATE_OK` was not re-observed by this pass** — the suite is registered and the commit reports the
   run; I did not re-run it (Unity lock held by the CLI seat).
4. The captured session is **one device, one wallet**. A stub/copied save being rejected by the allowlist is
   asserted by the suite, not observed in the wild.
