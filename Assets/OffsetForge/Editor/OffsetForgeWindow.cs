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
        // View-only turntable for the hero context — lets the owner spin the hero to inspect the
        // weapon mapping from any side WITHOUT changing the saved weapon offset (local transform).
        private float _contextYaw;

        // Auto-fit (owner request 2026-06-29): the GAME auto-sizes a weapon to a target length when
        // it equips (NormalizeInto), so a tiny raw model that needed ~16x by hand should instead
        // load pre-fit at Scale 1. We replicate that generically: measure the model's longest-axis
        // bounds and scale it to _autoFitTarget metres. The saved "Scale" then rides on top as a
        // clean multiplier (1 = the fit) — matching the runtime's scale-as-multiplier semantics.
        private bool  _autoFit = true;
        private float _autoFitTarget = 0.9f;   // target longest-axis length (m) at Scale 1
        private float _autoFitScale = 1f;      // computed from the model's native bounds on load
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

        // ---- Grip context (reference hand/hero for positional context) -------
        private GameObject _contextModel;       // the reference hand/hero asset dropped in
        private GameObject _contextInstance;    // instantiated clone in the preview scene
        private bool _showContext;              // checkbox: show the hand/grip context
        private string _gripBoneName = "R_Hand"; // manual-override bone name (non-humanoid rigs only)
        private bool _useLeftHand;              // false = RightHand (weapons), true = LeftHand (shields)
        private Transform _gripAnchor;          // resolved grip bone on context (null => context root)

        private const string PrefShowContext = "OffsetForge.ShowContext";
        private const string PrefGripBone = "OffsetForge.GripBone";
        private const string PrefUseLeftHand = "OffsetForge.UseLeftHand";

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
            _showContext = EditorPrefs.GetBool(PrefShowContext, false);
            _gripBoneName = EditorPrefs.GetString(PrefGripBone, "R_Hand");
            _useLeftHand = EditorPrefs.GetBool(PrefUseLeftHand, false);
            EnsurePreviewUtility();
        }

        private void OnDisable()
        {
            EditorPrefs.SetBool(PrefShowContext, _showContext);
            EditorPrefs.SetString(PrefGripBone, _gripBoneName);
            EditorPrefs.SetBool(PrefUseLeftHand, _useLeftHand);
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
            DestroyContextInstance();
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
                // If context is active, seat the weapon under the resolved grip bone so
                // the authored offset reads as the weapon's LOCAL transform in the hand.
                ParentPreviewToContext();
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
        // Grip context lifecycle
        // ---------------------------------------------------------------------
        private void DestroyContextInstance()
        {
            if (_contextInstance != null)
            {
                DestroyImmediate(_contextInstance);
                _contextInstance = null;
            }
            _gripAnchor = null;
        }

        private void RebuildContextInstance()
        {
            // Re-parent the weapon to the preview-scene root first so destroying the
            // old context never takes the weapon (a child) down with it.
            if (_previewInstance != null)
                _previewInstance.transform.SetParent(null, false);

            DestroyContextInstance();
            EnsurePreviewUtility();

            if (_showContext && _contextModel != null)
            {
                try
                {
                    _contextInstance = (GameObject)UnityEngine.Object.Instantiate(_contextModel);
                    _contextInstance.hideFlags = HideFlags.HideAndDontSave;
                    _contextInstance.transform.position = Vector3.zero;
                    _contextInstance.transform.rotation = Quaternion.identity;
                    _contextInstance.transform.localScale = Vector3.one;
                    _preview.AddSingleGO(_contextInstance);
                    _gripAnchor = ResolveGripAnchor(_contextInstance);
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[OffsetForge] failed to instantiate context '" +
                                     (_contextModel != null ? _contextModel.name : "<null>") + "': " + e.Message);
                    _contextInstance = null;
                    _gripAnchor = null;
                }
            }

            // Re-seat the weapon now that context (and its anchor) is rebuilt.
            ParentPreviewToContext();
            _framed = false;
        }

        private Transform ResolveGripAnchor(GameObject contextRoot)
        {
            if (contextRoot == null) return null;

            // (a) Authoritative path: mirror the GAME, which attaches via the humanoid
            //     AVATAR (animator.GetBoneTransform(HumanBodyBones.Right/LeftHand)) rather
            //     than a literal bone name. Works on ANY rig (Mixamo/Tripo etc.).
            var animator = contextRoot.GetComponentInChildren<Animator>();
            if (animator != null && animator.isHuman)
            {
                HumanBodyBones bone = _useLeftHand ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand;
                Transform handBone = animator.GetBoneTransform(bone);
                if (handBone != null)
                {
                    Debug.Log("[OffsetForge] grip anchor via humanoid avatar (" +
                              (_useLeftHand ? "LeftHand" : "RightHand") + "): " + handBone.name);
                    return handBone;
                }
            }

            var xforms = contextRoot.GetComponentsInChildren<Transform>(true);

            // (b) Manual override: exact name match (case-insensitive) on the grip bone.
            for (int i = 0; i < xforms.Length; i++)
            {
                if (xforms[i] != null &&
                    string.Equals(xforms[i].name, _gripBoneName, StringComparison.OrdinalIgnoreCase))
                {
                    Debug.Log("[OffsetForge] grip anchor via name match: " + xforms[i].name);
                    return xforms[i];
                }
            }

            // (c) Fallback: any transform whose name contains "hand".
            for (int i = 0; i < xforms.Length; i++)
            {
                if (xforms[i] != null &&
                    xforms[i].name.IndexOf("hand", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    Debug.Log("[OffsetForge] grip anchor via 'hand' fallback: " + xforms[i].name);
                    return xforms[i];
                }
            }

            // (d) Last resort: context root.
            Debug.LogWarning("[OffsetForge] grip bone '" + _gripBoneName +
                             "' not found on context '" + contextRoot.name +
                             "'; parented weapon at the context root instead.");
            return contextRoot.transform;
        }

        // Parent the weapon under the grip anchor when context is active, or detach it
        // back to the preview-scene root (floating, world-relative) when context is off.
        private void ParentPreviewToContext()
        {
            if (_previewInstance == null) return;
            bool contextActive = _showContext && _contextInstance != null && _gripAnchor != null;
            if (contextActive)
                _previewInstance.transform.SetParent(_gripAnchor, false);
            else
                _previewInstance.transform.SetParent(null, false);
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
            DrawGripContext();
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
                // Sync the save KEY to the loaded model EVERY time (owner bug 2026-06-29: "all id's
                // resolve to sword_a"). Previously this only set the id when empty, so the first model
                // loaded ('_tripobak_sword_A') stuck and every later sword would overwrite that entry.
                // The id field stays editable for a custom key AFTER loading.
                if (_sourceModel != null)
                    _saveId = _sourceModel.name;
                // Owner spec 2026-06-29 ("on drag onto, on the hover/drop event clear the cell first,
                // then drop"): the drop must NOT inherit the previous model's pose (the carry-over bug
                // that gave sword_G sword_F's rotation). So CLEAR the offset cell on every model change,
                // then RELOAD the new id's own saved entry from disk (true round-trip; stays cleared if
                // no entry exists yet).
                ClearOffsetCell();
                LoadSavedOffsetForCurrentId();
                RecomputeAutoFit();
                RebuildPreviewInstance();
            }
            if (_sourceModel == null)
                EditorGUILayout.HelpBox("Drop in any prefab or model asset to begin.", MessageType.Info);
        }

        private void DrawGripContext()
        {
            EditorGUILayout.LabelField("Grip Context", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();

            var newContext = (GameObject)EditorGUILayout.ObjectField(
                "Hand / Hero", _contextModel, typeof(GameObject), false);
            bool newShow = EditorGUILayout.ToggleLeft("Show hand / grip context", _showContext);

            // Hand-side selector. On a humanoid rig this picks Right vs Left HAND bone
            // through the avatar (matches the game); Left = shields.
            int sideIndex = _useLeftHand ? 1 : 0;
            int newSideIndex = GUILayout.Toolbar(sideIndex, new[] { "Right hand", "Left hand (shields)" });
            bool newUseLeftHand = newSideIndex == 1;

            string newBone = EditorGUILayout.TextField("Grip bone (manual)", _gripBoneName);

            if (EditorGUI.EndChangeCheck())
            {
                _contextModel = newContext;
                _showContext = newShow;
                _useLeftHand = newUseLeftHand;
                _gripBoneName = string.IsNullOrEmpty(newBone) ? "R_Hand" : newBone;
                RebuildContextInstance();
                Repaint();
            }

            EditorGUILayout.HelpBox(
                "Drop a hand/hero model and toggle it on; on a humanoid rig the weapon seats " +
                "in the correct hand automatically via the avatar (matches the game), no typing " +
                "needed. Right/Left picks the hand bone (Left = shields). 'Grip bone (manual)' " +
                "is only used as a fallback when the rig is NOT humanoid.", MessageType.None);

            if (GUILayout.Button("Load Hero", GUILayout.Width(120)))
                TryLoadHero();

            // View controls — keep the WEAPON the framed subject and let the owner spin the hero
            // to inspect the mapping from any angle (view-only; the saved offset is untouched).
            if (GUILayout.Button("Frame Weapon", GUILayout.Width(120)))
            {
                _framed = false;
                Repaint();
            }
            EditorGUI.BeginChangeCheck();
            float newYaw = EditorGUILayout.Slider("Rotate hero (view)", _contextYaw, -180f, 180f);
            if (EditorGUI.EndChangeCheck())
            {
                _contextYaw = newYaw;
                _framed = false;   // re-center on the hand as the hero turns
                Repaint();
            }
            EditorGUILayout.HelpBox(
                "Left-drag = orbit the camera · scroll = zoom · middle/alt-drag = pan. " +
                "'Frame Weapon' re-centers on the blade; 'Rotate hero' spins the body to view the " +
                "grip from any side. Both are VIEW-only — they never change the saved offset.",
                MessageType.None);
        }

        private void TryLoadHero()
        {
            string[] roots = { "Assets/Resources/Heroes" };
            string[] guids = AssetDatabase.FindAssets("t:GameObject", roots);
            if (guids == null || guids.Length == 0)
            {
                Debug.LogWarning("[OffsetForge] no GameObject assets found under Assets/Resources/Heroes.");
                return;
            }

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrEmpty(path) || !path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                    continue;
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go == null) continue;
                if (HasTransformNamed(go, "R_Hand"))
                {
                    _contextModel = go;
                    _showContext = true;
                    RebuildContextInstance();
                    Repaint();
                    Debug.Log("[OffsetForge] loaded hero context: " + path);
                    return;
                }
            }

            Debug.LogWarning("[OffsetForge] no hero prefab with an 'R_Hand' transform found under Assets/Resources/Heroes.");
        }

        private static bool HasTransformNamed(GameObject root, string boneName)
        {
            if (root == null) return false;
            var xforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < xforms.Length; i++)
            {
                if (xforms[i] != null &&
                    string.Equals(xforms[i].name, boneName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
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
                // View-only hero turntable (does NOT touch the weapon's saved local offset).
                if (_showContext && _contextInstance != null)
                    _contextInstance.transform.rotation = Quaternion.Euler(0f, _contextYaw, 0f);
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
            bool has = false;
            EncapsulateRenderers(_previewInstance, ref bounds, ref has);
            // When context is shown, the WEAPON stays the subject. Framing the whole hero makes the
            // body dominate and shrinks the weapon to a speck (owner bug 2026-06-29: "hero becomes
            // dominant item not sword"). Frame the weapon + just the grip-anchor point (the hand);
            // the hero is still visible context you orbit around / spin with "Rotate hero".
            if (_showContext && _gripAnchor != null)
            {
                if (!has) { bounds = new Bounds(_gripAnchor.position, Vector3.zero); has = true; }
                else bounds.Encapsulate(_gripAnchor.position);
            }
            return has;
        }

        private static void EncapsulateRenderers(GameObject go, ref Bounds bounds, ref bool has)
        {
            if (go == null) return;
            var rends = go.GetComponentsInChildren<Renderer>();
            if (rends == null) return;
            for (int i = 0; i < rends.Length; i++)
            {
                if (rends[i] == null) continue;
                if (!has) { bounds = rends[i].bounds; has = true; }
                else bounds.Encapsulate(rends[i].bounds);
            }
        }

        private void ApplyOffsetToInstance()
        {
            if (_previewInstance == null) return;
            _previewInstance.transform.localRotation = Quaternion.Euler(_rotation);
            _previewInstance.transform.localPosition = _position;
            float mult = _modelScale <= 0f ? 1f : _modelScale;
            // Auto-fit (owner request 2026-06-29): the saved "Scale" is a MULTIPLIER on the auto-sized
            // weapon, exactly as the runtime treats fo.scale on top of NormalizeInto. So the preview's
            // true scale = native auto-fit * the multiplier. This makes a tiny raw model show correctly
            // at Scale 1 (no more "needs 16x by hand") and matches what the game will render.
            float fit = (_autoFit && _autoFitScale > 0f) ? _autoFitScale : 1f;
            _previewInstance.transform.localScale = Vector3.one * (fit * mult);
        }

        /// <summary>
        /// Clear the offset CELL to the neutral pose (owner: "on drag onto ... clear cell()") so a
        /// freshly dropped model never inherits the previous model's rotation/position/scale.
        /// </summary>
        private void ClearOffsetCell()
        {
            _rotation = Vector3.zero;
            _position = Vector3.zero;
            _modelScale = 1f;
        }

        /// <summary>
        /// Round-trip: after a model is dropped, load that id's OWN saved entry (if one exists on disk)
        /// into the sliders so re-opening a known weapon shows its real pose instead of a blank cell.
        /// </summary>
        private void LoadSavedOffsetForCurrentId()
        {
            if (string.IsNullOrEmpty(_saveId) || string.IsNullOrEmpty(_savePath) || !File.Exists(_savePath))
                return;
            try
            {
                var table = OffsetTableIO.Load(File.ReadAllText(_savePath));
                var e = table != null ? table.Find(_saveId) : null;
                if (e == null) return;
                _rotation = new Vector3(e.rot.x, e.rot.y, e.rot.z);
                _position = new Vector3(e.pos.x, e.pos.y, e.pos.z);
                _modelScale = e.scale > 0f ? e.scale : 1f;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[OffsetForge] load-existing offset failed for '" + _saveId + "': " + ex.Message);
            }
        }

        /// <summary>
        /// Measure the dropped model's native longest-axis length and compute the scale that fits it to
        /// _autoFitTarget metres — the editor analogue of the runtime's NormalizeInto auto-size. Lets the
        /// owner work at Scale 1 instead of guessing a raw multiplier (e.g. the "scale 16" trap).
        /// </summary>
        private void RecomputeAutoFit()
        {
            _autoFitScale = 1f;
            if (_sourceModel == null) return;
            try
            {
                // Measure at identity scale: instantiate-free bounds from the asset's renderers is not
                // reliable for prefabs, so measure the live preview clone after it rebuilds. Here we
                // estimate from the source asset's mesh bounds at unit scale.
                var bounds = new Bounds(Vector3.zero, Vector3.zero);
                bool has = false;
                var filters = _sourceModel.GetComponentsInChildren<MeshFilter>();
                for (int i = 0; i < filters.Length; i++)
                {
                    if (filters[i] == null || filters[i].sharedMesh == null) continue;
                    var mb = filters[i].sharedMesh.bounds;
                    if (!has) { bounds = mb; has = true; } else bounds.Encapsulate(mb);
                }
                var skins = _sourceModel.GetComponentsInChildren<SkinnedMeshRenderer>();
                for (int i = 0; i < skins.Length; i++)
                {
                    if (skins[i] == null || skins[i].sharedMesh == null) continue;
                    var mb = skins[i].sharedMesh.bounds;
                    if (!has) { bounds = mb; has = true; } else bounds.Encapsulate(mb);
                }
                if (!has) return;
                float longest = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
                if (longest > 1e-4f && _autoFitTarget > 0f)
                    _autoFitScale = _autoFitTarget / longest;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[OffsetForge] auto-fit measure failed: " + ex.Message);
                _autoFitScale = 1f;
            }
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

            // Position sliders (metres) — parity with the rotation rows (owner request 2026-06-29).
            EditorGUILayout.LabelField("Position (metres)");
            _position.x = PositionSlider("X", _position.x);
            _position.y = PositionSlider("Y", _position.y);
            _position.z = PositionSlider("Z", _position.z);

            EditorGUILayout.Space(2);

            // Uniform scale (a MULTIPLIER on the auto-fit; 1 = the fitted size).
            _modelScale = EditorGUILayout.FloatField("Scale (x auto-fit)", _modelScale);
            if (_modelScale <= 0f) _modelScale = 1f;

            // Auto-fit row (owner request 2026-06-29): replicate the runtime NormalizeInto auto-size so
            // the editor is WYSIWYG and the owner authors at Scale 1 instead of the "scale 16" trap.
            using (new EditorGUILayout.HorizontalScope())
            {
                bool prevFit = _autoFit;
                _autoFit = EditorGUILayout.ToggleLeft("Auto-fit length", _autoFit, GUILayout.Width(120));
                using (new EditorGUI.DisabledScope(!_autoFit))
                {
                    EditorGUILayout.LabelField("target m", GUILayout.Width(56));
                    float t = EditorGUILayout.FloatField(_autoFitTarget, GUILayout.Width(50));
                    if (t > 0f) _autoFitTarget = t;
                }
                EditorGUILayout.LabelField("fit x" + _autoFitScale.ToString("0.00"), GUILayout.Width(70));
                if (prevFit != _autoFit || GUI.changed) RecomputeAutoFit();
            }

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

        // Position slider in metres — fine grip-nudge range (the editable value box on the right
        // still accepts exact typed values). Parity with RotationSlider per owner request.
        private float PositionSlider(string label, float value)
        {
            EditorGUI.BeginChangeCheck();
            float v = EditorGUILayout.Slider(label, value, -0.5f, 0.5f);
            if (EditorGUI.EndChangeCheck()) Repaint();
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
