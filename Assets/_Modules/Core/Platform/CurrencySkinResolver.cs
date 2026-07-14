// =============================================================================
// CurrencySkinResolver — resolves the ACTIVE CurrencySkin once, before first render
// -----------------------------------------------------------------------------
// WO-603, Option B (runtime skin.json + URL-param override). One build artifact
// serves both the Pi and the Solana/$SKR skins; which one is live is decided at
// runtime here — no rebuild, config only.
//
// Resolution order (first hit wins), all synchronous so it completes before the
// first view builds:
//   1. URL query param  ?skin=pi | ?skin=skr | ?skin=wallet   (WebGL — the SKR
//      Vercel deployment appends ?skin=skr; mirrors FeatureFlags.ApplyUrlActivationOnce,
//      allow-listed to pi|skr|wallet only so a crafted link can only swap skin,
//      never game state).
//   2. skin.json "active" field  (Assets/Resources/Data/Canonical/skin.json).
//   3. "wallet" — the V1 GENERIC WALLET default (WO-713 owner ruling 2026-07-13:
//      "remove the Pi symbol on inventory screen ... leave generic as wallet").
//      V1 ships ZERO crypto; the Pi/SKR skins stay in the table for the later
//      crypto arc (?skin=pi / ?skin=skr / skin.json "active" re-select them).
//
// The generic wallet skin is PRESENTATION-only: no symbol glyph (views show a
// wallet ICON + the plain amount); auth/identity stay on the Pi path so the
// live auth behaviour is unchanged by the currency-presentation swap.
//
// PRESENTATION READS CurrencySkinResolver.Active — never hardcoded π / Pi / $SKR.
// =============================================================================

using System;
using Newtonsoft.Json.Linq;
using UnityEngine;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Core.Platform
{
    /// <summary>Resolves + caches the active <see cref="CurrencySkin"/> (WO-603).</summary>
    public static class CurrencySkinResolver
    {
        /// <summary>StreamingAssets/Resources-relative path to the skin table.</summary>
        private const string SkinJsonPath = "Data/Canonical/skin.json";

        private static CurrencySkin _active;

        /// <summary>
        /// The active skin. Resolved lazily on first access (synchronous — Resources.Load +
        /// a URL-param read), so any view that touches it gets the resolved skin before it
        /// builds. Never null after access; falls back to <see cref="CurrencySkin.PiDefault"/>.
        /// </summary>
        public static CurrencySkin Active
        {
            get
            {
                if (_active == null) Resolve();
                return _active;
            }
        }

        /// <summary>True once the active skin is the SKR/Solana skin.</summary>
        public static bool IsSkr => Active.SkinId == "skr";

        /// <summary>True once the active skin is the V1 generic-wallet skin (no crypto symbol).</summary>
        public static bool IsGenericWallet => Active.SkinId == "wallet";

        /// <summary>
        /// The V1 GENERIC WALLET skin (WO-713 owner ruling 2026-07-13): currency rows show a
        /// wallet ICON + the plain amount — no Pi/SKR symbol (CurrencySymbol is intentionally
        /// EMPTY; views render icon + amount and may label with CurrencyName). Auth/identity
        /// mirror the Pi defaults so making this skin active changes PRESENTATION ONLY —
        /// the live sign-in/identity behaviour is untouched (V1 ships zero crypto; the Pi and
        /// SKR skins remain in skin.json for the later crypto arc).
        /// </summary>
        public static CurrencySkin WalletDefault { get; } = new CurrencySkin(
            skinId: "wallet",
            currencySymbol: "",
            currencyName: "Wallet",
            authMode: SkinAuthMode.PiSdk,
            brandingKey: "",
            storeCtaVerb: "Spend",
            identityKeyKind: SkinIdentityKeyKind.PiUid,
            bindIdentityOnAuth: false);

        /// <summary>
        /// Raised when a view's auth button under the SolanaWallet skin is pressed. The
        /// Wallet assembly (DeNelle.Wallet) subscribes to actually drive wallet-connect —
        /// Core cannot reference Wallet, so this is the seam. If nothing is subscribed the
        /// button logs a Guard warning (no silent failure) — see the RESULT follow-up note.
        /// </summary>
        public static event Action WalletConnectRequested;

        /// <summary>Fired by a view's "Connect Wallet" button (SKR skin). Routes to the
        /// Wallet-assembly subscriber, or warns if the full flow is not yet wired.</summary>
        public static void RequestWalletConnect()
        {
            var handler = WalletConnectRequested;
            if (handler == null)
            {
                FlowTrace.Warn("Skin",
                    "Connect Wallet pressed but no wallet-connect handler is subscribed " +
                    "(WalletConnectRequested) — the full Solana wallet flow is a follow-up (WO-603 flagged).");
                return;
            }
            try { handler.Invoke(); }
            catch (Exception e) { FlowTrace.Fail("Skin", $"WalletConnectRequested handler threw: {e.Message}"); }
        }

        /// <summary>Forces a re-resolve (tests / a config hot-swap). Rarely needed.</summary>
        public static void Reload() { _active = null; Resolve(); }

        /// <summary>Test seam — pin a specific skin.</summary>
        public static void Override(CurrencySkin skin) => _active = skin ?? CurrencySkin.PiDefault;

        // =====================================================================
        //  Resolution
        // =====================================================================

        private static void Resolve()
        {
            string requested = ReadUrlSkinOverride();          // step 1 — URL param
            JObject table = LoadSkinTable();                   // skin.json (may be null)

            if (string.IsNullOrEmpty(requested))               // step 2 — skin.json "active"
                requested = table?["active"]?.ToString();

            if (string.IsNullOrEmpty(requested))               // step 3 — V1 generic-wallet default
                requested = "wallet";

            requested = requested.Trim().ToLowerInvariant();

            _active = BuildSkin(requested, table);
            FlowTrace.Step("Skin",
                $"Currency skin resolved: '{_active.SkinId}' (auth={_active.AuthMode}, " +
                $"symbol={_active.CurrencySymbol}, identity={_active.IdentityKeyKind}).");
        }

        /// <summary>
        /// Reads <c>?skin=pi</c>/<c>?skin=skr</c>/<c>?skin=wallet</c> from the WebGL page URL.
        /// Allow-listed to pi|skr|wallet only. Empty off-web (Application.absoluteURL is empty
        /// in editor/standalone). Never throws.
        /// </summary>
        private static string ReadUrlSkinOverride()
        {
            try
            {
                string url = Application.absoluteURL;
                if (string.IsNullOrEmpty(url)) return null;
                int q = url.IndexOf('?');
                if (q < 0) return null;

                foreach (var pair in url.Substring(q + 1).Split('&'))
                {
                    int eq = pair.IndexOf('=');
                    string key = (eq < 0 ? pair : pair.Substring(0, eq)).Trim();
                    if (!key.Equals("skin", StringComparison.OrdinalIgnoreCase)) continue;
                    string val = (eq < 0 ? "" : pair.Substring(eq + 1)).Trim().ToLowerInvariant();
                    if (val == "pi" || val == "skr" || val == "wallet")
                    {
                        FlowTrace.Step("Skin", $"?skin={val} detected on the page URL — overriding skin.json.");
                        return val;
                    }
                    if (!string.IsNullOrEmpty(val))
                        FlowTrace.Warn("Skin", $"?skin={val} is not an allow-listed skin (pi|skr|wallet) — ignored.");
                }
            }
            catch (Exception ex) { FlowTrace.Warn("Skin", "URL skin-override parse skipped: " + ex.Message); }
            return null;
        }

        /// <summary>Loads the skin.json table via the WebGL-safe CanonicalJson loader.
        /// Returns null (→ hardcoded defaults) when absent or garbled — never throws.</summary>
        private static JObject LoadSkinTable()
        {
            try
            {
                string json = CanonicalJson.Read(SkinJsonPath);
                if (string.IsNullOrEmpty(json))
                {
                    FlowTrace.Warn("Skin", "skin.json not found — using hardcoded skin defaults (Pi).");
                    return null;
                }
                return JObject.Parse(json);
            }
            catch (Exception ex)
            {
                FlowTrace.Warn("Skin", $"skin.json parse failed ({ex.Message}) — using hardcoded skin defaults.");
                return null;
            }
        }

        /// <summary>
        /// Builds a <see cref="CurrencySkin"/> for the requested id — from the skin.json
        /// entry when present, else the hardcoded default for that id, else the Pi default.
        /// </summary>
        private static CurrencySkin BuildSkin(string skinId, JObject table)
        {
            CurrencySkin fallback =
                skinId == "skr"    ? CurrencySkin.SkrDefault :
                skinId == "wallet" ? WalletDefault :
                                     CurrencySkin.PiDefault;

            JToken entry = table?["skins"]?[skinId];
            if (entry == null) return fallback;

            return new CurrencySkin(
                skinId:            Str(entry["skinId"], fallback.SkinId),
                currencySymbol:    Str(entry["currencySymbol"], fallback.CurrencySymbol),
                currencyName:      Str(entry["currencyName"], fallback.CurrencyName),
                authMode:          ParseAuth(entry["authMode"], fallback.AuthMode),
                brandingKey:       Str(entry["brandingKey"], fallback.BrandingKey),
                storeCtaVerb:      Str(entry["storeCtaVerb"], fallback.StoreCtaVerb),
                identityKeyKind:   ParseIdentity(entry["identityKeyKind"], fallback.IdentityKeyKind),
                bindIdentityOnAuth: entry["bindIdentityOnAuth"]?.Type == JTokenType.Boolean
                                        ? entry["bindIdentityOnAuth"].Value<bool>()
                                        : fallback.BindIdentityOnAuth);
        }

        private static string Str(JToken t, string fallback)
        {
            string s = t?.ToString();
            return string.IsNullOrEmpty(s) ? fallback : s;
        }

        private static SkinAuthMode ParseAuth(JToken t, SkinAuthMode fallback)
        {
            string s = t?.ToString();
            if (string.IsNullOrEmpty(s)) return fallback;
            return s.Trim().Equals("SolanaWallet", StringComparison.OrdinalIgnoreCase)
                ? SkinAuthMode.SolanaWallet
                : s.Trim().Equals("PiSdk", StringComparison.OrdinalIgnoreCase)
                    ? SkinAuthMode.PiSdk
                    : fallback;
        }

        private static SkinIdentityKeyKind ParseIdentity(JToken t, SkinIdentityKeyKind fallback)
        {
            string s = t?.ToString();
            if (string.IsNullOrEmpty(s)) return fallback;
            return s.Trim().Equals("WalletPubkey", StringComparison.OrdinalIgnoreCase)
                ? SkinIdentityKeyKind.WalletPubkey
                : s.Trim().Equals("PiUid", StringComparison.OrdinalIgnoreCase)
                    ? SkinIdentityKeyKind.PiUid
                    : fallback;
        }
    }
}
