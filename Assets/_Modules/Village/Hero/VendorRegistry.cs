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
// → GearKind flags), so the legacy ShopPanel/ShopVM, ShopCatalog, PartyShopVM AND the
// AutoPilot vendor oracle all read the same mapping. VendorStockResolver reads the
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
