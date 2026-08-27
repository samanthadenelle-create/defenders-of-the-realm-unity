# RESULT -- WO-1249 -- Boot still lands on the "validate wallet" screen

**Verdict:** **WORKS AS INTENDED** for the Unity boot route. The bound-wallet gate already CONTINUES. First-run PRESENT is production-correct and is the same on a tester APK as on the store build. Extra native Seed Vault sheets AFTER CONTINUE are session minting (WO-1157), not this panel.

**No tester skip was added.** Owner ruling 2026-08-27 stands: `TESTER_BUILD` is tooling she invokes, not a licence to change boot behaviour.

**Status line:** left as-is on the WO (this seat does not flip Status).

---

## 1. Prior attempt ("still") -- found, not stacked

Two prior attempts, neither a tester skip in code:

| Attempt | Where | Outcome |
|---|---|---|
| **PROD-006** (2026-08-18, `ba76f67eb`) | `LoginPanelController.ShouldContinueWithoutLogin` + `[login-gate]` | REAL FIX. Gate used to read Firebase only, so a wallet-only player saw SIGN IN on every launch. After the fix, connected OR attested-bound CONTINUES. That is why today's capture says CONTINUE and there is no `LoginPanelController.Build`. |
| **This ticket's original recommendation** | WO-1249 body, before the owner ruling | NEVER IMPLEMENTED. Grep of `Assets/_Modules/Onboarding/` is clean of `TESTER_BUILD` / `IsTesterBuild`. A second skip layered on that recommendation is exactly what the owner forbade. |

`DevBootScene` is ARG-GATED (`-bootScene`) and is not a tester-wallet bypass. `WalletSkinBootstrap.TryAutoResumeAsync` is the silent MWA resume; it is not a skip of the Unity panel.

---

## 2. The decision that shows (or does not show) the Unity panel

Named from a captured device line, not from a code read.

Device: `SM02G4061955851`. Tester APK boot 2026-08-27 11:49 (`logs/f8-inbox/device-stage/logcat-wallet.txt`).

```
08-27 11:49:19.886  [Flow:Wallet] auto-resume SUCCEEDED -- connected at boot with no player action.
08-27 11:53:52.214  [Flow:Auth] login gate decision=CONTINUE (walletConnected=True ..., walletIdentityBound=True, legacySignedIn=false [wallet-only build, WO-837-B]).
```

Same shape on the 12:33 relaunch:

```
08-27 12:33:06.439  [Flow:Wallet] auto-resume SUCCEEDED -- connected at boot with no player action.
08-27 12:33:25.628  [Flow:Auth] login gate decision=CONTINUE (walletConnected=True ..., walletIdentityBound=True, ...)
```

There is **no** `LoginPanelController.Build` / `login panel presented` line in that session. The Unity panel was never constructed.

The seam:

```117:177:Assets/_Modules/Onboarding/LoginPanelController.cs
        public static bool ShouldContinueWithoutLogin(bool walletConnected, bool walletIdentityBound,
                                                      bool legacySignedIn)
            => walletConnected || walletIdentityBound || legacySignedIn;
        // ...
        public static void PresentOrContinue(Action onContinue)
        {
            // samples CurrencySkinResolver.IsWalletConnected + GameStateService.HasAttestedWalletIdentity
            bool continueIn = ShouldContinueWithoutLogin(walletConnected, walletIdentityBound, false);
            // CONTINUE => onContinue(); PRESENT => Present(onContinue)
        }
```

The one production chokepoint (new-game only; Title Continue never hits it):

```102:110:Assets/_Modules/Onboarding/FoundingChoiceController.cs
        public static void PresentOrContinue(Action onContinue)
        {
            // returning players (Title -> Continue) never pass through
            LoginPanelController.PresentOrContinue(() => PresentFoundingChoice(onContinue));
        }
```

Called from `HeroSelectController.OnDiveVillageClicked` (and PetSelect as belt-and-braces). First-run with every input false PRESENTS. That is the one-time connect. Bound / connected CONTINUES. There is no tester arm.

---

## 3. What the owner actually saw (and what this ticket is not)

The word "validate wallet" does **not** exist in Unity copy. The screen on that boot is the **native** `com.solanamobile.wallet.MWABottomSheetActivity`.

Two native sheets on the same capture, both AFTER the Unity gate already said CONTINUE:

1. **Boot auto-resume (~11:49:16-19).** Unity traces it as silent reauthorize. WindowManager still focuses `MWABottomSheetActivity` for ~3s. Wallet silo, not this panel.
2. **Immediately after CONTINUE (~11:53:52).** `SignMessage via targeted MWA association` then the same bottom sheet. That is session mint on the first authed action. `WarmUpSessionAsync` at connect logged `session warm-up deferred - first authenticated action will mint; boot/connect never signs.` That mint is **WO-1157 / `BackendRequestSigner`**, which this ticket is forbidden to touch.

So: Unity boot route = WAI. Extra sheets after CONTINUE = 1157. Do not skip the Unity panel to hide Seed Vault.

---

## 4. What this seat changed (copy + log hygiene + pin; no skip)

First-run PRESENT is correct production behaviour, so the panel stays. Changes are so it does not read as a bug, and so the next capture cannot leak an address.

| Change | Why |
|---|---|
| Panel title `"SIGN IN"` -> `"YOUR WALLET"` | Email-era wall. The owner already named SIGN IN as the 08-18 defect. |
| Intro now says `Connect now (one-time on this device).` ASCII only | First-run connect is the product, not a tester wall. |
| Decision `FlowTrace` dropped `ConnectedWalletShortAddress` | WO-1249 acceptance: never log a wallet address. Booleans are enough. |
| PRESENT Step now says `production path, same on every build` | Next device capture names the route without a tester branch. |
| `[login-gate]` `CheckProductionBootRoute` | Pins the production route: no tester define, no address log, founding still calls `PresentOrContinue`, one-time copy. **No tester variant to assert.** |

RED-first (WO-1138), against the unpatched file:

- `Forbid("ConnectedWalletShortAddress")` -- the unpatched decision Step logged `wallet=` + short address. That needle is gone.
- `Require("\"YOUR WALLET\"")` / `Require("one-time on this device")` -- the unpatched title was `"SIGN IN"`. That needle is gone.

`ShouldContinueWithoutLogin` truth table is unchanged: first run PRESENT; connected / bound / legacy CONTINUE.

---

## 5. What was NOT done

- No `TESTER_BUILD` / `IsTesterBuild` branch on this gate.
- No edit to `BackendRequestSigner` / `MintSessionAsync` / `WarmUpSessionAsync` (WO-1157).
- No edit to `HeroSelectController` (WO-1248).
- No commit, no Unity run (orchestrator gates).
- WO Status line not flipped.

Brace-balance + NUL: `LoginPanelController.cs` 23/23, `LoginGateRegression.cs` 60/60, no `\x00`.

---

## 6. Owner felt-verify

On a **bound** wallet: Title -> Continue (or new-game through hero select) must **not** construct the Unity `YOUR WALLET` panel. Prove by the absence of `login panel presented` and a `login gate decision=CONTINUE` line.

On a **wipe / first run**: the Unity `YOUR WALLET` panel with Connect Wallet + Play as Guest is the production one-time connect. Walk it. Native Seed Vault sheets after CONTINUE are WO-1157, not a regression of this ticket.
