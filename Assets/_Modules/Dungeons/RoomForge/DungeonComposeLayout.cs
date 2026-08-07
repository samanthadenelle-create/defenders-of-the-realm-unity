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
        // Every canonical catalog carries a top-level "version" (CoreDataHub/data-web oracle) or
        // it fails the gate. The composer never emitted one, so each composed layout had to have
        // it hand-added afterwards - which is silent until someone composes a NEW dungeon and the
        // oracle catches it. Emitting it here fixes that for every future compose.
        [JsonProperty("version")] public int version = 1;
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
        /// <summary>
        /// WO-797: optional per-room enemy encounter. Null (the default, and the state of
        /// every pre-797 layout) = no enemies in this room. Rooms OWN their enemies: the
        /// baker seats one spawner per encounter room and the confine block pins the
        /// spawned mobs to the room's AABB.
        /// </summary>
        [JsonProperty("encounter")] public EncounterSpec encounter;
    }

    /// <summary>
    /// WO-797 per-room encounter block (F8 seq 461/622 "all enemies at the entrance"):
    /// what spawns in a room and how it is confined to it. Authored in the dungeon-graphs
    /// node (or directly in a compose layout room); carried verbatim by
    /// GraphDungeonComposer into the compose layout, consumed by DungeonBaker at bake
    /// and by DungeonRoomBinder at runtime for already-baked scenes.
    /// </summary>
    [Serializable]
    public sealed class EncounterSpec
    {
        /// <summary>Encounter family. "hollow-group" = the weighted skeleton group. "none" disables.</summary>
        [JsonProperty("kind")] public string kind = "hollow-group";
        [JsonProperty("min")] public int min = 3;
        [JsonProperty("max")] public int max = 7;
        /// <summary>Seating shape inside the room ("ring" = the existing jittered formation ring).</summary>
        [JsonProperty("seatMode")] public string seatMode = "ring";
        [JsonProperty("formationRadius")] public float formationRadius = 3.5f;
        [JsonProperty("confine")] public EncounterConfine confine = new EncounterConfine();
    }

    /// <summary>WO-797 confinement rules: pin the room's mobs to the room footprint.</summary>
    [Serializable]
    public sealed class EncounterConfine
    {
        /// <summary>"room" = clamp nav destinations into the room AABB (the only mode v1).</summary>
        [JsonProperty("mode")] public string mode = "room";
        /// <summary>Metres a mob may step OUTSIDE the room AABB (through a doorway) while fighting.</summary>
        [JsonProperty("slack")] public float slack = 2f;
        /// <summary>When leashed out, walk back to the spawn anchor instead of freezing in place.</summary>
        [JsonProperty("returnHome")] public bool returnHome = true;
        /// <summary>Wake distance measured from the ROOM FOOTPRINT (not a ring slot) to the hero.</summary>
        [JsonProperty("wakeRadius")] public float wakeRadius = 6f;
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
