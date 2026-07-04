// =============================================================================
// CurrencySkin — the single source of truth for the currency/auth/branding SKIN
// -----------------------------------------------------------------------------
// WO-603. The game ships ONE build artifact that can present as either the Pi
// Network skin (Pi auth, π symbol, Pi branding — the live production skin) or the
// Solana/$SKR skin (wallet connect, $SKR, Seeker branding) for the Seekerthon
// submission. Which skin is active is resolved at runtime BEFORE first render by
// CurrencySkinResolver (Option B — runtime skin.json + URL-param override).
//
// PRESENTATION ONLY READS THIS RECORD. No view hardcodes "π" / "Pi" / "$SKR" —
// every currency symbol, currency name, auth-button mode, wordmark key, store CTA
// verb and NeonDB identity-key kind flows from the active CurrencySkin.
//
// Owner data-structure lens (memory owner-thinks-in-data-structures): the skin is
// a RECORD looked up from a table (skin.json), interpreted by a thin resolver —
// never a control-flow branch scattered through the views.
// =============================================================================

namespace DeNelle.Core.Platform
{
    /// <summary>How the player authenticates / establishes identity under a skin.</summary>
    public enum SkinAuthMode
    {
        /// <summary>Pi Network SDK sign-in (the live production path — <see cref="PiSignInController"/>).</summary>
        PiSdk = 0,
        /// <summary>Solana wallet connect (Mobile Wallet Adapter / deep-link — the $SKR skin).</summary>
        SolanaWallet = 1,
    }

    /// <summary>Which key identifies the player row in NeonDB under a skin.</summary>
    public enum SkinIdentityKeyKind
    {
        /// <summary>The Pi Network UID (from the verified accessToken).</summary>
        PiUid = 0,
        /// <summary>The connected Solana wallet public key (base58 address).</summary>
        WalletPubkey = 1,
    }

    /// <summary>
    /// One immutable currency/auth/branding skin. Hydrated from skin.json by
    /// <see cref="CurrencySkinResolver"/>; presentation reads it and never types
    /// π / Pi / $SKR inline. Behaves as a record (value-carrying, readonly).
    /// </summary>
    public sealed class CurrencySkin
    {
        /// <summary>Stable skin id — "pi" or "skr".</summary>
        public string SkinId { get; }

        /// <summary>Displayed currency symbol — e.g. "π" (Pi) or "$SKR" (Solana Seeker token).</summary>
        public string CurrencySymbol { get; }

        /// <summary>Displayed currency name — e.g. "Pi" or "SKR".</summary>
        public string CurrencyName { get; }

        /// <summary>How the player signs in / establishes identity under this skin.</summary>
        public SkinAuthMode AuthMode { get; }

        /// <summary>Wordmark / branding lookup key — e.g. "pi_network" or "seeker_skr".
        /// The View resolves this to a logo sprite (or omits branding when absent).</summary>
        public string BrandingKey { get; }

        /// <summary>Store / economy CTA verb — e.g. "Spend Pi" or "Spend $SKR".</summary>
        public string StoreCtaVerb { get; }

        /// <summary>Which key identifies the player row in NeonDB under this skin.</summary>
        public SkinIdentityKeyKind IdentityKeyKind { get; }

        /// <summary>
        /// When true, a successful auth binds the skin-appropriate identity key into
        /// <c>GameState.BoundWallet</c> (the NeonDB playerId). DEFAULT FALSE so the live
        /// Pi deployment keeps today's identity behaviour (zero regression) — enabling it
        /// changes which key writes to NeonDB and therefore needs a migration decision.
        /// </summary>
        public bool BindIdentityOnAuth { get; }

        public CurrencySkin(
            string skinId,
            string currencySymbol,
            string currencyName,
            SkinAuthMode authMode,
            string brandingKey,
            string storeCtaVerb,
            SkinIdentityKeyKind identityKeyKind,
            bool bindIdentityOnAuth)
        {
            SkinId = string.IsNullOrEmpty(skinId) ? "pi" : skinId;
            CurrencySymbol = currencySymbol ?? "π";
            CurrencyName = currencyName ?? "Pi";
            AuthMode = authMode;
            BrandingKey = brandingKey ?? string.Empty;
            StoreCtaVerb = string.IsNullOrEmpty(storeCtaVerb) ? "Spend" : storeCtaVerb;
            IdentityKeyKind = identityKeyKind;
            BindIdentityOnAuth = bindIdentityOnAuth;
        }

        /// <summary>
        /// Picks the NeonDB identity key for this skin from the two candidates the
        /// caller has on hand (Pi UID for the Pi skin, wallet pubkey for the SKR skin).
        /// Returns null when the required candidate is absent (caller falls back to
        /// the guest-local identity — no silent mis-binding).
        /// </summary>
        public string ResolveIdentityKey(string piUid, string walletPubkey)
        {
            switch (IdentityKeyKind)
            {
                case SkinIdentityKeyKind.WalletPubkey:
                    return string.IsNullOrEmpty(walletPubkey) ? null : walletPubkey;
                case SkinIdentityKeyKind.PiUid:
                default:
                    return string.IsNullOrEmpty(piUid) ? null : piUid;
            }
        }

        // --- Hardcoded fallbacks — used only if skin.json is missing/garbled, so the
        //     game NEVER boots without a skin. The Pi default preserves production. ---

        /// <summary>The Pi skin — the live production default (zero regression).</summary>
        public static CurrencySkin PiDefault { get; } = new CurrencySkin(
            skinId: "pi",
            currencySymbol: "π",
            currencyName: "Pi",
            authMode: SkinAuthMode.PiSdk,
            brandingKey: "pi_network",
            storeCtaVerb: "Spend Pi",
            identityKeyKind: SkinIdentityKeyKind.PiUid,
            bindIdentityOnAuth: false);

        /// <summary>The Solana/$SKR skin — the Seekerthon submission skin.</summary>
        public static CurrencySkin SkrDefault { get; } = new CurrencySkin(
            skinId: "skr",
            currencySymbol: "$SKR",
            currencyName: "SKR",
            authMode: SkinAuthMode.SolanaWallet,
            brandingKey: "seeker_skr",
            storeCtaVerb: "Spend $SKR",
            identityKeyKind: SkinIdentityKeyKind.WalletPubkey,
            // SKR is a NEW deployment (no existing players) and the NeonDB save path is already
            // Solana-wallet-native (playerId = base58 pubkey), so binding the wallet pubkey as the
            // identity is correct here. The Pi default stays false to preserve production identity.
            bindIdentityOnAuth: true);
    }
}
