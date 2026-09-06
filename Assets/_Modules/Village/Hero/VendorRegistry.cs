// =============================================================================
// VendorRegistry — typed model + WebGL-safe loader for vendors.json (WO-598).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// THE VENDOR/SHOPS REGISTRY: each shoppable vendor (buildings.json isShoppable —
// forge / armorer / market / jeweler) declares its STOCK QUERY as data:
//   • categories   — which catalog bands the shelf pulls (weapon/armor/consumable/
//                    material/ring/amulet/gem/craftable). A shelf is a QUERY over
//                    the item catalogs, never a hardcoded list (owner data-structure
//                    doctrine; WO-598 "the honest shelf").
//   • classFilter  — "roster" (default) drops items NO currently-playable class can
//                    use (Knight-only V1 via ff.knightonly ⇒ no Mage wands at the
//                    Forge — the flag_08 fix). "none" disables the roster gate.
//   • maxReqLevel  — 0 = uncapped; N = don't stock items requiring level > N.
//   • emptyLine    — the AUTHORED never-raw empty-state line (flag_11 fix: a vendor
//                    never renders "No wares in stock." raw).
//   • layout       — gear | goods | jeweler: which shelf PRESENTATION the shop binds
//                    (Market = flat goods list, NO equip tabs/paper-doll — flag_03).
//
// Mirrors ConsumableCatalog / MaterialCatalog EXACTLY: canonical JSON via
// DeNelle.Core.CanonicalJson (Resources first, StreamingAssets fallback), parsed by
// Newtonsoft.Json. GRACEFUL: a missing/empty registry yields no vendors and every
// consumer falls back to the legacy VendorStockContract heuristic — never throws.
//
// ONE TRUTH: VendorStockContract.AllowedFor consults this registry FIRST (categories
// → GearKind flags), so ShopCatalog, PartyShopVM AND the AutoPilot vendor oracle all
// read the same mapping. (The legacy ShopPanel/ShopVM pair read it too, until both were
// DELETED as doorless panels on 2026-09-06 - WO-1430 Group A.) VendorStockResolver reads the
// full query (roster/level/emptyLine/layout) for the MVVM shelf.
// =============================================================================

using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>One vendor's declared stock query (see vendors.json _schemaNotes).</summary>
    [Serializable]
    public sealed class VendorDef
    {
        [JsonProperty("id")] public string Id;
        [JsonProperty("displayName")] public string DisplayName;
        [JsonProperty("layout")] public string Layout;              // "gear" | "goods" | "jeweler"
        [JsonProperty("categories")] public List<string> Categories = new List<string>();
        [JsonProperty("classFilter")] public string ClassFilter;    // "roster" (default) | "none"
        [JsonProperty("maxReqLevel")] public int MaxReqLevel;       // 0 = uncapped
        [JsonProperty("emptyLine")] public string EmptyLine;

        // ── WO-860 Part B — the THINNED shelf (all four are DATA, not constants, so the
        //    owner can retune the store without a recompile). Every one defaults to the
        //    pre-860 behaviour when absent, so an unmigrated/3rd-party vendor row is unchanged.

        /// <summary>
        /// true = HIDE rows the shopper cannot equip right now (wrong class / under-level)
        /// instead of listing them LOCKED. Default false = the pre-860 "aspiration" shelf.
        /// </summary>
        [JsonProperty("onlyEquippable")] public bool OnlyEquippable;

        /// <summary>
        /// Max weapon/armor rows to stock PER REQUIRED LEVEL (owner: "only 2 options on each
        /// new level"). 0 = uncapped (pre-860). See VendorStockResolver.ApplyPerLevelCap for
        /// the documented sort that decides WHICH survive.
        /// </summary>
        [JsonProperty("perLevelCap")] public int PerLevelCap;

        /// <summary>
        /// WO-960 locked PREVIEW window (owner: "display as greyed out with lvl and only show
        /// ones in the next 5 levels"). N &gt; 0 = a class-appropriate row locked ONLY by level
        /// still shows LOCKED when its req.level is within the shopper's next N levels
        /// (req in (level, level+N]); deeper future rows stay hidden. Under onlyEquippable this
        /// RE-ADMITS the near-future slice of the ladder; on the aspiration shelf it CLAMPS the
        /// level-locked rows to the same window. 0/absent = pre-960 behaviour exactly.
        /// </summary>
        [JsonProperty("lockedPreviewLevels")] public int LockedPreviewLevels;

        /// <summary>
        /// Item-id prefixes this vendor never stocks (case-insensitive). Used to keep the
        /// ~65 "blink_*" placeholder rows — art-pack filler that is real in the catalog but
        /// not authored content — off the player-facing shelf without editing the catalogs
        /// (WO-860 "do NOT edit the weapon/armor catalogs to fix the overload").
        /// Empty/absent = stock everything, exactly as before.
        /// </summary>
        [JsonProperty("excludeIdPrefixes")] public List<string> ExcludeIdPrefixes = new List<string>();

        /// <summary>
        /// The "come back after levelling for new stock" line shown UNDER a NON-EMPTY capped
        /// list (the case a player hits most once the cap is on: they have wares, better ones
        /// unlock later). Distinct from <see cref="EmptyLine"/>, which covers 0 results.
        /// </summary>
        [JsonProperty("footerLine")] public string FooterLine;
    }

    [Serializable]
    public sealed class VendorData
    {
        [JsonProperty("version")] public int Version;
        [JsonProperty("vendors")] public List<VendorDef> Vendors = new List<VendorDef>();
    }

    /// <summary>Static loader + query surface for the vendor/shops registry (vendors.json).</summary>
    public static class VendorRegistry
    {
        private const string CanonicalRelativePath = "Data/Canonical/vendors.json";
        private static VendorData _data;

        public static void Reload() { _data = null; EnsureLoaded(); }

        /// <summary>All registered vendors. Never null (empty when the JSON is absent/broken).</summary>
        public static IReadOnlyList<VendorDef> All { get { EnsureLoaded(); return _data.Vendors; } }

        /// <summary>
        /// Resolve a vendor by context. EXACT id match first (the structureId path —
        /// "market"/"forge"/"armorer"/"jeweler"), then a substring match so composite
        /// contexts ("blacksmith_forge", "marketplace") still route to their trade.
        /// Null when nothing matches (callers fall back to the legacy heuristic).
        /// </summary>
        public static VendorDef Find(string vendorContext)
        {
            if (string.IsNullOrEmpty(vendorContext)) return null;
            EnsureLoaded();
            string ctx = vendorContext.Trim().ToLowerInvariant();

            foreach (var v in _data.Vendors)
                if (v != null && !string.IsNullOrEmpty(v.Id) && v.Id.ToLowerInvariant() == ctx)
                    return v;

            foreach (var v in _data.Vendors)
                if (v != null && !string.IsNullOrEmpty(v.Id) && ctx.Contains(v.Id.ToLowerInvariant()))
                    return v;

            return null;
        }

        /// <summary>The authored empty-shelf line for a vendor, or null when unregistered/unauthored.</summary>
        public static string EmptyLineFor(string vendorContext)
        {
            var v = Find(vendorContext);
            return v != null && !string.IsNullOrEmpty(v.EmptyLine) ? v.EmptyLine : null;
        }

        /// <summary>WO-860: the authored under-a-non-empty-list footer ("come back after you
        /// level"), or null when unregistered/unauthored. Null means "render no footer".</summary>
        public static string FooterLineFor(string vendorContext)
        {
            var v = Find(vendorContext);
            return v != null && !string.IsNullOrEmpty(v.FooterLine) ? v.FooterLine : null;
        }

        private static void EnsureLoaded()
        {
            if (_data != null) return;
            try
            {
                string json = DeNelle.Core.CanonicalJson.Read(CanonicalRelativePath);
                if (!string.IsNullOrEmpty(json))
                {
                    var parsed = JsonConvert.DeserializeObject<VendorData>(json);
                    if (parsed != null)
                    {
                        _data = parsed;
                        if (_data.Vendors == null) _data.Vendors = new List<VendorDef>();
                        return;
                    }
                }
                Debug.LogWarning("[VendorRegistry] vendors.json not found (Resources or StreamingAssets) - vendor queries fall back to the legacy contract heuristic.");
            }
            catch (Exception ex)
            {
                Debug.LogError("[VendorRegistry] Read failed: " + ex.Message);
            }
            _data = new VendorData();
        }
    }
}
