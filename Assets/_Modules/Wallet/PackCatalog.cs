// =============================================================================
// PackCatalog — typed model + loader for the canonical packs.json (spec Part 4)
// -----------------------------------------------------------------------------
// C# port of the React PackDef interface (src/content/packs.ts — monetization-
// v2-spec §8.4). The five packs are CONTENT, not code: PackCatalog reads
// Assets/StreamingAssets/Data/Canonical/packs.json at load time and hydrates
// typed PackDef records. Mirrors the Theme.cs pattern (canonical JSON under
// StreamingAssets, read via Application.streamingAssetsPath, parsed by
// Newtonsoft.Json) — see unity-decisions.md, 2026-05-18.
//
// The pack store (PackStore.cs) reads PackDefs from here; it never types pack
// names, prices or contents inline (spec Part 4 — canon strings flow from JSON).
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Wallet
{
    /// <summary>Per-currency price of a pack — mirrors PackDef.pricing (§8.4).</summary>
    [Serializable]
    public sealed class PackPricing
    {
        /// <summary>The Stripe / USD reference price. Display only — Stripe is web-only.</summary>
        [JsonProperty("usd")] public double Usd;
        /// <summary>USDC wallet-rail amount.</summary>
        [JsonProperty("usdc")] public double Usdc;
        /// <summary>Native SOL wallet-rail amount.</summary>
        [JsonProperty("sol")] public double Sol;
        /// <summary>SKR (Solana Seeker token) wallet-rail amount.</summary>
        [JsonProperty("skr")] public double Skr;
    }

    /// <summary>One in-game currency top-up — mirrors PackDef.contents.economy (§5.2).</summary>
    [Serializable]
    public sealed class PackEconomy
    {
        /// <summary>Glimmer (cosmetic-shop currency, carried for catalogue completeness).</summary>
        [JsonProperty("glimmer")] public int Glimmer;
        /// <summary>Crystals — the primary build currency.</summary>
        [JsonProperty("crystals")] public int Crystals;
        /// <summary>Food.</summary>
        [JsonProperty("food")] public int Food;
        /// <summary>Coins.</summary>
        [JsonProperty("coins")] public int Coins;
    }

    /// <summary>
    /// One convenience-power item — TIME-SAVING only, never combat-power
    /// (the bent covenant, monetization-v2-spec §5.3).
    /// </summary>
    [Serializable]
    public sealed class ConvenienceItemDef
    {
        /// <summary>The item kind: instant-build / instant-repair / harvest-auto-collect / xp-weekend.</summary>
        [JsonProperty("kind")] public string Kind;
        /// <summary>How many of this item the pack grants.</summary>
        [JsonProperty("count")] public int Count;
        /// <summary>Player-facing description of the item.</summary>
        [JsonProperty("description")] public string Description;
    }

    /// <summary>The contents bag of a pack — cosmetics + economy + convenience (§5).</summary>
    [Serializable]
    public sealed class PackContents
    {
        /// <summary>Cosmetic SKUs granted by the pack (1–5 depending on tier).</summary>
        [JsonProperty("cosmetics")] public List<string> Cosmetics = new List<string>();
        /// <summary>The in-game currency top-up.</summary>
        [JsonProperty("economy")] public PackEconomy Economy = new PackEconomy();
        /// <summary>The convenience-power items.</summary>
        [JsonProperty("convenience")] public List<ConvenienceItemDef> Convenience = new List<ConvenienceItemDef>();
    }

    /// <summary>
    /// One purchasable pack — the C# port of React's PackDef (monetization-v2-spec
    /// §8.4). Hydrated from packs.json; never constructed inline by game code.
    /// </summary>
    [Serializable]
    public sealed class PackDef
    {
        /// <summary>Stable SKU (e.g. "hearth-spark"). The entitlement key.</summary>
        [JsonProperty("sku")] public string Sku;
        /// <summary>Pricing tier 1–5 (Hearth Spark → Founder's Vow).</summary>
        [JsonProperty("tier")] public int Tier;
        /// <summary>Canon pack name — verbatim, never paraphrased.</summary>
        [JsonProperty("name")] public string Name;
        /// <summary>Narrative-bible-voice tagline shown on the pack card / detail page.</summary>
        [JsonProperty("tagline")] public string Tagline;
        /// <summary>The pricing-ladder theme description (§4 table).</summary>
        [JsonProperty("theme")] public string Theme;
        /// <summary>True for the launch-window-only Founder's Vow (§4).</summary>
        [JsonProperty("founderOnly")] public bool FounderOnly;
        /// <summary>Per-currency pricing.</summary>
        [JsonProperty("pricing")] public PackPricing Pricing = new PackPricing();
        /// <summary>The contents bag.</summary>
        [JsonProperty("contents")] public PackContents Contents = new PackContents();
        /// <summary>The single cosmetic SKU exclusive to this pack (§5.1).</summary>
        [JsonProperty("packExclusiveCosmetic")] public string PackExclusiveCosmetic;

        /// <summary>The native amount payable in the given currency rail.</summary>
        public double AmountFor(CurrencyKind currency)
        {
            if (Pricing == null) return 0d;
            switch (currency)
            {
                case CurrencyKind.Sol: return Pricing.Sol;
                case CurrencyKind.Usdc: return Pricing.Usdc;
                case CurrencyKind.Skr: return Pricing.Skr;
                default: return 0d;
            }
        }

        /// <summary>The USD reference price string, e.g. <c>"$4.99"</c>.</summary>
        public string UsdReference => Pricing != null ? $"${Pricing.Usd:0.00}" : "$0.00";

        /// <summary>Formats one currency rail's amount + symbol, e.g. <c>"60 SKR"</c>.</summary>
        public string AmountLabel(CurrencyKind currency)
        {
            var amount = AmountFor(currency);
            switch (currency)
            {
                case CurrencyKind.Sol: return $"{amount:0.###} SOL";
                case CurrencyKind.Usdc: return $"{amount:0.00} USDC";
                case CurrencyKind.Skr: return $"{amount:0.##} SKR";
                default: return amount.ToString("0.##");
            }
        }
    }

    /// <summary>The parsed packs.json root.</summary>
    [Serializable]
    public sealed class PackCatalogData
    {
        [JsonProperty("version")] public int Version;
        /// <summary>The permanent UI disclaimer for wallet-rail purchases (§4.1).</summary>
        [JsonProperty("currencyDisclaimer")] public string CurrencyDisclaimer;
        [JsonProperty("packs")] public List<PackDef> Packs = new List<PackDef>();
    }

    /// <summary>
    /// Static surface over the canonical packs.json — loads + caches the five
    /// PackDefs, exposes lookups by SKU / tier. The Theme.cs loading pattern.
    /// </summary>
    public static class PackCatalog
    {
        /// <summary>StreamingAssets-relative path to the canonical pack data.</summary>
        private const string StreamingRelativePath = "Data/Canonical/packs.json";

        private static PackCatalogData _data;

        /// <summary>All five packs, ordered by tier (Hearth Spark → Founder's Vow).</summary>
        public static IReadOnlyList<PackDef> Packs
        {
            get { EnsureLoaded(); return _data.Packs; }
        }

        /// <summary>The permanent wallet-rail purchase disclaimer (§4.1).</summary>
        public static string CurrencyDisclaimer
        {
            get { EnsureLoaded(); return _data.CurrencyDisclaimer ?? "Token price moves with the market."; }
        }

        /// <summary>Looks up a pack by its SKU. Returns null when not found.</summary>
        public static PackDef Find(string sku)
        {
            if (string.IsNullOrEmpty(sku)) return null;
            EnsureLoaded();
            foreach (var pack in _data.Packs)
                if (pack != null && pack.Sku == sku) return pack;
            return null;
        }

        /// <summary>Looks up a pack by its tier (1–5). Returns null when not found.</summary>
        public static PackDef FindByTier(int tier)
        {
            EnsureLoaded();
            foreach (var pack in _data.Packs)
                if (pack != null && pack.Tier == tier) return pack;
            return null;
        }

        /// <summary>Forces a re-read of packs.json (used by tests / the Monday sync).</summary>
        public static void Reload()
        {
            _data = null;
            EnsureLoaded();
        }

        // =====================================================================
        //  Loading
        // =====================================================================

        // =====================================================================
        //  Covenant firewall — the RUNTIME chokepoint (LB-5 / C-COV / C-KIND)
        // ---------------------------------------------------------------------
        //  ConvenienceItemDef.Kind is an unvalidated string; the covenant (§5.3:
        //  TIME-SAVING only, never combat power) was comment-only. This is the
        //  canonical allowlist of sanctioned convenience kinds — the SAME set the
        //  editor MonetizationCovenantRegression gate derives from skr_staking.json
        //  convenienceAllowList + packs.json _schemaNotes + the economy-pack
        //  extension set. Any convenience def whose Kind is NOT here is REJECTED at
        //  load (FlowTrace.Fail + skipped — the Guard pattern; a bad def never grants
        //  power, it is simply dropped from the pack). Stored normalised (lower,
        //  '-'/' ' -> '_') so hyphen/underscore spellings compare equal.
        private static readonly HashSet<string> ConvenienceAllowList = new HashSet<string>
        {
            // PackDef documented set (packs.json _schemaNotes.convenience)
            "instant_build", "instant_repair", "harvest_auto_collect", "xp_weekend",
            // economy-pack extension set (WO economy_store_packs _schemaExtensions)
            "harvest_boost", "instant_fill_storage", "workforce_slot",
            "storage_tier_jump", "offline_window_extension",
            // skr_staking.json convenienceAllowList (loyalty convenience bumps)
            "echo_storage_slot", "passive_accrual_hours",
        };

        private static string NormalizeKind(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            return s.Trim().ToLowerInvariant().Replace('-', '_').Replace(' ', '_');
        }

        /// <summary>Drops any convenience item whose Kind is outside the sanctioned
        /// allowlist (combat/stat/RNG smuggled in as "convenience"). Logs the breach
        /// via FlowTrace.Fail and removes the def so it can never grant power.</summary>
        private static void EnforceCovenant(PackCatalogData data)
        {
            if (data == null || data.Packs == null) return;
            foreach (var pack in data.Packs)
            {
                var conv = pack?.Contents?.Convenience;
                if (conv == null) continue;
                for (int i = conv.Count - 1; i >= 0; i--)
                {
                    var item = conv[i];
                    string norm = item != null ? NormalizeKind(item.Kind) : string.Empty;
                    if (string.IsNullOrEmpty(norm) || !ConvenienceAllowList.Contains(norm))
                    {
                        FlowTrace.Fail("Covenant",
                            $"PackCatalog: pack '{pack?.Sku}' convenience kind '{item?.Kind}' is NOT sanctioned " +
                            "(covenant §5.3 — time-saving only, never combat power) — REJECTED + skipped.");
                        conv.RemoveAt(i);
                    }
                }
            }
        }

        private static void EnsureLoaded()
        {
            if (_data != null) return;
            _data = LoadCatalog();
            EnforceCovenant(_data);
        }

        private static PackCatalogData LoadCatalog()
        {
            // WebGL-safe load via CanonicalJson: Resources.Load first (works in a
            // browser, where File.ReadAllText(streamingAssetsPath) THROWS), then a
            // StreamingAssets fallback on desktop/editor. packs.json is mirrored into
            // Assets/Resources/Data/Canonical/ so the Resources path resolves.
            try
            {
                string json = DeNelle.Core.CanonicalJson.Read(StreamingRelativePath);
                if (!string.IsNullOrEmpty(json))
                {
                    var parsed = JsonConvert.DeserializeObject<PackCatalogData>(json);
                    if (parsed != null && parsed.Packs != null)
                        return parsed;
                    Debug.LogError("[PackCatalog] packs.json parsed empty.");
                }
                else
                {
                    Debug.LogError("[PackCatalog] packs.json not found (Resources or StreamingAssets).");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PackCatalog] Failed to read packs.json: {ex.Message}");
            }

            Debug.LogError("[PackCatalog] packs.json could not be loaded — using an empty catalog.");
            return new PackCatalogData { Packs = new List<PackDef>() };
        }
    }
}
