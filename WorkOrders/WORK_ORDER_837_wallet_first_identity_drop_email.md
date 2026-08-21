> ⚠ **NUMBER COLLISION — this document does not own WO-837; `WORK_ORDER_837_stockpiles_cap_capacity.md` does.**
> Referred to hereafter as **WO-837-B (wallet-first identity, drop email)**.
> Flagged by the 2026-08-16 Sunday board-grooming pass (`python tools/board_build.py` → `DUPLICATE_WO_NUMBERS`);
> ownership decided by **first-on-disk** (`git log --follow --diff-filter=A`): the winner's file was created first.
> Banner only — nothing was renumbered or deleted.

> ## COLLIDED NUMBER (4th two-seat collision, 2026-08-02)
> The number 837 belongs to WORK_ORDER_837_stockpiles_cap_capacity.md (committed).
> ⚠ The old "SUPERSEDED by WO-847, do not implement" banner is RETIRED — WO-847 scoped wallet-first to
> ANDROID ONLY for a Google Play release that does not exist, and the owner reopened and closed this on
> 2026-08-21 (see the ruling at the bottom). Phase 2 is now IMPLEMENTED.

# WORK ORDER 837 — Wallet-first identity (drop email/Firebase login)

**Status:** DONE - 2026-08-21 CLI, gate-green (COMPILE_GATE_OK + REGRESSION_OK 234/234).
Phase 1 (real Solana Mobile Wallet Adapter connect) = WO-766, unchanged. Phase 3 (backend wallet-signature
verify) was **already shipped before this ticket** — see the correction in §2 below.
**Author:** UI/QA triage (read-only RCA, §13) — Claude UI
**Lane:** Monetization/Backend + Onboarding (§9, isolated). Cross-cutting: client boot login, wallet SDK, backend save-auth.
**Depends on:** WO-766 (real Solana Mobile Wallet Adapter connect) — HARD prerequisite (see §Sequencing).
**Reverses:** WO-769 (which made email/Firebase the login + wallet payments-only). Supersede its §0 ruling.

---

## 1. Owner decision (2026-08-02)
The wallet is the **sole identity/login** everywhere. **Remove the email/password (Firebase Auth) login entirely.**
Play as Guest stays as the local escape; a guest can bind a wallet later. This reverses the WO-769 email-first ruling.

### ★ CRITICAL DISTINCTION (owner clarification 2026-08-02): Firebase App Distribution ≠ Firebase Auth
Two different Firebase products were conflated. **Keep one, remove the other:**
- **Firebase App Distribution = KEEP** — this is how TESTERS get the APK (invited by their Google/email → download
  the build from the App Distribution portal). It is EXTERNAL to the game, CLI-driven (`distribute-android.ps1` /
  App Distribution, memory `firebase-app-distribution`), and the game binary does NOT need Firebase for it. A tester's
  Firebase/Google account has **nothing to do with in-game identity**.
- **Firebase Auth (in-game email/password login) = REMOVE** — the "SIGN IN" screen. In-game identity is the WALLET.
So: retiring Firebase **Auth** from the game must NOT disturb App Distribution. Do not remove the App-Distribution /
`distribute-android.ps1` / `firebase-appid.txt` delivery path — only the in-game Auth login.

## 1b. ⚠ CORRECTION — §2/§3-Phase-3 below were ALREADY STALE when this was implemented (2026-08-21)
Read this before believing §2. The 2026-08-02 **security audit** had already corrected the claims §2 makes,
and the file headers in-tree say so at source:
- **Firebase NEVER keyed a save.** Nothing ever called `GetIdTokenAsync` (zero call sites) and no
  `Authorization: Bearer` header is built anywhere. `FirebaseAuthService.cs`'s own header calls the old
  description "EVERY CLAUSE OF THAT WAS FALSE". §2's bullet 1 and §3 Phase 3's "currently verifies a
  Firebase Bearer ID token" are both wrong.
- **`/api/game/save` already verifies a WALLET signature** — `X-Wallet` + `X-Nonce` + `X-Signature`, or
  `X-Guest-Id` for the guest rail (`GameStateService.TryAttachAuthHeaders`, `api/_lib/wallet-auth.js`).
  Phase 3 needed **no work**; it was done.
- **Therefore removing email login orphaned ZERO saves.** Email/Google were ACCESS-only and bound nothing,
  so an email player's save key already WAS their `guest-local-<sha256(deviceId)>` device hash — and still
  is. Nothing on the save/backend path was touched by this implementation.

## 2. Read-first — current state (sourced 2026-08-02 — SEE §1b, partly false)
- The "SIGN IN" screen = `LoginPanelController` → `LoginViewModel` → `FirebaseAuthService.SignInWithEmailAndPasswordAsync`
  (`FirebaseAuthService.cs:176`). On success it binds the Firebase UID as the save key via
  `GameStateService.BindWallet(uid)` (`LoginViewModel.cs:110`) and attaches a Firebase **ID token** as
  `Authorization: Bearer` to `/api/game/save` (Neon) (`FirebaseAuthService.cs:210-219`; `GameStateService.cs:1314`).
- **"Connect Wallet" is currently a PREVIEW NO-OP** — the SKR/skin surface (`SkrShowcasePanel.cs:204`,
  `WalletSkinBootstrap.cs:71`), NOT a real connect. Real connect = WO-766 (Solana Mobile Wallet Adapter).
- All identity rails already converge on ONE save key: `GameState.BoundWallet` (the Neon `playerId`) — Firebase UID,
  Google, Pi (`PiSignInController.cs:182`), SKR skin all call `BindWallet(...)`. Field name is legacy wallet-era.
- Guest = `guest-local-<sha256(deviceId)>` minted on load (`GameStateService.cs:948-955`); real login overwrites via
  `BindWallet` (`:947`). Guest is the always-works escape (panel has no Close).
- The **"An internal error has occurred"** = Firebase's raw `CONFIGURATION_NOT_FOUND` passed through unmapped
  (`FirebaseAuthService.Explain` fallthrough `:228/245`) — a live Firebase project/config mismatch. **Moot once email
  login is removed** (below), but noted.

## 3. The build (phased — sequencing is mandatory, §Sequencing)

### Phase 1 — wire REAL wallet-connect as identity (prerequisite; = WO-766)
- Replace the preview no-op with a real connect: Solana Mobile **Wallet Adapter** on Seeker/Android; a browser wallet
  (Phantom/Backpack) path on WebGL if web keeps a wallet option. On connect, `BindWallet(<real pubkey>)` — the pubkey
  becomes the durable `BoundWallet` save key (reverting WO-769's UID-in-`BoundWallet`).
- Sign a nonce/challenge so the backend can verify ownership (feeds Phase 3).

### Phase 2 — make wallet the boot login gate; remove email/Firebase login
- Boot flow: show **Connect Wallet** (primary) + **Play as Guest** (local escape). REMOVE the email/password +
  "Create Account" UI from `LoginPanelController` and retire `FirebaseAuthService` as the identity path.
- Keep guest → wallet **bind-later** (a guest who connects a wallet migrates their local save to the pubkey key).
- Retire Firebase Auth wiring (SDK can stay in-tree but is no longer the login; or remove per CLI's call). The
  `CONFIGURATION_NOT_FOUND` error disappears with the screen.

### Phase 3 — backend save-auth: Firebase token → wallet signature
> **Note (canon-corrected 2026-08-02):** the backend `api/` is **git-tracked IN THIS REPO** (not a separate project),
> and wallet-signature scaffolding **already exists** — `api/auth/nonce.js` + `api/_lib/wallet-auth.js` + `api/game/save.js`.
> So this phase EXTENDS existing in-repo code, not a greenfield service. Read those first.
- `/api/game/save` currently verifies a Firebase Bearer ID token. Change it to verify a **wallet signature**
  (the Phase 1 signed nonce) keyed to the pubkey, OR drop token-verify to the wallet-owned key model. Update
  `TryAttachAuthHeaders` (`GameStateService.cs:1314`) to send the wallet proof instead of a Firebase token.
- `BoundWallet` = the real pubkey again (canonical `playerId`).

## 4. Sequencing (do NOT skip — avoids a no-login state)
Real wallet-connect (Phase 1) MUST land and verify BEFORE email is removed (Phase 2). If email is removed first,
the only working login is guest/local (real connect is a no-op today). Recommend gating the email removal behind a
flag until Phase 1 is proven on-device (Seeker APK), so there is never a build with no working cloud login.

## 5. OWNER CONFIRM (defaults; veto — some are consequential)
1. **Non-wallet web/Pi players:** guest/local-only (no cloud save) unless they connect a browser wallet (default) —
   vs. add a browser-wallet path for web cloud save. Default = guest-only for now (Seeker-first strategy).
2. **Firebase:** retire the Auth SDK from the tree entirely (default = leave it dormant, remove the login UI only, so
   it's reversible) — vs. rip it out.
3. **Backend timing:** ship Phase 3 (wallet-signature verify) with Phase 2, or keep saves unauthenticated-by-pubkey
   short-term? Default = Phase 3 with Phase 2 (don't ship an unverified save endpoint).

## 6. Files (indicative — CLI to confirm)
- `Assets/_Modules/Onboarding/LoginPanelController.cs` / `LoginViewModel.cs` — remove email/create-account UI; wallet+guest only.
- `Assets/_Modules/.../WalletSkinBootstrap.cs` / `SkrShowcasePanel.cs` / the WO-766 connect — real connect → `BindWallet(pubkey)`.
- `Assets/_Modules/Core/State/GameStateService.cs` — `BindWallet`/`TryAttachAuthHeaders` (wallet proof not Firebase token).
- `FirebaseAuthService.cs` — retire as identity (dormant or removed per §5.2).
- Backend `/api/game/save` — wallet-signature verify (Phase 3).

## 7. Acceptance criteria
- [ ] Boot shows Connect Wallet (primary) + Play as Guest; NO email/password/Create-Account UI.
- [ ] Real wallet-connect binds the pubkey as the save key; cloud save works keyed to the wallet (Seeker APK, on-device).
- [ ] Guest plays locally and can bind a wallet later, migrating the save.
- [ ] `/api/game/save` accepts wallet-verified saves (Phase 3); rejects unproven ones.
- [ ] The "internal error" path is gone (email login removed).
- [ ] No build ships with real-connect stubbed AND email removed (sequencing §4 honored).

## 8. Do NOT
- Do NOT remove the email login before real wallet-connect is wired + verified on-device (§4).
- Do NOT leave `/api/game/save` accepting saves without a pubkey ownership proof (§Phase 3).
- Do NOT break Play-as-Guest (the always-works escape).

---

## 9. IMPLEMENTED 2026-08-21 (Phase 2)

**Owner ruling that closed the WO-847 conflict:** *"That's only true with the Play Store, which we are not
in. We are only in the dApp Store, which is all wallet authentication based."* WO-847's Android-only caveat
existed to serve a Google Play release. There is none. Email/Firebase login is removed on **every** platform.

**Changed:**
- `Assets/_Modules/Core/Platform/LoginSurfacePlatform.cs` — `LoginSurfaceLayout` enum + `LoginSurfacePlatform`
  (and its `LayoutOverride` test seam) DELETED. There is one login surface, so a one-armed layout switch was
  removed rather than left resolving to a constant. `LoginWalletBridge` (the wallet-connect seam) is untouched.
- `Assets/_Modules/Onboarding/LoginViewModel.cs` — `SignInAsync` / `SignUpAsync` / `SendPasswordResetAsync` /
  `SignInWithGoogleAsync` / `NoteAccessGranted` / the `Google` plugin import + `WebClientId` all removed.
  What remains: `ConnectWalletAsync` (binds the pubkey) and `ContinueAsGuest`.
- `Assets/_Modules/Onboarding/LoginPanelController.cs` — `BuildEmailForm`, `OnSignIn`, `OnCreateAccount`,
  `OnGoogleSignIn`, `OnForgotPassword`, `BeginAttempt`, `MaskEmail`, `MakeInputField`, `Bounded`, `Observe`
  and the email/password/Google/forgot fields removed. `Build` always builds Connect Wallet + Play as Guest.
  **`PresentOrContinue` is now synchronous** — the boot-time `FirebaseAuthService.EnsureInitializedAsync`
  probe (a blocking, up-to-12s network await that ran *before any UI existed*) is gone, so there is no await
  at all between app start and the first screen. `ShouldContinueWithoutLogin(bool,bool,bool)` KEEPS its
  signature (LoginGateRegression drives a truth table against it by reflection); the third arg is renamed
  `legacySignedIn` and is now always passed `false`.
- `Assets/_Modules/Core/Auth/FirebaseAuthService.cs` — **kept, with a RETIRED banner. Zero callers.** Kept
  because it declares `AuthOutcome`, which the *wallet* path resolves with; deleting the file would break
  code unrelated to email. This is §5.2's stated default ("leave it dormant, remove the login UI only").
- `Assets/Editor/Regression/WalletIdentityRegression.cs` — Case 5 rewritten. It used to REQUIRE the email
  paths' `NoteAccessGranted` trace to exist (it would now fail); it asserts the stronger property instead —
  the login VM contains no non-wallet identity path at all, and `BindPlayer` has exactly one call site.
- `Assets/Tests/EditMode/LoginSurfacePlatformTests.cs` — the three tests pinning the platform split deleted
  (incl. `NoOverride_OffAndroid_ResolvesEmailForm`); replaced with whole-file source-lints that no
  email/Google/Firebase control can come back behind a `#if UNITY_ANDROID`.
- `Assets/_Modules/Onboarding/FoundingChoiceController.cs` — comment only.

**Deliberately NOT touched:** Firebase App Distribution / `distribute-android.ps1` / `firebase-appid.txt`
(how testers get the APK — a different product, §1); `google-services.json` and the Firebase SDK in-tree;
`BackendRequestSigner` and the guest rail; `/api/game/save`; `PiSignInController`; `GameStateService.BindWallet`.
Promo/referral redemption still reads `BackendRequestSigner.CurrentPlayerId()`, which is unchanged.

**Behaviour change worth calling out at felt-test:** a player who was Firebase-signed-in but has never
connected a wallet is no longer "already in" at boot, so they get the connect-or-guest surface once and tap
Play as Guest. **Their save is unaffected** — it was always keyed to the device-hash guest id (§1b).

**Note for a different ticket (not acted on here):** the owner has separately said HeroSelect has no value in
a wallet-only world. `HeroSelectController` / `PetSelectController` both route into
`FoundingChoiceController.PresentOrContinue`, so hiding HeroSelect must not orphan that entry point.

> **OWNER RULING 2026-08-21 (verbal, this session):** Owner: "I have asked you many times to remove that and get it done." Deliverable = DROP email/Firebase login, wallet-first identity. NOTE the conflict to resolve while implementing: WO-847 recorded wallet-first as ANDROID-ONLY and left Assets/_Modules/Core/Auth/FirebaseAuthService.cs live by design. The owner ask is the newer instruction and wins; whoever implements must decide what happens to non-Android surfaces rather than silently deleting the service.

> **CLI 2026-08-21:** d6123fe3a - email/Firebase removed everywhere; no save migration was needed
