// =============================================================================
// DungeonComposeLayout — JSON source of truth for Room Forge → DungeonBaker.
// -----------------------------------------------------------------------------
// Parallel to legacy DungeonLayout (healers-cottage wall-run format). This format
// places ROOM PREFABS on a cell grid and mates named sockets door-touch-door.
// Path: StreamingAssets/Data/Canonical/dungeon-layouts/<id>.json
// =============================================================================

using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace DeNelle.Dungeons.RoomForge
{
    /// <summary>Root document for a socket-composed dungeon.</summary>
    [Serializable]
    public sealed class DungeonComposeLayout
    {
        [JsonProperty("dungeonId")] public string dungeonId = "untitled";
        [JsonProperty("cellSize")] public float cellSize = 6f;
        [JsonProperty("rooms")] public List<ComposeRoomPlacement> rooms = new List<ComposeRoomPlacement>();
        [JsonProperty("connections")] public List<ComposeConnection> connections = new List<ComposeConnection>();
        [JsonProperty("rules")] public ComposeRules rules = new ComposeRules();
    }

    /// <summary>One room instance in the layout.</summary>
    [Serializable]
    public sealed class ComposeRoomPlacement
    {
        /// <summary>Prefab stem under Assets/Dungeon/Rooms/ or Resources/Dungeon/Rooms/.</summary>
        [JsonProperty("prefab")] public string prefab;
        /// <summary>Optional instance id when the same prefab appears twice.</summary>
        [JsonProperty("instanceId")] public string instanceId;
        /// <summary>Grid cell position [x, y, z] — y usually 0; z is depth.</summary>
        [JsonProperty("cell")] public int[] cell = new[] { 0, 0, 0 };
        /// <summary>Yaw degrees (0/90/180/270 preferred).</summary>
        [JsonProperty("yawDeg")] public float yawDeg;
        /// <summary>Optional archetype override for pacing lint.</summary>
        [JsonProperty("archetype")] public string archetype;
    }

    /// <summary>Mates socket A on room A to socket B on room B.</summary>
    [Serializable]
    public sealed class ComposeConnection
    {
        [JsonProperty("fromInstance")] public string fromInstance;
        [JsonProperty("fromSocket")] public string fromSocket;
        [JsonProperty("toInstance")] public string toInstance;
        [JsonProperty("toSocket")] public string toSocket;
    }

    /// <summary>Bake / lint rules.</summary>
    [Serializable]
    public sealed class ComposeRules
    {
        [JsonProperty("spineOnly")] public bool spineOnly = true;
        [JsonProperty("minShrines")] public int minShrines = 1;
        [JsonProperty("maxMateDistance")] public float maxMateDistance = 1.25f;
        [JsonProperty("sealUnmated")] public bool sealUnmated = true;
        /// <summary>Combat/lore/reward target ratios (sum ~1). Default 0.6/0.2/0.2.</summary>
        [JsonProperty("pacingCombat")] public float pacingCombat = 0.6f;
        [JsonProperty("pacingLore")] public float pacingLore = 0.2f;
        [JsonProperty("pacingReward")] public float pacingReward = 0.2f;
    }

    /// <summary>Catalog row written by Room Forge Save.</summary>
    [Serializable]
    public sealed class RoomCatalogFile
    {
        [JsonProperty("version")] public int version = 1;
        [JsonProperty("rooms")] public List<RoomCatalogEntry> rooms = new List<RoomCatalogEntry>();
    }

    [Serializable]
    public sealed class RoomCatalogEntry
    {
        [JsonProperty("id")] public string id;
        [JsonProperty("prefabPath")] public string prefabPath;
        [JsonProperty("archetype")] public string archetype;
        [JsonProperty("themePalette")] public string themePalette;
        [JsonProperty("footprintCells")] public int[] footprintCells = new[] { 1, 1 };
        [JsonProperty("cellSize")] public float cellSize = 6f;
        [JsonProperty("sockets")] public List<RoomCatalogSocket> sockets = new List<RoomCatalogSocket>();
    }

    [Serializable]
    public sealed class RoomCatalogSocket
    {
        [JsonProperty("id")] public string id;
        [JsonProperty("type")] public string type;
        [JsonProperty("facing")] public string facing;
        [JsonProperty("isSecret")] public bool isSecret;
        [JsonProperty("localPosition")] public float[] localPosition = new[] { 0f, 0f, 0f };
    }
}
