# WORK ORDER 769 — Firebase Auth (email/password) in front of the Neon backend

**Status:** SPEC — READY (owner 2026-07-24). Gated on external steps (§Gates). Architecture in memory `firebase-auth-neon-architecture`.
**Lane:** Auth / Backend / Unity. Scope: **MODERATE** (SDK import + Unity auth layer + Neon token-verify server change).

---

## 0. Architecture (owner-decided)
- **Firebase Auth (email/password) = identity/login.** Player signs up/in → Firebase **ID token** + UID.
- **Neon (Postgres) via `/api/game/save` = saves — KEPT.** The API verifies the Firebase ID token and keys saves by **Firebase UID** (replaces/joins wallet-signed auth WO-121). `SaveUrl` = `GameStateService.cs:913`.
- **Firestore = secondary** (us-central1), not the save store.
- **Wallet = payments only.**

## 1. Done (headless, 2026-07-24/25)
- Android app registered: App ID `1:264518851517:android:8e193b012cba6986d050d4`, package `com.denellestudios.echoesofelarion`, project `defenders-of-the-realm-echos` (264518851517).
- `Assets/google-services.json` in place (gitignored).
- **Firebase Unity SDK 13.14.0 imported headlessly** (reconstructed FirebaseAuth.unitypackage → `Assets/Firebase`; App+Auth+Platform DLLs, EDM4U, Android m2repository; binaries git-LFS-tracked). EDM4U generated `FirebaseApp.androidlib/google-services.xml` from the config. Commit `0e21fe6b`.
- **`FirebaseAuthService`** (`Assets/_Modules/Core/Auth/`, DeNelle.Core): EnsureInitializedAsync + SignUp/SignIn/SignOut + GetIdTokenAsync, FlowTrace-instrumented. Compiles green vs the SDK.
- **`LoginPanelController`** (View) + **`LoginViewModel`** (MVVM logic) in DeNelle.Onboarding: Obsidian modal (email + masked password + Sign In + Create Account + **Play-as-Guest**), PanelManager-registered, UI-MVVM + Obsidian conformance GREEN. Commit `1f789013`.
- **Boot wired**: `FoundingChoiceController.PresentOrContinue` gates login-or-guest first at the new-game chokepoint (returning players not re-prompted; guest never soft-locks). Commit `0fbea11c`.
- Compile + regression green throughout.

## 2. GATES (not headless — do before the C# lands)
1. **Enable Email/Password** provider — Firebase console → Authentication → Sign-in providers → Email/Password → Enable. (No CLI/gcloud on this machine flips it.)
2. **Import the Firebase Unity SDK** — FirebaseAuth (+ Firestore if used) from firebase.google.com/download/unity, imported in the editor (adds `Assets/Firebase`, EDM4U resolves the Android deps against `google-services.json`). GATE: do NOT commit any `Firebase.*`-referencing `.cs` before this, or CompileGate breaks.
3. **Neon `/api/game/save` server change** (separate repo/Vercel project) — verify the Firebase ID token (Firebase Admin SDK / JWKS) on the request, key by UID. Client already sends auth headers via `TryAttachAuthHeaders` (`GameStateService.cs:1314`) — extend/replace the wallet-signed path with the Firebase token.
4. (Optional) enable Firestore API + `firebase firestore:databases:create "(default)" --location=us-central1`.

## 3. Unity C# to build (after gate 2)
- **`FirebaseAuthService`** (new, `Assets/_Modules/Core/Auth/`): init `Firebase.Auth.FirebaseAuth.DefaultInstance`; `SignUp(email,pw)`, `SignIn(email,pw)`, `SignOut()`, `CurrentUser`, `GetIdTokenAsync()`. Guard.Try-wrapped, FlowTrace-instrumented (§12).
- **Identity bridge:** on sign-in, call `GameStateService.BindWallet`-equivalent for Firebase UID (add `BindFirebaseUser(uid)` or reuse `playerId` seam) so saves key by UID.
- **Save-auth:** `TryAttachAuthHeaders` (`GameStateService.cs:1306-1318`) attaches `Authorization: Bearer <firebaseIdToken>` when a Firebase user is signed in (mirrors the WO-121 wallet path; `CanSignMessages`-style gate).
- **Login UI:** an Obsidian email/password sign-in/up panel (presentation layer only; logic in `FirebaseAuthService`). Wire into the boot/founding flow.
- Regression: a `FirebaseAuthRegression` proving token attach + UID keying (mock the SDK where headless can't reach it).

## 4. Acceptance
- [ ] Email/Password enabled; a Unity build lets a player sign up + sign in with email/password.
- [ ] Signed-in requests to `/api/game/save` carry the Firebase ID token; Neon verifies it + keys by UID.
- [ ] Wallet no longer the login (payments only); guest fallback still works pre-login.
- [ ] `COMPILE_GATE_OK` + `REGRESSION_OK` (SDK imported first).

## 5. Notes
- Firebase "skill" = `npx firebase-tools mcp` (MCP not connected this session; CLI used directly, logged in as samanthadenelle@gmail.com).
- Sequence: gates 1-2 → Unity auth layer → gate 3 (Neon) → test build. Don't wire code before the SDK import.

## 6. UPDATE 2026-07-25 — Google Sign-In added + all providers live
- Email/Password ENABLED (owner, console). Google Sign-In ENABLED + SHA-1/256 registered (from dotr-release.keystore) + oauth clients in google-services.json.
- App: `Sign in with Google` button wired (surgical GoogleSignIn plugin import — only Assets/GoogleSignIn, its old EDM4U/Parse skipped, Firebase's EDM4U kept; asmdef added; `SignInWithGoogleCredentialAsync` via GoogleAuthProvider). Commit 75d9fcd8, pushed.
- APK (2026-07-25 15:24, 457MB, release-signed) contains libFirebaseCppAuth + libnative-googlesignin + play-services-auth — no EDM4U/Gradle conflict.
- Test flow: new game -> login-or-guest gate -> Email/Password, Google, or Guest all work on-device.
- STILL OPEN: Neon /api/game/save should verify the Firebase ID token + key by UID (server change, separate repo).
