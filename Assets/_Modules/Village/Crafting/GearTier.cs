// =============================================================================
// GearTier — WO-293 crafting rarity tiers (Common / Fine / Master / Legendary).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.Crafting
//
// The Forgemasters' Saga (DESIGN_FORGEMASTERS_SAGA_LEGENDARY.md) defines a four-rung
// crafting ladder. This file is the PURE DATA + lookup for those rungs:
//   * a stat multiplier applied on top of a base weapon/armor def's stats, and
//   * a visual rarity colour (hex) + display label for the UI.
//
// RECONCILES with Gear v1: weapons.json / armor.json already carry a free-form
// `rarity` STRING ("common"/"uncommon"/"rare"/"epic"). This enum is the CRAFTING
// tier ladder, which is intentionally distinct from that drop-rarity string —
// crafted tiers are the saga's Common->Fine->Master->Legendary. GearTierTable maps
// BOTH the new tier names AND the legacy drop-rarity strings onto a single enum so
// existing data ("uncommon"/"rare"/"epic") still resolves to a sensible tier.
//
// PURE: no MonoBehaviour, no I/O, no cross-assembly calls. ASCII strings only.
// =============================================================================

using System;

namespace DeNelle.Village.Crafting
{
    /// <summary>The four crafting rungs. Order matters (CompareTo gates "needs >= Master").</summary>
    public enum GearTier
    {
        Common = 0,
        Fine = 1,
        Master = 2,
        Legendary = 3
    }

    /// <summary>Pure lookup: tier -> stat multiplier, label, rarity colour, and string parsing.</summary>
    public static class GearTierTable
    {
        /// <summary>
        /// Stat multiplier the tier applies on top of a base def's damageMult / defense.
        /// Common is the baseline (1.0). Tune here, no recompile of data needed.
        /// </summary>
        public static float StatMultiplier(GearTier tier)
        {
            switch (tier)
            {
                case GearTier.Fine:      return 1.25f;
                case GearTier.Master:    return 1.6f;
                case GearTier.Legendary: return 2.2f;
                case GearTier.Common:
                default:                 return 1.0f;
            }
        }

        /// <summary>Player-facing label.</summary>
        public static string DisplayName(GearTier tier)
        {
            switch (tier)
            {
                case GearTier.Fine:      return "Fine";
                case GearTier.Master:    return "Master";
                case GearTier.Legendary: return "Legendary";
                case GearTier.Common:
                default:                 return "Common";
            }
        }

        /// <summary>Visual rarity colour as a hex string (UI tint). No UnityEngine dependency here.</summary>
        public static string RarityHex(GearTier tier)
        {
            switch (tier)
            {
                case GearTier.Fine:      return "#5BC85B"; // green
                case GearTier.Master:    return "#3D7BFF"; // blue
                case GearTier.Legendary: return "#FF9A1F"; // amber/gold
                case GearTier.Common:
                default:                 return "#C8C8C8"; // grey
            }
        }

        /// <summary>
        /// Parse a tier from a string. Accepts the new tier names AND the legacy
        /// Gear-v1 drop-rarity strings so existing weapons.json/armor.json data maps:
        ///   common -> Common, uncommon -> Fine, rare -> Master, epic/legendary -> Legendary.
        /// Unknown/empty -> Common (graceful).
        /// </summary>
        public static GearTier Parse(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return GearTier.Common;
            switch (raw.Trim().ToLowerInvariant())
            {
                case "fine":
                case "uncommon":  return GearTier.Fine;
                case "master":
                case "rare":      return GearTier.Master;
                case "legendary":
                case "epic":      return GearTier.Legendary;
                case "common":
                default:          return GearTier.Common;
            }
        }
    }
}
