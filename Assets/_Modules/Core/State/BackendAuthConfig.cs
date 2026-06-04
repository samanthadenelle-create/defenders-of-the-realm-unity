// =============================================================================
// BackendAuthConfig — the client-side backend save-auth feature flag (WO-121).
// -----------------------------------------------------------------------------
// Gates whether GameStateService attaches wallet-signed auth headers
// (X-Wallet / X-Nonce / X-Signature) to /api/game/save and /api/game/load.
//
// ── WHY A FLAG (offline / unauthed play must keep working) ───────────────────
// The WO-120 backend now ENFORCES the signature (401 on a bad/missing one), but
// the backend was never deployed live yet (MEMORY: "Backend never connected"),
// and the real MWA signer (SolanaWalletProvider behind SOLANA_SDK) is not wired
// either. So the client must NOT start signing until BOTH the backend is live
// AND a real signer exists. This flag is the master switch. It defaults OFF, so
// the current behaviour is unchanged: GameStateService syncs without auth
// headers exactly as before.
//
// ── HOW IT FLIPS LIVE ────────────────────────────────────────────────────────
// 1. Add the scripting define BACKEND_AUTH_ENFORCED (Project Settings ▸ Player ▸
//    Scripting Define Symbols), OR set Override = true at runtime (a remote-
//    config / debug toggle). Either makes Enforced true.
// 2. A real IWalletSigner must be connected AND report CanSign == true (a
//    SOLANA_SDK SolanaWalletProvider). Until then GameStateService logs that
//    signing is stubbed and SKIPS the headers — the request still goes out
//    unauthed (the live backend would 401 it, which is the intended pressure to
//    finish wiring the signer; an undeployed backend simply ignores the headers).
//
// The flag and the signer-capability check are independent gates: BOTH must be
// satisfied for a real signature to be sent. This keeps a half-finished rollout
// (flag on, no signer; or signer present, flag off) from breaking saves.
// =============================================================================

namespace DeNelle.Core.State
{
    /// <summary>
    /// Master switch for the backend wallet-signature auth on save/load.
    /// Defaults OFF (offline-safe). Flip on via the BACKEND_AUTH_ENFORCED
    /// scripting define or the runtime <see cref="Override"/>.
    /// </summary>
    public static class BackendAuthConfig
    {
        /// <summary>
        /// Runtime override for the enforcement flag. Null = defer to the
        /// compile-time define. Set true/false from a debug menu or remote
        /// config to flip enforcement without a rebuild. Default null (off).
        /// </summary>
        public static bool? Override { get; set; }

        /// <summary>
        /// True when the client should attach wallet-signed auth headers to
        /// backend save/load calls. The runtime <see cref="Override"/> wins when
        /// set; otherwise the BACKEND_AUTH_ENFORCED compile define decides.
        /// Defaults false so the existing unauthed sync path is preserved.
        /// </summary>
        public static bool Enforced
        {
            get
            {
                if (Override.HasValue) return Override.Value;
#if BACKEND_AUTH_ENFORCED
                return true;
#else
                return false;
#endif
            }
        }
    }
}
