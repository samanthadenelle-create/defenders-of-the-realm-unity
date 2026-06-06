// =============================================================================
// BuildPreviewModal — small clean modal preview + rotation chooser for Build Mode UX.
// -----------------------------------------------------------------------------
// Shows the armed CatalogEntry's visual on a neutral plane (flat gray quad +
// neutral lights) in an isolated RenderTexture-backed RawImage. Supports 90°
// buttons and free drag-to-rotate on the preview area. On Confirm, invokes
// callback with the final yaw offset (degrees); caller saves to placement data
// and places with it. Self-contained, code-built UI (no UXML per project rules),
// low-res RT for mobile perf, destroyed on close.
//
// Used by BuildModeController on place confirm to give "wow factor" rotation
// choice before commit.
// =============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DeNelle.Core.Catalog;

namespace DeNelle.Village
{
    /// <summary>
    /// Modal for previewing and rotating a build object before placement.
    /// Create via new GameObject().AddComponent<BuildPreviewModal>().Show(...);
    /// </summary>
    public sealed class BuildPreviewModal : MonoBehaviour
    {
        private Action<float> _onConfirm;
        private Action _onCancel;
        private CatalogEntry _entry;
        private GameObject _previewRoot;
        private GameObject _previewVisual;
        private Camera _previewCam;
        private RenderTexture _rt;
        private Canvas _canvas;
        private RawImage _previewImage;
        private float _currentYaw;
        private bool _dragging;
        private Vector2 _lastDragPos;
        private Text _yawReadout; // live "Yaw: XX°" for premium viewer feel

        private const int RT_SIZE = 384; // larger for proper 3D model viewer experience (still mobile friendly)
        private const string PLANE_NAME = "PreviewPlane";
        private const string VISUAL_NAME = "PreviewVisual";
        // Dedicated layer the whole preview rig lives on so the preview camera + lights are
        // masked to ONLY these objects — they never render into (or light) the live scene.
        private const int PREVIEW_LAYER = 31;

        public void Show(CatalogEntry entry, Action<float> onConfirm, Action onCancel = null)
        {
            _entry = entry;
            _onConfirm = onConfirm;
            _onCancel = onCancel;

            // Seed from per-type saved correction (if any). This is the core of the
            // "always appears correctly oriented" feature: first time a structure type
            // (wall, bridge, building, etc.) is armed, yaw=0 or model default; after the
            // player confirms a natural orientation in the viewer, all future opens of
            // the modal for that CatalogEntry.id start already rotated correctly.
            _currentYaw = RotationCorrectionRegistry.GetYawOffset(entry != null ? entry.id : null);

            SetupUI();
            SetupPreview3D();

            gameObject.SetActive(true);
        }

        private void SetupUI()
        {
            // Self contained screen-space overlay canvas for the modal.
            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 100; // on top
            gameObject.AddComponent<CanvasScaler>();
            gameObject.AddComponent<GraphicRaycaster>();

            // Semi-transparent panel (modal background) — slightly larger for premium viewer breathing room.
            var panel = new GameObject("ModalPanel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(_canvas.transform, false);
            var panelRT = panel.GetComponent<RectTransform>();
            panelRT.anchorMin = new Vector2(0.5f, 0.5f);
            panelRT.anchorMax = new Vector2(0.5f, 0.5f);
            panelRT.sizeDelta = new Vector2(360, 440);
            panelRT.anchoredPosition = Vector2.zero;
            var panelImg = panel.GetComponent<Image>();
            panelImg.color = new Color(0.12f, 0.10f, 0.08f, 0.96f); // earthy dark (matches Village theme)

            // Title with friendly display name when available (premium touch).
            var titleGO = new GameObject("Title", typeof(RectTransform), typeof(Text));
            titleGO.transform.SetParent(panel.transform, false);
            var titleRT = titleGO.GetComponent<RectTransform>();
            titleRT.anchorMin = new Vector2(0, 1);
            titleRT.anchorMax = new Vector2(1, 1);
            titleRT.sizeDelta = new Vector2(0, 32);
            titleRT.anchoredPosition = new Vector2(0, -18);
            var title = titleGO.GetComponent<Text>();
            string display = (_entry != null && !string.IsNullOrEmpty(_entry.displayName)) ? _entry.displayName : (_entry != null ? _entry.id : "Object");
            title.text = "Preview & Orient — " + display;
            title.alignment = TextAnchor.MiddleCenter;
            title.fontSize = 15;
            title.color = new Color(0.95f, 0.88f, 0.65f); // warm parchment/gold

            // RawImage for 3D preview RT (bigger = proper model viewer)
            var imgGO = new GameObject("PreviewImage", typeof(RectTransform), typeof(RawImage));
            imgGO.transform.SetParent(panel.transform, false);
            var imgRT = imgGO.GetComponent<RectTransform>();
            imgRT.anchorMin = new Vector2(0.08f, 0.28f);
            imgRT.anchorMax = new Vector2(0.92f, 0.82f);
            imgRT.sizeDelta = Vector2.zero;
            _previewImage = imgGO.GetComponent<RawImage>();
            _previewImage.color = Color.white;

            // Add drag handler for free rotate on the image area.
            var dragHandler = imgGO.AddComponent<PreviewDragHandler>();
            dragHandler.modal = this;

            // Live yaw readout (premium intuitive feedback — player always sees the exact offset being chosen).
            var yawGO = new GameObject("YawReadout", typeof(RectTransform), typeof(Text));
            yawGO.transform.SetParent(panel.transform, false);
            var yawRT = yawGO.GetComponent<RectTransform>();
            yawRT.anchorMin = new Vector2(0, 0.82f);
            yawRT.anchorMax = new Vector2(1, 0.88f);
            yawRT.sizeDelta = Vector2.zero;
            yawRT.anchoredPosition = new Vector2(0, -4);
            _yawReadout = yawGO.GetComponent<Text>();
            _yawReadout.alignment = TextAnchor.MiddleCenter;
            _yawReadout.fontSize = 13;
            _yawReadout.color = new Color(0.85f, 0.85f, 0.9f);

            // Buttons row — larger thumb-friendly targets for mobile, earthy theme.
            CreateButton(panel.transform, "–90°", new Vector2(-85, -38), () => RotatePreview(-90));
            CreateButton(panel.transform, "+90°", new Vector2(0, -38), () => RotatePreview(90));
            CreateButton(panel.transform, "Reset", new Vector2(85, -38), ResetToSaved);
            CreateButton(panel.transform, "Confirm", new Vector2(-85, -78), Confirm);
            CreateButton(panel.transform, "Cancel", new Vector2(85, -78), Cancel);

            // Instruction text — explicitly communicates the "save as default for type" behavior.
            var instrGO = new GameObject("Instr", typeof(RectTransform), typeof(Text));
            instrGO.transform.SetParent(panel.transform, false);
            var instrRT = instrGO.GetComponent<RectTransform>();
            instrRT.anchorMin = new Vector2(0, 0);
            instrRT.anchorMax = new Vector2(1, 0);
            instrRT.sizeDelta = new Vector2(0, 22);
            instrRT.anchoredPosition = new Vector2(0, 12);
            var instr = instrGO.GetComponent<Text>();
            instr.text = "Drag the preview or use snaps to rotate until it sits naturally. This orientation is remembered for this structure type.";
            instr.alignment = TextAnchor.MiddleCenter;
            instr.fontSize = 10;
            instr.color = new Color(0.75f, 0.72f, 0.65f);
        }

        private void CreateButton(Transform parent, string label, Vector2 anchored, Action onClick)
        {
            var btnGO = new GameObject(label, typeof(RectTransform), typeof(Button), typeof(Image), typeof(Text));
            btnGO.transform.SetParent(parent, false);
            var btnRT = btnGO.GetComponent<RectTransform>();
            btnRT.anchorMin = new Vector2(0.5f, 0);
            btnRT.anchorMax = new Vector2(0.5f, 0);
            btnRT.sizeDelta = new Vector2(78, 32); // compact but still thumb-reachable with 5 buttons row
            btnRT.anchoredPosition = anchored;

            var img = btnGO.GetComponent<Image>();
            img.color = new Color(0.25f, 0.22f, 0.18f, 1f); // earthy button bg

            var txt = btnGO.GetComponentInChildren<Text>();
            txt.text = label;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.fontSize = 12;
            txt.color = new Color(0.95f, 0.9f, 0.75f);

            var btn = btnGO.GetComponent<Button>();
            btn.onClick.AddListener(() => onClick());
        }

        private void SetupPreview3D()
        {
            // RT for the RawImage.
            _rt = new RenderTexture(RT_SIZE, RT_SIZE, 16, RenderTextureFormat.ARGB32);
            _rt.Create();
            _previewImage.texture = _rt;

            // Preview root — ISOLATED far below the play area so its plane/disc/lights never
            // sit in the live scene (it was at world origin on the default layer → the green
            // preview disc rendered in-world as the "green circle" near the hero). Combined
            // with the PREVIEW_LAYER mask on the camera + lights below, the rig is fully
            // sealed off from the village.
            _previewRoot = new GameObject("BuildPreviewRoot");
            _previewRoot.transform.position = new Vector3(0f, -5000f, 0f);

            // Neutral plane (flat gray).
            var plane = GameObject.CreatePrimitive(PrimitiveType.Quad);
            plane.name = PLANE_NAME;
            plane.transform.SetParent(_previewRoot.transform, false);
            plane.transform.localScale = new Vector3(4f, 4f, 1f); // neutral size
            plane.transform.localPosition = Vector3.zero;
            plane.transform.localRotation = Quaternion.Euler(90, 0, 0); // flat on XZ
            var planeR = plane.GetComponent<Renderer>();
            if (planeR != null)
            {
                var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color"));
                mat.color = new Color(0.4f, 0.4f, 0.42f, 1f); // neutral gray
                planeR.sharedMaterial = mat;
            }
            var planeCol = plane.GetComponent<Collider>();
            if (planeCol != null) Destroy(planeCol);

            // Neutral lighting (soft, no harsh shadows for clean preview).
            var light1 = new GameObject("PreviewLight1").AddComponent<Light>();
            light1.transform.SetParent(_previewRoot.transform, false);
            light1.transform.localPosition = new Vector3(2, 3, -2);
            light1.type = LightType.Directional;
            light1.color = new Color(0.9f, 0.9f, 0.95f);
            light1.intensity = 0.8f;

            var light2 = new GameObject("PreviewLight2").AddComponent<Light>();
            light2.transform.SetParent(_previewRoot.transform, false);
            light2.transform.localPosition = new Vector3(-2, 2, 2);
            light2.type = LightType.Directional;
            light2.color = new Color(0.6f, 0.65f, 0.7f);
            light2.intensity = 0.5f;

            // Instantiate the visual (reuse ghost logic for fidelity: visualPrefabPath or fallback disc).
            _previewVisual = new GameObject(VISUAL_NAME);
            _previewVisual.transform.SetParent(_previewRoot.transform, false);
            _previewVisual.transform.localPosition = new Vector3(0, 0.5f, 0); // above plane

            GameObject skinned = null;
            if (_entry != null && !string.IsNullOrEmpty(_entry.visualPrefabPath))
            {
                skinned = VisualFactory.Skin(_previewVisual.transform, _entry.visualPrefabPath, SkinOptions.Prop(2.5f));
            }
            if (skinned == null)
            {
                // Fallback neutral marker (scaled to typical footprint).
                var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                disc.transform.SetParent(_previewVisual.transform, false);
                disc.transform.localScale = new Vector3(2f, 0.1f, 2f);
                var c = disc.GetComponent<Collider>();
                if (c != null) Destroy(c);
                var r = disc.GetComponent<Renderer>();
                if (r != null) r.sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color")) { color = new Color(0.3f, 0.5f, 0.3f) };
            }

            // Preview camera (orthographic for clean object view, or perspective).
            var camGO = new GameObject("PreviewCam");
            camGO.transform.SetParent(_previewRoot.transform, false);
            _previewCam = camGO.AddComponent<Camera>();
            _previewCam.clearFlags = CameraClearFlags.SolidColor;
            _previewCam.backgroundColor = new Color(0.15f, 0.15f, 0.18f);
            _previewCam.orthographic = true;
            _previewCam.orthographicSize = 2.2f;
            _previewCam.nearClipPlane = 0.1f;
            _previewCam.farClipPlane = 20f;
            _previewCam.targetTexture = _rt;
            // Position for 3/4 view of object on plane.
            camGO.transform.localPosition = new Vector3(3f, 3f, -3f);
            camGO.transform.LookAt(_previewVisual.transform.position + Vector3.up * 0.5f);

            // ISOLATE: put the whole rig on PREVIEW_LAYER and mask the camera + lights to it,
            // so nothing here renders into — or lights — the live village scene.
            SetLayerRecursive(_previewRoot.transform, PREVIEW_LAYER);
            _previewCam.cullingMask = 1 << PREVIEW_LAYER;
            light1.cullingMask = 1 << PREVIEW_LAYER;
            light2.cullingMask = 1 << PREVIEW_LAYER;

            // Initial rotation on visual.
            if (_previewVisual != null)
                _previewVisual.transform.localRotation = Quaternion.Euler(0, _currentYaw, 0);

            // Seed the live yaw readout with the (possibly saved) starting value so the viewer
            // shows the correct number immediately on open.
            UpdateYawReadout();
        }

        private void Update()
        {
            if (_previewVisual == null || _rt == null) return;

            // Apply current yaw to visual (for buttons + drag).
            _previewVisual.transform.localRotation = Quaternion.Euler(0, _currentYaw, 0);

            // Simple free drag on the preview image area (approximates pointer over RawImage).
            // For full mobile, would use IPointerHandler; this works for desktop + basic touch.
            if (Input.GetMouseButtonDown(0))
            {
                // Check rough screen rect of the image (assumes centered modal).
                Vector2 mp = Input.mousePosition;
                // Rough check: if in center 1/3 of screen vertically/horizontally (modal area).
                if (mp.x > Screen.width * 0.35f && mp.x < Screen.width * 0.65f &&
                    mp.y > Screen.height * 0.35f && mp.y < Screen.height * 0.75f)
                {
                    _dragging = true;
                    _lastDragPos = mp;
                }
            }
            if (_dragging && Input.GetMouseButton(0))
            {
                Vector2 mp = Input.mousePosition;
                float dx = mp.x - _lastDragPos.x;
                _currentYaw += dx * 0.5f; // sensitivity for free rotate
                _currentYaw = Mathf.Repeat(_currentYaw, 360f);
                _lastDragPos = mp;
                UpdateYawReadout(); // live update during drag for premium viewer feel
            }
            if (Input.GetMouseButtonUp(0))
            {
                _dragging = false;
            }

            // Touch support basic (first touch as drag).
            if (Input.touchCount > 0)
            {
                var t = Input.GetTouch(0);
                if (t.phase == TouchPhase.Began)
                {
                    Vector2 tp = t.position;
                    if (tp.x > Screen.width * 0.35f && tp.x < Screen.width * 0.65f &&
                        tp.y > Screen.height * 0.35f && tp.y < Screen.height * 0.75f)
                    {
                        _dragging = true;
                        _lastDragPos = tp;
                    }
                }
                else if (_dragging && (t.phase == TouchPhase.Moved || t.phase == TouchPhase.Stationary))
                {
                    float dx = t.position.x - _lastDragPos.x;
                    _currentYaw += dx * 0.5f;
                    _currentYaw = Mathf.Repeat(_currentYaw, 360f);
                    _lastDragPos = t.position;
                    UpdateYawReadout(); // live update during drag for premium viewer feel
                }
                else if (t.phase == TouchPhase.Ended)
                {
                    _dragging = false;
                }
            }
        }

        private void RotatePreview(float delta)
        {
            _currentYaw = Mathf.Repeat(_currentYaw + delta, 360f);
            if (_previewVisual != null)
                _previewVisual.transform.localRotation = Quaternion.Euler(0, _currentYaw, 0);
            UpdateYawReadout();
        }

        private void UpdateYawReadout()
        {
            if (_yawReadout != null) _yawReadout.text = $"Yaw: {Mathf.Repeat(_currentYaw, 360f):0}°";
        }

        private void ResetToSaved()
        {
            // Reset the preview to the last saved natural orientation for this exact prefab type.
            // If none saved yet, this lands on 0 (model default) — player can then drag to discover it.
            _currentYaw = RotationCorrectionRegistry.GetYawOffset(_entry != null ? _entry.id : null);
            if (_previewVisual != null)
                _previewVisual.transform.localRotation = Quaternion.Euler(0, _currentYaw, 0);
            UpdateYawReadout();
        }

        private void Confirm()
        {
            float yaw = _currentYaw;

            // Core persistence: save (or overwrite) the final yaw the player chose as the
            // permanent default correction for this CatalogEntry.id / prefab type.
            // Next time any structure of this type is armed, Show() will seed the preview
            // with this value so it "always appears correctly oriented".
            if (_entry != null)
            {
                RotationCorrectionRegistry.SetAndSave(_entry.id, yaw);
            }

            Cleanup();
            _onConfirm?.Invoke(yaw);
            Destroy(gameObject);
        }

        private void Cancel()
        {
            Cleanup();
            _onCancel?.Invoke();
            Destroy(gameObject);
        }

        private void Cleanup()
        {
            if (_rt != null) { _rt.Release(); Destroy(_rt); _rt = null; }
            if (_previewRoot != null) Destroy(_previewRoot);
            // Canvas etc destroyed with this GO.
        }

        private void OnDestroy()
        {
            Cleanup();
        }

        /// <summary>Set <paramref name="root"/> and all descendants to <paramref name="layer"/>.</summary>
        private static void SetLayerRecursive(Transform root, int layer)
        {
            if (root == null) return;
            root.gameObject.layer = layer;
            for (int i = 0; i < root.childCount; i++)
                SetLayerRecursive(root.GetChild(i), layer);
        }

        /// <summary>Helper for drag on the RawImage area (attached in SetupUI).</summary>
        private class PreviewDragHandler : MonoBehaviour
        {
            public BuildPreviewModal modal;
            // The Update in modal handles the actual drag; this is marker for area.
        }
    }
}
