# WORK ORDER 847 — Login: wallet-first surface on Android/Seeker (platform-conditional)

**Status:** IMPLEMENTED — pending gates (CompileGate + EditMode run by the CLI committer)
**Author:** edit-only implementation agent (owner-ruled design supplied by orchestrator)
**Lane:** Onboarding/Auth + Wallet — `LoginPanelController.cs` + `LoginViewModel.cs` + `WalletSkinBootstrap.cs` + new Core platform seam `LoginSurfacePlatform.cs` + new `LoginSurfacePlatformTests.cs`.
**Origin:** owner ruling 2026-08-02 — on Android/Seeker the login page is **"connect wallet or play as guest"**. NO email form on that platform; desktop/web KEEP the current WO-787 email layout including the WO-845 additions (forgot-password + honest error mapping).

---

## 1. The ruling and the split

- **Android/Seeker (`Application.platform == Android`):** WALLET-FIRST layout — one big
  gold **"Connect Wallet"** primary CTA (kit law: exactly ONE gold button on the surface),
  **"Play as Guest"** secondary, and one caption line:
  *"Your wallet is your save. Guest progress stays on this device until you connect."*
  No email/password fields, no Create Account, no Google row, no forgot-password
  (the wallet is its own recovery).
- **Desktop / WebGL:** the WO-787/845 email layout **byte-identical** — Sign In /
  Create Account / (Android-editor Google) / Play as Guest / Forgot password?, same
  fractions, same clamp-growth profile, same error mapping.
- The split is resolved at panel build time through a **testable platform seam**, never
  an inline platform check in the View.

## 2. Fix shape

### A. New Core platform seam — `Assets/_Modules/Core/Platform/LoginSurfacePlatform.cs`

- `enum LoginSurfaceLayout { EmailForm, WalletFirst }`.
- `LoginSurfacePlatform.Resolve()` — Android -> `WalletFirst`, everything else ->
  `EmailForm`; `LayoutOverride` (nullable static) is the EditMode/headless-capture seam
  that wins over the platform check, so either layout is testable without an Android build.
- `LoginWalletBridge` — the Core-side connect seam. Assembly direction is Wallet -> Core
  (Onboarding references Core only; Core can never reference DeNelle.Wallet), so the
  bridge holds a `Func<Task<AuthOutcome>> ConnectHandler` that DeNelle.Wallet registers at
  boot. `ConnectAsync()` is honest on every branch: no handler -> FlowTrace Warn + a
  mapped player failure ("Wallet connect isn't available in this build."); a throwing
  handler -> FlowTrace Fail + "Wallet connect failed. Please try again." — never a
  silent no-op.

### B. Wallet side — `WalletSkinBootstrap.cs` (WO-603/766 file)

- **Skin-conditional finding (verified in source):** the pre-existing connect path only
  worked via `CurrencySkinResolver.WalletConnectRequested`, whose subscriber installs
  **only under the SKR skin** (`Install()` early-returns on `!IsSkr`) and whose
  `BindWallet` call is **gated behind `skin.BindIdentityOnAuth`** (`WalletSkinBootstrap`
  :39/:66-71 pre-edit). On a live Android APK the resolver does force `skr` (owner
  2026-07-30 gate) whose default is `bindIdentityOnAuth:true` — but in the editor
  (skin.json `active:"wallet"`) the handler never installs at all, and one skin.json flip
  would silently unbind the LOGIN path. Per the design ruling, the login path must not
  depend on that optional config.
- **Fix:** `Install()` now registers `LoginWalletBridge.ConnectHandler =
  ConnectForLoginAsync` **unconditionally, BEFORE the skin gate**; the SKR-only
  event subscription (corner-button path) is untouched below it — zero Pi/skin
  regression.
- **New `ConnectForLoginAsync()`** — the same stack as the corner path (shared
  `_connecting` re-entrancy guard, same lazily-built `WalletService` -> `Connect()` ->
  MWA on device / stub in editor), but the identity bind is **EXPLICIT and
  unconditional**: `Guard.Try(... svc.BindWallet(account.Address))` (the :71 precedent),
  never consulting `BindIdentityOnAuth`. Resolves the email-path `AuthOutcome` shape
  (`UserId` = wallet address; cancel -> `Fail("Connect cancelled.")`).

### C. VM — `LoginViewModel.cs` (MVVM kept; WO-845 edits preserved)

- `Layout => LoginSurfacePlatform.Resolve()` — the View reads the VM, not the platform.
- `ConnectWalletAsync()` — awaits `LoginWalletBridge.ConnectAsync()`; on success also
  runs the same idempotent `BindPlayer(outcome.UserId)` the email paths use
  (`BindWallet` early-outs on an unchanged key), so a success can never continue
  unbound even if a future handler forgets. No duplicated connect logic anywhere.

### D. Panel — `LoginPanelController.cs` (WO-845 edits preserved verbatim)

- `Build()` branches once on `_vm.Layout`: `BuildWalletFirst(body)` or
  `BuildEmailForm(body)`. **`BuildEmailForm` is the shipped WO-787/845 layout moved
  verbatim** (googleRow `#if`, fractions, forgot-password bottom-band split — zero
  geometry change).
- `BuildWalletFirst`: caption (y 0.78-0.92), status line (0.62-0.68), **Connect Wallet**
  gold CTA (`ObsidianButtonStyle.Style1` + `ObsidianButtonColor.Yellow` — the
  `ButtonKind.Gold` mapping; dark-ink label per kit) at y 0.38-0.56, **Play as Guest**
  gray at y 0.14-0.30. Two rows only; button centers 0.25 apart vs the ~0.131 MinTouch
  clamp floor from the WO-787 analysis — no collision risk at any live canvas height;
  `BuildObsidianButton` applies `ClampMinTouch` (112 ref px) as everywhere else.
  Text-first (colorblind-safe): every control is labelled; color never carries meaning.
- `OnConnectWallet()`: busy-guard -> "Opening your wallet..." -> `_vm.ConnectWalletAsync()`
  -> `HandleOutcome(outcome)` — **the exact continuation the email paths fire**
  (`HandleOutcome` -> `Continue()` -> the `_onContinue` callback), so downstream flow
  does not care which identity bound. Failures paint the mapped message
  ("Connect cancelled." / bridge messages) in the danger tone and unlock the form.
- Guest = the existing `OnPlayAsGuest` verbatim. `_connectWallet` wired into `SetBusy`
  with the other controls.

## 3. Files touched

- `Assets/_Modules/Core/Platform/LoginSurfacePlatform.cs` (+ .meta) — NEW seam + bridge.
  Braces 10/10. ASCII-clean.
- `Assets/_Modules/Wallet/WalletSkinBootstrap.cs` — unconditional login-handler
  registration + `ConnectForLoginAsync` (explicit bind). Braces 25/25.
- `Assets/_Modules/Onboarding/LoginViewModel.cs` — `Layout` + `ConnectWalletAsync`.
  Braces 13/13.
- `Assets/_Modules/Onboarding/LoginPanelController.cs` — layout branch,
  `BuildWalletFirst`, `BuildEmailForm` extraction (verbatim), `OnConnectWallet`,
  `SetBusy` wiring. Braces 28/28.
- `Assets/Tests/EditMode/LoginSurfacePlatformTests.cs` (+ .meta) — NEW. Braces 21/21.
  ASCII-clean.

## 4. Regression added — `LoginSurfacePlatformTests.cs` (EditMode)

- **Seam, live:** override WalletFirst -> WalletFirst; override EmailForm -> EmailForm;
  no override off-Android (editor) -> EmailForm. Override cleared in `[TearDown]`.
- **Bridge, live:** no handler -> failed outcome with a player-facing message (never
  silent); a registered handler's outcome passes through (`UserId` = address); a
  throwing handler maps to an honest failure. Handler saved/restored around each test.
- **Source-lint** (Onboarding/Wallet halves this asmdef does not reference — WO-845
  precedent): panel branches on `LoginSurfaceLayout.WalletFirst`; the `BuildWalletFirst`
  block contains "Connect Wallet" + `ObsidianButtonColor.Yellow` + "Play as Guest" and
  contains NO `MakeInputField` / forgot-password; `BuildEmailForm` still carries the
  fields + "Forgot password?" + guest; the VM routes through
  `LoginWalletBridge.ConnectAsync()` and re-binds; `WalletSkinBootstrap` registers the
  login handler **before** the `!IsSkr` gate and `ConnectForLoginAsync` calls
  `BindWallet(` without any `BindIdentityOnAuth` gate.

## 5. Acceptance criteria / gate checklist

- [ ] CompileGate green (all touched files; NUL-scan clean — verified locally, 0 NULs).
- [ ] EditMode `LoginSurfacePlatformTests` green (and `AuthErrorMessagesTests` still green).
- [ ] **Server prerequisite (gate-checklist, NOT this WO's code): `auth_nonces` table** —
      today's live probe found it MISSING on the backend; the wallet save-auth
      challenge/nonce path needs it provisioned before connect-keyed cloud saves verify.
- [ ] **Server prerequisite: save-auth `Enforced` mode** — enforcement is not yet on;
      until flipped, a bound wallet writes saves keyed by address but the signature is
      not demanded server-side. Track with `auth_nonces` above.
- [ ] Seeker felt-test (PO closes): login shows ONLY Connect Wallet (gold) + Play as
      Guest + the caption; Connect opens the MWA wallet sheet ("Opening your wallet...");
      cancel reads "Connect cancelled."; success continues into the game exactly like a
      sign-in and the save re-keys to the wallet address (one `[Flow:Wallet] Login
      connect bound save identity...` line in the capture).
- [ ] Desktop felt-test: login layout unchanged from WO-845 (fields + forgot-password
      present); guest path unchanged on both platforms.

## 6. What was NOT touched

- The WO-787/845 email layout geometry and logic (moved verbatim, byte-preserved).
- The SKR corner-button connect path, `CurrencySkinResolver` resolution order, skin.json,
  `CurrencySkin`, Pi auth — zero skin-behaviour change.
- Guest flow, Google flow, `PresentOrContinue` boot logic, `FirebaseAuthService`.
- `CLI_LANES_WO_NUMBERS.md`, `DataRegression.cs`, `api/**`, all fenced lane files.
- No Unity runs, gates, or commits from this agent (CLI committer owns those).
