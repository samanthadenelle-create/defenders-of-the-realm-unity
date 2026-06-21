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
using DeNelle.Core.Catalog;

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

        /// <summary>
        /// (Re)build the ghost for <paramref name="entry"/>. No-op if the same entry
        /// is already shown. Falls back to a flat marker when the visual is absent.
        /// </summary>
        public void SetEntry(CatalogEntry entry)
        {
            if (entry == null) { Hide(); return; }
            if (_visual != null && _builtForId == entry.id) return;

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
            SkinOptions opts;
            float visualHeight = entry.repo != null ? entry.repo.visualHeight : 0f;
            if (visualHeight > 0f)
            {
                opts = SkinOptions.Structure(0f);
                opts.FitHeight = visualHeight;
            }
            else
            {
                opts = SkinOptions.Structure(fit);
            }

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

        /// <summary>Move the ghost to a snapped world position with a discrete yaw.</summary>
        public void MoveTo(Vector3 snappedWorldPos, int yawSteps)
        {
            if (_visual == null) return;
            _visual.transform.SetPositionAndRotation(
                snappedWorldPos, Quaternion.Euler(0f, yawSteps * 90f, 0f));
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

        /// <summary>Hide (but keep) the ghost — re-shown on the next MoveTo.</summary>
        public void Hide()
        {
            if (_visual != null) _visual.SetActive(false);
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
        }

        private void OnDestroy() => Clear();

        // ── helpers ────────────────────────────────────────────────────────────

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
                Shader shader = (src != null ? src.shader : null)
                                ?? Shader.Find("Universal Render Pipeline/Lit")
                                ?? Shader.Find("Sprites/Default");
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
    }
}
