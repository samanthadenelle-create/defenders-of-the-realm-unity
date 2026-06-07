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
using UnityEngine.Rendering.Universal; // URP: configure the preview camera to actually render to the RT
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
        private bool _closing;    // WO-314: idempotent close guard (prevents double-fire + post-close NRE)
        private readonly List<Material> _tempMaterials = new List<Material>(); // WO-314: runtime mats to free on close

        // Manual hit-test targets — this project's builds have no reliable EventSystem/
        // GraphicRaycaster, so Button.onClick never fires (documented recurring issue,
        // see GameOverScreen). We poll these rects against taps in Update() instead.
        private RectTransform _previewImageRT; // the drag-rotate area (taps here = drag, NOT a button)
        private readonly List<(RectTransform rect, Action action)> _buttons = new List<(RectTransform, Action)>();

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

            // Belt-and-suspenders: ensure an EventSystem exists so the uGUI Buttons COULD
            // route clicks. But builds here often lack a working one, so the authoritative
            // input path is the manual rect hit-test in Update() below.
            EventSystemEnsurer.EnsureEventSystem();

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
            _previewImageRT = imgRT; // drag-rotate area for the manual hit-test in Update()

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

            // uGUI onClick is registered as a best-effort path, but is NOT relied upon —
            // builds here lack a working EventSystem/GraphicRaycaster, so the manual rect
            // hit-test in Update() is the authoritative input. Register the rect for it.
            var btn = btnGO.GetComponent<Button>();
            btn.onClick.AddListener(() => onClick());
            _buttons.Add((btnRT, onClick));
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
                _tempMaterials.Add(mat);
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
                try { skinned = VisualFactory.Skin(_previewVisual.transform, _entry.visualPrefabPath, SkinOptions.Prop(2.5f)); }
                catch (Exception e) { Debug.LogWarning($"[BuildPreviewModal] preview skin failed for {_entry.id}: {e.Message}"); skinned = null; }
                // If Skin returned null the viewer falls back to a disc and looks "broken" —
                // surface WHY so missing/mis-pathed Resources prefabs are diagnosable.
                if (skinned == null)
                    Debug.LogWarning($"[BuildPreviewModal] VisualFactory.Skin returned null for id='{_entry.id}' " +
                                     $"visualPrefabPath='{_entry.visualPrefabPath}' — showing fallback disc. " +
                                     "Verify the prefab exists under a Resources/ folder at that path.");
            }
            else
            {
                Debug.LogWarning($"[BuildPreviewModal] entry has no visualPrefabPath (id='{(_entry != null ? _entry.id : "<null>")}') — showing fallback disc.");
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
                if (r != null)
                {
                    var discMat = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color")) { color = new Color(0.3f, 0.5f, 0.3f) };
                    _tempMaterials.Add(discMat);
                    r.sharedMaterial = discMat;
                }
            }

            // Preview camera (orthographic for a clean object view).
            var camGO = new GameObject("PreviewCam");
            camGO.transform.SetParent(_previewRoot.transform, false);
            _previewCam = camGO.AddComponent<Camera>();
            _previewCam.clearFlags = CameraClearFlags.SolidColor;
            _previewCam.backgroundColor = new Color(0.15f, 0.15f, 0.18f);
            _previewCam.orthographic = true;
            _previewCam.nearClipPlane = 0.1f;
            _previewCam.farClipPlane = 10000f; // rig is at y=-5000; far must reach the camera→object span
            _previewCam.targetTexture = _rt;

            // URP: a runtime-created Camera needs UniversalAdditionalCameraData to render
            // (the SRP only walks cameras it knows about). Mark it a self-contained Base
            // camera with no overlay stack so it draws the rig into the RT under URP.
            // (DeNelle.Village already references Unity.RenderPipelines.Universal.Runtime.)
            var urp = camGO.AddComponent<UniversalAdditionalCameraData>();
            urp.renderType = CameraRenderType.Base;
            urp.renderPostProcessing = false;
            urp.requiresColorOption = CameraOverrideOption.Off;
            urp.requiresDepthOption = CameraOverrideOption.Off;

            // ISOLATE: put the whole rig on PREVIEW_LAYER and mask the camera + lights to it,
            // so nothing here renders into — or lights — the live village scene. Do this
            // BEFORE framing so bounds are measured on the final rig.
            SetLayerRecursive(_previewRoot.transform, PREVIEW_LAYER);
            _previewCam.cullingMask = 1 << PREVIEW_LAYER;
            light1.cullingMask = 1 << PREVIEW_LAYER;
            light2.cullingMask = 1 << PREVIEW_LAYER;

            // Initial rotation on visual.
            if (_previewVisual != null)
                _previewVisual.transform.localRotation = Quaternion.Euler(0, _currentYaw, 0);

            // Frame the camera on the actual rendered bounds of the rig (object + plane) so
            // the object is guaranteed in-shot regardless of its fitted size / seat position.
            // Without this the fixed orthoSize + LookAt-on-the-empty-root could leave the
            // model outside the frustum → RT clears to the bg colour → "blank" viewer.
            FrameCameraOnRig(camGO.transform);

            // Seed the live yaw readout with the (possibly saved) starting value so the viewer
            // shows the correct number immediately on open.
            UpdateYawReadout();
        }

        /// <summary>Points the (orthographic) preview camera at the rig's combined renderer
        /// bounds from a 3/4 angle and sizes the ortho frustum to contain it with margin.</summary>
        private void FrameCameraOnRig(Transform cam)
        {
            Bounds b;
            var rends = _previewRoot != null ? _previewRoot.GetComponentsInChildren<Renderer>() : null;
            if (rends != null && rends.Length > 0)
            {
                b = rends[0].bounds;
                for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            }
            else
            {
                // No renderers (shouldn't happen — disc fallback always adds one) — frame the root.
                b = new Bounds(_previewRoot != null ? _previewRoot.transform.position : Vector3.zero, Vector3.one * 4f);
            }

            float radius = Mathf.Max(0.5f, b.extents.magnitude);
            // 3/4 viewing direction, distance scaled to the object's radius.
            Vector3 dir = new Vector3(1f, 0.9f, -1f).normalized;
            cam.position = b.center + dir * (radius * 2.5f);
            cam.LookAt(b.center);
            _previewCam.orthographicSize = radius * 1.15f; // a little margin around the object
        }

        private void Update()
        {
            if (_closing || _previewVisual == null || _rt == null) return;

            // Apply current yaw to visual (for buttons + drag).
            _previewVisual.transform.localRotation = Quaternion.Euler(0, _currentYaw, 0);

            // ── Manual input (NO EventSystem) ───────────────────────────────────
            // This project's builds do not have a reliable EventSystem/GraphicRaycaster,
            // so uGUI Button.onClick never fires (documented recurring issue — see
            // GameOverScreen.cs). We hit-test each control's RectTransform against the
            // pointer ourselves, using the SAME proven pattern. A press that lands on a
            // button fires that button; a press inside the preview image becomes a drag;
            // everything else is ignored. This guarantees Confirm / Cancel / ±90 / Reset
            // and drag-rotate all work in the player build.

            // 1) Press: button taps take priority over starting a drag.
            if (TryGetPressDown(out Vector2 pressPos))
            {
                bool hitButton = false;
                for (int i = 0; i < _buttons.Count; i++)
                {
                    var b = _buttons[i];
                    if (b.rect != null &&
                        RectTransformUtility.RectangleContainsScreenPoint(b.rect, pressPos, null))
                    {
                        hitButton = true;
                        // Cancel/Confirm Destroy this object — guard against running another
                        // action afterwards by breaking immediately.
                        b.action?.Invoke();
                        break;
                    }
                }

                // Only begin a drag if the press was inside the preview image and NOT on a button.
                if (!hitButton && _previewImageRT != null &&
                    RectTransformUtility.RectangleContainsScreenPoint(_previewImageRT, pressPos, null))
                {
                    _dragging = true;
                    _lastDragPos = pressPos;
                }
                if (_closing) return; // a button (Confirm/Cancel) tore us down
            }

            // 2) Drag-rotate (held). Stops when the press is released.
            if (_dragging && TryGetHeldPos(out Vector2 heldPos))
            {
                float dx = heldPos.x - _lastDragPos.x;
                _currentYaw = Mathf.Repeat(_currentYaw + dx * 0.5f, 360f); // sensitivity for free rotate
                _lastDragPos = heldPos;
                UpdateYawReadout(); // live update during drag for premium viewer feel
            }

            // 3) Release.
            if (TryGetPressUp())
                _dragging = false;
        }

        /// <summary>True on the frame a mouse-down or touch-begin happens; outputs the screen pos.</summary>
        private static bool TryGetPressDown(out Vector2 pos)
        {
            if (Input.touchCount > 0)
            {
                var t = Input.GetTouch(0);
                if (t.phase == TouchPhase.Began) { pos = t.position; return true; }
                pos = default; return false;
            }
            if (Input.GetMouseButtonDown(0)) { pos = (Vector2)Input.mousePosition; return true; }
            pos = default; return false;
        }

        /// <summary>True while a press is held; outputs the current screen pos.</summary>
        private static bool TryGetHeldPos(out Vector2 pos)
        {
            if (Input.touchCount > 0)
            {
                var t = Input.GetTouch(0);
                if (t.phase == TouchPhase.Moved || t.phase == TouchPhase.Stationary)
                { pos = t.position; return true; }
                pos = default; return false;
            }
            if (Input.GetMouseButton(0)) { pos = (Vector2)Input.mousePosition; return true; }
            pos = default; return false;
        }

        /// <summary>True on the frame the press is released.</summary>
        private static bool TryGetPressUp()
        {
            if (Input.touchCount > 0)
            {
                var t = Input.GetTouch(0);
                return t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled;
            }
            return Input.GetMouseButtonUp(0);
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
            if (_closing) return;   // WO-314: idempotent — a second click / re-entry can't double-fire
            _closing = true;
            float yaw = _currentYaw;

            // Core persistence: save (or overwrite) the final yaw the player chose as the
            // permanent default correction for this CatalogEntry.id / prefab type.
            // Next time any structure of this type is armed, Show() will seed the preview
            // with this value so it "always appears correctly oriented".
            if (_entry != null)
            {
                RotationCorrectionRegistry.SetAndSave(_entry.id, yaw);
            }

            var cb = _onConfirm;
            Cleanup();
            Destroy(gameObject);    // WO-314: close FIRST so a throwing placement callback can't leave the modal stuck open
            try { cb?.Invoke(yaw); }
            catch (Exception e) { Debug.LogError($"[BuildPreviewModal] confirm callback threw: {e}"); }
        }

        private void Cancel()
        {
            if (_closing) return;
            _closing = true;
            var cb = _onCancel;
            Cleanup();
            Destroy(gameObject);
            try { cb?.Invoke(); }
            catch (Exception e) { Debug.LogError($"[BuildPreviewModal] cancel callback threw: {e}"); }
        }

        private void Cleanup()
        {
            if (_rt != null) { _rt.Release(); Destroy(_rt); _rt = null; }
            if (_previewRoot != null) { Destroy(_previewRoot); _previewRoot = null; }
            // WO-314: destroy runtime-created materials — Unity does NOT auto-free these when the
            // renderer GameObject is destroyed, so they leaked on every modal open.
            for (int i = 0; i < _tempMaterials.Count; i++)
                if (_tempMaterials[i] != null) Destroy(_tempMaterials[i]);
            _tempMaterials.Clear();
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
    }
}
