// =============================================================================
// FirebaseAuthService (WO-769) — email/password/Google identity: ACCESS to the game.
// -----------------------------------------------------------------------------
// CORRECTED 2026-08-02 (security audit). This header used to claim: "Firebase issues
// a verified ID token; GameStateService attaches it as a Bearer header to
// /api/game/save, which keys the save by the Firebase UID." EVERY CLAUSE OF THAT WAS
// FALSE, and believing it is what produced a real P0: nothing ever calls
// GetIdTokenAsync (zero call sites), no Bearer header is built anywhere, and the
// backend's save-auth is a WALLET-signed nonce challenge (X-Wallet / X-Nonce /
// X-Signature — see GameStateService.TryAttachAuthHeaders + api/_lib/wallet-auth.js).
// A Firebase UID cannot even pass api/_lib/wallet-auth.js's base58 WALLET_RE check.
//
// The actual contract, and the owner ruling it implements:
//   FIREBASE = ACCESS  — sign-in/distribution. This service. Binds NO save key.
//   WALLET   = DATA    — the connected Solana address is the cloud save key
//                        (GameStateService.BindWallet attested overload).
// If a Bearer/ID-token scheme is ever built, write it here THEN — do not describe it
// in advance; a comment that describes an unbuilt scheme reads as a spec and gets
// "implemented" against by the next author.
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

    /// <summary>
    /// PURE auth-failure string vocabulary + unwrap/marker mapping (WO-845). No Firebase
    /// types — compiles on every platform (including the WebGL stub build) and is
    /// EditMode-testable without the Firebase DLLs (the test asmdef overrideReferences
    /// them out). Why it exists: on DESKTOP the Firebase C++ core reports credential
    /// failures as a bare "An internal error has occurred." (AuthError.Failure) with the
    /// REAL REST error marker (e.g. INVALID_LOGIN_CREDENTIALS) buried in the exception
    /// message text — the AuthError enum switch alone can never map it, so sign-in on a
    /// wrong password read as an internal error (owner F8 seq 623; the REST probe proved
    /// the backend answered INVALID_LOGIN_CREDENTIALS the whole time).
    /// </summary>
    public static class AuthErrorMessages
    {
        // Player-facing strings (ASCII only). One vocabulary for enum-mapped AND
        // marker-mapped failures so both paths read identically to the player.
        public const string CredentialMismatch = "Email or password is incorrect.";
        public const string UserNotFound      = "No account for that email.";
        public const string EmailInUse        = "That email is already registered.";
        public const string WeakPassword      = "Password is too weak (6+ characters).";
        public const string InvalidEmail      = "That email address isn't valid.";
        public const string UserDisabled      = "This account has been disabled.";
        public const string TooManyAttempts   = "Too many attempts. Wait a moment and try again.";
        public const string NetworkError      = "Network error. Check your connection and try again.";
        public const string ProviderDisabled  = "Email/password sign-in isn't enabled for this project (enable it in the Firebase console).";
        public const string RetryHint         = " Please check your details and try again.";

        /// <summary>Innermost real exception: flattens AggregateException layers and walks
        /// the InnerException chain (depth-capped so a malformed chain can never loop).</summary>
        public static Exception Unwrap(Exception e)
        {
            var ex = e;
            for (int depth = 0; ex != null && depth < 8; depth++)
            {
                if (ex is AggregateException agg)
                {
                    var flat = agg.Flatten();
                    if (flat.InnerExceptions.Count == 0) break;
                    ex = flat.InnerExceptions[0];
                    continue;
                }
                if (ex.InnerException == null) break;
                ex = ex.InnerException;
            }
            return ex ?? e;
        }

        /// <summary>Every message in the chain joined — a REST marker can hide at any level
        /// (the desktop SDK sometimes puts it on the wrapper, sometimes on the leaf).</summary>
        public static string JoinMessages(Exception e)
        {
            var sb = new System.Text.StringBuilder();
            var ex = e;
            for (int depth = 0; ex != null && depth < 8; depth++)
            {
                sb.Append(ex.Message).Append(' ');
                if (ex is AggregateException agg && agg.InnerExceptions.Count > 0)
                    ex = agg.InnerExceptions[0];
                else
                    ex = ex.InnerException;
            }
            return sb.ToString();
        }

        /// <summary>Map raw exception text to a player string via the Firebase REST error
        /// markers embedded in it. Returns null when no marker is present (caller falls
        /// back). Credential markers are checked FIRST — INVALID_LOGIN_CREDENTIALS is what
        /// email-enumeration-protected projects answer for BOTH wrong-password and
        /// unknown-email, so the honest player string covers both.</summary>
        public static string FromMarkers(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return null;
            string t = raw.ToUpperInvariant();
            if (t.Contains("INVALID_LOGIN_CREDENTIALS") || t.Contains("INVALID_PASSWORD") ||
                t.Contains("WRONG_PASSWORD") || t.Contains("INVALID_CREDENTIAL"))
                return CredentialMismatch;
            if (t.Contains("EMAIL_NOT_FOUND") || t.Contains("USER_NOT_FOUND")) return UserNotFound;
            if (t.Contains("EMAIL_EXISTS"))          return EmailInUse;
            if (t.Contains("WEAK_PASSWORD"))         return WeakPassword;
            if (t.Contains("INVALID_EMAIL") || t.Contains("MISSING_EMAIL")) return InvalidEmail;
            if (t.Contains("USER_DISABLED"))         return UserDisabled;
            if (t.Contains("TOO_MANY_ATTEMPTS"))     return TooManyAttempts;
            if (t.Contains("NETWORK_REQUEST_FAILED")) return NetworkError;
            if (t.Contains("OPERATION_NOT_ALLOWED")) return ProviderDisabled;
            return null;
        }

        /// <summary>The unmapped fallback: the raw message plus a short retry hint — never a
        /// bare "An internal error has occurred" with no next step for the player.</summary>
        public static string Fallback(string rawMessage)
        {
            string msg = string.IsNullOrEmpty(rawMessage) ? "Sign-in failed." : rawMessage.Trim();
            if (!msg.EndsWith(".")) msg += ".";
            return msg + RetryHint;
        }
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

        public Task<AuthOutcome> SendPasswordResetEmailAsync(string email)
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
            catch (Exception e) { return AuthOutcome.Fail(Explain(e, Mask(email))); }
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
            catch (Exception e) { return AuthOutcome.Fail(Explain(e, Mask(email))); }
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
            catch (Exception e) { return AuthOutcome.Fail(Explain(e, "(google)")); }
        }

        /// <summary>
        /// Email a password-reset link (WO-845 — the login panel's "Forgot password?").
        /// Success means Firebase ACCEPTED the send; the mail arrives out-of-band.
        /// Failures map through the same Explain vocabulary as sign-in.
        /// </summary>
        public async Task<AuthOutcome> SendPasswordResetEmailAsync(string email)
        {
            if (!await EnsureInitializedAsync()) return AuthOutcome.Fail("auth not initialized");
            try
            {
                FlowTrace.Step("Auth", $"PasswordReset {Mask(email)}");
                await _auth.SendPasswordResetEmailAsync(email);
                FlowTrace.Step("Auth", $"PasswordReset accepted for {Mask(email)}");
                return new AuthOutcome { Success = true, Email = email, Error = string.Empty };
            }
            catch (Exception e) { return AuthOutcome.Fail(Explain(e, Mask(email))); }
        }

        public void SignOut()
        {
            if (_auth == null) return;
            FlowTrace.Step("Auth", "SignOut");
            _auth.SignOut();
        }

        /// <summary>
        /// The current user's Firebase ID token (JWT). Null when signed out; forceRefresh
        /// re-mints if near expiry.
        /// <para>
        /// UNUSED as of 2026-08-02 — ZERO call sites. The backend does not accept a Bearer
        /// token; /api/game/save authenticates a WALLET-signed nonce. Keep this method (it
        /// is the obvious hook if a Firebase-verified rail is ever built server-side), but
        /// do not describe that rail as if it exists — the previous version of this comment
        /// did, and the next author implemented against the description.
        /// </para>
        /// </summary>
        public async Task<string> GetIdTokenAsync(bool forceRefresh = false)
        {
            if (!IsSignedIn) return null;
            try { return await _auth.CurrentUser.TokenAsync(forceRefresh); }
            catch (Exception e) { FlowTrace.Warn("Auth", $"token fetch failed: {e.Message}"); return null; }
        }

        // ── helpers ─────────────────────────────────────────────────────────
        // The SDK wraps failures in AggregateException layers; a FirebaseException (when
        // present) carries an AuthError code — but the DESKTOP C++ core reports credential
        // failures as a bare "An internal error has occurred." (code = Failure) with the real
        // REST marker (INVALID_LOGIN_CREDENTIALS et al.) only in the message TEXT. So: map
        // the enum FIRST, the raw-text markers SECOND, and only then fall back generically
        // with a retry hint (WO-845; owner F8 seq 623 — SignIn on an existing account read
        // "internal error" while the REST probe proved INVALID_LOGIN_CREDENTIALS).
        private static string Explain(Exception e, string who = null)
        {
            Exception leaf = AuthErrorMessages.Unwrap(e);

            // Find the FirebaseException anywhere in the chain (it is not always the leaf).
            FirebaseException fb = null;
            for (var ex = e; ex != null && fb == null; ex = ex.InnerException)
            {
                fb = ex as FirebaseException;
                if (fb == null && ex is AggregateException agg)
                    foreach (var inner in agg.Flatten().InnerExceptions)
                    {
                        fb = inner as FirebaseException;
                        if (fb != null) break;
                    }
            }

            // RAW self-identifying line (SS12): inner type + AuthError code + masked email only.
            // Firebase exception messages never contain the password; 'who' is Mask()'d upstream.
            string code = fb != null ? ((AuthError)fb.ErrorCode).ToString() : "-";
            FlowTrace.Warn("Auth",
                $"auth failed raw={leaf.GetType().Name} code={code} who={who ?? "-"} msg={leaf.Message}");

            string msg = null;
            if (fb != null)
            {
                switch ((AuthError)fb.ErrorCode)
                {
                    case AuthError.EmailAlreadyInUse:    msg = AuthErrorMessages.EmailInUse; break;
                    case AuthError.WrongPassword:
                    case AuthError.InvalidCredential:    msg = AuthErrorMessages.CredentialMismatch; break;
                    case AuthError.InvalidEmail:         msg = AuthErrorMessages.InvalidEmail; break;
                    case AuthError.WeakPassword:         msg = AuthErrorMessages.WeakPassword; break;
                    case AuthError.UserNotFound:         msg = AuthErrorMessages.UserNotFound; break;
                    case AuthError.UserDisabled:         msg = AuthErrorMessages.UserDisabled; break;
                    case AuthError.TooManyRequests:      msg = AuthErrorMessages.TooManyAttempts; break;
                    case AuthError.NetworkRequestFailed: msg = AuthErrorMessages.NetworkError; break;
                    case AuthError.OperationNotAllowed:  msg = AuthErrorMessages.ProviderDisabled; break;
                }
            }
            // Desktop internal-error wrapper: the enum said nothing usable — scan the raw text
            // of the WHOLE chain for the REST markers before giving up.
            if (msg == null) msg = AuthErrorMessages.FromMarkers(AuthErrorMessages.JoinMessages(e));
            if (msg == null) msg = AuthErrorMessages.Fallback(leaf.Message);
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
