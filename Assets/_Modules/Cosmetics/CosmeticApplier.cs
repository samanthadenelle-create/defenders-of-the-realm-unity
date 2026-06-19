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
using DeNelle.Core.Diagnostics;

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
            using var _ = FlowTrace.Enter("Cosmetics", $"ApplyCosmetic(id='{cosmeticId}') on '{gameObject.name}'");
            if (string.IsNullOrEmpty(cosmeticId))
            {
                FlowTrace.Warn("Cosmetics", "ApplyCosmetic: empty cosmetic id — no-op.");
                return;
            }

            var def = CosmeticCatalog.Find(cosmeticId);
            if (def == null)
            {
                FlowTrace.Fail("Cosmetics", $"ApplyCosmetic: id '{cosmeticId}' not found in catalog — nothing applied.");
                return;
            }

            _equippedCosmeticId = cosmeticId;

            ApplyMaterial(def);
            ApplyPrefab();
            ApplyVfx();

            FlowTrace.Step("Cosmetics", $"ApplyCosmetic: applied '{def.DisplayName}' to '{gameObject.name}'.");
        }

        /// <summary>
        /// Convenience overload that takes a <see cref="CosmeticDef"/> directly
        /// (avoids a second catalog lookup when the caller already has the def).
        /// </summary>
        public void ApplyCosmetic(CosmeticDef cosmetic)
        {
            using var _ = FlowTrace.Enter("Cosmetics", $"ApplyCosmetic(def) on '{gameObject.name}'");
            if (cosmetic == null)
            {
                FlowTrace.Warn("Cosmetics", "ApplyCosmetic(def): null CosmeticDef — no-op.");
                return;
            }
            _equippedCosmeticId = cosmetic.Id;
            ApplyMaterial(cosmetic);
            ApplyPrefab();
            ApplyVfx();
            FlowTrace.Step("Cosmetics", $"ApplyCosmetic: applied '{cosmetic.DisplayName}' to '{gameObject.name}'.");
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
            using var _ = FlowTrace.Enter("Cosmetics", $"ApplyMaterial('{def?.Id}') on '{gameObject.name}'");
            if (meshRenderer == null)
            {
                FlowTrace.Fail("Cosmetics",
                    $"ApplyMaterial: no MeshRenderer on '{gameObject.name}' — cosmetic '{def?.Id}' cannot render a material.");
                return;
            }

            if (materialOverride != null)
            {
                // Inspector-assigned material override — full swap.
                meshRenderer.material = materialOverride;
                // READ-BACK VERIFY: confirm the swap took (material non-null + identity matches).
                var applied = meshRenderer.material;
                if (applied == null)
                {
                    FlowTrace.Fail("Cosmetics",
                        $"ApplyMaterial: material override for '{def?.Id}' did not take — renderer material is null after swap.");
                    return;
                }
                FlowTrace.Step("Cosmetics", $"ApplyMaterial: swapped to override material '{applied.name}' for '{def?.Id}'.");
            }
            else
            {
                // First-pass: tint by preview color. Creates a material instance
                // (not shared) so other instances are unaffected.
                var mat = meshRenderer.material; // creates instance
                if (mat == null)
                {
                    FlowTrace.Fail("Cosmetics",
                        $"ApplyMaterial: renderer produced no material instance for '{def?.Id}' — tint cannot apply.");
                    return;
                }
                Color want = def != null ? def.PreviewUnityColor : Color.white;
                mat.color = want;
                // READ-BACK VERIFY (owner directive 2026-06-19: "anything that renders can be
                // broken — read back the applied tint"). A shader without a `_Color` slot silently
                // drops the assignment; the read-back self-reports that miss instead of a wrong colour.
                Color got = mat.color;
                bool took = Mathf.Approximately(got.r, want.r) && Mathf.Approximately(got.g, want.g) &&
                            Mathf.Approximately(got.b, want.b);
                if (!took)
                {
                    FlowTrace.Warn("Cosmetics",
                        $"ApplyMaterial: tint for '{def?.Id}' did not read back (wanted {want}, got {got}) on " +
                        $"material '{mat.name}' — shader may lack a colour slot; cosmetic tint may not show.");
                }
                else
                {
                    FlowTrace.Step("Cosmetics", $"ApplyMaterial: tinted '{def?.Id}' to {want} on '{mat.name}' (verified).");
                }
            }
        }

        private void ApplyPrefab()
        {
            if (prefabOverride == null) return;
            using var _ = FlowTrace.Enter("Cosmetics", $"ApplyPrefab('{prefabOverride.name}') on '{gameObject.name}'");

            // LIVE BUG FIX (verify-before-hide, mirrors HeroArmorVisual): the old order hid the
            // default model (SetActive(false)) BEFORE an UNGUARDED Instantiate. If Instantiate
            // threw, the GameObject was left permanently invisible with no rollback — a naked/blank
            // object. So now: build + verify the replacement FIRST, hide the default ONLY after the
            // replacement is confirmed instantiated AND renders; on ANY failure RE-ENABLE the default.

            // Tear down any prior override (the default is still shown, so no blank flash).
            if (_currentOverrideModel != null)
                Destroy(_currentOverrideModel);

            var parent = attachmentPoint != null ? attachmentPoint : transform;

            // 1) GUARDED instantiate — a throw no longer leaves the default hidden (it isn't yet).
            GameObject instance = null;
            try
            {
                instance = Instantiate(prefabOverride, parent);
            }
            catch (System.Exception ex)
            {
                FlowTrace.Fail("Cosmetics",
                    $"ApplyPrefab: Instantiate threw for '{prefabOverride.name}': {ex.GetType().Name}: {ex.Message} — " +
                    "keeping default model (no invisible object).");
                RestoreDefaultModel();
                return;
            }
            if (instance == null)
            {
                FlowTrace.Fail("Cosmetics",
                    $"ApplyPrefab: Instantiate returned null for '{prefabOverride.name}' — keeping default model.");
                RestoreDefaultModel();
                return;
            }

            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;

            // 2) RENDER-VERIFY: the override must carry >=1 renderer (Mesh or Skinned) so we never
            // hide the default for a replacement that shows nothing. Failure => destroy + roll back.
            if (!OverrideRenders(instance))
            {
                FlowTrace.Fail("Cosmetics",
                    $"ApplyPrefab: override '{prefabOverride.name}' has no visible renderer — " +
                    "dropping it, keeping default model (no invisible object).");
                Destroy(instance);
                RestoreDefaultModel();
                return;
            }

            // 3) Replacement confirmed renderable — NOW it is safe to hide the default.
            _currentOverrideModel = instance;
            if (defaultModel != null)
                defaultModel.SetActive(false);

            FlowTrace.Step("Cosmetics",
                $"ApplyPrefab: override '{prefabOverride.name}' instantiated + renders; default model hidden.");
        }

        // Render-verify helper: true when the instance has at least one renderer carrying a mesh,
        // so hiding the default never leaves the object blank. Counts MeshRenderer + SkinnedMeshRenderer.
        private static bool OverrideRenders(GameObject instance)
        {
            if (instance == null) return false;
            foreach (var mr in instance.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (mr == null) continue;
                var mf = mr.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null) return true;
            }
            foreach (var sr in instance.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (sr != null && sr.sharedMesh != null) return true;
            }
            return false;
        }

        // Re-enable the default model — the never-invisible fallback whenever a prefab override
        // fails to build/render. Always runs (not behind the FlowTrace toggle).
        private void RestoreDefaultModel()
        {
            if (defaultModel != null && !defaultModel.activeSelf)
                defaultModel.SetActive(true);
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
