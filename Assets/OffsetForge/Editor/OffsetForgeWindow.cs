// =============================================================================
// OffsetForge.Editor.OffsetForgeWindow — generic model attachment-offset authoring tool.
// -----------------------------------------------------------------------------
// A self-contained EditorWindow (Tools > Offset Forge) for visually dialing in a
// model's rotation/position/scale offset and exporting it to JSON. 100% generic:
// NO game references, drops into any Unity project. Uses PreviewRenderUtility
// (editor-native offscreen renderer) for the 3D viewport.
//
// Viewport input over the preview rect: left-drag orbits the CAMERA, scroll zooms,
// middle-drag (or Alt+left-drag) pans. The camera orbit is SEPARATE from the
// authored model offset — orbiting never changes the model's rot/pos values.
//
// Compatible Unity 2021.3 LTS through Unity 6. ASCII-only in Debug.Log strings.
// =============================================================================

using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using OffsetForge;

namespace OffsetForge.Editor
{
    public sealed class OffsetForgeWindow : EditorWindow
    {
        // ---- Model + preview state ------------------------------------------
        private GameObject _sourceModel;       // the asset the user dropped in
        private GameObject _previewInstance;    // instantiated clone in the preview scene
        private PreviewRenderUtility _preview;  // editor-native offscreen renderer

        // ---- Authored offset (the values we export) -------------------------
        private Vector3 _rotation = Vector3.zero;   // euler degrees
        private Vector3 _position = Vector3.zero;   // local position
        private float _modelScale = 1f;

        // ---- Camera orbit state (separate from the model offset) ------------
        private float _camYaw = 30f;
        private float _camPitch = 20f;
        private float _camDistance = 5f;
        private Vector3 _camPivot = Vector3.zero;
        private bool _framed;

        // ---- Snap ------------------------------------------------------------
        private bool _snapEnabled;
        private int _snapIndex; // 0 -> 5, 1 -> 15
        private static readonly float[] SnapIncrements = { 5f, 15f };
        private static readonly string[] SnapLabels = { "5", "15" };

        // ---- Save target -----------------------------------------------------
        private string _savePath = "Assets/OffsetForge/offsets.json";
        private string _saveId = "";

        private Vector2 _scroll;

        [MenuItem("Tools/Offset Forge")]
        public static void Open()
        {
            var win = GetWindow<OffsetForgeWindow>("Offset Forge");
            win.minSize = new Vector2(420, 560);
            win.Show();
        }

        private void OnEnable()
        {
            EnsurePreviewUtility();
        }

        private void OnDisable()
        {
            CleanupPreview();
        }

        private void OnDestroy()
        {
            CleanupPreview();
        }

        // ---------------------------------------------------------------------
        // Preview lifecycle
        // ---------------------------------------------------------------------
        private void EnsurePreviewUtility()
        {
            if (_preview != null) return;
            _preview = new PreviewRenderUtility();
            // Neutral camera setup. Wide clip range so any model size frames fine.
            _preview.camera.clearFlags = CameraClearFlags.SolidColor;
            _preview.camera.backgroundColor = new Color(0.20f, 0.20f, 0.23f, 1f);
            _preview.camera.nearClipPlane = 0.01f;
            _preview.camera.farClipPlane = 1000f;
            _preview.camera.fieldOfView = 45f;

            // Key + fill light so the model is never black.
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
            _preview.ambientColor = new Color(0.35f, 0.35f, 0.38f, 1f);
        }

        private void CleanupPreview()
        {
            DestroyPreviewInstance();
            if (_preview != null)
            {
                try { _preview.Cleanup(); }
                catch (Exception e) { Debug.LogWarning("[OffsetForge] preview cleanup failed: " + e.Message); }
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
        }

        private void RebuildPreviewInstance()
        {
            DestroyPreviewInstance();
            EnsurePreviewUtility();
            if (_sourceModel == null) return;

            try
            {
                _previewInstance = (GameObject)UnityEngine.Object.Instantiate(_sourceModel);
                _previewInstance.hideFlags = HideFlags.HideAndDontSave;
                // PreviewRenderUtility owns an isolated scene; add the instance to it.
                _preview.AddSingleGO(_previewInstance);
                _framed = false; // re-frame on next repaint
            }
            catch (Exception e)
            {
                Debug.LogWarning("[OffsetForge] failed to instantiate model '" +
                                 (_sourceModel != null ? _sourceModel.name : "<null>") + "': " + e.Message);
                _previewInstance = null;
            }
        }

        // ---------------------------------------------------------------------
        // GUI
        // ---------------------------------------------------------------------
        private void OnGUI()
        {
            EnsurePreviewUtility();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            DrawModelField();
            EditorGUILayout.Space(4);
            DrawViewport();
            EditorGUILayout.Space(6);
            DrawControls();
            EditorGUILayout.Space(6);
            DrawReadout();
            EditorGUILayout.Space(6);
            DrawCopyButtons();
            EditorGUILayout.Space(6);
            DrawSavePanel();

            EditorGUILayout.EndScrollView();
        }

        private void DrawModelField()
        {
            EditorGUILayout.LabelField("Model", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            var newModel = (GameObject)EditorGUILayout.ObjectField(
                "Prefab / Model", _sourceModel, typeof(GameObject), false);
            if (EditorGUI.EndChangeCheck())
            {
                _sourceModel = newModel;
                if (_sourceModel != null && string.IsNullOrEmpty(_saveId))
                    _saveId = _sourceModel.name;
                RebuildPreviewInstance();
            }
            if (_sourceModel == null)
                EditorGUILayout.HelpBox("Drop in any prefab or model asset to begin.", MessageType.Info);
        }

        private void DrawViewport()
        {
            var rect = GUILayoutUtility.GetRect(10, 4000, 240, 240, GUILayout.ExpandWidth(true));
            HandleViewportInput(rect);

            if (Event.current.type != EventType.Repaint)
                return;

            if (_preview == null || _previewInstance == null)
            {
                EditorGUI.DrawRect(rect, new Color(0.16f, 0.16f, 0.18f));
                var prev = GUI.color;
                GUI.color = new Color(0.7f, 0.7f, 0.7f);
                GUI.Label(rect, "No model loaded", EditorStyles.centeredGreyMiniLabel);
                GUI.color = prev;
                return;
            }

            try
            {
                ApplyOffsetToInstance();
                if (!_framed) FrameCamera();

                PositionCamera();

                _preview.BeginPreview(rect, GUIStyle.none);
                _preview.Render(true, false);
                var tex = _preview.EndPreview();
                GUI.DrawTexture(rect, tex, ScaleMode.StretchToFill, false);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[OffsetForge] viewport render failed: " + e.Message);
                EditorGUI.DrawRect(rect, new Color(0.16f, 0.16f, 0.18f));
            }
        }

        private void HandleViewportInput(Rect rect)
        {
            var e = Event.current;
            if (!rect.Contains(e.mousePosition) && e.type != EventType.MouseDrag && e.type != EventType.MouseUp)
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
                    bool pan = e.button == 2 || (e.button == 0 && e.alt);
                    if (pan)
                    {
                        // Pan the pivot in camera-relative screen space.
                        float panScale = _camDistance * 0.0015f;
                        Quaternion camRot = Quaternion.Euler(_camPitch, _camYaw, 0f);
                        Vector3 right = camRot * Vector3.right;
                        Vector3 up = camRot * Vector3.up;
                        _camPivot += (-right * e.delta.x + up * e.delta.y) * panScale;
                        e.Use();
                        Repaint();
                    }
                    else if (e.button == 0)
                    {
                        // Orbit the camera (does NOT touch the authored model offset).
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
                float radius = Mathf.Max(0.1f, b.extents.magnitude);
                _camDistance = radius * 2.5f;
            }
            else
            {
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
            if (_previewInstance == null) return false;
            var rends = _previewInstance.GetComponentsInChildren<Renderer>();
            if (rends == null || rends.Length == 0) return false;
            bool has = false;
            for (int i = 0; i < rends.Length; i++)
            {
                if (rends[i] == null) continue;
                if (!has) { bounds = rends[i].bounds; has = true; }
                else bounds.Encapsulate(rends[i].bounds);
            }
            return has;
        }

        private void ApplyOffsetToInstance()
        {
            if (_previewInstance == null) return;
            _previewInstance.transform.localRotation = Quaternion.Euler(_rotation);
            _previewInstance.transform.localPosition = _position;
            float s = _modelScale <= 0f ? 1f : _modelScale;
            _previewInstance.transform.localScale = Vector3.one * s;
        }

        private void DrawControls()
        {
            EditorGUILayout.LabelField("Offset", EditorStyles.boldLabel);

            // Snap row.
            using (new EditorGUILayout.HorizontalScope())
            {
                _snapEnabled = EditorGUILayout.ToggleLeft("Snap rotation", _snapEnabled, GUILayout.Width(110));
                using (new EditorGUI.DisabledScope(!_snapEnabled))
                {
                    _snapIndex = EditorGUILayout.Popup(_snapIndex, SnapLabels, GUILayout.Width(60));
                    EditorGUILayout.LabelField("deg", GUILayout.Width(30));
                }
            }

            // Rotation sliders (-180..180), snapped if enabled.
            EditorGUILayout.LabelField("Rotation (euler)");
            _rotation.x = RotationSlider("X", _rotation.x);
            _rotation.y = RotationSlider("Y", _rotation.y);
            _rotation.z = RotationSlider("Z", _rotation.z);

            EditorGUILayout.Space(2);

            // Position float fields.
            EditorGUILayout.LabelField("Position");
            _position = EditorGUILayout.Vector3Field(GUIContent.none, _position);

            EditorGUILayout.Space(2);

            // Uniform scale.
            _modelScale = EditorGUILayout.FloatField("Scale (uniform)", _modelScale);
            if (_modelScale <= 0f) _modelScale = 1f;

            // Reset.
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Reset Offset", GUILayout.Width(120)))
                {
                    _rotation = Vector3.zero;
                    _position = Vector3.zero;
                    _modelScale = 1f;
                    Repaint();
                }
                if (GUILayout.Button("Frame Camera", GUILayout.Width(120)))
                {
                    _framed = false;
                    Repaint();
                }
            }
        }

        private float RotationSlider(string label, float value)
        {
            EditorGUI.BeginChangeCheck();
            float v = EditorGUILayout.Slider(label, value, -180f, 180f);
            if (EditorGUI.EndChangeCheck())
            {
                if (_snapEnabled)
                {
                    float inc = SnapIncrements[Mathf.Clamp(_snapIndex, 0, SnapIncrements.Length - 1)];
                    v = Mathf.Round(v / inc) * inc;
                }
                Repaint();
            }
            return v;
        }

        private void DrawReadout()
        {
            EditorGUILayout.LabelField("Readout", EditorStyles.boldLabel);
            Vector3 euler = Quaternion.Euler(_rotation).eulerAngles; // normalized 0..360
            string rotStr = string.Format("eulerAngles  ({0:0.00}, {1:0.00}, {2:0.00})", euler.x, euler.y, euler.z);
            string posStr = string.Format("localPosition  ({0:0.00}, {1:0.00}, {2:0.00})", _position.x, _position.y, _position.z);
            EditorGUILayout.SelectableLabel(rotStr, EditorStyles.textField, GUILayout.Height(18));
            EditorGUILayout.SelectableLabel(posStr, EditorStyles.textField, GUILayout.Height(18));
        }

        private void DrawCopyButtons()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Copy Rotation"))
                {
                    EditorGUIUtility.systemCopyBuffer = string.Format(
                        "new Vector3({0}f, {1}f, {2}f)", F(_rotation.x), F(_rotation.y), F(_rotation.z));
                    Debug.Log("[OffsetForge] copied rotation: " + EditorGUIUtility.systemCopyBuffer);
                }
                if (GUILayout.Button("Copy Position"))
                {
                    EditorGUIUtility.systemCopyBuffer = string.Format(
                        "new Vector3({0}f, {1}f, {2}f)", F(_position.x), F(_position.y), F(_position.z));
                    Debug.Log("[OffsetForge] copied position: " + EditorGUIUtility.systemCopyBuffer);
                }
                if (GUILayout.Button("Copy as Quaternion.Euler"))
                {
                    EditorGUIUtility.systemCopyBuffer = string.Format(
                        "Quaternion.Euler({0}f, {1}f, {2}f)", F(_rotation.x), F(_rotation.y), F(_rotation.z));
                    Debug.Log("[OffsetForge] copied quaternion: " + EditorGUIUtility.systemCopyBuffer);
                }
            }
        }

        private static string F(float v)
        {
            // Invariant-culture, trimmed numeric for clean code strings.
            return v.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture);
        }

        private void DrawSavePanel()
        {
            EditorGUILayout.LabelField("Save", EditorStyles.boldLabel);
            _saveId = EditorGUILayout.TextField("Id (key)", _saveId);
            _savePath = EditorGUILayout.TextField("JSON path", _savePath);

            using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(_saveId)))
            {
                if (GUILayout.Button("Save to JSON"))
                    SaveToJson();
            }
        }

        private void SaveToJson()
        {
            if (string.IsNullOrEmpty(_saveId))
            {
                Debug.LogWarning("[OffsetForge] cannot save: id is empty.");
                return;
            }
            if (string.IsNullOrEmpty(_savePath))
            {
                Debug.LogWarning("[OffsetForge] cannot save: path is empty.");
                return;
            }

            try
            {
                // Load existing table (if present) so we append/update rather than overwrite.
                OffsetTable table;
                if (File.Exists(_savePath))
                    table = OffsetTableIO.Load(File.ReadAllText(_savePath));
                else
                    table = new OffsetTable();

                var entry = new OffsetEntry
                {
                    id = _saveId,
                    rot = new Vec3(_rotation.x, _rotation.y, _rotation.z),
                    pos = new Vec3(_position.x, _position.y, _position.z),
                    scale = _modelScale <= 0f ? 1f : _modelScale
                };
                table.Upsert(entry);

                string dir = Path.GetDirectoryName(_savePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                File.WriteAllText(_savePath, OffsetTableIO.ToJson(table));
                AssetDatabase.Refresh();
                Debug.Log("[OffsetForge] saved offset id='" + _saveId + "' to " + _savePath);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[OffsetForge] save failed: " + e.Message);
            }
        }
    }
}
