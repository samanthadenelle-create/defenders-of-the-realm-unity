// =============================================================================
// DungeonLayout — typed records for a dungeon-layout JSON file + its loader.
// -----------------------------------------------------------------------------
// Port spec Part 4 (data-extraction protocol): the canonical dungeon layout
// lives in StreamingAssets/Data/Canonical/dungeons/healers-cottage.json. This
// file is the strongly-typed C# mirror — Newtonsoft.Json deserialises the JSON
// into a DungeonLayout, and DungeonController hydrates the scene from it.
//
// JSON is the source of truth (port spec Part 4 "JSON wins"). These records are
// PLAIN C# — no MonoBehaviour, no ScriptableObject — so they unit-test cleanly
// and the same shape can be reused by future dungeons (Apothecary's Vault, etc).
//
// Coordinate convention (matches the JSON's _schemaNotes): X east(+)/west(-),
// Z south(+)/north(-), Y up. ~6u room cells. The Keeper enters at the SW
// Garden Approach.
// =============================================================================

using System;
using System.IO;
using Cysharp.Threading.Tasks;
using DeNelle.Core.Diagnostics;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace DeNelle.Dungeons
{
    /// <summary>A 2D point on the dungeon floor plane (X east, Z south).</summary>
    [Serializable]
    public struct DungeonPointXZ
    {
        public float x;
        public float z;

        /// <summary>The point as a world-space <see cref="Vector3"/> at Y = 0.</summary>
        public Vector3 ToWorld() => new Vector3(x, 0f, z);
    }

    /// <summary>A 3D point in the dungeon (X east, Y up, Z south).</summary>
    [Serializable]
    public struct DungeonPoint
    {
        public float x;
        public float y;
        public float z;

        /// <summary>The point as a world-space <see cref="Vector3"/>.</summary>
        public Vector3 ToWorld() => new Vector3(x, y, z);
    }

    /// <summary>An axis-aligned room footprint on the floor plane.</summary>
    [Serializable]
    public sealed class DungeonBounds
    {
        public DungeonPointXZ min;
        public DungeonPointXZ max;

        /// <summary>The room's centre point on the floor plane.</summary>
        public Vector3 Center =>
            new Vector3((min.x + max.x) * 0.5f, 0f, (min.z + max.z) * 0.5f);

        /// <summary>True when <paramref name="p"/> falls inside the footprint.</summary>
        public bool Contains(Vector3 p) =>
            p.x >= min.x && p.x <= max.x && p.z >= min.z && p.z <= max.z;
    }

    /// <summary>
    /// One wall run. <c>kind</c> is <c>"solid"</c> (collidable), <c>"doorway"</c>
    /// (collidable frame + walk-through gap, wires room-to-room) or
    /// <c>"illusory"</c> (renders as a wall but the scene builds NO collider —
    /// the Keeper walks through into the secret room named by <c>leadsTo</c>).
    /// </summary>
    [Serializable]
    public sealed class DungeonWall
    {
        public string kind = "solid";
        public DungeonPointXZ start;
        public DungeonPointXZ end;
        public string assetKey = "wall";
        public string leadsTo;

        /// <summary>True for an illusory (no-collider) wall — see <see cref="kind"/>.</summary>
        public bool IsIllusory => kind == "illusory";

        /// <summary>True for a doorway — a collidable frame around a walk-through gap.</summary>
        public bool IsDoorway => kind == "doorway";
    }

    /// <summary>One room of the dungeon — a footprint, a floor, and its walls.</summary>
    [Serializable]
    public sealed class DungeonRoom
    {
        public string id;
        public string title;
        public string kind = "standard";
        public bool secret;
        public DungeonBounds bounds;
        public string floorAssetKey;
        public DungeonWall[] walls = Array.Empty<DungeonWall>();
    }

    /// <summary>The Keeper's spawn placement at the dungeon entrance.</summary>
    [Serializable]
    public sealed class DungeonSpawn
    {
        public string roomId;
        public DungeonPoint position;
        public float facingY;
    }

    /// <summary>A tap-to-read lore stone — body keyed to data/lore-fragments.json.</summary>
    [Serializable]
    public sealed class DungeonLoreStone
    {
        public string id;
        public string roomId;
        public DungeonPoint position;
        public string assetKey;
        public string title;
        public string[] body = Array.Empty<string>();
    }

    /// <summary>A checkpoint shrine — heals + saves on first proximity.</summary>
    [Serializable]
    public sealed class DungeonCheckpoint
    {
        public string id;
        public string roomId;
        public DungeonPoint position;
        public float radius = 2f;
        public string toast;
    }

    /// <summary>A scripted, room-placed ATB encounter — fires once on proximity.</summary>
    [Serializable]
    public sealed class DungeonScriptedEncounter
    {
        public string id;
        public string roomId;
        public DungeonPoint triggerPosition;
        public float triggerRadius = 3f;
        public string[] enemyTypes = Array.Empty<string>();
        public string waveLabel;
    }

    /// <summary>The dungeon's scripted mini-boss.</summary>
    [Serializable]
    public sealed class DungeonMiniBoss
    {
        public string id;
        public string roomId;
        public DungeonPoint position;
        public string enemyType;
        public string displayName;
    }

    /// <summary>A treasure chest — <c>rewardKey</c> is resolved by the loot layer.</summary>
    [Serializable]
    public sealed class DungeonChest
    {
        public string id;
        public string roomId;
        public DungeonPoint position;
        public string assetKey;
        public string rewardKey;
    }

    /// <summary>The tiered random-encounter pool (apprentice / veteran / cellar).</summary>
    [Serializable]
    public sealed class DungeonEncounterPool
    {
        public string[] apprentice = Array.Empty<string>();
        public string[] veteran = Array.Empty<string>();
        public string[] cellar = Array.Empty<string>();
    }

    /// <summary>Bryn the Wanderer's placement + dialogue triggers at the entrance.</summary>
    [Serializable]
    public sealed class DungeonBrynDef
    {
        public string npcId;
        public string roomId;
        public DungeonPoint position;
        public float facingY;
        public float speakRadius = 6f;
        public string loreFragmentId;
        public string firstEncounterLine;
    }

    /// <summary>A lightable lantern post — a permanent ambient light when lit.</summary>
    [Serializable]
    public sealed class DungeonLanternPost
    {
        public string id;
        public string roomId;
        public DungeonPoint position;
        public string assetKey;
    }

    /// <summary>An oil stone — refills the Keeper's lantern oil on proximity.</summary>
    [Serializable]
    public sealed class DungeonOilStone
    {
        public string id;
        public string roomId;
        public DungeonPoint position;
        public float radius = 2f;
    }

    /// <summary>
    /// The complete authored layout of one dungeon — the deserialised shape of a
    /// <c>data/dungeons/&lt;id&gt;.json</c> file. Plain data; the scene builds on top.
    /// </summary>
    [Serializable]
    public sealed class DungeonLayout
    {
        public int version;
        public string id;
        public string title;
        public int tier;
        public string questlineId;
        public string entryRoomId;
        public bool disableRandomEncounters;
        public string ambientBgm;

        public DungeonRoom[] rooms = Array.Empty<DungeonRoom>();
        public DungeonSpawn spawn;
        public DungeonLoreStone[] loreStones = Array.Empty<DungeonLoreStone>();
        public DungeonCheckpoint[] checkpoints = Array.Empty<DungeonCheckpoint>();
        public DungeonScriptedEncounter[] scriptedEncounters = Array.Empty<DungeonScriptedEncounter>();
        public DungeonMiniBoss miniBoss;
        public DungeonChest[] chests = Array.Empty<DungeonChest>();
        public DungeonEncounterPool encounterPool;
        public DungeonBrynDef bryn;
        public DungeonLanternPost[] lanternPosts = Array.Empty<DungeonLanternPost>();
        public DungeonOilStone[] oilStones = Array.Empty<DungeonOilStone>();

        /// <summary>Finds a room by id, or null when the dungeon has no such room.</summary>
        public DungeonRoom FindRoom(string roomId)
        {
            if (string.IsNullOrEmpty(roomId)) return null;
            foreach (var r in rooms)
                if (r != null && r.id == roomId) return r;
            return null;
        }

        /// <summary>The room whose footprint contains <paramref name="p"/>, or null.</summary>
        public DungeonRoom RoomAt(Vector3 p)
        {
            foreach (var r in rooms)
                if (r?.bounds != null && r.bounds.Contains(p)) return r;
            return null;
        }
    }

    /// <summary>
    /// Loads a <see cref="DungeonLayout"/> from a canonical JSON file under
    /// <c>StreamingAssets/Data/Canonical/dungeons/</c>. Reading from
    /// StreamingAssets is async on Android (the files live inside the APK / a
    /// jar), so the loader returns a <see cref="UniTask"/> — never <c>async void</c>
    /// (port spec Part 3 mandate).
    /// </summary>
    public static class DungeonLayoutLoader
    {
        /// <summary>The StreamingAssets-relative folder canonical dungeon JSON lives in.</summary>
        public const string CanonicalDungeonFolder = "Data/Canonical/dungeons";

        /// <summary>
        /// Loads and deserialises the layout for <paramref name="dungeonId"/>
        /// (e.g. <c>"healers-cottage"</c>). Returns null and logs an error when
        /// the file is missing or malformed.
        /// </summary>
        public static async UniTask<DungeonLayout> LoadAsync(string dungeonId)
        {
            if (string.IsNullOrEmpty(dungeonId))
            {
                FlowTrace.Fail("Dungeon", "DungeonLayoutLoader.LoadAsync: empty dungeon id — load aborted.");
                return null;
            }

            string fileName = $"{dungeonId}.json";
            string relativePath = $"{CanonicalDungeonFolder}/{fileName}";

            string json = await ReadTextAsync(relativePath);
            if (string.IsNullOrEmpty(json))
            {
                FlowTrace.Fail("Dungeon",
                    $"DungeonLayoutLoader.LoadAsync: could not read '{relativePath}' (empty/missing) — " +
                    "layout will be null and the dungeon cannot assemble.");
                return null;
            }

            try
            {
                var layout = JsonConvert.DeserializeObject<DungeonLayout>(json);
                if (layout == null || string.IsNullOrEmpty(layout.id))
                {
                    FlowTrace.Fail("Dungeon",
                        $"DungeonLayoutLoader.LoadAsync: '{fileName}' deserialised to an empty layout.");
                    return null;
                }
                FlowTrace.Step("Dungeon",
                    $"DungeonLayoutLoader.LoadAsync: parsed '{fileName}' -> id='{layout.id}', " +
                    $"rooms={layout.rooms?.Length ?? 0}.");
                return layout;
            }
            catch (JsonException ex)
            {
                FlowTrace.Fail("Dungeon",
                    $"DungeonLayoutLoader.LoadAsync: malformed JSON in '{fileName}': {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Reads canonical JSON for a StreamingAssets-relative path. Tries the
        /// WebGL-safe Resources copy FIRST (Resources.Load, synchronous on every
        /// platform incl. WebGL — File.ReadAllText / a StreamingAssets URL is
        /// unreliable in a browser). Falls back to a direct file read on desktop /
        /// editor, then a UnityWebRequest for packed StreamingAssets (Android).
        /// </summary>
        private static async UniTask<string> ReadTextAsync(string relativePath)
        {
            // 1) Resources-first (WebGL-safe). DeNelle.Core.CanonicalJson.Read
            //    resolves Assets/Resources/Data/Canonical/* and never throws.
            string fromResources = DeNelle.Core.CanonicalJson.Read(relativePath);
            if (!string.IsNullOrEmpty(fromResources)) return fromResources;

            // 2) StreamingAssets fallback — desktop/editor plain file, else web request.
            string fullPath = Path.Combine(Application.streamingAssetsPath, relativePath);
            bool needsWebRequest =
                fullPath.Contains("://") || fullPath.Contains(":///");

            if (!needsWebRequest)
            {
                try
                {
                    if (!File.Exists(fullPath))
                    {
                        FlowTrace.Warn("Dungeon",
                            $"DungeonLayoutLoader.ReadTextAsync: no Resources copy and file not found at " +
                            $"'{fullPath}' — returning null (caller fails the load).");
                        return null;
                    }
                    return await File.ReadAllTextAsync(fullPath);
                }
                catch (Exception ex)
                {
                    // NO SILENT CATCH (§12): a read that throws must self-report, not blank.
                    FlowTrace.Fail("Dungeon",
                        $"DungeonLayoutLoader.ReadTextAsync: read threw for '{fullPath}': " +
                        $"{ex.GetType().Name}: {ex.Message} — returning null (caller fails the load).");
                    return null;
                }
            }

            using var req = UnityWebRequest.Get(fullPath);
            await req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success)
            {
                FlowTrace.Fail("Dungeon",
                    $"DungeonLayoutLoader.ReadTextAsync: UnityWebRequest failed for '{fullPath}': {req.error}");
                return null;
            }
            return req.downloadHandler.text;
        }
    }
}
