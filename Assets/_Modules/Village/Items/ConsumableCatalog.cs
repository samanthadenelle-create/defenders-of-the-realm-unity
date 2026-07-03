// =============================================================================
// ConsumableCatalog - typed model + WebGL-safe loader for consumables.json.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.Items
//
// Mirrors GearCatalog / LootTableCatalog: canonical JSON via DeNelle.Core.CanonicalJson,
// parsed by Newtonsoft.Json. Consumables are CONTENT - add/retune by editing the
// JSON, no recompile. A consumable's id is also its larder key (the same key the
// Workshop recipe outputs), so crafting a "minor-heal-potion" recipe deposits a
// "minor-heal-potion" into VillageInventory, and using one consumes that key.
//
// SHAPE (consumables.json):
//   {
//     "version": 1,
//     "consumables": [
//       {
//         "id": "minor-heal-potion",
//         "displayName": "Minor Healing Draught",
//         "kind": "potion",          // "potion" | "tent" | "food"
//         "effect": "heal",          // "heal" | "mana" | "buff" | "rest"
//         "magnitude": 40,           // heal amount / buff percent / etc.
//         "duration": 0,             // seconds for buffs/food; 0 = instant
//         "usableInFight": true,     // food/potions yes; tent kits no (rest-only)
//         "glyph": "!"
//       }
//     ]
//   }
//
// GRACEFUL: a missing/empty catalog yields no consumables; the use-service simply
// finds nothing to use. The whole feature is gated by ItemDropSystem.Enabled.
// =============================================================================

using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
using DeNelle.Core.State;

namespace DeNelle.Village.Items
{
    /// <summary>The broad family of a consumable (drives where/how it can be used).</summary>
    public enum ConsumableKind { Potion, Tent, Food, Unknown }

    /// <summary>The effect a consumable applies on use.</summary>
    public enum ConsumableEffect { Heal, Mana, Buff, Rest, None }

    [Serializable]
    public sealed class ConsumableDef
    {
        [JsonProperty("id")] public string Id;
        [JsonProperty("displayName")] public string DisplayName;
        [JsonProperty("kind")] public string KindRaw;       // "potion" | "tent" | "food"
        [JsonProperty("effect")] public string EffectRaw;   // "heal" | "mana" | "buff" | "rest"
        [JsonProperty("magnitude")] public float Magnitude;
        // WO3: percent-of-max effect size (0..100). When > 0 it drives heal/mana as a
        // PERCENT of the hero's effective max (not the flat `magnitude`); `magnitude`
        // stays as the fallback. Default 0 = use the flat magnitude path.
        [JsonProperty("magnitudePct")] public float MagnitudePct;
        [JsonProperty("duration")] public float Duration;   // seconds; 0 = instant
        [JsonProperty("usableInFight")] public bool UsableInFight = true;
        [JsonProperty("glyph")] public string Glyph;
        // WO-598: authored gold BUY price at goods vendors (the Market shelf). 0/absent =
        // VendorStockResolver.PriceFor's kind default. Shelf price is CONTENT, not code.
        [JsonProperty("price")] public int Price;

        // WO-Item-1: the catalog⊥repo LOOK half (docs/ITEM_MODEL.md §3). prefabPath =
        // the world/loot model, iconPath = the inventory/store sprite. Both NULL for now
        // — WO-Item-2's generator populates them; do NOT hand-author here. The legacy
        // `glyph` stays the v1 placeholder.
        [JsonProperty("prefabPath")] public string PrefabPath;
        [JsonProperty("iconPath")] public string IconPath;

        // WO-Item-1: OPTIONAL explicit capability override from JSON (null when absent);
        // when absent the Consumable default (Carriable|Usable) applies. See Capabilities.
        [JsonProperty("capabilities")] public ItemCapability? CapabilitiesRaw;

        /// <summary>WO-Item-1: the entry's resolved capability flags. A Consumable defaults to
        /// Carriable|Usable (docs/ITEM_MODEL.md §2/§3); an explicit JSON `capabilities`
        /// override wins when present. Systems read THIS, never the catalog-of-origin.</summary>
        public ItemCapability Capabilities =>
            CapabilitiesRaw ?? (ItemCapability.Carriable | ItemCapability.Usable);

        public ConsumableKind Kind
        {
            get
            {
                if (string.Equals(KindRaw, "potion", StringComparison.OrdinalIgnoreCase)) return ConsumableKind.Potion;
                if (string.Equals(KindRaw, "tent", StringComparison.OrdinalIgnoreCase)) return ConsumableKind.Tent;
                if (string.Equals(KindRaw, "food", StringComparison.OrdinalIgnoreCase)) return ConsumableKind.Food;
                return ConsumableKind.Unknown;
            }
        }

        public ConsumableEffect Effect
        {
            get
            {
                if (string.Equals(EffectRaw, "heal", StringComparison.OrdinalIgnoreCase)) return ConsumableEffect.Heal;
                if (string.Equals(EffectRaw, "mana", StringComparison.OrdinalIgnoreCase)) return ConsumableEffect.Mana;
                if (string.Equals(EffectRaw, "buff", StringComparison.OrdinalIgnoreCase)) return ConsumableEffect.Buff;
                if (string.Equals(EffectRaw, "rest", StringComparison.OrdinalIgnoreCase)) return ConsumableEffect.Rest;
                return ConsumableEffect.None;
            }
        }
    }

    [Serializable]
    public sealed class ConsumableData
    {
        [JsonProperty("version")] public int Version;
        [JsonProperty("consumables")] public List<ConsumableDef> Consumables = new List<ConsumableDef>();
    }

    /// <summary>Static loader + query surface for consumable definitions.</summary>
    public static class ConsumableCatalog
    {
        private const string CanonicalRelativePath = "Data/Canonical/consumables.json";
        private static ConsumableData _data;

        public static void Reload() { _data = null; EnsureLoaded(); }

        public static IReadOnlyList<ConsumableDef> All { get { EnsureLoaded(); return _data.Consumables; } }

        public static ConsumableDef Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            EnsureLoaded();
            foreach (var c in _data.Consumables)
                if (c != null && c.Id == id) return c;
            return null;
        }

        /// <summary>True when the id resolves to a known consumable.</summary>
        public static bool IsConsumable(string id) => Find(id) != null;

        private static void EnsureLoaded()
        {
            if (_data != null) return;
            try
            {
                string json = DeNelle.Core.CanonicalJson.Read(CanonicalRelativePath);
                if (!string.IsNullOrEmpty(json))
                {
                    var parsed = JsonConvert.DeserializeObject<ConsumableData>(json);
                    if (parsed != null) { _data = parsed; if (_data.Consumables == null) _data.Consumables = new List<ConsumableDef>(); return; }
                }
                Debug.LogWarning("[ConsumableCatalog] consumables.json not found (Resources or StreamingAssets) - consumables disabled.");
            }
            catch (Exception ex)
            {
                Debug.LogError("[ConsumableCatalog] Read failed: " + ex.Message);
            }
            _data = new ConsumableData();
        }
    }
}
