// =============================================================================
// LoginViewModel (WO-769, rewritten by WO-837-B) — the state+logic seam behind
// LoginPanelController.
// Strict MVVM (ARCHITECTURE_PRINCIPLES §1/§2; UiMvvmConformanceRegression): the
// View is a dumb Obsidian skin that binds this VM and reads NO game state; ALL
// auth + player-binding logic lives here. VMs are pure C# (build no uGUI).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Onboarding   Namespace: DeNelle.Onboarding.
//
// ⛔ IDENTITY LAW (owner ruling 2026-08-21 — WALLET-ONLY, everywhere):
//   THE WALLET IS THE IDENTITY. There is no second login path on any platform.
//   Guest (the device-hash key GameStateService mints on load) is the local
//   escape; connecting a wallet is the only thing that binds a cloud identity.
//
// WHAT WAS REMOVED, AND WHY IT CANNOT COME BACK QUIETLY:
//   This VM used to wrap DeNelle.Core.Auth.FirebaseAuthService and expose
//   SignInAsync / SignUpAsync / SendPasswordResetAsync / SignInWithGoogleAsync.
//   WO-769 made email the login; WO-847 then narrowed the wallet-first surface
//   to ANDROID ONLY and deliberately kept those paths alive for a Google Play
//   release. The owner closed that:
//     "That's only true with the Play Store, which we are not in. We are only
//      in the dApp Store, which is all wallet authentication based."
//   The game ships ONLY to the Solana dApp Store, so email/Google/Firebase are
//   gone as player-facing paths on every platform.
//
// ⚠ NO SAVE WAS ORPHANED BY THIS REMOVAL, and the reason is worth keeping:
//   the email/Google paths were ACCESS ONLY — they bound NOTHING. The security
//   audit (2026-08-02) proved a Firebase UID never keyed a save (it cannot even
//   pass api/_lib/wallet-auth.js's base58 WALLET_RE), so an email player's save
//   key already IS their guest-local-* device hash and still is after this
//   change. The backend rails (X-Guest-Id / X-Wallet+X-Nonce+X-Signature) never
//   saw a Firebase token — nothing on the save path is touched here.
//
// FirebaseAuthService.cs REMAINS in the tree but has NO caller from this seam.
// It still hosts the AuthOutcome type that the wallet bridge resolves with, and
// Firebase App Distribution (how testers get the APK) is a different product
// entirely and is untouched — see WO-837-B §1.
// =============================================================================
using System.Threading.Tasks;
using DeNelle.Core.Auth;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.Platform;
using DeNelle.Core.State;

namespace DeNelle.Onboarding
{
    /// <summary>Wallet-connect + guest logic for the login screen. Pure C#, no uGUI.</summary>
    public sealed class LoginViewModel
    {
        /// <summary>Factory the View binds to (the VM-routing seam the MVVM gate looks for).</summary>
        public static LoginViewModel CreateDefault() => new LoginViewModel();

        /// <summary>
        /// Connect a wallet as the sign-in identity — the ONLY identity path in the
        /// game (WO-837-B). Routes through the EXISTING wallet stack via
        /// LoginWalletBridge (WalletSkinBootstrap -> WalletService.Connect); the
        /// Wallet-side handler binds the connected address explicitly (never
        /// skin-gated). The idempotent re-bind here exists so a success can never
        /// continue unbound (BindWallet early-outs on an unchanged key).
        /// </summary>
        public async Task<AuthOutcome> ConnectWalletAsync()
        {
            FlowTrace.Step("Auth", "wallet connect requested from the login surface.");
            AuthOutcome outcome = await LoginWalletBridge.ConnectAsync();
            if (outcome.Success && !string.IsNullOrEmpty(outcome.UserId)) BindPlayer(outcome.UserId);
            return outcome;
        }

        /// <summary>
        /// Play without connecting: nothing to bind — GameStateService.EnsureAccount already
        /// mints a stable device-hash guest identity on load. A guest can connect a wallet
        /// later and carry that save over.
        /// </summary>
        public void ContinueAsGuest()
        {
            FlowTrace.Step("Auth", "guest continue — using the device-hash guest identity (no bind).");
        }

        /// <summary>
        /// Bind a connected WALLET address as the save player-id (Guarded — one bad op logs
        /// and is skipped, never a soft-lock). The attested cloud bind already happened
        /// inside the Wallet assembly's connect handler; this idempotent re-bind exists so
        /// a success can never continue unbound. It passes through the UNATTESTED overload
        /// on purpose — Onboarding cannot see the provider, so it must not be able to grant
        /// a cloud identity; when the address already matches, BindWallet early-outs and the
        /// existing attestation is untouched.
        /// </summary>
        private static void BindPlayer(string walletAddress)
        {
            var svc = GameStateService.Instance;
            if (svc == null) { FlowTrace.Warn("Auth", "BindPlayer: GameStateService null — save not re-keyed."); return; }
            Guard.Try("Auth", "BindWallet(connected wallet address)", () => svc.BindWallet(walletAddress));
        }
    }
}
