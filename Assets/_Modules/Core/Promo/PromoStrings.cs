// =============================================================================
// PromoStrings — the ONE home for every word the promo-code door says.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.Promo
//
// WHY THIS FILE EXISTS
// The redeem screen is the one place in the game where a vague failure reads as a
// SCAM. "Invalid code." on a screen the player just typed a real code into leaves
// them unable to tell whether they mistyped, whether the code was already spent,
// whether it expired, or whether WE lost their reward. So every documented server
// error gets its OWN sentence, each sentence says whether the code was consumed,
// and none of them is reused for a second cause.
//
// Those sentences are player-facing copy, so per CLAUDE.md §7 they live in
// canon-strings.json — in BOTH canonical copies (Assets/Resources/Data/Canonical
// and Assets/StreamingAssets/Data/Canonical), byte-identical, ASCII-only (TMP
// renders non-ASCII as tofu). Nothing here hardcodes a sentence; this class only
// names KEYS. Pinned by PromoRedeemEntryRegression.
//
// Loading mirrors CanonStrings.LoadMap verbatim (flat string->string map read
// through DeNelle.Core.CanonicalJson — Resources first, StreamingAssets fallback,
// WebGL-safe). CanonStrings itself lives in DeNelle.Onboarding, which neither
// DeNelle.Core nor DeNelle.Wallet may reference (read the .asmdef — §5), hence
// this Core-side twin rather than a cross-assembly reach.
//
// A missing key returns the visible "[[missing:key]]" marker (the house
// convention) AND self-reports through FlowTrace — never a silent blank on a
// screen that is handing out rewards.
// =============================================================================

using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Core.Promo
{
    /// <summary>Canon-backed copy for the promo-code redeem door. Keys only — no sentences.</summary>
    public static class PromoStrings
    {
        private const string CanonRelativePath = "Data/Canonical/canon-strings.json";

        // ── Chrome ───────────────────────────────────────────────────────────
        /// <summary>Label of the store's entry button ("Redeem a Code").</summary>
        public const string KeyEntry       = "redeemEntry";
        /// <summary>Panel title.</summary>
        public const string KeyTitle       = "redeemTitle";
        /// <summary>One-line explanation under the title.</summary>
        public const string KeyBlurb       = "redeemBlurb";
        /// <summary>Input-field placeholder.</summary>
        public const string KeyPlaceholder = "redeemPlaceholder";
        /// <summary>Submit-button label.</summary>
        public const string KeyAction      = "redeemAction";
        /// <summary>Static hint under the field (case-insensitivity).</summary>
        public const string KeyHint        = "redeemHint";
        /// <summary>Status line while the request is in flight.</summary>
        public const string KeyBusy        = "redeemBusy";

        // ── Success ──────────────────────────────────────────────────────────
        /// <summary>Success line; {0} = the composed reward summary.</summary>
        public const string KeySuccess          = "redeemSuccess";
        /// <summary>Success line when the code carried no reward at all.</summary>
        public const string KeySuccessNoReward  = "redeemSuccessNoReward";
        /// <summary>Reward part; {0} = crystal amount.</summary>
        public const string KeyRewardCrystals   = "redeemRewardCrystals";
        /// <summary>Reward part; {0} = coin amount.</summary>
        public const string KeyRewardCoins      = "redeemRewardCoins";
        /// <summary>Reward part; {0} = the store pack name.</summary>
        public const string KeyRewardPack       = "redeemRewardPack";

        // ── Failures — ONE distinct sentence per documented cause ────────────
        /// <summary>The player submitted an empty field.</summary>
        public const string KeyErrEmpty       = "redeemErrEmpty";
        /// <summary>Server: INVALID_CODE.</summary>
        public const string KeyErrInvalid     = "redeemErrInvalid";
        /// <summary>Server: ALREADY_REDEEMED (also the local dedup set).</summary>
        public const string KeyErrAlreadyUsed = "redeemErrAlreadyUsed";
        /// <summary>Server: EXPIRED.</summary>
        public const string KeyErrExpired     = "redeemErrExpired";
        /// <summary>Server: PLAYER_LIMIT_REACHED.</summary>
        public const string KeyErrPlayerLimit = "redeemErrPlayerLimit";
        /// <summary>No connection / unreachable endpoint — the code was NOT spent.</summary>
        public const string KeyErrOffline     = "redeemErrOffline";
        /// <summary>Identity proof refused (401/400 from the wallet-auth rail).</summary>
        public const string KeyErrIdentity    = "redeemErrIdentity";
        /// <summary>No player identity at all — nothing to key the redemption to.</summary>
        public const string KeyErrSignIn      = "redeemErrSignIn";
        /// <summary>Anything else (unparseable body, unnamed error code).</summary>
        public const string KeyErrUnknown     = "redeemErrUnknown";

        /// <summary>Every failure key, in one place, so the oracle can prove they are distinct.</summary>
        public static readonly string[] FailureKeys =
        {
            KeyErrEmpty, KeyErrInvalid, KeyErrAlreadyUsed, KeyErrExpired,
            KeyErrPlayerLimit, KeyErrOffline, KeyErrIdentity, KeyErrSignIn, KeyErrUnknown,
        };

        private static Dictionary<string, string> _canon;

        /// <summary>Resolves a canon key. Returns "[[missing:key]]" (and self-reports) when absent.</summary>
        public static string Get(string key)
        {
            EnsureLoaded();
            if (_canon != null && key != null && _canon.TryGetValue(key, out var value) && !string.IsNullOrEmpty(value))
                return value;
            FlowTrace.Fail("Promo", $"canon-strings key '{key}' missing — the redeem screen would show a placeholder marker instead of a sentence.");
            return $"[[missing:{key}]]";
        }

        /// <summary>Resolves a canon key and formats it. A bad format string degrades to the raw sentence.</summary>
        public static string Format(string key, params object[] args)
        {
            string raw = Get(key);
            if (args == null || args.Length == 0) return raw;
            try { return string.Format(raw, args); }
            catch (FormatException ex)
            {
                FlowTrace.Fail("Promo", $"canon-strings key '{key}' has a bad format placeholder: {ex.Message}");
                return raw;
            }
        }

        /// <summary>Test/diagnostic hook — drops the cached map so a re-read picks up an edit.</summary>
        public static void Reload() { _canon = null; }

        private static void EnsureLoaded()
        {
            if (_canon != null) return;
            try
            {
                string json = CanonicalJson.Read(CanonRelativePath);
                if (string.IsNullOrEmpty(json))
                {
                    FlowTrace.Fail("Promo", $"canonical file not found (Resources or StreamingAssets): {CanonRelativePath} — every redeem sentence would render as a placeholder.");
                    _canon = new Dictionary<string, string>();
                    return;
                }

                // Flat string->string map with some leading "_" metadata keys: deserialize
                // loosely, keep only the string entries (the CanonStrings convention).
                var raw = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
                var map = new Dictionary<string, string>();
                if (raw != null)
                {
                    foreach (var kv in raw)
                        if (kv.Value is string s) map[kv.Key] = s;
                }
                _canon = map;
            }
            catch (Exception ex)
            {
                // No silent catch (§12): the screen still works, but say why it lost its words.
                FlowTrace.Fail("Promo", $"failed to read {CanonRelativePath}: {ex.GetType().Name}: {ex.Message}");
                _canon = new Dictionary<string, string>();
            }
        }
    }
}
