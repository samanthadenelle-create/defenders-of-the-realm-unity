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
using System.Threading.Tasks;
using DeNelle.Core.Auth;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.State;

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
