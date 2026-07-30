// =============================================================================
// FirebaseAuthService (WO-769) — email/password identity in front of the Neon
// backend. Firebase issues a verified ID token; GameStateService attaches it as
// a Bearer header to /api/game/save, which keys the save by the Firebase UID.
// -----------------------------------------------------------------------------
// Lives in DeNelle.Core (overrideReferences:false -> auto-refs Firebase.*.dll).
// Instrumented per §12 (FlowTrace) and Guard-safe. Async via the Firebase SDK's
// System.Threading.Tasks; awaits resume on Unity's main-thread SynchronizationContext.
//
// GATE (runtime, not compile): the Email/Password provider must be ENABLED in the
// Firebase console (Auth -> Sign-in providers). Until then SignIn/SignUp fail at
// runtime with "operation not allowed" — the code is correct; the toggle is owner's.
// =============================================================================
// PLATFORM GATE (2026-07-30): the Firebase Unity SDK does NOT support WebGL. Its
// plugins are enabled for Editor/Android only (Firebase.Auth.dll.meta: Any=0,
// Android=1, Editor=1), so a WebGL player build cannot resolve the Firebase types
// and the WHOLE PROJECT fails to compile. That silently broke every WebGL build
// from the moment WO-769 landed until 2026-07-30 -- nobody noticed because no
// WebGL build was attempted in between (prod is still the 07-16 pre-Firebase build).
// Below, the real implementation compiles everywhere Firebase exists; WebGL gets a
// stub with a BYTE-IDENTICAL public API so no caller needs a platform guard, and
// web players fall through to the existing guest / device-hash identity.
using System;
using System.Threading.Tasks;
using UnityEngine;
#if !UNITY_WEBGL || UNITY_EDITOR
using Firebase;
using Firebase.Auth;
#endif
using DeNelle.Core.Diagnostics;

namespace DeNelle.Core.Auth
{
    /// <summary>Result of a sign-in / sign-up attempt.</summary>
    public struct AuthOutcome
    {
        public bool Success;
        public string UserId;
        public string Email;
        public string Error;   // human-readable failure reason (empty on success)

#if !UNITY_WEBGL || UNITY_EDITOR
        public static AuthOutcome Ok(FirebaseUser u) =>
            new AuthOutcome { Success = true, UserId = u?.UserId, Email = u?.Email, Error = string.Empty };
#endif
        public static AuthOutcome Fail(string err) =>
            new AuthOutcome { Success = false, Error = string.IsNullOrEmpty(err) ? "unknown auth error" : err };
    }

#if UNITY_WEBGL && !UNITY_EDITOR
    /// <summary>
    /// WEBGL STUB. The Firebase Unity SDK has no WebGL support, so on web this type
    /// keeps the exact public surface of the real service and reports "unavailable"
    /// instead of failing the build. Callers need no platform guard: every Sign* path
    /// returns a clear AuthOutcome.Fail, IsSignedIn stays false, and the login screen
    /// falls through to the existing GUEST / device-hash identity that
    /// GameStateService already mints when no wallet or UID is bound.
    /// If web accounts are ever wanted, the route is Firebase's REST auth API over
    /// UnityWebRequest (or the JS SDK via a jslib) behind this same surface -- NOT the
    /// Unity SDK, which will never load here.
    /// </summary>
    public sealed class FirebaseAuthService
    {
        private const string Unsupported =
            "Accounts aren't available in the web build - continuing as a guest.";

        private static FirebaseAuthService _instance;
        public static FirebaseAuthService Instance => _instance ??= new FirebaseAuthService();

        public event Action<bool> AuthStateChanged;

        public bool IsReady => false;
        public bool IsSignedIn => false;
        public string UserId => null;
        public string Email => null;

        public Task<bool> EnsureInitializedAsync()
        {
            FlowTrace.Step("Auth", "WebGL build - Firebase Unity SDK is unsupported here; guest identity only.");
            AuthStateChanged?.Invoke(false);
            return Task.FromResult(false);
        }

        public Task<AuthOutcome> SignUpAsync(string email, string password)
            => Task.FromResult(AuthOutcome.Fail(Unsupported));

        public Task<AuthOutcome> SignInAsync(string email, string password)
            => Task.FromResult(AuthOutcome.Fail(Unsupported));

        public Task<AuthOutcome> SignInWithGoogleCredentialAsync(string googleIdToken)
            => Task.FromResult(AuthOutcome.Fail(Unsupported));

        public void SignOut() { }

        public Task<string> GetIdTokenAsync(bool forceRefresh = false) => Task.FromResult<string>(null);
    }
#else
    /// <summary>
    /// App-facing Firebase email/password auth. Single instance; call
    /// <see cref="EnsureInitializedAsync"/> once at boot before Sign* calls.
    /// </summary>
    public sealed class FirebaseAuthService
    {
        private static FirebaseAuthService _instance;
        public static FirebaseAuthService Instance => _instance ??= new FirebaseAuthService();

        private FirebaseAuth _auth;
        private bool _ready;

        /// <summary>Fires (true) on sign-in, (false) on sign-out. May fire off the main thread.</summary>
        public event Action<bool> AuthStateChanged;

        public bool IsReady => _ready;
        public bool IsSignedIn => _ready && _auth != null && _auth.CurrentUser != null;
        public string UserId => _auth != null && _auth.CurrentUser != null ? _auth.CurrentUser.UserId : null;
        public string Email => _auth != null && _auth.CurrentUser != null ? _auth.CurrentUser.Email : null;

        // ── init ────────────────────────────────────────────────────────────
        /// <summary>
        /// Fixes/checks Firebase dependencies (Play services etc.) then binds the
        /// default auth instance. Idempotent. Returns false (and logs) if deps are
        /// unavailable — callers must gate Sign* on a true return.
        /// </summary>
        public async Task<bool> EnsureInitializedAsync()
        {
            if (_ready) return true;
            try
            {
                FlowTrace.Step("Auth", "CheckAndFixDependenciesAsync…");
                var status = await FirebaseApp.CheckAndFixDependenciesAsync();
                if (status != DependencyStatus.Available)
                {
                    FlowTrace.Fail("Auth", $"Firebase deps unavailable: {status}");
                    return false;
                }
                _auth = FirebaseAuth.DefaultInstance;
                _auth.StateChanged += OnAuthStateChanged;
                _ready = true;
                FlowTrace.Step("Auth", $"ready (signedIn={IsSignedIn}, uid={UserId ?? "-"})");
                return true;
            }
            catch (Exception e)
            {
                FlowTrace.Fail("Auth", $"init threw: {e.GetType().Name}: {e.Message}");
                return false;
            }
        }

        private void OnAuthStateChanged(object sender, EventArgs e)
        {
            bool signedIn = IsSignedIn;
            FlowTrace.Step("Auth", $"state changed -> signedIn={signedIn} uid={UserId ?? "-"}");
            AuthStateChanged?.Invoke(signedIn);
        }

        // ── sign up / in / out ──────────────────────────────────────────────
        public async Task<AuthOutcome> SignUpAsync(string email, string password)
        {
            if (!await EnsureInitializedAsync()) return AuthOutcome.Fail("auth not initialized");
            try
            {
                FlowTrace.Step("Auth", $"SignUp {Mask(email)}");
                AuthResult result = await _auth.CreateUserWithEmailAndPasswordAsync(email, password);
                FlowTrace.Step("Auth", $"SignUp OK uid={result.User?.UserId}");
                return AuthOutcome.Ok(result.User);
            }
            catch (Exception e) { return AuthOutcome.Fail(Explain(e)); }
        }

        public async Task<AuthOutcome> SignInAsync(string email, string password)
        {
            if (!await EnsureInitializedAsync()) return AuthOutcome.Fail("auth not initialized");
            try
            {
                FlowTrace.Step("Auth", $"SignIn {Mask(email)}");
                AuthResult result = await _auth.SignInWithEmailAndPasswordAsync(email, password);
                FlowTrace.Step("Auth", $"SignIn OK uid={result.User?.UserId}");
                return AuthOutcome.Ok(result.User);
            }
            catch (Exception e) { return AuthOutcome.Fail(Explain(e)); }
        }

        /// <summary>
        /// Federated sign-in with a Google ID token (obtained by the GoogleSignIn plugin) —
        /// exchanges it for a Firebase credential and signs in, so a Google account resolves to
        /// the same Firebase UID model as email/password.
        /// </summary>
        public async Task<AuthOutcome> SignInWithGoogleCredentialAsync(string googleIdToken)
        {
            if (string.IsNullOrEmpty(googleIdToken)) return AuthOutcome.Fail("no Google ID token");
            if (!await EnsureInitializedAsync()) return AuthOutcome.Fail("auth not initialized");
            try
            {
                Credential credential = GoogleAuthProvider.GetCredential(googleIdToken, null);
                FlowTrace.Step("Auth", "Google credential -> Firebase sign-in");
                AuthResult result = await _auth.SignInAndRetrieveDataWithCredentialAsync(credential);
                FlowTrace.Step("Auth", $"Google sign-in OK uid={result.User?.UserId}");
                return AuthOutcome.Ok(result.User);
            }
            catch (Exception e) { return AuthOutcome.Fail(Explain(e)); }
        }

        public void SignOut()
        {
            if (_auth == null) return;
            FlowTrace.Step("Auth", "SignOut");
            _auth.SignOut();
        }

        /// <summary>
        /// The current user's Firebase ID token (JWT) for the Neon /api/game/save
        /// Bearer header. Null when signed out. forceRefresh re-mints if near expiry.
        /// </summary>
        public async Task<string> GetIdTokenAsync(bool forceRefresh = false)
        {
            if (!IsSignedIn) return null;
            try { return await _auth.CurrentUser.TokenAsync(forceRefresh); }
            catch (Exception e) { FlowTrace.Warn("Auth", $"token fetch failed: {e.Message}"); return null; }
        }

        // ── helpers ─────────────────────────────────────────────────────────
        // Firebase wraps failures in AggregateException(FirebaseException); surface the leaf.
        private static string Explain(Exception e)
        {
            var ex = e;
            if (ex is AggregateException agg && agg.InnerException != null) ex = agg.InnerException;
            var fb = ex as FirebaseException;
            string msg = ex.Message;
            if (fb != null)
            {
                var code = (AuthError)fb.ErrorCode;
                switch (code)
                {
                    case AuthError.EmailAlreadyInUse: msg = "That email is already registered."; break;
                    case AuthError.WrongPassword:     msg = "Incorrect password."; break;
                    case AuthError.InvalidEmail:      msg = "That email address isn't valid."; break;
                    case AuthError.WeakPassword:      msg = "Password is too weak (6+ characters)."; break;
                    case AuthError.UserNotFound:      msg = "No account for that email."; break;
                    case AuthError.OperationNotAllowed:
                        msg = "Email/password sign-in isn't enabled for this project (enable it in the Firebase console).";
                        break;
                }
            }
            FlowTrace.Warn("Auth", $"auth failed: {msg}");
            return msg;
        }

        private static string Mask(string email)
        {
            if (string.IsNullOrEmpty(email)) return "(empty)";
            int at = email.IndexOf('@');
            return at <= 1 ? "*" + (at >= 0 ? email.Substring(at) : "") : email[0] + "***" + email.Substring(at);
        }
    }
#endif
}
