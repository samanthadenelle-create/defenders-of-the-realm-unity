// =============================================================================
// RoomForgeWindow — visual authoring surface for socketed dungeon room prefabs.
// -----------------------------------------------------------------------------
// Menu: Defenders/Dungeon/Room Forge
// Grid grain = 6u cells. Drag KayKit pieces onto the working room root, add
// N/E/S/W door sockets, save prefab + append rooms-catalog.json.
// =============================================================================

using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
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
        private string _status = "Open or create a working room, drop KayKit pieces, add sockets, Save.";

        [MenuItem("Defenders/Dungeon/Room Forge")]
        public static void Open()
        {
            var w = GetWindow<RoomForgeWindow>("Room Forge");
            w.minSize = new Vector2(360, 420);
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
            {
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(_piecePrefab, _workingRoot.transform);
                if (inst == null)
                    inst = (GameObject)Object.Instantiate(_piecePrefab, _workingRoot.transform);
                inst.name = _piecePrefab.name;
                Undo.RegisterCreatedObjectUndo(inst, "Room Forge add piece");
                _status = $"Added piece '{inst.name}'.";
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

            Selection.activeGameObject = _workingRoot;
            _status = $"Created working room '{_roomId}' ({_footprint.x}x{_footprint.y} cells @ {CellSize}u).";
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
                return;
            }

            AppendCatalog(meta, prefabPath, _workingRoot.GetComponentsInChildren<RoomSocket>(true));
            AssetDatabase.Refresh();
            _status = $"Saved {prefabPath} + catalog entry '{meta.roomId}'.";
            Debug.Log($"[RoomForge] {_status}");
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
            File.WriteAllText(CatalogPath, json, Encoding.UTF8);

            // Resources mirror (WebGL-safe path if ever loaded at runtime).
            string resDir = "Assets/Resources/Data/Canonical/dungeon-layouts";
            EnsureFolder(resDir);
            string resPath = resDir + "/rooms-catalog.json";
            File.WriteAllText(resPath, json, Encoding.UTF8);
        }
    }
}
