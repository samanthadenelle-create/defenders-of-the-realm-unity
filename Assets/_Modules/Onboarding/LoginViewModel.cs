// =============================================================================
// LoginViewModel (WO-769) — the state+logic seam behind LoginPanelController.
// Strict MVVM (ARCHITECTURE_PRINCIPLES §1/§2; UiMvvmConformanceRegression): the
// View is a dumb Obsidian skin that binds this VM and reads NO game state; ALL
// auth + player-binding logic lives here. VMs are pure C# (build no uGUI).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Onboarding   Namespace: DeNelle.Onboarding.
// Wraps DeNelle.Core.Auth.FirebaseAuthService (email/password/Google).
//
// IDENTITY LAW (owner ruling, re-asserted by the security audit 2026-08-02):
//   FIREBASE = ACCESS   — who may open the game (distribution + login).
//   SOLANA WALLET = DATA — what keys the cloud save (BoundWallet / playerId).
// So email + Google sign-in DO NOT bind a save key. They used to bind the Firebase
// UID, which was wrong twice over: it contradicts the ruling, and a 28-char UID
// contains glyphs outside base58 and is REJECTED by api/_lib/wallet-auth.js
// (^[1-9A-HJ-NP-Za-km-z]{32,44}$) - so the moment backend auth flips to Enforced
// those players would be permanently 401'd out of their own saves.
// An email player therefore keeps the guest/device key (GameStateService mints a
// stable guest-local-* id on load) until they connect a wallet, and only
// ConnectWalletAsync - a real wallet - ever binds an identity here.
// =============================================================================
using System;
using System.Threading.Tasks;
using DeNelle.Core.Auth;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.Platform;
using DeNelle.Core.State;
#if UNITY_ANDROID || UNITY_EDITOR
// GoogleSignIn is an ANDROID NATIVE plugin. Its asmdef is scoped includePlatforms:
// [Android, Editor], so this guard must match THAT scope exactly - "everything except
// WebGL" broke the WINDOWS player build (the assembly doesn't exist there either).
using Google;
#endif

namespace DeNelle.Onboarding
{
    /// <summary>Auth + player-binding logic for the login screen. Pure C#, no uGUI.</summary>
    public sealed class LoginViewModel
    {
        /// <summary>Factory the View binds to (the VM-routing seam the MVVM gate looks for).</summary>
        public static LoginViewModel CreateDefault() => new LoginViewModel();

        /// <summary>
        /// Which login surface this platform presents (WO-847, owner ruling 2026-08-02):
        /// wallet-first ("connect wallet or play as guest") on Android/Seeker, the
        /// WO-787/845 email form everywhere else. Pure-testable via
        /// <see cref="LoginSurfacePlatform.LayoutOverride"/>.
        /// </summary>
        public LoginSurfaceLayout Layout => LoginSurfacePlatform.Resolve();

        /// <summary>
        /// Connect a wallet as the sign-in identity (WO-847, the Android wallet-first
        /// primary). Routes through the EXISTING wallet stack via LoginWalletBridge
        /// (WalletSkinBootstrap -> WalletService.Connect); the Wallet-side handler
        /// binds the connected address explicitly (never skin-gated). The idempotent
        /// re-bind here mirrors the email paths (BindWallet early-outs on an unchanged
        /// key) so a success can never continue unbound.
        /// </summary>
        public async Task<AuthOutcome> ConnectWalletAsync()
        {
            FlowTrace.Step("Auth", "wallet connect requested from the login surface.");
            AuthOutcome outcome = await LoginWalletBridge.ConnectAsync();
            if (outcome.Success && !string.IsNullOrEmpty(outcome.UserId)) BindPlayer(outcome.UserId);
            return outcome;
        }

        /// <summary>
        /// Sign in with email/password. Grants ACCESS only — it deliberately does NOT
        /// re-key the save (identity law, see the file header). The player keeps their
        /// device/guest key until they connect a wallet.
        /// </summary>
        public async Task<AuthOutcome> SignInAsync(string email, string password)
        {
            AuthOutcome outcome = await FirebaseAuthService.Instance.SignInAsync(email, password);
            if (outcome.Success) NoteAccessGranted("email sign-in");
            return outcome;
        }

        /// <summary>Create an account. ACCESS only — never binds the save key (identity law).</summary>
        public async Task<AuthOutcome> SignUpAsync(string email, string password)
        {
            AuthOutcome outcome = await FirebaseAuthService.Instance.SignUpAsync(email, password);
            if (outcome.Success) NoteAccessGranted("email account creation");
            return outcome;
        }

        /// <summary>
        /// Email a password-reset link (WO-845). Nothing to bind — success just means the
        /// send was accepted; the player finishes recovery out-of-band, then signs in.
        /// Failures arrive pre-mapped to player strings by FirebaseAuthService.Explain.
        /// </summary>
        public async Task<AuthOutcome> SendPasswordResetAsync(string email)
        {
            FlowTrace.Step("Auth", "password reset requested.");
            return await FirebaseAuthService.Instance.SendPasswordResetEmailAsync(email);
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
#if !UNITY_ANDROID && !UNITY_EDITOR
            // The GoogleSignIn plugin exists ONLY on Android (+ Editor for iteration) - its
            // asmdef is scoped to those platforms, so on WebGL AND desktop players this
            // assembly does not exist and the native half never did. Fail clearly and let the
            // caller fall through to Guest (see FirebaseAuthService's WebGL stub). On desktop
            // this is strictly BETTER than before: the old unscoped assembly compiled here and
            // then threw DllNotFound at runtime on tap. Web accounts, if ever wanted, are
            // Google Identity Services in JS + Firebase REST behind this same method.
            FlowTrace.Step("Auth", "Google sign-in is Android-only on this build; guest/email identity instead.");
            await Task.CompletedTask;
            return AuthOutcome.Fail("Google sign-in isn't available on this platform.");
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
            if (outcome.Success) NoteAccessGranted("Google sign-in");   // ACCESS only - never the save key
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

        /// <summary>
        /// A Firebase identity granted ACCESS. Nothing is bound: the save key is the
        /// wallet's job (identity law, file header). Traced so "I signed in and my save
        /// did not move" is answerable from the log rather than from a code read.
        /// </summary>
        private static void NoteAccessGranted(string how)
        {
            FlowTrace.Step("Auth", how + " OK - ACCESS granted. The save key is NOT re-bound " +
                                   "(Firebase = access, wallet = data identity); connect a wallet to key the cloud save.");
        }

        /// <summary>
        /// Bind a connected WALLET address as the save player-id (Guarded — one bad op logs
        /// and is skipped, never a soft-lock). Wallet path ONLY: the attested cloud bind
        /// already happened inside the Wallet assembly's connect handler, and this
        /// idempotent re-bind exists so a success can never continue unbound. It passes
        /// through the UNATTESTED overload on purpose — Onboarding cannot see the provider,
        /// so it must not be able to grant a cloud identity; when the address already
        /// matches, BindWallet early-outs and the existing attestation is untouched.
        /// </summary>
        private static void BindPlayer(string walletAddress)
        {
            var svc = GameStateService.Instance;
            if (svc == null) { FlowTrace.Warn("Auth", "BindPlayer: GameStateService null — save not re-keyed."); return; }
            Guard.Try("Auth", "BindWallet(connected wallet address)", () => svc.BindWallet(walletAddress));
        }
    }
}
