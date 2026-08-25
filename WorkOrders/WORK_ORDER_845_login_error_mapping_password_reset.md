# WORK ORDER 845 — Login: honest sign-in errors on desktop + password recovery

**Status:** FIXED 2026-08-02 (`731840e78`) — awaiting owner felt-verify. *(Status audit 2026-08-24: BUCKET CORRECTION — the prior line predated the commit and still advertised gates/commit as owed; verified at source in `git log`, `731840e78` (2026-08-02) landed this work. Body unchanged. Prior line: IMPLEMENTED — pending gates (CompileGate + EditMode run by the CLI committer))*
**Author:** edit-only implementation agent (proven RCA supplied by orchestrator)
**Lane:** Onboarding/Auth — `FirebaseAuthService.cs` + `LoginViewModel.cs` + `LoginPanelController.cs` + new `AuthErrorMessagesTests.cs`.
**Origin:** owner F8 seq 623 (desktop build) — *SignIn on an EXISTING account fails as "An internal error has occurred"*; no password-recovery path anywhere on the login panel.

---

## 1. RCA — proven (F8 + REST probe evidence, file:line)

- **Backend is clean.** A direct REST probe of the Firebase identity endpoint with bad
  credentials returned `INVALID_LOGIN_CREDENTIALS` (provider enabled, project answering
  correctly). The failure is client-side translation, not auth infrastructure.
- **The desktop SDK wraps credential failures as an internal error.** The Firebase C++
  desktop core reports a credential failure as a `FirebaseException` whose `ErrorCode`
  maps to `AuthError.Failure` and whose message is the bare *"An internal error has
  occurred."* wrapper — the REAL REST marker (`INVALID_LOGIN_CREDENTIALS`) rides only in
  the exception message TEXT (sometimes on an inner exception), never as a mappable enum
  code.
- **Pre-fix `Explain` could not see it.** `Assets/_Modules/Core/Auth/FirebaseAuthService.cs`
  (pre-fix :223-246) unwrapped exactly ONE `AggregateException` layer and then switched on
  `(AuthError)fb.ErrorCode` over six codes (`EmailAlreadyInUse`, `WrongPassword`,
  `InvalidEmail`, `WeakPassword`, `UserNotFound`, `OperationNotAllowed`). `AuthError.Failure`
  matched nothing, so `msg = ex.Message` fell through and the player read the wrapper
  verbatim. Session evidence is consistent: Sign**Up** failures mapped fine ("Password is
  too weak", "That email is already registered" — those DO arrive as real enum codes on
  desktop) while Sign**In** produced the generic internal error twice.
- **No recovery path existed.** The service had no password-reset API; the panel
  (WO-787 layout) had Sign In / Create Account / (Android-only Google) / Play as Guest and
  nothing else — a forgotten password dead-ended at the mis-labelled internal error.

## 2. Fix shape

### A. Explain rework — enum first, RAW-text markers second, honest fallback last

`Assets/_Modules/Core/Auth/FirebaseAuthService.cs`:

- **New PURE `AuthErrorMessages` static class** (same file, OUTSIDE the platform `#if` —
  no Firebase types, so it compiles on the WebGL stub build too and is EditMode-testable
  without the Firebase DLLs). It owns:
  - the single player-string vocabulary (`CredentialMismatch = "Email or password is
    incorrect."`, `UserNotFound = "No account for that email."`, plus in-use / weak /
    invalid-email / disabled / too-many / network / provider-disabled / `RetryHint`);
  - `Unwrap(e)` — flattens nested `AggregateException` layers + walks the inner chain to
    the leaf (depth-capped);
  - `JoinMessages(e)` — the whole chain's text (markers hide at any level);
  - `FromMarkers(raw)` — case-insensitive REST-marker scan: `INVALID_LOGIN_CREDENTIALS` /
    `INVALID_PASSWORD` / `WRONG_PASSWORD` / `INVALID_CREDENTIAL` -> `CredentialMismatch`
    (checked FIRST — enumeration-protected projects answer `INVALID_LOGIN_CREDENTIALS`
    for both wrong-password and unknown-email, so one honest string covers both);
    `EMAIL_NOT_FOUND`/`USER_NOT_FOUND` -> `UserNotFound`; plus `EMAIL_EXISTS`,
    `WEAK_PASSWORD`, `INVALID_EMAIL`, `USER_DISABLED`, `TOO_MANY_ATTEMPTS`,
    `NETWORK_REQUEST_FAILED`, `OPERATION_NOT_ALLOWED`. Null when no marker (caller falls
    back);
  - `Fallback(raw)` — the raw message + `" Please check your details and try again."` —
    the generic path keeps the truth but never dead-ends the player.
- **`Explain(Exception, string who)`** now: full-chain unwrap -> find the
  `FirebaseException` ANYWHERE in the chain (not only the leaf) -> extended enum switch
  (adds `InvalidCredential` -> `CredentialMismatch`, `WrongPassword` unified into the same
  string, plus `UserDisabled` / `TooManyRequests` / `NetworkRequestFailed`) -> when the
  enum says nothing, `FromMarkers(JoinMessages(e))` — **this is the branch that catches
  the F8 seq 623 shape** -> `Fallback` with the retry hint.
- **SS12 self-identification:** one `FlowTrace.Warn("Auth", "auth failed raw=<innerType>
  code=<AuthError> who=<masked-email> msg=<leaf message>")` per failure — the next mystery
  names itself in one captured line. `who` is `Mask(email)`'d at every call site; the
  password never appears (not passed, and Firebase exception text never contains it).

### B. Password recovery — service API -> VM command -> panel control

- **Service:** `SendPasswordResetEmailAsync(string email)` — `EnsureInitializedAsync`
  gate, `_auth.SendPasswordResetEmailAsync(email)`, FlowTrace Step in/out (masked email),
  failures through the same `Explain` vocabulary. **Byte-identical stub** added to the
  WebGL branch (returns the existing "guest only" unavailable outcome) so no caller ever
  needs a platform guard.
- **VM (`LoginViewModel.cs`):** `SendPasswordResetAsync(email)` — pure pass-through
  command (+ FlowTrace Step); nothing to bind on success (recovery completes out-of-band).
  MVVM kept: the View owns zero auth logic.
- **Panel (`LoginPanelController.cs`):** new "Forgot password?" Obsidian button (kit
  style Style1/Gray, ASCII label, ClampMinTouch applied by `BuildObsidianButton` like
  every sibling). Uses the email field's current text. Honest statuses for every branch:
  "Enter your email first." (empty field), "Sending reset email..." (busy),
  "Reset email sent to s***@hp.com. Check your inbox." (masked, info tone),
  or the Explain-mapped failure (danger tone). Wired into `SetBusy` with the other four
  controls.

### C. Panel geometry — WO-787 preserved exactly (verified before placement)

The WO-787 unstacked layout is at MinTouch capacity on the shortest live canvas: the
googleRow (Android) variant's button centers sit 0.135 apart against a ~0.131 clamp
floor (112 ref px over the ~853 px body), so a NEW stacked row cannot fit on Android and
would crowd desktop. Smallest-change placement: **"Forgot password?" splits the bottom
band with "Play as Guest"** — Guest x `0.08-0.55`, Forgot x `0.58-0.92`, y fractions
UNCHANGED from the shipped layout (`0.07-0.15` googleRow / `0.12-0.21` desktop). Vertical
clamp-growth profile is byte-identical to WO-787's, so zero new collision risk; both
halves stay far above the 112 px touch floor horizontally on any landscape canvas.
Guest path itself untouched.

## 3. Files touched

- `Assets/_Modules/Core/Auth/FirebaseAuthService.cs` — pure `AuthErrorMessages` class;
  `Explain` rework (+ masked `who` at all 4 call sites); `SendPasswordResetEmailAsync`
  real + WebGL stub. Braces 63/63.
- `Assets/_Modules/Onboarding/LoginViewModel.cs` — `SendPasswordResetAsync` command.
  Braces 12/12.
- `Assets/_Modules/Onboarding/LoginPanelController.cs` — `_forgot` button (bottom-band
  split), `OnForgotPassword` handler, `MaskEmail` presentation helper, `SetBusy` wiring.
  Braces 25/25.
- `Assets/Tests/EditMode/AuthErrorMessagesTests.cs` (+ .meta) — NEW. Braces 18/18.

## 4. Regression added

`AuthErrorMessagesTests.cs` (EditMode, pure — no Firebase DLL refs needed):

- `Unwrap` reaches the leaf through nested aggregates + plain inner chains; pass-through.
- **The exact F8 seq 623 shape:** internal-error wrapper text carrying
  `INVALID_LOGIN_CREDENTIALS` -> `CredentialMismatch`; all credential markers (incl.
  lowercase) unify; `EMAIL_NOT_FOUND`/`USER_NOT_FOUND` -> the honest user-not-found
  string; other markers -> their strings; no marker -> null (fallback proceeds).
- Marker buried on an INNER exception still maps via `JoinMessages`.
- `Fallback` keeps the raw message and appends the retry hint (incl. empty-message case).
- **Source-lint** (the Firebase-typed halves this asmdef can't compile against):
  `SendPasswordResetEmailAsync(string email)` declared >= 2x (stub + real, byte-identical
  API) + the real `_auth.SendPasswordResetEmailAsync(email)` call present + `Explain`
  routes through `AuthErrorMessages.FromMarkers(AuthErrorMessages.JoinMessages(...))` and
  maps `AuthError.InvalidCredential`.

## 5. Acceptance criteria

- [ ] CompileGate green (all touched files; NUL-scan clean — verified locally, 0 NULs).
- [ ] EditMode `AuthErrorMessagesTests` green.
- [ ] Desktop felt-test (PO closes): wrong password on an existing account reads
      **"Email or password is incorrect."** — never "An internal error has occurred";
      the F8 capture shows the one `auth failed raw=... code=... who=<masked>` Warn line.
- [ ] "Forgot password?" with an email entered -> "Reset email sent to <masked>..." and
      the mail arrives; with the field empty -> "Enter your email first."; layout stays
      unstacked on desktop AND Seeker (WO-787 geometry preserved).
- [ ] Guest path unchanged.

## 6. What was NOT touched

- Guest flow, Google sign-in flow, `PresentOrContinue` boot logic, `BindPlayer`.
- `CLI_LANES_WO_NUMBERS.md`, `DataRegression.cs`, any other module, any scene file.
- No Unity runs, gates, or commits from this agent (CLI committer owns those).
