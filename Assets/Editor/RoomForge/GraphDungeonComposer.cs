// =============================================================================
// GraphDungeonComposer — author a dungeon as a HIGH-LEVEL ROOM GRAPH (nodes +
// which door connects to which) and AUTO-COMPUTE every room's world transform by
// mating sockets. No hand-typed cell coordinates, no pre-aligned rotations.
// -----------------------------------------------------------------------------
// Menu:  Defenders/Dungeon/Compose Dungeon From Graph (Selected JSON)
//        Defenders/Dungeon/Compose Starter Loop (dg_starter_loop)
// Batch: DeNelle.Editor.RoomForge.GraphDungeonComposer.ComposeAndBake(graphAssetPath)
//        DeNelle.Editor.RoomForge.GraphDungeonComposer.ComposeStarterLoopBatch()
//
// WHY: Room Forge's compose-layout JSON needs hand-authored cell positions +
// manually pre-aligned yaws, and DungeonBaker's TryMate only PLANAR-NUDGES the
// "to" room (never rotates) -> branches drift-fail unless pre-aligned by hand.
// This tool takes a graph, INSTANTIATES each prefab, reads the REAL socket
// transforms (RoomSocket.Outward = transform.forward, not in the catalog), and
// solves each child's rotate+translate so its socket mates opposite the parent's
// socket (door-touch-door). It then emits a FULLY-POSITIONED DungeonComposeLayout
// JSON and hands it to the existing DungeonBaker (ONE bake path: mate-verify ->
// NavMesh -> save). Mate/compat/verify math is REUSED from DungeonBakerChecks.
//
// Output positions land on integer world units for the default 6u room kit
// (sockets at multiples of 3u, yaws at multiples of 90), so the emitted layout
// uses cellSize=1 with cell=[round(x),round(y),round(z)] -> lossless round-trip.
// =============================================================================

using System;
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
    // ---- Graph spec JSON schema (the authorable source) ---------------------

    /// <summary>High-level room graph. First node (or <see cref="entry"/>) is the origin room.</summary>
    [Serializable]
    public sealed class DungeonGraph
    {
        [JsonProperty("graphId")] public string graphId = "untitled_graph";
        /// <summary>Node id placed at world origin/identity. Empty => first node in the list.</summary>
        [JsonProperty("entry")] public string entry = "";
        [JsonProperty("nodes")] public List<GraphNode> nodes = new List<GraphNode>();
        [JsonProperty("edges")] public List<GraphEdge> edges = new List<GraphEdge>();
        /// <summary>Optional bake/lint rules passed straight through to the compose layout.</summary>
        [JsonProperty("rules")] public ComposeRules rules;
    }

    /// <summary>One room instance in the graph.</summary>
    [Serializable]
    public sealed class GraphNode
    {
        /// <summary>Local id unique within the graph (becomes the compose instanceId).</summary>
        [JsonProperty("id")] public string id;
        /// <summary>Room prefab stem under Assets/Dungeon/Rooms/ (e.g. "TurnRight").</summary>
        [JsonProperty("prefab")] public string prefab;
        /// <summary>
        /// WO-797: optional per-room encounter (rooms own their enemies). Carried verbatim
        /// into the emitted compose layout; DungeonBaker seats one confined spawner per
        /// encounter room. Null = no enemies in this room.
        /// </summary>
        [JsonProperty("encounter")] public EncounterSpec encounter;
    }

    /// <summary>A door-to-door connection: fromNode.fromSocket mates toNode.toSocket.</summary>
    [Serializable]
    public sealed class GraphEdge
    {
        [JsonProperty("from")] public string from;
        [JsonProperty("fromSocket")] public string fromSocket;
        [JsonProperty("to")] public string to;
        [JsonProperty("toSocket")] public string toSocket;
    }

    // ---- Composer -----------------------------------------------------------

    public static class GraphDungeonComposer
    {
        private const string GraphsFolder = "Assets/StreamingAssets/Data/Canonical/dungeon-graphs";
        private const string LayoutsFolder = "Assets/StreamingAssets/Data/Canonical/dungeon-layouts";
        private const string StarterGraph = "dg_starter_loop.json";
        private const string Sys = "DungeonGraph";

        /// <summary>UTF-8 WITHOUT a byte-order mark. Never use <c>Encoding.UTF8</c> to
        /// write canonical JSON: that overload emits a leading EF BB BF which fails the
        /// static check-in gate's JSON parse.</summary>
        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);
        // Emitted layout uses cellSize=1 so cell=[x,y,z] carries the exact solved world
        // coords (default kit is grid-aligned to integer units) with no quantization loss.
        private const float EmitCellSize = 1f;

        // -------- Menu / batch entry points --------

        [MenuItem("Defenders/Dungeon/Compose Dungeon From Graph (Selected JSON)")]
        public static void ComposeSelected()
        {
            var obj = Selection.activeObject;
            string path = obj != null ? AssetDatabase.GetAssetPath(obj) : null;
            if (string.IsNullOrEmpty(path) || !path.EndsWith(".json"))
            {
                EditorUtility.DisplayDialog("GraphDungeonComposer",
                    "Select a dungeon-graphs JSON asset first (nodes + edges), " +
                    "or use 'Compose Starter Loop'.", "OK");
                return;
            }
            ComposeAndBake(path);
        }

        [MenuItem("Defenders/Dungeon/Compose Starter Loop (dg_starter_loop)")]
        public static void ComposeStarterLoop()
        {
            // populateForPlay: seat a playable hero + hero-aggro enemy spawners so the owner's
            // first dungeon is enterable + fightable straight off the portal.
            ComposeAndBake(Path.Combine(GraphsFolder, StarterGraph), populateForPlay: true);
        }

        /// <summary>Batchmode: -executeMethod DeNelle.Editor.RoomForge.GraphDungeonComposer.ComposeStarterLoopBatch</summary>
        public static void ComposeStarterLoopBatch()
        {
            ComposeAndBake(Path.Combine(GraphsFolder, StarterGraph), populateForPlay: true);
            EditorApplication.Exit(0);
        }

        // -------- Core: graph -> positioned compose layout -> bake --------

        /// <summary>
        /// Read a graph JSON, solve every room's world transform by socket-mating, emit a
        /// fully-positioned DungeonComposeLayout JSON, and bake it through the existing
        /// <see cref="DungeonBaker"/> (mate-verify + NavMesh + save). No EditorApplication.Exit.
        /// </summary>
        public static void ComposeAndBake(string graphAssetPath, bool populateForPlay = false)
        {
            string fsPath = ToFilesystemPath(graphAssetPath);
            if (!File.Exists(fsPath))
            {
                FlowTrace.Fail(Sys, $"graph not found: {graphAssetPath} (resolved '{fsPath}')");
                return;
            }

            string json = Guard.Try(Sys, "read graph json",
                () => File.ReadAllText(fsPath, Encoding.UTF8), null);
            if (string.IsNullOrEmpty(json))
            {
                FlowTrace.Fail(Sys, $"graph unreadable/empty: {fsPath}");
                return;
            }

            DungeonGraph graph = Guard.Try(Sys, "parse graph json",
                () => JsonConvert.DeserializeObject<DungeonGraph>(json), null);
            if (graph == null || graph.nodes == null || graph.nodes.Count == 0)
            {
                FlowTrace.Fail(Sys, "graph parse returned null or 0 nodes - abort");
                return;
            }

            FlowTrace.Step(Sys, $"graph loaded id='{graph.graphId}' nodes={graph.nodes.Count} " +
                                $"edges={(graph.edges != null ? graph.edges.Count : 0)} entry='{graph.entry}'");

            DungeonComposeLayout layout = SolveGraph(graph);
            if (layout == null)
            {
                FlowTrace.Fail(Sys, $"solve failed for graph '{graph.graphId}' - no layout emitted");
                return;
            }

            // Emit the positioned compose layout next to the hand-authored layouts so the
            // existing DungeonBaker (the single bake path) can consume it verbatim.
            EnsureFolder(LayoutsFolder);
            string layoutAssetPath = $"{LayoutsFolder}/{layout.dungeonId}.json";
            string outJson = JsonConvert.SerializeObject(layout, Formatting.Indented);
            // UTF8-no-BOM: Encoding.UTF8 emits a leading EF BB BF that fails the
            // static check-in gate's canonical-JSON parse (this writes into
            // StreamingAssets/Data/Canonical/dungeon-layouts).
            bool wrote = Guard.Try(Sys, "write composed layout json", () =>
                File.WriteAllText(ToFilesystemPath(layoutAssetPath), outJson, Utf8NoBom));
            if (!wrote)
            {
                FlowTrace.Fail(Sys, $"failed to write composed layout {layoutAssetPath} - abort");
                return;
            }
            AssetDatabase.Refresh();
            FlowTrace.Step(Sys, $"composed layout written id='{layout.dungeonId}' rooms={layout.rooms.Count} " +
                                $"connections={layout.connections.Count} -> {layoutAssetPath} (cellSize={EmitCellSize})");

            // Hand off to the ONE bake path: DungeonBaker mate-verifies, bakes NavMesh, saves.
            // populateForPlay seats the playable hero + enemy spawners (starter-loop only).
            DungeonBaker.BakeFromFile(layoutAssetPath, populateForPlay);
        }

        /// <summary>
        /// Instantiate each prefab, BFS/flood from the entry, and solve each child's world
        /// transform so its socket mates opposite the parent socket. Loop-closing edges (both
        /// ends already placed) are VERIFIED in place (kept if they mate, logged + dropped as a
        /// returning dead-end if not). Returns a fully-positioned compose layout.
        /// </summary>
        private static DungeonComposeLayout SolveGraph(DungeonGraph graph)
        {
            var rules = graph.rules ?? new ComposeRules { spineOnly = false };
            float maxD = rules.maxMateDistance > 0f
                ? rules.maxMateDistance
                : DungeonBakerChecks.DefaultMaxMateDistance;

            // Instantiate every node's prefab once (identity) under a temp root; we read the
            // REAL socket transforms, solve poses, then destroy the temp tree. The bake later
            // re-instantiates from the emitted layout, so these instances are scratch only.
            var tempRoot = new GameObject($"__GraphSolve_{graph.graphId}");
            var go = new Dictionary<string, GameObject>();
            var placed = new HashSet<string>();
            var nodePrefab = new Dictionary<string, string>();
            try
            {
                foreach (var n in graph.nodes)
                {
                    if (n == null || string.IsNullOrEmpty(n.id) || string.IsNullOrEmpty(n.prefab))
                    {
                        FlowTrace.Warn(Sys, "skipping node with empty id/prefab");
                        continue;
                    }
                    if (go.ContainsKey(n.id))
                    {
                        FlowTrace.Warn(Sys, $"duplicate node id '{n.id}' - skipping the second one");
                        continue;
                    }
                    var prefab = LoadRoomPrefab(n.prefab);
                    GameObject inst;
                    if (prefab != null)
                    {
                        inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, tempRoot.transform);
                        if (inst == null) inst = UnityEngine.Object.Instantiate(prefab, tempRoot.transform);
                    }
                    else
                    {
                        FlowTrace.Fail(Sys, $"prefab NOT FOUND for node '{n.id}' prefab='{n.prefab}' " +
                                            "(needs Assets/Dungeon/Rooms/<name>.prefab) - node dropped");
                        continue;
                    }
                    inst.name = n.id;
                    inst.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                    go[n.id] = inst;
                    nodePrefab[n.id] = n.prefab;
                }

                if (go.Count == 0)
                {
                    FlowTrace.Fail(Sys, "no nodes instantiated (all prefabs missing?) - abort");
                    return null;
                }

                // Entry node = origin/identity.
                string entry = !string.IsNullOrEmpty(graph.entry) && go.ContainsKey(graph.entry)
                    ? graph.entry
                    : FirstKey(go, graph);
                if (string.IsNullOrEmpty(entry))
                {
                    FlowTrace.Fail(Sys, "could not resolve an entry node - abort");
                    return null;
                }
                go[entry].transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                placed.Add(entry);
                FlowTrace.Step(Sys, $"entry '{entry}' placed at origin/identity");

                var edges = graph.edges ?? new List<GraphEdge>();
                var keptConnections = new List<ComposeConnection>();
                var deferred = new List<GraphEdge>(edges);

                // Flood: repeatedly resolve any edge with exactly one placed endpoint. An edge
                // with BOTH endpoints placed when we reach it is a loop-closing edge (verify).
                bool progress = true;
                int guard = 0;
                while (progress && deferred.Count > 0 && guard++ < 4096)
                {
                    progress = false;
                    for (int i = deferred.Count - 1; i >= 0; i--)
                    {
                        var e = deferred[i];
                        if (e == null || string.IsNullOrEmpty(e.from) || string.IsNullOrEmpty(e.to))
                        {
                            deferred.RemoveAt(i);
                            continue;
                        }
                        if (!go.ContainsKey(e.from) || !go.ContainsKey(e.to))
                        {
                            FlowTrace.Fail(Sys, $"edge references unknown node ({e.from} -> {e.to}) - dropped");
                            deferred.RemoveAt(i);
                            continue;
                        }
                        bool fromPlaced = placed.Contains(e.from);
                        bool toPlaced = placed.Contains(e.to);

                        if (fromPlaced && toPlaced)
                            continue; // loop edge - handle after the tree is fully placed

                        if (!fromPlaced && !toPlaced)
                            continue; // neither placed yet - wait for a parent

                        // Exactly one placed: mate the unplaced child to the placed parent.
                        string parentId = fromPlaced ? e.from : e.to;
                        string parentSocketId = fromPlaced ? e.fromSocket : e.toSocket;
                        string childId = fromPlaced ? e.to : e.from;
                        string childSocketId = fromPlaced ? e.toSocket : e.fromSocket;

                        var pSock = DungeonBakerChecks.FindSocket(go[parentId], parentSocketId);
                        var cSock = DungeonBakerChecks.FindSocket(go[childId], childSocketId);
                        if (pSock == null || cSock == null)
                        {
                            FlowTrace.Fail(Sys, $"MISSING socket on edge {e.from}.{e.fromSocket}->{e.to}.{e.toSocket} " +
                                                $"(parent {parentId}.{parentSocketId}={(pSock == null ? "MISSING" : "ok")}, " +
                                                $"child {childId}.{childSocketId}={(cSock == null ? "MISSING" : "ok")}) - edge dropped");
                            deferred.RemoveAt(i);
                            continue;
                        }
                        if (!DungeonBakerChecks.TypesCompatible(pSock.type, cSock.type))
                            FlowTrace.Warn(Sys, $"edge {parentId}.{parentSocketId}->{childId}.{childSocketId} " +
                                                $"socket types {pSock.type} vs {cSock.type} not compatible (baker will fail-gate)");

                        SolveMate(pSock, cSock, go[childId]);
                        placed.Add(childId);
                        // Emit the connection in from->to authored order (DungeonBaker is direction-agnostic).
                        keptConnections.Add(new ComposeConnection
                        {
                            fromInstance = e.from, fromSocket = e.fromSocket,
                            toInstance = e.to, toSocket = e.toSocket,
                        });

                        var vp = pSock.WorldPosition; var vc = cSock.WorldPosition;
                        float d = Vector3.Distance(vp, vc);
                        float al = Vector3.Dot(pSock.Outward.normalized, -cSock.Outward.normalized);
                        FlowTrace.Step(Sys, $"mate SOLVED {parentId}.{parentSocketId}->{childId}.{childSocketId} " +
                                            $"childYaw={go[childId].transform.eulerAngles.y:F0} dist={d:F3} align={al:F3}");
                        deferred.RemoveAt(i);
                        progress = true;
                    }
                }

                // Whatever remains with both ends placed = loop-closing edges: verify in place.
                foreach (var e in deferred)
                {
                    if (e == null) continue;
                    if (!placed.Contains(e.from) || !placed.Contains(e.to))
                    {
                        FlowTrace.Warn(Sys, $"edge {e.from}.{e.fromSocket}->{e.to}.{e.toSocket} " +
                                            "left unresolved (disconnected from entry) - dropped");
                        continue;
                    }
                    var aSock = DungeonBakerChecks.FindSocket(go[e.from], e.fromSocket);
                    var bSock = DungeonBakerChecks.FindSocket(go[e.to], e.toSocket);
                    if (aSock == null || bSock == null)
                    {
                        FlowTrace.Warn(Sys, $"loop edge {e.from}.{e.fromSocket}->{e.to}.{e.toSocket} missing socket - dropped");
                        continue;
                    }
                    if (DungeonBakerChecks.StillMated(aSock, bSock, maxD))
                    {
                        keptConnections.Add(new ComposeConnection
                        {
                            fromInstance = e.from, fromSocket = e.fromSocket,
                            toInstance = e.to, toSocket = e.toSocket,
                        });
                        FlowTrace.Step(Sys, $"LOOP closed cleanly {e.from}.{e.fromSocket}->{e.to}.{e.toSocket} " +
                                            $"(dist={Vector3.Distance(aSock.WorldPosition, bSock.WorldPosition):F3})");
                    }
                    else
                    {
                        FlowTrace.Warn(Sys, $"LOOP does not close within tol {e.from}.{e.fromSocket}->{e.to}.{e.toSocket} " +
                                            $"(dist={Vector3.Distance(aSock.WorldPosition, bSock.WorldPosition):F3}) " +
                                            "- leaving as returning dead-end (both sockets seal), NOT failing the bake");
                    }
                }

                // Build the positioned compose layout from the solved instances.
                var layout = new DungeonComposeLayout
                {
                    dungeonId = graph.graphId,
                    cellSize = EmitCellSize,
                    rooms = new List<ComposeRoomPlacement>(),
                    connections = keptConnections,
                    rules = rules,
                };

                foreach (var n in graph.nodes)
                {
                    if (n == null || !go.ContainsKey(n.id)) continue;
                    var t = go[n.id].transform;
                    var meta = go[n.id].GetComponent<RoomPrefabMeta>();
                    string arch = meta != null ? meta.archetype : null;
                    var p = t.position;
                    if (!placed.Contains(n.id))
                        FlowTrace.Warn(Sys, $"node '{n.id}' never placed (no edge reached it) - emitting at origin");
                    layout.rooms.Add(new ComposeRoomPlacement
                    {
                        prefab = nodePrefab[n.id],
                        instanceId = n.id,
                        // cellSize=1 => cell carries the exact solved integer world coords.
                        cell = new[] { Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y), Mathf.RoundToInt(p.z) },
                        yawDeg = Mathf.Repeat(t.eulerAngles.y, 360f),
                        archetype = arch,
                        // WO-797: carry the authored encounter block verbatim (rooms own their enemies).
                        encounter = n.encounter,
                    });
                }

                FlowTrace.Step(Sys, $"solved graph '{graph.graphId}': rooms={layout.rooms.Count} " +
                                    $"connections={layout.connections.Count} placed={placed.Count}/{go.Count}");
                return layout;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(tempRoot);
            }
        }

        /// <summary>
        /// Rotate + translate the child room so <paramref name="cSock"/> sits ON
        /// <paramref name="pSock"/> and faces OPPOSITE it (door-to-door). Rotation is yaw-only
        /// (rooms stay upright); the child socket's outward is turned to face back into the
        /// parent socket's outward, then the room is slid so the socket origins coincide.
        /// </summary>
        private static void SolveMate(RoomSocket pSock, RoomSocket cSock, GameObject childGo)
        {
            Vector3 pPos = pSock.WorldPosition;
            Vector3 pFwd = pSock.Outward;

            // Child socket local pose (childGo is currently at identity, so world == local).
            Vector3 cLocalPos = cSock.transform.position;
            Vector3 cLocalFwd = cSock.Outward;

            // Desired child-socket outward = opposite of the parent outward (planar).
            Vector3 target = new Vector3(-pFwd.x, 0f, -pFwd.z);
            Vector3 src = new Vector3(cLocalFwd.x, 0f, cLocalFwd.z);

            float yaw;
            if (target.sqrMagnitude < 1e-6f || src.sqrMagnitude < 1e-6f)
            {
                // Vertical socket (stair): no planar facing to solve - keep identity yaw and
                // just align positions. Doors never hit this branch.
                yaw = 0f;
                FlowTrace.Warn(Sys, $"socket '{cSock.id}' has no planar outward (stair?) - yaw unsolved, aligning position only");
            }
            else
            {
                yaw = Vector3.SignedAngle(src, target, Vector3.up);
            }

            Quaternion rot = Quaternion.Euler(0f, yaw, 0f);
            // Child socket world position if the room is rotated about the origin (pos still 0).
            Vector3 rotatedSocket = rot * cLocalPos;
            Vector3 pos = pPos - rotatedSocket;
            childGo.transform.SetPositionAndRotation(pos, rot);
        }

        // -------- helpers --------

        private static string FirstKey(Dictionary<string, GameObject> go, DungeonGraph graph)
        {
            foreach (var n in graph.nodes)
                if (n != null && go.ContainsKey(n.id)) return n.id;
            foreach (var k in go.Keys) return k;
            return null;
        }

        // Mirror of DungeonBaker.LoadRoomPrefab lookup order (Assets/Dungeon/Rooms then Resources
        // then GUID-by-name). Kept local so the composer does not need DungeonBaker internals.
        private static GameObject LoadRoomPrefab(string prefabStem)
        {
            string p1 = $"Assets/Dungeon/Rooms/{prefabStem}.prefab";
            var a = AssetDatabase.LoadAssetAtPath<GameObject>(p1);
            if (a != null) return a;
            var r = Resources.Load<GameObject>($"Dungeon/Rooms/{prefabStem}");
            if (r != null) return r;
            string[] guids = AssetDatabase.FindAssets($"{prefabStem} t:Prefab");
            foreach (var g in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(g);
                if (path.Contains("/Rooms/") || path.EndsWith($"/{prefabStem}.prefab"))
                {
                    var gob = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (gob != null) return gob;
                }
            }
            return null;
        }

        private static string ToFilesystemPath(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return assetPath;
            if (Path.IsPathRooted(assetPath)) return assetPath;
            if (assetPath.StartsWith("Assets/", StringComparison.Ordinal))
                return Application.dataPath + "/" + assetPath.Substring("Assets/".Length);
            return assetPath;
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
