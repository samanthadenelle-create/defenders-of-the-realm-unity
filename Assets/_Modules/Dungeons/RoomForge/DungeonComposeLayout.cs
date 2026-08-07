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
        /// <summary>Metres per grid cell for the authored <c>cell</c> coords. Defaults to the kit
        /// canon (WO-922: 10, was 6); GraphDungeonComposer emits 1 and puts solved world ints in
        /// <c>cell</c>, so this only bites hand-authored layouts that omit the field.</summary>
        [JsonProperty("cellSize")] public float cellSize = RoomForgeCanon.Cell;
        [JsonProperty("rooms")] public List<ComposeRoomPlacement> rooms = new List<ComposeRoomPlacement>();
        [JsonProperty("connections")] public List<ComposeConnection> connections = new List<ComposeConnection>();
        [JsonProperty("rules")] public ComposeRules rules = new ComposeRules();
        /// <summary>WO-1001 slice 5: oil refill points for composed lantern drain.</summary>
        [JsonProperty("oilStones")] public List<ComposeOilStone> oilStones = new List<ComposeOilStone>();
        /// <summary>WO-1001 slice 7: step-on traps.</summary>
        [JsonProperty("traps")] public List<ComposeTrap> traps = new List<ComposeTrap>();
        /// <summary>WO-1001 slice 7: key pickups (run-local).</summary>
        [JsonProperty("keys")] public List<ComposeKey> keys = new List<ComposeKey>();
        /// <summary>WO-1001 slice 7: locked ports (need keyId to pass).</summary>
        [JsonProperty("locks")] public List<ComposeLock> locks = new List<ComposeLock>();
        /// <summary>WO-1001 slice 8: extra extract/exit points (per-floor bank-and-leave).</summary>
        [JsonProperty("extracts")] public List<ComposeExtract> extracts = new List<ComposeExtract>();
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
        /// <summary>WO-1001 slice 4: breakable chests / crates with loot tables.</summary>
        [JsonProperty("chests")] public List<ComposeChest> chests;
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
        /// <summary>WO-1001 slice 3: single elite/boss spawn (forces count=1).</summary>
        [JsonProperty("isBoss")] public bool isBoss;
        /// <summary>WO-1001 slice 3: optional fixed enemies.json id for the boss/elite.</summary>
        [JsonProperty("enemyType")] public string enemyType;
        /// <summary>Optional display name for boss intro cards (future HUD).</summary>
        [JsonProperty("displayName")] public string displayName;
    }

    /// <summary>WO-1001 slice 4: one breakable loot prop in a room.</summary>
    [Serializable]
    public sealed class ComposeChest
    {
        [JsonProperty("id")] public string id;
        /// <summary>loot-tables.json key (e.g. dungeon-chest, dungeon-deepboss, crate-common).</summary>
        [JsonProperty("lootTableId")] public string lootTableId = "dungeon-chest";
        /// <summary>Visual token: chest / crate / barrel.</summary>
        [JsonProperty("visual")] public string visual = "chest";
        /// <summary>Optional local offset from room centre [x,y,z]. Default = room centre.</summary>
        [JsonProperty("offset")] public float[] offset;
    }

    /// <summary>WO-1001 slice 5: lantern refill stone (world coords after compose, or cell-local).</summary>
    [Serializable]
    public sealed class ComposeOilStone
    {
        [JsonProperty("id")] public string id;
        /// <summary>Room instance id — stone is placed near that room's centre when set.</summary>
        [JsonProperty("roomId")] public string roomId;
        /// <summary>World offset from room centre, or absolute if roomId empty [x,y,z].</summary>
        [JsonProperty("offset")] public float[] offset;
        [JsonProperty("radius")] public float radius = 2.5f;
    }

    /// <summary>WO-1001 slice 7: floor trap (spike/grate).</summary>
    [Serializable]
    public sealed class ComposeTrap
    {
        [JsonProperty("id")] public string id;
        [JsonProperty("roomId")] public string roomId;
        [JsonProperty("kind")] public string kind = "spike"; // spike | grate
        [JsonProperty("damage")] public float damage = 12f;
        [JsonProperty("radius")] public float radius = 1.4f;
        [JsonProperty("offset")] public float[] offset;
    }

    /// <summary>WO-1001 slice 7: key pickup.</summary>
    [Serializable]
    public sealed class ComposeKey
    {
        [JsonProperty("id")] public string id;
        [JsonProperty("keyId")] public string keyId = "crypt-key";
        [JsonProperty("roomId")] public string roomId;
        [JsonProperty("offset")] public float[] offset;
    }

    /// <summary>WO-1001 slice 7: locked traversal to a target room centre.</summary>
    [Serializable]
    public sealed class ComposeLock
    {
        [JsonProperty("id")] public string id;
        [JsonProperty("keyId")] public string keyId = "crypt-key";
        [JsonProperty("fromRoomId")] public string fromRoomId;
        [JsonProperty("toRoomId")] public string toRoomId;
        [JsonProperty("fromOffset")] public float[] fromOffset;
        [JsonProperty("toOffset")] public float[] toOffset;
    }

    /// <summary>WO-1001 slice 8: bank-and-leave extract point (mirrors entry exit).</summary>
    [Serializable]
    public sealed class ComposeExtract
    {
        [JsonProperty("id")] public string id;
        [JsonProperty("roomId")] public string roomId;
        [JsonProperty("offset")] public float[] offset;
        [JsonProperty("label")] public string label = "Extract";
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
        /// <summary>Metres per cell this room was forged at. Kit canon (WO-922: 10, was 6).</summary>
        [JsonProperty("cellSize")] public float cellSize = RoomForgeCanon.Cell;
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
