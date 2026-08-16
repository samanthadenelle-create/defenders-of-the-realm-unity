// =============================================================================
// TalentNodeVfxRig - off-screen render rig for the owner-picked talent node VFX.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.Talents
//
// OWNER VFX PICKS (2026-08-16, mapped VERBATIM, never substituted):
//   "Assets\Hovl Studio\Map track markers VFX\Prefabs\Marker 2 Pointer Loop.prefab"
//       -> the NODE POINTER (single focused/next node). Ships via the tracked
//          mirror Resources/VFX/UI/TalentNodePointer (TalentPointerVfxMirror).
//   "Assets\Resources\VFX\Aura\Aura_PetLevel2.prefab"
//       -> the NODE AURA (owned/learned nodes, plural). Already tracked - loaded
//          straight from Resources "VFX/Aura/Aura_PetLevel2", no mirror needed.
//
// ONE rig class serves both picks: the talent panel is screen-space uGUI
// (code-built, ElarionUiKit.BuildModalCanvas), so a world particle prefab cannot
// draw over it directly. This reuses the PROVEN in-tree idiom for live
// 3D/particles inside a uGUI panel - the HeroPreviewViewer / TowerPreviewCamera
// pattern:
//   * instantiate the effect at a far-off origin on the masked preview layer,
//   * a dedicated DISABLED camera (URP skips off-screen Base cameras in its auto
//     loop, so the panel drives camera.Render() manually each frame while open),
//   * RenderTexture -> RawImage patches seated behind the node plates.
// The camera clears to the graph well's own near-black, so a patch composites
// invisibly against the Obsidian canvas whether the shaders are additive or
// alpha-blended (the same opaque-clear trick both preview rigs already use).
//
// DRAW COST: each rig holds ONE instance and ONE RenderTexture, rendered once
// per frame; every node patch is just a RawImage SAMPLING that shared texture.
// Ten owned aura nodes cost ten quads, not ten particle systems.
//
// GRACEFUL: if the prefab is absent (pointer mirror not run / asset missing),
// Begin() FlowTrace.Warns (never errors) and returns false - the panel keeps its
// code-built node art. Every effect is ADDITIVE presentation, never a
// replacement, in both outcomes.
// =============================================================================

using UnityEngine;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village.Talents
{
    /// <summary>
    /// Off-screen render rig for one owner-picked talent node effect (pointer loop or
    /// node aura). Create with <see cref="Begin"/>, repaint per frame with
    /// <see cref="RenderTick"/>, bind <see cref="Texture"/> to RawImage patches on the
    /// target nodes, free with <see cref="Dispose"/>. Graceful: a missing prefab Warns
    /// and leaves the panel's code-built node art as the sole presentation.
    /// </summary>
    public sealed class TalentNodeVfxRig : System.IDisposable
    {
        /// <summary>Pointer mirror's Resources path (no extension). The committer's
        /// TalentPointerVfxMirror run is what puts the asset here.</summary>
        public const string PointerResourcePath = "VFX/UI/TalentNodePointer";

        /// <summary>Aura pick's Resources path - already tracked, no mirror step.</summary>
        public const string AuraResourcePath = "VFX/Aura/Aura_PetLevel2";

        // Masked-off preview layer - same resolution chain as HeroPreviewViewer.
        private const string PreviewLayerName = "HeroPreview";

        // The graph well's near-black (HeroSkillTreePanelMvvm.BuildScrollGraph viewport
        // fill). Clearing to the SAME ink makes the rectangular RT patch read as the
        // canvas itself, so only the effect's light is visible.
        private static readonly Color WellInk = new Color(0.018f, 0.016f, 0.022f, 1f);

        private readonly string _resourcePath;
        private readonly string _flowSys;
        private readonly string _rigName;
        private readonly Vector3 _origin;
        private readonly string _missingNote;

        private GameObject    _root;
        private GameObject    _instance;
        private Camera        _cam;
        private RenderTexture _rt;
        private int           _layer = -1;
        private bool          _disposed;

        /// <summary>The rig for the owner's pointer pick (focused/next node).</summary>
        public static TalentNodeVfxRig CreatePointer()
        {
            // Origin far from the live scene AND distinct from HeroPreview (-5000,-5000)
            // / TowerPreview origins, so rigs never share space when panels overlap.
            return new TalentNodeVfxRig(PointerResourcePath, "TalentPointer",
                "TalentPointerRig", new Vector3(-5400f, -5400f, 0f),
                "TalentPointerVfxMirror has not run (or the mirror asset is absent on " +
                "this machine). The code-built gold focus ring remains the pointer presentation.");
        }

        /// <summary>The rig for the owner's aura pick (owned/learned nodes).</summary>
        public static TalentNodeVfxRig CreateAura()
        {
            return new TalentNodeVfxRig(AuraResourcePath, "TalentAura",
                "TalentAuraRig", new Vector3(-5600f, -5600f, 0f),
                "the tracked aura asset did not resolve. Owned nodes keep their " +
                "code-built gold-border prestige read.");
        }

        private TalentNodeVfxRig(string resourcePath, string flowSys, string rigName,
                                 Vector3 origin, string missingNote)
        {
            _resourcePath = resourcePath;
            _flowSys      = flowSys;
            _rigName      = rigName;
            _origin       = origin;
            _missingNote  = missingNote;
        }

        /// <summary>The texture the panel binds to the node patches. Null until Begin succeeds.</summary>
        public RenderTexture Texture => _rt;

        /// <summary>True when the rig exists and can be rendered.</summary>
        public bool IsValid => !_disposed && _rt != null && _cam != null && _instance != null;

        /// <summary>
        /// Resolve the prefab and build the off-screen rig. Returns false (creating
        /// nothing, Warn not error) when the asset is absent - the caller keeps the
        /// code-built node art and must not retry every repaint.
        /// </summary>
        public bool Begin(int textureSize = 256)
        {
            if (_disposed) return false;

            var prefab = Resources.Load<GameObject>(_resourcePath);
            if (prefab == null)
            {
                FlowTrace.Warn(_flowSys, "prefab MISSING at Resources path '" + _resourcePath +
                    "' - " + _missingNote);
                return false;
            }

            _layer = ResolvePreviewLayer();

            _rt = new RenderTexture(textureSize, textureSize, 16, RenderTextureFormat.ARGB32)
            {
                name             = _rigName + "RT",
                antiAliasing     = 1,
                useMipMap        = false,
                autoGenerateMips = false,
            };
            if (!_rt.Create())
            {
                FlowTrace.Warn(_flowSys, "rt.Create() FAILED for " + textureSize + "x" + textureSize +
                    " ARGB32 - device/format capability failure; the code-built node art alone shows the state.");
                Object.Destroy(_rt);
                _rt = null;
                return false;
            }

            _root = new GameObject(_rigName) { hideFlags = HideFlags.HideAndDontSave };
            _root.transform.position = _origin;
            SetLayerRecursive(_root, _layer);

            _instance = Object.Instantiate(prefab, _origin, Quaternion.identity, _root.transform);
            if (_instance == null) { Dispose(); return false; }
            _instance.name = _rigName + "Instance";
            _instance.SetActive(true);
            SetLayerRecursive(_instance, _layer);

            // Pure visual: nothing on the effect may collide/tick gameplay off-screen.
            foreach (var col in _instance.GetComponentsInChildren<Collider>(true))
                if (col != null) col.enabled = false;

            // Prewarm the loop so the very first bound frame shows a formed effect, and
            // so the framing bounds below measure LIVE particles, not an empty t=0.
            var systems = _instance.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < systems.Length; i++)
            {
                if (systems[i] == null) continue;
                systems[i].Simulate(1.5f, withChildren: false, restart: true);
                systems[i].Play(withChildren: false);
            }

            Bounds bounds = ComputeBounds(_instance);
            FrameCamera(bounds);

            FlowTrace.Step(_flowSys, "spawn: resolved '" + _resourcePath + "' (owner pick, " +
                _rigName + "), systems=" + systems.Length +
                " layer=" + _layer + " rt=" + textureSize + "x" + textureSize +
                " bounds=(" + bounds.size.x.ToString("F2") + "," + bounds.size.y.ToString("F2") +
                "," + bounds.size.z.ToString("F2") + ")");

            _cam.Render();   // first draw so bound RawImages are never blank
            return true;
        }

        /// <summary>Repaint the loop's current frame into the texture. Call once per frame
        /// while the panel is open (URP will not auto-render this off-screen camera).</summary>
        public void RenderTick()
        {
            if (!IsValid) return;
            _cam.Render();
        }

        /// <summary>Destroy the instance, camera, rig holder and render texture.</summary>
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
            _instance = null;
            _cam = null;
            FlowTrace.Step(_flowSys, "despawn: " + _rigName + " disposed with the panel");
        }

        // -- helpers (mirrors HeroPreviewViewer) --------------------------------

        private void FrameCamera(Bounds bounds)
        {
            var camGo = new GameObject(_rigName + "Cam");
            camGo.transform.SetParent(_root.transform, false);
            SetLayerRecursive(camGo, _layer);

            _cam = camGo.AddComponent<Camera>();
            _cam.clearFlags       = CameraClearFlags.SolidColor;
            _cam.backgroundColor  = WellInk;              // composites as the graph canvas
            _cam.cullingMask      = 1 << _layer;          // sees ONLY this rig
            _cam.orthographic     = true;
            _cam.nearClipPlane    = 0.05f;
            _cam.farClipPlane     = 200f;
            _cam.allowMSAA        = false;
            _cam.enabled          = false;                // CRITICAL - manual Render() only

            // Straight-on front view; ortho size frames the LIVE loop with margin so the
            // effect's motion never clips at the patch edge.
            float half = Mathf.Max(bounds.extents.x, bounds.extents.y, 0.4f);
            _cam.orthographicSize = half * 1.3f;
            _cam.transform.position = bounds.center + Vector3.back * 20f;
            _cam.transform.rotation = Quaternion.identity;   // looks +Z at the effect
        }

        private static Bounds ComputeBounds(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0)
                return new Bounds(go.transform.position, Vector3.one);

            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                if (renderers[i] != null) b.Encapsulate(renderers[i].bounds);
            if (b.size.sqrMagnitude < 0.0001f)
                b = new Bounds(go.transform.position, Vector3.one);
            return b;
        }

        private static int ResolvePreviewLayer()
        {
            int layer = LayerMask.NameToLayer(PreviewLayerName);
            if (layer >= 0) return layer;
            int tower = LayerMask.NameToLayer("TowerPreview");
            if (tower >= 0) return tower;
            return 31;   // same masked-off fallback HeroPreviewViewer / TowerPreviewCamera use
        }

        private static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform)
                SetLayerRecursive(child.gameObject, layer);
        }
    }
}
