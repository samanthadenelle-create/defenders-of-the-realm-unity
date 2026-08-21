// =============================================================================
// LoginWalletBridge (WO-847, narrowed by WO-837-B) - the Core-side seam the
// login surface uses to drive the wallet-connect stack.
// -----------------------------------------------------------------------------
// ⛔ THE PLATFORM SPLIT IS RETIRED (owner ruling 2026-08-21). WO-847 shipped a
// LoginSurfacePlatform / LoginSurfaceLayout seam so that ONLY Android/Seeker got
// the wallet-first surface while desktop/WebGL kept the WO-787/845 email form.
// That caveat existed to serve a Google Play release. The owner's ruling:
//   "That's only true with the Play Store, which we are not in. We are only in
//    the dApp Store, which is all wallet authentication based."
// There is now exactly ONE login surface on every platform - Connect Wallet +
// Play as Guest - so the enum, the platform check and the LayoutOverride test
// seam are DELETED rather than left resolving to a constant. A "which layout"
// switch with one arm is the duplicated-state trap CLAUDE.md §5/§2 describes:
// the next author restores the other arm because the type still implies it.
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
using DeNelle.Core.Auth;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Core.Platform
{
    /// <summary>
    /// Core-side bridge from the login surface to the wallet-connect stack.
    /// <see cref="ConnectHandler"/> is registered by DeNelle.Wallet
    /// (WalletSkinBootstrap.Install, unconditional) and resolves an
    /// <see cref="AuthOutcome"/> whose UserId carries the connected wallet
    /// address - the ONLY identity the login surface can bind (WO-837-B).
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
