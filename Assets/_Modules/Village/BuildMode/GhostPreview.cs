// =============================================================================
// GhostPreview — the translucent placement ghost for Build Mode (WO-108 P1).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// Renders the selected CatalogEntry's visual as a semi-transparent preview that
// follows the cursor and tints green (valid) / red (blocked). Reuses the proven
// MaterialPropertyBlock tint approach from TowerPlacementSystem (never leaks a
// material instance per frame) and VisualFactory to skin the real prefab so the
// ghost looks like what will be placed (not a cylinder).
//
// The ghost host has NO collider, so it never blocks its own overlap test nor
// catches the placement ray.
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DeNelle.Core.Catalog;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village
{
    /// <summary>
    /// A throwaway translucent clone of the selected entry's visual that tracks the
    /// cursor and shows valid/blocked state. One ghost at a time; rebuilt when the
    /// armed entry changes.
    /// </summary>
    public sealed class GhostPreview : MonoBehaviour
    {
        private static readonly Color s_validColor = new Color(0.2f, 0.9f, 0.3f, 0.45f);
        private static readonly Color s_invalidColor = new Color(0.9f, 0.2f, 0.2f, 0.45f);

        private GameObject _visual;
        private string _builtForId;
        // The armed entry's upright correction (StructureFactory applies the SAME at
        // build time). Applied UNDER the ghost's yaw so the ghost stands upright exactly
        // like the placed structure (WYSIWYG) — yaw outermost, orientation inner.
        private OrientationFix _orientation;
        private readonly List<Renderer> _renderers = new List<Renderer>();
        // Audit P2 (build-mode): every transparent ghost material is a `new Material(shader)`
        // instance; track them so Clear() can Destroy() them instead of leaking one set per
        // re-arm for the rest of the session.
        private readonly List<Material> _createdMaterials = new List<Material>();
        private MaterialPropertyBlock _mpb;

        // ── Reject-reason label (owner 2026-07-24 "tell me why it's red") ────────
        // A silent world-space label that floats above the ghost showing WHY it can't be
        // placed while blocked. NON-buzzing + no toast spam: the place loop just feeds it
        // the reason string every frame (MessageFor / shortfall) and it shows/hides with
        // the red tint. Parented to the host (persists across re-arm); follows the tracked
        // visual and billboards to the camera each frame. Fail-safe: build/positioning is
        // guarded so a UI hiccup never throws into the place loop.
        private GameObject _reasonGo;
        private Text _reasonText;
        private Camera _reasonCam;
        private const float ReasonLabelHeight = 2.4f;   // world units above the ghost base

        /// <summary>
        /// (Re)build the ghost for <paramref name="entry"/>. No-op if the same entry
        /// is already shown. Falls back to a flat marker when the visual is absent.
        /// </summary>
        public void SetEntry(CatalogEntry entry)
        {
            if (entry == null) { FlowTrace.Warn("Ghost", "SetEntry(null) — hiding ghost"); Hide(); return; }
            if (_visual != null && _builtForId == entry.id) return;

            FlowTrace.Step("Ghost", $"SetEntry id='{entry.id}' prefabPath='{entry.visualPrefabPath ?? "<null>"}'");
            Clear();
            _builtForId = entry.id;
            _orientation = entry.orientation;   // applied UNDER the yaw on the skinned model
            _mpb = new MaterialPropertyBlock();

            _visual = new GameObject("BuildGhost");
            _visual.transform.SetParent(transform, false);

            float fit = entry.repo != null && entry.repo.placement != null
                ? Mathf.Max(1f, entry.repo.placement.footprint)
                : 3f;

            // WYSIWYG SCALE (TKT-12): match StructureFactory.Create's fit EXACTLY, or the ghost and the
            // PLACED object end up different sizes. The placed path uses FitHeight when the entry sets a
            // visualHeight (DEF-208 tall-structure fix) and FitLargest(footprint) otherwise — so the ghost
            // must do the SAME, not always FitLargest. Mirrors StructureFactory.cs:79-94. (`fit` stays
            // method-scoped — the pack-missing fallback disc below also uses it.)
            // WO-764: EVERY structure now fits-to-HEIGHT = YHeightVariable * repo.heightMul (uniform
            // base × the per-item multiplier; buildings inherit 1.0, towers 1.25). The ghost must match
            // Create's centralized formula EXACTLY or the dragged ghost and the placed building end up
            // different sizes.
            SkinOptions opts = SkinOptions.Structure(0f);
            float ghostMul = (entry.repo != null && entry.repo.heightMul > 0f) ? entry.repo.heightMul : 1f;
            opts.FitHeight = StructureFactory.YHeightVariable * ghostMul;

            GameObject skinned = null;
            if (!string.IsNullOrEmpty(entry.visualPrefabPath))
                skinned = VisualFactory.Skin(_visual.transform, entry.visualPrefabPath, opts);

            // WYSIWYG — apply the entry's human-verified upright correction to the skinned
            // model the SAME way StructureFactory.Create does (StructureFactory.cs:90-98),
            // so the ghost stands upright exactly like the placed result. _visual carries
            // the player's yaw (MoveTo), so this composes UNDER it (yaw outermost).
            if (skinned != null && _orientation != null && _orientation.manual)
            {
                var t = skinned.transform;
                t.localRotation = Quaternion.Euler(_orientation.Euler) * t.localRotation;
                t.localPosition += _orientation.Offset;
                if (_orientation.scale > 0f && !Mathf.Approximately(_orientation.scale, 1f))
                    t.localScale *= _orientation.scale;

                // WYSIWYG seat — VisualFactory.SeatOnGround seated the RAW bounds at the host
                // y; the correction above re-tipped the mesh so it now floats/sinks. Re-drop
                // the CORRECTED bounds base to the ghost host's y, matching StructureFactory's
                // ReseatCorrectedBottom so the ghost sits exactly where the piece will land.
                ReseatCorrectedBottom(skinned, _visual.transform.position.y);
            }

            if (skinned == null)
            {
                FlowTrace.Warn("Ghost", $"VisualFactory.Skin returned null for '{entry.visualPrefabPath ?? "<none>"}' — falling back to a flat disc marker");
                // Pack-missing-safe: a flat translucent disc stands in for the mesh.
                var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                disc.transform.SetParent(_visual.transform, false);
                disc.transform.localScale = new Vector3(fit, 0.05f, fit);
                var c = disc.GetComponent<Collider>();
                if (c != null) Destroy(c);
            }

            // Collect renderers, strip colliders, and swap to a transparent material.
            CollectRenderers(_visual.transform);
            StripColliders(_visual.transform);
            ApplyTransparentMaterials();
        }

        /// <summary>
        /// (Re)build a simple PLACEHOLDER ghost (a translucent capsule on a disc) not
        /// tied to a CatalogEntry. Used by the Arena Defense setup screen (WO-389): a
        /// pre-placed troop has no CatalogEntry visual at setup time (full troop-body
        /// skinning happens at raid spawn), so the setup screen reuses this SAME ghost
        /// primitive — MoveTo / SetValid / Hide — with a generic marker. The
        /// <paramref name="key"/> dedupes rebuilds so re-arming the same troop is a no-op.
        /// </summary>
        public void SetPlaceholder(string key, float fit = 2.5f)
        {
            if (_visual != null && _builtForId == key) return;

            Clear();
            _builtForId = key;
            _mpb = new MaterialPropertyBlock();

            _visual = new GameObject("ArenaDefenseGhost");
            _visual.transform.SetParent(transform, false);

            // A capsule body (the unit) standing on a thin footprint disc — enough to
            // read placement + validity; the real troop body is skinned at spawn.
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.transform.SetParent(_visual.transform, false);
            body.transform.localScale = new Vector3(0.9f, 1.1f, 0.9f);
            body.transform.localPosition = new Vector3(0f, 1.1f, 0f);

            var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.transform.SetParent(_visual.transform, false);
            disc.transform.localScale = new Vector3(Mathf.Max(1f, fit), 0.05f, Mathf.Max(1f, fit));

            CollectRenderers(_visual.transform);
            StripColliders(_visual.transform);
            ApplyTransparentMaterials();
        }

        /// <summary>Move the ghost to a snapped world position with a discrete 90° yaw
        /// (legacy quarter-step callers, e.g. the Arena defense setup screen).</summary>
        public void MoveTo(Vector3 snappedWorldPos, int yawSteps)
            => MoveTo(snappedWorldPos, yawSteps * 90f);

        /// <summary>Move the ghost to a snapped world position with an exact yaw in degrees
        /// (WO-673 L5 — Build Mode rotates in 45° steps, so the controller passes degrees).</summary>
        public void MoveTo(Vector3 snappedWorldPos, float yawDegrees)
        {
            if (_visual == null) return;
            _visual.transform.SetPositionAndRotation(
                snappedWorldPos, Quaternion.Euler(0f, yawDegrees, 0f));
            if (!_visual.activeSelf) _visual.SetActive(true);
        }

        /// <summary>Tint the ghost green (valid) or red (blocked) via the shared MPB.</summary>
        public void SetValid(bool valid)
        {
            if (_mpb == null) _mpb = new MaterialPropertyBlock();
            var color = valid ? s_validColor : s_invalidColor;
            foreach (var r in _renderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(_mpb);
                _mpb.SetColor("_BaseColor", color);
                _mpb.SetColor("_Color", color);
                r.SetPropertyBlock(_mpb);
            }
        }

        /// <summary>
        /// Show the floating "why it's red" label above the ghost (owner 2026-07-24). Pass a
        /// message (e.g. "Ground is too uneven here", "Not enough Wood (70)") while the ghost
        /// is blocked; pass null/empty to clear it (valid placement, or ghost hidden). Silent
        /// (no buzz) and idempotent per frame so passive hover never spams a toast/sound.
        /// Fail-safe: any build hiccup just leaves the label hidden.
        /// </summary>
        public void SetReason(string message)
        {
            bool show = !string.IsNullOrEmpty(message) && _visual != null && _visual.activeSelf;
            try
            {
                if (show && _reasonGo == null) BuildReasonLabel();
                if (_reasonGo == null) return;
                if (_reasonGo.activeSelf != show) _reasonGo.SetActive(show);
                if (show && _reasonText != null && _reasonText.text != message)
                    _reasonText.text = message;
            }
            catch (System.Exception e)
            {
                FlowTrace.Warn("Ghost", $"SetReason failed (label hidden): {e.Message}");
                if (_reasonGo != null) _reasonGo.SetActive(false);
            }
        }

        /// <summary>
        /// The ghost's CURRENT world position — the TRACKED VISUAL's transform, not the
        /// host. WO-683 fleet RCA (run detail "stuck at (15, 15)" = WorldToCell(origin)):
        /// MoveTo drives the child <c>_visual</c>; the GhostPreview host GameObject never
        /// moves off world origin, so any probe reading <c>transform.position</c> sees a
        /// constant. Falls back to the host position when no visual is built.
        /// </summary>
        public Vector3 CurrentPosition => _visual != null ? _visual.transform.position : transform.position;

        /// <summary>Hide (but keep) the ghost — re-shown on the next MoveTo.</summary>
        public void Hide()
        {
            if (_visual != null) _visual.SetActive(false);
            if (_reasonGo != null) _reasonGo.SetActive(false);   // no floating label on a hidden ghost
        }

        /// <summary>Destroy the ghost entirely (placement landed / cancelled).</summary>
        public void Clear()
        {
            _renderers.Clear();
            // Audit P2 (build-mode): destroy the ghost's owned material instances so re-arming
            // doesn't leak a Material set per entry.
            for (int i = 0; i < _createdMaterials.Count; i++)
                if (_createdMaterials[i] != null) Destroy(_createdMaterials[i]);
            _createdMaterials.Clear();
            if (_visual != null) Destroy(_visual);
            _visual = null;
            _builtForId = null;
            _orientation = null;
            if (_reasonGo != null) _reasonGo.SetActive(false);   // keep the label object; just hide it
        }

        private void OnDestroy() => Clear();

        /// <summary>Keep the reason label floating above the ghost + facing the camera.</summary>
        private void Update()
        {
            if (_reasonGo == null || !_reasonGo.activeSelf) return;
            try
            {
                if (_reasonCam == null) _reasonCam = Camera.main;
                _reasonGo.transform.position = CurrentPosition + Vector3.up * ReasonLabelHeight;
                if (_reasonCam != null)
                    _reasonGo.transform.rotation = Quaternion.LookRotation(
                        _reasonGo.transform.position - _reasonCam.transform.position, Vector3.up);
            }
            catch (System.Exception e)
            {
                FlowTrace.Warn("Ghost", $"reason-label follow failed (hidden): {e.Message}");
                _reasonGo.SetActive(false);
            }
        }

        // ── helpers ────────────────────────────────────────────────────────────

        /// <summary>
        /// Lazily build the world-space reason label: a dark pill with soft-red WebGL-safe
        /// Text (LegacyRuntime.ttf), parented to the host so it survives ghost re-arms.
        /// </summary>
        private void BuildReasonLabel()
        {
            _reasonGo = new GameObject("GhostReasonLabel");
            _reasonGo.transform.SetParent(transform, false);

            var canvas = _reasonGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            var crt = (RectTransform)_reasonGo.transform;
            crt.sizeDelta = new Vector2(360f, 64f);
            crt.localScale = Vector3.one * 0.012f;   // 360px * 0.012 ~= 4.3 world units wide

            // Dark pill so the text reads over any terrain colour.
            var bg = new GameObject("bg");
            bg.transform.SetParent(_reasonGo.transform, false);
            var bgImg = bg.AddComponent<Image>();
            bgImg.color = new Color(0f, 0f, 0f, 0.72f);
            var bgrt = (RectTransform)bg.transform;
            bgrt.anchorMin = Vector2.zero; bgrt.anchorMax = Vector2.one;
            bgrt.offsetMin = Vector2.zero; bgrt.offsetMax = Vector2.zero;

            var txtGo = new GameObject("text");
            txtGo.transform.SetParent(_reasonGo.transform, false);
            _reasonText = txtGo.AddComponent<Text>();
            _reasonText.font = ReasonFont();
            _reasonText.fontSize = 28;
            _reasonText.alignment = TextAnchor.MiddleCenter;
            _reasonText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _reasonText.verticalOverflow = VerticalWrapMode.Overflow;
            _reasonText.color = new Color(1f, 0.55f, 0.5f, 1f);   // soft red, matches the blocked tint
            var trt = (RectTransform)txtGo.transform;
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(10f, 4f); trt.offsetMax = new Vector2(-10f, -4f);
        }

        private static Font ReasonFont() =>
            Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
            ?? Resources.GetBuiltinResource<Font>("Arial.ttf");

        private void CollectRenderers(Transform root)
        {
            _renderers.Clear();
            _renderers.AddRange(root.GetComponentsInChildren<Renderer>(true));
        }

        /// <summary>
        /// Drop <paramref name="go"/> so its current (post-correction) world-bounds base sits
        /// at <paramref name="groundY"/> — mirrors StructureFactory.ReseatCorrectedBottom so
        /// the ghost and the placed structure seat identically (WYSIWYG).
        /// </summary>
        private static void ReseatCorrectedBottom(GameObject go, float groundY)
        {
            if (go == null) return;
            var rends = go.GetComponentsInChildren<Renderer>(true);
            if (rends == null || rends.Length == 0) return;
            Bounds b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            float dy = groundY - b.min.y;
            if (!Mathf.Approximately(dy, 0f))
                go.transform.position += new Vector3(0f, dy, 0f);
        }

        private void StripColliders(Transform root)
        {
            foreach (var c in root.GetComponentsInChildren<Collider>(true))
                if (c != null) Destroy(c);
        }

        private void ApplyTransparentMaterials()
        {
            foreach (var r in _renderers)
            {
                if (r == null) continue;
                var src = r.sharedMaterial;
                // §12 (owner F8 build-mode top-down: solid PINK gate/wall ghost): a build strips the
                // polyperfect Standard/built-in variant, so src.shader resolves to
                // Hidden/InternalErrorShader (magenta) — NON-null, so the old `?? URP/Lit` fallback
                // never fired and we copied the error shader. Worse, InternalErrorShader has no
                // _Surface, so the transparency block below was skipped → an OPAQUE pink ghost.
                // Treat a null/broken/Standard/Legacy/error source shader as "unusable" and build the
                // ghost on URP/Lit so it renders translucent, never magenta.
                Shader srcShader = src != null ? src.shader : null;
                bool broken = srcShader == null || IsBrokenGhostShader(srcShader);
                if (broken) FlowTrace.Warn("Ghost", $"source shader '{(srcShader != null ? srcShader.name : "<null>")}' is broken/stripped for URP — rebuilding ghost on URP/Lit (pink-ghost guard)");
                Shader shader = !broken
                                ? srcShader
                                : (Shader.Find("Universal Render Pipeline/Lit")
                                   ?? Shader.Find("Sprites/Default"));
                var mat = new Material(shader);
                _createdMaterials.Add(mat);   // Audit P2: tracked for Destroy() in Clear()
                if (mat.HasProperty("_Surface"))
                {
                    mat.SetFloat("_Surface", 1f);   // transparent
                    mat.SetFloat("_Blend", 0f);
                    mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    mat.SetFloat("_ZWrite", 0f);
                    mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                }
                r.sharedMaterial = mat;
            }
            SetValid(true);
        }

        // A source shader that renders MAGENTA / wrong under URP (or was stripped from the build).
        // Mirrors MagentaGuard.IsBrokenShader — a ghost must never inherit one of these, or it draws
        // as an opaque pink block instead of a translucent placement preview.
        private static bool IsBrokenGhostShader(Shader sh)
        {
            if (sh == null) return true;
            string sn = sh.name;
            if (string.IsNullOrEmpty(sn)) return true;
            return sn == "Standard"
                || sn == "Standard (Specular setup)"
                || sn.StartsWith("Legacy Shaders/")
                || sn.Contains("InternalError");
        }
    }
}
