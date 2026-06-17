// =============================================================================
// HeroPreviewViewer — WO-434 Phase D. A reusable live-actor RenderTexture preview.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.Hero
//
// Renders a live 3D preview of ANY actor body (the hero, a companion, or — later —
// a troop-creation candidate for raids/defenses) into a RenderTexture that a UI
// RawImage shows. Built DIRECTLY on the proven Village/UI/TowerPreviewCamera rig:
//   • clone the actor onto a far-off origin on a dedicated "HeroPreview" layer,
//   • strip gameplay MonoBehaviours/colliders/rigidbodies so nothing ticks,
//   • a dedicated Camera (DISABLED — driven MANUALLY via camera.Render() because URP
//     SKIPS an off-screen Base camera in its auto render loop) + a key light,
//   • a RenderTexture the panel binds; frame the actor at a 3/4 hero angle.
//
// WEAPON MIRROR (reuse, low-risk): the preview body gets its OWN EquipmentController
// (the SAME component the world hero uses to attach a KayKit weapon mesh to the
// RightHand bone with the primitive fallback). RefreshWeapon(weaponId) drives that
// controller's Equip(id) so the preview shows the EXACT mesh the world hero shows —
// no separate attach path invented. The controller is added in a disabled state and
// driven explicitly, so its OnEnable auto-read / WaveManager combat-pose Update never
// run on the off-screen clone.
//
// ARMOR LIMITATION (honored, not faked): EquipmentController.SetArmorTier is a recorded
// NO-OP today (body armor art unauthored). So equipping a WEAPON updates the preview;
// equipping ARMOR does not change the body mesh yet — expected, the moment art lands the
// same controller lights it up here with no further plumbing.
//
// GRACEFUL: if the source body / RenderTexture can't be created, Begin returns false and
// the panel simply skips the preview (no NRE, no blank screen). Render only happens while
// the panel is open (the panel calls RenderOnce on its repaint / a SetRotation), and
// Dispose() frees the clone + RT + camera — no per-frame allocation, no leak.
//
// Lives in DeNelle.Village (no new asmdef — Village already references the URP runtime
// assemblies RenderTexture work needs; mirrors TowerPreviewCamera).
// =============================================================================

using UnityEngine;

namespace DeNelle.Village.Hero
{
    /// <summary>
    /// Self-contained render-texture preview rig for a single actor body. Create via
    /// <see cref="Begin"/>, mirror the equipped weapon with <see cref="RefreshWeapon"/>,
    /// rotate with <see cref="SetRotation"/>, repaint with <see cref="RenderOnce"/>, and
    /// free everything with <see cref="Dispose"/>. Implements <see cref="System.IDisposable"/>.
    /// </summary>
    public sealed class HeroPreviewViewer : System.IDisposable
    {
        // Reuse TowerPreview's layer approach: a dedicated layer keeps the preview camera
        // masked to ONLY the preview model. The layer is optional in TagManager (layers
        // 9-31 are unnamed in this project), so ResolvePreviewLayer falls back to layer 31
        // exactly like TowerPreviewCamera — the preview still draws rather than throwing.
        private const string PreviewLayerName = "HeroPreview";

        // Far from the live scene so the clone / its light never bleed into gameplay even
        // if the layer mask were somehow wrong. Distinct from TowerPreview's origin so the
        // two rigs never share space if both are open.
        private static readonly Vector3 RigOrigin = new Vector3(-5000f, -5000f, 0f);

        private GameObject    _root;     // parent holder for the whole rig
        private GameObject    _model;    // cloned actor body (rotated)
        private Camera        _cam;      // manually-driven preview camera
        private Light         _light;    // key light for the preview
        private RenderTexture _rt;
        private EquipmentController _equip;   // preview-body weapon-mesh driver (reused world component)
        private int  _previewLayer = -1;
        private bool _disposed;

        /// <summary>The render texture the panel binds to a RawImage. Null if Begin failed.</summary>
        public RenderTexture Texture => _rt;

        /// <summary>True when the rig was created and a texture is available to display.</summary>
        public bool IsValid => !_disposed && _rt != null && _cam != null && _model != null;

        /// <summary>
        /// Build the preview rig for <paramref name="actorBody"/> — a body PREFAB or an
        /// already-instantiated body (e.g. the live "HeroBody" child). The source is cloned
        /// (the live object is never reparented or mutated), placed on the hidden layer, has
        /// its gameplay behaviours stripped, and is framed at a 3/4 hero angle. Returns false
        /// (and creates nothing) when the body is null or the RenderTexture can't be created —
        /// the caller then skips the preview. <paramref name="weaponId"/> (optional) seats the
        /// initial weapon mesh; pass the active loadout's equipped weapon id.
        /// </summary>
        public bool Begin(GameObject actorBody, int textureSize = 512, string weaponId = null)
        {
            if (_disposed) return false;
            if (actorBody == null) return false;

            _previewLayer = ResolvePreviewLayer();

            _rt = new RenderTexture(textureSize, textureSize, 16, RenderTextureFormat.ARGB32)
            {
                name             = "HeroPreviewRT",
                antiAliasing     = 2,
                useMipMap        = false,
                autoGenerateMips = false,
            };
            if (!_rt.Create())
            {
                // RT allocation failed (rare — out of VRAM / unsupported format). Bail
                // gracefully: free the half-made RT and report failure so the panel skips.
                Object.Destroy(_rt);
                _rt = null;
                return false;
            }

            _root = new GameObject("HeroPreviewRig") { hideFlags = HideFlags.HideAndDontSave };
            _root.transform.position = RigOrigin;
            SetLayerRecursive(_root, _previewLayer);

            // --- model (CLONE — never touch the live body) ----------------------
            _model = Object.Instantiate(actorBody, RigOrigin, Quaternion.identity, _root.transform);
            if (_model == null) { Dispose(); return false; }
            _model.name = "PreviewActor";
            _model.SetActive(true);                 // the live child may be inactive mid-build; the clone must render
            SetLayerRecursive(_model, _previewLayer);
            StripGameplayBehaviours(_model);

            Bounds bounds = ComputeBounds(_model);

            // --- camera (manually driven; URP won't auto-render an off-screen Base cam) ---
            var camGo = new GameObject("PreviewCam");
            camGo.transform.SetParent(_root.transform, false);
            SetLayerRecursive(camGo, _previewLayer);

            _cam = camGo.AddComponent<Camera>();
            _cam.clearFlags      = CameraClearFlags.SolidColor;
            _cam.backgroundColor = new Color(0.02f, 0.047f, 0.094f, 1f); // #050c18 viewport bg (matches TowerPreview)
            _cam.cullingMask     = 1 << _previewLayer;                   // sees ONLY the preview layer
            _cam.targetTexture   = _rt;
            _cam.fieldOfView     = 32f;
            _cam.nearClipPlane   = 0.05f;
            _cam.farClipPlane    = 5000f;
            _cam.allowMSAA       = false;
            _cam.enabled         = false;          // CRITICAL — manual Render() only

            FrameCamera(bounds);

            // --- key light -------------------------------------------------------
            var lightGo = new GameObject("PreviewLight");
            lightGo.transform.SetParent(_root.transform, false);
            SetLayerRecursive(lightGo, _previewLayer);
            _light = lightGo.AddComponent<Light>();
            _light.type        = LightType.Directional;
            _light.color       = new Color(1f, 0.96f, 0.85f);
            _light.intensity   = 1.25f;
            _light.cullingMask = 1 << _previewLayer;
            lightGo.transform.rotation = Quaternion.Euler(35f, -40f, 0f);

            // --- weapon-mesh driver (REUSE the world EquipmentController) ---------
            // The clone carries a valid Humanoid Animator/avatar (it is a copy of the live
            // class body), so the controller's RightHand bone lookup resolves and it attaches
            // the SAME KayKit weapon mesh the world hero uses (or the primitive fallback). It
            // is added DISABLED + driven explicitly so its OnEnable auto-read / Update combat
            // poll never run on the off-screen clone.
            AttachWeaponDriver(weaponId);

            // First draw so the texture isn't blank before the first repaint.
            _cam.Render();
            return true;
        }

        /// <summary>
        /// Re-point the preview at a DIFFERENT actor body (e.g. switching the equip target to
        /// a companion). Tears down the current clone + weapon driver and rebuilds against the
        /// new body, KEEPING the existing camera / light / RenderTexture (so the bound RawImage
        /// keeps its texture). Returns false (and leaves the rig unchanged) if the body is null.
        /// </summary>
        public bool Retarget(GameObject actorBody, string weaponId = null)
        {
            if (!IsValid || actorBody == null) return false;

            if (_equip != null) { Object.Destroy(_equip); _equip = null; }
            if (_model != null) { Object.Destroy(_model); _model = null; }

            _model = Object.Instantiate(actorBody, RigOrigin, Quaternion.identity, _root.transform);
            if (_model == null) return false;
            _model.name = "PreviewActor";
            _model.SetActive(true);
            SetLayerRecursive(_model, _previewLayer);
            StripGameplayBehaviours(_model);

            FrameCamera(ComputeBounds(_model));
            AttachWeaponDriver(weaponId);
            _cam.Render();
            return true;
        }

        /// <summary>
        /// Mirror the equipped weapon onto the preview body: drives the preview's own
        /// EquipmentController to show <paramref name="weaponId"/>'s mesh (null / empty
        /// unequips). Then repaints. Call from EquipVM.Changed. No-op (safe) if the rig is
        /// invalid or the driver couldn't attach. ARMOR is intentionally not mirrored — the
        /// controller's armor visual is a NO-OP stub today (see file header).
        /// </summary>
        public void RefreshWeapon(string weaponId)
        {
            if (!IsValid || _equip == null) return;
            _equip.Equip(weaponId);
            _cam.Render();
        }

        /// <summary>Apply a yaw to the preview model and MANUALLY render (URP won't auto-render the off-screen cam).</summary>
        public void SetRotation(float yawDegrees)
        {
            if (!IsValid) return;
            _model.transform.localRotation = Quaternion.Euler(0f, yawDegrees, 0f);
            _cam.Render();
        }

        /// <summary>Repaint the RenderTexture once (manual render). Safe no-op when invalid.</summary>
        public void RenderOnce()
        {
            if (!IsValid) return;
            _cam.Render();
        }

        /// <summary>Destroy the clone, weapon driver, camera, light, rig holder, and render texture.</summary>
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
            _equip = null;
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        // Add an EquipmentController to the clone (disabled so its lifecycle hooks don't run
        // on the off-screen body) and seat the initial weapon. The controller resolves the
        // RightHand bone off the clone's own Animator (CacheRig). Guarded so a clone without a
        // valid Humanoid avatar (e.g. a fallback capsule) just shows no weapon, never throws.
        private void AttachWeaponDriver(string weaponId)
        {
            if (_model == null) return;
            try
            {
                _equip = _model.GetComponent<EquipmentController>();
                if (_equip == null)
                {
                    _equip = _model.AddComponent<EquipmentController>();
                    _equip.enabled = false;   // we drive Equip() explicitly; no OnEnable/Update on the clone
                }
                _equip.Equip(weaponId);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[HeroPreviewViewer] weapon driver attach skipped: " + e.Message);
                _equip = null;
            }
        }

        // Find the "HeroPreview" layer index. If the project has no such layer (it must be
        // added in TagManager; cannot be created at runtime) fall back to layer 31 so the
        // preview still draws — same strategy as TowerPreviewCamera.
        private static int ResolvePreviewLayer()
        {
            int layer = LayerMask.NameToLayer(PreviewLayerName);
            if (layer >= 0) return layer;

            // Reuse TowerPreview's layer if THAT one exists (both are masked-off preview
            // layers and never on-screen together), else the high fallback layer.
            int tower = LayerMask.NameToLayer("TowerPreview");
            if (tower >= 0) return tower;

            Debug.LogWarning(
                "[HeroPreviewViewer] Layer 'HeroPreview' not found — add it in " +
                "Project Settings > Tags and Layers so the preview camera masks correctly. " +
                "Falling back to layer 31.");
            return 31;
        }

        // Frame the camera so the model's bounds fill the viewport at a 3/4 hero angle —
        // a touch more front-on than the tower preview so a character reads well.
        private void FrameCamera(Bounds bounds)
        {
            float radius = Mathf.Max(bounds.extents.magnitude, 0.5f);
            float fovRad = _cam.fieldOfView * Mathf.Deg2Rad;
            float dist   = radius / Mathf.Sin(fovRad * 0.5f) * 1.08f;

            // 3/4 hero angle: slightly above, offset to the side (mirrors TowerPreviewCamera).
            Vector3 dir = new Vector3(0.35f, 0.22f, -1f).normalized;
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

        // Make the clone a pure visual: disable colliders, kinematic rigidbodies, and disable
        // every MonoBehaviour so no gameplay/AI/locomotion script ticks on the off-screen body.
        // EquipmentController is added AFTER this pass (already disabled) and driven explicitly,
        // so it is not re-enabled here. We disable rather than destroy to respect [RequireComponent].
        private static void StripGameplayBehaviours(GameObject go)
        {
            foreach (var col in go.GetComponentsInChildren<Collider>(true))
                if (col != null) col.enabled = false;
            foreach (var rb in go.GetComponentsInChildren<Rigidbody>(true))
                if (rb != null) rb.isKinematic = true;
            foreach (var cam in go.GetComponentsInChildren<Camera>(true))
                if (cam != null) cam.enabled = false;     // a stray body camera must not fight the rig cam
            foreach (var al in go.GetComponentsInChildren<AudioListener>(true))
                if (al != null) Object.Destroy(al);
            foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null) continue;
                // Keep Animator-adjacent visual drivers off the worry list — MonoBehaviours
                // only (Animator is not a MonoBehaviour, so the rig still poses). Conservative:
                // disable, never destroy.
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
