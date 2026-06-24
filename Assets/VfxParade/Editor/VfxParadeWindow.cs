// =============================================================================
// VfxParade.Editor.VfxParadeWindow - owner-curated VFX effect browser.
// -----------------------------------------------------------------------------
// A self-contained EditorWindow (Tools > VFX Parade) that parades particle/mesh
// effect prefabs past the owner one at a time, ANIMATING their ParticleSystems
// live in an offscreen PreviewRenderUtility viewport. The owner tags the combat
// MOMENT each effect is for and bookmarks her picks; the tool appends them to
// Assets/VfxParade/vfx-picks.json which the AI then reads and wires.
//
// The AI cannot see pixels, so this makes the OWNER the visual judge and emits
// her picks as data. Editor-only - no runtime/game references.
//
// Mirrors the proven Offset Forge PreviewRenderUtility pattern in this repo:
// OnEnable/OnDisable/OnDestroy cleanup, neutral light/camera/bg, left-drag orbit
// + scroll zoom, bounds framing. Compatible Unity 2021.3 LTS through Unity 6.
// ASCII-only in Debug.Log strings.
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace VfxParade.Editor
{
    public sealed class VfxParadeWindow : EditorWindow
    {
        // ---- JSON mirror (self-contained, JsonUtility) ----------------------
        [Serializable]
        private sealed class Pick
        {
            public string path;
            public string name;
            public string moment;
            public string note;
        }

        [Serializable]
        private sealed class PickFile
        {
            public List<Pick> picks = new List<Pick>();
        }

        // ---- Source / list state --------------------------------------------
        private string _sourceFolder = "Assets/Spells Pack";
        private int _categoryIndex;   // index into Categories
        private static readonly string[] Categories =
        {
            "All", "Casting", "Projectile", "Explosion", "Aura",
            "Buff", "Shield", "Slash", "Hit", "Death"
        };

        private readonly List<string> _prefabPaths = new List<string>();
        private int _index;

        // ---- Preview state ---------------------------------------------------
        private GameObject _previewInstance;
        private ParticleSystem[] _systems;          // cached from the current instance
        private PreviewRenderUtility _preview;

        // ---- Particle simulation timing -------------------------------------
        private float _accumTime;                    // simulated seconds for current prefab
        private double _lastEditorTime;              // for delta between updates

        // ---- Camera orbit (separate from anything authored) -----------------
        private float _camYaw = 30f;
        private float _camPitch = 20f;
        private float _camDistance = 5f;
        private Vector3 _camPivot = Vector3.zero;
        private bool _framed;

        // ---- Transport -------------------------------------------------------
        private bool _playing;
        private float _intervalSeconds = 10f;        // DEFAULT 10s auto-advance
        private double _lastAdvanceTime;

        // ---- Bookmark --------------------------------------------------------
        private static readonly string[] Moments =
        {
            "cast", "hit", "death", "buff", "projectile", "aura", "other"
        };
        private int _momentIndex;
        private string _pendingNote = "";
        private const string PicksPath = "Assets/VfxParade/vfx-picks.json";
        private PickFile _picks = new PickFile();

        private Vector2 _scroll;

        // ---------------------------------------------------------------------
        [MenuItem("Tools/VFX Parade")]
        public static void Open()
        {
            var win = GetWindow<VfxParadeWindow>("VFX Parade");
            win.minSize = new Vector2(460, 640);
            win.Show();
        }

        private void OnEnable()
        {
            EnsurePreviewUtility();
            RefreshList();
            LoadPicks();
            _lastEditorTime = EditorApplication.timeSinceStartup;
            _lastAdvanceTime = EditorApplication.timeSinceStartup;
            EditorApplication.update += OnEditorUpdate;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            CleanupPreview();
        }

        private void OnDestroy()
        {
            EditorApplication.update -= OnEditorUpdate;
            CleanupPreview();
        }

        // ---------------------------------------------------------------------
        // Editor update - drives particle sim time + auto-advance + repaint.
        // ---------------------------------------------------------------------
        private void OnEditorUpdate()
        {
            double now = EditorApplication.timeSinceStartup;
            float dt = (float)(now - _lastEditorTime);
            _lastEditorTime = now;
            if (dt < 0f) dt = 0f;
            if (dt > 0.2f) dt = 0.2f; // clamp big editor hitches

            // Advance simulated particle time so the effect plays live.
            if (_previewInstance != null)
            {
                _accumTime += dt;
                Repaint();
            }

            // Auto-advance the parade.
            if (_playing && _prefabPaths.Count > 0)
            {
                if (now - _lastAdvanceTime >= _intervalSeconds)
                {
                    _lastAdvanceTime = now;
                    Next();
                }
            }
        }

        // ---------------------------------------------------------------------
        // Preview lifecycle (mirrors Offset Forge)
        // ---------------------------------------------------------------------
        private void EnsurePreviewUtility()
        {
            if (_preview != null) return;
            _preview = new PreviewRenderUtility();
            _preview.camera.clearFlags = CameraClearFlags.SolidColor;
            _preview.camera.backgroundColor = new Color(0.16f, 0.16f, 0.20f, 1f);
            _preview.camera.nearClipPlane = 0.01f;
            _preview.camera.farClipPlane = 1000f;
            _preview.camera.fieldOfView = 45f;

            if (_preview.lights != null && _preview.lights.Length > 0)
            {
                _preview.lights[0].intensity = 1.1f;
                _preview.lights[0].transform.rotation = Quaternion.Euler(40f, 40f, 0f);
                _preview.lights[0].color = new Color(1f, 0.98f, 0.95f);
                if (_preview.lights.Length > 1)
                {
                    _preview.lights[1].intensity = 0.6f;
                    _preview.lights[1].transform.rotation = Quaternion.Euler(-20f, -120f, 0f);
                    _preview.lights[1].color = new Color(0.7f, 0.75f, 0.85f);
                }
            }
            _preview.ambientColor = new Color(0.4f, 0.4f, 0.44f, 1f);
        }

        private void CleanupPreview()
        {
            DestroyPreviewInstance();
            if (_preview != null)
            {
                try { _preview.Cleanup(); }
                catch (Exception e) { Debug.LogWarning("[VfxParade] preview cleanup failed: " + e.Message); }
                _preview = null;
            }
        }

        private void DestroyPreviewInstance()
        {
            if (_previewInstance != null)
            {
                DestroyImmediate(_previewInstance);
                _previewInstance = null;
            }
            _systems = null;
        }

        // ---------------------------------------------------------------------
        // Prefab list
        // ---------------------------------------------------------------------
        private void RefreshList()
        {
            _prefabPaths.Clear();
            string folder = string.IsNullOrEmpty(_sourceFolder) ? "Assets" : _sourceFolder;

            if (!AssetDatabase.IsValidFolder(folder))
            {
                Debug.LogWarning("[VfxParade] source folder is not a valid project folder: " + folder);
                _index = 0;
                RebuildPreviewInstance();
                return;
            }

            string category = Categories[Mathf.Clamp(_categoryIndex, 0, Categories.Length - 1)];
            bool filterByCategory = category != "All";

            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { folder });
            if (guids != null)
            {
                for (int i = 0; i < guids.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    if (string.IsNullOrEmpty(path)) continue;
                    if (filterByCategory &&
                        path.IndexOf(category, StringComparison.OrdinalIgnoreCase) < 0)
                        continue;
                    _prefabPaths.Add(path);
                }
            }

            _prefabPaths.Sort(StringComparer.OrdinalIgnoreCase);

            if (_index < 0) _index = 0;
            if (_index >= _prefabPaths.Count) _index = 0;
            RebuildPreviewInstance();
        }

        private void RebuildPreviewInstance()
        {
            DestroyPreviewInstance();
            EnsurePreviewUtility();
            _accumTime = 0f; // restart accumulation for the new prefab

            if (_prefabPaths.Count == 0) return;
            _index = Mathf.Clamp(_index, 0, _prefabPaths.Count - 1);

            string path = _prefabPaths[_index];
            GameObject asset = null;
            try
            {
                asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[VfxParade] failed to load prefab at '" + path + "': " + e.Message);
                asset = null;
            }

            if (asset == null)
            {
                Debug.LogWarning("[VfxParade] skipping null or broken prefab at '" + path + "'.");
                return;
            }

            try
            {
                _previewInstance = (GameObject)UnityEngine.Object.Instantiate(asset);
                _previewInstance.hideFlags = HideFlags.HideAndDontSave;
                _previewInstance.transform.position = Vector3.zero;
                _previewInstance.transform.rotation = Quaternion.identity;
                _preview.AddSingleGO(_previewInstance);
                _systems = _previewInstance.GetComponentsInChildren<ParticleSystem>(true);
                _framed = false;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[VfxParade] failed to instantiate prefab '" + path + "': " + e.Message);
                DestroyPreviewInstance();
            }
        }

        private void Next()
        {
            if (_prefabPaths.Count == 0) return;
            _index = (_index + 1) % _prefabPaths.Count;
            RebuildPreviewInstance();
            Repaint();
        }

        private void Prev()
        {
            if (_prefabPaths.Count == 0) return;
            _index = (_index - 1 + _prefabPaths.Count) % _prefabPaths.Count;
            RebuildPreviewInstance();
            Repaint();
        }

        // ---------------------------------------------------------------------
        // GUI
        // ---------------------------------------------------------------------
        private void OnGUI()
        {
            EnsurePreviewUtility();
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            DrawSourcePanel();
            EditorGUILayout.Space(4);
            DrawIndexLabel();
            EditorGUILayout.Space(2);
            DrawViewport();
            EditorGUILayout.Space(6);
            DrawTransport();
            EditorGUILayout.Space(6);
            DrawBookmarkPanel();
            EditorGUILayout.Space(6);
            DrawPicksList();

            EditorGUILayout.EndScrollView();
        }

        private void DrawSourcePanel()
        {
            EditorGUILayout.LabelField("Source", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            string newFolder = EditorGUILayout.TextField("Source folder", _sourceFolder);
            int newCategory = EditorGUILayout.Popup("Category filter", _categoryIndex, Categories);
            bool changed = EditorGUI.EndChangeCheck();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Rescan", GUILayout.Width(120)))
                    changed = true;
                EditorGUILayout.LabelField(_prefabPaths.Count + " prefab(s)", GUILayout.Width(120));
            }

            if (changed)
            {
                _sourceFolder = newFolder;
                _categoryIndex = newCategory;
                _index = 0;
                RefreshList();
                Repaint();
            }
        }

        private void DrawIndexLabel()
        {
            int total = _prefabPaths.Count;
            int human = total == 0 ? 0 : _index + 1;
            string path = (total > 0 && _index >= 0 && _index < total) ? _prefabPaths[_index] : "<none>";
            var style = new GUIStyle(EditorStyles.boldLabel) { fontSize = 13, wordWrap = true };
            EditorGUILayout.LabelField("[" + human + " / " + total + "]  " + path, style);
        }

        private void DrawViewport()
        {
            var rect = GUILayoutUtility.GetRect(10, 4000, 280, 280, GUILayout.ExpandWidth(true));

            HandleViewportInput(rect);

            if (Event.current.type != EventType.Repaint)
                return;

            if (_preview == null || _previewInstance == null)
            {
                EditorGUI.DrawRect(rect, new Color(0.14f, 0.14f, 0.16f));
                var prev = GUI.color;
                GUI.color = new Color(0.7f, 0.7f, 0.7f);
                string msg = _prefabPaths.Count == 0 ? "No prefabs in folder" : "No effect loaded";
                GUI.Label(rect, msg, EditorStyles.centeredGreyMiniLabel);
                GUI.color = prev;
                return;
            }

            try
            {
                SimulateParticles();
                if (!_framed) FrameCamera();
                PositionCamera();

                _preview.BeginPreview(rect, GUIStyle.none);
                _preview.Render(true, false);
                var tex = _preview.EndPreview();
                GUI.DrawTexture(rect, tex, ScaleMode.StretchToFill, false);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[VfxParade] viewport render failed: " + e.Message);
                EditorGUI.DrawRect(rect, new Color(0.14f, 0.14f, 0.16f));
            }
        }

        // Advance every ParticleSystem to the accumulated time so the effect plays
        // live in the editor window. Simulate(t, withChildren:true, restart:false).
        private void SimulateParticles()
        {
            if (_systems == null || _systems.Length == 0) return;
            for (int i = 0; i < _systems.Length; i++)
            {
                var ps = _systems[i];
                if (ps == null) continue;
                try
                {
                    // Simulate per-system without recursing into children (we already
                    // hold the full flattened list), seeking to absolute accum time.
                    ps.Simulate(_accumTime, false, true, false);
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[VfxParade] particle simulate failed on '" +
                                     ps.name + "': " + e.Message);
                }
            }
        }

        private void HandleViewportInput(Rect rect)
        {
            var e = Event.current;
            if (!rect.Contains(e.mousePosition) && e.type != EventType.MouseDrag)
                return;

            switch (e.type)
            {
                case EventType.ScrollWheel:
                    if (rect.Contains(e.mousePosition))
                    {
                        _camDistance = Mathf.Clamp(_camDistance * (1f + e.delta.y * 0.05f), 0.05f, 5000f);
                        e.Use();
                        Repaint();
                    }
                    break;

                case EventType.MouseDrag:
                    if (e.button == 0)
                    {
                        _camYaw += e.delta.x * 0.5f;
                        _camPitch += e.delta.y * 0.5f;
                        _camPitch = Mathf.Clamp(_camPitch, -89f, 89f);
                        e.Use();
                        Repaint();
                    }
                    break;
            }
        }

        private void FrameCamera()
        {
            Bounds b;
            if (TryGetInstanceBounds(out b))
            {
                _camPivot = b.center;
                float radius = Mathf.Max(0.25f, b.extents.magnitude);
                _camDistance = radius * 2.5f;
            }
            else
            {
                // Particle-only effects may have no renderer bounds yet; use a default.
                _camPivot = Vector3.zero;
                _camDistance = 5f;
            }
            _framed = true;
        }

        private void PositionCamera()
        {
            Quaternion rot = Quaternion.Euler(_camPitch, _camYaw, 0f);
            Vector3 dir = rot * Vector3.forward;
            _preview.camera.transform.position = _camPivot - dir * _camDistance;
            _preview.camera.transform.rotation = rot;
        }

        private bool TryGetInstanceBounds(out Bounds bounds)
        {
            bounds = new Bounds(Vector3.zero, Vector3.zero);
            bool has = false;
            if (_previewInstance == null) return false;

            var rends = _previewInstance.GetComponentsInChildren<Renderer>();
            if (rends != null)
            {
                for (int i = 0; i < rends.Length; i++)
                {
                    if (rends[i] == null) continue;
                    if (!has) { bounds = rends[i].bounds; has = true; }
                    else bounds.Encapsulate(rends[i].bounds);
                }
            }
            return has;
        }

        private void DrawTransport()
        {
            EditorGUILayout.LabelField("Transport", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("< Prev", GUILayout.Height(26)))
                    Prev();

                string playLabel = _playing ? "Pause" : "Play";
                if (GUILayout.Button(playLabel, GUILayout.Height(26)))
                {
                    _playing = !_playing;
                    _lastAdvanceTime = EditorApplication.timeSinceStartup;
                }

                if (GUILayout.Button("Next >", GUILayout.Height(26)))
                    Next();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Auto-advance interval (s)", GUILayout.Width(180));
                float v = EditorGUILayout.FloatField(_intervalSeconds, GUILayout.Width(80));
                _intervalSeconds = v < 0.5f ? 0.5f : v;
            }

            if (GUILayout.Button("Restart effect"))
            {
                _accumTime = 0f;
                Repaint();
            }
        }

        private void DrawBookmarkPanel()
        {
            EditorGUILayout.LabelField("Bookmark", EditorStyles.boldLabel);

            _momentIndex = EditorGUILayout.Popup("Moment", _momentIndex, Moments);

            _pendingNote = EditorGUILayout.TextField("Note", _pendingNote);
            EditorGUILayout.LabelField(
                "how you'd use it: 'level-up only', 'boss crit', 'heal aura'",
                EditorStyles.miniLabel);

            using (new EditorGUI.DisabledScope(_prefabPaths.Count == 0 || _index < 0 || _index >= _prefabPaths.Count))
            {
                if (GUILayout.Button("Bookmark this", GUILayout.Height(26)))
                    BookmarkCurrent();
            }

            EditorGUILayout.LabelField("Picks saved: " + _picks.picks.Count, EditorStyles.miniLabel);
        }

        private void DrawPicksList()
        {
            EditorGUILayout.LabelField("Current picks (" + PicksPath + ")", EditorStyles.boldLabel);

            if (_picks.picks.Count == 0)
            {
                EditorGUILayout.HelpBox("No picks yet. Tag a moment and click 'Bookmark this'.", MessageType.Info);
                return;
            }

            int removeAt = -1;
            for (int i = 0; i < _picks.picks.Count; i++)
            {
                var p = _picks.picks[i];
                if (p == null) continue;
                using (new EditorGUILayout.HorizontalScope())
                {
                    string noteSuffix = string.IsNullOrEmpty(p.note) ? "" : " - " + p.note;
                    EditorGUILayout.LabelField("[" + p.moment + "]  " + p.name + noteSuffix, GUILayout.ExpandWidth(true));
                    if (GUILayout.Button("Remove", GUILayout.Width(70)))
                        removeAt = i;
                }
            }

            if (removeAt >= 0)
                RemovePick(removeAt);
        }

        // ---------------------------------------------------------------------
        // Picks file IO (load-append-write, never clobber)
        // ---------------------------------------------------------------------
        private void LoadPicks()
        {
            _picks = new PickFile();
            try
            {
                if (File.Exists(PicksPath))
                {
                    string json = File.ReadAllText(PicksPath);
                    if (!string.IsNullOrEmpty(json))
                    {
                        var loaded = JsonUtility.FromJson<PickFile>(json);
                        if (loaded != null && loaded.picks != null)
                            _picks = loaded;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[VfxParade] failed to load picks file; starting fresh: " + e.Message);
                _picks = new PickFile();
            }
            if (_picks.picks == null) _picks.picks = new List<Pick>();
        }

        private void BookmarkCurrent()
        {
            if (_prefabPaths.Count == 0 || _index < 0 || _index >= _prefabPaths.Count)
            {
                Debug.LogWarning("[VfxParade] cannot bookmark: no current effect.");
                return;
            }

            // Re-load first so we never clobber picks another session/window appended.
            LoadPicks();

            string path = _prefabPaths[_index];
            string name = Path.GetFileNameWithoutExtension(path);
            string moment = Moments[Mathf.Clamp(_momentIndex, 0, Moments.Length - 1)];

            string note = _pendingNote == null ? "" : _pendingNote;
            _picks.picks.Add(new Pick { path = path, name = name, moment = moment, note = note });
            WritePicks();
            _pendingNote = ""; // clear, ready for the next bookmark
            Debug.Log("[VfxParade] bookmarked '" + name + "' as moment '" + moment + "' -> " + PicksPath);
        }

        private void RemovePick(int idx)
        {
            // Re-load so the remove operates on the on-disk truth, then rewrite.
            LoadPicks();
            if (idx < 0 || idx >= _picks.picks.Count)
            {
                Repaint();
                return;
            }
            string removed = _picks.picks[idx] != null ? _picks.picks[idx].name : "<null>";
            _picks.picks.RemoveAt(idx);
            WritePicks();
            Debug.Log("[VfxParade] removed pick '" + removed + "' from " + PicksPath);
            Repaint();
        }

        private void WritePicks()
        {
            try
            {
                string dir = Path.GetDirectoryName(PicksPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                string json = JsonUtility.ToJson(_picks, true);
                File.WriteAllText(PicksPath, json);
                AssetDatabase.Refresh();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[VfxParade] failed to write picks file '" + PicksPath + "': " + e.Message);
            }
        }
    }
}
