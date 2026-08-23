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
    /// <summary>
    /// The four shelf bands of The Night Market (WO-1050), in their FIXED render order.
    /// <para>⛔ THE ORDER IS NOT A PREFERENCE AND IS NOT SORTED AT RENDER TIME — it is the enum
    /// order, and the shelf walks the enum. <see cref="Free"/> is first because nothing is asked for
    /// before something is given; <see cref="Patronage"/> is last so it is the read on the way out.
    /// Renumbering these values re-orders the store.</para>
    /// </summary>
    public enum StoreBand
    {
        /// <summary>Costs nothing, ever. Never rendered in a colour that also appears on a priced row.</summary>
        Free = 0,
        /// <summary>One resource, nothing else — the curated single-resource impulse rows.</summary>
        Gap = 1,
        /// <summary>Baskets: everything at once.</summary>
        Basket = 2,
        /// <summary>Status, never power.</summary>
        Patronage = 3,
    }

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
        /// <summary>Crystals — the primary build currency.</summary>
        [JsonProperty("crystals")] public int Crystals;
        /// <summary>Food.</summary>
        [JsonProperty("food")] public int Food;
        /// <summary>Coins (Gold).</summary>
        [JsonProperty("coins")] public int Coins;
        /// <summary>Wood - build resource (additive; absent in older packs.json rows = 0, no migration break). Granted via EconomyService.GrantSpendable (ECON-01).</summary>
        [JsonProperty("wood")] public int Wood;
        /// <summary>Iron - build resource (additive; absent = 0). Granted via EconomyService.GrantSpendable (ECON-01).</summary>
        [JsonProperty("iron")] public int Iron;
    }

    /// <summary>
    /// A timed multiplier attached to a convenience item (WO economy_store_packs §2c).
    /// Null on every non-timed kind, and absent from every pack authored before it existed —
    /// additive, so nothing migrates. Wall-clock based by design (the consumer stores an
    /// <c>endsAtUtc</c>), so a boost keeps ticking while the player is offline.
    /// </summary>
    [Serializable]
    public sealed class BoostSpec
    {
        /// <summary>Rate multiplier, e.g. 2.0 for 2x.</summary>
        [JsonProperty("multiplier")] public double Multiplier;
        /// <summary>How long the boost runs, in hours.</summary>
        [JsonProperty("durationHours")] public double DurationHours;
        /// <summary>Which channel it applies to: "all" | "wood" | "iron" | "food" | "crystals".</summary>
        [JsonProperty("appliesTo")] public string AppliesTo;
        /// <summary>Re-buy behaviour while one is already running: "extend" | "refresh" | "reject" | "queue".</summary>
        [JsonProperty("stack")] public string Stack;
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
        /// <summary>Timed-multiplier spec for boost kinds; null for simple count-only kinds.</summary>
        [JsonProperty("boost")] public BoostSpec Boost;
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
        /// <summary>Whether this SKU appears on the browsable Realm Store shelf.</summary>
        [JsonProperty("storeVisible")] public bool StoreVisible = true;

        /// <summary>
        /// Owner ruling 2026-08-21 ("Middle — one impulse tier per resource"): opts a SINGLE
        /// <see cref="Impulse"/> SKU per resource onto the browsable shelf. Every other impulse SKU
        /// stays shortfall-only. Absent = false, so the other nine rows need no edit.
        /// <para>This is DATA, not a SKU list in PackStore.cs, deliberately: which three tiers are
        /// curated is a merchandising decision that will be re-ruled, and a hardcoded list in the
        /// render loop is the thing that goes stale silently. The render loop asks the row.</para>
        /// </summary>
        [JsonProperty("shelfCurated")] public bool ShelfCurated;

        /// <summary>
        /// Retired SKU ids that still resolve to THIS pack (WO-1118 rename migration).
        /// <para>⚠ LOAD-BEARING. <c>sku</c> is a live save key: PackStoreVM.RecordOwned writes it
        /// into <c>GameState.OwnedItemIds</c> and IsOwned reads it back with an ORDINAL
        /// <c>List.Contains</c>. Renaming a SKU with money already spent against the old id
        /// therefore ORPHANS that entitlement — the player silently un-owns what they bought.
        /// Listing the old id here keeps it resolving, forever, with no save rewrite and no schema
        /// bump. NEVER delete an entry from this array; a retired id must stay redeemable for as
        /// long as any save can carry it.</para>
        /// </summary>
        [JsonProperty("legacySkus")] public List<string> LegacySkus = new List<string>();

        /// <summary>True when <paramref name="sku"/> is this pack's current id OR one of its retired ids.</summary>
        public bool MatchesSku(string sku)
        {
            if (string.IsNullOrEmpty(sku)) return false;
            if (string.Equals(Sku, sku, StringComparison.Ordinal)) return true;
            if (LegacySkus == null) return false;
            for (int i = 0; i < LegacySkus.Count; i++)
                if (string.Equals(LegacySkus[i], sku, StringComparison.Ordinal)) return true;
            return false;
        }
        /// <summary>Retention-oriented shelf section: featured / essentials / style / support.</summary>
        /// <remarks>
        /// ⛔ NOT DELETED BY WO-1050, deliberately. <see cref="Band"/> supersedes it as the GROUPING
        /// key, but this field is still the FALLBACK a row without a band resolves through
        /// (<see cref="PackCatalog.BandOf"/>), so deleting it would silently drop any un-migrated row
        /// into the wrong band. Two fields over one decision is duplicated state ONLY when both are
        /// authoritative; here one is authored and the other is a documented fallback.
        /// </remarks>
        [JsonProperty("storeSection")] public string StoreSection = "essentials";

        // ── WO-1050 "The Night Market" PRESENTATION fields (additive; absent = null/false) ──
        // ⚠ EVERY FIELD IN THIS BLOCK DECIDES WHERE OR HOW A PACK IS SHOWN, AND NONE OF THEM CAN
        // CHANGE WHAT IT GRANTS. That distinction is why ImpulsePackRegression's AllowedPackKeys
        // gate accepts them (they cannot be the "grant a finished upgrade" field it exists to catch)
        // and it is the line to hold if this block ever grows: a key that touches contents, pricing
        // or entitlement does NOT belong here.

        /// <summary>
        /// The shelf band this pack lives in: <c>free</c> | <c>gap</c> | <c>basket</c> |
        /// <c>patronage</c>. Replaces <see cref="StoreSection"/>'s mood names as the grouping key.
        /// Absent → resolved from <see cref="StoreSection"/> by <see cref="PackCatalog.BandOf"/>.
        /// </summary>
        [JsonProperty("band")] public string Band;

        /// <summary>
        /// Hex tint (<c>#RRGGBB</c>) for the card's gem. Authored in DATA so a merchandising change
        /// is a data edit, never a code edit. Absent → the band's own light is used.
        /// <para>⚠ DECORATION ONLY. The owner is red/green colourblind: band identity is carried by
        /// the 3 px mark, the text eyebrow and the step in greyscale value, NEVER by this tint. A
        /// card whose meaning depends on its orb colour is a defect.</para>
        /// </summary>
        [JsonProperty("orbTint")] public string OrbTint;

        /// <summary>
        /// The SKU this pack's spotlight comparison line is drawn against. Absent → no line.
        /// <para>The line is PURE ARITHMETIC over two real SKUs (a goods ratio and a price ratio) or
        /// it is not drawn at all — no adjectives, no invented "value index". If the named SKU does
        /// not exist, or the two rows share no economy key, the line is ABSENT.</para>
        /// </summary>
        [JsonProperty("compareTo")] public string CompareTo;

        /// <summary>
        /// A price ANCHOR: the card renders fully priced and <b>no Buy control is ever built</b>, on
        /// either side of the purchase flag. It cannot be bought, so it cannot disappoint.
        /// <para>⚠ NO ROW CARRIES THIS TODAY (WO-1050). The mechanism ships ahead of its data on
        /// purpose: WO-1121 is the SAME-DAY owner ruling that un-hid the $9.99/$19.99/$49.99 ladder
        /// and made those rows buyable behind <c>PurchaseGate</c>'s wallet rule, so flagging any of
        /// them <c>anchorOnly</c> now would walk that ruling back by an authoring edit. Setting this
        /// on a row is an OWNER call.</para>
        /// </summary>
        [JsonProperty("anchorOnly")] public bool AnchorOnly;

        /// <summary>
        /// How much of one economy key this pack grants. Read straight off the contents bag — the
        /// SAME bag <c>PackStoreVM.ApplyPackContents</c> pays out — so the ledger and the comparison
        /// line can never advertise a figure the grant seam does not deliver.
        /// </summary>
        public int EconomyAmount(string key)
        {
            var e = Contents != null ? Contents.Economy : null;
            if (e == null || string.IsNullOrEmpty(key)) return 0;
            switch (key.Trim().ToLowerInvariant())
            {
                case "wood":     return e.Wood;
                case "iron":     return e.Iron;
                case "crystals": return e.Crystals;
                case "food":     return e.Food;
                case "coins":    return e.Coins;
                default:         return 0;
            }
        }
        /// <summary>Short, honest card badge such as "BEST START" or "EXPEDITION".</summary>
        [JsonProperty("storeBadge")] public string StoreBadge;
        /// <summary>Per-currency pricing.</summary>
        [JsonProperty("pricing")] public PackPricing Pricing = new PackPricing();
        /// <summary>The contents bag.</summary>
        [JsonProperty("contents")] public PackContents Contents = new PackContents();
        /// <summary>The single cosmetic SKU exclusive to this pack (§5.1).</summary>
        [JsonProperty("packExclusiveCosmetic")] public string PackExclusiveCosmetic;

        // ── WO-1037 single-resource impulse family (additive; absent = false/null) ──
        // These three fields are the MACHINE-READABLE family tag ShortfallPackOffer resolves on.
        // They are deliberately data, not a name-prefix convention: matching on "impulse-" in the
        // SKU string would silently mis-classify the day someone renames a SKU, and the shortfall
        // surface would then offer a 4-key bundle against a one-resource gap. Older packs omit all
        // three (JSON absent -> false/null), so nothing migrates.

        /// <summary>True for a WO-1037 single-resource impulse SKU (exactly ONE economy key).</summary>
        [JsonProperty("impulse")] public bool Impulse;
        /// <summary>The ONE economy key this impulse pack grants: "wood" / "iron" / "food" / "crystals".</summary>
        [JsonProperty("impulseResource")] public string ImpulseResource;
        /// <summary>"small" / "medium" / "large" — the size rung inside its resource family.</summary>
        [JsonProperty("impulseSize")] public string ImpulseSize;

        /// <summary>
        /// How much of <see cref="ImpulseResource"/> this pack grants (0 when it is not an impulse
        /// pack, or when the tagged resource key carries no amount). Read straight off the contents
        /// bag rather than from a second authored number, so the advertised figure and the granted
        /// figure cannot drift apart.
        /// </summary>
        public int ImpulseAmount
        {
            get
            {
                var e = Contents != null ? Contents.Economy : null;
                if (e == null || string.IsNullOrEmpty(ImpulseResource)) return 0;
                switch (ImpulseResource.Trim().ToLowerInvariant())
                {
                    case "wood":     return e.Wood;
                    case "iron":     return e.Iron;
                    case "food":     return e.Food;
                    case "crystals": return e.Crystals;
                    default:         return 0;
                }
            }
        }

        /// <summary>The native amount payable in the given currency rail.</summary>
        public double AmountFor(CurrencyKind currency)
        {
            if (Pricing == null) return 0d;
            switch (currency)
            {
                case CurrencyKind.Sol: return Pricing.Sol;
                case CurrencyKind.Usdc: return Pricing.Usdc;
                case CurrencyKind.Skr:
                    // ⛔ SERVER-VERIFIED SKUs ARE NEVER REPRICED. The backend checks the on-chain
                    // amount by EXACT EQUALITY against a fixed `amountBaseUnits`
                    // (api/_lib/purchase-catalog.js), so a client that resolves a DIFFERENT number
                    // sends a transfer the server then refuses - and /verify runs AFTER settlement,
                    // so the money is already gone with no entitlement granted. That is the same
                    // paid-but-not-granted shape as the 6-vs-9 decimals near-miss, arriving by a
                    // different door.
                    //
                    // The mainnet canary is the live case and it is NOT hypothetical: it is priced
                    // Usd 0.006 against a server contract of exactly 1_000_000 base units (1 SKR).
                    // ceil(0.006 / skrPrice) is 1 only while SKR trades ABOVE $0.006. Below that it
                    // silently becomes 2 and every purchase breaks. A market move is not a deploy -
                    // nobody would be watching.
                    //
                    // The oracle is for DISPLAY and for rails the server does not pin. A price the
                    // server verifies is a PROTOCOL CONSTANT, not a market quote.
                    if (IsServerPinnedSku(Sku)) return Pricing.Skr;
                    var valued = SkrValuationOracle.SkrForUsd(Pricing.Usd);
                    return valued > 0d ? valued : Pricing.Skr;
                default: return 0d;
            }
        }

        /// <summary>
        /// True when the BACKEND pins this SKU's on-chain amount and verifies it by exact equality
        /// (<c>api/_lib/purchase-catalog.js</c>). Such a price may never be resolved from a market
        /// oracle: client and server must agree to the base unit or the purchase is refused after
        /// the funds have already moved.
        /// <para>⚠ Keep this in step with the server catalog. If a SKU is added there, add it here
        /// in the SAME change - a server-pinned SKU that is missing from this list is a silent
        /// paid-but-not-granted bug that only fires when the market crosses the price.</para>
        /// </summary>
        private static bool IsServerPinnedSku(string sku)
            => string.Equals(sku, MainnetCanaryCatalog.Sku, System.StringComparison.Ordinal)
            || string.Equals(sku, PurchaseGate.DevnetCanarySku, System.StringComparison.Ordinal);

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

        /// <summary>
        /// Looks up a pack by its SKU. Returns null when not found.
        /// <para>Resolves RETIRED ids too (<see cref="PackDef.LegacySkus"/>), so a save, a promo
        /// code (<c>reward_pack_sku</c> → RedeemCodePanel) or a dev grant written against an old id
        /// still lands on the renamed pack. Current ids are matched FIRST across the whole
        /// catalogue before any legacy id is considered, so a live SKU can never be shadowed by
        /// another pack's retired alias.</para>
        /// </summary>
        public static PackDef Find(string sku)
        {
            if (string.IsNullOrEmpty(sku)) return null;
            EnsureLoaded();
            foreach (var pack in _data.Packs)
                if (pack != null && pack.Sku == sku) return pack;
            foreach (var pack in _data.Packs)
                if (pack != null && pack.MatchesSku(sku)) return pack;
            return null;
        }

        /// <summary>
        /// Maps a possibly-retired SKU onto the id the catalogue uses TODAY. Returns the input
        /// unchanged when it is already current or resolves to nothing — callers can apply it
        /// unconditionally. Used by ownership checks so a pre-rename entitlement still reads owned.
        /// </summary>
        public static string ResolveCurrentSku(string sku)
        {
            var pack = Find(sku);
            return pack != null && !string.IsNullOrEmpty(pack.Sku) ? pack.Sku : sku;
        }

        /// <summary>
        /// Every id under which this pack may appear in a save: its current SKU plus every retired
        /// one. An ownership check must test them ALL — a player who bought the pack before the
        /// rename carries only the old string.
        /// </summary>
        public static IEnumerable<string> OwnershipKeysFor(string sku)
        {
            var pack = Find(sku);
            if (pack == null) { yield return sku; yield break; }
            if (!string.IsNullOrEmpty(pack.Sku)) yield return pack.Sku;
            if (pack.LegacySkus == null) yield break;
            foreach (var legacy in pack.LegacySkus)
                if (!string.IsNullOrEmpty(legacy)) yield return legacy;
        }

        // =====================================================================
        //  WO-1050 — band resolution + the ledger key list
        // =====================================================================

        /// <summary>
        /// Every economy key <c>PackStoreVM.ApplyPackContents</c> actually pays out, in grant order.
        /// <para>⛔ THIS LIST IS THE HONESTY CONTRACT OF THE SPOTLIGHT LEDGER. The ledger draws one
        /// bar per key here that the pack carries a non-zero amount of, so a good that IS granted can
        /// never be un-drawn, and a good that is NOT granted can never appear. If the grant seam ever
        /// learns a new currency, add it here in the SAME commit — the same rule that governs
        /// <c>RedeemableConvenienceKinds</c> above.</para>
        /// </summary>
        public static readonly string[] LedgerEconomyKeys =
            { "wood", "iron", "crystals", "food", "coins" };

        /// <summary>
        /// The band a pack renders in. Reads the authored <c>band</c> first and falls back to the
        /// legacy <c>storeSection</c> mapping, so a row that predates WO-1050 still lands somewhere
        /// sane instead of vanishing off the shelf.
        /// </summary>
        public static StoreBand BandOf(PackDef pack)
        {
            if (pack == null) return StoreBand.Basket;

            switch ((pack.Band ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "free":      return StoreBand.Free;
                case "gap":       return StoreBand.Gap;
                case "basket":    return StoreBand.Basket;
                case "patronage": return StoreBand.Patronage;
            }

            // Fallback: the retired mood-named sections. "support" was where the two top rungs of
            // the ladder already lived, so it maps to Patronage; everything else is a basket.
            // An impulse row that somehow lost its band is a Gap row by construction — it grants
            // exactly one resource, which is the definition of the band.
            if (pack.Impulse) return StoreBand.Gap;
            return string.Equals(pack.StoreSection, "support", StringComparison.OrdinalIgnoreCase)
                ? StoreBand.Patronage
                : StoreBand.Basket;
        }

        /// <summary>True when the row carries an explicitly authored, recognised band.</summary>
        public static bool HasAuthoredBand(PackDef pack)
        {
            if (pack == null) return false;
            switch ((pack.Band ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "free": case "gap": case "basket": case "patronage": return true;
                default: return false;
            }
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
            "lantern_oil_2x_expedition", "lantern_oil_3x_expedition",
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

        // =====================================================================
        //  Shelf honesty — REDEEMABLE-TODAY convenience (WO-1118 §0 / §2.3)
        // ---------------------------------------------------------------------
        //  ⚠ THIS IS A DIFFERENT AXIS FROM ConvenienceAllowList ABOVE. Do not merge them.
        //    * ConvenienceAllowList answers "is this kind LEGAL under the covenant?" — it is the
        //      pay-to-win firewall, and every sanctioned kind belongs in it forever.
        //    * This set answers "does anything in the shipped game actually SPEND this token?" —
        //      it is a statement about the CURRENT build, and it shrinks/grows as redeemers land.
        //  A kind can be perfectly legal and still be vapor. Today exactly TWO kinds have a
        //  redeemer: Lantern.cs:405-406 reads GearInventory["convenience:lantern-oil-3x-expedition"]
        //  and ".../-2x-...". Every other kind (instant-build, instant-repair, harvest-auto-collect,
        //  xp-weekend, harvest_boost, instant_fill_storage, workforce_slot, storage_tier_jump,
        //  offline_window_extension) accumulates in GearInventory via PackStoreVM.ApplyPackContents
        //  and is read by NOTHING — advertising it on a card the player can pay for is a refund
        //  problem on a live store, which is the whole of WO-1118.
        //  ⛔ WHEN YOU SHIP A REDEEMER, ADD ITS KIND HERE IN THE SAME COMMIT. Adding the kind here
        //  without a consumer re-creates the exact lie this set exists to stop.
        private static readonly HashSet<string> RedeemableConvenienceKinds = new HashSet<string>
        {
            // Lantern.cs (Dungeons) — consumed per expedition. The ONLY redeemers in the build.
            "lantern_oil_2x_expedition", "lantern_oil_3x_expedition",
        };

        /// <summary>
        /// True when a convenience kind has a live redeemer in THIS build — i.e. the token the
        /// player pays for is actually spendable. The store must not advertise a kind for which
        /// this is false (WO-1118 §2.3). Hyphen and underscore spellings compare equal.
        /// </summary>
        public static bool IsRedeemableConvenience(string kind)
        {
            var norm = NormalizeKind(kind);
            return norm.Length > 0 && RedeemableConvenienceKinds.Contains(norm);
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
#if MAINNET_CANARY_TEST
            // Isolated from canonical production data. The compile symbol is forwarded only to an
            // owner sideload; clean builds cannot load, find, render, or grant this SKU.
            _data.Packs.Add(MainnetCanaryCatalog.Create());
#endif
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
