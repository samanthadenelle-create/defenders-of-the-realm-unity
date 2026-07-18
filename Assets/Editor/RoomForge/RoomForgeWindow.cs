// =============================================================================
// RoomForgeWindow — visual authoring surface for socketed dungeon room prefabs.
// -----------------------------------------------------------------------------
// Menu: Defenders/Dungeon/Room Forge
// Grid grain = 6u cells. Drag KayKit pieces onto the working room root, add
// N/E/S/W door sockets, save prefab + append rooms-catalog.json.
// =============================================================================

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using DeNelle.Core.Diagnostics;
using DeNelle.Dungeons.RoomForge;

namespace DeNelle.Editor.RoomForge
{
    public sealed class RoomForgeWindow : EditorWindow
    {
        private const string RoomsFolder = "Assets/Dungeon/Rooms";
        private const string CatalogPath = "Assets/StreamingAssets/Data/Canonical/dungeon-layouts/rooms-catalog.json";
        private const float CellSize = 6f;

        private string _roomId = "EntryHall";
        private string _archetype = "hub";
        private string _theme = "default";
        private Vector2Int _footprint = new Vector2Int(1, 1);
        private GameObject _workingRoot;
        private Vector2 _scroll;
        private GameObject _piecePrefab;
        private string _status = "Open or create a working room, KayKit props via carousel, add sockets, Save.";

        // Simple KayKit prop carousel (no external package) — scans dungeon pack once.
        private List<GameObject> _kayProps = new List<GameObject>();
        private int _carouselIndex;
        private string _carouselFilter = "barrel,crate,chest,torch,banner,pillar,table,chair,shelf";
        private Vector2 _carouselScroll;
        private bool _carouselLoaded;

        [MenuItem("Defenders/Dungeon/Room Forge")]
        public static void Open()
        {
            var w = GetWindow<RoomForgeWindow>("Room Forge");
            w.minSize = new Vector2(380, 520);
            w.Show();
        }

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            EditorGUILayout.LabelField("Room Forge", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Socketed room authoring (6u cells). Save writes a prefab under Assets/Dungeon/Rooms " +
                "and appends StreamingAssets/.../rooms-catalog.json. Bake layouts via Defenders/Dungeon/Bake Compose Layout.",
                MessageType.Info);

            _roomId = EditorGUILayout.TextField("Room Id", _roomId);
            _archetype = EditorGUILayout.TextField("Archetype", _archetype);
            _theme = EditorGUILayout.TextField("Theme palette", _theme);
            _footprint = EditorGUILayout.Vector2IntField("Footprint (cells)", _footprint);
            if (_footprint.x < 1) _footprint.x = 1;
            if (_footprint.y < 1) _footprint.y = 1;

            EditorGUILayout.Space(6);
            _workingRoot = (GameObject)EditorGUILayout.ObjectField(
                "Working room root", _workingRoot, typeof(GameObject), true);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Create / Reset Working Room"))
                CreateWorkingRoom();
            if (GUILayout.Button("Select Working Room") && _workingRoot != null)
                Selection.activeGameObject = _workingRoot;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("KayKit piece drop", EditorStyles.boldLabel);
            _piecePrefab = (GameObject)EditorGUILayout.ObjectField(
                "Piece prefab/FBX", _piecePrefab, typeof(GameObject), false);
            if (GUILayout.Button("Add piece as child of room") && _piecePrefab != null && _workingRoot != null)
                PlaceProp(_piecePrefab);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("KayKit prop carousel (simple)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Shared wall/floor mats use KayKit dungeon_texture.png (Defenders/Dungeon/Ensure Room Forge Materials). " +
                "Carousel scans KayKit dungeon meshes for prop names (barrel, crate, chest…).",
                MessageType.None);
            _carouselFilter = EditorGUILayout.TextField("Name filter (csv)", _carouselFilter);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Scan KayKit props"))
            {
                ScanKayKitProps();
                _carouselLoaded = true;
            }
            if (GUILayout.Button("Ensure wall/floor mats"))
                RoomForgeMaterials.EnsureMenu();
            EditorGUILayout.EndHorizontal();

            if (_carouselLoaded && _kayProps.Count > 0)
            {
                EditorGUILayout.LabelField($"Props: {_carouselIndex + 1}/{_kayProps.Count}");
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("◀ Prev"))
                    _carouselIndex = (_carouselIndex - 1 + _kayProps.Count) % _kayProps.Count;
                if (GUILayout.Button("Place current") && _workingRoot != null)
                    PlaceProp(_kayProps[_carouselIndex]);
                if (GUILayout.Button("Next ▶"))
                    _carouselIndex = (_carouselIndex + 1) % _kayProps.Count;
                EditorGUILayout.EndHorizontal();

                var cur = _kayProps[_carouselIndex];
                if (cur != null)
                {
                    EditorGUILayout.ObjectField("Current", cur, typeof(GameObject), false);
                    // Thumbnail preview when possible
                    var preview = AssetPreview.GetAssetPreview(cur);
                    if (preview != null)
                    {
                        GUILayout.Label(preview, GUILayout.Width(96), GUILayout.Height(96));
                    }
                }

                _carouselScroll = EditorGUILayout.BeginScrollView(_carouselScroll, GUILayout.Height(80));
                int show = Mathf.Min(12, _kayProps.Count);
                for (int i = 0; i < show; i++)
                {
                    int idx = (_carouselIndex + i) % _kayProps.Count;
                    if (_kayProps[idx] == null) continue;
                    if (GUILayout.Button(_kayProps[idx].name, GUILayout.Height(18)))
                    {
                        _carouselIndex = idx;
                        if (_workingRoot != null) PlaceProp(_kayProps[idx]);
                    }
                }
                EditorGUILayout.EndScrollView();
            }
            else if (_carouselLoaded)
            {
                EditorGUILayout.HelpBox("No props matched filter under Assets/Models/KayKit. Widen filter or import pack.", MessageType.Warning);
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Sockets (door-touch-door)", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("+N Door")) AddCardinalSocket("N", RoomSocketType.Door);
            if (GUILayout.Button("+E Door")) AddCardinalSocket("E", RoomSocketType.Door);
            if (GUILayout.Button("+S Door")) AddCardinalSocket("S", RoomSocketType.Door);
            if (GUILayout.Button("+W Door")) AddCardinalSocket("W", RoomSocketType.Door);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("+N Arch")) AddCardinalSocket("N", RoomSocketType.Arch);
            if (GUILayout.Button("+Stair Up")) AddCardinalSocket("N", RoomSocketType.StairUp);
            if (GUILayout.Button("+Stair Down")) AddCardinalSocket("S", RoomSocketType.StairDown);
            EditorGUILayout.EndHorizontal();

            if (_workingRoot != null)
            {
                var sockets = _workingRoot.GetComponentsInChildren<RoomSocket>(true);
                EditorGUILayout.LabelField($"Sockets on room: {sockets.Length}");
                foreach (var s in sockets)
                {
                    EditorGUILayout.BeginHorizontal();
                    s.id = EditorGUILayout.TextField(s.id, GUILayout.Width(120));
                    s.type = (RoomSocketType)EditorGUILayout.EnumPopup(s.type, GUILayout.Width(90));
                    s.facing = EditorGUILayout.TextField(s.facing, GUILayout.Width(28));
                    s.isSecret = EditorGUILayout.ToggleLeft("secret", s.isSecret, GUILayout.Width(60));
                    if (GUILayout.Button("X", GUILayout.Width(22)))
                    {
                        Undo.DestroyObjectImmediate(s.gameObject);
                        break;
                    }
                    EditorGUILayout.EndHorizontal();
                }
            }

            EditorGUILayout.Space(10);
            if (GUILayout.Button("Save Room Prefab + Catalog", GUILayout.Height(32)))
                SaveRoom();

            EditorGUILayout.Space(4);
            EditorGUILayout.HelpBox(_status, MessageType.None);
            EditorGUILayout.EndScrollView();
        }

        private void CreateWorkingRoom()
        {
            if (string.IsNullOrWhiteSpace(_roomId))
            {
                _status = "Room Id required.";
                return;
            }

            if (_workingRoot != null)
                Undo.DestroyObjectImmediate(_workingRoot);

            _workingRoot = new GameObject($"Room_{_roomId}");
            Undo.RegisterCreatedObjectUndo(_workingRoot, "Room Forge create room");

            var meta = _workingRoot.AddComponent<RoomPrefabMeta>();
            meta.roomId = _roomId.Trim();
            meta.archetype = string.IsNullOrWhiteSpace(_archetype) ? "combat" : _archetype.Trim().ToLowerInvariant();
            meta.themePalette = _theme;
            meta.footprintCells = _footprint;
            meta.cellSize = CellSize;

            // Placeholder floor so the room is visible / walkable without KayKit yet.
            float wx = _footprint.x * CellSize;
            float wz = _footprint.y * CellSize;
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Floor_Placeholder";
            floor.transform.SetParent(_workingRoot.transform, false);
            floor.transform.localPosition = new Vector3(0f, -0.05f, 0f);
            floor.transform.localScale = new Vector3(wx, 0.1f, wz);
            GameObjectUtility.SetStaticEditorFlags(floor,
                StaticEditorFlags.NavigationStatic | StaticEditorFlags.BatchingStatic);

            // Simple KayKit atlas on the placeholder floor (walls get same mat when added).
            RoomForgeMaterials.ApplyToRoomRoot(_workingRoot);

            Selection.activeGameObject = _workingRoot;
            _status = $"Created working room '{_roomId}' ({_footprint.x}x{_footprint.y} cells @ {CellSize}u) with KayKit floor mat.";
        }

        private void PlaceProp(GameObject prefab)
        {
            if (prefab == null || _workingRoot == null) return;
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, _workingRoot.transform);
            if (inst == null)
                inst = (GameObject)Object.Instantiate(prefab, _workingRoot.transform);
            inst.name = prefab.name;
            // Seat near room center on the floor
            inst.transform.localPosition = new Vector3(0f, 0f, 0f);
            Undo.RegisterCreatedObjectUndo(inst, "Room Forge place prop");
            _status = $"Placed KayKit prop '{inst.name}'.";
        }

        private void ScanKayKitProps()
        {
            _kayProps.Clear();
            _carouselIndex = 0;
            string[] roots =
            {
                "Assets/Models/KayKit/dungeon",
                "Assets/Models/KayKit/KayKit Dungeon Remastered 1.1",
                "Assets/Models/KayKit",
            };
            var tokens = _carouselFilter.Split(',')
                .Select(t => t.Trim().ToLowerInvariant())
                .Where(t => t.Length > 0)
                .ToArray();

            var found = new HashSet<string>();
            foreach (var root in roots)
            {
                if (!AssetDatabase.IsValidFolder(root)) continue;
                foreach (var filter in new[] { "t:Model", "t:Prefab" })
                {
                    string[] guids = AssetDatabase.FindAssets(filter, new[] { root });
                    foreach (var g in guids)
                    {
                        string path = AssetDatabase.GUIDToAssetPath(g);
                        if (string.IsNullOrEmpty(path)) continue;
                        // Prefer mesh props, skip huge source blends
                        if (path.EndsWith(".blend", System.StringComparison.OrdinalIgnoreCase)) continue;
                        string file = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
                        bool match = tokens.Length == 0 || tokens.Any(t => file.Contains(t));
                        if (!match) continue;
                        if (!found.Add(path)) continue;
                        var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                        if (go != null) _kayProps.Add(go);
                        if (_kayProps.Count >= 200) break;
                    }
                    if (_kayProps.Count >= 200) break;
                }
                if (_kayProps.Count >= 200) break;
            }

            _kayProps = _kayProps.OrderBy(g => g.name).ToList();
            _status = $"Carousel loaded {_kayProps.Count} KayKit props (filter: {_carouselFilter}).";
            Debug.Log($"[RoomForge] {_status}");
        }

        private void AddCardinalSocket(string facing, RoomSocketType type)
        {
            if (_workingRoot == null)
            {
                _status = "Create a working room first.";
                return;
            }

            float hx = _footprint.x * CellSize * 0.5f;
            float hz = _footprint.y * CellSize * 0.5f;
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
            // Unique id if duplicate.
            int n = 1;
            while (HasSocketId(id))
            {
                n++;
                id = $"{facing.ToLowerInvariant()}_{type.ToString().ToLowerInvariant()}_{n:00}";
            }

            var go = new GameObject($"Socket_{id}");
            Undo.RegisterCreatedObjectUndo(go, "Room Forge add socket");
            go.transform.SetParent(_workingRoot.transform, false);
            go.transform.localPosition = local;
            go.transform.localRotation = rot;

            var sock = go.AddComponent<RoomSocket>();
            sock.id = id;
            sock.type = type;
            sock.facing = facing;
            sock.halfWidth = type == RoomSocketType.Arch ? 1.5f : 1f;

            Selection.activeGameObject = go;
            _status = $"Added socket '{id}' ({type}) facing {facing}.";
        }

        private bool HasSocketId(string id)
        {
            if (_workingRoot == null) return false;
            foreach (var s in _workingRoot.GetComponentsInChildren<RoomSocket>(true))
                if (s != null && s.id == id) return true;
            return false;
        }

        private void SaveRoom()
        {
            if (_workingRoot == null)
            {
                _status = "No working room.";
                return;
            }

            var meta = _workingRoot.GetComponent<RoomPrefabMeta>();
            if (meta == null) meta = _workingRoot.AddComponent<RoomPrefabMeta>();
            meta.roomId = string.IsNullOrWhiteSpace(_roomId) ? meta.roomId : _roomId.Trim();
            meta.archetype = string.IsNullOrWhiteSpace(_archetype) ? meta.archetype : _archetype.Trim().ToLowerInvariant();
            meta.themePalette = _theme;
            meta.footprintCells = _footprint;
            meta.cellSize = CellSize;
            _workingRoot.name = $"Room_{meta.roomId}";

            EnsureFolder(RoomsFolder);
            string prefabPath = $"{RoomsFolder}/{meta.roomId}.prefab";
            bool ok;
            PrefabUtility.SaveAsPrefabAsset(_workingRoot, prefabPath, out ok);
            if (!ok)
            {
                _status = $"FAILED saving prefab: {prefabPath}";
                FlowTrace.Fail("RoomForge", $"failed to save prefab '{prefabPath}'");
                return;
            }

            // Re-apply shared wall/floor atlas so saves stay consistent after prop adds.
            RoomForgeMaterials.ApplyToRoomRoot(_workingRoot,
                useAccentFloor: meta.archetype == "reward" || meta.archetype == "boss");

            var savedSockets = _workingRoot.GetComponentsInChildren<RoomSocket>(true);
            FlowTrace.Step("RoomForge", $"room saved id='{meta.roomId}' archetype='{meta.archetype}' " +
                                        $"footprint={meta.footprintCells.x}x{meta.footprintCells.y} sockets={savedSockets.Length} -> {prefabPath}");
            AppendCatalog(meta, prefabPath, savedSockets);
            AssetDatabase.Refresh();
            _status = $"Saved {prefabPath} + catalog entry '{meta.roomId}' (KayKit wall/floor mats).";
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

        private static void AppendCatalog(RoomPrefabMeta meta, string prefabPath, RoomSocket[] sockets)
        {
            EnsureFolder("Assets/StreamingAssets/Data/Canonical/dungeon-layouts");

            RoomCatalogFile file;
            if (File.Exists(CatalogPath))
            {
                try
                {
                    file = JsonConvert.DeserializeObject<RoomCatalogFile>(File.ReadAllText(CatalogPath))
                           ?? new RoomCatalogFile();
                }
                catch
                {
                    file = new RoomCatalogFile();
                }
            }
            else file = new RoomCatalogFile();

            if (file.rooms == null) file.rooms = new List<RoomCatalogEntry>();

            var entry = new RoomCatalogEntry
            {
                id = meta.roomId,
                prefabPath = prefabPath,
                archetype = meta.archetype,
                themePalette = meta.themePalette,
                footprintCells = new[] { meta.footprintCells.x, meta.footprintCells.y },
                cellSize = meta.cellSize,
                sockets = new List<RoomCatalogSocket>(),
            };
            foreach (var s in sockets)
            {
                if (s == null) continue;
                var lp = s.transform.localPosition;
                entry.sockets.Add(new RoomCatalogSocket
                {
                    id = s.id,
                    type = s.type.ToString(),
                    facing = s.facing,
                    isSecret = s.isSecret,
                    localPosition = new[] { lp.x, lp.y, lp.z },
                });
            }

            // Replace existing id.
            file.rooms.RemoveAll(r => r != null && r.id == entry.id);
            file.rooms.Add(entry);

            string json = JsonConvert.SerializeObject(file, Formatting.Indented);

            // Resources mirror (WebGL-safe path if ever loaded at runtime) — dual-copy, byte-identical.
            string resDir = "Assets/Resources/Data/Canonical/dungeon-layouts";
            EnsureFolder(resDir);
            string resPath = resDir + "/rooms-catalog.json";
            bool wrote = Guard.Try("RoomForge", "write rooms-catalog dual-copy", () =>
            {
                File.WriteAllText(CatalogPath, json, Encoding.UTF8);
                File.WriteAllText(resPath, json, Encoding.UTF8);
            });
            FlowTrace.Step("RoomForge", $"catalog write id='{meta.roomId}' entries={file.rooms.Count} " +
                                        $"dualCopy={(wrote ? "ok" : "FAILED")} (StreamingAssets + Resources)");
        }
    }
}
