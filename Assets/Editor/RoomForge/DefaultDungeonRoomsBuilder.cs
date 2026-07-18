// =============================================================================
// DefaultDungeonRoomsBuilder — forges the KEY DEFAULT room prefab library.
// -----------------------------------------------------------------------------
// Menu: Defenders/Dungeon/Build Default Room Prefabs
// Batch: DeNelle.Editor.RoomForge.DefaultDungeonRoomsBuilder.BuildAll
//
// Produces Assets/Dungeon/Rooms/*.prefab with RoomPrefabMeta + RoomSockets so
// layouts can compose: entrance, straight, turns, T, cross, dead-end, choke,
// combat, lore, reward, secret, stairs. Geometry is procedural (floor + walls)
// dressed with SHARED KayKit dungeon atlas materials (RoomForgeMaterials).
// Sockets match DungeonBaker mate convention (6u cells).
// =============================================================================

using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using DeNelle.Core.Diagnostics;
using DeNelle.Dungeons.RoomForge;

namespace DeNelle.Editor.RoomForge
{
    public static class DefaultDungeonRoomsBuilder
    {
        private const string RoomsFolder = "Assets/Dungeon/Rooms";
        private const string CatalogPath =
            "Assets/StreamingAssets/Data/Canonical/dungeon-layouts/rooms-catalog.json";
        private const string CatalogPathRes =
            "Assets/Resources/Data/Canonical/dungeon-layouts/rooms-catalog.json";
        private const float Cell = 6f;
        private const string Sys = "RoomForgeDefaults";

        private struct RoomSpec
        {
            public string id;
            public string archetype;
            public Vector2Int footprint; // cells
            public string[] facings;     // which cardinals get Door sockets
            public bool choke;           // narrow interior walls
            public bool secretSocket;    // mark first socket secret (or west)
            public RoomSocketType? stairType; // if set, adds a stair socket at center-north
            public bool accentFloor;         // reward/boss — warm KayKit accent mat
            public string note;
        }

        [MenuItem("Defenders/Dungeon/Build Default Room Prefabs")]
        public static void BuildAll()
        {
            EnsureFolder(RoomsFolder);
            EnsureFolder("Assets/StreamingAssets/Data/Canonical/dungeon-layouts");
            EnsureFolder("Assets/Resources/Data/Canonical/dungeon-layouts");

            // One shared wall + floor mat from KayKit dungeon_texture.png (all rooms).
            RoomForgeMaterials.EnsureMenu();

            var specs = DefaultSpecs();
            FlowTrace.Step("RoomForge", $"BuildDefaultRoomPrefabs specs={specs.Count} folder='{RoomsFolder}'");
            var catalog = new RoomCatalogFile { version = 1, rooms = new List<RoomCatalogEntry>() };
            int ok = 0;
            foreach (var spec in specs)
            {
                if (BuildOne(spec, catalog)) ok++;
            }

            WriteCatalog(catalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            FlowTrace.Step("RoomForge", $"built {ok}/{specs.Count} default room prefabs -> {RoomsFolder} + rooms-catalog.json " +
                                        $"(shared KayKit atlas mats on all walls/floors)");
        }

        /// <summary>Batchmode: Unity -executeMethod DeNelle.Editor.RoomForge.DefaultDungeonRoomsBuilder.BuildAll</summary>
        public static void BuildAllBatch()
        {
            BuildAll();
            // Do not EditorApplication.Exit here if invoked from a multi-method run; menu path is fine.
        }

        private static List<RoomSpec> DefaultSpecs()
        {
            return new List<RoomSpec>
            {
                // ── Spine / flow ───────────────────────────────────────────
                new RoomSpec
                {
                    id = "Entrance",
                    archetype = "hub",
                    footprint = new Vector2Int(1, 1),
                    facings = new[] { "S", "N" }, // S = world/approach, N = into dungeon
                    note = "Dungeon mouth — approach south, continue north",
                },
                new RoomSpec
                {
                    id = "EntryHall",
                    archetype = "hub",
                    footprint = new Vector2Int(1, 1),
                    facings = new[] { "S", "N" },
                    note = "Alias-friendly hub (spine sample name)",
                },
                new RoomSpec
                {
                    id = "Straight",
                    archetype = "combat",
                    footprint = new Vector2Int(1, 1),
                    facings = new[] { "S", "N" },
                    note = "Corridor cell — north/south",
                },
                new RoomSpec
                {
                    id = "TurnLeft",
                    archetype = "combat",
                    footprint = new Vector2Int(1, 1),
                    facings = new[] { "S", "W" }, // enter from S facing N → exit W = left
                    note = "Left bend (enter S, leave W)",
                },
                new RoomSpec
                {
                    id = "TurnRight",
                    archetype = "combat",
                    footprint = new Vector2Int(1, 1),
                    facings = new[] { "S", "E" }, // enter S → leave E = right
                    note = "Right bend (enter S, leave E)",
                },
                new RoomSpec
                {
                    id = "TJunction",
                    archetype = "combat",
                    footprint = new Vector2Int(1, 1),
                    facings = new[] { "S", "E", "W" }, // no north wall open — T when approached from S
                    note = "T-junction (S/E/W)",
                },
                new RoomSpec
                {
                    id = "Intersection",
                    archetype = "combat",
                    footprint = new Vector2Int(1, 1),
                    facings = new[] { "N", "E", "S", "W" },
                    note = "4-way cross",
                },
                new RoomSpec
                {
                    id = "DeadEnd",
                    archetype = "lore",
                    footprint = new Vector2Int(1, 1),
                    facings = new[] { "S" }, // only way in/out
                    note = "Cul-de-sac (single south socket)",
                },
                new RoomSpec
                {
                    id = "ChokePoint",
                    archetype = "combat",
                    footprint = new Vector2Int(1, 1),
                    facings = new[] { "S", "N" },
                    choke = true,
                    note = "Narrow pass N/S — ambush / squeeze",
                },

                // ── Content rooms ──────────────────────────────────────────
                new RoomSpec
                {
                    id = "CombatChamber",
                    archetype = "combat",
                    footprint = new Vector2Int(2, 2),
                    facings = new[] { "S", "N" },
                    note = "2x2 fight room",
                },
                new RoomSpec
                {
                    id = "LoreShrine",
                    archetype = "lore",
                    footprint = new Vector2Int(1, 1),
                    facings = new[] { "S" },
                    note = "Shrine / lore stone (dead-end lore)",
                },
                new RoomSpec
                {
                    id = "RewardVault",
                    archetype = "reward",
                    footprint = new Vector2Int(1, 1),
                    facings = new[] { "S" },
                    accentFloor = true,
                    note = "Treasure end room (accent floor tint)",
                },
                new RoomSpec
                {
                    id = "SecretAlcove",
                    archetype = "secret",
                    footprint = new Vector2Int(1, 1),
                    facings = new[] { "S" },
                    secretSocket = true,
                    note = "Secret alcove — socket flagged isSecret",
                },

                // ── Vertical ───────────────────────────────────────────────
                new RoomSpec
                {
                    id = "StairDown",
                    archetype = "hub",
                    footprint = new Vector2Int(1, 1),
                    facings = new[] { "S" },
                    stairType = RoomSocketType.StairDown,
                    note = "Horizontal entry + stair down socket",
                },
                new RoomSpec
                {
                    id = "StairUp",
                    archetype = "hub",
                    footprint = new Vector2Int(1, 1),
                    facings = new[] { "S" },
                    stairType = RoomSocketType.StairUp,
                    note = "Horizontal entry + stair up socket",
                },

                // ── Branches / extras ──────────────────────────────────────
                new RoomSpec
                {
                    id = "SideBranch",
                    archetype = "combat",
                    footprint = new Vector2Int(1, 1),
                    facings = new[] { "W", "E" }, // east-west spur
                    note = "East-west spur corridor",
                },
                new RoomSpec
                {
                    id = "BossKeep",
                    archetype = "boss",
                    footprint = new Vector2Int(2, 2),
                    facings = new[] { "S" },
                    accentFloor = true,
                    note = "Boss arena (enter south only, accent floor)",
                },
            };
        }

        private static bool BuildOne(RoomSpec spec, RoomCatalogFile catalog)
        {
            float wx = spec.footprint.x * Cell;
            float wz = spec.footprint.y * Cell;
            float hx = wx * 0.5f;
            float hz = wz * 0.5f;

            var root = new GameObject($"Room_{spec.id}");
            try
            {
                var meta = root.AddComponent<RoomPrefabMeta>();
                meta.roomId = spec.id;
                meta.archetype = spec.archetype;
                meta.themePalette = "default";
                meta.footprintCells = spec.footprint;
                meta.cellSize = Cell;

                // Floor
                var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                floor.name = "Floor";
                floor.transform.SetParent(root.transform, false);
                floor.transform.localPosition = new Vector3(0f, -0.05f, 0f);
                floor.transform.localScale = new Vector3(wx, 0.1f, wz);
                GameObjectUtility.SetStaticEditorFlags(floor,
                    StaticEditorFlags.NavigationStatic | StaticEditorFlags.BatchingStatic);

                // Perimeter walls with gaps at open facings
                BuildPerimeterWalls(root.transform, hx, hz, spec.facings);

                if (spec.choke)
                    BuildChokeInterior(root.transform, hx, hz);

                // ONE KayKit atlas for every wall/floor (simple + consistent).
                RoomForgeMaterials.ApplyToRoomRoot(root, useAccentFloor: spec.accentFloor);

                // Door sockets
                var socketList = new List<RoomCatalogSocket>();
                foreach (var f in spec.facings)
                {
                    bool secret = spec.secretSocket && f == "S";
                    var sock = AddSocket(root.transform, f, RoomSocketType.Door, hx, hz, secret);
                    socketList.Add(ToCatalogSocket(sock));
                }

                if (spec.stairType.HasValue)
                {
                    var stair = AddStairSocket(root.transform, spec.stairType.Value);
                    socketList.Add(ToCatalogSocket(stair));
                }

                // Center marker for spawn / shrine later
                var marker = new GameObject("Anchor_Center");
                marker.transform.SetParent(root.transform, false);
                marker.transform.localPosition = Vector3.zero;

                string prefabPath = $"{RoomsFolder}/{spec.id}.prefab";
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath, out bool success);
                if (!success)
                {
                    FlowTrace.Fail("RoomForge", $"failed to save prefab '{prefabPath}'");
                    return false;
                }

                catalog.rooms.Add(new RoomCatalogEntry
                {
                    id = spec.id,
                    prefabPath = prefabPath,
                    archetype = spec.archetype,
                    themePalette = "default",
                    footprintCells = new[] { spec.footprint.x, spec.footprint.y },
                    cellSize = Cell,
                    sockets = socketList,
                });

                FlowTrace.Step("RoomForge", $"room saved id='{spec.id}' archetype='{spec.archetype}' " +
                                            $"footprint={spec.footprint.x}x{spec.footprint.y} sockets={socketList.Count} -> {prefabPath}");
                return true;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void BuildPerimeterWalls(Transform parent, float hx, float hz, string[] openFacings)
        {
            var open = new HashSet<string>(openFacings);
            float wallH = 2.8f;
            float thick = 0.4f;
            float gap = 2.2f; // doorway clear width

            // North wall (+Z)
            if (open.Contains("N"))
                BuildWallWithGap(parent, "Wall_N", new Vector3(0f, wallH * 0.5f, hz),
                    new Vector3(hx * 2f, wallH, thick), gap, alongX: true);
            else
                BuildSolidWall(parent, "Wall_N", new Vector3(0f, wallH * 0.5f, hz),
                    new Vector3(hx * 2f, wallH, thick));

            // South (-Z)
            if (open.Contains("S"))
                BuildWallWithGap(parent, "Wall_S", new Vector3(0f, wallH * 0.5f, -hz),
                    new Vector3(hx * 2f, wallH, thick), gap, alongX: true);
            else
                BuildSolidWall(parent, "Wall_S", new Vector3(0f, wallH * 0.5f, -hz),
                    new Vector3(hx * 2f, wallH, thick));

            // East (+X)
            if (open.Contains("E"))
                BuildWallWithGap(parent, "Wall_E", new Vector3(hx, wallH * 0.5f, 0f),
                    new Vector3(thick, wallH, hz * 2f), gap, alongX: false);
            else
                BuildSolidWall(parent, "Wall_E", new Vector3(hx, wallH * 0.5f, 0f),
                    new Vector3(thick, wallH, hz * 2f));

            // West (-X)
            if (open.Contains("W"))
                BuildWallWithGap(parent, "Wall_W", new Vector3(-hx, wallH * 0.5f, 0f),
                    new Vector3(thick, wallH, hz * 2f), gap, alongX: false);
            else
                BuildSolidWall(parent, "Wall_W", new Vector3(-hx, wallH * 0.5f, 0f),
                    new Vector3(thick, wallH, hz * 2f));
        }

        private static void BuildSolidWall(Transform parent, string name, Vector3 localPos, Vector3 scale)
        {
            var w = GameObject.CreatePrimitive(PrimitiveType.Cube);
            w.name = name;
            w.transform.SetParent(parent, false);
            w.transform.localPosition = localPos;
            w.transform.localScale = scale;
            // Material applied in bulk via RoomForgeMaterials.ApplyToRoomRoot.
            GameObjectUtility.SetStaticEditorFlags(w, StaticEditorFlags.NavigationStatic);
        }

        private static void BuildWallWithGap(Transform parent, string name, Vector3 center, Vector3 fullScale,
            float gap, bool alongX)
        {
            // Two half-walls flanking a center gap.
            if (alongX)
            {
                float total = fullScale.x;
                float side = (total - gap) * 0.5f;
                if (side < 0.2f) side = 0.2f;
                float z = center.z;
                float y = center.y;
                float thick = fullScale.z;
                float h = fullScale.y;
                // Left (-X) piece
                BuildSolidWall(parent, name + "_L",
                    new Vector3(-(gap * 0.5f + side * 0.5f), y, z),
                    new Vector3(side, h, thick));
                // Right (+X) piece
                BuildSolidWall(parent, name + "_R",
                    new Vector3(+(gap * 0.5f + side * 0.5f), y, z),
                    new Vector3(side, h, thick));
            }
            else
            {
                float total = fullScale.z;
                float side = (total - gap) * 0.5f;
                if (side < 0.2f) side = 0.2f;
                float x = center.x;
                float y = center.y;
                float thick = fullScale.x;
                float h = fullScale.y;
                BuildSolidWall(parent, name + "_A",
                    new Vector3(x, y, -(gap * 0.5f + side * 0.5f)),
                    new Vector3(thick, h, side));
                BuildSolidWall(parent, name + "_B",
                    new Vector3(x, y, +(gap * 0.5f + side * 0.5f)),
                    new Vector3(thick, h, side));
            }
        }

        private static void BuildChokeInterior(Transform parent, float hx, float hz)
        {
            // Two side masses leaving a ~2u walk lane along +Z (N/S sockets).
            float wallH = 2.4f;
            float laneHalf = 1.05f;
            float massW = Mathf.Max(0.6f, hx - laneHalf - 0.15f);
            BuildSolidWall(parent, "Choke_W",
                new Vector3(-(laneHalf + massW * 0.5f), wallH * 0.5f, 0f),
                new Vector3(massW, wallH, hz * 1.4f));
            BuildSolidWall(parent, "Choke_E",
                new Vector3(+(laneHalf + massW * 0.5f), wallH * 0.5f, 0f),
                new Vector3(massW, wallH, hz * 1.4f));
        }

        private static RoomSocket AddSocket(Transform parent, string facing, RoomSocketType type,
            float hx, float hz, bool secret)
        {
            Vector3 local = facing switch
            {
                "N" => new Vector3(0f, 0f, hz),
                "S" => new Vector3(0f, 0f, -hz),
                "E" => new Vector3(hx, 0f, 0f),
                "W" => new Vector3(-hx, 0f, 0f),
                _ => Vector3.zero,
            };
            Quaternion rot = facing switch
            {
                "N" => Quaternion.LookRotation(Vector3.forward),
                "S" => Quaternion.LookRotation(Vector3.back),
                "E" => Quaternion.LookRotation(Vector3.right),
                "W" => Quaternion.LookRotation(Vector3.left),
                _ => Quaternion.identity,
            };

            string id = $"{facing.ToLowerInvariant()}_{type.ToString().ToLowerInvariant()}_01";
            var go = new GameObject($"Socket_{id}");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            go.transform.localRotation = rot;
            var sock = go.AddComponent<RoomSocket>();
            sock.id = id;
            sock.type = type;
            sock.facing = facing;
            sock.isSecret = secret;
            sock.halfWidth = type == RoomSocketType.Arch ? 1.5f : 1.1f;
            return sock;
        }

        private static RoomSocket AddStairSocket(Transform parent, RoomSocketType stairType)
        {
            var go = new GameObject($"Socket_stair_{stairType}");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, 0f, 0.5f);
            go.transform.localRotation = Quaternion.LookRotation(Vector3.down);
            var sock = go.AddComponent<RoomSocket>();
            sock.id = stairType == RoomSocketType.StairDown ? "stair_down_01" : "stair_up_01";
            sock.type = stairType;
            sock.facing = "U";
            sock.halfWidth = 1.2f;
            return sock;
        }

        private static RoomCatalogSocket ToCatalogSocket(RoomSocket s)
        {
            var lp = s.transform.localPosition;
            return new RoomCatalogSocket
            {
                id = s.id,
                type = s.type.ToString(),
                facing = s.facing,
                isSecret = s.isSecret,
                localPosition = new[] { lp.x, lp.y, lp.z },
            };
        }

        private static void WriteCatalog(RoomCatalogFile catalog)
        {
            string json = JsonConvert.SerializeObject(catalog, Formatting.Indented);
            bool wrote = Guard.Try("RoomForge", "write rooms-catalog dual-copy", () =>
            {
                File.WriteAllText(CatalogPath, json, Encoding.UTF8);
                File.WriteAllText(CatalogPathRes, json, Encoding.UTF8);
            });
            FlowTrace.Step("RoomForge", $"catalog write entries={catalog.rooms.Count} dualCopy={(wrote ? "ok" : "FAILED")} " +
                                        $"(StreamingAssets + Resources)");
            // Also write a human README of the kit
            string readme = Path.Combine(Application.dataPath, "Dungeon/Rooms/DEFAULT_ROOMS.md");
            var sb = new StringBuilder();
            sb.AppendLine("# Default dungeon room prefabs");
            sb.AppendLine();
            sb.AppendLine("| Prefab | Archetype | Sockets | Notes |");
            sb.AppendLine("|--------|-----------|---------|-------|");
            foreach (var s in DefaultSpecs())
            {
                string socks = string.Join(",", s.facings);
                if (s.stairType.HasValue) socks += $"+{s.stairType}";
                sb.AppendLine($"| `{s.id}` | {s.archetype} | {socks} | {s.note} |");
            }
            sb.AppendLine();
            sb.AppendLine("Rebuild: `Defenders/Dungeon/Build Default Room Prefabs`");
            File.WriteAllText(readme, sb.ToString(), Encoding.UTF8);
        }

        private static void EnsureFolder(string assetFolder)
        {
            if (AssetDatabase.IsValidFolder(assetFolder)) return;
            string[] parts = assetFolder.Split('/');
            string cur = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = cur + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(cur, parts[i]);
                cur = next;
            }
        }
    }
}
