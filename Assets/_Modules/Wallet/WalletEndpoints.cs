// =============================================================================
// WalletEndpoints — Solana cluster + RPC config (Week 7, single source of truth)
// -----------------------------------------------------------------------------
// C# port of src/modules/wallet/walletConfig.ts. The React file hard-codes the
// cluster as a constant so it is unambiguous in the bundle and in code review;
// the Unity port keeps the same discipline.
//
// CRITICAL — devnet-only (v2-unity-port-spec.md Part 10):
//   The React walletConfig.ts ships WALLET_CLUSTER = 'mainnet-beta'. The Unity
//   v2 foundation does the OPPOSITE on purpose: WalletService.ActiveNetwork
//   defaults to Devnet and the agent never flips it. This file therefore only
//   has to resolve endpoints for whichever WalletNetwork it is handed — it does
//   not itself pick the network. The owner-gated Mainnet flip is a single
//   change to WalletService.ActiveNetwork; the Mainnet RPC + token mints below
//   are present ONLY so that flip resolves to something, never used by the agent.
//
// SPL TOKEN MINTS:
//   USDC and SKR transfers need each token's mint address. Devnet USDC is the
//   well-known Circle devnet mint. The SKR mint is NOT yet published for devnet
//   in any doc the agent can read — see docs/port-notes/week7-wallet.md: the
//   integrator must drop the real devnet SKR mint into SkrMintDevnet before an
//   SKR transfer can be built. Until then an SKR Pay() fails cleanly with a
//   descriptive error rather than sending to a wrong address.
// =============================================================================

namespace DeNelle.Wallet
{
    /// <summary>
    /// Cluster + RPC + SPL-mint configuration. Pure constants — no SDK types,
    /// so this compiles with or without the Solana Unity SDK installed.
    /// </summary>
    public static class WalletEndpoints
    {
        // ── RPC endpoints ────────────────────────────────────────────────────
        // Public Solana RPC. Rate-limited — fine for devnet QA and demos. A
        // production mainnet build would swap in a dedicated provider (Helius /
        // Triton / QuickNode), per walletConfig.ts. The agent ships devnet only.
        public const string DevnetRpcUrl = "https://api.devnet.solana.com";
        public const string MainnetRpcUrl = "https://api.mainnet-beta.solana.com";

        // WebSocket endpoints — used by the SDK for confirmation subscriptions.
        public const string DevnetWsUrl = "wss://api.devnet.solana.com";
        public const string MainnetWsUrl = "wss://api.mainnet-beta.solana.com";

        // ── SPL token mints ──────────────────────────────────────────────────
        // USDC devnet mint — Circle's canonical devnet USDC. Verified-stable.
        public const string UsdcMintDevnet = "4zMMC9srt5Ri5X14GAgXhaHii3GnPAEERYPJgZJDncDU";
        // USDC mainnet mint — Circle's canonical mainnet USDC.
        public const string UsdcMintMainnet = "EPjFWdd5AufqSSqeM2qN1xzybapC8G4wEGGkZwyTDt1v";

        // SKR (Solana Seeker token) mints. SKR has no published devnet mint in
        // any doc the agent can read — INTEGRATOR MUST FILL THIS (week7-wallet.md).
        // Left empty so an SKR transfer fails loudly instead of sending wrongly.
        public const string SkrMintDevnet = "3BwWSAUZmyngXDSZiCawEnP7iLgY5ANNopBDz94AB77N";
        // SKR mainnet mint — owner-gated; not the agent's to provision.
        public const string SkrMintMainnet = "";

        // Decimals — SOL is 9 (lamports); USDC and SKR are 6 by SPL convention.
        public const int SolDecimals = 9;
        public const int UsdcDecimals = 6;
        public const int SkrDecimals = 9;

        /// <summary>The HTTP RPC URL for a network.</summary>
        public static string RpcUrl(WalletNetwork network)
            => network == WalletNetwork.Mainnet ? MainnetRpcUrl : DevnetRpcUrl;

        /// <summary>The WebSocket RPC URL for a network.</summary>
        public static string WsUrl(WalletNetwork network)
            => network == WalletNetwork.Mainnet ? MainnetWsUrl : DevnetWsUrl;

        /// <summary>
        /// The Mobile Wallet Adapter / Wallet-Standard CAIP-2 chain id for a
        /// network — <c>solana:devnet</c> / <c>solana:mainnet</c>. MWA uses
        /// <c>mainnet</c>, not <c>mainnet-beta</c> (walletConfig.ts WALLET_MWA_CHAIN).
        /// </summary>
        public static string MwaChain(WalletNetwork network)
            => network == WalletNetwork.Mainnet ? "solana:mainnet" : "solana:devnet";

        /// <summary>The USDC SPL mint for a network.</summary>
        public static string UsdcMint(WalletNetwork network)
            => network == WalletNetwork.Mainnet ? UsdcMintMainnet : UsdcMintDevnet;

        /// <summary>
        /// The SKR SPL mint for a network. Empty until the integrator fills the
        /// real devnet SKR mint into <see cref="SkrMintDevnet"/>.
        /// </summary>
        public static string SkrMint(WalletNetwork network)
            => network == WalletNetwork.Mainnet ? SkrMintMainnet : SkrMintDevnet;

        /// <summary>
        /// The SPL mint for an SPL-token currency rail (USDC / SKR), or empty for
        /// native SOL (which has no mint).
        /// </summary>
        public static string MintFor(CurrencyKind currency, WalletNetwork network)
        {
            switch (currency)
            {
                case CurrencyKind.Usdc: return UsdcMint(network);
                case CurrencyKind.Skr: return SkrMint(network);
                default: return string.Empty; // SOL is native — no mint
            }
        }

        /// <summary>Decimal places for a currency rail.</summary>
        public static int DecimalsFor(CurrencyKind currency)
        {
            switch (currency)
            {
                case CurrencyKind.Usdc: return UsdcDecimals;
                case CurrencyKind.Skr: return SkrDecimals;
                default: return SolDecimals;
            }
        }

        // ── MWA app identity (MIRROR of walletConfig.ts — NOT the live wire values) ──
        // The identity triplet that actually goes on the wire is
        // SolanaWalletProvider.DappIdentityUri / DappIconUri / DappIdentityName
        // (SolanaWalletProvider.cs:186/219/222) — those are what
        // TargetedLocalAssociationScenario.Authorize/Reauthorize pass to
        // MobileWalletAdapterClient. The three constants below are an unreferenced
        // port-parity mirror of the React walletConfig.ts; nothing reads them.
        //
        // SCOPING: `name` is DISPLAY-ONLY — MobileWalletAdapterClient.PrepareAuthRequest
        // (:60-87) puts it in the request's `identity.name` purely for the approval
        // sheet, and Reauthorize (:34-46) re-keys off the stored `auth_token`, with the
        // dapp's real identity verified from the identity URI's
        // /.well-known/assetlinks.json + the APK package/signing cert. So renaming the
        // app never invalidates an existing authorization. The URI IS the scoping key —
        // do NOT casually change it: it must remain the host that actually serves
        // assetlinks.json, or every wallet re-verification fails.

        /// <summary>
        /// The MWA app-identity name advertised to mobile wallets during the
        /// connect handshake — shown on the Seeker's approval screen
        /// (walletConfig.ts WALLET_APP_IDENTITY.name). Display-only.
        /// </summary>
        public const string AppIdentityName = "Echoes of Elarion";

        /// <summary>
        /// The MWA app-identity URI (walletConfig.ts WALLET_APP_IDENTITY.uri).
        /// NOTE: this host does not resolve (NXDOMAIN as of 2026-08-08) — it is dead
        /// mirror data only. The live, assetlinks-serving identity URI is
        /// SolanaWalletProvider.DappIdentityUri. Left as-is deliberately.
        /// </summary>
        public const string AppIdentityUri = "https://defenders-of-the-realm.app";

        /// <summary>The MWA app-identity icon path (walletConfig.ts WALLET_APP_IDENTITY.icon).</summary>
        public const string AppIdentityIcon = "/favicon.ico";
    }
}
