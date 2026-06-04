// =============================================================================
// RegionZone — the outer-world region model (WO-142 / WO-107). Pure data, lives
// in DeNelle.Core so harvest nodes (WO-141), raids (WO-143), and regional crystal
// grades (WO-144) can all classify a world position into ONE shared region set
// without referencing Village.
// -----------------------------------------------------------------------------
// The four regions map onto ExteriorTerrainBuilder's existing directional biomes
// (N forest / E farmland / S barren-Wound / W river valley) and carry the
// warmth-in / dread-out danger dial: Goldfields (E, safe) -> Stoneback (W) ->
// Mirewood (S) -> Ashwood (N, the ruined front line nearest the Wound).
// =============================================================================
using System;
using System.Collections.Generic;

namespace DeNelle.Core.World
{
    /// <summary>
    /// The outer-world regions. Order follows the danger dial (Village = 0 safest
    /// → Ashwood = most dangerous). Stable enum — append new regions, never
    /// renumber (saved data / spawn rules key off these).
    /// </summary>
    public enum RegionId
    {
        /// <summary>Inside the walls — the safe home zone (not an outer region).</summary>
        Village = 0,
        /// <summary>East — peopled breadbasket farmland. Safest outer region (DangerTier 1).</summary>
        Goldfields = 1,
        /// <summary>West — old stony river-valley uplands. Neutral (DangerTier 2).</summary>
        Stoneback = 2,
        /// <summary>South — the drowned valley toward the Wound. Heavy (DangerTier 3).</summary>
        Mirewood = 3,
        /// <summary>North — the ruined front line nearest the Wound. Most dangerous (DangerTier 4).</summary>
        Ashwood = 4,
    }

    /// <summary>Static facts about a region — danger tier, display name, cardinal sign.</summary>
    public sealed class RegionZone
    {
        public RegionId Id;
        public string   DisplayName;
        /// <summary>1 (safe) … 4 (deadly). Village = 0. Drives raid size (WO-143) and crystal grade (WO-144).</summary>
        public int      DangerTier;
        /// <summary>Cardinal direction of the region from the village centre (for the terrain-biome map).</summary>
        public string   Cardinal;

        public RegionZone(RegionId id, string displayName, int dangerTier, string cardinal)
        {
            Id = id;
            DisplayName = displayName;
            DangerTier = dangerTier;
            Cardinal = cardinal;
        }
    }

    /// <summary>
    /// What a zone resolves to in the City↔Horde rhythm (WO-164 zone graph). Low-danger
    /// zones read as friendly <see cref="City"/> destinations; high-danger zones read as
    /// hostile <see cref="Horde"/> staging grounds; <see cref="Neutral"/> is the in-between
    /// (and the safe home Village). Largely derivable from <see cref="RegionZone.DangerTier"/>,
    /// but stored explicitly so designers can override per zone. Stable enum — append, never
    /// renumber (persisted via <see cref="ZoneState"/>).
    /// </summary>
    public enum NodeType
    {
        /// <summary>Safe / in-between zone (also the home Village). Default.</summary>
        Neutral = 0,
        /// <summary>Peopled settlement to visit / trade / quest (low danger).</summary>
        City = 1,
        /// <summary>Hostile staging ground — raids and horde pressure (high danger).</summary>
        Horde = 2,
    }

    /// <summary>
    /// Per-region runtime/persisted state record (WO-164). The static <see cref="RegionZone"/>
    /// carries immutable facts (danger tier, name); <see cref="ZoneState"/> carries the
    /// mutable, player-specific spine the world features hang off: discovery/clear flags, the
    /// neighbor graph edge list, and the City/Horde destination tag. Later node/tribe/settlement
    /// sub-records (WO-155/159/160) attach here.
    /// <para>
    /// JsonUtility-safe (public fields, no dictionaries, enum serialized as int) so it
    /// round-trips directly inside <c>GameState.Zones</c>. <see cref="RegionKey"/> mirrors the
    /// <see cref="RegionId"/> enum NAME (not its int), matching <c>RegionProgress</c>'s
    /// string-keyed convention so saves survive enum renumbering.
    /// </para>
    /// </summary>
    [Serializable]
    public class ZoneState
    {
        /// <summary>Stable region key — the <see cref="RegionId"/> enum name (e.g. "Ashwood").</summary>
        public string RegionKey = "";
        /// <summary>True once the player has entered this zone.</summary>
        public bool Discovered;
        /// <summary>True once the zone's objective (boss / horde) is defeated.</summary>
        public bool Cleared;
        /// <summary>Adjacent zones in the world graph — each entry is a <see cref="RegionId"/> enum name.</summary>
        public List<string> Neighbors = new List<string>();
        /// <summary>What this zone resolves to in the City↔Horde rhythm.</summary>
        public NodeType Destination = NodeType.Neutral;

        public ZoneState() { }

        public ZoneState(RegionId id, NodeType destination, params RegionId[] neighbors)
        {
            RegionKey = id.ToString();
            Destination = destination;
            Neighbors = new List<string>();
            if (neighbors != null)
            {
                for (int i = 0; i < neighbors.Length; i++)
                    Neighbors.Add(neighbors[i].ToString());
            }
        }
    }
}
