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
// GEAR MIRROR (WO-567): the preview body's EquipmentController now also mirrors the OFF-HAND
// (shield) and the ARMOR TIER. Weapon + shield show their real KayKit meshes; armor (the static
// single-model north star — no mesh swap) shows as the tier TINT the world hero gets, so the
// showcase reflects the full equipped look (weapon + shield + armor accent) — not just the weapon.
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
using DeNelle.Core.Diagnostics;

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

        /// <summary>The preview body's weapon-mesh driver (reused world EquipmentController),
        /// so the Gear-screen Orient tool can seat the SHOWN weapon live (parity with the
        /// build-mode model-select Orient). Null until <see cref="Begin"/> attaches it.</summary>
        public EquipmentController Equip => _equip;

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
        public bool Begin(GameObject actorBody, int textureSize = 512, string weaponId = null,
                          string offHandId = null, int armorTier = 0)
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
                // WO-1015 E2 CANDIDATE C: this used to fail SILENTLY (bare `return false`), which
                // reached the panel as an indistinguishable "no preview" and is one live path to
                // the owner's blank navy box. It is now a Fail line naming the size and format.
                FlowTrace.Fail("Preview", string.Format(
                    "rt.Create() FAILED for {0}x{1} ARGB32 (depth 16, AA 2) - no render texture " +
                    "exists, so the preview CANNOT draw. This is a device/format capability " +
                    "failure, not a layout or culling problem.", textureSize, textureSize));
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

            // --- INSTRUMENTATION (WO preview cube-head RCA): enumerate every renderer on the
            // cloned preview body so the trace proves WHY the head renders as a cube here while
            // the live Arena hero's head is fine (wrong source body / disabled-or-missing head
            // renderer / null mesh / null material / wrong shader). Logging only — never throws
            // into Begin. system tag = "Preview".
            try
            {
                var renderers = _model.GetComponentsInChildren<Renderer>(true);
                int skinned = 0;
                for (int i = 0; i < renderers.Length; i++)
                    if (renderers[i] is SkinnedMeshRenderer) skinned++;
                FlowTrace.Step("Preview",
                    $"PreviewActor cloned from '{(actorBody != null ? actorBody.name : "null")}': " +
                    $"{renderers.Length} renderers ({skinned} skinned), preview layer={_previewLayer}");

                for (int i = 0; i < renderers.Length; i++)
                {
                    var r = renderers[i];
                    if (r == null) { FlowTrace.Warn("Preview", $"renderer[{i}] is NULL"); continue; }

                    string rType;
                    Mesh mesh = null;
                    if (r is SkinnedMeshRenderer smr) { rType = "SkinnedMeshRenderer"; mesh = smr.sharedMesh; }
                    else if (r is MeshRenderer)
                    {
                        rType = "MeshRenderer";
                        var mf = r.GetComponent<MeshFilter>();
                        mesh = mf != null ? mf.sharedMesh : null;
                    }
                    else { rType = r.GetType().Name; }

                    bool meshNull = mesh == null;
                    string meshDesc = meshNull ? "MESH-NULL" : $"mesh='{mesh.name}'";

                    var mat = r.sharedMaterial;
                    bool matNull = mat == null;
                    string shaderDesc = matNull
                        ? "NULL-material"
                        : (mat.shader != null ? $"shader='{mat.shader.name}'" : "NULL-shader");

                    string goName = r.gameObject != null ? r.gameObject.name : "<null-go>";
                    Vector3 ext = r.bounds.size;

                    // Box-head RCA: a flat tan/cardboard head = the head submesh lost its albedo.
                    // Log the bound _BaseMap (or _MainTex) texture name + whether a MaterialPropertyBlock
                    // is overriding this renderer — Instantiate does NOT copy per-renderer MPBs, so a
                    // clone can revert to the material's authored albedo OR sit on a stripped block.
                    string texDesc = "tex=?";
                    if (!matNull)
                    {
                        Texture baseTex = null;
                        if (mat.HasProperty("_BaseMap")) baseTex = mat.GetTexture("_BaseMap");
                        if (baseTex == null && mat.HasProperty("_MainTex")) baseTex = mat.GetTexture("_MainTex");
                        Color baseCol = mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor")
                                       : (mat.HasProperty("_Color") ? mat.GetColor("_Color") : Color.white);
                        texDesc = (baseTex != null ? $"baseMap='{baseTex.name}'" : "baseMap=NULL")
                                  + $" baseColor=({baseCol.r:F2},{baseCol.g:F2},{baseCol.b:F2})";
                    }
                    string pbDesc = $"hasMPB={r.HasPropertyBlock()}";

                    FlowTrace.Step("Preview",
                        $"  rend[{i}] '{goName}' {rType} enabled={r.enabled} {meshDesc} " +
                        $"bounds={ext.x:F2}x{ext.y:F2}x{ext.z:F2} {shaderDesc} {texDesc} {pbDesc}");

                    bool looksLikeHead = goName.ToLowerInvariant().Contains("head");
                    if (meshNull || matNull || (looksLikeHead && !r.enabled))
                    {
                        FlowTrace.Warn("Preview",
                            $"  SUSPECT rend[{i}] '{goName}' {rType} enabled={r.enabled} " +
                            $"{meshDesc} {shaderDesc} (meshNull={meshNull} matNull={matNull} headDisabled={(looksLikeHead && !r.enabled)})");
                    }
                }
            }
            catch (System.Exception ex)
            {
                FlowTrace.Warn("Preview", $"renderer enumeration threw ({ex.GetType().Name}: {ex.Message})");
            }

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

            // WO-1015 E2 CANDIDATES D + E. The camera's cullingMask is `1 << _previewLayer` and the
            // clone's layers are set by SetLayerRecursive — if those two ever disagree the camera
            // renders an EMPTY frustum and the RawImage shows the clear colour, which is the exact
            // navy the owner photographed. Likewise a degenerate bounds (all renderers disabled or
            // zero-size) frames the camera on nothing. Both are stated as numbers, not assumed.
            FlowTrace.Step("Preview", string.Format(
                "camera rig: previewLayer={0} (named '{1}' resolved={2}) cullingMask=0x{3:X8} " +
                "modelLayer={4} layersAgree={5} | bounds center=({6:F2},{7:F2},{8:F2}) " +
                "size=({9:F2},{10:F2},{11:F2}) degenerate={12}",
                _previewLayer, PreviewLayerName, LayerMask.NameToLayer(PreviewLayerName) >= 0,
                1 << _previewLayer, _model != null ? _model.layer : -1,
                _model != null && _model.layer == _previewLayer,
                bounds.center.x, bounds.center.y, bounds.center.z,
                bounds.size.x, bounds.size.y, bounds.size.z,
                bounds.size.sqrMagnitude < 0.0001f));

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
            AttachWeaponDriver(weaponId, offHandId, armorTier);

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
        public bool Retarget(GameObject actorBody, string weaponId = null,
                             string offHandId = null, int armorTier = 0)
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
            AttachWeaponDriver(weaponId, offHandId, armorTier);
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

        /// <summary>
        /// WO-567: mirror the FULL equipped look onto the preview body — weapon mesh + off-hand
        /// (shield) mesh + armor TIER tint — then repaint once. Null/empty weapon or off-hand
        /// detaches that slot; armorTier 0 clears the tint. No-op (safe) when the rig is invalid.
        /// </summary>
        public void RefreshGear(string weaponId, string offHandId, int armorTier)
        {
            if (!IsValid || _equip == null) return;
            _equip.Equip(weaponId);
            _equip.EquipOffHand(offHandId);
            _equip.SetArmorTier(armorTier);
            _cam.Render();
        }

        /// <summary>Apply a yaw to the preview model and MANUALLY render (URP won't auto-render the off-screen cam).</summary>
        public void SetRotation(float yawDegrees)
        {
            if (!IsValid) return;
            _model.transform.localRotation = Quaternion.Euler(0f, yawDegrees, 0f);
            _cam.Render();
        }

        /// <summary>
        /// WO-1015 E2 — THE DECISIVE INSTRUMENT. Everything else in this class proves the rig was
        /// CONSTRUCTED; this proves whether anything was DRAWN.
        ///
        /// WHY IT IS NEEDED AND WHY NOTHING ELSE SUBSTITUTES: the camera clears to
        /// Color(0.02, 0.047, 0.094) — byte-identical to the equipment panel's own preview plate
        /// fill. So "the camera rendered an empty frustum" and "the RawImage was never enabled"
        /// and "the panel never built a rig" are the SAME PIXELS on screen. A screenshot cannot
        /// separate them and neither can reading the source. This does: it blits the render
        /// texture down to 16x16, reads it back, and reports how many pixels differ from the clear
        /// colour plus the min/max luminance actually present.
        ///
        ///   diff == 0     -> the camera ran and drew NOTHING. The rig is fine; the MODEL is the
        ///                    dead step (culling mask vs. layer, all renderers disabled, the clone
        ///                    outside the frustum, or a null-material model). Look at the
        ///                    "camera rig:" line and the per-renderer enumeration above it.
        ///   diff &gt; 0     -> the hero IS in the texture. If the owner still sees navy, the dead
        ///                    step is downstream in the PANEL (RawImage disabled, zero-size rect,
        ///                    covered by a sibling, alpha 0) — not in this rig at all.
        ///
        /// One 16x16 readback per Begin/Retarget. Never throws; a failure to probe is itself
        /// reported rather than swallowed.
        /// </summary>
        public void ProbeRenderedContent(string system = "Preview")
        {
            if (!IsValid)
            {
                FlowTrace.Warn(system, "RT PROBE skipped - the rig is not valid (no texture to read).");
                return;
            }

            int diff, total;
            float minLum, maxLum;
            Color clear;
            string error;
            if (!TryMeasureDrawn(out diff, out total, out minLum, out maxLum, out clear, out error))
            {
                // Never swallow: a probe that cannot run must say so, or the next reader assumes
                // the absence of a probe line means the probe passed.
                FlowTrace.Warn(system, "RT PROBE threw (" + error +
                                       ") - the readback is unavailable on this platform/pipeline; " +
                                       "the blank-vs-drawn question stays open.");
                return;
            }

            {
                string verdict = diff == 0
                    ? "NOTHING WAS DRAWN - every sampled pixel is the camera clear colour. The rig " +
                      "built fine, so the dead step is the MODEL (layer vs. cullingMask, all " +
                      "renderers disabled, clone outside the frustum, or null materials) - read the " +
                      "'camera rig:' line and the per-renderer enumeration above."
                    : "CONTENT PRESENT - the hero is in the render texture. If the screen still " +
                      "shows a flat plate, the dead step is DOWNSTREAM in the panel (RawImage " +
                      "disabled / zero rect / covered / alpha 0), not in this rig.";

                FlowTrace.Step(system, string.Format(
                    "RT PROBE {0}x{1}->16x16: {2}/{3} px differ from clear ({4:F3},{5:F3},{6:F3}); " +
                    "lum min={7:F3} max={8:F3}. {9}",
                    _rt.width, _rt.height, diff, total,
                    clear.r, clear.g, clear.b, minLum, maxLum, verdict));

                if (diff == 0)
                    FlowTrace.Fail(system, "RT PROBE: the preview render texture is a UNIFORM clear " +
                                           "colour - the preview box is blank at the SOURCE, not at " +
                                           "the panel. Fix the model/culling, not the RawImage.");
            }
        }

        /// <summary>
        /// WO-1133 — THE EVIDENCE GATE. Same readback as <see cref="ProbeRenderedContent"/>
        /// (deliberately the SAME private measurement, so the number a caller acts on can never
        /// drift from the number the trace reports), but returned as a decision instead of logged
        /// as a verdict.
        ///
        /// WHY A CALLER NEEDS THIS AND NOT JUST THE TRACE: the camera clears to a colour
        /// byte-identical to the panel plate behind it, so a rig that drew NOTHING and a rig that
        /// drew a hero are the same pixels to a screenshot AND to the player. A surface that mounts
        /// the RawImage unconditionally therefore cannot tell that it is presenting an empty box —
        /// which is precisely the defect WO-1133 exists to remove (the owner's "empty navy
        /// rectangle"). Ask this BEFORE mounting, and fall back to a portrait when it answers
        /// false: an honest 2D portrait is strictly better than a plate that reads as broken.
        ///
        /// Returns false when the rig is invalid, when the readback cannot run on this
        /// platform/pipeline, or when every sampled pixel is the clear colour. The reason is in
        /// <paramref name="detail"/> either way — a false is never silent.
        /// </summary>
        public bool DrewContent(out string detail, string system = "Preview")
        {
            if (!IsValid)
            {
                detail = "the rig is not valid (no texture to read)";
                FlowTrace.Warn(system, "DrewContent: " + detail + " - the caller must not mount a RawImage.");
                return false;
            }

            int diff, total;
            float minLum, maxLum;
            Color clear;
            string error;
            if (!TryMeasureDrawn(out diff, out total, out minLum, out maxLum, out clear, out error))
            {
                // A readback that cannot RUN is not evidence that the rig drew. Treat it as
                // "unproven" and answer false: the fallback portrait is always safe, whereas
                // mounting on an unproven texture is how the empty box shipped in the first place.
                detail = "the readback could not run (" + error + ") - drawn-ness is UNPROVEN";
                FlowTrace.Warn(system, "DrewContent: " + detail +
                                       " - answering false so the caller uses its 2D fallback rather " +
                                       "than mounting a texture nothing has verified.");
                return false;
            }

            bool drew = diff > 0;
            detail = string.Format("{0}/{1} sampled px differ from the clear colour (lum {2:F3}..{3:F3})",
                                   diff, total, minLum, maxLum);
            FlowTrace.Step(system, "DrewContent=" + drew + ": " + detail +
                (drew ? " - mounting the live preview."
                      : " - the rig drew NOTHING, so the caller falls back to the 2D portrait " +
                        "instead of presenting a flat plate the player reads as broken."));
            return drew;
        }

        /// <summary>
        /// The ONE readback both <see cref="ProbeRenderedContent"/> and <see cref="DrewContent"/>
        /// measure with: blit the render texture down to 16x16, read it back, and count how many
        /// pixels differ from the camera's clear colour. Returns false with the reason in
        /// <paramref name="error"/> when the readback cannot run (unsupported platform/pipeline) —
        /// never throws, and never reports a false zero, which a caller could mistake for "blank".
        /// </summary>
        private bool TryMeasureDrawn(out int diff, out int total, out float minLum, out float maxLum,
                                     out Color clear, out string error)
        {
            diff = 0; total = 0; minLum = 0f; maxLum = 0f;
            clear = _cam != null ? _cam.backgroundColor : Color.black;
            error = null;

            RenderTexture small = null;
            Texture2D readback = null;
            var prevActive = RenderTexture.active;
            try
            {
                _cam.Render();   // make sure the texture reflects the current state before reading

                const int N = 16;
                small = RenderTexture.GetTemporary(N, N, 0, RenderTextureFormat.ARGB32);
                Graphics.Blit(_rt, small);
                RenderTexture.active = small;
                readback = new Texture2D(N, N, TextureFormat.RGBA32, false);
                readback.ReadPixels(new Rect(0, 0, N, N), 0, 0, false);
                readback.Apply(false, false);

                var px = readback.GetPixels();
                total = px.Length;
                minLum = 1f; maxLum = 0f;
                for (int i = 0; i < px.Length; i++)
                {
                    var c = px[i];
                    float lum = 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
                    if (lum < minLum) minLum = lum;
                    if (lum > maxLum) maxLum = lum;
                    if (Mathf.Abs(c.r - clear.r) > 0.02f ||
                        Mathf.Abs(c.g - clear.g) > 0.02f ||
                        Mathf.Abs(c.b - clear.b) > 0.02f) diff++;
                }
                return true;
            }
            catch (System.Exception ex)
            {
                error = ex.GetType().Name + ": " + ex.Message;
                return false;
            }
            finally
            {
                RenderTexture.active = prevActive;
                if (readback != null) Object.Destroy(readback);
                if (small != null) RenderTexture.ReleaseTemporary(small);
            }
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
        private void AttachWeaponDriver(string weaponId, string offHandId = null, int armorTier = 0)
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
                _equip.EquipOffHand(offHandId);   // WO-567: mirror the shield
                _equip.SetArmorTier(armorTier);   // WO-567: mirror the armor tier tint
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

            // ── WO-1133 §12 — THE LINE THAT SEPARATES THE LAST TWO BLANK-PREVIEW CAUSES ──
            // The RT PROBE (captured 2026-08-21 F8 seq 3585, and again at seq 2833) reports
            // "NOTHING WAS DRAWN". That verdict has exactly two survivors once the layer
            // masks are read: either the camera is AIMED AT EMPTY SPACE, or the clone has no
            // drawable renderers. Nothing printed either number, so the two were
            // indistinguishable in every capture so far.
            //
            // WHY AIM CAN BE WRONG AND LOOK RIGHT: FrameCamera aims at ComputeBounds, which
            // sums Renderer.bounds — the WORLD-space AABB. A SkinnedMeshRenderer with
            // updateWhenOffscreen=false (the default) derives that AABB from its root bone
            // plus local bounds, and the clone is instantiated at RigOrigin (-5000,-5000,0)
            // in the SAME frame this runs. If the bounds still describe the SOURCE body near
            // the world origin, the camera is aimed ~7000 units away from the model it is
            // supposed to frame, renders an empty frustum, and the RawImage shows the clear
            // colour — which is byte-identical to the panel's own plate fill. That is the
            // owner's navy box exactly, and no screenshot can tell it from "no renderers".
            //
            // READ IT AS: aimVsModel is the distance from the aim point to where the clone
            // ACTUALLY is. Near zero => the aim is sound and the cause is the MODEL (read the
            // rend[i] enumeration above for enabled=False / MESH-NULL / NULL-material).
            // Large (hundreds or thousands) => the aim is the dead step and the bounds are
            // stale — fix the bounds source, NOT the renderers.
            try
            {
                Vector3 modelPos = _model != null ? _model.transform.position : RigOrigin;
                float aimVsModel = Vector3.Distance(bounds.center, modelPos);
                FlowTrace.Step("Preview", string.Format(
                    "camera framing: aim=({0:F1},{1:F1},{2:F1}) modelPos=({3:F1},{4:F1},{5:F1}) " +
                    "aimVsModel={6:F1} camDist={7:F2} radius={8:F2} rigOrigin=({9:F0},{10:F0},{11:F0}) " +
                    "aimLooksStale={12}",
                    bounds.center.x, bounds.center.y, bounds.center.z,
                    modelPos.x, modelPos.y, modelPos.z,
                    aimVsModel, dist, radius,
                    RigOrigin.x, RigOrigin.y, RigOrigin.z,
                    aimVsModel > radius * 4f + 1f));

                if (aimVsModel > radius * 4f + 1f)
                    FlowTrace.Warn("Preview", string.Format(
                        "camera framing: the aim point is {0:F0} units from the cloned model " +
                        "(model radius {1:F2}). The camera is framing EMPTY SPACE, so the render " +
                        "texture will read as a uniform clear colour no matter how healthy the " +
                        "renderers are. The bounds handed to FrameCamera do not describe the " +
                        "clone at RigOrigin.", aimVsModel, radius));
            }
            catch (System.Exception ex)
            {
                // Never swallow (§12): a diagnostic that cannot run says so, or its absence
                // reads as a pass.
                FlowTrace.Warn("Preview", "camera framing trace threw (" + ex.GetType().Name +
                                          ": " + ex.Message + ") - the aim-vs-model question stays open.");
            }
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
