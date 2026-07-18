// =============================================================================
// DungeonBaker — instantiate socketed rooms from DungeonComposeLayout JSON.
// -----------------------------------------------------------------------------
// Menu: Defenders/Dungeon/Bake Compose Layout
// Hard gate: each listed connection must mate within maxMateDistance and
// opposing alignment. Unmated sockets → seal (wall box) or secret flag.
// Reuses NavMesh bake patterns from DungeonChainBuilder / DungeonComposer.
// =============================================================================

using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Unity.AI.Navigation;
using DeNelle.Dungeons.RoomForge;

namespace DeNelle.Editor.RoomForge
{
    public static class DungeonBaker
    {
        private const string LayoutsFolder = "Assets/StreamingAssets/Data/Canonical/dungeon-layouts";
        private const string DefaultLayout = "d4_sunken_crypt_spine.json";
        private const string OutputScenesFolder = "Assets/Scenes/DungeonCompose";

        [MenuItem("Defenders/Dungeon/Bake Compose Layout (default spine)")]
        public static void BakeDefault()
        {
            string path = Path.Combine(LayoutsFolder, DefaultLayout);
            BakeFromFile(path);
        }

        [MenuItem("Defenders/Dungeon/Bake Compose Layout From Selected JSON")]
        public static void BakeSelected()
        {
            var obj = Selection.activeObject;
            string path = obj != null ? AssetDatabase.GetAssetPath(obj) : null;
            if (string.IsNullOrEmpty(path) || !path.EndsWith(".json"))
            {
                EditorUtility.DisplayDialog("DungeonBaker",
                    "Select a dungeon-layouts JSON asset first (or use Bake Compose Layout default spine).",
                    "OK");
                return;
            }
            BakeFromFile(path);
        }

        /// <summary>Batchmode entry: -executeMethod DeNelle.Editor.RoomForge.DungeonBaker.BakeDefault</summary>
        public static void BakeDefaultBatch()
        {
            BakeDefault();
            EditorApplication.Exit(0);
        }

        public static void BakeFromFile(string layoutAssetPath)
        {
            if (!File.Exists(layoutAssetPath))
            {
                // Unity asset path → filesystem
                string fs = layoutAssetPath.Replace("Assets/", Application.dataPath + "/");
                if (!File.Exists(fs))
                {
                    Debug.LogError($"[DungeonBaker] Layout not found: {layoutAssetPath}");
                    return;
                }
                layoutAssetPath = fs;
            }
            else if (layoutAssetPath.StartsWith("Assets/"))
            {
                layoutAssetPath = layoutAssetPath.Replace("Assets/", Application.dataPath + "/");
            }

            string json = File.ReadAllText(layoutAssetPath, Encoding.UTF8);
            DungeonComposeLayout layout;
            try
            {
                layout = JsonConvert.DeserializeObject<DungeonComposeLayout>(json);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[DungeonBaker] JSON parse failed: {ex.Message}");
                return;
            }

            if (layout == null || layout.rooms == null || layout.rooms.Count == 0)
            {
                Debug.LogError("[DungeonBaker] Layout empty — abort.");
                return;
            }

            float cell = layout.cellSize > 0.1f ? layout.cellSize : 6f;
            var rules = layout.rules ?? new ComposeRules();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject($"DungeonCompose_{layout.dungeonId}").transform;

            // Instance lookup
            var instances = new Dictionary<string, GameObject>();
            var instanceMeta = new Dictionary<string, string>(); // instanceId -> archetype

            foreach (var place in layout.rooms)
            {
                if (place == null || string.IsNullOrEmpty(place.prefab)) continue;
                string instId = string.IsNullOrEmpty(place.instanceId) ? place.prefab : place.instanceId;
                GameObject prefab = LoadRoomPrefab(place.prefab);
                if (prefab == null)
                {
                    Debug.LogWarning($"[DungeonBaker] Missing prefab '{place.prefab}' — spawning placeholder box room.");
                    prefab = null;
                }

                GameObject go;
                if (prefab != null)
                {
                    go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root);
                    if (go == null) go = Object.Instantiate(prefab, root);
                }
                else
                {
                    go = CreatePlaceholderRoom(instId, root);
                }

                go.name = instId;
                int cx = place.cell != null && place.cell.Length > 0 ? place.cell[0] : 0;
                int cy = place.cell != null && place.cell.Length > 1 ? place.cell[1] : 0;
                int cz = place.cell != null && place.cell.Length > 2 ? place.cell[2] : 0;
                go.transform.position = new Vector3(cx * cell, cy * cell, cz * cell);
                go.transform.rotation = Quaternion.Euler(0f, place.yawDeg, 0f);

                instances[instId] = go;
                string arch = place.archetype;
                if (string.IsNullOrEmpty(arch))
                {
                    var meta = go.GetComponent<RoomPrefabMeta>();
                    arch = meta != null ? meta.archetype : "combat";
                }
                instanceMeta[instId] = arch ?? "combat";
            }

            // Mate connections
            int mateOk = 0, mateFail = 0;
            if (layout.connections != null)
            {
                foreach (var c in layout.connections)
                {
                    if (c == null) continue;
                    if (!instances.TryGetValue(c.fromInstance, out var aGo) ||
                        !instances.TryGetValue(c.toInstance, out var bGo))
                    {
                        Debug.LogError($"[DungeonBaker] Connection references missing instance " +
                                       $"'{c.fromInstance}' -> '{c.toInstance}'.");
                        mateFail++;
                        continue;
                    }

                    var aSock = FindSocket(aGo, c.fromSocket);
                    var bSock = FindSocket(bGo, c.toSocket);
                    if (aSock == null || bSock == null)
                    {
                        Debug.LogError($"[DungeonBaker] Socket missing: {c.fromInstance}.{c.fromSocket} " +
                                       $"or {c.toInstance}.{c.toSocket}");
                        mateFail++;
                        continue;
                    }

                    if (!TypesCompatible(aSock.type, bSock.type))
                    {
                        Debug.LogError($"[DungeonBaker] Type mismatch {aSock.type} vs {bSock.type} " +
                                       $"on {c.fromInstance}.{c.fromSocket}");
                        mateFail++;
                        continue;
                    }

                    float dist = Vector3.Distance(aSock.WorldPosition, bSock.WorldPosition);
                    float maxD = rules.maxMateDistance > 0f ? rules.maxMateDistance : 1.25f;
                    // Prefer sliding the "to" room so sockets touch if slightly off grid.
                    if (dist > maxD)
                    {
                        Vector3 delta = aSock.WorldPosition - bSock.WorldPosition;
                        // Only planar nudge of the whole "to" instance.
                        bGo.transform.position += new Vector3(delta.x, 0f, delta.z);
                        dist = Vector3.Distance(aSock.WorldPosition, bSock.WorldPosition);
                    }

                    float align = Vector3.Dot(aSock.Outward.normalized, -bSock.Outward.normalized);
                    if (dist > maxD || align < 0.25f)
                    {
                        Debug.LogError($"[DungeonBaker] Mate FAIL {c.fromInstance}.{c.fromSocket} <-> " +
                                       $"{c.toInstance}.{c.toSocket} dist={dist:F2} align={align:F2} " +
                                       $"(door-touch-door hard gate).");
                        mateFail++;
                        continue;
                    }

                    string connId = $"{c.fromInstance}.{c.fromSocket}::{c.toInstance}.{c.toSocket}";
                    aSock.matedTo = connId;
                    bSock.matedTo = connId;
                    mateOk++;
                    Debug.Log($"[DungeonBaker] Mated {connId} dist={dist:F2} align={align:F2}");
                }
            }

            // Seal unmated
            int sealedN = 0;
            if (rules.sealUnmated)
            {
                foreach (var kv in instances)
                {
                    foreach (var s in kv.Value.GetComponentsInChildren<RoomSocket>(true))
                    {
                        if (s == null || s.IsMated) continue;
                        SealSocket(s);
                        sealedN++;
                    }
                }
            }

            // Pacing lint
            LintPacing(instanceMeta, rules);

            // Lighting defaults (dim)
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.08f, 0.09f, 0.12f);
            var lightGo = new GameObject("DirLight");
            lightGo.transform.SetParent(root, false);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 0.35f;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // NavMesh
            var navHost = new GameObject("NavMesh");
            navHost.transform.SetParent(root, false);
            var surface = navHost.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.layerMask = ~0;
            surface.BuildNavMesh();
            bool walkable = NavMesh.SamplePosition(Vector3.zero, out _, 8f, NavMesh.AllAreas);
            Debug.Log($"[DungeonBaker] NavMesh baked; sample@origin walkable={walkable}.");

            // Save scene
            if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
                AssetDatabase.CreateFolder("Assets", "Scenes");
            if (!AssetDatabase.IsValidFolder(OutputScenesFolder))
                AssetDatabase.CreateFolder("Assets/Scenes", "DungeonCompose");

            string scenePath = $"{OutputScenesFolder}/{layout.dungeonId}.unity";
            EditorSceneManager.MarkSceneDirty(scene);
            bool saved = EditorSceneManager.SaveScene(scene, scenePath);
            EnsureInBuildSettings(scenePath);

            Debug.Log($"[DungeonBaker] DONE id={layout.dungeonId} rooms={instances.Count} " +
                      $"matesOk={mateOk} matesFail={mateFail} sealed={sealedN} saved={saved} path={scenePath}");

            if (mateFail > 0)
                Debug.LogError($"[DungeonBaker] HARD GATE: {mateFail} mate failure(s) — fix layout or sockets.");
        }

        private static GameObject LoadRoomPrefab(string prefabStem)
        {
            // Prefer Assets/Dungeon/Rooms/<stem>.prefab
            string p1 = $"Assets/Dungeon/Rooms/{prefabStem}.prefab";
            var a = AssetDatabase.LoadAssetAtPath<GameObject>(p1);
            if (a != null) return a;
            // Resources
            var r = Resources.Load<GameObject>($"Dungeon/Rooms/{prefabStem}");
            if (r != null) return r;
            // GUID search by name
            string[] guids = AssetDatabase.FindAssets($"{prefabStem} t:Prefab");
            foreach (var g in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(g);
                if (path.Contains("/Rooms/") || path.EndsWith($"/{prefabStem}.prefab"))
                {
                    var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (go != null) return go;
                }
            }
            return null;
        }

        private static GameObject CreatePlaceholderRoom(string id, Transform parent)
        {
            var go = new GameObject(id);
            go.transform.SetParent(parent, false);
            var meta = go.AddComponent<RoomPrefabMeta>();
            meta.roomId = id;
            meta.archetype = "combat";
            meta.cellSize = 6f;
            meta.footprintCells = Vector2Int.one;

            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Floor";
            floor.transform.SetParent(go.transform, false);
            floor.transform.localPosition = new Vector3(0f, -0.05f, 0f);
            floor.transform.localScale = new Vector3(6f, 0.1f, 6f);
            GameObjectUtility.SetStaticEditorFlags(floor, StaticEditorFlags.NavigationStatic);

            // Four door sockets at cardinals
            AddPlaceholderSocket(go, "north_door_01", "N", new Vector3(0, 0, 3f), Vector3.forward);
            AddPlaceholderSocket(go, "south_door_01", "S", new Vector3(0, 0, -3f), Vector3.back);
            AddPlaceholderSocket(go, "east_door_01", "E", new Vector3(3f, 0, 0), Vector3.right);
            AddPlaceholderSocket(go, "west_door_01", "W", new Vector3(-3f, 0, 0), Vector3.left);
            return go;
        }

        private static void AddPlaceholderSocket(GameObject room, string id, string facing, Vector3 local, Vector3 outward)
        {
            var sgo = new GameObject($"Socket_{id}");
            sgo.transform.SetParent(room.transform, false);
            sgo.transform.localPosition = local;
            sgo.transform.localRotation = Quaternion.LookRotation(outward);
            var sock = sgo.AddComponent<RoomSocket>();
            sock.id = id;
            sock.type = RoomSocketType.Door;
            sock.facing = facing;
        }

        private static RoomSocket FindSocket(GameObject room, string socketId)
        {
            if (room == null || string.IsNullOrEmpty(socketId)) return null;
            foreach (var s in room.GetComponentsInChildren<RoomSocket>(true))
                if (s != null && s.id == socketId) return s;
            return null;
        }

        private static bool TypesCompatible(RoomSocketType a, RoomSocketType b)
        {
            if (a == b) return true;
            if (a == RoomSocketType.Door && b == RoomSocketType.Arch) return true;
            if (a == RoomSocketType.Arch && b == RoomSocketType.Door) return true;
            if (a == RoomSocketType.StairUp && b == RoomSocketType.StairDown) return true;
            if (a == RoomSocketType.StairDown && b == RoomSocketType.StairUp) return true;
            return false;
        }

        private static void SealSocket(RoomSocket s)
        {
            if (s.isSecret)
            {
                // Invisible marker only — runtime can treat as illusory (no collider).
                s.matedTo = "SEALED_SECRET";
                Debug.Log($"[DungeonBaker] Secret-sealed unmated socket {s.id} on {s.transform.root.name}");
                return;
            }

            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = $"Seal_{s.id}";
            wall.transform.SetParent(s.transform, false);
            wall.transform.localPosition = Vector3.forward * 0.15f;
            wall.transform.localRotation = Quaternion.identity;
            wall.transform.localScale = new Vector3(s.halfWidth * 2f, 2.5f, 0.35f);
            GameObjectUtility.SetStaticEditorFlags(wall, StaticEditorFlags.NavigationStatic);
            s.matedTo = "SEALED_WALL";
        }

        private static void LintPacing(Dictionary<string, string> archetypes, ComposeRules rules)
        {
            int combat = 0, lore = 0, reward = 0, other = 0;
            foreach (var a in archetypes.Values)
            {
                string k = (a ?? "").ToLowerInvariant();
                if (k.Contains("combat") || k.Contains("boss")) combat++;
                else if (k.Contains("lore") || k.Contains("story")) lore++;
                else if (k.Contains("reward") || k.Contains("loot") || k.Contains("treasure")) reward++;
                else other++;
            }
            int total = combat + lore + reward + other;
            if (total <= 0) return;
            float rc = combat / (float)total;
            float rl = lore / (float)total;
            float rr = reward / (float)total;
            Debug.Log($"[DungeonBaker] Pacing lint rooms={total} combat={rc:P0} (target {rules.pacingCombat:P0}) " +
                      $"lore={rl:P0} (target {rules.pacingLore:P0}) reward={rr:P0} (target {rules.pacingReward:P0}) other={other}");
            // Soft warn only — small spines will not hit 60/20/20.
            if (total >= 5 && Mathf.Abs(rc - rules.pacingCombat) > 0.25f)
                Debug.LogWarning("[DungeonBaker] Pacing: combat ratio far from 60/20/20 canon — author more lore/reward rooms.");
        }

        private static void EnsureInBuildSettings(string scenePath)
        {
            var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (var s in list)
                if (s.path == scenePath) return;
            list.Add(new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = list.ToArray();
        }
    }
}
