// =============================================================================
// WallTierData — the cosmetic + economic data for the CoC wall ladder.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.Walls
//
// RECONCILE-FIRST (CLAUDE.md): WallSegment ALREADY owns the durability half of the
// tier system — `_tier` (1..3) + `s_tierToughness {_,1,1.6,2.56}` (incoming contact
// damage is DIVIDED by toughness, ~x1.6 effective-HP per tier) + StructureTierVisual
// (accent tint) + GameState.WallLevel (persisted). This file does NOT duplicate any
// of that. It adds only what was MISSING for the owner's 2026-06-13 ladder:
//   - the tier NAMING (Wood -> Iron -> Reinforced Steel, replacing the old
//     wood/stone/reinforced labels),
//   - the per-tier SEGMENT MESH prefab path (owner art, pending import), and
//   - the per-tier UPGRADE COST (Iron, then Iron + Crystals for the rune-temper).
// Tier index matches WallSegment: 1 = Wood, 2 = Iron, 3 = Reinforced Steel.
// Durability stays in WallSegment.s_tierToughness (single source) — exposed here
// as ToughnessFor() for read-only convenience, NOT redefined.
//
// Pure data — no scene, no save-schema, no mesh dependency (prefab paths are
// placeholders until the owner's meshes land; see docs/PLAN_grid_coc_base_walls.md).
// =============================================================================

using UnityEngine;

namespace DeNelle.Village.Walls
{
    /// <summary>The three wall material tiers. Index matches WallSegment._tier (1..3).</summary>
    public enum WallTier
    {
        Wood = 1,
        Iron = 2,
        ReinforcedSteel = 3,
    }

    /// <summary>A tier's display name, segment mesh, and the cost to upgrade INTO it.</summary>
    public readonly struct WallTierDef
    {
        public readonly WallTier Tier;
        public readonly string DisplayName;
        // Cost to UPGRADE INTO this tier (per segment). Wood is the base tier (no
        // upgrade-in cost; placing a fresh wood wall uses BuildWoodCost).
        public readonly int UpgradeWood;
        public readonly int UpgradeIron;
        public readonly int UpgradeCrystals;
        // Resources.Load path to this tier's straight wall-segment prefab (owner art, pending).
        public readonly string SegmentPrefabPath;

        public WallTierDef(WallTier tier, string name,
                           int upWood, int upIron, int upCrystals, string prefabPath)
        {
            Tier = tier; DisplayName = name;
            UpgradeWood = upWood; UpgradeIron = upIron; UpgradeCrystals = upCrystals;
            SegmentPrefabPath = prefabPath;
        }
    }

    /// <summary>Curated wall-tier ladder (cosmetic + cost). Durability = WallSegment toughness.</summary>
    public static class WallTierData
    {
        public const int MinTier = 1;                 // Wood
        public const int MaxTier = 3;                 // Reinforced Steel
        public const int TierCount = 3;
        /// <summary>Cost (Wood) to BUILD a fresh Wood wall segment (Phase 2 player placement).</summary>
        public const int BuildWoodCost = 25;

        // Mirror of WallSegment.s_tierToughness (effective-HP multiplier per tier). Kept here
        // as a read-only convenience for UI; WallSegment remains the authority that APPLIES it.
        private static readonly float[] s_toughness = { 1f, 1f, 1.6f, 2.56f }; // [_, wood, iron, steel]

        private static readonly WallTierDef[] _tiers =
        {
            // index 0 unused (tiers are 1..3, matching WallSegment._tier).
            default,
            new WallTierDef(WallTier.Wood, "Wood Palisade",
                            upWood: 0, upIron: 0, upCrystals: 0,
                            prefabPath: "Walls/wood_wall"),      // owner art, imported 2026-06-12
            new WallTierDef(WallTier.Iron, "Iron-Banded Wall",
                            upWood: 0, upIron: 120, upCrystals: 0,
                            prefabPath: "Walls/iron_wall"),      // owner art, imported 2026-06-12
            // Reinforced Steel — rune-tempered. Iron + Crystals (the magic-temper arc).
            new WallTierDef(WallTier.ReinforcedSteel, "Reinforced Steel",
                            upWood: 0, upIron: 200, upCrystals: 40,
                            prefabPath: "Walls/steel_wall"),     // PENDING owner art (runic steel)
        };

        /// <summary>The tier def for a 1..3 index (clamped).</summary>
        public static WallTierDef Get(int tier) => _tiers[Mathf.Clamp(tier, MinTier, MaxTier)];

        /// <summary>The tier def for a WallTier.</summary>
        public static WallTierDef Get(WallTier tier) => Get((int)tier);

        /// <summary>True if this tier can still be upgraded (not yet at the top).</summary>
        public static bool CanUpgrade(int tier) => tier < MaxTier;

        /// <summary>The next tier up (clamped at the max).</summary>
        public static WallTierDef Next(int tier) => Get(Mathf.Min(tier + 1, MaxTier));

        /// <summary>Effective-HP multiplier at a tier (read-only mirror of WallSegment toughness).</summary>
        public static float ToughnessFor(int tier) => s_toughness[Mathf.Clamp(tier, MinTier, MaxTier)];
    }
}
