// =============================================================================
// CosmeticApplier — WO-73 visual applier (reconciled).
// -----------------------------------------------------------------------------
// Sits in DeNelle.Cosmetics (same asmdef as GlimmerCurrencyService / CosmeticCatalog).
// Handles material swaps, prefab overrides, and VFX attachment on any character
// or building that can wear a cosmetic.
//
// Reconciliation vs WO-73 spec:
//   • WO-73 used `CosmeticData` (WO-72 SO that was never built). This branch
//     uses `CosmeticDef` (DeNelle.Cosmetics.CosmeticDef from CosmeticCatalog).
//     Because CosmeticDef has only a `previewColor` swatch (no materialOverride
//     / prefabOverride / vfxPrefab asset refs yet), the material path tints the
//     MeshRenderer's shared material to the preview color as a first-pass visual.
//     Prefab + VFX override slots are Inspector-exposed for art/prefab hookup
//     later without any code change.
//   • Does NOT reference MonetizationManager (doesn't exist). Ownership is
//     queried through GlimmerCurrencyService.Instance.Owns() at the call site.
// =============================================================================

using UnityEngine;

namespace DeNelle.Cosmetics
{
    /// <summary>
    /// Apply cosmetic visuals (material tint, prefab swap, VFX attach) to a
    /// character or building. Add this component to any prefab that should
    /// be skinnable. Wire meshRenderer, defaultModel, and attachmentPoint in
    /// the Inspector; the cosmetic data fills in the rest at runtime.
    /// </summary>
    [RequireComponent(typeof(MeshRenderer))]
    [DisallowMultipleComponent]
    public sealed class CosmeticApplier : MonoBehaviour
    {
        // ── Inspector references ──────────────────────────────────────────────
        [Header("References")]
        [Tooltip("The MeshRenderer to tint / material-swap. Auto-resolved from this GameObject if null.")]
        public MeshRenderer  meshRenderer;

        [Tooltip("The default model root to show/hide when a prefab override is applied.")]
        public GameObject    defaultModel;

        [Tooltip("Parent transform for prefab-override and VFX instantiation.")]
        public Transform     attachmentPoint;

        // ── Per-cosmetic art overrides (filled by art team, optional) ─────────
        [Header("Art Overrides (filled per cosmetic id)")]
        [Tooltip("Material to swap to for the current cosmetic. Leave null to use preview-color tinting.")]
        public Material      materialOverride;

        [Tooltip("Replacement model prefab. Leave null to keep the default model.")]
        public GameObject    prefabOverride;

        [Tooltip("VFX prefab to attach at the attachment point.")]
        public GameObject    vfxPrefab;

        // ── Runtime state ─────────────────────────────────────────────────────
        private Material     _originalMaterial;
        private Color        _originalColor;
        private GameObject   _currentOverrideModel;
        private GameObject   _currentVfx;

        private string       _equippedCosmeticId;

        private void Awake()
        {
            if (meshRenderer == null)
                meshRenderer = GetComponent<MeshRenderer>();

            if (meshRenderer != null && meshRenderer.sharedMaterial != null)
            {
                _originalMaterial = meshRenderer.sharedMaterial;
                _originalColor    = _originalMaterial.color;
            }
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Apply a cosmetic by its catalog id. Looks up the <see cref="CosmeticDef"/>
        /// from <see cref="CosmeticCatalog"/> and applies the best visual available:
        /// <list type="number">
        ///   <item>If <see cref="materialOverride"/> is assigned in the Inspector, swap it.</item>
        ///   <item>Otherwise tint the MeshRenderer to the cosmetic's preview color.</item>
        ///   <item>If <see cref="prefabOverride"/> is assigned, instantiate and hide the default model.</item>
        ///   <item>If <see cref="vfxPrefab"/> is assigned, instantiate at the attachment point.</item>
        /// </list>
        /// No-op if the id is empty or not found in the catalog.
        /// </summary>
        public void ApplyCosmetic(string cosmeticId)
        {
            if (string.IsNullOrEmpty(cosmeticId)) return;

            var def = CosmeticCatalog.Find(cosmeticId);
            if (def == null)
            {
                Debug.LogWarning($"[CosmeticApplier] Cosmetic id '{cosmeticId}' not found in catalog.");
                return;
            }

            _equippedCosmeticId = cosmeticId;

            ApplyMaterial(def);
            ApplyPrefab();
            ApplyVfx();

            Debug.Log($"[CosmeticApplier] Applied cosmetic '{def.DisplayName}' to {gameObject.name}.");
        }

        /// <summary>
        /// Convenience overload that takes a <see cref="CosmeticDef"/> directly
        /// (avoids a second catalog lookup when the caller already has the def).
        /// </summary>
        public void ApplyCosmetic(CosmeticDef cosmetic)
        {
            if (cosmetic == null) return;
            _equippedCosmeticId = cosmetic.Id;
            ApplyMaterial(cosmetic);
            ApplyPrefab();
            ApplyVfx();
            Debug.Log($"[CosmeticApplier] Applied cosmetic '{cosmetic.DisplayName}' to {gameObject.name}.");
        }

        /// <summary>
        /// Restore the original material/color and default model; destroy any
        /// active override prefab and VFX instance.
        /// </summary>
        public void ResetToDefault()
        {
            // Restore material.
            if (meshRenderer != null)
            {
                if (_originalMaterial != null)
                    meshRenderer.sharedMaterial = _originalMaterial;
                else if (meshRenderer.material != null)
                    meshRenderer.material.color = _originalColor;
            }

            // Destroy override prefab.
            if (_currentOverrideModel != null)
            {
                Destroy(_currentOverrideModel);
                _currentOverrideModel = null;
            }

            // Restore default model.
            if (defaultModel != null)
                defaultModel.SetActive(true);

            // Destroy VFX.
            if (_currentVfx != null)
            {
                Destroy(_currentVfx);
                _currentVfx = null;
            }

            _equippedCosmeticId = null;
        }

        /// <summary>The cosmetic id currently applied, or null if none.</summary>
        public string EquippedCosmeticId => _equippedCosmeticId;

        // ── Private helpers ───────────────────────────────────────────────────

        private void ApplyMaterial(CosmeticDef def)
        {
            if (meshRenderer == null) return;

            if (materialOverride != null)
            {
                // Inspector-assigned material override — full swap.
                meshRenderer.material = materialOverride;
            }
            else
            {
                // First-pass: tint by preview color. Creates a material instance
                // (not shared) so other instances are unaffected.
                var mat = meshRenderer.material; // creates instance
                mat.color = def.PreviewUnityColor;
            }
        }

        private void ApplyPrefab()
        {
            if (prefabOverride == null) return;

            // Tear down any prior override.
            if (_currentOverrideModel != null)
                Destroy(_currentOverrideModel);

            if (defaultModel != null)
                defaultModel.SetActive(false);

            var parent = attachmentPoint != null ? attachmentPoint : transform;
            _currentOverrideModel = Instantiate(prefabOverride, parent);
            _currentOverrideModel.transform.localPosition = Vector3.zero;
            _currentOverrideModel.transform.localRotation = Quaternion.identity;
        }

        private void ApplyVfx()
        {
            if (vfxPrefab == null) return;

            // Destroy any prior VFX.
            if (_currentVfx != null)
                Destroy(_currentVfx);

            var parent = attachmentPoint != null ? attachmentPoint : transform;
            _currentVfx = Instantiate(vfxPrefab, parent);
            _currentVfx.transform.localPosition = Vector3.zero;
            _currentVfx.transform.localRotation = Quaternion.identity;
        }
    }
}
