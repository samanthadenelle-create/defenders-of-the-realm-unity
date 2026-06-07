// =============================================================================
// TowerPreviewCamera — WO-334 (Tower Placement Rotate Menu) RT-preview helper.
// -----------------------------------------------------------------------------
// Renders a live 3D preview of a tower prefab into a RenderTexture that the
// TowerPlacementRotateMenu shows as a VisualElement background.
//
// CRITICAL render detail (diagnosed cause of the old BuildPreviewModal white
// box): URP SKIPS an off-screen RenderTexture Base camera in its auto render
// loop. So we DRIVE THE CAMERA MANUALLY — previewCam.enabled = false and we call
// previewCam.Render() every frame (after applying the preview rotation) to draw
// into the RenderTexture ourselves.
//
// Everything (camera, prefab instance, light) lives on a dedicated "TowerPreview"
// layer so the preview camera sees ONLY the preview model and nothing from the
// live scene. The whole rig is created in Begin() and torn down in Dispose().
//
// Lives in DeNelle.Village (no new asmdef — Village already references the URP
// runtime assemblies needed for RenderTexture work).
// =============================================================================

using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>
    /// Self-contained render-texture preview rig for a single tower prefab.
    /// Create via <see cref="Begin"/>, rotate the model each frame with
    /// <see cref="SetRotation"/> (which manually renders), and free everything
    /// with <see cref="Dispose"/>. Implements <see cref="System.IDisposable"/>.
    /// </summary>
    public sealed class TowerPreviewCamera : System.IDisposable
    {
        private const string PreviewLayerName = "TowerPreview";

        // The preview rig is positioned far from the live scene so its bounds /
        // light never bleed into gameplay even if the layer mask were wrong.
        private static readonly Vector3 RigOrigin = new Vector3(0f, -5000f, 0f);

        private GameObject     _root;       // parent holder for the whole rig
        private GameObject     _model;      // instantiated tower prefab (rotated)
        private Camera         _cam;        // manually-driven preview camera
        private Light          _light;      // key light for the preview
        private RenderTexture  _rt;
        private int            _previewLayer = -1;
        private bool           _disposed;

        /// <summary>The render texture the menu binds as a VisualElement background. Null if Begin failed.</summary>
        public RenderTexture Texture => _rt;

        /// <summary>True when the rig was created and a texture is available to display.</summary>
        public bool IsValid => !_disposed && _rt != null && _cam != null && _model != null;

        /// <summary>
        /// Build the preview rig for <paramref name="prefab"/>. Returns true on
        /// success. If the prefab is null (a data-only TowerData with no Level-1
        /// visual) this returns false and the menu should use its icon fallback.
        /// </summary>
        public bool Begin(GameObject prefab, int textureSize = 512)
        {
            if (prefab == null) return false;

            _previewLayer = ResolvePreviewLayer();

            _rt = new RenderTexture(textureSize, textureSize, 16, RenderTextureFormat.ARGB32)
            {
                name             = "TowerPreviewRT",
                antiAliasing     = 2,
                useMipMap        = false,
                autoGenerateMips = false,
            };
            _rt.Create();

            _root = new GameObject("TowerPreviewRig") { hideFlags = HideFlags.HideAndDontSave };
            _root.transform.position = RigOrigin;
            SetLayerRecursive(_root, _previewLayer);

            // --- model ----------------------------------------------------------
            _model = Object.Instantiate(prefab, RigOrigin, Quaternion.identity, _root.transform);
            _model.name = "PreviewModel";
            SetLayerRecursive(_model, _previewLayer);
            StripGameplayBehaviours(_model);

            Bounds bounds = ComputeBounds(_model);

            // --- camera ---------------------------------------------------------
            var camGo = new GameObject("PreviewCam");
            camGo.transform.SetParent(_root.transform, false);
            SetLayerRecursive(camGo, _previewLayer);

            _cam = camGo.AddComponent<Camera>();
            _cam.clearFlags      = CameraClearFlags.SolidColor;
            _cam.backgroundColor = new Color(0.02f, 0.047f, 0.094f, 1f); // #050c18 viewport bg
            _cam.cullingMask     = 1 << _previewLayer;                    // sees ONLY the preview layer
            _cam.targetTexture   = _rt;
            _cam.fieldOfView     = 35f;
            _cam.nearClipPlane   = 0.05f;
            _cam.farClipPlane    = 5000f;
            _cam.allowMSAA       = false;
            // CRITICAL: disable so URP does not try (and skip) to auto-render an
            // off-screen Base camera. We render() it manually each frame instead.
            _cam.enabled         = false;

            FrameCamera(bounds);

            // --- key light ------------------------------------------------------
            var lightGo = new GameObject("PreviewLight");
            lightGo.transform.SetParent(_root.transform, false);
            SetLayerRecursive(lightGo, _previewLayer);
            _light = lightGo.AddComponent<Light>();
            _light.type        = LightType.Directional;
            _light.color       = new Color(1f, 0.96f, 0.85f);
            _light.intensity   = 1.25f;
            _light.cullingMask = 1 << _previewLayer;
            lightGo.transform.rotation = Quaternion.Euler(35f, -40f, 0f);

            // First draw so the texture isn't blank before the first SetRotation.
            _cam.Render();
            return true;
        }

        /// <summary>
        /// Apply <paramref name="rotation"/> to the preview model and MANUALLY
        /// render the camera into the RenderTexture (URP will not auto-render an
        /// off-screen Base camera). Call this every frame the panel is open.
        /// </summary>
        public void SetRotation(Quaternion rotation)
        {
            if (!IsValid) return;
            _model.transform.localRotation = rotation;
            _cam.Render();
        }

        /// <summary>Destroy the model, camera, light, rig holder, and render texture.</summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            if (_cam != null) _cam.targetTexture = null;

            if (_rt != null)
            {
                _rt.Release();
                Object.Destroy(_rt);
                _rt = null;
            }

            if (_root != null)
            {
                Object.Destroy(_root);
                _root = null;
            }

            _model = null;
            _cam   = null;
            _light = null;
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        /// <summary>
        /// Find the "TowerPreview" layer index. If the project has no such layer
        /// (it must be added in TagManager; cannot be created at runtime), fall
        /// back to a high unused layer guess and finally to the default layer so
        /// the preview still draws rather than throwing.
        /// </summary>
        private static int ResolvePreviewLayer()
        {
            int layer = LayerMask.NameToLayer(PreviewLayerName);
            if (layer >= 0) return layer;

            Debug.LogWarning(
                "[TowerPreviewCamera] Layer 'TowerPreview' not found — add it in " +
                "Project Settings > Tags and Layers so the preview camera masks " +
                "correctly. Falling back to layer 31.");
            return 31;
        }

        /// <summary>Frame the camera so the model's bounds fill the viewport at a pleasing 3/4 angle.</summary>
        private void FrameCamera(Bounds bounds)
        {
            float radius = Mathf.Max(bounds.extents.magnitude, 0.5f);
            float fovRad = _cam.fieldOfView * Mathf.Deg2Rad;
            float dist   = radius / Mathf.Sin(fovRad * 0.5f) * 1.15f;

            // 3/4 hero angle: slightly above, offset to the side.
            Vector3 dir = new Vector3(0.45f, 0.35f, -1f).normalized;
            _cam.transform.position = bounds.center - dir * dist;
            _cam.transform.LookAt(bounds.center);
        }

        private static Bounds ComputeBounds(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0)
                return new Bounds(go.transform.position, Vector3.one);

            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                b.Encapsulate(renderers[i].bounds);
            return b;
        }

        /// <summary>
        /// Remove gameplay-side behaviours from the instantiated preview so it is
        /// a pure visual (no colliders firing, no AI ticking, no rigidbodies).
        /// We disable rather than destroy to avoid touching [RequireComponent] webs.
        /// </summary>
        private static void StripGameplayBehaviours(GameObject go)
        {
            foreach (var col in go.GetComponentsInChildren<Collider>(true))
                col.enabled = false;
            foreach (var rb in go.GetComponentsInChildren<Rigidbody>(true))
                rb.isKinematic = true;
            foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true))
            {
                // Keep visual drivers (anything rendering) but kill scripts that
                // would tick logic. Conservative: only disable, never destroy.
                if (mb == null) continue;
                mb.enabled = false;
            }
        }

        private static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform)
                SetLayerRecursive(child.gameObject, layer);
        }
    }
}
