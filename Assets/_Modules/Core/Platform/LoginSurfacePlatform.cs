// =============================================================================
// LoginSurfacePlatform + LoginWalletBridge (WO-847) - the platform seam behind
// the login surface's Android wallet-first split.
// -----------------------------------------------------------------------------
// Owner ruling 2026-08-02: on Android/Seeker the login page is "connect wallet
// or play as guest" - NO email form; desktop/web keep the WO-787/845 email
// layout. LoginViewModel reads LoginSurfacePlatform.Resolve() to pick the
// layout; tests/headless captures pin either layout via LayoutOverride.
//
// LoginWalletBridge is the Core-side seam the login surface uses to drive the
// EXISTING wallet-connect stack. Assembly direction is Wallet -> Core (Core can
// never reference DeNelle.Wallet), so WalletSkinBootstrap registers the handler
// at boot - UNCONDITIONALLY, independent of the currency skin - and the handler
// ends in WalletService.Connect() + an EXPLICIT GameStateService.BindWallet of
// the connected address (the WO-766 identity chain). The login path NEVER
// leaves identity binding to the optional skin config (skin.json
// bindIdentityOnAuth) - that flag keeps gating only the corner-button skin
// path (WalletSkinBootstrap's SKR event subscriber).
// =============================================================================

using System;
using System.Threading.Tasks;
using UnityEngine;
using DeNelle.Core.Auth;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Core.Platform
{
    /// <summary>Which login surface a platform presents (WO-847).</summary>
    public enum LoginSurfaceLayout
    {
        /// <summary>The WO-787/845 email/password form (+ forgot password, guest). Desktop/WebGL.</summary>
        EmailForm = 0,
        /// <summary>Wallet-first: Connect Wallet primary + Play as Guest. Android/Seeker.</summary>
        WalletFirst = 1,
    }

    /// <summary>
    /// Resolves the login-surface layout for the running platform. Pure and
    /// synchronous; <see cref="LayoutOverride"/> is the test/capture seam so
    /// EditMode tests and headless screenshot runs can pin either layout
    /// without an Android build.
    /// </summary>
    public static class LoginSurfacePlatform
    {
        /// <summary>Test/capture seam - when set, wins over the platform check. Null = live behaviour.</summary>
        public static LoginSurfaceLayout? LayoutOverride;

        /// <summary>Android -> wallet-first; everything else -> the email form.</summary>
        public static LoginSurfaceLayout Resolve()
        {
            if (LayoutOverride.HasValue) return LayoutOverride.Value;
            return Application.platform == RuntimePlatform.Android
                ? LoginSurfaceLayout.WalletFirst
                : LoginSurfaceLayout.EmailForm;
        }
    }

    /// <summary>
    /// Core-side bridge from the login surface to the wallet-connect stack.
    /// <see cref="ConnectHandler"/> is registered by DeNelle.Wallet
    /// (WalletSkinBootstrap.Install, unconditional) and resolves with the same
    /// <see cref="AuthOutcome"/> shape the email paths use - UserId carries the
    /// connected wallet address - so the panel's continuation logic does not
    /// care which identity bound.
    /// </summary>
    public static class LoginWalletBridge
    {
        /// <summary>
        /// The Wallet-assembly connect handler (WalletService.Connect + explicit
        /// BindWallet). Set at boot by WalletSkinBootstrap; settable by tests.
        /// </summary>
        public static Func<Task<AuthOutcome>> ConnectHandler { get; set; }

        /// <summary>True once a wallet-connect handler is registered.</summary>
        public static bool HasHandler => ConnectHandler != null;

        /// <summary>
        /// Runs the registered connect flow. Honest failure outcomes on every
        /// branch - no handler and thrown handlers both resolve to a mapped
        /// player-facing error, never a silent no-op.
        /// </summary>
        public static async Task<AuthOutcome> ConnectAsync()
        {
            var handler = ConnectHandler;
            if (handler == null)
            {
                FlowTrace.Warn("Auth",
                    "login Connect Wallet pressed but no wallet-connect handler is registered " +
                    "(LoginWalletBridge) - WalletSkinBootstrap.Install did not run?");
                return AuthOutcome.Fail("Wallet connect isn't available in this build.");
            }
            try
            {
                return await handler();
            }
            catch (Exception e)
            {
                FlowTrace.Fail("Auth", "login wallet-connect handler threw: " + e.Message);
                return AuthOutcome.Fail("Wallet connect failed. Please try again.");
            }
        }
    }
}
