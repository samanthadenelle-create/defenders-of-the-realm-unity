// =============================================================================
// LoginViewModel (WO-769) — the state+logic seam behind LoginPanelController.
// Strict MVVM (ARCHITECTURE_PRINCIPLES §1/§2; UiMvvmConformanceRegression): the
// View is a dumb Obsidian skin that binds this VM and reads NO game state; ALL
// auth + player-binding logic lives here. VMs are pure C# (build no uGUI).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Onboarding   Namespace: DeNelle.Onboarding.
// Wraps DeNelle.Core.Auth.FirebaseAuthService (email/password) and, on success,
// binds the Firebase UID as the save player-id via GameStateService.BindWallet so
// /api/game/save (Neon) keys by that UID. Guest needs no bind — GameStateService
// mints a stable guest-local-* id on load when not signed in.
// =============================================================================
using System;
using System.Threading.Tasks;
using DeNelle.Core.Auth;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.State;
#if !UNITY_WEBGL || UNITY_EDITOR
using Google;   // GoogleSignIn is an ANDROID NATIVE plugin - absent on WebGL (see SignInWithGoogleAsync)
#endif

namespace DeNelle.Onboarding
{
    /// <summary>Auth + player-binding logic for the login screen. Pure C#, no uGUI.</summary>
    public sealed class LoginViewModel
    {
        /// <summary>Factory the View binds to (the VM-routing seam the MVVM gate looks for).</summary>
        public static LoginViewModel CreateDefault() => new LoginViewModel();

        /// <summary>Sign in with email/password; on success bind the UID as the save player-id.</summary>
        public async Task<AuthOutcome> SignInAsync(string email, string password)
        {
            AuthOutcome outcome = await FirebaseAuthService.Instance.SignInAsync(email, password);
            if (outcome.Success) BindPlayer(outcome.UserId);
            return outcome;
        }

        /// <summary>Create an account, then bind the UID as the save player-id.</summary>
        public async Task<AuthOutcome> SignUpAsync(string email, string password)
        {
            AuthOutcome outcome = await FirebaseAuthService.Instance.SignUpAsync(email, password);
            if (outcome.Success) BindPlayer(outcome.UserId);
            return outcome;
        }

        // The OAuth 2.0 WEB client id (google-services.json oauth_client type 3). Google Sign-In
        // needs this to mint an ID token Firebase will accept.
        private const string WebClientId = "264518851517-q9i3gj5dfocqme8v9vh8ria4na6avlj1.apps.googleusercontent.com";

        /// <summary>
        /// Sign in with a Google account: pops the native Google picker (GoogleSignIn plugin),
        /// gets an ID token, exchanges it for a Firebase sign-in, then binds the UID.
        /// </summary>
        public async Task<AuthOutcome> SignInWithGoogleAsync()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            // The GoogleSignIn plugin is an ANDROID NATIVE plugin and the Firebase Unity SDK
            // has no WebGL support, so neither half of this flow exists on web. Fail clearly
            // and let the caller fall through to Guest (see FirebaseAuthService's WebGL stub).
            // Web accounts, if ever wanted, are Google Identity Services in JS + Firebase REST
            // behind this same method -- not the native plugin.
            FlowTrace.Step("Auth", "WebGL build - Google sign-in is unavailable; guest identity only.");
            await Task.CompletedTask;
            return AuthOutcome.Fail("Google sign-in isn't available in the web build - continuing as a guest.");
#else
            string idToken;
            try
            {
                GoogleSignIn.Configuration = new GoogleSignInConfiguration
                {
                    WebClientId = WebClientId,
                    RequestIdToken = true,
                    RequestEmail = true,
                    UseGameSignIn = false,
                };
                GoogleSignInUser user = await GoogleSignIn.DefaultInstance.SignIn();
                idToken = user != null ? user.IdToken : null;
            }
            catch (Exception e)
            {
                FlowTrace.Warn("Auth", "Google sign-in cancelled/failed: " + e.Message);
                return AuthOutcome.Fail("Google sign-in was cancelled.");
            }
            if (string.IsNullOrEmpty(idToken)) return AuthOutcome.Fail("Google returned no ID token.");
            AuthOutcome outcome = await FirebaseAuthService.Instance.SignInWithGoogleCredentialAsync(idToken);
            if (outcome.Success) BindPlayer(outcome.UserId);
            return outcome;
#endif
        }

        /// <summary>
        /// Play without an account: nothing to bind — GameStateService.EnsureAccount already
        /// mints a stable device-hash guest identity on load when not signed in.
        /// </summary>
        public void ContinueAsGuest()
        {
            FlowTrace.Step("Auth", "guest continue — using the device-hash guest identity (no bind).");
        }

        // Key the save to the Firebase UID (Guarded — one bad op logs + is skipped, never a soft-lock).
        private static void BindPlayer(string uid)
        {
            var svc = GameStateService.Instance;
            if (svc == null) { FlowTrace.Warn("Auth", "BindPlayer: GameStateService null — save not re-keyed."); return; }
            Guard.Try("Auth", "BindWallet(firebase uid)", () => svc.BindWallet(uid));
        }
    }
}
