// =============================================================================
// WalletRegistry — typed model + loader for the canonical wallets.json (Week 7)
// -----------------------------------------------------------------------------
// The v1 React app keeps wallet addresses in docs/wallets-of-record.md and in
// src/services/rewards-distributor.ts. The Unity port lifts the PUBLIC addresses
// into Assets/StreamingAssets/Data/Canonical/wallets.json — exactly the
// canonical-JSON pattern PackCatalog and Theme use — so the transparency
// display and the devnet pack-purchase recipient both read one source of truth.
//
// PUBLIC ADDRESSES ONLY. Per v2-unity-port-spec.md Part 10 no private key, seed
// phrase or signer keypair is ever stored in wallets.json or anywhere in this
// repo. The game holds no key — the player's own wallet (Phantom / Seeker Seed
// Vault) signs every transaction; the game only knows where to SEND funds.
//
// Two addresses ship today, both verbatim from docs/wallets-of-record.md:
//   * rewardsDistributor   (§2) — hardware-backed Seeker wallet, shown for
//     transparency. NOT a payment destination.
//   * devnetPurchaseRecipient (§3) — the documented devnet smoke-test sink that
//     pack-purchase transfers land in until the §4 Squads multisig revenue
//     treasuries are provisioned (those are owner/mainnet-gated — not the agent).
// =============================================================================

using System;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace DeNelle.Wallet
{
    /// <summary>One public wallet entry from wallets.json — address + metadata.</summary>
    [Serializable]
    public sealed class WalletEntry
    {
        /// <summary>Human-readable label for UI display.</summary>
        [JsonProperty("label")] public string Label;
        /// <summary>The base58 PUBLIC address. Never a private key.</summary>
        [JsonProperty("address")] public string Address;
        /// <summary>The wallet provider (Seed Vault, Solflare, …).</summary>
        [JsonProperty("provider")] public string Provider;
        /// <summary>The wallet holder of record.</summary>
        [JsonProperty("holder")] public string Holder;
        /// <summary>What the wallet is used for.</summary>
        [JsonProperty("purpose")] public string Purpose;
        /// <summary>Always true — wallets.json only ever holds public addresses.</summary>
        [JsonProperty("public")] public bool Public = true;
        /// <summary>Optional network qualifier (e.g. "devnet").</summary>
        [JsonProperty("network")] public string Network;
        /// <summary>Block-explorer / transparency note.</summary>
        [JsonProperty("explorerNote")] public string ExplorerNote;

        /// <summary>True when an address is present.</summary>
        public bool IsValid => !string.IsNullOrEmpty(Address);

        /// <summary>Short <c>AbCd…WxYz</c> form for compact UI.</summary>
        public string ShortAddress
        {
            get
            {
                if (string.IsNullOrEmpty(Address) || Address.Length < 8) return Address ?? string.Empty;
                return $"{Address.Substring(0, 4)}…{Address.Substring(Address.Length - 4)}";
            }
        }
    }

    /// <summary>The parsed wallets.json root.</summary>
    [Serializable]
    public sealed class WalletRegistryData
    {
        [JsonProperty("version")] public int Version;
        /// <summary>The public Rewards Distributor — shown for transparency (§2).</summary>
        [JsonProperty("rewardsDistributor")] public WalletEntry RewardsDistributor;
        /// <summary>The devnet pack-purchase recipient (§3 — dev/staging wallet).</summary>
        [JsonProperty("devnetPurchaseRecipient")] public WalletEntry DevnetPurchaseRecipient;
    }

    /// <summary>
    /// Static surface over the canonical wallets.json — loads + caches the
    /// public wallet entries. Mirrors <see cref="PackCatalog"/> and Theme.cs:
    /// canonical JSON under StreamingAssets, read via streamingAssetsPath,
    /// parsed by Newtonsoft.Json.
    /// </summary>
    public static class WalletRegistry
    {
        /// <summary>StreamingAssets-relative path to the canonical wallet data.</summary>
        private const string StreamingRelativePath = "Data/Canonical/wallets.json";

        // Hard fallbacks — verbatim from docs/wallets-of-record.md §2 / §3. Used
        // only if wallets.json fails to load, so the transparency display and the
        // devnet smoke test still have the canonical PUBLIC addresses. No keys.
        private const string FallbackRewardsDistributor = "2JRmEmrqUbhTiHX3u5bes5kHYZeZkJ2V1cMWubxwnmNi";
        private const string FallbackDevnetRecipient = "3Eeww2hyBUhiLi7AS2xsjZbfZQ2fmPFq8yh53vNzgaHe";

        private static WalletRegistryData _data;

        /// <summary>The public Rewards Distributor entry (transparency display).</summary>
        public static WalletEntry RewardsDistributor
        {
            get { EnsureLoaded(); return _data.RewardsDistributor; }
        }

        /// <summary>The devnet pack-purchase recipient entry.</summary>
        public static WalletEntry DevnetPurchaseRecipient
        {
            get { EnsureLoaded(); return _data.DevnetPurchaseRecipient; }
        }

        /// <summary>The public Rewards Distributor base58 address (convenience).</summary>
        public static string RewardsDistributorAddress
        {
            get
            {
                var e = RewardsDistributor;
                return e != null && e.IsValid ? e.Address : FallbackRewardsDistributor;
            }
        }

        /// <summary>The devnet pack-purchase recipient base58 address (convenience).</summary>
        public static string DevnetPurchaseRecipientAddress
        {
            get
            {
                var e = DevnetPurchaseRecipient;
                return e != null && e.IsValid ? e.Address : FallbackDevnetRecipient;
            }
        }

        /// <summary>Forces a re-read of wallets.json (tests / Monday sync).</summary>
        public static void Reload()
        {
            _data = null;
            EnsureLoaded();
        }

        // =====================================================================
        //  Loading
        // =====================================================================

        private static void EnsureLoaded()
        {
            if (_data != null) return;
            _data = LoadRegistry();
        }

        private static WalletRegistryData LoadRegistry()
        {
            // WebGL-safe load via CanonicalJson: Resources.Load first (works in a
            // browser, where File.ReadAllText(streamingAssetsPath) THROWS), then a
            // StreamingAssets fallback on desktop/editor. wallets.json is mirrored
            // into Assets/Resources/Data/Canonical/ so the Resources path resolves.
            try
            {
                string json = DeNelle.Core.CanonicalJson.Read(StreamingRelativePath);
                if (!string.IsNullOrEmpty(json))
                {
                    var parsed = JsonConvert.DeserializeObject<WalletRegistryData>(json);
                    if (parsed != null)
                        return Fill(parsed);
                    Debug.LogError("[WalletRegistry] wallets.json parsed empty.");
                }
                else
                {
                    Debug.LogError("[WalletRegistry] wallets.json not found (Resources or StreamingAssets).");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[WalletRegistry] Failed to read wallets.json: {ex.Message}");
            }

            Debug.LogWarning("[WalletRegistry] Falling back to the canonical hard-coded PUBLIC addresses.");
            return Fill(new WalletRegistryData());
        }

        /// <summary>Backfills any missing entry with the canonical public fallback.</summary>
        private static WalletRegistryData Fill(WalletRegistryData data)
        {
            if (data.RewardsDistributor == null || !data.RewardsDistributor.IsValid)
            {
                data.RewardsDistributor = new WalletEntry
                {
                    Label = "Rewards Distributor",
                    Address = FallbackRewardsDistributor,
                    Purpose = "SKR rewards distribution to players (Streams A/B/C).",
                    Public = true,
                };
            }
            if (data.DevnetPurchaseRecipient == null || !data.DevnetPurchaseRecipient.IsValid)
            {
                data.DevnetPurchaseRecipient = new WalletEntry
                {
                    Label = "Devnet Pack-Purchase Recipient (Dev / Staging wallet)",
                    Address = FallbackDevnetRecipient,
                    Purpose = "Devnet pack-purchase smoke-test recipient.",
                    Public = true,
                    Network = "devnet",
                };
            }
            return data;
        }
    }
}
