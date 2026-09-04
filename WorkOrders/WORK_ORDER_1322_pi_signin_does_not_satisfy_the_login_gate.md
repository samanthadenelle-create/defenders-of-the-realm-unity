> **PARKED 2026-09-02 by owner ruling — the Android APK is the priority. Pi work resumes on her word.**

# WORK ORDER 1322 — A signed-in Pi player is still forced through the wallet gate

**Status:** CLOSED 2026-09-04 - owner felt-test PASS (validated 2026-09-04T14:37:23, build 2026.09.04.354315). PRIOR STATUS: FIXED — implemented by de5bb13a5 `fix(pi): the wallet gate, the SKR storefront, and the overlapping action bar` (body §WO-1322). Awaiting the owner's felt-verification (PO closes, CLAUDE.md §13). *(Board status audit 2026-09-02; body unchanged.)* *(Prior line:)* **Status:** READY TO IMPLEMENT
**Silo:** Onboarding / Auth / Pi
**Minted:** 2026-09-02 (CLI) from an owner felt-test in REAL Pi Browser.
**Severity:** P0 for the Pi channel — Pi sign-in works and then the game demands a Solana wallet anyway.

## Owner report + the screenshot

> *"After signing in asking for wallet"*

After a SUCCESSFUL Pi sign-in, the player is shown **CHOOSE YOUR WALLET** — *"Your wallet is your
save. Connect now (one-time on this device). Guest progress stays here until you connect."* with
**CONNECT WALLET** / **PLAY AS GUEST**.

## Captured proof that the identity was already good

Session `wt-1454dc8bfaa4` (and reproduced in `wt-ea6bc0d7b98f`, `wt-2129a836dcdd`), real Pi Browser:
```
[Flow:Skin] Pi Browser host detected - resolving the Pi skin.
[Flow:Skin] Currency skin resolved: 'pi' (auth=PiSdk, symbol=pi, identity=PiUid).
[Flow:Pi]   PiInit(sandbox=False)
[Flow:Pi]   PiAuthenticate(scopes=username)
[Flow:Pi]   Signed in as samanthadenelle (uid bound to session).
```
The skin says the identity is **`PiUid`**. The player is authenticated. The gate asks for a wallet anyway.

## Root cause, read at source

`Assets/_Modules/Onboarding/LoginPanelController.cs:159-183` (`PresentOrContinue`) samples exactly two
inputs and hardcodes the third:
```csharp
walletConnected     = CurrencySkinResolver.IsWalletConnected;
walletIdentityBound = svc != null && svc.HasAttestedWalletIdentity;
bool continueIn = ShouldContinueWithoutLogin(walletConnected, walletIdentityBound, false);
//                                                                                 ^^^^^
// "Third input is permanently false in a wallet-only build (WO-837-B)"
```
`PiSignInController` is not referenced anywhere in the file. **The gate is skin-blind.**

⚠ The `false` is not a bug someone typed carelessly — it is a WO-837-B assumption ("wallet-only
build") that was TRUE when written and that **the Pi skin outgrew**. Same shape as the retired
dependency table and the stale WO block in CLAUDE.md: a correct fact that the project moved past.

## The fix

Under `SkinAuthMode.PiSdk`, the WALLET IS NOT THE IDENTITY — `PiSignInController.SignedInUid` is.
`CurrencySkinResolver.Active.AuthMode` already carries this, and `CurrencySkin.ResolveIdentityKey`
already knows how to produce the right key per skin.

The gate must treat **a signed-in Pi session under the Pi skin** as satisfying the identity
requirement, and continue without presenting.

## ⛔ Constraints — read before touching this file

- ⛔ **Do NOT weaken the gate for the SKR/Solana skin.** `walletConnected` / `walletIdentityBound`
  remain the authority there. WO-1249 states plainly that a first run with every input false
  PRESENTS, that this is the one-time connect and not a bug, and that it must behave identically on a
  tester APK and a store build. **Do not branch this decision on a tester define.**
- ⛔ Do NOT reintroduce any network call or `await` in `PresentOrContinue`. WO-837-B removed a
  blocking 12s Firebase probe from here and calls it "the worst softlock site on the whole surface".
  `PiSignInController.IsSignedIn` is a static bool — free, synchronous. Keep it that way.
- ⛔ **"Play as Guest" must remain reachable and never disabled** (`LoginPanelController.cs:31`).
- ⛔ Do not delete or weaken the `FlowTrace.Step("Auth", "login gate decision=...")` line — extend it.
  It must report the NEW input too, or the next reader cannot see why the panel appeared. A trace
  that cannot report the wrong outcome is decoration.
- ⛔ Do not touch `CurrencySkinResolver`'s WO-787 host routing (WO-1317 proved it correct in the field).

## Acceptance criteria

1. Pi Browser + signed in with Pi -> the wallet gate does **NOT** appear; the player continues.
2. Pi Browser + NOT signed in -> unchanged behaviour (the panel may present; Guest still works).
3. Non-Pi browser / SKR skin -> byte-for-byte unchanged. Prove this explicitly; it is the regression risk.
4. The gate's FlowTrace line reports the Pi input alongside the wallet ones.
5. A regression pins all three rows above, including the SKR row. `COMPILE_GATE_OK` + `REGRESSION_OK`.
6. Verified from a CAPTURED Pi Browser session showing the gate decision = CONTINUE, not from reasoning.

## Note for whoever implements

`ShouldContinueWithoutLogin`'s third parameter is named `legacySignedIn` and documented as permanently
false. **Do not just pass the Pi flag into it and move on** — that hides a live, correct identity
source behind a parameter named "legacy". Give the Pi input its own named parameter (or its own clause)
so the next reader sees what it is.
