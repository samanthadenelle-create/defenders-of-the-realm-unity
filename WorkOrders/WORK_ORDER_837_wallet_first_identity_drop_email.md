> ## COLLIDED NUMBER (4th two-seat collision, 2026-08-02) + SUPERSEDED
> The number 837 belongs to WORK_ORDER_837_stockpiles_cap_capacity.md (committed). This spec's content
> is SUPERSEDED by WO-847 (wallet-first Android login, IMPLEMENTED) + WO-766 (real connect). Do not implement.

# WORK ORDER 837 — Wallet-first identity (drop email/Firebase login)

**Status:** READY TO IMPLEMENT (owner decision 2026-08-02 — **REVERSES WO-769**)
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

## 2. Read-first — current state (sourced 2026-08-02)
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
