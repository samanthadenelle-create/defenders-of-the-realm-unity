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
        private readonly List<Renderer> _renderers = new List<Renderer>();
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
            _mpb = new MaterialPropertyBlock();

            _visual = new GameObject("BuildGhost");
            _visual.transform.SetParent(transform, false);

            float fit = entry.repo != null && entry.repo.placement != null
                ? Mathf.Max(1f, entry.repo.placement.footprint)
                : 3f;

            GameObject skinned = null;
            if (!string.IsNullOrEmpty(entry.visualPrefabPath))
                skinned = VisualFactory.Skin(_visual.transform, entry.visualPrefabPath,
                    SkinOptions.Structure(fit));

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
            if (_visual != null) Destroy(_visual);
            _visual = null;
            _builtForId = null;
        }

        private void OnDestroy() => Clear();

        // ── helpers ────────────────────────────────────────────────────────────

        private void CollectRenderers(Transform root)
        {
            _renderers.Clear();
            _renderers.AddRange(root.GetComponentsInChildren<Renderer>(true));
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
